import { describe, expect, it } from 'vitest';
import { resolveWorkspaceView, visibleEndpointCount, type RepositoryCapabilities } from './RepositoryWorkspacePage';

const capabilities: RepositoryCapabilities = { hasIndexedContent: true, hasSourceCode: true, hasTests: true, hasDocumentation: true, hasApiEndpoints: true, representativePaths: [] };
describe('repository workspace state', () => {
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
