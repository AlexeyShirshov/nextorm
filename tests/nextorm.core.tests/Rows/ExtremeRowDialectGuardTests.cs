using System.Data.Common;
using System.Text;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Gate for <c>SelectWhereMax</c>/<c>SelectWhereMin</c> on a dialect that has not opted in: preparing
/// the command must fail with a clear <see cref="NotSupportedException"/> before any SQL is emitted.
/// </summary>
public class ExtremeRowDialectGuardTests
{
    private sealed class NoExtremeDialect : SqlDialectBase
    {
        internal static readonly NoExtremeDialect Instance = new();

        public override string MakeParam(string name) => "@" + name;

        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }

    private sealed class TestContext : DataContext
    {
        public TestContext() : base(new DataContextBuilder())
        {
        }

        public override ISqlDialect Dialect => NoExtremeDialect.Instance;

        public override DbParameter CreateParam(string name, object? value) => throw new NotSupportedException();

        protected override DbConnection CreateDbConnection(string? connectionString) => throw new NotSupportedException();
    }

    [Fact]
    public void SelectWhereMax_OnDialectWithoutSupport_ShouldThrowBeforeSql()
    {
        using var ctx = new TestContext();
        var command = ctx.From<ExtremeRowEntity>().SelectWhereMax(it => it.Score, it => new { it.Id });

        var act = () => ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);

        act.Should().Throw<NotSupportedException>().WithMessage("*does not support SelectWhereMax/SelectWhereMin*");
    }

    /// <summary>
    /// The capability is a default interface method (<c>false</c>) so an external
    /// <see cref="ISqlDialect"/> implementation written before the member existed keeps compiling with
    /// the safe default, without having to implement it. <see cref="SqlDialectBase"/> and the built-in
    /// providers still override it to opt in.
    /// </summary>
    [Fact]
    public void SupportsSelectWhereMinMax_ShouldBeADefaultInterfaceMethod()
    {
        var getter = typeof(ISqlDialect)
            .GetProperty(nameof(ISqlDialect.SupportsSelectWhereMinMax))!
            .GetGetMethod()!;

        getter.IsAbstract.Should().BeFalse(
            "a default interface method is required so external dialects that predate the member keep compiling");
    }
}
