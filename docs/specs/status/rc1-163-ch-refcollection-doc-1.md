# ClickHouse reference->collection accepted limitation — #163 (task D163)

- task: D163
- issue: #163 (https://github.com/AlexeyShirshov/nextorm/issues/163)
- collection: 1.0.9-rc1, group G01
- branch: 1.0.9-rc1 (current worktree)
- cycle: N=1, revision r=1, attempt n=1/3, contract rv=rv1
- mode: autonomous + auto-commit; no push; no merge

## Goal

Disposition-closure task for issue #163. The ClickHouse reference->collection navigation shape is an
**accepted provider limitation**, already implemented, documented EN+RU and pinned by a rejection test;
#163 is already satisfied. This cycle performs the **verification/disposition closure**: confirm the gate
and precise diagnostic, re-run the rejection test on a real ClickHouse container, audit public docs for
residual affirmative defect framing, confirm the work is docs-only (no production/test/config change), and
record the authoritative milestone (`1.0.9-rc1`). No production change is expected or permitted.

## Decision

Decision = **verification/disposition closure** (not implementation). #163 is already satisfied:

- the gate exists (`ISqlDialect.SupportsReferenceToCollectionNavigation`, DIM default `true`;
  `ClickHouseDialect` overrides `false`) and throws a precise `NotSupportedException`
  ("cannot correlate a subquery on a joined source") instead of leaking the engine `NOT_IMPLEMENTED`;
- the rejection test exists
  (`tests/nextorm.integration.tests/ClickHouseImplicitNavigationTests.cs:100`,
  `Reference_to_collection_is_rejected_with_a_precise_diagnostic`);
- EN+RU docs state the accepted limitation (`docs/guide/29-implicit-navigation.md`,
  `docs/ru/guide/29-implicit-navigation.md`, `docs/advanced/limitations.md`,
  `docs/ru/advanced/limitations.md`);
- the authoritative milestone is `1.0.9-rc1` (verified from the issue), not the earlier `1.0.9-rc2`
  mention in the historical SCOPE SIGN-OFF r3.

Disposition: **close as completed** (D3, after CHECK PASS) once E163 evidence is green. No product work.

## Acceptance criteria (R163-01..R163-06)

- **R163-01** The ClickHouse reference->collection shape is gated by
  `ISqlDialect.SupportsReferenceToCollectionNavigation` (`ClickHouseDialect` false) and rejected with a
  precise `NotSupportedException`, never the raw engine `NOT_IMPLEMENTED`.
- **R163-02** `ClickHouseImplicitNavigationTests.Reference_to_collection_is_rejected_with_a_precise_diagnostic`
  passes on a real ClickHouse container (exit 0, 0 failed).
- **R163-03** EN+RU docs describe the shape as an **accepted provider limitation (not a defect)** and
  state the precise rejection; the gate and revisit trigger are unchanged.
- **R163-04** The framing audit finds no residual affirmative defect framing in public docs; negations
  ("accepted limitation"/"not a defect"/«не дефект») are acceptable.
