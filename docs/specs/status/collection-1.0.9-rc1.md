# Collection 1.0.9-rc1

- collection-id: `1.0.9-rc1`
- input: open issues of GitHub milestone `1.0.9-rc1` (milestone #19): 182, 181, 168, 163, 161, 150, 141, 140, 134, 133, 132, 131, 128, 127, 126
- mode: autonomous; auto-commit authorized by explicit user request and the repo AGENTS `pdca-collection` exception; **push never**
- base: branch `1.0.9-rc1` = `main` = merge of `1.0.9-b` (tag `v1.0.9-b`); working tree clean
- collection revision: r1; evidence revision: rv1

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G01 | D141, D126, D181, D182, D168, D131, D128, D133, D127, D163 | D141 → D126 → D181 → D182 → D168 → D131 → D128 → D133 → D127 → D163 | current worktree | 1.0.9-rc1 | in-progress |

Single group ⇒ no group/task worktrees or branches are created; authorized commits go to the current branch `1.0.9-rc1`; no `git merge --no-ff`.

## Tasks

| task | group | branch | status | reason + patch | task status file |
|---|---|---|---|---|---|
| D141 | G01 | 1.0.9-rc1 | done | issue #141 — version gates MariaDB 13 + PostgreSQL FILTER aggregates | docs/specs/status/rc1-141-version-gates-1.md |
| D126 | G01 | 1.0.9-rc1 | done | issue #126 — tuple constructor on MySQL/MariaDB/SQLite | docs/specs/status/rc1-126-tuple-ctor-1.md |
| D181 | G01 | 1.0.9-rc1 | done | issue #181 — SQLite FTS3/4/5 query surface + FTS5 FROM source | docs/specs/status/rc1-181-sqlite-fts-1.md |
| D182 | G01 | 1.0.9-rc1 | done | issue #182 — SQL Server scalar-function parity | docs/specs/status/rc1-182-mssql-scalars-1.md |
| D168 | G01 | 1.0.9-rc1 | pending | issue #168 — SQL Server MapColumnExpression numeric boxing | docs/specs/status/rc1-168-mssql-boxing-1.md |
| D131 | G01 | 1.0.9-rc1 | pending | issue #131 — PostgreSQL native json/jsonb column mapping | docs/specs/status/rc1-131-pg-json-column-1.md |
| D128 | G01 | 1.0.9-rc1 | pending | issue #128 — ClickHouse native JSON column mapping (scout reader site first) | docs/specs/status/rc1-128-ch-json-column-1.md |
| D133 | G01 | 1.0.9-rc1 | pending | issue #133 — streaming LOB MySQL/MariaDB/ClickHouse | docs/specs/status/rc1-133-streaming-lob-1.md |
| D127 | G01 | 1.0.9-rc1 | pending | issue #127 — SQL Server OUTPUT INTO table variable | docs/specs/status/rc1-127-mssql-output-into-1.md |
| D163 | G01 | 1.0.9-rc1 | pending | issue #163 — ClickHouse ref→collection accepted limitation (docs/disposition) | docs/specs/status/rc1-163-ch-refcollection-doc-1.md |

## Decisions

- **One group (G01).** The actionable-issue footprint graph is connected through the core dialect contracts (`ISqlDialect.cs`, `SqlDialectBase.cs`, `DialectCapabilities.cs`), shared core query/planner/context files, the provider dialects (ClickHouse, SQLite, MySQL/MariaDB, PostgreSQL, SQL Server), shared integration suites and shared EN+RU docs. Disjoint-footprint groups are impossible; a single sequential lane is the honest minimal grouping.
- The intra-group order is a **serialization dependency** (footprint overlap), not a functional chain.
- **Excluded from lanes (not actionable now):**
  - **#161** blocked — deferred Medium work with conflicting rc1/rc2 milestone; re-entry: explicit release-placement/priority decision or a demonstrated correctness/security requirement.
  - **#150** blocked — an approved spec is not implementation authorization for rc1; implementation not started.
  - **#140** blocked — "free column list" has no confirmed meaning or identifiable capability.
  - **#134** blocked — duplicates rc2 design issue #189; ownership/release placement unresolved.
  - **#132** already-done/close — the JSON1 family is already declared (`SqlFunctions.Sqlite.cs:83-273`) and rendered (`SqliteFunctionRenderer.cs`); recommend closing the original request; any concrete residual defect becomes separately scoped work.
- Each admitted task owns its full `pdca-dotnet` cycle (own PLAN/DO/CHECK/ACT, own status file, own acceptance contract).

## Evidence contract r1/rv1

| requirement ID | row ID | check / scenario | command or invocation + required result | planned artifacts | owner | applicability | rv |
|---|---|---|---|---|---|---|---|
| truthful admission | C-E01 | Validate collection r1 admission against the 15-issue brief: dispositions, excluded issues, G01 membership, ordered task IDs. | `Task → check`; explicit PASS/FAIL with discrepancies (no exit code) | collection status + admission-check report | collection CHECK | always | rv1 |
| exclusive ownership | C-E02 | Validate G01 union footprint, registered child footprints, and absence of concurrent overlapping child execution. | `Task → check`; explicit PASS/FAIL | footprint register, child execution ledger, check report | collection CHECK | before first dispatch, on footprint/admission change, before merge | rv1 |
| verified completion | C-E03 | For each admitted task, verify its frozen pdca-dotnet evidence contract, final CHECK and ACT, required execution logs/artifacts, and final done status; missing evidence is not completion. | `Task → check`; explicit PASS/FAIL per stable task ID | child status files, execution logs, CHECK/ACT reports, completion ledger | child cycle supplies; collection CHECK audits | every admitted task | rv1 |
| safe integration | C-E04 | Pre-merge authorization and complete-group checks pass, then integration. | `git merge --no-ff --no-edit collection/1.0.9-rc1/g01`; exit 0, no unresolved conflicts. (Single group ⇒ no group branch/merge performed; row applies only if a group branch exists.) | integration-check report, merge log, HEAD id | collection CHECK gates; coder executes | only after G01 is `done` | rv1 |

CHECK re-gather budget: at most 2 targeted evidence requests per collection CHECK invocation, owned by collection CHECK.

## Общая верификация и восстановление

- Last common C: D182 task-level CHECK PASS (see rc1-182-mssql-scalars-1.md)
- Blocking reason: —
- Verification state: pending.
- Defect id and history: —
- Related corrective-task status file: —
- Next allowed step: D168 pending — start its pdca-dotnet cycle (PLAN gather).
- Notice: host has no todowrite tool; task status files carry the progress log instead.
- Notice: gh CLI was available; issue #141 was closed remotely (glab/gh exit 0, state CLOSED) — documented in the D141 status ACT. The commit is unpushed (push never authorized).
- Notice: D126 is done — issue #126 closed; commit 4c10548a (unpushed); CHECK PASS r=4/rv=5 recorded in rc1-126-tuple-ctor-1.md; the earlier run-1 "remains pending" note is superseded.
- Notice: D181 is done — task-level CHECK PASS (r=3, n=2/3, rv=D181.r3.ec1); issue #181 closed; commit 99c41de2 (unpushed); FTS3/4/5 query surface + FTS5 FROM source shipped, with FTS5 maintenance/control deferred to follow-ups #195/#196 (milestone 1.0.9-rc1); see docs/specs/status/rc1-181-sqlite-fts-1.md. The earlier "looping back" note is superseded.
- Notice: D182 is done — task-level CHECK PASS (r=2, n=1/3, rv=D182.r2.ec1); issue #182 closed; commit c75ef84a (unpushed); SQL Server scalar-function parity shipped for the pinned 61-entry manifest = 51 covered + 10 excluded (E1–E7); defect D182-c1 closed; see docs/specs/status/rc1-182-mssql-scalars-1.md. The earlier "in-progress (PLAN gather dispatched)" note is superseded.

## Done / Verified / Incomplete

- Done: D141 (#141) — commit 45107d9c; D126 (#126) — commit 4c10548a; D181 (#181) — commit 99c41de2; D182 (#182) — commit c75ef84a
- Verified: D141 CHECK PASS, rv=D141.ContractA.strong.1; D126 CHECK PASS, rv=5; D181 CHECK PASS, rv=D181.r3.ec1; D182 CHECK PASS, rv=D182.r2.ec1
- Incomplete: —
