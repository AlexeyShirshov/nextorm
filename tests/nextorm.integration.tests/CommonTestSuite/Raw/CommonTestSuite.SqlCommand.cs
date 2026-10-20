using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public async Task SelectEntity_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
        }
    }

    [Fact]
    public async Task SelectEntityToList_ShouldReturnData()
    {
        (await _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).ToAsyncEnumerable().CountAsync(TestContext.Current.CancellationToken)).Should().Be(10);
    }

    [Fact]
    public async Task SelectModifiedEntity_ShouldReturnData()
    {
        long idx = 1;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id + 1 }).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
        }
    }

    [Fact]
    public async Task SelectTable_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new { Id = tbl.GetInt64("id") }).ToAsyncEnumerable())
        {
            row.Id.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task QuotedIdentifiers_SelectAndWhere_ShouldReturnData()
    {
        var rows = await _sut.SimpleEntity
            .WithQuotedIdentifiers()
            .Where(e => e.Id > 1)
            .Select(e => new { Id = (long)e.Id })
            .ToListAsync();

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => r.Id > 1);
    }

    [Fact]
    public async Task SelectSubQuery_ShouldReturnData()
    {
        await foreach (var row in _sut.From(_sut.From("simple_entity").Select(tbl => new { Id = tbl.GetInt64("id") })).Select(subQuery => new { subQuery.Id }).ToAsyncEnumerable())
        {
            row.Id.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SelectComplexEntity_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.Datetime, it.RequiredString, it.Boolean, it.Date, it.Double, it.Int, it.Numeric, it.Real, it.SmallInt, it.String, it.TinyInt }).ToAsyncEnumerable())
        {
            row.Id.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SelectEntity_ShouldCancel_WhenCancel()
    {
        CancellationTokenSource tokenSource = new();
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).ToAsyncEnumerable().WithCancellation(tokenSource.Token))
        {
            tokenSource.Cancel();
            break;
        }
    }

    [Fact]
    public async Task SelectEntityIntoDTO_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new SimpleEntityDTO(entity.Id)).ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
        }
    }

    [Fact]
    public async Task SelectTableIntoDTO_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new SimpleEntityDTO(tbl.GetInt64("id"))).ToAsyncEnumerable())
        {
            row.Id.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SelectEntityIntoTuple_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new Tuple<long>(tbl.GetInt64("id"))).ToAsyncEnumerable())
        {
            row.Item1.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SelectEntityIntoRecord_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id"))).ToAsyncEnumerable())
        {
            row.Id.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SelectNestedEntityIntoRecord_ShouldReturnData()
    {
        var nested = _sut.From("simple_entity").Select(tbl => new SimpleEntityRecord(tbl.GetInt64("id")));
        await foreach (var row in _sut.From(nested).Select(rec => new { rec.Id }).ToAsyncEnumerable())
        {
            row.Id.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task SelectComplexEntityWithCalculatedFields_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.RequiredString, it.String, CalcString = it.RequiredString + "/" + it.String }).ToAsyncEnumerable())
        {
            if (row.Id == 3)
                row.CalcString.Should().BeNull();
            else
                row.CalcString.Should().Be($"{row.RequiredString}/{row.String}");
        }
    }

    [Fact]
    public async Task SelectNestedComplexEntityWithCalculatedFields_ShouldReturnData()
    {
        var nested = _sut.ComplexEntity.Select(it => new { it.Id, it.RequiredString, it.String, CalcString = it.RequiredString + "/" + it.String });

        await foreach (var row in _sut.From(nested).Select(t1 => new { t1.Id, t1.RequiredString, t1.String, t1.CalcString }).ToAsyncEnumerable())
        {
            if (row.Id == 3)
                row.CalcString.Should().BeNull();
            else
                row.CalcString.Should().Be($"{row.RequiredString}/{row.String}");
        }
    }

    [Fact]
    public async Task SelectComplexEntityWithCalculatedFields_WhenConditional_ShouldReturnString()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.RequiredString, it.String, CalcString = it.RequiredString + "/" + (it.String ?? "") }).ToAsyncEnumerable())
        {
            row.CalcString.Should().Be($"{row.RequiredString}/{row.String ?? string.Empty}");
        }
    }

    [Fact]
    public async Task SelectComplexEntityWithCalculatedNumericFields_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt, it.Real, it.Double, Calc = it.TinyInt + it.SmallInt, Calc2 = (it.Real ?? 1) + it.Double }).ToAsyncEnumerable())
        {
            row.Calc.Should().Be(row.TinyInt + row.SmallInt);
            row.Calc2.Should().Be((row.Real ?? 1f) + row.Double);
        }
    }

    [Fact]
    public async Task SelectComplexEntityWithCalculatedNumericFields2_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt, Calc = (it.TinyInt + it.SmallInt) * 2 }).ToAsyncEnumerable())
        {
            row.Calc.Should().Be((row.TinyInt + row.SmallInt) * 2);
        }
    }

    [InlineData(2)]
    [Theory]
    public async Task SelectComplexEntityWithCalculatedNumericFieldsWithParam_ShouldReturnData(int i)
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt, Calc = (it.TinyInt + it.SmallInt) * i }).ToAsyncEnumerable())
        {
            row.Calc.Should().Be((row.TinyInt + row.SmallInt) * i);
        }
    }

    [Fact]
    public async Task SelectEntity_WhenFilterById_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id == 1)
            .Select(entity => new { Id = (long)entity.Id })
            .ToAsyncEnumerable())
        {
            idx++;
            row.Id.Should().Be(1);
        }
        idx.Should().Be(1);
    }

    [Fact]
    public async Task SelectEntity_WhenFilterByIdNeg_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id != 1)
            .Select(entity => new { Id = (long)entity.Id })
            .ToAsyncEnumerable())
        {
            idx++;
        }
        idx.Should().Be(9);
    }

    [Fact]
    public async Task SelectEntity_WhenFilterByIdGt_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id > 1)
            .Select(entity => new { Id = (long)entity.Id })
            .ToAsyncEnumerable())
        {
            idx++;
        }
        idx.Should().Be(9);
    }

    [Fact]
    public async Task SelectEntity_WhenFilterByIdGte_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id >= 9)
            .Select(entity => new { Id = (long)entity.Id })
            .ToAsyncEnumerable())
        {
            idx++;
        }
        idx.Should().Be(2);
    }

    [Fact]
    public async Task SelectEntity_WhenFilterByIdLt_ShouldReturnFilteredData()
    {
        var r = await _sut.SimpleEntity
            .Where(entity => entity.Id < 1)
            .Select(entity => new { Id = (long)entity.Id }).AnyAsync();

        r.Should().BeFalse();
    }

    [Fact]
    public async Task SelectEntity_WhenFilterByIdLte_ShouldReturnFilteredData()
    {
        var row = await _sut.SimpleEntity
            .Where(entity => entity.Id <= 1)
            .Select(entity => new { Id = (long)entity.Id }).FirstOrDefaultAsync();

        row.Should().NotBeNull();
        row.Id.Should().Be(1);
    }

    [InlineData(1)]
    [Theory]
    public async Task SelectEntityParam_WhenFilterById_ShouldReturnFilteredData(long id)
    {
        var row = await _sut.SimpleEntity
            .Where(entity => entity.Id == id)
            .Select(entity => new { Id = (long)entity.Id }).FirstOrDefaultAsync();

        row.Should().NotBeNull();
        row.Id.Should().Be(1);
    }

    [InlineData("dadfasd")]
    [InlineData(null)]
    [Theory]
    public async Task ComplexEntityParam_WhenFilterByString_ShouldReturnFilteredData(string? str)
    {
        await foreach (var row in _sut.ComplexEntity
            .Where(entity => entity.String == str)
            .Select(entity => new { entity.Id, entity.String })
            .ToAsyncEnumerable())
        {
            row.String.Should().Be(str);

            if (row.Id == 3)
                row.String.Should().BeNull();
        }
    }

    [Fact]
    public async Task SelectEntityParam2_WhenFilterById_ShouldReturnFilteredData()
    {
        var e = new cls1(1);

        var row = await _sut.SimpleEntity
            .Where(entity => entity.Id == e.Id)
            .Select(entity => new { Id = (long)entity.Id }).FirstOrDefaultAsync();

        row.Should().NotBeNull();

        row.Id.Should().Be(1);
    }

    [Fact]
    public async Task SelectTableWithWhere_ShouldReturnData()
    {
        var row = await _sut.From("simple_entity")
            .Where(tbl => tbl.GetInt64("id") == 1)
            .Select(tbl => new { Id = tbl.GetInt64("id") }).FirstOrDefaultAsync();

        row.Should().NotBeNull();

        row.Id.Should().Be(1);
    }

    [Fact]
    public async Task SelectTableWithWhereCalc_ShouldReturnData()
    {
        var row = await _sut.From("simple_entity")
            .Where(tbl => tbl.GetInt64("id") + 2 == 1)
            .Select(tbl => new { Id = tbl.GetInt64("id") }).FirstOrDefaultAsync();

        row.Should().BeNull();
    }

    [Fact]
    public async Task SelectEntityAsClass_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntityAsClass.ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
        }
    }

    [Fact]
    public async Task SelectSubQueryWhere_ShouldReturnData()
    {
        var idx = 0;
        await foreach (var row in _sut.From(_sut.SimpleEntity.Where(it => it.Id > 8)).Select(it => new { it.Id }).ToAsyncEnumerable())
        {
            idx++;
        }

        idx.Should().Be(2);
    }

    [Fact]
    public void SelectPrimitive_ShouldReturnData()
    {
        const int limit = 5;
        // Given
        var q1 = _sut.SimpleEntity.Where(it => it.Id < limit).Select(it => it.Id);
        // When
        var r = q1.ToList();
        // Then
        for (var i = 0; i < r.Count; i++)
        {
            r[i].Should().Be(i + 1);
        }
    }

    [Fact]
    public void SelectPrimitiveOnComplex_ShouldReturnData()
    {
        const int limit = 5;
        // Given
        var q1 = _sut.ComplexEntity.Where(it => it.Id < limit).Select(it => (it.String ?? string.Empty) + it.RequiredString);
        // When
        var r = q1.ToList();
        // Then
        for (var i = 0; i < r.Count; i++)
        {
            r[i].Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void SelectBoolOnComplex_ShouldReturnData()
    {
        // Given
        var q1 = _sut.ComplexEntity.Where(it => it.Boolean == true).Select(it => it.Boolean);
        // When
        var r = q1.ToList();
        // Then
        for (var i = 0; i < r.Count; i++)
        {
            r[i].Should().BeTrue();
        }
    }

    [Fact]
    public async Task Any_ShouldReturnTrue()
    {
        await SelectAnyParam_ShouldReturnTrue();
        var r = await _sut.ComplexEntity.Where(it => it.Boolean == true).AnyAsync();

        r.Should().BeTrue();

        r = await _sut.ComplexEntity.Where(it => it.Id == 100).AnyAsync();

        r.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAny_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Where(it => it.Boolean == true).AnyAsync();

        r.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAsterisk_ShouldReturnTrue()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntityAsClass.ToAsyncEnumerable())
        {
            idx++;
            idx.Should().Be(row.Id);
        }
    }

    [Fact]
    public async Task SelectAnyParam_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Where(it => it.Boolean == SqlFunctions.Parameter<bool>(0)).AnyAsync(true);

        r.Should().BeTrue();
    }

    [Fact]
    public async Task SelectAnyAsProp_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(_sut.SimpleEntity) }).ToListAsync();

        r.Should().NotBeEmpty();

        r.Select(it => it.exists).All(it => it).Should().BeTrue();

        r = await _sut.ComplexEntity.Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(_sut.SimpleEntity.Where(it => it.Id == 100)) }).ToListAsync();

        r.Should().NotBeEmpty();

        r.Select(it => it.exists).All(it => it).Should().BeFalse();

        r = await _sut.ComplexEntity.Select(it => new { it.Id, exists = SqlFunctions.Sql.exists(_sut.SimpleEntity) }).ToListAsync();

        r.Should().NotBeEmpty();

        r.Select(it => it.exists).All(it => it).Should().BeTrue();
    }

    [Fact]
    public async Task SelectExists_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Select(it => new { it.Id, exists = _sut.SimpleEntity.Any() }).ToListAsync();

        r.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SelectAnyOnBuilder_ShouldReturnTrue()
    {
        var r = await _sut.SimpleEntity.AnyAsync();

        r.Should().BeTrue();

        r = await _sut.SimpleEntity.Where(it => it.Id == 234234).AnyAsync();

        r.Should().BeFalse();
    }

    [Fact]
    public void Top_ShouldLimitData()
    {
        // Given
        var r = _sut.SimpleEntity.Limit(1).Select(it => it.Id).ToList();
        // When
        r.Count.Should().Be(1);
        r[0].Should().Be(1);
    }

    [Fact]
    public void Top_ShouldLimitOffsetData()
    {
        // Given
        var r = _sut.SimpleEntity.Page(1, 1).Select(it => it.Id).ToList();
        // When
        r.Count.Should().Be(1);
        r[0].Should().Be(2);
    }

    [Fact]
    public void ProjectedCommand_OrderByDescendingAndPage_ShouldReturnOrderedPage()
    {
        var rows = _sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, Cnt = SqlFunctions.Sql.count() })
            .OrderByDescending(x => x.Cnt)
            .Page(1, 0)
            .ToList();

        rows.Should().HaveCount(1);
        rows[0].Cnt.Should().Be(2);

        var second = _sut.ComplexEntity
            .GroupBy(x => x.Int)
            .Select(x => new { x.Int, Cnt = SqlFunctions.Sql.count() })
            .OrderByDescending(x => x.Cnt)
            .Page(1, 1)
            .ToList();

        second.Should().HaveCount(1);
        second[0].Cnt.Should().Be(1);
    }

    [Fact]
    public void ProjectedCommand_OrderByAscending_ShouldResolveProjectionMember()
    {
        var ids = _sut.SimpleEntity
            .Select(x => new { x.Id })
            .OrderBy(x => x.Id)
            .Limit(3)
            .ToList();

        ids.Select(x => x.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void First_ShouldReturnFirst()
    {
        // Given
        var r = _sut.SimpleEntity.Select(it => it.Id).First();

        // When
        r.Should().Be(1);
        // Then
    }

    [Fact]
    public void FirstOffset_ShouldReturnFirst()
    {
        // Given
        var r = _sut.SimpleEntity.Offset(1).Select(it => it.Id).First();

        // When
        r.Should().Be(2);
        // Then
    }

    [Fact]
    public void FirstOffsetEmpty_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Offset(10).Select(it => it.Id).First();
        };

        test.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FirstOrDefault_ShouldReturnFirst()
    {
        var r = _sut.SimpleEntity.Offset(10).Select(it => it.Id).FirstOrDefault();

        r.Should().Be(default);
    }

    [Fact]
    public void Single_ShouldReturnSingle()
    {
        // Given
        var r = _sut.SimpleEntity.Where(it => it.Id == 2).Select(it => it.Id).Single();

        // When
        r.Should().Be(2);
        // Then
    }

    [Fact]
    public void SingleEmpty_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Offset(10).Select(it => it.Id).Single();
        };

        test.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SingleMany_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Select(it => it.Id).Single();
        };

        test.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SingleOrDefault_ShouldReturnData()
    {
        var r = _sut.SimpleEntity.Offset(10).Select(it => it.Id).SingleOrDefault();

        r.Should().Be(default);
    }

    [Fact]
    public void SingleOrDefaultMany_ShouldThrow()
    {
        var test = () =>
        {
            _sut.SimpleEntity.Select(it => it.Id).SingleOrDefault();
        };

        test.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void OrderBy_ShouldSortData()
    {
        // Given
        var r = _sut.SimpleEntity.OrderByDescending(it => it.Id).Select(it => it.Id).First();
        // When
        r.Should().Be(10);
        // Then
    }

    [Fact]
    public void OrderByNumber_ShouldSortData()
    {
        // Given
        var r = _sut.SimpleEntity.Select(it => it.Id).OrderByDescending(1).First();
        // When
        r.Should().Be(10);
        // Then
    }

    [Fact]
    public void OrderBySubquery_ShouldSortData()
    {
        // Given
        var r = _sut.ComplexEntity.OrderBy(_ => _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First()).OrderBy(it => it.Id).Select(it => new { it.Id }).ToList();
        // When
        r[0].Id.Should().Be(1);
        r[1].Id.Should().Be(2);
        r[2].Id.Should().Be(3);
    }

    [Fact]
    public async Task SubQuerySelect_ShouldReturnData()
    {
        var r = await _sut.ComplexEntity.Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First() }).ToListAsync();

        r.Should().NotBeEmpty();

        r[0].sid.Should().Be(1);
    }

    [Fact]
    public async Task SubQuerySelectOrder_ShouldReturnData()
    {
        var r = await _sut.ComplexEntity.Select(it => new { it.Id, sid = _sut.SimpleEntity.OrderByDescending(it => it.Id).Select(it => it.Id).First() }).ToListAsync();

        r.Should().NotBeEmpty();

        r[0].sid.Should().Be(10);
    }

    [Fact]
    public async Task SubQuerySelectOrderSingle_ShouldReturnData()
    {
        Assert.SkipUnless(Provider.EnforcesScalarSubqueryCardinality,
            "This provider's scalar subqueries do not enforce cardinality (Single/SingleOrDefault are rejected there).");

        var r = await _sut.ComplexEntity.Select(it => new { it.Id, sid = _sut.SimpleEntity.Where(it => it.Id >= 10).OrderByDescending(it => it.Id).Select(it => it.Id).Single() }).ToListAsync();

        r.Should().NotBeEmpty();

        r[0].sid.Should().Be(10);
    }

    [Fact]
    public async Task WhereSubQuery_ShouldReturnData()
    {
        var r = await _sut.ComplexEntity.Where(it => it.Id == _sut.SimpleEntity.OrderBy(it => it.Id).Select(it => it.Id).First()).Select(it => new { it.Id }).FirstOrDefaultAsync();

        r.Should().NotBeNull();

        r.Id.Should().Be(1);
    }

    [Fact]
    public async Task WhereInSubQuery_ShouldReturnData()
    {
        var r = await _sut.ComplexEntity.Where(it => SqlFunctions.Sql.@in((int)it.Id, _sut.SimpleEntity.Where(it => it.Id == 2).Select(it => it.Id))).Select(it => it.Id).FirstOrDefaultAsync();

        r.Should().Be(2);
    }

    [Fact]
    public async Task ManualSql_ShouldWork()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => it.Id).PrepareFromSql("select id from simple_entity -- this is custom sql", TestContext.Current.CancellationToken);
        // When
        var r = await _sut.DataProvider.ToListAsync(cmd);

        // Then

        r.Should().NotBeEmpty();

        r.Count.Should().Be(10);

        r[0].Should().Be(1);

        cmd = _sut.SimpleEntity.Select(it => it.Id).PrepareFromSql("select id from simple_entity where id=-1", TestContext.Current.CancellationToken);
        // When
        r = await _sut.DataProvider.ToListAsync(cmd);

        r.Should().BeEmpty();

        r = await _sut.SimpleEntity.Select(it => it.Id).ToListAsync();

        r.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ManualSqlWithParam_ShouldWork()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => new SimpleEntity { Id = it.Id }).PrepareFromSql("select id from simple_entity where id = @id", new { id = 1 }, TestContext.Current.CancellationToken);
        // When
        var r = await _sut.DataProvider.FirstAsync(cmd);

        // Then

        r.Should().NotBeNull();

        r.Id.Should().Be(1);
    }

    [Fact]
    public async Task ManualSqlWithTwoParam_ShouldWork()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => new SimpleEntity { Id = it.Id }).PrepareFromSql("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, TestContext.Current.CancellationToken);
        // When
        var r = await _sut.DataProvider.FirstAsync(cmd, 1);

        // Then

        r.Should().NotBeNull();

        r.Id.Should().Be(2);
    }

    [Fact]
    public async Task Select_MemeberInit()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => new SimpleEntity { Id = it.Id });
        // When
        var r = await cmd.FirstAsync();

        // Then

        r.Should().NotBeNull();

        r.Id.Should().Be(1);
    }

    [Fact]
    public void UnionScalar_ShouldWork()
    {
        var cnt = _sut.From(_sut.SimpleEntity.Select(it => it.Id).Union(_sut.ComplexEntity.Select(it => (int)it.Id))).Count();

        cnt.Should().Be(10);

        var cmd = _sut.SimpleEntity.Select(it => it.Id).UnionAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        cnt = _sut.From(cmd).Count();

        cnt.Should().Be(13);

        cnt = _sut.From(cmd.UnionAll(_sut.ComplexEntity.Select(it => it.Int))).Count();

        cnt.Should().Be(16);
    }

    [Fact]
    public void UnionEntity_ShouldWork()
    {
        var cnt = _sut.From(_sut.SimpleEntity.Select(it => new { it.Id }).Union(_sut.ComplexEntity.Select(it => new { it.Id }))).Count();

        cnt.Should().Be(10);

        cnt = _sut.From(_sut.SimpleEntity.Select(it => new { it.Id }).UnionAll(_sut.ComplexEntity.Select(it => new { it.Id }))).Count();

        cnt.Should().Be(13);
    }
}
