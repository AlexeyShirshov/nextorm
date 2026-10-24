using System.Text;

namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>
/// The single deterministic accounting block emitted once per test invocation (T328 AC05, V08/V14).
/// It is pure: it reads a ledger snapshot and correlates it with the runner's final outcomes, so the
/// same inputs always produce the same lines. Identities are the only free text, and they are
/// sanitised and bounded — no message body, query, connection string or credential can appear.
/// </summary>
internal static class ClickHouseRetrySummary
{
    /// <summary>Bound on a sanitised identity, so one line stays readable.</summary>
    internal const int MaxIdentityLength = 200;

    /// <summary>
    /// Builds the accounting block: one line per test identity, followed by exactly one summary
    /// line. An empty ledger still emits the summary line with zeros.
    /// </summary>
    /// <param name="ledger">The accounting ledger; read only.</param>
    /// <param name="finalOutcome">Maps a test identity to <see langword="true"/> (passed),
    /// <see langword="false"/> (failed) or <see langword="null"/> (not observed).</param>
    internal static IReadOnlyList<string> Build(ClickHouseRetryLedger ledger, Func<string, bool?> finalOutcome)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(finalOutcome);

        var states = ledger.Snapshot();
        var lines = new List<string>(states.Count + 1);

        var retriedTests = 0;
        var additionalAttempts = 0;
        var recoveredTests = 0;
        var exhaustedRetries = 0;
        var notRecoveredTests = 0;
        var unretriedFailures = 0;
        var attemptedOnce = 0;

        foreach (var state in states)
        {
            var final = finalOutcome(state.TestIdentity);

            // "recovered" is a qualified claim: the operation must have recovered on attempt 2 and
            // the test itself must have passed. A later assertion failure is never a recovery.
            var recovered = state.RecoveredOnSecondAttempt && final == true;

            if (state.RetryScheduled)
                retriedTests++;
            if (state.SecondAttemptExecuted)
                additionalAttempts++;
            if (recovered)
                recoveredTests++;
            if (state.SecondAttemptFailed)
                exhaustedRetries++;
            if (state.RetryScheduled && !recovered)
                notRecoveredTests++;
            if (!state.RetryScheduled && final == false)
                unretriedFailures++;
            if (state.Attempts == 1 && final is not null)
                attemptedOnce++;

            lines.Add(
                $"CLICKHOUSE-RETRY-TEST test={Sanitise(state.TestIdentity)} " +
                $"attempts={state.Attempts} " +
                $"retried={Lower(state.RetryScheduled)} " +
                $"operation_recovered={Lower(state.RecoveredOnSecondAttempt)} " +
                $"final={FinalText(final)} " +
                $"recovered={Lower(recovered)}");
        }

        lines.Add(
            $"CLICKHOUSE-RETRY-SUMMARY retried_tests={retriedTests} " +
            $"additional_attempts={additionalAttempts} " +
            $"recovered_tests={recoveredTests} " +
            $"exhausted_retries={exhaustedRetries} " +
            $"not_recovered_tests={notRecoveredTests} " +
            $"unretried_failures={unretriedFailures} " +
            $"attempted_once={attemptedOnce}");

        return lines;
    }

    /// <summary>
    /// Keeps an identity on one bounded line: control characters (including line breaks) become
    /// spaces and the result is capped at <see cref="MaxIdentityLength"/> characters.
    /// </summary>
    internal static string Sanitise(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return "unknown";

        var builder = new StringBuilder(Math.Min(identity.Length, MaxIdentityLength));
        foreach (var character in identity)
        {
            if (builder.Length >= MaxIdentityLength)
                break;

            builder.Append(char.IsControl(character) ? ' ' : character);
        }

        var sanitised = builder.ToString().Trim();
        return sanitised.Length == 0 ? "unknown" : sanitised;
    }

    private static string FinalText(bool? final) => final switch
    {
        true => "passed",
        false => "failed",
        _ => "unknown",
    };

    private static string Lower(bool value) => value ? "true" : "false";
}
