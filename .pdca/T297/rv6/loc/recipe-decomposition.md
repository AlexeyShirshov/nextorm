# E38 recipe decomposition — T297 r=6 — P04 bounded sub-phase pass

Generated 2026-10-19T12:23:57Z by `.pdca/T297/rv3/tools/run_r4.py attribute`.

`planner_recipe` is the parent span; the partition terms are mutually exclusive and
cover it except for the un-timed remainder (`planner_recipe_exclusive`: eligibility gate plus
`CaptureRawGraph` of the graph root). Shares are of the paired `planner_recipe` delta, so they
are additive up to that remainder and the per-term median arithmetic shown as
`partition_minus_recipe_ns`.

Definitions:

- `capture_exclusive = rcp_capture − rcp_fingerprint`; `lookup_exclusive = rcp_lookup − rcp_matches`;
- `copyto_exclusive = rcp_copyto − rcp_copyto_members`; `qplan_ctor_exclusive = rcp_qplan_ctor − rcp_qplan_hash`;
- `transfer_exclusive = rcp_transfer − rcp_copyto`;
- `staged_exclusive = rcp_staged − (rcp_qplan_ctor + rcp_equals1 + rcp_equals2 + rcp_discard)`;
- `planner_recipe_exclusive = planner_recipe − (rcp_capture + rcp_filter_scan + rcp_lookup + rcp_transfer + rcp_staged)`;
- `recipe_residual = planner_recipe − (planner_recipe_exclusive + all nested terms added back)` — 0 by construction, reported separately.

## cte

`planner_recipe` A 50300.6 / B 58594.8 ns/op, paired ratio 1.1895 [1.0669, 1.2405], delta +9481.2 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| copyto_exclusive | 1552.3 | 5281.1 | +3808.4 | 3.4613 | [3.2770, 3.9430] | 1.00/1.00 | +40.2% |
| capture_exclusive | 22226.6 | 24132.9 | +2493.0 | 1.1121 | [0.9663, 1.1484] | 2.00/2.00 | +26.3% |
| rcp_matches | 16566.3 | 17435.6 | +1138.6 | 1.0687 | [0.9515, 1.1222] | 2.00/2.00 | +12.0% |
| rcp_fingerprint | 12623.9 | 13293.6 | +781.0 | 1.0619 | [0.9751, 1.1496] | 2.00/2.00 | +8.2% |
| rcp_equals1 | 2526.1 | 3161.0 | +631.1 | 1.2521 | [1.1963, 1.3184] | 1.00/1.00 | +6.7% |
| staged_exclusive | 1905.0 | 2490.9 | +616.1 | 1.3299 | [1.2145, 1.5081] | 1.00/1.00 | +6.5% |
| rcp_qplan_hash | 1517.2 | 1960.0 | +432.9 | 1.2941 | [1.1825, 1.5324] | 1.00/1.00 | +4.6% |
| rcp_copyto_members | 43.5 | 288.9 | +247.5 | 7.2226 | [5.9362, 7.6397] | 1.00/1.00 | +2.6% |
| transfer_exclusive | 236.5 | 465.8 | +218.1 | 1.9893 | [1.8261, 2.0494] | 1.00/1.00 | +2.3% |
| rcp_filter_scan | 3825.0 | 4128.5 | +166.8 | 1.0412 | [0.9880, 1.1425] | 1.00/1.00 | +1.8% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| lookup_exclusive | -1639.9 | -1565.7 | -10.5 | — | [—, —] | 1.00/1.00 | -0.1% |
| qplan_ctor_exclusive | -1450.1 | -1844.5 | -388.1 | — | [—, —] | 1.00/1.00 | -4.1% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 34818.6 | 37389.7 | +3274.0 | 1.0937 | [0.9694, 1.1488] | 2.00/2.00 |
| rcp_fingerprint | 12623.9 | 13293.6 | +781.0 | 1.0619 | [0.9751, 1.1496] | 2.00/2.00 |
| rcp_filter_scan | 3825.0 | 4128.5 | +166.8 | 1.0412 | [0.9880, 1.1425] | 1.00/1.00 |
| rcp_lookup | 14926.4 | 15869.9 | +1121.9 | 1.0735 | [0.9552, 1.1118] | 1.00/1.00 |
| rcp_matches | 16566.3 | 17435.6 | +1138.6 | 1.0687 | [0.9515, 1.1222] | 2.00/2.00 |
| rcp_transfer | 1813.5 | 6052.3 | +4297.7 | 3.3644 | [3.1686, 3.7443] | 1.00/1.00 |
| rcp_copyto | 1591.5 | 5597.4 | +4071.5 | 3.5497 | [3.3578, 4.0500] | 1.00/1.00 |
| rcp_copyto_members | 43.5 | 288.9 | +247.5 | 7.2226 | [5.9362, 7.6397] | 1.00/1.00 |
| rcp_transfer_edges | 142.3 | 370.4 | +213.5 | 2.5943 | [2.3308, 2.7988] | 1.00/1.00 |
| rcp_staged | 4492.5 | 5770.3 | +1288.9 | 1.2878 | [1.2130, 1.4039] | 1.00/1.00 |
| rcp_qplan_ctor | 67.1 | 107.3 | +44.8 | 1.6812 | [1.5710, 1.7796] | 1.00/1.00 |
| rcp_qplan_hash | 1517.2 | 1960.0 | +432.9 | 1.2941 | [1.1825, 1.5324] | 1.00/1.00 |
| rcp_equals1 | 2526.1 | 3161.0 | +631.1 | 1.2521 | [1.1963, 1.3184] | 1.00/1.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +10135.1 ns/op vs parent delta +9481.2 ns/op -> gap +653.8 ns/op (un-timed remainder `planner_recipe_exclusive` -646.0 ns/op, -6.8%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **copyto_exclusive** +3808.4 ns/op = 40.2 % of the +9481.2 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 3.2770 > 1): True; dominance=FAIL.

