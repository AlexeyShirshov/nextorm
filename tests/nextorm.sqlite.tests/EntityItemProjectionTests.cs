using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Executes real SQLite queries that project whole entities and materializes the entities back from the
/// flattened scalar columns. Unlike a SQL-text test, these run the query so a mapper that rejects or
/// mis-groups entity-typed columns fails here.
/// </summary>
public class EntityItemProjectionTests
{
    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-entity-items-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "create table entity_parent (id integer primary key, name text);" +
                              "create table entity_child (id integer primary key, parent_id integer, name text, state text);" +
                              "create table entity_value (id integer primary key, label text);" +
                              "insert into entity_parent (id, name) values (1, 'p1'), (2, 'p2');" +
                              "insert into entity_child (id, parent_id, name, state) values (10, 1, 'c1', 'Active'), (11, 1, 'c2', 'Closed');" +
                              "insert into entity_value (id, label) values (1, 'v1'), (2, 'v2');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void OuterJoin_EntityItems_ShouldMaterializeBothEntitiesAndNullChild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new { Left = p.Item1, Right = p.Item2 })
                .ToList();

            rows.Should().HaveCount(3);

            var withChildren = rows.Where(r => r.Right is not null).OrderBy(r => r.Right!.Id).ToList();
            withChildren.Should().HaveCount(2);
            withChildren.Should().OnlyContain(r => r.Left.Id == 1 && r.Left.Name == "p1");

            // Field order and the value converter on the nested entity survive the expansion.
            withChildren.Select(r => r.Right!.Id).Should().Equal(10, 11);
            withChildren.Select(r => r.Right!.Name).Should().Equal("c1", "c2");
            withChildren.Select(r => r.Right!.State).Should().Equal(EntityItemState.Active, EntityItemState.Closed);

            var childless = rows.Single(r => r.Left.Id == 2);
            childless.Left.Name.Should().Be("p2");
            childless.Right.Should().BeNull("a LEFT JOIN row with no child materializes the entity item as null");

