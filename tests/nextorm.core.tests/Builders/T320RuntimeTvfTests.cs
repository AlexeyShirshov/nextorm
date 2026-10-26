using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

public sealed class T320TvfRow
{
    public int Id { get; set; }
}

internal static class T320TvfDb
{
    public static TvfEntityBuilder<T320TvfRow> Echo(int id) => new("echo_rows", "dbo");

    public static TvfEntityBuilder<T320TvfRow> EchoText(string? text) => new("echo_text", "dbo");

    public static TvfEntityBuilder<T320TvfRow> EchoWithOptions(int id) => new(
        "echo_rows",
        "dbo",
        "row_estimator=1",
        null,
        new[] { 0 },
        TableFunctionSchema.None);
}

/// <summary>
/// #320 (T320) part 1: the runtime TVF descriptor and the lambda-source <c>From</c> lowering. Trait
/// <c>Category=T320</c> is the unit's inner-loop selector.
/// </summary>
[Trait("Category", "T320")]
public class T320RuntimeTvfTests
{
    // ---- AC01: descriptor metadata is constructor-only, immutable and context-free -----------------

    [Fact]
    public void Descriptor_ShouldExposeRuntimeMetadata()
    {
        var descriptor = new TvfEntityBuilder<T320TvfRow>(
            "echo_rows",
            "dbo",
            "row_estimator=1",
            null,
            new[] { 0 },
            TableFunctionSchema.None);

        descriptor.Name.Should().Be("echo_rows");
        descriptor.Schema.Should().Be("dbo");
        descriptor.WithClause.Should().Be("row_estimator=1");
        descriptor.CallClause.Should().BeNull();
        descriptor.VerbatimArguments.Should().Equal(0);
        descriptor.ResultSchema.Should().Be(TableFunctionSchema.None);
    }

    [Fact]
    public void Descriptor_ShouldDefensivelyCopyVerbatimArguments()
    {
        var verbatim = new List<int> { 0 };

        var descriptor = new TvfEntityBuilder<T320TvfRow>("echo_rows", null, null, null, verbatim, TableFunctionSchema.None);
        verbatim.Add(1);
        verbatim[0] = 7;

        descriptor.VerbatimArguments.Should().Equal(0);
    }

    [Fact]
    public void Descriptor_ShouldNotExposePublicSetters()
    {
        var properties = typeof(TvfEntityBuilder<T320TvfRow>)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        properties.Should().NotBeEmpty();
        properties.Should().OnlyContain(p => p.CanWrite == false);
    }

    [Fact]
    public void Descriptor_ShouldNotStoreAContext()
    {
        var descriptor = T320TvfDb.Echo(1);

        foreach (var field in ContextFields(typeof(TvfEntityBuilder<T320TvfRow>)))
            field.GetValue(descriptor).Should().BeNull($"the descriptor field {field.Name} must not carry a data context");
    }

