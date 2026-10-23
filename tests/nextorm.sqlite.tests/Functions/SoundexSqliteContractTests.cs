using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// #278: the shipped SQLite engine is built without <c>SQLITE_SOUNDEX</c>, so <c>soundex</c> does not
/// exist there and the façade must fail closed while the query is prepared (never at execution, never
/// as a silent <c>NULL</c>). <see cref="SqliteFunctions.soundex(string)"/> stays for source/binary
/// compatibility; the refusal is unconditional, including on a custom engine that does have the
/// compile-time option. The target object is the dialect hook
/// (<see cref="ISqlDialect.SqliteFunctions"/>), which is the very <c>SqliteFunctionRenderer</c>
/// instance the provider installs.
/// </summary>
public class SoundexSqliteContractTests
{
    private const string RefusalMessage = "The SQLite function 'soundex' is not supported by this provider.";

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "278")]
    public void Soundex_PreparationShouldReject()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        string? typedNull = null;

        // (a) a mapped column, (b) a constant literal and (c) an explicitly typed string? null each
        // refuse while the command is prepared: SqlOf can only return SQL if preparation succeeded, so
        // a successful rendering fails this test instead of being observed as emitted text.
        var column = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.soundex(x.String) }));
        var literal = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.soundex("Robert") }));
        var nullArgument = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Sqlite.soundex(typedNull) }));

        column.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
        literal.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
        nullArgument.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
    }

    [Fact]
    [Trait("Issue", "278")]
    public void Soundex_SecondPreparationShouldReject()
    {
        using var ctx = SqliteTestContext.Create();
        var e = ctx.From<IComplexEntity>();
        var command = e.Select(x => new { V = SqlFunctions.Sqlite.soundex(x.String) });

        // The rejection must not be a first-call-only effect and must never leave a cached successful
        // preparation behind: both attempts on the same query instance refuse independently.
        var first = () => SqlOf(ctx, command);
        var second = () => SqlOf(ctx, command);

        first.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
        second.Should().Throw<NotSupportedException>().WithMessage(RefusalMessage);
    }

    [Fact]
    [Trait("Issue", "278")]
    public void Soundex_ShouldNotBeAdvertised()
    {
        var functions = SqliteDialect.Instance.SqliteFunctions;
        functions.Should().NotBeNull();

        // The shipped engine has no such function, so the dialect hook must not advertise it — while a
        // neighbouring native scalar and the #285 total aggregate stay supported.
        functions!.Supports("soundex").Should().BeFalse("the shipped SQLite build has no soundex function");
        functions.Supports("printf").Should().BeTrue();
        functions.Supports("unicode").Should().BeTrue();
        functions.Supports("total").Should().BeTrue("the #285 total aggregate keeps its native form");
    }

    [Fact]
    [Trait("Issue", "278")]
    public void Soundex_RendererShouldReject()
    {
        var functions = SqliteDialect.Instance.SqliteFunctions;
        functions.Should().NotBeNull();

        // A direct rendering bypass (the translator gate skipped) hits the renderer's own unsupported
        // arm: the function is never emitted, not even as text.
        var act = () => functions!.Render("soundex", ["somestring"]);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("The soundex function is not supported by SQLite.");
    }
}
