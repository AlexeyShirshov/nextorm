namespace NextORM.Core;

/// <summary>
/// A single common table expression: the name it is declared under, the query that produces it and
/// whether it is recursive (a recursive CTE body may reference its own name).
/// </summary>
public sealed class CteDefinition
{
    public CteDefinition(string name, QueryCommand query)
        : this(name, query, false, null)
    {
    }

    public CteDefinition(string name, QueryCommand query, bool recursive, int? maxRecursion = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(query);

        Name = name;
        Query = query;
        Recursive = recursive;
        MaxRecursion = maxRecursion;
    }

    /// <summary>Name the CTE is declared under and referenced by in <c>from</c>.</summary>
    public string Name { get; }
    /// <summary>Query that defines the CTE.</summary>
    public QueryCommand Query { get; }
    /// <summary>True when the CTE body may reference <see cref="Name"/> (a recursive CTE).</summary>
    public bool Recursive { get; }
    /// <summary>
    /// Optional recursion depth limit. Rendered as <c>option (maxrecursion n)</c> by dialects that
    /// need one (SQL Server); ignored by dialects that rely on their own default.
    /// </summary>
    public int? MaxRecursion { get; }
}

/// <summary>
/// Fluent scope that collects the CTE declarations of a query. <see cref="With"/> /
/// <see cref="WithRecursive"/> add a definition and return a new scope (declarations are immutable),
/// and <see cref="From(string)"/> starts a query whose <c>from</c> is one of the declared CTEs while
/// carrying every declaration into the resulting command.
/// </summary>
public sealed class CteQuery
{
    private readonly IDataContext _dataContext;
    private readonly IReadOnlyList<CteDefinition> _ctes;

    internal CteQuery(IDataContext dataContext, IReadOnlyList<CteDefinition> ctes)
    {
        _dataContext = dataContext;
        _ctes = ctes;
    }

    /// <summary>Declarations collected so far, in declaration order.</summary>
    public IReadOnlyList<CteDefinition> Ctes => _ctes;

    /// <summary>Declares a non-recursive common table expression.</summary>
    public CteQuery With(string name, QueryCommand query)
        => new(_dataContext, Append(new CteDefinition(name, query)));

    /// <summary>
    /// Declares a recursive common table expression. A recursive CTE body references its own name
    /// through <see cref="From(string)"/>.
    /// </summary>
    public CteQuery WithRecursive(string name, QueryCommand query, int? maxRecursion = null)
        => new(_dataContext, Append(new CteDefinition(name, query, true, maxRecursion)));

    /// <summary>Starts a query whose source is the CTE declared as <paramref name="cteName"/>.</summary>
    public EntityBuilder From(string cteName) => new(_dataContext, cteName) { Ctes = _ctes, Logger = _dataContext.CommandLogger };

    /// <summary>Starts a query whose source is <paramref name="cte"/>.</summary>
    public EntityBuilder From(CteDefinition cte) => From(cte.Name);

    private IReadOnlyList<CteDefinition> Append(CteDefinition cte)
    {
        var list = new List<CteDefinition>(_ctes.Count + 1);
        list.AddRange(_ctes);
        list.Add(cte);
        return list;
    }
}
