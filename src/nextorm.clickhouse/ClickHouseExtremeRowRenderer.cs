using System.Globalization;
using System.Text;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// ClickHouse's optional native strategy for <c>SelectWhereMax</c>/<c>SelectWhereMin</c>. Both forms
/// pick the winning row(s) with one <c>argMax</c>/<c>argMin</c> aggregate over a payload tuple:
/// <list type="bullet">
/// <item>global: <c>argMax(tuple(payload), key) as t</c> over the filtered source, projecting the tuple
/// elements back to the canonical payload aliases and suppressing the empty-input default row with
/// <c>having count() &gt; 0</c>;</item>
/// <item>grouped: <c>argMax(tuple(payload), key) as t</c> next to the group columns and a
/// <c>group by</c>, so every group yields one whole winner row.</item>
/// </list>
/// A composite key is passed as a lexicographic tuple argument. The shared <c>SqlBuilder</c>
/// still applies the outer projection, <c>DISTINCT</c> and the user's output ordering, so the native
/// aggregate never legalizes user paging and never interleaves the user ordering into winner
/// selection. Nullable keys are excluded before aggregation by the shared source filter and nullable
/// payload fields survive inside the tuple.
/// </summary>
/// <remarks>
/// Eligibility is intentionally narrow. Extreme-key columns must be direct mapped columns bound to
/// <see cref="short"/>/<see cref="int"/>/<see cref="long"/> or floating-point
/// <see cref="float"/>/<see cref="double"/> (including their nullable forms). A purely integral key
/// has no arity cap; once a key carries a floating component it is capped at three components (the
/// proven floating matrix). Group columns stay integral-only; the payload must be integral or
/// <see cref="string"/> columns (including their nullable forms) and admits
/// <see cref="float"/>/<see cref="double"/> only as carriers of a floating extreme key. A floating
/// key component is not passed to
/// <c>argMin</c>/<c>argMax</c> verbatim -
/// ClickHouse seeds the aggregate with the first row and compares NaN false against everything, so a
/// leading NaN would win. Instead the comparison key is adapted in-query: the component becomes a
/// lexicographic pair <c>(isNaN(k)[= 0], k)</c> (the flag leading so NaN ranks last in both
/// directions, matching the portable oracle), with <c>toFloat64(k)</c> widening a
/// <see cref="float"/> component. This is a shape-only decision: no value pre-scan, no public switch
/// and no mutation of the shared command or build context. Everything else - computed expressions,
/// converters, other CLR types - keeps the portable window-function lowering. The decision is made
/// from the prepared description and never touches the build context, so a rejected candidate leaves
/// no state behind.
/// </remarks>
internal sealed class ClickHouseExtremeRowRenderer : IExtremeRowRenderer
{
    public static readonly ClickHouseExtremeRowRenderer Instance = new();

    // Deliberately distinct from the deterministic t1/t2/... aliases the shared builder hands out, so
    // the bases cannot collide with the outer source alias. Both are extended by MakeFreeAliases when a
    // mapped source column legitimately carries the same physical name: the source alias would collide
    // with that column inside the aggregate, and the tuple alias with a projected column of the same
    // name in the derived select.
    private const string SourceAliasBase = "__nextorm_extreme_src";
    private const string TupleAliasBase = "__nextorm_extreme_tuple";

    /// <inheritdoc/>
    public bool CanRender(ExtremeRowDescription description)
    {
        if (description.Keys.Count == 0)
            return false;

        // A floating key column is carried in the payload tuple, so a floating key is what makes a
        // float/double payload carrier admissible. Keep the flag shape-derived (never value-derived)
        // and thread the same decision through every gate so an integral key restores the previous
        // payload/arity behavior.
        var hasFloatingKey = HasFloatingComponent(description.Keys);
        return AreSupportedKeyColumns(description.Keys, hasFloatingKey)
            && AreIntegralDirectColumns(description.Groups)
            && AreSupportedPayloadColumns(description.Payload, hasFloatingKey);
    }

