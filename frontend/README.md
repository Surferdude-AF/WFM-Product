# Frontend

The React/TypeScript single-page app (ADR-005), scaffolded with Vite. The first UI
slice (scaffolding-plan step 11c): trigger a forecast and view the curve.

## Run the demo
One command from the repo root — starts everything, opens the UI, tears it down on Enter:
```
./scripts/demo.ps1
```
Or by hand: `docker compose up --build`, then open http://localhost:5173 and click **Forecast now**.

Inner loop with hot reload: `docker compose up -d postgres migrate`, then
`dotnet run --project src/Wfm.Api` (one terminal) and `npm install && npm run dev`
here (another). `launchSettings.json` + `appsettings.Development.json` supply the
Development environment and the worker connection, so no env-var juggling.

The app seeds a demo tenant/skill (`POST /dev/seed`, Development only), lists skills,
triggers a forecast, polls, and renders the next week's contacts with Recharts.

## Tests
- `npm run build` — typecheck + bundle.
- `npm run test:e2e` — the single Playwright e2e (generate → view), against a running
  API + the Vite preview server (ADR-006). Not in the dotnet CI gate yet; joins a
  dedicated e2e lane at the hosting step (ADR-009).
