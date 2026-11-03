using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace NextORM.QueryComponents;

public enum ExternalExtremeKind
{
    Min,
    Max
}

/// <summary>
/// The reflection handles the component emits into its structural expression tree, resolved once for
/// the process instead of re-scanned on every cold preparation. Every handle is a method the public
/// engine surface already exposes; nothing internal is opened.
/// </summary>
internal static class ExternalSelectWhereReflection
{
    internal static readonly MethodInfo Asc =
        typeof(CommonFunctions).GetMethod("asc", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo Desc =
        typeof(CommonFunctions).GetMethod("desc", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo RowNumber =
        typeof(CommonFunctions).GetMethod("row_number", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo Rank =
        typeof(CommonFunctions).GetMethod("rank", BindingFlags.Public | BindingFlags.Instance)!;

    internal static readonly MethodInfo ColumnOfInt =
        typeof(SqlFunctions).GetMethod("Column", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(int));

    internal static readonly MethodInfo Over =
        typeof(WindowFunction<int>).GetMethod(
            "Over",
            new[] { typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame) })!;
}

public static class ExternalSelectWhereExtensions
{
    public static EntityBuilder<TEntity> ExternalSelectWhereMin<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(new ExternalSelectWhereComponent<TEntity>(ExternalExtremeKind.Min, valueSelector, ties, groupBy));

    public static EntityBuilder<TEntity> ExternalSelectWhereMax<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(new ExternalSelectWhereComponent<TEntity>(ExternalExtremeKind.Max, valueSelector, ties, groupBy));

    public static QueryCommand<TResult> ExternalSelectWhereMin<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return source.WithComponent(new ExternalSelectWhereComponent<TEntity>(ExternalExtremeKind.Min, valueSelector, ties, groupBy))
            .Select(projection);
    }

    public static QueryCommand<TResult> ExternalSelectWhereMax<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        return source.WithComponent(new ExternalSelectWhereComponent<TEntity>(ExternalExtremeKind.Max, valueSelector, ties, groupBy))
            .Select(projection);
    }
}

public sealed class ExternalSelectWhereComponent<TEntity> : IQueryComponent
{
    public static int BuildCount;
    public static void ResetBuildCount() => BuildCount = 0;
    private readonly ExternalExtremeKind _kind;
    private readonly LambdaExpression _valueSelector;
    private readonly ExtremeRowTies _ties;
    private readonly Expression<Func<TEntity, object?>>? _groupBy;

    public ExternalSelectWhereComponent(
        ExternalExtremeKind kind,
        LambdaExpression valueSelector,
        ExtremeRowTies ties,
        Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);

