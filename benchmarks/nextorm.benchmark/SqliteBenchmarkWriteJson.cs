using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

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

    private IDataContext _db = null!;
    private EntityBuilder<JsonBenchEntity> _entities = null!;
    private string _path = null!;

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
        _sink.SetLength(0);
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
            _db.InsertInto<JsonBenchEntity>().Values(batch).Insert();
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
}

[SqlTable("json_bench")]
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
