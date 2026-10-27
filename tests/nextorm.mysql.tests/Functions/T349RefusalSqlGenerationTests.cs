using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// The T349 shape refusal (no database): <c>JSON_TABLE</c> is a table-producing construct, so its
/// surface member carries the exact message and fails during translation with no SQL emitted, while the
/// sibling JSON names on the same provider keep rendering. MariaDB keeps the newly gated JSON names
/// unsupported through its exclusion list.
/// </summary>
public class T349RefusalSqlGenerationTests
{
    private const string JsonTableRefusal =
        "MySQL JSON_TABLE is a table-producing construct that requires a FROM/join position and a " +
        "COLUMNS definition list; it is not a scalar function and cannot be expressed on " +
        "SqlFunctions.MySql.";

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void JsonTable_ShouldBeRefusedBeforeAnySql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => ctx.GetPreparedQueryCommand(
            e.Select(x => new { x.Id, V = MySqlFunctions.json_table() }), false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(JsonTableRefusal);

        // Positive controls on the same provider: the refusal must not break its siblings.
        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free(x.String)))
            .Should().Contain("json_storage_free(somestring)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.json_array(1)))
            .Should().Contain("json_array(1)");
    }

    [Fact]
    public void MariaDb_ShouldKeepTheNewlyGatedJsonNamesUnsupported()
    {
        var capability = MariaDbDialect.Instance.MySqlFunctions!;

        capability.Supports("json_pretty").Should().BeFalse("MariaDB has JSON_DETAILED instead");
        capability.Supports("member_of").Should().BeFalse("MEMBER OF has no MariaDB spelling");
        capability.Supports("json_table").Should().BeFalse("JSON_TABLE is a table construct");
        capability.Supports("json_storage_free").Should().BeFalse("MariaDB has no binary-JSON free count");
    }
}