        _kind = kind;
        _valueSelector = valueSelector;
        _ties = ties;
        _groupBy = groupBy;
    }

    public Type Slot => typeof(ExternalSelectWhereComponent<TEntity>);

    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;


    private static void ValidateCompatibility(QueryCommand cmd)
    {
        if (cmd.Ctes is { Count: > 0 }) throw NotCompatible("common table expressions (WITH)");
        if (cmd.Having is not null) throw NotCompatible("HAVING");
        if (cmd.Windows is { Count: > 0 }) throw NotCompatible("named windows");
        if (cmd.GroupingList is { Length: > 0 } || cmd.GroupBy is not null) throw NotCompatible("GROUP BY");
        if (cmd.Joins is { Length: > 0 }) throw NotCompatible("joins");
        if (cmd.PreWhere is not null) throw NotCompatible("PREWHERE");
        if (!cmd.Paging.IsEmpty) throw NotCompatible("paging");
        if (cmd.UnionQuery is not null) throw NotCompatible("a set operation");
        if (cmd.TableHints is { Count: > 0 }) throw NotCompatible("table hints");
        if (cmd.IndexHints is { Count: > 0 }) throw NotCompatible("index hints");
        if (cmd.TablesInScopeHints is { Count: > 0 }) throw NotCompatible("tables-in-scope hints");
        if (cmd.Hints is { Count: > 0 }) throw NotCompatible("query hints");
        if (cmd.Final) throw NotCompatible("FINAL");
        if (cmd.SampleRatio is not null) throw NotCompatible("SAMPLE");
        if (cmd.Settings is { Count: > 0 }) throw NotCompatible("SETTINGS");
        if (cmd.ForJsonClause is not null || cmd.ForXmlClause is not null) throw NotCompatible("FOR JSON/FOR XML");
    }

    private static BuildSqlCommandException NotCompatible(string modifier)
        => new($"SelectWhereMax/SelectWhereMin cannot be combined with {modifier}.");

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException("ExternalSelectWhereMin/Max is not supported by the in-memory provider.");

        // The ties value is validated here rather than at construction: attaching a component must stay
        // as cheap as attaching the built-in clause (AC7 construction phase), while an unsupported
        // value is still rejected explicitly during preparation.
        if (_ties is not (ExtremeRowTies.One or ExtremeRowTies.All))
            throw new ArgumentOutOfRangeException(nameof(_ties), _ties, "Unknown ExtremeRowTies value.");

        ValidateCompatibility(context.Command);

        var comparer = context.ExpressionComparer;
        var discriminator = _kind == ExternalExtremeKind.Min ? "Min" : "Max";
        var hash = new HashCode();
        hash.Add(Slot);
        hash.Add(GetType());
        hash.Add(discriminator);
        hash.Add(_ties);
        hash.Add(_valueSelector, comparer);
        if (_groupBy is not null)
            hash.Add(_groupBy, comparer);

        var captured = new CapturedState(_kind, _ties, _valueSelector, _groupBy, comparer);
        return new Descriptor(this, captured, hash.ToHashCode());
    }

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        ExternalSelectWhereComponent<TEntity>.BuildCount++;
        ArgumentNullException.ThrowIfNull(context);
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var rankAlias = MakeRankAlias(entityType);
        var entityParam = Expression.Parameter(entityType, "x");
        var valueComponents = DecomposeSelector(_valueSelector, entityParam);
        var groupComponents = _groupBy is null ? Array.Empty<Expression>() : DecomposeSelector(_groupBy, entityParam);

        var inner = cmd.Clone();
        inner.Components = null;
        // The built-in portable lowering excludes rows with a NULL in any value key. Apply the same
        // filter inside the ranked derived source, before the window is evaluated.
        Expression? innerFilter = null;
        for (var i = 0; i < valueComponents.Length; i++)
        {
            var component = valueComponents[i];
            if (IsNullable(component.Type))
            {
                var notNull = Expression.NotEqual(component, Expression.Constant(null, component.Type));
                innerFilter = innerFilter is null ? notNull : Expression.AndAlso(innerFilter, notNull);
            }
        }
        var existingCondition = inner.GetComponentPreparedCondition();
        if (existingCondition is LambdaExpression conditionLambda && conditionLambda.Parameters.Count > 0)
        {
            var conditionBody = new ParameterReplacer(conditionLambda.Parameters[0], entityParam).Visit(conditionLambda.Body);
            innerFilter = innerFilter is null ? conditionBody : Expression.AndAlso(innerFilter, conditionBody);
        }
        else if (existingCondition is not null)
        {
            innerFilter = innerFilter is null ? existingCondition : Expression.AndAlso(innerFilter, existingCondition);
        }
        inner.SetComponentPreparedCondition(innerFilter is null ? null : Expression.Lambda(innerFilter, entityParam));

        var sqlProperty = Expression.Property(null, typeof(SqlFunctions), "Sql");
        var rankMethod = _ties == ExtremeRowTies.All
            ? ExternalSelectWhereReflection.Rank
            : ExternalSelectWhereReflection.RowNumber;
        var rankCall = Expression.Call(sqlProperty, rankMethod);

        var directionMethod = _kind == ExternalExtremeKind.Max
            ? ExternalSelectWhereReflection.Desc
            : ExternalSelectWhereReflection.Asc;
        var orderCalls = new Expression[valueComponents.Length];
        for (var i = 0; i < valueComponents.Length; i++)
        {
            var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(valueComponents[i], typeof(object)));
            orderCalls[i] = Expression.Call(sqlProperty, directionMethod, Expression.Quote(lambda));
        }
        var orderArray = Expression.NewArrayInit(typeof(WindowOrder), orderCalls);

        Expression partitionArg;
        if (groupComponents.Length == 0)
        {
            partitionArg = Expression.Constant(null, typeof(Expression<Func<object?>>[]));
        }
        else
        {
            var partitionLambdas = new Expression[groupComponents.Length];
            for (var i = 0; i < groupComponents.Length; i++)
            {
                var lambda = Expression.Lambda<Func<object?>>(Expression.Convert(groupComponents[i], typeof(object)));
                partitionLambdas[i] = Expression.Quote(lambda);
            }
            partitionArg = Expression.NewArrayInit(typeof(Expression<Func<object?>>), partitionLambdas);
        }

        var overMethod = ExternalSelectWhereReflection.Over;
        var rankExpression = Expression.Call(
            rankCall,
            overMethod,
            partitionArg,
            orderArray,
            Expression.Constant(null, typeof(WindowFrame)));

        var rankSelect = new SelectExpression(typeof(int))
        {
            Index = inner.SelectList?.Length ?? 0,
            PropertyName = rankAlias,
            Expression = rankExpression
        };

        var innerSelects = inner.SelectList is null
            ? new[] { rankSelect }
            : inner.SelectList.Concat(new[] { rankSelect }).ToArray();
        inner.SetComponentSelectList(innerSelects);
        inner.SetComponentSorting(null);

        // The engine hands this method a private clone of the live command (SqlBuilder.MakeSelect), so
        // the rebuilt outer statement is written in place: no second full command clone per build.
        cmd.From = new FromExpression(inner);

        var outerParam = Expression.Parameter(entityType, "o");
        var columnCall = Expression.Call(null, ExternalSelectWhereReflection.ColumnOfInt, outerParam, Expression.Constant(rankAlias));
        var rankFilter = Expression.Lambda(Expression.Equal(columnCall, Expression.Constant(1)), outerParam);
        cmd.SetComponentPreparedCondition(rankFilter);
    }


    private static Expression[] DecomposeSelector(LambdaExpression selector, ParameterExpression entityParameter)
    {
        var body = StripBoxing(selector.Body);
        if (body is NewExpression { Arguments.Count: > 0 } ctor)
        {
            var result = new Expression[ctor.Arguments.Count];
            for (var i = 0; i < result.Length; i++)
                result[i] = new ParameterReplacer(selector.Parameters[0], entityParameter).Visit(ctor.Arguments[i])!;
            return result;
        }

        return [new ParameterReplacer(selector.Parameters[0], entityParameter).Visit(body)!];
    }


    private static bool IsNullable(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static Expression StripBoxing(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert, Type: var type } convert && type == typeof(object)
            ? convert.Operand
            : expression;

    private static string MakeRankAlias(Type entityType)
    {
        const string seed = "__nextorm_ext_rn";
        if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return seed;

        var name = seed;
        while (true)
        {
            var collides = false;
            foreach (var property in metadata.Properties)
            {
                if (string.Equals(property.ColumnName, name, StringComparison.OrdinalIgnoreCase))
                {
                    collides = true;
                    break;
                }
            }
            if (!collides)
                return name;
            name += "_";
        }
    }

    private sealed class CapturedState
    {
        public CapturedState(ExternalExtremeKind kind, ExtremeRowTies ties, Expression valueSelector, Expression? groupBy, IEqualityComparer<Expression> comparer)
        {
            Kind = kind;
            Ties = ties;
            ValueSelector = valueSelector;
            GroupBy = groupBy;
            Comparer = comparer;
        }

        public ExternalExtremeKind Kind { get; }
        public ExtremeRowTies Ties { get; }
        public Expression ValueSelector { get; }
        public Expression? GroupBy { get; }
        public IEqualityComparer<Expression> Comparer { get; }
    }

    private sealed class Descriptor : IQueryComponentDescriptor
    {
        private readonly ExternalSelectWhereComponent<TEntity> _component;
        private readonly CapturedState _state;

        public Descriptor(ExternalSelectWhereComponent<TEntity> component, CapturedState state, int hash)
        {
            _component = component;
            _state = state;
            StructuralHash = hash;
        }

        public Type Slot => _component.Slot;
        public Type ComponentType => _component.GetType();
        public QueryComponentContribution Contribution => _component.Contribution;
        public string Discriminator => _state.Kind == ExternalExtremeKind.Min ? "Min" : "Max";
        public int StructuralHash { get; }

        public bool StructurallyEquals(IQueryComponentDescriptor? other)
        {
            if (other is not Descriptor rhs)
                return false;
            if (Slot != rhs.Slot || ComponentType != rhs.ComponentType || Contribution != rhs.Contribution)
                return false;
            if (Discriminator != rhs.Discriminator || _state.Ties != rhs._state.Ties)
                return false;
            if (!_state.Comparer.Equals(_state.ValueSelector, rhs._state.ValueSelector))
                return false;
            if (_state.GroupBy is null || rhs._state.GroupBy is null)
                return _state.GroupBy is null && rhs._state.GroupBy is null;
            return _state.Comparer.Equals(_state.GroupBy, rhs._state.GroupBy);
        }
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public ParameterReplacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}

public sealed class ConflictingReshapeComponent<TEntity> : IQueryComponent
{
    public Type Slot => typeof(ConflictingReshapeComponent<TEntity>);
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;
    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context) =>
        new SimpleDescriptor(Slot, GetType(), Contribution, "Reshape", 0, (a, b) => true);
    public void BuildQuery(IQueryComponentBuildContext context) { }
}

