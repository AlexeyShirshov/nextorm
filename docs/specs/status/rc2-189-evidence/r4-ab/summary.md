# D189 r=4 — paired A/B acceptance measurement summary

- Generated: 2026-10-09T07:49:58Z
- Pairs: 8 (A=`65477266558f2ad959a38d3c045360690769815f`, B=`b25cd227b61f3958376ae3ace4bb81cee1833166`)
- Decision rule: no regression iff all 7 cases U<=1.05; proven regression iff any L>1.05; else indeterminate.
- Replacement pairs used: 0

## Verdict: **PROVEN REGRESSION** (exit 1)

| # | case | R | L | U | U<=1.05 | L>1.05 | alloc A (B) | alloc B (B) | growth flag |
|---|------|---|---|---|---------|--------|-------------|-------------|-------------|
| 1 | `InMemoryBenchmarkAggregates.Nextorm_Count` | 1.0109 | 0.9634 | 1.0606 | NO | no | 342400.0 | 384000.0 | True |
| 2 | `InMemoryBenchmarkGroupBy.Nextorm_GroupByCount` | 0.9948 | 0.9457 | 1.0463 | yes | no | 52488222.0 | 52531288.0 | False |
| 3 | `SqliteBenchmarkAny.Nextorm_Cached` | 1.0821 | 1.0519 | 1.1132 | NO | YES | 560847.0 | 624849.0 | True |
| 4 | `SqliteBenchmarkCachedPlan.Prepared_ToList` | 1.0060 | 0.9850 | 1.0274 | yes | no | 77966.0 | 77966.0 | False |
| 5 | `SqliteBenchmarkCachedPlan.Cached_ToList` | 1.0765 | 1.0413 | 1.1130 | NO | no | 553983.0 | 597984.0 | True |
| 6 | `SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param` | 1.0728 | 1.0064 | 1.1435 | NO | no | 476016.0 | 520016.0 | True |
| 7 | `SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync` | 1.0776 | 1.0398 | 1.1169 | NO | no | 579064.0 | 623077.0 | True |

## Global total time (informational only — never affects exit code)

- Sum over 8 A runs: 922.65 s
- Sum over 8 B runs: 884.20 s

## Notes

- `N` used in `v_i` is BDN `Statistics.N` (post upper-outlier removal; the run configured `--iterationCount 15`).
- Allocated metric is BDN `BytesAllocatedPerOperation`; the median is taken across the 8 runs per side. Growth flag: `B-A > max(1 KiB, 1% of median A)`.
