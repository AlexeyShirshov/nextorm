using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.SqlServer;

/// <summary>
/// SQL Server-only fluent members of <c>EntityBuilder&lt;TEntity&gt;</c>, removed from the common
/// <c>nextorm</c> package and re-exposed here as extension methods. Add
/// <c>using NextORM.SqlServer;</c> to keep the fluent chain unchanged on a SQL Server provider.
/// The render-time dialect gating (and the thrown exceptions) is unchanged.
/// </summary>
public static class SqlServerEntityBuilderExtensions
{
    /// <summary>
    /// Reshapes this query's source into columns with the native <c>PIVOT</c> operator: for every
    /// distinct <paramref name="forColumn"/> value in <paramref name="values"/> a result column is
    /// produced from <paramref name="aggregate"/> of <paramref name="aggregateColumn"/>. The result is
    /// an untyped source (<see cref="TableAlias"/>): select the grouping columns and the pivoted columns
    /// by name. Each result column is named by its pivot value; the value string is a <b>raw</b> name
    /// that is always safely quoted, so read it through <c>SqlFunctions.Column&lt;T&gt;</c> in the
    /// projection (<c>Q1 = SqlFunctions.Column&lt;decimal?&gt;(t, "1")</c>) rather than pre-quoting it.
    /// Requires a dialect that supports it (see <c>ISqlDialect.Pivot</c>). The source may be a plain
    /// table/entity, a table-valued function or a derived query (<c>From(query)</c>); filters and other
    /// modifiers belong to the reshaped result (or, for a derived source, inside the derived query).
    /// </summary>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="aggregate">The aggregate applied to each cell (<c>SUM</c>/<c>COUNT</c>/<c>AVG</c>/<c>MIN</c>/<c>MAX</c>).</param>
    /// <param name="aggregateColumn">The column expression aggregated into each cell.</param>
    /// <param name="forColumn">The column whose values become the result columns.</param>
    /// <param name="values">The pivot values; each value names its result column and is a raw name, safely quoted independently of the command's <c>QuoteIdentifiers</c> option.</param>
    public static EntityBuilder<TableAlias> Pivot<TEntity>(
        this EntityBuilder<TEntity> builder,
        PivotAggregate aggregate,
        Expression<Func<TEntity, object?>> aggregateColumn,
        Expression<Func<TEntity, object?>> forColumn,
        params PivotValue[] values)
    {
        ArgumentNullException.ThrowIfNull(aggregateColumn);
        ArgumentNullException.ThrowIfNull(forColumn);
        ArgumentNullException.ThrowIfNull(values);
        if (values.Length == 0)
            throw new ArgumentException("A PIVOT requires at least one value.", nameof(values));
        builder.EnsureNoEagerLoadState(nameof(Pivot));

        var spec = PivotExpression.ForPivot(ResolvePivotInner(builder), typeof(TEntity), aggregate, aggregateColumn, forColumn, values);
        return new EntityBuilder<TableAlias>(builder.DataProvider) { Logger = builder.Logger, SourceFrom = new FromExpression(spec) };
    }

    /// <summary>
    /// Reshapes this query's source with the native <c>UNPIVOT</c> operator: the listed columns are
    /// stacked into two result columns (<paramref name="valueColumnName"/> holding the value and
    /// <paramref name="nameColumnName"/> holding the originating column name). Requires a dialect that
    /// supports it (see <c>ISqlDialect.Pivot</c>). The source may be a plain table/entity or a derived
    /// query (<c>From(query)</c>).
    /// </summary>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="valueColumnName">The raw name of the output column that receives the values; safely quoted independently of the command's <c>QuoteIdentifiers</c> option.</param>
    /// <param name="nameColumnName">The raw name of the output column that receives the source column name; safely quoted independently of the command's <c>QuoteIdentifiers</c> option.</param>
    /// <param name="columns">The source columns to unpivot; each column's raw name becomes its name-column value and is safely quoted.</param>
    public static EntityBuilder<TableAlias> Unpivot<TEntity>(
        this EntityBuilder<TEntity> builder,
        string valueColumnName,
        string nameColumnName,
        params UnpivotColumn[] columns)
    {
        ArgumentException.ThrowIfNullOrEmpty(valueColumnName);
        ArgumentException.ThrowIfNullOrEmpty(nameColumnName);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
            throw new ArgumentException("An UNPIVOT requires at least one column.", nameof(columns));
        builder.EnsureNoEagerLoadState(nameof(Unpivot));

        var spec = PivotExpression.ForUnpivot(ResolvePivotInner(builder), typeof(TEntity), valueColumnName, nameColumnName, columns);
        return new EntityBuilder<TableAlias>(builder.DataProvider) { Logger = builder.Logger, SourceFrom = new FromExpression(spec) };
    }

