using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D07 (r=5) real-integration fixture for a <b>composite relationship key without a composite junction</b>,
/// mapped to the existing <c>eager_composite_parent</c>/<c>eager_composite_child</c> tables so no extra
/// provider DDL is needed. The parent principal key <c>(tenant, link_id)</c> is deliberately not the table
/// key (<c>row_id</c>): the seeded parents share one key component each, so a join or child match that used
/// only one component would blend parents. The declared relationship is injected through
/// <see cref="DataContextCache"/> because the fluent declaration surface is single-column only.
/// </summary>
internal static class JoinIntoCompositeKeyFixtures
{
    private static int _seed = -4_500_000;

    internal static int NextBase() => Interlocked.Add(ref _seed, -1_000);

    internal static void RegisterOneToMany()
    {
        var navigation = typeof(JiCompositeOneToManyParent).GetProperty(nameof(JiCompositeOneToManyParent.Children))!;
        Register<JiCompositeOneToManyParent>(
            navigation,
            RelationshipKind.OneToMany,
            nameof(JiCompositeOneToManyParent.Tenant),
            nameof(JiCompositeOneToManyParent.LinkId));
    }

    internal static void RegisterOneToOne()
    {
        var navigation = typeof(JiCompositeOneToOneParent).GetProperty(nameof(JiCompositeOneToOneParent.Primary))!;
        Register<JiCompositeOneToOneParent>(
            navigation,
            RelationshipKind.OneToOne,
            nameof(JiCompositeOneToOneParent.Tenant),
            nameof(JiCompositeOneToOneParent.LinkId));
    }

    private static void Register<T>(PropertyInfo navigation, RelationshipKind kind, string tenantName, string linkName)
    {
        var built = new EntityMetadataBuilder<T>().Build();
        var properties = built.Properties.Where(p => p.PropertyInfo != navigation).ToList();
        var parentProps = built.Properties;
        var childProps = new EntityMetadataBuilder<JiCompositeKeyChild>().Build().Properties;

        static IPropertyMetadata Find(IReadOnlyList<IPropertyMetadata> all, string name)
            => all.Single(p => p.PropertyInfo.Name == name);

        DataContextCache.Metadata[typeof(T)] = new EntityMetadata(
            built.TableName,
            properties,
            built.IsTableNameAuto,
            relationships:
            [
                new InjectedRelationship(
                    kind,
                    typeof(T),
                    typeof(JiCompositeKeyChild),
                    navigation,
                    kind == RelationshipKind.OneToMany,
                    foreignKey:
                    [
                        Find(childProps, nameof(JiCompositeKeyChild.Tenant)),
                        Find(childProps, nameof(JiCompositeKeyChild.ParentId)),
                    ],
                    principalKey:
                    [
                        Find(parentProps, tenantName),
                        Find(parentProps, linkName),
                    ]),
            ]);
    }

    /// <summary>
    /// Seeds three parents whose composite link key overlaps on one component each:
    /// <c>(t1, l1)</c>, <c>(t1, l2)</c>, <c>(t2, l1)</c>. The first has two children, the others one
    /// each, plus an orphan child keyed to <c>(t2, l2)</c> that no parent matches.
    /// </summary>
    internal static (int A, int B, int C) SeedOneToMany(IDataContext ctx)
    {
        RegisterOneToMany();

        var a = NextBase();
        var t1 = a;
        var t2 = a + 1;
        var l1 = a + 10;
        var l2 = a + 20;

        ctx.CreateInsertBuilder<JiCompositeOneToManyParent>().Values([
            new JiCompositeOneToManyParent { RowId = a, Tenant = t1, LinkId = l1 },
            new JiCompositeOneToManyParent { RowId = a + 1, Tenant = t1, LinkId = l2 },
            new JiCompositeOneToManyParent { RowId = a + 2, Tenant = t2, LinkId = l1 },
        ]).Insert();

        ctx.CreateInsertBuilder<JiCompositeKeyChild>().Values([
            new JiCompositeKeyChild { Id = a + 100, Tenant = t1, ParentId = l1, SortKey = 1 },
            new JiCompositeKeyChild { Id = a + 101, Tenant = t1, ParentId = l1, SortKey = 2 },
            new JiCompositeKeyChild { Id = a + 102, Tenant = t1, ParentId = l2, SortKey = 3 },
            new JiCompositeKeyChild { Id = a + 103, Tenant = t2, ParentId = l1, SortKey = 4 },
            new JiCompositeKeyChild { Id = a + 104, Tenant = t2, ParentId = l2, SortKey = 5 },
        ]).Insert();

        return (a, a + 1, a + 2);
    }

