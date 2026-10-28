using System.Collections.Frozen;
using System.Text;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// ClickHouse dialect: backtick-quoted identifiers, <c>@name</c> parameters, <c>concat(...)</c>
/// concatenation, <c>limit/offset</c> paging, ClickHouse type names and the camel-case
/// standard-deviation/variance aggregate names. ClickHouse has no recursive CTE support, so the
/// <c>recursive</c> modifier is omitted.
/// </summary>
/// <remarks>
/// The type is deliberately not <see langword="sealed"/>. Subclassing it and overriding
/// <see cref="ExtremeRowRenderer"/> is the supported external extension boundary (a public
/// compatibility commitment), so an assembly without <c>InternalsVisibleTo</c> can install its own
/// public <see cref="IExtremeRowRenderer"/>. The built-in renderer is <c>internal</c> and is not part
/// of that contract. The public constructor and the shared <see cref="Instance"/> are preserved;
/// returning <see langword="null"/> from an override keeps the portable window-function lowering.
/// </remarks>
public class ClickHouseDialect : SqlDialectBase
{
    /// <summary>
    /// The shared ClickHouse dialect instance. Subclassing the dialect does not change this instance;
    /// a derived dialect is created through the public constructor instead.
    /// </summary>
    public static readonly ClickHouseDialect Instance = new();

    private readonly IMultiIfRenderer _multiIf;
    private readonly IUniqAggregateRenderer _uniqAggregates;
    private readonly IQuantileAggregateRenderer _quantileAggregates;
    private readonly ITopKAggregateRenderer _topKAggregates;
    private readonly IClickHouseAggregateRenderer _clickHouseAggregates;
    private readonly IScalarFunctions _scalarFunctions;
    private readonly Version? _serverVersion;

    /// <summary>
    /// Creates the dialect and its capability renderers. The constructor is public so an external
    /// assembly can derive a custom dialect from <see cref="ClickHouseDialect"/>.
    /// </summary>
    public ClickHouseDialect()
        : this(VersionDeclaration.Undeclared)
    {
    }

    /// <summary>
    /// Creates a dialect bound to an explicitly declared ClickHouse server version. The declaration is
    /// caller-supplied (there is no server probing): it only widens the capability set for the gates
    /// whose native function appeared in a later ClickHouse release (currently <c>arrayExcept</c> from
    /// 25.9). An undeclared dialect keeps the pinned-baseline surface, i.e. the shared
    /// <see cref="Instance"/> renderers.
    /// </summary>
    /// <param name="serverVersion">The ClickHouse server version this dialect declares.</param>
    /// <exception cref="ArgumentNullException"><paramref name="serverVersion"/> is <see langword="null"/>; use the parameterless constructor for an undeclared dialect.</exception>
    public ClickHouseDialect(Version serverVersion)
        : this(VersionDeclaration.Of(serverVersion))
    {
    }

    private ClickHouseDialect(VersionDeclaration declaration)
    {
        _multiIf = new ClickHouseMultiIfRenderer(this);
        _uniqAggregates = new ClickHouseUniqAggregateRenderer(this);
        _quantileAggregates = new ClickHouseQuantileAggregateRenderer(this);
        _topKAggregates = new ClickHouseTopKAggregateRenderer(this);
        _clickHouseAggregates = new ClickHouseAggregateRenderer(this);
        _serverVersion = declaration.ServerVersion;
        _scalarFunctions = _serverVersion is null
            ? ClickHouseScalarFunctions.Instance
            : new ClickHouseScalarFunctions(_serverVersion);
    }

    /// <summary>
    /// Distinguishes the undeclared constructor from the versioned one: a reference-type nullable
    /// annotation cannot be an overload, so the two public constructors meet here through a wrapper.
    /// </summary>
    private readonly record struct VersionDeclaration(Version? ServerVersion)
    {
        public static VersionDeclaration Undeclared => default;

        public static VersionDeclaration Of(Version serverVersion)
        {
            ArgumentNullException.ThrowIfNull(serverVersion);
            return new VersionDeclaration(serverVersion);
        }
    }

    /// <summary>The declared server version, or <see langword="null"/> for the undeclared (pinned-baseline) dialect.</summary>
    internal Version? DeclaredVersion => _serverVersion;

    /// <summary>
    /// True when the declared server version provides the native <c>arrayExcept</c> function, introduced
    /// in ClickHouse 25.9. Always <see langword="false"/> for an undeclared dialect.
    /// </summary>
    internal bool SupportsClickHouse25_9Functions => ClickHouseVersionFacts.Supports(_serverVersion, 25, 9);

    /// <summary>A ClickHouse derived table (subquery in FROM) must have an alias.</summary>
    public override bool RequireSubqueryAlias => true;

    /// <summary>ClickHouse has no recursive <c>WITH</c>, so the new typed recursive CTE API fails closed.</summary>
    public override bool SupportsRecursiveCte => false;

    /// <summary>ClickHouse supports a raw SQL derived table (<c>FROM (&lt;sql&gt;) AS alias</c>).</summary>
    public override bool SupportsRawSqlSource => true;

    /// <inheritdoc/>
    public override bool SupportsRangeColumns => true;

    /// <summary>ClickHouse renders the native <c>NULLS FIRST</c>/<c>NULLS LAST</c> ordering modifier.</summary>
    public override bool SupportsNullsOrdering => true;

    /// <inheritdoc/>
    public override string RenderOrderBy(string expressionSql, OrderDirection direction, NullsPosition nullsPosition)
        => NullsOrderingRenderer.RenderNative(expressionSql, direction, nullsPosition);

    /// <summary>
    /// ClickHouse emulates a table-valued parameter with a native array bound through the driver and
    /// expanded server-side with <c>arrayJoin(@p)</c>: a scalar set is an <c>Array(T)</c> and an entity
    /// set an <c>Array(Tuple(...))</c>. See <c>ClickHouseDataContext.CreateProcedureParameter</c>.
    /// </summary>
    public override bool SupportsTableValuedParameters => true;

    /// <summary>
    /// ClickHouse has a native bulk path: the driver's <c>ClickHouseClient.InsertBinaryAsync</c> streams
    /// RowBinary rows over HTTP, so the native path is selected where a ClickHouse client is available and
    /// the portable <c>INSERT ... VALUES</c> path stays the compatible fallback. The legacy
    /// <c>ClickHouseBulkCopy</c> wrapper is not used.
    /// </summary>
    public override bool SupportsBulkCopy => true;

    /// <summary>
    /// ClickHouse has no unique constraint and no <c>INSERT IGNORE</c> form, so a duplicate-skipping
    /// request cannot be expressed. Capability is reported as <see langword="false"/> so the request is
    /// refused by name instead of being silently rendered as a plain <c>INSERT INTO</c> that writes every
    /// row.
    /// </summary>
    public override bool SupportsInsertIgnore => false;

    /// <summary>ClickHouse deletes through the synchronous <c>ALTER TABLE ... DELETE</c> mutation with <c>SETTINGS mutations_sync = 1</c>.</summary>
    public override string MakeDeleteHead(string table, KeywordCase keywordCase = KeywordCase.Lower) => $"{Kw(keywordCase, "alter table ")}{table}{Kw(keywordCase, " delete")}";

    /// <summary>ClickHouse's <c>ALTER TABLE ... DELETE</c> mutation requires a <c>WHERE</c> clause.</summary>
    public override bool DeleteRequiresWhere => true;

    /// <summary>Waits for the ClickHouse mutation, so a delete is visible to the next statement.</summary>
    public override string? MakeDeleteSuffix(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " settings mutations_sync = 1");

    /// <summary>ClickHouse renders the <c>ALTER TABLE ... UPDATE</c> mutation head instead of the ANSI <c>UPDATE ... SET</c>.</summary>
    public override string MakeUpdateHead(string table, KeywordCase keywordCase = KeywordCase.Lower) => $"{Kw(keywordCase, "alter table ")}{table}{Kw(keywordCase, " update ")}";

    /// <summary>ClickHouse's <c>ALTER TABLE ... UPDATE</c> mutation requires a <c>WHERE</c> clause.</summary>
    public override bool UpdateRequiresWhere => true;

    /// <summary>Waits for the ClickHouse mutation, so an update is visible to the next statement.</summary>
    public override string? MakeUpdateSuffix(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " settings mutations_sync = 1");

    /// <summary>ClickHouse has a native synchronous <c>TRUNCATE TABLE</c>.</summary>
    public override bool SupportsTruncate => true;

    /// <summary>ClickHouse implements the <c>INTERSECT ALL</c>/<c>EXCEPT ALL</c> set-operation variants.</summary>
    public override bool SupportsIntersectExceptAll => true;

    /// <summary>ClickHouse materialises a query into a persistent table with <c>CREATE TABLE ... ENGINE = ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelect => true;

    /// <summary>ClickHouse accepts <c>IF NOT EXISTS</c> on <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public override bool SupportsCreateTableAsSelectIfNotExists => true;

    /// <summary>ClickHouse temporary tables take an explicit column list and no <c>AS SELECT</c>, so only a persistent <c>ToTable</c> is supported.</summary>
    public override bool SupportsTemporaryCreateTableAsSelect => false;

    /// <summary>Renders the ClickHouse <c>CREATE TABLE ... ENGINE = MergeTree ORDER BY tuple() AS SELECT</c> form (an explicit engine is required).</summary>
    public override string MakeCreateTableAsSelect(CreateTableAsClause clause, string selectSql, KeywordCase keywordCase = KeywordCase.Lower)
        => MakeCreateTableAsHead(clause, keywordCase)
            + Kw(keywordCase, " engine = ")
            + "MergeTree"
            + Kw(keywordCase, " order by tuple() as ")
            + selectSql;

    /// <summary>ClickHouse concatenates with the <c>concat(part, ...)</c> function rather than the <c>+</c> operator.</summary>
    public override string MakeConcat(IReadOnlyList<string> parts) => $"concat({string.Join(", ", parts)})";

    /// <summary>ClickHouse implements the <c>ANY</c>/<c>ALL</c>/<c>ASOF</c> join modifiers.</summary>
    public override bool SupportsJoinStrictness => true;

    /// <summary>ClickHouse implements the <c>GLOBAL</c> join modifier.</summary>
    public override bool SupportsGlobalJoin => true;

    /// <summary>ClickHouse implements the <c>LEFT SEMI</c>/<c>LEFT ANTI</c> join kinds.</summary>
    public override bool SupportsSemiAntiJoin => true;

    /// <summary>ClickHouse implements the <c>PASTE</c> join kind.</summary>
    public override bool SupportsPasteJoin => true;

    /// <summary>
    /// ClickHouse has a native <c>Array(T)</c> type and implements the array functions over array
    /// columns (<c>length</c>, <c>has</c>, <c>indexOf</c>, <c>arrayStringConcat</c>, <c>hasAny</c>/<c>hasAll</c>,
    /// <c>startsWith</c>/<c>endsWith</c>/<c>hasSubstr</c>, <c>arraySort</c>, <c>arrayReverse</c>, <c>arrayDistinct</c>,
    /// <c>splitByChar</c>).
    /// </summary>
    public override bool SupportsArrayFunctions => true;

    /// <summary>
    /// ClickHouse implements the higher-order (lambda) array functions (<c>arrayMap</c>,
    /// <c>arrayFilter</c>, <c>arrayExists</c>, <c>arrayAll</c>, <c>arrayCount</c>,
    /// <c>arrayFirst*</c>, <c>arrayLast*</c>).
    /// </summary>
    public override bool SupportsHigherOrderArrayFunctions => true;

    /// <summary>
    /// ClickHouse has a native <c>Tuple(...)</c> type and implements the <c>tuple</c> constructor and
    /// <c>tupleElement</c> access.
    /// </summary>
    public override ITupleRenderer? Tuple => ClickHouseTupleRenderer.Instance;

    /// <summary>ClickHouse implements <c>arrayJoin(array)</c>, which expands one row per element.</summary>
    public override bool SupportsArrayJoin => true;

    /// <summary>ClickHouse renders <c>string.Split</c> as <c>splitByChar(separator, value)</c>.</summary>
    public override IStringSplitRenderer StringSplit => ClickHouseStringSplitRenderer.Instance;

    /// <summary>
    /// <c>length</c>, <c>indexOf</c>, <c>arrayCount</c>, <c>arrayFirstIndex</c> and
    /// <c>arrayLastIndex</c> return <c>UInt64</c>/<c>UInt32</c> natively; cast them to <c>Int64</c> so
    /// the result matches the declared CLR <c>long</c>.
    /// </summary>
    public override string MakeArrayFunction(string name, string call) =>
        name is "length" or "indexOf" or "arrayCount" or "arrayFirstIndex" or "arrayLastIndex" or "countEqual"
            ? $"toInt64({call})"
            : call;

    /// <summary>
    /// Normalises the result of the array-producing facades to the closed declared CLR array type.
    /// ClickHouse infers the element type from the arguments and the server defaults
    /// (<c>retention</c> → <c>Array(UInt8)</c>, <c>range(3)</c> → <c>Array(UInt8)</c>, an <c>arrayMap</c>
    /// or <c>top_k</c> whose native element type is wider than the declared one → <c>Array(Int64)</c>), so
    /// a targeted result is wrapped in exactly one <c>cast(&lt;call&gt; as Array(...))</c>. Every other
    /// facade result is returned unchanged.
    /// </summary>
    public override string MakeArrayResult(string name, string call, Type resultType)
    {
        if (name is not ("top_k" or "top_k_weighted" or "group_array" or "group_uniq_array" or "retention"
                or "array_map" or "range" or "array_enumerate_dense" or "array_difference"
                or "array_fill" or "array_reverse_fill" or "array_top_k" or "array_bottom_k"
                or "array_random_sample" or "array_compact" or "array_pop_back" or "array_pop_front"
                or "array_push_front" or "array_resize" or "array_remove" or "array_rotate_left"
                or "array_rotate_right" or "array_shift_left" or "array_shift_right"
                or "array_reverse_sort" or "array_partial_sort" or "array_with_constant"
                or "array_split" or "array_reverse_split" or "array_shingles" or "array_transpose"
                or "array_cum_sum_non_negative"))
            return call;

        if (!resultType.IsArray || resultType.GetArrayRank() != 1)
            throw new NotSupportedException(
                $"The {name} result type '{resultType}' must be a one-dimensional CLR array.");

        return $"cast({call} as Array({MakeArrayElementType(resultType.GetElementType()!)}))";
    }

    // Nullable value elements keep their CLR nullability. A reference element's nullable
    // annotation is erased at runtime, so the CLR says nothing about whether its entries may be
    // null: every reference element is declared Nullable(...) unconditionally. This intentionally
    // changes the emitted SQL text for existing string-array results, not the data, the CLR result
    // type or the public API. The array arm runs first, so a jagged reference element becomes
    // Array(Array(Nullable(String))) and never Nullable(Array(...)).
    private string MakeArrayElementType(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { IsValueType: true } underlying)
            return $"Nullable({MakeTypeName(underlying)})";

        if (type.IsArray)
            return $"Array({MakeArrayElementType(type.GetElementType()!)})";

