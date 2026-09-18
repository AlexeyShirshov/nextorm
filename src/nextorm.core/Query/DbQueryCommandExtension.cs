namespace nextorm.core;

/// <summary>
/// Carries a manually supplied SQL body and an optional parameter factory for a query command,
/// overriding the generated SQL.
/// </summary>
/// <remarks>
/// This is not an extension class: it exposes mutable public fields and holds override data. The
/// singular name also breaks the <c>*Extensions</c> convention used by the sibling helper classes
/// (<c>EntityExtensions</c>, <c>QueryCommandExtensions</c>, <c>TypeExtensions</c>). Recommended name:
/// <c>RawSqlOverride</c> with properties instead of fields.
/// See <c>API-NAMING-REVIEW.md</c> finding P0-6.
/// </remarks>
public class DbQueryCommandExtension
{
    /// <summary>SQL text to use instead of the generated statement.</summary>
    public string? ManualSql;
    /// <summary>Factory that produces the parameters referenced by <see cref="ManualSql"/>.</summary>
    public Func<List<Param>>? MakeParams;
}