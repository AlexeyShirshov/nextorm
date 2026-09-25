using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>SQL generation for the cross-provider scalar functions on SQLite (no database).</summary>
public class CrossProviderScalarSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void SupportedScalarFunctions_ShouldEmitNativeSpellings()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, e.Select(x => new
        {
            L = SqlFunctions.Sql.left(x.String, 3),
            R = SqlFunctions.Sql.right(x.String, 3),
            Cw = SqlFunctions.Sql.concat_ws(",", x.String, x.String),
            Asc = SqlFunctions.Sql.ascii(x.String),
            Ch = SqlFunctions.Sql.@char(65)
        }));

        sql.Should().Contain("substr(somestring, 1, 3)");
        sql.Should().Contain("case when (3) >= length(somestring) then somestring else substr(somestring, length(somestring) - (3) + 1, 3) end");
        sql.Should().Contain("concat_ws(',', somestring, somestring)");
        sql.Should().Contain("unicode(somestring)");
        sql.Should().Contain("char(65)");
    }

    [Fact]
    public void Lpad_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.Sql.lpad(x.String, 5, "0") }), "lpad");
    }

    [Fact]
    public void Repeat_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.Sql.repeat(x.String, 2) }), "repeat");
    }

    [Fact]
    public void Reverse_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.Sql.reverse(x.String) }), "reverse");
    }

    [Fact]
    public void Space_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.Sql.space(3) }), "space");
    }

    [Fact]
    public void Translate_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.Sql.translate(x.String, "ab", "xy") }), "translate");
    }

    private static void AssertUnsupported<T>(IDataContext ctx, QueryCommand<T> cmd, string name)
    {
        var act = () => SqlOf(ctx, cmd);
        act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }
}
