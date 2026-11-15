namespace NextORM.Core;

/// <summary>
/// A host pin created by <see cref="DataContext.PinHost"/>: while it is alive, every command routed to
/// <see cref="Topology"/> runs on <see cref="HostId"/>. Disposing the handle releases the pin, so a pin
/// can be scoped with <c>using</c> instead of being tracked by hand.
/// </summary>
/// <remarks>
/// A pin belongs to the topology, not to the handle: pinning the same topology again replaces the pin,
/// and disposing the older handle then does nothing. Disposing twice is harmless. Like the context, the
/// handle is not thread-safe for concurrent disposal and pinning.
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
