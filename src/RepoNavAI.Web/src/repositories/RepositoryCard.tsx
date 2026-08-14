import { Compass, ExternalLink, GitBranch, LockKeyhole, RefreshCw, Star, X } from 'lucide-react';
import { AppIcon } from '../components/AppIcon';
import { IndexingStatusBadge } from './IndexingStatusBadge';
import type { RegisteredRepository } from './types';

interface RepositoryCardProps {
  repository: RegisteredRepository;
  selected: boolean;
  onCancel: () => void;
  onRetry: () => void;
  onExplore: () => void;
  onFavorite: () => void;
  favoritePending?: boolean;
  cancelPending?: boolean;
  retryPending?: boolean;
}

export function RepositoryCard({ repository, selected, onCancel, onRetry, onExplore, onFavorite, favoritePending, cancelPending, retryPending }: RepositoryCardProps) {
  const retryable = ['Failed', 'Cancelled'].includes(repository.indexingStatus);
  return <article className={`relative min-w-0 overflow-hidden rounded-xl border p-4 ${selected ? 'border-brand-500 ring-2 ring-brand-100' : 'border-slate-200'}`}>
    <div className="flex items-start gap-3">
      <a className="group block min-w-0 flex-1 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500" href={repository.webUrl} target="_blank" rel="noreferrer" title={repository.fullName}>
        <span className="block break-all text-xs font-medium text-slate-500">{repository.owner}/</span>
        <span className="mt-0.5 flex min-w-0 items-start gap-1 text-base font-semibold leading-5 text-ink group-hover:text-brand-600">
          <span className="min-w-0 break-words [overflow-wrap:anywhere]">{repository.name}</span>
          <AppIcon icon={ExternalLink} size="xs" className="mt-0.5 shrink-0"/>
        </span>
      </a>
      <button type="button" className={`grid h-8 w-8 shrink-0 place-items-center rounded-lg transition hover:scale-110 hover:text-amber-500 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 disabled:opacity-50 ${repository.isFavorite ? 'text-amber-500' : 'text-slate-200'}`} aria-label={`${repository.isFavorite ? 'Remove' : 'Add'} ${repository.fullName} ${repository.isFavorite ? 'from' : 'to'} favorites`} aria-pressed={repository.isFavorite} disabled={favoritePending} onClick={onFavorite}><AppIcon icon={Star} size="sm" className={repository.isFavorite ? 'fill-current' : undefined}/></button>
    </div>
    <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-2">
      <IndexingStatusBadge status={repository.indexingStatus}/>
      <span className="inline-flex min-w-0 items-center gap-1 text-xs text-slate-500">
        <AppIcon icon={repository.visibility === 'Private' ? LockKeyhole : GitBranch} size="xs"/>
        {repository.visibility} · <span className="break-all">{repository.defaultBranch}</span>{repository.commitSha && <span className="text-slate-500">· {repository.commitSha.slice(0, 8)}</span>}
      </span>
    </div>
    {repository.errorMessage && <p className="mt-3 break-words pr-8 text-xs font-medium leading-5 text-red-600">{repository.errorMessage}</p>}
    <div className={`flex items-end gap-4 ${repository.errorMessage || repository.indexingStatus === 'Completed' || ['Pending', 'Processing'].includes(repository.indexingStatus) ? 'mt-3' : 'mt-1'}`}>
      {(['Pending', 'Processing'].includes(repository.indexingStatus)) && <div className="ml-auto flex min-w-0 flex-wrap items-center justify-end gap-2"><span className="text-xs text-slate-500">{cancelPending ? 'Stopping…' : repository.indexingCheckpoint}</span><button type="button" className="inline-flex min-h-9 items-center gap-1.5 rounded-lg border border-slate-200 px-3 py-1.5 text-xs font-semibold text-red-600 transition hover:bg-red-50 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 disabled:opacity-60" disabled={cancelPending} onClick={onCancel} aria-label={`Cancel indexing ${repository.fullName}`}><AppIcon icon={cancelPending ? RefreshCw : X} size="xs" className={cancelPending ? 'animate-spin motion-reduce:animate-none' : undefined}/>{cancelPending ? 'Stopping' : 'Cancel'}</button></div>}
      {repository.indexingStatus === 'Completed' && <button className="inline-flex items-center gap-1.5 text-xs font-semibold text-brand-600" onClick={onExplore}><AppIcon icon={Compass} size="xs"/> Explore repository</button>}
      {retryable && <button type="button" className="ml-auto grid h-8 w-8 place-items-center rounded-lg text-slate-200 transition hover:scale-110 hover:text-brand-600 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 disabled:opacity-60" disabled={retryPending} onClick={onRetry} aria-label={`Retry indexing ${repository.fullName}`} title="Retry indexing"><AppIcon icon={RefreshCw} size="sm" className={retryPending ? 'animate-spin text-brand-600 motion-reduce:animate-none' : undefined}/></button>}
    </div>
  </article>;
}
