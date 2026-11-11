using System.Linq.Expressions;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using NextORM.Core;
using NextORM.QueryComponents;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// #362 attempt-3 diagnostic: the minimum work a public-API component must do to attach an ordinary
/// nested <see cref="QueryCommand"/> as the row source (<c>From</c>), append one hidden window column
/// and put an outer <c>rank = 1</c> predicate on it. No validation, no compatibility boundary, no NULL
/// filter, no composite decomposition, no captured/positional handling, no selector rewriting: only the
/// attach itself. It exists to measure the allocation floor of the nested-command attach so the
/// remaining cold-prepare gap can be attributed. It is not a product fix and not a candidate operator.
/// </summary>
public sealed class T362AttachOnlyMinComponent<TEntity> : IQueryComponent
{
    private readonly PropertyInfo _valueProperty;
    private readonly string _rankAlias;

    public T362AttachOnlyMinComponent(PropertyInfo valueProperty, string rankAlias)
    {
        _valueProperty = valueProperty;
        _rankAlias = rankAlias;
    }

    private static readonly MethodInfo AscMethod =
        typeof(CommonFunctions).GetMethod("asc", BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly MethodInfo RowNumberMethod =
        typeof(CommonFunctions).GetMethod("row_number", BindingFlags.Public | BindingFlags.Instance)!;

    private static readonly MethodInfo OverMethod =
        typeof(WindowFunction<int>).GetMethod(
            "Over",
            [typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame)])!;

    private static readonly MethodInfo ColumnOfIntMethod =
        typeof(SqlFunctions).GetMethod("Column", BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(typeof(int));

    public Type Slot => typeof(T362AttachOnlyMinComponent<TEntity>);

    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
        => new AttachOnlyDescriptor(Slot, GetType(), Contribution, "Min", 0);

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var entityType = typeof(TEntity);

        var inner = cmd.Clone();
        inner.Components = null;

        var entityParameter = Expression.Parameter(entityType, "x");
        var sql = Expression.Property(null, typeof(SqlFunctions), "Sql");
        var rankCall = Expression.Call(sql, RowNumberMethod);
        var orderLambda = Expression.Lambda<Func<object?>>(
            Expression.Convert(Expression.Property(entityParameter, _valueProperty), typeof(object)));
        var orderArray = Expression.NewArrayInit(
            typeof(WindowOrder),
            Expression.Call(sql, AscMethod, Expression.Quote(orderLambda)));
        var rankExpression = Expression.Call(
            rankCall,
            OverMethod,
            Expression.Constant(null, typeof(Expression<Func<object?>>[])),
            orderArray,
            Expression.Constant(null, typeof(WindowFrame)));

        var rankSelect = new SelectExpression(typeof(int))
        {
            Index = inner.SelectList?.Length ?? 0,
            PropertyName = _rankAlias,
            Expression = rankExpression,
        };

        var sourceSelects = inner.SelectList;
        var innerSelects = sourceSelects is null
            ? new[] { rankSelect }
            : new SelectExpression[sourceSelects.Length + 1];
        sourceSelects?.CopyTo(innerSelects, 0);
        innerSelects[^1] = rankSelect;
        inner.SetComponentSelectList(innerSelects);
        inner.SetComponentSorting(null);

        cmd.From = new FromExpression(inner);

        var outerParameter = Expression.Parameter(entityType, "o");
        var rankColumn = Expression.Call(
            null,
            ColumnOfIntMethod,
            outerParameter,
            Expression.Constant(_rankAlias));
        cmd.SetComponentPreparedCondition(
            Expression.Lambda(Expression.Equal(rankColumn, Expression.Constant(1)), outerParameter));
    }

    private sealed class AttachOnlyDescriptor : IQueryComponentDescriptor
    {
        public AttachOnlyDescriptor(
            Type slot,
            Type componentType,
            QueryComponentContribution contribution,
            string discriminator,
            int hash)
        {
            Slot = slot;
            ComponentType = componentType;
            Contribution = contribution;
            Discriminator = discriminator;
            StructuralHash = hash;
        }

        public Type Slot { get; }
        public Type ComponentType { get; }
        public QueryComponentContribution Contribution { get; }
        public string Discriminator { get; }
        public int StructuralHash { get; }

        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is AttachOnlyDescriptor;
    }
}

/// <summary>
/// #362 attempt-3 floor probe: cold preparation of the same Min/One/global/scalar command in three
/// arms — the forced-portable built-in baseline, the complete external component, and the attach-only
/// component above. The gap between the baseline and the attach-only arm is the cost the nested-command
/// attach itself imposes; the gap between the attach-only and the complete arm is what the operator's
/// own preparation work adds. Diagnostic only, never part of the acceptance category.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(T362Config))]
public class T362FloorProbeBenchmark
{
    private readonly SqliteDataContext _ctx = new($"Data Source={BenchDb.FilePath}", new DataContextBuilder());

    private static readonly PropertyInfo IntProperty = typeof(ComplexEntity).GetProperty(nameof(ComplexEntity.Int))!;

    private const string RankAlias = "__nextorm_ext_rn";

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    public void FloorProbe_ColdPrepare_Builtin()
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            T362Queries.Command(_ctx, ExternalExtremeKind.Min, ExtremeRowTies.One, grouped: false, composite: false, external: false),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    public void FloorProbe_ColdPrepare_External()
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            T362Queries.Command(_ctx, ExternalExtremeKind.Min, ExtremeRowTies.One, grouped: false, composite: false, external: true),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));

    [Benchmark]
    [BenchmarkCategory("T362", "query-components")]
    public void FloorProbe_ColdPrepare_AttachOnly()
        => GC.KeepAlive(_ctx.GetPreparedQueryCommand(
            (QueryCommand<ComplexEntity>)_ctx.From<ComplexEntity>()
                .WithComponent(new T362AttachOnlyMinComponent<ComplexEntity>(IntProperty, RankAlias)),
            createEnumerator: false,
            storeInCache: false,
            CancellationToken.None));
}
