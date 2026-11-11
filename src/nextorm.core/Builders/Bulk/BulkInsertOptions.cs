namespace NextORM.Core;

/// <summary>
/// Options for a bulk insert started with <c>CreateBulkInsertBuilder</c>.
/// Immutable and safe to reuse; build one with <see cref="BulkInsertOptionsBuilder"/> or set the properties
/// directly. Providers that cannot express a requested option reject it with
/// <see cref="NotSupportedException"/> when the write runs, and invalid values are rejected as soon as the
/// options are attached to a builder.
/// </summary>
public sealed record BulkInsertOptions
{
    private bool _keepIdentity;
    private bool _keepIdentitySpecified;
    private bool _ignoreDuplicates;
    private bool _ignoreDuplicatesSpecified;

    /// <summary>
    /// The requested execution path. <see cref="BulkInsertMode.Auto"/> is the default: the provider prefers
    /// its native path when every request is compatible and otherwise uses the portable path only when that
    /// path honours every request.
    /// </summary>
    public BulkInsertMode Mode { get; init; } = BulkInsertMode.Auto;

    /// <summary>
    /// The kind of destination table. <see cref="BulkInsertTargetKind.Regular"/> is the default; a local
    /// temporary target uses the context's existing connection and session and never creates or drops the
    /// table.
    /// </summary>
    public BulkInsertTargetKind TargetKind { get; init; } = BulkInsertTargetKind.Regular;

    /// <summary>
    /// The maximum rows per statement on the portable path; the native path treats it as a batch hint
    /// (SQL Server <c>SqlBulkCopy.BatchSize</c>) or ignores it (PostgreSQL <c>COPY</c> streams). Defaults
    /// to <see langword="null"/> (one statement, or the provider default).
    /// </summary>
    public int? MaxBatchSize { get; init; }

    /// <summary>The maximum bound parameters per statement on the portable path. Defaults to <see langword="null"/>.</summary>
    public int? MaxParameters { get; init; }

    /// <summary>The approximate maximum SQL text length per statement on the portable path. Defaults to <see langword="null"/>.</summary>
    public int? MaxSqlLength { get; init; }

    /// <summary>
    /// Whether explicit values are written to identity columns. The path decision stays capability based:
    /// the native path writes the explicit values where the provider can (MySQL/MariaDB through their
    /// explicit-column load, SQL Server through <c>SqlBulkCopyOptions.KeepIdentity</c>, PostgreSQL through
    /// the explicit <c>COPY</c> column list), and the portable path renders the provider's overriding form
    /// (SQL Server wraps the insert with <c>SET IDENTITY_INSERT</c>, PostgreSQL adds
    /// <c>OVERRIDING SYSTEM VALUE</c>). Silently ignored when the entity's mapping declares no identity
    /// column (matching linq2db), so it never surfaces SQL Server error 8106 on a non-identity table and
    /// does not change the selected path there. Defaults to <see langword="false"/>.
    /// </summary>
    public bool KeepIdentity
    {
        get => _keepIdentity;
        init
        {
            _keepIdentity = value;
            _keepIdentitySpecified = true;
        }
    }

    /// <summary>
    /// Whether rows that violate a unique constraint are skipped instead of failing the whole write. Uses
    /// the provider's ignore form on whichever path can express it: the native MySQL/MariaDB load
    /// (<c>ConflictOption.Ignore</c>) or the portable statement (<c>ON CONFLICT DO NOTHING</c>,
    /// <c>INSERT OR IGNORE</c>, <c>INSERT IGNORE</c>). A provider where neither path can express it (for
    /// example SQL Server) refuses the request with <see cref="NotSupportedException"/> before any row is
    /// read. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IgnoreDuplicates
    {
        get => _ignoreDuplicates;
        init
        {
            _ignoreDuplicates = value;
            _ignoreDuplicatesSpecified = true;
        }
    }

    /// <summary>
    /// Whether CHECK and FOREIGN KEY constraints are checked by the destination while the rows are
    /// written. <see langword="true"/> is honoured everywhere: a path that always checks constraints
    /// accepts it by its default behaviour, and SQL Server maps it to
    /// <c>SqlBulkCopyOptions.CheckConstraints</c>. <see langword="false"/> asks for unchecked rows,
    /// which only the SQL Server native path can express (MariaDB and SQLite honour it through their
    /// scoped session controls); every other path always checks and rejects it with
    /// <see cref="NotSupportedException"/>. Defaults to <see langword="null"/> (the provider default).
    /// </summary>
    public bool? CheckConstraints { get; init; }

