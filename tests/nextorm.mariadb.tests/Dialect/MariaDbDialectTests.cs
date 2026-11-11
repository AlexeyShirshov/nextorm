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
    public void SupportsTableValuedParameters_IsTrue()
        => Dialect.SupportsTableValuedParameters.Should().BeTrue();

    [Fact]
    public void SupportsSequentialAccess_IsFalse()
        => Dialect.SupportsSequentialAccess.Should().BeFalse();

    [Fact]
    public void SupportsSavepoints_InheritsMySqlTrue()
        => Dialect.SupportsSavepoints.Should().BeTrue();

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
        Dialect.MakeTypeName(typeof(uint)).Should().Be("bigint");
    }

    [Fact]
    public void MakeLikeEscape_ShouldRenderModeIndependentHexTokens()
    {
        // MariaDB inherits the MySQL policy: a charset-introduced hex literal is spelled identically
        // with and without NO_BACKSLASH_ESCAPES, so the escape operand never depends on session mode.
        Dialect.MakeLikeEscape("\\").Should().Be(" escape _utf8mb4 X'5c'");
        Dialect.MakeLikeEscape("!").Should().Be(" escape _utf8mb4 X'21'");
    }

    [Fact]
    public void InheritedBackslashQuoting_ShouldRemainUnchanged()
    {
        // Scope lock for #161: MariaDB inherits MySQL quoting and must not adopt the ClickHouse
        // backslash-escaping rule.
        Dialect.QuoteIdentifier(@"a\b").Should().Be(@"`a\b`");
        Dialect.Escape(@"a\b").Should().Be(@"`a\b`");
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
        Dialect.SupportsApply.Should().BeFalse(
            "MariaDB 11.4 rejects the generated lateral spelling, so APPLY is a capability refusal");
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
        var versioned = new MariaDbDialect(new Version(11, 7));

        versioned.UuidGenerators.Should().NotBeNull();
        versioned.UuidGenerators!.Supports("gen_random_uuid").Should().BeTrue();
        versioned.UuidGenerators!.Supports("uuidv7").Should().BeTrue();
        versioned.UuidGenerators!.Render("gen_random_uuid").Should().Be("uuid_v4()");
        versioned.UuidGenerators!.Render("uuidv7").Should().Be("uuid_v7()");

        Dialect.UuidGenerators.Should().NotBeNull();
        Dialect.UuidGenerators!.Supports("gen_random_uuid").Should().BeFalse();
        Dialect.UuidGenerators!.Supports("uuidv7").Should().BeFalse();
        var unset = () => Dialect.UuidGenerators!.Render("uuidv7");
        unset.Should().Throw<NotSupportedException>().WithMessage("*uuidv7*11.7*");
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
    public void T284_JsonFunctions_ShouldAdvertiseMariaDbCapability()
    {
        Dialect.MySqlFunctions.Should().NotBeNull();
        Dialect.MySqlFunctions!.Supports("json_equals").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("json_normalize").Should().BeTrue();
        Dialect.MySqlFunctions!.Render("json_equals", ["lhs", "rhs"]).Should().Be("json_equals(lhs, rhs)");
        Dialect.MySqlFunctions!.Render("json_normalize", ["j"]).Should().Be("json_normalize(j)");
    }

    [Fact]
    public void MariaDbOnlyFunctionHooks_ShouldRenderTheNativeSurface()
    {
        Dialect.MySqlFunctions.Should().NotBeNull();

        Dialect.MySqlFunctions!.Supports("regexp_instr").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("nvl").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("add_months").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("kdf").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("next_value_for").Should().BeTrue();

        Dialect.MySqlFunctions!.Render("regexp_instr", ["a", "'b'"]).Should().Be("regexp_instr(a, 'b')");
        Dialect.MySqlFunctions!.Render("regexp_substr", ["a", "'b'"]).Should().Be("regexp_substr(a, 'b')");
        Dialect.MySqlFunctions!.Render("regexp_replace", ["a", "'b'", "'c'"]).Should().Be("regexp_replace(a, 'b', 'c')");
        Dialect.MySqlFunctions!.Render("nvl", ["a", "b"]).Should().Be("nvl(a, b)");
        Dialect.MySqlFunctions!.Render("nvl2", ["a", "b", "c"]).Should().Be("nvl2(a, b, c)");
        Dialect.MySqlFunctions!.Render("add_months", ["d", "2"]).Should().Be("add_months(d, 2)");
        Dialect.MySqlFunctions!.Render("to_char", ["d", "'YYYY'"]).Should().Be("to_char(d, 'YYYY')");
        Dialect.MySqlFunctions!.Render("kdf", ["'p'", "'s'", "'i'", "'hkdf'"]).Should().Be("kdf('p', 's', 'i', 'hkdf')");
        Dialect.MySqlFunctions!.Render("json_detailed", ["j"]).Should().Be("json_detailed(j)");
        Dialect.MySqlFunctions!.Render("json_compact", ["j"]).Should().Be("json_compact(j)");

        Dialect.MySqlFunctions!.Render("next_value_for", ["'s'"]).Should().Be("next value for s");
        Dialect.MySqlFunctions!.Render("nextval", ["'s'"]).Should().Be("nextval(s)");
        Dialect.MySqlFunctions!.Render("setval", ["'s'", "42"]).Should().Be("setval(s, 42)");
        Dialect.MySqlFunctions!.Render("lastval", ["'s'"]).Should().Be("lastval(s)");
    }

    [Fact]
    public void T280_VersionGatedFunctionHooks_ShouldFollowTheConfiguredVersion()
    {
        var versioned = new MariaDbDialect(new Version(12, 3));
        string[] gated = ["months_between", "to_date", "to_number"];

        foreach (var name in gated)
        {
            versioned.MySqlFunctions!.Supports(name).Should().BeTrue(name);
            Dialect.MySqlFunctions!.Supports(name).Should().BeFalse(name);

            var unset = () => Dialect.MySqlFunctions!.Render(name, ["a", "b"]);
            unset.Should().Throw<NotSupportedException>().WithMessage($"*{name}*requires MariaDB*");
        }

        versioned.MySqlFunctions!.Render("months_between", ["a", "b"]).Should().Be("months_between(a, b)");
        versioned.MySqlFunctions!.Render("to_date", ["'s'", "'YYYY'"]).Should().Be("to_date('s', 'YYYY')");
        versioned.MySqlFunctions!.Render("to_number", ["'1'", "'9'"]).Should().Be("to_number('1', '9')");

        // The MariaDB-11.4 baseline names stay unconditional.
        Dialect.MySqlFunctions!.Supports("add_months").Should().BeTrue();
        Dialect.MySqlFunctions!.Supports("to_char").Should().BeTrue();

        var unknown = () => versioned.MySqlFunctions!.Render("no_such_function", ["a"]);
        unknown.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void T352_XxhHooks_ShouldFollowTheConfiguredVersion()
    {
        // #352: XXH3/XXH32 were added in MariaDB 13.1 — MariaDB documentation, numeric-functions
        // reference: "XXH3 was added in MariaDB 13.1." and "XXH32 was added in MariaDB 13.1."
        // (https://mariadb.com/docs/server/reference/sql-functions/numeric-functions/xxh3, …/xxh32;
        // accessed 2026-10-30). The pinned 11.4 baseline has neither, so the gate stays closed for it
        // and for an unversioned dialect instead of emitting SQL the server rejects. The source states
        // the minimum; the 11.4 half of the boundary is also observed live in the integration suite.
        var enabled = new MariaDbDialect(new Version(13, 1));
        var below = new MariaDbDialect(new Version(13, 0));

        foreach (var name in new[] { "xxh3", "xxh32" })
        {
            enabled.MySqlFunctions!.Supports(name).Should().BeTrue(name);
            below.MySqlFunctions!.Supports(name).Should().BeFalse(
                $"{name} is documented as MariaDB 13.1+ (mariadb.com/docs/server/reference/sql-functions/numeric-functions/{name})");
            new MariaDbDialect(new Version(11, 4)).MySqlFunctions!.Supports(name).Should().BeFalse(name);
            Dialect.MySqlFunctions!.Supports(name).Should().BeFalse(name);

            var unset = () => Dialect.MySqlFunctions!.Render(name, ["x"]);
            unset.Should().Throw<NotSupportedException>().WithMessage($"*{name}*requires MariaDB*");

            var tooOld = () => below.MySqlFunctions!.Render(name, ["x"]);
            tooOld.Should().Throw<NotSupportedException>().WithMessage($"*{name}*requires MariaDB*");
        }

        enabled.MySqlFunctions!.Render("xxh3", ["x"]).Should().Be("xxh3(x)");
        enabled.MySqlFunctions!.Render("xxh32", ["x"]).Should().Be("xxh32(x)");
    }
}
