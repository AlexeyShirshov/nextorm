namespace NextORM.Core;

/// <summary>
/// A dialect's renderer for the conditional <c>iif(condition, whenTrue, whenFalse)</c>. The object's
/// presence is the capability: a dialect whose provider has a native spelling exposes a stateless
/// singleton, while a dialect without one returns <c>null</c> from <see cref="ISqlDialect.Iif"/> and
/// the expression translator rejects the call instead of emitting SQL the provider cannot execute.
/// </summary>
public interface IIifRenderer
{
    /// <summary>
    /// Renders the conditional over the already-rendered <paramref name="condition"/>,
    /// <paramref name="whenTrue"/> and <paramref name="whenFalse"/> operands.
    /// </summary>
    string Render(string condition, string whenTrue, string whenFalse);
}

/// <summary>
/// A dialect's session/information function surface (<c>current_user</c>, <c>session_user</c>,
/// <c>current_schema</c>, <c>current_database</c>, <c>version</c>). The predicate and the renderer live
/// on one object, so a name the dialect reports as supported always has a rendering; a provider that
/// can express only part of the family (ClickHouse, SQLite) exposes the same object and answers
/// <see cref="Supports"/> per name.
/// </summary>
/// <remarks>
/// Unlike the stateless <see cref="IIifRenderer"/>/<see cref="ILimitByRenderer"/>, this object carries
/// both the per-name predicate and the renderer, hence the plural capability-noun name rather than the
/// <c>*Renderer</c> suffix.
/// </remarks>
public interface ISessionInfoFunctions
{
    /// <summary>True when the session/information function <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>Renders the session/information function <paramref name="name"/>.</summary>
    string Render(string name);
}

/// <summary>
/// A dialect's UUID generator surface (<c>gen_random_uuid</c>, <c>uuidv7</c>). The predicate and the
/// renderer live on one object, so a generator the dialect reports as supported always has a
/// rendering; a provider with only one of the two (SQL Server, MariaDB) answers
/// <see cref="Supports"/> per name.
/// </summary>
/// <remarks>
/// Unlike the stateless <see cref="IIifRenderer"/>/<see cref="ILimitByRenderer"/>, this object carries
/// both the per-name predicate and the renderer, hence the plural capability-noun name rather than the
/// <c>*Renderer</c> suffix.
/// </remarks>
public interface IUuidGenerators
{
    /// <summary>True when the UUID generator <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>Renders the UUID generator <paramref name="name"/>.</summary>
    string Render(string name);
}

/// <summary>
/// A dialect's formatting surface for the culture-invariant subset of CLR format specifiers used by
/// <c>string.Format</c>/<c>$"...{x:fmt}"</c>/<c>x.ToString(fmt)</c>. The object also answers whether a
/// given specifier is expressible exactly, so a provider that can render <c>F</c>/<c>D</c> but not
/// <c>N</c> rejects the latter instead of dropping the group separators. The object's presence is the
/// capability; <see langword="null"/> means the provider cannot render CLR format specifiers at all.
/// </summary>
public interface IStringFormatFunctions
{
    /// <summary>True when the numeric standard specifier <paramref name="specifier"/> (<c>N</c>, <c>F</c>, <c>D</c>, <c>X</c>) can be rendered exactly.</summary>
    bool SupportsNumber(char specifier);

    /// <summary>
    /// Renders the numeric standard specifier <paramref name="specifier"/> with
    /// <paramref name="precision"/> fractional (or minimum integral) digits over the already-rendered
    /// <paramref name="value"/>.
    /// </summary>
    string RenderNumber(string value, char specifier, int precision);

    /// <summary>True when the CLR custom date/time format <paramref name="clrFormat"/> can be rendered exactly.</summary>
    bool SupportsDateFormat(string clrFormat);

    /// <summary>Renders the CLR custom date/time format <paramref name="clrFormat"/> over the already-rendered <paramref name="value"/>.</summary>
    string RenderDate(string value, string clrFormat);
}

