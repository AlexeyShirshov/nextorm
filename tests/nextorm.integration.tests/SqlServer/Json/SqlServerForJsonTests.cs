using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Verifies the SQL Server <c>FOR JSON</c>/<c>FOR XML</c> terminal operators end to end: the whole
/// result set must come back as one document (no implicit <c>TOP 1</c>), including an anonymous
/// projection that cannot be materialized as a row type.
/// </summary>
public sealed class SqlServerForJsonTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Fact]
    public void ForJson_AnonymousProjection_ShouldReturnWholeDocument()
    {
        var json = _sut.ComplexEntity
            .Select(x => new { x.Id, x.String })
            .ForJson(ForJsonMode.Path, root: "items", includeNullValues: true);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task ForJsonAsync_ShouldReturnWholeDocument()
    {
        var json = await _sut.ComplexEntity
            .Select(x => new { x.Id, x.String })
            .ForJsonAsync(ForJsonMode.Path, root: "items", includeNullValues: true, cancellationToken: TestContext.Current.CancellationToken);

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void ForJson_NoRows_ShouldReturnNull()
    {
        var json = _sut.ComplexEntity
            .Where(x => x.Id < 0)
            .Select(x => new { x.Id })
            .ForJson(ForJsonMode.Path, root: "items");

        json.Should().BeNull();
    }

    [Fact]
    public void ForJson_WithHint_ShouldCompose()
    {
        var json = _sut.ComplexEntity
            .Select(x => new { x.Id })
            .Hint("recompile")
            .ForJson(ForJsonMode.Path, root: "items");

        json.Should().NotBeNull();
        using var doc = JsonDocument.Parse(json!);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(3);
    }

    [Fact]
    public void ForXml_ShouldReturnWholeDocument()
    {
        var xml = _sut.ComplexEntity
            .Select(x => new { x.Id, x.String })
            .ForXml(ForXmlMode.Path, root: "items");

        xml.Should().NotBeNull();
        xml!.Should().Contain("<items>");
    }

    [Fact]
    public void ForXml_NoRows_ShouldReturnNull()
    {
        var xml = _sut.ComplexEntity
            .Where(x => x.Id < 0)
            .Select(x => new { x.Id })
            .ForXml(ForXmlMode.Path, root: "items");

        xml.Should().BeNull();
    }
}
