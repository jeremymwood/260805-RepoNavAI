import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { CapabilitySummary } from './RepositoryWorkspacePage';

describe('CapabilitySummary', () => {
  it('states available and unavailable repository capabilities', () => {
    const markup = renderToStaticMarkup(<CapabilitySummary capabilities={{ hasIndexedContent: true, hasSourceCode: true, hasTests: false, hasDocumentation: true, hasApiEndpoints: false, representativePaths: [] }}/>);

    expect(markup).toContain('Source search: Available');
    expect(markup).toContain('Tests: Not detected');
    expect(markup).toContain('API endpoints: Not detected');
  });

  it('explains an index without supported executable source', () => {
    const markup = renderToStaticMarkup(<CapabilitySummary capabilities={{ hasIndexedContent: true, hasSourceCode: false, hasTests: false, hasDocumentation: true, hasApiEndpoints: false, representativePaths: ['README.md'] }}/>);

    expect(markup).toContain('Executable source support is limited');
  });
});
