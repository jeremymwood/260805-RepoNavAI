import { useState, type FormEvent } from 'react';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ArrowRight } from 'lucide-react';
import { getApiError } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import { AuthLayout } from './AuthLayout';

interface ReturnLocation { pathname?: string; search?: string; hash?: string }

export function RegisterPage() {
  const { user, register } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const from = (location.state as { from?: ReturnLocation } | null)?.from;
  const destination = from?.pathname ? `${from.pathname}${from.search ?? ''}${from.hash ?? ''}` : '/';

  if (user) return <Navigate to={destination} replace />;

  async function submit(event: FormEvent) {
    event.preventDefault();
    setError('');
    setBusy(true);
    try {
      await register({ displayName, email, password });
      navigate(destination, { replace: true });
    } catch (reason) {
      setError(getApiError(reason));
    } finally {
      setBusy(false);
    }
  }

  return <AuthLayout><p className="eyebrow">Start exploring</p><h2 className="auth-title">Create your account</h2><p className="auth-copy">Set up your workspace in less than a minute.</p><form className="mt-8 space-y-4" onSubmit={submit}>{error && <div className="error" role="alert">{error}</div>}<label className="field">Full name<input autoComplete="name" required maxLength={100} value={displayName} onChange={event => setDisplayName(event.target.value)} placeholder="Ada Lovelace" /></label><label className="field">Work email<input type="email" autoComplete="email" required value={email} onChange={event => setEmail(event.target.value)} placeholder="you@company.com" /></label><label className="field">Password<input type="password" autoComplete="new-password" required minLength={12} value={password} onChange={event => setPassword(event.target.value)} placeholder="12+ characters" /><span className="text-xs font-normal text-slate-400">Use uppercase, lowercase, a number, and a symbol.</span></label><button className="primary-button" disabled={busy}>{busy ? 'Creating account…' : <>Create account <ArrowRight size={18} /></>}</button></form><p className="mt-6 text-center text-sm text-slate-500">Already have an account? <Link className="font-semibold text-brand-600" to="/login" state={location.state}>Sign in</Link></p></AuthLayout>;
}
