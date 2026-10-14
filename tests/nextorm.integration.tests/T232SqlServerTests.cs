using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// D-05 (#232) SQL Server container-backed facts for the typed PIVOT/UNPIVOT result projection
/// (evidence row E232-08, test-contract rows V01/V11/V14/V22/V24/V26/V27). The typed overloads are executed
/// against the deterministic <c>T232SqlServerFixture</c> tables and compared with the legacy
/// <see cref="TableAlias"/> path, the frozen oracle in <c>test-contract.json</c> and the materialized
/// rows. The database is provided by <see cref="SqlServerTestProvider"/> (Testcontainers unless
/// <c>NEXTORM_SQLSERVER_CONNECTION</c> wins); a missing server skips the whole class rather than
/// passing it.
/// </summary>
[Collection("SqlServer")]
public sealed class T232SqlServerTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    /// <summary>The shared context with the T232 fixture tables created and seeded.</summary>
    private IDataContext Ctx
    {
        get
        {
            var ctx = _sut.DataProvider;
            T232SqlServerFixture.EnsureSeeded(ctx);
            return ctx;
        }
    }

    // ------------------------------------------------------------------ V01/AC-02: typed-vs-legacy parity

    [Fact]
    public void TypedPivot_RowsMatchTheLegacyPath()
    {
        var ctx = Ctx;

        var legacy = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter, PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new
            {
                Category = t.GetString("category"),
                Q1 = t.GetNullableDecimal("[1]"),
                Q2 = t.GetNullableDecimal("[2]")
            })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        var typed = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2") })
            .Select(x => new { x.Category, x.Q1, x.Q2 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        // Same rows, same ordered values: North has both quarters, South only quarter 1.
        typed.Should().HaveCount(2);
        typed.Select(r => (r.Category, r.Q1, r.Q2))
            .Should().Equal(legacy.Select(r => ((string?)r.Category, r.Q1, r.Q2)));
        typed.Single(r => r.Category == "North").Should().BeEquivalentTo(new { Category = "North", Q1 = (decimal?)10m, Q2 = (decimal?)20m });
        typed.Single(r => r.Category == "South").Should().BeEquivalentTo(new { Category = "South", Q1 = (decimal?)5m, Q2 = (decimal?)null });
    }

    // ------------------------------------------------------------------ V08/AC-02: ordinal materialization

    [Fact]
    public void TypedPivot_MaterializesTheDeclaredValueOrderByOrdinal()
    {
        var ctx = Ctx;

        // The declaration declares value "2" before value "1": the IN list, the SELECT order and the
        // shape members all follow that declaration order and the row materializes ordinally.
        var rows = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q2 = pv.Value<decimal?>("2"), Q1 = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.Q2, x.Q1 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        rows.Should().HaveCount(2);
        var north = rows.Single(r => r.Category == "North");
        north.Q1.Should().Be(10m);
        north.Q2.Should().Be(20m);

        var south = rows.Single(r => r.Category == "South");
        south.Q1.Should().Be(5m);
        south.Q2.Should().BeNull("South has no quarter 2 row");
    }

    // ------------------------------------------------------------------ V11/AC-02: NULL cells and COUNT

    [Fact]
    public void TypedPivot_NullCellsAndCountFollowTheFrozenExpectations()
    {
        var ctx = Ctx;

        var sums = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2") })
            .Select(x => new { x.Category, x.Q1, x.Q2 })
            .ToList();

        sums.Single(r => r.Category == "South").Q2.Should().BeNull("a missing PIVOT cell materializes NULL");

        var counts = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Count, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, N1 = pv.Value<int?>("1"), N2 = pv.Value<int?>("2") })
            .Select(x => new { x.Category, x.N1, x.N2 })
            .ToList();

        var south = counts.Single(r => r.Category == "South");
        south.N1.Should().Be(1);
        south.N2.Should().Be(0, "COUNT of a missing PIVOT cell is 0, not NULL");
        counts.Single(r => r.Category == "North").N2.Should().Be(1);
    }

    [Fact]
    public void TypedUnpivot_RowsMatchTheLegacyPath()
    {
        var ctx = Ctx;

        var legacy = ctx.From<IT232Quarterly>()
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new
            {
                Category = t.GetString("category"),
                Quarter = t.GetString("qtr"),
                Measure = t.GetNullableDecimal("val")
            })
            .ToList();

        var typed = ctx.From<IT232Quarterly>()
            .Unpivot(
                (m, u) => new { m.Category, Quarter = u.Name<string>("qtr"), Measure = u.Value<decimal?>("val") },
                UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(x => new { x.Category, x.Quarter, x.Measure })
            .ToList();

        // SQL Server UNPIVOT excludes a NULL source cell, so the frozen fixture's South/q2 = NULL
        // yields no row: 3 rows, and the typed path drops exactly the same row as the legacy path.
        typed.Should().HaveCount(3);
        typed.Select(r => (r.Category, r.Quarter, r.Measure))
            .Should().BeEquivalentTo(legacy.Select(r => (r.Category, r.Quarter, r.Measure)));
        typed.Should().NotContain(r => r.Category == "South" && r.Quarter == "q2",
            "T-SQL UNPIVOT drops a NULL source cell instead of materializing a NULL value row");
    }

    // ------------------------------------------------------------------ V13/AC-01: runtime composition

    [Fact]
    public void TypedPivot_DirectComposition_Executes()
    {
        var ctx = Ctx;

        var pivot = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        // The typed members are usable in Where/OrderBy/Select on the returned builder.
        var rows = pivot
            .Where(x => x.Q1 > 0)
            .OrderBy(x => x.Q1)
            .Select(x => new { x.Category, x.Q1 })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Category.Should().Be("South");
        rows[0].Q1.Should().Be(5m);
        rows[1].Category.Should().Be("North");
        rows[1].Q1.Should().Be(10m);
    }

    // ------------------------------------------------------------------ V14/AC-08: AsCte

    [Fact]
    public void TypedPivot_AsCte_KeepsTheTypedOutput()
    {
        var ctx = Ctx;

        var pivot = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        // CTE route: the typed result is declared once and consumed through From(cte).
        var cte = pivot.Select(x => new { x.Category, x.Q1 }).AsCte("t232_pivot");
        var rows = ctx.From(cte)
            .Select(x => new { x.Category, x.Q1 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        rows.Select(r => r.Category).Should().Equal("North", "South");
        rows.Single(r => r.Category == "South").Q1.Should().Be(5m);
    }

    // ------------------------------------------------------------------ V22/AC-08: twice-joined typed result

    [Fact]
    public void TypedPivot_CteSelfJoin_QualifiesBothSidesDistinctly()
    {
        var ctx = Ctx;

        var pivot = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") });

        // A self-join of the typed result through its CTE descriptor: each side is qualified by its
        // own slot, and the (South 5, North 10) pair proves the two slots resolve to different rows.
        var cte = pivot.Select(x => new { x.Category, x.Q1 }).AsCte("t232_pivot_self");
        var rows = ctx.From(cte)
            .Join(ctx.From(cte), (a, b) => a.Q1 < b.Q1)
            .Select(p => new { Left = p.Item1.Category, LeftQ1 = p.Item1.Q1, Right = p.Item2.Category, RightQ1 = p.Item2.Q1 })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Left.Should().Be("South");
        rows[0].LeftQ1.Should().Be(5m);
        rows[0].Right.Should().Be("North");
        rows[0].RightQ1.Should().Be(10m);
    }

    // ------------------------------------------------------------------ V24/AC-09: cache reuse without sticky state

    [Fact]
    public void TypedPivot_InterleavedShapesAndRepeats_ShareNoStickyState()
    {
        var ctx = Ctx;

        var first = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, V = pv.Value<decimal?>("1") })
            .Select(x => new { x.Category, x.V });
        var second = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, V = pv.Value<decimal?>("2") })
            .Select(x => new { x.Category, x.V });

        var firstRows = first.ToList().OrderBy(r => r.Category).ToList();
        var secondRows = second.ToList().OrderBy(r => r.Category).ToList();
        var firstAgain = first.ToList().OrderBy(r => r.Category).ToList();

        // Distinct values, stable reuse and no command mutation leaking between the two shapes.
        firstRows.Single(r => r.Category == "North").V.Should().Be(10m);
        secondRows.Single(r => r.Category == "North").V.Should().Be(20m);
        firstAgain.Select(r => (r.Category, r.V)).Should().Equal(firstRows.Select(r => (r.Category, r.V)));

        // The shared Count/Any command still works after the typed runs: no sticky _dontCache leak.
        ctx.From<IT232Sales>().Where(s => s.Quarter == 1).Count().Should().Be(2);
    }

    // ------------------------------------------------------------------ V26/AC-08: derived builder route
    //
    // RESOLVED (D232-D05-derived-fallback-insufficient, r=3 n=2): the additive fallback clones the
    // derived subquery's projection columns into projection-output references, so the intermediate
    // derived layer emits the derived table's own output identifiers (`[category]`, `[Q1]`) instead of
    // re-aliasing the underlying source column (`[1] as [Q1]` -> `Invalid column name '1'`). The facts
    // below are ordinary facts again and execute against the real SQL Server container.

    [Fact]
    public void TypedPivot_DerivedBuilderRoute_Executes()
    {
        var ctx = Ctx;

        // The typed result consumed as a derived EntityBuilder<TShape> source: From(EntityBuilder<TShape>)
        // materialises the builder through ToCommand(), whose metadata-less derived intermediate is the
        // route the gated projection-preserving fallback repairs. The projection must survive to the
        // real database and return the pivot rows.
        var typed = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1"), Q2 = pv.Value<decimal?>("2") });

        var rows = ctx.From(typed)
            .Select(x => new { x.Category, x.Q1, x.Q2 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        rows.Should().HaveCount(2);
        rows.Single(r => r.Category == "North").Should()
            .BeEquivalentTo(new { Category = "North", Q1 = (decimal?)10m, Q2 = (decimal?)20m });
        rows.Single(r => r.Category == "South").Q1.Should().Be(5m);

        // The typed members also compose through Where on the derived source after the projection survives.
        var filtered = ctx.From(typed)
            .Where(x => x.Q1 > 5)
            .Select(x => new { x.Category, x.Q1 })
            .ToList();

        filtered.Should().ContainSingle();
        filtered[0].Category.Should().Be("North");
    }

    // ------------------------------------------------------------------ V27/AC-08: derived command route

    [Fact]
    public void TypedPivot_DerivedCommandRoute_Executes()
    {
        var ctx = Ctx;

        // The same typed result exposed as a QueryCommand<TShape> and consumed as a derived source.
        var command = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") })
            .ToCommand();

        var rows = ctx.From(command)
            .Select(x => new { x.Category, x.Q1 })
            .ToList()
            .OrderBy(r => r.Category)
            .ToList();

        rows.Select(r => r.Category).Should().Equal("North", "South");
        rows.Single(r => r.Category == "North").Q1.Should().Be(10m);
        rows.Single(r => r.Category == "South").Q1.Should().Be(5m);
    }

    [Fact]
    public void TypedPivot_DerivedCommandSelfJoin_Executes()
    {
        var ctx = Ctx;

        // The typed derived command joined to itself: both slots must resolve and qualify distinctly.
        // The only pair with a.Q1 < b.Q1 is (South 5) x (North 10), which proves both sides read real rows.
        var command = ctx.From<IT232Sales>()
            .Pivot(PivotAggregate.Sum, s => s.Margin, s => s.Quarter,
                (m, pv) => new { m.Category, Q1 = pv.Value<decimal?>("1") })
            .ToCommand();

        var rows = ctx.From(command)
            .Join(ctx.From(command), (a, b) => a.Q1 < b.Q1)
            .Select(p => new { Left = p.Item1.Category, LeftQ1 = p.Item1.Q1, Right = p.Item2.Category, RightQ1 = p.Item2.Q1 })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Left.Should().Be("South");
        rows[0].LeftQ1.Should().Be(5m);
        rows[0].Right.Should().Be("North");
        rows[0].RightQ1.Should().Be(10m);
    }
}
