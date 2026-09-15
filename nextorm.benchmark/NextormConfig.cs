using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace nextorm.benchmark;

internal class NextormConfig : ManualConfig
{
    public NextormConfig()
    {
        AddColumn(CategoriesColumn.Default);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);

        // Quick by default: short run (warmup 3 / iterations 3) and in-process toolchain,
        // which avoids building a generated project and launching a process per benchmark case.
        // Set NEXTORM_BENCH_FULL=1 to get the full out-of-process run.
        AddJob(Environment.GetEnvironmentVariable("NEXTORM_BENCH_FULL") == "1"
            ? Job.Default
            : Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));
    }
}
internal class AntiVirusFriendlyConfig : ManualConfig
{
    public AntiVirusFriendlyConfig()
    {
        AddJob(Job.Default
            .WithToolchain(InProcessNoEmitToolchain.Instance));

        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
    }
}