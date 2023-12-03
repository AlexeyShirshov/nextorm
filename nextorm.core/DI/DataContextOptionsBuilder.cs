using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DbContextBuilder
{
    //private readonly Dictionary<string, object> _props = new();
    private ILoggerFactory? _loggerFactory;
    //private IDataProvider? _dataProvider;
    private bool _logSensetiveData;
    private Func<DbContextBuilder, IDataContext>? _factory;
    public bool ShouldLogSensetiveData => _logSensetiveData;
    //internal IDataProvider? DataProvider => _dataProvider;
    internal ILoggerFactory? LoggerFactory => _loggerFactory;
    public bool CacheQueryCommand { get; set; } = true;
    public Func<DbContextBuilder, IDataContext>? Factory { get => _factory; set => _factory = value; }

    //public Dictionary<string, object> Property => _props;
    public DbContextBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
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
    public DbContextBuilder LogSensetiveData(bool logSensetiveData)
    {
        _logSensetiveData = logSensetiveData;
        return this;
    }

    public IDataContext CreateDbContext()
    {
        if (_factory is null)
            throw new InvalidOperationException("Context is not set");

        return _factory(this);
    }

    // public DataContextOptionsBuilder UseInMemoryClient()
    // {
    //     _dataProvider = new InMemoryDataProvider();
    //     return this;
    // }
}