    /// <summary>
    /// Whether a table-level bulk-update lock is taken for the duration of the write. Only the SQL
    /// Server native <c>SqlBulkCopy</c> path can express it (<c>SqlBulkCopyOptions.TableLock</c>), so
    /// <see langword="true"/> is rejected elsewhere. <see langword="false"/> requests no such lock and
    /// is honoured by every path by not asking for one. Defaults to <see langword="null"/> (row locks).
    /// </summary>
    public bool? TableLock { get; init; }

    /// <summary>
    /// Whether explicit null values are written instead of the destination column's DEFAULT. Every path
    /// writes the caller's values, so <see langword="true"/> is honoured everywhere (SQL Server maps it
    /// to <c>SqlBulkCopyOptions.KeepNulls</c>). <see langword="false"/> asks the destination to apply the
    /// column DEFAULT instead of an explicit null; it is accepted where the written shape is
    /// default-equivalent and rejected otherwise when the target provider is known. Defaults to
    /// <see langword="null"/> (the provider default).
    /// </summary>
    public bool? KeepNulls { get; init; }

    /// <summary>
    /// Whether INSERT triggers fire for the written rows. Only the SQL Server native
    /// <c>SqlBulkCopy</c> path can express it (<c>SqlBulkCopyOptions.FireTriggers</c>), so
    /// <see langword="true"/> is rejected elsewhere. <see langword="false"/> requests no firing and is
    /// honoured by every path by not asking for it. Defaults to <see langword="null"/> (triggers do not
    /// fire on the native path).
    /// </summary>
    public bool? FireTriggers { get; init; }

    /// <summary>
    /// Overrides the entity's mapped table for this write, without requiring a second <c>[SqlTable]</c>
    /// mapping. When set, the naming convention is not applied to the target; <see cref="TableSchema"/>
    /// optionally qualifies it. Defaults to <see langword="null"/> (the mapped table name is used).
    /// </summary>
    public string? TableName { get; init; }

    /// <summary>
    /// The schema (or database) that qualifies <see cref="TableName"/>, or <see langword="null"/> for an
    /// unqualified target. Only meaningful together with <see cref="TableName"/>. Kept for compatibility;
    /// the independent destination part is <see cref="SchemaName"/>.
    /// </summary>
    public string? TableSchema { get; init; }

    /// <summary>
    /// The schema part of an independently qualified destination, or <see langword="null"/> to take it from
    /// the mapping/context. Unlike <see cref="TableSchema"/> it does not require <see cref="TableName"/>:
    /// schema-only targeting keeps the mapped table and overrides only its schema.
    /// </summary>
    public string? SchemaName { get; init; }

    /// <summary>
    /// The database part of an independently qualified destination, or <see langword="null"/> to take it
    /// from the mapping/context. Provider support is validated when the target provider is known; not every
    /// provider can address another database.
    /// </summary>
    public string? DatabaseName { get; init; }

    /// <summary>
    /// The server part of an independently qualified destination, or <see langword="null"/>. This is an
    /// identifier qualification only where the provider/path supports it, never connection rerouting; an
    /// unsupported server qualification is rejected when the target provider is known.
    /// </summary>
    public string? ServerName { get; init; }

    /// <summary>
    /// The whole-invocation timeout budget, measured from the moment the bulk operation starts, or
    /// <see langword="null"/> for no additional nextorm deadline. Spans connection acquisition, setup,
    /// asynchronous enumeration, batching, driver work and progress work, and never resets per batch.
    /// <see cref="TimeSpan.Zero"/> and negative values are rejected.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// SQL Server-specific native <c>SqlBulkCopy</c> options, or <see langword="null"/> (nothing requested).
    /// A group with every value unset creates no request; a supplied value on another provider is rejected
    /// when the target provider is known.
    /// </summary>
    public SqlServerBulkInsertOptions? SqlServer { get; init; }

    /// <summary>
    /// ClickHouse-specific native <c>InsertBinaryAsync</c> options, or <see langword="null"/> (nothing
    /// requested). A group with every value unset creates no request; a supplied value on another provider
    /// is rejected when the target provider is known.
    /// </summary>
    public ClickHouseBulkInsertOptions? ClickHouse { get; init; }

