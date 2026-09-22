# Быстрый старт

> Определите сущность, создайте контекст SQLite, выполните типизированный запрос и выведите строки - самая маленькая сквозная программа на NextORM.

**Предварительные требования:** [Установка](01-installation.md).

## Обзор

Запрос NextORM всегда состоит из четырёх частей:

1. **сущность** (класс или интерфейс), свойства которой отображаются на столбцы;
2. **контекст** ([`IDataContext`](xref:NextORM.Core.IDataContext)), созданный из подключения или строки подключения;
3. **запрос**, построенный с помощью [`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)), [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})), [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) и так далее;
4. **терминальный метод**, такой как [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})), [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})), [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) или [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])), который его выполняет.

[`From`](xref:NextORM.Core.DataContextExtensions.From(NextORM.Core.IDataContext,System.String)) также регистрирует метаданные `T` при первом обращении. Контекст поддерживает освобождение: контекст, созданный из строки подключения, владеет подключением и закрывает его, тогда как контекст, созданный из переданного `DbConnection`, оставляет это подключение открытым.

## Полная минимальная программа

Следующая программа самодостаточна. Она создаёт базу данных SQLite в памяти, наполняет одну таблицу, строит контекст с помощью [`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) и возвращает строки как анонимный тип.

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var setup = connection.CreateCommand())
{
    setup.CommandText =
        "create table simple_entity (id integer primary key);" +
        "insert into simple_entity (id) values (1), (2), (3);";
    setup.ExecuteNonQuery();
}

var builder = new DataContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDataContext();

await foreach (var row in dataContext.From<ISimpleEntity>()
                                   .Select(entity => new { Id = (long)entity.Id })
                                   .ToAsyncEnumerable())
{
    Console.WriteLine($"Id = {row.Id}");
}

// The entity can live in its own file. In a single-file top-level program the type
// declaration must come after the top-level statements.
[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}
```

Вывод:

```text
Id = 1
Id = 2
Id = 3
```

Сгенерированный SQL - это обычный `select` по отображённым таблице и столбцу:

```sql
select id from simple_entity
```

## Использование строки подключения

[`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection)) также принимает путь к файлу (или фрагмент строки подключения SQLite), и тогда контекст создаёт подключение и владеет им:

```csharp
var builder = new DataContextBuilder().UseSqlite("app.db");
using var dataContext = builder.CreateDataContext();
```

> **Примечание:** в сборках `DEBUG` `UseSqlite(string filepath)` выбрасывает `ArgumentException`, когда файл
> не существует. При разработке передавайте `:memory:` или существующий путь; передавайте `DbConnection`,
> чтобы сохранить полный контроль. `UseSqlServer(connectionString)` и `UsePostgres(connectionString)` имеют
> соответствующие строковые перегрузки.

## Чтение данных

В запросе выше используется [`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])). Другие распространённые терминальные методы:

| Терминал | Результат |
|---|---|
| [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) / [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `List<TResult>` |
| [`First`](xref:NextORM.Core.EntityBuilderExtensions.First``1(NextORM.Core.EntityBuilder{``0})) / [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | первая строка; выбрасывает исключение, если последовательность пуста |
| [`FirstOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefault``1(NextORM.Core.EntityBuilder{``0})) / [`FirstOrDefaultAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstOrDefaultAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | первая строка или `default` |
| [`Single`](xref:NextORM.Core.EntityBuilderExtensions.Single``1(NextORM.Core.EntityBuilder{``0})) / [`SingleOrDefault`](xref:NextORM.Core.EntityBuilderExtensions.SingleOrDefault``1(NextORM.Core.EntityBuilder{``0})) (и `…Async`) | ровно одна строка (или `default`) |
| [`Any`](xref:NextORM.Core.EntityBuilderExtensions.Any``1(NextORM.Core.EntityBuilder{``0})) / [`AnyAsync`](xref:NextORM.Core.EntityBuilderExtensions.AnyAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `bool` |
| [`Count`](xref:NextORM.Core.EntityBuilderExtensions.Count``1(NextORM.Core.EntityBuilder{``0})) / [`CountAsync`](xref:NextORM.Core.EntityBuilderExtensions.CountAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])) | `int` |

Каждый терминальный метод имеет синхронную и асинхронную форму; в коде приложения предпочитайте асинхронную форму.

## См. также

* [Сущности и метаданные](03-entities-and-metadata.md) - атрибуты, отображение интерфейс/класс,
  режим [`TableAlias`](xref:NextORM.Core.TableAlias) без сущностей и fluent-конфигурация.
* [Внедрение зависимостей](04-dependency-injection.md) - зарегистрируйте контекст вместо его ручного
  создания.

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:20`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:37`;
`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs:32`;
`tests/nextorm.sqlite.tests/ConnectionManagementTests.cs:115`;
generated SQL: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:111`.
