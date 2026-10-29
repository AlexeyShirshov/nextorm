namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// The narrow, fixture-only retry path for ClickHouse setup/cleanup work (step-1 r2, S1-RELIABILITY).
/// It exists because a fixture statement can hit the same classified transport transient the read
/// retrier already recognises, while the read path itself deliberately refuses DDL.
/// <para>
/// Boundaries, all enforced here:
/// </para>
/// <list type="bullet">
/// <item><description>only setup/cleanup work is wrapped; the product operation under test never goes
/// through this type, so no assertion can be retried into a pass;</description></item>
/// <item><description>at most <see cref="RetriesPerOperationLimit"/> retry per operation and
/// <see cref="RetriesPerSuiteLimit"/> retry for the whole suite; the suite counter is the single
/// process-wide budget, and asking for one more is a failure, never a longer wait;</description></item>
/// <item><description>only the already-classified transient kinds are retried
/// (<see cref="ClickHouseFailureClassification.IsRetryable"/>: a client HTTP timeout or a typed
/// response-ended reset); everything else is rethrown unchanged and immediately;</description></item>
/// <item><description>the retried delegate must be idempotent — the counters are re-derived from the
/// server state on every attempt, so an operation whose answer was lost cannot double-apply;</description></item>
/// <item><description>no timeout is widened: the driver's 120 s <c>HttpClient.Timeout</c> stays as it
/// is, so a structural stall still fails the suite.</description></item>
/// </list>
/// <para>
/// <b>Accounting (r2, attempt 2).</b> A retry must be observable <em>from the process that ran the
/// measurement</em>, including the zero case, so the helper writes a durable file instead of relying on
/// a console line: every attempt outcome is appended as it happens, and a final summary line is written
/// on process exit. The destination comes from
/// <see cref="AccountingPathVariable"/>; without it the helper falls back to a temp file and says so in
/// the summary. Nothing here can fail a test: accounting is best effort by design.
/// </para>
/// </summary>
internal static class ClickHouseFixtureRetry
{
    /// <summary>Environment variable carrying the accounting file path for the current run.</summary>
    internal const string AccountingPathVariable = "NEXTORM_CLICKHOUSE_FIXTURE_RETRY_ACCOUNTING";

    /// <summary>Optional environment variable carrying the run id that ties the file to a measurement.</summary>
    internal const string RunIdVariable = "NEXTORM_CLICKHOUSE_FIXTURE_RETRY_RUN_ID";

    /// <summary>Retries allowed for one fixture operation.</summary>
    internal const int RetriesPerOperationLimit = 1;

    /// <summary>Retries allowed for one full integration run.</summary>
    internal const int RetriesPerSuiteLimit = 1;

    private static readonly object AccountingGate = new();

    private static readonly DateTime ProcessTouchUtc = DateTime.UtcNow;

    private static int _suiteRetries;
    private static int _exitHooked;
    private static int _summaryWritten;
    private static int _headerWritten;

    // Provenance carried by every line, so a file can be bound to the process and the run that wrote
    // it without trusting a console transcript: pid, run id (from the measurement command), the UTC
    // time this type was first touched in the process, and a per-line UTC timestamp.
    private static readonly string Provenance =
        $"pid={Environment.ProcessId} run_id={RunId} process_touch_utc={ProcessTouchUtc:O}";

