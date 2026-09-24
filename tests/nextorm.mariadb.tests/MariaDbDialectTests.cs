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
}
