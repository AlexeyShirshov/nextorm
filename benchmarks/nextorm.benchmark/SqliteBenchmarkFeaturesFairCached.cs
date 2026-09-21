using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using LinqToDB;

namespace NextORM.Benchmark;

/// <summary>
/// Fair Category B comparison: nextorm through the implicit plan cache (a fresh fluent command with
/// constants on every call, so the cached plan is the only thing that can be reused) against EF Core
/// and linq2db *regular* (non-compiled) queries and raw Dapper.
/// <para>
/// The IN-list feature is split four ways on purpose. nextorm disables the plan cache for a captured
/// collection (<c>BaseExpressionVisitor.TranslateInValues</c> sets <c>Cache = false</c> when the
/// values expression is not an inline <c>NewArrayExpression</c>), so the captured variants should
/// pay a full SQL build per call while the inline variants hit the cache.
/// </para>
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkFeaturesFairCached
{
    private const int Iterations = 10;

    private static readonly long[] CapturedInValues = { 1, 3, 10 };

    private static int _verified;

    private readonly NextORM.Core.IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;

    private long _sink;

    public SqliteBenchmarkFeaturesFairCached()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDataContext)_ctx.DataContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
    }

    private static class Udf
    {
        [SqlFunction("upper")]
        public static string ToUpper(string value) => throw new NotSupportedException();
    }

    [GlobalSetup]
    public void Verify()
    {
        if (Interlocked.Exchange(ref _verified, 1) == 1) return;

        var counts = new List<(string Name, int Actual, int Expected)>();
        void Check(string name, int actual, int expected) => counts.Add((name, actual, expected));
        void CheckCmd<T>(string name, QueryCommand<T> cmd, int expected) => Check(name, cmd.ToList().Count, expected);

        CheckCmd("LeftJoin", _ctx.SimpleEntity
            .LeftJoin(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Select(p => new LeftJoinRow { Id = p.Item1.Id, RightString = p.Item2.RequiredString }), 10);

        CheckCmd("Join4", _ctx.SimpleEntity
            .Join(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Join(_ctx.SimpleEntity, (p, s) => p.Item2.Id == (long)s.Id)
            .Join(_ctx.ComplexEntity, (p, c) => (long)p.Item3.Id == c.Id)
            .Select(p => new FourJoinRow { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString }), 3);

        CheckCmd("Distinct", _ctx.ComplexEntity.Select(c => c.Int).Distinct(), 2);
        CheckCmd("Case", _ctx.ComplexEntity.Select(c => c.Id > 1 ? 10 : 20), 3);
        CheckCmd("ToUpper", _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()), 1);
        CheckCmd("Contains", _ctx.ComplexEntity.Where(c => c.String!.Contains("df")).Select(c => c.Id), 1);
        CheckCmd("InCaptured", _ctx.ComplexEntity.Where(c => CapturedInValues.Contains(c.Id)).Select(c => c.Id), 2);
        CheckCmd("InInline", _ctx.ComplexEntity.Where(c => new long[] { 1, 3, 10 }.Contains(c.Id)).Select(c => c.Id), 2);

        CheckCmd("RowNumber", _ctx.ComplexEntity.Select(c => new RowNumberRow
        {
            Id = c.Id,
            Rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => c.Int, orderBy: () => c.Id)
        }), 3);

        CheckCmd("SumOver", _ctx.ComplexEntity.Select(c => new SumOverRow
        {
            Id = c.Id,
            Total = SqlFunctions.Sql.sum_over(c.Id).Over(partitionBy: () => c.Int)
        }), 3);

        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        CheckCmd("Cte", _db.With("recent", recent)
            .From("recent")
            .Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id }), 2);

        var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        CheckCmd("RecursiveCte", _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt), 5);

        CheckCmd("Intersect", _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id)), 3);
        CheckCmd("Except", _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id)), 7);
        CheckCmd("Udf", _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => Udf.ToUpper(c.String!)), 1);

        foreach (var (name, actual, expected) in counts)
            if (actual != expected)
                throw new InvalidOperationException($"Row count mismatch for {name}: expected {expected}, got {actual}.");

        Console.WriteLine("SqliteBenchmarkFeaturesFairCached verification passed: " + string.Join(", ", counts.Select(c => $"{c.Name}={c.Actual}")));
    }

    #region LEFT JOIN

    [Benchmark]
    [BenchmarkCategory("B_LeftJoin")]
    public async Task B_Nextorm_Cached_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.SimpleEntity
                .LeftJoin(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
                .Select(p => new LeftJoinRow { Id = p.Item1.Id, RightString = p.Item2.RequiredString })
                .ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_LeftJoin")]
    public async Task B_EFCore_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            await (from s in _efCtx.SimpleEntities
                   join c in _efCtx.ComplexEntities on (long)s.Id equals c.Id into g
                   from c in g.DefaultIfEmpty()
                   select new LeftJoinRow { Id = s.Id, RightString = c.RequiredString }).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_LeftJoin")]
    public async Task B_Linq2Db_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.LeftJoinAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_LeftJoin")]
    public async Task B_Dapper_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<LeftJoinRow>(
                "select s.id, c.requiredString as RightString from simple_entity s left join complex_entity c on s.id = c.id")) _sink++;
    }

    #endregion

    #region Four-table join

    [Benchmark]
    [BenchmarkCategory("B_Join4")]
    public async Task B_Nextorm_Cached_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.SimpleEntity
                .Join(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
                .Join(_ctx.SimpleEntity, (p, s) => p.Item2.Id == (long)s.Id)
                .Join(_ctx.ComplexEntity, (p, c) => (long)p.Item3.Id == c.Id)
                .Select(p => new FourJoinRow { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
                .ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Join4")]
    public async Task B_EFCore_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            await (from s1 in _efCtx.SimpleEntities
                   join c1 in _efCtx.ComplexEntities on (long)s1.Id equals c1.Id
                   join s2 in _efCtx.SimpleEntities on c1.Id equals (long)s2.Id
                   join c2 in _efCtx.ComplexEntities on (long)s2.Id equals c2.Id
                   select new FourJoinRow { A = s1.Id, B = c1.RequiredString, C = s2.Id, D = c2.RequiredString }).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_Join4")]
    public async Task B_Linq2Db_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.Join4Async()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Join4")]
    public async Task B_Dapper_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<FourJoinRow>(
                @"select s1.id as A, c1.requiredString as B, s2.id as C, c2.requiredString as D
                  from simple_entity s1
                  join complex_entity c1 on s1.id = c1.id
                  join simple_entity s2 on c1.id = s2.id
                  join complex_entity c2 on s2.id = c2.id")) _sink++;
    }

    #endregion

    #region SELECT DISTINCT

    [Benchmark]
    [BenchmarkCategory("B_Distinct")]
    public async Task B_Nextorm_Cached_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Select(c => c.Int).Distinct().ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Distinct")]
    public async Task B_EFCore_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.ComplexEntities.Select(c => c.Int).Distinct().ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_Distinct")]
    public async Task B_Linq2Db_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.DistinctAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Distinct")]
    public async Task B_Dapper_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int?>("select distinct nullableInt from complex_entity")) _sink++;
    }

    #endregion

    #region CASE WHEN

    [Benchmark]
    [BenchmarkCategory("B_CaseWhen")]
    public async Task B_Nextorm_Cached_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Select(c => c.Id > 1 ? 10 : 20).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_CaseWhen")]
    public async Task B_EFCore_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.ComplexEntities.Select(c => c.Id > 1 ? 10 : 20).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_CaseWhen")]
    public async Task B_Linq2Db_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.CaseAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_CaseWhen")]
    public async Task B_Dapper_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select case when id > 1 then 10 else 20 end from complex_entity")) _sink++;
    }

    #endregion

    #region String functions

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_Nextorm_Cached_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_EFCore_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.ComplexEntities.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_Linq2Db_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ToUpperAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_Dapper_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<string>("select upper(someString) from complex_entity where id = 2")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_Nextorm_Cached_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => c.String!.Contains("df")).Select(c => c.Id).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_EFCore_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.ComplexEntities.Where(c => c.String!.Contains("df")).Select(c => c.Id).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_Linq2Db_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ContainsAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_StringFunctions")]
    public async Task B_Dapper_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select id from complex_entity where someString like @p", new { p = "%df%" })) _sink++;
    }

    #endregion

    #region IN list (captured vs inline)

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_Nextorm_Cached_In_AtIn_Captured()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => SqlFunctions.Sql.@in(c.Id, CapturedInValues)).Select(c => c.Id).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_Nextorm_Cached_In_AtIn_Inline()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => SqlFunctions.Sql.@in(c.Id, new long[] { 1, 3, 10 })).Select(c => c.Id).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_Nextorm_Cached_In_ListContains_Captured()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => CapturedInValues.Contains(c.Id)).Select(c => c.Id).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_Nextorm_Cached_In_ListContains_Inline()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => new long[] { 1, 3, 10 }.Contains(c.Id)).Select(c => c.Id).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_EFCore_In()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var values = new long[] { 1, 3, 10 };
            await _efCtx.ComplexEntities.Where(c => values.Contains(c.Id)).Select(c => c.Id).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_Linq2Db_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.InAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_InList")]
    public async Task B_Dapper_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select id from complex_entity where id in (1,3,10)")) _sink++;
    }

    #endregion

    #region Window functions (nextorm / linq2db / dapper)

    [Benchmark]
    [BenchmarkCategory("B_Window")]
    public async Task B_Nextorm_Cached_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Select(c => new RowNumberRow
            {
                Id = c.Id,
                Rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => c.Int, orderBy: () => c.Id)
            }).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Window")]
    public async Task B_Linq2Db_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.RowNumberAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Window")]
    public async Task B_Dapper_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<RowNumberRow>(
                "select id, row_number() over (partition by nullableInt order by id) as rn from complex_entity")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Window")]
    public async Task B_Nextorm_Cached_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Select(c => new SumOverRow
            {
                Id = c.Id,
                Total = SqlFunctions.Sql.sum_over(c.Id).Over(partitionBy: () => c.Int)
            }).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Window")]
    public async Task B_Linq2Db_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.SumOverAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Window")]
    public async Task B_Dapper_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<SumOverRow>(
                "select id, sum(id) over (partition by nullableInt) as total from complex_entity")) _sink++;
    }

    #endregion

    #region CTE (nextorm / linq2db / dapper)

    [Benchmark]
    [BenchmarkCategory("B_Cte")]
    public async Task B_Nextorm_Cached_Cte()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
            foreach (var row in await _db.With("recent", recent)
                .From("recent")
                .Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
                .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id })
                .ToListAsync()) _sink++;
        }
    }

    [Benchmark]
    [BenchmarkCategory("B_Cte")]
    public async Task B_Linq2Db_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.CteAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Cte")]
    public async Task B_Dapper_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<CteJoinRow>(
                @"with recent as (select id, requiredString from complex_entity where id > 1)
                  select recent.id as Id, simple_entity.id as SimpleId from recent join simple_entity on recent.id = simple_entity.id")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_RecursiveCte")]
    public async Task B_Nextorm_Cached_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
            var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
            foreach (var row in await _db.WithRecursive("nums", anchor.UnionAll(step)).From("nums").Select(t => t["n"].AsInt).ToListAsync()) _sink++;
        }
    }

    [Benchmark]
    [BenchmarkCategory("B_RecursiveCte")]
    public async Task B_Linq2Db_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.RecursiveCteAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_RecursiveCte")]
    public async Task B_Dapper_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>(
                @"with recursive nums(n) as (
                      select id from simple_entity where id = 1
                      union all
                      select n + 1 from nums where n < 5)
                  select n from nums")) _sink++;
    }

    #endregion

    #region INTERSECT / EXCEPT

    [Benchmark]
    [BenchmarkCategory("B_Intersect")]
    public async Task B_Nextorm_Cached_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Intersect")]
    public async Task B_EFCore_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.SimpleEntities.Select(s => s.Id).Intersect(_efCtx.ComplexEntities.Select(c => (int)c.Id)).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_Intersect")]
    public async Task B_Linq2Db_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.IntersectAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Intersect")]
    public async Task B_Dapper_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select id from simple_entity intersect select id from complex_entity")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Except")]
    public async Task B_Nextorm_Cached_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id)).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Except")]
    public async Task B_EFCore_Except()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.SimpleEntities.Select(s => s.Id).Except(_efCtx.ComplexEntities.Select(c => (int)c.Id)).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_Except")]
    public async Task B_Linq2Db_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ExceptAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Except")]
    public async Task B_Dapper_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select id from simple_entity except select id from complex_entity")) _sink++;
    }

    #endregion

    #region User-defined scalar function

    [Benchmark]
    [BenchmarkCategory("B_Udf")]
    public async Task B_Nextorm_Cached_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => Udf.ToUpper(c.String!)).ToListAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Udf")]
    public async Task B_EFCore_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            await _efCtx.ComplexEntities.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).ToListAsync();
    }

    [Benchmark]
    [BenchmarkCategory("B_Udf")]
    public async Task B_Linq2Db_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ToUpperAsync()) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("B_Udf")]
    public async Task B_Dapper_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<string>("select upper(someString) from complex_entity where id = 2")) _sink++;
    }

    #endregion
}
