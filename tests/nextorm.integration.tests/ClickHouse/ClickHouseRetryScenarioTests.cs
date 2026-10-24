using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.ExceptionServices;
using ClickHouse.Driver;
using FluentAssertions;
using NextORM.ClickHouse;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Integration.Tests;

/// <summary>
/// The nine live acceptance probes for T328 (plan §10 C05, contract EV06/EV09, AC09/AC10). Every probe
/// runs against a real ClickHouse server — no simulation and no in-memory provider — and is excluded
/// from the canonical suite by the class-level <c>Category=Diagnostic</c> trait, which the canonical
/// command filters out (<c>-notrait "Category=Diagnostic"</c>). Each fact carries exactly one
/// <c>T328Scenario</c> trait, which the isolated C05 invocation selects.
/// <para>
/// Six probes fail by design (retry-exhaustion, assertion-failure, unknown-table, code216,
/// unsafe-operation, recovered-then-assertion): each asserts its facts first and then fails, so the
/// runner records a genuine failed test and the frozen C05 expectation of a nonzero exit holds.
/// </para>
/// <para>
/// Evidence discipline: one machine-readable <c>CLICKHOUSE-PROBE scenario=… </c> line per probe, with
/// counters, timings, codes and version values only — never a connection string, credential or server
/// message body. The retry-relevant probes also print the accounting block built from their private
/// ledger, so the scenario log carries the correlation evidence itself. No probe requires
/// <c>NEXTORM_SKIP_PROVIDERS</c>: an unavailable server fails closed, and only an explicit opt-out
/// skips.
/// </para>
/// </summary>
[Trait("Category", "Diagnostic")]
public sealed class ClickHouseRetryScenarioTests
{
    private const string ScenarioTrait = "T328Scenario";

    private const string ProbePrefix = "CLICKHOUSE-PROBE";

    private const string TestIdentity = "ClickHouseRetryScenarioTests";

    /// <summary>The probe's deliberately small supported client timeout, in seconds.</summary>
    private const string EffectiveTimeoutSeconds = "1";

    /// <summary>
    /// Response bytes forwarded before the reset. Large enough to prove the response really travelled
    /// back from the live server through the proxy, small enough that the reset lands while the client
    /// is still reading the response head rather than a materialised body.
    /// </summary>
    private const int ResponseBytesBeforeReset = 32;

    /// <summary>Longer than the probe's 1 s client timeout, so attempt 1 must fail client-side.</summary>
    private static readonly TimeSpan ProxyDelay = TimeSpan.FromSeconds(3);

    /// <summary>The table name the unknown-table probe reads from; it never exists anywhere.</summary>
    private const string MissingTable = "t328_probe_missing_table";

    /// <summary>The session-scoped temporary table used only by the code216 probe.</summary>
    private const string TemporaryTable = "t328_probe_code216";

    /// <summary>The ClickHouse error code the code216 probe asserts (contract §10 C05).</summary>
    private const int UnknownTableArgumentCode = 216;

    public ClickHouseRetryScenarioTests()
    {
        if (ClickHouseTestProvider.Instance.IsAvailable)
            return;

        // Fail closed, exactly like ProviderTestSuite: a missing server must not turn a probe into a
        // silently green run, and only an explicit opt-out may skip it.
        if (ProviderSkipPolicy.IsOptedOut(ClickHouseTestProvider.Instance.Name))
        {
            Assert.Skip(
                $"Provider '{ClickHouseTestProvider.Instance.Name}' is explicitly opted out via " +
                $"{ProviderSkipPolicy.EnvironmentVariable}: {ClickHouseTestProvider.Instance.SkipReason}");
        }
        else
        {
            ClickHouseTestProvider.Instance.IsAvailable.Should().BeTrue(
                $"the live acceptance probes need a real ClickHouse server ({ClickHouseTestProvider.Instance.SkipReason}); " +
                "NEXTORM_SKIP_PROVIDERS is never required to run them");
        }
    }

