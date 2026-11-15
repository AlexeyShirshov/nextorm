namespace NextORM.Core;
/// <summary>
/// Base class for prepared (compiled) query commands.
/// </summary>
public class PreparedQueryCommand<TResult, TRecord> : IPreparedQueryCommand<TResult>
{
    /// <summary>
    /// Maps a single record to the query result, or <see langword="null"/> when no projection is needed.
    /// </summary>
    public readonly Func<TRecord, TResult>? MapDelegate;

    /// <summary>
    /// The mapping scope the command was planned for. A bucket scope selects the shard (server) whose
    /// connection executes the command; the default scope keeps the context connection.
    /// </summary>
    public MappingScope MappingScope { get; internal set; } = MappingScope.Default;

    /// <summary>
    /// The entity type the command is routed by. Plans are shared across contexts with different routing
    /// rules, so the topologies are resolved per execution from this type and <see cref="MappingScope"/>.
    /// </summary>
    internal Type? RouteEntityType { get; init; }

    /// <summary>
    /// Creates a prepared command with a fixed record-to-result mapper.
    /// </summary>
    /// <param name="mapDelegate">Maps a record to a result, or <see langword="null"/> when no projection is needed.</param>
    public PreparedQueryCommand(Func<TRecord, TResult>? mapDelegate)
    {
        MapDelegate = mapDelegate;
    }
    /// <summary>
    /// Creates a prepared command that resolves its record-to-result mapper lazily.
    /// </summary>
    /// <param name="getMap">Produces the mapper, or <see langword="null"/> when no projection is needed.</param>
    public PreparedQueryCommand(Func<Func<TRecord, TResult>?> getMap)
    {
        MapDelegate = getMap();
    }
}
