using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// r=2 D209-NL (#209, R209-05): a <c>LoadWith</c> per-parent top-N (child <c>Limit</c>/<c>Offset</c>/
/// <c>Page</c>) or an explicit <c>LoadWith(..., Correlated)</c> is rejected at the non-list terminal
/// boundary — <see cref="EntityBuilder{TEntity}.ToParentCommand"/> and
/// <see cref="EntityBuilder{TEntity}.SelectParent{TResult}"/> — before any parent command is built. Without
/// this guard a non-list terminal (which ignores eager loads) silently dropped the unsupported request:
/// the parent ran and the collection's paging/strategy was never honored. The guard is the shared,
/// side-effect-free <c>EntityBuilderEagerLoading.ValidateLoadWithForJoinInto</c>; it only rejects an
/// explicit mode (never a context default) and never materializes a collection.
/// </summary>
public class EagerTerminalGuardTests : IDisposable
{
    /// <summary>
    /// D5b (T258, r=1 n=1): <see cref="QueryPlanStore"/> is process-wide <c>[ThreadStatic]</c> state, so a
    /// plan cached by another test class on this thread (for example a cached prepare) is still visible
    /// here. The empty-store assertions below describe only the rejected terminal's own effect, so each
    /// test instance starts from an empty thread-local store and drops whatever it left behind.
    /// </summary>
    public EagerTerminalGuardTests() => QueryPlanStore.Clear();

    public void Dispose() => QueryPlanStore.Clear();

    public sealed class TgParent
    {
        public int Id { get; set; }
        public ICollection<TgChild> Children { get; set; } = new List<TgChild>();
        public ICollection<TgNote> Notes { get; set; } = new List<TgNote>();
    }

    public sealed class TgChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class TgNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    /// <summary>The unsupported eager-load request shapes (V19–V23).</summary>
    public enum Shape
    {
        Limit,
        Offset,
        Page,
        Correlated,
        JoinIntoLimit,
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<TgParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
        context.From<TgChild>().WithData(new[]
        {
            new TgChild { Id = 10, ParentId = 1 },
            new TgChild { Id = 11, ParentId = 1 },
            new TgChild { Id = 12, ParentId = 2 },
        });
        context.From<TgNote>().WithData(new[]
        {
            new TgNote { Id = 100, ParentId = 1 },
            new TgNote { Id = 101, ParentId = 2 },
        });
        context.From<TgParent>().WithData(new[]
        {
            new TgParent { Id = 1 },
            new TgParent { Id = 2 },
        });
        return context;
    }

