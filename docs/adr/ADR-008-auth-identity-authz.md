# ADR-008 — Auth & identity: delegate authN, own permission-based authZ, multi-tenant per user

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
Identity is a day-one, hard-to-retrofit, security-critical decision, and it closes a dependency [[ADR-001]] left open ("the auth token must carry the tenant claim"). WFM specifics: a single user can legitimately access **multiple tenants** (support staff investigating a bug; an independent consultant selling WFM-spec-as-a-service; BPO operators per [[PS-010]]) — and may want to work on **different tenants in different browser tabs simultaneously**. Tenants will also define their **own custom roles** on top of the personas we ship.

## Decision

### Delegate authentication, own authorization
- **AuthN (who are you) → managed B2B provider** (Clerk / WorkOS / Auth0 / self-host Ory-Keycloak). Don't own password storage, sessions, MFA, reset, or — critically for B2B — **per-tenant SSO/SAML**, which enterprise buyers will demand.
- **AuthZ (what may you do, in which tenant) → owned domain logic.** WFM-specific; the provider knows none of it.

### Multi-tenant-per-user, with request-scoped active tenant
- Membership is a many-to-many **`user × tenant → role` grant**. Role is **per grant, not global** (same identity can be Specialist in client A and B, or time-boxed support in a customer tenant).
- **The trap:** "active tenant" must be **request-scoped, never user-session/cookie-scoped** — cookies and server sessions are shared across tabs, so a single "current tenant" lets Tab A's switch corrupt Tab B's scope → cross-tenant leak.
- **Therefore:** carry tenant in the **URL (path `/t/{tenantId}/…` or subdomain)** → each tab is its own URL, multi-tab works by construction, bookmarkable. Client-side active-tenant state in **`sessionStorage` (per-tab)**, never `localStorage`/cookie.
- **Re-validate membership on every request** before setting Postgres `app.current_tenant`; RLS ([[ADR-001]]) is the backstop. A forged tenant id in the URL is denied by the membership check *and* RLS.

### Permission-based RBAC (so custom roles work)
- Custom roles force checking **permissions, not role names**: `can('timeoff.approve')`, never `role === 'TeamLead'` (which custom roles would break).
- **Permission catalog → system-defined, code-owned**, versioned with features. Tenants compose capabilities, can't invent them.
- **Default roles → seeded per tenant as editable copies** (the personas: WFM Specialist, Team Lead, Agent, Owner/Director, Admin). Improving a default later doesn't mutate existing tenants' copies (correct — don't silently change a customer's access); new tenants get the better defaults.
- **Custom roles → tenant-created** named bundles from the catalog.
- **Platform roles (support, platform-admin) → system-owned, tenant-uneditable.** A tenant can't redefine what support staff do or escalate a support grant.
- Data model under RLS: `permissions` (global, system-owned) · `roles` (tenant-scoped: defaults + customs) · `role_permissions` · `user_tenant_grants` (user × tenant × role). Enforcement at API + data-access layer.

### Traceability as a trust feature
- **Audit `(acting user, tenant context, action)`** ([[ADR-001]] audit log). For cross-tenant **support access**, use **time-boxed, prominently-logged grants** (not standing) — turns "your vendor can see my data" into "…and every access is logged and time-limited," echoing ADR-001's defend-it-to-a-prospect theme.

## Build sequencing (where the seam is today)
- **RBAC is deferred until scheduling.** Forecasting needs none: a forecaster legitimately has access to **all** of a tenant's Skills, so tenant isolation + RLS ([[ADR-001]]) is sufficient for the entire forecasting vertical. The permission catalog (`permissions`/`roles`/`Can()`) and DB-backed `user_tenant_grants` membership only earn their place once **scheduling and other modules** introduce finer-grained access. Building them earlier would be speculative.
- **Agents must be modelled before scheduling, and that area is unprototyped.** An agent wears three hats at once — an **employee**, a **schedulable resource** with a schedule, and a **user** with authN/authZ (this ADR). Unlike forecasting (which has the WFM-Take1 prototype-as-spec), there is no design for agents yet; expect a discovery/prototyping pass first. The employee/resource/user overlap will drive the data model and feeds back into the membership/grant model above.
- **Scaffolding step 8 shipped a thin seam:** URL-path tenancy + a Dev authN stub that fails closed outside Development; the URL tenant is bound to the authenticated tenant claim per request. The DB-backed grant store and the managed B2B provider replace the claim check and the stub later.

## Consequences
- Multi-tab, multi-tenant works without cross-tab leakage; tenant is cryptographically/structurally bound per request and re-validated.
- Tenant autonomy over roles without exposing capability definitions.
- **Deferred extension (flagged, not built):** **resource/data scoping** — e.g. Team Lead may `timeoff.approve` *only for their own team* (which rows, not which actions). A real WFM need beyond RBAC; design permission checks so a scope predicate can be added later. Avoid a full policy engine (ABAC/OPA) in v1 — KISS.
- Realizes [[DUP-005]]; reinforces [[ADR-001]].
