using System.Data;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Benchmark.Acceptance;

/// <summary>
/// BenchmarkDotNet configuration for the acceptance gate. Iterations and warmups are read from
/// <c>NEXTORM_ACCEPTANCE_ITERATIONS</c> / <c>NEXTORM_ACCEPTANCE_WARMUP</c> (default 20 / 5) so the
/// harness can re-run additional 40-iteration rounds when the time gate is inconclusive. Results are
/// written under <c>NEXTORM_ACCEPTANCE_ARTIFACTS</c> so each fresh process keeps its own report.
/// The in-process emit toolchain keeps a run self-contained in one process; the harness still starts a
/// fresh process per A/B run.
/// </summary>
internal sealed class AcceptanceConfig : ManualConfig
{
    public AcceptanceConfig()
    {
        var iterations = ReadInt("NEXTORM_ACCEPTANCE_ITERATIONS", 20);
        var warmup = ReadInt("NEXTORM_ACCEPTANCE_WARMUP", 5);

        var artifacts = Environment.GetEnvironmentVariable("NEXTORM_ACCEPTANCE_ARTIFACTS");
        ArtifactsPath = string.IsNullOrEmpty(artifacts)
            ? Path.Combine(BenchmarkArtifacts.Path, "d168-acceptance")
            : artifacts;

        AddLogger(ConsoleLogger.Default);
        AddExporter(JsonExporter.Full);
        AddJob(Job.Default
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithWarmupCount(warmup)
            .WithIterationCount(iterations));
    }

    private static int ReadInt(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value > 0 ? value : fallback;
}

/// <summary>
/// Acceptance perf gate for #168: buffered materialization of a numeric projection through the SQL
/// Server general buffered mapper (<see cref="DataContext.MapColumnExpression"/>, the <c>mapColumn</c>
/// delegate that <c>RowMapperFactory</c> compiles) on a strict <see cref="StrictNumericDataReader"/>.
/// </summary>
/// <remarks>
/// The baseline (<c>GetValue</c> + <c>Convert.ChangeType</c>) boxes every non-null numeric value per
/// row; the candidate dispatches on <see cref="IDataRecord.GetFieldType"/> and reads with the typed
/// getter. <see cref="OperationsPerInvoke"/> is <see cref="RowCount"/>, so BDN reports time and
/// allocations <b>per row</b>. <see cref="Setup"/> also materializes a single row and prints the
/// reader's <c>GetValue</c> call count as <c>D168_SELFCHECK getValueCalls=…</c>; the gate asserts it is
/// zero for the candidate and non-zero for the baseline. This benchmark only runs with the
/// <c>acceptance</c> category, so it cannot affect other benchmark suites.
/// </remarks>
[MemoryDiagnoser]
[BenchmarkCategory("acceptance")]
[Config(typeof(AcceptanceConfig))]
public class SqlServerBufferedNumericAcceptanceBenchmark
{
    /// <summary>Rows materialized per benchmark invocation (the projection has eleven numeric columns).</summary>
    public const int RowCount = 10_000;

    private Func<IDataRecord, double> _mapper = null!;
    private StrictNumericDataReader _reader = null!;
    private double _sink;

    /// <summary>Builds the buffered mapper and the strict reader, then records the baseline boxing count.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var context = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());

        var record = Expression.Parameter(typeof(IDataRecord), "record");
        Expression sum = Expression.Constant(0d);

        // The closed numeric matrix plus both null shapes of int?/decimal?.
        var columns = NumericColumn.CreateAll();
        foreach (var column in columns)
        {
            var select = new SelectExpression(column.PropertyType)
            {
                Index = column.Index,
                PropertyName = column.Name,
            };

            var read = context.MapColumnExpression(select, record);
            var value = read;
            if (value.Type.IsGenericType && value.Type.GetGenericTypeDefinition() == typeof(Nullable<>))
                value = Expression.Call(value, value.Type.GetMethod(nameof(Nullable<int>.GetValueOrDefault), Type.EmptyTypes)!);

            value = Expression.Convert(value, typeof(double));
            sum = Expression.Add(sum, value);
        }

        _mapper = Expression.Lambda<Func<IDataRecord, double>>(sum, record).Compile();
        _reader = StrictNumericDataReaderFactory.CreateSample();

        _sink = _mapper(_reader);
        Console.WriteLine($"D168_SELFCHECK getValueCalls={_reader.GetValueCalls} typedReadCalls={_reader.TypedReadCalls} rows=1");
    }

    /// <summary>Materializes <see cref="RowCount"/> rows and returns a checksum that keeps the reads live.</summary>
    /// <returns>The sum of every materialized numeric value; assigned to <see cref="_sink"/> to defeat dead-code elimination.</returns>
    [Benchmark(OperationsPerInvoke = RowCount)]
    public double MaterializeRows()
    {
        var reader = _reader;
        var mapper = _mapper;
        var total = 0d;

        for (var i = 0; i < RowCount; i++)
            total += mapper(reader);

        _sink = total;
        return total;
    }

    private sealed record NumericColumn(int Index, string Name, Type PropertyType, Type Storage, bool IsNull)
    {
        public static IReadOnlyList<NumericColumn> CreateAll() =>
        [
            new(0, "Int32", typeof(int), typeof(int), false),
            new(1, "Int64", typeof(long), typeof(long), false),
            new(2, "Int16", typeof(short), typeof(short), false),
            new(3, "Byte", typeof(byte), typeof(byte), false),
            new(4, "Decimal", typeof(decimal), typeof(decimal), false),
            new(5, "Double", typeof(double), typeof(double), false),
            new(6, "Single", typeof(float), typeof(float), false),
            new(7, "Int32Value", typeof(int?), typeof(int), false),
            new(8, "Int32Null", typeof(int?), typeof(int), true),
            new(9, "DecimalValue", typeof(decimal?), typeof(decimal), false),
            new(10, "DecimalNull", typeof(decimal?), typeof(decimal), true),
        ];
    }

    private static class StrictNumericDataReaderFactory
    {
        public static StrictNumericDataReader CreateSample()
        {
            var columns = SqlServerBufferedNumericAcceptanceBenchmark.NumericColumn.CreateAll();
            var values = new object?[columns.Count];
            var storage = new Type[columns.Count];
            var isNull = new bool[columns.Count];

            foreach (var column in columns)
            {
                storage[column.Index] = column.Storage;
                isNull[column.Index] = column.IsNull;
                values[column.Index] = column.IsNull
                    ? DBNull.Value
                    : column.Storage switch
                    {
                        _ when column.Storage == typeof(int) => 1_234_567,
                        _ when column.Storage == typeof(long) => 9_876_543_210L,
                        _ when column.Storage == typeof(short) => (short)32_000,
                        _ when column.Storage == typeof(byte) => (byte)200,
                        _ when column.Storage == typeof(decimal) => 123_456.789m,
                        _ when column.Storage == typeof(double) => 123_456.789d,
                        _ when column.Storage == typeof(float) => 123_456.5f,
                        _ => throw new NotSupportedException($"No sample value for {column.Storage.Name}."),
                    };
            }

            return new StrictNumericDataReader(values, storage, isNull);
        }
    }
}
