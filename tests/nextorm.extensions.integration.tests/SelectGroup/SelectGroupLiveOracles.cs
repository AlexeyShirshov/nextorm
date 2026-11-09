using FluentAssertions;
using NextORM.Core;
using NextORM.Extensions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Exact-value oracles for the packaged grouping operators, shared by every live provider suite
/// (PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse and SQLite).
/// </summary>
/// <remarks>
/// Every provider seeds the same deterministic <c>extrema_entity</c> fixture (11 rows):
/// <code>
/// id score category
///  1  null  a
///  2     5  a
///  3     9  a
///  4     9  a
///  5     3  b
///  6     1  b
///  7  null  null
///  8     7  null
///  9     4  c
/// 10     1  b
/// 11  null  d
/// </code>
/// Per category: <c>a</c> count 4 / sum 23, <c>b</c> count 3 / sum 5, <c>null</c> count 2 / sum 7,
/// <c>c</c> count 1 / sum 4, <c>d</c> count 1 / sum NULL. The expected values below are written out
/// from this table, never from another query, so the oracles cannot agree with a defect.
/// </remarks>
internal static class SelectGroupLiveOracles
{
    /// <summary>Exact grouped rows: every key once, with its exact aggregate.</summary>
    internal static void KeyedGrouping(EntityBuilder<ExtremaEntity> e)
    {
        // max(score) keeps the column's own integer width on every provider (ClickHouse's sum(Int32)
        // widens to Int64 and cannot bind a projected int?), so the oracle stays provider-neutral.
        var rows = e.SelectGroup(x => new { x.Category, Top = SqlFunctions.Sql.max(x.Score) }).ToList();

        rows.Should().HaveCount(5);
        rows.OrderBy(r => r.Category, StringComparer.Ordinal)
            .Select(r => (r.Category, r.Top))
            .Should().Equal(
                ((string?)null, (int?)7),
                ("a", (int?)9),
                ("b", (int?)3),
                ("c", (int?)4),
                ("d", (int?)null));
    }

