using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClickHouse.Driver.ADO.Parameters;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Provider-local coverage for the native ClickHouse <c>JSON</c> mapping (issue #128): the bare
/// <see cref="JsonObject"/> parameter typing, the buffered projection accessor, the defensive
/// <c>DBNull</c> handling and the base delegation for every other column type. The tests never open
/// a connection, so they run on every build.
/// </summary>
public class ClickHouseJsonObjectMappingTests
{
    private static readonly ParameterExpression Record = Expression.Parameter(typeof(IDataRecord), "record");

    [Fact]
    public void CreateParam_JsonObject_ShouldBindNativeJsonType()
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();
        var doc = new JsonObject { ["name"] = "alice", ["nested"] = new JsonObject { ["x"] = 1 } };

        var parameter = (ClickHouseDbParameter)ctx.CreateParam("@doc", doc);

        parameter.ParameterName.Should().Be("doc");
        parameter.Value.Should().BeSameAs(doc);
        parameter.ClickHouseType.Should().Be("JSON");
    }

    [Theory]
    [InlineData("plain text")]
    [InlineData(42)]
    public void CreateParam_NonJsonObject_ShouldNotForceJsonType(object value)
    {
        using var ctx = ClickHouseTestContext.CreateClickHouse();

        var parameter = (ClickHouseDbParameter)ctx.CreateParam("@p", value);

        // The native JSON branch must not leak to a string/int parameter, otherwise the type would bind
        // as JSON instead of String/Int32.
        parameter.ClickHouseType.Should().BeNullOrEmpty();
    }

    [Fact]
    public void BareJsonObjectProjection_ShouldRenderAndBuildTheRowMapper()
    {
        using var ctx = ClickHouseTestContext.Create();

        // A bare JsonObject property (no [JsonColumn] converter) inside a named projection: preparing the
        // command renders the column and builds the buffered row mapper, so the provider accessor is
        // exercised rather than only the SQL text.
        var command = ctx.From<IJsonObjectEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { x.Id, x.Doc });

        var sql = SqlOf(ctx, command);

        sql.Should().Contain("from json_entity");
        sql.Should().Contain("doc");
    }

    [Fact]
    public void BareTopLevelJsonObjectScalarProjection_ShouldFailClosedWithNamedLimitation()
    {
        // Retained limitation (docs/providers/clickhouse.md, docs/advanced/limitations.md): a bare
        // top-level scalar JsonObject projection is not handled by the core projection classifier —
        // project through a named shape such as Select(x => new { x.Doc }) or a DTO. Without the guard
        // the empty select list reached the row materializer, which failed with an opaque
        // "Incorrect number of arguments for constructor" (System.ArgumentException). It must now fail
        // closed at preparation with a clear message naming the shape.
        using var ctx = ClickHouseTestContext.Create();

        var command = ctx.From<IJsonObjectEntity>()
            .Where(x => x.Id == 1)
            .Select(x => x.Doc);

        var act = () => SqlOf(ctx, command);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*bare top-level scalar*JsonObject*");
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
                command, createEnumerator: false, storeInCache: false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    [Fact]
    public void JsonObjectProjection_NestedDocument_ShouldMaterializeThroughGetFieldValue()
    {
        var doc = new JsonObject
        {
            ["name"] = "alice",
            ["nested"] = new JsonObject { ["x"] = 1 },
        };

        var result = Invoke(MapColumn(typeof(JsonObject)), doc);

        result.Should().BeSameAs(doc);
    }

    [Fact]
    public void JsonObjectProjection_EmptyObject_ShouldStayNonNullObject()
    {
        var doc = new JsonObject();

        var result = Invoke(MapColumn(typeof(JsonObject)), doc);

        result.Should().NotBeNull();
        result.Should().BeSameAs(doc);
    }

    [Fact]
    public void JsonObjectProjection_SqlNull_ShouldMaterializeNull()
    {
        var result = Invoke(MapColumn(typeof(JsonObject)), DBNull.Value);

        result.Should().BeNull();
    }

    [Fact]
    public void NonJsonObjectColumn_ShouldDelegateToBaseMapping()
    {
        // A bare string column must still read through the base GetString accessor; if the JsonObject
        // override swallowed other types the reader's GetFieldValue<string> would not be called.
        var result = Invoke(MapColumn(typeof(string)), "hello");

        result.Should().Be("hello");
    }

    [Fact]
    public void BareJsonDocumentProjection_ShouldAdaptFromNativeJsonObject()
    {
        var doc = new JsonObject
        {
            ["name"] = "alice",
            ["nested"] = new JsonObject { ["x"] = 1 },
        };

        var result = Invoke(MapColumn(typeof(JsonDocument)), doc).Should().BeOfType<JsonDocument>().Subject;

        result.RootElement.GetProperty("name").GetString().Should().Be("alice");
        result.RootElement.GetProperty("nested").GetProperty("x").GetInt32().Should().Be(1);
    }

    [Fact]
    public void BareJsonElementProjection_ShouldAdaptFromNativeJsonObject()
    {
        var doc = new JsonObject { ["name"] = "alice" };

        var result = (JsonElement)Invoke(MapColumn(typeof(JsonElement)), doc)!;

        result.ValueKind.Should().Be(JsonValueKind.Object);
        result.GetProperty("name").GetString().Should().Be("alice");
    }

    [Fact]
    public void BareJsonDocumentProjection_SqlNull_ShouldMaterializeNull()
    {
        var result = Invoke(MapColumn(typeof(JsonDocument)), DBNull.Value);

        result.Should().BeNull();
    }

    [Fact]
    public void BareJsonElementProjection_SqlNull_ShouldBeUndefined()
    {
        // JsonElement is a value type, so SQL NULL cannot be null; it stays distinct from {} as the
        // default (Undefined) element.
        var result = (JsonElement)Invoke(MapColumn(typeof(JsonElement)), DBNull.Value)!;

        result.ValueKind.Should().Be(JsonValueKind.Undefined);
    }

    [Fact]
    public void BareNullableJsonElementProjection_SqlNull_ShouldMaterializeNull()
    {
        // JsonElement? is a nullable value type: a SQL NULL must be a true null, not default(JsonElement)
        // (ValueKind Undefined) surfaced as a present value.
        var column = new SelectExpression(typeof(JsonElement?)) { Index = 0, PropertyName = "Doc" };
        var body = ClickHouseTestContext.CreateClickHouse().MapColumnExpression(column, Record);

        body.Type.Should().Be(typeof(JsonElement?));

        var result = Invoke(body, DBNull.Value);

        result.Should().BeNull();
    }

    [Fact]
    public void BareNullableJsonElementProjection_Value_ShouldAdapt()
    {
        var result = (JsonElement)Invoke(MapColumn(typeof(JsonElement?)), new JsonObject { ["name"] = "alice" })!;

        result.ValueKind.Should().Be(JsonValueKind.Object);
        result.GetProperty("name").GetString().Should().Be("alice");
    }

    private static Expression MapColumn(Type type)
    {
        var column = new SelectExpression(type) { Index = 0, PropertyName = "Doc" };
        return ClickHouseTestContext.CreateClickHouse().MapColumnExpression(column, Record);
    }

    private static object? Invoke(Expression body, object? value)
    {
        var lambda = Expression.Lambda(body, Record).Compile();
        return lambda.DynamicInvoke(new SingleColumnReader(value));
    }

    /// <summary>
    /// A one-column reader whose typed accessors only accept the shape the provider has to ask for: the
    /// native-JSON accessor is <c>GetFieldValue&lt;JsonObject&gt;</c> (with the storage null-checked
    /// first), while a non-JSON column reaches the base <see cref="IDataRecord.GetString"/> accessor.
    /// </summary>
    private sealed class SingleColumnReader(object? value) : DbDataReader
    {
        public override int FieldCount => 1;

        public override bool HasRows => true;

        public override bool IsClosed => false;

        public override int RecordsAffected => -1;

        public override int Depth => 0;

        public override int VisibleFieldCount => 1;

        public override object this[int ordinal] => throw new NotSupportedException();

        public override object this[string name] => throw new NotSupportedException();

        public override object GetValue(int ordinal) => value!;

        public override bool IsDBNull(int ordinal) => value is null or DBNull;

        public override Type GetFieldType(int ordinal) => value?.GetType() ?? typeof(object);

        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

        public override string GetName(int ordinal) => "Doc";

        public override int GetOrdinal(string name) => 0;

        public override bool Read() => throw new NotSupportedException();

        public override bool NextResult() => throw new NotSupportedException();

        public override IEnumerator GetEnumerator() => throw new NotSupportedException();

        public override int GetValues(object[] values) => throw new NotSupportedException();

        public override T GetFieldValue<T>(int ordinal)
            => value is T typed ? typed : throw new InvalidCastException($"GetFieldValue<{typeof(T).Name}> called for {value?.GetType().Name ?? "null"}.");

        public override bool GetBoolean(int ordinal) => Require<bool>(ordinal);

        public override byte GetByte(int ordinal) => Require<byte>(ordinal);

        public override short GetInt16(int ordinal) => Require<short>(ordinal);

        public override int GetInt32(int ordinal) => Require<int>(ordinal);

        public override long GetInt64(int ordinal) => Require<long>(ordinal);

        public override float GetFloat(int ordinal) => Require<float>(ordinal);

        public override double GetDouble(int ordinal) => Require<double>(ordinal);

        public override decimal GetDecimal(int ordinal) => Require<decimal>(ordinal);

        public override string GetString(int ordinal) => Require<string>(ordinal);

        public override Guid GetGuid(int ordinal) => Require<Guid>(ordinal);

        public override DateTime GetDateTime(int ordinal) => Require<DateTime>(ordinal);

        public override char GetChar(int ordinal) => Require<char>(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) => throw new NotSupportedException();

        private T Require<T>(int ordinal)
        {
            if (ordinal != 0)
                throw new IndexOutOfRangeException();

            if (value is not T typed)
                throw new InvalidCastException($"Get{typeof(T).Name} called for {value?.GetType().Name ?? "null"}.");

            return typed;
        }
    }
}

[SqlTable("json_entity")]
public interface IJsonObjectEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("doc")]
    JsonObject Doc { get; set; }
}
