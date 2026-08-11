import { useRef, useState, type KeyboardEvent } from 'react';
import { Boxes, Clock3, Cloud, Database, ExternalLink, GitBranch, Maximize2, Minus, Plus, RotateCcw } from 'lucide-react';
import type { CodeFlowBoundary, CodeFlowStep, CodeFlowTrace } from './types';

const maxSteps = 24;
const unsafeText = /<(?:script|iframe|object|embed)|javascript:/i;

export function validateCodeFlowTrace(trace: CodeFlowTrace): string | undefined {
  if (trace.schemaVersion !== '1.0') return 'Unsupported trace version.';
  if (!trace.summary?.trim() || unsafeText.test(trace.summary) || trace.summary.length > 4000) return 'The trace summary is invalid.';
  if (!Array.isArray(trace.steps) || trace.steps.length === 0 || trace.steps.length > maxSteps) return 'The trace is empty or too large to diagram safely.';
  const keys = new Set<string>();
  for (const [index, step] of trace.steps.entries()) {
    if (!step.key?.trim() || keys.has(step.key) || step.order !== index + 1) return 'The trace contains duplicate, cyclic, or unordered steps.';
    keys.add(step.key);
    const text = [step.title, step.component, step.symbol, step.responsibility, step.handoff];
    if (text.some(value => typeof value !== 'string' || value.length > 2000 || unsafeText.test(value))) return 'The trace contains unsupported content.';
    if (!['Synchronous', 'Asynchronous', 'Background', 'Persistence', 'External'].includes(step.boundary)) return 'The trace contains an unsupported boundary.';
    if (!['Confirmed', 'Inferred', 'Missing'].includes(step.evidenceLevel)) return 'The trace contains an unsupported evidence level.';
    if (!Array.isArray(step.citations) || step.citations.some(citation => !safeCitation(citation.sourceUrl, citation.commitSha, trace.commitSha))) return 'The trace contains an unsafe or unpinned citation.';
    if (step.evidenceLevel === 'Confirmed' && step.citations.length === 0) return 'A confirmed step is missing its source citation.';
  }
  return undefined;
}

function safeCitation(sourceUrl: string, citationCommit: string, traceCommit: string) {
  try {
    const url = new URL(sourceUrl);
    return url.protocol === 'https:' && citationCommit === traceCommit && url.pathname.includes(`/${traceCommit}/`);
  } catch { return false; }
}

const boundaryDetails: Record<CodeFlowBoundary, { label: string; icon: typeof Boxes; className: string }> = {
  Synchronous: { label: 'Synchronous function', icon: Boxes, className: 'bg-slate-100 text-slate-700' },
  Asynchronous: { label: 'Async boundary', icon: Clock3, className: 'bg-sky-50 text-sky-700' },
  Background: { label: 'Background work', icon: GitBranch, className: 'bg-violet-50 text-violet-700' },
  Persistence: { label: 'Data store', icon: Database, className: 'bg-emerald-50 text-emerald-700' },
  External: { label: 'External service', icon: Cloud, className: 'bg-amber-50 text-amber-800' }
};

export function CodeFlowDiagram({ trace }: { trace: CodeFlowTrace }) {
  const invalidReason = validateCodeFlowTrace(trace);
  const [selectedKey, setSelectedKey] = useState(trace.steps[0]?.key);
  const [zoom, setZoom] = useState(1);
  const diagramRef = useRef<HTMLDivElement>(null);
  if (invalidReason) return <CodeFlowFallback trace={trace} reason={invalidReason}/>;
  const selected = (trace.steps.find(step => step.key === selectedKey) ?? trace.steps[0])!;
  function moveFocus(event: KeyboardEvent<HTMLDivElement>) {
    if (!['ArrowDown', 'ArrowRight', 'ArrowUp', 'ArrowLeft', 'Home', 'End'].includes(event.key)) return;
    const nodes = [...(diagramRef.current?.querySelectorAll<HTMLButtonElement>('[data-flow-node]') ?? [])];
    const current = nodes.indexOf(document.activeElement as HTMLButtonElement);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? nodes.length - 1 : Math.min(nodes.length - 1, Math.max(0, current + (event.key === 'ArrowDown' || event.key === 'ArrowRight' ? 1 : -1)));
    event.preventDefault(); nodes[next]?.focus(); nodes[next]?.click();
  }
  return <div className="mt-4">
    <div className="flex flex-wrap items-center justify-between gap-3">
      <div><h5 className="text-sm font-semibold text-ink">Interactive flow diagram</h5><p className="text-xs text-slate-500">Select a node to inspect its commit-pinned evidence.</p></div>
      <div className="flex items-center gap-1" aria-label="Diagram zoom controls">
        <button type="button" aria-label="Zoom out" disabled={zoom <= .8} className="rounded-lg border border-slate-200 p-2 text-slate-600 disabled:opacity-40" onClick={() => setZoom(value => Math.max(.8, value - .2))}><Minus size={14}/></button>
        <button type="button" aria-label="Reset zoom" className="rounded-lg border border-slate-200 p-2 text-slate-600" onClick={() => setZoom(1)}><RotateCcw size={14}/></button>
        <button type="button" aria-label="Zoom in" disabled={zoom >= 1.4} className="rounded-lg border border-slate-200 p-2 text-slate-600 disabled:opacity-40" onClick={() => setZoom(value => Math.min(1.4, value + .2))}><Plus size={14}/></button>
        <button type="button" aria-label="Fit diagram" className="rounded-lg border border-slate-200 p-2 text-slate-600" onClick={() => { setZoom(.8); diagramRef.current?.scrollTo({ left: 0, top: 0, behavior: 'smooth' }); }}><Maximize2 size={14}/></button>
      </div>
    </div>
    <div ref={diagramRef} role="group" aria-label={`Code flow diagram with ${trace.steps.length} ordered nodes`} className="mt-3 max-h-[36rem] overflow-auto rounded-xl border border-slate-200 bg-slate-50 p-4" onKeyDown={moveFocus}>
      <div className="mx-auto grid min-w-64 max-w-xl justify-items-stretch transition-transform motion-reduce:transition-none" style={{ transform: `scale(${zoom})`, transformOrigin: 'top center', marginBottom: `${Math.max(0, (zoom - 1) * trace.steps.length * 120)}px` }}>
        {trace.steps.map((step, index) => <FlowNode key={step.key} step={step} next={trace.steps[index + 1]} selected={step.key === selected.key} onSelect={() => setSelectedKey(step.key)}/>)}
      </div>
    </div>
    <div aria-live="polite" className="mt-3 rounded-xl border border-brand-100 bg-brand-50/50 p-4">
      <p className="text-xs font-semibold uppercase tracking-wide text-brand-700">Selected source evidence</p>
      <p className="mt-1 text-sm font-semibold text-ink">{selected.order}. {selected.title}</p>
      {selected.citations.length ? <div className="mt-2 flex flex-wrap gap-2">{selected.citations.map(citation => <a key={`${citation.path}-${citation.startLine}`} href={citation.sourceUrl} target="_blank" rel="noreferrer" className="inline-flex max-w-full items-center gap-1 break-all rounded-lg bg-white px-2 py-1 text-xs text-brand-700 hover:underline">{citation.path}:{citation.startLine}-{citation.endLine}<ExternalLink size={11}/></a>)}</div> : <p className="mt-2 text-xs text-slate-600">No confirmed citation is available for this {selected.evidenceLevel.toLowerCase()} node.</p>}
    </div>
    <OrderedTrace trace={trace}/>
  </div>;
}

