import { expect, test as base, type Page } from '@playwright/test';
import type { IndexingRequestStatus, RegisteredRepository } from '../../src/repositories/types';

const test = base.extend<{ browserErrors: void }>({
  browserErrors: [async ({ page }, use) => {
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    await use();
    expect(errors, 'No unhandled browser errors').toEqual([]);
  }, { auto: true }],
});

const repository: RegisteredRepository = {
  id: 'repository-one', organizationId: 'organization-one', owner: 'test', name: 'status-fixture',
  fullName: 'test/status-fixture', defaultBranch: 'main', visibility: 'Public',
  webUrl: 'https://github.com/test/status-fixture', indexingStatus: 'Pending', indexingCheckpoint: 'Queued',
  commitSha: '12345678abcdef', registeredAtUtc: '2026-10-15T12:00:00Z', isFavorite: false,
};
const workspaceUrl = `/repositories/${repository.id}`;

async function mockWorkspace(page: Page, initialStatus: IndexingRequestStatus = 'Pending') {
  const state = {
    status: initialStatus,
    indexedContent: false,
    generation: 0,
    fastReindex: false,
    repositoryReads: 0,
    reindexRequests: 0,
    failedReadsRemaining: 0,
    unexpectedRequests: [] as string[],
    complete() { this.status = 'Completed'; this.indexedContent = true; this.generation += 1; },
  };
  await page.route('**/health', route => route.fulfill({ status: 200, body: 'Healthy' }));
  await page.route('http://127.0.0.1:4174/api/**', async route => {
    const { pathname } = new URL(route.request().url());
    const method = route.request().method();
    if (pathname === '/api/auth/me') return route.fulfill({ json: { id: 'user-one', email: 'tester@example.invalid', displayName: 'Workspace Tester', roles: [] } });
    if (pathname === '/api/organizations') return route.fulfill({ json: [{ id: repository.organizationId, name: 'Test organization', slug: 'test', role: 'Owner' }] });
    const collection = `/api/organizations/${repository.organizationId}/repositories`;
    if (pathname === collection && method === 'GET') {
      state.repositoryReads += 1;
      if (state.failedReadsRemaining > 0) {
        state.failedReadsRemaining -= 1;
        return route.fulfill({ status: 503, json: { title: 'Temporary fixture outage' } });
      }
      return route.fulfill({ json: {
        items: [
          { ...repository, indexingStatus: state.status, indexingCheckpoint: state.status === 'Processing' ? 'Parsing' : state.status },
          { ...repository, id: 'other-repository', name: 'unrelated-job', indexingStatus: 'Pending' },
        ], page: 1, pageSize: 50, totalCount: 2, hasMore: false,
      } });
    }
    const resource = `${collection}/${repository.id}`;
    if (pathname === `${resource}/indexing/reindex` && method === 'POST') {
      state.reindexRequests += 1;
      if (state.fastReindex) state.complete(); else state.status = 'Pending';
      return route.fulfill({ status: 202, json: { id: 'job-one' } });
    }
    if (pathname === `${resource}/capabilities`) return route.fulfill({ json: {
      hasIndexedContent: state.indexedContent, hasSourceCode: state.indexedContent,
      hasDocumentation: true, hasTests: false, hasApiEndpoints: state.indexedContent, representativePaths: [],
    } });
    if (pathname === `${resource}/endpoints`) return route.fulfill({ json: [{
      id: 'endpoint-one', httpMethod: 'GET', route: `/generation-${state.generation}`, handler: 'Get',
      path: 'Controller.cs', line: 1, requiresAuthorization: true, downstreamSymbols: [],
      commitSha: repository.commitSha, sourceUrl: `${repository.webUrl}/blob/${repository.commitSha}/Controller.cs#L1`,
    }] });
    if (pathname === `${resource}/architecture`) return route.fulfill({ json: {
      schemaVersion: '1', commitSha: repository.commitSha, isTruncated: false, totalNodeCount: 1,
      nodes: [{ id: 'module-one', label: `Module generation ${state.generation}`, kind: 'Module', childCount: 1 }], edges: [],
    } });
    if (pathname === `${resource}/orientation-plan`) return route.fulfill({ status: 204 });
    if (pathname === `${resource}/assistant/history`) return route.fulfill({ json: { items: [], page: 1, pageSize: 10, totalCount: 0, hasMore: false } });
    state.unexpectedRequests.push(`${method} ${pathname}`);
    return route.fulfill({ status: 501, json: { title: 'Unexpected test request' } });
  });
  return state;
}

async function expectPollingStopped(page: Page, state: Awaited<ReturnType<typeof mockWorkspace>>) {
  const reads = state.repositoryReads;
  // Observe more than two polling periods; do not trigger a focus event or reload.
  await page.clock.runFor(6_500);
  expect(state.repositoryReads).toBe(reads);
  expect(state.unexpectedRequests).toEqual([]);
}

test.beforeEach(async ({ page }) => { await page.clock.install(); });

