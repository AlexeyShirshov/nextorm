namespace NextORM.Core;

/// <summary>
/// A raw SQL fragment used as a composable <c>FROM</c> source (<c>ctx.FromSql</c>): it is rendered as
/// <c>(&lt;sql&gt;) AS alias</c> and its named parameters are bound into the enclosing command. This is
/// an implementation detail of <see cref="DataContextExtensions.FromSql"/>.
/// </summary>
internal sealed class RawSqlSourceExpression(string sql, object? parameters)
{
    /// <summary>The SQL text, emitted verbatim inside the derived-table parentheses.</summary>
    public string Sql { get; } = sql;

    /// <summary>An object whose public properties supply the named parameters referenced by <see cref="Sql"/>.</summary>
    public object? Parameters { get; } = parameters;
}
