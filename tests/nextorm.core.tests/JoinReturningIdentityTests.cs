using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Provider-independent guards of the identity (whole-<c>Projection</c>) multi-table RETURNING form:
/// expansion to every returnable mapped property of every item slot, slot grouping, deterministic
/// alias allocation and the fail-closed guards (missing source, multi-column Range). No database is
/// involved; SQL generation is covered by the provider test projects.
/// </summary>
public class JoinReturningIdentityTests
{
    // --- A1: identity accepted for joined UPDATE and DELETE on arities 2..8 (representatives 2, 3, 8) ---

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void UpdateJoinIdentityReturning_ShouldBeAccepted(int arity)
    {
        using var ctx = new InMemoryDataContext();

        var act = () => BuildUpdate(ctx, arity);

        act.Should().NotThrow();
        act().Should().NotBeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void DeleteJoinIdentityReturning_ShouldBeAccepted(int arity)
    {
        using var ctx = new InMemoryDataContext();

        var act = () => BuildDelete(ctx, arity);

        act.Should().NotThrow();
        act().Should().NotBeNull();
    }

    // --- A2: Returning() is equivalent to Returning(p => p) for both operations and arities ---

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void Returning_ShouldEqualIdentityLambda_ForUpdate(int arity)
    {
        using var ctx = new InMemoryDataContext();

        var parameterless = BuildUpdate(ctx, arity, identityLambda: false);
        var lambda = BuildUpdate(ctx, arity, identityLambda: true);

        ColumnsOf(parameterless).Should().Equal(ColumnsOf(lambda));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void Returning_ShouldEqualIdentityLambda_ForDelete(int arity)
    {
        using var ctx = new InMemoryDataContext();

        var parameterless = BuildDelete(ctx, arity, identityLambda: false);
        var lambda = BuildDelete(ctx, arity, identityLambda: true);

        ColumnsOf(parameterless).Should().Equal(ColumnsOf(lambda));
    }

    // --- A3: identity expands every returnable mapped property of every item slot ---

    [Fact]
    public void IdentityParse_Arity2_ShouldExpandEverySlotProperty()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();

        var parsed = Parse<Projection<ConventionalEntity, ConventionalEntity>>();

        parsed.Columns.Should().HaveCount(4); // Id, Name per slot x 2 slots
        parsed.SelectList.Should().HaveCount(4);
        parsed.OneColumn.Should().BeFalse();
        parsed.SelectList.Select(c => c.PropertyName).Should().Equal("Id", "Name", "Id", "Name");
        parsed.SelectList.Select(c => c.ProjectionItem!.Slot).Should().Equal(0, 0, 1, 1);
        parsed.SelectList.Select(c => c.ProjectionItem!.EntityType).Should().OnlyContain(t => t == typeof(ConventionalEntity));
        parsed.SelectList.Select(c => c.ProjectionItem!.Member!.Name).Should().Equal("Item1", "Item1", "Item2", "Item2");
    }

    [Fact]
    public void IdentityParse_Arity3_ShouldExpandEverySlotProperty()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();

        var parsed = Parse<Projection<ConventionalEntity, ConventionalEntity, ConventionalEntity>>();

        parsed.Columns.Should().HaveCount(6);
        parsed.SelectList.Should().HaveCount(6);
        parsed.SelectList.Select(c => c.ProjectionItem!.Slot).Should().Equal(0, 0, 1, 1, 2, 2);
    }

    // --- A4: deterministic unique aliases across the flattened output ---

