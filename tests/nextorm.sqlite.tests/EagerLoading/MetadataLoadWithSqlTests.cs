using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// T249-D4 single execution of the metadata-driven <c>LoadWith</c> against a real SQLite database: one
/// denormalized command per level, left-preserving joins, positional full-key correlation, hidden
/// correlation/rank columns kept out of the public projection, nested and per-parent paged child shapes,
/// occurrence-aware many-to-many stitching and mode resolution. The legacy explicit-key single-query path
/// is not exercised here; its own suites are the unchanged control.
/// </summary>
[Collection("Query plan cache")]
public class MetadataLoadWithSqlTests
{
    private sealed class SqlRecordingInterceptor : IQueryInterceptor
    {
        public List<string> Statements { get; } = [];

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Statements.Add(command.CommandText);
    }

    private static readonly string[] Schema =
    [
        "create table meta_sql_blog (tenant_id integer not null, id integer not null, name text, primary key (tenant_id, id));",
        "create table meta_sql_post (tenant_id integer not null, id integer not null, blog_id integer not null, sort_key int not null, title text, author_id int not null, primary key (tenant_id, id));",
        "create table meta_sql_author (id integer primary key, name text);",
        "create table meta_sql_comment (tenant_id integer not null, id integer not null, post_id integer not null, body text, primary key (tenant_id, id));",
        "create table meta_sql_tag_parent (id integer primary key, name text);",
        "create table meta_sql_tag (id integer primary key, name text);",
        "create table meta_sql_tag_link (parent_id int not null, tag_id int not null);",
        "create table meta_sql_simple_parent (id integer primary key, name text);",
        "create table meta_sql_simple_child (id integer primary key, parent_id int not null, sort_key int not null, name text);",

        // Blogs: tenant 7 has (1,2,3); tenant 8 owns a blog with the same id 1 (full-key proof);
        // tenant 9 owns the one-to-one parent. Blog 2 has no posts (left-preservation proof).
        "insert into meta_sql_blog (tenant_id, id, name) values (7,1,'a'),(7,2,'b'),(7,3,'c'),(8,1,'d'),(9,5,'e'),(10,6,'f');",
        "insert into meta_sql_author (id, name) values (301,'author-a'),(302,'author-b');",
        "insert into meta_sql_post (tenant_id, id, blog_id, sort_key, title, author_id) values " +
        "(7,101,1,2,'a2',301),(7,102,1,1,'a1',301),(7,103,3,1,'c1',302),(8,104,1,1,'d1',302),(7,106,999,9,'dangling',999);",
        "insert into meta_sql_post (tenant_id, id, blog_id, sort_key, title, author_id) values (9,105,5,1,'primary',301);",
        "insert into meta_sql_comment (tenant_id, id, post_id, body) values (7,201,101,'x'),(7,202,101,'y'),(7,203,103,'z');",

        // Many-to-many: parent 1 links tag 501 twice (a repeated physical link) and tag 502 once;
        // parent 2 has no link.
        "insert into meta_sql_tag_parent (id, name) values (1,'p1'),(2,'p2');",
        "insert into meta_sql_tag (id, name) values (501,'t1'),(502,'t2');",
        "insert into meta_sql_tag_link (parent_id, tag_id) values (1,501),(1,501),(1,502);",

        // Single-component paging: parent 1 owns two ordered children, parent 2 owns one.
        "insert into meta_sql_simple_parent (id, name) values (1,'s1'),(2,'s2');",
        "insert into meta_sql_simple_child (id, parent_id, sort_key, name) values (11,1,2,'c2'),(12,1,1,'c1'),(13,2,1,'c3');",
    ];

