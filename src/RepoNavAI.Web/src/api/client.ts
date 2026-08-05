import axios from 'axios';
export const TOKEN_KEY = 'reponav.access_token';
export const api = axios.create({ baseURL: import.meta.env.VITE_API_URL ?? '/api', headers: { 'Content-Type': 'application/json' } });
api.interceptors.request.use((config) => { const token = sessionStorage.getItem(TOKEN_KEY); if (token) config.headers.Authorization = `Bearer ${token}`; return config; });
api.interceptors.response.use((response) => response, (error) => { if (error.response?.status === 401) { sessionStorage.removeItem(TOKEN_KEY); window.dispatchEvent(new Event('auth:unauthorized')); } return Promise.reject(error); });
export function getApiError(error: unknown): string { if (axios.isAxiosError(error)) { const data = error.response?.data as { title?: string; errors?: Record<string,string[]> } | undefined; return (data?.errors && Object.values(data.errors).flat()[0]) ?? data?.title ?? 'Unable to reach RepoNav AI. Please try again.'; } return 'Something unexpected happened. Please try again.'; }
