# ADR-007 — Repo strategy: monorepo-first, modular monolith with enforced boundaries

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
This discovery/prototype repo stays the lab notebook; the product is a **new, separate repo** (decided earlier). As forecasting / optimizer / data-collection eventually become separate deployable services, do we use one **monorepo** or **polyrepo** (one repo per service)? And how do we keep the codebase tidy enough that extraction stays cheap? (Anders has seen first-hand the cost of premature multi-repo / microservices — "only go there when needed.")

## Decision

### Monorepo-first; split a service out only on a real forcing function
Same "extract when forced" logic we've applied throughout, now aimed at repos.

- **"Independently deployable" ≠ "separate repo."** The misconception that drives premature polyrepo. N services can live in one monorepo, each with its **own path-triggered deploy pipeline** — deployability is a CI/CD concern, not a repo-boundary concern. This removes polyrepo's main supposed advantage.
- **Monorepo wins where we are** (solo, boundaries still moving): atomic cross-cutting changes in one PR; extraction is a *move within the repo*, not a cross-repo migration; one CI/lint/test/tooling setup; one workspace the agent sees whole.
- **Polyrepo's cost for us:** cross-cutting changes span repos and PRs (versioning coordination), duplicated tooling, fragmented context. Pain, little payoff pre-scale.
- **Tooling:** pnpm workspaces + Turborepo (or Nx) for the TS product and its TS-adjacent packages.
- **Polyglot nuance:** the strongest case for a *separate* repo is a genuinely different ecosystem — a **Rust optimizer / Python ML engine** fits its own repo + toolchain better than wedged into a JS monorepo. So: let a polyglot service spin out **on evidence when built**, not pre-emptively.
- **Contracts at language boundaries are schema-based** (OpenAPI / protobuf / JSON Schema), not shared-language types — which makes repo layout at those seams matter less anyway.
- Escape hatch both ways (subtree/filter-repo); starting mono and splitting later is lower-regret because early boundaries churn most.

### Organizing principle: modular monolith with *enforced* boundaries
"Keep areas modular with zero-to-few dependencies" (Anders) — framed:

- **High cohesion** — one module = one capability; things that change together live together.
- **Low coupling** — a module depends only on another's **published contract, never its internals.**
- **Acyclic** — the module dependency graph is a **DAG, no cycles.** Cycles rot a modular monolith into a big ball of mud (and a distributed monolith on extraction).
- **Dependencies point toward stability** — pure domain core depends on nothing; adapters/UI depend on the core. Generalizes [[ADR-005]]'s framework-free forecast core to every module (Dependency Inversion / hexagonal).
- **Enforced mechanically** — module-boundary lint (dependency-cruiser / Nx tags / ESLint import rules) that **fails CI on a forbidden import.** Unenforced boundaries rot; enforcement also stops the *agent* from quietly creating coupling.
- **Minimal shared kernel** — only universal contracts/types in `shared`; no god `common/utils` hub (coupling in disguise).

### The strategic payoff
A module with few, one-directional, contract-only dependencies **is a service waiting to happen** — the seam is pre-cut. Tidiness and cheap-future-extraction are *one* discipline.

**The duplos are the module map:** forecasting, data-collection, client-contract, scheduling, IAM, copilot, data bank — each [[DUP-001]]..[[DUP-007]] = a module = a future service candidate. The discovery-phase decomposition *is* the architecture.

## Consequences
- Independent deploys without repo fragmentation; agentic-dev context stays whole.
- Module boundaries become CI-enforced, not aspirational.
- Extraction to a service (or its own repo) is a refactor, not a rewrite.
- Polyglot services join later as packages or, if their ecosystem demands, their own repo — decided on evidence.
- Reinforces [[ADR-005]] (polyglot compute kernels) and [[ADR-001]] (stateless services).