    private static EntityBuilder<TgParent> WithShape(EntityBuilder<TgParent> builder, Shape shape, Action? onFactory = null)
    {
        Action factory = onFactory ?? (() => { });
        if (shape == Shape.JoinIntoLimit)
            builder = builder.JoinInto(builder.DataProvider.From<TgChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        return shape switch
        {
            Shape.Limit or Shape.JoinIntoLimit
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TgNote>().Limit(1); }, p => p.Id, n => n.ParentId),
            Shape.Offset
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TgNote>().Offset(1); }, p => p.Id, n => n.ParentId),
            Shape.Page
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TgNote>().Page(1, 1); }, p => p.Id, n => n.ParentId),
            Shape.Correlated
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TgNote>(); }, p => p.Id, n => n.ParentId, EagerLoadMode.Correlated),
            _ => builder,
        };
    }

    private static string ExpectedMessage(Shape shape)
        => shape == Shape.Correlated ? "*Correlated*" : "*Limit/Offset/Page*";

    private static readonly string[] SyncTerminals =
    [
        "ToEnumerable", "ToHashSet", "ToDictionary",
        "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault",
        "Any", "Count", "LongCount", "Min", "Max", "Sum", "Avg", "Prepare",
        "FirstProjection", "SingleProjection",
        "WriteCsv", "WriteJson",
    ];

    private static readonly string[] AsyncTerminals =
    [
        "ToAsyncEnumerable", "ToHashSetAsync", "ToDictionaryAsync",
        "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
        "LastAsync", "LastOrDefaultAsync", "AnyAsync", "CountAsync", "LongCountAsync", "MinAsync",
        "WriteCsvAsync", "WriteJsonAsync",
    ];

    private static void InvokeSync(string terminal, EntityBuilder<TgParent> builder)
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
            case "Prepare": builder.Prepare(default, CancellationToken.None); break;
            case "FirstProjection": builder.FirstOrFirstOrDefaultCommand(p => p.Id); break;
            case "SingleProjection": builder.SingleOrSingleOrDefaultCommand(p => p.Id); break;
            case "WriteCsv": builder.WriteCsv(new MemoryStream()); break;
            case "WriteJson": builder.WriteJson(new MemoryStream()); break;
            default: throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null);
        }
    }

    private static Task InvokeAsync(string terminal, EntityBuilder<TgParent> builder)
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

    private static async Task WriteCsvAsync(EntityBuilder<TgParent> builder, CancellationToken token)
        => await builder.WriteCsvAsync(new MemoryStream(), cancellationToken: token);

    private static async Task WriteJsonAsync(EntityBuilder<TgParent> builder, CancellationToken token)
        => await builder.WriteJsonAsync(new MemoryStream(), token);

    private static async Task DrainAsync(EntityBuilder<TgParent> builder, CancellationToken token)
    {
        await foreach (var _ in builder.ToAsyncEnumerable(token))
        {
        }
    }

    public static IEnumerable<object[]> NegativeCases()
    {
        foreach (var shape in Enum.GetValues<Shape>())
        {
            foreach (var terminal in SyncTerminals)
                yield return [terminal, shape, false];
            foreach (var terminal in AsyncTerminals)
                yield return [terminal, shape, true];
        }
    }

    [Theory]
    [MemberData(nameof(NegativeCases))]
    public async Task NonListTerminal_RejectsUnsupportedLoad(string terminal, Shape shape, bool async)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        if (async)
        {
            var act = () => InvokeAsync(terminal, builder);
            await act.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedMessage(shape));
        }
        else
        {
            var act = () => InvokeSync(terminal, builder);
            act.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Validator_RejectsUnsupportedShapes(Shape shape)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        Action act = () => EntityBuilderEagerLoading.ValidateLoadWithForJoinInto(builder);
        act.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));
    }

    public static IEnumerable<object[]> Shapes() => Enum.GetValues<Shape>().Select(shape => new object[] { shape });

    [Fact]
    public void Validator_PlainLoadWith_DoesNotThrow()
    {
        using var context = CreateContext();
        var builder = context.From<TgParent>()
            .LoadWith(p => p.Notes, c => c.From<TgNote>(), p => p.Id, n => n.ParentId);

        Action act = () => EntityBuilderEagerLoading.ValidateLoadWithForJoinInto(builder);
        act.Should().NotThrow();
    }

    [Fact]
    public void PlainLoadWith_NonListTerminals_AreNotRejected()
    {
        using var context = CreateContext();
        var builder = context.From<TgParent>()
            .LoadWith(p => p.Notes, c => c.From<TgNote>(), p => p.Id, n => n.ParentId);

        builder.ToEnumerable().ToList().Should().HaveCount(2);
        builder.Any().Should().BeTrue();
        builder.Count().Should().Be(2);
        builder.First().Should().NotBeNull();
        builder.ToHashSet().Should().HaveCount(2);
        builder.Min(p => p.Id).Should().Be(1);
    }

    [Fact]
    public void JoinIntoWithoutUnsupportedLoad_NonListTerminals_AreNotRejected()
    {
        using var context = CreateContext();
        var builder = context.From<TgParent>()
            .JoinInto(context.From<TgChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        builder.ToEnumerable().ToList().Should().HaveCount(2);
        builder.Any().Should().BeTrue();
        builder.Count().Should().Be(2);
    }

    [Fact]
    public void NoEagerLoads_LeavesNoEagerCommandState()
    {
        using var context = CreateContext();
        var command = context.From<TgParent>().ToCommand();

        command.EagerLoadMode.Should().BeNull("a command with no eager loads carries no strategy");
        command.EagerLoadShape.IsEmpty.Should().BeTrue("a command with no eager loads carries the empty shape");
        context.From<TgParent>().ToEnumerable().ToList().Should().HaveCount(2);
    }

    /// <summary>
    /// r=3 (#209, R209-01 wording): driving the whole non-list terminal matrix on one builder must
    /// invoke the child factory at most once per <c>(spec, context)</c> — at the first terminal
    /// validation, which builds the child once to detect a per-parent top-N. A second invocation would
    /// make this assertion fail. An explicit <c>Correlated</c> is rejected before the factory is needed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task RejectedTerminalMatrix_InvokesTheChildFactoryAtMostOncePerSpecAndContext(Shape shape)
    {
        using var context = CreateContext();
        var calls = 0;
        var builder = WithShape(context.From<TgParent>(), shape, () => calls++);

        foreach (var terminal in SyncTerminals)
        {
            try { InvokeSync(terminal, builder); }
            catch (NotSupportedException) { }
        }

        foreach (var terminal in AsyncTerminals)
        {
            try { await InvokeAsync(terminal, builder); }
            catch (NotSupportedException) { }
        }

        if (shape == Shape.Correlated)
            calls.Should().Be(0, "an explicit Correlated is rejected before the child factory is invoked");
        else
            calls.Should().Be(1, "the factory is invoked at most once per (spec, context) at the first terminal validation");
    }

    /// <summary>r=3 (#209, R209-01 wording): a valid non-paged <c>LoadWith</c> builds the child once and reuses the memo.</summary>
    [Fact]
    public void ValidNonPagedLoadWith_ReusesTheChildMemoAcrossTheNonListMatrix()
    {
        using var context = CreateContext();
        var calls = 0;
        var builder = context.From<TgParent>()
            .LoadWith(p => p.Notes, c => { calls++; return c.From<TgNote>(); }, p => p.Id, n => n.ParentId);

        builder.ToEnumerable().ToList().Should().HaveCount(2);
        builder.Count().Should().Be(2);
        builder.Any().Should().BeTrue();
        builder.First().Should().NotBeNull();
        builder.ToHashSet().Should().HaveCount(2);
        builder.Min(p => p.Id).Should().Be(1);

        calls.Should().Be(1, "a valid non-paged LoadWith builds the child at most once per (spec, context) and reuses it");
    }

    /// <summary>r=3 (#209, R209-01 wording): a child-factory exception propagates unchanged (not swallowed/replaced).</summary>
    [Fact]
    public async Task FactoryException_PropagatesUnchanged()
    {
        using var context = CreateContext();
        var marker = new InvalidOperationException("child-factory-boom");
        var builder = context.From<TgParent>()
            .LoadWith(p => p.Notes, c => throw marker, p => p.Id, n => n.ParentId);

        Action sync = () => builder.Count();
        sync.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(marker);

        Func<Task> asyncAct = () => builder.CountAsync(CancellationToken.None);
        (await asyncAct.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(marker);
    }

    /// <summary>r=3 (#209, V25 wording): memoization is strictly per <c>(spec, context)</c>, with no static state.</summary>
    [Fact]
    public void Factory_MemoizedPerContext_NoStaticReuse()
    {
        using var first = CreateContext();
        using var second = CreateContext();
        var calls = 0;

        EntityBuilder<TgParent> Build(InMemoryDataContext ctx) => ctx.From<TgParent>()
            .LoadWith(p => p.Notes, c => { calls++; return c.From<TgNote>(); }, p => p.Id, n => n.ParentId);

        Build(first).ToParentCommand();
        calls.Should().Be(1, "the first context resolves its own memoized child");

        Build(second).ToParentCommand();
        calls.Should().Be(2, "a second context must resolve independently; no static/global reuse");
    }

    /// <summary>r=3 (#209, V25 wording): a rejected terminal leaves no plan-cache entry behind.</summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void RejectedLoad_LeavesNoPlanCacheEntry(Shape shape)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        Action act = () => builder.Count();
        act.Should().Throw<NotSupportedException>();

        QueryPlanStore.Values.Should().BeEmpty("a rejected terminal must not insert a plan-cache entry");
    }

    /// <summary>
    /// r=3 n=2 (#209, D:T209-required-tests, gap `rv2/async-zerobuild.md:84-93`): the async writers
    /// <c>WriteCsvAsync</c>/<c>WriteJsonAsync</c> are guarded terminals. With an unsupported load they
    /// throw before any command is built or a plan is cached, and — unlike the scalar terminals — leave
    /// the destination stream at zero bytes, the strongest direct observation of "no write started".
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public async Task AsyncWriters_RejectUnsupportedLoad_WithoutBuildingACommand(Shape shape)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        var csv = new MemoryStream();
        Func<Task> writeCsv = async () => await builder.WriteCsvAsync(csv);
        await writeCsv.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedMessage(shape));
        csv.Length.Should().Be(0, "the terminal guard rejects before the CSV writer emits any byte");
        QueryPlanStore.Values.Should().BeEmpty("a rejected async CSV writer must not insert a plan-cache entry");

        var json = new MemoryStream();
        Func<Task> writeJson = async () => await builder.WriteJsonAsync(json);
        await writeJson.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedMessage(shape));
        json.Length.Should().Be(0, "the terminal guard rejects before the JSON writer emits any byte");
        QueryPlanStore.Values.Should().BeEmpty("a rejected async JSON writer must not insert a plan-cache entry");
    }

    /// <summary>
    /// r=3 n=2 (#209, D:T209-required-tests, gap `rv2/changed-branch-map.md` terminal families): the
    /// projection terminal overloads <c>FirstOrFirstOrDefaultCommand(exp)</c> /
    /// <c>SingleOrSingleOrDefaultCommand(exp)</c> route through <c>SelectParent</c> and are rejected before
    /// the projected command is built or cached.
    /// </summary>
    [Theory]
    [MemberData(nameof(Shapes))]
    public void ProjectionTerminalOverloads_RejectUnsupportedLoad_WithoutBuildingACommand(Shape shape)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        Action first = () => builder.FirstOrFirstOrDefaultCommand(p => p.Id);
        first.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));

        Action single = () => builder.SingleOrSingleOrDefaultCommand(p => p.Id);
        single.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));

        QueryPlanStore.Values.Should().BeEmpty("a rejected projection terminal must not insert a plan-cache entry");
    }
}
