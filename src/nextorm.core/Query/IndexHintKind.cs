namespace NextORM.Core;

/// <summary>
/// The intent of a table index hint: prefer an index (<c>USE INDEX</c>/<c>INDEXED BY</c>), force it
/// (<c>FORCE INDEX</c>), or suppress index use (<c>IGNORE INDEX</c>/<c>NOT INDEXED</c>).
/// </summary>
public enum IndexHintKind
{
    /// <summary>Ask the planner to consider the named indexes (<c>USE INDEX</c>, <c>INDEXED BY</c>).</summary>
    Use = 0,
    /// <summary>Force the planner to use the named indexes (<c>FORCE INDEX</c>, <c>WITH (INDEX(...))</c>).</summary>
    Force = 1,
    /// <summary>Suppress the named indexes (<c>IGNORE INDEX</c>) or all index use (SQLite <c>NOT INDEXED</c>).</summary>
    Ignore = 2,
}
