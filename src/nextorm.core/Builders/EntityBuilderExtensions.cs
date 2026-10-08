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
    /// Binds a direct raw source (<see cref="DataContextExtensions.FromSql"/> or
    /// <see cref="DataContextExtensions.From(IDataContext, string)"/>) to an entity type so its global
    /// query filters may be applied to the source. <typeparamref name="TEntity"/> selects the effective
    /// metadata (a configured mapping wins over the auto mapping).
    /// <para>
    /// The caller also declares the output columns the raw SQL exposes; each filter is applied
    /// best-effort against that list, and a filter whose columns are missing is skipped rather than
    /// failing the query. Binding neither adds nor renames output columns and is <b>not</b> a security
    /// guarantee: a skipped filter means its predicate does not run.
    /// </para>
    /// <para>
    /// Only direct named/raw sources qualify, and the call must be the first operation after the source
    /// is created (before predicates, projection or joins). The receiver is not mutated; a new typed
    /// builder is returned.
    /// </para>
    /// <para>
    /// A filter whose declared columns are all present is injected; a filter with missing columns is
    /// skipped rather than failing the query. With an empty declared-column list, a filter with a proven
    /// zero-column dependency is applied, while a column-dependent or undetermined filter is skipped.
    /// One <c>RawSourceFilterSkipped</c> warning is logged per skipped filter on the
    /// <c>NextORM.QueryFilters</c> category (level <c>Warning</c>, reasons <c>MissingColumns</c> /
    /// <c>UndeterminedColumns</c>; the missing names are the physical mapped column names) while a plan is
    /// prepared on a cache miss; a cache hit does not re-emit it. The message carries no SQL text, table
    /// names, parameter or captured values.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type whose metadata and filters are used.</typeparam>
    /// <param name="source">
    /// The direct raw/named source builder created by <c>FromSql</c> or <c>From(string)</c>. The receiver
    /// must not be composed: a predicate, projection, join or other query operator rejects the call. A CTE
    /// declaration (<c>Ctes</c>) or a derived-table sub-query hint (<c>SubQueryHint</c>) is source metadata,
    /// not composition, and does not disqualify the binding.
    /// </param>
    /// <param name="availableColumns">The output columns the raw source exposes; names are compared case-insensitively.</param>
    /// <returns>A new typed builder over the same raw source, carrying the binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="availableColumns"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="availableColumns"/> contains a <see langword="null"/> or blank entry.</exception>
    /// <exception cref="NotSupportedException">The receiver is not a direct raw/named source, or <typeparamref name="TEntity"/> is exactly <see cref="TableAlias"/> (a mapped subclass of <see cref="TableAlias"/> is accepted).</exception>
    /// <exception cref="InvalidOperationException">The source has already been composed (predicate, projection, join or another query operator).</exception>
    public static EntityBuilder<TEntity> BindEntity<TEntity>(this EntityBuilder<TableAlias> source, IReadOnlyCollection<string> availableColumns)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(availableColumns);

        var binding = new FromExpression.EntityBinding(typeof(TEntity), NormalizeColumns(availableColumns));

        // Resolve the mapping eagerly so binding is never a silent no-op: a configured mapping wins over
        // the auto mapping, an unmapped type is auto-resolved, and a broken mapping fails here instead of
        // producing a query whose filters were silently dropped. The metadata is not frozen into the
        // binding; the filter resolver re-resolves it per preparation, so DataContextCache.Clear() is
        // honored. TableAlias is rejected later with NotSupportedException and has no mapping to resolve.
        if (typeof(TEntity) != typeof(TableAlias))
            _ = DataContextExtensions.ResolveMetadata(source.DataProvider, typeof(TEntity));

        return source.BindEntitySource<TEntity>(binding);
    }

    /// <summary>Copies, deduplicates and case-insensitively sorts the declared output columns.</summary>
    /// <param name="availableColumns">The caller-declared columns.</param>
    /// <returns>An owned, normalized column list.</returns>
    /// <exception cref="ArgumentException">An entry is <see langword="null"/> or blank.</exception>
    private static string[] NormalizeColumns(IReadOnlyCollection<string> availableColumns)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in availableColumns)
        {
            if (string.IsNullOrWhiteSpace(column))
                throw new ArgumentException("Available columns must be non-null and non-blank.", nameof(availableColumns));
            set.Add(column);
        }

        var result = new string[set.Count];
        set.CopyTo(result);
        Array.Sort(result, StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>Writes the builder's <c>Select</c> result as CSV to <paramref name="destination"/>.</summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="destination">The stream that receives the UTF-8 CSV; it stays open.</param>
    /// <param name="options">The CSV dialect options, or <c>null</c> for the defaults.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    public static void WriteCsv<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params ReadOnlySpan<object?> @params)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToParentCommand().WriteCsv(destination, options, cancellationToken, @params);
    }

    /// <summary>Asynchronously writes the builder's <c>Select</c> result as CSV to <paramref name="destination"/>.</summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="destination">The stream that receives the UTF-8 CSV; it stays open.</param>
    /// <param name="options">The CSV dialect options, or <c>null</c> for the defaults.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task that completes when the CSV has been written.</returns>
    public static Task WriteCsvAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params object?[] @params)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ToParentCommand().WriteCsvAsync(destination, options, cancellationToken, @params);
    }

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
    public static IAsyncEnumerable<TEntity> ToAsyncEnumerable<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params) => builder.ToParentCommand().ToAsyncEnumerable(cancellationToken, @params);
    /// <summary>
    /// Executes the query and returns the matching entities as a synchronous sequence.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A sequence over the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IEnumerable<TEntity> ToEnumerable<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => builder.ToParentCommand().ToEnumerable(@params);

    /// <summary>
    /// Writes the query's projected rows directly to <paramref name="destination"/> as a JSON array,
    /// without materializing a <typeparamref name="TEntity"/> per row. The destination is owned by the
    /// caller and is never closed. Supported on database providers only.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="destination">The caller-owned output stream; it is never closed.</param>
    /// <exception cref="NotSupportedException">The query runs on the in-memory provider.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteJson<TEntity>(this EntityBuilder<TEntity> builder, Stream destination)
        => builder.ToParentCommand().WriteJson(destination);

    /// <summary>
    /// Writes the query's projected rows directly to <paramref name="destination"/> as JSON using
    /// <paramref name="options"/>, without materializing a <typeparamref name="TEntity"/> per row. The
    /// destination is owned by the caller and is never closed. Supported on database providers only.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="destination">The caller-owned output stream; it is never closed.</param>
    /// <param name="options">The JSON container and shaping options.</param>
    /// <exception cref="NotSupportedException">The query runs on the in-memory provider, the projection shape is not supported, or the option combination is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteJson<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, JsonStreamOptions options)
        => builder.ToParentCommand().WriteJson(destination, options);

    /// <summary>
    /// Asynchronously writes the query's projected rows directly to <paramref name="destination"/> as a
    /// JSON array, without materializing a <typeparamref name="TEntity"/> per row. The destination is
    /// owned by the caller and is never closed. Supported on database providers only.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="destination">The caller-owned output stream; it is never closed.</param>
    /// <param name="cancellationToken">A token observed while reading rows and writing to the stream.</param>
    /// <returns>A task that completes when the whole document has been written.</returns>
    /// <exception cref="NotSupportedException">The query runs on the in-memory provider.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WriteJsonAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, CancellationToken cancellationToken = default)
        => builder.ToParentCommand().WriteJsonAsync(destination, cancellationToken);

    /// <summary>
    /// Asynchronously writes the query's projected rows directly to <paramref name="destination"/> as
    /// JSON using <paramref name="options"/>, without materializing a <typeparamref name="TEntity"/> per
    /// row. The destination is owned by the caller and is never closed. Supported on database providers only.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="destination">The caller-owned output stream; it is never closed.</param>
    /// <param name="options">The JSON container and shaping options.</param>
    /// <param name="cancellationToken">A token observed while reading rows and writing to the stream.</param>
    /// <returns>A task that completes when the whole document has been written.</returns>
    /// <exception cref="NotSupportedException">The query runs on the in-memory provider, the projection shape is not supported, or the option combination is invalid.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WriteJsonAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream destination, JsonStreamOptions options, CancellationToken cancellationToken = default)
        => builder.ToParentCommand().WriteJsonAsync(destination, options, cancellationToken);

    /// <summary>
    /// Writes the query's projected rows directly to <paramref name="stream"/> as JSON using
    /// <paramref name="options"/> and binding the positional SQL parameter values in
    /// <paramref name="params"/>, without materializing a <typeparamref name="TEntity"/> per row. The
    /// destination is owned by the caller and is never closed. Supported on database providers only.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="stream">The caller-owned output stream; it is never closed.</param>
    /// <param name="options">The JSON container and shaping options.</param>
    /// <param name="cancellationToken">A token observed while preparing, reading rows and writing to the stream.</param>
    /// <param name="params">
    /// The positional SQL parameter values, in the order their placeholders appear. An empty set binds
    /// nothing; a <see langword="null"/> element binds <see cref="System.DBNull"/>.
    /// </param>
    /// <exception cref="NotSupportedException">The query runs on the in-memory provider, the projection shape is not supported, or the option combination is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="stream"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteJson<TEntity>(this EntityBuilder<TEntity> builder, Stream stream, JsonStreamOptions options, CancellationToken cancellationToken, params ReadOnlySpan<object?> @params)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToParentCommand().WriteJson(stream, options, cancellationToken, @params);
    }

    /// <summary>
    /// Asynchronously writes the query's projected rows directly to <paramref name="stream"/> as JSON
    /// using <paramref name="options"/> and binding the positional SQL parameter values in
    /// <paramref name="params"/>, without materializing a <typeparamref name="TEntity"/> per row. The
    /// destination is owned by the caller and is never closed. Supported on database providers only.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="stream">The caller-owned output stream; it is never closed.</param>
    /// <param name="options">The JSON container and shaping options.</param>
    /// <param name="cancellationToken">A token observed while preparing, reading rows and writing to the stream.</param>
    /// <param name="params">
    /// The positional SQL parameter values, in the order their placeholders appear. An empty set binds
    /// nothing; a <see langword="null"/> element binds <see cref="System.DBNull"/>.
    /// </param>
    /// <returns>A task that completes when the whole document has been written.</returns>
    /// <exception cref="NotSupportedException">The query runs on the in-memory provider, the projection shape is not supported, or the option combination is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="stream"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WriteJsonAsync<TEntity>(this EntityBuilder<TEntity> builder, Stream stream, JsonStreamOptions options, CancellationToken cancellationToken, params object?[] @params)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.ToParentCommand().WriteJsonAsync(stream, options, cancellationToken, @params);
    }

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
        var cmd = builder.ToParentCommand();
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
        var cmd = builder.ToParentCommand();
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
        var cmd = builder.ToParentCommand();
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
        var cmd = builder.ToParentCommand();
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
        var cmd = builder.SelectParent(exp);
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
        var cmd = builder.SelectParent(exp);
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
        var cmd = builder.ToParentCommand();
        cmd.Paging.Limit = 2;
        return cmd;
    }

    /// <summary>
    /// Executes the query and materializes the matching entities into a list. When the builder carries a
    /// <c>JoinInto</c> declaration or a single-query (<c>AsSingleQuery</c>) eager load, one denormalized
    /// command is executed and its rows are stitched; otherwise the parent query is executed and any
    /// split <c>LoadWith</c> children are loaded afterwards.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A list containing the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TEntity> ToList<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
        => MaterializeList(builder, @params);
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
        => MaterializeListAsync(builder, cancellationToken, @params);
    /// <summary>
    /// Executes the query and materializes the matching entities into an array, applying the same
    /// stitching as <see cref="ToList{TEntity}(EntityBuilder{TEntity}, ReadOnlySpan{object?})"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>An array containing the matching entities.</returns>
    public static TEntity[] ToArray<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params)
    {
        if (!UsesStitching(builder))
            return builder.ToParentCommand().ToArray(@params);

        return [.. MaterializeList(builder, @params)];
    }
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into an array, applying the
    /// same stitching as <see cref="ToListAsync{TEntity}(EntityBuilder{TEntity}, CancellationToken, object[])"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is an array containing the matching entities.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TEntity[]> ToArrayAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => ToArrayAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Executes the query asynchronously and materializes the matching entities into an array, applying the
    /// same stitching as <see cref="ToListAsync{TEntity}(EntityBuilder{TEntity}, CancellationToken, object[])"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is an array containing the matching entities.</returns>
    public static async Task<TEntity[]> ToArrayAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
    {
        if (!UsesStitching(builder))
            return await builder.ToParentCommand().ToArrayAsync(cancellationToken, @params).ConfigureAwait(false);

        return [.. await MaterializeListAsync(builder, cancellationToken, @params).ConfigureAwait(false)];
    }

    /// <summary>
    /// Whether the builder materializes through a stitched result: a <c>JoinInto</c> declaration, a
    /// single-query (<c>AsSingleQuery</c>) eager-load set, or a split <c>LoadWith</c> set. The
    /// non-stitching terminals bypass this and evaluate the parent only.
    /// </summary>
    private static bool UsesStitching<TEntity>(EntityBuilder<TEntity> builder)
        => builder.JoinIntos is { Count: > 0 } || builder.LoadSpecs is { Count: > 0 };

    /// <summary>Whether the builder must run its <c>LoadWith</c> collections through the single-query path.</summary>
    private static bool UsesSingleQueryLoading<TEntity>(EntityBuilder<TEntity> builder)
        => builder.SingleQuery && builder.LoadSpecs is { Count: > 0 };

    /// <summary>
    /// Materializes the builder for the list terminals: one stitched command for <c>JoinInto</c> /
    /// single-query <c>LoadWith</c>, otherwise the parent query followed by the split child loads.
    /// </summary>
    private static List<TEntity> MaterializeList<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params)
    {
        if (UsesSingleQueryLoading(builder))
        {
            var (specs, joins) = builder.BuildSingleQueryJoins();
            var rows = builder.CreatePairCommand(specs, joins).ToObjectList(@params);
            return JoinIntoStitcher.Stitch(builder, specs, rows, requireMappedParentKey: true);
        }

        if (builder.JoinIntos is { Count: > 0 })
        {
            var joined = JoinIntoStitcher.Execute(builder, @params);
            EntityBuilderEagerLoading.Execute(builder, joined);
            return joined;
        }

        var list = builder.ToParentCommand().ToList(@params);
        EntityBuilderEagerLoading.Execute(builder, list);
        return list;
    }

    /// <summary>Asynchronous counterpart of <see cref="MaterializeList{TEntity}"/>.</summary>
    private static async Task<List<TEntity>> MaterializeListAsync<TEntity>(EntityBuilder<TEntity> builder, CancellationToken cancellationToken, object[] @params)
    {
        if (UsesSingleQueryLoading(builder))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (specs, joins) = builder.BuildSingleQueryJoins();
            var rows = await builder.CreatePairCommand(specs, joins).ToObjectListAsync(@params, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return JoinIntoStitcher.Stitch(builder, specs, rows, requireMappedParentKey: true);
        }

        if (builder.JoinIntos is { Count: > 0 })
        {
            var joined = await JoinIntoStitcher.ExecuteAsync(builder, @params, cancellationToken).ConfigureAwait(false);
            await EntityBuilderEagerLoading.ExecuteAsync(builder, joined, cancellationToken).ConfigureAwait(false);
            return joined;
        }

        var list = await builder.ToParentCommand().ToListAsync(cancellationToken, @params).ConfigureAwait(false);
        await EntityBuilderEagerLoading.ExecuteAsync(builder, list, cancellationToken).ConfigureAwait(false);
        return list;
    }
    /// <summary>
    /// Executes the query and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A hash set containing the distinct matching entities.</returns>
    public static HashSet<TEntity> ToHashSet<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => builder.ToParentCommand().ToHashSet(@params);
    /// <summary>
    /// Executes the query and materializes the distinct matching entities into a hash set.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="comparer">The equality comparer to use, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A hash set containing the distinct matching entities.</returns>
    public static HashSet<TEntity> ToHashSet<TEntity>(this EntityBuilder<TEntity> builder, IEqualityComparer<TEntity>? comparer, params ReadOnlySpan<object?> @params) => builder.ToParentCommand().ToHashSet(comparer, @params);
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
        => builder.ToParentCommand().ToHashSetAsync(cancellationToken, @params);
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
        => builder.ToParentCommand().ToHashSetAsync(comparer, cancellationToken, @params);
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
        => builder.ToParentCommand().ToDictionary(keySelector, @params);
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
        => builder.ToParentCommand().ToDictionary(keySelector, comparer, @params);
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
        => builder.ToParentCommand().ToDictionaryAsync(keySelector, cancellationToken, @params);
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
        => builder.ToParentCommand().ToDictionaryAsync(keySelector, comparer, cancellationToken, @params);

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
        => builder.ToParentCommand().First(@params);
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
        => builder.ToParentCommand().FirstAsync(cancellationToken, @params);
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
        => builder.ToParentCommand().FirstOrDefault(@params);
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
        => builder.ToParentCommand().FirstOrDefaultAsync(cancellationToken, @params);

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
        => builder.ToParentCommand().Single(@params);
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
        => builder.ToParentCommand().SingleAsync(cancellationToken, @params);
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
        => builder.ToParentCommand().SingleOrDefault(@params);
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
        => builder.ToParentCommand().SingleOrDefaultAsync(cancellationToken, @params);

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
        => builder.ToParentCommand().Last(@params);
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
        => builder.ToParentCommand().LastAsync(cancellationToken, @params);
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
        => builder.ToParentCommand().LastOrDefault(@params);
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
        => builder.ToParentCommand().LastOrDefaultAsync(cancellationToken, @params);

    /// <summary>
    /// Creates a command with its SQL already compiled so it can be executed repeatedly, typically with different parameters.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="nonStreamUsing">Whether the prepared command favors buffered (non-streaming) execution.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The prepared query command.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IPreparedQueryCommand<TEntity> Prepare<TEntity>(this EntityBuilder<TEntity> builder, bool nonStreamUsing = true, CancellationToken cancellationToken = default) => builder.ToParentCommand().Prepare(nonStreamUsing, cancellationToken);

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
        var cmd = builder.SelectParent(e => SqlFunctions.Sql.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }

    /// <summary>
    /// Counts the matching rows as a 64-bit value by emitting a SQL <c>COUNT_BIG(*)</c> (<c>COUNT(*)</c>
    /// surfaced as <see cref="long"/> on providers without a separate 64-bit count function).
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <returns>The number of matching rows as a 64-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long LongCount<TEntity>(this EntityBuilder<TEntity> builder) => LongCountCore(builder, ReadOnlySpan<object?>.Empty);
    /// <summary>
    /// Counts the matching rows as a 64-bit value by emitting a SQL <c>COUNT_BIG(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>The number of matching rows as a 64-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long LongCount<TEntity>(this EntityBuilder<TEntity> builder, params ReadOnlySpan<object?> @params) => LongCountCore(builder, @params);
    /// <summary>
    /// Counts the matching rows as a 64-bit value asynchronously by emitting a SQL <c>COUNT_BIG(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the number of matching rows as a 64-bit integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<long> LongCountAsync<TEntity>(this EntityBuilder<TEntity> builder, params object[] @params) => LongCountAsync(builder, CancellationToken.None, @params);
    /// <summary>
    /// Counts the matching rows as a 64-bit value asynchronously by emitting a SQL <c>COUNT_BIG(*)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being queried.</typeparam>
    /// <param name="builder">The query builder being extended.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <param name="params">The query parameters, in the order their placeholders appear.</param>
    /// <returns>A task whose result is the number of matching rows as a 64-bit integer.</returns>
    public static Task<long> LongCountAsync<TEntity>(this EntityBuilder<TEntity> builder, CancellationToken cancellationToken, params object[] @params)
    {
        var cmd = builder.SelectParent(e => SqlFunctions.Sql.count_big());
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

    /// <summary>
    /// Adapts a declared collection navigation to a full <see cref="EntityBuilder{T}"/> so it can be
    /// composed like any other query, for example
    /// <c>e.Children.AsEntityBuilder().Where(c =&gt; c.IsActive).Any()</c>.
    /// </summary>
    /// <remarks>
    /// This is an expression-only marker: it must appear inside a query expression tree, where the
    /// translator resolves the receiver to the declared navigation path rooted in the surrounding
    /// query. Calling it as an ordinary method (outside an expression tree) always throws
    /// <see cref="NotSupportedException"/>; it never returns a default or a null marker. Captured
    /// receivers and undeclared navigations are not supported.
    /// </remarks>
    /// <typeparam name="T">The related entity type; must be a reference type.</typeparam>
    /// <param name="source">The declared collection navigation to adapt.</param>
    /// <returns>Never returns; the call is only valid inside a query expression.</returns>
    /// <exception cref="NotSupportedException">Always, when called outside a query expression tree.</exception>
    public static EntityBuilder<T> AsEntityBuilder<T>(this IEnumerable<T> source) where T : class
        => throw new NotSupportedException(
            "AsEntityBuilder can only be used inside a query expression; a navigation collection or reference cannot be read outside the query.");

    /// <summary>
    /// Adapts a declared reference navigation to a full <see cref="EntityBuilder{T}"/> so it can be
    /// composed like any other query, for example
    /// <c>e.Parent!.AsEntityBuilder&lt;Parent&gt;().Where(p =&gt; p.IsActive).Any()</c>.
    /// </summary>
    /// <remarks>
    /// This is the reference-navigation counterpart of the collection overload. The generic argument
    /// must be supplied explicitly because the receiver is typed as <see cref="object"/>; the
    /// collection overload wins when the receiver is a collection, so this form never steals
    /// collection calls. Like the collection form it is an expression-only marker and always throws
    /// <see cref="NotSupportedException"/> outside a query expression tree.
    /// </remarks>
    /// <typeparam name="T">The related entity type; must be a reference type.</typeparam>
    /// <param name="navigation">The declared reference navigation to adapt.</param>
    /// <returns>Never returns; the call is only valid inside a query expression.</returns>
    /// <exception cref="NotSupportedException">Always, when called outside a query expression tree.</exception>
    public static EntityBuilder<T> AsEntityBuilder<T>(this object? navigation) where T : class
        => throw new NotSupportedException(
            "AsEntityBuilder can only be used inside a query expression; a navigation collection or reference cannot be read outside the query.");

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

    private static long LongCountCore<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params)
    {
        var cmd = builder.SelectParent(e => SqlFunctions.Sql.count_big());
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }

    private static int CountCore<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params)
    {
        var cmd = builder.SelectParent(e => SqlFunctions.Sql.count());
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }

    private static TResult? AggregateCore<TEntity, TResult>(EntityBuilder<TEntity> builder, MethodInfo sqlMethod, Expression<Func<TEntity, TResult>> exp, ReadOnlySpan<object?> @params)
    {
        var cmd = builder.SelectParent(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(CommonFunctions.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalar(@params);
    }

    private static Task<TResult?> AggregateAsyncCore<TEntity, TResult>(EntityBuilder<TEntity> builder, MethodInfo sqlMethod, Expression<Func<TEntity, TResult>> exp, CancellationToken cancellationToken, object[] @params)
    {
        var cmd = builder.SelectParent(Expression.Lambda<Func<TEntity, TResult>>(Expression.Call(CommonFunctions.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body), exp.Parameters));
        cmd.SingleRow = true;
        return cmd.ExecuteScalarAsync(cancellationToken, @params);
    }
}