    /// <summary>
    /// Called with the cumulative written-row count every <see cref="NotifyEvery"/> rows, or
    /// <see langword="null"/>. The callback runs inline after a committed batch (portable) or on the
    /// provider's native progress tick; nextorm opens no implicit transaction, so a throwing callback
    /// propagates and leaves the already-written rows committed. The second argument is the token from
    /// <see cref="ProgressCancellationTokenSource"/> (or <see cref="CancellationToken.None"/>); the write
    /// aborts with <see cref="OperationCanceledException"/> if the token is cancelled when the callback
    /// returns — arm it with <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> to time-limit the
    /// callback cooperatively.
    /// </summary>
    public Action<int, CancellationToken>? Progress { get; init; }

    /// <summary>
    /// The source whose <see cref="CancellationTokenSource.Token"/> is passed to <see cref="Progress"/>, or
    /// <see langword="null"/> for <see cref="CancellationToken.None"/>. Owned by the caller: nextorm never
    /// cancels or disposes it. Cancelling it (for example through
    /// <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/>) makes the writer throw
    /// <see cref="OperationCanceledException"/> once the in-flight callback returns.
    /// </summary>
    public CancellationTokenSource? ProgressCancellationTokenSource { get; init; }

    /// <summary>The progress reporting interval in rows. Defaults to <c>1</c>.</summary>
    public int NotifyEvery { get; init; } = 1;

    /// <summary>
    /// Whether <see cref="KeepIdentity"/> was explicitly supplied, including an explicit
    /// <see langword="false"/>. Presence is tracked separately from the value because the public member
    /// keeps its non-nullable <see cref="bool"/> shape.
    /// </summary>
    internal bool KeepIdentitySpecified => _keepIdentitySpecified;

    /// <summary>
    /// Whether <see cref="IgnoreDuplicates"/> was explicitly supplied, including an explicit
    /// <see langword="false"/>.
    /// </summary>
    internal bool IgnoreDuplicatesSpecified => _ignoreDuplicatesSpecified;

    /// <summary>Whether <see cref="CheckConstraints"/> was explicitly supplied (a nullable flag is present when it has a value).</summary>
    internal bool CheckConstraintsSpecified => CheckConstraints is not null;

    /// <summary>Whether <see cref="TableLock"/> was explicitly supplied (a nullable flag is present when it has a value).</summary>
    internal bool TableLockSpecified => TableLock is not null;

    /// <summary>Whether <see cref="KeepNulls"/> was explicitly supplied (a nullable flag is present when it has a value).</summary>
    internal bool KeepNullsSpecified => KeepNulls is not null;

    /// <summary>Whether <see cref="FireTriggers"/> was explicitly supplied (a nullable flag is present when it has a value).</summary>
    internal bool FireTriggersSpecified => FireTriggers is not null;

