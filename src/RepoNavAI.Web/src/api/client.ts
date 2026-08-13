import axios from 'axios';

export const TOKEN_KEY = 'reponav.access_token';
export type ApiFailureKind = 'unauthorized' | 'unavailable' | 'other';

export function classifyApiFailure(error: unknown): ApiFailureKind {
  if (!axios.isAxiosError(error)) return 'other';
  if (error.response?.status === 401) return 'unauthorized';
  if (!error.response || (error.response.status >= 500 && error.response.status <= 599)) return 'unavailable';
  return 'other';
}

function emit(name: 'api:available' | 'api:unavailable' | 'auth:unauthorized') {
  if (typeof window !== 'undefined') window.dispatchEvent(new Event(name));
}

function record(event: 'api_available' | 'api_unavailable' | 'session_rejected', severity: 'info' | 'warn' = 'info') {
  // Fixed event names intentionally exclude request data, credentials, and access tokens.
  console[severity]({ source: 'web-client', event, occurredAt: new Date().toISOString() });
}

let apiAvailable = true;
function markAvailable() {
  if (!apiAvailable) record('api_available');
  apiAvailable = true;
  emit('api:available');
}
function markUnavailable() {
  if (apiAvailable) record('api_unavailable', 'warn');
  apiAvailable = false;
  emit('api:unavailable');
}
function rejectSession() {
  sessionStorage.removeItem(TOKEN_KEY);
  record('session_rejected', 'warn');
  emit('auth:unauthorized');
}

export const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
  timeout: 15_000,
});

export async function probeApi(): Promise<boolean> {
  try {
    await axios.get('/health', { timeout: 3_000 });
    return true;
  } catch {
    markUnavailable();
    return false;
  }
}

api.interceptors.request.use((config) => {
  const token = sessionStorage.getItem(TOKEN_KEY);
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

api.interceptors.response.use((response) => {
  markAvailable();
  return response;
}, (error: unknown) => {
  const kind = classifyApiFailure(error);
  if (kind === 'unauthorized') rejectSession();
  else if (kind === 'unavailable') markUnavailable();
  else if (axios.isAxiosError(error) && error.response) markAvailable();
  return Promise.reject(error);
});

export function getApiError(error: unknown): string {
  if (classifyApiFailure(error) === 'unavailable') return 'RepoNav AI is temporarily unavailable. We will reconnect automatically.';
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as { title?: string; errors?: Record<string,string[]> } | undefined;
    return (data?.errors && Object.values(data.errors).flat()[0]) ?? data?.title ?? 'Unable to complete the request. Please try again.';
  }
  return 'Something unexpected happened. Please try again.';
}

export async function streamApi<T>(path: string, body: unknown, signal: AbortSignal, onEvent: (event: T) => void): Promise<void> {
  const token = sessionStorage.getItem(TOKEN_KEY);
  let response: Response;
  try {
    response = await fetch(`${api.defaults.baseURL}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream', ...(token ? { Authorization: `Bearer ${token}` } : {}) },
      body: JSON.stringify(body),
      signal,
    });
  } catch (error) {
    if (!signal.aborted) markUnavailable();
    throw error;
  }
  if (response.status === 401) rejectSession();
  if (response.status >= 500) markUnavailable(); else markAvailable();
  if (!response.ok) {
    const problem = await response.json().catch(() => undefined) as { title?: string; errors?: Record<string,string[]> } | undefined;
    throw new Error((problem?.errors && Object.values(problem.errors).flat()[0]) ?? problem?.title ?? 'Unable to start repository chat.');
  }
  if (!response.body) throw new Error('The server did not provide a response stream.');

  const reader = response.body.getReader(); const decoder = new TextDecoder(); let buffer = ''; const cancelReader = () => { void reader.cancel(); };
  signal.addEventListener('abort', cancelReader, { once: true });
  try {
    while (true) {
      const { value, done } = await reader.read(); buffer += decoder.decode(value, { stream: !done });
      const frames = buffer.split(/\r?\n\r?\n/); buffer = frames.pop() ?? '';
      for (const frame of frames) {
        const data = frame.split(/\r?\n/).filter(line => line.startsWith('data:')).map(line => line.slice(5).trimStart()).join('\n');
        if (data && !signal.aborted) onEvent(JSON.parse(data) as T);
      }
      if (done || signal.aborted) break;
    }
  } finally { signal.removeEventListener('abort', cancelReader); reader.releaseLock(); }
}
