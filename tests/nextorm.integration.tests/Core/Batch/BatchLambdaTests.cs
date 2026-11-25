using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class BatchLambdaTests
{
    [Fact]
    public async Task InsertAndQueryLambdas_ShouldReadInsertedRowsOnlyWhenExecuted()
    {
        using var ctx = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var rows = new[] { (Id: 2, Label: "second"), (Id: 1, Label: "first") };

        var batch = ctx.CreateBatchBuilder()
            .Raw("create table batch_lambda_row (id integer primary key, label text not null)")
            .Insert<IBatchLambdaRow>(insert => insert.Values(rows, row => new { row.Id, row.Label }))
            .Query(db => db.From<IBatchLambdaRow>()
                .OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Label }));

        ctx.From("sqlite_master")
            .Where(t => t["name"].AsString == "batch_lambda_row")
            .Select(t => t["name"].AsString)
            .ToList().Should().BeEmpty();

        var result = await batch.ToListAsync(TestContext.Current.CancellationToken);

        result.Select(row => (row.Id, row.Label)).Should().Equal((1, "first"), (2, "second"));
    }

    [SqlTable("batch_lambda_row")]
    public interface IBatchLambdaRow
    {
        [Column("id")]
        int Id { get; set; }

        [Column("label")]
        string Label { get; set; }
    }
}
