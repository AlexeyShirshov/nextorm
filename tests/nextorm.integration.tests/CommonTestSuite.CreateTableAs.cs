using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static string CreateTableAsName(int seed) => "ctas_" + seed.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void CreateTableAs_TempTable_ShouldBeReadableOnTheSameContext()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect, "This provider cannot materialise a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var marker = "ctas-" + Guid.NewGuid().ToString("N");
        var keep = Random.Shared.Next(1_000_000, int.MaxValue);
        var drop = keep + 1;
        var name = CreateTableAsName(keep);

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = keep, Name = marker, Age = 1 },
            new DeleteEntity { Id = drop, Name = marker, Age = 2 },
        ]).Insert();

        ctx.From<IDeleteEntity>()
            .Where(x => x.Id == keep)
            .Select(x => new { x.Id, x.Name })
            .ToTempTable(name);

        var rows = ctx.From(name)
            .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(keep);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void TempTableSource_ShouldMaterialiseOnRead()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "lazy-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var source = ctx.From<IDeleteEntity>()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name })
            .AsTempTable();

        var rows = ctx.From(source)
            .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void TempTableSource_ShouldRematerialiseOnEachExecution()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "lazy-repeat-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var source = ctx.From<IDeleteEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Id })
            .AsTempTable();

        ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle();

        // A second read must not fail with "table already exists": each read drops and re-creates it.
        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id + 1, Name = marker, Age = 2 })
            .Insert();

        ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().HaveCount(2);
    }

    [Fact]
    public void TempTableSource_NestedTempTables_ShouldWork()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "lazy-nested-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var first = ctx.From<IDeleteEntity>().Where(x => x.Name == marker).Select(x => new { x.Id }).AsTempTable();
        var second = ctx.From(first).Select(t => new { Id = t.GetInt32("id") }).AsTempTable();

        var rows = ctx.From(second).Select(t => new { Id = t.GetInt32("id") }).ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
    }

    [Fact]
    public void TempTableSource_Any_ShouldSeeTheMaterialisedRows()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "lazy-any-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var source = ctx.From<IDeleteEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Id })
            .AsTempTable();

        ctx.From(source).Where(t => t.GetInt32("id") == id).Any().Should().BeTrue();
        ctx.From(source).Where(t => t.GetInt32("id") == id + 1).Any().Should().BeFalse();
    }

    [Fact]
    public async Task TempTableSource_ShouldStreamTheRows()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "lazy-stream-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var source = ctx.From<IDeleteEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Id })
            .AsTempTable();

        var ids = new List<int>();
        await foreach (var row in ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToAsyncEnumerable(TestContext.Current.CancellationToken))
            ids.Add(row.Id);

        ids.Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    public void TempTableSource_ScalarTerminals_ShouldWork()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "lazy-scalar-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values([
                new DeleteEntity { Id = id, Name = marker, Age = 1 },
                new DeleteEntity { Id = id + 1, Name = marker, Age = 2 },
            ])
            .Insert();

        var source = ctx.From<IDeleteEntity>()
            .Where(x => x.Name == marker)
            .Select(x => new { x.Id })
            .AsTempTable();

        ctx.From(source).Where(t => t.GetInt32("id") >= id).Select(t => new { Id = t.GetInt32("id") }).First().Id.Should().Be(id);
        ctx.From(source).Count().Should().Be(2);
    }

    [Fact]
    public void TempTableSource_UnsupportedProvider_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsTemporaryCreateTableAsSelect, "This provider materialises a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        var source = ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id }).AsTempTable();

        var act = () => ctx.From(source).Select(t => new { Id = t.GetInt32("id") }).ToList();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateTableAs_TempTableIfNotExists_ShouldBeRepeatable()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect, "This provider cannot materialise a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);
        var options = new CreateTableOptions { IfNotExists = true };

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ctas-repeat", Age = 1 })
            .Insert();

        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id }).ToTempTable(name, options);
        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id }).ToTempTable(name, options);

        ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle();
    }

    [Fact]
    public void CreateTableAs_WithCteBody_ShouldBeReadableOnTheSameContext()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect, "This provider cannot materialise a query into a temporary table.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ctas-cte", Age = 1 })
            .Insert();

        var cte = ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id });
        ctx.With("recent", cte)
            .From("recent")
            .Select(t => new { Id = t.GetInt32("id") })
            .ToTempTable(name);

        ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle();
    }

    [Fact]
    public void CreateTableAs_Table_ShouldBeReadableOnTheSameContext()
    {
        Assert.SkipUnless(Provider.SupportsCreateTableAsSelect, "This provider cannot materialise a query into a table.");
        var ctx = _sut.DataProvider;
        var marker = "ctas-" + Guid.NewGuid().ToString("N");
        var keep = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(keep);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = keep, Name = marker, Age = 1 })
            .Insert();

        try
        {
            ctx.From<IDeleteEntity>()
                .Where(x => x.Id == keep)
                .Select(x => new { x.Id, x.Name })
                .ToTable(name);

            var rows = ctx.From(name)
                .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
                .ToList();

            rows.Should().ContainSingle();
            rows[0].Id.Should().Be(keep);
            rows[0].Name.Should().Be(marker);
        }
        finally
        {
            var connections = (IConnectionManager)ctx;
            connections.EnsureConnectionOpen();
            using var cmd = connections.GetConnection().CreateCommand();
            cmd.CommandText = "drop table if exists " + name;
            cmd.ExecuteNonQuery();
        }
    }

    [Fact]
    public void CreateTableAs_TableDropExisting_ShouldReplaceTheTable()
    {
        Assert.SkipUnless(Provider.SupportsCreateTableAsSelect, "This provider cannot materialise a query into a table.");
        var ctx = _sut.DataProvider;
        var marker = "ctas-drop-" + Guid.NewGuid().ToString("N");
        var first = Random.Shared.Next(1_000_000, int.MaxValue);
        var second = first + 1;
        var name = CreateTableAsName(first);

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = first, Name = marker, Age = 1 },
            new DeleteEntity { Id = second, Name = marker, Age = 2 },
        ]).Insert();

        try
        {
            ctx.From<IDeleteEntity>().Where(x => x.Id == first).Select(x => new { x.Id })
                .ToTable(name, new CreateTableOptions { DropExisting = true });
            ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle().Which.Id.Should().Be(first);

            // Re-running with a different query drops the existing table and replaces its rows.
            ctx.From<IDeleteEntity>().Where(x => x.Id == second).Select(x => new { x.Id })
                .ToTable(name, new CreateTableOptions { DropExisting = true });
            ctx.From(name).Select(t => new { Id = t.GetInt32("id") }).ToList().Should().ContainSingle().Which.Id.Should().Be(second);
        }
        finally
        {
            var connections = (IConnectionManager)ctx;
            connections.EnsureConnectionOpen();
            using var cmd = connections.GetConnection().CreateCommand();
            cmd.CommandText = "drop table if exists " + name;
            cmd.ExecuteNonQuery();
        }
    }

    [Fact]
    public void CreateTableAs_UnsupportedProvider_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsTemporaryCreateTableAsSelect, "This provider materialises a query into a temporary table.");
        var ctx = _sut.DataProvider;

        var act = () => ctx.From<IDeleteEntity>().Select(x => new { x.Id }).ToTempTable(CreateTableAsName(1));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Batch_CreateTempTableThenQuery_ShouldReadTheTableInTheSameBatch()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "batch-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var rows = ctx.Batch()
            .CreateTempTable(name, ctx.From<IDeleteEntity>()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.Name }))
            .Query(ctx.From(name).Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public async Task Batch_CreateTempTableThenQueryAsync_ShouldReadTheTableInTheSameBatch()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var marker = "batch-async-" + Guid.NewGuid().ToString("N");
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = marker, Age = 1 })
            .Insert();

        var rows = await ctx.Batch()
            .CreateTempTable(name, ctx.From<IDeleteEntity>()
                .Where(x => x.Id == id)
                .Select(x => new { x.Id, x.Name }))
            .Query(ctx.From(name).Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") }))
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public async Task Batch_CreateTempTableThenQuery_ToAsyncEnumerable_ShouldStreamTheRows()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "batch-stream", Age = 1 })
            .Insert();

        var rows = new List<int>();
        await foreach (var row in ctx.Batch()
            .CreateTempTable(name, ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Id }))
            .Query(ctx.From(name).Select(t => new { Id = t.GetInt32("id") }))
            .ToAsyncEnumerable(TestContext.Current.CancellationToken))
        {
            rows.Add(row.Id);
        }

        rows.Should().ContainSingle().Which.Should().Be(id);
    }

    [Fact]
    public void Batch_CapturedParameterWithSameName_ShouldNotCollideAcrossStatements()
    {
        Assert.SkipUnless(Provider.SupportsTemporaryCreateTableAsSelect && Provider.SupportsBatch, "This provider cannot run a CTAS batch.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var name = CreateTableAsName(id);

        ctx.InsertInto<IDeleteEntity>()
            .Values([
                new DeleteEntity { Id = id, Name = "batch-collide-a", Age = 1 },
                new DeleteEntity { Id = id + 1, Name = "batch-collide-b", Age = 2 },
            ])
            .Insert();

        var limit = id;
        var rows = ctx.Batch()
            .CreateTempTable(name, ctx.From<IDeleteEntity>().Where(x => x.Id >= limit).Select(x => new { x.Id }))
            .Query(ctx.From<IDeleteEntity>().Where(x => x.Id == limit).Select(x => new { x.Id }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
    }

    [Fact]
    public void Batch_UpdateThenQuery_ShouldSeeTheUpdateInTheSameBatch()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var marker = "batch-update-" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 1 })
            .Insert();

        var rows = ctx.Batch()
            .Update(ctx.Update<IDeleteEntity>().Set(x => x.Name, marker).Where(x => x.Id == id))
            .Query(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Name }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public async Task Batch_UpdateThenQueryAsync_ShouldSeeTheUpdateInTheSameBatch()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var marker = "batch-update-async-" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 1 })
            .Insert();

        var rows = await ctx.Batch()
            .Update(ctx.Update<IDeleteEntity>().Set(x => x.Name, marker).Where(x => x.Id == id))
            .Query(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Name }))
            .ToListAsync(TestContext.Current.CancellationToken);

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void Batch_DeleteThenQuery_ShouldSeeTheDeletionInTheSameBatch()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);

        ctx.InsertInto<IDeleteEntity>()
            .Values([
                new DeleteEntity { Id = id, Name = "batch-delete-a", Age = 1 },
                new DeleteEntity { Id = id + 1, Name = "batch-delete-b", Age = 2 },
            ])
            .Insert();

        var rows = ctx.Batch()
            .Delete(ctx.DeleteFrom<IDeleteEntity>().Where(x => x.Id == id || x.Id == id + 1))
            .Query(ctx.From<IDeleteEntity>().Where(x => x.Id == id || x.Id == id + 1).Select(x => new { x.Id }))
            .ToList();

        rows.Should().BeEmpty();
    }

    [Fact]
    public void Batch_InsertThenQuery_ShouldSeeTheInsertInTheSameBatch()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var marker = "batch-insert-" + Guid.NewGuid().ToString("N");

        var rows = ctx.Batch()
            .Insert(ctx.InsertInto<IDeleteEntity>().Values(new DeleteEntity { Id = id, Name = marker, Age = 1 }))
            .Query(ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Name }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void Batch_TwoMutations_ShouldShareParameterSequence()
    {
        Assert.SkipUnless(Provider.SupportsBatch, "This provider cannot run batches.");
        var ctx = _sut.DataProvider;
        var id = Random.Shared.Next(1_000_000, int.MaxValue);
        var marker = "batch-two-" + Guid.NewGuid().ToString("N");

        ctx.InsertInto<IDeleteEntity>()
            .Values([
                new DeleteEntity { Id = id, Name = "before", Age = 1 },
                new DeleteEntity { Id = id + 1, Name = "remove", Age = 2 },
            ])
            .Insert();

        var rows = ctx.Batch()
            .Update(ctx.Update<IDeleteEntity>().Set(x => x.Name, marker).Where(x => x.Id == id))
            .Delete(ctx.DeleteFrom<IDeleteEntity>().Where(x => x.Id == id + 1))
            .Query(ctx.From<IDeleteEntity>().Where(x => x.Id == id || x.Id == id + 1).Select(x => new { x.Id, x.Name }))
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Name.Should().Be(marker);
    }

    [Fact]
    public void Batch_UnsupportedProvider_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsBatch, "This provider supports batches.");
        var ctx = _sut.DataProvider;
        var name = CreateTableAsName(1);

        var act = () => ctx.Batch()
            .CreateTable(name, ctx.From<IDeleteEntity>().Select(x => new { x.Id }))
            .Query(ctx.From<IDeleteEntity>().Select(x => new { x.Id }))
            .ToSql();

        act.Should().Throw<NotSupportedException>();
    }}
