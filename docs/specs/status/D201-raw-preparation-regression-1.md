# CCORR-201 — corrective task (raw-preparation regression)

- corrective-id: `CCORR-201`
- origin: collection `1.0.9-todo` stage **C (collection CHECK) — FAIL** (2026-10-15), branch `1.0.9-rc3` @ `df2b30ca`
- source issue: #201 / T201 (cached-path parity), milestone `1.0.9-rc3`
- selected_variant: `pdca-dotnet`
- phase: **P — pending** (PLAN not yet authored; `planner` authors it)
- evidence: `artifacts/pdca/collection-1.0.9-todo/C/` (C-report.md + raw logs)

## Defects to correct (do NOT fix in place)

### D01 — P1/High: global-filter policy reuse
- `src/nextorm.core/DataContext/QueryPlanner.cs:629` — raw preparation reuse (`ApplyPreparationFrom(recipePlan.QueryCommand)` + `CaptureRawGraph` + staged key) reuses a prior prepared predicate without evaluating the current context-dependent builder filter. Introduced by commit `6f7ad459` (#201 T201).
- Acceptance (draft, PLAN finalizes): permissive-context → restrictive-context regression AND reverse ordering; either safe reuse with correct filter isolation or fallback to re-preparation; `file:line` evidence.

### D02 — P1: cached-preparation liveness integration failure
- `tests/nextorm.integration.tests/CommonTestSuite.CachedPreparation.cs:148` — `CachedPreparationRawCandidateIsLive` returns False non-deterministically (Postgres 3/3 targeted, MySQL 1/3 targeted; full runs exit 1). Test new in `a21e5d89`.
- Acceptance (draft, PLAN finalizes): preserve result-parity checks; make raw-candidate liveness deterministic (fix the raw-plan lifecycle OR correct the thread-sensitive oracle — do not delete the assertion or retry-until-green); integration suite green, 0 failed, 0 provider-level skips.

## Explicitly out of scope

- T232 PIVOT/UNPIVOT identifier safety (`SqlServerEntityBuilderExtensions.cs:148,199`) — already accepted risk under **#237**; no duplicate corrective work.

## Required after the fix

- Re-run the FULL collection C on the integrated branch (`1.0.9-rc3`), same streams as `artifacts/pdca/collection-1.0.9-todo/C/C-report.md`.
