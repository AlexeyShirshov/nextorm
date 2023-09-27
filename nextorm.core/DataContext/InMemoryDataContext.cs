using System.Linq.Expressions;

namespace nextorm.core;

public class InMemoryDataContext : DataContext
{
    public InMemoryDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
    }
    protected InMemoryCommandBuilder<T> Create<T, InMemoryDataContextT>(Expression<Func<InMemoryDataContextT, InMemoryCommandBuilder<T>>> accessor)
        where InMemoryDataContextT : InMemoryDataContext
    {
        return new (_dataProvider) {Logger = _cmdLogger};
    }
}