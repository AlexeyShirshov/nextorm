using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 3 — the FROM-source rendering surface of
/// <c>src/nextorm.core/DataContext/Sql/SqlSourceRenderer.cs</c> on a live SQLite database: the untyped
/// <c>From("table")</c> source (both the legacy <c>TableAlias</c> accessors and the
/// <c>SqlFunctions.Column</c> by-name route), a derived source built from another query, a CTE read
/// through a raw name, and the table-valued <c>json_each</c> source in its bare-column, transparent
/// and entity-item projection shapes.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4SourceRendererTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_src_row (id integer primary key, label text null, num int null);
        delete from s4_src_row;
        insert into s4_src_row (id, label, num) values
            (1, 'a', 10),
            (2, 'b', null),
            (3, null, 30);
        """;

    public SqliteStep4SourceRendererTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    /// <summary>The legacy TableAlias accessors read an untyped source into an anonymous projection.</summary>
    [Fact]
    public void RawSource_Accessors_ShouldReadColumns()
    {
        var rows = Context.From("s4_src_row")
            .OrderBy(t => t.GetInt32("id"))
            .Select(t => new
            {
                Id = t.GetInt32("id"),
                Label = t.GetString("label"),
                Num = t.GetNullableInt32("num")
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows[0].Should().Be(new { Id = 1, Label = "a", Num = (int?)10 });
        rows[1].Label.Should().Be("b");
        rows[1].Num.Should().BeNull();
        rows[2].Label.Should().BeNull("the stored NULL survives the raw read");
        rows[2].Num.Should().Be(30);
    }

    /// <summary>The column indexer reads the same untyped source through its typed column wrapper.</summary>
    [Fact]
    public void RawSource_Indexer_ShouldReadColumns()
    {
        var rows = Context.From("s4_src_row")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => new
            {
                Id = t["id"].AsInt,
                Label = t["label"].AsNullableString
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3);
        rows[0].Label.Should().Be("a");
        rows[2].Label.Should().BeNull();
    }

    /// <summary>The by-name <c>SqlFunctions.Column</c> route reads a raw source without the accessor table.</summary>
    [Fact]
    public void RawSource_ByNameColumn_ShouldReadColumns()
    {
        var ids = Context.From("s4_src_row")
            .Where(t => SqlFunctions.Column<int>(t, "id") > 1)
            .OrderBy(t => SqlFunctions.Column<int>(t, "id"))
            .Select(t => SqlFunctions.Column<int>(t, "id"))
            .ToList();

        ids.Should().Equal(2, 3);
    }

    [Fact]
    public void RawSource_TypedNullableAccessors_ShouldPreserveZeroAndDatabaseNull()
    {
        using (var setup = Context.ExecuteRaw("""
            create table if not exists s4_src_typed (id integer primary key, n integer, weight real, payload blob);
            delete from s4_src_typed;
            insert into s4_src_typed values (1, 0, 1.25, X'00FF'), (2, null, null, null), (3, 7, -2.5, X'');
            """))
        {
        }

        var rows = Context.From("s4_src_typed")
            .OrderBy(t => t.GetInt32("id"))
            .Select(t => new
            {
                Id = t.GetInt32("id"),
                Small = t.GetNullableInt16("n"),
                Wide = t.GetNullableInt64("n"),
                Byte = t.GetNullableByte("n"),
                Flag = t.GetNullableBoolean("n"),
                Float = t.GetNullableSingle("weight"),
                Double = t.GetNullableDouble("weight"),
                Blob = t.GetNullableBytes("payload")
            })
            .ToList();

        rows.Select(r => r.Id).Should().Equal(1, 2, 3);
        rows.Select(r => r.Small).Should().Equal((short?)0, null, 7);
        rows.Select(r => r.Wide).Should().Equal(0L, null, 7L);
        rows.Select(r => r.Byte).Should().Equal((byte?)0, null, 7);
        rows.Select(r => r.Flag).Should().Equal(false, null, true);
        rows.Select(r => r.Float).Should().Equal(1.25f, null, -2.5f);
        rows.Select(r => r.Double).Should().Equal(1.25, null, -2.5);
        rows[0].Blob.Should().Equal(0, 255);
        rows[1].Blob.Should().BeNull();
        rows[2].Blob.Should().BeEmpty();
    }

    [Fact]
    public void RawSource_TypedAccessors_ShouldReadDateGuidAndDecimal()
    {
        using (var setup = Context.ExecuteRaw("""
            create table if not exists s4_src_values (id integer primary key, moment text, token text, amount text);
            delete from s4_src_values;
            insert into s4_src_values values (1, '2024-02-29 13:14:15', 'f970c0f8-84c0-417b-97ee-842a90866fdf', '19.875');
            """))
        {
        }

        var row = Context.From("s4_src_values")
            .Select(t => new
            {
                Moment = t.GetDateTime("moment"),
                Token = t.GetGuid("token"),
                Amount = t.GetDecimal("amount")
            })
            .Single();

        row.Moment.Should().Be(new DateTime(2024, 2, 29, 13, 14, 15, DateTimeKind.Unspecified));
        row.Token.Should().Be(Guid.Parse("f970c0f8-84c0-417b-97ee-842a90866fdf"));
        row.Amount.Should().Be(19.875m);
    }

    /// <summary>Two raw sources join on their by-name columns.</summary>
    [Fact]
    public void RawSource_JoinedToRawSource_ShouldJoin()
    {
        var rows = Context.From("s4_src_row")
            .Join(Context.From("s4_src_row"), (a, b) => a.GetInt32("id") == b.GetInt32("id"))
            .Where(p => p.Item1.GetInt32("id") <= 2)
            .OrderBy(p => p.Item1.GetInt32("id"))
            .Select(p => new
            {
                Id = p.Item1.GetInt32("id"),
                Mirror = p.Item2.GetInt32("id")
            })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Should().Be(new { Id = 1, Mirror = 1 });
    }

    /// <summary>A derived source wraps another query and still filters and projects.</summary>
    [Fact]
    public void DerivedSource_ShouldReadRows()
    {
        var inner = Context.From<S4SrcRow>()
            .Where(x => x.Num != null)
            .Select(x => new { x.Id, x.Num });

        var rows = Context.From(inner)
            .Where(r => r.Id >= 1)
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Num })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().Equal(1, 3);
        rows[0].Num.Should().Be(10);
    }

    /// <summary>A CTE declared by name is read back as a raw source.</summary>
    [Fact]
    public void CteRawSource_ShouldReadRows()
    {
        var inner = Context.From<S4SrcRow>()
            .Where(x => x.Id <= 2)
            .Select(x => new { x.Id });

        var ids = Context.With("s4_src_cte", inner)
            .From("s4_src_cte")
            .OrderBy(t => t.GetInt32("id"))
            .Select(t => t.GetInt32("id"))
            .ToList();

        ids.Should().Equal(1, 2);
    }

    /// <summary>The table-valued <c>json_each</c> source projects a bare scalar column.</summary>
    [Fact]
    public void TableFunction_BareColumnProjection_ShouldReadValues()
    {
        var values = Context.From(() => JsonEach("[1,2,3]"))
            .Select(r => r.Value)
            .ToList();

        values.OrderBy(v => v).Should().Equal(1L, 2L, 3L);
    }

    /// <summary>The table-valued source filters and orders, then projects a transparent anonymous shape.</summary>
    [Fact]
    public void TableFunction_TransparentProjection_ShouldFilterAndSort()
    {
        var rows = Context.From(() => JsonEach("[3,1,2]"))
            .Where(r => r.Value > 1)
            .OrderByDescending(r => r.Value)
            .Select(r => new { r.Key, r.Value })
            .ToList();

        rows.Select(r => r.Value).Should().Equal(3L, 2L);
        rows.Select(r => r.Key).Should().Equal(0L, 2L);
    }

    /// <summary>The table-valued source joins the mapped entity on the shared numeric value.</summary>
    [Fact]
    public void TableFunction_JoinedToEntity_ShouldJoin()
    {
        var rows = Context.From(() => JsonEach("[1,2,3]"))
            .Join(Context.From<S4SrcRow>(), (j, s) => j.Value == s.Id)
            .OrderBy(p => p.Item1.Value)
            .Select(p => new
            {
                Value = p.Item1.Value,
                Label = p.Item2.Label
            })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Label).Should().Equal("a", "b", null);
    }

    /// <summary>
    /// A whole-row projection materializes the declared interface from the table-valued source.
    /// </summary>
    [Fact]
    public void TableFunction_WholeRowProjection_ShouldMaterializeTheInterface()
    {
        var rows = Context.From(() => JsonEach("[7]"))
            .Select(r => r)
            .ToList();

        var row = rows.Should().ContainSingle().Which;
        row.Key.Should().Be(0);
        row.Value.Should().Be(7);
    }

    private static TvfEntityBuilder<IS4JsonEachRow> JsonEach(string json) => new("json_each");

    /// <summary>Numeric row shape of <c>json_each</c> (the array element is its SQL value).</summary>
    public interface IS4JsonEachRow
    {
        [Column("key")]
        long Key { get; set; }

        [Column("value")]
        long Value { get; set; }
    }

    /// <summary>Marker table of the FROM-source facts.</summary>
    [SqlTable("s4_src_row")]
    public sealed class S4SrcRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("label")]
        public string? Label { get; set; }

        [Column("num")]
        public int? Num { get; set; }
    }
}
