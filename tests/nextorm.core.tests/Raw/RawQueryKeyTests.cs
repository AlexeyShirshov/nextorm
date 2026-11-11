using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// Issue #319 raw-key oracle: the description is a value, a branch never mutates its source or a
/// sibling, and every dimension that changes the rendered SQL or the materialized result changes the
/// canonical key while a captured runtime value does not.
/// </summary>
public class RawQueryKeyTests
{
    private sealed class OtherEntity
    {
        public int Id { get; set; }
    }

    private static RawQuery<SimpleEntity> Source() => RawQueryFactory.From<SimpleEntity>();

    private static readonly RawQueryContext DefaultContext = new(
        providerType: typeof(object),
        quoteIdentifiers: false,
        namingConvention: null,
        keywordCase: KeywordCase.Lower,
        commandTimeout: null);

    private static RawQueryState WhereCaptured(RawQuery<SimpleEntity> query, int value) =>
        query.Where(x => x.Id > value).State;

    private static RawQueryState WhereCapturedAtAnotherSite(RawQuery<SimpleEntity> query, int value) =>
        query.Where(x => x.Id > value).State;

    private static RawQueryKey KeyOf(RawQueryState state) => KeyOf(state, DefaultContext);

    private static RawQueryKey KeyOf(RawQueryState state, in RawQueryContext context)
    {
        RawQueryStateEquality.TryCreateKey(state, context, out var key).Should().BeTrue("the shape is representable");
        return key;
    }

    private static RawQueryState SimpleShape() =>
        Source().Where(x => x.Id > 1).Select(x => x.Id).State;

    [Fact]
    public void FreshDescription_OfTheSameShape_ShouldShareTheKey()
    {
        var first = KeyOf(SimpleShape());
        var second = KeyOf(SimpleShape());

        second.Hash.Should().Be(first.Hash);
        second.Should().Be(first);
    }

    [Fact]
    public void CapturedRuntimeValue_ShouldNotChangeTheKey()
    {
        var one = KeyOf(WhereCaptured(Source(), 1));
        var two = KeyOf(WhereCaptured(Source(), 2));

        two.Hash.Should().Be(one.Hash);
        two.Should().Be(one);
    }

    [Fact]
    public void DifferentClosureSites_ShouldNotShareTheKey()
    {
        var first = KeyOf(WhereCaptured(Source(), 1));
        var second = KeyOf(WhereCapturedAtAnotherSite(Source(), 1));

        second.Should().NotBe(first);
    }

    [Fact]
    public void Branching_ShouldNotMutateTheSourceOrASibling()
    {
        var source = Source();
        var withWhere = source.Where(x => x.Id > 1);
        var withTake = source.Take(5);
        var twoConditions = withWhere.Where(x => x.Id < 10);

        source.State.Conditions.Should().BeNull();
        source.State.Paging.Limit.Should().Be(0);
        withWhere.State.Conditions.Should().HaveCount(1);
        withTake.State.Conditions.Should().BeNull();
        withTake.State.Paging.Limit.Should().Be(5);
        twoConditions.State.Conditions.Should().HaveCount(2);

        KeyOf(withWhere.State).Should().NotBe(KeyOf(twoConditions.State));
        KeyOf(withTake.State).Should().NotBe(KeyOf(withWhere.State));
    }

    [Fact]
    public void RepeatedKeyDerivation_ShouldNotMutateTheDescription()
    {
        var state = SimpleShape();
        var first = KeyOf(state);

        for (var i = 0; i < 16; i++)
        {
            KeyOf(state).Should().Be(first);
        }

        state.Conditions.Should().HaveCount(1);
    }

