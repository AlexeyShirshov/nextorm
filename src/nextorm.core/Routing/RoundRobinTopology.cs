using System.Globalization;
using System.Threading;

namespace NextORM.Core;

/// <summary>
/// Chooses one connection per command from a set of hosts in round-robin order. This is the reference
/// topology for routing without sharding: connection strings are opaque to the engine, and the topology
/// keeps only a rotation counter.
/// </summary>
/// <remarks>
/// This topology does not model load, health, or read/write preferences. Implement <see cref="IHostTopology"/>
/// when host selection depends on them. <see cref="DataContext.PinHost"/> and routed transactions override
/// rotation and bind commands to one host.
/// </remarks>
public sealed class RoundRobinTopology : IHostTopology
{
    private readonly HostTarget[] _hosts;
    private int _next;

    /// <summary>Creates the topology over connection strings, naming the hosts <c>host-1</c>, <c>host-2</c>, … in order.</summary>
    /// <param name="name">The name the routing rules refer to.</param>
    /// <param name="connectionStrings">The connection strings to rotate over; at least one is required.</param>
    /// <exception cref="ArgumentException">The name or a connection string is empty, or no connection string is given.</exception>
    public RoundRobinTopology(string name, params string[] connectionStrings)
        : this(name, ToHosts(connectionStrings))
    {
    }

    /// <summary>Creates the topology over explicit hosts, so the caller chooses the ids it can pin by.</summary>
    /// <param name="name">The name the routing rules refer to.</param>
    /// <param name="hosts">The hosts to rotate over; at least one is required and the ids must be unique.</param>
    /// <exception cref="ArgumentException">The name is empty, no host is given, or an id or connection string is empty.</exception>
    public RoundRobinTopology(string name, IEnumerable<HostTarget> hosts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(hosts);

        _hosts = [.. hosts];

        if (_hosts.Length == 0)
            throw new ArgumentException("A topology needs at least one host.", nameof(hosts));

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var host in _hosts)
        {
            if (string.IsNullOrWhiteSpace(host.Id))
                throw new ArgumentException("Every host needs a non-empty id.", nameof(hosts));
            if (string.IsNullOrWhiteSpace(host.ConnectionString))
                throw new ArgumentException($"Host '{host.Id}' has an empty connection string.", nameof(hosts));
            if (!ids.Add(host.Id))
                throw new ArgumentException($"Host id '{host.Id}' is declared more than once.", nameof(hosts));
        }

        Name = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>The hosts of the topology, in rotation order.</summary>
    public IReadOnlyList<HostTarget> Hosts => _hosts;

    /// <inheritdoc />
    /// <remarks>
    /// A pinned host is returned as is; otherwise the next host in rotation is chosen. The rotation is
    /// atomic, so a context used from several threads still distributes commands instead of always
    /// picking the same connection.
    /// </remarks>
    public bool TryGetHost(in TopologyContext context, out HostTarget host)
    {
        if (context.HostId is { } pinned)
        {
            foreach (var candidate in _hosts)
            {
                if (string.Equals(candidate.Id, pinned, StringComparison.Ordinal))
                {
                    host = candidate;
                    return true;
                }
            }

            host = default;
            return false;
        }

        var rotation = Interlocked.Increment(ref _next) - 1;
        var index = (int)((uint)rotation % (uint)_hosts.Length);
        host = _hosts[index];
        return true;
    }

    private static HostTarget[] ToHosts(string[] connectionStrings)
    {
        ArgumentNullException.ThrowIfNull(connectionStrings);

        var hosts = new HostTarget[connectionStrings.Length];
        for (var i = 0; i < hosts.Length; i++)
            hosts[i] = new HostTarget(string.Concat("host-", (i + 1).ToString(CultureInfo.InvariantCulture)), connectionStrings[i]);

        return hosts;
    }
}