    /// <summary>Validates the option values, throwing for a non-positive bound or timeout, an invalid enum value, an empty target part or an invalid interval.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A size, timeout or interval is not positive, or an enum value is undefined.</exception>
    /// <exception cref="ArgumentException">A table or destination part is empty, or a schema was given without a table.</exception>
    internal void Validate()
    {
        ThrowIfNonPositive(MaxBatchSize, nameof(MaxBatchSize));
        ThrowIfNonPositive(MaxParameters, nameof(MaxParameters));
        ThrowIfNonPositive(MaxSqlLength, nameof(MaxSqlLength));

        if (Timeout is { } timeout && timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(Timeout), Timeout, "Timeout must be positive.");

        if (!Enum.IsDefined(Mode))
            throw new ArgumentOutOfRangeException(nameof(Mode), Mode, "Mode must be a defined BulkInsertMode value.");

        if (!Enum.IsDefined(TargetKind))
            throw new ArgumentOutOfRangeException(nameof(TargetKind), TargetKind, "TargetKind must be a defined BulkInsertTargetKind value.");

        if (NotifyEvery <= 0)
            throw new ArgumentOutOfRangeException(nameof(NotifyEvery), NotifyEvery, "NotifyEvery must be positive.");

        // A non-positive parallelism has no meaning on either path, so it is an argument error wherever the
        // group is supplied, not a path-dependent refusal.
        if (ClickHouse is { MaxDegreeOfParallelism: <= 0 } parallel)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ClickHouseBulkInsertOptions.MaxDegreeOfParallelism),
                parallel.MaxDegreeOfParallelism,
                "ClickHouseBulkInsertOptions.MaxDegreeOfParallelism must be positive.");
        }

        if (TableName is not null && string.IsNullOrWhiteSpace(TableName))
            throw new ArgumentException("TableName must not be empty.", nameof(TableName));

        if (TableSchema is not null && string.IsNullOrWhiteSpace(TableSchema))
            throw new ArgumentException("TableSchema must not be empty.", nameof(TableSchema));

        if (TableSchema is not null && TableName is null)
            throw new ArgumentException("TableSchema requires TableName to be set.", nameof(TableSchema));

        if (SchemaName is not null && string.IsNullOrWhiteSpace(SchemaName))
            throw new ArgumentException("SchemaName must not be empty.", nameof(SchemaName));

        if (DatabaseName is not null && string.IsNullOrWhiteSpace(DatabaseName))
            throw new ArgumentException("DatabaseName must not be empty.", nameof(DatabaseName));

        if (ServerName is not null && string.IsNullOrWhiteSpace(ServerName))
            throw new ArgumentException("ServerName must not be empty.", nameof(ServerName));
    }

    /// <summary>
    /// Validates the provider-scoped option groups and the provider-specific request against the provider
    /// whose bulk path is about to run. A group that explicitly requests a value is meaningful only on its
    /// own provider, including an explicit <see langword="false"/>; an empty group requests nothing and is
    /// accepted everywhere. <see langword="null"/> means the provider identity could not be resolved (a
    /// custom dialect or a context with no SQL dialect), which cannot host a provider-scoped group.
    /// </summary>
    /// <param name="provider">The provider identity resolved for the write, or <see langword="null"/> when unknown.</param>
    /// <exception cref="NotSupportedException">A provider group was supplied for a different provider.</exception>
    internal void ValidateForProvider(BulkInsertProvider? provider)
    {
        if (SqlServer is { RequestsAny: true } && provider != BulkInsertProvider.SqlServer)
            throw new NotSupportedException(
                $"SqlServerBulkInsertOptions.UseInternalTransaction is only supported by the SQL Server native SqlBulkCopy path; {BulkInsertProviders.Describe(provider)} cannot express it.");

        if (ClickHouse is { RequestsAny: true } && provider != BulkInsertProvider.ClickHouse)
            throw new NotSupportedException(
                $"ClickHouseBulkInsertOptions (MaxDegreeOfParallelism/UseSession) are only supported by the ClickHouse native InsertBinaryAsync path; {BulkInsertProviders.Describe(provider)} cannot express them.");
    }

    /// <summary>
    /// Validates the requested destination kind against the resolved provider and path. A regular target
    /// takes the existing path unchanged. A local temporary target uses the provider's existing temporary
    /// mechanism on the context's own connection/session, so a request that contradicts that mechanism is
    /// refused instead of being accepted and ignored.
    /// </summary>
    /// <param name="provider">The provider identity resolved for the write, or <see langword="null"/> when unknown.</param>
    /// <exception cref="NotSupportedException">The target kind contradicts a requested option.</exception>
    internal void ValidateTargetKind(BulkInsertProvider? provider)
    {
        if (TargetKind == BulkInsertTargetKind.Regular)
            return;

        if (DatabaseName is not null)
            throw new NotSupportedException(
                $"BulkInsertOptions.TargetKind.LocalTemporary cannot be combined with BulkInsertOptions.DatabaseName ('{DatabaseName}'): a local temporary table lives in the current session's temporary store, never in another database.");

        if (provider == BulkInsertProvider.SqlServer && (SchemaName ?? TableSchema) is not null)
            throw new NotSupportedException(
                $"BulkInsertOptions.TargetKind.LocalTemporary cannot be combined with BulkInsertOptions.SchemaName/TableSchema on SQL Server: a #temp table is session-scoped and takes no schema qualifier.");

        if (ClickHouse is { UseSession: false })
            throw new NotSupportedException(
                "BulkInsertOptions.TargetKind.LocalTemporary cannot be combined with ClickHouseBulkInsertOptions.UseSession=false: a sessionless insert cannot see a session-local temporary table.");

        if (ClickHouse is { MaxDegreeOfParallelism: > 1 } clickHouse)
            throw new NotSupportedException(
                $"BulkInsertOptions.TargetKind.LocalTemporary cannot be combined with ClickHouseBulkInsertOptions.MaxDegreeOfParallelism={clickHouse.MaxDegreeOfParallelism}: a parallel insert cannot address a session-local temporary table.");

        // Every built-in provider writes a local temporary table on the context's own connection, which
        // is exactly the path the native/portable executors already use, so no further refusal is needed.
    }

    /// <summary>
    /// Validates options that only the provider's native path can express against the resolved path, so a
    /// request the portable statement cannot honour is refused instead of silently ignored (the documented
    /// contract of <see cref="BulkInsertMode.Portable"/>). A request whose portable form is equivalent to
    /// the native one is accepted here and validated by the provider scope, which alone knows the
    /// connection's session.
    /// </summary>
    /// <param name="useNative">Whether the write takes the provider's native bulk path.</param>
    /// <exception cref="NotSupportedException">A native-only option was requested on the portable path.</exception>
    internal void ValidateNativeOnlyOptions(bool useNative)
    {
        if (useNative)
            return;

        // ClickHouse: the portable INSERT ... VALUES path is a single stream over the context connection,
        // so a parallelism above one has no portable form. Null and 1 are the same single-stream result and
        // are accepted as equivalent; the session semantics (UseSession null/true/false) are decided by the
        // provider scope, because only it can read the connection's actual session id and visibility.
        if (ClickHouse is { MaxDegreeOfParallelism: > 1 } parallel)
        {
            throw new NotSupportedException(
                $"ClickHouseBulkInsertOptions.MaxDegreeOfParallelism={parallel.MaxDegreeOfParallelism} cannot be honoured by the portable INSERT ... VALUES path: "
                + "it writes a single stream. Use MaxDegreeOfParallelism=null or 1, or run the write with Mode.Auto or Mode.Native on a ClickHouse client.");
        }

        if (SqlServer is { UseInternalTransaction: true })
        {
            throw new NotSupportedException(
                "The bulk-insert option SqlServerBulkInsertOptions.UseInternalTransaction=true is only supported by the provider's native bulk path; "
                + "the portable INSERT ... VALUES path cannot express it. Use Mode.Auto or Mode.Native.");
        }
    }

    /// <summary>
    /// Validates the SQL Server-shaped bulk-copy flags against the semantic matrix: a request the resolved
    /// path cannot honour is refused before any row is read, while an explicitly <see langword="false"/>
    /// value is honoured by absence and is never treated as a request. Every offending option is named in
    /// one message.
    /// </summary>
    /// <param name="provider">The provider identity resolved for the write, or <see langword="null"/> when unknown.</param>
    /// <param name="useNative">Whether the write takes the provider's native bulk path.</param>
    /// <exception cref="NotSupportedException">A requested flag cannot be expressed by the resolved path.</exception>
    internal void ValidateBulkCopyFlags(BulkInsertProvider? provider, bool useNative)
    {
        var sqlServerNative = provider == BulkInsertProvider.SqlServer && useNative;
        var rejected = new List<string>(3);

        if (TableLock is true && !sqlServerNative)
            rejected.Add("TableLock=true");

        if (FireTriggers is true && !sqlServerNative)
            rejected.Add("FireTriggers=true");

        // false means "do not check constraints": only the SQL Server native flag turns checking off, and
        // MariaDB/SQLite honour it through their scoped session controls. Every other path checks them.
        if (CheckConstraints is false
            && !sqlServerNative
            && provider is not (BulkInsertProvider.MariaDb or BulkInsertProvider.Sqlite))
        {
            rejected.Add("CheckConstraints=false");
        }

        // KeepNulls needs no D1 refusal: true is honoured by every path (they write the caller's values),
        // and false is accepted only for a default-equivalent shape, which the target provider validates
        // against the mapped columns before the write.

        if (rejected.Count == 0)
            return;

        var path = useNative ? "the native bulk path" : "the portable INSERT ... VALUES path";
        throw new NotSupportedException(
            $"The bulk-insert option(s) {string.Join(", ", rejected)} cannot be honoured by {path} on {BulkInsertProviders.Describe(provider)}. "
            + "Only the SQL Server native SqlBulkCopy path can turn constraint checking off, take a table lock or fire triggers; "
            + "an explicit false is honoured everywhere by not requesting the behaviour.");
    }

    private static void ThrowIfNonPositive(int? value, string name)
    {
        if (value is <= 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
    }
}

