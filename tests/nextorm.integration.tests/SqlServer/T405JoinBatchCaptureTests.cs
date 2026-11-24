using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("SqlServer")]
public sealed class T405JoinBatchCaptureTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturesOnlyTheRowSelectedByBothJoinAndPredicate(bool delete)
    {
        var ctx = _sut.DataProvider;
        var key = Random.Shared.Next(100_000_000, 1_000_000_000);
        var selected = key + 1;
        var excludedByPredicate = key + 2;
        var excludedByJoin = key + 3;
        try
        {
            ctx.CreateInsertBuilder<IMergeEntity>().Values([
                new MergeEntity { Id = key, Age = 0, Name = "witness" },
                new MergeEntity { Id = selected, Age = key, Name = "selected" },
                new MergeEntity { Id = excludedByPredicate, Age = key, Name = "excluded-by-predicate" },
                new MergeEntity { Id = excludedByJoin, Age = key + 4, Name = "excluded-by-join" },
            ]).Insert();

            var join = ctx.From<IMergeEntity>()
                .Join(ctx.From<IMergeEntity>(), (target, witness) => target.Age == witness.Id)
                .Where(p => p.Item2.Id == key && p.Item1.Id != excludedByPredicate);
            var batch = ctx.CreateBatchBuilder();
            if (delete)
                batch.OutputIntoTableVariable(
                    join.CreateDeleteJoinBuilder().Returning(p => new { p.Item1.Id, p.Item1.Name }),
                    "@captured", "id int, name nvarchar(100)");
            else
                batch.OutputIntoTableVariable(
                    join.CreateUpdateJoinBuilder().Set(p => p.Item1.Name, "updated").Returning(p => new { p.Item1.Id, p.Item1.Name }),
                    "@captured", "id int, name nvarchar(100)");

            var captured = batch.Query(batch.FromOutputTable("@captured")
                .Select(t => new { Id = t["id"].AsInt, Name = t["name"].AsString })).ToList();
            var row = captured.Should().ContainSingle().Subject;
            row.Id.Should().Be(selected);
            row.Name.Should().Be(delete ? "selected" : "updated");

            var remaining = ctx.From<IMergeEntity>().Where(x => x.Id >= key && x.Id <= excludedByJoin)
                .Select(x => x.Name).ToList();
            remaining.Should().BeEquivalentTo(delete
                ? new[] { "witness", "excluded-by-predicate", "excluded-by-join" }
                : new[] { "witness", "updated", "excluded-by-predicate", "excluded-by-join" });
        }
        finally
        {
            ctx.CreateDeleteBuilder<IMergeEntity>().Where(x => x.Id >= key && x.Id <= excludedByJoin).Delete();
        }
    }
}