Top 3 positive contributors: copyto_exclusive +3808.4 ns (+40.2%); capture_exclusive +2493.0 ns (+26.3%); rcp_matches +1138.6 ns (+12.0%).

## recursive

`planner_recipe` A 57212.1 / B 65843.3 ns/op, paired ratio 1.1474 [1.1083, 1.1865], delta +8567.7 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| capture_exclusive | 60769.9 | 66003.4 | +5048.5 | 1.0811 | [1.0404, 1.1109] | 4.00/4.00 | +58.9% |
| copyto_exclusive | 1012.4 | 3531.8 | +2513.5 | 3.4405 | [3.2412, 3.6904] | 1.00/1.00 | +29.3% |
| rcp_fingerprint | 18515.2 | 19798.3 | +1128.5 | 1.0593 | [1.0236, 1.0928] | 4.00/4.00 | +13.2% |
| rcp_matches | 20446.1 | 21557.0 | +1050.8 | 1.0496 | [1.0196, 1.1093] | 3.00/3.00 | +12.3% |
| staged_exclusive | 1746.1 | 2395.3 | +636.3 | 1.3614 | [1.3121, 1.4707] | 1.00/1.00 | +7.4% |
| rcp_equals1 | 2560.3 | 3159.1 | +598.9 | 1.2343 | [1.1926, 1.3163] | 1.00/1.00 | +7.0% |
| rcp_qplan_hash | 1465.7 | 1885.3 | +396.5 | 1.2663 | [1.2582, 1.4110] | 1.00/1.00 | +4.6% |
| rcp_copyto_members | 88.7 | 448.5 | +359.8 | 5.0312 | [4.8893, 5.2041] | 2.00/2.00 | +4.2% |
| transfer_exclusive | 235.8 | 470.2 | +242.5 | 2.0414 | [1.8394, 2.2264] | 1.00/1.00 | +2.8% |
| rcp_filter_scan | 2050.8 | 2158.3 | +140.1 | 1.0693 | [0.9855, 1.1181] | 1.00/1.00 | +1.6% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| lookup_exclusive | -7505.1 | -7981.4 | -283.8 | — | [—, —] | 1.00/1.00 | -3.3% |
| qplan_ctor_exclusive | -1404.3 | -1778.0 | -357.2 | — | [—, —] | 1.00/1.00 | -4.2% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 79246.8 | 85801.7 | +5990.1 | 1.0737 | [1.0387, 1.1067] | 4.00/4.00 |
| rcp_fingerprint | 18515.2 | 19798.3 | +1128.5 | 1.0593 | [1.0236, 1.0928] | 4.00/4.00 |
| rcp_filter_scan | 2050.8 | 2158.3 | +140.1 | 1.0693 | [0.9855, 1.1181] | 1.00/1.00 |
| rcp_lookup | 12941.1 | 13659.1 | +718.2 | 1.0555 | [1.0247, 1.1077] | 1.00/1.00 |
| rcp_matches | 20446.1 | 21557.0 | +1050.8 | 1.0496 | [1.0196, 1.1093] | 3.00/3.00 |
| rcp_transfer | 1349.1 | 4465.4 | +3095.2 | 3.2752 | [3.1374, 3.5226] | 1.00/1.00 |
| rcp_copyto | 1103.7 | 3986.7 | +2862.1 | 3.5637 | [3.3726, 3.8178] | 1.00/1.00 |
| rcp_copyto_members | 88.7 | 448.5 | +359.8 | 5.0312 | [4.8893, 5.2041] | 2.00/2.00 |
| rcp_transfer_edges | 143.3 | 377.9 | +241.5 | 2.7175 | [2.3585, 3.0564] | 1.00/1.00 |
| rcp_staged | 4322.0 | 5692.0 | +1344.4 | 1.3133 | [1.2572, 1.3505] | 1.00/1.00 |
| rcp_qplan_ctor | 62.9 | 108.2 | +44.6 | 1.7011 | [1.5521, 1.8672] | 1.00/1.00 |
| rcp_qplan_hash | 1465.7 | 1885.3 | +396.5 | 1.2663 | [1.2582, 1.4110] | 1.00/1.00 |
| rcp_equals1 | 2560.3 | 3159.1 | +598.9 | 1.2343 | [1.1926, 1.3163] | 1.00/1.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +11474.3 ns/op vs parent delta +8567.7 ns/op -> gap +2906.5 ns/op (un-timed remainder `planner_recipe_exclusive` -2790.5 ns/op, -32.6%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **capture_exclusive** +5048.5 ns/op = 58.9 % of the +8567.7 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 1.0404 > 1): True; dominance=FAIL.

