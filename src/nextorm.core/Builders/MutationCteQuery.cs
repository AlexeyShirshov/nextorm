using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent scope that collects the declarations of a <c>WITH</c> clause whose body includes one
/// data-modifying common table expression (an <c>INSERT ... RETURNING</c>), started with
/// <see cref="DataContextExtensions.With{TEntity, TResult}(IDataContext, string, InsertReturningBuilder{TEntity, TResult})"/>.
/// <para>
/// Only PostgreSQL accepts a data-modifying CTE body; every other provider rejects it. The scope is
/// typed by the <c>RETURNING</c> projection (<typeparamref name="TResult"/>), so
/// <see cref="From(string)"/> reads the rows the mutation returns while
/// <see cref="FromTable(string)"/> reads an ordinary read CTE declared alongside it.
/// </para>
/// </summary>
/// <typeparam name="TResult">The row shape the data-modifying CTE returns through <c>RETURNING</c>.</typeparam>
public sealed class MutationCteQuery<TResult>
{
    private readonly IDataContext _dataContext;
    private readonly IReadOnlyList<CteDefinition> _ctes;
    private readonly string _mutationName;

    internal MutationCteQuery(IDataContext dataContext, IReadOnlyList<CteDefinition> ctes, string mutationName)
    {
        _dataContext = dataContext;
        _ctes = ctes;
        _mutationName = mutationName;
    }

    /// <summary>Declarations collected so far, in declaration order.</summary>
    public IReadOnlyList<CteDefinition> Ctes => _ctes;

    /// <summary>Declares an additional non-recursive read CTE alongside the data-modifying one.</summary>
    /// <param name="name">The name the CTE is declared under.</param>
    /// <param name="query">The query that defines the CTE.</param>
    /// <returns>A scope carrying every declaration, still typed by the mutation's projection.</returns>
    public MutationCteQuery<TResult> With(string name, QueryCommand query)
        => new(_dataContext, Append(new CteDefinition(name, query)), _mutationName);

    /// <summary>Declares an additional recursive read CTE alongside the data-modifying one.</summary>
    /// <param name="name">The name the CTE is declared under.</param>
    /// <param name="query">The query that defines the CTE.</param>
    /// <param name="maxRecursion">Optional recursion-depth limit.</param>
    /// <returns>A scope carrying every declaration, still typed by the mutation's projection.</returns>
    public MutationCteQuery<TResult> WithRecursive(string name, QueryCommand query, int? maxRecursion = null)
        => new(_dataContext, Append(new CteDefinition(name, query, true, maxRecursion)), _mutationName);

    /// <summary>
    /// Starts a query whose source is the data-modifying CTE, so the rows the <c>INSERT ... RETURNING</c>
    /// produced can be filtered, joined and projected with the full operator set.
    /// </summary>
    /// <param name="cteName">The name of the data-modifying CTE.</param>
    /// <returns>A typed builder over the returned rows.</returns>
    /// <exception cref="ArgumentException"><paramref name="cteName"/> is not the data-modifying CTE (use <see cref="FromTable(string)"/> for read CTEs).</exception>
    public EntityBuilder<TResult> From(string cteName)
    {
        if (!string.Equals(cteName, _mutationName, StringComparison.Ordinal))
            throw new ArgumentException($"'{cteName}' is not the data-modifying CTE of this scope; use {nameof(FromTable)} to read a plain CTE.", nameof(cteName));

        var cte = FindCte(cteName);
        var builder = new EntityBuilder<TResult>(_dataContext) { Logger = _dataContext.CommandLogger, Ctes = _ctes };
        builder.SourceFrom = new FromExpression(cteName, cte.Query);
        return builder;
    }

    /// <summary>Starts a query whose source is an ordinary read CTE declared alongside the mutation.</summary>
    /// <param name="cteName">The name of the read CTE.</param>
    /// <returns>A builder whose columns are read through <see cref="TableAlias"/> accessors.</returns>
    public EntityBuilder<TableAlias> FromTable(string cteName)
    {
        FindCte(cteName);
        return new EntityBuilder<TableAlias>(_dataContext, cteName) { Logger = _dataContext.CommandLogger, Ctes = _ctes };
    }

    /// <summary>
    /// Builds a scope whose data-modifying CTE is declared after <paramref name="preceding"/> read CTEs,
    /// so the insert body may reference them (PostgreSQL declares a CTE visible to later ones).
    /// </summary>
    internal static MutationCteQuery<TResult> Create<TEntity>(IDataContext dataContext, string name, InsertReturningBuilder<TEntity, TResult> insert, IReadOnlyList<CteDefinition>? preceding = null)
    {
        if (dataContext is not DataContext context || !context.Dialect.SupportsDataModifyingCtes)
            throw new NotSupportedException(
                "Data-modifying common table expressions (WITH <name> AS (INSERT ... RETURNING ...)) are only supported by PostgreSQL; this provider requires a CTE body to be a SELECT.");

        var shape = BuildShape(dataContext, insert);
        var cte = new CteDefinition(name, shape, insert.BuildMutationCommand());

        var ctes = new List<CteDefinition>((preceding?.Count ?? 0) + 1);
        if (preceding is not null)
            ctes.AddRange(preceding);
        ctes.Add(cte);

        return new MutationCteQuery<TResult>(dataContext, ctes, name);
    }

    /// <summary>
    /// Builds the prepared column-shape command a data-modifying CTE read uses: a projection over the
    /// inserted entity that mirrors the <c>RETURNING</c> selector, so member access resolves to the
    /// returned columns.
    /// </summary>
    internal static QueryCommand BuildShape<TEntity, TReturn>(IDataContext dataContext, InsertReturningBuilder<TEntity, TReturn> insert)
    {
        var projection = insert.Projection ?? BuildKeyProjection<TEntity, TReturn>(insert);

        var shape = dataContext.CreateCommand<TReturn>(new QueryDefinition
        {
            Exp = projection,
            SrcType = typeof(TEntity),
        });
        shape.PrepareCommand(false, CancellationToken.None);
        return shape;
    }

    // ReturningKey<TKey>() carries no selector; reconstruct the single key member access so the read
    // can still be typed (the key type was validated against TKey when the returning builder was built).
    private static LambdaExpression BuildKeyProjection<TEntity, TReturn>(InsertReturningBuilder<TEntity, TReturn> insert)
    {
        var columns = insert.ReturningColumns;
        if (columns.Count == 0)
            throw new NotSupportedException(
                "A data-modifying CTE requires RETURNING columns: ReturningIdentity<TKey>() names no column. Project the returned columns instead, e.g. Return(x => new { x.Id }).");

        PropertyInfo property = columns[0].PropertyInfo;
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        return Expression.Lambda(Expression.MakeMemberAccess(parameter, property), parameter);
    }

    private CteDefinition FindCte(string name)
    {
        for (var (i, cnt) = (0, _ctes.Count); i < cnt; i++)
        {
            if (string.Equals(_ctes[i].Name, name, StringComparison.Ordinal))
                return _ctes[i];
        }

        throw new ArgumentException($"No CTE named '{name}' is declared in this scope.", nameof(name));
    }

    private IReadOnlyList<CteDefinition> Append(CteDefinition cte)
    {
        var list = new List<CteDefinition>(_ctes.Count + 1);
        list.AddRange(_ctes);
        list.Add(cte);
        return list;
    }
}
