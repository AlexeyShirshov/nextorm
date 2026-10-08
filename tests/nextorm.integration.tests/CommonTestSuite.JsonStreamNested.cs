using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// D176.5 shared integration coverage for the phase-2 JSON shape work (D176.1-D176.4 and D176.7):
/// nested anonymous/named constructions, the actual <c>Projection&lt;T1,T2&gt;</c> slot terminal,
/// conditional null/object arms and <c>byte[]</c> nested in an object, on both terminal surfaces
/// (sync <c>WriteJson</c> and async <c>WriteJsonAsync</c>). Every supported result is compared with
/// the STJ oracle over the equivalent constructed objects; the unmatched outer-join slot must be a
/// JSON <c>null</c>.
/// </summary>
/// <remarks>
/// The bodies are <c>internal static</c> so the standalone MariaDB class
/// (<c>MariaDbJsonStreamTests</c>) and the standalone ClickHouse class
/// (<c>ClickHouseIntegrationTests</c>) can mirror them — neither derives
/// <see cref="CommonTestSuite"/>, so inheriting the facts is not enough. ClickHouse adds its own
/// join/slot fixture and native-array facts in its class because it has no <c>binary_entity</c>.
/// </remarks>
public abstract partial class CommonTestSuite
{
    /// <summary>Named outer result of a member-init nested projection (member-init is not anonymous).</summary>
    public sealed class JsonNamedOuter
    {
        public long Id { get; set; }
        public JsonNamedInner? Child { get; set; }
    }

    /// <summary>Named inner result of a member-init nested projection.</summary>
    public sealed class JsonNamedInner
    {
        public string? Name { get; set; }
        public int? Value { get; set; }
    }

    private static string JsonText<TResult>(QueryCommand<TResult> command)
        => Encoding.UTF8.GetString(WriteJsonBytes(command));

