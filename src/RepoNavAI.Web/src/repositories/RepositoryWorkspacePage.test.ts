import { describe, expect, it } from 'vitest';
import { findRepository, resolveWorkspaceView, visibleEndpointCount, type RepositoryCapabilities } from './RepositoryWorkspacePage';
import type { RepositoryPage } from './types';

const capabilities: RepositoryCapabilities = { hasIndexedContent: true, hasSourceCode: true, hasTests: true, hasDocumentation: true, hasApiEndpoints: true, representativePaths: [] };
describe('repository workspace state', () => {
  it('selects a repository from the paginated repository response', () => {
    const page = { items: [{ id: 'selected' }, { id: 'other' }], page: 1, pageSize: 10, totalCount: 2, hasMore: false } as RepositoryPage;
    expect(findRepository(page, 'selected')?.id).toBe('selected');
    expect(findRepository(page, 'missing')).toBeUndefined();
  });
  it('only restores views backed by repository capabilities', () => {
    expect(resolveWorkspaceView('endpoints', capabilities)).toBe('endpoints');
    expect(resolveWorkspaceView('search', capabilities)).toBe('search');
    expect(resolveWorkspaceView('endpoints', { ...capabilities, hasApiEndpoints: false })).toBe('summary');
    expect(resolveWorkspaceView('unknown', capabilities)).toBe('summary');
  });
  it('bounds endpoint previews and expands to the filtered total', () => {
    expect(visibleEndpointCount(12, false)).toBe(5);
    expect(visibleEndpointCount(3, false)).toBe(3);
    expect(visibleEndpointCount(12, true)).toBe(12);
  });
});
