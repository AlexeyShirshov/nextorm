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
    /// <summary>
    /// Whether physical identifiers (table and column names) are quoted with the provider's delimiter
    /// in generated SQL. Defaults to <c>false</c> (names are emitted verbatim); can be overridden per
    /// command with <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public bool QuoteIdentifiers { get; private set; }

    /// <summary>
    /// Convention applied by default to table and column names that were not declared explicitly
    /// (no attribute/fluent mapping). <see langword="null"/> (the default) emits the CLR names
    /// verbatim; can be overridden per command with <c>WithNamingConvention</c>.
    /// </summary>
    public INamingConvention? NamingConvention { get; private set; }
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

    /// <summary>
    /// Enables quoting of physical identifiers (table and column names) in generated SQL, using the
    /// provider's delimiter (<c>"id"</c> on PostgreSQL/SQLite, <c>[id]</c> on SQL Server, `` `id` `` on
    /// MySQL/MariaDB/ClickHouse). A single command can override the setting with
    /// <c>WithQuotedIdentifiers</c>.
    /// </summary>
    public DataContextBuilder UseQuotedIdentifiers(bool value = true)
    {
        QuoteIdentifiers = value;
        return this;
    }

    /// <summary>
    /// Applies a naming convention to auto-derived table and column names; for example
    /// <see cref="SnakeCaseNamingConvention.Instance"/> maps <c>SimpleEntity</c> to
    /// <c>simple_entity</c> and <c>FirstName</c> to <c>first_name</c>. Names declared with
    /// <c>[SqlTable]</c>/<c>[Column]</c> or a fluent mapping are never translated. Pass
    /// <see langword="null"/> to emit CLR names verbatim (the default). A single command can override
    /// the setting with <c>WithNamingConvention</c>.
    /// </summary>
    public DataContextBuilder UseNamingConvention(INamingConvention? convention)
    {
        NamingConvention = convention;
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