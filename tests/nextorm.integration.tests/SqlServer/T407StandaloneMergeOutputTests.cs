using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

[Collection("SqlServer")]
public sealed class T407StandaloneMergeOutputTests(ITestOutputHelper output) : ProviderTestSuite
{
    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WritesInsertAndUpdateActionsAndOptionallyReturnsOnlyAffectedProjection(bool returnRows)
    {
        var ctx = _sut.DataProvider;
        using var connection = new SqlConnection(SqlServerContainer.ConnectionString);
        connection.Open();
        using var setup = connection.CreateCommand();
        setup.CommandText = """
            create table t407_capture ([action] nvarchar(10), id int, name nvarchar(100));
            insert into t407_capture values ('OLD', 40700, 'preexisting');
            insert into merge_entity (id, name, age) values (40701, 'before', 1);
            """;
        setup.ExecuteNonQuery();

        try
        {
            var merge = ctx.CreateMergeBuilder<IMergeEntity>().Using([
                new MergeEntity { Id = 40701, Name = "updated", Age = 41 },
                new MergeEntity { Id = 40702, Name = "inserted", Age = 42 },
            ]).OnKeys();
            if (returnRows)
                merge.WhenMatched().ThenUpdate().WhenNotMatched().ThenInsert();
            else
                merge.WhenMatchedUpdate().WhenNotMatchedInsert();

            var returning = merge.Returning(x => new { x.Id, x.Name });
            if (returnRows)
            {
                var rows = returning.OutputIntoThenOutput("t407_capture").ToList().OrderBy(r => r.Id).ToArray();
                rows.Should().Equal(new { Id = 40701, Name = (string?)"updated" },
                    new { Id = 40702, Name = (string?)"inserted" });
                output.WriteLine("client=40701:updated,40702:inserted");
            }
            else
            {
                returning.OutputInto("t407_capture").Execute();
            }

            var audit = ctx.From("t407_capture").OrderBy(x => x.GetInt32("id"))
                .Select(x => new { Action = x.GetString("action"), Id = x.GetInt32("id"), Name = x.GetString("name") })
                .ToList();
            audit.Should().Equal(
                new { Action = "OLD", Id = 40700, Name = "preexisting" },
                new { Action = "UPDATE", Id = 40701, Name = "updated" },
                new { Action = "INSERT", Id = 40702, Name = "inserted" });
            ctx.From<IMergeEntity>().Where(row => row.Id == 40701 || row.Id == 40702)
                .OrderBy(row => row.Id).Select(row => row.Age).ToList().Should().Equal(41, 42);
            output.WriteLine("audit=40700:OLD:preexisting,40701:UPDATE:updated,40702:INSERT:inserted; ages=41,42");
        }
        finally
        {
            setup.CommandText = "drop table t407_capture; delete from merge_entity where id in (40701, 40702);";
            setup.ExecuteNonQuery();
        }
    }
}
