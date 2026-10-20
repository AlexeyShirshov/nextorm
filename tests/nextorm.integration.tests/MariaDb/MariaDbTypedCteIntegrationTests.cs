using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// MariaDB-only coverage for the typed recursive CTE (#146 slice B). MariaDB has no
/// <see cref="CommonTestSuite"/>-based integration class (it uses its own <see cref="MariaDbContainer"/>
/// harness), so the shared recursive facts are re-pinned here against a real MariaDB server.
/// </summary>
public sealed class MariaDbTypedCteIntegrationTests : IDisposable
{
    private readonly IDataContext _ctx;

    public MariaDbTypedCteIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void TypedRecursiveCte_ShouldMaterializeBoundedSeries()
    {
        var nums = _ctx.From<ITypedCteNumber>()
            .Where(x => x.Id == 1)
            .Select(x => x.Id)
            .AsRecursiveCte("nums", self => _ctx.From(self).Where(n => n < 5).Select(n => n + 1));

        var rows = _ctx.From(nums).Limit(20).ToList();

        rows.OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public void TypedRecursiveCte_DuplicateAnchorRows_ShouldBeRetained()
    {
        // Two anchor rows project the same value and the step never fires: UNION ALL keeps both.
        var nums = _ctx.From<ITypedCteNumber>()
            .Where(x => x.Id <= 2)
            .Select(_ => 1)
            .AsRecursiveCte("nums_dupes", self => _ctx.From(self).Where(n => n < 1).Select(n => n + 1));

        var rows = _ctx.From(nums).Limit(20).ToList();

        rows.Should().HaveCount(2);
        rows.Should().OnlyContain(n => n == 1);
    }

    private void Seed()
    {
        Execute("drop table if exists typed_cte_number");
        Execute("create table typed_cte_number (id int not null primary key)");
        Execute("insert into typed_cte_number (id) values (1), (2)");
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}

[SqlTable("typed_cte_number")]
internal interface ITypedCteNumber
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
