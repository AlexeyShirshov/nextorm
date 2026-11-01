using NextORM.Examples.Postgres.Aviasales;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Benchmark;

/// <summary>Public-API allocation attribution for the Aviasales airport OTP query.</summary>
internal static class T269AirportOnTimeProbe
{
    public static async Task<int> RunAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);
        await using var database = await DemoDatabase.Start(null, CancellationToken.None);
        var context = new PostgresDataContext(database.ConnectionString, new DataContextBuilder());
        try
        {
            // Warm the public path, then measure repeated full API calls. The example exposes the query
            // only as one async operation: command construction/preparation, execution and typed row
            // materialization cannot be separated without core internals, so report that limitation.
            var first = await AviasalesQueries.AirportOnTimePerformance(context, default);
            var count = ((System.Collections.ICollection)first).Count;
            if (count != 63)
            {
                Console.Error.WriteLine($"Expected 63 rows, observed {count}.");
                return 2;
            }

            const int iterations = 5;
            long bytes = 0;
            for (var i = 0; i < iterations; i++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var rows = await AviasalesQueries.AirportOnTimePerformance(context, default);
                bytes += GC.GetAllocatedBytesForCurrentThread() - before;
                GC.KeepAlive(rows);
            }

            var perCall = bytes / (double)iterations;
            var report = $"rows={count}; iterations={iterations}; observed_api_bytes_per_call={perCall:F0}; " +
                         "build_prepare_execution_materialization=combined (public method returns only completed typed rows); closure_residual=not independently observable\n";
            await File.WriteAllTextAsync(Path.Combine(outDir, "probe.txt"), report);
            Console.Write(report);
            GC.KeepAlive(context);
            return 0;
        }
        finally
        {
            await context.DisposeAsync();
        }
    }
}
