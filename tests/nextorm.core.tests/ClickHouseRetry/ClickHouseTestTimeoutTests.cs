using FluentAssertions;
using NextORM.Tests.Shared.ClickHouseRetry;

namespace NextORM.Core.Tests.ClickHouseRetry;

/// <summary>
/// DB-free contract of the ClickHouse test timeout resolver (T328 D1, contract row EV08):
/// <c>NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS</c> &gt; a <c>Timeout</c> already present in the resolved
/// connection string &gt; the driver default of 120 seconds, with a strict knob domain and one
/// credential-free override diagnostic.
/// </summary>
public sealed class ClickHouseTestTimeoutTests
{
    private const string Knob = ClickHouseTestTimeout.EnvironmentVariable;

    [Fact]
    public void Resolve_ShouldUseTheDriverDefault_WhenNeitherKnobNorConnectionStringTimeoutIsSet()
    {
        var connectionString = Fresh("Host=clickhouse-default.local;Port=8123;Username=default;Password=default-secret");

        var result = ClickHouseTestTimeout.Resolve(connectionString, knobValue: null);

        result.Source.Should().Be(ClickHouseTestTimeoutSource.DriverDefault);
        result.EffectiveSeconds.Should().Be(120);
        result.IsInfinite.Should().BeFalse();
        result.OverriddenExistingTimeout.Should().BeFalse();
        result.DiagnosticText.Should().BeNull();
        result.ConnectionString.Should().BeSameAs(connectionString);
    }

    [Fact]
    public void Resolve_ShouldPreserveAnExistingConnectionStringTimeout_WhenTheKnobIsAbsent()
    {
        var connectionString = Fresh("Host=clickhouse-existing.local;Port=8123;Timeout=30;Password=existing-secret");

        var result = ClickHouseTestTimeout.Resolve(connectionString, knobValue: null);

        result.Source.Should().Be(ClickHouseTestTimeoutSource.ConnectionString);
        result.EffectiveSeconds.Should().Be(30);
        result.IsInfinite.Should().BeFalse();
        result.OverriddenExistingTimeout.Should().BeFalse();
        result.DiagnosticText.Should().BeNull();
        result.ConnectionString.Should().BeSameAs(connectionString);
    }

    [Fact]
    public void Resolve_ShouldTreatAnExistingZeroTimeoutAsInfinite_WhenTheKnobIsAbsent()
    {
        var connectionString = Fresh("Host=clickhouse-infinite.local;Port=8123;Timeout=0");

        var result = ClickHouseTestTimeout.Resolve(connectionString, knobValue: null);

        result.Source.Should().Be(ClickHouseTestTimeoutSource.ConnectionString);
        result.IsInfinite.Should().BeTrue();
        result.EffectiveSeconds.Should().BeNull();
        result.DiagnosticText.Should().BeNull();
        result.ConnectionString.Should().BeSameAs(connectionString);
    }

    [Theory]
    [InlineData("timeout=30")]
    [InlineData("TIMEOUT=30")]
    [InlineData("TimeOut=30")]
    public void Resolve_ShouldReadTheConnectionStringTimeoutKeyCaseInsensitively(string timeoutPair)
    {
        var connectionString = Fresh(string.Concat("Host=clickhouse-case.local;Port=8123;", timeoutPair));

        var result = ClickHouseTestTimeout.Resolve(connectionString, knobValue: null);

        result.Source.Should().Be(ClickHouseTestTimeoutSource.ConnectionString);
        result.EffectiveSeconds.Should().Be(30);
        result.IsInfinite.Should().BeFalse();
        result.ConnectionString.Should().BeSameAs(connectionString);
    }

    [Fact]
    public void Resolve_ShouldApplyTheKnob_WhenTheConnectionStringHasNoTimeout()
    {
        var connectionString = Fresh("Host=clickhouse-knob.local;Port=8123;Username=knob;Password=knob-secret");

        var result = ClickHouseTestTimeout.Resolve(connectionString, "180");

        result.Source.Should().Be(ClickHouseTestTimeoutSource.Knob);
        result.EffectiveSeconds.Should().Be(180);
        result.IsInfinite.Should().BeFalse();
        result.OverriddenExistingTimeout.Should().BeFalse();
        result.DiagnosticText.Should().BeNull();
        result.DiagnosticEmitCount.Should().Be(0);
        result.ConnectionString.Should().ContainEquivalentOf("Timeout=180");
        result.ConnectionString.Should().ContainEquivalentOf("Host=clickhouse-knob.local");
        result.ConnectionString.Should().ContainEquivalentOf("Username=knob");
    }

