# Scaffolding providers and options

Use this reference to select a generator, configure its output, or regenerate models after a
database schema change. [`Nextorm.Scaffold`](https://www.nuget.org/packages/Nextorm.Scaffold) is a
.NET tool for Linux x64, separate from the `nextorm.extensions` query-operator NuGet package.
Version `1.0.0` requires glibc 2.38 or later.
Run it with `dotnet-nextorm scaffold`. For installation and a first query, see
[Generate models from an existing database](../getting-started/05-existing-database.md).
The internal `Nextorm.Scaffolding` project lives in the
[`nextorm.extensions` repository](https://github.com/AlexeyShirshov/nextorm.extensions).

## Database coverage

The CLI reads database metadata and writes mapped C# classes plus a `RepositoryContext` over
the application's `IDataContext`. It doesn't generate wrappers for stored procedures or table-valued
functions, perform migrations, or add change tracking. The available objects depend on the generator:

| Database | CLI `--provider` | Input and objects |
|---|---|---|
| SQLite | `sqlite` | Existing file; tables, views, and virtual tables in `main`. Generates navigation properties for declared foreign keys to included tables. |
| PostgreSQL | `postgresql` | Live connection; ordinary, partitioned, and foreign tables, plus views and materialized views. |
| MySQL | `mysql` | Live connection; base tables in one schema. Use `mariadb` for a MariaDB server. |
| MariaDB | `mariadb` | Live connection; base tables and views in one schema. |
| SQL Server | `sqlserver` or `mssql` | Live connection; user tables, views, and external tables. |
| ClickHouse | Not available | [`ClickHouseSchemaGenerator.GenerateFromCatalog`](https://github.com/AlexeyShirshov/nextorm.extensions/blob/main/src/Generators/Nextorm.Contexts.ClickHouse/ClickHouseSchemaGenerator.cs) accepts supplied table and view metadata. It doesn't connect to a server and doesn't infer relational keys. |

For programmatic generation from one named MariaDB table or view, see
[`LiveCatalogGenerator.Generate`](https://github.com/AlexeyShirshov/nextorm.extensions/blob/main/src/Generators/Nextorm.Contexts.LiveCatalog/LiveCatalogGenerator.cs).
Each generator maps a subset of SQL types. If a selected object's column type isn't supported, the
generator reports the object, column, and SQL type rather than silently omitting the column.

## CLI options

The following options control input, selection, and output:

| Option | Meaning |
|---|---|
| `--provider` | Required. Selects the CLI provider. |
| `--database` | Required for SQLite and rejected by the other providers. Path to an existing SQLite file. |
| `--connection-env` | Required for PostgreSQL, MySQL, MariaDB, and SQL Server. Names an environment variable containing the connection string. SQLite rejects this option. |
| `--schema`, `--exclude-schema` | Include or exclude schemas. MySQL and MariaDB require exactly one included schema that isn't excluded. SQLite rejects schema filters. |
| `--table`, `--exclude-table` | Include or exclude objects by exact, case-sensitive database name without the schema prefix. To select an object in a particular schema, also filter the schema. Exclusions take precedence. |
| `--namespace`, `--context`, `--output-dir` | Required. Set the C# namespace, the PascalCase context name, and the destination directory. |
| `--models-dir` | Sets the model subdirectory under `--output-dir`. The default is `Models`. |
| `--single-file` | Writes models and context to one file. Can't be combined with `--models-dir`. |
| `--import` | Reads options from a JSON file. |
| `--force` | Permits overwriting files generated for the same context and removes obsolete generated models from the selected model directory. |
| `--customize` | Not available in the published Native AOT tool. In non-AOT builds, loads a trusted `.dll` exposing one `IScaffoldInterceptor` implementation with a public parameterless constructor. |

Use `--connection-env` to keep the connection string out of the command and committed
configuration. Use a database account with only the access needed to read the relevant schema
metadata.

## JSON configuration

Pass `--import` the path to a JSON object whose keys are option names without `--`. This example
selects two MySQL tables. Set `SHOP_DB_CONNECTION` in the environment before running the tool:

```json
{
  "provider": "mysql",
  "connection-env": "SHOP_DB_CONNECTION",
  "schema": ["shop"],
  "table": ["products", "orders"],
  "namespace": "ShopDemo.Data",
  "context": "ShopRepositoryContext",
  "output-dir": "Generated"
}
```

The command-line value wins for scalar options. List values from both sources are combined.
Setting `force` or `single-file` to `true` in the file enables that flag. Relative values for
`database`, `output-dir`, and `customize` in the file are resolved relative to the file's directory.
The `models-dir` value is relative to the output directory.

## Regeneration

Without `--force`, the CLI refuses to replace existing output. With `--force`, it checks the
first-line ownership marker before replacing a file; it still refuses to replace a file without
the matching marker. It also deletes obsolete `*.g.cs` files in the selected model directory
if they have the matching marker.

Keep hand-written code in separate files. Put application-specific queries in a separate class
or extension method rather than editing generated files. After regenerating, review the diff,
build the application, and run a query against the updated schema.
