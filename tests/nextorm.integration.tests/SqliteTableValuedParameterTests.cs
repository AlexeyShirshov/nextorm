using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// SQLite emulates a table-valued parameter with a JSON document: an entity set is expanded with
/// <c>json_each</c>/<c>json_extract</c>, a scalar set with <c>json_each</c>. Unlike the in-memory unit
/// tests in <c>nextorm.sqlite.tests</c>, these run against the seeded provider database, so the
/// insert + read-back path is exercised end to end.
/// </summary>
[Collection("Sqlite")]
[Trait("D162", "Conformance")]
public sealed class SqliteTableValuedParameterTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private sealed class TvpRow
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_InsertSelectEntity_ShouldPersistRows()
    {
        var ctx = _sut.DataProvider;
        var marker = "tvp_" + Guid.NewGuid().ToString("N");
        var baseId = Random.Shared.Next(10_000_000, int.MaxValue / 2);

        var rows = new[]
        {
            new TvpRow { Id = baseId, Name = marker, Age = 1 },
            new TvpRow { Id = baseId + 1, Name = null, Age = 2 },
        };

        using (ctx.ExecuteRaw(
            "insert into delete_entity (id, name, age) "
            + "select json_extract(value, '$.Id'), json_extract(value, '$.Name'), json_extract(value, '$.Age') from json_each(@rows)",
            [ProcedureParameter.Table("rows", rows)]))
        {
        }

        var persisted = ctx.From<IDeleteEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Id, x.Age })
            .ToList();

        persisted.Should().ContainSingle();
        persisted[0].Id.Should().Be(baseId);
        persisted[0].Age.Should().Be(1);

        ctx.From<IDeleteEntity>()
            .Where(x => x.Id == baseId + 1)
            .Select(x => x.Name)
            .ToList()
            .Should()
            .ContainSingle()
            .Which.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteRawAsync_TableParameter_ScalarInClause_ShouldFilter()
    {
        var ctx = _sut.DataProvider;

        await using var result = await ctx.ExecuteRawAsync(
            "select id from simple_entity where id in (select value from json_each(@ids)) order by id",
            [ProcedureParameter.Table("ids", new[] { 1, 3, 5 })],
            TestContext.Current.CancellationToken);

        var read = new List<int>();
        await foreach (var id in result.ReadAsync<int>(TestContext.Current.CancellationToken))
            read.Add(id);

        read.Should().Equal(1, 3, 5);
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptyScalarSet_ShouldMatchNothing()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select id from simple_entity where id in (select value from json_each(@ids))",
            [ProcedureParameter.Table("ids", Array.Empty<int>())]);

        result.Read<int>().Should().BeEmpty();
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptyEntitySet_ShouldMatchNothing()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "select json_extract(value, '$.Id') as Id from json_each(@rows)",
            [ProcedureParameter.Table("rows", Array.Empty<TvpRow>())]);

        result.Read<int>().Should().BeEmpty();
    }
}
