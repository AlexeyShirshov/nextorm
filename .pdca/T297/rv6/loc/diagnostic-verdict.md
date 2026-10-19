# E38/LOC diagnostic verdict — T297-cow r=6, P04 bounded recipe decomposition

Diagnostic-only pass (no product edit, no fix named). Instrumented probe copies A=`/tmp/opencode/t297-p02/A`
(baseline `0d4d5df2`) and C=`/tmp/opencode/t297-p02/C` (flat-root candidate `777e9233`), probe dlls
`probe-a` / `probe-c`; 15 new `rcp_*` phases (47 total) decompose the `planner_recipe` span. Source and
boundary semantics: `instrumentation.md` (copied from `/tmp/opencode/t297-p02/p04/instrumentation.md`).
Raw numbers: `phase-table.json` (`recipe_decomposition` per arm), `recipe-decomposition.md`, `runs/`.

Protocol: fixed six paired rounds, `A,B,B,A` ABBA, 12 blocks, one pinned process per block
(`taskset -c 1`, `NEXTORM_BENCH_DB`, `DOTNET_gcServer=0`), canonical lock `/tmp/opencode/nextorm-bdn-host.lock`, 60 s quiet window.
Bootstrap 95 % CI of the per-round median ratio (10000 resamples, seed 297). No uninstrumented ABBA re-run.

## Validity

| check | value |
|---|---|
| blocks / protocol | 12/12, ABBA, exits 0 |
| DB sha before/after (all blocks) | `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` (expected, unchanged) |
| side commits | A `0d4d5df2`, C `777e9233` |
| probe dlls | A `t297-probe-a.dll`, C `t297-probe-c.dll` |
| quiet window loadavg | 0.71 -> 0.59 |
| wall total | 70.1 s |

Valid: 12/12 blocks exit 0, parse clean, DB sha identical before and after every block, both sides
instrumented with the same phase set; no competing benchmark/probe process at start.

## Overhead

Instrumentation overhead = measured pair cost x instrumented boundary calls per query (separate from the
phase table; it is the maximum bias instrumentation can add to a delta).

| arm | pair ns A/C | boundary calls/op A/C | overhead A ns/op | overhead C ns/op | delta ns/op |
|---|---|---|---|---|---|
| cte | 60.57 / 61.01 | 29.00 / 33.00 | 1757 | 2013 | +257 |
| recursive | 60.57 / 61.01 | 41.00 / 50.00 | 2483 | 3050 | +567 |
| inlist | 60.57 / 61.01 | 36.00 / 37.00 | 2180 | 2257 | +77 |
| leftjoin | 60.57 / 61.01 | 42.00 / 44.00 | 2544 | 2684 | +140 |

## Per-arm decomposition

`planner_recipe` is the parent span. Inclusive rows are as measured (a recursion level per call);
exclusive rows subtract the nested leaf once, as defined in `recipe-decomposition.md`.
Share = term delta / paired `planner_recipe` delta.

### cte

`planner_recipe`: A **50300.6** / C **58594.8** ns/op, ratio **1.1895** [1.0669, 1.2405], delta **+9481.2 ns/op** (calls/op 1.00/1.00).

| inclusive span | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| rcp_capture | 34818.6 | 37389.7 | 1.0937 | [0.9694, 1.1488] | 2.00/2.00 | +3274.0 | +34.5% |
| rcp_fingerprint | 12623.9 | 13293.6 | 1.0619 | [0.9751, 1.1496] | 2.00/2.00 | +781.0 | +8.2% |
| rcp_filter_scan | 3825.0 | 4128.5 | 1.0412 | [0.9880, 1.1425] | 1.00/1.00 | +166.8 | +1.8% |
| rcp_lookup | 14926.4 | 15869.9 | 1.0735 | [0.9552, 1.1118] | 1.00/1.00 | +1121.9 | +11.8% |
| rcp_matches | 16566.3 | 17435.6 | 1.0687 | [0.9515, 1.1222] | 2.00/2.00 | +1138.6 | +12.0% |
| rcp_transfer | 1813.5 | 6052.3 | 3.3644 | [3.1686, 3.7443] | 1.00/1.00 | +4297.7 | +45.3% |
| rcp_copyto | 1591.5 | 5597.4 | 3.5497 | [3.3578, 4.0500] | 1.00/1.00 | +4071.5 | +42.9% |
| rcp_copyto_members | 43.5 | 288.9 | 7.2226 | [5.9362, 7.6397] | 1.00/1.00 | +247.5 | +2.6% |
| rcp_transfer_edges | 142.3 | 370.4 | 2.5943 | [2.3308, 2.7988] | 1.00/1.00 | +213.5 | +2.3% |
| rcp_staged | 4492.5 | 5770.3 | 1.2878 | [1.2130, 1.4039] | 1.00/1.00 | +1288.9 | +13.6% |
| rcp_qplan_ctor | 67.1 | 107.3 | 1.6812 | [1.5710, 1.7796] | 1.00/1.00 | +44.8 | +0.5% |
| rcp_qplan_hash | 1517.2 | 1960.0 | 1.2941 | [1.1825, 1.5324] | 1.00/1.00 | +432.9 | +4.6% |
| rcp_equals1 | 2526.1 | 3161.0 | 1.2521 | [1.1963, 1.3184] | 1.00/1.00 | +631.1 | +6.7% |
| rcp_equals2 | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |

| exclusive term | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| capture_exclusive | 22226.6 | 24132.9 | 1.1121 | [0.9663, 1.1484] | 2.00/2.00 | +2493.0 | +26.3% |
| lookup_exclusive | -1639.9 | -1565.7 | — | — | 1.00/1.00 | -10.5 | -0.1% |
| transfer_exclusive | 236.5 | 465.8 | 1.9893 | [1.8261, 2.0494] | 1.00/1.00 | +218.1 | +2.3% |
| copyto_exclusive | 1552.3 | 5281.1 | 3.4613 | [3.2770, 3.9430] | 1.00/1.00 | +3808.4 | +40.2% |
| staged_exclusive | 1905.0 | 2490.9 | 1.3299 | [1.2145, 1.5081] | 1.00/1.00 | +616.1 | +6.5% |
| qplan_ctor_exclusive | -1450.1 | -1844.5 | — | — | 1.00/1.00 | -388.1 | -4.1% |
| planner_recipe_exclusive | -9632.8 | -10435.0 | — | — | —/— | -646.0 | -6.8% |

Reconciliation: literal `recipe_residual` (planner_recipe − exclusive − nested terms added back) =
**+0.0 ns/op** (0.0 % of the measured delta; 0 by algebraic construction, reported separately).
Partition sum (14 measured terms) +10135.1 ns/op vs parent +9481.2 -> closure gap +653.8 ns/op; un-timed
remainder `planner_recipe_exclusive` -646.0 ns/op (-6.8%).

80 % test (single coherent actionable cost): top positive term **copyto_exclusive** +3808.4 ns/op; arithmetic
3808.4 / 9481.2 = **40.2 %** of the positive `planner_recipe` delta; threshold >= 80 %: **False**;
paired 95 % CI excludes zero (ci_low 3.2770 > 1): **True**; dominance **FAIL**.

Top 3: copyto_exclusive +3808.4 ns/op (40.2 %); capture_exclusive +2493.0 ns/op (26.3 %); rcp_matches +1138.6 ns/op (12.0 %).

### recursive

`planner_recipe`: A **57212.1** / C **65843.3** ns/op, ratio **1.1474** [1.1083, 1.1865], delta **+8567.7 ns/op** (calls/op 1.00/1.00).

| inclusive span | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| rcp_capture | 79246.8 | 85801.7 | 1.0737 | [1.0387, 1.1067] | 4.00/4.00 | +5990.1 | +69.9% |
| rcp_fingerprint | 18515.2 | 19798.3 | 1.0593 | [1.0236, 1.0928] | 4.00/4.00 | +1128.5 | +13.2% |
| rcp_filter_scan | 2050.8 | 2158.3 | 1.0693 | [0.9855, 1.1181] | 1.00/1.00 | +140.1 | +1.6% |
| rcp_lookup | 12941.1 | 13659.1 | 1.0555 | [1.0247, 1.1077] | 1.00/1.00 | +718.2 | +8.4% |
| rcp_matches | 20446.1 | 21557.0 | 1.0496 | [1.0196, 1.1093] | 3.00/3.00 | +1050.8 | +12.3% |
| rcp_transfer | 1349.1 | 4465.4 | 3.2752 | [3.1374, 3.5226] | 1.00/1.00 | +3095.2 | +36.1% |
| rcp_copyto | 1103.7 | 3986.7 | 3.5637 | [3.3726, 3.8178] | 1.00/1.00 | +2862.1 | +33.4% |
| rcp_copyto_members | 88.7 | 448.5 | 5.0312 | [4.8893, 5.2041] | 2.00/2.00 | +359.8 | +4.2% |
| rcp_transfer_edges | 143.3 | 377.9 | 2.7175 | [2.3585, 3.0564] | 1.00/1.00 | +241.5 | +2.8% |
| rcp_staged | 4322.0 | 5692.0 | 1.3133 | [1.2572, 1.3505] | 1.00/1.00 | +1344.4 | +15.7% |
| rcp_qplan_ctor | 62.9 | 108.2 | 1.7011 | [1.5521, 1.8672] | 1.00/1.00 | +44.6 | +0.5% |
| rcp_qplan_hash | 1465.7 | 1885.3 | 1.2663 | [1.2582, 1.4110] | 1.00/1.00 | +396.5 | +4.6% |
| rcp_equals1 | 2560.3 | 3159.1 | 1.2343 | [1.1926, 1.3163] | 1.00/1.00 | +598.9 | +7.0% |
| rcp_equals2 | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |

