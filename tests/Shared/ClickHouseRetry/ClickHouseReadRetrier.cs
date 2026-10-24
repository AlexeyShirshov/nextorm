using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// One sanitised attempt record for the accounting stream (T328 AC03). It carries identities and a
/// short reason token — never a message body, query or connection string.
/// </summary>
internal readonly record struct ClickHouseRetryAttemptRecord(
    string TestIdentity,
    string OperationIdentity,
    int Attempt,
    int AttemptLimit,
    string ReasonCode,
    bool ScheduledRetry,
    bool Succeeded = false)
{
    /// <summary>The attempt that a scheduled retry will start, or <see langword="null"/> when no
    /// additional attempt was scheduled. It completes AC03's tuple on the record itself.</summary>
    public int? NextAttempt => ScheduledRetry ? Attempt + 1 : null;
}

/// <summary>
/// The two-attempt lifecycle for an explicitly audited, wholly read-only ClickHouse operation
/// (T328 AC02/AC10, V01–V04/V08–V13). It is instance-based on purpose: there is no static or ambient
/// retry policy, no assembly hook and no provider-wide interception — a call site opts in by using an
/// instance, which D4 wires explicitly.
/// <para>
/// Semantics: attempt 1 is awaited; on a retryable classification (and only after the failed task has
/// demonstrably terminated) one record is emitted and the retrier waits a cancellable one-second
/// backoff; attempt 2 is then started once. A second failure is recorded and rethrown, never repeated.
/// The retrier never wraps, transforms or disposes the caller's result: the value of <c>T</c> is
/// returned unchanged (V13).
/// </para>
/// </summary>
internal sealed class ClickHouseReadRetrier
{
    /// <summary>Total attempts per operation, including the first one.</summary>
    internal const int AttemptLimit = 2;

    /// <summary>The single backoff before the only additional attempt.</summary>
    internal static readonly TimeSpan BackoffDelay = TimeSpan.FromSeconds(1);

    /// <summary>Reason token for a backoff interrupted by the caller's cancellation token.</summary>
    internal const string BackoffCancelledReason = "backoff_cancelled";

    /// <summary>Reason token of an attempt outcome that returned successfully.</summary>
    internal const string SucceededReasonCode = "attempt_succeeded";

    private readonly Action<ClickHouseRetryAttemptRecord> _sink;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly ClickHouseRetryLedger? _ledger;

