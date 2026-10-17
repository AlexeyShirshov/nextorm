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

    private static EntityBuilder<TerminalGuardParent> WithShape(EntityBuilder<TerminalGuardParent> builder, Shape shape, Action? onFactory = null)
    {
        Action factory = onFactory ?? (() => { });
        if (shape == Shape.JoinIntoLimit)
            builder = builder.JoinInto(builder.DataProvider.From<TerminalGuardChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        return shape switch
        {
            Shape.Limit or Shape.JoinIntoLimit
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>().Limit(1); }, p => p.Id, n => n.ParentId),
            Shape.Offset
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>().Offset(1); }, p => p.Id, n => n.ParentId),
            Shape.Page
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>().Page(1, 1); }, p => p.Id, n => n.ParentId),
            Shape.Correlated
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>(); }, p => p.Id, n => n.ParentId, EagerLoadMode.Correlated),
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
        "FirstProjection", "SingleProjection",
        "ToAsyncEnumerable", "ToHashSetAsync", "ToDictionaryAsync",
        "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
        "LastAsync", "LastOrDefaultAsync", "AnyAsync", "CountAsync", "LongCountAsync", "MinAsync",
        "WriteJson", "WriteCsv", "WriteJsonAsync", "WriteCsvAsync",
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
            case "FirstProjection": builder.FirstOrFirstOrDefaultCommand(p => p.Id); break;
            case "SingleProjection": builder.SingleOrSingleOrDefaultCommand(p => p.Id); break;
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
            "WriteCsvAsync" => WriteCsvAsync(builder, token),
            "WriteJsonAsync" => WriteJsonAsync(builder, token),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null),
        };
    }

    private static async Task WriteCsvAsync(EntityBuilder<TerminalGuardParent> builder, CancellationToken token)
        => await builder.WriteCsvAsync(new MemoryStream(), cancellationToken: token);

    private static async Task WriteJsonAsync(EntityBuilder<TerminalGuardParent> builder, CancellationToken token)
        => await builder.WriteJsonAsync(new MemoryStream(), token);

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
            or "LastAsync" or "LastOrDefaultAsync" or "AnyAsync" or "CountAsync" or "LongCountAsync" or "MinAsync"
            or "WriteCsvAsync" or "WriteJsonAsync";

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

    public static IEnumerable<object[]> Shapes() => Enum.GetValues<Shape>().Select(shape => new object[] { shape });

    /// <summary>
    /// r=3 (#209, R209-01 wording): driving the whole non-list terminal matrix on one builder invokes the
    /// child factory at most once per <c>(spec, context)</c>, at the first terminal validation, while
    /// every rejection stays side-effect free — no executed SQL and no plan-cache entry. An explicit
    /// <c>Correlated</c> is rejected before the factory is needed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task RejectedTerminalMatrix_InvokesTheChildFactoryAtMostOnce_WithZeroSqlAndNoCacheEntry(Shape shape)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            var calls = 0;
            var builder = WithShape(Parents(ctx), shape, () => calls++);

            foreach (var terminal in Terminals)
            {
                try
                {
                    if (IsAsync(terminal))
                        await InvokeAsync(terminal, builder);
                    else
                        Invoke(terminal, builder);
                }
                catch (NotSupportedException)
                {
                }
            }

            interceptor.Statements.Should().BeEmpty("the terminal guard must reject the query before any SQL is executed");
            QueryPlanStore.Values.Should().BeEmpty("a rejected terminal must not insert a plan-cache entry");
            if (shape == Shape.Correlated)
                calls.Should().Be(0, "an explicit Correlated is rejected before the child factory is invoked");
            else
                calls.Should().Be(1, "the factory is invoked at most once per (spec, context) at the first terminal validation");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// r=3 (#209, R209-01 wording): a valid non-paged <c>LoadWith</c> keeps its baseline parent-only
    /// results and SQL on a non-list terminal, and builds the child once.
    /// </summary>
    [Fact]
    public void ValidNonPagedLoadWith_KeepsBaselineResultsAndSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            var calls = 0;
            var builder = ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Notes, c => { calls++; return c.From<TerminalGuardNote>(); }, p => p.Id, n => n.ParentId);

            var parents = builder.ToEnumerable().ToList();
            parents.Should().HaveCount(3);
            parents.Should().OnlyContain(p => p.Notes.Count == 0, "a non-list terminal does not materialize the collection");

            interceptor.Statements.Should().ContainSingle("a valid non-paged LoadWith keeps the parent-only baseline SQL on a non-list terminal");
            calls.Should().Be(1, "the valid load is validated at most once per (spec, context)");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>r=3 (#209, R209-01 wording): a child-factory exception propagates unchanged, before any SQL runs.</summary>
    [Fact]
    public void FactoryException_PropagatesUnchanged_WithZeroSql()
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            var marker = new InvalidOperationException("child-factory-boom");
            var builder = ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Notes, c => throw marker, p => p.Id, n => n.ParentId);

            Action act = () => builder.Count();
            act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(marker);
            interceptor.Statements.Should().BeEmpty("a throwing child factory must propagate before any SQL runs");
            QueryPlanStore.Values.Should().BeEmpty("a throwing child factory must not insert a plan-cache entry");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>r=3 (#209, V25 wording): memoization is strictly per <c>(spec, context)</c>; two contexts never share a child.</summary>
    [Fact]
    public void Factory_MemoizedPerContext_NoCrossContextReuse()
    {
        var (first, firstPath) = CreateDb();
        var (second, secondPath) = CreateDb();
        try
        {
            var calls = 0;
            EntityBuilder<TerminalGuardParent> Build(SqliteDataContext ctx) => ctx.From<TerminalGuardParent>()
                .LoadWith(p => p.Notes, c => { calls++; return c.From<TerminalGuardNote>(); }, p => p.Id, n => n.ParentId);

            Build(first).Count().Should().Be(3);
            calls.Should().Be(1, "the first context resolves its own memoized child");

            Build(second).Count().Should().Be(3);
            calls.Should().Be(2, "each (spec, context) resolves independently; no cross-context reuse");
        }
        finally
        {
            first.Dispose();
            File.Delete(firstPath);
            second.Dispose();
            File.Delete(secondPath);
        }
    }

    /// <summary>r=3 (#209, V19–V22): <c>Limit</c>, <c>Offset</c> and <c>Page</c> are rejected alike.</summary>
    [Theory]
    [InlineData(Shape.Limit)]
    [InlineData(Shape.Offset)]
    [InlineData(Shape.Page)]
    public void LimitOffsetPage_AreRejectedAlike(Shape shape)
    {
        var (ctx, path) = CreateDb();
        try
        {
            var builder = WithShape(Parents(ctx), shape);
            Action act = () => builder.Count();
            act.Should().Throw<NotSupportedException>().WithMessage("*Limit/Offset/Page*");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// r=3 n=2 (#209, D:T209-required-tests, gap `rv2/async-zerobuild.md:84-93`): the async writers
    /// <c>WriteCsvAsync</c>/<c>WriteJsonAsync</c> are guarded terminals. With an unsupported load they
    /// throw before any command is built, cached or executed, and — unlike the scalar terminals — leave
    /// the destination stream at zero bytes, the strongest direct observation of "no write started".
    /// </summary>
    [Theory]
    [InlineData(Shape.Limit)]
    [InlineData(Shape.Correlated)]
    public async Task AsyncWriters_RejectUnsupportedLoad_BeforeAnySqlOrWrite(Shape shape)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            var builder = WithShape(Parents(ctx), shape);

            var csv = new MemoryStream();
            Func<Task> writeCsv = async () => await builder.WriteCsvAsync(csv);
            await writeCsv.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedMessage(shape));
            csv.Length.Should().Be(0, "the terminal guard rejects before the CSV writer emits any byte");
            interceptor.Statements.Should().BeEmpty("no SQL may run for a rejected async CSV writer");
            QueryPlanStore.Values.Should().BeEmpty("a rejected async CSV writer must not insert a plan-cache entry");

            var json = new MemoryStream();
            Func<Task> writeJson = async () => await builder.WriteJsonAsync(json);
            await writeJson.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedMessage(shape));
            json.Length.Should().Be(0, "the terminal guard rejects before the JSON writer emits any byte");
            interceptor.Statements.Should().BeEmpty("no SQL may run for a rejected async JSON writer");
            QueryPlanStore.Values.Should().BeEmpty("a rejected async JSON writer must not insert a plan-cache entry");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// r=3 n=2 (#209, D:T209-required-tests, candidate #6 zero-command-build observation): the projection
    /// terminal overloads <c>FirstOrFirstOrDefaultCommand(exp)</c> / <c>SingleOrSingleOrDefaultCommand(exp)</c>
    /// route through <c>SelectParent</c> and are rejected before the projected command is built, cached or
    /// executed. No <c>CreateCommand</c>/<c>GetAnyCommand</c> counter exists (see `rv2/async-zerobuild.md:9-12`),
    /// so zero executed SQL plus an unchanged plan-cache count is the strongest available observation.
    /// </summary>
    [Theory]
    [InlineData(Shape.Limit)]
    [InlineData(Shape.Correlated)]
    public void ProjectionTerminalOverloads_RejectUnsupportedLoad_BeforeAnySqlOrCache(Shape shape)
    {
        var interceptor = new SqlRecordingInterceptor();
        var (ctx, path) = CreateDb();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            var builder = WithShape(Parents(ctx), shape);

            Action first = () => builder.FirstOrFirstOrDefaultCommand(p => p.Id);
            first.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));

            Action single = () => builder.SingleOrSingleOrDefaultCommand(p => p.Id);
            single.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));

            interceptor.Statements.Should().BeEmpty("a rejected projection terminal must not execute SQL");
            QueryPlanStore.Values.Should().BeEmpty("a rejected projection terminal must not insert a plan-cache entry");
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
