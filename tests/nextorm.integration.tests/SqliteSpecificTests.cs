using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Tests for behaviour that is specific to the SQLite provider and therefore not part of the
/// shared suite (SQLite has no ANY/ALL subquery support and sorts NULLs first).
/// </summary>
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
    /// The multi-column <c>ToDataReader</c> terminal is fail-closed on SQLite: its streaming projection
    /// always appends a trailing <c>rowid</c> locator, so the terminal would expose a driver column the
    /// caller never asked for. It is rejected before the reader is opened, and the context stays usable.
    /// </summary>
    [Fact]
    public void LobDataReader_ToDataReader_ShouldThrowNotSupported()
    {
        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = () => command.ToDataReader();

        act.Should().Throw<NotSupportedException>()
            .WithMessage("ToDataReader is not supported on providers that append a LOB locator column (SQLite); use ToStream/ToTextReader for a single LOB column.");

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public async Task LobDataReader_ToDataReaderAsync_ShouldThrowNotSupported()
    {
        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = async () => await command.ToDataReaderAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>()
            .WithMessage("ToDataReader is not supported on providers that append a LOB locator column (SQLite); use ToStream/ToTextReader for a single LOB column.");

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

    private sealed class TeardownAction(Action action) : IDisposable
    {
        public void Dispose() => action();
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

    private static void Execute(IDataContext ctx, string sql)
    {
        ((DataContext)ctx).EnsureConnectionOpen();
        using var cmd = ((DataContext)ctx).CreateCommand(sql);
        cmd.ExecuteNonQuery();
    }
}
