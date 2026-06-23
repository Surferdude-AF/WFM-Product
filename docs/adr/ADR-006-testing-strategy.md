# ADR-006 — Testing strategy: TDD on the core, double-loop acceptance, three-layer forecast-correctness

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
Production code needs a verification strategy. The agentic-dev thesis driving this whole ADR set: **tests are the verification loop that makes letting the model build *safe* and fast** — the model writes a lot quickly; a test is how it (and we) know a change is right. So "how we test" is "how we build the guardrails." A forecasting product adds a hard wrinkle: a forecast has **no single correct value**, so correctness can't be a simple equality assertion.

## Decision

### Discipline — strict TDD on the core, pragmatic at the edges
- **Pure domain core (forecast math, Erlang, anomaly detection, method competition/registry — ST-006) → test-first, always.** It's numeric (can't eyeball correctness), refactored most, and *is* the product's value. Test-first gives the agent a precise executable spec and gives us fearless refactoring. Clean to do because the core is framework-free ([[ADR-005]]).
- **API / integration → test-after acceptable, but every endpoint gets ≥1 test.** The **cross-tenant RLS isolation suite is non-negotiable** ([[ADR-001]]).
- **UI / glue → light.**

### Pyramid — and why it's shaped this way
**Many unit (strict TDD, domain core) → a solid band of acceptance tests at the API → a deliberate handful of browser e2e.** Plus the RLS isolation suite in the integration band.

- **Acceptance tests are the executable form of a story's acceptance criteria**, driven at the **API layer, not the UI.** This closes a methodology loop: CLAUDE.md's production rule "refer back to stories' acceptance criteria before marking done" becomes automatic — the AC checkboxes *become* the test; "done" = AC test green.
- **Double-loop TDD (GOOS):** outer loop = write the failing acceptance test from the story AC ("build the right thing"); inner loop = unit-TDD the domain pieces red-green-refactor until it passes ("build the thing right"). In the agentic frame: the acceptance test is the spec handed to the agent for the *story*, the unit tests for the *pieces*.
- **Browser e2e: a handful on critical flows only** (generate→view a forecast; copilot propose→approve). *Scar (Teleopti):* full UI e2e coverage was expensive to maintain and almost always flaky — hence a precious few at the top of the pyramid, never coverage.
- **e2e isolation = a fresh tenant per test, never a shared mutable seed.** *Scar (Teleopti):* a shared, mutated dataset made e2e order-dependent and flaky — one test that writes corrupts the next test's setup. Here each browser e2e seeds and drives **its own tenant** (random id); [[ADR-001]] RLS makes that a hard, database-enforced data boundary, so tests can't see or corrupt each other's state and need no shared fixture or between-test DB reset (and can run in parallel). The first smoke flow may use a fixed demo tenant; introduce per-test tenant seeding the moment a second e2e — especially a mutating one — lands. (The unit/integration/acceptance layers already mint a random tenant per test for the same reason.)

### Forecasting correctness — three layers, because there's no single right answer
Test three *different questions*, none sufficient alone:

1. **Property-based / invariant tests (the backbone).** Assert truths that hold for *any* input; a generator (fast-check) tries to break them. These encode **domain truths as executable specs** — where 22 years of WFM expertise is the differentiator. Catches whole classes of bugs.
2. **Golden / characterization tests (refactor safety).** Fixed history → recorded exact output; fail on any drift, forcing a conscious re-bless. Lets the agent refactor and ST-006 add methods without silent change. Only proves *unchanged*, not *right* — initial blessing needs a human eye. **Precondition: the forecast pipeline is deterministic** (no wall-clock, no hidden randomness, "today" injected) — a design rule, satisfied by the pure core.
3. **Accuracy-regression gate (quality ratchet).** Walk-forward backtest on a *frozen, representative* dataset; fail CI if WMAPE crosses threshold / worsens vs. the current champion. A quality bar, not a correctness check. Threshold **anchored in data, not arbitrary** (standing rule). Benchmark dataset ideally **real anonymized series from [[DUP-006]]** — the data bank does double duty as product feature *and* test corpus (and supplies PS-005 cold-start fixtures).

Why all three: golden alone locks in bugs + is brittle; property alone is sane but maybe inaccurate; accuracy alone hides edge-case breakage behind an average. Together: *"did it change?" + "is it always sane?" + "did it get worse?"* — as close to "is the forecast correct?" as the domain allows.

### Seed invariants (living list — expand with domain input)
- Forecast volume ≥ 0, always.
- A closed interval (operating hours) always forecasts 0.
- Aggregated Skill AHT stays within [min, max] of contributing queue AHTs.
- Flat history → flat forecast (parsimony / seasonal-naive case).
- Scale equivariance: ×k on all history → ×k forecast for a linear method.
- Outlier robustness: one extreme spike moves the forecast less than the spike itself (MAD detection).
- Determinism: same input twice → identical output.

## Consequences
- The agent can be handed a failing test (acceptance for a story, unit for a piece) as the task, and trust the green.
- Determinism becomes a design constraint on the forecast core (also enables golden tests).
- [[DUP-006]] data bank is now also test infrastructure.
- Tooling (provisional, not yet separately decided): Vitest + fast-check (property) + Playwright (the few e2e) + Testcontainers (real Postgres so RLS is tested, not mocked). To confirm in an implementation decision.
- The invariant list is owned by domain expertise and grows over time.
