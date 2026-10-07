using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Fact]
    public void MapColumn_ForJsonNode_ShouldReadStringAndParse()
    {
        var column = new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var mapper = Expression.Lambda<Func<DbDataReader, JsonNode>>(
            RowMapperFactory.MapColumn(column, param), param).Compile();

        var reader = new RecordingJsonReader("{\"a\":1}");
        var node = mapper(reader);

        node.Should().NotBeNull();
        JsonNode.DeepEquals(node, JsonNode.Parse("{\"a\":1}")).Should().BeTrue();
        reader.RequestedTypes.Should().Equal(typeof(string));
    }

    [Fact]
    public void MapColumn_ForJsonNode_ShouldNotEmitGenericJsonNodeAccessor()
    {
        var column = new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var collector = new MethodCallCollector();
        collector.Visit(RowMapperFactory.MapColumn(column, param));

        collector.Methods.Should().Contain(m => m.DeclaringType == typeof(DbDataReader)
            && m.Name == nameof(DbDataReader.GetFieldValue)
            && m.IsGenericMethod
            && m.GetGenericArguments()[0] == typeof(string));
        collector.Methods.Should().NotContain(m => m.IsGenericMethod && m.GetGenericArguments()[0] == typeof(JsonNode));
        collector.Methods.Should().Contain(m => m.DeclaringType == typeof(JsonNode) && m.Name == nameof(JsonNode.Parse));
    }

    [Fact]
    public void MapColumn_ForJsonNode_WithSqlNull_ShouldReturnNullWithoutReadingString()
    {
        var column = new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var body = RowMapperFactory.MapColumn(column, param);

        // `JsonNode` and `JsonNode?` are the same CLR type; both annotations close over the same guard.
        var nonNullable = Expression.Lambda<Func<DbDataReader, JsonNode>>(body, param).Compile();
        var nullable = Expression.Lambda<Func<DbDataReader, JsonNode?>>(body, param).Compile();

        var reader = new RecordingJsonReader(DBNull.Value);

        nonNullable(reader).Should().BeNull();
        nullable(reader).Should().BeNull();
        reader.RequestedTypes.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2,3]")]
    [InlineData("42")]
    [InlineData("\"hello\"")]
    [InlineData("true")]
    [InlineData("null")]
    public void MapColumn_ForJsonNode_ShouldParseEveryRootKind(string json)
    {
        var column = new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var mapper = Expression.Lambda<Func<DbDataReader, JsonNode?>>(
            RowMapperFactory.MapColumn(column, param), param).Compile();

        var node = mapper(new RecordingJsonReader(json));

        JsonNode.DeepEquals(node, JsonNode.Parse(json)).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("{\"a\":}")]
    public void MapColumn_ForJsonNode_WithMalformedJson_ShouldThrowJsonException(string json)
    {
        var column = new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 };
        var param = Expression.Parameter(typeof(DbDataReader), "reader");
        var mapper = Expression.Lambda<Func<DbDataReader, JsonNode?>>(
            RowMapperFactory.MapColumn(column, param), param).Compile();

        var act = () => mapper(new RecordingJsonReader(json));

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void MapColumn_ForJsonDocumentAndJsonElement_ShouldKeepGenericAccessors()
    {
        var param = Expression.Parameter(typeof(DbDataReader), "reader");

        var documentCollector = new MethodCallCollector();
        documentCollector.Visit(RowMapperFactory.MapColumn(
            new SelectExpression(typeof(JsonDocument)) { PropertyName = "Data", Index = 0 }, param));
        documentCollector.Methods.Should().Contain(m => m.Name == nameof(DbDataReader.GetFieldValue)
            && m.IsGenericMethod
            && m.GetGenericArguments()[0] == typeof(JsonDocument));

        var elementCollector = new MethodCallCollector();
        elementCollector.Visit(RowMapperFactory.MapColumn(
            new SelectExpression(typeof(JsonElement)) { PropertyName = "Data", Index = 0 }, param));
        elementCollector.Methods.Should().Contain(m => m.Name == nameof(DbDataReader.GetFieldValue)
            && m.IsGenericMethod
            && m.GetGenericArguments()[0] == typeof(JsonElement));
    }

    [Fact]
    public void MapColumn_ForJsonNode_ShouldReuseParseMethodAcrossMaterializations()
    {
        var param = Expression.Parameter(typeof(DbDataReader), "reader");

        var first = new MethodCallCollector();
        first.Visit(RowMapperFactory.MapColumn(new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 }, param));
        var second = new MethodCallCollector();
        second.Visit(RowMapperFactory.MapColumn(new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 }, param));

        var firstParse = first.Methods.Single(m => m.DeclaringType == typeof(JsonNode) && m.Name == nameof(JsonNode.Parse));
        var secondParse = second.Methods.Single(m => m.DeclaringType == typeof(JsonNode) && m.Name == nameof(JsonNode.Parse));
        ReferenceEquals(firstParse, secondParse).Should().BeTrue();

        var mapper = Expression.Lambda<Func<DbDataReader, JsonNode?>>(
            RowMapperFactory.MapColumn(new SelectExpression(typeof(JsonNode)) { PropertyName = "Data", Index = 0 }, param), param).Compile();
        for (var i = 0; i < 3; i++)
            JsonNode.DeepEquals(mapper(new RecordingJsonReader("{\"a\":1}")), JsonNode.Parse("{\"a\":1}")).Should().BeTrue();
    }

    private sealed class MethodCallCollector : ExpressionVisitor
    {
        public List<MethodInfo> Methods { get; } = [];

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            Methods.Add(node.Method);
            return base.VisitMethodCall(node);
        }
    }

    private sealed class RecordingJsonReader(object? value) : SingleColumnReader(value)
    {
        public List<Type> RequestedTypes { get; } = [];

        public override T GetFieldValue<T>(int ordinal)
        {
            RequestedTypes.Add(typeof(T));

            if (typeof(T) != typeof(string))
                throw new InvalidCastException($"JSON reader only supports GetFieldValue<string>; requested {typeof(T)}.");

            return base.GetFieldValue<T>(ordinal);
        }
    }

    private class SingleColumnReader(object? value) : DbDataReader
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
