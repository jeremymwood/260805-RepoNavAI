import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, GitBranch, Github, LockKeyhole, Plus, X } from 'lucide-react';
import { api, getApiError } from '../api/client';
import type { RegisteredRepository, RepositoryEndpoint } from './types';
import { RepositoryAssistant } from './RepositoryAssistant';
import { IndexingStatusBadge } from './IndexingStatusBadge';

export function RepositoryPanel({ organizationId }: { organizationId: string }) {
  const queryClient = useQueryClient();
  const [url, setUrl] = useState('');
  const [error, setError] = useState('');
  const [selected, setSelected] = useState<RegisteredRepository>();
  const repositories = useQuery({
    queryKey: ['organization', organizationId, 'repositories'],
    queryFn: async () => (await api.get<RegisteredRepository[]>(`/organizations/${organizationId}/repositories`)).data,
    refetchInterval: query => query.state.data?.some(item => item.indexingStatus === 'Pending' || item.indexingStatus === 'Processing') ? 3000 : false
  });
  const indexingAction = useMutation({
    mutationFn: ({ repositoryId, action }: { repositoryId: string; action: 'cancel' | 'retry' }) => api.post(`/organizations/${organizationId}/repositories/${repositoryId}/indexing/${action}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['organization', organizationId, 'repositories'] }),
    onError: reason => setError(getApiError(reason))
  });
  const reindex = useMutation({
    mutationFn: (repositoryId: string) => api.post(`/organizations/${organizationId}/repositories/${repositoryId}/indexing/reindex`),
    onSuccess: async () => { setSelected(undefined); await queryClient.invalidateQueries({ queryKey: ['organization', organizationId, 'repositories'] }); },
    onError: reason => setError(getApiError(reason))
  });
  const register = useMutation({
    mutationFn: async () => (await api.post<RegisteredRepository>(`/organizations/${organizationId}/repositories`, { url: url.trim() })).data,
    onSuccess: async () => { setUrl(''); setError(''); await queryClient.invalidateQueries({ queryKey: ['organization', organizationId, 'repositories'] }); },
    onError: reason => setError(getApiError(reason))
  });
  function submit(event: FormEvent) { event.preventDefault(); setError(''); register.mutate(); }

  return <section id="repositories" className="mt-10 scroll-mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
    <div className="flex items-center gap-3"><span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-50 text-brand-600"><Github size={20}/></span><div><p className="font-semibold text-ink">Repositories</p><p className="text-sm text-slate-500">Register a GitHub repository and queue it for analysis.</p></div></div>
    <form className="mt-6 flex flex-col gap-3 md:flex-row" onSubmit={submit}><input className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-4 text-sm outline-none focus:border-brand-500" type="url" required maxLength={2048} value={url} onChange={event => setUrl(event.target.value)} placeholder="https://github.com/owner/repository" aria-label="GitHub repository URL"/><button className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 text-sm font-semibold text-white disabled:opacity-60" disabled={register.isPending}>{register.isPending ? 'Verifying...' : <><Plus size={17}/> Register</>}</button></form>
    <p className="mt-2 text-xs text-slate-400">Private repositories require access through the server-configured GitHub integration.</p>
    {error && <div className="error mt-4">{error}</div>}
    {repositories.isLoading ? <p className="mt-6 text-sm text-slate-500">Loading repositories...</p> : repositories.data?.length ? <div className="mt-6 grid gap-3 md:grid-cols-2">{repositories.data.map(repository => <article key={repository.id} className="rounded-xl border border-slate-200 p-4"><div className="flex items-start gap-3"><span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-600">{repository.visibility === 'Private' ? <LockKeyhole size={17}/> : <GitBranch size={17}/>}</span><div className="min-w-0 flex-1"><a className="inline-flex max-w-full items-center gap-1 font-semibold text-ink hover:text-brand-600" href={repository.webUrl} target="_blank" rel="noreferrer"><span className="truncate">{repository.fullName}</span><ExternalLink size={14}/></a><p className="mt-1 text-xs text-slate-500">{repository.visibility} / {repository.defaultBranch}</p><p className="mt-1 text-xs text-slate-400">{repository.indexingCheckpoint}{repository.commitSha ? ` / ${repository.commitSha.slice(0, 8)}` : ''}</p></div><IndexingStatusBadge status={repository.indexingStatus}/></div>{repository.errorMessage && <p className="mt-3 text-xs text-red-600">{repository.errorMessage}</p>}<div className="mt-3 flex gap-3">{(['Pending','Processing'].includes(repository.indexingStatus)) && <button className="text-xs font-semibold text-slate-500 hover:text-red-600" onClick={() => indexingAction.mutate({ repositoryId: repository.id, action: 'cancel' })}>Cancel</button>}{(['Failed','Cancelled'].includes(repository.indexingStatus)) && <button className="text-xs font-semibold text-brand-600" onClick={() => indexingAction.mutate({ repositoryId: repository.id, action: 'retry' })}>Retry indexing</button>}{repository.indexingStatus === 'Completed' && <button className="text-xs font-semibold text-brand-600" onClick={() => setSelected(repository)}>Open analysis</button>}</div></article>)}</div> : <div className="mt-6 rounded-xl border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">No repositories registered yet.</div>}
    {selected && <EndpointCatalog organizationId={organizationId} repository={selected} onClose={() => setSelected(undefined)} />}
    {selected && <RepositoryAssistant organizationId={organizationId} repository={selected} />}
    {selected && <button className="mt-5 text-xs font-semibold text-brand-600 disabled:opacity-50" disabled={reindex.isPending} onClick={() => reindex.mutate(selected.id)}>{reindex.isPending ? 'Queuing re-index...' : 'Re-index this repository'}</button>}
  </section>;
}

function EndpointCatalog({ organizationId, repository, onClose }: { organizationId: string; repository: RegisteredRepository; onClose: () => void }) {
  const [search, setSearch] = useState(''); const [method, setMethod] = useState(''); const [authorization, setAuthorization] = useState('');
  const endpoints = useQuery({ queryKey: ['endpoint-catalog', organizationId, repository.id, method, search, authorization], queryFn: async () => (await api.get<RepositoryEndpoint[]>(`/organizations/${organizationId}/repositories/${repository.id}/endpoints`, { params: { method: method || undefined, search: search || undefined, requiresAuthorization: authorization || undefined } })).data });
  return <div className="mt-8 border-t border-slate-200 pt-6"><div className="flex items-start justify-between"><div><h3 className="font-semibold text-ink">API endpoints / {repository.fullName}</h3><p className="text-sm text-slate-500">Routes found in the latest indexed commit.</p></div><button aria-label="Close endpoint catalog" onClick={onClose}><X size={20}/></button></div><div className="mt-4 grid gap-3 md:grid-cols-3"><input className="h-10 rounded-lg border border-slate-200 px-3 text-sm" placeholder="Search route or handler" value={search} onChange={event => setSearch(event.target.value)}/><select className="h-10 rounded-lg border border-slate-200 px-3 text-sm" value={method} onChange={event => setMethod(event.target.value)}><option value="">All methods</option>{['GET','POST','PUT','PATCH','DELETE'].map(value => <option key={value}>{value}</option>)}</select><select className="h-10 rounded-lg border border-slate-200 px-3 text-sm" value={authorization} onChange={event => setAuthorization(event.target.value)}><option value="">Any authorization</option><option value="true">Authorized</option><option value="false">Anonymous</option></select></div>{endpoints.isLoading ? <p className="mt-5 text-sm text-slate-500">Loading endpoint catalog...</p> : endpoints.isError ? <div className="error mt-5">{getApiError(endpoints.error)}</div> : endpoints.data?.length ? <div className="mt-5 overflow-x-auto"><table className="w-full text-left text-sm"><thead className="text-xs uppercase text-slate-400"><tr><th className="pb-3">Method</th><th className="pb-3">Route</th><th className="pb-3">Handler</th><th className="pb-3">Access</th></tr></thead><tbody>{endpoints.data.map(endpoint => <tr key={endpoint.id} className="border-t border-slate-100"><td className="py-3 font-semibold text-brand-700">{endpoint.httpMethod}</td><td className="py-3 font-mono text-xs">{endpoint.route}</td><td className="py-3"><a href={endpoint.sourceUrl} target="_blank" rel="noreferrer" className="text-brand-600 hover:underline">{endpoint.handler}</a><p className="text-xs text-slate-400">{endpoint.path}:{endpoint.line}</p>{endpoint.downstreamSymbols.length > 0 && <p className="mt-1 text-xs text-slate-500">Calls: {endpoint.downstreamSymbols.join(', ')}</p>}</td><td className="py-3">{endpoint.requiresAuthorization ? 'Authorized' : 'Anonymous'}</td></tr>)}</tbody></table></div> : <p className="mt-5 rounded-lg bg-slate-50 p-4 text-sm text-slate-500">No endpoints matched. Unsupported or dynamically constructed routes are not inferred.</p>}</div>;
}
