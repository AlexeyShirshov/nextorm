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
        EnsureSupported(dataContext);
        var cte = new CteDefinition(name, BuildShape(dataContext, insert), new CteMutation(insert.BuildMutationCommand()));
        return Wrap(dataContext, name, cte, preceding);
    }

    /// <summary>
    /// Builds a scope whose data-modifying CTE body is a single-table <c>UPDATE ... RETURNING</c>.
    /// </summary>
    internal static MutationCteQuery<TResult> Create<TEntity>(IDataContext dataContext, string name, UpdateReturningBuilder<TEntity, TResult> update, IReadOnlyList<CteDefinition>? preceding = null)
    {
        EnsureSupported(dataContext);
        var projection = update.Projection ?? throw MissingProjection("update");
        var cte = new CteDefinition(name, BuildShape<TResult>(dataContext, projection, typeof(TEntity)), new CteMutation(update.BuildMutationCommand()));
        return Wrap(dataContext, name, cte, preceding);
    }

    /// <summary>
    /// Builds a scope whose data-modifying CTE body is a single-table <c>DELETE ... RETURNING</c>.
    /// </summary>
    internal static MutationCteQuery<TResult> Create<TEntity>(IDataContext dataContext, string name, DeleteReturningBuilder<TEntity, TResult> delete, IReadOnlyList<CteDefinition>? preceding = null)
    {
        EnsureSupported(dataContext);
        var projection = delete.Projection ?? throw MissingProjection("delete");
        var cte = new CteDefinition(name, BuildShape<TResult>(dataContext, projection, typeof(TEntity)), new CteMutation(delete.BuildMutationCommand()));
        return Wrap(dataContext, name, cte, preceding);
    }

    /// <summary>
    /// Builds a scope whose data-modifying CTE body is a multi-table <c>UPDATE ... FROM ... RETURNING</c>.
    /// </summary>
    internal static MutationCteQuery<TResult> Create<TProjection>(IDataContext dataContext, string name, UpdateJoinReturningBuilder<TProjection, TResult> update, IReadOnlyList<CteDefinition>? preceding = null)
    {
        EnsureSupported(dataContext);
        var command = update.BuildMutationCommand();
        var source = JoinShapeSource(command);
        var cte = new CteDefinition(name, BuildShape<TResult>(dataContext, update.Projection, typeof(TProjection), source), new CteMutation(command));
        return Wrap(dataContext, name, cte, preceding);
    }

    /// <summary>
    /// Builds a scope whose data-modifying CTE body is a multi-table <c>DELETE ... USING ... RETURNING</c>.
    /// </summary>
    internal static MutationCteQuery<TResult> Create<TProjection>(IDataContext dataContext, string name, DeleteJoinReturningBuilder<TProjection, TResult> delete, IReadOnlyList<CteDefinition>? preceding = null)
    {
        EnsureSupported(dataContext);
        var command = delete.BuildMutationCommand();
        var source = JoinShapeSource(command);
        var cte = new CteDefinition(name, BuildShape<TResult>(dataContext, delete.Projection, typeof(TProjection), source), new CteMutation(command));
        return Wrap(dataContext, name, cte, preceding);
    }

    // The multi-table mutation's source already carries the target FROM and the INNER joins. The CTE
    // read shape must project over the same joined sources so a member reference like p.Item2.Name
    // resolves to the joined table's alias; a shape built over the bare Projection<T1, T2> type cannot
    // resolve a table for Item2. Preparing the source here is idempotent (the CTE preparation skips an
    // already-prepared body source).
    private static QueryCommand JoinShapeSource(MutationCommand command)
    {
        var source = command switch
        {
            UpdateJoinCommand updateJoin => updateJoin.Source,
            DeleteJoinCommand deleteJoin => deleteJoin.Source,
            _ => throw new NotSupportedException($"'{command.GetType().Name}' is not a multi-table mutation."),
        };

        if (!source.IsPrepared)
            source.PrepareCommand(false, CancellationToken.None);

        return source;
    }

    private static void EnsureSupported(IDataContext dataContext)
    {
        if (dataContext is not DataContext context || !context.Dialect.SupportsDataModifyingCtes)
            throw new NotSupportedException(
                "Data-modifying common table expressions (WITH <name> AS (INSERT ... RETURNING ...)) are only supported by PostgreSQL; this provider requires a CTE body to be a SELECT.");
    }

    private static NotSupportedException MissingProjection(string operation)
        => new($"A data-modifying CTE {operation} requires a RETURNING projection; call Returning(...) on the {operation} builder.");

    private static MutationCteQuery<TResult> Wrap(IDataContext dataContext, string name, CteDefinition cte, IReadOnlyList<CteDefinition>? preceding)
    {
        var ctes = new List<CteDefinition>((preceding?.Count ?? 0) + 1);
        if (preceding is not null)
            ctes.AddRange(preceding);
        ctes.Add(cte);

        return new MutationCteQuery<TResult>(dataContext, ctes, name);
    }

    /// <summary>
    /// Builds the prepared column-shape command a data-modifying CTE read uses: a projection over the
    /// mutated entity that mirrors the <c>RETURNING</c> selector, so member access resolves to the
    /// returned columns.
    /// </summary>
    internal static QueryCommand BuildShape<TEntity, TReturn>(IDataContext dataContext, InsertReturningBuilder<TEntity, TReturn> insert)
    {
        var projection = insert.Projection ?? BuildKeyProjection<TEntity, TReturn>(insert);
        return BuildShape<TReturn>(dataContext, projection, typeof(TEntity));
    }

    private static QueryCommand BuildShape<TShape>(IDataContext dataContext, LambdaExpression projection, Type sourceType, QueryCommand? joinedSource = null)
    {
        // An identity projection has no column-producing body for the normal
        // projector path, so the shape is built as a bare projection over TShape and expanded by
        // TryBuildProjectionSelectList into slot-tagged item columns (mirroring the mutation's
        // RETURNING list). Non-identity shapes keep their selector.
        var isIdentity = TypeFacts.UnwrapConvert(projection.Body) is ParameterExpression;
        var shape = dataContext.CreateCommand<TShape>(new QueryDefinition
        {
            Exp = isIdentity ? null : projection,
            SrcType = isIdentity ? typeof(TShape) : sourceType,
            Joins = joinedSource?.Joins,
        });
        // Mark the identity shape BEFORE preparation so TryBuildProjectionSelectList tags every item
        // column with its deterministic, collision-free per-slot alias before the column plan hash is
        // finalized. Assigning it afterwards (after PrepareCommand) would leave PlanHashCode and the
        // command's ColumnsPlanHash describing a shape without the alias.
        if (isIdentity)
            shape.IdentitySlotAliases = true;
        if (joinedSource?.From is { } from)
            shape.From = from;
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