| exclusive term | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| capture_exclusive | 60769.9 | 66003.4 | 1.0811 | [1.0404, 1.1109] | 4.00/4.00 | +5048.5 | +58.9% |
| lookup_exclusive | -7505.1 | -7981.4 | — | — | 1.00/1.00 | -283.8 | -3.3% |
| transfer_exclusive | 235.8 | 470.2 | 2.0414 | [1.8394, 2.2264] | 1.00/1.00 | +242.5 | +2.8% |
| copyto_exclusive | 1012.4 | 3531.8 | 3.4405 | [3.2412, 3.6904] | 1.00/1.00 | +2513.5 | +29.3% |
| staged_exclusive | 1746.1 | 2395.3 | 1.3614 | [1.3121, 1.4707] | 1.00/1.00 | +636.3 | +7.4% |
| qplan_ctor_exclusive | -1404.3 | -1778.0 | — | — | 1.00/1.00 | -357.2 | -4.2% |
| planner_recipe_exclusive | -42696.6 | -45815.6 | — | — | —/— | -2790.5 | -32.6% |

Reconciliation: literal `recipe_residual` (planner_recipe − exclusive − nested terms added back) =
**+0.0 ns/op** (0.0 % of the measured delta; 0 by algebraic construction, reported separately).
Partition sum (14 measured terms) +11474.3 ns/op vs parent +8567.7 -> closure gap +2906.5 ns/op; un-timed
remainder `planner_recipe_exclusive` -2790.5 ns/op (-32.6%).

80 % test (single coherent actionable cost): top positive term **capture_exclusive** +5048.5 ns/op; arithmetic
5048.5 / 8567.7 = **58.9 %** of the positive `planner_recipe` delta; threshold >= 80 %: **False**;
paired 95 % CI excludes zero (ci_low 1.0404 > 1): **True**; dominance **FAIL**.

Top 3: capture_exclusive +5048.5 ns/op (58.9 %); copyto_exclusive +2513.5 ns/op (29.3 %); rcp_fingerprint +1128.5 ns/op (13.2 %).

### inlist

`planner_recipe`: A **13320.0** / C **14279.6** ns/op, ratio **1.0762** [0.9316, 1.1080], delta **+1027.2 ns/op** (calls/op 1.00/1.00).

| inclusive span | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| rcp_capture | 10823.3 | 11601.2 | 1.0799 | [0.9133, 1.1143] | 1.00/1.00 | +860.2 | +83.7% |
| rcp_fingerprint | 6850.2 | 7333.7 | 1.0771 | [0.9655, 1.1070] | 1.00/1.00 | +531.0 | +51.7% |
| rcp_filter_scan | 1467.2 | 1567.9 | 1.0913 | [0.9466, 1.1359] | 1.00/1.00 | +133.1 | +13.0% |
| rcp_lookup | 583.6 | 548.3 | 0.9044 | [0.8739, 0.9829] | 1.00/1.00 | -56.6 | -5.5% |
| rcp_matches | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_transfer | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_copyto | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_copyto_members | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_transfer_edges | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_staged | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_qplan_ctor | 572.9 | 845.1 | 1.4383 | [1.3651, 1.5623] | 1.00/1.00 | +256.1 | +24.9% |
| rcp_qplan_hash | 1632.8 | 2377.3 | 1.4986 | [1.3868, 1.6113] | 1.00/1.00 | +790.3 | +76.9% |
| rcp_equals1 | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_equals2 | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |

