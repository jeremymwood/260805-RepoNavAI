import { useEffect, useRef } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ArrowLeft, BookOpenCheck, Braces, ExternalLink, FileSearch, GitBranch, LockKeyhole, Map, MinusCircle, Search, Sparkles } from 'lucide-react';
import { Link, useLocation, useParams, useSearchParams } from 'react-router-dom';
import { api, getApiError } from '../api/client';
import { useOrganization } from '../organizations/OrganizationContext';
import { IndexingStatusBadge } from './IndexingStatusBadge';
import { RepositoryAssistant } from './RepositoryAssistant';
import { RepositoryArchitectureMap } from './RepositoryArchitectureMap';
import type { RegisteredRepository, RepositoryEndpoint, RepositoryPage } from './types';

export interface RepositoryLanguageCoverage { language: string; indexed: number; skippedUnsupported: number; skippedExcluded: number; skippedBinary: number }
export interface RepositoryCapabilities { hasIndexedContent: boolean; hasSourceCode: boolean; hasTests: boolean; hasDocumentation: boolean; hasApiEndpoints: boolean; representativePaths: string[]; coverageStatus?: 'full' | 'partial' | 'none'; languages?: RepositoryLanguageCoverage[] }
export type RepositoryWorkspaceView = 'summary' | 'architecture' | 'assistant' | 'search' | 'endpoints';
export function resolveWorkspaceView(value: string | null, capabilities?: RepositoryCapabilities): RepositoryWorkspaceView {
  if (value === 'endpoints' && capabilities?.hasApiEndpoints) return 'endpoints';
  if (value === 'search' && capabilities?.hasIndexedContent) return 'search';
  if (value === 'architecture' && capabilities?.hasIndexedContent) return 'architecture';
  if (value === 'assistant') return 'assistant';
  return 'summary';
}
export function visibleEndpointCount(total: number, expanded: boolean, previewCount = 5) { return expanded ? total : Math.min(total, previewCount); }
export function findRepository(page: RepositoryPage | undefined, repositoryId: string | undefined) { return page?.items.find(item => item.id === repositoryId); }
export function focusAnalysisHeading(element: Pick<HTMLElement, 'focus' | 'scrollIntoView'>, reducedMotion: boolean) {
  element.focus({ preventScroll: true });
  element.scrollIntoView({ behavior: reducedMotion ? 'auto' : 'smooth', block: 'start' });
}

