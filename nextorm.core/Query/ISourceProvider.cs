namespace nextorm.core;

public interface ISourceProvider
{
    QueryCommand FindSourceFromAlias(string? alias);
}