        return type.IsValueType ? MakeTypeName(type) : $"Nullable({MakeTypeName(type)})";
    }

    /// <summary>
    /// Renders <c>[global] [inner|left|right|full|cross] [any|all|asof] join</c>. ClickHouse places the
    /// <c>GLOBAL</c> modifier first and the strictness after the join type, before <c>join</c>. The
    /// <c>LEFT SEMI</c>/<c>LEFT ANTI</c>/<c>PASTE</c> kinds render their own keyword.
    /// </summary>
    public override string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal, KeywordCase keywordCase = KeywordCase.Lower)
    {
        if (joinType is JoinType.Semi or JoinType.Anti or JoinType.Paste)
        {
            var kind = joinType switch
            {
                JoinType.Semi => "left semi",
                JoinType.Anti => "left anti",
                JoinType.Paste => "paste",
                _ => throw new NotSupportedException($"The {joinType} join is not supported")
            };

            return Kw(keywordCase, (isGlobal ? " global" : "") + " " + kind + " join ");
        }

        var type = joinType switch
        {
            JoinType.Inner => "",
            JoinType.Left => " left",
            JoinType.Right => " right",
            JoinType.Full => " full",
            JoinType.Cross => " cross",
            JoinType.FullCross => " cross",
            _ => throw new NotSupportedException($"The join modifier cannot be applied to a {joinType} join")
        };

        var modifier = strictness switch
        {
            JoinStrictness.Default => "",
            JoinStrictness.Any => " any",
            JoinStrictness.All => " all",
            JoinStrictness.Asof => " asof",
            _ => throw new NotSupportedException($"The {strictness} join modifier is not supported")
        };

        return Kw(keywordCase, (isGlobal ? " global" : "") + type + modifier + " join ");
    }

    // ClickHouse renders greatest(...)/least(...).
    /// <summary>ClickHouse renders the standard <c>greatest(...)</c>/<c>least(...)</c> functions.</summary>
    public override bool SupportsGreatestLeast => true;

    /// <summary>ClickHouse renders the portable <c>iif</c> as <c>if(condition, whenTrue, whenFalse)</c>.</summary>
    public override IIifRenderer Iif => ClickHouseIifRenderer.Instance;

    /// <summary>ClickHouse implements the multi-branch conditional <c>multiIf(cond1, then1, ..., else)</c>.</summary>
    public override IMultiIfRenderer MultiIf => _multiIf;

    // ClickHouse has dateTrunc(unit, datetime) and the addYears/.../addSeconds family plus
    // toLastDayOfMonth(date).
    /// <summary>ClickHouse implements date truncation through <c>dateTrunc(unit, value)</c>.</summary>
    public override bool SupportsDateTrunc => true;
    /// <summary>ClickHouse implements date arithmetic through its <c>addYears</c>/.../<c>addSeconds</c> family.</summary>
    public override bool SupportsDateArithmetic => true;

    /// <summary>
    /// ClickHouse exposes the native date conversion/truncation surface (<c>toDate</c>/<c>toDateTime</c>/
    /// <c>toDate32</c>, the <c>toYear</c>/... part accessors, <c>toStartOf*</c>, <c>toMonday</c>,
    /// <c>toYYYYMM</c>/<c>toYYYYMMDD</c>, <c>toUnixTimestamp</c>).
    /// </summary>
    public override IDateConversionRenderer DateConversion => ClickHouseDateConversionRenderer.Instance;

    /// <summary>Accepts the date-trunc fields except <c>decade</c>, <c>century</c> and <c>millennium</c>, which ClickHouse <c>dateTrunc</c> lacks.</summary>
    public override bool SupportsDateTruncField(string field) =>
        field is not ("decade" or "century" or "millennium") && base.SupportsDateTruncField(field);

    // string_agg is rendered as arrayStringConcat(groupArray(value), delimiter). ClickHouse has no
    // array type usable by the row reader, so array_agg stays unavailable.
    /// <summary>ClickHouse renders <c>string_agg</c> through <c>arrayStringConcat(groupArray(value), delimiter)</c>; <c>array_agg</c> stays unavailable.</summary>
    public override bool SupportsStringAgg => true;

    // ClickHouse renders the bit aggregates as groupBitAnd/groupBitOr/groupBitXor, the statistical
    // aggregates as corr/covarPop/covarSamp and the filtered aggregates as the -If combinators.
    /// <summary>ClickHouse implements the bit aggregates as <c>groupBitAnd</c>/<c>groupBitOr</c>/<c>groupBitXor</c>.</summary>
    public override bool SupportsBitAggregates => true;
    /// <summary>ClickHouse implements the statistical aggregates as <c>corr</c>/<c>covarPop</c>/<c>covarSamp</c>.</summary>
    public override bool SupportsStatisticalAggregates => true;
    /// <summary>ClickHouse implements the <c>argMin</c>/<c>argMax</c> aggregates.</summary>
    public override bool SupportsArgMinMax => true;
    /// <summary>ClickHouse renders an aggregate filter as the <c>-If</c> combinator (<c>sumIf</c>, <c>countIf</c>).</summary>
    public override AggregateFilterStyle AggregateFilterStyle => AggregateFilterStyle.IfCombinator;
    /// <summary>ClickHouse implements the distinct-count <c>uniq*</c> aggregates.</summary>
    public override IUniqAggregateRenderer UniqAggregates => _uniqAggregates;

    /// <summary>ClickHouse implements the parameterised <c>quantile(level)(value)</c> family and <c>median</c>.</summary>
    public override IQuantileAggregateRenderer QuantileAggregates => _quantileAggregates;

    /// <summary>ClickHouse implements the parameterised <c>topK(N)(value)</c>/<c>topKWeighted(N)(value, weight)</c> aggregates.</summary>
    public override ITopKAggregateRenderer TopKAggregates => _topKAggregates;

    /// <summary>ClickHouse implements the ClickHouse-only aggregate catalogue (statistical, quantile-algorithm, array/tuple and time-series aggregates).</summary>
    public override IClickHouseAggregateRenderer ClickHouseAggregates => _clickHouseAggregates;

    /// <summary>ClickHouse implements the <c>any</c>/<c>anyLast</c> row-picking aggregates.</summary>
    public override bool SupportsAnyAggregates => true;

    /// <summary>ClickHouse renders the arbitrary-value aggregate as <c>any(x)</c>.</summary>
    public override bool SupportsAnyValueAggregate => true;

    /// <summary>ClickHouse implements the <c>percent_rank</c>/<c>cume_dist</c> window functions.</summary>
    public override bool SupportsPercentRankCumeDist => true;

    /// <summary>ClickHouse implements the <c>nth_value(value, n)</c> window function.</summary>
    public override bool SupportsNthValue => true;

    /// <summary>
    /// ClickHouse implements the frame-respecting offset window functions <c>lagInFrame</c>/<c>leadInFrame</c>
    /// (unlike the standard <c>lag</c>/<c>lead</c>, which ignore the window frame).
    /// </summary>
    public override bool SupportsInFrameWindowFunctions => true;

    /// <summary>ClickHouse declares named windows (<c>WINDOW w AS (...)</c>) and references them with <c>OVER w</c>.</summary>
    public override bool SupportsNamedWindows => true;

    /// <summary>ClickHouse 21+ renders <c>SelectWhereMax</c>/<c>SelectWhereMin</c> with a window function.</summary>
    public override bool SupportsSelectWhereMinMax => true;

    /// <summary>
    /// ClickHouse renders eligible <c>SelectWhereMax</c>/<c>SelectWhereMin</c> with <c>argMin</c>/<c>argMax</c> over a payload tuple; the rest falls back to the portable window function.
    /// The property is <see langword="virtual"/> so a derived dialect can override it with a custom
    /// <see cref="IExtremeRowRenderer"/>; returning <see langword="null"/> keeps the portable
    /// window-function lowering. The built-in renderer is <c>internal</c> and is not a public contract.
    /// </summary>
    public override IExtremeRowRenderer? ExtremeRowRenderer => ClickHouseExtremeRowRenderer.Instance;

    /// <summary>ClickHouse supports the <c>GROUPS</c> window frame unit.</summary>
    public override bool SupportsWindowFrameGroups => true;

    /// <summary>
    /// ClickHouse emulates both null-safe comparison forms over guarded ordinary equality: its native
    /// <c>isNotDistinctFrom</c>/<c>&lt;=&gt;</c> spellings are restricted to the JOIN ON section (Code 48
    /// outside it), so they are never emitted in a predicate or scalar position.
    /// </summary>
    public override INullSafeComparisonRenderer? NullSafeComparison => ClickHouseNullSafeComparisonRenderer.Instance;

    /// <summary>
    /// ClickHouse has a native <c>JSON</c> type and stores a mapped JSON column as it. This is the
    /// storage capability only: the PostgreSQL JSON functions/operators surface stays opt-out, because
    /// ClickHouse has no <c>json_agg</c>/<c>-&gt;</c>/<c>@&gt;</c> and its own JSON surface is
    /// <see cref="SupportsJsonExtract"/>.
    /// </summary>
    public override bool SupportsJson => true;

    /// <summary>
    /// ClickHouse materializes and binds its native <c>JSON</c> type as a
    /// <see cref="System.Text.Json.Nodes.JsonObject"/> (the driver's object-root DOM).
    /// </summary>
    public override Type NativeJsonProviderType => typeof(System.Text.Json.Nodes.JsonObject);

    /// <summary>
    /// ClickHouse implements the <c>JSONExtract*</c>/<c>JSONHas</c>/<c>visitParamExtract*</c> string-JSON
    /// family and the JSONPath scalars <c>JSON_VALUE</c>/<c>JSON_QUERY</c>/<c>JSON_EXISTS</c>.
    /// </summary>
    public override bool SupportsJsonExtract => true;

    /// <summary>
    /// The JSON extractors whose vendor result is a value type but which return <c>NULL</c> for a
    /// <c>NULL</c> argument; their rendered call is wrapped in <c>toNullable</c> so the column type
    /// matches the declared nullable CLR result.
    /// </summary>
    private static readonly HashSet<string> JsonNullableExtractNames = new(StringComparer.Ordinal)
    {
        "json_extract_uint", "json_key", "is_valid_json",
        "simple_json_extract_raw", "simple_json_extract_string", "simple_json_extract_int",
        "simple_json_extract_uint", "simple_json_extract_float", "simple_json_extract_bool"
    };

    /// <summary>
    /// ClickHouse spells the string-JSON extractors in camel case and the native-JSON functions as
    /// <c>JSONAllPaths</c>/<c>JSONAllPathsWithTypes</c>/<c>toJSONString</c>; <c>JSONLength</c> and
    /// <c>JSONArrayLength</c> return <c>UInt64</c>/<c>Nullable(UInt64)</c>, which the row reader cannot
    /// materialise, so they are cast to <c>Int64</c>/<c>Nullable(Int64)</c>. The extractors whose
    /// vendor result is a value type but which return <c>NULL</c> for a <c>NULL</c> argument
    /// (<c>JSONExtractUInt</c>, <c>JSONKey</c>, <c>isValidJSON</c> and the <c>simpleJSONExtract*</c>
    /// family) are wrapped in <c>toNullable</c>, so the declared nullable CLR result is a real
    /// <c>Nullable(T)</c> column and a SQL <c>NULL</c> never has to masquerade as the vendor default.
    /// </summary>
    public override string MakeJsonExtract(string name, IReadOnlyList<string> args)
    {
        var function = name switch
        {
            "json_extract_string" => "JSONExtractString",
            "json_extract_int" => "JSONExtractInt",
            "json_extract_float" => "JSONExtractFloat",
            "json_extract_bool" => "JSONExtractBool",
            "json_extract_raw" => "JSONExtractRaw",
            "json_extract_keys" => "JSONExtractKeys",
            "json_extract_array_raw" => "JSONExtractArrayRaw",
            "json_extract_keys_and_values" => "JSONExtractKeysAndValues",
            "json_has" => "JSONHas",
            "json_length" => "JSONLength",
            "json_type" => "JSONType",
            "visit_param_extract_string" => "visitParamExtractString",
            "visit_param_extract_int" => "visitParamExtractInt",
            "visit_param_extract_float" => "visitParamExtractFloat",
            "visit_param_extract_bool" => "visitParamExtractBool",
            "visit_param_extract_raw" => "visitParamExtractRaw",
            "json_value" => "JSON_VALUE",
            "json_query" => "JSON_QUERY",
            "json_exists" => "JSON_EXISTS",
            "json_all_paths" => "JSONAllPaths",
            "json_all_paths_with_types" => "JSONAllPathsWithTypes",
            "to_json_string" => "toJSONString",
            "json_merge_patch" => "JSONMergePatch",
            "json_array_length" => "JSONArrayLength",
            "json_dynamic_paths" => "JSONDynamicPaths",
            "json_dynamic_paths_with_types" => "JSONDynamicPathsWithTypes",
            "json_shared_data_paths" => "JSONSharedDataPaths",
            "json_shared_data_paths_with_types" => "JSONSharedDataPathsWithTypes",
            "json_key" => "JSONKey",
            "json_extract_keys_and_values_raw" => "JSONExtractKeysAndValuesRaw",
            "json_extract_uint" => "JSONExtractUInt",
            "is_valid_json" => "isValidJSON",
            "simple_json_extract_raw" => "simpleJSONExtractRaw",
            "simple_json_extract_string" => "simpleJSONExtractString",
            "simple_json_extract_int" => "simpleJSONExtractInt",
            "simple_json_extract_uint" => "simpleJSONExtractUInt",
            "simple_json_extract_float" => "simpleJSONExtractFloat",
            "simple_json_extract_bool" => "simpleJSONExtractBool",
            _ => name
        };

        var call = $"{function}({string.Join(", ", args)})";

        if (name is "json_length" or "json_array_length")
            return $"toInt64({call})";

        return JsonNullableExtractNames.Contains(name) ? $"toNullable({call})" : call;
    }

    /// <summary>
    /// ClickHouse renders <c>currentUser()</c>, <c>currentDatabase()</c> and <c>version()</c>; it has no
    /// session-user or schema concept, so <c>session_user</c>/<c>current_schema</c> stay unavailable.
    /// </summary>
    public override ISessionInfoFunctions SessionInfoFunctions => ClickHouseSessionInfoFunctions.Instance;

    /// <summary>ClickHouse renders both UUID generators.</summary>
    public override IUuidGenerators UuidGenerators => ClickHouseUuidGenerators.Instance;

    /// <summary>ClickHouse renders every cross-provider scalar function (UTF-8 character-wise where it matters).</summary>
    public override IScalarFunctions ScalarFunctions => _scalarFunctions;

    /// <summary>
    /// ClickHouse implements the dictionary functions <c>dictGet</c>/<c>dictGetOrDefault</c>/
    /// <c>dictGetOrNull</c>/<c>dictGetAll</c>/<c>dictGetDescendants</c>/<c>dictHas</c>/
    /// <c>dictGetHierarchy</c>/<c>dictGetChildren</c>/<c>dictIsIn</c> and the embedded-geobase region
    /// functions <c>regionToName</c>/<c>regionToCity</c>/<c>regionToArea</c>/<c>regionToDistrict</c>/
    /// <c>regionToCountry</c>/<c>regionToContinent</c>/<c>regionToTopContinent</c>/
    /// <c>regionToPopulation</c>/<c>regionHierarchy</c>/<c>regionIn</c>.
    /// </summary>
    public override bool SupportsDictionaries => true;

    /// <summary>ClickHouse spells the dictionary and region functions in camel case.</summary>
    public override string MakeDictionaryFunction(string name, IReadOnlyList<string> args)
    {
        var function = name switch
        {
            "dict_get" => "dictGet",
            "dict_get_or_default" => "dictGetOrDefault",
            "dict_get_or_null" => "dictGetOrNull",
            "dict_get_all" => "dictGetAll",
            "dict_get_descendants" => "dictGetDescendants",
            "dict_has" => "dictHas",
            "dict_get_hierarchy" => "dictGetHierarchy",
            "dict_get_children" => "dictGetChildren",
            "dict_is_in" => "dictIsIn",
            "region_to_name" => "regionToName",
            "region_to_city" => "regionToCity",
            "region_to_area" => "regionToArea",
            "region_to_district" => "regionToDistrict",
            "region_to_country" => "regionToCountry",
            "region_to_continent" => "regionToContinent",
            "region_to_top_continent" => "regionToTopContinent",
            "region_to_population" => "regionToPopulation",
            "region_hierarchy" => "regionHierarchy",
            "region_in" => "regionIn",
            _ => name
        };

        return $"{function}({string.Join(", ", args)})";
    }

    // count()/countIf() also return UInt64; cast to the CLR type the function declares (int vs long).
    /// <summary>ClickHouse's <c>count</c>/<c>countIf</c> return <c>UInt64</c>, so the result is cast to the declared CLR type.</summary>
    public override bool WrapsCountResult => true;

    /// <summary>Casts the count expression to <c>Int64</c> for the large form and <c>Int32</c> otherwise.</summary>
    public override string WrapCount(string countExpression, bool big) =>
        big ? $"toInt64({countExpression})" : $"toInt32({countExpression})";

    // ClickHouse avg() over an empty or all-NULL input is nan, not NULL (unlike the SQL standard). The
    // row-eligibility check (count/countIf over the same argument) restores NULL without inspecting the
    // result value, so a genuine NaN or infinity from non-empty data is preserved unchanged.
    /// <summary>ClickHouse <c>avg</c> returns <c>nan</c> for an empty input, so the AVG result is wrapped in an eligibility check.</summary>
    public override bool WrapsAverageResult => true;

    /// <summary>
    /// Wraps a rendered AVG expression as <c>if(count(...)=0, cast(null as Nullable(Float64)), agg)</c>;
    /// the filtered form checks <c>countIf(isNotNull(x) AND predicate)</c> and the window form uses the
    /// identical <c>over</c> clause for the count.
    /// </summary>
    public override string MakeAverageResult(string aggregate, string argument, string? filterPredicate = null, string? windowClause = null)
    {
        var eligibility = windowClause is not null
            ? $"count({argument}){windowClause}"
            : filterPredicate is not null
                ? $"countIf(isNotNull({argument}) AND {filterPredicate})"
                : $"count({argument})";

        return $"if({eligibility}=0, cast(null as Nullable(Float64)), {aggregate})";
    }

    // ClickHouse's ranking window functions (row_number/rank/dense_rank/ntile) return UInt64, which the
    // row reader cannot materialise into the int the function declares (WindowFunction<int>), so the
    // rendered window is narrowed to Int32 (the many-to-many JoinInto link occurrence reads it as int).
    /// <summary>Casts a ranking window-function expression to <c>Int32</c> so it maps to the declared <see cref="int"/>.</summary>
    public override string WrapIntegerWindowResult(string windowExpression) => $"toInt32({windowExpression})";

    /// <summary>ClickHouse implements the <c>windowFunnel</c>/<c>retention</c>/<c>sequenceMatch</c> aggregates.</summary>
    public override ISequenceAggregateRenderer SequenceAggregates => ClickHouseSequenceAggregateRenderer.Instance;

    // The ClickHouse driver turns CommandBehavior.SingleRow into a trailing LIMIT 1, which would
    // duplicate the limit the dialect already renders for single-row commands.
    /// <summary>ClickHouse is <c>false</c> because the driver would append a duplicate <c>LIMIT 1</c> to the one the dialect already renders.</summary>
    public override bool SupportsCommandBehaviorSingleRow => false;

    // ClickHouse speaks HTTP and has no ADO.NET transaction in the driver (BeginDbTransaction throws).
    /// <summary>ClickHouse is <c>false</c> because its HTTP protocol has no ADO.NET transaction.</summary>
    public override bool SupportsTransactions => false;

    // ClickHouse spells the super-aggregate as a trailing modifier (GROUP BY a, b WITH ROLLUP/CUBE).
    /// <summary>ClickHouse spells <c>ROLLUP</c> as the trailing modifier <c>GROUP BY ... WITH ROLLUP</c>.</summary>
    public override bool SupportsRollup => true;
    /// <summary>ClickHouse spells <c>CUBE</c> as the trailing modifier <c>GROUP BY ... WITH CUBE</c>.</summary>
    public override bool SupportsCube => true;
    /// <summary>ClickHouse supports <c>GROUP BY GROUPING SETS (...)</c>.</summary>
    public override bool SupportsGroupingSets => true;

    /// <summary>ClickHouse implements the <c>GROUP BY ... WITH TOTALS</c> modifier.</summary>
    public override bool SupportsGroupByWithTotals => true;

    /// <summary>The super-aggregate <c>WITH TOTALS</c> is a trailing modifier after the grouping list.</summary>
    public override string MakeGroupByTotals(string grouping, KeywordCase keywordCase = KeywordCase.Lower) => grouping + Kw(keywordCase, " with totals");

    /// <summary>
    /// ClickHouse provides the <c>numbers</c>/<c>numbers_mt</c>, <c>zeros</c>/<c>zeros_mt</c> and
    /// <c>generateRandom</c> table functions plus the server/cluster table functions <c>url</c>,
    /// <c>s3</c>, <c>file</c>, <c>remote</c>, <c>remoteSecure</c>, <c>cluster</c> and
    /// <c>clusterAllReplicas</c>.
    /// </summary>
    public override bool SupportsTableFunction(string name) =>
        name is "numbers" or "numbers_mt" or "zeros" or "zeros_mt" or "generateRandom" or "values"
            or "url" or "s3" or "file" or "remote" or "remoteSecure" or "cluster" or "clusterAllReplicas";

    /// <summary>ClickHouse renders the row-derived structure as the leading argument of <c>values</c>.</summary>
    public override bool SupportsResultSchema(TableFunctionSchema placement) => placement == TableFunctionSchema.LeadingArgument;

    /// <summary>ClickHouse's types are not nullable by default, so a <c>Nullable&lt;T&gt;</c> result column is wrapped in <c>Nullable(...)</c>.</summary>
    public override string MakeResultColumnType(Type clrType, bool nullable)
    {
        var underlying = Nullable.GetUnderlyingType(clrType);
        return underlying is not null ? $"Nullable({MakeTypeName(underlying)})" : MakeTypeName(clrType);
    }

    // The fixed structure the built-in generate_random()/generate_random(seed) map to
    // (ClickHouse's own no-argument generateRandom has a dynamic, random schema).
    private const string GenerateRandomStructure = "'id UInt64, value Float64, name String'";

    /// <summary>
    /// <c>numbers</c>/<c>numbers_mt</c> expose an unsigned <c>UInt64 number</c> column; cast it to
    /// <c>Int64</c> through a wrapping subquery to match <c>INumbersRow.Value</c>. The built-in
    /// <c>generateRandom</c> gets its fixed structure injected here and its <c>id</c> column cast the
    /// same way.
    /// </summary>
    public override string WrapTableFunction(string name, string call)
    {
        if (name is "numbers" or "numbers_mt")
            return $"(select toInt64(number) as number\n from {call})";

        if (name == "generateRandom")
        {
            var inner = call["generateRandom(".Length..^1];
            var args = inner.Length == 0 ? GenerateRandomStructure : $"{GenerateRandomStructure}, {inner}";
            return $"(select toInt64(id) as id, value, name\n from generateRandom({args}))";
        }

        return call;
    }

    /// <summary>
    /// ClickHouse cannot correlate a subquery on a joined source alias (the engine reports
    /// <c>NOT_IMPLEMENTED: can't find correlated column</c>), so a collection reached through a
    /// reference navigation is rejected by navigation expansion.
    /// </summary>
    public override bool SupportsReferenceToCollectionNavigation => false;

    /// <summary>ClickHouse implements the distributed <c>GLOBAL IN</c> predicate.</summary>
    public override bool SupportsGlobalPredicates => true;

    /// <summary>ClickHouse implements <c>LIMIT n BY expr</c>.</summary>
    public override ILimitByRenderer LimitBy => ClickHouseLimitByRenderer.Instance;

    /// <summary>ClickHouse implements the <c>FINAL</c> table modifier.</summary>
    public override bool SupportsFinal => true;

    /// <summary>ClickHouse implements the <c>SAMPLE</c> table modifier.</summary>
    public override bool SupportsSample => true;

    /// <summary>ClickHouse implements the <c>PREWHERE</c> clause.</summary>
    public override bool SupportsPreWhere => true;

    /// <summary>ClickHouse implements the <c>ARRAY JOIN</c> clause.</summary>
    public override IArrayJoinRenderer ArrayJoinClause => ClickHouseArrayJoinRenderer.Instance;

    /// <summary>ClickHouse implements the trailing <c>SETTINGS</c> clause.</summary>
    public override bool SupportsSettings => true;

    /// <summary>
    /// ClickHouse fills the unmatched side of an outer join with column defaults unless
    /// <c>join_use_nulls=1</c> is set; reference-navigation materialization needs real SQL nulls to see an
    /// absent principal, so the core preparation injects it query-locally.
    /// </summary>
    public override IReadOnlyList<KeyValuePair<string, string>> OuterJoinNullSettings { get; } = [new("join_use_nulls", "1")];

    /// <summary>Renders the trailing <c>with rollup</c>/<c>with cube</c> super-aggregate modifier.</summary>
    public override string MakeGrouping(string columns, GroupingType groupingType, KeywordCase keywordCase = KeywordCase.Lower) => groupingType switch
    {
        GroupingType.Rollup => columns + Kw(keywordCase, " with rollup"),
        GroupingType.Cube => columns + Kw(keywordCase, " with cube"),
        _ => columns
    };

    // ClickHouse quotes identifiers with backticks; single-quoted aliases are syntax errors.
    /// <summary>ClickHouse quotes aliases with backticks (single-quoted aliases are syntax errors), escaping an embedded backslash or backtick. <see langword="null"/> yields the empty quoted identifier.</summary>
    public override string Escape(string keyword) => QuoteIdentifier(keyword ?? string.Empty);

    /// <summary>ClickHouse quotes a physical identifier with backticks, escaping an embedded backslash or backtick.</summary>
    public override string QuoteIdentifier(string name) => "`" + name.Replace("\\", "\\\\").Replace("`", "``") + "`";

    /// <summary>ClickHouse qualifies a table with a database name (<c>`db`.`table`</c>); there is no separate schema level.</summary>
    public override bool SupportsCrossDatabase => true;

    /// <summary>
    /// Renders the ClickHouse single-qualifier form <c>database.table</c>. A database and a schema are
    /// the same concept here, so the two overrides are mutually exclusive; a linked server is unsupported.
    /// </summary>
    public override string MakeQualifiedTableName(string? server, string? database, string? schema, string table)
    {
        if (server is not null)
            throw new NotSupportedException("A linked-server table qualifier is not supported by this SQL dialect.");

        if (database is not null && schema is not null)
            throw new NotSupportedException("ClickHouse treats the database and the schema as the same qualifier; set only one of them.");

        return JoinTableQualifier(database ?? schema, table);
    }

    /// <summary>ClickHouse uses the backtick-quoted identifier for references as well.</summary>
    public override string MakeColumnReference(string name) => Escape(name);

    /// <summary>Renders a positional parameter as <c>@name</c>.</summary>
    public override string MakeParam(string name) => $"@{name}";

    /// <summary>Renders the standard <c>coalesce(v1, v2)</c>.</summary>
    public override string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";

    /// <summary>ClickHouse has a native boolean type, rendered as <c>true</c>/<c>false</c>.</summary>
    public override string MakeBool(bool v) => v ? "true" : "false";

    // length() counts bytes in ClickHouse; lengthUTF8() counts characters, matching string.Length.
    /// <summary><c>length</c> counts bytes, so <c>lengthUTF8(value)</c> is used to match <c>string.Length</c>.</summary>
    public override string MakeStringLength(string value) => $"lengthUTF8({value})";

    // position() works in bytes; positionUTF8() works in characters, matching string.IndexOf.
    /// <summary><c>position</c> counts bytes, so <c>positionUTF8(value, substring)</c> is used to match <c>string.IndexOf</c>.</summary>
    protected override string MakeStringPosition(string value, string substring) =>
        $"positionUTF8({value}, {substring})";

    /// <summary>Renders the character-wise <c>positionUTF8(value, substring, start + 1)</c> from a zero-based start.</summary>
    protected override string MakeStringPosition(string value, string substring, string start) =>
        $"positionUTF8({value}, {substring}, {start} + 1)";

    /// <summary>Renders <c>repeat(value, count)</c>.</summary>
    public override string MakeRepeat(string value, string count) => $"repeat({value}, {count})";

    /// <summary>Renders the character-wise <c>reverseUTF8(value)</c>.</summary>
    protected override string MakeStringReverse(string value) => $"reverseUTF8({value})";

    /// <summary>Renders <c>trimLeft</c>, <c>trimRight</c> or <c>trimBoth</c> according to the trim kind.</summary>
    public override string MakeTrim(string value, StringTrimKind kind) => kind switch
    {
        StringTrimKind.Start => $"trimLeft({value})",
        StringTrimKind.End => $"trimRight({value})",
        _ => $"trimBoth({value})"
    };

    /// <summary>ClickHouse renders CLR format specifiers through <c>format</c>/<c>formatDateTime</c>.</summary>
    public override IStringFormatFunctions? StringFormats => ClickHouseStringFormats.Instance;

    // ClickHouse has no COLLATE clause; the native String order is already byte-order.
    /// <summary>ClickHouse has no <c>COLLATE</c> clause.</summary>
    public override bool SupportsCollation => false;

    /// <summary>ClickHouse's native <c>String</c> order is byte-order, so ordinal comparison is native.</summary>
    public override bool SupportsOrdinalComparison => true;

    /// <inheritdoc/>
    public override string MakeOrdinal(string value, bool ignoreCase) => ignoreCase ? $"lower({value})" : value;

    // ClickHouse's LIKE always treats the backslash as the escape character and its parser accepts no
    // ESCAPE clause in any spelling (measured live), so the clause is omitted rather than spelled: the
    // pattern literal already carries the escaped wildcards as doubled backslashes.
    /// <summary>Omits the <c>ESCAPE</c> clause entirely; a custom escape character is rejected.</summary>
    public override string MakeLikeEscape(string escapeChar, KeywordCase keywordCase = KeywordCase.Lower) =>
        escapeChar == "\\"
            ? string.Empty
            : throw new NotSupportedException("A custom LIKE escape character is not supported by ClickHouse, which rejects the ESCAPE clause.");

    /// <summary>ClickHouse matches with the RE2-based <c>match</c> and replaces with <c>replaceRegexpAll</c>.</summary>
    public override bool SupportsRegex => true;

    /// <inheritdoc/>
    public override string MakeRegexMatch(string value, string pattern, bool ignoreCase) =>
        $"match({value}, {SqlStringLiteralPolicy.ClickHouse.Render((ignoreCase ? "(?i)" : string.Empty) + pattern, SqlStringLiteralRole.RegexPattern)})";

    /// <inheritdoc/>
    public override string MakeRegexReplace(string value, string pattern, string replacement, bool ignoreCase) =>
        $"replaceRegexpAll({value}, {SqlStringLiteralPolicy.ClickHouse.Render((ignoreCase ? "(?i)" : string.Empty) + pattern, SqlStringLiteralRole.RegexPattern)}, {SqlStringLiteralPolicy.ClickHouse.Render(replacement, SqlStringLiteralRole.RegexReplacement)})";

    // now() uses the server time zone; the optional argument selects a zone.
    /// <summary>Renders <c>now('UTC')</c> for UTC or the server-timezone <c>now()</c> for local time.</summary>
    public override string MakeNow(bool utc) => utc ? "now('UTC')" : "now()";

    /// <summary>
    /// ClickHouse spells the date parts through the <c>toXxx</c> family; the accessors return
    /// UInt8/UInt16, which the row reader cannot read through GetInt32, so they are cast. The
    /// normalised parts follow the cross-provider <c>extract</c> contract: <c>week</c> is ISO 8601,
    /// <c>dow</c> is 0=Sunday..6=Saturday (native <c>toDayOfWeek</c> is ISO 1..7, hence <c>% 7</c>) and
    /// <c>isodow</c> is 1=Monday..7=Sunday.
    /// </summary>
    public override string MakeDatePart(string part, string value) => part switch
    {
        "year" => $"toInt32(toYear({value}))",
        "quarter" => $"toInt32(toQuarter({value}))",
        "month" => $"toInt32(toMonth({value}))",
        "day" => $"toInt32(toDayOfMonth({value}))",
        "dow" => $"toInt32(toDayOfWeek({value}) % 7)",
        "isodow" => $"toInt32(toDayOfWeek({value}))",
        "doy" => $"toInt32(toDayOfYear({value}))",
        "week" => $"toInt32(toISOWeek({value}))",
        "hour" => $"toInt32(toHour({value}))",
        "minute" => $"toInt32(toMinute({value}))",
        "second" => $"toInt32(toSecond({value}))",
        "epoch" => $"toFloat64(toUnixTimestamp({value}))",
        _ => base.MakeDatePart(part, value)
    };

    /// <summary>ClickHouse additionally accepts the normalised weekdays and epoch.</summary>
    public override bool SupportsDatePart(string part) =>
        part is "dow" or "isodow" or "epoch" || base.SupportsDatePart(part);

    /// <summary>Maps the portable aggregate name to its ClickHouse spelling (for example <c>stdev</c> to <c>stddevSamp</c>).</summary>
    public override string MakeAggregate(string name) => name switch
    {
        "stdev" => "stddevSamp",
        "stdevp" => "stddevPop",
        "var" => "varSamp",
        "varp" => "varPop",
        "covar_pop" => "covarPop",
        "covar_samp" => "covarSamp",
        "bit_and" => "groupBitAnd",
        "bit_or" => "groupBitOr",
        "bit_xor" => "groupBitXor",
        "arg_min" => "argMin",
        "arg_max" => "argMax",
        "uniq_exact" => "uniqExact",
        "uniq_combined" => "uniqCombined",
        "uniq_hll12" => "uniqHLL12",
        "quantile_exact" => "quantileExact",
        "quantile_timing" => "quantileTiming",
        "top_k" => "topK",
        "top_k_weighted" => "topKWeighted",
        "any_agg" => "any",
        "any_last" => "anyLast",
        "group_array" => "groupArray",
        "group_uniq_array" => "groupUniqArray",
        _ => name
    };

    /// <summary>
    /// ClickHouse truncates through <c>dateTrunc(unit, value)</c>. It accepts <c>second</c>..<c>year</c>
    /// and the sub-second units for <c>DateTime64</c>, but has no <c>decade</c>/<c>century</c>/<c>millennium</c>;
    /// the plural ANSI sub-second names map to the ClickHouse singular ones.
    /// </summary>
    public override string MakeDateTrunc(string field, string value)
    {
        var part = field switch
        {
            "microseconds" => "microsecond",
            "milliseconds" => "millisecond",
            "decade" or "century" or "millennium" =>
                throw new NotSupportedException($"ClickHouse dateTrunc does not support the '{field}' field."),
            _ => field
        };

        return $"dateTrunc('{part}', {value})";
    }

    /// <summary>
    /// ClickHouse adds intervals through the dedicated <c>addYears</c>/<c>addQuarters</c>/.../<c>addSeconds</c>
    /// functions. <c>decade</c>/<c>century</c> are folded onto a scaled <c>addYears</c>; the year family
    /// (<c>year</c>/<c>decade</c>/<c>century</c>) promotes its operand to <c>DateTime64(6)</c>, because
    /// <c>addYears</c> on a 4-byte <c>DateTime</c> wraps past 2106 while <c>DateTime64</c> reaches 2299.
    /// <c>millennium</c> is rejected: the pinned 25.8 server cannot represent such results.
    /// </summary>
    public override string MakeDateAdd(string field, string amount, string value)
    {
        var (function, factor) = field switch
        {
            "year" => ("addYears", 1),
            "quarter" => ("addQuarters", 1),
            "month" => ("addMonths", 1),
            "week" => ("addWeeks", 1),
            "day" => ("addDays", 1),
            "hour" => ("addHours", 1),
            "minute" => ("addMinutes", 1),
            "second" => ("addSeconds", 1),
            "milliseconds" => ("addMilliseconds", 1),
            "microseconds" => ("addMicroseconds", 1),
            "decade" => ("addYears", 10),
            "century" => ("addYears", 100),
            "millennium" => throw new NotSupportedException(
                "ClickHouse date_add does not support the 'millennium' field: on the pinned ClickHouse 25.8 " +
                "server the supported DateTime64 result range is 1900-2299, so a millennium offset cannot be represented."),
            _ => throw new NotSupportedException($"ClickHouse date_add does not support the '{field}' field.")
        };

        var operand = field is "year" or "decade" or "century" ? $"toDateTime64({value}, 6)" : value;
        var scaled = factor == 1 ? amount : $"({amount}) * {factor}";
        return $"{function}({operand}, {scaled})";
    }

    /// <summary>
    /// ClickHouse 25.8 <c>DateTime64</c> spans 1900-2299, so a <c>millennium</c> offset cannot be
    /// represented; it is rejected up front instead of being silently clamped. Every other
    /// <c>date_add</c> part defers to the base capability set.
    /// </summary>
    public override bool SupportsDateAddField(string field) => field != "millennium" && base.SupportsDateAddField(field);

    /// <summary>ClickHouse stores a <see cref="System.DateTimeOffset"/> as a UTC-normalized <c>DateTime64</c>, so DTO operations are supported.</summary>
    public override bool SupportsDateTimeOffsetTranslation => true;

    /// <summary>
    /// Renders the UTC server clock at microsecond precision. ClickHouse <c>DateTime64</c> does not
    /// preserve an offset, so a materialized <see cref="System.DateTimeOffset"/> canonicalises to +00:00.
    /// </summary>
    public override string MakeDateTimeOffsetNow(bool utc) => "now64(6, 'UTC')";

    /// <summary>
    /// DTO date parts are taken from an explicit UTC-normalized value so the result does not depend on
    /// the column's declared time zone, then rendered through the native <c>toXxx</c> accessors.
    /// </summary>
    public override string MakeDatePart(string part, string value, bool isDateTimeOffset) =>
        isDateTimeOffset ? MakeDatePart(part, $"toTimeZone({value}, 'UTC')") : MakeDatePart(part, value);

    /// <summary>
    /// DTO date addition runs on an explicit UTC-normalized <c>DateTime64</c> value. ClickHouse
    /// <c>addXxx</c> truncates a fractional interval to whole units, so a coarse-unit DTO amount is
    /// scaled to <c>addMicroseconds</c> (the finest <c>addXxx</c> matching the documented
    /// <c>DateTime64(6)</c> precision), keeping a significant fraction instead of collapsing it to a
    /// no-op. Integer amounts stay correct through the same scaling.
    /// </summary>
    public override string MakeDateAdd(string field, string amount, string value, bool isDateTimeOffset)
    {
        if (!isDateTimeOffset)
            return MakeDateAdd(field, amount, value);

        var normalized = $"toTimeZone({value}, 'UTC')";
        var factor = field switch
        {
            "day" => 86_400_000_000L,
            "hour" => 3_600_000_000L,
            "minute" => 60_000_000L,
            "second" => 1_000_000L,
            "milliseconds" => 1_000L,
            _ => 0L
        };

        return factor == 0
            ? MakeDateAdd(field, amount, normalized)
            : $"addMicroseconds({normalized}, ({amount}) * {factor})";
    }

    /// <summary>ClickHouse computes date differences through <c>dateDiff(unit, start, end)</c>.</summary>
    public override string MakeDateDiff(string field, string start, string end) =>
        $"toInt32(dateDiff('{DateDiffUnit(field)}', {start}, {end}))";

    /// <summary>
    /// Renders the native 64-bit <c>dateDiff(unit, start, end)</c>: ClickHouse already answers with
    /// <c>Int64</c>, so <c>date_diff_big</c> must not inherit the narrow <c>toInt32</c> renderer.
    /// </summary>
    public override string MakeDateDiffBig(string field, string start, string end) =>
        $"dateDiff('{DateDiffUnit(field)}', {start}, {end})";

    // ClickHouse dateDiff uses the singular part names; plural ANSI parts map onto them.
    private static string DateDiffUnit(string field) => field switch
    {
        "milliseconds" => "millisecond",
        "microseconds" => "microsecond",
        _ => field
    };

    /// <summary>
    /// ClickHouse <c>add*</c> on a <c>Date</c> operand truncates the time of day, so a sub-day operand is
    /// promoted to <c>DateTime</c> (or <c>DateTime64</c> for the sub-second parts).
    /// </summary>
    public override string PromoteDateOperand(string field, string value) => field switch
    {
        "milliseconds" => $"toDateTime64({value}, 3)",
        "microseconds" => $"toDateTime64({value}, 6)",
        "hour" or "minute" or "second" => $"toDateTime({value})",
        _ => value
    };

    /// <summary>Renders <c>toLastDayOfMonth(value)</c>.</summary>
    public override string MakeEndOfMonth(string value) => $"toLastDayOfMonth({value})";

    /// <summary>Renders <c>makeDate(year, month, day)</c>.</summary>
    public override string MakeDateFromParts(string year, string month, string day) =>
        $"makeDate({year}, {month}, {day})";

    /// <summary>Renders <c>arrayStringConcat(groupArray(value), delimiter)</c>.</summary>
    public override string MakeStringAgg(string value, string delimiter) =>
        $"arrayStringConcat(groupArray({value}), {delimiter})";

    /// <summary>Renders the filtered <c>string_agg</c> as <c>arrayStringConcat(groupArrayIf(value, predicate), delimiter)</c>.</summary>
    public override string MakeFilteredStringAgg(string value, string delimiter, string predicate) =>
        $"arrayStringConcat(groupArrayIf({value}, {predicate}), {delimiter})";

    /// <summary>Maps the CLR type to its ClickHouse type name (<c>UInt8</c>, <c>Int16</c>, <c>Int32</c>, <c>Int64</c>, <c>Float32</c>, <c>Float64</c>, <c>Decimal</c>).</summary>
    public override string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "UInt8",
        _ when type == typeof(short) => "Int16",
        _ when type == typeof(int) => "Int32",
        _ when type == typeof(uint) => "UInt32",
        _ when type == typeof(long) => "Int64",
        _ when type == typeof(float) => "Float32",
        _ when type == typeof(double) => "Float64",
        _ when type == typeof(decimal) => "Decimal(38, 10)",
        _ when type == typeof(TimeSpan) => "Int64",
        _ when type == typeof(DateTimeOffset) => "DateTime64(6)",
        _ => base.MakeTypeName(type)
    };

    /// <summary>ClickHouse has no native duration type read back as <see cref="TimeSpan"/>; the value is stored in an <c>Int64</c>.</summary>
    public override string MakeDurationType(DurationUnit? unit, int precision = 0) => "Int64";

    /// <summary>ClickHouse's <c>Int64</c> is not nullable, so a nullable duration column uses <c>Nullable(Int64)</c>.</summary>
    public override string MakeNullableDurationType(DurationUnit? unit, int precision = 0) =>
        $"Nullable({MakeDurationType(unit, precision)})";

    // ClickHouse declares every CTE with `with`; there is no RECURSIVE keyword.
    /// <summary>ClickHouse declares every CTE with <c>with</c>; there is no <c>RECURSIVE</c> keyword.</summary>
    public override string MakeWith(bool recursive, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "with ");

    /// <summary>Renders <c>limit n offset m</c>, using the maximum unsigned bigint limit for offset-only pages.</summary>
    public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
    {
        // ClickHouse requires LIMIT before OFFSET, so an offset-only page uses the maximum unsigned
        // bigint as the limit.
        if (paging.Limit > 0)
            sqlBuilder.Append(Kw(keywordCase, "limit ")).Append(paging.Limit);
        else if (paging.Offset > 0)
            sqlBuilder.Append(Kw(keywordCase, "limit 18446744073709551615"));

        if (paging.Offset > 0)
            sqlBuilder.Append(Kw(keywordCase, " offset ")).Append(paging.Offset);
    }
}

