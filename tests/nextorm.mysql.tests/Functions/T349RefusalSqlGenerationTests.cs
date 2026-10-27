using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;

namespace NextORM.MySql.Tests;

/// <summary>
/// The T349 refusals (no database): <c>JSON_TABLE</c> is a table-producing construct, <c>UUID()</c>
/// generates a v1 value outside this API's v4/v7 contract and <c>VALUES(column)</c> has no scalar
/// contract. Each surface member carries the exact message, fails during translation with no SQL
/// emitted and fails in its own body too, because the planner folds a projection whose whole body is a
/// bare static call client-side; the sibling names on the same provider keep rendering. MariaDB keeps
/// the newly gated JSON names unsupported through its exclusion list.
/// </summary>
public class T349RefusalSqlGenerationTests
{
    private const string JsonTableRefusal =
        "MySQL JSON_TABLE is a table-producing construct that requires a FROM/join position and a " +
        "COLUMNS definition list; it is not a scalar function and cannot be expressed on " +
        "SqlFunctions.MySql.";

    private const string UuidRefusal =
        "uuid is refused: MySQL UUID() generates UUID version 1; this API's UUID contract is v4 " +
        "(gen_random_uuid) or v7 (uuidv7), and a v1 value is never substituted for it. Use uuidv7() " +
        "to generate an identifier, or UUID() through raw SQL when v1 is genuinely intended.";

    private const string ValuesRefusal =
        "MySQL VALUES(column) cannot be translated as a scalar query function; use the mutation " +
        "builder's upsert API.";

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
    public void Uuid_ShouldBeRefusedBeforeAnySql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => ctx.GetPreparedQueryCommand(
            e.Select(x => new { x.Id, V = SqlFunctions.MySql.uuid() }), false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(UuidRefusal);

        // Positive control on the same provider: the refusal must not break its sibling.
        SqlOf(ctx, e.Select(x => new { x.Id, V = SqlFunctions.MySql.uuid_short() }))
            .Should().Contain("uuid_short()");
    }

    [Fact]
    public void Values_ShouldBeRefusedBeforeAnySql()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => ctx.GetPreparedQueryCommand(
            e.Select(x => SqlFunctions.MySql.values(x.Id)), false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage(ValuesRefusal);

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.json_storage_free(x.String)))
            .Should().Contain("json_storage_free(somestring)");
    }

    /// <summary>
    /// R8: the refusal is enforced in the member body as well, because the planner can fold a projection
    /// whose whole body is a bare static call client-side (a prepared bare <c>json_table()</c> projection
    /// emits no select item and never reaches the translator). The instance members keep their
    /// translation-time refusal, which a bare projection also reaches.
    /// </summary>
    [Fact]
    public void BareProjections_ShouldThrowFromTheMemberBody()
    {
        using var ctx = MySqlTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var jsonTableBody = () => MySqlFunctions.json_table();
        jsonTableBody.Should().Throw<NotSupportedException>().WithMessage(JsonTableRefusal);

        var uuid = () => ctx.GetPreparedQueryCommand(
            e.Select(_ => SqlFunctions.MySql.uuid()), false, false, CancellationToken.None);
        uuid.Should().Throw<NotSupportedException>().WithMessage(UuidRefusal);

        var values = () => ctx.GetPreparedQueryCommand(
            e.Select(_ => SqlFunctions.MySql.values(1L)), false, false, CancellationToken.None);
        values.Should().Throw<NotSupportedException>().WithMessage(ValuesRefusal);

        var valuesBody = () => new MySqlFunctions().values(1L);
        valuesBody.Should().Throw<NotSupportedException>().WithMessage(ValuesRefusal);

        var uuidBody = () => new MySqlFunctions().uuid();
        uuidBody.Should().Throw<NotSupportedException>().WithMessage(UuidRefusal);
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
