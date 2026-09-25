# Оптимистичная конкурентность и отслеживание изменений

> В nextorm намеренно нет отслеживания изменений и identity map, поэтому конкурентность задаётся явно:
> положите ожидаемый токен в `Where` и считайте `0` затронутых строк конфликтом. Ниже — этот паттерн,
> чтение нового токена обратно, upsert с проверкой токена и тонкий слой отслеживания изменений поверх,
> если хочется эргономики `SaveChanges`.

**Предварительно:** [Изменение данных (UPDATE)](21-update-statement.md) · [Изменение данных (MERGE)](23-merge-statement.md) · [Транзакции](25-transactions.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)

## Почему встроенного трекера изменений нет

Трекер изменений полезен, только когда сущностью **владеет** фреймворк: держит инстанс в identity map,
помнит исходные значения, замечает мутации и превращает их в `UPDATE` в `SaveChanges`. nextorm ничего
этого не делает — это построитель запросов с явной поверхностью DML
([Ограничения и что вне области](../advanced/limitations.md)):

* каждый терминал (`ToList`, `Update`, `Insert`, `Merge`, `Delete`, ...) выполняет ровно одну команду и
  возвращает её результат;
* загруженная строка — обычный POCO, которым владеет вызывающий код; контекст больше её не видит;
* нет ни identity map, ни снимка исходных значений, ни понятия «изменённых свойств».

Для конкурентности это означает: фреймворк не знает, какое значение токена было у строки при чтении,
поэтому не может подставить проверку сам. От него нужны лишь два примитива — `WHERE`, сравнивающий
токен, и число затронутых строк, — и оба уже есть. Поэтому оптимистичная конкурентность здесь —
**паттерн** поверх явной поверхности, описанный ниже, а не API `IfUnchanged`/`WithRefresh`, поставляемый
библиотекой.

## Колонка-токен

Для оптимистичной конкурентности нужно значение, меняющееся при каждой записи. Маппится как обычное
свойство; nextorm сравнивает его как любую другую колонку.

| Токен | Провайдер | Тип в C# | Замечания |
|---|---|---|---|
| версия `long` / `int` | все | `long Version { get; set; }` | управляется приложением; инкремент в том же утверждении |
| `rowversion` / `timestamp` | SQL Server | `byte[] Version { get; }` | управляется базой; помечается computed |
| `TIMESTAMP ... ON UPDATE CURRENT_TIMESTAMP` | MySQL / MariaDB | `DateTime Version { get; set; }` | управляется базой |
| версия `long` (или `xmin`) | PostgreSQL | `long Version { get; set; }` | родной колонки rowversion нет; `xmin` — системная колонка |

```csharp
[SqlTable("orders")]
public interface IOrder
{
    [Key] long Id { get; }
    string Status { get; set; }
    long Version { get; set; }   // токен конкурентности, управляемый приложением
}
```

Токен, управляемый базой, приложением не пишется, поэтому помечайте его computed
(`[DatabaseGenerated(DatabaseGeneratedOption.Computed)]` или `.Computed()`) — тогда `Set(entity)`
пропустит его автоматически.

## Проверка при UPDATE

Загрузите строку, запомните её токен, затем положите в `Where` и ключ, и исходный токен:

```csharp
public sealed class ConcurrencyException(string message) : Exception(message);

var originalVersion = order.Version;

var affected = ctx.Update<IOrder>()
    .Set(o => o.Status, "paid")
    .Set(o => o.Version, o => o.Version + 1)          // переносимо: инкремент в SQL
    .Where(o => o.Id == order.Id && o.Version == originalVersion)
    .Update();

if (affected == 0)
    throw new ConcurrencyException($"Order {order.Id} was modified by someone else.");
```

```sql
-- PostgreSQL
update orders set status = @p0, version = version + 1 where (id = @p1 and version = @p2)
```

* `Where` — тот же транслятор предикатов, что и в `WHERE` запроса, поэтому `Version == originalVersion`
  связывает токен параметром, а не инлайнит в SQL.
* `Set(o => o.Version, o => o.Version + 1)` инкрементирует атомарно в базе — гонки read-modify-write в
  коде приложения нет.
* Уберите предикат по версии — и обновление станет *last write wins*.
* Для токена, управляемого базой, вообще не вызывайте `Set` (он computed); сравнивайте его только в `Where`.

`0` затронутых строк намеренно неоднозначен: строка могла быть удалена либо её токен изменился. Если
нужно различить эти случаи, сделайте один дополнительный `FirstOrDefault` по ключу.

## Чтение нового токена обратно

После успешного обновления токен изменился. Когда провайдер умеет возвращать строки, запросите новое
значение в том же утверждении:

```csharp
var row = ctx.Update<IOrder>()
    .Set(o => o.Status, "paid")
    .Set(o => o.Version, o => o.Version + 1)
    .Where(o => o.Id == order.Id && o.Version == order.Version)
    .Returning(o => new { o.Id, o.Version })          // UPDATE ... RETURNING / OUTPUT
    .Single();

order.Version = row.Version;                          // write-back во владении вызывающего кода
```

