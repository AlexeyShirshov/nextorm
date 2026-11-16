namespace NextORM.Core;

/// <summary>
/// The topology a routing rule sends an entity type to.
/// </summary>
/// <param name="Topology">The name of a topology registered on the context's builder.</param>
public readonly record struct RouteTarget(string Topology);

/// <summary>
/// A custom routing rule: decides the topology of an entity type, optionally by the mapping variant
/// (which is how a data rule such as sharding reads the entity's key values).
/// </summary>
public interface IEntityRouteResolver
{
    /// <summary>Resolves the topology of <paramref name="entityType"/> under <paramref name="scope"/>.</summary>
    /// <param name="entityType">The entity type being routed.</param>
    /// <param name="scope">The mapping variant the command runs under.</param>
    /// <param name="target">Receives the topology when the rule applies.</param>
    /// <returns><see langword="true"/> when this rule claims the type.</returns>
    bool TryResolve(Type entityType, in MappingVariant scope, out RouteTarget target);
}

/// <summary>
/// Declares the topology an entity type (or a whole assembly) belongs to. The structural rule with the
/// highest priority: a type-level attribute wins over assembly-, namespace- and custom rules, and a
/// conflicting target is an error rather than a silent preference.
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