Top 3 positive contributors: capture_exclusive +5048.5 ns (+58.9%); copyto_exclusive +2513.5 ns (+29.3%); rcp_fingerprint +1128.5 ns (+13.2%).

## inlist

`planner_recipe` A 13320.0 / B 14279.6 ns/op, paired ratio 1.0762 [0.9316, 1.1080], delta +1027.2 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| rcp_qplan_hash | 1632.8 | 2377.3 | +790.3 | 1.4986 | [1.3868, 1.6113] | 1.00/1.00 | +76.9% |
| rcp_fingerprint | 6850.2 | 7333.7 | +531.0 | 1.0771 | [0.9655, 1.1070] | 1.00/1.00 | +51.7% |
| capture_exclusive | 3996.6 | 4270.7 | +323.9 | 1.0820 | [0.8410, 1.1411] | 1.00/1.00 | +31.5% |
| rcp_filter_scan | 1467.2 | 1567.9 | +133.1 | 1.0913 | [0.9466, 1.1359] | 1.00/1.00 | +13.0% |
| rcp_matches | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| transfer_exclusive | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| copyto_exclusive | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_copyto_members | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_equals1 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| lookup_exclusive | 583.6 | 548.3 | -56.6 | 0.9044 | [0.8739, 0.9829] | 1.00/1.00 | -5.5% |
| staged_exclusive | -572.9 | -845.1 | -256.1 | — | [—, —] | 0.00/0.00 | -24.9% |
| qplan_ctor_exclusive | -1058.0 | -1541.1 | -514.4 | — | [—, —] | 1.00/1.00 | -50.1% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 10823.3 | 11601.2 | +860.2 | 1.0799 | [0.9133, 1.1143] | 1.00/1.00 |
| rcp_fingerprint | 6850.2 | 7333.7 | +531.0 | 1.0771 | [0.9655, 1.1070] | 1.00/1.00 |
| rcp_filter_scan | 1467.2 | 1567.9 | +133.1 | 1.0913 | [0.9466, 1.1359] | 1.00/1.00 |
| rcp_lookup | 583.6 | 548.3 | -56.6 | 0.9044 | [0.8739, 0.9829] | 1.00/1.00 |
| rcp_matches | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_transfer | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_copyto | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_copyto_members | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_transfer_edges | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_staged | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_qplan_ctor | 572.9 | 845.1 | +256.1 | 1.4383 | [1.3651, 1.5623] | 1.00/1.00 |
| rcp_qplan_hash | 1632.8 | 2377.3 | +790.3 | 1.4986 | [1.3868, 1.6113] | 1.00/1.00 |
| rcp_equals1 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +951.2 ns/op vs parent delta +1027.2 ns/op -> gap -76.0 ns/op (un-timed remainder `planner_recipe_exclusive` +70.9 ns/op, +6.9%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **rcp_qplan_hash** +790.3 ns/op = 76.9 % of the +1027.2 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 1.3868 > 1): True; dominance=FAIL.

Top 3 positive contributors: rcp_qplan_hash +790.3 ns (+76.9%); rcp_fingerprint +531.0 ns (+51.7%); capture_exclusive +323.9 ns (+31.5%).

## leftjoin

