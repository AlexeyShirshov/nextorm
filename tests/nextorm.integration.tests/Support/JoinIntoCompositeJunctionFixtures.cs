using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D03 (#223) real-integration fixture for a <b>composite junction</b> many-to-many: the parent principal
/// key is <c>(tenant, link_id)</c> and the junction carries two parent foreign-key components
/// (<c>parent_tenant</c>, <c>parent_link_id</c>) plus one child foreign key (<c>child_id</c>). Two parents
/// share the first component and differ in the last, so a join or child match that used only one component
/// would cross-link them. The junction is injected through <see cref="DataContextCache"/> because the fluent
/// declaration surface is single-column only. Mapped to dedicated <c>eager_cj_*</c> tables.
/// </summary>
internal static class JoinIntoCompositeJunctionFixtures
{
    private static int _seed = -7_500_000;

    internal static int NextBase() => Interlocked.Add(ref _seed, -1_000);

    internal static void Register()
    {
        var parentBuilt = new EntityMetadataBuilder<JiCompositeJunctionParent>().Build();
        var parentNavigation = typeof(JiCompositeJunctionParent).GetProperty(nameof(JiCompositeJunctionParent.Children))!;
        var noteNavigation = typeof(JiCompositeJunctionParent).GetProperty(nameof(JiCompositeJunctionParent.Notes))!;
        var childProps = new EntityMetadataBuilder<JiCompositeJunctionChild>().Build().Properties;
        var linkProps = new EntityMetadataBuilder<JiCompositeJunctionLink>().Build().Properties;
        var noteProps = new EntityMetadataBuilder<JoinIntoNote>().Build().Properties;
        var parentProps = parentBuilt.Properties;

        static IPropertyMetadata Find(IReadOnlyList<IPropertyMetadata> all, string name)
            => all.Single(p => p.PropertyInfo.Name == name);

        var parentKey = new[]
        {
            Find(parentProps, nameof(JiCompositeJunctionParent.Tenant)),
            Find(parentProps, nameof(JiCompositeJunctionParent.LinkId)),
        };
        var childKey = new[] { Find(childProps, nameof(JiCompositeJunctionChild.Id)) };

        var junction = new RelationshipJunctionMetadata(
            typeof(JiCompositeJunctionLink),
            parentKey,
            childKey,
            new[]
            {
                Find(linkProps, nameof(JiCompositeJunctionLink.ParentTenant)),
                Find(linkProps, nameof(JiCompositeJunctionLink.ParentLinkId)),
            },
            new[] { Find(linkProps, nameof(JiCompositeJunctionLink.ChildId)) });

        DataContextCache.Metadata[typeof(JiCompositeJunctionParent)] = new EntityMetadata(
            parentBuilt.TableName,
            parentProps.Where(p => p.PropertyInfo != parentNavigation && p.PropertyInfo != noteNavigation).ToList(),
            parentBuilt.IsTableNameAuto,
            relationships:
            [
                new InjectedManyToMany(
                    parentNavigation,
                    typeof(JiCompositeJunctionChild),
                    parentKey,
                    childKey,
                    junction),
                new InjectedOneToMany(
                    noteNavigation,
                    typeof(JoinIntoNote),
                    foreignKey: [Find(noteProps, nameof(JoinIntoNote.ParentId))],
                    principalKey: [Find(parentProps, nameof(JiCompositeJunctionParent.RowId))]),
            ]);
    }

    /// <summary>
    /// Seeds two parents that share the first composite component and differ in the last, one child per
    /// parent, a repeated occurrence of the first pair, a dangling child foreign key, and one note per
    /// parent. Returns the two parent row ids.
    /// </summary>
    internal static (int First, int Second) Seed(IDataContext ctx)
    {
        Register();

        var first = NextBase();
        var second = first + 1;
        var tenant = first;
        var link1 = first + 1;
        var link2 = first + 2;

        ctx.CreateInsertBuilder<JiCompositeJunctionParent>().Values([
            new JiCompositeJunctionParent { RowId = first, Tenant = tenant, LinkId = link1, Name = "p1" },
            new JiCompositeJunctionParent { RowId = second, Tenant = tenant, LinkId = link2, Name = "p2" },
        ]).Insert();

        ctx.CreateInsertBuilder<JiCompositeJunctionChild>().Values([
            new JiCompositeJunctionChild { Id = first + 100, Name = "c1" },
            new JiCompositeJunctionChild { Id = first + 101, Name = "c2" },
        ]).Insert();

        ctx.CreateInsertBuilder<JiCompositeJunctionLink>().Values([
            // Both links share the parent's first component; the last routes each child to exactly one parent.
            new JiCompositeJunctionLink { ParentTenant = tenant, ParentLinkId = link1, ChildId = first + 100 },
            new JiCompositeJunctionLink { ParentTenant = tenant, ParentLinkId = link2, ChildId = first + 101 },
            // A repeated occurrence of the (parent, child) pair: it must remain a distinct element.
            new JiCompositeJunctionLink { ParentTenant = tenant, ParentLinkId = link1, ChildId = first + 100 },
            // A dangling child foreign key: it must contribute no element.
            new JiCompositeJunctionLink { ParentTenant = tenant, ParentLinkId = link1, ChildId = first + 999 },
        ]).Insert();

        ctx.CreateInsertBuilder<JoinIntoNote>().Values([
            new JoinIntoNote { Id = first + 200, ParentId = first, Text = "n1" },
            new JoinIntoNote { Id = first + 201, ParentId = second, Text = "n2" },
        ]).Insert();

        return (first, second);
    }

    private sealed class InjectedManyToMany : IRelationshipMetadata
    {
        private readonly RelationshipJunctionMetadata _junction;

        public InjectedManyToMany(
            PropertyInfo? navigation,
            Type relatedType,
            IReadOnlyList<IPropertyMetadata> principalKey,
            IReadOnlyList<IPropertyMetadata> foreignKey,
            RelationshipJunctionMetadata junction)
        {
            Navigation = navigation;
            RelatedType = relatedType;
            PrincipalKey = principalKey;
            ForeignKey = foreignKey;
            _junction = junction;
        }

        public RelationshipKind Kind => RelationshipKind.ManyToMany;
        public Type DeclaringType => typeof(JiCompositeJunctionParent);
        public Type RelatedType { get; }
        public PropertyInfo? Navigation { get; }
        public bool IsCollection => true;
        public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }
        public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }
        public IRelationshipMetadata? Inverse => null;
        public RelationshipJunctionMetadata? Junction => _junction;
    }

    private sealed class InjectedOneToMany : IRelationshipMetadata
    {
        public InjectedOneToMany(
            PropertyInfo? navigation,
            Type relatedType,
            IReadOnlyList<IPropertyMetadata> foreignKey,
            IReadOnlyList<IPropertyMetadata> principalKey)
        {
            Navigation = navigation;
            RelatedType = relatedType;
            ForeignKey = foreignKey;
            PrincipalKey = principalKey;
        }

        public RelationshipKind Kind => RelationshipKind.OneToMany;
        public Type DeclaringType => typeof(JiCompositeJunctionParent);
        public Type RelatedType { get; }
        public PropertyInfo? Navigation { get; }
        public bool IsCollection => true;
        public IReadOnlyList<IPropertyMetadata> ForeignKey { get; }
        public IReadOnlyList<IPropertyMetadata> PrincipalKey { get; }
        public IRelationshipMetadata? Inverse => null;
    }
}

