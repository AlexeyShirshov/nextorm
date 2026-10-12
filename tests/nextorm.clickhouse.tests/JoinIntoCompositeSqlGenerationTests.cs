using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// SQL-generation coverage for <c>JoinInto</c> with a <b>composite relationship key</b> (no composite
/// junction): a 1:N collection and a 1:1 reference whose parent principal key and child foreign key are
/// both composite. The declared relationship is injected through <see cref="DataContextCache"/> because
/// the fluent declaration surface is single-column only; the join must render every key component
/// componentwise (not just the first) and must not disable the plan cache. The scalar many-to-many
/// declaration composed with <see cref="EagerLoadMode.SingleQuery"/> is retained as the control. These
/// tests never open a connection.
/// </summary>
[Collection("Query plan cache")]
public class JoinIntoCompositeSqlGenerationTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static string Canon(string sql)
    {
        var s = sql
            .Replace("\r\n", "\n")
            .Replace("\"", string.Empty)
            .Replace("'", string.Empty)
            .Replace("`", string.Empty)
            .Replace("[", string.Empty)
            .Replace("]", string.Empty);
        return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static string SingleQuerySql(IDataContext ctx, EntityBuilder<JnCompositeParent> builder)
    {
        var (specs, joins) = builder.BuildSingleQueryJoins();
        return SqlOf(ctx, (dynamic)builder.CreatePairCommand(specs, joins));
    }

    private static EntityBuilder<JnCompositeParent> Parents(IDataContext ctx)
        => ctx.From<JnCompositeParent>(b => b
            .HasManyThrough<JnCompositeChild, JnCompositeLink, int, int>(
                p => p.Children, p => p.Id, l => l.ParentId, c => c.Id, l => l.ChildId)
            .HasMany(p => p.Notes, n => n.ParentId));

    [Fact]
    public void ManyToMany_WithSingleQueryLoadWith_ShouldRenderBothCollections()
    {
        using var ctx = ClickHouseTestContext.Create();

        var builder = Parents(ctx)
            .JoinInto(ctx.From<JnCompositeChild>(), (p, c) => true, p => p.Children)
            .LoadWith(p => p.Notes, n => n.From<JnCompositeNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.SingleQuery);

        var sql = SingleQuerySql(ctx, builder);

        sql.Should().Contain("jn_comp_link", "the derived junction link source is rendered");
        sql.Should().Contain("jn_comp_child", "the many-to-many child is joined");
        sql.Should().Contain("jn_comp_note", "the single-query LoadWith child is joined");
    }

    // ---------------------------------------------------------------------------------------------
    // Composite relationship key, no composite junction (1:N and 1:1).
    // ---------------------------------------------------------------------------------------------

    private static void RegisterComposite<T>(
        PropertyInfo? navigation,
        RelationshipKind kind,
        Type relatedType,
        bool isCollection,
        IReadOnlyList<IPropertyMetadata> foreignKey,
        IReadOnlyList<IPropertyMetadata> principalKey)
        where T : class
    {
        var built = new EntityMetadataBuilder<T>().Build();
        var properties = built.Properties
            .Where(p => navigation is null || p.PropertyInfo != navigation)
            .ToList();

        DataContextCache.Metadata[typeof(T)] = new EntityMetadata(
            built.TableName,
            properties,
            built.IsTableNameAuto,
            relationships:
            [
                new InjectedRelationship(kind, typeof(T), relatedType, navigation, isCollection, foreignKey, principalKey),
            ]);
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

    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop<T>(string name) => Props<T>().Single(p => p.PropertyInfo.Name == name);

    [Fact]
    public void CompositeOneToMany_ShouldRenderComponentwiseJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var navigation = typeof(CkParent).GetProperty(nameof(CkParent.Children))!;
        RegisterComposite<CkParent>(
            navigation,
            RelationshipKind.OneToMany,
            typeof(CkChild),
            isCollection: true,
            foreignKey: [Prop<CkChild>(nameof(CkChild.TenantId)), Prop<CkChild>(nameof(CkChild.ParentId))],
            principalKey: [Prop<CkParent>(nameof(CkParent.TenantId)), Prop<CkParent>(nameof(CkParent.Id))]);

        var command = ctx.From<CkParent>()
            .JoinInto(ctx.From<CkChild>(), (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId, p => p.Children)
            .ToCommand();
        var sql = Canon(SqlOf(ctx, command));

        sql.Should().Contain("from ck_parent as t1")
            .And.Contain("left join ck_child as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)");
        command.Cache.Should().BeTrue("declaring a composite JoinInto must not disable the plan cache");
    }

    [Fact]
    public void CompositeOneToOne_ShouldRenderComponentwiseJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var navigation = typeof(CkProfileParent).GetProperty(nameof(CkProfileParent.Profile))!;
        RegisterComposite<CkProfileParent>(
            navigation,
            RelationshipKind.OneToOne,
            typeof(CkProfile),
            isCollection: false,
            foreignKey: [Prop<CkProfile>(nameof(CkProfile.TenantId)), Prop<CkProfile>(nameof(CkProfile.ParentId))],
            principalKey: [Prop<CkProfileParent>(nameof(CkProfileParent.TenantId)), Prop<CkProfileParent>(nameof(CkProfileParent.Id))]);

        var command = ctx.From<CkProfileParent>()
            .JoinInto(ctx.From<CkProfile>(), (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId, p => p.Profile)
            .ToCommand();
        var sql = Canon(SqlOf(ctx, command));

        sql.Should().Contain("from ck_profile_parent as t1")
            .And.Contain("left join ck_profile as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)");
        command.Cache.Should().BeTrue("declaring a composite JoinInto must not disable the plan cache");
    }

    [Fact]
    public void CompositeOneToMany_NullComponent_ShouldRenderComponentwiseEqualityLeftJoin()
    {
        using var ctx = ClickHouseTestContext.Create();
        var navigation = typeof(CkNullParent).GetProperty(nameof(CkNullParent.Children))!;
        RegisterComposite<CkNullParent>(
            navigation,
            RelationshipKind.OneToMany,
            typeof(CkNullChild),
            isCollection: true,
            foreignKey: [Prop<CkNullChild>(nameof(CkNullChild.TenantId)), Prop<CkNullChild>(nameof(CkNullChild.ParentId))],
            principalKey: [Prop<CkNullParent>(nameof(CkNullParent.TenantId)), Prop<CkNullParent>(nameof(CkNullParent.Id))]);

        var command = ctx.From<CkNullParent>()
            .JoinInto(ctx.From<CkNullChild>(), (p, c) => p.TenantId == c.TenantId && p.Id == c.ParentId, p => p.Children)
            .ToCommand();
        var sql = Canon(SqlOf(ctx, command));

        // A NULL key component still renders as plain equality on every component (no null-safe operator,
        // no COALESCE): SQL evaluates NULL = NULL as false, so this is the provider-side record of the
        // documented in-memory/SQL divergence. The join stays a LEFT join, so an absent child contributes
        // only NULL columns and never a default-substituted row.
        sql.Should().Contain("from ck_null_parent as t1")
            .And.Contain("left join ck_null_child as t2 on (t1.tenant_id = t2.tenant_id and t1.id = t2.parent_id)");
        sql.Should().NotContain("coalesce")
            .And.NotContain("<=>")
            .And.NotContain("is not distinct");
        command.Cache.Should().BeTrue("a nullable composite JoinInto must not disable the plan cache");
    }
}

[SqlTable("jn_comp_parent")]
public sealed class JnCompositeParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<JnCompositeChild> Children { get; set; } = new List<JnCompositeChild>();

    public ICollection<JnCompositeNote> Notes { get; set; } = new List<JnCompositeNote>();
}

[SqlTable("jn_comp_child")]
public sealed class JnCompositeChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("jn_comp_link")]
public sealed class JnCompositeLink
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("jn_comp_note")]
public sealed class JnCompositeNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("text")]
    public string? Text { get; set; }
}

[SqlTable("ck_parent")]
public sealed class CkParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<CkChild> Children { get; set; } = new List<CkChild>();
}

[SqlTable("ck_child")]
public sealed class CkChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("ck_profile_parent")]
public sealed class CkProfileParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public CkProfile? Profile { get; set; }
}

[SqlTable("ck_profile")]
public sealed class CkProfile
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }
}

[SqlTable("ck_null_parent")]
public sealed class CkNullParent
{
    [Key]
    [Column("tenant_id")]
    public int? TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<CkNullChild> Children { get; set; } = new List<CkNullChild>();
}

[SqlTable("ck_null_child")]
public sealed class CkNullChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("tenant_id")]
    public int? TenantId { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
