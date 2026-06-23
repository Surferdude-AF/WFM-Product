# Project state — living pickup point

The always-current snapshot for resuming work: what's **done**, what's **next**, and **open threads** that aren't yet captured elsewhere. Keep this updated as work progresses (see CLAUDE.md "Where things live"). Durable decisions live in the ADRs and `scaffolding-plan.md`; this file is the *current* state + loose ends, so any session — human or agent — can pick up from disk rather than from chat history.

_Last updated: 2026-06-23._

## Done
- **Phase A** — repo hygiene, context (ADRs/CLAUDE.md), solution skeleton, CI gate (format + analyzers + build + tests, NetArchTest boundaries, Testcontainers).
- **Phase B (walking skeleton)** — EF Core + Postgres migrations; multi-tenancy with RLS; `GET /t/{tenantId}/skills` vertical; auth seam (Dev stub, URL-path tenancy, fail-closed outside Development).
- **Phase C** —
  - **Step 9 (forecast core, 9a–9h):** baseline forecast, per-Skill timezone (NodaTime), outlier detection, operating hours + holidays, events overlay, Erlang A, method competition, accuracy-regression gate. Pure/deterministic, golden + property tested.
  - **Step 10 (ingestion spine, 10a–c):** `queue_interval_stats` + RLS, `skill_queues` mapping + `security_invoker` aggregation view, CSV ingestion adapter. Persistence→core proven against the prototype golden.
  - **Step 11 (end-to-end vertical, 11a–c):** forecast pipeline + result persistence (+ Skill timezone), `forecast_jobs` queue + `BackgroundService` worker (cross-tenant claim, per-job tenant impersonation) + trigger/read API, React/Recharts chart.
- **Dev experience:** one-command stack (`./scripts/Start-DevTestEnv.ps1` or `docker compose up`), demo seeded from the prototype `cs` series, **e2e CI lane** (Playwright smoke, separate non-blocking job, green).

`main` is green and the demo is runnable. The **live forecast pipeline is the thin vertical** (baseline + timezone + outlier exclusion); the other core layers are ported and tested but not yet wired into the running pipeline.

## Next (roadmap — see `scaffolding-plan.md`)
- **Pipeline enrichment** — wire the already-ported core layers into the live pipeline, each a small slice with its config persistence, suggested order:
  1. Operating hours + holidays (the seeded `cs` data has clear business-hours shape to show it off)
  2. Events overlay (+ event storage)
  3. Method competition (selects the method per Skill)
  4. Erlang A staffing (+ per-Skill staffing params)
- **Step 12/13** — containerise the frontend; first hosted demo env / shareable URL (ADR-009 phases).
- **Before scheduling** — model agents first (unprototyped; employee + schedulable resource + user). See ADR-008 "Build sequencing".

## Open threads (loose ends not yet ticketed)
- **Demo data not "current":** the forecast starts the Monday after the seed's last day (2026-06-08), since the `cs` series ends 2026-06-07 — by design (data-driven `weekStart`, not the clock). Optional tweak: generate the seed relative to today so the forecast lands on the upcoming week.
- **e2e lane stability:** watch it over the next several PRs; promote toward a required check once consistently green. (Branch protection itself is still deferred to a Pro upgrade — `scaffolding-plan.md` A.1.)
- **e2e per-test isolation:** the smoke uses the fixed demo tenant; switch to minting a fresh tenant per test when the second (especially mutating) e2e lands (ADR-006).
- **CI maintenance:** `actions/checkout` & `actions/setup-*` run on deprecated Node 20 (forced to 24) — bump action versions when convenient.
