using System.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.Sqlite;
using NextORM.SqlServer;

namespace NextORM.Postgres.Tests;

/// <summary>Verifies the unified core vector-search surface and its PostgreSQL pgvector rendering.</summary>
public sealed class UnifiedVectorSearchTests
{
    private const string PlaceholderSqlServer = "Server=localhost;Database=nextorm;User Id=sa;Password=Nextorm-test-only-123!;TrustServerCertificate=true";
    private const string PlaceholderMySql = "Server=localhost;Database=nextorm;User ID=nextorm;Password=nextorm";
    private const string PlaceholderClickHouse = "Host=localhost;Port=8123;Database=default;Username=default;Password=";

    [SqlTable("unified_vector_search_probe")]
    public sealed class VectorSearchProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("embedding")]
        public Vector Embedding { get; set; } = default!;

        [Column("pg_embedding")]
        public PgVector PgEmbedding { get; set; } = default!;
    }

    private static DbPreparedQueryCommand<T> Prepared<T>(IDataContext context, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static void AssertMaterializesResult(DbPreparedQueryCommand<VectorSearchResult<VectorSearchProbe>> prepared)
    {
        prepared.DbCommandParams.Count.Should().Be(1);
        prepared.DbCommandParams[0].ParameterName.Should().Contain("queryVector");
        prepared.DbCommandParams[0].Value.Should().NotBe(DBNull.Value);

        var table = new DataTable();
        table.Columns.Add("id", typeof(int));
        table.Columns.Add("embedding", typeof(string));
        table.Columns.Add("pg_embedding", typeof(string));
        table.Columns.Add("distance", typeof(double));
        table.Rows.Add(7, "[1,2,3]", "[4,5,6]", 0.25d);

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();
        prepared.MapDelegate.Should().NotBeNull();
        var result = prepared.MapDelegate!(reader);

        result.Entity.Id.Should().Be(7);
        result.Entity.Embedding.ToArray().Should().Equal(1f, 2f, 3f);
        result.Entity.PgEmbedding.ToArray().Should().Equal(4f, 5f, 6f);
        result.Distance.Should().Be(0.25d);
    }

    [Fact]
    public void VectorDistance_ShouldRenderEachPgvectorMetricForTheSharedVector()
    {
        using var context = PostgresTestContext.Create();
        var target = new Vector(new[] { 1f, 2f, 3f });

        var cosine = Normalize(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Cosine))).DbCommand.CommandText);
        var euclidean = Normalize(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.Euclidean))).DbCommand.CommandText);
        var dotProduct = Normalize(Prepared(context, context.From<VectorSearchProbe>()
            .Select(row => SqlFunctions.VectorDistance(row.Embedding, target, VectorDistanceMetric.DotProduct))).DbCommand.CommandText);

        cosine.Should().Be("select (embedding <=> cast(@target as vector))\n from unified_vector_search_probe");
        euclidean.Should().Be("select (embedding <-> cast(@target as vector))\n from unified_vector_search_probe");
        dotProduct.Should().Be("select (embedding <#> cast(@target as vector))\n from unified_vector_search_probe");
    }

    [Theory]
    [InlineData(VectorSearchMode.Exact)]
    [InlineData(VectorSearchMode.Approximate)]
    public void VectorSearch_ShouldRenderOrderedTopKAndEntityDistance(VectorSearchMode mode)
    {
        using var context = PostgresTestContext.Create();
        var target = PgVector.Parse("[1,2,3]");
        var source = context.From<VectorSearchProbe>().OrderBy(row => row.Id).Page(50, 7);

        var prepared = Prepared(context,
            source.VectorSearch(row => row.PgEmbedding, target, VectorDistanceMetric.Cosine, 4, mode));
        AssertMaterializesResult(prepared);
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Be("select id, CAST(embedding AS text), CAST(pg_embedding AS text) as \"PgEmbedding\", (pg_embedding <=> cast(@queryVector as vector)) as \"distance\"\n from unified_vector_search_probe\n order by (pg_embedding <=> cast(@queryVector as vector))\nlimit 4");
    }

    [Fact]
    public void VectorSearch_ShouldAcceptCoreVectorAndPgvectorAdapters()
    {
        using var context = PostgresTestContext.Create();
        Vector shared = PgVector.Parse("[1,2,3]");
        PgVector providerVector = shared;

        shared.Dimensions.Should().Be(3);
        providerVector.ToArray().Should().Equal(new[] { 1f, 2f, 3f });

        var prepared = Prepared(context, context.From<VectorSearchProbe>()
            .VectorSearch<Vector>(row => row.Embedding, providerVector, VectorDistanceMetric.Euclidean, 2));
        AssertMaterializesResult(prepared);
        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Contain("(embedding <-> cast(@queryVector as vector))");
    }

    [Fact]
    public void VectorSearch_ShouldRejectUnsupportedDialectsDuringPreparation()
    {
        using var sqlite = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        using var sqlServer = new SqlServerDataContext(PlaceholderSqlServer, new DataContextBuilder());
        using var mySql = new MySqlDataContext(PlaceholderMySql, new DataContextBuilder());
        using var mariaDb = new MariaDbDataContext(PlaceholderMySql, new DataContextBuilder());
        using var clickHouse = new ClickHouseDataContext(PlaceholderClickHouse, new DataContextBuilder());

        AssertRejected(sqlite, "Vector distance and nearest-neighbor search are not supported by the SqliteDialect provider.");
        AssertRejected(sqlServer, "The VECTOR_DISTANCE function requires SQL Server major version 17 or later; declared major version: unspecified.");
        AssertRejected(mySql, "Vector distance and nearest-neighbor search are not supported by the MySqlDialect provider.");
        AssertRejected(mariaDb, "Vector distance and nearest-neighbor search are not supported by the MariaDbDialect provider.");
        AssertRejected(clickHouse, "Vector distance and nearest-neighbor search are not supported by the ClickHouseDialect provider.");
    }

    [Fact]
    public void VectorSearch_ShouldRejectInMemoryAtCallTime()
    {
        using var inMemory = new InMemoryDataContext();

        FluentActions.Invoking(() => inMemory.From<VectorSearchProbe>().VectorSearch(
                row => row.Embedding,
                new Vector(new[] { 1f, 2f }),
                VectorDistanceMetric.Cosine,
                2,
                VectorSearchMode.Approximate))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage("Vector distance and nearest-neighbor search are not supported by the InMemoryDialect provider.");
    }

    [Fact]
    public void VectorSearch_ShouldRejectNullEmptyAndInvalidTopK()
    {
        using var context = PostgresTestContext.Create();
        var source = context.From<VectorSearchProbe>();
        var empty = new Vector(Array.Empty<float>());
        var target = new Vector(new[] { 1f });

        FluentActions.Invoking(() => source.VectorSearch(row => row.Embedding, null!, VectorDistanceMetric.Cosine, 1))
            .Should().ThrowExactly<ArgumentNullException>().WithParameterName("queryVector");
        FluentActions.Invoking(() => source.VectorSearch(row => row.Embedding, empty, VectorDistanceMetric.Cosine, 1))
            .Should().ThrowExactly<ArgumentException>().WithMessage("The query vector must have at least one dimension.*");
        FluentActions.Invoking(() => source.VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, 0))
            .Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName("topK");
        FluentActions.Invoking(() => source.VectorSearch(row => row.Embedding, target, VectorDistanceMetric.Cosine, -1))
            .Should().ThrowExactly<ArgumentOutOfRangeException>().WithParameterName("topK");
    }

    private static void AssertRejected(IDataContext context, string refusal)
    {
        var query = context.From<VectorSearchProbe>().VectorSearch(
            row => row.Embedding,
            new Vector(new[] { 1f, 2f }),
            VectorDistanceMetric.Cosine,
            2,
            VectorSearchMode.Approximate);

        FluentActions.Invoking(() => context.GetPreparedQueryCommand(query, false, false, CancellationToken.None))
            .Should().ThrowExactly<NotSupportedException>()
            .WithMessage(refusal);
    }
}