    /// <summary>Aggregate HAVING: only groups with more than one row survive; the rest are absent by name.</summary>
    internal static void SelectGroupHaving(EntityBuilder<ExtremaEntity> e)
    {
        var rows = e.SelectGroupHaving(
            x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) },
            r => r.Rows > 1).ToList();

        rows.Select(r => r.Category).Should().BeEquivalentTo(["a", "b", null]);
        rows.Should().NotContain(r => r.Category == "c", "the c group has one row and is filtered by HAVING");
        rows.Should().NotContain(r => r.Category == "d", "the d group has one row and is filtered by HAVING");
        rows.OrderBy(r => r.Category, StringComparer.Ordinal)
            .Select(r => (r.Category, r.Rows))
            .Should().Equal(
                ((string?)null, 2),
                ("a", 4),
                ("b", 3));
    }

    /// <summary>Zero inferred keys with a real aggregate HAVING: exactly one row on a passing threshold,
    /// zero rows on a failing one (the condition is a genuine HAVING, never dropped).</summary>
    internal static void SelectGroupHavingGlobalAggregation(EntityBuilder<ExtremaEntity> e)
    {
        var passing = e.SelectGroupHaving(
            x => new { Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Total > 10).ToList();
        passing.Should().ContainSingle().Which.Total.Should().Be(11);

        var failing = e.SelectGroupHaving(
            x => new { Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Total > 11).ToList();
        failing.Should().BeEmpty("the aggregate condition is a real HAVING");
    }

    /// <summary>Row-independent projection + row-independent condition: one exact constant over a
    /// non-empty source, zero rows over an empty one; the condition is lowered into the pre-group WHERE
    /// with DISTINCT (constant outcome).</summary>
    internal static void SelectGroupHavingConstantProjection(EntityBuilder<ExtremaEntity> e)
    {
        var threshold = 1;

        var nonEmpty = e.SelectGroupHaving(
            x => new { Constant = "constant" },
            r => threshold > 0).ToList();
        nonEmpty.Should().ContainSingle().Which.Constant.Should().Be("constant");

        var empty = e.Where(x => x.Id < 0).SelectGroupHaving(
            x => new { Constant = "constant" },
            r => threshold > 0).ToList();
        empty.Should().BeEmpty("a constant projection over an empty source keeps zero rows");
    }

    /// <summary>Source HAVING AND operator condition: proves AND, not OR and not replacement.</summary>
    internal static void HavingCombined(EntityBuilder<ExtremaEntity> e)
    {
        var rows = e.Having(x => SqlFunctions.Sql.count(x.Id) > 1)
            .SelectGroupHaving(
                x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) },
                r => r.Category != "d")
            .ToList();

        // AND: count > 1 AND category <> 'd' -> a, b. The NULL group has count 2 but `NULL <> 'd'`
        // is UNKNOWN, so SQL three-valued logic drops it too (all providers agree).
        // OR would also keep c (count 1, category 'c') and the NULL group; replacement would keep c.
        rows.Select(r => r.Category).Should().BeEquivalentTo(["a", "b"]);
        rows.Should().NotContain(r => r.Category == "c", "c satisfies only the operator condition, not the source HAVING");
        rows.Should().NotContain(r => r.Category == "d", "d satisfies neither predicate");
        rows.OrderBy(r => r.Category, StringComparer.Ordinal)
            .Select(r => (r.Category, r.Rows))
            .Should().Equal(("a", 4), ("b", 3));
    }

    /// <summary>Global aggregation: exactly one row with the exact total, and the HAVING pass/fail outcomes.</summary>
    internal static void GlobalAggregation(EntityBuilder<ExtremaEntity> e)
    {
        var all = e.SelectGroup(x => new { Total = SqlFunctions.Sql.count(x.Id) }).ToList();
        all.Should().ContainSingle().Which.Total.Should().Be(11);

        var passing = e.SelectGroupHaving(
            x => new { Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Total > 10).ToList();
        passing.Should().ContainSingle().Which.Total.Should().Be(11);

        var failing = e.SelectGroupHaving(
            x => new { Total = SqlFunctions.Sql.count(x.Id) },
            r => r.Total > 11).ToList();
        failing.Should().BeEmpty();
    }

    /// <summary>Constant projection: one exact row on non-empty input, zero rows on empty input.</summary>
    internal static void ConstantProjection(EntityBuilder<ExtremaEntity> e)
    {
        // A string literal is rendered as the dialect's string type on every provider; a small integer
        // literal is UInt8 on ClickHouse and cannot bind a projected int.
        var nonEmpty = e.SelectGroup(x => new { Constant = "constant" }).ToList();
        nonEmpty.Should().ContainSingle().Which.Constant.Should().Be("constant");

        var empty = e.Where(x => x.Id < 0).SelectGroup(x => new { Constant = "constant" }).ToList();
        empty.Should().BeEmpty("a constant projection over an empty source keeps zero rows");
    }

    /// <summary>The nullable key is its own group, present exactly once with its exact aggregate.</summary>
    internal static void NullableKey(EntityBuilder<ExtremaEntity> e)
    {
        var rows = e.SelectGroup(x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) }).ToList();

        rows.Should().HaveCount(5);
        rows.Where(r => r.Category == null).Should().ContainSingle("NULL is one group, not one per row")
            .Which.Rows.Should().Be(2);
        rows.Where(r => r.Category != null)
            .OrderBy(r => r.Category, StringComparer.Ordinal)
            .Select(r => (r.Category, r.Rows))
            .Should().Equal(
                ("a", 4),
                ("b", 3),
                ("c", 1),
                ("d", 1));
    }

    /// <summary>ORDER BY then LIMIT over the surviving groups: exact ordered keys and exact page size.</summary>
    internal static void OrderingTake(EntityBuilder<ExtremaEntity> e)
    {
        // ORDER BY is declared on the source (the analyzer validates ORDER BY keys against the inferred
        // grouping keys, so a same-shaped post-group sort over the projected row is not accepted), and
        // LIMIT applies to the grouped result after HAVING.
        var rows = e.Where(x => x.Category != null)
            .OrderBy(x => x.Category)
            .SelectGroupHaving(
                x => new { x.Category, Rows = SqlFunctions.Sql.count(x.Id) },
                r => r.Category != "d")
            .Limit(2)
            .ToList();

        rows.Select(r => (r.Category, r.Rows)).Should().Equal(("a", 4), ("b", 3));
    }
}
