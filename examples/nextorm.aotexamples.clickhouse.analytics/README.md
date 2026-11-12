# nextorm.aotexamples.clickhouse.analytics

The **NativeAOT probe for the ClickHouse provider**: the same analytics queries as
[`nextorm.examples.clickhouse.analytics`](../nextorm.examples.clickhouse.analytics/README.md), reduced
to the nextorm arm so the process can be published with `PublishAot` and executed natively.

Deliberately absent — the project exists to keep the AOT surface small:

- no **Testcontainers**: the database is never provisioned here;
- no **BenchmarkDotNet**;
- no **linq2db**: there is no second arm and no reflection-heavy comparison library.

Like the other two probes, this one consumes the **published `nextorm` / `nextorm.clickhouse` /
`nextorm.extensions` packages** (everything the queries use, including `SelectGroup`, is part of the
shipped `1.0.9` surface). The comparison example still binds the workspace projects; the probe measures
what ships on nuget.org.

## Running

Point it at a ClickHouse server that already holds `datasets.hits_v1`:

```bash
dotnet run --project examples/nextorm.aotexamples.clickhouse.analytics -c Release -- \
  --connection "Host=127.0.0.1;Port=8123;Username=default;Database=default"

# or through the environment
CLICKHOUSE_ANALYTICS_CONNECTION="Host=…;Port=8123" \
  dotnet run --project examples/nextorm.aotexamples.clickhouse.analytics -c Release
```

`--only Name1,Name2` runs a subset (the names are the method names of `Queries.cs`). Without a
connection string the process prints usage and exits with code 2 — it never starts a container.

Every query prints its row count and a **digest** of the formatted rows, so a JIT run and a native run
can be compared line by line: the same input must produce the same counts and the same digests.

## NativeAOT

```bash
dotnet publish examples/nextorm.aotexamples.clickhouse.analytics -c Release -r linux-x64
./examples/nextorm.aotexamples.clickhouse.analytics/bin/linux/Release/net11.0/linux-x64/publish/nextorm.aotexamples.clickhouse.analytics \
  --connection "Host=…;Port=8123"
```

`PublishAot=true` lives in the csproj, so a plain `dotnet publish` is the whole command; the
`IlcTreatWarningsAsErrors`/`WarningsNotAsErrors` pair is repository policy (`TreatWarningsAsErrors` is
global here), not nextorm configuration. The standard NativeAOT prerequisites apply (`clang`,
`zlib1g-dev`, the RID-specific native toolchain).

Support for the ClickHouse provider under NativeAOT is **not** claimed by
[`docs/infrastructure/05-nativeaot.md`](../../docs/infrastructure/05-nativeaot.md) yet (SQLite is the
verified provider); this project is the reproducer that measures what works.
