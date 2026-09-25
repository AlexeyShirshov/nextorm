using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Round-trip tests for <see cref="TimeSpan"/> columns on a provider without a native duration type
/// (SQLite). The value is stored in an integer column in the unit declared by
/// <see cref="DurationAttribute"/> (ticks by default); reading, writing, nullability and comparison
/// all go through the same conversion.
/// </summary>
public class DurationColumnTests
{
    [SqlTable("duration_entity")]
    public interface IDurationEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("ticks")]
        TimeSpan Ticks { get; set; }

        [Column("seconds")]
        [Duration(DurationUnit.Seconds)]
        TimeSpan Seconds { get; set; }

        [Column("nullable_ms")]
        [Duration(DurationUnit.Milliseconds)]
        TimeSpan? Nullable { get; set; }
    }

    public class DurationEntity : IDurationEntity
    {
        public int Id { get; set; }
        public TimeSpan Ticks { get; set; }
        public TimeSpan Seconds { get; set; }
        public TimeSpan? Nullable { get; set; }
    }

    private static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using var setup = conn.CreateCommand();
        setup.CommandText =
            "create table duration_entity (id integer primary key, ticks bigint, seconds bigint, nullable_ms bigint);";
        setup.ExecuteNonQuery();
        return conn;
    }

    private static SqliteDataContext ContextFor(SqliteConnection connection)
        => new(connection, new DataContextBuilder());

    private static long RawLong(SqliteConnection connection, string column)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"select {column} from duration_entity where id = 1";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    [Fact]
    public void DurationColumn_ShouldRoundTripInDeclaredUnit()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        var value = new TimeSpan(0, 0, 90);
        ctx.InsertInto<IDurationEntity>()
            .Values(new DurationEntity { Id = 1, Ticks = value, Seconds = value, Nullable = TimeSpan.FromMilliseconds(1500) })
            .Insert();

        // The stored integers are the value expressed in the declared unit, not always ticks.
        RawLong(conn, "ticks").Should().Be(value.Ticks);
        RawLong(conn, "seconds").Should().Be(90);
        RawLong(conn, "nullable_ms").Should().Be(1500);

        var row = ctx.From<DurationEntity>().ToList().Single();
        row.Ticks.Should().Be(value);
        row.Seconds.Should().Be(value);
        row.Nullable.Should().Be(TimeSpan.FromMilliseconds(1500));
    }

    [Fact]
    public void DurationColumn_ShouldRoundTripNull()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IDurationEntity>()
            .Values(new DurationEntity { Id = 1, Ticks = TimeSpan.Zero, Seconds = TimeSpan.Zero, Nullable = null })
            .Insert();

        using var raw = conn.CreateCommand();
        raw.CommandText = "select nullable_ms from duration_entity where id = 1";
        raw.ExecuteScalar().Should().Be(DBNull.Value);

        ctx.From<DurationEntity>().ToList().Single().Nullable.Should().BeNull();
    }

    [Fact]
    public void DurationColumn_ShouldCompareAgainstConstant()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IDurationEntity>()
            .Values([
                new DurationEntity { Id = 1, Ticks = TimeSpan.FromSeconds(30), Seconds = TimeSpan.FromSeconds(30) },
                new DurationEntity { Id = 2, Ticks = TimeSpan.FromSeconds(120), Seconds = TimeSpan.FromSeconds(120) },
            ])
            .Insert();

        ctx.From<IDurationEntity>().Where(x => x.Seconds > TimeSpan.FromSeconds(60)).Select(x => x.Id).ToList()
            .Should().Equal(2);

        // The constant may appear on either side of the comparison.
        ctx.From<IDurationEntity>().Where(x => TimeSpan.FromSeconds(60) < x.Seconds).Select(x => x.Id).ToList()
            .Should().Equal(2);
    }

    [Fact]
    public void DurationColumn_ShouldCompareAgainstCapturedVariable()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IDurationEntity>()
            .Values([
                new DurationEntity { Id = 1, Ticks = TimeSpan.FromSeconds(30), Seconds = TimeSpan.FromSeconds(30) },
                new DurationEntity { Id = 2, Ticks = TimeSpan.FromSeconds(120), Seconds = TimeSpan.FromSeconds(120) },
            ])
            .Insert();

        var threshold = TimeSpan.FromSeconds(60);
        ctx.From<IDurationEntity>().Where(x => x.Seconds >= threshold).Select(x => x.Id).ToList()
            .Should().Equal(2);
    }

    [Fact]
    public void DurationColumn_ShouldRoundTripNegativeValue()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        var value = TimeSpan.FromSeconds(-45);
        ctx.InsertInto<IDurationEntity>()
            .Values(new DurationEntity { Id = 1, Ticks = value, Seconds = value })
            .Insert();

        RawLong(conn, "seconds").Should().Be(-45);
        ctx.From<DurationEntity>().ToList().Single().Seconds.Should().Be(value);
    }

    [Fact]
    public void ScalarProjection_ShouldReadInDeclaredUnit()
    {
        using var conn = OpenConnection();
        using var ctx = ContextFor(conn);

        ctx.InsertInto<IDurationEntity>()
            .Values(new DurationEntity { Id = 1, Ticks = TimeSpan.FromSeconds(90), Seconds = TimeSpan.FromSeconds(90) })
            .Insert();

        ctx.From<IDurationEntity>().Select(x => x.Seconds).ToList().Single().Should().Be(TimeSpan.FromSeconds(90));
    }
}
