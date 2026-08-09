import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { getIndexingStatusPresentation, IndexingStatusBadge } from './IndexingStatusBadge';
import type { IndexingRequestStatus } from './types';

const statuses: IndexingRequestStatus[] = ['Pending', 'Processing', 'Completed', 'Failed', 'Cancelled'];

describe('IndexingStatusBadge', () => {
  it.each(statuses)('renders an accessible text and icon treatment for %s', status => {
    const markup = renderToStaticMarkup(<IndexingStatusBadge status={status}/>);
    const presentation = getIndexingStatusPresentation(status);

    expect(markup).toContain(`aria-label="${presentation.description}"`);
    expect(markup).toContain(`>${presentation.label}</span>`);
    expect(markup).toContain('aria-hidden="true"');
  });

  it('uses motion-safe activity styling for processing', () => {
    const markup = renderToStaticMarkup(<IndexingStatusBadge status="Processing"/>);

    expect(markup).toContain('animate-spin');
    expect(markup).toContain('motion-reduce:animate-none');
  });

  it('falls back safely for a future unknown status', () => {
    const markup = renderToStaticMarkup(<IndexingStatusBadge status="Paused"/>);

    expect(markup).toContain('Repository indexing status is unknown');
    expect(markup).toContain('>Unknown</span>');
  });
});
