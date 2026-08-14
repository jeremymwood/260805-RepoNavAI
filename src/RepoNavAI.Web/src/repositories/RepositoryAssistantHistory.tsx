import { useCallback, useEffect, useState } from 'react';
import { Clock3, History, Pencil, Star, Trash2 } from 'lucide-react';
import { api, getApiError } from '../api/client';
import { AppIcon } from '../components/AppIcon';
import { OperationState, RetryButton } from '../components/OperationState';
import type { RepositoryAssistantHistoryDetail, RepositoryAssistantHistoryPage, RepositoryAssistantHistorySummary } from './types';

export function RepositoryAssistantHistory({ organizationId, repositoryId, refreshKey, onOpen }: {
  organizationId: string; repositoryId: string; refreshKey: number; onOpen: (detail: RepositoryAssistantHistoryDetail) => void;
}) {
  const [page, setPage] = useState(1); const [history, setHistory] = useState<RepositoryAssistantHistoryPage>();
  const [loading, setLoading] = useState(true); const [error, setError] = useState(''); const [pendingId, setPendingId] = useState('');
  const load = useCallback(async (targetPage: number) => {
    setLoading(true); setError('');
    try { setHistory((await api.get<RepositoryAssistantHistoryPage>(`/organizations/${organizationId}/repositories/${repositoryId}/assistant/history`, { params: { page: targetPage, pageSize: 10 } })).data); }
    catch (reason) { setError(getApiError(reason)); }
    finally { setLoading(false); }
  }, [organizationId, repositoryId]);
  useEffect(() => { void load(page); }, [load, page, refreshKey]);
  useEffect(() => { setPage(1); }, [organizationId, repositoryId]);

  async function open(item: RepositoryAssistantHistorySummary) {
    setPendingId(item.id); setError('');
    try { onOpen((await api.get<RepositoryAssistantHistoryDetail>(`/organizations/${organizationId}/repositories/${repositoryId}/assistant/history/${item.id}`)).data); }
    catch (reason) { setError(getApiError(reason)); }
    finally { setPendingId(''); }
  }
  async function star(item: RepositoryAssistantHistorySummary) {
    setPendingId(item.id);
    try { await api.put(`/organizations/${organizationId}/repositories/${repositoryId}/assistant/history/${item.id}/star`, { isStarred: !item.isStarred }); await load(page); }
    catch (reason) { setError(getApiError(reason)); }
    finally { setPendingId(''); }
  }
  async function rename(item: RepositoryAssistantHistorySummary) {
    const title = window.prompt('Saved result title', item.displayTitle)?.trim();
    if (!title || title === item.displayTitle) return;
    if (title.length > 120) { setError('Saved result titles cannot exceed 120 characters.'); return; }
    setPendingId(item.id);
    try { await api.put(`/organizations/${organizationId}/repositories/${repositoryId}/assistant/history/${item.id}/title`, { title }); await load(page); }
    catch (reason) { setError(getApiError(reason)); }
    finally { setPendingId(''); }
  }
  async function remove(item: RepositoryAssistantHistorySummary) {
    if (!window.confirm(`Permanently delete “${item.displayTitle}” from your assistant history?`)) return;
    setPendingId(item.id);
    try { await api.delete(`/organizations/${organizationId}/repositories/${repositoryId}/assistant/history/${item.id}`); await load(page); }
    catch (reason) { setError(getApiError(reason)); }
    finally { setPendingId(''); }
  }
  async function clear() {
    if (window.prompt('Type CLEAR to permanently delete your assistant history for this repository.') !== 'CLEAR') return;
    setPendingId('clear');
    try { await api.delete(`/organizations/${organizationId}/repositories/${repositoryId}/assistant/history`, { data: { confirmation: 'CLEAR' } }); setPage(1); await load(1); }
    catch (reason) { setError(getApiError(reason)); }
    finally { setPendingId(''); }
  }
  function changePage(next: number) { setPage(next); }

  return <section aria-labelledby="assistant-history-title" className="mt-4 rounded-xl border border-slate-200 p-3">
    <div className="flex flex-wrap items-center justify-between gap-2">
      <div className="flex items-center gap-2"><AppIcon icon={History} size="sm" className="text-brand-600"/><h4 id="assistant-history-title" className="text-sm font-semibold text-ink">Your recent results</h4></div>
      {history && history.totalCount > 0 && <button type="button" className="text-xs font-semibold text-red-600 hover:underline disabled:opacity-50" disabled={pendingId === 'clear'} onClick={clear}>Clear history</button>}
    </div>
    <p className="mt-1 text-xs leading-5 text-slate-500">Private to you. Starred results appear first; saved citations remain pinned to their original commit.</p>
    {loading && !history && <div className="mt-3"><OperationState kind="loading" title="Loading recent results" compact/></div>}
    {error && <div className="mt-3"><OperationState kind="failure" title="History unavailable" message={error} action={<RetryButton onRetry={() => void load(page)}/>} compact/></div>}
    {history && history.items.length === 0 && !loading && <p className="empty-state mt-3">Completed assistant requests will appear here after you run them.</p>}
    {history && history.items.length > 0 && <div className="mt-3 grid gap-2">
      {history.items.map(item => <article key={item.id} className="rounded-lg bg-slate-50 p-3">
        <div className="flex items-start gap-2">
          <button type="button" className="min-w-0 flex-1 text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500 disabled:cursor-not-allowed" disabled={item.status !== 'Completed' || !item.isSupported || pendingId === item.id} onClick={() => void open(item)}>
            <span className="block break-words text-sm font-semibold text-ink">{item.displayTitle}</span>
            <span className="mt-1 flex flex-wrap items-center gap-x-2 gap-y-1 text-[11px] text-slate-500"><span>{item.mode === 'CodeFlow' ? 'Code flow' : item.mode}</span><span className="inline-flex items-center gap-1"><AppIcon icon={Clock3} size="xs"/>{new Date(item.createdAtUtc).toLocaleString()}</span><span>commit {item.commitSha.slice(0, 8) || 'unknown'}</span></span>
          </button>
          <button type="button" className={`icon-button h-8 w-8 ${item.isStarred ? 'text-amber-500' : 'text-slate-400'}`} aria-label={`${item.isStarred ? 'Unstar' : 'Star'} ${item.displayTitle}`} aria-pressed={item.isStarred} disabled={pendingId === item.id} onClick={() => void star(item)}><AppIcon icon={Star} size="xs" className={item.isStarred ? 'fill-current' : undefined}/></button>
          <button type="button" className="icon-button h-8 w-8 text-slate-400" aria-label={`Rename ${item.displayTitle}`} disabled={pendingId === item.id} onClick={() => void rename(item)}><AppIcon icon={Pencil} size="xs"/></button>
          <button type="button" className="icon-button h-8 w-8 text-red-500" aria-label={`Delete ${item.displayTitle}`} disabled={pendingId === item.id} onClick={() => void remove(item)}><AppIcon icon={Trash2} size="xs"/></button>
        </div>
        <div className="mt-2 flex flex-wrap gap-2 text-[11px]">
          {item.isStale && <span className="rounded-full bg-amber-50 px-2 py-1 text-amber-800">Older index</span>}
          {item.status !== 'Completed' && <span className="rounded-full bg-slate-200 px-2 py-1 text-slate-700">{item.status}</span>}
          {!item.isSupported && item.status === 'Completed' && <span className="rounded-full bg-slate-200 px-2 py-1 text-slate-700">Unsupported saved format</span>}
        </div>
      </article>)}
    </div>}
    {history && history.totalCount > history.pageSize && <div className="mt-3 flex items-center justify-between"><button type="button" className="button-secondary min-h-8 px-3 py-1 text-xs" disabled={page === 1 || loading} onClick={() => changePage(page - 1)}>Previous</button><span className="text-xs text-slate-500">Page {page}</span><button type="button" className="button-secondary min-h-8 px-3 py-1 text-xs" disabled={!history.hasMore || loading} onClick={() => changePage(page + 1)}>Next</button></div>}
  </section>;
}
