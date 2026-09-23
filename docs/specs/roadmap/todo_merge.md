# TODO: Слияние данных (`MERGE` / upsert)

> **Статус.** **Фаза 1 (key upsert) — реализована** (23.09.2026): `MergeInto` → `MergeBuilder<T>`,
> `.Using(entity|batch)`, `.OnKeys()`, `.WhenMatchedUpdate()`, `.WhenNotMatchedInsert()`,
> `.Merge()/.MergeAsync()/.ToSql()`; диалектные хуки `SupportsOnConflict`/`SupportsOnDuplicateKey`/
> `SupportsMerge` + `MakeOnConflict`/`MakeOnDuplicateKey`/`MakeUpsertValueReference`/`MakeMerge`;
> SQLite/PostgreSQL/MySQL/MariaDB/SQL Server — поддержка, ClickHouse и in-memory — `NotSupportedException`.
> См. публичные доки: [Upsert (key merge)](../guide/19-insert-statement.md#upsert-key-merge),
> [ограничения](../advanced/limitations.md), [обзор провайдеров](../providers/overview.md).
> **Фаза 2 (полный `MERGE` с ветками, источник-запрос) — открыта** (см. «Этапы внедрения»).

> Рабочий план (design RFC). Источник: GitHub issue
> [#6 «TODO: Merge»](https://github.com/AlexeyShirshov/nextorm/issues/6), milestone `1.0-a.4`.
> Общий каркас DML (метаданные, ось `MutationCommand`, `SqlMutationBuilder`, роль
> `IMutationExecutor`, план-кэш, транзакции) описан в [todo_insert.md](todo_insert.md);
> зависит от `INSERT` (#3), `UPDATE` (#4), `DELETE` (#5).

## Пункт и цель

- Фича: слияние набора строк-источника с целевой таблицей по ключу. Два уровня:
  1. **key upsert** — «вставить или обновить по ключу», выразимый на всех SQL-провайдерах через
     их родной синтаксис (`ON CONFLICT` / `ON DUPLICATE KEY` / `MERGE`);
  2. **полный `MERGE`** — с ветками `WHEN MATCHED` / `WHEN NOT MATCHED` (включая `DELETE`),
     доступный только там, где движок его поддерживает (SQL Server, PostgreSQL 15+).
- Критерий приёмки: `ctx.MergeInto<T>().Using(source).OnKeys().WhenMatchedUpdate().WhenNotMatchedInsert().Merge()`
  выполняет upsert на SQLite/PostgreSQL/SQL Server/MySQL/MariaDB; ветки/конструкции, которые
  диалект не умеет, отклоняются `NotSupportedException` с явным сообщением; ClickHouse — без
  поддержки.
- **Change tracking отсутствует**: источник задаётся явно (сущность, батч, запрос), решение
  «вставить/обновить» принимает база, а не контекст.

## Почему это нужно (мотивация)

1. **Upsert — базовая операция записи.** «Вставить, если нет, иначе обновить» нужен повсеместно;
   без него пользователи собирают сырой SQL поверх nextorm.
2. **Провайдерная переносимость.** Синтаксис радикально различается (`ON CONFLICT` vs
   `ON DUPLICATE KEY` vs `MERGE`); это ровно тот случай, для которого существует `ISqlDialect`
   с capability-флагами (`DataContext/Dialect/SqlDialectBase.cs:20-41`).
3. **Пробел в матрице.** `comparison/linq2db-comparison.md:50-51` отмечает DML и «bulk copy / merge» как
   отсутствующие; issue #6 — трекер, входящий в тот же milestone `1.0-a.4`
   (полный `MERGE` сложнее одиночных `INSERT`/`UPDATE`/`DELETE`).

## Текущее состояние и разрыв

Общий разрыв — в [todo_insert.md](todo_insert.md#текущее-состояние-и-разрыв). Специфично для merge:

| Слой | Чего не хватает |
|---|---|
| Команда | `MergeCommand<TEntity>`: источник, ключи совпадения, список веток |
| Рендер | `SqlMutationBuilder.MakeMerge`: `USING`, `ON`, `WHEN MATCHED/NOT MATCHED`, per-provider хвосты |
| Диалект | `SupportsMerge`/`SupportsOnConflict`/`SupportsOnDuplicateKey`, `MakeUpsert`/`MakeMerge` |
| Источник | представление in-memory батча как `VALUES`-производной таблицы; подзапрос/таблица как источник |
| Метаданные | ключ (вводится в #3) для `.OnKeys()` |

`MergeCommand` — самая сложная команда: в отличие от `INSERT`/`UPDATE`/`DELETE`, у неё есть
*источник* (`USING`) и *несколько условных веток*, поэтому её нельзя свести к одному списку
присваиваний. Рендер веток переиспользует транслятор выражений/SQL-условий, но структура команд
задаётся явно.

## Дизайн: два уровня

### Уровень 1 — key upsert (основной, все SQL-провайдеры)

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) одна сущность
ctx.MergeInto<ISimpleEntity>()
    .Using(entity)
    .OnKeys()                          // объявленный ключ из метаданных (#3)
    .WhenMatchedUpdate()               // SET не-key/non-identity/non-computed из источника
    .WhenNotMatchedInsert()            // INSERT не-identity/non-computed
    .Merge();

// (B) батч
ctx.MergeInto<ISimpleEntity>()
    .Using(new[] { e1, e2, e3 })
    .OnKeys()
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .Merge();

// (C) источник — запрос nextorm
ctx.MergeInto<IDest>()
    .Using(ctx.From<ISource>().Where(s => s.Active))
    .On((d, s) => d.Id == s.Id)
    .WhenMatchedUpdate()
    .WhenNotMatchedInsert()
    .Merge();
```

Отображение на SQL по провайдерам:

| Провайдер | Level 1 | SQL |
|---|---|---|
| PostgreSQL | да | `INSERT ... ON CONFLICT (<keys>) DO UPDATE SET ...` (`SupportsOnConflict`) |
| SQLite | да | `INSERT ... ON CONFLICT (<keys>) DO UPDATE SET ...` (3.24+) |
| MySQL | да | `INSERT ... ON DUPLICATE KEY UPDATE ...` (`SupportsOnDuplicateKey`) |
| MariaDB | да | `INSERT ... ON DUPLICATE KEY UPDATE ...` (или `REPLACE`, но с его семантикой delete+insert) |
| SQL Server | да | `MERGE <target> USING (VALUES ...) src ON ... WHEN MATCHED THEN UPDATE ... WHEN NOT MATCHED THEN INSERT ...` |
| ClickHouse | нет | движкового upsert нет; `ReplacingMergeTree` — свойство движка, не DML — `NotSupportedException` |

### Уровень 2 — полный `MERGE` (SQL Server, PostgreSQL 15+)

```csharp
ctx.MergeInto<IDest>()
    .Using(source)                                        // сущность/батч/запрос/таблица
    .On((d, s) => d.Id == s.Id)
    .WhenMatched().ThenUpdate(d => new { d.Name })        // или .ThenDelete()
    .WhenNotMatched().ThenInsert(d => new { d.Id, d.Name })
    .WhenNotMatchedBySource().ThenDelete()                // SQL Server; PG 15 — только matched
    .Merge();
```

Полный `MERGE` гейтится флагом `SupportsMerge` и отклоняется остальными провайдерами, кроме
случая, когда форма сводится к key upsert уровня 1 (тогда рендерится родной upsert-синтаксис).
`WhenNotMatchedBySource` доступен только SQL Server; `WHEN MATCHED THEN DELETE` — SQL Server и
PostgreSQL 15+; capability-флаги `SupportsMergeDelete`/`SupportsMergeBySourceDelete`.

Черновые сигнатуры:

```csharp
public sealed class MergeBuilder<TEntity>
{
    public MergeBuilder<TEntity> Using(TEntity entity);
    public MergeBuilder<TEntity> Using(IEnumerable<TEntity> entities);
    public MergeBuilder<TEntity> Using(EntityBuilder<TEntity> source);
    public MergeBuilder<TEntity> Using(QueryCommand<TEntity> source);
    public MergeBuilder<TEntity> OnKeys();
    public MergeBuilder<TEntity> On(Expression<Func<TEntity, TEntity, bool>> match);
    public MergeBuilder<TEntity> WhenMatchedUpdate();
    public MergeBuilder<TEntity> WhenNotMatchedInsert();
    public MergeBuilder<TEntity> WhenMatched();
    public MergeBuilder<TEntity> WhenNotMatched();
    public MergeBuilder<TEntity> WhenNotMatchedBySource();

    public int Merge();
    public Task<int> MergeAsync(CancellationToken cancellationToken = default);
    public MergeBuilder<TEntity> Prepare();
}
```

Имена SQL-ориентированы (`MergeInto`/`Using`/`On`/`WhenMatched`/`WhenNotMatched`) и совпадают с
linq2db (`Merge`, `MergeWithOutput`). Для батча источник рендерится как производная таблица
`USING (VALUES (...),(...)) AS src (<cols>)`; для запроса — `USING (<select>) AS src`.

## Диалектный план

- `ISqlDialect`/`SqlDialectBase` (base `false`):
  `SupportsMerge`, `SupportsMergeDelete`, `SupportsMergeBySourceDelete`, `SupportsOnConflict`,
  `SupportsOnDuplicateKey`; хуки `MakeUpsert`/`MakeMerge` (или один `MakeMerge` с ветвлением по
  форме).
- `PostgresDialect`: `SupportsOnConflict = true`; `SupportsMerge = true` при сервере ≥ 15.
- `SQLiteDialect`: `SupportsOnConflict = true` (SQLite ≥ 3.24).
- `MySqlDialect`: `SupportsOnDuplicateKey = true`; `MariaDbDialect`: то же (при желании `REPLACE`
  как фаза 2).
- `SqlServerDialect`: `SupportsMerge = true`, `SupportsMergeDelete = true`,
  `SupportsMergeBySourceDelete = true`.
- `ClickHouseDialect`: все `false` — `NotSupportedException`.

## Ограничения и цена

- **Версия сервера.** PostgreSQL: `MERGE` только с 15; `ON CONFLICT` — с 9.5. Диалект статичен и о
  версии сервера не знает (открытый вопрос №1). В фазе 1 — key upsert для PG (работает везде), а
  полный `MERGE` помечен как требующий PG 15+.
- **`WHEN NOT MATCHED BY SOURCE`.** Только SQL Server (и PG с оговорками); на остальных —
  `NotSupportedException`, а не молчаливая деградация.
- **Источник-батч.** Большие батчи параметризуются и разбиваются на чанки (как в
  [todo_insert.md](todo_insert.md)); `BulkCopy`/бинарная загрузка — отдельный план
  [todo_bulk_insert.md](todo_bulk_insert.md).
- **Нет `RETURNING`/`OUTPUT`-материализации** в этой фазе (issue #15) — при необходимости отдельно.
- **ClickHouse** не поддерживает ни транзакции, ни DML-merge — только `NotSupportedException`.
- **Публичный API** — самый крупный билдер; обновить `API-NAMING-REVIEW.md` и (при заморозке)
  `PublicAPI.*`.
- **Аллокации**: построение `USING (VALUES ...)` масштабируется по числу строк/колонок; для
  одиночной сущности путь не должен добавлять боксинга сверх SELECT-параметров.

## Этапы внедрения

- **Фаза 1 (выполнена, 23.09.2026):** key upsert (одна сущность и батч, `.OnKeys()`) на
  SQLite/PostgreSQL/SQL Server/MySQL/MariaDB; ClickHouse и in-memory — `NotSupportedException`.
- **Фаза 2 (открыта):** источник-запрос (`Using(EntityBuilder/QueryCommand)`), полный `MERGE` с ветками
  `WHEN MATCHED`/`WHEN NOT MATCHED BY SOURCE` для SQL Server/PostgreSQL 15+, `WHEN MATCHED THEN DELETE`,
  `RETURNING`/`OUTPUT` (issue #15), in-memory upsert.
- **Вне области** (bulk — обособленно в [todo_bulk_insert.md](todo_bulk_insert.md)): `BulkCopy`/binary insert, временные таблицы, `REPLACE`-семантика MariaDB,
  `ReplacingMergeTree` ClickHouse.

## План тестов

- Core (`tests/nextorm.core.tests/`): `.OnKeys()` требует объявленный ключ; форма `.On(expr)`
  без ключей; ClickHouse/in-memory — `NotSupportedException`; `Prepare()` не пересобирает SQL.
- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`):
  `ON CONFLICT ... DO UPDATE` (PG/SQLite), `ON DUPLICATE KEY UPDATE` (MySQL/MariaDB),
  `MERGE ... USING ... WHEN MATCHED/NOT MATCHED` (SQL Server); параметризация `VALUES`.
- Диалекты (`*DialectTests.cs`): значения `SupportsMerge`/`SupportsMergeDelete`/
  `SupportsMergeBySourceDelete`/`SupportsOnConflict`/`SupportsOnDuplicateKey`.
- Интеграция (`tests/nextorm.integration.tests/`, `*SpecificTests.cs`): upsert новой строки и
  существующей на каждом провайдере; батч с частичным совпадением; полный `MERGE` с
  `WHEN MATCHED THEN DELETE` (SQL Server/PG 15+) и `WHEN NOT MATCHED BY SOURCE` (SQL Server);
  ClickHouse — `NotSupportedException`.
- Покрытие: база 84.9% line / 73.2% branch; новые файлы core входят в `coverage.settings.xml`
  (`nextorm.{core,sqlite,postgres,sqlserver}`); MySQL/ClickHouse-специфика покрытием не измеряется
  — это указывается явно.

## Открытые вопросы

1. Знать версию сервера (PostgreSQL 15+/SQLite 3.24+/MariaDB 10.5+) для выбора синтаксиса:
   определять при открытии соединения (`ServerVersion`) или документировать как требование?
2. Единый `MakeMerge` с ветвлением по форме vs отдельные `MakeUpsert`/`MakeMerge`.
3. Синтаксис `.On((target, source) => ...)` vs `.OnKeys()`; нужен ли явный алиас источника.
4. Как выражать источник-запрос: перегрузка `Using(EntityBuilder<T>)` vs `Using(QueryCommand<T>)`
   vs `UsingRowSet(...)`.
5. Поддержка `MERGE ... RETURNING`/`OUTPUT` — в этой фазе или целиком в issue #15.
6. MariaDB: `ON DUPLICATE KEY UPDATE` vs `REPLACE` (delete+insert меняет FK-поведение).

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/MergeBuilder.cs`, `src/nextorm.core/Query/Mutations/MergeCommand.cs`.
- Правки: `src/nextorm.core/DataContext/SqlMutationBuilder.cs`, `DataContext/Roles/IMutationExecutor.cs`,
  `DataContext/QueryExecutor.cs`, `DataContext/DataContextExtensions.cs`,
  `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, провайдерные диалекты,
  `DataContext/InMemoryDataContext.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql}.tests/`,
  `tests/nextorm.integration.tests/*SpecificTests.cs` (MERGE/upsert не единообразен между
  провайдерами, поэтому — provider-specific).
- Документация: `docs/guide/19-insert-statement.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