internal sealed class ClickHouseIifRenderer : IIifRenderer
{
    public static readonly ClickHouseIifRenderer Instance = new();

    public string Render(string condition, string whenTrue, string whenFalse) =>
        $"if({condition}, {whenTrue}, {whenFalse})";
}

internal sealed class ClickHouseSessionInfoFunctions : ISessionInfoFunctions
{
    public static readonly ClickHouseSessionInfoFunctions Instance = new();

    public bool Supports(string name) =>
        name is "current_user" or "current_database" or "version"
            or "connection_id" or "clickhouse.host_name" or "clickhouse.tcp_port";

    public string Render(string name) => name switch
    {
        "current_user" => "currentUser()",
        "current_database" => "currentDatabase()",
        "version" => "version()",
        "connection_id" => "CAST(connectionId() AS UInt64)",
        "clickhouse.host_name" => "hostName()",
        "clickhouse.tcp_port" => "CAST(tcpPort() AS Int32)",
        _ => throw new NotSupportedException($"The {name} session information function is not supported by ClickHouse.")
    };
}

internal sealed class ClickHouseUuidGenerators : IUuidGenerators
{
    public static readonly ClickHouseUuidGenerators Instance = new();

    public bool Supports(string name) => name is "gen_random_uuid" or "uuidv7";

