using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// MariaDB inherits the MySQL rendering and only changes the set-operation capability flags.
/// </summary>
public class MariaDbDialectTests
{
    private static readonly ISqlDialect Dialect = MariaDbDialect.Instance;

    [Fact]
    public void InheritedMySqlHooks_ShouldBeUnchanged()
    {
        Dialect.MakeConcat(["a", "b"]).Should().Be("concat(a, b)");
        Dialect.Escape("t1").Should().Be("`t1`");
        Dialect.MakeParam("p0").Should().Be("@p0");
        Dialect.MakeCoalesce("a", "b").Should().Be("coalesce(a, b)");
        Dialect.MakeStringLength("x").Should().Be("char_length(x)");
        Dialect.MakeNow(true).Should().Be("utc_timestamp()");
    }

    [Fact]
    public void CapabilityFlags_ShouldEnableIntersectExceptAll()
    {
        Dialect.RequireSubqueryAlias.Should().BeTrue();
        Dialect.SupportsRightFullJoin.Should().BeTrue();
        Dialect.SupportsFullJoin.Should().BeFalse();
        Dialect.SupportsIntersectExceptAll.Should().BeTrue();
        Dialect.SupportsApply.Should().BeTrue();
        Dialect.SupportsQueryHints.Should().BeFalse();
        Dialect.SupportsAnyValueAggregate.Should().BeFalse();
        Dialect.MakeAggregate("any_agg").Should().Be("ANY_VALUE");
        Dialect.SupportsPercentileWindow.Should().BeTrue();
    }

    [Fact]
    public void InheritedSessionInfoHooks_ShouldBeUnchanged()
    {
        Dialect.SupportsSessionInfoFunctions.Should().BeTrue();
        Dialect.MakeSessionInfoFunction("current_user").Should().Be("current_user()");
        Dialect.MakeSessionInfoFunction("current_database").Should().Be("database()");
    }

    [Fact]
    public void UuidHooks_ShouldUseMariaDbForms()
    {
        Dialect.SupportsUuidGenerators.Should().BeTrue();
        Dialect.SupportsUuidGenerator("gen_random_uuid").Should().BeTrue();
        Dialect.SupportsUuidGenerator("uuidv7").Should().BeTrue();
        Dialect.MakeUuidGenerator("gen_random_uuid").Should().Be("uuid_v4()");
        Dialect.MakeUuidGenerator("uuidv7").Should().Be("uuid_v7()");
    }
}
