using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;

namespace NextORM.Tests.Shared.ClickHouseRetry;

/// <summary>Where the effective ClickHouse HTTP timeout came from.</summary>
internal enum ClickHouseTestTimeoutSource
{
    /// <summary>The <c>NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS</c> environment variable supplied it.</summary>
    Knob,

    /// <summary>The resolved connection string already carried a <c>Timeout</c> key.</summary>
    ConnectionString,

    /// <summary>Neither source supplied a timeout, so the driver default (120 seconds) applies.</summary>
    DriverDefault,
}

/// <summary>
/// A supplied <c>NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS</c> value that is not a positive 32-bit
/// integer. A configuration failure never falls back to the driver default or to the connection
/// string, and the rejected value is only echoed when it is safe to print.
/// </summary>
internal sealed class ClickHouseTestTimeoutConfigurationException : InvalidOperationException
{
    internal ClickHouseTestTimeoutConfigurationException(string message)
        : base(message)
    {
    }
}

/// <summary>The immutable outcome of resolving the effective ClickHouse test timeout.</summary>
internal sealed class ClickHouseTestTimeoutResult
{
    private int _diagnosticEmitted;

    internal ClickHouseTestTimeoutResult(
        string connectionString,
        int? effectiveSeconds,
        bool isInfinite,
        ClickHouseTestTimeoutSource source,
        bool overriddenExistingTimeout,
        string? diagnosticText)
    {
        ConnectionString = connectionString;
        EffectiveSeconds = effectiveSeconds;
        IsInfinite = isInfinite;
        Source = source;
        OverriddenExistingTimeout = overriddenExistingTimeout;
        DiagnosticText = diagnosticText;
    }

    /// <summary>
    /// The connection string every ClickHouse test context must use. When no knob is supplied this is
    /// the operator's string, byte-identical (the very same instance); when a knob is supplied it is
    /// the same string with <c>Timeout</c> explicitly set to the validated value.
    /// </summary>
    public string ConnectionString { get; }

    /// <summary>The effective timeout in whole seconds, or <c>null</c> when <see cref="IsInfinite"/>.</summary>
    public int? EffectiveSeconds { get; }

    /// <summary>True when the effective timeout is infinite (a connection-string <c>Timeout=0</c>).</summary>
    public bool IsInfinite { get; }

    /// <summary>Which of the three precedence levels produced the effective timeout.</summary>
    public ClickHouseTestTimeoutSource Source { get; }

    /// <summary>True when a knob replaced a timeout that was already present in the connection string.</summary>
    public bool OverriddenExistingTimeout { get; }

    /// <summary>
    /// The credential-free override diagnostic that is written to the console, or <c>null</c> when no
    /// timeout was overridden. Exposed so a test can assert its exact fields without capturing stdout.
    /// </summary>
    public string? DiagnosticText { get; }

    /// <summary>True when this result is the one that wrote <see cref="DiagnosticText"/>.</summary>
    public bool DiagnosticEmitted => Volatile.Read(ref _diagnosticEmitted) != 0;

    /// <summary>The number of console diagnostics written for this result (0 or 1).</summary>
    public int DiagnosticEmitCount => Volatile.Read(ref _diagnosticEmitted);

    /// <summary>
    /// Writes the override diagnostic at most once per resolved configuration. The harness resolves a
    /// single ClickHouse connection string per process, so this is the required exactly-one diagnostic
    /// per process, and a memoised input can never emit a second one.
    /// </summary>
    internal void EmitDiagnosticOnce()
    {
        var text = DiagnosticText;
        if (text is null)
            return;

        if (Interlocked.Exchange(ref _diagnosticEmitted, 1) != 0)
            return;

        Console.WriteLine(text);
    }
}

/// <summary>
/// Resolves the effective ClickHouse HTTP timeout for the integration test harness with an explicit
/// three-level precedence: <c>NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS</c> wins over a <c>Timeout</c>
/// already present in the resolved connection string, which wins over the driver default of 120
/// seconds. The value is resolved explicitly and written into the string; duplicate-key ordering is
/// never relied on.
/// <para>
/// The knob must be a positive 32-bit integer (no sign, no leading whitespace, no fraction, no
/// exponent, no overflow). Anything else is a <see cref="ClickHouseTestTimeoutConfigurationException"/>
/// - there is no fallback. The string itself is only rewritten when the knob is present; without it the
/// operator's string is returned unchanged, including <c>Timeout=0</c>, which stays infinite.
/// </para>
/// </summary>
internal static class ClickHouseTestTimeout
{
    /// <summary>Names the knob so tests and diagnostics never spell the variable twice.</summary>
    internal const string EnvironmentVariable = "NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS";

    /// <summary>The driver's own default when neither the knob nor the connection string sets a timeout.</summary>
    internal const int DriverDefaultSeconds = 120;

    private static readonly ConcurrentDictionary<(string ConnectionString, string? Knob), ClickHouseTestTimeoutResult> Cache =
        new();

    /// <summary>
    /// Resolves the effective timeout from the environment. This is the only overload that reads the
    /// environment, so the deterministic overload below stays free of ambient state.
    /// </summary>
    internal static ClickHouseTestTimeoutResult Resolve(string connectionString) =>
        Resolve(connectionString, Environment.GetEnvironmentVariable(EnvironmentVariable));

    /// <summary>
    /// Resolves the effective timeout from an explicit knob value (<c>null</c> means "not set").
    /// Results are memoised per distinct input pair, so repeated context creation does the work once
    /// and the override diagnostic can never be emitted twice for the same configuration.
    /// </summary>
    internal static ClickHouseTestTimeoutResult Resolve(string connectionString, string? knobValue)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        // Validate before touching the cache: an invalid knob must fail on every call, never be
        // memoised into a usable result.
        if (knobValue is not null)
            ParseKnob(knobValue);

