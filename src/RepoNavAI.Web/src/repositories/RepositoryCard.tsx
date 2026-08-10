import { Compass, ExternalLink, GitBranch, LockKeyhole } from 'lucide-react';
import { IndexingStatusBadge } from './IndexingStatusBadge';
import type { RegisteredRepository } from './types';

interface RepositoryCardProps {
  repository: RegisteredRepository;
  selected: boolean;
  onCancel: () => void;
  onRetry: () => void;
  onExplore: () => void;
}

export function RepositoryCard({ repository, selected, onCancel, onRetry, onExplore }: RepositoryCardProps) {
  return <article className={`min-w-0 overflow-hidden rounded-xl border p-4 ${selected ? 'border-brand-500 ring-2 ring-brand-100' : 'border-slate-200'}`}>
    <a className="group block min-w-0 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500" href={repository.webUrl} target="_blank" rel="noreferrer" title={repository.fullName}>
      <span className="block break-all text-xs font-medium text-slate-500">{repository.owner}/</span>
      <span className="mt-0.5 flex min-w-0 items-start gap-1 text-base font-semibold leading-5 text-ink group-hover:text-brand-600">
        <span className="min-w-0 break-words [overflow-wrap:anywhere]">{repository.name}</span>
        <ExternalLink aria-hidden="true" size={13} className="mt-0.5 shrink-0"/>
      </span>
    </a>
    <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2">
      <IndexingStatusBadge status={repository.indexingStatus}/>
      <span className="inline-flex min-w-0 items-center gap-1 text-xs text-slate-500">
        {repository.visibility === 'Private' ? <LockKeyhole aria-hidden="true" size={13}/> : <GitBranch aria-hidden="true" size={13}/>} {repository.visibility} · <span className="break-all">{repository.defaultBranch}</span>
      </span>
    </div>
    <p className="mt-2 break-words text-xs text-slate-400">{repository.indexingCheckpoint}{repository.commitSha ? ` · ${repository.commitSha.slice(0, 8)}` : ''}</p>
    {repository.errorMessage && <p className="mt-3 break-words text-xs font-medium leading-5 text-red-600">{repository.errorMessage}</p>}
    <div className="mt-3 flex flex-wrap gap-x-4 gap-y-2">
      {(['Pending', 'Processing'].includes(repository.indexingStatus)) && <button className="text-xs font-semibold text-slate-500 hover:text-red-600" onClick={onCancel}>Cancel</button>}
      {(['Failed', 'Cancelled'].includes(repository.indexingStatus)) && <button className="text-xs font-semibold text-brand-600" onClick={onRetry}>Retry indexing</button>}
      {repository.indexingStatus === 'Completed' && <button className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand-600" onClick={onExplore}><Compass aria-hidden="true" size={14}/> Explore repository</button>}
    </div>
  </article>;
}
