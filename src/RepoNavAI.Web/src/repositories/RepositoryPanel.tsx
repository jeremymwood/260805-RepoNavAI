import { useState, type FormEvent } from 'react';
import { useInfiniteQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Github, Plus } from 'lucide-react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { api, getApiError } from '../api/client';
import { AppIcon } from '../components/AppIcon';
import type { RegisteredRepository, RepositoryPage } from './types';
import { RepositoryCard } from './RepositoryCard';

const pageSize = 10;

export function RepositoryPanel({ organizationId, initialVisibleCount = pageSize }: { organizationId: string; initialVisibleCount?: number }) {
  const queryClient = useQueryClient(); const navigate = useNavigate(); const location = useLocation(); const [searchParams, setSearchParams] = useSearchParams();
  const [url, setUrl] = useState(''); const [error, setError] = useState('');
  const [expanded, setExpanded] = useState(() => searchParams.get('repositories') === 'all');
  const queryKey = ['organization', organizationId, 'repositories'];
  const repositories = useInfiniteQuery({
    queryKey,
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => (await api.get<RepositoryPage>(`/organizations/${organizationId}/repositories`, { params: { page: pageParam, pageSize } })).data,
    getNextPageParam: lastPage => lastPage.hasMore ? lastPage.page + 1 : undefined,
    refetchInterval: query => query.state.data?.pages.some(page => page.items.some(item => item.indexingStatus === 'Pending' || item.indexingStatus === 'Processing')) ? 3000 : false
  });
  const indexingAction = useMutation({ mutationFn: ({ repositoryId, action }: { repositoryId: string; action: 'cancel' | 'retry' }) => api.post(`/organizations/${organizationId}/repositories/${repositoryId}/indexing/${action}`), onSuccess: () => queryClient.invalidateQueries({ queryKey }), onError: reason => setError(getApiError(reason)) });
  const favorite = useMutation({ mutationFn: ({ repositoryId, isFavorite }: { repositoryId: string; isFavorite: boolean }) => api.put(`/organizations/${organizationId}/repositories/${repositoryId}/favorite`, { isFavorite }), onSuccess: () => queryClient.invalidateQueries({ queryKey }), onError: reason => setError(getApiError(reason)) });
  const register = useMutation({ mutationFn: async () => (await api.post<RegisteredRepository>(`/organizations/${organizationId}/repositories`, { url: url.trim() })).data, onSuccess: async () => { setUrl(''); setError(''); await queryClient.invalidateQueries({ queryKey }); }, onError: reason => setError(getApiError(reason)) });
  function submit(event: FormEvent) { event.preventDefault(); setError(''); register.mutate(); }
  const loaded = repositories.data?.pages.flatMap(page => page.items) ?? [];
  const totalCount = repositories.data?.pages[0]?.totalCount ?? 0;
  const visibleRepositories = expanded ? loaded : loaded.slice(0, initialVisibleCount);
  const hiddenCount = Math.max(0, totalCount - visibleRepositories.length);
  function setDisclosure(next: boolean) { const params = new URLSearchParams(searchParams); if (next) params.set('repositories', 'all'); else params.delete('repositories'); setSearchParams(params, { replace: true }); setExpanded(next); }
  async function showMore() { setDisclosure(true); if (loaded.length < totalCount) await repositories.fetchNextPage(); }
  function explore(repositoryId: string) { navigate(`/repositories/${repositoryId}`, { state: { returnTo: `${location.pathname}${location.search}#repositories` } }); }

  return <section id="repositories" className="mt-8 scroll-mt-28 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
    <div className="flex items-center gap-3"><span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-50 text-brand-600"><AppIcon icon={Github} size="lg"/></span><div><p className="font-semibold text-ink">Repositories</p><p className="text-sm text-slate-500">Register a GitHub repository and queue it for analysis.</p></div></div>
    <form id="register-repository" className="mt-6 scroll-mt-28 flex flex-col gap-3 md:flex-row" onSubmit={submit}><input className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 bg-white px-4 text-sm text-ink outline-none focus:border-brand-500" type="url" required maxLength={2048} value={url} onChange={event => setUrl(event.target.value)} placeholder="https://github.com/owner/repository" aria-label="GitHub repository URL"/><button className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 text-sm font-semibold text-white disabled:opacity-60" disabled={register.isPending}>{register.isPending ? 'Verifying...' : <><AppIcon icon={Plus} size="sm"/> Register</>}</button></form>
    <p className="mt-2 text-xs text-slate-400">Private repositories require access through the server-configured GitHub integration.</p>
    {error && <div className="error mt-4">{error}</div>}
    {repositories.isLoading ? <p className="mt-6 text-sm text-slate-500">Loading repositories...</p> : repositories.isError ? <div className="error mt-6">{getApiError(repositories.error)}</div> : totalCount ? <div id="repository-list" className="mt-6 grid gap-3 md:grid-cols-2">{visibleRepositories.map(repository => <RepositoryCard key={repository.id} repository={repository} selected={false} onCancel={() => indexingAction.mutate({ repositoryId: repository.id, action: 'cancel' })} onRetry={() => indexingAction.mutate({ repositoryId: repository.id, action: 'retry' })} onExplore={() => explore(repository.id)} retryPending={indexingAction.isPending && indexingAction.variables?.repositoryId === repository.id && indexingAction.variables?.action === 'retry'} favoritePending={favorite.isPending && favorite.variables?.repositoryId === repository.id} onFavorite={() => favorite.mutate({ repositoryId: repository.id, isFavorite: !repository.isFavorite })}/>)}</div> : <div className="mt-6 rounded-xl border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">No repositories registered yet.</div>}
    {totalCount > initialVisibleCount && <div className="mt-5 flex flex-wrap justify-center gap-3">{expanded && <button type="button" className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-brand-600 hover:bg-brand-50" aria-expanded="true" aria-controls="repository-list" onClick={() => setDisclosure(false)}>Show less</button>}{hiddenCount > 0 && <button type="button" className="rounded-xl border border-slate-200 px-4 py-2 text-sm font-semibold text-brand-600 hover:bg-brand-50 disabled:opacity-60" aria-expanded={expanded} aria-controls="repository-list" disabled={repositories.isFetchingNextPage} onClick={showMore}>{repositories.isFetchingNextPage ? 'Loading...' : `Show more (${hiddenCount} hidden)`}</button>}</div>}
  </section>;
}
