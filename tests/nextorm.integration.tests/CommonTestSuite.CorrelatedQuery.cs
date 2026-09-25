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
    /// A correlated subquery may reference a column of a join-projection item (<c>p.Item1.Id</c>);
    /// the projection item then supplies the alias for the inner predicate. Resolving the member
    /// through the item position must work per row on every SQL provider.
    /// </summary>
    [Fact]
    public void CorrelatedScalarOnJoinProjection_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new
            {
                p.Item1.Id,
                cid = _sut.ComplexEntity.Where(c => c.Id == p.Item1.Id).Select(c => (long?)c.Id).FirstOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.cid == row.Id);
    }

    [Fact]
    public void CorrelatedExistsOnJoinProjection_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Select(p => new
            {
                p.Item1.Id,
                has = SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == p.Item1.Id))
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.has);
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

    /// <summary>
    /// A subquery in HAVING is prepared through the correlated-query visitor like WHERE, so a
    /// correlated <c>exists</c> can reference the grouping key. It used to be rejected with
    /// <c>NotSupportedException</c> because HAVING was never run through the visitor.
    /// </summary>
    [Fact]
    public void CorrelatedExistsInHaving_ShouldEvaluatePerGroup()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.Int != null)
            .GroupBy(e => new { e.Int })
            .Having(g => SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Int == g.Int)))
            .Select(g => new { g.Int, count = SqlFunctions.Sql.count() })
            .ToList();

        r.Should().NotBeEmpty();
    }

    /// <summary>
    /// Correlation depth greater than one: the innermost subquery references both the middle subquery's
    /// parameter and the outermost one, and evaluates per outer row.
    /// </summary>
    [Fact]
    public void NestedCorrelationDepth2_ShouldEvaluatePerRow()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                x = _sut.SimpleEntity.Where(s => s.Id == it.Id)
                       .Select(s => _sut.SimpleEntity.Where(s2 => s2.Id == s.Id && s2.Id == it.Id).Select(s2 => s2.Id).First())
                       .First()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.x == (int)row.Id);
    }

    /// <summary>
    /// An aggregate terminal (<c>Count</c>/<c>Max</c>/...) inside a correlated subquery is rewritten to
    /// the equivalent aggregate projection, so it evaluates per outer row on every SQL provider.
    /// </summary>
    [Fact]
    public void CorrelatedAggregateTerminalInSelect_ShouldAggregatePerRow()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                cnt = _sut.ComplexEntity.Where(c => c.Id == it.Id).Count(),
                mx = _sut.ComplexEntity.Where(c => c.Id == it.Id).Max(c => (long?)c.Id)
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.cnt == 1 && row.mx == row.Id);
    }

    /// <summary>
    /// A correlated reference may be wrapped in a function over the outer column
    /// (<c>e.String.ToUpper()</c>); the member translator rewrites the outer column to its marker and
    /// the ordinary scalar translator renders the call around it.
    /// </summary>
    [Fact]
    public void CorrelatedFunctionOverOuterColumn_ShouldEvaluatePerRow()
    {
        var r = _sut.ComplexEntity
            .Where(e => e.String != null)
            .Select(e => new
            {
                e.Id,
                matches = SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.String == e.String!.ToUpper()))
            })
            .ToList();

        r.Should().NotBeEmpty();
    }

    /// <summary>
    /// A correlated <c>CROSS APPLY</c>/<c>CROSS JOIN LATERAL</c> source is evaluated per left-hand
    /// row, so a match filter on the outer key keeps only the matching left rows.
    /// </summary>
    [Fact]
    public void CorrelatedCrossApply_ShouldEvaluatePerOuterRow()
    {
        Assert.SkipUnless(Provider.SupportsApply, ApplySkipReason);

        var r = _sut.SimpleEntity
            .CrossApply(s => _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => new { c.Id, c.String }))
            .Select(p => new { p.Item1.Id, p.Item2.String })
            .ToList();

        r.Should().HaveCount(3);
        r.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// A correlated <c>OUTER APPLY</c>/<c>LEFT JOIN LATERAL ... ON true</c> keeps left-hand rows with
    /// no match, projecting NULLs from the applied source.
    /// </summary>
    [Fact]
    public void CorrelatedOuterApply_ShouldPreserveUnmatchedRows()
    {
        Assert.SkipUnless(Provider.SupportsApply, ApplySkipReason);

        var r = _sut.SimpleEntity
            .OuterApply(s => _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => new { c.Id }))
            .Select(p => new { p.Item1.Id, cid = (long?)p.Item2.Id })
            .ToList();

        r.Should().HaveCount(10);
        r.Count(x => x.cid is null).Should().Be(7);
        r.Where(x => x.cid is not null).Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// A plain table applied with <c>CROSS APPLY</c>/<c>OUTER APPLY</c> cannot be correlated, so it is
    /// rendered as an ordinary <c>CROSS</c>/<c>LEFT</c> join. PostgreSQL/MySQL only allow <c>LATERAL</c>
    /// before a subquery or function, so emitting <c>CROSS JOIN LATERAL &lt;table&gt;</c> would be invalid.
    /// </summary>
    [Fact]
    public void CrossApply_OnPlainTable_ShouldCrossJoin()
    {
        Assert.SkipUnless(Provider.SupportsApply, ApplySkipReason);

        var r = _sut.SimpleEntity
            .CrossApply(_sut.ComplexEntity)
            .Select(p => new { OuterId = p.Item1.Id, InnerId = p.Item2.Id })
            .ToList();

        r.Should().HaveCount(30);
    }

    /// <summary>
    /// <c>OUTER APPLY</c> over a plain table becomes a <c>LEFT JOIN ... ON true</c>, so every left-hand
    /// row survives even when the right table is unrelated.
    /// </summary>
    [Fact]
    public void OuterApply_OnPlainTable_ShouldKeepEveryLeftRow()
    {
        Assert.SkipUnless(Provider.SupportsApply, ApplySkipReason);

        var r = _sut.SimpleEntity
            .OuterApply(_sut.ComplexEntity)
            .Select(p => new { OuterId = p.Item1.Id, InnerId = p.Item2.Id })
            .ToList();

        r.Should().HaveCount(30);
    }

    /// <summary>
    /// The whole-entity (<c>EntityBuilder&lt;T&gt;</c>) apply source is projected as a derived table that
    /// exposes the entity's columns under their projected names (<c>somestring as "String"</c>). The
    /// enclosing projection must reference that projected name, not the physical column, or the query
    /// fails with "column ... does not exist".
    /// </summary>
    [Fact]
    public void CorrelatedCrossApply_BuilderSource_ShouldProjectEntityMembers()
    {
        Assert.SkipUnless(Provider.SupportsApply, ApplySkipReason);

        var r = _sut.SimpleEntity
            .CrossApply(s => _sut.ComplexEntity.Where(c => c.Id == s.Id))
            .Select(p => new { p.Item1.Id, p.Item2.String })
            .ToList();

        r.Should().HaveCount(3);
        r.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// <c>OUTER APPLY</c> over the whole-entity builder source keeps left-hand rows with no match and
    /// must resolve the derived entity's members through the projected alias as well.
    /// </summary>
    [Fact]
    public void CorrelatedOuterApply_BuilderSource_ShouldPreserveUnmatchedRows()
    {
        Assert.SkipUnless(Provider.SupportsApply, ApplySkipReason);

        var r = _sut.SimpleEntity
            .OuterApply(s => _sut.ComplexEntity.Where(c => c.Id == s.Id))
            .Select(p => new { p.Item1.Id, InnerId = (long?)p.Item2.Id })
            .ToList();

        r.Should().HaveCount(10);
        r.Count(x => x.InnerId is null).Should().Be(7);
        r.Where(x => x.InnerId is not null).Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// A captured local referenced more than once in a <c>Where</c> over a join projection must be
    /// registered as a single parameter; before the de-duplication it produced two equally named
    /// placeholders and the command failed with <c>Must add values for the following parameters</c>.
    /// </summary>
    [Fact]
    public void CapturedLocalRepeatedInJoinWhere_ShouldEvaluatePerRow()
    {
        var threshold = 0;

        var r = _sut.SimpleEntity
            .Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id)
            .Where(p => p.Item1.Id > threshold && p.Item2.Id > threshold)
            .Select(p => new { p.Item1.Id })
            .ToList();

        r.Should().HaveCount(3);
        r.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// A captured local that lives only inside a joined derived subquery is bound on the enclosing
    /// command, so the filter the subquery carries is applied before the join.
    /// </summary>
    [Fact]
    public void CapturedLocalInJoinedDerivedSubquery_ShouldEvaluatePerRow()
    {
        var min = 1;

        var derived = _sut.ComplexEntity
            .Where(c => c.Id >= min)
            .Select(c => new { c.Id });

        var r = _sut.SimpleEntity
            .Join(derived, (s, d) => s.Id == d.Id)
            .Select(p => new { p.Item1.Id })
            .ToList();

        r.Should().HaveCount(3);
        r.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>
    /// A correlated <c>EXISTS</c> on the left/right of <c>||</c> must combine with a predicate instead
    /// of failing preparation with <c>The binary operator OrElse is not defined ...</c>.
    /// </summary>
    [Fact]
    public void CorrelatedExistsCombinedWithOr_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Where(s => SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id)) || s.Id == 1)
            .Select(s => s.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>A correlated <c>EXISTS</c> combined with <c>&amp;&amp;</c> keeps the surrounding predicate.</summary>
    [Fact]
    public void CorrelatedExistsCombinedWithAnd_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id <= 3)
            .Where(s => s.Id >= 1 && SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id)))
            .Select(s => s.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    /// <summary>A negated correlated <c>EXISTS</c> renders and evaluates as the complement of the subquery.</summary>
    [Fact]
    public void NegatedCorrelatedExists_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Where(s => !SqlFunctions.Sql.exists(_sut.ComplexEntity.Where(c => c.Id == s.Id + 100)))
            .Select(s => s.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1 });
    }

    private static string ApplySkipReason =>
        "This provider has no lateral/APPLY source, so a correlated APPLY cannot be rendered.";
}
