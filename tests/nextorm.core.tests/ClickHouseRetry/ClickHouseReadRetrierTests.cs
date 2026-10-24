using FluentAssertions;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Core.Tests.ClickHouseRetry;

/// <summary>
/// Two-attempt lifecycle contract (T328 AC02/AC03/AC10, V03/V04/V06/V08/V10/V13). The retrier must
/// retry at most once, only after a positively established client transport failure, with a
/// cancellable one-second backoff, and must never transform or dispose the caller's result.
/// </summary>
public sealed class ClickHouseReadRetrierTests
{
    private static ClickHouseReadOperation Operation =>
        ClickHouseReadOperation.Audited("test.identity", "operation.identity", ClickHouseReadCategory.PointRead);

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheFirstResult_WithoutRecordingOrDelaying()
    {
        var records = new List<ClickHouseRetryAttemptRecord>();
        var delays = new List<TimeSpan>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (backoff, _) =>
        {
            delays.Add(backoff);
            return Task.CompletedTask;
        });

        var result = await retrier.ExecuteAsync(
            Operation,
            _ =>
            {
                invocations++;
                return Task.FromResult("payload");
            },
            CancellationToken.None);

        result.Should().Be("payload");
        invocations.Should().Be(1);
        records.Should().BeEmpty();
        delays.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRecover_AfterExactlyOneScheduledBackoff()
    {
        var records = new List<ClickHouseRetryAttemptRecord>();
        var delays = new List<TimeSpan>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (backoff, _) =>
        {
            delays.Add(backoff);
            return Task.CompletedTask;
        });

        var result = await retrier.ExecuteAsync(
            Operation,
            _ =>
            {
                invocations++;
                return invocations == 1
                    ? Task.FromException<string>(DriverTimeoutChain())
                    : Task.FromResult("recovered");
            },
            CancellationToken.None);

