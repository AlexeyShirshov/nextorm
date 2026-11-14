namespace NextORM.SqlServer;

/// <summary>
/// Chooses the explicit SQL Server <c>VECTOR_SEARCH</c> grammar for approximate vector search. The
/// grammar is not inferred from the server major version; builds within the same major may implement
/// different forms, and the selected form is never silently substituted.
/// </summary>
public enum SqlServerVectorSearchSyntax
{
    /// <summary>
    /// Uses <c>TOP (k)</c> and <c>TOP_N = k</c> inside <c>VECTOR_SEARCH</c>, without
    /// <c>WITH APPROXIMATE</c>. This is the default and is verified on SQL Server 2025 RTM-CU9.
    /// </summary>
    TopN,

    /// <summary>
    /// Uses <c>TOP (k) WITH APPROXIMATE</c> and omits <c>TOP_N</c> from <c>VECTOR_SEARCH</c>.
    /// Select this explicitly for SQL Server builds whose vector-search parser implements this grammar.
    /// </summary>
    WithApproximate
}
