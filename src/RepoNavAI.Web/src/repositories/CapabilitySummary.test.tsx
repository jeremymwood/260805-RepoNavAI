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

  it('reports partial per-language coverage without claiming skipped files were analyzed', () => {
    const markup = renderToStaticMarkup(<CapabilitySummary capabilities={{ hasIndexedContent: true, hasSourceCode: true, hasTests: false, hasDocumentation: false, hasApiEndpoints: false, representativePaths: [], coverageStatus: 'partial', languages: [{ language: 'python', indexed: 12, skippedUnsupported: 0, skippedExcluded: 3, skippedBinary: 1 }] }}/>);
    expect(markup).toContain('Partial source coverage');
    expect(markup).toContain('python');
    expect(markup).toContain('>12<');
    expect(markup).toContain('>3<');
  });

  it('explains an index without supported executable source', () => {
    const markup = renderToStaticMarkup(<CapabilitySummary capabilities={{ hasIndexedContent: true, hasSourceCode: false, hasTests: false, hasDocumentation: true, hasApiEndpoints: false, representativePaths: ['README.md'] }}/>);

    expect(markup).toContain('Executable source support is limited');
  });
});
