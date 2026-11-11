using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D386: negative paths for the source-less join overloads. Every invalid table name is rejected at
/// the call site (before any builder state is created) with <see cref="ArgumentNullException"/>
/// (<c>null</c>) or <see cref="ArgumentException"/> (<c>""</c> / whitespace-only), <c>ParamName == "table"</c>,
/// identically on the source-less form and the explicit <c>ctx.From(...)</c> form.
/// </summary>
public class SourceLessJoinNegativePathsTests
{
    private enum Stage { None, Construction, SqlCompilation }
    [SqlTable("neg_parent")]
    public sealed class NegParent
    {
        public int Id { get; set; }
    }

    public sealed class NegUnresolvable
    {
        public int Id { get; set; }

        [DynamicColumns]
        public int Broken { get; set; }
    }

    private static IDataContext Create() => SqliteTestContext.Create();

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    // ---------- (a) unregistered TJoinEntity in CrossJoin/CrossApply/OuterApply ----------

    [Theory]
    [InlineData("CrossJoin")]
    [InlineData("CrossApply")]
    [InlineData("OuterApply")]
    public void UnregisteredType_TypeOnly_FailsAtSameStageAsExplicit(string op)
    {
        using var ctx = Create();

        Func<QueryCommand<int>> explicitBuild = op switch
        {
            "CrossJoin" => () => ctx.From("a").CrossJoin(ctx.From<NegUnresolvable>()).Select(p => 1),
            "CrossApply" => () => ctx.From("a").CrossApply(ctx.From<NegUnresolvable>()).Select(p => 1),
            _ => () => ctx.From("a").OuterApply(ctx.From<NegUnresolvable>()).Select(p => 1)
        };
        Func<QueryCommand<int>> sourcelessBuild = op switch
        {
            "CrossJoin" => () => ctx.From("a").CrossJoin<NegUnresolvable>().Select(p => 1),
            "CrossApply" => () => ctx.From("a").CrossApply<NegUnresolvable>().Select(p => 1),
            _ => () => ctx.From("a").OuterApply<NegUnresolvable>().Select(p => 1)
        };

        Func<QueryCommand<int>>[] builds = { explicitBuild, sourcelessBuild };
        foreach (var build in builds)
        {
            Action act = () => build();
            act.Should().Throw<InvalidOperationException>()
                .Which.Message.Should().Contain("dynamic-columns store property 'Broken'");
        }
    }

    // ---------- (b) empty / whitespace / null table name in all seven string-form operators ----------

    private static void AssertTableGuard<T>(IDataContext ctx, Func<QueryCommand<T>> build, string form, Type expectedExceptionType)
    {
        Action act = () => build();
        if (expectedExceptionType == typeof(ArgumentNullException))
        {
            act.Should().ThrowExactly<ArgumentNullException>($"{form} must throw ArgumentNullException at the call site")
                .Which.ParamName.Should().Be("table");
        }
        else
        {
            act.Should().ThrowExactly<ArgumentException>($"{form} must throw ArgumentException at the call site")
                .Which.ParamName.Should().Be("table");
        }
        _ = ctx;
    }

    [Theory]
    [InlineData("Join", "")]
    [InlineData("Join", "   ")]
    [InlineData("Join", null)]
    [InlineData("LeftJoin", "")]
    [InlineData("LeftJoin", "   ")]
    [InlineData("LeftJoin", null)]
    [InlineData("RightJoin", "")]
    [InlineData("RightJoin", "   ")]
    [InlineData("RightJoin", null)]
    [InlineData("FullJoin", "")]
    [InlineData("FullJoin", "   ")]
    [InlineData("FullJoin", null)]
    [InlineData("CrossJoin", "")]
    [InlineData("CrossJoin", "   ")]
    [InlineData("CrossJoin", null)]
    [InlineData("CrossApply", "")]
    [InlineData("CrossApply", "   ")]
    [InlineData("CrossApply", null)]
    [InlineData("OuterApply", "")]
    [InlineData("OuterApply", "   ")]
    [InlineData("OuterApply", null)]
    public void TableName_InvalidOrNull_ThrowsAtCallSite(string op, string? table)
    {
        using var ctx = Create();

        Func<QueryCommand<int>> explicitBuild = op switch
        {
            "Join" => () => ctx.From("a").Join(ctx.From(table!), (s, c) => true).Select(p => 1),
            "LeftJoin" => () => ctx.From("a").LeftJoin(ctx.From(table!), (s, c) => true).Select(p => 1),
            "RightJoin" => () => ctx.From("a").RightJoin(ctx.From(table!), (s, c) => true).Select(p => 1),
            "FullJoin" => () => ctx.From("a").FullJoin(ctx.From(table!), (s, c) => true).Select(p => 1),
            "CrossJoin" => () => ctx.From("a").CrossJoin(ctx.From(table!)).Select(p => 1),
            "CrossApply" => () => ctx.From("a").CrossApply(ctx.From(table!)).Select(p => 1),
            "OuterApply" => () => ctx.From("a").OuterApply(ctx.From(table!)).Select(p => 1),
            _ => throw new InvalidOperationException(op)
        };

        Func<QueryCommand<int>> sourcelessBuild = op switch
        {
            "Join" => () => ctx.From("a").Join(table!, (s, c) => true).Select(p => 1),
            "LeftJoin" => () => ctx.From("a").LeftJoin(table!, (s, c) => true).Select(p => 1),
            "RightJoin" => () => ctx.From("a").RightJoin(table!, (s, c) => true).Select(p => 1),
            "FullJoin" => () => ctx.From("a").FullJoin(table!, (s, c) => true).Select(p => 1),
            "CrossJoin" => () => ctx.From("a").CrossJoin(table!).Select(p => 1),
            "CrossApply" => () => ctx.From("a").CrossApply(table!).Select(p => 1),
            "OuterApply" => () => ctx.From("a").OuterApply(table!).Select(p => 1),
            _ => throw new InvalidOperationException(op)
        };

        var expectedType = table is null ? typeof(ArgumentNullException) : typeof(ArgumentException);
        AssertTableGuard(ctx, explicitBuild, "explicit ctx.From(tableName) form", expectedType);
        AssertTableGuard(ctx, sourcelessBuild, "source-less string-table form", expectedType);
    }

