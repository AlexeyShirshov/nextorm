using System.Collections.Concurrent;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// The routing rules of one builder: structural (attribute, namespace, assembly) and custom
/// (<see cref="IEntityRouteResolver"/>). Resolution is cheap and allocation-free on the hot path: the
/// structural parts are cached per type/assembly, custom rules are consulted in registration order.
/// </summary>
internal sealed class RouteTable
{
    private readonly List<IEntityRouteResolver> _resolvers = [];
    private readonly List<(string Prefix, string Topology)> _namespaces = [];
    private readonly List<(Assembly Assembly, string Topology)> _assemblies = [];
    private readonly ConcurrentDictionary<Type, RouteTarget?> _typeAttributes = new();
    private readonly ConcurrentDictionary<Assembly, RouteTarget?> _assemblyAttributes = new();
    private readonly ConcurrentDictionary<Type, RouteTarget?> _namespaceTargets = new();
    private readonly ConcurrentDictionary<Assembly, RouteTarget?> _assemblyTargets = new();

    /// <summary>Every topology name a declared structural rule points at, for construction-time validation.</summary>
    internal IEnumerable<string> DeclaredTopologies
    {
        get
        {
            foreach (var (_, topology) in _namespaces)
                yield return topology;
            foreach (var (_, topology) in _assemblies)
                yield return topology;
        }
    }

    /// <summary>Appends a custom rule; rules are consulted in registration order.</summary>
    internal void AddResolver(IEntityRouteResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolvers.Add(resolver);
    }

    /// <summary>Routes every type whose full name is under <paramref name="namespacePrefix"/>.</summary>
    internal void AddNamespace(string namespacePrefix, string topology)
    {
        if (string.IsNullOrWhiteSpace(namespacePrefix))
            throw new ArgumentException("A namespace prefix must not be empty.", nameof(namespacePrefix));
        if (string.IsNullOrWhiteSpace(topology))
            throw new ArgumentException("A route target must not be empty.", nameof(topology));

        _namespaces.Add((namespacePrefix.TrimEnd('.'), topology));
        _namespaceTargets.Clear();
    }

    /// <summary>Routes every type declared in <paramref name="assembly"/>.</summary>
    internal void AddAssembly(Assembly assembly, string topology)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (string.IsNullOrWhiteSpace(topology))
            throw new ArgumentException("A route target must not be empty.", nameof(topology));

        _assemblies.Add((assembly, topology));
        _assemblyTargets.Clear();
    }

    /// <summary>
    /// Resolves the topology of <paramref name="entityType"/>, or <see langword="null"/> when no rule
    /// claims it. Two rules with different targets for one type are an error: a type belongs to exactly
    /// one topology.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two matching rules disagree on the topology.</exception>
    internal RouteTarget? Resolve(Type? entityType, in MappingScope scope)
    {
        // Attribute rules are implicit, so a table with no registered rule can still route: the
        // structural lookups below are cached per type/assembly and cost a dictionary hit.
        if (entityType is null)
            return null;

        RouteTarget? found = null;

        Consider(AttributeTarget(entityType));

        foreach (var resolver in _resolvers)
        {
            if (resolver.TryResolve(entityType, in scope, out var target))
                Consider(target);
        }

        Consider(NamespaceTarget(entityType));
        Consider(AssemblyTarget(entityType));

        return found;

        void Consider(RouteTarget? candidate)
        {
            if (candidate is not { } target)
                return;

            if (found is { } existing)
            {
                if (existing != target)
                {
                    throw new InvalidOperationException(
                        $"Entity '{entityType}' is routed to both '{existing.Topology}' and '{target.Topology}'. " +
                        "A type belongs to exactly one topology; remove one of the conflicting rules.");
                }

                return;
            }

            found = target;
        }
    }

    private RouteTarget? AttributeTarget(Type type)
    {
        var typeTarget = _typeAttributes.GetOrAdd(type, static t =>
        {
            var attribute = t.GetCustomAttribute<RouteToAttribute>(inherit: true);
            return attribute is null ? null : new RouteTarget(attribute.Topology);
        });

        if (typeTarget is { } explicitTarget)
            return explicitTarget;

        return _assemblyAttributes.GetOrAdd(type.Assembly, static assembly =>
        {
            var attribute = assembly.GetCustomAttribute<RouteToAttribute>();
            return attribute is null ? null : new RouteTarget(attribute.Topology);
        });
    }

    private RouteTarget? NamespaceTarget(Type type)
    {
        if (_namespaces.Count == 0)
            return null;

        return _namespaceTargets.GetOrAdd(type, static (t, prefixes) =>
        {
            var fullName = t.FullName ?? t.Name;
            string? bestPrefix = null;
            string? bestTopology = null;

            foreach (var (prefix, topology) in prefixes)
            {
                if (fullName.Length <= prefix.Length || !fullName.StartsWith(prefix, StringComparison.Ordinal))
                    continue;
                if (fullName[prefix.Length] != '.')
                    continue;
                if (bestPrefix is null || prefix.Length > bestPrefix.Length)
                {
                    bestPrefix = prefix;
                    bestTopology = topology;
                }
            }

            return bestTopology is null ? null : new RouteTarget(bestTopology);
        }, _namespaces);
    }

    private RouteTarget? AssemblyTarget(Type type)
    {
        if (_assemblies.Count == 0)
            return null;

        return _assemblyTargets.GetOrAdd(type.Assembly, static (assembly, rules) =>
        {
            foreach (var (declared, topology) in rules)
            {
                if (declared == assembly)
                    return new RouteTarget(topology);
            }

            return null;
        }, _assemblies);
    }
}

/// <summary>
/// The resolved routing decision of one command: the topology name, or none when the command stays on
/// the context's own connection.
/// </summary>
internal readonly record struct RoutePlan(string? TopologyName)
{
    /// <summary>No topology: the command runs on the context connection.</summary>
    public static RoutePlan None => default;

    /// <summary>Whether a topology was selected.</summary>
    public bool HasTopology => !string.IsNullOrEmpty(TopologyName);
}
