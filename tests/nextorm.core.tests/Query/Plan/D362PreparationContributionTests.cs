using System.Linq.Expressions;
using FluentAssertions;
using NextORM.Core;
using Xunit;

namespace NextORM.Core.Tests;

/// <summary>Smoke tests for the additive public preparation-contribution types (D362 unit 1).</summary>
public class D362PreparationContributionTests
{
    [Fact]
    public void QueryPreparationContribution_Construction_RoundTripsAllFields()
    {
        var param = Expression.Parameter(typeof(int), "x");
        var lambda = Expression.Lambda(param, param);
        var having = Expression.Lambda(Expression.Constant(true), param);
        var sourcePred = Expression.Lambda(Expression.Constant(true), param);

        var contribution = new QueryPreparationContribution(
            new[] { lambda },
            QueryPreparationHavingMode.Replace,
            having,
            sourcePred,
            requireDistinct: true);

        contribution.GroupingKeys.Should().HaveCount(1);
        contribution.HavingMode.Should().Be(QueryPreparationHavingMode.Replace);
        contribution.Having.Should().BeSameAs(having);
        contribution.SourcePredicate.Should().BeSameAs(sourcePred);
        contribution.RequireDistinct.Should().BeTrue();
    }

    [Fact]
    public void QueryPreparationContribution_EmptyKeys_DenotesGlobalAggregation()
    {
        var contribution = new QueryPreparationContribution(
            Array.Empty<LambdaExpression>(),
            QueryPreparationHavingMode.Unchanged,
            null,
            null,
            requireDistinct: false);

        contribution.GroupingKeys.Should().BeEmpty();
        contribution.HavingMode.Should().Be(QueryPreparationHavingMode.Unchanged);
    }
}
