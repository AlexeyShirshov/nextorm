using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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
    {
        var planner = new QueryPlanner(
            ctx,
            () => dialect,
            ctx.GetType(),
            new ProviderHooks(
                (column, param) => param,
                (name, value) => new SqliteParameter(name, value ?? DBNull.Value),
                sql => new SqliteCommand(sql)),
            new LoggingOptions(null),
            new InterceptorHooks([], []));

        var prepared = (DbPreparedQueryCommand<TResult>)planner.GetPreparedQueryCommand(command, false, false, true, CancellationToken.None);
        return Normalize(prepared.DbCommand.CommandText);
    }

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
