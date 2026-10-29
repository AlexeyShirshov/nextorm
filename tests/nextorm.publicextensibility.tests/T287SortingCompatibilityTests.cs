using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.PublicExtensibility.Tests;

/// <summary>
/// A custom dialect written against the <b>old</b> public extension surface: it derives from
/// <see cref="SqlDialectBase"/> through this assembly (no <c>InternalsVisibleTo</c> grant) and does not
/// mention the T287 members, so it proves the new capability and renderer arrive as default interface
/// implementations and add no abstract obligation to an external implementer.
/// </summary>
public sealed class T287LegacyDialect : SqlDialectBase
{
    /// <inheritdoc/>
    public override string MakeParam(string name) => "@" + name;

    /// <inheritdoc/>
    public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
    {
    }
}

/// <summary>
/// T287 (issue #287): source-level compatibility of the additive NULL-placement API and the default
/// dialect behavior, asserted from a non-friend consumer assembly.
/// </summary>
public class T287SortingCompatibilityTests
{
    // ---- existing constructors and properties keep working ---------------------------------

    [Fact]
    public void T287_OldConstructors_ShouldStillCompileAndYieldDefaultPlacement()
    {
        var fromExpression = new Sorting(Expression.Constant(1));
        var fromIndex = new Sorting(3);

        fromExpression.NullsPosition.Should().Be(NullsPosition.Default);
        fromIndex.NullsPosition.Should().Be(NullsPosition.Default);

        fromExpression.SortExpression.Should().NotBeNull();
        fromIndex.ColumnIndex.Should().Be(3);

        fromExpression.Direction = OrderDirection.Desc;
        fromExpression.Direction.Should().Be(OrderDirection.Desc);

        fromExpression.PreparedExpression = Expression.Constant(2);
        fromExpression.PreparedExpression.Should().NotBeNull();
    }

    [Fact]
    public void T287_OldConstructorSignatures_ShouldStillExist()
    {
        var constructors = typeof(Sorting).GetConstructors();

        constructors.Should().Contain(c =>
            c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(Expression));
        constructors.Should().Contain(c =>
            c.GetParameters().Length == 1 && c.GetParameters()[0].ParameterType == typeof(int));
    }

    [Fact]
    public void T287_NewConstructorsAndProperty_ShouldCarryPlacement()
    {
        var fromExpression = new Sorting(Expression.Constant(1), NullsPosition.First);
        var fromIndex = new Sorting(2, NullsPosition.Last);

        fromExpression.NullsPosition.Should().Be(NullsPosition.First);
        fromIndex.NullsPosition.Should().Be(NullsPosition.Last);

        fromIndex.NullsPosition = NullsPosition.Default;
        fromIndex.NullsPosition.Should().Be(NullsPosition.Default);

        FluentActions.Invoking(() => fromIndex.NullsPosition = (NullsPosition)99)
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");
    }

    [Fact]
    public void T287_EnumValues_ShouldBeStable()
    {
        ((int)NullsPosition.Default).Should().Be(0);
        ((int)NullsPosition.First).Should().Be(1);
        ((int)NullsPosition.Last).Should().Be(2);
    }

    // ---- an external dialect gains the default capability and CASE renderer ----------------

    [Fact]
    public void T287_ExternalDialect_ShouldDefaultToNoNativeSupport()
    {
        new T287LegacyDialect().SupportsNullsOrdering.Should().BeFalse(
            "a dialect compiled against the old surface acquires no new abstract obligation and keeps the portable emulation");
    }

    [Theory]
    [InlineData(OrderDirection.Asc, NullsPosition.First, "CASE WHEN t.k IS NULL THEN 0 ELSE 1 END ASC, t.k")]
    [InlineData(OrderDirection.Asc, NullsPosition.Last, "CASE WHEN t.k IS NULL THEN 1 ELSE 0 END ASC, t.k")]
    [InlineData(OrderDirection.Desc, NullsPosition.First, "CASE WHEN t.k IS NULL THEN 0 ELSE 1 END ASC, t.k desc")]
    [InlineData(OrderDirection.Desc, NullsPosition.Last, "CASE WHEN t.k IS NULL THEN 1 ELSE 0 END ASC, t.k desc")]
    public void T287_DefaultRenderer_ShouldProduceTheCaseOracle(OrderDirection direction, NullsPosition placement, string expected)
    {
        new T287LegacyDialect().RenderOrderBy("t.k", direction, placement).Should().Be(expected);
    }

    [Fact]
    public void T287_DefaultRenderer_ShouldRejectDefaultPlacement()
    {
        FluentActions.Invoking(() => new T287LegacyDialect().RenderOrderBy("t.k", OrderDirection.Asc, NullsPosition.Default))
            .Should().Throw<ArgumentOutOfRangeException>().WithParameterName("nullsPosition");
    }

    // ---- the non-generic builder keeps no ordering surface ---------------------------------

    [Fact]
    public void T287_NonGenericEntityBuilder_ShouldHaveNoOrderingSurface()
    {
        var orderingMethods = typeof(EntityBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .Where(n => n is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending")
            .ToArray();

        orderingMethods.Should().BeEmpty("the non-generic builder is not the generic builder's base and exposes no ordering API");
    }

    [Fact]
    public void T287_GenericEntityBuilder_ShouldExposeTheAdditiveFamilyDeclaredOnItself()
    {
        var names = typeof(EntityBuilder<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(m => m.Name)
            .ToArray();

        names.Should().Contain("ThenBy");
        names.Should().Contain("ThenByDescending");
    }
}
