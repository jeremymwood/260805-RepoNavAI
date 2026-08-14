import { Ban, CircleAlert, Clock3, LoaderCircle, RotateCcw, type LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { AppIcon } from './AppIcon';

export type OperationStateKind = 'loading' | 'progress' | 'stopped' | 'timeout' | 'failure';

const presentation: Record<OperationStateKind, { icon: LucideIcon; tone: string; role: 'status' | 'alert'; animated?: boolean }> = {
  loading: { icon: LoaderCircle, tone: 'border-brand-100 bg-brand-50/50 text-brand-700', role: 'status', animated: true },
  progress: { icon: LoaderCircle, tone: 'border-brand-100 bg-brand-50/50 text-brand-700', role: 'status', animated: true },
  stopped: { icon: Ban, tone: 'border-slate-200 bg-slate-50 text-slate-700', role: 'status' },
  timeout: { icon: Clock3, tone: 'border-amber-200 bg-amber-50/60 text-amber-900', role: 'alert' },
  failure: { icon: CircleAlert, tone: 'border-red-200 bg-red-50 text-red-700', role: 'alert' }
};

export function OperationState({ kind, title, message, action, compact = false }: { kind: OperationStateKind; title: string; message?: string; action?: ReactNode; compact?: boolean }) {
  const state = presentation[kind];
  return <div className={`min-w-0 rounded-xl border ${compact ? 'p-3' : 'p-4'} ${state.tone}`} role={state.role} aria-live={state.role === 'status' ? 'polite' : 'assertive'} aria-atomic="true">
    <div className="flex min-w-0 items-start gap-3">
      <AppIcon icon={state.icon} size="sm" className={`mt-0.5 shrink-0 ${state.animated ? 'animate-spin motion-reduce:animate-none' : ''}`}/>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold [overflow-wrap:anywhere]">{title}</p>
        {message && <p className="mt-1 text-xs leading-5 opacity-90 [overflow-wrap:anywhere]">{message}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </div>
  </div>;
}

export function RetryButton({ onRetry, label = 'Retry' }: { onRetry: () => void; label?: string }) {
  return <button type="button" className="inline-flex min-h-9 items-center gap-1 rounded-lg border border-current px-3 py-1.5 text-xs font-semibold" onClick={onRetry}><AppIcon icon={RotateCcw} size="xs"/>{label}</button>;
}
