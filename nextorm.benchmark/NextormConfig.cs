using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

namespace nextorm.benchmark;

internal class NextormConfig : ManualConfig
{
    public NextormConfig()
    {
        AddColumn(CategoriesColumn.Default);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
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