    /// <param name="sink">Receives one record per observed attempt outcome; per instance, never shared.</param>
    /// <param name="delay">The backoff implementation; defaults to <see cref="Task.Delay(TimeSpan, CancellationToken)"/>
    /// so a test can observe and control timing without sleeping.</param>
    /// <param name="ledger">Optional accounting ledger. When supplied it enforces the one
    /// retry-controlled operation per test invocation before any operation runs; it never decides
    /// whether a retry happens.</param>
    internal ClickHouseReadRetrier(
        Action<ClickHouseRetryAttemptRecord> sink,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        ClickHouseRetryLedger? ledger = null)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
        _delay = delay ?? Task.Delay;
        _ledger = ledger;
    }

    /// <summary>Executes <paramref name="read"/> at most twice, retrying only a positive
    /// client-timeout/connection-reset classification.</summary>
    internal async Task<T> ExecuteAsync<T>(
        ClickHouseReadOperation operation,
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        ValidateOperation(operation);
        ArgumentNullException.ThrowIfNull(read);
        ClaimOperation(operation);

        var firstAttempt = StartAttempt(read, cancellationToken);

        Exception firstFailure;
        try
        {
            return await firstAttempt.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstFailure = exception;
        }

        EnsureAttemptTerminated(firstAttempt);

        var classification = ClickHouseTransientClassifier.Classify(firstFailure, cancellationToken);
        if (!classification.IsRetryable)
            return Rethrow<T>(firstFailure);

        // The scheduled record exists before the delay, so a recovery can never look like a
        // first-attempt success.
        Emit(operation, attempt: 1, classification.ReasonCode, scheduledRetry: true);

        try
        {
            await _delay(BackoffDelay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The scheduled record already announced the retry; this distinct token records that the
            // backoff was interrupted, and no second attempt is started.
            Emit(operation, attempt: 1, BackoffCancelledReason, scheduledRetry: false);
            throw;
        }

        var secondAttempt = StartAttempt(read, cancellationToken);

        Exception secondFailure;
        try
        {
            var secondResult = await secondAttempt.ConfigureAwait(false);

            // Recovery is an observed success outcome, not the absence of a second failure: the
            // ledger can therefore never infer it.
            Emit(operation, attempt: 2, SucceededReasonCode, scheduledRetry: false, succeeded: true);
            return secondResult;
        }
        catch (Exception exception)
        {
            secondFailure = exception;
        }

        EnsureAttemptTerminated(secondAttempt);

        var secondClassification = ClickHouseTransientClassifier.Classify(secondFailure, cancellationToken);
        Emit(operation, attempt: 2, secondClassification.ReasonCode, scheduledRetry: false);
        return Rethrow<T>(secondFailure);
    }

    /// <summary>
    /// The pre-retry quiescence guard (V10): a second attempt must not start while the failed attempt
    /// is still running. Through <see cref="ExecuteAsync{T}"/> the awaited task is always complete
    /// when this runs; the guard is exposed so the invariant can be unit-tested directly rather than
    /// faked through an unreachable <c>await</c> path.
    /// </summary>
    /// <exception cref="InvalidOperationException">The attempt task has not completed.</exception>
    internal static void EnsureAttemptTerminated(Task attemptTask)
    {
        ArgumentNullException.ThrowIfNull(attemptTask);

        if (!attemptTask.IsCompleted)
        {
            throw new InvalidOperationException(
                "T328: the failed ClickHouse read attempt has not terminated; quiescence is unproven, so no additional attempt is started.");
        }
    }

    private void Emit(ClickHouseReadOperation operation, int attempt, string reasonCode, bool scheduledRetry, bool succeeded = false)
    {
        _sink(new ClickHouseRetryAttemptRecord(
            operation.TestIdentity,
            operation.OperationIdentity,
            attempt,
            AttemptLimit,
            reasonCode,
            scheduledRetry,
            succeeded));
    }

    /// <summary>
    /// Enforces decision 4 before the first attempt: a second retry-controlled operation for the
    /// same opted-in test invocation is a programming error, and it is rejected before any
    /// operation runs.
    /// </summary>
    private void ClaimOperation(ClickHouseReadOperation operation)
    {
        if (_ledger is null)
            return;

        if (!_ledger.TryClaimOperation(operation.TestIdentity, operation.OperationIdentity))
        {
            throw new InvalidOperationException(
                $"T328: test '{operation.TestIdentity}' already claimed a retry-controlled ClickHouse operation; " +
                "only one retry-controlled operation is permitted per test invocation.");
        }
    }

    /// <summary>
    /// Starts one attempt. A delegate that throws synchronously never produces an attempt task, so
    /// quiescence cannot be observed and the failure is rethrown without a retry — ClickHouse's async
    /// driver APIs return tasks, so this is a misuse guard rather than an expected failure shape.
    /// </summary>
    private static Task<T> StartAttempt<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken)
    {
        Task<T>? attempt = null;

        try
        {
            attempt = read(cancellationToken);
        }
        catch (Exception synchronousFailure)
        {
            Rethrow<T>(synchronousFailure);
        }

        if (attempt is null)
        {
            throw new InvalidOperationException(
                "T328: the ClickHouse read delegate returned no task; the attempt cannot be observed, so it is never retried.");
        }

        return attempt;
    }

    private static void ValidateOperation(ClickHouseReadOperation operation)
    {
        if (string.IsNullOrWhiteSpace(operation.TestIdentity) || string.IsNullOrWhiteSpace(operation.OperationIdentity))
        {
            throw new ArgumentException(
                "The retried operation must be created through ClickHouseReadOperation.Audited(...).",
                nameof(operation));
        }
    }

    /// <summary>Rethrows the original exception, preserving its stack trace.</summary>
    [DoesNotReturn]
    private static T Rethrow<T>(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
        throw new UnreachableException("T328: ExceptionDispatchInfo.Throw returned; this is unreachable.");
    }
}
