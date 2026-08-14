import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { RepositoryAssistantHistory } from './RepositoryAssistantHistory';

describe('RepositoryAssistantHistory', () => {
  it('labels the private per-user history surface while it loads', () => {
    const markup = renderToStaticMarkup(<RepositoryAssistantHistory organizationId="organization" repositoryId="repository" refreshKey={0} onOpen={vi.fn()}/>);

    expect(markup).toContain('aria-labelledby="assistant-history-title"');
    expect(markup).toContain('Your recent results');
    expect(markup).toContain('Private to you');
    expect(markup).toContain('Loading recent results');
  });
});