    // --- 1. version -------------------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "version")]
    public async Task Version_ShouldReportTheContractualServerVersion()
    {
        using var context = CreateContext(BaseConnectionString());

        string version;
        await using (var result = await context.ExecuteRawAsync("select version() as v", [], CancellationToken.None))
        {
            var values = result.Read<string>();
            values.Should().HaveCount(1, "version() returns exactly one row");
            version = values[0];
        }

        var external = ClickHouseContainer.IsExternallyConfigured;

        WriteProbe(
            "version",
            $"server_version={version} endpoint_external={Lower(external)} endpoint_container={Lower(!external)}");

        version.Should().StartWith("25.8.", "the acceptance criteria pin a real ClickHouse 25.8 server");
    }

    // --- 2. timeout-recovery ----------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "timeout-recovery")]
    public async Task TimeoutRecovery_ShouldFailOnceByClientTimeout_ThenRecover()
    {
        var baseConnectionString = BaseConnectionString();
        await using var proxy = FaultProxy.DelayFirstRequest(baseConnectionString, ProxyDelay);

        // Resolved explicitly and deterministically: the environment is never mutated.
        var effective = ClickHouseTestTimeout.Resolve(baseConnectionString, EffectiveTimeoutSeconds);
        effective.EffectiveSeconds.Should().Be(1, "the probe pins a deliberately small supported timeout");
        effective.Source.Should().Be(ClickHouseTestTimeoutSource.Knob, "the explicit knob value must win");

        var harness = new ProbeRetrier();
        using var context = CreateContext(proxy.ConnectionStringFor(effective.ConnectionString));
        var stopwatch = Stopwatch.StartNew();
        var (succeeded, value) = await harness
            .TryExecuteAsync(Audited("timeout-recovery"), cancellationToken => ReadScalarAsync(context, cancellationToken));
        stopwatch.Stop();

        harness.FirstFailure.Should().NotBeNull("attempt 1 must fail before a retry can happen");
        var firstClassification = Classify(harness.FirstFailure);
        var state = SingleState(harness.Ledger);

        succeeded.Should().BeTrue(
            "attempt 2 must recover after the proxy released its pass-through connection; first failure was {0}",
            DescribeFailure(harness.FirstFailure));
        firstClassification.Kind.Should().Be(
            ClickHouseFailureKind.ClientHttpTimeout,
            "a request held past the 1 s client timeout must classify as a client HTTP timeout; observed chain {0}",
            DescribeFailure(harness.FirstFailure));
        harness.Attempts.Should().Be(2, "exactly one additional attempt is allowed");
        harness.Records.Should().HaveCount(2, "one scheduled-retry record for attempt 1 and one success record for attempt 2");
        harness.Records.Count(static record => record.ScheduledRetry).Should().Be(1, "exactly one retry is scheduled");
        harness.Delays.Should().ContainSingle("exactly one backoff precedes the only additional attempt")
            .Which.Should().Be(ClickHouseReadRetrier.BackoffDelay, "the frozen backoff is one second");
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(
            ClickHouseReadRetrier.BackoffDelay,
            "the observed elapsed time must include the real backoff");
        value.Should().Be(42L, "attempt 2 returned the live server value");
        state.Attempts.Should().Be(2);
        state.RetryScheduled.Should().BeTrue();
        state.RecoveredOnSecondAttempt.Should().BeTrue("recovery is the observed success of attempt 2");

        WriteProbe(
            "timeout-recovery",
            $"effective_seconds={effective.EffectiveSeconds} attempts={harness.Attempts} retry_records={harness.Records.Count} " +
            $"delays={harness.Delays.Count} delay_ms={(long)harness.Delays[0].TotalMilliseconds} " +
            $"elapsed_ms={(long)stopwatch.Elapsed.TotalMilliseconds} classification={firstClassification.ReasonCode} " +
            $"server_responded_before_fault={Lower(proxy.ServerRespondedBeforeFault)}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: true);
        lines.Should().Contain(
            line => line.Contains("operation_recovered=true", StringComparison.Ordinal)
                && line.Contains("recovered=true", StringComparison.Ordinal)
                && line.Contains("final=passed", StringComparison.Ordinal),
            "the accounting block must qualify the recovery with the passing final outcome");
    }

    // --- 3. reset-recovery ------------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "reset-recovery")]
    public async Task ResetRecovery_ShouldResetAfterTheLiveResponseThenRecover()
    {
        var baseConnectionString = BaseConnectionString();
        await using var proxy = FaultProxy.ResetAfterFirstResponseBytes(baseConnectionString, ResponseBytesBeforeReset);

        var harness = new ProbeRetrier();
        using var context = CreateContext(proxy.ConnectionStringFor(baseConnectionString));
        var (succeeded, value) = await harness
            .TryExecuteAsync(Audited("reset-recovery"), cancellationToken => ReadScalarAsync(context, cancellationToken));

        proxy.ServerRespondedBeforeFault.Should().BeTrue(
            "the request must reach the live endpoint before the fault: accepted={0} client_bytes={1} server_bytes={2} faulted={3}",
            proxy.AcceptedConnections,
            proxy.ClientToServerBytes,
            proxy.ServerToClientBytes,
            proxy.FaultedConnections);

        harness.FirstFailure.Should().NotBeNull("attempt 1 must fail before a retry can happen");
        var firstClassification = Classify(harness.FirstFailure);

        // The D2 reset branch is provisional until D0b measures the live signature: this probe must be
        // honest evidence, so an unexpected chain fails loudly instead of passing silently.
        firstClassification.Kind.Should().Be(
            ClickHouseFailureKind.ConnectionReset,
            "the D2 connection-reset branch is provisional; the observed chain was {0}, classified {1} ({2}), " +
            "proxy counters accepted={3} client_bytes={4} server_bytes={5} faulted={6} — D0b must confirm the " +
            "live reset signature or the classifier and this probe must be revised",
            DescribeFailure(harness.FirstFailure),
            firstClassification.Kind,
            firstClassification.ReasonCode,
            proxy.AcceptedConnections,
            proxy.ClientToServerBytes,
            proxy.ServerToClientBytes,
            proxy.FaultedConnections);

        harness.Attempts.Should().Be(
            2,
            "a retry happens only when the live reset classifies as retryable; observed chain {0}",
            DescribeFailure(harness.FirstFailure));
        succeeded.Should().BeTrue("attempt 2 must recover on the proxy's pass-through connection");
        value.Should().Be(42L, "attempt 2 returned the live server value");

        var state = SingleState(harness.Ledger);
        state.Attempts.Should().Be(2);
        state.RetryScheduled.Should().BeTrue();
        state.RecoveredOnSecondAttempt.Should().BeTrue();

        WriteProbe(
            "reset-recovery",
            $"attempts={harness.Attempts} retry_records={harness.Records.Count} classification={firstClassification.ReasonCode} " +
            $"response_bytes_before_reset={ResponseBytesBeforeReset} server_responded_before_fault={Lower(proxy.ServerRespondedBeforeFault)} " +
            $"accepted={proxy.AcceptedConnections} client_bytes={proxy.ClientToServerBytes} server_bytes={proxy.ServerToClientBytes} " +
            $"faulted={proxy.FaultedConnections}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: true);
        lines.Should().Contain(
            line => line.Contains("operation_recovered=true", StringComparison.Ordinal)
                && line.Contains("recovered=true", StringComparison.Ordinal),
            "the accounting block must record the recovered operation");
    }

    // --- 4. retry-exhaustion ----------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "retry-exhaustion")]
    public async Task RetryExhaustion_ShouldStopAfterTwoAttempts()
    {
        var baseConnectionString = BaseConnectionString();
        await using var proxy = FaultProxy.ResetAlways(baseConnectionString);

        var harness = new ProbeRetrier();
        using var context = CreateContext(proxy.ConnectionStringFor(baseConnectionString));
        var (succeeded, _) = await harness
            .TryExecuteAsync(Audited("retry-exhaustion"), cancellationToken => ReadScalarAsync(context, cancellationToken));

        succeeded.Should().BeFalse("every connection is reset, so both attempts must fail");

        harness.Attempts.Should().Be(
            2,
            "exactly two total attempts are allowed and no third attempt may start; first failure {0}, second failure {1}",
            DescribeFailure(harness.FirstFailure),
            DescribeFailure(harness.LastFailure));
        harness.Records.Should().HaveCount(2, "one scheduled-retry record and one terminal failure record");
        harness.Records.Count(static record => record.ScheduledRetry).Should().Be(1);
        harness.Delays.Should().ContainSingle().Which.Should().Be(ClickHouseReadRetrier.BackoffDelay);

        harness.Thrown.Should().NotBeNull("the exhausted operation must rethrow");
        ReferenceEquals(harness.Thrown, harness.LastFailure).Should().BeTrue(
            "the retrier must rethrow the original exception instance, not a wrapper");
        harness.Thrown!.GetType().Should().Be(
            harness.LastFailure!.GetType(),
            "the rethrown exception must preserve the original type");

        var state = SingleState(harness.Ledger);
        state.Attempts.Should().Be(2);
        state.RetryScheduled.Should().BeTrue();
        state.Exhausted.Should().BeTrue("the attempt budget was exhausted");

        WriteProbe(
            "retry-exhaustion",
            $"attempts={harness.Attempts} retry_records={harness.Records.Count} thrown={harness.Thrown!.GetType().Name} " +
            $"first_chain={Describe(harness.FirstFailure)} second_chain={Describe(harness.LastFailure)} " +
            $"faulted={proxy.FaultedConnections} server_responded_before_fault={Lower(proxy.ServerRespondedBeforeFault)}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: false);
        lines.Should().Contain(
            line => line.Contains("recovered=false", StringComparison.Ordinal)
                && line.Contains("final=failed", StringComparison.Ordinal),
            "an exhausted retry is never a recovery");

        throw new InvalidOperationException("T328 retry-exhaustion probe: expected failure");
    }

    // --- 5. assertion-failure ---------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "assertion-failure")]
    public async Task AssertionFailure_ShouldRunOnceAndNeverRetry()
    {
        var harness = new ProbeRetrier();
        using var context = CreateContext(BaseConnectionString());
        var reads = 0;

        var value = await harness.Retrier.ExecuteAsync(
            Audited("assertion-failure"),
            async cancellationToken =>
            {
                reads++;
                return await ReadScalarAsync(context, cancellationToken);
            },
            CancellationToken.None);

        reads.Should().Be(1, "the read delegate ran exactly once");
        harness.Records.Should().BeEmpty("a successful read schedules no retry");
        var state = SingleState(harness.Ledger);
        state.Attempts.Should().Be(1);
        state.RetryScheduled.Should().BeFalse("no retry exists for this identity");

        WriteProbe("assertion-failure", $"reads={reads} retry_records={harness.Records.Count} server_value={value}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: false);
        lines.Should().Contain(
            line => line.Contains("retried=false", StringComparison.Ordinal)
                && line.Contains("final=failed", StringComparison.Ordinal),
            "a later assertion failure on a single attempt is an unretried failure");

        // Genuine assertion failure: the server returns 42. The test must fail here, unchanged and once.
        value.Should().Be(41L, "this is a real assertion failure, not a retried or simulated one");
    }

    // --- 6. unknown-table -------------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "unknown-table")]
    public async Task UnknownTable_ShouldFailOnceWithAServerError()
    {
        var harness = new ProbeRetrier();
        using var context = CreateContext(BaseConnectionString());

        var (succeeded, _) = await harness
            .TryExecuteAsync(
                Audited("unknown-table"),
                async cancellationToken =>
                {
                    await using var result = await context
                        .ExecuteRawAsync($"select id from {MissingTable}", [], cancellationToken);

                    return result.Read<int>();
                });

        succeeded.Should().BeFalse("reading a non-existent table must fail");
        harness.Thrown.Should().BeOfType<ClickHouseServerException>(
            "a genuine server SQL error must stay a typed server exception; observed {0}",
            DescribeFailure(harness.Thrown));

        var serverError = (ClickHouseServerException)harness.Thrown!;
        var code = serverError.ErrorCode;
        var classification = Classify(serverError);

        classification.Kind.Should().Be(
            ClickHouseFailureKind.ServerError,
            "a server error is never retryable, even when its text looks timeout-like; code={0}",
            code);
        harness.Attempts.Should().Be(1, "the operation ran exactly once");
        harness.Records.Should().BeEmpty("no retry record exists for a server error");
        SingleState(harness.Ledger).Attempts.Should().Be(1);

        WriteProbe(
            "unknown-table",
            $"error_code={code} error_name={ErrorCodeName(code)} attempts={harness.Attempts} " +
            $"retry_records={harness.Records.Count} classification={classification.ReasonCode}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: false);
        lines.Should().Contain(
            line => line.Contains("retried=false", StringComparison.Ordinal),
            "a server error is an unretried failure");

        Rethrow(harness.Thrown!);
    }

    // --- 7. code216 -------------------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "code216")]
    public async Task Code216_ShouldRunOnceWithAServerErrorFromTheMissingColumn()
    {
        var harness = new ProbeRetrier();

        // A session-scoped temporary table: the only write any probe performs, and it disappears with
        // the session. UseSession/SessionId keep both statements on one server session.
        using var context = CreateContext(WithSession(BaseConnectionString()));

        using (await context
                   .ExecuteRawAsync($"create temporary table {TemporaryTable} (id Int32) engine = Memory", [], CancellationToken.None)
                   )
        {
            // The temporary table is created; it never touches a persistent table.
        }

        var (succeeded, _) = await harness
            .TryExecuteAsync(
                Audited("code216"),
                async cancellationToken =>
                {
                    await using var result = await context
                        .ExecuteRawAsync($"select missing_column from {TemporaryTable}", [], cancellationToken);

                    return result.Read<int>();
                });

        succeeded.Should().BeFalse("the missing column must fail on the server");
        harness.Thrown.Should().BeOfType<ClickHouseServerException>(
            "the failure must be a genuine server error; observed {0}",
            DescribeFailure(harness.Thrown));

        var serverError = (ClickHouseServerException)harness.Thrown!;
        var code = serverError.ErrorCode;
        var classification = Classify(serverError);

        classification.Kind.Should().Be(ClickHouseFailureKind.ServerError, "a server error is never retryable");
        harness.Attempts.Should().Be(1, "the operation ran exactly once");
        harness.Records.Should().BeEmpty("no retry record exists for a server error");
        SingleState(harness.Ledger).Attempts.Should().Be(1);

        WriteProbe(
            "code216",
            $"error_code={code} error_name={ErrorCodeName(code)} attempts={harness.Attempts} " +
            $"retry_records={harness.Records.Count} classification={classification.ReasonCode}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: false);
        lines.Should().Contain(
            line => line.Contains("retried=false", StringComparison.Ordinal),
            "a server error is an unretried failure");

        code.Should().Be(
            UnknownTableArgumentCode,
            "the probe must observe a genuine server code 216; the observed code was {0} ({1}) with chain {2}",
            code,
            ErrorCodeName(code),
            Describe(harness.Thrown));

        Rethrow(harness.Thrown!);
    }

    // --- 8. unsafe-operation ----------------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "unsafe-operation")]
    public void UnsafeOperation_ShouldBeRejectedBeforeAnyCommand()
    {
        // A command can only be issued after a successful enrollment, so this counter can only move
        // if the guard failed to fire.
        var commandExecutions = 0;

        foreach (var category in new[]
                 {
                     ClickHouseReadCategory.Write,
                     ClickHouseReadCategory.Ddl,
                     ClickHouseReadCategory.UnknownEffect,
                     ClickHouseReadCategory.StreamedRead,
                     ClickHouseReadCategory.FixtureMutation,
                 })
        {
            var enrolled = false;
            Action enrol = () =>
            {
                _ = ClickHouseReadOperation.Audited(
                    $"{TestIdentity}.unsafe-operation",
                    $"unsafe-operation/{category.ToString().ToLowerInvariant()}",
                    category);

                enrolled = true;
            };

            enrol.Should().Throw<ArgumentException>("'{0}' is not a retry-eligible read category", category)
                .WithParameterName("category");

            enrolled.Should().BeFalse("the enrollment guard must reject '{0}' before the probe can run a command", category);

            if (enrolled)
                commandExecutions++;
        }

        // Every enrollment above threw, so nothing reached the data layer: the guard fires before any
        // command is created or executed.
        commandExecutions.Should().Be(0, "no command may be created or executed for a rejected category");

        WriteProbe("unsafe-operation", $"rejected_categories=5 commands_executed={commandExecutions}");

        throw new InvalidOperationException("T328 unsafe-operation probe: expected failure");
    }

    // --- 9. recovered-then-assertion ---------------------------------------------------------------

    [Fact]
    [Trait(ScenarioTrait, "recovered-then-assertion")]
    public async Task RecoveredThenAssertion_ShouldNotCountAsARecoveredTest()
    {
        var baseConnectionString = BaseConnectionString();
        await using var proxy = FaultProxy.DelayFirstRequest(baseConnectionString, ProxyDelay);

        var effective = ClickHouseTestTimeout.Resolve(baseConnectionString, EffectiveTimeoutSeconds);
        var harness = new ProbeRetrier();
        using var context = CreateContext(proxy.ConnectionStringFor(effective.ConnectionString));

        var (succeeded, value) = await harness
            .TryExecuteAsync(Audited("recovered-then-assertion"), cancellationToken => ReadScalarAsync(context, cancellationToken));

        succeeded.Should().BeTrue("the operation itself must recover on attempt 2 before the assertion runs");
        harness.Attempts.Should().Be(2);

        var state = SingleState(harness.Ledger);
        state.RecoveredOnSecondAttempt.Should().BeTrue("the operation recovered on the second attempt");

        WriteProbe(
            "recovered-then-assertion",
            $"attempts={harness.Attempts} operation_recovered={Lower(state.RecoveredOnSecondAttempt)} final=failed " +
            $"effective_seconds={effective.EffectiveSeconds} proxy_faulted={proxy.FaultedConnections}");

        var lines = WriteAccounting(harness.Ledger, finalOutcome: false);
        lines.Should().Contain(
            line => line.Contains("operation_recovered=true", StringComparison.Ordinal)
                && line.Contains("recovered=false", StringComparison.Ordinal)
                && line.Contains("final=failed", StringComparison.Ordinal),
            "a recovered operation followed by a failed assertion is not a recovered test");

        // Genuine assertion failure after the recovery: the summary above already proved the accounting.
        value.Should().Be(41L, "this assertion fails after a recovered operation");
    }

    // --- helpers ----------------------------------------------------------------------------------

    private static string BaseConnectionString() =>
        ClickHouseTestTimeout.Resolve(ClickHouseContainer.ConnectionString).ConnectionString;

    private static ClickHouseDataContext CreateContext(string connectionString) =>
        new(connectionString, new DataContextBuilder());

    /// <summary>
    /// One materialised scalar read through the provider and the real driver: a one-row result, fully
    /// read before the delegate returns, so the attempt is quiescent when the retrier observes it.
    /// </summary>
    private static async Task<long> ReadScalarAsync(IDataContext context, CancellationToken cancellationToken)
    {
        await using var result = await context
            .ExecuteRawAsync("select CAST(42 AS Int64) as v", [], cancellationToken);

        var values = result.Read<long>();
        values.Should().HaveCount(1, "the probe's read returns exactly one scalar row");
        return values[0];
    }

    private static ClickHouseReadOperation Audited(string scenario) =>
        ClickHouseReadOperation.Audited($"{TestIdentity}.{scenario}", $"{scenario}/select-42", ClickHouseReadCategory.PointRead);

    private static ClickHouseFailureClassification Classify(Exception? failure) =>
        failure is null
            ? throw new InvalidOperationException("T328: the probe observed no failure to classify.")
            : ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

    private static ClickHouseRetryLedgerState SingleState(ClickHouseRetryLedger ledger) =>
        ledger.Snapshot()
            .Should()
            .ContainSingle("each probe owns its ledger and claims exactly one retry-controlled operation")
            .Which;

    private static string Describe(Exception? failure)
    {
        if (failure is null)
            return "none";

        var names = new List<string>(4);
        for (Exception? current = failure; current is not null && names.Count < 8; current = current.InnerException)
            names.Add(current.GetType().Name);

        return string.Join(" > ", names);
    }

    private static string DescribeFailure(Exception? failure) =>
        failure is null ? "no failure observed" : $"{Describe(failure)} [{Classify(failure).Kind}]";

    /// <summary>
    /// A symbolic name for the server codes the probes can realistically observe. Only the symbolic
    /// name is printed; message bodies never are.
    /// </summary>
    private static string ErrorCodeName(int code) => code switch
    {
        47 => "unknown_identifier",
        60 => "unknown_table",
        62 => "syntax_error",
        81 => "unknown_database",
        159 => "timeout_exceeded",
        216 => "query_with_same_id_is_already_running",
        _ => $"code_{code.ToString(CultureInfo.InvariantCulture)}",
    };

    /// <summary>
    /// Keeps both statements of the code216 probe on one ClickHouse HTTP session, which is what makes a
    /// temporary table visible: the session ends with the context and the table with it.
    /// </summary>
    private static string WithSession(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder["UseSession"] = "true";
        builder["SessionId"] = $"t328-code216-{Guid.NewGuid():N}";
        return builder.ConnectionString;
    }

    private static void WriteProbe(string scenario, string fields) =>
        Console.WriteLine($"{ProbePrefix} scenario={scenario} {fields}");

    private static IReadOnlyList<string> WriteAccounting(ClickHouseRetryLedger ledger, bool? finalOutcome)
    {
        var lines = ClickHouseRetrySummary.Build(ledger, _ => finalOutcome);
        foreach (var line in lines)
            Console.WriteLine(line);

        return lines;
    }

    private static string Lower(bool value) => value ? "true" : "false";

    [DoesNotReturn]
    private static void Rethrow(Exception exception)
    {
        ExceptionDispatchInfo.Capture(exception).Throw();
        throw new InvalidOperationException("T328: ExceptionDispatchInfo.Throw returned; this is unreachable.");
    }

    /// <summary>
    /// One probe's retrier wiring: a private ledger, the observed attempt records and delays, and a
    /// read delegate that counts attempts and remembers the first and last failure. Nothing is shared
    /// with the suite-wide ledger, so a probe's accounting cannot be contaminated by another test.
    /// </summary>
    private sealed class ProbeRetrier
    {
        private readonly List<ClickHouseRetryAttemptRecord> _records = [];
        private readonly List<TimeSpan> _delays = [];

        internal ProbeRetrier()
        {
            Retrier = new ClickHouseReadRetrier(Record, Delay, Ledger);
        }

        internal ClickHouseRetryLedger Ledger { get; } = new();

        internal ClickHouseReadRetrier Retrier { get; }

        internal IReadOnlyList<ClickHouseRetryAttemptRecord> Records => _records;

        internal IReadOnlyList<TimeSpan> Delays => _delays;

        internal int Attempts { get; private set; }

        internal Exception? FirstFailure { get; private set; }

        internal Exception? LastFailure { get; private set; }

        internal Exception? Thrown { get; private set; }

        /// <summary>Runs one audited read through the retrier and captures the outcome instead of unwinding.</summary>
        internal async Task<(bool Succeeded, T Value)> TryExecuteAsync<T>(
            ClickHouseReadOperation operation,
            Func<CancellationToken, Task<T>> read)
        {
            try
            {
                var value = await Retrier.ExecuteAsync(
                    operation,
                    async cancellationToken =>
                    {
                        Attempts++;
                        var attempt = Attempts;

                        try
                        {
                            return await read(cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            if (attempt == 1)
                                FirstFailure = exception;
                            else
                                LastFailure = exception;

                            throw;
                        }
                    },
                    CancellationToken.None);

                return (true, value);
            }
            catch (Exception exception)
            {
                Thrown = exception;
                return (false, default!);
            }
        }

        private void Record(ClickHouseRetryAttemptRecord record)
        {
            _records.Add(record);
            Ledger.Record(record);
        }

        private Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            _delays.Add(delay);
            return Task.Delay(delay, cancellationToken);
        }
    }
}