    [Fact]
    public void Resolve_ShouldEmitOneCredentialFreeOverrideDiagnostic_WhenTheKnobReplacesAnExistingTimeout()
    {
        var connectionString = Fresh("Host=clickhouse-override.local;Port=8123;Username=svc;Password=sup3r-secret;Timeout=30");

        var result = ClickHouseTestTimeout.Resolve(connectionString, "180");

        result.Source.Should().Be(ClickHouseTestTimeoutSource.Knob);
        result.EffectiveSeconds.Should().Be(180);
        result.OverriddenExistingTimeout.Should().BeTrue();
        result.ConnectionString.Should().ContainEquivalentOf("Timeout=180");
        result.ConnectionString.Should().NotContainEquivalentOf("Timeout=30");

        result.DiagnosticText.Should().NotBeNull();
        var diagnostic = result.DiagnosticText!;
        diagnostic.Should().Be(
            "NEXTORM_CLICKHOUSE_HTTP_TIMEOUT_SECONDS override: timeout_source=knob "
            + "effective_seconds=180 connection_string_timeout_overridden=true");
        diagnostic.Should().Contain("timeout_source=knob");
        diagnostic.Should().Contain("effective_seconds=180");
        diagnostic.Should().Contain("connection_string_timeout_overridden=true");
        diagnostic.Should().NotContain("Password=");
        diagnostic.Should().NotContain("Username=");
        diagnostic.Should().NotContain("sup3r-secret");
        diagnostic.Should().NotContain("clickhouse-override.local");
        diagnostic.Should().NotContain(connectionString);

        // Exactly one diagnostic: the resolver writes it at most once per resolved configuration.
        result.DiagnosticEmitCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_ShouldNotReportAnOverride_WhenTheKnobMatchesTheExistingTimeout()
    {
        var connectionString = Fresh("Host=clickhouse-equal.local;Port=8123;Timeout=180");

        var result = ClickHouseTestTimeout.Resolve(connectionString, "180");

        result.Source.Should().Be(ClickHouseTestTimeoutSource.Knob);
        result.EffectiveSeconds.Should().Be(180);
        result.OverriddenExistingTimeout.Should().BeFalse();
        result.DiagnosticText.Should().BeNull();
        result.DiagnosticEmitCount.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldLetTheKnobWin_WhenTheExistingTimeoutIsNotNumeric()
    {
        var connectionString = Fresh("Host=clickhouse-unparsable.local;Port=8123;Timeout=abc");

        var result = ClickHouseTestTimeout.Resolve(connectionString, "180");

        result.Source.Should().Be(ClickHouseTestTimeoutSource.Knob);
        result.EffectiveSeconds.Should().Be(180);
        result.OverriddenExistingTimeout.Should().BeTrue();
        result.ConnectionString.Should().ContainEquivalentOf("Timeout=180");
        result.ConnectionString.Should().NotContainEquivalentOf("Timeout=abc");
        result.DiagnosticText.Should().Contain("timeout_source=knob");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("1.5")]
    [InlineData("1e3")]
    [InlineData("+5")]
    [InlineData(" 5")]
    [InlineData("99999999999")]
    public void Resolve_ShouldRejectAnInvalidKnobWithoutFallback(string knobValue)
    {
        var connectionString = Fresh("Host=clickhouse-invalid-knob.local;Port=8123;Password=invalid-secret");

        var exception = FluentActions
            .Invoking(() => ClickHouseTestTimeout.Resolve(connectionString, knobValue))
            .Should()
            .Throw<ClickHouseTestTimeoutConfigurationException>()
            .Which;

        exception.Message.Should().Contain(Knob);
        exception.Message.Should().Contain("Accepted: a positive 32-bit integer number of seconds");
        exception.Message.Should().Contain("no fallback is applied");
        exception.Message.Should().NotContain("invalid-secret");

        // No effective string was produced: the failure neither memoised a usable result nor let the
        // driver default stand in for the rejected value.
        var withoutKnob = ClickHouseTestTimeout.Resolve(connectionString, knobValue: null);
        withoutKnob.Source.Should().Be(ClickHouseTestTimeoutSource.DriverDefault);
        withoutKnob.EffectiveSeconds.Should().Be(120);
    }

    [Fact]
    public void Resolve_ShouldEchoANonCredentialLikeRejectedValue()
    {
        var connectionString = Fresh("Host=clickhouse-echo.local;Port=8123");

        var exception = FluentActions
            .Invoking(() => ClickHouseTestTimeout.Resolve(connectionString, "abc"))
            .Should()
            .Throw<ClickHouseTestTimeoutConfigurationException>()
            .Which;

        exception.Message.Should().Contain("'abc'");
    }

    [Fact]
    public void Resolve_ShouldMemoisePerInputAndEmitTheOverrideDiagnosticOnlyOnce()
    {
        var connectionString = Fresh("Host=clickhouse-memo.local;Port=8123;Username=memo;Password=memo-secret;Timeout=30");

        var first = ClickHouseTestTimeout.Resolve(connectionString, "240");
        var second = ClickHouseTestTimeout.Resolve(connectionString, "240");

        second.Should().BeSameAs(first);
        second.ConnectionString.Should().Be(first.ConnectionString);
        second.Source.Should().Be(first.Source);
        second.EffectiveSeconds.Should().Be(first.EffectiveSeconds);
        second.DiagnosticText.Should().Be(first.DiagnosticText);
        first.DiagnosticEmitCount.Should().Be(1);
        second.DiagnosticEmitCount.Should().Be(1);
    }

    [Fact]
    public void Resolve_ShouldReadTheKnobFromTheEnvironment()
    {
        var connectionString = Fresh("Host=clickhouse-env.local;Port=8123;Timeout=45");
        var original = Environment.GetEnvironmentVariable(Knob);

        try
        {
            Environment.SetEnvironmentVariable(Knob, "300");

            var result = ClickHouseTestTimeout.Resolve(connectionString);

            result.Source.Should().Be(ClickHouseTestTimeoutSource.Knob);
            result.EffectiveSeconds.Should().Be(300);
            result.OverriddenExistingTimeout.Should().BeTrue();
            result.ConnectionString.Should().ContainEquivalentOf("Timeout=300");
        }
        finally
        {
            Environment.SetEnvironmentVariable(Knob, original);
        }
    }

    /// <summary>A non-interned copy, so identity assertions prove the resolver returns the caller's string.</summary>
    private static string Fresh(string value) => new(value.ToCharArray());
}