test('Pending and Processing automatically reopen the requested assistant view on completion', async ({ page }) => {
  const state = await mockWorkspace(page);
  await page.goto(`${workspaceUrl}?view=assistant`);
  await expect(page.getByRole('status')).toContainText('Current indexing status: Pending');
  await expect(page.getByRole('status')).toContainText('Status refreshes automatically');
  state.status = 'Processing';
  await expect(page.getByRole('status')).toContainText('Current indexing status: Processing', { timeout: 10_000 });
  state.complete();
  await expect(page.getByRole('heading', { name: 'Repository assistant', exact: true, level: 2 })).toBeVisible({ timeout: 10_000 });
  await expect(page.getByRole('button', { name: /^Source search/ })).toBeVisible();
  await expect(page).toHaveURL(`${workspaceUrl}?view=assistant`);
  expect(state.reindexRequests).toBe(0);
  await expectPollingStopped(page, state);
});

test('same-commit reindex refreshes capabilities and reveals newly available tools', async ({ page }) => {
  const state = await mockWorkspace(page, 'Completed');
  await page.goto(workspaceUrl);
  await expect(page.getByRole('button', { name: 'Re-index this repository' })).toBeVisible();
  await expect(page.getByRole('button', { name: /^Source search/ })).toHaveCount(0);
  await page.getByRole('button', { name: 'Re-index this repository' }).click();
  await expect(page.getByRole('status')).toContainText('Current indexing status: Pending');
  state.complete();
  await expect(page.getByRole('button', { name: /^Source search/ })).toBeVisible({ timeout: 10_000 });
  await expect(page.getByText('12345678', { exact: true })).toBeVisible();
  expect(state.reindexRequests).toBe(1);
  await expectPollingStopped(page, state);
});

test('completion invalidates cached endpoints and architecture at the same commit', async ({ page }) => {
  const state = await mockWorkspace(page, 'Completed');
  state.indexedContent = true;
  await page.goto(workspaceUrl);
  await page.getByRole('button', { name: /^API endpoints/ }).click();
  await expect(page.getByRole('cell', { name: '/generation-0', exact: true })).toBeVisible();
  await page.getByRole('button', { name: /^Architecture map/ }).click();
  await expect(page.getByRole('button', { name: /^Module generation 0/ })).toBeVisible();
  await page.getByRole('button', { name: /^Analysis summary/ }).click();
  await page.getByRole('button', { name: 'Re-index this repository' }).click();
  await expect(page.getByRole('status')).toContainText('Current indexing status: Pending');
  state.complete();
  await expect(page.getByRole('button', { name: /^API endpoints/ })).toBeVisible({ timeout: 10_000 });
  await page.getByRole('button', { name: /^API endpoints/ }).click();
  await expect(page.getByRole('cell', { name: '/generation-1', exact: true })).toBeVisible();
  await page.getByRole('button', { name: /^Architecture map/ }).click();
  await expect(page.getByRole('button', { name: /^Module generation 1/ })).toBeVisible();
  await expectPollingStopped(page, state);
});

test('a job finishing before Pending is observed still refreshes analysis', async ({ page }) => {
  const state = await mockWorkspace(page, 'Completed');
  state.fastReindex = true;
  await page.goto(workspaceUrl);
  await page.getByRole('button', { name: 'Re-index this repository' }).click();
  await expect(page.getByRole('button', { name: /^Source search/ })).toBeVisible();
  expect(state.reindexRequests).toBe(1);
  await expectPollingStopped(page, state);
});

for (const status of ['Failed', 'Cancelled'] as const) {
  test(`${status} stops polling and offers a path back to retry`, async ({ page }) => {
    const state = await mockWorkspace(page, 'Processing');
    await page.goto(workspaceUrl);
    await expect(page.getByRole('status')).toContainText('Current indexing status: Processing');
    state.status = status;
    await expect(page.getByRole('status')).toContainText(`Current indexing status: ${status}`, { timeout: 10_000 });
    await expect(page.getByRole('status')).toContainText('Return to the overview to retry indexing');
    await expect(page.getByRole('link', { name: 'Back to repositories' })).toHaveAttribute('href', '/#repositories');
    await expectPollingStopped(page, state);
  });
}

test('a completed repository does not poll for another repository indexing in the organization', async ({ page }) => {
  const state = await mockWorkspace(page, 'Completed');
  await page.goto(workspaceUrl);
  await expect(page.getByRole('button', { name: 'Re-index this repository' })).toBeVisible();
  await expectPollingStopped(page, state);
});

test('leaving the workspace stops its active polling', async ({ page }) => {
  const state = await mockWorkspace(page, 'Processing');
  await page.goto(workspaceUrl);
  await expect(page.getByRole('status')).toContainText('Current indexing status: Processing');
  await page.getByRole('link', { name: 'Profile settings' }).click();
  await expect(page.getByRole('heading', { name: 'Profile', exact: true })).toBeVisible();
  await expectPollingStopped(page, state);
});

test('polling recovers after a temporary status API failure', async ({ page }) => {
  const state = await mockWorkspace(page, 'Processing');
  await page.goto(workspaceUrl);
  await expect(page.getByRole('status')).toContainText('Current indexing status: Processing');
  state.failedReadsRemaining = 2; // Initial attempt and the configured query retry.
  await expect(page.getByRole('heading', { name: 'Repository unavailable' })).toBeVisible({ timeout: 10_000 });
  state.complete();
  await expect(page.getByRole('button', { name: /^Source search/ })).toBeVisible({ timeout: 10_000 });
  await expectPollingStopped(page, state);
});
