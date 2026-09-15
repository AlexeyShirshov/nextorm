namespace nextorm.benchmark;

internal static class BenchDb
{
    private static readonly string _filePath = Resolve();

    public static string FilePath => _filePath;

    private static string Resolve()
    {
        var custom = Environment.GetEnvironmentVariable("NEXTORM_BENCH_DB");
        if (!string.IsNullOrEmpty(custom))
            return custom;

        const string wslPath = "/tmp/nextorm-bench/test.db";
        if (File.Exists(wslPath))
            return wslPath;

        return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "data", "test.db");
    }
}
