import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ExternalLink, MessageSquareText, RefreshCw, Search, Sparkles, Square, Workflow } from 'lucide-react';
import { api, getApiError, streamApi } from '../api/client';
import type { CodeFlowTrace, OrientationExperience, OrientationFocus, OrientationPlan, OrientationRole, RegisteredRepository, RepositoryChatCitation, RepositoryChatEvent, SemanticSearchResult } from './types';
import { applicableGuidedPrompts, nextGuidedPromptSet, type GuidedPrompt } from './guidedPrompts';
import { CodeFlowDiagram } from './CodeFlowDiagram';

export type AssistantMode = 'Auto' | 'Search' | 'Answer' | 'Orientation' | 'CodeFlow';
type AssistantResult =
  | { kind: 'Search'; results: SemanticSearchResult[] }
  | { kind: 'Answer'; answer: string; citations: RepositoryChatCitation[] }
  | { kind: 'Orientation'; plan: OrientationPlan }
  | { kind: 'CodeFlow'; trace: CodeFlowTrace };
interface IntentResponse { intent: Exclude<AssistantMode, 'Auto'>; reason: string }
interface CapabilityResponse { hasIndexedContent: boolean; hasSourceCode: boolean; hasTests: boolean; hasDocumentation: boolean; hasApiEndpoints: boolean; representativePaths: string[] }