    [Fact]
    public void AllocateIdentityAliases_ShouldBePerSlotAndUnique()
    {
        var pairs = new (int Slot, string Column)[]
        {
            (0, "id"), (0, "name"), (1, "id"), (1, "name"),
        };

        var aliases = ProjectionAliasCache.AllocateIdentityAliases(pairs);

        aliases.Should().Equal("__s1_id", "__s1_name", "__s2_id", "__s2_name");
        aliases.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllocateIdentityAliases_ShouldDisambiguateCollisionsDeterministically()
    {
        // A repeated (slot, column) pair and a literal alias-shaped column name must never collapse two
        // entries onto one alias. The disambiguator is a pure function of the ordered pair list.
        var pairs = new (int Slot, string Column)[]
        {
            (0, "id"), (0, "id"), (0, "id_2"), (1, "id"),
        };

        var first = ProjectionAliasCache.AllocateIdentityAliases(pairs);
        var second = ProjectionAliasCache.AllocateIdentityAliases(pairs);

        first.Should().Equal(second);
        first.Should().OnlyHaveUniqueItems();
        first[0].Should().Be("__s1_id");
        first[1].Should().Be("__s1_id_2");
        first[2].Should().Be("__s1_id_2_2");
        first[3].Should().Be("__s2_id");
    }

    [Fact]
    public void IdentityParse_ShouldCopyConverterDurationAndProviderTypeMetadata()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<BuiltInEnumConverterEntity>();
        ctx.From<AttributedDurationEntity>();

        var parsed = Parse<Projection<BuiltInEnumConverterEntity, AttributedDurationEntity>>();

        var status = parsed.SelectList.Single(c => c.PropertyName == nameof(BuiltInEnumConverterEntity.Status));
        status.Converter.Should().NotBeNull();
        status.ProviderType.Should().Be(typeof(string));

        var span = parsed.SelectList.Single(c => c.PropertyName == nameof(AttributedDurationEntity.Span));
        span.DurationUnit.Should().Be(DurationUnit.Seconds);
        span.DurationPrecision.Should().Be(3);
    }

    // --- A5: a slot with no resolvable source member fails with slot + member info ---

    [Fact]
    public void IdentityParse_UnresolvableSlot_ShouldThrowWithSlotAndMember()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();

