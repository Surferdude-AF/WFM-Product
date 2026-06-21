# WFM Product — Production Engineering Guide

## What this repo is
The production WFM product: a **modular monolith** — **C#/.NET (ASP.NET Core) backend + React/TypeScript frontend, on Postgres** — serving multiple tenants. Discovery, problem framing, and the throwaway prototype live in the separate **WFM-Take1** repo (`c:\dev\WFM-Take1`) — that is the "why" archive (problem statements, duplos/stories as specs, ADR origins, and the forecasting prototype-as-spec). **This** repo is the build, in quality mode.

> **First time here?** Read `docs/adr/` (the architecture decisions) and `docs/scaffolding-plan.md` (the build roadmap). Then start at Phase A.

## Domain terminology (durable — carries over verbatim)
- **Queue** — routing destination in the CCaaS platform (normalised term).
- **Skill** — the WFM unit for forecasting/scheduling; maps 0–n Queues; one Skill = one forecast stream. (CCaaS "skill" = agent routing attribute — different concept, same word.)
- Personas & problem statements: see `c:\dev\WFM-Take1\discovery`.

## Architecture rules (full decisions in `/docs/adr`)
- **Modular monolith, enforced boundaries** — high cohesion, low coupling, **acyclic** dependency graph, depend only on a module's **published contract**. Forbidden references **fail CI**. No god `common/utils`. (ADR-007)
- **Hexagonal** — the domain core (forecast math, Erlang, detection, competition) is **pure: framework-free, I/O-free, deterministic.** Adapters/UI depend on the core, never the reverse. (ADR-005)
- **Multi-tenant** — `tenant_id` on every domain row; **RLS** enforces scoping; tenant comes from the auth token / request URL, **never trusted from the client**; membership re-validated per request. (ADR-001, ADR-008)
- **Persistence** — Postgres relational core + **JSONB only on variable edges**; **forward-only EF Core migrations**, expand/contract. (ADR-002)
- **Lifecycle** — **effective-dated states only where a story needs it** (not blanket); audit log for recovery; no generic soft-delete. (ADR-003)
- **Compute kernels** (scheduling optimizer, ML forecasting) — extracted as **polyglot services on profiling evidence**, behind schema contracts. C# covers most compute in-process. (ADR-005)

## How to build here — PRODUCTION MODE (note: inverts the WFM-Take1 discovery repo)
- **Quality mode, not spike mode.** Correctness, tests, error handling, security, performance — not "working over perfect."
- **Test-first on the domain core.** No domain-core code without a failing test first.
- **Double-loop TDD:** write the failing **acceptance test from the story's acceptance criteria** (at the **API**, not the UI) → then unit-TDD the pieces red-green-refactor until green. (ADR-006)
- **Security by default:** every query tenant-scoped; check **permissions** (`Can("timeoff.approve")`), **never role names**; validate inputs at boundaries (FluentValidation); secrets never in code or committed.
- **No comments except non-obvious *why*.** Match the surrounding code's style and idiom.
- **Small PRs, trunk-based**, `main` always deployable.

## Definition of done
- **The CI gate is green** — format + analyzers, build, unit + acceptance, integration on **real Postgres (Testcontainers.NET)**, **cross-tenant isolation suite**, **module-boundary check (NetArchTest)**, **accuracy-regression gate**. (ADR-009)
- Every story's acceptance criteria have green acceptance tests.

## Testing strategy (ADR-006)
- Pyramid: **many unit (TDD, core) → a band of acceptance tests (API = executable story AC) → a deliberate handful of browser e2e** (e2e is flaky/expensive — keep it tiny).
- **Forecasting correctness = three layers:** property-based domain **invariants** (backbone, CsCheck/FsCheck) + **golden** tests (refactor safety; requires a deterministic core) + **accuracy-regression gate** (frozen real-anonymized dataset; threshold **anchored in data**, never arbitrary).

## Git & workflow
- Trunk-based; short-lived branches; commit at checkpoints.
- **Commit only when confirmed; never push without explicit go-ahead** (outward-facing). End commit messages with the `Co-Authored-By` line.
- **Continuous *delivery*** — every green merge is deployable; **prod ships behind a human approval gate**, not automatically. (ADR-009)
- Migrations run in the pipeline (forward-only / expand-contract).

## Where things live
- `/docs/adr` — architecture decisions (ADR-001..011), owned here.
- `/docs/scaffolding-plan.md` — the phased build roadmap.
- **WFM-Take1 repo** (`c:\dev\WFM-Take1`) — problem statements, duplos/stories (the backlog & specs), the rationale behind every decision, and the **forecasting prototype** (`prototypes/forecasting`) that is the executable spec for the forecasting domain core. Consult it before building a capability; refer to a story's acceptance criteria before marking work done.
