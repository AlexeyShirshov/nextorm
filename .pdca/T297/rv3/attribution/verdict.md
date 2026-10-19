# E22 attribution verdict — T297 r=4

Pre-registered rule (written in the runner before execution):

A phase is named as the cause only if **all** hold, in **every** arm:

1. (a) its paired B/A ratio median > 1 with bootstrap 95 % CI lower bound > 1;
2. (b) it reproduces in both ABBA pair directions — at least 2 of 3 A→B pairs > 1 and at least 2 of 3 B→A pairs > 1, and at least 5 of 6 pairs > 1 (per-round signs are in phase-table.md);
3. (c) its recorded boundary `file:line` resolves (Roslyn table) and its call count is non-zero in both trees;
4. (d) every measured exclusive phase reconciles with the e2e delta: the full phase sum (six exclusive phases plus the nested plan/bind/enum_create/cow_shape added back) differs from the e2e delta by no more than the combined 95 % CI half-width (quadrature);
5. (e) the unexplained positive residual is insignificant: `residual_unattributed` ≤ 0 or ≤ 5 % of the positive e2e delta.

Amendment recorded before the verdict was issued: the dispatch's six-term `e2e_phases` omits exactly the
nested phases that its own `prepare_exclusive` definition subtracted, so the six-term residual contains
measured work by construction. The full phase sum adds them back; the six-term residual is still reported
as `phase_residual`. No threshold in (a), (b), (c) or (e) changed after the run.

Otherwise the verdict is `attribution: incomplete` with the numbers, and no cause is named.

## cte

e2e delta +10257.9 ns/op; full phase sum +10244.0; unattributed residual +13.6 (+0.1%); dispatch six-term residual +446.0 (+4.3%); combined CI hw 2010.9 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | True | True | True | False |
| get_db_command | False | False | True | True | True | False |
| execute | True | True | True | True | True | True |
| read_rows | False | False | True | True | True | False |
| map_rows | False | False | True | True | True | False |

## recursive

e2e delta +12615.0 ns/op; full phase sum +12541.1; unattributed residual +108.4 (+0.9%); dispatch six-term residual +522.7 (+4.1%); combined CI hw 2121.0 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | True | True | True | False |
| get_db_command | False | False | True | True | True | False |
| execute | True | True | True | True | True | True |
| read_rows | False | True | True | True | True | False |
| map_rows | False | True | True | True | True | False |

## inlist

e2e delta +7331.5 ns/op; full phase sum +7385.7; unattributed residual -1.5 (-0.0%); dispatch six-term residual +2285.6 (+31.2%); combined CI hw 2801.2 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | False | False | True | True | True | False |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | True | True | True | True | True | True |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | False | True | True | False |
| get_db_command | True | True | True | True | True | True |
| execute | False | True | True | True | True | False |
| read_rows | False | False | True | True | True | False |
| map_rows | False | False | True | True | True | False |

## leftjoin

e2e delta +10325.9 ns/op; full phase sum +10165.1; unattributed residual +115.1 (+1.1%); dispatch six-term residual +636.1 (+6.2%); combined CI hw 1920.8 ns; reconciles=True; residual insignificant=True.

| phase | (a) pos+CI | (b) both dirs | (c) boundary+calls | (d) reconciles | (e) residual | pass |
|---|---|---|---|---|---|---|
| construct | True | True | True | True | True | True |
| prepare_exclusive | True | True | True | True | True | True |
| plan | True | True | True | True | True | True |
| bind | False | False | False | True | True | False |
| enum_create | False | False | False | True | True | False |
| cow_shape | False | False | True | True | True | False |
| get_db_command | False | True | True | True | True | False |
| execute | True | True | True | True | True | True |
| read_rows | True | True | True | True | True | True |
| map_rows | True | True | True | True | True | True |

**attribution: prepare_exclusive, plan** — satisfies (a)-(e) in all four arms.
