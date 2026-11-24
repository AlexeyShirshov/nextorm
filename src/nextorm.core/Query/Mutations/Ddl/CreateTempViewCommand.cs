namespace NextORM.Core;

internal sealed class CreateTempViewCommand : MutationCommand
{
    public CreateTempViewCommand(Type resultType, string name, QueryCommand source)
        : base(SqlStatementType.CreateTempView, resultType)
    {
        Name = name;
        Source = source;
        MappingVariant = source.MappingVariant;
        RouteSources = CommandRouteSources.Collect(source);
    }

    public string Name { get; }
    public QueryCommand Source { get; }
}
