
namespace nextorm.core;

public interface IColumnsProvider
{
    void Add(Type entityType);
    void Add(QueryCommand queryCommand);
    int? FindAlias(Type entityType);
    QueryCommand? FindQueryCommand(Type entityType);
}

public class DefaultColumnsProvider : IColumnsProvider
{
#if NET8_0_OR_GREATER
    private ValueList<(Type,QueryCommand?)> _list;
#else
    private readonly List<(Type, QueryCommand?)> _list = [];
#endif
    public void Add(Type entityType)
    {
        _list.Add((entityType, null));
    }

    public void Add(QueryCommand queryCommand)
    {
        _list.Add((queryCommand.ResultType!, queryCommand));
    }

    public int? FindAlias(Type entityType)
    {
        for (var (i, cnt) = (0, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 == entityType)
                return i;
        }

        return null;
    }

    public QueryCommand? FindQueryCommand(Type entityType)
    {
        for (var (i, cnt) = (0, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 == entityType)
                return item.Item2;
        }

        return null;
    }
}