import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { RepositoryRemovalDialog, confirmationMatches } from './RepositoryRemovalDialog';
import type { RegisteredRepository } from './types';

const repository: RegisteredRepository = { id: 'id', organizationId: 'org', owner: 'acme', name: 'platform', fullName: 'acme/platform', defaultBranch: 'main', visibility: 'Private', webUrl: 'https://github.com/acme/platform', indexingStatus: 'Completed', indexingCheckpoint: 'Completed', registeredAtUtc: '2026-08-14T00:00:00Z', isFavorite: false };

describe('RepositoryRemovalDialog', () => {
  it('requires the exact repository identity while tolerating surrounding whitespace and case', () => {
    expect(confirmationMatches(' acme/platform ', repository.fullName)).toBe(true);
    expect(confirmationMatches('ACME/PLATFORM', repository.fullName)).toBe(true);
    expect(confirmationMatches('acme/other', repository.fullName)).toBe(false);
  });
  it('labels the blocking dialog and explains the destructive boundary', () => {
    const markup = renderToStaticMarkup(<RepositoryRemovalDialog repository={repository} removing={false} onRemove={vi.fn()} onClose={vi.fn()}/>);
    expect(markup).toContain('aria-labelledby="remove-repository-title"');
    expect(markup).toContain('aria-describedby="remove-repository-impact"');
    expect(markup).toContain('never changes the source GitHub repository');
    expect(markup).toContain('disabled=""');
  });
});
