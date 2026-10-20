using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Unit tests for <see cref="SnakeCaseNamingConvention"/>. No database or context is involved; the
/// convention is the engine-independent name translator used by the SQL providers.
/// </summary>
public class SnakeCaseNamingConventionTests
{
    [Theory]
    [InlineData("SimpleEntity", "simple_entity")]
    [InlineData("FirstName", "first_name")]
    [InlineData("OrderID", "order_id")]
    [InlineData("HTTPServer", "http_server")]
    [InlineData("ID", "id")]
    [InlineData("Url", "url")]
    [InlineData("Id", "id")]
    public void ColumnName_ShouldSnakeCaseThePropertyName(string propertyName, string expected)
    {
        SnakeCaseNamingConvention.Instance.ColumnName(propertyName).Should().Be(expected);
    }

    [Theory]
    [InlineData("SimpleEntity", "simple_entity")]
    [InlineData("HTTPServer", "http_server")]
    public void TableName_ForClass_ShouldSnakeCaseTheTypeName(string typeName, string expected)
    {
        SnakeCaseNamingConvention.Instance.TableName(typeName, isInterface: false).Should().Be(expected);
    }

    [Theory]
    [InlineData("IProduct", "product")]
    [InlineData("IBareEntity", "bare_entity")]
    [InlineData("Idle", "idle")]
    [InlineData("I", "i")]
    [InlineData("IO", "o")]
    public void TableName_ForInterface_ShouldDropTheLeadingIWhenFollowedByACapital(string typeName, string expected)
    {
        SnakeCaseNamingConvention.Instance.TableName(typeName, isInterface: true).Should().Be(expected);
    }
}
