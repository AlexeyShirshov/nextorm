using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// Compile-level proof for #249 (T353 C6): the four additive parameterless terminals
/// (<c>EntityBuilder&lt;T&gt;.ToList()</c>/<c>ToArray()</c> and <c>QueryCommand&lt;T&gt;.ToList()</c>/<c>ToArray()</c>)
/// bind inside an expression tree, where the <c>params ReadOnlySpan&lt;object?&gt;</c> overloads are illegal
/// (<c>CS8640</c>/<c>CS9226</c>).
/// </summary>
/// <remarks>
/// Nothing here executes a query: the assertions inspect the tree the compiler built, and each one proves the
/// zero-parameter overload was selected rather than the <c>params</c> form. The adapter arm is the reason the
/// overloads exist -- <c>o.Items.AsEntityBuilder&lt;Item&gt;()...Select(...).ToList()</c> cannot be written as an
/// expression tree otherwise.
/// </remarks>
public sealed class CollectionProjectionExpressionBindingTests
{
    public sealed class ParentRow
    {
        public int Id { get; set; }
        public List<ChildRow> Items { get; set; } = [];
    }

    public sealed class ChildRow
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class ProjectedChild
    {
        public int Id { get; set; }
    }

    [Fact]
    public void EntityBuilder_ParameterlessToListAndToArray_ShouldBindInsideExpressionTrees()
    {
        var context = (IDataContext)null!;

        Expression<Func<ParentRow, List<ChildRow>>> list = p => context.From<ChildRow>().Where(c => c.ParentId == p.Id).ToList();
        Expression<Func<ParentRow, ChildRow[]>> array = p => context.From<ChildRow>().Where(c => c.ParentId == p.Id).ToArray();

        VerifyParameterlessTerminal(list, typeof(List<ChildRow>), nameof(EntityBuilderExtensions.ToList));
        VerifyParameterlessTerminal(array, typeof(ChildRow[]), nameof(EntityBuilderExtensions.ToArray));
    }

    [Fact]
    public void QueryCommand_ParameterlessToListAndToArray_ShouldBindInsideExpressionTrees()
    {
        var command = (QueryCommand<ChildRow>)null!;

        Expression<Func<ParentRow, List<ChildRow>>> list = p => command.ToList();
        Expression<Func<ParentRow, ChildRow[]>> array = p => command.ToArray();

        VerifyParameterlessTerminal(list, typeof(List<ChildRow>), nameof(QueryCommand<ChildRow>.ToList));
        VerifyParameterlessTerminal(array, typeof(ChildRow[]), nameof(QueryCommand<ChildRow>.ToArray));
    }

    [Fact]
    public void AdapterChain_ParameterlessTerminals_ShouldBindInsideExpressionTrees()
    {
        Expression<Func<ParentRow, List<ProjectedChild>>> list = p =>
            p.Items.AsEntityBuilder<ChildRow>()
                .Where(c => c.ParentId == p.Id)
                .OrderBy(c => c.Id)
                .Limit(5)
                .Select(c => new ProjectedChild { Id = c.Id })
                .ToList();

        Expression<Func<ParentRow, ProjectedChild[]>> array = p =>
            p.Items.AsEntityBuilder<ChildRow>()
                .Where(c => c.ParentId == p.Id)
                .OrderBy(c => c.Id)
                .Limit(5)
                .Select(c => new ProjectedChild { Id = c.Id })
                .ToArray();

        VerifyParameterlessTerminal(list, typeof(List<ProjectedChild>), nameof(QueryCommand<ProjectedChild>.ToList));
        VerifyParameterlessTerminal(array, typeof(ProjectedChild[]), nameof(QueryCommand<ProjectedChild>.ToArray));
    }

    private static void VerifyParameterlessTerminal(LambdaExpression expression, Type expectedType, string terminalName)
    {
        expression.Body.Type.Should().Be(expectedType);

        var call = expression.Body.Should().BeAssignableTo<MethodCallExpression>().Subject;
        call.Method.Name.Should().Be(terminalName);

        // The zero-argument terminal carries only the extension receiver (if any): the selected overload is the
        // parameterless one, not the `params ReadOnlySpan<object?>` form that has a second parameter.
        var declaredParameterCount = call.Method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), inherit: false) ? 1 : 0;
        call.Method.GetParameters().Should().HaveCount(declaredParameterCount);
    }
}