    public string Render(string name) => name switch
    {
        "gen_random_uuid" => "generateUUIDv4()",
        "uuidv7" => "generateUUIDv7()",
        _ => throw new NotSupportedException($"The {name} UUID generator function is not supported by ClickHouse.")
    };
}

internal sealed class ClickHouseLimitByRenderer : ILimitByRenderer
{
    public static readonly ClickHouseLimitByRenderer Instance = new();

    public string Render(int limit, int offset, IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var prefix = offset > 0 ? $"{offset}, " : string.Empty;
        return SqlKeywords.Of(keywordCase, "limit ") + prefix + limit + SqlKeywords.Of(keywordCase, " by ") + string.Join(", ", columns);
    }
}

internal sealed class ClickHouseStringSplitRenderer : IStringSplitRenderer
{
    public static readonly ClickHouseStringSplitRenderer Instance = new();

    public string Render(string separator, string value) =>
        $"splitByChar({separator}, {value})";
}

internal sealed class ClickHouseMultiIfRenderer(ClickHouseDialect dialect) : IMultiIfRenderer
{
    public string Render(IReadOnlyList<string> arguments, Type resultType)
    {
        var call = $"multiIf({string.Join(", ", arguments)})";

        return resultType == typeof(byte) || resultType == typeof(short) || resultType == typeof(int)
            || resultType == typeof(long) || resultType == typeof(float) || resultType == typeof(double)
            || resultType == typeof(decimal)
            ? $"cast({call} as {dialect.MakeTypeName(resultType)})"
            : call;
    }
}

internal sealed class ClickHouseUniqAggregateRenderer(ClickHouseDialect dialect) : IUniqAggregateRenderer
{
    // The uniq* aggregates return UInt64; cast to Int64 to match the declared long.
    public string Render(string name, string argument) =>
        $"toInt64({dialect.MakeAggregate(name)}({argument}))";
}

internal sealed class ClickHouseQuantileAggregateRenderer(ClickHouseDialect dialect) : IQuantileAggregateRenderer
{
    // quantileTiming returns Float32 and quantileExact keeps the input type; toFloat64 pins every
    // variant to the CLR double the methods declare.
    public string Render(string name, string level, string value) =>
        $"toFloat64({dialect.MakeAggregate(name)}({level})({value}))";

    // quantiles returns Array(Float64) for numeric input, which already matches the declared double[].
    public string RenderLevels(string name, string levels, string value) =>
        $"{dialect.MakeAggregate(name)}({levels})({value})";

    // ClickHouse's median is quantile(0.5); cast it so it materialises as a CLR double.
    public string RenderMedian(string value) => $"toFloat64(median({value}))";
}

internal sealed class ClickHouseTopKAggregateRenderer(ClickHouseDialect dialect) : ITopKAggregateRenderer
{
    // topK/topKWeighted return the value type, which the caller declares as T[].
    public string Render(string name, string k, string value) =>
        $"{dialect.MakeAggregate(name)}({k})({value})";

    public string RenderWeighted(string name, string k, string value, string weight) =>
        $"{dialect.MakeAggregate(name)}({k})({value}, {weight})";
}