export function RepositoryAssistant({ organizationId, repository, initialMode = 'Auto' }: { organizationId: string; repository: RegisteredRepository; initialMode?: AssistantMode }) {
  const [prompt, setPrompt] = useState(''); const [mode, setMode] = useState<AssistantMode>(initialMode); const [resolvedMode, setResolvedMode] = useState<Exclude<AssistantMode, 'Auto'>>();
  const [result, setResult] = useState<AssistantResult>(); const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [isRunning, setRunning] = useState(false);
  const [role, setRole] = useState<OrientationRole>('Developer'); const [experience, setExperience] = useState<OrientationExperience>('MidLevel');
  const [focus, setFocus] = useState<OrientationFocus>('GeneralOnboarding'); const [timeBudgetMinutes, setTime] = useState(60);
  const [capabilities, setCapabilities] = useState<CapabilityResponse>(); const [promptSetStart, setPromptSetStart] = useState(0);
  const abortRef = useRef<AbortController | undefined>(undefined);
  const promptRef = useRef<HTMLTextAreaElement | null>(null);
  useEffect(() => () => abortRef.current?.abort(), []);
  useEffect(() => {
    let active = true; setResult(undefined); setResolvedMode(undefined); setError(''); setNotice(''); setCapabilities(undefined); setPromptSetStart(0);
    void api.get<OrientationPlan | null>(`/organizations/${organizationId}/repositories/${repository.id}/orientation-plan`).then(response => {
      if (active && response.status !== 204 && response.data) setResult({ kind: 'Orientation', plan: response.data });
    }).catch(reason => { if (active) setError(getApiError(reason)); });
    void api.get<CapabilityResponse>(`/organizations/${organizationId}/repositories/${repository.id}/capabilities`).then(response => {
      if (active) setCapabilities(response.data);
    }).catch(() => { if (active) setCapabilities({ hasIndexedContent: false, hasSourceCode: false, hasTests: false, hasDocumentation: false, hasApiEndpoints: false, representativePaths: [] }); });
    return () => { active = false; };
  }, [organizationId, repository.id]);

  async function submit(event: FormEvent) {
    event.preventDefault(); const value = prompt.trim(); if (!value || isRunning) return;
    const controller = new AbortController(); abortRef.current = controller; setResult(undefined); setError(''); setNotice(''); setResolvedMode(undefined); setRunning(true);
    try {
      const intent: Exclude<AssistantMode, 'Auto'> = mode === 'Auto' ? (await api.post<IntentResponse>(`/organizations/${organizationId}/repositories/${repository.id}/assistant/intent`, { prompt: value }, { signal: controller.signal })).data.intent : mode;
      setResolvedMode(intent);
      if (intent === 'Search') {
        const response = await api.get<SemanticSearchResult[]>(`/organizations/${organizationId}/repositories/${repository.id}/semantic-search`, { params: { query: value }, signal: controller.signal });
        if (!controller.signal.aborted) setResult({ kind: 'Search', results: response.data });
      } else if (intent === 'Answer') {
        let answer = ''; let citations: RepositoryChatCitation[] = [];
        setResult({ kind: 'Answer', answer, citations });
        await streamApi<RepositoryChatEvent>(`/organizations/${organizationId}/repositories/${repository.id}/chat`, { question: value }, controller.signal, chatEvent => {
          if (chatEvent.type === 'Citations') citations = chatEvent.citations;
          else if (chatEvent.type === 'Delta') answer += chatEvent.delta;
          else if (chatEvent.type === 'Error') setError(chatEvent.delta);
          setResult({ kind: 'Answer', answer, citations: [...citations] });
        });
      } else if (intent === 'Orientation') {
        const response = await api.post<OrientationPlan>(`/organizations/${organizationId}/repositories/${repository.id}/orientation-plan`, { role, experience, focus, timeBudgetMinutes, objective: value }, { signal: controller.signal });
        if (!controller.signal.aborted) setResult({ kind: 'Orientation', plan: response.data });
      } else {
        const response = await api.post<CodeFlowTrace>(`/organizations/${organizationId}/repositories/${repository.id}/code-flow`, { question: value }, { signal: controller.signal });
        if (!controller.signal.aborted) setResult({ kind: 'CodeFlow', trace: response.data });
      }
    } catch (reason) {
      if (!controller.signal.aborted) setError((reason as { isAxiosError?: boolean })?.isAxiosError ? getApiError(reason) : reason instanceof Error ? reason.message : getApiError(reason));
    } finally {
      if (abortRef.current === controller) { abortRef.current = undefined; setRunning(false); }
    }
  }
  function cancel() { abortRef.current?.abort(); setRunning(false); setNotice('Request stopped. You can update the prompt or mode and try again.'); }
  async function updateProgress(stepKey: string) {
    if (result?.kind !== 'Orientation') return;
    const completedStepKeys = result.plan.steps.filter(step => step.completed !== (step.key === stepKey)).map(step => step.key);
    try {
      const response = await api.put<OrientationPlan>(`/organizations/${organizationId}/repositories/${repository.id}/orientation-plan/${result.plan.id}/progress`, { completedStepKeys });
      setResult({ kind: 'Orientation', plan: response.data });
    } catch (reason) { setError(getApiError(reason)); }
  }
  const showOrientationOptions = mode === 'Orientation' || resolvedMode === 'Orientation';
  const guidedPrompts = applicableGuidedPrompts({ hasIndexedContent: capabilities?.hasIndexedContent ?? false, hasSourceCode: capabilities?.hasSourceCode ?? false, hasTests: capabilities?.hasTests ?? false, hasDocumentation: capabilities?.hasDocumentation ?? false, apiEndpoints: capabilities?.hasApiEndpoints ?? false, representativePaths: capabilities?.representativePaths ?? [] });
  const visiblePrompts = nextGuidedPromptSet(guidedPrompts, promptSetStart);
  function selectPrompt(suggestion: GuidedPrompt) {
    setPrompt(suggestion.text); setMode(suggestion.mode); setResolvedMode(undefined);
    requestAnimationFrame(() => promptRef.current?.focus());
  }
  function rotatePrompts() { if (guidedPrompts.length > visiblePrompts.length) setPromptSetStart(current => (current + visiblePrompts.length) % guidedPrompts.length); }

  return <div id="repository-assistant" className="min-w-0">
    <div className="flex items-center gap-2"><Sparkles className="text-brand-600" size={19}/><h3 className="font-semibold text-ink">Repository assistant</h3></div>
    <p className="mt-1 text-sm text-slate-500">Search source, ask a cited question, build an orientation, or trace a code flow from one place.</p>
    <div className="mt-4 rounded-xl bg-slate-50 p-3" aria-label="Suggested repository questions">
      <div className="flex items-center justify-between gap-3"><p className="text-xs font-semibold uppercase tracking-wide text-slate-500">Try a repository-aware prompt</p><button type="button" aria-label="Show different prompts" disabled={guidedPrompts.length <= visiblePrompts.length} className="inline-flex shrink-0 items-center gap-1 rounded-lg px-2 py-1 text-xs font-semibold text-brand-700 hover:bg-brand-50 focus:outline-none focus:ring-2 focus:ring-brand-500 disabled:cursor-not-allowed disabled:opacity-40" onClick={rotatePrompts}><RefreshCw size={13} aria-hidden="true"/> Refresh</button></div>
      {capabilities && visiblePrompts.length === 0 ? <p className="mt-2 text-sm text-slate-500">No guided prompts are available because this index contains no supported searchable source. You can still enter your own question.</p> : <div className="mt-2 flex flex-wrap gap-2">{visiblePrompts.map(suggestion => <button key={suggestion.id} type="button" className="max-w-full rounded-lg border border-slate-200 bg-white px-3 py-2 text-left text-xs leading-5 text-slate-600 hover:border-brand-500 hover:text-brand-700 focus:outline-none focus:ring-2 focus:ring-brand-500" onClick={() => selectPrompt(suggestion)}><span className="mr-2 font-semibold text-brand-700">{suggestion.mode === 'CodeFlow' ? 'Code flow' : suggestion.mode}</span>{suggestion.text}</button>)}</div>}
    </div>
    <form className="mt-4" onSubmit={submit}>
      <div className="flex flex-col gap-3 md:flex-row md:items-stretch">
        <label className="flex min-w-0 flex-1 flex-col text-xs font-semibold text-slate-500">What do you want to understand?<textarea ref={promptRef} className="mt-1 min-h-24 w-full flex-1 resize-y rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm font-normal text-ink outline-none focus:border-brand-500" maxLength={2000} required disabled={isRunning} value={prompt} onChange={event => setPrompt(event.target.value)} placeholder="How does repository indexing recover after an API restart?"/></label>
        <div className="flex flex-col md:w-44">
          <label className="text-xs font-semibold text-slate-500">Mode<select aria-label="Assistant mode" className="mt-1 h-11 w-full rounded-xl border border-slate-200 bg-white px-3 text-sm text-ink" value={mode} disabled={isRunning} onChange={event => { setMode(event.target.value as AssistantMode); setResolvedMode(undefined); }}>{['Auto','Search','Answer','Orientation','CodeFlow'].map(value => <option key={value} value={value}>{value === 'CodeFlow' ? 'Code flow' : value}</option>)}</select></label>
          {isRunning ? <button key="stop-assistant" type="button" className="mt-3 inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-slate-200 px-5 text-sm font-semibold text-slate-600 md:flex-1" onClick={event => { event.preventDefault(); event.stopPropagation(); cancel(); }}><Square size={14}/> Stop</button> : <button key="run-assistant" type="submit" className="mt-3 inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-brand-600 px-5 text-sm font-semibold text-white md:flex-1"><Sparkles size={16}/> Ask</button>}
        </div>
      </div>
      {showOrientationOptions && <div className="mt-3 grid gap-3 rounded-xl bg-slate-50 p-3 sm:grid-cols-4"><CompactSelect label="Role" value={role} values={['Developer','Tester','Architect','DevOps','Product']} onChange={value => setRole(value as OrientationRole)}/><CompactSelect label="Experience" value={experience} values={['NewToSoftware','Junior','MidLevel','Senior']} onChange={value => setExperience(value as OrientationExperience)}/><CompactSelect label="Focus" value={focus} values={['GeneralOnboarding','ImplementFeature','FixBug','Architecture','Operations']} onChange={value => setFocus(value as OrientationFocus)}/><CompactSelect label="Time" value={String(timeBudgetMinutes)} values={['30','60','120','240']} onChange={value => setTime(Number(value))}/></div>}
    </form>
    {resolvedMode && <p className="mt-3 text-xs font-semibold text-brand-700">Using {resolvedMode === 'CodeFlow' ? 'Code flow' : resolvedMode} mode{mode === 'Auto' ? ' (automatically selected)' : ''}.</p>}
    {isRunning && <p role="status" className="mt-3 text-sm text-slate-500">Working from the latest indexed commit…</p>}{notice && <p role="status" className="mt-3 text-sm font-medium text-slate-500">{notice}</p>}{error && <div className="error mt-4">{error}</div>}
    {result?.kind === 'Search' && <SearchResultView results={result.results}/>}
    {result?.kind === 'Answer' && <AnswerView answer={result.answer} citations={result.citations} isRunning={isRunning}/>}
    {result?.kind === 'Orientation' && <OrientationView plan={result.plan} onToggle={updateProgress}/>}
    {result?.kind === 'CodeFlow' && <CodeFlowView trace={result.trace}/>}
  </div>;
}

