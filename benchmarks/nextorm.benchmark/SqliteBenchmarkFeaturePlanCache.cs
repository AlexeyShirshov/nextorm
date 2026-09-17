using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using nextorm.core;
using nextorm.sqlite;

namespace nextorm.benchmark;

/// <summary>
/// Warm plan-cache probe (no DB round-trip, <c>storeInCache: true</c>). A fresh fluent command is
/// built on every iteration and then handed to <c>GetPreparedQueryCommand</c>, so the measured time
/// is construction + preparation + hashing + cache lookup. A cache hit returns the existing compiled
/// command; a miss additionally builds the SQL. Comparing the IN-list arms with the known-cacheable
/// <see cref="Warm_PlanOnly_Distinct"/> arm shows whether the implicit plan cache actually hits for
/// inline arrays (which <c>BaseExpressionVisitor.TranslateInValues</c> intentionally leaves cacheable)
/// and how bad the captured-collection cache disable is.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkFeaturePlanCache
{
    private const int Iterations = 100;

    private static readonly long[] CapturedInValues = { 1, 3, 10 };

    private readonly nextorm.core.IDataContext _db;
    private readonly TestDataRepository _ctx;

    public SqliteBenchmarkFeaturePlanCache()
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Prime the plan caches so the measured loops see hits where hits are possible.
        _ = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Select(c => c.Int).Distinct(), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, CapturedInValues)).Select(c => c.Id), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => CapturedInValues.Contains(c.Id)).Select(c => c.Id), false, true, CancellationToken.None);
        _ = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => new long[] { 1, 3, 10 }.Contains(c.Id)).Select(c => c.Id), false, true, CancellationToken.None);
    }

    [GlobalSetup]
    public void Verify()
    {
        if (Interlocked.Exchange(ref _verified, 1) == 1) return;

        var inlineAtIn = _ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id);
        _db.GetPreparedQueryCommand(inlineAtIn, false, false, CancellationToken.None);
        var inlineContains = _ctx.ComplexEntity.Where(c => new long[] { 1, 3, 10 }.Contains(c.Id)).Select(c => c.Id);
        _db.GetPreparedQueryCommand(inlineContains, false, false, CancellationToken.None);
        var capturedAtIn = _ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, CapturedInValues)).Select(c => c.Id);
        _db.GetPreparedQueryCommand(capturedAtIn, false, false, CancellationToken.None);

        Console.WriteLine($"PlanCacheProbe: inline@in.Cache={inlineAtIn.Cache}, inlineListContains.Cache={inlineContains.Cache}, captured@in.Cache={capturedAtIn.Cache}");
    }

    private static int _verified;

    [Benchmark(Baseline = true)]
    public IPreparedQueryCommand<int?> Warm_PlanOnly_Distinct()
    {
        IPreparedQueryCommand<int?>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Select(c => c.Int).Distinct(), false, true, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Warm_PlanOnly_In_AtIn_Captured()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, CapturedInValues)).Select(c => c.Id), false, true, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Warm_PlanOnly_In_AtIn_Inline()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id), false, true, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Warm_PlanOnly_In_ListContains_Captured()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => CapturedInValues.Contains(c.Id)).Select(c => c.Id), false, true, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Warm_PlanOnly_In_ListContains_Inline()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => new long[] { 1, 3, 10 }.Contains(c.Id)).Select(c => c.Id), false, true, CancellationToken.None);
        return r!;
    }
}
