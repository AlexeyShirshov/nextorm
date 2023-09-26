using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContext
{
    protected readonly DataProvider _sqlClient;
    protected readonly ILogger? _cmdLogger;
    public DataContext(DataContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(optionsBuilder.SqlClient);

        _sqlClient = optionsBuilder.SqlClient;

        if (optionsBuilder.LoggerFactory is not null)
        {
            _sqlClient.Logger = optionsBuilder.LoggerFactory.CreateLogger(typeof(SqlClient));
            _cmdLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(SqlCommand));
        }
    }
    public CommandBuilder<T> From<T>(IQueryCommand<T> query) => new(_sqlClient, query) { Logger = _cmdLogger };
}