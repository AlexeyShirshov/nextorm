# Generate models from an existing database

If you have a SQLite database, use the Nextorm scaffolding tool to generate C# models and a
`RepositoryContext`, then run a query through the generated context. This example uses a sample
database so you can try it without connecting to an application database. Scaffolding reads the
schema and writes C# files; it doesn't create or migrate the database.

## Before you begin

- Use Linux x64 with glibc 2.38 or later. The published tool for this example is a native executable.
- Install the .NET 11 SDK and the `sqlite3` command-line client.
- Open Bash in a directory where you can create `ShopDemo`. Run all commands from that directory.

## Generate the C# files

1. Install [`Nextorm.Scaffold`](https://www.nuget.org/packages/Nextorm.Scaffold) as a global
   .NET tool:

   ```bash
   dotnet tool install --global Nextorm.Scaffold --version 1.0.0
   ```

   If Bash can't find `dotnet-nextorm` after installation, add `$HOME/.dotnet/tools` to `PATH`.

2. Create the application that will use the generated files:

   ```bash
   dotnet new console --name ShopDemo --framework net11.0
   ```

3. Add the SQLite provider to the application:

   ```bash
   dotnet add ShopDemo/ShopDemo.csproj package nextorm.sqlite --version 1.0.11
   ```

4. Create a sample database with one product:

   ```bash
   sqlite3 ShopDemo/shop.db <<'SQL'
   CREATE TABLE products (
     id INTEGER PRIMARY KEY,
     name TEXT NOT NULL
   );
   INSERT INTO products (id, name) VALUES (1, 'Notebook');
   SQL
   ```

5. Run the scaffolder:

   ```bash
   dotnet-nextorm scaffold --provider sqlite \
     --database ShopDemo/shop.db --output-dir ShopDemo/Generated \
     --namespace ShopDemo.Data --context ShopRepositoryContext
   ```

   The tool reports `Generated 2 file(s)` followed by the output path. In `ShopDemo/Generated`
   you get `Models/Products.g.cs` with the table and column mappings and
   `ShopRepositoryContext.cs` with a `Products` repository.

## Run a query

1. Replace `ShopDemo/Program.cs` with the following code. The SQLite provider creates the data
   context; the generated `ShopRepositoryContext` uses that same context to run queries:

   ```csharp
   using Microsoft.Data.Sqlite;
   using NextORM.Core;
   using NextORM.Sqlite;
   using ShopDemo.Data;

   var settings = new SqliteConnectionStringBuilder
   {
       DataSource = args[0],
       Mode = SqliteOpenMode.ReadOnly,
   };
   using var connection = new SqliteConnection(settings.ToString());
   connection.Open();

   var builder = new DataContextBuilder().UseSqlite(connection);
   using var dataContext = builder.CreateDataContext();
   var shop = new ShopRepositoryContext(dataContext);
   Console.WriteLine(await shop.Products.CountAsync(CancellationToken.None));
   ```

2. Run the application. It prints `1`, the number of rows in `products`:

   ```bash
   dotnet run --project ShopDemo -- "$PWD/ShopDemo/shop.db"
   ```

## Next steps

- To select tables or refresh the model after a schema change, see [Scaffolding providers and
  options](../advanced/schema-scaffolding.md).
- To share common queries across services, see [Repositories and application
  context](../guide/37-repositories.md).
