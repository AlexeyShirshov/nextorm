# E38 recipe decomposition — T297 r=6 — P04 bounded sub-phase pass

Generated 2026-10-19T12:45:16Z by `.pdca/T297/rv3/tools/run_r4.py attribute`.

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

`planner_recipe` A 50576.4 / B 57076.2 ns/op, paired ratio 1.1285 [1.0737, 1.2063], delta +6499.8 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| transfer_exclusive | 238.8 | 3372.3 | +3154.8 | 14.4640 | [13.5187, 15.5275] | 1.00/1.00 | +48.5% |
| capture_exclusive | 22533.1 | 25471.0 | +2937.9 | 1.1304 | [1.0425, 1.1974] | 2.00/2.00 | +45.2% |
| rcp_fingerprint | 12373.2 | 13868.9 | +1411.3 | 1.1134 | [1.0587, 1.1947] | 2.00/2.00 | +21.7% |
| staged_exclusive | 1832.0 | 2711.9 | +924.9 | 1.5108 | [1.4156, 1.5662] | 1.00/1.00 | +14.2% |
| rcp_qplan_hash | 1510.2 | 2156.9 | +671.7 | 1.4638 | [1.3969, 1.5193] | 1.00/1.00 | +10.3% |
| rcp_equals1 | 2547.4 | 3153.2 | +622.8 | 1.2409 | [1.1667, 1.2938] | 1.00/1.00 | +9.6% |
| rcp_matches | 16560.1 | 16970.6 | +398.0 | 1.0240 | [0.9720, 1.1045] | 2.00/2.00 | +6.1% |
| lookup_exclusive | -1678.6 | -1480.6 | +151.5 | — | [—, —] | 1.00/1.00 | +2.3% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_filter_scan | 4007.0 | 3968.9 | -40.7 | 0.9897 | [0.9642, 1.1303] | 1.00/1.00 | -0.6% |
| rcp_copyto_members | 41.7 | 0.0 | -41.7 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 | -0.6% |
| qplan_ctor_exclusive | -1440.7 | -2024.8 | -615.2 | — | [—, —] | 1.00/1.00 | -9.5% |
| copyto_exclusive | 1513.8 | 0.0 | -1513.8 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 | -23.3% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 34990.7 | 39339.9 | +4349.2 | 1.1243 | [1.0482, 1.1964] | 2.00/2.00 |
| rcp_fingerprint | 12373.2 | 13868.9 | +1411.3 | 1.1134 | [1.0587, 1.1947] | 2.00/2.00 |
| rcp_filter_scan | 4007.0 | 3968.9 | -40.7 | 0.9897 | [0.9642, 1.1303] | 1.00/1.00 |
| rcp_lookup | 15016.5 | 15500.0 | +483.5 | 1.0321 | [0.9917, 1.1039] | 1.00/1.00 |
| rcp_matches | 16560.1 | 16970.6 | +398.0 | 1.0240 | [0.9720, 1.1045] | 2.00/2.00 |
| rcp_transfer | 1794.2 | 3372.3 | +1547.4 | 1.8392 | [1.7964, 1.9721] | 1.00/1.00 |
| rcp_copyto | 1558.1 | 0.0 | -1558.1 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 |
| rcp_copyto_members | 41.7 | 0.0 | -41.7 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 |
| rcp_transfer_edges | 141.2 | 3163.4 | +3032.2 | 22.5266 | [21.1568, 25.7709] | 1.00/1.00 |
| rcp_staged | 4430.1 | 5986.6 | +1556.5 | 1.3592 | [1.2858, 1.4097] | 1.00/1.00 |
| rcp_qplan_ctor | 66.8 | 132.1 | +61.8 | 1.8802 | [1.6335, 2.2311] | 1.00/1.00 |
| rcp_qplan_hash | 1510.2 | 2156.9 | +671.7 | 1.4638 | [1.3969, 1.5193] | 1.00/1.00 |
| rcp_equals1 | 2547.4 | 3153.2 | +622.8 | 1.2409 | [1.1667, 1.2938] | 1.00/1.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +8061.5 ns/op vs parent delta +6499.8 ns/op -> gap +1561.7 ns/op (un-timed remainder `planner_recipe_exclusive` -1368.7 ns/op, -21.1%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **transfer_exclusive** +3154.8 ns/op = 48.5 % of the +6499.8 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 13.5187 > 1): True; dominance=FAIL.

