using System.Reflection;
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
        var batch = ctx.CreateBatchBuilder();
        CaptureThreshold(ctx, batch, 5, "@t");
        CaptureThreshold(ctx, batch, 9, "@u");
        ComposeThreshold(batch, 7);

        var sql = batch.ToSql();

        // Every step owns its own captured-member prefix (@b0_/@b1_/@b2_), so the same logical member
        // name in three steps never collapses into one placeholder; each prefix is used by one step only.
        sql.Should().Contain("@b0_min");
        sql.Should().Contain("@b1_min");
        sql.Should().Contain("@b2_min");
        Occurrences(sql, "@b0_").Should().Be(1);
        Occurrences(sql, "@b1_").Should().Be(1);
        Occurrences(sql, "@b2_").Should().Be(1);
        sql.Should().NotContain("@min ");

        // The isolation is a value-level property, not only a naming one: the three bindings are
        // separate parameter entries carrying the three different values 5, 9 and 7.
        var plan = RenderPlan(ctx, batch);
        plan.Statements.Should().HaveCount(3);
        var values = new[]
        {
            BoundValue(plan.Statements[0], "b0_min"),
            BoundValue(plan.Statements[1], "b1_min"),
            BoundValue(plan.Statements[2], "b2_min"),
        };
        values.Should().Equal(5L, 9L, 7L);
        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void T266_TwoCapturesTwoReads_BindTheirOwnValues()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder();
        CaptureThreshold(ctx, batch, 5, "@t");
        CaptureThreshold(ctx, batch, 9, "@u");
        ComposeThreshold(batch, 7, "@t");
        ComposeThreshold(batch, 8, "@u");

        var sql = batch.ToSql();
        foreach (var prefix in new[] { "@b0_", "@b1_", "@b2_", "@b3_" })
        {
            sql.Should().Contain(prefix);
            Occurrences(sql, prefix).Should().Be(1);
        }

        // The live regression T266_ParametersAndTwoCaptures_DoNotCollide renders exactly this shape:
        // two captures and two reads, every closure named 'min', one distinct value per step. The
        // binding must already be correct before any server executes the batch.
        var plan = RenderPlan(ctx, batch);
        plan.Statements.Should().HaveCount(4);

        var values = new[]
        {
            BoundValue(plan.Statements[0], "b0_min"),
            BoundValue(plan.Statements[1], "b1_min"),
            BoundValue(plan.Statements[2], "b2_min"),
            BoundValue(plan.Statements[3], "b3_min"),
        };

        DumpSteps(plan);
        values.Should().Equal(5L, 9L, 7L, 8L);
    }

    [Fact]
    public void T266_TwoCapturesTwoReads_SameValue_KeepDistinctBindings()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder();
        CaptureThreshold(ctx, batch, 5, "@t");
        CaptureThreshold(ctx, batch, 9, "@u");
        ComposeThreshold(batch, 7, "@t");
        ComposeThreshold(batch, 7, "@u");

        // The reported case: both reads carry the SAME value, so a read-to-read leak would be invisible
        // in the row set of a two-read batch; only the four independent bindings expose it.
        var plan = RenderPlan(ctx, batch);
        plan.Statements.Should().HaveCount(4);

        DumpSteps(plan);
        BoundValue(plan.Statements[0], "b0_min").Should().Be(5L);
        BoundValue(plan.Statements[1], "b1_min").Should().Be(9L);
        BoundValue(plan.Statements[2], "b2_min").Should().Be(7L);
        BoundValue(plan.Statements[3], "b3_min").Should().Be(7L);
    }

    [Fact]
    public void T266_EmptyProjection_IsRefused()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = ctx.CreateBatchBuilder();

        // ReturningIdentity<TKey>() without a selector names no output column: the capture would have
        // nothing to write into the declaration, so it is refused at builder time.
        var act = () => batch.OutputIntoTableVariable(
            ctx.CreateInsertBuilder<IInsertEntity>()
                .Value(x => x.Name, "a")
                .ReturningIdentity<long>(),
            "@t",
            "id bigint");

        var exception = act.Should().Throw<NotSupportedException>().WithMessage("*batch output source*").Which;
        exception.Message.Should().Contain("output column");
        StepCount(batch).Should().Be(0);

        // The no-argument Returning() is a different form: it projects every mapped column and the same
        // capture is accepted (the refusal above is about a genuinely empty projection).
        var accepted = ctx.CreateBatchBuilder().OutputIntoTableVariable(
            ctx.CreateInsertBuilder<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(),
            "@t",
            "id bigint, name nvarchar(100), age int, total int");
        accepted.Query(accepted.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt })).ToSql().Should().Be(
            "declare @t table (id bigint, name nvarchar(100), age int, total int); "
            + "insert into insert_entity (name) output inserted.id, inserted.name, inserted.age, inserted.total into @t (id, name, age, total) values (@p0); "
            + "select id\n from @t");
    }

    [Fact]
    public void T266_InvalidOwnership_IsRefused()
    {
        using var ctx = SqlServerTestContext.Create();
        using var foreignCtx = SqlServerTestContext.Create();
        var cases = new List<(string Case, Action Act, Type Error, string Message, string? Parameter, BatchBuilder Batch, int Steps)>();

        void Add(string name, Action act, Type error, string message, string? parameter, BatchBuilder batch, int steps)
            => cases.Add((name, act, error, message, parameter, batch, steps));

        InsertReturningBuilder<IInsertEntity, T266CapturedRow> Insert() =>
            ctx.CreateInsertBuilder<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(x => new T266CapturedRow { Id = x.Id });

        var nullBatch = ctx.CreateBatchBuilder();
        Add("null mutation",
            () => nullBatch.OutputIntoTableVariable((InsertReturningBuilder<IInsertEntity, T266CapturedRow>)null!, "@t", "id bigint"),
            typeof(ArgumentNullException), "batch output source", "mutation", nullBatch, 0);

        foreach (var badName in new string?[] { null, "", "   " })
        {
            var caseBatch = ctx.CreateBatchBuilder();
            Add($"variable name '{badName ?? "<null>"}'",
                () => caseBatch.OutputIntoTableVariable(Insert(), badName!, "id bigint"),
                typeof(ArgumentException), "batch output source", "variableName", caseBatch, 0);
        }

        foreach (var badDefinitions in new string?[] { null, "", "   " })
        {
            var caseBatch = ctx.CreateBatchBuilder();
            Add($"column definitions '{badDefinitions ?? "<null>"}'",
                () => caseBatch.OutputIntoTableVariable(Insert(), "@t", badDefinitions!),
                typeof(ArgumentException), "batch output source", "columnDefinitions", caseBatch, 0);
        }

        foreach (var malformed in new[] { "@1bad", "[@t]", "\"@t\"", "@t; drop table x", "@t x" })
        {
            var caseBatch = ctx.CreateBatchBuilder();
            Add($"malformed name '{malformed}'",
                () => caseBatch.OutputIntoTableVariable(Insert(), malformed, "id bigint"),
                typeof(ArgumentException), "batch output source", "variableName", caseBatch, 0);
        }

        var duplicateBatch = CaptureInsert(ctx, "@t");
        Add("duplicate capture name",
            () => duplicateBatch.OutputIntoTableVariable(Insert(), "@t", "id bigint"),
            typeof(InvalidOperationException), "batch output source", null, duplicateBatch, 1);

        var foreignBatch = ctx.CreateBatchBuilder();
        Add("mutation from a different context",
            () => foreignBatch.OutputIntoTableVariable(
                foreignCtx.CreateInsertBuilder<IInsertEntity>()
                    .Value(x => x.Name, "a")
                    .Returning(x => new T266CapturedRow { Id = x.Id }),
                "@t",
                "id bigint"),
            typeof(ArgumentException), "batch output source", "variableName", foreignBatch, 0);

        var capturedBatch = CaptureInsert(ctx, "@t");
        Add("FromOutput of a name never captured",
            () => capturedBatch.FromOutput<T266CapturedRow>("@nope"),
            typeof(InvalidOperationException), "batch output source", null, capturedBatch, 1);
        Add("FromOutputTable of a name never captured",
            () => capturedBatch.FromOutputTable("@nope"),
            typeof(InvalidOperationException), "batch output source", null, capturedBatch, 1);

        var laterBatch = ctx.CreateBatchBuilder();
        Add("FromOutput before the capture",
            () => laterBatch.FromOutput<T266CapturedRow>("@t"),
            typeof(InvalidOperationException), "batch output source", null, laterBatch, 0);
        var laterTableBatch = ctx.CreateBatchBuilder();
        Add("FromOutputTable before the capture",
            () => laterTableBatch.FromOutputTable("@t"),
            typeof(InvalidOperationException), "batch output source", null, laterTableBatch, 0);

        var batchA = CaptureInsert(ctx, "@t");
        var sourceA = batchA.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt });
        var batchB = CaptureInsert(ctx, "@u");
        Add("batch A source used in batch B Query",
            () => batchB.Query(sourceA),
            typeof(InvalidOperationException), "batch output source", null, batchB, 1);
        var batchC = CaptureInsert(ctx, "@u");
        Add("batch A source used in batch C AddQuery",
            () => batchC.AddQuery(sourceA),
            typeof(InvalidOperationException), "batch output source", null, batchC, 1);

        foreach (var testCase in cases)
        {
            // Builder time: the call itself refuses; ToSql/Execute is never reached because no step (and
            // therefore no command object) is registered by the failed call.
            var exception = FluentActions.Invoking(testCase.Act)
                .Should().Throw<Exception>("'{0}' must fail closed at builder time", testCase.Case).Which;

            exception.GetType().Should().Be(testCase.Error, "'{0}' keeps a typed refusal", testCase.Case);
            exception.Message.Should().Contain(testCase.Message, "'{0}' names the reason", testCase.Case);
            exception.Message.Should().Contain("batch output source", "'{0}' carries the stable text", testCase.Case);

            if (testCase.Parameter is not null)
                exception.As<ArgumentException>().ParamName.Should().Be(testCase.Parameter, "'{0}' names the argument", testCase.Case);

            StepCount(testCase.Batch).Should().Be(
                testCase.Steps, "'{0}' must not register a capture step or build a command", testCase.Case);
        }

        // The use-before-capture refusal does not poison the batch: registering the capture later makes
        // the same source readable.
        laterBatch.OutputIntoTableVariable(Insert(), "@t", "id bigint");
        laterBatch.FromOutput<T266CapturedRow>("@t").Should().NotBeNull();
        StepCount(laterBatch).Should().Be(1);
    }

    [Fact]
    public async Task T266_StandaloneSource_IsRefused()
    {
        using var ctx = SqlServerTestContext.Create();
        var batch = CaptureInsert(ctx, "@t");

        var typed = batch.FromOutput<T266CapturedRow>("@t").Select(r => new T266CapturedRow { Id = r.Id });
        var alias = batch.FromOutputTable("@t").Select(t => new { Id = t["id"].AsInt });

        // A direct terminal would emit `select ... from @t` with no DECLARE: invalid SQL, refused before
        // any SQL is rendered or executed.
        FluentActions.Invoking(() => typed.ToList())
            .Should().Throw<NotSupportedException>().WithMessage("*batch output source*");
        await FluentActions.Invoking(() => typed.ToListAsync())
            .Should().ThrowAsync<NotSupportedException>().WithMessage("*batch output source*");
        FluentActions.Invoking(() => alias.ToList())
            .Should().Throw<NotSupportedException>().WithMessage("*batch output source*");
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(typed, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>().WithMessage("*batch output source*");
        FluentActions.Invoking(() => ctx.GetPreparedQueryCommand(alias, false, false, CancellationToken.None))
            .Should().Throw<NotSupportedException>().WithMessage("*batch output source*");

        // Composed into the owning batch, the same source renders the declaration, the capture and the read.
        batch.Query(alias).ToSql().Should().Be(
            "declare @t table (id bigint); "
            + "insert into insert_entity (name) output inserted.id into @t (id) values (@p0); "
            + "select id\n from @t");
    }

    [Fact]
    public void T266_DefaultReturningAndLegacyOutput_AreUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        // No-argument Returning() keeps projecting every mapped column.
        ctx.CreateInsertBuilder<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Value(x => x.Age, 5)
            .Returning()
            .ToSql()
            .Should().Be("insert into insert_entity (name, age) output inserted.id, inserted.name, inserted.age, inserted.total values (@p0, @p1)");

        // The pre-existing single-statement forms are byte-identical: the literals are copied from
        // InsertSqlGenerationTests.cs (OutputInto :404, OutputIntoThenOutput :417,
        // OutputIntoTableVariable :443), which is not modified.
        ctx.CreateInsertBuilder<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id, x.Name })
            .OutputInto("audit_log")
            .ToSql()
            .Should().Be("insert into insert_entity (name) output inserted.id, inserted.name into audit_log (id, name) values (@p0)");

        ctx.CreateInsertBuilder<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id, x.Name })
            .OutputIntoThenOutput("audit_log")
            .ToSql()
            .Should().Be("insert into insert_entity (name) output inserted.id, inserted.name into audit_log (id, name) output inserted.id, inserted.name values (@p0)");

        ctx.CreateInsertBuilder<IInsertEntity>()
            .Value(x => x.Name, "a")
            .Returning(x => new { x.Id, x.Name })
            .OutputIntoTableVariable("@t", "id bigint, name nvarchar(100)")
            .ToSql()
            .Should().Be("declare @t table (id bigint, name nvarchar(100)); insert into insert_entity (name) output inserted.id, inserted.name into @t (id, name) values (@p0); select id, name\n from @t");
    }

    [Fact]
    public void T266_MergeWithoutReturning_RemainsUnchanged()
    {
        using var ctx = SqlServerTestContext.Create();

        var sql = ctx.CreateMergeBuilder<IMergeEntity>()
            .Using(new MergeEntity { Id = 1, Name = "a", Age = 5 })
            .OnKeys()
            .WhenMatchedUpdate()
            .WhenNotMatchedInsert()
            .ToSql();

        sql.Should().Be("merge into merge_entity as target using (values (@p0, @p1, @p2)) as source (id, name, age) on target.id = source.id when matched then update set target.name = source.name, target.age = source.age when not matched then insert (id, name, age) values (source.id, source.name, source.age);");
        sql.Should().NotContain("output");
        sql.Should().NotContain("declare");

        // The MERGE capture is a BatchBuilder step; the fluent MERGE surface gained no output method.
        typeof(MergeBuilder<IMergeEntity>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Should().OnlyContain(name => !HasOutputForm(name));
        typeof(MergeReturningBuilder<IMergeEntity, object>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Should().OnlyContain(name => !HasOutputForm(name));
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

    // The two captures share the same predicate shape and the same captured member name ('min'); only
    // the per-step @b{i}_ prefix keeps the two bindings apart.
    private static void CaptureThreshold(IDataContext ctx, BatchBuilder batch, int value, string name)
    {
        var min = value;
        batch.OutputIntoTableVariable(
            ctx.CreateUpdateBuilder<IMergeEntity>()
                .Set(x => x.Name, "a")
                .Where(x => x.Id > min)
                .Returning(x => new T266CapturedRow { Id = x.Id }),
            name,
            "id bigint");
    }

    private static void ComposeThreshold(BatchBuilder batch, int value)
    {
        var min = value;
        batch.AddQuery(
            batch.FromOutput<T266CapturedRow>("@t")
                .Where(r => r.Id > min)
                .Select(r => new T266CapturedRow { Id = r.Id }));
    }

    private static void ComposeThreshold(BatchBuilder batch, int value, string name)
    {
        var min = value;
        batch.AddQuery(
            batch.FromOutput<T266CapturedRow>(name)
                .Where(r => r.Id > min)
                .Select(r => new T266CapturedRow { Id = r.Id }));
    }

    // Records each rendered step's parameters and SQL in the test output (-showLiveOutput), so the
    // binding values are evidence in the run log and not only inside an assertion expression.
    private static void DumpSteps(BatchPlan plan)
    {
        var output = TestContext.Current.TestOutputHelper;
        if (output is null)
            return;

        for (var i = 0; i < plan.Statements.Count; i++)
        {
            var statement = plan.Statements[i];
            var parameters = string.Join(", ", statement.Parameters.Select(p => $"{p.Name}={p.Value}"));
            output.WriteLine($"step{i}: [{parameters}] {statement.Sql}");
        }
    }

    private static BatchBuilder CaptureInsert(IDataContext ctx, string name)
        => ctx.CreateBatchBuilder().OutputIntoTableVariable(
            ctx.CreateInsertBuilder<IInsertEntity>()
                .Value(x => x.Name, "a")
                .Returning(x => new T266CapturedRow { Id = x.Id }),
            name,
            "id bigint");

    // Renders the batch through the same executor the public ToSql() uses, so the parameter bindings of
    // every step can be inspected; the step list is internal state the test assembly can see.
    private static BatchPlan RenderPlan(IDataContext ctx, BatchBuilder batch)
        => ((IBatchExecutor)ctx).RenderBatch(StepsOf(batch));

    private static int StepCount(BatchBuilder batch) => StepsOf(batch).Count;

    private static IReadOnlyList<BatchStepSpec> StepsOf(BatchBuilder batch)
        => (IReadOnlyList<BatchStepSpec>)typeof(BatchBuilder)
            .GetField("_steps", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(batch)!;

    private static long BoundValue(BatchStatement statement, string parameterName)
    {
        var parameter = statement.Parameters.SingleOrDefault(p => p.Name == parameterName);
        parameter.Should().NotBeNull("the rendered SQL binds '@{0}'", parameterName);
        return Convert.ToInt64(parameter!.Value);
    }

    private static bool HasOutputForm(string name)
        => name.Contains("Output", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Capture", StringComparison.OrdinalIgnoreCase);

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
