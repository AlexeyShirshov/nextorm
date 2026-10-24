using System.Data.Common;
using System.Net.Sockets;
using ClickHouse.Driver;
using FluentAssertions.Execution;
using Xunit.Sdk;

namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>The classifier's decision for one failed attempt. Only a positively established
/// client-side transport failure is retryable.</summary>
internal enum ClickHouseFailureKind
{
    /// <summary>A client HTTP timeout: an <see cref="OperationCanceledException"/> whose own inner
    /// chain carries the driver's <see cref="TimeoutException"/> while the caller token is active.</summary>
    ClientHttpTimeout,

    /// <summary>The caller's cancellation token is cancelled (or was cancelled during the attempt).</summary>
    CallerCancelled,

    /// <summary>A typed database/server error, including ClickHouse Code 216 / <c>UNKNOWN_TABLE</c>.</summary>
    ServerError,

    /// <summary>An assertion failure, directly or wrapped.</summary>
    AssertionFailure,

    /// <summary>An exception shape that is not positively recognised; always fail closed.</summary>
    UnknownShape,

    /// <summary>Accounting-only kind: an eligible failure met the attempt limit. Produced by the
    /// retrier's records for the accounting stream, never by <see cref="ClickHouseTransientClassifier"/>.</summary>
    AttemptBudgetExhausted,
}

/// <summary>
/// A sanitised classification result: a short reason token is exposed instead of the exception
/// message, so no message body, query or connection string can leak into the attempt records.
/// </summary>
internal readonly record struct ClickHouseFailureClassification(ClickHouseFailureKind Kind, string ReasonCode)
{
    /// <summary>True only for the positively established client HTTP timeout.</summary>
    public bool IsRetryable => Kind is ClickHouseFailureKind.ClientHttpTimeout;

    /// <summary>Accounting classification for an eligible failure that consumed the attempt limit.</summary>
    internal static ClickHouseFailureClassification Exhausted { get; } =
        new(ClickHouseFailureKind.AttemptBudgetExhausted, "attempt_budget_exhausted");
}

/// <summary>
/// Exclusion-first transient classifier for the ClickHouse test harness (T328 AC01/AC06/AC07, V01–V07/V17/V18).
/// <para>
/// The decision table is evaluated in this order and exclusions win over any positive match:
/// </para>
/// <list type="number">
/// <item>Caller token cancelled ⇒ <see cref="ClickHouseFailureKind.CallerCancelled"/>. The exception's
/// own token identity is deliberately not compared: the driver links its token to the caller's, so a
/// <c>TaskCanceledException</c> token is not evidence of caller intent (measured in D0).</item>
/// <item>Any chain element is a <see cref="DbException"/> (covers <see cref="ClickHouseServerException"/>,
/// <c>UNKNOWN_TABLE</c> and Code 216) ⇒ <see cref="ClickHouseFailureKind.ServerError"/>, even when the
/// message contains "timeout".</item>
/// <item>Any chain element is <see cref="XunitException"/> or
/// <see cref="AssertionFailedException"/> ⇒ <see cref="ClickHouseFailureKind.AssertionFailure"/>.</item>
/// <item>An <see cref="OperationCanceledException"/> whose own inner chain carries a
/// <see cref="TimeoutException"/>, caller token active ⇒ <see cref="ClickHouseFailureKind.ClientHttpTimeout"/>.
/// A bare timeout or a bare cancellation never matches.</item>
/// <item>Anything else — an <see cref="HttpRequestException"/> without a positive transport signal,
/// a bare <see cref="IOException"/>/<see cref="System.Net.Http.HttpIOException"/>, a reset-shaped
/// <see cref="ClickHouseServerException"/> (including <c>ErrorCode = -1</c>), an unknown wrapper type
/// anywhere in the chain, an <see cref="AggregateException"/>
/// wrapper, a truncated (deeper than the bound) chain, or a message-only lookalike
/// ⇒ <see cref="ClickHouseFailureKind.UnknownShape"/>. Reset-shaped faults fail closed: the driver
/// (1.4.0) exposes no typed reset signal distinct from a genuine server error, so they are never
/// retried (T328 V18 / DEF-328-04).</item>
/// </list>
/// <para>
/// The node allow-list for step 4 is the transport vocabulary measured live in D0b:
/// <see cref="OperationCanceledException"/> (and its <see cref="TaskCanceledException"/> subclass),
/// <see cref="TimeoutException"/>, <see cref="HttpRequestException"/>, <see cref="IOException"/> and
/// <see cref="SocketException"/>. <see cref="IOException"/> is understood but carries no positive
/// match of its own: it appears between the driver's cancellation wrapper and the socket on the
/// measured client-timeout chain, and a standalone <see cref="IOException"/> still fails closed.
/// An <see cref="AggregateException"/> node itself stays unrecognised (its inners are still walked),
/// so an aggregate wrapper keeps failing closed.
/// </para>
/// <para>
/// Chain walking is cycle-safe (reference identity) and bounded to <see cref="MaxChainDepth"/> nodes;
/// <see cref="AggregateException.InnerExceptions"/> is traversed. A chain that exceeds the bound is
/// treated as unknown and never retried.
/// </para>
/// </summary>
internal static class ClickHouseTransientClassifier
{
    internal const string ClientHttpTimeoutReason = "client_http_timeout";
    internal const string CallerCancelledReason = "caller_cancelled";
    internal const string ServerErrorReason = "server_error";
    internal const string AssertionFailureReason = "assertion_failure";
    internal const string UnknownShapeReason = "unknown_shape";