Top 3 positive contributors: transfer_exclusive +3154.8 ns (+48.5%); capture_exclusive +2937.9 ns (+45.2%); rcp_fingerprint +1411.3 ns (+21.7%).

## recursive

`planner_recipe` A 57223.4 / B 64266.7 ns/op, paired ratio 1.0780 [1.0500, 1.1563], delta +4555.7 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| capture_exclusive | 60504.1 | 65501.5 | +2901.1 | 1.0479 | [1.0092, 1.1079] | 4.00/4.00 | +63.7% |
| transfer_exclusive | 242.3 | 2905.4 | +2658.6 | 12.5059 | [11.1129, 13.2716] | 1.00/1.00 | +58.4% |
| staged_exclusive | 1777.0 | 2388.1 | +660.2 | 1.3316 | [1.2285, 1.4451] | 1.00/1.00 | +14.5% |
| rcp_fingerprint | 18240.7 | 19633.6 | +645.4 | 1.0355 | [0.9969, 1.1072] | 4.00/4.00 | +14.2% |
| rcp_equals1 | 2596.8 | 3119.5 | +630.9 | 1.2385 | [1.1573, 1.2997] | 1.00/1.00 | +13.8% |
| rcp_matches | 20665.2 | 22107.7 | +521.5 | 1.0248 | [0.9893, 1.0860] | 3.00/3.00 | +11.4% |
| rcp_qplan_hash | 1487.0 | 1942.2 | +489.8 | 1.2939 | [1.1948, 1.3970] | 1.00/1.00 | +10.8% |
| rcp_copyto_members | 91.4 | 222.9 | +123.4 | 2.3666 | [1.8328, 2.8076] | 2.00/1.00 | +2.7% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_filter_scan | 2085.7 | 2109.3 | -5.5 | 0.9980 | [0.9472, 1.0662] | 1.00/1.00 | -0.1% |
| lookup_exclusive | -7715.2 | -8046.4 | -116.8 | — | [—, —] | 1.00/1.00 | -2.6% |
| qplan_ctor_exclusive | -1424.6 | -1832.6 | -445.3 | — | [—, —] | 1.00/1.00 | -9.8% |
| copyto_exclusive | 1031.8 | -222.9 | -1243.1 | -0.2071 | [-0.2371, -0.1862] | 1.00/0.00 | -27.3% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 78744.8 | 85135.1 | +3546.5 | 1.0450 | [1.0063, 1.1077] | 4.00/4.00 |
| rcp_fingerprint | 18240.7 | 19633.6 | +645.4 | 1.0355 | [0.9969, 1.1072] | 4.00/4.00 |
| rcp_filter_scan | 2085.7 | 2109.3 | -5.5 | 0.9980 | [0.9472, 1.0662] | 1.00/1.00 |
| rcp_lookup | 12950.0 | 14017.1 | +452.1 | 1.0354 | [1.0124, 1.1022] | 1.00/1.00 |
| rcp_matches | 20665.2 | 22107.7 | +521.5 | 1.0248 | [0.9893, 1.0860] | 3.00/3.00 |
| rcp_transfer | 1364.4 | 2905.4 | +1544.7 | 2.1082 | [1.9346, 2.2692] | 1.00/1.00 |
| rcp_copyto | 1122.1 | 0.0 | -1122.1 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 |
| rcp_copyto_members | 91.4 | 222.9 | +123.4 | 2.3666 | [1.8328, 2.8076] | 2.00/1.00 |
| rcp_transfer_edges | 148.3 | 2743.4 | +2592.9 | 19.8325 | [16.6232, 22.2950] | 1.00/1.00 |
| rcp_staged | 4381.7 | 5608.6 | +1316.7 | 1.2768 | [1.1967, 1.3639] | 1.00/1.00 |
| rcp_qplan_ctor | 61.8 | 101.0 | +39.5 | 1.6134 | [1.5682, 1.7480] | 1.00/1.00 |
| rcp_qplan_hash | 1487.0 | 1942.2 | +489.8 | 1.2939 | [1.1948, 1.3970] | 1.00/1.00 |
| rcp_equals1 | 2596.8 | 3119.5 | +630.9 | 1.2385 | [1.1573, 1.2997] | 1.00/1.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +6820.3 ns/op vs parent delta +4555.7 ns/op -> gap +2264.5 ns/op (un-timed remainder `planner_recipe_exclusive` -1901.4 ns/op, -41.7%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **capture_exclusive** +2901.1 ns/op = 63.7 % of the +4555.7 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 1.0092 > 1): True; dominance=FAIL.

