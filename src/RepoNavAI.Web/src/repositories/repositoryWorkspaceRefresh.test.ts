import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it } from 'vitest';
import { indexingCompleted, indexingPollInterval, refreshRepositoryAnalysis, type RepositoryIndexState } from './repositoryWorkspaceRefresh';
import type { IndexingRequestStatus } from './types';

const state: RepositoryIndexState = { organizationId: 'org', repositoryId: 'repo', status: 'Pending' };

describe('workspace indexing refresh', () => {
  it.each<IndexingRequestStatus>(['Pending', 'Processing'])('polls %s every three seconds', status => {
    expect(indexingPollInterval(status)).toBe(3000);
    expect(indexingCompleted({ ...state, status }, { ...state, status: 'Completed' })).toBe(true);
  });

  it.each<IndexingRequestStatus | undefined>(['Completed', 'Failed', 'Cancelled', undefined])('does not poll %s', status => {
    expect(indexingPollInterval(status)).toBe(false);
  });

  it('does not treat a different repository, organization, or initial load as completion', () => {
    expect(indexingCompleted(state, { ...state, repositoryId: 'other', status: 'Completed' })).toBe(false);
    expect(indexingCompleted(state, { ...state, organizationId: 'other', status: 'Completed' })).toBe(false);
    expect(indexingCompleted(undefined, { ...state, status: 'Completed' })).toBe(false);
    expect(indexingCompleted(state, undefined)).toBe(false);
  });

  it('only refreshes for active-to-completed transitions', () => {
    for (const status of ['Pending', 'Processing', 'Failed', 'Cancelled'] as const) {
      expect(indexingCompleted(state, { ...state, status })).toBe(false);
    }
    for (const status of ['Completed', 'Failed', 'Cancelled'] as const) {
      expect(indexingCompleted({ ...state, status }, { ...state, status: 'Completed' })).toBe(false);
    }
  });

  it('invalidates all scoped analysis variants without touching another tenant, repository, or saved results', async () => {
    const client = new QueryClient();
    const targeted = [
      ['repository-capabilities', 'org', 'repo'],
      ['repository-architecture', 'org', 'repo'],
      ['endpoint-catalog', 'org', 'repo', '', '', ''],
      ['endpoint-catalog', 'org', 'repo', 'GET', 'users', 'true'],
    ];
    const untouched = [
      ['repository-capabilities', 'other-org', 'repo'],
      ['repository-capabilities', 'org', 'other-repo'],
      ['repository-architecture', 'other-org', 'repo'],
      ['endpoint-catalog', 'org', 'other-repo'],
      ['assistant-history', 'org', 'repo'],
    ];
    try {
      for (const key of [...targeted, ...untouched]) client.setQueryData(key, { commitSha: 'unchanged' });
      await refreshRepositoryAnalysis(client, 'org', 'repo');
      for (const key of targeted) expect(client.getQueryState(key)?.isInvalidated).toBe(true);
      for (const key of untouched) expect(client.getQueryState(key)?.isInvalidated).toBe(false);
    } finally {
      client.clear();
    }
  });
});
