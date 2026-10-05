using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Diagnostics coverage for the <c>JoinInto.MultipleCollections</c> warning (#135): a pair command that
/// declares two or more collection navigations multiplies the parent rows and warns once per prepared
/// command, a one-to-one reference is excluded from the count, and the fluent
/// <see cref="JoinOptions.SuppressCartesianWarning"/> silences it.
/// </summary>
public class JoinIntoMultipleCollectionsWarningTests
{
    private static EntityBuilder<WarnParent> Parents(IDataContext ctx) =>
        ctx.From<WarnParent>(b => b
            .HasMany(p => p.First, c => c.ParentId)
            .HasMany(p => p.Second, c => c.ParentId)
            .HasOneToOne(p => p.Primary, p => p.Id, c => c.ParentId));

    private static string[] JoinIntoWarnings(WarningCapture capture) =>
        [.. capture.Warnings.Where(static w => w.Contains("JoinInto.MultipleCollections"))];

    [Fact]
    public void TwoCollectionNavigations_ShouldWarnExactlyOnce()
    {
        using var ctx = CreateWithLogger(out var capture);

        var cmd = (dynamic)Parents(ctx)
            .JoinInto(ctx.From<WarnChild>(), (p, c) => p.Id == c.ParentId, p => p.First)
            .JoinInto(ctx.From<WarnChild>(), (p, c) => p.Id == c.ParentId, p => p.Second)
            .CreateJoinIntoPairCommand();

        _ = ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        _ = ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

        JoinIntoWarnings(capture).Should().ContainSingle("the diagnostic must fire at most once per command");
    }

    [Fact]
    public void ACollectionPlusAOneToOneReference_ShouldNotWarn()
    {
        using var ctx = CreateWithLogger(out var capture);

        var cmd = (dynamic)Parents(ctx)
            .JoinInto(ctx.From<WarnChild>(), (p, c) => p.Id == c.ParentId, p => p.First)
            .JoinInto(ctx.From<WarnChild>(), (p, c) => p.Id == c.ParentId, p => p.Primary)
            .CreateJoinIntoPairCommand();

        _ = ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

        JoinIntoWarnings(capture).Should().BeEmpty("a one-to-one reference is not a collection navigation");
    }

    [Fact]
    public void SuppressCartesianWarning_ShouldSilenceTheDiagnostic()
    {
        using var ctx = CreateWithLogger(out var capture);

        var cmd = (dynamic)Parents(ctx)
            .JoinInto(ctx.From<WarnChild>(), (p, c) => p.Id == c.ParentId, p => p.First, j => j.SuppressCartesianWarning())
            .JoinInto(ctx.From<WarnChild>(), (p, c) => p.Id == c.ParentId, p => p.Second)
            .CreateJoinIntoPairCommand();

        _ = ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

        JoinIntoWarnings(capture).Should().BeEmpty("the fluent suppression carries to the whole command");
    }

    private static IDataContext CreateWithLogger(out WarningCapture capture)
    {
        var sink = new WarningCapture();
        capture = sink;
        return new SqliteDataContext(
            "Data Source=:memory:",
            new DataContextBuilder().UseLoggerFactory(LoggerFactory.Create(b => b.AddProvider(sink))));
    }

    private sealed class WarningCapture : ILoggerProvider
    {
        private readonly List<string> _warnings = [];

        public IReadOnlyList<string> Warnings => _warnings;

        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);

        public void Dispose()
        {
        }

        private sealed class CaptureLogger(WarningCapture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    owner._warnings.Add(formatter(state, exception));
            }
        }
    }
}

[SqlTable("warn_parent")]
public sealed class WarnParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<WarnChild> First { get; set; } = new List<WarnChild>();

    public ICollection<WarnChild> Second { get; set; } = new List<WarnChild>();

    public WarnChild? Primary { get; set; }
}

[SqlTable("warn_child")]
public sealed class WarnChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
