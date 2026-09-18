using FluentAssertions;
using nextorm.core;
using nextorm.mariadb;

namespace nextorm.mariadb.tests;

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
    }
}
