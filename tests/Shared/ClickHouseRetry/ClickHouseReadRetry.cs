namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// The one call-site entry point for a retry-controlled ClickHouse read (T328 D4b, AC10/EV09). Each
/// enrolled call site wraps exactly one audited read site with it, so enrolment stays a local,
/// reviewable decision: there is no attribute, ambient policy, assembly hook or provider-wide
/// interception here, and nothing is injected into the harness.
/// <para>
/// The single process-wide <see cref="ClickHouseReadRetrier"/> is stateless between calls (all
/// attempt state is local to <c>ExecuteAsync</c>), so concurrent tests can share it. It reports into
/// <see cref="ClickHouseRetryLedger.Default"/>, the same ledger the integration reporter reads for
/// its end-of-run summary, and it enforces the one retry-controlled operation per test invocation.
/// </para>
/// <para>
/// On a healthy run the wrapper adds exactly one attempt and no delay: the delegate is awaited once
/// and its result is returned unchanged.
/// </para>
/// </summary>
internal static class ClickHouseReadRetry
{
    private static readonly ClickHouseReadRetrier Retrier = new(
        static record => ClickHouseRetryLedger.Default.Record(record),
        ledger: ClickHouseRetryLedger.Default);

    /// <summary>
    /// Executes one audited, wholly read-only ClickHouse operation with at most two total attempts.
    /// </summary>
    /// <typeparam name="T">The materialised read result.</typeparam>
    /// <param name="operation">The audited operation created through <see cref="ClickHouseReadOperation.Audited"/>.</param>
    /// <param name="read">The read itself — never an assertion, a mutation or any other work.</param>
    /// <param name="cancellationToken">The test's cancellation token, passed through to the driver.</param>
    /// <returns>The read result, unchanged.</returns>
    internal static Task<T> ExecuteAsync<T>(
        ClickHouseReadOperation operation,
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
        => Retrier.ExecuteAsync(operation, read, cancellationToken);
}
