using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

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

    [Theory]
    [InlineData(false, "temp")]
    [InlineData(true, "pg_temp")]
    public void AsTempView_RendersCreateReadAndCleanupWithoutOpeningConnection(bool postgres, string schema)
    {
        using var db = CreateContext(postgres);
        var definition = db.From<Source>().Select(row => new { Projected = row.Id });
        var view = definition.AsTempView();
        var otherView = definition.AsTempView();
        var query = db.From(view).Select(t => t.GetInt32("projected"));

        view.Name.Should().NotBe(otherView.Name);
        var statements = query.ToBatchSql().Split(';', StringSplitOptions.TrimEntries);
        statements.Should().HaveCount(4);
        statements[1].Should().StartWith($"create temp view \"{view.Name}\" as select ");
        statements[1].Should().ContainEquivalentOf("projected").And.NotContain("unprojected_value");
        statements[2].Should().StartWith("select ").And.Contain(view.Name);
        statements[3].Should().Be($"drop view if exists \"{schema}\".\"{view.Name}\"");
        db.GetConnection().State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AsTempView_RejectsParametersInDefinitionBeforeOpeningConnection(bool postgres)
    {
        using var db = CreateContext(postgres);
        var threshold = 23;
        var view = db.From<Source>().Where(row => row.Id > threshold).Select(row => new { row.Id }).AsTempView();
        var query = db.From(view).Select(t => t.GetInt32("id"));

        FluentActions.Invoking(() => query.ToList()).Should().Throw<NotSupportedException>()
            .WithMessage("*temporary view*bound SQL parameters*");
        db.GetConnection().State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void AsTempView_RejectsUnsupportedProviderBeforeOpeningConnection()
    {
        using var db = new SqlServerDataContext(
            "Server=127.0.0.1,1;Database=unused;Integrated Security=true", new DataContextBuilder());
        var view = db.From<Source>().AsTempView();
        var query = db.From(view).Select(t => t.GetInt32("id"));

        FluentActions.Invoking(() => query.ToList()).Should().Throw<NotSupportedException>()
            .WithMessage("*only by SQLite and PostgreSQL*");
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