    private static string RunId
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(RunIdVariable);
            return string.IsNullOrWhiteSpace(configured) ? "unset" : configured;
        }
    }

    static ClickHouseFixtureRetry()
    {
        EnsureExitHook();
        EnsureHeader();
    }

    /// <summary>How many fixture retries the current process has already spent.</summary>
    internal static int RetriesUsed => Volatile.Read(ref _suiteRetries);

    /// <summary>The accounting file path this process writes to.</summary>
    internal static string AccountingPath
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(AccountingPathVariable);
            return string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(Path.GetTempPath(), "nextorm-clickhouse-fixture-retry.txt")
                : configured;
        }
    }

    /// <summary>Runs one idempotent fixture operation, retrying a classified transient exactly once.</summary>
    /// <param name="operationIdentity">Stable, sanitised identity recorded in the accounting file.</param>
    /// <param name="operation">The idempotent setup/cleanup work.</param>
    internal static void Execute(string operationIdentity, Action operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        try
        {
            operation();
            Append($"CLICKHOUSE-FIXTURE-ATTEMPT operation={Sanitise(operationIdentity)} attempt=1 outcome=succeeded retry=0");
            return;
        }
        catch (Exception first)
        {
            var classification = ClickHouseTransientClassifier.Classify(first, CancellationToken.None);
            if (!classification.IsRetryable)
            {
                Append($"CLICKHOUSE-FIXTURE-ATTEMPT operation={Sanitise(operationIdentity)} attempt=1 " +
                       $"outcome=failed reason={classification.ReasonCode} retry=0");
                throw;
            }

            if (Interlocked.Increment(ref _suiteRetries) > RetriesPerSuiteLimit)
            {
                Interlocked.Decrement(ref _suiteRetries);
                Append($"CLICKHOUSE-FIXTURE-ATTEMPT operation={Sanitise(operationIdentity)} attempt=1 " +
                       $"outcome=failed reason={classification.ReasonCode} retry=0 budget=exhausted");
                throw new InvalidOperationException(
                    $"ClickHouse fixture retry budget exhausted ({RetriesPerSuiteLimit} per suite); " +
                    $"operation '{operationIdentity}' failed again with {classification.ReasonCode}.",
                    first);
            }

            // Exactly one more attempt: the delegate starts from scratch and its result is either the
            // completed fixture step or the second failure, which propagates unchanged.
            Append($"CLICKHOUSE-FIXTURE-ATTEMPT operation={Sanitise(operationIdentity)} attempt=1 " +
                   $"outcome=failed reason={classification.ReasonCode} retry=1");
            try
            {
                operation();
                Append($"CLICKHOUSE-FIXTURE-ATTEMPT operation={Sanitise(operationIdentity)} attempt=2 outcome=succeeded retry=1");
            }
            catch (Exception second)
            {
                var secondKind = ClickHouseTransientClassifier.Classify(second, CancellationToken.None);
                Append($"CLICKHOUSE-FIXTURE-ATTEMPT operation={Sanitise(operationIdentity)} attempt=2 " +
                       $"outcome=failed reason={secondKind.ReasonCode} retry=1");
                throw;
            }
        }
    }

    /// <summary>One summary line, deterministic for the current process state.</summary>
    internal static string Summary() =>
        $"CLICKHOUSE-FIXTURE-RETRY-SUMMARY retries_used={RetriesUsed} " +
        $"per_operation_limit={RetriesPerOperationLimit} per_suite_limit={RetriesPerSuiteLimit} " +
        $"budget_exhausted={Lower(RetriesUsed >= RetriesPerSuiteLimit)} " +
        $"accounting_file={AccountingPath}";

    /// <summary>
    /// Writes the final summary line once per process. Called from the process-exit hook and callable
    /// directly so a test or a diagnostic run can flush on demand.
    /// </summary>
    internal static void FlushSummary()
    {
        if (Interlocked.Exchange(ref _summaryWritten, 1) != 0)
            return;

        Append(Summary());
    }

    private static void EnsureExitHook()
    {
        if (Interlocked.Exchange(ref _exitHooked, 1) != 0)
            return;

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => FlushSummary();
    }

    private static void EnsureHeader()
    {
        if (Interlocked.Exchange(ref _headerWritten, 1) != 0)
            return;

        Append("CLICKHOUSE-FIXTURE-RETRY-PROVENANCE path=" + AccountingPath);
    }

    private static void Append(string line)
    {
        try
        {
            lock (AccountingGate)
            {
                var path = AccountingPath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.AppendAllText(
                    path,
                    $"{DateTime.UtcNow:O} {Provenance} {line}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Accounting must never turn a fixture outcome into a test failure.
        }
    }

    private static string Sanitise(string value) =>
        value.Length <= 120 ? value.Replace(' ', '_') : value[..120].Replace(' ', '_');

    private static string Lower(bool value) => value ? "true" : "false";
}
