# Изменение данных (UPDATE)

> nextorm изменяет строки по той же модели явных команд, что `INSERT` и `DELETE`:
> [`Update<TEntity>()`](xref:NextORM.Core.DataContextExtensions.Update``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
> строит параметризованный `UPDATE <table> SET ... [WHERE ...]`, а расширение контекста
> [`Update<TEntity>(entity)`](xref:NextORM.Core.DataContextExtensions.Update``1(NextORM.Core.IDataContext,``0))
> обновляет по объявленному ключу сущности. Отслеживания изменений и `SaveChanges` нет: каждый терминал
> выполняет ровно одну команду.

**Что нужно знать:** [Изменение данных (INSERT)](19-insert-statement.md) · [Изменение данных (DELETE)](20-delete-statement.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Обзор провайдеров](../providers/overview.md)

## Обновление строк по предикату

[`Update<TEntity>()`](xref:NextORM.Core.DataContextExtensions.Update``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}}))
возвращает [`UpdateBuilder<TEntity>`](xref:NextORM.Core.UpdateBuilder`1). Добавьте по одному `Set` на
записываемую колонку, затем `Where`, и завершите `Update()`/`UpdateAsync()`, чтобы получить число
затронутых строк:

```csharp
var updated = ctx.Update<ISimpleEntity>()
    .Set(x => x.Name, "renamed")
    .Set(x => x.Age, 42)
    .Where(x => x.Id == 1)
    .Update();

await ctx.Update<ISimpleEntity>()
    .Set(x => x.Name, "renamed")
    .Where(x => x.Age > 10)
    .UpdateAsync(cancellationToken);
```

* `Set(column, value)` всегда привязывает значение как параметр, никогда не подставляет его в SQL.
* `Where` принимает те же выражения-предикаты, что и запрос (`From<T>().Where(...)`); повторный вызов
  объединяет предикаты через `and`. Захваченные переменные становятся параметрами, литералы
  подставляются дословно — как в `WHERE` запроса.
* `Update()`/`UpdateAsync()` возвращают число затронутых строк (`0`, если ничего не совпало). Если
  опустить `Where`, обновляются **все** строки таблицы.
* `ToSql()` рендерит инструкцию, не открывая соединение.

## Присваивания

Правая часть `Set` — это значение, ссылка на колонку или выражение:

```csharp
// значение (параметр)
.Set(x => x.Name, "renamed")

// другая колонка той же строки
.Set(x => x.UpdatedAt, x => x.CreatedAt)

// произвольное выражение: другие колонки и захваченные значения
.Set(x => x.Counter, x => x.Counter + 1)
.Set(x => x.Total, x => x.Price * quantity)
```

Выражение рендерится тем же транслятором, что выражение `SELECT`/`WHERE`, поэтому может использовать
только mapped-колонки, захваченные значения и поддерживаемые скалярные функции. `Set(entity)`
записывает сразу все mapped-колонки сущности, кроме ключа, identity и computed:

```csharp
ctx.Update<ISimpleEntity>()
    .Set(entity)
    .Where(x => x.Id == entity.Id)
    .Update();
```

Присваивание computed-колонки бросает `NotSupportedException`. Повторный `Set` для той же колонки
заменяет предыдущее присваивание.

## Обновление всех строк

В отличие от `DELETE`, который требует явного маркера `All()`, `UPDATE` без `Where` обновляет всю
таблицу:

```csharp
ctx.Update<ISimpleEntity>().Set(x => x.Archived, true).Update();   // update simple_entity set archived = @p0
```

## Обновление по ключу

Форма по ключу обновляет ровно строку, определяемую объявленным ключом сущности (`[Key]`/`.Key()`),
записывая все колонки, кроме ключа, identity и computed, и рендеря равенство по ключу параметром:

```csharp
ctx.Update(new SimpleEntity { Id = 1, Name = "renamed" });   // update simple_entity set name = @p0 where id = @p1
await ctx.UpdateAsync(new SimpleEntity { Id = 1, Name = "renamed" });
```

Тип сущности должен объявлять ключ; иначе вызов бросает `InvalidOperationException`.

## Возврат обновлённых строк

`Returning()` / `Returning(projection)` материализуют обновлённые строки через `RETURNING`/`OUTPUT`
провайдера, читаются через `Single()`/`ToList()`:

```csharp
var updated = ctx.Update<ISimpleEntity>()
    .Set(x => x.Archived, true)
    .Where(x => x.Age > 10)
    .Returning(x => new { x.Id, x.Name })
    .ToList();

var one = ctx.Update<ISimpleEntity>()
    .Set(x => x.Archived, true)
    .Where(x => x.Id == 1)
    .Returning()
    .Single();
```

`Returning()` возвращает всю mapped-сущность (конкретный `TEntity`); `Returning(x => ...)` — спроецированную
форму (один член, анонимный тип или member-init). `Single()` бросает, если обновление не затронуло ни
одной строки или затронуло больше одной. `Returning` доступен только в форме по предикату: форма по
ключу (`Update(entity)`) его не предоставляет.

## Обновление из join

[`UpdateJoin()`](xref:NextORM.Core.DataContextExtensions.UpdateJoin``2(NextORM.Core.JoinedEntityBuilder{``0,``1}))
на join-запросе строит multi-table `UPDATE`, где target — **первая** таблица join, а значения `SET` могут
читать любую присоединённую таблицу:

```csharp
var updated = ctx.From<IOrder>()
    .Join(ctx.From<ICustomer>(), (o, c) => o.CustomerId == c.Id)
    .UpdateJoin()
    .Set(p => p.Item1.Status, "priority")
    .Set(p => p.Item1.Total, p => p.Item1.Total + p.Item2.Credit)
    .Where(p => p.Item2.Tier == "gold")
    .Update();
```

[`Set`](xref:NextORM.Core.UpdateJoinBuilder`1.Set``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},``0))
выбирает колонку первой (`Item1`) таблицы; значение — константа или выражение, ссылающееся на любую
присоединённую таблицу (`p.Item2...`). `Where` фильтрует по всей проекции, а терминал —
`Update()`/`UpdateAsync()` (число затронутых строк) или `ToSql()`.