            // A second execution reuses the cached mapper for the same SQL/shape and produces the same rows.
            var again = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new { Left = p.Item1, Right = p.Item2 })
                .ToList();
            again.Should().HaveCount(3);
            again.Count(r => r.Right is null).Should().Be(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void InnerJoin_MixedProjection_ShouldMaterializeScalarAndEntity()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var rows = ctx.From<EntityItemParent>()
                .Join(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new { ParentId = p.Item1.Id, Child = p.Item2 })
                .ToList();

            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(r => r.ParentId == 1);
            rows.Select(r => r.Child!.Id).OrderBy(x => x).Should().Equal(10, 11);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void BareProjection_PreparedWithoutCache_ShouldStillMaterialize()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToCommand();

            // Preparing with storeInCache: false skips the plan hash and the per-column ordinal must
            // still be reassigned after expansion.
            var prepared = ctx.GetPreparedQueryCommand(cmd, createEnumerator: false, storeInCache: false, CancellationToken.None);
            var rows = prepared.ToList(ctx);

            rows.Should().HaveCount(3);
            rows.Single(r => r.Item1.Id == 2).Item2.Should().BeNull();
            rows.Single(r => r.Item2 is { Id: 10 }).Item2!.Name.Should().Be("c1");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void BareProjection_ShouldMaterializeProjectionOfEntitiesAndNullChild()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var sql = SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToCommand());

            // Item columns are expanded in a deterministic order: the parent item first, then the child.
            sql.Should().Be("select t1.id, t1.name, t2.id, t2.parent_id as 'ParentId', t2.name, t2.state\n" +
                            " from entity_parent as 't1'\n left join entity_child as 't2' on t1.id = t2.parent_id");

            var rows = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToList();

            rows.Should().HaveCount(3);
            rows.Should().OnlyContain(r => r.Item1.Name == "p1" || r.Item1.Name == "p2");

            var childless = rows.Single(r => r.Item1.Id == 2);
            childless.Item2.Should().BeNull();

            var child = rows.Single(r => r.Item2 is { Id: 10 });
            child.Item1.Id.Should().Be(1);
            child.Item2!.State.Should().Be(EntityItemState.Active);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectEntityItem_SecondSlot_ShouldSelectOnlyRequestedEntityColumns()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var sql = SqlOf(ctx, ctx.From<EntityItemParent>()
                .Join(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item2));

            sql.Should().StartWith("select t2.id, t2.parent_id as 'ParentId', t2.name, t2.state\n from");
            sql.Should().NotContain("*", "explicit mapped columns are the agreed form of the projection");

            var children = ctx.From<EntityItemParent>()
                .Join(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item2)
                .ToList();

            children.Should().HaveCount(2);
            children.Should().OnlyContain(c => c.ParentId == 1);
            children.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
            children.Select(c => c.State).OrderBy(x => x).Should().Equal(EntityItemState.Active, EntityItemState.Closed);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectEntityItem_OuterJoin_ShouldReturnNullForMissingSide()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var children = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item2)
                .ToList();

            children.Should().HaveCount(3);
            children.Count(c => c is null).Should().Be(1, "a LEFT JOIN row with no child materializes the whole entity as null");
            children.Where(c => c is not null).Select(c => c!.Id).OrderBy(x => x).Should().Equal(10, 11);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectEntityItem_FirstSlot_ShouldSelectOnlyRequestedEntityColumns()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var sql = SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item1));

            sql.Should().StartWith("select t1.id, t1.name\n from");
            sql.Should().NotContain("*");

            var parents = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item1)
                .ToList();

            parents.Should().HaveCount(3);
            parents.Select(p => p.Id).OrderBy(x => x).Should().Equal(1, 1, 2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ScalarJoinItem_ShouldKeepSingleScalarColumnProjection()
    {
        // A joined item that is a scalar (here an int subquery source) is NOT a whole entity. The
        // direct-item recognizer must not claim it, or the select list would stay empty and the SQL
        // would be broken; the previous single-column scalar projection must be preserved.
        var (ctx, path) = CreateDb();
        try
        {
            var scalarIds = ctx.From<EntityItemChild>().Where(c => c.Id == 10).Select(c => c.Id);

            var cmd = ctx.From<EntityItemParent>()
                .Join(ctx.From(scalarIds), (p, s) => p.Id == s)
                .Select(p => p.Item2);

            var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None);
            var sql = prepared.DbCommand.CommandText;

            // Before the fix the direct-item recognizer claimed the scalar item and left the select
            // list empty ("select from ..."); the prior single-column scalar projection must survive.
            sql.Should().StartWith("select ", "the scalar join item must produce a non-empty projection");
            sql.Should().NotMatchRegex(@"select\s+from", "a scalar item must still project exactly one column");
            cmd.SelectList.Should().NotBeNullOrEmpty("the scalar join item must keep its single column");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectEntityProjection_CancelledPreparation_ShouldThrowOperationCanceled()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item2);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Action act = () => cmd.PrepareCommand(dontCalculateHash: true, cts.Token);

            act.Should().Throw<OperationCanceledException>(
                "a cancelled direct-entity expansion must surface cancellation, not an empty select list");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void UnmappedReferenceItem_ShouldThrowClearNotSupported()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var unmapped = ctx.From<EntityItemChild>().Select(c => new UnmappedItem { X = c.Id });

            Action act = () => SqlOf(ctx, ctx.From<EntityItemParent>()
                .Join(ctx.From(unmapped), (p, s) => p.Id == s.X)
                .Select(p => p.Item2));

            act.Should().Throw<NotSupportedException>()
                .WithMessage("*UnmappedItem*not a mapped entity*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void UnmappedReferenceItem_Throw_ShouldNotDisableThePlanCache()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var unmapped = ctx.From<EntityItemChild>().Select(c => new UnmappedItem { X = c.Id });
            var throwing = ctx.From<EntityItemParent>()
                .Join(ctx.From(unmapped), (p, s) => p.Id == s.X)
                .Select(p => p.Item2);

            Action act = () => throwing.PrepareCommand(false, CancellationToken.None);
            act.Should().Throw<NotSupportedException>();

            throwing.Cache.Should().BeTrue("a rejected projection must not stick _dontCache on the command");

            // The same context still serves an unrelated direct projection, and its plan is reused.
            var rows = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item2)
                .ToList();
            rows.Should().HaveCount(3);
            rows.Count(c => c is null).Should().Be(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void NestedEntityMember_ShouldBeRejectedNotSilentlyEmpty()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // p.Item2.Name is a scalar member of the item, not the item itself: it must resolve to a
            // single column, never to an empty select list.
            var sql = SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => p.Item2.Name));

            sql.Should().StartWith("select ");
            sql.Should().NotMatchRegex(@"select\s+from");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void CastToUnrelatedType_ShouldBeRejectedNotSilentlyEmpty()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => (object)p.Item2);

            Action act = () => SqlOf(ctx, cmd);
            act.Should().Throw<NotSupportedException>()
                .WithMessage("*EntityItemChild*unrelated result type*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ValueEntityItem_WholeProjection_ShouldFailClosedAsUnsupported()
    {
        // A value-type entity whole projection is not supported by the existing shared materializer:
        // RowMaterializerBuilder.BuildCore cannot build a struct (no reflected constructor), exactly as
        // the nested entity-item path. This is a pre-existing limitation, pinned here as a guard so the
        // direct path never silently returns a default/mis-materialized struct.
        var (ctx, path) = CreateDb();
        try
        {
            ctx.From<EntityItemValue>(b => b.Table("entity_value"));

            Action act = () => ctx.From<EntityItemParent>()
                .Join(ctx.From<EntityItemValue>(), (p, v) => p.Id == v.Id)
                .Select(p => p.Item2)
                .ToList();

            act.Should().Throw<QueryPreparationException>()
                .WithMessage("*Cannot get ctor*EntityItemValue*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void SelfJoin_DirectEntityItem_ShouldMaterializeBothSlots()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var first = ctx.From<EntityItemChild>()
                .Join(ctx.From<EntityItemChild>(), (a, b) => a.Id == b.Id)
                .Select(p => p.Item1)
                .ToList();
            var second = ctx.From<EntityItemChild>()
                .Join(ctx.From<EntityItemChild>(), (a, b) => a.Id == b.Id)
                .Select(p => p.Item2)
                .ToList();

            first.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
            second.Select(c => c.Id).OrderBy(x => x).Should().Equal(10, 11);
            first.Select(c => c.Name).OrderBy(x => x).Should().Equal("c1", "c2");
            second.Select(c => c.Name).OrderBy(x => x).Should().Equal("c1", "c2");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    /// <summary>
    /// The three projection shapes whose cancellation handling T207 unifies. Each builds a fresh command
    /// over the same LEFT JOIN pair; the case id is carried into the test display name so a per-shape
    /// failure is identifiable in the run output.
    /// </summary>
    private static QueryCommand T207Shape(IDataContext ctx, string shape)
    {
        if (shape == "new-expression")
            return ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new T207ProbePair(p.Item2, p.Item1.Id));
        if (shape == "member-init")
            return ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new T207ProbeDto { Child = p.Item2, Name = p.Item1.Name });
        if (shape == "bare-join")
            return ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToCommand();
        throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
    }

    /// <summary>Builds a fresh command of the named shape and returns its rendered SQL.</summary>
    private static string T207Sql(IDataContext ctx, string shape)
    {
        if (shape == "new-expression")
            return SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new T207ProbePair(p.Item2, p.Item1.Id)));
        if (shape == "member-init")
            return SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .Select(p => new T207ProbeDto { Child = p.Item2, Name = p.Item1.Name }));
        if (shape == "bare-join")
            return SqlOf(ctx, ctx.From<EntityItemParent>()
                .LeftJoin(ctx.From<EntityItemChild>(), (p, c) => p.Id == c.ParentId)
                .ToCommand());
        throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
    }

    /// <summary>
    /// T207 red regressions: a pre-cancelled preparation must surface <see cref="OperationCanceledException"/>
    /// for every projection shape, never a partial select list (new-expression/bare-join) or a null-filled
    /// column array (member-init, whose cancellation early-return used to hand back <c>[null, null]</c>).
    /// </summary>
    [Theory]
    [InlineData("new-expression")]
    [InlineData("member-init")]
    [InlineData("bare-join")]
    public void T207_PreCancelledProjection_ThrowsOperationCanceled(string shape)
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cmd = T207Shape(ctx, shape);

            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Action act = () => cmd.PrepareCommand(dontCalculateHash: true, cts.Token);

            act.Should().Throw<OperationCanceledException>(
                $"the pre-cancelled '{shape}' projection must surface cancellation, not a partial or masking result");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    /// <summary>
    /// Pre-fix expected values captured on the unmodified build (<c>c7ff89ca</c>), so the happy path is
    /// pinned rather than retro-fitted: <c>new-expression</c> and <c>bare-join</c> keep their exact SQL and
    /// column count/order; <c>member-init</c>'s entity-typed binding is not expanded by the MemberInit
    /// branch, so the mapper refuses it with a pre-existing <see cref="NotSupportedException"/> (the
    /// cancellation fix must not turn either into <see cref="OperationCanceledException"/>).
    /// </summary>
    public static IEnumerable<object[]> T207NonCancelledCases()
    {
        yield return
        [
            "new-expression",
            "select t2.id, t2.parent_id as 'ParentId', t2.name, t2.state, t1.id as 'parentId'\n" +
            " from entity_parent as 't1'\n left join entity_child as 't2' on t1.id = t2.parent_id",
            5,
            "Id,ParentId,Name,State,parentId",
        ];
        yield return ["member-init", null!, 2, "Child,Name"];
        yield return
        [
            "bare-join",
            "select t1.id, t1.name, t2.id, t2.parent_id as 'ParentId', t2.name, t2.state\n" +
            " from entity_parent as 't1'\n left join entity_child as 't2' on t1.id = t2.parent_id",
            6,
            "Id,Name,Id,ParentId,Name,State",
        ];
    }

    [Theory]
    [MemberData(nameof(T207NonCancelledCases))]
    public void T207_NonCancelledProjection_PreservesSqlAndColumns(string shape, string? expectedSql, int expectedCount, string expectedNames)
    {
        var (ctx, path) = CreateDb();
        try
        {
            var forColumns = T207Shape(ctx, shape);
            Action prepare = () => forColumns.PrepareCommand(dontCalculateHash: true, CancellationToken.None);
            prepare.Should().NotThrow<OperationCanceledException>("a non-cancelled preparation must never surface cancellation");

            forColumns.SelectList!.Length.Should().Be(expectedCount);
            string.Join(",", forColumns.SelectList.Select(s => s.PropertyName)).Should().Be(expectedNames);

            if (expectedSql is null)
            {
                Action sql = () => T207Sql(ctx, shape);
                sql.Should().Throw<NotSupportedException>(
                    "the member-init entity-typed binding is a pre-existing mapper-level refusal, not a cancellation");
            }
            else
            {
                T207Sql(ctx, shape).Should().Be(expectedSql, "a non-cancelled preparation must keep its pre-fix SQL");
            }
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    /// <summary>
    /// A structurally unsupported entity expansion with a non-cancelled token must keep its typed
    /// refusal and never be reported as <see cref="OperationCanceledException"/>.
    /// </summary>
    [Theory]
    [InlineData("unmapped-item")]
    [InlineData("value-entity-item")]
    public void T207_UnsupportedEntityExpansion_PreservesStructuralRefusal(string shape)
    {
        var (ctx, path) = CreateDb();
        try
        {
            Exception thrown;
            if (shape == "unmapped-item")
            {
                var unmapped = ctx.From<EntityItemChild>().Select(c => new UnmappedItem { X = c.Id });
                Action act = () => SqlOf(ctx, ctx.From<EntityItemParent>()
                    .Join(ctx.From(unmapped), (p, s) => p.Id == s.X)
                    .Select(p => p.Item2));
                thrown = act.Should().Throw<Exception>().Which;
                thrown.Should().BeOfType<NotSupportedException>().Which.Message.Should().Contain("UnmappedItem").And.Contain("not a mapped entity");
            }
            else
            {
                ctx.From<EntityItemValue>(b => b.Table("entity_value"));
                Action act = () => ctx.From<EntityItemParent>()
                    .Join(ctx.From<EntityItemValue>(), (p, v) => p.Id == v.Id)
                    .Select(p => p.Item2)
                    .ToList();
                thrown = act.Should().Throw<Exception>().Which;
                thrown.Should().BeOfType<QueryPreparationException>().Which.Message.Should().Contain("Cannot get ctor").And.Contain("EntityItemValue");
            }

            thrown.Should().NotBeAssignableTo<OperationCanceledException>(
                "an unsupported shape must keep its typed refusal when no token is cancelled");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }
}

/// <summary>A reference type that is deliberately not a mapped entity.</summary>
public sealed class UnmappedItem
{
    public int X { get; set; }
}

public struct EntityItemValue
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("label")]
    public string? Label { get; set; }
}

public enum EntityItemState
{
    Unknown,
    Active,
    Closed,
}

[SqlTable("entity_parent")]
public sealed class EntityItemParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("entity_child")]
public sealed class EntityItemChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("state")]
    [ValueConverter(typeof(EnumToStringConverter<EntityItemState>))]
    public EntityItemState State { get; set; }
}

/// <summary>A new-expression projection DTO that takes the entity item plus a scalar (T207 shape 1).</summary>
public sealed class T207ProbePair
{
    public T207ProbePair(EntityItemChild child, int parentId)
    {
        Child = child;
        ParentId = parentId;
    }

    public EntityItemChild Child { get; }

    public int ParentId { get; }
}

/// <summary>A member-init projection DTO binding the entity item plus a scalar (T207 shape 2).</summary>
public sealed class T207ProbeDto
{
    public EntityItemChild? Child { get; set; }

    public string? Name { get; set; }
}
