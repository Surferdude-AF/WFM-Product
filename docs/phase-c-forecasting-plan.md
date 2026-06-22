# Phase C plan — port the forecasting domain core (scaffolding-plan step 9)

Decomposes the one-line roadmap step ("port the pure domain core, test-first") into ordered, individually-green PRs. The prototype at `c:\dev\WFM-Take1\prototypes\forecasting` is the **executable spec**; the evolved `server.js` (not the simpler `forecast.js`/`evaluate.js` spikes) is the **source of truth** where they diverge.

## Principles (from the ADRs)
- **Test-first on the core**, always — it's numeric and is the product's value (ADR-006). No core code without a failing test first.
- **Three correctness layers per capability** (ADR-006): property/invariant (CsCheck) + golden/characterization (recorded prototype output) + — for the forecast pipeline as a whole — the accuracy-regression gate.
- **Pure & deterministic** (ADR-005): framework-free, I/O-free, "today"/start-date injected, no wall-clock, no hidden randomness. The boundary check (NetArchTest) already forbids the domain depending on EF/ASP.NET.
- **Small green PRs, trunk-based** (ADR-009). Each slice below is one PR.

## Three decisions locked here
1. **Source of truth = `server.js`.** The spike `forecast.js`/`evaluate.js` are superseded. Concretely this means: detection is the **modified z-score** (median+MAD, threshold 3.5, with a ratio-band fallback), *not* the spike's flat 1.25×-median; grouping/forecast use the `server.js` versions below.
2. **Determinism is already satisfied by the source of truth — preserve it.** `server.js`'s `parseTS` reads `"YYYY-MM-DD HH:MM"` as **UTC** and every downstream uses `getUTCDay/Hours/Minutes`; the local-vs-UTC mixing was only in the throwaway spike. The C# port keys intervals by `(dayOfWeek, intervalIndex 0..95)` computed from the timestamps it is given, and injects the forecast start date — never `DateTime.Now`. Represent timestamps as `DateTimeOffset` or as the integer interval key.
4. **Per-Skill timezone is a gap in the prototype — own it in a dedicated slice (9b), keep the baseline timezone-agnostic.** The prototype is implicitly single-timezone; in reality a Skill like "CS Germany" must learn and forecast in its **local wall-clock** (CET/CEST), because its daily pattern, operating hours, holidays and event dates are all local. Historical stats are UTC. So 9a groups by the day-of-week+interval of *whatever timestamps it receives* (tz-agnostic), and 9b converts UTC→Skill-local **before** grouping and local→UTC **after** the forecast. This means 9a needs no rework when 9b lands. Localization uses **NodaTime** for IANA zones + DST (pure/deterministic; not a forbidden dependency, but new — pin the tz-db version so golden fixtures stay stable). **DST policy (v1):** forecast in local wall-clock at a fixed 96 intervals/day; the twice-a-year 92/100-interval transition days are a documented known simplification.
3. **Golden capture recipe.** The prototype is deterministic given a frozen CSV. We carve the pure functions for a slice into a throwaway `capture-golden.mjs` (copy the functions verbatim — do **not** `require('./server.js')`, which boots an HTTP server), run it over the frozen inputs (`data/historical.csv` ~9 weeks; `data/historical-cs.csv` is a second series), and emit JSON fixtures committed under the test project. The fixture is blessed once by eye, then locks behaviour. Capture script is not committed to this repo (it lives/ran in WFM-Take1); the **fixtures** are the artifact.

## Value-object boundaries (introduced per slice, not all up front)
- `HistoricalInterval { DateTimeOffset Start; int Contacts; int AhtSeconds }` — ingestion input (9a).
- `ForecastPoint { DateTimeOffset Start; int Contacts; int AhtSeconds }` — forecast output (9a).
- `IntervalKey { DayOfWeek Dow; int Index /*0..95*/ }` — the seasonal grouping key (9a).
- `StaffingRequirement { int Agents }` and Erlang inputs (`Aht`, `Patience`, `SlTarget`, `SlSeconds`, `OccupancyCap`) (9f).
- Operating-hours config (`OpenHours`, `SpecialDay`) and `Anomaly { Date; Direction; Ratio }` (9c/9d).
- `Skill.TimeZoneId` (IANA, e.g. `Europe/Berlin`); unset → UTC (backward-compatible) (9b).
- `Event { ScopeSkills; LocalDateRange; double VolumeMultiplier; double AhtMultiplier }` (9e).
- `Skill` already exists (Id/TenantId/Name). Forecasting config (timezone, open hours, special days, events) hangs off the Skill but is modelled in its own slice, not retrofitted into the persistence entity until ingestion (step 10).

