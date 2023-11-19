using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;

namespace nextorm.core.benchmark;

internal class NextormConfig : ManualConfig
{
    public NextormConfig()
    {
        AddColumn(CategoriesColumn.Default);
    }
}