import { defineConfig } from '@playwright/test';

// The single browser e2e (ADR-006): generate -> view a forecast. Runs against a
// running stack (the full `docker compose` env) plus the Vite preview server, which
// Playwright reuses if it's already serving. In CI it lives in its own job.
const isCI = !!process.env.CI;

export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  workers: 1,
  reporter: isCI ? [['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: 'http://localhost:5173',
    trace: 'on-first-retry',
  },
  webServer: {
    command: 'npm run preview',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 60_000,
  },
});
