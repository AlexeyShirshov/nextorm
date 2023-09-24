using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContext
{
    private readonly SqlClient _sqlClient;
    private readonly ILogger? _cmdLogger;
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

        _sqlClient.LogSensetiveData = optionsBuilder.ShouldLogSensetiveData;
    }
    public CommandBuilder From(string table) => new(_sqlClient, table) { Logger = _cmdLogger };
    public CommandBuilder<T> From<T>(SqlCommand<T> query) => new(_sqlClient, query) { Logger = _cmdLogger };
    public CommandBuilder<T> Create<T>() => new(_sqlClient) { Logger = _cmdLogger };
}