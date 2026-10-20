using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// D07 (r=5) focused MariaDB adapter for a <b>composite relationship key without a composite junction</b>:
/// MariaDB has no <see cref="CommonTestSuite"/>-based integration class, so the shared composite-key
/// 1:N/1:1 cases are re-pinned here against a real server with their own tables. The declared relationship
/// (single-column-only fluent surface) is injected through <see cref="DataContextCache"/>. A skip is never
/// a pass; MySQL is not a substitute.
/// </summary>
public sealed class MariaDbJoinIntoCompositeKeyIntegrationTests : IDisposable
{
    private readonly MariaDbDataContext _ctx;

    public MariaDbJoinIntoCompositeKeyIntegrationTests()
    {
        Assert.SkipUnless(MariaDbContainer.IsAvailable, MariaDbContainer.Failure ?? "MariaDB is not available.");

        _ctx = new MariaDbDataContext(MariaDbContainer.ConnectionString, new DataContextBuilder());
        Seed();
    }

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public void JoinInto_CompositeKeyOneToMany_ShouldMatchEveryComponentWithoutBlendingParents()
    {
        var parents = _ctx.From<MdbJiCmpOneToManyParent>()
            .Where(p => p.RowId == ParentBase || p.RowId == ParentBase + 1 || p.RowId == ParentBase + 2)
            .JoinInto(
                _ctx.From<MdbJiCmpChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Children)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(ParentBase, ParentBase + 1, ParentBase + 2);
        parents[0].Children.Select(ch => ch.Id).Should().BeEquivalentTo([ParentBase + 100, ParentBase + 101]);
        parents[1].Children.Select(ch => ch.Id).Should().Equal(ParentBase + 102);
        parents[2].Children.Select(ch => ch.Id).Should().Equal(ParentBase + 103);
        parents.SelectMany(p => p.Children).Select(ch => ch.Id).Should().NotContain(ParentBase + 104);
    }

    [Fact]
    public void JoinInto_CompositeKeyOneToOne_ShouldAssignEachParentItsOwnChildAndLeaveChildlessNull()
    {
        var parents = _ctx.From<MdbJiCmpOneToOneParent>()
            .Where(p => p.RowId == OneToOneBase || p.RowId == OneToOneBase + 1 || p.RowId == OneToOneBase + 2)
            .JoinInto(
                _ctx.From<MdbJiCmpChild>(),
                (p, ch) => p.Tenant == ch.Tenant && p.LinkId == ch.ParentId,
                p => p.Primary)
            .OrderBy(p => p.RowId)
            .ToList();

        parents.Select(p => p.RowId).Should().Equal(OneToOneBase, OneToOneBase + 1, OneToOneBase + 2);
        parents[0].Primary!.Id.Should().Be(ParentBase + 110);
        parents[1].Primary!.Id.Should().Be(ParentBase + 111);
        parents[2].Primary.Should().BeNull();
    }

    private const int ParentBase = -660_000_000;

    // The one-to-one fixture uses its own key space so its parents/children never collide with the
    // one-to-many rows that share the same physical tables.
    private const int OneToOneBase = ParentBase + 10;
    private const int OneToOneTenant = ParentBase + 50;
    private const int OneToOneLink1 = ParentBase + 60;
    private const int OneToOneLink2 = ParentBase + 70;
    private const int OneToOneLinkChildless = ParentBase + 80;

