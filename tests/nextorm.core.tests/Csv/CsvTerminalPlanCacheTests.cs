using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Sqlite;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Cache-key correctness coverage for the bounded per-context CSV terminal plan cache:
/// the key must describe every compiled-plan dependency, a differing option must never reuse a
/// plan, <c>ValueTransform</c> must bypass lookup and publication, and a process-wide generation
/// bump / purge must invalidate stale entries.
/// </summary>
public class CsvTerminalPlanCacheTests
{
    [SqlTable("csv_cache_row")]
    private sealed class CsvCacheRow
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    private sealed class FakeRecord : IDataRecord
    {
        private readonly Type[] _types;

        public FakeRecord(Type[] types) => _types = types;

        public int FieldCount => _types.Length;

        public object this[int i] => throw new NotSupportedException();

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => _types[i].Name;

        public DateTime GetDateTime(int i) => throw new NotSupportedException();

        public decimal GetDecimal(int i) => throw new NotSupportedException();

        public double GetDouble(int i) => throw new NotSupportedException();

        public Type GetFieldType(int i) => _types[i];

        public float GetFloat(int i) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => throw new NotSupportedException();

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetName(int i) => $"c{i}";

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => throw new NotSupportedException();

        public object GetValue(int i) => throw new NotSupportedException();

        public int GetValues(object[] values) => throw new NotSupportedException();

        public bool IsDBNull(int i) => false;
    }

    private static SqliteDataContext CreateDb(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"nextorm-csvcache-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using (var create = connection.CreateCommand())
            {
                create.CommandText = "create table csv_cache_row (id integer primary key, name text);";
                create.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = "insert into csv_cache_row (id, name) values ($id, $name);";
            insert.Parameters.Add(new SqliteParameter("$id", 1));
            insert.Parameters.Add(new SqliteParameter("$name", "alpha"));
            insert.ExecuteNonQuery();

            insert.Parameters["$id"].Value = 2;
            insert.Parameters["$name"].Value = "be,ta";
            insert.ExecuteNonQuery();
        }

        return new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
    }

