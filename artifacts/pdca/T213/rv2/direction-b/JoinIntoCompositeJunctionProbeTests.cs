using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T213 D02(r=2) direction-B focused probe: a many-to-many <c>JoinInto</c> whose junction carries a
/// composite parent foreign key must build the derived <c>Projection&lt;TJunction, long&gt;</c> link
/// source, join parent-&gt;link and link-&gt;child componentwise and prepare as SQL. A throwing test here
/// is the probe's failure signal (the exact exception is the finding); it is not a committed red
/// regression.
/// </summary>
[Collection("Query plan cache")]
public class JoinIntoCompositeJunctionProbeTests
{
    public JoinIntoCompositeJunctionProbeTests()
    {
        DataContextCache.Clear();
    }

    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    private static void Register()
    {
        var parentProps = Props<ProbeCompositeParent>();
        var childProps = Props<ProbeCompositeChild>();
        var linkProps = Props<ProbeCompositeLink>();
        var parentKey = Prop(parentProps, nameof(ProbeCompositeParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(ProbeCompositeChild.Id)).PropertyInfo;
        var navigation = typeof(ProbeCompositeParent).GetProperty(nameof(ProbeCompositeParent.Children));

        DataContextCache.Metadata[typeof(ProbeCompositeParent)] = new EntityMetadata(
            nameof(ProbeCompositeParent),
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(ProbeCompositeParent),
                    typeof(ProbeCompositeChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    parentKey,
                    typeof(ProbeCompositeChild),
                    typeof(ProbeCompositeParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(ProbeCompositeLink),
                        ParentKey = [parentKey],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(ProbeCompositeLink.ParentId)).PropertyInfo,
                            Prop(linkProps, nameof(ProbeCompositeLink.ParentId2)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(ProbeCompositeLink.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    [Fact]
    public void CompositeJunction_ManyToMany_ShouldPrepareSql()
    {
        Register();
        using var ctx = SqliteTestContext.Create();

        var builder = ctx.From<ProbeCompositeParent>()
            .JoinInto(ctx.From<ProbeCompositeChild>(), (p, c) => true, p => p.Children);

        var pair = builder.CreateJoinIntoPairCommand();
        var prepared = (dynamic)ctx.GetPreparedQueryCommand((dynamic)pair, false, false, CancellationToken.None);
        var sql = (string)prepared.DbCommand.CommandText;

        sql.Should().Contain("row_number", "the composite link source carries the occurrence scalar");
        sql.Should().Contain("probe_composite_link", "the junction is rendered");
    }

    [SqlTable("probe_composite_parent")]
    public sealed class ProbeCompositeParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        public ICollection<ProbeCompositeChild> Children { get; set; } = new List<ProbeCompositeChild>();
    }

    [SqlTable("probe_composite_child")]
    public sealed class ProbeCompositeChild
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
    }

    [SqlTable("probe_composite_link")]
    public sealed class ProbeCompositeLink
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("parent_id2")]
        public int ParentId2 { get; set; }

        [Column("child_id")]
        public int ChildId { get; set; }
    }
}
