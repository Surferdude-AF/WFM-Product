# Step 10 — Interval-stats storage + ingestion

## Context
Phase C ported the pure forecasting core (it consumes `HistoricalInterval`, returns forecasts). To forecast real tenants we now need to: store raw contact-volume/AHT stats per **Queue** (UTC), map Queues→**Skills** (a Skill = one forecast stream over 0..n Queues), expose each Skill's aggregated UTC interval stream the core can consume, and get real data in via a **CSV adapter** (reuse the prototype's path). This is ADR-004 (interval storage) under ADR-001 (RLS).

Confirmed from the prototype (`server.js`): the Skill-aggregation **is** `mergeQueues` — `SUM(contacts)`, volume-weighted AHT = `round(Σ(aht·contacts)/Σcontacts)`, default `300` when zero volume. Queue↔Skill is **many-to-many** (queue `cs` → Skills `CS` and `ClientCo Care`). Timestamps are UTC.

## Decisions
- **DB stays UTC** — no `AT TIME ZONE` in the forecasting view. Localization is the domain core's job (`LocalizedForecaster`/`SkillTimeZone`, 9b), one tested+pinned tz implementation.
- **Aggregation view = `mergeQueues` in SQL** (SUM contacts, volume-weighted AHT, 300 default).
- **View is `security_invoker = true`** (PG16) so the base tables' RLS applies to the `wfm_app` role → tenant isolation holds *through* the view.
- Queue/stats/mapping are **persistence entities in Infrastructure** (like `Tenant`), keeping the Domain pure (it only knows `HistoricalInterval`).
- The view read-model **projects to the existing `HistoricalInterval`** → feeds the core directly, no new value object.
- **Skill forecasting-config persistence** (timezone, operating hours, special days, events, Erlang inputs) is **deferred to step 11**, modelled against the worker's actual consumption rather than speculatively now. Step 10 stays "interval stats in".

## Slices (each its own red→green PR; integration-tested on real Postgres via Testcontainers)

### 10a — Queue + raw interval-stats schema & RLS
- Tables (snake_case, `tenant_id` FK→`tenants`, RLS + FORCE, granted to `wfm_app`, policy on `app.tenant_id` — mirror `EnableSkillRowLevelSecurity`):
  - `queues` (id uuid PK, tenant_id, name)
  - `queue_interval_stats` (queue_id FK→queues, tenant_id, interval_start timestamptz, contacts int, aht_seconds int; PK `(queue_id, interval_start)`; index on tenant_id)
- EF: `Queue`, `QueueIntervalStat` entities + configs (value conversions per `SkillConfiguration`); one forward-only migration.
- Test: cross-tenant isolation on `queue_interval_stats` (extend the `TenantIsolationTests` pattern) — tenant A cannot see B's stats; write for B's tenant under A's scope is rejected.

### 10b — Queue→Skill mapping + aggregation view
- `skill_queues` (skill_id FK→skills, queue_id FK→queues, tenant_id; PK `(skill_id, queue_id)`; RLS).
- SQL view `skill_interval_stats` (`WITH (security_invoker = true)`) joining stats→mapping, `GROUP BY (skill_id, interval_start)`: `SUM(contacts)`, volume-weighted AHT with 300 default — via `migrationBuilder.Sql`.
- EF keyless entity (`ToView`) → Application port `ISkillIntervalStatsReader.ForSkillAsync(SkillId) : IReadOnlyList<HistoricalInterval>`; impl in Infrastructure.
- Tests: aggregation correctness (two queues merge → weighted AHT equals `mergeQueues`); tenant isolation through the view; chronological ordering.

### 10c — CSV ingestion adapter
- Infrastructure adapter (Application port `IIntervalStatsIngestion`): parse `timestamp,contacts,aht_seconds` (reuse prototype `loadCSV` semantics; parse timestamp as **UTC**) → upsert `queue_interval_stats` for a queue (`ON CONFLICT (queue_id, interval_start) DO UPDATE` — idempotent).
- Tests: ingest frozen `historical.csv`→queue `support`, `historical-cs.csv`→queue `cs`; map Skills (TS→support, CS→cs); assert the `skill_interval_stats` stream fed to `BaselineForecaster` **reproduces the 9a golden** (persistence→core characterization); re-ingest → identical row count (idempotent).

## Key files
- Mirror: `Persistence/Configurations/SkillConfiguration.cs`, `Migrations/20260621192145_EnableSkillRowLevelSecurity.cs`, `Persistence/Tenant.cs`.
- New entities/configs/adapter under `src/Modules/Forecasting/Wfm.Forecasting.Infrastructure/Persistence/`.
- New port(s) under `src/Modules/Forecasting/Wfm.Forecasting.Application/`.
- Tests under `tests/Wfm.IntegrationTests/` (`[DockerFact]` + `PostgreSqlBuilder`).
- Migrations via `dotnet ef migrations add` with Infrastructure as the startup project.

## Verification
- Integration suite green on real Postgres (Testcontainers): RLS isolation on the new tables and through the view; aggregation == `mergeQueues`; ingestion idempotent; **persistence→core reproduces the 9a golden forecast** end-to-end.
- Compose stack still migrates (the `efbundle` picks up the new migrations).
- Full gate: build (0 warnings), `dotnet format`, boundary tests (Domain stays framework-free), all suites.
