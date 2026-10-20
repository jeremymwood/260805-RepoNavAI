import type { QueryClient } from '@tanstack/react-query';
import type { IndexingRequestStatus } from './types';

export interface RepositoryIndexState {
  organizationId: string;
  repositoryId: string;
  status: IndexingRequestStatus;
}

export function indexingPollInterval(status?: IndexingRequestStatus): 3000 | false {
  return status === 'Pending' || status === 'Processing' ? 3000 : false;
}

export function indexingCompleted(previous: RepositoryIndexState | undefined, current: RepositoryIndexState | undefined): boolean {
  return Boolean(previous && current
    && previous.organizationId === current.organizationId
    && previous.repositoryId === current.repositoryId
    && indexingPollInterval(previous.status)
    && current.status === 'Completed');
}

export async function refreshRepositoryAnalysis(queryClient: QueryClient, organizationId: string, repositoryId: string) {
  // Invalidate filtered endpoint variants as well as inactive tools. Commit-based
  // keys alone cannot detect a repaired/backfilled index at the same commit.
  await Promise.all(['repository-capabilities', 'endpoint-catalog', 'repository-architecture'].map(key =>
    queryClient.invalidateQueries({ queryKey: [key, organizationId, repositoryId] })));
}
