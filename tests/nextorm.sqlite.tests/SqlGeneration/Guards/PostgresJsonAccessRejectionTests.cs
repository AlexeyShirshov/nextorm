using FluentAssertions;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// The PostgreSQL JSON access surface (<c>json_get</c>/<c>json_get_text</c>/<c>json_get_path</c>/
/// <c>json_get_path_text</c>) is gated on <c>ISqlDialect.SupportsPostgresJsonSql</c>, which SQLite
/// does not advertise: the SQLite JSON1 operators have different semantics and must not be enabled
/// by the PostgreSQL typing fix. All six overloads must therefore fail closed while the query is
/// being planned, before any SQL reaches the provider. No database connection is opened.
/// </summary>
public class PostgresJsonAccessRejectionTests
{
    private const string GuardMessage = "*JSON*not supported*this provider*PostgreSQL*";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void T263_JsonGet_StringKey_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get("{\"a\":1}", "a") }));

        act.Should().Throw<NotSupportedException>().WithMessage(GuardMessage);
    }

    [Fact]
    public void T263_JsonGet_IntegerIndex_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get("{\"a\":1}", 0) }));

        act.Should().Throw<NotSupportedException>().WithMessage(GuardMessage);
    }

    [Fact]
    public void T263_JsonGetText_StringKey_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_text("{\"a\":1}", "a") }));

        act.Should().Throw<NotSupportedException>().WithMessage(GuardMessage);
    }

    [Fact]
    public void T263_JsonGetText_IntegerIndex_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_text("{\"a\":1}", 0) }));

        act.Should().Throw<NotSupportedException>().WithMessage(GuardMessage);
    }

    [Fact]
    public void T263_JsonGetPath_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_path("{\"a\":1}", new[] { "a", "b" }) }));

        act.Should().Throw<NotSupportedException>().WithMessage(GuardMessage);
    }

    [Fact]
    public void T263_JsonGetPathText_ShouldThrowBecauseNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, e.Where(x => x.Id > 0)
            .Select(x => new { V = SqlFunctions.Postgres.json_get_path_text("{\"a\":1}", new[] { "a", "b" }) }));

        act.Should().Throw<NotSupportedException>().WithMessage(GuardMessage);
    }
}
