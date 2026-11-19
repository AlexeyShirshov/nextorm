namespace NextORM.Core;

/// <summary>
/// A handle for a host pin created by <see cref="DataContext.PinHost"/>. While the handle is alive, every
/// command routed to <see cref="Topology"/> runs on <see cref="HostId"/>. Dispose the handle to release
/// the pin, or scope it with <c>using</c>.
/// </summary>
/// <remarks>
/// A pin belongs to a topology, not to an individual handle. Pinning the same topology again replaces its
/// pin, so disposing the older handle has no effect. Disposing this handle twice also has no effect. As
/// with <see cref="DataContext"/>, do not pin and dispose concurrently from multiple threads.
/// </remarks>
public sealed class HostPin : IDisposable
{
    private DataContext? _context;

    internal HostPin(DataContext context, string topology, string hostId)
    {
        _context = context;
        Topology = topology;
        HostId = hostId;
    }

    /// <summary>The topology this pin applies to.</summary>
    public string Topology { get; }

    /// <summary>The host routed commands are pinned to.</summary>
    public string HostId { get; }

    /// <summary>
    /// Releases the pin. When the topology has been pinned to another host since this handle was
    /// created, the newer pin stays; disposing twice does nothing.
    /// </summary>
    public void Dispose()
    {
        var context = Interlocked.Exchange(ref _context, null);
        context?.ReleaseHostPin(Topology, HostId);
    }
}