        var result = Cache.GetOrAdd((connectionString, knobValue), static key => Build(key.ConnectionString, key.Knob));
        result.EmitDiagnosticOnce();
        return result;
    }

    private static ClickHouseTestTimeoutResult Build(string connectionString, string? knobValue)
    {
        if (knobValue is not null)
        {
            var seconds = ParseKnob(knobValue);
            var existing = ReadExistingTimeout(connectionString, out var existingSeconds);
            // A key that is present but unusable is still replaced, so it is an override too; a
            // non-numeric value must never make the comparison throw.
            var overridden = existing != ExistingTimeoutState.Absent
                && (existing == ExistingTimeoutState.Unusable || existingSeconds != seconds);
            var effective = SetTimeout(connectionString, seconds);

            return new ClickHouseTestTimeoutResult(
                effective,
                seconds,
                isInfinite: false,
                ClickHouseTestTimeoutSource.Knob,
                overridden,
                overridden ? BuildOverrideDiagnostic(seconds) : null);
        }

        if (ReadExistingTimeout(connectionString, out var preserved) == ExistingTimeoutState.Usable)
        {
            // The operator's string is returned byte-identical: this branch neither validates nor
            // rewrites it. Timeout=0 is the driver's "infinite" sentinel and is preserved as such.
            return preserved == 0
                ? new ClickHouseTestTimeoutResult(
                    connectionString, null, isInfinite: true, ClickHouseTestTimeoutSource.ConnectionString, false, null)
                : new ClickHouseTestTimeoutResult(
                    connectionString, ProjectSeconds(preserved), isInfinite: false, ClickHouseTestTimeoutSource.ConnectionString, false, null);
        }

        // No key, or a value the driver itself falls back from (non-numeric) or rejects (negative):
        // the string is left untouched and the driver default is reported.
        return new ClickHouseTestTimeoutResult(
            connectionString,
            DriverDefaultSeconds,
            isInfinite: false,
            ClickHouseTestTimeoutSource.DriverDefault,
            false,
            null);
    }


    private static int ParseKnob(string value)
    {
        // NumberStyles.None accepts digits only: "+5", "-5", " 5", "1.5", "1e3" and overflow all fail.
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
            throw new ClickHouseTestTimeoutConfigurationException(BuildInvalidKnobMessage(value));

        return seconds;
    }

    private static string BuildInvalidKnobMessage(string value)
    {
        const string Domain =
            "a positive 32-bit integer number of seconds (for example 30 or 180); "
            + "empty, whitespace-only, zero, negative, fractional and overflowing values are rejected";

        var subject = IsEchoSafe(value)
            ? $"{EnvironmentVariable} is invalid: '{value}'"
            : $"{EnvironmentVariable} is set to an invalid value, which is not echoed because it may contain credentials";

        return $"{subject}. Accepted: {Domain}. The value is not used and no fallback is applied.";
    }

    /// <summary>
    /// A rejected value is only repeated when it cannot be, or carry, credential material: printable
    /// ASCII, short, and free of separators and of words that suggest a secret.
    /// </summary>
    private static bool IsEchoSafe(string value)
    {
        if (value.Length is 0 or > 32)
            return false;

        foreach (var character in value)
        {
            if (character is < ' ' or > '~')
                return false;
            if (character is '=' or ';' or '@' or ':' or '/' or '\\' or '\'' or '"')
                return false;
        }

        return !value.Contains("pass", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("pwd", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !value.Contains("user", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildOverrideDiagnostic(int seconds) =>
        $"{EnvironmentVariable} override: timeout_source=knob effective_seconds={seconds.ToString(CultureInfo.InvariantCulture)} "
        + "connection_string_timeout_overridden=true";

    /// <summary>
    /// Reads a <c>Timeout</c> key (case-insensitive) without validating or rewriting anything. A key
    /// that is present but unusable (non-numeric, negative, NaN or infinite) is reported as such, so
    /// the knob branch can count it as an override instead of comparing against a non-number; the
    /// absent-knob branch still leaves the operator's string untouched.
    /// </summary>
    private static ExistingTimeoutState ReadExistingTimeout(string connectionString, out double seconds)
    {
        seconds = 0;

        string? raw;
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            if (!builder.TryGetValue("Timeout", out var value) || value is null)
                return ExistingTimeoutState.Absent;

            raw = Convert.ToString(value, CultureInfo.InvariantCulture);
        }
        catch (ArgumentException)
        {
            return ExistingTimeoutState.Absent;
        }

        if (raw is null
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || double.IsNaN(parsed)
            || double.IsInfinity(parsed)
            || parsed < 0)
        {
            return ExistingTimeoutState.Unusable;
        }

        seconds = parsed;
        return ExistingTimeoutState.Usable;
    }


    /// <summary>Projects the driver's fractional seconds onto the reported whole-second value.</summary>
    private static int ProjectSeconds(double seconds)
    {
        var truncated = Math.Truncate(seconds);
        if (truncated < 1)
            return 1;

        return truncated > int.MaxValue ? int.MaxValue : (int)truncated;
    }

    /// <summary>
    /// Writes the knob value into the string through the BCL builder and returns the rebuilt string.
    /// Setting the key explicitly means an existing <c>Timeout</c> is replaced by value, never by
    /// duplicate-key ordering. The builder normalises key casing (keys render lowercase); key matching
    /// stays case-insensitive, so the effective string is semantically the operator's.
    /// </summary>
    private static string SetTimeout(string connectionString, int seconds)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder["Timeout"] = seconds.ToString(CultureInfo.InvariantCulture);
        return builder.ConnectionString;
    }

    private enum ExistingTimeoutState
    {
        Absent,
        Usable,
        Unusable,
    }
}
