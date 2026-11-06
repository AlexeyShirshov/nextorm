using System.Diagnostics;
using System.Text;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

[Collection("Sqlite")]
public sealed class AotNativeRunTests
{
    private static readonly string[] ScenarioIds =
    [
        "interface-entity",
        "captured-predicate",
        "anonymous-projection",
        "nullable-scalar",
        "sqlite-json",
        "groupby-aggregates"
    ];

    [Fact]
    public async Task PublishedNativeRunner_UsesParentSqliteDatabase_AndReportsEveryScenario()
    {
        var repositoryRoot = FindRepositoryRoot();
        var binaryPath = Path.Combine(repositoryRoot, "tests", "nextorm.aot.runner", "bin", "linux", "Release", "net10.0", "linux-x64", "publish", "nextorm.aot.runner");
        // The NativeAOT runner is a published evidence artifact, not part of the ordinary build:
        // `dotnet build` never produces it and CI does not publish it. Skip unless the binary is
        // there, and fail loudly when a run explicitly asks for this harness
        // (NEXTORM_AOT_EVIDENCE=1), so the opt-in path can never degrade into a silent skip.
        const string publishCommand = "dotnet publish tests/nextorm.aot.runner -c Release -r linux-x64";
        if (!File.Exists(binaryPath))
        {
            if (Environment.GetEnvironmentVariable("NEXTORM_AOT_EVIDENCE") == "1")
                throw new InvalidOperationException(
                    $"NEXTORM_AOT_EVIDENCE=1 requires the published native binary at {binaryPath}; publish it with `{publishCommand}`.");

            Assert.Skip($"the published NativeAOT runner is missing at {binaryPath}; publish it with `{publishCommand}` (or set NEXTORM_AOT_EVIDENCE=1 to require it).");
        }

        SqliteTestProvider.Instance.EnsureSeeded();
        var databasePath = SqliteTestProvider.DatabasePath;
        if (!OperatingSystem.IsWindows())
        {
            (File.GetUnixFileMode(binaryPath) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute))
                .Should().NotBe(0, "the published native binary must be executable");
        }

        var startInfo = new ProcessStartInfo(binaryPath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(databasePath);
        using var process = new Process { StartInfo = startInfo };
        var stopwatch = Stopwatch.StartNew();
        process.Start().Should().BeTrue("the NativeAOT child process should start");
        var childPid = process.Id;
        var cancellationToken = TestContext.Current.CancellationToken;
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        stopwatch.Stop();

        var scenarioLines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("scenario=", StringComparison.Ordinal))
            .ToArray();
        var reports = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
        var reportedConnection = reports.SingleOrDefault(line => line.StartsWith("connection=", StringComparison.Ordinal));
        var childDatabasePath = Path.Combine(Path.GetDirectoryName(databasePath)!, $"nextorm.integration.{childPid}.db");
        var nativeReports = scenarioLines.ToDictionary(ParseScenarioId, ParseReport, StringComparer.Ordinal);
        var jitResults = RunJitScenarios(databasePath);
        var oracle = BuildOracle(repositoryRoot, databasePath, childPid, jitResults, nativeReports);

        var evidenceDirectory = Environment.GetEnvironmentVariable("NEXTORM_AOT_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(evidenceDirectory))
            evidenceDirectory = Path.Combine("/tmp/opencode", "nextorm-aot", Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(evidenceDirectory);
        var evidencePath = Path.Combine(evidenceDirectory, "native-run.log");
        var oraclePath = Path.Combine(evidenceDirectory, "oracle.md");
        var log = new StringBuilder()
            .AppendLine($"command: {binaryPath}")
            .AppendLine($"args[0]: {databasePath}")
            .AppendLine($"parent db path: {databasePath}")
            .AppendLine($"child process id: {childPid}")
            .AppendLine($"duration: {stopwatch.Elapsed}")
            .AppendLine($"exit code: {process.ExitCode}")
            .AppendLine("--- stdout ---")
            .Append(stdout)
            .AppendLine("--- stderr ---")
            .Append(stderr)
            .AppendLine("--- JIT control ---")
            .AppendLine(string.Join(Environment.NewLine, jitResults.Select(FormatJitReport)))
            .AppendLine($"--- child-specific db check: {childDatabasePath}; exists={File.Exists(childDatabasePath)} ---")
            .ToString();
        await WriteNewEvidenceAsync(evidencePath, log, cancellationToken);
        await WriteNewEvidenceAsync(oraclePath, oracle, cancellationToken);

        process.ExitCode.Should().Be(0, $"the harness must complete; raw child output is in {evidencePath}");
        scenarioLines.Should().HaveCount(ScenarioIds.Length, "the runner must report exactly once per declared scenario");
        scenarioLines.Select(line => line.Split('\t')[0].Replace("scenario=", "", StringComparison.Ordinal))
            .Should().BeEquivalentTo(ScenarioIds, options => options.WithStrictOrdering());
        var componentReports = reports
            .Where(line => line.StartsWith("component-slice=", StringComparison.Ordinal))
            .Select(ParseReport)
            .ToDictionary(report => report.GetValueOrDefault("component-slice", ""), StringComparer.Ordinal);
        componentReports.Should().ContainKey("nextorm.extensions.SelectWhereMin");
        componentReports["nextorm.extensions.SelectWhereMin"].GetValueOrDefault("status").Should().Be("pass");
        componentReports.Should().ContainKey("nextorm.extensions.SelectWhereMax");
        componentReports["nextorm.extensions.SelectWhereMax"].GetValueOrDefault("status").Should().Be("pass");
        reportedConnection.Should().Be($"connection={databasePath}", "the child must report the parent's database path");
        File.Exists(childDatabasePath).Should().BeFalse("the child must not derive its own process-specific database");
    }

    private static async Task WriteNewEvidenceAsync(string path, string contents, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var bytes = Encoding.UTF8.GetBytes(contents);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "nextorm.slnx")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }

    private static Dictionary<string, JitScenarioResult> RunJitScenarios(string databasePath)
    {
        using var context = new SqliteDataContext(
            string.Format(SqliteTestProvider.ConnectionString, databasePath), new DataContextBuilder());
        var results = new Dictionary<string, JitScenarioResult>(StringComparer.Ordinal)
        {
            ["interface-entity"] = CaptureJit(() =>
                string.Join(",", context.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id).ToList())),
            ["captured-predicate"] = CaptureJit(() =>
            {
                var minimum = 5;
                var count = context.From<SimpleEntity>().Where(x => x.Id > minimum).Count();
                return $"minimum={minimum},count={count}";
            }),
            ["anonymous-projection"] = CaptureJit(() =>
            {
                var rows = context.From<IComplexEntity>().OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.String }).ToList();
                return string.Join(";", rows.Select(row => $"id={row.Id},string={row.String}"));
            }),
            ["nullable-scalar"] = CaptureJit(() =>
            {
                var values = context.From<IComplexEntity>().OrderBy(x => x.Id).Select(x => x.Int).ToList();
                return string.Join(",", values.Select(value => value?.ToString() ?? "null"));
            }),
            ["sqlite-json"] = CaptureJit(() =>
            {
                var value = context.From<SimpleEntity>().Where(x => x.Id == 1)
                    .Select(x => new
                    {
                        Extract = SqlFunctions.Sqlite.json_extract<string>("{\"a\":\"hello\"}", "$.a"),
                        Text = SqlFunctions.Sqlite.json_get_text("{\"a\":\"hello\"}", "$.a")
                    }).First();
                return $"extract={value.Extract},text={value.Text}";
            }),
            ["groupby-aggregates"] = CaptureJit(() =>
            {
                var groups = context.From<IComplexEntity>().GroupBy(x => x.Int)
                    .Having(g => SqlFunctions.Sql.count() > 1)
                    .Select(g => new { g.Int, Count = SqlFunctions.Sql.count(), Sum = SqlFunctions.Sql.sum(g.Id) })
                    .ToList();
                return string.Join(";", groups.Select(group =>
                    $"int={group.Int?.ToString() ?? "null"},count={group.Count},sum={group.Sum}"));
            })
        };
        return results;
    }

    private static JitScenarioResult CaptureJit(Func<string> action)
    {
        try
        {
            return new JitScenarioResult("pass", action(), "", "");
        }
        catch (Exception ex)
        {
            var detail = ex;
            while (detail.InnerException is not null)
                detail = detail.InnerException;
            var message = ReferenceEquals(detail, ex)
                ? ex.Message
                : $"{ex.Message} InnerException={detail.GetType().FullName}: {detail.Message}";
            return new JitScenarioResult("fail", "", ex.GetType().FullName ?? ex.GetType().Name, message);
        }
    }

    private static string ParseScenarioId(string line) =>
        ParseReport(line).GetValueOrDefault("scenario", "");

    private static Dictionary<string, string> ParseReport(string line) => line.Split('\t')
        .Select(part => part.Split('=', 2))
        .Where(parts => parts.Length == 2)
        .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);

    private static string FormatJitReport(KeyValuePair<string, JitScenarioResult> item) =>
        $"scenario={item.Key}\tstatus={item.Value.Status}\tvalues={Escape(item.Value.Values)}\texception={Escape(item.Value.Exception)}\tmessage={Escape(item.Value.Message)}\tframe=jit";

    private static string BuildOracle(
        string repositoryRoot,
        string databasePath,
        int childPid,
        Dictionary<string, JitScenarioResult> jit,
        Dictionary<string, Dictionary<string, string>> native)
    {
        var childDatabasePath = Path.Combine(Path.GetDirectoryName(databasePath)!, $"nextorm.integration.{childPid}.db");
        var lines = new List<string>
        {
            "# JIT/native semantic oracle",
            "",
            $"Parent SQLite DatabasePath: `{databasePath}`.",
            $"Native child PID: `{childPid}`; child-specific database `{childDatabasePath}` exists: `{File.Exists(childDatabasePath)}`.",
            "The JIT control executes the same six query shapes in the adapter against the same seeded parent SQLite file. Comparison uses scenario status and escaped `values`; native failure is never relabeled as success.",
            "",
            "| scenario | JIT expected status / values | native status / values | equivalence |",
            "|---|---|---|---|"
        };
        foreach (var id in ScenarioIds)
        {
            var j = jit[id];
            var n = native[id];
            var nativeStatus = n.GetValueOrDefault("status", "missing");
            var nativeValues = n.GetValueOrDefault("values", "");
            var jitValues = Escape(j.Values);
            var verdict = nativeStatus != "pass" ? "native-failed"
                : j.Status != "pass" || !string.Equals(nativeValues, jitValues, StringComparison.Ordinal) ? "different"
                : "equal";
            lines.Add($"| {id} | `{j.Status}` / `{jitValues}` | `{nativeStatus}` / `{nativeValues}` | **{verdict}** |");
        }
        lines.Add("");
        lines.Add($"No child reseed: the child's reported connection path is `{databasePath}`; child-PID database exists: `{File.Exists(childDatabasePath)}`.");
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\")
        .Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

    private readonly record struct JitScenarioResult(string Status, string Values, string Exception, string Message);
}
