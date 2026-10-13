using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D01 (#223) decisive carrier probe (SQLite half): a composite-junction many-to-many <c>JoinInto</c>
/// must prepare, generate SQL, execute, materialize and stitch through the existing entity-item route.
/// The parent key is the two-component [TenantId, Id]; the junction carries two parent foreign keys
/// [ParentId, ParentId2] and one child foreign key [ChildId]. On the retained pre-D01 source the same
/// query fails closed at <c>JunctionLinkSourceFactory.BuildSource</c>; with the flat arity-general
/// carrier it produces the correct graph. The second link row shares the parent's first component but
/// carries a different last component, so omitting the last component cross-links child 11 to parent 1
/// (the collision sensitivity oracle).
/// </summary>
[Collection("Query cache controls")]
public class JoinIntoCompositeCarrierSpikeProbeTests
{
    private static IReadOnlyList<IPropertyMetadata> Props<T>()
        => new EntityMetadataBuilder<T>().Build().Properties;

    private static IPropertyMetadata Prop(IReadOnlyList<IPropertyMetadata> properties, string name)
        => properties.First(p => p.PropertyInfo.Name == name);

    private static void RegisterProbeJunction()
    {
        var parentProps = Props<ProbeCjParent>();
        var childProps = Props<ProbeCjChild>();
        var linkProps = Props<ProbeCjJunction>();
        var tenant = Prop(parentProps, nameof(ProbeCjParent.TenantId)).PropertyInfo;
        var id = Prop(parentProps, nameof(ProbeCjParent.Id)).PropertyInfo;
        var childKey = Prop(childProps, nameof(ProbeCjChild.Id)).PropertyInfo;
        var navigation = typeof(ProbeCjParent).GetProperty(nameof(ProbeCjParent.Children));
        var builtParent = new EntityMetadataBuilder<ProbeCjParent>().Build();

        DataContextCache.Metadata[typeof(ProbeCjParent)] = new EntityMetadata(
            builtParent.TableName,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            builtParent.IsTableNameAuto,
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(ProbeCjParent),
                    typeof(ProbeCjChild),
                    navigation,
                    isCollection: true,
                    childKey,
                    id,
                    typeof(ProbeCjChild),
                    typeof(ProbeCjParent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(ProbeCjJunction),
                        ParentKey = [tenant, id],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(ProbeCjJunction.ParentId)).PropertyInfo,
                            Prop(linkProps, nameof(ProbeCjJunction.ParentId2)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(ProbeCjJunction.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    private static void RegisterThreeComponentJunction()
    {
        var parentProps = Props<ProbeCj3Parent>();
        var childProps = Props<ProbeCj3Child>();
        var linkProps = Props<ProbeCj3Junction>();
        var a = Prop(parentProps, nameof(ProbeCj3Parent.A)).PropertyInfo;
        var b = Prop(parentProps, nameof(ProbeCj3Parent.B)).PropertyInfo;
        var c = Prop(parentProps, nameof(ProbeCj3Parent.C)).PropertyInfo;
        var childKey = Prop(childProps, nameof(ProbeCj3Child.Id)).PropertyInfo;
        var navigation = typeof(ProbeCj3Parent).GetProperty(nameof(ProbeCj3Parent.Children));
        var builtParent = new EntityMetadataBuilder<ProbeCj3Parent>().Build();

        DataContextCache.Metadata[typeof(ProbeCj3Parent)] = new EntityMetadata(
            builtParent.TableName,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            builtParent.IsTableNameAuto,
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(ProbeCj3Parent),
                    typeof(ProbeCj3Child),
                    navigation,
                    isCollection: true,
                    childKey,
                    a,
                    typeof(ProbeCj3Child),
                    typeof(ProbeCj3Parent),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(ProbeCj3Junction),
                        ParentKey = [a, b, c],
                        ChildKey = [childKey],
                        JunctionParentForeignKey =
                        [
                            Prop(linkProps, nameof(ProbeCj3Junction.A)).PropertyInfo,
                            Prop(linkProps, nameof(ProbeCj3Junction.B)).PropertyInfo,
                            Prop(linkProps, nameof(ProbeCj3Junction.C)).PropertyInfo,
                        ],
                        JunctionChildForeignKey = [Prop(linkProps, nameof(ProbeCj3Junction.ChildId)).PropertyInfo],
                    }),
            ]);
    }

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-composite-carrier-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table probe_cj_parent (tenant_id integer, id integer, name text, primary key (tenant_id, id));" +
                "create table probe_cj_child (id integer primary key, name text);" +
                "create table probe_cj_link (parent_id integer, parent_id2 integer, child_id integer);" +
                "insert into probe_cj_parent (tenant_id, id, name) values (1, 1, 'p1'), (1, 2, 'p2');" +
                "insert into probe_cj_child (id, name) values (10, 'c10'), (11, 'c11');" +
                // Both links share the parent's first component (1); the second differs. The full composite
                // key must route child 10 to parent (1,1) and child 11 to parent (1,2) without cross-linking.
                "insert into probe_cj_link (parent_id, parent_id2, child_id) values (1, 1, 10), (1, 2, 11);";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    [Fact]
    public void CompositeJunctionManyToMany_SqlPath_ShouldExecuteAndStitchByFullKey()
    {
        DataContextCache.Clear();
        RegisterProbeJunction();
        var (ctx, path) = CreateDb();
        try
        {
            var parents = ctx.From<ProbeCjParent>()
                .JoinInto(ctx.From<ProbeCjChild>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2, "the two composite-key parents are distinct");
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(new[] { 10 },
                "the junction's last parent component routes child 10 to parent (1,1) only");
            parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(new[] { 11 },
                "the junction's last parent component routes child 11 to parent (1,2) only");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_ThreeParentComponents_ShouldCarryEveryComponent()
    {
        DataContextCache.Clear();
        RegisterThreeComponentJunction();

        var path = Path.Combine(Path.GetTempPath(), $"nextorm-composite-carrier3-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table probe_cj3_parent (a integer, b integer, c integer, name text, primary key (a, b, c));" +
                "create table probe_cj3_child (id integer primary key, name text);" +
                "create table probe_cj3_link (a integer, b integer, c integer, child_id integer);" +
                "insert into probe_cj3_parent (a, b, c, name) values (1, 1, 1, 'p1'), (1, 1, 2, 'p2');" +
                "insert into probe_cj3_child (id, name) values (10, 'c10'), (11, 'c11');" +
                "insert into probe_cj3_link (a, b, c, child_id) values (1, 1, 1, 10), (1, 1, 2, 11);";
            cmd.ExecuteNonQuery();
        }

        using var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        try
        {
            ctx.EnsureConnectionOpen();

            var parents = ctx.From<ProbeCj3Parent>()
                .JoinInto(ctx.From<ProbeCj3Child>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2, "the arity-general carrier must not impose a two-component maximum");
            parents.Single(p => p.C == 1).Children.Select(child => child.Id).Should().Equal(new[] { 10 });
            parents.Single(p => p.C == 2).Children.Select(child => child.Id).Should().Equal(new[] { 11 });
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // D223 TASK 1: SQL-execution closure of the plan §4 variant matrix on the arity-general carrier.
    // The in-memory sibling tests live in tests/nextorm.core.tests/JoinIntoCompositeRemainderTests.cs;
    // only the SQL path builds the emitted flat carrier, so these are the decisive "no silent
    // two-column limit" and reference/converter/null key-representation assertions.
    // ---------------------------------------------------------------------------------------------

    private static void RegisterJunction<TEntity, TChild, TJunction>(
        string navigationName,
        IReadOnlyList<string> parentKeyNames,
        IReadOnlyList<string> childKeyNames,
        IReadOnlyList<string> junctionParentNames,
        IReadOnlyList<string> junctionChildNames)
    {
        var parentProps = Props<TEntity>();
        var childProps = Props<TChild>();
        var linkProps = Props<TJunction>();
        var builtParent = new EntityMetadataBuilder<TEntity>().Build();
        var navigation = typeof(TEntity).GetProperty(navigationName);
        var parentKeys = parentKeyNames.Select(name => Prop(parentProps, name).PropertyInfo).ToArray();
        var childKeys = childKeyNames.Select(name => Prop(childProps, name).PropertyInfo).ToArray();

        DataContextCache.Metadata[typeof(TEntity)] = new EntityMetadata(
            builtParent.TableName,
            parentProps.Where(p => p.PropertyInfo != navigation).ToList(),
            builtParent.IsTableNameAuto,
            relationships:
            [
                new RelationshipMetadata(
                    RelationshipKind.ManyToMany,
                    typeof(TEntity),
                    typeof(TChild),
                    navigation,
                    isCollection: true,
                    childKeys[0],
                    parentKeys[0],
                    typeof(TChild),
                    typeof(TEntity),
                    new RelationshipJunctionDeclaration
                    {
                        JunctionType = typeof(TJunction),
                        ParentKey = parentKeys,
                        ChildKey = childKeys,
                        JunctionParentForeignKey = junctionParentNames.Select(name => Prop(linkProps, name).PropertyInfo).ToArray(),
                        JunctionChildForeignKey = junctionChildNames.Select(name => Prop(linkProps, name).PropertyInfo).ToArray(),
                    }),
            ]);
    }

    private static (SqliteDataContext Ctx, string Path) CreateJunctionDb(string ddl)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-composite-matrix-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = ddl;
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    [Fact]
    public void CompositeJunctionManyToMany_ArityOneTwo_ShouldCarryEveryChildComponent()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeC12Parent, ProbeC12Child, ProbeC12Link>(
            nameof(ProbeC12Parent.Children),
            [nameof(ProbeC12Parent.Id)],
            [nameof(ProbeC12Child.Pod), nameof(ProbeC12Child.Id)],
            [nameof(ProbeC12Link.ParentId)],
            [nameof(ProbeC12Link.ChildPod), nameof(ProbeC12Link.ChildId)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_c12_parent (id integer primary key, name text);" +
            "create table probe_c12_child (pod integer, id integer, name text, primary key (pod, id));" +
            "create table probe_c12_link (parent_id integer, child_pod integer, child_id integer);" +
            "insert into probe_c12_parent (id, name) values (1, 'p1');" +
            "insert into probe_c12_child (pod, id, name) values (1, 10, 'c10'), (1, 20, 'c20');" +
            "insert into probe_c12_link (parent_id, child_pod, child_id) values (1, 1, 10);");
        try
        {
            var parents = ctx.From<ProbeC12Parent>()
                .JoinInto(ctx.From<ProbeC12Child>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().ContainSingle();
            parents[0].Children.Select(c => c.Id).Should().Equal(new[] { 10 },
                "the carrier's last child component is part of the SQL match; child 20 shares only the first");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_ArityTwoTwo_ShouldCarryEveryParentAndChildComponent()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeC22Parent, ProbeC22Child, ProbeC22Link>(
            nameof(ProbeC22Parent.Children),
            [nameof(ProbeC22Parent.TenantId), nameof(ProbeC22Parent.Id)],
            [nameof(ProbeC22Child.Pod), nameof(ProbeC22Child.Id)],
            [nameof(ProbeC22Link.ParentTenant), nameof(ProbeC22Link.ParentId)],
            [nameof(ProbeC22Link.ChildPod), nameof(ProbeC22Link.ChildId)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_c22_parent (tenant_id integer, id integer, name text, primary key (tenant_id, id));" +
            "create table probe_c22_child (pod integer, id integer, name text, primary key (pod, id));" +
            "create table probe_c22_link (parent_tenant integer, parent_id integer, child_pod integer, child_id integer);" +
            "insert into probe_c22_parent (tenant_id, id, name) values (1, 1, 'p1'), (1, 2, 'p2');" +
            "insert into probe_c22_child (pod, id, name) values (1, 10, 'c10'), (1, 20, 'c20');" +
            "insert into probe_c22_link (parent_tenant, parent_id, child_pod, child_id) values (1, 1, 1, 10), (1, 2, 1, 20);");
        try
        {
            var parents = ctx.From<ProbeC22Parent>()
                .JoinInto(ctx.From<ProbeC22Child>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2, "the two composite-key parents are distinct");
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(new[] { 10 },
                "the last parent component routes child 10 to parent (1,1) only");
            parents.Single(p => p.Id == 2).Children.Select(c => c.Id).Should().Equal(new[] { 20 },
                "the last parent component routes child 20 to parent (1,2) only");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_ArityOneThree_ShouldCarryEveryChildComponent()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeC13Parent, ProbeC13Child, ProbeC13Link>(
            nameof(ProbeC13Parent.Children),
            [nameof(ProbeC13Parent.Id)],
            [nameof(ProbeC13Child.A), nameof(ProbeC13Child.B), nameof(ProbeC13Child.C)],
            [nameof(ProbeC13Link.ParentId)],
            [nameof(ProbeC13Link.ChildA), nameof(ProbeC13Link.ChildB), nameof(ProbeC13Link.ChildC)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_c13_parent (id integer primary key, name text);" +
            "create table probe_c13_child (a integer, b integer, c integer, name text, primary key (a, b, c));" +
            "create table probe_c13_link (parent_id integer, child_a integer, child_b integer, child_c integer);" +
            "insert into probe_c13_parent (id, name) values (1, 'p1');" +
            "insert into probe_c13_child (a, b, c, name) values (1, 1, 10, 'c10'), (1, 1, 20, 'c20');" +
            "insert into probe_c13_link (parent_id, child_a, child_b, child_c) values (1, 1, 1, 10);");
        try
        {
            var parents = ctx.From<ProbeC13Parent>()
                .JoinInto(ctx.From<ProbeC13Child>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().ContainSingle();
            parents[0].Children.Select(c => c.C).Should().Equal(new[] { 10 },
                "the third child component is part of the SQL match; the carrier has no two-column limit");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_ArityThreeThree_ShouldCarryEveryParentAndChildComponent()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeC33Parent, ProbeC33Child, ProbeC33Link>(
            nameof(ProbeC33Parent.Children),
            [nameof(ProbeC33Parent.A), nameof(ProbeC33Parent.B), nameof(ProbeC33Parent.C)],
            [nameof(ProbeC33Child.D), nameof(ProbeC33Child.E), nameof(ProbeC33Child.F)],
            [nameof(ProbeC33Link.ParentA), nameof(ProbeC33Link.ParentB), nameof(ProbeC33Link.ParentC)],
            [nameof(ProbeC33Link.ChildD), nameof(ProbeC33Link.ChildE), nameof(ProbeC33Link.ChildF)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_c33_parent (a integer, b integer, c integer, name text, primary key (a, b, c));" +
            "create table probe_c33_child (d integer, e integer, f integer, name text, primary key (d, e, f));" +
            "create table probe_c33_link (parent_a integer, parent_b integer, parent_c integer, child_d integer, child_e integer, child_f integer);" +
            "insert into probe_c33_parent (a, b, c, name) values (1, 1, 1, 'p1'), (1, 1, 2, 'p2');" +
            "insert into probe_c33_child (d, e, f, name) values (1, 1, 10, 'c10'), (1, 1, 20, 'c20');" +
            "insert into probe_c33_link (parent_a, parent_b, parent_c, child_d, child_e, child_f) values (1, 1, 1, 1, 1, 10), (1, 1, 2, 1, 1, 20);");
        try
        {
            var parents = ctx.From<ProbeC33Parent>()
                .JoinInto(ctx.From<ProbeC33Child>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2, "the arity-general carrier must not impose a two-component maximum");
            parents.Single(p => p.C == 1).Children.Select(c => c.F).Should().Equal(new[] { 10 },
                "the third parent and child components route child 10 to parent (1,1,1) only");
            parents.Single(p => p.C == 2).Children.Select(c => c.F).Should().Equal(new[] { 20 },
                "the third parent and child components route child 20 to parent (1,1,2) only");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_StringParentKey_ShouldStitchByReferenceKeyValue()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeStrParent, ProbeStrChild, ProbeStrLink>(
            nameof(ProbeStrParent.Children),
            [nameof(ProbeStrParent.Code)],
            [nameof(ProbeStrChild.Id)],
            [nameof(ProbeStrLink.ParentCode)],
            [nameof(ProbeStrLink.ChildId)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_str_parent (code text primary key, name text);" +
            "create table probe_str_child (id integer primary key, name text);" +
            "create table probe_str_link (parent_code text, child_id integer);" +
            "insert into probe_str_parent (code, name) values ('P1', 'p1'), ('P2', 'p2');" +
            "insert into probe_str_child (id, name) values (10, 'c10');" +
            "insert into probe_str_link (parent_code, child_id) values ('P1', 10);");
        try
        {
            var parents = ctx.From<ProbeStrParent>()
                .JoinInto(ctx.From<ProbeStrChild>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2);
            parents.Single(p => p.Code == "P1").Children.Select(c => c.Id).Should().Equal(new[] { 10 },
                "the string junction key matches by value on SQL");
            parents.Single(p => p.Code == "P2").Children.Should().BeEmpty("only the declared string key matches");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_NullParentComponent_ShouldNotCrossLink()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeNjParent, ProbeNjChild, ProbeNjLink>(
            nameof(ProbeNjParent.Children),
            [nameof(ProbeNjParent.TenantId), nameof(ProbeNjParent.Id)],
            [nameof(ProbeNjChild.Id)],
            [nameof(ProbeNjLink.ParentTenant), nameof(ProbeNjLink.ParentId)],
            [nameof(ProbeNjLink.ChildId)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_nj_parent (tenant_id integer, id integer, name text);" +
            "create table probe_nj_child (id integer primary key, name text);" +
            "create table probe_nj_link (parent_tenant integer, parent_id integer, child_id integer);" +
            "insert into probe_nj_parent (tenant_id, id, name) values (null, 1, 'nullp'), (1, 1, 'p1');" +
            "insert into probe_nj_child (id, name) values (10, 'c10'), (20, 'c20');" +
            "insert into probe_nj_link (parent_tenant, parent_id, child_id) values (1, 1, 10), (null, 1, 20);");
        try
        {
            var parents = ctx.From<ProbeNjParent>()
                .JoinInto(ctx.From<ProbeNjChild>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2);
            parents.Single(p => p.TenantId is null).Children.Should().BeEmpty(
                "SQL NULL = NULL is false, so a null junction component creates no association");
            parents.Single(p => p.TenantId == 1).Children.Select(c => c.Id).Should().Equal(new[] { 10 },
                "a null junction component must not attach to a parent whose component is a valid value");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void CompositeJunctionManyToMany_ConvertedParentKey_ShouldStitch()
    {
        DataContextCache.Clear();
        RegisterJunction<ProbeCvParent, ProbeCvChild, ProbeCvLink>(
            nameof(ProbeCvParent.Children),
            [nameof(ProbeCvParent.Id)],
            [nameof(ProbeCvChild.Id)],
            [nameof(ProbeCvLink.ParentId)],
            [nameof(ProbeCvLink.ChildId)]);

        var (ctx, path) = CreateJunctionDb(
            "create table probe_cv_parent (id text primary key, name text);" +
            "create table probe_cv_child (id integer primary key, name text);" +
            "create table probe_cv_link (parent_id text, child_id integer);" +
            "insert into probe_cv_parent (id, name) values ('1', 'p1'), ('2', 'p2');" +
            "insert into probe_cv_child (id, name) values (10, 'c10');" +
            "insert into probe_cv_link (parent_id, child_id) values ('1', 10);");
        try
        {
            var parents = ctx.From<ProbeCvParent>()
                .JoinInto(ctx.From<ProbeCvChild>(), (p, c) => true, p => p.Children)
                .ToList();

            parents.Should().HaveCount(2);
            parents.Single(p => p.Id == 1).Children.Select(c => c.Id).Should().Equal(new[] { 10 },
                "the supported value converter on the junction component round-trips through the carrier");
            parents.Single(p => p.Id == 2).Children.Should().BeEmpty("only the converted key '1' has a link");
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}

/// <summary>A supported value converter for the D223 key-representation matrix row: CLR int, provider text.</summary>
public sealed class IntToTextConverter : ValueConverter<int, string>
{
    public override string ConvertToProvider(int model) => model.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public override int ConvertFromProvider(string? provider) => int.Parse(provider!, System.Globalization.CultureInfo.InvariantCulture);
}

[SqlTable("probe_cj_parent")]
public sealed class ProbeCjParent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeCjChild> Children { get; set; } = new List<ProbeCjChild>();
}

[SqlTable("probe_cj_child")]
public sealed class ProbeCjChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_cj_link")]
public sealed class ProbeCjJunction
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("parent_id2")]
    public int ParentId2 { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("probe_cj3_parent")]
public sealed class ProbeCj3Parent
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Key]
    [Column("b")]
    public int B { get; set; }

    [Key]
    [Column("c")]
    public int C { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeCj3Child> Children { get; set; } = new List<ProbeCj3Child>();
}

[SqlTable("probe_cj3_child")]
public sealed class ProbeCj3Child
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_cj3_link")]
public sealed class ProbeCj3Junction
{
    [Column("a")]
    public int A { get; set; }

    [Column("b")]
    public int B { get; set; }

    [Column("c")]
    public int C { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

// --- D223 TASK 1 variant-matrix fixtures: arity 1/2, 2/2, 1/3, 3/3, string, nullable, converter ----

[SqlTable("probe_c12_parent")]
public sealed class ProbeC12Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeC12Child> Children { get; set; } = new List<ProbeC12Child>();
}

[SqlTable("probe_c12_child")]
public sealed class ProbeC12Child
{
    [Key]
    [Column("pod")]
    public int Pod { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_c12_link")]
public sealed class ProbeC12Link
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_pod")]
    public int ChildPod { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("probe_c22_parent")]
public sealed class ProbeC22Parent
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeC22Child> Children { get; set; } = new List<ProbeC22Child>();
}

[SqlTable("probe_c22_child")]
public sealed class ProbeC22Child
{
    [Key]
    [Column("pod")]
    public int Pod { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_c22_link")]
public sealed class ProbeC22Link
{
    [Column("parent_tenant")]
    public int ParentTenant { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_pod")]
    public int ChildPod { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("probe_c13_parent")]
public sealed class ProbeC13Parent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeC13Child> Children { get; set; } = new List<ProbeC13Child>();
}

[SqlTable("probe_c13_child")]
public sealed class ProbeC13Child
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Key]
    [Column("b")]
    public int B { get; set; }

    [Key]
    [Column("c")]
    public int C { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_c13_link")]
public sealed class ProbeC13Link
{
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_a")]
    public int ChildA { get; set; }

    [Column("child_b")]
    public int ChildB { get; set; }

    [Column("child_c")]
    public int ChildC { get; set; }
}

[SqlTable("probe_c33_parent")]
public sealed class ProbeC33Parent
{
    [Key]
    [Column("a")]
    public int A { get; set; }

    [Key]
    [Column("b")]
    public int B { get; set; }

    [Key]
    [Column("c")]
    public int C { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeC33Child> Children { get; set; } = new List<ProbeC33Child>();
}

[SqlTable("probe_c33_child")]
public sealed class ProbeC33Child
{
    [Key]
    [Column("d")]
    public int D { get; set; }

    [Key]
    [Column("e")]
    public int E { get; set; }

    [Key]
    [Column("f")]
    public int F { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_c33_link")]
public sealed class ProbeC33Link
{
    [Column("parent_a")]
    public int ParentA { get; set; }

    [Column("parent_b")]
    public int ParentB { get; set; }

    [Column("parent_c")]
    public int ParentC { get; set; }

    [Column("child_d")]
    public int ChildD { get; set; }

    [Column("child_e")]
    public int ChildE { get; set; }

    [Column("child_f")]
    public int ChildF { get; set; }
}

[SqlTable("probe_str_parent")]
public sealed class ProbeStrParent
{
    [Key]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeStrChild> Children { get; set; } = new List<ProbeStrChild>();
}

[SqlTable("probe_str_child")]
public sealed class ProbeStrChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_str_link")]
public sealed class ProbeStrLink
{
    [Column("parent_code")]
    public string ParentCode { get; set; } = string.Empty;

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("probe_nj_parent")]
public sealed class ProbeNjParent
{
    [Key]
    [Column("tenant_id")]
    public int? TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeNjChild> Children { get; set; } = new List<ProbeNjChild>();
}

[SqlTable("probe_nj_child")]
public sealed class ProbeNjChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_nj_link")]
public sealed class ProbeNjLink
{
    [Column("parent_tenant")]
    public int? ParentTenant { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}

[SqlTable("probe_cv_parent")]
public sealed class ProbeCvParent
{
    [Key]
    [Column("id")]
    [ValueConverter(typeof(IntToTextConverter))]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<ProbeCvChild> Children { get; set; } = new List<ProbeCvChild>();
}

[SqlTable("probe_cv_child")]
public sealed class ProbeCvChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("probe_cv_link")]
public sealed class ProbeCvLink
{
    [Column("parent_id")]
    [ValueConverter(typeof(IntToTextConverter))]
    public int ParentId { get; set; }

    [Column("child_id")]
    public int ChildId { get; set; }
}
