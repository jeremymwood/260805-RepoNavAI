import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Trash2, X } from 'lucide-react';
import { getApiError } from '../api/client';
import { AppIcon } from '../components/AppIcon';
import type { RegisteredRepository } from './types';

export const confirmationMatches = (confirmation: string, fullName: string) => confirmation.trim().toLocaleLowerCase() === fullName.toLocaleLowerCase();

export function RepositoryRemovalDialog({ repository, removing, onRemove, onClose }: { repository: RegisteredRepository; removing: boolean; onRemove: (confirmation: string) => Promise<void>; onClose: () => void }) {
  const dialogRef = useRef<HTMLDialogElement>(null); const inputRef = useRef<HTMLInputElement>(null); const invokerRef = useRef<HTMLElement | null>(null);
  const [confirmation, setConfirmation] = useState(''); const [error, setError] = useState('');
  useEffect(() => {
    invokerRef.current = document.activeElement as HTMLElement | null; const dialog = dialogRef.current; dialog?.showModal(); inputRef.current?.focus();
    return () => { if (dialog?.open) dialog.close(); invokerRef.current?.focus(); };
  }, []);
  async function submit(event: FormEvent) { event.preventDefault(); setError(''); try { await onRemove(confirmation.trim()); } catch (reason) { setError(getApiError(reason)); } }
  return <dialog ref={dialogRef} aria-labelledby="remove-repository-title" aria-describedby="remove-repository-impact" onCancel={event => event.preventDefault()} className="m-auto w-[min(32rem,calc(100%-2rem))] rounded-2xl border border-slate-200 bg-white p-0 text-ink shadow-2xl backdrop:bg-slate-950/60">
    <form className="p-5 sm:p-6" onSubmit={submit}>
      <div className="flex items-start gap-3"><span className="panel-icon bg-red-50 text-red-600"><AppIcon icon={Trash2} size="lg"/></span><div className="min-w-0 flex-1"><h2 id="remove-repository-title" className="text-lg font-semibold">Remove {repository.fullName}?</h2><p id="remove-repository-impact" className="mt-2 text-sm leading-6 text-slate-600">This permanently removes RepoNavAI indexing requests, snapshots, documents, symbols, vectors, endpoints, assistant metadata, orientation plans, and favorites. It never changes the source GitHub repository.</p></div><button type="button" className="icon-button" disabled={removing} onClick={onClose} aria-label="Close repository removal"><AppIcon icon={X}/></button></div>
      <label className="field mt-5">Type <strong className="break-all text-ink">{repository.fullName}</strong> to confirm<input ref={inputRef} autoComplete="off" spellCheck={false} value={confirmation} onChange={event => setConfirmation(event.target.value)} disabled={removing}/></label>
      {error && <div className="error mt-4" role="alert">{error}</div>}
      <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end"><button type="button" className="button-secondary" disabled={removing} onClick={onClose}>Keep repository</button><button type="submit" className="button-danger border border-red-200" disabled={removing || !confirmationMatches(confirmation, repository.fullName)}>{removing ? 'Removing…' : 'Remove repository'}</button></div>
    </form>
  </dialog>;
}
