using System.Data.SQLite;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using nextorm.core;
using OneOf.Types;

namespace nextorm.sqlite.tests;

public class SqlCommandTests
{
    private readonly TestDataRepository _sut;
    private readonly ILogger<SqlCommandTests> _logger;
    private readonly IServiceProvider _sp;

    public SqlCommandTests(TestDataRepository sut, ILogger<SqlCommandTests> logger, IServiceProvider sp)
    {
        _sut = sut;
        _logger = logger;
        _sp = sp;
    }

    [Fact]
    public async void SelectEntity_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectEntityToList_ShouldReturnData()
    {
        (await _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).CountAsync()).Should().Be(10);
    }
    [Fact]
    public async Task SelectModifiedEntity_ShouldReturnData()
    {
        long idx = 1;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id + 1 }))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectTable_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new { Id = tbl.Long("id") }))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectSubQuery_ShouldReturnData()
    {
        await foreach (var row in _sut.From(_sut.From("simple_entity").Select(tbl => new { Id = tbl.Long("id") })).Select(subQuery => new { subQuery.Id }))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async Task SelectComplexEntity_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.Datetime, it.RequiredString, it.Boolean, it.Date, it.Double, it.Int, it.Numeric, it.Real, it.SmallInt, it.String, it.TinyInt }))
        {
            _logger.LogInformation("{entity}", row);
        }
    }
    [Fact]
    public async Task SelectEntity_ShouldCancel_WhenCancel()
    {
        CancellationTokenSource tokenSource = new();
        await foreach (var row in _sut.SimpleEntity.Select(entity => new { Id = (long)entity.Id }).WithCancellation(tokenSource.Token))
        {
            _logger.LogInformation("{entity}", row);
            tokenSource.Cancel();
            break;
        }
        _logger.LogInformation("Loop cancelled");
    }
    [Fact]
    public async void SelectEntityIntoDTO_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity.Select(entity => new SimpleEntityDTO(entity.Id)))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectTableIntoDTO_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new SimpleEntityDTO(tbl.Long("id"))))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectEntityIntoTuple_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new Tuple<long>(tbl.Long("id"))))
        {
            _logger.LogInformation("Id = {id}", row.Item1);
        }
    }
    [Fact]
    public async void SelectEntityIntoRecord_ShouldReturnData()
    {
        await foreach (var row in _sut.From("simple_entity").Select(tbl => new SimpleEntityRecord(tbl.Long("id"))))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectNestedEntityIntoRecord_ShouldReturnData()
    {
        var nested = _sut.From("simple_entity").Select(tbl => new SimpleEntityRecord(tbl.Long("id")));
        await foreach (var row in _sut.From(nested).Select(rec => new { rec.Id }))
        {
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async Task SelectComplexEntityWithCalculatedFields_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.RequiredString, it.String, CalcString = it.RequiredString + "/" + it.String }))
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

        await foreach (var row in _sut.From(nested).Select(t1 => new { t1.Id, t1.RequiredString, t1.String, t1.CalcString }))
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
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.RequiredString, it.String, CalcString = it.RequiredString + "/" + (it.String ?? "") }))
        {
            row.CalcString.Should().Be($"{row.RequiredString}/{row.String ?? string.Empty}");
        }
    }
    [Fact]
    public async Task SelectComplexEntityWithCalculatedNumericFields_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt, it.Real, it.Double, Calc = it.TinyInt + it.SmallInt, Calc2 = (it.Real ?? 1) + it.Double }))
        {
            row.Calc.Should().Be(row.TinyInt + row.SmallInt);
            row.Calc2.Should().Be((row.Real ?? 1f) + row.Double);
        }
    }
    [Fact]
    public async Task SelectComplexEntityWithCalculatedNumericFields2_ShouldReturnData()
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt, Calc = (it.TinyInt + it.SmallInt) * 2 }))
        {
            row.Calc.Should().Be((row.TinyInt + row.SmallInt) * 2);
        }
    }
    [InlineData(2)]
    [Theory]
    public async Task SelectComplexEntityWithCalculatedNumericFieldsWithParam_ShouldReturnData(int i)
    {
        await foreach (var row in _sut.ComplexEntity.Select(it => new { it.Id, it.TinyInt, it.SmallInt, Calc = (it.TinyInt + it.SmallInt) * i }))
        {
            row.Calc.Should().Be((row.TinyInt + row.SmallInt) * i);
        }
    }
    [Fact]
    public async void SelectEntity_WhenFilterById_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id == 1)
            .Select(entity => new { Id = (long)entity.Id }))
        {
            idx++;
            row.Id.Should().Be(1);
        }
        idx.Should().Be(1);
    }
    [Fact]
    public async void SelectEntity_WhenFilterByIdNeg_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id != 1)
            .Select(entity => new { Id = (long)entity.Id }))
        {
            idx++;
        }
        idx.Should().Be(9);
    }
    [Fact]
    public async void SelectEntity_WhenFilterByIdGt_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id > 1)
            .Select(entity => new { Id = (long)entity.Id }))
        {
            idx++;
        }
        idx.Should().Be(9);
    }
    [Fact]
    public async void SelectEntity_WhenFilterByIdGte_ShouldReturnFilteredData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntity
            .Where(entity => entity.Id >= 9)
            .Select(entity => new { Id = (long)entity.Id }))
        {
            idx++;
        }
        idx.Should().Be(2);
    }
    [Fact]
    public async void SelectEntity_WhenFilterByIdLt_ShouldReturnFilteredData()
    {
        var r = await _sut.SimpleEntity
            .Where(entity => entity.Id < 1)
            .Select(entity => new { Id = (long)entity.Id }).AnyAsync();

        r.Should().BeFalse();
    }
    [Fact]
    public async void SelectEntity_WhenFilterByIdLte_ShouldReturnFilteredData()
    {
        var row = await _sut.SimpleEntity
            .Where(entity => entity.Id <= 1)
            .Select(entity => new { Id = (long)entity.Id }).FirstOrDefaultAsync();

        row.Should().NotBeNull();
        row.Id.Should().Be(1);
    }
    [InlineData(1)]
    [Theory]
    public async void SelectEntityParam_WhenFilterById_ShouldReturnFilteredData(long id)
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
    public async void ComplexEntityParam_WhenFilterByString_ShouldReturnFilteredData(string str)
    {
        await foreach (var row in _sut.ComplexEntity
            .Where(entity => entity.String == str)
            .Select(entity => new { entity.Id, entity.String }))
        {
            row.String.Should().Be(str);

            if (row.Id == 3)
                row.String.Should().BeNull();
        }

    }
    [Fact]
    public async void SelectEntityParam2_WhenFilterById_ShouldReturnFilteredData()
    {
        var e = new cls1(1);

        var row = await _sut.SimpleEntity
            .Where(entity => entity.Id == e.Id)
            .Select(entity => new { Id = (long)entity.Id }).FirstOrDefaultAsync();

        row.Should().NotBeNull();

        row.Id.Should().Be(1);
    }
    [Fact]
    public async void SelectTableWithWhere_ShouldReturnData()
    {
        var row = await _sut.From("simple_entity")
            .Where(tbl => tbl.Long("id") == 1)
            .Select(tbl => new { Id = tbl.Long("id") }).FirstOrDefaultAsync();

        row.Should().NotBeNull();

        row.Id.Should().Be(1);
    }
    [Fact]
    public async void SelectTableWithWhereCalc_ShouldReturnData()
    {
        var row = await _sut.From("simple_entity")
            .Where(tbl => tbl.Long("id") + 2 == 1)
            .Select(tbl => new { Id = tbl.Long("id") }).FirstOrDefaultAsync();

        row.Should().BeNull();
    }
    [Fact]
    public async void SelectEntityAsClass_ShouldReturnData()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntityAsClass)
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id = {id}", row.Id);
        }
    }
    [Fact]
    public async void SelectSubQueryWhere_ShouldReturnData()
    {
        var idx = 0;
        await foreach (var row in _sut.From(_sut.SimpleEntity.Where(it => it.Id > 8)).Select(it => new { it.Id }))
        {
            idx++;
            _logger.LogInformation("Id = {id}", row.Id);
        }

        idx.Should().Be(2);
    }
    class cls1
    {
        public cls1(int id)
        {
            Id = id;
        }

        public long Id { get; set; }
        public string OtherValue { get; set; } = "h";
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
    }
    [Fact]
    public async Task SelectAny_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Where(it => it.Boolean == true).Select(it => it).AnyAsync();

        r.Should().BeTrue();
    }
    [Fact]
    public async Task SelectAsterisk_ShouldReturnTrue()
    {
        long idx = 0;
        await foreach (var row in _sut.SimpleEntityAsClass.Select(it => it))
        {
            idx++;
            idx.Should().Be(row.Id);
            _logger.LogInformation("Id: {id}", row.Id);
        }
    }
    [Fact]
    public async Task SelectAnyParam_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Where(it => it.Boolean == NORM.Param<bool>(0)).AnyAsync(true);

        r.Should().BeTrue();
    }
    [Fact]
    public async Task SelectAnyAsProp_ShouldReturnTrue()
    {
        var r = await _sut.ComplexEntity.Select(it => new { it.Id, exists = NORM.SQL.exists(_sut.SimpleEntity) }).ToListAsync();

        r.Should().NotBeEmpty();
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
    public void OrderBy2_ShouldSortData()
    {
        // Given
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).OrderByDescending(it => it.Id).Select(it => new { it.Id }).ToList();
        // When
        r[0].Id.Should().Be(1);
        r[1].Id.Should().Be(3);
        r[2].Id.Should().Be(2);
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
        var r = await _sut.ComplexEntity.Select(it => new { it.Id, sid = _sut.SimpleEntity.OrderByDescending(it => it.Id).Select(it => it.Id).Single() }).ToListAsync();

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
        var r = await _sut.ComplexEntity.Where(it => NORM.SQL.@in((int)it.Id, _sut.SimpleEntity.Where(it => it.Id == 2).Select(it => it.Id))).Select(it => it.Id).FirstOrDefaultAsync();

        r.Should().Be(2);
    }
    [Fact]
    public async Task WhereAnySubQuery_ShouldReturnData()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == NORM.SQL.any(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SQLiteException>();
    }
    [Fact]
    public async Task WhereAllSubQuery_ShouldReturnData()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == NORM.SQL.all(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SQLiteException>();
    }
    [Fact]
    public async Task ManualSql_ShouldWork()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => it.Id).Compile("select id from simple_entity --this is my sql");
        // When
        var r = await cmd.ToListAsync();

        // Then

        r.Should().NotBeEmpty();

        r.Count.Should().Be(10);

        r[0].Should().Be(1);

        cmd = _sut.SimpleEntity.Select(it => it.Id).Compile("select id from simple_entity where id=-1");
        // When
        r = await cmd.ToListAsync();

        r.Should().BeEmpty();

        r = await _sut.SimpleEntity.Select(it => it.Id).ToListAsync();

        r.Should().NotBeEmpty();
    }
    [Fact]
    public async Task ManualSqlWithParam_ShouldWork()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => new SimpleEntity { Id = it.Id }).Compile("select id from simple_entity where id = @id", new { id = 1 }, true);
        // When
        var r = await cmd.FirstAsync();

        // Then

        r.Should().NotBeNull();

        r.Id.Should().Be(1);
    }
    [Fact]
    public async Task ManualSqlWithTwoParam_ShouldWork()
    {
        // Given
        var cmd = _sut.SimpleEntity.Select(it => new SimpleEntity { Id = it.Id }).Compile("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, true);
        // When
        var r = await cmd.FirstAsync(1);

        // Then

        r.Should().NotBeNull();

        r.Id.Should().Be(2);
    }
}