/// <summary>
/// A dialect's renderer for the ClickHouse <c>LIMIT [offset, ]n BY expr, ...</c> modifier that keeps
/// the first <c>n</c> rows per group. The object's presence is the capability; only ClickHouse exposes
/// it today.
/// </summary>
public interface ILimitByRenderer
{
    /// <summary>
    /// Renders the <c>limit [offset, ]n by col1, col2</c> clause over the already-rendered
    /// <paramref name="columns"/>.
    /// </summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string Render(int limit, int offset, IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower);
}

/// <summary>
/// A dialect's surface for the postfix XML data-type methods on a SQL Server <c>xml</c> column
/// (<c>operand.value('xpath', 'sqltype')</c>, <c>operand.query('xpath')</c>,
/// <c>operand.exist('xpath')</c> and the <c>operand.nodes('xpath')</c> rowset). The predicate and the
/// renderer live on one object, so a method the dialect reports as supported always has a rendering;
/// <c>null</c> is the capability being absent.
/// </summary>
public interface IXmlFunctions
{
    /// <summary>True when the XML data-type method <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>
    /// Renders the XML data-type method <paramref name="name"/> as a postfix call over the
    /// already-rendered <paramref name="operand"/>, with the already-rendered <paramref name="args"/>.
    /// </summary>
    string Render(string name, string operand, IReadOnlyList<string> args);
}

/// <summary>
/// A dialect's renderer for the ClickHouse sequence/funnel aggregates (<c>windowFunnel</c>,
/// <c>sequenceMatch</c>, <c>retention</c>). The object's presence is the capability.
/// </summary>
public interface ISequenceAggregateRenderer
{
    /// <summary>
    /// Renders the sequence aggregate <paramref name="name"/> with the already-rendered
    /// <paramref name="parameters"/> (contents of the first parenthesis pair, or <c>null</c> for the
    /// single-pair form) and <paramref name="arguments"/> (the second pair).
    /// </summary>
    string Render(string name, string? parameters, string arguments);
}

/// <summary>
/// A dialect's renderer for the ClickHouse distinct-count family (<c>uniq</c>, <c>uniqExact</c>,
/// <c>uniqCombined</c>, <c>uniqHLL12</c>). The object's presence is the capability.
/// </summary>
public interface IUniqAggregateRenderer
{
    /// <summary>Renders the distinct-count aggregate <paramref name="name"/> over <paramref name="argument"/>.</summary>
    string Render(string name, string argument);
}

/// <summary>
/// A dialect's renderer for the ClickHouse parameterised quantile aggregates
/// (<c>quantile(level)(value)</c>, <c>median(value)</c>). The object's presence is the capability.
/// </summary>
public interface IQuantileAggregateRenderer
{
    /// <summary>Renders the quantile aggregate <paramref name="name"/> over <paramref name="level"/> and <paramref name="value"/>.</summary>
    string Render(string name, string level, string value);

    /// <summary>Renders the multi-level quantile aggregate <paramref name="name"/> over <paramref name="levels"/> and <paramref name="value"/>.</summary>
    string RenderLevels(string name, string levels, string value);

    /// <summary>Renders the <c>median</c> aggregate over <paramref name="value"/>.</summary>
    string RenderMedian(string value);
}

/// <summary>
/// A dialect's renderer for the ClickHouse parameterised top-K aggregates
/// (<c>topK(N)(value)</c>, <c>topKWeighted(N)(value, weight)</c>). The object's presence is the capability.
/// </summary>
public interface ITopKAggregateRenderer
{
    /// <summary>Renders the top-K aggregate <paramref name="name"/> over <paramref name="k"/> and <paramref name="value"/>.</summary>
    string Render(string name, string k, string value);

    /// <summary>Renders the weighted top-K aggregate <paramref name="name"/> over <paramref name="k"/>, <paramref name="value"/> and <paramref name="weight"/>.</summary>
    string RenderWeighted(string name, string k, string value, string weight);
}

/// <summary>
/// A dialect's renderer for the ClickHouse multi-branch conditional
/// <c>multiIf(cond1, then1, ..., else)</c>. The object's presence is the capability.
/// </summary>
public interface IMultiIfRenderer
{
    /// <summary>
    /// Renders <c>multiIf(...)</c> over the already-rendered <paramref name="arguments"/> (condition/value
    /// pairs followed by the else value), casting to the CLR <paramref name="resultType"/> the row reader
    /// must materialise when the common supertype would otherwise differ.
    /// </summary>
    string Render(IReadOnlyList<string> arguments, Type resultType);
}