/// <summary>Composite-junction M:N parent mapped to <c>eager_cj_parent</c>; principal key (tenant, link_id).</summary>
[SqlTable("eager_cj_parent")]
internal sealed class JiCompositeJunctionParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JiCompositeJunctionChild> Children { get; set; } = new List<JiCompositeJunctionChild>();

    public ICollection<JoinIntoNote> Notes { get; set; } = new List<JoinIntoNote>();
}

/// <summary>Composite-junction M:N child mapped to <c>eager_cj_child</c>.</summary>
[SqlTable("eager_cj_child")]
internal sealed class JiCompositeJunctionChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>Composite-junction link mapped to <c>eager_cj_link</c>; two parent components and one child.</summary>
[SqlTable("eager_cj_link")]
internal sealed class JiCompositeJunctionLink
{
    [Column("parent_tenant")]
    public int ParentTenant { get; set; }

    [Column("parent_link_id")]
    public int ParentLinkId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

/// <summary>Query-counting interceptor shared by the composite-junction integration fixtures.</summary>
internal sealed class JoinIntoQueryCountingInterceptor : IQueryInterceptor
{
    private int _executing;
    private int _executed;

    public int Executing => Volatile.Read(ref _executing);
    public int Executed => Volatile.Read(ref _executed);

    public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
        => Interlocked.Increment(ref _executing);

    public void CommandExecuted(CommandEventData eventData, System.Data.Common.DbCommand command, TimeSpan elapsed)
        => Interlocked.Increment(ref _executed);

    public void CommandFailed(CommandEventData eventData, System.Data.Common.DbCommand command, Exception exception)
    {
    }
}
