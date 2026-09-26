# TODO: per-query переопределение источника (table/schema/database/server, `WithTableExpression`)

> Tracking issue: [#99](https://github.com/AlexeyShirshov/nextorm/issues/99).

> Рабочий план (design RFC). Источник: подсекция «Пропущенная ось: shipped-поверхность
> `LinqExtensions`» в [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md)
> (Gap-пункт «`WithTableExpression` / runtime-переопределение `TableName`/`SchemaName`/`ServerName`»).
> Аналоги linq2db `LinqExtensions.TableID` / `TableName` / `DatabaseName` / `SchemaName` / `ServerName` /
> `WithTableExpression`.

## 1. Пункт и цель

- **Что уже есть в nextorm:** `From("table")` + `TableAlias` для произвольного источника; маппинг имени
  таблицы через `EntityMetadataBuilder<T>.Table(name)` (per-mapping, на процесс); `BulkInsertOptions.TableName`
  (только bulk). `WithTableExpression` (замена доступа к таблице сырым SQL) нет.
- **Чего нет:** переопределения имени таблицы/схемы/БД/сервера **для конкретного запроса** без правки
  метаданных, и raw-выражения вместо таблицы.
- **Критерий приёмки:**
  1. `ctx.From<T>(...).WithTableName("x")` / `.WithSchema("s")` / `.WithDatabase("d")` / `.WithServer("srv")`
     и/или `.WithTableExpression("...")` меняют рендер только этого запроса;
  2. квалификация рендерится с корректным квотированием/лимитами провайдера;
  3. переопределение входит в ключ плана (иначе кэш отдаст SQL с прежним именем);
  4. метаданные и другие запросы не затронуты;
  5. in-memory — `WithTableExpression`/server не применимы (явный отказ или no-op по решению).

## 2. Провайдерная матрица (форм)

Источник — публичные доки провайдеров (часть ячеек «проверить» перед реализацией).

| Провайдер | Table / Schema | Database | Server (linked/remote) |
|---|---|---|---|
| PostgreSQL | `"schema"."table"` | — (кросс-БД нет без dblink/FDW) | — |
| SQL Server | `[schema].[table]` | `[database].[schema].[table]` | `[server].[database].[schema].[table]` (linked server) |
| MySQL | `\`database\`.\`table\`` (db = schema) | = schema (`db.table`) | — |
| MariaDB | то же, что MySQL | = schema | — |
| SQLite | `"schema"."table"` (`main`/attached db) | = attached schema | — |
| ClickHouse | `\`database\`.\`table\`` | = database | — (cluster — не server-квалификация) |
| InMemory | `—` (нет SQL) | `—` | `—` |

`WithTableExpression` — сырой SQL вместо доступа к таблице; форма одинаково «сырая» на всех SQL-провайдерах,
in-memory `—`. Риск инъекции → ответственность вызывающего (как у `WithSql`/`FromSql`), но нужна явная
документация.

**Единообразие:** квалификация `schema`/`database`/`server` — per-provider `ISqlDialect.MakeQualifiedTableName(...)`
с дефолтами; провайдеры без соответствующего уровня (PG server/database) отклоняют соответствующее
переопределение `NotSupportedException`. `WithTableExpression` — общий хук рендера `FROM`.

## 3. C#-аналог и tier

CLR-аналога нет → **tier b** (новые методы на билдере + диалектный хук). Имена — по linq2db
(`WithTableName`/`WithSchema`/...); согласовать с `API-NAMING-REVIEW.md`.

## 4. Дизайн и публичный API (предложение)

```csharp
// EntityBuilder<TEntity>
public EntityBuilder<TEntity> WithTableName(string name);
public EntityBuilder<TEntity> WithSchema(string schema);
public EntityBuilder<TEntity> WithDatabase(string database);
public EntityBuilder<TEntity> WithServer(string server);
public EntityBuilder<TEntity> WithTableExpression(string sql);
```

- Хранение: слоты на `FromExpression` (`TableNameOverride`/`Schema`/`Database`/`Server`/`TableExpression`),
  в `CopyTo`/`Clone`; переопределение имеет приоритет над именем из метаданных.
- Диалект: `ISqlDialect.MakeQualifiedTableName(string? server, string? db, string? schema, string table)`
  + `SupportsCrossDatabase`/`SupportsLinkedServer`; база — только `schema.table`.
- `WithTableExpression`: `MakeFromExpression(...)` рендерит сырой текст как источник (алиасуется как обычная
  таблица).
- План-ключ: переопределения в `FromExpressionPlanEqualityComparer` (+ план-хэш `FromPlanHash`).
- Extend-only: без переопределений путь не меняется.

## 5. План этапов

1. RFC по приоритету overrides и семантике `WithTableExpression` (алиасы, join).
2. Слоты на `FromExpression` + поверхность `EntityBuilder<T>` + XML-док.
3. Диалектный `MakeQualifiedTableName` (SQL Server 4-part, MySQL/ClickHouse db.table, PG/SQLite schema.table).
4. План-ключ и `CopyTo`/`Clone`.
5. Тесты + доки (EN+RU) + API-реестр.

## 6. План тестов

- SQL-gen: schema/database/server override на SQL Server (1–4 part), `db.table` на MySQL/MariaDB/ClickHouse,
  `schema.table` на PostgreSQL/SQLite; `WithTableExpression` как derived-источник; `ShouldThrow` на
  невыразимых уровнях (PG server/database, in-memory).
- План-кэш: два запроса с разными override → разные планы; без override — прежний SQL.
- Интеграция (опционально): схема `schema.table` на PostgreSQL/SQL Server.

## 7. Файлы к изменению

- `src/nextorm.core/Expressions/FromExpression.cs`, `src/nextorm.core/Builders/EntityBuilder.cs`,
  `src/nextorm.core/DataContext/SqlSourceRenderer.cs`, `DataContext/SqlBuilder.cs`,
  `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, `Query/QueryPlanEqualityComparer.cs`,
  `Expressions/FromExpressionPlanEqualityComparer.cs`, `nextorm.*/*Dialect.cs`.
- Доки: `docs/guide/01-querying-and-projections.md` (+RU) либо `docs/guide/03-joins.md` (+RU),
  `docs/advanced/api-reference.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## 8. Открытые вопросы

1. Приоритет над `From("table")`/метаданными — override всегда побеждает?
2. `WithTableExpression` — разрешать ли параметры/CTE, или только сырой текст (как `WithSql`)?
3. Нужны ли `WithServer`/`WithDatabase` вообще, или `From("server.db.schema.table")` уже покрывает кейс?
4. Дедупликация с `todo_join_projection_mapping.md` (`As`) — оба трогают `FromExpression`/`From`.
