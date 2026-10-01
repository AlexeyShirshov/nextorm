# Explicit DELETE-join starter — #145 (task 2)

- status: CLOSED
- cycle: 1, revision r1, attempt 1/3
- issue: #145 (milestone 1.0.9-b)
- collection: 1.0.9-b-3, task 2, group-1, branch 1.0.9-b (current worktree)
- goal: give the row-returning multi-table DELETE an explicit starter instead of overloading `JoinedEntityBuilder<T1..Tn>.Returning(...)`, removing the 14 ambiguous joined `Returning` overloads and exposing one public seam.

## Acceptance criteria
- **AC1** Public seam: 7 `public static DeleteJoinBuilder<Projection<T1..Tn>> CreateDeleteJoinBuilder<T1..Tn>(this JoinedEntityBuilder<T1..Tn> builder)` starters (arities 2–8) and `public sealed class DeleteJoinBuilder<TProjection>` with `Returning()` / `Returning<TResult>(Expression<Func<TProjection,TResult>>)`; the 14 ambiguous joined `Returning` overloads removed with no obsolete shims; core builds 0 warnings / 0 errors.
- **AC2** Behavioural parity: `CreateDeleteJoinBuilder(j).Returning()` / `.Returning(p => p)` produce the same `DELETE ... USING ... RETURNING` SQL and materialization as the pre-change `j.Returning()` / `j.Returning(p => p)`; explicit projections unchanged; `Single`/`SingleAsync`/`ToList`/`ToListAsync`/`ToSql` and CTE `With(name, delete)` unchanged.
- **AC3** Null/signature contract: `CreateDeleteJoinBuilder(null)` and `Returning<TResult>(null)` throw `ArgumentNullException`; overload resolution is no longer ambiguous (compile-negative tests in `nextorm.alias.tests`).
- **AC4** All migrated call sites (26 `Returning` usages) compile and pass; DELETE/UPDATE SQL + #143 identity-returning behaviour unchanged; compile-negative tests pass.
- **AC5** EN+RU docs (08-cte, 16-delete, 17-update, API reference + register) updated; XML docs on every new public member; full build clean.

## Frozen public contract
```csharp
public static DeleteJoinBuilder<Projection<T1, T2>> CreateDeleteJoinBuilder<T1, T2>(this JoinedEntityBuilder<T1, T2> builder);
// ... arities 3..8 identical, Projection<T1..Tn>

public sealed class DeleteJoinBuilder<TProjection>
{
    public DeleteJoinReturningBuilder<TProjection, TProjection> Returning();
    public DeleteJoinReturningBuilder<TProjection, TResult> Returning<TResult>(Expression<Func<TProjection, TResult>> projection);
}
```

## Removed overloads (no shims)
The 14 `public static DeleteJoinReturningBuilder<...> Returning<T1..Tn>(this JoinedEntityBuilder<T1..Tn>)` and `Returning<T1..Tn, TResult>(this JoinedEntityBuilder<T1..Tn>, Expression<...>)` extensions, arities 2–8 (identity + projected).

## Tests plan
- Compile-negative API tests: `tests/nextorm.alias.tests/JoinedDeleteReturningApiCompilationTests.cs`.
- Migrate 26 `Returning` call sites (core sql-gen + PG integration) to `CreateDeleteJoinBuilder(...).Returning(...)`.
- Core SQL-generation + integration parity for arities 2–8 (both identity and projected).

## Docs plan
Update EN+RU `docs/guide/08-cte.md`, `docs/guide/16-delete-statement.md`, `docs/guide/17-update-statement.md`, API reference and register; XML on the new public surface. No public docs links to `docs/specs`.

## Decisions
- Perf: not needed (construction-time only; no runtime/SQL-path change).
- Recon: not needed (mechanical refactor; sibling `UpdateJoinBuilder<TProjection>` is the pattern).
- Unit mode: sequential, current tree; no worktree/branch/merge.
- `DeleteJoinReturningBuilder.cs` XML `<see cref>` to the removed extension retargeted to the new seam (dangling cref would fail the enforced build).

