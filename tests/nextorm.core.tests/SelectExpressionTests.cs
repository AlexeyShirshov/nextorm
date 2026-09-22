using System.Data;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public class SelectExpressionTests
{
    [Fact]
    public void GetDataRecordMethod_ForByteArray_ShouldReadThroughGetValue()
    {
        var column = new SelectExpression(typeof(byte[])) { PropertyName = "Data", Index = 0 };

        column.GetDataRecordMethod().Name.Should().Be(nameof(IDataRecord.GetValue));
    }

    [Fact]
    public void GetDataRecordMethod_ForUInt64_ShouldReadThroughGetFieldValue()
    {
        var column = new SelectExpression(typeof(ulong)) { PropertyName = "Big", Index = 0 };

        var method = column.GetDataRecordMethod();

        method.Name.Should().Be(nameof(DbDataReader.GetFieldValue));
        method.ReturnType.Should().Be(typeof(ulong));
        method.IsGenericMethod.Should().BeTrue();
        method.GetGenericArguments().Should().ContainSingle().Which.Should().Be(typeof(ulong));
    }

    [Fact]
    public void GetDataRecordMethod_ForNullableUInt64_ShouldReadThroughGetFieldValue()
    {
        var column = new SelectExpression(typeof(ulong?)) { PropertyName = "Big", Index = 0 };

        column.GetDataRecordMethod().ReturnType.Should().Be(typeof(ulong));
    }

    [Fact]
    public void GetDataRecordMethod_ForUnsupportedType_ShouldThrow()
    {
        var column = new SelectExpression(typeof(Uri)) { PropertyName = "Link", Index = 0 };

        var act = () => column.GetDataRecordMethod();

        act.Should().Throw<NotSupportedException>().WithMessage("*System.Uri*");
    }
}
