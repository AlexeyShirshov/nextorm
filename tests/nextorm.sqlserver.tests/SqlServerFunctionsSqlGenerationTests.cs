using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>SQL generation for the SQL Server-only T-SQL scalar functions (no database).</summary>
public class SqlServerFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void StringFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            P = SqlFunctions.SqlServer.patindex("%a%", x.String),
            Q1 = SqlFunctions.SqlServer.quotename(x.String),
            Q2 = SqlFunctions.SqlServer.quotename(x.String, "["),
            S = SqlFunctions.SqlServer.soundex(x.String),
            D = SqlFunctions.SqlServer.difference(x.String, "abc"),
            E = SqlFunctions.SqlServer.string_escape(x.String, "json"),
            U = SqlFunctions.SqlServer.unicode(x.String),
            N = SqlFunctions.SqlServer.nchar(65),
            F = SqlFunctions.SqlServer.format(x.Id, "N"),
            F2 = SqlFunctions.SqlServer.format(x.Id, "N", "en-US")
        }));

        sql.Should().Contain("patindex('%a%', somestring)");
        sql.Should().Contain("quotename(somestring)");
        sql.Should().Contain("quotename(somestring, '[')");
        sql.Should().Contain("soundex(somestring)");
        sql.Should().Contain("difference(somestring, 'abc')");
        sql.Should().Contain("string_escape(somestring, 'json')");
        sql.Should().Contain("unicode(somestring)");
        sql.Should().Contain("nchar(65)");
        sql.Should().Contain("format(id, 'N')");
        sql.Should().Contain("format(id, 'N', 'en-US')");
    }

    [Fact]
    public void TrigFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            A = SqlFunctions.SqlServer.acos(0.5),
            As = SqlFunctions.SqlServer.asin(0.5),
            At = SqlFunctions.SqlServer.atan(0.5),
            A2 = SqlFunctions.SqlServer.atn2(1.0, 2.0),
            C = SqlFunctions.SqlServer.cot(0.5),
            D = SqlFunctions.SqlServer.degrees(1.0),
            R = SqlFunctions.SqlServer.radians(180.0),
            P = SqlFunctions.SqlServer.pi(),
            Sq = SqlFunctions.SqlServer.square((double)x.Int!)
        }));

        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("asin(0.5)");
        sql.Should().Contain("atan(0.5)");
        sql.Should().Contain("atn2(1, 2)");
        sql.Should().Contain("cot(0.5)");
        sql.Should().Contain("degrees(1)");
        sql.Should().Contain("radians(180)");
        sql.Should().Contain("pi()");
        sql.Should().Contain("square(cast(nullableint as float))");
    }

    [Fact]
    public void DateFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var origin = new DateTime(2020, 1, 1);

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            Name = SqlFunctions.SqlServer.datename("month", x.Datetime),
            Bucket = SqlFunctions.SqlServer.date_bucket("day", 1, x.Datetime),
            BucketOrigin = SqlFunctions.SqlServer.date_bucket("week", 2, x.Datetime, origin)
        }));

        sql.Should().Contain("datename(month, dt)");
        sql.Should().Contain("date_bucket(day, 1, dt)");
        sql.Should().Contain("date_bucket(week, 2, dt,");
    }

    [Fact]
    public void BinaryAndSystemFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var data = new byte[] { 1, 2, 3 };

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            H = SqlFunctions.SqlServer.hashbytes("SHA2_256", data),
            N = SqlFunctions.SqlServer.newsequentialid()
        }));

        sql.Should().Contain("hashbytes('SHA2_256'");
        sql.Should().Contain("newsequentialid()");
    }

    [Fact]
    public void JsonConstructors_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            A = SqlFunctions.SqlServer.json_array("a", x.Id, "b"),
            Empty = SqlFunctions.SqlServer.json_array(),
            O = SqlFunctions.SqlServer.json_object("k", x.Id, "k2", "v2")
        }));

        sql.Should().Contain("json_array('a', id, 'b')");
        sql.Should().Contain("json_array()");
        sql.Should().Contain("json_object('k' : id, 'k2' : 'v2')");
    }

    [Fact]
    public void JsonAggregates_ShouldEmitNativeSpellings()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.SqlServer.json_arrayagg(x.String),
            O = SqlFunctions.SqlServer.json_objectagg(x.String, x.Id)
        }));

        sql.Should().Contain("json_arrayagg(somestring)");
        sql.Should().Contain("json_objectagg(somestring : id)");
    }

    [Fact]
    public void JsonPredicates_ShouldMaterialiseBooleanValue()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Where(x => x.Id == 1).Select(x => new
        {
            C = SqlFunctions.SqlServer.json_contains(x.String, "a", "$.x"),
            P = SqlFunctions.SqlServer.json_path_exists(x.String, "$.x")
        }));

        sql.Should().Contain("cast(case when json_contains(somestring, 'a', '$.x') = 1 then 1 else 0 end as bit)");
        sql.Should().Contain("cast(case when json_path_exists(somestring, '$.x') = 1 then 1 else 0 end as bit)");
    }

    [Fact]
    public void JsonPredicate_InWhere_ShouldBeAPredicate()
    {
        using var ctx = SqlServerTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Where(x => SqlFunctions.SqlServer.json_path_exists(x.String, "$.x")).Select(x => new { x.Id }))
            .Should().Contain("where json_path_exists(somestring, '$.x') = 1");
    }

    [Fact]
    public void Functions_ShouldBeGated()
    {
        // The SQL Server renderer reports exactly the SQL Server-only names.
        var functions = SqlServerDialect.Instance.SqlServerFunctions;
        functions.Should().NotBeNull();
        functions!.Supports("patindex").Should().BeTrue();
        functions.Supports("hashbytes").Should().BeTrue();
        functions.Supports("json_object").Should().BeTrue();
        functions.Supports("log10").Should().BeFalse();

        var act = () => functions.Render("unknown", ["x"]);
        act.Should().Throw<NotSupportedException>().WithMessage("*unknown*not supported*");
    }
}
