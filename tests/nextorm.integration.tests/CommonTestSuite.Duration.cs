using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [SqlTable("duration_probe")]
    public interface IDurationProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }

        [Column("span")]
        TimeSpan Span { get; set; }

        [Column("span_sec")]
        [Duration(DurationUnit.Seconds)]
        TimeSpan SpanSeconds { get; set; }

        [Column("span_null")]
        TimeSpan? SpanNull { get; set; }
    }

    public sealed class DurationProbe : IDurationProbe
    {
        public long Id { get; set; }
        public TimeSpan Span { get; set; }
        public TimeSpan SpanSeconds { get; set; }
        public TimeSpan? SpanNull { get; set; }
    }

    /// <summary>
    /// Round-trips a <see cref="TimeSpan"/> column on every provider: natively on PostgreSQL
    /// (<c>interval</c>) and MySQL/MariaDB (<c>TIME</c>), and in a <c>bigint</c> integer column in the
    /// declared unit (ticks when none is declared) on SQL Server, SQLite and ClickHouse. The table is
    /// created from the dialect's <c>MakeDurationType</c> so the test stays provider-agnostic, and is
    /// dropped again at the end.
    /// </summary>
    [Fact]
    public void DurationColumns_ShouldRoundTrip() => DurationRoundTrip(_sut.DataProvider);

    /// <summary>
    /// The round-trip body, shared with provider integration classes that do not inherit
    /// <see cref="CommonTestSuite"/> (ClickHouse runs its own provider-specific suite).
    /// </summary>
    internal static void DurationRoundTrip(IDataContext ctx)
    {
        var dialect = ((DataContext)ctx).Dialect;
        var durationType = dialect.MakeDurationType(null);
        var nullableDurationType = dialect.MakeNullableDurationType(null);
        var engine = dialect.GetType().Name.Contains("ClickHouse", StringComparison.Ordinal) ? " engine = Memory" : string.Empty;

        ExecuteDuration(ctx, "drop table if exists duration_probe");
        ExecuteDuration(ctx, $"create table duration_probe (id bigint, span {durationType}, span_sec {durationType}, span_null {nullableDurationType}){engine}");

        try
        {
            // Whole-second values only: MySQL/MariaDB's bare TIME stores no fractional part.
            var value = TimeSpan.FromSeconds(90);
            ctx.InsertInto<IDurationProbe>()
                .Values(new DurationProbe { Id = 1, Span = value, SpanSeconds = value, SpanNull = TimeSpan.FromSeconds(2) })
                .Insert();
            ctx.InsertInto<IDurationProbe>()
                .Values(new DurationProbe { Id = 2, Span = TimeSpan.FromSeconds(30), SpanSeconds = TimeSpan.FromSeconds(30), SpanNull = null })
                .Insert();

            var first = ctx.From<DurationProbe>().Where(x => x.Id == 1).ToList().Single();
            first.Span.Should().Be(value);
            first.SpanSeconds.Should().Be(value);
            first.SpanNull.Should().Be(TimeSpan.FromSeconds(2));

            var second = ctx.From<DurationProbe>().Where(x => x.Id == 2).ToList().Single();
            second.Span.Should().Be(TimeSpan.FromSeconds(30));
            second.SpanNull.Should().BeNull();

            // Bulk insert uses the provider's native path where available (SqlBulkCopy) and the
            // portable path otherwise; both must store the declared unit.
            ctx.BulkInsertInto<IDurationProbe>()
                .Values([
                    new DurationProbe { Id = 3, Span = TimeSpan.FromSeconds(45), SpanSeconds = TimeSpan.FromSeconds(45), SpanNull = null },
                    new DurationProbe { Id = 4, Span = TimeSpan.FromSeconds(75), SpanSeconds = TimeSpan.FromSeconds(75), SpanNull = TimeSpan.FromSeconds(5) },
                ])
                .BulkInsert();

            var fourth = ctx.From<DurationProbe>().Where(x => x.Id == 4).ToList().Single();
            fourth.SpanSeconds.Should().Be(TimeSpan.FromSeconds(75));
            fourth.SpanNull.Should().Be(TimeSpan.FromSeconds(5));

            // A TimeSpan constant is converted to the column's storage form on either side of the
            // comparison (the driver's native type on PostgreSQL/MySQL, the declared unit elsewhere).
            ctx.From<DurationProbe>().Where(x => x.SpanSeconds > TimeSpan.FromSeconds(60)).Select(x => x.Id).ToList()
                .Should().BeEquivalentTo(new long[] { 1, 4 });
            ctx.From<DurationProbe>().Where(x => TimeSpan.FromSeconds(60) < x.Span).Select(x => x.Id).ToList()
                .Should().BeEquivalentTo(new long[] { 1, 4 });
        }
        finally
        {
            ExecuteDuration(ctx, "drop table if exists duration_probe");
        }
    }

    private static void ExecuteDuration(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
