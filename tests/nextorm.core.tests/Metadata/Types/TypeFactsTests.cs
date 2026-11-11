using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Direct contract of the internal <see cref="TypeFacts.IsSingleColumnProjection"/> classification used by
/// the query preparer (#197 additive prerequisite D197-P01): a bare <see cref="JsonNode"/> is a
/// single-column scalar, while the derived/related JSON types and arbitrary entities are not. Exercised
/// through <c>InternalsVisibleTo("nextorm.core.tests")</c>.
/// </summary>
public class TypeFactsTests
{
    [Fact]
    public void IsSingleColumnProjection_ForExactJsonNode_ShouldBeTrue()
    {
        TypeFacts.IsSingleColumnProjection(typeof(JsonNode)).Should().BeTrue();
    }

    [Theory]
    [InlineData(typeof(JsonObject))]
    [InlineData(typeof(JsonArray))]
    [InlineData(typeof(JsonValue))]
    [InlineData(typeof(JsonDocument))]
    [InlineData(typeof(JsonElement))]
    public void IsSingleColumnProjection_ForNonExactJsonTypes_ShouldBeFalse(Type type)
    {
        TypeFacts.IsSingleColumnProjection(type).Should().BeFalse();
    }

    [Fact]
    public void IsSingleColumnProjection_ForArbitraryEntity_ShouldBeFalse()
    {
        TypeFacts.IsSingleColumnProjection(typeof(ProbeEntity)).Should().BeFalse();
    }

    [Theory]
    [InlineData(typeof(string))]
    [InlineData(typeof(int))]
    [InlineData(typeof(int?))]
    [InlineData(typeof(byte[]))]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(Guid))]
    public void IsSingleColumnProjection_ForExistingCategories_ShouldBeUnchanged(Type type)
    {
        TypeFacts.IsSingleColumnProjection(type).Should().BeTrue();
    }

    private sealed class ProbeEntity
    {
        public int Id { get; set; }
    }
}
