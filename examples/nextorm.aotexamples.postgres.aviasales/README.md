# nextorm.aotexamples.postgres.aviasales

The **NativeAOT probe for the PostgreSQL provider**: the same Aviasales queries as
[`nextorm.examples.postgres.aviasales`](../nextorm.examples.postgres.aviasales/README.md), reduced to
the nextorm arm so the process can be published with `PublishAot` and executed natively.

Deliberately absent — the project exists to keep the AOT surface small:

- no **Testcontainers**: the database is never provisioned here;
- no **BenchmarkDotNet**;
- no **linq2db**: there is no second arm and no reflection-heavy comparison library;
- no `src/` `ProjectReference`: the project consumes the **published `nextorm` / `nextorm.postgres` /
  `nextorm.extensions` packages** pinned by `NextOrmVersion` in
  [`Directory.Packages.props`](../Directory.Packages.props), exactly like a real consumer.

## Running

Point it at a PostgreSQL server that already holds the Aviasales demo database (`bookings` schema):

```bash
dotnet run --project examples/nextorm.aotexamples.postgres.aviasales -c Release -- \
  --connection "Host=127.0.0.1;Port=5432;Username=postgres;Password=postgres;Database=demo"

# or through the environment
AVIASALES_CONNECTION="Host=…;Database=demo" \
  dotnet run --project examples/nextorm.aotexamples.postgres.aviasales -c Release
```

`--only Name1,Name2` runs a subset (the names are the method names of `Queries.cs`). Without a
connection string the process prints usage and exits with code 2 — it never starts a container.

Every query prints its row count and a **digest** of the formatted rows, so a JIT run and a native run
can be compared line by line: the same input must produce the same counts and the same digests.

## NativeAOT

```bash
dotnet publish examples/nextorm.aotexamples.postgres.aviasales -c Release -r linux-x64
./examples/nextorm.aotexamples.postgres.aviasales/bin/linux/Release/net11.0/linux-x64/publish/nextorm.aotexamples.postgres.aviasales \
  --connection "Host=…;Database=demo"
```

`PublishAot=true` lives in the csproj, so a plain `dotnet publish` is the whole command; the
`IlcTreatWarningsAsErrors`/`WarningsNotAsErrors` pair is repository policy (`TreatWarningsAsErrors` is
global here), not nextorm configuration. The standard NativeAOT prerequisites apply (`clang`,
`zlib1g-dev`, the RID-specific native toolchain).

Support for the PostgreSQL provider under NativeAOT is **not** claimed by
[`docs/infrastructure/05-nativeaot.md`](../../docs/infrastructure/05-nativeaot.md) yet (SQLite is the
verified provider); this project is the reproducer that measures what works.
