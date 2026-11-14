using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;
using Npgsql;

namespace NextORM.Integration.Tests;

/// <summary>Executes the unified vector-search API against the live pgvector integration provider.</summary>
[Collection("Postgres")]
public sealed class UnifiedVectorSearchIntegrationTests(ITestOutputHelper output) : ProviderTestSuite
{
    private const string Table = "unified_vector_search_probe";
    private const string HnswIndex = "ix_unified_vector_search_probe_pg_embedding_cosine";

    protected override ITestProvider Provider => PostgresTestProvider.Instance;

    [SqlTable(Table)]
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

    [Fact]
    public void UnifiedVectorSearch_ShouldExecuteExactAndApproximateWithCoreAndPgvectorAdapters()
    {
        var context = _sut.DataProvider;
        Execute(
            $"drop table if exists {Table}; "
            + $"create table {Table} (id integer primary key, embedding vector(3) not null, pg_embedding vector(3) not null)");

        try
        {
            var rows = new[]
            {
                new VectorSearchProbe { Id = 1, Embedding = new Vector([1f, 2f, 3f]), PgEmbedding = PgVector.Parse("[1,2,3]") },
                new VectorSearchProbe { Id = 2, Embedding = new Vector([2f, 2f, 3f]), PgEmbedding = PgVector.Parse("[2,2,3]") },
                new VectorSearchProbe { Id = 3, Embedding = new Vector([1f, 0f, 0f]), PgEmbedding = PgVector.Parse("[1,0,0]") },
                new VectorSearchProbe { Id = 4, Embedding = new Vector([0f, 1f, 0f]), PgEmbedding = PgVector.Parse("[0,1,0]") },
                new VectorSearchProbe { Id = 5, Embedding = new Vector([0f, 0f, 1f]), PgEmbedding = PgVector.Parse("[0,0,1]") }
            };

            context.CreateInsertBuilder<VectorSearchProbe>().Values(rows).Insert();

            var materialized = context.From<VectorSearchProbe>().Where(row => row.Id == 1).First();
            materialized.Embedding.ToArray().Should().Equal(1f, 2f, 3f);
            materialized.PgEmbedding.ToArray().Should().Equal(1f, 2f, 3f);

            Execute($"create index {HnswIndex} on {Table} using hnsw (pg_embedding vector_cosine_ops)");

            var queryVector = PgVector.Parse("[1,2,3]");
            var bruteForce = rows
                .Select(row => (row.Id, Distance: CosineDistance(row.PgEmbedding.ToArray(), queryVector.ToArray())))
                .OrderBy(result => result.Distance)
                .ThenBy(result => result.Id)
                .Take(3)
                .ToArray();
            var explainJson = ExplainExactSearch(queryVector);
            explainJson.Should().NotContain(HnswIndex);
            PersistExplainOutput(explainJson);
            output.WriteLine($"Exact PostgreSQL ANN-index observation: {HnswIndex} not present in EXPLAIN plan.");
            output.WriteLine($"Exact PostgreSQL EXPLAIN (FORMAT JSON): {explainJson}");

            var exact = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.PgEmbedding, queryVector, VectorDistanceMetric.Cosine, 3, mode: VectorSearchMode.Exact)
                .ToList();

            exact.Should().HaveCount(bruteForce.Length);
            exact.Select(result => result.Entity.Id).Should().Equal(bruteForce.Select(result => result.Id));
            for (var i = 0; i < bruteForce.Length; i++)
                exact[i].Distance!.Value.Should().BeApproximately(bruteForce[i].Distance, 0.00001d);

            var approximate = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.PgEmbedding, queryVector, VectorDistanceMetric.Cosine, 3, mode: VectorSearchMode.Approximate)
                .ToList();

            approximate.Should().HaveCount(bruteForce.Length);
            approximate.Select(result => result.Entity.Id).Should().Equal(bruteForce.Select(result => result.Id));
            for (var i = 0; i < bruteForce.Length; i++)
                approximate[i].Distance!.Value.Should().BeApproximately(bruteForce[i].Distance, 0.00001d);
            approximate.Select(result => result.Distance!.Value).Should().BeInAscendingOrder();

            output.WriteLine(
                $"PostgreSQL unified vector search: exact ids=[{string.Join(",", exact.Select(result => result.Entity.Id))}], "
                + $"approximate ids=[{string.Join(",", approximate.Select(result => result.Entity.Id))}], "
                + $"approximate distances=[{string.Join(",", approximate.Select(result => result.Distance!.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)))}]");
        }
        finally
        {
            Execute($"drop table if exists {Table}");
        }
    }

    private static void Execute(string sql)
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static double CosineDistance(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        var dot = 0d;
        var leftNorm = 0d;
        var rightNorm = 0d;
        for (var i = 0; i < left.Count; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        return 1d - dot / Math.Sqrt(leftNorm * rightNorm);
    }

    private static string ExplainExactSearch(PgVector queryVector)
    {
        using var connection = new NpgsqlConnection(PostgresContainer.ConnectionString);
        connection.Open();

        using (var settings = connection.CreateCommand())
        {
            settings.CommandText = "set enable_seqscan = off";
            settings.ExecuteNonQuery();
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"explain (format json) select id, cosine_distance(pg_embedding, cast(@queryVector as vector)) as distance from {Table} order by cosine_distance(pg_embedding, cast(@queryVector as vector)) limit 3";
        command.Parameters.AddWithValue("queryVector", queryVector.ToString());
        return Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("PostgreSQL returned no EXPLAIN JSON for the exact vector-search query.");
    }

    private static void PersistExplainOutput(string explainJson)
    {
        var outputPath = Environment.GetEnvironmentVariable("NEXTORM_PGVECTOR_EXPLAIN_OUTPUT");
        if (string.IsNullOrWhiteSpace(outputPath))
            return;

        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, explainJson);
    }
}
