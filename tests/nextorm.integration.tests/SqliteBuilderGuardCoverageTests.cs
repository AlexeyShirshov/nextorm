using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

/// <summary>
/// Builder-level guard coverage against a live SQLite database: the rejection chains of
/// <c>EntityBuilder</c> (eager loading, JoinInto, windows, alias root, source overrides) that the
/// existing sweeps do not walk. Every fact runs at least one real query through the inherited
/// <see cref="ProviderTestSuite"/> repository before asserting the guard, so a rejection is always
/// paired with proof that the provider is live and the surrounding shape is valid SQLite.
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteBuilderGuardCoverageTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    /// <summary>
    /// A row id no fixture of the shared SQLite database ever inserts, used to scope a live probe to
    /// rows this class owns: the eager_parent/eager_child tables are written by the eager-loading suite
    /// in the same file database, so an unfiltered emptiness assertion is order-dependent.
    /// </summary>
    private const int AbsentParentId = int.MinValue + 7;

    /// <summary>The seeded <c>simple_entity</c> row count, asserted to prove the live database.</summary>
    private void SeedIsLive() => _sut.SimpleEntityAsClass.Count().Should().Be(10);

    /// <summary>A real split-query eager load over the (empty) eager_parent/eager_child fixtures.</summary>
    private void EagerLoadIsExecutable(IDataContext ctx)
    {
        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == int.MinValue)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId)
            .ToList();

        parents.Should().BeEmpty();
    }

    [Fact]
    public void LoadWith_DuplicateCollectionMember_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        EagerLoadIsExecutable(ctx);

        var builder = ctx.From<EagerParent>()
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId);

        FluentActions
            .Invoking(() => builder.LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*already has an eager-load declaration*");
    }

    [Fact]
    public void LoadWith_UndefinedEagerLoadMode_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        EagerLoadIsExecutable(ctx);

        FluentActions
            .Invoking(() => ctx.From<EagerParent>()
                .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, (EagerLoadMode)999))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void LoadWith_ConflictingExplicitModes_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;

        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == int.MinValue)
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery)
            .ToList();
        parents.Should().BeEmpty();

        var builder = ctx.From<EagerParent>()
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SingleQuery);

        FluentActions
            .Invoking(() => builder.LoadWith(
                p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId, EagerLoadMode.SplitQuery))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*conflicts with it*");
    }

    [Fact]
    public void JoinInto_UnsupportedJoinKind_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        SeedIsLive();

        FluentActions
            .Invoking(() => ctx.From<EagerParent>().JoinInto(
                ctx.From<EagerChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Right))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*JoinInto supports only*");
    }

    [Fact]
    public void JoinInto_KeySelectorTypeMismatch_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        SeedIsLive();

        FluentActions
            .Invoking(() => ctx.From<EagerParent>().JoinInto<EagerChild, object>(
                ctx.From<EagerChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must select the same property type*");
    }

    [Fact]
    public void JoinInto_CombinedWithProjection_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;

        // Filtered to an id this fact owns: the eager_parent table is shared with the eager-loading
        // suite, so the live probe must not depend on the rows another test leaves behind. The joined
        // query still executes for real.
        var parents = ctx.From<EagerParent>()
            .Where(p => p.Id == AbsentParentId)
            .JoinInto(ctx.From<EagerChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId)
            .ToList();
        parents.Should().BeEmpty();

        var joined = ctx.From<EagerParent>()
            .JoinInto(ctx.From<EagerChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId);

        FluentActions
            .Invoking(() => joined.Select(x => x.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*JoinInto cannot be combined with Select*");
    }

    [Fact]
    public void LoadWith_CombinedWithProjection_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        EagerLoadIsExecutable(ctx);

        var eager = ctx.From<EagerParent>()
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId);

        FluentActions
            .Invoking(() => eager.Select(x => x.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*LoadWith cannot be combined with Select*");
    }

    [Fact]
    public void LoadWith_CombinedWithJoin_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        EagerLoadIsExecutable(ctx);

        var eager = ctx.From<EagerParent>()
            .LoadWith(p => p.Children, c => c.From<EagerChild>(), p => p.Id, c => c.ParentId);

        FluentActions
            .Invoking(() => eager.Join(ctx.From<EagerChild>(), (p, c) => p.Id == c.ParentId))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*LoadWith cannot be combined with Join/Apply*");
    }

    [Fact]
    public void Window_DeclaredBeforeJoin_ShouldBeRejected()
    {
        var ids = _sut.ComplexEntity
            .Window("w", partitionBy: [x => x.Int])
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("w") })
            .ToList();
        ids.Should().HaveCount(3);

        FluentActions
            .Invoking(() => _sut.ComplexEntity
                .Window("w", partitionBy: [x => x.Int])
                .Join(_sut.SimpleEntityAsClass, (c, s) => c.Id == s.Id))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Named windows must be declared after joins*");
    }

    [Fact]
    public void Distinct_AfterDistinctOn_ShouldBeRejected()
    {
        SeedIsLive();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.DistinctOn(x => x.Id).Distinct())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DISTINCT cannot be combined with DISTINCT ON*");
    }

    [Fact]
    public void DistinctOn_AfterDistinct_ShouldBeRejected()
    {
        _sut.SimpleEntityAsClass.Distinct().ToList().Should().HaveCount(10);

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.Distinct().DistinctOn(x => x.Id))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*DISTINCT ON cannot be combined with DISTINCT*");
    }

    [Fact]
    public void JoinInto_WithoutDeclaredRelationship_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        SeedIsLive();

        FluentActions
            .Invoking(() => ctx.From<GuardUnrelatedParent>()
                .JoinInto(ctx.From<GuardUnrelatedChild>(), (p, c) => p.Id == c.ParentId, p => p.Children))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires an explicitly declared relationship*");
    }

    [Fact]
    public void AliasRoot_AfterJoin_ShouldBeRejected()
    {
        SeedIsLive();

        var joined = _sut.SimpleEntityAsClass.Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id);

        FluentActions
            .Invoking(() => joined.AliasRoot<EntityBuilder<SimpleEntity>, SimpleEntity>(c => c.From<SimpleEntity>()))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*WithAlias names the root source*");
    }

    [Fact]
    public void WithTablesInScopeHint_WithoutAnyName_ShouldBeRejected()
    {
        SeedIsLive();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass.WithTablesInScopeHint("  ", string.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CorrelatedApply_AfterJoin_ShouldBeRejected_BecauseSqliteHasNoLateral()
    {
        var ctx = _sut.DataProvider;
        SeedIsLive();

        // #324: a correlated APPLY after a join binds to the flat transition and is refused by the dialect
        // capability gate (no lateral/APPLY source), not by the removed "join projection" refusal.
        var joined = _sut.SimpleEntityAsClass.Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id);

        var command = joined
            .CrossApply((Projection<SimpleEntity, IComplexEntity> p) =>
                ctx.From<SimpleEntity>().Where(x => x.Id == p.Item1.Id))
            .Select(q => new { Left = q.Item1.Id, Applied = q.Item3.Id });

        var exception = FluentActions
            .Invoking(() => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>("the refusal happens while the command is prepared")
            .Which;

        exception.Message.Should().Contain("CrossApply");
        exception.Message.Should().Contain("not supported by this SQL dialect");
        exception.Message.Should().NotContain(
            "join projection", "the left source being a projection is not the failure reason");
    }

    [Fact]
    public void CorrelatedApply_AfterJoin_Control_JoinedProjectionWithoutApply_ShouldExecute()
    {
        SeedIsLive();

        // Control for the negative above: the same joined projection without an APPLY is valid SQLite and
        // executes, so the refusal is the missing lateral capability and nothing about the projection shape.
        var joined = _sut.SimpleEntityAsClass.Join(_sut.ComplexEntity, (s, c) => s.Id == c.Id);

        var rows = joined
            .Select(p => new { Left = p.Item1.Id, Right = p.Item2.Id })
            .ToList();

        rows.Should().NotBeEmpty();
    }

    [Fact]
    public void WithTableExpression_WithNameOverride_ShouldBeRejected()
    {
        SeedIsLive();

        FluentActions
            .Invoking(() => _sut.SimpleEntityAsClass
                .WithTableExpression("(select id from simple_entity)")
                .WithTableName("simple_entity")
                .Select(x => x.Id)
                .ToList())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*WithTableExpression cannot be combined*");
    }

    [Fact]
    public void SourceOverride_OnDerivedQuerySource_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        SeedIsLive();

        // A derived query source is only accepted with an anonymous projection, so the DTO shape the
        // sibling fact uses cannot be the live counterpart here.
        var derived = ctx.From(_sut.SimpleEntityAsClass
            .Where(x => x.Id > 8)
            .Select(x => new { x.Id }));

        FluentActions
            .Invoking(() => derived.WithSchema("main").Select(x => x.Id).ToList())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*can only be applied to a physical table or mapped-entity source*");
    }

    [Fact]
    public void SourceOverride_OnUnregisteredSource_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;
        SeedIsLive();

        // A builder that never went through From<T>() carries no source and no metadata; the
        // override forces source resolution, which has nothing to resolve.
        var raw = new EntityBuilder<GuardUnregisteredProbe>(ctx);

        FluentActions
            .Invoking(() => raw.WithSchema("main").Select(x => x.Id).ToList())
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Table name is not registered*GuardUnregisteredProbe*");
    }

    [Fact]
    public void SelectMany_OnSqlProvider_ShouldBeRejected()
    {
        SeedIsLive();

        var values = new[] { 1, 2, 3 };

        FluentActions
            .Invoking(() => _sut.SimpleEntity.SelectMany(x => values))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*only available on the in-memory provider*");
    }

    [Fact]
    public void DerivedQuerySource_WithModifierBeforeJoin_ShouldBeRejected()
    {
        var ctx = _sut.DataProvider;

        var derivedRows = ctx.From(_sut.SimpleEntityAsClass
                .Where(x => x.Id > 8)
                .Select(x => new { x.Id }))
            .Select(x => new { x.Id })
            .ToList();
        derivedRows.Select(x => x.Id).Should().Equal(9, 10);

        var derived = ctx.From(_sut.SimpleEntityAsClass
            .Where(x => x.Id > 0)
            .Select(x => new { x.Id }));

        FluentActions
            .Invoking(() => derived.OrderBy(x => x.Id).Join(_sut.SimpleEntityAsClass, (d, s) => d.Id == s.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*supports only a Where clause before Join*");
    }

    /// <summary>Parent fixture with a collection navigation that has no declared relationship.</summary>
    [SqlTable("guard_unrelated_parent")]
    public sealed class GuardUnrelatedParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<GuardUnrelatedChild> Children { get; } = new List<GuardUnrelatedChild>();
    }

    /// <summary>Child fixture of <see cref="GuardUnrelatedParent"/>; the relationship is never declared.</summary>
    [SqlTable("guard_unrelated_child")]
    public sealed class GuardUnrelatedChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }
    }

    /// <summary>A CLR type that is never materialized, so the metadata cache has no table for it.</summary>
    public sealed class GuardUnregisteredProbe
    {
        public int Id { get; set; }
    }
}
