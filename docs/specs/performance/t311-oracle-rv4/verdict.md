# T311 #311 JSON streaming oracle v2 verdict

## Session Provenance

session-1-invalid was rejected by CHECK (T311-CHECK-01..03, exit 2, INVALID_INPUT, 0 valid pairs, preserved at .pdca/T311/rv4/session-1-invalid/). Every verdict below comes exclusively from the repaired session-2.

## Capture basis

All benchmark builds and 2-round paired ABBA measurements were executed in a clean detached worktree at the retained commit 2ea3ef45 (tree b3026856..., zero diff across src/, tests/, benchmarks/), while collector orchestration, evidence storage (.pdca/T311/rv4/) and versioned receipts (docs/specs/performance/t311-oracle-rv4/) reside on branch 1.0.9-rc5 at HEAD 9e7fb4b3.

## Per-size verdict

| size | limit | round 1 ratio | round 2 ratio | exp(mean(d)) | direction agree | unstable | verdict |
|---|---:|---:|---:|---:|---|---|---|
| 1k | 1.20 | 1.115625 | 1.056459 | 1.085639 | True | False | PASS |
| 10k | 1.00 | 0.933859 | 0.914260 | 0.924008 | True | False | PASS |
| 100k | 0.95 | 0.901249 | 0.909219 | 0.905226 | True | False | PASS |

## Arithmetic

### 1k (limit 1.20)
- round 1: nextorm_mean_ns=590104.676 linq2db_mean_ns=528945.535 d_i=ln(nextorm/linq2db)=0.109414 ratio=exp(d_i)=1.115625
- round 2: nextorm_mean_ns=572401.854 linq2db_mean_ns=541811.708 d_i=ln(nextorm/linq2db)=0.054923 ratio=exp(d_i)=1.056459
- exp(mean(d)) = 1.085639
- direction_agreement = True (sign(d_1)==sign(d_2))
- unstable = False (noise s=0.30, instability factor >1.30)
- min(nextorm)/min(linq2db) = 1.082157 (diagnostic only)
- verdict = PASS

### 10k (limit 1.00)
- round 1: nextorm_mean_ns=7748377.091 linq2db_mean_ns=8297155.206 d_i=ln(nextorm/linq2db)=-0.068429 ratio=exp(d_i)=0.933859
- round 2: nextorm_mean_ns=7428037.159 linq2db_mean_ns=8124639.958 d_i=ln(nextorm/linq2db)=-0.089640 ratio=exp(d_i)=0.914260
- exp(mean(d)) = 0.924008
- direction_agreement = True (sign(d_1)==sign(d_2))
- unstable = False (noise s=0.30, instability factor >1.30)
- min(nextorm)/min(linq2db) = 0.914260 (diagnostic only)
- nextorm alloc bytes: [412748.0, 412748.0] (limit 446095, ok=True)
- verdict = PASS

### 100k (limit 0.95)
- round 1: nextorm_mean_ns=74532572.429 linq2db_mean_ns=82699176.714 d_i=ln(nextorm/linq2db)=-0.103973 ratio=exp(d_i)=0.901249
- round 2: nextorm_mean_ns=73202814.000 linq2db_mean_ns=80511721.095 d_i=ln(nextorm/linq2db)=-0.095169 ratio=exp(d_i)=0.909219
- exp(mean(d)) = 0.905226
- direction_agreement = True (sign(d_1)==sign(d_2))
- unstable = False (noise s=0.30, instability factor >1.30)
- min(nextorm)/min(linq2db) = 0.909219 (diagnostic only)
- verdict = PASS

## Acceptance controls (7/7)

all_controls_pass=True all_rounds_within_240s=True
- {'round': 1, 'case': 'InMemoryBenchmarkAggregates.Nextorm_Count', 'baseline_mean_ns': 2230064.8111979165, 'candidate_mean_ns': 2184036.69921875, 'ratio': 0.9793601909020565, 'threshold': 1.2, 'pass': True}
- {'round': 1, 'case': 'InMemoryBenchmarkGroupBy.Nextorm_GroupByCount', 'baseline_mean_ns': 60656632.629629634, 'candidate_mean_ns': 59729364.7037037, 'ratio': 0.9847128354192056, 'threshold': 1.2, 'pass': True}
- {'round': 1, 'case': 'SqliteBenchmarkAny.Nextorm_Cached', 'baseline_mean_ns': 2114751.9505208335, 'candidate_mean_ns': 2111547.4986979165, 'ratio': 0.9984847150408691, 'threshold': 1.2, 'pass': True}
- {'round': 1, 'case': 'SqliteBenchmarkCachedPlan.Prepared_ToList', 'baseline_mean_ns': 898763.4440104166, 'candidate_mean_ns': 897928.1500651041, 'ratio': 0.9990706186918492, 'threshold': 1.2, 'pass': True}
- {'round': 1, 'case': 'SqliteBenchmarkCachedPlan.Cached_ToList', 'baseline_mean_ns': 1535478.3365885417, 'candidate_mean_ns': 1545551.9401041667, 'ratio': 1.006560563751102, 'threshold': 1.2, 'pass': True}
- {'round': 1, 'case': 'SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param', 'baseline_mean_ns': 421549.1520182292, 'candidate_mean_ns': 405135.16878255206, 'ratio': 0.9610627060756908, 'threshold': 1.2, 'pass': True}
- {'round': 1, 'case': 'SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync', 'baseline_mean_ns': 2098922.9368489585, 'candidate_mean_ns': 2137681.4986979165, 'ratio': 1.0184659289621871, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'InMemoryBenchmarkAggregates.Nextorm_Count', 'baseline_mean_ns': 2161473.05859375, 'candidate_mean_ns': 2160699.2447916665, 'ratio': 0.9996419970172624, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'InMemoryBenchmarkGroupBy.Nextorm_GroupByCount', 'baseline_mean_ns': 57566910.25925926, 'candidate_mean_ns': 57879139.44444445, 'ratio': 1.0054237613896426, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'SqliteBenchmarkAny.Nextorm_Cached', 'baseline_mean_ns': 2129551.8072916665, 'candidate_mean_ns': 2160066.3268229165, 'ratio': 1.0143290806200473, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'SqliteBenchmarkCachedPlan.Prepared_ToList', 'baseline_mean_ns': 894330.7666015625, 'candidate_mean_ns': 896680.9554036459, 'ratio': 1.0026278742606765, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'SqliteBenchmarkCachedPlan.Cached_ToList', 'baseline_mean_ns': 1614632.3893229167, 'candidate_mean_ns': 1567353.6881510417, 'ratio': 0.9707185973200371, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'SqliteBenchmarkCachedPlan.Cached_PlanOnly_Param', 'baseline_mean_ns': 415823.4541015625, 'candidate_mean_ns': 394611.0260416667, 'ratio': 0.9489869370025608, 'threshold': 1.2, 'pass': True}
- {'round': 2, 'case': 'SqliteBenchmarkWhere.Nextorm_Cached_ToListAsync', 'baseline_mean_ns': 2124073.7721354165, 'candidate_mean_ns': 2131410.8255208335, 'ratio': 1.0034542366097015, 'threshold': 1.2, 'pass': True}

## Exit code

exit_code=0
