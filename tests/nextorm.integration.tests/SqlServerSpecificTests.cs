using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert Microsoft SQL Server specific behaviour, backed by a Testcontainers instance
/// unless NEXTORM_SQLSERVER_CONNECTION points at an existing server.
/// </summary>
public sealed class SqlServerSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void IifChoose_ShouldEvaluateCondition()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                I = SqlFunctions.SqlServer.iif(x.Id > 5, "big", "small"),
                C = SqlFunctions.SqlServer.choose(x.Id, "a", "b", "c")
            })
            .First();

        r.I.Should().Be("small");
        r.C.Should().Be("a");
    }

    [Fact]
    public void Choose_OutOfRange_ShouldReturnNull()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.SqlServer.choose(99, "a", "b", "c"))
            .FirstOrDefault()
            .Should().BeNull();
    }

    [Fact]
    public void InfoFunctions_ShouldReturnServerValues()
    {
        var r = _sut.SimpleEntity
            .Select(x => new
            {
                User = SqlFunctions.Sql.current_user(),
                Session = SqlFunctions.Sql.session_user(),
                Schema = SqlFunctions.Sql.current_schema(),
                Db = SqlFunctions.Sql.current_database(),
                Ver = SqlFunctions.Sql.version(),
                U = SqlFunctions.Sql.gen_random_uuid()
            })
            .First();

        r.User.Should().NotBeNullOrEmpty();
        r.Session.Should().NotBeNullOrEmpty();
        r.Schema.Should().NotBeNullOrEmpty();
        r.Db.Should().NotBeNullOrEmpty();
        r.Ver.Should().NotBeNullOrEmpty();
        r.U.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void PercentileWindow_ShouldReturnValue()
    {
        var value = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new { V = SqlFunctions.Sql.percentile_cont(0.5, x.Id).Over() })
            .First();

        value.V.Should().Be(1);
    }

    [Fact]
    public void Stdev_ShouldMatchSampleStandardDeviation()
    {
        _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.stdev((double)x.Id))
            .First()
            .Should().BeApproximately(3.0276503540974917, 1e-12);
    }

    [Fact]
    public void OrderByDescending_NullsLast_ShouldSortData()
    {
        // SQL Server treats NULL as the smallest value, so DESC puts the NULL group last
        // (the opposite of PostgreSQL). The shared suite avoids depending on this.
        var r = _sut.ComplexEntity.OrderByDescending(it => it.Int).Select(it => new { it.Id }).ToList();

        r[^1].Id.Should().Be(1);
    }

    [Fact]
    public void Avg_OnIntColumn_ShouldTruncate()
    {
        // T-SQL evaluates AVG over an integer column as an integer, unlike PostgreSQL and SQLite
        // which return the fractional value. The shared suite skips its rounding assertion for
        // this provider; here the actual behaviour is pinned explicitly.
        _sut.SimpleEntity.Select(x => SqlFunctions.Sql.avg(x.Id)).First().Should().Be(5);
    }

    [Fact]
    public void StringSplit_TableFunction_ShouldReturnFragments()
    {
        var csv = "a,b,c";
        var separator = ",";

        var values = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.SqlServer.string_split(csv, separator))
            .Select(r => new { r.Value })
            .ToList()
            .Select(r => r.Value)
            .OrderBy(v => v)
            .ToArray();

        values.Should().Equal("a", "b", "c");
    }

    [Fact]
    public void OpenJson_TableFunction_ShouldReturnEntries()
    {
        var json = """{"a":1,"b":2}""";

        var keys = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.SqlServer.openjson(json))
            .Select(r => new { r.Key })
            .ToList()
            .Select(r => r.Key)
            .OrderBy(k => k)
            .ToArray();

        keys.Should().Equal("a", "b");
    }

    public interface IOpenJsonTypedRow
    {
        [Column("name")]
        string? Name { get; set; }
        [Column("age")]
        int Age { get; set; }
    }

    private static class OpenJsonTvf
    {
        [SqlTableFunction("openjson", WithClause = "name nvarchar(50) '$.name', age int '$.age'")]
        public static IQueryable<IOpenJsonTypedRow> Typed(string json) => throw new NotSupportedException();
    }

    [Fact]
    public void OpenJson_WithTypedSchema_ShouldReturnTypedColumns()
    {
        var json = """{"name":"Ada","age":36}""";

        var row = _sut.DataProvider
            .FromTableFunction(() => OpenJsonTvf.Typed(json))
            .Select(r => new { r.Name, r.Age })
            .First();

        row.Name.Should().Be("Ada");
        row.Age.Should().Be(36);
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void Page_WithoutOrderBy_ShouldUseInjectedEmptySort()
    {
        // SQL Server rejects OFFSET/FETCH without ORDER BY, so the provider injects one; the query
        // must not fail and must return just the requested page. The injected sort is a constant,
        // so only the page size is deterministic.
        var r = _sut.SimpleEntity.Page(2, 3).Select(it => it.Id).ToList();

        r.Should().HaveCount(2);
        r.Should().OnlyHaveUniqueItems();
        r.Should().OnlyContain(id => id >= 1 && id <= 10);
    }

    [Fact]
    public void LastIndexOf_ShouldReturnZeroBasedLastPosition()
    {
        // "dadfasd" has its last 'd' at index 6.
        _sut.ComplexEntity
            .Where(e => e.Id == 1)
            .Select(e => e.String!.LastIndexOf("d"))
            .First()
            .Should().Be(6);
    }

    [SqlTable("xml_entity")]
    public interface IXmlEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("payload")]
        string? Payload { get; set; }
    }

    [Fact]
    public void XmlMethods_ShouldReturnValues()
    {
        var e = _sut.DataProvider.From<IXmlEntity>();

        var r = e.Where(x => x.Id == 1)
            .Select(x => new
            {
                Value = SqlFunctions.SqlServer.xml_value<string>(x.Payload, "(/root/item)[1]", "nvarchar(100)"),
                Query = SqlFunctions.SqlServer.xml_query(x.Payload, "/root/item[1]"),
                Exists = SqlFunctions.SqlServer.xml_exist(x.Payload, "/root/item[2]")
            })
            .First();

        r.Value.Should().Be("alpha");
        r.Query.Should().Contain("alpha");
        r.Exists.Should().BeTrue();
    }

    [Fact]
    public void XmlNodes_ShouldUnfoldNodesAndProjectValues()
    {
        var rows = _sut.DataProvider.From<IXmlEntity>()
            .CrossApply(x => SqlFunctions.SqlServer.xml_nodes(x.Payload, "/root/item"))
            .Select(p => new
            {
                Id = SqlFunctions.SqlServer.xml_value<int>(p.Item2.Value, "(.)[1]/@id", "int"),
                Value = SqlFunctions.SqlServer.xml_value<string>(p.Item2.Value, "(.)[1]", "nvarchar(100)")
            })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Should().Contain(r => r.Id == 1 && r.Value == "alpha");
        rows.Should().Contain(r => r.Id == 2 && r.Value == "beta");
    }

    [SqlTable("pivot_entity")]
    private interface IPivotEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("q1")]
        int? Q1 { get; set; }
        [Column("q2")]
        int? Q2 { get; set; }
    }

    [Fact]
    public void Pivot_ShouldReshapeRows()
    {
        var row = _sut.SimpleEntity
            .Pivot(PivotAggregate.Count, x => x.Id, x => x.Id, PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new { One = t.GetNullableInt32("[1]"), Two = t.GetNullableInt32("[2]") })
            .First();

        row.One.Should().Be(1);
        row.Two.Should().Be(1);
    }

    [Fact]
    public void Unpivot_ShouldStackColumns()
    {
        var rows = _sut.DataProvider.From<IPivotEntity>()
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new { Id = t.GetInt32("id"), Qtr = t.GetString("qtr"), Val = t.GetNullableInt32("val") })
            .ToList();

        rows.Should().HaveCount(4);
        rows.Should().Contain(r => r.Id == 1 && r.Qtr == "q1" && r.Val == 10);
        rows.Should().Contain(r => r.Id == 2 && r.Qtr == "q2" && r.Val == 40);
    }

    [Fact]
    public void Pivot_ShouldReshapeDerivedSource()
    {
        var derived = _sut.SimpleEntity
            .Where(x => x.Id <= 2)
            .Select(x => new { x.Id });

        var row = _sut.DataProvider.From(derived)
            .Pivot(PivotAggregate.Count, x => x.Id, x => x.Id, PivotValue.Create("1"), PivotValue.Create("2"))
            .Select(t => new { One = t.GetNullableInt32("[1]"), Two = t.GetNullableInt32("[2]") })
            .First();

        row.One.Should().Be(1);
        row.Two.Should().Be(1);
    }

    [Fact]
    public void Unpivot_ShouldStackDerivedColumns()
    {
        var derived = _sut.DataProvider.From<IPivotEntity>()
            .Where(x => x.Id > 0)
            .Select(x => new { x.Id, x.Q1, x.Q2 });

        var rows = _sut.DataProvider.From(derived)
            .Unpivot("val", "qtr", UnpivotColumn.Create("q1"), UnpivotColumn.Create("q2"))
            .Select(t => new { Id = t.GetInt32("id"), Qtr = t.GetString("qtr"), Val = t.GetNullableInt32("val") })
            .ToList();

        rows.Should().HaveCount(4);
        rows.Should().Contain(r => r.Id == 1 && r.Qtr == "q1" && r.Val == 10);
        rows.Should().Contain(r => r.Id == 2 && r.Qtr == "q2" && r.Val == 40);
    }

    private static int MergeTestKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    [Fact]
    public void FullMerge_MatchedDelete_ShouldDeleteMatchedRow()
    {
        var ctx = _sut.DataProvider;
        var deleteId = MergeTestKey();

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = deleteId, Name = "old", Age = 1 })
            .Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = deleteId, Name = "ignored", Age = 0 })
            .OnKeys()
            .WhenMatched().ThenDelete()
            .WhenNotMatched().ThenInsert()
            .Merge();

        ctx.From<IMergeEntity>().Where(x => x.Id == deleteId).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void FullMerge_NotMatchedBySource_ShouldDeleteOrphan()
    {
        var ctx = _sut.DataProvider;
        var orphanId = MergeTestKey();
        var matchedId = orphanId + 1;

        ctx.InsertInto<IMergeEntity>().Values(new MergeEntity { Id = orphanId, Name = "orphan", Age = 1 }).Insert();
        ctx.InsertInto<IMergeEntity>().Values(new MergeEntity { Id = matchedId, Name = "keep", Age = 1 }).Insert();

        ctx.MergeInto<IMergeEntity>()
            .Using(new MergeEntity { Id = matchedId, Name = "updated", Age = 9 })
            .OnKeys()
            .WhenMatched().ThenUpdate()
            .WhenNotMatchedBySource().ThenDelete()
            .Merge();

        ctx.From<IMergeEntity>().Where(x => x.Id == orphanId).Select(x => x.Id).ToList().Should().BeEmpty();
        ctx.From<IMergeEntity>().Where(x => x.Id == matchedId).Select(x => x.Age).ToList().Should().ContainSingle().Which.Should().Be(9);
    }

    [Fact]
    public void BulkInsert_InsideTransaction_ShouldRollBack()
    {
        var ctx = _sut.DataProvider;
        var marker = "bulktx_" + Guid.NewGuid().ToString("N");

        using (var transaction = ((ITransactionManager)ctx).BeginTransaction())
        {
            ctx.BulkInsertInto<IInsertEntity>().Values([new InsertEntity { Name = marker, Age = 1 }]).BulkInsert();
            transaction.Rollback();
        }

        ctx.From<IInsertEntity>().Where(x => x.Name == marker).Select(x => x.Age).ToList().Should().BeEmpty();
    }

    [SqlTable("collation_probe")]
    internal interface ICollationProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        [Collation("Latin1_General_100_BIN2")]
        string? Name { get; set; }
    }

    [Fact]
    public void ColumnCollation_ShouldApplyDeclaredBinaryCollation()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists collation_probe");
        Execute(ctx, "create table collation_probe (id bigint, name varchar(50))");

        try
        {
            Execute(ctx, "insert into collation_probe (id, name) values (1, 'abc')");
            Execute(ctx, "insert into collation_probe (id, name) values (2, 'ABC')");

            ctx.From<ICollationProbe>().Where(x => x.Name == "abc").Select(x => x.Id).ToList()
                .Should().Equal(1L);
        }
        finally
        {
            Execute(ctx, "drop table if exists collation_probe");
        }
    }

    [Fact]
    public void Batch_CreateTableAs_WithTempTableName_ShouldReadTheTableInTheSameBatch()
    {
        var name = "#batch_" + Guid.NewGuid().ToString("N");
        var ctx = _sut.DataProvider;

        var rows = ctx.Batch()
            .CreateTable(name, _sut.SimpleEntity.Where(x => x.Id == 1).Select(x => new { x.Id }))
            .Query(ctx.From(name).Select(t => new { Id = t.GetInt32("id") }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(1);
    }

    [Fact]
    public void SqlServerStringFunctions_ShouldCompute()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Pat = SqlFunctions.SqlServer.patindex("%b%", "abc"),
                Quo = SqlFunctions.SqlServer.quotename("abc"),
                Snd = SqlFunctions.SqlServer.soundex("Robert"),
                Dif = SqlFunctions.SqlServer.difference("Robert", "Rupert"),
                Esc = SqlFunctions.SqlServer.string_escape("a\nb", "json"),
                Uni = SqlFunctions.SqlServer.unicode("A"),
                Nch = SqlFunctions.SqlServer.nchar(65),
                Fmt = SqlFunctions.SqlServer.format(42, "D6")
            })
            .First();

        r.Pat.Should().Be(2);
        r.Quo.Should().Be("[abc]");
        r.Snd.Should().Be("R163");
        r.Dif.Should().Be(4);
        r.Esc.Should().Be("a\\nb");
        r.Uni.Should().Be(65);
        r.Nch.Should().Be("A");
        r.Fmt.Should().Be("000042");
    }

    [Fact]
    public void SqlServerTrigFunctions_ShouldCompute()
    {
        // The operand is a float column cast so T-SQL does not evaluate an integer literal (DEGREES(1)
        // truncates to 57); the seed makes Id == 1, so the angle is 0.5.
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Aco = SqlFunctions.SqlServer.acos((double)x.Id / 2),
                Asi = SqlFunctions.SqlServer.asin((double)x.Id / 2),
                Ata = SqlFunctions.SqlServer.atan((double)x.Id / 2),
                Atn = SqlFunctions.SqlServer.atn2((double)x.Id, 2.0),
                Cot = SqlFunctions.SqlServer.cot((double)x.Id / 2),
                Deg = SqlFunctions.SqlServer.degrees((double)x.Id),
                Rad = SqlFunctions.SqlServer.radians((double)x.Id * 180),
                Pi = SqlFunctions.SqlServer.pi(),
                Squ = SqlFunctions.SqlServer.square((double)x.Id * 4)
            })
            .First();

        r.Aco.Should().BeApproximately(Math.Acos(0.5), 1e-12);
        r.Asi.Should().BeApproximately(Math.Asin(0.5), 1e-12);
        r.Ata.Should().BeApproximately(Math.Atan(0.5), 1e-12);
        r.Atn.Should().BeApproximately(Math.Atan2(1.0, 2.0), 1e-12);
        r.Cot.Should().BeApproximately(1.0 / Math.Tan(0.5), 1e-12);
        r.Deg.Should().BeApproximately(180.0 / Math.PI, 1e-12);
        r.Rad.Should().BeApproximately(Math.PI, 1e-12);
        r.Pi.Should().BeApproximately(Math.PI, 1e-12);
        r.Squ.Should().Be(16.0);
    }

    [Fact]
    public void SqlServerDateFunctions_ShouldCompute()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Name = SqlFunctions.SqlServer.datename("year", x.Datetime),
                Bucket = SqlFunctions.SqlServer.date_bucket("day", 1, x.Datetime)
            })
            .First();

        r.Name.Should().Be("2023");
        r.Bucket.Should().Be(new DateTime(2023, 1, 1));
    }

    [Fact]
    public void SqlServerHashBytes_ShouldReturnDigest()
    {
        var data = Encoding.UTF8.GetBytes("abc");

        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.SqlServer.hashbytes("SHA2_256", data))
            .First();

        r.Should().Equal(SHA256.HashData(data));
    }

    [Fact]
    public void SqlServerJsonConstructors_ShouldBuildJson()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Arr = SqlFunctions.SqlServer.json_array("a", 1, "b"),
                Obj = SqlFunctions.SqlServer.json_object("k", 1),
                Exists = SqlFunctions.SqlServer.json_path_exists("{\"k\":1}", "$.k")
            })
            .First();

        r.Arr.Should().Be("[\"a\",1,\"b\"]");
        r.Obj.Should().Be("{\"k\":1}");
        r.Exists.Should().BeTrue();
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
