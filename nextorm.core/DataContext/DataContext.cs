using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContext : IDisposable, IAsyncDisposable
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
    public IDataProvider DataProvider => _dataProvider;
    public CommandBuilder<TResult> From<TResult>(QueryCommand<TResult> query) => new(_dataProvider, query) { Logger = _cmdLogger };
    public CommandBuilder<TResult> From<TResult>(CommandBuilder<TResult> builder) => new(_dataProvider, builder) { Logger = _cmdLogger };
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return _dataProvider.DisposeAsync();
    }
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _dataProvider.Dispose();
    }
}