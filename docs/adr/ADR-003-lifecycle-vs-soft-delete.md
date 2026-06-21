# ADR-003 — Effective-dated lifecycle + audit log, not generic soft-delete

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
A generic `deleted_at` "soft delete" flag on everything is tempting for recoverability, but it **conflates two different concerns** and then forces everyone to guess which was meant.

First-hand scar: at Teleopti, after years of soft-deletes the team concluded a soft-deleted "Skill CS-B" really meant a *domain* statement — "keep its forecast for active schedules so impact still shows, but generate no new forecasts" — i.e. the real operation should have been **"Deactivate CS-B from date X."** The generic delete flag hid a domain lifecycle state and made meaning ambiguous.

## Decision
**Separate the two concerns soft-delete conflates:**

1. **Domain lifecycle is a first-class, effective-dated concept** — not a delete flag. The real operation is e.g. "deactivate Skill CS-B from 2026-09-01": valid before that date, generates nothing after. This is *valid-time* effective-dating, and WFM is intensely temporal (skill validity, forecast versions, schedule periods all have date ranges). The meaning lives **in the data**, not in tribal memory.
2. **Recoverability/forensics is the audit log's job** — a separate append-only before→after record. That, not a delete flag, is what undoes a bad write (see [[ADR-001]] recovery posture).
3. **True row deletion** is reserved for genuine "this was a mistake, erase it" cases (and future GDPR-style erasure).

### Scope nuance (Anders, 2026-06-20)
**Effective-dating is added only where a story genuinely needs it — as a deliberate, scoped story — not as a blanket pattern on every entity.** It carries real complexity; pay for it per entity when the domain demands it, not reflexively.

### Guardrail
Full **bitemporal** modeling (valid-time × transaction-time on everything) is the over-engineered version — avoid it. Effective-dating on the entities that need it + an audit log gives ~90% of the value without the bitemporal headache.

## Consequences
- Domain operations read clearly ("deactivate from X") instead of ambiguous deletes.
- Audit log (from ADR-001) does double duty: recovery *and* the transaction-time history.
- Slightly more deliberate modeling per entity — accepted, and intentionally not blanket.
- Each effective-dated entity will surface as its own story when scoped.
