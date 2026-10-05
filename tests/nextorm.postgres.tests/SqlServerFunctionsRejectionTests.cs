using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// The SQL Server-only T-SQL scalar functions (the #182 date/binary/other families) must be rejected
/// by a provider that does not expose <see cref="ISqlDialect.SqlServerFunctions"/> (no database).
/// </summary>
public class SqlServerFunctionsRejectionTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    [Trait("Issue", "182")]
    public void DateFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.sysdatetime() }), "sysdatetime");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.sysdatetimeoffset() }), "sysdatetimeoffset");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.sysutcdatetime() }), "sysutcdatetime");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.switchoffset(SqlFunctions.SqlServer.sysdatetimeoffset(), 480) }), "switchoffset");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.todatetimeoffset(x.Datetime, -120) }), "todatetimeoffset");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.timefromparts(1, 2, 3, 4, 7) }), "timefromparts");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.smalldatetimefromparts(2020, 1, 2, 3, 4) }), "smalldatetimefromparts");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.datetimefromparts(2020, 1, 2, 3, 4, 5, 6) }), "datetimefromparts");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.datetime2fromparts(2020, 1, 2, 3, 4, 5, 6, 3) }), "datetime2fromparts");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.datetimeoffsetfromparts(2020, 1, 2, 3, 4, 5, 6, 7, -30, 3) }), "datetimeoffsetfromparts");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void BinaryAndOtherFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var data = new byte[] { 1, 2, 3 };

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.checksum(x.Id) }), "checksum");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.binary_checksum(x.Id) }), "binary_checksum");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.compress(x.String) }), "compress");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.decompress(data) }), "decompress");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.rand() }), "rand");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.rand(0) }), "rand");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.stuff(x.String, 1, 2, "x") }), "stuff");
    }

    [Fact]
    [Trait("Issue", "182")]
    public void MetadataFunctions_ShouldThrowBecauseNotSupported()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.col_length("t", "c") }), "col_length");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.col_name(1, 2) }), "col_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.ident_incr("t") }), "ident_incr");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.ident_seed("t") }), "ident_seed");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.index_col("t", 1, 2) }), "index_col");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_definition(1) }), "object_definition");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_id("t") }), "object_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_id("t", "U") }), "object_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_name(1) }), "object_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_name(1, 2) }), "object_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_schema_name(1) }), "object_schema_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.object_schema_name(1, 2) }), "object_schema_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.stats_date(1, 2) }), "stats_date");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.db_id() }), "db_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.db_id("db") }), "db_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.db_name() }), "db_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.db_name(1) }), "db_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.original_db_name() }), "original_db_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.schema_id() }), "schema_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.schema_id("s") }), "schema_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.schema_name() }), "schema_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.schema_name(1) }), "schema_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.type_id("t") }), "type_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.type_name(1) }), "type_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.filegroup_id("fg") }), "filegroup_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.filegroup_name(1) }), "filegroup_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.file_id("f") }), "file_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.file_idex("f") }), "file_idex");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.file_name(1) }), "file_name");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.current_timezone() }), "current_timezone");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.current_timezone_id() }), "current_timezone_id");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.formatmessage("m") }), "formatmessage");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.formatmessage(50001, "x") }), "formatmessage");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.getansinull() }), "getansinull");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.getansinull("db") }), "getansinull");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.isdate("x") }), "isdate");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.isnumeric("x") }), "isnumeric");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.parsename("a.b", 1) }), "parsename");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.publishingservername() }), "publishingservername");
        AssertUnsupported(ctx, e.Select(x => new { V = SqlFunctions.SqlServer.str(1.5) }), "str");
    }

    private static void AssertUnsupported<T>(IDataContext ctx, QueryCommand<T> cmd, string name)
    {
        var act = () => SqlOf(ctx, cmd);
        act.Should().Throw<NotSupportedException>().WithMessage($"*{name}*not supported*");
    }
}
