# Routing data to topologies

Routing chooses which connection executes a statement. You write the query against an entity type as
usual; rules associate that type with a **topology**, a named source of connections. Before each routed
command, the engine asks the topology for a host and runs the statement there.

Two kinds of rule produce the mapping:

- **Structure** — an attribute on the entity type or its assembly, a namespace prefix, or a custom
  resolver. The rule is a property of the code.
- **Data** — the sharding router turns a shard key or a bucket variant into a topology. This is the same
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
        .UseDefaultTopology("main", "Host=app-1;Database=app")
        .AddTopology(new RoundRobinTopology(
            "reports",
            "Host=reports-1;Database=warehouse",
            "Host=reports-2;Database=warehouse"))
        .RouteNamespace("MyApp.Reports", "reports"));

using var context = new PostgresDataContext(builder);
```

A routed context is created from the builder alone. Its own connection — the one that unsharded
entities, raw SQL and the context transaction use — is the **default topology's** host, and a single
connection string is the simplest topology. Passing a connection string to the context constructor as
well is rejected: there would be two answers to "where do commands without a rule run". The
`DataContextBuilder` chain follows the same rule: `UsePostgres(connectionString)` (or
`UseSqlite(filepath)`, …) is the unrouted form, and the parameterless `UsePostgres()` creates the context
from the builder alone.

`RoundRobinTopology` is the reference implementation for routing without sharding: one host per
command, in rotation. Host ids are `host-1`, `host-2`, … in declaration order, or any ids you choose
through the `IEnumerable<HostTarget>` constructor. A one-host round-robin is what `UseDefaultTopology`
registers for a connection string; to use a topology you configured yourself, register it first and
name it — `UseDefaultTopology("main")`.

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

## One batch, one topology

A batch (`CreateBatchBuilder`) is one SQL session and one round trip, not several statements that happen
to travel together, so the rule applies to the batch as a whole: the entity types of every step must
resolve to one topology and one mapping variant. The first routed step decides the host, a step with no
rule rides along on it, and a step that needs another topology or another variant is rejected **while the
batch is rendered** — `ToSql()` fails too, before a connection is opened:

```text
A batch runs on one host, but its statements span topologies 'billing' and 'crm'. A batch is one SQL
session (one round trip), so every statement of it must belong to a single topology; split it into one
batch per topology.
```

A batch with any side-effecting step — DML, a materialisation, an output capture or a raw step — asks
the topology for a writer (`CommandKind.Write`); a batch of result-bearing queries only asks for a
reader. Inside a routed transaction the whole batch joins the transaction's host, and a batch that would
leave it fails like any other command.

### Temporary tables are one batch too

The lazy temporary table is exactly this case. Reading an `AsTempTable` source runs one batch — `DROP`,
`CREATE TEMPORARY TABLE ... AS SELECT`, then the read — on one session, so it must be one host, and the
table is materialised from its source query **under that query's own variant**. Give the source the same
variant as the read:

```csharp
var variant = new MappingVariant("3");

var source = context.From<Order>(variant)     // the rows of bucket 3
    .Select(order => new { order.Id, order.Total })
    .AsTempTable();

var rows = context.From(source, variant)      // the same bucket: one batch, one host
    .Select(row => new { Id = row.GetInt32("Id"), Total = row.GetDecimal("Total") })
    .ToList();
```

A source that resolves to another bucket than the read is rejected: its `CREATE ... AS SELECT` and the
read would have to run on two hosts.

An eager materialisation follows the same rule: `ToTable`/`ToTempTable` writes a literal target name, so
it is routed by the entities its **source query** reads — not by the row type it projects — and runs under
that query's variant. With `DropExisting` the drop accompanies the create to the same host.

`ExecuteRaw`, `ExecuteProcedure` and bulk insert take no part in routing: they run on the context
connection. A batch is how several statements are kept together on one routed host.

## Connections, transactions and pinning

### Context connection and routed hosts

Every host keeps its own lazily opened connection, cached per `topology/host` for the lifetime of the
context and closed with it. The default topology is asked once, when the context is created; its host
becomes the context connection, so it is used by statements that no rule routes, by raw SQL and by the
context transaction. It is stable for the lifetime of the context: pin it or route commands elsewhere
instead of expecting the default host to change per command.

A transaction covers one connection. While a transaction is open on the context connection, a command
that routes elsewhere fails with an explicit error instead of writing outside the transaction.

### Start a transaction on a routed host

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

### Pin a host for a session

An explicit pin keeps a session on one server for other reasons — a replica you warmed up, a shard you
are about to load:

```csharp
using (context.PinHost("reports", "host-2"))   // the picker no longer chooses
{
    // every command routed to "reports" runs on host-2 here
}
// the handle released the pin; the picker chooses again
```

### Choose and release a host id

`host-2` is a **host id** — the `HostTarget.Id` the topology returns for a command, unique inside that
topology. `RoundRobinTopology("reports", cs1, cs2)` names its hosts `host-1`, `host-2`, … in the order the
connection strings are given, so it is the second server above; pass your own `HostTarget`s when you want
the ids you pin by:

```csharp
new RoundRobinTopology("reports",
[
    new HostTarget("eu", "Host=reports-1;Database=warehouse"),
    new HostTarget("us", "Host=reports-2;Database=warehouse"),
]);
```

A shard topology's host ids are its shard ids (`AddShard("pg-2", ...)`, listed by `ShardIds` — and by
`IShardRouter.ShardIds` on the router a sharded context hands out), and `RoundRobinTopology.Hosts` lists
its hosts. An `IHostTopology` of your own decides the ids in `TryGetHost`: the interface has only `Name`
and `TryGetHost`, because hosts can be dynamic, so keep the ids you pin by in your own configuration. The
context does not enumerate hosts. `PinHost` resolves the id through the topology and throws
`ArgumentException` when the topology does not know it, so a stale id fails loudly instead of pinning
silently elsewhere.

`PinHost` returns a handle that releases the pin when disposed, so a pin covers exactly the block you
give it. `UnpinHost("reports")` releases it explicitly when the handle is not kept. A pin belongs to the
topology: pinning the same topology again replaces it, and disposing the older handle then does nothing.

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
        // context.Scope   — the mapping variant the command runs under
        // context.Kind    — CommandKind.Read or CommandKind.Write
        // context.HostId  — a pinned host, or null to choose
        host = context.Kind == CommandKind.Read ? PickReadReplica() : PickPrimary();
        return true;
    }
}
```

`TryGetHost` returns `false` to leave the command on the context's own connection (the default
topology's host). Everything else — weighted
selection, request-rate balancing, health checks, a read/write split — is implemented inside the method.

## Relation to sharding

Sharding is the data rule of the same model: the shard key (or the bucket variant of a fan-out) selects
the `"sharding"` topology, and `ShardTopology` turns the bucket into a shard host. Nothing in this page
is required to use sharding, and sharding does not require the structural rules. When both are
configured, the sharding rule is an ordinary custom resolver that claims the sharded entity types.
