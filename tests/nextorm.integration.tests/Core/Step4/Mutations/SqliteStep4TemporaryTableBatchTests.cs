using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>
/// Step 4, batch 2 — the temporary-table, batch and bulk paths of
/// <c>src/nextorm.core/DataContext/DataContext.cs</c> on a live SQLite database.
/// <para>
/// The accepted paths create and read real temporary tables (eager <c>ToTempTable</c>/<c>ToTempTableAsync</c>
/// and the lazy <c>AsTempTable</c> source), run a batch that materialises a temporary table and reads it in
/// the same round trip, materialise two result sets of one batch, persist through the asynchronous bulk
/// terminal and read the generated identity through the provider's identity function. Each asserts the
/// computed rows or key, not merely that the statement ran.
/// </para>
/// <para>
/// The refusals are deterministic guards: a batch render of a query that reads no temporary table, a CSV or
/// data-reader terminal over a lazy temporary table, the synchronous bulk terminal over an asynchronous
/// source, and an already-cancelled token. Each is asserted by exception type plus the marker it carries.
/// </para>
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteStep4TemporaryTableBatchTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    private const string Ddl = """
        create table if not exists s4_dc_row (id integer primary key autoincrement, name text null, amount int not null default 0);
        delete from s4_dc_row;
        insert into s4_dc_row (id, name, amount) values (9001, 'a', 10), (9002, 'b', 20), (9003, 'c', 30);
        """;

    public SqliteStep4TemporaryTableBatchTests()
    {
        var dataContext = (DataContext)Context;
        dataContext.EnsureConnectionOpen();
        using var command = dataContext.GetConnection().CreateCommand();
        command.CommandText = Ddl;
        command.ExecuteNonQuery();
    }

    private IDataContext Context => _sut.DataProvider;

    // ---------------------------------------------------------------------------------------------
    // Temporary tables — real DROP + CREATE TEMPORARY TABLE AS + read
    // ---------------------------------------------------------------------------------------------

    /// <summary>Reading a lazy temporary table materialises it and streams its rows back in one batch.</summary>
    [Fact]
    public void LazyTempTable_ShouldMaterializeAndRead()
    {
        var source = Context.From<S4DcRow>()
            .Where(x => x.Amount >= 20)
            .Select(x => new { x.Id, x.Name })
            .AsTempTable();

        var rows = Context.From(source)
            .OrderBy(t => t.GetInt32("id"))
            .Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") })
            .ToList();

        rows.Should().HaveCount(2);
        rows.Select(r => r.Id).Should().Equal(9002, 9003);
        rows.Select(r => r.Name).Should().Equal("b", "c");
    }

    /// <summary>The eager temporary-table terminal creates the table, which the same session reads back.</summary>
    [Fact]
    public void ToTempTable_ShouldCreateAndRead()
    {
        Context.From<S4DcRow>()
            .Where(x => x.Amount >= 20)
            .Select(x => new { x.Id, x.Name })
            .ToTempTable("s4_dc_tmp_sync");

        var names = Context.From("s4_dc_tmp_sync")
            .OrderBy(t => t["id"].AsInt)
            .Select(t => t["name"].AsString)
            .ToList();

        names.Should().Equal("b", "c");
    }

    /// <summary>The asynchronous temporary-table terminal produces the same rows.</summary>
    [Fact]
    public async Task ToTempTableAsync_ShouldCreateAndRead()
    {
        await Context.From<S4DcRow>()
            .Where(x => x.Amount == 10)
            .Select(x => new { x.Id, x.Name })
            .ToTempTableAsync("s4_dc_tmp_async", cancellationToken: TestContext.Current.CancellationToken);

        var names = Context.From("s4_dc_tmp_async")
            .Select(t => t["name"].AsString)
            .ToList();

        names.Should().Equal("a");
    }

    /// <summary>The batch render of a query that reads no temporary table is refused, not degraded.</summary>
    [Fact]
    public void ToBatchSql_OnPlainQuery_ShouldRefuse()
    {
        var query = Context.From<S4DcRow>().Where(x => x.Amount > 0).Select(x => x.Id);

        var act = () => query.ToBatchSql();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*does not read a temporary table*AsTempTable*");
    }

    /// <summary>The batch render of a lazy temporary-table read contains the drop, the create and the read.</summary>
    [Fact]
    public void ToBatchSql_OnTempTableRead_ShouldRenderTheWholeBatch()
    {
        var source = Context.From<S4DcRow>()
            .Where(x => x.Amount == 30)
            .Select(x => new { x.Id })
            .AsTempTable();

        var sql = Context.From(source)
            .OrderBy(t => t.GetInt32("id"))
            .Select(t => t.GetInt32("id"))
            .ToBatchSql();

        sql.Should().Contain("drop table if exists", "the batch drops the previous materialisation first");
        sql.Should().Contain("create temporary table");
        sql.Should().Contain("select");
    }

    /// <summary>The CSV terminal cannot stream a batch-backed temporary table and says so.</summary>
    [Fact]
    public void WriteCsv_OnTempTableRead_ShouldRefuseWithTheCsvWording()
    {
        var source = Context.From<S4DcRow>()
            .Where(x => x.Amount >= 20)
            .Select(x => new { x.Id })
            .AsTempTable();

        var query = Context.From(source).OrderBy(t => t.GetInt32("id")).Select(t => t.GetInt32("id"));
        using var buffer = new MemoryStream();

        var act = () => query.WriteCsv(buffer);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*CSV terminal (WriteCsv/WriteCsvAsync)*lazy temporary table*Materialise the query first*");
    }

    /// <summary>The multi-column reader terminal refuses the same source with its own wording.</summary>
    [Fact]
    public void ToDataReader_OnTempTableRead_ShouldRefuseWithTheReaderWording()
    {
        var source = Context.From<S4DcRow>()
            .Where(x => x.Amount >= 20)
            .Select(x => new { x.Id })
            .AsTempTable();

        var query = Context.From(source).OrderBy(t => t.GetInt32("id")).Select(t => t.GetInt32("id"));

        var act = () => query.ToDataReader();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*terminal does not support a query that reads a lazy temporary table*Materialise the query first*");
    }

    // ---------------------------------------------------------------------------------------------
    // Batches — one round trip
    // ---------------------------------------------------------------------------------------------

    /// <summary>A batch materialises a temporary table and reads it in the same round trip.</summary>
    [Fact]
    public void Batch_CreateTempTableThenQuery_ShouldReturnTheMaterializedRows()
    {
        var source = Context.From<S4DcRow>().Where(x => x.Amount >= 20).Select(x => new { x.Id, x.Name });

        var rows = Context.CreateBatchBuilder()
            .CreateTempTable("s4_dc_batch_tmp", source)
            .Query(Context.From("s4_dc_batch_tmp").OrderBy(t => t["id"].AsInt).Select(t => t["id"].AsInt))
            .ToList();

        rows.Should().Equal(9002, 9003);
    }

    /// <summary>Two result sets added with <c>AddQuery</c> are materialised by one execution.</summary>
    [Fact]
    public void Batch_AddQueryTwice_ShouldMaterializeBothResultSets()
    {
        var result = Context.CreateBatchBuilder()
            .AddQuery(Context.From<S4DcRow>().OrderBy(x => x.Id).Select(x => x.Id))
            .AddQuery(Context.From<S4DcRow>().Where(x => x.Amount >= 30).Select(x => x.Name))
            .Execute();

        result.ResultSetCount.Should().Be(2);
        result.Read<int>().Should().Equal(9001, 9002, 9003);
        result.Read<string?>().Should().Equal("c");
    }

    // ---------------------------------------------------------------------------------------------
    // Bulk insert and the identity function
    // ---------------------------------------------------------------------------------------------

    /// <summary>The synchronous bulk terminal refuses an asynchronous row source instead of silently blocking.</summary>
    [Fact]
    public void BulkInsert_SyncTerminalOnAsyncSource_ShouldRefuse()
    {
        var builder = Context.CreateBulkInsertBuilder<S4DcRow>()
            .Values(RowsAsync(9041));

        var act = () => builder.BulkInsert();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*BulkInsert is synchronous but the source is async*BulkInsertAsync*");
    }

    /// <summary>The asynchronous bulk terminal persists every buffered row and reports the count.</summary>
    [Fact]
    public async Task BulkInsertAsync_WithBufferedRows_ShouldPersist()
    {
        var written = await Context.CreateBulkInsertBuilder<S4DcRow>()
            .Values(
            [
                new S4DcRow { Name = "bulk-x", Amount = 1 },
                new S4DcRow { Name = "bulk-y", Amount = 2 }
            ])
            .BulkInsertAsync(TestContext.Current.CancellationToken);

        written.Should().Be(2);
        Context.From<S4DcRow>()
            .Where(x => x.Name == "bulk-x" || x.Name == "bulk-y")
            .OrderBy(x => x.Amount)
            .Select(x => x.Name)
            .ToList()
            .Should().Equal("bulk-x", "bulk-y");
    }

    /// <summary>The identity function terminal returns the generated key, not the declared one.</summary>
    [Fact]
    public void Insert_ReturningIdentityFunction_ShouldReturnTheGeneratedKey()
    {
        var key = Context.CreateInsertBuilder<S4DcRow>()
            .Value(x => x.Name, "generated")
            .Value(x => x.Amount, 7)
            .ReturningIdentity<int>()
            .Single();

        key.Should().BeGreaterThan(9003, "SQLite assigns max(rowid)+1 for the new row");
        Context.From<S4DcRow>().Where(x => x.Id == key).Single().Name.Should().Be("generated");
    }

    // ---------------------------------------------------------------------------------------------
    // Cancellation — refused before any statement is dispatched
    // ---------------------------------------------------------------------------------------------

    /// <summary>A synchronous terminal observes an already-cancelled token before dispatch.</summary>
    [Fact]
    public void ToDataReader_WithCancelledToken_ShouldRefuseBeforeDispatch()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var query = Context.From<S4DcRow>().Where(x => x.Amount > 0).Select(x => x.Id);

        var act = () => query.ToDataReader(cancelled.Token);

        var thrown1 = act.Should().Throw<OperationCanceledException>().Which;
        thrown1.CancellationToken.Should().Be(cancelled.Token);
    }

    /// <summary>The asynchronous terminal observes the same token before touching the database.</summary>
    [Fact]
    public async Task ToListAsync_WithCancelledToken_ShouldRefuse()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => Context.From<S4DcRow>()
            .Where(x => x.Amount > 0)
            .Select(x => x.Id)
            .ToListAsync(cancelled.Token);

        var thrown2 = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown2.Which.CancellationToken.Should().Be(cancelled.Token);
    }

    private static async IAsyncEnumerable<S4DcRow> RowsAsync(int start)
    {
        await Task.Yield();
        yield return new S4DcRow { Id = start, Name = "async", Amount = 1 };
    }

    /// <summary>Marker row of the temporary-table, batch, bulk and identity facts.</summary>
    [SqlTable("s4_dc_row")]
    public sealed class S4DcRow
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("amount")]
        public int Amount { get; set; }
    }
}
