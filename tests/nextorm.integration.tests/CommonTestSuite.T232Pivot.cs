namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    /// <summary>
    /// V20 / E232-07: the typed PIVOT/UNPIVOT operators inherit the SQL Server-only capability of the
    /// existing PIVOT AST and renderer. SQL Server is the single supporting provider: both typed
    /// operators execute here and return the frozen fixture shape and rows. Every other provider whose
    /// dialect has no PIVOT renderer rejects both operators while the query renders.
    /// MariaDB and ClickHouse do not derive this suite and re-pin the same case in
    /// <c>T232PivotFixture.cs</c>.
    /// </summary>
    [Fact]
    public void T232TypedOperators_RespectCapability()
    {
        if (Provider.Name == "sqlserver")
        {
            T232PivotFixture.AssertTypedOperatorsSupported(_sut.DataProvider);
            return;
        }

        T232PivotFixture.AssertTypedOperatorsRejected(_sut.DataProvider, Provider.Name);
    }
}