/// <summary>
/// Identifies the provider whose bulk path is about to run, so provider-scoped option groups can be
/// validated only after the target provider is known.
/// </summary>
internal enum BulkInsertProvider
{
    /// <summary>SQL Server native <c>SqlBulkCopy</c>.</summary>
    SqlServer,

    /// <summary>PostgreSQL native <c>COPY</c>.</summary>
    Postgres,

    /// <summary>MySQL native <c>MySqlBulkCopy</c>.</summary>
    MySql,

    /// <summary>MariaDB through the shared MySQL implementation.</summary>
    MariaDb,

    /// <summary>ClickHouse native <c>InsertBinaryAsync</c>.</summary>
    ClickHouse,

    /// <summary>SQLite portable path.</summary>
    Sqlite,
}

/// <summary>
/// Resolves a dialect's built-in provider identity and renders it for diagnostics, so core never references
/// a provider assembly. Only the six built-in dialects are mapped; a custom dialect or a user subclass of a
/// built-in one resolves to <see langword="null"/>, which is treated as "cannot host a provider-scoped
/// group" rather than guessed. The identity check is exact (assembly name plus full type name), so a
/// same-named type from another assembly is not mistaken for a built-in either.
/// </summary>
internal static class BulkInsertProviders
{
    /// <summary>Resolves the provider identity of <paramref name="dialect"/>, or <see langword="null"/> when it is not a built-in dialect.</summary>
    /// <param name="dialect">The dialect of the context the write runs on, or <see langword="null"/> when the context has none.</param>
    /// <returns>The provider identity, or <see langword="null"/>.</returns>
    internal static BulkInsertProvider? From(ISqlDialect? dialect)
    {
        if (dialect is null)
            return null;

        var type = dialect.GetType();
        return (type.Assembly.GetName().Name, type.FullName) switch
        {
            ("nextorm.sqlserver", "NextORM.SqlServer.SqlServerDialect") => BulkInsertProvider.SqlServer,
            ("nextorm.postgres", "NextORM.Postgres.PostgresDialect") => BulkInsertProvider.Postgres,
            ("nextorm.mysql", "NextORM.MySql.MySqlDialect") => BulkInsertProvider.MySql,
            ("nextorm.mariadb", "NextORM.MariaDb.MariaDbDialect") => BulkInsertProvider.MariaDb,
            ("nextorm.clickhouse", "NextORM.ClickHouse.ClickHouseDialect") => BulkInsertProvider.ClickHouse,
            ("nextorm.sqlite", "NextORM.Sqlite.SqliteDialect") => BulkInsertProvider.Sqlite,
            _ => null,
        };
    }