        result.Should().Be("recovered");
        invocations.Should().Be(2);
        delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromSeconds(1));

        records.Should().HaveCount(2);
        var record = records[0];
        record.TestIdentity.Should().Be("test.identity");
        record.OperationIdentity.Should().Be("operation.identity");
        record.Attempt.Should().Be(1);
        record.AttemptLimit.Should().Be(2);
        record.ReasonCode.Should().Be("client_http_timeout");
        record.ScheduledRetry.Should().BeTrue();
        record.NextAttempt.Should().Be(2);

        var recovered = records[1];
        recovered.Attempt.Should().Be(2);
        recovered.Succeeded.Should().BeTrue();
        recovered.ScheduledRetry.Should().BeFalse();
        recovered.NextAttempt.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrowTheSecondFailure_AfterExactlyTwoAttempts()
    {
        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var firstFailure = DriverTimeoutChain();
        var secondFailure = DriverTimeoutChain();
        var failures = new Queue<Exception>(new Exception[] { firstFailure, secondFailure });
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) => Task.CompletedTask);

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation,
            _ =>
            {
                invocations++;
                return Task.FromException<string>(failures.Dequeue());
            },
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<TaskCanceledException>();

        thrown.Which.Should().BeSameAs(secondFailure);
        invocations.Should().Be(2);
        records.Should().HaveCount(2);
        records[0].Attempt.Should().Be(1);
        records[0].ScheduledRetry.Should().BeTrue();
        records[0].ReasonCode.Should().Be("client_http_timeout");
        records[1].Attempt.Should().Be(2);
        records[1].AttemptLimit.Should().Be(2);
        records[1].ScheduledRetry.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRetry_WhenTheCallerTokenIsAlreadyCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) =>
            throw new InvalidOperationException("the backoff must not run for a non-retryable failure"));

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation,
            _ =>
            {
                invocations++;
                return Task.FromException<string>(new TaskCanceledException());
            },
            cancellation.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();

        invocations.Should().Be(1);
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRetry_WhenTheTokenIsCancelledDuringTheOperation()
    {
        using var cancellation = new CancellationTokenSource();
        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) =>
            throw new InvalidOperationException("the backoff must not run for a caller cancellation"));

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation,
            _ =>
            {
                invocations++;
                cancellation.Cancel();
                return Task.FromException<string>(new TaskCanceledException());
            },
            cancellation.Token);

        await act.Should().ThrowAsync<TaskCanceledException>();

        invocations.Should().Be(1);
        records.Should().BeEmpty();
        ClickHouseTransientClassifier.Classify(new TaskCanceledException(), cancellation.Token)
            .Kind.Should().Be(ClickHouseFailureKind.CallerCancelled);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotStartASecondAttempt_WhenTheBackoffIsCancelled()
    {
        var records = new List<ClickHouseRetryAttemptRecord>();
        var delays = new List<TimeSpan>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (backoff, _) =>
        {
            delays.Add(backoff);
            return Task.FromException(new OperationCanceledException("cancelled during backoff"));
        });

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation,
            _ =>
            {
                invocations++;
                return Task.FromException<string>(DriverTimeoutChain());
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();

        invocations.Should().Be(1);
        delays.Should().ContainSingle().Which.Should().Be(TimeSpan.FromSeconds(1));
        records.Should().HaveCount(2);
        records[0].ScheduledRetry.Should().BeTrue();
        records[1].Attempt.Should().Be(1);
        records[1].ScheduledRetry.Should().BeFalse();
        records[1].ReasonCode.Should().Be(ClickHouseReadRetrier.BackoffCancelledReason);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRethrowARealAssertionFailure_AfterExactlyOneInvocation()
    {
        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var assertionFailure = CaptureFluentAssertionsFailure();
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) =>
            throw new InvalidOperationException("the backoff must not run for an assertion failure"));

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation,
            _ =>
            {
                invocations++;
                return Task.FromException<string>(assertionFailure);
            },
            CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<Exception>();

        thrown.Which.Should().BeSameAs(assertionFailure);
        invocations.Should().Be(1);
        records.Should().BeEmpty();
    }

    [Fact]
    public void EnsureAttemptTerminated_ShouldThrow_WhenTheAttemptTaskIsStillPending()
    {
        // The pending task is never completed: the guard must reject it rather than allow a second
        // attempt. Through ExecuteAsync the awaited task is always complete, so this is the honest
        // way to exercise the pre-retry invariant (V10); the through-await path is unreachable.
        var pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        FluentActions.Invoking(() => ClickHouseReadRetrier.EnsureAttemptTerminated(pending.Task))
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*quiescence*");
    }

    [Fact]
    public void EnsureAttemptTerminated_ShouldAcceptATerminatedAttempt()
    {
        FluentActions.Invoking(() => ClickHouseReadRetrier.EnsureAttemptTerminated(Task.CompletedTask))
            .Should()
            .NotThrow();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotRetry_WhenTheReadDelegateReturnsNoTask()
    {
        var records = new List<ClickHouseRetryAttemptRecord>();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(records.Add, (_, _) => Task.CompletedTask);

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation,
            _ =>
            {
                invocations++;
                return null!;
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        invocations.Should().Be(1);
        records.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveEveryResultShape_OnTheFirstAttempt()
    {
        (await ExecuteOnce<string?>(null)).Should().BeNull();

        var instance = new object();
        (await ExecuteOnce(instance)).Should().BeSameAs(instance);

        (await ExecuteOnce(default(DateTime))).Should().Be(default(DateTime));

        var empty = new List<int>();
        (await ExecuteOnce(empty)).Should().BeSameAs(empty);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveEveryResultShape_WhenTheSecondAttemptRecovers()
    {
        (await ExecuteRecovering<string?>(null)).Should().BeNull();

        var instance = new object();
        (await ExecuteRecovering(instance)).Should().BeSameAs(instance);

        (await ExecuteRecovering(default(DateTime))).Should().Be(default(DateTime));

        var empty = new List<int>();
        (await ExecuteRecovering(empty)).Should().BeSameAs(empty);
    }

    [Fact]
    public async Task Retriers_ShouldNotShareSinkOrState()
    {
        var recordsA = new List<ClickHouseRetryAttemptRecord>();
        var recordsB = new List<ClickHouseRetryAttemptRecord>();
        var retrierA = new ClickHouseReadRetrier(recordsA.Add, (_, _) => Task.CompletedTask);
        var retrierB = new ClickHouseReadRetrier(recordsB.Add, (_, _) => Task.CompletedTask);

        var invocationsA = 0;
        await retrierA.ExecuteAsync(
            Operation,
            _ =>
            {
                invocationsA++;
                return invocationsA == 1
                    ? Task.FromException<string>(DriverTimeoutChain())
                    : Task.FromResult("recovered");
            },
            CancellationToken.None);

        await retrierB.ExecuteAsync(Operation, _ => Task.FromResult("ok"), CancellationToken.None);

        recordsA.Should().HaveCount(2);
        recordsA[0].ScheduledRetry.Should().BeTrue();
        recordsA[1].Succeeded.Should().BeTrue();
        recordsB.Should().BeEmpty();
    }

    private static async Task<T> ExecuteOnce<T>(T value)
    {
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(_ => { }, (_, _) => Task.CompletedTask);

        return await retrier.ExecuteAsync(
            Operation,
            _ =>
            {
                invocations++;
                return Task.FromResult(value);
            },
            CancellationToken.None);
    }

    private static async Task<T> ExecuteRecovering<T>(T value)
    {
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(_ => { }, (_, _) => Task.CompletedTask);

        return await retrier.ExecuteAsync(
            Operation,
            _ =>
            {
                invocations++;
                return invocations == 1
                    ? Task.FromException<T>(DriverTimeoutChain())
                    : Task.FromResult(value);
            },
            CancellationToken.None);
    }

    private static TaskCanceledException DriverTimeoutChain() =>
        new(
            "The request was canceled due to the configured HttpClient.Timeout of 120 seconds elapsing.",
            new TimeoutException(
                "The request was canceled due to the configured HttpClient.Timeout of 120 seconds elapsing.",
                new TaskCanceledException("A task was canceled.")));

    private static Exception CaptureFluentAssertionsFailure()
    {
        try
        {
            FluentActions.Invoking(() => 1.Should().Be(2)).Invoke();
        }
        catch (Exception failure)
        {
            return failure;
        }

        throw new InvalidOperationException("FluentAssertions did not throw, so the assertion case cannot be exercised.");
    }
}