/// <summary>
/// A dialect's renderer for the PostgreSQL <c>DISTINCT ON (col, ...)</c> modifier. The object's
/// presence is the capability.
/// </summary>
public interface IDistinctOnRenderer
{
    /// <summary>Renders the <c>DISTINCT ON</c> modifier over the already-rendered <paramref name="columns"/>.</summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string Render(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower);
}

/// <summary>
/// One prepared extreme-row column as the dialect's optional <see cref="IExtremeRowRenderer"/> reasons
/// about it. It carries only shape facts (CLR type, nullability, whether the key is a direct mapped
/// column) and never the expression, command or build context.
/// </summary>
/// <remarks>
/// This is a read-only input constructed by the core and handed to an external
/// <see cref="IExtremeRowRenderer"/>. Implementing a renderer means receiving instances through
/// <see cref="IExtremeRowRenderer.CanRender"/>; the constructor is intentionally not part of the public
/// contract, so no external code needs to (or can) construct one.
/// </remarks>
public sealed record ExtremeRowRenderColumn
{
    internal ExtremeRowRenderColumn(Type clrType, bool isNullable, bool isDirectMappedColumn, bool usesConverter)
    {
        ClrType = clrType;
        IsNullable = isNullable;
        IsDirectMappedColumn = isDirectMappedColumn;
        UsesConverter = usesConverter;
    }

    /// <summary>The CLR type of the value produced by the column (the model type of a key/group selector).</summary>
    public Type ClrType { get; }

    /// <summary>Whether <see cref="ClrType"/> can hold <c>null</c> (a reference type or a <see cref="Nullable{T}"/>).</summary>
    public bool IsNullable { get; }

    /// <summary>
    /// Whether the column is a direct access to a mapped entity property (no computed expression and no
    /// converter). Only such columns can participate in the initial native eligibility.
    /// </summary>
    public bool IsDirectMappedColumn { get; }

    /// <summary>Whether a value converter is attached to the mapped property.</summary>
    public bool UsesConverter { get; }
}

/// <summary>
/// A side-effect-free description of a prepared <c>SelectWhereMax</c>/<c>SelectWhereMin</c> command,
/// handed to <see cref="IExtremeRowRenderer.CanRender"/> so a dialect can decide - before any SQL,
/// alias or parameter side effect - whether its native strategy applies. An empty
/// <see cref="Groups"/> list means the global form; a non-empty one the grouped form.
/// </summary>
/// <remarks>
/// This is a read-only input constructed by the core and handed to an external
/// <see cref="IExtremeRowRenderer"/>. Implementing a renderer means receiving instances through
/// <see cref="IExtremeRowRenderer.CanRender"/>; the constructor is intentionally not part of the public
/// contract, so no external code needs to (or can) construct one.
/// </remarks>
public sealed record ExtremeRowDescription
{
    // The payload is resolved lazily: PostgreSQL's renderer decides from Keys/Groups only, so its
    // payload projection must never be built on the eligibility path, while ClickHouse reads Payload
    // exactly once during CanRender. Lazy materializes the factory at most once; the field is excluded
    // from the record's value semantics (see Equals/GetHashCode below).
    private Lazy<IReadOnlyList<ExtremeRowRenderColumn>>? _payload;

    internal ExtremeRowDescription(
        bool isMax,
        IReadOnlyList<ExtremeRowRenderColumn> keys,
        IReadOnlyList<ExtremeRowRenderColumn> groups,
        IReadOnlyList<ExtremeRowRenderColumn> payload)
        : this(isMax, keys, groups, () => payload)
    {
    }

