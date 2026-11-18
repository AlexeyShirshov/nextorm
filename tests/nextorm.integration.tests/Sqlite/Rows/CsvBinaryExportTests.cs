using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class CsvBinaryExportTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteCsv_StoredBinary_ShouldPreserveChunksPaddingAndNulls(bool asynchronous)
    {
        var db = _sut.DataProvider;
        var token = TestContext.Current.CancellationToken;
        byte[] payload = new byte[25_001];
        for (var i = 0; i < payload.Length; i++)
        {
            payload[i] = (byte)(248 + i);
        }

        using (db.ExecuteRaw("create temporary table csv_binary_export_probe (Id integer, Payload blob)"))
        {
        }
        try
        {
            using (db.ExecuteRaw(
                "insert into csv_binary_export_probe values (1, @payload), (2, X''), (3, null), (4, X'01'), (5, X'0102')",
                [new ProcedureParameter("payload", payload)]))
            {
            }

            var query = db.From<BinaryRow>().OrderBy(row => row.Id)
                .Select(row => new { row.Id, row.Payload });
            using var stream = new MemoryStream();
            var options = new CsvStreamOptions { ExcelMode = true };
            if (asynchronous)
            {
                await query.WriteCsvAsync(stream, options, token);
            }
            else
            {
                query.WriteCsv(stream, options, token);
            }

            var base64 = Convert.ToBase64String(payload);
            Encoding.UTF8.GetString(stream.ToArray()).Should().Be(
                $"Id,Payload\r\n1,'{base64}\r\n2,\r\n3,\\N\r\n4,AQ==\r\n5,AQI=\r\n");
        }
        finally
        {
            using (db.ExecuteRaw("drop table csv_binary_export_probe"))
            {
            }
        }
    }

    [Theory]
    [InlineData('+', "\\N", "1+\"'+A==\"\r\n2+\\N\r\n")]
    [InlineData(',', "'+A==", "1,\"'+A==\"\r\n2,'+A==\r\n")]
    [InlineData(',', "+A==", "1,\"+A==\"\r\n2,+A==\r\n")]
    public void WriteCsv_StoredBinary_ShouldQuoteDelimiterAndNullMarkerCollisions(
        char delimiter, string marker, string expected)
    {
        var db = _sut.DataProvider;
        using (db.ExecuteRaw("""
            create temporary table csv_binary_export_probe (Id integer, Payload blob);
            insert into csv_binary_export_probe values (1, X'F8'), (2, null);
            """))
        {
        }
        try
        {
            using var stream = new MemoryStream();
            var options = new CsvStreamOptions
            {
                Delimiter = delimiter,
                ExcelMode = true,
                IncludeHeader = false,
                NullMarker = marker
            };
            db.From<BinaryRow>().OrderBy(row => row.Id).Select(row => new { row.Id, row.Payload })
                .WriteCsv(stream, options, TestContext.Current.CancellationToken);

            Encoding.UTF8.GetString(stream.ToArray()).Should().Be(expected);
        }
        finally
        {
            using (db.ExecuteRaw("drop table csv_binary_export_probe"))
            {
            }
        }
    }

    [Fact]
    public void WriteCsv_WithTransform_ShouldEscapeBinaryValuesAndKeepSqlNullDistinct()
    {
        var db = _sut.DataProvider;
        using (db.ExecuteRaw("""
            create temporary table csv_binary_export_probe (Id integer, Payload blob);
            insert into csv_binary_export_probe values (1, X'F8'), (2, null), (3, X'01');
            """))
        {
        }
        try
        {
            var transformed = new List<byte>();
            var options = new CsvStreamOptions
            {
                Delimiter = '§',
                ExcelMode = true,
                NullMarker = "(null)",
                ValueTransform = value =>
                {
                    var bytes = (byte[])value!;
                    transformed.Add(bytes[0]);
                    return bytes[0] == 248 ? "+§\"x\"" : null;
                }
            };
            using var stream = new MemoryStream();
            db.From<BinaryRow>().OrderBy(row => row.Id).Select(row => row.Payload)
                .WriteCsv(stream, options, TestContext.Current.CancellationToken);

            Encoding.UTF8.GetString(stream.ToArray()).Should().Be(
                "Payload\r\n\"'+§\"\"x\"\"\"\r\n(null)\r\n(null)\r\n");
            transformed.Should().Equal((byte)248, (byte)1);
        }
        finally
        {
            using (db.ExecuteRaw("drop table csv_binary_export_probe"))
            {
            }
        }
    }

    [Table("csv_binary_export_probe")]
    public sealed class BinaryRow
    {
        public int Id { get; set; }
        public byte[]? Payload { get; set; }
    }
}