## Progress log
- DO started.
- 2026-10-01T18:24:28Z | DO | r1 | 1/3 | migrated all joined-delete `.Returning` call sites to `.CreateDeleteJoinBuilder().Returning(...)` — 74 occurrences across 12 test files (core: JoinReturningBuilderTests, JoinReturningIdentityTests; postgres: JoinReturningIdentitySqlGenerationTests, DeleteSqlGenerationTests; integration: IdentityReturningIntegrationTests, PostgresJoinAritiesTests, PostgresSpecificTests; per-provider DataModifyingCteRejectionTests for sqlite/sqlserver/mysql/mariadb/clickhouse). Scout list undercounted: additional files PostgresJoinAritiesTests, PostgresSpecificTests, DeleteSqlGenerationTests and many more lines in the listed files | /tmp/opencode/145/build_migrate.log
- 2026-10-01T18:24:28Z | DO | r1 | 1/3 | added compile-contract tests `tests/nextorm.alias.tests/JoinedDeleteReturningApiCompilationTests.cs` (real-API CSharpCompilation harness): positive arities 2/5/8 `CreateDeleteJoinBuilder().Returning()`/`.Returning(p=>...)` → 0 errors; negative direct `.Returning()`/`.Returning(p=>p)` → CS1061 reported at the `Returning` identifier; control guard proves the chain compiles with the new starter | /tmp/opencode/145/tests/alias.log
- 2026-10-01T18:24:28Z | DO | r1 | 1/3 | builds: Debug 0 warnings/0 errors, Release 0 warnings/0 errors | /tmp/opencode/145/tests/build-debug.log, /tmp/opencode/145/tests/build-release.log
- 2026-10-01T18:24:28Z | DO | r1 | 1/3 | tests: core `FullyQualifiedName~JoinReturning` 44/44 pass; postgres `~JoinReturning` 36/36 pass; alias `~JoinedDeleteReturningApiCompilation` 5/5 pass; core full 1249/1249 pass; integration IdentityReturningIntegrationTests (PostgreSQL only, provider pinned) 50/50 pass, 0 skipped | /tmp/opencode/145/tests/{core-joinreturning,postgres-joinreturning,alias,core-full,integration-identity-returning}.log
- 2026-10-01T20:33Z | DO→PLAN | r2 | 1/3 | Stryker infeasible (no repo stryker-config, no per-method scoping) → planner required a manual targeted-mutation campaign

## Mutation campaign (r2/n1)

Manual targeted mutations of the new forwarding/starter API; each compile-valid, RED then byte-exact revert then GREEN. Logs `/tmp/opencode/145/mutation/`.

| id | target | red | green |
|---|---|---|---|
| 1 | `DeleteJoinBuilder.cs:51` identity lambda → non-identity | core `~JoinReturningIdentityTests` exit 2, 6 failed | exit 0, 24/24 |
| 2 | `DeleteJoinBuilder.cs:69` projection null-guard removed | core `~JoinReturningBuilderTests` exit 2, 1 failed | exit 0, 20/20 |
| 3 | `DeleteJoinBuilder.cs:28` `_targetType = typeof(object)` | SURVIVOR | equivalence argued (see below) |
| 4 | new test + `DataContextExtensions.cs:853` query null-guard removed | exit 2, 1 failed (`CreateDeleteJoinBuilder_NullReceiver_ShouldThrow`) | test 1/1, revert 1/1 |
| 6 | `DeleteJoinBuilder.cs:71` forward `null!` projection | postgres exit 2, 6 failed | exit 0, 36/36 |

- Survivor (id 3) equivalence: `_targetType` flows to `DeleteJoinCommand.EntityType`, but the DELETE renderer (`DataContext.cs:1340` → `QueryPlanner.RenderDeleteJoinCore` → `SqlBuilder.MakeDeleteJoin`, `SqlBuilder.cs:1226`) derives the target from `cmd.Source.EntityType`/`cmd.Source.From` and never reads `cmd.EntityType`; `MakeJoinReturning` uses only columns+projection; the CTE path (`MutationCteQuery.cs:124`) uses `typeof(TProjection)`+`command.Source`. Pre-existing dead plumbing (the removed overloads also passed an unused `typeof(T1)`); provider- and arity-independent. Documented equivalence, not a test gap.
- Starters: 7 bodies are structurally identical modulo arity (`DataContextExtensions.cs:851,863,876,890,905,921,938`); the shared target-type plumbing is exercised by integration `PostgresJoinAritiesTests` (arities 3–8) and unit SQL-gen (2/3/8). Null-receiver guard directly tested.
- Revert proof: `md5sum -c pre-campaign.md5` OK; `git diff` still the same 23 files + 3 untracked; final `dotnet build nextorm.slnx -c Debug` 0/0. Original D stays active/blocked — not done, not superseded.

CHECK PASS (r2/n1) → ACT