    /// <summary>
    /// Reshapes this query's source into columns with the native <c>PIVOT</c> operator and projects the
    /// typed result shape in one declaration: <paramref name="projection"/> declares the result members
    /// (a grouping member is a direct member of the inner source) and the CLR type of every pivoted cell
    /// (<c>pivot.Value&lt;decimal?&gt;("1")</c>). The declared value order defines the <c>IN (...)</c>
    /// list, so each value is declared exactly once.
    /// <para>
    /// Each value is the data value matched by the pivoted column (the same string
    /// <c>PivotValue.Create</c> takes), not its SQL identifier: the identifier is derived from it the way
    /// the renderer escapes the <c>IN</c> list. The value string is a <b>runtime identifier</b> with no
    /// compile-time membership guarantee — it is never validated against the data. A value absent from
    /// the data still renders its column but materializes as SQL <c>NULL</c> for every row, so declare the
    /// cell nullable when that is possible; an invalid name or an incompatible explicit cell type can
    /// fail during query preparation or materialization. A cell whose type is non-nullable against a SQL
    /// NULL behaves like the matching <see cref="TableAlias"/> accessor; the nullability is never
    /// inferred. A truly dynamic PIVOT whose value list is known only at runtime is outside this typed
    /// overload and stays with the untyped <see cref="TableAlias"/> path.
    /// </para>
    /// <para>
    /// The returned builder is a full source over the typed projection: its members are usable in
    /// <c>Where</c>/<c>OrderBy</c>/<c>Select</c> and it can be consumed as a derived source or CTE. Like
    /// every derived source, materializing it requires an explicit <c>Select</c> (an anonymous/record
    /// shape has no entity mapping to fall back on).
    /// </para>
    /// <para>
    /// Requires a dialect that supports <c>PIVOT</c> (SQL Server); other providers reject the query when
    /// it renders, exactly like the untyped overload. The projection is fail-closed: a duplicate value,
    /// a projection without values, a value name that is not a compile-time or captured constant, and a
    /// grouping member that is also the aggregate or <c>FOR</c> column all throw
    /// <see cref="ArgumentException"/> instead of emitting wrong SQL. The source rules are those of the
    /// untyped overload (a plain table/entity, a table-valued function or a derived query; filters and
    /// other modifiers belong to the reshaped result or inside the derived query).
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity">The inner entity/projection type whose members act as grouping columns.</typeparam>
    /// <typeparam name="TShape">The typed result shape declared by <paramref name="projection"/>.</typeparam>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="aggregate">The aggregate applied to each cell (<c>SUM</c>/<c>COUNT</c>/<c>AVG</c>/<c>MIN</c>/<c>MAX</c>).</param>
    /// <param name="aggregateColumn">The column expression aggregated into each cell.</param>
    /// <param name="forColumn">The column whose values become the result columns.</param>
    /// <param name="projection">The typed result projection; unused inner members (other than the aggregate/<c>FOR</c> column) remain grouping inputs.</param>
    /// <returns>A builder over the typed <c>PIVOT</c> result.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The projection declares no value, an empty shape, a duplicate value, a non-constant/empty value name, or reads the aggregate/<c>FOR</c> column as a grouping member.</exception>
    /// <exception cref="NotSupportedException">The query is rendered by a provider without <c>PIVOT</c> support, or the projection reads the inner source as a whole.</exception>
    /// <exception cref="BuildSqlCommandException">A grouping member has no registered column name (the inner mapping was never registered).</exception>
    public static EntityBuilder<TShape> Pivot<TEntity, TShape>(
        this EntityBuilder<TEntity> builder,
        PivotAggregate aggregate,
        Expression<Func<TEntity, object?>> aggregateColumn,
        Expression<Func<TEntity, object?>> forColumn,
        Expression<Func<TEntity, PivotValues, TShape>> projection)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(aggregateColumn);
        ArgumentNullException.ThrowIfNull(forColumn);
        ArgumentNullException.ThrowIfNull(projection);
        builder.EnsureNoEagerLoadState(nameof(Pivot));

        var declared = PivotProjectionRewriter.ExtractValues(projection, aggregateColumn, forColumn);
        var values = new PivotValue[declared.Length];
        for (var i = 0; i < declared.Length; i++)
            values[i] = PivotValue.Create(declared[i]);

        var spec = PivotExpression.ForPivot(ResolvePivotInner(builder), typeof(TEntity), aggregate, aggregateColumn, forColumn, values);

