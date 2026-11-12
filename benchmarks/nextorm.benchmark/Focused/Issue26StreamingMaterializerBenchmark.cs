using System.Data;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Microsoft.Data.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Diagnostic-only matched SQLite row-stream probe for Issue #26. It isolates materializer and
/// Current ownership/copy behavior; it is not a NextORM implementation or a product speedup claim.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(Issue26StreamingMaterializerConfig))]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Issue26StreamingMaterializerBenchmark
{
    private const int Rows = 1_000;
    private SqliteConnection _connection = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var path = Path.Combine(Path.GetTempPath(), $"issue26-stream-{Guid.NewGuid():N}.db");
        _connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await _connection.OpenAsync();
        await using var command = _connection.CreateCommand();
        command.CommandText = "CREATE TABLE data (" + string.Join(",", Enumerable.Range(0, 32).Select(i => $"c{i} INTEGER")) + ");" +
            "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000) INSERT INTO data SELECT " +
            string.Join(",", Enumerable.Range(0, 32).Select(i => $"x+{i}")) + " FROM n;";
        await command.ExecuteNonQueryAsync();

        foreach (var fields in new[] { 4, 16, 32 })
        {
            var cls = fields switch { 4 => await RunClass<S32>(fields), 16 => await RunClass<S128>(fields), _ => await RunClass<S256>(fields) };
            var val = fields switch { 4 => await RunValue<S32>(fields), 16 => await RunValue<S128>(fields), _ => await RunValue<S256>(fields) };
            var rf = fields switch { 4 => await RunRef<S32>(fields), 16 => await RunRef<S128>(fields), _ => await RunRef<S256>(fields) };
            var expected = Expected(fields);
            if (cls != expected || val != expected || rf != expected)
                throw new InvalidOperationException($"Correctness mismatch fields={fields}: class={cls}, value={val}, ref={rf}, expected={expected}");
            Console.WriteLine($"Issue26 stream validation fields={fields} rows={Rows} checksum={expected}");
        }
    }

    [GlobalCleanup] public async Task Cleanup() => await _connection.DisposeAsync();

    [Benchmark(Baseline = true), BenchmarkCategory("32B")] public Task<long> Class32() => RunClass<S32>(4);
    [Benchmark, BenchmarkCategory("32B")] public Task<long> Value32() => RunValue<S32>(4);
    [Benchmark, BenchmarkCategory("32B")] public Task<long> RefReadonly32() => RunRef<S32>(4);
    [Benchmark(Baseline = true), BenchmarkCategory("128B")] public Task<long> Class128() => RunClass<S128>(16);
    [Benchmark, BenchmarkCategory("128B")] public Task<long> Value128() => RunValue<S128>(16);
    [Benchmark, BenchmarkCategory("128B")] public Task<long> RefReadonly128() => RunRef<S128>(16);
    [Benchmark(Baseline = true), BenchmarkCategory("256B")] public Task<long> Class256() => RunClass<S256>(32);
    [Benchmark, BenchmarkCategory("256B")] public Task<long> Value256() => RunValue<S256>(32);
    [Benchmark, BenchmarkCategory("256B")] public Task<long> RefReadonly256() => RunRef<S256>(32);

    private SqliteCommand Query(int fields)
    {
        var c = _connection.CreateCommand();
        c.CommandText = $"SELECT {string.Join(",", Enumerable.Range(0, fields).Select(i => $"c{i}"))} FROM data";
        return c;
    }

    private async Task<long> RunClass<T>(int fields) where T : struct, IPayload<T>
    {
        await using var command = Query(fields);
        await using var reader = await command.ExecuteReaderAsync();
        await using var rows = new ClassEnumerator<T>(reader, fields);
        long sum = 0;
        while (await rows.MoveNextAsync()) sum += T.Sum(in rows.Current.Value, fields);
        return sum;
    }

    private async Task<long> RunValue<T>(int fields) where T : struct, IPayload<T>
    {
        await using var command = Query(fields);
        await using var reader = await command.ExecuteReaderAsync();
        await using var rows = new ValueEnumerator<T>(reader, fields);
        long sum = 0;
        while (await rows.MoveNextAsync()) { var row = rows.Current; sum += T.Sum(in row, fields); }
        return sum;
    }

    private async Task<long> RunRef<T>(int fields) where T : struct, IPayload<T>
    {
        await using var command = Query(fields);
        await using var reader = await command.ExecuteReaderAsync();
        await using var rows = new RefEnumerator<T>(reader, fields);
        long sum = 0;
        while (await rows.MoveNextAsync()) { ref readonly var row = ref rows.Current; sum += T.Sum(in row, fields); }
        return sum;
    }

    private static long Expected(int fields) => Enumerable.Range(1, Rows).Sum(x => (long)fields * x + (long)fields * (fields - 1) / 2);
    private interface IPayload<T> where T : struct
    {
        static abstract T Read(IDataRecord reader, int fields);
        static abstract void Fill(IDataRecord reader, int fields, ref T value);
        static abstract long Sum(in T value, int fields);
    }

    private sealed class RowBox<T>(T value) { public T Value = value; }

    private sealed class ClassEnumerator<T>(SqliteDataReader reader, int fields) : IAsyncDisposable where T : struct, IPayload<T>
    {
        private RowBox<T>? _current;
        public RowBox<T> Current => _current!;
        public async ValueTask<bool> MoveNextAsync()
        {
            if (!await reader.ReadAsync()) return false;
            var value = default(T);
            T.Fill(reader, fields, ref value);
            _current = new RowBox<T>(value);
            return true;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ValueEnumerator<T>(SqliteDataReader reader, int fields) : IAsyncDisposable where T : struct, IPayload<T>
    {
        private T _current;
        public T Current => _current;
        public async ValueTask<bool> MoveNextAsync()
        {
            if (!await reader.ReadAsync()) return false;
            _current = T.Read(reader, fields);
            return true;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RefEnumerator<T>(SqliteDataReader reader, int fields) : IAsyncDisposable where T : struct, IPayload<T>
    {
        private T _current;
        public ref readonly T Current => ref _current;
        public async ValueTask<bool> MoveNextAsync()
        {
            if (!await reader.ReadAsync()) return false;
            T.Fill(reader, fields, ref _current);
            return true;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private struct S32 : IPayload<S32>
    {
        public long A,B,C,D;
        public static S32 Read(IDataRecord r, int n) { var x = default(S32); Fill(r,n,ref x); return x; }
        public static void Fill(IDataRecord r,int n,ref S32 x) { x.A=r.GetInt64(0);x.B=r.GetInt64(1);x.C=r.GetInt64(2);x.D=r.GetInt64(3); }
        public static long Sum(in S32 x,int n) => x.A+x.B+x.C+x.D;
    }
    private struct S128 : IPayload<S128>
    {
        public long A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P;
        public static S128 Read(IDataRecord r,int n) { var x=default(S128);Fill(r,n,ref x);return x; }
        public static void Fill(IDataRecord r,int n,ref S128 x) { x.A=r.GetInt64(0);x.B=r.GetInt64(1);x.C=r.GetInt64(2);x.D=r.GetInt64(3);x.E=r.GetInt64(4);x.F=r.GetInt64(5);x.G=r.GetInt64(6);x.H=r.GetInt64(7);x.I=r.GetInt64(8);x.J=r.GetInt64(9);x.K=r.GetInt64(10);x.L=r.GetInt64(11);x.M=r.GetInt64(12);x.N=r.GetInt64(13);x.O=r.GetInt64(14);x.P=r.GetInt64(15); }
        public static long Sum(in S128 x,int n) => x.A+x.B+x.C+x.D+x.E+x.F+x.G+x.H+x.I+x.J+x.K+x.L+x.M+x.N+x.O+x.P;
    }
    private struct S256 : IPayload<S256>
    {
        public long A,B,C,D,E,F,G,H,I,J,K,L,M,N,O,P,Q,R,S,T,U,V,W,X,Y,Z,AA,AB,AC,AD,AE,AF;
        public static S256 Read(IDataRecord r,int n) { var x=default(S256);Fill(r,n,ref x);return x; }
        public static void Fill(IDataRecord r,int n,ref S256 x) { x.A=r.GetInt64(0);x.B=r.GetInt64(1);x.C=r.GetInt64(2);x.D=r.GetInt64(3);x.E=r.GetInt64(4);x.F=r.GetInt64(5);x.G=r.GetInt64(6);x.H=r.GetInt64(7);x.I=r.GetInt64(8);x.J=r.GetInt64(9);x.K=r.GetInt64(10);x.L=r.GetInt64(11);x.M=r.GetInt64(12);x.N=r.GetInt64(13);x.O=r.GetInt64(14);x.P=r.GetInt64(15);x.Q=r.GetInt64(16);x.R=r.GetInt64(17);x.S=r.GetInt64(18);x.T=r.GetInt64(19);x.U=r.GetInt64(20);x.V=r.GetInt64(21);x.W=r.GetInt64(22);x.X=r.GetInt64(23);x.Y=r.GetInt64(24);x.Z=r.GetInt64(25);x.AA=r.GetInt64(26);x.AB=r.GetInt64(27);x.AC=r.GetInt64(28);x.AD=r.GetInt64(29);x.AE=r.GetInt64(30);x.AF=r.GetInt64(31); }
        public static long Sum(in S256 x,int n) => x.A+x.B+x.C+x.D+x.E+x.F+x.G+x.H+x.I+x.J+x.K+x.L+x.M+x.N+x.O+x.P+x.Q+x.R+x.S+x.T+x.U+x.V+x.W+x.X+x.Y+x.Z+x.AA+x.AB+x.AC+x.AD+x.AE+x.AF;
    }
}

internal sealed class Issue26StreamingMaterializerConfig : ManualConfig
{
    public Issue26StreamingMaterializerConfig() => AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance).WithWarmupCount(3).WithIterationCount(5));
}
