# Status: issue 113 — join alias projection (iteration 1: mandatory PoC)
- Task: #113 "В join добавить новый параметр - алиас" (milestone 1.0.9-b)
- Phase: iteration 1 CLOSED — PoC PASS. Feasibility of the source-generator approach confirmed.
- Goal (achieved): prove a source generator can produce a per-call-site typed lexical alias property `p.Alias` (e.g. `p.Buyer.Id`) with no pre-declared projection, incl. local-variable + method split and distinct SQL slots for a repeated CLR type.
- VERDICT (independent re-verification, /tmp/opencode/v2-*.log):
  - P1 two aliased joins compile+execute: PASS
  - P2 `Select(p => p.Buyer.Id)` expression tree + executed SQL: PASS
  - P3 local variable + method split preserves generated types: PASS (user must name the generated type in the method signature)
  - P4 repeated CLR type (`Person` twice) maps to distinct slots: PASS — SQL selects `t2.Id` (Buyer) vs `t3.Id` (Approver); executed Buyer=10, Approver=20
  - Clean Release build 0 Warning(s)/0 Error(s); `tests/nextorm.core.tests` 1215/1215 pass.
  - Engine diff red-flag check: no `As<TResult>`, no `Cache=false`/`_dontCache`, no shared QueryCommand mutation; ItemN behavior preserved.
- Evidence artifacts: `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`; `tests/nextorm.alias.poc/`; engine seam `EntityBuilder<T>.JoinAlias<...>`; `JoinSlotAttribute`; slot resolution via `ProjectionAliasCache.GetMemberPosition`.
- Accepted PoC-only limitations (must be addressed in production): generator is full-compilation (not incremental); no arity cap beyond `Projection<T1..T8>`; alias detection syntax-only; generated getters throw `NotSupportedException` (never invoked in expressions); PoC project is outside nextorm.slnx and relaxes `Nullable`/`TreatWarningsAsErrors` locally; generated type names are user-facing in method signatures.
- Open decisions for production PLAN (escalated): experimental isolation/removal vs stable public commitment of `JoinSlotAttribute`/`JoinAlias`; attribute targets + slot validation; freeze overload/type-naming contracts; whether generated type names are supported user-facing API; in-memory support vs documented fail-closed; EN/RU public docs; API compatibility validation.
- Next plan: iteration 2 — production contract + hardening (incremental generator, arity guard, diagnostics for duplicates/collisions, all 8 operators incl. JoinInto, provider tests, coverage >=85/75, query-path perf acceptance, EN/RU docs). Nothing moves out of milestone 1.0.9-b.
## Iteration 2 PLAN — BLOCKED on product-owner decisions
- Verdict of `escalate` (2026-10-01): proceed to production in 1.0.9-b as an **Experimental slice** with a narrow public seam, split into Block A (Join/LeftJoin/RightJoin/FullJoin/CrossJoin on an incremental generator with diagnostics) and Block B (CrossApply/OuterApply/JoinInto, each with its own mini-PoC).
- BLOCKERS requiring the product owner BEFORE code (irreversible / API-shaping):
  1. Generator packaging: `nextorm.core.sourcegenerator` is an unreferenced stub; shipping it as a NuGet analyzer (`analyzers/dotnet/cs`, netstandard2.0) changes the package graph — irreversible after publish.
  2. Public naming contract: the `Alias` marker class, the `AliasJoin_` type prefix, the reserved namespace, and `internal` vs `public` for generated types.
  3. `Experimental` vs stable commitment of `JoinSlotAttribute` / `EntityBuilder<T>.JoinAlias` (recommended: public + `[EditorBrowsable(Never)]` + `[Experimental("NEXTORM_ALIAS")]`, generated code wraps calls in `#pragma warning disable NEXTORM_ALIAS`).
  4. Whether an internal join kind exists to back a single `JoinAlias` seam (must not open a new public enum per operator).
  5. Measured generator overhead on a large compilation ("Measure, never assert").
- Recommended frozen semantics: `[JoinSlot(n)]` n = entity position in the chain, starting at 2 (additive-extensible, never break). Arity cap = existing `Projection<T1..T8>` (do NOT extend beyond 8). In-memory fail-closed `NotSupportedException` at the `JoinAlias` seam, naming operator + alias. Generated types `internal` in a reserved namespace so they stay out of user public API.
- Ordered production task list (Block A): incremental generator (`CreateSyntaxProvider` + semantic check + value-equatable model); diagnostics (duplicate alias, collision with `ItemN`, invalid identifier, arity > 8, non-`Alias.X`); seam `JoinAlias` + `JoinSlotAttribute` with Experimental/EditorBrowsable + in-memory fail; core tests (in-memory fail per operator, positional/ItemN regression); plan-cache/alternating-slot regression (Buyer/Approver interleaved on one DataContext, repeated cached executions — risk of slot mixing, must be verified not assumed); integration tests across all SQL providers (DOCKER_HOST); generator-overhead measurement + cached-path perf acceptance; EN/RU docs; coverage 85/75. Block B (CrossApply/OuterApply/JoinInto) is a separate unit in the SAME milestone 1.0.9-b.
- Explicitly rejected: arity > 8; hand-written user use of `JoinSlot`; named path in in-memory; cross-project chaining (already out of scope).
- State: STOPPED in autonomous mode — a blocker genuinely requires the user (product-owner API/packaging decisions). Iteration 1 PoC PASS remains recorded above. Worktree left uncommitted.

