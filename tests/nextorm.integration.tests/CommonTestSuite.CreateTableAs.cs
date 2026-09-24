using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static string CreateTableAsName(int seed) => "ctas_" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void CreateTableAs_TempTable_ShouldBeReadableOnTheSameContext()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect, "This provider cannot materialise a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var marker = "ctas-" + Guid.NewGuid().ToString("N");
        var keep = Random.Shared.Next(1_000_000, int.MaxValue);
        var drop = keep + 1;
        var name = CreateTableAsName(keep);

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = keep, Name = marker, Age = 1 },
            new DeleteEntity { Id = drop, Name = marker, Age = 2 },
        ]).Insert();

        ctx.From<IDeleteEntity>()
            .Where(x => x.Id == keep)
            .Select(x => new { x.Id, x.Name })
            .ToTempTable(name);

        var rows = ctx.From(name)
            .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(keep);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void CreateTableAs_TempTableIfNotExists_ShouldBeRepeatable()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect, "This provider cannot materialise a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);
        var options = new CreateTableAsOptions { IfNotExists = true };

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ctas-repeat", Age = 1 })
            .Insert();

        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id }).ToTempTable(name, options);
        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id }).ToTempTable(name, options);

        ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle();
    }

    [Fact]
    public void CreateTableAs_WithCteBody_ShouldBeReadableOnTheSameContext()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect, "This provider cannot materialise a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ctas-cte", Age = 1 })
            .Insert();

        var cte = ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id });
        ctx.With("recent", cte)
            .From("recent")
            .Select(t => new { Id = t.GetInt32("id") })
            .ToTempTable(name);

        ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle();
    }

    [Fact]
    public void CreateTableAs_Table_ShouldBeReadableOnTheSameContext()
    {
        Assert.SkipUnless(Provider.SupportsCreateTableAsSelect, "This provider cannot materialise a query into a table.");
        var ctx = _sut.DataProvider;
        var marker = "ctas-" + Guid.NewGuid().ToString("N");
        var keep = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(keep);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = keep, Name = marker, Age = 1 })
            .Insert();

        try
        {
            ctx.From<IDeleteEntity>()
                .Where(x => x.Id == keep)
                .Select(x => new { x.Id, x.Name })
                .ToTable(name);

            var rows = ctx.From(name)
                .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
                .ToList();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(keep);
            rows[0].Name.Should().Be(marker);
        }
        finally
        {
            var connections = (IConnectionManager)ctx;
            connections.EnsureConnectionOpen();
            using var cmd = connections.GetConnection().CreateCommand();
            cmd.CommandText = "drop table if exists " + name;
            cmd.ExecuteNonQuery();
        }
    }

    [Fact]
    public void CreateTableAs_UnsupportedProvider_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsTemporaryCreateTableAsSelect, "This provider materialises a query into a temporary table.");
        var ctx = _sut.DataProvider;

        var act = () => ctx.From<IDeleteEntity>().Select(x => new { x.Id }).ToTempTable(CreateTableAsName(1));

        act.Should().Throw<NotSupportedException>();
    }
}
