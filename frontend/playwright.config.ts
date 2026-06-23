import { defineConfig } from '@playwright/test';

// The single browser e2e (ADR-006): generate -> view a forecast. Runs against a
// running API (e.g. `docker compose up`) plus the Vite preview server, which
// Playwright starts. Not wired into the dotnet CI gate yet (it joins a dedicated
// e2e lane at the hosting step, ADR-009).
export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  use: { baseURL: 'http://localhost:5173' },
  webServer: {
    command: 'npm run preview',
    url: 'http://localhost:5173',
    reuseExistingServer: true,
    timeout: 60_000,
  },
});
