using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;

namespace nextorm.core.benchmark;

[MemoryDiagnoser]
public class SqliteBenchmarkLargeIteration
{
    private readonly TestDataContext _ctx;
    private readonly QueryCommand<LargeEntity> _cmd;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly SqliteCommand _adoCmd;

    public SqliteBenchmarkLargeIteration(bool withLogging = false)
    {
        var builder = new DataContextOptionsBuilder();
        builder.UseSqlite(@$"{Directory.GetCurrentDirectory()}\data\test.db");
        if (withLogging)
        {
            builder.UseLoggerFactory(LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug)));
            builder.LogSensetiveData(true);
        }
        _ctx = new TestDataContext(builder);

        _cmd = _ctx.LargeEntity.Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt });
        (_ctx.DataProvider as SqlDataProvider)!.Compile(_cmd, CancellationToken.None);

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Directory.GetCurrentDirectory()}\data\test.db");

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataProvider)_ctx.DataProvider).ConnectionString);

        _adoCmd = _conn.CreateCommand();
        _adoCmd.CommandText = "select id, someString, dt from large_table";
    }
    // public async Task FillLargeTable()
    // {
    //     var r = new Random(Environment.TickCount);

    //     for(var i=0;i<10_000;i++)
    //         await _conn.ExecuteAsync("insert into large_table(someString,dt) values(@str,@dt)", new {str=Guid.NewGuid().ToString(),dt=DateTime.Now.AddDays(r.Next(-10,10))});
    // }
    // [Benchmark()]
    // public async Task NextormCompiledAsync()
    // {
    //     await foreach (var row in _cmd)
    //     {
    //     }
    // }
    // [Benchmark(Baseline = true)]
    // public async Task NextormCompiled()
    // {
    //     foreach (var row in await _cmd.Get())
    //     {
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCompiledToList()
    // {
    //     foreach (var row in (await _cmd.Get()).ToList())
    //     {
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCached()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }))
    //     {
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCachedFetch()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }).Fetch())
    //     {
    //     }
    // }
    // [Benchmark()]
    // public async Task NextormCachedToList()
    // {
    //     foreach (var row in await _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
    //     {
    //     }
    // }
    // [Benchmark]
    // public async Task EFCore()
    // {
    //     foreach (var row in await _efCtx.LargeEntities.Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
    //     {
    //     }
    // }
    // [Benchmark]
    // public async Task Dapper()
    // {
    //     foreach (var row in await _conn.QueryAsync<LargeEntity>("select id, someString, dt from large_table"))
    //     {
    //     }
    // }
    [Benchmark]
    public async Task IterateManual()
    {
        //Console.WriteLine("start method");
        // if (_conn.State != System.Data.ConnectionState.Open)
        // {
        //     await _conn.OpenAsync();
        // }
        // using var reader = await _adoCmd.ExecuteReaderAsync();
        //var l = new List<LargeEntity>();
        // while (await reader.ReadAsync())
        // {
        //     var o = cq.MapDelegate(reader);
        // }
        using var ee = (ResultSetEnumerator<LargeEntity>)((SqlDataProvider.SqlCacheEntry)_cmd.CacheEntry).Enumerator!;
        await ee.InitReaderAsync(CancellationToken.None);
        while (ee!.MoveNext())
        {
            var o = ee.Current;
            //      Console.WriteLine(o.Id);
        }

        //Console.WriteLine("end method");
    }
    [Benchmark(Baseline = true)]
    public async Task AdoWithDelegate()
    {
        if (_conn.State != System.Data.ConnectionState.Open)
        {
            await _conn.OpenAsync();
        }
        using var reader = await _adoCmd.ExecuteReaderAsync();
        //var l = new List<LargeEntity>();
        //var buf = new object[3];
        var cq = (DatabaseCompiledQuery<LargeEntity>)_cmd.CacheEntry.CompiledQuery;
        while (reader.Read())
        {
            var o = cq.MapDelegate(reader);
            //reader.GetValues(buf);
            // var Id = reader.GetInt64(0);
            // var Str = reader.GetString(1);
            // DateTime? Dt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            // var o = new LargeEntity() { Id = Id, Str = Str, Dt = Dt };
            // o.Id = (long)buf[0];
            // o.Str = (string)buf[1];
            // o.Dt = Convert.ToDateTime(buf[2]);
            // l.Add(o);
        }
    }
}
