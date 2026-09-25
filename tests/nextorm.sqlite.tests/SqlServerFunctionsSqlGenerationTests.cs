using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// The SQL Server-only T-SQL scalar functions must be rejected by a provider that does not expose
/// <see cref="ISqlDialect.SqlServerFunctions"/> (no database).
/// </summary>
public class SqlServerFunctionsSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void StringFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.patindex("%a%", x.String) }), "patindex");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.quotename(x.String) }), "quotename");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.soundex(x.String) }), "soundex");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.difference(x.String, "a") }), "difference");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.unicode(x.String) }), "unicode");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.nchar(65) }), "nchar");
    }

    [Fact]
    public void NumericFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.acos(0.5) }), "acos");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.square(2.0) }), "square");
    }

    [Fact]
    public void DateFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.datename("month", x.Datetime) }), "datename");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.date_bucket("day", 1, x.Datetime) }), "date_bucket");
    }

    [Fact]
    public void BinaryFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var data = new byte[] { 1, 2, 3 };

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.hashbytes("SHA2_256", data) }), "hashbytes");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.newsequentialid() }), "newsequentialid");
    }

    [Fact]
    public void JsonFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.json_array("a", "b") }), "json_array");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.json_object("k", "v") }), "json_object");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.json_path_exists(x.String, "$") }), "json_path_exists");
    }

    private static void AssertUnsupported<T>(IDataContext ctx, QueryCommand<T> cmd, string name)
    {
        var act = () => SqlOf(ctx, cmd);
        act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }
}
