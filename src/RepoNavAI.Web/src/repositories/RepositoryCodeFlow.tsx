import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ExternalLink, Square, Workflow } from 'lucide-react';
import { api, getApiError } from '../api/client';
import type { CodeFlowTrace, RegisteredRepository } from './types';

export function RepositoryCodeFlow({ organizationId, repository }: { organizationId: string; repository: RegisteredRepository }) {
  const [question, setQuestion] = useState(''); const [trace, setTrace] = useState<CodeFlowTrace>();
  const [error, setError] = useState(''); const [notice, setNotice] = useState(''); const [isGenerating, setGenerating] = useState(false);
  const abortRef = useRef<AbortController | undefined>(undefined);
  useEffect(() => () => abortRef.current?.abort(), []);

  async function submit(event: FormEvent) {
    event.preventDefault(); const value = question.trim(); if (!value || isGenerating) return;
    const controller = new AbortController(); abortRef.current = controller; setTrace(undefined); setError(''); setNotice(''); setGenerating(true);
    try {
      const response = await api.post<CodeFlowTrace>(`/organizations/${organizationId}/repositories/${repository.id}/code-flow`, { question: value }, { signal: controller.signal });
      if (!controller.signal.aborted) setTrace(response.data);
    } catch (reason) { if (!controller.signal.aborted) setError(getApiError(reason)); }
    finally { if (abortRef.current === controller) { abortRef.current = undefined; setGenerating(false); } }
  }
  function cancel() { abortRef.current?.abort(); setGenerating(false); setNotice('Trace generation stopped. You can submit another question.'); }

  return <div className="mt-8 min-w-0 border-t border-slate-200 pt-6">
    <div className="flex items-center gap-2"><Workflow className="text-brand-600" size={19}/><h3 className="font-semibold text-ink">Explain a code flow</h3></div>
    <p className="mt-1 text-sm text-slate-500">Trace one behavior through concrete functions, data handoffs, and commit-pinned evidence.</p>
    <form className="mt-4 flex flex-col gap-3 md:flex-row" onSubmit={submit}>
      <textarea className="min-h-24 min-w-0 flex-1 resize-y rounded-xl border border-slate-200 px-4 py-3 text-sm outline-none focus:border-brand-500" maxLength={2000} required disabled={isGenerating} value={question} onChange={event => setQuestion(event.target.value)} placeholder="Trace repository indexing from the API request through the worker and database persistence. Cite each function-to-function handoff."/>
      {isGenerating ? <button key="stop-flow" type="button" className="inline-flex h-11 items-center justify-center gap-2 rounded-xl border border-slate-200 px-5 text-sm font-semibold text-slate-600" onClick={event => { event.preventDefault(); event.stopPropagation(); cancel(); }}><Square size={14}/> Stop</button> : <button key="explain-flow" type="submit" className="h-11 rounded-xl bg-brand-600 px-5 text-sm font-semibold text-white">Explain flow</button>}
    </form>
    {isGenerating && <p role="status" className="mt-3 text-sm text-slate-500">Retrieving and validating the execution trace…</p>}
    {notice && <p role="status" className="mt-3 text-sm font-medium text-slate-500">{notice}</p>}
    {error && <div className="error mt-4">{error}</div>}
    {trace && <div className="mt-6">
      <div className="flex flex-wrap items-center justify-between gap-2"><h4 className="font-semibold text-ink">Execution trace</h4><span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-600">commit {trace.commitSha.slice(0, 8)}</span></div>
      <p className="mt-3 text-sm leading-6 text-slate-600">{trace.summary}</p>
      <ol className="mt-5 grid gap-3">{trace.steps.map(step => <li key={step.key} className="rounded-xl border border-slate-200 p-4">
        <div className="flex flex-wrap items-start justify-between gap-2"><div><p className="font-semibold text-ink">{step.order}. {step.title}</p><p className="mt-1 font-mono text-xs text-brand-700">{step.component} · {step.symbol}</p></div><div className="flex gap-2"><span className="rounded-full bg-slate-100 px-2 py-1 text-[10px] font-semibold text-slate-600">{step.boundary}</span><span className={`rounded-full px-2 py-1 text-[10px] font-semibold ${step.evidenceLevel === 'Confirmed' ? 'bg-emerald-50 text-emerald-700' : step.evidenceLevel === 'Inferred' ? 'bg-amber-50 text-amber-700' : 'bg-slate-100 text-slate-600'}`}>{step.evidenceLevel}</span></div></div>
        <p className="mt-3 text-sm leading-6 text-slate-600">{step.responsibility}</p>{step.handoff && <p className="mt-2 text-xs leading-5 text-slate-500"><span className="font-semibold">Next handoff:</span> {step.handoff}</p>}
        {step.citations.length > 0 && <div className="mt-3 flex flex-wrap gap-2">{step.citations.map((citation, index) => <a key={`${citation.path}-${citation.startLine}`} href={citation.sourceUrl} target="_blank" rel="noreferrer" className="inline-flex max-w-full items-center gap-1 break-all rounded bg-brand-50 px-2 py-1 text-xs text-brand-700 hover:underline">[{index + 1}] {citation.path}:{citation.startLine}-{citation.endLine}<ExternalLink size={11}/></a>)}</div>}
      </li>)}</ol>
      {trace.missingEvidence.length > 0 && <div className="mt-4 rounded-xl bg-amber-50 p-4"><p className="text-xs font-semibold uppercase tracking-wide text-amber-800">Missing or unresolved evidence</p><ul className="mt-2 list-disc pl-5 text-sm text-amber-900">{trace.missingEvidence.map(item => <li key={item}>{item}</li>)}</ul></div>}
    </div>}
  </div>;
}
