using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void TestWhere()
    {
        var r = _sut.SimpleEntity.Where(s => SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().NotBeNullOrEmpty();

        r.Count.Should().Be(3);
    }

    [Fact]
    public void CorrelatedScalarInSelect_ShouldMatchTheOuterRow()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == (int)row.Id);
    }

    [Fact]
    public void CorrelatedScalarInWhere_ShouldFilterOnTheOuterRow()
    {
        var r = _sut.ComplexEntity
            .Where(it => it.Id == _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id })
            .ToList();

        r.Select(x => x.Id).Should().BeEquivalentTo(new long[] { 1, 2, 3 });
    }

    [Fact]
    public void CorrelatedScalarInOrderBy_ShouldOrderByTheOuterRow()
    {
        var r = _sut.ComplexEntity
            .OrderBy(it => _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id })
            .ToList();

        r.Select(x => x.Id).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void CorrelatedExistsInSelect_ShouldEvaluatePerRow()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                has = SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id))
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.has);
    }

    [Fact]
    public void CorrelatedScalarFirstOrDefault_ShouldYieldNullWhenNoRowMatches()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = (int?)_sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).FirstOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == null);
    }

    [Fact]
    public void CorrelatedScalarFirstOrDefaultValue_ShouldYieldDefaultWhenNoRowMatches()
    {
        // The projection is a non-nullable value type: before the terminal flag was carried to the
        // materializer this threw on SQL NULL even though the terminal is *OrDefault.
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).FirstOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == 0);
    }

    [Fact]
    public void CorrelatedScalarSingleOrDefault_ShouldYieldNullWhenNoRowMatches()
    {
        Assert.SkipUnless(Provider.EnforcesScalarSubqueryCardinality, ScalarCardinalitySkipReason);

        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = (int?)_sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).SingleOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == null);
    }

    [Fact]
    public void CorrelatedScalarSingle_ShouldYieldValueWhenExactlyOneRowMatches()
    {
        Assert.SkipUnless(Provider.EnforcesScalarSubqueryCardinality, ScalarCardinalitySkipReason);

        var r = _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new
            {
                s.Id,
                cid = _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).Single()
            })
            .ToList();

        r.Should().ContainSingle();
        r[0].cid.Should().Be(1);
    }

    [Fact]
    public void CorrelatedScalarSingleOrDefaultValue_ShouldYieldDefaultWhenNoRowMatches()
    {
        Assert.SkipUnless(Provider.EnforcesScalarSubqueryCardinality, ScalarCardinalitySkipReason);

        // Non-nullable value projection with a SingleOrDefault terminal: no row must yield 0, not throw.
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).SingleOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == 0);
    }

    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenMultipleRowsMatch()
    {
        Assert.SkipUnless(Provider.EnforcesScalarSubqueryCardinality, ScalarCardinalitySkipReason);

        var act = () => _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new
            {
                s.Id,
                cid = _sut.ComplexEntity.Select(c => (int?)c.Id).Single()
            })
            .ToList();

        // The command renders `limit 2`, so the engine rejects the second row instead of returning the first.
        act.Should().Throw<Exception>();
    }

    private static string ScalarCardinalitySkipReason =>
        "This provider's scalar subqueries do not enforce cardinality (Single/SingleOrDefault are rejected there).";

    /// <remarks>
    /// The projection is nullable (<c>Select(s => (int?)s.Id)</c>), so a missing row maps to <c>null</c>.
    /// Casting the result after a non-nullable projection (<c>(int?)...First()</c>) does not - the scalar
    /// is materialized as <c>int</c> first and throws on SQL NULL (see the SQLite-specific test).
    /// </remarks>
    [Fact]
    public void CorrelatedScalarNullableFirst_ShouldYieldNullWhenNoRowMatches()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => (int?)s.Id).First()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == null);
    }

    /// <summary>
    /// Two correlated subqueries over the same entity type must each use their own table alias.
    /// Before the source scope was introduced the second subquery resolved its column to the first
    /// subquery's alias, which is out of scope and fails on a real database.
    /// </summary>
    [Fact]
    public void CorrelatedSiblingSubqueriesOfSameType_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Select(s => new
            {
                s.Id,
                a = _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).FirstOrDefault(),
                b = _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).FirstOrDefault()
            })
            .ToList();

        r.Should().HaveCount(3);
        r.Should().OnlyContain(x => x.a == x.Id && x.b == x.Id);
    }
}
