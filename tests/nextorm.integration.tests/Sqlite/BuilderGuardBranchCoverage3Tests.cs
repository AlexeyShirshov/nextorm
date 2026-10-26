using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Builder guard/validation branch coverage (cycle 3, D7) against a live SQLite database. Every fact
/// drives a public <c>EntityBuilder</c> entry point that fails closed, asserts the exact exception type
/// and message fragment, and is paired with a live counterpart of the same builder shape that executes
/// and returns rows over the class-owned <c>bg3_*</c> tables (created, seeded and dropped by this class).
///
/// <para>
/// Targeted surface: <c>src/nextorm.core/Builders/EntityBuilder.cs</c> -
/// <c>OrderBy(int)</c>:4142, <c>ResolveJoinIntoRelationship</c>:947/:965/:988,
/// <c>ResolveJoinIntoReferenceRelationship</c>:1045/:1073/:1082, <c>JoinApply</c>:3622,
/// <c>JoinAliasApply</c>:3122, <c>JoinCore(QueryCommand)</c>:3675, <c>AliasRoot</c>:3300/:3290,
/// <c>CreateJoined</c>:3653 and <c>ResolveJoinBase</c>:3715.
/// </para>
///
/// <para>
/// Overlap avoided (read and deliberately not repeated):
/// <c>Sqlite/SqliteBuilderGuardCoverageTests.cs</c> owns the LoadWith/eager-mode guards, the JoinInto
/// kind/declared-relationship/key-type guards, the <c>Select</c>/<c>Join</c> eager-load combinations,
/// <c>Distinct</c>/<c>DistinctOn</c>, the <c>Window before Join</c> form of <c>:2868</c>,
/// <c>AliasRoot after Join</c> (<c>:3286</c>), <c>WithTablesInScopeHint</c>, the <c>Apply after join</c>
/// projection guard (<c>:3114</c>), the <c>WithTableExpression</c> override guard and the
/// source-override/derived-source guards;
/// <c>Sqlite/SqlitePreparationShapeCoverageTests.cs</c> owns the projection/preparation shapes;
/// <c>Sqlite/SqlitePlanCacheShapeCoverageTests.cs</c> owns the plan-key separation;
/// <c>Sqlite/BuilderValidationSweepTests.cs</c> owns the paging guards and <c>QueryCommand.OrderBy(0)</c>
/// (this class drives the <b>builder-level</b> <c>OrderBy(0)</c> instead);
/// <c>Sqlite/RawSourceBindingGuardTests.cs</c> owns the whole <c>BindEntity</c> chain
/// (<c>:2407</c>/<c>:2413</c>/<c>:2416</c>).
/// </para>
///
/// <para>
/// The three <c>JoinInto</c> relationship-resolution guards that the fluent/attribute surface cannot
/// express (kind mismatch on a collection, a many-to-many without a junction, a kind mismatch on a
/// reference navigation and a reference navigation without a setter) are driven by injecting the
/// relationship metadata into <see cref="DataContextCache"/> for this class's own probe types - the same
/// pattern as <c>Support/JoinIntoCompositeKeyFixtures.cs</c>, which also documents why the fluent surface
/// cannot declare those shapes.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class BuilderGuardBranchCoverage3Tests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        drop table if exists bg3_single;
        drop table if exists bg3_child;
        drop table if exists bg3_parent;
        create table bg3_parent
        (
            id integer primary key,
            name text not null
        );
        create table bg3_child
        (
            id integer primary key,
            parent_id int not null,
            name text not null
        );
        create table bg3_single
        (
            id integer primary key,
            parent_id int not null,
            name text not null
        );
        insert into bg3_parent (id, name) values (1, 'one'), (2, 'two'), (3, 'three');
        insert into bg3_child (id, parent_id, name) values (11, 1, 'c11'), (12, 1, 'c12'), (13, 2, 'c13');
        insert into bg3_single (id, parent_id, name) values (21, 1, 's21'), (22, 2, 's22');
        drop table if exists bg10_merge;
        drop table if exists bg10_key;
        drop table if exists bg10_composite;
        drop table if exists bg10_only_identity;
        drop table if exists bg10_insert;
        drop table if exists bg10_dynamic;
        drop table if exists bg10_range;
        drop table if exists bg10_filtered;
        create table bg10_merge
        (
            id integer primary key,
            name text not null,
            qty int not null,
            doubled int not null default 0
        );
        insert into bg10_merge (id, name, qty, doubled) values (1, 'one', 10, 20), (2, 'two', 20, 40);
        create table bg10_key
        (
            code text primary key,
            name text not null
        );
        create table bg10_composite
        (
            a text not null,
            b text not null,
            primary key (a, b)
        );
        create table bg10_only_identity
        (
            id integer primary key autoincrement
        );
        create table bg10_insert
        (
            id integer primary key autoincrement,
            name text not null,
            doubled int not null default 0
        );
        create table bg10_dynamic
        (
            id integer primary key,
            name text not null,
            extra text
        );
        insert into bg10_dynamic (id, name, extra) values (1, 'dyn', 'captured');
        create table bg10_range
        (
            id integer primary key,
            during_lower int,
            during_upper int
        );
        insert into bg10_range (id, during_lower, during_upper) values (1, 1, 5);
        create table bg10_filtered
        (
            id integer primary key,
            name text not null
        );
        insert into bg10_filtered (id, name) values (1, 'f1'), (2, 'f2');
        """;

    public BuilderGuardBranchCoverage3Tests()
    {
        // The DDL drops, creates and seeds the class-owned marker tables, so every test of the class
        // starts from its own schema and no state leaks between the facts.
        ExecuteRaw(Ddl);
    }

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // Live counterparts (each guard fact is paired with one of these and asserts real rows)
    // ---------------------------------------------------------------------------------------------

    /// <summary>The seeded parents with their joined children (the live <c>JoinInto</c> shape).</summary>
    private List<Bg3Parent> CollectionJoinIsLive()
    {
        var parents = Context.From<Bg3Parent>()
            .Where(p => p.Id <= 2)
            .JoinInto(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList()
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(2);
        parents[0].Children.Select(c => c.Id).Should().Equal(11, 12);
        parents[1].Children.Select(c => c.Id).Should().Equal(13);
        return parents;
    }

    /// <summary>The seeded parents with their single joined child (the live reference <c>JoinInto</c> shape).</summary>
    private List<Bg3Parent> ReferenceJoinIsLive()
    {
        var parents = Context.From<Bg3Parent>()
            .Where(p => p.Id <= 2)
            .JoinInto(Context.From<Bg3Single>(), (p, c) => p.Id == c.ParentId, p => p.Single)
            .ToList()
            .OrderBy(p => p.Id)
            .ToList();

        parents.Should().HaveCount(2);
        parents[0].Single!.Name.Should().Be("s21");
        parents[1].Single!.Name.Should().Be("s22");
        return parents;
    }

    /// <summary>The live named-window query: <c>row_number() over (partition by id)</c> over the marker rows.</summary>
    private void WindowIsLive()
    {
        var rows = Context.From<Bg3Parent>()
            .Window("bg3_w", partitionBy: [x => x.Id])
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Row = SqlFunctions.Sql.row_number().Over("bg3_w") })
            .ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Row).Should().Equal(1, 1, 1);
    }

    /// <summary>The live two-table join projection over which the derived-source guards are asserted.</summary>
    private EntityBuilder<Projection<Bg3Parent, Bg3Child>> JoinProjectionIsLive()
    {
        var joined = JoinProjection();
        var rows = joined.Select(x => new { x.Item1.Id, x.Item2.Name }).ToList();

        rows.Should().HaveCount(3);
        rows.Select(r => r.Id).Should().Equal(1, 1, 2);
        return joined;
    }

    private EntityBuilder<Projection<Bg3Parent, Bg3Child>> JoinProjection()
        => Context.From<Bg3Parent>().Join(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId);

    // ---------------------------------------------------------------------------------------------
    // 1. Builder-level ordinal ordering: the column index must be positive
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OrderBy_NonPositiveColumnIndex_ShouldBeRejected()
    {
        var live = Context.From<Bg3Parent>().OrderBy(1).Select(x => x.Id).ToList();
        live.Should().Equal(1, 2, 3);

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>().OrderBy(0))
            .Should().Throw<ArgumentException>()
            .WithMessage("*Column index must be greater than zero*");
    }

    // ---------------------------------------------------------------------------------------------
    // 2. JoinInto collection selector must be a property access
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_CollectionSelectorNotAPropertyAccess_ShouldBeRejected()
    {
        RegisterBg3Parent();
        CollectionJoinIsLive();

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .JoinInto(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId, p => WrapCollection(p.Children)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires the collection selector to be a property access*");
    }

    // ---------------------------------------------------------------------------------------------
    // 3. JoinInto reference selector must be a property access
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_NavigationSelectorNotAPropertyAccess_ShouldBeRejected()
    {
        RegisterBg3Parent();
        ReferenceJoinIsLive();

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .JoinInto(Context.From<Bg3Single>(), (p, c) => p.Id == c.ParentId, p => WrapNavigation(p.Single)))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*requires the navigation selector to be a property access*");
    }

    // ---------------------------------------------------------------------------------------------
    // 4. A collection JoinInto whose declared relationship is many-to-many without a junction
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_CollectionManyToManyWithoutJunction_ShouldBeRejected()
    {
        RegisterBg3Parent();
        CollectionJoinIsLive();

        RegisterProbe<Bg3ManyToManyProbe>(
            nameof(Bg3ManyToManyProbe.Children), RelationshipKind.ManyToMany, typeof(Bg3Child), isCollection: true);

        FluentActions
            .Invoking(() => Context.From<Bg3ManyToManyProbe>()
                .JoinInto(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId, p => p.Children))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*has no junction metadata*");
    }

    // ---------------------------------------------------------------------------------------------
    // 5. A collection JoinInto whose declared relationship is one-to-one
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_CollectionRelationshipDeclaredOneToOne_ShouldBeRejected()
    {
        RegisterBg3Parent();
        CollectionJoinIsLive();

        RegisterProbe<Bg3CollectionOneToOneProbe>(
            nameof(Bg3CollectionOneToOneProbe.Children), RelationshipKind.OneToOne, typeof(Bg3Child), isCollection: true);

        FluentActions
            .Invoking(() => Context.From<Bg3CollectionOneToOneProbe>()
                .JoinInto(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId, p => p.Children))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*expects a OneToMany relationship*");
    }

    // ---------------------------------------------------------------------------------------------
    // 6. A reference JoinInto whose declared relationship is one-to-many
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_ReferenceRelationshipDeclaredOneToMany_ShouldBeRejected()
    {
        RegisterBg3Parent();
        ReferenceJoinIsLive();

        RegisterProbe<Bg3ReferenceOneToManyProbe>(
            nameof(Bg3ReferenceOneToManyProbe.Single), RelationshipKind.OneToMany, typeof(Bg3Single), isCollection: false);

        FluentActions
            .Invoking(() => Context.From<Bg3ReferenceOneToManyProbe>()
                .JoinInto(Context.From<Bg3Single>(), (p, c) => p.Id == c.ParentId, p => p.Single))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*reference navigation supports only a OneToOne relationship*");
    }

    // ---------------------------------------------------------------------------------------------
    // 7. A reference JoinInto whose navigation has no setter
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinInto_ReferenceNavigationWithoutSetter_ShouldBeRejected()
    {
        RegisterBg3Parent();
        ReferenceJoinIsLive();

        RegisterProbe<Bg3NoSetterProbe>(
            nameof(Bg3NoSetterProbe.Single), RelationshipKind.OneToOne, typeof(Bg3Single), isCollection: false);

        FluentActions
            .Invoking(() => Context.From<Bg3NoSetterProbe>()
                .JoinInto(Context.From<Bg3Single>(), (p, c) => p.Id == c.ParentId, p => p.Single))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*because it has no setter*");
    }

    // ---------------------------------------------------------------------------------------------
    // 8. A correlated APPLY cannot follow a named-window declaration
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void OuterApply_AfterWindowDeclaration_ShouldBeRejected()
    {
        WindowIsLive();

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .Window("bg3_w", partitionBy: [x => x.Id])
                .OuterApply<Bg3Child>(x => Context.From<Bg3Child>()))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Named windows must be declared after joins*");
    }

    // ---------------------------------------------------------------------------------------------
    // 9. The alias APPLY seam rejects a windowed builder as well
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void JoinAliasApply_AfterWindowDeclaration_ShouldBeRejected()
    {
        WindowIsLive();

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .Window("bg3_w", partitionBy: [x => x.Id])
                .JoinAlias<EntityBuilder<Bg3Parent>, Bg3Parent, Bg3Child>(
                    c => c.From<Bg3Parent>(),
                    x => Context.From<Bg3Child>(),
                    JoinType.CrossApply))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Named windows must be declared after joins*");
    }

    // ---------------------------------------------------------------------------------------------
    // 10. A join over a derived command rejects a windowed builder too
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Join_WithDerivedCommandAfterWindowDeclaration_ShouldBeRejected()
    {
        var live = Context.From<Bg3Parent>()
            .Join(Context.From<Bg3Child>().ToCommand(), (p, c) => p.Id == c.ParentId)
            .Select(x => new { x.Item1.Id, x.Item2.Name })
            .ToList();
        live.Should().HaveCount(3);

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .Window("bg3_w", partitionBy: [x => x.Id])
                .Join(Context.From<Bg3Child>().ToCommand(), (p, c) => p.Id == c.ParentId))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Named windows must be declared after joins*");
    }

    // ---------------------------------------------------------------------------------------------
    // 11. AliasRoot cannot follow a named-window declaration
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AliasRoot_WithWindowDeclaration_ShouldBeRejected()
    {
        WindowIsLive();

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .Window("bg3_w", partitionBy: [x => x.Id])
                .AliasRoot<EntityBuilder<Bg3Parent>, Bg3Parent>(c => c.From<Bg3Parent>()))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Named windows must be declared after joins*");
    }

    // ---------------------------------------------------------------------------------------------
    // 12. AliasRoot must not be applied to a join/alias projection source
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AliasRoot_OnDerivedProjectionSource_ShouldBeRejected()
    {
        var joined = JoinProjectionIsLive();
        var derived = Context.From(joined);

        FluentActions
            .Invoking(() => derived.AliasRoot<EntityBuilder<Bg3Parent>, Bg3Parent>(c => c.From<Bg3Parent>()))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*WithAlias must be applied to a plain root source*");
    }

    // ---------------------------------------------------------------------------------------------
    // 13. A positional join cannot be combined with a JoinInto declaration
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Join_AfterJoinInto_ShouldBeRejected()
    {
        RegisterBg3Parent();

        var live = Context.From<Bg3Parent>()
            .Where(p => p.Id == 1)
            .JoinInto(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
            .ToList();
        live.Should().ContainSingle();
        live[0].Children.Should().HaveCount(2);

        FluentActions
            .Invoking(() => Context.From<Bg3Parent>()
                .JoinInto(Context.From<Bg3Child>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .Join(Context.From<Bg3Single>(), (p, s) => p.Id == s.ParentId))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*JoinInto cannot be combined with other joins*");
    }

    // ---------------------------------------------------------------------------------------------
    // 14. A derived join projection source can be joined only once
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Join_OnDerivedProjectionSource_ShouldBeRejected()
    {
        var joined = JoinProjectionIsLive();
        var derived = Context.From(joined);

        FluentActions
            .Invoking(() => derived.Join(Context.From<Bg3Parent>(), (p, s) => p.Item1.Id == s.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*can be joined only once*");
    }

    // ---------------------------------------------------------------------------------------------
    // D10-02: merge / insert / metadata guard arms. Every fact drives a public entry point against
    // the class-owned bg10_* tables, asserts the exact exception type and message fragment, and is
    // paired with a live counterpart of the same builder shape that executes SQL.
    // ---------------------------------------------------------------------------------------------

    /// <summary>The live key-upsert counterpart: one row is upserted and its value is read back.</summary>
    private void MergeUpsertIsLive()
    {
        Context.CreateMergeBuilder<Bg10MergeRow>()
            .Using(new Bg10MergeRow { Id = 1, Name = "one-upserted", Qty = 11 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge()
            .Should().BeGreaterThan(0);

        Context.From<Bg10MergeRow>()
            .Where(x => x.Id == 1)
            .Select(x => x.Name)
            .Single()
            .Should().Be("one-upserted");
    }

    [Fact]
    public void Merge_ActiveTargetFilterWithoutMergeStatement_ShouldBeRejected()
    {
        Context.CreateMergeBuilder<Bg10FilteredRow>()
            .IgnoreFilters()
            .Using(new Bg10FilteredRow { Id = 1, Name = "filtered-updated" })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .Merge()
            .Should().BeGreaterThan(0);

        Context.From<Bg10FilteredRow>()
            .IgnoreFilters()
            .Where(x => x.Id == 1)
            .Select(x => x.Name)
            .Single()
            .Should().Be("filtered-updated");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10FilteredRow>()
                .Using(new Bg10FilteredRow { Id = 2, Name = "two" })
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .Merge())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*active global query filter cannot isolate the write target*");
    }

    [Fact]
    public void Merge_SourceShape_ShouldBeRejected()
    {
        MergeUpsertIsLive();

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
                .Using(new Bg10MergeRow { Id = 2, Name = "b", Qty = 2 }))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*Using can only be specified once per merge*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>().OnKeys().Merge())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*No source rows were specified*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(Context.From<Bg10MergeRow>())
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .Merge())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*query source requires the full-MERGE branch form*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
                .OnKeys()
                .Merge())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*key upsert requires both*");
    }

    [Fact]
    public void Merge_MatchConditionExclusivity_ShouldBeRejected()
    {
        MergeUpsertIsLive();

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .On((t, s) => t.Id == s.Id)
                .OnKeys())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*OnKeys can only be specified once*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10NoKeyRow>().OnKeys())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*has no key property*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .OnKeys()
                .On((t, s) => t.Id == s.Id))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*On can only be specified once; OnKeys was already called*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .On((t, s) => t.Id == s.Id)
                .On((t, s) => t.Id == s.Id))
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*On can only be specified once*");
    }

    [Fact]
    public void Merge_KeyUpsertWithoutNonKeyColumn_ShouldBeRejected()
    {
        MergeUpsertIsLive();

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10CompositeKeyRow>()
                .Using(new Bg10CompositeKeyRow { A = "a", B = "b" })
                .OnKeys()
                .WhenMatchedUpdate()
                .WhenNotMatchedInsert()
                .Merge())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*has only key columns*");
    }

    [Fact]
    public void Merge_ExplicitBranchColumns_ShouldBeRejected()
    {
        MergeUpsertIsLive();

        // The conditional branch entry points (:465, :484) are pure factory calls; they are driven on a
        // discarded builder because SQLite renders the key-upsert form only.
        _ = Context.CreateMergeBuilder<Bg10MergeRow>()
            .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
            .OnKeys()
            .WhenMatched((t, s) => t.Qty < s.Qty).ThenDoNothing()
            .WhenNotMatched((t, s) => s.Qty > 0).ThenDoNothing();

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
                .WhenMatched().ThenUpdate((t, s) => new object()))
            .Should().Throw<ArgumentException>()
            .WithMessage("*MERGE assignments must be a non-empty anonymous initializer*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
                .WhenMatched().ThenUpdate((t, s) => new { Extra = s.Extra }))
            .Should().Throw<ArgumentException>()
            .WithMessage("*is not a mapped CLR property*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
                .WhenMatched().ThenUpdate((t, s) => new { Qty = s.Name }))
            .Should().Throw<ArgumentException>()
            .WithMessage("*has incompatible value type*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10MergeRow>()
                .Using(new Bg10MergeRow { Id = 1, Name = "a", Qty = 1 })
                .WhenMatched().ThenUpdate((t, s) => new { Doubled = t.Qty }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot assign computed property 'Doubled'*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10InsertTarget>()
                .Using(new Bg10InsertTarget { Name = "a" })
                .WhenMatched().ThenUpdate((t, s) => new { Id = t.Id }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot assign identity property 'Id'*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10KeyRow>()
                .Using(new Bg10KeyRow { Code = "c", Name = "a" })
                .WhenMatched().ThenUpdate((t, s) => new { Code = t.Code }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*cannot assign merge key property 'Code'*");
    }

    [Fact]
    public void Merge_BranchWithoutSelectableColumn_ShouldBeRejected()
    {
        MergeUpsertIsLive();

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10OnlyIdentity>()
                .Using(Context.From<Bg10OnlyIdentity>())
                .WhenMatched().ThenUpdate())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*has no column to update*");

        FluentActions
            .Invoking(() => Context.CreateMergeBuilder<Bg10OnlyIdentity>()
                .Using(Context.From<Bg10OnlyIdentity>())
                .WhenNotMatched().ThenInsert())
            .Should().Throw<NotSupportedException>()
            .WithMessage("*has no column to insert*");
    }

    [Fact]
    public void InsertSelect_InvalidMapping_ShouldBeRejected()
    {
        var source = Context.From<Bg10MergeRow>();

        Context.CreateInsertBuilder<Bg10InsertTarget>()
            .Values(source, s => new { Name = s.Name })
            .Insert()
            .Should().Be(2);

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(source, s => new { Name = SqlDefault.Value }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*DEFAULT is not valid in an INSERT ... SELECT source*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(source, s => new { Unknown = s.Name }))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Property Unknown*is not mapped*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(source, s => new { Doubled = s.Qty }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*Property Doubled*is computed and cannot be written*");
    }

    [Fact]
    public void InsertValues_MappingAndSelectorShape_ShouldBeRejected()
    {
        Context.CreateInsertBuilder<Bg10InsertTarget>()
            .Values(x => x.Name, new[] { "n1", "n2" })
            .Insert()
            .Should().Be(2);

        var source = new[] { new Bg10MergeRow { Id = 1, Name = "s", Qty = 1 } };

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10BindingRow>()
                .Values(source, s => new Bg10BindingRow { Meta = { Tag = s.Name } }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*may only assign mapped properties*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(source, s => s.Name))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be an object initializer*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(x => x.Name.ToUpper(), new[] { "a" }))
            .Should().Throw<ArgumentException>()
            .WithMessage("*column selector must select a mapped property*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(x => x.Extra, new[] { "a" }))
            .Should().Throw<BuildSqlCommandException>()
            .WithMessage("*Property Extra*is not mapped*");

        FluentActions
            .Invoking(() => Context.CreateInsertBuilder<Bg10InsertTarget>()
                .Values(x => x.Doubled, new[] { 1 }))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*is computed and cannot be written*");
    }

    [Fact]
    public void QueryFilterAttribute_FormGuards_ShouldBeRejected()
    {
        Context.From<Bg10FilterValid>().Select(x => x.Id).ToList().Should().Equal(2);

        FluentActions
            .Invoking(() => Context.From<Bg10FilterBoth>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must set either*not both*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterNeither>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must name a static member*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterDuplicateKey>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*declares more than one*");
    }

    [Fact]
    public void QueryFilterAttribute_LambdaMemberGuards_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => Context.From<Bg10FilterLambdaMissing>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*was not found*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterLambdaBadKind>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a static field, a static property or a parameterless static method*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterLambdaBadReturn>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a LambdaExpression*");
    }

    [Fact]
    public void QueryFilterAttribute_FuncMemberGuards_ShouldBeRejected()
    {
        FluentActions
            .Invoking(() => Context.From<Bg10FilterFuncMissing>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*was not found*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterFuncBadKind>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a static field, a static property or a parameterless static method*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterFuncBadReturn>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Delegate*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterShapeNonGeneric>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterShapeFirstNotGeneric>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterShapeFirstValue>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterShapeSecondArg>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterShapeResult>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>*");

        FluentActions
            .Invoking(() => Context.From<Bg10FilterShapeGenericDefinition>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must return a Func<EntityBuilder<TEntity>, IDataContext, EntityBuilder<TEntity>>*");
    }

    [Fact]
    public void DynamicColumnsStore_ShapeGuards_ShouldBeRejected()
    {
        var row = Context.From<Bg10ValidStore>().ToList().Single();
        row.Extras.Should().ContainKey("extra").WhoseValue.Should().Be("captured");

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10TwoStores>().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one dynamic-columns store*");

        FluentActions
            .Invoking(() => FluentNameBuilder().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one dynamic-columns store*");

        FluentActions
            .Invoking(() => TwoFluentStores().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one dynamic-columns store*");

        FluentActions
            .Invoking(() => FluentStoreBuilder().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot also have a fluent column mapping*");
    }

    [Fact]
    public void DynamicColumnsStore_ValidationGuards_ShouldBeRejected()
    {
        Context.From<Bg10ValidStore>().Select(x => x.Id).ToList().Should().Equal(1);

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10StructStore>().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*not supported on the value-type entity*");

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10NoSetterStore>().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must have a public setter*");

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10WrongStoreType>().Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*must be a string-keyed dictionary*");
    }

    [Fact]
    public void Metadata_ConverterAndRangeConflicts_ShouldBeRejected()
    {
        Context.From<Bg10ValidRange>().Select(x => x.Id).ToList().Should().Equal(1);

        FluentActions
            .Invoking(() => Context.From<Bg10ConverterConflict>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be mapped with both*ValueConverterAttribute*JsonColumnAttribute*");

        FluentActions
            .Invoking(() => Context.From<Bg10RangeWithConverter>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be mapped with both*RangeColumnsAttribute*converter*");

        FluentActions
            .Invoking(() => Context.From<Bg10RangeOnInt>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*is not Range<T>*");

        FluentActions
            .Invoking(() => Context.From<Bg10RangeWithColumn>())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be mapped with both*ColumnAttribute*RangeColumnsAttribute*");
    }

    [Fact]
    public void Relationship_NavigationShapeGuards_ShouldBeRejected()
    {
        new EntityMetadataBuilder<Bg10NavParent>()
            .HasMany<Bg3Child, int>(p => p.Children, c => c.ParentId)
            .Build()
            .Relationships
            .Should().ContainSingle();

        Context.From<Bg10NavParent>().Select(x => x.Id).ToList().Should().HaveCount(3);

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10NavParent>()
                .HasMany<Bg3Child, int>(p => (ICollection<Bg3Child>)p.Any, c => c.ParentId))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be a collection of*");

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10NavParent>()
                .HasOne<string, int>(p => p.Name, p => p.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be a reference-typed property*");

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10NavParent>()
                .HasOneToOne<string, int>(p => p.Name, p => p.Id, c => c.Length))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be a reference-typed property*");

        FluentActions
            .Invoking(() => new EntityMetadataBuilder<Bg10NavParent>()
                .HasManyThrough<Bg3Child, Bg3Single, int, int>(
                    p => (IEnumerable<Bg3Child>)p.Any, p => p.Id, j => j.Id, c => c.Id, j => j.Id))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*must be a collection of*");
    }

    private static EntityMetadataBuilder<Bg10TwoStores> TwoFluentStores()
    {
        var builder = new EntityMetadataBuilder<Bg10TwoStores>();
        builder.Property(x => x.First).DynamicColumnsStore();
        builder.Property(x => x.Second).DynamicColumnsStore();
        return builder;
    }

    private static EntityMetadataBuilder<Bg10TwoStores> FluentNameBuilder()
    {
        var builder = new EntityMetadataBuilder<Bg10TwoStores>();
        builder.Property(x => x.Name);
        return builder;
    }

    private static EntityMetadataBuilder<Bg10StoreWithFluent> FluentStoreBuilder()
    {
        var builder = new EntityMetadataBuilder<Bg10StoreWithFluent>();
        builder.Property(x => x.Store);
        return builder;
    }

    // ---------------------------------------------------------------------------------------------
    // Marker table / metadata plumbing
    // ---------------------------------------------------------------------------------------------

    private void ExecuteRaw(string sql)
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static ICollection<Bg3Child> WrapCollection(ICollection<Bg3Child> children) => children;

    private static Bg3Single? WrapNavigation(Bg3Single? single) => single;

    private static IPropertyMetadata FindProperty(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.Single(p => p.PropertyInfo.Name == name);

    /// <summary>
    /// Registers the class-owned <see cref="Bg3Parent"/> mapping with the two live relationships the
    /// fluent surface can declare, so a guarded shape always has a live counterpart to compare against.
    /// The registration is process-wide (like <c>From&lt;T&gt;(cfg)</c>) and replaces any convention
    /// mapping of this class-owned type.
    /// </summary>
    private static void RegisterBg3Parent()
    {
        var built = new EntityMetadataBuilder<Bg3Parent>().Build();
        var parentProps = built.Properties;
        var childProps = new EntityMetadataBuilder<Bg3Child>().Build().Properties;
        var singleProps = new EntityMetadataBuilder<Bg3Single>().Build().Properties;
        var children = typeof(Bg3Parent).GetProperty(nameof(Bg3Parent.Children))!;
        var single = typeof(Bg3Parent).GetProperty(nameof(Bg3Parent.Single))!;

        DataContextCache.Metadata[typeof(Bg3Parent)] = new EntityMetadata(
            built.TableName,
            built.Properties.Where(p => p.PropertyInfo != children && p.PropertyInfo != single).ToList(),
            built.IsTableNameAuto,
            relationships:
            [
                new InjectedRelationship(
                    RelationshipKind.OneToMany,
                    typeof(Bg3Parent),
                    typeof(Bg3Child),
                    children,
                    isCollection: true,
                    foreignKey: [FindProperty(childProps, nameof(Bg3Child.ParentId))],
                    principalKey: [FindProperty(parentProps, nameof(Bg3Parent.Id))]),
                new InjectedRelationship(
                    RelationshipKind.OneToOne,
                    typeof(Bg3Parent),
                    typeof(Bg3Single),
                    single,
                    isCollection: false,
                    foreignKey: [FindProperty(singleProps, nameof(Bg3Single.ParentId))],
                    principalKey: [FindProperty(parentProps, nameof(Bg3Parent.Id))]),
            ]);
    }

    /// <summary>
    /// Replaces the mapping of a class-owned probe type with exactly one injected relationship, so a
    /// guard shape the fluent/attribute surface cannot express (kind mismatch, missing junction,
    /// setter-less navigation) can be driven through the public <c>JoinInto</c> overloads.
    /// </summary>
    private static void RegisterProbe<TEntity>(
        string navigationName, RelationshipKind kind, Type relatedType, bool isCollection)
    {
        var built = new EntityMetadataBuilder<TEntity>().Build();
        var parentProps = built.Properties;
        var childProps = new EntityMetadataBuilder<Bg3Child>().Build().Properties;
        var navigation = typeof(TEntity).GetProperty(navigationName)!;

        DataContextCache.Metadata[typeof(TEntity)] = new EntityMetadata(
            built.TableName,
            built.Properties.Where(p => p.PropertyInfo != navigation).ToList(),
            built.IsTableNameAuto,
            relationships:
            [
                new InjectedRelationship(
                    kind,
                    typeof(TEntity),
                    relatedType,
                    navigation,
                    isCollection,
                    foreignKey: [FindProperty(childProps, nameof(Bg3Child.ParentId))],
                    principalKey: [FindProperty(parentProps, nameof(Bg3Parent.Id))]),
            ]);
    }

    /// <summary>
    /// Relationship metadata with an arbitrary kind/related type/navigation shape, injected through
    /// <see cref="DataContextCache"/> exactly like <c>Support/JoinIntoCompositeKeyFixtures.cs</c>.
    /// </summary>
    private sealed class InjectedRelationship : IRelationshipMetadata
    {
        public InjectedRelationship(
            RelationshipKind kind,
            Type declaringType,
            Type relatedType,
            PropertyInfo? navigation,
            bool isCollection,
            IReadOnlyList<IPropertyMetadata> foreignKey,
            IReadOnlyList<IPropertyMetadata> principalKey)
        {
            Kind = kind;
            DeclaringType = declaringType;
            RelatedType = relatedType;
            Navigation = navigation;
            IsCollection = isCollection;
            ForeignKey = foreignKey;
            PrincipalKey = principalKey;
        }

        public RelationshipKind Kind { get; }

        public Type DeclaringType { get; }

        public Type RelatedType { get; }

        public PropertyInfo? Navigation { get; }

        public bool IsCollection { get; }

        public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }

        public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }

        public IRelationshipMetadata? Inverse => null;
    }

    /// <summary>Class-owned parent marker: a collection navigation and a reference navigation.</summary>
    [SqlTable("bg3_parent")]
    public sealed class Bg3Parent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Bg3Child> Children { get; } = new List<Bg3Child>();

        public Bg3Single? Single { get; set; }
    }

    /// <summary>Class-owned child marker behind the collection relationship.</summary>
    [SqlTable("bg3_child")]
    public sealed class Bg3Child
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Class-owned child marker behind the one-to-one relationship.</summary>
    [SqlTable("bg3_single")]
    public sealed class Bg3Single
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Probe whose collection navigation declares a many-to-many relationship without a junction.</summary>
    [SqlTable("bg3_parent")]
    public sealed class Bg3ManyToManyProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Bg3Child> Children { get; } = new List<Bg3Child>();
    }

    /// <summary>Probe whose collection navigation declares a one-to-one relationship.</summary>
    [SqlTable("bg3_parent")]
    public sealed class Bg3CollectionOneToOneProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Bg3Child> Children { get; } = new List<Bg3Child>();
    }

    /// <summary>Probe whose reference navigation declares a one-to-many relationship.</summary>
    [SqlTable("bg3_parent")]
    public sealed class Bg3ReferenceOneToManyProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public Bg3Single? Single { get; set; }
    }

    /// <summary>Probe whose one-to-one reference navigation has no setter.</summary>
    [SqlTable("bg3_parent")]
    public sealed class Bg3NoSetterProbe
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public Bg3Single? Single { get; }
    }

    // ---------------------------------------------------------------------------------------------
    // D10-02 marker entities
    // ---------------------------------------------------------------------------------------------

    /// <summary>Key-upsert marker: key, two writable columns and a database-generated (computed) column.</summary>
    [SqlTable("bg10_merge")]
    public sealed class Bg10MergeRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("qty")]
        public int Qty { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("doubled")]
        public int Doubled { get; set; }

        /// <summary>Not a mapped column: read-only, so the automatic builder skips it.</summary>
        public string Extra => "extra";
    }

    /// <summary>Marker whose key is a non-generated string column.</summary>
    [SqlTable("bg10_key")]
    public sealed class Bg10KeyRow
    {
        [Key]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Marker whose every mapped column is part of the composite key.</summary>
    [SqlTable("bg10_composite")]
    public sealed class Bg10CompositeKeyRow
    {
        [Key]
        [Column("a")]
        public string A { get; set; } = string.Empty;

        [Key]
        [Column("b")]
        public string B { get; set; } = string.Empty;
    }

    /// <summary>Marker whose only mapped column is the database-generated identity.</summary>
    [SqlTable("bg10_only_identity")]
    public sealed class Bg10OnlyIdentity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }
    }

    /// <summary>INSERT..SELECT target: identity key, one writable column, a computed column, an unmapped one.</summary>
    [SqlTable("bg10_insert")]
    public sealed class Bg10InsertTarget
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
        [Column("doubled")]
        public int Doubled { get; set; }

        /// <summary>Not a mapped column.</summary>
        public string Extra => "extra";
    }

    /// <summary>Marker without any key property.</summary>
    [SqlTable("bg10_nokey")]
    public sealed class Bg10NoKeyRow
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Values-mapping target with a getter-only nested value (drives a member-list binding).</summary>
    [SqlTable("bg10_binding")]
    public sealed class Bg10BindingRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public Bg10Meta Meta { get; } = new();
    }

    /// <summary>Nested value behind the member-list binding.</summary>
    public sealed class Bg10Meta
    {
        public string Tag { get; set; } = string.Empty;
    }

    /// <summary>Dynamic-columns probe with two attribute-declared stores.</summary>
    public sealed class Bg10TwoStores
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        [DynamicColumns]
        public Dictionary<string, object?> First { get; set; } = new();

        [DynamicColumns]
        public Dictionary<string, object?> Second { get; set; } = new();
    }

    /// <summary>Dynamic-columns probe with one attribute store the fluent builder also maps as a column.</summary>
    public sealed class Bg10StoreWithFluent
    {
        [DynamicColumns]
        public Dictionary<string, object?> Store { get; set; } = new();

        public string Name { get; set; } = string.Empty;
    }

    /// <summary>Value-type entity declaring a dynamic-columns store.</summary>
    public struct Bg10StructStore
    {
        [DynamicColumns]
        public Dictionary<string, object?> Store { get; set; }
    }

    /// <summary>Dynamic-columns store without a public setter.</summary>
    public sealed class Bg10NoSetterStore
    {
        [DynamicColumns]
        public Dictionary<string, object?> Store { get; } = new();
    }

    /// <summary>Dynamic-columns store of a type the store rules reject.</summary>
    public sealed class Bg10WrongStoreType
    {
        [DynamicColumns]
        public string Store { get; set; } = string.Empty;
    }

    /// <summary>The live dynamic-columns counterpart: a valid store that collects unmapped columns.</summary>
    [SqlTable("bg10_dynamic")]
    public sealed class Bg10ValidStore
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [DynamicColumns]
        public Dictionary<string, object?> Extras { get; set; } = new();
    }

    /// <summary>A valid int-to-text converter used only by the conflict probes.</summary>
    public sealed class Bg10IntToTextConverter : ValueConverter<int, string>
    {
        public override string? ConvertToProvider(int model)
            => model.ToString(CultureInfo.InvariantCulture);

        public override int ConvertFromProvider(string? provider)
            => provider is null ? 0 : int.Parse(provider, CultureInfo.InvariantCulture);
    }

    /// <summary>A converter and a JSON column on the same property.</summary>
    [SqlTable("bg10_converter")]
    public sealed class Bg10ConverterConflict
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [ValueConverter(typeof(Bg10IntToTextConverter))]
        [JsonColumn]
        [Column("v")]
        public int Value { get; set; }
    }

    /// <summary>A range-column pair combined with a value converter.</summary>
    public sealed class Bg10RangeWithConverter
    {
        [RangeColumns("during_lower", "during_upper")]
        [ValueConverter(typeof(Bg10IntToTextConverter))]
        public Range<int> During { get; set; }
    }

    /// <summary>A range-column pair on a property that is not <c>Range&lt;T&gt;</c>.</summary>
    public sealed class Bg10RangeOnInt
    {
        [RangeColumns("during_lower", "during_upper")]
        public int During { get; set; }
    }

    /// <summary>A range-column pair combined with an explicit column name.</summary>
    public sealed class Bg10RangeWithColumn
    {
        [RangeColumns("during_lower", "during_upper")]
        [Column("during")]
        public Range<int> During { get; set; }
    }

    /// <summary>The live range-column counterpart.</summary>
    [SqlTable("bg10_range")]
    public sealed class Bg10ValidRange
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [RangeColumns("during_lower", "during_upper")]
        public Range<int> During { get; set; }
    }

    /// <summary>Navigation-shape probe: a real collection, a string and an object-typed accessor.</summary>
    [SqlTable("bg3_parent")]
    public sealed class Bg10NavParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public ICollection<Bg3Child> Children { get; } = new List<Bg3Child>();

        public object Any { get; } = new object();
    }

    /// <summary>The live attribute-filter counterpart: two named filters, both <c>id &gt; 1</c>.</summary>
    [SqlTable("bg10_filtered")]
    [QueryFilter(FilterKey = "lambda", FilterLambda = nameof(AboveOne))]
    [QueryFilter(FilterKey = "func", FilterFunc = nameof(AboveOneFunc))]
    public sealed class Bg10FilterValid
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public static Expression<Func<Bg10FilterValid, bool>> AboveOne => x => x.Id > 1;

        public static Func<EntityBuilder<Bg10FilterValid>, IDataContext, EntityBuilder<Bg10FilterValid>> AboveOneFunc
            => (builder, _) => builder.Where(x => x.Id > 1);
    }

    /// <summary>A filter attribute that sets both declaration forms.</summary>
    [QueryFilter(FilterLambda = "Any", FilterFunc = "Any")]
    public sealed class Bg10FilterBoth
    {
    }

    /// <summary>A filter attribute that sets neither form and names no key.</summary>
    [QueryFilter]
    public sealed class Bg10FilterNeither
    {
    }

    /// <summary>Two filter attributes sharing a named key on the same type.</summary>
    [QueryFilter(FilterKey = "dup", FilterLambda = nameof(Above))]
    [QueryFilter(FilterKey = "dup", FilterLambda = nameof(Above))]
    public sealed class Bg10FilterDuplicateKey
    {
        public static Expression<Func<Bg10FilterDuplicateKey, bool>> Above => x => true;
    }

    /// <summary>A lambda declaration naming a member that does not exist.</summary>
    [QueryFilter(FilterLambda = "NoSuchMember")]
    public sealed class Bg10FilterLambdaMissing
    {
    }

    /// <summary>A lambda declaration naming a static method with a parameter.</summary>
    [QueryFilter(FilterLambda = nameof(WithParameter))]
    public sealed class Bg10FilterLambdaBadKind
    {
        public static Expression<Func<Bg10FilterLambdaBadKind, bool>> WithParameter(int value) => _ => value > 0;
    }

    /// <summary>A lambda declaration whose member does not return a lambda expression.</summary>
    [QueryFilter(FilterLambda = nameof(NotALambda))]
    public sealed class Bg10FilterLambdaBadReturn
    {
        public static int NotALambda => 1;
    }

    /// <summary>A function declaration naming a member that does not exist.</summary>
    [QueryFilter(FilterFunc = "NoSuchMember")]
    public sealed class Bg10FilterFuncMissing
    {
    }

    /// <summary>A function declaration naming a static method with a parameter.</summary>
    [QueryFilter(FilterFunc = nameof(WithParameter))]
    public sealed class Bg10FilterFuncBadKind
    {
        public static object WithParameter(int value) => value;
    }

    /// <summary>A function declaration whose member does not return a delegate.</summary>
    [QueryFilter(FilterFunc = nameof(NotADelegate))]
    public sealed class Bg10FilterFuncBadReturn
    {
        public static int NotADelegate => 1;
    }

    /// <summary>A non-generic delegate shape.</summary>
    public delegate EntityBuilder<Bg10FilterShapeNonGeneric> Bg10ShapeDelegate(
        EntityBuilder<Bg10FilterShapeNonGeneric> builder, IDataContext context);

    /// <summary>A function declaration returning a non-generic delegate.</summary>
    [QueryFilter(FilterFunc = nameof(Shape))]
    public sealed class Bg10FilterShapeNonGeneric
    {
        public static Bg10ShapeDelegate Shape => (builder, _) => builder;
    }

    /// <summary>A function declaration whose first argument is a generic type other than the builder.</summary>
    [QueryFilter(FilterFunc = nameof(Shape))]
    public sealed class Bg10FilterShapeFirstNotGeneric
    {
        public static Func<List<int>, IDataContext, List<int>> Shape => (values, _) => values;
    }

    /// <summary>A function declaration whose first argument is a value type.</summary>
    [QueryFilter(FilterFunc = nameof(Shape))]
    public sealed class Bg10FilterShapeFirstValue
    {
        public static Func<int, IDataContext, int> Shape => (value, _) => value;
    }

    /// <summary>A function declaration whose second argument is not the data context.</summary>
    [QueryFilter(FilterFunc = nameof(Shape))]
    public sealed class Bg10FilterShapeSecondArg
    {
        public static Func<EntityBuilder<Bg10FilterShapeSecondArg>, string, EntityBuilder<Bg10FilterShapeSecondArg>> Shape
            => (builder, _) => builder;
    }

    /// <summary>A function declaration whose result is a builder over another entity.</summary>
    [QueryFilter(FilterFunc = nameof(Shape))]
    public sealed class Bg10FilterShapeResult
    {
        public static Func<EntityBuilder<Bg10FilterShapeResult>, IDataContext, EntityBuilder<Bg10FilterShapeNonGeneric>> Shape
            => (_, _) => null!;
    }

    /// <summary>A three-parameter generic delegate that is not <c>Func&lt;,,&gt;</c>.</summary>
    public delegate EntityBuilder<Bg10FilterShapeGenericDefinition> Bg10GenericDelegate<T1, T2, T3>(T1 first, T2 second, T3 third);

    /// <summary>A function declaration returning a generic delegate whose definition is not <c>Func&lt;,,&gt;</c>.</summary>
    [QueryFilter(FilterFunc = nameof(Shape))]
    public sealed class Bg10FilterShapeGenericDefinition
    {
        public static Bg10GenericDelegate<
            EntityBuilder<Bg10FilterShapeGenericDefinition>,
            IDataContext,
            EntityBuilder<Bg10FilterShapeGenericDefinition>> Shape
            => (builder, _, _) => builder;
    }

    /// <summary>The live merge counterpart of the active-filter guard: a type with an always-true filter.</summary>
    [SqlTable("bg10_filtered")]
    [QueryFilter(FilterLambda = nameof(Visible))]
    public sealed class Bg10FilteredRow
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        public static Expression<Func<Bg10FilteredRow, bool>> Visible => x => x.Id > 0;
    }
}
