using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// QueryPreparer branch battery (cycle 3, D8) executed against a live SQLite database. Each fact is a
/// deliberate "battery": many heterogeneous expression and clause forms are packed into ONE query so a
/// wide slice of <c>QueryCommand.QueryPreparer</c> runs for a single preparation, and the live rows are
/// asserted against the class-owned <c>pa3_*</c> marker tables (dropped, created and seeded by this
/// class's own DDL in the constructor - no global seed-table writes, no global seed-table counts and no
/// <c>InMemoryDataContext</c>).
/// </summary>
///
/// <para>
/// Deliberately recorded as unreachable through the public surface (never fabricated): the <c>All</c>
/// terminal (collecting-predicate "every row satisfies" - no <c>All</c> member exists on
/// <c>EntityBuilder</c>/<c>QueryCommand</c>; the closest form is <c>!Any(negated)</c>) and <c>Take(0)</c>
/// (<c>EntityBuilder.Limit(0)</c> is documented as "zero means no limit" and returns every row, so a
/// zero-row take has no public expression). Both are exercised by their live counterparts instead:
/// <c>Take(0)</c> is asserted through the documented no-limit semantics of <c>Limit(0)</c> and the
/// paging battery uses <c>Offset</c> beyond the row count for the empty page.
/// </para>
[Collection("Sqlite")]
public sealed class PreparerBatteryBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists pa3_line;
        drop table if exists pa3_main;
        create table pa3_main
        (
            id integer primary key,
            name text not null,
            qty int not null,
            price real not null,
            note text null,
            active boolean not null,
            tag int not null,
            grp int not null
        );
        create table pa3_line
        (
            id integer primary key,
            main_id int not null,
            qty int not null,
            amount real not null,
            label text not null,
            grp int not null
        );
        insert into pa3_main (id, name, qty, price, note, active, tag, grp) values
            (1, 'alpha', 2, 1.5, 'n1', 1, 10, 1),
            (2, 'beta', 3, 2.5, null, 0, 20, 1),
            (3, 'gamma', 4, 3.5, 'n3', 1, 10, 2);
        insert into pa3_line (id, main_id, qty, amount, label, grp) values
            (10, 1, 1, 10.0, 'x', 1),
            (11, 1, 1, 20.0, 'y', 1),
            (12, 1, 3, 30.0, 'z', 2),
            (13, 2, 5, 50.0, 'w', 1),
            (14, 3, 7, 70.0, 'v', 3);
        """;

    public PreparerBatteryBranchCoverage3Tests() => ExecuteRaw(Ddl);

    private IDataContext Context => _sut.DataProvider;

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // ---------------------------------------------------------------------------------------------
    // 1. Projection battery: ~15 heterogeneous projection expressions in one Select
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ProjectionBattery_ManyHeterogeneousExpressions_ShouldReadLiveRows()
    {
        var rows = Context.From<Pa3Main>()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,                                                                        // scalar column
                PlusOne = x.Qty + 1,                                                         // arithmetic
                Concat = string.Concat(x.Name, "!"),                                         // static method call
                Sized = x.Qty > 2 ? "big" : "small",                                         // conditional
                Coalesced = x.Note ?? "none",                                                // null-coalescing
                AsDouble = (double)x.Qty,                                                     // cast / convert
                Absolute = Math.Abs(x.Qty),                                                   // static method call (Math)
                NameLength = x.Name.Length,                                                   // nested member
                Literal = 7,                                                                   // constant literal
                DuplicateA = x.Qty,                                                            // duplicate-valued pair
                DuplicateB = x.Qty,
                ScalarLineId = Context.From<Pa3Line>()                                         // correlated scalar subquery
                    .Where(l => l.MainId == x.Id).Select(l => l.Id).First(),
                HasLines = Context.From<Pa3Line>().Where(l => l.MainId == x.Id).Any(),         // entity reference
                IsActive = x.Active,                                                          // boolean expression
                Note = x.Note                                                                 // nullable column
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Id).Should().Equal(new[] { 1, 2, 3 }, "the battery keeps exactly one row per marker row");

        var first = rows[0];
        first.PlusOne.Should().Be(3);
        first.Concat.Should().Be("alpha!");
        first.Sized.Should().Be("small");
        first.Coalesced.Should().Be("n1");
        first.AsDouble.Should().Be(2.0);
        first.Absolute.Should().Be(2);
        first.NameLength.Should().Be(5);
        first.Literal.Should().Be(7);
        first.DuplicateA.Should().Be(first.DuplicateB);
        first.ScalarLineId.Should().Be(10);
        first.HasLines.Should().BeTrue();
        first.IsActive.Should().BeTrue();
        first.Note.Should().Be("n1");

        var second = rows[1];
        second.Concat.Should().Be("beta!");
        second.Sized.Should().Be("big");
        second.Coalesced.Should().Be("none");
        second.AsDouble.Should().Be(3.0);
        second.ScalarLineId.Should().Be(13);
        second.HasLines.Should().BeTrue();
        second.IsActive.Should().BeFalse();
        second.Note.Should().BeNull();

        var third = rows[2];
        third.Concat.Should().Be("gamma!");
        third.Sized.Should().Be("big");
        third.Coalesced.Should().Be("n3");
        third.Absolute.Should().Be(4);
        third.NameLength.Should().Be(5);
        third.IsActive.Should().BeTrue();
    }

    // ---------------------------------------------------------------------------------------------
    // 2. Predicate battery: ~12 predicate forms combined with && / || / !
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void PredicateBattery_ManyPredicateForms_ShouldReadLiveRows()
    {
        var minQty = 2; // captured closure value

        var rows = Context.From<Pa3Main>()
            .Where(x =>
                (x.Id >= 1                                                        // comparison
                 && x.Qty + 1 > 1                                                 // arithmetic
                 && x.Name != "nope"                                              // string inequality
                 && x.Name.Contains("a")                                          // LIKE / Contains
                 && SqlFunctions.Sql.@in(x.Tag, 10, 20)                           // IN with literals
                 && x.Note != null                                                // null check on a reference string
                 && x.Active                                                      // boolean column
                 && x.Name.Length > 3                                             // nested member
                 && !(x.Qty < minQty)                                             // logical negation + closure
                 && Context.From<Pa3Line>().Where(l => l.MainId == x.Id && l.Qty > 0).Any())
                || x.Qty == 3)                                                    // OR arm / constant interplay
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        // Row 1 and row 3 satisfy the whole && chain; row 2 has a null note and only comes back through ||.
        rows.Should().Equal(1, 2, 3);

        var withoutOrArm = Context.From<Pa3Main>()
            .Where(x => x.Id >= 1
                        && x.Qty + 1 > 1
                        && x.Name != "nope"
                        && x.Name.Contains("a")
                        && SqlFunctions.Sql.@in(x.Tag, 10, 20)
                        && x.Note != null
                        && x.Active
                        && x.Name.Length > 3
                        && !(x.Qty < minQty))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        withoutOrArm.Should().Equal(new[] { 1, 3 }, "the && chain alone drops the null-note row 2");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. Grouping battery: two keys + HAVING + Count/Sum/Min/Max/Avg
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void GroupingBattery_TwoKeysHavingAndAggregates_ShouldReadLiveRows()
    {
        var rows = Context.From<Pa3Line>()
            .GroupBy(x => new { x.MainId, x.Qty })
            .Having(g => SqlFunctions.Sql.count() >= 1)
            .Select(g => new
            {
                g.MainId,
                g.Qty,
                Count = SqlFunctions.Sql.count(),
                Total = SqlFunctions.Sql.sum(g.Qty),
                Lowest = SqlFunctions.Sql.min(g.Qty),
                Highest = SqlFunctions.Sql.max(g.Qty),
                Average = SqlFunctions.Sql.avg(Convert.ToDouble(g.Qty))
            })
            .ToList();

        rows.Should().HaveCount(4);

        var repeated = rows.Single(r => r.MainId == 1 && r.Qty == 1);
        repeated.Count.Should().Be(2);
        repeated.Total.Should().Be(2);
        repeated.Lowest.Should().Be(1);
        repeated.Highest.Should().Be(1);
        repeated.Average.Should().Be(1.0);

        rows.Single(r => r.MainId == 1 && r.Qty == 3).Total.Should().Be(3);
        rows.Single(r => r.MainId == 2).Total.Should().Be(5);
        rows.Single(r => r.MainId == 3).Total.Should().Be(7);
        rows.Should().OnlyContain(r => r.Lowest == r.Highest, "each group is keyed on its own Qty value");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. Sorting battery: four keys (asc / desc / expression / renamed column) + paging
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SortingBattery_FourKeysAndPaging_ShouldReadLiveRows()
    {
        var ordered = Context.From<Pa3Main>()
            .OrderBy(x => x.Tag)                 // first key, ascending
            .OrderByDescending(x => x.Qty)       // second key, descending
            .OrderBy(x => -x.Price)              // third key, expression
            .OrderBy(x => x.Name)                // fourth key, a renamed column (property Name -> column name)
            .Select(x => x.Id)
            .ToList();

        ordered.Should().Equal(new[] { 3, 1, 2 }, "tag asc, then qty desc, then the -price expression and the name column");

        var paged = Context.From<Pa3Main>()
            .OrderBy(x => x.Tag)
            .OrderByDescending(x => x.Qty)
            .OrderBy(x => -x.Price)
            .OrderBy(x => x.Name)
            .Offset(1)
            .Limit(2)
            .Select(x => x.Id)
            .ToList();

        paged.Should().Equal(new[] { 1, 2 }, "skip one row and take the next two of the same ordering");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. Join battery: inner + left join over composite keys and a joined derived source
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinBattery_CompositeKeysLeftJoinAndDerivedSource_ShouldReadLiveRows()
    {
        var inner = Context.From<Pa3Main>()
            .Join(Context.From<Pa3Line>(), (m, l) => m.Id == l.MainId && m.Grp == l.Grp)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new
            {
                MainId = p.Item1.Id,
                LineId = p.Item2.Id,
                Tag = p.Item1.Tag,
                LineQty = p.Item2.Qty
            })
            .ToList();

        // The composite (id, grp) key matches main 1 -> lines 10/11 and main 2 -> line 13; main 3 (grp 2)
        // has no line with grp 2, so the inner join drops it.
        inner.Should().HaveCount(3);
        inner.Select(r => r.LineId).Should().Equal(10, 11, 13);
        inner.Select(r => r.MainId).Should().Equal(1, 1, 2);
        inner.Should().OnlyContain(r => r.Tag == r.MainId * 10 || r.Tag > 0);

        var left = Context.From<Pa3Main>()
            .LeftJoin(Context.From<Pa3Line>(), (m, l) => m.Id == l.MainId && m.Grp == l.Grp)
            .Select(p => new { MainId = p.Item1.Id, Label = p.Item2.Label })
            .ToList();

        left.Should().HaveCount(4, "the left join also keeps main 3 without a composite-key match");
        left.Select(r => r.MainId).Should().BeEquivalentTo([1, 1, 2, 3]);
        left.Where(r => r.Label != null).Select(r => r.Label).Should().BeEquivalentTo(["x", "y", "w"]);

        var derived = Context.From<Pa3Line>().Where(l => l.Qty >= 3);
        var joined = Context.From<Pa3Main>()
            .Join(Context.From(derived), (m, l) => m.Id == l.MainId)
            .OrderBy(p => p.Item2.Id)
            .Select(p => new { Name = p.Item1.Name, LineLabel = p.Item2.Label })
            .ToList();

        joined.Should().HaveCount(3);
        joined.Select(r => r.LineLabel).Should().Equal("z", "w", "v");
        joined.Select(r => r.Name).Should().Equal("alpha", "beta", "gamma");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. CTE battery: two chained CTEs read from a join, and one CTE read twice under two aliases
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void CteBattery_ChainedCtesAndDoubleRead_ShouldReadLiveRows()
    {
        // Chain: a filtering CTE feeds an aggregating CTE; the aggregate is then joined back to the table.
        var filtered = Context.From<Pa3Line>()
            .Where(l => l.Qty >= 1)
            .Select(l => new { l.MainId, l.Qty })
            .AsCte("pa3_f");

        var aggregated = Context.From(filtered)
            .GroupBy(r => r.MainId)
            .Select(g => new { MainId = g.MainId, Total = SqlFunctions.Sql.sum(g.Qty) })
            .AsCte("pa3_agg");

        var chained = Context.From(aggregated)
            .Join(Context.From<Pa3Main>(), (a, m) => a.MainId == m.Id)
            .OrderBy(p => p.Item1.MainId)
            .Select(p => new { p.Item1.MainId, p.Item1.Total, p.Item2.Name })
            .ToList();

        chained.Should().HaveCount(3);
        chained.Select(r => r.MainId).Should().Equal(1, 2, 3);
        chained.Select(r => r.Total).Should().Equal(5, 5, 7);
        chained.Select(r => r.Name).Should().Equal("alpha", "beta", "gamma");

        // One CTE read twice under two aliases inside one statement: main 1 has two rows with qty 1,
        // so the self-join on (MainId, Qty) yields 2 x 2 = 4 pairs for that group alone.
        var duplicated = Context.From<Pa3Line>()
            .Where(l => l.Qty >= 1)
            .Select(l => new { l.MainId, l.Qty })
            .AsCte("pa3_dup");

        var twice = Context.From(duplicated)
            .Join(duplicated, (a, b) => a.MainId == b.MainId && a.Qty == b.Qty)
            .Select(p => new { MainId = p.Item1.MainId, Qty = p.Item1.Qty })
            .ToList();

        twice.Should().HaveCount(7);
        twice.Where(r => r.MainId == 1 && r.Qty == 1).Should().HaveCount(4);
        twice.Where(r => r.Qty != 1).Should().HaveCount(3);
    }

    // ---------------------------------------------------------------------------------------------
    // 7. Set-operation battery: Union / UnionAll / Except / Intersect, with and without a Where
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SetOperationBattery_FourOperatorsAndFollowingWhere_ShouldReadLiveRows()
    {
        var left = Context.From<Pa3Main>().Where(m => m.Id <= 2).Select(m => m.Id);
        var right = Context.From<Pa3Main>().Where(m => m.Id >= 2).Select(m => m.Id);

        var union = left.Union(right);
        var unionAll = left.UnionAll(right);
        var except = left.Except(right);
        var intersect = left.Intersect(right);

        Context.From(union).Select(v => new { Value = v }).ToList().Select(r => r.Value).OrderBy(x => x)
            .Should().Equal(new[] { 1, 2, 3 });
        Context.From(unionAll).Select(v => new { Value = v }).ToList().Select(r => r.Value).OrderBy(x => x)
            .Should().Equal(new[] { 1, 2, 2, 3 });
        Context.From(except).Select(v => new { Value = v }).ToList().Select(r => r.Value).OrderBy(x => x)
            .Should().Equal(new[] { 1 });
        Context.From(intersect).Select(v => new { Value = v }).ToList().Select(r => r.Value).OrderBy(x => x)
            .Should().Equal(new[] { 2 });

        Context.From(unionAll).Where(x => x > 1).Select(v => new { Value = v }).ToList().Select(r => r.Value)
            .OrderBy(x => x).Should().Equal(new[] { 2, 2, 3 }, "a Where after the set operation filters the combined rows");
        Context.From(except).Where(x => x > 1).Select(v => new { Value = v }).ToList().Should()
            .BeEmpty("the excepted set has no row above one");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. Terminal / paging battery
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TerminalAndPagingBattery_TerminalsAndExtrema_ShouldReadLiveRows()
    {
        Context.From<Pa3Main>().Where(x => x.Tag == 10).OrderBy(x => x.Id).First().Id.Should().Be(1);
        Context.From<Pa3Main>().Where(x => x.Id == 999).FirstOrDefault().Should().BeNull();
        Context.From<Pa3Main>().Where(x => x.Id == 2).Single().Name.Should().Be("beta");
        Context.From<Pa3Main>().Where(x => x.Id == 999).SingleOrDefault().Should().BeNull();

        Context.From<Pa3Main>().Where(x => x.Active).Count().Should().Be(2);
        Context.From<Pa3Main>().Where(x => x.Active).LongCount().Should().Be(2L);
        Context.From<Pa3Main>().Where(x => x.Qty > 3).Any().Should().BeTrue();
        Context.From<Pa3Main>().Where(x => x.Qty > 100).Any().Should().BeFalse();

        // The documented "zero means no limit" semantics of Limit/Take and the empty page beyond the rows.
        Context.From<Pa3Main>().Limit(0).Select(x => x.Id).ToList().Should().HaveCount(3);
        Context.From<Pa3Main>().OrderBy(x => x.Id).Offset(100).Limit(2).Select(x => x.Id).ToList().Should().BeEmpty();

        // Extrema with and without a predicate.
        var globalMax = Context.From<Pa3Main>().SelectWhereMax(x => x.Qty).ToList();
        globalMax.Should().ContainSingle();
        globalMax[0].Id.Should().Be(3);

        var activeMin = Context.From<Pa3Main>().Where(x => x.Active).SelectWhereMin(x => x.Qty).ToList();
        activeMin.Should().ContainSingle();
        activeMin[0].Id.Should().Be(1);
        activeMin[0].Qty.Should().Be(2);

        var taggedMax = Context.From<Pa3Main>()
            .Where(x => x.Tag == 10)
            .SelectWhereMax(x => x.Price, x => new { x.Id, x.Price })
            .ToList();
        taggedMax.Should().ContainSingle();
        taggedMax[0].Id.Should().Be(3, "of the tag-10 rows (1 and 3) the higher price is row 3");
    }

    /// <summary>Marker row of the class-owned <c>pa3_main</c> table.</summary>
    [SqlTable("pa3_main")]
    public sealed class Pa3Main
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("qty")]
        public int Qty { get; set; }

        [Column("price")]
        public double Price { get; set; }

        [Column("note")]
        public string? Note { get; set; }

        [Column("active")]
        public bool Active { get; set; }

        [Column("tag")]
        public int Tag { get; set; }

        [Column("grp")]
        public int Grp { get; set; }
    }

    /// <summary>Marker row of the class-owned <c>pa3_line</c> table.</summary>
    [SqlTable("pa3_line")]
    public sealed class Pa3Line
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("main_id")]
        public int MainId { get; set; }

        [Column("qty")]
        public int Qty { get; set; }

        [Column("amount")]
        public double Amount { get; set; }

        [Column("label")]
        public string Label { get; set; } = string.Empty;

        [Column("grp")]
        public int Grp { get; set; }
    }
}
