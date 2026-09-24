using System.Globalization;
using System.Text;

namespace NextORM.Core;

/// <summary>
/// Default implementations of the dialect contract. Only the parts that genuinely differ between
/// dialects are abstract (<see cref="MakeParam"/>, <see cref="MakePage"/>); everything else has a
/// working generic-SQL default, so a dialect overrides just what is different. A class-A capability is
/// expressed as a nullable capability object (for example <see cref="Iif"/>) and its <c>Supports*</c>
/// flag is computed from the object's presence, so a dialect cannot report a capability without
/// supplying its rendering; the remaining emitters default to a working generic body and are only
/// overridden where a dialect deviates.
/// </summary>
public abstract class SqlDialectBase : ISqlDialect
{
    /// <inheritdoc/>
    public virtual string ConcatStringOperator => "+";
    /// <inheritdoc/>
    public virtual string EmptyString => "''";
    /// <inheritdoc/>
    public virtual bool RequireSubqueryAlias => false;
    /// <inheritdoc/>
    public virtual bool EnforcesScalarSubqueryCardinality => true;
    /// <inheritdoc/>
    public virtual bool SupportsRightFullJoin => true;
    /// <inheritdoc/>
    public virtual bool SupportsFullJoin => true;
    /// <inheritdoc/>
    public virtual bool SupportsIntersectExceptAll => false;
    /// <inheritdoc/>
    public virtual bool SupportsRollup => false;
    /// <inheritdoc/>
    public virtual bool SupportsCube => false;
    /// <inheritdoc/>
    public virtual bool SupportsGroupingSets => false;
    /// <inheritdoc/>
    public virtual bool SupportsApply => false;
    /// <inheritdoc/>
    public virtual bool SupportsApplyOnPlainTable => false;
    /// <inheritdoc/>
    public virtual bool SupportsJoinStrictness => false;
    /// <inheritdoc/>
    public virtual bool SupportsGlobalJoin => false;
    /// <summary>True when the dialect understands the ClickHouse <c>LEFT SEMI</c>/<c>LEFT ANTI</c> join kinds; the safe default is <c>false</c>.</summary>
    public virtual bool SupportsSemiAntiJoin => false;
    /// <summary>True when the dialect understands the ClickHouse <c>PASTE JOIN</c> kind; the safe default is <c>false</c>.</summary>
    public virtual bool SupportsPasteJoin => false;
    /// <inheritdoc/>
    public virtual bool SupportsQueryHints => false;
    /// <inheritdoc/>
    public virtual bool SupportsTableHints => false;
    /// <inheritdoc/>
    public virtual bool SupportsForJson => false;
    /// <inheritdoc/>
    public virtual bool SupportsForXml => false;
    /// <inheritdoc/>
    public virtual bool SupportsArrays => false;
    /// <inheritdoc/>
    public virtual bool SupportsArrayFunctions => false;
    /// <inheritdoc/>
    public virtual bool SupportsTupleFunctions => Tuple is not null;

    /// <inheritdoc/>
    public virtual ITupleRenderer? Tuple => null;
    /// <inheritdoc/>
    public virtual bool SupportsHigherOrderArrayFunctions => false;
    /// <inheritdoc/>
    public virtual bool SupportsArrayJoin => false;
    /// <inheritdoc/>
    public virtual IStringSplitRenderer? StringSplit => null;

    /// <inheritdoc/>
    public virtual bool SupportsJson => false;
    /// <inheritdoc/>
    public virtual bool SupportsTextJson => false;
    /// <summary>Defaults to <c>null</c>; only SQL Server opts into the postfix XML data-type methods.</summary>
    public virtual IXmlFunctions? XmlFunctions => null;

    /// <inheritdoc/>
    public virtual bool SupportsFullText => false;
    /// <inheritdoc/>
    public virtual bool SupportsFilter => false;
    /// <inheritdoc/>
    public virtual bool SupportsGreatestLeast => false;
    /// <inheritdoc/>
    public virtual bool SupportsPercentRankCumeDist => false;
    /// <inheritdoc/>
    public virtual bool SupportsNthValue => false;
    /// <summary>Defaults to <c>false</c>; SQL Server and MariaDB opt into the window percentile functions.</summary>
    public virtual bool SupportsPercentileWindow => false;
    /// <summary>Defaults to <c>false</c>; PostgreSQL, MySQL, MariaDB, ClickHouse and SQLite opt into named windows.</summary>
    public virtual bool SupportsNamedWindows => false;
    /// <summary>Defaults to <c>false</c>; PostgreSQL, ClickHouse and SQLite opt into the <c>GROUPS</c> frame unit.</summary>
    public virtual bool SupportsWindowFrameGroups => false;
    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQLite opt into the frame <c>EXCLUDE</c> clause.</summary>
    public virtual bool SupportsWindowFrameExclusion => false;
    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the frame-respecting <c>lagInFrame</c>/<c>leadInFrame</c>.</summary>
    public virtual bool SupportsInFrameWindowFunctions => false;
    /// <inheritdoc/>
    public virtual bool SupportsDateTrunc => false;
    /// <inheritdoc/>
    public virtual bool SupportsDateArithmetic => false;
    /// <summary>Defaults to <c>false</c>; PostgreSQL, MySQL and MariaDB opt into a native duration type.</summary>
    public virtual bool SupportsNativeDuration => false;
    /// <summary>Non-native providers store a duration in a <c>bigint</c>; a native provider overrides this with its interval/time type.</summary>
    public virtual string MakeDurationType(DurationUnit? unit, int precision = 0) => "bigint";
    /// <inheritdoc/>
    public virtual IDateConversionRenderer? DateConversion => null;

    /// <inheritdoc/>
    public virtual bool SupportsStringArrayAggregates => false;
    // The umbrella flag seeds the individual capabilities; a dialect opts out of one of them by
    // overriding it (SQL Server has string_agg but no array_agg).
    /// <inheritdoc/>
    public virtual bool SupportsStringAgg => SupportsStringArrayAggregates;
    /// <inheritdoc/>
    public virtual bool SupportsArrayAgg => SupportsStringArrayAggregates;
    /// <inheritdoc/>
    public virtual bool SupportsExtendedScalarFunctions => false;
    /// <summary>Only PostgreSQL has a standalone session random seed (<c>setseed</c>).</summary>
    public virtual bool SupportsRandomSeed => false;
    /// <summary>Only PostgreSQL has the <c>digest</c>/<c>sha256</c> hash surface.</summary>
    public virtual bool SupportsCryptoFunctions => false;

    /// <summary>Defaults to <c>false</c>; PostgreSQL opts into the native text-search scalar surface.</summary>
    public virtual bool SupportsTextSearchFunctions => false;
    /// <summary>Defaults to <c>null</c>; a provider with a native spelling exposes its renderer.</summary>
    public virtual ISessionInfoFunctions? SessionInfoFunctions => null;

    /// <summary>Defaults to <c>null</c>; a provider with native UUID generators exposes its renderer.</summary>
    public virtual IUuidGenerators? UuidGenerators => null;

