# ADR-011 — Secrets: two classes, and a cloud-pull-default integration model

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
Production secrets handling — but it surfaces a bigger decision, because *how credentials are held* is downstream of *how we integrate to the CCaaS platform.* WFM has **two different classes of secrets**, and conflating them is the mistake.

Domain input (Anders / Teleopti): mid-large customers on **on-prem** Cisco/Avaya were served by an **on-prem collector** that read the ACD locally and pushed data to the WFM SaaS — so **credentials never left the customer** (great trust story), but the on-prem server was **expensive and slow to stand up.**

## Decision

### Two classes of secrets
- **Class 1 — app/platform secrets** (DB connection string, Anthropic key, auth-provider keys, Sentry DSN). One set per environment.
  - Never in code or git: `.env` gitignored + committed `.env.example`. CI = GitHub Actions encrypted secrets. Hosting = platform env management. At scale = managed store (Doppler / cloud Secrets Manager / Vault) — KISS for v1.
  - **Secret scanning in CI** (gitleaks / GitHub secret scanning + pre-commit hook) extends the [[ADR-009]] gate.
- **Class 2 — per-tenant CCaaS integration credentials** (a tenant's API keys/OAuth tokens). Application *data*, one set per tenant.
  - **Encrypted at rest** in the DB (never plaintext): envelope encryption with a **KMS-managed key** (app-level AES-GCM key acceptable for v1, upgrade to KMS). **Tenant-scoped** under RLS ([[ADR-001]]), **never logged** (extends [[ADR-010]] no-PII rule), **rotatable**, OAuth refresh handled.

### Integration / ingestion model — cloud-pull default, edge-collector optional
The target-market shift dissolves the expensive part of the Teleopti model: small-to-medium US contact centers run overwhelmingly on **cloud CCaaS** (Five9, Genesys Cloud, Talkdesk, Amazon Connect, RingCentral, NICE CXone, 8x8) — cloud REST APIs mean a **cloud-to-cloud pull with no on-prem collector.** That is what makes **[[PS-005]] (self-serve onboarding)** and **[[PS-009]] (demo cost)** achievable; the integration model and the go-to-market thesis align.

Ingestion is a **pluggable port with multiple adapters** ([[ADR-007]] modularity, realized in [[DUP-003]]):
- **Cloud-pull adapter (default, per CCaaS platform):** SaaS holds the tenant's creds → **Class-2 encrypted-at-rest.** Self-serve. *Where the small-medium market lives.*
- **Edge-collector / push adapter (optional):** customer-side agent holds creds locally and pushes → **creds never reach the SaaS.** Preserves the Teleopti property for on-prem CCaaS or trust-sensitive enterprises — **not** the v1 default.

The trade is explicit: Teleopti optimized *creds-stay-local* at the cost of *setup expense*; this market lets us optimize *zero-setup self-serve* at the cost of *the SaaS holding encrypted creds* — a cost the cloud-CCaaS SMB buyer gladly pays for instant onboarding.

## Consequences
- Self-serve onboarding becomes possible because the on-prem collector is no longer mandatory for the target segment.
- Secret handling differs by adapter — cloud-pull = encrypted Class-2; edge-push = creds never ingested.
- Class-2 encryption + tenant scoping + no-logging is a day-one design constraint, part of onboarding ([[DUP-003]]).
- Edge-collector remains an option, not dead — captured so on-prem/trust-sensitive deals aren't architecturally excluded.
