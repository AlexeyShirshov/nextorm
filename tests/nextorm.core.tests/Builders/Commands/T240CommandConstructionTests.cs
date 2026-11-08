using System.Linq.Expressions;
using FluentAssertions;
using NextORM.SqlServer;

namespace NextORM.Core.Tests;

/// <summary>
/// D240-2 (#240): the <see cref="QueryCommand"/> fluent-construction path. Proves that a fluent step
/// yields a NEW command while the previous command keeps its values and its generated SQL, that sibling
/// branches stay isolated, that set-op operands stay reusable, and that a caller-supplied
/// <see cref="QueryDefinition"/> keeps its arrays defensively copied — the ownership elision is private
/// to the self-clone path that produces the array itself. Trait <c>Category=T240Command</c> is the
/// unit's inner-loop selector.
/// </summary>
[Trait("Category", "T240Command")]
public class T240CommandConstructionTests
{
    private const string SqlServerPlaceholder =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    private static SqlServerDataContext NewContext() => new(SqlServerPlaceholder, new DataContextBuilder());

    /// <summary>Renders the generated SQL for a command without ever opening a connection.</summary>
    private static string RenderSql<T>(IDataContext context, QueryCommand<T> command)
        => ((DbPreparedQueryCommand<T>)context.GetPreparedQueryCommand(command, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    // ---- (a) a fluent step leaves the previous command unchanged (values AND generated SQL) --------

    [Fact]
    public void FluentStep_LeavesPreviousCommandValuesUnchanged()
    {
        using var ctx = NewContext();
        var first = ctx.From<SimpleEntity>().Where(x => x.Id > 10).Select(x => x.Id);

        var second = first.OrderBy(1).Limit(5);

        second.Should().NotBeSameAs(first);
        first.Definition.Sorting.Should().BeNull();
        first.Definition.Paging.Limit.Should().Be(0);
        second.Definition.Sorting.Should().NotBeNull().And.HaveCount(1);
        second.Definition.Paging.Limit.Should().Be(5);
    }

    [Fact]
    public void FluentStep_LeavesPreviousCommandGeneratedSqlUnchanged()
    {
        using var ctx = NewContext();
        var first = ctx.From<SimpleEntity>().Where(x => x.Id > 10).Select(x => x.Id);
        var before = RenderSql(ctx, first);

        var second = first.OrderBy(1).Limit(5);

        RenderSql(ctx, first).Should().Be(before);
        RenderSql(ctx, second).Should().NotBe(before);
    }

    // ---- (b) sibling branches from the same parent do not contaminate each other or the parent -------

    [Fact]
    public void SiblingBranches_DoNotContaminateEachOtherOrTheParent()
    {
        using var ctx = NewContext();
        var parent = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var parentSql = RenderSql(ctx, parent);

        var ordered = parent.OrderBy(1);
        var distinct = parent.Distinct();

        RenderSql(ctx, parent).Should().Be(parentSql);
        parent.Definition.Sorting.Should().BeNull();
        parent.Definition.IsDistinct.Should().BeFalse();

        ordered.Definition.Sorting.Should().NotBeNull().And.HaveCount(1);
        ordered.Definition.IsDistinct.Should().BeFalse();
        distinct.Definition.IsDistinct.Should().BeTrue();
        distinct.Definition.Sorting.Should().BeNull();

        var orderedSql = RenderSql(ctx, ordered);
        var distinctSql = RenderSql(ctx, distinct);
        orderedSql.Should().NotBe(parentSql);
        distinctSql.Should().NotBe(parentSql);
        orderedSql.Should().NotBe(distinctSql);
        RenderSql(ctx, parent).Should().Be(parentSql);
    }

    // ---- (c) set-op / Distinct operand reuse ----------------------------------------------------------

    [Fact]
    public void Union_ReusingOperands_LeavesOperandsAndResultIndependent()
    {
        using var ctx = NewContext();
        var left = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var right = ctx.From<SimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id);
        var leftSql = RenderSql(ctx, left);
        var rightSql = RenderSql(ctx, right);

        var union = left.Union(right);
        var unionAgain = left.Union(right);

        RenderSql(ctx, left).Should().Be(leftSql);
        RenderSql(ctx, right).Should().Be(rightSql);

        var unionSql = RenderSql(ctx, union);
        unionSql.Should().Contain("union");
        unionSql.Should().NotBe(leftSql);
        RenderSql(ctx, unionAgain).Should().Be(unionSql);
    }

    [Fact]
    public void Distinct_ClonesAndLeavesOperandUnchanged()
    {
        using var ctx = NewContext();
        var operand = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var operandSql = RenderSql(ctx, operand);

        var distinct = operand.Distinct();

        distinct.Should().NotBeSameAs(operand);
        distinct.Definition.IsDistinct.Should().BeTrue();
        operand.Definition.IsDistinct.Should().BeFalse();
        RenderSql(ctx, operand).Should().Be(operandSql);
        RenderSql(ctx, distinct).Should().Contain("distinct");
    }

    // ---- (d) caller-supplied QueryDefinition arrays stay defensively copied --------------------------

    [Fact]
    public void CallerSuppliedDefinition_MutatedJoinsDoNotAffectCommand()
    {
        using var ctx = NewContext();
        var original = new JoinExpression(null, JoinType.Cross) { From = new FromExpression(typeof(SimpleEntity)) };
        var replacement = new JoinExpression(null, JoinType.Inner) { From = new FromExpression(typeof(SimpleEntity)) };
        var suppliedJoins = new[] { original };
        var definition = new QueryDefinition { SrcType = typeof(SimpleEntity), Joins = suppliedJoins };

        var command = ctx.CreateCommand<SimpleEntity>(definition);

        command.Joins.Should().NotBeNull();
        command.Joins!.Should().NotBeSameAs(suppliedJoins);
        command.Joins.Should().HaveCount(1);
        command.Joins[0].Should().BeSameAs(original);

        suppliedJoins[0] = replacement;

        command.Joins[0].Should().BeSameAs(original);
    }

    [Fact]
    public void CallerSuppliedDefinition_MutatedArrayJoinsDoNotAffectCommand()
    {
        using var ctx = NewContext();
        LambdaExpression original = (Expression<Func<int, int>>)(x => x);
        LambdaExpression replacement = (Expression<Func<int, int>>)(x => x + 1);
        var supplied = new[] { original };
        var definition = new QueryDefinition { SrcType = typeof(SimpleEntity), ArrayJoins = supplied };

        var command = ctx.CreateCommand<SimpleEntity>(definition);

        command.Definition.ArrayJoins.Should().NotBeNull();
        command.Definition.ArrayJoins!.Should().NotBeSameAs(supplied);
        command.Definition.ArrayJoins.Should().HaveCount(1);

        supplied[0] = replacement;

        command.Definition.ArrayJoins![0].Should().BeSameAs(original);
    }

    // ---- (e) repeated operators / nested ---------------------------------------------------------------

    [Fact]
    public void RepeatedOperators_EachIntermediateCommandStaysIndependent()
    {
        using var ctx = NewContext();
        var root = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var rootSql = RenderSql(ctx, root);

        var step1 = root.OrderBy(1);
        var step2 = step1.Limit(3);
        var step3 = step2.Offset(1);

        RenderSql(ctx, root).Should().Be(rootSql);
        step1.Definition.Sorting.Should().HaveCount(1);
        step1.Definition.Paging.Limit.Should().Be(0);
        step2.Definition.Sorting.Should().HaveCount(1);
        step2.Definition.Paging.Limit.Should().Be(3);
        step2.Definition.Paging.Offset.Should().Be(0);
        step3.Definition.Paging.Limit.Should().Be(3);
        step3.Definition.Paging.Offset.Should().Be(1);
        step1.Should().NotBeSameAs(root);
        step2.Should().NotBeSameAs(step1);
        step3.Should().NotBeSameAs(step2);
    }

    [Fact]
    public void NestedSetOperations_KeepOperandsUnchanged()
    {
        using var ctx = NewContext();
        var a = ctx.From<SimpleEntity>().Where(x => x.Id > 0).Select(x => x.Id);
        var b = ctx.From<SimpleEntity>().Where(x => x.Id > 5).Select(x => x.Id);
        var c = ctx.From<SimpleEntity>().Where(x => x.Id > 9).Select(x => x.Id);
        var aSql = RenderSql(ctx, a);
        var bSql = RenderSql(ctx, b);
        var cSql = RenderSql(ctx, c);

        var union = a.Union(b);
        var except = union.Except(c);

        RenderSql(ctx, a).Should().Be(aSql);
        RenderSql(ctx, b).Should().Be(bSql);
        RenderSql(ctx, c).Should().Be(cSql);
        var unionSql = RenderSql(ctx, union);
        unionSql.Should().NotBe(aSql);
        RenderSql(ctx, except).Should().NotBe(unionSql);
    }

    // ---- (f) null / empty collections ------------------------------------------------------------------

    [Fact]
    public void EmptyDefinition_YieldsCommandWithoutArrays()
    {
        using var ctx = NewContext();

        var command = ctx.CreateCommand<SimpleEntity>(new QueryDefinition());

        command.Joins.Should().BeNull();
        command.Definition.ArrayJoins.Should().BeNull();
    }

    [Fact]
    public void EmptyCallerArrays_AreCopiedNotShared()
    {
        using var ctx = NewContext();
        var emptyJoins = Array.Empty<JoinExpression>();
        var emptyArrayJoins = Array.Empty<LambdaExpression>();

        var command = ctx.CreateCommand<SimpleEntity>(new QueryDefinition
        {
            SrcType = typeof(SimpleEntity),
            Joins = emptyJoins,
            ArrayJoins = emptyArrayJoins,
        });

        command.Joins.Should().NotBeNull().And.BeEmpty();
        command.Definition.ArrayJoins.Should().NotBeNull().And.BeEmpty();
    }
}
