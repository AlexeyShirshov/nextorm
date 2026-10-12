using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T213 D04-R (r=4, direction D): the composite-junction many-to-many <c>JoinInto</c> SQL link source
/// is rejected explicitly at preparation. The rejection is fail-closed (a <see cref="NotSupportedException"/>
/// from <c>JunctionLinkSourceFactory.BuildSource</c>), carries the follow-up issue number in its message,
/// and must not execute any SQL. The scalar junction path is a control: it is not rejected.
/// <para>
/// Runs in the "Query cache controls" collection because it writes the process-wide
/// <see cref="DataContextCache"/> and must not race other metadata tests.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public class JoinIntoCompositeJunctionRejectionTests
{
    private const string CompositeRejectionMessage = "composite-junction M:N is not supported; see #223";

    public JoinIntoCompositeJunctionRejectionTests()
    {
        DataContextCache.Clear();
    }

    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    public sealed class CompositeJunctionParent
    {
        public int Id { get; set; }
        public ICollection<CompositeJunctionChild> Children { get; set; } = new List<CompositeJunctionChild>();
    }

    public sealed class CompositeJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class CompositeJunctionLink
    {
        public int ParentId { get; set; }
        public int ParentId2 { get; set; }
        public int ChildId { get; set; }
    }

    private static void RegisterCompositeJunctionParent()
    {
        var parentProps = Props<CompositeJunctionParent>();
        var childProps = Props<CompositeJunctionChild>();
        var linkProps = Props<CompositeJunctionLink>();
        var parentKey = Prop(parentProps, nameof(CompositeJunctionParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(CompositeJunctionChild.Id)).PropertyInfo;
        var navigation = typeof(CompositeJunctionParent).GetProperty(nameof(CompositeJunctionParent.Children));

        DataContextCache.Metadata[typeof(CompositeJunctionParent)] = new EntityMetadata(
            typeof(CompositeJunctionParent).Name,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(CompositeJunctionParent),
                    typeof(CompositeJunctionChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    parentKey,
                    typeof(CompositeJunctionChild),
                    typeof(CompositeJunctionParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(CompositeJunctionLink),
                        ParentKey = [parentKey],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(CompositeJunctionLink.ParentId)).PropertyInfo,
                            Prop(linkProps, nameof(CompositeJunctionLink.ParentId2)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(CompositeJunctionLink.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    [Fact]
    public void CompositeJunctionManyToMany_SqlPath_ShouldThrowAtPreparationWithIssueMessage_AndExecuteNoSql()
    {
        RegisterCompositeJunctionParent();
        using var ctx = new TrackingContext();

        // The composite-junction SQL link source cannot be materialized on the entity-item path yet
        // (carried by #223). Building the declaration must fail closed with the explicit message rather
        // than degrade into a broken column mapping or a raw materialization error.
        Action act = () => ctx.From<CompositeJunctionParent>()
            .JoinInto(ctx.From<CompositeJunctionChild>(), (p, c) => true, p => p.Children)
            .ToList();

        act.Should().Throw<NotSupportedException>().WithMessage(CompositeRejectionMessage);
        ctx.ConnectionsRequested.Should().Be(0, "the rejection happens at preparation; no SQL is executed");
    }

    public sealed class ScalarJunctionParent
    {
        public int Id { get; set; }
        public ICollection<ScalarJunctionChild> Children { get; set; } = new List<ScalarJunctionChild>();
    }

    public sealed class ScalarJunctionChild
    {
        public int Id { get; set; }
    }

    public sealed class ScalarJunctionLink
    {
        public int ParentId { get; set; }
        public int ChildId { get; set; }
    }

    [Fact]
    public void ScalarJunctionManyToMany_SqlPath_ShouldNotBeRejected()
    {
        using var ctx = new TrackingContext();
        ctx.From<ScalarJunctionParent>(b => b.HasManyThrough<ScalarJunctionChild, ScalarJunctionLink, int, int>(
            p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId));
        ctx.From<ScalarJunctionChild>();

        // The rejection is specific to a composite junction key: a scalar junction still builds its
        // derived row_number() SQL link source.
        Action act = () => ctx.From<ScalarJunctionParent>()
            .JoinInto(ctx.From<ScalarJunctionChild>(), (p, c) => true, p => p.Children);

        act.Should().NotThrow("the scalar junction link source is not rejected");
    }

    // ---------------------------------------------------------------------------------------------
    // Provider-free SQL context: a connection request is the observable proof that SQL would execute.
    // ---------------------------------------------------------------------------------------------

    private sealed class TrackingContext : DataContext
    {
        public TrackingContext() : base(new DataContextBuilder())
        {
        }

        public int ConnectionsRequested { get; private set; }

        public override ISqlDialect Dialect => TestDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => new TrackingParameter(name) { Value = value };

        protected override DbConnection CreateDbConnection(string? connectionString)
        {
            ConnectionsRequested++;
            throw new NotSupportedException("no SQL must be executed for a rejected composite-junction M:N JoinInto");
        }
    }

    private sealed class TestDialect : SqlDialectBase
    {
        internal static readonly TestDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override bool SupportsRawSqlSource => true;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TrackingParameter(string name) : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; }

        public override bool IsNullable { get; set; }

        [AllowNull]
        public override string ParameterName { get; set; } = name;

        public override int Size { get; set; }

        [AllowNull]
        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType()
        {
        }
    }
}
