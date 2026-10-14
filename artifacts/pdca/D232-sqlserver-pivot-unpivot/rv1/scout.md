# D-01 scout — preparation facts (T232 / #232)

Source of truth for the raw reconnaissance (read-only, no code changed):
`docs/specs/status/evidence/pivot-typed-overload/evidence-01-recon.md`. This file records
only the facts D-01 pins as preparation probes, plus the recon checks D-01 adds.

## Name-resolution route (AC-08, E232-01)

- Public naming helper: `MemberInfoExtensions.GetPropertyColumnName` —
  `src/nextorm.core/MemberInfoExtensions.cs:44`. Declared names are returned verbatim;
  auto-derived names are translated through the active `INamingConvention`
  (`:53-60`). Empty string when the member's type is not registered in
  `DataContextCache.Metadata`.
- Metadata registry: `DataContextCache.Metadata` (`src/nextorm.core/DataContext/DataContextCache.cs`),
  populated lazily by `From&lt;T&gt;()`; the resolved column name is cached per
  `(PropertyInfo, INamingConvention?)` (`MemberInfoExtensions.cs:15`).
- By-name translation of a source column: `SqlFunctions.Column&lt;T&gt;` →
  `NormSqlTranslator.TranslateColumn` sets `visitor.ColumnName` (`src/nextorm.core/Visitors/NormSqlTranslator.cs:99-109`);
  `BaseExpressionVisitor.AppendColumnReference` resolves the source parameter
  (`ResolveColumnSource`, `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:463-475,510-522`).
- Alias emission for a select-list column (`SqlBuilder.cs:548-561`,
  `SqlSourceRenderer.cs:1165-1186`): the rename alias is emitted only when
  `visitor.ColumnName` differs from `SelectExpression.PropertyName` (case-insensitive unless
  `ExactProjectionAliases`), or when the visitor set `NeedAliasForColumn`.

## PIVOT/UNPIVOT source registration (unknown 4)

- `SqlSourceRenderer.MakePivot` (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:986-1020`)
  registers the pivot source as a `TableAlias` provider — `ctx.ColumnsProvider.Add(typeof(TableAlias), false)`
  (`:1014` from the earlier recon; the call is inside `MakePivot`) — before the enclosing
  select list is rendered, so a by-name column over the pivot source binds.
- Legacy exact SQL (must stay): `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3074`.

## D-01 probe outcome

`tests/nextorm.sqlserver.tests/T232PreparationProbeTests.cs` — 6 deterministic probes, all
passing (see `logs/C01-preparation.out`):

- mapped name resolved at the extension-call lifecycle (declared + naming convention);
- an unregistered entity resolves to the empty string (never silently defaulted);
- existing PIVOT + `SqlFunctions.Column&lt;T&gt;` → `select category, [1] as [Q1], [2] as [Q2] ...`
  (rename alias emitted; the source parameter resolves — no "entity source parameter" failure);
- existing UNPIVOT + `SqlFunctions.Column&lt;T&gt;` → `select category, qtr as [Quarter], val as [Measure] ...`;
- a non-source argument fails closed with `BuildSqlCommandException`.