    internal ExtremeRowDescription(
        bool isMax,
        IReadOnlyList<ExtremeRowRenderColumn> keys,
        IReadOnlyList<ExtremeRowRenderColumn> groups,
        Func<IReadOnlyList<ExtremeRowRenderColumn>> payloadFactory)
    {
        IsMax = isMax;
        Keys = keys;
        Groups = groups;
        _payload = new Lazy<IReadOnlyList<ExtremeRowRenderColumn>>(
            payloadFactory,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary><see langword="true"/> for a maximum (<c>SelectWhereMax</c>), <see langword="false"/> for a minimum.</summary>
    public bool IsMax { get; }

    /// <summary>The extreme-key components in selector order (composite keys have more than one).</summary>
    public IReadOnlyList<ExtremeRowRenderColumn> Keys { get; }

    /// <summary>The group-by components in selector order; empty for the global form.</summary>
    public IReadOnlyList<ExtremeRowRenderColumn> Groups { get; }

    /// <summary>The source payload columns the winning row must expose, in mapping declaration order.</summary>
    public IReadOnlyList<ExtremeRowRenderColumn> Payload => _payload!.Value;

    // The synthesized record equality would compare the memoization field, so a factory-backed
    // description would never equal an eager one even when their exposed values match. Compare the
    // exposed shape instead, with the same default comparers the synthesized members would use.
    /// <inheritdoc/>
    public bool Equals(ExtremeRowDescription? other)
        => other is not null
            && IsMax == other.IsMax
            && EqualityComparer<IReadOnlyList<ExtremeRowRenderColumn>>.Default.Equals(Keys, other.Keys)
            && EqualityComparer<IReadOnlyList<ExtremeRowRenderColumn>>.Default.Equals(Groups, other.Groups)
            && EqualityComparer<IReadOnlyList<ExtremeRowRenderColumn>>.Default.Equals(Payload, other.Payload);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(IsMax);
        hash.Add(Keys, EqualityComparer<IReadOnlyList<ExtremeRowRenderColumn>>.Default);
        hash.Add(Groups, EqualityComparer<IReadOnlyList<ExtremeRowRenderColumn>>.Default);
        hash.Add(Payload, EqualityComparer<IReadOnlyList<ExtremeRowRenderColumn>>.Default);
        return hash.ToHashCode();
    }
}

/// <summary>
/// The already-prepared inputs of one native extreme-row render: the filtered source SQL, the canonical
/// payload aliases the returned source must expose, and the ordered key/group aliases that source
/// exposes for the native construct.
/// </summary>
/// <remarks>
/// This is a read-only input constructed by the core and handed to an external
/// <see cref="IExtremeRowRenderer"/>. Implementing a renderer means receiving instances through
/// <see cref="IExtremeRowRenderer.Render"/>; the constructor is intentionally not part of the public
/// contract, so no external code needs to (or can) construct one.
/// </remarks>
public sealed record ExtremeRowRenderRequest
{
    internal ExtremeRowRenderRequest(
        string sourceSql,
        bool isMax,
        IReadOnlyList<string> payloadAliases,
        IReadOnlyList<string> keyAliases,
        IReadOnlyList<ExtremeRowRenderColumn> keyColumns,
        IReadOnlyList<string> groupAliases,
        KeywordCase keywordCase)
    {
        SourceSql = sourceSql;
        IsMax = isMax;
        PayloadAliases = payloadAliases;
        KeyAliases = keyAliases;
        KeyColumns = keyColumns;
        GroupAliases = groupAliases;
        KeywordCase = keywordCase;
    }

    /// <summary>
    /// The prepared filtered source: <c>select * from &lt;source&gt; where &lt;key-component IS NOT
    /// NULL&gt; [and &lt;condition&gt;]</c>. The renderer wraps it and returns the winning-row source.
    /// </summary>
    public string SourceSql { get; }

    /// <summary><see langword="true"/> for a maximum, <see langword="false"/> for a minimum.</summary>
    public bool IsMax { get; }

    /// <summary>The canonical aliases of the payload columns the returned source must expose, in order.</summary>
    public IReadOnlyList<string> PayloadAliases { get; }

    /// <summary>The aliases of the extreme-key components, in selector order.</summary>
    public IReadOnlyList<string> KeyAliases { get; }

    /// <summary>
    /// The shape facts of the extreme-key components, positionally aligned with
    /// <see cref="KeyAliases"/>. A renderer whose key emission depends on the component type (for
    /// example ClickHouse's floating-key NaN adaptation) reads this instead of re-deriving it.
    /// </summary>
    public IReadOnlyList<ExtremeRowRenderColumn> KeyColumns { get; }

    /// <summary>The aliases of the group-by components, in selector order; empty for the global form.</summary>
    public IReadOnlyList<string> GroupAliases { get; }

