using System.Text.Json;
using System.Text.Json.Nodes;
using ClickHouse.Driver.ADO.Parameters;
using FluentAssertions;
using FluentAssertions.Execution;
using NextORM.ClickHouse;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// The driver infers a non-nullable element type from a CLR array value, so a null element is replaced
/// by the element default: <c>new int?[] { 1, null, 3 }</c> binds as <c>Array(Int32)</c> and the server
/// receives <c>{1, 0, 3}</c>. <see cref="ClickHouseDataContext.CreateParam"/> therefore sets an explicit
/// <c>Array(Nullable(T))</c> type for a rank-1 array whose declared element type is nullable
/// (<see cref="Nullable{T}"/> or <see cref="string"/>), and leaves every other shape on the driver's
/// inference. The name comes from the declared element type, so empty and all-null arrays keep it too.
/// </summary>
public class NullableArrayParameterTests
{
    [Fact]
    public void NullableIntArray_BindsNullableElementTypeAndKeepsTheSameValue()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();
        var values = new int?[] { 1, null, 3 };

        var parameter = (ClickHouseDbParameter)context.CreateParam("@p", values);

        using (new AssertionScope())
        {
            parameter.ParameterName.Should().Be("p", "the ADO @ prefix stays stripped as before");
            parameter.ClickHouseType.Should().Be("Array(Nullable(Int32))");
            parameter.Value.Should().BeSameAs(values, "the caller's array is bound directly, never copied");
            values.Should().Equal(new int?[] { 1, null, 3 });
        }
    }

    [Fact]
    public void NullableElementArrays_UseTheDeclaredElementType()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();

        using (new AssertionScope())
        {
            ((ClickHouseDbParameter)context.CreateParam("@p", new double?[] { 1.5, null, -2.5 }))
                .ClickHouseType.Should().Be("Array(Nullable(Float64))");
            ((ClickHouseDbParameter)context.CreateParam("@p", new DateTime?[] { DateTime.UnixEpoch, null }))
                .ClickHouseType.Should().Be("Array(Nullable(DateTime))");
            ((ClickHouseDbParameter)context.CreateParam("@p", new decimal?[] { 1.5m, null, 3.25m }))
                .ClickHouseType.Should().Be("Array(Nullable(Decimal(38,10)))");
            ((ClickHouseDbParameter)context.CreateParam("@p", new string?[] { "a", null, "b" }))
                .ClickHouseType.Should().Be("Array(Nullable(String))");
        }
    }

    [Fact]
    public void EmptyAndAllNullNullableIntArrays_KeepTheDeclaredElementType()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();

        using (new AssertionScope())
        {
            ((ClickHouseDbParameter)context.CreateParam("@p", Array.Empty<int?>()))
                .ClickHouseType.Should().Be("Array(Nullable(Int32))", "the type never comes from the values");
            ((ClickHouseDbParameter)context.CreateParam("@p", new int?[] { null, null, null }))
                .ClickHouseType.Should().Be("Array(Nullable(Int32))");
            ((ClickHouseDbParameter)context.CreateParam("@p", Array.Empty<string?>()))
                .ClickHouseType.Should().Be("Array(Nullable(String))");
        }
    }

    [Fact]
    public void OtherShapes_KeepTheDriverInference()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();

        using (new AssertionScope())
        {
            ((ClickHouseDbParameter)context.CreateParam("@p", new[] { 1, 2, 3 }))
                .ClickHouseType.Should().BeNull("a non-nullable value-type element array stays on the old path");
            ((ClickHouseDbParameter)context.CreateParam("@p", new[] { 1.5, 2.5 }))
                .ClickHouseType.Should().BeNull();
            ((ClickHouseDbParameter)context.CreateParam("@p", new[] { DateTime.UnixEpoch }))
                .ClickHouseType.Should().BeNull();
            ((ClickHouseDbParameter)context.CreateParam("@p", new[] { 1.5m }))
                .ClickHouseType.Should().BeNull();
            ((ClickHouseDbParameter)context.CreateParam("@p", new byte[] { 1, 2 }))
                .ClickHouseType.Should().BeNull("byte[] is bound as a binary string, not an array");
            ((ClickHouseDbParameter)context.CreateParam("@p", new int?[][] { [1, null], [2] }))
                .ClickHouseType.Should().BeNull("Nullable(Array(...)) is illegal in ClickHouse");
            ((ClickHouseDbParameter)context.CreateParam("@p", new int[][] { [1, 2], [3] }))
                .ClickHouseType.Should().BeNull("an unsupported jagged rank keeps the driver's inference");
            ((ClickHouseDbParameter)context.CreateParam("@p", new[] { "a", "b" }))
                .ClickHouseType.Should().Be("Array(Nullable(String))", "string is the one nullable reference element type");
        }
    }

    [Fact]
    public void CallerArrayIsNotMutatedByCreateParam()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();
        var ints = new int?[] { 1, null, 3 };
        var strings = new string?[] { "a", null, "b" };
        var expectedInts = (int?[])ints.Clone();
        var expectedStrings = (string?[])strings.Clone();

        var intParameter = (ClickHouseDbParameter)context.CreateParam("@p", ints);
        var stringParameter = (ClickHouseDbParameter)context.CreateParam("@p", strings);

        using (new AssertionScope())
        {
            ints.Should().Equal(expectedInts);
            strings.Should().Equal(expectedStrings);
            intParameter.Value.Should().BeSameAs(ints);
            stringParameter.Value.Should().BeSameAs(strings);
        }
    }

    [Fact]
    public void NullArrayObject_KeepsTheExistingBinding()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();

        var parameter = (ClickHouseDbParameter)context.CreateParam("@p", (int[]?)null);

        using (new AssertionScope())
        {
            parameter.ParameterName.Should().Be("p");
            parameter.Value.Should().BeNull("a null array object carries no declared element type");
            parameter.ClickHouseType.Should().BeNull(
                "the guard is value-based: no explicit type is set for a null array object");
        }
    }

    [Fact]
    public void JsonBranches_StillBindTheNativeJsonType()
    {
        using var context = ClickHouseTestContext.CreateClickHouse();
        using var document = JsonDocument.Parse("{\"a\":1}");

        using (new AssertionScope())
        {
            ((ClickHouseDbParameter)context.CreateParam("@p", new JsonObject { ["a"] = 1 }))
                .ClickHouseType.Should().Be("JSON");
            ((ClickHouseDbParameter)context.CreateParam("@p", document.RootElement))
                .ClickHouseType.Should().Be("JSON");
            ((ClickHouseDbParameter)context.CreateParam("@p", document))
                .ClickHouseType.Should().Be("JSON");
        }
    }
}
