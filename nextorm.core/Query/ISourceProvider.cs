
namespace nextorm.core;

public interface IColumnsProvider
{
    void Add(Type entityType, bool fromProjection);
    void Add(QueryCommand queryCommand, bool fromProjection);
    int? FindAlias(Type entityType, bool fromProjection);
    QueryCommand? FindQueryCommand(Type entityType);
}

public class DefaultColumnsProvider : IColumnsProvider
{
#if NET8_0_OR_GREATER
    private ValueList<(Type, QueryCommand?, bool)> _list;
#else
    private readonly List<(Type, QueryCommand?,bool)> _list = [];
#endif
    public void Add(Type entityType, bool fromProjection)
    {
        _list.Add((entityType, null, fromProjection));
    }

    public void Add(QueryCommand queryCommand, bool fromProjection)
    {
        _list.Add((queryCommand.ResultType!, queryCommand, fromProjection));
    }

    public int? FindAlias(Type entityType, bool fromProjection)
    {
        for (var (i, cnt) = (0, _list.Count); i < cnt; i++)
        {
            var item = _list[i];
            if (item.Item1 == entityType && item.Item3 == fromProjection)
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