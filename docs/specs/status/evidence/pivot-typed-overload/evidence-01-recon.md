# Evidence 01: typed `Pivot`/`Unpivot` overload — projection, alias, member-length, marker machinery

## Question

Designing a typed overload for SQL Server `Pivot`/`Unpivot` (today `Pivot` returns
`EntityBuilder<TableAlias>` and the user reads output columns by name, then calls `.Select`).
Proposal: `Pivot(agg, aggCol, forCol, (m, pv) => new { m.GroupCol, Q1 = pv.Value<decimal?>("1") })`
returning `EntityBuilder<TShape>` usable by `Where`/`OrderBy`/`Select`.
Gather raw facts on 10 points: (1) select/projection machinery, (2) member→column alias rule,
(3) translating member access on a projection/derived source, (4) `SqlFunctions.Column<T>`,
(5) `BindEntity<TEntity>`, (6) `TableAlias` accessor translation, (7) `MakePivot`/PIVOT SQL shape,
(8) UNPIVOT SQL + name/value columns, (9) named class/record projections, (10) expression rewriting
utilities.

## Commands

```
cwd: /home/alex/sources/nextorm
rg -n "SqlFunctions\.Column|Column<" src tests --glob '*.cs'
rg -n "class \w+.*: ExpressionVisitor" src/nextorm.core --glob '*.cs'
rg -n "SourceBinding" src --glob '*.cs'
rg -n "Select\(.*=> new [A-Z]" tests --glob '*.cs'
rg -n "ExactProjectionAliases" src --glob '*.cs'
```

Raw reads (paths + line ranges) are cited inline under `Facts`; no build/test was run (read-only
reconnaissance, no code changed).

## Facts

### 1. Select/projection machinery

- `src/nextorm.core/Builders/EntityBuilder.cs:331` —
  `public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)`
  ```csharp
  public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp)
  {
      EnsureNoJoinIntos(nameof(Select));
      EnsureNoEagerLoadState(nameof(Select));

      return SelectCore(exp);
  }
  ```
- `src/nextorm.core/Builders/EntityBuilder.cs:343` —
  `private QueryCommand<TResult> SelectCore<TResult>(Expression<Func<TEntity, TResult>> exp)`;
  the lambda is stored verbatim as the definition's `Exp` (line 347) and attached to the command's
  `From` (line 369):
  ```csharp
  var cmd = _dataProvider.CreateCommand<TResult>(new QueryDefinition
  {
      Exp = exp,
      SrcType = _sourceEntityType,
      ...
  });

  cmd.From = ResolveSource();
  ```
- `src/nextorm.core/Builders/EntityBuilder.cs:448` — the derived-source entry point:
  ```csharp
  public EntityBuilder<TResult> As<TResult>(Expression<Func<TEntity, TResult>> selector)
  {
      EnsureNoJoinIntos(nameof(As));
      EnsureNoEagerLoadState(nameof(As));

      return _dataProvider.From(Select(selector));
  }
  ```
- `src/nextorm.core/Query/QueryDefinition.cs:21` — `public LambdaExpression? Exp { get; init; }`
  (doc line 20: "Projection lambda of the query, or `null` when `SrcType` is used.").
