namespace NextORM.Core;

/// <summary>
/// Whether a routed command reads or writes. A topology whose picker balances load may use it to
/// prefer a read replica for reads.
/// </summary>
public enum CommandKind
{
    /// <summary>A read-only command (<c>SELECT</c> and the terminals that consume one).</summary>
    Read = 0,

    /// <summary>A command that mutates data (<c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c>/<c>MERGE</c>/DDL).</summary>
    Write = 1,
}

/// <summary>
/// Everything a topology may use to choose a host. Deliberately small: load, health and weight are the
/// topology's own business and are not modelled by the engine.
/// </summary>
public readonly struct TopologyContext
{
    /// <summary>Creates a context for one command.</summary>
    /// <param name="scope">The mapping scope the command was planned for.</param>
    /// <param name="kind">Whether the command reads or writes.</param>
    public TopologyContext(MappingScope scope, CommandKind kind)
    {
        Scope = scope;
        Kind = kind;
    }

    /// <summary>The mapping scope of the command.</summary>
    public MappingScope Scope { get; }

    /// <summary>Whether the command reads or writes.</summary>
    public CommandKind Kind { get; }
}

/// <summary>
/// One connection of a topology: a stable identifier (the key the context caches the connection
/// manager by) and the connection string that addresses it.
/// </summary>
/// <param name="Id">The identifier of the host inside its topology; must be stable across calls.</param>
/// <param name="ConnectionString">The connection string of the host.</param>
public readonly record struct HostTarget(string Id, string ConnectionString);

/// <summary>
/// A source of hosts for routed commands: several connections of a shard ring, several plain
/// connections picked by round-robin or by a custom criterion, or anything else the application needs.
/// </summary>
/// <remarks>
/// The engine asks a topology for a host before every routed command and never interprets what is
/// behind it; weight, health and load are implemented inside the topology.
/// </remarks>
public interface IHostTopology
{
    /// <summary>The name the routing rules refer to.</summary>
    string Name { get; }

    /// <summary>Chooses the host that executes the command described by <paramref name="context"/>.</summary>
    /// <param name="context">The scope and kind of the command.</param>
    /// <param name="host">Receives the chosen host when the topology claims the command.</param>
    /// <returns>
    /// <see langword="true"/> when the topology claims the command; <see langword="false"/> leaves the
    /// command on the context's own connection.
    /// </returns>
    bool TryGetHost(in TopologyContext context, out HostTarget host);
}
