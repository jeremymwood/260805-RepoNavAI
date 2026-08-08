import { useState } from 'react';
import { ArrowRight, BookOpenCheck, Braces, CheckCircle2, ExternalLink, GitBranch, Github, Network, Search, ShieldCheck, Sparkles } from 'lucide-react';
import { Brand } from '../components/Brand';

const sourceRoot = 'https://github.com/jeremymwood/260805-RepoNavAI/blob/main/';
const prompts = [
  'How do indexing jobs recover after a restart?',
  'Where is organization authorization enforced?',
  'How are repository answers grounded in source?'
];

const endpoints = [
  { method: 'POST', route: '/api/auth/login', handler: 'AuthController.Login', source: 'src/RepoNavAI.Api/Controllers/AuthController.cs', access: 'Anonymous' },
  { method: 'POST', route: '/api/organizations/{organizationId}/repositories', handler: 'RepositoriesController.Register', source: 'src/RepoNavAI.Api/Controllers/RepositoriesController.cs', access: 'Authorized' },
  { method: 'POST', route: '/api/organizations/{organizationId}/repositories/{repositoryId}/chat', handler: 'RepositoriesController.Chat', source: 'src/RepoNavAI.Api/Controllers/RepositoriesController.cs', access: 'Authorized' }
];

export function DemoPage() {
  const [question, setQuestion] = useState(prompts[0]!);
  const [submitted, setSubmitted] = useState(prompts[0]!);

  return <div className="min-h-screen bg-canvas text-ink">
    <header className="sticky top-0 z-20 border-b border-slate-200/80 bg-white/95 backdrop-blur">
      <div className="mx-auto flex h-20 max-w-7xl items-center justify-between px-5 sm:px-8"><Brand/><div className="flex items-center gap-3"><span className="rounded-full bg-amber-100 px-3 py-1 text-xs font-bold text-amber-800">Read-only demo</span><a className="hidden items-center gap-2 text-sm font-semibold text-slate-600 hover:text-brand-600 sm:flex" href="https://github.com/jeremymwood/260805-RepoNavAI" target="_blank" rel="noreferrer"><Github size={17}/> View source</a></div></div>
    </header>

    <main>
      <section className="overflow-hidden bg-ink text-white"><div className="grid-bg"><div className="mx-auto grid max-w-7xl gap-10 px-5 py-16 sm:px-8 md:py-24 lg:grid-cols-[1.15fr_.85fr] lg:items-center"><div><div className="inline-flex items-center gap-2 rounded-full border border-blue-300/20 bg-blue-300/10 px-3 py-1 text-xs font-semibold text-blue-200"><Sparkles size={14}/> Product preview · fixture data</div><h1 className="mt-6 max-w-3xl text-4xl font-semibold tracking-tight sm:text-5xl">Understand an unfamiliar codebase without tracing every file by hand.</h1><p className="mt-5 max-w-2xl text-base leading-7 text-slate-300 sm:text-lg">RepoNav AI turns indexed repositories into navigable endpoints, semantic evidence, architecture paths, and source-grounded answers.</p><div className="mt-8 flex flex-wrap gap-3"><a className="inline-flex items-center gap-2 rounded-xl bg-white px-5 py-3 text-sm font-semibold text-ink hover:bg-blue-50" href="#workspace">Explore the demo <ArrowRight size={17}/></a><a className="inline-flex items-center gap-2 rounded-xl border border-slate-600 px-5 py-3 text-sm font-semibold text-white hover:border-slate-400" href="https://github.com/jeremymwood/260805-RepoNavAI" target="_blank" rel="noreferrer">GitHub repository <ExternalLink size={16}/></a></div></div><div className="rounded-3xl border border-white/10 bg-white/5 p-5 shadow-2xl backdrop-blur"><div className="flex items-center justify-between border-b border-white/10 pb-4"><div className="flex items-center gap-3"><span className="grid h-10 w-10 place-items-center rounded-xl bg-brand-500/20 text-blue-200"><Network size={20}/></span><div><p className="font-semibold">Repository map</p><p className="text-xs text-slate-400">jeremymwood/260805-RepoNavAI</p></div></div><span className="text-xs font-semibold text-emerald-300">Indexed</span></div><div className="mt-6 grid grid-cols-3 gap-3 text-center text-xs"><Metric value="5" label="Projects"/><Metric value="18" label="API routes"/><Metric value="202" label="Source chunks"/></div><div className="mt-5 space-y-3"><Flow label="HTTP request" detail="Controller → MediatR command"/><Flow label="Tenant boundary" detail="Organization membership policy"/><Flow label="Repository insight" detail="Retrieval → cited answer"/></div></div></div></div></section>

      <section id="workspace" className="mx-auto max-w-7xl scroll-mt-24 px-5 py-14 sm:px-8"><div className="mb-8 flex flex-col justify-between gap-4 md:flex-row md:items-end"><div><p className="eyebrow">Demo workspace</p><h2 className="mt-2 text-3xl font-semibold tracking-tight">Acme Engineering / RepoNav AI</h2><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">A static walkthrough of the current product surface. Controls below use bundled fixtures and never contact an API or AI provider.</p></div><div className="flex items-center gap-2 text-sm font-semibold text-emerald-700"><CheckCircle2 size={18}/> Commit indexed successfully</div></div>

        <div className="grid gap-5 md:grid-cols-3"><Capability icon={GitBranch} title="Trace request paths" copy="Find entry points and follow known downstream calls."/><Capability icon={Search} title="Search by meaning" copy="Retrieve relevant code with commit-pinned evidence."/><Capability icon={ShieldCheck} title="Respect tenant boundaries" copy="Authorize every repository query through organization membership."/></div>

        <section className="mt-8 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm sm:p-7"><div className="flex flex-col gap-4 border-b border-slate-200 pb-6 sm:flex-row sm:items-center sm:justify-between"><div className="flex items-center gap-3"><span className="grid h-11 w-11 place-items-center rounded-xl bg-slate-100 text-slate-700"><Github size={21}/></span><div><h3 className="font-semibold">jeremymwood/260805-RepoNavAI</h3><p className="text-sm text-slate-500">Public · main · indexed at 41f0efb</p></div></div><span className="w-fit rounded-full bg-emerald-50 px-3 py-1 text-xs font-bold text-emerald-700">Completed</span></div>

          <div className="mt-7"><div className="flex items-center gap-2"><Braces className="text-brand-600" size={19}/><h3 className="font-semibold">API endpoint catalog</h3></div><div className="mt-4 overflow-x-auto"><table className="w-full min-w-[720px] text-left text-sm"><thead className="text-xs uppercase text-slate-400"><tr><th className="pb-3">Method</th><th className="pb-3">Route</th><th className="pb-3">Handler</th><th className="pb-3">Access</th></tr></thead><tbody>{endpoints.map(endpoint=><tr key={endpoint.route} className="border-t border-slate-100"><td className="py-4 font-bold text-brand-700">{endpoint.method}</td><td className="py-4 font-mono text-xs">{endpoint.route}</td><td className="py-4"><a className="font-semibold text-brand-600 hover:underline" href={`${sourceRoot}${endpoint.source}`} target="_blank" rel="noreferrer">{endpoint.handler}</a><p className="mt-1 text-xs text-slate-400">{endpoint.source}</p></td><td className="py-4">{endpoint.access}</td></tr>)}</tbody></table></div></div>

          <div className="mt-8 border-t border-slate-200 pt-7"><div className="flex items-center gap-2"><BookOpenCheck className="text-brand-600" size={19}/><h3 className="font-semibold">Ask this repository</h3></div><p className="mt-1 text-sm text-slate-500">Try a curated question to preview a source-grounded explanation.</p><div className="mt-4 flex flex-wrap gap-2">{prompts.map(prompt=><button key={prompt} type="button" onClick={()=>setQuestion(prompt)} className="rounded-full border border-slate-200 px-3 py-2 text-left text-xs font-semibold text-slate-600 hover:border-brand-200 hover:bg-brand-50 hover:text-brand-700">{prompt}</button>)}</div><form className="mt-4 flex flex-col gap-3 sm:flex-row" onSubmit={event=>{event.preventDefault();setSubmitted(question);}}><input className="h-12 min-w-0 flex-1 rounded-xl border border-slate-200 px-4 text-sm outline-none focus:border-brand-500 focus:ring-4 focus:ring-brand-100" value={question} onChange={event=>setQuestion(event.target.value)} aria-label="Repository question"/><button className="rounded-xl bg-brand-600 px-6 text-sm font-semibold text-white hover:bg-brand-700" type="submit">Explain</button></form><DemoAnswer question={submitted}/></div>
        </section>
      </section>
    </main>

    <footer className="border-t border-slate-200 bg-white"><div className="mx-auto flex max-w-7xl flex-col gap-3 px-5 py-8 text-sm text-slate-500 sm:px-8 md:flex-row md:items-center md:justify-between"><p>Public preview only. No backend, account, repository ingestion, or AI calls run on GitHub Pages.</p><a className="font-semibold text-brand-600 hover:underline" href="https://github.com/jeremymwood/260805-RepoNavAI#readme" target="_blank" rel="noreferrer">Run the functional MVP locally</a></div></footer>
  </div>;
}

