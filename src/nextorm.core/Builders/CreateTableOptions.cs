namespace NextORM.Core;

/// <summary>
/// The action a provider takes on the rows of a temporary table created with <c>ON COMMIT</c> when the
/// creating transaction commits.
/// </summary>
public enum TempTableOnCommit
{
    /// <summary>Keep the rows (the default; the clause is omitted).</summary>
    PreserveRows,
    /// <summary>Delete the rows on commit (the table is kept).</summary>
    DeleteRows,
    /// <summary>Drop the table on commit.</summary>
    Drop,
}

/// <summary>
/// Options for materialising a query into a table with <see cref="TempTableExtensions"/>.
/// Providers that cannot express a given option reject it with <see cref="NotSupportedException"/> when
/// the SQL is built.
/// </summary>
public sealed record CreateTableOptions
{
    /// <summary>Whether the statement carries <c>IF NOT EXISTS</c>. Defaults to <see langword="false"/>.</summary>
    public bool IfNotExists { get; init; }

    /// <summary>
    /// Whether an existing table with the target name is dropped first (<c>DROP TABLE IF EXISTS</c>),
    /// so the materialisation always reflects the current query. Only valid for a persistent table
    /// (see <see cref="TempTableExtensions"/>); mutually exclusive with <see cref="IfNotExists"/>.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    public bool DropExisting { get; init; }

    /// <summary>
    /// Whether the table is populated from the query; <see langword="false"/> renders <c>WITH NO DATA</c>
    /// (PostgreSQL only). Defaults to <see langword="true"/>.
    /// </summary>
    public bool WithData { get; init; } = true;

    /// <summary>
    /// The <c>ON COMMIT</c> action of a temporary table (PostgreSQL only). Only valid for a temporary
    /// table (see <see cref="TempTableExtensions"/>); defaults to
    /// <see cref="TempTableOnCommit.PreserveRows"/>.
    /// </summary>
    public TempTableOnCommit OnCommit { get; init; } = TempTableOnCommit.PreserveRows;

    /// <summary>
    /// The column names to declare on the created table, or <see langword="null"/> to derive them from
    /// the query. Not every provider accepts a column list together with <c>AS SELECT</c> (SQLite does not).
    /// </summary>
    public IReadOnlyList<string>? Columns { get; init; }

    /// <summary>Validates the option combination against the materialisation form.</summary>
    /// <param name="temporary">Whether the target is a temporary table.</param>
    /// <exception cref="ArgumentException"><see cref="DropExisting"/> and <see cref="IfNotExists"/> are both set.</exception>
    /// <exception cref="NotSupportedException"><see cref="DropExisting"/> is requested for a temporary table.</exception>
    internal void Validate(bool temporary)
    {
        ValidateCombination();

        if (DropExisting && temporary)
            throw new NotSupportedException(
                "DropExisting applies only to a persistent table; a temporary table is already session-scoped.");
    }

    /// <summary>Validates the options that are invalid regardless of whether the target is temporary or persistent.</summary>
    /// <exception cref="ArgumentException"><see cref="DropExisting"/> and <see cref="IfNotExists"/> are both set.</exception>
    internal void ValidateCombination()
    {
        if (DropExisting && IfNotExists)
            throw new ArgumentException("DropExisting and IfNotExists are mutually exclusive; set only one of them.");
    }
}

/// <summary>
/// Fluent builder for <see cref="CreateTableOptions"/>. Every setter returns the builder, so options can be
/// composed inline at a materialisation terminal, for example <c>ToTable("t", o =&gt; o.DropExisting())</c>.
/// A rule that depends on the target form (temporary vs persistent) is checked when the statement is built;
/// the contradictory combination is already rejected by <see cref="Build()"/>.
/// </summary>
public sealed class CreateTableOptionsBuilder
{
    private bool _ifNotExists;
    private bool _dropExisting;
    private bool _withData = true;
    private TempTableOnCommit _onCommit = TempTableOnCommit.PreserveRows;
    private List<string>? _columns;

    /// <summary>Adds <c>IF NOT EXISTS</c>, so repeating the statement is a no-op.</summary>
    /// <param name="value">Whether to emit the clause; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public CreateTableOptionsBuilder IfNotExists(bool value = true)
    {
        _ifNotExists = value;
        return this;
    }

    /// <summary>
    /// Drops an existing table with the target name first (<c>DROP TABLE IF EXISTS</c>), so the
    /// materialisation always reflects the current query. Persistent tables only; mutually exclusive with
    /// <see cref="IfNotExists(bool)"/>.
    /// </summary>
    /// <param name="value">Whether to drop the existing table; defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public CreateTableOptionsBuilder DropExisting(bool value = true)
    {
        _dropExisting = value;
        return this;
    }

    /// <summary>Populates the table from the query.</summary>
    /// <param name="value"><see langword="false"/> renders <c>WITH NO DATA</c> (PostgreSQL only); defaults to <see langword="true"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public CreateTableOptionsBuilder WithData(bool value = true)
    {
        _withData = value;
        return this;
    }

    /// <summary>Sets the <c>ON COMMIT</c> action of a temporary table (PostgreSQL only).</summary>
    /// <param name="value">The commit action; defaults to <see cref="TempTableOnCommit.PreserveRows"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public CreateTableOptionsBuilder OnCommit(TempTableOnCommit value)
    {
        _onCommit = value;
        return this;
    }

    /// <summary>Declares the target column names instead of deriving them from the query.</summary>
    /// <param name="names">The column names, in order; must not be empty and must not contain null or empty entries.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="names"/> is empty or contains a null or empty entry.</exception>
    public CreateTableOptionsBuilder Columns(params string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (names.Length == 0)
            throw new ArgumentException("At least one column name is required.", nameof(names));

        var columns = new List<string>(names.Length);
        foreach (var name in names)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            columns.Add(name);
        }

        _columns = columns;
        return this;
    }

    /// <summary>Builds the immutable options and rejects the combination that is invalid regardless of the target form.</summary>
    /// <returns>The configured options.</returns>
    /// <exception cref="ArgumentException"><see cref="CreateTableOptions.DropExisting"/> and <see cref="CreateTableOptions.IfNotExists"/> are both set.</exception>
    public CreateTableOptions Build()
    {
        var options = new CreateTableOptions
        {
            IfNotExists = _ifNotExists,
            DropExisting = _dropExisting,
            WithData = _withData,
            OnCommit = _onCommit,
            Columns = _columns?.ToArray(),
        };

        options.ValidateCombination();
        return options;
    }

    /// <summary>Runs a fluent configuration callback and builds the resulting options; used by the materialisation terminals.</summary>
    /// <param name="configure">Configures the options; its return value is ignored.</param>
    /// <returns>The configured options.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    internal static CreateTableOptions Build(Func<CreateTableOptionsBuilder, CreateTableOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new CreateTableOptionsBuilder();
        configure(builder);
        return builder.Build();
    }
}
