using FluentAssertions;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Core.Tests.ClickHouseRetry;

/// <summary>
/// Accounting contract (T328 AC03/AC05, V08/V14): the ledger correlates attempt records with the
/// runner's final outcomes, and the summary reports retried tests, additional attempts, recovered
/// tests and exhausted retries without ever inferring a recovery. All cases are DB-free.
/// </summary>
public sealed class ClickHouseRetryAccountingTests
{
    private const string TestIdentity = "NextORM.Integration.Tests.ExampleTests.ReadsRows";

    [Fact]
    public async Task Build_ShouldReportRecoveredTest_WhenTheSecondAttemptReturnsAndTheTestPasses()
    {
        var ledger = new ClickHouseRetryLedger();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(record => ledger.Record(record), (_, _) => Task.CompletedTask, ledger);

        var result = await retrier.ExecuteAsync(
            Operation(TestIdentity),
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

        var lines = ClickHouseRetrySummary.Build(ledger, _ => true);

        lines.Should().HaveCount(2);
        lines[0].Should().Be(
            "CLICKHOUSE-RETRY-TEST test=NextORM.Integration.Tests.ExampleTests.ReadsRows attempts=2 " +
            "retried=true operation_recovered=true final=passed recovered=true");
        lines[1].Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=1 additional_attempts=1 recovered_tests=1 " +
            "exhausted_retries=0 not_recovered_tests=0 unretried_failures=0 attempted_once=0");
    }

