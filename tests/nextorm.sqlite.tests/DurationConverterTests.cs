using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// A value converter owns its provider representation: a converter to <see cref="TimeSpan"/> is applied
/// as-is, without the integer duration normalization a bare <see cref="TimeSpan"/> property gets. SQLite
/// has no native duration type, so the driver stores the converted value in its own time-of-day form; the
/// round-trip below pins that the converter contract is not silently reduced to a duration unit.
/// </summary>
public sealed class IntToDurationConverter : ValueConverter<int, TimeSpan>
{
    public override TimeSpan ConvertToProvider(int model) => TimeSpan.FromSeconds(model);

    public override int ConvertFromProvider(TimeSpan provider) => (int)provider.TotalSeconds;
}

[SqlTable("converter_duration_entity")]
public interface IConverterDurationEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("elapsed")]
    [ValueConverter(typeof(IntToDurationConverter))]
    int Elapsed { get; set; }
}

public sealed class ConverterDurationEntity : IConverterDurationEntity
{
    public int Id { get; set; }
    public int Elapsed { get; set; }
}

public class DurationConverterTests
{
    [Fact]
    public void ConverterToTimeSpan_ShouldRoundTripInsertSelectPredicate()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        using (var setup = conn.CreateCommand())
        {
            setup.CommandText = "create table converter_duration_entity (id integer primary key, elapsed bigint);";
            setup.ExecuteNonQuery();
        }

        using var ctx = new SqliteDataContext(conn, new DataContextBuilder());

        ctx.InsertInto<IConverterDurationEntity>()
            .Values(new ConverterDurationEntity { Id = 1, Elapsed = 90 })
            .Insert();
        ctx.InsertInto<IConverterDurationEntity>()
            .Values(new ConverterDurationEntity { Id = 2, Elapsed = 30 })
            .Insert();

        var row = ctx.From<ConverterDurationEntity>().Where(x => x.Id == 1).ToList().Single();
        row.Elapsed.Should().Be(90);

        var filtered = ctx.From<ConverterDurationEntity>().Where(x => x.Elapsed >= 60).Select(x => x.Id).ToList();
        filtered.Should().Equal(1);

        var reversed = ctx.From<ConverterDurationEntity>().Where(x => 60 <= x.Elapsed).Select(x => x.Id).ToList();
        reversed.Should().Equal(1);
    }
}
