namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    /// <summary>
    /// V20 / E232-07: the typed PIVOT/UNPIVOT operators inherit the SQL Server-only capability of the
    /// existing PIVOT AST and renderer, so every provider whose dialect has no PIVOT renderer rejects
    /// both operators while the query renders. SQL Server is the single supporting provider and is
    /// skipped here — its typed execution path is asserted by the typed SQL Server integration facts.
    /// MariaDB and ClickHouse do not derive this suite and re-pin the same case in
    /// <c>T232PivotFixture.cs</c>.
    /// </summary>
    [Fact]
    public void T232TypedOperators_RespectCapability()
    {
        Assert.SkipWhen(
            Provider.Name == "sqlserver",
            "SQL Server supports PIVOT/UNPIVOT; the typed SQL Server execution path is covered by the T232 integration facts.");

        T232PivotFixture.AssertTypedOperatorsRejected(_sut.DataProvider, Provider.Name);
    }
}