        var act = () => Parse<Projection<ConventionalEntity, OpaqueSlot>>();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*item 2*Item2*");
    }

    // --- A6: a multi-column Range anywhere in the identity result fails before SQL ---

    [Fact]
    public void IdentityParse_MultiColumnRange_ShouldThrow()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();
        ctx.From<RangeColumnEntity>();

        var act = () => Parse<Projection<ConventionalEntity, RangeColumnEntity>>();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*Range<T> column pair*RETURNING*");
    }

    [Fact]
    public void IdentityParse_MultiColumnRange_AtHigherArity_ShouldNameTheMember()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();
        ctx.From<RangeColumnEntity>();

        // The Range pair is reached at arity 3 (a higher arity than the arity-2 core case); the
        // diagnostic still names the offending member, not just the Range kind.
        var act = () => Parse<Projection<ConventionalEntity, ConventionalEntity, RangeColumnEntity>>();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*During*");
    }

    [Fact]
    public void IdentityParse_UnresolvableSlot_AtHigherArity_ShouldNameItem3()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();

        // The opaque slot sits at item 3, so the diagnostic must name the higher slot, not the first.
        var act = () => Parse<Projection<ConventionalEntity, ConventionalEntity, OpaqueSlot>>();

        act.Should().Throw<QueryPreparationException>()
            .WithMessage("*item 3*Item3*");
    }

    // --- A8: existing explicit forms keep their shape (identity is opt-in) ---

    [Fact]
    public void ExplicitProjection_ShouldKeepItsShape_NotExpandToIdentity()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ConventionalEntity>();

        var scalar = JoinedReturningProjection.Parse(
            (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, int>>)(p => p.Item1.Id));
        scalar.Columns.Should().HaveCount(1);
        scalar.OneColumn.Should().BeTrue();

        var anonymous = JoinedReturningProjection.Parse(
            (Expression<Func<Projection<ConventionalEntity, ConventionalEntity>, object>>)(p => new { TargetId = p.Item1.Id, SourceId = p.Item2.Id }));
        anonymous.Columns.Should().HaveCount(2);
        anonymous.OneColumn.Should().BeFalse();

        // Identity over the same projection is strictly larger: explicit forms did not change.
        Parse<Projection<ConventionalEntity, ConventionalEntity>>().Columns.Should().HaveCount(4);
    }

    // --- Fix 2: projection-item alias resolution stays fail-closed for non-identity sources ---

    [Fact]
    public void ProjectionItemAliasResolution_IdentityShape_ShouldFallBackToProjectionAlias()
    {
        using var ctx = new InMemoryDataContext();
        var columns = new DefaultColumnsProvider();

        var shape = ctx.CreateCommand<Projection<ConventionalEntity, ConventionalEntity>>(new QueryDefinition());
        shape.ResultType = typeof(Projection<ConventionalEntity, ConventionalEntity>);
        shape.IdentitySlotAliases = true;
        columns.Add(shape, fromProjection: false);

        var projectionParameter = Expression.Parameter(typeof(Projection<ConventionalEntity, ConventionalEntity>));

        var idx = MemberTranslator.ResolveProjectionItemAliasIndex(
            columns,
            typeof(ConventionalEntity),
            itemOccurrence: 0,
            projectionParameter,
            includeNestedSources: false);

        idx.Should().Be(0);
    }

    [Fact]
    public void ProjectionItemAliasResolution_OrdinaryAliasMiss_ShouldFailClosed()
    {
        using var ctx = new InMemoryDataContext();
        var columns = new DefaultColumnsProvider();

        // An ordinary (non-identity) source registers the projection type but not the item type. A
        // member access like p.Item1.Id must not silently resolve through the projection parameter:
        // it must keep the throwing behavior the pre-#143 path had.
        var shape = ctx.CreateCommand<Projection<ConventionalEntity, ConventionalEntity>>(new QueryDefinition());
        shape.ResultType = typeof(Projection<ConventionalEntity, ConventionalEntity>);
        shape.IdentitySlotAliases = false;
        columns.Add(shape, fromProjection: false);

        var projectionParameter = Expression.Parameter(typeof(Projection<ConventionalEntity, ConventionalEntity>));

        var act = () => MemberTranslator.ResolveProjectionItemAliasIndex(
            columns,
            typeof(ConventionalEntity),
            itemOccurrence: 0,
            projectionParameter,
            includeNestedSources: false);

        act.Should().Throw<InvalidOperationException>();
    }

    // --- helpers ---

    private static object BuildUpdate(IDataContext ctx, int arity, bool identityLambda = false)
    {
        object Build() => arity switch
        {
            2 => identityLambda ? Join2(ctx).UpdateJoin().Returning(p => p) : Join2(ctx).UpdateJoin().Returning(),
            3 => identityLambda ? Join3(ctx).UpdateJoin().Returning(p => p) : Join3(ctx).UpdateJoin().Returning(),
            8 => identityLambda ? Join8(ctx).UpdateJoin().Returning(p => p) : Join8(ctx).UpdateJoin().Returning(),
            _ => throw new ArgumentOutOfRangeException(nameof(arity)),
        };

        return Build();
    }

    private static object BuildDelete(IDataContext ctx, int arity, bool identityLambda = false)
    {
        object Build() => arity switch
        {
            2 => identityLambda ? Join2(ctx).Returning(p => p) : Join2(ctx).Returning(),
            3 => identityLambda ? Join3(ctx).Returning(p => p) : Join3(ctx).Returning(),
            8 => identityLambda ? Join8(ctx).Returning(p => p) : Join8(ctx).Returning(),
            _ => throw new ArgumentOutOfRangeException(nameof(arity)),
        };

        return Build();
    }

    private static JoinedEntityBuilder<ConventionalEntity, ConventionalEntity> Join2(IDataContext ctx)
        => ctx.From<ConventionalEntity>()
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Id == b.Id);

    private static JoinedEntityBuilder<ConventionalEntity, ConventionalEntity, ConventionalEntity> Join3(IDataContext ctx)
        => Join2(ctx)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id);

    private static JoinedEntityBuilder<ConventionalEntity, ConventionalEntity, ConventionalEntity, ConventionalEntity, ConventionalEntity, ConventionalEntity, ConventionalEntity, ConventionalEntity> Join8(IDataContext ctx)
        => Join3(ctx)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id)
            .Join(ctx.From<ConventionalEntity>(), (a, b) => a.Item1.Id == b.Id);

    private static (IReadOnlyList<IPropertyMetadata> Columns, SelectExpression[] SelectList, bool OneColumn) Parse<TProjection>()
        where TProjection : class
    {
        Expression<Func<TProjection, TProjection>> identity = p => p;
        return JoinedReturningProjection.Parse(identity);
    }

    private static IReadOnlyList<IPropertyMetadata> ColumnsOf(object returningBuilder)
    {
        var property = returningBuilder.GetType().GetProperty("ReturningColumns", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return (IReadOnlyList<IPropertyMetadata>)property!.GetValue(returningBuilder)!;
    }

    internal sealed class OpaqueSlot
    {
        internal int Hidden { get; set; }
    }
}
