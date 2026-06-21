# Architecture Decision Records (ADRs)

Durable technical decisions distilled during **solution discovery** in this prototyping/discovery repo, so the future **product repo** (the modular-monolith v1) can reference them as a vetted starting point rather than re-deriving them.

One file = one decision. Format (Nygard): **Context → Decision → Consequences**. Each carries a status.

These are *technical* solution-discovery artifacts — the engineering sibling of `../evaluated-approaches.md` (SA-XXX, product/approach direction) and the `/discovery` problem statements (PS-XXX, the why).

## Status values
`proposed | accepted | superseded | deprecated`

## Index
- [ADR-001](ADR-001-multi-tenancy.md) — Multi-tenancy: shared DB, `tenant_id` everywhere, RLS backstop — **accepted**
- [ADR-002](ADR-002-persistence-postgres.md) — Persistence: Postgres relational core + JSONB edges, forward-only migrations — **accepted**
- [ADR-003](ADR-003-lifecycle-vs-soft-delete.md) — Effective-dated lifecycle + audit log instead of generic soft-delete — **accepted**
- [ADR-004](ADR-004-interval-statistics-storage.md) — Interval stats: raw per-Queue, narrow partitioned table, live Skill aggregation — **accepted**
- [ADR-005](ADR-005-app-stack.md) — App stack: **C#/.NET (ASP.NET Core) backend + React/TS frontend** (revised 2026-06-21, reversed TS→C#), polyglot kernels only when forced, stateless scaling — **accepted**
- [ADR-006](ADR-006-testing-strategy.md) — Testing: TDD on the core, double-loop acceptance, three-layer forecast-correctness — **accepted**
- [ADR-007](ADR-007-repo-strategy-modular-monolith.md) — Repo strategy: monorepo-first, modular monolith with enforced boundaries — **accepted**
- [ADR-008](ADR-008-auth-identity-authz.md) — Auth & identity: delegate authN, own permission-based authZ, multi-tenant per user — **accepted**
- [ADR-009](ADR-009-cicd-trunk-based.md) — CI/CD: trunk-based, hard CI merge gate, continuous delivery, local-first hosting — **accepted**
- [ADR-010](ADR-010-observability.md) — Observability: standard operational layer + WFM-specific forecast-accuracy monitoring — **accepted**
- [ADR-011](ADR-011-secrets-and-integration-model.md) — Secrets: two classes, and a cloud-pull-default integration model — **accepted**

## Convention
- Number sequentially: `ADR-NNN-short-slug.md`.
- A decision that replaces an earlier one sets the old one's status to `superseded` and links forward.
- Keep them short. The reasoning, not a manual.
