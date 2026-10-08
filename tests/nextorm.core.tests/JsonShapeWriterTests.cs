using System.Data;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Focused D176.3 coverage for the recursive JSON shape writer compiled from a captured
/// <see cref="JsonShapeNode"/> descriptor: nested object writing, per-object scoped name validation,
/// construction/outer-join presence, Base64 precedence inside arrays and the fail-closed array guards.
/// The record is a hand-written <see cref="IDataRecord"/> so ordinal binding, presence and element
/// classification are exercised without a database. The comprehensive boundary suite is D176.4.
/// </summary>
public class JsonShapeWriterTests
{
    private sealed class FakeRecord : IDataRecord
    {
        private readonly object?[] _values;
        private readonly Type[] _types;
        private readonly int _throwOnOrdinal;

        public FakeRecord(object?[] values, Type[] types)
            : this(values, types, throwOnOrdinal: -1)
        {
        }

        public FakeRecord(object?[] values, Type[] types, int throwOnOrdinal)
        {
            _values = values;
            _types = types;
            _throwOnOrdinal = throwOnOrdinal;
        }

        public int FieldCount => _values.Length;

        public object this[int i] => GetValue(i);

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => (bool)_values[i]!;

        public byte GetByte(int i) => (byte)_values[i]!;

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => _types[i].Name;

        public DateTime GetDateTime(int i) => (DateTime)_values[i]!;

        public decimal GetDecimal(int i) => (decimal)_values[i]!;

        public double GetDouble(int i) => (double)_values[i]!;

        public Type GetFieldType(int i) => _types[i];

        public float GetFloat(int i) => (float)_values[i]!;

        public Guid GetGuid(int i) => (Guid)_values[i]!;

        public short GetInt16(int i) => (short)_values[i]!;

        public int GetInt32(int i)
        {
            if (i == _throwOnOrdinal)
                throw new InvalidOperationException("reader failed mid-read");
            return (int)_values[i]!;
        }

        public long GetInt64(int i) => (long)_values[i]!;

        public string GetName(int i) => $"c{i}";

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => (string)_values[i]!;