    /// <summary>
    /// Seeds three parents that all share tenant <c>t1</c> but differ in <c>link_id</c>: two have exactly
    /// one distinct child each and one has none. Sharing the tenant means a tenant-only match would pair
    /// each parent with both children, which the one-to-one backstop must reject.
    /// </summary>
    internal static (int WithChild, int OtherChild, int Childless) SeedOneToOne(IDataContext ctx)
    {
        RegisterOneToOne();

        var a = NextBase();
        var t1 = a;
        var l1 = a + 10;
        var l2 = a + 20;

        ctx.CreateInsertBuilder<JiCompositeOneToOneParent>().Values([
            new JiCompositeOneToOneParent { RowId = a, Tenant = t1, LinkId = l1 },
            new JiCompositeOneToOneParent { RowId = a + 1, Tenant = t1, LinkId = l2 },
            new JiCompositeOneToOneParent { RowId = a + 2, Tenant = t1, LinkId = a + 30 },
        ]).Insert();

        ctx.CreateInsertBuilder<JiCompositeKeyChild>().Values([
            new JiCompositeKeyChild { Id = a + 110, Tenant = t1, ParentId = l1, SortKey = 1 },
            new JiCompositeKeyChild { Id = a + 111, Tenant = t1, ParentId = l2, SortKey = 2 },
        ]).Insert();

        return (a, a + 1, a + 2);
    }

    // ---------------------------------------------------------------------------------------------
    // T251 declared surface: the same physical tables and the same row shape, declared through the
    // public fluent A–J overloads instead of an injected IRelationshipMetadata. The CLR types are
    // distinct so the process-wide metadata registration of the manual fixtures above cannot shadow
    // or be shadowed by these declarations.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Registers the declared fixture graph: the child must be registered first so its own reference
    /// navigation is excluded from the mapping before the parent-side collection provision resolves it.
    /// </summary>
    internal static void RegisterDeclared(IDataContext ctx)
    {
        ctx.From<T251DeclaredChild>((EntityMetadataBuilder<T251DeclaredChild> b) => b.HasOne(
            c => c.Parent,
            c => new { c.Tenant, c.ParentId },
            p => new { p.Tenant, p.LinkId }));
        ctx.From<T251DeclaredPrimary>();
        ctx.From<T251DeclaredNullableChild>();
        // Single-column nullable FK (legacy shape) to the same principal: the null component must mean
        // "no relationship" without throwing Expression.Equal(int?, int).
        ctx.From<T251SingleNullableChild>((EntityMetadataBuilder<T251SingleNullableChild> b) => b.HasOne(
            c => c.Parent,
            c => new { c.ParentId },
            p => new { p.LinkId }));
        // Composite FK with exactly one nullable component.
        ctx.From<T251PartialNullableChild>((EntityMetadataBuilder<T251PartialNullableChild> b) => b.HasOne(
            c => c.Parent,
            c => new { c.Tenant, c.ParentId },
            p => new { p.Tenant, p.LinkId }));
        ctx.From<T251DeclaredManyParent>((EntityMetadataBuilder<T251DeclaredManyParent> b) => b
            .HasMany(p => p.Children, c => new { c.Tenant, c.ParentId }, p => new { p.Tenant, p.LinkId })
            .HasMany(p => p.SwappedChildren, c => new { c.Tenant, c.ParentId }, p => new { p.RowId, p.Tenant })
            .HasMany(p => p.NullableChildren, c => new { c.Tenant, c.ParentId }, p => new { p.Tenant, p.LinkId })
            .HasOneToOne(p => p.Primary, p => new { p.Tenant, p.LinkId }, c => new { c.OwnerTenant, c.ParentLink }));
    }

