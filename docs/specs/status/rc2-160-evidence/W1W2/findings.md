# W1/W2 — positional `CreateJoined` state transfer: baseline vs r=3

- task_id D160 (issue #160), cycle 1, plan r=3, contract rv=4, attempt n=1/3
- scope: **facts only** (CHECK re-gather). No product behavior changed. The regression below is
  **demonstrated, not fixed** (DO→PLAN candidate).
- Baseline code = `git show 40b1a159:src/nextorm.core/Builders/EntityBuilder.cs` (HEAD, before the
  uncommitted r=3 D160.3-1 carrier change). Excerpts saved next to this file.
- Current code = working tree (`40b1a159+dirty`).

## 1. What the positional join transfer copied at baseline vs now

Baseline positional path: `CreateJoined` (`baseline-CreateJoined.txt`, 40b1a159:3526-3547) calls
`ApplyJoinStateTo(cb, query)` (40b1a159:3280-3320). That procedure copied an **explicit hand-maintained
list**: `Logger`, `Table`, `Query`, `IsDistinct`, `GroupingType`, `GroupingSets`, `GroupByWithTotals`,
`LimitByClause`, `DistinctOnClause`, `TableSampleClause`, `TemporalClause`, `RowLockClause`, `IsFinal`,
`SampleRatio`, `SampleOffset`, `SettingsList`, `PreWhereCondition`, `ArrayJoins`, `ArrayJoinKind`,
`TableHints`, `IndexHints`, `IndexHintKind`, `TablesInScopeHints`, `Ctes`, `QuoteIdentifiers`,
`NamingConvention`, `KeywordCase`, `SourceFrom`, `FilterScope`; table overrides only when `query is null`.

Current positional path (same call chain as the alias path):
`CreateJoined` `EntityBuilder.cs:3607` → `ApplyJoinStateTo` `EntityBuilder.cs:3381` →
`CopySharedStateTo` `EntityBuilder.cs:3360`. `CopySharedStateTo` does `target._state = _state`
(one structural assignment of the whole `EntityBuilderSharedState` value type,
`EntityBuilder.cs:26-81`) plus explicit `Paging`/`ArrayJoins`/`SettingsList`.

Fields present in the carrier but **absent from the baseline hand list** (newly copied on the
positional path): `SourceEntityType` (carrier field `:44`), `BindArrayJoinElement` (`:45`),
`ExtremeRow` (`:37`), `SubQueryHint` (`:50`), `Tag` (`:71`), `CommandTimeout` (`:72`),
`Windows` (`:43`).

## 2. Per-field observable impact on the positional path

| field | set by | consumers (current `file:line`) | observable on positional path? |
|---|---|---|---|
| `SubQueryHint` | `From(query/builder, options.SubQueryHint)` (`DataContextExtensions.cs:1333,1397`) | `EntityBuilder.cs:130,169,2350`; `BuildCommand` builds `FromExpression{ SubQueryHint }` `EntityBuilder.cs:4025,4027,4029`; renderer `SqlSourceRenderer.cs:532`; SQL `SqlBuilder.cs:1743,1748` | **YES — demonstrated** (SQL hint now present) |
| `Tag` | `WithTag` `EntityBuilder.cs:3889-3893` | `EntityBuilder.cs:290,387` (`cmd.Tag = Tag`), `:2354`; `SqlBuilder.cs:451,994`; plan key `QueryPlanEqualityComparer.cs:197,547` | **YES — demonstrated** (tag comment + plan key) |
| `CommandTimeout` | `WithCommandTimeout` `EntityBuilder.cs:3905-3909` | `EntityBuilder.cs:296,388` (`cmd.CommandTimeout = CommandTimeout`), `:2355`; `QueryPlanner.cs:688` sets `dbCommand.CommandTimeout` | **YES — demonstrated** (execution timeout) |
| `ExtremeRow` | `SelectWhereMax`/`SelectWhereMin` `EntityBuilder.cs:1914,1929,1971-1983` (return `EntityBuilder<TEntity>` ⇒ a following positional `Join` binds) | `EntityBuilder.cs:117,240,393` (`cmd.ExtremeRow = _extremeRow`), `:2337`; `SqlBuilder.cs:48,651,790,885,1113`; in-memory `InMemoryQueryBuilder.cs:238,329,419` | **YES by construction** (extreme-row clause now survives the join; baseline dropped it). Not separately executed here — same shared procedure as the three demonstrated fields. |
| `SourceEntityType` | only `ArrayJoinElement`/`LeftArrayJoinElement` `EntityBuilder.cs:1855` (with `BindArrayJoinElement` `:1866`) | `EntityBuilder.cs:124,310,343,432,1379,1837,2434,3268,4060` | **NO** — the setter always makes `TEntity = ArrayJoinProjection<…>` (projection-shaped), and a positional `Join` with `query is null` fails closed at `EntityBuilder.cs:3619-3621` before any transfer; not independently reachable. |
| `BindArrayJoinElement` | only `ArrayJoinElement` `EntityBuilder.cs:1866` | `EntityBuilder.cs:125,312,361,450,1459`; `QueryCommand.cs:481` | **NO** — same `ArrayJoinProjection` receiver as above; inert on the positional join path. |
| `Windows` | `Window` `EntityBuilder.cs:2015-2038` | `EntityBuilder.cs:123,260,2303,2037`; guards `:2836,:3059,:3105,:3254,:3579,:3632`; SQL `SqlBuilder.cs:307` | **NO** — every join/root transition rejects a non-null window list (`"Named windows must be declared after joins"`), so `_windows` is always `null` at copy time. Inert today. |

## 3. Ownership / sharing policy — `Windows` vs `ArrayJoins` / `SettingsList`

`CopySharedStateTo` (`EntityBuilder.cs:3360-3369`) starts with `target._state = _state`, which
shallow-copies **every** reference-typed carrier entry. It then re-assigns two of them through their
**cloning** property setters:

- `target.ArrayJoins = ArrayJoins` (`:3367`) → setter `:258` clones (`value is null ? null : [.. value]`).
- `target.SettingsList = SettingsList` (`:3368`) → setter `:177` clones similarly.

`Windows` has a cloning setter too (`:260`) but is **not** re-assigned in `CopySharedStateTo`, so it is
left **shallow-shared by reference** with the source builder. So the answer is: `ArrayJoins`/
`SettingsList` are defensively deep-copied on the transition; `Windows` is shared by reference —
**incidentally, not by an explicit design decision** (the code comment `:3365` “the other reference-typed
collections are shallow-shared, exactly as the previous join transfer did” is inaccurate for
`ArrayJoins`/`SettingsList`). The sharing is **inert today** because a non-null `Windows` cannot reach
any transition (guards above), so `(joined._windows ??= []).Add(...)` (`:2037`) always allocates a new
list on the joined builder. Latent risk only if the pre-join window guard is ever relaxed.

## 4. Verdict

**Regression (demonstrated).** The r=3 shared state carrier also became the positional transfer, so a
positional-only chain now carries `SubQueryHint` / `Tag` / `CommandTimeout` (and, by construction,
`ExtremeRow`) onto the joined builder; baseline `40b1a159` dropped all of them. This changes positional
SQL / plan key / command execution, contrary to **R160-02** (“positional-only preserves existing
API/SQL/execution path with no alias overhead”).

Control test (pins baseline behavior, currently **red**):
`NextORM.Core.Tests.EntityBuilderStateCopyTests.Positional_join_control_pins_the_baseline_modifier_behavior`
(`tests/nextorm.core.tests/EntityBuilderStateCopyTests.cs:172`).

Command (argument array):
`dotnet test tests/nextorm.core.tests -c Debug --filter FullyQualifiedName~Positional_join_control_pins_the_baseline_modifier_behavior`
→ exit 2; total 1 / failed 1 / succeeded 0. Three failing assertions:
- `_subQueryHint`: SQL starts `select /*+ MY_HINT(positional) */ …` (baseline: no hint).
- `_tag`: SQL contains `/* positional-tag */` (baseline: no tag).
- `_commandTimeout`: `DbCommand.CommandTimeout == 42` (baseline: `0`).

Log: `positional-control.log`; required solution build `solution-build.log` → exit 0, 0 warnings / 0 errors.

Disposition: **DO→PLAN candidate** (positional-path behavior change). Not fixed here (CHECK re-gather);
the planner must decide whether to scope `CopySharedStateTo` back to the root/alias transition or to
accept the positional propagation as a contract revision under R160-02.