    /// <inheritdoc/>
    public virtual bool SupportsBooleanAggregates => false;
    /// <inheritdoc/>
    public virtual bool SupportsBitAggregates => false;
    /// <inheritdoc/>
    public virtual bool SupportsStatisticalAggregates => false;
    /// <inheritdoc/>
    public virtual bool SupportsRegressionAggregates => false;
    /// <inheritdoc/>
    public virtual bool SupportsArgMinMax => false;
    /// <inheritdoc/>
    public virtual bool SupportsIfAggregates => false;
    /// <inheritdoc/>
    public virtual IUniqAggregateRenderer? UniqAggregates => null;

    /// <inheritdoc/>
    public virtual IQuantileAggregateRenderer? QuantileAggregates => null;

    /// <summary>Defaults to <c>null</c>; ClickHouse opts into the <c>topK</c>/<c>topKWeighted</c> aggregates.</summary>
    public virtual ITopKAggregateRenderer? TopKAggregates => null;

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the <c>any</c>/<c>anyLast</c> aggregates.</summary>
    public virtual bool SupportsAnyAggregates => false;

    /// <summary>Defaults to <c>false</c>; MySQL/MariaDB and ClickHouse opt into the arbitrary-value aggregate.</summary>
    public virtual bool SupportsAnyValueAggregate => false;

    /// <summary>Defaults to <c>null</c>; ClickHouse opts into the <c>windowFunnel</c>/<c>retention</c>/<c>sequenceMatch</c> aggregates.</summary>
    public virtual ISequenceAggregateRenderer? SequenceAggregates => null;

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the string-JSON <c>JSONExtract*</c> family and the native-JSON functions.</summary>
    public virtual bool SupportsJsonExtract => false;

    /// <summary>Renders <c>name(args)</c>; ClickHouse maps the snake_case name to its native spelling and casts unsigned results.</summary>
    public virtual string MakeJsonExtract(string name, IReadOnlyList<string> args) =>
        $"{name}({string.Join(", ", args)})";
    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the <c>GROUP BY ... WITH TOTALS</c> modifier.</summary>
    public virtual bool SupportsGroupByWithTotals => false;

    /// <summary>Returns the grouping clause unchanged; ClickHouse appends <c> with totals</c>.</summary>
    public virtual string MakeGroupByTotals(string grouping, KeywordCase keywordCase = KeywordCase.Lower) => grouping;
    /// <summary>Defaults to <c>null</c>; every SQL provider exposes its native <c>iif</c> renderer.</summary>
    public virtual IIifRenderer? Iif => null;

    /// <summary>Defaults to <c>false</c>; only SQL Server opts into the <c>choose</c> conditional function.</summary>
    public virtual bool SupportsChoose => false;
    /// <summary>Defaults to <c>null</c>; only ClickHouse opts into the <c>multiIf</c> multi-branch conditional.</summary>
    public virtual IMultiIfRenderer? MultiIf => null;

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the dictionary functions.</summary>
    public virtual bool SupportsDictionaries => false;

    /// <summary>Renders <c>name(args)</c>; ClickHouse maps the snake_case name to its camel-case spelling.</summary>
    public virtual string MakeDictionaryFunction(string name, IReadOnlyList<string> args) =>
        $"{name}({string.Join(", ", args)})";
    /// <inheritdoc/>
    public virtual bool SupportsOrderedAggregates => false;
    /// <inheritdoc/>
    public virtual bool SupportsCommandBehaviorSingleRow => true;
    /// <inheritdoc/>
    public virtual bool SupportsTransactions => true;

    /// <summary>Defaults to <c>null</c>; ClickHouse exposes the <c>LIMIT n BY expr</c> renderer.</summary>
    public virtual ILimitByRenderer? LimitBy => null;

    /// <summary>Defaults to <c>null</c>; PostgreSQL opts into <c>DISTINCT ON</c>.</summary>
    public virtual IDistinctOnRenderer? DistinctOn => null;

    /// <summary>
    /// Wraps the rendered table-function call, or returns it unchanged. ClickHouse uses it to cast the
    /// unsigned <c>numbers</c> column to a type the row reader supports.
    /// </summary>
    public virtual string WrapTableFunction(string name, string call) => call;

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the <c>FINAL</c> modifier.</summary>
    public virtual bool SupportsFinal => false;

    /// <summary>Renders the <c>FINAL</c> modifier; ClickHouse places it right after the table.</summary>
    public virtual string MakeFinal(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " final");

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the <c>SAMPLE</c> modifier.</summary>
    public virtual bool SupportsSample => false;

    /// <summary>Renders the <c>SAMPLE ratio [OFFSET offset]</c> modifier.</summary>
    public virtual string MakeSample(double ratio, double offset, KeywordCase keywordCase = KeywordCase.Lower)
    {
        var text = Kw(keywordCase, " sample ") + ratio.ToString(CultureInfo.InvariantCulture);
        return offset > 0
            ? text + Kw(keywordCase, " offset ") + offset.ToString(CultureInfo.InvariantCulture)
            : text;
    }

    /// <summary>Defaults to <c>null</c>; PostgreSQL and SQL Server opt into <c>TABLESAMPLE</c>.</summary>
    public virtual ITableSampleMethods? TableSample => null;

    /// <summary>Defaults to <c>false</c>; SQL Server and MariaDB opt into <c>FOR SYSTEM_TIME</c>.</summary>
    public virtual bool SupportsTemporalTable => false;

    /// <summary>
    /// Defaults to <c>false</c>; a dialect that set <see cref="SupportsTemporalTable"/> names the kinds
    /// it accepts (MariaDB has no <c>CONTAINED IN</c>).
    /// </summary>
    public virtual bool SupportsTemporalKind(TemporalKind kind) => false;

    /// <summary>
    /// Renders the SQL:2011 <c>FOR SYSTEM_TIME</c> clause (shared by SQL Server and MariaDB). Only
    /// reached through a dialect that set <see cref="SupportsTemporalTable"/>.
    /// </summary>
    public virtual string MakeTemporalTable(TemporalClause clause, KeywordCase keywordCase = KeywordCase.Lower)
    {
        static string Literal(DateTime value) =>
            "'" + value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "'";

        return clause.Kind switch
        {
            TemporalKind.AsOf => Kw(keywordCase, " for system_time as of ") + Literal(clause.From),
            TemporalKind.Between => Kw(keywordCase, " for system_time between ") + Literal(clause.From) + Kw(keywordCase, " and ") + Literal(clause.To),
            TemporalKind.FromTo => Kw(keywordCase, " for system_time from ") + Literal(clause.From) + Kw(keywordCase, " to ") + Literal(clause.To),
            TemporalKind.ContainedIn => Kw(keywordCase, " for system_time contained in (") + Literal(clause.From) + ", " + Literal(clause.To) + ")",
            TemporalKind.All => Kw(keywordCase, " for system_time all"),
            _ => throw new NotSupportedException($"Unknown temporal kind {clause.Kind}.")
        };
    }

    /// <summary>Defaults to <c>null</c>; SQL Server opts into the native <c>PIVOT</c>/<c>UNPIVOT</c> pair.</summary>
    public virtual IPivotRenderer? Pivot => null;

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the <c>PREWHERE</c> clause.</summary>
    public virtual bool SupportsPreWhere => false;