    /// <summary>
    /// Seeds the declared one-to-many rows with the manual fixture's shape: three parents matching on
    /// one component each, two children for the first, one each for the others, plus an orphan child
    /// keyed to <c>(t2, l2)</c> that no parent matches.
    /// </summary>
    internal static (int A, int B, int C) SeedDeclaredOneToMany(IDataContext ctx)
    {
        RegisterDeclared(ctx);

        var a = NextBase();
        var t1 = a;
        var t2 = a + 1;
        var l1 = a + 10;
        var l2 = a + 20;

        ctx.CreateInsertBuilder<T251DeclaredManyParent>().Values([
            new T251DeclaredManyParent { RowId = a, Tenant = t1, LinkId = l1 },
            new T251DeclaredManyParent { RowId = a + 1, Tenant = t1, LinkId = l2 },
            new T251DeclaredManyParent { RowId = a + 2, Tenant = t2, LinkId = l1 },
        ]).Insert();

        ctx.CreateInsertBuilder<T251DeclaredChild>().Values([
            new T251DeclaredChild { Id = a + 100, Tenant = t1, ParentId = l1, SortKey = 1 },
            new T251DeclaredChild { Id = a + 101, Tenant = t1, ParentId = l1, SortKey = 2 },
            new T251DeclaredChild { Id = a + 102, Tenant = t1, ParentId = l2, SortKey = 3 },
            new T251DeclaredChild { Id = a + 103, Tenant = t2, ParentId = l1, SortKey = 4 },
            new T251DeclaredChild { Id = a + 104, Tenant = t2, ParentId = l2, SortKey = 5 },
            // A dangling non-null foreign key: no parent carries link id <c>a + 99</c>.
            new T251DeclaredChild { Id = a + 105, Tenant = t1, ParentId = a + 99, SortKey = 6 },
        ]).Insert();

        return (a, a + 1, a + 2);
    }

    /// <summary>
    /// Seeds the declared one-to-one rows: all parents share the tenant and differ only in the trailing
    /// link component, so a tenant-only match would pair each parent with both children.
    /// </summary>
    internal static (int WithChild, int OtherChild, int Childless) SeedDeclaredOneToOne(IDataContext ctx)
    {
        RegisterDeclared(ctx);

        var a = NextBase();
        var t1 = a;
        var l1 = a + 10;
        var l2 = a + 20;

        ctx.CreateInsertBuilder<T251DeclaredManyParent>().Values([
            new T251DeclaredManyParent { RowId = a, Tenant = t1, LinkId = l1 },
            new T251DeclaredManyParent { RowId = a + 1, Tenant = t1, LinkId = l2 },
            new T251DeclaredManyParent { RowId = a + 2, Tenant = t1, LinkId = a + 30 },
        ]).Insert();

        ctx.CreateInsertBuilder<T251DeclaredPrimary>().Values([
            new T251DeclaredPrimary { Id = a + 110, OwnerTenant = t1, ParentLink = l1, SortKey = 1 },
            new T251DeclaredPrimary { Id = a + 111, OwnerTenant = t1, ParentLink = l2, SortKey = 2 },
        ]).Insert();

        return (a, a + 1, a + 2);
    }

