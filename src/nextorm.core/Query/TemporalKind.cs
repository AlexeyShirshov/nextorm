namespace NextORM.Core;

/// <summary>Which <c>FOR SYSTEM_TIME</c> temporal clause to apply to a table.</summary>
public enum TemporalKind
{
    /// <summary>Rows that were valid at a point in time (<c>AS OF</c>).</summary>
    AsOf,
    /// <summary>Rows valid at some point in the closed-open range (<c>BETWEEN ... AND ...</c>).</summary>
    Between,
    /// <summary>Rows valid in the closed-open range (<c>FROM ... TO ...</c>).</summary>
    FromTo,
    /// <summary>Rows whose validity period lies entirely within the range (<c>CONTAINED IN</c>; SQL Server only).</summary>
    ContainedIn,
    /// <summary>Every row version (<c>ALL</c>).</summary>
    All
}
