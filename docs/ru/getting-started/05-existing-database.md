# Сгенерируйте модели из существующей базы данных

Если у вас есть база SQLite, инструмент Nextorm может сгенерировать по её схеме классы C# и
`RepositoryContext`. В этом примере вы создадите учебную базу и выполните запрос через
сгенерированный контекст, не подключаясь к базе приложения. Инструмент читает схему и пишет
файлы C#. Он не создаёт базу и не выполняет миграции.

## Перед началом

- Используйте Linux x64 с glibc 2.38 или новее. Опубликованный инструмент — нативная программа.
- Установите .NET 11 SDK и консольную утилиту `sqlite3`.
- Откройте Bash в каталоге, где можно создать `ShopDemo`. Выполняйте все команды из этого каталога.

## Сгенерируйте файлы C#

1. Установите [`Nextorm.Scaffold`](https://www.nuget.org/packages/Nextorm.Scaffold) как глобальный
   инструмент .NET:

   ```bash
   dotnet tool install --global Nextorm.Scaffold --version 1.0.0
   ```

   Если после установки Bash не находит `dotnet-nextorm`, добавьте `$HOME/.dotnet/tools` в `PATH`.

2. Создайте приложение, которое будет использовать сгенерированные файлы:

   ```bash
   dotnet new console --name ShopDemo --framework net11.0
   ```

3. Добавьте в приложение провайдер SQLite:

   ```bash
   dotnet add ShopDemo/ShopDemo.csproj package nextorm.sqlite --version 1.0.11
   ```

4. Создайте учебную базу с одним товаром:

   ```bash
   sqlite3 ShopDemo/shop.db <<'SQL'
   CREATE TABLE products (
     id INTEGER PRIMARY KEY,
     name TEXT NOT NULL
   );
   INSERT INTO products (id, name) VALUES (1, 'Notebook');
   SQL
   ```

5. Запустите генерацию:

   ```bash
   dotnet-nextorm scaffold --provider sqlite \
     --database ShopDemo/shop.db --output-dir ShopDemo/Generated \
     --namespace ShopDemo.Data --context ShopRepositoryContext
   ```

   Инструмент выведет `Generated 2 file(s)` и путь к результату. В `ShopDemo/Generated`
   появятся `Models/Products.g.cs` с маппингом таблицы и столбцов и
   `ShopRepositoryContext.cs` с репозиторием `Products`.

## Выполните запрос

1. Замените содержимое `ShopDemo/Program.cs` следующим кодом. Провайдер SQLite создаёт
   контекст данных, а сгенерированный `ShopRepositoryContext` использует его для запросов:

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

2. Запустите приложение. Оно выведет `1` — число строк в таблице `products`:

   ```bash
   dotnet run --project ShopDemo -- "$PWD/ShopDemo/shop.db"
   ```

## Что дальше

- Чтобы выбрать таблицы или обновить модель после изменения схемы, см.
  [Провайдеры и параметры генерации](../advanced/schema-scaffolding.md).
- Чтобы использовать общие запросы в нескольких сервисах, см.
  [Репозитории и контекст приложения](../guide/37-repositories.md).
