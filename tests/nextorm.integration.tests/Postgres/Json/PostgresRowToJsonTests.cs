using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Postgres")]
public sealed class PostgresRowToJsonTests : ProviderTestSuite
{
    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [Fact]
    public void RowToJson_ProjectsBothFieldsInOrder()
    {
        var json = _sut.SimpleEntity
            .Where(row => row.Id == 1)
            .Select(row => SqlFunctions.Postgres.row_to_json(Tuple.Create(row.Id, row.Id + 1)))
            .First();

        json.Should().NotBeNull();
        using var document = JsonDocument.Parse(json!);
        var fields = document.RootElement.EnumerateObject();
        fields.Select(field => (field.Name, field.Value.GetInt64()))
            .Should().Equal(("f1", 1L), ("f2", 2L));
    }
}
