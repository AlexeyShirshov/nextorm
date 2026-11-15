# Routing data to topologies

Routing decides which connection executes a statement. nextorm keeps the decision separate from the
query: the query is written against an entity type as usual, and a set of rules maps that type to a
**topology**, a named source of connections. The engine asks the topology for a host before every routed
command and runs the statement there.

Two kinds of rule produce the mapping:

- **Structure** — an attribute on the entity type or its assembly, a namespace prefix, or a custom
  resolver. The rule is a property of the code.
- **Data** — the sharding router turns a shard key or a bucket scope into a topology. This is the same
  mechanism described in [Sharding and horizontal partitioning](35-sharding-and-horizontal-partitioning.md),
  seen from the routing side.

A topology may hold the servers of a shard ring, or several plain connections that are picked in turn,
or anything else that can name a connection. The engine never interprets what is behind it: weight,
health and load are the topology's own business.

## Route by structure

Register the topologies and the rules on the context builder:

```csharp
var builder = new DataContextBuilder()
    .UseDataRouting(routing => routing
        .AddTopology(new RoundRobinTopology(
            "reports",
            "Host=reports-1;Database=warehouse",
            "Host=reports-2;Database=warehouse"))
        .RouteNamespace("MyApp.Reports", "reports"));

using var context = new PostgresDataContext(
    "Host=app-1;Database=app",
    builder);
```

`RoundRobinTopology` is the reference implementation for routing without sharding: one host per
command, in rotation. Host ids are `host-1`, `host-2`, … in declaration order, or any ids you choose
through the `IEnumerable<HostTarget>` constructor.

The other rules are the same shape:

```csharp
[RouteTo("crm")]
public sealed class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```

- `[RouteTo("crm")]` on a type routes that type; the same attribute on an assembly routes every type
  declared in it.
- `RouteNamespace("MyApp.Crm", "crm")` routes every type whose full name is under the prefix. When
  several prefixes match, the longest one wins.
- `RouteAssembly(typeof(Customer).Assembly, "crm")` routes a whole assembly.
- `UseResolver(rule)` appends a custom `IEntityRouteResolver`, consulted in registration order.

The order of evaluation is: the type attribute, the assembly attribute, the custom resolvers, the
namespace rules, the assembly rules. A type belongs to exactly one topology, so two rules that disagree
about it are an error rather than a silent preference:

```text
Entity 'MyApp.Crm.Customer' is routed to both 'crm' and 'archive'.
```

A rule that names a topology which is not registered is also an error. Declared namespace and assembly
rules are checked when the context is created; an attribute is checked the first time its type is routed.

## One statement, one topology

A statement is a single server-side operation, so the engine collects every entity type it reads or
writes — the `FROM` source, joins, subqueries, CTEs, set-operation branches and the target of a
data-modifying statement — and requires them to agree:

- all routed sources must resolve to the **same** topology;
- sources with no rule at all are allowed: they ride along on the host the routed sources selected, so a
  sharded table may be joined with a small reference table that exists on every shard.

A statement that would need two different topologies is rejected before it runs:

```text
The statement reads 'Crm.Customer' routed to topology 'crm' and
'Billing.Invoice' routed to topology 'billing'. One SQL statement runs on one host, so its sources must
belong to a single topology; split it into one command per topology.
```

Split such a statement into separate commands, or move both entity types to one topology. nextorm never
merges results across hosts.

## Connections, transactions and pinning

Every host keeps its own lazily opened connection, cached per `topology/host` for the lifetime of the
context and closed with it. The connection string the context was created with is used only by
statements that no rule routes.

A transaction covers one connection. While one is open on the context connection, a command that routes
elsewhere fails with an explicit error instead of writing outside the transaction.

To transact on a routed host, start the transaction through `IRoutedTransactionManager` (the extension
methods are on `DataContext`, so the cast is not needed in the common case):

```csharp
using var transaction = context.BeginTransaction(new RouteTarget("reports"));

context.CreateInsertBuilder<ReportRow>()
    .Value(row => row.Id, 1)
    .Insert();

transaction.Commit();
```

Starting the transaction pins the topology to the chosen host until the transaction ends: every command
routed there joins it instead of picking another host. A command that would leave that host fails, and so
does an explicit pin that would move the topology elsewhere while the transaction is open. One context
carries one transaction at a time; savepoints stay on the context connection.

An explicit pin keeps a session on one server for other reasons — a replica you warmed up, a shard you
are about to load:

```csharp
context.PinHost("reports", "host-2");   // the picker no longer chooses
context.UnpinHost("reports");           // the picker chooses again
```

A topology must return the pinned host from `TryGetHost` (or decline); the context verifies this and
fails rather than silently routing elsewhere.

## Writing a topology

A topology is one property and one method:

```csharp
public sealed class HealthAwareTopology : IHostTopology
{
    public string Name => "reports";

    public bool TryGetHost(in TopologyContext context, out HostTarget host)
    {
        // context.Scope   — the mapping scope the command runs under
        // context.Kind    — CommandKind.Read or CommandKind.Write
        // context.HostId  — a pinned host, or null to choose
        host = context.Kind == CommandKind.Read ? PickReadReplica() : PickPrimary();
        return true;
    }
}
```

`TryGetHost` returns `false` to leave the command on the context connection. Everything else — weighted
selection, request-rate balancing, health checks, a read/write split — is implemented inside the method.

## Relation to sharding

Sharding is the data rule of the same model: the shard key (or the bucket scope of a fan-out) selects
the `"sharding"` topology, and `ShardTopology` turns the bucket into a shard host. Nothing in this page
is required to use sharding, and sharding does not require the structural rules. When both are
configured, the sharding rule is an ordinary custom resolver that claims the sharded entity types.
