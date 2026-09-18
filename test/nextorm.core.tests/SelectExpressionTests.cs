using System.Data;
using FluentAssertions;
using nextorm.core;

namespace nextorm.core.tests;

public class SelectExpressionTests
{
    [Fact]
    public void GetDataRecordMethod_ForByteArray_ShouldReadThroughGetValue()
    {
        var column = new SelectExpression(typeof(byte[])) { PropertyName = "Data", Index = 0 };

        column.GetDataRecordMethod().Name.Should().Be(nameof(IDataRecord.GetValue));
    }

    [Fact]
    public void GetDataRecordMethod_ForUnsupportedType_ShouldThrow()
    {
        var column = new SelectExpression(typeof(Uri)) { PropertyName = "Link", Index = 0 };

        var act = () => column.GetDataRecordMethod();

        act.Should().Throw<NotSupportedException>().WithMessage("*System.Uri*");
    }
}
