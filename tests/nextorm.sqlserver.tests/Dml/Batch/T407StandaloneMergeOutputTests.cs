using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.SqlServer.Tests;

public sealed class T407StandaloneMergeOutputTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MapsActionFirstAndProjectsOnlyInsertedValuesToClient(bool returnRows)
    {
        using var ctx = SqlServerTestContext.Create();
        var result = ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Returning(x => new { x.Id, x.Name });
        var sql = returnRows ? result.OutputIntoThenOutput("merge_audit").ToSql()
            : result.OutputInto("merge_audit").ToSql();
        var client = returnRows ? " output inserted.id, inserted.name" : "";
        sql.Should().EndWith(" output $action, inserted.id, inserted.name into [merge_audit] ([action], id, name)" + client + ";");
        result.ToSql().Should().EndWith(" output inserted.id, inserted.name;");
    }

    [Fact]
    public void RejectsDeleteBranchWhenSelectingInsertedValues()
    {
        using var ctx = SqlServerTestContext.Create();
        var result = ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys().WhenMatched().ThenDelete().Returning(x => x.Id);

        FluentActions.Invoking(() => result.OutputInto("merge_audit"))
            .Should().Throw<NotSupportedException>().WithMessage("*only INSERT and UPDATE branches*");
        FluentActions.Invoking(() => result.OutputIntoThenOutput("merge_audit"))
            .Should().Throw<NotSupportedException>().WithMessage("*only INSERT and UPDATE branches*");
    }

    [Fact]
    public void RejectsPostgresAtRegistrationWithoutOpeningAConnection()
    {
        using var ctx = new PostgresDataContext(new DataContextBuilder());
        var result = ctx.CreateMergeBuilder<IMergeEntity>().Returning(x => x.Id);

        FluentActions.Invoking(() => result.OutputInto("merge_audit"))
            .Should().Throw<NotSupportedException>().WithMessage("*requires SQL Server*");
        FluentActions.Invoking(() => result.OutputIntoThenOutput("merge_audit"))
            .Should().Throw<NotSupportedException>().WithMessage("*requires SQL Server*");
    }
}
