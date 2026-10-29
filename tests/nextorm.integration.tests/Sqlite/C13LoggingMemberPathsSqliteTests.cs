using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Campaign 13, group 4 lever: the same query builder work executed with a <b>Trace-level logger</b>
/// attached to the context. The translator emits diagnostic logging on its hot member paths, so one
/// logging-enabled context flips those branches across the core visitors while the assertions stay on the
/// rows the query returns.
/// </summary>
public sealed class C13LoggingMemberPathsSqliteTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SqliteDataContext _db;

    public C13LoggingMemberPathsSqliteTests()
    {
        _databasePath = Path.Combine(
            Path.GetTempPath(),
            $"nextorm.c13.log.{Environment.ProcessId}.{Guid.NewGuid():N}.db");

        var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(new TraceSink());
        });

        _db = new SqliteDataContext($"Data Source='{_databasePath}'", new DataContextBuilder().UseLoggerFactory(factory));
        ((IConnectionManager)_db).EnsureConnectionOpen();

        using var command = ((DataContext)_db).GetConnection().CreateCommand();
        command.CommandText = """
            create table c13_log (id integer primary key, name text null, when_utc text null, amount real null);
            insert into c13_log (id, name, when_utc, amount) values (1, 'alpha', '2024-05-17 13:45:30', 10.0),
                                                                    (2, 'beta',  '2023-01-02 00:00:00', 20.0);
            """;
        command.ExecuteNonQuery();
    }

    /// <summary>Member access on a projected entity and on a mapped column with logging enabled.</summary>
    [Fact]
    public void MemberAccess_WithTraceLogging_ShouldReturnTheRows()
    {
        var rows = _db.From<C13Log>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Upper = x.Name!.ToUpper(), Length = x.Name!.Length })
            .ToList();

        rows.Should().HaveCount(2);
        rows[0].Upper.Should().Be("ALPHA");
        rows[1].Length.Should().Be(4);
    }

    /// <summary>DateTime member translation (Year/Month/Day) with logging enabled.</summary>
    [Fact]
    public void DateTimeMembers_WithTraceLogging_ShouldTranslateAndReturnValues()
    {
        var row = _db.From<C13Log>()
            .Where(x => x.Id == 1)
            .Select(x => new { Year = x.WhenUtc!.Value.Year, Month = x.WhenUtc!.Value.Month, Day = x.WhenUtc!.Value.Day })
            .Single();

        row.Year.Should().Be(2024);
        row.Month.Should().Be(5);
        row.Day.Should().Be(17);
    }

    /// <summary>A member used as a constant source inside a projection, with logging enabled.</summary>
    [Fact]
    public void ConstantMemberSource_WithTraceLogging_ShouldEvaluate()
    {
        var row = _db.From<C13Log>()
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, Doubled = x.Amount!.Value * 2 })
            .Single();

        row.Id.Should().Be(2);
        row.Doubled.Should().Be(40.0);
    }

    public void Dispose()
    {
        _db.Dispose();
        try
        {
            if (File.Exists(_databasePath))
                File.Delete(_databasePath);
        }
        catch (IOException)
        {
            // Locked temporary files are left to the OS; the test outcome is unaffected.
        }
    }

    /// <summary>A logger provider that discards records; it exists to have the level enabled.</summary>
    private sealed class TraceSink : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Sink();

        public void Dispose()
        {
        }

        private sealed class Sink : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel <= LogLevel.Trace;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // The records are intentionally discarded: the branch under test is the enabled level,
                // and every assertion stays on the returned rows.
            }
        }
    }

    /// <summary>The seeded row.</summary>
    [SqlTable("c13_log")]
    public sealed class C13Log
    {
        /// <summary>The primary key.</summary>
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>The optional name.</summary>
        [Column("name")]
        public string? Name { get; set; }

        /// <summary>The optional timestamp.</summary>
        [Column("when_utc")]
        public DateTime? WhenUtc { get; set; }

        /// <summary>The optional amount.</summary>
        [Column("amount")]
        public double? Amount { get; set; }
    }
}