    /// <inheritdoc/>
    public string Render(ExtremeRowRenderRequest request)
    {
        var keywordCase = request.KeywordCase;
        var (sourceAlias, tupleAlias) = MakeFreeAliases(request);
        var aggregate = (request.IsMax ? "argMax" : "argMin")
            + "(" + MakeTuple(request.PayloadAliases) + ", "
            + MakeKeyArgument(request.KeyAliases, request.KeyColumns, request.IsMax) + ")";

        var builder = new StringBuilder();

        if (request.GroupAliases.Count == 0)
        {
            builder.Append(SqlKeywords.Of(keywordCase, "select "))
                .Append(MakeTupleElementProjection(request.PayloadAliases, request.GroupAliases, tupleAlias, keywordCase))
                .AppendLine()
                .Append(SqlKeywords.Of(keywordCase, "from ("))
                .AppendLine().Append(' ')
                .Append(SqlKeywords.Of(keywordCase, "select ")).Append(aggregate)
                .Append(SqlKeywords.Of(keywordCase, " as ")).Append(ClickHouseDialect.Instance.QuoteIdentifier(tupleAlias))
                .AppendLine().Append(' ')
                .Append(SqlKeywords.Of(keywordCase, "from (")).Append(request.SourceSql).Append(')')
                .Append(SqlKeywords.Of(keywordCase, " as ")).Append(ClickHouseDialect.Instance.QuoteIdentifier(sourceAlias))
                .AppendLine().Append(' ')
                .Append(SqlKeywords.Of(keywordCase, "having count() > 0"))
                .Append(')');

            return builder.ToString();
        }

        // Grouped winner: the group columns are selected directly (the outer projection and the user
        // ordering reference them by name), the payload tuple supplies every non-group column.
        var groups = MakeQuotedList(request.GroupAliases);
        var projection = MakeTupleElementProjection(request.PayloadAliases, request.GroupAliases, tupleAlias, keywordCase);

        builder.Append(SqlKeywords.Of(keywordCase, "select ")).Append(groups);
        if (projection.Length > 0)
            builder.Append(", ").Append(projection);

        builder.AppendLine()
            .Append(SqlKeywords.Of(keywordCase, "from ("))
            .AppendLine().Append(' ')
            .Append(SqlKeywords.Of(keywordCase, "select ")).Append(groups).Append(", ").Append(aggregate)
            .Append(SqlKeywords.Of(keywordCase, " as ")).Append(ClickHouseDialect.Instance.QuoteIdentifier(tupleAlias))
            .AppendLine().Append(' ')
            .Append(SqlKeywords.Of(keywordCase, "from (")).Append(request.SourceSql).Append(')')
            .Append(SqlKeywords.Of(keywordCase, " as ")).Append(ClickHouseDialect.Instance.QuoteIdentifier(sourceAlias))
            .AppendLine().Append(' ')
            .Append(SqlKeywords.Of(keywordCase, "group by ")).Append(groups)
            .Append(')');

        return builder.ToString();
    }

    private static bool AreSupportedKeyColumns(IReadOnlyList<ExtremeRowRenderColumn> columns, bool hasFloating)
    {
        // The proven floating allowlist covers single, two- and three-component keys only (see the
        // spike record); a wider key with a floating component is deferred, so it keeps the portable
        // lowering rather than rendering a comparison the repository never verified against the
        // server. A purely integral key was never capped (it stays a plain lexicographic tuple), so
        // the cap must not silently demote integral composites of arity > 3.
        if (hasFloating && columns.Count > 3)
            return false;

        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (!column.IsDirectMappedColumn || column.UsesConverter)
                return false;

            if (!IsIntegralKeyType(column.ClrType) && !IsFloatingKeyType(column.ClrType))
                return false;
        }