- **R163-05** No production/test/config change: `git diff -- src tests` is empty; only status docs
  changed; `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors; `git diff --check` exit 0.
- **R163-06** The current #163 milestone `1.0.9-rc1` is recorded; the historical `1.0.9-rc2` mention does
  not define the follow-up's current binding, and the accepted limitation / gate / revisit trigger are
  unchanged.

## DO units

- **D0** Persist this plan (header, goal, decision, acceptance, evidence contract `rv1`) and capture the
  baseline (`git status --porcelain`, production diff, issue URL/milestone).
- **D1** Milestone clarification: dated `2026-10-06` note next to the recorded milestone in
  `docs/specs/status/nav-implicit-148b-1.md` (current milestone `1.0.9-rc1`); do not rewrite the
  historical SCOPE SIGN-OFF r3 lines.
- **D2** Verification: read the integration-tests skill; run the ClickHouse rejection test on a real
  container with `DOCKER_HOST` (no external `NEXTORM_CLICKHOUSE_CONNECTION`); framing audit over
  `docs` + `readme.md`; `dotnet build nextorm.slnx -c Debug` 0/0; confirm `git diff -- src tests` empty;
  `git diff --check`.
- **D3** (after CHECK PASS) Disposition/closure: close #163 as completed, record ACT in this file and the
  collection tracker. Not executed in DO.

## Variant matrix

| # | Variant | Outcome |
| --- | --- | --- |
| A | Close as accepted provider limitation (docs/disposition closure) | **Chosen** — the shape is gated explicitly and documented; not a defect. |
| B | Implement ClickHouse reference->collection correlation | Rejected — the engine cannot correlate a subquery on a joined source; not expressible without a rewrite outside this scope. |
| C | Keep #163 open as a defect | Rejected — no defect: the gate is an approved provider limitation with a precise diagnostic. |

## Perf

- N/A — docs/verification-only; no production path is touched. No performance gate applies.

## Recon

- None — the target artifacts are known and pinned from the #148-B SCOPE SIGN-OFF r3.

## Evidence contract rv1

| requirement ID | row ID | check / scenario | command or invocation + required result | planned artifacts | owner | applicability | rv |
|---|---|---|---|---|---|---|---|
| R163-01 | E163-01 | Gate + precise diagnostic in source | read `src/nextorm.core/ISqlDialect.cs` (DIM) + `src/nextorm.clickhouse/ClickHouseDialect.cs` override; explicit PASS/FAIL | source pointers | coder | always | rv1 |
| R163-01 | E163-02 | Rejection test exists and asserts precise message | read `tests/nextorm.integration.tests/ClickHouseImplicitNavigationTests.cs:100`; explicit PASS/FAIL | test pointer | coder | always | rv1 |
| R163-02 | E163-03 | Real ClickHouse container rejection test | `DOCKER_HOST=... dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~ClickHouseImplicitNavigationTests.Reference_to_collection_is_rejected_with_a_precise_diagnostic"`; exit 0, 0 failed | `/tmp/nextorm-d163/ch-rejection.log` | coder | always | rv1 |
| R163-03 | E163-04 | EN+RU docs state accepted limitation + precise rejection | read `docs/guide/29-implicit-navigation.md`, `docs/ru/guide/29-implicit-navigation.md`, `docs/advanced/limitations.md`, `docs/ru/advanced/limitations.md`; explicit PASS/FAIL | doc pointers | coder | always | rv1 |
| R163-04 | E163-05 | Framing audit — no residual affirmative defect framing | `git grep -n -i -E 'clickhouse\|#163\|reference.{0,12}collection\|reference→collection\|коррелир' -- docs readme.md`; manual classification of each hit | `/tmp/nextorm-d163/framing-audit.log` | coder | always | rv1 |
| R163-05 | E163-06 | Build 0/0 | `dotnet build nextorm.slnx -c Debug`; exit 0, 0 warnings / 0 errors | `/tmp/nextorm-d163/build-debug.log` | coder | always | rv1 |
| R163-05 | E163-07 | No production/test/config change | `git diff -- src tests` empty; `git status --porcelain` shows only status docs | `/tmp/nextorm-d163/prod-diff.log` | coder | always | rv1 |
| R163-06 | E163-08 | Milestone clarification + clean diff | `docs/specs/status/nav-implicit-148b-1.md` dated note; issue milestone `1.0.9-rc1`; `git diff --check` exit 0 | `/tmp/nextorm-d163/git-diff-check.log` | coder | always | rv1 |

## Baseline (D0)

- `git status --porcelain`: ` M docs/specs/status/collection-1.0.9-rc1.md` + `?? artifacts/` (pre-existing,
  not produced by D163).
- Production diff (`git diff -- src tests`): empty.
- Issue: #163 OPEN, milestone `1.0.9-rc1` (number 19), URL
  https://github.com/AlexeyShirshov/nextorm/issues/163.

## CHECK re-gather budget

- CHECK may re-gather evidence at most **2** times for this revision.

## Progress log

