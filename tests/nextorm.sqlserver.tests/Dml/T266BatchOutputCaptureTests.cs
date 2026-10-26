using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>The typed row shape read back from a batch table-variable capture.</summary>
public sealed class T266CapturedRow
{
    /// <summary>The captured identity value.</summary>
    public long Id { get; set; }

    /// <summary>The captured name value.</summary>
    public string? Name { get; set; }
}

/// <summary>
/// SQL generation of the SQL Server batch <c>OUTPUT ... INTO</c> capture (no database connection): the
/// capture renders <c>DECLARE @t TABLE (...)</c> plus the DML with its <c>OUTPUT ... INTO @t</c> clause and
/// the composed follow-up query reads the table variable in the same batch.
/// </summary>
public class T266BatchOutputCaptureTests
{
    [Fact]
    public void T266_Insert_Capture_RendersDeclareIntoAndComposedSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateInsertBuilder<IInsertEntity>()
                    .Value(x => x.Name, "a")
                    .Returning(x => new { x.Id, x.Name }),
                "@t",
                "id bigint, name nvarchar(100)");

        var sql = batch.Query(
                batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt, Name = t["name"].AsString }))
            .ToSql();

        sql.Should().Be(
            "declare @t table (id bigint, name nvarchar(100)); "
            + "insert into insert_entity (name) output inserted.id, inserted.name into @t (id, name) values (@p0); "
            + "select id, name\n from @t");
    }

    [Fact]
    public void T266_Update_Capture_RendersDeclareIntoAndComposedSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateUpdateBuilder<IMergeEntity>()
                    .Set(x => x.Name, "a")
                    .Where(x => x.Id == 1)
                    .Returning(x => new { x.Id, x.Name }),
                "@t",
                "id bigint, name nvarchar(100)");

        var sql = batch.Query(
                batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt, Name = t["name"].AsString }))
            .ToSql();

        sql.Should().Be(
            "declare @t table (id bigint, name nvarchar(100)); "
            + "update merge_entity set name = @p0 output inserted.id, inserted.name into @t (id, name) where id = 1; "
            + "select id, name\n from @t");
    }

    [Fact]
    public void T266_Delete_Capture_RendersDeclareIntoAndComposedSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateDeleteBuilder<IMergeEntity>()
                    .Where(x => x.Id == 1)
                    .Returning(x => new { x.Id, x.Name }),
                "@t",
                "id bigint, name nvarchar(100)");

        var sql = batch.Query(
                batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt, Name = t["name"].AsString }))
            .ToSql();

        sql.Should().Be(
            "declare @t table (id bigint, name nvarchar(100)); "
            + "delete from merge_entity output deleted.id, deleted.name into @t (id, name) where id = 1; "
            + "select id, name\n from @t");
    }

    [Fact]
    public void T266_Merge_Capture_RendersDeclareActionIntoAndComposedSelect()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateMergeBuilder<IMergeEntity>()
                    .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
                    .OnKeys()
                    .WhenMatchedUpdate()
                    .WhenNotMatchedInsert()
                    .Returning(x => new { x.Id, x.Name }),
                "@t",
                "action nvarchar(10), id bigint, name nvarchar(100)")
            // The INTO names are the caller's declaration names: a first column that is not called
            // 'action' must be honoured instead of a hard-coded literal.
            .OutputIntoTableVariable(
                ctx.CreateMergeBuilder<IMergeEntity>()
                    .Using(new MergeEntity { Id = 2, Name = "b", Age = 6 })
                    .OnKeys()
                    .WhenMatchedUpdate()
                    .WhenNotMatchedInsert()
                    .Returning(x => new { x.Id, x.Name }),
                "@u",
                "verb nvarchar(10), id bigint, name nvarchar(100)");

        var sql = batch.Query(
                batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt, Name = t["name"].AsString }))
            .ToSql();

        // One DECLARE + DML per capture; the second capture's VALUES continue the batch-wide parameter
        // sequence (@p3..@p5) instead of restarting at @p0. The INTO names are the caller's declaration
        // names, so '@u' receives (verb, id, name) while '@t' receives (action, id, name).
        sql.Should().Be(
            "declare @t table (action nvarchar(10), id bigint, name nvarchar(100)); "
            + "merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) "
            + "on target.id = source.id "
            + "when matched then update set target.name = source.name, target.age = source.age "
            + "when not matched then insert (id, name, age) values (source.id, source.name, source.age) "
            + "output $action, inserted.id, inserted.name into @t (action, id, name);; "
            + "declare @u table (verb nvarchar(10), id bigint, name nvarchar(100)); "
            + "merge into merge_entity as target using (values (@p3, @p4, @p5)) as source (id, name, age) "
            + "on target.id = source.id "
            + "when matched then update set target.name = source.name, target.age = source.age "
            + "when not matched then insert (id, name, age) values (source.id, source.name, source.age) "
            + "output $action, inserted.id, inserted.name into @u (verb, id, name);; "
            + "select id, name\n from @t");
    }

    [Fact]
    public void T266_TypedAndTableAliasSources_PreserveProjection()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateInsertBuilder<IInsertEntity>()
                    .Value(x => x.Name, "a")
                    .Returning(x => new T266CapturedRow { Id = x.Id, Name = x.Name }),
                "@t",
                "id bigint, name nvarchar(100)");

        var typed = batch.FromOutput<T266CapturedRow>("@t").Select(r => new T266CapturedRow { Id = r.Id, Name = r.Name });
        var alias = batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt, Name = t["name"].AsString });

        var sql = batch.AddQuery(typed).AddQuery(alias).ToSql();

        // The typed source carries a column shape, so the ordinary shaped-source rule aliases it ([t1])
        // exactly as it does for any derived source; the table-alias source stays a bare @t. Neither
        // form ever brackets or quotes the table-variable name itself.
        sql.Should().Be(
            "declare @t table (id bigint, name nvarchar(100)); "
            + "insert into insert_entity (name) output inserted.id, inserted.name into @t (id, name) values (@p0); "
            + "select id, name\n from @t as [t1]; "
            + "select id, name\n from @t");
    }

    [Fact]
    public void T266_FilterOrderLimitJoinAggregate_RenderCorrectly()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateInsertBuilder<IInsertEntity>()
                    .Value(x => x.Name, "a")
                    .Returning(x => new T266CapturedRow { Id = x.Id, Name = x.Name }),
                "@t",
                "id bigint, name nvarchar(100)");

        var filtered = batch.FromOutput<T266CapturedRow>("@t")
            .Where(r => r.Id > 0)
            .OrderBy(r => r.Name)
            .Limit(10)
            .Select(r => new T266CapturedRow { Id = r.Id, Name = r.Name });

        var joined = batch.FromOutput<T266CapturedRow>("@t")
            .Join(ctx.From<ISimpleEntity>(), (r, s) => r.Id == s.Id)
            .Select(p => new { p.Item1.Id, Other = p.Item2.Id });

        var aggregated = batch.FromOutput<T266CapturedRow>("@t")
            .GroupBy(r => r.Name)
            .Select(g => new { g.Name, Cnt = SqlFunctions.Sql.count() });

        var sql = batch.AddQuery(filtered).AddQuery(joined).AddQuery(aggregated).ToSql();

        sql.Should().StartWith(
            "declare @t table (id bigint, name nvarchar(100)); "
            + "insert into insert_entity (name) output inserted.id, inserted.name into @t (id, name) values (@p0); ");
        // SQL Server pages with TOP, an aliased (shaped) source qualifies predicate/sort keys, an inner
        // join is the bare "join" keyword and a single-source projection stays unqualified.
        sql.Should().Contain("select top(10) id, name\n from @t as [t1]\n where (t1.id > 0)\n order by t1.name");
        sql.Should().Contain("select t1.id, t2.id as [Other]\n from @t as [t1]\n join simple_entity as [t2] on t1.id = cast(t2.id as bigint)");
        sql.Should().Contain("select name, count(*) as [Cnt]\n from @t as [t1]\n group by t1.name");
    }

    [Fact]
    public void T266_TwoCaptures_KeepDistinctParameterPrefixes()
    {
        using var ctx = SqlServerTestContext.Create();
        var name = "a";
        var min = 5;

        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateUpdateBuilder<IMergeEntity>()
                    .Set(x => x.Name, name)
                    .Where(x => x.Id > min)
                    .Returning(x => new T266CapturedRow { Id = x.Id }),
                "@t",
                "id bigint")
            .OutputIntoTableVariable(
                ctx.CreateDeleteBuilder<IMergeEntity>()
                    .Where(x => x.Id > min)
                    .Returning(x => new T266CapturedRow { Id = x.Id }),
                "@u",
                "id bigint");

        var sql = batch.Query(
                batch.FromOutput<T266CapturedRow>("@t").Where(r => r.Id > min).Select(r => new T266CapturedRow { Id = r.Id }))
            .ToSql();

        // Every step owns its own captured-member prefix (@b0_/@b1_/@b2_), so the same member name in
        // three steps never collapses into one placeholder. A value (not a predicate member) keeps the
        // batch-wide @pN sequence.
        sql.Should().Contain("@b0_min");
        sql.Should().Contain("@b1_min");
        sql.Should().Contain("@b2_min");
        sql.Should().Contain("set name = @p0");
        sql.Should().NotContain("@min ");
    }

    [Fact]
    public void T266_Capture_DoesNotEmitImplicitReadBack()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder()
            .OutputIntoTableVariable(
                ctx.CreateInsertBuilder<IInsertEntity>()
                    .Value(x => x.Name, "a")
                    .Returning(x => new { x.Id }),
                "@t",
                "id bigint");

        var sql = batch.Query(batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt })).ToSql();

        Occurrences(sql, "declare").Should().Be(1);
        Occurrences(sql, "select").Should().Be(1);
        sql.Should().NotContain("; select id\n from @t; select");
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