        return true;
    }

    private static bool AreIntegralDirectColumns(IReadOnlyList<ExtremeRowRenderColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (!column.IsDirectMappedColumn || column.UsesConverter)
                return false;

            if (!IsIntegralKeyType(column.ClrType))
                return false;
        }

        return true;
    }

    private static bool IsIntegralKeyType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return type == typeof(short) || type == typeof(int) || type == typeof(long);
    }

    private static bool IsFloatingKeyType(Type clrType)
    {
        var type = Nullable.GetUnderlyingType(clrType) ?? clrType;
        return type == typeof(float) || type == typeof(double);
    }

    private static bool IsFloat32KeyType(Type clrType)
        => (Nullable.GetUnderlyingType(clrType) ?? clrType) == typeof(float);

    private static bool AreSupportedPayloadColumns(IReadOnlyList<ExtremeRowRenderColumn> columns, bool hasFloatingKey)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];

            // A converted, computed or Range<T>-expanded payload column is not a plain mapped column
            // and keeps the portable lowering (same rule as the key/group columns).
            if (!column.IsDirectMappedColumn || column.UsesConverter)
                return false;

            var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
            if (type == typeof(string))
                continue;

            if (type == typeof(float) || type == typeof(double))
            {
                // A floating extreme-key column is itself carried in the payload tuple, so a
                // native-eligible floating-key row requires float/double payload carriers too. This
                // widens only the tuple element type; the direct-mapped/no-converter payload rule is
                // unchanged and the key comparison still goes through the NaN adaptation. A purely
                // integral key never needed the widening, so a float/double payload must not flip such
                // a row from portable to native; gate the admission on the key shape.
                if (!hasFloatingKey)
                    return false;

                continue;
            }

            if (type != typeof(sbyte) && type != typeof(byte)
                && type != typeof(short) && type != typeof(ushort)
                && type != typeof(int) && type != typeof(uint)
                && type != typeof(long) && type != typeof(ulong))
                return false;
        }

        return true;
    }

    private static bool HasFloatingComponent(IReadOnlyList<ExtremeRowRenderColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            if (IsFloatingKeyType(columns[i].ClrType))
                return true;
        }

        return false;
    }

    private static (string SourceAlias, string TupleAlias) MakeFreeAliases(ExtremeRowRenderRequest request)
    {
        var taken = new List<string>(
            request.PayloadAliases.Count + request.KeyAliases.Count + request.GroupAliases.Count);
        CollectTaken(request.PayloadAliases, taken);
        CollectTaken(request.KeyAliases, taken);
        CollectTaken(request.GroupAliases, taken);

        var sourceAlias = MakeFreeAlias(SourceAliasBase, taken);

        // The tuple alias is a projected column name, so it must also avoid the source alias just
        // chosen (and, transitively, the source column names already collected).
        taken.Add(sourceAlias);
        var tupleAlias = MakeFreeAlias(TupleAliasBase, taken);

        return (sourceAlias, tupleAlias);
    }

    private static void CollectTaken(IReadOnlyList<string> aliases, List<string> taken)
    {
        for (var i = 0; i < aliases.Count; i++)
        {
            if (!Contains(taken, aliases[i]))
                taken.Add(aliases[i]);
        }
    }

    private static string MakeFreeAlias(string baseAlias, List<string> taken)
    {
        var candidate = baseAlias;
        while (Contains(taken, candidate))
            candidate += "_";

        return candidate;
    }

    private static string MakeTupleElementProjection(
        IReadOnlyList<string> payloadAliases,
        IReadOnlyList<string> groupAliases,
        string tupleAlias,
        KeywordCase keywordCase)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < payloadAliases.Count; i++)
        {
            var alias = payloadAliases[i];
            if (Contains(groupAliases, alias))
                continue;

            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append("tupleElement(").Append(ClickHouseDialect.Instance.QuoteIdentifier(tupleAlias)).Append(", ")
                .Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(')')
                .Append(SqlKeywords.Of(keywordCase, " as ")).Append(ClickHouseDialect.Instance.QuoteIdentifier(alias));
        }

        return builder.ToString();
    }

    private static string MakeTuple(IReadOnlyList<string> names)
    {
        var builder = new StringBuilder("tuple(");
        AppendQuotedList(builder, names);
        return builder.Append(')').ToString();
    }

    /// <summary>
    /// Renders the aggregate's comparison key. A single integral key stays a bare quoted alias; a
    /// composite key (or any key with a floating component) becomes a lexicographic tuple. A floating
    /// component is adapted in place to <c>(isNaN(k)[= 0], k)</c> with <c>toFloat64(k)</c> widening a
    /// <see cref="float"/>, so NaN ranks last in both directions and the ordering matches the portable
    /// oracle.
    /// </summary>
    private static string MakeKeyArgument(
        IReadOnlyList<string> keyAliases,
        IReadOnlyList<ExtremeRowRenderColumn> keyColumns,
        bool isMax)
    {
        // The aliases and the column shapes are positionally aligned by the shared builder; a
        // mismatch is a programming error, not a server decision, so fail fast and cleanly instead of
        // letting the positional lookup below throw IndexOutOfRangeException.
        if (keyColumns.Count != keyAliases.Count)
            throw new InvalidOperationException(
                "The extreme-row key columns and key aliases must be positionally aligned.");

        var hasFloating = HasFloatingComponent(keyColumns);

        if (keyAliases.Count == 1 && !hasFloating)
            return ClickHouseDialect.Instance.QuoteIdentifier(keyAliases[0]);

        var builder = new StringBuilder("(");
        for (var i = 0; i < keyAliases.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            AppendKeyComponent(builder, keyAliases[i], keyColumns[i], isMax);
        }

        return builder.Append(')').ToString();
    }

    private static void AppendKeyComponent(
        StringBuilder builder,
        string alias,
        ExtremeRowRenderColumn column,
        bool isMax)
    {
        var quoted = ClickHouseDialect.Instance.QuoteIdentifier(alias);

        if (!IsFloatingKeyType(column.ClrType))
        {
            builder.Append(quoted);
            return;
        }

        // Flag first so NaN always ranks last, regardless of the aggregate direction; <= 0 for Max,
        // >= 0 for Min. A finite Float32 NaN check must precede the widening, so the widened value is
        // only the tie-breaker.
        builder.Append("isNaN(").Append(quoted).Append(')');
        if (isMax)
            builder.Append(" = 0");

        builder.Append(", ");
        if (IsFloat32KeyType(column.ClrType))
            builder.Append("toFloat64(").Append(quoted).Append(')');
        else
            builder.Append(quoted);
    }

    private static string MakeQuotedList(IReadOnlyList<string> names)
    {
        var builder = new StringBuilder();
        AppendQuotedList(builder, names);
        return builder.ToString();
    }

    private static void AppendQuotedList(StringBuilder builder, IReadOnlyList<string> names)
    {
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append(ClickHouseDialect.Instance.QuoteIdentifier(names[i]));
        }
    }

    private static bool Contains(IReadOnlyList<string> values, string value)
    {
        for (var i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