export function RepositoryWorkspacePage() {
  const { repositoryId } = useParams(); const { current } = useOrganization(); const location = useLocation(); const [searchParams, setSearchParams] = useSearchParams(); const queryClient = useQueryClient(); const headingRef = useRef<HTMLHeadingElement>(null); const analysisHeadingRef = useRef<HTMLHeadingElement>(null); const pendingFocusView = useRef<RepositoryWorkspaceView|null>(null); const initialAnalysisFocusHandled = useRef(false);
  const repositories = useQuery({ queryKey: ['organization', current?.id, 'repositories', 'workspace'], queryFn: async () => (await api.get<RepositoryPage>(`/organizations/${current!.id}/repositories`, { params: { page: 1, pageSize: 50 } })).data, enabled: Boolean(current) });
  const repository = findRepository(repositories.data, repositoryId);
  const capabilities = useQuery({ queryKey: ['repository-capabilities', current?.id, repositoryId], queryFn: async () => (await api.get<RepositoryCapabilities>(`/organizations/${current!.id}/repositories/${repositoryId}/capabilities`)).data, enabled: Boolean(current && repository?.indexingStatus === 'Completed') });
  const reindex = useMutation({ mutationFn: () => api.post(`/organizations/${current!.id}/repositories/${repositoryId}/indexing/reindex`), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ['organization', current!.id, 'repositories'] }); }, });
  const activeView = resolveWorkspaceView(searchParams.get('view'), capabilities.data);
  const returnTo = typeof location.state === 'object' && location.state && 'returnTo' in location.state && typeof location.state.returnTo === 'string' ? location.state.returnTo : '/#repositories';
  useEffect(() => { headingRef.current?.focus(); }, [repositoryId]);
  useEffect(() => {
    const requestedFromRepositoryCard = typeof location.state === 'object' && location.state && 'focusAnalysis' in location.state && location.state.focusAnalysis === true;
    const requestedFromTool = pendingFocusView.current === activeView;
    if ((!requestedFromRepositoryCard || initialAnalysisFocusHandled.current) && !requestedFromTool) return;
    const heading = analysisHeadingRef.current;
    if (!heading) return;
    initialAnalysisFocusHandled.current = true; pendingFocusView.current = null;
    const frame = requestAnimationFrame(() => focusAnalysisHeading(heading, window.matchMedia('(prefers-reduced-motion: reduce)').matches));
    return () => cancelAnimationFrame(frame);
  }, [activeView, capabilities.data, location.state]);
  function selectView(view: RepositoryWorkspaceView) { pendingFocusView.current = view; const params = new URLSearchParams(searchParams); if (view === 'summary') params.delete('view'); else params.set('view', view); setSearchParams(params); }

  if (repositories.isLoading) return <WorkspaceState title="Loading repository workspace" copy="Retrieving the selected repository and its latest index context."/>;
  if (repositories.isError) return <WorkspaceState title="Repository unavailable" copy={getApiError(repositories.error)}/>;
  if (!repository) return <WorkspaceState title="Repository unavailable" copy="This repository was removed or is not available in the current organization."/>;
  if (repository.indexingStatus !== 'Completed') return <WorkspaceState title={`${repository.fullName} is not ready`} copy={`Current indexing status: ${repository.indexingStatus}. Return to the overview to monitor, retry, or cancel indexing.`}/>;

  const tools: Array<{ view: RepositoryWorkspaceView; label: string; copy: string; icon: typeof FileSearch }> = [
    { view: 'summary', label: 'Analysis summary', copy: 'Coverage and index context', icon: BookOpenCheck },
    { view: 'assistant', label: 'Repository assistant', copy: 'Ask, orient, and trace flows', icon: Sparkles },
  ];
  if (capabilities.data?.hasIndexedContent) tools.push({ view: 'search', label: 'Source search', copy: 'Search indexed source evidence', icon: Search });
  if (capabilities.data?.hasIndexedContent) tools.splice(1, 0, { view: 'architecture', label: 'Architecture map', copy: 'Navigate modules and relationships', icon: Map });
  if (capabilities.data?.hasApiEndpoints) tools.push({ view: 'endpoints', label: 'API endpoints', copy: 'Browse detected routes', icon: Braces });
  const activeTool = tools.find(tool => tool.view === activeView) ?? tools[0];

  return <div className="min-w-0"><Link to={returnTo} className="inline-flex items-center gap-2 text-sm font-semibold text-brand-600 hover:text-brand-700"><ArrowLeft size={16}/> Back to repositories</Link>
    <header className="panel mt-4"><div className="flex flex-wrap items-start justify-between gap-4"><div className="min-w-0"><p className="break-all text-xs font-semibold text-slate-500">{repository.owner}/</p><h1 ref={headingRef} tabIndex={-1} className="mt-1 break-words text-2xl font-semibold leading-tight text-ink [overflow-wrap:anywhere]">{repository.name}</h1><a href={repository.webUrl} target="_blank" rel="noreferrer" className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-brand-600 hover:underline">Open on GitHub <ExternalLink size={12}/></a></div><IndexingStatusBadge status={repository.indexingStatus}/></div>
      <div className="mt-4 flex flex-wrap gap-x-5 gap-y-2 border-t border-slate-100 pt-4 text-xs text-slate-500"><span className="inline-flex items-center gap-1">{repository.visibility === 'Private' ? <LockKeyhole size={13}/> : <GitBranch size={13}/>} {repository.visibility}</span><span>Branch: <strong className="break-all text-slate-700">{repository.defaultBranch}</strong></span><span>Commit: <strong className="text-slate-700">{repository.commitSha?.slice(0, 8)}</strong></span><span>Checkpoint: <strong className="text-slate-700">{repository.indexingCheckpoint}</strong></span></div>
      {capabilities.isLoading ? <p className="mt-4 text-sm text-slate-500" role="status">Detecting analysis capabilities...</p> : capabilities.isError ? <div className="error mt-4" role="alert">{getApiError(capabilities.error)}</div> : capabilities.data && <CapabilitySummary capabilities={capabilities.data}/>}
    </header>
    <nav aria-label="Repository tools" className="mt-4 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">{tools.map(({ view, label, copy, icon: Icon }) => <button key={view} type="button" aria-current={activeView === view ? 'page' : undefined} onClick={() => selectView(view)} className={`flex min-w-0 items-center gap-3 rounded-xl border p-3 text-left transition focus:outline-none focus:ring-2 focus:ring-brand-500 ${activeView === view ? 'border-brand-500 bg-brand-50' : 'border-slate-200 bg-white hover:border-brand-100'}`}><span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-brand-50 text-brand-600"><Icon size={18}/></span><span className="min-w-0"><span className="block text-sm font-semibold text-ink">{label}</span><span className="block truncate text-xs text-slate-500">{copy}</span></span></button>)}</nav>
    <section className="panel mt-4 scroll-mt-28" aria-labelledby="repository-analysis-heading">
      <h2 id="repository-analysis-heading" ref={analysisHeadingRef} tabIndex={-1} className="mb-4 text-lg font-semibold text-ink focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-500">{activeTool?.label ?? 'Analysis summary'}</h2>
      {activeView === 'summary' && capabilities.data && <RepositorySummary repository={repository} capabilities={capabilities.data} onReindex={() => reindex.mutate()} reindexing={reindex.isPending}/>}
      {activeView === 'architecture' && <RepositoryArchitectureMap organizationId={current!.id} repositoryId={repository.id}/>}
      {(activeView === 'assistant' || activeView === 'search') && <RepositoryAssistant key={activeView} organizationId={current!.id} repository={repository} initialMode={activeView === 'search' ? 'Search' : 'Auto'}/>}
      {activeView === 'endpoints' && capabilities.data?.hasApiEndpoints && <EndpointCatalog organizationId={current!.id} repository={repository}/>}
    </section>
  </div>;
}

function WorkspaceState({ title, copy }: { title: string; copy: string }) { return <section className="panel p-8 text-center"><h1 className="break-words text-xl font-semibold text-ink">{title}</h1><p className="mt-2 text-sm text-slate-500">{copy}</p><Link to="/#repositories" className="button-secondary mt-5"><ArrowLeft size={16}/> Back to repositories</Link></section>; }
export function CapabilitySummary({ capabilities }: { capabilities: RepositoryCapabilities }) { const items = [['Source search', capabilities.hasIndexedContent], ['Source code', capabilities.hasSourceCode], ['Documentation', capabilities.hasDocumentation], ['Tests', capabilities.hasTests], ['API endpoints', capabilities.hasApiEndpoints]] as const; const languages = capabilities.languages?.filter(item => item.indexed + item.skippedUnsupported + item.skippedExcluded + item.skippedBinary > 0) ?? []; return <div className="mt-4"><div className="flex flex-wrap items-center gap-2"><p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Analysis coverage</p>{capabilities.coverageStatus && <span className={`rounded-full px-2 py-0.5 text-xs font-semibold ${capabilities.coverageStatus === 'full' ? 'bg-emerald-50 text-emerald-800' : capabilities.coverageStatus === 'partial' ? 'bg-amber-50 text-amber-800' : 'bg-slate-100 text-slate-600'}`}>{capabilities.coverageStatus === 'full' ? 'Full source coverage' : capabilities.coverageStatus === 'partial' ? 'Partial source coverage' : 'No analyzable source'}</span>}</div><ul className="mt-2 flex flex-wrap gap-2">{items.map(([label, available]) => <li key={label} className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${available ? 'bg-emerald-50 text-emerald-800' : 'bg-slate-100 text-slate-600'}`}>{available ? <BookOpenCheck size={13}/> : <MinusCircle size={13}/>} {label}: {available ? 'Available' : 'Not detected'}</li>)}</ul>{languages.length > 0 && <div className="responsive-table mt-3"><table className="w-full text-left text-xs"><thead><tr className="text-slate-500"><th>Language</th><th>Indexed</th><th>Excluded</th><th>Binary</th><th>Unsupported</th></tr></thead><tbody>{languages.map(item => <tr key={item.language} className="border-t border-slate-100"><td className="font-semibold text-ink">{item.language}</td><td>{item.indexed}</td><td>{item.skippedExcluded}</td><td>{item.skippedBinary}</td><td>{item.skippedUnsupported}</td></tr>)}</tbody></table></div>}{!capabilities.hasSourceCode && <p className="mt-3 text-sm text-slate-600">Executable source support is limited for this index. Results may be restricted to documentation or configuration files.</p>}</div>; }
function RepositorySummary({ repository, capabilities, onReindex, reindexing }: { repository: RegisteredRepository; capabilities: RepositoryCapabilities; onReindex: () => void; reindexing: boolean }) { return <div><p className="text-sm text-slate-500">Analysis is pinned to commit {repository.commitSha?.slice(0, 8)}. Choose a tool above to inspect evidence or ask a cited question.</p>{!capabilities.hasSourceCode && <p className="mt-4 rounded-xl bg-amber-50 p-4 text-sm text-amber-800">Executable source support is limited for this index. Results may be restricted to documentation or configuration files.</p>}<button className="mt-5 text-xs font-semibold text-brand-600 disabled:opacity-50" disabled={reindexing} onClick={onReindex}>{reindexing ? 'Queuing re-index...' : 'Re-index this repository'}</button></div>; }

function EndpointCatalog({ organizationId, repository }: { organizationId: string; repository: RegisteredRepository }) {
  const [params, setParams] = useSearchParams(); const search = params.get('endpointSearch') ?? ''; const method = params.get('method') ?? ''; const authorization = params.get('authorization') ?? ''; const expanded = params.get('endpoints') === 'all';
  const endpoints = useQuery({ queryKey: ['endpoint-catalog', organizationId, repository.id, method, search, authorization], queryFn: async () => (await api.get<RepositoryEndpoint[]>(`/organizations/${organizationId}/repositories/${repository.id}/endpoints`, { params: { method: method || undefined, search: search || undefined, requiresAuthorization: authorization || undefined } })).data });
  function update(key: string, value: string) { const next = new URLSearchParams(params); if (value) next.set(key, value); else next.delete(key); if (key !== 'endpoints') next.delete('endpoints'); setParams(next, { replace: true }); }
  const visible = endpoints.data?.slice(0, visibleEndpointCount(endpoints.data.length, expanded));
  return <div><p className="text-sm text-slate-500">Routes detected in commit {repository.commitSha?.slice(0, 8)}.</p><div className="mt-4 grid gap-3 md:grid-cols-3"><input aria-label="Search endpoints" className="control" placeholder="Search route or handler" value={search} onChange={event => update('endpointSearch', event.target.value)}/><select aria-label="HTTP method" className="control" value={method} onChange={event => update('method', event.target.value)}><option value="">All methods</option>{['GET','POST','PUT','PATCH','DELETE'].map(value => <option key={value}>{value}</option>)}</select><select aria-label="Endpoint authorization" className="control" value={authorization} onChange={event => update('authorization', event.target.value)}><option value="">Any authorization</option><option value="true">Authorized</option><option value="false">Anonymous</option></select></div>
    {endpoints.isLoading ? <p className="mt-5 text-sm text-slate-500" role="status">Loading endpoint catalog...</p> : endpoints.isError ? <div className="error mt-5" role="alert">{getApiError(endpoints.error)}</div> : endpoints.data?.length ? <><p className="mt-4 text-xs font-medium text-slate-500">Showing {visible?.length} of {endpoints.data.length} matching endpoints.</p><div className="responsive-table mt-3 border-0"><table className="w-full text-left text-sm"><thead className="text-xs uppercase text-slate-400"><tr><th className="pb-3">Method</th><th className="pb-3">Route</th><th className="pb-3">Handler</th><th className="pb-3">Access</th></tr></thead><tbody>{visible?.map(endpoint => <tr key={endpoint.id} className="border-t border-slate-100"><td className="py-3 font-semibold text-brand-700">{endpoint.httpMethod}</td><td className="py-3 font-mono text-xs">{endpoint.route}</td><td className="py-3"><a href={endpoint.sourceUrl} target="_blank" rel="noreferrer" className="text-brand-600 hover:underline">{endpoint.handler}</a><p className="text-xs text-slate-400">{endpoint.path}:{endpoint.line}</p>{endpoint.downstreamSymbols.length > 0 && <p className="mt-1 text-xs text-slate-500">Calls: {endpoint.downstreamSymbols.join(', ')}</p>}</td><td className="py-3">{endpoint.requiresAuthorization ? 'Authorized' : 'Anonymous'}</td></tr>)}</tbody></table></div>{endpoints.data.length > 5 && <button type="button" className="button-secondary mt-4 min-h-0 px-3 py-2 text-xs" aria-expanded={expanded} onClick={() => update('endpoints', expanded ? '' : 'all')}>{expanded ? 'Show less' : `Show all ${endpoints.data.length} endpoints`}</button>}</> : <p className="empty-state mt-5">No endpoints matched the current filters.</p>}
  </div>;
}