        // The declared value and the grouping member names stay raw. Each emitted occurrence then passes
        // through exactly one escaping stage: the PIVOT declaration (SqlServerPivotRenderer.RenderPivot)
        // and the always-quoted Column<T> sink in the rewritten projection references. Pre-escaping here
        // would quote a name twice (the quoted-mode double-bracket bug of R237-02).
        return BuildTypedSource<TEntity, TShape>(
            builder,
            spec,
            projection,
            typeof(PivotValues),
            static name => name);
    }

    /// <summary>
    /// Reshapes this query's source by stacking the listed <paramref name="columns"/> with the native
    /// <c>UNPIVOT</c> operator and projects the typed result shape in one declaration. The two output
    /// roles are declared in <paramref name="projection"/>: <c>unpivot.Value&lt;decimal?&gt;("val")</c>
    /// reads the value column and <c>unpivot.Name&lt;string&gt;("qtr")</c> the name column, using the same
    /// role identifiers the <c>UNPIVOT ([val] for [qtr] in (...))</c> clause takes. A direct member of the
    /// inner source is a grouping member.
    /// <para>
    /// The role identifiers passed to <c>Value</c>/<c>Name</c> are <b>runtime</b> values: they are matched
    /// against the <c>UNPIVOT ([value] for [name] in (...))</c> clause at runtime, so an incompatible
    /// explicit cell type or a malformed role name is not rejected at compile time. SQL Server excludes a
    /// source cell whose value is <c>NULL</c>, so the stacked result contains only non-null cells and can
    /// have fewer rows than <c>columns × source rows</c>; declaring the value cell nullable models that
    /// exclusion.
    /// </para>
    /// <para>
    /// The returned builder is a full source over the typed projection (see the typed <c>Pivot</c>
    /// overload); like every derived source it is materialized with an explicit <c>Select</c>. Requires a
    /// dialect that supports <c>UNPIVOT</c> (SQL Server); other providers reject the query when it
    /// renders. The projection is fail-closed: zero columns, a missing role, and conflicting role
    /// identifiers throw <see cref="ArgumentException"/>; a repeated consistent reference reuses the role.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity">The inner entity/projection type whose members act as grouping columns.</typeparam>
    /// <typeparam name="TShape">The typed result shape declared by <paramref name="projection"/>.</typeparam>
    /// <param name="builder">The query builder to extend.</param>
    /// <param name="projection">The typed result projection declaring the value and name column roles.</param>
    /// <param name="columns">The source columns to unpivot; each column's name becomes its name-column value.</param>
    /// <returns>A builder over the typed <c>UNPIVOT</c> result.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">No column is supplied, a role is missing, or a role is declared with conflicting identifiers.</exception>
    /// <exception cref="NotSupportedException">The query is rendered by a provider without <c>UNPIVOT</c> support, or the projection reads the inner source as a whole.</exception>
    /// <exception cref="BuildSqlCommandException">A grouping member has no registered column name (the inner mapping was never registered).</exception>
    public static EntityBuilder<TShape> Unpivot<TEntity, TShape>(
        this EntityBuilder<TEntity> builder,
        Expression<Func<TEntity, UnpivotValues, TShape>> projection,
        params UnpivotColumn[] columns)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(columns);
        if (columns.Length == 0)
            throw new ArgumentException("An UNPIVOT requires at least one column.", nameof(columns));
        builder.EnsureNoEagerLoadState(nameof(Unpivot));

        var (valueColumn, nameColumn) = PivotProjectionRewriter.ExtractUnpivotRoles(projection);
        var spec = PivotExpression.ForUnpivot(ResolvePivotInner(builder), typeof(TEntity), valueColumn, nameColumn, columns);

        return BuildTypedSource<TEntity, TShape>(builder, spec, projection, typeof(UnpivotValues), static name => name);
    }

    /// <summary>
    /// Builds the typed pivot source: the legacy pivot construction feeds the rewritten projection
    /// through <see cref="EntityBuilder{TEntity}.Select{TResult}"/>, and the resulting command becomes the
    /// derived source of the returned typed builder (so the shape members resolve at render time).
    /// </summary>
    private static EntityBuilder<TShape> BuildTypedSource<TEntity, TShape>(
        EntityBuilder<TEntity> builder,
        PivotExpression spec,
        LambdaExpression projection,
        Type markerType,
        Func<string, string> formatValueName)
    {
        var aliasParameter = Expression.Parameter(typeof(TableAlias), "t");
        var body = PivotProjectionRewriter.Rewrite(
            projection,
            aliasParameter,
            markerType,
            member => ResolveGroupingColumn(builder, member),
            formatValueName);

        var rewritten = Expression.Lambda<Func<TableAlias, TShape>>(body, aliasParameter);
        var pivotSource = new EntityBuilder<TableAlias>(builder.DataProvider)
        {
            Logger = builder.Logger,
            SourceFrom = new FromExpression(spec)
        };

        return new EntityBuilder<TShape>(builder.DataProvider, pivotSource.Select(rewritten)) { Logger = builder.Logger };
    }

    /// <summary>
    /// Resolves the effective SQL name of a grouping member: the registered metadata column name for an
    /// entity/table-valued-function inner, or the derived projection's SQL-visible output name for a
    /// derived inner. Returns <see langword="null"/> when it cannot be resolved, so the caller never
    /// silently defaults to the CLR name.
    /// </summary>
    private static string? ResolveGroupingColumn<TEntity>(EntityBuilder<TEntity> builder, System.Reflection.MemberInfo member)
    {
        var convention = EffectiveNamingConvention(builder);

        if (builder.Query is not { } innerQuery)
            return member.GetPropertyColumnName(convention) is { Length: > 0 } name ? name : null;

        if (innerQuery.SelectList is not { Length: > 0 })
            innerQuery.PrepareCommand(CancellationToken.None);

        if (innerQuery.SelectList is not { Length: > 0 } selectList)
            return null;

        foreach (var column in selectList)
        {
            if (column.PropertyName == member.Name)
                return DerivedOutputColumn(column, convention);
        }

        return null;
    }

    /// <summary>
    /// The SQL-visible output name of a derived projection column. A plain source column that keeps its
    /// name in the derived <c>SELECT</c> is read through under its mapped name; every other item is
    /// aliased to its projected property name (the same rule the derived-member translator renders).
    /// </summary>
    private static string? DerivedOutputColumn(SelectExpression column, INamingConvention? convention)
    {
        if (column.PropertyName is not { Length: > 0 } propertyName)
            return null;

        if (column.Expression is { } expression
            && TypeFacts.UnwrapConvert(expression) is MemberExpression { Member: System.Reflection.MemberInfo member }
            && member.GetPropertyColumnName(convention) is { Length: > 0 } physical
            && string.Equals(physical, propertyName, StringComparison.OrdinalIgnoreCase))
        {
            return physical;
        }

        return propertyName;
    }

    /// <summary>The effective naming convention of the query (the per-query override, otherwise the context default).</summary>
    private static INamingConvention? EffectiveNamingConvention<TEntity>(EntityBuilder<TEntity> builder)
        => builder.NamingConvention ?? (builder.DataProvider as DataContext)?.NamingConvention;

    /// <summary>
    /// Resolves the source a <c>PIVOT</c>/<c>UNPIVOT</c> wraps. A plain table/entity mapping, a
    /// table-valued function and a derived query (<c>From(query)</c>) are allowed; the native operators
    /// apply to a table expression, so any filter, join, grouping, ordering, paging or table modifier on
    /// this builder must be applied to the reshaped result (or, for a derived source, moved into the
    /// derived query).
    /// </summary>
    private static FromExpression ResolvePivotInner<TEntity>(EntityBuilder<TEntity> builder)
    {
        if (builder.Joins is { Count: > 0 } || builder.Condition is not null || builder.HavingCondition is not null
            || builder.GroupByExpression is not null || builder.Sorting is { Count: > 0 } || !builder.Paging.IsEmpty || builder.IsDistinct
            || builder.TableSampleClause is not null || builder.TemporalClause is not null || builder.RowLockClause is not null || builder.PreWhereCondition is not null
            || builder.ArrayJoins is { Count: > 0 } || builder.SettingsList is { Count: > 0 } || builder.LimitByClause is not null
            || builder.DistinctOnClause is not null || builder.ExtremeRowClause is not null || builder.Windows is { Count: > 0 } || builder.IsFinal || builder.SampleRatio is not null
            || builder.TableHints is { Count: > 0 } || builder.IndexHints is not null || builder.Ctes is { Count: > 0 })
            throw new NotSupportedException(builder.Query is not null
                ? "PIVOT/UNPIVOT over a derived query accepts no modifiers on the pivot builder; apply filters, joins, grouping, ordering, paging and table modifiers inside the derived query or to the reshaped result."
                : "PIVOT/UNPIVOT can only be applied to a plain table/entity source, a table-valued function or a derived query; apply filters, joins, grouping, ordering, paging and table modifiers to the reshaped result.");

        if (builder.Query is not null)
            return new FromExpression(builder.Query);

        if (builder.SourceFrom is not null)
            return builder.SourceFrom;

        if (!string.IsNullOrEmpty(builder.Table))
            return new FromExpression(builder.Table);

        return builder.DataProvider.GetFrom(typeof(TEntity), null)
            ?? throw new BuildSqlCommandException($"A PIVOT/UNPIVOT source could not be resolved for type {typeof(TEntity)}.");
    }
}
