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

Источник — публичные доки провайдеров, проверено перед реализацией: PostgreSQL — SQL reference
(`current_schema`, квалификация `schema.table`); SQL Server — Microsoft Learn / T-SQL reference
(четырёхчастные имена `server.database.schema.table`, linked server, `[db].[schema].[obj]`); MySQL —
Reference Manual (квалификация `db_name.tbl_name`, «schema» и «database» синонимы); MariaDB — Knowledge
Base (то же, что MySQL); SQLite — документация (квалификация `schema-name.table-name`, attached-базы);
ClickHouse — SQL reference (`database.table`, отдельного schema-уровня нет).

Матрица «сколько частей имени выразимо» (реализовано; `—` = уровень не выразим и отклоняется):

| Провайдер | 1 часть `table` | 2 части `schema.table` | 2 части `db.table` | 3 части `db.schema.table` | 4 части `server.db.schema.table` | `WithTableExpression` |
|---|---|---|---|---|---|---|
| PostgreSQL | да | да (`"schema"."table"`) | — (кросс-БД нет без dblink/FDW) | — | — | да, `(sql) as "t1"` |
| SQL Server | да | да (`[schema].[table]`) | да (`[db].[table]`) | да | да (linked server) | да, `(sql) as [t1]` |
| MySQL | да | — (db = schema) | да (`` `db`.`table` ``) | — | — | да, `` (sql) as `t1` `` |
| MariaDB | да | — (db = schema) | да (`` `db`.`table` ``) | — | — | да, `` (sql) as `t1` `` |
| SQLite | да | да (`"main"."table"`, attached-база) | да (то же, `database` = attached-база) | — | — | да, `(sql) as 't1'` |
| ClickHouse | да | — (нет schema-уровня) | да (`` `db`.`table` ``) | — | — (cluster — не server-квалификация) | да, `` (sql) as `t1` `` |
| InMemory | — (нет SQL) | — | — | — | — | — (нет SQL) |

`WithTableExpression` — сырой SQL вместо доступа к таблице; форма одинаково «сырая» на всех SQL-провайдерах,
in-memory `—`. Риск инъекции → ответственность вызывающего (как у `WithSql`/`FromSql`), но нужна явная
документация. Уровни `database`/`schema` у MySQL/MariaDB/ClickHouse/SQLite называют один и тот же
единственный квалификатор, поэтому задать можно только один из двух (`WithSchema` **или** `WithDatabase`),
иначе — `NotSupportedException`.

**Единообразие (реализовано):** квалификация `schema`/`database`/`server` — per-provider
`ISqlDialect.MakeQualifiedTableName(server, database, schema, table)` (база — ANSI `schema.table`) плюс
флаги `SupportsCrossDatabase`/`SupportsLinkedServer` (дефолт `false`). Провайдер без уровня
(PostgreSQL — server/database; MySQL/MariaDB/ClickHouse/SQLite — server) отклоняет переопределение
`NotSupportedException` в `SqlSourceRenderer.MakeFrom`, а не молча теряет его. `WithTableExpression` —
общий хук рендера `FROM` (`MakeTableExpression`), требует `SupportsRawSqlSource`.

## 3. C#-аналог и tier

CLR-аналога нет → **tier b** (новые методы на билдере + диалектный хук). Имена — по linq2db
(`WithTableName`/`WithSchema`/...); согласовать с `API-NAMING-REVIEW.md`.

## 4. Дизайн и публичный API (реализовано)

```csharp
// EntityBuilder<TEntity>
public EntityBuilder<TEntity> WithTableName(string name);
public EntityBuilder<TEntity> WithSchema(string schema);
public EntityBuilder<TEntity> WithDatabase(string database);
public EntityBuilder<TEntity> WithServer(string server);
public EntityBuilder<TEntity> WithTableExpression(string sql);
```

- Хранение: слоты на `FromExpression` (`TableNameOverride`/`SchemaOverride`/`DatabaseOverride`/
  `ServerOverride`/`TableExpressionOverride`, `internal readonly`), копируются приватным копирующим
  конструктором `WithOverrides(...)`; `CloneForCache` не меняется (строки неизменяемы и разделяются).
  Override имеет приоритет над именем из метаданных; при `WithTableName` naming convention **не**
  применяется (имя явное).
- Поверхность: пять методов на `EntityBuilder<TEntity>`; хранятся как поля билдера, применяются в
  `Select`/`ToCommand` через `internal ResolveSource()`. Тот же `ResolveSource()` вызывает
  `JoinSourceResolver`, поэтому override присоединяемой сущности доезжает до join. Non-generic
  `EntityBuilder` (`From("table")` фактически создаёт `EntityBuilder<TableAlias>`) новых методов не
  получил — и так покрыт.
