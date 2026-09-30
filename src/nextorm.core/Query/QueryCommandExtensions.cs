using System.Data;
using System.Data.Common;
using System.Reflection;
namespace NextORM.Core;

/// <summary>
/// Extension methods that run a <see cref="QueryCommand{TResult}"/> against raw SQL instead of the
/// generated query, and the terminal operators that stream LOB columns through
/// <see cref="System.Data.CommandBehavior.SequentialAccess"/>: the single-column
/// <see cref="ToStream(QueryCommand{byte[]}, ReadOnlySpan{object?})"/> /
/// <see cref="ToTextReader(QueryCommand{string}, ReadOnlySpan{object?})"/> and the multi-column
/// <see cref="ToDataReader{TResult}(QueryCommand{TResult}, ReadOnlySpan{object?})"/>, with their
/// asynchronous counterparts.
/// </summary>
public static class QueryCommandExtensions
{
    /// <summary>Prepares the command to read the given SQL, materializing the result as <typeparamref name="TResult"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, null, PrepareFromSqlMode.None, cancellationToken);
    /// <summary>Prepares the command to read the given SQL with parameters supplied as the public properties of <paramref name="params"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="params">An object whose public properties become the SQL parameters, or <c>null</c>.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, @params, PrepareFromSqlMode.None, cancellationToken);
    /// <summary>Prepares the command to read the given SQL using the given caching/streaming <paramref name="mode"/>.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="mode">How the prepared command is cached and whether it streams.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, PrepareFromSqlMode mode, CancellationToken cancellationToken = default)
        => queryCommand.PrepareFromSql(sql, null, mode, cancellationToken);
    /// <summary>Prepares the command to read the given SQL with parameters and an explicit caching/streaming mode.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to run.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="params">An object whose public properties become the SQL parameters, or <c>null</c>.</param>
    /// <param name="mode">How the prepared command is cached and whether it streams.</param>
    /// <param name="cancellationToken">A token that cancels preparation.</param>
    /// <returns>The prepared command.</returns>
    public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, PrepareFromSqlMode mode, CancellationToken cancellationToken = default)
        => queryCommand.DataContext!.GetPreparedQueryCommand(WithSql(queryCommand, sql, @params), !mode.HasFlag(PrepareFromSqlMode.Streaming), mode.HasFlag(PrepareFromSqlMode.StoreInCache), cancellationToken);
    /// <summary>Overrides the command's generated SQL with <paramref name="sql"/> and no parameters.</summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to override.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <returns>The same command, with the raw-SQL override attached.</returns>
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql) => WithSql(queryCommand, sql, null);
    /// <summary>
    /// Overrides the command's generated SQL with <paramref name="sql"/>, taking the SQL parameters from
    /// the public properties of <paramref name="params"/>.
    /// </summary>
    /// <typeparam name="TResult">The projected result type.</typeparam>
    /// <param name="queryCommand">The command to override.</param>
    /// <param name="sql">The SQL text to execute.</param>
    /// <param name="params">An object whose public properties become the SQL parameters, or <c>null</c>.</param>
    /// <returns>The same command, with the raw-SQL override attached.</returns>
    public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params)
    {
        queryCommand.CustomData = new RawSqlOverride
        {
            ManualSql = sql,
            MakeParams = () =>
            {
                List<Parameter> ps = [];
                if (@params is not null)
                {
                    var t = @params.GetType();
                    var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public);
                    for (var (i, cnt) = (0, props.Length); i < cnt; i++)
                    {
                        var prop = props[i];
                        ps.Add(new Parameter(prop.Name, prop.GetValue(@params)));
                    }
                }
                return ps;
            }
        };
        return queryCommand;
    }

    /// <summary>Opens a streaming read of a single <c>byte[]</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>byte[]</c> column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A stream that owns the reader until it is disposed.</returns>
    public static Stream ToStream(this QueryCommand<byte[]> command, params ReadOnlySpan<object?> parameters)
        => ToStream(command, CancellationToken.None, parameters);

    /// <summary>Opens a streaming read of a single <c>byte[]</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>byte[]</c> column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A stream that owns the reader until it is disposed.</returns>
    /// <remarks>On the in-memory provider the value is already materialized: the returned read-only <see cref="MemoryStream"/> wraps it, and a missing row or a <c>NULL</c> value yields <see cref="Stream.Null"/>.</remarks>
    public static Stream ToStream(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.DataContext is InMemoryDataContext)
        {
            ThrowIfDisposed(command.DataContext);
            return command.ExecuteScalar(parameters) is { } value ? new MemoryStream(value, writable: false) : Stream.Null;
        }

        var context = RequireRelationalContext(command);
        return CreateStream(context.OpenLobReader(command, parameters, cancellationToken), context, command);
    }

    /// <summary>Asynchronously opens a streaming read of a single <c>byte[]</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>byte[]</c> column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a stream that owns the reader until it is disposed.</returns>
    public static Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, params object?[] parameters)
        => ToStreamAsync(command, CancellationToken.None, parameters);

    /// <summary>Asynchronously opens a streaming read of a single <c>byte[]</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>byte[]</c> column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a stream that owns the reader until it is disposed.</returns>
    /// <remarks>On the in-memory provider the value is already materialized: the returned read-only <see cref="MemoryStream"/> wraps it, and a missing row or a <c>NULL</c> value yields <see cref="Stream.Null"/>.</remarks>
    public static async Task<Stream> ToStreamAsync(this QueryCommand<byte[]> command, CancellationToken cancellationToken, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.DataContext is InMemoryDataContext)
        {
            ThrowIfDisposed(command.DataContext);
            var value = await command.ExecuteScalarAsync(cancellationToken, (object[])parameters).ConfigureAwait(false);
            return value is null ? Stream.Null : new MemoryStream(value, writable: false);
        }

        var context = RequireRelationalContext(command);
        var owner = await context.OpenLobReaderAsync(command, (object[]?)parameters, cancellationToken).ConfigureAwait(false);
        return await CreateStreamAsync(owner, context, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a streaming read of a single <c>string</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>string</c> column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A text reader that owns the reader until it is disposed.</returns>
    public static TextReader ToTextReader(this QueryCommand<string> command, params ReadOnlySpan<object?> parameters)
        => ToTextReader(command, CancellationToken.None, parameters);

    /// <summary>Opens a streaming read of a single <c>string</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>string</c> column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A text reader that owns the reader until it is disposed.</returns>
    /// <remarks>On the in-memory provider the value is already materialized: the returned <see cref="StringReader"/> wraps it, and a missing row or a <c>NULL</c> value yields <see cref="TextReader.Null"/>.</remarks>
    public static TextReader ToTextReader(this QueryCommand<string> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.DataContext is InMemoryDataContext)
        {
            ThrowIfDisposed(command.DataContext);
            return command.ExecuteScalar(parameters) is { } value ? new StringReader(value) : TextReader.Null;
        }

        var context = RequireRelationalContext(command);
        return CreateTextReader(context.OpenLobReader(command, parameters, cancellationToken), context, command);
    }

    /// <summary>Asynchronously opens a streaming read of a single <c>string</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>string</c> column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a text reader that owns the reader until it is disposed.</returns>
    public static Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, params object?[] parameters)
        => ToTextReaderAsync(command, CancellationToken.None, parameters);

    /// <summary>Asynchronously opens a streaming read of a single <c>string</c> column.</summary>
    /// <param name="command">The command whose projection is exactly one <c>string</c> column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a text reader that owns the reader until it is disposed.</returns>
    /// <remarks>On the in-memory provider the value is already materialized: the returned <see cref="StringReader"/> wraps it, and a missing row or a <c>NULL</c> value yields <see cref="TextReader.Null"/>.</remarks>
    public static async Task<TextReader> ToTextReaderAsync(this QueryCommand<string> command, CancellationToken cancellationToken, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        if (command.DataContext is InMemoryDataContext)
        {
            ThrowIfDisposed(command.DataContext);
            var value = await command.ExecuteScalarAsync(cancellationToken, (object[])parameters).ConfigureAwait(false);
            return value is null ? TextReader.Null : new StringReader(value);
        }

        var context = RequireRelationalContext(command);
        var owner = await context.OpenLobReaderAsync(command, (object[]?)parameters, cancellationToken).ConfigureAwait(false);
        return await CreateTextReaderAsync(owner, context, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a streaming read of a binary column selected by name as a <see cref="Stream"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming binary column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A stream that owns the reader and the per-call command until it is disposed.</returns>
    public static Stream ToStream(this QueryCommand<Stream> command, params ReadOnlySpan<object?> parameters)
        => ToStream(command, CancellationToken.None, parameters);

    /// <summary>Opens a streaming read of a binary column selected by name as a <see cref="Stream"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming binary column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A stream that owns the reader and the per-call command until it is disposed.</returns>
    public static Stream ToStream(this QueryCommand<Stream> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContext(command);
        return CreateStream(context.OpenLobReader(command, parameters, cancellationToken), context, command);
    }

    /// <summary>Asynchronously opens a streaming read of a binary column selected by name as a <see cref="Stream"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming binary column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a stream that owns the reader and the per-call command until it is disposed.</returns>
    public static Task<Stream> ToStreamAsync(this QueryCommand<Stream> command, params object?[] parameters)
        => ToStreamAsync(command, CancellationToken.None, parameters);

    /// <summary>Asynchronously opens a streaming read of a binary column selected by name as a <see cref="Stream"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming binary column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a stream that owns the reader and the per-call command until it is disposed.</returns>
    public static async Task<Stream> ToStreamAsync(this QueryCommand<Stream> command, CancellationToken cancellationToken, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContext(command);
        var owner = await context.OpenLobReaderAsync(command, (object[]?)parameters, cancellationToken).ConfigureAwait(false);
        return await CreateStreamAsync(owner, context, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a streaming read of a text column selected by name as a <see cref="TextReader"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming text column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A text reader that owns the reader and the per-call command until it is disposed.</returns>
    public static TextReader ToTextReader(this QueryCommand<TextReader> command, params ReadOnlySpan<object?> parameters)
        => ToTextReader(command, CancellationToken.None, parameters);

    /// <summary>Opens a streaming read of a text column selected by name as a <see cref="TextReader"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming text column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A text reader that owns the reader and the per-call command until it is disposed.</returns>
    public static TextReader ToTextReader(this QueryCommand<TextReader> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContext(command);
        return CreateTextReader(context.OpenLobReader(command, parameters, cancellationToken), context, command);
    }

    /// <summary>Asynchronously opens a streaming read of a text column selected by name as a <see cref="TextReader"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming text column.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a text reader that owns the reader and the per-call command until it is disposed.</returns>
    public static Task<TextReader> ToTextReaderAsync(this QueryCommand<TextReader> command, params object?[] parameters)
        => ToTextReaderAsync(command, CancellationToken.None, parameters);

    /// <summary>Asynchronously opens a streaming read of a text column selected by name as a <see cref="TextReader"/>.</summary>
    /// <param name="command">The command whose projection is a single streaming text column.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a text reader that owns the reader and the per-call command until it is disposed.</returns>
    public static async Task<TextReader> ToTextReaderAsync(this QueryCommand<TextReader> command, CancellationToken cancellationToken, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContext(command);
        var owner = await context.OpenLobReaderAsync(command, (object[]?)parameters, cancellationToken).ConfigureAwait(false);
        return await CreateTextReaderAsync(owner, context, command, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a forward-only <see cref="DbDataReader"/> over the command's multi-column projection.</summary>
    /// <typeparam name="TResult">The projected result type; it is not materialized on this path.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A reader that owns the provider reader and the per-call command until it is disposed.</returns>
    /// <remarks>
    /// The caller owns the returned reader and must dispose it, which releases the provider reader and the
    /// per-call command; the context and its connection stay open. The reader is forward-only and uses
    /// sequential access: read columns in ascending ordinal order and do not read a column twice. Not
    /// supported on SQLite (its <c>rowid</c> locator would be appended to the projection) and on providers
    /// without sequential access, such as MySQL/MariaDB, ClickHouse and the in-memory provider.
    /// </remarks>
    public static DbDataReader ToDataReader<TResult>(this QueryCommand<TResult> command, params ReadOnlySpan<object?> parameters)
        => ToDataReader(command, CancellationToken.None, parameters);

    /// <summary>Opens a forward-only <see cref="DbDataReader"/> over the command's multi-column projection.</summary>
    /// <typeparam name="TResult">The projected result type; it is not materialized on this path.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A reader that owns the provider reader and the per-call command until it is disposed.</returns>
    /// <remarks>
    /// The caller owns the returned reader and must dispose it, which releases the provider reader and the
    /// per-call command; the context and its connection stay open. The reader is forward-only and uses
    /// sequential access: read columns in ascending ordinal order and do not read a column twice. Not
    /// supported on SQLite (its <c>rowid</c> locator would be appended to the projection) and on providers
    /// without sequential access, such as MySQL/MariaDB, ClickHouse and the in-memory provider.
    /// </remarks>
    public static DbDataReader ToDataReader<TResult>(this QueryCommand<TResult> command, CancellationToken cancellationToken, params ReadOnlySpan<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContext(command);
        EnsureDataReaderSupported(context);

        var owner = context.OpenLobReader(command, parameters, cancellationToken);
        try
        {
            return new LobDataReader(owner, cancellationToken);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    /// <summary>Asynchronously opens a forward-only <see cref="DbDataReader"/> over the command's multi-column projection.</summary>
    /// <typeparam name="TResult">The projected result type; it is not materialized on this path.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a reader that owns the provider reader and the per-call command until it is disposed.</returns>
    /// <remarks>
    /// The caller owns the returned reader and must dispose it, which releases the provider reader and the
    /// per-call command; the context and its connection stay open. The reader is forward-only and uses
    /// sequential access: read columns in ascending ordinal order and do not read a column twice. Not
    /// supported on SQLite (its <c>rowid</c> locator would be appended to the projection) and on providers
    /// without sequential access, such as MySQL/MariaDB, ClickHouse and the in-memory provider.
    /// </remarks>
    public static Task<DbDataReader> ToDataReaderAsync<TResult>(this QueryCommand<TResult> command, params object?[] parameters)
        => ToDataReaderAsync(command, CancellationToken.None, parameters);

    /// <summary>Asynchronously opens a forward-only <see cref="DbDataReader"/> over the command's multi-column projection.</summary>
    /// <typeparam name="TResult">The projected result type; it is not materialized on this path.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="cancellationToken">A token that cancels opening the reader.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task producing a reader that owns the provider reader and the per-call command until it is disposed.</returns>
    /// <remarks>
    /// The caller owns the returned reader and must dispose it, which releases the provider reader and the
    /// per-call command; the context and its connection stay open. The reader is forward-only and uses
    /// sequential access: read columns in ascending ordinal order and do not read a column twice. Not
    /// supported on SQLite (its <c>rowid</c> locator would be appended to the projection) and on providers
    /// without sequential access, such as MySQL/MariaDB, ClickHouse and the in-memory provider.
    /// </remarks>
    public static async Task<DbDataReader> ToDataReaderAsync<TResult>(this QueryCommand<TResult> command, CancellationToken cancellationToken, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContext(command);
        EnsureDataReaderSupported(context);

        var owner = await context.OpenLobReaderAsync(command, (object[]?)parameters, cancellationToken).ConfigureAwait(false);
        try
        {
            return new LobDataReader(owner, cancellationToken);
        }
        catch
        {
            await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Writes the command's <c>Select</c> result as CSV to <paramref name="destination"/>.</summary>
    /// <typeparam name="TResult">The projected result type; it is not materialized on this path.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="destination">The stream that receives the UTF-8 CSV; it stays open.</param>
    /// <param name="options">The CSV dialect options, or <c>null</c> for the defaults.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    public static void WriteCsv<TResult>(this QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params ReadOnlySpan<object?> parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        var context = RequireRelationalContextForCsv(command);
        var dialectOptions = options ?? new CsvStreamOptions();
        CsvStreamWriter.ValidateOptions(dialectOptions);

        command.PrepareCommand(cancellationToken);

        // Reject unsupported projections from static information before any SQL is executed.
        CsvStreamWriter.ValidateProjection(command.SelectList, context.MapColumnExpression, context.SupportsTypedColumn);

        var owner = context.OpenResultReader(command, parameters, cancellationToken);
        try
        {
            // Binding happens after the reader is open so the provider can pick a typed getter from the
            // storage type; the plan (and every guard) is still built before the header is written.
            var plan = CsvStreamWriter.Build(command.SelectList, owner.Reader, context.MapTypedColumn);
            CsvStreamWriter.Write(owner.Reader, destination, plan, dialectOptions, cancellationToken);
        }
        finally
        {
            owner.Dispose();
        }
    }

    /// <summary>Asynchronously writes the command's <c>Select</c> result as CSV to <paramref name="destination"/>.</summary>
    /// <typeparam name="TResult">The projected result type; it is not materialized on this path.</typeparam>
    /// <param name="command">The command to execute.</param>
    /// <param name="destination">The stream that receives the UTF-8 CSV; it stays open.</param>
    /// <param name="options">The CSV dialect options, or <c>null</c> for the defaults.</param>
    /// <param name="cancellationToken">A token that cancels the write.</param>
    /// <param name="parameters">The positional parameter values bound to the query.</param>
    /// <returns>A task that completes when the CSV has been written.</returns>
    public static Task WriteCsvAsync<TResult>(this QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options = null, CancellationToken cancellationToken = default, params object?[] parameters)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(destination);
        cancellationToken.ThrowIfCancellationRequested();

        return WriteCsvAsyncCore(command, destination, options, cancellationToken, parameters);
    }

    private static async Task WriteCsvAsyncCore<TResult>(QueryCommand<TResult> command, Stream destination, CsvStreamOptions? options, CancellationToken cancellationToken, object?[] parameters)
    {
        var context = RequireRelationalContextForCsv(command);
        var dialectOptions = options ?? new CsvStreamOptions();
        CsvStreamWriter.ValidateOptions(dialectOptions);

        command.PrepareCommand(cancellationToken);

        // Reject unsupported projections from static information before any SQL is executed.
        CsvStreamWriter.ValidateProjection(command.SelectList, context.MapColumnExpression, context.SupportsTypedColumn);

        var owner = await context.OpenResultReaderAsync(command, (object[]?)parameters, cancellationToken).ConfigureAwait(false);
        try
        {
            // Binding happens after the reader is open so the provider can pick a typed getter from the
            // storage type; the plan (and every guard) is still built before the header is written.
            var plan = CsvStreamWriter.Build(command.SelectList, owner.Reader, context.MapTypedColumn);
            await CsvStreamWriter.WriteAsync(owner.Reader, destination, plan, dialectOptions, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await owner.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal const string LobDataReaderLocatorMessage =
        "ToDataReader is not supported on providers that append a LOB locator column (SQLite); use ToStream/ToTextReader for a single LOB column.";

    private const string LobProjectionMessageLeavingLocatorHint =
        "ToStream/ToTextReader require a projection with exactly one byte[] or string column; read several columns with ToDataReader instead.";

    private const string LobProjectionMessageLocatorDialect =
        "ToStream/ToTextReader require a projection with exactly one byte[] or string column.";

    // The general reader needs the same sequential-access capability as the single-column terminals, but
    // it cannot expose a dialect locator column, so it is narrower than they are: SQLite (which appends
    // rowid) and the providers without sequential access both fail closed before opening the reader.
    private static void EnsureDataReaderSupported(DataContext context)
    {
        if (!context.Dialect.SupportsSequentialAccess)
            throw new NotSupportedException(
                $"ToDataReader is not supported by the {context.Dialect.GetType().Name} provider; it requires sequential-access support (PostgreSQL or SQL Server).");

        if (context.Dialect.LobLocatorColumn is not null)
            throw new NotSupportedException(LobDataReaderLocatorMessage);
    }

    private static string LobProjectionMessage(DataContext context)
        => context.Dialect.LobLocatorColumn is null
            ? LobProjectionMessageLeavingLocatorHint
            : LobProjectionMessageLocatorDialect;

    private const string RawSqlLocatorMessage =
        "SQLite LOB streaming with raw SQL (WithSql) is not supported because a rowid locator cannot be added safely.";

    // A LOB projection is one user column plus an optional trailing dialect locator (SQLite's rowid)
    // that makes the driver stream the payload. Two user columns exceed this count and are still
    // rejected, because the locator is appended only by the dialect on the streaming path. A raw SQL
    // override (WithSql/PrepareFromSql) bypasses the SQL builder entirely, so no locator is appended
    // for it: on a locator dialect there is no safe way to stream an arbitrary single-column raw
    // projection, so that shape is rejected outright; every other raw shape keeps the generic
    // projection-mismatch error.
    private static void EnsureLobFieldCount(CommandReaderOwner owner, DataContext context, QueryCommand command)
    {
        if (context.Dialect.LobLocatorColumn is not null && command.CustomData is RawSqlOverride)
        {
            if (owner.Reader.FieldCount == 1)
                throw new NotSupportedException(RawSqlLocatorMessage);

            throw new InvalidOperationException(LobProjectionMessage(context));
        }

        var expectedFieldCount = context.Dialect.LobLocatorColumn is null ? 1 : 2;
        if (owner.Reader.FieldCount != expectedFieldCount)
            throw new InvalidOperationException(LobProjectionMessage(context));
    }

    private static DataContext RequireRelationalContext(QueryCommand command)
    {
        ThrowIfDisposed(command.DataContext);

        return command.DataContext as DataContext
            ?? throw new NotSupportedException(
                "LOB reads require a database provider; the in-memory provider has no DbDataReader and supports only the single-column ToStream/ToTextReader terminals.");
    }

    private static DataContext RequireRelationalContextForCsv(QueryCommand command)
    {
        ThrowIfDisposed(command.DataContext);

        return command.DataContext as DataContext
            ?? throw new NotSupportedException(
                "CSV streaming requires a database provider; the in-memory provider has no DbDataReader and does not support the WriteCsv/WriteCsvAsync terminals.");
    }

    private static void ThrowIfDisposed(IDataContext? context)
    {
        var disposed = context switch
        {
            DataContext db => db.IsDisposed,
            InMemoryDataContext memory => memory.IsDisposed,
            _ => false,
        };

        ObjectDisposedException.ThrowIf(disposed, nameof(DataContext));
    }

    private static Stream CreateStream(CommandReaderOwner owner, DataContext context, QueryCommand command)
    {
        try
        {
            EnsureLobFieldCount(owner, context, command);

            if (owner.Reader.Read())
                return new LobStream(owner.Reader.GetStream(0), owner);
        }
        catch
        {
            owner.Dispose();
            throw;
        }

        owner.Dispose();
        return Stream.Null;
    }

    private static async Task<Stream> CreateStreamAsync(CommandReaderOwner owner, DataContext context, QueryCommand command, CancellationToken cancellationToken)
    {
        try
        {
            EnsureLobFieldCount(owner, context, command);

            if (await owner.Reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return new LobStream(owner.Reader.GetStream(0), owner);
        }
        catch
        {
            await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await owner.DisposeAsync().ConfigureAwait(false);
        return Stream.Null;
    }

    private static TextReader CreateTextReader(CommandReaderOwner owner, DataContext context, QueryCommand command)
    {
        try
        {
            EnsureLobFieldCount(owner, context, command);

            if (owner.Reader.Read())
                return new LobTextReader(owner.Reader.GetTextReader(0), owner);
        }
        catch
        {
            owner.Dispose();
            throw;
        }

        owner.Dispose();
        return TextReader.Null;
    }

    private static async Task<TextReader> CreateTextReaderAsync(CommandReaderOwner owner, DataContext context, QueryCommand command, CancellationToken cancellationToken)
    {
        try
        {
            EnsureLobFieldCount(owner, context, command);

            if (await owner.Reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return new LobTextReader(owner.Reader.GetTextReader(0), owner);
        }
        catch
        {
            await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await owner.DisposeAsync().ConfigureAwait(false);
        return TextReader.Null;
    }
}
