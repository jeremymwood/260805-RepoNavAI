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

export interface RepositoryEndpoint {
  id: string; httpMethod: string; route: string; handler: string; path: string; line: number;
  requiresAuthorization: boolean; downstreamSymbols: string[]; commitSha: string; sourceUrl: string;
}

export interface SemanticSearchResult {
  chunkId: string; path: string; startLine: number; endLine: number; content: string;
  score: number; commitSha: string; sourceUrl: string;
}

export interface RepositoryChatCitation {
  number: number; path: string; startLine: number; endLine: number; commitSha: string; sourceUrl: string; score: number;
}

export type RepositoryChatEvent =
  | { type: 'Citations'; citations: RepositoryChatCitation[]; delta?: never }
  | { type: 'Delta' | 'Error'; delta: string; citations?: never }
  | { type: 'Completed'; delta?: never; citations?: never };