    /// <summary>Renders a provider identity for an error message.</summary>
    /// <param name="provider">The provider identity, or <see langword="null"/> when unknown.</param>
    /// <returns>The provider display name.</returns>
    internal static string Describe(BulkInsertProvider? provider)
        => provider switch
        {
            BulkInsertProvider.SqlServer => "SQL Server",
            BulkInsertProvider.Postgres => "PostgreSQL",
            BulkInsertProvider.MySql => "MySQL",
            BulkInsertProvider.MariaDb => "MariaDB",
            BulkInsertProvider.ClickHouse => "ClickHouse",
            BulkInsertProvider.Sqlite => "SQLite",
            _ => "the resolved provider (identity unknown)",
        };
}

/// <summary>
/// Fluent builder for <see cref="BulkInsertOptions"/>. Every setter validates immediately and returns the
/// builder, so an invalid option fails at the call site rather than at execution.
/// </summary>
public sealed class BulkInsertOptionsBuilder
{
    private BulkInsertMode _mode = BulkInsertMode.Auto;
    private BulkInsertTargetKind _targetKind = BulkInsertTargetKind.Regular;
    private int? _maxBatchSize;
    private int? _maxParameters;
    private int? _maxSqlLength;
    private bool _keepIdentity;
    private bool _keepIdentitySpecified;
    private bool _ignoreDuplicates;
    private bool _ignoreDuplicatesSpecified;
    private bool? _checkConstraints;
    private bool? _tableLock;
    private bool? _keepNulls;
    private bool? _fireTriggers;
    private string? _tableName;
    private string? _tableSchema;
    private string? _schemaName;
    private string? _databaseName;
    private string? _serverName;
    private TimeSpan? _timeout;
    private SqlServerBulkInsertOptions? _sqlServer;
    private ClickHouseBulkInsertOptions? _clickHouse;
    private Action<int, CancellationToken>? _progress;
    private int _notifyEvery = 1;
    private CancellationTokenSource? _progressCancellationTokenSource;

