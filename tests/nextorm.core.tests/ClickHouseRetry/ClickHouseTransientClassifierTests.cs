using System.Data.Common;
using System.Net.Sockets;
using ClickHouse.Driver;
using FluentAssertions;
using NextORM.Tests.Shared.ClickHouseRetry;
using System.Net.Http;

namespace NextORM.Core.Tests.ClickHouseRetry;

/// <summary>
/// Exclusion-first classifier contract (T328 AC01/AC06/AC07, V01/V02/V05/V06/V07/V17). Every shape
/// that is not a positively established client transport failure must fail closed.
/// </summary>
public sealed class ClickHouseTransientClassifierTests
{
    [Fact]
    public void Classify_ShouldReportClientHttpTimeout_ForTheMeasuredDriverTimeoutChain()
    {
        var classification = ClickHouseTransientClassifier.Classify(DriverTimeoutChain(), CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.ClientHttpTimeout);
        classification.ReasonCode.Should().Be("client_http_timeout");
        classification.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void Classify_ShouldFailClosed_WhenTheDriverTimeoutChainIsWrappedInAnUnknownType()
    {
        var wrapped = new InvalidOperationException("wrapper", DriverTimeoutChain());

        var classification = ClickHouseTransientClassifier.Classify(wrapped, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.UnknownShape);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportClientHttpTimeout_ForTheMeasuredLiveChainCarryingAnIoException()
    {
        // The exact chain measured live in C05 (type names only, no messages): the driver's own
        // cancellation wrapper around its TimeoutException, around a TaskCanceledException, around an
        // IOException carrying the reset socket. Before the IOException node was tolerated this shape
        // was rejected as UnknownShape and the retry never happened.
        var measured = new TaskCanceledException(
            "measured live timeout chain",
            new TimeoutException(
                "measured live timeout chain",
                new TaskCanceledException(
                    "measured live timeout chain",
                    new IOException(
                        "measured live timeout chain",
                        new SocketException((int)SocketError.ConnectionReset)))));

        var classification = ClickHouseTransientClassifier.Classify(measured, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.ClientHttpTimeout);
        classification.ReasonCode.Should().Be("client_http_timeout");
        classification.IsRetryable.Should().BeTrue();
    }

    [Fact]
    public void Classify_ShouldFailClosed_ForAStandaloneIoExceptionAndAStandaloneSocketException()
    {
        var shapes = new (string Name, Exception Failure)[]
        {
            ("standalone IOException", new IOException("transport failure")),
            ("standalone reset SocketException", new SocketException((int)SocketError.ConnectionReset)),
        };

        foreach (var (name, failure) in shapes)
        {
            var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

            classification.Kind.Should().Be(
                ClickHouseFailureKind.UnknownShape,
                $"{name} is tolerated as a transport node but is never a positive match on its own");
            classification.IsRetryable.Should().BeFalse($"{name} must never be retried");
        }
    }

    [Fact]
    public void Classify_ShouldFailClosed_ForAnHttpRequestExceptionWithoutASocket()
    {
        var classification = ClickHouseTransientClassifier.Classify(new HttpRequestException("reset"), CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.UnknownShape);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportServerError_ForAResetShapedDriverExceptionWithMinusOneCode()
    {
        // Measured live (D0b / DEF-328-04): a pre-response RST surfaces as ClickHouseServerException
        // with ErrorCode -1 and no inner chain. Because -1 also marks an unparseable genuine server
        // error, the fail-closed classification is ServerError — never a retry (V18).
        var failure = new ClickHouseServerException("Code: -1. (transport reset before response)", "select 42", -1);

        var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.ServerError);
        classification.ReasonCode.Should().Be("server_error");
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldFailClosed_ForABareHttpIoException()
    {
        // Measured live (D0b / DEF-328-04): an RST after a partial response surfaces as a bare
        // HttpIOException — no HttpRequestException, no SocketException — with no attributable code.
        var failure = new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");

        var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.UnknownShape);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldFailClosed_ForAnHttpRequestExceptionWithoutASocketSignal()
    {
        var failure = new HttpRequestException("The response ended prematurely.", new IOException("connection closed"));

        var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.UnknownShape);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportCallerCancelled_WhenTheTokenIsAlreadyCancelled()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var classification = ClickHouseTransientClassifier.Classify(DriverTimeoutChain(), cancellation.Token);

        classification.Kind.Should().Be(ClickHouseFailureKind.CallerCancelled);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldPreferCallerCancellation_OverATypedServerError()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var serverError = new ClickHouseServerException("Code: 216. DB::Exception: timeout", "SELECT 1", 216);

        var classification = ClickHouseTransientClassifier.Classify(serverError, cancellation.Token);

        classification.Kind.Should().Be(ClickHouseFailureKind.CallerCancelled);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportServerError_ForATypedDriverServerExceptionWithATimeoutLikeMessage()
    {
        var failure = new ClickHouseServerException(
            "Code: 216. DB::Exception: Table default.missing does not exist. The request timed out (UNKNOWN_TABLE)",
            "SELECT * FROM default.missing",
            216);

        var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.ServerError);
        classification.ReasonCode.Should().Be("server_error");
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportServerError_ForAnyDbExceptionSubclass()
    {
        var classification = ClickHouseTransientClassifier.Classify(
            new TestDbException("connection timeout while executing"),
            CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.ServerError);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportAssertionFailure_ForARealFluentAssertionsFailure()
    {
        var assertionFailure = CaptureFluentAssertionsFailure();

        var classification = ClickHouseTransientClassifier.Classify(assertionFailure, CancellationToken.None);

        classification.Kind.Should().Be(ClickHouseFailureKind.AssertionFailure);
        classification.ReasonCode.Should().Be("assertion_failure");
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldReportAssertionFailure_WhenTheAssertionIsWrapped()
    {
        var wrapped = new InvalidOperationException("wrapper", CaptureFluentAssertionsFailure());

        var classification = ClickHouseTransientClassifier.Classify(wrapped, CancellationToken.None);

        // Exclusions win over the wrapper rule: assertion identity is established through the chain.
        classification.Kind.Should().Be(ClickHouseFailureKind.AssertionFailure);
        classification.IsRetryable.Should().BeFalse();
    }

    [Fact]
    public void Classify_ShouldFailClosed_ForEveryMessageOnlyOrBareCancellationShape()
    {
        var shapes = new (string Name, Exception Failure)[]
        {
            ("bare TimeoutException", new TimeoutException("The request timed out.")),
            ("bare OperationCanceledException", new OperationCanceledException("A task was canceled.")),
            ("bare TaskCanceledException", new TaskCanceledException("A task was canceled.")),
            ("InvalidOperationException with a timeout message", new InvalidOperationException("request timed out")),
            ("plain Exception with a timeout message", new Exception("... timed out ...")),
            ("IOException with a reset message", new IOException("connection reset by peer")),
            ("AggregateException wrapping the timeout chain", new AggregateException(DriverTimeoutChain())),
        };

        foreach (var (name, failure) in shapes)
        {
            var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

            classification.Kind.Should().Be(
                ClickHouseFailureKind.UnknownShape,
                $"{name} is not a positively established client transport failure");
            classification.IsRetryable.Should().BeFalse($"{name} must never be retried");
        }
    }

    [Fact]
    public void Classify_ShouldFailClosed_ForUnknownWrappersAndUnknownSocketForms()
    {
        var shapes = new (string Name, Exception Failure)[]
        {
            ("IOException wrapping a reset socket", new IOException("transport failure", new SocketException((int)SocketError.ConnectionReset))),
            ("bare reset SocketException", new SocketException((int)SocketError.ConnectionReset)),
            ("HTTP exception with a non-reset socket error", new HttpRequestException("timeout", new SocketException((int)SocketError.TimedOut))),
            ("AggregateException wrapping a reset", new AggregateException(new HttpRequestException("reset", new SocketException((int)SocketError.ConnectionReset)))),
        };

        foreach (var (name, failure) in shapes)
        {
            var classification = ClickHouseTransientClassifier.Classify(failure, CancellationToken.None);

            classification.Kind.Should().Be(ClickHouseFailureKind.UnknownShape, $"{name} is not a recognised shape");
            classification.IsRetryable.Should().BeFalse($"{name} must never be retried");
        }
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

    private sealed class TestDbException : DbException
    {
        public TestDbException(string message)
            : base(message, 216)
        {
        }
    }
}
