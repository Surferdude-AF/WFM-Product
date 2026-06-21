# ADR-002 — Persistence: Postgres relational core + JSONB edges, forward-only migrations

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
The prototype persists to flat JSON files (`data/*.json`). v1 needs a real datastore. WFM is a deeply relational, deeply *temporal* domain (tenants → skills → queues → agents → schedules → forecasts, heavily related and queried). Open questions raised: is a relational DB's migration burden worth it vs. a schemaless store? And is Postgres specifically the right engine, given that DB leadership rotates (Oracle → SQL Server → …)?

## Decision
**Postgres as the backbone. Relational core under migrations; JSONB for the genuinely variable edges.**

- **Relational core** for stable, related, queried entities — referential integrity and SQL (a core team strength) are exactly what this domain wants. Going fully schemaless doesn't *remove* schema; it relocates it into application code (schema-on-read) and forfeits integrity + querying — a bad trade for this domain.
- **JSONB columns** for the churning/variable parts: copilot event payloads, "needs review" items, integration-specific raw CCaaS blobs, flexible config. Migration discipline only where it earns its keep; schema-on-read flexibility where things change weekly. This is why "Postgres vs. document DB" is largely a false choice.
- **Forward-only migrations** with a mature tool (Prisma/Drizzle/Flyway). Symmetric up/down is obsolete for prod; use the **expand/contract** pattern (add → backfill → switch reads → drop) for zero-downtime changes. Down-migrations, if kept, are a local-dev convenience only.

### Why Postgres specifically (it's the constraints, not universal superiority)
Not obvious in a vacuum — all three majors are excellent; a .NET shop should pick SQL Server. Postgres falls out of *our* constraints:
1. **License cost/friction** — free, open-source; rules out Oracle/SQL Server licensing at our scale.
2. **Our architecture's two pillars are Postgres sweet spots** — RLS (ADR-001) and best-in-class JSONB.
3. **Extensions cover our next decisions for free** — TimescaleDB (interval time-series, ADR-TBD) and pgvector (embeddings for the copilot / data bank) live *in the same DB*.
4. **Cloud-portable, no lock-in** — managed everywhere (RDS/Aurora, Cloud SQL, Azure, Neon/Supabase serverless for the demo).
5. **Agentic-dev advantage** — most-represented DB in model training data → better completions, richer TS tooling. For a learn-the-agentic-cycle project, "the engine the agent knows best" is a real criterion.

## Consequences
- The **engine is the most reversible decision** we've made — standard SQL behind a query layer means a later swap is a refactor, not a one-way door. Low regret.
- Migration burden is real but **bounded and one-directional**; tooling makes it routine.
- Time-series treatment for 15-min interval statistics is **deferred to its own ADR** (likely a partitioned table or a Timescale hypertable, distinct from relational config).
- Replaces the prototype's `data/*.json` (gitignored runtime state) with a migrated schema.
