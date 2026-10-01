using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;
using NextORM.EntityFrameworkCore;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Compares the EF Core bridge's imported-filter path (an EF <c>HasQueryFilter</c> capturing the live
/// <c>DbContext</c>, evaluated through <c>QueryFilterContextAccessor</c>) with an equivalent native
/// nextorm context filter (<c>c.Properties[key]</c>) on the same SQLite table. Both arms share one
/// connection and one table, use the same <c>Expression&lt;Func&lt;T, IDataContext, bool&gt;&gt;</c>
/// registration seam and the same projection.
/// </summary>
/// <remarks>
/// Cold vs warm preparation, per-operation allocations, accessor-cache growth and the plan-identity /
/// parameter-rebinding contract are checked once in <see cref="Setup"/> and printed to the benchmark
/// log; the measured methods quantify the steady-state warm overhead only. The benchmark never runs
/// with the acceptance category, so it cannot affect the acceptance perf gate.
/// </remarks>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("bridge-filter")]
public class BridgeFilterBenchmark
{
    private const int Iterations = 100;
    private const int PrepareIterations = 200;
    private const string TenantKey = "bench_bridge_tenant";

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"nextorm.bridge-bench.{Guid.NewGuid():N}.db");
    private SqliteConnection _connection = null!;
    private BridgeFilterDbContext _owner = null!;
    private IDataContext _bridge = null!;
    private IDataContext _native = null!;
    private EntityBuilder<BridgeFilterBenchEntity> _bridgeFiltered = null!;
    private EntityBuilder<NativeFilterBenchEntity> _nativeFiltered = null!;

    [GlobalSetup]
    public void Setup()
    {
        _connection = new SqliteConnection($"Data Source={_path}");
        _connection.Open();

        using (var seed = _connection.CreateCommand())
        {
            seed.CommandText =
                "CREATE TABLE bridge_filter_bench (id INTEGER NOT NULL PRIMARY KEY, tenant_id INTEGER NOT NULL, value INTEGER NOT NULL);" +
                "INSERT INTO bridge_filter_bench (id, tenant_id, value) VALUES (1, 1, 10), (2, 2, 20), (3, 1, 30);";
            seed.ExecuteNonQuery();
        }

        _owner = new BridgeFilterDbContext(_connection, tenantId: 1);
        _bridge = _owner.CreateNextOrmContext();

        var nativeBuilder = new DataContextBuilder();
        nativeBuilder.UseSqlite(_connection);
        _native = nativeBuilder.CreateDataContext();
        _native.Properties[TenantKey] = 1;
        _nativeFiltered = _native.From<NativeFilterBenchEntity>(
            b => b.HasQueryFilter("tenant", (e, c) => e.TenantId == (int)c.Properties[TenantKey]!));

        _bridgeFiltered = _bridge.From<BridgeFilterBenchEntity>();

        MeasureColdAndWarmPreparation();
        VerifyPlanIdentityAndRebinding();
    }

    // Warm, cached imported filter: plan lookup + ExtractParams + live-owner accessor invocation.
    [Benchmark(Baseline = true)]
    public int Bridge_Cached_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var id in _bridgeFiltered.Select(e => e.Id).ToList())
                sum += id;
        }

        return sum;
    }

    // Native context filter over the same SQL/table/projection: plan lookup + ExtractParams.
    [Benchmark]
    public int Native_Cached_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var id in _nativeFiltered.Select(e => e.Id).ToList())
                sum += id;
        }

        return sum;
    }

    // No DB: warm preparation of the imported filter (plan-cache hit + accessor-cache hit).
    [Benchmark]
    public IPreparedQueryCommand<int> Bridge_Warm_Prepare()
    {
        IPreparedQueryCommand<int>? prepared = null;
        for (var i = 0; i < PrepareIterations; i++)
            prepared = PrepareOne(_bridge, _bridgeFiltered);

        return prepared!;
    }

    // No DB: warm preparation of the native context filter.
    [Benchmark]
    public IPreparedQueryCommand<int> Native_Warm_Prepare()
    {
        IPreparedQueryCommand<int>? prepared = null;
        for (var i = 0; i < PrepareIterations; i++)
            prepared = PrepareOne(_native, _nativeFiltered);

        return prepared!;
    }

    // Imported filter with the owner value changing on every execution: same cached plan, new bound
    // tenant parameter read from the live DbContext.
    [Benchmark]
    public int Bridge_Rebind_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            _owner.TenantId = (i & 1) + 1;
            foreach (var id in _bridgeFiltered.Select(e => e.Id).ToList())
                sum += id;
        }

        return sum;
    }

    // Native equivalent: the context property changes on every execution.
    [Benchmark]
    public int Native_Rebind_ToList()
    {
        var sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            _native.Properties[TenantKey] = (i & 1) + 1;
            foreach (var id in _nativeFiltered.Select(e => e.Id).ToList())
                sum += id;
        }

        return sum;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _bridge?.Dispose();
        _native?.Dispose();
        _owner?.Dispose();
        _connection?.Dispose();

        if (File.Exists(_path))
            File.Delete(_path);
    }

    private static IPreparedQueryCommand<int> PrepareOne(IDataContext context, EntityBuilder<BridgeFilterBenchEntity> filtered)
        => context.GetPreparedQueryCommand(filtered.Select(e => e.Id), false, true, CancellationToken.None);

    private static IPreparedQueryCommand<int> PrepareOne(IDataContext context, EntityBuilder<NativeFilterBenchEntity> filtered)
        => context.GetPreparedQueryCommand(filtered.Select(e => e.Id), false, true, CancellationToken.None);

    private void MeasureColdAndWarmPreparation()
    {
        // Cold: the first imported-filter plan in this process compiles and caches the owner accessor.
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        _ = PrepareOne(_bridge, _bridgeFiltered);
        stopwatch.Stop();
        var coldMs = stopwatch.Elapsed.TotalMilliseconds;
        var coldAlloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;

        var accessorsBeforeWarm = AccessorCount();

        // Warm: every later preparation hits both the plan cache and the accessor cache.
        allocBefore = GC.GetAllocatedBytesForCurrentThread();
        stopwatch.Restart();
        for (var i = 0; i < PrepareIterations; i++)
            _ = PrepareOne(_bridge, _bridgeFiltered);
        stopwatch.Stop();
        var warmUs = stopwatch.Elapsed.TotalMicroseconds / PrepareIterations;
        var warmAlloc = (GC.GetAllocatedBytesForCurrentThread() - allocBefore) / PrepareIterations;

        var accessorsAfterWarm = AccessorCount();

        Console.WriteLine($"[bridge-filter] cold prepare: {coldMs:F3} ms, {coldAlloc} B");
        Console.WriteLine($"[bridge-filter] warm prepare: {warmUs:F3} us/op, {warmAlloc} B/op over {PrepareIterations} ops");
        Console.WriteLine($"[bridge-filter] accessor cache entries before warm={accessorsBeforeWarm}, after warm={accessorsAfterWarm}, added during warm={accessorsAfterWarm - accessorsBeforeWarm}");
    }

    private void VerifyPlanIdentityAndRebinding()
    {
        _owner.TenantId = 1;
        var bridgeSqlOne = SqlOf(_bridge, _bridgeFiltered);
        var bridgeRowsOne = _bridgeFiltered.OrderBy(e => e.Id).Select(e => e.Id).ToList();

        _owner.TenantId = 2;
        var bridgeSqlTwo = SqlOf(_bridge, _bridgeFiltered);
        var bridgeRowsTwo = _bridgeFiltered.OrderBy(e => e.Id).Select(e => e.Id).ToList();

        _native.Properties[TenantKey] = 1;
        var nativeSqlOne = SqlOf(_native, _nativeFiltered);
        var nativeRowsOne = _nativeFiltered.OrderBy(e => e.Id).Select(e => e.Id).ToList();

        _native.Properties[TenantKey] = 2;
        var nativeSqlTwo = SqlOf(_native, _nativeFiltered);
        var nativeRowsTwo = _nativeFiltered.OrderBy(e => e.Id).Select(e => e.Id).ToList();

        var bridgeSqlIdentical = string.Equals(bridgeSqlOne, bridgeSqlTwo, StringComparison.Ordinal);
        var nativeSqlIdentical = string.Equals(nativeSqlOne, nativeSqlTwo, StringComparison.Ordinal);
        var bridgeRowsRebound = bridgeRowsOne.SequenceEqual([1, 3]) && bridgeRowsTwo.SequenceEqual([2]);
        var nativeRowsRebound = nativeRowsOne.SequenceEqual([1, 3]) && nativeRowsTwo.SequenceEqual([2]);

        Console.WriteLine($"[bridge-filter] bridge sql identical across owner values: {bridgeSqlIdentical}");
        Console.WriteLine($"[bridge-filter] native sql identical across property values: {nativeSqlIdentical}");
        Console.WriteLine($"[bridge-filter] bridge rows rebound (1,3)->(2): {bridgeRowsRebound}");
        Console.WriteLine($"[bridge-filter] native rows rebound (1,3)->(2): {nativeRowsRebound}");

        if (!bridgeSqlIdentical || !nativeSqlIdentical || !bridgeRowsRebound || !nativeRowsRebound)
            throw new InvalidOperationException("Plan identity / parameter-rebinding contract violated.");

        _owner.TenantId = 1;
        _native.Properties[TenantKey] = 1;
    }

    private static string SqlOf(IDataContext context, EntityBuilder<BridgeFilterBenchEntity> filtered)
    {
        var prepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            filtered.Select(e => e.Id), false, false, CancellationToken.None);

        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    private static string SqlOf(IDataContext context, EntityBuilder<NativeFilterBenchEntity> filtered)
    {
        var prepared = (DbPreparedQueryCommand<int>)context.GetPreparedQueryCommand(
            filtered.Select(e => e.Id), false, false, CancellationToken.None);

        return prepared.DbCommand.CommandText.Replace("\r\n", "\n");
    }

    private static int AccessorCount()
    {
        var property = typeof(DataContextCache).GetProperty(
            "QueryFilterContextAccessors",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        return property?.GetValue(null) is System.Collections.IEnumerable accessors
            ? accessors.Cast<object>().Count()
            : 0;
    }

    [SqlTable("bridge_filter_bench")]
    public sealed class BridgeFilterBenchEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("value")]
        public int Value { get; set; }
    }

    [SqlTable("bridge_filter_bench")]
    public sealed class NativeFilterBenchEntity
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("value")]
        public int Value { get; set; }
    }

    private sealed class BridgeFilterDbContext(DbConnection connection, int tenantId) : DbContext
    {
        public int TenantId { get; set; } = tenantId;

        public DbSet<BridgeFilterBenchEntity> Rows => Set<BridgeFilterBenchEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseSqlite(connection);

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BridgeFilterBenchEntity>(entity =>
            {
                entity.ToTable("bridge_filter_bench");
                entity.HasKey(row => row.Id);
                entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
                entity.Property(row => row.TenantId).HasColumnName("tenant_id");
                entity.Property(row => row.Value).HasColumnName("value");
                entity.HasQueryFilter("tenant", row => row.TenantId == TenantId);
            });
        }
    }
}
