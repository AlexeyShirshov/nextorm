namespace NextORM.Core;

/// <summary>Sampling algorithm for a <c>TABLESAMPLE</c> table modifier.</summary>
public enum TableSampleMethod
{
    /// <summary>Block-level sampling (<c>SYSTEM</c>); supported by PostgreSQL and SQL Server.</summary>
    System,
    /// <summary>Row-level Bernoulli sampling; PostgreSQL only.</summary>
    Bernoulli
}
