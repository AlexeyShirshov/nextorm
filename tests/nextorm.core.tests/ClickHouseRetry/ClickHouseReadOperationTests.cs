using FluentAssertions;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Core.Tests.ClickHouseRetry;

/// <summary>
/// Eligibility guard for the retried operation (T328 AC10, V09): only the four read categories can be
/// audited; streamed reads and every mutating/unknown-effect form are rejected by construction.
/// </summary>
public sealed class ClickHouseReadOperationTests
{
    // The category is passed as its numeric value because the shared enum is internal to this test
    // assembly and cannot appear in a public test method signature.
    [Theory]
    [InlineData((int)ClickHouseReadCategory.PointRead)]
    [InlineData((int)ClickHouseReadCategory.MaterialisedListRead)]
    [InlineData((int)ClickHouseReadCategory.ScalarAggregate)]
    public void Audited_ShouldAcceptAReadOnlyCategory(int categoryValue)
    {
        var category = (ClickHouseReadCategory)categoryValue;

        var operation = ClickHouseReadOperation.Audited("test.identity", "operation.identity", category);

        operation.TestIdentity.Should().Be("test.identity");
        operation.OperationIdentity.Should().Be("operation.identity");
        operation.Category.Should().Be(category);
    }

    [Theory]
    [InlineData((int)ClickHouseReadCategory.StreamedRead)]
    [InlineData((int)ClickHouseReadCategory.Write)]
    [InlineData((int)ClickHouseReadCategory.Ddl)]
    [InlineData((int)ClickHouseReadCategory.FixtureMutation)]
    [InlineData((int)ClickHouseReadCategory.UnknownEffect)]
    public void Audited_ShouldRejectANonReadOnlyCategory_NamingIt(int categoryValue)
    {
        var category = (ClickHouseReadCategory)categoryValue;

        var act = () => ClickHouseReadOperation.Audited("test.identity", "operation.identity", category);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage($"*{category}*")
            .And.ParamName.Should().Be("category");
    }

    [Theory]
    [InlineData("", "operation.identity")]
    [InlineData("   ", "operation.identity")]
    [InlineData("test.identity", "")]
    [InlineData("test.identity", "\t")]
    public void Audited_ShouldRejectAnEmptyIdentity(string testIdentity, string operationIdentity)
    {
        var act = () => ClickHouseReadOperation.Audited(testIdentity, operationIdentity, ClickHouseReadCategory.PointRead);

        act.Should().Throw<ArgumentException>();
    }
}
