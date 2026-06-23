# Frontend

The React/TypeScript single-page app (ADR-005), scaffolded with Vite. The first UI
slice (scaffolding-plan step 11c): trigger a forecast and view the curve.

## Run the demo locally
1. Start Postgres and apply migrations:
   ```
   docker compose up -d --build postgres migrate
   ```
2. Run the API in Development (enables the demo seed, CORS, and the Dev auth stub):
   ```
   ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:8080 \
   ConnectionStrings__Wfm='Host=localhost;Port=5432;Database=wfm;Username=wfm_app;Password=wfm_app' \
   ConnectionStrings__WfmWorker='Host=localhost;Port=5432;Database=wfm;Username=wfm_worker;Password=wfm_worker' \
   dotnet run --project src/Wfm.Api -c Release --no-launch-profile
   ```
3. Run the frontend:
   ```
   cd frontend && npm install && npm run dev
   ```
   Open http://localhost:5173 and click **Forecast now**.

The app seeds a demo tenant/skill (`POST /dev/seed`, Development only), lists skills,
triggers a forecast, polls, and renders the next week's contacts with Recharts.

## Tests
- `npm run build` — typecheck + bundle.
- `npm run test:e2e` — the single Playwright e2e (generate → view), against a running
  API + the Vite preview server (ADR-006). Not in the dotnet CI gate yet; joins a
  dedicated e2e lane at the hosting step (ADR-009).