    private const int MaxChainDepth = 16;

    /// <summary>Classifies a failed attempt. The caller token is checked first and overrides every
    /// other shape; an unrecognised shape is never retryable.</summary>
    internal static ClickHouseFailureClassification Classify(Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(exception);

        // 1. Caller cancellation wins over everything, including a server error or an assertion.
        //    Do not compare exception token identity: the driver's token is linked to ours.
        if (cancellationToken.IsCancellationRequested)
            return new ClickHouseFailureClassification(ClickHouseFailureKind.CallerCancelled, CallerCancelledReason);

        if (!TryFlatten(exception, out var chain))
            return new ClickHouseFailureClassification(ClickHouseFailureKind.UnknownShape, UnknownShapeReason);

        // 2. Typed server error. ClickHouseServerException is a DbException, but the explicit
        //    check keeps the driver contract visible and survives a base-type change.
        foreach (var node in chain)
        {
            if (node is ClickHouseServerException || node is DbException)
                return new ClickHouseFailureClassification(ClickHouseFailureKind.ServerError, ServerErrorReason);
        }

        // 3. Assertion failure, direct or wrapped.
        foreach (var node in chain)
        {
            if (node is XunitException || node is AssertionFailedException)
                return new ClickHouseFailureClassification(ClickHouseFailureKind.AssertionFailure, AssertionFailureReason);
        }

        // 4. Positive match: only the measured client-timeout chain. Every node must be a shape this
        //    classifier understands; an unknown wrapper anywhere in the chain fails closed. IOException
        //    (and its HttpIOException subclass) is a tolerated transport node on the measured live
        //    timeout chain, but it is never a positive match on its own. A reset-shaped fault has no
        //    typed signal in driver 1.4.0 (DEF-328-04) and therefore falls through to UnknownShape.
        foreach (var node in chain)
        {
            if (node is not (OperationCanceledException or TimeoutException or HttpRequestException or IOException or SocketException))
                return new ClickHouseFailureClassification(ClickHouseFailureKind.UnknownShape, UnknownShapeReason);
        }

        var timeoutMatches = 0;
        foreach (var node in chain)
        {
            if (node is OperationCanceledException && InnerChainContainsTimeout(node))
                timeoutMatches++;
        }

        if (timeoutMatches == 1)
            return new ClickHouseFailureClassification(ClickHouseFailureKind.ClientHttpTimeout, ClientHttpTimeoutReason);

        return new ClickHouseFailureClassification(ClickHouseFailureKind.UnknownShape, UnknownShapeReason);
    }

    /// <summary>Walks the exception chain (including every <see cref="AggregateException"/> inner and
    /// <see cref="Exception.InnerException"/>) up to the depth bound. Returns false when the chain is
    /// deeper than the bound, in which case the shape is not established.</summary>
    private static bool TryFlatten(Exception root, out List<Exception> nodes)
    {
        nodes = new List<Exception>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<(Exception Node, int Depth)>();
        pending.Push((root, 0));

        while (pending.Count > 0)
        {
            var (node, depth) = pending.Pop();

            if (depth > MaxChainDepth)
                return false;

            if (!visited.Add(node))
                continue;

            nodes.Add(node);

            if (node is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                    pending.Push((inner, depth + 1));
            }
            else if (node.InnerException is { } inner)
            {
                pending.Push((inner, depth + 1));
            }
        }

        return true;
    }

    /// <summary>Follows only the linear <see cref="Exception.InnerException"/> chain of the node and
    /// reports whether a <see cref="TimeoutException"/> is on it. Cycle-safe and depth-bounded.</summary>
    private static bool InnerChainContainsTimeout(Exception node)
    {
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var current = node.InnerException;

        for (var depth = 0; current is not null && depth <= MaxChainDepth; depth++)
        {
            if (!visited.Add(current))
                return false;

            if (current is TimeoutException)
                return true;

            current = current.InnerException;
        }

        return false;
    }
}
