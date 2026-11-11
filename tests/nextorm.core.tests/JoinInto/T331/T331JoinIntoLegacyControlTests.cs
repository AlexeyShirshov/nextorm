using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T331 AC09 control: the five original <c>JoinInto</c> overloads and their five T330 source-less
/// counterparts keep their binding, their supported join kinds and their behaviour. The rendered SQL of
/// every legacy shape is pinned as an exact string captured from the untouched tree, so a change to a
/// legacy overload body (or a new key conjunct leaking into a legacy <c>ON</c>) fails this class.
/// <para>
/// The legacy contract is deliberately <b>not</b> the navigation-first contract: a caller-supplied
/// predicate is the whole <c>ON</c> condition, it is never merged with the declared relationship keys.
/// <see cref="LegacyCompositePredicate_ShouldRemainTheOnlyOnEquality"/> pins that boundary, because the
/// navigation-first overloads added by T331 must not retro-fit key synthesis into these forms.
/// </para>
/// <para>
/// Runs in the "Query cache controls" collection because it writes the process-wide
/// <see cref="DataContextCache"/>.
/// </para>
/// </summary>
[Collection("Query cache controls")]
[Trait("Category", "T331")]
public class T331JoinIntoLegacyControlTests
{
    public T331JoinIntoLegacyControlTests()
    {
        DataContextCache.Clear();
    }

    // ---------------------------------------------------------------------------------------------
    // Fixtures
    // ---------------------------------------------------------------------------------------------

    public sealed class CtrlParent
    {
        public int Id { get; set; }

        public string? Name { get; set; }

        public ICollection<CtrlChild> Children { get; set; } = new List<CtrlChild>();

        public ICollection<CtrlTag> Tags { get; set; } = new List<CtrlTag>();

        public CtrlDetail? Detail { get; set; }
    }