| exclusive term | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| capture_exclusive | 3996.6 | 4270.7 | 1.0820 | [0.8410, 1.1411] | 1.00/1.00 | +323.9 | +31.5% |
| lookup_exclusive | 583.6 | 548.3 | 0.9044 | [0.8739, 0.9829] | 1.00/1.00 | -56.6 | -5.5% |
| transfer_exclusive | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| copyto_exclusive | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| staged_exclusive | -572.9 | -845.1 | — | — | 0.00/0.00 | -256.1 | -24.9% |
| qplan_ctor_exclusive | -1058.0 | -1541.1 | — | — | 1.00/1.00 | -514.4 | -50.1% |
| planner_recipe_exclusive | 424.1 | 516.1 | 1.1682 | [1.0595, 1.5078] | —/— | +70.9 | +6.9% |

Reconciliation: literal `recipe_residual` (planner_recipe − exclusive − nested terms added back) =
**+0.0 ns/op** (0.0 % of the measured delta; 0 by algebraic construction, reported separately).
Partition sum (14 measured terms) +951.2 ns/op vs parent +1027.2 -> closure gap -76.0 ns/op; un-timed
remainder `planner_recipe_exclusive` +70.9 ns/op (+6.9%).

80 % test (single coherent actionable cost): top positive term **rcp_qplan_hash** +790.3 ns/op; arithmetic
790.3 / 1027.2 = **76.9 %** of the positive `planner_recipe` delta; threshold >= 80 %: **False**;
paired 95 % CI excludes zero (ci_low 1.3868 > 1): **True**; dominance **FAIL**.

Top 3: rcp_qplan_hash +790.3 ns/op (76.9 %); rcp_fingerprint +531.0 ns/op (51.7 %); capture_exclusive +323.9 ns/op (31.5 %).

### leftjoin

`planner_recipe`: A **29178.1** / C **36753.8** ns/op, ratio **1.2632** [1.1431, 1.2933], delta **+7579.6 ns/op** (calls/op 1.00/1.00).

| inclusive span | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| rcp_capture | 10945.0 | 12224.9 | 1.1238 | [1.0233, 1.1639] | 1.00/1.00 | +1337.8 | +17.7% |
| rcp_fingerprint | 5568.2 | 6152.6 | 1.1087 | [1.0312, 1.1397] | 1.00/1.00 | +616.1 | +8.1% |
| rcp_filter_scan | 2868.4 | 3019.7 | 1.0516 | [0.9063, 1.0898] | 1.00/1.00 | +150.6 | +2.0% |
| rcp_lookup | 9110.2 | 9869.0 | 1.0876 | [0.9927, 1.1056] | 1.00/1.00 | +817.0 | +10.8% |
| rcp_matches | 7659.5 | 8302.9 | 1.0860 | [0.9881, 1.1085] | 1.00/1.00 | +652.8 | +8.6% |
| rcp_transfer | 1702.7 | 5463.2 | 3.2975 | [3.0771, 3.4809] | 1.00/1.00 | +3735.1 | +49.3% |
| rcp_copyto | 1487.3 | 5013.8 | 3.5055 | [3.2225, 3.6785] | 1.00/1.00 | +3518.9 | +46.4% |
| rcp_copyto_members | 43.2 | 188.0 | 4.4498 | [3.6692, 5.5078] | 1.00/1.00 | +143.5 | +1.9% |
| rcp_transfer_edges | 131.6 | 357.4 | 2.7974 | [2.5089, 3.0655] | 1.00/1.00 | +230.9 | +3.0% |
| rcp_staged | 4169.4 | 5388.6 | 1.3137 | [1.1385, 1.3928] | 1.00/1.00 | +1282.0 | +16.9% |
| rcp_qplan_ctor | 61.9 | 104.9 | 1.6034 | [1.5654, 1.9214] | 1.00/1.00 | +39.5 | +0.5% |
| rcp_qplan_hash | 1450.7 | 1811.0 | 1.2687 | [1.0992, 1.4576] | 1.00/1.00 | +370.9 | +4.9% |
| rcp_equals1 | 2434.3 | 3076.7 | 1.2776 | [1.1037, 1.3502] | 1.00/1.00 | +672.4 | +8.9% |
| rcp_equals2 | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |
| rcp_discard | 0.0 | 0.0 | — | — | 0.00/0.00 | +0.0 | +0.0% |