    /// <summary>The keyword casing the returned SQL should use.</summary>
    public KeywordCase KeywordCase { get; }
}

/// <summary>
/// A dialect's optional native renderer for <c>SelectWhereMax</c>/<c>SelectWhereMin</c> (PostgreSQL
/// <c>DISTINCT ON</c>/<c>ORDER BY ... LIMIT 1</c>, ClickHouse <c>argMin</c>/<c>argMax</c>). The
/// object's presence is the capability: a dialect that returns <see langword="null"/> keeps the
/// shared portable window-function lowering, as does a renderer whose <see cref="CanRender"/> answers
/// <c>false</c>.
/// </summary>
/// <remarks>
/// <see cref="CanRender"/> runs before the shared path builds any SQL, alias or parameter, so it must
/// be side-effect free; only a positive answer leads to <see cref="Render"/>, whose failure is
/// propagated (there is no late fallback to the portable path).
/// </remarks>
public interface IExtremeRowRenderer
{
    /// <summary>
    /// True when this dialect can natively express the described command. Called with a prepared,
    /// immutable description before any SQL is assembled; must not mutate shared state.
    /// </summary>
    bool CanRender(ExtremeRowDescription description);

    /// <summary>
    /// Renders the native winning-row source over the already-prepared
    /// <see cref="ExtremeRowRenderRequest.SourceSql"/>, exposing the request's
    /// <see cref="ExtremeRowRenderRequest.PayloadAliases"/>. The shared path already applied the
    /// compatibility validation and the source filter, and applies the outer projection, DISTINCT and
    /// output ordering afterwards.
    /// </summary>
    string Render(ExtremeRowRenderRequest request);
}

/// <summary>
/// A dialect's renderer for <c>TABLESAMPLE</c>. The predicate and the renderer live on one object, so a
/// sampling method the dialect reports as supported always has a rendering.
/// </summary>
public interface ITableSampleMethods
{
    /// <summary>True when the sampling <paramref name="method"/> can be rendered.</summary>
    bool Supports(TableSampleMethod method);

    /// <summary>Renders the <c>TABLESAMPLE</c> clause for <paramref name="method"/>.</summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string Render(TableSampleMethod method, double percent, double? seed, KeywordCase keywordCase = KeywordCase.Lower);
}

/// <summary>
/// A dialect's renderer for the native <c>PIVOT</c>/<c>UNPIVOT</c> pair (SQL Server). The object's
/// presence is the capability.
/// </summary>
public interface IPivotRenderer
{
    /// <summary>
    /// Renders the native <c>PIVOT</c> construct over the already-rendered <paramref name="source"/>,
    /// with the already-rendered <paramref name="aggregateColumn"/> and <paramref name="forColumn"/>
    /// fragments and the result <paramref name="alias"/>.
    /// </summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string RenderPivot(PivotExpression pivot, string source, string aggregateColumn, string forColumn, string alias, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// Renders the native <c>UNPIVOT</c> construct over the already-rendered <paramref name="source"/>
    /// and the result <paramref name="alias"/>.
    /// </summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string RenderUnpivot(PivotExpression pivot, string source, string alias, KeywordCase keywordCase = KeywordCase.Lower);
}

/// <summary>
/// A dialect's renderer for the ClickHouse <c>ARRAY JOIN</c> clause. The object's presence is the
/// capability.
/// </summary>
public interface IArrayJoinRenderer
{
    /// <summary>Renders the <c>ARRAY JOIN</c> clause over the already-rendered <paramref name="expressions"/>.</summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string Render(ArrayJoinKind kind, IReadOnlyList<string> expressions, KeywordCase keywordCase = KeywordCase.Lower);
}

/// <summary>
/// A dialect's renderer for the ClickHouse date-conversion surface
/// (<c>toDate</c>/<c>toDateTime</c>/<c>toStartOf*</c>/<c>toYYYYMM</c>/<c>toUnixTimestamp</c>). The
/// object's presence is the capability.
/// </summary>
public interface IDateConversionRenderer
{
    /// <summary>Renders the date-conversion function <paramref name="name"/> over the already-rendered <paramref name="args"/>.</summary>
    string Render(string name, IReadOnlyList<string> args);
}

/// <summary>
/// A dialect's renderer for a scalar split of a string (<c>string.Split</c>; ClickHouse
/// <c>splitByChar</c>). The object's presence is the capability.
/// </summary>
public interface IStringSplitRenderer
{
    /// <summary>Renders the split of <paramref name="value"/> by <paramref name="separator"/>.</summary>
    string Render(string separator, string value);
}

/// <summary>
/// A dialect's renderer for row locking. <see cref="UsesTableHints"/> selects the shape of the token
/// <see cref="Render(LockMode, KeywordCase)"/> returns: a trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> clause, or a bare table
/// hint (the caller wraps it in <c>WITH (...)</c>). The object's presence is the capability, and since
/// one shape is selected by <see cref="UsesTableHints"/> the renderer never has an unreachable method.
/// A dialect that can express <c>NOWAIT</c>/<c>SKIP LOCKED</c> additionally overrides
/// <see cref="Render(LockMode, LockWaitMode, KeywordCase)"/>; the inherited default only supports
/// <see cref="LockWaitMode.Wait"/> and throws <see cref="NotSupportedException"/> for the other modes.
/// </summary>
public interface ILockRenderer
{
    /// <summary>True when the dialect expresses locking as a table hint rather than a trailing clause.</summary>
    bool UsesTableHints { get; }

