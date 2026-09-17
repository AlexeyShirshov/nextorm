using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using nextorm.core;
using nextorm.sqlite;

namespace nextorm.benchmark;

/// <summary>
/// Cold SQL-build cost for each SQL-capabilities feature. No DB round-trip and no plan cache
/// (<c>GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: false, ...)</c>), so every
/// iteration exercises the visitors + alias resolution + parameter materialisation exactly like
/// <see cref="SqliteBenchmarkCachedPlan.Build_Sql"/>. Use this to find translations whose build cost
/// is disproportionate to the runtime cost.
/// <para>
/// The captured-collection IN-list arms are deliberately separate from the inline-array arms: for a
/// captured collection nextorm disables the plan cache
/// (<c>BaseExpressionVisitor.TranslateInValues</c>), so this is the build cost that the warm Category
/// B path pays on every call.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkFeaturePlanBuild
{
    private const int Iterations = 100;

    private static readonly long[] CapturedInValues = { 1, 3, 10 };

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;

    public SqliteBenchmarkFeaturePlanBuild()
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();
    }

    private static class Udf
    {
        [SqlFunction("upper")]
        public static string ToUpper(string value) => throw new NotSupportedException();
    }

    private static class Tvf
    {
        [SqlTableFunction("json_each")]
        public static IQueryable<JsonEachRow> JsonEach(string json) => throw new NotSupportedException();
    }

    [GlobalSetup]
    public void Verify()
    {
        if (Interlocked.Exchange(ref _verified, 1) == 1) return;

        IPreparedQueryCommand<int> baseline = _db.GetPreparedQueryCommand(
            _ctx.SimpleEntity.Where(it => it.Id == 5).Select(it => it.Id), false, false, CancellationToken.None);
        if (baseline.ToList(_db).Count != 1)
            throw new InvalidOperationException("Plan-build baseline did not produce a row.");

        Console.WriteLine("SqliteBenchmarkFeaturePlanBuild verification passed.");
    }

    private static int _verified;

    private IPreparedQueryCommand<int> Build(QueryCommand<int> cmd)
        => _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    [Benchmark(Baseline = true)]
    public IPreparedQueryCommand<int> Build_Baseline_SimpleSelect()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.SimpleEntity.Where(it => it.Id == 5).Select(it => it.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<LeftJoinRow> Build_LeftJoin()
    {
        IPreparedQueryCommand<LeftJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.SimpleEntity
                .LeftJoin(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
                .Select(p => new LeftJoinRow { Id = p.t1.Id, RightString = p.t2.RequiredString }), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<FourJoinRow> Build_Join4()
    {
        IPreparedQueryCommand<FourJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.SimpleEntity
                .Join(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
                .Join(_ctx.SimpleEntity, (p, s) => p.t2.Id == (long)s.Id)
                .Join(_ctx.ComplexEntity, (p, c) => (long)p.t3.Id == c.Id)
                .Select(p => new FourJoinRow { A = p.t1.Id, B = p.t2.RequiredString, C = p.t3.Id, D = p.t4.RequiredString }), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int?> Build_Distinct()
    {
        IPreparedQueryCommand<int?>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Select(c => c.Int).Distinct(), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Build_Case()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Select(c => c.Id > 1 ? 10 : 20), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<string> Build_StringFn_ToUpper()
    {
        IPreparedQueryCommand<string>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_StringFn_Contains()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => c.String!.Contains("df")).Select(c => c.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_In_AtIn_Captured()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, CapturedInValues)).Select(c => c.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_In_AtIn_Inline()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_In_ListContains_Captured()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => CapturedInValues.Contains(c.Id)).Select(c => c.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_In_ListContains_Inline()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => new long[] { 1, 3, 10 }.Contains(c.Id)).Select(c => c.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_Unary_Not()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => !c.Boolean!.Value).Select(c => c.Id), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<RowNumberRow> Build_Window_RowNumber()
    {
        IPreparedQueryCommand<RowNumberRow>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Select(c => new RowNumberRow
            {
                Id = c.Id,
                Rn = NORM.SQL.row_number().Over(partitionBy: () => c.Int, orderBy: () => c.Id)
            }), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<SumOverRow> Build_Window_SumOver()
    {
        IPreparedQueryCommand<SumOverRow>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Select(c => new SumOverRow
            {
                Id = c.Id,
                Total = NORM.SQL.sum_over(c.Id).Over(partitionBy: () => c.Int)
            }), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<CteJoinRow> Build_Cte()
    {
        IPreparedQueryCommand<CteJoinRow>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
            var cmd = _db.With("recent", recent)
                .From("recent")
                .Join(_ctx.SimpleEntity, (t, s) => t["id"].AsInt == s.Id)
                .Select(p => new CteJoinRow { Id = p.t1["id"].AsInt, SimpleId = p.t2.Id });
            r = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Build_RecursiveCte()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
            var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
            var cmd = _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt);
            r = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        }
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Build_Intersect()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(
                _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id)), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<int> Build_Except()
    {
        IPreparedQueryCommand<int>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(
                _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id)), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<string> Build_Udf()
    {
        IPreparedQueryCommand<string>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => Udf.ToUpper(c.String!)), false, false, CancellationToken.None);
        return r!;
    }

    [Benchmark]
    public IPreparedQueryCommand<long> Build_Tvf()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(
                _db.FromTableFunction(() => Tvf.JsonEach("[1,2,3]")).Select(t => t.Value), false, false, CancellationToken.None);
        return r!;
    }
}
