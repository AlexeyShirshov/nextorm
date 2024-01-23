using BenchmarkDotNet.Attributes;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Linq.Expressions;
using System.Data.Common;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using nextorm.core;

namespace nextorm.benchmark;

[SimpleJob(RuntimeMoniker.Net80)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByJob, BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkLargeIteration
{
    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly IPreparedQueryCommand<LargeEntity> _cmdExec;
    private readonly IPreparedQueryCommand<LargeEntity> _cmdToList;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly SqliteCommand _adoCmd;
    private readonly Func<EFDataContext, IAsyncEnumerable<LargeEntity>> _efCompiled = EF.CompileAsyncQuery((EFDataContext ctx) => ctx.LargeEntities.Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }));
    private readonly ILoggerFactory? _logFactory;
    public SqliteBenchmarkLargeIteration(bool withLogging = false)
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db"));
        if (withLogging)
        {
            _logFactory = LoggerFactory.Create(config => config.AddConsole().SetMinimumLevel(LogLevel.Debug));
            builder.UseLoggerFactory(_logFactory);
            builder.LogSensitiveData(true);
        }
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        _db.EnsureConnectionOpen();

        _cmdExec = _ctx.LargeEntity.Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).Prepare(false);

        _cmdToList = _ctx.LargeEntity.Select(entity => new LargeEntity { Id = entity.Id, Str = entity.Str, Dt = entity.Dt }).Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db")}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        if (withLogging)
        {
            efBuilder.UseLoggerFactory(_logFactory);
            efBuilder.EnableSensitiveDataLogging(true);
        }

        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDbContext)_ctx.DbContext).ConnectionString);
        _conn.Open();

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
    // public async Task NextormPreparedAsync()
    // {
    //     await foreach (var row in _cmd)
    //     {
    //     }
    // }
    [Benchmark()]
    //[BenchmarkCategory("Stream")]
    public async Task Nextorm_Prepared_AsyncStream()
    {
        await foreach (var row in _cmdExec.ToAsyncEnumerable(_db))
        {
        }
    }
    [Benchmark()]
    //[BenchmarkCategory("Buffered")]
    public async Task Nextorm_Prepared_ToListAsync()
    {
        foreach (var row in await _cmdToList.ToListAsync(_db))
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormCachedAsync()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }))
    //     {
    //     }
    // }
    [Benchmark()]
    //[BenchmarkCategory("Stream")]
    public async Task Nextorm_Cached_AsyncStream()
    {
        await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToAsyncEnumerable())
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormCachedFetch()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }).Fetch())
    //     {
    //     }
    // }
    [Benchmark()]
    //[BenchmarkCategory("Buffered")]
    public async Task Nextorm_Cached_ToListAsync()
    {
        foreach (var row in await _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
        }
    }
    // [Benchmark()]
    // public async Task NextormCachedFetch()
    // {
    //     await foreach (var row in _ctx.LargeEntity.Select(entity => new { entity.Id, entity.Str, entity.Dt }).Fetch())
    //     {
    //     }
    // }
    [Benchmark]
    //[BenchmarkCategory("Buffered")]
    public async Task EFCore_ToListAsync()
    {
        foreach (var row in await _efCtx.LargeEntities.Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
        {
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Stream")]
    public async Task EFCore_AsyncStream()
    {
        await foreach (var row in _efCtx.LargeEntities.Select(entity => new { entity.Id, entity.Str, entity.Dt }).AsAsyncEnumerable())
        {
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Stream")]
    public async Task EFCore_Compiled_AsyncStream()
    {
        await foreach (var row in _efCompiled(_efCtx))
        {
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Buffered")]
    public async Task Dapper_Async()
    {
        foreach (var row in await _conn.QueryAsync<LargeEntity>("select id, someString as str, dt from large_table"))
        {
        }
    }
    [Benchmark]
    //[BenchmarkCategory("Stream")]
    public async Task Dapper_AsyncStream()
    {
        await foreach (var row in _conn.QueryUnbufferedAsync<LargeEntity>("select id, someString as str, dt from large_table"))
        {
        }
    }
    // [Benchmark]
    // public async Task IterateManual()
    // {
    //     //Console.WriteLine("start method");
    //     // if (_conn.State != System.Data.ConnectionState.Open)
    //     // {
    //     //     await _conn.OpenAsync();
    //     // }
    //     // using var reader = await _adoCmd.ExecuteReaderAsync();
    //     var l = new List<TupleLargeEntity>();
    //     // while (await reader.ReadAsync())
    //     // {
    //     //     var o = cq.MapDelegate(reader);
    //     // }
    //     using var ee = (ResultSetEnumerator<TupleLargeEntity>)((SqlDataProvider.SqlCacheEntry)_cmd.CacheEntry).Enumerator!;
    //     await ee.InitReaderAsync(CancellationToken.None);
    //     while (ee!.MoveNext())
    //     {
    //         var o = ee.Current;
    //         l.Add(o);
    //         //      Console.WriteLine(o.Id);
    //     }
    //     foreach (var row in l)
    //     {

    //     }

    //     //Console.WriteLine("end method");
    // }
    [Benchmark()]
    //[BenchmarkCategory("Buffered")]
    public async Task AdoWithDelegate()
    {
        if (_conn.State == System.Data.ConnectionState.Closed)
        {
            await _conn.OpenAsync();
        }
        using var reader = await _adoCmd.ExecuteReaderAsync(CommandBehavior.SingleResult);
        var l = new List<LargeEntity>();
        //var buf = new object[3];
        var cq = _cmdToList as DbPreparedQueryCommand<LargeEntity>;
        while (reader.Read())
        {
            var o = cq!.MapDelegate!(reader);
            //reader.GetValues(buf);
            // var Id = reader.GetInt64(0);
            // var Str = reader.GetString(1);
            // DateTime? Dt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            // var o = new LargeEntity() { Id = Id, Str = Str, Dt = Dt };
            // o.Id = (long)buf[0];
            // o.Str = (string)buf[1];
            // o.Dt = Convert.ToDateTime(buf[2]);
            l.Add(o);
        }
        foreach (var row in l)
        {

        }
    }
    // [Benchmark()]
    // public async Task AdoRead()
    // {
    //     if (_conn.State != System.Data.ConnectionState.Open)
    //     {
    //         await _conn.OpenAsync();
    //     }
    //     using var reader = await _adoCmd.ExecuteReaderAsync();
    //     while (reader.Read())
    //     {
    //     }
    // }
    // [Benchmark()]
    // public async Task AdoTuple()
    // {
    //     Expression<Func<IDataRecord, (long id, string? str, DateTime? dt)>> lambda = (IDataRecord record) => TupleFactory(record.GetInt64(0), record.GetString(1), record.IsDBNull(2) ? null : record.GetDateTime(2));
    //     //Expression<Func<IDataRecord, (long id, string? str, DateTime? dt)>> lambda2 = (IDataRecord record) => TupleFactory(record.GetInt64(0), record.GetString(1), record.IsDBNull(2) ? null : record.GetDateTime(2));
    //     var del = lambda.Compile();
    //     if (_conn.State != System.Data.ConnectionState.Open)
    //     {
    //         await _conn.OpenAsync();
    //     }
    //     using var reader = await _adoCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult);
    //     while (reader.Read())
    //     {
    //         var tuple = del(reader);
    //     }
    // }
    // static (long id, string? str, DateTime? dt) TupleFactory(long id, string? str, DateTime? dt) => (id, str, dt);
    // [Benchmark()]
    // public async Task AdoTupleRef()
    // {
    //     if (_conn.State != System.Data.ConnectionState.Open)
    //     {
    //         await _conn.OpenAsync();
    //     }
    //     using var reader = await _adoCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult);
    //     while (reader.Read())
    //     {
    //         (long id, string? str, DateTime? dt) tuple = default;
    //         TupleFactory(ref tuple, reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetDateTime(2));
    //     }

    //     void TupleFactory(ref (long id, string? str, DateTime? dt) tuple, long id, string? str, DateTime? dt)
    //     {
    //         tuple.id = id;
    //         tuple.str = str;
    //         tuple.dt = dt;
    //     }
    // }
    [Benchmark()]
    //[BenchmarkCategory("Buffered")]
    public void AdoTupleToList()
    {
        Expression<Func<ILargeEntity, (long id, string? str, DateTime? dt)>> lambda = record => new ValueTuple<long, string?, DateTime?>(record.Id, record.Str, record.Dt);
        if (_conn.State != System.Data.ConnectionState.Open)
        {
            _conn.Open();
        }
        var l = new List<(long id, string? str, DateTime? dt)>();
        var i = 0;
        using var reader = _adoCmd.ExecuteReader(CommandBehavior.SingleResult);
        while (reader.Read())
        {
            l.Add(default);
            var listSpan = CollectionsMarshal.AsSpan(l);
            TupleFactory(ref listSpan[i++], reader);
        }
    }
    //[Benchmark(Baseline = true)]
    //[BenchmarkCategory("Stream")]
    public async Task AdoTupleIteration()
    {
        //Expression<Func<ILargeEntity, (long id, string? str, DateTime? dt)>> lambda = record => new ValueTuple<long, string?, DateTime?>(record.Id, record.Str, record.Dt);
        if (_conn.State != System.Data.ConnectionState.Open)
        {
            await _conn.OpenAsync();
        }
        using var ee = new TupleEnumerator<(long id, string? str, DateTime? dt)>(await _adoCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult), TupleFactory);
        Enumerate(ee);
        static void Enumerate(TupleEnumerator<(long id, string? str, DateTime? dt)> ee)
        {
            foreach (ref var t in ee)
            {
            }
        }
    }
    void TupleFactory(ref (long id, string? str, DateTime? dt) tuple, DbDataReader reader)
    {
        tuple.id = reader.GetInt64(0);
        tuple.str = reader.GetString(1);
        tuple.dt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
    }
    class TupleEnumerator<T> : IDisposable
        where T : struct
    {
        public delegate void InitTupleDelegate(ref T obj, DbDataReader reader);
        private readonly InitTupleDelegate _map;
        private T _current;
        private readonly DbDataReader _reader;
        public TupleEnumerator(DbDataReader reader, InitTupleDelegate map)
        {
            _reader = reader;
            _map = map;
        }
        public ref T Current
        {
            get
            {
                _map(ref _current, _reader!);
                return ref _current;
            }
        }
        public bool MoveNext()
        {
            return _reader.Read();
        }
        public TupleEnumerator<T> GetEnumerator()
        {
            return this;
        }
        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
    // [Benchmark()]
    // public async Task AdoClassToList()
    // {
    //     if (_conn.State != System.Data.ConnectionState.Open)
    //     {
    //         await _conn.OpenAsync().ConfigureAwait(false);
    //     }
    //     using var reader = await _adoCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult).ConfigureAwait(false);
    //     var l = new List<LargeEntity>();
    //     while (await reader.ReadAsync().ConfigureAwait(false))
    //     {
    //         var Id = reader.GetInt64(0);
    //         var Str = reader.GetString(1);
    //         DateTime? Dt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
    //         var o = new LargeEntity() { Id = Id, Str = Str, Dt = Dt };
    //         l.Add(o);
    //     }
    //     foreach (var row in l)
    //     {

    //     }
    // }
    //[Benchmark(Baseline = true)]
    //[BenchmarkCategory("Buffered")]
    public async Task AdoClassToListWithInit()
    {
        if (_conn.State != System.Data.ConnectionState.Open)
        {
            await _conn.OpenAsync().ConfigureAwait(false);
        }
        using var reader = await _adoCmd.ExecuteReaderAsync(CommandBehavior.SingleResult).ConfigureAwait(false);
        var l = new List<LargeEntity>(10_000);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            var Id = reader.GetInt64(0);
            var Str = reader.IsDBNull(1) ? null : reader.GetString(1);
            DateTime? Dt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
            var o = new LargeEntity() { Id = Id, Str = Str, Dt = Dt };
            l.Add(o);
        }
        foreach (var row in l)
        {

        }
    }
    // [Benchmark()]
    // public async Task AdoTupleToList()
    // {
    //     if (_conn.State != System.Data.ConnectionState.Open)
    //     {
    //         await _conn.OpenAsync().ConfigureAwait(false);
    //     }
    //     using var reader = await _adoCmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess | CommandBehavior.SingleResult).ConfigureAwait(false);
    //     var l = new List<(long id, string? str, DateTime? dt)>();
    //     while (await reader.ReadAsync().ConfigureAwait(false))
    //     {
    //         var Id = reader.GetInt64(0);
    //         var Str = reader.GetString(1);
    //         DateTime? Dt = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
    //         l.Add((Id, Str, Dt));
    //     }
    //     foreach (var row in l)
    //     {
    //     }
    // }
    // public async Task NextormInfiniteLoop()
    // {
    //     while (true)
    //     {
    //         var s = Guid.NewGuid().ToString();

    //         foreach (var row in await _ctx.LargeEntity.Where(it => it.Str == s).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
    //         {
    //         }
    //     }
    // }
    // public async Task EFCoreInfiniteLoop()
    // {
    //     while (true)
    //     {
    //         var s = Guid.NewGuid().ToString();
    //         foreach (var row in await _efCtx.LargeEntities.Where(it => it.Str == s).Select(entity => new { entity.Id, entity.Str, entity.Dt }).ToListAsync())
    //         {
    //         }
    //     }
    // }
}
