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

/// <summary>Verifies every pgvector marker is rejected during query preparation by non-PostgreSQL dialects.</summary>
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

        [Column("half_value")]
        PgHalfVector HalfValue { get; set; }

        [Column("sparse_value")]
        PgSparseVector SparseValue { get; set; }

        [Column("bits")]
        PgBit Bits { get; set; }
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
        foreach (var prepare in MarkerQueries(context))
            prepare.Should().ThrowExactly<NotSupportedException>().WithMessage(Refusal);
    }

    private static void AssertAllPrepared(IDataContext context)
    {
        foreach (var prepare in MarkerQueries(context))
            prepare.Should().NotThrow();
    }

    /// <summary>
    /// The whole pgvector marker surface: four distances on <c>vector</c>, the Hamming/Jaccard pair on
    /// <c>bit</c>, the half-precision/sparse norm and distance, the scalar functions and the element-wise
    /// operators. Each entry prepares its query against the supplied context.
    /// </summary>
    private static IEnumerable<Action> MarkerQueries(IDataContext context)
    {
        var target = PgVector.Parse("[1,2,3]");
        var halfTarget = new PgHalfVector(new[] { 1f, 2f, 3f });
        var sparseTarget = new PgSparseVector(3, new[] { 0, 2 }, new[] { 1f, 2f });
        var bitTarget = PgBit.Parse("10101");

        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_distance(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.cosine_distance(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.negative_inner_product(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l1_distance(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.hamming_distance(x.Bits, bitTarget)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.jaccard_distance(x.Bits, bitTarget)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_dims(x.Value)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_norm(x.Value)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_norm(x.HalfValue)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_normalize(x.Value)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_normalize(x.HalfValue)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.subvector(x.Value, 1, 2)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_add(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_subtract(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_multiply(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.vector_concat(x.Value, target)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.binary_quantize<PgVector, PgBit>(x.Value)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_distance(x.HalfValue, halfTarget)));
        yield return () => Prepare(context, context.From<IVectorMarker>().Select(x => SqlFunctions.Postgres.l2_distance(x.SparseValue, sparseTarget)));
    }

    private static void Prepare<T>(IDataContext context, QueryCommand<T> query)
        => context.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
}
