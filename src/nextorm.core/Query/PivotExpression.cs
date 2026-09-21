using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// The aggregate a <c>PIVOT</c> applies to each pivoted cell. T-SQL restricts the aggregate to one of
/// these five functions.
/// </summary>
public enum PivotAggregate
{
    /// <summary><c>SUM</c>.</summary>
    Sum,
    /// <summary><c>COUNT</c>.</summary>
    Count,
    /// <summary><c>AVG</c>.</summary>
    Avg,
    /// <summary><c>MIN</c>.</summary>
    Min,
    /// <summary><c>MAX</c>.</summary>
    Max
}

/// <summary>
/// One value of the <c>PIVOT ... IN (...)</c> list. The value names the resulting column, so select
/// it in the projection under the desired name
/// (for example <c>.Select(t =&gt; new { Q1 = t.GetNullableDecimal("[1]") })</c> when the value is
/// not a plain identifier).
/// </summary>
public sealed class PivotValue
{
    private PivotValue(string value)
    {
        Value = value;
    }

    /// <summary>The source value matched by this pivot column; also the resulting column's name.</summary>
    public string Value { get; }

    /// <summary>Creates a pivot value.</summary>
    public static PivotValue Create(string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        return new PivotValue(value);
    }
}

/// <summary>
/// One source column of the <c>UNPIVOT ... IN (...)</c> list. The column name becomes the name-column
/// value for the rows produced from this column.
/// </summary>
public sealed class UnpivotColumn
{
    private UnpivotColumn(string column)
    {
        Column = column;
    }

    /// <summary>The source column to unpivot (rendered as an identifier).</summary>
    public string Column { get; }

    /// <summary>Creates an unpivot column entry.</summary>
    public static UnpivotColumn Create(string column)
    {
        ArgumentException.ThrowIfNullOrEmpty(column);
        return new UnpivotColumn(column);
    }
}

/// <summary>
/// A <c>PIVOT</c>/<c>UNPIVOT</c> source: the inner source whose rows/columns are reshaped, together
/// with the aggregate/values (<c>PIVOT</c>) or the value/name columns and column list
/// (<c>UNPIVOT</c>). Stored on <see cref="FromExpression.Pivot"/> and rendered by the provider that
/// supports the construct (see <see cref="ISqlDialect.Pivot"/> /
/// <see cref="ISqlDialect.Pivot"/>).
/// </summary>
public sealed class PivotExpression
{
    private PivotExpression(
        FromExpression inner,
        Type innerEntityType,
        bool isUnpivot,
        PivotAggregate aggregate,
        LambdaExpression? aggregateColumn,
        LambdaExpression? forColumn,
        IReadOnlyList<PivotValue> values,
        string? unpivotValueColumn,
        string? unpivotNameColumn,
        IReadOnlyList<UnpivotColumn> columns)
    {
        Inner = inner;
        InnerEntityType = innerEntityType;
        IsUnpivot = isUnpivot;
        Aggregate = aggregate;
        AggregateColumn = aggregateColumn;
        ForColumn = forColumn;
        Values = values;
        UnpivotValueColumn = unpivotValueColumn;
        UnpivotNameColumn = unpivotNameColumn;
        Columns = columns;
    }

    internal static PivotExpression ForPivot(
        FromExpression inner,
        Type innerEntityType,
        PivotAggregate aggregate,
        LambdaExpression aggregateColumn,
        LambdaExpression forColumn,
        IReadOnlyList<PivotValue> values)
        => new(inner, innerEntityType, false, aggregate, aggregateColumn, forColumn, values, null, null, []);

    internal static PivotExpression ForUnpivot(
        FromExpression inner,
        Type innerEntityType,
        string valueColumn,
        string nameColumn,
        IReadOnlyList<UnpivotColumn> columns)
        => new(inner, innerEntityType, true, PivotAggregate.Sum, null, null, [], valueColumn, nameColumn, columns);

    /// <summary>The source whose rows/columns are reshaped (a table, table-valued function or subquery).</summary>
    public FromExpression Inner { get; }

    /// <summary>The entity type of <see cref="Inner"/>, used to resolve the pivot column expressions.</summary>
    public Type InnerEntityType { get; }

    /// <summary><c>true</c> for an <c>UNPIVOT</c>; <c>false</c> for a <c>PIVOT</c>.</summary>
    public bool IsUnpivot { get; }

    /// <summary>The aggregate applied by a <c>PIVOT</c>.</summary>
    public PivotAggregate Aggregate { get; }

    /// <summary>The aggregated column expression of a <c>PIVOT</c>, or <c>null</c> for <c>UNPIVOT</c>.</summary>
    public LambdaExpression? AggregateColumn { get; }

    /// <summary>The <c>FOR</c> column expression of a <c>PIVOT</c>, or <c>null</c> for <c>UNPIVOT</c>.</summary>
    public LambdaExpression? ForColumn { get; }

    /// <summary>The <c>IN (...)</c> values of a <c>PIVOT</c>; empty for <c>UNPIVOT</c>.</summary>
    public IReadOnlyList<PivotValue> Values { get; }

    /// <summary>The output value-column name of an <c>UNPIVOT</c>, or <c>null</c> for <c>PIVOT</c>.</summary>
    public string? UnpivotValueColumn { get; }

    /// <summary>The output name-column name of an <c>UNPIVOT</c>, or <c>null</c> for <c>PIVOT</c>.</summary>
    public string? UnpivotNameColumn { get; }

    /// <summary>The <c>IN (...)</c> source columns of an <c>UNPIVOT</c>; empty for <c>PIVOT</c>.</summary>
    public IReadOnlyList<UnpivotColumn> Columns { get; }

    /// <summary>
    /// Clones the inner source for the plan cache. A table/TVF/entity inner is immutable and shared; a
    /// derived inner is deep-cloned so the cached plan does not alias the live command (mirrors
    /// <see cref="FromExpression.CloneForCache"/>).
    /// </summary>
    internal PivotExpression CloneForCache()
    {
        var inner = Inner.CloneForCache()!;
        if (ReferenceEquals(inner, Inner))
            return this;

        return new PivotExpression(
            inner, InnerEntityType, IsUnpivot, Aggregate, AggregateColumn, ForColumn, Values,
            UnpivotValueColumn, UnpivotNameColumn, Columns);
    }
}
