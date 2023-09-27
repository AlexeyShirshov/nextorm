using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContext
{
    protected readonly IDataProvider _dataProvider;
    protected readonly ILogger? _cmdLogger;
    public DataContext(DataContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);
        ArgumentNullException.ThrowIfNull(optionsBuilder.DataProvider);

        _dataProvider = optionsBuilder.DataProvider;

        if (optionsBuilder.LoggerFactory is not null)
        {
            _dataProvider.Logger = optionsBuilder.LoggerFactory.CreateLogger(typeof(IDataProvider));
            _cmdLogger = optionsBuilder.LoggerFactory.CreateLogger(typeof(QueryCommand));
        }
    }
    public CommandBuilder<T> From<T>(QueryCommand<T> query) => new(_dataProvider, query) { Logger = _cmdLogger };
}