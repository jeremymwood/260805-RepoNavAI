import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { RepositoryCard } from './RepositoryCard';
import type { RegisteredRepository } from './types';

const repository: RegisteredRepository = {
  id: 'repository-id', organizationId: 'organization-id', owner: 'very-long-organization-owner',
  name: 'a-repository-name-that-needs-readable-wrapping-on-mobile',
  fullName: 'very-long-organization-owner/a-repository-name-that-needs-readable-wrapping-on-mobile',
  defaultBranch: 'feature/a-long-default-branch', visibility: 'Public', webUrl: 'https://github.com/example/repository',
  indexingStatus: 'Completed', indexingCheckpoint: 'Completed', commitSha: '1234567890abcdef', registeredAtUtc: '2026-08-09T00:00:00Z', isFavorite: false
};

describe('RepositoryCard', () => {
  it('preserves complete repository identity and accessible status text', () => {
    const markup = renderToStaticMarkup(<RepositoryCard repository={repository} selected={false} onCancel={vi.fn()} onRetry={vi.fn()} onExplore={vi.fn()} onFavorite={vi.fn()}/>);
    expect(markup).toContain('very-long-organization-owner/');
    expect(markup).toContain('a-repository-name-that-needs-readable-wrapping-on-mobile');
    expect(markup).toContain(`title="${repository.fullName}"`);
    expect(markup).toContain('Repository indexing completed successfully');
  });

  it('contains failure details and exposes the retry action', () => {
    const failed = { ...repository, indexingStatus: 'Failed' as const, indexingCheckpoint: 'Failed' as const, errorMessage: 'Repository indexing failed safely.' };
    const markup = renderToStaticMarkup(<RepositoryCard repository={failed} selected={false} onCancel={vi.fn()} onRetry={vi.fn()} onExplore={vi.fn()} onFavorite={vi.fn()}/>);
    expect(markup).toContain('Repository indexing failed safely.');
    expect(markup).toContain(`aria-label="Retry indexing ${repository.fullName}"`);
    expect(markup).not.toContain('Explore repository');
  });

  it('animates and disables retry while the action is pending', () => {
    const failed = { ...repository, indexingStatus: 'Failed' as const, indexingCheckpoint: 'Failed' as const };
    const markup = renderToStaticMarkup(<RepositoryCard repository={failed} selected={false} retryPending onCancel={vi.fn()} onRetry={vi.fn()} onExplore={vi.fn()} onFavorite={vi.fn()}/>);
    expect(markup).toContain('disabled=""');
    expect(markup).toContain('animate-spin');
    expect(markup).toContain('motion-reduce:animate-none');
  });

  it('uses an animated cancel control while indexing is running', () => {
    const processing = { ...repository, indexingStatus: 'Processing' as const, indexingCheckpoint: 'Parsing' as const };
    const markup = renderToStaticMarkup(<RepositoryCard repository={processing} selected={false} onCancel={vi.fn()} onRetry={vi.fn()} onExplore={vi.fn()} onFavorite={vi.fn()}/>);
    expect(markup).toContain(`aria-label="Cancel indexing ${repository.fullName}"`);
    expect(markup).toContain('animate-spin');
    expect(markup).toContain('group-hover:hidden');
    expect(markup).not.toContain('>Cancel<');
  });

  it('exposes favorite state as an accessible toggle', () => {
    const markup = renderToStaticMarkup(<RepositoryCard repository={{ ...repository, isFavorite: true }} selected={false} onCancel={vi.fn()} onRetry={vi.fn()} onExplore={vi.fn()} onFavorite={vi.fn()}/>);
    expect(markup).toContain('aria-pressed="true"');
    expect(markup).toContain(`aria-label="Remove ${repository.fullName} from favorites"`);
  });

  it('places the commit next to the branch metadata', () => {
    const markup = renderToStaticMarkup(<RepositoryCard repository={repository} selected={false} onCancel={vi.fn()} onRetry={vi.fn()} onExplore={vi.fn()} onFavorite={vi.fn()}/>);
    expect(markup).toContain('feature/a-long-default-branch');
    expect(markup).toContain('12345678');
    expect(markup).not.toContain('indexingCheckpoint');
  });
});
