using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// A row type used only by the receiver/arity matrix: the process-wide metadata cache is first-writer-wins,
/// so the matrix keeps its own mapping instead of depending on the class/test order of <c>T320TvfRow</c>.
/// </summary>
public sealed class T320MatrixRow
{
    public int Id { get; set; }
}

internal static class T320MatrixTvf
{
    public static TvfEntityBuilder<T320MatrixRow> Echo(int id) => new("echo_rows", "dbo");

    /// <summary>
    /// Two-argument variant used by the #343 joined-apply cache cell: the first argument is the correlated
    /// column, the second a captured value, so a stale plan shows up as the wrong bound parameter.
    /// </summary>
    public static TvfEntityBuilder<T320MatrixRow> EchoPair(int id, int capture) => new("echo_rows_pair", "dbo");
}

/// <summary>
/// #320 (T320) MX-JOIN: the receiver/arity compile matrix of the lambda-source join family. Every call site
/// below is a real compiled call: the C# compiler binds each overload, and the fact then asserts the
/// resulting builder type and that the runtime table-valued-function source is carried on the new join.
/// <para>
/// The unchanged ambiguity control stays where it is: the untyped all-null pair
/// <c>TestTypeOnlyJoin_AllNullPair_BindsToPreExistingCteOverload</c> in
/// <c>tests/nextorm.core.tests/InMemory/InMemoryJoinTests.cs:579</c> keeps binding <c>Join(null, null)</c> to
/// the pre-existing <c>Cte&lt;T&gt;</c> instance overload — this file adds the negatives next to it but does
/// not edit it.
/// </para>
/// </summary>
[Trait("Category", "T320")]
public class T320RuntimeTvfOverloadMatrixTests
{
    private static readonly Type[] Receivers =
    [
        typeof(EntityBuilder<T320MatrixRow>),
        typeof(EntityBuilder),
        typeof(JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>),
        typeof(JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>),
        typeof(JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>),
        typeof(JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>),
        typeof(JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>),
        typeof(JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>),
    ];

    /// <summary>Asserts the last join of the chain is the expected type and carries the runtime function.</summary>
    private static void AssertLambdaSourceJoin<TProjection>(
        EntityBuilder<TProjection> joined,
        JoinType expected,
        string form)
    {
        joined.Joins.Should().NotBeNull($"the '{form}' form adds a join to the chain");
        var join = joined.Joins![^1];
        join.JoinType.Should().Be(expected, $"the '{form}' form binds the {expected} join");
        join.From.TableFunction.Should().NotBeNull($"the '{form}' form carries the runtime table-function source");
        join.From.TableFunction!.Name.Should().Be("echo_rows", $"the '{form}' form carries the factory's source name");
        join.From.TableFunction.Schema.Should().Be("dbo", $"the '{form}' form carries the factory's schema");
    }

    [Fact]
    public void CompileMatrix_EntityBuilderReceiver_ShouldBindEveryLambdaSourceForm()
    {
        using var ctx = new InMemoryDataContext();
        var receiver = ctx.From<T320MatrixRow>();

        var join = receiver.Join(() => T320MatrixTvf.Echo(1), (left, right) => left.Id == right.Id);
        join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(join, JoinType.Inner, "EntityBuilder<T> Join");

        var leftJoin = receiver.LeftJoin(() => T320MatrixTvf.Echo(2), (left, right) => left.Id == right.Id);
        leftJoin.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(leftJoin, JoinType.Left, "EntityBuilder<T> LeftJoin");

        var rightJoin = receiver.RightJoin(() => T320MatrixTvf.Echo(3), (left, right) => left.Id == right.Id);
        rightJoin.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(rightJoin, JoinType.Right, "EntityBuilder<T> RightJoin");

        var fullJoin = receiver.FullJoin(() => T320MatrixTvf.Echo(4), (left, right) => left.Id == right.Id);
        fullJoin.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(fullJoin, JoinType.Full, "EntityBuilder<T> FullJoin");

        var crossJoin = receiver.CrossJoin(() => T320MatrixTvf.Echo(5));
        crossJoin.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(crossJoin, JoinType.Cross, "EntityBuilder<T> CrossJoin");

        var crossApply = receiver.CrossApply(() => T320MatrixTvf.Echo(6));
        crossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(crossApply, JoinType.CrossApply, "EntityBuilder<T> CrossApply");
        crossApply.JoinCondition.ApplySource.Should().BeNull("a zero-argument APPLY source is not correlated");

        var outerApply = receiver.OuterApply(() => T320MatrixTvf.Echo(7));
        outerApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(outerApply, JoinType.OuterApply, "EntityBuilder<T> OuterApply");
        outerApply.JoinCondition.ApplySource.Should().BeNull("a zero-argument APPLY source is not correlated");
    }

