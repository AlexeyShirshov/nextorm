# nextorm.aotexamples.mssql.adventureworks

The **NativeAOT probe for the SQL Server provider**: the same AdventureWorks queries as
[`nextorm.examples.mssql.adventureworks`](../nextorm.examples.mssql.adventureworks/README.md), reduced
to the nextorm arm so the process can be published with `PublishAot` and executed natively.

Deliberately absent — the project exists to keep the AOT surface small:

- no **Testcontainers**: the database is never provisioned here;
- no **BenchmarkDotNet**;
- no **linq2db**: there is no second arm and no reflection-heavy comparison library.

Like the PostgreSQL probe, this one consumes the **published `nextorm` / `nextorm.sqlserver` /
`nextorm.extensions` packages**. The typed `Pivot` overload used by `QuarterlyPivot` has been part of
the shipped `nextorm.sqlserver` surface since `1.0.9` (`PublicAPI.Shipped.txt` lists both overloads), so
nothing here needs the source projects — the comparison example's note about an unpublished `Pivot`
(#232) predates that release.

## Running

Point it at a SQL Server that already holds the AdventureWorks 2022 database. The database name is the
one the comparison example restores, `AdventureWorks` (the `.bak` file is called
`AdventureWorks2022.bak`; the catalog is not):

```bash
dotnet run --project examples/nextorm.aotexamples.mssql.adventureworks -c Release -- \
  --connection "Server=127.0.0.1,1433;Database=AdventureWorks;User Id=sa;Password=…;TrustServerCertificate=True"

# or through the environment
ADVENTUREWORKS_CONNECTION="Server=…;Database=AdventureWorks" \
  dotnet run --project examples/nextorm.aotexamples.mssql.adventureworks -c Release
```

Every command gets a 600-second timeout; the aggregations scan millions of rows and do not fit the
30-second provider default on a cold server.

`--only Name1,Name2` runs a subset (the names are the method names of `Queries.cs`). Without a
connection string the process prints usage and exits with code 2 — it never starts a container.

Every query prints its row count and a **digest** of the formatted rows, so a JIT run and a native run
can be compared line by line: the same input must produce the same counts and the same digests.

## NativeAOT

```bash
dotnet publish examples/nextorm.aotexamples.mssql.adventureworks -c Release -r linux-x64
./examples/nextorm.aotexamples.mssql.adventureworks/bin/linux/Release/net11.0/linux-x64/publish/nextorm.aotexamples.mssql.adventureworks \
  --connection "Server=…;Database=AdventureWorks"
```

`PublishAot=true` lives in the csproj, so a plain `dotnet publish` is the whole command; the
`IlcTreatWarningsAsErrors`/`WarningsNotAsErrors` pair is repository policy (`TreatWarningsAsErrors` is
global here), not nextorm configuration. The standard NativeAOT prerequisites apply (`clang`,
`zlib1g-dev`, the RID-specific native toolchain).

Native support for the SQL Server provider is **measured, not guaranteed**, by this probe: the
[current matrix](../../docs/infrastructure/05-nativeaot.md) lists what runs natively and what does not.
The project pins the latest **published** `nextorm` packages (`NextOrmVersion` in
[`Directory.Packages.props`](../Directory.Packages.props)), so a run reproduces the **released**
surface; the guide's current numbers are for the development build of the next release. Released
results are tracked in [`VERIFICATION.md`](../VERIFICATION.md).
