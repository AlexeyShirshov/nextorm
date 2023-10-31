using System.Data.Common;

namespace nextorm.core;
public class CompiledQuery<TResult>
{
    private Func<object, TResult>? _mapCache;
    private readonly Func<Func<object, TResult>> _getMap;
    public CompiledQuery(Func<Func<object, TResult>> getMap)
    {
        _getMap=getMap;
    }
    public TResult Map(object dataRecord)
    {
        var resultType = typeof(TResult);

        var recordType = dataRecord.GetType();

        if (resultType == recordType)
            return (TResult)dataRecord;

        if (_mapCache is null)
        {
            //_mapCache = (object _) => Activator.CreateInstance<TResult>();
            _mapCache=_getMap();
        }

        return _mapCache(dataRecord);

    }
}
public class DatabaseCompiledQuery<TResult> : CompiledQuery<TResult>
{
    public DbCommand dbCommand;

    public DatabaseCompiledQuery(DbCommand dbCommand, Func<Func<object, TResult>> getMap) 
        : base(getMap)
    {
        this.dbCommand = dbCommand;
    }   
}