- 2026-10-06 07:16 UTC | PLAN | r1 | n=1/3 | plan r1 persisted (rv1); decision = verification/disposition closure; DO units D0-D3 defined; baseline captured | docs/specs/status/rc1-163-ch-refcollection-doc-1.md
- 2026-10-06 07:16 UTC | DO | r1 | n=1/3 | D0 baseline: `git status --porcelain` = only `collection-1.0.9-rc1.md` modified + `artifacts/` untracked (pre-existing); `git diff -- src tests` empty; issue #163 OPEN, milestone `1.0.9-rc1` | /tmp/nextorm-d163/baseline-status.txt, prod-diff.log, issue-163.json
- 2026-10-06 07:16 UTC | DO | r1 | n=1/3 | D1 milestone clarification note (2026-10-06) added to nav-implicit-148b-1.md: current #163 milestone `1.0.9-rc1`; historical `1.0.9-rc2` mention does not bind; limitation/gate/trigger unchanged; SCOPE SIGN-OFF r3 lines not rewritten | docs/specs/status/nav-implicit-148b-1.md
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | E163-01 gate confirmed: `ISqlDialect.SupportsReferenceToCollectionNavigation` DIM true (ISqlDialect.cs:1236) + `SqlDialectBase.cs:797` + `ClickHouseDialect.cs:443` override false; rejection at `NavigationExpansion.cs:377-381` precise `NotSupportedException` | src/nextorm.core/DataContext/Dialect/ISqlDialect.cs, src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs, src/nextorm.clickhouse/ClickHouseDialect.cs, src/nextorm.core/Visitors/NavigationExpansion.cs
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | E163-02 rejection test present: `tests/nextorm.integration.tests/ClickHouseImplicitNavigationTests.cs:100` asserts `NotSupportedException` with `*cannot correlate a subquery on a joined source*` | tests/nextorm.integration.tests/ClickHouseImplicitNavigationTests.cs
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | E163-03 real ClickHouse container rejection test: exit 0, total 1 / failed 0 / succeeded 1 / skipped 0 | /tmp/nextorm-d163/ch-rejection.log
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | E163-04/E163-05 docs + framing: public docs (guide 29 EN/RU, limitations EN/RU) describe the shape as an accepted provider limitation (not a defect) with the precise rejection; no affirmative defect framing; #163 not referenced from public docs | /tmp/nextorm-d163/framing-public.log, framing-audit.log
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | E163-06 build `dotnet build nextorm.slnx -c Debug` exit 0 / 0 warnings / 0 errors | /tmp/nextorm-d163/build-debug.log
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | E163-07/E163-08 no production/test/config change: `git diff -- src tests` empty; `git status --porcelain` only the two status docs + untracked plan; `git diff --check` exit 0 | /tmp/nextorm-d163/prod-diff.log, status-after.txt, git-diff-check.log
- 2026-10-06 07:17 UTC | DO | r1 | n=1/3 | durable state: cycle N=1, revision r=1, attempt n=1/3; no replan; no rejected candidate; no defect keys; D0-D2 complete; D3 pending CHECK PASS | docs/specs/status/rc1-163-ch-refcollection-doc-1.md
- 2026-10-06 07:19 UTC | DO | r1 | n=1/3 | CRLF byte scan of D163 changed status docs (pre-append): rc1-163-ch-refcollection-doc-1.md total_lines=120 CRLF=120 lone_LF=0 lone_CR=0; nav-implicit-148b-1.md total_lines=371 CRLF=371 lone_LF=0 lone_CR=0; both fully CRLF | /tmp/nextorm-d163/crlf.log
- 2026-10-06 07:20 UTC | CHECK | r1 | n=1/3 | CHECK PASS on rv1: E163-01..E163-08 all green; rejection test on real containerized ClickHouse 1/1, 0 failed, 0 skipped; build 0/0; no production/test/config change. Frozen contract rv1; no replan; no rejected candidate; no defect keys | docs/specs/status/rc1-163-ch-refcollection-doc-1.md
- 2026-10-06 07:20 UTC | ACT | r1 | n=1/3 | D3 disposition/closure: #163 closed as completed (accepted provider limitation); explicit gate retained; docs-only; milestone clarification recorded; queue terminal | https://github.com/AlexeyShirshov/nextorm/issues/163
- 2026-10-06 07:20 UTC | ACT | r1 | n=1/3 | commit plan: stage only the D163 change set (nav-implicit-148b-1.md + rc1-163-ch-refcollection-doc-1.md), commit `#163 Verify ClickHouse reference-to-collection disposition (accepted limitation)`, then a separate bookkeeping commit for collection-1.0.9-rc1.md; push never | git status --short

## ACT

- Freeze point: CHECK PASS on contract **rv1** (revision r=1, attempt n=1/3, cycle N=1); evidence
  E163-01..E163-08 all green; no replan, no rejected candidate, no defect keys. Frozen inputs:
  `ISqlDialect.SupportsReferenceToCollectionNavigation` (DIM `true`), `ClickHouseDialect` override
  `false`, the precise `NotSupportedException` rejection, the integration rejection test
  (`ClickHouseImplicitNavigationTests.Reference_to_collection_is_rejected_with_a_precise_diagnostic`,
  1/1 on a real containerized ClickHouse, 0 skipped), and the EN+RU accepted-limitation docs. No
  production/test/config path was touched (`git diff -- src tests` empty); the cycle is
  docs/disposition-only.
- Commit plan (auto-commit authorized; push never): stage only the D163 change set —
  `docs/specs/status/nav-implicit-148b-1.md` (milestone clarification note) and
  `docs/specs/status/rc1-163-ch-refcollection-doc-1.md` (this plan + ACT); commit message
  `#163 Verify ClickHouse reference-to-collection disposition (accepted limitation)`; then a separate
  bookkeeping commit updates `docs/specs/status/collection-1.0.9-rc1.md`. `artifacts/` is never staged.
- Disposition: **close #163 as completed** — verification/disposition closure. The explicit gate is
  **retained** (`ClickHouseDialect.SupportsReferenceToCollectionNavigation = false`, precise
  `NotSupportedException`, never the raw engine `NOT_IMPLEMENTED`); docs-only change; milestone
  clarification recorded (current binding `1.0.9-rc1`; historical `1.0.9-rc2` mention does not bind).
  Revisit trigger intact.
- Issue outcome: #163 closed as completed with a closure comment citing the verification evidence, the
  retained gate and the commit SHA; no production change.
- Durable state: cycle N=1, revision r=1, attempt n=1/3; D0-D3 complete; queue terminal.
