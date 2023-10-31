namespace nextorm.core;

public class SqlDataContext : DataContext
{
    public SqlDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        if (_dataProvider is SqlDataProvider sql)
        {
            sql.LogSensetiveData = optionsBuilder.ShouldLogSensetiveData;
            //sql.CacheQueryCommand=optionsBuilder.CacheQueryCommand;
        }
    }
    public CommandBuilder From(string table) => new((SqlDataProvider)_dataProvider, table) { Logger = _cmdLogger };
    public CommandBuilder<T> Create<T>() => new(_dataProvider) { Logger = _cmdLogger };
}