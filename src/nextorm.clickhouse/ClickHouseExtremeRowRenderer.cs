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
/// A composite key is passed as a lexicographic tuple argument. The shared <see cref="SqlBuilder"/>
/// still applies the outer projection, <c>DISTINCT</c> and the user's output ordering, so the native
/// aggregate never legalizes user paging and never interleaves the user ordering into winner
/// selection. Nullable keys are excluded before aggregation by the shared source filter and nullable
/// payload fields survive inside the tuple.
/// </summary>
/// <remarks>
/// Eligibility is intentionally narrow: only direct mapped key/group columns bound to
/// <see cref="short"/>/<see cref="int"/>/<see cref="long"/> (including their nullable forms) and a
/// payload of integral or <see cref="string"/> columns (including their nullable forms) are rendered
/// natively. Floating-point keys are rejected as a whole because their ordering is not portable.
/// Everything else - computed expressions, converters, other CLR types - keeps the portable
/// window-function lowering. The decision is made from the prepared description and never touches the
/// build context, so a rejected candidate leaves no state behind.
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
        => description.Keys.Count > 0
            && AreIntegralDirectColumns(description.Keys)
            && AreIntegralDirectColumns(description.Groups)
            && AreSupportedPayloadColumns(description.Payload);

    /// <inheritdoc/>
    public string Render(ExtremeRowRenderRequest request)
    {
        var keywordCase = request.KeywordCase;
        var (sourceAlias, tupleAlias) = MakeFreeAliases(request);
        var aggregate = (request.IsMax ? "argMax" : "argMin")
            + "(" + MakeTuple(request.PayloadAliases) + ", " + MakeKeyArgument(request.KeyAliases) + ")";

        var builder = new StringBuilder();

        if (request.GroupAliases.Count == 0)
        {
            builder.Append(SqlKeywords.Of(keywordCase, "select "))
                .Append(MakeTupleElementProjection(request.PayloadAliases, request.GroupAliases, tupleAlias, keywordCase))
                .AppendLine()
                .Append(SqlKeywords.Of(keywordCase, "from ("))
                .AppendLine().Append(' ')
                .Append(SqlKeywords.Of(keywordCase, "select ")).Append(aggregate)
                .Append(SqlKeywords.Of(keywordCase, " as ")).Append(Quote(tupleAlias))
                .AppendLine().Append(' ')
                .Append(SqlKeywords.Of(keywordCase, "from (")).Append(request.SourceSql).Append(')')
                .Append(SqlKeywords.Of(keywordCase, " as ")).Append(Quote(sourceAlias))
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
            .Append(SqlKeywords.Of(keywordCase, " as ")).Append(Quote(tupleAlias))
            .AppendLine().Append(' ')
            .Append(SqlKeywords.Of(keywordCase, "from (")).Append(request.SourceSql).Append(')')
            .Append(SqlKeywords.Of(keywordCase, " as ")).Append(Quote(sourceAlias))
            .AppendLine().Append(' ')
            .Append(SqlKeywords.Of(keywordCase, "group by ")).Append(groups)
            .Append(')');

        return builder.ToString();
    }

    private static bool AreIntegralDirectColumns(IReadOnlyList<ExtremeRowRenderColumn> columns)
    {
        for (var i = 0; i < columns.Count; i++)
        {
            var column = columns[i];
            if (!column.IsDirectMappedColumn || column.UsesConverter)
                return false;

            var type = Nullable.GetUnderlyingType(column.ClrType) ?? column.ClrType;
            if (type != typeof(short) && type != typeof(int) && type != typeof(long))
                return false;
        }

        return true;
    }

    private static bool AreSupportedPayloadColumns(IReadOnlyList<ExtremeRowRenderColumn> columns)
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

            if (type != typeof(sbyte) && type != typeof(byte)
                && type != typeof(short) && type != typeof(ushort)
                && type != typeof(int) && type != typeof(uint)
                && type != typeof(long) && type != typeof(ulong))
                return false;
        }

        return true;
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

            builder.Append("tupleElement(").Append(Quote(tupleAlias)).Append(", ")
                .Append((i + 1).ToString(CultureInfo.InvariantCulture)).Append(')')
                .Append(SqlKeywords.Of(keywordCase, " as ")).Append(Quote(alias));
        }

        return builder.ToString();
    }

    private static string MakeTuple(IReadOnlyList<string> names)
    {
        var builder = new StringBuilder("tuple(");
        AppendQuotedList(builder, names);
        return builder.Append(')').ToString();
    }

    private static string MakeKeyArgument(IReadOnlyList<string> keyAliases)
        => keyAliases.Count == 1 ? Quote(keyAliases[0]) : MakeRow(keyAliases);

    private static string MakeRow(IReadOnlyList<string> names)
    {
        var builder = new StringBuilder("(");
        AppendQuotedList(builder, names);
        return builder.Append(')').ToString();
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

            builder.Append(Quote(names[i]));
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

    private static string Quote(string name) => "`" + name.Replace("`", "``") + "`";
}