`planner_recipe` A 29178.1 / B 36753.8 ns/op, paired ratio 1.2632 [1.1431, 1.2933], delta +7579.6 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| copyto_exclusive | 1448.6 | 4836.3 | +3388.8 | 3.4796 | [3.1865, 3.6541] | 1.00/1.00 | +44.7% |
| capture_exclusive | 5376.8 | 6088.2 | +697.5 | 1.1303 | [1.0154, 1.1991] | 1.00/1.00 | +9.2% |
| rcp_equals1 | 2434.3 | 3076.7 | +672.4 | 1.2776 | [1.1037, 1.3502] | 1.00/1.00 | +8.9% |
| rcp_matches | 7659.5 | 8302.9 | +652.8 | 1.0860 | [0.9881, 1.1085] | 1.00/1.00 | +8.6% |
| rcp_fingerprint | 5568.2 | 6152.6 | +616.1 | 1.1087 | [1.0312, 1.1397] | 1.00/1.00 | +8.1% |
| staged_exclusive | 1728.9 | 2248.4 | +513.7 | 1.3172 | [1.1510, 1.5285] | 1.00/1.00 | +6.8% |
| rcp_qplan_hash | 1450.7 | 1811.0 | +370.9 | 1.2687 | [1.0992, 1.4576] | 1.00/1.00 | +4.9% |
| transfer_exclusive | 226.0 | 452.8 | +237.2 | 2.0813 | [1.8932, 2.2360] | 1.00/1.00 | +3.1% |
| rcp_filter_scan | 2868.4 | 3019.7 | +150.6 | 1.0516 | [0.9063, 1.0898] | 1.00/1.00 | +2.0% |
| rcp_copyto_members | 43.2 | 188.0 | +143.5 | 4.4498 | [3.6692, 5.5078] | 1.00/1.00 | +1.9% |
| lookup_exclusive | 1517.7 | 1585.3 | +75.2 | 1.0522 | [1.0163, 1.1401] | 1.00/1.00 | +1.0% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| qplan_ctor_exclusive | -1393.5 | -1708.4 | -335.5 | — | [—, —] | 1.00/1.00 | -4.4% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 10945.0 | 12224.9 | +1337.8 | 1.1238 | [1.0233, 1.1639] | 1.00/1.00 |
| rcp_fingerprint | 5568.2 | 6152.6 | +616.1 | 1.1087 | [1.0312, 1.1397] | 1.00/1.00 |
| rcp_filter_scan | 2868.4 | 3019.7 | +150.6 | 1.0516 | [0.9063, 1.0898] | 1.00/1.00 |
| rcp_lookup | 9110.2 | 9869.0 | +817.0 | 1.0876 | [0.9927, 1.1056] | 1.00/1.00 |
| rcp_matches | 7659.5 | 8302.9 | +652.8 | 1.0860 | [0.9881, 1.1085] | 1.00/1.00 |
| rcp_transfer | 1702.7 | 5463.2 | +3735.1 | 3.2975 | [3.0771, 3.4809] | 1.00/1.00 |
| rcp_copyto | 1487.3 | 5013.8 | +3518.9 | 3.5055 | [3.2225, 3.6785] | 1.00/1.00 |
| rcp_copyto_members | 43.2 | 188.0 | +143.5 | 4.4498 | [3.6692, 5.5078] | 1.00/1.00 |
| rcp_transfer_edges | 131.6 | 357.4 | +230.9 | 2.7974 | [2.5089, 3.0655] | 1.00/1.00 |
| rcp_staged | 4169.4 | 5388.6 | +1282.0 | 1.3137 | [1.1385, 1.3928] | 1.00/1.00 |
| rcp_qplan_ctor | 61.9 | 104.9 | +39.5 | 1.6034 | [1.5654, 1.9214] | 1.00/1.00 |
| rcp_qplan_hash | 1450.7 | 1811.0 | +370.9 | 1.2687 | [1.0992, 1.4576] | 1.00/1.00 |
| rcp_equals1 | 2434.3 | 3076.7 | +672.4 | 1.2776 | [1.1037, 1.3502] | 1.00/1.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +7183.4 ns/op vs parent delta +7579.6 ns/op -> gap -396.2 ns/op (un-timed remainder `planner_recipe_exclusive` +146.4 ns/op, +1.9%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **copyto_exclusive** +3388.8 ns/op = 44.7 % of the +7579.6 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 3.1865 > 1): True; dominance=FAIL.

Top 3 positive contributors: copyto_exclusive +3388.8 ns (+44.7%); capture_exclusive +697.5 ns (+9.2%); rcp_equals1 +672.4 ns (+8.9%).

