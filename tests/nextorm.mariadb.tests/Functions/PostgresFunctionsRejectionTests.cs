using FluentAssertions;
using NextORM.Core;
using NextORM.MariaDb;

namespace NextORM.MariaDb.Tests;

/// <summary>
/// The PostgreSQL numeric-scale functions of #281 (<see cref="SqlFunctions.Postgres"/>) have no
/// MariaDB equivalent, so the MariaDB dialect must reject each name instead of rendering PostgreSQL
/// SQL. MariaDB inherits the MySQL dialect; this is the MariaDB dialect path, never a MySQL run.
/// </summary>
public class PostgresFunctionsRejectionTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldThrow()
    {
        using var ctx = MariaDbTestContext.Create();
        var e = ctx.From<IComplexEntity>();

        var scale = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.scale(12.3400m) }));
        var minScale = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.min_scale(12.3400m) }));
        var trimScale = () => SqlOf(ctx, e.Select(x => new { V = SqlFunctions.Postgres.trim_scale(12.3400m) }));

        scale.Should().Throw<NotSupportedException>().WithMessage("*scale*not supported*");
        minScale.Should().Throw<NotSupportedException>().WithMessage("*min_scale*not supported*");
        trimScale.Should().Throw<NotSupportedException>().WithMessage("*trim_scale*not supported*");
    }

    [Fact]
    [Trait("Issue", "281")]
    public void NumericScaleFunctions_ShouldNotBeAdvertised()
    {
        var scalars = MariaDbDialect.Instance.ScalarFunctions;

        scalars.Supports("scale").Should().BeFalse();
        scalars.Supports("min_scale").Should().BeFalse();
        scalars.Supports("trim_scale").Should().BeFalse();
    }
}
