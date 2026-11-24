using FluentAssertions;
using NextORM.Generated.nextorm_integration_tests;

namespace NextORM.Integration.Tests;

public sealed class SqliteCorrelatedTvfCrossJoinTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public void CrossJoin_ExpandsTwoDependentJsonEachSources_AndSkipsEmptyObjects()
    {
        // The root is uncorrelated, the next two sources consume the preceding source's value.
        // All three use the same row type, so type-based alias selection cannot pass this test.
        const string json = """[[{"ref":"first"},{}],[{}, {"ref":"second","extra":"third"}],[{}]]""";
        var ctx = _sut.DataProvider;
        var result = ctx.From(() => SqlFunctions.Sqlite.json_each(json))
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.Value))
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.Item2.Value))
            .Select(row => new { RootKey = row.Item1.Key, Reference = row.Item3.Value })
            .ToList();

        result.Select(row => (row.RootKey, row.Reference)).Should().BeEquivalentTo(
            new (string?, string?)[] { ("0", "first"), ("1", "second"), ("1", "third") });
    }

    [Fact]
    public void CrossJoin_ReadsNamedTvfSlotInNextPositionalCrossJoin()
    {
        const string json = """[[{"ref":"first"},{}],[{}, {"ref":"second","extra":"third"}],[{}]]""";
        var ctx = _sut.DataProvider;
        var result = ctx.From(() => SqlFunctions.Sqlite.json_each(json))
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.Value), Alias.States)
            .CrossJoin(row => SqlFunctions.Sqlite.json_each(row.States.Value))
            .Select(row => new { RootKey = row.Item1.Key, StateKey = row.States.Key, Reference = row.Item3.Value })
            .ToList();

        result.Select(row => (row.RootKey, row.StateKey, row.Reference)).Should().BeEquivalentTo(
            new (string?, string?, string?)[] { ("0", "0", "first"), ("1", "1", "second"), ("1", "1", "third") });
    }
}
