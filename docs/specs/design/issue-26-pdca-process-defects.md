# Issue #26 PDCA process defects

## I26-ACCEPTANCE-CASE-DRIFT

**Symptom.** The mandatory cached-path command, `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance`, selected 12 cases in N=2 r1/n1. The project policy requires exactly seven. The process exited 0, but only two cases produced measurements; ten failed SQLite setup. Selection drift and fixture setup are separate failures.

**Root cause.** Commit `7f4b8907b4d3ae80b603f4794d1d412004439b66` added the `acceptance` category to `Nextorm_WriteCsv_3Col` on a benchmark class whose `Rows` parameter expands to 1, 10, 100, 1000 and 10000 (`benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkCsvFixedCost.cs:31,66–73`). The category selector includes all five cases, not one. The seven-case policy was not aligned with that expanded population. Provisioning the SQLite fixture can address setup but cannot change selection.

**Cycle effect.** N=2 ended without valid cached-path evidence or independent CHECK. No fix was applied. N=3 carries the contradiction forward for authorized routing; #26 does not change benchmark categories, the invocation or the threshold.

**Evidence.** `.pdca/issue-26/rv1/attempts/1/acceptance-benchmark.log` and `benchmark-summary.md` record the selected population and results. `.pdca/status/issue-26-2.md` records the DO result, ACT outcome and defect history. `.pdca/status/issue-26-3.md` §PLAN record r1 records the unchanged measurement contract.

**Prevention.** When changing an acceptance category or a benchmark parameter set, enumerate the cases selected by the declared command, counting each parameter expansion. Reconcile the identities and count with the policy before publishing the harness change. In acceptance evidence, retain selection and per-case measurement results separately from the process exit code. Route any discrepancy to the policy owner instead of silently filtering cases or changing the threshold.

**N=3 observation (r1/n1).** After SQLite fixture recovery, the unchanged command exited 0: 12 selected, 12/12 measured, zero case setup errors, 79.76 s BenchmarkDotNet global time and 82.42 s external wall time. Five excess cases are the parameter expansions of `SqliteBenchmarkCsvFixedCost.Nextorm_WriteCsv_3Col` (`benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkCsvFixedCost.cs:22–32,66–73`). The successful measurements rule out SQLite setup failure as the cause of selection drift; the prescribed seven-case gate remains unachievable with the current harness. No fixes were applied and no attempt was added. Evidence: `.pdca/issue-26/rv2/attempts/1/acceptance-benchmark.log`; complete `pdca-escalate` report: `.pdca/status/issue-26-3.md §DO result → pdca-escalate report`. The escalation does not constitute acceptance or a routing verdict; independent CHECK judges the criterion after DO completes.