internal sealed class SimpleDescriptor : IQueryComponentDescriptor
{
    private readonly Func<IQueryComponentDescriptor, IQueryComponentDescriptor, bool> _equals;
    public SimpleDescriptor(Type slot, Type componentType, QueryComponentContribution contribution, string discriminator, int hash, Func<IQueryComponentDescriptor, IQueryComponentDescriptor, bool> equals)
    {
        Slot = slot;
        ComponentType = componentType;
        Contribution = contribution;
        Discriminator = discriminator;
        StructuralHash = hash;
        _equals = equals;
    }
    public Type Slot { get; }
    public Type ComponentType { get; }
    public QueryComponentContribution Contribution { get; }
    public string Discriminator { get; }
    public int StructuralHash { get; }
    public bool StructurallyEquals(IQueryComponentDescriptor? other) => other is not null && _equals(this, other);
}
public sealed class AdditivePredicateComponent<TEntity> : IQueryComponent
{
    private readonly LambdaExpression _predicate;

    public AdditivePredicateComponent(LambdaExpression predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _predicate = predicate;
    }

    public Type Slot => typeof(AdditivePredicateComponent<TEntity>);
    public QueryComponentContribution Contribution => QueryComponentContribution.AdditivePredicate;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
        => new SimpleDescriptor(Slot, GetType(), Contribution, "Predicate", 0, (a, b) => true);

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var param = Expression.Parameter(entityType, "x");

        Expression? body = null;
        var existing = cmd.GetComponentPreparedCondition();
        if (existing is LambdaExpression existingLambda)
            body = new Replacer(existingLambda.Parameters[0], param).Visit(existingLambda.Body);

        var predicateBody = new Replacer(_predicate.Parameters[0], param).Visit(_predicate.Body);
        var combined = body is null ? predicateBody : Expression.AndAlso(body, predicateBody);
        cmd.SetComponentPreparedCondition(Expression.Lambda(combined, param));
    }

    private sealed class Replacer : ExpressionVisitor
    {
        private readonly ParameterExpression _oldParameter;
        private readonly ParameterExpression _newParameter;

        public Replacer(ParameterExpression oldParameter, ParameterExpression newParameter)
        {
            _oldParameter = oldParameter;
            _newParameter = newParameter;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _oldParameter ? _newParameter : base.VisitParameter(node);
    }
}
