using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// T357: many-to-many per-parent paging. Both surfaces — the collection-projection slot adapter's
/// <c>Limit</c> and the metadata <c>LoadWith</c> child's <c>Limit</c>/<c>Offset</c> — page the
/// <b>surviving junction occurrences</b> of each parent: a repeated junction row repeats its child and
/// occupies its own slot, a dangling link (one whose child row is gone) is absent from the count and from
/// the page, and a parent with no surviving row gets an empty collection rather than being dropped.
/// <para>
/// The split loader reads the junction and the children in batched statements and pages the grouped
/// occurrences afterwards; the child statement's own page is cleared so it can never become a global
/// <c>LIMIT</c>. The single-query loader pages the stitched occurrences after the link-aware de-duplication.
/// The statements below are recorded, so "no global limit" is observed rather than asserted from intent.
/// </para>
/// </summary>
[Collection("Query cache controls")]
public sealed class ManyToManyPagingTests
{
    public ManyToManyPagingTests() => DataContextCache.Clear();

    [SqlTable("t357_parent")]
    public sealed class PagedParent
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        public ICollection<PagedTag> Tags { get; } = new List<PagedTag>();
    }

    [SqlTable("t357_tag")]
    public sealed class PagedTag
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }
    }

    [SqlTable("t357_parent_tag")]
    public sealed class PagedLink
    {
        [Column("parent_id")]
        public int ParentId { get; set; }

        [Column("tag_id")]
        public int TagId { get; set; }
    }

    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
            => Statements.Add(command.CommandText.Replace("\r\n", "\n"));
    }

    /// <summary>
    /// Parent <c>1</c> links tag <c>100</c> twice (two surviving occurrences) and tag <c>101</c> once, plus a
    /// dangling link to the missing tag <c>999</c>; parent <c>2</c> links tag <c>102</c> once; parent <c>3</c>
    /// has only the dangling link; parent <c>4</c> has no junction row at all.
    /// </summary>
    private static (SqliteDataContext Ctx, string Path, SqlRecordingInterceptor Recorder) CreateDb(
        EagerLoadMode mode = EagerLoadMode.SplitQuery)
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-t357-mn-paging-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table t357_parent (id integer primary key, name text null);" +
                "create table t357_tag (id integer primary key, name text null);" +
                "create table t357_parent_tag (parent_id int not null, tag_id int not null);" +
                "insert into t357_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3'), (4, 'p4');" +
                "insert into t357_tag (id, name) values (100, 'a'), (101, 'b'), (102, 'c');" +
                "insert into t357_parent_tag (parent_id, tag_id) values" +
                " (1, 100), (1, 100), (1, 101), (1, 999), (2, 102), (3, 999);";
            cmd.ExecuteNonQuery();
        }

        var context = new SqliteDataContext(
            $"Data Source={path}",
            new DataContextBuilder().UseEagerLoadMode(mode));
        var recorder = new SqlRecordingInterceptor();
        context.AddInterceptor(recorder);
        return (context, path, recorder);
    }

    private static void Register(SqliteDataContext context)
        => _ = context.From<PagedParent>(cfg => cfg.HasManyThrough<PagedTag, PagedLink, int, int>(
            p => p.Tags,
            p => p.Id,
            l => l.ParentId,
            t => t.Id,
            l => l.TagId));

    [Fact]
    public void SlotAdapterLimit_Split_ShouldPageSurvivingOccurrencesPerParent()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            Register(context);

            var rows = context.From<PagedParent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Tags = p.Tags.AsEntityBuilder<PagedTag>().OrderBy(t => t.Id).Limit(2).ToList(),
                })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
            rows[0].Tags.Select(t => t.Id).Should().Equal([100, 100],
                "the repeated junction row occupies its own slot and the page keeps both occurrences");
            rows[1].Tags.Select(t => t.Id).Should().Equal([102], "a parent with fewer occurrences than the page keeps them all");
            rows[2].Tags.Should().NotBeNull().And.BeEmpty("a dangling link leaves no surviving occurrence");
            rows[3].Tags.Should().NotBeNull().And.BeEmpty();
            recorder.Statements.Should().HaveCount(3, "split many-to-many: parent + junction + tag statements");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void SlotAdapterLimit_Single_ShouldPageSurvivingOccurrencesPerParent()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            Register(context);

            var rows = context.From<PagedParent>()
                .OrderBy(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    Tags = p.Tags.AsEntityBuilder<PagedTag>().OrderBy(t => t.Id).Limit(2).ToList(),
                })
                .ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2, 3, 4);
            rows[0].Tags.Select(t => t.Id).Should().Equal([100, 100]);
            rows[1].Tags.Select(t => t.Id).Should().Equal([102]);
            rows[2].Tags.Should().NotBeNull().And.BeEmpty();
            rows[3].Tags.Should().NotBeNull().And.BeEmpty();
            recorder.Statements.Should().HaveCount(1, "single many-to-many: one statement for the whole graph");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithPage_Split_ShouldPageSurvivingOccurrencesAcrossTheBoundary()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            Register(context);

            var parents = context.From<PagedParent>()
                .LoadWith(
                    p => p.Tags,
                    t => t.From<PagedTag>().OrderBy(x => x.Id).Offset(1).Limit(2))
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3, 4);
            parents[0].Tags.Select(t => t.Id).Should().Equal([100, 101],
                "offset 1 skips the first surviving occurrence and the page crosses the boundary");
            parents[1].Tags.Should().BeEmpty("offset 1 removes the only surviving occurrence of this parent");
            parents[2].Tags.Should().BeEmpty();
            parents[3].Tags.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(3);
            AssertNoGlobalPage(recorder.Statements);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithLimit_Single_ShouldPageSurvivingOccurrencesPerParent()
    {
        var (context, path, recorder) = CreateDb(EagerLoadMode.SingleQuery);
        try
        {
            Register(context);

            var parents = context.From<PagedParent>()
                .LoadWith(p => p.Tags, t => t.From<PagedTag>().OrderBy(x => x.Id).Limit(2))
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2, 3, 4);
            parents[0].Tags.Select(t => t.Id).Should().Equal([100, 100]);
            parents[1].Tags.Select(t => t.Id).Should().Equal([102]);
            parents[2].Tags.Should().BeEmpty();
            parents[3].Tags.Should().BeEmpty();
            recorder.Statements.Should().HaveCount(1);
            AssertNoGlobalPage(recorder.Statements);
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithPage_WithoutOrdering_IsRefusedBeforeAnyStatement()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            Register(context);

            Action paged = () => context.From<PagedParent>()
                .LoadWith(p => p.Tags, t => t.From<PagedTag>().Limit(1))
                .ToList();

            paged.Should().Throw<NotSupportedException>().WithMessage("*without an OrderBy*");
            recorder.Statements.Should().BeEmpty("an unordered per-parent page is refused before any SQL");
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void LoadWithPage_WithTies_IsRefusedBeforeAnyStatement()
    {
        var (context, path, recorder) = CreateDb();
        try
        {
            Register(context);

            Action paged = () => context.From<PagedParent>()
                .LoadWith(p => p.Tags, t => t.From<PagedTag>().OrderBy(x => x.Id).Limit(1).WithTies())
                .ToList();

            paged.Should().Throw<NotSupportedException>().WithMessage("*WithTies*");
            recorder.Statements.Should().BeEmpty();
        }
        finally
        {
            context.Dispose();
            File.Delete(path);
        }
    }

    /// <summary>
    /// A per-parent page must never reach a statement as a global page: a global <c>LIMIT</c>/<c>TOP</c>
    /// would silently truncate every parent after the first. The page is trimmed from each parent's
    /// surviving occurrence list instead.
    /// </summary>
    private static void AssertNoGlobalPage(IReadOnlyList<string> statements)
    {
        foreach (var sql in statements)
        {
            sql.Should().NotMatchRegex("(?i)\\b(limit|top|fetch first)\\b",
                "a per-parent page is applied per parent, never as a global page: {0}", sql);
        }
    }
}
