using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// T288 (#288) live coverage for the explicit null-safe comparison helpers, executed once per shared
/// provider subclass (SQLite, PostgreSQL, SQL Server, MySQL). ClickHouse and MariaDB run the same
/// scenario body from their own dedicated classes, because MariaDB must never be executed through MySQL.
/// <para>
/// Every scenario asserts by rows (the frozen truth table and its ordinary-comparison controls in the same
/// execution), not by SQL text; the fixture owns and drops its own tables. In-memory reference-NULL
/// behaviour is covered by <c>T288NullSafeComparisonTests</c> in <c>nextorm.core.tests</c>, because the
/// in-memory provider is unit-only by policy.
/// </para>
/// </summary>
public abstract partial class CommonTestSuite
{
    [Fact]
    public void T288_TruthTable_ShouldMatchByRowsInPredicateAndProjection()
        => T288NullSafeComparisonFixture.TruthTable(_sut.DataProvider);

    [Fact]
    public void T288_ColumnParameterAndReversedOperands_ShouldAgreeByRows()
        => T288NullSafeComparisonFixture.ColumnParameterAndReversed(_sut.DataProvider);

    [Fact]
    public void T288_CapturedNullThenValue_ShouldRebindWithoutValueDependentSql()
        => T288NullSafeComparisonFixture.CapturedNullThenValueRebinding(_sut.DataProvider);

    [Fact]
    public void T288_LiteralAndTypedNull_ShouldFoldAndReturnRows()
        => T288NullSafeComparisonFixture.LiteralNullAndDefault(_sut.DataProvider);

    [Fact]
    public void T288_OuterJoinNullExtension_ShouldStayNullSafe()
        => T288NullSafeComparisonFixture.OuterJoinNullExtension(_sut.DataProvider);

    [Fact]
    public void T288_SharedCommandAndPlanCache_ShouldNotLeakState()
        => T288NullSafeComparisonFixture.SharedCommandAndCacheReuse(_sut.DataProvider);
}
