namespace NextORM.Core;

/// <summary>
/// The kind of set operation joining two queries. <c>None = 0</c> and the original
/// <see cref="Distinct"/>/<see cref="All"/> values keep their numeric assignment so existing
/// serialized values and plan hashes stay stable; the INTERSECT/EXCEPT members are appended.
/// </summary>
public enum UnionType
{
    /// <summary>No set operation; the query is not combined with another.</summary>
    None = 0,
    /// <summary><c>UNION</c>: distinct rows from both queries.</summary>
    Distinct = 1,
    /// <summary><c>UNION ALL</c>: all rows from both queries, including duplicates.</summary>
    All = 2,
    /// <summary><c>INTERSECT</c>: distinct rows present in both queries.</summary>
    Intersect = 3,
    /// <summary><c>INTERSECT ALL</c>: rows present in both queries, preserving duplicates.</summary>
    IntersectAll = 4,
    /// <summary><c>EXCEPT</c>: distinct rows from the first query that are absent from the second.</summary>
    Except = 5,
    /// <summary><c>EXCEPT ALL</c>: rows from the first query absent from the second, preserving duplicates.</summary>
    ExceptAll = 6
}
