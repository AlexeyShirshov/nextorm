using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>SQL generation for the SQLite-only surface (<see cref="SqlFunctions.Sqlite"/>) without a database.</summary>
public class SqliteFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void CoreScalars_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            P = SqlFunctions.Sqlite.printf("%d", 1),
            F = SqlFunctions.Sqlite.format("%d", x.Id),
            H = SqlFunctions.Sqlite.hex(x.String),
            U = SqlFunctions.Sqlite.unhex("41"),
            Q = SqlFunctions.Sqlite.quote(x.String),
            T = SqlFunctions.Sqlite.@typeof(x.String),
            G = SqlFunctions.Sqlite.glob("a*", x.String),
            N = SqlFunctions.Sqlite.unicode(x.String),
            O = SqlFunctions.Sqlite.octet_length(x.String),
            Co = SqlFunctions.Sqlite.@char(65, 66),
            Ifn = SqlFunctions.Sqlite.ifnull(x.Int, 0)
        }));

        sql.Should().Contain("printf('%d', 1)");
        sql.Should().Contain("format('%d', id)");
        sql.Should().Contain("hex(somestring)");
        sql.Should().Contain("unhex('41')");
        sql.Should().Contain("quote(somestring)");
        sql.Should().Contain("typeof(somestring)");
        sql.Should().Contain("glob('a*', somestring)");
        sql.Should().Contain("unicode(somestring)");
        sql.Should().Contain("octet_length(somestring)");
        sql.Should().Contain("char(65, 66)");
        sql.Should().Contain("ifnull(nullableint, 0)");
    }

    [Fact]
    public void NoArgumentCoreScalars_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            R = SqlFunctions.Sqlite.random(),
            Rb = SqlFunctions.Sqlite.randomblob(4),
            I = SqlFunctions.Sqlite.@if(x.Int > 0, "yes", "no"),
            S = SqlFunctions.Sqlite.soundex(x.String)
        }));

        sql.Should().Contain("random()");
        sql.Should().Contain("randomblob(4)");
        sql.Should().Contain("if((nullableint > 0), 'yes', 'no')");
        sql.Should().Contain("soundex(somestring)");
    }

    [Fact]
    public void Math_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Sqlite.acos(0.5),
            Ah = SqlFunctions.Sqlite.acosh(1.5),
            As = SqlFunctions.Sqlite.asin(0.5),
            Ash = SqlFunctions.Sqlite.asinh(0.5),
            At = SqlFunctions.Sqlite.atan(0.5),
            At2 = SqlFunctions.Sqlite.atan2(1.0, 2.0),
            Ath = SqlFunctions.Sqlite.atanh(0.5),
            Csh = SqlFunctions.Sqlite.cosh(0.5),
            L10 = SqlFunctions.Sqlite.log10(100.0),
            L2 = SqlFunctions.Sqlite.log2(8.0),
            M = SqlFunctions.Sqlite.mod(5.0, 2.0),
            Sh = SqlFunctions.Sqlite.sinh(0.5),
            Th = SqlFunctions.Sqlite.tanh(0.5)
        }));

        sql.Should().Contain("acos(0.5)");
        sql.Should().Contain("acosh(1.5)");
        sql.Should().Contain("asin(0.5)");
        sql.Should().Contain("asinh(0.5)");
        sql.Should().Contain("atan(0.5)");
        sql.Should().Contain("atan2(1, 2)");
        sql.Should().Contain("atanh(0.5)");
        sql.Should().Contain("cosh(0.5)");
        sql.Should().Contain("log10(100)");
        sql.Should().Contain("log2(8)");
        sql.Should().Contain("mod(5, 2)");
        sql.Should().Contain("sinh(0.5)");
        sql.Should().Contain("tanh(0.5)");
    }

    [Fact]
    public void Dates_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            T = SqlFunctions.Sqlite.timediff(x.Datetime, x.Datetime),
            U = SqlFunctions.Sqlite.unixepoch(x.Datetime),
            J = SqlFunctions.Sqlite.julianday(x.Datetime)
        }));

        sql.Should().Contain("timediff(dt, dt)");
        sql.Should().Contain("unixepoch(dt)");
        sql.Should().Contain("julianday(dt)");
    }

    [Fact]
    public void JsonScalars_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            Ex = SqlFunctions.Sqlite.json_extract<int>(x.String, "$.a"),
            G = SqlFunctions.Sqlite.json_get(x.String, "$.a"),
            Gt = SqlFunctions.Sqlite.json_get_text(x.String, "$.a"),
            J = SqlFunctions.Sqlite.json(x.String),
            Jb = SqlFunctions.Sqlite.jsonb(x.String),
            Ar = SqlFunctions.Sqlite.json_array(1, x.String),
            Ob = SqlFunctions.Sqlite.json_object("a", 1),
            Ins = SqlFunctions.Sqlite.json_insert(x.String, "$.a", 1),
            Rep = SqlFunctions.Sqlite.json_replace(x.String, "$.a", 1),
            Set = SqlFunctions.Sqlite.json_set(x.String, "$.a", 1),
            Ai = SqlFunctions.Sqlite.json_array_insert(x.String, "$.a[0]", 1),
            Pa = SqlFunctions.Sqlite.json_patch(x.String, "{}"),
            Pr = SqlFunctions.Sqlite.json_pretty(x.String),
            Qu = SqlFunctions.Sqlite.json_quote(x.String),
            Rm = SqlFunctions.Sqlite.json_remove(x.String, "$.a"),
            Ty = SqlFunctions.Sqlite.json_type(x.String, "$.a"),
            Va = SqlFunctions.Sqlite.json_valid(x.String)
        }));

        sql.Should().Contain("json_extract(somestring, '$.a')");
        sql.Should().Contain("(somestring -> '$.a')");
        sql.Should().Contain("(somestring ->> '$.a')");
        sql.Should().Contain("json(somestring)");
        sql.Should().Contain("jsonb(somestring)");
        sql.Should().Contain("json_array(1, somestring)");
        sql.Should().Contain("json_object('a', 1)");
        sql.Should().Contain("json_insert(somestring, '$.a', 1)");
        sql.Should().Contain("json_replace(somestring, '$.a', 1)");
        sql.Should().Contain("json_set(somestring, '$.a', 1)");
        sql.Should().Contain("json_array_insert(somestring, '$.a[0]', 1)");
        sql.Should().Contain("json_patch(somestring, '{}')");
        sql.Should().Contain("json_pretty(somestring)");
        sql.Should().Contain("json_quote(somestring)");
        sql.Should().Contain("json_remove(somestring, '$.a')");
        sql.Should().Contain("json_type(somestring, '$.a')");
        sql.Should().Contain("json_valid(somestring)");
    }

    [Fact]
    public void JsonAggregates_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            A = SqlFunctions.Sqlite.json_group_array(x.String),
            O = SqlFunctions.Sqlite.json_group_object(x.Int, x.String)
        }));

        sql.Should().Contain("json_group_array(somestring)");
        sql.Should().Contain("json_group_object(nullableint, somestring)");
    }

    [Fact]
    public void JsonTableFunctions_ShouldEmitNativeForms()
    {
        using var ctx = SqliteTestContext.Create();

        var eachSql = SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Sqlite.json_each("[1,2,3]")).Select(r => r.Value));
        var treeSql = SqlOf(ctx, ctx.FromTableFunction(() => SqlFunctions.Sqlite.json_tree("[1,2,3]")).Select(r => r.Value));

        eachSql.Should().Contain("json_each('[1,2,3]')");
        treeSql.Should().Contain("json_tree('[1,2,3]')");
    }
}
