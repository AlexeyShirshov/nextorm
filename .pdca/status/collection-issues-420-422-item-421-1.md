# Cycle status: collection-issues-420-422-item-421 / cycle 1

## Current state

- task: `collection-issues-420-422-item-421` · form: `cycle` · specialized skill: `pdca-dotnet` · overlay: `coder+.NET` (`pdca-coder` + `pdca-dotnet`) + `nextorm-pdca`
- mode: `autonomous`
- cycle: `N=1` · revision: `r=1` · attempt: `n=2` (2/3)
- scope: `whole #421` · status path: `.pdca/status/collection-issues-420-422-item-421-1.md`
- goal: общий типизированный модификатор `WITH ORDINALITY` для поддерживаемых TVF PostgreSQL и его описание в руководстве EN/RU — §PLAN record and §Original task are authoritative.
- phase: `ACT` · record: `completed` · outcome: `accepted` · item: `done` · disposition: `paused` · plan_state: `ready` · preparation_state: `not applicable` (bounded cycle)
- source: `.pdca/status/collection-issues-420-422.md` §§ `Collection input` (#421), `Constraints` · evidence root: `.pdca/collection-issues-420-422-item-421/rv1/`
- criteria: `421-AC1`–`421-AC5` — exact text in §PLAN record `r1`, sourced from §Original task.
- Next: #422 saved-PLAN applicability preflight on the new HEAD; not started.

## Original task

`{"path":".pdca/collection-issues-420-422/rv1/issue-421.md","sha256":"d7873fb642447ece1bcdf319cec3b1146771812d70439dfe83c36b6daa55f296"}` — complete original issue body and acceptance criteria.

## PLAN record

### r1

`{"path":".pdca/collection-issues-420-422-item-421/rv1/report-ebd9e5c2249a8c96d0f42530ef75091848bb2378f1a97d763e88732d86c5a1c3.md","sha256":"ebd9e5c2249a8c96d0f42530ef75091848bb2378f1a97d763e88732d86c5a1c3"}`

Scheduling: see `Метаданные для планирования коллекции` in the pinned PLAN for footprint and prerequisites. No #420/#422 dependency is stated; exact overlaps are unknown. No group, branch or lane assigned by this item PLAN.

### Saved-PLAN applicability preflight

- Scoped `pdca-scout` factual report: `{"path":".pdca/collection-issues-420-422-item-421/rv1/report-2beb553c8a8a65588414b46fc5e2a943db7ed4697de08ea2724eb7c5a95572fb.md","sha256":"2beb553c8a8a65588414b46fc5e2a943db7ed4697de08ea2724eb7c5a95572fb"}`.
- `pdca-planner` decision: saved r1 remains applicable; no revision. `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-393a36f0257e2c060972a0ce784b2523509a417c293fd535d1c4338e6d49c1b2.md", "sha256": "393a36f0257e2c060972a0ce784b2523509a417c293fd535d1c4338e6d49c1b2"}`.

## Approval record

`not applicable: autonomous mode`

## DO result

Implemented candidate, no verdict. Exact DO source `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-a962ec32064684001bd22ccc5f6e4201faf0febc08e88e9a4707605aae147020.md", "sha256": "a962ec32064684001bd22ccc5f6e4201faf0febc08e88e9a4707605aae147020"}`.

- CHECK packet `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-36f7396d19c318f7b483bfd11a45cf6a2deb1aa50f7f0b3769f7ad7c64712b7d.md", "sha256": "36f7396d19c318f7b483bfd11a45cf6a2deb1aa50f7f0b3769f7ad7c64712b7d"}`.
- Proof gap: private wslc PostgreSQL endpoint not verified; 8 native IDs discovered, none executed. See the complete DO result and CHECK packet for AC1–AC5 separately.

## CHECK record

Independent `pdca-check` report (`ses_de750fe56ffeyPxfH61WHKVMIx`): `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189.md", "sha256": "994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189"}`.

Previous CHECK (historical): Verdict: `not accepted`; `421-AC1`, `421-AC2`, `421-AC3`, `421-AC4`, `421-AC5` are all `unverified`. PostgreSQL live: 8 discovered, 0 executed. AC5 additionally lacks direct EN/RU wording observation. No product defect established; CHECK → DO for evidence only.

Prior independent `pdca-check` report (`ses_de7475dd5ffev37ySX5dWvXkzb`): `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-96e0dfa887535ebff885e27443b57b036122969c95840bd08b27b28681dab079.md", "sha256": "96e0dfa887535ebff885e27443b57b036122969c95840bd08b27b28681dab079"}`.

Prior CHECK (before authenticated live assessment): `not accepted`; independent CHECK dispatch pending on the same candidate. `421-AC1`, `421-AC2`, `421-AC3`, `421-AC4`: `unverified` (latest signed CHECK predated the following live re-gather); `421-AC5`: `met` (candidate-bound EN/RU observation). Same candidate: `attempts/regather-check1/identity.log:1–14` confirms 12 files and the integration build match. No CHECK exit to DO, PLAN or ACT; counters and defect history unchanged.

Последний дособор без пароля: сервер ответил `AuthenticationRequest` method 10 (SASL), SQLSTATE не прислан; сессия и тесты не запускались. См. `.pdca/collection-issues-420-422-item-421/rv1/attempts/regather-passwordless-vU1jjn/result.md`.

Completed route diagnosis: `.pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-auth-source/result.md`. Windows `wslc` is the container-management context; PostgreSQL is a separate TCP publication on port 5433, which the WSL test worker reached. `NEXTORM_POSTGRES_CONNECTION` is unset in WSL and Windows Process/User/Machine scopes and is not listed in `WSLENV`. Presence-only checks of local `.env`, PostgreSQL client config and the known-name `.wslcconfig` found no authorized credential source. No authenticated query or tests ran. The remaining gap is authorized fixture injection and authenticated verification from the WSL test worker, not TCP reachability or an established product defect.

Latest live evidence (not a CHECK verdict): Authenticated WSL/Npgsql PG17.12 verified matching fixture cluster, role/database and extensions/settings; eight frozen native IDs executed: 7 passed, 1 failed (`Npgsql.PostgresException`, SQLSTATE `42703`, column `string_to_table` does not exist), zero skip/missing/unexpected/duplicate; test exit 1. Same candidate and assembly verified before/after; independent CHECK assessment pending, no rerun, product attempt, or queue advance. See `.pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-history-kGV7F1/010-native-reconcile.json` and `007-worker-query.log` in the same directory. The preliminary launcher receipt did not parse ANSI-colored events; the offline ANSI-stripped reconciliation is authoritative for outcomes.

Previous independent `pdca-check` report (`ses_de7475dd5ffev37ySX5dWvXkzb`, `N=1,r=1,n=1/3`): `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md", "sha256": "e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95"}`. CHECK `not accepted`; `421-AC1`, `421-AC2`, `421-AC4` unmet; `421-AC3`, `421-AC5` met. Confirmed `421-PG-SCALAR-OUTPUT` in `From_NumbersRepeatedScalarOutputsAndKeepsLegacyUnnest`: authenticated PG17.12, `42703 column "string_to_table" does not exist`, POSITION 8. CHECK → DO for implementation correction.

Prior independent `pdca-check` report on do2 (superseded verdict) (`ses_de7475dd5ffev37ySX5dWvXkzb`, `N=1,r=1,n=2/3`): `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b.md", "sha256": "c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b"}`. CHECK `not accepted`; AC1–AC4 `met`. AC5 unverified: EN `a4f893…` → `31f1d4…` and RU `6127a2…` → `2ee8b7…` guide hashes differ between do1 and do2, so the prior scout observation does not carry forward. No text defect established.

Latest independent `pdca-check` report (`ses_de7475dd5ffev37ySX5dWvXkzb`, `do2`, `N=1,r=1,n=2/3`): `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9.md", "sha256": "abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9"}`.

## ACT record

CHECK → ACT: `accepted` for the unchanged `do2` candidate (`N=1,r=1,n=2/3`); `421-AC1`–`421-AC5` all `met`. Same-test red → green refutes recurrence of `421-PG-SCALAR-OUTPUT` on `do2`; current EN/RU content was observed on `do2`. No unmet or unverified item criterion remains. Inter-attempt section equality was not established or required; this is not collection integration acceptance. Full CHECK report: `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9.md", "sha256": "abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9"}`.

ACT completed; independent retrospective source: `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-38200c7e23237f6e3863be7646f48f46a03b98e13a8355680bef676597532449.md", "sha256": "38200c7e23237f6e3863be7646f48f46a03b98e13a8355680bef676597532449"}`. Outer item autocommit completed (see §Git state). Next: #422 saved-PLAN applicability preflight. #421 remains open on GitHub pending integrated CHECK.

## Lessons learned

Completed independent `pdca-check` retrospective (unchanged): `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-38200c7e23237f6e3863be7646f48f46a03b98e13a8355680bef676597532449.md", "sha256": "38200c7e23237f6e3863be7646f48f46a03b98e13a8355680bef676597532449"}`.

## Revision history

`none`

## Defect history

`421-PG-SCALAR-OUTPUT` | observed `N=1,r=1,n=1/3` | fixes applied: 1 in attempt 2 | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md", "sha256": "e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95"}`; `.pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-history-kGV7F1/010-native-reconcile.json` | first observation; no recurrence or escalation; corrected evidence `.pdca/collection-issues-420-422-item-421/rv1/attempts/do2/native-reconcile.json`, recurrence not observed in exact roster

## Changed files

- `.pdca/status/collection-issues-420-422-item-421-1.md` — created item PLAN record; updated G1 item boundary and applicability preflight.
- Pinned planner report under the item evidence root, referenced in §PLAN record.
- `.pdca/collection-issues-420-422-item-421/rv1/report-2beb553c8a8a65588414b46fc5e2a943db7ed4697de08ea2724eb7c5a95572fb.md` — pinned scoped `pdca-scout` report, referenced in §PLAN record / Saved-PLAN applicability preflight.
- `.pdca/collection-issues-420-422-item-421/rv1/report-393a36f0257e2c060972a0ce784b2523509a417c293fd535d1c4338e6d49c1b2.md` — pinned applicability decision; item and parent status updated.

- `.pdca/collection-issues-420-422-item-421/rv1/attempts/do1/012-candidate-files.sha256` — owned-path ledger (12 paths: 10 tracked modifications, 2 new tests); product changes unstaged.
- `.pdca/collection-issues-420-422-item-421/rv1/attempts/do1/029-run-register.md` — full run ledger and evidence locations.

- `.pdca/collection-issues-420-422-item-421/rv1/report-994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189.md` — exact pinned CHECK report; both status records updated.

- `.pdca/collection-issues-420-422-item-421/rv1/report-96e0dfa887535ebff885e27443b57b036122969c95840bd08b27b28681dab079.md` — exact latest CHECK report; item and parent status updated.

- `.pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md` — exact latest independent CHECK report; both status records updated.

- `.pdca/collection-issues-420-422-item-421/rv1/report-c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b.md` — exact latest independent CHECK report; item and parent status updated.

- `.pdca/collection-issues-420-422-item-421/rv1/report-abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9.md` — exact accepted CHECK report; both status records updated.

## Git state

Status path: `.pdca/status/collection-issues-420-422-item-421-1.md` — created this cycle, updated in this turn; untracked, durable on disk. No item baseline supplied; parent baseline reference: `.pdca/status/collection-issues-420-422.md` § `Git state` (revalidated: attached `refs/heads/1.0.12`, HEAD `4d03dca71dfd7392101c9ce948da7eca522c48f3`; no index changes). The pinned scout report is ignored. No staging, commit, push, or other Git changes made by this turn. The pinned applicability decision is also ignored. This status-only turn updated the two untracked status files and pinned the ignored CHECK report; product worktree and index untouched.
Latest CHECK Recovery write: both status files updated (untracked); `.pdca/collection-issues-420-422-item-421/rv1/report-96e0dfa887535ebff885e27443b57b036122969c95840bd08b27b28681dab079.md` pinned (ignored). No product, index or GitHub changes.

Route-diagnosis pause: only `.pdca/status/collection-issues-420-422-item-421-1.md` and `.pdca/status/collection-issues-420-422.md` updated; untracked per existing ledger. Evidence referenced, not modified.

Updated `.pdca/status/collection-issues-420-422-item-421-1.md` and `.pdca/status/collection-issues-420-422.md` (untracked); pinned `.pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md` (ignored). No product, index, or issue changes.

CHECK Recovery #421: updated `.pdca/status/collection-issues-420-422-item-421-1.md` and `.pdca/status/collection-issues-420-422.md` (untracked per existing ledger); pinned `.pdca/collection-issues-420-422-item-421/rv1/report-c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b.md` (ignored). No product, index, or issue changes.

CHECK → ACT #421: updated `.pdca/status/collection-issues-420-422-item-421-1.md` and `.pdca/status/collection-issues-420-422.md` (untracked); pinned `.pdca/collection-issues-420-422-item-421/rv1/report-abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9.md` (ignored). No product, index, commit or issue changes.

#421 outer item autocommit (after ACT): commit and new HEAD `100c4f635fe10cb197a06b4dd33e87b8fa64c781`; parent `eed3b018525cf11068709642950e72eabdfe8199`. Exact touched-file receipt: `.pdca/collection-issues-420-422-item-421/rv1/autocommit-receipt.log`. Accepted do2 bytes matched before staging. No status or evidence staged; #423 draft unchanged.

- `M` `docs/guide/13-table-valued-functions.md`
- `M` `docs/ru/guide/13-table-valued-functions.md`
- `M` `src/nextorm.core/Builders/Tvf/SourceExpressionLowerer.cs`
- `M` `src/nextorm.core/Builders/Tvf/TvfEntityBuilder.cs`
- `M` `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`
- `M` `src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs`
- `M` `src/nextorm.core/Expressions/Comparers/FromExpressionPlanEqualityComparer.cs`
- `M` `src/nextorm.core/Expressions/Sources/TableFunctionExpression.cs`
- `M` `src/nextorm.core/PublicAPI.Unshipped.txt`
- `M` `src/nextorm.postgres/PostgresDialect.cs`
- `A` `tests/nextorm.integration.tests/T320/PostgresOrdinalityTests.cs`
- `A` `tests/nextorm.postgres.tests/T320/PostgresOrdinalitySqlTests.cs`

## Progress log

unknown | PLAN | revision 1 | attempt 0 | Plan ready at PLAN → DO boundary; DO not started | §PLAN record / r1
2026-11-26T11:20:34Z | PLAN | revision 1 | attempt 0 | G1/#421 in-progress: saved-PLAN applicability preflight opened; next pdca-scout, then pdca-planner; DO not started | §PLAN record / r1
2026-11-26T11:41:29Z | PLAN | revision 1 | attempt 0 | G1/#421: r1 baseline TVF descriptor, From, correlated OuterApply and PostgreSQL catalog premises observed in source. PostgreSQL environment and exact #422 file overlap unknown; visible From/TVF-guide surface overlap noted against the unchanged sequential G1 order. Planner decision pending; DO not started | .pdca/collection-issues-420-422-item-421/rv1/report-2beb553c8a8a65588414b46fc5e2a943db7ed4697de08ea2724eb7c5a95572fb.md
2026-11-26T11:44:00Z | PLAN | revision 1 | attempt 0 | G1/#421: follow-up could not resume prior planner session `ses_de9851103ffeTC3pPzMXW6r1Vb`: `tool.execution: Session ... is not a child of the current session`. No applicability or cycle decision returned; no DO attempt, counters unchanged (N=1,r=1,n=0). Route the same saved r1 applicability question with pinned facts to a fresh `pdca-planner` session; no new entry PLAN or revision; DO not started | §PLAN record / Saved-PLAN applicability preflight
2026-11-26T11:50:42Z | DO | revision 1 | attempt 0 | G1/#421: PLAN → DO authorized; saved r1 applicable, no revision; DO not yet started | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-393a36f0257e2c060972a0ce784b2523509a417c293fd535d1c4338e6d49c1b2.md", "sha256": "393a36f0257e2c060972a0ce784b2523509a417c293fd535d1c4338e6d49c1b2"}`
2026-11-26T14:12:41Z | DO | revision 1 | attempt 1 | G1/#421: DO started (N=1,r=1,n=1); in-progress | .pdca/status/collection-issues-420-422-item-421-1.md §DO result
2026-11-26T16:05:59Z | DO | revision 1 | attempt 1 | G1/#421: Recovered after compaction: server restart cancelled build 002 without exit receipt; saved r1 and attempt unchanged, product delta preserved; resumed bounded DO | .pdca/collection-issues-420-422-item-421/rv1/attempts/do1/002-postgres-build.log
2026-11-26T16:24:36Z | DO | revision 1 | attempt 1 | Recovered after compaction: second server restart cancelled core build 018; recovered with serial build 030 and native results; no counter reset | .pdca/collection-issues-420-422-item-421/rv1/attempts/do1/030-core-build.log
2026-11-26T16:24:36Z | DO | revision 1 | attempt 1 | G1/#421: candidate ready for independent CHECK; live PostgreSQL proof remains unverified; queue unchanged | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-36f7396d19c318f7b483bfd11a45cf6a2deb1aa50f7f0b3769f7ad7c64712b7d.md", "sha256": "36f7396d19c318f7b483bfd11a45cf6a2deb1aa50f7f0b3769f7ad7c64712b7d"}`
2026-11-26T16:31:13Z | CHECK | revision 1 | attempt 1 | G1/#421: not accepted; AC1–AC5 unverified, 8 discovered and 0 executed; no product defect established | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189.md", "sha256": "994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189"}`
2026-11-26T16:31:13Z | DO | revision 1 | attempt 1 | G1/#421: CHECK → DO, bounded same-candidate evidence re-gather and one AC5 scout observation requested for next turn; no product attempt; ACT pending | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189.md", "sha256": "994b67f22b680bb1017d38b6b89fa135791cc90cf4aae144138535579e4ea189"}`
2026-11-26T16:35:37Z | DO | revision 1 | attempt 1 | G1/#421: CHECK-directed live re-gather diagnostics recorded; no verified endpoint, 8 expected, 0 executed; scout observation remains separate | .pdca/collection-issues-420-422-item-421/rv1/attempts/regather-check1/result.md
2026-11-26T16:36:45Z | DO | revision 1 | attempt 1 | G1/#421: AC5 independent wording report pinned and correlated with existing frozen-file checks; PostgreSQL live gap remains; fresh CHECK packet ready | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-a2b8d12bc664733f5787bb1bf4fd513b7b5c42328321efd6030a5e5135218124.md", "sha256": "a2b8d12bc664733f5787bb1bf4fd513b7b5c42328321efd6030a5e5135218124"}`
2026-11-26T16:41:47Z | CHECK | revision 1 | attempt 1 | G1/#421: same candidate, resource-blocked CHECK pending in Recovery; AC1–AC4 unverified (0/8 executed), AC5 met; no polling or new re-gather | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-96e0dfa887535ebff885e27443b57b036122969c95840bd08b27b28681dab079.md", "sha256": "96e0dfa887535ebff885e27443b57b036122969c95840bd08b27b28681dab079"}`
2026-11-26T16:51:08Z | CHECK | revision 1 | attempt 1 | G1/#421: user lead verbatim: `ты идиот!!! контейнер доступен через сокет на винде wslc там подняты все субд`; Windows-side wslc socket/context and reported running DBs are a lead only, not verified endpoint identity, readiness, or connection. Evidence Recovery resumed (paused → active); no product attempt, diagnostics, or tests | .pdca/status/collection-issues-420-422-item-421-1.md#check-record
2026-11-26T16:56:39Z | CHECK | revision 1 | attempt 1 | G1/#421: Windows wslc PG17 publication verified by inventory and TCP from WSL; worker credentials unavailable, authenticated query not run, 0/8 executed; Recovery paused, no product attempt | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-7b1db716d33e7613e163f23353a49d8c7d973b817f07b7e3b95b301a266b33d9.md", "sha256": "7b1db716d33e7613e163f23353a49d8c7d973b817f07b7e3b95b301a266b33d9"}`
2026-11-26T16:59:14Z | CHECK | revision 1 | attempt 1 | G1/#421: evidence Recovery resumed (paused → active). Windows wslc PG17 publication on port 5433 and WSL TCP reachability recorded; override unset, authenticated identity/version/fixture settings unverified, 0/8 executed. Next: narrow read-only route and integration configuration inspection; no new product attempt | .pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-windows/
2026-11-26T17:17:44Z | CHECK | revision 1 | attempt 1 | G1/#421: route diagnosis complete; Recovery paused again pending authorized host injection into the WSL test worker. No authenticated query or tests; 0/8 executed; no polling, re-gather or advance | .pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-auth-source/result.md
2026-11-26T17:32:28Z | CHECK | revision 1 | attempt 1 | G1/#421: новое указание пользователя дословно: `там нет аутентифкации`; возможность подключения без пароля пока не проверена. CHECK Recovery paused → active; без продуктовой попытки, очередь неизменна | .pdca/status/collection-issues-420-422-item-421-1.md#check-record
2026-11-26T17:37:13Z | CHECK | revision 1 | attempt 1 | G1/#421: CHECK Recovery снова paused; PostgreSQL запросил SASL, SQLSTATE не прислан. Аутентифицированной сессии и запуска тестов не было; очередь и кандидат неизменны | .pdca/collection-issues-420-422-item-421/rv1/attempts/regather-passwordless-vU1jjn/result.md
2026-11-26T17:56:17Z | CHECK | revision 1 | attempt 1 | G1/#421: Recovered after compaction: source-of-truth status reloaded; no phase, counter, criteria, or product change | .pdca/status/collection-issues-420-422.md#progress-log
2026-11-27T05:58:30Z | CHECK | revision 1 | attempt 1 | G1/#421: status recovered after server restart; user authorized one same-candidate CHECK re-gather using ctxpg-1795349659 historical PG17 route and current fixture-owned authentication, not the retired passwordless route. Recovery active; criteria, counters and queue unchanged | .pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-history-kGV7F1/
2026-11-27T06:01:52Z | CHECK | revision 1 | attempt 1 | G1/#421: Authenticated WSL/Npgsql PG17.12 verified matching fixture cluster, role/database and extensions/settings; eight frozen native IDs executed: 7 passed, 1 failed (`Npgsql.PostgresException`, SQLSTATE `42703`, column `string_to_table` does not exist), zero skip/missing/unexpected/duplicate; test exit 1. Same candidate and assembly verified before/after; paused pending independent CHECK assessment, no rerun, product attempt, or queue advance. | .pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-history-kGV7F1/010-native-reconcile.json
2026-11-27T06:04:21Z | CHECK | revision 1 | attempt 1 | G1/#421: disposition paused → active; authenticated WSL route to confirmed wslc PG17.12 fixture matched cluster/database/role/settings and pgcrypto/vector; 8 frozen IDs executed, 7 passed, 1 failed (SQLSTATE 42703, column string_to_table does not exist), 0 skipped/missing/unexpected/duplicate. Next: dispatch independent CHECK; no product-defect or attempt decision by DO. | .pdca/collection-issues-420-422-item-421/rv1/attempts/recovery-history-kGV7F1/; `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-1ae86d435c295fdc4b0bfa58dbefd6b02f7f5ee9248deb20171b1e9d2c460f46.md", "sha256": "1ae86d435c295fdc4b0bfa58dbefd6b02f7f5ee9248deb20171b1e9d2c460f46"}`
2026-11-27T06:17:17Z | CHECK | revision 1 | attempt 1 | G1/#421: not accepted; confirmed 421-PG-SCALAR-OUTPUT; AC1/AC2/AC4 unmet, AC3/AC5 met; fixes applied: 0 | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md", "sha256": "e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95"}`
2026-11-27T06:17:17Z | DO | revision 1 | attempt 2 | G1/#421: CHECK → DO implementation correction started (N=1,r=1,n=2/3); executor fix next, ACT pending, G1 queue unchanged | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md", "sha256": "e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95"}`
2026-11-27T06:21:17Z | DO | revision 1 | attempt 2 | G1/#421: DO started, attempt 2; fix 421-PG-SCALAR-OUTPUT under unchanged r1 | .pdca/collection-issues-420-422-item-421/rv1/report-e174dfbbf1466a2ee238110aaa25dc54d9898ee1e9b40751ee33ef6b419d8d95.md
2026-11-27T06:57:11Z | DO | revision 1 | attempt 2 | G1/#421: 421-PG-SCALAR-OUTPUT fixed in PostgreSQL renderer only, 8 exact live cases and 17 provider cases passed; corrected candidate ready for independent CHECK | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-97b775fbe199781c442e766e1e7e6b70fbf1bc2a5c48a5ed0b08619c6a789df4.md", "sha256": "97b775fbe199781c442e766e1e7e6b70fbf1bc2a5c48a5ed0b08619c6a789df4"}`
2026-11-27T07:01:30Z | CHECK | revision 1 | attempt 2 | G1/#421 in-progress, disposition paused in CHECK Recovery; CHECK `not accepted`; AC1–AC4 `met`. AC5 unverified: EN `a4f893…` → `31f1d4…` and RU `6127a2…` → `2ee8b7…` guide hashes differ between do1 and do2, so the prior scout observation does not carry forward. No #422 advance | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b.md", "sha256": "c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b"}`
2026-11-27T07:08:32Z | CHECK | revision 1 | attempt 2 | G1/#421 existing-evidence inventory: no do2 EN/RU WITH ORDINALITY passage text or section-equivalence proof found; prior scout tied to do1 and already considered. DO2 handoff claims only PostgresDialect.cs changed and guides unchanged, but both guide file hashes differ. Cause and passage changes unknown; process/evidence-packaging discrepancy for parent follow-up, not a product finding. AC5 unverified; r1 re-gather exhausted; await authorized external evidence bound to do2 (section equivalence or current content); no status transition or queue advance | .pdca/collection-issues-420-422-item-421/rv1/attempts/do1/012-candidate-files.sha256:9-10; .pdca/collection-issues-420-422-item-421/rv1/attempts/do2/candidate.sha256:9-10; .pdca/collection-issues-420-422-item-421/rv1/attempts/do2/identity-after.log:9-10 (do2 match only); .pdca/collection-issues-420-422-item-421/rv1/attempts/do1/025-candidate-integrity.log:3 (do1 bilingual snippet parity only); .pdca/collection-issues-420-422-item-421/rv1/report-974112b78a61b1901f49bae9c648c2d0b08eafafe415e8d8c3f5a7f88546ecd7.md SHA-256 974112b78a61b1901f49bae9c648c2d0b08eafafe415e8d8c3f5a7f88546ecd7; .pdca/collection-issues-420-422-item-421/rv1/check-97b775fbe199781c442e766e1e7e6b70fbf1bc2a5c48a5ed0b08619c6a789df4.md:1236,1244,1254; .pdca/collection-issues-420-422-item-421/rv1/report-c1ce88f8fac41e8122074c990a2c90cffb0a7d95084f3d8a1f1f18f9581ae34b.md:11,15-17
2026-11-27T09:40:57Z | CHECK | revision 1 | attempt 2 | G1/#421: user verbatim `сверяй` authorizes exactly one additional read-only AC5 section comparison as a narrow same-r1 exception, no new plan, criterion, revision or attempt. Both hashes match; disposition paused → active for that observation only. | .pdca/collection-issues-420-422-item-421/rv1/attempts/do2/candidate.sha256:9-10; docs/guide/13-table-valued-functions.md: current 31f1d4fea6c8123115896dbfedd29b4447a777e5a252c73e3f2784f30e27688e, do2 31f1d4fea6c8123115896dbfedd29b4447a777e5a252c73e3f2784f30e27688e, match; docs/ru/guide/13-table-valued-functions.md: current 2ee8b7ad5e0c32439a3ee110e23c8d5420041d09caa2fe3aa41c23929ccac6c3, do2 2ee8b7ad5e0c32439a3ee110e23c8d5420041d09caa2fe3aa41c23929ccac6c3, match
2026-11-27T10:00:00Z | CHECK | revision 1 | attempt 2 | G1/#421: user-authorized single AC5 comparison completed; earlier dispatch transport error recovered by binding exact existing final message `msg_21c6d28d7001A6U4htPu8606mE` in `ses_de3931976ffeEIIJiMHLwsa4AV` (PDCA-REPORT `4cbfb60d8fc90888`). Fresh CHECK packet ready; no rerun, verdict, state change or queue advance | .pdca/collection-issues-420-422-item-421/rv1/report-8e39b71f73e174fb0594f8bb04aed164d9684e36d6a22d2953ddb21b9cee0e0c.md; .pdca/collection-issues-420-422-item-421/rv1/check-3c14ed56372a4a15dcd1d124f88c7d53ee238976aa6dbad368e0c024467290ee.md
2026-11-27T10:05:12Z | CHECK | revision 1 | attempt 2 | G1/#421 independent do2 CHECK accepted; AC1–AC5 met; scalar failure recurrence refuted by same-test red → green; current EN/RU content observed on do2 | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9.md", "sha256": "abd3f44e323697601d5a4700fec7376f5a8574bf9e04427d81a40b98c94731a9"}`
2026-11-27T10:05:12Z | ACT | revision 1 | attempt 2 | G1/#421 CHECK → ACT, record open; required retrospective and closure pending; #421/G1 in-progress, queue unchanged | .pdca/status/collection-issues-420-422-item-421-1.md#act-record
2026-11-27T10:08:03Z | ACT | revision 1 | attempt 2 | G1/#421: item-only ACT retrospective packet ready, pending dispatch to pdca-check; ACT open, accepted do2 unchanged, #421/G1 in-progress, #422 pending | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/check-5b2d4fc2ebbfd9b046844f653a83a4e7d9dcc8b5517883709efbae926e8d7cff.md", "sha256": "5b2d4fc2ebbfd9b046844f653a83a4e7d9dcc8b5517883709efbae926e8d7cff"}`
2026-11-27T10:24:13Z | ACT | revision 1 | attempt 2 | G1/#421 ACT completed, accepted do2 (5/5 criteria met); item done, G1 in-progress, #422 pending; authorized outer product autocommit pending | `{"path": ".pdca/collection-issues-420-422-item-421/rv1/report-38200c7e23237f6e3863be7646f48f46a03b98e13a8355680bef676597532449.md", "sha256": "38200c7e23237f6e3863be7646f48f46a03b98e13a8355680bef676597532449"}`
2026-11-27T10:25:16Z | ACT | revision 1 | attempt 2 | G1/#421 outer item autocommit 100c4f635fe10cb197a06b4dd33e87b8fa64c781, 12 accepted paths; G1 queue unchanged; next #422 saved-PLAN applicability preflight, not started | .pdca/collection-issues-420-422-item-421/rv1/autocommit-receipt.log
