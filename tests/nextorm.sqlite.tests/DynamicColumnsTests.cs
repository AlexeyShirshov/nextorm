using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
                              "insert into dynamic_entity (id, name, age, city) values (2, 'Bob', null, 'LA');" +
                              "create table dynamic_write_entity (id integer primary key, name text, age integer default 99, city text);" +
                              "create table range_dynamic_write_entity (id integer primary key, during_lower integer, during_upper integer, \"During\" integer);";
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

    [Fact]
    public void ReadEntity_WithDynamicColumnsStore_JoinedQueryAfterCachedSelectList_ShouldThrow()
    {
        using var ctx = SqliteTestContext.Create();

        // Warm the whole-entity select list cache for the dynamic-columns entity with a non-join query.
        // The join guard must run before the cache lookup, so the cached (store-bearing) select list is
        // still rejected instead of silently reused for a joined query.
        _ = ctx.GetPreparedQueryCommand(
            ctx.From<DynamicColumnsEntity>().ToCommand(), false, false, CancellationToken.None);

        var joined = ctx.From<DynamicColumnsEntity>()
            .SemiJoin(ctx.From<ISimpleEntity>(), (e, s) => e.Id == s.Id);

        var act = () => ctx.GetPreparedQueryCommand(joined.ToCommand(), false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*single physical source*");
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_ShouldWriteColumnsOrdinalSortedAndReadBack()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = new DynamicWriteEntity { Id = 1, Name = "Ann" };
            // Insert in reverse ordinal order: "city" before "age"; the set is ordinal-sorted to age, city.
            entity.Extra["city"] = "NY";
            entity.Extra["age"] = 30;

            ctx.InsertInto<DynamicWriteEntity>()
                .Values(entity)
                .ToSql()
                .Should().Be("insert into dynamic_write_entity (id, name, \"age\", \"city\") values ($p0, $p1, $p2, $p3)");

            ctx.InsertInto<DynamicWriteEntity>().Values(entity).Insert().Should().Be(1);

            var read = ctx.From<DynamicWriteEntity>().ToList().Single(x => x.Id == 1);
            read.Name.Should().Be("Ann");
            read.Extra.Should().ContainKey("age").WhoseValue.Should().Be(30L);
            read.Extra.Should().ContainKey("city").WhoseValue.Should().Be("NY");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_PresentNullKeyShouldStoreNull()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = new DynamicWriteEntity { Id = 2, Name = "Bob" };
            entity.Extra["age"] = null;
            entity.Extra["city"] = "LA";

            ctx.InsertInto<DynamicWriteEntity>().Values(entity).Insert().Should().Be(1);

            var read = ctx.From<DynamicWriteEntity>().ToList().Single(x => x.Id == 2);
            read.Extra.Should().ContainKey("age").WhoseValue.Should().BeNull();
            read.Extra.Should().ContainKey("city").WhoseValue.Should().Be("LA");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_MissingKeyShouldStoreColumnDefault()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = new DynamicWriteEntity { Id = 3, Name = "Cid" };
            // "age" is deliberately omitted, so the seeded column default (99) must survive.
            entity.Extra["city"] = "SF";

            ctx.InsertInto<DynamicWriteEntity>().Values(entity).Insert().Should().Be(1);

            var read = ctx.From<DynamicWriteEntity>().ToList().Single(x => x.Id == 3);
            read.Extra.Should().ContainKey("age").WhoseValue.Should().Be(99L);
            read.Extra.Should().ContainKey("city").WhoseValue.Should().Be("SF");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_UpdateDynamicColumns_OmittedKeyShouldKeepStoredValue()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var inserted = new DynamicWriteEntity { Id = 4, Name = "Dan" };
            inserted.Extra["age"] = 30;
            inserted.Extra["city"] = "NY";
            ctx.InsertInto<DynamicWriteEntity>().Values(inserted).Insert().Should().Be(1);

            var update = new DynamicWriteEntity { Id = 4, Name = "Dan2" };
            // "age" is omitted: it must keep 30, never the physical column's default 99.
            update.Extra["city"] = "LA";

            ctx.Update<DynamicWriteEntity>()
                .Set(update)
                .Where(x => x.Id == 4)
                .ToSql()
                .Should().Be("update dynamic_write_entity set name = $p0, \"city\" = $p1 where id = 4");

            ctx.Update<DynamicWriteEntity>().Set(update).Where(x => x.Id == 4).Update().Should().Be(1);

            var read = ctx.From<DynamicWriteEntity>().ToList().Single(x => x.Id == 4);
            read.Name.Should().Be("Dan2");
            read.Extra.Should().ContainKey("age").WhoseValue.Should().Be(30L);
            read.Extra.Should().ContainKey("city").WhoseValue.Should().Be("LA");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_MultiRowDifferentKeysShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var first = new DynamicWriteEntity { Id = 5, Name = "Eve" };
            first.Extra["age"] = 1;
            var second = new DynamicWriteEntity { Id = 6, Name = "Fox" };
            second.Extra["city"] = "X";

            var act = () => ctx.InsertInto<DynamicWriteEntity>().Values([first, second]);

            act.Should().Throw<InvalidOperationException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("a\0b")]
    public void WriteEntity_InsertDynamicColumns_InvalidKeyShouldThrow(string key)
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = new DynamicWriteEntity { Id = 7, Name = "Gus" };
            entity.Extra[key] = 1;

            var act = () => ctx.InsertInto<DynamicWriteEntity>().Values(entity);

            act.Should().Throw<ArgumentException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_MergeDynamicColumns_ShouldExtendInsertAndSetButNeverOn()
    {
        using var ctx = SqliteTestContext.Create();
        var entity = new DynamicWriteEntity { Id = 1, Name = "a" };
        entity.Extra["age"] = 5;

        var sql = ctx.MergeInto<DynamicWriteEntity>()
            .Using(entity)
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Contain("insert into dynamic_write_entity (id, name, \"age\")");
        sql.Should().Contain("on conflict (id) do update set name = excluded.name, \"age\" = excluded.\"age\"");
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_MultiRow_ShouldWriteAndReadBackEveryRow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            DynamicWriteEntity Row(int id, int age, string city)
            {
                var row = new DynamicWriteEntity { Id = id, Name = $"row-{id}" };
                // Reverse dictionary order on purpose: the writer ordinal-sorts the physical keys.
                row.Extra["city"] = city;
                row.Extra["age"] = age;
                return row;
            }

            var rows = new[] { Row(10, 10, "AA"), Row(11, 11, "BB"), Row(12, 12, "CC") };

            // One column list for the whole statement, one value tuple per row, parameters sequential.
            ctx.InsertInto<DynamicWriteEntity>()
                .Values(rows)
                .ToSql()
                .Should().Be(
                    "insert into dynamic_write_entity (id, name, \"age\", \"city\") " +
                    "values ($p0, $p1, $p2, $p3), ($p4, $p5, $p6, $p7), ($p8, $p9, $p10, $p11)");

            ctx.InsertInto<DynamicWriteEntity>().Values(rows).Insert().Should().Be(3);

            var read = ctx.From<DynamicWriteEntity>().ToList()
                .Where(x => x.Id is >= 10 and <= 12)
                .ToDictionary(x => x.Id);

            read.Should().HaveCount(3);
            read[10].Extra["age"].Should().Be(10L);
            read[10].Extra["city"].Should().Be("AA");
            read[11].Extra["age"].Should().Be(11L);
            read[11].Extra["city"].Should().Be("BB");
            read[12].Extra["age"].Should().Be(12L);
            read[12].Extra["city"].Should().Be("CC");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_KeyCollidingWithMappedColumnShouldThrow()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = new DynamicWriteEntity { Id = 20, Name = "Eve" };
            // "name" is a mapped physical column of dynamic_write_entity; the store must not shadow it.
            entity.Extra["name"] = "shadow";

            var act = () => ctx.InsertInto<DynamicWriteEntity>().Values(entity);

            act.Should().Throw<InvalidOperationException>().WithMessage("*name*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteEntity_InsertDynamicColumns_KeyEqualToRangeClrNameShouldRoundTrip()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var entity = new RangeDynamicWriteEntity
            {
                Id = 30,
                During = new Range<int>(1, 10),
            };
            // "During" is the range property's CLR name, not either physical bound column
            // (during_lower/during_upper). On read it is not a mapped name, so the read side collects it
            // as a dynamic column; on write it must be an ordinary dynamic key, not a collision.
            entity.Extra["During"] = 7;

            ctx.InsertInto<RangeDynamicWriteEntity>().Values(entity).Insert().Should().Be(1);

            var read = ctx.From<RangeDynamicWriteEntity>().ToList().Single(x => x.Id == 30);
            read.During.Should().Be(new Range<int>(1, 10));
            read.Extra.Should().ContainKey("During").WhoseValue.Should().Be(7L);
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

[SqlTable("range_dynamic_write_entity")]
public class RangeDynamicWriteEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [RangeColumns("during_lower", "during_upper")]
    public Range<int> During { get; set; }

    [DynamicColumns]
    public Dictionary<string, object?> Extra { get; set; } = new();
}