    /// <summary>Selects the execution path.</summary>
    /// <param name="mode">The requested mode; must be a defined <see cref="BulkInsertMode"/> value.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is undefined.</exception>
    public BulkInsertOptionsBuilder Mode(BulkInsertMode mode)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "mode must be a defined BulkInsertMode value.");

        _mode = mode;
        return this;
    }

    /// <summary>Selects the kind of destination table.</summary>
    /// <param name="kind">The requested target kind; must be a defined <see cref="BulkInsertTargetKind"/> value.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is undefined.</exception>
    public BulkInsertOptionsBuilder TargetKind(BulkInsertTargetKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a defined BulkInsertTargetKind value.");

        _targetKind = kind;
        return this;
    }

    /// <summary>Limits the rows written per statement (the portable path chunks; the native path treats it as a batch hint or ignores it).</summary>
    /// <param name="rows">The maximum rows per statement; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows"/> is not positive.</exception>
    public BulkInsertOptionsBuilder MaxBatchSize(int rows)
    {
        ThrowIfNonPositive(rows, nameof(rows));
        _maxBatchSize = rows;
        return this;
    }

    /// <summary>Limits the bound parameters per statement on the portable path.</summary>
    /// <param name="count">The maximum parameters per statement; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is not positive.</exception>
    public BulkInsertOptionsBuilder MaxParameters(int count)
    {
        ThrowIfNonPositive(count, nameof(count));
        _maxParameters = count;
        return this;
    }

    /// <summary>Limits the approximate SQL text length per statement on the portable path.</summary>
    /// <param name="characters">The approximate maximum SQL length; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="characters"/> is not positive.</exception>
    public BulkInsertOptionsBuilder MaxSqlLength(int characters)
    {
        ThrowIfNonPositive(characters, nameof(characters));
        _maxSqlLength = characters;
        return this;
    }

    /// <summary>
    /// Writes explicit values to identity columns. The path stays capability based: <see cref="BulkInsertMode.Auto"/>
    /// uses the native path where it can honour the option and the portable path otherwise,
    /// <see cref="BulkInsertMode.Native"/> refuses the request by name instead of dropping the option, and
    /// <see cref="BulkInsertMode.Portable"/> selects the portable path explicitly.
    /// </summary>
    /// <param name="value">Whether to write identity values; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder KeepIdentity(bool value = true)
    {
        _keepIdentity = value;
        _keepIdentitySpecified = true;
        return this;
    }

    /// <summary>
    /// Skips rows that violate a unique constraint. Honoured where either path can express it (the native
    /// provider load or the portable statement); a provider where neither can refuses the request by name
    /// instead of writing the rows as a plain insert, and an explicit <see cref="BulkInsertMode.Native"/>
    /// refuses it rather than silently dropping the option.
    /// </summary>
    /// <param name="value">Whether to skip conflicting rows; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder IgnoreDuplicates(bool value = true)
    {
        _ignoreDuplicates = value;
        _ignoreDuplicatesSpecified = true;
        return this;
    }

    /// <summary>Checks CHECK and FOREIGN KEY constraints during the write (SQL Server native path only).</summary>
    /// <param name="value">Whether constraints are checked; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder CheckConstraints(bool value = true)
    {
        _checkConstraints = value;
        return this;
    }

    /// <summary>Takes a table-level bulk-update lock for the duration of the write (SQL Server native path only).</summary>
    /// <param name="value">Whether the table is locked; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder TableLock(bool value = true)
    {
        _tableLock = value;
        return this;
    }

    /// <summary>Writes explicit null values instead of the destination DEFAULT (SQL Server native path only).</summary>
    /// <param name="value">Whether nulls are kept; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder KeepNulls(bool value = true)
    {
        _keepNulls = value;
        return this;
    }

    /// <summary>Fires INSERT triggers for the written rows (SQL Server native path only).</summary>
    /// <param name="value">Whether triggers fire; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public BulkInsertOptionsBuilder FireTriggers(bool value = true)
    {
        _fireTriggers = value;
        return this;
    }

    /// <summary>
    /// Overrides the target table for this write. The naming convention is not applied to an explicit
    /// target; use the two-argument <see cref="Table(string, string)"/> overload to qualify it with a schema.
    /// </summary>
    /// <param name="table">The destination table name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="table"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Table(string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(table);
        _tableName = table;
        _tableSchema = null;
        return this;
    }

    /// <summary>Overrides the target table for this write and qualifies it with a schema (or database).</summary>
    /// <param name="schema">The schema (or database) name; must not be empty.</param>
    /// <param name="table">The destination table name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="schema"/> or <paramref name="table"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Table(string schema, string table)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        ArgumentException.ThrowIfNullOrEmpty(table);
        _tableSchema = schema;
        _tableName = table;
        return this;
    }

    /// <summary>Overrides the schema part of the destination, keeping the mapped or explicit table.</summary>
    /// <param name="schema">The schema name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="schema"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Schema(string schema)
    {
        ArgumentException.ThrowIfNullOrEmpty(schema);
        _schemaName = schema;
        return this;
    }

    /// <summary>Overrides the database part of the destination.</summary>
    /// <param name="database">The database name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="database"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Database(string database)
    {
        ArgumentException.ThrowIfNullOrEmpty(database);
        _databaseName = database;
        return this;
    }

    /// <summary>Overrides the server part of the destination where the provider/path supports server qualification.</summary>
    /// <param name="server">The server name; must not be empty.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="server"/> is <see langword="null"/> or empty.</exception>
    public BulkInsertOptionsBuilder Server(string server)
    {
        ArgumentException.ThrowIfNullOrEmpty(server);
        _serverName = server;
        return this;
    }

    /// <summary>Sets the whole-invocation timeout budget (all paths), measured from execution entry.</summary>
    /// <param name="timeout">The budget; must be positive.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is zero or negative.</exception>
    public BulkInsertOptionsBuilder Timeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "timeout must be positive.");

        _timeout = timeout;
        return this;
    }

    /// <summary>Supplies the SQL Server native options group.</summary>
    /// <param name="options">The SQL Server options; must not be <see langword="null"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public BulkInsertOptionsBuilder SqlServer(SqlServerBulkInsertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _sqlServer = options;
        return this;
    }

    /// <summary>Supplies the ClickHouse native options group.</summary>
    /// <param name="options">The ClickHouse options; must not be <see langword="null"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public BulkInsertOptionsBuilder ClickHouse(ClickHouseBulkInsertOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _clickHouse = options;
        return this;
    }

    /// <summary>Reports progress with the cumulative written-row count every <paramref name="rows"/> rows.</summary>
    /// <param name="rows">The reporting interval in rows; must be positive.</param>
    /// <param name="onRows">Called with the cumulative written-row count and the progress cancellation token.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="onRows"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="rows"/> is not positive.</exception>
    public BulkInsertOptionsBuilder NotifyAfter(int rows, Action<int, CancellationToken> onRows)
    {
        ArgumentNullException.ThrowIfNull(onRows);
        ThrowIfNonPositive(rows, nameof(rows));

        _notifyEvery = rows;
        _progress = onRows;
        return this;
    }

    /// <summary>
    /// Supplies the token passed to the <see cref="NotifyAfter"/> callback, enabling cooperative
    /// time-limiting (<c>source.CancelAfter(timeout)</c>). The source is owned by the caller.
    /// </summary>
    /// <param name="source">The source whose token the callback observes.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    public BulkInsertOptionsBuilder ProgressCancellationTokenSource(CancellationTokenSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _progressCancellationTokenSource = source;
        return this;
    }

    /// <summary>Builds the immutable options.</summary>
    /// <returns>The configured options.</returns>
    public BulkInsertOptions Build()
    {
        var options = new BulkInsertOptions
        {
            Mode = _mode,
            TargetKind = _targetKind,
            MaxBatchSize = _maxBatchSize,
            MaxParameters = _maxParameters,
            MaxSqlLength = _maxSqlLength,
            CheckConstraints = _checkConstraints,
            TableLock = _tableLock,
            KeepNulls = _keepNulls,
            FireTriggers = _fireTriggers,
            TableName = _tableName,
            TableSchema = _tableSchema,
            SchemaName = _schemaName,
            DatabaseName = _databaseName,
            ServerName = _serverName,
            Timeout = _timeout,
            SqlServer = _sqlServer,
            ClickHouse = _clickHouse,
            Progress = _progress,
            NotifyEvery = _notifyEvery,
            ProgressCancellationTokenSource = _progressCancellationTokenSource,
        };

        // A non-nullable flag records presence only through its init accessor, so an unset builder flag
        // must not be assigned at all; an explicit false is assigned and therefore becomes present.
        if (_keepIdentitySpecified)
            options = options with { KeepIdentity = _keepIdentity };

        if (_ignoreDuplicatesSpecified)
            options = options with { IgnoreDuplicates = _ignoreDuplicates };

        return options;
    }

    private static void ThrowIfNonPositive(int value, string name)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(name, value, $"{name} must be positive.");
    }
}
