using System.Net.Http;
using ClickHouse.Driver.ADO;
using Testcontainers.ClickHouse;

namespace NextORM.Examples.ClickHouse.Analytics;

/// <summary>
/// Provisions the ClickHouse sample used by the demo queries: either an already running server (the
/// <c>CLICKHOUSE_ANALYTICS_CONNECTION</c> environment variable or <c>--connection</c>) or a throwaway
/// Testcontainers instance loaded with <c>hits_v1</c> (downloaded once and cached) plus the
/// <c>daily_unique_users_mv</c> materialized view.
/// </summary>
public sealed class DemoDatabase : IAsyncDisposable
{
    public const string ConnectionVariable = "CLICKHOUSE_ANALYTICS_CONNECTION";
    public const string DatasetUrlVariable = "CLICKHOUSE_HITS_URL";

    private const string FallbackConnectionVariable = "NEXTORM_DEMODB_CLICKHOUSE_CONNECTION";
    private const string DefaultDatasetUrl = "https://datasets.clickhouse.com/hits/tsv/hits_v1.tsv.xz";
    private const string DefaultImage = "clickhouse/clickhouse-server:25.8-alpine";
    private const string DatasetFile = "hits_v1.tsv.xz";

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "nextorm", "clickhouse");

    private readonly ClickHouseContainer? _container;

    private DemoDatabase(string connectionString, ClickHouseContainer? container)
    {
        ConnectionString = connectionString;
        _container = container;
    }

    public string ConnectionString { get; }

    public static async Task<DemoDatabase> Start(string? explicitConnectionString, CancellationToken ct)
    {
        var external = explicitConnectionString
            ?? Environment.GetEnvironmentVariable(ConnectionVariable)
            ?? Environment.GetEnvironmentVariable(FallbackConnectionVariable);

        if (!string.IsNullOrWhiteSpace(external))
        {
            Console.WriteLine("[analytics] using the provided ClickHouse connection string");
            return new DemoDatabase(external, null);
        }

        var dataset = await EnsureDataset(ct).ConfigureAwait(false);

        var container = new ClickHouseBuilder(DefaultImage)
            .WithEnvironment("TZ", "UTC")
            .WithResourceMapping(dataset, "/tmp/")
            .Build();

        Console.WriteLine($"[analytics] starting {DefaultImage} ...");
        await container.StartAsync(ct).ConfigureAwait(false);

        var connectionString = container.GetConnectionString();
        await CreateSchema(connectionString, ct).ConfigureAwait(false);

        Console.WriteLine($"[analytics] loading {Path.GetFileName(dataset)} (large, this takes several minutes) ...");
        var result = await container.ExecAsync(
            new[] { "sh", "-c", $"unxz -c /tmp/{DatasetFile} | clickhouse-client --max_insert_block_size=100000 --query 'INSERT INTO datasets.hits_v1 FORMAT TSV'" },
            ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
            throw new InvalidOperationException($"Loading hits_v1 failed (exit {result.ExitCode}).{Environment.NewLine}{result.Stderr}");

        return new DemoDatabase(connectionString, container);
    }

    private static async Task<string> EnsureDataset(CancellationToken ct)
    {
        Directory.CreateDirectory(CacheDirectory);
        var target = Path.Combine(CacheDirectory, DatasetFile);
        if (File.Exists(target) && new FileInfo(target).Length > 0)
            return target;

        var url = Environment.GetEnvironmentVariable(DatasetUrlVariable) ?? DefaultDatasetUrl;
        Console.WriteLine($"[analytics] downloading {url} -> {target} (large, cached afterwards) ...");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var temporary = target + ".part";
        await using (var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
        await using (var destination = File.Create(temporary))
        {
            await source.CopyToAsync(destination, ct).ConfigureAwait(false);
        }

        File.Move(temporary, target, overwrite: true);
        return target;
    }

    private static async Task CreateSchema(string connectionString, CancellationToken ct)
    {
        await using var connection = new ClickHouseConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        foreach (var statement in SchemaStatements)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
            await _container.DisposeAsync().ConfigureAwait(false);
    }

    private static readonly string[] SchemaStatements =
    [
        "CREATE DATABASE IF NOT EXISTS datasets",

        """
        CREATE TABLE IF NOT EXISTS datasets.hits_v1 ( WatchID UInt64,  JavaEnable UInt8,  Title String,  GoodEvent Int16,  EventTime DateTime,  EventDate Date,  CounterID UInt32,  ClientIP UInt32,  ClientIP6 FixedString(16),  RegionID UInt32,  UserID UInt64,  CounterClass Int8,  OS UInt8,  UserAgent UInt8,  URL String,  Referer String,  URLDomain String,  RefererDomain String,  Refresh UInt8,  IsRobot UInt8,  RefererCategories Array(UInt16),  URLCategories Array(UInt16), URLRegions Array(UInt32),  RefererRegions Array(UInt32),  ResolutionWidth UInt16,  ResolutionHeight UInt16,  ResolutionDepth UInt8,  FlashMajor UInt8, FlashMinor UInt8,  FlashMinor2 String,  NetMajor UInt8,  NetMinor UInt8, UserAgentMajor UInt16,  UserAgentMinor FixedString(2),  CookieEnable UInt8, JavascriptEnable UInt8,  IsMobile UInt8,  MobilePhone UInt8,  MobilePhoneModel String,  Params String,  IPNetworkID UInt32,  TraficSourceID Int8, SearchEngineID UInt16,  SearchPhrase String,  AdvEngineID UInt8,  IsArtifical UInt8,  WindowClientWidth UInt16,  WindowClientHeight UInt16,  ClientTimeZone Int16,  ClientEventTime DateTime,  SilverlightVersion1 UInt8, SilverlightVersion2 UInt8,  SilverlightVersion3 UInt32,  SilverlightVersion4 UInt16,  PageCharset String,  CodeVersion UInt32,  IsLink UInt8,  IsDownload UInt8,  IsNotBounce UInt8,  FUniqID UInt64,  HID UInt32,  IsOldCounter UInt8, IsEvent UInt8,  IsParameter UInt8,  DontCountHits UInt8,  WithHash UInt8, HitColor FixedString(1),  UTCEventTime DateTime,  Age UInt8,  Sex UInt8,  Income UInt8,  Interests UInt16,  Robotness UInt8,  GeneralInterests Array(UInt16), RemoteIP UInt32,  RemoteIP6 FixedString(16),  WindowName Int32,  OpenerName Int32,  HistoryLength Int16,  BrowserLanguage FixedString(2),  BrowserCountry FixedString(2),  SocialNetwork String,  SocialAction String,  HTTPError UInt16, SendTiming Int32,  DNSTiming Int32,  ConnectTiming Int32,  ResponseStartTiming Int32,  ResponseEndTiming Int32,  FetchTiming Int32,  RedirectTiming Int32, DOMInteractiveTiming Int32,  DOMContentLoadedTiming Int32,  DOMCompleteTiming Int32,  LoadEventStartTiming Int32,  LoadEventEndTiming Int32, NSToDOMContentLoadedTiming Int32,  FirstPaintTiming Int32,  RedirectCount Int8, SocialSourceNetworkID UInt8,  SocialSourcePage String,  ParamPrice Int64, ParamOrderID String,  ParamCurrency FixedString(3),  ParamCurrencyID UInt16, GoalsReached Array(UInt32),  OpenstatServiceName String,  OpenstatCampaignID String,  OpenstatAdID String,  OpenstatSourceID String,  UTMSource String, UTMMedium String,  UTMCampaign String,  UTMContent String,  UTMTerm String, FromTag String,  HasGCLID UInt8,  RefererHash UInt64,  URLHash UInt64,  CLID UInt32,  YCLID UInt64,  ShareService String,  ShareURL String,  ShareTitle String,  ParsedParams Nested(Key1 String,  Key2 String, Key3 String, Key4 String, Key5 String,  ValueDouble Float64),  IslandID FixedString(16),  RequestNum UInt32,  RequestTry UInt8) ENGINE = MergeTree() PARTITION BY toYYYYMM(EventDate) ORDER BY (CounterID, EventDate, intHash32(UserID)) SAMPLE BY intHash32(UserID)
        """,

        // Stand-specific: not part of the public sample.
        """
        CREATE MATERIALIZED VIEW IF NOT EXISTS datasets.daily_unique_users_mv
        ENGINE = AggregatingMergeTree() ORDER BY EventDate
        AS SELECT EventDate, uniqState(UserID) AS users_state
        FROM datasets.hits_v1
        GROUP BY EventDate
        """
    ];
}
