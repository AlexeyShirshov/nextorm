using System.Text;
using NextORM.Core;

namespace NextORM.Postgres;

/// <summary>
/// PostgreSQL's optional native strategy for <c>SelectWhereMax</c>/<c>SelectWhereMin</c>. The grouped
/// form is a <c>DISTINCT ON (&lt;group&gt;) ... ORDER BY &lt;group&gt;, &lt;extreme key&gt;</c> that
/// keeps the extreme row per group; the global form appends an inner
/// <c>ORDER BY &lt;extreme key&gt; LIMIT 1</c> to the filtered source. The shared
/// <c>SqlBuilder</c> still applies the outer projection, <c>DISTINCT</c> and the user's output
/// ordering, so the native clauses never legalize user paging and never interleave the user ordering
/// into winner selection.
/// </summary>
/// <remarks>
/// Eligibility is intentionally narrow: only direct mapped key/group columns bound to
/// <see cref="short"/>/<see cref="int"/>/<see cref="long"/> (including their nullable forms) are
/// rendered natively. Everything else - computed expressions, converters and other CLR types - keeps
/// the portable window-function lowering. The decision is made from the prepared description and never
/// touches the build context, so a rejected candidate leaves no state behind.
/// </remarks>
internal sealed class PostgresExtremeRowRenderer : IExtremeRowRenderer
{
    public static readonly PostgresExtremeRowRenderer Instance = new();

    // A derived table in FROM requires an alias in PostgreSQL. The base is deliberately distinct from
    // the deterministic t1/t2/... aliases the shared builder hands out, so it cannot collide with the
    // outer source alias. It is additionally extended by MakeFreeAlias when a mapped source column
    // legitimately carries the same physical name: without that, the ORDER BY on that column would be
    // ambiguous between the column and the derived-table alias.
    private const string DerivedAliasBase = "__nextorm_extreme";

    /// <inheritdoc/>
    public bool CanRender(ExtremeRowDescription description)
        => description.Keys.Count > 0
            && AreIntegralDirectColumns(description.Keys)
            && AreIntegralDirectColumns(description.Groups);

    /// <inheritdoc/>
    public string Render(ExtremeRowRenderRequest request)
    {
        var keywordCase = request.KeywordCase;
        var keyOrder = MakeOrderList(request.KeyAliases, request.IsMax, keywordCase);

        if (request.GroupAliases.Count == 0)
        {
            // Global winner: the filtered source already projects * so the canonical payload aliases are
            // exposed; the inner ORDER BY ... LIMIT 1 picks the single extreme row.
            return request.SourceSql
                + SqlKeywords.Of(keywordCase, " order by ") + keyOrder
                + SqlKeywords.Of(keywordCase, " limit 1");
        }

        // Grouped winner: DISTINCT ON keeps the first row per group under the ordering, so ordering the
        // group components first and the full extreme key after them selects the extreme row per group.
        var groups = MakeQuotedList(request.GroupAliases);
        var derivedAlias = MakeFreeAlias(DerivedAliasBase, request);
        return SqlKeywords.Of(keywordCase, "select distinct on (") + groups + ") *"
            + SqlKeywords.Of(keywordCase, " from (") + request.SourceSql + ") " + derivedAlias
            + SqlKeywords.Of(keywordCase, " order by ") + groups + ", " + keyOrder;
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

    private static string MakeOrderList(IReadOnlyList<string> keys, bool isMax, KeywordCase keywordCase)
    {
        var direction = isMax ? SqlKeywords.Of(keywordCase, " desc") : string.Empty;
        var builder = new StringBuilder();
        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append(Quote(keys[i])).Append(direction);
        }

        return builder.ToString();
    }

    private static string MakeQuotedList(IReadOnlyList<string> names)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append(Quote(names[i]));
        }

        return builder.ToString();
    }

    private static string MakeFreeAlias(string baseAlias, ExtremeRowRenderRequest request)
    {
        var candidate = baseAlias;
        while (Collides(candidate, request))
            candidate += "_";

        return candidate;
    }

    private static bool Collides(string candidate, ExtremeRowRenderRequest request)
        => Contains(request.PayloadAliases, candidate)
            || Contains(request.KeyAliases, candidate)
            || Contains(request.GroupAliases, candidate);

    private static bool Contains(IReadOnlyList<string> aliases, string candidate)
    {
        for (var i = 0; i < aliases.Count; i++)
        {
            if (string.Equals(aliases[i], candidate, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
}
