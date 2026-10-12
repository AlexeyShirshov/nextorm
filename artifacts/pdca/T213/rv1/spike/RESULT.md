# T213 D02-P1 — bounded carrier spike RESULT: **FAILED (STOP)**

- UTC: 2026-10-12T14:10:16Z
- Scope: harness-only experiment authorized by the r=1 reconnaissance decision ("only a bounded
  harness-only experiment — not a speculative new runtime abstraction"). No production source changed.
- Question (escalate binding decision, direction B): does a `Projection<TJunction, long>` link item
  (`Item1` = junction entity, `Item2` = `row_number()`) flow through
  `QueryPreparer.TryExpandEntityItem` / `TryBuildProjectionSelectList` and
  `RowMaterializerBuilder.BuildProjectionItems` when the derived link command is used as a
  `FromExpression` inside the JoinInto pair command?

## Answer: NO — the mechanism does not work. Exact failures

Harness source: `JoinIntoCompositeCarrierSpikeTests.cs` (archived here). Logs: `spike2.log`
(final, parent metadata registered), `spike.log` (first run).

### (1) Derived link command cannot materialize an entity-typed `Item1`
Command: `ctx.CreateCommand(Projection<TJunction, long>, { Exp = j => new Projection<TJunction,long>
{ Item1 = j, Item2 = row_number().Over(...) }, SrcType = TJunction, ProjectionType = Projection<...> })`,
`From = new FromExpression("spike_junction")`, prepared via `GetPreparedQueryCommand`.

- Exit: 2. Failure:
  `System.NotSupportedException : Property 'Item1' with index (0) has type
  NextORM.Sqlite.Tests.SpikeCarrierJunction which is not supported`
  at `SelectExpression.GetDataRecordMethod` ← `RowMapperFactory.GetReaderAccessor` ←
  `RowMapperFactory.MapColumn` ← `RowMaterializerBuilder.BuildMemberInit`
  (`src/nextorm.core/DataContext/RowMaterializerBuilder.cs:465`).
- Meaning: the `MemberInitExpression` branch
  (`QueryCommand.QueryPreparer.cs:685-728`) emits one column per binding; the `Item1` column keeps the
  junction entity CLR type and is never flattened into the junction's mapped columns. Nothing calls
  `BuildProjectionItems` for it.

### (2) Outer pair command does not expand the `Projection<TJunction,long>` link item
Command: `ctx.CreateCommand(Projection<TEntity, Projection<TJunction,long>>,
{ SrcType = TEntity, ProjectionType = outer, Joins = [ left join whose From = new FromExpression(derived) ] })`,
prepared via `GetPreparedQueryCommand`.

- Exit: 2. Failure:
  `System.ArgumentException : 'Id' is not a member of type
  'NextORM.Core.Projection`2[SpikeCarrierParent, Projection`2[SpikeCarrierJunction,long]]'
  (Parameter 'bindings[0]')`
  at `Expression.MemberInit` ← `RowMaterializerBuilder.BuildMemberInit`
  (`src/nextorm.core/DataContext/RowMaterializerBuilder.cs:468`).
- Root cause: `TryExpandEntityItem(Projection<TJunction,long>, ...)` returns `false` because
  `DataContextCache.Metadata` has no entry for the projection type
  (`QueryCommand.QueryPreparer.cs:1627`). `TryBuildProjectionSelectList` therefore returns `false`
  (`:1448-1450`), the `srcType`-metadata fallback at `:792-838` builds the parent's entity columns
  instead, and the row materializer cannot bind those parent columns onto the outer projection type.
- First run (parent metadata not registered) failed even earlier at
  `QueryCommand.QueryPreparer.cs:836` with `QueryPreparationException : Select must return new
  anonymous type` — the same fallback with no parent columns.

## Conclusion / routing

The escalate decision's specified carrier does **not** flow through the existing expander/mapper
machinery. Per the binding decision: STOP; do not silently invent a different carrier. This decision
routes back to `planner`.

Imports/reproducibility: the archived harness is a standalone xunit test; drop it into
`tests/nextorm.sqlite.tests/` to reproduce (`dotnet test tests/nextorm.sqlite.tests -c Debug
--filter FullyQualifiedName~JoinIntoCompositeCarrierSpikeTests`).
