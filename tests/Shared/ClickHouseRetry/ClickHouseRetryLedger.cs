using System.Collections.Concurrent;

namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// Process-wide accounting for retry-controlled ClickHouse test operations (T328 AC03/AC05, V08/V14).
/// It is deliberately a ledger only: it never decides whether a retry happens, never runs an
/// operation and never exposes an ambient policy. A call site (D4) passes one instance to the
/// retrier explicitly, and the thin result reporter reads the same instance for the final summary.
/// </summary>
/// <remarks>
/// Thread-safe: test states are kept in a <see cref="ConcurrentDictionary{TKey,TValue}"/> and every
/// counter/flag is updated with <see cref="Interlocked"/>, so parallel test completions never share
/// mutable state and no lock covers the recording path.
/// </remarks>
internal sealed class ClickHouseRetryLedger
{
    /// <summary>The ledger the integration suite and its reporter share. Accounting only.</summary>
    internal static ClickHouseRetryLedger Default { get; } = new();

    private readonly ConcurrentDictionary<string, TestState> _tests = new(StringComparer.Ordinal);

    /// <summary>
    /// Claims the single retry-controlled operation allowed for one opted-in test invocation
    /// (T328 decision 4). The second claim for the same test identity is rejected before any
    /// operation runs.
    /// </summary>
    /// <returns><see langword="true"/> for the first claim of <paramref name="testIdentity"/>;
    /// <see langword="false"/> when a retry-controlled operation was already claimed.</returns>
    internal bool TryClaimOperation(string testIdentity, string operationIdentity)
    {
        if (string.IsNullOrWhiteSpace(testIdentity))
            throw new ArgumentException("A test identity is required to claim a retry-controlled operation.", nameof(testIdentity));
        if (string.IsNullOrWhiteSpace(operationIdentity))
            throw new ArgumentException("An operation identity is required to claim a retry-controlled operation.", nameof(operationIdentity));

        var state = _tests.GetOrAdd(testIdentity, static identity => new TestState(identity));
        return state.TryClaimOperation(operationIdentity);
    }

    /// <summary>Applies one sanitised attempt outcome to the owning test state.</summary>
    internal void Record(in ClickHouseRetryAttemptRecord record)
    {
        if (string.IsNullOrWhiteSpace(record.TestIdentity))
            throw new ArgumentException("The attempt record carries no test identity.", nameof(record));
        if (record.Attempt < 1)
            throw new ArgumentOutOfRangeException(nameof(record), "The attempt number must be at least 1.");

        _tests.GetOrAdd(record.TestIdentity, static identity => new TestState(identity)).Apply(record);
    }

    /// <summary>A deterministically ordered snapshot of every test identity the ledger has seen
    /// (claimed or recorded).</summary>
    internal IReadOnlyList<string> TestIdentities
    {
        get
        {
            var identities = _tests.Keys.ToArray();
            Array.Sort(identities, StringComparer.Ordinal);
            return identities;
        }
    }

    /// <summary>An immutable, deterministically ordered snapshot used by the summary formatter.</summary>
    internal IReadOnlyList<ClickHouseRetryLedgerState> Snapshot()
    {
        var states = _tests.Values.Select(static state => state.Snapshot()).ToArray();
        Array.Sort(states, static (left, right) => StringComparer.Ordinal.Compare(left.TestIdentity, right.TestIdentity));
        return states;
    }

    /// <summary>The accounting state of one test identity, captured without exposing the mutable
    /// object to callers.</summary>
    internal sealed class TestState
    {
        private readonly string _testIdentity;
        private readonly object _claimGate = new();
        private string? _claimedOperation;
        private int _attempts;
        private int _retryScheduled;
        private int _secondAttemptExecuted;
        private int _secondAttemptSucceeded;
        private int _secondAttemptFailed;
        private int _backoffCancelled;

        internal TestState(string testIdentity) => _testIdentity = testIdentity;

        internal bool TryClaimOperation(string operationIdentity)
        {
            lock (_claimGate)
            {
                if (_claimedOperation is not null)
                    return false;

                _claimedOperation = operationIdentity;

                // The claim happens immediately before the first attempt starts, so it is what keeps
                // the attempt count honest when that attempt succeeds and emits no failure record.
                Raise(ref _attempts, 1);
                return true;
            }
        }

        internal void Apply(in ClickHouseRetryAttemptRecord record)
        {
            Raise(ref _attempts, record.Attempt);

            if (record.ScheduledRetry)
                Interlocked.Exchange(ref _retryScheduled, 1);

            if (record.Attempt == 2)
            {
                Interlocked.Exchange(ref _secondAttemptExecuted, 1);

                // Recovery is set only by an explicit success outcome, never inferred from the
                // absence of a second failure (AC03/AC05).
                if (record.Succeeded)
                    Interlocked.Exchange(ref _secondAttemptSucceeded, 1);
                else
                    Interlocked.Exchange(ref _secondAttemptFailed, 1);
            }

            if (string.Equals(record.ReasonCode, ClickHouseReadRetrier.BackoffCancelledReason, StringComparison.Ordinal))
                Interlocked.Exchange(ref _backoffCancelled, 1);
        }

        internal ClickHouseRetryLedgerState Snapshot() => new(
            _testIdentity,
            Volatile.Read(ref _attempts),
            Volatile.Read(ref _retryScheduled) == 1,
            Volatile.Read(ref _secondAttemptExecuted) == 1,
            Volatile.Read(ref _secondAttemptSucceeded) == 1,
            Volatile.Read(ref _secondAttemptFailed) == 1,
            Volatile.Read(ref _backoffCancelled) == 1);

        private static void Raise(ref int target, int value)
        {
            var current = Volatile.Read(ref target);
            while (value > current)
            {
                var observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }
    }
}

/// <summary>An immutable accounting snapshot for one test identity (T328 AC05).</summary>
internal sealed record ClickHouseRetryLedgerState(
    string TestIdentity,
    int Attempts,
    bool RetryScheduled,
    bool SecondAttemptExecuted,
    bool RecoveredOnSecondAttempt,
    bool SecondAttemptFailed,
    bool BackoffCancelled)
{
    /// <summary>True when the operation exhausted its attempt budget.</summary>
    public bool Exhausted => SecondAttemptFailed;
}