    /// <summary>
    /// Renders the locking token for <paramref name="mode"/>: the trailing clause when
    /// <see cref="UsesTableHints"/> is <c>false</c>, otherwise the bare table-hint token.
    /// </summary>
    /// <paramref name="keywordCase"/> selects the letter case of the emitted SQL keywords.
    string Render(LockMode mode, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>
    /// Renders the locking token for <paramref name="mode"/> with the requested <paramref name="wait"/>
    /// behaviour for rows already locked by another transaction. The default implementation supports only
    /// <see cref="LockWaitMode.Wait"/> (delegating to <see cref="Render(LockMode, KeywordCase)"/>) and rejects
    /// a non-blocking mode with <see cref="NotSupportedException"/>; a dialect whose provider can express
    /// <c>NOWAIT</c>/<c>SKIP LOCKED</c> overrides it.
    /// </summary>
    /// <param name="mode">The row-locking strength.</param>
    /// <param name="wait">How to react to a row already locked by another transaction.</param>
    /// <param name="keywordCase">Selects the letter case of the emitted SQL keywords.</param>
    string Render(LockMode mode, LockWaitMode wait, KeywordCase keywordCase = KeywordCase.Lower) =>
        wait == LockWaitMode.Wait
            ? Render(mode, keywordCase)
            : throw new NotSupportedException("The locking wait mode is not supported by this SQL dialect.");
}

/// <summary>
/// A dialect's renderer for table index hints (MySQL/MariaDB <c>USE|FORCE|IGNORE INDEX</c>, SQLite
/// <c>INDEXED BY</c>/<c>NOT INDEXED</c>, SQL Server <c>WITH (INDEX(...))</c>). The object's presence
/// is the capability: a dialect that returns <see langword="null"/> rejects a command that carries an
/// index hint.
/// </summary>
public interface IIndexHintRenderer
{
    /// <summary>
    /// True when the hint is rendered as another entry of the table-hint <c>WITH (...)</c> list
    /// (SQL Server) rather than as a standalone clause after the table name.
    /// </summary>
    bool MergesWithTableHints { get; }

