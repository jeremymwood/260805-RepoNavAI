import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ExternalLink, GitBranch, Github, LockKeyhole, Plus } from 'lucide-react';
import { api, getApiError } from '../api/client';
import type { RegisteredRepository } from './types';

export function RepositoryPanel({ organizationId }: { organizationId: string }) {
  const queryClient = useQueryClient();
  const [url, setUrl] = useState('');
  const [error, setError] = useState('');
  const repositories = useQuery({
    queryKey: ['organization', organizationId, 'repositories'],
    queryFn: async () => (await api.get<RegisteredRepository[]>(`/organizations/${organizationId}/repositories`)).data,
    refetchInterval: query => query.state.data?.some(repository => repository.indexingStatus === 'Pending' || repository.indexingStatus === 'Processing') ? 3000 : false
  });
  const indexingAction = useMutation({
    mutationFn: ({ repositoryId, action }: { repositoryId: string; action: 'cancel' | 'retry' }) => api.post(`/organizations/${organizationId}/repositories/${repositoryId}/indexing/${action}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['organization', organizationId, 'repositories'] }),
    onError: reason => setError(getApiError(reason))
  });
  const register = useMutation({
    mutationFn: async () => (await api.post<RegisteredRepository>(`/organizations/${organizationId}/repositories`, { url: url.trim() })).data,
    onSuccess: async () => {
      setUrl('');
      setError('');
      await queryClient.invalidateQueries({ queryKey: ['organization', organizationId, 'repositories'] });
    },
    onError: reason => setError(getApiError(reason))
  });

  function submit(event: FormEvent) {
    event.preventDefault();
    setError('');
    register.mutate();
  }

  return <section id="repositories" className="mt-10 scroll-mt-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
    <div className="flex items-center gap-3"><span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-50 text-brand-600"><Github size={20}/></span><div><p className="font-semibold text-ink">Repositories</p><p className="text-sm text-slate-500">Register a GitHub repository and queue it for analysis.</p></div></div>
    <form className="mt-6 flex flex-col gap-3 md:flex-row" onSubmit={submit}>
      <input className="h-11 min-w-0 flex-1 rounded-xl border border-slate-200 px-4 text-sm outline-none focus:border-brand-500" type="url" required maxLength={2048} value={url} onChange={event => setUrl(event.target.value)} placeholder="https://github.com/owner/repository" aria-label="GitHub repository URL" />
      <button className="inline-flex items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 text-sm font-semibold text-white disabled:opacity-60" disabled={register.isPending}>{register.isPending ? 'Verifying…' : <><Plus size={17} /> Register</>}</button>
    </form>
    <p className="mt-2 text-xs text-slate-400">Private repositories require access through the server-configured GitHub integration.</p>
    {error && <div className="error mt-4">{error}</div>}
    {repositories.isLoading ? <p className="mt-6 text-sm text-slate-500">Loading repositories…</p> : repositories.data?.length ? <div className="mt-6 grid gap-3 md:grid-cols-2">{repositories.data.map(repository => <article key={repository.id} className="rounded-xl border border-slate-200 p-4"><div className="flex items-start gap-3"><span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-slate-100 text-slate-600">{repository.visibility === 'Private' ? <LockKeyhole size={17} /> : <GitBranch size={17} />}</span><div className="min-w-0 flex-1"><a className="inline-flex max-w-full items-center gap-1 font-semibold text-ink hover:text-brand-600" href={repository.webUrl} target="_blank" rel="noreferrer"><span className="truncate">{repository.fullName}</span><ExternalLink className="shrink-0" size={14} /></a><p className="mt-1 text-xs text-slate-500">{repository.visibility} · {repository.defaultBranch}</p><p className="mt-1 text-xs text-slate-400">{repository.indexingCheckpoint}{repository.commitSha ? ` · ${repository.commitSha.slice(0, 8)}` : ''}</p></div><span className="rounded-full bg-amber-50 px-2 py-1 text-[11px] font-semibold text-amber-700">{repository.indexingStatus}</span></div>{repository.errorMessage&&<p className="mt-3 text-xs text-red-600">{repository.errorMessage}</p>}<div className="mt-3 flex gap-3">{(repository.indexingStatus==='Pending'||repository.indexingStatus==='Processing')&&<button className="text-xs font-semibold text-slate-500 hover:text-red-600" onClick={()=>indexingAction.mutate({repositoryId:repository.id,action:'cancel'})}>Cancel</button>}{(repository.indexingStatus==='Failed'||repository.indexingStatus==='Cancelled')&&<button className="text-xs font-semibold text-brand-600" onClick={()=>indexingAction.mutate({repositoryId:repository.id,action:'retry'})}>Retry indexing</button>}</div></article>)}</div> : <div className="mt-6 rounded-xl border border-dashed border-slate-200 p-6 text-center text-sm text-slate-500">No repositories registered yet.</div>}
  </section>;
}
