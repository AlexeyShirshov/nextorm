# Слияние данных (`MERGE` / upsert)

> nextorm строит `MERGE` (upsert) через [`MergeInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.MergeInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})): одна точка входа покрывает и переносимый **key upsert** (`INSERT ... ON CONFLICT` / `ON DUPLICATE KEY` / `MERGE`), и общий много-веточный **полный `MERGE`** с ветками `WHEN MATCHED`/`WHEN NOT MATCHED`. Изменения не отслеживаются, `SaveChanges` нет: каждый терминал выполняет ровно одну команду.

**Предварительно:** [Изменение данных (INSERT)](19-insert-statement.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Обзор провайдеров](../providers/overview.md)

## Обзор

`MergeInto<TEntity>()` записывает набор строк-источника в целевую таблицу и позволяет базе построчно решить, что делать. Источник — отображённая сущность, батч или серверный запрос; совпадение — либо объявленный ключ (`OnKeys()`), либо произвольное условие (`On(...)`); действия задаются ветками. `Merge()`/`MergeAsync()` выполняют команду и возвращают число затронутых строк, а `ToSql()` рендерит утверждение без соединения.

Один билдер обслуживает две формы:

* **key upsert** — `OnKeys()` + `WhenMatchedUpdate()` + `WhenNotMatchedInsert()`. Каждый SQL-провайдер выражает её нативно, и только она применяется in-memory-провайдером (к зарегистрированной последовательности).
* **полный `MERGE`** — `WhenMatched()`/`WhenNotMatched()`/`WhenNotMatchedBySource()`, каждая завершается `ThenUpdate`/`ThenInsert`/`ThenDelete`/`ThenDoNothing`. Нативно на SQL Server и PostgreSQL 15+; остальные провайдеры его отклоняют.

## Upsert (key merge)

[`MergeInto<TEntity>()`](xref:NextORM.Core.DataContextExtensions.MergeInto``1(NextORM.Core.IDataContext,System.Action{NextORM.Core.EntityMetadataBuilder{``0}})) записывает набор строк-источника в таблицу и позволяет базе по объявленному ключу решить: обновить существующую строку или вставить новую. Источник — одна сущность или батч, ключ совпадения разрешается из маппинга сущности через `OnKeys()`, и обе ветки — `WhenMatchedUpdate()` (присвоить все не-key записываемые колонки из источника) и `WhenNotMatchedInsert()` — обязательны:

```csharp
ctx.MergeInto<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })   // или Using(new[] { e1, e2 })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .Merge();
```

Билдер рендерит родную форму провайдера; `ToSql()` показывает её без соединения:

| Провайдер | Рендер |
|---|---|
| PostgreSQL, SQLite | `INSERT ... ON CONFLICT (<keys>) DO UPDATE SET <col> = excluded.<col>` |
| MySQL, MariaDB | `INSERT ... ON DUPLICATE KEY UPDATE <col> = VALUES(<col>)` |
| SQL Server | `MERGE ... USING (VALUES ...) AS source (...) ON ... WHEN MATCHED THEN UPDATE SET ... WHEN NOT MATCHED THEN INSERT ...;` |
| In-memory | применяется к зарегистрированной последовательности в контексте (без SQL) |
| ClickHouse | `NotSupportedException` (движкового DML-upsert нет) |

`Merge()`/`MergeAsync()` возвращают число затронутых строк. Ключ должен быть объявлен (`[Key]`/`.Key()`) и не быть генерируемым базой; сущность только из key-колонок отклоняется, так как обновлять нечего. Это форма *key upsert*; произвольные ветки (включая `DELETE`) — см. [Полный `MERGE`](#полный-merge).

## Полный `MERGE`

[`WhenMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenMatched) / [`WhenNotMatched()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatched) / [`WhenNotMatchedBySource()`](xref:NextORM.Core.MergeBuilder`1.WhenNotMatchedBySource) расширяют тот же билдер до общего много-веточного `MERGE` на провайдерах, которые рендерят его нативно (SQL Server, PostgreSQL 15+). Каждая ветка завершается действием, и ветки выполняются в порядке объявления:

```csharp
ctx.MergeInto<IDest>()
    .Using(source)                            // сущность, батч или запрос: Using(ctx.From<IDest>().Where(...))
    .OnKeys()                                 // или .On((t, s) => t.Id == s.Id && s.Age > 0)
    .WhenMatched().ThenUpdate()               // или .WhenMatched((t, s) => t.Name != s.Name).ThenUpdate()
    .WhenNotMatched().ThenInsert()            // или .WhenNotMatched((t, s) => s.Age > 0).ThenInsert()
    .WhenNotMatchedBySource().ThenDelete()    // только SQL Server
    .Merge();
```

`ThenUpdate()`/`ThenInsert()` без селектора пишут все подходящие колонки; явный селектор (`x => new { x.Name }`) ограничивает набор, а значения берутся из одноимённых колонок строки-источника. Селектор не может назвать генерируемую базой колонку, а matched-ветка не может переписать ключ совпадения. `ThenDelete()` на matched-ветке и `WhenNotMatchedBySource().ThenDelete()` позволяют merge удалять строки — отличие от key upsert. `ThenDoNothing()` оставляет строку-кандидат без изменений; доступно только на PostgreSQL, так как в SQL Server действия `DO NOTHING` нет.