    private static (SqliteDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-metadata-sql-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            foreach (var statement in Schema)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.ExecuteNonQuery();
            }
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        Register(ctx);
        return (ctx, path);
    }

    private static void Register(IDataContext ctx)
    {
        ctx.From<MetaSqlBlog>(b => b
            .HasMany(blog => blog.Posts, post => new { post.TenantId, post.BlogId }, blog => new { blog.TenantId, blog.Id })
            .HasOneToOne(blog => blog.PrimaryPost, blog => new { blog.TenantId, blog.Id }, post => new { post.TenantId, post.BlogId }));
        ctx.From<MetaSqlPost>(b => b
            .HasOne(post => post.Blog, post => new { post.TenantId, post.BlogId }, blog => new { blog.TenantId, blog.Id })
            .HasMany(post => post.Comments, comment => new { comment.TenantId, comment.PostId }, post => new { post.TenantId, post.Id })
            .HasOne(post => post.Author, post => post.AuthorId, author => author.Id));
        ctx.From<MetaSqlAuthor>();
        ctx.From<MetaSqlComment>();
        ctx.From<MetaSqlTagParent>(b => b
            .HasManyThrough<MetaSqlTag, MetaSqlTagLink>(
                parent => parent.Tags,
                parent => new { parent.Id },
                link => new { link.ParentId },
                tag => new { tag.Id },
                link => new { link.TagId }));
        ctx.From<MetaSqlTag>();
        ctx.From<MetaSqlTagLink>();
        ctx.From<MetaSqlSimpleParent>(b => b.HasMany(parent => parent.Children, child => child.ParentId));
        ctx.From<MetaSqlSimpleChild>();
    }

    private static string Canon(string sql)
    {
        var s = sql.Replace("\r\n", "\n").Replace("\"", string.Empty).Replace("`", string.Empty)
            .Replace("[", string.Empty).Replace("]", string.Empty);
        return System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
    }

    private static int Count(string text, string needle)
    {
        var count = 0;
        for (var index = text.IndexOf(needle, StringComparison.Ordinal); index >= 0; index = text.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static void WithDb(Action<SqliteDataContext, SqlRecordingInterceptor> body)
    {
        var (ctx, path) = CreateDb();
        var interceptor = new SqlRecordingInterceptor();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            body(ctx, interceptor);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    private static async Task WithDbAsync(Func<SqliteDataContext, SqlRecordingInterceptor, Task> body)
    {
        var (ctx, path) = CreateDb();
        var interceptor = new SqlRecordingInterceptor();
        ctx.AddInterceptor(interceptor);
        try
        {
            ctx.PurgeQueryCache();
            await body(ctx, interceptor);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 1:N collection: one command, LEFT JOIN, parents without children retained.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_SingleShape_Collection_IssuesOneLeftJoinCommandAndKeepsChildlessParents()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id >= 1 && b.Id <= 3)
                .LoadWith(b => b.Posts, EagerLoadMode.SingleQuery)
                .OrderBy(b => b.Id)
                .ToList();

            blogs.Select(b => b.Id).Should().Equal([1, 2, 3], "a child filter or join never filters parents");
            blogs[0].Posts.Select(p => p.Id).Should().BeEquivalentTo([101, 102]);
            blogs[1].Posts.Should().BeEmpty("blog 2 has no children");
            blogs[2].Posts.Select(p => p.Id).Should().Equal(103);

            interceptor.Statements.Should().HaveCount(1, "single execution issues one denormalized command");
            var sql = Canon(interceptor.Statements[0]);
            sql.Should().Contain("left join meta_sql_post").And.NotContain(" in (");
        });

    [Fact]
    public void MetadataLoadWith_SingleShape_Collection_PairsEveryCompositeComponentPositionally()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, EagerLoadMode.SingleQuery)
                .ToList();

            blogs.Should().ContainSingle().Which.Posts.Select(p => p.Id).Should().BeEquivalentTo([101, 102]);

            var sql = Canon(interceptor.Statements[0]);
            sql.Should().Contain("t1.tenant_id = t2.tenant_id", "component 1 pairs with component 1");
            sql.Should().Contain("t1.id = t2.blog_id", "component 2 pairs with component 2");
            sql.IndexOf("t1.tenant_id = t2.tenant_id", StringComparison.Ordinal)
                .Should().BeLessThan(sql.IndexOf("t1.id = t2.blog_id", StringComparison.Ordinal));

            // The same id under another tenant is a different key: a first-component-only pair would
            // merge the two blogs.
            var tenant8 = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 8 && b.Id == 1)
                .LoadWith(b => b.Posts, EagerLoadMode.SingleQuery)
                .ToList();
            tenant8.Should().ContainSingle().Which.Posts.Select(p => p.Id).Should().Equal(104);
        });

    // ---------------------------------------------------------------------------------------------
    // Hidden correlation/rank/key columns never reach the public projection.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_HiddenCorrelation_OuterSelectCarriesOnlyTheMappedEntityColumns()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, EagerLoadMode.SingleQuery)
                .ToList();

            blogs.Should().ContainSingle();

            var sql = Canon(interceptor.Statements[0]);
            var select = sql[..sql.IndexOf(" from ", StringComparison.Ordinal)];
            select.Should().NotContain("row_number").And.NotContain("__").And.NotContain("occurrence")
                .And.NotContain("partition by");
            Count(select, "t1.").Should().Be(3, "blog maps three columns and no hidden key/rank column leaks");
            Count(select, "t2.").Should().Be(6, "post maps six columns and no hidden key/rank column leaks");

            // Materialized surface: exactly the mapped navigation and scalar members, no injected member.
            typeof(MetaSqlBlog).GetProperties().Select(p => p.Name)
                .Should().BeEquivalentTo(["TenantId", "Id", "Name", "Posts", "PrimaryPost"]);
        });

    [Fact]
    public void MetadataLoadWith_HiddenCorrelation_PerParentPagedShape_KeepsRankInsideTheDerivedSource()
        => WithDb((ctx, interceptor) =>
        {
            var parents = ctx.From<MetaSqlSimpleParent>()
                .Where(p => p.Id >= 1 && p.Id <= 2)
                .LoadWith(p => p.Children, c => c.From<MetaSqlSimpleChild>().OrderBy(x => x.SortKey).Limit(1), EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => new { p.Id, Children = p.Children.Select(c => c.Id).ToArray() })
                .Should().BeEquivalentTo([
                    new { Id = 1, Children = new[] { 12 } },
                    new { Id = 2, Children = new[] { 13 } },
                ]);

            interceptor.Statements.Should().HaveCount(1);
            var sql = Canon(interceptor.Statements[0]);
            sql.Should().Contain("row_number()").And.Contain("partition by");
            var outerSelect = sql[..sql.IndexOf(" from ", StringComparison.Ordinal)];
            outerSelect.Should().NotContain("row_number").And.NotContain("partition by").And.NotContain("__");
        });

    // ---------------------------------------------------------------------------------------------
    // Reference routes (N:1 and 1:1): LEFT JOIN, null for zero matches, at most one child.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_SingleShape_Reference_IssuesOneLeftJoinAndNullsTheUnmatchedParent()
        => WithDb((ctx, interceptor) =>
        {
            var posts = ctx.From<MetaSqlPost>()
                .Where(p => p.Id == 101 || p.Id == 106)
                .LoadWith(p => p.Blog!, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            posts.Should().HaveCount(2);
            posts.Single(p => p.Id == 101).Blog!.Name.Should().Be("a");
            posts.Single(p => p.Id == 106).Blog.Should().BeNull("a dangling foreign key has no principal");

            interceptor.Statements.Should().HaveCount(1);
            var sql = Canon(interceptor.Statements[0]);
            sql.Should().Contain("left join meta_sql_blog");
            sql.Should().Contain("t1.tenant_id = t2.tenant_id").And.Contain("t1.blog_id = t2.id");
        });

    [Fact]
    public void MetadataLoadWith_SingleShape_OneToOne_IssuesOneLeftJoinAndNullsTheUnmatchedParent()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => (b.TenantId == 9 && b.Id == 5) || (b.TenantId == 10 && b.Id == 6))
                .LoadWith(b => b.PrimaryPost, EagerLoadMode.SingleQuery)
                .OrderBy(b => b.TenantId)
                .ToList();

            blogs.Select(b => b.TenantId).Should().Equal(9, 10);
            blogs[0].PrimaryPost!.Id.Should().Be(105);
            blogs[1].PrimaryPost.Should().BeNull("zero matching identities resolves to null");

            interceptor.Statements.Should().HaveCount(1);
            Canon(interceptor.Statements[0]).Should().Contain("left join meta_sql_post");
        });

    [Fact]
    public void MetadataLoadWith_SingleShape_OneToOne_TwoDistinctIdentities_ThrowBeforeAssignment()
        => WithDb((ctx, _) =>
        {
            InsertRaw(ctx, "insert into meta_sql_post (tenant_id, id, blog_id, sort_key, title, author_id) values (7,107,2,7,'extra',301),(7,108,2,8,'extra2',301);");

            FluentActions.Invoking(() => ctx.From<MetaSqlBlog>()
                    .Where(b => b.TenantId == 7 && b.Id == 2)
                    .LoadWith(b => b.PrimaryPost, EagerLoadMode.SingleQuery)
                    .ToList())
                .Should().Throw<InvalidOperationException>()
                .WithMessage("*more than one distinct*");
        });

    // ---------------------------------------------------------------------------------------------
    // Many-to-many: one command, occurrence-aware stitching, no cartesian multiplication.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_SingleShape_ManyToMany_IssuesOneCommandAndPreservesEveryLinkOccurrence()
        => WithDb((ctx, interceptor) =>
        {
            var parents = ctx.From<MetaSqlTagParent>()
                .Where(p => p.Id == 1 || p.Id == 2)
                .LoadWith(p => p.Tags, EagerLoadMode.SingleQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2);
            parents[0].Tags.Should().HaveCount(3);
            parents[0].Tags.Select(t => t.Id).Should().Equal(501, 501, 502);
            parents[1].Tags.Should().BeEmpty();

            interceptor.Statements.Should().HaveCount(1);
            var sql = Canon(interceptor.Statements[0]);
            sql.Should().Contain("meta_sql_tag_link").And.Contain("meta_sql_tag");
            Count(sql, " join ").Should().Be(2, "one link edge and one child edge");
        });

    // ---------------------------------------------------------------------------------------------
    // Nested shapes: two levels in one command, child Where/OrderBy honored.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_ChildShape_NestedCollectionToReference_StitchesBothLevelsFromOneCommand()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(
                    b => b.Posts,
                    child => child.From<MetaSqlPost>().LoadWith(p => p.Author!),
                    EagerLoadMode.SingleQuery)
                .ToList();

            blogs.Should().ContainSingle();
            blogs[0].Posts.Should().HaveCount(2);
            blogs[0].Posts.Should().OnlyContain(p => p.Author != null && p.Author.Id == 301);

            interceptor.Statements.Should().HaveCount(1);
            var sql = Canon(interceptor.Statements[0]);
            Count(sql, " join ").Should().Be(2, "the collection edge and the reference edge share one command");
        });

    // --- T249-D4b: a level-2 reference that repeats the query-root type must keep its own alias in the
    // --- shared select list; an unqualified slot makes the one denormalized command ambiguous.

    [Fact]
    public void MetadataLoadWith_ChildShape_NestedRepeatedRootReference_QualifiesTheRepeatedRootSlot()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, child => child.From<MetaSqlPost>().LoadWith(p => p.Blog!), EagerLoadMode.SingleQuery)
                .ToList();

            blogs.Should().ContainSingle();
            blogs[0].Posts.Should().HaveCount(2);
            blogs[0].Posts.Should().OnlyContain(p => p.Blog != null && p.Blog.TenantId == 7 && p.Blog.Id == 1,
                "the level-2 reference materializes the root blog through its own join");

            interceptor.Statements.Should().HaveCount(1);
            var sql = Canon(interceptor.Statements[0]);
            var select = sql[..sql.IndexOf(" from ", StringComparison.Ordinal)];
            SqlSnapshot.Write("repeated-root-metadata", select);
            foreach (var column in select.Split(','))
                column.Trim().Should().Contain(".", "the repeated root slot must emit <alias>.<column>, got '{0}'", column.Trim());
        });

    [Fact]
    public void MetadataLoadWith_ChildShape_NestedDistinctType_RecordsTheNonRepeatedRootSql()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, child => child.From<MetaSqlPost>().LoadWith(p => p.Author!), EagerLoadMode.SingleQuery)
                .ToList();

            blogs.Should().ContainSingle();
            blogs[0].Posts.Should().HaveCount(2);
            blogs[0].Posts.Should().OnlyContain(p => p.Author != null && p.Author.Id == 301);

            var sql = interceptor.Statements.Should().ContainSingle().Subject;
            var select = Canon(sql);
            select = select[..select.IndexOf(" from ", StringComparison.Ordinal)];
            select.Should().Contain("t3.", "the distinct level-2 type keeps its own qualifier");
            foreach (var column in select.Split(','))
                column.Trim().Should().Contain(".", "every column of a multi-join select list is table-qualified, got '{0}'", column.Trim());

            SqlSnapshot.Write("metadata", sql);
        });

    [Fact]
    public void MetadataLoadWith_ChildShape_NestedCollectionToCollection_StitchesGrandChildrenPerChild()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 3)
                .LoadWith(
                    b => b.Posts,
                    child => child.From<MetaSqlPost>().LoadWith(p => p.Comments),
                    EagerLoadMode.SingleQuery)
                .ToList();

            blogs.Should().ContainSingle();
            var post = blogs[0].Posts.Should().ContainSingle().Subject;
            post.Id.Should().Be(103);
            post.Comments.Select(c => c.Id).Should().Equal(203);

            interceptor.Statements.Should().HaveCount(1);
        });

    [Fact]
    public void MetadataLoadWith_ChildShape_OrderedChild_KeepsTheChildQueryOrderAndParents()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id >= 1 && b.Id <= 2)
                .LoadWith(
                    b => b.Posts,
                    child => child.From<MetaSqlPost>().OrderByDescending(p => p.SortKey),
                    EagerLoadMode.SingleQuery)
                .OrderBy(b => b.Id)
                .ToList();

            blogs.Select(b => b.Id).Should().Equal(1, 2);
            blogs[0].Posts.Select(p => p.SortKey).Should().Equal([2, 1], "the child OrderBy is carried into the pair command");
            blogs[1].Posts.Should().BeEmpty();

            interceptor.Statements.Should().HaveCount(1);
        });

    [Fact]
    public void MetadataLoadWith_ChildShape_ChildWhere_FiltersChildrenButKeepsParents()
        => WithDb((ctx, interceptor) =>
        {
            var blogs = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id >= 1 && b.Id <= 2)
                .LoadWith(
                    b => b.Posts,
                    child => child.From<MetaSqlPost>().Where(p => p.SortKey == 1),
                    EagerLoadMode.SingleQuery)
                .OrderBy(b => b.Id)
                .ToList();

            blogs.Select(b => b.Id).Should().Equal(1, 2);
            blogs[0].Posts.Select(p => p.Id).Should().Equal(102);
            blogs[1].Posts.Should().BeEmpty();
            interceptor.Statements.Should().HaveCount(1);
        });

    // ---------------------------------------------------------------------------------------------
    // Fail-closed shapes: a composite per-parent page has no single partition expression.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_ChildShape_CompositePerParentPage_FailsClosedWithANamedRefusal()
        => WithDb((ctx, interceptor) =>
        {
            FluentActions.Invoking(() => ctx.From<MetaSqlBlog>()
                    .Where(b => b.TenantId == 7 && b.Id == 1)
                    .LoadWith(
                        b => b.Posts,
                        child => child.From<MetaSqlPost>().OrderBy(p => p.SortKey).Limit(1),
                        EagerLoadMode.SingleQuery)
                    .ToList())
                .Should().Throw<NotSupportedException>()
                .Which.Message.Should().Contain("composite").And.Contain("per-parent partition");

            interceptor.Statements.Should().BeEmpty("the refusal happens before any SQL is executed");
        });

    [Fact]
    public void MetadataLoadWith_ChildShape_ReferenceChildPage_FailsClosed()
        => WithDb((ctx, interceptor) =>
        {
            FluentActions.Invoking(() => ctx.From<MetaSqlPost>()
                    .Where(p => p.Id == 101)
                    .LoadWith(
                        p => p.Blog!,
                        child => child.From<MetaSqlBlog>().OrderBy(b => b.Id).Limit(1),
                        EagerLoadMode.SingleQuery)
                    .ToList())
                .Should().Throw<NotSupportedException>()
                .WithMessage("*Limit/Offset/Page*");

            interceptor.Statements.Should().BeEmpty();
        });

    // ---------------------------------------------------------------------------------------------
    // Mode resolution: explicit wins, Default inherits, Auto stays split.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Mode_ExplicitSingleQuery_IssuesOneCommand()
        => WithDb((ctx, interceptor) =>
        {
            var builder = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, EagerLoadMode.SingleQuery);

            builder.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SingleQuery);
            builder.ToList().Should().ContainSingle();
            interceptor.Statements.Should().HaveCount(1);
        });

    [Fact]
    public void MetadataLoadWith_Mode_ExplicitSplitQuery_IssuesTwoCommands()
        => WithDb((ctx, interceptor) =>
        {
            ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, EagerLoadMode.SplitQuery)
                .ToList()
                .Should().ContainSingle();

            interceptor.Statements.Should().HaveCount(2, "the explicit split choice loads the child separately");
            Canon(interceptor.Statements[1]).Should().Contain(" in (");
        });

    [Fact]
    public void MetadataLoadWith_Mode_Auto_RemainsSplit()
        => WithDb((ctx, interceptor) =>
        {
            var builder = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts, EagerLoadMode.Auto);

            builder.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SplitQuery);
            builder.ToList().Should().ContainSingle();
            interceptor.Statements.Should().HaveCount(2);
            Canon(interceptor.Statements[1]).Should().Contain(" in (");
        });

    [Fact]
    public void MetadataLoadWith_Mode_DefaultInheritsTheContextDefault()
        => WithDb((ctx, _) =>
        {
            var builder = ctx.From<MetaSqlBlog>()
                .Where(b => b.TenantId == 7 && b.Id == 1)
                .LoadWith(b => b.Posts);

            builder.ResolvedEagerLoadMode.Should().Be(EagerLoadMode.SplitQuery);
        });

    // ---------------------------------------------------------------------------------------------
    // D6b mutation closure: the split M:N occurrence count, the split per-parent page, the split
    // composite-page refusal and the two asynchronous loaders each get a case of their own.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_Split_ManyToMany_PreservesEveryLinkOccurrence()
        => WithDb((ctx, interceptor) =>
        {
            var parents = ctx.From<MetaSqlTagParent>()
                .Where(p => p.Id == 1 || p.Id == 2)
                .LoadWith(p => p.Tags, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Select(p => p.Id).Should().Equal(1, 2);
            parents[0].Tags.Select(t => t.Id).Should().Equal([501, 501, 502],
                "the split loader keeps one element per physical link row, exactly like the single-query stitch");
            parents[1].Tags.Should().BeEmpty("parent 2 has no link row");

            interceptor.Statements.Should().HaveCount(3, "one parent command, one junction read and one child read");
        });

    [Fact]
    public void MetadataLoadWith_Split_PerParentPage_AppliesPerParent()
        => WithDb((ctx, _) =>
        {
            var parents = ctx.From<MetaSqlSimpleParent>()
                .Where(p => p.Id == 1 || p.Id == 2)
                .LoadWith(
                    p => p.Children,
                    child => child.From<MetaSqlSimpleChild>().OrderBy(c => c.SortKey).Limit(1),
                    EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToList();

            parents.Should().HaveCount(2);
            parents[0].Children.Select(c => c.Id).Should().Equal([12], "parent 1 keeps its lowest sort_key child");
            parents[1].Children.Select(c => c.Id).Should().Equal([13],
                "the page is applied per parent, so the second parent is not starved by a global top-1");
        });

    [Fact]
    public void MetadataLoadWith_Split_CompositeChildPage_FailsClosedWithANamedRefusal()
        => WithDb((ctx, interceptor) =>
        {
            FluentActions.Invoking(() => ctx.From<MetaSqlBlog>()
                    .Where(b => b.TenantId == 7 && b.Id == 1)
                    .LoadWith(
                        b => b.Posts,
                        child => child.From<MetaSqlPost>().OrderBy(p => p.SortKey).Limit(1),
                        EagerLoadMode.SplitQuery)
                    .ToList())
                .Should().Throw<NotSupportedException>()
                .Which.Message.Should().Contain("composite-key relationship").And.Contain("per-parent partition");

            interceptor.Statements.Should().ContainSingle("only the parent command ran; the child plan is refused");
        });

    [Fact]
    public async Task MetadataLoadWith_Split_ManyToMany_AsyncTerminal_LoadsTheGraph()
        => await WithDbAsync(async (ctx, _) =>
        {
            var parents = await ctx.From<MetaSqlTagParent>()
                .Where(p => p.Id == 1 || p.Id == 2)
                .LoadWith(p => p.Tags, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            parents.Select(p => p.Id).Should().Equal(1, 2);
            parents[0].Tags.Select(t => t.Id).Should().Equal([501, 501, 502],
                "the asynchronous many-to-many loader keeps every link occurrence");
            parents[1].Tags.Should().BeEmpty();
        });

    [Fact]
    public async Task MetadataLoadWith_Split_Reference_AsyncTerminal_ResolvesPerFullIdentity()
        => await WithDbAsync(async (ctx, _) =>
        {
            var posts = await ctx.From<MetaSqlPost>()
                .Where(p => p.Id == 101 || p.Id == 104)
                .LoadWith(p => p.Blog!, EagerLoadMode.SplitQuery)
                .OrderBy(p => p.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            posts.Select(p => p.TenantId).Should().Equal(7, 8);
            posts[0].Blog.Should().NotBeNull();
            posts[0].Blog!.TenantId.Should().Be(7, "the same id under another tenant is a different full identity");
            posts[0].Blog!.Id.Should().Be(1);
            posts[1].Blog.Should().NotBeNull();
            posts[1].Blog!.TenantId.Should().Be(8);
        });

    // ---------------------------------------------------------------------------------------------
    // D5 rejection timing: malformed metadata emits zero SQL and leaves the plan store untouched.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MetadataLoadWith_RejectionCounters_MalformedMetadata_ZeroCommandsAndNoPlanEntry()
        => WithDb((ctx, interceptor) =>
        {
            PublishInvalidParentMetadata();

            var storedBefore = QueryPlanStore.Values.Count();

            Action rejected = () => ctx.From<MetaSqlInvalidParent>().LoadWith(p => p.Children);

            rejected.Should().Throw<NotSupportedException>().WithMessage("*repeats the mapped foreign key property*");

            interceptor.Statements.Should().BeEmpty("the rejected shape emits zero SQL statements");
            QueryPlanStore.Values.Count().Should().Be(storedBefore, "the plan store receives zero inserts for the rejected shape");

            // The shared scalar cache stays usable after the rejection: a real scalar query executes and caches.
            ctx.From<MetaSqlAuthor>().Count().Should().Be(2);
            interceptor.Statements.Should().HaveCount(1, "exactly the neighbouring scalar statement executed");
            QueryPlanStore.Values.Count().Should().Be(storedBefore + 1, "the neighbouring query cached exactly one plan");
        });

    [Fact]
    public void MetadataLoadWith_RejectionCounters_RepeatedMalformedDeclaration_StaysCounterFree()
        => WithDb((ctx, interceptor) =>
        {
            PublishInvalidParentMetadata();
            var storedBefore = QueryPlanStore.Values.Count();

            Action first = () => ctx.From<MetaSqlInvalidParent>().LoadWith(p => p.Children);
            Action second = () => ctx.From<MetaSqlInvalidParent>().LoadWith(p => p.Children);

            first.Should().Throw<NotSupportedException>();
            second.Should().Throw<NotSupportedException>("a rejected declaration is re-evaluated, never served from a cache");

            interceptor.Statements.Should().BeEmpty();
            QueryPlanStore.Values.Count().Should().Be(storedBefore);
        });

    /// <summary>
    /// Publishes a relationship whose foreign-key component repeats, exactly as a faulty producer could.
    /// The consumer must reject it at declaration, before any SQL and before any plan-store insert.
    /// </summary>
    private static void PublishInvalidParentMetadata()
    {
        var properties = new EntityMetadataBuilder<MetaSqlInvalidParent>().Build().Properties
            .Where(property => property.PropertyInfo.Name != nameof(MetaSqlInvalidParent.Children))
            .ToList();

        var relationship = new RelationshipMetadata(
            RelationshipKind.OneToMany,
            typeof(MetaSqlInvalidParent),
            typeof(MetaSqlInvalidChild),
            typeof(MetaSqlInvalidParent).GetProperty(nameof(MetaSqlInvalidParent.Children)),
            isCollection: true,
            foreignKeys:
            [
                typeof(MetaSqlInvalidChild).GetProperty(nameof(MetaSqlInvalidChild.ParentId))!,
                typeof(MetaSqlInvalidChild).GetProperty(nameof(MetaSqlInvalidChild.ParentId))!,
            ],
            principalKeys:
            [
                typeof(MetaSqlInvalidParent).GetProperty(nameof(MetaSqlInvalidParent.Id))!,
                typeof(MetaSqlInvalidParent).GetProperty(nameof(MetaSqlInvalidParent.Id))!,
            ],
            dependentType: typeof(MetaSqlInvalidChild),
            principalType: typeof(MetaSqlInvalidParent));

        DataContextCache.Metadata[typeof(MetaSqlInvalidParent)] =
            new EntityMetadata("meta_sql_invalid_parent", properties, relationships: [relationship]);
        DataContextCache.Metadata[typeof(MetaSqlInvalidChild)] =
            new EntityMetadataBuilder<MetaSqlInvalidChild>().Build();
    }


    private static void InsertRaw(IDataContext ctx, string sql)
    {
        using var connection = new SqliteConnection(((DataContext)ctx).ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

[SqlTable("meta_sql_blog")]
public sealed class MetaSqlBlog
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetaSqlPost> Posts { get; set; } = new List<MetaSqlPost>();

    public MetaSqlPost? PrimaryPost { get; set; }
}

[SqlTable("meta_sql_post")]
public sealed class MetaSqlPost
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("blog_id")]
    public int BlogId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    [Column("title")]
    public string? Title { get; set; }

    [Column("author_id")]
    public int AuthorId { get; set; }

    public MetaSqlBlog? Blog { get; set; }

    public MetaSqlAuthor? Author { get; set; }

    public ICollection<MetaSqlComment> Comments { get; set; } = new List<MetaSqlComment>();
}

[SqlTable("meta_sql_author")]
public sealed class MetaSqlAuthor
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("meta_sql_comment")]
public sealed class MetaSqlComment
{
    [Key]
    [Column("tenant_id")]
    public int TenantId { get; set; }

    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("post_id")]
    public int PostId { get; set; }

    [Column("body")]
    public string? Body { get; set; }
}

[SqlTable("meta_sql_tag_parent")]
public sealed class MetaSqlTagParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetaSqlTag> Tags { get; set; } = new List<MetaSqlTag>();
}

[SqlTable("meta_sql_tag")]
public sealed class MetaSqlTag
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("meta_sql_tag_link")]
public sealed class MetaSqlTagLink
{
    [Key]
    [Column("parent_id")]
    public int ParentId { get; set; }

    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }
}

[SqlTable("meta_sql_simple_parent")]
public sealed class MetaSqlSimpleParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetaSqlSimpleChild> Children { get; set; } = new List<MetaSqlSimpleChild>();
}

[SqlTable("meta_sql_simple_child")]
public sealed class MetaSqlSimpleChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("sort_key")]
    public int SortKey { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

/// <summary>D5 rejection fixture: a parent whose declared relationship publishes a malformed key list.</summary>
[SqlTable("meta_sql_invalid_parent")]
public sealed class MetaSqlInvalidParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    public ICollection<MetaSqlInvalidChild> Children { get; set; } = new List<MetaSqlInvalidChild>();
}

/// <summary>D5 rejection fixture: the related type of <see cref="MetaSqlInvalidParent"/>.</summary>
[SqlTable("meta_sql_invalid_child")]
public sealed class MetaSqlInvalidChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
