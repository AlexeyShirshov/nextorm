using System.IO;

namespace nextorm.benchmark;

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
        // Walk up from the output folder (benchmarks/nextorm.benchmark/bin/<os>/<config>/<tfm>) to
        // the repository root instead of counting '..', so the layout can change freely.
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(dir.FullName, SolutionFileName)))
                return System.IO.Path.Combine(dir.FullName, "benchmarks", "BenchmarkDotNet.Artifacts");
        }

        // Not running from the repository (for example a copied build): keep the default location.
        return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkDotNet.Artifacts");
    }
}