- `src/nextorm.core/Query/QueryCommand.cs:25` — `protected LambdaExpression? _exp;`
  (doc line 24: "The original projection lambda, or `null` for a command without an explicit
  projection."); `:180` `_exp = definition.Exp;`; `:225` `Exp = _exp,`;
  `:342` `internal LambdaExpression? ProjectionExpression => _exp;`.
- The lambda is *retained as a `LambdaExpression`* and is *parsed into a column-spec list during
  preparation*: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:489`
  `private static (SelectExpression[]?, int) PrepareColumns(...)`; the projection-shape branches are
  `:631` (`cmd._exp.Body is NewExpression ...` → anonymous/named ctor),
  `:699` (single-column scalar), `:747` (`cmd._exp.Body is MemberInitExpression init`).
- `src/nextorm.core/Expressions/SelectExpression.cs:15-29` — the column spec class:
  ```csharp
  public sealed class SelectExpression
  {
      private readonly Type _realType;
      public readonly bool Nullable;
      /// <summary>The zero-based ordinal of the column in the result set.</summary>
      public int Index { get; set; }
      /// <summary>The target property name, or <c>null</c> for a positional projection.</summary>
      public string? PropertyName { get; set; }
      /// <summary>The expression that produces the value, or <c>null</c> for a plain entity column.</summary>
      public Expression? Expression { get; set; }
      /// <summary>The CLR type of the value produced.</summary>
      public Type PropertyType { get; set; }
      internal PropertyInfo? PropertyInfo { get; set; }
  ```
  Other members quoted for context: `OutputName` (`:126`), `IsProjectionOutputReference` (`:133`),
  `ProjectionItem` (`:117`), `IsLobStreaming` (`:92`).

### 2. Member→column alias rule

- `src/nextorm.core/DataContext/SqlBuilder.cs:548` — select-list rendering calls `MakeColumn` with
  `dontNeedAlias: !needAlias` and `renameAware: true`:
  ```csharp
  var (needAliasForColumn, column) = SqlSourceRenderer.MakeColumn(in _ctx, item, entityType, !needAlias, renameAware: true);
  ```
- `src/nextorm.core/DataContext/SqlBuilder.cs:554-561` — alias emission:
  ```csharp
  if (needAliasForColumn)
  {
      // A generated output name (an unaliased computed scalar) is an alias only for
      // a typed-CTE declaration body; anywhere else the column keeps its implicit
      // name (the alias text was null before, and MakeColumnAlias emits nothing).
      var alias = item.PropertyName ?? (_ctx.ExactProjectionAliases ? item.OutputName : null);
      selectBuilder.Append(_ctx.Dialect.MakeColumnAlias(alias, _ctx.KeywordCase));
  }
  ```
- `src/nextorm.core/DataContext/SqlSourceRenderer.cs:1143` — `MakeColumn`, alias decision `:1165-1186`:
  ```csharp
  var comparison = ctx.ExactProjectionAliases ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
  var needAliasForColumn = visitor.NeedAliasForColumn
      || (renameAware
          && visitor.ColumnName is not null
          && selExp.PropertyName is not null
          && !string.Equals(visitor.ColumnName, selExp.PropertyName, comparison))
      || (ctx.ExactProjectionAliases
          && renameAware
          && selExp.PropertyName is not null
          && selExp.Expression is not null
          && TypeFacts.UnwrapConvert(selExp.Expression) is ConstantExpression)
      || (ctx.ExactProjectionAliases
          && renameAware
          && selExp.PropertyName is null
          && selExp.OutputName is not null);
  ```
- `PropertyName` population: NewExpression branch `QueryPreparer.cs:673` `PropertyName = ctorParam.Name!`;
  MemberInit branch `:768` `PropertyName = binding.Member.Name!`; single scalar `:717`
  `PropertyName = (TypeFacts.UnwrapConvert(cmd._exp.Body) as MemberExpression)?.Member.Name`.
- `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:441-443`:
  ```csharp
  public virtual string MakeColumnAlias(string? colAlias, KeywordCase keywordCase = KeywordCase.Lower) => string.IsNullOrEmpty(colAlias)
      ? string.Empty
      : SqlKeywords.Of(keywordCase, " as ") + Escape(colAlias);
  ```
- `ExactProjectionAliases` is false for ordinary SELECTs; true only for a typed-CTE declaration body
  or a FOR JSON shape: `src/nextorm.core/DataContext/QueryPlanner.cs:545`
  `ExactProjectionAliases = queryCommand.JsonShapeMode && queryCommand.ForJsonClause is not null`,
  and `SqlSourceRenderer.cs:93` for `cte.TypedProjection`.

**Alias-nullability fact:** the alias is *not* always emitted. It is emitted only when the rendered
column name differs from `PropertyName` (case-insensitively, unless `ExactProjectionAliases`) or the
visitor set `NeedAliasForColumn`. When emitted, the alias equals `PropertyName` (the member name).
When not emitted, the output column's SQL name is the underlying rendered expression text.
Observed tests:
- `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:621` — `e.Select(x => new { x.Id })` →
  `"select id from simple_entity"` (member `Id`, physical `id`, case-insensitive equal → no alias).
- `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:632` — `new { Region = SqlFunctions.Column<int>(x, "region_id") }`
  → `"select region_id as 'Region' from simple_entity"` (renamed → alias equals member name;
  `SqlFunctions.Column` sets `ColumnName`, see §4).
- `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3074` — a `TableAlias` accessor projection
  `new { Category = t.GetString("category"), Q1 = t.GetNullableDecimal("[1]"), Q2 = t.GetNullableDecimal("[2]") }`
  renders `select category, [1], [2] from ...` — i.e. **no** `as [Category]` / `as [Q1]` because a
  `TableAlias` accessor does not set `ColumnName` (see §6). So for a pivot source the SQL output
  column names are `category`, `[1]`, `[2]`, not the anonymous member names.

### 3. Translating a member access on a projection/derived source

- `src/nextorm.core/Visitors/MemberTranslator.cs:329` — `internal static Expression? VisitMember(BaseExpressionVisitor visitor, MemberExpression node)`.
- `:350` branch `if (node.Expression?.Type == visitor.EntityType)`.
- `:629-691` the projection/derived member resolution, `:635-637`:
  ```csharp
  if (lambdaParameter.Type!.IsAssignableTo(typeof(IProjection))
      && node.Expression is MemberExpression
      && TryTranslateDerivedProjectionMember(visitor, node, hasTableAliasForColumn))
      return node;
  ```
- `src/nextorm.core/Visitors/MemberTranslator.cs:704` —
  `private static bool TryTranslateDerivedProjectionMember(BaseExpressionVisitor visitor, MemberExpression node, bool hasTableAliasForColumn)`;
  alias qualification `:806-815`:
  ```csharp
  if (!visitor.IsParamMode)
  {
      if (!hasTableAliasForColumn && !visitor.DontNeedAlias)
          visitor.Builder!.Append(visitor.AliasProvider!.FindAlias(found.Value.Index)).Append('.');

      if (needAliasForColumn)
          visitor.Builder!.Append(visitor.Dialect.MakeColumnReference(innerCol.PropertyName!));
      else
          visitor.Builder!.Append(column);
  }
  ```
- Plain lambda-parameter member (entity column) path `:363-391`, alias prefix `:385-386`
  `visitor.Builder!.Append(tableAliasForColumn).Append('.')`.
- Source alias is resolved through `AliasResolver` (`src/nextorm.core/Visitors/AliasResolver.cs:8`,
  `:31`): `GetAliasFromParam` → `AliasProvider.FindAlias(idx)`.
- Test `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:3001-3011`
  `ColumnByName_OnJoinProjection_ShouldQualifyWithTableAlias`:
  `sql.Should().Contain("where t2.somestring = 'x'");` and
  `sql.Should().Contain("t2.somestring as \`S\`");` — a projection member over `p.Item2` is qualified
  with the source alias (`t2.`).

### 4. `SqlFunctions.Column<T>`

- Full declaration, `src/nextorm.core/Query/SqlFunctions.cs:82-95`:
  ```csharp
  /// <summary>
  /// References a column of an entity source by its database name with an explicit CLR type, for
  /// entities that do not declare a property for the column (for example a wide ClickHouse table).
  /// The value is materialized as <typeparamref name="T"/>.
  /// </summary>
  /// <typeparam name="T">The CLR type of the column value.</typeparam>
  /// <param name="entity">The entity source the column belongs to (a query lambda parameter).</param>
  /// <param name="columnName">The database column name.</param>
  /// <remarks>
  /// Only valid inside a query expression. The in-memory provider has no column-name concept and
  /// rejects the query.
  /// </remarks>
  public static T Column<T>(object entity, string columnName) => throw new NotSupportedException(
      "SqlFunctions.Column can only be used inside a query expression; the in-memory provider does not support column-by-name access.");
  ```
- Recognised by `MethodInfo`/declaring type + name, not by instance:
  `src/nextorm.core/Visitors/NormSqlTranslator.cs:43-51` (`TryTranslateColumnReference`) and
  `:99-109` (`TranslateColumn`):
  ```csharp
  private static void TranslateColumn(BaseExpressionVisitor visitor, MethodCallExpression node)
  {
      if (node.Arguments is not [Expression entity, ConstantExpression { Value: string columnName }])
          throw new NotSupportedException(nameof(SqlFunctions.Column));

      if (!visitor.IsParamMode)
      {
          visitor.AppendColumnReference(entity, columnName);
          visitor.ColumnName = columnName;
      }
  }
  ```
  Note `visitor.ColumnName = columnName;` — a by-name projection carries its column name, so a
  rename alias is emitted (§2).
- Dispatch hook: `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:364`
  `if (NormSqlTranslator.TryTranslateColumnReference(this, node)) return node;`; the emitter
  `AppendColumnReference` is `BaseExpressionVisitor.cs:464-475`:
  ```csharp
  internal void AppendColumnReference(Expression source, string columnName)
  {
      var unwrapped = TypeFacts.UnwrapConvert(source);
      var target = ResolveColumnSource(unwrapped)
          ?? throw new BuildSqlCommandException(
              $"SqlFunctions.Column requires an entity source parameter (got '{source}').");

      if (_columnsProvider.HasAliases && !_dontNeedAlias)
          AppendColumnAlias(unwrapped, target);

      AppendIdentifier(columnName);
  }
  ```
- Tests/usages: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:630` (`region_id as 'Region'`),
  `:640` (`Column<int>(x, "id")` scalar → `select id from simple_entity`);
  `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:2995` (`RegionID as \`Region\``),
  `:3008` (`t2.somestring as \`S\``), `:3031` (non-source arg throws);
  `tests/nextorm.postgres.tests/SqlGenerationTests.cs:320`;
  `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:33,44`;
  `tests/nextorm.core.tests/InMemoryTests.cs:1021`.

### 5. `BindEntity<TEntity>`

- Full extension source, `src/nextorm.core/Builders/EntityBuilderExtensions.cs:62-82`:
  ```csharp
  public static EntityBuilder<TEntity> BindEntity<TEntity>(this EntityBuilder<TableAlias> source, IReadOnlyCollection<string> availableColumns)
  {
      ArgumentNullException.ThrowIfNull(source);
      ArgumentNullException.ThrowIfNull(availableColumns);

      var binding = new FromExpression.EntityBinding(typeof(TEntity), NormalizeColumns(availableColumns));

      if (typeof(TEntity) != typeof(TableAlias) && !IsRegisteredInMetadata(typeof(TEntity)))
          _ = DataContextExtensions.ResolveMetadata<TEntity>(source.DataProvider, null);

      return source.BindEntitySource<TEntity>(binding);
  }
  ```
- Carrier `src/nextorm.core/Expressions/FromExpression.cs:15-31`:
  ```csharp
  internal sealed class EntityBinding
  {
      public EntityBinding(Type entityType, IReadOnlyList<string> availableColumns)
      {
           EntityType = entityType;
           AvailableColumns = availableColumns;
      }

      /// <summary>The bound entity type.</summary>
      public Type EntityType { get; }

      /// <summary>The declared output columns (sorted, deduplicated, case-insensitive).</summary>
      public IReadOnlyList<string> AvailableColumns { get; }
  }
  ```
  Stored at `FromExpression.cs:199` `internal EntityBinding? SourceBinding { get; init; }`, attached via
  `:258-259` `WithEntityBinding`.
- Applier, `src/nextorm.core/Builders/EntityBuilder.cs:2321-2346`:
  ```csharp
  internal EntityBuilder<TResult> BindEntitySource<TResult>(FromExpression.EntityBinding binding)
  {
      ArgumentNullException.ThrowIfNull(binding);

      if (typeof(TResult) == typeof(TableAlias))
          throw new NotSupportedException("An entity binding cannot target TableAlias; bind a mapped entity type instead.");

      var raw = _from;
      var isRawSql = raw?.RawSqlSource is not null;
      var isNamedTable = raw is null && !string.IsNullOrEmpty(_table);
      if (!isRawSql && !isNamedTable)
          throw new NotSupportedException("BindEntity can only be applied to a direct FromSql or From(string) source.");

      if (HasCompositionState)
          throw new InvalidOperationException("BindEntity must be called immediately after FromSql/From(string), before any predicate, projection, join or other query operator.");

      var bound = isRawSql
          ? raw!.WithEntityBinding(binding)
          : new FromExpression(_table!).WithEntityBinding(binding);

      var dst = new EntityBuilder<TResult>(_dataProvider) { Logger = Logger };
      CopyProjectionIndependentStateTo(dst);
      dst._from = bound;
      dst._table = null;
      return dst;
  }
  ```
- What it does with the binding: it only drives **global query-filter injection** (and the plan key);
  no member-access rewriting was found. `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:1980`
  `if (cmd._from?.SourceBinding is { } binding)`; `:2022` `InjectBoundSourceFilters(...)`; `:2069`
  `TryGetBoundFilterBody(...)`. Filter compatibility uses
  `FilterColumnDependencyVisitor` (`:2330`) against `binding.AvailableColumns` (`:2105`). Cross/apply
  joins use it via `:2186,2196,2218`.
- `tests/nextorm.core.tests/RawSourceBindingTests.cs` test names (18):
  `BindEntity_NullSource_ShouldThrow` (:27), `BindEntity_NullColumns_ShouldThrow` (:37),
  `BindEntity_BlankColumn_ShouldThrow` (:50), `BindEntity_NullColumnEntry_ShouldThrow` (:61),
  `BindEntity_EmptyColumns_ShouldBeAllowed` (:72), `BindEntity_TableAlias_ShouldThrowNotSupported` (:83),
  `BindEntity_MappedTableAliasSubclass_ShouldSucceed` (:94), `BindEntity_CteOnlySource_ShouldSucceed` (:108),
  `BindEntity_SubQueryHintOnly_ShouldSucceed` (:120), `BindEntity_CteAndSubQueryHint_ShouldSucceed` (:132),
  `BindEntity_CteWithComposedPredicate_ShouldStillReject` (:146), `BindEntity_NonRawSource_ShouldThrowNotSupported` (:158),
  `BindEntity_AfterWhere_ShouldThrowInvalidOperation` (:169), `BindEntity_AfterOrderBy_ShouldThrowInvalidOperation` (:180),
  `BindEntity_ShouldReturnTypedBuilder_AndCarryBinding` (:191), `BindEntity_ShouldNotMutateOriginal` (:205),
  `BindEntity_ShouldDeduplicateSortAndIgnoreCase` (:216), `BindEntity_FromNamedTable_ShouldCarryBindingAndTable` (:227).

### 6. `TableAlias` accessor translation

- Full file `src/nextorm.core/Visitors/TableAliasAccessors.cs` (69 lines):
  ```csharp
  internal static class TableAliasAccessors
  {
      private static readonly FrozenSet<string> s_accessors = BuildAccessors();
      private static readonly string s_getColumn = nameof(TableAlias.GetColumn);
      private static readonly FrozenSet<string> s_streamingAccessors = BuildStreamingAccessors();
      private static readonly FrozenSet<string> s_streamingMembers = BuildStreamingMembers();

      internal static bool IsAccessor(string methodName) => s_accessors.Contains(methodName);

      internal static bool IsStreamingAccessor(string methodName) => s_streamingAccessors.Contains(methodName);

      internal static bool IsStreaming(Expression expression)
      {
          switch (TypeFacts.UnwrapConvert(expression))
          {
              case MethodCallExpression call:
                  return call.Method.DeclaringType == typeof(TableAlias) && IsStreamingAccessor(call.Method.Name);
              case MemberExpression member:
                  return member.Member.DeclaringType == typeof(TableColumn) && s_streamingMembers.Contains(member.Member.Name);
              default:
                  return false;
          }
      }

      private static FrozenSet<string> BuildStreamingAccessors()
          => new[] { nameof(TableAlias.GetStream), nameof(TableAlias.GetTextReader) }.ToFrozenSet(StringComparer.Ordinal);

      private static FrozenSet<string> BuildStreamingMembers()
          => new[] { nameof(TableColumn.AsStream), nameof(TableColumn.AsTextReader) }.ToFrozenSet(StringComparer.Ordinal);

      internal static bool AllowsExpression(string methodName) => methodName == s_getColumn;

      private static FrozenSet<string> BuildAccessors()
      {
          var names = new HashSet<string>(StringComparer.Ordinal);
          foreach (var method in typeof(TableAlias).GetMethods(BindingFlags.Public | BindingFlags.Instance))
              names.Add(method.Name);

          return names.ToFrozenSet(StringComparer.Ordinal);
      }
  }
  ```
- Rendering, `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:442-456`:
  ```csharp
  private void EmitTableAliasColumn(MethodCallExpression node)
  {
      if (!TableAliasAccessors.IsAccessor(node.Method.Name))
          throw new NotSupportedException(node.Method.Name);

      if (_columnsProvider.HasAliases && !_dontNeedAlias)
          AppendColumnAlias(node.Object!);

      if (node.Arguments is [ConstantExpression constExp])
          AppendIdentifier(constExp.Value?.ToString() ?? string.Empty);
      else if (TableAliasAccessors.AllowsExpression(node.Method.Name) && node.Arguments is [Expression exp])
          AppendIdentifier(CompileExpression(exp));
      else
          throw new NotSupportedException(node.Method.Name);
  }
  ```
- **Exact mapping fact:** there is *no* per-accessor SQL switch. Every accessor (any public instance
  method of `TableAlias`, including the indexer `get_Item`) is recognised by *name* via
  `IsAccessor`, and its constant string argument is emitted as an identifier via `AppendIdentifier`
  (`BaseExpressionVisitor.cs:335-341`, quotes when `_options.QuoteIdentifiers`). The method name only
  (a) gates recognition and (b) determines the CLR return type used for materialization
  (`SelectExpression.PropertyType`). Only `GetColumn` additionally allows a non-constant computed
  column expression (`AllowsExpression`), evaluated via `CompileExpression`
  (`BaseExpressionVisitor.cs:646-661`). The accessor body never runs.
  `TableAlias` methods quoted from `src/nextorm.core/Builders/TableAlias.cs:12-138`
  (`GetInt32`, `GetInt64`, `GetInt16`, `GetString`, `GetSingle`, `GetDouble`, `GetDateTime`,
  `GetDecimal`, `GetByte`, `GetBoolean`, `GetGuid`, `GetBytes`, `GetStream`, `GetTextReader`,
  `GetNullableInt32/64/16`, `GetNullableString`, `GetNullableSingle`, `GetNullableDouble`,
  `GetNullableDateTime`, `GetNullableDecimal`, `GetNullableByte`, `GetNullableBoolean`,
  `GetNullableGuid`, `GetNullableBytes`, `GetColumn`, indexer `this[string]`).
- Dispatching call site: `BaseExpressionVisitor.cs:382-388`
  ```
  if (node.Object?.Type == typeof(TableAlias))
  {
      if (!_paramMode)
          EmitTableAliasColumn(node);

      return node;
  }
  ```

### 7. `MakePivot` / PIVOT SQL shape

- `src/nextorm.core/DataContext/SqlSourceRenderer.cs:986-1020` (`MakePivot`):
  ```csharp
  private static string MakePivot(in SqlBuildContext ctx, FromExpression from)
  {
      var pivot = from.Pivot!;

      if (ctx.Dialect.Pivot is not { } pivotRenderer)
          throw new NotSupportedException(pivot.IsUnpivot
              ? "The UNPIVOT source construct is not supported by this provider."
              : "The PIVOT source construct is not supported by this provider.");

      var source = MakeFrom(in ctx, pivot.Inner, new FromRenderOptions(false, pivot.InnerEntityType, false, null, null));

      var aggregateColumn = string.Empty;
      var forColumn = string.Empty;
      if (!pivot.IsUnpivot)
      {
          aggregateColumn = RenderPivotColumn(in ctx, pivot.InnerEntityType, pivot.AggregateColumn!);
          forColumn = RenderPivotColumn(in ctx, pivot.InnerEntityType, pivot.ForColumn!);
      }

      if (ctx.ParamMode) return string.Empty;

      ctx.ColumnsProvider.Add(typeof(TableAlias), false);

      var alias = ctx.AliasProvider!.GetNextAlias(from);
      return pivot.IsUnpivot
          ? pivotRenderer.RenderUnpivot(pivot, source, alias, ctx.KeywordCase)
          : pivotRenderer.RenderPivot(pivot, source, aggregateColumn, forColumn, alias, ctx.KeywordCase);
  }
  ```
- `src/nextorm.sqlserver/SqlServerDialect.cs:827-843` (`SqlServerPivotRenderer.RenderPivot`):
  ```csharp
  internal sealed class SqlServerPivotRenderer(SqlServerDialect dialect) : IPivotRenderer
  {
      public string RenderPivot(PivotExpression pivot, string source, string aggregateColumn, string forColumn, string alias, KeywordCase keywordCase = KeywordCase.Lower)
      {
          var sb = new StringBuilder(source);
          sb.Append(SqlKeywords.Of(keywordCase, " pivot (")).Append(pivot.Aggregate.ToString().ToLowerInvariant()).Append('(')
            .Append(aggregateColumn).Append(SqlKeywords.Of(keywordCase, ") for ")).Append(forColumn).Append(SqlKeywords.Of(keywordCase, " in ("));

          for (var i = 0; i < pivot.Values.Count; i++)
          {
              if (i > 0) sb.Append(", ");
              sb.Append(dialect.Escape(pivot.Values[i].Value));
          }

          sb.Append("))").Append(dialect.MakeTableAlias(alias, keywordCase));
          return sb.ToString();
      }
  ```
  `MakeTableAlias` default `SqlDialectBase.cs:438-439` → `" as " + Escape(tableAlias)`.
- Test `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3060-3075` (`Pivot_ShouldEmitPivotClause`):
  ```csharp
  SqlOf(ctx, e
      .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
          PivotValue.Create("1"), PivotValue.Create("2"))
      .Select(t => new
      {
          Category = t.GetString("category"),
          Q1 = t.GetNullableDecimal("[1]"),
          Q2 = t.GetNullableDecimal("[2]")
      }))
      .Should().Be("select category, [1], [2] from sales pivot (sum(margin) for quarter in ([1], [2])) as [t1]");
  ```
  Derived-computed variant `:3182`:
  ```
  "select CategoryName, [Q1] from (select category as [CategoryName], ('Q'+quarter) as [QuarterNum], (margin * cast(2 as decimal(38, 10))) as [Margin] from sales\n where (margin > cast(0 as decimal(38, 10)))) as [t1] pivot (sum([Margin]) for [QuarterNum] in ([Q1])) as [t2]"
  ```
- **Qualification fact:** in the enclosing SELECT the grouping column and the pivoted columns are
  rendered **bare/unqualified** (`category`, `[1]`, `[2]`, `CategoryName`, `[Q1]`), even though the
  pivot source in FROM is aliased (`as [t1]`/`as [t2]`). Reason in code: `MakeSelect` computes
  `needAlias` = `hasJoins || outer refs || dynamic store || source lock`
  (`SqlBuilder.cs:98-100`), and passes `dontNeedAlias: !needAlias` into `MakeColumn` (`:548`);
  with no joins that is `true`, so `EmitTableAliasColumn`'s `!_dontNeedAlias` guard
  (`BaseExpressionVisitor.cs:447`) suppresses the alias prefix. (The FROM alias is still emitted
  because `MakePivot` calls `AliasProvider.GetNextAlias(from)` itself.)
- Pivot builder (return type): `src/nextorm.sqlserver/Extensions/SqlServerEntityBuilderExtensions.cs:30`
  `public static EntityBuilder<TableAlias> Pivot<TEntity>(...)` → line 45
  `return new EntityBuilder<TableAlias>(builder.DataProvider) { Logger = builder.Logger, SourceFrom = new FromExpression(spec) };`
- `src/nextorm.core/Query/PivotExpression.cs:76-147` — `PivotExpression` fields
  (`Inner`, `InnerEntityType`, `IsUnpivot`, `Aggregate`, `AggregateColumn`, `ForColumn`, `Values`,
  `UnpivotValueColumn`, `UnpivotNameColumn`, `Columns`); factories `:102` `ForPivot`, `:111` `ForUnpivot`.

### 8. Unpivot SQL + name/value columns

- `src/nextorm.sqlserver/SqlServerDialect.cs:845-859` (`RenderUnpivot`):
  ```csharp
  public string RenderUnpivot(PivotExpression pivot, string source, string alias, KeywordCase keywordCase = KeywordCase.Lower)
  {
      var sb = new StringBuilder(source);
      sb.Append(SqlKeywords.Of(keywordCase, " unpivot (")).Append(dialect.Escape(pivot.UnpivotValueColumn!)).Append(SqlKeywords.Of(keywordCase, " for "))
        .Append(dialect.Escape(pivot.UnpivotNameColumn!)).Append(SqlKeywords.Of(keywordCase, " in ("));

      for (var i = 0; i < pivot.Columns.Count; i++)
      {
          if (i > 0) sb.Append(", ");
          sb.Append(dialect.Escape(pivot.Columns[i].Column));
      }

      sb.Append("))").Append(dialect.MakeTableAlias(alias, keywordCase));
      return sb.ToString();
  }
  ```
- Test `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3136-3150` (`Unpivot_ShouldEmitUnpivotClause`):
  ```csharp
  SqlOf(ctx, e
      .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
      .Select(t => new
      {
          Category = t.GetString("category"),
          Qtr = t.GetString("qtr"),
          Val = t.GetNullableDecimal("val")
      }))
      .Should().Be("select category, qtr, val from quarterly unpivot ([val] for [qtr] in ([q1], [q2])) as [t1]");
  ```
  Derived variant `:3195`: `"select category, val from (select category, q1, q2 from quarterly) as [t1] unpivot ([val] for [qtr] in ([q1], [q2])) as [t2]"`.
- **Name/value column naming:** the value column is the caller-supplied `valueColumnName` (here `"val"`,
  escaped `[val]`) and the name column is `nameColumnName` (`"qtr"`, escaped `[qtr]`), rendered by
  `RenderUnpivot` as `unpivot ([val] for [qtr] in (...))`. The projected `Qtr`/`Val` are read back by
  name `t.GetString("qtr")` / `t.GetNullableDecimal("val")`.
- `Unpivot` builder signature: `SqlServerEntityBuilderExtensions.cs:59-63`
  `public static EntityBuilder<TableAlias> Unpivot<TEntity>(this EntityBuilder<TEntity> builder, string valueColumnName, string nameColumnName, params UnpivotColumn[] columns)`.

### 9. Named class / record projections

- Supported. The `MemberInitExpression` branch handles named-class object-initializer projections:
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:747-790`; per member
  `:768` `PropertyName = binding.Member.Name!`.
- The `NewExpression` branch handles positional records/DTOs (constructor): `:631-698`.
- Tests over an entity/derived/raw source:
  - `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:99` `SelectTableIntoDTO_ShouldReturnData` —
    `_sut.From("simple_entity").Select(tbl => new SimpleEntityDTO(tbl.GetInt64("id")))`.
  - `:117` `SelectEntityIntoRecord_ShouldReturnData` —
    `_sut.From("simple_entity").Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")))`
    (`internal record SimpleEntityRecord(long Id)` — `tests/nextorm.integration.tests/TestModels.cs:12`).
  - `:126` `SelectNestedEntityIntoRecord_ShouldReturnData` —
    `var nested = _sut.From("simple_entity").Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")));`
    then `_sut.From(nested).Select(rec => new { rec.Id })` — a named-record derived source re-projected
    by member access.
  - `tests/nextorm.integration.tests/IdentityReturningIntegrationTests.cs:598` —
    `.Select(x => new IdnDerivedShape { Id = x.Id, Name = x.Name, Age = x.Age })` (MemberInit over a derived shape).
  - `tests/nextorm.core.tests/Iteration14CteLookupTests.cs:371` — `new CteIdRow { id = t["id"].AsInt }`.
  - `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:3172` — `Select(s => new { CategoryName = s.Category, QuarterNum = "Q" + s.Quarter, Margin = s.Margin * 2 })` (anonymous, over entity) feeds the derived pivot source.

### 10. Expression rewriting utilities

- Public rewriters:
  - `src/nextorm.core/Visitors/ReplaceExpressionVisitor.cs:8`
    `public class ReplaceParameterExpressionVisitor : ExpressionVisitor` — replaces any parameter
    whose *type* matches (`:22-27` `if (_parameter.Type == node.Type) return _parameter;`); matches by
    type, not by reference.
  - `src/nextorm.core/Visitors/ReplaceExpressionVisitor.cs:38`
    `public class ReplaceConstantExpressionVisitor : ExpressionVisitor` — `VisitConstant` type match.
  - `src/nextorm.core/ExpressionExtensions.cs:58` `public class TypeExpressionVisitor<T> : ExpressionVisitor`
    (records first node of T); `:95` `TypeExpressionVisitor<T1,T2>`;
    `:143` `public class ReplaceConstantsExpressionVisitor : ExpressionVisitor` (parametrises
    constants / outer references).
- Internal/private rewriters (all `: ExpressionVisitor`):
  - `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:796` `TargetParameterReplacer(ParameterExpression target, Expression replacement)`.
  - `src/nextorm.core/Builders/EntityBuilder.cs:3408` `RebaseAliasProjectionVisitor(ParameterExpression source, ParameterExpression target, Type targetType)`.
  - `src/nextorm.core/Builders/EntityBuilder.cs:3690` `ReplaceTargetParameterVisitor(ParameterExpression target, Expression replacement)`.
  - `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:2571` `ReplaceParameterInstanceVisitor(ParameterExpression target, Expression replacement)`.
  - `src/nextorm.core/DataContext/Meta/QueryFilterValidator.cs:492` `ReplaceParameterVisitor(Expression from, Expression to)`.
  - `src/nextorm.core/Query/InValues.cs:95` `ReplaceInstanceConstantVisitor(ConstantExpression target, Expression replacement)`.
  - `src/nextorm.core/Query/QueryFilterContext.cs:269` `ContextAccessReplacer(ParameterExpression parameter)`.
  - `src/nextorm.core/DataContext/InMemoryCorrelatedPlan.cs:309` `OuterReferenceToParameterVisitor`.
  - `src/nextorm.core/Visitors/NavigationExpansion.cs:522` `RewriteVisitor(ExpansionState state, ParameterExpression root)`.

## Not found

- No per-accessor SQL mapping/switch for `TableAlias` methods: `EmitTableAliasColumn` is
  name-agnostic (all accessors emit their constant argument as an identifier); the mapping is
  recognition + CLR result type only. Searched `src/nextorm.core/Visitors/*.cs`
  (`TableAliasAccessors`, `EmitTableAliasColumn`).
- No expression rewriting of name-based accesses into typed entity member accesses inside
  `BindEntity`/`EntityBinding`: `EntityBinding` carries `(EntityType, AvailableColumns)` and is used
  for filter injection only; searched `SourceBinding` across `src` (all hits listed in §5).
- No test that feeds a `Pivot`/`Unpivot` command back into `From(...)` as a derived subquery (to
  observe whether the enclosing SELECT would alias the pivot columns to the projection member names).
  Existing pivot tests project directly over the pivot source, so the output column names observed are
  the rendered expressions (`category`, `[1]`), not the anonymous member names. Searched
  `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs` and `tests/nextorm.integration.tests/SqlServerSpecificTests.cs`
  for `Pivot`/`Unpivot` usages.

## Open questions

- Does a `Pivot` over a `TableAlias` source produce a consumable *derived* source (an outer
  `From(pivotCommand).Select(...)` by member name), given that the pivot projection emits no
  `as <MemberName>` alias? No test covers it.
- Is `visitor.ColumnName` intentionally left unset by `EmitTableAliasColumn` (so by-name accessors
  never rename-alias), while `SqlFunctions.Column` sets it? Confirmed in code (§4, §6); the design
  intent is not stated in comments.
- What is the intended wiring for a typed overload that must rewrite `(m, pv) => shape` into a select
  over the `TableAlias` pivot source — is `ReplaceParameterExpressionVisitor` (type-based, §10)
  sufficient, or is a reference-based rewriter required? No existing helper maps a two-parameter lambda
  onto a pivot source; facts only.
