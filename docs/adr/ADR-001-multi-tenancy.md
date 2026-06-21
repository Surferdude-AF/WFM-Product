# ADR-001 — Multi-tenancy: shared database, `tenant_id` everywhere, RLS backstop

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
A tenant = a customer contact center (its Skills, queues, agents, forecasts, history). Multi-tenancy is the single hardest thing to retrofit: bolting it on later means touching every table, query, route, and auth check, and risks cross-tenant data leaks. So it must be a day-one decision.

First-hand scar: at Teleopti, multi-tenancy was retrofitted and was a major pain — and we stopped at **one app instance, one DB per tenant**, partly because there wasn't time to do shared-DB properly. This repo's v1 is the chance to do it the way we'd have wanted.

Two classic objections to shared-DB were stress-tested and answered (see Consequences): per-tenant recovery, and provable isolation under bugs.

## Decision
**One shared database. Every domain row carries `tenant_id`. Isolation is enforced in depth:**

1. **Postgres Row-Level Security (RLS)** as the enforcing backstop — policies filter every table on a per-request session variable (`app.current_tenant`). If application code ever forgets a `WHERE tenant_id`, the *database* still denies the rows. Isolation moves from "every dev must remember" to "the DB guarantees."
2. **Tenant comes from the authenticated identity, never from a request parameter.** Middleware resolves the tenant from the auth token and sets the session variable once per request.
3. **A single data-access layer** — no ad-hoc raw queries — centralizes scoping (belt to RLS's suspenders).
4. **A cross-tenant isolation test suite in CI** — create tenants A and B, attempt to read B's data as A on *every* route, assert denial. This is also the *explanation artifact*: "we run automated cross-tenant-access tests on every endpoint and they're green."

**Recovery posture (replaces naive whole-DB restore):**
- **Audit log** (append-only, before→after) is the primary tool to *undo* a bad logical write for one tenant — no restore needed.
- **Per-tenant logical backups** (`WHERE tenant_id = X` dumps) give a tenant-scoped restore artifact.
- **Break-glass:** restore a backup to a side instance, extract the one tenant's rows by `tenant_id`, re-import. Works precisely *because* everything is tenant-scoped.
- Physical/cluster corruption is the managed-Postgres provider's problem (replication + PITR), not a tenant-specific risk.

**Not** DB-per-tenant (that was the time-pressured Teleopti compromise; it raises per-customer onboarding/ops cost and fragments the data the data bank needs).

## Consequences
- **Enables [[DUP-006]]** (anonymized cross-tenant data bank) — the data is already co-located. DB-per-tenant would have made this much harder.
- **Escape hatch preserved:** because callers go through the data-access layer, a single large/sensitive tenant can later be *promoted* to its own DB without rewriting callers.
- Costs: small (~single-digit-%) RLS overhead; discipline required (session var set on every request; policies maintained as tables are added).
- Defensible to prospects/auditors pre-SOC2 via the layered controls + the passing isolation test suite.
- Ties to [[DUP-005]] (IAM) — the auth token must carry the tenant claim.
