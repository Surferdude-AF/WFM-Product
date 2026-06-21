# ADR-005 — App stack: C#/.NET (ASP.NET Core) product + React/TS frontend, polyglot kernels only when forced, stateless scaling

**Status:** accepted — **revised 2026-06-21 (reversed TypeScript→C#; see Reversal note)**
**Date:** 2026-06-20, revised 2026-06-21
**Decision owner:** Anders

## Context
v1 is a demoable forecasting product that must also be a credible foundation for a SaaS serving hundreds of tenants / thousands of users — and increasingly a portfolio artifact for a **crescendo.ai WFM builder-PM** conversation and/or a **product-led SMB SaaS**. Choosing the language/stack.

The decision was first made (2026-06-20) as **TypeScript/Next.js**, on one-language-full-stack + AI-assist + time-to-demo grounds. It was **reversed to C#/.NET on 2026-06-21** after weighing: it "feels like building a proper system"; Anders's deep **.NET background** (Teleopti was a .NET shop — so it's his highest-velocity stack, not a relearn); **enterprise credibility** for the crescendo/SaaS paths; **runtime type safety** (C# types are *not* erased — structurally answers the "crashes in prod" worry that TS only answers via boundary validation); and **future-proofing**. The estimated cost — ~10–20% slower to v1, almost entirely the two-language frontend tax — was judged worth it: on a solo project, developer-fit/motivation is the #1 predictor of finishing, and the choice is reversible-by-design anyway.

**Crucially, the hard-to-retrofit decisions ([[ADR-001]] tenancy/RLS, [[ADR-002]] Postgres, [[ADR-003]] lifecycle, [[ADR-004]] interval storage, [[ADR-011]] integration) are language-agnostic and unchanged.** The language was always the reversible layer — this reversal demonstrates the thesis.

## Decision

**Backend / product layer → C#/.NET (ASP.NET Core).**
- Runtime-type-safe end to end (no erased types); strong compute (JIT, structs, `Span<T>`, **real in-process multithreading**) — which **raises the in-process ceiling so fewer compute kernels need extracting** than the TS plan implied.
- **Domain core → pure C# class libraries** (framework-free, I/O-free, **deterministic** — required for golden tests). Hexagonal: adapters/UI depend on the core, never the reverse. The ASP.NET app **and** the background workers **share the same domain assemblies directly** — no reimplementation across the on-demand and scheduled paths.

**Frontend → React/TypeScript.**
- The web default; richest data-UI ecosystem (charts, data grids) — which a chart/grid-heavy WFM UI needs. Accepts a **two-language split (C# + TS)** as the cost. Contract between them is **schema-based (OpenAPI → generated TS client)**, so the frontend stays in sync without hand-maintained types. *This is the one place TS remains.*
- Hard-UI notes (Anders domain input, both **later** capabilities, not v1): **schedule edit screens** (dense/fidgety) → virtualization + canvas for the densest grid (DUP-007); **real-time adherence** → SignalR push + canvas/virtualized rendering (DUP-004). C#/SignalR + React is a *strength* for the real-time case that was painful at Teleopti.

**Background / forecasting service → .NET Worker Service / `BackgroundService`.**
- Scheduling via Hangfire/Quartz or a Postgres-backed queue; C#'s real multithreading fans a batch across cores **in one process**. (See DUP-001 production-shape note.)

**Compute-heavy kernels → extract on profiling evidence, but the polyglot footprint shrinks.**
- C# handles much compute natively, so likely only **ML forecasting → Python** if at all; the **scheduling optimizer may stay C#** (OR-Tools has C# bindings). Decide on evidence when built.

**Real-time (future) → SignalR.** Best-in-class .NET server→client push.

**Scaling → stateless, horizontal (unchanged).** Workers stateless too.

## Tooling map — the other ADRs' *decisions* are unchanged; only tools change
| ADR | Decision (unchanged) | TS tool → C# tool |
|---|---|---|
| 002 | Postgres relational + JSONB, forward-only migrations | Prisma/Drizzle → **EF Core + Npgsql** (JSONB + RLS session var via Npgsql) |
| 006 | TDD, double-loop, 3-layer forecast-correctness | Vitest→**xUnit/NUnit**; fast-check→**CsCheck/FsCheck**; Testcontainers→**Testcontainers.NET**; Playwright (still, for the few e2e) |
| 007 | Monorepo, modular monolith, enforced boundaries | pnpm/Turbo→**.NET solution** (project-per-module) + the React/TS app in the same repo; boundary lint→**NetArchTest/ArchUnitNET** (fail CI on forbidden refs) |
| 008 | Delegate authN / own permission-based authZ | Frontend (React/TS) still holds per-tab `sessionStorage` active-tenant; ASP.NET reads tenant from URL/token → sets Npgsql session var; provider .NET SDKs or standard OIDC; permission checks in the .NET layer |
| 009 | Trunk-based, CI gate, continuous delivery | `dotnet build/test`; **EF Core migrations** in pipeline; hosting off Vercel → **Azure App Service / Render / Fly.io / containers**; **Neon Postgres still fine**; preview-per-PR via container previews (less turnkey) |
| 010 | Observability + forecast-accuracy monitoring | **Serilog** (structured logs) + **OpenTelemetry .NET** + **Sentry .NET**; SignalR available to push live signals |

## Consequences
- **Two languages (C# + TS)** — the accepted cost; mitigated by OpenAPI-generated clients and a thin frontend.
- **Smaller polyglot footprint** than the TS plan (C# covers more compute in-process).
- **.NET enterprise credibility** for the crescendo / SMB-SaaS paths; and Anders's highest-velocity stack.
- Hosting shifts off the Vercel lane; Postgres/Neon and the entire data spine are untouched.
- The switch **validates the reversibility thesis** — only the stack/tooling layer moved; ADR-001/002/003/004/011 stand.

## Reversal note
Supersedes the original **TypeScript/Next.js** decision (2026-06-20). TypeScript is retained **only for the React frontend**. The original reasoning (one-language-full-stack, AI-assist, time-to-demo) was sound and is preserved here as the considered alternative; it lost to developer-fit (.NET background), enterprise credibility, runtime type safety, and the judgment that motivation > a ~10–20% velocity delta on a solo project — with reversibility keeping the stakes low.
