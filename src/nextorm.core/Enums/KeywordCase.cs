namespace NextORM.Core;

/// <summary>
/// The letter case in which SQL keywords are emitted. Function names, identifiers, string literals,
/// type names and raw SQL are never affected. Configure the context-wide default with
/// <c>DataContextBuilder.UseKeywordCase</c> and override it for a single query with
/// <c>EntityBuilder&lt;TEntity&gt;.WithKeywordCase</c>.
/// </summary>
public enum KeywordCase
{
    /// <summary>Lower case (<c>select ... from ...</c>) — the default, matching the historical output.</summary>
    Lower = 0,
    /// <summary>Upper case (<c>SELECT ... FROM ...</c>).</summary>
    Upper = 1,
}
