using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// r=2 D209-NL (#209, R209-05) SQLite evidence: a <c>LoadWith</c> per-parent top-N (child
/// <c>Limit</c>/<c>Offset</c>/<c>Page</c>) or an explicit <c>LoadWith(..., Correlated)</c> is rejected at the
/// non-list terminal boundary before any command is built: zero executed statements, zero plan-cache
/// entries, and the context-shared <c>AnyCommand</c> is neither replaced nor mutated. The same builder
/// keeps its existing behaviour for a plain <c>LoadWith</c>, and an inherited (context-default)
/// <c>Correlated</c> does not trigger the terminal guard.
/// </summary>
public class EagerTerminalGuardTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Statements.Add(command.CommandText);
    }

    public enum Shape
    {
        Limit,
        Offset,
        Page,
        Correlated,
        JoinIntoLimit,
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb(EagerLoadMode? contextMode = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-terminal-guard-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table term_guard_parent (id integer primary key, name text);" +
                "create table term_guard_child (id integer primary key, parent_id int not null, name text);" +
                "create table term_guard_note (id integer primary key, parent_id int not null, name text);" +
                "insert into term_guard_parent (id, name) values (1, 'a'), (2, 'b'), (3, 'zero');" +
                "insert into term_guard_child (id, parent_id, name) values (10, 1, 'x'), (11, 1, 'y'), (12, 2, 'z');" +
                "insert into term_guard_note (id, parent_id, name) values (100, 1, 'n');";
            cmd.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        if (contextMode is { } mode)
            builder.UseEagerLoadMode(mode);
        var ctx = new SqliteDataContext($"Data Source={path}", builder);
        // Declare the relationships so JoinInto can resolve the collection navigation.
        ctx.From<TerminalGuardParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static EntityBuilder<TerminalGuardParent> Parents(SqliteDataContext ctx)
        => ctx.From<TerminalGuardParent>();

    private static EntityBuilder<TerminalGuardParent> WithShape(EntityBuilder<TerminalGuardParent> builder, Shape shape)
    {
        if (shape == Shape.JoinIntoLimit)
            builder = builder.JoinInto(builder.DataProvider.From<TerminalGuardChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        return shape switch
        {
            Shape.Limit or Shape.JoinIntoLimit
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>().Limit(1), p => p.Id, n => n.ParentId),
            Shape.Offset
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>().Offset(1), p => p.Id, n => n.ParentId),
            Shape.Page
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>().Page(1, 1), p => p.Id, n => n.ParentId),
            Shape.Correlated
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Correlated),
            _ => builder,
        };
    }

    private static string ExpectedMessage(Shape shape)
        => shape == Shape.Correlated ? "*Correlated*" : "*Limit/Offset/Page*";

    private static readonly string[] Terminals =
    [
        "ToEnumerable", "ToHashSet", "ToDictionary",
        "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault",
        "Any", "Count", "LongCount", "Min", "Max", "Sum", "Avg", "Prepare",
        "ToAsyncEnumerable", "ToHashSetAsync", "ToDictionaryAsync",
        "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
        "LastAsync", "LastOrDefaultAsync", "AnyAsync", "CountAsync", "LongCountAsync", "MinAsync",
        "WriteJson", "WriteCsv",
    ];

    private static void Invoke(string terminal, EntityBuilder<TerminalGuardParent> builder)
    {
        switch (terminal)
        {
            case "ToEnumerable": builder.ToEnumerable().ToList(); break;
            case "ToHashSet": builder.ToHashSet(); break;
            case "ToDictionary": builder.ToDictionary(p => p.Id); break;
            case "First": builder.First(); break;
            case "FirstOrDefault": builder.FirstOrDefault(); break;
            case "Single": builder.Single(); break;
            case "SingleOrDefault": builder.SingleOrDefault(); break;
            case "Last": builder.Last(); break;
            case "LastOrDefault": builder.LastOrDefault(); break;
            case "Any": builder.Any(); break;
            case "Count": builder.Count(); break;
            case "LongCount": builder.LongCount(); break;
            case "Min": builder.Min(p => p.Id); break;
            case "Max": builder.Max(p => p.Id); break;
            case "Sum": builder.Sum(p => p.Id); break;
            case "Avg": builder.Avg(p => p.Id); break;
            case "Prepare": builder.Prepare(cancellationToken: CancellationToken.None); break;
            case "WriteJson": builder.WriteJson(new MemoryStream()); break;
            case "WriteCsv": builder.WriteCsv(new MemoryStream()); break;
            default: throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null);
        }
    }

    private static Task InvokeAsync(string terminal, EntityBuilder<TerminalGuardParent> builder)
    {
        var token = CancellationToken.None;
        return terminal switch
        {
            "ToAsyncEnumerable" => DrainAsync(builder, token),
            "ToHashSetAsync" => builder.ToHashSetAsync(token),
            "ToDictionaryAsync" => builder.ToDictionaryAsync(p => p.Id, token),
            "FirstAsync" => builder.FirstAsync(token),
            "FirstOrDefaultAsync" => builder.FirstOrDefaultAsync(token),
            "SingleAsync" => builder.SingleAsync(token),
            "SingleOrDefaultAsync" => builder.SingleOrDefaultAsync(token),
            "LastAsync" => builder.LastAsync(token),
            "LastOrDefaultAsync" => builder.LastOrDefaultAsync(token),
            "AnyAsync" => builder.AnyAsync(token),
            "CountAsync" => builder.CountAsync(token),
            "LongCountAsync" => builder.LongCountAsync(token),
            "MinAsync" => builder.MinAsync(p => p.Id, token),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null),
        };
    }

    private static async Task DrainAsync(EntityBuilder<TerminalGuardParent> builder, CancellationToken token)
    {
        await foreach (var _ in builder.ToAsyncEnumerable(token))
        {
        }
    }

    public static IEnumerable<object[]> NegativeCases()
    {
        foreach (var shape in Enum.GetValues<Shape>())
        {
            foreach (var terminal in Terminals)
                yield return [terminal, shape];
        }
    }

    [Theory]
    [MemberData(nameof(NegativeCases))]
    public async Task NonListTerminal_RejectsUnsupportedLoad_WithZeroSqlAndNoCacheEntry(string terminal, Shape shape)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            QueryPlanStore.Values.Should().BeEmpty("the purge must leave a clean plan cache");

            var builder = WithShape(Parents(ctx), shape);
            Exception? caught = null;
            try
            {
                if (IsAsync(terminal))
                    await InvokeAsync(terminal, builder);
                else
                    Invoke(terminal, builder);
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            // Assert the no-side-effect contract first so a regression (the guard disabled) fails with the
            // observed SQL/plan-cache activity rather than only the missing exception.
            interceptor.Statements.Should().BeEmpty("the terminal guard must reject the query before any SQL is executed");
            QueryPlanStore.Values.Should().BeEmpty("a rejected terminal must not insert a plan-cache entry");
            caught.Should().BeOfType<NotSupportedException>("the terminal guard must fail closed before building the command");
            caught!.Message.Should().Match(ExpectedMessage(shape));
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static bool IsAsync(string terminal)
        => terminal is "ToAsyncEnumerable" or "ToHashSetAsync" or "ToDictionaryAsync"
            or "FirstAsync" or "FirstOrDefaultAsync" or "SingleAsync" or "SingleOrDefaultAsync"
            or "LastAsync" or "LastOrDefaultAsync" or "AnyAsync" or "CountAsync" or "LongCountAsync" or "MinAsync";

    [Fact]
    public void RejectedNonListTerminal_LeavesTheSharedAnyCommandAndCacheIntact()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            // Allocate the context-shared AnyCommand first.
            ctx.From<TerminalGuardParent>().Where(p => p.Id == 1).Any().Should().BeTrue();
            var sharedAnyCommand = ctx.AnyCommand!.Value;
            var planCount = QueryPlanStore.Values.Count();

            // A rejected non-list terminal must not allocate/replace the shared AnyCommand nor touch the cache.
            Action act = () => WithShape(Parents(ctx), Shape.Limit).Any();
            act.Should().Throw<NotSupportedException>();

            QueryPlanStore.Values.Count().Should().Be(planCount, "a rejected terminal must not change the plan cache");
            ctx.AnyCommand!.Value.Should().BeSameAs(sharedAnyCommand, "the shared AnyCommand must not be replaced");
            sharedAnyCommand.Cache.Should().BeTrue("the sticky cache flag must not be mutated");

            // A later valid query on the same context still caches.
            var command = ctx.From<TerminalGuardParent>().Select(p => p.Id);
            command.Cache.Should().BeTrue();
            var first = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
            var second = ctx.GetPreparedQueryCommand(command, createEnumerator: false, storeInCache: true, CancellationToken.None);
            second.Should().BeSameAs(first, "ordinary queries must still reuse the plan cache after a rejected terminal");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void PlainLoadWith_NonListTerminals_KeepPriorBehaviour()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();
            var builder = ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>(), p => p.Id, n => n.ParentId);

            var parents = builder.ToEnumerable().ToList();
            parents.Should().HaveCount(3, "a plain LoadWith is not rejected by the terminal guard");
            parents.Should().OnlyContain(p => p.Children.Count == 0, "the non-list terminal does not load collections");

            builder.Any().Should().BeTrue();
            builder.Count().Should().Be(3);
            builder.First().Should().NotBeNull();
            builder.ToHashSet().Should().HaveCount(3);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ContextDefaultCorrelated_DoesNotTriggerTheTerminalGuard()
    {
        var (ctx, path) = CreateDb(EagerLoadMode.Correlated);
        try
        {
            ctx.PurgeQueryCache();
            var builder = ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>(), p => p.Id, n => n.ParentId);

            // Only an explicit Correlated triggers the guard; an inherited default must not be rejected at
            // the terminal boundary. Building the parent command runs the guard without preparing, so this
            // isolates the guard from the resolver (which still fails closed during preparation).
            Action build = () => builder.ToParentCommand();
            build.Should().NotThrow("a context-default Correlated must not be treated as explicit by the terminal guard");
            builder.ToParentCommand().EagerLoadMode.Should().BeNull("the builder made no explicit choice");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void ListTerminals_StillHonourPlainLoadWith()
    {
        var (ctx, path) = CreateDb();
        try
        {
            ctx.PurgeQueryCache();

            var toList = ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Children, c => c.From<TerminalGuardChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToList();
            toList.Should().HaveCount(3);
            toList.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(10, 11);

            var toArray = ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Children, c => c.From<TerminalGuardChild>(), p => p.Id, c => c.ParentId)
                .OrderBy(p => p.Id)
                .ToArray();
            toArray.Should().HaveCount(3);
            toArray.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(12);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

[SqlTable("term_guard_parent")]
public sealed class TerminalGuardParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<TerminalGuardChild> Children { get; set; } = new List<TerminalGuardChild>();
    public ICollection<TerminalGuardNote> Notes { get; set; } = new List<TerminalGuardNote>();
}

[SqlTable("term_guard_child")]
public sealed class TerminalGuardChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("term_guard_note")]
public sealed class TerminalGuardNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
