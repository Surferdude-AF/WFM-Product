# Step 11 — Wire forecasting end-to-end (first demoable vertical)

## Context
The forecast core (step 9) and the ingestion spine (step 10) exist but nothing connects them to a user. Step 11 wires **trigger → BackgroundService worker → forecast → persist → API → React chart** — the first slice you can *see*. We build a **thin vertical first** (baseline + per-Skill timezone + outlier exclusion), then enrich (operating hours, events, competition, Erlang staffing) in follow-on slices. Determinism holds: the forecast week is derived from the **data** (the Monday after the latest stat, in the Skill's local time), never the wall clock.

## Decisions
- **Thin pipeline first:** `read stats (10b reader) → OutlierDetection.WithoutOutlierDays → LocalizedForecaster(SkillTimeZone, weekStart) → persist`. Operating hours / events / competition / Erlang staffing are later slices.
- **Worker model:** an API trigger enqueues a Postgres job row; an **in-process `BackgroundService`** claims it (`FOR UPDATE SKIP LOCKED`), runs the pipeline, persists, marks done. No new infra. Extractable to a separate worker host later (ADR-005).
- **Worker is cross-tenant** (a platform actor): it reads the queue across tenants via a dedicated `wfm_worker` role, then **sets `app.tenant_id` to the job's tenant** before the pipeline runs, so stat reads and forecast writes stay RLS-scoped (ADR-001). `forecast_jobs` policy: `wfm_app` sees its own tenant; `wfm_worker` sees all.
- **Frontend:** scaffold `frontend/` with Vite (React + TS, ADR-005), Recharts line chart, Dev-auth header; one Playwright e2e for the generate→view flow (ADR-006's named critical flow). CORS enabled in the API in Development only.

## Slices (each its own red→green PR)

### 11a — Forecast pipeline + result persistence (incl. Skill timezone)
- Schema: add `time_zone_id text NULL` to `skills` (unset → UTC via `SkillTimeZone.Of`). New `skill_forecasts` (skill_id, interval_start timestamptz, contacts, aht_seconds, generated_at timestamptz, tenant_id; PK `(skill_id, interval_start)`; RLS mirroring skills). Migration.
- `Skill.TimeZoneId` on the Domain entity; map in `SkillConfiguration`.
- Application: `IForecastService.ForecastSkillAsync(SkillId)` → read tz + stats (`ISkillIntervalStatsReader`), derive weekStart from latest stat (next local Monday), `WithoutOutlierDays` → `LocalizedForecaster.Forecast` → replace this skill's `skill_forecasts`. `IForecastReader.ForSkillAsync(SkillId)`.
- Infra: `EfForecastService`, `EfForecastReader`.
- **Test (integration):** ingest sample data → run pipeline → persisted forecast reproduces a captured **pipeline golden** (mergeQueues → exclude outliers → forecast; differs from the 10c golden, which had no exclusion). Tenant isolation on `skill_forecasts`.

### 11b — Job queue + BackgroundService worker + trigger/read API
- Schema: `forecast_jobs` (id, skill_id, tenant_id, status, requested_at, completed_at). Policy: `wfm_app` scoped to its tenant; `wfm_worker` (new login role, created in migration like `wfm_app`) sees/updates all. Migration.
- API (existing `/t/{tenantId:guid}` authorized group): `POST .../skills/{id}/forecast` → enqueue → 202; `GET .../skills/{id}/forecast` → latest forecast (or 404).
- Worker: `ForecastWorker : BackgroundService` in the API. Claim queued job (`SKIP LOCKED`) via `wfm_worker` → open a DI scope with `ITenantContext` = job's tenant → run `IForecastService` → mark done/failed. A settable `ITenantContext` (worker analogue of `RouteTenantContext`).
- **Tests:** integration — enqueue → worker → forecast persisted; queue tenant-isolated (A can't see B; worker sees both). Acceptance — POST then poll GET returns the forecast; cross-tenant denied.

### 11c — React forecast chart + e2e
- Scaffold `frontend/` (Vite React TS). `ForecastChart` (Recharts line, contacts across the forecast week): list skills (`GET /t/{t}/skills`), trigger (`POST`), poll, render `GET .../forecast`. Dev-auth header; API CORS / Vite proxy for Development.
- Dev seed: Development-only path to load the sample CSV for a demo tenant/skill.
- **Test:** one Playwright e2e — open app → Forecast now → chart renders.

## Verification
- Integration (Testcontainers): pipeline reproduces the golden; `skill_forecasts` + `forecast_jobs` tenant-isolated; worker impersonates the tenant and persists a forecast.
- Acceptance: POST→GET round-trip for the authed tenant; cross-tenant denied.
- e2e: Playwright generate→view renders the chart.
- Full gate: build (0 warnings), `dotnet format`, boundary tests, all suites; Compose `efbundle` picks up new migrations.

## Deferred to follow-on slices
Enrich the pipeline: operating hours + holidays (9d), events overlay (9e) + storage, method competition (9g), Erlang A staffing (9f) + per-Skill params — each with its config persistence. Multi-week horizon. Containerise the frontend (step 12/13).
