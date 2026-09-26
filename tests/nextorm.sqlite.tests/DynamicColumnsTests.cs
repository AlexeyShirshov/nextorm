using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
namespace NextORM.Sqlite.Tests;

/// <summary>
/// Exercises the read side of the dynamic-columns store against a real (temp-file) SQLite database: the
/// generated <c>SELECT</c> appends the source's <c>*</c> and the store receives every column that is not
/// mapped to a declared member.
/// </summary>
public class DynamicColumnsTests
{
    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-dynamic-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table dynamic_entity (id integer primary key, name text, age integer, city text);" +
                              "insert into dynamic_entity (id, name, age, city) values (1, 'Ann', 30, 'NY');" +
                              "insert into dynamic_entity (id, name, age, city) values (2, 'Bob', null, 'LA');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_ShouldCollectUnmappedColumns()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<DynamicColumnsEntity>().ToList();

            rows.Should().HaveCount(2);

            var ann = rows.Single(x => x.Id == 1);
            ann.Name.Should().Be("Ann");
            ann.Extra.Should().ContainKey("age").WhoseValue.Should().Be(30L);
            ann.Extra.Should().ContainKey("city").WhoseValue.Should().Be("NY");
            ann.Extra.Should().NotContainKey("id");
            ann.Extra.Should().NotContainKey("name");

            var bob = rows.Single(x => x.Id == 2);
            bob.Extra.Should().ContainKey("age").WhoseValue.Should().BeNull();
            bob.Extra.Should().ContainKey("city").WhoseValue.Should().Be("LA");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_ShouldGenerateStar()
    {
        using var ctx = SqliteTestContext.Create();
        var sql = ((DbPreparedQueryCommand<DynamicColumnsEntity>)ctx.GetPreparedQueryCommand(
            ctx.From<DynamicColumnsEntity>().ToCommand(), false, false, CancellationToken.None)).DbCommand.CommandText;

        sql.Replace("\r\n", "\n").Should().Be("select id, name, * from dynamic_entity");
    }

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_ShouldSupportTerminals()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.From<DynamicColumnsEntity>().Any().Should().BeTrue();
            ctx.From<DynamicColumnsEntity>().Count().Should().Be(2);

            var first = ctx.From<DynamicColumnsEntity>().OrderBy(x => x.Id).First();
            first.Id.Should().Be(1);
            first.Extra.Should().ContainKey("city").WhoseValue.Should().Be("NY");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_Quoted_ShouldQuoteMappedColumnsOnly()
    {
        using var ctx = SqliteTestContext.CreateQuoted();
        var sql = ((DbPreparedQueryCommand<DynamicColumnsEntity>)ctx.GetPreparedQueryCommand(
            ctx.From<DynamicColumnsEntity>().ToCommand(), false, false, CancellationToken.None)).DbCommand.CommandText;

        sql.Replace("\r\n", "\n").Should().Be("select \"id\", \"name\", * from \"dynamic_entity\"");
    }

    [Fact]
    public void ReadEntity_WithFluentDynamicColumnsStore_ShouldCollectUnmappedColumns()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = ctx.From<FluentDynamicEntity>((EntityMetadataBuilder<FluentDynamicEntity> b) =>
            {
                b.Property(x => x.Id).Key().HasColumnName("id");
                b.Property(x => x.Extra).DynamicColumnsStore();
            });

            var rows = entity.ToList();

            rows.Should().HaveCount(2);
            var ann = rows.Single(x => x.Id == 1);
            ann.Extra.Should().ContainKey("name").WhoseValue.Should().Be("Ann");
            ann.Extra.Should().ContainKey("age").WhoseValue.Should().Be(30L);
            ann.Extra.Should().NotContainKey("id");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

[SqlTable("dynamic_entity")]
public class FluentDynamicEntity
{
    public int Id { get; set; }

    public Dictionary<string, object?> Extra { get; set; } = new();
}
