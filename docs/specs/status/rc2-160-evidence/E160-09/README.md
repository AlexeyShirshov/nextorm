# E160-09 — acceptance benchmarks (D160, plan r=2, rv=3)

## Command and result

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

- exit **0**; BDN selected/executed **7** cases, **0** failures
- external shell wall clock **51 s**; BDN `Global total time: 00:00:44 (44.48 sec), executed benchmarks: 7`
- budget 4 min (240 s) → **PASS** (both figures under budget)
- environment: AMD Ryzen 7 5800HS, Ubuntu 22.04.5 LTS, .NET SDK 10.0.401, .NET 10.0.12,
  BenchmarkDotNet 0.15.8, `Job=ShortRun` (`IterationCount=3`, `WarmupCount=3`, `LaunchCount=1`),
  `InProcessEmitToolchain`, `MemoryDiagnoser` on, `Categories=acceptance`

## The seven cases

| Case | Mean | Allocated | Delta Mean vs baseline |
|------|------|-----------|------------------------|
| `Nextorm_Count` | 2.166 ms | 374.22 KB | -25.7% (high-variance row, not an assertion) |
| `Nextorm_GroupByCount` | 58.70 ms | 50.1 MB | -3.7% |
| `Nextorm_Cached` | 1.983 ms | 609.45 KB | +11.9% (high-variance row) |
| `Prepared_ToList` | 902.0 us | 76.14 KB | -2.4% |
| `Cached_ToList` | 1,840.1 us | 583.19 KB | +6.5% |
| `Cached_PlanOnly_Param` | 550.2 us | 507.05 KB | +6.2% (no-database micro, not comparable) |
| `Nextorm_Cached_ToListAsync` | 2.035 ms | 607.69 KB | -4.8% |

## Cached vs prepared (same run)

| Case | Mean | Ratio vs `Prepared_ToList` | Allocated | Alloc ratio |
|------|------|----------------------------|-----------|-------------|
| `Prepared_ToList` (baseline) | 902.0 us | 1.00 | 76.14 KB | 1.00 |
| `Cached_ToList` | 1,840.1 us | **2.040** | 583.19 KB | **7.66** |

Tracked ratio `Cached_ToList / Prepared_ToList` = **2.040** vs documented baseline **1.87**
(**+9.1%**), below the **20%** investigation threshold (absolute gate **2.244**). Allocated-memory
ratio **7.66** vs baseline **7.42** (**+3.2%**), unchanged within noise. **Verdict: no regression,
no investigation trigger.**

Raw BDN output: `acceptance.log` (this directory).
