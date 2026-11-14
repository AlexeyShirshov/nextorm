using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

/// <summary>Executes SQL Server 2025 vector binding, exact distance and approximate search live.</summary>
[Collection("SqlServer")]
public sealed class SqlServerVectorSearchIntegrationTests(ITestOutputHelper output) : ProviderTestSuite
{
    private const string Table = "sqlserver_vector_probe";

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    [SqlTable(Table)]
    public sealed class VectorSearchProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("embedding")]
        public Vector Embedding { get; set; } = default!;
    }

    [Fact]
    public void SqlServer2025VectorSearch_ShouldRoundTripAndExecuteExactAndApproximateModes()
    {
        var database = $"d392_vector_search_{Guid.NewGuid():N}";
        CreateDatabase(database);
        var connectionString = ConnectionStringForDatabase(database);

        try
        {
            LogServerVersion(connectionString);
            EnablePreviewFeatures(connectionString);
            Execute(connectionString,
                $"drop table if exists dbo.{Table}; create table dbo.{Table} (id int primary key, embedding vector(3) not null);");

            using var context = new SqlServer17Context(connectionString);
            var rows = CreateRows();
            context.CreateInsertBuilder<VectorSearchProbe>().Values(rows).Insert();
            context.From<VectorSearchProbe>().Count().Should().Be(100);

            var materialized = context.From<VectorSearchProbe>().Where(row => row.Id == 1).First();
            materialized.Embedding.ToArray().Should().Equal(1f, 2f, 3f);

            var queryVector = new Vector([1f, 2f, 3f]);
            var expectedSecondCosine = 1d - 15d / Math.Sqrt(17d * 14d);
            var expectedFifthCosine = 1d - 3d / Math.Sqrt(14d);

            var exactCosine = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, queryVector, VectorDistanceMetric.Cosine, 3, mode: VectorSearchMode.Exact)
                .ToList();
            exactCosine.Should().HaveCount(3);
            exactCosine.Select(result => result.Entity.Id).Should().Equal(1, 2, 5);
            exactCosine[0].Distance!.Value.Should().BeApproximately(0d, 0.00001d);
            exactCosine[1].Distance!.Value.Should().BeApproximately(expectedSecondCosine, 0.00001d);
            exactCosine[2].Distance!.Value.Should().BeApproximately(expectedFifthCosine, 0.00001d);

            var exactDotProduct = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, queryVector, VectorDistanceMetric.DotProduct, 3, mode: VectorSearchMode.Exact)
                .ToList();
            exactDotProduct.Should().HaveCount(3);
            exactDotProduct.Select(result => result.Entity.Id).Should().Equal(2, 1, 5);
            exactDotProduct.Select(result => result.Distance!.Value).Should().Equal(-15d, -14d, -3d);

            var exactEuclidean = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, queryVector, VectorDistanceMetric.Euclidean, 3, mode: VectorSearchMode.Exact)
                .ToList();
            exactEuclidean.Should().HaveCount(3);
            exactEuclidean.Select(result => result.Entity.Id).Should().Equal(1, 2, 5);
            exactEuclidean.Select(result => result.Distance!.Value).Should().Equal(0d, 1d, 3d);

            var scalarCosine = context.From<VectorSearchProbe>().Where(row => row.Id == 2)
                .Select(row => SqlFunctions.VectorDistance(row.Embedding, queryVector, VectorDistanceMetric.Cosine)).First();
            var scalarDotProduct = context.From<VectorSearchProbe>().Where(row => row.Id == 2)
                .Select(row => SqlFunctions.VectorDistance(row.Embedding, queryVector, VectorDistanceMetric.DotProduct)).First();
            var scalarEuclidean = context.From<VectorSearchProbe>().Where(row => row.Id == 2)
                .Select(row => SqlFunctions.VectorDistance(row.Embedding, queryVector, VectorDistanceMetric.Euclidean)).First();
            scalarCosine.Should().NotBeNull();
            scalarCosine!.Value.Should().BeApproximately(expectedSecondCosine, 0.00001d);
            scalarDotProduct.Should().Be(-15d);
            scalarEuclidean.Should().Be(1d);

            Execute(connectionString,
                $"create vector index ix_{Table}_embedding_cosine on dbo.{Table} (embedding) with (metric = 'cosine', type = 'DiskANN');");
            output.WriteLine("SQL Server vector index created: 100 non-null VECTOR(3) rows, cosine DiskANN.");
            output.WriteLine("The cosine DiskANN index is created before the approximate VECTOR_SEARCH query; this SQL Server grammar requires a matching ANN index.");

            const int topK = 3;
            var approximateQuery = context.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, queryVector, VectorDistanceMetric.Cosine, topK, mode: VectorSearchMode.Approximate);
            var approximatePrepared = Prepared(context, approximateQuery);
            output.WriteLine("SQL Server approximate VECTOR_SEARCH emitted SQL BEGIN\n" + approximatePrepared.DbCommand.CommandText + "\nSQL Server approximate VECTOR_SEARCH emitted SQL END");
            var approximate = approximateQuery.ToList();
            approximate.Should().HaveCount(topK);
            approximate.Select(result => result.Entity.Id).Should().Equal(1, 2, 5);
            approximate[0].Distance!.Value.Should().BeApproximately(0d, 0.00001d);
            approximate[1].Distance!.Value.Should().BeApproximately(expectedSecondCosine, 0.00001d);
            approximate[2].Distance!.Value.Should().BeApproximately(expectedFifthCosine, 0.00001d);
            approximate.Select(result => result.Distance!.Value).Should().BeInAscendingOrder();

            using var sqlServer16 = new SqlServer16Context(connectionString);
            var unsupportedQuery = sqlServer16.From<VectorSearchProbe>()
                .VectorSearch(row => row.Embedding, queryVector, VectorDistanceMetric.Cosine, topK, mode: VectorSearchMode.Approximate);
            FluentActions.Invoking(() => Prepared(sqlServer16, unsupportedQuery))
                .Should().ThrowExactly<NotSupportedException>()
                .Which.Message.Should().Contain("requires SQL Server major version 17 or later; declared major version: 16");

            output.WriteLine(
                $"SQL Server exact cosine ids=[{string.Join(",", exactCosine.Select(result => result.Entity.Id))}], "
                + $"dot ids=[{string.Join(",", exactDotProduct.Select(result => result.Entity.Id))}], "
                + $"euclidean ids=[{string.Join(",", exactEuclidean.Select(result => result.Entity.Id))}], "
                + $"approximate ids=[{string.Join(",", approximate.Select(result => result.Entity.Id))}], "
                + $"approximate distances=[{string.Join(",", approximate.Select(result => result.Distance!.Value.ToString("R", CultureInfo.InvariantCulture)))}]");
        }
        finally
        {
            DropDatabase(database);
        }
    }

    private static List<VectorSearchProbe> CreateRows()
    {
        var rows = new List<VectorSearchProbe>(100)
        {
            new() { Id = 1, Embedding = new Vector([1f, 2f, 3f]) },
            new() { Id = 2, Embedding = new Vector([2f, 2f, 3f]) },
            new() { Id = 3, Embedding = new Vector([1f, 0f, 0f]) },
            new() { Id = 4, Embedding = new Vector([0f, 1f, 0f]) },
            new() { Id = 5, Embedding = new Vector([0f, 0f, 1f]) }
        };

        for (var id = 6; id <= 100; id++)
        {
            var coordinate = (float)id;
            rows.Add(new VectorSearchProbe { Id = id, Embedding = new Vector([coordinate, -coordinate, -coordinate]) });
        }

        return rows;
    }

    private void EnablePreviewFeatures(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        try
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "ALTER DATABASE SCOPED CONFIGURATION SET PREVIEW_FEATURES = ON;";
                var affected = command.ExecuteNonQuery();

                using var configCommand = connection.CreateCommand();
                configCommand.CommandText = "SELECT CAST(value AS int) FROM sys.database_scoped_configurations WHERE name = 'PREVIEW_FEATURES';";
                var configuredValue = Convert.ToInt32(configCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
                output.WriteLine(
                    $"SQL Server PREVIEW_FEATURES actual response: ALTER accepted (ExecuteNonQuery={affected}); "
                    + $"sys.database_scoped_configurations.value={configuredValue}; database={connection.Database}.");
                configuredValue.Should().Be(1);
            }
        }
        catch (SqlException exception)
        {
            output.WriteLine("SQL Server PREVIEW_FEATURES actual response: " + exception);
            throw;
        }
    }

    private void LogServerVersion(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT @@VERSION, SERVERPROPERTY('ProductVersion');";
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        output.WriteLine("SQL Server @@VERSION: " + reader.GetString(0));
        output.WriteLine("SQL Server SERVERPROPERTY('ProductVersion'): " + Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture));
    }

    private static DbPreparedQueryCommand<T> Prepared<T>(SqlServerDataContext context, QueryCommand<T> command)
        => (DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ConnectionStringForDatabase(string database)
    {
        var builder = new SqlConnectionStringBuilder(SqlServerContainer.ConnectionString)
        {
            InitialCatalog = database
        };
        return builder.ConnectionString;
    }

    private static string MasterConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(SqlServerContainer.ConnectionString)
        {
            InitialCatalog = "master"
        };
        return builder.ConnectionString;
    }

    private static void CreateDatabase(string database)
        => Execute(MasterConnectionString(), $"create database [{database}];");

    private static void DropDatabase(string database)
        => Execute(MasterConnectionString(),
            $"alter database [{database}] set single_user with rollback immediate; drop database [{database}];");

    private sealed class SqlServer17Context(string connectionString)
        : SqlServerDataContext(new Version(17, 0), connectionString)
    {
    }

    private sealed class SqlServer16Context(string connectionString)
        : SqlServerDataContext(new Version(16, 0), connectionString)
    {
    }
}
