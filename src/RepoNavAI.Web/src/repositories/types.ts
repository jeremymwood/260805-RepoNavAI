export type RepositoryVisibility = 'Public' | 'Private';
export type IndexingRequestStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled';
export type IndexingCheckpoint = 'Queued' | 'Acquiring' | 'Parsing' | 'Persisting' | 'Completed' | 'Failed' | 'Cancelled';

export interface RegisteredRepository {
  id: string;
  organizationId: string;
  owner: string;
  name: string;
  fullName: string;
  defaultBranch: string;
  visibility: RepositoryVisibility;
  webUrl: string;
  indexingStatus: IndexingRequestStatus;
  indexingCheckpoint: IndexingCheckpoint;
  commitSha?: string;
  errorMessage?: string;
  registeredAtUtc: string;
}