## Slices (each = one red→green PR; golden + property tests)

### 9a — Baseline forecast core *(first slice; thinnest real value)* ✔ DONE
*Ported `groupRecords`/`wavg`/`forecastWeek`/`wmape` as `BaselineForecaster` + `ForecastAccuracy` (`HistoricalInterval`/`ForecastPoint` value objects). Golden test reproduces the prototype byte-for-byte over the frozen `historical.csv`; property tests cover non-negativity, AHT floor, determinism, flat→flat, scale equivariance, and WMAPE scale-invariance. Rounding matches JS `Math.round` (away-from-zero, not banker's). No outlier exclusion (9c).*
- **Port:** `parseTS`, `groupRecords` (key `utcDow-intervalIdx`, history sorted oldest→newest), `wavg` (linear-ramp weights `i+1`), `forecastWeek` (7×96; `contacts=max(0,round(wavg))`, `aht=max(60,round(wavg))`; empty history → `contacts=0, aht=300`), `wmape` (`Σ|a−f| / Σa ×100`, `0` when `Σa=0`).
- **Golden:** group+forecast a frozen ≥4-week slice of `historical.csv` → recorded `ForecastPoint[]`; `wmape` over a recorded hold-out pairing.
- **Invariants (ADR-006 seed):** volume ≥ 0; determinism (same input → identical output); flat history → flat forecast; scale equivariance (×k history → ×k forecast, naive method); AHT ≥ 60.
- **Done:** golden + invariants green; pure (no I/O); boundary check passes.

### 9b — Skill timezone & UTC↔local conversion *(new; precedes everything that reasons in local time)*
- **Build:** `Skill.TimeZoneId` (IANA; unset → UTC). A pure converter (NodaTime, pinned tz-db) that maps UTC `HistoricalInterval`s to Skill-local interval records *before* 9a groups them, and maps the forecast week local → UTC *after*. The forecast week is built on a **local** Monday start (injected).
- **DST policy (v1):** local wall-clock, fixed 96 intervals/day; transition days (92/100 intervals) are a documented known simplification.
- **Invariants:** UTC skill (or unset tz) is identity vs. 9a alone; round-trip UTC→local→UTC preserves the instant off-transition; a CET interval lands on the expected local dow/index for a known instant.
- **Golden:** a known UTC series localized to `Europe/Berlin` across a DST boundary (recorded expected local interval keys).

### 9c — Robust outlier / anomaly detection
- **Port:** `median`, `dayDeviations` (per-weekday median+MAD, `ratio`, modified-z `mz`), `isOutlierDev` (`|mz|>3.5 && |ratio−1|≥0.10`, else ratio band `>1.25 / <0.80`), `detectOutliers` (Set for training exclusion), `detectAnomalies` (direction+magnitude list).
- **Invariants:** one extreme spike moves the forecast less than the spike itself (MAD robustness — ADR-006 seed); a constant series flags nothing; outlier set ⊆ input dates.
- **Golden:** anomaly list for a frozen series with a known injected spike/dip.

### 9d — Operating hours + holiday calendar (ST-002)
- **Port:** `timeToIdx`, `hoursToRange` (close `00:00`→96), `weekdayRange` (unset=24/7, absent weekday=closed), `dayOperating` (special-day override → closed/custom/normal + volume·AHT haircut), `applyOperatingDay` (zero out-of-hours, apply haircut); `holidays.js` (`nthWeekday`/`lastWeekday`, US federal, `holidaysInRange`). Operating hours are **local** (depends on 9b).
- **Invariants:** a closed interval always forecasts 0 (ADR-006 seed); 24/7 leaves the forecast unchanged; haircut of 1.0 is identity.
- **Golden:** US holidays for 2026 across a range; an operating-day mask applied to a known week.

### 9e — Events overlay *(new; deterministic engine only — copilot deferred)*
- **Port:** `applyEvents` — multiply the volume (and AHT) of forecast intervals falling in an event's **local** date range by its multipliers; multiplicative stacking when events overlap; multi-Skill scope. Base forecast stays a pure, regenerable layer underneath.
- **Composition order:** matches `server.js` — `applyOperatingDay(applyEvents(forecast))`: events multiply first, then operating hours zero out-of-hours (a closed interval stays 0 regardless of an event multiplier).
- **Invariants:** no events / multipliers = 1 → identity; volume ≥ 0; out-of-range intervals unchanged; stacking is order-independent (commutative product); a closed interval stays 0 after an event.
- **Golden:** a known forecast week with one and with two overlapping events.
- **Out of scope (later phase):** `copilot.js` — the LLM that *proposes* events. Guardrail (duplo): the LLM fills the same `Event` schema a human would and the deterministic engine here does the math; it never writes a forecast number. Wired as an application/adapter concern with a human-approval loop after the core lands.

### 9f — Erlang A staffing
- **Port:** `erlangAPWait` (log-space, Palm M/M/c+M, tail cutoff), `erlangASL` (`1 − Pwait·e^{…}`), `erlangAStaff` (min agents meeting SL target **and** occupancy cap; `λ=contacts/900`).
- **Invariants:** monotonicity — more contacts (same params) never decreases required agents; SL is non-decreasing in agent count; `contacts=0 → 0 agents`; reduces toward Erlang C as patience→∞.
- **Golden:** staffing curve for a grid of (contacts, AHT, patience, SL) against recorded prototype output (this is the numerically fiddly one — golden is the safety net for the log-space sum).

### 9g — Trend + method competition (ST-006)
- **Port:** `splitIntoWeeks` (ISO-Monday key), `weeklyTotalsOf`, `weeklySlope` (OLS), `trendFactor`, `FORECASTERS` registry (`seasonal-naive`, `seasonal-trend`), `runCompetition` (walk-forward folds `w=4..weeks−2`, needs ≥6 weeks; parsimony+margin selection: upgrade to trend only if it beats naive by ≥ `max(1, SE-of-diff)`), green/amber thresholds.
- **Invariants:** with <6 weeks → defaults to `seasonal-naive`; a registry of one method always selects that method; flat series → trend never wins (parsimony); selection is deterministic.
- **Golden:** the competition result (chosen method, per-method accuracy/bias, thresholds) for both frozen series — `historical.csv` is expected to keep `seasonal-naive` (documented in ST-006).

### 9h — Accuracy-regression gate (ADR-006 layer 3)
- **Build:** a walk-forward backtest over the **frozen corpus** committed as a test fixture, asserting WMAPE does not cross a threshold **anchored in the measured baseline** (not arbitrary) and does not regress vs. the recorded champion. Wired into the CI gate.
- **Done:** the gate fails CI on a deliberate accuracy regression, passes on the current core.

## Sequencing notes
- **9a** is independent and first (tz-agnostic; ships with **no** outlier exclusion — added in 9c).
- **9b** (timezone) precedes everything that reasons in local time (9d operating hours/holidays, 9e events). 9a needs no rework when it lands.
- **9c** (detection) feeds 9a's training-exclusion and is used by 9g's folds.
- **9e** (events) and **9d** (operating hours) both transform the forecast **output** in local time; composition is `applyOperatingDay(applyEvents(forecast))`.
- **9f** (Erlang A) is independent of the forecast path — can be built any time after 9a.
- **9g** depends on 9a (+9c). **9h** depends on 9a (+9c, and 9g for champion comparison).
- Property tooling: **CsCheck** (already referenced; `tests/Wfm.Forecasting.Domain.Tests/DomainPropertyHarnessTests.cs` is the seed). Golden fixtures live beside the domain tests.
- This is the build-out of ADR-006's three-layer strategy for forecasting; the seed-invariant list there is owned by domain expertise and grows as we port.

## Open questions (resolve before the affected slice, not now)
- 9b: confirm **NodaTime** as the domain tz library (vs. `TimeZoneInfo`, which has weaker cross-platform IANA support on Windows) and the DST-transition v1 simplification.
- 9d/9e: do timezone/operating-hours/special-days/events persist on the `Skill` (JSONB edge, ADR-002) now, or stay pure value objects until the ingestion slice (step 10)? Leaning: value objects now, persist in step 10.
- 9f: expose Erlang inputs as per-Skill config now or hardcode demo defaults until a later staffing slice? Leaning: value objects now, persistence later.
- 9h: which series is the frozen corpus — `historical.csv`, `historical-cs.csv`, or both? And the exact anchored threshold (set from the measured baseline once 9a–9g land).
