using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class SqliteRangeMaterializationTests : IDisposable
{
    private readonly SqliteTestProvider.IsolatedSqliteDatabase _database = SqliteTestProvider.CreateIsolatedDatabase();
    private readonly SqliteDataContext _db;

    public SqliteRangeMaterializationTests()
    {
        _db = new SqliteDataContext(string.Format(SqliteTestProvider.ConnectionString, _database.DatabasePath),
            new DataContextBuilder());
        using var setup = _db.ExecuteRaw("""
            create table range_materialization_probe (Id integer primary key, lower_bound integer, upper_bound integer);
            insert into range_materialization_probe values (1, 10, 20), (2, null, 10), (3, 20, null);
            """);
    }

    [Fact]
    public void RangePair_ShouldMaterializeBoundedAndUnboundedRecords()
    {
        var rows = _db.From<Reservation>().OrderBy(x => x.Id).ToList();

        rows.Select(x => x.Id).Should().Equal(1, 2, 3);
        rows[0].During.Lower.Should().Be(10);
        rows[0].During.Upper.Should().Be(20);
        rows[0].During.LowerInclusive.Should().BeFalse();
        rows[0].During.UpperInclusive.Should().BeTrue();
        rows[1].During.LowerInfinite.Should().BeTrue();
        rows[1].During.Upper.Should().Be(10);
        rows[2].During.Lower.Should().Be(20);
        rows[2].During.UpperInfinite.Should().BeTrue();
    }

    [Fact]
    public void RangePair_ShouldRoundTripAnUnboundedRecord()
    {
        var reservation = new Reservation(4, new Range<int>(
            -5, 0, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: true));
        _db.CreateInsertBuilder<Reservation>().Values(reservation).Insert();

        var row = _db.From<Reservation>().Where(x => x.Id == 4).Single();

        row.During.Lower.Should().Be(-5);
        row.During.LowerInclusive.Should().BeFalse();
        row.During.UpperInfinite.Should().BeTrue();
    }

    [Fact]
    public void RangePair_Contains_ShouldRebindThePointAtEachBoundary()
    {
        var point = 10;
        var query = _db.From<Reservation>()
            .Where(x => SqlFunctions.Postgres.range_contains(x.During, point))
            .OrderBy(x => x.Id);

        query.ToList().Select(x => x.Id).Should().Equal(2);

        point = 20;
        query.ToList().Select(x => x.Id).Should().Equal(1);

        point = 21;
        query.ToList().Select(x => x.Id).Should().Equal(3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteRaw_RangePairs_ShouldBindEachRangeByNameRegardlessOfColumnOrder(bool useAsync)
    {
        const string sql = """
            select upper_bound + 100 as fallback_upper, upper_bound as UPPER_BOUND,
                   Id, lower_bound + 100 as fallback_lower, lower_bound
            from range_materialization_probe order by Id
            """;

        var rows = await ReadRawRanges(sql, useAsync);

        rows.Select(row => row.Id).Should().Equal(1, 2, 3);
        rows[0].During.Lower.Should().Be(10);
        rows[0].During.Upper.Should().Be(20);
        rows[0].Fallback.Lower.Should().Be(110);
        rows[0].Fallback.Upper.Should().Be(120);
        rows[1].During.LowerInfinite.Should().BeTrue();
        rows[1].Fallback.LowerInfinite.Should().BeTrue();
        rows[1].Fallback.Upper.Should().Be(110);
        rows[2].During.UpperInfinite.Should().BeTrue();
        rows[2].Fallback.UpperInfinite.Should().BeTrue();
        rows[2].Fallback.Lower.Should().Be(120);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteRaw_IncompleteRange_ShouldLeaveThatPropertyUntouchedAndReadTheOther(bool useAsync)
    {
        const string sql = """
            select Id, lower_bound, upper_bound + 100 as fallback_upper,
                   lower_bound + 100 as fallback_lower
            from range_materialization_probe where Id = 1
            """;

        var rows = await ReadRawRanges(sql, useAsync);

        var row = rows.Should().ContainSingle().Which;
        row.Id.Should().Be(1);
        row.During.Lower.Should().Be(-2);
        row.During.Upper.Should().Be(-1);
        row.Fallback.Lower.Should().Be(110);
        row.Fallback.Upper.Should().Be(120);
    }

    [Fact]
    public void ExecuteRaw_OnlyOneRangeBound_ShouldRejectAnUnmappableResult()
    {
        using var result = _db.ExecuteRaw("select upper_bound from range_materialization_probe where Id = 1");

        var read = () => result.Read<RawReservation>();

        read.Should().Throw<InvalidOperationException>()
            .WithMessage("*None of the result-set columns (upper_bound) matches a mapped property*");
    }

    private async Task<IReadOnlyList<RawReservation>> ReadRawRanges(string sql, bool useAsync)
    {
        if (!useAsync)
        {
            using var result = _db.ExecuteRaw(sql);
            return result.Read<RawReservation>();
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        await using var asyncResult = await _db.ExecuteRawAsync(sql, [], cancellationToken);
        var rows = new List<RawReservation>();
        await foreach (var row in asyncResult.ReadAsync<RawReservation>(cancellationToken))
            rows.Add(row);

        return rows;
    }

    public void Dispose()
    {
        _db.Dispose();
        _database.Dispose();
    }

    [SqlTable("range_materialization_probe")]
    public sealed record Reservation(
        int Id,
        [property: RangeColumns("lower_bound", "upper_bound", LowerInclusive = false, UpperInclusive = true)]
        Range<int> During);

    [SqlTable("range_materialization_probe")]
    public sealed class RawReservation
    {
        [Column("reservation_id")]
        public int Id { get; set; }

        [RangeColumns("lower_bound", "upper_bound")]
        public Range<int> During { get; set; } = new(
            -2, -1, lowerInclusive: true, upperInclusive: false, lowerInfinite: false, upperInfinite: false);

        [RangeColumns("fallback_lower", "fallback_upper")]
        public Range<int> Fallback { get; set; }
    }
}