## Open / deferred with trigger (milestone 1.0.9-b)
- **alias→positional mixed chain** (`...Join<X>(_, cond, Alias.Buyer).Join<Y>(_, cond)`): the engine cannot resolve a nested alias projection used as `Item1`, so the chain is refused at construction with an explicit `NotSupportedException` (`EntityBuilder.CreateJoined`) instead of a confusing `BuildSqlCommandException` during planning. Positional→alias chains remain supported. Trigger to lift the deferral: **owner-approved design for nested-projection alias inheritance**. Recorded evidence: `tests/nextorm.alias.tests/MixedJoinChainTests.Alias_then_positional_join_fails_closed_at_construction`.

## Iteration 2 ACT — Block A implemented, CHECK FAIL (acceptance gates open)
- Owner decisions fixed: generator shipped as analyzer in the `nextorm` package (verified: `analyzers/dotnet/cs/nextorm.core.sourcegenerator.dll`, consumer-reference e2e); generated types PUBLIC in `NextORM.Generated.<AssemblyName>`; prefixes `AliasJoin_`/`AliasProjection_`; marker `Alias`; STABLE (no `[Experimental]`).
- Implemented: single `JoinAlias` seam for the 7 projection operators (Join/Left/Right/Full/Cross/CrossApply/OuterApply) with conditionless + correlated APPLY; incremental generator with operator→JoinType mapping, arity cap 8, diagnostics NORMGEN001–006, deterministic emission; slot resolution via `JoinSlotAttribute` + memoized `ProjectionAliasCache.GetMemberPosition`; in-memory fail-closed; tests (`tests/nextorm.alias.tests` 31/31) + provider SQL-gen tests + integration `CommonTestSuite.JoinAlias.cs` (Postgres/SqlServer/MySql/Sqlite 7/7); EN/RU docs; packaging.
- Loop-back fixes (after CHECK #1 FAIL): ItemN fast-path before attribute probe + per-`MemberInfo` memoization; `Where` before/around a second alias join (real defect found + fixed, `AliasJoinWhereTests.cs`); positional→alias mixed chain works (generator emits `JoinedEntityBuilder<...>` receivers); alias→positional fails closed at construction.
- Verification (fresh): `nextorm.slnx -c Debug` 0/0; alias tests 31/31; core 1215/1215; provider units all 0 failed (sqlite 866[865/1skip], postgres 669, sqlserver 545, mysql 249, mariadb 149, clickhouse 437); integration Total 2912 / 0 failed / 187 capability skips; perf acceptance 7 cases / 0 failed / ~50 s / cached-prepared ratio 2.01 vs baseline 1.87 (+7.6%, <20%); local coverage (without containers) overall 80.7% line / 73.7% branch.
- CHECK #2 verdict: FAIL — acceptance/evidence gates, NOT a functional failure. CHECK status in this cycle: FAIL (2 fails; both evidence/completeness-driven).
- RESOLVED (owner decision 2026-10-01) — join aliases are alias-only, `JoinInto` excluded: `JoinInto` gets no alias surface at all. The generator `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs` has no `JoinInto` handling (grep for `JoinInto` over the source generator: no match) and emits no alias receiver/overload for it; its `JoinOperators` set is the seven projection operators only. Nothing had to be removed.
- RESOLVED (owner decision 2026-10-01) — positional mixing removed: the generator no longer reconstructs a positional prefix (chain resolution accepts only a plain `EntityBuilder<T>` root or a generated `AliasJoin_*`), the dead `JoinedEntityBuilder<…>.JoinAlias<…>` overloads were removed, and `tests/nextorm.alias.tests/MixedJoinChainTests.cs` was deleted; the positional `ItemN` API itself is untouched. Join aliases are a self-contained chained API; alias→alias inheritance (an aliased join chained onto a previous alias chain) remains in scope and supported.
- Verification after the removals: `nextorm.slnx -c Debug` 0 Warning(s)/0 Error(s); `tests/nextorm.alias.tests` 29/29 pass; `tests/nextorm.core.tests` 1215/1215; `tests/nextorm.postgres.tests` 669/669; `tests/nextorm.sqlserver.tests` 545/545; PoC green.
- OPEN evidence work (before ACT can be considered complete for a future cycle): full matrix→test/guard `file:line` pack; full-scope coverage >=85/75 with a baseline (integration included); mutation testing on changed runtime logic; package-consumer certification.
- Remains open in milestone 1.0.9-b; nothing moved out.
- Worktree left UNCOMMITTED (autocommit not requested).

## Block A CLOSED (owner decision 2026-10-01)
- Owner resolved both scope decisions: the alias feature is ALIAS-ONLY (no mixing with positional joins; positional API untouched), and `JoinInto` is EXCLUDED from aliases. Feature surface = the 7 projection operators (Join/LeftJoin/RightJoin/FullJoin/CrossJoin/CrossApply/OuterApply).
- Resulting functional state (fresh): `nextorm.slnx -c Debug` 0 Warning(s)/0 Error(s); `tests/nextorm.alias.tests` 29/29; core 1215/1215; provider units 0 failed (postgres 669, sqlserver 545, sqlite 866, mysql 249, mariadb 149, clickhouse 437); integration `JoinAlias` 7/7 per provider (Postgres/SqlServer/MySql/Sqlite); perf acceptance 7 cases / 0 failed / ~50 s / cached-prepared ratio +7.6% (<20%); PoC green.
- CAVEAT — owner-accepted deviation from the CHECK gate: the final CHECK verdict was FAIL solely on acceptance/evidence gates, not on functionality. Still OPEN and tracked in milestone 1.0.9-b:
  * full variant-matrix -> test/guard evidence pack with file:line — DONE (see Evidence pack, item 1);
  * full-scope coverage >=85% line / >=75% branch INCLUDING integration containers — DONE (see Evidence pack, item 2; PASS with containers);
  * mutation testing of changed runtime logic (ProjectionAliasCache / MemberTranslator / EntityBuilder alias path) — BLOCKED (tooling; see Evidence pack, item 3);
  * OPEN — package-consumer certification beyond the single packed-consumer e2e check;
  * OPEN — API-freeze tooling (`PublicAPI.*` / ApiCompat) — tracked in `docs/specs/roadmap/todo_public_api_freeze.md`.
- Intentional limitations (documented EN/RU): alias members are expression-only (direct read throws `NotSupportedException`); `JoinInto` has no alias surface; aliases are alias-only; arity cap 8; SQL providers only (in-memory fail-closed).
- Status file retained because the tracked OPEN evidence items above remain.

## Evidence pack (items 1-3, 2026-10-01)
### Item 1 - variant matrix (done)
- Added `docs/specs/design/join-alias-variant-matrix.md`: every alias variant mapped to its test or runtime guard with `file:line` (7 operators, chain shape, Where interplay, plan cache, guards, generator diagnostics, frozen surface, cross-provider integration).
- Gaps recorded there (small, single-test each): (a) positional-after-alias guard `EntityBuilder.cs:3137` has no direct test; (b) correlated `CrossApply`/`OuterApply` alias has no positive SQL test; (c) `ProjectionAliasCache.TryParseItemPosition` digit-edge (e.g. `Buyer2`) untested.

### Item 2 - coverage WITH integration containers (done, PASS)
- Recipe (CI-equivalent): `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet tool run dotnet-coverage collect -s coverage.settings.xml -f cobertura -o tests/coverage/coverage.cobertura.xml "dotnet test --no-build --verbosity normal"` then `dotnet tool run reportgenerator`.
- Tests: 7220 total, 0 failed, 188 skipped (all providers ran); collection 1m42s.
- Overall: **Line 88.1% (>=85 PASS), Branch 78.8% (>=75 PASS)**. Assemblies: core 87.9, postgres 88.9, sqlite 89.3, sqlserver 94.3.
- Alias units: `ProjectionAliasCache` 96.9%, `JoinSlotAttribute` 100%, `AliasFromProjectionVisitor` 83.3%, `MemberTranslator` 86.3%, `EntityBuilder<TEntity>` 91.3/92.1%, `RebaseAliasProjectionVisitor` 81.2%.
- Artifacts: `tests/coverage/report/Summary.txt`, `tests/coverage/coverage.cobertura.xml`.

### Item 3 - mutation testing (BLOCKED - tooling, no trustworthy score)
- Tool: Stryker.NET 5.0.0 (global). Must run from the source-project dir (`src/nextorm.core`) with `-tp ../../tests/nextorm.alias.tests/nextorm.alias.tests.csproj`; solution-context runs select all 7189 tests.
- Scope: the alias-specific runtime units `ProjectionAliasCache.cs`, `AliasFromProjectionVisitor.cs`, `JoinSlotAttribute.cs` (whole-file mutation of the 700-line `MemberTranslator.cs` / 4100-line `EntityBuilder.cs` is out of scope for a targeted run).
- Result: mutation score 0.00%; Killed 0, Survived all; all 53 mutant test runs reported "success", 0 failed runs.
- Decisive evidence this is a tooling failure, not a test-quality finding: the mutant `ProjectionAliasCache.cs:46` (`position - 1` -> `position + 1`) survives, although `tests/nextorm.alias.tests/AliasProjectionShapeTests.cs:56` asserts `GetMemberPosition(Item2) == 1`. Coverage capture also failed ("It looks like the test coverage capture failed"). Stryker used its bundled **net8.0** `vstest.console` against the repo's **net10.0** xunit.v3 tests while `global.json` forces the Microsoft.Testing.Platform runner, so mutant activation/result surfacing does not work.
- Verdict: mutation score COULD NOT be produced on this toolchain; tracked as a tooling task (candidate: Stryker+MTP/VSTest integration), not a defect in the alias code. Logs: `/tmp/opencode/stryker.log`, `/tmp/opencode/stryker2.log`.
