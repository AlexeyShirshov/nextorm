using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

            Execute($"create index ix_{Table}_pg_embedding_cosine on {Table} using hnsw (pg_embedding vector_cosine_ops)");

            var queryVector = new Vector([1f, 2f, 3f]);
            var exact = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, queryVector, VectorDistanceMetric.Euclidean, 3, mode: VectorSearchMode.Exact)
                .ToList();

            exact.Should().HaveCount(3);
            exact.Select(result => result.Entity.Id).Should().Equal(1, 2, 5);
            exact.Select(result => result.Distance!.Value).Should().Equal(0d, 1d, 3d);

            var queryPgVector = PgVector.Parse("[1,2,3]");
            var approximate = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.PgEmbedding, queryPgVector, VectorDistanceMetric.Cosine, 3, mode: VectorSearchMode.Approximate)
                .ToList();

            var expectedSecondCosine = 1d - 15d / Math.Sqrt(17d * 14d);
            var expectedFifthCosine = 1d - 3d / Math.Sqrt(14d);
            approximate.Should().HaveCount(3);
            approximate.Select(result => result.Entity.Id).Should().Equal(1, 2, 5);
            approximate[0].Distance!.Value.Should().BeApproximately(0d, 0.00001d);
            approximate[1].Distance!.Value.Should().BeApproximately(expectedSecondCosine, 0.00001d);
            approximate[2].Distance!.Value.Should().BeApproximately(expectedFifthCosine, 0.00001d);
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
}