`Returning` доступен на PostgreSQL (`RETURNING`), SQLite (`RETURNING`, 3.35+) и SQL Server (`OUTPUT`),
только в форме с предикатом. MySQL/MariaDB и ClickHouse его отклоняют — там перечитайте строку (внутри
той же транзакции, если она есть), чтобы получить новый токен. Никакого «refresh» на стороне фреймворка
нет: nextorm не держит вашу сущность, поэтому возвращённое значение присваиваете *вы*, ровно как после
любого другого чтения.

## Upsert с проверкой токена

Когда строка может существовать или нет, полный `MERGE` умеет атомарно вставить либо обновить и
отклонить устаревшее совпадение — на провайдерах, рендерящих условные ветки (SQL Server, PostgreSQL 15+):

```csharp
ctx.MergeInto<IOrder>()
    .Using(order)                                     // сущность, батч или серверный запрос
    .OnKeys()
    .WhenMatched((t, s) => t.Version == s.Version)
    .ThenUpdate(o => new { o.Status })
    .WhenNotMatched().ThenInsert()
    .Merge();
```

* Ветка `WHEN MATCHED AND <токен>`, которая **не** сработала, оставляет существующую строку как есть —
  это *пропустить устаревшую*, а не *упасть на устаревшей*. Если нужна реакция, читайте число
  затронутых строк или проекцию `Returning`; для жёсткого «бросить при конфликте» используйте паттерн
  с `UPDATE` выше.
* Условная ветка (`WHEN MATCHED AND ...`) поддерживается только на SQL Server и PostgreSQL 15+;
  SQLite/MySQL/MariaDB поддерживают лишь key upsert и отклоняют общий `MERGE`. См.
  [MERGE](23-merge-statement.md).

## Свой трекер изменений

Хука материализации строк нет, поэтому пользовательский трекер привязывает строки по мере загрузки и
делает diff в момент `SaveChanges`. Для одного типа сущности это немного:

```csharp
public sealed class OrderTracker
{
    private readonly IDataContext _ctx;
    private readonly Dictionary<long, (IOrder Entity, long Version, string Status)> _original = new();

    public OrderTracker(IDataContext ctx) => _ctx = ctx;

    public List<IOrder> Load()
    {
        var rows = _ctx.From<IOrder>().ToList();
        foreach (var row in rows)
            _original[row.Id] = (row, row.Version, row.Status);   // снимок
        return rows;
    }

    public int SaveChanges()
    {
        var affected = 0;
        foreach (var (id, (entity, version, status)) in _original)
        {
            if (entity.Status == status)                          // обнаружение изменения
                continue;

            var n = _ctx.Update<IOrder>()
                .Set(o => o.Status, entity.Status)
                .Set(o => o.Version, o => o.Version + 1)
                .Where(o => o.Id == id && o.Version == version)
                .Update();

            if (n == 0)
                throw new ConcurrencyException($"Order {id} was modified.");
            affected += n;
        }
        return affected;
    }
}
```

Оберните `SaveChanges` в транзакцию, чтобы конфликт откатывал всю единицу работы целиком:

```csharp
await using var tx = await ((ITransactionManager)ctx).BeginTransactionAsync();
try
{
    tracker.SaveChanges();
    await tx.CommitAsync();
}
catch
{
    await tx.RollbackAsync();
    throw;
}
```

Чтобы обобщить, сравнивайте не одно поле, а все отображённые свойства — `DataContextCache.Metadata[typeof(T)].Properties`
отдаёт маппинг ([`IPropertyMetadata`](xref:NextORM.Core.IPropertyMetadata)), — либо сгенерируйте
типизированный трекер генератором исходников. nextorm намеренно останавливается на примитивах: identity
map, ленивая загрузка, упорядочивание в `SaveChanges` и модель «изменённых свойств» — вне области
([Ограничения и что вне области](../advanced/limitations.md)).

## Поддержка провайдерами

| Провайдер | Проверка токена (`WHERE` + число строк) | `RETURNING` / `OUTPUT` у `UPDATE` | Условная ветка `MERGE` |
|---|---|---|---|
| PostgreSQL | да | `RETURNING` | да (15+) |
| SQL Server | да | `OUTPUT` | да |
| SQLite | да | `RETURNING` (3.35+) | — (только key upsert) |
| MySQL / MariaDB | да | — | — (только key upsert) |
| ClickHouse | предикат обязателен; `UPDATE` **не** возвращает число затронутых строк | — | — |
| In-memory | — (только запросы) | — | — |

ClickHouse — заметное исключение: `ALTER TABLE ... UPDATE` не возвращает число затронутых строк, поэтому
тест `0 == конфликт` там неприменим. Чтобы обнаружить конкуренцию, используйте токен в `SET`-выражении,
совпадающий только с ожидаемым значением, либо перечитывайте строку.

## См. также

- [Изменение данных (UPDATE)](21-update-statement.md)
- [Изменение данных (MERGE)](23-merge-statement.md)
- [Транзакции](25-transactions.md)
- [Изменение данных (INSERT)](19-insert-statement.md)
- [Изменение данных (DELETE)](20-delete-statement.md)
- [Ограничения и что вне области](../advanced/limitations.md)
