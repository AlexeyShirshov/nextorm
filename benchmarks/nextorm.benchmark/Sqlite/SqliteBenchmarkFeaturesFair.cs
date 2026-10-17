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
/// Fair Category A comparison for the SQL-capabilities workstream: every nextorm <c>Prepare()</c>d
/// command is measured against the closest *compiled* form of the same query
/// (<c>EF.CompileAsyncQuery</c>, <c>LinqToDB.CompiledQuery.Compile</c>) and against raw Dapper
/// (whose IL is cached by SQL text). This removes the "nextorm caches, they rebuild the LINQ" bias
/// of <see cref="SqliteBenchmarkFeatures"/>.
/// <para>
/// Features where a provider has no compiled equivalent (window functions / CTE for EF Core) are
/// paired only with the competitors that support them.
/// </para>
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkFeaturesFair
{
    private const int Iterations = 10;

    private static readonly long[] InValues = { 1, 3, 10 };

    private readonly NextORM.Core.IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly EFDataContext _efCtx;
    private readonly SqliteConnection _conn;
    private readonly Linq2DbDataRepository _linq2Db;

    private readonly IPreparedQueryCommand<LeftJoinRow> _nextormLeftJoin;
    private readonly IPreparedQueryCommand<FourJoinRow> _nextormJoin4;
    private readonly IPreparedQueryCommand<int?> _nextormDistinct;
    private readonly IPreparedQueryCommand<int> _nextormCase;
    private readonly IPreparedQueryCommand<string> _nextormToUpper;
    private readonly IPreparedQueryCommand<long> _nextormContains;
    private readonly IPreparedQueryCommand<long> _nextormIn;
    private readonly IPreparedQueryCommand<long> _nextormListContains;
    private readonly IPreparedQueryCommand<RowNumberRow> _nextormRowNumber;
    private readonly IPreparedQueryCommand<SumOverRow> _nextormSumOver;
    private readonly IPreparedQueryCommand<CteJoinRow> _nextormCte;
    private readonly IPreparedQueryCommand<int> _nextormRecursiveCte;
    private readonly IPreparedQueryCommand<int> _nextormIntersect;
    private readonly IPreparedQueryCommand<int> _nextormExcept;
    private readonly IPreparedQueryCommand<string> _nextormUdf;

    private long _sink;

    public SqliteBenchmarkFeaturesFair()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _nextormLeftJoin = _ctx.SimpleEntity
            .LeftJoin(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Select(p => new LeftJoinRow { Id = p.Item1.Id, RightString = p.Item2.RequiredString })
            .Prepare();

        _nextormJoin4 = _ctx.SimpleEntity
            .Join(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Join(_ctx.SimpleEntity, (p, s) => p.Item2.Id == (long)s.Id)
            .Join(_ctx.ComplexEntity, (p, c) => (long)p.Item3.Id == c.Id)
            .Select(p => new FourJoinRow { A = p.Item1.Id, B = p.Item2.RequiredString, C = p.Item3.Id, D = p.Item4.RequiredString })
            .Prepare();

        _nextormDistinct = _ctx.ComplexEntity.Select(c => c.Int).Distinct().Prepare();
        _nextormCase = _ctx.ComplexEntity.Select(c => c.Id > 1 ? 10 : 20).Prepare();
        _nextormToUpper = _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).Prepare();
        _nextormContains = _ctx.ComplexEntity.Where(c => c.String!.Contains("df")).Select(c => c.Id).Prepare();

        _nextormIn = _ctx.ComplexEntity.Where(c => SqlFunctions.Sql.@in(c.Id, InValues)).Select(c => c.Id).Prepare();
        _nextormListContains = _ctx.ComplexEntity.Where(c => InValues.Contains(c.Id)).Select(c => c.Id).Prepare();

        _nextormRowNumber = _ctx.ComplexEntity.Select(c => new RowNumberRow
        {
            Id = c.Id,
            Rn = SqlFunctions.Sql.row_number().Over(partitionBy: () => c.Int, orderBy: () => c.Id)
        }).Prepare();

        _nextormSumOver = _ctx.ComplexEntity.Select(c => new SumOverRow
        {
            Id = c.Id,
            Total = SqlFunctions.Sql.sum_over(c.Id).Over(partitionBy: () => c.Int)
        }).Prepare();

        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        _nextormCte = _db.With("recent", recent)
            .From("recent")
            .Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.Item1["id"].AsInt, SimpleId = p.Item2.Id })
            .Prepare();

        var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);
        _nextormRecursiveCte = _db.WithRecursive("nums", body).From("nums").Select(t => t["n"].AsInt).Prepare();

        _nextormIntersect = _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id)).Prepare();
        _nextormExcept = _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id)).Prepare();

        _nextormUdf = _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => Udf.ToUpper(c.String!)).Prepare();

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

    #region EF Core compiled queries

    private static readonly Func<EFDataContext, IAsyncEnumerable<LeftJoinRow>> _efLeftJoin = EF.CompileAsyncQuery(
        (EFDataContext ctx) => from s in ctx.SimpleEntities
                               join c in ctx.ComplexEntities on (long)s.Id equals c.Id into g
                               from c in g.DefaultIfEmpty()
                               select new LeftJoinRow { Id = s.Id, RightString = c.RequiredString });

    private static readonly Func<EFDataContext, IAsyncEnumerable<FourJoinRow>> _efJoin4 = EF.CompileAsyncQuery(
        (EFDataContext ctx) => from s1 in ctx.SimpleEntities
                               join c1 in ctx.ComplexEntities on (long)s1.Id equals c1.Id
                               join s2 in ctx.SimpleEntities on c1.Id equals (long)s2.Id
                               join c2 in ctx.ComplexEntities on (long)s2.Id equals c2.Id
                               select new FourJoinRow { A = s1.Id, B = c1.RequiredString, C = s2.Id, D = c2.RequiredString });

    private static readonly Func<EFDataContext, IAsyncEnumerable<int?>> _efDistinct = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.ComplexEntities.Select(c => c.Int).Distinct());

    private static readonly Func<EFDataContext, IAsyncEnumerable<int>> _efCase = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.ComplexEntities.Select(c => c.Id > 1 ? 10 : 20));

    private static readonly Func<EFDataContext, IAsyncEnumerable<string>> _efToUpper = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.ComplexEntities.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()));

    private static readonly Func<EFDataContext, IAsyncEnumerable<long>> _efContains = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.ComplexEntities.Where(c => c.String!.Contains("df")).Select(c => c.Id));

    private static readonly Func<EFDataContext, long[], IAsyncEnumerable<long>> _efIn = EF.CompileAsyncQuery(
        (EFDataContext ctx, long[] values) => ctx.ComplexEntities.Where(c => values.Contains(c.Id)).Select(c => c.Id));

    private static readonly Func<EFDataContext, IAsyncEnumerable<int>> _efIntersect = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.SimpleEntities.Select(s => s.Id).Intersect(ctx.ComplexEntities.Select(c => (int)c.Id)));

    private static readonly Func<EFDataContext, IAsyncEnumerable<int>> _efExcept = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.SimpleEntities.Select(s => s.Id).Except(ctx.ComplexEntities.Select(c => (int)c.Id)));

    private static readonly Func<EFDataContext, IAsyncEnumerable<string>> _efUdf = EF.CompileAsyncQuery(
        (EFDataContext ctx) => ctx.ComplexEntities.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()));

    #endregion

    #region linq2db compiled queries

    private static readonly Func<LinqToDB.IDataContext, List<LeftJoinRow>> _l2dbLeftJoin = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => (from s in db.GetTable<Linq2DbSimpleEntity>()
                              from c in db.GetTable<Linq2DbComplexEntity>().LeftJoin(c => c.Id == (long)s.Id)
                              select new LeftJoinRow { Id = s.Id, RightString = c.RequiredString }).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<FourJoinRow>> _l2dbJoin4 = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => (from s1 in db.GetTable<Linq2DbSimpleEntity>()
                              join c1 in db.GetTable<Linq2DbComplexEntity>() on (long)s1.Id equals c1.Id
                              join s2 in db.GetTable<Linq2DbSimpleEntity>() on c1.Id equals (long)s2.Id
                              join c2 in db.GetTable<Linq2DbComplexEntity>() on (long)s2.Id equals c2.Id
                              select new FourJoinRow { A = s1.Id, B = c1.RequiredString, C = s2.Id, D = c2.RequiredString }).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<int?>> _l2dbDistinct = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Select(it => it.Int).Distinct().ToList());

    private static readonly Func<LinqToDB.IDataContext, List<int>> _l2dbCase = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Select(it => it.Id > 1 ? 10 : 20).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<string>> _l2dbToUpper = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Where(it => it.Id == 2).Select(it => it.String!.ToUpper()).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<long>> _l2dbContains = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Where(it => it.String!.Contains("df")).Select(it => it.Id).ToList());

    private static readonly Func<LinqToDB.IDataContext, long[], List<long>> _l2dbIn = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db, long[] values) => db.GetTable<Linq2DbComplexEntity>().Where(it => values.Contains(it.Id)).Select(it => it.Id).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<RowNumberRow>> _l2dbRowNumber = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Select(it => new RowNumberRow
        {
            Id = it.Id,
            Rn = Sql.Window.RowNumber(f => f.PartitionBy(it.Int).OrderBy(it.Id))
        }).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<SumOverRow>> _l2dbSumOver = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Select(it => new SumOverRow
        {
            Id = it.Id,
            Total = Sql.Window.Sum(it.Id, f => f.PartitionBy(it.Int))
        }).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<CteJoinRow>> _l2dbCte = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => (from r in db.GetTable<Linq2DbComplexEntity>().Where(it => it.Id > 1)
                                  .Select(it => new { it.Id, it.RequiredString }).AsCte("recent")
                              join s in db.GetTable<Linq2DbSimpleEntity>() on r.Id equals (long)s.Id
                              select new CteJoinRow { Id = (int)r.Id, SimpleId = s.Id }).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<int>> _l2dbRecursiveCte = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetCte<int>(cte =>
            db.GetTable<Linq2DbSimpleEntity>().Where(s => s.Id == 1).Select(s => s.Id)
                .Concat(cte.Where(n => n < 5).Select(n => n + 1)), "nums").ToList());

    private static readonly Func<LinqToDB.IDataContext, List<int>> _l2dbIntersect = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbSimpleEntity>().Select(it => it.Id)
            .Intersect(db.GetTable<Linq2DbComplexEntity>().Select(it => (int)it.Id)).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<int>> _l2dbExcept = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbSimpleEntity>().Select(it => it.Id)
            .Except(db.GetTable<Linq2DbComplexEntity>().Select(it => (int)it.Id)).ToList());

    private static readonly Func<LinqToDB.IDataContext, List<string>> _l2dbUdf = LinqToDB.CompiledQuery.Compile(
        (LinqToDB.IDataContext db) => db.GetTable<Linq2DbComplexEntity>().Where(it => it.Id == 2).Select(it => it.String!.ToUpper()).ToList());

    #endregion

    [GlobalSetup]
    public void Verify()
    {
        if (Interlocked.Exchange(ref _verified, 1) == 1) return;

        var counts = new List<(string Name, int Actual, int Expected)>();
        void Check(string name, int actual, int expected) => counts.Add((name, actual, expected));

        Check("LeftJoin/nextorm", _nextormLeftJoin.ToList(_db).Count, 10);
        Check("Join4/nextorm", _nextormJoin4.ToList(_db).Count, 3);
        Check("Distinct/nextorm", _nextormDistinct.ToList(_db).Count, 2);
        Check("Case/nextorm", _nextormCase.ToList(_db).Count, 3);
        Check("ToUpper/nextorm", _nextormToUpper.ToList(_db).Count, 1);
        Check("Contains/nextorm", _nextormContains.ToList(_db).Count, 1);
        Check("In/nextorm", _nextormIn.ToList(_db).Count, 2);
        Check("ListContains/nextorm", _nextormListContains.ToList(_db).Count, 2);
        Check("RowNumber/nextorm", _nextormRowNumber.ToList(_db).Count, 3);
        Check("SumOver/nextorm", _nextormSumOver.ToList(_db).Count, 3);
        Check("Cte/nextorm", _nextormCte.ToList(_db).Count, 2);
        Check("RecursiveCte/nextorm", _nextormRecursiveCte.ToList(_db).Count, 5);
        Check("Intersect/nextorm", _nextormIntersect.ToList(_db).Count, 3);
        Check("Except/nextorm", _nextormExcept.ToList(_db).Count, 7);
        Check("Udf/nextorm", _nextormUdf.ToList(_db).Count, 1);

        foreach (var (name, actual, expected) in counts)
            if (actual != expected)
                throw new InvalidOperationException($"Row count mismatch for {name}: expected {expected}, got {actual}.");

        Console.WriteLine("SqliteBenchmarkFeaturesFair verification passed: " + string.Join(", ", counts.Select(c => $"{c.Name}={c.Actual}")));
    }

    private static int _verified;

    #region LEFT JOIN

    [Benchmark]
    [BenchmarkCategory("A_LeftJoin")]
    public async Task A_Nextorm_Prepared_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormLeftJoin.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_LeftJoin")]
    public async Task A_EFCore_Compiled_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efLeftJoin(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_LeftJoin")]
    public long A_Linq2Db_Compiled_LeftJoin()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbLeftJoin(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_LeftJoin")]
    public async Task A_Dapper_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<LeftJoinRow>(
                "select s.id, c.requiredString as RightString from simple_entity s left join complex_entity c on s.id = c.id")) _sink++;
    }

    #endregion

    #region Four-table join

    [Benchmark]
    [BenchmarkCategory("A_Join4")]
    public async Task A_Nextorm_Prepared_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormJoin4.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Join4")]
    public async Task A_EFCore_Compiled_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efJoin4(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Join4")]
    public long A_Linq2Db_Compiled_Join4()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbJoin4(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Join4")]
    public async Task A_Dapper_Join4()
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
    [BenchmarkCategory("A_Distinct")]
    public async Task A_Nextorm_Prepared_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormDistinct.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Distinct")]
    public async Task A_EFCore_Compiled_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efDistinct(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Distinct")]
    public long A_Linq2Db_Compiled_Distinct()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbDistinct(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Distinct")]
    public async Task A_Dapper_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int?>("select distinct nullableInt from complex_entity")) _sink++;
    }

    #endregion

    #region CASE WHEN

    [Benchmark]
    [BenchmarkCategory("A_CaseWhen")]
    public async Task A_Nextorm_Prepared_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormCase.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_CaseWhen")]
    public async Task A_EFCore_Compiled_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efCase(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_CaseWhen")]
    public long A_Linq2Db_Compiled_CaseWhen()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbCase(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_CaseWhen")]
    public async Task A_Dapper_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select case when id > 1 then 10 else 20 end from complex_entity")) _sink++;
    }

    #endregion

    #region String functions

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public async Task A_Nextorm_Prepared_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormToUpper.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public async Task A_EFCore_Compiled_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efToUpper(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public long A_Linq2Db_Compiled_ToUpper()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbToUpper(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public async Task A_Dapper_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<string>("select upper(someString) from complex_entity where id = 2")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public async Task A_Nextorm_Prepared_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormContains.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public async Task A_EFCore_Compiled_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efContains(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public long A_Linq2Db_Compiled_Contains()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbContains(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_StringFunctions")]
    public async Task A_Dapper_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select id from complex_entity where someString like @p", new { p = "%df%" })) _sink++;
    }

    #endregion

    #region IN list

    [Benchmark]
    [BenchmarkCategory("A_InList")]
    public async Task A_Nextorm_Prepared_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormIn.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_InList")]
    public async Task A_Nextorm_Prepared_ListContains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormListContains.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_InList")]
    public async Task A_EFCore_Compiled_In()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efIn(_efCtx, InValues)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_InList")]
    public long A_Linq2Db_Compiled_In()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbIn(_linq2Db.Db, InValues).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_InList")]
    public async Task A_Dapper_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select id from complex_entity where id in (1,3,10)")) _sink++;
    }

    #endregion

    #region Window functions (linq2db + Dapper; EF Core has no row_number/over translation)

    [Benchmark]
    [BenchmarkCategory("A_Window")]
    public async Task A_Nextorm_Prepared_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormRowNumber.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Window")]
    public long A_Linq2Db_Compiled_RowNumber()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbRowNumber(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Window")]
    public async Task A_Dapper_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<RowNumberRow>(
                "select id, row_number() over (partition by nullableInt order by id) as rn from complex_entity")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Window")]
    public async Task A_Nextorm_Prepared_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormSumOver.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Window")]
    public long A_Linq2Db_Compiled_SumOver()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbSumOver(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Window")]
    public async Task A_Dapper_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<SumOverRow>(
                "select id, sum(id) over (partition by nullableInt) as total from complex_entity")) _sink++;
    }

    #endregion

    #region CTE (linq2db + Dapper; EF Core has no CTE surface)

    [Benchmark]
    [BenchmarkCategory("A_Cte")]
    public async Task A_Nextorm_Prepared_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormCte.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Cte")]
    public long A_Linq2Db_Compiled_Cte()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbCte(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Cte")]
    public async Task A_Dapper_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<CteJoinRow>(
                @"with recent as (select id, requiredString from complex_entity where id > 1)
                  select recent.id as Id, simple_entity.id as SimpleId from recent join simple_entity on recent.id = simple_entity.id")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_RecursiveCte")]
    public async Task A_Nextorm_Prepared_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormRecursiveCte.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_RecursiveCte")]
    public long A_Linq2Db_Compiled_RecursiveCte()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbRecursiveCte(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_RecursiveCte")]
    public async Task A_Dapper_RecursiveCte()
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
    [BenchmarkCategory("A_Intersect")]
    public async Task A_Nextorm_Prepared_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormIntersect.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Intersect")]
    public async Task A_EFCore_Compiled_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efIntersect(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Intersect")]
    public long A_Linq2Db_Compiled_Intersect()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbIntersect(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Intersect")]
    public async Task A_Dapper_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select id from simple_entity intersect select id from complex_entity")) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Except")]
    public async Task A_Nextorm_Prepared_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormExcept.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Except")]
    public async Task A_EFCore_Compiled_Except()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efExcept(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Except")]
    public long A_Linq2Db_Compiled_Except()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbExcept(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Except")]
    public async Task A_Dapper_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select id from simple_entity except select id from complex_entity")) _sink++;
    }

    #endregion

    #region User-defined scalar function

    [Benchmark]
    [BenchmarkCategory("A_Udf")]
    public async Task A_Nextorm_Prepared_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormUdf.ToListAsync(_db)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Udf")]
    public async Task A_EFCore_Compiled_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            await foreach (var row in _efUdf(_efCtx)) _sink++;
    }

    [Benchmark]
    [BenchmarkCategory("A_Udf")]
    public long A_Linq2Db_Compiled_Udf()
    {
        long n = 0;
        for (var i = 0; i < Iterations; i++)
            n += _l2dbUdf(_linq2Db.Db).Count;
        _sink += n;
        return n;
    }

    [Benchmark]
    [BenchmarkCategory("A_Udf")]
    public async Task A_Dapper_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<string>("select upper(someString) from complex_entity where id = 2")) _sink++;
    }

    #endregion
}
