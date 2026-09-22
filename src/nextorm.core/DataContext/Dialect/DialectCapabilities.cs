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
    string Render(int limit, int offset, IReadOnlyList<string> columns);
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

    /// <summary>Renders the <c>median</c> aggregate over <paramref name="value"/>.</summary>
    string RenderMedian(string value);
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
    string Render(IReadOnlyList<string> columns);
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
    string Render(TableSampleMethod method, double percent, double? seed);
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
    string RenderPivot(PivotExpression pivot, string source, string aggregateColumn, string forColumn, string alias);

    /// <summary>
    /// Renders the native <c>UNPIVOT</c> construct over the already-rendered <paramref name="source"/>
    /// and the result <paramref name="alias"/>.
    /// </summary>
    string RenderUnpivot(PivotExpression pivot, string source, string alias);
}

/// <summary>
/// A dialect's renderer for the ClickHouse <c>ARRAY JOIN</c> clause. The object's presence is the
/// capability.
/// </summary>
public interface IArrayJoinRenderer
{
    /// <summary>Renders the <c>ARRAY JOIN</c> clause over the already-rendered <paramref name="expressions"/>.</summary>
    string Render(ArrayJoinKind kind, IReadOnlyList<string> expressions);
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
/// <see cref="Render"/> returns: a trailing <c>FOR UPDATE</c>/<c>FOR SHARE</c> clause, or a bare table
/// hint (the caller wraps it in <c>WITH (...)</c>). The object's presence is the capability, and since
/// one shape is selected by <see cref="UsesTableHints"/> the renderer never has an unreachable method.
/// </summary>
public interface ILockRenderer
{
    /// <summary>True when the dialect expresses locking as a table hint rather than a trailing clause.</summary>
    bool UsesTableHints { get; }

    /// <summary>
    /// Renders the locking token for <paramref name="mode"/>: the trailing clause when
    /// <see cref="UsesTableHints"/> is <c>false</c>, otherwise the bare table-hint token.
    /// </summary>
    string Render(LockMode mode);
}
