import axios from 'axios';
export const TOKEN_KEY = 'reponav.access_token';
export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api', headers: { 'Content-Type': 'application/json' } });
api.interceptors.request.use((config) => { const token = sessionStorage.getItem(TOKEN_KEY); if (token) config.headers.Authorization = `Bearer ${token}`; return config; });
api.interceptors.response.use((response) => response, (error) => { if (error.response?.status === 401) { sessionStorage.removeItem(TOKEN_KEY); window.dispatchEvent(new Event('auth:unauthorized')); } return Promise.reject(error); });
export function getApiError(error: unknown): string { if (axios.isAxiosError(error)) { const data = error.response?.data as { title?: string; errors?: Record<string,string[]> } | undefined; return (data?.errors && Object.values(data.errors).flat()[0]) ?? data?.title ?? 'Unable to reach RepoNav AI. Please try again.'; } return 'Something unexpected happened. Please try again.'; }

export async function streamApi<T>(path: string, body: unknown, signal: AbortSignal, onEvent: (event: T) => void): Promise<void> {
  const token = sessionStorage.getItem(TOKEN_KEY);
  const response = await fetch(`${api.defaults.baseURL}${path}`, { method: 'POST', headers: { 'Content-Type': 'application/json', Accept: 'text/event-stream', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify(body), signal });
  if (response.status === 401) { sessionStorage.removeItem(TOKEN_KEY); window.dispatchEvent(new Event('auth:unauthorized')); }
  if (!response.ok) { const problem = await response.json().catch(() => undefined) as { title?: string; errors?: Record<string,string[]> } | undefined; throw new Error((problem?.errors && Object.values(problem.errors).flat()[0]) ?? problem?.title ?? 'Unable to start repository chat.'); }
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