/// <summary>
/// Renders the ClickHouse-only aggregate catalogue of T286b F16: the statistical/probability
/// aggregates, the parameterised quantile algorithms, the array- and tuple-returning aggregates, the
/// sequence aggregates and the time-series grid aggregates. The arm owns the vendor spelling and the
/// normalisation of the native result to the declared CLR type (signed narrowing of the unsigned
/// counts, <c>Float64</c> for the approximate quantiles, and <c>Array/Tuple</c> casts for the
/// composite results), so a name reported by <see cref="Supports"/> always renders the shape the
/// <see cref="ClickHouseFunctions"/> member declares.
/// </summary>
internal sealed class ClickHouseAggregateRenderer(ClickHouseDialect dialect) : IClickHouseAggregateRenderer
{
    /// <summary>The CLR member names of the aggregate catalogue this renderer implements.</summary>
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "entropy", "first_value", "last_value",
        "max_intersections", "max_intersections_position",
        "simple_linear_regression", "delta_sum", "delta_sum_timestamp",
        "gini", "skew_pop", "skew_samp", "kurt_pop", "kurt_samp", "rank_corr",
        "contingency", "cramers_v", "cramers_v_bias_corrected", "theils_u",
        "welch_t_test", "student_t_test", "student_t_test_one_sample",
        "mann_whitney_u_test", "kolmogorov_smirnov_test",
        "mean_z_test", "analysis_of_variance",
        "quantile_tdigest", "quantile_gk", "quantile_dd", "quantile_bfloat16",
        "quantile_prometheus_histogram",
        "quantiles_exact", "quantiles_exact_low", "quantiles_exact_high",
        "quantiles_exact_weighted", "quantiles_timing", "quantiles_tdigest",
        "quantiles_bfloat16", "quantiles_dd", "quantiles_gk", "quantiles_prometheus_histogram",
        "approx_top_k", "approx_top_sum", "any_heavy", "max_map", "min_map",
        "sum_count", "sum_kahan",
        "group_array_moving_avg", "group_array_moving_sum", "group_array_sample",
        "group_array_sorted", "group_array_last", "group_array_insert_at", "group_array_intersect",
        "group_concat", "group_format", "histogram", "sparkbar", "uniq_up_to",
        "sequence_count", "sequence_match_events", "sequence_next_node",
        "exponential_time_decayed_sum", "exponential_time_decayed_max",
        "exponential_time_decayed_count", "exponential_time_decayed_avg",
        "time_series_group_sum", "time_series_group_rate_sum",
        "time_series_instant_rate_to_grid", "time_series_rate_to_grid",
        "time_series_resample_to_grid_with_staleness"
    };

    /// <inheritdoc/>
    public bool Supports(string name) => Names.Contains(name);

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> arguments, Type resultType) =>
        RenderCore(name, parameters: null, arguments, resultType);

    /// <inheritdoc/>
    public string RenderParameterised(
        string name,
        IReadOnlyList<string> parameters,
        IReadOnlyList<string> arguments,
        Type resultType) =>
        RenderCore(name, parameters, arguments, resultType);

    /// <summary>Renders the vendor call and normalises its result to <paramref name="resultType"/>.</summary>
    private string RenderCore(
        string name,
        IReadOnlyList<string>? parameters,
        IReadOnlyList<string> arguments,
        Type resultType)
    {
        if (!Names.Contains(name))
            throw new NotSupportedException($"The {name} aggregate is not supported by this ClickHouse renderer.");

        var vendor = VendorName(name);
        var call = parameters is null
            ? $"{vendor}({string.Join(", ", arguments)})"
            : $"{vendor}({string.Join(", ", parameters)})({string.Join(", ", arguments)})";

        return name switch
        {
            // Unsigned native counters narrowed to the declared signed CLR type.
            "uniq_up_to" or "sequence_count" or "max_intersections" => $"toInt64({call})",
            "max_intersections_position" => $"toInt32({call})",

            // Approximate quantile algorithms whose native result is the input type or Float32.
            "quantile_tdigest" or "quantile_gk" or "quantile_prometheus_histogram" => $"toFloat64({call})",

            // deltaSum keeps the input type except for the signed -> unsigned rewrite of
            // deltaSumTimestamp; both are normalised back to the declared CLR type.
            "delta_sum" or "delta_sum_timestamp" => $"cast({call} as {TypeName(resultType)})",

            // Composite results: the native Tuple/Array element types follow the arguments, so the
            // declared CLR shape pins them.
            "simple_linear_regression" or "welch_t_test" or "student_t_test"
                or "student_t_test_one_sample" or "mann_whitney_u_test"
                or "kolmogorov_smirnov_test" or "mean_z_test" or "analysis_of_variance"
                or "sum_count" or "max_map" or "min_map"
                => $"cast({call} as {TypeName(resultType)})",

            "sequence_next_node" => $"cast({call} as Nullable(String))",

            // Array results (quantile families, groupArray* family, histograms, top-K pairs, the
            // sequence event timestamps and the time-series grids).
            "quantiles_exact" or "quantiles_exact_low" or "quantiles_exact_high"
                or "quantiles_exact_weighted" or "quantiles_timing" or "quantiles_tdigest"
                or "quantiles_bfloat16" or "quantiles_dd" or "quantiles_gk"
                or "quantiles_prometheus_histogram"
                or "group_array_moving_avg" or "group_array_moving_sum"
                or "group_array_sample" or "group_array_sorted" or "group_array_last"
                or "group_array_insert_at" or "group_array_intersect"
                or "histogram" or "approx_top_k" or "approx_top_sum"
                or "sequence_match_events"
                or "time_series_group_sum" or "time_series_group_rate_sum"
                or "time_series_instant_rate_to_grid" or "time_series_rate_to_grid"
                or "time_series_resample_to_grid_with_staleness"
                => $"cast({call} as {ElementArrayType(resultType)})",

            _ => call
        };
    }

    /// <summary>Maps the CLR member name to its vendor spelling.</summary>
    private static string VendorName(string name) => name switch
    {
        "max_intersections" => "maxIntersections",
        "max_intersections_position" => "maxIntersectionsPosition",
        "simple_linear_regression" => "simpleLinearRegression",
        "delta_sum" => "deltaSum",
        "delta_sum_timestamp" => "deltaSumTimestamp",
        "skew_pop" => "skewPop",
        "skew_samp" => "skewSamp",
        "kurt_pop" => "kurtPop",
        "kurt_samp" => "kurtSamp",
        "rank_corr" => "rankCorr",
        "cramers_v" => "cramersV",
        "cramers_v_bias_corrected" => "cramersVBiasCorrected",
        "theils_u" => "theilsU",
        "welch_t_test" => "welchTTest",
        "student_t_test" => "studentTTest",
        "student_t_test_one_sample" => "studentTTestOneSample",
        "mann_whitney_u_test" => "mannWhitneyUTest",
        "kolmogorov_smirnov_test" => "kolmogorovSmirnovTest",
        "mean_z_test" => "meanZTest",
        "analysis_of_variance" => "analysisOfVariance",
        "quantile_tdigest" => "quantileTDigest",
        "quantile_gk" => "quantileGK",
        "quantile_dd" => "quantileDD",
        "quantile_bfloat16" => "quantileBFloat16",
        "quantile_prometheus_histogram" => "quantilePrometheusHistogram",
        "quantiles_exact" => "quantilesExact",
        "quantiles_exact_low" => "quantilesExactLow",
        "quantiles_exact_high" => "quantilesExactHigh",
        "quantiles_exact_weighted" => "quantilesExactWeighted",
        "quantiles_timing" => "quantilesTiming",
        "quantiles_tdigest" => "quantilesTDigest",
        "quantiles_bfloat16" => "quantilesBFloat16",
        "quantiles_dd" => "quantilesDD",
        "quantiles_gk" => "quantilesGK",
        "quantiles_prometheus_histogram" => "quantilesPrometheusHistogram",
        "any_heavy" => "anyHeavy",
        "max_map" => "maxMappedArrays",
        "min_map" => "minMappedArrays",
        "sum_count" => "sumCount",
        "sum_kahan" => "sumKahan",
        "group_array_moving_avg" => "groupArrayMovingAvg",
        "group_array_moving_sum" => "groupArrayMovingSum",
        "group_array_sample" => "groupArraySample",
        "group_array_sorted" => "groupArraySorted",
        "group_array_last" => "groupArrayLast",
        "group_array_insert_at" => "groupArrayInsertAt",
        "group_array_intersect" => "groupArrayIntersect",
        "group_concat" => "groupConcat",
        "group_format" => "groupFormat",
        "uniq_up_to" => "uniqUpTo",
        "sequence_count" => "sequenceCount",
        "sequence_match_events" => "sequenceMatchEvents",
        "sequence_next_node" => "sequenceNextNode",
        "time_series_group_sum" => "timeSeriesGroupSum",
        "time_series_group_rate_sum" => "timeSeriesGroupRateSum",
        "time_series_instant_rate_to_grid" => "timeSeriesInstantRateToGrid",
        "time_series_rate_to_grid" => "timeSeriesRateToGrid",
        "time_series_resample_to_grid_with_staleness" => "timeSeriesResampleToGridWithStaleness",
        _ => name
    };

    /// <summary>Renders the element type of the declared one-dimensional CLR array result.</summary>
    private string ElementArrayType(Type resultType)
    {
        if (!resultType.IsArray || resultType.GetArrayRank() != 1)
            throw new NotSupportedException(
                $"The aggregate result type '{resultType}' must be a one-dimensional CLR array.");

        return $"Array({TypeName(resultType.GetElementType()!)})";
    }

    /// <summary>
    /// Renders a ClickHouse type name for a CLR type, covering the nullable, array and
    /// <see cref="Tuple"/> family shapes the aggregate results declare.
    /// </summary>
    private string TypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { IsValueType: true } underlying)
            return $"Nullable({TypeName(underlying)})";

        if (type.IsArray)
            return $"Array({TypeName(type.GetElementType()!)})";

        if (type.IsGenericType && TupleArities.Contains(type.GetGenericTypeDefinition()))
            return "Tuple(" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ")";

        return dialect.MakeTypeName(type);
    }

    /// <summary>The <see cref="Tuple"/> generic arities the row materializer supports.</summary>
    private static readonly HashSet<Type> TupleArities =
    [
        typeof(Tuple<>), typeof(Tuple<,>), typeof(Tuple<,,>), typeof(Tuple<,,,>),
        typeof(Tuple<,,,,>), typeof(Tuple<,,,,,>), typeof(Tuple<,,,,,,>)
    ];
}

internal sealed class ClickHouseSequenceAggregateRenderer : ISequenceAggregateRenderer
{
    public static readonly ClickHouseSequenceAggregateRenderer Instance = new();

    public string Render(string name, string? parameters, string arguments)
    {
        var function = name switch
        {
            "window_funnel" => "windowFunnel",
            "sequence_match" => "sequenceMatch",
            "retention" => "retention",
            _ => name
        };

        var call = parameters is null
            ? $"{function}({arguments})"
            : $"{function}({parameters})({arguments})";

        return name is "window_funnel" or "sequence_match" ? $"toInt32({call})" : call;
    }
}

internal sealed class ClickHouseArrayJoinRenderer : IArrayJoinRenderer
{
    public static readonly ClickHouseArrayJoinRenderer Instance = new();

    public string Render(ArrayJoinKind kind, IReadOnlyList<string> expressions, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var keyword = SqlKeywords.Of(keywordCase, kind == ArrayJoinKind.Left ? " left array join " : " array join ");
        return keyword + string.Join(", ", expressions);
    }
}

internal sealed class ClickHouseDateConversionRenderer : IDateConversionRenderer
{
    public static readonly ClickHouseDateConversionRenderer Instance = new();

    public string Render(string name, IReadOnlyList<string> args)
    {
        var function = name switch
        {
            "to_date" => "toDate",
            "to_date_time" => "toDateTime",
            "to_date32" => "toDate32",
            "to_day_of_week" => "toDayOfWeek",
            "to_start_of_year" => "toStartOfYear",
            "to_start_of_quarter" => "toStartOfQuarter",
            "to_start_of_month" => "toStartOfMonth",
            "to_start_of_week" => "toStartOfWeek",
            "to_start_of_day" => "toStartOfDay",
            "to_start_of_hour" => "toStartOfHour",
            "to_start_of_minute" => "toStartOfMinute",
            "to_start_of_second" => "toStartOfSecond",
            "to_monday" => "toMonday",
            "to_yyyymm" => "toYYYYMM",
            "to_yyyymmdd" => "toYYYYMMDD",
            "to_unix_timestamp" => "toUnixTimestamp",
            _ => name
        };

        var call = $"{function}({string.Join(", ", args)})";

        return name switch
        {
            "to_day_of_week" or "to_yyyymm" or "to_yyyymmdd" => $"toInt32({call})",
            "to_unix_timestamp" => $"toInt64({call})",
            _ => call
        };
    }
}

internal sealed class ClickHouseTupleRenderer : ITupleRenderer
{
    public static readonly ClickHouseTupleRenderer Instance = new();

    public string RenderConstructor(IReadOnlyList<string> fields) => "tuple(" + string.Join(", ", fields) + ")";

    public string? RenderElement(string row, int oneBasedIndex) => "tupleElement(" + row + ", " + oneBasedIndex + ")";
}

/// <summary>
/// ClickHouse rendering of the culture-invariant CLR format specifiers: <c>F</c> through <c>format</c>,
/// <c>D</c>/<c>X</c> through <c>toString</c>/<c>leftPad</c>/<c>hex</c>, and date/time through
/// <c>formatDateTime</c>. The group separator (<c>N</c>) is not offered because ClickHouse's
/// <c>format</c> grouping is not the invariant one.
/// </summary>
internal sealed class ClickHouseStringFormats : IStringFormatFunctions
{
    internal static readonly ClickHouseStringFormats Instance = new();

    public bool SupportsNumber(char specifier) => specifier is 'F' or 'D' or 'X';

    public string RenderNumber(string value, char specifier, int precision) => specifier switch
    {
        'F' => precision <= 0 ? $"format('{{:.0f}}', {value})" : $"format('{{:.{precision}f}}', {value})",
        'D' => precision <= 0 ? $"toString({value})" : $"leftPad(toString({value}), {precision}, '0')",
        'X' => precision <= 0 ? $"hex({value})" : $"leftPad(hex({value}), {precision}, '0')",
        _ => throw new NotSupportedException($"The numeric format specifier '{specifier}' is not supported by ClickHouse.")
    };

    public bool SupportsDateFormat(string clrFormat) => TryMapDate(clrFormat, out _);

    public string RenderDate(string value, string clrFormat) =>
        TryMapDate(clrFormat, out var native)
            ? $"formatDateTime({value}, '{native}')"
            : throw new NotSupportedException($"The date/time format string '{clrFormat}' is not supported by ClickHouse.");

    private static bool TryMapDate(string format, out string native)
    {
        var sb = new StringBuilder(format.Length + 6);
        var i = 0;

        while (i < format.Length)
        {
            if (Match(format, i, "yyyy")) { sb.Append("%Y"); i += 4; }
            else if (Match(format, i, "yy")) { sb.Append("%y"); i += 2; }
            else if (Match(format, i, "MM")) { sb.Append("%m"); i += 2; }
            else if (Match(format, i, "dd")) { sb.Append("%d"); i += 2; }
            else if (Match(format, i, "HH")) { sb.Append("%H"); i += 2; }
            else if (Match(format, i, "mm")) { sb.Append("%M"); i += 2; }
            else if (Match(format, i, "ss")) { sb.Append("%S"); i += 2; }
            else if (format[i] is '-' or '/' or '.' or ':' or ' ') { sb.Append(format[i]); i++; }
            else { native = string.Empty; return false; }
        }

        native = sb.ToString();
        return true;
    }

    private static bool Match(string value, int index, string token) =>
        index + token.Length <= value.Length && string.CompareOrdinal(value, index, token, 0, token.Length) == 0;
}

/// <summary>
/// The server-version boundaries of the ClickHouse capability gates. A version is caller-declared on
/// <see cref="ClickHouseDialect"/>; an undeclared (<see langword="null"/>) version never satisfies a
/// boundary, so the pinned-baseline surface stays exactly as it was.
/// </summary>
internal static class ClickHouseVersionFacts
{
    /// <summary>
    /// True when <paramref name="declaredVersion"/> is at or above
    /// <paramref name="requiredMajor"/>.<paramref name="requiredMinor"/> (a major/minor boundary; the
    /// build and revision components are ignored).
    /// </summary>
    internal static bool Supports(Version? declaredVersion, int requiredMajor, int requiredMinor) =>
        declaredVersion is { } version
        && (version.Major > requiredMajor
            || (version.Major == requiredMajor && version.Minor >= requiredMinor));
}

/// <summary>
/// Renders the cross-provider scalar functions of <see cref="CommonFunctions"/> on ClickHouse:
/// <c>left</c>/<c>right</c>, <c>leftPad</c>/<c>rightPad</c>, <c>repeat</c>, <c>reverseUTF8</c>,
/// <c>space</c>, <c>concatWithSeparator</c>, <c>translate</c>, <c>ascii</c> and <c>char</c>.
/// Note that ClickHouse's <c>concatWithSeparator</c> returns NULL when any argument is NULL,
/// whereas the other providers skip null arguments.
/// </summary>
/// <remarks>
/// A versioned instance is created by <see cref="ClickHouseDialect"/> when the caller declares a server
/// version; <see cref="Instance"/> stays the undeclared renderer of the shared dialect. The only
/// version-gated name today is <c>array_except</c>, whose native <c>arrayExcept</c> appeared in
/// ClickHouse 25.9 (issue #252).
/// </remarks>
internal sealed class ClickHouseScalarFunctions : IScalarFunctions
{
    /// <summary>The scalar name whose native ClickHouse implementation is version-gated.</summary>
    private const string ArrayExceptName = "array_except";

    /// <summary>The undeclared refusal, byte-identical to the pre-#252 message.</summary>
    private const string ArrayExceptUndeclaredReason =
        "The array_except function is not supported by ClickHouse.";

    internal static readonly ClickHouseScalarFunctions Instance = new(null);

    private readonly Version? _declaredVersion;

    /// <summary>Creates a renderer for the declared server version, or the undeclared one when <see langword="null"/>.</summary>
    internal ClickHouseScalarFunctions(Version? declaredVersion) => _declaredVersion = declaredVersion;

    /// <summary>True when the declared version provides the native <c>arrayExcept</c> (25.9+).</summary>
    private bool SupportsArrayExcept => ClickHouseVersionFacts.Supports(_declaredVersion, 25, 9);

    /// <inheritdoc/>
    public bool Supports(string name) =>
        name == ArrayExceptName ? SupportsArrayExcept : Names.Contains(name);

    /// <inheritdoc/>
    public string Render(string name, IReadOnlyList<string> args) =>
        name == ArrayExceptName
            ? RenderArrayExcept(args)
            : Arms.TryGetValue(name, out var arm)
                ? arm(args)
                : throw new NotSupportedException($"The {name} function is not supported by ClickHouse.");

    /// <inheritdoc/>
    public string? GetUnsupportedReason(string name) =>
        name == ArrayExceptName && _declaredVersion is { } version && !SupportsArrayExcept
            ? ArrayExceptVersionReason(version)
            : null;

    /// <summary>
    /// Renders the native <c>arrayExcept</c>. An undeclared dialect refuses with the historical
    /// message; a declared version below 25.9 refuses with the required and the declared version.
    /// </summary>
    private string RenderArrayExcept(IReadOnlyList<string> args)
    {
        if (_declaredVersion is null)
            throw new NotSupportedException(ArrayExceptUndeclaredReason);

        if (!SupportsArrayExcept)
            throw new NotSupportedException(ArrayExceptVersionReason(_declaredVersion));

        if (args.Count != ArrayExceptArity)
            throw new NotSupportedException(ArrayExceptArityReason(args.Count));

        return $"arrayExcept({string.Join(", ", args)})";
    }

    /// <summary>The number of arrays the native <c>arrayExcept</c> accepts (ClickHouse 25.9+).</summary>
    private const int ArrayExceptArity = 2;

    private static string ArrayExceptArityReason(int arity) =>
        $"The array_except function is not supported by ClickHouse with {arity} " +
        "array argument(s): ClickHouse's arrayExcept takes exactly two arrays.";

    private static string ArrayExceptVersionReason(Version declaredVersion) =>
        "The array_except function requires ClickHouse version 25.9 or later; " +
        $"declared server version: {declaredVersion.Major}.{declaredVersion.Minor}.";

