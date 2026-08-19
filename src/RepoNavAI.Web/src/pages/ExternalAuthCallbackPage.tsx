import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';
import { AuthLayout } from './AuthLayout';

export function parseExternalAuthCallback(hash: string) {
  const parameters = new URLSearchParams(hash.replace(/^#/, ''));
  const requestedReturnUrl = parameters.get('return_url');
  return {
    code: parameters.get('code'),
    error: parameters.get('error'),
    returnUrl: requestedReturnUrl?.startsWith('/') && !requestedReturnUrl.startsWith('//') && !requestedReturnUrl.includes('\\') ? requestedReturnUrl : '/',
  };
}

export function ExternalAuthCallbackPage() {
  const { acceptExternalCode } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState('');
  useEffect(() => {
    const { code, error: providerError, returnUrl } = parseExternalAuthCallback(window.location.hash);
    window.history.replaceState(null, '', window.location.pathname);
    if (!code) { setError(providerError ?? 'External sign-in could not be completed.'); return; }
    void acceptExternalCode(code).then(() => navigate(returnUrl, { replace: true })).catch(() => setError('RepoNavAI could not establish your session. Please try again.'));
  }, [acceptExternalCode, navigate]);
  return <AuthLayout><p className="eyebrow">Secure sign-in</p><h2 className="auth-title">{error ? 'Unable to sign in' : 'Finishing your sign-in'}</h2><p className="auth-copy" role={error ? 'alert' : undefined}>{error || 'Verifying your RepoNavAI session…'}</p>{error && <button className="primary-button mt-8" onClick={() => navigate('/login', { replace: true })}>Return to sign in</button>}</AuthLayout>;
}
