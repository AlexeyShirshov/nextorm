using FluentAssertions;
using NextORM.Core;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// Version-gated <c>UPDATE ... RETURNING</c> on MariaDB (13.0+). <c>INSERT</c>/<c>DELETE</c>
/// <c>RETURNING</c> stays unsupported at every version, and — unlike PostgreSQL — the same concrete
/// context type may be used with several versions (no type guard).
/// </summary>
public class VersionGateTests
{
    [Fact]
    public void UpdateReturning_Unset_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();

        Action act = () => ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UpdateReturning_V12_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create(new Version(12, 1));

        Action act = () => ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UpdateReturning_V13_ShouldEmitReturning()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0));

        ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("update merge_entity set name = _utf8mb4 X'61' where id = 1 returning id, name, age, total");
    }

    [Fact]
    public void UpdateReturning_V13Patch_ShouldEmitReturning()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0, 2));

        ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Contain(" returning id, name, age, total");
    }

    [Fact]
    public void UpdateReturning_Later_ShouldEmitReturning()
    {
        using var ctx = MariaDbTestContext.Create(new Version(14, 0));

        ctx.CreateUpdateBuilder<IMergeEntity>()
            .Set(x => x.Name, "a")
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Contain(" returning ");
    }

    [Fact]
    public void InsertReturning_V13_ShouldStillThrow()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0));

        var builder = ctx.CreateInsertBuilder<IMergeEntity>()
            .Value(x => x.Name, "a")
            .Returning();

        Action act = () => builder.ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DeleteReturning_V13_ShouldStillThrow()
    {
        using var ctx = MariaDbTestContext.Create(new Version(13, 0));

        Action act = () => ctx.CreateDeleteBuilder<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NoPostgresStyleTypeGuard_Applies()
    {
        // The same concrete MariaDbDataContext type may be bound to two different versions; the
        // PostgreSQL one-version-per-context-type guard must not apply here.
        using (var old = MariaDbTestContext.CreateMariaDb(new Version(12, 1)))
            old.Dialect.Should().NotBeNull();

        using (var newer = MariaDbTestContext.CreateMariaDb(new Version(13, 0)))
            newer.Dialect.Should().NotBeNull();
    }

    [Fact]
    public void ServerVersionSnapshot_ShouldAgreeWithTheDialect()
    {
        // The versioned constructor must forward the configured version to the base snapshot, not only
        // to the dialect: otherwise DataContext.ServerVersion would stay null while the dialect is
        // version-gated (the protected snapshot would disagree with the dialect).
        using (var versioned = new ServerVersionExposingContext(new Version(13, 0)))
        {
            versioned.ExposedServerVersion.Should().Be(new Version(13, 0));
            versioned.Dialect.SupportsUpdateReturning.Should().BeTrue();
        }

        using (var unset = new ServerVersionExposingContext(null))
        {
            unset.ExposedServerVersion.Should().BeNull();
            unset.Dialect.SupportsUpdateReturning.Should().BeFalse();
        }
    }

    [Fact]
    public void GatedFunctions_Unset_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb();

        AssertAllGatedFunctionsThrow(ctx, "an unversioned context must refuse every version-gated form");
    }

    [Fact]
    public void GatedFunctions_V114_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb(new Version(11, 4));

        AssertAllGatedFunctionsThrow(ctx, "none of the five forms exists on MariaDB 11.4");
    }

    [Fact]
    public void UuidGenerators_V117_ShouldRender_WhileScalarsRefuse()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb(new Version(11, 7));

        AssertUuidGeneratorsRender(ctx);
        AssertScalarConversionsThrow(ctx);
    }

    [Fact]
    public void GatedFunctions_V121_ShouldKeepUuidAndRefuseScalars()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb(new Version(12, 1));

        AssertUuidGeneratorsRender(ctx);
        AssertScalarConversionsThrow(ctx);
    }

    [Fact]
    public void ScalarConversions_V122Family_ShouldRefuse()
    {
        foreach (var version in new Version[] { new(12, 2), new(12, 2, 0) })
        {
            using var ctx = MariaDbTestContext.CreateMariaDb(version);

            AssertUuidGeneratorsRender(ctx);
            AssertScalarConversionsThrow(ctx);
        }
    }

    [Fact]
    public void NumberFamily_V1221_ShouldEnable_WhileToDateRefuses()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb(new Version(12, 2, 1));
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.months_between(x.Datetime, x.Datetime)))
            .Should().Contain("months_between(dt, dt)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_number("100.00", "999.99")))
            .Should().Contain("to_number(_utf8mb4 X'3130302e3030', _utf8mb4 X'3939392e3939')");
        AssertUuidGeneratorsRender(ctx);

        var date = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_date("2023-01-02", "YYYY-MM-DD")));
        date.Should().Throw<NotSupportedException>().WithMessage("*to_date*");
    }

    [Fact]
    public void GatedFunctions_V123Family_ShouldRenderAll()
    {
        foreach (var version in new Version[] { new(12, 3), new(12, 3, 0) })
        {
            using var ctx = MariaDbTestContext.CreateMariaDb(version);

            AssertGatedFunctionsRender(ctx);
        }
    }

    [Fact]
    public void GatedFunctions_V130_ShouldRenderAll_AndKeepUpdateReturning()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb(new Version(13, 0));

        AssertGatedFunctionsRender(ctx);
        ctx.Dialect.SupportsUpdateReturning.Should().BeTrue();
    }

    [Fact]
    public void GatedCapabilities_ShouldNotLeakAcrossInterleavedContexts()
    {
        using var unset = MariaDbTestContext.CreateMariaDb();
        using var old = MariaDbTestContext.CreateMariaDb(new Version(11, 4));
        using var newer = MariaDbTestContext.CreateMariaDb(new Version(12, 3));

        AssertGatedFunctionsRender(newer);
        AssertAllGatedFunctionsThrow(unset, "the unversioned context must stay pessimistic");
        AssertAllGatedFunctionsThrow(old, "11.4 must not inherit a newer context's capability");
        AssertGatedFunctionsRender(newer);

        unset.Dialect.MySqlFunctions!.Supports("months_between").Should().BeFalse();
        unset.Dialect.UuidGenerators!.Supports("uuidv7").Should().BeFalse();
        old.Dialect.MySqlFunctions!.Supports("to_date").Should().BeFalse();
        newer.Dialect.MySqlFunctions!.Supports("to_date").Should().BeTrue();
        newer.Dialect.UuidGenerators!.Supports("uuidv7").Should().BeTrue();
    }

    [Fact]
    public void GatedFunctions_NullableArguments_ShouldRenderOnEnabledAndRefuseOnUnset()
    {
        using var enabled = MariaDbTestContext.CreateMariaDb(new Version(12, 3));
        var enabledEntity = enabled.From<IComplexEntity>();

        SqlOf(enabled, enabledEntity.Select(x => SqlFunctions.MySql.months_between(x.Datetime, null)))
            .Should().Contain("months_between(dt, null)");
        SqlOf(enabled, enabledEntity.Select(_ => SqlFunctions.MySql.to_date(null, "YYYY-MM-DD")))
            .Should().Contain("to_date(null, _utf8mb4 X'595959592d4d4d2d4444')");

        using var unset = MariaDbTestContext.CreateMariaDb();
        var unsetEntity = unset.From<IComplexEntity>();
        var act = () => SqlOf(unset, unsetEntity.Select(x => SqlFunctions.MySql.months_between(x.Datetime, null)));
        act.Should().Throw<NotSupportedException>("refusal is capability-based, not argument-based");
    }

    [Fact]
    public void GatedFunctions_FormatArguments_ShouldStayVerbatim()
    {
        using var ctx = MariaDbTestContext.CreateMariaDb(new Version(12, 3));
        var e = ctx.From<IComplexEntity>();

        var dateSql = SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_date("2023-01-02", "YYYY-MM-DD")));
        dateSql.Should().Contain("_utf8mb4 X'595959592d4d4d2d4444'");
        dateSql.Should().NotContain("%Y", "the Oracle mask must not be translated to a str_to_date mask");

        var numberSql = SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_number("100.00", "999.99")));
        numberSql.Should().Contain("to_number(_utf8mb4 X'3130302e3030', _utf8mb4 X'3939392e3939')");
        numberSql.Should().NotContain("cast(", "no decimal-cast substitution");
    }

    [Fact]
    public void GatedCapabilities_DirectSupportsAndRender_ShouldAgree()
    {
        (Version? Version, bool Months, bool Date, bool Number, bool Uuid)[] matrix =
        [
            (null, false, false, false, false),
            (new Version(11, 4), false, false, false, false),
            (new Version(11, 7), false, false, false, true),
            (new Version(12, 1), false, false, false, true),
            (new Version(12, 2), false, false, false, true),
            (new Version(12, 2, 0), false, false, false, true),
            (new Version(12, 2, 1), true, false, true, true),
            (new Version(12, 3), true, true, true, true),
            (new Version(12, 3, 0), true, true, true, true),
            (new Version(13, 0), true, true, true, true)
        ];

        foreach (var (version, months, date, number, uuid) in matrix)
        {
            var dialect = new MariaDbDialect(version);
            var functions = dialect.MySqlFunctions!;

            functions.Supports("months_between").Should().Be(months, $"months_between at {version?.ToString() ?? "unset"}");
            functions.Supports("to_date").Should().Be(date, $"to_date at {version?.ToString() ?? "unset"}");
            functions.Supports("to_number").Should().Be(number, $"to_number at {version?.ToString() ?? "unset"}");
            dialect.UuidGenerators!.Supports("gen_random_uuid").Should().Be(uuid, $"gen_random_uuid at {version?.ToString() ?? "unset"}");
            dialect.UuidGenerators!.Supports("uuidv7").Should().Be(uuid, $"uuidv7 at {version?.ToString() ?? "unset"}");

            AssertRenderAgrees(functions, "months_between", months);
            AssertRenderAgrees(functions, "to_date", date);
            AssertRenderAgrees(functions, "to_number", number);
            AssertUuidRenderAgrees(dialect.UuidGenerators!, "gen_random_uuid", uuid);
            AssertUuidRenderAgrees(dialect.UuidGenerators!, "uuidv7", uuid);
        }
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) =>
        Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    private static void AssertGatedFunctionsRender(IDataContext ctx)
    {
        AssertScalarConversionsRender(ctx);
        AssertUuidGeneratorsRender(ctx);
    }

    private static void AssertScalarConversionsRender(IDataContext ctx)
    {
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(x => SqlFunctions.MySql.months_between(x.Datetime, x.Datetime)))
            .Should().Contain("months_between(dt, dt)");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_date("2023-01-02", "YYYY-MM-DD")))
            .Should().Contain("to_date(_utf8mb4 X'323032332d30312d3032', _utf8mb4 X'595959592d4d4d2d4444')");
        SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_number("100.00", "999.99")))
            .Should().Contain("to_number(_utf8mb4 X'3130302e3030', _utf8mb4 X'3939392e3939')");
    }

    private static void AssertUuidGeneratorsRender(IDataContext ctx)
    {
        var e = ctx.From<IComplexEntity>();

        SqlOf(ctx, e.Select(_ => new { U = SqlFunctions.Sql.gen_random_uuid(), V7 = SqlFunctions.Sql.uuidv7() }))
            .Should().Contain("uuid_v4()").And.Contain("uuid_v7()");
    }

    private static void AssertAllGatedFunctionsThrow(IDataContext ctx, string because)
    {
        AssertScalarConversionsThrow(ctx, because);
        AssertUuidGeneratorsThrow(ctx, because);
    }

    private static void AssertScalarConversionsThrow(IDataContext ctx, string because = "")
    {
        var e = ctx.From<IComplexEntity>();

        var months = () => SqlOf(ctx, e.Select(x => SqlFunctions.MySql.months_between(x.Datetime, x.Datetime)));
        var date = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_date("2023-01-02", "YYYY-MM-DD")));
        var number = () => SqlOf(ctx, e.Select(_ => SqlFunctions.MySql.to_number("100.00", "999.99")));

        months.Should().Throw<NotSupportedException>(because).WithMessage("*months_between*");
        date.Should().Throw<NotSupportedException>(because).WithMessage("*to_date*");
        number.Should().Throw<NotSupportedException>(because).WithMessage("*to_number*");
    }

    private static void AssertUuidGeneratorsThrow(IDataContext ctx, string because = "")
    {
        var e = ctx.From<IComplexEntity>();

        var v4 = () => SqlOf(ctx, e.Select(_ => new { U = SqlFunctions.Sql.gen_random_uuid() }));
        var v7 = () => SqlOf(ctx, e.Select(_ => new { V = SqlFunctions.Sql.uuidv7() }));

        v4.Should().Throw<NotSupportedException>(because).WithMessage("*gen_random_uuid*");
        v7.Should().Throw<NotSupportedException>(because).WithMessage("*uuidv7*");
    }

    private static void AssertRenderAgrees(IMySqlFunctions functions, string name, bool expected)
    {
        var act = () => functions.Render(name, ["a", "b"]);

        if (expected)
            act.Should().NotThrow($"{name} must render its native form when Supports is true");
        else
            act.Should().Throw<NotSupportedException>($"{name} must throw when Supports is false");
    }

    private static void AssertUuidRenderAgrees(IUuidGenerators generators, string name, bool expected)
    {
        var act = () => generators.Render(name);

        if (expected)
            act.Should().NotThrow($"{name} must render its native form when Supports is true");
        else
            act.Should().Throw<NotSupportedException>($"{name} must throw when Supports is false");
    }

    private sealed class ServerVersionExposingContext : MariaDbDataContext
    {
        public ServerVersionExposingContext(Version? serverVersion)
            : base("Server=localhost;Port=3306;Database=nextorm;User ID=nextorm;Password=nextorm", new DataContextBuilder(), serverVersion)
        {
        }

        public Version? ExposedServerVersion => ServerVersion;
    }
}