    /// <summary>
    /// The supported names. A hash set keeps <see cref="Supports"/> O(1) and independent of the table
    /// size (the previous <c>is</c>-pattern chain was linear in the number of names).
    /// </summary>
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "left",
        "right",
        "lpad",
        "rpad",
        "repeat",
        "reverse",
        "space",
        "concat_ws",
        "translate",
        "ascii",
        "char",
        "lower_utf8",
        "upper_utf8",
        "trim_left",
        "trim_right",
        "trim_both",
        "replace_regexp_one",
        "replace_regexp_all",
        "match",
        "extract",
        "extract_all",
        "split_by_string",
        "split_by_regexp",
        "split_by_whitespace",
        "overlay",
        "initcap",
        "soundex",
        "base64_encode",
        "base64_decode",
        "hex",
        "unhex",
        "edit_distance",
        "jaro_similarity",
        "byte_hamming_distance",
        "substring_index",
        "count_substrings",
        "extract_groups",
        "normalize_utf8_nfc",
        "normalize_utf8_nfd",
        "normalize_utf8_nfkc",
        "normalize_utf8_nfkd",
        "format_date_time",
        "parse_date_time",
        "parse_date_time_best_effort",
        "now",
        "today",
        "yesterday",
        "array_concat",
        "array_flatten",
        "array_uniq",
        "array_intersect",
        "array_union",
        "array_symmetric_difference",
        "map",
        "map_keys",
        "map_values",
        "map_contains_key",
        "map_contains_value",
        "map_add",
        "map_concat",
        "map_filter",
        "map_apply",
        "map_all",
        "map_exists",
        "map_sort",
        "group_bitmap",
        "group_bitmap_and",
        "group_bitmap_or",
        "group_bitmap_xor",
        "sum_map",
        "sum_map_filtered",
        "md5",
        "sha1",
        "sha256",
        "sha512",
        "xx_hash32",
        "xx_hash64",
        "xxh3",
        "city_hash64",
        "sip_hash64",
        "sip_hash128",
        "murmur_hash2_32",
        "murmur_hash2_64",
        "murmur_hash3_32",
        "murmur_hash3_64",
        "murmur_hash3_128",
        "generate_ulid",
        "bin",
        "unbin",
        "base32_encode",
        "base32_decode",
        "base58_encode",
        "base58_decode",
        "base64_url_encode",
        "base64_url_decode",
        "bech32_encode",
        "bech32_decode",
        "bitmask_to_array",
        "bitmask_to_list",
        "bit_positions_to_array",
        "sqid_encode",
        "sqid_decode",
        "hilbert_encode",
        "hilbert_decode",
        "morton_encode",
        "morton_decode",
        "uuid_num_to_string",
        "uuid_string_to_num",
        "uuid_to_num",
        "uuid_v7_to_datetime",
        "to_uuid",
        "to_uuid_or_default",
        "to_uuid_or_null",
        "ulid_string_to_datetime",
        "generate_snowflake_id",
        "date_time_to_snowflake_id",
        "date_time64_to_snowflake_id",
        "date_time_to_uuid_v7",
        "snowflake_id_to_date_time",
        "snowflake_id_to_date_time64",
        "domain",
        "protocol",
        "path",
        "query_string",
        "fragment",
        "port",
        "cut_url_parameter",
        "top_level_domain",
        "encode_url_component",
        "decode_url_component",
        "encode_url_form_component",
        "decode_url_form_component",
        "domain_without_www",
        "first_significant_subdomain",
        "first_significant_subdomain_custom",
        "path_full",
        "query_string_and_fragment",
        "cut_www",
        "cut_query_string",
        "cut_fragment",
        "cut_query_string_and_fragment",
        "cut_to_first_significant_subdomain",
        "cut_to_first_significant_subdomain_with_www",
        "cut_to_first_significant_subdomain_custom",
        "cut_to_first_significant_subdomain_custom_with_www",
        "url_hierarchy",
        "url_path_hierarchy",
        "extract_url_parameter",
        "extract_url_parameter_names",
        "ipv4_num_to_string",
        "ipv4_num_to_string_class_c",
        "ipv4_string_to_num",
        "ipv4_string_to_num_or_default",
        "ipv4_string_to_num_or_null",
        "ipv6_num_to_string",
        "ipv6_string_to_num",
        "ipv6_string_to_num_or_default",
        "ipv6_string_to_num_or_null",
        "cut_ipv6",
        "is_ipv4_string",
        "is_ipv6_string",
        "is_ip_address_in_range",
        "to_ipv4",
        "to_ipv6",
        "ipv4_to_ipv6",
        "ipv4_cidr_to_range",
        "ipv6_cidr_to_range",
        "bit_and",
        "bit_or",
        "bit_xor",
        "bit_not",
        "bit_shift_left",
        "bit_shift_right",
        "bit_rotate_left",
        "bit_rotate_right",
        "bit_count",
        "bit_test",
        "bit_test_all",
        "bit_test_any",
        "bit_hamming_distance",
        "non_negative_derivative",
        "sha224",
        "sha384",
        "sha512_256",
        "blake3",
        "md4",
        "ripemd160",
        "keccak256",
        "farm_hash64",
        "metro_hash64",
        "java_hash",
        "hive_hash",
        "half_md5",
        "url_hash",
        "wy_hash64",
        "int_hash32",
        "int_hash64",
        "ngram_min_hash",
        "ngram_min_hash_arg",
        "ngram_sim_hash",
        "word_shingle_min_hash",
        "word_shingle_min_hash_arg",
        "word_shingle_sim_hash",
        "bit_length",
        "octet_length",
        "degrees",
        "radians",
        "pi",
        "log2",
        "cbrt",
        "exp2",
        "exp10",
        "erf",
        "erfc",
        "lgamma",
        "tgamma",
        "gcd",
        "lcm",
        "is_finite",
        "is_infinite",
        "is_nan",
        "hypot",
        "int_div",
        "int_exp2",
        "int_exp10",
        "sigmoid",
        "sqr",
        "round_age",
        "round_bankers",
        "round_down",
        "round_duration",
        "round_to_exp2",
        "make_date",
        "make_date32",
        "make_date_time",
        "now64",
        "now_in_block",
        "time_slot",
        "time_slots",
        "to_start_of_fifteen_minutes",
        "to_start_of_five_minutes",
        "to_start_of_ten_minutes",
        "to_start_of_interval",
        "to_iso_year",
        "to_week",
        "to_year_week",
        "to_last_day_of_week",
        "to_days_in_month",
        "to_modified_julian_day",
        "age",
        "date_name",
        "server_timezone",
        "timezone_of",
        "timezone_offset",
        "to_time",
        "timestamp",
        "from_unix_timestamp",
        "from_unix_timestamp64_milli",
        "from_unix_timestamp64_micro",
        "tuple_concat",
        "tuple_names",
        "flatten_tuple",
        "tuple_to_name_value_pairs",
        "tuple_plus",
        "tuple_minus",
        "tuple_multiply",
        "tuple_divide",
        "tuple_hamming_distance",
        "dot_product",
        "map_subtract",
        "map_update",
        "map_reverse_sort",
        "map_partial_sort",
        "map_from_arrays",
        "map_populate_series",
        "extract_key_value_pairs",
        "to_int8",
        "to_int8_or_null",
        "to_int8_or_default",
        "to_int16",
        "to_int16_or_null",
        "to_int16_or_default",
        "to_int32",
        "to_int32_or_null",
        "to_int32_or_default",
        "to_int64",
        "to_int64_or_null",
        "to_int64_or_default",
        "to_uint8",
        "to_uint8_or_null",
        "to_uint8_or_default",
        "to_uint16",
        "to_uint16_or_null",
        "to_uint16_or_default",
        "to_uint32",
        "to_uint32_or_null",
        "to_uint32_or_default",
        "to_uint64",
        "to_uint64_or_null",
        "to_uint64_or_default",
        "to_float32",
        "to_float32_or_null",
        "to_float32_or_default",
        "to_float64",
        "to_float64_or_null",
        "to_float64_or_default",
        "to_decimal32",
        "to_decimal32_or_null",
        "to_decimal32_or_default",
        "to_decimal64",
        "to_decimal64_or_null",
        "to_decimal64_or_default",
        "to_string",
        "to_bool",
        "to_fixed_string",
        "to_low_cardinality",
        "accurate_cast_int8",
        "accurate_cast_int8_or_null",
        "accurate_cast_int8_or_default",
        "accurate_cast_int16",
        "accurate_cast_int16_or_null",
        "accurate_cast_int16_or_default",
        "accurate_cast_int32",
        "accurate_cast_int32_or_null",
        "accurate_cast_int32_or_default",
        "accurate_cast_int64",
        "accurate_cast_int64_or_null",
        "accurate_cast_int64_or_default",
        "accurate_cast_uint8",
        "accurate_cast_uint8_or_null",
        "accurate_cast_uint8_or_default",
        "accurate_cast_uint16",
        "accurate_cast_uint16_or_null",
        "accurate_cast_uint16_or_default",
        "accurate_cast_uint32",
        "accurate_cast_uint32_or_null",
        "accurate_cast_uint32_or_default",
        "accurate_cast_uint64",
        "accurate_cast_uint64_or_null",
        "accurate_cast_uint64_or_default",
        "accurate_cast_float32",
        "accurate_cast_float32_or_null",
        "accurate_cast_float32_or_default",
        "accurate_cast_float64",
        "accurate_cast_float64_or_null",
        "accurate_cast_float64_or_default",
        "accurate_cast_decimal32",
        "accurate_cast_decimal32_or_null",
        "accurate_cast_decimal32_or_default",
        "accurate_cast_decimal64",
        "accurate_cast_decimal64_or_null",
        "accurate_cast_decimal64_or_default",
        "reinterpret_as_uint8",
        "reinterpret_as_uint16",
        "reinterpret_as_uint32",
        "reinterpret_as_uint64",
        "reinterpret_as_int8",
        "reinterpret_as_int16",
        "reinterpret_as_int32",
        "reinterpret_as_int64",
        "reinterpret_as_float32",
        "reinterpret_as_float64",
        "reinterpret_as_date",
        "reinterpret_as_date_time",
        "reinterpret_as_string",
        "reinterpret_as_fixed_string",
        "reinterpret_as_uuid",
    };

    /// <summary>
    /// The per-name renderers. A frozen hash table keeps <see cref="Render"/> O(1) and independent of
    /// the arm count (the previous string switch grew with every delivered member), and each arm is a
    /// non-capturing lambda, so the table is built once and never allocates per call.
    /// </summary>
    private static readonly FrozenDictionary<string, Func<IReadOnlyList<string>, string>> Arms =
        new Dictionary<string, Func<IReadOnlyList<string>, string>>(StringComparer.Ordinal)
        {

            // The UTF8 variants count characters, matching the CLR string semantics and the other
            // providers; the plain left/right/leftPad/rightPad count bytes.
            ["left"] = static args => $"leftUTF8({args[0]}, {args[1]})",
            ["right"] = static args => $"rightUTF8({args[0]}, {args[1]})",
            ["lpad"] = static args => $"leftPadUTF8({args[0]}, {args[1]}, {Pad(args)})",
            ["rpad"] = static args => $"rightPadUTF8({args[0]}, {args[1]}, {Pad(args)})",
            ["repeat"] = static args => $"repeat({args[0]}, {args[1]})",
            ["reverse"] = static args => $"reverseUTF8({args[0]})",
            ["space"] = static args => $"space({args[0]})",
            ["concat_ws"] = static args => $"concatWithSeparator({string.Join(", ", args)})",
            ["translate"] = static args => $"translate({args[0]}, {args[1]}, {args[2]})",
            ["ascii"] = static args => $"ascii({args[0]})",
            ["char"] = static args => $"char({args[0]})",
            ["lower_utf8"] = static args => $"lowerUTF8({args[0]})",
            ["upper_utf8"] = static args => $"upperUTF8({args[0]})",
            ["trim_left"] = static args => $"trimLeft({string.Join(", ", args)})",
            ["trim_right"] = static args => $"trimRight({string.Join(", ", args)})",
            ["trim_both"] = static args => $"trimBoth({string.Join(", ", args)})",
            ["replace_regexp_one"] = static args => $"replaceRegexpOne({args[0]}, {args[1]}, {args[2]})",
            ["replace_regexp_all"] = static args => $"replaceRegexpAll({args[0]}, {args[1]}, {args[2]})",
            ["match"] = static args => $"match({args[0]}, {args[1]})",
            ["extract"] = static args => $"extract({args[0]}, {args[1]})",
            ["extract_all"] = static args => $"extractAll({args[0]}, {args[1]})",
            ["split_by_string"] = static args => $"splitByString({args[0]}, {args[1]})",
            ["split_by_regexp"] = static args => $"splitByRegexp({args[0]}, {args[1]})",
            ["split_by_whitespace"] = static args => $"splitByWhitespace({args[0]})",


            // String-family arms. The UInt64 results are lowered to Int64 so the materializer sees the
            // declared long; the trailing optional arguments arrive only when the caller supplied them.
            ["overlay"] = static args => $"overlay({string.Join(", ", args)})",
            ["initcap"] = static args => $"initcap({args[0]})",
            ["soundex"] = static args => $"soundex({args[0]})",
            ["base64_encode"] = static args => $"base64Encode({args[0]})",
            ["base64_decode"] = static args => $"base64Decode({args[0]})",
            ["hex"] = static args => $"hex({args[0]})",
            ["unhex"] = static args => $"unhex({args[0]})",
            ["edit_distance"] = static args => $"toInt64(editDistance({args[0]}, {args[1]}))",
            ["jaro_similarity"] = static args => $"jaroSimilarity({args[0]}, {args[1]})",
            ["byte_hamming_distance"] = static args => $"toInt64(byteHammingDistance({args[0]}, {args[1]}))",
            ["substring_index"] = static args => $"substringIndex({args[0]}, {args[1]}, {args[2]})",
            ["count_substrings"] = static args => $"toInt64(countSubstrings({string.Join(", ", args)}))",
            ["extract_groups"] = static args => $"extractGroups({args[0]}, {args[1]})",
            ["normalize_utf8_nfc"] = static args => $"normalizeUTF8NFC({args[0]})",
            ["normalize_utf8_nfd"] = static args => $"normalizeUTF8NFD({args[0]})",
            ["normalize_utf8_nfkc"] = static args => $"normalizeUTF8NFKC({args[0]})",
            ["normalize_utf8_nfkd"] = static args => $"normalizeUTF8NFKD({args[0]})",
            ["format_date_time"] = static args => $"formatDateTime({string.Join(", ", args)})",
            ["parse_date_time"] = static args => $"parseDateTime({args[0]}, {args[1]})",
            ["parse_date_time_best_effort"] = static args => $"parseDateTimeBestEffort({string.Join(", ", args)})",
            ["now"] = static args => "now()",
            ["today"] = static args => "today()",
            ["yesterday"] = static args => "yesterday()",
            ["array_concat"] = static args => $"arrayConcat({string.Join(", ", args)})",
            ["array_flatten"] = static args => $"arrayFlatten({args[0]})",
            ["array_uniq"] = static args => $"toInt64(arrayUniq({args[0]}))",
            ["array_intersect"] = static args => $"arrayIntersect({string.Join(", ", args)})",
            ["array_union"] = static args => $"arrayUnion({string.Join(", ", args)})",
            ["array_symmetric_difference"] = static args => $"arraySymmetricDifference({string.Join(", ", args)})",
            ["map"] = static args => $"map({string.Join(", ", args)})",
            ["map_keys"] = static args => $"mapKeys({args[0]})",
            ["map_values"] = static args => $"mapValues({args[0]})",
            ["map_contains_key"] = static args => $"mapContainsKey({args[0]}, {args[1]})",
            ["map_contains_value"] = static args => $"mapContainsValue({args[0]}, {args[1]})",
            ["map_add"] = static args => $"mapAdd({args[0]}, {args[1]})",
            ["map_concat"] = static args => $"mapConcat({string.Join(", ", args)})",
            ["map_filter"] = static args => $"mapFilter({args[0]}, {args[1]})",
            ["map_apply"] = static args => $"mapApply({args[0]}, {args[1]})",
            ["map_all"] = static args => $"mapAll({args[0]}, {args[1]})",
            ["map_exists"] = static args => $"mapExists({args[0]}, {args[1]})",
            ["map_sort"] = static args => $"mapSort({args[0]})",
            ["group_bitmap"] = static args => $"groupBitmap({args[0]})",
            ["group_bitmap_and"] = static args => $"groupBitmapAnd({args[0]})",
            ["group_bitmap_or"] = static args => $"groupBitmapOr({args[0]})",
            ["group_bitmap_xor"] = static args => $"groupBitmapXor({args[0]})",
            ["sum_map"] = static args => $"sumMap({args[0]}, {args[1]})",
            ["sum_map_filtered"] = static args => $"sumMapFiltered({args[0]})({args[1]}, {args[2]})",

            // Tuple family (T286b F06): the native spellings; the translator normalises the completed
            // call to the declared CLR tuple with a cast, and lowers the UInt* Hamming result to Int64.
            ["tuple_concat"] = static args => $"tupleConcat({args[0]}, {args[1]})",
            ["tuple_names"] = static args => $"tupleNames({args[0]})",
            ["flatten_tuple"] = static args => $"flattenTuple({args[0]})",
            ["tuple_to_name_value_pairs"] = static args => $"tupleToNameValuePairs({args[0]})",
            ["tuple_plus"] = static args => $"tuplePlus({args[0]}, {args[1]})",
            ["tuple_minus"] = static args => $"tupleMinus({args[0]}, {args[1]})",
            ["tuple_multiply"] = static args => $"tupleMultiply({args[0]}, {args[1]})",
            ["tuple_divide"] = static args => $"tupleDivide({args[0]}, {args[1]})",
            ["tuple_hamming_distance"] = static args => $"toInt64(tupleHammingDistance({args[0]}, {args[1]}))",
            ["dot_product"] = static args => $"dotProduct({args[0]}, {args[1]})",

            // Map family (T286b F07): the native spellings; the translator normalises the completed call
            // to the declared Map(K, V). mapPartialSort follows the vendor argument order (limit, map).
            ["map_subtract"] = static args => $"mapSubtract({args[0]}, {args[1]})",
            ["map_update"] = static args => $"mapUpdate({args[0]}, {args[1]})",
            ["map_reverse_sort"] = static args => $"mapReverseSort({args[0]})",
            ["map_partial_sort"] = static args => $"mapPartialSort({args[0]}, {args[1]})",
            ["map_from_arrays"] = static args => $"mapFromArrays({args[0]}, {args[1]})",
            ["map_populate_series"] = static args => args.Count == 2
            ? $"mapPopulateSeries({args[0]}, {args[1]})"
            : $"mapPopulateSeries({args[0]})",
            ["extract_key_value_pairs"] = static args => $"extractKeyValuePairs({string.Join(", ", args)})",
            ["md5"] = static args => $"lower(hex(MD5({args[0]})))",
            ["sha1"] = static args => $"lower(hex(SHA1({args[0]})))",
            ["sha256"] = static args => $"lower(hex(SHA256({args[0]})))",
            ["sha512"] = static args => $"lower(hex(SHA512({args[0]})))",
            ["xx_hash32"] = static args => $"toInt32(xxHash32({args[0]}))",
            ["xx_hash64"] = static args => $"toInt64(xxHash64({args[0]}))",
            ["xxh3"] = static args => $"toInt64(xxh3({args[0]}))",
            ["city_hash64"] = static args => $"toInt64(cityHash64({args[0]}))",
            ["sip_hash64"] = static args => $"toInt64(sipHash64({args[0]}))",
            ["sip_hash128"] = static args => $"lower(hex(sipHash128({args[0]})))",
            ["murmur_hash2_32"] = static args => $"toInt32(murmurHash2_32({args[0]}))",
            ["murmur_hash2_64"] = static args => $"toInt64(murmurHash2_64({args[0]}))",
            ["murmur_hash3_32"] = static args => $"toInt32(murmurHash3_32({args[0]}))",
            ["murmur_hash3_64"] = static args => $"toInt64(murmurHash3_64({args[0]}))",
            ["murmur_hash3_128"] = static args => $"lower(hex(murmurHash3_128({args[0]})))",

            // Hash family (T286b F10). Digests keep the shipped md5/sha contract (the server returns
            // FixedString, so lower(hex(...)) is the readable lowercase form with leading zeros intact).
            // The non-cryptographic hashes keep the server's own width/signedness: no toInt64 lowering,
            // because UInt64 must not be reinterpreted as a negative Int64. ngramsize/shinglesize are the
            // vendor UInt8 parameters, so a supplied value is cast; the vendor hashnum is fixed at its
            // default 6 and never rendered.
            ["sha224"] = static args => $"lower(hex(SHA224({args[0]})))",
            ["sha384"] = static args => $"lower(hex(SHA384({args[0]})))",
            ["sha512_256"] = static args => $"lower(hex(SHA512_256({args[0]})))",
            ["blake3"] = static args => $"lower(hex(BLAKE3({args[0]})))",
            ["md4"] = static args => $"lower(hex(MD4({args[0]})))",
            ["ripemd160"] = static args => $"lower(hex(RIPEMD160({args[0]})))",
            ["keccak256"] = static args => $"lower(hex(keccak256({args[0]})))",
            ["farm_hash64"] = static args => $"farmHash64({args[0]})",
            ["metro_hash64"] = static args => $"metroHash64({args[0]})",
            ["java_hash"] = static args => $"javaHash({args[0]})",
            ["hive_hash"] = static args => $"hiveHash({args[0]})",
            ["half_md5"] = static args => $"halfMD5({args[0]})",
            ["url_hash"] = static args => args.Count == 2
            ? $"URLHash({args[0]}, {args[1]})"
            : $"URLHash({args[0]})",
            ["wy_hash64"] = static args => $"wyHash64({args[0]})",
            ["int_hash32"] = static args => $"intHash32({args[0]})",
            ["int_hash64"] = static args => $"intHash64({args[0]})",
            ["ngram_min_hash"] = static args => args.Count == 2
            ? $"ngramMinHash({args[0]}, {args[1]})"
            : $"ngramMinHash({args[0]})",
            ["ngram_min_hash_arg"] = static args => args.Count == 2
            ? $"ngramMinHashArg({args[0]}, {args[1]})"
            : $"ngramMinHashArg({args[0]})",
            ["ngram_sim_hash"] = static args => args.Count == 2
            ? $"ngramSimHash({args[0]}, {args[1]})"
            : $"ngramSimHash({args[0]})",
            ["word_shingle_min_hash"] = static args => args.Count == 2
            ? $"wordShingleMinHash({args[0]}, {args[1]})"
            : $"wordShingleMinHash({args[0]})",
            ["word_shingle_min_hash_arg"] = static args => args.Count == 2
            ? $"wordShingleMinHashArg({args[0]}, {args[1]})"
            : $"wordShingleMinHashArg({args[0]})",
            ["word_shingle_sim_hash"] = static args => args.Count == 2
            ? $"wordShingleSimHash({args[0]}, {args[1]})"
            : $"wordShingleSimHash({args[0]})",
            ["generate_ulid"] = static args => "generateULID()",


            // Encoding family (T286b F11). Every binary result is wrapped in hex(...) so the declared CLR
            // shape is an explicit hexadecimal string rather than a UTF-8-decoded binary string. The
            // text-taking encoders keep the vendor spelling. bech32Decode returns Tuple(String, String)
            // with binary data in the second component, so the data is hex-wrapped here as well; the
            // decode is pure, so evaluating it twice for the two tuple components is value-safe.
            ["bin"] = static args => $"bin({args[0]})",
            ["unbin"] = static args => $"hex(unbin({args[0]}))",
            ["base32_encode"] = static args => $"base32Encode({args[0]})",
            ["base32_decode"] = static args => $"hex(base32Decode({args[0]}))",
            ["base58_encode"] = static args => $"base58Encode({args[0]})",
            ["base58_decode"] = static args => $"hex(base58Decode({args[0]}))",
            ["base64_url_encode"] = static args => $"base64URLEncode({args[0]})",
            ["base64_url_decode"] = static args => $"hex(base64URLDecode({args[0]}))",
            ["bech32_encode"] = static args => args.Count == 3
            ? $"bech32Encode({args[0]}, {args[1]}, {args[2]})"
            : $"bech32Encode({args[0]}, {args[1]})",
            ["bech32_decode"] = static args => $"tuple(tupleElement(bech32Decode({args[0]}), 1), hex(tupleElement(bech32Decode({args[0]}), 2)))",
            ["bitmask_to_array"] = static args => $"bitmaskToArray(toUInt64({args[0]}))",
            ["bitmask_to_list"] = static args => $"bitmaskToList({args[0]})",
            ["bit_positions_to_array"] = static args => $"bitPositionsToArray({args[0]})",
            ["sqid_encode"] = static args => $"sqidEncode({string.Join(", ", args)})",
            ["sqid_decode"] = static args => $"sqidDecode({args[0]})",
            ["hilbert_encode"] = static args => args.Count == 2
            ? $"hilbertEncode({args[0]}, {args[1]})"
            : $"hilbertEncode({args[0]})",
            ["hilbert_decode"] = static args => $"hilbertDecode(2, {args[0]})",
            ["morton_encode"] = static args => $"mortonEncode({args[0]}, {args[1]})",
            ["morton_decode"] = static args => $"mortonDecode(2, {args[0]})",


            // UUID/ULID/snowflake family (T286b F12). The binary 16-byte UUID values are wrapped in
            // hex(...) or unpacked with toFixedString(unhex(...)), so a Guid is used only where the
            // native UUID type is the exact mapping. dateTime64ToSnowflakeID requires DateTime64, which
            // the arm supplies at millisecond precision (the vendor format itself truncates below ms).
            ["uuid_num_to_string"] = static args => $"UUIDNumToString(toFixedString(unhex({args[0]}), 16))",
            ["uuid_string_to_num"] = static args => $"hex(UUIDStringToNum({args[0]}))",
            ["uuid_to_num"] = static args => $"hex(UUIDToNum({args[0]}))",
            ["uuid_v7_to_datetime"] = static args => $"UUIDv7ToDateTime({args[0]})",
            ["to_uuid"] = static args => $"toUUID({args[0]})",
            ["to_uuid_or_default"] = static args => $"toUUIDOrDefault({args[0]}, {args[1]})",
            ["to_uuid_or_null"] = static args => $"toUUIDOrNull({args[0]})",
            ["ulid_string_to_datetime"] = static args => $"ULIDStringToDateTime({args[0]})",
            ["generate_snowflake_id"] = static args => "generateSnowflakeID()",
            ["date_time_to_snowflake_id"] = static args => $"dateTimeToSnowflakeID({args[0]})",
            ["date_time64_to_snowflake_id"] = static args => $"dateTime64ToSnowflakeID(toDateTime64({args[0]}, 3))",
            ["date_time_to_uuid_v7"] = static args => $"dateTimeToUUIDv7({args[0]})",
            ["snowflake_id_to_date_time"] = static args => $"snowflakeIDToDateTime({args[0]})",
            ["snowflake_id_to_date_time64"] = static args => $"snowflakeIDToDateTime64({args[0]})",


            // URL/IP family (T286b F13). The vendor function names are case-sensitive: the hierarchy
            // functions are URLHierarchy/URLPathHierarchy (not urlHierarchy/urlPathHierarchy). The
            // 16-byte IPv6 binary form is hex text, so an argument that needs FixedString(16) is unpacked
            // with toFixedString(unhex(...), 16) and a binary result is wrapped in hex(...).
            ["domain"] = static args => $"domain({args[0]})",
            ["protocol"] = static args => $"protocol({args[0]})",
            ["path"] = static args => $"path({args[0]})",
            ["query_string"] = static args => $"queryString({args[0]})",
            ["fragment"] = static args => $"fragment({args[0]})",
            ["port"] = static args => args.Count == 2
            ? $"toInt32(port({args[0]}, {args[1]}))"
            : $"toInt32(port({args[0]}))",
            ["cut_url_parameter"] = static args => $"cutURLParameter({args[0]}, {args[1]})",
            ["top_level_domain"] = static args => $"topLevelDomain({args[0]})",
            ["encode_url_component"] = static args => $"encodeURLComponent({args[0]})",
            ["decode_url_component"] = static args => $"decodeURLComponent({args[0]})",
            ["encode_url_form_component"] = static args => $"encodeURLFormComponent({args[0]})",
            ["decode_url_form_component"] = static args => $"decodeURLFormComponent({args[0]})",
            ["domain_without_www"] = static args => $"domainWithoutWWW({args[0]})",
            ["first_significant_subdomain"] = static args => $"firstSignificantSubdomain({args[0]})",
            ["first_significant_subdomain_custom"] = static args => 
            $"firstSignificantSubdomainCustom({args[0]}, {args[1]})",
            ["path_full"] = static args => $"pathFull({args[0]})",
            ["query_string_and_fragment"] = static args => $"queryStringAndFragment({args[0]})",
            ["cut_www"] = static args => $"cutWWW({args[0]})",
            ["cut_query_string"] = static args => $"cutQueryString({args[0]})",
            ["cut_fragment"] = static args => $"cutFragment({args[0]})",
            ["cut_query_string_and_fragment"] = static args => $"cutQueryStringAndFragment({args[0]})",
            ["cut_to_first_significant_subdomain"] = static args => $"cutToFirstSignificantSubdomain({args[0]})",
            ["cut_to_first_significant_subdomain_with_www"] = static args => 
            $"cutToFirstSignificantSubdomainWithWWW({args[0]})",
            ["cut_to_first_significant_subdomain_custom"] = static args => 
            $"cutToFirstSignificantSubdomainCustom({args[0]}, {args[1]})",
            ["cut_to_first_significant_subdomain_custom_with_www"] = static args => 
            $"cutToFirstSignificantSubdomainCustomWithWWW({args[0]}, {args[1]})",
            ["url_hierarchy"] = static args => $"URLHierarchy({args[0]})",
            ["url_path_hierarchy"] = static args => $"URLPathHierarchy({args[0]})",
            ["extract_url_parameter"] = static args => $"extractURLParameter({args[0]}, {args[1]})",
            ["extract_url_parameter_names"] = static args => $"extractURLParameterNames({args[0]})",
            ["ipv4_num_to_string"] = static args => $"IPv4NumToString({args[0]})",
            ["ipv4_num_to_string_class_c"] = static args => $"IPv4NumToStringClassC({args[0]})",
            ["ipv4_string_to_num"] = static args => $"IPv4StringToNum({args[0]})",
            ["ipv4_string_to_num_or_default"] = static args => $"IPv4StringToNumOrDefault({args[0]})",
            ["ipv4_string_to_num_or_null"] = static args => $"IPv4StringToNumOrNull({args[0]})",
            ["ipv6_num_to_string"] = static args => $"IPv6NumToString(toFixedString(unhex({args[0]}), 16))",
            ["ipv6_string_to_num"] = static args => $"hex(IPv6StringToNum({args[0]}))",
            ["ipv6_string_to_num_or_default"] = static args => $"hex(IPv6StringToNumOrDefault({args[0]}))",
            ["ipv6_string_to_num_or_null"] = static args => $"hex(IPv6StringToNumOrNull({args[0]}))",
            ["cut_ipv6"] = static args => 
            $"cutIPv6(toFixedString(unhex({args[0]}), 16), {args[1]}, {args[2]})",
            ["is_ipv4_string"] = static args => $"isIPv4String({args[0]})",
            ["is_ipv6_string"] = static args => $"isIPv6String({args[0]})",
            ["is_ip_address_in_range"] = static args => $"isIPAddressInRange({args[0]}, {args[1]})",
            ["to_ipv4"] = static args => $"toString(toIPv4({args[0]}))",
            ["to_ipv6"] = static args => $"toString(toIPv6({args[0]}))",
            ["ipv4_to_ipv6"] = static args => $"toString(IPv4ToIPv6(toIPv4({args[0]})))",
            ["ipv4_cidr_to_range"] = static args => 
            $"tuple(toString(tupleElement(IPv4CIDRToRange(toIPv4({args[0]}), {args[1]}), 1)), " +
            $"toString(tupleElement(IPv4CIDRToRange(toIPv4({args[0]}), {args[1]}), 2)))",
            ["ipv6_cidr_to_range"] = static args => 
            $"tuple(toString(tupleElement(IPv6CIDRToRange(toIPv6({args[0]}), {args[1]}), 1)), " +
            $"toString(tupleElement(IPv6CIDRToRange(toIPv6({args[0]}), {args[1]}), 2)))",


            // Bit family (T286b F14). The dialect owns the vendor spelling only; the width decisions
            // (exact toUInt64 widening and the UInt16 toInt32 result widening) live in the translator,
            // which sees the overload signature the name alone cannot carry.
            ["bit_and"] = static args => $"bitAnd({args[0]}, {args[1]})",
            ["bit_or"] = static args => $"bitOr({args[0]}, {args[1]})",
            ["bit_xor"] = static args => $"bitXor({args[0]}, {args[1]})",
            ["bit_not"] = static args => $"bitNot({args[0]})",
            ["bit_shift_left"] = static args => $"bitShiftLeft({args[0]}, {args[1]})",
            ["bit_shift_right"] = static args => $"bitShiftRight({args[0]}, {args[1]})",
            ["bit_rotate_left"] = static args => $"bitRotateLeft({args[0]}, {args[1]})",
            ["bit_rotate_right"] = static args => $"bitRotateRight({args[0]}, {args[1]})",
            ["bit_count"] = static args => $"bitCount({args[0]})",
            ["bit_test"] = static args => $"bitTest({args[0]}, {args[1]})",
            ["bit_test_all"] = static args => $"bitTestAll({string.Join(", ", args)})",
            ["bit_test_any"] = static args => $"bitTestAny({string.Join(", ", args)})",
            ["bit_hamming_distance"] = static args => $"bitHammingDistance({args[0]}, {args[1]})",


            // Window family (T286b F17). The function is window-only; the OVER clause is appended by the
            // window translator, so this arm is the vendor spelling of the call itself (also the pinned
            // dialect-table row). The translator gate keeps other providers from emitting it.
            ["non_negative_derivative"] = static args => $"nonNegativeDerivative({string.Join(", ", args)})",


            // ClickHouse length(String) is the byte count; bit_length is eight times it. There is no cot.
            ["bit_length"] = static args => $"toInt32(length({args[0]}) * 8)",
            ["octet_length"] = static args => $"toInt32(length({args[0]}))",
            ["degrees"] = static args => $"degrees({args[0]})",
            ["radians"] = static args => $"radians({args[0]})",
            ["pi"] = static args => "pi()",
            ["log2"] = static args => $"log2({args[0]})",
            ["cbrt"] = static args => $"cbrt({args[0]})",
            ["exp2"] = static args => $"exp2({args[0]})",
            ["exp10"] = static args => $"exp10({args[0]})",
            ["erf"] = static args => $"erf({args[0]})",
            ["erfc"] = static args => $"erfc({args[0]})",
            ["lgamma"] = static args => $"lgamma({args[0]})",
            ["tgamma"] = static args => $"tgamma({args[0]})",
            ["gcd"] = static args => $"gcd({args[0]}, {args[1]})",
            ["lcm"] = static args => $"lcm({args[0]}, {args[1]})",
            ["is_finite"] = static args => $"isFinite({args[0]})",
            ["is_infinite"] = static args => $"isInfinite({args[0]})",
            ["is_nan"] = static args => $"isNaN({args[0]})",
            ["hypot"] = static args => $"hypot({args[0]}, {args[1]})",
            ["int_div"] = static args => $"intDiv({args[0]}, {args[1]})",
            ["int_exp2"] = static args => $"intExp2({args[0]})",
            ["int_exp10"] = static args => $"intExp10({args[0]})",
            ["sigmoid"] = static args => $"sigmoid({args[0]})",


            // sqr was introduced in ClickHouse 26.7 and the pinned 25.8 server rejects the name; the vendor
            // definition is "the product of x multiplied by itself" with the multiplication operator's return
            // type, so the arm renders exactly that product and stays version-independent.
            ["sqr"] = static args => $"({args[0]} * {args[0]})",


            // Rounding family. roundAge returns UInt8 and roundDuration UInt16; both are lowered to Int64
            // so the materializer sees the declared long. roundBankers/roundDown/roundToExp2 already return
            // the argument's own numeric type, which is the declared CLR shape.
            ["round_age"] = static args => $"toInt64(roundAge({args[0]}))",
            ["round_bankers"] = static args => $"roundBankers({string.Join(", ", args)})",
            ["round_down"] = static args => $"roundDown({args[0]}, {args[1]})",
            ["round_duration"] = static args => $"toInt64(roundDuration({args[0]}))",
            ["round_to_exp2"] = static args => $"roundToExp2({args[0]})",


            // Date/time family. The UInt8/UInt16/UInt32 results are lowered to Int32 so the materializer
            // sees the declared int; timeSlots needs exactly UInt32 for its duration, and the week modes
            // must be UInt8 (the server rejects an Int32 parameter in that position).
            ["make_date"] = static args => $"makeDate({string.Join(", ", args)})",
            ["make_date32"] = static args => $"makeDate32({string.Join(", ", args)})",
            ["make_date_time"] = static args => $"makeDateTime({string.Join(", ", args)})",
            ["now64"] = static args => args.Count == 0 ? "now64()" : $"now64({args[0]})",
            ["now_in_block"] = static args => "nowInBlock()",
            ["time_slot"] = static args => $"timeSlot({args[0]})",
            ["time_slots"] = static args => $"timeSlots(toDateTime({args[0]}), toUInt32({args[1]}))",
            ["to_start_of_fifteen_minutes"] = static args => $"toStartOfFifteenMinutes({args[0]})",
            ["to_start_of_five_minutes"] = static args => $"toStartOfFiveMinutes({args[0]})",
            ["to_start_of_ten_minutes"] = static args => $"toStartOfTenMinutes({args[0]})",
            ["to_start_of_interval"] = static args => StartOfIntervalArm(args),
            ["to_iso_year"] = static args => $"toInt32(toISOYear({args[0]}))",
            ["to_week"] = static args => args.Count == 1
            ? $"toInt32(toWeek({args[0]}))"
            : $"toInt32(toWeek({args[0]}, toUInt8({args[1]})))",
            ["to_year_week"] = static args => args.Count == 1
            ? $"toInt32(toYearWeek({args[0]}))"
            : $"toInt32(toYearWeek({args[0]}, toUInt8({args[1]})))",
            ["to_last_day_of_week"] = static args => args.Count == 1
            ? $"toLastDayOfWeek({args[0]})"
            : $"toLastDayOfWeek({args[0]}, toUInt8({args[1]}))",

            // toDaysInMonth arrived in 26.3; this is the vendor definition with the same UInt8 result.
            ["to_days_in_month"] = static args => $"toInt32(toDayOfMonth(toLastDayOfMonth({args[0]})))",
            ["to_modified_julian_day"] = static args => $"toModifiedJulianDay({args[0]})",
            ["age"] = static args => $"age({string.Join(", ", args)})",
            ["date_name"] = static args => $"dateName({string.Join(", ", args)})",
            ["server_timezone"] = static args => "serverTimezone()",
            ["timezone_of"] = static args => $"timezoneOf({args[0]})",
            ["timezone_offset"] = static args => $"timezoneOffset({args[0]})",

            // toTime was reassigned in 26.7; toTimeWithFixedDate is the stable name of the legacy value.
            ["to_time"] = static args => $"toTimeWithFixedDate({args[0]})",
            ["timestamp"] = static args => $"timestamp({string.Join(", ", args)})",
            ["from_unix_timestamp"] = static args => $"fromUnixTimestamp({string.Join(", ", args)})",
            ["from_unix_timestamp64_milli"] = static args => $"fromUnixTimestamp64Milli({args[0]})",
            ["from_unix_timestamp64_micro"] = static args => $"fromUnixTimestamp64Micro({args[0]})",

            // Type-conversion family (T286b F15). The dialect owns the vendor spelling and the
            // exact result widening the shared row reader needs (Int8 -> Int16, UInt16 -> Int32,
            // Int8 reinterpret -> Int16); the scale and default arguments are validated in the
            // translator, which sees the constant and the overload signature.
            ["to_int8"] = static args => $"toInt16(toInt8({args[0]}))",
            ["to_int8_or_null"] = static args => $"toInt16(toInt8OrNull({args[0]}))",
            ["to_int8_or_default"] = static args => args.Count == 1
            ? $"toInt16(toInt8OrDefault({args[0]}))"
            : $"toInt16(toInt8OrDefault({args[0]}, toInt8({args[1]})))",
            ["to_int16"] = static args => $"toInt16({args[0]})",
            ["to_int16_or_null"] = static args => $"toInt16OrNull({args[0]})",
            ["to_int16_or_default"] = static args => args.Count == 1
            ? $"toInt16OrDefault({args[0]})"
            : $"toInt16OrDefault({args[0]}, toInt16({args[1]}))",
            ["to_int32"] = static args => $"toInt32({args[0]})",
            ["to_int32_or_null"] = static args => $"toInt32OrNull({args[0]})",
            ["to_int32_or_default"] = static args => args.Count == 1
            ? $"toInt32OrDefault({args[0]})"
            : $"toInt32OrDefault({args[0]}, toInt32({args[1]}))",
            ["to_int64"] = static args => $"toInt64({args[0]})",
            ["to_int64_or_null"] = static args => $"toInt64OrNull({args[0]})",
            ["to_int64_or_default"] = static args => args.Count == 1
            ? $"toInt64OrDefault({args[0]})"
            : $"toInt64OrDefault({args[0]}, toInt64({args[1]}))",
            ["to_uint8"] = static args => $"toUInt8({args[0]})",
            ["to_uint8_or_null"] = static args => $"toUInt8OrNull({args[0]})",
            ["to_uint8_or_default"] = static args => args.Count == 1
            ? $"toUInt8OrDefault({args[0]})"
            : $"toUInt8OrDefault({args[0]}, toUInt8({args[1]}))",
            ["to_uint16"] = static args => $"toInt32(toUInt16({args[0]}))",
            ["to_uint16_or_null"] = static args => $"toInt32(toUInt16OrNull({args[0]}))",
            ["to_uint16_or_default"] = static args => args.Count == 1
            ? $"toInt32(toUInt16OrDefault({args[0]}))"
            : $"toInt32(toUInt16OrDefault({args[0]}, toUInt16({args[1]})))",
            ["to_uint32"] = static args => $"toUInt32({args[0]})",
            ["to_uint32_or_null"] = static args => $"toUInt32OrNull({args[0]})",
            ["to_uint32_or_default"] = static args => args.Count == 1
            ? $"toUInt32OrDefault({args[0]})"
            : $"toUInt32OrDefault({args[0]}, toUInt32({args[1]}))",
            ["to_uint64"] = static args => $"toUInt64({args[0]})",
            ["to_uint64_or_null"] = static args => $"toUInt64OrNull({args[0]})",
            ["to_uint64_or_default"] = static args => args.Count == 1
            ? $"toUInt64OrDefault({args[0]})"
            : $"toUInt64OrDefault({args[0]}, toUInt64({args[1]}))",
            ["to_float32"] = static args => $"toFloat32({args[0]})",
            ["to_float32_or_null"] = static args => $"toFloat32OrNull({args[0]})",
            ["to_float32_or_default"] = static args => args.Count == 1
            ? $"toFloat32OrDefault({args[0]})"
            : $"toFloat32OrDefault({args[0]}, toFloat32({args[1]}))",
            ["to_float64"] = static args => $"toFloat64({args[0]})",
            ["to_float64_or_null"] = static args => $"toFloat64OrNull({args[0]})",
            ["to_float64_or_default"] = static args => args.Count == 1
            ? $"toFloat64OrDefault({args[0]})"
            : $"toFloat64OrDefault({args[0]}, toFloat64({args[1]}))",
            ["to_decimal32"] = static args => $"toDecimal32({args[0]}, {args[1]})",
            ["to_decimal32_or_null"] = static args => $"toDecimal32OrNull({args[0]}, {args[1]})",
            ["to_decimal32_or_default"] = static args => args.Count == 2
            ? $"toDecimal32OrDefault({args[0]}, {args[1]})"
            : $"toDecimal32OrDefault({args[0]}, {args[1]}, toDecimal32({args[2]}, {args[1]}))",
            ["to_decimal64"] = static args => $"toDecimal64({args[0]}, {args[1]})",
            ["to_decimal64_or_null"] = static args => $"toDecimal64OrNull({args[0]}, {args[1]})",
            ["to_decimal64_or_default"] = static args => args.Count == 2
            ? $"toDecimal64OrDefault({args[0]}, {args[1]})"
            : $"toDecimal64OrDefault({args[0]}, {args[1]}, toDecimal64({args[2]}, {args[1]}))",
            ["to_string"] = static args => $"toString({args[0]})",
            ["to_bool"] = static args => $"toBool({args[0]})",
            ["to_fixed_string"] = static args => $"toFixedString({args[0]}, {args[1]})",
            ["to_low_cardinality"] = static args => $"toLowCardinality({args[0]})",
            ["accurate_cast_int8"] = static args => $"toInt16(accurateCast({args[0]}, 'Int8'))",
            ["accurate_cast_int8_or_null"] = static args => $"toInt16(accurateCastOrNull({args[0]}, 'Int8'))",
            ["accurate_cast_int8_or_default"] = static args => $"toInt16(accurateCastOrDefault({args[0]}, 'Int8'))",
            ["accurate_cast_int16"] = static args => $"accurateCast({args[0]}, 'Int16')",
            ["accurate_cast_int16_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Int16')",
            ["accurate_cast_int16_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Int16')",
            ["accurate_cast_int32"] = static args => $"accurateCast({args[0]}, 'Int32')",
            ["accurate_cast_int32_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Int32')",
            ["accurate_cast_int32_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Int32')",
            ["accurate_cast_int64"] = static args => $"accurateCast({args[0]}, 'Int64')",
            ["accurate_cast_int64_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Int64')",
            ["accurate_cast_int64_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Int64')",
            ["accurate_cast_uint8"] = static args => $"accurateCast({args[0]}, 'UInt8')",
            ["accurate_cast_uint8_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'UInt8')",
            ["accurate_cast_uint8_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'UInt8')",
            ["accurate_cast_uint16"] = static args => $"toInt32(accurateCast({args[0]}, 'UInt16'))",
            ["accurate_cast_uint16_or_null"] = static args => $"toInt32(accurateCastOrNull({args[0]}, 'UInt16'))",
            ["accurate_cast_uint16_or_default"] = static args => $"toInt32(accurateCastOrDefault({args[0]}, 'UInt16'))",
            ["accurate_cast_uint32"] = static args => $"accurateCast({args[0]}, 'UInt32')",
            ["accurate_cast_uint32_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'UInt32')",
            ["accurate_cast_uint32_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'UInt32')",
            ["accurate_cast_uint64"] = static args => $"accurateCast({args[0]}, 'UInt64')",
            ["accurate_cast_uint64_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'UInt64')",
            ["accurate_cast_uint64_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'UInt64')",
            ["accurate_cast_float32"] = static args => $"accurateCast({args[0]}, 'Float32')",
            ["accurate_cast_float32_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Float32')",
            ["accurate_cast_float32_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Float32')",
            ["accurate_cast_float64"] = static args => $"accurateCast({args[0]}, 'Float64')",
            ["accurate_cast_float64_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Float64')",
            ["accurate_cast_float64_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Float64')",
            ["accurate_cast_decimal32"] = static args => $"accurateCast({args[0]}, 'Decimal(9, {args[1]})')",
            ["accurate_cast_decimal32_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Decimal(9, {args[1]})')",
            ["accurate_cast_decimal32_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Decimal(9, {args[1]})')",
            ["accurate_cast_decimal64"] = static args => $"accurateCast({args[0]}, 'Decimal(18, {args[1]})')",
            ["accurate_cast_decimal64_or_null"] = static args => $"accurateCastOrNull({args[0]}, 'Decimal(18, {args[1]})')",
            ["accurate_cast_decimal64_or_default"] = static args => $"accurateCastOrDefault({args[0]}, 'Decimal(18, {args[1]})')",
            ["reinterpret_as_uint8"] = static args => $"reinterpretAsUInt8({args[0]})",
            ["reinterpret_as_uint16"] = static args => $"toInt32(reinterpretAsUInt16({args[0]}))",
            ["reinterpret_as_uint32"] = static args => $"reinterpretAsUInt32({args[0]})",
            ["reinterpret_as_uint64"] = static args => $"reinterpretAsUInt64({args[0]})",
            ["reinterpret_as_int8"] = static args => $"toInt16(reinterpretAsInt8({args[0]}))",
            ["reinterpret_as_int16"] = static args => $"reinterpretAsInt16({args[0]})",
            ["reinterpret_as_int32"] = static args => $"reinterpretAsInt32({args[0]})",
            ["reinterpret_as_int64"] = static args => $"reinterpretAsInt64({args[0]})",
            ["reinterpret_as_float32"] = static args => $"reinterpretAsFloat32({args[0]})",
            ["reinterpret_as_float64"] = static args => $"reinterpretAsFloat64({args[0]})",
            ["reinterpret_as_date"] = static args => $"reinterpretAsDate({args[0]})",
            ["reinterpret_as_date_time"] = static args => $"reinterpretAsDateTime({args[0]})",
            ["reinterpret_as_string"] = static args => $"reinterpretAsString({args[0]})",
            ["reinterpret_as_fixed_string"] = static args => $"reinterpretAsFixedString({args[0]})",
            ["reinterpret_as_uuid"] = static args => $"reinterpretAsUUID({args[0]})",
        }.ToFrozenDictionary(StringComparer.Ordinal);
    /// <summary>The units <c>toStartOfInterval</c> accepts for a <see cref="DateTime"/> argument.</summary>
    private static readonly Dictionary<string, string> IntervalUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["second"] = "SECOND",
        ["minute"] = "MINUTE",
        ["hour"] = "HOUR",
        ["day"] = "DAY",
        ["week"] = "WEEK",
        ["month"] = "MONTH",
        ["quarter"] = "QUARTER",
        ["year"] = "YEAR"
    };

    private static string StartOfIntervalArm(IReadOnlyList<string> args)
    {
        if (args.Count < 3 || !IntervalUnits.TryGetValue(args[2], out var unit))
            throw new NotSupportedException(
                "The to_start_of_interval unit must be second, minute, hour, day, week, month, quarter " +
                "or year; ClickHouse rejects sub-second units for a DateTime argument.");
        // The anchored (TimescaleDB) form requires the value and the origin to have the same type;
        // both are cast to DateTime so a DateTime64 column and a CLR DateTime parameter agree.
        return args.Count == 4
            ? $"toStartOfInterval(toDateTime({args[0]}), INTERVAL {args[1]} {unit}, toDateTime({args[3]}))"
            : $"toStartOfInterval({args[0]}, INTERVAL {args[1]} {unit})";
    }

    private static string Pad(IReadOnlyList<string> args) => args.Count == 3 ? args[2] : "' '";
}

/// <summary>
/// Renders the explicit null-safe comparison helpers on ClickHouse. The native
/// <c>isNotDistinctFrom</c>/<c>&lt;=&gt;</c> spellings are rejected outside a JOIN ON section (Code 48),
/// so both forms are the guarded ordinary-equality emulation:
/// <c>(a is null and b is null) or (a is not null and b is not null and a = b)</c>, with distinct as its
/// logical complement. No sentinel normalisation is used, so a NULL never compares equal to a real value
/// (including <c>0</c>), and the two-valued result is already <c>UInt8</c> in a scalar position.
/// </summary>
internal sealed class ClickHouseNullSafeComparisonRenderer : INullSafeComparisonRenderer
{
    internal static readonly ClickHouseNullSafeComparisonRenderer Instance = new();

    /// <inheritdoc/>
    public NullSafeComparisonDisposition DistinctDisposition => NullSafeComparisonDisposition.Emulated;

    /// <inheritdoc/>
    public NullSafeComparisonDisposition NotDistinctDisposition => NullSafeComparisonDisposition.Emulated;

    /// <inheritdoc/>
    public string RenderDistinct(string left, string right) => $"(not {Guarded(left, right)})";

    /// <inheritdoc/>
    public string RenderNotDistinct(string left, string right) => Guarded(left, right);

    private static string Guarded(string left, string right) =>
        $"(({left} is null and {right} is null) or ({left} is not null and {right} is not null and {left} = {right}))";
}
