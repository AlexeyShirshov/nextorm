using FluentAssertions;

namespace NextORM.Core.Tests;

/// <summary>
/// D2 unit contract of <see cref="BulkInsertDeadline"/>: one monotonic budget, no per-batch reset, a
/// positive budget only, and the caller's cancellation taking priority over an exhausted deadline.
/// </summary>
public partial class BulkInsertBuilderTests
{
    [Fact]
    public void Deadline_ShouldExpireOnceTheSingleBudgetIsSpent()
    {
        using var deadline = BulkInsertDeadline.Start(TimeSpan.FromMilliseconds(40), CancellationToken.None);

        deadline.IsExpired.Should().BeFalse();
        deadline.Remaining.Should().BePositive();

        Thread.Sleep(120);

        deadline.IsExpired.Should().BeTrue();
        deadline.Remaining.Should().BeLessThanOrEqualTo(TimeSpan.Zero);
        FluentActions.Invoking(deadline.ThrowIfCancelled).Should().Throw<TimeoutException>();
    }

    [Fact]
    public void Deadline_WithoutTimeout_ShouldNeverExpire()
    {
        using var cancellation = new CancellationTokenSource();
        using var deadline = BulkInsertDeadline.Start(timeout: null, cancellation.Token);

        deadline.Token.Should().Be(cancellation.Token);
        deadline.Remaining.Should().BeNull();
        deadline.IsExpired.Should().BeFalse();
        FluentActions.Invoking(deadline.ThrowIfCancelled).Should().NotThrow();
    }

    [Fact]
    public void Deadline_UserCancellation_ShouldWinOverAnExhaustedBudget()
    {
        using var cancellation = new CancellationTokenSource();
        using var deadline = BulkInsertDeadline.Start(TimeSpan.FromMilliseconds(1), cancellation.Token);
        cancellation.Cancel();
        Thread.Sleep(30);

        var act = () => deadline.ThrowIfCancelled();

        var thrown = act.Should().Throw<OperationCanceledException>().Which;
        thrown.CancellationToken.Should().Be(cancellation.Token);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Deadline_NonPositiveBudget_ShouldBeRejected(int milliseconds)
    {
        var act = () => BulkInsertDeadline.Start(TimeSpan.FromMilliseconds(milliseconds), CancellationToken.None);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Deadline_Token_ShouldBeCancelledWhenTheBudgetExpires()
    {
        using var deadline = BulkInsertDeadline.Start(TimeSpan.FromMilliseconds(20), CancellationToken.None);

        var act = () => Task.Delay(Timeout.InfiniteTimeSpan, deadline.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        deadline.IsExpired.Should().BeTrue();
    }
}
