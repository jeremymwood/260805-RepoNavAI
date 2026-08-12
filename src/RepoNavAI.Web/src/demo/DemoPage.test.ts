import { describe, expect, it } from 'vitest';
import { demoRepositories, demoTrace, visibleDemoRepositories } from './DemoPage';
import { validateCodeFlowTrace } from '../repositories/CodeFlowDiagram';

describe('GitHub Pages fixture preview', () => {
  it('provides representative repository states and favorite treatment', () => {
    expect(new Set(demoRepositories.map(repository => repository.indexingStatus))).toEqual(new Set(['Completed', 'Processing', 'Failed', 'Cancelled']));
    expect(demoRepositories.some(repository => repository.isFavorite)).toBe(true);
  });

  it('bounds the overview until the visitor asks to show more', () => {
    expect(visibleDemoRepositories(demoRepositories, false)).toHaveLength(4);
    expect(visibleDemoRepositories(demoRepositories, true)).toHaveLength(demoRepositories.length);
  });

  it('ships a safe cited diagram fixture with raw evidence', () => {
    expect(validateCodeFlowTrace(demoTrace)).toBeUndefined();
    expect(demoTrace.sources).toHaveLength(3);
    expect(demoTrace.sources.every(source => source.sourceUrl.includes(`/blob/${demoTrace.commitSha}/`))).toBe(true);
  });
});
