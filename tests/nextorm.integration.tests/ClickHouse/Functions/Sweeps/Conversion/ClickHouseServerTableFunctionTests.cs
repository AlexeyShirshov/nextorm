using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("ClickHouse")]
public sealed class ClickHouseServerTableFunctionTests : ProviderTestSuite
{
    internal static FileInfo CsvFixture => new(Path.Combine(
        AppContext.BaseDirectory, "ClickHouse", "Fixtures", "TableFunctions", CsvName));

    private const string CsvName = "nextorm-tvf-129.csv";
    private const string Format = "CSV";
    private const string Structure = "id Int32, name String";
    private const string S3ObjectUrlVariable = "NEXTORM_CLICKHOUSE_TVF_S3_URL";

    protected override ITestProvider Provider => ClickHouseTestProvider.Instance;

    [Fact]
    public void Remote_ShouldReadRowsFromAddressedTable() =>
        CheckSeededTable(static (context, database, table) => context
            .From(() => SqlFunctions.ClickHouse.remote<ISourceRow>("127.0.0.1:9000", database, table)));

    [Fact]
    public void Cluster_ShouldReadRowsThroughNamedCluster() =>
        CheckSeededTable(static (context, database, table) => context
            .From(() => SqlFunctions.ClickHouse.cluster<ISourceRow>("default", database, table)));

    [Fact]
    public void File_ShouldReadRowsFromServerFile()
    {
        using var context = Provider.CreateContext();
        ReadRows(context.From(() => SqlFunctions.ClickHouse.file<ISourceRow>(CsvName, Format, Structure)))
            .Should().Equal((18, "alder"), (53, "willow"));
    }

    [Fact]
    public async Task S3_ShouldReadRowsFromObjectStore()
    {
        using var context = Provider.CreateContext();
        if (ClickHouseContainer.IsExternallyConfigured)
        {
            var url = Environment.GetEnvironmentVariable(S3ObjectUrlVariable)
                ?? throw new InvalidOperationException(
                    $"Set {S3ObjectUrlVariable} to the test CSV's S3 object URL as seen by ClickHouse.");
            CheckS3Rows(context, url);
            return;
        }

        // Both containers use the test runtime's bridge. Do not publish the disposable object
        // store to the host. Seed it through its own HTTP S3 API, independently of NextORM.
        const string bucketPath = "/nextorm";
        const string objectPath = "/nextorm/" + CsvName;
        const string readPolicy = """
            {"Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":"*",
            "Action":"s3:GetObject","Resource":"arn:aws:s3:::nextorm/nextorm-tvf-129.csv"}]}
            """;
        var accessKey = Guid.NewGuid().ToString("N");
        var secretKey = Guid.NewGuid().ToString("N");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        await using var store = new ContainerBuilder("chrislusf/seaweedfs:4.17")
            .WithEnvironment("AWS_ACCESS_KEY_ID", accessKey)
            .WithEnvironment("AWS_SECRET_ACCESS_KEY", secretKey)
            .WithCommand("mini", "-dir=/data", "-master.volumeSizeLimitMB=64",
                "-master.telemetry=false", "-webdav=false", "-admin.ui=false")
            .WithCreateParameterModifier(parameters =>
            {
                parameters.HostConfig ??= new HostConfig();
                parameters.HostConfig.Memory = 512L * 1024 * 1024;
            })
            .WithResourceMapping(CsvFixture, "/tmp/")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(
                "All enabled components are running and ready to use:"))
            .Build();

        await store.StartAsync(timeout.Token);
        var endpoint = $"http://{store.IpAddress}:8333";
        await SeedStore("create the bucket", "-X", "PUT", endpoint + bucketPath);
        await SeedStore("upload the CSV", "--upload-file", "/tmp/" + CsvName, endpoint + objectPath);
        await SeedStore("allow ClickHouse to read the fixture without credentials",
            "-X", "PUT", "--data-binary", readPolicy, endpoint + bucketPath + "?policy");
        CheckS3Rows(context, endpoint + objectPath);

        async Task SeedStore(string operation, params string[] args)
        {
            var result = await store.ExecAsync(
                ["curl", "--noproxy", "*", "--fail-with-body", "--silent", "--max-time", "15",
                    "--aws-sigv4", "aws:amz:us-east-1:s3", "--user", $"{accessKey}:{secretKey}", .. args],
                timeout.Token);
            result.ExitCode.Should().Be(0, "the S3 fixture must {0}: {1}", operation, result.Stdout);
        }
    }

    private static void CheckS3Rows(IDataContext context, string url) =>
        ReadRows(context.From(() => SqlFunctions.ClickHouse.s3<ISourceRow>(url, Format, Structure)))
            .Should().Equal((18, "alder"), (53, "willow"));

    private void CheckSeededTable(Func<IDataContext, string, string, EntityBuilder<ISourceRow>> source)
    {
        using var context = (ClickHouseDataContext)Provider.CreateContext();
        using var connection = context.GetConnection();
        connection.Open();
        using var databaseCommand = connection.CreateCommand();
        databaseCommand.CommandText = "select currentDatabase()";
        var database = (string)databaseCommand.ExecuteScalar()!;

        var table = $"nextorm_tvf_129_{Guid.NewGuid():N}";
        Execute(connection, $"create table {table} (id Int32, name String) engine = Memory");
        try
        {
            Execute(connection, $"insert into {table} values (42, 'ember'), (7, 'snow')");
            ReadRows(source(context, database, table)).Should().Equal((7, "snow"), (42, "ember"));
        }
        finally
        {
            Execute(connection, $"drop table {table} sync");
        }
    }

    private static List<(int Id, string Name)> ReadRows(EntityBuilder<ISourceRow> source) => source
        .OrderBy(row => row.Id)
        .Select(row => new { row.Id, row.Name })
        .ToList()
        .ConvertAll(row => (row.Id, row.Name));

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public interface ISourceRow
    {
        [Column("id")]
        int Id { get; }

        [Column("name")]
        string Name { get; }
    }
}
