using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Collects routing configuration: the topologies a command may run on and the rules that choose one
/// per entity type.
/// </summary>
public sealed class DataRoutingBuilder
{
    private readonly DataContextBuilder _builder;

    internal DataRoutingBuilder(DataContextBuilder builder) => _builder = builder;

    /// <summary>Registers a topology that rules can refer to by name.</summary>
    /// <param name="topology">The topology to register.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataRoutingBuilder AddTopology(IHostTopology topology)
    {
        _builder.AddTopology(topology);
        return this;
    }

    /// <summary>Designates a registered topology as the one unrouted commands run on.</summary>
    /// <param name="topologyName">The name of a topology registered with <see cref="AddTopology"/>.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataRoutingBuilder UseDefaultTopology(string topologyName)
    {
        _builder.UseDefaultTopology(topologyName);
        return this;
    }

    /// <summary>
    /// Registers a one-host topology under <paramref name="topologyName"/> and makes it the default for
    /// unrouted commands.
    /// </summary>
    /// <param name="topologyName">The name to register the topology under.</param>
    /// <param name="connectionString">The connection string that addresses its only host.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataRoutingBuilder UseDefaultTopology(string topologyName, string connectionString)
    {
        AddTopology(new RoundRobinTopology(topologyName, connectionString));
        _builder.UseDefaultTopology(topologyName);
        return this;
    }

    /// <summary>Routes every entity type under <paramref name="namespacePrefix"/> to a topology.</summary>
    /// <param name="namespacePrefix">The namespace (or namespace prefix) the rule covers.</param>
    /// <param name="topology">The registered topology name.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataRoutingBuilder RouteNamespace(string namespacePrefix, string topology)
    {
        _builder.RouteNamespace(namespacePrefix, topology);
        return this;
    }

    /// <summary>Routes every entity type declared in <paramref name="assembly"/> to a topology.</summary>
    /// <param name="assembly">The assembly the rule covers.</param>
    /// <param name="topology">The registered topology name.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataRoutingBuilder RouteAssembly(Assembly assembly, string topology)
    {
        _builder.RouteAssembly(assembly, topology);
        return this;
    }

    /// <summary>Appends a custom routing rule; rules are consulted in registration order.</summary>
    /// <param name="resolver">The rule.</param>
    /// <returns>This builder, to allow chaining.</returns>
    public DataRoutingBuilder UseResolver(IEntityRouteResolver resolver)
    {
        _builder.UseRouteResolver(resolver);
        return this;
    }
}

/// <summary>
/// Enables topology-based routing of data on a <see cref="DataContextBuilder"/>.
/// </summary>
public static class DataRoutingBuilderExtensions
{
    /// <summary>
    /// Configures routing: registers topologies and the rules (attribute, namespace, assembly, custom)
    /// that decide which topology executes a command.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="configure">The routing configuration, or <see langword="null"/> to keep it empty.</param>
    /// <returns>The same builder, to allow chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static DataContextBuilder UseDataRouting(
        this DataContextBuilder builder,
        Action<DataRoutingBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        configure?.Invoke(new DataRoutingBuilder(builder));
        return builder;
    }
}
