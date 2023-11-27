namespace nextorm.core;

public class InMemoryDataContext : DataContext
{
    public InMemoryDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
    }
    protected CommandBuilder<T> Create<T>()
    {
        return new(_dataProvider) { Logger = _dataProvider.CommandLogger };
    }
}