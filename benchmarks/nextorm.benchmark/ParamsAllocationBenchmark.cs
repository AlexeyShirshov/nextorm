using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Sqlite;
using System.Data.Common;

namespace NextORM.Benchmark;

/// <summary>
/// M2 isolation benchmark: quantifies the per-call <c>object[]</c> allocation that
/// <c>params object[]</c> used to cause on the hot API.
///
/// The public sync API is now <c>M()</c> (an exact parameterless overload, required because a
/// <c>params ReadOnlySpan&lt;T&gt;</c> call site is rejected inside an expression tree: CS8640/CS9226)
/// plus <c>M(params ReadOnlySpan&lt;object?&gt;)</c>, which covers every arity with no heap array.
///
/// The <c>*__Array</c> arms pass an explicitly allocated <c>object[]</c> (which implicitly converts
/// to the span), so the <c>Allocated</c> delta against the span arm is exactly the array cost that
/// the span signature removes.
///
/// The <c>Generic_*</c> pair is a language-level proof that <c>params ReadOnlySpan&lt;T&gt;</c>
/// (C# 13+) compiles under <c>LangVersion=latest</c> and allocates nothing.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class ParamsAllocationBenchmark
{
    private const int Iterations = 100;

    private readonly IDataContext _db;
    private readonly DataContext _dbCtx;
    private readonly TestDataRepository _repo;
    private readonly IPreparedQueryCommand<bool> _cmd1;
    private readonly IPreparedQueryCommand<bool> _cmd2;
    private readonly DbPreparedQueryCommand<bool> _dbCmd;
    private readonly DbConnection _conn;
    private readonly Func<string, object?, DbParameter> _createParam;
    private readonly object[] _args1 = new object[1];
    private readonly object[] _args2 = new object[2];

    // Pre-boxed so the sync arms isolate the params-array cost, not int boxing.
    private static readonly object s_boxedId = 1;

    // Consumed so the JIT cannot eliminate the measured work.
    private int _sink;

    public ParamsAllocationBenchmark() : this(false) { }
    public ParamsAllocationBenchmark(bool withLogging = false)
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        if (withLogging)
        {
            var logFactory = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Trace));
            builder.UseLoggerFactory(logFactory);
            builder.LogSensitiveData(true);
        }

        _dbCtx = (DataContext)builder.CreateDataContext();
        _db = _dbCtx;
        _repo = new TestDataRepository(_db);

        _cmd1 = _repo.SimpleEntity
            .Where(e => e.Id == SqlFunctions.Parameter<int>(0))
            .AnyCommand()
            .Prepare(true);

        _cmd2 = _repo.SimpleEntity
            .Where(e => e.Id > SqlFunctions.Parameter<int>(0) && e.Id < SqlFunctions.Parameter<int>(1))
            .AnyCommand()
            .Prepare(true);

        ((IConnectionManager)_db).EnsureConnectionOpen();
        _conn = _dbCtx.GetConnection();
        // Bound once, mirroring production: GetDbCommand now takes parameter creation as a
        // delegate rather than the context, so the arms below must not allocate one per call.
        _createParam = _dbCtx.CreateParam;
        _dbCmd = (DbPreparedQueryCommand<bool>)_cmd1;
    }

    // ---- NextORM public sync API: span params vs explicit array -----------------
    // M() covers 0 args; the span overload covers 1..N with the compiler's inline array.

    [Benchmark]
    public bool Nextorm_EntityAny_1Arg_Span()
        => _repo.SimpleEntity.Where(e => e.Id == SqlFunctions.Parameter<int>(0)).Any(s_boxedId);

    [Benchmark]
    public bool Nextorm_EntityAny_1Arg_Array()
        => _repo.SimpleEntity.Where(e => e.Id == SqlFunctions.Parameter<int>(0)).Any(new object[] { s_boxedId });

    [Benchmark]
    public bool Nextorm_EntityAny_3Arg_Span()
        => _repo.SimpleEntity
            .Where(e => e.Id > SqlFunctions.Parameter<int>(0) && e.Id < SqlFunctions.Parameter<int>(1) && e.Id != SqlFunctions.Parameter<int>(2))
            .Any(s_boxedId, s_boxedId, s_boxedId);

    [Benchmark]
    public bool Nextorm_EntityAny_3Arg_Array()
        => _repo.SimpleEntity
            .Where(e => e.Id > SqlFunctions.Parameter<int>(0) && e.Id < SqlFunctions.Parameter<int>(1) && e.Id != SqlFunctions.Parameter<int>(2))
            .Any(new object[] { s_boxedId, s_boxedId, s_boxedId });

    // ---- Where does the builder path spend, before the plan-cache lookup? -------

    // Builder only: fresh QueryCommand + exists(subquery) wrapper. No plan build, no lookup.
    [Benchmark]
    public int EntityAnyCommand_Build()
        => _repo.SimpleEntity.Where(e => e.Id == SqlFunctions.Parameter<int>(0)).AnyCommand().Paging.Limit;

    // Builder + full PrepareCommand (from/join/columns/where/group/sort walks + clause hashes).
    // This is what GetAnyCommand does per call on a fresh command; no SQL is built.
    [Benchmark]
    public int EntityAnyCommand_BuildAndPrepare()
    {
        var cmd = _repo.SimpleEntity.Where(e => e.Id == SqlFunctions.Parameter<int>(0)).AnyCommand();
        cmd.PrepareCommand(false, CancellationToken.None);
        return cmd.Paging.Limit;
    }

    // ---- NextORM hot API: full async execute ------------------------------------

    [Benchmark(Baseline = true)]
    public async Task Nextorm_Any_1Arg_Params()
    {
        for (var i = 0; i < Iterations; i++)
            if (await _db.AnyAsync(_cmd1, i)) _sink++;
    }

    [Benchmark]
    public async Task Nextorm_Any_1Arg_ReusedArray()
    {
        var args = _args1;
        for (var i = 0; i < Iterations; i++)
        {
            args[0] = i;
            if (await _db.AnyAsync(_cmd1, args)) _sink++;
        }
    }

    [Benchmark]
    public async Task Nextorm_Any_2Arg_Params()
    {
        for (var i = 0; i < Iterations; i++)
            if (await _db.AnyAsync(_cmd2, i, i + 1)) _sink++;
    }

    [Benchmark]
    public async Task Nextorm_Any_2Arg_ReusedArray()
    {
        var args = _args2;
        for (var i = 0; i < Iterations; i++)
        {
            args[0] = i;
            args[1] = i + 1;
            if (await _db.AnyAsync(_cmd2, args)) _sink++;
        }
    }

    // ---- No DB: GetDbCommand param application only -----------------------------

    [Benchmark]
    public void GetDbCommand_1Arg_Params()
    {
        for (var i = 0; i < Iterations; i++)
            _sink += _dbCmd.GetDbCommand(new object[] { i }, _createParam, _conn).Parameters.Count;
    }

    [Benchmark]
    public void GetDbCommand_1Arg_ReusedArray()
    {
        var args = _args1;
        for (var i = 0; i < Iterations; i++)
        {
            args[0] = i;
            _sink += _dbCmd.GetDbCommand(args, _createParam, _conn).Parameters.Count;
        }
    }

    // ---- Language-level proof: params array vs params ReadOnlySpan --------------

    [Benchmark]
    public int Generic_ParamsArray() => SumArray(1, 2);

    [Benchmark]
    public int Generic_ParamsReadOnlySpan() => SumSpan(1, 2);

    private static int SumArray(params int[] xs)
    {
        var s = 0;
        for (var i = 0; i < xs.Length; i++) s += xs[i];
        return s;
    }

    private static int SumSpan(params ReadOnlySpan<int> xs)
    {
        var s = 0;
        foreach (var x in xs) s += x;
        return s;
    }
}