    /// <summary>Defaults to <c>null</c>; ClickHouse opts into the <c>ARRAY JOIN</c> clause.</summary>
    public virtual IArrayJoinRenderer? ArrayJoinClause => null;

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the trailing <c>SETTINGS</c> clause.</summary>
    public virtual bool SupportsSettings => false;

    /// <summary>Renders the trailing <c>SETTINGS key = value, ...</c> clause.</summary>
    public virtual string MakeSettings(IReadOnlyList<KeyValuePair<string, string>> settings, KeywordCase keywordCase = KeywordCase.Lower)
        => Kw(keywordCase, " settings ") + string.Join(", ", settings.Select(static s => s.Key + " = " + s.Value));

    /// <inheritdoc/>
    public abstract string MakeParam(string name);
    /// <inheritdoc/>
    public abstract void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower);

    /// <summary>Resolves a lower-case keyword fragment (keywords and separators only) to the requested <see cref="KeywordCase"/>.</summary>
    protected static string Kw(KeywordCase keywordCase, string text) => SqlKeywords.Of(keywordCase, text);

    // ANSI super-aggregate form. A provider that spells ROLLUP/CUBE as a trailing modifier
    // (MySQL/MariaDB, ClickHouse) overrides this; only reached through a dialect that opted in.
    /// <inheritdoc/>
    public virtual string MakeGrouping(string columns, GroupingType groupingType, KeywordCase keywordCase = KeywordCase.Lower) => groupingType switch
    {
        GroupingType.Rollup => Kw(keywordCase, "rollup (") + columns + ")",
        GroupingType.Cube => Kw(keywordCase, "cube (") + columns + ")",
        _ => columns
    };

    // Reached only through a dialect that set SupportsGroupingSets.
    /// <inheritdoc/>
    public virtual string MakeGroupingSets(IReadOnlyList<string> groupingSets, KeywordCase keywordCase = KeywordCase.Lower) =>
        Kw(keywordCase, "grouping sets (") + string.Join(", ", groupingSets) + ")";

    // ANSI lateral form. A provider whose surface is literally CROSS/OUTER APPLY (SQL Server)
    // overrides this; the base body is only reached through a dialect that opted in with
    // SupportsApply, so it never runs for a provider that cannot express a lateral source.
    /// <inheritdoc/>
    public virtual string MakeApply(JoinType applyType, string source, KeywordCase keywordCase = KeywordCase.Lower) => applyType switch
    {
        JoinType.CrossApply => Kw(keywordCase, " cross join lateral ") + source,
        JoinType.OuterApply => Kw(keywordCase, " left join lateral ") + source + Kw(keywordCase, " on true"),
        _ => throw new ArgumentOutOfRangeException(nameof(applyType), applyType, "Not an APPLY join type")
    };

    // ANSI form. The strictness/GLOBAL modifiers are ClickHouse-only; the SQL builder rejects them
    // for a dialect that did not opt in, so only Default/false reach this body in practice.
    /// <inheritdoc/>
    public virtual string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal, KeywordCase keywordCase = KeywordCase.Lower)
    {
        if (isGlobal)
            throw new NotSupportedException("The GLOBAL join modifier is not supported by this SQL dialect");

        if (strictness is not JoinStrictness.Default)
            throw new NotSupportedException($"The {strictness} join modifier is not supported by this SQL dialect");

        return joinType switch
        {
            JoinType.Inner => SqlKeywords.Of(keywordCase, " join "),
            JoinType.Left => SqlKeywords.Of(keywordCase, " left join "),
            JoinType.Right => SqlKeywords.Of(keywordCase, " right join "),
            JoinType.Full => SqlKeywords.Of(keywordCase, " full join "),
            JoinType.Cross => SqlKeywords.Of(keywordCase, " cross join "),
            JoinType.FullCross => SqlKeywords.Of(keywordCase, " cross join "),
            _ => throw new NotSupportedException(joinType.ToString())
        };
    }

    // ANSI/SQLite/PostgreSQL form: the RECURSIVE modifier is part of the WITH keyword. SQL Server
    // overrides MakeWith to drop it, and MakeMaxRecursion to expose its depth option.
    /// <inheritdoc/>
    public virtual string MakeWith(bool recursive, KeywordCase keywordCase = KeywordCase.Lower)
        => SqlKeywords.Of(keywordCase, recursive ? "with recursive " : "with ");
    /// <inheritdoc/>
    public virtual string? MakeMaxRecursion(int maxRecursion, KeywordCase keywordCase = KeywordCase.Lower) => null;

    // A dialect with a concatenation operator joins the operands with it; a dialect where the
    // operator is not a concatenation overrides this with the concat function.
    /// <inheritdoc/>
    public virtual string MakeConcat(IReadOnlyList<string> parts) => string.Join(ConcatStringOperator, parts);

    /// <inheritdoc/>
    public virtual string Escape(string keyword) => "'" + keyword + "'";
    /// <summary>
    /// Quotes a physical identifier with the ANSI double-quote delimiter, doubling an embedded quote.
    /// A provider with a different delimiter overrides this; the alias-oriented
    /// <see cref="Escape(string)"/> is deliberately not reused because SQLite's single-quoted alias
    /// form is not a valid identifier.
    /// </summary>
    public virtual string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    /// <inheritdoc/>
    public virtual string MakeColumnReference(string name) => name;
    /// <inheritdoc/>
    public virtual string MakeTableAlias(string tableAlias, KeywordCase keywordCase = KeywordCase.Lower)
        => SqlKeywords.Of(keywordCase, " as ") + Escape(tableAlias);
    /// <inheritdoc/>
    public virtual string MakeColumnAlias(string? colAlias, KeywordCase keywordCase = KeywordCase.Lower) => string.IsNullOrEmpty(colAlias)
        ? string.Empty
        : SqlKeywords.Of(keywordCase, " as ") + Escape(colAlias);

    /// <inheritdoc/>
    public virtual string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "smallint",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(int) => "integer",
        _ when type == typeof(long) => "bigint",
        _ when type == typeof(float) => "real",
        _ when type == typeof(double) => "double precision",
        _ when type == typeof(decimal) => "numeric",
        _ when type == typeof(TimeSpan) => "bigint",
        _ when type == typeof(DateTimeOffset) => "timestamp with time zone",
        _ => type.Name
    };

    /// <inheritdoc/>
    public virtual string MakeBool(bool v) => v ? "1" : "0";
    /// <inheritdoc/>
    public virtual string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";
    /// <inheritdoc/>
    public virtual string MakeBoolCoalesce(string v1, string v2) => MakeCoalesce(v1, v2);

    // Dialects with a boolean type can return the ANSI CASE unchanged: it is already a valid
    // scalar and (for the boolean case) a valid predicate.
    /// <inheritdoc/>
    public virtual string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate, KeywordCase keywordCase = KeywordCase.Lower) => caseExpression;
    /// <inheritdoc/>
    public virtual string MakeAggregate(string name) => name;

    // Scalar function defaults are ANSI/portable; a dialect overrides only where it deviates
    // (SQL Server len/datepart, SQLite strftime/datetime, PostgreSQL/ SQLite natural log, ...).
    /// <inheritdoc/>
    public virtual string MakeStringLength(string value) => $"length({value})";
    /// <inheritdoc/>
    public virtual string MakeUpper(string value) => $"upper({value})";
    /// <inheritdoc/>
    public virtual string MakeLower(string value) => $"lower({value})";
    /// <inheritdoc/>
    public virtual string MakeTrim(string value, StringTrimKind kind) => kind switch
    {
        StringTrimKind.Start => $"ltrim({value})",
        StringTrimKind.End => $"rtrim({value})",
        _ => $"trim({value})"
    };
    /// <inheritdoc/>
    public virtual string MakeSubstring(string value, string start, string? length) => length is null
        // C# Substring(start) has no SQL equivalent in the ANSI form, so the remaining length is
        // derived from the value: substring(x, start + 1, length(x) - start).
        ? $"substring({value}, {start} + 1, {MakeStringLength(value)} - ({start}))"
        : $"substring({value}, {start} + 1, {length})";
    /// <inheritdoc/>
    public virtual string MakeReplace(string value, string oldValue, string newValue) =>
        $"replace({value}, {oldValue}, {newValue})";
    // String repetition has no ANSI spelling; every concrete dialect overrides it (replicate/repeat,
    // or a zeroblob trick on SQLite). Reaching the base means a new dialect forgot to opt in.
    /// <inheritdoc/>
    public virtual string MakeRepeat(string value, string count) =>
        throw new NotSupportedException("String repetition is not supported by this provider.");
    /// <inheritdoc/>
    public virtual string MakePad(string value, string length, string pad, bool left)
    {
        var valueLength = MakeStringLength(value);
        var padding = MakeRepeat(pad, $"({length}) - {valueLength}");
        var padded = left ? MakeConcat([padding, value]) : MakeConcat([value, padding]);
        // C# PadLeft/PadRight never truncate a value that is already long enough, while the SQL
        // lpad/rpad family does; guard the length so the two agree.
        return $"case when {valueLength} >= ({length}) then {value} else {padded} end";
    }
    /// <inheritdoc/>
    public virtual string MakeStringIndexOf(string value, string substring, string? start)
    {
        var position = start is null
            ? MakeStringPosition(value, substring)
            : MakeStringPosition(value, substring, start);

        // SQL position is one-based and returns 0 when absent; C# IndexOf is zero-based and -1.
        return $"case when ({position}) = 0 then -1 else ({position}) - 1 end";
    }
    /// <inheritdoc/>
    public virtual string MakeStringLastIndexOf(string value, string substring)
    {
        var position = MakeStringPosition(MakeStringReverse(value), MakeStringReverse(substring));
        var valueLength = MakeStringLength(value);
        var substringLength = MakeStringLength(substring);

        // position is the one-based end of the last match in the reversed value, so the zero-based
        // start of the matching occurrence is valueLength - position - substringLength + 1.
        return $"case when ({position}) = 0 then -1 else {valueLength} - ({position}) - {substringLength} + 1 end";
    }
    /// <inheritdoc/>
    public virtual string MakeStuff(string value, string start, string? count, string newValue)
    {
        var head = MakeSubstring(value, "0", start);

        // count null removes through the end: only the head survives.
        if (count is null)
            return head;

        // No portable stuff/overlay: keep the head, splice the replacement in, and keep the tail that
        // starts at start + count. A zero count inserts at start, so the tail starts at start.
        var tailStart = count == "0" ? start : $"({start}) + ({count})";
        return MakeConcat([head, newValue, MakeSubstring(value, tailStart, null)]);
    }

    /// <summary>
    /// Renders the one-based position of <paramref name="substring"/> in <paramref name="value"/>, or
    /// <c>0</c> when it is absent (<c>charindex</c>, <c>instr</c>, <c>strpos</c>, <c>position</c>).
    /// </summary>
    protected virtual string MakeStringPosition(string value, string substring) =>
        throw new NotSupportedException("String position is not supported by this provider.");

    /// <summary>
    /// Renders the one-based position of <paramref name="substring"/> in <paramref name="value"/>
    /// starting at the already-rendered zero-based <paramref name="start"/>. The default composes the
    /// two-argument primitive over the tail; a dialect with a native start argument overrides it.
    /// </summary>
    protected virtual string MakeStringPosition(string value, string substring, string start)
    {
        var relative = MakeStringPosition(MakeSubstring(value, start, null), substring);

        // relative is one-based within the tail, so the absolute one-based position adds the offset.
        return $"case when ({relative}) = 0 then 0 else ({relative}) + ({start}) end";
    }

    /// <summary>Renders a character-wise reversal of <paramref name="value"/>.</summary>
    protected virtual string MakeStringReverse(string value) =>
        throw new NotSupportedException("String reversal is not supported by this provider.");

    // Reached only through a dialect that opts into formatting; a provider that cannot render CLR
    // format specifiers returns null and the translator rejects the call before this is asked.
    /// <inheritdoc/>
    public virtual IStringFormatFunctions? StringFormats => null;
    /// <inheritdoc/>
    public virtual bool SupportsCollation => false;
    // Reached only through a dialect that set SupportsCollation; the ANSI form is `value collate name`,
    // which the capable providers either reuse or override to quote the name (PostgreSQL).
    /// <inheritdoc/>
    public virtual string MakeCollate(string value, string collation, KeywordCase keywordCase = KeywordCase.Lower) =>
        value + Kw(keywordCase, " collate ") + collation;
    /// <inheritdoc/>
    public virtual bool SupportsOrdinalComparison => false;
    // Reached only through a dialect that set SupportsOrdinalComparison; a dialect that opts in
    // overrides this with its binary-collation rendering.
    /// <inheritdoc/>
    public virtual string MakeOrdinal(string value, bool ignoreCase) =>
        throw new NotSupportedException("Ordinal string comparison is not supported by this provider.");
    /// <inheritdoc/>
    public virtual bool SupportsOrdinalLike => SupportsOrdinalComparison;
    /// <inheritdoc/>
    public virtual string MakeLikeEscape(string escapeChar, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " escape ") + SqlLiteral.ToSqlStringLiteral(escapeChar);
    /// <inheritdoc/>
    public virtual string MakeOnesComplement(string operand) => $"~({operand})";
    // Dialects with a boolean type can use the predicate unchanged as a scalar.
    /// <inheritdoc/>
    public virtual string MakeBooleanPredicate(string predicate, bool asPredicate) => predicate;
    // Dialects with a boolean type can use a boolean value unchanged as a predicate.
    /// <inheritdoc/>
    public virtual string MakeBooleanValuePredicate(string value) => value;
    /// <inheritdoc/>
    public virtual string MakeDatePart(string part, string value) => $"extract({part} from {value})";

    // ANSI date parts the generic extract() can render. A dialect whose native spelling differs for a
    // part overrides MakeDatePart and extends this set (quarter/week/dow/isodow/epoch) as needed.
    private static readonly HashSet<string> DatePartFields = new(StringComparer.Ordinal)
    {
        "year", "quarter", "month", "week", "day", "doy",
        "hour", "minute", "second"
    };
    /// <summary>
    /// True for the ANSI date parts the generic <c>extract</c> can render; a dialect whose native
    /// spelling differs for a part extends this set (quarter/week/dow/isodow/epoch) as needed.
    /// </summary>
    public virtual bool SupportsDatePart(string part) => DatePartFields.Contains(part);
    /// <inheritdoc/>
    public virtual string MakeNow(bool utc) => utc ? "now() at time zone 'utc'" : "now()";
    /// <inheritdoc/>
    public virtual string MakeMathFunction(string name, IReadOnlyList<string> args) =>
        $"{name}({string.Join(", ", args)})";
    /// <summary>
    /// Renders a math function call, receiving the static CLR argument types for dialects whose native
    /// function has a narrower overload set than ANSI. The default ignores the types and delegates to
    /// <see cref="MakeMathFunction(string, IReadOnlyList{string})"/>.
    /// </summary>
    public virtual string MakeMathFunction(string name, IReadOnlyList<string> args, IReadOnlyList<Type> argTypes) =>
        MakeMathFunction(name, args);

    // ANSI/portable defaults. Only reached for the functions their capability flag opts into, so a
    // dialect that does not support one never renders it (the translator rejects the call first).
    /// <inheritdoc/>
    public virtual string MakeNullIf(string value, string other) => $"nullif({value}, {other})";
    /// <inheritdoc/>
    public virtual string MakeGreatest(IReadOnlyList<string> args) => $"greatest({string.Join(", ", args)})";
    /// <inheritdoc/>
    public virtual string MakeLeast(IReadOnlyList<string> args) => $"least({string.Join(", ", args)})";

    /// <inheritdoc/>
    public virtual string MakeDateTrunc(string field, string value) => $"date_trunc('{field}', {value})";
    // ANSI/PostgreSQL interval arithmetic; a dialect with a dedicated dateadd-style function overrides
    // this (SQL Server renders dateadd(field, amount, value)).
    /// <inheritdoc/>
    public virtual string MakeDateAdd(string field, string amount, string value)
    {
        var unit = field switch
        {
            "quarter" => "3 months",
            "millennium" => "1000 years",
            "century" => "100 years",
            "decade" => "10 years",
            _ => "1 " + field
        };

        return $"{value} + ({amount} * interval '{unit}')";
    }
    // ANSI/PostgreSQL difference. Date parts count boundaries (like T-SQL); time parts count whole
    // units from the epoch, which can differ from T-SQL near a boundary. A dialect with a native
    // datediff overrides this (SQL Server renders datediff(field, start, end)).
    /// <inheritdoc/>
    public virtual string MakeDateDiff(string field, string start, string end) => field switch
    {
        "year" => $"(extract(year from {end}) - extract(year from {start}))",
        "quarter" => $"((extract(year from {end}) * 4 + extract(quarter from {end})) - (extract(year from {start}) * 4 + extract(quarter from {start})))",
        "month" => $"((extract(year from {end}) * 12 + extract(month from {end})) - (extract(year from {start}) * 12 + extract(month from {start})))",
        "day" => $"(cast({end} as date) - cast({start} as date))",
        "week" => $"cast(trunc((cast({end} as date) - cast({start} as date)) / 7) as integer)",
        "hour" => $"cast(trunc(extract(epoch from ({end} - {start})) / 3600) as integer)",
        "minute" => $"cast(trunc(extract(epoch from ({end} - {start})) / 60) as integer)",
        "second" => $"cast(trunc(extract(epoch from ({end} - {start}))) as integer)",
        "milliseconds" => $"cast(trunc(extract(epoch from ({end} - {start})) * 1000) as integer)",
        "microseconds" => $"cast(trunc(extract(epoch from ({end} - {start})) * 1000000) as integer)",
        _ => throw new NotSupportedException($"'{field}' is not a supported date_diff field.")
    };
    /// <inheritdoc/>
    public virtual string MakeEndOfMonth(string value) =>
        $"(date_trunc('month', {value}) + interval '1 month - 1 day')";
    /// <inheritdoc/>
    public virtual string MakeDateFromParts(string year, string month, string day) =>
        $"make_date({year}, {month}, {day})";

    // Known date-part names. A dialect whose function rejects some of them (SQL Server/ClickHouse
    // datetrunc have no decade/century/millennium) overrides the matching predicate.
    private static readonly HashSet<string> DateTruncFields = new(StringComparer.Ordinal)
    {
        "microseconds", "milliseconds", "second", "minute", "hour",
        "day", "week", "month", "quarter", "year", "decade", "century", "millennium"
    };
    private static readonly HashSet<string> DateAddFields = new(StringComparer.Ordinal)
    {
        "microseconds", "milliseconds", "second", "minute", "hour",
        "day", "week", "month", "quarter", "year", "decade", "century", "millennium"
    };
    // T-SQL datediff has no decade/century/millennium part, so those are not accepted here.
    private static readonly HashSet<string> DateDiffFields = new(StringComparer.Ordinal)
    {
        "microseconds", "milliseconds", "second", "minute", "hour",
        "day", "week", "month", "quarter", "year"
    };

    /// <inheritdoc/>
    public virtual bool SupportsDateTruncField(string field) => DateTruncFields.Contains(field);
    /// <inheritdoc/>
    public virtual bool SupportsDateAddField(string field) => DateAddFields.Contains(field);
    /// <inheritdoc/>
    public virtual bool SupportsDateDiffField(string field) => DateDiffFields.Contains(field);

    /// <inheritdoc/>
    public virtual string MakeStringAgg(string value, string delimiter) => $"string_agg({value}, {delimiter})";
    /// <inheritdoc/>
    public virtual string MakeArrayAgg(string value) => $"array_agg({value})";
    /// <inheritdoc/>
    public virtual string MakeArrayFunction(string name, string call) => call;

    /// <inheritdoc/>
    public virtual string MakeWithinGroup(string aggregate, string orderBy, KeywordCase keywordCase = KeywordCase.Lower) => aggregate + Kw(keywordCase, " within group (order by ") + orderBy + ")";

    // A user-defined function name is emitted verbatim by default; a dialect that quotes or remaps
    // identifiers overrides this.
    /// <inheritdoc/>
    public virtual string MakeFunction(string name, string? schema)
        => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    // Built-in SqlFunctions.Sql table functions are provider-specific; user-defined [SqlTableFunction]
    // functions are emitted verbatim and never consult this.
    /// <inheritdoc/>
    public virtual bool SupportsTableFunction(string name) => false;

    /// <summary>Defaults to <c>false</c>; every SQL provider overrides it to <c>true</c> (derived tables).</summary>
    public virtual bool SupportsRawSqlSource => false;

    /// <inheritdoc/>
    public virtual string MakeCount(bool distinct, bool big) => distinct ? "count(distinct " : "count(";

    // ClickHouse's count aggregates return UInt64, which the row reader cannot materialise; a dialect
    // that sets the flag overrides WrapCount to cast the rendered expression.
    /// <inheritdoc/>
    public virtual bool WrapsCountResult => false;

    /// <inheritdoc/>
    public virtual string WrapCount(string countExpression, bool big) => countExpression;

    /// <inheritdoc/>
    public virtual string MakeSubqueryPredicate(string keyword, string query, bool asPredicate) => $"{keyword}({query})";

    // ClickHouse's distributed GLOBAL IN predicate; every other dialect rejects it.
    /// <inheritdoc/>
    public virtual bool SupportsGlobalPredicates => false;

    // Reached only through a dialect that set SupportsQueryHints; such a dialect overrides this to
    // place the hints. The base body keeps the contract honest (no throwing placeholder) and lets a
    // provider stage hint support without breaking compilation.
    /// <inheritdoc/>
    public virtual string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption, KeywordCase keywordCase = KeywordCase.Lower)
        => sql;

    // Reached only through a dialect that set SupportsTableHints; the base body is harmless so a
    // dialect can stage the capability without breaking compilation.
    /// <inheritdoc/>
    public virtual string MakeTableHints(IReadOnlyList<string> hints, KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;

    /// <inheritdoc/>
    public virtual IIndexHintRenderer? IndexHints => null;

    // Reached only through a dialect that set SupportsForJson.
    /// <inheritdoc/>
    public virtual string MakeForJson(ForJsonClause clause, KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;

    // Reached only through a dialect that set SupportsForXml.
    /// <inheritdoc/>
    public virtual string MakeForXml(ForXmlClause clause, KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;

    // Reached only through a dialect that set SupportsFullText.
    /// <inheritdoc/>
    public virtual string MakeFullText(string functionName, string column, string search) => string.Empty;

    // Reached only through a dialect that set SupportsTextJson.
    /// <inheritdoc/>
    public virtual string MakeIsJson(string value, bool asPredicate) => string.Empty;

    /// <inheritdoc/>
    public virtual string MakeTextJsonFunction(string name) => name;

    /// <inheritdoc/>
    public virtual string MakeTextJsonFunction(string name, IReadOnlyList<string> args) =>
        $"{MakeTextJsonFunction(name)}({string.Join(", ", args)})";

    /// <inheritdoc/>
    public virtual bool MakeTop(int limit, bool withTies, out string? topStmt, KeywordCase keywordCase = KeywordCase.Lower)
    {
        topStmt = null;
        return false;
    }

    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQL Server opt into <c>WITH TIES</c> paging.</summary>
    public virtual bool SupportsWithTies => false;

    /// <summary>Defaults to <c>null</c>; PostgreSQL, MySQL, MariaDB and SQL Server opt into row locking.</summary>
    public virtual ILockRenderer? Lock => null;

    /// <inheritdoc/>
    public virtual string? GetPagingOrderBy(QueryCommand queryCommand) => null;

    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQLite opt into the <c>RETURNING</c> clause.</summary>
    public virtual bool SupportsReturning => false;
    /// <summary>Defaults to <c>false</c>; SQL Server opts into the <c>OUTPUT</c> clause.</summary>
    public virtual bool SupportsOutput => false;
    /// <summary>Defaults to <c>false</c>; MySQL/MariaDB opt into <c>LAST_INSERT_ID()</c>.</summary>
    public virtual bool SupportsLastInsertId => false;
    /// <summary>Renders <c>RETURNING &lt;columns&gt;</c>; only reached through a dialect that set <see cref="SupportsReturning"/>.</summary>
    public virtual string MakeReturning(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " returning ") + string.Join(", ", columns);
    /// <summary>Renders <c>OUTPUT inserted.&lt;column&gt;</c>; only reached through a dialect that set <see cref="SupportsOutput"/>.</summary>
    public virtual string MakeOutput(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " output ") + string.Join(", ", columns.Select(static c => "inserted." + c));
    /// <summary>Renders <c>OUTPUT deleted.&lt;column&gt; ...</c> for a <c>DELETE</c>; only reached through a dialect that set <see cref="SupportsOutput"/>.</summary>
    public virtual string MakeDeletedOutput(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " output ") + string.Join(", ", columns.Select(static c => "deleted." + c));
    /// <summary>Renders the scalar query for the last generated identity; only reached through a dialect that set <see cref="SupportsLastInsertId"/>.</summary>
    public virtual string MakeLastInsertId(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "select last_insert_rowid()");
    /// <summary>Defaults to <see cref="SupportsLastInsertId"/>; SQL Server and PostgreSQL opt in explicitly.</summary>
    public virtual bool SupportsIdentityFunction => SupportsLastInsertId;
    /// <summary>Renders the scalar query for the last generated identity without naming the column; only reached through a dialect that set <see cref="SupportsIdentityFunction"/>.</summary>
    public virtual string MakeIdentityFunction(KeywordCase keywordCase = KeywordCase.Lower) => MakeLastInsertId(keywordCase);

    /// <summary>Defaults to <c>false</c>; PostgreSQL, SQL Server, MySQL/MariaDB and ClickHouse opt into a native bulk API.</summary>
    public virtual bool SupportsBulkCopy => false;
    /// <summary>Defaults to <c>false</c>; SQLite, MySQL, MariaDB and ClickHouse opt into an <c>INSERT ... IGNORE</c> head.</summary>
    public virtual bool SupportsInsertIgnore => false;
    /// <summary>Renders the <c>INSERT</c> head that skips conflicting rows; MySQL/MariaDB override it with <c>insert ignore into </c>.</summary>
    public virtual string MakeInsertIgnoreInto(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "insert or ignore into ");
    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQLite opt into a trailing <c>ON CONFLICT DO NOTHING</c>.</summary>
    public virtual bool SupportsOnConflictDoNothing => false;
    /// <summary>Renders the trailing <c> ON CONFLICT DO NOTHING</c>; only reached through a dialect that set <see cref="SupportsOnConflictDoNothing"/>.</summary>
    public virtual string MakeOnConflictDoNothing(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " on conflict do nothing");
    /// <summary>Renders the PostgreSQL <c> OVERRIDING SYSTEM VALUE</c> clause; the empty string on dialects that need no clause.</summary>
    public virtual string MakeOverridingSystemValue(KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;
    /// <summary>Defaults to <c>false</c>; SQL Server wraps explicit-identity inserts with <c>SET IDENTITY_INSERT</c>.</summary>
    public virtual bool RequiresIdentityInsertToggle => false;
    /// <summary>Renders the statement that enables explicit identity values for a table; only reached through a dialect that set <see cref="RequiresIdentityInsertToggle"/>.</summary>
    public virtual string MakeIdentityInsertOn(string table, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "set identity_insert ") + table + Kw(keywordCase, " on");
    /// <summary>Renders the statement that disables explicit identity values for a table; only reached through a dialect that set <see cref="RequiresIdentityInsertToggle"/>.</summary>
    public virtual string MakeIdentityInsertOff(string table, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "set identity_insert ") + table + Kw(keywordCase, " off");

    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQLite 3.24+ opt into <c>ON CONFLICT ... DO UPDATE</c>.</summary>
    public virtual bool SupportsOnConflict => false;
    /// <summary>Renders <c> ON CONFLICT (&lt;keys&gt;) DO UPDATE SET </c>; only reached through a dialect that set <see cref="SupportsOnConflict"/>.</summary>
    public virtual string MakeOnConflict(IReadOnlyList<string> keys, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " on conflict (") + string.Join(", ", keys) + Kw(keywordCase, ") do update set ");
    /// <summary>Defaults to <c>false</c>; MySQL and MariaDB opt into <c>ON DUPLICATE KEY UPDATE</c>.</summary>
    public virtual bool SupportsOnDuplicateKey => false;
    /// <summary>Renders <c> ON DUPLICATE KEY UPDATE </c>; only reached through a dialect that set <see cref="SupportsOnDuplicateKey"/>.</summary>
    public virtual string MakeOnDuplicateKey(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " on duplicate key update ");
    /// <summary>Renders the incoming value of a column (<c>excluded.&lt;column&gt;</c>); MySQL/MariaDB override it with <c>VALUES(&lt;column&gt;)</c>. The qualifier is not cased by <see cref="KeywordCase"/>.</summary>
    public virtual string MakeUpsertValueReference(string column, KeywordCase keywordCase = KeywordCase.Lower) => "excluded." + column;
    /// <summary>Defaults to <c>false</c>; SQL Server opts into a key-upsert <c>MERGE</c>.</summary>
    public virtual bool SupportsMerge => false;
    /// <summary>Renders a key-upsert <c>MERGE</c>; only reached through a dialect that set <see cref="SupportsMerge"/>.</summary>
    public virtual string MakeMerge(
        string target,
        IReadOnlyList<string> columns,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> updateColumns,
        string valuesRows,
        KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render a MERGE upsert.");

    /// <summary>Defaults to <c>false</c>; SQL Server and PostgreSQL 15+ opt into the general, multi-branch <c>MERGE</c>.</summary>
    public virtual bool SupportsMergeStatement => false;

    /// <summary>Defaults to <c>false</c>; SQL Server and PostgreSQL opt into a <c>WHEN MATCHED THEN DELETE</c> branch.</summary>
    public virtual bool SupportsMergeDelete => false;

    /// <summary>Defaults to <c>false</c>; only SQL Server opts into <c>WHEN NOT MATCHED BY SOURCE</c>.</summary>
    public virtual bool SupportsMergeBySourceDelete => false;

    /// <summary>Defaults to <c>false</c>; only PostgreSQL accepts <c>THEN DO NOTHING</c> (SQL Server has no such action).</summary>
    public virtual bool SupportsMergeDoNothing => false;

    /// <summary>Defaults to <c>false</c>; SQL Server and PostgreSQL accept an explicit <c>ON &lt;condition&gt;</c> and <c>WHEN ... AND &lt;condition&gt;</c>.</summary>
    public virtual bool SupportsMergeConditionalBranches => false;

    /// <summary>Defaults to <c>true</c>; PostgreSQL overrides it because a <c>MERGE</c> target column must not be qualified.</summary>
    public virtual bool SupportsMergeTargetQualification => true;

    /// <summary>Renders the terminator of a general <c>MERGE</c>; defaults to none, SQL Server requires <c>;</c>.</summary>
    public virtual string MakeMergeStatementTerminator(KeywordCase keywordCase = KeywordCase.Lower) => string.Empty;

    /// <summary>
    /// Renders the <c>RETURNING</c> clause of a general <c>MERGE</c>. Defaults to the ANSI form; PostgreSQL
    /// overrides it to qualify the target columns, whose names would otherwise be ambiguous with the source.
    /// </summary>
    public virtual string MakeMergeReturning(IReadOnlyList<string> columns, KeywordCase keywordCase = KeywordCase.Lower)
        => MakeReturning(columns, keywordCase);

    /// <summary>Defaults to <c>false</c>; only PostgreSQL accepts a data-modifying statement (<c>INSERT ... RETURNING</c>) as a CTE body.</summary>
    public virtual bool SupportsDataModifyingCtes => false;

    /// <summary>Defaults to <c>true</c>; ClickHouse renders its <c>ALTER TABLE ... DELETE</c> mutation through <see cref="MakeDeleteHead"/>.</summary>
    public virtual bool SupportsDelete => true;

    /// <summary>Defaults to <c>false</c>; SQLite has no <c>TRUNCATE</c>, so it keeps the default and callers use <c>DELETE</c>.</summary>
    public virtual bool SupportsTruncate => false;

    /// <summary>Renders <c>TRUNCATE TABLE &lt;table&gt;</c>; only reached through a dialect that set <see cref="SupportsTruncate"/>.</summary>
    public virtual string MakeTruncate(string table, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "truncate table ") + table;

    /// <summary>Renders the delete head; defaults to the ANSI <c>DELETE FROM &lt;table&gt;</c>.</summary>
    public virtual string MakeDeleteHead(string table, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "delete from ") + table;

    /// <summary>Defaults to <c>false</c>; ClickHouse's <c>ALTER TABLE ... DELETE</c> requires a WHERE clause.</summary>
    public virtual bool DeleteRequiresWhere => false;

    /// <summary>Renders a suffix appended after the delete's filter; defaults to none.</summary>
    public virtual string? MakeDeleteSuffix(KeywordCase keywordCase = KeywordCase.Lower) => null;

    /// <summary>Defaults to <c>false</c>; PostgreSQL, SQL Server, MySQL and MariaDB override it for native multi-table deletes.</summary>
    public virtual bool SupportsDeleteJoin => false;

    /// <summary>
    /// Renders the alias-style multi-table delete (<c>DELETE &lt;alias&gt; FROM &lt;target&gt; AS alias JOIN ... WHERE ...</c>),
    /// shared by SQL Server, MySQL and MariaDB. PostgreSQL overrides it with the <c>USING</c> form.
    /// </summary>
    public virtual string MakeDeleteJoin(
        string target,
        string targetAlias,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        var sql = Kw(keywordCase, "delete ") + targetAlias + Kw(keywordCase, " from ") + fromAndJoins;
        return string.IsNullOrEmpty(whereSql) ? sql : sql + Kw(keywordCase, " where ") + whereSql;
    }

    /// <summary>Defaults to <c>false</c>; PostgreSQL overrides it because its multi-table delete uses <c>USING</c>.</summary>
    public virtual bool DeleteJoinRequiresUsing => false;

    /// <summary>Defaults to <c>true</c>; ClickHouse renders its <c>ALTER TABLE ... UPDATE</c> mutation through <see cref="MakeUpdateHead"/>.</summary>
    public virtual bool SupportsUpdate => true;

    /// <summary>Renders the update head; defaults to the ANSI <c>UPDATE &lt;table&gt; SET </c>.</summary>
    public virtual string MakeUpdateHead(string table, KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "update ") + table + Kw(keywordCase, " set ");

    /// <summary>Defaults to <c>false</c>; ClickHouse's <c>ALTER TABLE ... UPDATE</c> requires a WHERE clause.</summary>
    public virtual bool UpdateRequiresWhere => false;

    /// <summary>Renders a suffix appended after the update's filter; defaults to none.</summary>
    public virtual string? MakeUpdateSuffix(KeywordCase keywordCase = KeywordCase.Lower) => null;

    /// <summary>Defaults to <c>false</c>; PostgreSQL, SQLite, SQL Server, MySQL and MariaDB override it for native multi-table updates.</summary>
    public virtual bool SupportsUpdateJoin => false;

    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQLite override it because their multi-table update uses the <c>FROM</c> spelling.</summary>
    public virtual bool UpdateJoinRequiresFrom => false;

    /// <summary>
    /// Renders the <c>FROM</c>-style multi-table update
    /// (<c>UPDATE &lt;target&gt; AS alias SET &lt;assignments&gt; FROM &lt;usingSources&gt; WHERE &lt;joinConditions&gt; [AND ...]</c>),
    /// shared by PostgreSQL and SQLite. The target is aliased in <c>UPDATE</c>, the joined tables are listed
    /// in <c>FROM</c> and the join conditions are folded into the <c>WHERE</c> because a <c>FROM</c> join
    /// condition cannot reference the update target. Only reached through a dialect that set
    /// <see cref="SupportsUpdateJoin"/>; the alias-style dialects override it.
    /// </summary>
    public virtual string MakeUpdateJoin(
        string target,
        string targetAlias,
        string assignments,
        string fromAndJoins,
        string usingSources,
        string joinConditions,
        string? whereSql,
        KeywordCase keywordCase = KeywordCase.Lower)
    {
        if (!UpdateJoinRequiresFrom)
            throw new NotSupportedException($"{GetType().Name} cannot render a multi-table UPDATE.");

        var where = string.IsNullOrEmpty(joinConditions)
            ? whereSql
            : string.IsNullOrEmpty(whereSql)
                ? joinConditions
                : joinConditions + Kw(keywordCase, " and ") + whereSql;

        var sql = Kw(keywordCase, "update ") + target + Kw(keywordCase, " as ") + targetAlias
            + Kw(keywordCase, " set ") + assignments
            + Kw(keywordCase, " from ") + usingSources;

        return string.IsNullOrEmpty(where) ? sql : sql + Kw(keywordCase, " where ") + where;
    }

    /// <summary>Defaults to <c>false</c>; every SQL provider except ClickHouse opts into an all-defaults insert.</summary>
    public virtual bool SupportsDefaultValues => false;
    /// <summary>Renders the all-defaults row form; MySQL/MariaDB override it with <c>() VALUES ()</c>.</summary>
    public virtual string MakeDefaultValues(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, " default values");
    /// <summary>Defaults to <c>false</c>; PostgreSQL, SQL Server, MySQL and MariaDB accept <c>DEFAULT</c> as a value.</summary>
    public virtual bool SupportsColumnDefault => false;
    /// <summary>Renders the <c>DEFAULT</c> keyword as a value; only reached through a dialect that set <see cref="SupportsColumnDefault"/>.</summary>
    public virtual string MakeColumnDefault(KeywordCase keywordCase = KeywordCase.Lower) => Kw(keywordCase, "default");

    /// <summary>Defaults to <c>false</c>; PostgreSQL, SQLite, MySQL, MariaDB, ClickHouse and SQL Server can materialise a query into a table.</summary>
    public virtual bool SupportsCreateTableAsSelect => false;

    /// <summary>Defaults to <see cref="SupportsCreateTableAsSelect"/>; only PostgreSQL, SQLite, MySQL and MariaDB keep a temporary form.</summary>
    public virtual bool SupportsTemporaryCreateTableAsSelect => SupportsCreateTableAsSelect;

    /// <summary>Defaults to <c>false</c>; PostgreSQL, SQLite, MySQL, MariaDB and ClickHouse accept <c>IF NOT EXISTS</c>, SQL Server's <c>SELECT ... INTO</c> does not.</summary>
    public virtual bool SupportsCreateTableAsSelectIfNotExists => false;

    /// <summary>Defaults to <c>false</c>; SQL Server materialises through <c>SELECT ... INTO</c> instead of <c>CREATE TABLE ... AS SELECT</c>.</summary>
    public virtual bool CreateTableAsSelectUsesSelectInto => false;

    /// <summary>Defaults to <c>false</c>; PostgreSQL, MySQL and MariaDB accept a column list with <c>AS SELECT</c>; SQLite, SQL Server and ClickHouse do not.</summary>
    public virtual bool SupportsCreateTableAsSelectColumnList => false;

    /// <summary>Defaults to <c>false</c>; only PostgreSQL accepts <c>ON COMMIT</c> on a temporary table.</summary>
    public virtual bool SupportsCreateTableAsSelectOnCommit => false;

    /// <summary>Defaults to <c>false</c>; only PostgreSQL accepts <c>WITH [NO] DATA</c>.</summary>
    public virtual bool SupportsCreateTableAsSelectWithNoData => false;

    /// <summary>
    /// Renders the clause that introduces the target of a <c>SELECT ... INTO</c> materialisation, inserted
    /// into the select list by the statement builder. Only reached through a dialect that set
    /// <see cref="CreateTableAsSelectUsesSelectInto"/>.
    /// </summary>
    public virtual string MakeCreateTableAsSelectInto(CreateTableAsClause clause, KeywordCase keywordCase = KeywordCase.Lower)
        => throw new NotSupportedException($"{GetType().Name} cannot render a SELECT ... INTO materialisation.");

    /// <summary>
    /// Renders the ANSI/CLI <c>CREATE [TEMPORARY] TABLE [IF NOT EXISTS] &lt;t&gt; [(cols)] AS &lt;select&gt;</c>
    /// form by composing <see cref="MakeCreateTableAsHead"/> and <see cref="MakeCreateTableAsTail"/>.
    /// Only reached through a dialect that set <see cref="SupportsCreateTableAsSelect"/>; PostgreSQL
    /// overrides just those two seams to add <c>ON COMMIT</c> and <c>WITH [NO] DATA</c>.
    /// </summary>
    public virtual string MakeCreateTableAsSelect(CreateTableAsClause clause, string selectSql, KeywordCase keywordCase = KeywordCase.Lower)
        => MakeCreateTableAsHead(clause, keywordCase) + Kw(keywordCase, " as ") + selectSql + MakeCreateTableAsTail(clause, keywordCase);

    /// <summary>
    /// Renders the head of a <c>CREATE ... TABLE ...</c> up to (but not including) <c>AS &lt;select&gt;</c>:
    /// the temporary modifier, <c>IF NOT EXISTS</c>, the resolved table and the optional column list.
    /// </summary>
    /// <param name="clause">The resolved clause options.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered statement head.</returns>
    protected virtual string MakeCreateTableAsHead(CreateTableAsClause clause, KeywordCase keywordCase)
    {
        var writer = new StringBuilder();
        writer.Append(Kw(keywordCase, "create "));
        if (clause.Temporary)
            writer.Append(Kw(keywordCase, "temporary "));

        writer.Append(Kw(keywordCase, "table "));
        if (clause.IfNotExists)
            writer.Append(Kw(keywordCase, "if not exists "));

        writer.Append(clause.Table);

        if (clause.Columns is { Count: > 0 } columns)
            writer.Append(" (").Append(string.Join(", ", columns)).Append(')');

        return writer.ToString();
    }

    /// <summary>
    /// Renders the tail appended after the body query. The base returns nothing; PostgreSQL overrides it
    /// to emit <c>WITH NO DATA</c>.
    /// </summary>
    /// <param name="clause">The resolved clause options.</param>
    /// <param name="keywordCase">The letter case in which SQL keywords are emitted.</param>
    /// <returns>The rendered statement tail.</returns>
    protected virtual string MakeCreateTableAsTail(CreateTableAsClause clause, KeywordCase keywordCase) => string.Empty;
}