    /// <summary>
    /// Renders the index hint for <paramref name="indexes"/> and <paramref name="kind"/>. When
    /// <see cref="MergesWithTableHints"/> is <c>true</c> the returned fragment is the bare table-hint
    /// token (the caller wraps it in <c>WITH (...)</c>); otherwise it is the standalone clause
    /// including its leading separator. Returns <see langword="null"/> when the kind cannot be
    /// expressed (for example <c>FORCE</c> on SQL Server).
    /// </summary>
    string? RenderIndexHint(IReadOnlyList<string> indexes, IndexHintKind kind, KeywordCase keywordCase = KeywordCase.Lower);
}

/// <summary>
/// A dialect's renderer for the cross-provider scalar string/number functions of
/// <see cref="CommonFunctions"/> (<c>left</c>/<c>right</c>, <c>lpad</c>/<c>rpad</c>,
/// <c>repeat</c>/<c>reverse</c>/<c>space</c>, <c>concat_ws</c>, <c>translate</c>, <c>ascii</c>/<c>char</c>,
/// <c>bit_length</c>/<c>octet_length</c>, <c>cot</c>/<c>degrees</c>/<c>radians</c>/<c>pi</c>). The
/// predicate and the renderer live on one object, so a name
/// the dialect reports as supported always has a rendering; a provider that can express only part of
/// the family answers <see cref="Supports"/> per name, and a provider that can express none returns
/// <see langword="null"/> from <see cref="ISqlDialect.ScalarFunctions"/>.
/// </summary>
/// <remarks>
/// Unlike the stateless <see cref="IIifRenderer"/>/<see cref="ILimitByRenderer"/>, this object carries
/// both the per-name predicate and the renderer, hence the plural capability-noun name rather than the
/// <c>*Renderer</c> suffix.
/// </remarks>
public interface IScalarFunctions
{
    /// <summary>True when the scalar function <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>
    /// Renders the scalar function <paramref name="name"/> over the already-rendered
    /// <paramref name="args"/>. <paramref name="name"/> is the CLR method name of the
    /// <see cref="CommonFunctions"/> member (for example <c>lpad</c>, <c>concat_ws</c>).
    /// </summary>
    string Render(string name, IReadOnlyList<string> args);
}

/// <summary>
/// A dialect's renderer for the MySQL/MariaDB-only functions of <see cref="MySqlFunctions"/> (the
/// native string/conditional idioms, the <c>%</c>-templated date conversion and Unix-epoch functions,
/// the hexadecimal hashes, the IPv4 conversion pair, the JSON mutation family and the binary UUID
/// pair). The predicate and the renderer live on one object, so a name the dialect reports as
/// supported always has a rendering; a provider with only part of the family (MariaDB) answers
/// <see cref="Supports"/> per name, and a provider with none returns <see langword="null"/> from
/// <see cref="ISqlDialect.MySqlFunctions"/>.
/// </summary>
/// <remarks>
/// Unlike the stateless <see cref="IIifRenderer"/>/<see cref="ILimitByRenderer"/>, this object carries
/// both the per-name predicate and the renderer, hence the plural capability-noun name rather than the
/// <c>*Renderer</c> suffix.
/// </remarks>
public interface IMySqlFunctions
{
    /// <summary>True when the MySQL/MariaDB function <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>
    /// Renders the MySQL/MariaDB function <paramref name="name"/> over the already-rendered
    /// <paramref name="args"/>. <paramref name="name"/> is the CLR method name of the
    /// <see cref="MySqlFunctions"/> member (for example <c>find_in_set</c>, <c>json_set</c>).
    /// </summary>
    string Render(string name, IReadOnlyList<string> args);
}

/// <summary>
/// A dialect's surface for the SQL Server-only T-SQL scalar functions that have no cross-provider
/// analog (the string functions <c>PATINDEX</c>/<c>QUOTENAME</c>/<c>SOUNDEX</c>/<c>DIFFERENCE</c>/
/// <c>STRING_ESCAPE</c>/<c>UNICODE</c>/<c>NCHAR</c>/<c>FORMAT</c>, the trigonometric functions, the
/// date functions <c>DATENAME</c>/<c>DATE_BUCKET</c> and the clock/offset/<c>*FROMPARTS</c> family,
/// the binary/system functions <c>HASHBYTES</c>/<c>NEWSEQUENTIALID</c>, the
/// <c>CHECKSUM</c>/<c>COMPRESS</c>/<c>RAND</c>/<c>STUFF</c> scalars, the metadata functions
/// (<c>COL_LENGTH</c>/<c>OBJECT_ID</c>/<c>DB_ID</c>/<c>SCHEMA_NAME</c>/<c>FILE_ID</c>/
/// <c>ISDATE</c>/<c>STR</c>/<c>FORMATMESSAGE</c> and the rest of the A–D metadata families) and the
/// SQL/JSON constructors/aggregates/predicates). The
/// predicate and the renderer live on one object, so a name the dialect reports as supported always
/// has a rendering; <see langword="null"/> is the capability being absent, which makes every other
/// provider reject the members with a clear message.
/// </summary>
/// <remarks>
/// Unlike <see cref="IScalarFunctions"/>, this object is provider-specific and is only reached from
/// the <see cref="SqlServerFunctions"/> members; a name the provider cannot express is gated per
/// function rather than by a family flag.
/// </remarks>
public interface ISqlServerFunctions
{
    /// <summary>True when the SQL Server-only function <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>
    /// Renders the SQL Server-only function <paramref name="name"/> over the already-rendered
    /// <paramref name="args"/>. <paramref name="name"/> is the CLR method name of the
    /// <see cref="SqlServerFunctions"/> member (for example <c>patindex</c>, <c>json_object</c>).
    /// </summary>
    string Render(string name, IReadOnlyList<string> args);
}

/// <summary>
/// A dialect's renderer for row values / composite tuples: the constructor
/// (PostgreSQL <c>ROW(a, b)</c>, ClickHouse <c>tuple(a, b)</c>) and, when supported, positional element
/// access on a server-side row (PostgreSQL <c>(row).fN</c>, ClickHouse <c>tupleElement(row, N)</c>).
/// The object's presence is the capability: a dialect that returns <see langword="null"/> rejects the
/// tuple surface.
/// </summary>
public interface ITupleRenderer
{
    /// <summary>Renders a row constructor from its already-rendered field expressions.</summary>
    string RenderConstructor(IReadOnlyList<string> fields);