| exclusive term | A ns/op | C ns/op | ratio | 95% CI | calls/op | delta ns/op | share |
|---|---|---|---|---|---|---|---|
| capture_exclusive | 5376.8 | 6088.2 | 1.1303 | [1.0154, 1.1991] | 1.00/1.00 | +697.5 | +9.2% |
| lookup_exclusive | 1517.7 | 1585.3 | 1.0522 | [1.0163, 1.1401] | 1.00/1.00 | +75.2 | +1.0% |
| transfer_exclusive | 226.0 | 452.8 | 2.0813 | [1.8932, 2.2360] | 1.00/1.00 | +237.2 | +3.1% |
| copyto_exclusive | 1448.6 | 4836.3 | 3.4796 | [3.1865, 3.6541] | 1.00/1.00 | +3388.8 | +44.7% |
| staged_exclusive | 1728.9 | 2248.4 | 1.3172 | [1.1510, 1.5285] | 1.00/1.00 | +513.7 | +6.8% |
| qplan_ctor_exclusive | -1393.5 | -1708.4 | — | — | 1.00/1.00 | -335.5 | -4.4% |
| planner_recipe_exclusive | 499.6 | 636.1 | 1.3047 | [1.1366, 1.3702] | —/— | +146.4 | +1.9% |

Reconciliation: literal `recipe_residual` (planner_recipe − exclusive − nested terms added back) =
**+0.0 ns/op** (0.0 % of the measured delta; 0 by algebraic construction, reported separately).
Partition sum (14 measured terms) +7183.4 ns/op vs parent +7579.6 -> closure gap -396.2 ns/op; un-timed
remainder `planner_recipe_exclusive` +146.4 ns/op (+1.9%).

80 % test (single coherent actionable cost): top positive term **copyto_exclusive** +3388.8 ns/op; arithmetic
3388.8 / 7579.6 = **44.7 %** of the positive `planner_recipe` delta; threshold >= 80 %: **False**;
paired 95 % CI excludes zero (ci_low 3.1865 > 1): **True**; dominance **FAIL**.

Top 3: copyto_exclusive +3388.8 ns/op (44.7 %); capture_exclusive +697.5 ns/op (9.2 %); rcp_equals1 +672.4 ns/op (8.9 %).

## Diagnostic acceptance verdict

| criterion | result |
|---|---|
| valid blocks / DB | **PASS** — 12/12 blocks exit 0, DB sha `32bf10a08291f25a` unchanged before/after every block |
| overhead quantified | **PASS** — pair cost 60.57/61.01 ns; per-arm overhead C−A +257/+567/+77/+140 ns/op |
| reconciliation residual <=5 % of measured delta | **PASS by the pre-registered definition** — literal `recipe_residual` +0.0/+0.0/+0.0/+0.0 ns/op (0.0 % in every arm, exactly 0 by construction) |
| >=80 % of the positive `planner_recipe` delta from one coherent cost with paired CI excluding zero | **FAIL in all four arms** — best shares 40.2 % (cte), 58.9 % (recursive), 76.9 % (inlist), 44.7 % (leftjoin) |

**No single measured cost reaches 80 % of the positive `planner_recipe` overhead in any arm.**
Per-arm top 3 with numbers (all four measured on the same 12 blocks):

| arm | #1 | #2 | #3 |
|---|---|---|---|
| cte | copyto_exclusive +3808.4 ns (40.2 %, ratio 3.461 [3.2770, 3.9430]) | capture_exclusive +2493.0 ns (26.3 %, ratio 1.112 [0.9663, 1.1484]) | rcp_matches +1138.6 ns (12.0 %, ratio 1.069 [0.9515, 1.1222]) |
| recursive | capture_exclusive +5048.5 ns (58.9 %, ratio 1.081 [1.0404, 1.1109]) | copyto_exclusive +2513.5 ns (29.3 %, ratio 3.441 [3.2412, 3.6904]) | rcp_fingerprint +1128.5 ns (13.2 %, ratio 1.059 [1.0236, 1.0928]) |
| inlist | rcp_qplan_hash +790.3 ns (76.9 %, ratio 1.499 [1.3868, 1.6113]) | rcp_fingerprint +531.0 ns (51.7 %, ratio 1.077 [0.9655, 1.1070]) | capture_exclusive +323.9 ns (31.5 %, ratio 1.082 [0.8410, 1.1411]) |
| leftjoin | copyto_exclusive +3388.8 ns (44.7 %, ratio 3.480 [3.1865, 3.6541]) | capture_exclusive +697.5 ns (9.2 %, ratio 1.130 [1.0154, 1.1991]) | rcp_equals1 +672.4 ns (8.9 %, ratio 1.278 [1.1037, 1.3502]) |

