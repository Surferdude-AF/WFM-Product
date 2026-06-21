# ADR-010 — Observability: standard operational layer + WFM-specific forecast-accuracy monitoring

**Status:** accepted
**Date:** 2026-06-20
**Decision owner:** Anders

## Context
Production needs observability. It splits into a **generic operational layer** (standard, uncontroversial) and a **WFM-specific layer** — *monitoring forecast accuracy in production* — which generic tools can't provide and which is the signal that the product is actually working. The hard wrinkle: a forecast can only be *scored* once actuals arrive (a lag of days/weeks).

## Decision

### Operational layer (table stakes)
- **Structured JSON logs** with a request-id for correlation and **`tenant_id` as a dimension** on every line (slice errors per tenant). **Hard rule: tenant id yes, customer data / PII never** in telemetry (privacy; ties to data-bank governance, [[DUP-006]]).
- **Sentry (or equivalent) error tracking** — highest ROI, lowest effort.
- **Metrics: RED** (Rate, Errors, Duration) on the API; resource metrics.
- **OpenTelemetry** as the vendor-neutral instrumentation standard — instrument once, send to any backend; ready for the multi-service polyglot future where **traces** matter ([[ADR-005]]). Start on a managed free tier.
- Health/readiness endpoints + uptime ping.

### WFM-specific layer — forecast accuracy monitoring
- **Realized-accuracy feedback loop:** when actuals land, compute **realized WMAPE per Skill**, store it, compare against (a) the backtest-predicted accuracy and (b) the Skill's historical accuracy band.
- **Treat realized per-Skill accuracy as a first-class *product* signal, not just an ops metric.** It is the production continuation of **[[PS-011]] (forecast trust)** and feeds the existing **"needs review"** surface — the system proactively says *"CS-B's forecast has degraded vs. its track record — a new pattern/event, or time to re-select the method."*
- **Complements, does not replace, the [[ADR-006]] accuracy-regression gate.** The gate guards *code changes* on a frozen dataset ("did my change make it worse?"); production monitoring guards *reality changing* ("did the world drift from the model?"). Both needed — different failures.
- **"Degraded" is anchored in data and based on *sustained* drift, not single-day noise.** Flag when realized accuracy is outside the Skill's own historical band (e.g. >2σ worse) **over a rolling window** — single bad days are exactly what the existing anomaly detection handles, and per-day alerting would cause alert fatigue.
- Distinguish **forecast monitoring** (accuracy — product/WFM-specialist audience) from **system monitoring** (latency/errors — ops audience).

## Consequences
- The product can prove it's working (live accuracy), not just assert it — the credibility thread of PS-011 extended into production.
- Drift detection surfaces through the same human-in-the-loop "needs review" channel already designed.
- **Future learning loop (flagged, not built):** production realized accuracy is the ultimate scoreboard; the [[ST-006]] method-competition champion selection could eventually weight *live* performance, not just backtest — an ML/agentic opportunity.
- No PII in telemetry constrains log/metric design from day one.
