using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using nextorm.core;
using nextorm.sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Dapper;
using LinqToDB;

namespace nextorm.benchmark;

/// <summary>
/// SQL-feature benchmarks: every feature introduced by the SQL-capabilities workstream is measured
/// next to its closest EF Core / linq2db equivalent. Where neither provider can express a construct
/// (window functions, CTEs, TVFs) a raw-SQL Dapper variant is used as the baseline and flagged with
/// the <c>DapperRaw</c> benchmark category.
/// </summary>
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByJob, BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkFeatures
{
    private const int Iterations = 25;

    private readonly nextorm.core.IDataContext _db;
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
    private readonly IPreparedQueryCommand<long> _nextormTvf;

    public SqliteBenchmarkFeatures()
    {
        var builder = new DbContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDbContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        _nextormLeftJoin = _ctx.SimpleEntity
            .LeftJoin(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Select(p => new LeftJoinRow { Id = p.t1.Id, RightString = p.t2.RequiredString })
            .Prepare();

        _nextormJoin4 = _ctx.SimpleEntity
            .Join(_ctx.ComplexEntity, (s, c) => (long)s.Id == c.Id)
            .Join(_ctx.SimpleEntity, (p, s) => p.t2.Id == (long)s.Id)
            .Join(_ctx.ComplexEntity, (p, c) => (long)p.t3.Id == c.Id)
            .Select(p => new FourJoinRow { A = p.t1.Id, B = p.t2.RequiredString, C = p.t3.Id, D = p.t4.RequiredString })
            .Prepare();

        _nextormDistinct = _ctx.ComplexEntity.Select(c => c.Int).Distinct().Prepare();
        _nextormCase = _ctx.ComplexEntity.Select(c => c.Id > 1 ? 10 : 20).Prepare();
        _nextormToUpper = _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).Prepare();
        _nextormContains = _ctx.ComplexEntity.Where(c => c.String!.Contains("df")).Select(c => c.Id).Prepare();

        var inValues = new long[] { 1, 3, 10 };
        _nextormIn = _ctx.ComplexEntity.Where(c => NORM.SQL.@in(c.Id, inValues)).Select(c => c.Id).Prepare();
        _nextormListContains = _ctx.ComplexEntity.Where(c => inValues.Contains(c.Id)).Select(c => c.Id).Prepare();

        _nextormRowNumber = _ctx.ComplexEntity.Select(c => new RowNumberRow
        {
            Id = c.Id,
            Rn = NORM.SQL.row_number().Over(partitionBy: () => c.Int, orderBy: () => c.Id)
        }).Prepare();

        _nextormSumOver = _ctx.ComplexEntity.Select(c => new SumOverRow
        {
            Id = c.Id,
            Total = NORM.SQL.sum_over(c.Id).Over(partitionBy: () => c.Int)
        }).Prepare();

        var recent = _ctx.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id, c.RequiredString });
        _nextormCte = _db.With("recent", recent)
            .From("recent")
            .Join(_ctx.SimpleEntity, (r, s) => r["id"].AsInt == s.Id)
            .Select(p => new CteJoinRow { Id = p.t1["id"].AsInt, SimpleId = p.t2.Id })
            .Prepare();

        var anchor = _ctx.SimpleEntity.Where(s => s.Id == 1).Select(s => new CteNumberRow { n = s.Id });
        var step = _db.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.UnionAll(step);
        _nextormRecursiveCte = _db.WithRecursive("nums", body).From("nums").Select(t => t["n"].AsInt).Prepare();

        _nextormIntersect = _ctx.SimpleEntity.Select(s => s.Id).Intersect(_ctx.ComplexEntity.Select(c => (int)c.Id)).Prepare();
        _nextormExcept = _ctx.SimpleEntity.Select(s => s.Id).Except(_ctx.ComplexEntity.Select(c => (int)c.Id)).Prepare();

        _nextormUdf = _ctx.ComplexEntity.Where(c => c.Id == 2).Select(c => Udf.ToUpper(c.String!)).Prepare();
        _nextormTvf = _db.FromTableFunction(() => Tvf.JsonEach("[1,2,3]")).Select(r => r.Value).Prepare();

        var efBuilder = new DbContextOptionsBuilder<EFDataContext>();
        efBuilder.UseSqlite(@$"Filename={BenchDb.FilePath}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new EFDataContext(efBuilder.Options);

        _conn = new SqliteConnection(((SqliteDbContext)_ctx.DbContext).ConnectionString);
        _conn.Open();

        _linq2Db = new Linq2DbDataRepository();
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

    #region LEFT JOIN

    [Benchmark]
    [BenchmarkCategory("LeftJoin")]
    public async Task Nextorm_Prepared_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormLeftJoin.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("LeftJoin")]
    public async Task EFCore_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await (from s in _efCtx.SimpleEntities
                              join c in _efCtx.ComplexEntities on (long)s.Id equals c.Id into g
                              from c in g.DefaultIfEmpty()
                              select new LeftJoinRow { Id = s.Id, RightString = c.RequiredString }).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("LeftJoin")]
    public async Task Linq2Db_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.LeftJoinAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("LeftJoin", "DapperRaw")]
    public async Task Dapper_LeftJoin()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<LeftJoinRow>(
                "select s.id, c.requiredString as RightString from simple_entity s left join complex_entity c on s.id = c.id")) { }
    }

    #endregion

    #region Four-table join

    [Benchmark]
    [BenchmarkCategory("Join4")]
    public async Task Nextorm_Prepared_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormJoin4.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Join4")]
    public async Task EFCore_Join4()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await (from s1 in _efCtx.SimpleEntities
                              join c1 in _efCtx.ComplexEntities on (long)s1.Id equals c1.Id
                              join s2 in _efCtx.SimpleEntities on c1.Id equals (long)s2.Id
                              join c2 in _efCtx.ComplexEntities on (long)s2.Id equals c2.Id
                              select new FourJoinRow { A = s1.Id, B = c1.RequiredString, C = s2.Id, D = c2.RequiredString }).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Join4")]
    public async Task Linq2Db_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.Join4Async()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Join4", "DapperRaw")]
    public async Task Dapper_Join4()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<FourJoinRow>(
                @"select s1.id as A, c1.requiredString as B, s2.id as C, c2.requiredString as D
                  from simple_entity s1
                  join complex_entity c1 on s1.id = c1.id
                  join simple_entity s2 on c1.id = s2.id
                  join complex_entity c2 on s2.id = c2.id")) { }
    }

    #endregion

    #region SELECT DISTINCT

    [Benchmark]
    [BenchmarkCategory("Distinct")]
    public async Task Nextorm_Prepared_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormDistinct.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Distinct")]
    public async Task EFCore_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.ComplexEntities.Select(c => c.Int).Distinct().ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Distinct")]
    public async Task Linq2Db_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.DistinctAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Distinct", "DapperRaw")]
    public async Task Dapper_Distinct()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int?>("select distinct nullableInt from complex_entity")) { }
    }

    #endregion

    #region CASE WHEN / ternary

    [Benchmark]
    [BenchmarkCategory("CaseWhen")]
    public async Task Nextorm_Prepared_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormCase.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("CaseWhen")]
    public async Task EFCore_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.ComplexEntities.Select(c => c.Id > 1 ? 10 : 20).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("CaseWhen")]
    public async Task Linq2Db_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.CaseAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("CaseWhen", "DapperRaw")]
    public async Task Dapper_CaseWhen()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select case when id > 1 then 10 else 20 end from complex_entity")) { }
    }

    #endregion

    #region String functions

    [Benchmark]
    [BenchmarkCategory("StringFunctions")]
    public async Task Nextorm_Prepared_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormToUpper.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions")]
    public async Task EFCore_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.ComplexEntities.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions")]
    public async Task Linq2Db_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ToUpperAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions", "DapperRaw")]
    public async Task Dapper_ToUpper()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<string>("select upper(someString) from complex_entity where id = 2")) { }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions")]
    public async Task Nextorm_Prepared_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormContains.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions")]
    public async Task EFCore_Contains()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.ComplexEntities.Where(c => c.String!.Contains("df")).Select(c => c.Id).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions")]
    public async Task Linq2Db_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ContainsAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("StringFunctions", "DapperRaw")]
    public async Task Dapper_Contains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select id from complex_entity where someString like @p", new { p = "%df%" })) { }
    }

    #endregion

    #region IN list

    [Benchmark]
    [BenchmarkCategory("InList")]
    public async Task Nextorm_Prepared_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormIn.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("InList")]
    public async Task Nextorm_Prepared_ListContains()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormListContains.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("InList")]
    public async Task EFCore_InContains()
    {
        var values = new long[] { 1, 3, 10 };
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.ComplexEntities.Where(c => values.Contains(c.Id)).Select(c => c.Id).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("InList")]
    public async Task Linq2Db_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.InAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("InList", "DapperRaw")]
    public async Task Dapper_In()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select id from complex_entity where id in (1,3,10)")) { }
    }

    #endregion

    #region Window functions

    [Benchmark]
    [BenchmarkCategory("Window")]
    public async Task Nextorm_Prepared_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormRowNumber.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Window")]
    public async Task Linq2Db_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.RowNumberAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Window", "DapperRaw")]
    public async Task Dapper_RowNumber()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<RowNumberRow>(
                "select id, row_number() over (partition by nullableInt order by id) as rn from complex_entity")) { }
    }

    [Benchmark]
    [BenchmarkCategory("Window")]
    public async Task Nextorm_Prepared_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormSumOver.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Window")]
    public async Task Linq2Db_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.SumOverAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Window", "DapperRaw")]
    public async Task Dapper_SumOver()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<SumOverRow>(
                "select id, sum(id) over (partition by nullableInt) as total from complex_entity")) { }
    }

    #endregion

    #region CTE

    [Benchmark]
    [BenchmarkCategory("Cte")]
    public async Task Nextorm_Prepared_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormCte.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Cte")]
    public async Task Linq2Db_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.CteAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Cte", "DapperRaw")]
    public async Task Dapper_Cte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<CteJoinRow>(
                @"with recent as (select id, requiredString from complex_entity where id > 1)
                  select recent.id as Id, simple_entity.id as SimpleId from recent join simple_entity on recent.id = simple_entity.id")) { }
    }

    [Benchmark]
    [BenchmarkCategory("RecursiveCte")]
    public async Task Nextorm_Prepared_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormRecursiveCte.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("RecursiveCte")]
    public async Task Linq2Db_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.RecursiveCteAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("RecursiveCte", "DapperRaw")]
    public async Task Dapper_RecursiveCte()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>(
                @"with recursive nums(n) as (
                      select id from simple_entity where id = 1
                      union all
                      select n + 1 from nums where n < 5)
                  select n from nums")) { }
    }

    #endregion

    #region INTERSECT / EXCEPT

    [Benchmark]
    [BenchmarkCategory("Intersect")]
    public async Task Nextorm_Prepared_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormIntersect.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Intersect")]
    public async Task EFCore_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.SimpleEntities.Select(s => s.Id)
                .Intersect(_efCtx.ComplexEntities.Select(c => (int)c.Id)).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Intersect")]
    public async Task Linq2Db_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.IntersectAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Intersect", "DapperRaw")]
    public async Task Dapper_Intersect()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select id from simple_entity intersect select id from complex_entity")) { }
    }

    [Benchmark]
    [BenchmarkCategory("Except")]
    public async Task Nextorm_Prepared_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormExcept.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Except")]
    public async Task EFCore_Except()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.SimpleEntities.Select(s => s.Id)
                .Except(_efCtx.ComplexEntities.Select(c => (int)c.Id)).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Except")]
    public async Task Linq2Db_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ExceptAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Except", "DapperRaw")]
    public async Task Dapper_Except()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<int>("select id from simple_entity except select id from complex_entity")) { }
    }

    #endregion

    #region User-defined scalar function

    [Benchmark]
    [BenchmarkCategory("Udf")]
    public async Task Nextorm_Prepared_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormUdf.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Udf")]
    public async Task EFCore_Udf()
    {
        for (var i = 0; i < Iterations; i++)
        {
            var rows = await _efCtx.ComplexEntities.Where(c => c.Id == 2).Select(c => c.String!.ToUpper()).ToListAsync();
        }
    }

    [Benchmark]
    [BenchmarkCategory("Udf")]
    public async Task Linq2Db_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _linq2Db.ToUpperAsync()) { }
    }

    [Benchmark]
    [BenchmarkCategory("Udf", "DapperRaw")]
    public async Task Dapper_Udf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<string>("select upper(someString) from complex_entity where id = 2")) { }
    }

    #endregion

    #region Table-valued function

    [Benchmark]
    [BenchmarkCategory("Tvf")]
    public async Task Nextorm_Prepared_Tvf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _nextormTvf.ToListAsync(_db)) { }
    }

    [Benchmark]
    [BenchmarkCategory("Tvf", "DapperRaw")]
    public async Task Dapper_Tvf()
    {
        for (var i = 0; i < Iterations; i++)
            foreach (var row in await _conn.QueryAsync<long>("select value from json_each('[1,2,3]')")) { }
    }

    #endregion
}
