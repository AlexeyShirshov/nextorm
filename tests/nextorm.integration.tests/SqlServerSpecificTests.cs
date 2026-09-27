using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests that assert Microsoft SQL Server specific behaviour, backed by a Testcontainers instance
/// unless NEXTORM_SQLSERVER_CONNECTION points at an existing server.
/// </summary>
[Collection("SqlServer")]
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

    [Fact]
    public void OutputInto_ShouldWriteModifiedRowsIntoTable()
    {
        var ctx = _sut.DataProvider;
        var id = MergeTestKey();
        var marker = "audit_" + Guid.NewGuid().ToString("N");

        Execute(ctx, "drop table if exists output_audit");
        Execute(ctx, "create table output_audit (id int, name nvarchar(100))");

        try
        {
            ctx.InsertInto<IMergeEntity>()
                .Values(new MergeEntity { Id = id, Name = marker, Age = 1 })
                .Returning(x => new { x.Id, x.Name })
                .OutputInto("output_audit")
                .Execute()
                .Should().Be(1);

            var audited = ctx.From("output_audit")
                .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
                .ToList();

            audited.Should().ContainSingle();
            audited[0].Id.Should().Be(id);
            audited[0].Name.Should().Be(marker);
        }
        finally
        {
            Execute(ctx, "drop table if exists output_audit");
        }
    }

    [Fact]
    public void OutputIntoThenOutput_ShouldWriteAndReturnRows()
    {
        var ctx = _sut.DataProvider;
        var id = MergeTestKey();
        var marker = "audit_" + Guid.NewGuid().ToString("N");

        Execute(ctx, "drop table if exists output_audit");
        Execute(ctx, "create table output_audit (id int, name nvarchar(100))");

        try
        {
            var returned = ctx.InsertInto<IMergeEntity>()
                .Values(new MergeEntity { Id = id, Name = marker, Age = 1 })
                .Returning(x => new { x.Id, x.Name })
                .OutputIntoThenOutput("output_audit")
                .ToList();

            returned.Should().ContainSingle();
            returned[0].Id.Should().Be(id);
            returned[0].Name.Should().Be(marker);

            ctx.From("output_audit")
                .Select(t => new { Id = t.GetInt32("id") })
                .ToList()
                .Should().ContainSingle()
                .Which.Id.Should().Be(id);
        }
        finally
        {
            Execute(ctx, "drop table if exists output_audit");
        }
    }

    [Fact]
    public void ExecuteRaw_OutputParameter_ShouldReturnServerValue()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "set @o = 42",
            [new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.Int32)]);

        var outputs = result.OutputParameters;

        outputs.Should().ContainSingle();
        outputs[0].Name.Should().Be("o");
        outputs[0].Direction.Should().Be(ParameterDirection.Output);
        outputs[0].Value.Should().Be(42);
    }

    [Fact]
    public void ExecuteRaw_InputOutputParameter_ShouldReturnUpdatedValue()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "set @io = @io + 1",
            [new ProcedureParameter("io", 41, Direction: ParameterDirection.InputOutput, DbType: DbType.Int32)]);

        var outputs = result.OutputParameters;

        outputs.Should().ContainSingle();
        outputs[0].Direction.Should().Be(ParameterDirection.InputOutput);
        outputs[0].Value.Should().Be(42);
    }

    [Fact]
    public void ExecuteProcedure_ReturnValue_ShouldCaptureProcedureReturn()
    {
        var ctx = _sut.DataProvider;
        var proc = "#raw_rv_" + Guid.NewGuid().ToString("N");

        Execute(ctx, $"create procedure {proc} as begin return 7; end;");

        try
        {
            // CommandType.StoredProcedure is what makes ADO.NET bind the ReturnValue parameter to the
            // procedure's own return status.
            using var result = ctx.ExecuteProcedure(
                proc,
                [new ProcedureParameter("rv", null, Direction: ParameterDirection.ReturnValue, DbType: DbType.Int32)]);

            result.ReturnValue.Should().Be(7);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc};");
        }
    }

    [Fact]
    public void ExecuteProcedure_MultipleResultSets_ShouldReadSequentially()
    {
        var ctx = _sut.DataProvider;
        var proc = "#raw_mrs_" + Guid.NewGuid().ToString("N");

        Execute(ctx, $"create procedure {proc} as begin select 1 as a; select 2 as b; end;");

        try
        {
            using var result = ctx.ExecuteProcedure(proc, []);

            result.Read<int>().Should().Equal(1);
            result.Read<int>().Should().Equal(2);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc};");
        }
    }

    [Fact]
    public void ExecuteProcedure_OutputAndInputOutputParameters_ShouldReturnValues()
    {
        var ctx = _sut.DataProvider;
        var proc = "#raw_out_" + Guid.NewGuid().ToString("N");

        Execute(ctx, $"create procedure {proc} @io int output, @o int output as begin set @o = @io + 1; set @io = @io * 2; end;");

        try
        {
            using var result = ctx.ExecuteProcedure(
                proc,
                [
                    new ProcedureParameter("io", 10, Direction: ParameterDirection.InputOutput, DbType: DbType.Int32),
                    new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.Int32),
                ]);

            var outputs = result.OutputParameters;
            outputs.Should().HaveCount(2);
            outputs.Single(x => x.Name == "io").Value.Should().Be(20);
            outputs.Single(x => x.Name == "o").Value.Should().Be(11);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc};");
        }
    }

    [Fact]
    public async Task ExecuteProcedureAsync_ShouldCaptureOutputAndReturnValue()
    {
        var ctx = _sut.DataProvider;
        var proc = "#raw_async_" + Guid.NewGuid().ToString("N");

        Execute(ctx, $"create procedure {proc} @o int output as begin set @o = 5; return 9; end;");

        try
        {
            await using var result = await ctx.ExecuteProcedureAsync(
                proc,
                [
                    new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.Int32),
                    new ProcedureParameter("rv", null, Direction: ParameterDirection.ReturnValue, DbType: DbType.Int32),
                ],
                TestContext.Current.CancellationToken);

            result.ReturnValue.Should().Be(9);
            result.OutputParameters.Single().Value.Should().Be(5);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc};");
        }
    }

    [Fact]
    public void ExecuteRaw_ProcedureReturnValue_ShouldBeCapturedThroughOutputParameter()
    {
        var ctx = _sut.DataProvider;
        var proc = "#raw_out_" + Guid.NewGuid().ToString("N");

        using (ctx.ExecuteRaw($"create procedure {proc} as begin return 7; end;"))
        {
        }

        try
        {
            // The T-SQL variable captures EXEC's return status, which is then copied into the output
            // parameter so ProcedureResult.OutputParameters can expose it.
            using var result = ctx.ExecuteRaw(
                $"declare @rv int; exec @rv = {proc}; set @o = @rv;",
                [new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.Int32)]);

            result.OutputParameters.Should().ContainSingle();
            result.OutputParameters[0].Value.Should().Be(7);
        }
        finally
        {
            using (ctx.ExecuteRaw($"drop procedure if exists {proc};"))
            {
            }
        }
    }

    [Fact]
    public void ExecuteRaw_TypeName_ShouldExecuteStructuredParameter()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop type if exists dbo.RawTvpType");
        Execute(ctx, "create type dbo.RawTvpType as table (id int not null)");

        try
        {
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Rows.Add(1);
            table.Rows.Add(2);
            table.Rows.Add(3);

            using var result = ctx.ExecuteRaw(
                "select count(*) as c from @p",
                [new ProcedureParameter("p", table, TypeName: "dbo.RawTvpType")]);

            result.Read<int>().Should().Equal(3);
        }
        finally
        {
            Execute(ctx, "drop type if exists dbo.RawTvpType");
        }
    }

    [Fact]
    public void ExecuteRaw_StructuredOutputDirection_ThrowsArgumentException()
    {
        var ctx = _sut.DataProvider;

        // A table-valued parameter is input-only: an output/return direction is rejected before the
        // command is built.
        var act = () => ctx.ExecuteRaw(
            "select 1",
            [new ProcedureParameter("p", null, Direction: ParameterDirection.Output, TypeName: "dbo.RawTvpType")]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ExecuteRaw_OutputParameterNull_ShouldNormalizeToNull()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "set @o = null",
            [new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.Int32)]);

        result.OutputParameters.Should().ContainSingle();
        result.OutputParameters[0].Value.Should().BeNull();
    }

    [Fact]
    public void ExecuteRaw_OutputStringWithSize_ShouldReturnValue()
    {
        var ctx = _sut.DataProvider;

        using var result = ctx.ExecuteRaw(
            "set @o = 'hello'",
            [new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.String, Size: 32)]);

        result.OutputParameters.Should().ContainSingle();
        result.OutputParameters[0].Value.Should().Be("hello");
    }

    [Fact]
    public void ExecuteRaw_OutputParametersWithUnreadResultSet_ShouldDiscardIt()
    {
        var ctx = _sut.DataProvider;

        // The batch returns a result set and also sets an output parameter. Accessing OutputParameters
        // closes the reader, discarding the unread set, so a later Read must throw.
        using var result = ctx.ExecuteRaw(
            "select 1 as a; set @o = 5",
            [new ProcedureParameter("o", null, Direction: ParameterDirection.Output, DbType: DbType.Int32)]);

        result.OutputParameters[0].Value.Should().Be(5);

        var act = () => result.Read<int>();
        act.Should().Throw<InvalidOperationException>();
    }

    private sealed class TvpEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [SqlTable("tvp_decimal_row")]
    private sealed class DecimalTvpRow
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("Amount")]
        [DecimalPrecision(12, 4)]
        public decimal Amount { get; set; }

        [Column("Optional")]
        [DecimalPrecision(12, 4)]
        public decimal? Optional { get; set; }
    }

    private sealed class DecimalProjection
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public decimal? Optional { get; set; }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_Scalar_ShouldStreamRows()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpScalar_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            using (var result = ctx.ExecuteRaw(
                "select count(*) as c from @p",
                [ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3 })]))
            {
                result.Read<int>().Should().Equal(3);
            }

            using (var result = ctx.ExecuteRaw(
                "select sum(value) as s from @p",
                [ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3 })]))
            {
                result.Read<int>().Should().Equal(6);
            }
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_Entity_ShouldStreamMappedColumns()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpEntity_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Name nvarchar(max) null)");

        try
        {
            var rows = new[]
            {
                new TvpEntity { Id = 1, Name = "alpha" },
                new TvpEntity { Id = 2, Name = null },
            };

            using var result = ctx.ExecuteRaw(
                "select Id, Name from @p order by Id",
                [ProcedureParameter.Table("p", typeName, rows)]);

            var read = result.Read<TvpEntity>();

            read.Should().HaveCount(2);
            read[0].Id.Should().Be(1);
            read[0].Name.Should().Be("alpha");
            read[1].Id.Should().Be(2);
            read[1].Name.Should().BeNull();
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteProcedure_WithTableParameter_ShouldPassRows()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpProc_" + Guid.NewGuid().ToString("N");
        var proc = "tvp_proc_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");
        Execute(ctx, $"create procedure {proc} @p {typeName} readonly as begin select count(*) as c from @p; end");

        try
        {
            using var result = ctx.ExecuteProcedure(proc, [ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3, 4 })]);

            result.Read<int>().Should().Equal(4);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc}");
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EmptySet_ShouldReturnZero()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpEmpty_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            using var result = ctx.ExecuteRaw(
                "select count(*) as c from @p",
                [ProcedureParameter.Table("p", typeName, Array.Empty<int>())]);

            result.Read<int>().Should().Equal(0);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameterWithoutTypeName_ShouldThrowArgumentException()
    {
        var ctx = _sut.DataProvider;

        var act = () => ctx.ExecuteRaw(
            "select count(*) as c from @p",
            [ProcedureParameter.Table("p", new[] { 1 })]);

        act.Should().Throw<ArgumentException>();
    }

    private enum TvpLevel : short
    {
        One = 1,
        Two = 2,
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EnumScalar_ShouldStreamUnderlyingNumber()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpEnum_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value smallint not null)");

        try
        {
            using var result = ctx.ExecuteRaw(
                "select sum(value) as s from @p",
                [ProcedureParameter.Table("p", typeName, new[] { TvpLevel.One, TvpLevel.Two })]);

            result.Read<int>().Should().Equal(3);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_ReEnumerated_ShouldExecuteTwice()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpRepeat_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            // The same descriptor is executed twice: the streamed records must be re-enumerable, not a
            // captured, already-drained enumerator.
            var parameter = ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3 });

            using (var first = ctx.ExecuteRaw("select sum(value) as s from @p", [parameter]))
                first.Read<int>().Should().Equal(6);

            using (var second = ctx.ExecuteRaw("select sum(value) as s from @p", [parameter]))
                second.Read<int>().Should().Equal(6);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_LazyNonEmptySet_ShouldStreamPeekedRow()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpLazyRows_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            // A lazy, non-empty source with an unknown count: the provider peeks the first row and hands
            // the started enumerator to the command, which must still see every row (3, 4, 5).
            var rows = Enumerable.Range(1, 5).Where(x => x > 2);

            using (var count = ctx.ExecuteRaw("select count(*) as c from @p", [ProcedureParameter.Table("p", typeName, rows)]))
                count.Read<int>().Should().Equal(3);

            using (var sum = ctx.ExecuteRaw("select sum(value) as s from @p", [ProcedureParameter.Table("p", typeName, rows)]))
                sum.Read<int>().Should().Equal(12);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_LazyEmptySet_ShouldReturnZero()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpLazyEmpty_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");

        try
        {
            // A lazy sequence with an unknown count and no elements: SqlClient rejects an empty record
            // sequence, so the provider peeks and binds an unset (null) parameter.
            var rows = Enumerable.Range(1, 5).Where(x => x > 100);

            using var result = ctx.ExecuteRaw(
                "select count(*) as c from @p",
                [ProcedureParameter.Table("p", typeName, rows)]);

            result.Read<int>().Should().Equal(0);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_EntityNullColumn_ShouldCountNulls()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpNull_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Name nvarchar(max) null)");

        try
        {
            var rows = new[]
            {
                new TvpEntity { Id = 1, Name = "alpha" },
                new TvpEntity { Id = 2, Name = null },
                new TvpEntity { Id = 3, Name = null },
            };

            using var result = ctx.ExecuteRaw(
                "select count(*) as c from @p where Name is null",
                [ProcedureParameter.Table("p", typeName, rows)]);

            result.Read<int>().Should().Equal(2);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_DecimalPrecision_ShouldRoundtripAgainstRealUddt()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpDecimal_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Amount decimal(12,4) not null, Optional decimal(12,4) null)");

        try
        {
            var rows = new[]
            {
                new DecimalTvpRow { Id = 1, Amount = 12.3456m, Optional = null },
                new DecimalTvpRow { Id = 2, Amount = -0.0001m, Optional = 9876.5432m },
            };

            using var result = ctx.ExecuteRaw(
                "select Id, Amount, Optional from @p order by Id",
                [ProcedureParameter.Table("p", typeName, rows)]);

            var read = result.Read<DecimalProjection>();

            read.Should().HaveCount(2);
            read[0].Amount.Should().Be(12.3456m);
            read[0].Optional.Should().BeNull();
            read[1].Amount.Should().Be(-0.0001m);
            read[1].Optional.Should().Be(9876.5432m);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_DecimalPrecisionOverflow_ShouldNotSilentlyCorrupt()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpDecimalOverflow_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Amount decimal(12,4) not null, Optional decimal(12,4) null)");

        try
        {
            // 123456789.1234 has 9 integer digits, more than decimal(12, 4) can hold (8); the declared
            // metadata must surface the overflow as a failure rather than binding a wrong value.
            var rows = new[] { new DecimalTvpRow { Id = 1, Amount = 123456789.1234m, Optional = null } };

            Action act = () =>
            {
                using var result = ctx.ExecuteRaw(
                    "select Id, Amount, Optional from @p",
                    [ProcedureParameter.Table("p", typeName, rows)]);
            };

            // The declared decimal(12, 4) metadata makes SqlClient reject the over-precise record with a
            // typed truncation failure ("Numeric arithmetic causes truncation."), not a generic server
            // error: asserting the exact type and message keeps an unrelated SqlException from being
            // accepted as evidence.
            act.Should().Throw<SqlTruncateException>().WithMessage("*truncation*");
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_DecimalScaleExcess_ShouldRoundToDeclaredScale()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpDecimalScaleExcess_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Amount decimal(12,4) not null, Optional decimal(12,4) null)");

        try
        {
            // 12.345678 carries two digits more than the declared scale 4; the client rounds it to the
            // declared scale (12.3457) rather than truncating or failing on the server.
            var rows = new[] { new DecimalTvpRow { Id = 1, Amount = 12.345678m, Optional = null } };

            using var result = ctx.ExecuteRaw(
                "select Id, Amount, Optional from @p",
                [ProcedureParameter.Table("p", typeName, rows)]);

            var read = result.Read<DecimalProjection>();

            read.Should().ContainSingle();
            read[0].Amount.Should().Be(12.3457m);
        }
        finally
        {
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public void ExecuteRaw_TableParameter_InsertSelect_ShouldInsertRows()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpInsert_" + Guid.NewGuid().ToString("N");
        var table = "tvp_insert_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Name nvarchar(max) null)");
        Execute(ctx, $"create table {table} (Id int not null, Name nvarchar(max) null)");

        try
        {
            var rows = new[]
            {
                new TvpEntity { Id = 1, Name = "alpha" },
                new TvpEntity { Id = 2, Name = null },
                new TvpEntity { Id = 3, Name = "gamma" },
            };

            // `INSERT ... SELECT ... FROM @tvp` is the primary TVP use case: one round trip writes the
            // whole set without an N-row command.
            using (ctx.ExecuteRaw(
                $"insert into {table} (Id, Name) select Id, Name from @p",
                [ProcedureParameter.Table("p", typeName, rows)]))
            {
            }

            using (var result = ctx.ExecuteRaw($"select count(*) as c from {table}"))
                result.Read<int>().Should().Equal(3);

            using (var result = ctx.ExecuteRaw($"select count(*) as c from {table} where Name is null"))
                result.Read<int>().Should().Equal(1);

            using (var result = ctx.ExecuteRaw(
                $"select count(*) as c from {table} where Name = @n",
                [new ProcedureParameter("n", "alpha")]))
                result.Read<int>().Should().Equal(1);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public async Task ExecuteRawAsync_TableParameter_InsertSelect_ShouldInsertRows()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpInsertAsync_" + Guid.NewGuid().ToString("N");
        var table = "tvp_insert_async_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (Id int not null, Name nvarchar(max) null)");
        Execute(ctx, $"create table {table} (Id int not null, Name nvarchar(max) null)");

        try
        {
            var rows = new[]
            {
                new TvpEntity { Id = 10, Name = "a" },
                new TvpEntity { Id = 11, Name = "b" },
            };

            await using (await ctx.ExecuteRawAsync(
                $"insert into {table} (Id, Name) select Id, Name from @p",
                [ProcedureParameter.Table("p", typeName, rows)],
                TestContext.Current.CancellationToken))
            {
            }

            using (var result = ctx.ExecuteRaw($"select count(*) as c from {table}"))
                result.Read<int>().Should().Equal(2);
        }
        finally
        {
            Execute(ctx, $"drop table if exists {table}");
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [Fact]
    public async Task ExecuteProcedureAsync_WithTableParameter_ShouldPassRows()
    {
        var ctx = _sut.DataProvider;
        var typeName = "dbo.TvpProcAsync_" + Guid.NewGuid().ToString("N");
        var proc = "tvp_proc_async_" + Guid.NewGuid().ToString("N");
        Execute(ctx, $"create type {typeName} as table (value int not null)");
        Execute(ctx, $"create procedure {proc} @p {typeName} readonly as begin select count(*) as c from @p; end");

        try
        {
            await using var result = await ctx.ExecuteProcedureAsync(
                proc,
                [ProcedureParameter.Table("p", typeName, new[] { 1, 2, 3, 4, 5 })],
                TestContext.Current.CancellationToken);

            var read = new List<int>();
            await foreach (var value in result.ReadAsync<int>(TestContext.Current.CancellationToken))
                read.Add(value);

            read.Should().Equal(5);
        }
        finally
        {
            Execute(ctx, $"drop procedure if exists {proc}");
            Execute(ctx, $"drop type if exists {typeName}");
        }
    }

    [SqlTable("uint_bigint_probe")]
    internal interface IUintBigintProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("value")]
        uint Value { get; set; }
    }

    // Probes whether a CLR uint property materializes from a SQL Server bigint column. uint is not in
    // the provider's IsNumeric widening list, so it falls through to the core GetFieldValue<uint> branch.
    [Fact]
    public void Uint_FromBigintColumn_ShouldMaterialize()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists uint_bigint_probe");
        Execute(ctx, "create table uint_bigint_probe (id bigint not null primary key, value bigint not null)");

        try
        {
            Execute(ctx, "insert into uint_bigint_probe (id, value) values (1, 42)");

            ctx.From<IUintBigintProbe>().Where(x => x.Id == 1).Select(x => x.Value).First()
                .Should().Be(42u);
        }
        finally
        {
            Execute(ctx, "drop table if exists uint_bigint_probe");
        }
    }

    [SqlTable("ulong_bigint_probe")]
    internal interface IUlongBigintProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("value")]
        ulong Value { get; set; }
    }

    // Companion probe: ulong is likewise absent from IsNumeric, so the comparison shows whether uint is
    // uniquely broken or shares the same GetFieldValue<T> path as the already-tolerated ulong type.
    [Fact]
    public void Ulong_FromBigintColumn_ShouldMaterialize()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists ulong_bigint_probe");
        Execute(ctx, "create table ulong_bigint_probe (id bigint not null primary key, value bigint not null)");

        try
        {
            Execute(ctx, "insert into ulong_bigint_probe (id, value) values (1, 42)");

            ctx.From<IUlongBigintProbe>().Where(x => x.Id == 1).Select(x => x.Value).First()
                .Should().Be(42ul);
        }
        finally
        {
            Execute(ctx, "drop table if exists ulong_bigint_probe");
        }
    }

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
