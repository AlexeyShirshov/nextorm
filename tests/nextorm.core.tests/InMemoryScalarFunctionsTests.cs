using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider evaluates the cross-provider scalar functions through
/// <c>InMemoryScalarFunctionRewriter</c>; these tests pin the SQL semantics it reproduces.
/// </summary>
public class InMemoryScalarFunctionsTests
{
    private readonly InMemoryRepository _sut;

    public InMemoryScalarFunctionsTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void StringFunctions_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity
            .Select(it => new
            {
                L = SqlFunctions.Sql.left("abcdef", 3),
                R = SqlFunctions.Sql.right("abcdef", 3),
                Lp = SqlFunctions.Sql.lpad("7", 3, "0"),
                Rp = SqlFunctions.Sql.rpad("7", 3, "0"),
                Rep = SqlFunctions.Sql.repeat("ab", 3),
                Rev = SqlFunctions.Sql.reverse("abc"),
                Sp = SqlFunctions.Sql.space(2),
                Tr = SqlFunctions.Sql.translate("abc", "ab", "xy"),
                Asc = SqlFunctions.Sql.ascii("A"),
                Ch = SqlFunctions.Sql.@char(65)
            })
            .ToList();

        r.Should().OnlyContain(x =>
            x.L == "abc" && x.R == "def" && x.Lp == "007" && x.Rp == "700" &&
            x.Rep == "ababab" && x.Rev == "cba" && x.Sp == "  " && x.Tr == "xyc" &&
            x.Asc == 65 && x.Ch == "A");
    }

    [Fact]
    public void ConcatWs_ShouldSkipNulls()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { F = SqlFunctions.Sql.concat_ws(",", "a", null, "b") })
            .ToList();

        r.Should().OnlyContain(x => x.F == "a,b");
    }

    [Fact]
    public void NumericAndLengthFunctions_ShouldExecuteNatively()
    {
        var r = _sut.SimpleEntity
            .Select(it => new
            {
                Bl = SqlFunctions.Sql.bit_length("abc"),
                Ol = SqlFunctions.Sql.octet_length("abc"),
                Cot = SqlFunctions.Sql.cot(1.0),
                Deg = SqlFunctions.Sql.degrees(Math.PI),
                Rad = SqlFunctions.Sql.radians(180.0),
                Pi = SqlFunctions.Sql.pi(),
                Acos = Math.Acos(1.0),
                Atan2 = Math.Atan2(1.0, 1.0)
            })
            .ToList();

        r.Should().OnlyContain(x =>
            x.Bl == 24 && x.Ol == 3 &&
            Math.Abs(((double?)x.Cot).GetValueOrDefault() - 1.0 / Math.Tan(1.0)) < 1e-12 &&
            Math.Abs(((double?)x.Deg).GetValueOrDefault() - 180.0) < 1e-9 &&
            Math.Abs(((double?)x.Rad).GetValueOrDefault() - Math.PI) < 1e-12 &&
            Math.Abs(((double?)x.Pi).GetValueOrDefault() - Math.PI) < 1e-12 &&
            Math.Abs(((double?)x.Acos).GetValueOrDefault()) < 1e-12 &&
            Math.Abs(((double?)x.Atan2).GetValueOrDefault() - Math.PI / 4) < 1e-12);
    }

    [Fact]
    public void SqlServerOnlyFunctions_ShouldThrowBecauseNotSupported()
    {
        var select = () => _sut.SimpleEntity
            .Select(it => new { V = SqlFunctions.SqlServer.patindex("%a%", "abc") })
            .ToList();

        var where = () => _sut.SimpleEntity
            .Where(it => SqlFunctions.SqlServer.json_path_exists("{}", "$"))
            .ToList();

        select.Should().Throw<NotSupportedException>().WithMessage("*patindex*not supported*");
        where.Should().Throw<NotSupportedException>().WithMessage("*json_path_exists*not supported*");
    }

    [Fact]
    public void RangeOverlaps_ShouldUsePostgresSemantics()
    {
        var a = new Range<int>(10, 20);
        var overlapping = new Range<int>(15, 25);
        var touchingAtExclusiveBound = new Range<int>(20, 30);

        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.overlaps(a, overlapping)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.overlaps(a, touchingAtExclusiveBound)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.overlaps(a, Range<int>.Empty)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void RangeContains_ShouldUsePostgresSemantics()
    {
        var range = new Range<int>(10, 20);
        var inner = new Range<int>(12, 18);
        var overlappingOutside = new Range<int>(18, 25);

        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contains(range, 15)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contains(range, 20)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contains(range, inner)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contained_by(inner, range)).Select(it => it.Id).ToList()
            .Should().NotBeEmpty();
        _sut.SimpleEntity.Where(it => SqlFunctions.Postgres.range_contained_by(overlappingOutside, range)).Select(it => it.Id).ToList()
            .Should().BeEmpty();
    }

    [Fact]
    public void RangeInspectionFunctions_ShouldUsePostgresSemantics()
    {
        var range = new Range<int>(10, 20);
        var unbounded = new Range<int>(10, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true);

        var result = _sut.SimpleEntity.Select(it => new
        {
            L = SqlFunctions.Postgres.lower(range),
            U = SqlFunctions.Postgres.upper(range),
            E = SqlFunctions.Postgres.isempty(Range<int>.Empty),
            Li = SqlFunctions.Postgres.lower_inc(range),
            Ui = SqlFunctions.Postgres.upper_inc(range),
            Lf = SqlFunctions.Postgres.lower_inf(unbounded),
            Uf = SqlFunctions.Postgres.upper_inf(unbounded),
            Elf = SqlFunctions.Postgres.lower_inf(Range<int>.Empty),
            Euf = SqlFunctions.Postgres.upper_inf(Range<int>.Empty)
        }).ToList();

        result.Should().OnlyContain(x => x.L == 10 && x.U == 20 && x.E && x.Li && !x.Ui && !x.Lf && x.Uf && !x.Elf && !x.Euf);
    }

    [Fact]
    public void RangeUnion_ShouldUsePostgresSemantics()
    {
        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_union(new Range<int>(1, 4), new Range<int>(2, 6)) })
            .First().R.Should().Be(new Range<int>(1, 6));

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_union(new Range<int>(1, 4), new Range<int>(4, 6)) })
            .First().R.Should().Be(new Range<int>(1, 6));

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_union(Range<int>.Empty, new Range<int>(1, 4)) })
            .First().R.Should().Be(new Range<int>(1, 4));

        var act = () => _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_union(new Range<int>(1, 2), new Range<int>(4, 5)) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*single range*");
    }

    [Fact]
    public void RangeIntersection_ShouldUsePostgresSemantics()
    {
        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_intersection(new Range<int>(1, 4), new Range<int>(2, 6)) })
            .First().R.Should().Be(new Range<int>(2, 4));

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_intersection(new Range<int>(1, 2), new Range<int>(3, 4)) })
            .First().R.IsEmpty.Should().BeTrue();

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_intersection(new Range<int>(1, 2), Range<int>.Empty) })
            .First().R.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void RangeDifference_ShouldUsePostgresSemantics()
    {
        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_difference(new Range<int>(5, 15), new Range<int>(10, 20)) })
            .First().R.Should().Be(new Range<int>(5, 10));

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_difference(new Range<int>(2, 4), new Range<int>(1, 3)) })
            .First().R.Should().Be(new Range<int>(3, 4));

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_difference(new Range<int>(2, 4), new Range<int>(3, 5)) })
            .First().R.Should().Be(new Range<int>(2, 3));

        _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_difference(new Range<int>(1, 5), new Range<int>(1, 5)) })
            .First().R.IsEmpty.Should().BeTrue();

        var act = () => _sut.SimpleEntity
            .Select(it => new { R = SqlFunctions.Postgres.range_difference(new Range<int>(1, 6), new Range<int>(2, 4)) })
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage("*contiguous*");
    }

    [Fact]
    public void RangePositionalPredicates_ShouldUsePostgresSemantics()
    {
        var result = _sut.SimpleEntity.Select(it => new
        {
            LtAdjacent = SqlFunctions.Postgres.range_strictly_left_of(new Range<int>(1, 2), new Range<int>(2, 3)),
            LtOverlapping = SqlFunctions.Postgres.range_strictly_left_of(new Range<int>(1, 2, true, true), new Range<int>(2, 3)),
            LtWithEmpty = SqlFunctions.Postgres.range_strictly_left_of(Range<int>.Empty, new Range<int>(1, 2)),
            Rt = SqlFunctions.Postgres.range_strictly_right_of(new Range<int>(50, 60), new Range<int>(20, 30)),
            OverLeft = SqlFunctions.Postgres.range_not_extend_right_of(new Range<int>(1, 20), new Range<int>(18, 20)),
            OverLeftInclusive = SqlFunctions.Postgres.range_not_extend_right_of(new Range<int>(1, 20, true, true), new Range<int>(18, 20)),
            OverRight = SqlFunctions.Postgres.range_not_extend_left_of(new Range<int>(7, 20), new Range<int>(5, 10)),
            Adjacent = SqlFunctions.Postgres.range_adjacent(new Range<int>(1, 2), new Range<int>(2, 3)),
            AdjacentFlipped = SqlFunctions.Postgres.range_adjacent(new Range<int>(1, 2, false, true), new Range<int>(2, 3, false, false)),
            AdjacentOverlap = SqlFunctions.Postgres.range_adjacent(new Range<int>(1, 2, true, true), new Range<int>(2, 3)),
            AdjacentEmpty = SqlFunctions.Postgres.range_adjacent(Range<int>.Empty, new Range<int>(2, 3))
        }).First();

        result.LtAdjacent.Should().BeTrue();
        result.LtOverlapping.Should().BeFalse();
        result.LtWithEmpty.Should().BeFalse();
        result.Rt.Should().BeTrue();
        result.OverLeft.Should().BeTrue();
        result.OverLeftInclusive.Should().BeFalse();
        result.OverRight.Should().BeTrue();
        result.Adjacent.Should().BeTrue();
        result.AdjacentFlipped.Should().BeTrue();
        result.AdjacentOverlap.Should().BeFalse();
        result.AdjacentEmpty.Should().BeFalse();
    }

    [Fact]
    public void RangeConstructors_ShouldBuildRanges()
    {
        var result = _sut.SimpleEntity.Select(it => new
        {
            Default = SqlFunctions.Postgres.int4range(1, 10),
            Unbounded = SqlFunctions.Postgres.int4range(null, 5),
            Empty = SqlFunctions.Postgres.empty_range<int>(),
            Numeric = SqlFunctions.Postgres.numrange(1.5m, 2.5m),
            NumericInclusive = SqlFunctions.Postgres.numrange(1.5m, 2.5m, "[]")
        }).First();

        result.Default.Should().Be(new Range<int>(1, 10));
        result.Unbounded.Should().Be(new Range<int>(0, 5, true, false, lowerInfinite: true, upperInfinite: false));
        result.Empty.IsEmpty.Should().BeTrue();
        result.Numeric.Should().Be(new Range<decimal>(1.5m, 2.5m));
        result.NumericInclusive.Should().Be(new Range<decimal>(1.5m, 2.5m, true, true));
    }

    [Fact]
    public void MultirangePredicates_ShouldUsePostgresSemantics()
    {
        var result = _sut.SimpleEntity.Select(it => new
        {
            Overlap = SqlFunctions.Postgres.overlaps(
                new[] { new Range<int>(1, 5), new Range<int>(10, 20) },
                new[] { new Range<int>(4, 6) }),
            NoOverlap = SqlFunctions.Postgres.overlaps(
                new[] { new Range<int>(1, 5) },
                new[] { new Range<int>(6, 9) }),
            ContainsRange = SqlFunctions.Postgres.range_contains(
                new[] { new Range<int>(1, 5), new Range<int>(10, 20) },
                new Range<int>(2, 3)),
            ContainsMulti = SqlFunctions.Postgres.range_contains(
                new[] { new Range<int>(1, 5), new Range<int>(10, 20) },
                new[] { new Range<int>(2, 3), new Range<int>(11, 12) }),
            ContainsValue = SqlFunctions.Postgres.range_contains(
                new[] { new Range<int>(1, 5), new Range<int>(10, 20) },
                12),
            Adjacent = SqlFunctions.Postgres.range_adjacent(
                new[] { new Range<int>(1, 2) },
                new[] { new Range<int>(2, 3) })
        }).First();

        result.Overlap.Should().BeTrue();
        result.NoOverlap.Should().BeFalse();
        result.ContainsRange.Should().BeTrue();
        result.ContainsMulti.Should().BeTrue();
        result.ContainsValue.Should().BeTrue();
        result.Adjacent.Should().BeTrue();
    }

    [Fact]
    public void MultirangeSetOperations_ShouldNormalize()
    {
        var union = _sut.SimpleEntity.Select(it => new
        {
            R = SqlFunctions.Postgres.range_union(new[] { new Range<int>(1, 5) }, new[] { new Range<int>(4, 8) })
        }).First().R;
        union.Should().HaveCount(1);
        union[0].Should().Be(new Range<int>(1, 8));

        var intersection = _sut.SimpleEntity.Select(it => new
        {
            R = SqlFunctions.Postgres.range_intersection(
                new[] { new Range<int>(1, 5), new Range<int>(10, 20) },
                new[] { new Range<int>(3, 12) })
        }).First().R;
        intersection.Should().HaveCount(2);
        intersection[0].Should().Be(new Range<int>(3, 5));
        intersection[1].Should().Be(new Range<int>(10, 12));

        var difference = _sut.SimpleEntity.Select(it => new
        {
            R = SqlFunctions.Postgres.range_difference(new[] { new Range<int>(1, 10) }, new[] { new Range<int>(3, 5) })
        }).First().R;
        difference.Should().HaveCount(2);
        difference[0].Should().Be(new Range<int>(1, 3));
        difference[1].Should().Be(new Range<int>(5, 10));

        var merged = _sut.SimpleEntity.Select(it => new
        {
            R = SqlFunctions.Postgres.range_merge(new[] { new Range<int>(1, 2), new Range<int>(5, 6) })
        }).First().R;
        merged.Should().Be(new Range<int>(1, 6));
    }

    [Fact]
    public void MultirangeInspectionAndPositional_ShouldUsePostgresSemantics()
    {
        var result = _sut.SimpleEntity.Select(it => new
        {
            Empty = SqlFunctions.Postgres.isempty(new Range<int>[0]),
            Lower = SqlFunctions.Postgres.lower(new[] { new Range<int>(1, 5), new Range<int>(10, 20) }),
            Upper = SqlFunctions.Postgres.upper(new[] { new Range<int>(1, 5), new Range<int>(10, 20) }),
            Left = SqlFunctions.Postgres.range_strictly_left_of(new[] { new Range<int>(1, 2) }, new[] { new Range<int>(5, 6) }),
            Single = SqlFunctions.Postgres.multirange(new Range<int>(3, 4))
        }).First();

        result.Empty.Should().BeTrue();
        result.Lower.Should().Be(1);
        result.Upper.Should().Be(20);
        result.Left.Should().BeTrue();
        result.Single.Should().ContainSingle().Which.Should().Be(new Range<int>(3, 4));
    }

    [Fact]
    public void MultirangeColumn_ShouldReadInMemory()
    {
        var repo = _sut.DataProvider.From<RangeAggEntity>();
        repo.WithData(new[]
        {
            new RangeAggEntity { Id = 1, Spans = new[] { new Range<int>(1, 3), new Range<int>(7, 9) } }
        });

        var spans = repo.Select(it => new { it.Spans }).First().Spans;

        spans.Should().HaveCount(2);
        spans[0].Should().Be(new Range<int>(1, 3));
        spans[1].Should().Be(new Range<int>(7, 9));
    }

    [Fact]
    public void RangeAggregates_ShouldAggregateRangesInMemory()
    {
        var repo = _sut.DataProvider.From<RangeAggEntity>();
        repo.WithData(new[]
        {
            new RangeAggEntity { Id = 1, Span = new Range<int>(1, 5) },
            new RangeAggEntity { Id = 2, Span = new Range<int>(3, 8) },
            new RangeAggEntity { Id = 3, Span = new Range<int>(10, 20) }
        });

        var aggregated = repo
            .GroupBy(it => 1)
            .Select(it => new { Agg = SqlFunctions.Postgres.range_agg(it.Span) })
            .ToList();
        aggregated.Should().HaveCount(1);
        aggregated[0].Agg.Should().HaveCount(2);
        aggregated[0].Agg[0].Should().Be(new Range<int>(1, 8));
        aggregated[0].Agg[1].Should().Be(new Range<int>(10, 20));

        var intersected = repo
            .GroupBy(it => 1)
            .Select(it => new { Inter = SqlFunctions.Postgres.range_intersect_agg(it.Span) })
            .ToList();
        intersected.Should().HaveCount(1);
        intersected[0].Inter.IsEmpty.Should().BeTrue();
    }
}

public class RangeAggEntity
{
    public int Id { get; set; }
    public Range<int> Span { get; set; }
    public Range<int>[] Spans { get; set; } = [];
}
