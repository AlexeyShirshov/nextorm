# Изменение данных (DELETE)

> nextorm удаляет строки в той же модели явных команд, что и `INSERT`: [`DeleteFrom<TEntity>()`](xref:NextORM.Core.DataContextExtensions.DeleteFrom``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) строит параметризованный `DELETE FROM <table> [WHERE ...]`, расширение контекста [`Delete<TEntity>(entity)`](xref:NextORM.Core.DataContextExtensions.Delete``1(NextORM.Core.IDataContext,``0)) удаляет по объявленному ключу сущности, а соединённый запрос может завершаться multi-table удалением. Тут нет ни change tracking, ни `SaveChanges`: каждый терминал выполняет ровно одну команду.

**Что нужно знать:** [Изменение данных (INSERT)](19-insert-statement.md) · [Фильтрация (WHERE)](02-filtering-where.md) · [Соединения (JOIN)](03-joins.md) · [Обзор провайдеров](../providers/overview.md)

## Удаление строк по предикату

[`DeleteFrom<TEntity>()`](xref:NextORM.Core.DataContextExtensions.DeleteFrom``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) удаляет строки и возвращает число удалённых строк:

```csharp
var deleted = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Delete();

await ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .DeleteAsync(cancellationToken);
```

`Where` принимает те же выражения-предикаты, что и запрос (`From<T>().Where(...)`), а повторный вызов объединяет предикаты через `and`. `DeleteFrom<T>().ToSql()` рендерит инструкцию, не открывая соединение.

* Захваченные в предикате значения становятся параметрами (`x => x.Id == id` рендерит `id = @id`); встроенные литералы подставляются как есть, точно как в `WHERE` запроса.
* `Delete()`/`DeleteAsync()` возвращают число затронутых строк (`0`, если ничего не совпало). ClickHouse число не возвращает (мутация его не отдаёт).

## Удаление всех строк

Удаление всех строк требует явного маркера `All()`, поэтому нефильтрованное удаление всей таблицы нельзя записать случайно. Совместное использование `Where` и `All` бросает `InvalidOperationException`:

```csharp
ctx.DeleteFrom<ISimpleEntity>().All().Delete();   // delete from simple_entity
```

## Удаление по ключу

Форма по сущности удаляет ровно строку, определяемую объявленным ключом (`[Key]`/`.Key()`), рендеря равенство по ключу параметром:

```csharp
ctx.Delete(new SimpleEntity { Id = 1 });          // delete from simple_entity where id = @p0
await ctx.DeleteAsync(new SimpleEntity { Id = 1 });
```

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

`Returning()` / `Returning(projection)` материализуют удалённые строки через `RETURNING`/`OUTPUT` провайдера и читаются через `Single()`/`ToList()`:

```csharp
var removed = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Age > 10)
    .Returning(x => new { x.Id, x.Name })
    .ToList();

var one = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Returning()
    .Single();
```

| Провайдер | Форма |
|---|---|
| SQLite, PostgreSQL | `DELETE ... RETURNING <cols>` |
| SQL Server | `DELETE ... OUTPUT deleted.<col> ...` |
| MySQL, MariaDB, ClickHouse, in-memory | `NotSupportedException` |

`Returning()` возвращает всю сущность (конкретный `TEntity`); `Returning(x => ...)` — проекцию (одно поле, анонимный тип или member-init). `Single()` бросает исключение, если удалено ноль строк или больше одной.

## Очистка таблицы (`TRUNCATE`)

`Truncate<TEntity>()` рендерит родной `TRUNCATE TABLE`, сбрасывая таблицу быстрее, чем `DeleteFrom<T>().All()`:

```csharp
var cleared = ctx.Truncate<ISimpleEntity>().Execute();   // truncate table simple_entity
```

`TRUNCATE TABLE` есть у SQL Server, PostgreSQL, MySQL, MariaDB и ClickHouse; у SQLite его нет — бросается `NotSupportedException`, а in-memory-контекст только для запросов и тоже бросает `NotSupportedException`. `Execute()`/`ExecuteAsync()` возвращают число затронутых строк там, где провайдер его отдаёт (`0` иначе).

## Удаление по соединению

Соединённый запрос может завершаться `Delete()`/`DeleteAsync()`: первая таблица цепочки — цель удаления, а удаляются строки, совпавшие по условию соединения и `Where`. Поддерживаются только INNER-соединения.

```csharp
var removed = await ctx.From<ISimpleEntity>()
    .Join(ctx.From<IComplexEntity>(), (s, c) => s.Id == c.Id)
    .Where(p => p.Item2.String == "archived")
    .DeleteAsync(cancellationToken);
```

| Провайдер | Рендеримая форма |
|---|---|
| PostgreSQL | `DELETE FROM <t> AS a USING <u> AS b WHERE ...` |
| SQL Server | `DELETE a FROM <t> AS a JOIN <u> AS b ON ... WHERE ...` |
| MySQL, MariaDB | ``DELETE `a` FROM <t> AS `a` JOIN <u> AS `b` ON ... WHERE ...`` |
| SQLite, ClickHouse, in-memory | `NotSupportedException` |

* Цель — первая таблица (`Item1`); принимается только `Join` (INNER). `LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin` и `APPLY`-соединения бросают `NotSupportedException`, потому что меняют набор удаляемых строк.
* Терминалы — extension-методы на соединённом билдере для арностей 2–8; `Delete()`/`DeleteAsync()` возвращают число затронутых строк. `ToSql()` рендерит инструкцию без открытия соединения и бросает на in-memory контексте.
* `Returning` на multi-table delete недоступен; при необходимости прочитайте удалённые строки отдельным запросом.

## Примечания и что вне области

* In-memory-контекст только для запросов: `Delete`/`DeleteAsync`/`Truncate` (как и любая другая запись) бросают `NotSupportedException`; запрашивайте собственные коллекции. `INSERT` в in-memory-провайдере тоже вне области по замыслу.
* `DELETE` не готовится и не кладётся в кэш планов — оптимизация в nextorm нацелена только на read-only запросы (`Prepare`, неявный кэш планов, бенчмарки); мутация всегда рендерит и выполняет одну команду за вызов.
* Soft delete и глобальных фильтров намеренно нет; полный `MERGE` с произвольными ветками в эту поверхность не входит, а `UPDATE` живёт в своём гайде ([Изменение данных (UPDATE)](21-update-statement.md)).

## См. также

- [Изменение данных (INSERT)](19-insert-statement.md)
- [Соединения (JOIN)](03-joins.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)