        public object GetValue(int i) => _values[i] ?? DBNull.Value;

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _values.Length);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);
            return count;
        }

        public bool IsDBNull(int i) => _values[i] is null;
    }

    private static JsonShapeNode Scalar(string? name, Type type, int ordinal, bool nullable)
        => JsonShapeNode.Scalar(name, type, new JsonShapeBinding(ordinal, nullable, defaultOnNull: false));

    private static JsonShapeNode Obj(string? name, JsonShapeNode[] members, JsonShapePresence presence)
        => JsonShapeNode.Object(name, typeof(object), members, presence, slot: null, member: null);

    private static JsonShapeNode Element(Type elementType)
    {
        var valueType = Nullable.GetUnderlyingType(elementType) ?? elementType;
        return valueType != typeof(byte[]) && valueType.IsArray
            ? JsonShapeNode.Array(null, elementType, Element(valueType.GetElementType()!), binding: null)
            : JsonShapeNode.Scalar(null, elementType, binding: null);
    }

    private static JsonShapeNode Arr(string name, Type arrayType, int ordinal, bool nullable)
        => JsonShapeNode.Array(name, arrayType, Element(arrayType.GetElementType()!), new JsonShapeBinding(ordinal, nullable, defaultOnNull: false));

    private static JsonRowWriter BuildWriter(JsonShapeNode shape)
    {
        var count = ColumnCountFor(shape);
        var columns = new SelectExpression[count];
        for (var i = 0; i < count; i++)
            columns[i] = new SelectExpression(typeof(int)) { Index = i, PropertyName = $"c{i}" };

        var plan = JsonShapePlan.Build(
            columns,
            oneColumn: false,
            new JsonStreamOptions(),
            shape);
        return JsonRowWriterFactory.Build(plan);
    }

    // The shape descriptor is validated against the prepared selection; this mirrors the production
    // invariant that the lowered column count covers every referenced ordinal.
    private static int ColumnCountFor(JsonShapeNode node)
    {
        var max = -1;

        void Visit(JsonShapeNode current)
        {
            if (current.Binding is { } binding)
                max = Math.Max(max, binding.Ordinal);

            if (current.Presence.Kind == JsonShapePresenceKind.AnyColumnNotNull)
                foreach (var ordinal in current.Presence.Ordinals)
                    max = Math.Max(max, ordinal);

            foreach (var member in current.Members)
                Visit(member);

            if (current.Element is not null)
                Visit(current.Element);
        }

        Visit(node);
        return max + 1;
    }

    private static string Write(JsonRowWriter rowWriter, FakeRecord record)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            rowWriter(record, writer);
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void NestedObject_ShouldWriteResolvedMemberNames()
    {
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Child", [Scalar("Name", typeof(string), 1, true)], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, "a"], [typeof(int), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Id\":1,\"Child\":{\"Name\":\"a\"}}]");
    }

    [Fact]
    public void SameNameInDifferentScopes_ShouldBeAccepted()
    {
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Child", [Scalar("Id", typeof(int), 1, false)], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, 2], [typeof(int), typeof(int)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Id\":1,\"Child\":{\"Id\":2}}]");
    }

    [Fact]
    public void DuplicateNameWithinOneScope_ShouldThrowBeforeWriting()
    {
        var shape = Obj(null,
            [Scalar("X", typeof(int), 0, false), Scalar("X", typeof(int), 1, false)],
            JsonShapePresence.Always);

        var act = () => BuildWriter(shape);

        act.Should().Throw<NotSupportedException>().WithMessage("*Duplicate JSON property name 'X'*");
    }

    [Fact]
    public void AllNullConstructedObject_ShouldStillWriteObject()
    {
        var shape = Obj(null,
            [Scalar("A", typeof(string), 0, true), Scalar("B", typeof(int?), 1, true)],
            JsonShapePresence.Always);
        var record = new FakeRecord([null, null], [typeof(string), typeof(int)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"A\":null,\"B\":null}]");
    }

    [Fact]
    public void AbsentJoinedSlot_ShouldWriteNull()
    {
        var shape = Obj(null,
            [
                Obj("Left", [Scalar("Id", typeof(int), 0, false)], JsonShapePresence.Always),
                Obj("Right", [Scalar("Id", typeof(int), 1, true), Scalar("Name", typeof(string), 2, true)],
                    JsonShapePresence.AnyColumnNotNull([1, 2])),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, null, null], [typeof(int), typeof(int), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Left\":{\"Id\":1},\"Right\":null}]");
    }

    [Fact]
    public void PresentJoinedSlot_ShouldWriteObject()
    {
        var shape = Obj(null,
            [
                Obj("Left", [Scalar("Id", typeof(int), 0, false)], JsonShapePresence.Always),
                Obj("Right", [Scalar("Id", typeof(int), 1, false), Scalar("Name", typeof(string), 2, true)],
                    JsonShapePresence.AnyColumnNotNull([1, 2])),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, 10, "c"], [typeof(int), typeof(int), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Left\":{\"Id\":1},\"Right\":{\"Id\":10,\"Name\":\"c\"}}]");
    }

    [Fact]
    public void ByteArrayMember_ShouldBeBase64()
    {
        var shape = Obj(null, [Scalar("Data", typeof(byte[]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new byte[] { 1, 2, 3 }], [typeof(byte[])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Data\":\"AQID\"}]");
    }

    [Fact]
    public void IntArray_ShouldRecurseElements()
    {
        var shape = Obj(null, [Arr("Tags", typeof(int[]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new[] { 1, 2 }], [typeof(int[])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Tags\":[1,2]}]");
    }

    [Fact]
    public void NullAndEmptyArray_ShouldWriteNullAndEmpty()
    {
        var shape = Obj(null, [Arr("Tags", typeof(int[]), 0, true)], JsonShapePresence.Always);

        Write(BuildWriter(shape), new FakeRecord([null], [typeof(int[])])).Should().Be("[{\"Tags\":null}]");
        Write(BuildWriter(shape), new FakeRecord([Array.Empty<int>()], [typeof(int[])])).Should().Be("[{\"Tags\":[]}]");
    }

    [Fact]
    public void JaggedIntArray_ShouldRecurseNestedArrays()
    {
        var shape = Obj(null, [Arr("Matrix", typeof(int[][]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new[] { new[] { 1, 2 }, new[] { 3 } }], [typeof(int[][])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Matrix\":[[1,2],[3]]}]");
    }

    [Fact]
    public void JaggedByteArray_ShouldBeBase64Recursively()
    {
        var shape = Obj(null, [Arr("Matrix", typeof(byte[][]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new[] { new byte[] { 1, 2, 3 }, new byte[] { 4, 5, 6 } }], [typeof(byte[][])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Matrix\":[\"AQID\",\"BAUG\"]}]");
    }

    [Fact]
    public void NullElementInsideArray_ShouldWriteNull()
    {
        var shape = Obj(null, [Arr("Names", typeof(string[]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new string?[] { "a", null }], [typeof(string[])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Names\":[\"a\",null]}]");
    }

    [Fact]
    public void MultidimensionalArray_ShouldThrowBeforeWriting()
    {
        var shape = Obj(null, [Arr("Grid", typeof(int[,]), 0, true)], JsonShapePresence.Always);

        var act = () => BuildWriter(shape);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void UnsupportedElementType_ShouldThrowBeforeWriting()
    {
        var shape = Obj(null, [Arr("Times", typeof(DateTimeOffset[]), 0, true)], JsonShapePresence.Always);

        var act = () => BuildWriter(shape);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CollectionElementType_ShouldThrowBeforeWriting()
    {
        var shape = Obj(null, [Arr("Items", typeof(List<int>[]), 0, true)], JsonShapePresence.Always);

        var act = () => BuildWriter(shape);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ExcessiveDepth_ShouldThrowBeforeWriting()
    {
        JsonShapeNode node = Scalar("Leaf", typeof(int), 0, false);
        for (var i = 0; i < 70; i++)
            node = Obj(i == 69 ? null : $"Level{i}", [node], JsonShapePresence.Always);

        var act = () => BuildWriter(node);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NamingPolicy_ShouldApplyPerObjectScope()
    {
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Child", [Scalar("Name", typeof(string), 1, true)], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var plan = JsonShapePlan.Build(
            [new SelectExpression(typeof(int)) { Index = 0, PropertyName = "c0" },
             new SelectExpression(typeof(string)) { Index = 1, PropertyName = "c1" }],
            oneColumn: false,
            new JsonStreamOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
            shape);
        var record = new FakeRecord([1, "a"], [typeof(int), typeof(string)]);

        Write(JsonRowWriterFactory.Build(plan), record).Should().Be("[{\"id\":1,\"child\":{\"name\":\"a\"}}]");
    }

    // --- D176.4 boundary sweep: direct recursive-writer coverage. ---

    private static JsonRowWriter BuildFlatWriter(SelectExpression[] columns, bool oneColumn, JsonStreamOptions? options = null)
    {
        var plan = JsonShapePlan.Build(columns, oneColumn, options ?? new JsonStreamOptions());
        return JsonRowWriterFactory.Build(plan);
    }

    private static SelectExpression Column(Type type, int ordinal, string? name)
        => new(type) { Index = ordinal, PropertyName = name };

    [Fact]
    public void FlatObject_NullableAndDefaultLeaves_ShouldFollowPhase1()
    {
        // Distinct sentinel values per ordinal catch a wrong-ordinal binding; the int? NULL stays null,
        // the non-nullable *OrDefault int maps SQL NULL to default(T).
        var columns = new[]
        {
            Column(typeof(int), 0, "Id"),
            Column(typeof(int?), 1, "Maybe"),
            Column(typeof(string), 2, "Name"),
        };
        columns[2].DefaultOnNull = true;
        var record = new FakeRecord([1, null, null], [typeof(int), typeof(int), typeof(string)]);

        Write(BuildFlatWriter(columns, oneColumn: false), record)
            .Should().Be("[{\"Id\":1,\"Maybe\":null,\"Name\":null}]");
    }

    [Fact]
    public void FlatScalar_NullAndDefault_ShouldFollowPhase1()
    {
        var column = Column(typeof(int), 0, null);
        column.DefaultOnNull = true;

        // A SQL NULL on a non-nullable *OrDefault scalar emits default(T), not null.
        Write(BuildFlatWriter([column], oneColumn: true), new FakeRecord([null], [typeof(int)]))
            .Should().Be("[0]");

        var nullable = Column(typeof(int?), 0, null);
        Write(BuildFlatWriter([nullable], oneColumn: true), new FakeRecord([null], [typeof(int)]))
            .Should().Be("[null]");
    }

    [Fact]
    public void NestedObject_MultipleLevels_ShouldPreserveBoundaries()
    {
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Outer",
                    [
                        Scalar("Name", typeof(string), 1, true),
                        Obj("Inner", [Scalar("Amount", typeof(decimal), 2, false)], JsonShapePresence.Always),
                    ],
                    JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, "a", 12.5m], [typeof(int), typeof(string), typeof(decimal)]);

        Write(BuildWriter(shape), record)
            .Should().Be("[{\"Id\":1,\"Outer\":{\"Name\":\"a\",\"Inner\":{\"Amount\":12.5}}}]");
    }

    [Fact]
    public void RepeatedSiblingNames_InSeparateScopes_ShouldBeAccepted()
    {
        var shape = Obj(null,
            [
                Obj("Left", [Scalar("Name", typeof(string), 0, true)], JsonShapePresence.Always),
                Obj("Right", [Scalar("Name", typeof(string), 1, true)], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord(["l", "r"], [typeof(string), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Left\":{\"Name\":\"l\"},\"Right\":{\"Name\":\"r\"}}]");
    }

    [Fact]
    public void AllNullChildren_ShouldStillWriteNestedObjects()
    {
        var shape = Obj(null,
            [
                Obj("A", [Scalar("X", typeof(string), 0, true)], JsonShapePresence.Always),
                Obj("B", [Scalar("Y", typeof(int?), 1, true)], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([null, null], [typeof(string), typeof(int)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"A\":{\"X\":null},\"B\":{\"Y\":null}}]");
    }

    [Fact]
    public void MemberOrderDifferentFromOrdinal_ShouldBindDeclaredOrdinals()
    {
        // Members are declared in B, A, C order while their ordinals are 2, 0, 1; sentinel values make
        // a wrong-ordinal binding observable.
        var shape = Obj(null,
            [
                Scalar("B", typeof(string), 2, true),
                Scalar("A", typeof(int), 0, false),
                Scalar("C", typeof(int?), 1, true),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, 42, "x"], [typeof(int), typeof(int), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"B\":\"x\",\"A\":1,\"C\":42}]");
    }

    [Fact]
    public void AnyColumnNotNull_AllNull_ShouldWriteNull()
    {
        var shape = Obj(null,
            [
                Obj("Slot",
                    [Scalar("Id", typeof(int), 0, true), Scalar("Name", typeof(string), 1, true)],
                    JsonShapePresence.AnyColumnNotNull([0, 1])),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([null, null], [typeof(int), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Slot\":null}]");
    }

    [Fact]
    public void AnyColumnNotNull_OnePresent_ShouldWriteObject()
    {
        var shape = Obj(null,
            [
                Obj("Slot",
                    [Scalar("Id", typeof(int), 0, true), Scalar("Name", typeof(string), 1, true)],
                    JsonShapePresence.AnyColumnNotNull([0, 1])),
            ],
            JsonShapePresence.Always);
        // Only the second ordinal is present; presence must use the whole group, not just the first leaf.
        var record = new FakeRecord([null, "n"], [typeof(int), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Slot\":{\"Id\":null,\"Name\":\"n\"}}]");
    }

    [Fact]
    public void IgnoreNull_AbsentJoinedSlot_ShouldOmitProperty()
    {
        var shape = Obj(null,
            [
                Obj("Left", [Scalar("Id", typeof(int), 0, false)], JsonShapePresence.Always),
                Obj("Right", [Scalar("Id", typeof(int), 1, true)], JsonShapePresence.AnyColumnNotNull([1])),
            ],
            JsonShapePresence.Always);
        var plan = JsonShapePlan.Build(
            [new SelectExpression(typeof(int)) { Index = 0, PropertyName = "c0" },
             new SelectExpression(typeof(int)) { Index = 1, PropertyName = "c1" }],
            oneColumn: false,
            new JsonStreamOptions { IgnoreNull = true },
            shape);
        var record = new FakeRecord([1, null], [typeof(int), typeof(int)]);

        Write(JsonRowWriterFactory.Build(plan), record).Should().Be("[{\"Left\":{\"Id\":1}}]");
    }

    [Fact]
    public void UnsupportedScalarMemberType_ShouldThrowBeforeWriting()
    {
        var shape = Obj(null, [Scalar("Items", typeof(List<int>), 0, true)], JsonShapePresence.Always);

        var act = () => BuildWriter(shape);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void DictionaryAndEnumerableArrayElements_ShouldThrowBeforeWriting()
    {
        var dictionary = () => BuildWriter(Obj(null, [Arr("Map", typeof(Dictionary<string, int>[]), 0, true)], JsonShapePresence.Always));
        dictionary.Should().Throw<NotSupportedException>();

        var enumerable = () => BuildWriter(Obj(null, [Arr("Seq", typeof(IEnumerable<int>[]), 0, true)], JsonShapePresence.Always));
        enumerable.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void RootArray_ShouldWriteArrayValue()
    {
        var shape = JsonShapeNode.Array(null, typeof(int[]), Element(typeof(int)), new JsonShapeBinding(0, true, defaultOnNull: false));
        var record = new FakeRecord([new[] { 1, 2, 3 }], [typeof(int[])]);

        Write(BuildWriter(shape), record).Should().Be("[[1,2,3]]");
    }

    [Fact]
    public void RootScalar_Null_ShouldWriteNullValue()
    {
        var shape = Scalar(null, typeof(string), 0, nullable: true);
        var record = new FakeRecord([null], [typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[null]");
    }

    [Fact]
    public void JaggedArray_WithNullInner_ShouldWriteNullElement()
    {
        var shape = Obj(null, [Arr("Matrix", typeof(int[][]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new int[]?[] { new[] { 1 }, null }], [typeof(int[][])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Matrix\":[[1],null]}]");
    }

    [Fact]
    public void NullableElementArray_ShouldWriteNullElement()
    {
        var shape = Obj(null, [Arr("Values", typeof(int?[]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new int?[] { 1, null, 3 }], [typeof(int?[])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Values\":[1,null,3]}]");
    }

    // --- D176.7 conditional-construction presence: the hidden sentinel ordinal is not a member, so the
    // object presence must come from the sentinel alone, never from the visible leaf values. ---

    [Fact]
    public void ConditionalConstruction_PresentSentinelWithAllNullMembers_ShouldWriteObject()
    {
        // The construction arm is present (sentinel ordinal 2 is non-null) even though every visible leaf is
        // null; an all-null object must stay an object, not collapse to null.
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Child", [Scalar("Name", typeof(string), 1, true)], JsonShapePresence.AnyColumnNotNull([2])),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, null, "json"], [typeof(int), typeof(string), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Id\":1,\"Child\":{\"Name\":null}}]");
    }

    [Fact]
    public void ConditionalConstruction_AbsentSentinelWithPresentMembers_ShouldWriteNull()
    {
        // The sentinel is SQL NULL (null arm) while the visible leaves carry values; presence must not be
        // inferred from the payload, so the object is null.
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Child", [Scalar("Name", typeof(string), 1, true)], JsonShapePresence.AnyColumnNotNull([2])),
            ],
            JsonShapePresence.Always);
        var record = new FakeRecord([1, "ignored", null], [typeof(int), typeof(string), typeof(string)]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Id\":1,\"Child\":null}]");
    }

    // --- CHECK r2-n2 loopback: W2 provider array DBNull element, T5 boundary edges. ---

    [Fact]
    public void DBNullElementInsideArray_ShouldWriteNull()
    {
        // Some drivers surface a SQL NULL array element as boxed DBNull.Value rather than CLR null; the
        // recursive array writer must treat both as JSON null instead of failing the element cast.
        var shape = Obj(null, [Arr("Names", typeof(string[]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new object?[] { "a", DBNull.Value, null }], [typeof(string[])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Names\":[\"a\",null,null]}]");
    }

    [Fact]
    public void DepthExactly64_ShouldBeAllowed()
    {
        JsonShapeNode node = Scalar("Leaf", typeof(int), 0, false);
        for (var i = 0; i < 65; i++)
            node = Obj(i == 64 ? null : $"Level{i}", [node], JsonShapePresence.Always);

        // The root is depth 0 and the innermost object is depth 64, which is the allowed maximum.
        Write(BuildWriter(node), new FakeRecord([7], [typeof(int)])).Should().Contain("\"Leaf\":7");
    }

    [Fact]
    public void Depth65_ShouldThrowBeforeWriting()
    {
        JsonShapeNode node = Scalar("Leaf", typeof(int), 0, false);
        for (var i = 0; i < 66; i++)
            node = Obj(i == 65 ? null : $"Level{i}", [node], JsonShapePresence.Always);

        var act = () => BuildWriter(node);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ZeroMemberObject_ShouldWriteEmptyObject()
    {
        // A member-less nested object is a valid writer shape as long as the enclosing projection still
        // lowers at least one scalar column (a zero-column root is rejected at plan build).
        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Empty", [], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);

        Write(BuildWriter(shape), new FakeRecord([7], [typeof(int)])).Should().Be("[{\"Id\":7,\"Empty\":{}}]");
    }

    [Fact]
    public void NoSelectedColumns_ShouldThrowProjectionBeforeWriting()
    {
        // W2 (r2/n2): the phase-1 flat path (shape == null) with no selected columns. No public query
        // surface lowers a zero-column projection: EntityBuilder.ToCommand applies the entity projection,
        // and the historical trigger was a construction projection aborted by cancellation during
        // PrepareColumns, which PrepareJsonStream now short-circuits with ThrowIfCancellationRequested.
        // The reachable trigger is therefore this internal entry point — the exact method
        // PrepareJsonStream calls with cmd.SelectList — so the branch is exercised directly, not faked.
        var nullList = () => JsonShapePlan.Build(null, oneColumn: false, new JsonStreamOptions());
        nullList.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [projection]*");

        var emptyList = () => JsonShapePlan.Build([], oneColumn: false, new JsonStreamOptions());
        emptyList.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [projection]*");
    }

    [Fact]
    public void OneElementArray_ShouldWriteSingleElement()
    {
        var shape = Obj(null, [Arr("Values", typeof(int[]), 0, true)], JsonShapePresence.Always);
        var record = new FakeRecord([new[] { 42 }], [typeof(int[])]);

        Write(BuildWriter(shape), record).Should().Be("[{\"Values\":[42]}]");
    }

    [Fact]
    public void ReaderFailure_MidRead_ShouldPropagate()
    {
        var shape = Obj(null, [Scalar("Id", typeof(int), 0, false)], JsonShapePresence.Always);
        var record = new FakeRecord([1], [typeof(int)], throwOnOrdinal: 0);

        var act = () => Write(BuildWriter(shape), record);

        act.Should().Throw<InvalidOperationException>().WithMessage("reader failed mid-read");
    }
}