function Metric({value,label}:{value:string;label:string}) { return <div className="rounded-xl bg-white/5 p-3"><p className="text-xl font-semibold text-white">{value}</p><p className="mt-1 text-slate-400">{label}</p></div>; }
function Flow({label,detail}:{label:string;detail:string}) { return <div className="flex items-center gap-3 rounded-xl border border-white/10 p-3"><span className="h-2.5 w-2.5 rounded-full bg-blue-400"/><div><p className="text-sm font-semibold">{label}</p><p className="text-xs text-slate-400">{detail}</p></div></div>; }
function Capability({icon:Icon,title,copy}:{icon:typeof Search;title:string;copy:string}) { return <article className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><span className="grid h-11 w-11 place-items-center rounded-xl bg-brand-50 text-brand-600"><Icon size={21}/></span><h3 className="mt-5 font-semibold">{title}</h3><p className="mt-2 text-sm leading-6 text-slate-500">{copy}</p></article>; }

function DemoAnswer({question}:{question:string}) {
  const authorization = question.toLowerCase().includes('authorization');
  const grounding = question.toLowerCase().includes('grounded') || question.toLowerCase().includes('source');
  const text = authorization
    ? 'Organization membership is evaluated before repository operations reach the query or command handler. Resource lookups remain organization-scoped so identifiers cannot be used to cross tenant boundaries.'
    : grounding
      ? 'The application retrieves chunks only from the latest completed repository snapshot, numbers citations before generation, and renders provider output as plain text. Citation URLs are created from trusted indexed metadata.'
      : 'A worker claims a durable PostgreSQL job with a unique owner token and renews its lease while processing. After interruption, the expired lease becomes claimable; concurrency checks prevent the stale worker from committing over the new owner.';
  const source = authorization ? 'docs/architecture/ADR-002-organization-tenancy.md' : grounding ? 'docs/architecture/ADR-007-streaming-repository-chat.md' : 'docs/architecture/ADR-004-durable-repository-indexing.md';
  return <div className="mt-5 rounded-xl border border-blue-100 bg-blue-50/60 p-5" aria-live="polite"><div className="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-brand-700"><Sparkles size={15}/> Fixture explanation</div><p className="mt-3 text-sm leading-7 text-slate-700">{text} <a className="font-semibold text-brand-600 hover:underline" href={`${sourceRoot}${source}`} target="_blank" rel="noreferrer">[1]</a></p><div className="mt-4 border-t border-blue-100 pt-3"><p className="text-xs font-bold uppercase tracking-wide text-slate-400">Source</p><a className="mt-1 inline-flex max-w-full items-center gap-1 break-all text-sm font-semibold text-brand-600 hover:underline" href={`${sourceRoot}${source}`} target="_blank" rel="noreferrer">[1] {source} <ExternalLink className="shrink-0" size={13}/></a></div></div>;
}
