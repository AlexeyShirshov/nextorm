using System.Linq.Expressions;
using System.Reflection;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T312 (#312) core contract tests for the boundary-local rendering bit and the shared
/// <c>DerivedPassThroughColumn</c> prediction rule.
///
/// The production member <c>SqlBuildContext.RequireDerivedScalarOutputName</c> does not exist before the
/// fix, so these tests locate it by reflection: the red arm must be a behavioural assertion failure
/// (member absent / default wrong), never a compile error. After part 1b lands, the same tests exercise
/// both outcomes of the new branch (default <c>false</c>, explicitly <c>true</c>), the shared predictor
/// for a named column and for an unaliased computed scalar, the unchanged JSON property-name policy and
/// the in-memory provider's data-definition refusal.
/// </summary>
public class T312DerivedScalarAliasContractTests
{
    private const string BoundaryBitName = "RequireDerivedScalarOutputName";

    private static MemberInfo? FindBoundaryBit()
        => typeof(SqlBuildContext)
            .GetMember(BoundaryBitName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault();

    private static object NewContext() => Activator.CreateInstance(typeof(SqlBuildContext))!;

    private static bool ReadBoundaryBit(object context)
        => FindBoundaryBit().Should().NotBeNull(
                "the boundary-local bit '" + BoundaryBitName + "' must exist on SqlBuildContext")
            .And.Subject switch
        {
            PropertyInfo property => (bool)property.GetValue(context)!,
            FieldInfo field => (bool)field.GetValue(context)!,
            _ => throw new InvalidOperationException("boundary bit is not declared"),
        };

    private static void WriteBoundaryBit(object context, bool value)
    {
        FindBoundaryBit().Should().NotBeNull(
            "the boundary-local bit '" + BoundaryBitName + "' must exist on SqlBuildContext");
        switch (FindBoundaryBit())
        {
            case PropertyInfo property:
                property.SetValue(context, value);
                break;
            case FieldInfo field:
                field.SetValue(context, value);
                break;
            default:
                throw new InvalidOperationException("boundary bit is not declared");
        }
    }

    private static MethodInfo Predictor()
    {
        var preparer = typeof(QueryCommand).GetNestedType("QueryPreparer", BindingFlags.NonPublic | BindingFlags.Public);
        preparer.Should().NotBeNull("QueryCommand.QueryPreparer is the shared predictor owner");

        var method = preparer!.GetMethod("DerivedPassThroughColumn", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
        method.Should().NotBeNull("DerivedPassThroughColumn is the shared derived-column prediction rule");
        return method!;
    }

    private static SelectExpression Predict(SelectExpression source)
        => (SelectExpression)Predictor().Invoke(null, [source])!;

    [Fact]
    public void D1_BoundaryBit_ShouldBeDeclared()
    {
        FindBoundaryBit().Should().NotBeNull(
            "the boundary-local bit '" + BoundaryBitName + "' must exist on SqlBuildContext and default to false");
    }

    [Fact]
    public void D1_BoundaryBit_DefaultShouldBeFalse()
    {
        var context = NewContext();
        ReadBoundaryBit(context).Should().BeFalse(
            "derived/CTAS boundaries opt in explicitly; every other render keeps the existing naming policy");
    }

    [Fact]
    public void D1_BoundaryBit_ShouldRoundTripTrue()
    {
        var context = NewContext();
        WriteBoundaryBit(context, true);
        ReadBoundaryBit(context).Should().BeTrue("the boundary can request the derived scalar output name");
    }

    [Fact]
    public void D1_BoundaryBit_BothOutcomesShouldBeDistinguishable()
    {
        var off = NewContext();
        var on = NewContext();
        WriteBoundaryBit(on, true);

        ReadBoundaryBit(off).Should().BeFalse();
        ReadBoundaryBit(on).Should().BeTrue();
        ReadBoundaryBit(on).Should().NotBe(ReadBoundaryBit(off));
    }

    [Fact]
    public void D1_Predictor_UnaliasedComputedScalar_ShouldExposeOutputName()
    {
        var source = new SelectExpression(typeof(int))
        {
            Index = 0,
            OutputName = "c0",
            Expression = Expression.Multiply(Expression.Parameter(typeof(int), "x"), Expression.Constant(2)),
        };

        var predicted = Predict(source);

        predicted.OutputName.Should().Be("c0", "the predictor must mirror the identifier the boundary now declares");
        predicted.IsProjectionOutputReference.Should().BeTrue("the consumer references the derived output, not the defining expression");
        predicted.PropertyName.Should().BeNull();
    }

    [Fact]
    public void D1_Predictor_NamedColumn_ShouldKeepMemberName()
    {
        var source = new SelectExpression(typeof(int))
        {
            Index = 0,
            PropertyName = "Id",
            Expression = Expression.Parameter(typeof(int), "Id"),
        };

        var predicted = Predict(source);

        predicted.OutputName.Should().Be("Id", "a named projection keeps its member identifier");
        predicted.PropertyName.Should().Be("Id");
    }

    [Fact]
    public void D1_JsonShapeMode_ShouldKeepExistingPropertyNamePolicy()
    {
        // JSON mode reads the member name only: an unaliased computed scalar has no JSON member name and
        // must not acquire one from OutputName. This is the "existing policy, unchanged" first rule.
        var named = new SelectExpression(typeof(int))
        {
            Index = 0,
            PropertyName = "Id",
            Expression = Expression.Parameter(typeof(int), "Id"),
        };
        var computed = new SelectExpression(typeof(int))
        {
            Index = 0,
            OutputName = "c0",
            Expression = Expression.Multiply(Expression.Parameter(typeof(int), "x"), Expression.Constant(2)),
        };

        var namedPlan = JsonShapePlan.Build([named], false, new JsonStreamOptions());
        namedPlan.Columns[0].Name.Should().Be("Id");

        var scalarPlan = JsonShapePlan.Build([computed], true, new JsonStreamOptions());
        scalarPlan.Columns[0].Name.Should().BeEmpty("a scalar JSON document has no object member name");
    }

    [Fact]
    public void D1_InMemory_ShouldStillEvaluateDerivedScalarClientSide()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ISimpleEntity>().WithData([new SimpleEntity { Id = 1 }, new SimpleEntity { Id = 2 }]);

        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var rows = inner.ToList();

        // Compatibility control: the in-memory provider never renders SQL and evaluates the derived
        // scalar client-side; the boundary bit must not change that path.
        rows.Should().Equal([2, 4], "the in-memory provider evaluates the derived scalar without the SQL boundary bit");
    }

    [Fact]
    public void D1_InMemory_ShouldRefuseDataDefinitionWithTheDefinedMessage()
    {
        using var ctx = new InMemoryDataContext();
        ctx.From<ISimpleEntity>().WithData([new SimpleEntity { Id = 1 }]);
        var inner = ctx.From<ISimpleEntity>().Where(s => s.Id <= 2).Select(s => s.Id * 2);
        var outer = ctx.From(inner).Select(x => x * 3);

        FluentActions.Invoking(() => outer.ToTableSql("t312_ctas"))
            .Should().Throw<NotSupportedException>()
            .WithMessage("*does not support data definition*");
    }
}
