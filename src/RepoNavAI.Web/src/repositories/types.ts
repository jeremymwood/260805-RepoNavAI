export type RepositoryVisibility = 'Public' | 'Private';
export type IndexingRequestStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled';

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
  registeredAtUtc: string;
}
