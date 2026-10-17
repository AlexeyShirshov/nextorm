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
