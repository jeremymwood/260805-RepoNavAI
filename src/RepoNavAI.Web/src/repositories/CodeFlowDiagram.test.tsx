import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { CodeFlowDiagram, validateCodeFlowTrace } from './CodeFlowDiagram';
import type { CodeFlowTrace } from './types';

const trace: CodeFlowTrace = {
  schemaVersion: '1.0', repositoryId: 'repository-id', commitSha: 'abc123', summary: 'A request crosses application boundaries.', missingEvidence: [],
  steps: [
    { key: 'entry', order: 1, title: 'Receive request', component: 'API', symbol: 'Run', responsibility: 'Accept the request.', handoff: 'Queue work.', boundary: 'Synchronous', evidenceLevel: 'Confirmed', citations: [{ path: 'src/Api.cs', startLine: 10, endLine: 15, commitSha: 'abc123', sourceUrl: 'https://github.com/example/repo/blob/abc123/src/Api.cs#L10-L15' }] },
    { key: 'worker', order: 2, title: 'Process work', component: 'Worker', symbol: 'Execute', responsibility: 'Run asynchronously.', handoff: 'Persist result.', boundary: 'Background', evidenceLevel: 'Inferred', citations: [] },
    { key: 'store', order: 3, title: 'Save result', component: 'Database', symbol: 'Save', responsibility: 'Commit state.', handoff: '', boundary: 'Persistence', evidenceLevel: 'Confirmed', citations: [{ path: 'src/Store.cs', startLine: 20, endLine: 25, commitSha: 'abc123', sourceUrl: 'https://github.com/example/repo/blob/abc123/src/Store.cs#L20-L25' }] }
  ]
};
const entry = trace.steps[0]!;
const worker = trace.steps[1]!;

describe('CodeFlowDiagram', () => {
  it('renders accessible nodes, boundary labels, edge certainty, zoom, and text fallback', () => {
    const markup = renderToStaticMarkup(<CodeFlowDiagram trace={trace}/>);
    expect(markup).toContain('Code flow diagram with 3 ordered nodes');
    expect(markup).toContain('Background work');
    expect(markup).toContain('Data store');
    expect(markup).toContain('Inferred handoff');
    expect(markup).toContain('aria-label="Zoom in"');
    expect(markup).toContain('overflow-auto');
    expect(markup).toContain('Ordered text trace');
  });

  it('exposes commit-pinned citations from the selected node', () => {
    const markup = renderToStaticMarkup(<CodeFlowDiagram trace={trace}/>);
    expect(markup).toContain('https://github.com/example/repo/blob/abc123/src/Api.cs#L10-L15');
    expect(markup).toContain('Selected source evidence');
  });

  it('rejects unsafe citation URLs and renders only the text fallback', () => {
    const unsafe: CodeFlowTrace = { ...trace, steps: [{ ...entry, citations: [{ ...entry.citations[0]!, sourceUrl: 'javascript:alert(1)' }] }] };
    expect(validateCodeFlowTrace(unsafe)).toContain('unsafe');
    const markup = renderToStaticMarkup(<CodeFlowDiagram trace={unsafe}/>);
    expect(markup).toContain('Diagram unavailable');
    expect(markup).not.toContain('javascript:alert');
  });

  it('fails safely for duplicate keys and oversized traces', () => {
    expect(validateCodeFlowTrace({ ...trace, steps: [entry, { ...worker, key: 'entry' }] })).toContain('duplicate');
    expect(validateCodeFlowTrace({ ...trace, steps: Array.from({ length: 25 }, (_, index) => ({ ...worker, key: `step-${index}`, order: index + 1 })) })).toContain('too large');
  });
});
