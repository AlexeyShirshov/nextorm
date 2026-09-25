# TODO: варианты хинтов — join / subquery / tables-in-scope

> Tracking issue: —

> Рабочий план (design RFC). Источник: подсекция «Пропущенная ось: shipped-поверхность
> `LinqExtensions`» в [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md)
> (Gap-пункт «`JoinHint`/`SubQueryHint`/`TablesInScopeHint`»). Аналоги linq2db
> `LinqExtensions.JoinHint` / `SubQueryHint` / `TablesInScopeHint`.

## 1. Пункт и цель

- **Что уже есть в nextorm:** statement-level `QueryCommand<TResult>.Hint(...)` (SQL Server `OPTION (...)`,
  PostgreSQL/MySQL/MariaDB `/*+ ... */`), табличные хинты SQL Server (`WithTableHint`), хинты индексов
  (MySQL/MariaDB, SQLite, SQL Server).
- **Чего нет:** хинт, привязанный к **конкретному join** (позиция внутри `JOIN`, а не после `SELECT`),
  к **подзапросу/производной таблице** и «на все таблицы в области видимости» одним вызовом.
- **Критерий приёмки:**
  1. можно повесить хинт на конкретный join/подзапрос/набор источников;
  2. SQL корректен по синтаксису на каждом провайдере, где форма существует; остальные отклоняют
     `NotSupportedException`;
  3. хинт входит в ключ плана;
  4. существующий `Hint`/`WithTableHint`/`WithIndex` не ломаются.
- **Примечание.** На PostgreSQL/MySQL statement-level `Hint(...)` уже рендерит сырой `/*+ ... */`, поэтому
  часть join-хинтов там достижима вручную; реальный структурный пробел — **SQL Server** (join-хинт идёт
  внутрь `JOIN`/в `OPTION`), и отсутствие «tables-in-scope» как отдельного вызова.

## 2. Провайдерная матрица (форм)

Источник по SQL Server — MS Learn «Join hints (Transact-SQL)» и «OPTION clause» (проверено); по остальным —
публичные доки провайдеров (часть ячеек помечена «проверить» перед реализацией).

| Провайдер | Join hint | Subquery hint | Tables-in-scope |
|---|---|---|---|
| SQL Server | `INNER\|LEFT\|RIGHT\|FULL [LOOP\|MERGE\|HASH] JOIN`; `OPTION (LOOP\|MERGE\|HASH JOIN)` | Отдельного API нет: query hints применяются ко всему statement (MS Learn: «Query hints can't be appended to the subselect»); table hints — на каждую таблицу | `WITH (...)` на каждой таблице; `OPTION (...)` глобально |
| PostgreSQL | Нативных нет; `pg_hint_plan`: `/*+ NestLoop(t1 t2) MergeJoin(t1 t2) Rows(t1 t2 #N) */` | `/*+ */` через `pg_hint_plan` | `/*+ ... */` с перечислением alias'ов |
| MySQL 8 | Optimizer hints: `/*+ JOIN_ORDER(...) JOIN_FIXED_ORDER() JOIN_PREFIX(...) JOIN_SUFFIX(...) NO_BNL() */` | `/*+ SUBQUERY(...) */` (проверить актуальный список) | `/*+ ... */` с alias'ами |
| MariaDB | Optimizer hints (частично иной набор) — **проверить** | **проверить** | **проверить** |
| ClickHouse | Нет join-хинтов; `SETTINGS join_algorithm=...` (query-level) | Нет | `SETTINGS` (query-level) |
| SQLite | — | — | — |
| InMemory | — | — | — |

**Единообразие:** поддержка сильно неоднородна → per-provider `Supports*`/`Make*`; базовые дефолты
`false`, отклонение явным `NotSupportedException`. Для PostgreSQL/MySQL/MariaDB форма — тот же
`/*+ ... */`-механизм, что уже у `Hint`, поэтому возможна общая реализация с указанием позиции.

## 3. C#-аналог и tier

CLR-аналога нет → **tier b** (новые методы на билдере + диалектные хуки). Имена — по linq2db:
`JoinHint`, `SubQueryHint`, `TablesInScopeHint`; согласовать с `API-NAMING-REVIEW.md`.

## 4. Дизайн и публичный API (предложение)

```csharp
// EntityBuilder<TEntity> / JoinedEntityBuilder<...>
public EntityBuilder<TEntity> WithJoinHint(string hint);
public EntityBuilder<TEntity> WithSubQueryHint(string hint);
public EntityBuilder<TEntity> WithTablesInScopeHint(string hint);
```

- Хранение: слот хинта на `JoinExpression`/`FromExpression` (join/subquery) и список хинтов области на
  `QueryCommand` (tables-in-scope); в `CopyTo`/`Clone`.
- Диалект: `ISqlDialect.SupportsJoinHints`/`MakeJoinHint(...)`,
  `SupportsSubQueryHints`/`MakeSubQueryHint(...)`,
  `SupportsTablesInScopeHints`/`MakeTablesInScopeHint(...)`; база — `false`.
- SQL Server: join-хинт рендерится внутри `JOIN` (`INNER LOOP JOIN`), tables-in-scope — либо `WITH (...)`
  на каждой таблице, либо `OPTION (...)`.
- PostgreSQL/MySQL/MariaDB: `/*+ ... */` с alias'ами источников (нужен резолвер alias'ов из `FromExpression`).
- План-ключ: хинты входят в хэш join/from/план (аналогично `HintsPlanHash`).

## 5. План этапов

1. RFC по семантике (join/subquery/scope) и именам.
2. Диалектные хуки `Supports*`/`Make*` (база `false`), SQL Server + PostgreSQL + MySQL + MariaDB.
3. Поверхность билдера + XML-док; слоты на `JoinExpression`/`FromExpression`.
4. План-ключ и `CopyTo`/`Clone`.
5. Тесты + доки (EN+RU) + API-реестр.

## 6. План тестов

- SQL-gen: SQL Server join hint (`INNER LOOP JOIN`), PG/MySQL `/*+ ... */` с alias'ами, tables-in-scope;
  `ShouldThrow` на SQLite/ClickHouse/in-memory.
- Негатив: коллизия alias'ов, повторные вызовы, пустой хинт.
- План-кэш: разные хинты → разные планы.
- Интеграция (опционально): поведение на реальных БД не проверяемо портируемо (хинты — подсказка);
  достаточно SQL-gen.

## 7. Файлы к изменению

- `src/nextorm.core/Expressions/JoinExpression.cs`, `FromExpression.cs`,
  `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Query/QueryCommand.cs`,
  `src/nextorm.core/DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, `DataContext/SqlBuilder.cs`,
  `Query/QueryPlanEqualityComparer.cs`, `nextorm.{sqlserver,postgres,mysql,mariadb}/*Dialect.cs`.
- Доки: `docs/guide/17-query-hints.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## 8. Открытые вопросы

1. Не дублирует ли `TablesInScopeHint` `WithTableHint` — может, достаточно расширить последний?
2. Нужен ли `SubQueryHint` как отдельный API, если на SQL Server его нет, а на PG/MySQL есть `Hint`?
3. Синтаксис хинта — строка (как `Hint`) или типизированный enum (`JoinAlgorithm.Loop/Hash/Merge`)?
4. Взаимодействие с `pg_hint_plan` (требует расширения; без него — обычный комментарий).
