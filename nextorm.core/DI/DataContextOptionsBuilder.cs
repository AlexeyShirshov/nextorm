using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContextOptionsBuilder
{
    private ILoggerFactory? _loggerFactory;
    private SqlClient? _sqlClient;
    private bool _logSensetiveData;

    public bool ShouldLogSensetiveData => _logSensetiveData;

    internal SqlClient? SqlClient => _sqlClient;
    internal ILoggerFactory? LoggerFactory => _loggerFactory; 

    public DataContextOptionsBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        return this;
    }
    public DataContextOptionsBuilder UseSqlClient(SqlClient sqlClient)
    {
        ArgumentNullException.ThrowIfNull(sqlClient);

        _sqlClient = sqlClient;        
        return this;
    }
    public DataContextOptionsBuilder LogSensetiveData(bool logSensetiveData)
    {
        _logSensetiveData = logSensetiveData;
        return this;
    }
}