`On(condition)` заменяет матч по ключу произвольным условием совпадения (`ON <condition>`). Билдер ветки тоже принимает условие (`WhenMatched(condition)`, `WhenNotMatched(condition)`, `WhenNotMatchedBySource(condition)`), которое рендерится как `WHEN ... AND <condition>`; ветка срабатывает, только если условие истинно. Условия используют форму с двумя параметрами `(target, source)` — существующая строка это первый параметр лямбды, входящая строка-источник — второй, например `On((t, s) => t.Id == s.Id && s.Age > 0)`. В SQL Server условие `WHEN NOT MATCHED BY SOURCE` может ссылаться только на строку target.

| Провайдер | Полный `MERGE` | `THEN DELETE` | `THEN DO NOTHING` | `ON` / `WHEN ... AND` | `WHEN NOT MATCHED BY SOURCE` |
|---|---|---|---|---|---|
| SQL Server | да | да | — | да | да |
| PostgreSQL | да (15+) | да | да | да | — |
| SQLite, MySQL, MariaDB | — (только key upsert) | — | — | — | — |
| In-memory | — (только key upsert) | — | — | — | — |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` |

`Using(ctx.From<T>().Where(...))` или `Using(QueryCommand<T>)` задаёт строки серверным запросом (`USING (<select>) AS source`) вместо батча `VALUES`; источник-запрос принимает только форма полного `MERGE`.

### Возврат слитых строк

[`Returning()`](xref:NextORM.Core.MergeBuilder`1.Returning) и [`Returning(x => new { ... })`](xref:NextORM.Core.MergeBuilder`1.Returning``1(System.Linq.Expressions.Expression{System.Func{`0,``0}})) материализуют слитые строки через выходную клаузу провайдера — SQL Server `OUTPUT inserted.<col>`, PostgreSQL `RETURNING target.<col>` (17+). Читаются через `Single()`/`SingleAsync()`/`ToList()`/`ToListAsync()`:

```csharp
var rows = ctx.MergeInto<IDest>()
    .Using(source)
    .OnKeys()
    .WhenMatched().ThenUpdate()
    .WhenNotMatched().ThenInsert()
    .Returning(x => new { x.Id, x.Name })
    .ToList();
```

Форма key upsert (`WhenMatchedUpdate()`/`WhenNotMatchedInsert()`) строк не возвращает — используйте веточную форму. SQLite/MySQL/MariaDB и ClickHouse отклоняют полный `MERGE` (а значит и возврат) с `NotSupportedException`.

## Просмотр SQL

[`ToSql()`](xref:NextORM.Core.MergeBuilder`1.ToSql) рендерит параметризованный SQL, который выполнил бы `Merge()`, не открывая соединение:

```csharp
var sql = ctx.MergeInto<ISimpleEntity>()
    .Using(new SimpleEntity { Id = 1, Name = "a" })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .ToSql();
```

## Сводка по провайдерам

| Провайдер | Key upsert | Полный `MERGE` | `RETURNING`/`OUTPUT` | Примечание |
|---|---|---|---|---|
| SQL Server | `MERGE ... USING (VALUES ...)` | да | `OUTPUT inserted.<col>` | все ветки, включая `WHEN NOT MATCHED BY SOURCE` |
| PostgreSQL | `ON CONFLICT ... DO UPDATE` | да (15+) | `RETURNING target.<col>` (17+) | `DO NOTHING`; без `BY SOURCE` |
| SQLite | `ON CONFLICT ... DO UPDATE` | — | — | только key upsert |
| MySQL | `ON DUPLICATE KEY UPDATE` | — | — | только key upsert |
| MariaDB | `ON DUPLICATE KEY UPDATE` | — | — | только key upsert |
| In-memory | применяется к зарегистрированной последовательности | — | — | только key upsert, без SQL |
| ClickHouse | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` | движкового upsert нет |

## Примечания и ограничения

* **Значения — параметры.** Значение источника никогда не встраивается в SQL; константы становятся именованными параметрами (`@p0`, `$p0`, ...) и переносятся вместе со строками `VALUES`, как в `INSERT`.
* **Ветки выполняются в порядке объявления.** SQL Server и PostgreSQL вычисляют клаузы `WHEN` сверху вниз; к строке применяется первая подходящая клауза.
* **Условие `WHEN NOT MATCHED BY SOURCE` на SQL Server — только по target.** `MERGE` в SQL Server не предоставляет в этой клаузе алиас источника, поэтому условие может ссылаться только на строку target.
* **Источник — серверный запрос.** `Using(ctx.From<T>().Where(...))` / `Using(QueryCommand<T>)` принимает только форма полного `MERGE`; key-upsert-провайдеры откатываются к батчу `VALUES`.
* **Мутации не готовятся и не кладутся в кэш планов.** Оптимизация в nextorm нацелена только на read-only запросы (`Prepare`, неявный кэш планов, бенчмарки); merge всегда рендерит и выполняет одну команду за вызов.
* **Число затронутых строк.** `Merge()`/`MergeAsync()` возвращают число строк, которое сообщает провайдер.
* In-memory-провайдер только для чтения: полный `MERGE` бросает `NotSupportedException`; только key-upsert merge применяется к зарегистрированной последовательности в контексте.

## См. также

- [Изменение данных (INSERT)](19-insert-statement.md)
- [Изменение данных (DELETE)](20-delete-statement.md)
- [Изменение данных (UPDATE)](21-update-statement.md)
- [Ограничения и что вне области](../advanced/limitations.md)
- [Обзор провайдеров](../providers/overview.md)
- [Краткий справочник API](../advanced/api-reference.md)