    // A bare join command is an EntityBuilder<Projection<...>>, not a QueryCommand; the user-defined
    // conversion is not considered for generic inference, so it needs its own overload.
    private static string JsonText<TEntity>(EntityBuilder<TEntity> builder)
    {
        using var buffer = new MemoryStream();
        builder.WriteJson(buffer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static async Task<string> JsonTextAsync<TResult>(QueryCommand<TResult> command)
    {
        using var buffer = new MemoryStream();
        await command.WriteJsonAsync(buffer, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static async Task<string> JsonTextAsync<TEntity>(EntityBuilder<TEntity> builder)
    {
        using var buffer = new MemoryStream();
        await builder.WriteJsonAsync(buffer, TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    // --- Nested objects ---

    [Fact]
    public void WriteJson_NestedAnonymous_ShouldMatchSerializer() => WriteJsonNestedAnonymous(_sut);

    internal static void WriteJsonNestedAnonymous(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.String, x.Int } });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = new { String = "dadfasd", Int = (int?)null } },
            new { Id = 2L, Child = new { String = "xxx", Int = (int?)1 } },
            new { Id = 3L, Child = new { String = (string?)null, Int = (int?)1 } },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_NestedMultipleLevels_ShouldMatchSerializer() => WriteJsonNestedMultipleLevels(_sut);

    internal static void WriteJsonNestedMultipleLevels(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Outer = new { x.String, Inner = new { x.Int } } });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Outer = new { String = "dadfasd", Inner = new { Int = (int?)null } } },
            new { Id = 2L, Outer = new { String = "xxx", Inner = new { Int = (int?)1 } } },
            new { Id = 3L, Outer = new { String = (string?)null, Inner = new { Int = (int?)1 } } },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_NestedNamed_ShouldMatchSerializer() => WriteJsonNestedNamed(_sut);

    internal static void WriteJsonNestedNamed(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new JsonNamedOuter
            {
                Id = x.Id,
                Child = new JsonNamedInner { Name = x.String, Value = x.Int },
            });

        var expected = JsonSerializer.Serialize(new[]
        {
            new JsonNamedOuter { Id = 1, Child = new JsonNamedInner { Name = "dadfasd", Value = null } },
            new JsonNamedOuter { Id = 2, Child = new JsonNamedInner { Name = "xxx", Value = 1 } },
            new JsonNamedOuter { Id = 3, Child = new JsonNamedInner { Name = null, Value = 1 } },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_NestedAllNullChild_ShouldStayObject() => WriteJsonNestedAllNullChild(_sut);

    internal static void WriteJsonNestedAllNullChild(TestDataRepository sut)
    {
        // Construction establishes object presence: the child has a single null leaf yet stays an object.
        var command = sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { x.Id, Child = new { x.Int } });

        JsonText(command).Should().Be("[{\"Id\":1,\"Child\":{\"Int\":null}}]");
    }

    [Fact]
    public void WriteJson_NestedSameNameDifferentScopes_ShouldBeAccepted() => WriteJsonNestedSameNameDifferentScopes(_sut);

    internal static void WriteJsonNestedSameNameDifferentScopes(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Id } });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = new { Id = 1L } },
            new { Id = 2L, Child = new { Id = 2L } },
            new { Id = 3L, Child = new { Id = 3L } },
        });

        JsonText(command).Should().Be(expected);
    }

    // --- Conditional constructions ---

    [Fact]
    public void WriteJson_ConditionalNullArmTrue_ShouldMatchSerializer() => WriteJsonConditionalNullArmTrue(_sut);

    internal static void WriteJsonConditionalNullArmTrue(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.String == null ? null : new { x.String } });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = (object)new { String = "dadfasd" } },
            new { Id = 2L, Child = (object)new { String = "xxx" } },
            new { Id = 3L, Child = (object?)null },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_ConditionalNullArmFalse_ShouldMatchSerializer() => WriteJsonConditionalNullArmFalse(_sut);

    internal static void WriteJsonConditionalNullArmFalse(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.String != null ? new { x.String } : null });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = (object)new { String = "dadfasd" } },
            new { Id = 2L, Child = (object)new { String = "xxx" } },
            new { Id = 3L, Child = (object?)null },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_ConditionalAllNullObject_ShouldStayObject() => WriteJsonConditionalAllNullObject(_sut);

    internal static void WriteJsonConditionalAllNullObject(TestDataRepository sut)
    {
        // Row 3 takes the construction arm with a null leaf; the sentinel presence keeps it an object.
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Id == 3 ? new { x.String } : null });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = (object?)null },
            new { Id = 2L, Child = (object?)null },
            new { Id = 3L, Child = (object)new { String = (string?)null } },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_ConditionalMemberInit_ShouldMatchSerializer() => WriteJsonConditionalMemberInit(_sut);

    internal static void WriteJsonConditionalMemberInit(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.String == null ? null : new JsonNamedInner { Name = x.String, Value = x.Int } });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1L, Child = (object)new JsonNamedInner { Name = "dadfasd", Value = null } },
            new { Id = 2L, Child = (object)new JsonNamedInner { Name = "xxx", Value = 1 } },
            new { Id = 3L, Child = (object?)null },
        });

        JsonText(command).Should().Be(expected);
    }

    [Fact]
    public void WriteJson_ConditionalBothArmsConstruction_ShouldThrowBeforeOutput() => WriteJsonConditionalBothArmsConstruction(_sut);

    internal static void WriteJsonConditionalBothArmsConstruction(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.String == null ? new { String = (string?)x.String } : new { String = (string?)x.String } });

        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // --- Projection<T1,T2> slots ---

    [Fact]
    public void WriteJson_BareProjectionLeftJoin_ShouldMatchItem1Item2() => WriteJsonBareProjectionLeftJoin(_sut);

    internal static void WriteJsonBareProjectionLeftJoin(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .LeftJoin(sut.BinaryEntity, (p, c) => p.Id == c.Id);

        var expected = JsonSerializer.Serialize(command.ToList());
        var actual = JsonText(command);

        actual.Should().Be(expected);
        actual.Should().Contain("\"Item1\"").And.Contain("\"Item2\"");
        actual.Should().Contain("\"Item2\":null", "the unmatched outer-join slot is JSON null");
    }

    [Fact]
    public void WriteJson_BareProjectionInnerJoin_ShouldMatchItem1Item2() => WriteJsonBareProjectionInnerJoin(_sut);

    internal static void WriteJsonBareProjectionInnerJoin(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .Join(sut.BinaryEntity, (p, c) => p.Id == c.Id);

        JsonText(command).Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public void WriteJson_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted() => WriteJsonDuplicateLeafNamesAcrossSlots(_sut);

    internal static void WriteJsonDuplicateLeafNamesAcrossSlots(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .LeftJoin(sut.BinaryEntity, (p, c) => p.Id == c.Id);

        using var document = JsonDocument.Parse(JsonText(command));
        var first = document.RootElement[0];

        // Id exists in both slots; uniqueness is enforced per object scope, not globally.
        first.GetProperty("Item1").GetProperty("Id").GetInt32().Should().Be(1);
        first.GetProperty("Item2").GetProperty("Id").GetInt32().Should().Be(1);
    }

    [Fact]
    public void WriteJson_EntityAndScalarSlot_ShouldNotWrapScalar() => WriteJsonEntityAndScalarSlot(_sut);

    internal static void WriteJsonEntityAndScalarSlot(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .Join(sut.BinaryEntity, (p, c) => p.Id == c.Id)
            .Select(p => new { p.Item1, ChildId = p.Item2.Id });

        var actual = JsonText(command);
        actual.Should().Be(JsonSerializer.Serialize(command.ToList()));

        using var document = JsonDocument.Parse(actual);
        var first = document.RootElement[0];
        first.GetProperty("Item1").ValueKind.Should().Be(JsonValueKind.Object);
        first.GetProperty("ChildId").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public void WriteJson_ScalarScalarSlots_ShouldBeFlatScalars() => WriteJsonScalarScalarSlots(_sut);

    internal static void WriteJsonScalarScalarSlots(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .Join(sut.BinaryEntity, (p, c) => p.Id == c.Id)
            .Select(p => new { ParentId = p.Item1.Id, ChildId = p.Item2.Id });

        var actual = JsonText(command);
        actual.Should().Be(JsonSerializer.Serialize(command.ToList()));

        using var document = JsonDocument.Parse(actual);
        var first = document.RootElement[0];
        first.GetProperty("ParentId").ValueKind.Should().Be(JsonValueKind.Number);
        first.GetProperty("ChildId").ValueKind.Should().Be(JsonValueKind.Number);
    }

    // --- byte[] inside a nested object ---

    [Fact]
    public void WriteJson_ByteArrayNested_ShouldBeBase64() => WriteJsonByteArrayNested(_sut);

    internal static void WriteJsonByteArrayNested(TestDataRepository sut)
    {
        var command = sut.BinaryEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Data } });

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = new { Data = new byte[] { 1, 2, 3, 4 } } },
            new { Id = 2, Child = new { Data = (byte[]?)null } },
        });

        JsonText(command).Should().Be(expected);
    }

    // --- Both terminal surfaces ---

    [Fact]
    public async Task WriteJson_NestedAsync_ShouldMatchSync() => await WriteJsonNestedAsyncMatchesSync(_sut);

    internal static async Task WriteJsonNestedAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.String, x.Int } });

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }

    [Fact]
    public async Task WriteJson_SlotAsync_ShouldMatchSync() => await WriteJsonSlotAsyncMatchesSync(_sut);

    internal static async Task WriteJsonSlotAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .LeftJoin(sut.BinaryEntity, (p, c) => p.Id == c.Id);

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }

    [Fact]
    public async Task WriteJson_ConditionalAsync_ShouldMatchSync() => await WriteJsonConditionalAsyncMatchesSync(_sut);

    internal static async Task WriteJsonConditionalAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.String == null ? null : new { x.String } });

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }

    [Fact]
    public async Task WriteJson_ByteArrayAsync_ShouldMatchSync() => await WriteJsonByteArrayAsyncMatchesSync(_sut);

    internal static async Task WriteJsonByteArrayAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.BinaryEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Data } });

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }

    // --- T3: duplicate effective name within one object scope ---

    [Fact]
    public void WriteJson_DuplicateNameWithinOneObject_ShouldThrowBeforeOutput()
        => WriteJsonDuplicateNameWithinOneObject(_sut);

    internal static void WriteJsonDuplicateNameWithinOneObject(TestDataRepository sut)
    {
        // The nested object's two distinct members map to one effective name; the per-object uniqueness
        // check (not the both-arms guard) must reject before any output.
        var command = sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.String, x.Int } });

        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream, new JsonStreamOptions { PropertyNamingPolicy = new SelectiveDuplicateNamingPolicy() });

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    private sealed class SelectiveDuplicateNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
            => name is "String" or "Int" ? "collision" : name;
    }

    // --- T4: remaining terminal surface combinations (whole entity, slots, conditional root) ---

    [Fact]
    public async Task WriteJson_WholeEntityAsync_ShouldMatchSync() => await WriteJsonWholeEntityAsyncMatchesSync(_sut);

    internal static async Task WriteJsonWholeEntityAsyncMatchesSync(TestDataRepository sut)
    {
        var builder = sut.ComplexEntity.OrderBy(x => x.Id);

        (await JsonTextAsync(builder)).Should().Be(JsonText(builder));
    }

    [Fact]
    public async Task WriteJson_EntityAndScalarSlotAsync_ShouldMatchSync() => await WriteJsonEntityAndScalarSlotAsyncMatchesSync(_sut);

    internal static async Task WriteJsonEntityAndScalarSlotAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .Join(sut.BinaryEntity, (p, c) => p.Id == c.Id)
            .Select(p => new { p.Item1, ChildId = p.Item2.Id });

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }

    [Fact]
    public async Task WriteJson_ScalarScalarSlotsAsync_ShouldMatchSync() => await WriteJsonScalarScalarSlotsAsyncMatchesSync(_sut);

    internal static async Task WriteJsonScalarScalarSlotsAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.SimpleEntityAsClass.OrderBy(p => p.Id)
            .Join(sut.BinaryEntity, (p, c) => p.Id == c.Id)
            .Select(p => new { ParentId = p.Item1.Id, ChildId = p.Item2.Id });

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }

    [Fact]
    public async Task WriteJson_ConditionalRootAsync_ShouldMatchSync() => await WriteJsonConditionalRootAsyncMatchesSync(_sut);

    internal static async Task WriteJsonConditionalRootAsyncMatchesSync(TestDataRepository sut)
    {
        var command = sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => x.String == null ? null : new { x.String });

        (await JsonTextAsync(command)).Should().Be(JsonText(command));
    }
}
