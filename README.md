# WFM-Product

Production WFM (Workforce Management) solution for small-to-medium US inbound contact centers.
**C#/.NET (ASP.NET Core) backend + React/TypeScript frontend, on Postgres** — a modular monolith.

> Working name. Renameable once a brand is settled.

## Start here
- **[CLAUDE.md](CLAUDE.md)** — engineering guide & rules (production mode).
- **[docs/adr/](docs/adr/)** — architecture decisions (ADR-001..011): tenancy, persistence, stack, testing, repo strategy, auth, CI/CD, observability, secrets/integration.
- **[docs/scaffolding-plan.md](docs/scaffolding-plan.md)** — the phased build roadmap. **Begin at Phase A.**

## Dev setup
New machine? Run the day-1 bootstrap (installs .NET SDK, Node, Docker, EF tools, pnpm):
```powershell
powershell -ExecutionPolicy Bypass -File scripts/bootstrap-dev.ps1
```

## The sibling repo
Discovery, problem statements, duplos/stories (the backlog & specs), the rationale behind every decision, and the **forecasting prototype** (the executable spec for the forecasting domain core) live in **`c:\dev\WFM-Take1`**.

## Status
**Scaffolding — Phase A not yet started.** This repo currently contains only the context the build needs (rules + ADRs + roadmap). The solution skeleton, CI, and code come next.
