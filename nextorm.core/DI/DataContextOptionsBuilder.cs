using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContextOptionsBuilder
{
    private readonly Dictionary<string, object> _props = new();
    private ILoggerFactory? _loggerFactory;
    //private IDataProvider? _dataProvider;
    private bool _logSensetiveData;

    public bool ShouldLogSensetiveData => _logSensetiveData;
    //internal IDataProvider? DataProvider => _dataProvider;
    internal ILoggerFactory? LoggerFactory => _loggerFactory;
    public bool CacheQueryCommand { get; set; } = true;
    public Dictionary<string, object> Property => _props;
    public DataContextOptionsBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        return this;
    }
    // public DataContextOptionsBuilder UseSqlClient(SqlDataProvider dataProvider)
    // {
    //     ArgumentNullException.ThrowIfNull(dataProvider);

    //     _dataProvider = dataProvider;        
    //     return this;
    // }
    public DataContextOptionsBuilder LogSensetiveData(bool logSensetiveData)
    {
        _logSensetiveData = logSensetiveData;
        return this;
    }

    // public DataContextOptionsBuilder UseInMemoryClient()
    // {
    //     _dataProvider = new InMemoryDataProvider();
    //     return this;
    // }
}