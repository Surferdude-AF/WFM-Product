# WFM-Product

Production WFM (Workforce Management) solution for small-to-medium US inbound contact centers.
**C#/.NET (ASP.NET Core) backend + React/TypeScript frontend, on Postgres** — a modular monolith.

> Working name. Renameable once a brand is settled.

## Start here
- **[CLAUDE.md](CLAUDE.md)** — engineering guide & rules (production mode).
- **[docs/adr/](docs/adr/)** — architecture decisions (ADR-001..011): tenancy, persistence, stack, testing, repo strategy, auth, CI/CD, observability, secrets/integration.
- **[docs/scaffolding-plan.md](docs/scaffolding-plan.md)** — the phased build roadmap.
- **[docs/STATE.md](docs/STATE.md)** — living pickup point: what's done, what's next, open threads. **Start here to resume work.**

## Dev setup
New machine? Run the day-1 bootstrap (installs .NET SDK, Node, Docker, EF tools, pnpm):
```powershell
powershell -ExecutionPolicy Bypass -File scripts/bootstrap-dev.ps1
```

## Run the demo
One command — starts a clean stack, opens the UI, and tears it all down when you press Enter:
```powershell
./scripts/Start-DevTestEnv.ps1
```
Or do it by hand: `docker compose up --build`, then open **http://localhost:5173** and click **Forecast now**. (`docker compose` auto-merges `docker-compose.override.yml`, which runs the API in Development with a demo seed + CORS; for a production-like API only, use `docker compose -f docker-compose.yml up`.)

Inner loop with hot reload instead: `docker compose up -d postgres migrate`, then `dotnet run --project src/Wfm.Api` in one terminal and `cd frontend && npm run dev` in another.

## The sibling repo
Discovery, problem statements, duplos/stories (the backlog & specs), the rationale behind every decision, and the **forecasting prototype** (the executable spec for the forecasting domain core) live in **`c:\dev\WFM-Take1`**.

## Status
**Phases A–C complete; the first end-to-end forecast vertical (scaffolding-plan step 11) is demoable** behind the green CI gate — ingest → aggregate → forecast (timezone-aware, outlier-excluded) → background worker → API → React chart. Next: enrich the pipeline (operating hours, events, method competition, Erlang staffing) and the first hosted demo env.
