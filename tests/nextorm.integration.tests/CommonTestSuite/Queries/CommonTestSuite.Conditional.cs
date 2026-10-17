using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Conditional_ShouldReturnIfTrueOrIfFalse()
    {
        var values = _sut.ComplexEntity
            .Select(e => e.Id > 1 ? 10 : 20)
            .ToList();

        // complex_entity holds ids 1, 2 and 3, so the condition is false once and true twice.
        values.OrderBy(x => x).Should().Equal(10, 10, 20);
    }

    [Fact]
    public void Conditional_ShouldReturnColumnValue()
    {
        var value = _sut.ComplexEntity
            .Where(e => e.Id == 2)
            .Select(e => new { V = e.Int == null ? -1 : e.Int })
            .First();

        value.V.Should().Be(1);
    }

    [Fact]
    public void Conditional_InWhere_ShouldFilterRows()
    {
        var cnt = _sut.ComplexEntity
            .Where(e => (e.Int == null ? 0 : e.Int) == 1)
            .Count();

        // id 1 has a null nullableint, ids 2 and 3 have 1, so two rows match.
        cnt.Should().Be(2);
    }

    [Fact]
    public void SwitchExpression_ShouldReturnMappedValues()
    {
        var parameter = Expression.Parameter(typeof(IComplexEntity), "e");
        var body = Expression.Switch(
            Expression.Property(parameter, nameof(IComplexEntity.Id)),
            Expression.Constant(100L),
            Expression.SwitchCase(Expression.Constant(10L), Expression.Constant(1L)),
            Expression.SwitchCase(Expression.Constant(20L), Expression.Constant(2L)));
        var exp = Expression.Lambda<Func<IComplexEntity, long>>(body, parameter);

        var values = _sut.ComplexEntity.Select(exp).ToList();

        values.OrderBy(x => x).Should().Equal(10L, 20L, 100L);
    }
}
