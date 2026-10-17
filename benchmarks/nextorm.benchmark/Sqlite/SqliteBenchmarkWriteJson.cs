using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Dapper;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SQLite;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NextORM.Core;
using NextORM.Sqlite;
using IDataContext = NextORM.Core.IDataContext;
using L2dbColumn = LinqToDB.Mapping.ColumnAttribute;
using L2dbPrimaryKey = LinqToDB.Mapping.PrimaryKeyAttribute;
using L2dbTable = LinqToDB.Mapping.TableAttribute;

namespace NextORM.Benchmark;

/// <summary>
/// Focused query-path benchmark for #39 <c>WriteJson</c>: streaming a query's projection as a JSON
/// array directly to a caller-owned stream, versus the buffered <c>ToList()</c> materialization of the
/// same projection. The query path is what is measured — the destination is a single reusable
/// <see cref="MemoryStream"/> reset before every call, so its backing buffer is allocated once during
/// warm-up and never during the measured region; the write itself is the real <c>Utf8JsonWriter</c> +
/// pooled-buffer flush per row.
/// <para>
/// <c>WriteJson_Array_Scalar</c> streams a numeric scalar projection, the shape the streaming contract
/// promises is allocation-flat (no <c>TResult</c> per row); comparing its <c>Allocated</c> at
/// <see cref="RowCount"/> 1k and 10k shows whether the numeric path is flat or linear in the row count.
/// <c>WriteJson_Array_ScalarPayload</c> does the same for a variable-width string scalar (512+ bytes),
/// where the per-row driver string is transient garbage, not retention: total allocation is expected to
/// be linear in the row count while the peak live set stays <c>O(buffer)</c>. <c>WriteJsonAsync_*</c>
/// exercises the async row loop over the same writer/buffer lifecycle. <c>ToList_Dto</c> is the buffered
/// baseline: the same DTO materialized into a list, i.e. the row-retaining shape.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkWriteJson
{
    private const int InsertBatchSize = 500;

    /// <summary>The base width, in ASCII bytes, of the variable-width <c>payload</c> column.</summary>
    private const int PayloadBaseWidth = 512;

    private readonly MemoryStream _sink = new();
    private readonly BenchmarkSerializationSink _competitorSink = new();

    private IDataContext _db = null!;
    private EntityBuilder<JsonBenchEntity> _entities = null!;
    private string _path = null!;
    private SqliteConnection _dapperConn = null!;
    private DataConnection _linq2Db = null!;
    private JsonBenchDbContext _efCtx = null!;

    /// <summary>The number of seeded rows; the triple 1k/10k/100k shows whether allocation scales with row count.</summary>
    [Params(1_000, 10_000, 100_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"json-stream-{RowCount}-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var create = conn.CreateCommand();
            create.CommandText = "create table json_bench (id integer primary key, name text, payload text);";
            create.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        Seed(RowCount);
        _entities = _db.From<JsonBenchEntity>();

        // Warm the plan metadata and the sink's backing buffer, and fail loudly if a shape is rejected
        // (a benchmark that throws in the measured region is not evidence). The lengths are discarded.
        _ = WriteJson_Array_Scalar();
        _ = WriteJson_Array_ScalarPayload();
        _ = WriteJson_Array_Dto();
        _ = WriteJson_Array_WideDto();
        _ = WriteJsonAsync_Array_Scalar().GetAwaiter().GetResult();
        _ = WriteJsonAsync_Array_ScalarPayload().GetAwaiter().GetResult();
        _ = ToList_Dto();

        _dapperConn = new SqliteConnection($"Data Source={_path}");
        _dapperConn.Open();
        _linq2Db = new DataConnection(new DataOptions().UseSQLite($"Data Source={_path}", SQLiteProvider.Microsoft));
        var efBuilder = new DbContextOptionsBuilder<JsonBenchDbContext>();
        efBuilder.UseSqlite($"Filename={_path}");
        efBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        _efCtx = new JsonBenchDbContext(efBuilder.Options);

        ValidateCompetitorJson();
        _sink.SetLength(0);
    }

    /// <summary>
    /// Setup-time proof that the three materialize→serialize arms emit the same logical rows as the
    /// native Nextorm JSON arm. Runs outside the timed region and fails the benchmark if they differ.
    /// </summary>
    private void ValidateCompetitorJson()
    {
        _ = WriteJson_Array_Dto();
        _sink.Position = 0;
        var baseline = MeasureJson(_sink);

        var dapper = _dapperConn.Query<JsonBenchDto>("select id, name from json_bench").ToList();
        var linq2Db = _linq2Db.GetTable<JsonBenchLinqRow>()
            .Select(x => new JsonBenchDto { Id = x.Id, Name = x.Name }).ToList();
        var ef = _efCtx.Rows.Select(x => new JsonBenchDto { Id = x.Id, Name = x.Name }).ToList();

        CompareJson("WriteJson/dapper", baseline, MeasureJson(SerializeToScratch(dapper)));
        CompareJson("WriteJson/linq2db", baseline, MeasureJson(SerializeToScratch(linq2Db)));
        CompareJson("WriteJson/EFCore", baseline, MeasureJson(SerializeToScratch(ef)));
        BenchmarkComparisonValidation.Report("WriteJson/nextorm", baseline.Count, baseline.Checksum, $"rowCount={RowCount}");
    }

    private static MemoryStream SerializeToScratch(List<JsonBenchDto> rows)
    {
        var scratch = new MemoryStream();
        JsonSerializer.Serialize(scratch, rows);
        scratch.Position = 0;
        return scratch;
    }

    private static (int Count, long Checksum) MeasureJson(Stream stream)
    {
        var rows = JsonSerializer.Deserialize<List<JsonBenchDto>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? [];
        return BenchmarkComparisonValidation.Measure(rows, r => r.Id);
    }

    private static void CompareJson(string context, (int Count, long Checksum) expected, (int Count, long Checksum) actual)
    {
        BenchmarkComparisonValidation.EnsureCount(expected.Count, actual.Count, context);
        BenchmarkComparisonValidation.EnsureChecksum(expected.Checksum, actual.Checksum, context);
    }

    private void Seed(int count)
    {
        var rows = new JsonBenchEntity[count];
        for (var i = 1; i <= count; i++)
            rows[i - 1] = new JsonBenchEntity { Id = i, Name = "row-" + i, Payload = MakePayload(i) };

        for (var offset = 0; offset < rows.Length; offset += InsertBatchSize)
        {
            var take = Math.Min(InsertBatchSize, rows.Length - offset);
            var batch = new JsonBenchEntity[take];
            Array.Copy(rows, offset, batch, 0, take);
            _db.CreateInsertBuilder<JsonBenchEntity>().Values(batch).Insert();
        }
    }

    /// <summary>A 512..639-byte ASCII payload whose width varies per row, so no row shares the same retained value.</summary>
    private static string MakePayload(int i)
    {
        var width = PayloadBaseWidth + (i % 128);
        return new string('p', width);
    }

    /// <summary>Streams the numeric scalar projection as a JSON array; the shape the streaming contract promises stays allocation-flat.</summary>
    /// <returns>The number of JSON bytes written, so the call is not dead-code-eliminated.</returns>
    [Benchmark]
    public long WriteJson_Array_Scalar()
    {
        _sink.SetLength(0);
        _entities.Select(x => x.Id).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Streams the variable-width string scalar projection (512+ bytes per row) as a JSON array.</summary>
    /// <returns>The number of JSON bytes written.</returns>
    [Benchmark]
    public long WriteJson_Array_ScalarPayload()
    {
        _sink.SetLength(0);
        _entities.Select(x => x.Payload).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Streams a flat DTO (int + driver-returned string) as a JSON array.</summary>
    /// <returns>The number of JSON bytes written.</returns>
    [Benchmark]
    public long WriteJson_Array_Dto()
    {
        _sink.SetLength(0);
        _entities.Select(x => new JsonBenchDto { Id = x.Id, Name = x.Name }).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Streams a flat DTO (int + variable-width 512+ byte string) as a JSON array.</summary>
    /// <returns>The number of JSON bytes written.</returns>
    [Benchmark]
    public long WriteJson_Array_WideDto()
    {
        _sink.SetLength(0);
        _entities.Select(x => new JsonBenchWideDto { Id = x.Id, Payload = x.Payload }).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Asynchronously streams the numeric scalar projection as a JSON array.</summary>
    /// <returns>The number of JSON bytes written.</returns>
    [Benchmark]
    public async Task<long> WriteJsonAsync_Array_Scalar()
    {
        _sink.SetLength(0);
        await _entities.Select(x => x.Id).WriteJsonAsync(_sink);
        return _sink.Length;
    }

    /// <summary>Asynchronously streams the variable-width string scalar projection (512+ bytes per row) as a JSON array.</summary>
    /// <returns>The number of JSON bytes written.</returns>
    [Benchmark]
    public async Task<long> WriteJsonAsync_Array_ScalarPayload()
    {
        _sink.SetLength(0);
        await _entities.Select(x => x.Payload).WriteJsonAsync(_sink);
        return _sink.Length;
    }

    /// <summary>Baseline: the same DTO projection materialized through the buffered <c>ToList()</c> terminal.</summary>
    /// <returns>The sum of the materialized ids, so the list is consumed.</returns>
    [Benchmark(Baseline = true)]
    public long ToList_Dto()
    {
        var sum = 0L;
        foreach (var row in _entities.Select(x => new JsonBenchDto { Id = x.Id, Name = x.Name }).ToList())
            sum += row.Id;
        return sum;
    }

    /// <summary>Closest equivalent for Dapper: materialize the DTO rows, then serialize the list.</summary>
    /// <remarks>This is not a zero-materialization arm; the label deliberately says materialize→serialize.</remarks>
    /// <returns>The number of JSON bytes written to the non-retaining sink.</returns>
    [Benchmark]
    public long Dapper_ToList_Json()
    {
        _competitorSink.Reset();
        var rows = _dapperConn.Query<JsonBenchDto>("select id, name from json_bench").ToList();
        JsonSerializer.Serialize(_competitorSink, rows);
        return _competitorSink.Bytes;
    }

    /// <summary>Closest equivalent for linq2db: materialize the DTO rows, then serialize the list.</summary>
    /// <returns>The number of JSON bytes written to the non-retaining sink.</returns>
    [Benchmark]
    public long Linq2Db_ToList_Json()
    {
        _competitorSink.Reset();
        var rows = _linq2Db.GetTable<JsonBenchLinqRow>()
            .Select(x => new JsonBenchDto { Id = x.Id, Name = x.Name })
            .ToList();
        JsonSerializer.Serialize(_competitorSink, rows);
        return _competitorSink.Bytes;
    }

    /// <summary>Closest equivalent for EF Core: materialize the DTO rows, then serialize the list.</summary>
    /// <returns>The number of JSON bytes written to the non-retaining sink.</returns>
    [Benchmark]
    public long EFCore_ToList_Json()
    {
        _competitorSink.Reset();
        var rows = _efCtx.Rows.Select(x => new JsonBenchDto { Id = x.Id, Name = x.Name }).ToList();
        JsonSerializer.Serialize(_competitorSink, rows);
        return _competitorSink.Bytes;
    }
}

[SqlTable("json_bench")]
[Table("json_bench")]
public sealed class JsonBenchEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("payload")]
    public string? Payload { get; set; }
}

/// <summary>The flat streaming DTO: an int and a string, the two kinds the JSON shape plan supports.</summary>
public sealed class JsonBenchDto
{
    public int Id { get; set; }

    public string? Name { get; set; }
}

/// <summary>The variable-width streaming DTO: an int and a 512+ byte string.</summary>
public sealed class JsonBenchWideDto
{
    public int Id { get; set; }

    public string? Payload { get; set; }
}

/// <summary>linq2db mapping for the temporary <c>json_bench</c> table used by the serializer competitor arm.</summary>
[L2dbTable("json_bench")]
public sealed class JsonBenchLinqRow
{
    [L2dbPrimaryKey]
    [L2dbColumn("id")]
    public int Id { get; set; }

    [L2dbColumn("name")]
    public string? Name { get; set; }
}

/// <summary>EF Core context over the temporary <c>json_bench</c> table used by the serializer competitor arm.</summary>
public sealed class JsonBenchDbContext : DbContext
{
    public JsonBenchDbContext(DbContextOptions<JsonBenchDbContext> options) : base(options)
    {
    }

    public DbSet<JsonBenchEntity> Rows { get; set; } = null!;
}