    [Fact]
    public async Task Build_ShouldNotCountARecovery_WhenTheRecoveredOperationIsFollowedByAnAssertionFailure()
    {
        var ledger = new ClickHouseRetryLedger();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(record => ledger.Record(record), (_, _) => Task.CompletedTask, ledger);

        await retrier.ExecuteAsync(
            Operation(TestIdentity),
            _ =>
            {
                invocations++;
                return invocations == 1
                    ? Task.FromException<string>(DriverTimeoutChain())
                    : Task.FromResult("recovered");
            },
            CancellationToken.None);

        // The operation recovered, but the test itself failed its assertion afterwards.
        var lines = ClickHouseRetrySummary.Build(ledger, _ => false);

        lines[0].Should().Be(
            "CLICKHOUSE-RETRY-TEST test=NextORM.Integration.Tests.ExampleTests.ReadsRows attempts=2 " +
            "retried=true operation_recovered=true final=failed recovered=false");
        lines[1].Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=1 additional_attempts=1 recovered_tests=0 " +
            "exhausted_retries=0 not_recovered_tests=1 unretried_failures=0 attempted_once=0");
    }

    [Fact]
    public async Task Build_ShouldReportExhaustion_WhenTheSecondAttemptFails()
    {
        var ledger = new ClickHouseRetryLedger();
        var invocations = 0;
        var retrier = new ClickHouseReadRetrier(record => ledger.Record(record), (_, _) => Task.CompletedTask, ledger);

        Func<Task> act = async () => await retrier.ExecuteAsync<string>(
            Operation(TestIdentity),
            _ =>
            {
                invocations++;
                return Task.FromException<string>(DriverTimeoutChain());
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<TaskCanceledException>();
        invocations.Should().Be(2);

        var lines = ClickHouseRetrySummary.Build(ledger, _ => false);

        lines[0].Should().Be(
            "CLICKHOUSE-RETRY-TEST test=NextORM.Integration.Tests.ExampleTests.ReadsRows attempts=2 " +
            "retried=true operation_recovered=false final=failed recovered=false");
        lines[1].Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=1 additional_attempts=1 recovered_tests=0 " +
            "exhausted_retries=1 not_recovered_tests=1 unretried_failures=0 attempted_once=0");
    }

    [Fact]
    public void Build_ShouldReportAnUnretriedFailure_ForATestThatNeverRetried()
    {
        var ledger = new ClickHouseRetryLedger();

        // A non-retryable first-attempt failure: one claim, no retry records.
        ledger.TryClaimOperation(TestIdentity, "read").Should().BeTrue();

        var lines = ClickHouseRetrySummary.Build(ledger, _ => false);

        lines[0].Should().Be(
            "CLICKHOUSE-RETRY-TEST test=NextORM.Integration.Tests.ExampleTests.ReadsRows attempts=1 " +
            "retried=false operation_recovered=false final=failed recovered=false");
        lines[1].Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=0 additional_attempts=0 recovered_tests=0 " +
            "exhausted_retries=0 not_recovered_tests=0 unretried_failures=1 attempted_once=1");
    }

    [Fact]
    public async Task Build_ShouldKeepConcurrentTestsSeparate_AndCountBothAdditionalAttempts()
    {
        var ledger = new ClickHouseRetryLedger();
        var bothInBackoff = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;

        async Task RunAsync(string testIdentity)
        {
            var invocations = 0;
            var retrier = new ClickHouseReadRetrier(
                record => ledger.Record(record),
                async (_, _) =>
                {
                    if (Interlocked.Increment(ref arrived) == 2)
                        bothInBackoff.SetResult();

                    // Both tests complete their backoff only once the other one is there too.
                    await bothInBackoff.Task.ConfigureAwait(false);
                },
                ledger);

            await retrier.ExecuteAsync(
                Operation(testIdentity),
                _ =>
                {
                    invocations++;
                    return invocations == 1
                        ? Task.FromException<string>(DriverTimeoutChain())
                        : Task.FromResult("recovered");
                },
                CancellationToken.None);
        }

        await Task.WhenAll(
            Task.Run(() => RunAsync("NextORM.Tests.Alpha.Passes"), TestContext.Current.CancellationToken),
            Task.Run(() => RunAsync("NextORM.Tests.Beta.Passes"), TestContext.Current.CancellationToken));

        var lines = ClickHouseRetrySummary.Build(ledger, _ => true);

        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("CLICKHOUSE-RETRY-TEST test=NextORM.Tests.Alpha.Passes attempts=2 retried=true");
        lines[1].Should().StartWith("CLICKHOUSE-RETRY-TEST test=NextORM.Tests.Beta.Passes attempts=2 retried=true");
        lines[2].Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=2 additional_attempts=2 recovered_tests=2 " +
            "exhausted_retries=0 not_recovered_tests=0 unretried_failures=0 attempted_once=0");
    }

    [Fact]
    public void Build_ShouldTreatParameterisedIdentitiesAsDistinctTests()
    {
        var ledger = new ClickHouseRetryLedger();

        ledger.TryClaimOperation("NextORM.Tests.Theory(x: 1)", "read").Should().BeTrue();
        ledger.TryClaimOperation("NextORM.Tests.Theory(x: 2)", "read").Should().BeTrue();
        ledger.Record(new ClickHouseRetryAttemptRecord(
            "NextORM.Tests.Theory(x: 2)", "read", 1, 2, "client_http_timeout", ScheduledRetry: true));
        ledger.Record(new ClickHouseRetryAttemptRecord(
            "NextORM.Tests.Theory(x: 2)", "read", 2, 2, ClickHouseReadRetrier.SucceededReasonCode,
            ScheduledRetry: false, Succeeded: true));

        var lines = ClickHouseRetrySummary.Build(ledger, _ => true);

        lines.Should().HaveCount(3);
        lines[0].Should().StartWith("CLICKHOUSE-RETRY-TEST test=NextORM.Tests.Theory(x: 1) attempts=1 retried=false");
        lines[1].Should().StartWith("CLICKHOUSE-RETRY-TEST test=NextORM.Tests.Theory(x: 2) attempts=2 retried=true");
        lines[2].Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=1 additional_attempts=1 recovered_tests=1 " +
            "exhausted_retries=0 not_recovered_tests=0 unretried_failures=0 attempted_once=1");
    }

    [Fact]
    public async Task Retrier_ShouldRejectASecondOperation_ForTheSameTest_BeforeItRuns()
    {
        var ledger = new ClickHouseRetryLedger();
        var retrier = new ClickHouseReadRetrier(_ => { }, (_, _) => Task.CompletedTask, ledger);

        await retrier.ExecuteAsync(Operation(TestIdentity), _ => Task.FromResult(1), CancellationToken.None);

        var secondOperationRan = false;
        Func<Task> act = async () => await retrier.ExecuteAsync(
            Operation(TestIdentity),
            _ =>
            {
                secondOperationRan = true;
                return Task.FromResult(2);
            },
            CancellationToken.None);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage($"*{TestIdentity}*only one retry-controlled operation is permitted per test invocation*");

        secondOperationRan.Should().BeFalse();
        ledger.TryClaimOperation(TestIdentity, "another.read").Should().BeFalse();
    }

    [Fact]
    public void Build_ShouldEmitASingleZeroSummaryLine_ForAnEmptyLedger()
    {
        var lines = ClickHouseRetrySummary.Build(new ClickHouseRetryLedger(), _ => null);

        lines.Should().ContainSingle().Which.Should().Be(
            "CLICKHOUSE-RETRY-SUMMARY retried_tests=0 additional_attempts=0 recovered_tests=0 " +
            "exhausted_retries=0 not_recovered_tests=0 unretried_failures=0 attempted_once=0");
    }

    private static ClickHouseReadOperation Operation(string testIdentity) =>
        ClickHouseReadOperation.Audited(testIdentity, "read", ClickHouseReadCategory.PointRead);

    private static TaskCanceledException DriverTimeoutChain() =>
        new(
            "The request was canceled due to the configured HttpClient.Timeout of 120 seconds elapsing.",
            new TimeoutException(
                "The request was canceled due to the configured HttpClient.Timeout of 120 seconds elapsing.",
                new TaskCanceledException("A task was canceled.")));
}
