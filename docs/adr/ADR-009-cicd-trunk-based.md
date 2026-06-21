# ADR-009 — CI/CD: trunk-based, hard CI merge gate, continuous *delivery*, local-first hosting

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
CI/CD is the **outer verification loop** the agent iterates against and the gate that keeps `main` shippable. Two clarifications mattered: trunk-based vs GitFlow, and what "CD" obliges us to host (and when).

## Decision

### Trunk-based development
Short-lived branches off `main`, small PRs, merge frequently, **`main` always deployable.** Not GitFlow (long-lived develop/release branches fight continuous delivery and are solo overhead). Small reviewable units also suit agentic work (and worktrees for parallel agents later). *(Anders ran trunk-based at Teleopti from 2012 — well ahead of the curve.)*

### Hard CI merge gate (all green to merge) — assembles every prior decision
- lint + **typecheck** (TS guardrail, [[ADR-005]])
- **unit + acceptance** tests ([[ADR-006]])
- **integration against real Postgres via Testcontainers** — RLS tested, not mocked ([[ADR-001]])
- **cross-tenant isolation suite** (the security proof, [[ADR-001]])
- **module-boundary check** — fail on forbidden imports ([[ADR-007]])
- **accuracy-regression gate** on the frozen benchmark ([[ADR-006]])
- build

This gate **is the mechanized definition of done**, and the surface the agent reads failures from to self-correct. CI runs on **GitHub Actions (free) — no hosting required.**

### Continuous *Delivery*, not Continuous *Deployment*
Every green merge produces a **deployable artifact; deploying is a one-click decision Anders makes.** Prod is never auto-shipped on merge — a human gate keeps the outward-facing release under control.
- Auto-deliver `main` → **staging**; **prod behind an approval gate.**
- **Per-service, path-triggered pipelines** (monorepo independent deploys, [[ADR-007]]).
- **Preview environment per PR** (cheap on Vercel + Neon DB branching) — verify real behavior before merge; doubles as a demo link.
- **Migrations run in the pipeline**, forward-only / expand-contract ([[ADR-002]]).

### Local-first hosting; managed hosting when a URL is needed
Hosting is a **deferred, cheap, near-instant** step — not an early burden. Artifacts are **containers** (`dotnet publish` → Docker image) + a static React build; the DB is managed Postgres.
- **Phase 0 — local only:** Docker Postgres + the .NET app/workers + full CI on Actions. Complete verification loop, zero hosting, ~zero cost. "Deploy" = run via Docker Compose locally to simulate the deployed topology.
- **Phase 1 — first hosted env when a demo link is wanted:** any zero-setup managed host (Render / Fly.io, or a cloud free tier) — `main` delivers to hosted staging/demo; preview-per-PR turns on. Throwaway, not the production target.
- **Phase 2 — production: single provider, app + DB co-located (decided 2026-06-21).** App and database on **one cloud** (not mixed) — same region/VNet/VPC for low latency, a **private-networked DB** (never public), one IAM/billing/monitoring plane, no cross-provider egress. **Azure is the natural fit for .NET** (Container Apps / App Service + **Azure Database for PostgreSQL**); **AWS** is equally valid (ECS/Fargate or App Runner + **RDS/Aurora PostgreSQL**). The specific provider is deferred to publish-time. Plus the human approval gate.
- **Nothing is locked in:** the stack is plain **Postgres + containers**, so it runs on any cloud — the provider is a late, reversible choice. Neon/Render are demo-phase conveniences, not production dependencies.
- **Dev/prod parity (12-factor):** same Postgres locally as in prod — no SQLite-local/Postgres-prod split.

### IaC deferred (KISS)
Managed platforms + the cloud's own dashboards need little infra code early. Adopt Terraform / Pulumi / Bicep (Azure) when infra outgrows the dashboards — not before.

## Consequences
- Full verification loop from commit one, no hosting, no cost.
- The laptop is the deploy target until a shareable demo is wanted; going live is a config step.
- Prod releases stay a deliberate human decision (delivery, not deployment).
- The CI gate centralizes and enforces ADR-001/002/005/006/007 mechanically.