- Семантика относительно `From("table")`/метаданных: override применяется к **физическому** источнику
  (маппинг-сущность, `From("table")` или явный физический `FromExpression`). Для производного запроса,
  подзапроса, TVF или PIVOT — `NotSupportedException` (иначе имя молча потерялось бы). `WithTableExpression`
  заменяет источник целиком (`(sql) AS alias`), не сочетается с квалификаторами имени
  (`InvalidOperationException`); с `FOR SYSTEM_TIME`/табличными/индексными хинтами — `NotSupportedException`.
- Экранирование/квотирование: имя собирается диалектом (части через `.`), затем `QuoteQualifiedIdentifier`
  квотирует **каждую** часть разделителем провайдера при `QuoteIdentifiers` (`[srv].[db].[dbo].[t]`,
  `` `db`.`t` ``, `"schema"."t"`). Алиас `WithTableExpression` — через `MakeTableAlias` (`"t1"`/`[t1]`/`` `t1` ``/`'t1'`).
- Диалект: `ISqlDialect.MakeQualifiedTableName(string? server, string? db, string? schema, string table)`
  (DIM, база — ANSI `schema.table`) + DIM-флаги `SupportsCrossDatabase`/`SupportsLinkedServer` (дефолт
  `false`); те же члены `public virtual` в `SqlDialectBase`; переопределения в `SqlServerDialect`,
  `MySqlDialect` (MariaDB наследует), `ClickHouseDialect`, `SqliteDialect`.
- `WithTableExpression`: `MakeTableExpression(...)` рендерит сырой текст как derived-источник с алиасом;
  требует `SupportsRawSqlSource`; in-memory не имеет SQL → все пять методов бросают `NotSupportedException`
  сразу в билдере (`EnsureSqlSourceOverride`, `_dataProvider is InMemoryDataContext`).
- План-ключ: `FromExpressionPlanEqualityComparer.Equals`/`GetHashCode` включают все пять слотов (сравнение
  `Ordinal`) до короткого замыкания по `Table`; `QueryCommand.FromPlanHash` использует тот же comparer.
- Extend-only: без переопределений `ResolveSource` возвращает прежний `FromExpression` (или `null`, как
  раньше), `MakeFrom`/план-ключ дают байт-в-байт прежний SQL/план.

## 5. План этапов (выполнено)

1. RFC по приоритету overrides и семантике `WithTableExpression` (алиасы, join) — ✅ (см. §4).
2. Слоты на `FromExpression` + поверхность `EntityBuilder<T>` + XML-док — ✅.
3. Диалектный `MakeQualifiedTableName` (SQL Server 4-part, MySQL/ClickHouse db.table, PG/SQLite schema.table) — ✅.
4. План-ключ и `CopyTo`/`Clone` (копирующий конструктор `WithOverrides`) — ✅.
5. Тесты + доки (EN+RU) + API-реестр — ✅ (см. §6, `## Статус реализации`).

## 6. План тестов (реализовано)

- SQL-gen (без БД, `dotnet run --project tests/nextorm.<provider>.tests -c Debug`):
  `tests/nextorm.sqlserver.tests/SourceOverrideSqlGenerationTests.cs` (1–4 part, quoting, `WithTableExpression`,
  комбинация с квалификатором → throw, отсутствие утечки),
  `tests/nextorm.postgres.tests/…` (`schema.table`; database/server → `ShouldThrow`),
  `tests/nextorm.mysql.tests/…` и `tests/nextorm.mariadb.tests/…` (`db.table`; schema+database → throw; server → throw),
  `tests/nextorm.clickhouse.tests/…` (`db.table`; server → throw),
  `tests/nextorm.sqlite.tests/…` (`main.table`, `WithTableExpression`, план-кэш),
  `tests/nextorm.core.tests/SourceOverrideTests.cs` (in-memory отказ по всем пяти методам).
- План-кэш (SQLite, `GetPreparedQueryCommand(..., storeInCache: true)`): одинаковый override → тот же
  prepared-объект (`ReferenceEquals`), разные override → разные; без override — прежний SQL.
- Интеграция (опционально): не запускалась (задача без контейнеров) — `schema.table` на PostgreSQL/SQL Server
  остаётся кандидатом для `CommonTestSuite`.

