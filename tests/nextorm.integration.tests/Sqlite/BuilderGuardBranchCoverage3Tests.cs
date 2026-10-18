using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
}