    /// <summary>
    /// Seeds one valid child plus one row for each null position of the nullable composite foreign key
    /// (first only, last only, both) and one dangling non-null row. A null-safe comparison would link
    /// the all-null row; a first-component-only match would link the last-null and first-null rows.
    /// </summary>
    internal static (int Parent, int Tenant, int Link, int ValidChild) SeedDeclaredNullable(IDataContext ctx)
    {
        RegisterDeclared(ctx);

        var a = NextBase();
        var t1 = a;
        var l1 = a + 10;

        ctx.CreateInsertBuilder<T251DeclaredManyParent>().Values([
            new T251DeclaredManyParent { RowId = a, Tenant = t1, LinkId = l1 },
        ]).Insert();

        ctx.CreateInsertBuilder<T251DeclaredNullableChild>().Values([
            new T251DeclaredNullableChild { Id = a + 120, Tenant = t1, ParentId = l1, SortKey = 1 },
            new T251DeclaredNullableChild { Id = a + 121, Tenant = null, ParentId = l1, SortKey = 2 },
            new T251DeclaredNullableChild { Id = a + 122, Tenant = t1, ParentId = null, SortKey = 3 },
            new T251DeclaredNullableChild { Id = a + 123, Tenant = null, ParentId = null, SortKey = 4 },
            new T251DeclaredNullableChild { Id = a + 124, Tenant = t1, ParentId = a + 99, SortKey = 5 },
        ]).Insert();

        return (a, t1, l1, a + 120);
    }

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
}

/// <summary>1:N composite-key parent; maps the shared <c>eager_composite_parent</c> table.</summary>
[SqlTable("eager_composite_parent")]
public sealed class JiCompositeOneToManyParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public ICollection<JiCompositeKeyChild> Children { get; set; } = new List<JiCompositeKeyChild>();
}

/// <summary>1:1 composite-key parent; maps the same shared <c>eager_composite_parent</c> table.</summary>
[SqlTable("eager_composite_parent")]
public sealed class JiCompositeOneToOneParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public JiCompositeKeyChild? Primary { get; set; }
}

/// <summary>Composite-key child; the foreign key <c>(tenant, parent_id)</c> pairs the principal key.</summary>
[SqlTable("eager_composite_child")]
public sealed class JiCompositeKeyChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}

/// <summary>
/// T251 declared-surface parent: the composite principal key <c>(tenant, link_id)</c> is not the table
/// key (<c>row_id</c>) and is declared through the fluent overloads in
/// <see cref="JoinIntoCompositeKeyFixtures.RegisterDeclared"/>.
/// </summary>
[SqlTable("eager_composite_parent")]
public sealed class T251DeclaredManyParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public ICollection<T251DeclaredChild> Children { get; set; } = new List<T251DeclaredChild>();

    /// <summary>Same foreign-key list, principal components declared in the opposite order.</summary>
    public ICollection<T251DeclaredChild> SwappedChildren { get; set; } = new List<T251DeclaredChild>();

    public ICollection<T251DeclaredNullableChild> NullableChildren { get; set; } = new List<T251DeclaredNullableChild>();

    public T251DeclaredPrimary? Primary { get; set; }
}

/// <summary>T251 declared-surface child; the foreign key <c>(tenant, parent_id)</c> pairs the principal.</summary>
[SqlTable("eager_composite_child")]
public sealed class T251DeclaredChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    public T251DeclaredManyParent? Parent { get; set; }
}

/// <summary>T251 declared-surface one-to-one dependent; the composite foreign key is non-unique at the schema.</summary>
[SqlTable("eager_composite_child")]
public sealed class T251DeclaredPrimary
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int OwnerTenant { get; set; }

    [Column("parent_id")]
    public int ParentLink { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}

/// <summary>
/// T251 declared-surface child whose composite foreign key components are nullable, so each null
/// position can be seeded on a live database. Mapped to the shared (nullable) table columns.
/// </summary>
[SqlTable("eager_composite_child")]
public sealed class T251DeclaredNullableChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int? Tenant { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }
}

/// <summary>
/// T251 live single-column nullable foreign key: <c>parent_id</c> is the only key component, so a null
/// or dangling value must resolve the reference to <c>null</c> (plain equality, no throw).
/// </summary>
[SqlTable("eager_composite_child")]
public sealed class T251SingleNullableChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    public T251DeclaredManyParent? Parent { get; set; }
}

/// <summary>
/// T251 live composite foreign key with exactly one nullable component: <c>(tenant, parent_id)</c> where
/// only <c>parent_id</c> is nullable. A null component breaks the key, so the reference is null.
/// </summary>
[SqlTable("eager_composite_child")]
public sealed class T251PartialNullableChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("parent_id")]
    public int? ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    public T251DeclaredManyParent? Parent { get; set; }
}