    [Fact]
    public void CompileMatrix_NonGenericEntityBuilderReceiver_ShouldBindEveryLambdaSourceForm()
    {
        using var ctx = new InMemoryDataContext();
        var receiver = new EntityBuilder(ctx);

        var join = receiver.Join(() => T320MatrixTvf.Echo(1), (alias, right) => alias["id"].AsInt == right.Id);
        join.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(join, JoinType.Inner, "EntityBuilder Join");

        var leftJoin = receiver.LeftJoin(() => T320MatrixTvf.Echo(2), (alias, right) => alias["id"].AsInt == right.Id);
        leftJoin.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(leftJoin, JoinType.Left, "EntityBuilder LeftJoin");

        var rightJoin = receiver.RightJoin(() => T320MatrixTvf.Echo(3), (alias, right) => alias["id"].AsInt == right.Id);
        rightJoin.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(rightJoin, JoinType.Right, "EntityBuilder RightJoin");

        var fullJoin = receiver.FullJoin(() => T320MatrixTvf.Echo(4), (alias, right) => alias["id"].AsInt == right.Id);
        fullJoin.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(fullJoin, JoinType.Full, "EntityBuilder FullJoin");

        var crossJoin = receiver.CrossJoin(() => T320MatrixTvf.Echo(5));
        crossJoin.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(crossJoin, JoinType.Cross, "EntityBuilder CrossJoin");

        var crossApply = receiver.CrossApply(() => T320MatrixTvf.Echo(6));
        crossApply.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(crossApply, JoinType.CrossApply, "EntityBuilder CrossApply");

        var outerApply = receiver.OuterApply(() => T320MatrixTvf.Echo(7));
        outerApply.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320MatrixRow>>();
        AssertLambdaSourceJoin(outerApply, JoinType.OuterApply, "EntityBuilder OuterApply");
    }

    [Fact]
    public void CompileMatrix_JoinedReceiversArityTwoAndThree_ShouldBindEveryLambdaSourceForm()
    {
        using var ctx = new InMemoryDataContext();

        var arity2 = ctx.From<T320MatrixRow>()
            .Join(() => T320MatrixTvf.Echo(1), (first, second) => first.Id == second.Id);
        arity2.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow>>();

        var a2Join = arity2.Join(() => T320MatrixTvf.Echo(2), (projection, right) => projection.Item1.Id == right.Id);
        a2Join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2Join, JoinType.Inner, "arity-2 Join");

