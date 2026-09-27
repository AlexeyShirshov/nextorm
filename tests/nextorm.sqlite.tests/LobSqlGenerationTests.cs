using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// SQL-generation coverage for the SQLite LOB locator seam, exercised through the real preparation
/// route (<see cref="QueryPlanner.GetPreparedQueryCommand"/> with <c>sequentialAccess: true</c>), so
/// the assertions observe the SQL the driver would actually receive. A LOB projection rendered for
/// sequential access gets a trailing <c>rowid</c> locator while the payload stays at ordinal 0. A
/// buffered projection (and any dialect whose <see cref="ISqlDialect.LobLocatorColumn"/> is
/// <c>null</c>) does not get one. Preparation only builds the <c>DbCommand</c>; no connection is
/// opened. PostgreSQL/SQL Server declare a <c>null</c> locator, asserted in their own dialect tests.
/// </summary>
public class LobSqlGenerationTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    /// <summary>
    /// Renders a prepared command through the real planner with the given dialect and
    /// <c>sequentialAccess: true</c>, returning the emitted <c>DbCommand.CommandText</c>. The planner
    /// is constructed with the context's own context type and a provider-style command factory; the
    /// column mapper is never invoked on the sequential path, which prepares without a compiled mapper.
    /// </summary>
    private static string SequentialSql<TResult>(IDataContext ctx, ISqlDialect dialect, QueryCommand<TResult> command)
        => Normalize(SequentialCommand(ctx, dialect, command).DbCommand.CommandText);

    /// <summary>
    /// Prepares a command through the real planner with <c>sequentialAccess: true</c> and returns the
    /// prepared command so its <see cref="DbPreparedQueryCommand{TResult}.Behavior"/> can be asserted.
    /// </summary>
    private static DbPreparedQueryCommand<TResult> SequentialCommand<TResult>(IDataContext ctx, ISqlDialect dialect, QueryCommand<TResult> command)
        => (DbPreparedQueryCommand<TResult>)CreatePlanner(ctx, dialect).GetPreparedQueryCommand(command, false, false, true, CancellationToken.None);

    /// <summary>
    /// Prepares a command through the real context the way the <c>ToAsyncEnumerable</c> terminal does:
    /// with an enumerator and the explicit streaming-rows request. This is the only route that may
    /// promote a row projection containing a live <see cref="Stream"/>/<see cref="TextReader"/>; it
    /// also compiles the real provider mapper, unlike the sequential-SQL helper above.
    /// </summary>
    private static DbPreparedQueryCommand<TResult> StreamingRowsCommand<TResult>(IDataContext ctx, QueryCommand<TResult> command)
        => (DbPreparedQueryCommand<TResult>)((DataContext)ctx).GetStreamingRowsQueryCommand(command, CancellationToken.None);

    private static QueryPlanner CreatePlanner(IDataContext ctx, ISqlDialect dialect)
        => new(
            ctx,
            () => dialect,
            ctx.GetType(),
            new ProviderHooks(
                (column, param) => param,
                (name, value) => new SqliteParameter(name, value ?? DBNull.Value),
                sql => new SqliteCommand(sql)),
            new LoggingOptions(null),
            new InterceptorHooks([], []));

    [Fact]
    public void LobBinaryCommand_ShouldAppendTrailingRowidAfterPayload()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<ILobProbeEntity>().Select(x => x.Payload!);

        var sql = SequentialSql(ctx, SqliteDialect.Instance, command);

        // The payload keeps ordinal 0: the locator is only ever the trailing projected column.
        sql.Should().Be("select payload, rowid from lob_probe");
    }

    [Fact]
    public void LobTextCommand_ShouldAppendTrailingRowidAfterPayload()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<ILobProbeEntity>().Select(x => x.Body!);

        SequentialSql(ctx, SqliteDialect.Instance, command).Should().Be("select body, rowid from lob_probe");
    }

    [Fact]
    public void NormalCommand_ShouldNotAppendRowid()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<ILobProbeEntity>().Select(x => x.Payload!);

        // A buffered (non-sequential) preparation uses the public route: no locator is needed.
        var prepared = (DbPreparedQueryCommand<byte[]>)ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        var sql = Normalize(prepared.DbCommand.CommandText);

        sql.Should().Be("select payload from lob_probe");
        sql.Should().NotContain("rowid");
    }

    [Fact]
    public void NamedColumnGetStream_ShouldAppendTrailingRowidAfterPayload()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => t.GetStream("payload"));

        SequentialSql(ctx, SqliteDialect.Instance, command).Should().Be("select payload, rowid from lob_probe");
    }

    [Fact]
    public void NamedColumnGetTextReader_ShouldAppendTrailingRowidAfterBody()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => t.GetTextReader("body"));

        SequentialSql(ctx, SqliteDialect.Instance, command).Should().Be("select body, rowid from lob_probe");
    }

    [Fact]
    public void NamedColumnAsStream_ShouldRenderColumnWithoutLocator()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => t["payload"].AsStream);

        SequentialSql(ctx, NullLocatorDialect.Instance, command).Should().Be("select payload from lob_probe");
    }

    [Fact]
    public void NamedColumnStream_ShouldRequestSequentialAccess()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => t.GetStream("payload"));

        var prepared = SequentialCommand(ctx, SqliteDialect.Instance, command);

        prepared.Behavior.Should().HaveFlag(CommandBehavior.SequentialAccess);
    }

    // --- Phase 3: a streaming member inside an enumerated row. Only the ToAsyncEnumerable
    // (streaming-rows) preparation route may promote the shape to sequential access (mapper + locator
    // + Behavior); the buffered and other enumerator routes must reject it with the mapper guard. ---

    [Fact]
    public void NamedColumnRowStream_ShouldPrepareStreamingMapperWithLocator()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => new LobRow(t.GetInt32("id"), t.GetStream("payload")));

        var prepared = StreamingRowsCommand(ctx, command);

        Normalize(prepared.DbCommand.CommandText).Should().Be("select id, payload, rowid from lob_probe");
        prepared.Behavior.Should().HaveFlag(CommandBehavior.SequentialAccess);
        prepared.MapDelegate.Should().NotBeNull();
    }

    [Fact]
    public void NamedColumnRowTextReader_ShouldPrepareStreamingMapperWithLocator()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => new LobTextRow(t.GetInt32("id"), t.GetTextReader("body")));

        var prepared = StreamingRowsCommand(ctx, command);

        Normalize(prepared.DbCommand.CommandText).Should().Be("select id, body, rowid from lob_probe");
        prepared.Behavior.Should().HaveFlag(CommandBehavior.SequentialAccess);
        prepared.MapDelegate.Should().NotBeNull();
    }

    [Fact]
    public void NamedColumnRowStream_EnumeratorRouteWithoutStreamingRequest_ShouldThrowNotSupported()
    {
        // Narrowed scope (#101 P1): merely preparing an enumerator (ToEnumerable/Pipeline/
        // CreateEnumerator) must not promote the streaming row projection; only ToAsyncEnumerable does.
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => new LobRow(t.GetInt32("id"), t.GetStream("payload")));

        var act = () => ctx.GetPreparedQueryCommand(command, true, true, CancellationToken.None);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*streaming LOB column*");
    }

    [Fact]
    public void NamedColumnRowStream_BufferedPreparation_ShouldThrowNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => new LobRow(t.GetInt32("id"), t.GetStream("payload")));

        var act = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*streaming LOB column*ToAsyncEnumerable*");
    }

    [Fact]
    public void NamedColumnRowStream_AfterStreamingPreparation_BufferedPreparationStillThrows()
    {
        // Regression guard for the mapper cache: a streaming mapper cached for this shape
        // (Streaming: true) must never be handed back to a buffered preparation (Streaming: false).
        using var ctx = SqliteTestContext.Create();
        var streaming = ctx.From("lob_probe").Select(t => new LobRow(t.GetInt32("id"), t.GetStream("payload")));
        var prepared = StreamingRowsCommand(ctx, streaming);
        prepared.MapDelegate.Should().NotBeNull();

        var buffered = ctx.From("lob_probe").Select(t => new LobRow(t.GetInt32("id"), t.GetStream("payload")));
        var act = () => ctx.GetPreparedQueryCommand(buffered, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>();
    }

    // --- Validation (#101 P1): a streaming row projection must have one streaming column, and it must
    // be the last projected column. The check runs before the SQL is generated. ---

    [Fact]
    public void NamedColumnRowStream_TwoStreamingColumns_ShouldThrowNotSupportedBeforeSql()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => new TwoLobs(t.GetStream("payload"), t.GetStream("body")));

        var act = () => StreamingRowsCommand(ctx, command);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*at most one streaming LOB column*");
    }

    [Fact]
    public void NamedColumnRowStream_StreamingColumnNotLast_ShouldThrowNotSupportedBeforeSql()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From("lob_probe").Select(t => new StreamThenScalar(t.GetStream("payload"), t.GetInt32("id")));

        var act = () => StreamingRowsCommand(ctx, command);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*must be the last projected column*");
    }

    [Fact]
    public void NullLocatorDialect_ShouldNotAppendRowid()
    {
        using var ctx = SqliteTestContext.Create();
        var command = ctx.From<ILobProbeEntity>().Select(x => x.Payload!);

        // PostgreSQL/SQL Server declare a null locator; the generator must then leave the projection
        // untouched even on the sequential-access path.
        var sql = SequentialSql(ctx, NullLocatorDialect.Instance, command);

        sql.Should().Be("select payload from lob_probe");
        sql.Should().NotContain("rowid");
    }

    /// <summary>A minimal PG/SQL-Server-style dialect that opts into sequential access but has no locator.</summary>
    private sealed class NullLocatorDialect : SqlDialectBase
    {
        public static readonly NullLocatorDialect Instance = new();

        public override bool SupportsSequentialAccess => true;

        public override string? LobLocatorColumn => null;

        public override string MakeParam(string name) => $"${name}";

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    /// <summary>A named row shape projecting a live <see cref="Stream"/> next to a scalar column.</summary>
    internal sealed record LobRow(int Id, Stream Data);

    /// <summary>A named row shape projecting a live <see cref="TextReader"/> next to a scalar column.</summary>
    internal sealed record LobTextRow(int Id, TextReader Body);

    /// <summary>An invalid shape: two live streaming columns in one projection.</summary>
    internal sealed record TwoLobs(Stream A, Stream B);

    /// <summary>An invalid shape: a live streaming column that is not the last projected column.</summary>
    internal sealed record StreamThenScalar(Stream Data, int Id);

    [SqlTable("lob_probe")]
    internal interface ILobProbeEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }
        [Column("payload")]
        byte[]? Payload { get; set; }
        [Column("body")]
        string? Body { get; set; }
    }
}