function CompactSelect({ label, value, values, onChange }: { label: string; value: string; values: string[]; onChange: (value: string) => void }) { return <label className="text-xs font-semibold text-slate-500">{label}<select className="mt-1 h-9 w-full rounded-lg border border-slate-200 bg-white px-2 text-xs" value={value} onChange={event => onChange(event.target.value)}>{values.map(item => <option key={item}>{item}</option>)}</select></label>; }
function EvidenceBadge({ level }: { level: 'Confirmed' | 'Inferred' | 'Missing' }) { return <span className={`rounded-full px-2 py-1 text-[10px] font-semibold ${level === 'Confirmed' ? 'bg-emerald-50 text-emerald-700' : level === 'Inferred' ? 'bg-amber-50 text-amber-700' : 'bg-slate-100 text-slate-600'}`}>{level}</span>; }
function SearchResultView({ results }: { results: SemanticSearchResult[] }) { return <section id="assistant-search-result" aria-label="Search results" className="mt-6 scroll-mt-6"><div className="flex items-center gap-2"><Search size={17} className="text-brand-600"/><h4 className="font-semibold text-ink">Source matches</h4></div>{results.length ? <div className="mt-3 grid gap-3">{results.map(item => <article key={item.chunkId} className="min-w-0 overflow-hidden rounded-xl border border-slate-200 p-4"><a href={item.sourceUrl} target="_blank" rel="noreferrer" className="break-all font-semibold text-brand-600 hover:underline">{item.path}:{item.startLine}-{item.endLine}</a><pre className="mt-3 max-h-56 max-w-full overflow-y-auto whitespace-pre-wrap [overflow-wrap:anywhere] text-xs leading-5 text-slate-600">{item.content}</pre></article>)}</div> : <p className="mt-3 rounded-lg bg-slate-50 p-4 text-sm text-slate-500">No relevant indexed source was found.</p>}</section>; }
function AnswerView({ answer, citations, isRunning }: { answer: string; citations: RepositoryChatCitation[]; isRunning: boolean }) { return <section id="assistant-answer-result" aria-label="Repository answer" className="mt-6 scroll-mt-6"><div className="flex items-center gap-2"><MessageSquareText size={17} className="text-brand-600"/><h4 className="font-semibold text-ink">Cited answer</h4></div><div className="mt-3 rounded-xl border border-slate-200 bg-slate-50 p-4"><p className="whitespace-pre-wrap text-sm leading-6 text-slate-700">{answer}{isRunning && <span className="ml-1 inline-block h-4 w-1 animate-pulse bg-brand-500"/>}</p></div><CitationList citations={citations}/></section>; }
function CitationList({ citations }: { citations: Array<{ path: string; startLine: number; endLine: number; sourceUrl: string }> }) { return citations.length ? <div className="mt-3 flex flex-wrap gap-2">{citations.map((citation, index) => <a key={`${citation.path}-${citation.startLine}-${index}`} href={citation.sourceUrl} target="_blank" rel="noreferrer" className="inline-flex max-w-full items-center gap-1 break-all rounded bg-brand-50 px-2 py-1 text-xs text-brand-700 hover:underline">[{index + 1}] {citation.path}:{citation.startLine}-{citation.endLine}<ExternalLink size={11}/></a>)}</div> : null; }
function OrientationView({ plan, onToggle }: { plan: OrientationPlan; onToggle: (key: string) => void }) { return <section id="assistant-orientation-result" aria-label="Orientation plan" className="mt-6 scroll-mt-6"><div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-semibold text-ink">Orientation plan</h4><span className="rounded-full bg-slate-100 px-2 py-1 text-xs">commit {plan.commitSha.slice(0,8)}</span></div><p className="mt-3 text-sm leading-6 text-slate-600">{plan.summary}</p><ol className="mt-4 grid gap-3">{plan.steps.map((step,index) => <li key={step.key} className="rounded-xl border border-slate-200 p-4"><label className="flex cursor-pointer items-start gap-3"><input className="mt-1" type="checkbox" checked={step.completed} onChange={() => onToggle(step.key)}/><span><span className="font-semibold text-ink">{index + 1}. {step.title}</span><span className="ml-2"><EvidenceBadge level={step.evidenceLevel}/></span><span className="mt-1 block text-sm text-slate-600">{step.objective}</span><span className="mt-2 block text-xs text-slate-500">{step.evidence}</span></span></label><CitationList citations={step.citations}/></li>)}</ol>{plan.isStale && <p className="mt-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">A newer repository index is available. Run the orientation again to refresh this plan.</p>}</section>; }
function CodeFlowView({ trace }: { trace: CodeFlowTrace }) { return <section id="assistant-code-flow-result" aria-label="Code flow" className="mt-6 scroll-mt-6"><div className="flex items-center justify-between gap-2"><div className="flex items-center gap-2"><Workflow size={17} className="text-brand-600"/><h4 className="font-semibold text-ink">Execution trace</h4></div><span className="rounded-full bg-slate-100 px-2 py-1 text-xs">commit {trace.commitSha.slice(0,8)}</span></div><p className="mt-3 text-sm leading-6 text-slate-600">{trace.summary}</p><CodeFlowDiagram trace={trace}/>{trace.missingEvidence.length > 0 && <div className="mt-4 rounded-xl bg-amber-50 p-4"><p className="text-xs font-semibold uppercase text-amber-800">Missing evidence</p><ul className="mt-2 list-disc pl-5 text-sm text-amber-900">{trace.missingEvidence.map(item => <li key={item}>{item}</li>)}</ul></div>}</section>; }
