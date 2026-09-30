using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

public class SelectExpressionTests
{
    [Fact]
    public void GetDataRecordMethod_ForByteArray_ShouldReadThroughGetFieldValue()
    {
        var column = new SelectExpression(typeof(byte[])) { PropertyName = "Data", Index = 0 };

        var method = column.GetDataRecordMethod();

        method.Name.Should().Be(nameof(DbDataReader.GetFieldValue));
        method.ReturnType.Should().Be(typeof(byte[]));
        method.IsGenericMethod.Should().BeTrue();
        method.GetGenericArguments().Should().ContainSingle().Which.Should().Be(typeof(byte[]));
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
    public void GetDataRecordMethod_ForUInt32_ShouldReadThroughGetFieldValue()
    {
        var column = new SelectExpression(typeof(uint)) { PropertyName = "Revision", Index = 0 };

        var method = column.GetDataRecordMethod();

        method.Name.Should().Be(nameof(DbDataReader.GetFieldValue));
        method.ReturnType.Should().Be(typeof(uint));
        method.IsGenericMethod.Should().BeTrue();
        method.GetGenericArguments().Should().ContainSingle().Which.Should().Be(typeof(uint));
    }

    [Fact]
    public void GetDataRecordMethod_ForNullableUInt32_ShouldReadThroughGetFieldValue()
    {
        var column = new SelectExpression(typeof(uint?)) { PropertyName = "Revision", Index = 0 };

        column.GetDataRecordMethod().ReturnType.Should().Be(typeof(uint));
    }

    [Fact]
    public void MapColumn_ForNullableUInt32_ShouldMaterializeDbNullAsNull()
    {
        var column = new SelectExpression(typeof(uint?)) { PropertyName = "Revision", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var body = RowMapperFactory.MapColumn(column, param);
        var mapper = Expression.Lambda<Func<DbDataReader, uint?>>(body, param).Compile();

        mapper(new SingleColumnReader(DBNull.Value)).Should().BeNull();
        mapper(new SingleColumnReader(null)).Should().BeNull();
        mapper(new SingleColumnReader((uint)7)).Should().Be((uint?)7);
    }

    [Fact]
    public void GetDataRecordMethod_ForUnsupportedType_ShouldThrow()
    {
        var column = new SelectExpression(typeof(Uri)) { PropertyName = "Link", Index = 0 };

        var act = () => column.GetDataRecordMethod();

        act.Should().Throw<NotSupportedException>().WithMessage("*System.Uri*");
    }

    [Fact]
    public void GetDataRecordMethod_ForNullableProviderType_ShouldStripNullable()
    {
        var column = new SelectExpression(typeof(long)) { PropertyName = "Value", Index = 0, ProviderType = typeof(long?) };

        column.GetDataRecordMethod().Name.Should().Be(nameof(IDataRecord.GetInt64));
    }

    [Fact]
    public void GetDataRecordMethod_ShouldPreferExplicitProviderType()
    {
        var column = new SelectExpression(typeof(ProbePoco))
        {
            PropertyName = "Data",
            Index = 0,
            Converter = new JsonColumnConverter<ProbePoco, string>(),
            ProviderType = typeof(JsonElement),
        };

        column.GetDataRecordMethod().ReturnType.Should().Be(typeof(JsonElement));
    }

    private sealed class SingleColumnReader(object? value) : DbDataReader
    {
        public override int FieldCount => 1;

        public override int Depth => 0;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(0);

        public override bool IsDBNull(int ordinal) => value is null or DBNull;

        public override object GetValue(int ordinal) => value ?? DBNull.Value;

        public override T GetFieldValue<T>(int ordinal) => (T)GetValue(ordinal);

        public override string GetName(int ordinal) => "Revision";

        public override int GetOrdinal(string name) => 0;

        public override string GetDataTypeName(int ordinal) => "uint4";

        public override Type GetFieldType(int ordinal) => typeof(uint);

        public override int GetValues(object[] values)
        {
            if (values is { Length: > 0 })
                values[0] = GetValue(0);

            return 1;
        }

        public override IEnumerator GetEnumerator()
        {
            yield return this;
        }

        public override bool NextResult() => false;

        public override bool Read() => false;

        public override bool GetBoolean(int ordinal) => throw new NotSupportedException();

        public override byte GetByte(int ordinal) => throw new NotSupportedException();

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override char GetChar(int ordinal) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();

        public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();

        public override double GetDouble(int ordinal) => throw new NotSupportedException();

        public override float GetFloat(int ordinal) => throw new NotSupportedException();

        public override Guid GetGuid(int ordinal) => throw new NotSupportedException();

        public override short GetInt16(int ordinal) => throw new NotSupportedException();

        public override int GetInt32(int ordinal) => throw new NotSupportedException();

        public override long GetInt64(int ordinal) => throw new NotSupportedException();

        public override string GetString(int ordinal) => throw new NotSupportedException();
    }
}
