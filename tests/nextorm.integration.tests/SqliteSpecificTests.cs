using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests for behaviour that is specific to the SQLite provider and therefore not part of the
/// shared suite (SQLite has no ANY/ALL subquery support and sorts NULLs first).
/// </summary>
[Collection("Sqlite")]
public sealed class SqliteSpecificTests : ProviderTestSuite
{
    protected override ITestProvider Provider => SqliteTestProvider.Instance;

    [Fact]
    public async Task WhereAnySubQuery_ShouldThrow()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == SqlFunctions.Sql.any(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task WhereAllSubQuery_ShouldThrow()
    {
        var test = async () =>
        {
            var r = await _sut.SimpleEntity.Where(it => it.Id == SqlFunctions.Sql.all(_sut.ComplexEntity.Select(it => it.Id))).Select(it => it.Id).ToListAsync();

            r.Should().NotBeEmpty();
            r.Count.Should().Be(3);
        };

        await test.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public void OrderBy2_NullsFirst_ShouldSortData()
    {
        // SQLite orders NULLs first, so the NULL Int group (id = 1) is the leading sort key.
        var r = _sut.ComplexEntity.OrderBy(it => it.Int).OrderByDescending(it => it.Id).Select(it => new { it.Id }).ToList();

        r[0].Id.Should().Be(1);
        r[1].Id.Should().Be(3);
        r[2].Id.Should().Be(2);
    }

    [Fact]
    public void InfoFunctions_ShouldReturnSqliteVersion()
    {
        var r = _sut.SimpleEntity
            .Select(x => SqlFunctions.Sql.version())
            .First();

        r.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void DateAdd_ShouldShiftDate()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_add("day", 1, x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
    }

    [Fact]
    public void DateDiff_ShouldCountDayBoundaries()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)))
            .First();

