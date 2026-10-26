# Sequences and UUID generators

Server-side value generators: sequences advance and read, and UUID generators mint unique
identifiers. Neither has a single portable spelling, because the providers do not offer the same set
of operations. For sequences, PostgreSQL, MariaDB and SQL Server are supported today.

| Operation | PostgreSQL | MariaDB | SQL Server |
|---|---|---|---|
| Next value | `SqlFunctions.Postgres.nextval(seq)` | `SqlFunctions.MySql.nextval(seq)` / `next_value_for(seq)` | `SqlFunctions.SqlServer.next_value_for(seq)` / `nextval(seq)` |
| Set the next value | `SqlFunctions.Postgres.setval(seq, value)` | `SqlFunctions.MySql.setval(seq, value)` | — |
| Current value (session) | `SqlFunctions.Postgres.currval(seq)` | — | — |
| Last value (session/connection) | `SqlFunctions.Postgres.lastval()` | `SqlFunctions.MySql.lastval(seq)` | — |

## PostgreSQL

The sequence name is cast to `regclass`. The functions are part of the extended scalar library
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions),
PostgreSQL only):

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.nextval(sequence)` | `nextval(cast(sequence as regclass))` |
| `SqlFunctions.Postgres.setval(sequence, value)` | `setval(cast(sequence as regclass), value)` |
| `SqlFunctions.Postgres.currval(sequence)` | `currval(cast(sequence as regclass))` |
| `SqlFunctions.Postgres.lastval()` | `lastval()` |

```csharp
var next = dataContext.From<IComplexEntity>()
    .Select(e => SqlFunctions.Postgres.nextval("order_id_seq"))
    .First();
```

`currval` and `lastval` are **session-scoped**: they require the same connection that last advanced
the sequence.

## MariaDB

The sequence name is taken as a string and emitted as an identifier (not a quoted literal), so
`SqlFunctions.MySql.next_value_for("order_seq")` renders `next value for order_seq`:

| C# | SQL |
|---|---|
| `SqlFunctions.MySql.next_value_for(sequence)` | `next value for sequence` |
| `SqlFunctions.MySql.nextval(sequence)` | `nextval(sequence)` |
| `SqlFunctions.MySql.setval(sequence, value)` | `setval(sequence, value)` |
| `SqlFunctions.MySql.lastval(sequence)` | `lastval(sequence)` |

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new { Id = SqlFunctions.MySql.nextval("order_seq") })
    .ToList();
```

These names are MariaDB-only: MySQL, PostgreSQL, SQL Server, SQLite, ClickHouse and the in-memory
context reject them with `NotSupportedException`. `lastval` reads the **connection's** last generated
value.

## SQL Server

SQL Server reads a sequence through `NEXT VALUE FOR`, exposed by
`SqlFunctions.SqlServer.next_value_for(sequence)` and its alias `nextval(sequence)` (both return CLR
`long`). The name must be a constant string naming one object, written as either one component
(`name`) or two (`schema.name`); each component is bracket-quoted separately with an embedded `]`
doubled, so raw name text cannot escape the identifier:

| C# | SQL |
|---|---|
| `SqlFunctions.SqlServer.next_value_for(sequence)` | `next value for [sequence]` |
| `SqlFunctions.SqlServer.next_value_for("dbo.sequence")` | `next value for [dbo].[sequence]` |
| `SqlFunctions.SqlServer.nextval(sequence)` | `next value for [sequence]` |

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new { Id = SqlFunctions.SqlServer.next_value_for("order_seq") })
    .ToList();
```

A non-constant or captured name (a column, a captured local, `default`/`null`) is rejected with
`NotSupportedException`, and the name is never bound as a SQL parameter — it is an identifier, not a
value. SQL Server has no session-scoped `currval`/`lastval` and no value-returning `setval` (its
equivalent is the DDL `ALTER SEQUENCE ... RESTART WITH`), so only the next value is exposed. These
names require a provider that reports them through
[`ISqlDialect.SqlServerFunctions`](xref:NextORM.Core.ISqlDialect.SqlServerFunctions); every other
provider rejects them. Tracked in
[#218](https://github.com/AlexeyShirshov/nextorm/issues/218).

## UUID generators

`SqlFunctions.Sql.gen_random_uuid()` (random v4) and `uuidv7()` are cross-provider
([`UuidGenerators`](xref:NextORM.Core.ISqlDialect.UuidGenerators)):

| C# | PostgreSQL | SQL Server | MySQL | MariaDB | ClickHouse | SQLite |
|---|---|---|---|---|---|---|
| `gen_random_uuid()` | `gen_random_uuid()` (13+) | `newid()` | — | `uuid_v4()` (11.7+) | `generateUUIDv4()` | — |
| `uuidv7()` | `uuidv7()` (18+) | — | — | `uuid_v7()` (11.7+) | `generateUUIDv7()` | — |

MySQL has only `UUID()` (v1) and SQLite has no UUID generator, so both reject the calls. On MariaDB both
generators require an explicit server version 11.7 or later; an **unversioned** `MariaDbDataContext` refuses
them with `NotSupportedException`, and `UUID()` (v1) is never substituted. These are
server-side generators, evaluated per row by the database; `Guid.NewGuid()` is a client-side value and
is not a substitute. SQL Server additionally exposes `newsequentialid()`
(`SqlFunctions.SqlServer.newsequentialid()`), which is valid only as a column `DEFAULT` and not in an
ordinary `SELECT`.
