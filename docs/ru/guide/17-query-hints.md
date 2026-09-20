# Хинты запросов

> Добавляйте хинты уровня инструкции к запросу через `Hint(...)`, например SQL Server `OPTION (RECOMPILE)`.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [CTE](09-cte.md) · [Обзор провайдеров](../providers/overview.md)

## Обзор

[`Hint`](xref:NextORM.Core.QueryCommand`1) возвращает новую команду с одним или несколькими
хинтами уровня инструкции. Хинты зависят от провайдера: команда хранит обычные строки, а активный
[`ISqlDialect`](xref:NextORM.Core.ISqlDialect) решает, где и как их отрисовать. Повторный вызов накапливает хинты:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(c => c.Id > 1)
    .Select(c => new { c.Id, c.RequiredString })
    .Hint("recompile")
    .Hint("fast 10")
    .ToList();
```

```sql
select id, somestring from complex_entity where (id > 1) option (recompile, fast 10)
```

Пустые хинты игнорируются. Список хинтов входит в ключ плана запроса, поэтому команда с хинтами
никогда не переиспользует кэшированный план идентичной команды без них (и наоборот), а две команды с
разными хинтами не делят один план.

## Сочетание с рекурсивным CTE

SQL Server допускает только одно предложение `OPTION` на инструкцию. Когда запрос также объявил
ограничение рекурсии CTE, хинты сливаются в это же предложение:

```csharp
var sql = dataContext.WithRecursive("nums", body, 100)
    .From("nums")
    .Select(t => new CteNumberRow { n = t["n"].AsInt })
    .Hint("recompile");
```

```sql
-- заканчивается на:
... option (maxrecursion 100, recompile)
```

## Табличные хинты

`EntityBuilder<T>.WithTableHint(params string[] hints)` прикрепляет хинты уровня таблицы к основной
физической таблице. SQL Server рендерит их как предложение `WITH (...)`, между именем таблицы и её
псевдонимом:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .WithTableHint("nolock")
    .Select(c => new { c.Id })
    .ToList();
```

```sql
select id from complex_entity with (nolock)
```

Хинты рендерятся дословно, поэтому передавайте только доверенные значения. Провайдер включается через
[`SupportsTableHints`](xref:NextORM.Core.ISqlDialect.SupportsTableHints) и [`MakeTableHints`](xref:NextORM.Core.ISqlDialect) (SQL Server); остальные диалекты
отклоняют команду с табличными хинтами через `NotSupportedException`. Покрыта только основная
таблица; хинты на присоединённых таблицах пока не входят в API.

## Провайдеры

| Провайдер | Хинты запросов |
|---|---|
| SQL Server | Поддерживаются: рендерятся как завершающее предложение `OPTION (hint, ...)`. |
| SQLite | Не поддерживаются: построение SQL выбрасывает `NotSupportedException`. |
| PostgreSQL | Не поддерживаются: построение SQL выбрасывает `NotSupportedException`. |
| MySQL / MariaDB | Не поддерживаются: построение SQL выбрасывает `NotSupportedException`. |
| ClickHouse | Не поддерживаются: построение SQL выбрасывает `NotSupportedException`. |

Провайдер включается через [`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) и [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect);
построитель отклоняет команду с хинтами у диалекта, который сообщает `false`.

## Модификаторы запроса ClickHouse

ClickHouse имеет четыре модификатора уровня запроса — не хинты, а отдельные методы построителя:
`Final()`, `Sample(ratio[, offset])`, `PreWhere(predicate)` и `Settings(("key", "value"), ...)`. Они
допустимы только в ClickHouse; остальные провайдеры и контекст in-memory бросают
`NotSupportedException`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Final()
    .Sample(0.1, 0.5)
    .PreWhere(c => c.Int > 0)
    .Where(c => c.Boolean == true)
    .Select(c => new { c.Id, c.Int })
    .Settings(("max_threads", "2"))
    .ToList();
```

```sql
select id, nullableint from complex_entity final sample 0.1 offset 0.5
prewhere (nullableint > 0)
where (b = true)
settings max_threads = 2
```

`Final()` принудительно выполняет merge ReplacingMergeTree/CollapsingMergeTree перед чтением; `Sample`
читает долю строк `[0, 1]`; `PreWhere` фильтрует до обычного `WHERE`, позволяя пропустить чтение
остальных колонок; `Settings` добавляет завершающее предложение, значения которого рендерятся как есть
(передавайте только доверенные литералы). `FINAL` и `PREWHERE` требуют движка таблицы, который их
поддерживает — движок `Memory` отклоняет оба.

## Ограничения

* Табличные хинты рендерятся только для основной таблицы; хинты на присоединённой таблице пока не
  входят в API ([`WithTableHint`](xref:NextORM.Core.EntityBuilder`1) применяется к таблице из `FROM` запроса).
* Объединение команды с хинтами через операцию над множествами ([`Union`](xref:NextORM.Core.QueryCommand`1), [`Intersect`](xref:NextORM.Core.QueryCommand`1), ...) не
  защищено; хинт «уезжает» в ту ветку, к которой был привязан, и этого следует избегать.

## См. также

- [Соединения](03-joins.md) - [`CrossApply`](xref:NextORM.Core.EntityBuilder`1)/[`OuterApply`](xref:NextORM.Core.EntityBuilder`1).
- [CTE](09-cte.md) - `maxRecursion` и предложение SQL Server `option (maxrecursion n)`.
- [Запросы и проекции](01-querying-and-projections.md)

---

Source: `src/nextorm.core/Query/QueryCommand.TResult.cs` ([`Hint`](xref:NextORM.Core.QueryCommand`1)),
`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` ([`SupportsQueryHints`](xref:NextORM.Core.ISqlDialect.SupportsQueryHints) / [`RenderQueryHints`](xref:NextORM.Core.ISqlDialect)),
`src/nextorm.sqlserver/SqlServerDialect.cs`.
