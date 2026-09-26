using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Validation and shape of the <see cref="ProcedureParameter.Table{T}(string, IEnumerable{T})"/>
/// factories and of the <see cref="TableParameterValue"/> carrier.
/// </summary>
public class ProcedureParameterTableTests
{
    [Fact]
    public void Table_WithRows_ShouldCarryInputCarrier()
    {
        var parameter = ProcedureParameter.Table("p", new[] { 1, 2, 3 });

        parameter.Direction.Should().Be(System.Data.ParameterDirection.Input);
        parameter.TypeName.Should().BeNull();

        var value = parameter.Value.Should().BeAssignableTo<TableParameterValue>().Subject;
        value.RowType.Should().Be(typeof(int));
        value.Rows.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Table_WithTypeName_ShouldSetTypeName()
    {
        var parameter = ProcedureParameter.Table("p", "dbo.MyType", new[] { "a" });

        parameter.TypeName.Should().Be("dbo.MyType");
        parameter.Value.Should().BeAssignableTo<TableParameterValue>();
    }

    [Fact]
    public void Table_NullOrBlankName_ShouldThrow()
    {
        Action nullName = () => ProcedureParameter.Table(null!, new[] { 1 });
        Action blankName = () => ProcedureParameter.Table("   ", new[] { 1 });

        nullName.Should().Throw<ArgumentException>();
        blankName.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Table_NullRows_ShouldThrow()
    {
        Action act = () => ProcedureParameter.Table<int>("p", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Table_BlankTypeName_ShouldThrow()
    {
        Action nullType = () => ProcedureParameter.Table("p", null!, new[] { 1 });
        Action blankType = () => ProcedureParameter.Table("p", "  ", new[] { 1 });

        nullType.Should().Throw<ArgumentException>();
        blankType.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Table_NullRowsWithTypeName_ShouldThrow()
    {
        Action act = () => ProcedureParameter.Table<int>("p", "dbo.MyType", null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
