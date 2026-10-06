# Collection 1.0.9-rc1-tail

- collection-id: `1.0.9-rc1-tail`
- input: deferred + follow-up issues of milestone `1.0.9-rc1` authorized by the user on 2026-10-06: 161, 150, 140, 134, 197, 193, 194, 195, 196, 198 (#132 closed as already-done, not in lanes)
- mode: autonomous; auto-commit authorized by explicit user request and the repo AGENTS `pdca-collection` exception; **push never**
- base: branch `1.0.9-rc1` (HEAD `b998df17`, pushed by the user); working tree clean except untracked `artifacts/`
- collection revision: r1; evidence revision: rv1
- collection status: **in-progress (resumed after author clarification on #140)**

## Groups

| group | tasks | order | worktree | branch/ref | status |
|---|---|---|---|---|---|
| G1 | D161, D197, D140, D134, D194, D150, D193, D198, D195, D196 | D161 → D197 → D140 → D134 → D194 → D150 → D193 → D198 → D195 → D196 | current worktree | 1.0.9-rc1 | in-progress |

Single group ⇒ no group/task worktrees or branches are created; authorized commits go to the current branch `1.0.9-rc1`; no `git merge --no-ff`. The already-made commits (D161 b06784c8, D197 95849d38) stay on `1.0.9-rc1` — no auto-rollback on stop.

## Tasks

| task | group | branch | status | reason + patch | task status file |
|---|---|---|---|---|---|
| D161 | G1 | 1.0.9-rc1 | done | issue #161 — ClickHouse identifier quoting escapes backslashes; commit b06784c8 | docs/specs/status/rc1-tail-161-ch-escape-1.md |
| D197 | G1 | 1.0.9-rc1 | done | issue #197 — PostgreSQL bare JsonNode read from native json/jsonb; commit 95849d38 | docs/specs/status/rc1-tail-197-pg-jsonnode-1.md |
| D140 | G1 | 1.0.9-rc1 | done | issue #140 — docs-only (author option A): documented EN+RU that jsonb_to_record/jsonb_to_recordset need a caller-declared TRow (alias column-definition list AS x(a int, b text)) and that schema-less use is via [DynamicColumns]/raw ToDataReader/ResultSet/jsonb_each*; docfx exit 0; commit 85083fb8 | docs/specs/status/rc1-tail-140-pg-free-columns-1.md |
| D134 | G1 | 1.0.9-rc1 | done | issue #134 — SQLite ToDataReader enabled (variant A, locator-free buffered); R06 open-failure leak fixed; docs EN+RU; CHECK pass r=1/n=3; coverage 87.2%/78.9%; benchmark 7/7; commit 65477266 | docs/specs/status/rc1-tail-134-sqlite-datareader-1.md |
| D194 | G1 | 1.0.9-rc1 | done | issue #194 — PostgreSQL raw ROW/composite materialization; CHECK PASS r=2/rv2/n=3/3; commit 451d5c6e | docs/specs/status/rc1-tail-194-pg-row-composite-1.md |
| D150 | G1 | 1.0.9-rc1 | done | issue #150 — ClickHouse native extreme-row parity for float/double keys (direction-aware NaN-safe adaptation); CHECK PASS r1/rv1/n=1/3; EN+RU docs; real-CH 25.8 parity 35/0/0, CH unit 546/0/0; commit 4d0237bf | docs/specs/status/rc1-tail-150-ch-float-extreme-1.md |
| D193 | G1 | 1.0.9-rc1 | pending | re-armed to pending after blocker resolution (not yet executed) | docs/specs/status/rc1-tail-193-tuple-in-1.md |
| D198 | G1 | 1.0.9-rc1 | pending | re-armed to pending after blocker resolution (not yet executed) | docs/specs/status/rc1-tail-198-ch-native-json-1.md |
| D195 | G1 | 1.0.9-rc1 | pending | re-armed to pending after blocker resolution (not yet executed) | docs/specs/status/rc1-tail-195-fts5-maintenance-1.md |
| D196 | G1 | 1.0.9-rc1 | pending | re-armed to pending after blocker resolution (not yet executed) | docs/specs/status/rc1-tail-196-fts5-tests-docs-1.md |

## Decisions

- **One group (G1).** The footprint/dependency graph is one connected component: `ClickHouseDialect.cs` links D161/D150/D193/D198; `SqlBuilder.cs` links D150/D140/D134; `DataContext.cs` links D134/D195; `SelectExpression.cs` links D197/D194/D198; `DialectCapabilities.cs` links D193/D194; `PostgresDialect.cs`/`PostgresDataContext.cs` link D140/D194/D197; and D195 → D196 is an explicit contract dependency. Disjoint-footprint groups are impossible; a single sequential lane is the honest minimal grouping.
- Intra-group order is a **serialization constraint** (footprint overlap), not a functional chain, except the explicit **D195 → D196** dependency.
- Each admitted task owns its full `pdca-dotnet` cycle (own PLAN/DO/CHECK/ACT, own status file, own acceptance contract).
- Scope flags for each task's own PLAN: D140 (supported partial/free projection shapes, typing/naming, unsupported cases); D194 (raw/composite result shapes, mapping/type registration, null handling, provider N/A evidence); D198 (Auto/Native/legacy storage matrix, compatibility/version); D193 (tuple representations, empty/null/default, nullable components, per-provider capability/rejection matrix); D195 (public command signatures, argument validation, execution/result semantics, unsupported-provider behavior); D196 (finalize tests/docs against D195's approved contract).

## Evidence contract r1/rv1

| requirement ID | row ID | check / scenario | command or invocation + required result | planned artifacts | owner | applicability | rv |
|---|---|---|---|---|---|---|---|
| truthful admission | C-E01 | Validate collection r1 admission against the 10-task brief: dispositions, G1 membership, ordered task IDs. | `Task → check`; explicit PASS/FAIL with discrepancies | collection status + admission-check report | collection CHECK | always | rv1 |
| exclusive ownership | C-E02 | Validate G1 union footprint, registered child footprints, absence of concurrent overlapping child execution. | `Task → check`; explicit PASS/FAIL | footprint register, child execution ledger, check report | collection CHECK | before first dispatch, on footprint/admission change, before merge | rv1 |
| verified completion | C-E03 | For each admitted task, verify its frozen pdca-dotnet evidence contract, final CHECK and ACT, required logs/artifacts, final done status; missing evidence is not completion. | `Task → check`; explicit PASS/FAIL per stable task ID | child status files, logs, CHECK/ACT reports, completion ledger | child cycle supplies; collection CHECK audits | every admitted task | rv1 |
| safe integration | C-E04 | Pre-merge authorization and complete-group checks, then integration. | single group ⇒ no group branch/merge performed; row applies only if a group branch exists | integration-check report, merge log, HEAD id | collection CHECK gates; coder executes | only after G1 is `done` | rv1 |

CHECK re-gather budget: at most 2 targeted evidence requests per collection CHECK invocation, owned by collection CHECK.

## Общая верификация и восстановление

- Last common C: not run (lane continues; single group ⇒ no C-E04 merge path).
- Verification state: **unverified**.
- Defect id and history: F140-SCOPE resolved via author option A (D140 done); D134 complete; no open defect.
- Related corrective-task status file: —.
- Next allowed step: D193 next, then D198 → D195 → D196.
- Notice: host has no todowrite tool for subagents; task status files carry the progress log instead.

## Done / Verified / Incomplete

- Done: D161, D197, D140, D134, D194, D150.
- Verified: —.
- Incomplete: —.

## Resume (2026-10-06)

- Blocker resolved: the author answered Q140 with **option A** — #140 is not a missing capability; the schema-less ("free/partial column list" without a caller-declared `TRow`) use case is already covered by `[DynamicColumns] Dictionary<string,object?>`, the raw `ToDataReader`/`ResultSet` path, and `jsonb_each`/`jsonb_each_text`/`jsonb_object_keys`; `jsonb_to_record`/`jsonb_to_recordset` require a declared schema (PostgreSQL requires a column definition list). #140 is closed on GitHub.
- D140 is re-scoped to a **docs-only** cycle: document the workaround paths in EN+RU (`docs/guide/provider-specific/postgresql.md` + RU, `docs/advanced/limitations.md` + RU, and the table-valued-functions guide where the record contract is stated), then close the loop; no production code.
- The `incomplete "группа остановлена"` marks on D134/D194/D150/D193/D198/D195/D196 were an artifact of the outside-cycle blocker; those tasks were **never executed** and are re-armed to `pending`.
- D161 (#161) and D197 (#197) remain `done` — their commits are valid.
- Verification state: **unverified** (collection not yet verified).
- Next allowed step: D193 next, then D198 → D195 → D196.
- D194 complete: PostgreSQL raw `ROW(...)`/composite materialization shipped (caller-registered
  composites, NULL/error contracts, EN+RU docs, PG integration tests); CHECK PASS r2/rv2 n=3/3; commit
  `451d5c6e`. The D194 status file is kept. Collection verification state remains **unverified**.
- D150 complete: ClickHouse native extreme-row parity for Float32/Float64 extreme keys via direction-aware
  NaN-safe adaptation (`(isNaN(k)[= 0], k)`, Float32 via `toFloat64`); groups integral-only, arity ≤ 3;
  Float16/Decimal/arity>3 floating keys stay portable; EN+RU docs; real-CH 25.8 parity CHECK PASS
  r1/rv1 n=1/3 (CH unit 546/0/0, real-CH parity 35/0/0); commit `4d0237bf`. Collection verification state
  remains **unverified**.