    public sealed class CtrlChild
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string? Name { get; set; }
    }

    public sealed class CtrlDetail
    {
        public int Id { get; set; }

        public int ParentId { get; set; }

        public string? Name { get; set; }
    }

    public sealed class CtrlTag
    {
        public int Id { get; set; }
    }

    public sealed class CtrlTagLink
    {
        public int ParentId { get; set; }

        public int TagId { get; set; }
    }

    [SqlTable("t331_ctrl_blog")]
    public sealed class CtrlBlog
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Id { get; set; }

        public ICollection<CtrlPost> Posts { get; set; } = new List<CtrlPost>();
    }

    [SqlTable("t331_ctrl_post")]
    public sealed class CtrlPost
    {
        [Key]
        public int TenantId { get; set; }

        [Key]
        public int Id { get; set; }

        public int ParentId { get; set; }
    }

    private static void RegisterLegacy()
    {
        var parent = new EntityMetadataBuilder<CtrlParent>();
        parent.HasMany(p => p.Children, c => c.ParentId)
            .HasOneToOne(p => p.Detail, p => p.Id, d => d.ParentId)
            .HasManyThrough<CtrlTag, CtrlTagLink>(
                p => p.Tags,
                p => p.Id,
                l => l.ParentId,
                t => t.Id,
                l => l.TagId);
        DataContextCache.Metadata[typeof(CtrlParent)] = parent.Build();
        DataContextCache.Metadata[typeof(CtrlChild)] = new EntityMetadataBuilder<CtrlChild>().Build();
        DataContextCache.Metadata[typeof(CtrlDetail)] = new EntityMetadataBuilder<CtrlDetail>().Build();
        DataContextCache.Metadata[typeof(CtrlTag)] = new EntityMetadataBuilder<CtrlTag>().Build();
        DataContextCache.Metadata[typeof(CtrlTagLink)] = new EntityMetadataBuilder<CtrlTagLink>().Build();
    }

    private static void RegisterComposite()
    {
        var blog = new EntityMetadataBuilder<CtrlBlog>();
        blog.HasMany(b => b.Posts, p => new { p.TenantId, p.ParentId });
        DataContextCache.Metadata[typeof(CtrlBlog)] = blog.Build();
        DataContextCache.Metadata[typeof(CtrlPost)] = new EntityMetadataBuilder<CtrlPost>().Build();
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(
                command,
                createEnumerator: false,
                storeInCache: false,
                TestContext.Current.CancellationToken))
            .DbCommand.CommandText
            .Replace("\r\n", "\n");

    /// <summary>
    /// Renders the ten legacy shapes (five original overloads plus the five T330 source-less
    /// counterparts) and the two junction shapes, in a fixed order, as one canonical text. The same
    /// function produced the before- and after-implementation captures, so a byte-identical result is a
    /// real no-change proof rather than a restated expectation.
    /// </summary>
    internal static string DumpLegacyShapes()
    {
        RegisterLegacy();
        using var ctx = new RootProjectionTests.TestContext();
        var lines = new List<string>();

        void Add(string name, EntityBuilder<CtrlParent> builder)
        {
            lines.Add($"## {name}");
            lines.Add(SqlOf(ctx, builder.ToCommand()));
            lines.Add(string.Empty);
        }

        Add(
            "01-explicit-collection-default-left",
            ctx.From<CtrlParent>().JoinInto(ctx.From<CtrlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children));
        Add(
            "02-explicit-collection-inner",
            ctx.From<CtrlParent>().JoinInto(ctx.From<CtrlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner));
        Add(
            "03-explicit-reference-default-left",
            ctx.From<CtrlParent>().JoinInto(ctx.From<CtrlDetail>(), (p, c) => p.Id == c.ParentId, p => p.Detail));
        Add(
            "04-explicit-reference-inner",
            ctx.From<CtrlParent>().JoinInto(ctx.From<CtrlDetail>(), (p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner));
        Add(
            "05-explicit-key-form",
            ctx.From<CtrlParent>().JoinInto<CtrlChild, int>(
                ctx.From<CtrlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId));
        Add(
            "06-sourceless-collection-default-left",
            ctx.From<CtrlParent>().JoinInto<CtrlChild>((p, c) => p.Id == c.ParentId, p => p.Children));
        Add(
            "07-sourceless-collection-inner",
            ctx.From<CtrlParent>().JoinInto<CtrlChild>((p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Inner));
        Add(
            "08-sourceless-reference-default-left",
            ctx.From<CtrlParent>().JoinInto<CtrlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail));
        Add(
            "09-sourceless-reference-inner",
            ctx.From<CtrlParent>().JoinInto<CtrlDetail>((p, c) => p.Id == c.ParentId, p => p.Detail, JoinType.Inner));
        Add(
            "10-sourceless-key-form",
            ctx.From<CtrlParent>().JoinInto<CtrlChild, int>(
                (p, c) => p.Id == c.ParentId, p => p.Children, p => p.Id, c => c.ParentId));
        Add(
            "11-explicit-junction-default-left",
            ctx.From<CtrlParent>().JoinInto(ctx.From<CtrlTag>(), (p, c) => true, p => p.Tags));
        Add(
            "12-sourceless-junction-default-left",
            ctx.From<CtrlParent>().JoinInto<CtrlTag>((p, c) => true, p => p.Tags));

        return string.Join("\n", lines).TrimEnd('\n');
    }

    // ---------------------------------------------------------------------------------------------
    // AC09 — the ten legacy shapes render exactly what they rendered before T331.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AC09_LegacyShapes_ShouldRenderThePinnedPreChangeSql()
    {
        DumpLegacyShapes().Should().Be(
            PinnedLegacySql.ReplaceLineEndings("\n"),
            "the pinned capture is stored in a CRLF source file; only the line terminator is normalized");
    }

    [Fact]
    public void AC09_LegacyCollectionPredicate_ShouldRemainTheOnlyOnEquality()
    {
        RegisterLegacy();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = SqlOf(
            ctx,
            ctx.From<CtrlParent>()
                .JoinInto(ctx.From<CtrlChild>(), (p, c) => p.Id == c.ParentId, p => p.Children)
                .ToCommand());

        sql.Should().Contain("on t1.Id = t2.ParentId", "the caller predicate is the whole ON condition");
        sql.Should().NotContain("t2.Name", "no declared key is synthesized into a legacy ON");
    }

    [Fact]
    public void LegacyCompositePredicate_ShouldRemainTheOnlyOnEquality()
    {
        RegisterComposite();
        using var ctx = new RootProjectionTests.TestContext();

        // The navigation declares a two-component key [TenantId, Id] -> [TenantId, ParentId], but the
        // legacy contract takes the caller predicate verbatim: exactly one equality, without the tenant
        // component the navigation-first form would add.
        var sql = SqlOf(
            ctx,
            ctx.From<CtrlBlog>()
                .JoinInto(ctx.From<CtrlPost>(), (b, p) => b.Id == p.ParentId, b => b.Posts)
                .ToCommand());

        sql.Should().Contain("on t1.Id = t2.ParentId");
        sql.Should().NotContain("t1.TenantId = t2.TenantId", "legacy ON is never merged with declared keys");
    }

    [Fact]
    public void LegacyShapes_ShouldNotGainAKeyConjunct_ForASourcelessCall()
    {
        RegisterComposite();
        using var ctx = new RootProjectionTests.TestContext();

        var sql = SqlOf(
            ctx,
            ctx.From<CtrlBlog>()
                .JoinInto<CtrlPost>((b, p) => b.Id == p.ParentId, b => b.Posts)
                .ToCommand());

        sql.Should().Contain("on t1.Id = t2.ParentId");
        sql.Should().NotContain("t1.TenantId = t2.TenantId");
    }

    /// <summary>
    /// The SQL rendered by <see cref="DumpLegacyShapes"/> on the untouched tree (T331 base
    /// <c>d577d384</c>), captured before any T331 source edit. Do not regenerate this from a modified
    /// tree: a mismatch is the signal that a legacy overload changed.
    /// </summary>
    private const string PinnedLegacySql = """
## 01-explicit-collection-default-left
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join CtrlChild as 't2' on t1.Id = t2.ParentId

## 02-explicit-collection-inner
select t1.Id, t1.Name
 from CtrlParent as 't1'
 join CtrlChild as 't2' on t1.Id = t2.ParentId

## 03-explicit-reference-default-left
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join CtrlDetail as 't2' on t1.Id = t2.ParentId

## 04-explicit-reference-inner
select t1.Id, t1.Name
 from CtrlParent as 't1'
 join CtrlDetail as 't2' on t1.Id = t2.ParentId

## 05-explicit-key-form
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join CtrlChild as 't2' on t1.Id = t2.ParentId

## 06-sourceless-collection-default-left
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join CtrlChild as 't2' on t1.Id = t2.ParentId

## 07-sourceless-collection-inner
select t1.Id, t1.Name
 from CtrlParent as 't1'
 join CtrlChild as 't2' on t1.Id = t2.ParentId

## 08-sourceless-reference-default-left
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join CtrlDetail as 't2' on t1.Id = t2.ParentId

## 09-sourceless-reference-inner
select t1.Id, t1.Name
 from CtrlParent as 't1'
 join CtrlDetail as 't2' on t1.Id = t2.ParentId

## 10-sourceless-key-form
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join CtrlChild as 't2' on t1.Id = t2.ParentId

## 11-explicit-junction-default-left
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join (select ParentId as 'ParentKey', TagId as 'ChildKey', row_number() over (partition by ParentId order by TagId) as 'Occurrence'
 from CtrlTagLink) as 't2' on t1.Id = t2.ParentKey
 left join CtrlTag as 't3' on (t3.Id = t2.ChildKey and 1)

## 12-sourceless-junction-default-left
select t1.Id, t1.Name
 from CtrlParent as 't1'
 left join (select ParentId as 'ParentKey', TagId as 'ChildKey', row_number() over (partition by ParentId order by TagId) as 'Occurrence'
 from CtrlTagLink) as 't2' on t1.Id = t2.ParentKey
 left join CtrlTag as 't3' on (t3.Id = t2.ChildKey and 1)
""";
}