Поддерживаются только INNER `Join`: условия join складываются в фильтр (или остаются `ON` join), поэтому
внешний join молча менял бы набор обновляемых строк — `LeftJoin`/`RightJoin`/`FullJoin`/`CrossJoin`
бросают `NotSupportedException`. PostgreSQL и SQLite рендерят `UPDATE ... FROM`, SQL Server —
`UPDATE <alias> ... FROM ... JOIN`, MySQL/MariaDB — `UPDATE ... JOIN ... SET`; ClickHouse и in-memory
бросают исключение.

## Поддержка провайдерами

| Провайдер | `UPDATE ... SET ... WHERE` | `RETURNING` / `OUTPUT` | `UPDATE ... FROM` |
|---|---|---|---|
| PostgreSQL | да | `UPDATE ... RETURNING <cols>` | `UPDATE <target> AS t1 SET ... FROM ... WHERE ...` |
| SQL Server | да | `UPDATE ... OUTPUT inserted.<col> ...` (между `SET` и `WHERE`) | `UPDATE <alias> SET ... FROM <target> JOIN ...` |
| SQLite | да | `UPDATE ... RETURNING <cols>` | `UPDATE <target> AS t1 SET ... FROM ... WHERE ...` (3.33+) |
| MySQL / MariaDB | да | `NotSupportedException` | `UPDATE <target> JOIN ... SET <alias>.<col> = ...` |
| ClickHouse | `ALTER TABLE ... UPDATE ... SETTINGS mutations_sync = 1` (число затронутых строк не возвращает) | `NotSupportedException` | `NotSupportedException` |
| In-memory | `NotSupportedException` (только запросы) | `NotSupportedException` | `NotSupportedException` |

## Примечания и вне области

* `UPDATE` пишет только колонки, названные в `Set` — отслеживания изменений нет, поэтому «изменённые
  свойства» вывести нельзя.
* ClickHouse обновляет через `ALTER TABLE ... UPDATE`; мутация применяется синхронно
  (`SETTINGS mutations_sync = 1`), но число затронутых строк не возвращает, поэтому
  `Update()`/`UpdateAsync()` возвращают `0`. Движок требует предикат, поэтому `UPDATE` без `Where`
  рендерит `WHERE 1`.
* Оптимистичная конкурентность (`rowversion`) и глобальные фильтры запросов в эту поверхность **не**
  входят.
* Контекст in-memory — только запросы: `Update`/`UpdateAsync` (как и любая другая запись) бросают
  `NotSupportedException`; запрашивайте собственные коллекции.
* `UPDATE` не подготавливается и не кэшируется планом — оптимизация nextorm нацелена только на
  read-only запросы (`Prepare`, неявный кэш планов, бенчмарки); мутация всегда рендерит и исполняет
  одну команду за вызов.

## См. также

- [Изменение данных (INSERT)](19-insert-statement.md)
- [Изменение данных (DELETE)](20-delete-statement.md)
- [Слияние данных (MERGE / upsert)](23-merge-statement.md)
- [Фильтрация (WHERE)](02-filtering-where.md)
- [Ограничения и вне области](../advanced/limitations.md)
- [Обзор провайдеров](../providers/overview.md)
- [Справочник API](../advanced/api-reference.md)
