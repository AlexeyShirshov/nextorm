namespace NextORM.Core;

/// <summary>
/// Carries a manually supplied SQL body and an optional parameter factory for a query command,
/// overriding the generated SQL. It is an implementation detail of <c>WithSql</c>/<c>PrepareFromSql</c>.
/// </summary>
internal sealed class RawSqlOverride
{
    /// <summary>SQL text to use instead of the generated statement.</summary>
    public string? ManualSql { get; init; }

    /// <summary>Factory that produces the parameters referenced by <see cref="ManualSql"/>.</summary>
    public Func<List<Parameter>>? MakeParams { get; init; }
}