        var a2Left = arity2.LeftJoin(() => T320MatrixTvf.Echo(3), (projection, right) => projection.Item1.Id == right.Id);
        a2Left.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2Left, JoinType.Left, "arity-2 LeftJoin");

        var a2Right = arity2.RightJoin(() => T320MatrixTvf.Echo(4), (projection, right) => projection.Item1.Id == right.Id);
        a2Right.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2Right, JoinType.Right, "arity-2 RightJoin");

        var a2Full = arity2.FullJoin(() => T320MatrixTvf.Echo(5), (projection, right) => projection.Item1.Id == right.Id);
        a2Full.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2Full, JoinType.Full, "arity-2 FullJoin");

        var a2Cross = arity2.CrossJoin(() => T320MatrixTvf.Echo(6));
        a2Cross.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2Cross, JoinType.Cross, "arity-2 CrossJoin");

        var a2CrossApply = arity2.CrossApply(() => T320MatrixTvf.Echo(7));
        a2CrossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2CrossApply, JoinType.CrossApply, "arity-2 CrossApply");

        var a2OuterApply = arity2.OuterApply(() => T320MatrixTvf.Echo(8));
        a2OuterApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a2OuterApply, JoinType.OuterApply, "arity-2 OuterApply");

        var arity3 = arity2.Join(() => T320MatrixTvf.Echo(9), (projection, right) => projection.Item1.Id == right.Id);
        arity3.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow>>();

        var a3Join = arity3.Join(() => T320MatrixTvf.Echo(10), (projection, right) => projection.Item1.Id == right.Id);
        a3Join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3Join, JoinType.Inner, "arity-3 Join");

        var a3Left = arity3.LeftJoin(() => T320MatrixTvf.Echo(11), (projection, right) => projection.Item1.Id == right.Id);
        a3Left.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3Left, JoinType.Left, "arity-3 LeftJoin");

        var a3Right = arity3.RightJoin(() => T320MatrixTvf.Echo(12), (projection, right) => projection.Item1.Id == right.Id);
        a3Right.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3Right, JoinType.Right, "arity-3 RightJoin");

        var a3Full = arity3.FullJoin(() => T320MatrixTvf.Echo(13), (projection, right) => projection.Item1.Id == right.Id);
        a3Full.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3Full, JoinType.Full, "arity-3 FullJoin");

        var a3Cross = arity3.CrossJoin(() => T320MatrixTvf.Echo(14));
        a3Cross.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3Cross, JoinType.Cross, "arity-3 CrossJoin");

        var a3CrossApply = arity3.CrossApply(() => T320MatrixTvf.Echo(15));
        a3CrossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3CrossApply, JoinType.CrossApply, "arity-3 CrossApply");

        var a3OuterApply = arity3.OuterApply(() => T320MatrixTvf.Echo(16));
        a3OuterApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a3OuterApply, JoinType.OuterApply, "arity-3 OuterApply");
    }

    [Fact]
    public void CompileMatrix_JoinedReceiversArityFourToSeven_ShouldBindEveryLambdaSourceForm()
    {
        using var ctx = new InMemoryDataContext();

        var arity4 = ctx.From<T320MatrixRow>()
            .Join(() => T320MatrixTvf.Echo(1), (first, second) => first.Id == second.Id)
            .Join(() => T320MatrixTvf.Echo(2), (projection, third) => projection.Item1.Id == third.Id)
            .Join(() => T320MatrixTvf.Echo(3), (projection, fourth) => projection.Item1.Id == fourth.Id);
        arity4.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();

        var a4Join = arity4.Join(() => T320MatrixTvf.Echo(4), (projection, right) => projection.Item1.Id == right.Id);
        a4Join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4Join, JoinType.Inner, "arity-4 Join");

        var a4Left = arity4.LeftJoin(() => T320MatrixTvf.Echo(5), (projection, right) => projection.Item1.Id == right.Id);
        a4Left.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4Left, JoinType.Left, "arity-4 LeftJoin");

        var a4Right = arity4.RightJoin(() => T320MatrixTvf.Echo(6), (projection, right) => projection.Item1.Id == right.Id);
        a4Right.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4Right, JoinType.Right, "arity-4 RightJoin");

        var a4Full = arity4.FullJoin(() => T320MatrixTvf.Echo(7), (projection, right) => projection.Item1.Id == right.Id);
        a4Full.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4Full, JoinType.Full, "arity-4 FullJoin");

        var a4Cross = arity4.CrossJoin(() => T320MatrixTvf.Echo(8));
        a4Cross.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4Cross, JoinType.Cross, "arity-4 CrossJoin");

        var a4CrossApply = arity4.CrossApply(() => T320MatrixTvf.Echo(9));
        a4CrossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4CrossApply, JoinType.CrossApply, "arity-4 CrossApply");

        var a4OuterApply = arity4.OuterApply(() => T320MatrixTvf.Echo(10));
        a4OuterApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a4OuterApply, JoinType.OuterApply, "arity-4 OuterApply");

        var arity5 = a4Join;
        var a5Join = arity5.Join(() => T320MatrixTvf.Echo(11), (projection, right) => projection.Item1.Id == right.Id);
        a5Join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5Join, JoinType.Inner, "arity-5 Join");

        var a5Left = arity5.LeftJoin(() => T320MatrixTvf.Echo(12), (projection, right) => projection.Item1.Id == right.Id);
        a5Left.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5Left, JoinType.Left, "arity-5 LeftJoin");

        var a5Right = arity5.RightJoin(() => T320MatrixTvf.Echo(13), (projection, right) => projection.Item1.Id == right.Id);
        a5Right.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5Right, JoinType.Right, "arity-5 RightJoin");

        var a5Full = arity5.FullJoin(() => T320MatrixTvf.Echo(14), (projection, right) => projection.Item1.Id == right.Id);
        a5Full.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5Full, JoinType.Full, "arity-5 FullJoin");

        var a5Cross = arity5.CrossJoin(() => T320MatrixTvf.Echo(15));
        a5Cross.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5Cross, JoinType.Cross, "arity-5 CrossJoin");

        var a5CrossApply = arity5.CrossApply(() => T320MatrixTvf.Echo(16));
        a5CrossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5CrossApply, JoinType.CrossApply, "arity-5 CrossApply");

        var a5OuterApply = arity5.OuterApply(() => T320MatrixTvf.Echo(17));
        a5OuterApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a5OuterApply, JoinType.OuterApply, "arity-5 OuterApply");

        var arity6 = a5Join;
        var a6Join = arity6.Join(() => T320MatrixTvf.Echo(18), (projection, right) => projection.Item1.Id == right.Id);
        a6Join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6Join, JoinType.Inner, "arity-6 Join");

        var a6Left = arity6.LeftJoin(() => T320MatrixTvf.Echo(19), (projection, right) => projection.Item1.Id == right.Id);
        a6Left.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6Left, JoinType.Left, "arity-6 LeftJoin");

        var a6Right = arity6.RightJoin(() => T320MatrixTvf.Echo(20), (projection, right) => projection.Item1.Id == right.Id);
        a6Right.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6Right, JoinType.Right, "arity-6 RightJoin");

        var a6Full = arity6.FullJoin(() => T320MatrixTvf.Echo(21), (projection, right) => projection.Item1.Id == right.Id);
        a6Full.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6Full, JoinType.Full, "arity-6 FullJoin");

        var a6Cross = arity6.CrossJoin(() => T320MatrixTvf.Echo(22));
        a6Cross.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6Cross, JoinType.Cross, "arity-6 CrossJoin");

        var a6CrossApply = arity6.CrossApply(() => T320MatrixTvf.Echo(23));
        a6CrossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6CrossApply, JoinType.CrossApply, "arity-6 CrossApply");

        var a6OuterApply = arity6.OuterApply(() => T320MatrixTvf.Echo(24));
        a6OuterApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a6OuterApply, JoinType.OuterApply, "arity-6 OuterApply");

        var arity7 = a6Join;
        var a7Join = arity7.Join(() => T320MatrixTvf.Echo(25), (projection, right) => projection.Item1.Id == right.Id);
        a7Join.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7Join, JoinType.Inner, "arity-7 Join");

        var a7Left = arity7.LeftJoin(() => T320MatrixTvf.Echo(26), (projection, right) => projection.Item1.Id == right.Id);
        a7Left.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7Left, JoinType.Left, "arity-7 LeftJoin");

        var a7Right = arity7.RightJoin(() => T320MatrixTvf.Echo(27), (projection, right) => projection.Item1.Id == right.Id);
        a7Right.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7Right, JoinType.Right, "arity-7 RightJoin");

        var a7Full = arity7.FullJoin(() => T320MatrixTvf.Echo(28), (projection, right) => projection.Item1.Id == right.Id);
        a7Full.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7Full, JoinType.Full, "arity-7 FullJoin");

        var a7Cross = arity7.CrossJoin(() => T320MatrixTvf.Echo(29));
        a7Cross.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7Cross, JoinType.Cross, "arity-7 CrossJoin");

        var a7CrossApply = arity7.CrossApply(() => T320MatrixTvf.Echo(30));
        a7CrossApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7CrossApply, JoinType.CrossApply, "arity-7 CrossApply");

        var a7OuterApply = arity7.OuterApply(() => T320MatrixTvf.Echo(31));
        a7OuterApply.Should().BeOfType<JoinedEntityBuilder<T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow, T320MatrixRow>>();
        AssertLambdaSourceJoin(a7OuterApply, JoinType.OuterApply, "arity-7 OuterApply");
    }

    /// <summary>
    /// The extension family is one 11-overload set per receiver — <see cref="EntityBuilder{TEntity}"/>, the
    /// non-generic <see cref="EntityBuilder"/> and the joined receivers of arity 2…7 — and no lambda-source
    /// <c>Join</c>/<c>LeftJoin</c>/<c>RightJoin</c>/<c>FullJoin</c>/<c>CrossJoin</c> instance overload exists
    /// on those receivers (that is what keeps an untyped <c>Join(null, null)</c> binding to the CTE overload).
    /// </summary>
    [Fact]
    public void OverloadFamily_ShouldExposeElevenOverloadsPerReceiverAndNoInstanceLambdaSourceJoin()
    {
        var overloads = typeof(TvfJoinSourceExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

        overloads.Should().HaveCount(88, "8 receivers × 11 forms");

        var perReceiver = overloads
            .GroupBy(method => ReceiverKey(method.GetParameters()[0].ParameterType))
            .ToDictionary(group => group.Key, group => group.Count());

        perReceiver.Should().HaveCount(8, "the family covers exactly the eight receivers");
        perReceiver.Keys.Should().BeEquivalentTo(Receivers.Select(ReceiverKey), "every receiver of the matrix is covered");
        perReceiver.Values.Should().OnlyContain(count => count == 11, "each receiver gets the same eleven forms");

        // The scan itself is meaningful: the correlated instance APPLY overloads of the same shape exist,
        // so an empty result for the ordinary join family is a real absence, not a broken predicate.
        var instanceMethods = Receivers
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .ToList();

        instanceMethods
            .Where(method => method.Name is "CrossApply" or "OuterApply")
            .Any(method => IsLambdaSource(method.GetParameters()[0].ParameterType, delegateArity: 2))
            .Should()
            .BeTrue("the scanner detects a correlated instance lambda source, so the negative below is not vacuous");

        var instanceLambdaSourceJoins = instanceMethods
            .Where(method => method.Name is "Join" or "LeftJoin" or "RightJoin" or "FullJoin" or "CrossJoin")
            .Where(method => IsLambdaSource(method.GetParameters()[0].ParameterType, delegateArity: 1))
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToList();

        instanceLambdaSourceJoins.Should().BeEmpty(
            "a zero-argument lambda source is an extension-only form, so Join(null, null) keeps binding to the CTE overload");
    }

    /// <summary>
    /// True when the parameter is an expression lambda source returning an <see cref="EntityBuilder{T}"/>:
    /// <paramref name="delegateArity"/> 1 is the zero-argument source form, 2 the correlated one.
    /// </summary>
    private static bool IsLambdaSource(Type parameterType, int delegateArity)
    {
        if (!parameterType.IsGenericType || parameterType.GetGenericTypeDefinition() != typeof(Expression<>))
            return false;

        var delegateType = parameterType.GetGenericArguments()[0];
        if (!delegateType.IsGenericType || delegateType.GetGenericTypeDefinition() != typeof(Func<,>) && delegateType.GetGenericTypeDefinition() != typeof(Func<>))
            return false;

        var arguments = delegateType.GetGenericArguments();
        if (arguments.Length != delegateArity)
            return false;

        var returnType = arguments[^1];
        return returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(EntityBuilder<>);
    }

    private static string ReceiverKey(Type receiver)
        => receiver.IsGenericType
            ? $"{receiver.Name.Split('`')[0]}`{receiver.GetGenericArguments().Length}"
            : receiver.Name;

    // ---- ambiguity / negative cases -------------------------------------------------------------

    /// <summary>
    /// #324 supersedes the pre-#324 expectation that a correlated APPLY over a join projection is
    /// refused: the joined receiver is a supported flat transition whose addressable left row is "this
    /// command's FROM plus exactly the joins preceding this one", so the applied source becomes the next
    /// flat slot and the correlation must address the exact occurrence it names. The in-memory refusal —
    /// which under #324 is the first check for every correlated APPLY — is the only remaining negative.
    /// </summary>
    [Fact]
    public void JoinedReceiver_CorrelatedApplyOnAJoinProjection_ShouldBuildTheFlatTransitionAndBindSlotOne()
    {
        // Pre-#324 this cell asserted JoinProjectionApplyMessage ("cannot reference a join projection"):
        // a joined receiver was refused outright. #324 makes the flat transition supported, so the
        // in-memory gate — not the projection guard — is what a projection-shaped receiver now observes.
        using (var inMemory = new InMemoryDataContext())
        {
            var arity2 = inMemory.From<T320MatrixRow>().Join(() => T320MatrixTvf.Echo(1), (first, second) => first.Id == second.Id);

            Action act = () => arity2.CrossApply(projection => T320MatrixTvf.Echo(projection.Item1.Id));

            act.Should().Throw<NotSupportedException>().WithMessage("*in-memory provider*");
        }

        // The supported side: a SQL provider renders the applied source as flat slot 3 (never nested in
        // Item1) and qualifies the correlated argument with the FROM occurrence rather than the join.
        // A runtime-factory body is not used here: the flat transition does not lower one yet (the same
        // call on this receiver throws InvalidOperationException), which is tracked as a separate defect;
        // this cell asserts the flat-transition property #324 exists to protect.
        using var ctx = new SqlServerDataContext(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

        var joined = ctx.From<T320MatrixRow>()
            .Join(ctx.From<T320MatrixRow>(), (first, second) => first.Id == second.Id)
            .CrossApply(projection => ctx.From<T320MatrixRow>()
                .Where(row => row.Id == projection.Item1.Id)
                .Select(row => new { row.Id }))
            .Select(p => new { From = p.Item1.Id, Joined = p.Item2.Id, Applied = p.Item3.Id });

        var sql = T324ApplyFixture.SqlOf(ctx, joined);

        sql.Should().Contain(" cross apply (select", "a joined receiver is a supported flat APPLY after #324");
        sql.Should().Contain("where t3.Id = t1.Id", "slot 1 is this command's FROM source");
        sql.Should().NotContain("where t3.Id = t2.Id", "slot 2 is the joined occurrence and is not the addressed left row");
        sql.Should().Contain("t4.Id", "the applied source is the next flat slot, never nested inside Item1");

        // T343 / #343: the same arity-2 joined receiver, now applied to a runtime-factory TVF body. This is
        // the exact shape the flat transition refused before the fix: the TVF is the applied source, the
        // correlated argument addresses this command's FROM slot (t1) — never the joined occurrence (t2) —
        // and the applied row is the next flat slot (t4), never nested inside Item1.
        var factoryJoined = ctx.From<T320MatrixRow>()
            .Join(ctx.From<T320MatrixRow>(), (first, second) => first.Id == second.Id)
            .CrossApply(projection => T320MatrixTvf.Echo(projection.Item1.Id))
            .Select(p => new { From = p.Item1.Id, Joined = p.Item2.Id, Applied = p.Item3.Id });

        var factorySql = T324ApplyFixture.SqlOf(ctx, factoryJoined);

        factorySql.Should().Contain("cross apply ", "a runtime factory is applied through the flat transition");
        factorySql.Should().Contain("echo_rows(", "the factory call stays the applied source");
        factorySql.Should().MatchRegex(@"echo_rows\([^)]*t1\.Id",
            "the correlated argument addresses slot 1, this command's FROM source");
        factorySql.Should().NotMatchRegex(@"echo_rows\([^)]*t2\.Id",
            "slot 2 is the joined occurrence and is not the addressed left row");
        factorySql.Should().Contain("t4.Id", "the applied source is the next flat slot, never nested inside Item1");
    }

    // ---- #343 (T343): composed correlated TVF bodies are refused by name ---------------------------

    /// <summary>
    /// The frozen refusal a <b>joined</b> receiver reports for a composed correlated table-valued-function
    /// body: the composition, the receiver and both supported forms are named.
    /// </summary>
    private const string ComposedBodyRefusedOnJoinedReceiver =
        "A correlated CROSS/OUTER APPLY source on a joined receiver accepts either a bare runtime table-valued-function factory call (row => Db.MyTvf(row.Id)) or a derived query (row => ctx.From<T>().Where(row => ...).Select(row => new { ... })); the body 'T320MatrixTvf.Echo followed by Where' is a factory call followed by builder operations and cannot be lowered in an APPLY position.";

    /// <summary>
    /// The same refusal on the legacy <b>single-entity</b> receiver: C3 is not path-scoped, so the composed
    /// shape is named on both APPLY surfaces instead of leaking the internal conversion failure.
    /// </summary>
    private const string ComposedBodyRefusedOnSingleEntityReceiver =
        "A correlated CROSS/OUTER APPLY source on a single-entity receiver accepts either a bare runtime table-valued-function factory call (row => Db.MyTvf(row.Id)) or a derived query (row => ctx.From<T>().Where(row => ...).Select(row => new { ... })); the body 'T320MatrixTvf.Echo followed by Where' is a factory call followed by builder operations and cannot be lowered in an APPLY position.";

    /// <summary>
    /// T343 / #343 (C3): a composed correlated body — a factory call followed by builder operations — is
    /// refused while the builder composes, before the body is converted into a command and before any
    /// command exists. The refusal names the composition and the two supported forms instead of surfacing
    /// the internal <see cref="InvalidOperationException"/> from <see cref="Expression.Convert"/>.
    /// </summary>
    [Fact]
    public void JoinedReceiver_ComposedCorrelatedTvfBody_ShouldBeRefusedWhileComposing()
    {
        using var ctx = new SqlServerDataContext(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

        var arity2 = ctx.From<T320MatrixRow>().Join(ctx.From<T320MatrixRow>(), (first, second) => first.Id == second.Id);
        var joinsBefore = arity2.Joins!.Count;

        // Only the composition call is inside the delegate: the throw must happen there, so nothing after
        // it (preparation, SQL rendering, execution) can have happened.
        Action act = () => arity2.CrossApply(projection => T320MatrixTvf.Echo(projection.Item1.Id).Where(row => row.Id > 0));

        var message = act.Should().Throw<NotSupportedException>().Which.Message;
        message.Should().Be(ComposedBodyRefusedOnJoinedReceiver, "the refusal text is frozen by the contract");

        arity2.Joins!.Should().HaveCount(joinsBefore, "the refusal happens before the applied source becomes a join");
        arity2.Joins!.Should().OnlyContain(join => join.ApplySource == null, "no APPLY source was recorded on the receiver");
    }

    /// <summary>
    /// T343 / #343 (C3, not path-scoped): the same composed shape on the legacy single-entity receiver is
    /// refused the same way, so the named refusal does not depend on which APPLY surface is used.
    /// </summary>
    [Fact]
    public void SingleEntityReceiver_ComposedCorrelatedTvfBody_ShouldBeRefusedWhileComposing()
    {
        using var ctx = new SqlServerDataContext(T324ApplyFixture.SqlServerConnectionString, new DataContextBuilder());

        var receiver = ctx.From<T320MatrixRow>();

        Action act = () => receiver.CrossApply(row => T320MatrixTvf.Echo(row.Id).Where(entity => entity.Id > 0));

        var message = act.Should().Throw<NotSupportedException>().Which.Message;
        message.Should().Be(ComposedBodyRefusedOnSingleEntityReceiver, "the same typed refusal, naming the single-entity receiver");
    }

    /// <summary>A null lambda source stays an <see cref="ArgumentNullException"/> on the joined and named receivers.</summary>
    [Fact]
    public void LambdaSource_NullOnJoinedAndNonGenericReceivers_ShouldThrowArgumentNullException()
    {
        using var ctx = new InMemoryDataContext();
        var arity2 = ctx.From<T320MatrixRow>().Join(() => T320MatrixTvf.Echo(1), (first, second) => first.Id == second.Id);

        Action joined = () => arity2.Join(
            (Expression<Func<EntityBuilder<T320MatrixRow>>>)null!,
            (projection, right) => projection.Item1.Id == right.Id);
        joined.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("source");

        var named = new EntityBuilder(ctx);
        Action nonGeneric = () => named.Join(
            (Expression<Func<EntityBuilder<T320MatrixRow>>>)null!,
            (alias, right) => alias["id"].AsInt == right.Id);
        nonGeneric.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("source");
    }

    /// <summary>
    /// An unsupported zero-argument shape stays a named refusal on a joined receiver too: a builder body that
    /// is not a direct factory call is rejected instead of being treated as a source.
    /// </summary>
    [Fact]
    public void LambdaSource_NonFactoryBodyOnAJoinedReceiver_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();
        var arity2 = ctx.From<T320MatrixRow>().Join(() => T320MatrixTvf.Echo(1), (first, second) => first.Id == second.Id);

        Expression<Func<EntityBuilder<T320MatrixRow>>> nonFactory = () => new EntityBuilder<T320MatrixRow>(ctx);

        Action act = () => arity2.Join(nonFactory, (projection, right) => projection.Item1.Id == right.Id);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*must be a direct call to a runtime table-valued-function factory*");
    }
}