    private static string Utf8(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    [Fact]
    public void WarmLookup_ShouldNotRecompile()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var options = new CsvStreamOptions();
            using var firstDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(firstDestination, options, CancellationToken.None);

            using var secondDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(secondDestination, options, CancellationToken.None);

            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1, "a warm hit must not recompile");
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);
            firstDestination.ToArray().Should().Equal(secondDestination.ToArray(), "warm output must be byte-identical");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Key_ShouldSeparateDelimiter_NullMarker_AndExcelMode()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var baseline = new CsvStreamOptions();
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, baseline, CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);

            var delimiterChanged = new CsvStreamOptions { Delimiter = ';' };
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, delimiterChanged, CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2, "a different delimiter must compile a fresh plan");

            var nullMarkerChanged = new CsvStreamOptions { NullMarker = "NULL" };
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, nullMarkerChanged, CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(3, "a different null marker must compile a fresh plan");

            var excelChanged = new CsvStreamOptions { ExcelMode = true };
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, excelChanged, CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(4, "a different excel mode must compile a fresh plan");

            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(4);
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Key_ShouldSeparateProjectionSignature()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);

            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().Select(r => new { r.Id }).OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2, "a different projection must compile a fresh plan");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ValueTransform_ShouldBypassLookupAndPublication()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var options = new CsvStreamOptions { ValueTransform = v => v?.ToString()?.ToUpperInvariant() };
            byte[] firstBytes;
            using (var d1 = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d1, options, CancellationToken.None);
                firstBytes = d1.ToArray();
            }
            using (var d2 = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d2, options, CancellationToken.None);

            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(0, "ValueTransform must not be published");
            Encoding.UTF8.GetString(firstBytes).Should().Contain("ALPHA", "the transform must be applied");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void GenerationBump_ShouldInvalidateAndNotReturnStale()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);

            DataContextCache.Clear();
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2, "a generation bump must recompile");
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);

            ctx.PurgeQueryCache();
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(0, "purge drops entries");
            using (var d = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(3);
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void StalePlanNegative_OptionsChangeContentDiffers()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var commaDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(commaDestination, new CsvStreamOptions { Delimiter = ',' }, CancellationToken.None);

            using var semicolonDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(semicolonDestination, new CsvStreamOptions { Delimiter = ';' }, CancellationToken.None);

            var comma = Utf8(commaDestination);
            var semicolon = Utf8(semicolonDestination);
            comma.Should().NotBe(semicolon, "a stale comma plan must not be reused for a semicolon request");
            comma.Should().Contain(",");
            semicolon.Should().Contain(";");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CsvPlanCacheKey_ShouldDifferByEveryDependency()
    {
        var columns = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" } };
        var schema = new FakeRecord(new[] { typeof(int) });

        var baseline = CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions());
        baseline.Should().NotBeNull();

        // delimiter
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions { Delimiter = ';' })
            .Should().NotBe(baseline, "delimiter is part of the key");
        // null marker
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions { NullMarker = "NULL" })
            .Should().NotBe(baseline, "null marker is part of the key");
        // excel mode
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions { ExcelMode = true })
            .Should().NotBe(baseline, "excel mode is part of the key");
        // include header
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions { IncludeHeader = false })
            .Should().NotBe(baseline, "header emission is part of the key");
        // sequential access
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, true, new CsvStreamOptions())
            .Should().NotBe(baseline, "sequential access is part of the key");
        // storage type
        var longSchema = new FakeRecord(new[] { typeof(long) });
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, longSchema, false, new CsvStreamOptions())
            .Should().NotBe(baseline, "reader storage type is part of the key");
        // context type
        CsvPlanCacheKey.Build(typeof(object), columns, schema, false, new CsvStreamOptions())
            .Should().NotBe(baseline, "context type is part of the key");
        // projection signature (property name)
        var renamed = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Other" } };
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), renamed, schema, false, new CsvStreamOptions())
            .Should().NotBe(baseline, "projection signature is part of the key");
        // ValueTransform bypasses
        CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions { ValueTransform = v => v?.ToString() })
            .Should().BeNull("ValueTransform must bypass the cache");
    }

    [Fact]
    public void CsvPlanCacheKey_ShouldBeStableForEquivalentOptions()
    {
        var columns = new[] { new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" } };
        var schema = new FakeRecord(new[] { typeof(int) });
        var a = CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions());
        var b = CsvPlanCacheKey.Build(typeof(SqliteDataContext), columns, schema, false, new CsvStreamOptions());
        a.Should().Be(b);
        a.Should().NotBeNull();
    }

    [Fact]
    public void CacheHit_ShouldBeByteIdenticalToColdRun_WithCrlfRfc4180()
    {
        var ctx = CreateDb(out var path);
        try
        {
            byte[] cold;
            using (var d = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
                cold = d.ToArray();
            }

            byte[] warm;
            using (var d = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
                warm = d.ToArray();
            }

            warm.Should().Equal(cold, "a cache hit must be byte-identical to the cold run");
            Encoding.UTF8.GetString(cold).Should().Contain("\r\n", "CSV output must use CRLF line endings (RFC 4180)");
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1, "the warm run must not recompile");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void CrossContext_ShouldNotShareEntriesOrCompilationCounter()
    {
        var ctx1 = CreateDb(out var path1);
        var ctx2 = CreateDb(out var path2);
        try
        {
            ctx1.TerminalPlanCacheForTesting.Should().NotBeSameAs(ctx2.TerminalPlanCacheForTesting,
                "each context owns its own terminal plan cache");

            using (var d = new MemoryStream()) ctx1.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx1.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);
            ctx1.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);

            // Second context must compile independently: no entry sharing, no shared counter.
            using (var d = new MemoryStream()) ctx2.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions(), CancellationToken.None);
            ctx2.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1, "context 2 must compile its own plan");
            ctx2.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);

            ctx1.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1, "context 1 counter must be untouched by context 2");
            ctx1.TerminalPlanCacheForTesting.EntryCount.Should().Be(1, "context 1 entries must not be shared with context 2");
        }
        finally
        {
            ctx1.Dispose();
            ctx2.Dispose();
            if (File.Exists(path1)) File.Delete(path1);
            if (File.Exists(path2)) File.Delete(path2);
        }
    }

    [Fact]
    public void OptionsChangeAndClear_ShouldProduceFreshPlanAndContent()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var commaDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(commaDestination, new CsvStreamOptions { Delimiter = ',' }, CancellationToken.None);
            var commaBytes = commaDestination.ToArray();
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);

            using var semicolonDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(semicolonDestination, new CsvStreamOptions { Delimiter = ';' }, CancellationToken.None);
            var semicolonBytes = semicolonDestination.ToArray();
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2, "a changed delimiter must compile a fresh plan");

            commaBytes.Should().NotEqual(semicolonBytes, "a stale plan must not be reused after an option change");
            Encoding.UTF8.GetString(commaBytes).Should().Contain(",");
            Encoding.UTF8.GetString(semicolonBytes).Should().Contain(";");

            // Clear drops entries and forces a recompile.
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(2);
            DataContextCache.Clear();
            using var afterClear = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(afterClear, new CsvStreamOptions { Delimiter = ',' }, CancellationToken.None);
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(3, "DataContextCache.Clear must invalidate and recompile");
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ValueTransform_ShouldBypassLookupAndPublication_CompileCounterUnchanged()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var options = new CsvStreamOptions { ValueTransform = v => v?.ToString()?.ToUpperInvariant() };
            byte[] firstBytes;
            using (var d1 = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d1, options, CancellationToken.None);
                firstBytes = d1.ToArray();
            }
            using (var d2 = new MemoryStream()) ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d2, options, CancellationToken.None);

            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(0, "ValueTransform must not be published");
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(0, "ValueTransform must bypass cache lookup and publication entirely");
            Encoding.UTF8.GetString(firstBytes).Should().Contain("ALPHA", "the transform must be applied");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Bound_ShouldNeverExceedCapacity()
    {
        var ctx = CreateDb(out var path);
        try
        {
            for (var i = 0; i < 70; i++)
            {
                var options = new CsvStreamOptions { NullMarker = $"N{i}" };
                using var d = new MemoryStream();
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, options, CancellationToken.None);
            }

            ctx.TerminalPlanCacheForTesting.EntryCount.Should().BeLessThanOrEqualTo(TerminalPlanCache.Capacity,
                "the combined terminal cache must stay within its bound");
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(70, "every distinct key must compile once");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void StorageTypeSeparation_EndToEnd_ShouldRecompileAndProduceCorrectOutput()
    {
        // End-to-end: two projections whose reader storage types differ (int column vs string column)
        // must not alias a cached plan; each must compile and emit its own correct bytes.
        var ctx = CreateDb(out var path);
        try
        {
            using var intDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(intDestination, new CsvStreamOptions(), CancellationToken.None);
            var intBytes = Encoding.UTF8.GetString(intDestination.ToArray());
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);

            using var stringDestination = new MemoryStream();
            ctx.From<CsvCacheRow>().Select(r => r.Name).WriteCsv(stringDestination, new CsvStreamOptions(), CancellationToken.None);
            var stringBytes = Encoding.UTF8.GetString(stringDestination.ToArray());
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2, "a differing projection/storage type must compile a fresh plan");

            intBytes.Should().Contain("Id");
            stringBytes.Should().Contain("Name");
            intBytes.Should().NotBe(stringBytes);
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void HeaderOption_ShouldNotAlias_AndProduceExactBytes()
    {
        var ctx = CreateDb(out var path);
        try
        {
            // First call with header enabled compiles a plan.
            byte[] withHeader;
            using (var d = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions { IncludeHeader = true }, CancellationToken.None);
                withHeader = d.ToArray();
            }
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(1);
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(1);

            // Second call differing only in the header option must NOT alias: it compiles a fresh plan.
            byte[] withoutHeader;
            using (var d = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions { IncludeHeader = false }, CancellationToken.None);
                withoutHeader = d.ToArray();
            }
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2,
                "differing only in the header option must not reuse a cached plan");
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().Be(2);

            // Exact byte-correct output for both (with header row).
            var expectedWithHeader = "Id,Name\r\n1,alpha\r\n2,\"be,ta\"\r\n";
            var expectedWithoutHeader = "1,alpha\r\n2,\"be,ta\"\r\n";
            Encoding.UTF8.GetString(withHeader).Should().Be(expectedWithHeader,
                "the header-enabled call must emit the header row and CRLF-terminated rows");
            Encoding.UTF8.GetString(withoutHeader).Should().Be(expectedWithoutHeader,
                "the header-disabled call must omit the header row and emit CRLF-terminated rows");

            // A repeat of the header-enabled call is a cache hit: no recompile, identical bytes.
            using (var d = new MemoryStream())
            {
                ctx.From<CsvCacheRow>().OrderBy(r => r.Id).WriteCsv(d, new CsvStreamOptions { IncludeHeader = true }, CancellationToken.None);
                d.ToArray().Should().Equal(withHeader, "a cache hit must be byte-identical to the cold run");
            }
            ctx.TerminalPlanCacheForTesting.CompilationCount.Should().Be(2, "the repeated call must be a cache hit");

            // The cache must not grow unbounded: entries stay within the combined capacity.
            ctx.TerminalPlanCacheForTesting.EntryCount.Should().BeLessThanOrEqualTo(TerminalPlanCache.Capacity,
                "the combined terminal cache must stay within its bound");
        }
        finally
        {
            ctx.Dispose();
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
