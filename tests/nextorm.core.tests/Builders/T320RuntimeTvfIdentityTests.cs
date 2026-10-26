using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The legacy attribute-declared declaration and the runtime <see cref="TvfEntityBuilder{T}"/> factory
/// used as a pair. The two carry identical SQL metadata and identical arguments, so their lowered
/// sources are the same SQL identity and must share one plan key (AC08).
/// </summary>
internal static class T320IdentityTvf
{
    [SqlTableFunction("probe_tvf")]
    public static IQueryable<T320TvfRow> LegacyProbe(int id) => throw new NotSupportedException();

    public static TvfEntityBuilder<T320TvfRow> ProbeTvf(int id) => new("probe_tvf");
}

/// <summary>
/// A declaration whose declaring type derives from <see cref="CommonFunctions"/>, so the renderer
/// classifies it as built-in even though its metadata is identical to the custom probe above.
/// </summary>
internal sealed class T320BuiltInTvf : CommonFunctions
{
    [SqlTableFunction("probe_tvf")]
    public static IQueryable<T320TvfRow> BuiltInProbe(int id) => throw new NotSupportedException();
}

/// <summary>
/// #320 (T320) AC08: source identity is the SQL identity, never the factory method. The plan-key
/// comparer must merge a runtime factory and a legacy attribute declaration with identical metadata and
/// arguments, and it must keep every SQL-visible difference apart. Comparer level only: no database.
/// </summary>
[Trait("Category", "T320")]
public class T320RuntimeTvfIdentityTests
{
    private static readonly MethodInfo CustomProbe =
        typeof(T320IdentityTvf).GetMethod(nameof(T320IdentityTvf.LegacyProbe), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo BuiltInProbe =
        typeof(T320BuiltInTvf).GetMethod(nameof(T320BuiltInTvf.BuiltInProbe), BindingFlags.Public | BindingFlags.Static)!;

    private static FromExpressionPlanEqualityComparer Comparer(IDataContext ctx)
        => ctx.From<T320TvfRow>().Select(row => row.Id).GetFromExpressionPlanEqualityComparer();

    private static MethodCallExpression Call(int argument = 42, MethodInfo? method = null)
        => Expression.Call(method ?? CustomProbe, Expression.Constant(argument));

    /// <summary>A source node with the custom declaration's metadata; only the named field varies.</summary>
    private static FromExpression Node(
        string name = "probe_tvf",
        string? schema = null,
        string? withClause = null,
        string? callClause = null,
        IReadOnlyList<int>? verbatimArguments = null,
        int argument = 42,
        MethodInfo? method = null)
        => new(new TableFunctionExpression(name, schema, withClause, callClause, verbatimArguments, Call(argument, method)));

    private static FromExpression ResultSchemaNode(TableFunctionSchema resultSchema)
        => new(TableFunctionExpression.CreateRuntime(
            "probe_tvf",
            null,
            null,
            null,
            null,
            Call(),
            typeof(T320TvfRow),
            resultSchema));

    private static void ShouldDiffer(FromExpression first, FromExpression second, string because)
    {
        using var ctx = new InMemoryDataContext();
        var comparer = Comparer(ctx);

        comparer.Equals(first, second).Should().BeFalse(because);
        comparer.Equals(second, first).Should().BeFalse($"{because} (symmetric)");
    }

    // ---- the claim: same SQL identity -> one plan key, whatever produced it ----------------------

    [Fact]
    public void RuntimeFactory_AndLegacyAttributeTvf_WithTheSameSqlIdentity_ShouldShareThePlanKey()
    {
        using var ctx = new InMemoryDataContext();

        var legacy = ctx.FromTableFunction(() => T320IdentityTvf.LegacyProbe(42)).SourceFrom!;
        var runtime = ctx.From(() => T320IdentityTvf.ProbeTvf(42)).SourceFrom!;

        legacy.TableFunction!.Name.Should().Be("probe_tvf");
        runtime.TableFunction!.Name.Should().Be("probe_tvf");
        legacy.TableFunction.ResultType.Should().BeNull("a legacy declaration without a result schema carries no result type");
        runtime.TableFunction.ResultType.Should().Be(typeof(T320TvfRow), "the runtime node always knows its row type");
        legacy.TableFunction.ResultSchema.Should().Be(TableFunctionSchema.None);
        runtime.TableFunction.ResultSchema.Should().Be(TableFunctionSchema.None);

        var comparer = Comparer(ctx);
        var observed = $"legacy={legacy.TableFunction.Name}; runtime={runtime.TableFunction.Name}; "
            + $"legacyResultType={legacy.TableFunction.ResultType?.Name ?? "null"}; runtimeResultType={runtime.TableFunction.ResultType?.Name}";

        comparer.Equals(legacy, runtime).Should().BeTrue(
            $"the factory method is not part of the SQL identity, so identical metadata and arguments must share one plan key; {observed}");
        comparer.GetHashCode(legacy).Should().Be(
            comparer.GetHashCode(runtime),
            $"equal plan keys must hash alike; {observed}");
    }

    // ---- negatives: every SQL-visible difference stays a different plan key ----------------------

    [Fact]
    public void DifferentFunctionName_ShouldNotShareThePlanKey()
        => ShouldDiffer(Node(name: "probe_tvf"), Node(name: "probe_other"), "the rendered function name differs");

    [Fact]
    public void DifferentSchema_ShouldNotShareThePlanKey()
        => ShouldDiffer(Node(schema: "dbo"), Node(schema: "other"), "the rendered schema prefix differs");

    [Fact]
    public void DifferentWithClause_ShouldNotShareThePlanKey()
        => ShouldDiffer(Node(withClause: "row_estimator=1"), Node(withClause: "row_estimator=2"), "the trailing WITH clause text differs");

    [Fact]
    public void DifferentCallClause_ShouldNotShareThePlanKey()
        => ShouldDiffer(
            Node(callClause: ", '$[0]' columns(id int path '$.id')"),
            Node(callClause: ", '$[*]' columns(id int path '$.id')"),
            "the in-call clause text differs");

    [Fact]
    public void DifferentVerbatimArgumentIndices_ShouldNotShareThePlanKey()
        => ShouldDiffer(Node(verbatimArguments: new[] { 0 }), Node(verbatimArguments: Array.Empty<int>()), "a verbatim identifier renders differently from a value argument");

    [Fact]
    public void DifferentResultSchema_ShouldNotShareThePlanKey()
        => ShouldDiffer(
            ResultSchemaNode(TableFunctionSchema.None),
            ResultSchemaNode(TableFunctionSchema.AliasColumnList),
            "the caller-declared result schema is rendered into the SQL");

    [Fact]
    public void DifferentArgumentValue_ShouldNotShareThePlanKey()
        => ShouldDiffer(Node(argument: 42), Node(argument: 43), "the argument expression shape/value differs");

    [Fact]
    public void BuiltInAndCustomDeclarations_ShouldNotShareThePlanKey()
        => ShouldDiffer(
            Node(method: BuiltInProbe),
            Node(method: CustomProbe),
            "built-in and custom functions have different support checks and literal rendering");
}
