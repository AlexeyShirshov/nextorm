using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Configures and creates an <see cref="IDataContext"/>.
/// </summary>
/// <remarks>
/// The <c>DataContext</c> prefix is inconsistent with the rest of the library, which otherwise uses
/// <c>DataContext</c> (for example <c>DataContextOptionsBuilderExtensions</c>). Recommended name:
/// <c>DataContextBuilder</c>. See <c>docs/specs/design/API-NAMING-REVIEW.md</c> finding P2-22.
/// </remarks>
public class DataContextBuilder
{
    //private readonly Dictionary<string, object> _props = new();
    private ILoggerFactory? _loggerFactory;
    //private IDataProvider? _dataProvider;
    private bool _logSensitiveData;
    private Func<DataContextBuilder, IDataContext>? _factory;
    public bool ShouldLogSensitiveData => _logSensitiveData;
    //internal IDataProvider? DataProvider => _dataProvider;
    internal ILoggerFactory? LoggerFactory => _loggerFactory;
    // public bool CacheQueryCommand { get; set; } = true;
    // public bool CacheExpressions { get; set; } = true;
    public Func<DataContextBuilder, IDataContext>? Factory { get => _factory; set => _factory = value; }

    //public Dictionary<string, object> Property => _props;
    public DataContextBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
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
    public DataContextBuilder LogSensitiveData(bool logSensitiveData)
    {
        _logSensitiveData = logSensitiveData;
        return this;
    }

    public IDataContext CreateDataContext()
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