    private static IEnumerable<FieldInfo> ContextFields(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var field in current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (typeof(IDataContext).IsAssignableFrom(field.FieldType))
                    yield return field;
            }
        }
    }

    [Fact]
    public void Descriptor_ShouldNotDependOnArgumentValues()
    {
        var id = 1;
        var first = T320TvfDb.Echo(id);

        id = 42;
        var second = T320TvfDb.Echo(id);

        second.Name.Should().Be(first.Name);
        second.Schema.Should().Be(first.Schema);
        second.VerbatimArguments.Should().Equal(first.VerbatimArguments);
        second.ResultSchema.Should().Be(first.ResultSchema);
    }

    [Fact]
    public void Descriptor_CompositionShouldNotNeedAContext()
    {
        var descriptor = T320TvfDb.Echo(1);

        var composed = descriptor.Where(r => r.Id > 0).OrderBy(r => r.Id);

        composed.Should().BeOfType<TvfEntityBuilder<T320TvfRow>>();
        var act = () => composed.ToCommand();
        act.Should().Throw<InvalidOperationException>().Which.Message.Should().Contain("unbound");
    }

    // ---- AC05: an unbound chain fails explicitly instead of executing or null-referencing ---------

    [Fact]
    public void UnboundDescriptor_ToCommandShouldFailExplicitly()
    {
        var descriptor = T320TvfDb.Echo(1);

        var act = () => descriptor.ToCommand();

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain("unbound");
    }

    // ---- AC02/AC03: the lambda source is the only argument carrier ---------------------------------

    [Fact]
    public void From_LambdaSource_ShouldLowerTheFactoryCallToATableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From(() => T320TvfDb.Echo(42));

        builder.SourceFrom.Should().NotBeNull();
        var function = builder.SourceFrom!.TableFunction;
        function.Should().NotBeNull();
        function!.Name.Should().Be("echo_rows");
        function.Schema.Should().Be("dbo");
        function.ResultSchema.Should().Be(TableFunctionSchema.None);
        function.ResultType.Should().Be(typeof(T320TvfRow));
        function.Call.Method.Name.Should().Be(nameof(T320TvfDb.Echo));
    }

    [Fact]
    public void From_LambdaSource_ShouldPreserveCapturedArgumentExpressions()
    {
        using var ctx = new InMemoryDataContext();
        var id = 7;

        var builder = ctx.From(() => T320TvfDb.Echo(id));

        var argument = builder.SourceFrom!.TableFunction!.Arguments.Should().ContainSingle().Subject;
        argument.Should().NotBeOfType<ConstantExpression>();
        Expression.Lambda<Func<int>>(argument).Compile()().Should().Be(7);
    }

    [Fact]
    public void From_LambdaSource_ShouldPreserveANullArgumentExpression()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From(() => T320TvfDb.EchoText(null));

        var argument = builder.SourceFrom!.TableFunction!.Arguments.Should().ContainSingle().Subject;
        argument.Should().BeAssignableTo<ConstantExpression>().Which.Value.Should().BeNull();
    }

    [Fact]
    public void From_LambdaSource_ShouldCarryTheDescriptorMetadata()
    {
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From(() => T320TvfDb.EchoWithOptions(1));

        var function = builder.SourceFrom!.TableFunction!;
        function.WithClause.Should().Be("row_estimator=1");
        function.VerbatimArguments.Should().Equal(0);
    }

    [Fact]
    public void From_NullSource_ShouldThrowArgumentNullException()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<T320TvfRow>((Expression<Func<EntityBuilder<T320TvfRow>>>)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void From_CapturedDescriptor_ShouldBeRejectedAsLostProvenance()
    {
        using var ctx = new InMemoryDataContext();
        var descriptor = T320TvfDb.Echo(1);

        var act = () => ctx.From(() => descriptor);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("provenance");
    }

    [Fact]
    public void From_NonFactoryBody_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From(() => new EntityBuilder<T320TvfRow>(ctx));

        act.Should().Throw<NotSupportedException>();
    }

    // ---- D2: the same lambda source on the ordinary join family ----------------------------------

    [Fact]
    public void Join_LambdaSource_ShouldCarryTheTableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().Join(() => T320TvfDb.Echo(1), (left, right) => left.Id == right.Id);

        joined.JoinCondition.JoinType.Should().Be(JoinType.Inner);
        var function = joined.JoinCondition.From.TableFunction;
        function.Should().NotBeNull();
        function!.Name.Should().Be("echo_rows");
        function.Schema.Should().Be("dbo");
        function.Call.Method.Name.Should().Be(nameof(T320TvfDb.Echo));
    }

    [Fact]
    public void LeftJoin_LambdaSource_ShouldCarryTheTableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().LeftJoin(() => T320TvfDb.Echo(1), (left, right) => left.Id == right.Id);

        joined.JoinCondition.JoinType.Should().Be(JoinType.Left);
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");
    }

    [Fact]
    public void RightJoin_LambdaSource_ShouldCarryTheTableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().RightJoin(() => T320TvfDb.Echo(1), (left, right) => left.Id == right.Id);

        joined.JoinCondition.JoinType.Should().Be(JoinType.Right);
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");
    }

    [Fact]
    public void FullJoin_LambdaSource_ShouldCarryTheTableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().FullJoin(() => T320TvfDb.Echo(1), (left, right) => left.Id == right.Id);

        joined.JoinCondition.JoinType.Should().Be(JoinType.Full);
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");
    }

    [Fact]
    public void Join_LambdaSource_WithOptions_ShouldApplyTheJoinOptions()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().Join(
            () => T320TvfDb.Echo(1),
            (left, right) => left.Id == right.Id,
            options => options.WithJoinHint("loop"));

        joined.JoinCondition.JoinHint.Should().Be("loop");
    }

    [Fact]
    public void CrossJoin_LambdaSource_ShouldHaveNoConditionAndKeepTheSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().CrossJoin(() => T320TvfDb.Echo(1));

        joined.JoinCondition.JoinType.Should().Be(JoinType.Cross);
        joined.JoinCondition.JoinCondition.Should().BeNull();
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");
    }

    [Fact]
    public void CrossApply_LambdaSource_ShouldCarryTheTableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().CrossApply(() => T320TvfDb.Echo(1));

        joined.JoinCondition.JoinType.Should().Be(JoinType.CrossApply);
        joined.JoinCondition.ApplySource.Should().BeNull("a zero-argument source is not correlated");
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");
    }

    [Fact]
    public void OuterApply_LambdaSource_ShouldCarryTheTableFunctionSource()
    {
        using var ctx = new InMemoryDataContext();

        var joined = ctx.From<T320TvfRow>().OuterApply(() => T320TvfDb.Echo(1));

        joined.JoinCondition.JoinType.Should().Be(JoinType.OuterApply);
        joined.JoinCondition.ApplySource.Should().BeNull("a zero-argument source is not correlated");
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");
    }

    [Fact]
    public void Join_LambdaSource_ShouldPreserveTheArgumentExpressions()
    {
        using var ctx = new InMemoryDataContext();
        var id = 5;

        var joined = ctx.From<T320TvfRow>().Join(() => T320TvfDb.Echo(id), (left, right) => left.Id == right.Id);

        var argument = joined.JoinCondition.From.TableFunction!.Arguments.Should().ContainSingle().Subject;
        argument.Should().NotBeOfType<ConstantExpression>();
        Expression.Lambda<Func<int>>(argument).Compile()().Should().Be(5);
    }

    [Fact]
    public void Join_LambdaSource_NullSource_ShouldThrowArgumentNullException()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From<T320TvfRow>().Join(
            (Expression<Func<EntityBuilder<T320TvfRow>>>)null!,
            (left, right) => left.Id == right.Id);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void JoinedBuilder_LambdaSource_ShouldMirrorTheWholeChain()
    {
        using var ctx = new InMemoryDataContext();

        var chain = ctx.From<T320TvfRow>()
            .Join(() => T320TvfDb.Echo(1), (item1, item2) => item1.Id == item2.Id)
            .Join(() => T320TvfDb.Echo(2), (projection, item3) => projection.Item1.Id == item3.Id)
            .LeftJoin(() => T320TvfDb.Echo(3), (projection, item4) => projection.Item1.Id == item4.Id)
            .RightJoin(() => T320TvfDb.Echo(4), (projection, item5) => projection.Item1.Id == item5.Id)
            .FullJoin(() => T320TvfDb.Echo(5), (projection, item6) => projection.Item1.Id == item6.Id)
            .Join(() => T320TvfDb.Echo(6), (projection, item7) => projection.Item1.Id == item7.Id);

        chain.Should().BeOfType<JoinedEntityBuilder<T320TvfRow, T320TvfRow, T320TvfRow, T320TvfRow, T320TvfRow, T320TvfRow, T320TvfRow>>();
        chain.Joins.Should().HaveCount(6);
        chain.Joins!.Should().OnlyContain(j => j.From.TableFunction != null);
    }

    [Fact]
    public void JoinedBuilder_AppliedLambdaSources_ShouldMirrorTheSourceForms()
    {
        using var ctx = new InMemoryDataContext();
        var baseChain = ctx.From<T320TvfRow>().Join(() => T320TvfDb.Echo(1), (item1, item2) => item1.Id == item2.Id);

        var crossed = baseChain.CrossJoin(() => T320TvfDb.Echo(2));
        crossed.Joins![^1].JoinType.Should().Be(JoinType.Cross);
        crossed.Joins![^1].From.TableFunction!.Name.Should().Be("echo_rows");

        var applied = baseChain.CrossApply(() => T320TvfDb.Echo(3));
        applied.Joins![^1].JoinType.Should().Be(JoinType.CrossApply);

        var outer = baseChain.OuterApply(() => T320TvfDb.Echo(4));
        outer.Joins![^1].JoinType.Should().Be(JoinType.OuterApply);
    }

    [Fact]
    public void NonGeneric_LambdaSource_ShouldFollowTheExistingForwardingConventions()
    {
        using var ctx = new InMemoryDataContext();

        var named = new EntityBuilder(ctx);

        var joined = named.Join(() => T320TvfDb.Echo(1), (alias, right) => alias["id"].AsInt == right.Id);
        joined.Should().BeOfType<JoinedEntityBuilder<TableAlias, T320TvfRow>>();
        joined.JoinCondition.From.TableFunction!.Name.Should().Be("echo_rows");

        named.LeftJoin(() => T320TvfDb.Echo(1), (alias, right) => alias["id"].AsInt == right.Id)
            .JoinCondition.JoinType.Should().Be(JoinType.Left);
        named.RightJoin(() => T320TvfDb.Echo(1), (alias, right) => alias["id"].AsInt == right.Id)
            .JoinCondition.JoinType.Should().Be(JoinType.Right);
        named.FullJoin(() => T320TvfDb.Echo(1), (alias, right) => alias["id"].AsInt == right.Id)
            .JoinCondition.JoinType.Should().Be(JoinType.Full);
        named.CrossJoin(() => T320TvfDb.Echo(1)).JoinCondition.JoinType.Should().Be(JoinType.Cross);
        named.CrossApply(() => T320TvfDb.Echo(1)).JoinCondition.JoinType.Should().Be(JoinType.CrossApply);
        named.OuterApply(() => T320TvfDb.Echo(1)).JoinCondition.JoinType.Should().Be(JoinType.OuterApply);
    }

    [Fact]
    public void CorrelatedApply_LambdaSource_ShouldKeepTheOuterColumnReference()
    {
        using var ctx = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());

        var query = ctx.From<T320TvfRow>(meta => meta.Table("t320_left"))
            .CrossApply(row => T320TvfDb.Echo(row.Id))
            .Select(projection => projection.Item2.Id);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(query, false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");

        sql.Should().Contain("echo_rows", "the correlated factory call is the applied source");
        sql.Should().Contain("t320_left", "the left-hand source is still selected");
        sql.Should().NotContain("@", "the outer column must stay a column reference, not an evaluated argument value");
    }

    // ---- D2: a composed descriptor chain keeps being rejected as lost provenance ------------------

    [Fact]
    public void From_ComposedDescriptorChain_ShouldStillBeRejected()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From(() => T320TvfDb.Echo(1).Where(row => row.Id > 0));

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("does not return TvfEntityBuilder<T>");
    }
}
