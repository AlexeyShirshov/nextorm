using System.Diagnostics;
using System.Text;
using FluentAssertions;

namespace NextORM.Integration.Tests;

/// <summary>Runs the provider SQL probe from JIT and NativeAOT against live database engines.</summary>
public sealed class AotSqlProbeNativeServerTests
{
    private static readonly string[] GraphOracles =
    [
        "[GRAPH] relationship-load-with: 1[10,11];2[];commands=2",
        "[GRAPH] eager-load-single-query: 1[10,11];2[];commands=1",
        "[GRAPH] join-into-collection: 1[10,11];2[];commands=1",
        "[GRAPH] eager-load-many-to-many: 1[10,10,11];2[12];3[];commands=1",
        "[GRAPH] join-into-many-to-many: 1[10,10,11];2[12];3[];commands=1",
        "[GRAPH] eager-load-composite-many-to-many: 1[10,10,11];2[11];3[];commands=1",
        "[GRAPH] join-into-composite-many-to-many: 1[10,10,11];2[11];3[];commands=1",
    ];

    [Fact]
    [Trait("Category", "Diagnostic")]
    public async Task NativeAndJitSqlProbe_ShouldMatchAllLiveProviderShapes()
    {
        var nativePath = Environment.GetEnvironmentVariable("NEXTORM_AOT_SQLPROBE_NATIVE");
        var jitAssembly = Environment.GetEnvironmentVariable("NEXTORM_AOT_SQLPROBE_JIT");
        if (string.IsNullOrWhiteSpace(nativePath) || string.IsNullOrWhiteSpace(jitAssembly))
            Assert.Skip("Set NEXTORM_AOT_SQLPROBE_NATIVE and NEXTORM_AOT_SQLPROBE_JIT to run the live AOT probe.");

        if (!File.Exists(nativePath))
            throw new FileNotFoundException("The NativeAOT SQL probe executable is missing.", nativePath);
        if (!File.Exists(jitAssembly))
            throw new FileNotFoundException("The JIT SQL probe assembly is missing.", jitAssembly);

        var evidenceDirectory = Environment.GetEnvironmentVariable("NEXTORM_AOT_SQLPROBE_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(evidenceDirectory))
            evidenceDirectory = Path.Combine(Path.GetTempPath(), "nextorm-aot-sqlprobe", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidenceDirectory);

        var repositoryRoot = FindRepositoryRoot();
        var errors = new List<string>();
        var providers = new (string Name, Func<string> ConnectionString, Func<ValueTask> Dispose)[]
        {
            ("postgres", () => PostgresContainer.ConnectionString, PostgresContainer.DisposeContainerAsync),
            ("sqlserver", () => SqlServerContainer.ConnectionString, SqlServerContainer.DisposeContainerAsync),
            ("mysql", () => MySqlContainer.ConnectionString, MySqlContainer.DisposeContainerAsync),
            ("mariadb", () => MariaDbContainer.ConnectionString, MariaDbContainer.DisposeContainerAsync),
            ("clickhouse", () => ClickHouseContainer.ConnectionString, ClickHouseContainer.DisposeContainerAsync),
        };

        foreach (var provider in providers)
        {
            try
            {
                var connectionString = provider.ConnectionString();
                var jit = await RunProbeAsync(
                    "jit", provider.Name, connectionString, native: false, nativePath, jitAssembly,
                    repositoryRoot, evidenceDirectory, TestContext.Current.CancellationToken);
                var native = await RunProbeAsync(
                    "native", provider.Name, connectionString, native: true, nativePath, jitAssembly,
                    repositoryRoot, evidenceDirectory, TestContext.Current.CancellationToken);

                if (jit.ExitCode != 0 || native.ExitCode != 0)
                {
                    errors.Add(
                        $"{provider.Name}: JIT exit={jit.ExitCode} log={jit.LogPath}; native exit={native.ExitCode} log={native.LogPath}");
                    continue;
                }

                if (!jit.Stdout.Contains("dynamic-code-supported=True", StringComparison.Ordinal))
                    errors.Add($"{provider.Name}: JIT control did not report dynamic-code-supported=True ({jit.LogPath}).");
                if (!native.Stdout.Contains("dynamic-code-supported=False", StringComparison.Ordinal))
                    errors.Add($"{provider.Name}: native run did not report dynamic-code-supported=False ({native.LogPath}).");

                foreach (var oracle in GraphOracles)
                {
                    if (!jit.Stdout.Contains(oracle, StringComparison.Ordinal))
                        errors.Add($"{provider.Name}: JIT did not report oracle '{oracle}' ({jit.LogPath}).");
                    if (!native.Stdout.Contains(oracle, StringComparison.Ordinal))
                        errors.Add($"{provider.Name}: native did not report oracle '{oracle}' ({native.LogPath}).");
                }

                if (!jit.OutcomeLines.SequenceEqual(native.OutcomeLines, StringComparer.Ordinal))
                    errors.Add($"{provider.Name}: JIT/native result lines differ; compare {jit.LogPath} and {native.LogPath}.");
            }
            catch (Exception exception)
            {
                errors.Add($"{provider.Name}: {exception.GetType().Name}: {exception.Message}");
            }
            finally
            {
                try
                {
                    await provider.Dispose();
                }
                catch (Exception exception)
                {
                    errors.Add($"{provider.Name}: container cleanup failed: {exception.GetType().Name}: {exception.Message}");
                }
            }
        }

        errors.Should().BeEmpty(string.Join(Environment.NewLine, errors));
    }

    private static async Task<ProbeRun> RunProbeAsync(
        string mode,
        string provider,
        string connectionString,
        bool native,
        string nativePath,
        string jitAssembly,
        string repositoryRoot,
        string evidenceDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = native ? nativePath : "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot,
        };
        if (!native)
            startInfo.ArgumentList.Add(jitAssembly);
        foreach (var argument in new[] { "--provider", provider, "--connection", connectionString })
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.Start().Should().BeTrue($"the {mode} probe for {provider} should start");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        var logPath = Path.Combine(evidenceDirectory, $"server-probe-{provider}-{mode}.log");
        var redactedArguments = native
            ? $"[\"{nativePath}\",\"--provider\",\"{provider}\",\"--connection\",\"<redacted>\"]"
            : $"[\"dotnet\",\"{jitAssembly}\",\"--provider\",\"{provider}\",\"--connection\",\"<redacted>\"]";
        var log = new StringBuilder()
            .AppendLine($"provider={provider}")
            .AppendLine($"mode={mode}")
            .AppendLine($"command={redactedArguments}")
            .AppendLine($"exit={process.ExitCode}")
            .AppendLine("--- stdout ---")
            .Append(stdout)
            .AppendLine("--- stderr ---")
            .Append(stderr)
            .ToString();
        await File.WriteAllTextAsync(logPath, log, Encoding.UTF8, cancellationToken).ConfigureAwait(false);

        var outcomeLines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line =>
                line.StartsWith("[ OK ] ", StringComparison.Ordinal) ||
                line.StartsWith("[SKIP] ", StringComparison.Ordinal) ||
                line.StartsWith("[FAIL] ", StringComparison.Ordinal) ||
                line.StartsWith("[GRAPH] ", StringComparison.Ordinal) ||
                line.Contains(" shapes completed;", StringComparison.Ordinal))
            .ToArray();

        return new ProbeRun(process.ExitCode, stdout, outcomeLines, logPath);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nextorm.slnx")))
            directory = directory.Parent;

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }

    private sealed record ProbeRun(int ExitCode, string Stdout, string[] OutcomeLines, string LogPath);
}
