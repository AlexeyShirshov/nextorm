using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

public sealed class RawScalarConversionTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public RawScalarConversionTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.raw-scalar.{Environment.ProcessId}.{Guid.NewGuid():N}.db");
        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder());
        ((IConnectionManager)_db).EnsureConnectionOpen();
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_databasePath))
            File.Delete(_databasePath);
    }

    [Fact]
    public void ExecuteRaw_Read_ShouldConvertNumericAndPrimitiveScalarValues()
    {
        AssertRead<sbyte>("select 42 as value", 42);
        AssertRead<byte>("select 42 as value", 42);
        AssertRead<short>("select 42 as value", 42);
        AssertRead<ushort>("select 42 as value", 42);
        AssertRead<int>("select 42 as value", 42);
        AssertRead<uint>("select 42 as value", 42);
        AssertRead<long>("select 42 as value", 42L);
        AssertRead<ulong>("select 42 as value", 42UL);
        AssertRead<float>("select 12.5 as value", 12.5f);
        AssertRead<double>("select 12.5 as value", 12.5);
        AssertRead<decimal>("select 12.5 as value", 12.5m);
        AssertRead<bool>("select 1 as value", true);
        AssertRead<bool>("select 0 as value", false);
        AssertRead<char>("select 'q' as value", 'q');
        AssertRead<string>("select 42 as value", "42");
        AssertRead<string>("select 'sqlite' as value", "sqlite");
    }

    [Fact]
    public async Task ExecuteRawAsync_ReadAsync_ShouldConvertNumericAndPrimitiveScalarValues()
    {
        await AssertReadAsync<sbyte>("select 42 as value", 42);
        await AssertReadAsync<byte>("select 42 as value", 42);
        await AssertReadAsync<short>("select 42 as value", 42);
        await AssertReadAsync<ushort>("select 42 as value", 42);
        await AssertReadAsync<int>("select 42 as value", 42);
        await AssertReadAsync<uint>("select 42 as value", 42);
        await AssertReadAsync<long>("select 42 as value", 42L);
        await AssertReadAsync<ulong>("select 42 as value", 42UL);
        await AssertReadAsync<float>("select 12.5 as value", 12.5f);
        await AssertReadAsync<double>("select 12.5 as value", 12.5);
        await AssertReadAsync<decimal>("select 12.5 as value", 12.5m);
        await AssertReadAsync<bool>("select 1 as value", true);
        await AssertReadAsync<bool>("select 0 as value", false);
        await AssertReadAsync<char>("select 'q' as value", 'q');
        await AssertReadAsync<string>("select 42 as value", "42");
        await AssertReadAsync<string>("select 'sqlite' as value", "sqlite");
    }

    [Fact]
    public void ExecuteRaw_Read_ShouldConvertDateGuidAndTimeScalarValues()
    {
        AssertRead<Guid>(
            "select 'd85b1407-351d-4694-9392-03acc5870eb1' as value",
            Guid.Parse("d85b1407-351d-4694-9392-03acc5870eb1"));
        AssertRead<DateOnly>("select '2024-02-29' as value", new DateOnly(2024, 2, 29));
        AssertRead<TimeOnly>("select '13:14:15.1234567' as value", new TimeOnly(13, 14, 15).Add(TimeSpan.FromTicks(1_234_567)));
        AssertRead<DateTime>(
            "select '2024-05-06T07:08:09' as value",
            new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified));
        AssertRead<DateTimeOffset>(
            "select '2024-05-06T07:08:09+00:00' as value",
            new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero));
        AssertRead<TimeSpan>("select '01:02:03.456' as value", new TimeSpan(1, 2, 3).Add(TimeSpan.FromMilliseconds(456)));
    }

    [Fact]
    public async Task ExecuteRawAsync_ReadAsync_ShouldConvertDateGuidAndTimeScalarValues()
    {
        await AssertReadAsync<Guid>(
            "select 'd85b1407-351d-4694-9392-03acc5870eb1' as value",
            Guid.Parse("d85b1407-351d-4694-9392-03acc5870eb1"));
        await AssertReadAsync<DateOnly>("select '2024-02-29' as value", new DateOnly(2024, 2, 29));
        await AssertReadAsync<TimeOnly>(
            "select '13:14:15.1234567' as value",
            new TimeOnly(13, 14, 15).Add(TimeSpan.FromTicks(1_234_567)));
        await AssertReadAsync<DateTime>(
            "select '2024-05-06T07:08:09' as value",
            new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Unspecified));
        await AssertReadAsync<DateTimeOffset>(
            "select '2024-05-06T07:08:09+00:00' as value",
            new DateTimeOffset(2024, 5, 6, 7, 8, 9, TimeSpan.Zero));
        await AssertReadAsync<TimeSpan>(
            "select '01:02:03.456' as value",
            new TimeSpan(1, 2, 3).Add(TimeSpan.FromMilliseconds(456)));
    }

    [Fact]
    public void ExecuteRaw_Read_ShouldConvertEnumsNullableValuesAndBytes()
    {
        AssertRead<RawScalarState>("select 'rEaDy' as value", RawScalarState.Ready);
        AssertRead<RawScalarState>("select 3 as value", RawScalarState.Complete);

        using (var nullable = _db.ExecuteRaw("select null as value union all select 17"))
            nullable.Read<int?>().Should().Equal(new int?[] { null, 17 });

        using var bytes = _db.ExecuteRaw("select X'0102FF' as value");
        bytes.Read<byte[]>().Should().ContainSingle().Which.Should().Equal(new byte[] { 1, 2, 255 });
    }

    [Fact]
    public async Task ExecuteRawAsync_ReadAsync_ShouldConvertEnumsNullableValuesAndBytes()
    {
        await AssertReadAsync<RawScalarState>("select 'rEaDy' as value", RawScalarState.Ready);
        await AssertReadAsync<RawScalarState>("select 3 as value", RawScalarState.Complete);

        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var nullable = await _db.ExecuteRawAsync("select null as value union all select 17", [], cancellationToken))
        {
            var values = new List<int?>();
            await foreach (var value in nullable.ReadAsync<int?>(cancellationToken))
                values.Add(value);

            values.Should().Equal(new int?[] { null, 17 });
        }

        await using var bytes = await _db.ExecuteRawAsync("select X'0102FF' as value", [], cancellationToken);
        var byteRows = new List<byte[]>();
        await foreach (var value in bytes.ReadAsync<byte[]>(cancellationToken))
            byteRows.Add(value);

        byteRows.Should().ContainSingle().Which.Should().Equal(new byte[] { 1, 2, 255 });
    }

    [Fact]
    public void ExecuteRaw_Read_ShouldSurfaceOverflowAndFormatErrorsForInvalidConversions()
    {
        Action overflow = () =>
        {
            using var result = _db.ExecuteRaw("select 256 as value");
            result.Read<byte>();
        };

        overflow.Should().Throw<OverflowException>();

        Action invalidNumber = () =>
        {
            using var result = _db.ExecuteRaw("select 'not-a-number' as value");
            result.Read<int>();
        };

        invalidNumber.Should().Throw<FormatException>();
    }

    [Fact]
    public async Task ExecuteRawAsync_ReadAsync_ShouldSurfaceOverflowAndFormatErrorsForInvalidConversions()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        Func<Task> overflow = async () =>
        {
            await using var result = await _db.ExecuteRawAsync("select 256 as value", [], cancellationToken);
            await foreach (var value in result.ReadAsync<byte>(cancellationToken))
                _ = value;
        };

        await overflow.Should().ThrowAsync<OverflowException>();

        Func<Task> invalidNumber = async () =>
        {
            await using var result = await _db.ExecuteRawAsync("select 'not-a-number' as value", [], cancellationToken);
            await foreach (var value in result.ReadAsync<int>(cancellationToken))
                _ = value;
        };

        await invalidNumber.Should().ThrowAsync<FormatException>();
    }

    private void AssertRead<T>(string sql, T expected)
    {
        using var result = _db.ExecuteRaw(sql);

        result.Read<T>().Should().Equal(new[] { expected });
    }

    private async Task AssertReadAsync<T>(string sql, T expected)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var result = await _db.ExecuteRawAsync(sql, [], cancellationToken);
        var values = new List<T>();

        await foreach (var value in result.ReadAsync<T>(cancellationToken))
            values.Add(value);

        values.Should().Equal(new[] { expected });
    }

    private enum RawScalarState
    {
        Pending = 1,
        Ready = 2,
        Complete = 3,
    }
}
