# Scaffolding plan — WFM-Product

The build roadmap, distilled from the ADRs. **Principle:** stand up the verification loops *first*, then the thinnest end-to-end slice that proves the architecture wires together, then port forecasting as the first real slice. Tests + CI + boundaries come before features because they are what make every subsequent (agentic) step safe (ADR-006/009).

Each step is a small, green, committable PR (trunk-based, ADR-009).

## Phase A — Repo + guardrails (verification loops, before any feature)
1. **Repo hygiene** — `.gitignore`, `.editorconfig`, README. ✔ Merge config done (squash default, merge commits off → linear history, auto-delete branches). **Branch protection (green-CI-to-merge, PR-only) is deferred:** GitHub gates it behind Pro/public for private repos, so the rule holds by discipline (CLAUDE.md) for now — CI runs on every PR but doesn't hard-block merge. Revisit on Pro upgrade / going public / first collaborator.
2. **Context in place** — `/docs/adr/` (ADR-001..011) and the production `CLAUDE.md` are already here. ✔
3. **Solution skeleton (ADR-007 module map made real)** — a .NET solution with the hexagonal split per module: e.g. `Forecasting.Domain` / `.Application` / `.Infrastructure`, an `Api` host, a minimal `SharedKernel`, and the **React/TS frontend** app folder. Empty but bounded. ✔
4. **CI gate (ADR-009)** — GitHub Actions: restore → build → format/analyzers → test. Plus **NetArchTest** (boundary rules) and an **xUnit + CsCheck + Testcontainers.NET** harness with one trivial passing test. *Done when:* a PR goes red on a forbidden cross-module reference, green otherwise. ✔ green proven on CI; red-on-violation demo still pending.

## Phase B — Walking skeleton (thinnest end-to-end slice)
5. **DB + migrations (ADR-002/004)** — EF Core + Npgsql, local **Docker Postgres** via Compose, first migration (`tenants` + one domain table). Migrations run in integration tests (Testcontainers) and the pipeline.
6. **Multi-tenancy spine first (ADR-001)** — `tenant_id` + an **RLS policy** + the session-variable middleware. Write the **cross-tenant isolation test red first**, then green. *Done when:* tenant A provably cannot read tenant B's row, in CI.
7. **One vertical, end-to-end** — a single tenant-scoped endpoint (`GET /skills`) flowing **API → Application → Domain → EF/Postgres → response**, driven by an **acceptance test written first** (double-loop, ADR-006). *Done when:* the container builds, the slice runs locally via Compose, CI is green. **The architecture is now proven.** ✔ Acceptance test (Testcontainers) green; image builds; Compose stack (postgres + one-shot migrator as owner + API as `wfm_app`) verified end-to-end — tenant A and B each see only their own skills, no tenant fails closed. Tenant is a temporary `X-Tenant-Id` dev header until the auth seam (step 8).
8. **Auth seam (ADR-008)** — wire the tenant-claim → session-var path; a **dev auth stub** first (sets a tenant); real managed provider later. ◑ Thin seam done: URL-path tenancy (`/t/{tenantId}/skills`, request-scoped), a `Dev` authN stub establishing identity, and a per-request check binding the URL tenant to the authenticated tenant claim before the session var is set — unauthenticated → 401, URL/identity mismatch → 403, match → RLS-scoped read. **Deferred:** DB-backed `user_tenant_grants` membership (replaces the claim check) and the permission-RBAC catalog (`permissions`/`roles`/`Can()`); the managed B2B provider replaces the `Dev` stub.

## Phase C — First real slice: Forecasting (prototype becomes the spec)
9. **Port the pure domain core, test-first (ADR-006)** — lift forecast math / Erlang / detection / competition into `Forecasting.Domain`. Source of truth = the prototype at **`c:\dev\WFM-Take1\prototypes\forecasting`** (`server.js`, `copilot.js`, `holidays.js`). Capture **golden datasets from the prototype's known outputs**, write them as failing tests, port until green; add the **property invariants** (ADR-006 seed list). Deterministic core (design rule). *This is where the prototype stops being a spike and becomes the executable spec.*
10. **Interval-stats storage + ingestion (ADR-004)** — raw-per-Queue table + the Skill-aggregation view; the **CSV adapter** (reuse the prototype's path) to get real data in.
11. **Wire forecasting end-to-end** — trigger → **`BackgroundService` worker** → forecast → persist → API → a **React forecast chart**. The first demoable vertical slice, behind the green gate.

## Phase D — Demo
12. **Frontend forecast view** (React + charting) — the demo surface.
13. **First hosted demo env** (ADR-009 Phase 1) only when a shareable URL is wanted. Production hosting = single provider, app + DB co-located (Azure natural for .NET, or AWS) — ADR-009 Phase 2.

---
**Later / heavier modules** (scheduling optimizer — DUP-007; intraday/real-time adherence — DUP-004): consider kicking off with a `Plan` sub-agent for recon. Not needed for the scaffolding above.

**Before scheduling: model agents first (unprototyped).** Scheduling can't be designed until **agents** exist in the model, and that area has no prototype yet (unlike forecasting). An agent is simultaneously an **employee**, a **schedulable resource** with a schedule, and a **user** with authN/authZ (ADR-008) — expect a discovery/prototyping pass before building. This is also where RBAC starts to matter (see ADR-008 "Build sequencing"); forecasting alone needs no RBAC.