**Baseline покрытия:** `coverage.settings.xml` включает только `nextorm.{core,sqlite,postgres,sqlserver}`;
новый код — в `nextorm.core` (покрыт core/sqlite/sqlserver/postgres SQL-gen тестами) и в
`nextorm.{mysql,mariadb,clickhouse}` (в coverage не входит — SQL-gen тесты добавлены отдельно). Полные
прогоны без контейнеров: core 406, sqlite 521, sqlserver 397, postgres 526, mysql 173, mariadb 96,
clickhouse 341 — 0 failed. Инструментальный before/after (`dotnet-coverage`+`reportgenerator`) не
запускался: прогон `dotnet test` без контейнеров и вне CI-скрипта даёт нестабильный базлайн, а новые
строки покрыты SQL-gen тестами напрямую (см. `## Статус реализации`).

## 7. Файлы (фактически изменённые)

- Код: `src/nextorm.core/Expressions/FromExpression.cs`, `src/nextorm.core/Builders/EntityBuilder.cs`,
  `src/nextorm.core/DataContext/SqlSourceRenderer.cs`,
  `src/nextorm.core/DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`,
  `src/nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs`,
  `src/nextorm.core/Builders/Joins/JoinSourceResolver.cs`,
  `src/nextorm.sqlserver/SqlServerDialect.cs`, `src/nextorm.mysql/MySqlDialect.cs`,
  `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.sqlite/SqliteDialect.cs`.
- Тесты: `tests/nextorm.{sqlserver,postgres,mysql,mariadb,clickhouse,sqlite}.tests/SourceOverrideSqlGenerationTests.cs`,
  `tests/nextorm.core.tests/SourceOverrideTests.cs`.
- Доки: `docs/guide/01-querying-and-projections.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/providers/overview.md` (+RU), `docs/advanced/limitations.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/comparison/linq2db-backlog-gap-analysis.md`.

## 8. Открытые вопросы (закрыты)

1. Приоритет над `From("table")`/метаданными — **override всегда побеждает** для физического источника;
   для производного источника (subquery/TVF/PIVOT) — `NotSupportedException`.
2. `WithTableExpression` — **только сырой текст** (как `WithSql`); параметры/CTE не разрешаются.
3. `WithServer`/`WithDatabase` — **нужны**: `From("server.db.schema.table")` работает только для безтипового
   `From("table")` и не покрывает маппинг-сущность; override меняет имя, не трогая метаданные.
4. Дедупликация с `todo_join_projection_mapping.md` (`As`) — общий `FromExpression`, но `As` даёт **алиас
   источника**, а override меняет **имя таблицы**; пересечение только в новых слотах (не конфликтуют).

## Статус реализации

**DONE (uncommitted, worktree `issue-99-source-override`, база `5d9b8d8`).**

- ✅ Провайдерная матрица квалификации (§2) — заполнена из доков провайдеров.
- ✅ Пять методов `EntityBuilder<TEntity>` (`WithTableName`/`WithSchema`/`WithDatabase`/`WithServer`/
  `WithTableExpression`) + XML-док; inner `ResolveSource` применяет override и в join-пути.
- ✅ Диалектный хук `MakeQualifiedTableName` + `SupportsCrossDatabase`/`SupportsLinkedServer`; SQL Server
  4-part, MySQL/MariaDB/ClickHouse/SQLite `db.table`, PG/SQLite `schema.table`; невыразимые уровни →
  `NotSupportedException`; InMemory → `NotSupportedException` во всех пяти методах.
- ✅ `WithTableExpression` — derived-источник `(sql) AS alias`, квотирование алиаса по провайдеру.
- ✅ План-ключ (`FromExpressionPlanEqualityComparer` + `FromPlanHash`): разные override → разные планы.
- ✅ Extend-only: без override SQL/план не меняются.
- ✅ Build `dotnet build nextorm.slnx -c Release -m:2` — **0 warnings / 0 errors**.
- ✅ Тесты без контейнеров: 6 провайдерных `SourceOverride*` + core `SourceOverrideTests`;
  полные прогоны core/sqlite/sqlserver/postgres/mysql/mariadb/clickhouse — 0 failed.
- ✅ Доки EN+RU (guide/api-reference/overview/limitations), API-NAMING-REVIEW, linq2db gap-analysis.
- ⚠️ Аудит: субагенты `nextorm-code-auditor`/`nextorm-design-engineer` недоступны (нет инструмента
  `task` в сессии) → выполнен тщательный self-review; запись добавлена в `API-NAMING-REVIEW.md`
  («Самоаудит 26.09.2026»), кодовые находки не выявлены.
- ⚠️ Интеграционные (контейнерные) тесты не запускались по условию задачи; инструментальный
  before/after coverage не снимался (обоснование в §6).
- ⚠️ Шаг 5 (заморозка PublicAPI) остаётся открытым — трекинг SRC1 в API-NAMING-REVIEW.