Top 3 positive contributors: capture_exclusive +2901.1 ns (+63.7%); transfer_exclusive +2658.6 ns (+58.4%); staged_exclusive +660.2 ns (+14.5%).

## inlist

`planner_recipe` A 13337.6 / B 13815.8 ns/op, paired ratio 1.0312 [0.9328, 1.1302], delta +414.7 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| rcp_qplan_hash | 1643.2 | 2470.1 | +735.3 | 1.4560 | [1.3076, 1.6004] | 1.00/1.00 | +177.3% |
| capture_exclusive | 3867.3 | 4226.7 | +243.5 | 1.0637 | [0.9615, 1.1918] | 1.00/1.00 | +58.7% |
| lookup_exclusive | 532.3 | 565.7 | +43.4 | 1.0830 | [0.8471, 1.1525] | 1.00/1.00 | +10.5% |
| rcp_filter_scan | 1517.8 | 1581.5 | +21.9 | 1.0148 | [0.9333, 1.1109] | 1.00/1.00 | +5.3% |
| rcp_matches | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| transfer_exclusive | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| copyto_exclusive | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_copyto_members | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_equals1 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_fingerprint | 7072.2 | 7121.5 | -24.8 | 0.9966 | [0.9142, 1.0911] | 1.00/1.00 | -6.0% |
| staged_exclusive | -598.7 | -789.5 | -230.8 | — | [—, —] | 0.00/0.00 | -55.7% |
| qplan_ctor_exclusive | -1057.6 | -1671.5 | -544.4 | — | [—, —] | 1.00/1.00 | -131.3% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 10877.9 | 11256.9 | +243.6 | 1.0225 | [0.9313, 1.1237] | 1.00/1.00 |
| rcp_fingerprint | 7072.2 | 7121.5 | -24.8 | 0.9966 | [0.9142, 1.0911] | 1.00/1.00 |
| rcp_filter_scan | 1517.8 | 1581.5 | +21.9 | 1.0148 | [0.9333, 1.1109] | 1.00/1.00 |
| rcp_lookup | 532.3 | 565.7 | +43.4 | 1.0830 | [0.8471, 1.1525] | 1.00/1.00 |
| rcp_matches | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_transfer | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_copyto | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_copyto_members | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_transfer_edges | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_staged | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_qplan_ctor | 598.7 | 789.5 | +230.8 | 1.3707 | [1.2203, 1.4636] | 1.00/1.00 |
| rcp_qplan_hash | 1643.2 | 2470.1 | +735.3 | 1.4560 | [1.3076, 1.6004] | 1.00/1.00 |
| rcp_equals1 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +244.1 ns/op vs parent delta +414.7 ns/op -> gap -170.6 ns/op (un-timed remainder `planner_recipe_exclusive` +61.3 ns/op, +14.8%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **rcp_qplan_hash** +735.3 ns/op = 177.3 % of the +414.7 ns/op `planner_recipe` delta; share>=80 %: True; paired 95 % CI excludes zero (ci_low 1.3076 > 1): True; dominance=PASS.

Top 3 positive contributors: rcp_qplan_hash +735.3 ns (+177.3%); capture_exclusive +243.5 ns (+58.7%); lookup_exclusive +43.4 ns (+10.5%).

## leftjoin

`planner_recipe` A 29269.9 / B 34236.2 ns/op, paired ratio 1.1598 [1.1191, 1.1810], delta +4676.0 ns/op, calls/op 1.00/1.00.

| term | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B | share of recipe delta |
|---|---|---|---|---|---|---|---|
| transfer_exclusive | 216.4 | 3112.5 | +2864.5 | 14.5598 | [12.6815, 15.9809] | 1.00/1.00 | +61.3% |
| capture_exclusive | 5327.1 | 6084.0 | +705.8 | 1.1348 | [1.1038, 1.1600] | 1.00/1.00 | +15.1% |
| rcp_equals1 | 2421.3 | 3082.8 | +641.6 | 1.2659 | [1.2091, 1.3088] | 1.00/1.00 | +13.7% |
| staged_exclusive | 1694.6 | 2362.5 | +636.2 | 1.3877 | [1.2730, 1.5063] | 1.00/1.00 | +13.6% |
| rcp_fingerprint | 5472.1 | 6106.2 | +537.1 | 1.0985 | [1.0258, 1.1309] | 1.00/1.00 | +11.5% |
| rcp_qplan_hash | 1407.4 | 1931.3 | +505.8 | 1.3710 | [1.2790, 1.4823] | 1.00/1.00 | +10.8% |
| rcp_matches | 7772.2 | 8273.1 | +320.1 | 1.0415 | [1.0056, 1.0697] | 1.00/1.00 | +6.8% |
| rcp_filter_scan | 2972.7 | 3032.4 | +94.4 | 1.0308 | [0.9841, 1.0531] | 1.00/1.00 | +2.0% |
| lookup_exclusive | 1520.1 | 1538.4 | +9.2 | 1.0061 | [0.9782, 1.0897] | 1.00/1.00 | +0.2% |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 | +0.0% |
| rcp_copyto_members | 42.0 | 0.0 | -42.0 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 | -0.9% |
| qplan_ctor_exclusive | -1346.1 | -1798.5 | -461.4 | — | [—, —] | 1.00/1.00 | -9.9% |
| copyto_exclusive | 1388.3 | 0.0 | -1388.3 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 | -29.7% |

Inclusive spans as measured (nested leaves are inside their parent):

| inclusive span | A ns/op | B ns/op | delta ns/op | ratio | 95% CI | calls/op A/B |
|---|---|---|---|---|---|---|
| rcp_capture | 10771.2 | 12108.4 | +1297.0 | 1.1211 | [1.0638, 1.1403] | 1.00/1.00 |
| rcp_fingerprint | 5472.1 | 6106.2 | +537.1 | 1.0985 | [1.0258, 1.1309] | 1.00/1.00 |
| rcp_filter_scan | 2972.7 | 3032.4 | +94.4 | 1.0308 | [0.9841, 1.0531] | 1.00/1.00 |
| rcp_lookup | 9252.9 | 9793.8 | +365.2 | 1.0395 | [1.0016, 1.0686] | 1.00/1.00 |
| rcp_matches | 7772.2 | 8273.1 | +320.1 | 1.0415 | [1.0056, 1.0697] | 1.00/1.00 |
| rcp_transfer | 1631.0 | 3112.5 | +1446.7 | 1.8675 | [1.8034, 1.9504] | 1.00/1.00 |
| rcp_copyto | 1434.0 | 0.0 | -1434.0 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 |
| rcp_copyto_members | 42.0 | 0.0 | -42.0 | 0.0000 | [0.0000, 0.0000] | 1.00/0.00 |
| rcp_transfer_edges | 123.6 | 2902.0 | +2779.1 | 24.0725 | [21.6322, 29.4030] | 1.00/1.00 |
| rcp_staged | 4177.1 | 5660.2 | +1331.2 | 1.3196 | [1.2583, 1.3920] | 1.00/1.00 |
| rcp_qplan_ctor | 59.3 | 117.2 | +55.9 | 1.9119 | [1.7662, 2.4497] | 1.00/1.00 |
| rcp_qplan_hash | 1407.4 | 1931.3 | +505.8 | 1.3710 | [1.2790, 1.4823] | 1.00/1.00 |
| rcp_equals1 | 2421.3 | 3082.8 | +641.6 | 1.2659 | [1.2091, 1.3088] | 1.00/1.00 |
| rcp_equals2 | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |
| rcp_discard | 0.0 | 0.0 | +0.0 | — | [—, —] | 0.00/0.00 |

Partition sum delta +4422.9 ns/op vs parent delta +4676.0 ns/op -> gap -253.1 ns/op (un-timed remainder `planner_recipe_exclusive` +156.4 ns/op, +3.3%; the gap is that remainder up to per-round median arithmetic). `recipe_residual` delta +0.0 ns/op (algebraic check, 0 by construction).

Dominance (80 % test): top positive contributor **transfer_exclusive** +2864.5 ns/op = 61.3 % of the +4676.0 ns/op `planner_recipe` delta; share>=80 %: False; paired 95 % CI excludes zero (ci_low 12.6815 > 1): True; dominance=FAIL.

Top 3 positive contributors: transfer_exclusive +2864.5 ns (+61.3%); capture_exclusive +705.8 ns (+15.1%); rcp_equals1 +641.6 ns (+13.7%).

