using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class CsvTypedScalarReaderTests : IDisposable
{
    private const string ExpectedCsv =
        "Id,BooleanValue,ByteValue,Int16Value,Int32Value,Int64Value,SingleValue,DoubleValue,DecimalValue,StringValue,GuidValue,DateTimeValue,NullableBooleanValue,NullableByteValue,NullableInt16Value,NullableInt32Value,NullableInt64Value,NullableSingleValue,NullableDoubleValue,NullableDecimalValue,NullableStringValue,NullableGuidValue,NullableDateTimeValue\r\n" +
        "1,true,2,3,4,5,1.5,2.5,12.5,plain,d3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e,2024-01-02T03:04:05.1234567,false,7,8,9,10,1.25,2.75,3.25,optional,f0e1d2c3-b4a5-4687-9abc-def012345678,2025-06-07T08:09:10.6543210\r\n" +
        "2,false,6,7,8,9,-1.25,-2.5,-12.5,second,11111111-2222-4333-8444-555555555555,2024-02-03T04:05:06.0000000,\\N,\\N,\\N,\\N,\\N,\\N,\\N,\\N,\\N,\\N,\\N\r\n";

    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public CsvTypedScalarReaderTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.csv-typed-scalars.{Environment.ProcessId}.{Guid.NewGuid():N}.db");
        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table csv_typed_scalar (
                id integer primary key,
                boolean_value integer not null,
                byte_value integer not null,
                int16_value integer not null,
                int32_value integer not null,
                int64_value integer not null,
                single_value real not null,
                double_value real not null,
                decimal_value numeric not null,
                string_value text not null,
                guid_value text not null,
                date_time_value text not null,
                nullable_boolean_value integer,
                nullable_byte_value integer,
                nullable_int16_value integer,
                nullable_int32_value integer,
                nullable_int64_value integer,
                nullable_single_value real,
                nullable_double_value real,
                nullable_decimal_value numeric,
                nullable_string_value text,
                nullable_guid_value text,
                nullable_date_time_value text
            );

            insert into csv_typed_scalar values
                (1, 1, 2, 3, 4, 5, 1.5, 2.5, 12.5, 'plain',
                 'd3b07384-d9a0-4b8f-8a1e-2f7a6b3c4d5e', '2024-01-02T03:04:05.1234567',
                 0, 7, 8, 9, 10, 1.25, 2.75, 3.25, 'optional',
                 'f0e1d2c3-b4a5-4687-9abc-def012345678', '2025-06-07T08:09:10.6543210'),
                (2, 0, 6, 7, 8, 9, -1.25, -2.5, -12.5, 'second',
                 '11111111-2222-4333-8444-555555555555', '2024-02-03T04:05:06.0000000',
                 null, null, null, null, null, null, null, null, null, null, null);
            """;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    [Fact]
    public void WriteCsv_ShouldUseTypedGettersForStoredScalarColumns()
    {
        using var destination = new MemoryStream();

        _db.From<CsvTypedScalarRow>()
            .OrderBy(row => row.Id)
            .WriteCsv(destination, null, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(ExpectedCsv);
    }

    [Fact]
    public async Task WriteCsvAsync_ShouldUseTypedGettersForStoredScalarColumns()
    {
        using var destination = new MemoryStream();

        await _db.From<CsvTypedScalarRow>()
            .OrderBy(row => row.Id)
            .WriteCsvAsync(destination, null, TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(destination.ToArray()).Should().Be(ExpectedCsv);
    }
}

[Table("csv_typed_scalar")]
public sealed class CsvTypedScalarRow
{
    [Column("id")]
    public long Id { get; set; }

    [Column("boolean_value")]
    public bool BooleanValue { get; set; }

    [Column("byte_value")]
    public byte ByteValue { get; set; }

    [Column("int16_value")]
    public short Int16Value { get; set; }

    [Column("int32_value")]
    public int Int32Value { get; set; }

    [Column("int64_value")]
    public long Int64Value { get; set; }

    [Column("single_value")]
    public float SingleValue { get; set; }

    [Column("double_value")]
    public double DoubleValue { get; set; }

    [Column("decimal_value")]
    public decimal DecimalValue { get; set; }

    [Column("string_value")]
    public string StringValue { get; set; } = string.Empty;

    [Column("guid_value")]
    public Guid GuidValue { get; set; }

    [Column("date_time_value")]
    public DateTime DateTimeValue { get; set; }

    [Column("nullable_boolean_value")]
    public bool? NullableBooleanValue { get; set; }

    [Column("nullable_byte_value")]
    public byte? NullableByteValue { get; set; }

    [Column("nullable_int16_value")]
    public short? NullableInt16Value { get; set; }

    [Column("nullable_int32_value")]
    public int? NullableInt32Value { get; set; }

    [Column("nullable_int64_value")]
    public long? NullableInt64Value { get; set; }

    [Column("nullable_single_value")]
    public float? NullableSingleValue { get; set; }

    [Column("nullable_double_value")]
    public double? NullableDoubleValue { get; set; }

    [Column("nullable_decimal_value")]
    public decimal? NullableDecimalValue { get; set; }

    [Column("nullable_string_value")]
    public string? NullableStringValue { get; set; }

    [Column("nullable_guid_value")]
    public Guid? NullableGuidValue { get; set; }

    [Column("nullable_date_time_value")]
    public DateTime? NullableDateTimeValue { get; set; }
}
