using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using nextorm.core;

namespace nextorm.integration.tests;

/// <summary>
/// Exercises <see cref="SqlTableFunctionAttribute"/> against a real database. The portable
/// row-returning function used here is SQLite's bundled <c>json_each</c>; providers that do not
/// expose an equivalent FROM table-valued function are skipped via
/// <see cref="ITestProvider.SupportsTableValuedFunctions"/>.
/// </summary>
public abstract partial class CommonTestSuite
{
    /// <summary>
    /// Row shape of <c>json_each</c>: for a JSON array, <c>key</c> is the zero-based index and
    /// <c>value</c> the element.
    /// </summary>
    public interface IJsonEachRow
    {
        [Column("key")]
        long Key { get; set; }
        [Column("value")]
        long Value { get; set; }
    }

    private static class Tvf
    {
        [SqlTableFunction("json_each")]
        public static IQueryable<IJsonEachRow> JsonEach(string json) => throw new NotSupportedException();
    }

    [Fact]
    public void TableFunction_JsonEach_ShouldReturnRows()
    {
        Assert.SkipUnless(Provider.SupportsTableValuedFunctions, Provider.TableValuedFunctionSkipReason);

        var values = _sut.DataProvider
            .FromTableFunction(() => Tvf.JsonEach("[1,2,3]"))
            .Select(r => r.Value)
            .ToList();

        values.OrderBy(v => v).Should().Equal(1L, 2L, 3L);
    }

    [Fact]
    public void TableFunction_JoinedToTable_ShouldReturnRows()
    {
        Assert.SkipUnless(Provider.SupportsTableValuedFunctions, Provider.TableValuedFunctionSkipReason);

        var rows = _sut.DataProvider
            .FromTableFunction(() => Tvf.JsonEach("[1,2,3]"))
            .Join(_sut.SimpleEntity, (j, s) => j.Value == s.Id)
            .Select(p => p.t2.Id)
            .ToList();

        rows.OrderBy(v => v).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void TableFunction_WhereAndOrderBy_ShouldFilterAndSort()
    {
        Assert.SkipUnless(Provider.SupportsTableValuedFunctions, Provider.TableValuedFunctionSkipReason);

        var values = _sut.DataProvider
            .FromTableFunction(() => Tvf.JsonEach("[3,1,2]"))
            .Where(r => r.Value > 1)
            .OrderByDescending(r => r.Value)
            .Select(r => r.Value)
            .ToList();

        values.Should().Equal(3L, 2L);
    }

    [Fact]
    public void TableFunction_GroupBy_ShouldAggregate()
    {
        Assert.SkipUnless(Provider.SupportsTableValuedFunctions, Provider.TableValuedFunctionSkipReason);

        var rows = _sut.DataProvider
            .FromTableFunction(() => Tvf.JsonEach("[1,1,2]"))
            .GroupBy(r => new { r.Value })
            .Select(g => new { g.Value, Cnt = NORM.SQL.count() })
            .ToList();

        rows.OrderBy(r => r.Value).Select(r => (r.Value, r.Cnt)).Should().Equal((1L, 2), (2L, 1));
    }
}
