import { defineConfig } from '@playwright/test';
import base from './playwright.config';

// Exercise the real authenticated app with intercepted API responses, not the
// static Pages demo. A separate port prevents reusing the demo dev server.
export default defineConfig({
  ...base,
  testMatch: '**/workspace-status.e2e.ts',
  workers: 4,
  use: { ...base.use, baseURL: 'http://127.0.0.1:4174' },
  webServer: {
    command: 'npm run dev -- --host 127.0.0.1 --port 4174 --strictPort',
    env: { VITE_PUBLIC_DEMO: 'false', VITE_API_URL: '/api', VITE_BASE_PATH: '/' },
    url: 'http://127.0.0.1:4174',
    reuseExistingServer: false,
  },
});
