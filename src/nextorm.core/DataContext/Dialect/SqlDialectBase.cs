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
    public virtual string ConcatStringOperator => "+";
    public virtual string EmptyString => "''";
    public virtual bool RequireSubqueryAlias => false;
    public virtual bool EnforcesScalarSubqueryCardinality => true;
    public virtual bool SupportsRightFullJoin => true;
    public virtual bool SupportsFullJoin => true;
    public virtual bool SupportsIntersectExceptAll => false;
    public virtual bool SupportsRollup => false;
    public virtual bool SupportsCube => false;
    public virtual bool SupportsGroupingSets => false;
    public virtual bool SupportsApply => false;
    public virtual bool SupportsJoinStrictness => false;
    public virtual bool SupportsGlobalJoin => false;
    public virtual bool SupportsQueryHints => false;
    public virtual bool SupportsTableHints => false;
    public virtual bool SupportsForJson => false;
    public virtual bool SupportsForXml => false;
    public virtual bool SupportsArrays => false;
    public virtual bool SupportsArrayFunctions => false;
    public virtual bool SupportsTupleFunctions => false;
    public virtual bool SupportsHigherOrderArrayFunctions => false;
    public virtual bool SupportsArrayJoin => false;
    public virtual IStringSplitRenderer? StringSplit => null;

    public virtual bool SupportsJson => false;
    public virtual bool SupportsTextJson => false;
    /// <summary>Defaults to <c>null</c>; only SQL Server opts into the postfix XML data-type methods.</summary>
    public virtual IXmlFunctions? XmlFunctions => null;

    public virtual bool SupportsFullText => false;
    public virtual bool SupportsFilter => false;
    public virtual bool SupportsGreatestLeast => false;
    public virtual bool SupportsPercentRankCumeDist => false;
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
    public virtual bool SupportsDateTrunc => false;
    public virtual bool SupportsDateArithmetic => false;
    public virtual IDateConversionRenderer? DateConversion => null;

    public virtual bool SupportsStringArrayAggregates => false;
    // The umbrella flag seeds the individual capabilities; a dialect opts out of one of them by
    // overriding it (SQL Server has string_agg but no array_agg).
    public virtual bool SupportsStringAgg => SupportsStringArrayAggregates;
    public virtual bool SupportsArrayAgg => SupportsStringArrayAggregates;
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

    public virtual bool SupportsBooleanAggregates => false;
    public virtual bool SupportsBitAggregates => false;
    public virtual bool SupportsStatisticalAggregates => false;
    public virtual bool SupportsRegressionAggregates => false;
    public virtual bool SupportsArgMinMax => false;
    public virtual bool SupportsIfAggregates => false;
    public virtual IUniqAggregateRenderer? UniqAggregates => null;

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
    public virtual string MakeGroupByTotals(string grouping) => grouping;
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
    public virtual bool SupportsOrderedAggregates => false;
    public virtual bool SupportsCommandBehaviorSingleRow => true;

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
    public virtual string MakeFinal() => " final";

    /// <summary>Defaults to <c>false</c>; ClickHouse opts into the <c>SAMPLE</c> modifier.</summary>
    public virtual bool SupportsSample => false;

    /// <summary>Renders the <c>SAMPLE ratio [OFFSET offset]</c> modifier.</summary>
    public virtual string MakeSample(double ratio, double offset)
    {
        var text = " sample " + ratio.ToString(CultureInfo.InvariantCulture);
        return offset > 0
            ? text + " offset " + offset.ToString(CultureInfo.InvariantCulture)
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
    public virtual string MakeTemporalTable(TemporalClause clause)
    {
        static string Literal(DateTime value) =>
            "'" + value.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) + "'";

        return clause.Kind switch
        {
            TemporalKind.AsOf => " for system_time as of " + Literal(clause.From),
            TemporalKind.Between => " for system_time between " + Literal(clause.From) + " and " + Literal(clause.To),
            TemporalKind.FromTo => " for system_time from " + Literal(clause.From) + " to " + Literal(clause.To),
            TemporalKind.ContainedIn => " for system_time contained in (" + Literal(clause.From) + ", " + Literal(clause.To) + ")",
            TemporalKind.All => " for system_time all",
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
    public virtual string MakeSettings(IReadOnlyList<KeyValuePair<string, string>> settings)
        => " settings " + string.Join(", ", settings.Select(static s => s.Key + " = " + s.Value));

    public abstract string MakeParam(string name);
    public abstract void MakePage(Paging paging, StringBuilder sqlBuilder);

    // ANSI super-aggregate form. A provider that spells ROLLUP/CUBE as a trailing modifier
    // (MySQL/MariaDB, ClickHouse) overrides this; only reached through a dialect that opted in.
    public virtual string MakeGrouping(string columns, GroupingType groupingType) => groupingType switch
    {
        GroupingType.Rollup => $"rollup ({columns})",
        GroupingType.Cube => $"cube ({columns})",
        _ => columns
    };

    // Reached only through a dialect that set SupportsGroupingSets.
    public virtual string MakeGroupingSets(IReadOnlyList<string> groupingSets) =>
        $"grouping sets ({string.Join(", ", groupingSets)})";

    // ANSI lateral form. A provider whose surface is literally CROSS/OUTER APPLY (SQL Server)
    // overrides this; the base body is only reached through a dialect that opted in with
    // SupportsApply, so it never runs for a provider that cannot express a lateral source.
    public virtual string MakeApply(JoinType applyType, string source) => applyType switch
    {
        JoinType.CrossApply => $" cross join lateral {source}",
        JoinType.OuterApply => $" left join lateral {source} on true",
        _ => throw new ArgumentOutOfRangeException(nameof(applyType), applyType, "Not an APPLY join type")
    };

    // ANSI form. The strictness/GLOBAL modifiers are ClickHouse-only; the SQL builder rejects them
    // for a dialect that did not opt in, so only Default/false reach this body in practice.
    public virtual string MakeJoinKeyword(JoinType joinType, JoinStrictness strictness, bool isGlobal)
    {
        if (isGlobal)
            throw new NotSupportedException("The GLOBAL join modifier is not supported by this SQL dialect");

        if (strictness is not JoinStrictness.Default)
            throw new NotSupportedException($"The {strictness} join modifier is not supported by this SQL dialect");

        return joinType switch
        {
            JoinType.Inner => " join ",
            JoinType.Left => " left join ",
            JoinType.Right => " right join ",
            JoinType.Full => " full join ",
            JoinType.Cross => " cross join ",
            JoinType.FullCross => " cross join ",
            _ => throw new NotSupportedException(joinType.ToString())
        };
    }

    // ANSI/SQLite/PostgreSQL form: the RECURSIVE modifier is part of the WITH keyword. SQL Server
    // overrides MakeWith to drop it, and MakeMaxRecursion to expose its depth option.
    public virtual string MakeWith(bool recursive) => recursive ? "with recursive " : "with ";
    public virtual string? MakeMaxRecursion(int maxRecursion) => null;

    // A dialect with a concatenation operator joins the operands with it; a dialect where the
    // operator is not a concatenation overrides this with the concat function.
    public virtual string MakeConcat(IReadOnlyList<string> parts) => string.Join(ConcatStringOperator, parts);

    public virtual string Escape(string keyword) => "'" + keyword + "'";
    /// <summary>
    /// Quotes a physical identifier with the ANSI double-quote delimiter, doubling an embedded quote.
    /// A provider with a different delimiter overrides this; the alias-oriented
    /// <see cref="Escape(string)"/> is deliberately not reused because SQLite's single-quoted alias
    /// form is not a valid identifier.
    /// </summary>
    public virtual string QuoteIdentifier(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    public virtual string MakeColumnReference(string name) => name;
    public virtual string MakeTableAlias(string tableAlias) => " as " + Escape(tableAlias);
    public virtual string MakeColumnAlias(string? colAlias) => string.IsNullOrEmpty(colAlias)
        ? string.Empty
        : " as " + Escape(colAlias);

    public virtual string MakeTypeName(Type type) => type switch
    {
        _ when type == typeof(byte) => "smallint",
        _ when type == typeof(short) => "smallint",
        _ when type == typeof(int) => "integer",
        _ when type == typeof(long) => "bigint",
        _ when type == typeof(float) => "real",
        _ when type == typeof(double) => "double precision",
        _ when type == typeof(decimal) => "numeric",
        _ => type.Name
    };

    public virtual string MakeBool(bool v) => v ? "1" : "0";
    public virtual string MakeCoalesce(string v1, string v2) => $"coalesce({v1}, {v2})";
    public virtual string MakeBoolCoalesce(string v1, string v2) => MakeCoalesce(v1, v2);

    // Dialects with a boolean type can return the ANSI CASE unchanged: it is already a valid
    // scalar and (for the boolean case) a valid predicate.
    public virtual string MakeCase(string caseExpression, bool isBooleanResult, bool asPredicate) => caseExpression;
    public virtual string MakeAggregate(string name) => name;

    // Scalar function defaults are ANSI/portable; a dialect overrides only where it deviates
    // (SQL Server len/datepart, SQLite strftime/datetime, PostgreSQL/ SQLite natural log, ...).
    public virtual string MakeStringLength(string value) => $"length({value})";
    public virtual string MakeUpper(string value) => $"upper({value})";
    public virtual string MakeLower(string value) => $"lower({value})";
    public virtual string MakeTrim(string value, StringTrimKind kind) => kind switch
    {
        StringTrimKind.Start => $"ltrim({value})",
        StringTrimKind.End => $"rtrim({value})",
        _ => $"trim({value})"
    };
    public virtual string MakeSubstring(string value, string start, string? length) => length is null
        // C# Substring(start) has no SQL equivalent in the ANSI form, so the remaining length is
        // derived from the value: substring(x, start + 1, length(x) - start).
        ? $"substring({value}, {start} + 1, {MakeStringLength(value)} - ({start}))"
        : $"substring({value}, {start} + 1, {length})";
    public virtual string MakeReplace(string value, string oldValue, string newValue) =>
        $"replace({value}, {oldValue}, {newValue})";
    // String repetition has no ANSI spelling; every concrete dialect overrides it (replicate/repeat,
    // or a zeroblob trick on SQLite). Reaching the base means a new dialect forgot to opt in.
    public virtual string MakeRepeat(string value, string count) =>
        throw new NotSupportedException("String repetition is not supported by this provider.");
    public virtual string MakePad(string value, string length, string pad, bool left)
    {
        var valueLength = MakeStringLength(value);
        var padding = MakeRepeat(pad, $"({length}) - {valueLength}");
        var padded = left ? MakeConcat([padding, value]) : MakeConcat([value, padding]);
        // C# PadLeft/PadRight never truncate a value that is already long enough, while the SQL
        // lpad/rpad family does; guard the length so the two agree.
        return $"case when {valueLength} >= ({length}) then {value} else {padded} end";
    }
    public virtual string MakeStringIndexOf(string value, string substring, string? start)
    {
        var position = start is null
            ? MakeStringPosition(value, substring)
            : MakeStringPosition(value, substring, start);

        // SQL position is one-based and returns 0 when absent; C# IndexOf is zero-based and -1.
        return $"case when ({position}) = 0 then -1 else ({position}) - 1 end";
    }
    public virtual string MakeStringLastIndexOf(string value, string substring)
    {
        var position = MakeStringPosition(MakeStringReverse(value), MakeStringReverse(substring));
        var valueLength = MakeStringLength(value);
        var substringLength = MakeStringLength(substring);

        // position is the one-based end of the last match in the reversed value, so the zero-based
        // start of the matching occurrence is valueLength - position - substringLength + 1.
        return $"case when ({position}) = 0 then -1 else {valueLength} - ({position}) - {substringLength} + 1 end";
    }
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
    public virtual string MakeLikeEscape(string escapeChar) => " escape " + SqlLiteral.ToSqlStringLiteral(escapeChar);
    public virtual string MakeOnesComplement(string operand) => $"~({operand})";
    // Dialects with a boolean type can use the predicate unchanged as a scalar.
    public virtual string MakeBooleanPredicate(string predicate, bool asPredicate) => predicate;
    // Dialects with a boolean type can use a boolean value unchanged as a predicate.
    public virtual string MakeBooleanValuePredicate(string value) => value;
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
    public virtual string MakeNow(bool utc) => utc ? "now() at time zone 'utc'" : "now()";
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
    public virtual string MakeNullIf(string value, string other) => $"nullif({value}, {other})";
    public virtual string MakeGreatest(IReadOnlyList<string> args) => $"greatest({string.Join(", ", args)})";
    public virtual string MakeLeast(IReadOnlyList<string> args) => $"least({string.Join(", ", args)})";

    public virtual string MakeDateTrunc(string field, string value) => $"date_trunc('{field}', {value})";
    // ANSI/PostgreSQL interval arithmetic; a dialect with a dedicated dateadd-style function overrides
    // this (SQL Server renders dateadd(field, amount, value)).
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
    public virtual string MakeEndOfMonth(string value) =>
        $"(date_trunc('month', {value}) + interval '1 month - 1 day')";
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

    public virtual bool SupportsDateTruncField(string field) => DateTruncFields.Contains(field);
    public virtual bool SupportsDateAddField(string field) => DateAddFields.Contains(field);
    public virtual bool SupportsDateDiffField(string field) => DateDiffFields.Contains(field);

    public virtual string MakeStringAgg(string value, string delimiter) => $"string_agg({value}, {delimiter})";
    public virtual string MakeArrayAgg(string value) => $"array_agg({value})";
    public virtual string MakeArrayFunction(string name, string call) => call;

    public virtual string MakeWithinGroup(string aggregate, string orderBy) => $"{aggregate} within group (order by {orderBy})";

    // A user-defined function name is emitted verbatim by default; a dialect that quotes or remaps
    // identifiers overrides this.
    public virtual string MakeFunction(string name, string? schema)
        => string.IsNullOrEmpty(schema) ? name : $"{schema}.{name}";

    // Built-in SqlFunctions.Sql table functions are provider-specific; user-defined [SqlTableFunction]
    // functions are emitted verbatim and never consult this.
    public virtual bool SupportsTableFunction(string name) => false;

    /// <summary>Defaults to <c>false</c>; every SQL provider overrides it to <c>true</c> (derived tables).</summary>
    public virtual bool SupportsRawSqlSource => false;

    public virtual string MakeCount(bool distinct, bool big) => distinct ? "count(distinct " : "count(";

    // ClickHouse's count aggregates return UInt64, which the row reader cannot materialise; a dialect
    // that sets the flag overrides WrapCount to cast the rendered expression.
    public virtual bool WrapsCountResult => false;

    public virtual string WrapCount(string countExpression, bool big) => countExpression;

    public virtual string MakeSubqueryPredicate(string keyword, string query, bool asPredicate) => $"{keyword}({query})";

    // ClickHouse's distributed GLOBAL IN predicate; every other dialect rejects it.
    public virtual bool SupportsGlobalPredicates => false;

    // Reached only through a dialect that set SupportsQueryHints; such a dialect overrides this to
    // place the hints. The base body keeps the contract honest (no throwing placeholder) and lets a
    // provider stage hint support without breaking compilation.
    public virtual string RenderQueryHints(string sql, IReadOnlyList<string> hints, string? maxRecursionOption)
        => sql;

    // Reached only through a dialect that set SupportsTableHints; the base body is harmless so a
    // dialect can stage the capability without breaking compilation.
    public virtual string MakeTableHints(IReadOnlyList<string> hints) => string.Empty;

    // Reached only through a dialect that set SupportsForJson.
    public virtual string MakeForJson(ForJsonClause clause) => string.Empty;

    // Reached only through a dialect that set SupportsForXml.
    public virtual string MakeForXml(ForXmlClause clause) => string.Empty;

    // Reached only through a dialect that set SupportsFullText.
    public virtual string MakeFullText(string functionName, string column, string search) => string.Empty;

    // Reached only through a dialect that set SupportsTextJson.
    public virtual string MakeIsJson(string value, bool asPredicate) => string.Empty;

    public virtual string MakeTextJsonFunction(string name) => name;

    public virtual string MakeTextJsonFunction(string name, IReadOnlyList<string> args) =>
        $"{MakeTextJsonFunction(name)}({string.Join(", ", args)})";

    public virtual bool MakeTop(int limit, bool withTies, out string? topStmt)
    {
        topStmt = null;
        return false;
    }

    /// <summary>Defaults to <c>false</c>; PostgreSQL and SQL Server opt into <c>WITH TIES</c> paging.</summary>
    public virtual bool SupportsWithTies => false;

    /// <summary>Defaults to <c>null</c>; PostgreSQL, MySQL, MariaDB and SQL Server opt into row locking.</summary>
    public virtual ILockRenderer? Lock => null;

    public virtual string? GetPagingOrderBy(QueryCommand queryCommand) => null;
}
