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
  isFavorite: boolean;
}

export interface RepositoryPage {
  items: RegisteredRepository[];
  page: number;
  pageSize: number;
  totalCount: number;
  hasMore: boolean;
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

export type OrientationRole = 'Developer' | 'Tester' | 'Architect' | 'DevOps' | 'Product';
export type OrientationExperience = 'NewToSoftware' | 'Junior' | 'MidLevel' | 'Senior';
export type OrientationFocus = 'GeneralOnboarding' | 'ImplementFeature' | 'FixBug' | 'Architecture' | 'Operations';
export interface OrientationCitation { path: string; startLine: number; endLine: number; commitSha: string; sourceUrl: string }
export interface OrientationStep { key: string; title: string; objective: string; evidence: string; evidenceLevel: 'Confirmed' | 'Inferred' | 'Missing'; citations: OrientationCitation[]; completed: boolean }
export interface OrientationPlan {
  id: string; repositoryId: string; commitSha: string; role: OrientationRole; experience: OrientationExperience;
  focus: OrientationFocus; timeBudgetMinutes: number; summary: string; steps: OrientationStep[];
  missingEvidence: string[]; isStale: boolean; createdAtUtc: string;
}

export type CodeFlowBoundary = 'Synchronous' | 'Asynchronous' | 'Background' | 'Persistence' | 'External';
export interface CodeFlowStep {
  key: string; order: number; title: string; component: string; symbol: string; responsibility: string; handoff: string;
  boundary: CodeFlowBoundary; evidenceLevel: 'Confirmed' | 'Inferred' | 'Missing'; citations: OrientationCitation[];
}
export interface CodeFlowTrace {
  schemaVersion: string; repositoryId: string; commitSha: string; summary: string; steps: CodeFlowStep[]; missingEvidence: string[]; sources: SemanticSearchResult[];
}