    /// <summary>
    /// Renders access to the one-based <paramref name="oneBasedIndex"/> element of a server-side row,
    /// or <see langword="null"/> when the dialect has no positional access (MySQL/MariaDB/SQLite). When
    /// <see langword="null"/>, an inline constructor's element access is folded to the argument instead.
    /// </summary>
    string? RenderElement(string row, int oneBasedIndex);
}

/// <summary>
/// A dialect's renderer for the SQLite-only SQL surface exposed through <see cref="SqlFunctions.Sqlite"/>
/// (<see cref="SqliteFunctions"/>): the core scalars (<c>printf</c>/<c>format</c>, <c>hex</c>/<c>unhex</c>,
/// <c>random</c>/<c>randomblob</c>, <c>quote</c>, <c>typeof</c>, <c>glob</c>, <c>unicode</c>/<c>char</c>,
/// <c>soundex</c>, <c>octet_length</c>, <c>if</c>/<c>ifnull</c>), the JSON1 functions/operators/aggregates
/// (<c>json_extract</c>, <c>-&gt;</c>/<c>-&gt;&gt;</c>, <c>json_set</c>, <c>json_group_array</c>, ...), the
/// date functions (<c>timediff</c>, <c>unixepoch</c>, <c>julianday</c>), the math-extension functions
/// (<c>acos</c>, <c>degrees</c>, <c>log2</c>, <c>mod</c>, <c>pi</c>, ...) and the full-text (FTS3/FTS4/FTS5)
/// query surface (<c>Match</c>, <c>Rank</c>, <c>FTS5bm25</c>, <c>Highlight</c>, <c>FTS3Offsets</c>, ...).
/// The predicate and the renderer
/// live on one object, so a name the dialect reports as supported always has a rendering, and a provider
/// that cannot express the surface returns <see langword="null"/> from
/// <see cref="ISqlDialect.SqliteFunctions"/>.
/// </summary>
/// <remarks>
/// Unlike the stateless renderers, this object carries both the per-name predicate and the renderer,
/// hence the plural capability-noun name rather than the <c>*Renderer</c> suffix.
/// </remarks>
public interface ISqliteFunctions
{
    /// <summary>True when the SQLite function <paramref name="name"/> can be rendered.</summary>
    bool Supports(string name);

    /// <summary>
    /// Renders the SQLite function <paramref name="name"/> over the already-rendered
    /// <paramref name="args"/>. <paramref name="name"/> is the CLR method name of the
    /// <see cref="SqliteFunctions"/> member (for example <c>printf</c>, <c>json_extract</c>,
    /// <c>json_get</c>).
    /// </summary>
    string Render(string name, IReadOnlyList<string> args);
}
