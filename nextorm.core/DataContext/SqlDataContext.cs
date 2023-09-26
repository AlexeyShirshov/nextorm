namespace nextorm.core;

public class SqlDataContext : DataContext
{
    public SqlDataContext(DataContextOptionsBuilder optionsBuilder) : base(optionsBuilder)
    {
        if (_sqlClient is SqlClient sql)
            sql.LogSensetiveData = optionsBuilder.ShouldLogSensetiveData;
    }
    public CommandBuilder From(string table) => new(_sqlClient as SqlClient, table) { Logger = _cmdLogger };
    public CommandBuilder<T> From<T>(SqlCommand<T> query) => new(_sqlClient, query) { Logger = _cmdLogger };
    public CommandBuilder<T> Create<T>() => new(_sqlClient) { Logger = _cmdLogger };
}