        r.Should().Be(3);
    }

    [Fact]
    public void EndOfMonth_ShouldReturnLastDay()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sql.end_of_month(x.Datetime))
            .First();

        r.Should().Be(new DateTime(2023, 1, 31));
    }

    [Fact]
    public void StringAgg_ShouldConcatenateGroupValues()
    {
        var r = _sut.ComplexEntity
            .Where(x => x.String != null)
            .Select(x => SqlFunctions.Sql.string_agg(x.String, ","))
            .First();

        r.Should().NotBeNull();
        r.Should().Contain("dadfasd").And.Contain("xxx");
    }

    [Fact]
    public void DateProjection_ShouldMaterialiseColumns()
    {
        // Exercises the row mapper (not the scalar path): SQLite returns datetime() as TEXT and the
        // diff as INTEGER, so both have to land in the DateTime?/int? projection members.
        var r = _sut.ComplexEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Added = SqlFunctions.Sql.date_add("day", 1, x.Datetime),
                Days = SqlFunctions.Sql.date_diff("day", x.Datetime, SqlFunctions.Sql.date_add("day", 3, x.Datetime)),
                Last = SqlFunctions.Sql.end_of_month(x.Datetime)
            })
            .First();

        r.Added.Should().Be(new DateTime(2023, 1, 2, 10, 0, 0));
        r.Days.Should().Be(3);
        r.Last.Should().Be(new DateTime(2023, 1, 31));
    }

    /// <summary>
    /// A correlated scalar whose projection is a non-nullable value type cannot yield a value when no
    /// row matches: SQL returns NULL and the reader getter throws. Project as <c>int?</c> (inside the
    /// subquery: <c>Select(s =&gt; (int?)s.Id)</c>) to get <c>null</c> instead. The exception type is
    /// provider-specific, hence this test is not in the shared suite.
    /// </summary>
    [Fact]
    public void CorrelatedScalarFirst_ShouldThrowWhenNoRowMatches()
    {
        var act = () => _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).First()
            })
            .ToList();

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// SQLite does not enforce scalar-subquery cardinality (it returns the first row), so a numeric
    /// Single/SingleOrDefault scalar subquery is rendered with a count guard. A single matching row
    /// returns the value.
    /// </summary>
    [Fact]
    public void CorrelatedScalarSingle_ShouldReturnTheSingleValue()
    {
        var r = _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new
            {
                s.Id,
                cid = _sut.ComplexEntity.Where(c => c.Id == s.Id).Select(c => (int?)c.Id).Single()
            })
            .ToList();

        r.Should().ContainSingle();
        r[0].cid.Should().Be(1);
    }

    /// <summary>
    /// More than one matching row must raise through the guard instead of silently returning the first.
    /// </summary>
    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenMultipleRowsMatch()
    {
        var act = () => _sut.SimpleEntity
            .Where(s => s.Id == 1)
            .Select(s => new
            {
                s.Id,
                cid = _sut.ComplexEntity.Select(c => (int?)c.Id).Single()
            })
            .ToList();

        act.Should().Throw<Microsoft.Data.Sqlite.SqliteException>();
    }

    /// <summary>No matching row yields the default for SingleOrDefault (same as enforcing providers).</summary>
    [Fact]
    public void CorrelatedScalarSingleOrDefault_ShouldYieldDefaultWhenNoRowMatches()
    {
        var r = _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).SingleOrDefault()
            })
            .ToList();

        r.Should().NotBeEmpty();
        r.Should().OnlyContain(row => row.sid == 0);
    }

    /// <summary>No matching row throws for Single on a non-nullable projection (same as enforcing providers).</summary>
    [Fact]
    public void CorrelatedScalarSingle_ShouldThrowWhenNoRowMatches()
    {
        var act = () => _sut.ComplexEntity
            .Select(it => new
            {
                it.Id,
                sid = _sut.SimpleEntity.Where(s => s.Id == it.Id + 100).Select(s => s.Id).Single()
            })
            .ToList();

        act.Should().Throw<Exception>();
    }

    [Fact]
    public void Cte_Recursive_WithDistinctUnion_ShouldProduceNumberSeries()
    {
        var ctx = _sut.DataProvider;

        var anchor = _sut.SimpleEntity.Where(s => s.Id == 1).Select(s => new CommonTestSuite.CteNumberRow { n = s.Id });
        var step = ctx.From("nums").Where(t => t["n"].AsInt < 5).Select(t => new CommonTestSuite.CteNumberRow { n = t["n"].AsInt + 1 });
        var body = anchor.Union(step);

        var rows = ctx
            .WithRecursive("nums", body)
            .From("nums")
            .Select(t => new CommonTestSuite.CteNumberRow { n = t["n"].AsInt })
            .ToList();

        rows.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3, 4, 5);
    }

    // --- SQLite LOB streaming (issue #27) ---------------------------------------------------------
    // The generated LOB SQL appends a trailing `rowid` locator so Microsoft.Data.Sqlite exposes a
    // streaming `SqliteBlob`; the payload stays at ordinal 0. These facts are SQLite-only: a source
    // without `rowid` (a view, a `WITHOUT ROWID` table, or a raw `WithSql` projection) fails closed
    // with the driver's raw exception, and they must not be lifted into the shared suite without a
    // `Dialect.LobLocatorColumn != null` / rowid-bearing guard (PostgreSQL/SQL Server have no
    // locator, so their terminal accepts a plain single-column projection instead).
    // ----------------------------------------------------------------------------------------------

    /// <summary>
    /// The LOB locator is fail-closed: a view has no <c>rowid</c>, so the generated
    /// <c>select data, rowid from ...</c> is rejected by the driver and the raw exception surfaces.
    /// Only the raw exception type is asserted; its exact text is a Microsoft.Data.Sqlite detail.
    /// </summary>
    [Fact]
    public void LobStream_OnView_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobView();

        var command = ctx.From<ILobViewEntity>().Where(it => it.Id == 1).Select(it => it.Data!);

        var act = () => command.ToStream();

        act.Should().Throw<SqliteException>();
    }

    /// <summary>Text LOB on a view: the same fail-closed locator path as the binary terminal.</summary>
    [Fact]
    public void LobTextReader_OnView_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobView();

        var command = ctx.From<ILobViewEntity>().Where(it => it.Id == 1).Select(it => it.Body!);

        var act = () => command.ToTextReader();

        act.Should().Throw<SqliteException>();
    }

    /// <summary>The async terminal opens the reader first, so the driver error surfaces from the returned task too.</summary>
    [Fact]
    public async Task LobStreamAsync_OnView_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobView();

        var command = ctx.From<ILobViewEntity>().Where(it => it.Id == 1).Select(it => it.Data!);

        var act = async () => await command.ToStreamAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task LobTextReaderAsync_OnView_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobView();

        var command = ctx.From<ILobViewEntity>().Where(it => it.Id == 1).Select(it => it.Body!);

        var act = async () => await command.ToTextReaderAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SqliteException>();
    }

    /// <summary>
    /// A <c>WITHOUT ROWID</c> table has no rowid either, so the locator SQL fails closed the same way.
    /// </summary>
    [Fact]
    public void LobStream_OnWithoutRowidTable_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobWithoutRowidTable();

        var command = ctx.From<ILobWithoutRowidEntity>().Where(it => it.Id == 1).Select(it => it.Data!);

        var act = () => command.ToStream();

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public void LobTextReader_OnWithoutRowidTable_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobWithoutRowidTable();

        var command = ctx.From<ILobWithoutRowidEntity>().Where(it => it.Id == 1).Select(it => it.Body!);

        var act = () => command.ToTextReader();

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public async Task LobStreamAsync_OnWithoutRowidTable_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobWithoutRowidTable();

        var command = ctx.From<ILobWithoutRowidEntity>().Where(it => it.Id == 1).Select(it => it.Data!);

        var act = async () => await command.ToStreamAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SqliteException>();
    }

    [Fact]
    public async Task LobTextReaderAsync_OnWithoutRowidTable_ShouldThrowRawSqliteException()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobWithoutRowidTable();

        var command = ctx.From<ILobWithoutRowidEntity>().Where(it => it.Id == 1).Select(it => it.Body!);

        var act = async () => await command.ToTextReaderAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SqliteException>();
    }

    /// <summary>
    /// There is no buffered fallback: after a fail-closed LOB attempt (binary and text, sync and
    /// async) the same <see cref="DataContext"/> still serves an ordinary buffered query.
    /// </summary>
    [Fact]
    public async Task LobFailClosed_ShouldKeepContextUsable()
    {
        var ctx = _sut.DataProvider;
        using var cleanup = SetUpLobView();

        var blob = ctx.From<ILobViewEntity>().Where(it => it.Id == 1).Select(it => it.Data!);
        FluentActions.Invoking(() => blob.ToStream()).Should().Throw<SqliteException>();

        var text = ctx.From<ILobViewEntity>().Where(it => it.Id == 1).Select(it => it.Body!);
        var textAct = async () => await text.ToTextReaderAsync(TestContext.Current.CancellationToken);
        await textAct.Should().ThrowAsync<SqliteException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    /// <summary>
    /// A raw <c>WithSql</c> override bypasses the SQL builder, so no trailing <c>rowid</c> can be
    /// added: a single-column raw LOB is rejected outright rather than silently switching to a
    /// buffered read. A raw projection with several columns keeps the generic projection-mismatch
    /// error (that generic shape is also exercised through the shared LOB suite).
    /// </summary>
    [Fact]
    public void LobStream_RawSqlSingleColumn_ShouldThrowNotSupported()
    {
        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data from lob_entity where id = 1");

        var act = () => command.ToStream();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQLite LOB streaming with raw SQL (WithSql) is not supported because a rowid locator cannot be added safely.");
    }

    [Fact]
    public void LobTextReader_RawSqlSingleColumn_ShouldThrowNotSupported()
    {
        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .WithSql("select body from lob_entity where id = 1");

        var act = () => command.ToTextReader();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("SQLite LOB streaming with raw SQL (WithSql) is not supported because a rowid locator cannot be added safely.");
    }

    [Fact]
    public void LobStream_RawSqlMultipleColumns_ShouldThrowInvalidOperation()
    {
        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data, id from lob_entity where id = 1");

        var act = () => command.ToStream();

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// A NULL blob cannot be opened as a stream by Microsoft.Data.Sqlite: the driver throws its raw
    /// <see cref="SqliteException"/> instead of a nextorm-specific error. This pins the actual
    /// behavior so a future silent buffered substitution is caught. Source <c>binary_entity</c> row 2
    /// is a rowid table with a NULL <c>data</c> column.
    /// </summary>
    [Fact]
    public void LobStream_NullBlob_ShouldThrowRawSqliteException()
    {
        var command = _sut.BinaryEntity.Where(it => it.Id == 2).Select(it => it.Data!);

        var act = () => command.ToStream();

        act.Should().Throw<SqliteException>();
    }

    [Fact]
    public async Task LobStreamAsync_NullBlob_ShouldThrowRawSqliteException()
    {
        var command = _sut.BinaryEntity.Where(it => it.Id == 2).Select(it => it.Data!);

        var act = async () => await command.ToStreamAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<SqliteException>();
    }

    /// <summary>
    /// A NULL text value yields an empty reader (Microsoft.Data.Sqlite maps it to an empty string
    /// reader) — it must not throw. Source <c>complex_entity</c> row 3 is a rowid table with a NULL
    /// <c>somestring</c> column.
    /// </summary>
    [Fact]
    public void LobTextReader_NullText_ShouldReturnEmpty()
    {
        using var reader = _sut.ComplexEntity.Where(it => it.Id == 3).Select(it => it.String!).ToTextReader();

        reader.ReadToEnd().Should().BeEmpty();
    }

    [Fact]
    public async Task LobTextReaderAsync_NullText_ShouldReturnEmpty()
    {
        using var reader = await _sut.ComplexEntity
            .Where(it => it.Id == 3)
            .Select(it => it.String!)
            .ToTextReaderAsync(TestContext.Current.CancellationToken);

        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        text.Should().BeEmpty();
    }

    /// <summary>
    /// Async terminal with a positional parameter on a normal rowid table: the parameter is bound to
    /// the generated <c>select data, rowid</c> command. The seed is 8 MiB of <c>0xAB</c>.
    /// </summary>
    [Fact]
    public async Task LobStreamAsync_WithParameter_ShouldRoundTripAllBytes()
    {
        await using var stream = await _sut.LobEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Data!)
            .ToStreamAsync(TestContext.Current.CancellationToken, 1);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        var bytes = buffer.ToArray();
        bytes.Should().HaveCount(8 * 1024 * 1024);
        bytes.Should().OnlyContain(b => b == 0xAB);
    }

    /// <summary>The text twin: the seed is 8 MiB of <c>'x'</c>.</summary>
    [Fact]
    public async Task LobTextReaderAsync_WithParameter_ShouldRoundTripAllChars()
    {
        using var reader = await _sut.LobEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Body!)
            .ToTextReaderAsync(TestContext.Current.CancellationToken, 1);

        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        text.Length.Should().Be(8 * 1024 * 1024);
        text.All(c => c == 'x').Should().BeTrue();
    }

    /// <summary>
    /// <c>ToDataReader</c> is a supported terminal on SQLite (issue #134): it uses the locator-free,
    /// buffered result path, so a multi-column projection exposes exactly the projected columns with no
    /// trailing <c>rowid</c> locator. These positive tests replace the former fail-closed expectations;
    /// the PostgreSQL/SQL Server sequential contract is unchanged in the shared suite.
    /// </summary>
    [Fact]
    public void LobDataReader_ToDataReader_SingleColumn_ShouldReadEveryRow()
    {
        using var reader = _sut.SimpleEntity.OrderBy(it => it.Id).Select(it => it.Id).ToDataReader();

        reader.FieldCount.Should().Be(1, "the locator-free path must not append a rowid column");
        var ids = new List<int>();
        while (reader.Read())
            ids.Add(reader.GetInt32(0));

        ids.Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    [Fact]
    public void LobDataReader_ToDataReader_MultiColumn_ShouldReadEveryRow()
    {
        using var reader = _sut.ComplexEntity
            .OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.String })
            .ToDataReader();

        reader.FieldCount.Should().Be(2, "SQLite's ToDataReader must not append a rowid locator");
        var rows = new List<(long Id, string? Text)>();
        while (reader.Read())
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));

        rows.Should().Equal((1L, "dadfasd"), (2L, "xxx"), (3L, null));
    }

    [Fact]
    public async Task LobDataReader_ToDataReaderAsync_MultiColumn_ShouldReadEveryRow()
    {
        await using var reader = await _sut.ComplexEntity
            .OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.String })
            .ToDataReaderAsync(TestContext.Current.CancellationToken);

        reader.FieldCount.Should().Be(2);
        var rows = new List<(long Id, string? Text)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));

        rows.Should().Equal((1L, "dadfasd"), (2L, "xxx"), (3L, null));
    }

    [Fact]
    public void LobDataReader_ToDataReader_BufferedBlobAndText_ShouldRoundTripNullEmptyNonEmpty()
    {
        using var cleanup = SetUpLobReaderProbeTable();
        using var reader = _sut.DataProvider.From<ILobReaderProbeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Payload, x.Body })
            .ToDataReader();

        reader.FieldCount.Should().Be(3);
        reader.GetName(1).Should().Be("payload");
        reader.GetName(2).Should().Be("body");

        var rows = new List<(int Id, byte[]? Payload, string? Body)>();
        while (reader.Read())
        {
            var payload = reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1);
            var body = reader.IsDBNull(2) ? null : reader.GetString(2);
            rows.Add((reader.GetInt32(0), payload, body));
        }

        rows.Should().HaveCount(3);
        rows[0].Id.Should().Be(1);
        rows[0].Payload.Should().Equal(new byte[] { 0x01, 0x02 });
        rows[0].Body.Should().Be("hello");
        rows[1].Payload.Should().BeEmpty("an empty blob must read back as an empty array, not null");
        rows[1].Body.Should().BeEmpty("an empty string must read back as empty, not null");
        rows[2].Payload.Should().BeNull();
        rows[2].Body.Should().BeNull();
    }

    [Fact]
    public async Task LobDataReader_ToDataReaderAsync_BufferedBlobAndText_ShouldRoundTripNullEmptyNonEmpty()
    {
        using var cleanup = SetUpLobReaderProbeTable();
        await using var reader = await _sut.DataProvider.From<ILobReaderProbeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Payload, x.Body })
            .ToDataReaderAsync(TestContext.Current.CancellationToken);

        reader.FieldCount.Should().Be(3);
        var rows = new List<(int Id, byte[]? Payload, string? Body)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            var payload = reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1);
            var body = reader.IsDBNull(2) ? null : reader.GetString(2);
            rows.Add((reader.GetInt32(0), payload, body));
        }

        rows.Should().HaveCount(3);
        rows[0].Id.Should().Be(1);
        rows[0].Payload.Should().Equal(new byte[] { 0x01, 0x02 });
        rows[0].Body.Should().Be("hello");
        rows[1].Payload.Should().BeEmpty();
        rows[1].Body.Should().BeEmpty();
        rows[2].Payload.Should().BeNull();
        rows[2].Body.Should().BeNull();
    }

    [Fact]
    public void LobDataReader_ToDataReader_CancelledToken_ShouldThrowAndKeepContextUsable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = () => command.ToDataReader(cts.Token);

        act.Should().Throw<OperationCanceledException>();
        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public async Task LobDataReader_ToDataReaderAsync_CancelledToken_ShouldThrowAndKeepContextUsable()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = async () => await command.ToDataReaderAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public void LobDataReader_ToDataReader_Dispose_ShouldReleaseReaderAndKeepContextUsable()
    {
        var reader = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String }).ToDataReader();

        reader.Read().Should().BeTrue();
        reader.Dispose();
        reader.Dispose();

        var act = () => reader.Read();
        act.Should().Throw<ObjectDisposedException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    private IDisposable SetUpLobView()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop view if exists lob_view");
        Execute(ctx, "drop table if exists lob_view_source");
        Execute(ctx, "create table lob_view_source (id integer primary key, data blob, body text)");
        Execute(ctx, "insert into lob_view_source (id, data, body) values (1, x'01020304', 'abcd')");
        Execute(ctx, "create view lob_view as select id, data, body from lob_view_source");
        return new TeardownAction(() =>
        {
            Execute(ctx, "drop view if exists lob_view");
            Execute(ctx, "drop table if exists lob_view_source");
        });
    }

    private IDisposable SetUpLobWithoutRowidTable()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists lob_without_rowid");
        Execute(ctx, "create table lob_without_rowid (id integer primary key, data blob, body text) without rowid");
        Execute(ctx, "insert into lob_without_rowid (id, data, body) values (1, x'01020304', 'abcd')");
        return new TeardownAction(() => Execute(ctx, "drop table if exists lob_without_rowid"));
    }

    /// <summary>Seed for the buffered multi-column reader: non-empty, empty and NULL blob/text values.</summary>
    private IDisposable SetUpLobReaderProbeTable()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists lob_reader_probe");
        Execute(ctx, "create table lob_reader_probe (id integer primary key, payload blob, body text)");
        Execute(ctx, "insert into lob_reader_probe (id, payload, body) values (1, x'0102', 'hello')");
        Execute(ctx, "insert into lob_reader_probe (id, payload, body) values (2, x'', '')");
        Execute(ctx, "insert into lob_reader_probe (id, payload, body) values (3, null, null)");
        return new TeardownAction(() => Execute(ctx, "drop table if exists lob_reader_probe"));
    }

    private sealed class TeardownAction(Action action) : IDisposable
    {
        public void Dispose() => action();
    }

    [SqlTable("lob_reader_probe")]
    internal interface ILobReaderProbeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("payload")]
        byte[]? Payload { get; set; }
        [Column("body")]
        string? Body { get; set; }
    }

    [SqlTable("lob_view")]
    internal interface ILobViewEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("data")]
        byte[]? Data { get; set; }
        [Column("body")]
        string? Body { get; set; }
    }

    [SqlTable("lob_without_rowid")]
    internal interface ILobWithoutRowidEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("data")]
        byte[]? Data { get; set; }
        [Column("body")]
        string? Body { get; set; }
    }

    [SqlTable("collation_probe")]
    internal interface ICollationProbe
    {
        [Key]
        [Column("id")]
        long Id { get; set; }
        [Column("name")]
        [Collation("binary")]
        string? Name { get; set; }
    }

    [Fact]
    public void ColumnCollation_ShouldApplyDeclaredBinaryCollation()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists collation_probe");
        Execute(ctx, "create table collation_probe (id bigint, name varchar(50))");

        try
        {
            Execute(ctx, "insert into collation_probe (id, name) values (1, 'abc')");
            Execute(ctx, "insert into collation_probe (id, name) values (2, 'ABC')");

            ctx.From<ICollationProbe>().Where(x => x.Name == "abc").Select(x => x.Id).ToList()
                .Should().Equal(1L);
        }
        finally
        {
            Execute(ctx, "drop table if exists collation_probe");
        }
    }

    [Fact]
    public void SqlitePrintfAndHex_ShouldReturnValues()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                P = SqlFunctions.Sqlite.printf("%d-%s", 7, "x"),
                F = SqlFunctions.Sqlite.format("%d", 42),
                H = SqlFunctions.Sqlite.hex("AB"),
                O = SqlFunctions.Sqlite.octet_length("AB"),
                U = SqlFunctions.Sqlite.unicode("A"),
                C = SqlFunctions.Sqlite.@char(65, 66),
                T = SqlFunctions.Sqlite.@typeof(1)
            })
            .First();

        r.P.Should().Be("7-x");
        r.F.Should().Be("42");
        r.H.Should().Be("4142");
        r.O.Should().Be(2);
        r.U.Should().Be(65);
        r.C.Should().Be("AB");
        r.T.Should().Be("integer");
    }

    [Fact]
    public void SqliteUnhex_ShouldDecodeBytes()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => SqlFunctions.Sqlite.unhex("4142"))
            .First();

        r.Should().Equal(0x41, 0x42);
    }

    [Fact]
    public void SqliteJsonScalars_ShouldRoundTrip()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                Ex = SqlFunctions.Sqlite.json_extract<string>("{\"a\":\"hello\"}", "$.a"),
                Gt = SqlFunctions.Sqlite.json_get_text("{\"a\":\"hello\"}", "$.a"),
                G = SqlFunctions.Sqlite.json_get("{\"a\":\"hello\"}", "$.a"),
                Set = SqlFunctions.Sqlite.json_set("{\"a\":1}", "$.b", 2),
                Ar = SqlFunctions.Sqlite.json_array(1, 2),
                Ob = SqlFunctions.Sqlite.json_object("a", 1),
                Pa = SqlFunctions.Sqlite.json_patch("{\"a\":1}", "{\"b\":2}"),
                Va = SqlFunctions.Sqlite.json_valid("{\"a\":1}"),
                Ty = SqlFunctions.Sqlite.json_type("{\"a\":1}", "$.a")
            })
            .First();

        r.Ex.Should().Be("hello");
        r.Gt.Should().Be("hello");
        r.G.Should().Be("\"hello\"");
        r.Set.Should().Be("{\"a\":1,\"b\":2}");
        r.Ar.Should().Be("[1,2]");
        r.Ob.Should().Be("{\"a\":1}");
        r.Pa.Should().Be("{\"a\":1,\"b\":2}");
        r.Va.Should().BeTrue();
        r.Ty.Should().Be("integer");
    }

    [Fact]
    public void SqliteJsonGroupArray_ShouldAggregate()
    {
        var r = _sut.SimpleEntity
            .Select(x => SqlFunctions.Sqlite.json_group_array("v"))
            .First();

        r.Should().StartWith("[").And.EndWith("]").And.Contain("\"v\"");
    }

    [Fact]
    public void SqliteDateFunctions_ShouldReturnValues()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                U = SqlFunctions.Sqlite.unixepoch(new DateTime(1970, 1, 1, 0, 0, 10, DateTimeKind.Utc)),
                J = SqlFunctions.Sqlite.julianday(new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)),
                T = SqlFunctions.Sqlite.timediff(new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Utc), new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            })
            .First();

        r.U.Should().Be(10);
        r.J.Should().BeApproximately(2451545.0, 1e-6);
        r.T.Should().Be("+0000-00-01 00:00:00.000");
    }

    [Fact]
    public void SqliteMathFunctions_ShouldReturnValues()
    {
        var r = _sut.SimpleEntity
            .Where(x => x.Id == 1)
            .Select(x => new
            {
                A = SqlFunctions.Sqlite.acos(1.0),
                D = SqlFunctions.Sqlite.degrees(SqlFunctions.Sqlite.pi()),
                L2 = SqlFunctions.Sqlite.log2(8.0),
                M = SqlFunctions.Sqlite.mod(5.0, 2.0),
                R = SqlFunctions.Sqlite.radians(180.0)
            })
            .First();

        r.A.Should().BeApproximately(0.0, 1e-9);
        r.D.Should().BeApproximately(180.0, 1e-9);
        r.L2.Should().BeApproximately(3.0, 1e-9);
        r.M.Should().BeApproximately(1.0, 1e-9);
        r.R.Should().BeApproximately(Math.PI, 1e-9);
    }

    [Fact]
    public void SqliteJsonEachTableFunction_ShouldReturnRows()
    {
        var values = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Sqlite.json_each("[\"a\",\"b\",\"c\"]"))
            .Select(r => r.Value)
            .ToList();

        values.Should().BeEquivalentTo(new[] { "a", "b", "c" });
    }

    [Fact]
    public void SqliteJsonTreeTableFunction_ShouldReturnRows()
    {
        var count = _sut.DataProvider
            .FromTableFunction(() => SqlFunctions.Sqlite.json_tree("{\"a\":{\"b\":1}}"))
            .Select(r => r.FullKey)
            .ToList();

        count.Should().Contain("$.a");
        count.Should().Contain("$.a.b");
    }

    // =============================================================================================
    // SQLite full-text search FTS3/FTS4/FTS5 (#181): real virtual tables on the bundled SQLite,
    // compared against the same parameterised native statement on the same connection.
    // =============================================================================================

    [SqlTable("fts5_docs")]
    private sealed class Fts5Doc
    {
        [Key]
        [Column("rowid")]
        public long RowId { get; set; }
        [Column("title")]
        public string? Title { get; set; }
        [Column("body")]
        public string? Body { get; set; }
    }

    [SqlTable("fts4_docs")]
    private sealed class Fts4Doc
    {
        [Key]
        [Column("rowid")]
        public long RowId { get; set; }
        [Column("title")]
        public string? Title { get; set; }
        [Column("body")]
        public string? Body { get; set; }
    }

    [SqlTable("fts3_docs")]
    private sealed class Fts3Doc
    {
        [Key]
        [Column("rowid")]
        public long RowId { get; set; }
        [Column("title")]
        public string? Title { get; set; }
        [Column("body")]
        public string? Body { get; set; }
    }

    private sealed class FtsRankRow
    {
        [Column("rowid")]
        public long RowId { get; set; }
        [Column("score")]
        public double? Score { get; set; }
    }

    private static void ResetFtsTables(IDataContext ctx)
    {
        foreach (var table in new[] { "fts5_docs", "fts4_docs", "fts3_docs" })
            Execute(ctx, $"drop table if exists {table}");

        foreach (var (table, module) in new[] { ("fts5_docs", "fts5"), ("fts4_docs", "fts4"), ("fts3_docs", "fts3") })
        {
            Execute(ctx, $"create virtual table {table} using {module}(title, body)");
            Execute(ctx, $"insert into {table}(title, body) values " +
                         "('hello world', 'the quick brown fox'), " +
                         "('goodbye moon', 'fox jumps over the lazy dog'), " +
                         "('hello again', 'hello hello world'), " +
                         "('naïve café', 'Ünïcode wörld')");
        }
    }

    private static void RegisterRankUdf(IDataContext ctx)
    {
        var db = (DataContext)ctx;
        db.EnsureConnectionOpen();
        var connection = (SqliteConnection)db.GetConnection();
        connection.CreateFunction<byte[]?, long>("rank", static info =>
        {
            if (info is null)
                return 0;

            long sum = 0;
            foreach (var b in info)
                sum += b;

            return sum;
        });
    }

    private static List<long> NativeRowIds(IDataContext ctx, string table, string matchExpression, string query)
    {
        using var result = ctx.ExecuteRaw(
            $"select rowid from {table} where {matchExpression} match @q order by rowid",
            [new ProcedureParameter("q", query)]);
        return result.Read<long>().ToList();
    }

    // ---- FTS5 -----------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_TableMatch_ShouldFilterParameterisedAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var ids = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .OrderBy(x => x.RowId)
            .Select(x => x.RowId)
            .ToList();

        ids.Should().Equal(1L, 3L);
        ids.Should().Equal(NativeRowIds(ctx, "fts5_docs", "fts5_docs", query));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_ColumnMatch_ShouldFilterAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var ids = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match(x.Title, query))
            .OrderBy(x => x.RowId)
            .Select(x => x.RowId)
            .ToList();

        ids.Should().Equal(1L, 3L);
        ids.Should().Equal(NativeRowIds(ctx, "fts5_docs", "title", query));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Bm25_ShouldRankWithAndWithoutWeightsAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var rows = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.FTS5bm25("fts5_docs") })
            .ToList()
            .OrderBy(x => x.Score)
            .ThenBy(x => x.RowId)
            .ToList();

        var weighted = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.FTS5bm25("fts5_docs", 1.0, 2.0) })
            .ToList()
            .OrderBy(x => x.Score)
            .ThenBy(x => x.RowId)
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select rowid, bm25(fts5_docs) as score from fts5_docs where fts5_docs match @q order by bm25(fts5_docs), rowid",
            [new ProcedureParameter("q", query)]);
        var nativeRows = native.Read<FtsRankRow>();

        using var nativeWeighted = ctx.ExecuteRaw(
            "select rowid, bm25(fts5_docs, 1.0, 2.0) as score from fts5_docs where fts5_docs match @q order by bm25(fts5_docs, 1.0, 2.0), rowid",
            [new ProcedureParameter("q", query)]);
        var nativeWeightedRows = nativeWeighted.Read<FtsRankRow>();

        rows.Select(r => r.RowId).Should().Equal(nativeRows.Select(r => r.RowId));
        rows.Select(r => r.Score).Should().Equal(nativeRows.Select(r => r.Score));
        weighted.Select(r => r.RowId).Should().Equal(nativeWeightedRows.Select(r => r.RowId));
        weighted.Select(r => r.Score).Should().Equal(nativeWeightedRows.Select(r => r.Score));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Highlight_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var highlights = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .OrderBy(x => x.RowId)
            .Select(x => SqlFunctions.Sqlite.Highlight("fts5_docs", 0, "[", "]"))
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select highlight(fts5_docs, 0, '[', ']') from fts5_docs where fts5_docs match @q order by rowid",
            [new ProcedureParameter("q", query)]);
        var nativeValues = native.Read<string>().ToList();

        highlights.Should().Equal(nativeValues);
        highlights.Should().OnlyContain(x => x.Contains("[hello]"));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_Snippet_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "fox";

        var snippets = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .OrderBy(x => x.RowId)
            .Select(x => SqlFunctions.Sqlite.Snippet("fts5_docs", 1, "[", "]", "...", 8))
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select snippet(fts5_docs, 1, '[', ']', '...', 8) from fts5_docs where fts5_docs match @q order by rowid",
            [new ProcedureParameter("q", query)]);
        var nativeValues = native.Read<string>().ToList();

        snippets.Should().Equal(nativeValues);
        snippets.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_RankHiddenColumn_ShouldMatchBm25()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var ranks = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .Select(x => new { x.RowId, Rank = SqlFunctions.Sqlite.Rank("fts5_docs") })
            .OrderBy(x => x.RowId)
            .ToList();

        var bm25 = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.FTS5bm25("fts5_docs") })
            .OrderBy(x => x.RowId)
            .ToList();

        ranks.Select(r => r.RowId).Should().Equal(bm25.Select(r => r.RowId));
        ranks.Select(r => r.Rank!.Value).Should().Equal(bm25.Select(r => r.Score!.Value));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_AdvancedQuerySyntax_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);

        foreach (var query in new[] { "hel*", "\"hello world\"", "hello OR goodbye", "café", "wörld" })
        {
            var ids = ctx.From<Fts5Doc>()
                .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
                .OrderBy(x => x.RowId)
                .Select(x => x.RowId)
                .ToList();

            ids.Should().Equal(NativeRowIds(ctx, "fts5_docs", "fts5_docs", query), "the query '{0}' must match native FTS5", query);
        }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MalformedQuery_ShouldThrowTheSameNativeError()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "nosuchcolumn:value";

        var act = () => ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .Select(x => x.RowId)
            .ToList();

        var thrown = act.Should().Throw<SqliteException>().Which;

        var native = () =>
        {
            using var result = ctx.ExecuteRaw("select rowid from fts5_docs where fts5_docs match @q", [new ProcedureParameter("q", query)]);
            result.Read<long>();
        };
        native.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(thrown.SqliteErrorCode);
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MatchTableFromSource_ShouldProjectAndCompose()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var rows = ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<Fts5Doc>("fts5_docs", query))
            .Where(x => x.RowId > 0)
            .OrderBy(x => x.RowId)
            .Select(x => new { x.RowId, x.Title })
            .ToList();

        rows.Select(r => r.RowId).Should().Equal(1L, 3L);
        rows.Should().OnlyContain(r => r.Title != null && r.Title.Contains("hello"));

        using var native = ctx.ExecuteRaw(
            "select rowid from fts5_docs('hello') order by rowid");
        native.Read<long>().Should().Equal(rows.Select(r => r.RowId));
    }

    // ---- FTS4 -----------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "181")]
    public void Fts4_Match_ShouldFilterAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "fox";

        var tableIds = ctx.From<Fts4Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts4_docs", query))
            .OrderBy(x => x.RowId)
            .Select(x => x.RowId)
            .ToList();

        var columnIds = ctx.From<Fts4Doc>()
            .Where(x => SqlFunctions.Sqlite.Match(x.Body, query))
            .OrderBy(x => x.RowId)
            .Select(x => x.RowId)
            .ToList();

        tableIds.Should().Equal(1L, 2L);
        tableIds.Should().Equal(NativeRowIds(ctx, "fts4_docs", "fts4_docs", query));
        columnIds.Should().Equal(NativeRowIds(ctx, "fts4_docs", "body", query));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts4_Helpers_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var row = ctx.From<Fts4Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts4_docs", query))
            .Select(x => new
            {
                RowId = SqlFunctions.Sqlite.RowId("fts4_docs"),
                Offsets = SqlFunctions.Sqlite.FTS3Offsets("fts4_docs"),
                MatchInfo = SqlFunctions.Sqlite.FTS3MatchInfo("fts4_docs"),
                MatchInfoPcx = SqlFunctions.Sqlite.FTS3MatchInfo("fts4_docs", "pcx"),
                Snippet = SqlFunctions.Sqlite.FTS3Snippet("fts4_docs"),
                SnippetArgs = SqlFunctions.Sqlite.FTS3Snippet("fts4_docs", "[", "]")
            })
            .OrderBy(x => x.RowId)
            .First();

        using var native = ctx.ExecuteRaw(
            "select rowid, offsets(fts4_docs) as offsets, matchinfo(fts4_docs) as matchinfo, " +
            "matchinfo(fts4_docs, 'pcx') as matchinfopcx, snippet(fts4_docs) as snippet, snippet(fts4_docs, '[', ']') as snippetargs " +
            "from fts4_docs where fts4_docs match @q order by rowid limit 1",
            [new ProcedureParameter("q", query)]);
        var nativeRow = native.Read<Fts4HelperRow>().Single();

        row.RowId.Should().Be(nativeRow.RowId);
        row.Offsets.Should().Be(nativeRow.Offsets);
        row.MatchInfo.Should().Equal(nativeRow.MatchInfo);
        row.MatchInfoPcx.Should().Equal(nativeRow.MatchInfoPcx);
        row.Snippet.Should().Be(nativeRow.Snippet);
        row.SnippetArgs.Should().Be(nativeRow.SnippetArgs);
    }

    private sealed class Fts4HelperRow
    {
        [Column("rowid")]
        public long RowId { get; set; }
        [Column("offsets")]
        public string? Offsets { get; set; }
        [Column("matchinfo")]
        public byte[]? MatchInfo { get; set; }
        [Column("matchinfopcx")]
        public byte[]? MatchInfoPcx { get; set; }
        [Column("snippet")]
        public string? Snippet { get; set; }
        [Column("snippetargs")]
        public string? SnippetArgs { get; set; }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts4_RankOverMatchInfo_ShouldMatchNativeWithUdf()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        RegisterRankUdf(ctx);
        var query = "hello";

        var scores = ctx.From<Fts4Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts4_docs", query))
            .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("fts4_docs")) })
            .OrderBy(x => x.RowId)
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select rowid, rank(matchinfo(fts4_docs)) as score from fts4_docs where fts4_docs match @q order by rowid",
            [new ProcedureParameter("q", query)]);
        var nativeRows = native.Read<FtsRankRow>();

        scores.Select(s => s.RowId).Should().Equal(nativeRows.Select(r => r.RowId));
        scores.Select(s => s.Score).Should().Equal(nativeRows.Select(r => r.Score));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts4_RankOverMatchInfo_ShouldFailWithoutUdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm.fts.norank.{Guid.NewGuid():N}.db");
        try
        {
            using var ctx = new SqliteDataContext($"Data Source='{path}'", new DataContextBuilder());
            Execute(ctx, "create virtual table fts4_docs using fts4(title, body)");
            Execute(ctx, "insert into fts4_docs(title, body) values ('hello world', 'x')");

            var act = () => ctx.From<Fts4Doc>()
                .Where(x => SqlFunctions.Sqlite.Match("fts4_docs", "hello"))
                .Select(x => SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("fts4_docs")))
                .First();

            act.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(1);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    // ---- FTS3 -----------------------------------------------------------------------------------

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_Match_ShouldFilterAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "fox";

        var tableIds = ctx.From<Fts3Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts3_docs", query))
            .OrderBy(x => x.RowId)
            .Select(x => x.RowId)
            .ToList();

        var columnIds = ctx.From<Fts3Doc>()
            .Where(x => SqlFunctions.Sqlite.Match(x.Body, query))
            .OrderBy(x => x.RowId)
            .Select(x => x.RowId)
            .ToList();

        tableIds.Should().Equal(1L, 2L);
        tableIds.Should().Equal(NativeRowIds(ctx, "fts3_docs", "fts3_docs", query));
        columnIds.Should().Equal(NativeRowIds(ctx, "fts3_docs", "body", query));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_Helpers_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        var row = ctx.From<Fts3Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts3_docs", query))
            .Select(x => new
            {
                RowId = SqlFunctions.Sqlite.RowId("fts3_docs"),
                Offsets = SqlFunctions.Sqlite.FTS3Offsets("fts3_docs"),
                MatchInfo = SqlFunctions.Sqlite.FTS3MatchInfo("fts3_docs"),
                Snippet = SqlFunctions.Sqlite.FTS3Snippet("fts3_docs", "[", "]", "...", 0, 8)
            })
            .OrderBy(x => x.RowId)
            .First();

        using var native = ctx.ExecuteRaw(
            "select rowid, offsets(fts3_docs) as offsets, matchinfo(fts3_docs) as matchinfo, snippet(fts3_docs, '[', ']', '...', 0, 8) as snippet " +
            "from fts3_docs where fts3_docs match @q order by rowid limit 1",
            [new ProcedureParameter("q", query)]);
        var nativeRow = native.Read<Fts3HelperRow>().Single();

        row.RowId.Should().Be(nativeRow.RowId);
        row.Offsets.Should().Be(nativeRow.Offsets);
        row.MatchInfo.Should().Equal(nativeRow.MatchInfo);
        row.Snippet.Should().Be(nativeRow.Snippet);
    }

    private sealed class Fts3HelperRow
    {
        [Column("rowid")]
        public long RowId { get; set; }
        [Column("offsets")]
        public string? Offsets { get; set; }
        [Column("matchinfo")]
        public byte[]? MatchInfo { get; set; }
        [Column("snippet")]
        public string? Snippet { get; set; }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_RankOverMatchInfo_ShouldMatchNativeWithUdf()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        RegisterRankUdf(ctx);
        var query = "hello";

        var scores = ctx.From<Fts3Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts3_docs", query))
            .Select(x => new { x.RowId, Score = SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("fts3_docs")) })
            .OrderBy(x => x.RowId)
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select rowid, rank(matchinfo(fts3_docs)) as score from fts3_docs where fts3_docs match @q order by rowid",
            [new ProcedureParameter("q", query)]);
        var nativeRows = native.Read<FtsRankRow>();

        scores.Select(s => s.RowId).Should().Equal(nativeRows.Select(r => r.RowId));
        scores.Select(s => s.Score).Should().Equal(nativeRows.Select(r => r.Score));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_RankOverMatchInfo_ShouldFailWithoutUdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm.fts3.norank.{Guid.NewGuid():N}.db");
        try
        {
            using var ctx = new SqliteDataContext($"Data Source='{path}'", new DataContextBuilder());
            Execute(ctx, "create virtual table fts3_docs using fts3(title, body)");
            Execute(ctx, "insert into fts3_docs(title, body) values ('hello world', 'x')");

            var act = () => ctx.From<Fts3Doc>()
                .Where(x => SqlFunctions.Sqlite.Match("fts3_docs", "hello"))
                .Select(x => SqlFunctions.Sqlite.Rank(SqlFunctions.Sqlite.FTS3MatchInfo("fts3_docs")))
                .First();

            act.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(1);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_AdvancedQuerySyntax_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);

        foreach (var query in new[] { "hel*", "\"hello world\"", "hello AND world", "hello OR goodbye", "hello NOT goodbye", "café", "wörld" })
        {
            var ids = ctx.From<Fts3Doc>()
                .Where(x => SqlFunctions.Sqlite.Match("fts3_docs", query))
                .OrderBy(x => x.RowId)
                .Select(x => x.RowId)
                .ToList();

            ids.Should().Equal(NativeRowIds(ctx, "fts3_docs", "fts3_docs", query), "the query '{0}' must match native FTS3", query);
        }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts4_AdvancedQuerySyntax_ShouldMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);

        foreach (var query in new[] { "hel*", "\"hello world\"", "hello AND world", "hello OR goodbye", "hello NOT goodbye", "café", "wörld" })
        {
            var ids = ctx.From<Fts4Doc>()
                .Where(x => SqlFunctions.Sqlite.Match("fts4_docs", query))
                .OrderBy(x => x.RowId)
                .Select(x => x.RowId)
                .ToList();

            ids.Should().Equal(NativeRowIds(ctx, "fts4_docs", "fts4_docs", query), "the query '{0}' must match native FTS4", query);
        }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts3_MalformedQuery_ShouldThrowTheSameNativeError()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "\"hello";

        var act = () => ctx.From<Fts3Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts3_docs", query))
            .Select(x => x.RowId)
            .ToList();

        var thrown = act.Should().Throw<SqliteException>().Which;

        var native = () =>
        {
            using var result = ctx.ExecuteRaw("select rowid from fts3_docs where fts3_docs match @q", [new ProcedureParameter("q", query)]);
            result.Read<long>();
        };
        native.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(thrown.SqliteErrorCode);
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts4_MalformedQuery_ShouldThrowTheSameNativeError()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "\"hello";

        var act = () => ctx.From<Fts4Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts4_docs", query))
            .Select(x => x.RowId)
            .ToList();

        var thrown = act.Should().Throw<SqliteException>().Which;

        var native = () =>
        {
            using var result = ctx.ExecuteRaw("select rowid from fts4_docs where fts4_docs match @q", [new ProcedureParameter("q", query)]);
            result.Read<long>();
        };
        native.Should().Throw<SqliteException>().Which.SqliteErrorCode.Should().Be(thrown.SqliteErrorCode);
    }

    // ---- FTS5 FROM alias / native rank oracle / token safety (R181-03, E181-14/15/17) --------------

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MatchTableFromSourceAliased_ShouldProjectAliasQualifiedAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var query = "hello";

        // The join forces the table-valued source to carry an alias; the projection must then qualify
        // rowid/title through that alias (distinct from the ordinary `FROM fts5_docs WHERE fts5_docs MATCH`).
        var rows = ctx
            .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<Fts5Doc>("fts5_docs", query))
            .Join(ctx.From<Fts5Doc>(), (a, b) => a.RowId == b.RowId)
            .Select(x => new { x.Item1.RowId, x.Item1.Title })
            .ToList()
            .OrderBy(r => r.RowId)
            .ToList();

        rows.Select(r => r.RowId).Should().Equal(1L, 3L);

        using var native = ctx.ExecuteRaw(
            "select a.rowid as rowid, a.title as title from fts5_docs('hello') as a " +
            "join fts5_docs as b on a.rowid = b.rowid order by a.rowid");
        var nativeRows = native.Read<Fts5Doc>();
        rows.Select(r => r.RowId).Should().Equal(nativeRows.Select(r => r.RowId));
        rows.Select(r => r.Title).Should().Equal(nativeRows.Select(r => r.Title));
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_RankHiddenColumn_ShouldMatchNativeConfiguredRankAndDifferFromBm25()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        // Reconfigure the FTS5 hidden `rank` column to a weighted bm25, so the native `rank` is no
        // longer equal to the unconfigured bm25() and the oracle is non-degenerate.
        ConfigureFts5Rank(ctx, "bm25(10.0, 1.0)");
        var query = "hello";

        var rows = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .Select(x => new { x.RowId, Rank = SqlFunctions.Sqlite.Rank("fts5_docs") })
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select rowid, rank as score from fts5_docs where fts5_docs match @q order by rank, rowid",
            [new ProcedureParameter("q", query)]);
        var nativeRows = native.Read<FtsRankRow>();

        // Deterministic ordering by the hidden rank equals the native ordering.
        rows.OrderBy(r => r.Rank).ThenBy(r => r.RowId).Select(r => r.RowId)
            .Should().Equal(nativeRows.Select(r => r.RowId));
        rows.OrderBy(r => r.RowId).Select(r => r.Rank!.Value)
            .Should().Equal(nativeRows.OrderBy(r => r.RowId).Select(r => r.Score!.Value));

        using var bm25 = ctx.ExecuteRaw(
            "select rowid, bm25(fts5_docs) as score from fts5_docs where fts5_docs match @q",
            [new ProcedureParameter("q", query)]);
        var bm25Rows = bm25.Read<FtsRankRow>();

        rows.OrderBy(r => r.RowId).Select(r => r.Rank!.Value)
            .Should().NotEqual(bm25Rows.OrderBy(r => r.RowId).Select(r => r.Score!.Value),
                "the configured hidden rank must demonstrably differ from the unconfigured bm25()");
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts_TokenAdversarial_ShouldStayEscapedAndNotDropSentinel()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        Execute(ctx, "create table if not exists sentinel(value text)");
        Execute(ctx, "insert into sentinel(value) values ('kept')");

        try
        {
            // The token is an inline literal so it is a constant in the expression tree; a captured
            // local would be rejected at translation as a computed value before it reaches SQLite.
            var act = () => ctx.From<Fts5Doc>()
                .Where(x => SqlFunctions.Sqlite.Match("fts5_docs\"; drop table sentinel; --", "hello"))
                .Select(x => x.RowId)
                .ToList();

            // The token stays one escaped identifier, so the query fails instead of executing the drop.
            act.Should().Throw<SqliteException>();

            using var check = ctx.ExecuteRaw("select count(*) from sentinel");
            check.Read<long>().Single().Should().Be(1, "the adversarial token must not be raw-injected");
        }
        finally
        {
            Execute(ctx, "drop table if exists sentinel");
        }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_MatchTableFromSourceAdversarialToken_ShouldStayEscapedAndNotDropSentinel()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        Execute(ctx, "create table if not exists sentinel(value text)");
        Execute(ctx, "insert into sentinel(value) values ('kept')");

        try
        {
            // The FROM table token is a verbatim argument echoed as one quoted identifier, so the
            // injected statement stays inside the identifier and SQLite rejects the missing table.
            var act = () => ctx
                .FromTableFunction(() => SqlFunctions.Sqlite.MatchTable<Fts5Doc>("fts5_docs\"; drop table sentinel; --", "hello"))
                .Select(x => x.RowId)
                .ToList();

            act.Should().Throw<SqliteException>();

            using var check = ctx.ExecuteRaw("select count(*) from sentinel");
            check.Read<long>().Single().Should().Be(1, "the adversarial FROM token must not raw-inject");
        }
        finally
        {
            Execute(ctx, "drop table if exists sentinel");
        }
    }

    [Fact]
    [Trait("Issue", "181")]
    public void Fts5_ApostropheScalarConstants_ShouldBindAndMatchNative()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        Execute(ctx, "insert into fts5_docs(title, body) values ('O''Brien', 'l''hopital')");
        var query = "hello";

        // Markers carrying apostrophes must be bound; before the fix an inline "O'[" broke the literal.
        var highlights = ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", query))
            .OrderBy(x => x.RowId)
            .Select(x => SqlFunctions.Sqlite.Highlight("fts5_docs", 0, "O'[", "]'"))
            .ToList();

        using var native = ctx.ExecuteRaw(
            "select highlight(fts5_docs, 0, @s, @e) from fts5_docs where fts5_docs match @q order by rowid",
            [new ProcedureParameter("s", "O'["), new ProcedureParameter("e", "]'"), new ProcedureParameter("q", query)]);
        var nativeValues = native.Read<string>().ToList();

        highlights.Should().Equal(nativeValues);
        highlights.Should().OnlyContain(x => x.Contains("O'[hello]'"));

        // An apostrophe-bearing search query is bound and reaches FTS5 as data: the statement still
        // fails with the native FTS5 syntax error (the exact same as a bound @q), never with the SQL
        // parse error an unescaped inline 'O'Brien' literal would produce.
        var ours = () => ctx.From<Fts5Doc>()
            .Where(x => SqlFunctions.Sqlite.Match("fts5_docs", "O'Brien"))
            .Select(x => x.RowId)
            .ToList();
        var nativeApostrophe = () =>
        {
            using var r = ctx.ExecuteRaw(
                "select rowid from fts5_docs where fts5_docs match @q",
                [new ProcedureParameter("q", "O'Brien")]);
            r.Read<long>();
        };

        var ourError = ours.Should().Throw<SqliteException>().Which;
        var nativeError = nativeApostrophe.Should().Throw<SqliteException>().Which;
        ourError.SqliteErrorCode.Should().Be(nativeError.SqliteErrorCode);
        ourError.Message.Should().Contain("fts5",
            "the apostrophe must reach FTS5 as bound data, not break the SQL text");
    }

    // =============================================================================================
    // FTS5 maintenance/control interface (#195): the operation is an INSERT through the FTS5 control
    // interface; on a real table the six operations must run, Rebuild must repair a stale
    // external-content index and the native SQLite errors must propagate unchanged.
    // =============================================================================================

    [Fact]
    [Trait("Issue", "195")]
    [Trait("Issue", "196")]
    public void Fts5Maintenance_AllOperations_ShouldExecuteAndKeepIndexQueryable()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        NativeRowIds(ctx, "fts5_docs", "fts5_docs", "hello").Should().Equal(1L, 3L);

        // D196 (#196): every operation-family result is captured and asserted nonnegative; FC195-1
        // does not freeze an exact affected-row count, so only ">= 0" is required.
        var autoMerge = ctx.CreateSqliteFts5CommandBuilder("fts5_docs").AutoMerge(4).Execute();
        var crisisMerge = ctx.CreateSqliteFts5CommandBuilder("fts5_docs").CrisisMerge(2).Execute();
        var merge = ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Merge(8).Execute();
        var integrityCheck = ctx.CreateSqliteFts5CommandBuilder("fts5_docs").IntegrityCheck().Execute();
        var optimize = ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Optimize().Execute();
        var rebuild = ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Rebuild().Execute();

        autoMerge.Should().BeGreaterThanOrEqualTo(0);
        crisisMerge.Should().BeGreaterThanOrEqualTo(0);
        merge.Should().BeGreaterThanOrEqualTo(0);
        integrityCheck.Should().BeGreaterThanOrEqualTo(0);
        optimize.Should().BeGreaterThanOrEqualTo(0);
        rebuild.Should().BeGreaterThanOrEqualTo(0);

        // The maintenance operations must not change the rows the index selects.
        NativeRowIds(ctx, "fts5_docs", "fts5_docs", "hello").Should().Equal(1L, 3L);
        NativeRowIds(ctx, "fts5_docs", "fts5_docs", "fox").Should().Equal(1L, 2L);
    }

    [Fact]
    [Trait("Issue", "195")]
    [Trait("Issue", "196")]
    public async Task Fts5Maintenance_AllOperations_ShouldExecuteAsyncAndKeepIndexQueryable()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);

        var cancellationToken = TestContext.Current.CancellationToken;

        // D196 (#196): every operation-family result is captured and asserted nonnegative; FC195-1
        // does not freeze an exact affected-row count.
        var autoMerge = await ctx.CreateSqliteFts5CommandBuilder("fts5_docs").AutoMerge(4).ExecuteAsync(cancellationToken);
        var crisisMerge = await ctx.CreateSqliteFts5CommandBuilder("fts5_docs").CrisisMerge(2).ExecuteAsync(cancellationToken);
        var merge = await ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Merge(8).ExecuteAsync(cancellationToken);
        var integrityCheck = await ctx.CreateSqliteFts5CommandBuilder("fts5_docs").IntegrityCheck().ExecuteAsync(cancellationToken);
        var optimize = await ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Optimize().ExecuteAsync(cancellationToken);
        var rebuild = await ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Rebuild().ExecuteAsync(cancellationToken);

        autoMerge.Should().BeGreaterThanOrEqualTo(0);
        crisisMerge.Should().BeGreaterThanOrEqualTo(0);
        merge.Should().BeGreaterThanOrEqualTo(0);
        integrityCheck.Should().BeGreaterThanOrEqualTo(0);
        optimize.Should().BeGreaterThanOrEqualTo(0);
        rebuild.Should().BeGreaterThanOrEqualTo(0);

        NativeRowIds(ctx, "fts5_docs", "fts5_docs", "hello").Should().Equal(1L, 3L);
    }

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_Rebuild_ShouldRepairStaleExternalContentIndex()
    {
        var ctx = _sut.DataProvider;
        CreateExternalContentFts(ctx, "ext_fts", "ext_content");

        // Fresh: the index was built from the content table.
        NativeRowIds(ctx, "ext_fts", "ext_fts", "hello").Should().Equal(1L);

        // Mutating the content table behind the index makes the index stale.
        Execute(ctx, "update ext_content set title = 'changed text' where id = 1");
        NativeRowIds(ctx, "ext_fts", "ext_fts", "hello").Should().Equal(new long[] { 1L }, "the stale index still reports the old token");
        NativeRowIds(ctx, "ext_fts", "ext_fts", "changed").Should().BeEmpty();

        ctx.CreateSqliteFts5CommandBuilder("ext_fts").Rebuild().Execute();

        NativeRowIds(ctx, "ext_fts", "ext_fts", "hello").Should().BeEmpty("rebuild refreshed the index from the content table");
        NativeRowIds(ctx, "ext_fts", "ext_fts", "changed").Should().Equal(1L);
    }

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_IntegrityCheck_OmittedAndFalse_ShouldSucceed_TrueOnStale_ShouldThrowNative()
    {
        var ctx = _sut.DataProvider;
        CreateExternalContentFts(ctx, "ic_fts", "ic_content");

        ctx.CreateSqliteFts5CommandBuilder("ic_fts").IntegrityCheck().Execute();
        ctx.CreateSqliteFts5CommandBuilder("ic_fts").IntegrityCheck(false).Execute();

        Execute(ctx, "update ic_content set title = 'changed text' where id = 1");

        var ours = Attempt(() => ctx.CreateSqliteFts5CommandBuilder("ic_fts").IntegrityCheck(true).Execute());
        var native = Attempt(() =>
        {
            Execute(ctx, "insert into ic_fts(ic_fts, rank) values('integrity-check', 1)");
            return 0;
        });

        ours.Success.Should().BeFalse("integrity-check 1 compares the index against the content table");
        ours.ErrorCode.Should().Be(native.ErrorCode, "the native result code must be propagated unchanged");
    }

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_RebuildOnContentlessTable_ShouldThrowNativeError()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists cl_fts");
        Execute(ctx, "create virtual table cl_fts using fts5(title, content='')");
        Execute(ctx, "insert into cl_fts(rowid, title) values (1, 'hello')");

        try
        {
            var act = () => ctx.CreateSqliteFts5CommandBuilder("cl_fts").Rebuild().Execute();

            act.Should().Throw<SqliteException>().WithMessage("*contentless*", "the native SQLite error propagates unchanged");
        }
        finally
        {
            Execute(ctx, "drop table if exists cl_fts");
        }
    }

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_MissingTable_ShouldThrowNativeError()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists no_such_fts");

        var ours = Attempt(() => ctx.CreateSqliteFts5CommandBuilder("no_such_fts").Optimize().Execute());
        var native = Attempt(() =>
        {
            Execute(ctx, "insert into no_such_fts(no_such_fts) values('optimize')");
            return 0;
        });

        ours.Success.Should().BeFalse();
        ours.ErrorCode.Should().Be(native.ErrorCode, "the native result code must be propagated unchanged");
        ours.ErrorCode.Should().Be(1, "SQLITE_ERROR (1) is SQLite's primary code for a missing table");
    }

    [Fact]
    [Trait("Issue", "195")]
    public void Fts5Maintenance_NonFtsTable_ShouldThrowNativeError()
    {
        var ctx = _sut.DataProvider;
        Execute(ctx, "drop table if exists plain_table");
        Execute(ctx, "create table plain_table (id integer primary key)");

        try
        {
            // The control command renders against a regular table, so SQLite rejects it as an insert
            // into a table lacking the control column; the native error must propagate, not be swallowed.
            var ours = Attempt(() => ctx.CreateSqliteFts5CommandBuilder("plain_table").Optimize().Execute());
            var native = Attempt(() =>
            {
                Execute(ctx, "insert into plain_table(plain_table) values('optimize')");
                return 0;
            });

            ours.Success.Should().BeFalse();
            ours.ErrorCode.Should().Be(native.ErrorCode, "the native result code must be propagated unchanged");
            ours.ErrorCode.Should().Be(1, "SQLITE_ERROR (1) is SQLite's primary code for a column that does not exist");
        }
        finally
        {
            Execute(ctx, "drop table if exists plain_table");
        }
    }

    [Fact]
    [Trait("Issue", "195")]
    public async Task Fts5Maintenance_PreCancelledToken_ShouldNotExecute()
    {
        var ctx = _sut.DataProvider;
        ResetFtsTables(ctx);
        var before = NativeRowIds(ctx, "fts5_docs", "fts5_docs", "hello");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => ctx.CreateSqliteFts5CommandBuilder("fts5_docs").Optimize().ExecuteAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        NativeRowIds(ctx, "fts5_docs", "fts5_docs", "hello").Should().Equal(before);
    }

    [Theory]
    [Trait("Issue", "195")]
    [InlineData(int.MinValue, "-2147483648")]
    [InlineData(int.MaxValue, "2147483647")]
    public void Fts5Maintenance_ExtremeMergeValue_ShouldMatchCanonicalDirectSql(int pages, string literal)
    {
        var ctx = _sut.DataProvider;
        CreateFtsProbe(ctx, "mnt_a");
        CreateFtsProbe(ctx, "mnt_b");

        try
        {
            var builder = ctx.CreateSqliteFts5CommandBuilder("mnt_a").Merge(pages);
            builder.ToSql().Should().Be(
                $"insert into \"mnt_a\" (\"mnt_a\", \"rank\") values ('merge', {literal})",
                "the rendered SQL must match the canonical FTS5 control form byte-for-byte");

            var ours = Attempt(() => builder.Execute());
            var native = Attempt(() =>
            {
                Execute(ctx, $"insert into mnt_b (mnt_b, rank) values('merge', {literal})");
                return 0;
            });

            ours.Success.Should().Be(native.Success, "the extreme merge value must have the same outcome as the canonical direct SQL");
            if (!ours.Success)
                ours.ErrorCode.Should().Be(native.ErrorCode);
        }
        finally
        {
            Execute(ctx, "drop table if exists mnt_a");
            Execute(ctx, "drop table if exists mnt_b");
        }
    }

    [Theory]
    [Trait("Issue", "195")]
    [InlineData(int.MaxValue, "2147483647")]
    public void Fts5Maintenance_ExtremeCrisisMergeValue_ShouldMatchCanonicalDirectSql(int value, string literal)
    {
        var ctx = _sut.DataProvider;
        CreateFtsProbe(ctx, "cm_a");
        CreateFtsProbe(ctx, "cm_b");

        try
        {
            var builder = ctx.CreateSqliteFts5CommandBuilder("cm_a").CrisisMerge(value);
            builder.ToSql().Should().Be(
                $"insert into \"cm_a\" (\"cm_a\", \"rank\") values ('crisismerge', {literal})",
                "the rendered SQL must match the canonical FTS5 control form byte-for-byte");

            var ours = Attempt(() => builder.Execute());
            var native = Attempt(() =>
            {
                Execute(ctx, $"insert into cm_b (cm_b, rank) values('crisismerge', {literal})");
                return 0;
            });

            ours.Success.Should().Be(native.Success, "the extreme crisismerge value must have the same outcome as the canonical direct SQL");
            if (!ours.Success)
                ours.ErrorCode.Should().Be(native.ErrorCode);
        }
        finally
        {
            Execute(ctx, "drop table if exists cm_a");
            Execute(ctx, "drop table if exists cm_b");
        }
    }

    private static void CreateExternalContentFts(IDataContext ctx, string ftsTable, string contentTable)
    {
        Execute(ctx, $"drop table if exists {ftsTable}");
        Execute(ctx, $"drop table if exists {contentTable}");
        Execute(ctx, $"create table {contentTable} (id integer primary key, title text, body text)");
        Execute(ctx, $"create virtual table {ftsTable} using fts5(title, body, content='{contentTable}', content_rowid='id')");
        Execute(ctx, $"insert into {contentTable}(id, title, body) values (1, 'hello world', 'the quick brown fox')");
        Execute(ctx, $"insert into {ftsTable}({ftsTable}) values('rebuild')");
    }

    private static void CreateFtsProbe(IDataContext ctx, string table)
    {
        Execute(ctx, $"drop table if exists {table}");
        Execute(ctx, $"create virtual table {table} using fts5(title, body)");
        Execute(ctx, $"insert into {table}(title, body) values ('hello world', 'the quick brown fox')");
    }

    private static (bool Success, int ErrorCode) Attempt(Func<int> action)
    {
        try
        {
            action();
            return (true, 0);
        }
        catch (SqliteException exception)
        {
            return (false, exception.SqliteErrorCode);
        }
    }

    private static void ConfigureFts5Rank(IDataContext ctx, string rankExpression)
        => Execute(ctx, $"insert into fts5_docs(fts5_docs, rank) values('rank', '{rankExpression}')");

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
