namespace NextORM.Core;

/// <summary>
/// The topology a routing rule sends an entity type to.
/// </summary>
/// <param name="Topology">The name of a topology registered on the context's builder.</param>
public readonly record struct RouteTarget(string Topology);

/// <summary>
/// Selects the topology for an entity type, optionally using the command's mapping variant.
/// </summary>
public interface IEntityRouteResolver
{
    /// <summary>Resolves the topology of <paramref name="entityType"/> under <paramref name="variant"/>.</summary>
    /// <param name="entityType">The entity type being routed.</param>
    /// <param name="variant">The mapping variant the command runs under.</param>
    /// <param name="target">Receives the topology when the rule applies.</param>
    /// <returns><see langword="true"/> when this rule claims the type.</returns>
    bool TryResolve(Type entityType, in MappingVariant variant, out RouteTarget target);
}

/// <summary>
/// Declares the topology for an entity type or an entire assembly. A type-level attribute takes
/// precedence over an assembly-level attribute. If matching rules select different topologies, routing
/// fails instead of choosing one silently.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly,
    AllowMultiple = false,
    Inherited = true)]
public sealed class RouteToAttribute : Attribute
{
    /// <summary>Creates the rule.</summary>
    /// <param name="topology">The name of a topology registered on the context's builder.</param>
    /// <exception cref="ArgumentException">The name is empty or whitespace.</exception>
    public RouteToAttribute(string topology)
    {
        if (string.IsNullOrWhiteSpace(topology))
            throw new ArgumentException("A route target must not be empty.", nameof(topology));

        Topology = topology;
    }

    /// <summary>The topology name.</summary>
    public string Topology { get; }
}
