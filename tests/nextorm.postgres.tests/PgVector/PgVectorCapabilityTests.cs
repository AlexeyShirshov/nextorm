using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;
using NextORM.MySql;
using NextORM.MariaDb;
using NextORM.ClickHouse;

namespace NextORM.Postgres.Tests;

/// <summary>Verifies pgvector markers are rejected during query preparation by non-PostgreSQL dialects.</summary>
public sealed class PgVectorCapabilityTests
{
    private const string Refusal = "pgvector functions require PostgreSQL.";
    private const string PlaceholderSqlServer = "Server=localhost;Database=nextorm;User Id=sa;Password=Nextorm-test-only-123!;TrustServerCertificate=true";
    private const string PlaceholderMySql = "Server=localhost;Database=nextorm;User ID=nextorm;Password=nextorm";
    private const string PlaceholderClickHouse = "Host=localhost;Port=8123;Database=default;Username=default;Password=";
    private const string PlaceholderPostgres = "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    [SqlTable("pgvector_capability_probe")]
    public interface IVectorMarker
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("value")]
        PgVector Value { get; set; }
    }

    [Fact]
    public void NonPostgresDialects_ShouldRejectEveryVectorMarkerDuringPreparation()
    {
        using var sqlite = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(PlaceholderSqlServer, new DataContextBuilder());
        using var mySql = new MySqlDataContext(PlaceholderMySql, new DataContextBuilder());
        using var mariaDb = new MariaDbDataContext(PlaceholderMySql, new DataContextBuilder());
        using var clickHouse = new ClickHouseDataContext(PlaceholderClickHouse, new DataContextBuilder());

        AssertAllRejected(sqlite);
        AssertAllRejected(sqlServer);
        AssertAllRejected(mySql);
        AssertAllRejected(mariaDb);
        AssertAllRejected(clickHouse);
    }

    [Fact]
    public void PostgresDialect_ShouldAdvertiseAndPrepareEveryVectorMarker()
    {
        using var postgres = new PostgresDataContext(PlaceholderPostgres, new DataContextBuilder());
        postgres.Dialect.SupportsExtendedScalarFunctions.Should().BeTrue();

        AssertAllPrepared(postgres);
    }

    private static void AssertAllRejected(IDataContext context)
    {
        var target = PgVector.Parse("[1,2,3]");
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_distance(x.Value, target)));
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.cosine_distance(x.Value, target)));
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target)));
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l1_distance(x.Value, target)));
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_dims(x.Value)));
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_norm(x.Value)));
        AssertRejected(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_normalize(x.Value)));
    }

    private static void AssertAllPrepared(IDataContext context)
    {
        var target = PgVector.Parse("[1,2,3]");
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_distance(x.Value, target)));
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.cosine_distance(x.Value, target)));
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target)));
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l1_distance(x.Value, target)));
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_dims(x.Value)));
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_norm(x.Value)));
        AssertPrepared(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_normalize(x.Value)));
    }

    private static void AssertRejected<T>(IDataContext context, QueryCommand<T> query)
    {
        Action prepare = () => context.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
        prepare.Should().ThrowExactly<NotSupportedException>().WithMessage(Refusal);
    }

    private static void AssertPrepared<T>(IDataContext context, QueryCommand<T> query)
    {
        var prepared = context.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
        prepared.Should().NotBeNull();
    }
}
