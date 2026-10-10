using System.IO;

namespace NextORM.Benchmark;

/// <summary>
/// Locates the shared artifacts folder, <c>benchmarks/BenchmarkDotNet.Artifacts</c>.
/// </summary>
/// <remarks>
/// BenchmarkDotNet defaults to <c>./BenchmarkDotNet.Artifacts</c> relative to the working
/// directory, so results end up in a different place depending on how the benchmarks are started
/// (repository root, project folder, IDE). Pinning the path keeps every run in one folder.
/// </remarks>
internal static class BenchmarkArtifacts
{
    private const string SolutionFileName = "nextorm.sln";

    public static string Path { get; } = Resolve();

    private static string Resolve()
    {
        // Honor the BenchmarkDotNet `--artifacts <path>` / `-a <path>` CLI override. The explicit
        // ArtifactsPath pinned by NextormConfig would otherwise win over the CLI-provided config, so
        // read the flag here (this helper is the single owner of the benchmark artifact root).
        var cliPath = GetCommandLineArtifactsPath();
        if (cliPath is not null)
            return System.IO.Path.GetFullPath(cliPath);

        // Walk up from the output folder (benchmarks/NextORM.Benchmark/bin/<os>/<config>/<tfm>) to
        // the repository root instead of counting '..', so the layout can change freely.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, SolutionFileName))
                || File.Exists(System.IO.Path.Combine(dir.FullName, "nextorm.slnx")))
                return System.IO.Path.Combine(dir.FullName, "benchmarks", "BenchmarkDotNet.Artifacts");
        }

        // Not running from the repository (for example a copied build): keep the default location.
        return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts");
    }

    private static string? GetCommandLineArtifactsPath()
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "--artifacts" or "-a")
                return args[i + 1];
        }

        return null;
    }
}
