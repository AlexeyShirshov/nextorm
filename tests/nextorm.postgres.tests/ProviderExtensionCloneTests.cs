using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Postgres.Tests;

/// <summary>
/// Clone/projection independence for the relocated PostgreSQL fluent extension. The tests never open a
/// connection.
/// </summary>
public class ProviderExtensionCloneTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        return (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void DistinctOn_SurvivesClone_AndIsIndependent()
    {
        using var ctx = PostgresTestContext.Create();
        var e = ctx.From<ISimpleEntity>();

        var distinctOn = e.DistinctOn(x => x.Id);
        var clone = distinctOn.Clone();

        SqlOf(ctx, distinctOn.OrderBy(x => x.Id).Select(x => new { x.Id }))
            .Should().StartWith("select distinct on (id) id from simple_entity");
        SqlOf(ctx, clone.OrderBy(x => x.Id).Select(x => new { x.Id }))
            .Should().StartWith("select distinct on (id) id from simple_entity");

        // The original builder never gained the DISTINCT ON clause itself.
        SqlOf(ctx, e.Select(x => new { x.Id })).Should().NotContain("distinct on");
    }
}
