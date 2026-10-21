using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The in-memory provider evaluates a correlated subquery once per outer row, with the outer row's
/// values bound to the inner query, matching the SQL providers' per-row semantics.
/// </summary>
public class CorrelatedQueryInMemoryTests
{
    private readonly InMemoryRepository _sut;

    public CorrelatedQueryInMemoryTests(InMemoryRepository sut)
    {
        _sut = sut;
        _sut.SimpleEntity.WithData(new[] { new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 } });
    }

    [Fact]
    public void CorrelatedExistsInWhere_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CorrelatedExistsInWhere_ShouldFilterOutNonMatchingRows()
    {
        var r = _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id + 100)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEmpty();
    }

    [Fact]
    public void CorrelatedExistsInProjection_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, has = SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)) })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.has);
    }

    [Fact]
    public void CorrelatedScalarInProjection_ShouldMatchTheOuterRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.sid == row.Id);
    }

    [Fact]
    public void CorrelatedScalarInWhere_ShouldFilterOnTheOuterRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => it.Id == _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id })
            .ToList();

        r.Select(x => x.Id).Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CorrelatedScalarInOrderBy_ShouldOrderByTheOuterRow()
    {
        var r = _sut.SimpleEntity
            .OrderBy(it => _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id })
            .ToList();

        r.Select(x => x.Id).Should().Equal(1, 2);
    }

    [Fact]
    public void CorrelatedInInProjection_ShouldEvaluatePerRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, inc = SqlFunctions.Sql.@in(it.Id, _sut.SimpleEntity.Where(s => s.Id == it.Id).Select(s => s.Id)) })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.inc);
    }

    [Fact]
    public void CorrelatedAggregateInProjection_ShouldAggregatePerRow()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, cnt = _sut.SimpleEntity.Where(s => s.Id == it.Id).Count() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.cnt == 1);
    }

    [Fact]
    public void LongCountProjection_ShouldMatchCountPerOuterRow()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 }
        });

        var r = _sut.SimpleEntity
            .Select(it => new
            {
                it.Id,
                Cnt = _sut.SimpleEntity.Where(s => s.Id <= it.Id).Count(),
                LCnt = _sut.SimpleEntity.Where(s => s.Id <= it.Id).LongCount()
            })
            .ToList()
            .OrderBy(it => it.Id)
            .ToList();

        r.Select(it => it.LCnt).Should().Equal(new long[] { 1, 2, 3 },
            "a correlated LongCount() counts the matching rows per outer row");
        r.Select(it => it.LCnt).Should().Equal(r.Select(it => (long)it.Cnt),
            "LongCount() must equal Count() converted to long for the same predicate");
    }

    [Fact]
    public void LongCountProjection_ShouldReturnZeroForNoMatches()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 }
        });

        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, LCnt = _sut.SimpleEntity.Where(s => s.Id > it.Id + 100).LongCount() })
            .ToList();

        r.Should().HaveCount(3);
        r.Should().OnlyContain(row => row.LCnt == 0L, "zero matching rows must count as 0L, not a substitute value");
    }

    [Fact]
    public void LongCountProjection_ShouldCountRowsWithNullableValues()
    {
        using var ctx = new InMemoryDataContext();
        var entity = ctx.From<NullableCountEntity>();
        entity.WithData(new[]
        {
            new NullableCountEntity { Id = 1, Value = null },
            new NullableCountEntity { Id = 2, Value = 7 },
            new NullableCountEntity { Id = 3, Value = null }
        });

        var r = entity
            .Select(it => new { it.Id, LCnt = entity.Where(v => v.Id <= it.Id).LongCount() })
            .ToList()
            .OrderBy(it => it.Id)
            .ToList();

        r.Select(it => it.LCnt).Should().Equal(new long[] { 1, 2, 3 },
            "the 64-bit count counts rows, so rows whose nullable value is null are still counted");
    }

    [Fact]
    public void LongCountProjection_ShouldCountEntityAndScalarReceivers()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 }
        });

        var r = _sut.SimpleEntity
            .Select(it => new
            {
                it.Id,
                All = _sut.SimpleEntity.LongCount(),
                Upto = _sut.SimpleEntity.Where(s => s.Id <= it.Id).LongCount()
            })
            .ToList();

        r.Should().HaveCount(3);
        r.Should().OnlyContain(row => row.All == 3L, "a plain entity receiver counts every row");
        r.OrderBy(row => row.Id).Select(row => row.Upto).Should().Equal(new long[] { 1, 2, 3 });
        ((object)r[0].All).Should().BeOfType<long>("LongCount() declares a 64-bit result");
    }

    [Fact]
    public void LongCountProjection_ShouldPreserveParametersAcrossCachedExecutions()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 }
        });

        var limit = 1;
        var first = _sut.SimpleEntity
            .Select(it => new { it.Id, LCnt = _sut.SimpleEntity.Where(s => s.Id <= it.Id && s.Id <= limit).LongCount() })
            .ToList()
            .OrderBy(it => it.Id)
            .Select(it => it.LCnt)
            .ToList();

        limit = 2;
        var second = _sut.SimpleEntity
            .Select(it => new { it.Id, LCnt = _sut.SimpleEntity.Where(s => s.Id <= it.Id && s.Id <= limit).LongCount() })
            .ToList()
            .OrderBy(it => it.Id)
            .Select(it => it.LCnt)
            .ToList();

        limit = 1;
        var third = _sut.SimpleEntity
            .Select(it => new { it.Id, LCnt = _sut.SimpleEntity.Where(s => s.Id <= it.Id && s.Id <= limit).LongCount() })
            .ToList()
            .OrderBy(it => it.Id)
            .Select(it => it.LCnt)
            .ToList();

        first.Should().Equal(new long[] { 1, 1, 1 }, "the first parameter value applies to every outer row");
        second.Should().Equal(new long[] { 1, 2, 2 }, "differing parameter values must not reuse the previous result");
        third.Should().Equal(new long[] { 1, 1, 1 }, "re-executing with the earlier value must not keep a stale parameter");
    }

    [Fact]
    public void NonNullableCorrelatedScalarProjection_ShouldRemainFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<D6Parent>(b => b.HasMany(p => p.Children, c => c.ParentId));
        ctx.From<D6Child>(b => b.HasOne(c => c.Parent, c => c.ParentId));
        ctx.From<D6Parent>().WithData(new[] { new D6Parent { Id = 1, Age = 42 } });
        ctx.From<D6Child>().WithData(new[]
        {
            new D6Child { Id = 10, ParentId = 1 },
            new D6Child { Id = 12, ParentId = 999 }
        });

        Action act = () => ctx.From<D6Child>()
            .Select(c => new { c.Id, Age = c.Parent!.Age })
            .ToList();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*D6Child.Parent.Age*System.Int32*");
    }

    [Fact]
    public async Task TopLevelLongCount_ShouldPreserveSyncAsyncAndDefaultArguments()
    {
        _sut.SimpleEntity.WithData(new[]
        {
            new SimpleEntity { Id = 1 },
            new SimpleEntity { Id = 2 },
            new SimpleEntity { Id = 3 }
        });

        var parameters = Array.Empty<object?>();
        var asyncParameters = Array.Empty<object>();

        _sut.SimpleEntity.LongCount().Should().Be(3L);
        _sut.SimpleEntity.LongCount(parameters).Should().Be(3L);
        (await _sut.SimpleEntity.LongCountAsync()).Should().Be(3L);
        (await _sut.SimpleEntity.LongCountAsync(CancellationToken.None)).Should().Be(3L);
        (await _sut.SimpleEntity.LongCountAsync(asyncParameters)).Should().Be(3L);
    }

    [Fact]
    public void CorrelatedScalarFirstOrDefault_ShouldYieldDefaultWhenNoRowMatches()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).FirstOrDefault() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.sid == 0);
    }

    [Fact]
    public void CorrelatedScalarSingle_ShouldYieldValueWhenExactlyOneRowMatches()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new { s.Id, cid = _sut.SimpleEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).Single() })
            .ToList();

        r.Should().ContainSingle();
        r[0].cid.Should().Be(1);
    }

    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenMultipleRowsMatch()
    {
        var act = () => _sut.SimpleEntity
            .Select(s => new { s.Id, cid = _sut.SimpleEntity.Select(c => (int?)c.Id).Single() })
            .ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void NonCorrelatedScalarInProjection_ShouldEvaluateOnce()
    {
        var r = _sut.SimpleEntity
            .Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => s.Id).First() })
            .ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyContain(row => row.sid == 1);
    }

    [Fact]
    public void CorrelatedExistsCombinedWithOr_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)) || it.Id == 1)
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void CorrelatedExistsCombinedWithAnd_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => it.Id > 0 && SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEquivalentTo(new[] { 1, 2 });
    }

    [Fact]
    public void NegatedCorrelatedExists_ShouldFilterPerRow()
    {
        var r = _sut.SimpleEntity
            .Where(it => !SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(s => s.Id == it.Id)))
            .Select(it => it.Id)
            .ToList();

        r.Should().BeEmpty();
    }
}

/// <summary>Entity with a nullable value column used by the row-count-versus-column-count tests.</summary>
public sealed class NullableCountEntity
{
    public int Id { get; set; }

    public int? Value { get; set; }
}