    private void Seed()
    {
        Execute("drop table if exists mdb_ji_cmp_child");
        Execute("drop table if exists mdb_ji_cmp_parent");
        Execute("create table mdb_ji_cmp_parent (row_id int not null primary key, tenant int not null, link_id int not null)");
        Execute("create table mdb_ji_cmp_child (id int not null primary key, tenant int not null, parent_id int not null, sort_key int not null)");

        RegisterOneToMany();
        RegisterOneToOne();

        var t1 = ParentBase;
        var t2 = ParentBase + 1;
        var l1 = ParentBase + 10;
        var l2 = ParentBase + 20;

        _ctx.CreateInsertBuilder<MdbJiCmpOneToManyParent>().Values([
            new MdbJiCmpOneToManyParent { RowId = ParentBase, Tenant = t1, LinkId = l1 },
            new MdbJiCmpOneToManyParent { RowId = ParentBase + 1, Tenant = t1, LinkId = l2 },
            new MdbJiCmpOneToManyParent { RowId = ParentBase + 2, Tenant = t2, LinkId = l1 },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbJiCmpOneToOneParent>().Values([
            new MdbJiCmpOneToOneParent { RowId = OneToOneBase, Tenant = OneToOneTenant, LinkId = OneToOneLink1 },
            new MdbJiCmpOneToOneParent { RowId = OneToOneBase + 1, Tenant = OneToOneTenant, LinkId = OneToOneLink2 },
            new MdbJiCmpOneToOneParent { RowId = OneToOneBase + 2, Tenant = OneToOneTenant, LinkId = OneToOneLinkChildless },
        ]).Insert();

        _ctx.CreateInsertBuilder<MdbJiCmpChild>().Values([
            new MdbJiCmpChild { Id = ParentBase + 100, Tenant = t1, ParentId = l1, SortKey = 1 },
            new MdbJiCmpChild { Id = ParentBase + 101, Tenant = t1, ParentId = l1, SortKey = 2 },
            new MdbJiCmpChild { Id = ParentBase + 102, Tenant = t1, ParentId = l2, SortKey = 3 },
            new MdbJiCmpChild { Id = ParentBase + 103, Tenant = t2, ParentId = l1, SortKey = 4 },
            new MdbJiCmpChild { Id = ParentBase + 104, Tenant = t2, ParentId = l2, SortKey = 5 },
            new MdbJiCmpChild { Id = ParentBase + 110, Tenant = OneToOneTenant, ParentId = OneToOneLink1, SortKey = 6 },
            new MdbJiCmpChild { Id = ParentBase + 111, Tenant = OneToOneTenant, ParentId = OneToOneLink2, SortKey = 7 },
        ]).Insert();
    }

    private void RegisterOneToMany()
    {
        var navigation = typeof(MdbJiCmpOneToManyParent).GetProperty(nameof(MdbJiCmpOneToManyParent.Children))!;
        Register<MdbJiCmpOneToManyParent>(navigation, RelationshipKind.OneToMany);
    }

    private void RegisterOneToOne()
    {
        var navigation = typeof(MdbJiCmpOneToOneParent).GetProperty(nameof(MdbJiCmpOneToOneParent.Primary))!;
        Register<MdbJiCmpOneToOneParent>(navigation, RelationshipKind.OneToOne);
    }

    private static void Register<T>(PropertyInfo navigation, RelationshipKind kind)
    {
        var built = new EntityMetadataBuilder<T>().Build();
        var properties = built.Properties.Where(p => p.PropertyInfo != navigation).ToList();
        var parentProps = built.Properties;
        var childProps = new EntityMetadataBuilder<MdbJiCmpChild>().Build().Properties;

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
                    typeof(MdbJiCmpChild),
                    navigation,
                    kind == RelationshipKind.OneToMany,
                    foreignKey:
                    [
                        Find(childProps, nameof(MdbJiCmpChild.Tenant)),
                        Find(childProps, nameof(MdbJiCmpChild.ParentId)),
                    ],
                    principalKey:
                    [
                        Find(parentProps, "Tenant"),
                        Find(parentProps, "LinkId"),
                    ]),
            ]);
    }

    private void Execute(string sql)
    {
        ((DataContext)_ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)_ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
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

[SqlTable("mdb_ji_cmp_parent")]
internal sealed class MdbJiCmpOneToManyParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public ICollection<MdbJiCmpChild> Children { get; set; } = new List<MdbJiCmpChild>();
}

[SqlTable("mdb_ji_cmp_parent")]
internal sealed class MdbJiCmpOneToOneParent
{
    [Key]
    [Column("row_id")]
    public int RowId { get; set; }

    [Column("tenant")]
    public int Tenant { get; set; }

    [Column("link_id")]
    public int LinkId { get; set; }

    public MdbJiCmpChild? Primary { get; set; }
}

[SqlTable("mdb_ji_cmp_child")]
internal sealed class MdbJiCmpChild
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
