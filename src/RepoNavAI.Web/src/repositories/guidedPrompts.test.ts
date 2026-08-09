import { describe, expect, it } from 'vitest';
import { applicableGuidedPrompts, nextGuidedPromptSet } from './guidedPrompts';

describe('guided repository prompts', () => {
  it('does not suggest endpoint prompts when the repository has no endpoints', () => {
    const prompts = applicableGuidedPrompts({ hasIndexedContent: true, hasSourceCode: true, hasTests: true, hasDocumentation: true, apiEndpoints: false, representativePaths: ['README.md'] });

    expect(prompts).not.toContainEqual(expect.objectContaining({ requires: 'apiEndpoints' }));
    expect(new Set(prompts.map(prompt => prompt.mode))).toEqual(new Set(['Search', 'Answer', 'Orientation']));
  });

  it('includes endpoint prompts when that capability is detected', () => {
    const prompts = applicableGuidedPrompts({ hasIndexedContent: true, hasSourceCode: true, hasTests: true, hasDocumentation: true, apiEndpoints: true, representativePaths: ['README.md'] });

    expect(prompts).toContainEqual(expect.objectContaining({ id: 'api-auth', mode: 'CodeFlow' }));
  });

  it('rotates to a different non-repeating set and wraps safely', () => {
    const prompts = applicableGuidedPrompts({ hasIndexedContent: true, hasSourceCode: true, hasTests: true, hasDocumentation: true, apiEndpoints: false, representativePaths: ['README.md'] });
    const first = nextGuidedPromptSet(prompts, 0);
    const second = nextGuidedPromptSet(prompts, first.length);

    expect(first).toHaveLength(3);
    expect(second).toHaveLength(3);
    expect(second.map(prompt => prompt.id)).not.toEqual(first.map(prompt => prompt.id));
    expect(nextGuidedPromptSet(prompts, prompts.length - 1)).toHaveLength(3);
  });

  it('returns no misleading prompts when indexing produced no searchable content', () => {
    expect(applicableGuidedPrompts({ hasIndexedContent: false, hasSourceCode: false, hasTests: false, hasDocumentation: true, apiEndpoints: false, representativePaths: ['README.md'] })).toEqual([]);
  });
});
