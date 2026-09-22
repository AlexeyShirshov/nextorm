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
    /// <summary>
    /// Streams the matching entities as an asynchronous sequence without buffering the whole result set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>An asynchronous sequence over the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => builder.ToAsyncEnumerable(CancellationToken.None, @params);
    /// <summary>
    /// Streams the matching entities as an asynchronous sequence without buffering the whole result set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>An asynchronous sequence over the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params) => builder.ToCommand().ToAsyncEnumerable(cancellationToken, @params);
    /// <summary>
    /// Executes the query and returns the matching entities as a synchronous sequence.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A sequence over the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TEntity> ToEnumerable<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => builder.ToCommand().ToEnumerable(@params);

    /// <summary>
    /// Determines whether the query matches at least one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>true if the query matches at least one row; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any<TEntity>(this EntityBuilder<TEntity> builder) => Any(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Determines whether the query matches at least one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>true if the query matches at least one row; otherwise, false.</returns>
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
    /// <summary>
    /// Determines asynchronously whether the query matches at least one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is true if the query matches at least one row; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => AnyAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Determines asynchronously whether the query matches at least one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is true if the query matches at least one row; otherwise, false.</returns>
    public static async Task<bool> AnyAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = builder.ToCommand();
        cmd.IgnoreColumns = true;
        var queryCommand = GetAnyCommand(builder.DataProvider, cmd);
        var preparedCommand = builder.DataProvider.GetPreparedQueryCommand(queryCommand, false, true, cancellationToken);
        return await builder.DataProvider.ExecuteScalar<bool>(preparedCommand, @params, true, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the command that evaluates whether the query matches any rows.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The command that evaluates EXISTS for the query.</returns>
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
    /// <summary>
    /// Builds a command that requests at most one row, shared by First and FirstOrDefault.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>A command that requests at most one row.</returns>
    public static QueryCommand<TEntity?> FirstOrFirstOrDefaultCommand<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var cmd = builder.ToCommand();
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    /// <summary>
    /// Builds a command that requests at most one row, shared by First and FirstOrDefault.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to project.</param>
    /// <returns>A command that requests at most one row.</returns>
    public static QueryCommand<TResult?> FirstOrFirstOrDefaultCommand<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = builder.Select(exp);
        cmd.Paging.Limit = 1;
        cmd.SingleRow = true;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    /// <summary>
    /// Builds a command that requests at most two rows, enough to tell Single from SingleOrDefault at execution time.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to project.</param>
    /// <returns>A command that requests at most two rows.</returns>
    public static QueryCommand<TResult?> SingleOrSingleOrDefaultCommand<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp)
    {
        var cmd = builder.Select(exp);
        cmd.Paging.Limit = 2;
#pragma warning disable CS8619 // Nullability of reference types in value doesn't match target type.
        return cmd;
#pragma warning restore CS8619 // Nullability of reference types in value doesn't match target type.
    }
    /// <summary>
    /// Builds a command that requests at most two rows, enough to tell Single from SingleOrDefault at execution time.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>A command that requests at most two rows.</returns>
    public static QueryCommand<TEntity> SingleOrSingleOrDefaultCommand<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var cmd = builder.ToCommand();
        cmd.Paging.Limit = 2;
        return cmd;
    }

    /// <summary>
    /// Executes the query and materializes the matching entities into a list.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A list containing the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TEntity> ToList<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().ToList(@params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into a list.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a list containing the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<List<TEntity>> ToListAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToListAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into a list.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a list containing the matching entities.</returns>
    public static Task<List<TEntity>> ToListAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToListAsync(cancellationToken, @params);
    /// <summary>
    /// Executes the query and materializes the matching entities into an array.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>An array containing the matching entities.</returns>
    public static TEntity[] ToArray<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => builder.ToCommand().ToArray(@params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into an array.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is an array containing the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity[]> ToArrayAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToArrayAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into an array.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is an array containing the matching entities.</returns>
    public static Task<TEntity[]> ToArrayAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToArrayAsync(cancellationToken, @params);
    /// <summary>
    /// Executes the query and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A hash set containing the distinct matching entities.</returns>
    public static HashSet<TEntity> ToHashSet<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => builder.ToCommand().ToHashSet(@params);
    /// <summary>
    /// Executes the query and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="comparer">The equality comparer to use, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A hash set containing the distinct matching entities.</returns>
    public static HashSet<TEntity> ToHashSet<TEntity>(this EntityBuilder<TEntity> builder, IEqualityComparer<TEntity>? comparer, params ReadOnlySpan<object?> @params) => builder.ToCommand().ToHashSet(comparer, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a hash set containing the distinct matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<HashSet<TEntity>> ToHashSetAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToHashSetAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a hash set containing the distinct matching entities.</returns>
    public static Task<HashSet<TEntity>> ToHashSetAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToHashSetAsync(cancellationToken, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="comparer">The equality comparer to use, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a hash set containing the distinct matching entities.</returns>
    public static Task<HashSet<TEntity>> ToHashSetAsync<TEntity>(this EntityBuilder<TEntity> builder, IEqualityComparer<TEntity>? comparer, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().ToHashSetAsync(comparer, cancellationToken, @params);
    /// <summary>
    /// Executes the query and materializes the matching entities into a dictionary keyed by the projected values.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="keySelector">A function that derives the dictionary key from an entity.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A dictionary of matching entities keyed by the selected keys.</returns>
    public static Dictionary<TKey, TEntity> ToDictionary<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, params ReadOnlySpan<object?> @params) where TKey : notnull
        => builder.ToCommand().ToDictionary(keySelector, @params);
    /// <summary>
    /// Executes the query and materializes the matching entities into a dictionary keyed by the projected values.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="keySelector">A function that derives the dictionary key from an entity.</param>
    /// <param name="comparer">The equality comparer to use, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A dictionary of matching entities keyed by the selected keys.</returns>
    public static Dictionary<TKey, TEntity> ToDictionary<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer, params ReadOnlySpan<object?> @params) where TKey : notnull
        => builder.ToCommand().ToDictionary(keySelector, comparer, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into a dictionary keyed by the projected values.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="keySelector">A function that derives the dictionary key from an entity.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a dictionary of matching entities keyed by the selected keys.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, params object[] @params) where TKey : notnull
        => ToDictionaryAsync(builder, keySelector, CancellationToken.None, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into a dictionary keyed by the projected values.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="keySelector">A function that derives the dictionary key from an entity.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a dictionary of matching entities keyed by the selected keys.</returns>
    public static Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
        => builder.ToCommand().ToDictionaryAsync(keySelector, cancellationToken, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into a dictionary keyed by the projected values.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TKey">The type of the dictionary key.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="keySelector">A function that derives the dictionary key from an entity.</param>
    /// <param name="comparer">The equality comparer to use, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is a dictionary of matching entities keyed by the selected keys.</returns>
    public static Task<Dictionary<TKey, TEntity>> ToDictionaryAsync<TEntity, TKey>(this EntityBuilder<TEntity> builder, Func<TEntity, TKey> keySelector, IEqualityComparer<TKey>? comparer, CancellationToken cancellationToken, params object[] @params) where TKey : notnull
        => builder.ToCommand().ToDictionaryAsync(keySelector, comparer, cancellationToken, @params);

    /// <summary>
    /// Returns the first matching entity and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The first matching entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity First<TEntity>(this EntityBuilder<TEntity> builder) => First(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Returns the first matching entity and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The first matching entity.</returns>
    public static TEntity First<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().First(@params);
    /// <summary>
    /// Returns the first matching entity asynchronously and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the first matching entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity> FirstAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => FirstAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Returns the first matching entity asynchronously and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the first matching entity.</returns>
    public static Task<TEntity> FirstAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().FirstAsync(cancellationToken, @params);
    /// <summary>
    /// Returns the first matching entity, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The first matching entity, or the default value when the query is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity? FirstOrDefault<TEntity>(this EntityBuilder<TEntity> builder) => FirstOrDefault(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Returns the first matching entity, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The first matching entity, or the default value when the query is empty.</returns>
    public static TEntity? FirstOrDefault<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().FirstOrDefault(@params);
    /// <summary>
    /// Returns the first matching entity asynchronously, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the first matching entity, or the default value when the query is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity?> FirstOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => FirstOrDefaultAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Returns the first matching entity asynchronously, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the first matching entity, or the default value when the query is empty.</returns>
    public static Task<TEntity?> FirstOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().FirstOrDefaultAsync(cancellationToken, @params);

    /// <summary>
    /// Returns the only matching entity and throws when the query returns zero or more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The single matching entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity Single<TEntity>(this EntityBuilder<TEntity> builder) => Single(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Returns the only matching entity and throws when the query returns zero or more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The single matching entity.</returns>
    public static TEntity Single<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().Single(@params);
    /// <summary>
    /// Returns the only matching entity asynchronously and throws when the query returns zero or more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the single matching entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity> SingleAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => SingleAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Returns the only matching entity asynchronously and throws when the query returns zero or more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the single matching entity.</returns>
    public static Task<TEntity> SingleAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().SingleAsync(cancellationToken, @params);
    /// <summary>
    /// Returns the only matching entity, or the default value when the query is empty; throws when it returns more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The single matching entity, or the default value when the query is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity? SingleOrDefault<TEntity>(this EntityBuilder<TEntity> builder) => SingleOrDefault(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Returns the only matching entity, or the default value when the query is empty; throws when it returns more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The single matching entity, or the default value when the query is empty.</returns>
    public static TEntity? SingleOrDefault<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().SingleOrDefault(@params);
    /// <summary>
    /// Returns the only matching entity asynchronously, or the default value when the query is empty; throws when it returns more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the single matching entity, or the default value when the query is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity?> SingleOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => SingleOrDefaultAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Returns the only matching entity asynchronously, or the default value when the query is empty; throws when it returns more than one row.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the single matching entity, or the default value when the query is empty.</returns>
    public static Task<TEntity?> SingleOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().SingleOrDefaultAsync(cancellationToken, @params);

    /// <summary>
    /// Returns the last matching entity and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The last matching entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity Last<TEntity>(this EntityBuilder<TEntity> builder) => Last(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Returns the last matching entity and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The last matching entity.</returns>
    public static TEntity Last<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().Last(@params);
    /// <summary>
    /// Returns the last matching entity asynchronously and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the last matching entity.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity> LastAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => LastAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Returns the last matching entity asynchronously and throws when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the last matching entity.</returns>
    public static Task<TEntity> LastAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().LastAsync(cancellationToken, @params);
    /// <summary>
    /// Returns the last matching entity, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The last matching entity, or the default value when the query is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TEntity? LastOrDefault<TEntity>(this EntityBuilder<TEntity> builder) => LastOrDefault(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Returns the last matching entity, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The last matching entity, or the default value when the query is empty.</returns>
    public static TEntity? LastOrDefault<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => builder.ToCommand().LastOrDefault(@params);
    /// <summary>
    /// Returns the last matching entity asynchronously, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the last matching entity, or the default value when the query is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity?> LastOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => LastOrDefaultAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Returns the last matching entity asynchronously, or the default value when the query is empty.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the last matching entity, or the default value when the query is empty.</returns>
    public static Task<TEntity?> LastOrDefaultAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
        => builder.ToCommand().LastOrDefaultAsync(cancellationToken, @params);

    /// <summary>
    /// Creates a command with its SQL already compiled so it can be executed repeatedly, typically with different parameters.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="nonStreamUsing">Whether the prepared command favors buffered (non-streaming) execution.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The prepared query command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPreparedQueryCommand<TEntity> Prepare<TEntity>(this EntityBuilder<TEntity> builder, bool nonStreamUsing = true, CancellationToken cancellationToken = default) => builder.ToCommand().Prepare(nonStreamUsing, cancellationToken);

    /// <summary>
    /// Counts the matching rows by emitting a SQL <c>COUNT(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The number of matching rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<TEntity>(this EntityBuilder<TEntity> builder) => CountCore(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Counts the matching rows by emitting a SQL <c>COUNT(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The number of matching rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Count<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => CountCore(builder, @params);
    /// <summary>
    /// Counts the matching rows asynchronously by emitting a SQL <c>COUNT(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the number of matching rows.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<int> CountAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => CountAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Counts the matching rows asynchronously by emitting a SQL <c>COUNT(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the number of matching rows.</returns>
    public static Task<int> CountAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = builder.Select(e => SqlFunctions.Sql.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }

    // The eight aggregate families differ only in the CommonFunctions method they wrap, so every public
    // member is a one-line forwarder and the body lives once in AggregateCore/AggregateAsyncCore.
    /// <summary>
    /// Computes the minimum of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The minimum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Min<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.MinMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the minimum of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The minimum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Min<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.MinMI, exp, @params);
    /// <summary>
    /// Computes the minimum of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the minimum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> MinAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => MinAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the minimum of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the minimum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> MinAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.MinMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the maximum of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The maximum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Max<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.MaxMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the maximum of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The maximum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Max<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.MaxMI, exp, @params);
    /// <summary>
    /// Computes the maximum of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the maximum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> MaxAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => MaxAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the maximum of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the maximum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> MaxAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.MaxMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the average of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The average of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Avg<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.AvgMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the average of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The average of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Avg<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.AvgMI, exp, @params);
    /// <summary>
    /// Computes the average of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the average of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> AvgAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => AvgAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the average of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the average of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> AvgAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.AvgMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the sum of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The sum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Sum<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.SumMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the sum of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The sum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Sum<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.SumMI, exp, @params);
    /// <summary>
    /// Computes the sum of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the sum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> SumAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => SumAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the sum of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the sum of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> SumAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.SumMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the sample standard deviation of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The sample standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdev<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.StdevMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the sample standard deviation of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The sample standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdev<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.StdevMI, exp, @params);
    /// <summary>
    /// Computes the sample standard deviation of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the sample standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> StdevAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the sample standard deviation of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the sample standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> StdevAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.StdevMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the population standard deviation of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The population standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdevp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.StdevpMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the population standard deviation of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The population standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Stdevp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.StdevpMI, exp, @params);
    /// <summary>
    /// Computes the population standard deviation of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the population standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> StdevpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => StdevpAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the population standard deviation of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the population standard deviation of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> StdevpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.StdevpMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the sample variance of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The sample variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Var<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.VarMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the sample variance of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The sample variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Var<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.VarMI, exp, @params);
    /// <summary>
    /// Computes the sample variance of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the sample variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> VarAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the sample variance of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the sample variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    public static Task<TResult?> VarAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, params object[] @params) => AggregateAsyncCore(builder, CommonFunctions.VarMI, exp, cancellationToken, @params);
    /// <summary>
    /// Computes the population variance of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <returns>The population variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Varp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp) => AggregateCore(builder, CommonFunctions.VarpMI, exp, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Computes the population variance of the projected values. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The population variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TResult? Varp<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params ReadOnlySpan<object?> @params) => AggregateCore(builder, CommonFunctions.VarpMI, exp, @params);
    /// <summary>
    /// Computes the population variance of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the population variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> VarpAsync<TEntity, TResult>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TResult>> exp, params object[] @params) => VarpAsync(builder, exp, CancellationToken.None, @params);
    /// <summary>
    /// Computes the population variance of the projected values asynchronously. Rows whose projection is <see langword="null"/> are ignored, matching SQL aggregate semantics.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <typeparam name="TResult">The type produced by the projection expression.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="exp">An expression selecting the values to aggregate.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the population variance of the projected values, or <see langword="null"/> when the query has no non-null values.</returns>
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
