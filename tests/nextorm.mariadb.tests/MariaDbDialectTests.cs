using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// MariaDB inherits the MySQL rendering and changes the set-operation capability flags plus the
/// row-locking renderer (<c>LOCK IN SHARE MODE</c> accepts wait modes on MariaDB, unlike MySQL).
/// </summary>
public class MariaDbDialectTests
{
    private static readonly ISqlDialect Dialect = MariaDbDialect.Instance;

    [Fact]
    public void DurationHooks_ShouldUseNativeTime()
    {
        Dialect.SupportsNativeDuration.Should().BeTrue();
        Dialect.MakeDurationType(null).Should().Be("time");
        Dialect.MakeDurationType(DurationUnit.Seconds, 6).Should().Be("time(6)");
        Dialect.MakeNullableDurationType(DurationUnit.Seconds, 6).Should().Be("time(6)");
        Dialect.MakeTypeName(typeof(TimeSpan)).Should().Be("time");
    }

    [Fact]
    public void InheritedMySqlHooks_ShouldBeUnchanged()
    {
        Dialect.MakeConcat(["a", "b"]).Should().Be("concat(a, b)");
        Dialect.Escape("t1").Should().Be("`t1`");
        Dialect.QuoteIdentifier("id").Should().Be("`id`");
        Dialect.QuoteIdentifier("a`b").Should().Be("`a``b`");
        Dialect.MakeParam("p0").Should().Be("@p0");
        Dialect.MakeCoalesce("a", "b").Should().Be("coalesce(a, b)");
        Dialect.MakeStringLength("x").Should().Be("char_length(x)");
        Dialect.MakeNow(true).Should().Be("utc_timestamp()");
    }

    [Fact]
    public void LockingHooks_ShouldAcceptWaitModesOnLockInShareMode()
    {
        Dialect.Lock.Should().NotBeNull();
        Dialect.Lock!.Render(LockMode.Update).Should().Be(" for update");
        Dialect.Lock!.Render(LockMode.Share).Should().Be(" lock in share mode");
        Dialect.Lock!.Render(LockMode.Update, LockWaitMode.NoWait).Should().Be(" for update nowait");
        Dialect.Lock!.Render(LockMode.Update, LockWaitMode.SkipLocked).Should().Be(" for update skip locked");
        Dialect.Lock!.Render(LockMode.Share, LockWaitMode.NoWait).Should().Be(" lock in share mode nowait");
        Dialect.Lock!.Render(LockMode.Share, LockWaitMode.SkipLocked).Should().Be(" lock in share mode skip locked");
    }

