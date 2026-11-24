using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

public sealed class TempViewTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreateTempViewSql_QuotesTargetAndPreservesNamedProjection(bool postgres)
    {
        using var db = CreateContext(postgres);
        var query = db.From<Source>().Select(row => new { Projected = row.Id });
        var sql = ((IMutationExecutor)db).Render(new CreateTempViewCommand(typeof(Source), "Odd.\"View", query));

        sql.Should().StartWith("create temp view \"Odd.\"\"View\" as select ");
        sql.Should().ContainEquivalentOf("Projected");
        sql.Should().NotContain("unprojected_value");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToTempView_RejectsCapturedValuesBeforeOpeningConnection(bool postgres)
    {
        using var db = CreateContext(postgres);
        var threshold = 23;
        var act = () => db.From<Source>().Where(row => row.Id > threshold).ToTempView("unused_view");

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*temporary view*bound SQL parameters*");
        db.GetConnection().State.Should().Be(System.Data.ConnectionState.Closed);
    }

    private static DataContext CreateContext(bool postgres) => postgres
        ? new PostgresDataContext("Host=127.0.0.1;Port=1;Database=unused", new DataContextBuilder())
        : new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());

    [SqlTable("source_rows")]
    private sealed class Source
    {
        public int Id { get; set; }

        [Column("unprojected_value")]
        public int Other { get; set; }
    }
}
