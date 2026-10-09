using System.Buffers.Text;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// End-to-end coverage for the <c>WriteCsv</c>/<c>WriteCsvAsync</c> terminals against a real SQLite
/// database: header, escaping, empty results, custom delimiter, parameters, async, cancellation and
/// the box-free streaming guarantee (no <c>TResult</c> per row).
/// </summary>
public class CsvStreamTests
{
    [SqlTable("csv_row")]
    private sealed class CsvRow
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("note")]
        public string? Note { get; set; }
    }

    /// <summary>A projection whose constructor increments a counter; it proves the terminal never materializes a row.</summary>
    private sealed class CountingRow
    {
        public static int Constructions;

        public CountingRow(int id, string? name)
        {
            Constructions++;
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string? Name { get; }
    }

    /// <summary>A row whose column is converted only through the object-based bridge.</summary>
    [SqlTable("csv_bridge_probe")]
    private sealed class CsvBridgeRow
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("value")]
        [ValueConverter(typeof(BridgeIntConverter))]
        public int Value { get; set; }
    }

    /// <summary>An <see cref="IPropertyValueConverter"/> implemented directly, so the mapper can only
    /// invoke it through the object-based <c>ConvertFromProvider(object)</c> bridge.</summary>
    private sealed class BridgeIntConverter : IPropertyValueConverter
    {
        public Type ProviderType => typeof(int);

        public bool ConvertsNulls => false;

        public object? ConvertToProvider(object? model) => model;

        public object? ConvertFromProvider(object? provider) => (int)(provider ?? 0);
    }

    /// <summary>A row whose property type has no CSV formatter (the integration-suite regression shape).</summary>
    [SqlTable("csv_unsupported_probe")]
    private sealed class CsvUnsupportedRow
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("state")]
        [ValueConverter(typeof(EnumToStringConverter<CsvProbeState>))]
        public CsvProbeState State { get; set; }
    }

    private enum CsvProbeState
    {
        Unknown,
        Active,
    }

    private static SqliteDataContext CreateDb(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"nextorm-csv-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using (var create = connection.CreateCommand())
            {
                create.CommandText = "create table csv_row (id integer primary key, name text, note text);";
                create.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = "insert into csv_row (id, name, note) values ($id, $name, $note);";
            insert.Parameters.Add(new SqliteParameter("$id", 1));
            insert.Parameters.Add(new SqliteParameter("$name", "alpha"));
            insert.Parameters.Add(new SqliteParameter("$note", "plain"));
            insert.ExecuteNonQuery();

            insert.Parameters["$id"].Value = 2;
            insert.Parameters["$name"].Value = "be,ta";
            insert.Parameters["$note"].Value = "quote\"inside";
            insert.ExecuteNonQuery();

            insert.Parameters["$id"].Value = 3;
            insert.Parameters["$name"].Value = DBNull.Value;
            insert.Parameters["$note"].Value = "line\nbreak";
            insert.ExecuteNonQuery();
        }

        return new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
    }

    private static string Utf8(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    [Fact]
    public void WriteCsv_RendersHeaderAndEscapedRows()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().OrderBy(r => r.Id).WriteCsv(destination, null, TestContext.Current.CancellationToken);

            Utf8(destination).Should().Be(
                "Id,Name,Note\r\n" +
                "1,alpha,plain\r\n" +
                "2,\"be,ta\",\"quote\"\"inside\"\r\n" +
                "3,\\N,\"line\nbreak\"\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteCsvAsync_MatchesSyncOutput()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var sync = new MemoryStream();
            using var async = new MemoryStream();
            var query = ctx.From<CsvRow>().OrderBy(r => r.Id);

            query.WriteCsv(sync, null, TestContext.Current.CancellationToken);
            await query.WriteCsvAsync(async, null, TestContext.Current.CancellationToken);

            Utf8(async).Should().Be(Utf8(sync));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_EmptyResult_WritesHeaderOnly()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().Where(r => r.Id > 100).WriteCsv(destination, null, TestContext.Current.CancellationToken);

            Utf8(destination).Should().Be("Id,Name,Note\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_IncludeHeaderFalse_WritesRowsOnly()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().Where(r => r.Id == 1)
                .WriteCsv(destination, new CsvStreamOptions { IncludeHeader = false }, TestContext.Current.CancellationToken);

            Utf8(destination).Should().Be("1,alpha,plain\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_CustomDelimiter_QuotesThatDelimiterOnly()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().Where(r => r.Id == 2)
                .WriteCsv(destination, new CsvStreamOptions { Delimiter = ';' }, TestContext.Current.CancellationToken);

            Utf8(destination).Should().Be("Id;Name;Note\r\n2;be,ta;\"quote\"\"inside\"\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteCsv_WithParameter_BindsPositionally()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            var query = ctx.From<CsvRow>()
                .Where(r => r.Id > SqlFunctions.Parameter<int>(0))
                .OrderBy(r => r.Id);

            await query.WriteCsvAsync(destination, null, TestContext.Current.CancellationToken, 1);

            Utf8(destination).Should().Be(
                "Id,Name,Note\r\n" +
                "2,\"be,ta\",\"quote\"\"inside\"\r\n" +
                "3,\\N,\"line\nbreak\"\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_DoesNotConstructProjectionType()
    {
        var ctx = CreateDb(out var path);
        try
        {
            CountingRow.Constructions = 0;
            using var destination = new MemoryStream();

            ctx.From<CsvRow>().OrderBy(r => r.Id)
                .Select(r => new CountingRow(r.Id, r.Name))
                .WriteCsv(destination, null, TestContext.Current.CancellationToken);

            CountingRow.Constructions.Should().Be(0, "the CSV terminal must not materialize TResult");
            Utf8(destination).Should().Contain("1,alpha");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_LeavesDestinationOpen()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var destination = new TrackingStream();
            ctx.From<CsvRow>().OrderBy(r => r.Id).WriteCsv(destination, null, TestContext.Current.CancellationToken);

            destination.WasDisposed.Should().BeFalse();
            destination.CanRead.Should().BeTrue();
            Utf8(destination).Should().Contain("1,alpha");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_CancelledMidStream_ThrowsAndLeavesPartialOutput()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var cts = new CancellationTokenSource();
            var destination = new CancellingStream(cts, cancelAfterWrites: 2);

            var act = () => ctx.From<CsvRow>().OrderBy(r => r.Id).WriteCsv(destination, null, cts.Token);

            act.Should().Throw<OperationCanceledException>();
            Utf8(destination).Should().Be("Id,Name,Note\r\n1,alpha,plain\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteCsvAsync_CancelledMidStream_Throws()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var cts = new CancellationTokenSource();
            var destination = new CancellingStream(cts, cancelAfterWrites: 2);

            var act = async () => await ctx.From<CsvRow>().OrderBy(r => r.Id).WriteCsvAsync(destination, null, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_RepeatedOnSameCommand_KeepsCacheAndWritesTwice()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var query = ctx.From<CsvRow>().OrderBy(r => r.Id).ToParentCommand();
            var cacheBefore = query.Cache;
            cacheBefore.Should().BeTrue("a new command caches by default");

            using var first = new MemoryStream();
            query.WriteCsv(first, null, TestContext.Current.CancellationToken);
            query.Cache.Should().BeTrue("the CSV terminal must not mutate the shared command");
            query.Cache.Should().Be(cacheBefore);

            using var second = new MemoryStream();
            query.WriteCsv(second, null, TestContext.Current.CancellationToken);

            query.Cache.Should().Be(cacheBefore);
            Utf8(second).Should().Be(Utf8(first));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_DestinationThrows_OriginalExceptionSurfacesAndStreamStaysOpen()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var destination = new ThrowingStream();

            var act = () => ctx.From<CsvRow>().OrderBy(r => r.Id)
                .WriteCsv(destination, null, TestContext.Current.CancellationToken);

            act.Should().ThrowExactly<ThrowingStream.WriteFailure>();
            destination.DisposeCount.Should().Be(0, "the terminal must not dispose a caller-owned stream");
            destination.CanRead.Should().BeTrue();

            // The reader and its command were released, so the same context still runs queries.
            using var after = new MemoryStream();
            ctx.From<CsvRow>().OrderBy(r => r.Id).WriteCsv(after, null, TestContext.Current.CancellationToken);
            Utf8(after).Should().Contain("1,alpha");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task WriteCsvAsync_DestinationThrows_OriginalExceptionSurfaces()
    {
        var ctx = CreateDb(out var path);
        try
        {
            var destination = new ThrowingStream();

            var act = async () => await ctx.From<CsvRow>().OrderBy(r => r.Id)
                .WriteCsvAsync(destination, null, TestContext.Current.CancellationToken);

            await act.Should().ThrowExactlyAsync<ThrowingStream.WriteFailure>();
            destination.DisposeCount.Should().Be(0, "the terminal must not dispose a caller-owned stream");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_TempTableSource_ThrowsBeforeHeaderInsteadOfRawProviderError()
    {
        using var ctx = SqliteTestContext.Create();
        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();
        using var destination = new MemoryStream();

        var act = () => ctx.From(source)
            .Select(t => new { Id = t.GetInt32("id") })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        // The CSV terminal keeps its own wording even though the shared guard is now parameterised.
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*temporary table*")
            .WithMessage("*WriteCsv/WriteCsvAsync*")
            .WithMessage("*CSV*");
        destination.Length.Should().Be(0, "the guard must run before the header");
    }

    [Fact]
    public async Task WriteCsvAsync_TempTableSource_ThrowsBeforeHeader()
    {
        using var ctx = SqliteTestContext.Create();
        var source = ctx.From<ISimpleEntity>().Select(x => new { x.Id }).AsTempTable();
        using var destination = new MemoryStream();

        var act = async () => await ctx.From(source)
            .Select(t => new { Id = t.GetInt32("id") })
            .WriteCsvAsync(destination, null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("*temporary table*")
            .WithMessage("*WriteCsv/WriteCsvAsync*")
            .WithMessage("*CSV*");
        destination.Length.Should().Be(0, "the guard must run before the header");
    }

    [Fact]
    public void WriteCsv_ObjectBridgedConverter_ThrowsBeforeExecution()
    {
        // The table does not exist; the pre-execution guard must reject the object-bridged converter
        // column before any SQL runs, so no provider missing-table error surfaces and no byte is written.
        using var ctx = SqliteTestContext.Create();
        using var destination = new MemoryStream();

        var act = () => ctx.From<CsvBridgeRow>()
            .Select(x => new { x.Value })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Value*")
            .WithMessage("*ConvertFromProvider*");
        destination.Length.Should().Be(0, "the guard must reject the projection before any SQL runs");
    }

    [Fact]
    public async Task WriteCsvAsync_ObjectBridgedConverter_ThrowsBeforeExecution()
    {
        using var ctx = SqliteTestContext.Create();
        using var destination = new MemoryStream();

        var act = async () => await ctx.From<CsvBridgeRow>()
            .Select(x => new { x.Value })
            .WriteCsvAsync(destination, null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*Value*");
        destination.Length.Should().Be(0, "the guard must reject the projection before any SQL runs");
    }

    [Fact]
    public void WriteCsv_UnsupportedProjectionColumn_ThrowsBeforeExecution()
    {
        // Mirrors the integration regression: an enum column with a typed converter has no CSV
        // formatter. On a missing table the guard must win over the provider's missing-table error.
        using var ctx = SqliteTestContext.Create();
        using var destination = new MemoryStream();

        var act = () => ctx.From<CsvUnsupportedRow>()
            .Select(x => new { x.State })
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        act.Should().Throw<NotSupportedException>().WithMessage("*State*");
        destination.Length.Should().Be(0, "the guard must reject the projection before any SQL runs");
    }

    [Fact]
    public async Task WriteCsvAsync_UnsupportedProjectionColumn_ThrowsBeforeExecution()
    {
        using var ctx = SqliteTestContext.Create();
        using var destination = new MemoryStream();

        var act = async () => await ctx.From<CsvUnsupportedRow>()
            .Select(x => new { x.State })
            .WriteCsvAsync(destination, null, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*State*");
        destination.Length.Should().Be(0, "the guard must reject the projection before any SQL runs");
    }

    [Fact]
    public void WriteCsv_CustomNullMarker_ReplacesDbNull()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().Where(r => r.Id == 3)
                .Select(r => new { r.Name })
                .WriteCsv(destination, new CsvStreamOptions { NullMarker = "(null)" }, TestContext.Current.CancellationToken);

            Utf8(destination).Should().Be("Name\r\n(null)\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_DefaultNullMarker_QuotesADataLiteralMarker()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().Where(r => r.Id == 1)
                .Select(r => new { Text = SqlFunctions.Parameter<string>(0) })
                .WriteCsv(destination, null, TestContext.Current.CancellationToken, "\\N");

            Utf8(destination).Should().Be("Text\r\n\"\\N\"\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_ExcelMode_GuardsAFormulaPrefix()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            ctx.From<CsvRow>().Where(r => r.Id == 1)
                .Select(r => new { Text = SqlFunctions.Parameter<string>(0) })
                .WriteCsv(destination, new CsvStreamOptions { ExcelMode = true }, TestContext.Current.CancellationToken, "=cmd");

            Utf8(destination).Should().Be("Text\r\n'=cmd\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void WriteCsv_ValueTransform_RunsBeforeExcelGuard()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new MemoryStream();
            var options = new CsvStreamOptions
            {
                ExcelMode = true,
                ValueTransform = value => "=" + value,
            };

            ctx.From<CsvRow>().Where(r => r.Id == 1)
                .Select(r => new { r.Name })
                .WriteCsv(destination, options, TestContext.Current.CancellationToken);

            Utf8(destination).Should().Be("Name\r\n'=alpha\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // --- D167 / issue #167: real-SQLite proof of the bounded chunked binary path and of the CSV-only
    // locator suppression. The buffered path would write the whole Base64 row in one destination write;
    // the chunked path streams bounded chunks, so the recorded maximum write discriminates the paths. ---

    /// <summary>A table with a real BLOB column large enough to cross several 12 KiB binary chunks.</summary>
    [SqlTable("csv_blob_row")]
    private sealed class CsvBlobRow
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("data")]
        public byte[]? Data { get; set; }
    }

    private const int BlobSize = (1024 * 1024) + 1;

    private static byte[] CreateBlob()
    {
        var payload = new byte[BlobSize];
        for (var i = 0; i < payload.Length; i++)
            payload[i] = (byte)((i * 31 + 7) & 0xFF);

        return payload;
    }

    private static SqliteDataContext CreateBlobDb(out string path, byte[] payload)
    {
        path = Path.Combine(Path.GetTempPath(), $"nextorm-csvblob-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using (var create = connection.CreateCommand())
            {
                create.CommandText = "create table csv_blob_row (id integer primary key, data blob);";
                create.ExecuteNonQuery();
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = "insert into csv_blob_row (id, data) values ($id, $data);";
            insert.Parameters.Add(new SqliteParameter("$id", 1));
            insert.Parameters.Add(new SqliteParameter("$data", payload));
            insert.ExecuteNonQuery();
        }

        return new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
    }

    /// <summary>
    /// Exports a real 1 MiB+1 SQLite BLOB through the public CSV terminal on both surfaces and asserts
    /// byte-identical Base64 plus a bounded destination write, which only the chunked GetBytes path can
    /// produce (a buffered path writes the whole Base64 row in a single write).
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteCsv_RealBlob_StreamsBoundedBase64_ByteIdentical(bool useAsync)
    {
        var payload = CreateBlob();
        var ctx = CreateBlobDb(out var path, payload);
        try
        {
            using var destination = new BoundedStream();
            var query = ctx.From<CsvBlobRow>().OrderBy(r => r.Id);

            if (useAsync)
                await query.WriteCsvAsync(destination, null, TestContext.Current.CancellationToken);
            else
                query.WriteCsv(destination, null, TestContext.Current.CancellationToken);

            var expected = "Id,Data\r\n1," + Convert.ToBase64String(payload) + "\r\n";
            Encoding.UTF8.GetString(destination.ToArray()).Should().Be(expected);

            var chunkLimit = Base64.GetMaxEncodedToUtf8Length(CsvBinaryFieldWriter.BinaryCapacity);
            destination.MaxWriteLength.Should().BeLessThanOrEqualTo(
                chunkLimit,
                "a real 1 MiB BLOB must be streamed through the bounded chunked path, not buffered into one whole Base64 row");
            ((long)destination.MaxWriteLength).Should().BeLessThan(
                expected.Length / 2,
                "no single destination write may approach the whole Base64 field");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// REQ-06: the two public CSV terminals — <see cref="EntityBuilder{TEntity}"/> and
    /// <see cref="QueryCommand{TResult}"/> — accept the same direct-stored BLOB projection with
    /// default and custom options, sync and async, and must produce byte-identical CSV. Each call also
    /// passes a positional WHERE value, exercising the <c>params</c> overload of both surfaces; the
    /// EntityBuilder overloads forward to the command terminal.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteCsv_EntityBuilderAndQueryCommandSurfaces_SyncAsync_DefaultAndCustomOptions_ByteIdentical(bool useAsync)
    {
        var payload = CreateBlob();
        var ctx = CreateBlobDb(out var path, payload);
        try
        {
            foreach (var options in new CsvStreamOptions?[] { null, new CsvStreamOptions { Delimiter = ';' } })
            {
                var builderCsv = await RenderAsync(options, commandSurface: false);
                var commandCsv = await RenderAsync(options, commandSurface: true);

                var expected = (options is null ? "Id,Data\r\n1," : "Id;Data\r\n1;")
                    + Convert.ToBase64String(payload) + "\r\n";
                Encoding.UTF8.GetString(builderCsv).Should().Be(expected);
                Encoding.UTF8.GetString(commandCsv).Should().Be(
                    expected, "the QueryCommand and EntityBuilder surfaces must be byte-identical");
            }

            async Task<byte[]> RenderAsync(CsvStreamOptions? options, bool commandSurface)
            {
                using var destination = new BoundedStream();
                var builder = ctx.From<CsvBlobRow>()
                    .Where(r => r.Id == SqlFunctions.Parameter<int>(0))
                    .OrderBy(r => r.Id);

                if (commandSurface)
                {
                    var command = builder.ToParentCommand();
                    if (useAsync)
                        await command.WriteCsvAsync(destination, options, TestContext.Current.CancellationToken, 1);
                    else
                        command.WriteCsv(destination, options, TestContext.Current.CancellationToken, 1);
                }
                else if (useAsync)
                {
                    await builder.WriteCsvAsync(destination, options, TestContext.Current.CancellationToken, 1);
                }
                else
                {
                    builder.WriteCsv(destination, options, TestContext.Current.CancellationToken, 1);
                }

                return destination.ToArray();
            }
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// Regression for the D3b P1 defect on real SQLite: projecting a bound parameter
    /// <c>SqlFunctions.Parameter&lt;byte[]&gt;</c> must not be admitted to the bounded <c>GetBytes</c>
    /// path (that call aborts the SQLite provider natively). It stays buffered and writes the exact
    /// Base64 bytes.
    /// </summary>
    [Fact]
    public void WriteCsv_BoundParameterBlob_DoesNotCrashAndWritesBase64()
    {
        var ctx = CreateDb(out var path);
        try
        {
            using var destination = new BoundedStream();
            var payload = new byte[] { 1, 2, 3, 4 };

            ctx.From<CsvRow>().Where(r => r.Id == 1)
                .Select(r => new { Blob = SqlFunctions.Parameter<byte[]>(0) })
                .WriteCsv(destination, null, TestContext.Current.CancellationToken, payload);

            Encoding.UTF8.GetString(destination.ToArray()).Should().Be("Blob\r\nAQIDBA==\r\n");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// Proves the CSV-local locator suppression at the SQL layer: the CSV preparation renders the SQLite
    /// projection without the trailing <c>rowid</c>, while the sibling sequential LOB route (the one
    /// <c>ToStream</c>/<c>ToTextReader</c> use) still appends it.
    /// </summary>
    [Fact]
    public void CsvBinary_SqliteCsvOmitsLocator_ButSequentialLobKeepsIt()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<CsvBlobRow>().Select(r => r.Data!);
        var planner = CreatePlanner(ctx, SqliteDialect.Instance);

        var csv = (DbPreparedQueryCommand<byte[]>)planner.GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            sequentialAccess: true,
            streamingRowsRequested: false,
            suppressLobLocator: true,
            TestContext.Current.CancellationToken);
        NormalizeCommand(csv).Should().Be("select data from csv_blob_row");
        NormalizeCommand(csv).Should().NotContain("rowid");

        var lob = (DbPreparedQueryCommand<byte[]>)planner.GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            sequentialAccess: true,
            streamingRowsRequested: false,
            suppressLobLocator: false,
            TestContext.Current.CancellationToken);
        NormalizeCommand(lob).Should().Be("select data, rowid from csv_blob_row");
    }

    /// <summary>
    /// REQ-10: on SQLite the CSV preparation requests a sequential reader, and the CSV-only locator
    /// suppression must keep the generated SQL identical to the pre-D167 CSV render (sequential access
    /// off). A DISTINCT, JOIN, aggregate or plain projection must not gain the trailing <c>rowid</c>,
    /// which would change the SQL semantics (an aggregate would even become invalid). The plain binary
    /// projection and its LOB sibling locator behavior are asserted by the neighbouring test.
    /// </summary>
    [Fact]
    public void CsvLocatorSuppression_KeepsDistinctJoinAggregateAndPlainSqlUnchanged()
    {
        using var ctx = SqliteTestContext.Create();
        var planner = CreatePlanner(ctx, SqliteDialect.Instance);

        AssertCsvSqlMatchesPreChange(planner, ctx.From<CsvBlobRow>().Select(r => r.Id));
        AssertCsvSqlMatchesPreChange(planner, ctx.From<CsvBlobRow>().Distinct().Select(r => r.Id));
        AssertCsvSqlMatchesPreChange(planner, ctx.From<CsvBlobRow>()
            .Join(ctx.From<CsvBlobRow>(), (a, b) => a.Id == b.Id)
            .Select(p => p.Item1.Id));
        AssertCsvSqlMatchesPreChange(planner, ctx.From<CsvBlobRow>().Select(r => SqlFunctions.Sql.count()));
    }

    private static void AssertCsvSqlMatchesPreChange<TResult>(QueryPlanner planner, QueryCommand<TResult> command)
    {
        // The pre-D167 CSV behavior: no sequential reader, so no locator is ever appended.
        var preChange = (DbPreparedQueryCommand<TResult>)planner.GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            sequentialAccess: false,
            streamingRowsRequested: false,
            suppressLobLocator: false,
            TestContext.Current.CancellationToken);

        // The current CSV path: sequential reader with the locator explicitly suppressed.
        var csv = (DbPreparedQueryCommand<TResult>)planner.GetPreparedQueryCommand(
            command,
            createEnumerator: false,
            storeInCache: false,
            sequentialAccess: true,
            streamingRowsRequested: false,
            suppressLobLocator: true,
            TestContext.Current.CancellationToken);

        var csvSql = NormalizeCommand(csv);
        csvSql.Should().NotContain("rowid", "the CSV path must not gain the SQLite locator");
        csvSql.Should().Be(
            NormalizeCommand(preChange),
            "locator suppression must keep the CSV SQL identical to the pre-change sequential-off render");
    }

    private static string NormalizeCommand<TResult>(DbPreparedQueryCommand<TResult> command)
        => command.DbCommand.CommandText.Replace("\r\n", "\n");

    private static QueryPlanner CreatePlanner(IDataContext ctx, ISqlDialect dialect)
        => new(
            ctx,
            () => dialect,
            ctx.GetType(),
            new ProviderHooks(
                (column, param) => param,
                (_, name, value) => new SqliteParameter(name, value ?? DBNull.Value),
                sql => new SqliteCommand(sql)),
            new LoggingOptions(null),
            new InterceptorHooks([], []));

    /// <summary>A <see cref="MemoryStream"/> that records the largest single write, exposing bounded streaming.</summary>
    private sealed class BoundedStream : MemoryStream
    {
        public int MaxWriteLength { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Record(count);
            base.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            Record(buffer.Length);
            base.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Record(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Record(buffer.Length);
            return base.WriteAsync(buffer, cancellationToken);
        }

        private void Record(int count)
        {
            if (count > MaxWriteLength)
                MaxWriteLength = count;
        }
    }

    /// <summary>Throws on every write and records whether the terminal tried to dispose it.</summary>
    private sealed class ThrowingStream : MemoryStream
    {
        public int DisposeCount { get; private set; }

        public override void Write(ReadOnlySpan<byte> buffer) => throw new WriteFailure();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new WriteFailure();

        protected override void Dispose(bool disposing)
        {
            DisposeCount++;
            base.Dispose(disposing);
        }

        internal sealed class WriteFailure : Exception
        {
        }
    }

    /// <summary>Reveals whether the terminal disposed a destination it was handed.</summary>
    private sealed class TrackingStream : MemoryStream
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }

    /// <summary>Cancels its token source once the configured number of buffer writes has completed, so the next loop iteration observes cancellation.</summary>
    private sealed class CancellingStream(CancellationTokenSource source, int cancelAfterWrites) : MemoryStream
    {
        private int _writes;

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            base.Write(buffer);
            CountWrite();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var result = base.WriteAsync(buffer, cancellationToken);
            CountWrite();
            return result;
        }

        private void CountWrite()
        {
            if (Interlocked.Increment(ref _writes) == cancelAfterWrites)
                source.Cancel();
        }
    }
}
