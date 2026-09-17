using FluentAssertions;

namespace nextorm.core.tests;

public class SqlTableFunctionAttributeTests
{
    [Fact]
    public void NoArgConstructor_ShouldLeaveNameAndSchemaUnset()
    {
        var attribute = new SqlTableFunctionAttribute();

        attribute.Name.Should().BeNull();
        attribute.Schema.Should().BeNull();
    }

    [Fact]
    public void NameConstructor_ShouldSetName()
    {
        var attribute = new SqlTableFunctionAttribute("all_rows");

        attribute.Name.Should().Be("all_rows");
    }

    [Fact]
    public void Schema_ShouldBeSettable()
    {
        var attribute = new SqlTableFunctionAttribute("rows_between")
        {
            Schema = "app"
        };

        attribute.Name.Should().Be("rows_between");
        attribute.Schema.Should().Be("app");
    }
}
