import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { BookOpenCheck, ExternalLink, RefreshCw } from 'lucide-react';
import { api, getApiError } from '../api/client';
import type { OrientationExperience, OrientationFocus, OrientationPlan, OrientationRole, RegisteredRepository } from './types';

export function RepositoryOrientation({ organizationId, repository }: { organizationId: string; repository: RegisteredRepository }) {
  const queryClient = useQueryClient(); const key = ['orientation-plan', organizationId, repository.id];
  const [role, setRole] = useState<OrientationRole>('Developer'); const [experience, setExperience] = useState<OrientationExperience>('MidLevel');
  const [focus, setFocus] = useState<OrientationFocus>('GeneralOnboarding'); const [timeBudgetMinutes, setTime] = useState(60); const [objective, setObjective] = useState('');
  const plan = useQuery({ queryKey: key, queryFn: async () => {
    const response = await api.get<OrientationPlan | null>(`/organizations/${organizationId}/repositories/${repository.id}/orientation-plan`);
    return response.status === 204 || response.data == null ? null : response.data;
  } });
  const create = useMutation({ mutationFn: async () => (await api.post<OrientationPlan>(`/organizations/${organizationId}/repositories/${repository.id}/orientation-plan`, { role, experience, focus, timeBudgetMinutes, objective: objective.trim() || null })).data, onSuccess: data => queryClient.setQueryData(key, data) });
  const progress = useMutation({ mutationFn: async (completedStepKeys: string[]) => (await api.put<OrientationPlan>(`/organizations/${organizationId}/repositories/${repository.id}/orientation-plan/${plan.data!.id}/progress`, { completedStepKeys })).data, onSuccess: data => queryClient.setQueryData(key, data) });
  function submit(event: FormEvent) { event.preventDefault(); create.mutate(); }
  function toggle(stepKey: string) { if (!plan.data || progress.isPending) return; progress.mutate(plan.data.steps.filter(x => x.completed !== (x.key === stepKey)).map(x => x.key)); }
  const completed = plan.data?.steps.filter(x => x.completed).length ?? 0;

  return <div className="mt-8 min-w-0 border-t border-slate-200 pt-6">
    <div className="flex items-center gap-2"><BookOpenCheck className="text-brand-600" size={19}/><h3 className="font-semibold text-ink">Repository orientation</h3></div>
    <p className="mt-1 text-sm text-slate-500">Build a private, evidence-grounded learning path for your role and current goal.</p>
    <form className="mt-4 grid gap-3 md:grid-cols-4" onSubmit={submit}>
      <label className="text-xs font-semibold text-slate-500">Role<select className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-sm" value={role} onChange={e => setRole(e.target.value as OrientationRole)}>{['Developer','Tester','Architect','DevOps','Product'].map(x => <option key={x}>{x}</option>)}</select></label>
      <label className="text-xs font-semibold text-slate-500">Experience<select className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-sm" value={experience} onChange={e => setExperience(e.target.value as OrientationExperience)}>{['NewToSoftware','Junior','MidLevel','Senior'].map(x => <option key={x}>{x}</option>)}</select></label>
      <label className="text-xs font-semibold text-slate-500">Focus<select className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-sm" value={focus} onChange={e => setFocus(e.target.value as OrientationFocus)}>{['GeneralOnboarding','ImplementFeature','FixBug','Architecture','Operations'].map(x => <option key={x}>{x}</option>)}</select></label>
      <label className="text-xs font-semibold text-slate-500">Available time<select className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 text-sm" value={timeBudgetMinutes} onChange={e => setTime(Number(e.target.value))}>{[30,60,120,240].map(x => <option key={x} value={x}>{x < 60 ? `${x} minutes` : `${x / 60} hour${x > 60 ? 's' : ''}`}</option>)}</select></label>
      <input className="h-11 min-w-0 rounded-xl border border-slate-200 px-4 text-sm md:col-span-3" maxLength={500} value={objective} onChange={e => setObjective(e.target.value)} placeholder="Optional: what are you preparing to change or understand?"/>
      <button className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 text-sm font-semibold text-white disabled:opacity-60" disabled={create.isPending}>{plan.data ? <RefreshCw size={16}/> : <BookOpenCheck size={16}/>} {create.isPending ? 'Building plan…' : plan.data ? 'Regenerate' : 'Create plan'}</button>
    </form>
    <p className="mt-2 text-xs text-slate-400">Your optional objective is used for this generation only and is not saved.</p>
    {create.isError && <div className="error mt-4">{getApiError(create.error)}</div>}
    {plan.isError && <div className="error mt-4">{getApiError(plan.error)}</div>}
    {plan.isLoading ? <p className="mt-5 text-sm text-slate-500">Loading orientation…</p> : plan.data && <div className="mt-6">
      <div className="flex flex-wrap items-center justify-between gap-2"><p className="text-sm font-semibold text-ink">{completed} of {plan.data.steps.length} steps complete</p><div className="flex gap-2"><span className="rounded-full bg-slate-100 px-2 py-1 text-xs text-slate-600">commit {plan.data.commitSha.slice(0, 8)}</span>{plan.data.isStale && <span className="rounded-full bg-amber-50 px-2 py-1 text-xs font-semibold text-amber-700">Newer index available</span>}</div></div>
      <p className="mt-3 text-sm leading-6 text-slate-600">{plan.data.summary}</p>
      <ol className="mt-4 grid gap-3">{plan.data.steps.map((step, index) => <li key={step.key} className="rounded-xl border border-slate-200 p-4"><label className="flex cursor-pointer items-start gap-3"><input className="mt-1 h-4 w-4" type="checkbox" checked={step.completed} onChange={() => toggle(step.key)}/><span className="min-w-0"><span className="font-semibold text-ink">{index + 1}. {step.title}</span><span className={`ml-2 rounded-full px-2 py-0.5 text-[10px] font-semibold ${step.evidenceLevel === 'Confirmed' ? 'bg-emerald-50 text-emerald-700' : step.evidenceLevel === 'Inferred' ? 'bg-amber-50 text-amber-700' : 'bg-slate-100 text-slate-600'}`}>{step.evidenceLevel}</span><span className="mt-1 block text-sm text-slate-600">{step.objective}</span><span className="mt-2 block text-xs leading-5 text-slate-500">{step.evidence}</span></span></label>{step.citations.length > 0 && <div className="ml-7 mt-3 flex flex-wrap gap-2">{step.citations.map((citation, i) => <a key={`${citation.path}-${citation.startLine}`} href={citation.sourceUrl} target="_blank" rel="noreferrer" className="inline-flex max-w-full items-center gap-1 break-all rounded bg-brand-50 px-2 py-1 text-xs text-brand-700 hover:underline">[{i + 1}] {citation.path}:{citation.startLine}-{citation.endLine}<ExternalLink size={11}/></a>)}</div>}</li>)}</ol>
      {plan.data.missingEvidence.length > 0 && <div className="mt-4 rounded-xl bg-amber-50 p-4"><p className="text-xs font-semibold uppercase tracking-wide text-amber-800">Questions the index could not answer</p><ul className="mt-2 list-disc pl-5 text-sm text-amber-900">{plan.data.missingEvidence.map(item => <li key={item}>{item}</li>)}</ul></div>}
    </div>}
  </div>;
}
