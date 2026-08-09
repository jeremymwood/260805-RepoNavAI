import { Ban, CircleCheck, CircleX, Clock3, LoaderCircle, CircleHelp, type LucideIcon } from 'lucide-react';
import type { IndexingRequestStatus } from './types';

interface StatusPresentation {
  label: string;
  description: string;
  className: string;
  icon: LucideIcon;
  animated?: boolean;
}

const presentations: Record<IndexingRequestStatus, StatusPresentation> = {
  Pending: { label: 'Pending', description: 'Indexing is waiting to start', className: 'bg-blue-50 text-blue-800 ring-blue-200', icon: Clock3 },
  Processing: { label: 'Processing', description: 'Repository indexing is in progress', className: 'bg-amber-50 text-amber-800 ring-amber-200', icon: LoaderCircle, animated: true },
  Completed: { label: 'Completed', description: 'Repository indexing completed successfully', className: 'bg-emerald-50 text-emerald-800 ring-emerald-200', icon: CircleCheck },
  Failed: { label: 'Failed', description: 'Repository indexing failed', className: 'bg-rose-50 text-rose-800 ring-rose-200', icon: CircleX },
  Cancelled: { label: 'Cancelled', description: 'Repository indexing was cancelled', className: 'bg-slate-100 text-slate-700 ring-slate-300', icon: Ban }
};

const fallback: StatusPresentation = { label: 'Unknown', description: 'Repository indexing status is unknown', className: 'bg-slate-100 text-slate-700 ring-slate-300', icon: CircleHelp };

export function getIndexingStatusPresentation(status: string): StatusPresentation {
  return presentations[status as IndexingRequestStatus] ?? fallback;
}

export function IndexingStatusBadge({ status }: { status: IndexingRequestStatus | string }) {
  const presentation = getIndexingStatusPresentation(status);
  const Icon = presentation.icon;
  return <span aria-label={presentation.description} className={`inline-flex shrink-0 items-center gap-1.5 rounded-full px-2.5 py-1 text-[11px] font-semibold ring-1 ring-inset ${presentation.className}`}>
    <Icon aria-hidden="true" size={13} className={presentation.animated ? 'animate-spin motion-reduce:animate-none' : undefined}/>
    {presentation.label}
  </span>;
}
