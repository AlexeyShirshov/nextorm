# Изменение данных (DELETE)

> Используйте [`CreateDeleteBuilder<TEntity>()`](xref:NextORM.Core.DataContextExtensions.CreateDeleteBuilder``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})), чтобы удалить строки по условию или явно удалить все строки. Он формирует параметризованный `DELETE FROM <table> [WHERE ...]`. Чтобы удалить сущность по объявленному ключу, передайте её в [`Delete<TEntity>(entity)`](xref:NextORM.Core.DataContextExtensions.Delete``1(NextORM.Core.IDataContext,``0)). Также можно удалить строки, выбранные соединением.

**Что нужно знать:** [Изменение данных (INSERT)](17-insert-statement.md) · [Фильтрация (WHERE)](03-filtering-where.md) · [Соединения (JOIN)](04-joins.md) · [Обзор провайдеров](../connecting-providers/overview.md)

## Выберите способ удаления

- Чтобы удалить строки по условию, используйте [Удаление строк по предикату](#удаление-строк-по-предикату).
- Чтобы задать максимум удаляемых строк, см. [Удалите ограниченное число строк](#удалите-ограниченное-число-строк).
- Чтобы удалить все строки, явно укажите маркер [`All()`](#удаление-всех-строк).
- Чтобы удалить сущность по ключу, см. [Удаление по ключу](#удаление-по-ключу).
- Чтобы получить удалённые строки, см. [Возврат удалённых строк](#возврат-удалённых-строк).
- Чтобы удалить строки по соединению или очистить таблицу, см. [Удаление по соединению](#удаление-по-соединению) или [Очистка таблицы (`TRUNCATE`)](#очистка-таблицы-truncate).

Каждый терминал выполняет одну команду. nextorm не отслеживает изменения и не предоставляет `SaveChanges`.

## Удаление строк по предикату

[`CreateDeleteBuilder<TEntity>()`](xref:NextORM.Core.DataContextExtensions.CreateDeleteBuilder``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) удаляет строки и возвращает число удалённых строк:

```csharp
var deleted = ctx.CreateDeleteBuilder<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Delete();

await ctx.CreateDeleteBuilder<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .DeleteAsync(cancellationToken);
```

```sql
-- PostgreSQL
delete from simple_entity where id = 1
delete from simple_entity where (age > 10)
```

`Where` принимает те же выражения-предикаты, что и запрос (`From<T>().Where(...)`). Повторные вызовы
объединяют условия через `and`.

Чтобы проверить сформированный SQL без открытия соединения, вызовите
`CreateDeleteBuilder<T>().ToSql()`.

* Захваченные в предикате значения становятся параметрами (`x => x.Id == id` рендерит `id = @id`); встроенные литералы подставляются как есть, точно как в `WHERE` запроса.
* `Delete()`/`DeleteAsync()` возвращают число затронутых строк (`0`, если ничего не совпало). ClickHouse число не возвращает (мутация его не отдаёт).

## Удалите ограниченное число строк

В примерах `app.Orders` — репозиторий из контекста приложения. Как его настроить, см.
в руководстве [«Репозитории и контекст приложения»](37-repositories.md).

Начните с одной отображённой таблицы и задайте явный `Where`. Терминал сохраняет
предикат и применимые глобальные фильтры, в том числе настройки `IgnoreFilters`.
Глобальный фильтр или лимит не заменяет явный `Where`.

Чтобы удалить до 100 самых старых архивных заказов, добавьте уникальный
идентификатор заказа для разрешения совпадений даты:

```csharp
var deleted = await app.Orders.Where(o => o.IsArchived)
    .OrderBy(o => o.ArchivedAt)
    .ThenBy(o => o.Id)
    .Limit(100)
    .DeleteAsync(cancellationToken);
```

Для синхронного вызова используйте `Delete()`. Такой же лимит можно задать на
явном построителе. Следующий пример удаляет произвольные 100 подходящих строк
или меньше:

```csharp
var deleted = app.Orders.MakeDelete()
    .Where(o => o.IsArchived)
    .Limit(100)
    .Delete();
```

Объявите уникальный ключ из столбцов без `NULL` в соответствии со схемой базы.
Ключ может быть составным. Ограниченный DELETE выбирает ключи и удаляет строки
одним серверным оператором, не загружая сущности или ключи на клиент. Уровень
изоляции базы не меняется. Для удаления по предикату без лимита ключ не нужен.

Задавайте положительный лимит. Для DELETE явный `Limit(0)` недопустим, хотя
для SELECT он означает отсутствие лимита. Ограниченный DELETE поддерживается
на PostgreSQL, SQL Server, MySQL, MariaDB и SQLite (без необязательного
расширения DELETE LIMIT). ClickHouse отклоняет ограниченный DELETE, а провайдер
InMemory не поддерживает изменения. Для удаления по соединению используйте
отдельный API без лимита. Короткий DELETE также отклоняет производные источники,
проекции и опции вроде положительного смещения и `WithTies`.

## Удаление всех строк

Чтобы удалить все строки, явно добавьте маркер `All()`. Так полное удаление таблицы становится
осознанным действием. Совместное использование `Where` и `All` приводит к `InvalidOperationException`:

```csharp
ctx.CreateDeleteBuilder<ISimpleEntity>().All().Delete();   // delete from simple_entity
```

## Удаление по ключу

Чтобы удалить сущность по ключу, передайте её в расширение контекста `Delete`. Операция использует
объявленный ключ сущности (`[Key]`/`.Key()`) и передаёт равенство по ключу параметром:

```csharp
ctx.Delete(new SimpleEntity { Id = 1 });          // delete from simple_entity where id = @p0
await ctx.DeleteAsync(new SimpleEntity { Id = 1 });
```

```sql
-- PostgreSQL
delete from simple_entity where id = @p0
```

## Шардированные сущности

Шардированный delete обязан назвать бакет: область берётся из равенства по shard key в предикате либо
из экземпляра в ключевой форме. Предикат без такого равенства — как и полный `All()` — падает до SQL,
вместо того чтобы назвать таблицу по умолчанию:

```csharp
ctx.CreateDeleteBuilder<Order>().Where(x => x.Tenant == tenant).Delete();   // по предикату
ctx.Delete(new Order { Id = 1 });                                          // по экземпляру
ctx.CreateDeleteBuilder<Order>().All().ToSql();                            // падает: бакет не назван
```

Таблица бакета, её шард и правила размещения описаны в разделе
[«Шардирование и горизонтальное партиционирование»](35-sharding-and-horizontal-partitioning.md).

## Глобальные фильтры запросов

[Глобальный фильтр запроса](../advanced/query-filters.md), объявленный для удаляемой сущности,
объединяется с условием `WHERE`:

* предикатная форма — `delete from ... where <predicate> and <filter>`;
* форма по ключу — `delete from ... where <pk> = @p and <filter>`, поэтому `ctx.Delete(entity)` не удалит ничего, если фильтр исключает этот ключ.

`All()` явно удаляет все строки, и фильтры к нему **не** применяются. Соединённое удаление фильтрует
цель (первую таблицу) и все источники соединений.

[`DeleteBuilder<TEntity>`](xref:NextORM.Core.DeleteBuilder`1) предоставляет четыре перегрузки
`IgnoreFilters`: для всех фильтров, типа сущности, ключа фильтра или пересечения ключа и типа. Вызов
меняет состояние билдера, поэтому повторные вызовы накапливаются:

```csharp
ctx.CreateDeleteBuilder<Document>()
    .IgnoreFilters(["soft-delete"])   // оставляем ограничение по тенанту, убираем soft-delete
    .Where(d => d.IsDeleted)
    .Delete();
```

Полная матрица — в разделе [UPDATE и DELETE (DML)](../advanced/query-filters.md#update-и-delete-dml).

## Поддержка провайдерами (сводка)

| Провайдер | `DELETE` | `RETURNING` / `OUTPUT` | `TRUNCATE` | `DELETE ... USING` / join |
|---|---|---|---|---|
| PostgreSQL | да | `RETURNING` | да | `USING` |
| SQL Server | да | `OUTPUT deleted.<col>` | да | `DELETE <alias> FROM ... JOIN` |
| SQLite | да | `RETURNING` | `NotSupportedException` | `NotSupportedException` |
| MySQL / MariaDB | да | `NotSupportedException` | да | `DELETE <alias> FROM ... JOIN` |
| ClickHouse | `ALTER TABLE ... DELETE` | `NotSupportedException` | да | `NotSupportedException` |
| In-memory | `NotSupportedException` (только запросы) | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` |

## Возврат удалённых строк

Чтобы прочитать строки, удалённые командой, используйте `Returning()` или `Returning(projection)`.
Провайдер формирует нативный вариант `RETURNING`/`OUTPUT`; результат читается через `Single()` или
`ToList()`:

```csharp
var removed = ctx.CreateDeleteBuilder<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .Returning(x => new { x.Id, x.Name })
    .ToList();

var one = ctx.CreateDeleteBuilder<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Returning()
    .Single();
```

```sql
-- PostgreSQL
delete from simple_entity where (age > 10) returning id, name
delete from simple_entity where id = 1 returning id, name, age
```

| Провайдер | Форма |
|---|---|
| SQLite, PostgreSQL | `DELETE ... RETURNING <cols>` |
| SQL Server | `DELETE ... OUTPUT deleted.<col> ...` |
| MySQL, MariaDB, ClickHouse, in-memory | `NotSupportedException` |

`Returning()` возвращает всю сущность (конкретный `TEntity`); `Returning(x => ...)` — проекцию (одно поле, анонимный тип или member-init). `Single()` бросает исключение, если удалено ноль строк или больше одной.

Чтобы захватить удалённые строки для следующей инструкции того же батча вместо возврата клиенту, зарегистрируйте захват в рамках батча — колонки `deleted.*` **и есть** захваченные строки — и постройте поверх него запрос через `FromOutput<TResult>`/`FromOutputTable`: см. [Захват в рамках батча](17-insert-statement.md#захват-в-рамках-батча-batchbuilderoutputintotablevariable). Только SQL Server.

## Очистка таблицы (`TRUNCATE`)

Чтобы очистить таблицу, используйте `CreateTruncateBuilder<TEntity>()`. Он формирует нативную команду
`TRUNCATE TABLE`, которая сбрасывает таблицу быстрее, чем `CreateDeleteBuilder<T>().All()`:

```csharp
var cleared = ctx.CreateTruncateBuilder<ISimpleEntity>().Execute();   // truncate table simple_entity
```

`TRUNCATE TABLE` есть у SQL Server, PostgreSQL, MySQL, MariaDB и ClickHouse; у SQLite его нет — бросается `NotSupportedException`, а in-memory-контекст только для запросов и тоже бросает `NotSupportedException`. `Execute()`/`ExecuteAsync()` возвращают число затронутых строк там, где провайдер его отдаёт (`0` иначе).

## Удаление по соединению

Чтобы удалить подходящие строки, завершите соединённый запрос вызовом `Delete()` или `DeleteAsync()`.
Первая таблица в цепочке — цель. Удаляются строки, совпавшие с условием соединения и предикатами
`Where`. Поддерживаются только INNER-соединения.

```csharp
var removed = await ctx.From<ISimpleEntity>()
    .Join<IComplexEntity>((s, c) => s.Id == c.Id)
    .Where(p => p.Item2.String == "archived")
    .DeleteAsync(cancellationToken);
```

```sql
-- PostgreSQL
delete from simple_entity as "t1" using complex_entity as "t2" where cast(t1.id as bigint) = t2.id and t2.somestring = 'archived'
```

| Провайдер | Рендеримая форма |
|---|---|
| PostgreSQL | `DELETE FROM <t> AS a USING <u> AS b WHERE ...` |
| SQL Server | `DELETE a FROM <t> AS a JOIN <u> AS b ON ... WHERE ...` |
| MySQL, MariaDB | ``DELETE `a` FROM <t> AS `a` JOIN <u> AS `b` ON ... WHERE ...`` |
| SQLite, ClickHouse, in-memory | `NotSupportedException` |

* Цель — первая таблица (`Item1`); принимается только `Join` (INNER). `LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin` и `APPLY`-соединения бросают `NotSupportedException`, потому что меняют набор удаляемых строк.
* Терминалы — методы расширения соединённого билдера для арностей 2–8; `Delete()`/`DeleteAsync()` возвращают число затронутых строк. `ToSql()` формирует SQL без открытия соединения и бросает исключение в контексте in-memory.
* `CreateDeleteJoinBuilder()` запускает многотабличное удаление с возвратом строк. Терминалы
  `Returning()` и `Returning(projection)` доступны только в PostgreSQL (`DELETE ... USING ... RETURNING`),
  для INNER-соединений и арностей 2–8.
  * `Returning()` эквивалентен `Returning(p => p)` и возвращает всю соединённую проекцию
    (`Projection<T1, ...>`): все доступные для возврата отображённые свойства каждого слота в исходном
    порядке. `Item1` — цель удаления. Если CLR-тип повторяется, слоты остаются раздельными: при
    self-join значения `Item1` и `Item2` не смешиваются.
  * `Returning(p => new { p.Item1.Id, p.Item2.Name })` возвращает заданную проекцию; результат читается
    через `Single()` или `ToList()`. Возвращаемые колонки получают детерминированные псевдонимы для
    каждого слота, а явные проекции остаются без изменений. `Returning()` не добавляет список
    `RETURNING` неявно.
  * Форма с возвратом строк работает как обычный терминал и как тело модифицирующего CTE через
    `With(имя, delete)`. См. [Общие табличные выражения](10-cte.md).
  * nextorm отклоняет возвращаемый элемент, если его отображённое свойство — многоколоночный
    `Range<T>`. Также отклоняется обязательный член без соответствия в форме соединённого источника;
    сообщение указывает слот и член.
* Присоединяемая сторона может быть CTE, его объявление поднимается перед `DELETE` (PostgreSQL `with c as (…) delete from <t> as "t1" using c as "t2" where …`); целью остаётся первая физическая таблица, а CTE может стоять на любой позиции join. См. [Common table expressions](10-cte.md).

```csharp
var all = await ctx.From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .Where(p => p.Item2.Tier == "gold")
    .CreateDeleteJoinBuilder()
    .Returning()          // эквивалент .Returning(p => p)
    .ToListAsync(cancellationToken);
```

### DELETE как тело модифицирующего CTE (PostgreSQL)

На PostgreSQL возвращающий строки соединённый `DELETE` может быть телом модифицирующего CTE: передайте
его в `With(имя, delete)` и читайте удалённые строки типизированно через `From(имя)`:

```csharp
var doomed = dataContext
    .From<IOrder>()
    .Join<ICustomer>((o, c) => o.CustomerId == c.Id)
    .CreateDeleteJoinBuilder()
    .Returning(p => new { OrderId = p.Item1.Id, CustomerName = p.Item2.Name });

var removed = dataContext
    .With("del", doomed)
    .From("del")
    .Select(r => new { r.OrderId, r.CustomerName })
    .ToList();
```

```sql
-- PostgreSQL
with del as (delete from orders as "t1" using customers as "t2" where t1.customer_id = t2.id returning t1.id as "OrderId", t2.name as "CustomerName") select "OrderId", "CustomerName"
 from del as "t1"
```

Проекция `RETURNING` обязательна (мутация только с побочным эффектом вне области охвата), и такая
инструкция никогда не кэшируется. См. [Common table expressions](10-cte.md).

### Удаление по CTE

```csharp
var e = ctx.From<IComplexEntity>();
var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

var removed = e
    .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
    .Delete();
```

```sql
-- PostgreSQL
with c as (select id
 from complex_entity where (id > 0)) delete from complex_entity as "t1" using c as "t2" where t1.id = cast(t2.id as bigint)
```

`with …` эмитится перед мутацией на каждом провайдере с multi-table delete, а CTE может стоять на любой позиции join. Два разных CTE с одинаковым именем по сторонам join отклоняются `InvalidOperationException`. См. [Common table expressions](10-cte.md).

## Примечания и что вне области

* In-memory-контекст только для запросов: `Delete`/`DeleteAsync`/`CreateTruncateBuilder` (как и любая другая запись) бросают `NotSupportedException`; запрашивайте собственные коллекции. `INSERT` в in-memory-провайдере тоже вне области по замыслу.
* `DELETE` не готовится и не кладётся в кэш планов — оптимизация в nextorm нацелена только на read-only запросы (`Prepare`, неявный кэш планов, бенчмарки); мутация всегда рендерит и выполняет одну команду за вызов.
* Встроенного поведения soft delete намеренно нет; soft delete выражается [глобальным фильтром запроса](../advanced/query-filters.md) плюс явным `UPDATE`. Полный `MERGE` с произвольными ветками в эту поверхность не входит, а `UPDATE` живёт в своём гайде ([Изменение данных (UPDATE)](19-update-statement.md)).

## См. также

- [Изменение данных (INSERT)](17-insert-statement.md)
- [Слияние данных (MERGE / upsert)](21-merge-statement.md)
- [Соединения (JOIN)](04-joins.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../connecting-providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)
