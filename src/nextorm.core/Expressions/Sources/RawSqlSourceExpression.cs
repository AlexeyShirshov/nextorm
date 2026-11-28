namespace NextORM.Core;

/// <summary>
/// A raw SQL fragment used as a composable <c>FROM</c> source (<c>ctx.FromSql</c>): it is rendered as
/// <c>(&lt;sql&gt;) AS alias</c> and its named parameters are bound into the enclosing command. This is
/// an implementation detail of <see cref="DataContextExtensions.FromSql"/>.
/// </summary>
internal sealed class RawSqlSourceExpression(string sql, object? parameters)
{
    private readonly object? _detachedIdentity;

    private RawSqlSourceExpression(RawSqlSourceExpression source) : this(source.Sql, null)
        => _detachedIdentity = source.PlanIdentity;

    /// <summary>The SQL text, emitted verbatim inside the derived-table parentheses.</summary>
    public string Sql { get; } = sql;

    /// <summary>An object whose public properties supply the named parameters referenced by <see cref="Sql"/>.</summary>
    public object? Parameters { get; } = parameters;

    internal object PlanIdentity => _detachedIdentity ?? PlanObjectIdentity.For(this)!;

    // Independent raw source instances remain incompatible; only the live source reads parameters.
    internal RawSqlSourceExpression ForSqlPlanKey() => Parameters is null ? this : new(this);
}