function FlowNode({ step, next, selected, onSelect }: { step: CodeFlowStep; next?: CodeFlowStep; selected: boolean; onSelect: () => void }) {
  const boundary = boundaryDetails[step.boundary]; const BoundaryIcon = boundary.icon;
  const inferredEdge = next && (step.evidenceLevel !== 'Confirmed' || next.evidenceLevel !== 'Confirmed');
  return <div className="grid justify-items-center">
    <button data-flow-node type="button" aria-pressed={selected} aria-label={`${step.order}. ${step.title}, ${boundary.label}, ${step.evidenceLevel} evidence`} onClick={onSelect} className={`w-full rounded-xl border-2 bg-white p-4 text-left shadow-sm outline-none transition hover:border-brand-400 focus:ring-2 focus:ring-brand-500 ${selected ? 'border-brand-500' : step.evidenceLevel === 'Inferred' ? 'border-dashed border-amber-300' : 'border-slate-200'}`}>
      <span className="flex flex-wrap items-start justify-between gap-2"><span><span className="block text-sm font-semibold text-ink">{step.order}. {step.title}</span><span className="mt-1 block break-all font-mono text-xs text-brand-700">{step.component} / {step.symbol}</span></span><span className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-[10px] font-semibold ${boundary.className}`}><BoundaryIcon size={12}/>{boundary.label}</span></span>
      <span className="mt-2 block text-xs leading-5 text-slate-600">{step.responsibility}</span>
      <span className="mt-2 block text-[10px] font-semibold uppercase tracking-wide text-slate-500">{step.evidenceLevel} evidence</span>
    </button>
    {next && <div aria-label={inferredEdge ? 'Inferred handoff' : 'Confirmed handoff'} className="grid h-14 justify-items-center"><span className={`h-9 border-l-2 ${inferredEdge ? 'border-dashed border-amber-500' : 'border-solid border-brand-500'}`}/><span className="-mt-2 text-xs text-slate-500">{inferredEdge ? '◇ inferred' : '▼ confirmed'}</span></div>}
  </div>;
}

function OrderedTrace({ trace }: { trace: CodeFlowTrace }) { return <details className="mt-4 rounded-xl border border-slate-200 p-4"><summary className="cursor-pointer text-sm font-semibold text-ink">Ordered text trace</summary><ol className="mt-3 grid gap-3">{trace.steps.map(step => <li key={step.key} className="text-sm text-slate-600"><strong className="text-ink">{step.order}. {step.title}</strong> — {step.responsibility}{step.handoff && <span className="block text-xs text-slate-500">Next: {step.handoff}</span>}</li>)}</ol></details>; }

function CodeFlowFallback({ trace, reason }: { trace: CodeFlowTrace; reason: string }) { return <div className="mt-4 rounded-xl border border-amber-200 bg-amber-50 p-4" role="status"><p className="text-sm font-semibold text-amber-900">Diagram unavailable</p><p className="mt-1 text-xs text-amber-800">{reason} Showing the safe text-only trace.</p><OrderedTrace trace={{ ...trace, steps: Array.isArray(trace.steps) ? trace.steps.slice(0, maxSteps) : [] }}/></div>; }
