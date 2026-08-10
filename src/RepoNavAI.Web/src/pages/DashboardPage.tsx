import { useEffect, useRef, useState, type FormEvent, type ReactNode } from 'react';
import { ArrowRight, Building2, GitBranch, LayoutDashboard, LogOut, Menu, PanelLeftClose, PanelLeftOpen, Plus, Search, Settings, UserRound, Users, X } from 'lucide-react';
import { NavLink, Navigate, Outlet, useLocation } from 'react-router-dom';
import { getApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { Brand } from '../components/Brand';
import { useOrganization } from '../organizations/OrganizationContext';
import type { OrganizationRole } from '../organizations/types';
import { RepositoryPanel } from '../repositories/RepositoryPanel';
import { ThemeControl } from '../themes/ThemeContext';

interface NavigationItem { to: string; label: string; icon: typeof LayoutDashboard; end?: boolean }
export function getNavigationItems(role: OrganizationRole): NavigationItem[] {
  const items: NavigationItem[] = [
    { to: '/', label: 'Overview', icon: LayoutDashboard, end: true },
    { to: '/organization/members', label: 'Organization members', icon: Users },
  ];
  if (role !== 'Member') items.push({ to: '/organization/settings', label: 'Organization settings', icon: Settings });
  items.push({ to: '/settings/profile', label: 'Profile settings', icon: UserRound });
  return items;
}

export function DashboardPage() {
  const { user, logout } = useAuth();
  const { organizations, current, isLoading, setCurrent, create } = useOrganization();
  const [name, setName] = useState(''); const [error, setError] = useState(''); const [busy, setBusy] = useState(false);
  async function createOrganization(event: FormEvent) { event.preventDefault(); setError(''); setBusy(true); try { await create(name); setName(''); } catch (reason) { setError(getApiError(reason)); } finally { setBusy(false); } }
  if (isLoading) return <div className="grid min-h-screen place-items-center bg-canvas"><div className="h-8 w-8 animate-spin rounded-full border-2 border-brand-600 border-t-transparent" aria-label="Loading workspace"/></div>;
  if (!current) return <div className="min-h-screen bg-canvas"><header className="border-b border-slate-200/80 bg-white"><div className="mx-auto flex h-20 max-w-7xl items-center px-6"><Brand/></div></header><main className="mx-auto max-w-7xl px-6 py-12"><section className="mx-auto max-w-xl rounded-3xl border border-slate-200 bg-white p-8 text-center shadow-panel md:p-12"><span className="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-brand-50 text-brand-600"><Building2/></span><p className="eyebrow mt-6">Create your workspace</p><h1 className="mt-3 text-3xl font-semibold tracking-tight">Start with an organization</h1><p className="mt-3 text-sm leading-6 text-slate-500">Organizations securely separate members, projects, repositories, and future AI usage.</p><form onSubmit={createOrganization} className="mt-8 space-y-4 text-left">{error && <div className="error">{error}</div>}<label className="field">Organization name<input required maxLength={150} value={name} onChange={event => setName(event.target.value)} placeholder="Acme Engineering"/></label><button className="primary-button" disabled={busy}>{busy ? 'Creating…' : <>Create organization <ArrowRight size={18}/></>}</button></form></section></main></div>;
  return <ApplicationShell organization={current} organizations={organizations} setCurrent={setCurrent} userName={user?.displayName ?? ''} userEmail={user?.email ?? ''} logout={logout}><Outlet/></ApplicationShell>;
}

function ApplicationShell({ organization, organizations, setCurrent, userName, userEmail, logout, children }: { organization: { id: string; name: string; role: OrganizationRole }; organizations: { id: string; name: string }[]; setCurrent: (id: string) => void; userName: string; userEmail: string; logout: () => void; children: ReactNode }) {
  const [menuOpen, setMenuOpen] = useState(false); const [sidebarCollapsed, setSidebarCollapsed] = useState(() => localStorage.getItem('reponav.sidebar_collapsed') === 'true'); const menuButtonRef = useRef<HTMLButtonElement>(null); const closeButtonRef = useRef<HTMLButtonElement>(null); const location = useLocation();
  useEffect(() => { setMenuOpen(false); }, [location.pathname]);
  useEffect(() => {
    if (!menuOpen) return;
    closeButtonRef.current?.focus();
    function handleKeyDown(event: KeyboardEvent) { if (event.key === 'Escape') { setMenuOpen(false); menuButtonRef.current?.focus(); } }
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [menuOpen]);
  function closeMenu() { setMenuOpen(false); menuButtonRef.current?.focus(); }
  function toggleSidebar() { setSidebarCollapsed(value => { const next = !value; localStorage.setItem('reponav.sidebar_collapsed', String(next)); return next; }); }
  const navigation = (collapsed = false) => <nav aria-label="Workspace navigation" className="space-y-1">{getNavigationItems(organization.role).map(({ to, label, icon: Icon, end }) => <NavLink key={to} to={to} end={end} title={collapsed ? label : undefined} aria-label={collapsed ? label : undefined} onClick={() => setMenuOpen(false)} className={({ isActive }) => `flex items-center rounded-xl py-2.5 text-sm font-semibold transition ${collapsed ? 'justify-center px-2' : 'gap-3 px-3'} ${isActive ? 'bg-brand-50 text-brand-700' : 'text-slate-600 hover:bg-slate-50 hover:text-ink'}`}><Icon size={18} aria-hidden="true"/>{!collapsed && <span>{label}</span>}</NavLink>)}</nav>;
  return <div className="min-h-screen bg-canvas"><header className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/95 backdrop-blur"><div className="flex h-20 items-center gap-3 px-4 lg:px-6"><button ref={menuButtonRef} className="icon-button lg:hidden" aria-label="Open navigation" aria-expanded={menuOpen} aria-controls="mobile-navigation" onClick={() => setMenuOpen(true)}><Menu size={20}/></button><Brand/><div className="ml-auto flex min-w-0 items-center gap-3"><select className="max-w-44 truncate rounded-xl border border-slate-200 bg-white px-3 py-2 text-sm font-semibold text-ink sm:max-w-64" value={organization.id} onChange={event => setCurrent(event.target.value)} aria-label="Current organization">{organizations.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}</select><div className="hidden text-right md:block"><p className="text-sm font-semibold text-ink">{userName}</p><p className="text-xs text-slate-500">{userEmail}</p></div><button className="icon-button" onClick={logout} aria-label="Sign out"><LogOut size={18}/></button></div></div></header><div className={`mx-auto grid max-w-[96rem] ${sidebarCollapsed ? 'lg:grid-cols-[5rem_minmax(0,1fr)]' : 'lg:grid-cols-[16rem_minmax(0,1fr)]'}`}><aside className={`sticky top-20 hidden h-[calc(100vh-5rem)] border-r border-slate-200 bg-white lg:block ${sidebarCollapsed ? 'p-3' : 'p-5'}`}><div className={`mb-4 flex items-center ${sidebarCollapsed ? 'justify-center' : 'justify-between gap-2'}`}>{!sidebarCollapsed && <p className="truncate px-3 text-xs font-bold uppercase tracking-wide text-slate-400" title={organization.name}>{organization.name}</p>}<button type="button" className="icon-button shrink-0" onClick={toggleSidebar} aria-label={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'} aria-expanded={!sidebarCollapsed}>{sidebarCollapsed ? <PanelLeftOpen size={18}/> : <PanelLeftClose size={18}/>}</button></div>{navigation(sidebarCollapsed)}</aside><main className="min-w-0 px-4 py-8 sm:px-6 lg:px-10 lg:py-10">{children}</main></div>{menuOpen && <div className="fixed inset-0 z-50 lg:hidden"><button className="absolute inset-0 bg-slate-950/40" aria-label="Close navigation" onClick={closeMenu}/><aside id="mobile-navigation" role="dialog" aria-modal="true" aria-label="Workspace navigation" className="relative h-full w-[min(20rem,88vw)] bg-white p-5 shadow-2xl"><div className="mb-7 flex items-center justify-between"><Brand/><button ref={closeButtonRef} className="icon-button" onClick={closeMenu} aria-label="Close navigation"><X size={20}/></button></div><p className="mb-4 truncate px-3 text-xs font-bold uppercase tracking-wide text-slate-400">{organization.name}</p>{navigation()}</aside></div>}</div>;
}

const actions = [
  { icon: Plus, title: 'Connect a repository', copy: 'Register and index source.', to: '/#register-repository' },
  { icon: Search, title: 'Ask your codebase', copy: 'Open repository analysis.', to: '/#repositories' },
  { icon: GitBranch, title: 'Explore dependencies', copy: 'Choose an indexed repository.', to: '/#repositories' },
];
export function WorkspaceOverviewPage() {
  const { current } = useOrganization(); const { user } = useAuth(); const location = useLocation();
  useEffect(() => {
    if (!location.hash) return;
    requestAnimationFrame(() => document.querySelector(location.hash)?.scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' }));
  }, [location.hash]);
  if (!current) return null;
  return <><section id="workspace-greeting" className="rounded-2xl border border-slate-200 bg-hero p-4 text-white shadow-sm sm:p-5"><h1 className="text-2xl font-semibold tracking-tight md:text-3xl">Good to see you, {(user?.displayName ?? '').split(' ')[0]}.</h1><p className="mt-1 max-w-xl text-sm text-slate-400">Turn unfamiliar codebases into navigable systems of answers, flows, and cited evidence.</p><div className="mt-4 grid gap-3 lg:grid-cols-[max-content_minmax(0,1fr)] lg:items-center"><p className="eyebrow whitespace-nowrap text-brand-400">Get started</p><div className="grid min-w-0 gap-2 md:grid-cols-3">{actions.map(({ icon: Icon, title, copy, to }) => <NavLink key={title} to={to} className="group flex min-w-0 items-center gap-3 rounded-xl border border-slate-200 p-3 transition hover:border-brand-400 hover:bg-white/5 focus:outline-none focus:ring-2 focus:ring-brand-500"><span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-brand-500/20 text-brand-400"><Icon size={18}/></span><span className="min-w-0"><span className="block text-sm font-semibold text-white">{title}</span><span className="block truncate text-xs text-slate-300">{copy}</span></span><ArrowRight size={14} className="ml-auto shrink-0 text-slate-500 transition group-hover:text-brand-400"/></NavLink>)}</div></div></section><RepositoryPanel organizationId={current.id} initialVisibleCount={4}/></>;
}
export function ProfileSettingsPage() { const { user } = useAuth(); return <><PageHeading eyebrow="Personal settings" title="Profile" copy="Review the identity associated with this browser session."/><section className="mt-8 max-w-2xl rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><dl className="grid gap-5 sm:grid-cols-2"><div><dt className="text-xs font-bold uppercase tracking-wide text-slate-400">Display name</dt><dd className="mt-1 font-semibold text-ink">{user?.displayName}</dd></div><div><dt className="text-xs font-bold uppercase tracking-wide text-slate-400">Email</dt><dd className="mt-1 break-all font-semibold text-ink">{user?.email}</dd></div></dl><div className="mt-6 border-t border-slate-200 pt-6"><ThemeControl/></div><p className="mt-6 text-sm text-slate-500">Editable account details will be added when the account-management API is available.</p></section></>; }
export function OrganizationSettingsRoute() { const { current } = useOrganization(); if (!current) return null; return current.role === 'Member' ? <Navigate to="/" replace/> : <Outlet/>; }
export function PageHeading({ eyebrow, title, copy }: { eyebrow: string; title: string; copy: string }) { return <header><p className="eyebrow">{eyebrow}</p><h1 className="mt-2 text-3xl font-semibold tracking-tight text-ink">{title}</h1><p className="mt-2 max-w-2xl text-sm leading-6 text-slate-500">{copy}</p></header>; }