    [Fact]
    public void EverySqlAffectingDimension_ShouldChangeTheKey()
    {
        var shapes = new (string Name, RawQueryState State)[]
        {
            ("base", SimpleShape()),
            ("other entity", RawQueryFactory.From<OtherEntity>().Select(x => x.Id).State),
            ("table source", RawQueryFactory.FromTable("simple_entity").State),
            ("long projection", Source().Select(x => (long)x.Id).State),
            ("condition", Source().Where(x => x.Id > 1).State),
            ("second condition", Source().Where(x => x.Id > 1).Where(x => x.Id < 10).State),
            ("take", Source().Take(1).State),
            ("other take", Source().Take(2).State),
            ("skip", Source().Skip(1).State),
            ("distinct", Source().Select(x => x.Id).Distinct().State),
            ("order asc", Source().Select(x => x.Id).OrderBy(x => x).State),
            ("order desc", Source().Select(x => x.Id).OrderBy(x => x, OrderDirection.Desc).State),
            ("order nulls first", Source().Select(x => x.Id).OrderBy(x => x, OrderDirection.Asc, NullsPosition.First).State),
            ("tag", Source().Select(x => x.Id).State.WithTag("a")),
            ("other tag", Source().Select(x => x.Id).State.WithTag("b")),
            ("hints", Source().Select(x => x.Id).State.WithHints(["x"])),
            ("union", Source().Select(x => x.Id).Union(Source().Select(x => x.Id)).State),
            ("union all", Source().Select(x => x.Id).UnionAll(Source().Select(x => x.Id)).State),
            ("intersect", Source().Select(x => x.Id).Intersect(Source().Select(x => x.Id)).State),
            ("except", Source().Select(x => x.Id).Except(Source().Select(x => x.Id)).State),
            ("cte", RawQueryFactory.With("c", Source().Select(x => x.Id).State).From("c").State),
            ("cte other name", RawQueryFactory.With("d", Source().Select(x => x.Id).State).From("d").State),
            ("recursive cte", RawQueryFactory.WithRecursive("c", Source().Select(x => x.Id).State).From("c").State),
            ("recursive limit", RawQueryFactory.WithRecursive("c", Source().Select(x => x.Id).State, 3).From("c").State),
            ("join inner", Source().Join(Source(), (l, r) => l.Id == r.Id).Select(x => x.Id).State),
            ("join left", Source().Join(Source(), (l, r) => l.Id == r.Id, JoinType.Left).Select(x => x.Id).State),
            ("join cross", Source().Join(Source(), (l, r) => l.Id == r.Id, JoinType.Cross).Select(x => x.Id).State),
        };

        for (var i = 0; i < shapes.Length; i++)
        {
            for (var j = i + 1; j < shapes.Length; j++)
            {
                var left = KeyOf(shapes[i].State);
                var right = KeyOf(shapes[j].State);
                right.Should().NotBe(left,
                    $"{shapes[j].Name} must not share the key of {shapes[i].Name}");
            }
        }
    }

    [Fact]
    public void HashCollision_ShouldBeResolvedByStructure()
    {
        var first = new RawQueryKey(SimpleShape(), DefaultContext, hash: 42);
        var second = new RawQueryKey(Source().Take(5).State, DefaultContext, hash: 42);

        first.Hash.Should().Be(second.Hash);
        second.Should().NotBe(first);
    }

    [Fact]
    public void ExecutionIdentity_ShouldPartitionTheKey()
    {
        var state = SimpleShape();
        var scoped = state.WithFilterScope(QueryFilterScope.AllFilters);

        var cases = new (string Name, RawQueryState State, RawQueryContext Context)[]
        {
            ("default", state, DefaultContext),
            ("provider", state, DefaultContext with { ProviderType = typeof(string) }),
            ("quoting", state, DefaultContext with { QuoteIdentifiers = true }),
            ("naming", state, new RawQueryContext(typeof(object), false, SnakeCaseNamingConvention.Instance, KeywordCase.Lower, null)),
            ("keyword case", state, DefaultContext with { KeywordCase = KeywordCase.Upper }),
            ("timeout", state, DefaultContext with { CommandTimeout = 30 }),
            ("filter generation", state, DefaultContext with { FilterGeneration = 1 }),
            ("configuration generation", state, DefaultContext with { ConfigurationGeneration = 1 }),
            ("filter scope", scoped, DefaultContext),
        };

        var keys = cases.Select(entry => KeyOf(entry.State, entry.Context)).ToArray();

        for (var i = 0; i < keys.Length; i++)
        {
            for (var j = i + 1; j < keys.Length; j++)
            {
                keys[j].Should().NotBe(keys[i],
                    $"{cases[j].Name} must not share the key of {cases[i].Name}");
            }
        }
    }

    [Fact]
    public void SameExecutionIdentity_ShouldShareTheKey()
    {
        var context = new RawQueryContext(typeof(object), true, SnakeCaseNamingConvention.Instance, KeywordCase.Upper, 15, 2, 3);

        var first = KeyOf(SimpleShape(), context);
        var second = KeyOf(SimpleShape(), context with { });

        second.Should().Be(first);
    }

    [Fact]
    public void EmptyDescription_ShouldFailClosed()
    {
        RawQueryStateEquality.TryCreateKey(default, DefaultContext, out var key).Should().BeFalse();
        key.Should().Be(default(RawQueryKey));
    }
}
