namespace NextORM.Core;

/// <summary>
/// A single common table expression: the name it is declared under, the query that produces it and
/// whether it is recursive (a recursive CTE body may reference its own name).
/// </summary>
public sealed class CteDefinition
{
    /// <summary>Creates a non-recursive CTE definition.</summary>
    /// <param name="name">The name the CTE is declared under and referenced by in <c>from</c>.</param>
    /// <param name="query">The query that defines the CTE.</param>
    public CteDefinition(string name, QueryCommand query)
        : this(name, query, false, null)
    {
    }

    /// <summary>Creates a CTE definition, optionally recursive and with a recursion-depth limit.</summary>
    /// <param name="name">The name the CTE is declared under and referenced by in <c>from</c>.</param>
    /// <param name="query">The query that defines the CTE.</param>
    /// <param name="recursive">Whether the CTE body may reference its own name.</param>
    /// <param name="maxRecursion">Optional recursion-depth limit; see <see cref="MaxRecursion"/>.</param>
    public CteDefinition(string name, QueryCommand query, bool recursive, int? maxRecursion = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(query);

        Name = name;
        Query = query;
        Recursive = recursive;
        MaxRecursion = maxRecursion;
    }

    /// <summary>
    /// Creates a data-modifying CTE definition whose body is an <c>INSERT</c> and whose readable
    /// columns are described by <paramref name="shape"/> (a prepared projection over the inserted
    /// entity). Only PostgreSQL accepts a data-modifying CTE body.
    /// </summary>
    /// <param name="name">The name the CTE is declared under and referenced by in <c>from</c>.</param>
    /// <param name="shape">A prepared command describing the columns returned by the mutation.</param>
    /// <param name="mutation">The <c>INSERT</c> that forms the CTE body.</param>
    internal CteDefinition(string name, QueryCommand shape, InsertCommand mutation)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(shape);
        ArgumentNullException.ThrowIfNull(mutation);

        Name = name;
        Query = shape;
        Mutation = mutation;
    }

    /// <summary>Name the CTE is declared under and referenced by in <c>from</c>.</summary>
    public string Name { get; }
    /// <summary>Query that defines the CTE, or the column shape of a data-modifying CTE.</summary>
    public QueryCommand Query { get; }
    /// <summary>
    /// The data-modifying statement (<c>INSERT ... RETURNING</c>) that forms the CTE body, or
    /// <c>null</c> when the CTE is an ordinary read CTE.
    /// </summary>
    internal InsertCommand? Mutation { get; }
    /// <summary>True when the CTE body is a data-modifying statement rather than a <c>SELECT</c>.</summary>
    public bool IsDataModifying => Mutation is not null;
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

    /// <summary>
    /// Declares a data-modifying common table expression <b>after</b> the read CTEs collected so far, so
    /// its <c>INSERT ... SELECT</c> body may reference them (PostgreSQL makes a CTE visible to later
    /// ones). The returned scope is typed by the <c>RETURNING</c> projection.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type inserted by the CTE body.</typeparam>
    /// <typeparam name="TResult">The row shape the CTE returns through <c>RETURNING</c>.</typeparam>
    /// <param name="name">The name the data-modifying CTE is declared under.</param>
    /// <param name="insert">The returning insert that forms the CTE body.</param>
    /// <returns>A scope that reads the mutation's returned rows.</returns>
    /// <exception cref="NotSupportedException">The provider does not accept a data-modifying CTE body (only PostgreSQL does).</exception>
    public MutationCteQuery<TResult> With<TEntity, TResult>(string name, InsertReturningBuilder<TEntity, TResult> insert)
        => MutationCteQuery<TResult>.Create(_dataContext, name, insert, _ctes);

    /// <summary>Starts a query whose source is the CTE declared as <paramref name="cteName"/>.</summary>
    public EntityBuilder<TableAlias> From(string cteName) => new(_dataContext, cteName) { Ctes = _ctes, Logger = _dataContext.CommandLogger };

    /// <summary>Starts a query whose source is <paramref name="cte"/>.</summary>
    public EntityBuilder<TableAlias> From(CteDefinition cte) => From(cte.Name);

    private IReadOnlyList<CteDefinition> Append(CteDefinition cte)
    {
        var list = new List<CteDefinition>(_ctes.Count + 1);
        list.AddRange(_ctes);
        list.Add(cte);
        return list;
    }
}
