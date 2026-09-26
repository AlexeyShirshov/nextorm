# TODO: варианты хинтов — join / subquery / tables-in-scope

> Tracking issue: [#96](https://github.com/AlexeyShirshov/nextorm/issues/96).

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

Источник по SQL Server — MS Learn «Join hints (Transact-SQL)», «Table hints (Transact-SQL)» и «OPTION
clause» (проверено); PostgreSQL — справочник `pg_hint_plan`; MySQL 8 — «Optimizer Hints» (8.0 reference
manual); MariaDB — «Optimizer Hints» (MariaDB KB, наследует MySQL, но набор уже); ClickHouse — «Settings»
и список join-алгоритмов (`join_algorithm`); SQLite — грамматика `INDEXED BY` (join-хинтов нет).

| Провайдер | Join hint | Subquery hint | Tables-in-scope |
|---|---|---|---|
| SQL Server | `INNER\|LEFT\|RIGHT\|FULL [LOOP\|MERGE\|HASH] JOIN`; `OPTION (LOOP\|MERGE\|HASH JOIN)` | Отдельного API нет: query hints применяются ко всему statement (MS Learn: «Query hints can't be appended to the subselect»); table hints — на каждую таблицу | `WITH (...)` на каждой таблице; `OPTION (...)` глобально |
| PostgreSQL | Нативных нет; `pg_hint_plan`: `/*+ NestLoop(t1 t2) MergeJoin(t1 t2) Rows(t1 t2 #N) */` | `/*+ */` через `pg_hint_plan` | `/*+ ... */` с перечислением alias'ов |
| MySQL 8 | Optimizer hints: `/*+ JOIN_ORDER(...) JOIN_FIXED_ORDER() JOIN_PREFIX(...) JOIN_SUFFIX(...) NO_BNL() */` | `/*+ SUBQUERY(...) */` | `/*+ ... */` с alias'ами |
| MariaDB | Optimizer hints (наследует MySQL; `JOIN_ORDER`/`NO_BNL` есть, `JOIN_PREFIX`/`JOIN_SUFFIX` — проверить версию) | `/*+ SUBQUERY(...) */` (как MySQL) | `/*+ ... */` с alias'ами |
| ClickHouse | Нет join-хинтов; `SETTINGS join_algorithm=...` (query-level) | Нет | `SETTINGS` (query-level) — уже покрыт `WithSettings` |
| SQLite | — | — | — |
| InMemory | — | — | — |

**Единообразие:** поддержка сильно неоднородна → per-provider `Supports*`/`Make*`; базовые дефолты
`false`, отклонение явным `NotSupportedException`.

**Ключевое решение:** существует ровно **две формы** рендера:
- **структурная** (`SupportsJoinHints`/`SupportsTablesInScopeHints`) — только SQL Server: join-хинт
  вставляется между типом join и `JOIN` (`inner loop join`), tables-in-scope — как `WITH (...)` на каждой
  физической таблице (переиспользует `MakeTableHints`);
- **комментарийная** (`SupportsInlineHints`, PG/MySQL/MariaDB) — все три варианта сворачиваются в один
  statement-level `/*+ ... */` сразу после `select` (переиспользует `RenderQueryHints`).

PostgreSQL/MySQL/MariaDB поэтому не нуждаются в отдельном `MakeJoinHint`: их форма — тот же
комментарий, что уже у `Hint`, но привязанный к join/подзапросу/области и участвующий в ключе плана.
ClickHouse `SETTINGS` уже реализован как `WithSettings`; отдельных variant-хинтов у него нет — все три
отклоняются. SQLite и InMemory — то же отклонение.

## 3. C#-аналог и tier

CLR-аналога нет → **tier b** (новые методы на билдере + диалектные хуки). Имена — по linq2db:
`WithJoinHint`/`WithSubQueryHint`/`WithTablesInScopeHint` (конвенция nextorm `With*`, как у
`WithTableHint`/`WithIndex`; см. `API-NAMING-REVIEW.md`).

## 4. Дизайн и публичный API

```csharp
// EntityBuilder<TEntity>
public EntityBuilder<TEntity> WithJoinHint(string hint);
public EntityBuilder<TEntity> WithSubQueryHint(string hint);
public EntityBuilder<TEntity> WithTablesInScopeHint(params string[] hints);
```

- **Хранение:** слот `JoinExpression.JoinHint` (join), `FromExpression.SubQueryHint` (производная
  таблица) и список `QueryCommand.TablesInScopeHints` (область). В `CopyTo`/`CloneForCache`.
- **Диалект:**
  - `bool SupportsJoinHints` + перегрузка `MakeJoinKeyword(JoinType, JoinStrictness, bool, string? hint, KeywordCase)`
    (SQL Server; база бросает `NotSupportedException`, если `hint` не `null`);
  - `bool SupportsSubQueryHints` (только комментарийная форма);
  - `bool SupportsTablesInScopeHints` + `MakeTablesInScopeHints(IReadOnlyList<string>, KeywordCase)`
    (SQL Server `WITH (...)`);
  - `bool SupportsInlineHints` — сворачивание всех вариантов в `/*+ ... */` (PG/MySQL/MariaDB).
- **План-ключ:** `JoinHint` в `JoinExpressionPlanEqualityComparer`, `SubQueryHint` в
  `FromExpressionPlanEqualityComparer`, `TablesInScopeHints` напрямую в `QueryPlanEqualityComparer`.
- **Алиасы.** Для комментарийной формы caller указывает alias'ы в тексте хинта (`NestLoop(t1 t2)`):
  nextorm-алиасы (`t1`, `t2`) назначаются на этапе рендера и наружу не отдаются. Резолвер alias'ов не
  заводится — хинт рендерится вербатим (как и `Hint`). Это зафиксировано в limitations.

## 5. План этапов

1. RFC по семантике (join/subquery/scope) и именам — настоящий файл.
2. Диалектные хуки `Supports*`/`Make*` (база `false`), SQL Server + PG + MySQL + MariaDB.
3. Поверхность билдера + XML-док; слоты на `JoinExpression`/`FromExpression`.
4. План-ключ и `CopyTo`/`Clone`.
5. Тесты + доки (EN+RU) + API-реестр.

## 6. План тестов

- SQL-gen: SQL Server join hint (`inner loop join`) и tables-in-scope (`with (...)`); PG/MySQL
  `/*+ ... */` для join/subquery/scope; регресс `Hint`/`WithTableHint`/`WithIndex`.
- `ShouldThrow` на SQLite/ClickHouse/InMemory (join/subquery/scope), `NotSupportedException`.
- Негатив: коллизия alias'ов (два хинта с одинаковым текстом не ломают SQL), пустой/пробельный хинт →
  `ArgumentException`, join-хинт без предшествующего join → `InvalidOperationException`.
- План-кэш: разные join/subquery/scope-хинты → разные ключи.
- Интеграция (опционально): хинты — подсказка, портируемого поведения нет; достаточно SQL-gen.

## 7. Файлы к изменению

- `src/nextorm.core/Expressions/JoinExpression.cs`, `FromExpression.cs`,
  `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Query/QueryCommand.cs`,
  `src/nextorm.core/DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, `DataContext/SqlBuilder.cs`,
  `DataContext/SqlSourceRenderer.cs`, `DataContext/FromRenderOptions.cs`,
  `Query/QueryPlanEqualityComparer.cs`, `Query/QueryCommand.Clone.cs`,
  `Query/QueryCommand.QueryPreparer.cs`, `nextorm.{sqlserver,postgres,mysql,mariadb}/*Dialect.cs`.
- Доки: `docs/guide/17-query-hints.md` (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## 8. Открытые вопросы (решения)

1. Не дублирует ли `TablesInScopeHint` `WithTableHint`? **Нет.** `WithTableHint` вешает хинт только на
   первичную физическую таблицу; `WithTablesInScopeHint` — на каждую физическую таблицу области
   (первичную и присоединённые), а на комментарийных диалектах — в общий `/*+ ... */`.
2. Нужен ли `SubQueryHint` как отдельный API? **Да**, но он выразим только комментарийными
   диалектами (PG/MySQL/MariaDB); SQL Server его отклоняет (MS Learn). Он не дублирует `Hint`: привязан
   к конкретному источнику и входит в ключ плана.
3. Синтаксис — строка или enum? **Строка** (как `Hint`/`WithTableHint`): словарь хинтов различается по
   провайдерам, enum его не покроет. Пустой/пробельный хинт отклоняется.
4. `pg_hint_plan` требует расширения; без него `/*+ ... */` — обычный комментарий (инертен).
   **Принято:** рендерим безусловно (как существующий `Hint`), поведение документировано.

## Статус реализации

- [x] Провайдерная матрица и решения зафиксированы (этот файл, §2/§8).
- [x] `JoinExpression.JoinHint`, `FromExpression.SubQueryHint`, `QueryCommand.TablesInScopeHints`.
- [x] Диалектные хуки: `SupportsJoinHints`/`SupportsSubQueryHints`/`SupportsTablesInScopeHints`/
      `SupportsInlineHints` + `MakeJoinKeyword` (SQL Server) и `MakeTablesInScopeHints` (SQL Server).
- [x] Поверхность билдера `WithJoinHint`/`WithSubQueryHint`/`WithTablesInScopeHint` + XML-док.
- [x] План-ключ (`JoinExpressionPlanEqualityComparer`, `FromExpressionPlanEqualityComparer`,
      `QueryPlanEqualityComparer`) и `CopyTo`/`CloneForCache`.
- [x] SQL-gen/негатив/план-кэш тесты без контейнеров; регресс `Hint`/`WithTableHint`/`WithIndex`.
- [x] Доки EN+RU, API-реестр, gap-analysis.

Итог: SQL Server — join-хинт внутри `JOIN` и tables-in-scope как `WITH (...)`; PostgreSQL/MySQL/MariaDB —
`/*+ ... */` для всех трёх вариантов; SQLite/ClickHouse/InMemory — `NotSupportedException`.
