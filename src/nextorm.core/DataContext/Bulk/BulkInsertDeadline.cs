using System.Diagnostics;

namespace NextORM.Core;

/// <summary>
/// The single monotonic execution budget of one bulk-insert call. It is started once at execution
/// entry and never reset per batch: every row pull and every write of the call draws from the same
/// budget, so a slow source and a slow driver share one deadline instead of extending it.
/// </summary>
/// <remarks>
/// Expiry is reported as <see cref="TimeoutException"/>; a cancellation requested by the caller is
/// reported as <see cref="OperationCanceledException"/> and always wins when both race, so a caller
/// can never mistake its own cancellation for a timeout. The type opens no transaction and makes no
/// rollback promise: rows already written stay written.
/// </remarks>
internal sealed class BulkInsertDeadline : IDisposable
{
    // CancellationTokenSource.CancelAfter accepts at most uint.MaxValue - 1 milliseconds; a longer
    // budget is treated as unbounded (no timer) because it can never plausibly expire.
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    private readonly long _startTimestamp;
    private readonly TimeSpan? _budget;
    private readonly CancellationToken _userToken;
    private readonly CancellationTokenSource? _source;
    private readonly Timer? _timer;
    private int _expired;
    private bool _disposed;

    private BulkInsertDeadline(TimeSpan? budget, CancellationToken userToken)
    {
        _budget = budget;
        _userToken = userToken;
        _startTimestamp = Stopwatch.GetTimestamp();

        if (budget is { } value && value <= MaxTimerDelay)
        {
            _source = CancellationTokenSource.CreateLinkedTokenSource(userToken);
            _timer = new Timer(static state => ((BulkInsertDeadline)state!).Expire(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timer.Change(value, Timeout.InfiniteTimeSpan);
        }
    }

    // The timer is the authority on expiry: it marks the deadline spent *before* cancelling the token, so a
    // provider cancellation observed at the deadline boundary is never mistaken for one caused by elapsed
    // time alone (which can trail the timer by a fraction of a millisecond).
    private void Expire()
    {
        Volatile.Write(ref _expired, 1);

        try
        {
            _source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The call completed and disposed the deadline before the timer fired.
        }
    }

    /// <summary>Starts the budget of one bulk-insert call.</summary>
    /// <param name="timeout">The whole-invocation budget, or <see langword="null"/> for no nextorm deadline.</param>
    /// <param name="cancellationToken">The caller's cancellation token.</param>
    /// <returns>The started deadline, which must be disposed.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is zero or negative.</exception>
    public static BulkInsertDeadline Start(TimeSpan? timeout, CancellationToken cancellationToken)
    {
        if (timeout is { } budget && budget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "A bulk-insert Timeout must be positive.");

        return new BulkInsertDeadline(timeout, cancellationToken);
    }

    /// <summary>The caller's cancellation token, as supplied at execution entry.</summary>
    public CancellationToken UserToken => _userToken;

    /// <summary>
    /// The token passed to row enumeration and to the provider write: it is cancelled by the caller or
    /// by the deadline expiring. Callers must translate the resulting cancellation through
    /// <see cref="ThrowIfCancelled"/> so the two causes stay distinguishable.
    /// </summary>
    public CancellationToken Token => _source?.Token ?? _userToken;

    /// <summary>The time left in the budget, or <see langword="null"/> when no budget was requested.</summary>
    public TimeSpan? Remaining => _budget is { } budget ? budget - Elapsed : null;

    /// <summary>Whether the whole-invocation budget has been exhausted.</summary>
    public bool IsExpired => Volatile.Read(ref _expired) != 0 || (_budget is { } budget && Elapsed >= budget);

    private TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startTimestamp);

    /// <summary>
    /// Throws when the call must stop: the caller's cancellation first (it wins a race with the
    /// deadline), then the exhausted budget.
    /// </summary>
    /// <exception cref="OperationCanceledException">The caller cancelled the call.</exception>
    /// <exception cref="TimeoutException">The whole-invocation budget is exhausted.</exception>
    public void ThrowIfCancelled()
    {
        if (_userToken.IsCancellationRequested)
            throw UserCancelled();

        if (IsExpired)
            throw TimedOut();
    }

    /// <summary>Builds the cancellation exception bound to the caller's token.</summary>
    /// <param name="inner">The provider's own cancellation, or <see langword="null"/>.</param>
    /// <returns>A cancellation carrying the caller's token identity.</returns>
    public OperationCanceledException UserCancelled(Exception? inner = null)
        => new("The bulk insert was cancelled by the caller.", inner, _userToken);

    /// <summary>Builds the timeout exception of an exhausted budget.</summary>
    /// <returns>The timeout exception.</returns>
    public TimeoutException TimedOut()
        => new($"The bulk insert exceeded its whole-invocation Timeout of {_budget}. Rows written before the deadline are not rolled back.");

    /// <summary>
    /// Translates a cancellation raised by the provider or the row source: the caller's cancellation
    /// (with its token identity) wins, an exhausted budget becomes a <see cref="TimeoutException"/>,
    /// and any other cancellation is returned unchanged.
    /// </summary>
    /// <param name="cancellation">The cancellation observed while executing.</param>
    /// <returns>The exception to throw.</returns>
    public Exception Translate(OperationCanceledException cancellation)
    {
        if (_userToken.IsCancellationRequested)
            return UserCancelled(cancellation);

        return IsExpired ? TimedOut() : cancellation;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _timer?.Dispose();
        _source?.Dispose();
    }
}
