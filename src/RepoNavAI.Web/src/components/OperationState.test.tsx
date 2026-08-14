import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { OperationState, RetryButton } from './OperationState';

describe('OperationState', () => {
  it('announces ordinary progress politely without claiming a percentage', () => {
    const markup = renderToStaticMarkup(<OperationState kind="progress" title="Indexing repository" message="Parsing source files."/>);
    expect(markup).toContain('role="status"');
    expect(markup).toContain('aria-live="polite"');
    expect(markup).toContain('aria-atomic="true"');
    expect(markup).toContain('motion-reduce:animate-none');
    expect(markup).not.toMatch(/\d+%/);
  });

  it.each(['timeout', 'failure'] as const)('announces %s as an alert', kind => {
    const markup = renderToStaticMarkup(<OperationState kind={kind} title="Request unavailable"/>);
    expect(markup).toContain('role="alert"');
    expect(markup).toContain('aria-live="assertive"');
  });

  it('renders stopped state without animation', () => {
    const markup = renderToStaticMarkup(<OperationState kind="stopped" title="Request stopped"/>);
    expect(markup).toContain('role="status"');
    expect(markup).not.toContain('animate-spin');
  });

  it('supports a reachable retry action', () => {
    const markup = renderToStaticMarkup(<OperationState kind="failure" title="Request failed" action={<RetryButton onRetry={vi.fn()}/>}/>);
    expect(markup).toContain('<button');
    expect(markup).toContain('Retry');
  });

  it('does not create a dismissible or blocking overlay', () => {
    const markup = renderToStaticMarkup(<OperationState kind="loading" title="Loading endpoint catalog"/>);
    expect(markup).not.toContain('role="dialog"');
    expect(markup).not.toContain('aria-modal');
    expect(markup).not.toContain('inert');
    expect(markup).not.toContain('Close');
  });
});
