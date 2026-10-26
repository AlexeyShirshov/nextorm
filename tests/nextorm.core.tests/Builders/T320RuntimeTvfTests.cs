using System.Data.Common;
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

public sealed class T320OtherRow
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

    /// <summary>
    /// Runtime configuration a factory may read instead of an SQL argument. This is the documented
    /// metadata source for a runtime factory: it is captured separately from the call expression, so the
    /// descriptor's metadata is stable while the arguments stay in the caller's tree.
    /// </summary>
    public static string ConfigurationName { get; set; } = "echo_configured";

    public static TvfEntityBuilder<T320TvfRow> Configured(int id) => new(ConfigurationName, "dbo");

    public static TvfEntityBuilder<T320TvfRow> Pair(int id, string? label) => new("echo_pair", "dbo");

    /// <summary>
    /// Deliberately <b>outside</b> the purity contract: the metadata depends on the value of a formal SQL
    /// argument. Used to observe what the metadata-only projection actually produces, not to detect it.
    /// </summary>
    public static TvfEntityBuilder<T320TvfRow> DefaultDriven(int id) => new($"echo_{id}", null);

    public static TvfEntityBuilder<T320TvfRow> VerbatimOutOfRange(int id) => new(
        "echo_rows",
        "dbo",
        null,
        null,
        new[] { 1 },
        TableFunctionSchema.None);

    public static TvfEntityBuilder<T320TvfRow> NoDescriptor(int id) => null!;

    public static TvfEntityBuilder<T320OtherRow> OtherRow(int id) => new("echo_other_rows");

    /// <summary>
    /// Fails for every non-default argument: used to prove that the metadata projection substitutes the
    /// typed default instead of evaluating the caller's SQL argument.
    /// </summary>
    public static TvfEntityBuilder<T320TvfRow> ExplosiveWhenEvaluated(int id)
        => id == default
            ? new("explosive_rows", "dbo")
            : throw new InvalidOperationException("The SQL argument value must never be evaluated for metadata discovery.");

    /// <summary>A static wrapper around a factory chain: the source body is not a factory call.</summary>
    public static EntityBuilder<T320TvfRow> Wrapped(int id) => Echo(id).Where(row => row.Id > 0);

    /// <summary>A predicate built outside the source lambda, so its node is a call rather than a lambda.</summary>
    public static Expression<Func<T320TvfRow, bool>> PrecomputedPredicate() => row => row.Id > 0;
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
    public void Descriptor_NullVerbatimArguments_ShouldThrowArgumentNullException()
    {
        // The declared contract is non-nullable: "no verbatim argument" is an empty list, never a null one.
        // NRT does not stop a runtime null, so the constructor has to reject it explicitly.
        var act = () => new TvfEntityBuilder<T320TvfRow>("echo_rows", "dbo", null, null, null!, TableFunctionSchema.None);

        act.Should().Throw<ArgumentNullException>()
            .Which.ParamName.Should().Be("verbatimArguments");
    }

    [Fact]
    public void Descriptor_VerbatimArgumentsParameter_ShouldBeNonNullableAndHaveNoDefault()
    {
        var constructor = typeof(TvfEntityBuilder<T320TvfRow>)
            .GetConstructors()
            .Single(candidate => candidate.GetParameters().Length == 6);
        var parameter = constructor.GetParameters()[4];

        parameter.Name.Should().Be("verbatimArguments");
        parameter.ParameterType.Should().Be(typeof(IReadOnlyList<int>), "the planned declaration is non-nullable");
        parameter.HasDefaultValue.Should().BeFalse("an optional-null parameter is forbidden by the plan");
        new NullabilityInfoContext().Create(parameter).ReadState.Should().Be(NullabilityState.NotNull);
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

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("must be a direct call to a runtime table-valued-function factory");
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

    // ---- F-04: a composed descriptor chain is a supported source expression -----------------------

    [Fact]
    public void From_ComposedDescriptorChain_ShouldKeepTheFactoryCallTheArgumentAndThePredicate()
    {
        using var ctx = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());
        var id = 7;

        var builder = ctx.From(() => T320TvfDb.Echo(id).Where(row => row.Id > 3));

        // The outermost Where must not be mistaken for the factory: the source stays the originating call
        // and its original argument expression, not the typed default used for metadata materialisation.
        var function = builder.SourceFrom!.TableFunction!;
        function.Name.Should().Be("echo_rows");
        function.Call.Method.Name.Should().Be(nameof(T320TvfDb.Echo));
        function.Arguments.Should().ContainSingle()
            .Which.NodeType.Should().Be(ExpressionType.MemberAccess);

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(row => row.Id), false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");

        sql.Should().Contain("echo_rows(", "the factory call stays the source of the statement");
        sql.Should().Contain("where", "the composed predicate is applied to the same statement");
        sql.Should().Contain("3", "the predicate body is preserved");
        prepared.DbCommandParams.Cast<DbParameter>().Should()
            .Contain(parameter => Equals(parameter.Value, 7), "the original SQL argument stays the function argument");
    }

    [Fact]
    public void From_ComposedDescriptorChain_ShouldKeepTheOrdering()
    {
        using var ctx = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());
        var id = 7;

        var builder = ctx.From(() => T320TvfDb.Echo(id).OrderBy(row => row.Id));

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(row => row.Id), false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");

        sql.Should().Contain("echo_rows(", "the factory call stays the source of the statement");
        sql.Should().Contain("order by", "the composed ordering is preserved");
        prepared.DbCommandParams.Cast<DbParameter>().Should().Contain(parameter => Equals(parameter.Value, 7));
    }

    [Fact]
    public void From_ComposedDescriptorChain_WithATypeChangingProjection_ShouldKeepTheSource()
    {
        using var ctx = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());
        var id = 7;

        // The factory row type (T320TvfRow) and the composed result type (T320OtherRow) differ, so the
        // lowering must track them separately while keeping the source and its argument.
        var builder = ctx.From(() => T320TvfDb.Echo(id).As(row => new T320OtherRow { Id = row.Id }));

        builder.Should().BeAssignableTo<EntityBuilder<T320OtherRow>>();

        var prepared = (DbPreparedQueryCommand<int>)ctx.GetPreparedQueryCommand(
            builder.Select(row => row.Id), false, false, CancellationToken.None);
        var sql = prepared.DbCommand.CommandText.Replace("\r\n", "\n");

        sql.Should().Contain("echo_rows(", "the composed source keeps the factory call");
        prepared.DbCommandParams.Cast<DbParameter>().Should()
            .Contain(parameter => Equals(parameter.Value, 7), "the original argument survives the projection");
    }

    [Fact]
    public void From_ComposedDescriptorChain_ShouldNotEvaluateTheSqlArgumentForMetadata()
    {
        using var ctx = new InMemoryDataContext();
        var id = 11;

        // The factory throws for every non-default argument, so this only succeeds when the metadata
        // projection substitutes the typed default instead of evaluating the caller's argument.
        var builder = ctx.From(() => T320TvfDb.ExplosiveWhenEvaluated(id).Where(row => row.Id > 0));

        builder.SourceFrom!.TableFunction!.Name.Should().Be("explosive_rows");
        builder.SourceFrom.TableFunction.Arguments.Should().ContainSingle()
            .Which.NodeType.Should().Be(ExpressionType.MemberAccess, "the original argument expression is preserved");
    }

    [Fact]
    public void From_ChainOverANonFactoryStaticCall_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From(() => T320TvfDb.Wrapped(1));

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("does not return TvfEntityBuilder<T>");
    }

    [Fact]
    public void From_BuilderOperationWithAPrecomputedLambda_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From(() => T320TvfDb.Echo(1).Where(T320TvfDb.PrecomputedPredicate()));

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("is not supported as part of a table-valued-function source chain");
    }

    // ---- D1 probe: what the metadata-only projection and the argument preservation actually do --------

    private static readonly MethodInfo EchoFactory =
        typeof(T320TvfDb).GetMethod(nameof(T320TvfDb.Echo), BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void From_ConfigurationCapturedOutsideTheSqlArguments_ShouldSupplyTheMetadata()
    {
        using var ctx = new InMemoryDataContext();
        var id = 3;
        var previous = T320TvfDb.ConfigurationName;

        try
        {
            T320TvfDb.ConfigurationName = "echo_configured";

            var builder = ctx.From(() => T320TvfDb.Configured(id));
            var function = builder.SourceFrom!.TableFunction!;

            function.Name.Should().Be("echo_configured", "the name is captured configuration, not an SQL argument");
            function.Schema.Should().Be("dbo");
            function.Call.Method.Name.Should().Be(nameof(T320TvfDb.Configured));

            var argument = function.Arguments.Should().ContainSingle().Subject;
            argument.NodeType.Should().Be(ExpressionType.MemberAccess, "the caller's closure field is preserved untouched");
            Expression.Lambda<Func<int>>(argument).Compile()().Should().Be(3);

            // A second lowering after the configuration changed picks the new value up: proof that the
            // metadata comes from the captured configuration and never from the argument expression.
            T320TvfDb.ConfigurationName = "echo_configured_2";
            ctx.From(() => T320TvfDb.Configured(id)).SourceFrom!.TableFunction!.Name
                .Should().Be("echo_configured_2");
        }
        finally
        {
            T320TvfDb.ConfigurationName = previous;
        }
    }

    [Fact]
    public void From_LambdaSource_ShouldPreserveANonDefaultLiteralAndACapturedArgument()
    {
        using var ctx = new InMemoryDataContext();
        var id = 9;

        var builder = ctx.From(() => T320TvfDb.Pair(id, "literal-x"));
        var function = builder.SourceFrom!.TableFunction!;

        function.Arguments.Should().HaveCount(2);
        function.Arguments.Should().NotContain(
            argument => argument is DefaultExpression,
            "the typed defaults used for the metadata projection must never reach SQL lowering");

        var captured = function.Arguments[0].Should().BeAssignableTo<MemberExpression>().Subject;
        captured.NodeType.Should().Be(ExpressionType.MemberAccess);
        captured.Member.Name.Should().Be("id");
        Expression.Lambda<Func<int>>(function.Arguments[0]).Compile()().Should().Be(9);

        var literal = function.Call.Arguments[1].Should().BeOfType<ConstantExpression>().Subject;
        literal.Value.Should().Be("literal-x");
    }

    [Fact]
    public void CorrelatedApply_ShouldKeepTheOuterMemberAccessAndEvaluateNothing()
    {
        // The in-memory provider rejects a correlated APPLY explicitly (EntityBuilder.JoinApply), so the
        // probe needs a SQL provider. No connection is opened: only the builder state is inspected.
        using var ctx = new SqlServerDataContext(
            "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True",
            new DataContextBuilder());

        var joined = ctx.From<T320TvfRow>(meta => meta.Table("t320_outer"))
            .CrossApply(row => T320TvfDb.Echo(row.Id));

        var applySource = joined.JoinCondition.ApplySource;
        applySource.Should().NotBeNull("the correlated form must carry the outer parameter");
        applySource!.Parameters.Should().ContainSingle();

        var factoryCall = FindFactoryCall(applySource.Body, EchoFactory);
        var argument = factoryCall.Arguments.Should().ContainSingle().Subject;

        argument.NodeType.Should().Be(
            ExpressionType.MemberAccess,
            "a correlated argument stays an outer-column member access; evaluating it would turn it into a ConstantExpression");

        var outer = argument.Should().BeAssignableTo<MemberExpression>().Subject;
        outer.Member.Name.Should().Be(nameof(T320TvfRow.Id));
        outer.Expression.Should().BeAssignableTo<ParameterExpression>(
            "the member access must still be rooted in the outer parameter, never in a captured value");
        ((ParameterExpression)outer.Expression!).Name.Should().Be("row");
    }

    [Fact]
    public void From_MetadataDerivedFromASqlArgument_ShouldUseTheTypedDefaultAsADocumentedObligation()
    {
        // DOCUMENTED CALLER OBLIGATION (purity contract): the metadata is materialised by invoking the
        // factory once with a typed default in every formal SQL argument position, because the original
        // argument expressions must never be evaluated to obtain a descriptor. A factory whose metadata
        // depends on an argument value is therefore outside the contract. This fact observes that
        // contract; it deliberately does not detect, reject or police an impure factory.
        using var ctx = new InMemoryDataContext();

        var builder = ctx.From(() => T320TvfDb.DefaultDriven(7));
        var function = builder.SourceFrom!.TableFunction!;

        function.Name.Should().Be(
            "echo_0",
            "default(int) is what the metadata projection passes, so the documented caller obligation is that metadata must not depend on an argument value");
        function.Arguments.Should().ContainSingle()
            .Which.Should().BeOfType<ConstantExpression>()
            .Which.Value.Should().Be(7, "the SQL argument itself is still the caller's untouched 7");
    }

    // ---- D3: the remaining fail-closed negatives, each with its named message -----------------------

    [Fact]
    public void Descriptor_EmptyName_ShouldBeRejected()
    {
        var act = () => new TvfEntityBuilder<T320TvfRow>("   ");

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("cannot be empty");
    }

    [Fact]
    public void Descriptor_NegativeVerbatimIndex_ShouldBeRejected()
    {
        var act = () => new TvfEntityBuilder<T320TvfRow>(
            "echo_rows",
            null,
            null,
            null,
            new[] { -1 },
            TableFunctionSchema.None);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("cannot be negative");
    }

    [Fact]
    public void Descriptor_DuplicateVerbatimIndex_ShouldBeRejected()
    {
        var act = () => new TvfEntityBuilder<T320TvfRow>(
            "echo_rows",
            null,
            null,
            null,
            new[] { 0, 0 },
            TableFunctionSchema.None);

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("declared more than once");
    }

    [Fact]
    public void From_OutOfRangeVerbatimIndex_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From(() => T320TvfDb.VerbatimOutOfRange(1));

        act.Should().Throw<ArgumentException>()
            .Which.Message.Should().Contain("out of range");
    }

    [Fact]
    public void From_FactoryReturningNoDescriptor_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();

        var act = () => ctx.From(() => T320TvfDb.NoDescriptor(1));

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("returned no table-valued-function descriptor");
    }

    [Fact]
    public void From_FactoryReturningADifferentRowType_ShouldBeRejected()
    {
        using var ctx = new InMemoryDataContext();
        Expression<Func<EntityBuilder<T320TvfRow>>> source =
            () => (EntityBuilder<T320TvfRow>)(object)T320TvfDb.OtherRow(1);

        var act = () => ctx.From(source);

        act.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain("does not match the source row type");
    }

    private static MethodCallExpression FindFactoryCall(Expression expression, MethodInfo factory)
    {
        var finder = new FactoryCallFinder(factory);
        finder.Visit(expression);

        return finder.Call
            ?? throw new InvalidOperationException($"The factory call '{factory.DeclaringType?.Name}.{factory.Name}' is not in the lowered APPLY source.");
    }

    private sealed class FactoryCallFinder(MethodInfo factory) : ExpressionVisitor
    {
        public MethodCallExpression? Call { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (Call is null && node.Method == factory)
                Call = node;

            return base.VisitMethodCall(node);
        }
    }
}
