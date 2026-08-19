import { useEffect, useState } from 'react';
import { api } from '../api/client';
import type { ExternalProvider } from './types';

export function ExternalProviderIcon({ provider }: { provider: string }) {
  if (provider === 'Google') return <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true"><path fill="#4285F4" d="M21.6 12.2c0-.7-.1-1.5-.2-2.2H12v4.2h5.4a4.6 4.6 0 0 1-2 3v2.7h3.3c1.9-1.8 2.9-4.4 2.9-7.7Z"/><path fill="#34A853" d="M12 22c2.7 0 5-.9 6.7-2.4l-3.3-2.7c-.9.6-2.1 1-3.4 1-2.6 0-4.8-1.8-5.6-4.1H3v2.8A10 10 0 0 0 12 22Z"/><path fill="#FBBC05" d="M6.4 13.8A6 6 0 0 1 6.1 12c0-.6.1-1.2.3-1.8V7.4H3A10 10 0 0 0 2 12c0 1.7.4 3.2 1 4.6l3.4-2.8Z"/><path fill="#EA4335" d="M12 6.1c1.5 0 2.8.5 3.9 1.5l2.9-2.9A9.8 9.8 0 0 0 12 2a10 10 0 0 0-9 5.4l3.4 2.8C7.2 7.9 9.4 6.1 12 6.1Z"/></svg>;
  if (provider === 'Apple') return <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor" aria-hidden="true"><path d="M18.7 12.9c0-3 2.5-4.4 2.6-4.5a5.5 5.5 0 0 0-4.3-2.3c-1.8-.2-3.6 1.1-4.5 1.1-.9 0-2.3-1.1-3.8-1-1.9 0-3.7 1.1-4.7 2.8-2 3.5-.5 8.6 1.4 11.4 1 1.4 2.1 2.9 3.6 2.8 1.4-.1 2-1 3.8-1s2.3 1 3.8 1c1.6 0 2.6-1.4 3.5-2.8a12.5 12.5 0 0 0 1.6-3.3 5 5 0 0 1-3-4.2ZM15.8 4.2A5.1 5.1 0 0 0 17 0a5.2 5.2 0 0 0-3.5 2.1 4.8 4.8 0 0 0-1.2 4c1.3.1 2.7-.7 3.5-1.9Z"/></svg>;
  return <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true"><path fill="#F25022" d="M2 2h9.5v9.5H2z"/><path fill="#7FBA00" d="M12.5 2H22v9.5h-9.5z"/><path fill="#00A4EF" d="M2 12.5h9.5V22H2z"/><path fill="#FFB900" d="M12.5 12.5H22V22h-9.5z"/></svg>;
}

export function ExternalLoginButtons({ returnUrl }: { returnUrl: string }) {
  const [providers, setProviders] = useState<ExternalProvider[]>([]);
  useEffect(() => { void api.get<ExternalProvider[]>('/auth/external/providers').then(response => setProviders(response.data)).catch(() => setProviders([])); }, []);
  if (!providers.length) return null;
  const start = (provider: ExternalProvider) => {
    const base = String(api.defaults.baseURL ?? '/api').replace(/\/$/, '');
    window.location.assign(`${base}/auth/external/${encodeURIComponent(provider.id)}/challenge?returnUrl=${encodeURIComponent(returnUrl)}`);
  };
  return <div className="mt-7">
    <div className="flex items-center gap-3 text-xs font-semibold uppercase tracking-widest text-slate-400"><span className="h-px flex-1 bg-slate-200"/><span>or continue with</span><span className="h-px flex-1 bg-slate-200"/></div>
    <div className="mt-4 grid gap-3">{providers.map(provider => <button className="button-secondary w-full gap-3" key={provider.id} type="button" disabled={!provider.enabled} title={provider.enabled ? undefined : `${provider.displayName} sign-in requires provider configuration`} onClick={() => start(provider)}><ExternalProviderIcon provider={provider.id}/><span>{provider.enabled ? `Continue with ${provider.displayName}` : `${provider.displayName} setup required`}</span></button>)}</div>
  </div>;
}
