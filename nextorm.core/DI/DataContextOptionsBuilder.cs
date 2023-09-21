using Microsoft.Extensions.Logging;

namespace nextorm.core;

public class DataContextOptionsBuilder
{
    private ILoggerFactory? _loggerFactory;
    private SqlClient? _sqlClient;
    internal SqlClient? SqlClient => _sqlClient;
    internal ILoggerFactory? LoggerFactory => _loggerFactory; 

    public virtual DataContextOptionsBuilder UseLoggerFactory(ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _loggerFactory = loggerFactory;
        return this;
    }
    public virtual DataContextOptionsBuilder UseSqlClient(SqlClient sqlClient)
    {
        ArgumentNullException.ThrowIfNull(sqlClient);

        _sqlClient = sqlClient;        
        return this;
    }
}