The reported shares are of the positive `planner_recipe` delta, so `inlist` #1 (`rcp_qplan_hash`, 76.9 %,
ratio 1.4986 [1.3868, 1.6113]) is the only arm where one term comes close; it still misses the 80 % bar.
`recursive` #1 (`capture_exclusive`, 58.9 %) is inflated by the recursion accounting documented in
`instrumentation.md` (capture accumulates every recursion level, 4 calls/query), so its true cost is lower.
No fix is named here.

### Measurement caveats (not a protocol change)

Three brief formulas assume strict nesting that the probe's call coverage does not always provide; the
affected exclusive values are negative and must be read as boundary artifacts, not costs:

- `lookup_exclusive = rcp_lookup − rcp_matches` is negative in cte (−1639.9/−1565.7 ns) and recursive
  (−7505.1/−7981.4): `RawShape.Matches` is invoked more often and at more recursion levels than the single
  `TryGetRaw` call (2–3 calls/query vs 1), so Matches is only partly nested in Lookup. Positive in leftjoin
  (+75.2) and inlist (+548.3, with Matches 0 on that plain recipe miss).
- `qplan_ctor_exclusive = rcp_qplan_ctor − rcp_qplan_hash` is negative in all arms (−1393.5…−1058.0 ns):
  the probe measures the ctor's assignments (≈62–107 ns) and `ComputeHash` (≈1451–2377 ns) as **disjoint**
  leaves, not as parent/child (see `instrumentation.md`), so their difference is not a cost; the ctor+hash
  total is `rcp_qplan_ctor + rcp_qplan_hash`.
- `staged_exclusive` is negative on the inlist recipe miss (−572.9/−845.1 ns): the staged block has 0 calls,
  while `rcp_qplan_ctor`/`rcp_qplan_hash` fire once from the ordinary plan-key lookup outside `rcp_staged`
  (same call-count signature as the smoke).

Consequently the partition closure gap (−7.4…+33.9 % of the parent delta) is explained by those artifacts
plus the recursive capture double-count; the literal `recipe_residual` row is unaffected (0.0 by construction).
The parent `planner_recipe` ratios themselves are unaffected: 1.1895 / 1.1474 / 1.0762 / 1.2632.

## Cost to fix — footprint split

Current edit footprint (`rv6/contract.json` `footprint`): `QueryCommand*.cs`, `QueryCommand.QueryPreparer.cs`,
`QueryCommand.Plan/Binding/Description/EagerLoading/Storage.cs`, `Builders/EntityBuilder.cs`, `Cte.cs`.

| sub-phase (file) | footprint |
|---|---|
| `rcp_capture` / `rcp_fingerprint` / `rcp_filter_scan` / `rcp_matches` (QueryCommand.QueryPreparer.cs) | **addressable** |
| `rcp_transfer` / `rcp_transfer_edges` / `rcp_discard` (QueryCommand.QueryPreparer.cs) | **addressable** |
| `rcp_copyto` / `rcp_copyto_members` (QueryCommand.Clone.cs) | **frozen** (outside footprint) |
| `rcp_lookup` (DataContext/Cache/QueryPlanStore.cs) | **frozen** (outside footprint) |
| `rcp_staged`, `rcp_filter_scan` call site (DataContext/QueryPlanner.cs) | **frozen** |
| `rcp_qplan_ctor` / `rcp_qplan_hash` (DataContext/Cache/QueryPlan.cs) | **frozen** |
| `rcp_equals1` / `rcp_equals2` (QueryPlanEqualityComparer.cs) | **frozen** |

Of the per-arm #1 contributors, `copyto_exclusive` (cte, leftjoin; QueryCommand.Clone.cs) and
`rcp_qplan_hash` (inlist; QueryPlan.cs) live in **frozen** files, while `capture_exclusive` (recursive;
QueryCommand.QueryPreparer.cs) is **addressable** but inflated by recursion accounting. `rcp_equals1`
and `staged_exclusive` (significant in leftjoin/recursive) are frozen too. No fix is selected in this row.