    // null table name via a string?-typed variable reaching the string overload
    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    [InlineData("RightJoin")]
    [InlineData("FullJoin")]
    public void NullTableName_CastString_ThrowsAtCallSite(string op)
    {
        using var ctx = Create();
        string? table = null;

        Func<QueryCommand<int>> explicitBuild = op switch
        {
            "Join" => () => ctx.From("a").Join(ctx.From(table!), (s, c) => true).Select(p => 1),
            "LeftJoin" => () => ctx.From("a").LeftJoin(ctx.From(table!), (s, c) => true).Select(p => 1),
            "RightJoin" => () => ctx.From("a").RightJoin(ctx.From(table!), (s, c) => true).Select(p => 1),
            "FullJoin" => () => ctx.From("a").FullJoin(ctx.From(table!), (s, c) => true).Select(p => 1),
            _ => throw new InvalidOperationException(op)
        };
        Func<QueryCommand<int>> sourcelessBuild = op switch
        {
            "Join" => () => ctx.From("a").Join(table!, (s, c) => true).Select(p => 1),
            "LeftJoin" => () => ctx.From("a").LeftJoin(table!, (s, c) => true).Select(p => 1),
            "RightJoin" => () => ctx.From("a").RightJoin(table!, (s, c) => true).Select(p => 1),
            "FullJoin" => () => ctx.From("a").FullJoin(table!, (s, c) => true).Select(p => 1),
            _ => throw new InvalidOperationException(op)
        };

        AssertTableGuard(ctx, explicitBuild, "explicit ctx.From(tableName) form", typeof(ArgumentNullException));
        AssertTableGuard(ctx, sourcelessBuild, "source-less string-table form", typeof(ArgumentNullException));
    }

    // ---------- (c) null join predicate in string-table Join/LeftJoin/RightJoin/FullJoin ----------

    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    [InlineData("RightJoin")]
    [InlineData("FullJoin")]
    public void NullPredicate_MatchesExplicitStage(string op)
    {
        using var ctx = Create();

        Func<QueryCommand<int>> explicitBuild = op switch
        {
            "Join" => () => ctx.From("a").Join(ctx.From("b"), default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            "LeftJoin" => () => ctx.From("a").LeftJoin(ctx.From("b"), default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            "RightJoin" => () => ctx.From("a").RightJoin(ctx.From("b"), default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            "FullJoin" => () => ctx.From("a").FullJoin(ctx.From("b"), default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            _ => throw new InvalidOperationException(op)
        };
        Func<QueryCommand<int>> sourcelessBuild = op switch
        {
            "Join" => () => ctx.From("a").Join("b", default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            "LeftJoin" => () => ctx.From("a").LeftJoin("b", default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            "RightJoin" => () => ctx.From("a").RightJoin("b", default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            "FullJoin" => () => ctx.From("a").FullJoin("b", default(Expression<Func<TableAlias, TableAlias, bool>>)!).Select(p => 1),
            _ => throw new InvalidOperationException(op)
        };

        // Both forms accept a null predicate and render no ON clause.
        var (eStage, eEx, eSql) = TryPipeline(ctx, explicitBuild);
        var (sStage, sEx, sSql) = TryPipeline(ctx, sourcelessBuild);

        eEx.Should().BeNull("the explicit form accepts a null predicate");
        sEx.Should().BeNull("the source-less form accepts a null predicate exactly like the explicit form");
        sSql.Should().Be(eSql, "source-less SQL must be byte-identical to the explicit-source oracle");
        eSql.Should().NotContain(" ON ", "a null predicate renders no ON clause");
        sSql.Should().NotContain(" ON ", "a null predicate renders no ON clause");
        _ = eStage; _ = sStage;
    }

    private static (Stage Stage, Exception? Error, string? Sql) TryPipeline<T>(IDataContext ctx, Func<QueryCommand<T>> build)
    {
        QueryCommand<T> cmd;
        try
        {
            cmd = build();
        }
        catch (Exception ex)
        {
            return (Stage.Construction, ex, null);
        }
        try
        {
            var sql = SqlOf(ctx, cmd);
            return (Stage.None, null, sql);
        }
        catch (Exception ex)
        {
            return (Stage.SqlCompilation, ex, null);
        }
    }
}
