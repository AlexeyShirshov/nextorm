# E222-DESIGN — T222 rv=1 footprint / invariant audit

Baseline commit: 4fbb603b ; HEAD: 535fb42901b2a4736fbf113c236bb1a57bdecc27

## Production footprint (baseline..HEAD)
 src/nextorm.core/Builders/EntityBuilder.cs         |  44 ++++
 .../Builders/Joins/JoinedEntityBuilder.cs          | 264 +++++++++++++++++++++
 2 files changed, 308 insertions(+)

## Invariant: JoinSourceResolver.cs unchanged
  RESULT: unchanged

## Invariant: provider src unchanged
  RESULT: unchanged

## Invariant: EntityBuilder.cs change is additive only (0 deletions)
  44	0	src/nextorm.core/Builders/EntityBuilder.cs
  264	0	src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs

## Invariant: no raw-table / Cross / Apply source-less additions

## Perf decision
  No planner / plan-cache / per-row materialization change; the new members are construction-time wrappers.
  Changed product files: EntityBuilder.cs (+44), JoinedEntityBuilder.cs (+264) only.

## Pre-existing dirty files (must stay uncommitted/preserved)
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BASE/git-status-porcelain.txt
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BASE/git-status-short.txt
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BASE/prerequisites.json
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BASE/receipt.json
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BASE/stdout.log
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BUILD/receipt.json
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-BUILD/stdout.log
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-COMPAT/T222-COMPAT-ALLNULL.log
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-COMPAT/T222-COMPAT-NEG-INFER.log
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-COMPAT/T222-COMPAT-POS.log
 M artifacts/pdca/D222-join-overloads-by-type/rv1/E222-COMPAT/receipt.json
 M artifacts/pdca/D222-join-overloads-by-type/rv1/baseline.patch
 M docs/assets/diagrams/nextorm-command-execution.svg
 M docs/guide/02-joins.md
 M docs/ru/assets/diagrams/nextorm-command-execution.svg
 M docs/ru/guide/02-joins.md
 M docs/specs/status/D53-public-api-freeze-1.md
