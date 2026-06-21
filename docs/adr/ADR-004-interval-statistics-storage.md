# ADR-004 — Interval statistics storage: raw per-Queue, narrow partitioned table, live Skill aggregation

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
The 15-minute interval statistics are the raw history forecasting consumes. The data is high-volume, append-mostly, and queried as time-range scans and seasonal (weekday × interval-of-day) aggregations. Several layered questions: at what **grain** to store, where to **aggregate**, the physical **shape**, and how to represent the **timestamp**.

Domain facts that drive it: raw CCaaS data arrives **per Queue**; forecasting runs **per Skill**; the Skill↔Queue mapping is configuration that can change over time; AHT aggregation is **volume-weighted**, not a plain average.

## Decision

**Grain — store raw at the Queue grain (source of truth).**
Raw per-queue intervals are persisted faithfully (no lossy transform on ingest — matches what [[DUP-003]] data collection produces). Because the mapping can change, raw-per-queue lets history be **re-aggregated under a new mapping** — something pre-rolled per-Skill storage can never do. (The mapping itself is a candidate for effective-dating per [[ADR-003]], *only if a story needs it*.)

**Aggregation queue→skill is a hard requirement; materializing it is an optimization.**
- The rollup *must* happen (forecaster is skill-grain) and must be **volume-weighted** (volume sums; AHT is volume-weighted; keep raw metric components so the weights survive).
- Implement first as a **live SQL view** behind a **stable read interface** (e.g. `skill_interval_stats`): always-fresh, zero extra storage, correct.
- **Materialize only when read latency forces it** (large histories × many skills × backtests rescanning windows) — via a materialized view or a Timescale continuous aggregate. The forecaster reads the same interface name; the swap is invisible.
- Materialization is *not* needed for reproducibility — "what did we forecast and when" is the audit-log / forecast-versioning's job ([[ADR-001]]). The live view re-aggregating after a mapping change is the intended feature, not a bug.

**Physical shape — narrow relational table, time-partitioned.**
One row per `(tenant_id, queue_id, interval_start)` with explicit metric columns (offered, handled, abandoned, talk, ACW, … so AHT components stay weight-able). Native Postgres range-partitioning by month; index `(tenant_id, queue_id, interval_start)`.

**Timestamp — one canonical `interval_start TIMESTAMPTZ` in UTC + indexed generated bucket columns.**
- *Not* a hand-split local `date` + `interval-of-day` pair. That split's stated rationale (narrower index keys: 6 vs 8 bytes) is negligible folklore — almost certainly never worth it, and it risks a two-columns-can-disagree bug and **DST errors** (days with 92/100 local intervals, ambiguous local times).
- The *legitimate* instinct behind the split — fast seasonal grouping by weekday × interval-of-day — is served by **STORED generated columns** (local date, weekday, interval-of-day) that are real, indexable columns derived from the canonical UTC timestamp. Speed of the split, minus the folklore, plus timezone correctness.
- Store UTC, derive local. The operating timezone is a real WFM concept (ties to operating-hours / special-days in the prototype). Where the timezone lives (tenant vs skill) is a downstream modeling story.
- **Scope caveat — UTC-as-truth is correct here *only because queue stats are historical* (observed past instants are unambiguous).** It does **not** generalize to *future-dated* data. A future instant's UTC offset can change at short notice — Jordan and Russia abolishing/shifting DST gave only days' warning, repricing every future value already frozen to UTC. So **forecasts and schedules** (local wall-clock commitments) must store **local time + IANA timezone id** and resolve to UTC *late*, with tzdata kept current. Deferred to the forecast/schedule-storage ADRs. (First-hand scar: Anders.)

**Reject JSONB-per-day.** This is the highest-volume, most-queried, most schema-*stable* data — the opposite of JSONB's job ([[ADR-002]] puts JSONB on variable edges). A day-blob kills range scans, per-interval indexing, partial-interval corrections, and the in-SQL weighted aggregation above.

**Timescale = named upgrade path, not the start.** A Postgres *extension* (hypertables = transparent time-partitioning; columnar **compression** of cold intervals; **continuous aggregates** = the eventual skill-rollup materialization). Adopt when volume justifies; a native partitioned table converts to a hypertable with low friction. Starting plain preserves [[ADR-002]] engine-portability (extension is host-dependent).

## Consequences
- Mapping changes are non-destructive; history re-aggregatable.
- DST/timezone handled correctly by construction.
- Table is "Timescale-ready" without committing to the extension on day one.
- One stable read interface (`skill_interval_stats`) decouples the forecaster from the live-vs-materialized decision.
- Slightly heavier than pre-rolled storage — accepted for fidelity and re-aggregation.