    [Fact]
    public void CapabilityFlags_ShouldEnableIntersectExceptAll()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsFullJoin.Should().BeFalse();
        Dialect.SupportsIntersectExceptAll.Should().BeTrue();
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.SupportsQueryHints.Should().BeTrue();
        Dialect.SupportsAnyValueAggregate.Should().BeFalse();
        Dialect.MakeAggregate("any_agg").Should().Be("ANY_VALUE");
        Dialect.SupportsPercentileWindow.Should().BeTrue();
    }

    [Fact]
    public void InheritedSessionInfoHooks_ShouldBeUnchanged()
    {
        Dialect.SessionInfoFunctions.Should().NotBeNull();
        Dialect.SessionInfoFunctions!.Render("current_user").Should().Be("current_user()");
        Dialect.SessionInfoFunctions!.Render("current_database").Should().Be("database()");
    }

    [Fact]
    public void QueryHintHooks_ShouldInheritInlineOptimizerHintComment()
    {
        Dialect.SupportsQueryHints.Should().BeTrue();
        Dialect.RenderQueryHints("select 1", ["MAX_EXECUTION_TIME(1000)"], null)
            .Should().Be("select /*+ MAX_EXECUTION_TIME(1000) */ 1");
    }

    [Fact]
    public void UuidHooks_ShouldUseMariaDbForms()
    {
        Dialect.UuidGenerators.Should().NotBeNull();
        Dialect.UuidGenerators!.Supports("gen_random_uuid").Should().BeTrue();
        Dialect.UuidGenerators!.Supports("uuidv7").Should().BeTrue();
        Dialect.UuidGenerators!.Render("gen_random_uuid").Should().Be("uuid_v4()");
        Dialect.UuidGenerators!.Render("uuidv7").Should().Be("uuid_v7()");
    }

    [Fact]
    public void MySqlFunctionHooks_ShouldBeInheritedFromMySql()
    {
        Dialect.MySqlFunctions.Should().NotBeNull();
        Dialect.MySqlFunctions!.Supports("find_in_set").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("json_set").Should().BeTrue();
        Dialect.MySqlFunctions!.Render("md5", ["x"]).Should().Be("md5(x)");
        Dialect.MySqlFunctions!.Render("date_format", ["d", "'%Y'"]).Should().Be("date_format(d, '%Y')");

        // MariaDB has no UUID_TO_BIN/BIN_TO_UUID; only MySQL supports them.
        Dialect.MySqlFunctions!.Supports("uuid_to_bin").Should().BeFalse();
        Dialect.MySqlFunctions!.Supports("bin_to_uuid").Should().BeFalse();
        var act = () => Dialect.MySqlFunctions!.Render("uuid_to_bin", ["'x'"]);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void MariaDbOnlyFunctionHooks_ShouldRenderTheNativeSurface()
    {
        Dialect.MySqlFunctions.Should().NotBeNull();

        Dialect.MySqlFunctions!.Supports("regexp_instr").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("nvl").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("add_months").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("kdf").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("xxh3").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("next_value_for").Should().BeTrue();

        Dialect.MySqlFunctions!.Render("regexp_instr", ["a", "'b'"]).Should().Be("regexp_instr(a, 'b')");
        Dialect.MySqlFunctions!.Render("regexp_substr", ["a", "'b'"]).Should().Be("regexp_substr(a, 'b')");
        Dialect.MySqlFunctions!.Render("regexp_replace", ["a", "'b'", "'c'"]).Should().Be("regexp_replace(a, 'b', 'c')");
        Dialect.MySqlFunctions!.Render("nvl", ["a", "b"]).Should().Be("nvl(a, b)");
        Dialect.MySqlFunctions!.Render("nvl2", ["a", "b", "c"]).Should().Be("nvl2(a, b, c)");
        Dialect.MySqlFunctions!.Render("add_months", ["d", "2"]).Should().Be("add_months(d, 2)");
        Dialect.MySqlFunctions!.Render("months_between", ["a", "b"]).Should().Be("months_between(a, b)");
        Dialect.MySqlFunctions!.Render("to_char", ["d", "'YYYY'"]).Should().Be("to_char(d, 'YYYY')");
        Dialect.MySqlFunctions!.Render("to_date", ["'s'", "'YYYY'"]).Should().Be("to_date('s', 'YYYY')");
        Dialect.MySqlFunctions!.Render("to_number", ["'1'", "'9'"]).Should().Be("to_number('1', '9')");
        Dialect.MySqlFunctions!.Render("kdf", ["'p'", "'s'", "'i'", "'hkdf'"]).Should().Be("kdf('p', 's', 'i', 'hkdf')");
        Dialect.MySqlFunctions!.Render("xxh3", ["x"]).Should().Be("xxh3(x)");
        Dialect.MySqlFunctions!.Render("xxh32", ["x"]).Should().Be("xxh32(x)");
        Dialect.MySqlFunctions!.Render("json_detailed", ["j"]).Should().Be("json_detailed(j)");
        Dialect.MySqlFunctions!.Render("json_compact", ["j"]).Should().Be("json_compact(j)");

        Dialect.MySqlFunctions!.Render("next_value_for", ["'s'"]).Should().Be("next value for s");
        Dialect.MySqlFunctions!.Render("nextval", ["'s'"]).Should().Be("nextval(s)");
        Dialect.MySqlFunctions!.Render("setval", ["'s'", "42"]).Should().Be("setval(s, 42)");
        Dialect.MySqlFunctions!.Render("lastval", ["'s'"]).Should().Be("lastval(s)");
    }
}
