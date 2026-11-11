using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// D384 U5: negative paths for the source-less join overloads. Every case declares its concrete
/// expected outcome up front (AC-5, cycle N=2 r=2) and asserts it independently on both the
/// source-less form and the explicit <c>ctx.From(...)</c> form: Class A (<c>null</c>/<c>""</c> table
/// name) builds without throwing and throws <see cref="NullReferenceException"/> at SQL compilation
/// inside NextORM.Core; Class B (<c>"   "</c>) is accepted by both forms with identical SQL carrying
/// the raw whitespace identifier token; Class C (APPLY on a dialect without APPLY support) is refused
/// at SQL compilation with <see cref="NotSupportedException"/> naming the operator.
/// </summary>
public class SourceLessJoinNegativePathsTests
{
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

    private enum Stage { None, Construction, SqlCompilation }

    // Stage (i): expression/argument construction only (build() — no SQL rendering).
    private static Exception? TryConstruct(Action build)
    {
        try { build(); return null; }
        catch (Exception ex) { return ex; }
    }

    // Stage (ii): SQL compilation/rendering only, given an already-built command.
    private static Exception? TryCompile<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        try { _ = SqlOf(ctx, cmd); return null; }
        catch (Exception ex) { return ex; }
    }

    // Full pipeline, but stages are reported separately so a failure's stage is observable
    // instead of being merged into a single try.
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

        var (eStage, eEx, _) = TryPipeline(ctx, explicitBuild);
        var (sStage, sEx, _) = TryPipeline(ctx, sourcelessBuild);

        // Declared expected outcome, not read from the explicit call: NegUnresolvable declares
        // [DynamicColumns] on a non-dictionary property, so the metadata builder rejects the mapping
        // while From<NegUnresolvable>() registers it — expression construction, before any SQL is
        // compiled. The pipeline carries the join itself (nothing is discarded or substituted), so a
        // stage or cause change on either form is observed.
        const Stage expectedStage = Stage.Construction;
        const string expectedMarker = "dynamic-columns store property 'Broken'";

        eStage.Should().Be(expectedStage, "the explicit form rejects the mapping while registering the joined type");
        sStage.Should().Be(expectedStage, "the source-less form must fail at the declared stage, independently of the explicit call");
        eEx.Should().BeOfType<InvalidOperationException>().Which.Message
            .Should().Contain(expectedMarker, "the failure must be the documented dynamic-columns store rejection");
        sEx.Should().BeOfType<InvalidOperationException>().Which.Message
            .Should().Contain(expectedMarker, "the source-less form must fail for the same documented reason");
    }

    // ---------- (b) empty / whitespace / null table name in all seven string-form operators ----------
    //
    // AC-5 (cycle N=2 r=2): each case declares its concrete outcome in its own [InlineData] row; the
    // expectation is never read from what the explicit ctx.From(tableName) call happens to do at run
    // time. The three declared classes below are the whole contract for this table.
    public enum DeclaredOutcome
    {
        // null / "" table name: builder construction succeeds; SQL compilation throws
        // NullReferenceException inside the NextORM.Core resolution path (a null table name resolves to
        // a null From source), on the source-less and the explicit form independently.
        ClassA_NullReferenceAtCompilation,

        // "   " table name: both forms accept it and render the same non-empty SQL carrying the raw
        // whitespace identifier token in the declared join clause.
        ClassB_WhitespaceAccepted,

        // CrossApply / OuterApply on a dialect without APPLY support: both forms refuse at SQL
        // compilation with NotSupportedException naming the operator.
        ClassC_ApplyRefused
    }

    // Declared Class A shape: construction must not throw; SQL compilation must throw
    // NullReferenceException whose throw site is inside NextORM.Core, not in the test harness.
    private static void AssertNullReferenceAtCompilation<T>(IDataContext ctx, Func<QueryCommand<T>> build, string form)
    {
        QueryCommand<T>? cmd = null;
        var constructionError = TryConstruct(() => { cmd = build(); });
        constructionError.Should().BeNull($"{form} must build the query without throwing for a null/empty table name");

        var compilationError = TryCompile(ctx, cmd!);
        compilationError.Should().BeOfType<NullReferenceException>($"{form} must throw NullReferenceException at SQL compilation");

        var throwSite = compilationError!.TargetSite?.DeclaringType?.FullName;
        throwSite.Should().StartWith("NextORM.Core.",
            $"{form} must fail inside the NextORM.Core compilation/resolution path, not in the test harness");
        compilationError.StackTrace.Should().Contain("NextORM.Core.");
    }

    // Declared Class B shape: both forms must accept the whitespace-only table name; the SQL is
    // non-empty, byte-identical between the forms, and carries the declared join clause with the raw
    // whitespace identifier token.
    private static void AssertWhitespaceAccepted<T>(
        IDataContext ctx,
        Func<QueryCommand<T>> explicitBuild,
        Func<QueryCommand<T>> sourcelessBuild,
        string expectedJoinClause)
    {
        var (explicitStage, explicitError, explicitSql) = TryPipeline(ctx, explicitBuild);
        var (sourcelessStage, sourcelessError, sourcelessSql) = TryPipeline(ctx, sourcelessBuild);

        explicitStage.Should().Be(Stage.None, "the explicit ctx.From(tableName) form accepts a whitespace table name");
        sourcelessStage.Should().Be(Stage.None, "the source-less form accepts a whitespace table name");
        explicitError.Should().BeNull("the declared outcome is Accept: the explicit form compiles");
        sourcelessError.Should().BeNull("the declared outcome is Accept: the source-less form compiles");

        sourcelessSql.Should().NotBeNullOrEmpty("the declared Accept outcome must produce SQL");
        sourcelessSql.Should().Be(explicitSql, "both forms must render the same SQL for a whitespace table name");
        sourcelessSql.Should().Contain(expectedJoinClause,
            "the rendered join clause must carry the raw whitespace identifier token");
    }

    // Declared Class C shape: construction must not throw; SQL compilation must refuse the operator
    // with NotSupportedException carrying the dialect's capability marker for that operator.
    private static void AssertApplyRefused<T>(IDataContext ctx, Func<QueryCommand<T>> build, string form, string operatorMarker)
    {
        QueryCommand<T>? cmd = null;
        var constructionError = TryConstruct(() => { cmd = build(); });
        constructionError.Should().BeNull($"{form} must build the query before the dialect refuses it at SQL compilation");

        var compilationError = TryCompile(ctx, cmd!);
        compilationError.Should().BeOfType<NotSupportedException>($"{form} must refuse the APPLY join at SQL compilation");
        compilationError!.Message.Should().Contain(operatorMarker, $"{form} must name the refused operator");
        compilationError.Message.Should().Contain("not supported by this SQL dialect",
            $"{form} must carry the dialect capability marker");
    }

    // The declared join clause for a whitespace table name: dialect keyword + the raw whitespace token
    // + the alias assigned to the joined source (the left source "a" holds t1, the join holds t2).
    private static string WhitespaceJoinClause(string op, string table) => op switch
    {
        "Join" => $"join {table} as 't2'",
        "LeftJoin" => $"left join {table} as 't2'",
        "RightJoin" => $"right join {table} as 't2'",
        "FullJoin" => $"full join {table} as 't2'",
        "CrossJoin" => $"cross join {table} as 't2'",
        _ => throw new InvalidOperationException($"no declared whitespace-accept clause for {op}")
    };

    [Theory]
    [InlineData("Join", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("Join", "   ", DeclaredOutcome.ClassB_WhitespaceAccepted)]
    [InlineData("Join", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("LeftJoin", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("LeftJoin", "   ", DeclaredOutcome.ClassB_WhitespaceAccepted)]
    [InlineData("LeftJoin", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("RightJoin", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("RightJoin", "   ", DeclaredOutcome.ClassB_WhitespaceAccepted)]
    [InlineData("RightJoin", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("FullJoin", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("FullJoin", "   ", DeclaredOutcome.ClassB_WhitespaceAccepted)]
    [InlineData("FullJoin", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("CrossJoin", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("CrossJoin", "   ", DeclaredOutcome.ClassB_WhitespaceAccepted)]
    [InlineData("CrossJoin", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("CrossApply", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("CrossApply", "   ", DeclaredOutcome.ClassC_ApplyRefused)]
    [InlineData("CrossApply", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("OuterApply", "", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("OuterApply", "   ", DeclaredOutcome.ClassC_ApplyRefused)]
    [InlineData("OuterApply", null, DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    public void TableName_InvalidOrNull_DeclaresConcreteOutcome(string op, string? table, DeclaredOutcome expected)
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

        // The switch runs on the declared expectation carried by the case data, never on an observed
        // result of the explicit call.
        switch (expected)
        {
            case DeclaredOutcome.ClassA_NullReferenceAtCompilation:
                AssertNullReferenceAtCompilation(ctx, explicitBuild, "explicit ctx.From(tableName) form");
                AssertNullReferenceAtCompilation(ctx, sourcelessBuild, "source-less string-table form");
                break;

            case DeclaredOutcome.ClassB_WhitespaceAccepted:
                AssertWhitespaceAccepted(ctx, explicitBuild, sourcelessBuild, WhitespaceJoinClause(op, table!));
                break;

            case DeclaredOutcome.ClassC_ApplyRefused:
                AssertApplyRefused(ctx, explicitBuild, "explicit ctx.From(tableName) form", op);
                AssertApplyRefused(ctx, sourcelessBuild, "source-less string-table form", op);
                break;

            default:
                throw new InvalidOperationException($"undeclared outcome {expected} for {op} / '{table}'");
        }
    }

    // null table name via a string?-typed variable reaching the string overload: the declared outcome
    // is the same Class A as for an empty table name — construction succeeds and SQL compilation
    // throws NullReferenceException inside NextORM.Core.
    [Theory]
    [InlineData("Join", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("LeftJoin", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("RightJoin", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    [InlineData("FullJoin", DeclaredOutcome.ClassA_NullReferenceAtCompilation)]
    public void NullTableName_CastString_DeclaresConcreteOutcome(string op, DeclaredOutcome expected)
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

        switch (expected)
        {
            case DeclaredOutcome.ClassA_NullReferenceAtCompilation:
                AssertNullReferenceAtCompilation(ctx, explicitBuild, "explicit ctx.From(tableName) form");
                AssertNullReferenceAtCompilation(ctx, sourcelessBuild, "source-less string-table form");
                break;

            default:
                throw new InvalidOperationException($"undeclared outcome {expected} for {op}");
        }
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
    }

    // ---------- scope note: a bare null literal for table ----------
    //
    // This suite makes no claim about how a bare null literal in the table position resolves an
    // overload, and none about the compiler diagnostics of that call: no retained test here compiles
    // or pins that form. The documented null behaviour is the one asserted above — a string?-typed
    // null value selects the string-table overload and SQL compilation throws NullReferenceException
    // (TableName_InvalidOrNull_DeclaresConcreteOutcome, NullTableName_CastString_DeclaresConcreteOutcome).
    // (D384-CHECK-11 item 6, D384-CHECK-13, AC-7)
}

