using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Terminal operators for <see cref="EntityBuilder{TEntity}"/>. They are extension methods so the
/// builder itself stays a focused query-shaping type; each one simply forwards to the command it
/// builds. Call sites are unchanged (instance-style invocation still resolves here).
/// </summary>
public static class EntityBuilderExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => builder.ToAsyncEnumerable(CancellationToken.None, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params) => builder.ToCommand().ToAsyncEnumerable(cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TEntity> ToEnumerable<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => builder.ToCommand().ToEnumerable(@params);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any<TEntity>(this EntityBuilder<TEntity> builder) => Any(builder, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => AnyCore(builder, @params);
    private static bool AnyCore<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params)
    {
        var cmd = builder.ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(builder.DataProvider, cmd);
        var preparedCommand = builder.DataProvider.GetPreparedQueryCommand(queryCommand, false, true, CancellationToken.None);
        return builder.DataProvider.ExecuteScalar<bool>(preparedCommand, @params, true);
    }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => AnyAsync(builder, CancellationToken.None, @params);
    public static async Task<bool> AnyAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = builder.ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(builder.DataProvider, cmd);
        var preparedCommand = builder.DataProvider.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        return await builder.DataProvider.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
    }

    public static QueryCommand<bool> AnyCommand<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var cmd = builder.ToCommand();
        var queryCommand = builder.DataProvider.CreateCommand<bool>(new QueryDefinition
        {
            Exp = (TableAlias _) => SqlFunctions.Sql.exists(cmd),
            Logger = builder.Logger,
        });
        queryCommand.SingleRow = true;
        cmd.IgnoreColumns = true;
        return queryCommand;
    }
    public static QueryCommand<TEntity?> FirstOrFirstOrDefaultCommand<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var cmd = builder.ToCommand();
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public static QueryCommand<TResult?> FirstOrFirstOrDefaultCommand<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = builder.Select(exp);
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public static QueryCommand<TResult?> SingleOrSingleOrDefaultCommand<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = builder.Select(exp);
        cmd.Paging.Limit = 2;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    public static QueryCommand<TEntity> SingleOrSingleOrDefaultCommand<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var cmd = builder.ToCommand();
        cmd.Paging.Limit = 2;
        return cmd;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TEntity> ToList<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().ToList(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<List<TEntity>> ToListAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToListAsync(builder, CancellationToken.None, @params);
    public static Task<List<TEntity>> ToListAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToListAsync(cancellationToken, @params);
    public static TEntity[] ToArray<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => builder.ToCommand().ToArray(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity[]> ToArrayAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToArrayAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity[]> ToArrayAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToArrayAsync(cancellationToken, @params);
    public static HashSet<TEntity> ToHashSet<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => builder.ToCommand().ToHashSet(@params);
    public static HashSet<TEntity> ToHashSet<TEntity>(this EntityBuilder<TEntity> builder, IEqualityComparer<TEntity>? comparer, params ReadOnlySpan<object?> @params) => builder.ToCommand().ToHashSet(comparer, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<HashSet<TEntity>> ToHashSetAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToHashSetAsync(builder, CancellationToken.None, @params);
    public static Task<HashSet<TEntity>> ToHashSetAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToHashSetAsync(cancellationToken, @params);
    public static Task<HashSet<TEntity>> ToHashSetAsync<TEntity>(this EntityBuilder<TEntity> builder, IEqualityComparer<TEntity>? comparer, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToHashSetAsync(comparer, cancellationToken, @params);
    public static Dictionary<TKey, TEntity> ToDictionary<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, params ReadOnlySpan<object?> @params) where TKey : notnull
        => builder.ToCommand().ToDictionary(keySelector, @params);
    public static Dictionary<TKey, TEntity> ToDictionary<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer, params ReadOnlySpan<object?> @params) where TKey : notnull
        => builder.ToCommand().ToDictionary(keySelector, comparer, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(builder, keySelector, CancellationToken.None, @params);
    public static Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
        => builder.ToCommand().ToDictionaryAsync(keySelector, cancellationToken, @params);
    public static Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
        => builder.ToCommand().ToDictionaryAsync(keySelector, comparer, cancellationToken, @params);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity First<TEntity>(this EntityBuilder<TEntity> builder) => First(builder, ReadOnlySpan<object?>.Empty);
    public static TEntity First<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().First(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity> FirstAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => FirstAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity> FirstAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().FirstAsync(cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity? FirstOrDefault<TEntity>(this EntityBuilder<TEntity> builder) => FirstOrDefault(builder, ReadOnlySpan<object?>.Empty);
    public static TEntity? FirstOrDefault<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().FirstOrDefault(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity?> FirstOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => FirstOrDefaultAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity?> FirstOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().FirstOrDefaultAsync(cancellationToken, @params);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity Single<TEntity>(this EntityBuilder<TEntity> builder) => Single(builder, ReadOnlySpan<object?>.Empty);
    public static TEntity Single<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().Single(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity> SingleAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => SingleAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity> SingleAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().SingleAsync(cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity? SingleOrDefault<TEntity>(this EntityBuilder<TEntity> builder) => SingleOrDefault(builder, ReadOnlySpan<object?>.Empty);
    public static TEntity? SingleOrDefault<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().SingleOrDefault(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity?> SingleOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => SingleOrDefaultAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity?> SingleOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().SingleOrDefaultAsync(cancellationToken, @params);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity Last<TEntity>(this EntityBuilder<TEntity> builder) => Last(builder, ReadOnlySpan<object?>.Empty);
    public static TEntity Last<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().Last(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity> LastAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => LastAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity> LastAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().LastAsync(cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity? LastOrDefault<TEntity>(this EntityBuilder<TEntity> builder) => LastOrDefault(builder, ReadOnlySpan<object?>.Empty);
    public static TEntity? LastOrDefault<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().LastOrDefault(@params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity?> LastOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => LastOrDefaultAsync(builder, CancellationToken.None, @params);
    public static Task<TEntity?> LastOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().LastOrDefaultAsync(cancellationToken, @params);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPreparedQueryCommand<TEntity> Prepare<TEntity>(this EntityBuilder<TEntity> builder, bool nonStreamUsing = true, CancellationToken cancellationToken = default) => builder.ToCommand().Prepare(nonStreamUsing, cancellationToken);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<TEntity>(this EntityBuilder<TEntity> builder) => CountCore(builder, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => CountCore(builder, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<int> CountAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => CountAsync(builder, CancellationToken.None, @params);
    public static Task<int> CountAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = builder.Select(e => SqlFunctions.Sql.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }

    // The eight aggregate families differ only in the CommonFunctions method they wrap, so every public
    // member is a one-line forwarder and the body lives once in AggregateCore/AggregateAsyncCore.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Min<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.MinMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Min<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.MinMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> MinAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => MinAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> MinAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.MinMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Max<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.MaxMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Max<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.MaxMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> MaxAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => MaxAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> MaxAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.MaxMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Avg<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.AvgMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Avg<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.AvgMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> AvgAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => AvgAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> AvgAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.AvgMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Sum<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.SumMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Sum<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.SumMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> SumAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => SumAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> SumAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.SumMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdev<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.StdevMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdev<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.StdevMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> StdevAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> StdevAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.StdevMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdevp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.StdevpMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdevp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.StdevpMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> StdevpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevpAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> StdevpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.StdevpMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Var<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.VarMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Var<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.VarMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> VarAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> VarAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.VarMI, exp, cancellationToken, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Varp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.VarpMI, exp, ReadOnlySpan<object?>.Empty);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Varp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.VarpMI, exp, @params);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> VarpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarpAsync(builder, exp, CancellationToken.None, @params);
    public static Task<TResult?> VarpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.VarpMI, exp, cancellationToken, @params);

    internal static QueryCommand<bool> GetAnyCommand(IDataContext dataProvider, QueryCommand cmd)
    {
        var created = false;
        if (dataProvider.AnyCommand is not Lazy<QueryCommand<bool>> anyCommand)
        {
            anyCommand = new Lazy<QueryCommand<bool>>(() =>
            {
                created = true;
                var queryCommand = dataProvider.CreateCommand<bool>(new QueryDefinition
                {
                    Exp = (TableAlias _) => SqlFunctions.Sql.exists(cmd),
                    Logger = cmd.Logger,
                });
                queryCommand.SingleRow = true;
                queryCommand.PrepareCommand(false, CancellationToken.None);
                return queryCommand;
            });
            dataProvider.AnyCommand = anyCommand;
        }

        var queryCommand = anyCommand.Value;
        if (!created)
        {
            if (!cmd.IsPrepared) cmd.PrepareCommand(false, CancellationToken.None);
            queryCommand.ReplaceCommand(cmd, 0);
        }

        return queryCommand;
    }

    private static int CountCore<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params)
    {
        var cmd = builder.Select(e => SqlFunctions.Sql.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }

    private static TResult? AggregateCore<TEntity, TResult>(EntityBuilder<TEntity> builder, MethodInfo sqlMethod, Expression<Func<TEntity, TResult>> exp, ReadOnlySpan<object?> @params)
    {
        var cmd = builder.Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(CommonFunctions.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }

    private static Task<TResult?> AggregateAsyncCore<TEntity, TResult>(EntityBuilder<TEntity> builder, MethodInfo sqlMethod, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, object[] @params)
    {
        var cmd = builder.Select(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(CommonFunctions.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
}
