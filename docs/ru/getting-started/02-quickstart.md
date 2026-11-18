# Быстрый старт

Выполните типизированный запрос к SQLite в памяти и выведите результат. Эта страница предназначена для разработчиков на .NET, которые установили NextORM и хотят увидеть полный пример запроса до знакомства с его возможностями.

**Предварительные требования:** [Установка](01-installation.md).

## Полная минимальная программа

Создайте консольный проект .NET и замените содержимое его файла `Program.cs` этим кодом. Оставьте
объявления интерфейсов сущностей после операторов верхнего уровня, как в примере: в C# операторы
верхнего уровня должны идти перед объявлениями типов.

Программа создаёт таблицы `orders` и `customers`, добавляет три заказа и читает идентификатор и
статус каждого заказа. Используется SQLite в памяти, поэтому сервер базы данных не нужен. Сначала
установите в консольный проект пакеты `nextorm` и `nextorm.sqlite`:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

using var connection = new SqliteConnection("Data Source=:memory:");
connection.Open();

using (var setup = connection.CreateCommand())
{
    setup.CommandText =
        "create table customers (id integer primary key, name text not null);" +
        "create table orders (id integer primary key, customer_id integer not null, " +
        "status text not null, total_cents integer not null);" +
        "insert into customers (id, name) values (1, 'Amina'), (2, 'Ravi');" +
        "insert into orders (id, customer_id, status, total_cents) values " +
        "(1, 1, 'Paid', 2550), (2, 2, 'Pending', 1800), (3, 1, 'Paid', 4250);";
    setup.ExecuteNonQuery();
}

var builder = new DataContextBuilder().UseSqlite(connection);
using var dataContext = builder.CreateDataContext();

var orders = await dataContext.From<IOrder>()
    .OrderBy(order => order.Id)
    .Select(order => new { order.Id, order.Status })
    .ToListAsync();

foreach (var order in orders)
{
    Console.WriteLine($"Order {order.Id}: {order.Status}");
}

[SqlTable("orders")]
public interface IOrder
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("customer_id")]
    int CustomerId { get; set; }

    [Column("status")]
    string Status { get; set; }

    [Column("total_cents")]
    int TotalCents { get; set; }
}

[SqlTable("customers")]
public interface ICustomer
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string Name { get; set; }
}
```

Из каталога проекта запустите программу:

```bash
dotnet run
```

Программа выведет:

```text
Order 1: Paid
Order 2: Pending
Order 3: Paid
```

Запрос выбирает две сопоставленные колонки из таблицы `orders`:

```sql
select id, status
 from orders
 order by id
```

## Обзор

Запрос использует сущность с сопоставлением, контекст и последовательность операций. В этом примере
[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
выбирает источник `orders`, [`Select`](xref:NextORM.Core.EntityBuilder`1.Select``1(System.Linq.Expressions.Expression{System.Func{`0,``0}}))
задаёт возвращаемые значения, а
[`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[]))
выполняет запрос и возвращает список.

[`From`](xref:NextORM.Core.DataContextExtensions.From``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
также регистрирует метаданные сущности при первом обращении. Контекст можно освободить. Контекст,
созданный из строки подключения, владеет подключением и закрывает его; контекст, созданный из
переданного `DbConnection`, оставляет подключение открытым. В этом примере подключение передаётся
отдельно и освобождается отдельно.

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

В примере используется `ToListAsync`, чтобы собрать строки в список. Чтобы читать результат
потоком, используйте
[`ToAsyncEnumerable`](xref:NextORM.Core.EntityBuilderExtensions.ToAsyncEnumerable``1(NextORM.Core.EntityBuilder{``0},System.Object[])).
Другие распространённые терминальные методы:

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

* [Фильтрация строк](../guide/03-filtering-where.md) - добавление условий перед выборкой результата.
* [Выбор нужных полей](../guide/02-projections.md) - формирование возвращаемых строк.
* [Соединение таблиц](../guide/04-joins.md) - добавление сведений о клиентах к заказам.
* [Сущности и метаданные](03-entities-and-metadata.md) - атрибуты, отображение интерфейс/класс,
  режим [`TableAlias`](xref:NextORM.Core.TableAlias) без сущностей и fluent-конфигурация.
* [Внедрение зависимостей](04-dependency-injection.md) - зарегистрируйте контекст вместо его ручного
  создания.

---

В быстром старте используются SQLite-подключение и API запросов. См.
[`UseSqlite`](xref:NextORM.Sqlite.SqliteDataContextOptionsBuilderExtensions.UseSqlite(NextORM.Core.DataContextBuilder,System.Data.Common.DbConnection))
и примеры в разделах [Фильтрация](../guide/03-filtering-where.md),
[Проекции](../guide/02-projections.md) и [Соединения](../guide/04-joins.md).
