using System.Collections.Concurrent;
using NextORM.Tests.Shared.ClickHouseRetry;
using Xunit.Runner.Common;
using Xunit.Sdk;

[assembly: Xunit.Runner.Common.RegisterRunnerReporter(typeof(NextOrm.TestInfrastructure.ClickHouseRetryReporter))]

namespace NextOrm.TestInfrastructure;

/// <summary>
/// A thin, observational result reporter for the ClickHouse retry accounting (T328 AC05, Q2).
/// xUnit auto-selects it because <see cref="IsEnvironmentallyEnabled"/> is true; it never adds a
/// switch and never needs an environment variable.
/// <para>
/// It forwards every runner message to a wrapped <see cref="DefaultRunnerReporterMessageHandler"/>,
/// so native output, counts, exit semantics and result replacement are untouched. The only addition
/// is exactly one <see cref="ClickHouseRetrySummary"/> block at assembly completion, printed from
/// the ledger the retry helpers already populated. No exception may escape this reporter.
/// </para>
/// </summary>
internal sealed class ClickHouseRetryReporter : IRunnerReporter
{
    /// <inheritdoc />
    public string? RunnerSwitch => null;

    /// <inheritdoc />
    public string Description => "NextORM ClickHouse retry accounting (observational only)";

    /// <inheritdoc />
    public bool ForceNoLogo => false;

    /// <inheritdoc />
    public bool CanBeEnvironmentallyEnabled => true;

    /// <inheritdoc />
    public bool IsEnvironmentallyEnabled => true;

    /// <inheritdoc />
    public ValueTask<IRunnerReporterMessageHandler> CreateMessageHandler(
        IRunnerLogger logger,
        IMessageSink? diagnosticMessageSink) =>
        new(new ObservingMessageHandler(logger, diagnosticMessageSink));

    /// <summary>
    /// Wraps the native reporter handler. Every message is forwarded unchanged; the wrapper only
    /// reads per-test outcomes and, once per run, prints the accounting block.
    /// </summary>
    private sealed class ObservingMessageHandler : IRunnerReporterMessageHandler
    {
        private readonly DefaultRunnerReporterMessageHandler _inner;
        private readonly ConcurrentDictionary<string, string> _displayNames = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, bool> _outcomes = new(StringComparer.Ordinal);
        private int _summaryEmitted;

        internal ObservingMessageHandler(IRunnerLogger logger, IMessageSink? diagnosticMessageSink)
        {
            _ = diagnosticMessageSink; // Native reporter output is the only destination by design.
            _inner = new DefaultRunnerReporterMessageHandler(logger);
        }

        public bool OnMessage(IMessageSinkMessage message)
        {
            // Observation is isolated: a failure there must not change what the run does.
            try
            {
                Observe(message);
            }
            catch
            {
                // Accounting is best-effort; native behaviour must stay authoritative.
            }

            var forwarded = _inner.OnMessage(message);

            if (message is ITestAssemblyFinished)
                EmitSummaryOnce();

            return forwarded;
        }

        public ValueTask DisposeAsync() => _inner.DisposeAsync();

        private void Observe(IMessageSinkMessage message)
        {
            switch (message)
            {
                case ITestStarting starting:
                    _displayNames[starting.TestUniqueID] = starting.TestDisplayName;
                    break;

                case ITestPassed passed:
                    RememberOutcome(passed.TestUniqueID, passed: true);
                    break;

                case ITestFailed failed:
                    RememberOutcome(failed.TestUniqueID, passed: false);
                    break;
            }
        }

        private void RememberOutcome(string testUniqueId, bool passed)
        {
            // TestStarting always precedes a result, so the display name is known here; the unique
            // id is a defensive fallback and is never printed on its own.
            var identity = _displayNames.TryGetValue(testUniqueId, out var displayName) ? displayName : testUniqueId;
            _outcomes[identity] = passed;
        }

        private void EmitSummaryOnce()
        {
            if (Interlocked.Exchange(ref _summaryEmitted, 1) != 0)
                return;

            try
            {
                foreach (var line in ClickHouseRetrySummary.Build(
                             ClickHouseRetryLedger.Default,
                             identity => _outcomes.TryGetValue(identity, out var passed) ? passed : null))
                {
                    Console.WriteLine(line);
                }

                // r2 (S1-RELIABILITY): the fixture-only retry budget is reported next to the read
                // ledger, so a spent retry is always visible in the run output.
                Console.WriteLine(ClickHouseFixtureRetry.Summary());
            }
            catch (Exception exception)
            {
                // Exactly one sanitised error line: a type token, never a message or a stack trace.
                Console.WriteLine($"CLICKHOUSE-RETRY-SUMMARY-ERROR {exception.GetType().Name}");
            }
        }
    }
}
