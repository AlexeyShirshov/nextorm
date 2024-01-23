using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Environments;

namespace nextorm.benchmark;

internal class NextormConfig : ManualConfig
{
    public NextormConfig()
    {
        AddColumn(CategoriesColumn.Default);
        Orderer = new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest);
    }
}