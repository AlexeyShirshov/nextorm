using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Compares projecting a mapped entity property against the by-name
/// <see cref="SqlFunctions.Column{T}(object, string)"/> accessor, both on SQL generation (plan cache
/// disabled, so the visitor runs every iteration) and end-to-end. The mapped <c>id</c> column yields
/// identical SQL in both forms, so the delta is the translation cost; the unmapped <c>r</c> column has
/// no property and can only be read by name.
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkColumnByName
{
    private const int Iterations = 100;
    private readonly IDataContext _db;
    private readonly TestDataRepository _repo;

    public SqliteBenchmarkColumnByName()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _repo = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();
    }

    // Mapped property: the reference SQL-build shape.
    [Benchmark(Baseline = true)]
    public IPreparedQueryCommand<long> Build_Sql_Property()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_repo.ComplexEntity.Select(it => it.Id), false, false, CancellationToken.None);
        return r!;
    }

    // Same column (id) addressed by name: identical SQL, so the delta is the by-name translation.
    [Benchmark]
    public IPreparedQueryCommand<long> Build_Sql_ColumnByName()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(
                _repo.ComplexEntity.Select(it => SqlFunctions.Column<long>(it, "id")), false, false, CancellationToken.None);
        return r!;
    }

    // A column with no entity property: the case the accessor exists for.
    [Benchmark]
    public IPreparedQueryCommand<float> Build_Sql_ColumnByName_Unmapped()
    {
        IPreparedQueryCommand<float>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(
                _repo.ComplexEntity.Select(it => SqlFunctions.Column<float>(it, "r")), false, false, CancellationToken.None);
        return r!;
    }

    // Control: a non-member projection (computed column). If this matches the by-name arms, the delta
    // is the generic non-member projection path, not anything specific to Column<T>.
    [Benchmark]
    public IPreparedQueryCommand<long> Build_Sql_Computed()
    {
        IPreparedQueryCommand<long>? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(_repo.ComplexEntity.Select(it => it.Id + 0), false, false, CancellationToken.None);
        return r!;
    }

    // The projected member name differs from the column, so the rename-aware alias path runs.
    [Benchmark]
    public object Build_Sql_ColumnByName_Rename()
    {
        object? r = null;
        for (var i = 0; i < Iterations; i++)
            r = _db.GetPreparedQueryCommand(
                _repo.ComplexEntity.Select(it => new { X = SqlFunctions.Column<long>(it, "id") }), false, false, CancellationToken.None);
        return r!;
    }

    // By-name column on a joined projection (p.Item2), exercising the table-alias resolution.
    [Benchmark]
    public object Build_Sql_ColumnByName_Join()
    {
        object? r = null;
        for (var i = 0; i < Iterations; i++)
        {
            var cmd = _repo.SimpleEntity
                .Join(_repo.ComplexEntity, (a, b) => a.Id == (int)b.Id)
                .Select(p => new { p.Item1.Id, R = SqlFunctions.Column<float>(p.Item2, "r") });
            r = _db.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        }
        return r!;
    }

    // End-to-end (DB-bound, plan cache on): mapped property.
    [Benchmark]
    public long ToList_Property()
    {
        long sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _repo.ComplexEntity.Select(it => it.Id).ToList())
                sum += row;
        }
        return sum;
    }

    // End-to-end (DB-bound, plan cache on): same column by name.
    [Benchmark]
    public long ToList_ColumnByName()
    {
        long sum = 0;
        for (var i = 0; i < Iterations; i++)
        {
            foreach (var row in _repo.ComplexEntity.Select(it => SqlFunctions.Column<long>(it, "id")).ToList())
                sum += row;
        }
        return sum;
    }
}
