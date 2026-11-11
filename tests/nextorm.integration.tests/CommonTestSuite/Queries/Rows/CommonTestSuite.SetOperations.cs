using System.Linq.Expressions;
using FluentAssertions;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    [Fact]
    public void Union_Selector_ShouldDeduplicateToTen()
    {
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Union<IComplexEntity>(it => (int)it.Id);

        _sut.From(cmd).Count().Should().Be(10);
    }

    [Fact]
    public void UnionAll_Selector_ShouldKeepThirteen()
    {
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .UnionAll<IComplexEntity>(it => (int)it.Id);

        _sut.From(cmd).Count().Should().Be(13);
    }

    [Fact]
    public void Intersect_ShouldReturnOnlyCommonRows()
    {
        // simple_entity ids are 1..10, complex_entity ids are 1..3, so only 1, 2 and 3 are common.
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Intersect(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(3);
    }

    [Fact]
    public void Except_ShouldReturnOnlyLeftRows()
    {
        // The left side is 1..10; removing the right side leaves 4..10.
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(7);
    }

    [Fact]
    public void SetOperations_WhenChained_ShouldApplyLeftToRight()
    {
        // (simple EXCEPT complex) INTERSECT simple = {4..10} INTERSECT {1..10} = {4..10}.
        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.ComplexEntity.Select(it => (int)it.Id))
            .Intersect(_sut.SimpleEntity.Select(it => it.Id));

        _sut.From(cmd).Count().Should().Be(7);
    }

    [Fact]
    public void SetOperations_SelectorChain_ShouldMatchExplicitForm()
    {
        // Parity check (AC2-5/AC2-6): the selector-overload chain must produce the same result as the
        // identical chain written in the explicit form on the same provider. The expected row count is
        // NOT hardcoded, because unparenthesized SQL set operations are not uniformly left-associative
        // across providers (INTERSECT binds tighter than UNION/EXCEPT on PostgreSQL/SQL Server/MySQL),
        // so a fixed number would only be valid for a subset of providers. Parity with the explicit
        // form is the contract; the provider's own SQL precedence applies to both identically.
        var selectorCmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except<IComplexEntity>(it => (int)it.Id)
            .Union<IComplexEntity>(it => (int)it.Id)
            .Intersect<IComplexEntity>(it => (int)it.Id);

        var explicitCmd = _sut.SimpleEntity.Select(it => it.Id)
            .Except(_sut.ComplexEntity.Select(it => (int)it.Id))
            .Union(_sut.ComplexEntity.Select(it => (int)it.Id))
            .Intersect(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(selectorCmd).Count().Should().Be(_sut.From(explicitCmd).Count());
    }

    [Fact]
    public void IntersectAll_WhenSupported_ShouldReturnCommonRows()
    {
        Assert.SkipUnless(Provider.SupportsIntersectExceptAll,
            "This provider does not support INTERSECT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .IntersectAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(3);
    }

    [Fact]
    public void ExceptAll_WhenSupported_ShouldReturnOnlyLeftRows()
    {
        Assert.SkipUnless(Provider.SupportsIntersectExceptAll,
            "This provider does not support EXCEPT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .ExceptAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        _sut.From(cmd).Count().Should().Be(7);
    }

    [Fact]
    public void IntersectAll_WhenUnsupported_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsIntersectExceptAll,
            "This provider supports INTERSECT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .IntersectAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        var act = () => _sut.From(cmd).Count();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ExceptAll_WhenUnsupported_ShouldThrow()
    {
        Assert.SkipUnless(!Provider.SupportsIntersectExceptAll,
            "This provider supports EXCEPT ALL.");

        var cmd = _sut.SimpleEntity.Select(it => it.Id)
            .ExceptAll(_sut.ComplexEntity.Select(it => (int)it.Id));

        var act = () => _sut.From(cmd).Count();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SetOperations_Selector_ShapeMismatch_FailsAtExecution()
    {
        // O-AC6 Branch A: the left operand projects one column (partial MemberInit) and the right
        // operand projects two columns (fuller MemberInit) for the same TResult. The selector overload
        // must behave exactly like the explicit form: preparation succeeds (no column-count validation
        // in QueryPreparer) and the database rejects the mismatched UNION at execution, so the stage
        // and the exception are identical on both forms on every provider. That parity is the
        // criterion; SQLite additionally pins the shared outcome to its execution exception.
        var left = _sut.ComplexEntity.Select(x => new SetOpShapeDto { Id = (int)x.Id });
        Expression<Func<IComplexEntity, SetOpShapeDto>> selector =
            it => new SetOpShapeDto { Id = (int)it.Id, Name = it.String };

        var selectorCmd = left.Union(selector);
        var explicitCmd = left.Union(_sut.ComplexEntity.Select(selector));

        // Preparation succeeds on both forms: the shape mismatch survives preparation.
        selectorCmd.PrepareCommand(false, CancellationToken.None);
        explicitCmd.PrepareCommand(false, CancellationToken.None);

        var selectorOutcome = CaptureSetOpOutcome(() => _sut.From(selectorCmd).Count());
        var explicitOutcome = CaptureSetOpOutcome(() => _sut.From(explicitCmd).Count());

        // Parity invariant: the same rows on success, otherwise the same exception type and message.
        explicitOutcome.Rows.Should().Be(selectorOutcome.Rows);
        explicitOutcome.ExceptionType.Should().Be(selectorOutcome.ExceptionType);
        explicitOutcome.ExceptionMessage.Should().Be(selectorOutcome.ExceptionMessage);

        // On SQLite the shared outcome is pinned to the database rejecting the UNION at execution.
        if (Provider.Name == "sqlite")
        {
            selectorOutcome.ExceptionType.Should()
                .Be(typeof(Microsoft.Data.Sqlite.SqliteException).FullName,
                    "the mismatched UNION must fail at execution, where the driver surfaces it");
        }

        static SetOpOutcome CaptureSetOpOutcome(Func<int> act)
        {
            try
            {
                return new SetOpOutcome(act(), null, null);
            }
            catch (Exception ex)
            {
                return new SetOpOutcome(null, ex.GetType().FullName, ex.Message);
            }
        }
    }

    private readonly record struct SetOpOutcome(int? Rows, string? ExceptionType, string? ExceptionMessage);
}

/// <summary>
/// Two-property DTO used by <c>SetOperations_Selector_ShapeMismatch_FailsAtExecution</c>:
/// a partial initializer projects one column while a fuller one projects two for the same TResult.
/// </summary>
public sealed class SetOpShapeDto
{
    public int Id { get; set; }

    public string? Name { get; set; }
}
