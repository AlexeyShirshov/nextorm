# TODO: Хинты запросов вне SQL Server (PostgreSQL `pg_hint_plan`, MySQL/MariaDB `/*+ ... */`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.16.

## Пункт и цель

- Проблема: statement-level `Hint(...)` рендерит только `SqlServerDialect` (`OPTION (...)`);
  PostgreSQL/MySQL/MariaDB отклоняют команду с хинтами через `NotSupportedException`, хотя у них есть
  собственные механизмы. Табличные хинты `WithTableHint(...)` тоже только в SQL Server, тогда как
  MySQL/MariaDB/SQLite умеют index hints / `INDEXED BY`.
- Цель: включить рендеринг statement-level хинтов там, где у провайдера есть механизм, оставив
  `SupportsQueryHints => false` только для движков без хинтов (SQLite, ClickHouse).
- Критерий приёмки: `Hint("...")` на PostgreSQL/MySQL/MariaDB рендерит нативный синтаксис; SQLite и
  ClickHouse по-прежнему бросают `NotSupportedException`; SQL-генерация и интеграционные тесты зелёные;
  покрытие не ниже предыдущего; доки EN+RU и матрицы обновлены.

## Матрица «провайдер × форма»

Statement-level хинты (заполнено по документации провайдеров, не по коду nextorm):

| Провайдер | Механизм | Нативный синтаксис | Позиция | Источник |
|---|---|---|---|---|
| SQL Server | да (реализовано) | `OPTION (hint, ...)` | в конце запроса | Microsoft Learn: `OPTION` clause |
| PostgreSQL | нет в ядре; опциональное расширение `pg_hint_plan` | `/*+ Hint(...) */` | сразу после `SELECT` | PostgreSQL docs (в ядре хинтов нет) + README `pg_hint_plan` |
| MySQL | да (5.7+) | `/*+ hint */` (optimizer hints) | сразу после `SELECT` | MySQL 8.0 Reference Manual: «Optimizer Hints» |
| MariaDB | да (10.2+) | `/*+ hint */` | сразу после `SELECT` | MariaDB Knowledge Base: «Optimizer Hints» |
| ClickHouse | нет комментарных хинтов; есть `SETTINGS` (уже exposed как `Settings`) | `SETTINGS k = v` | в конце | ClickHouse docs |
| SQLite | нет | — (нет комментарных хинтов) | — | SQLite docs |
| InMemory | — | — | — | нет SQL |

Табличные хинты:

| Провайдер | Механизм | Нативный синтаксис | Источник |
|---|---|---|---|
| SQL Server | да (реализовано) | `WITH (NOLOCK)` | Microsoft Learn: table hints |
| PostgreSQL | нет табличных хинтов; `pg_hint_plan` адресует отношения по имени | `/*+ IndexScan(t) */` | README `pg_hint_plan` |
| MySQL | index hints | `USE INDEX` / `FORCE INDEX` / `IGNORE INDEX` | MySQL manual: «Index Hints» |
| MariaDB | index hints + общие `/*+ */` | то же | MariaDB KB: «Index Hints» |
| SQLite | да | `INDEXED BY` / `NOT INDEXED` | SQLite docs: «The INDEXED BY Clause» |
| ClickHouse | `FINAL` / `PREWHERE` (уже exposed) | — | ClickHouse docs |
| InMemory | — | — | — |

## Единообразие провайдеров

- **Реализовать** statement-level хинты для PostgreSQL, MySQL и MariaDB: у всех есть форма, принимающая
  произвольную строку-хинт, а `Hint("...")` остаётся провайдерно-нейтральным носителем строки.
- **PostgreSQL зависимость:** `pg_hint_plan` — не часть ядра. Но `/*+ ... */` для сервера без расширения —
  обычный комментарий, поэтому рендер безвреден и не требует установленного расширения.
- **Gated off:** SQLite (нет синтаксиса) и ClickHouse (хинты выражаются отдельным `Settings`, который уже
  есть; смешивать с `Hint` не нужно).
- **Табличные хинты:** семантика `WITH (NOLOCK)` не переносится на index hints (`USE INDEX`/`INDEXED BY`
  влияют на план, но не на блокировки). Предлагается вынести в отдельный пункт/решение, а в рамках этого
  todo ограничиться statement-level `Hint(...)`.

## Ближайший C#-аналог и уровень реализации

- Нового C#-аналога нет: переиспользуется существующий fluent `Hint(params string[])`.
- Уровень — **dialect hook** (как tier (a), но не функция): переопределить `SupportsQueryHints` и
  `RenderQueryHints(sql, hints, maxRecursionOption)` в `PostgresDialect`, `MySqlDialect`
  (и, при различиях, `MariaDbDialect`). Нового публичного API не требуется.

## План диалекта

- `ISqlDialect`/`SqlDialectBase`: без изменений (`SupportsQueryHints => false`, `RenderQueryHints` — заглушка).
- `PostgresDialect`: `SupportsQueryHints => true`; `RenderQueryHints` вставляет `/*+ <hints> */` сразу после
  первого `SELECT`; `maxRecursionOption` игнорируется.
- `MySqlDialect`: `SupportsQueryHints => true`; `RenderQueryHints` вставляет `/*+ <hints> */` сразу после
  первого `SELECT`. `MariaDbDialect` наследует от `MySqlDialect`; переопределить только если набор
  хинтов/позиция отличается.
- Пустой список хинтов по-прежнему игнорируется; хинты участвуют в ключе плана — существующее поведение
  `SqlBuilder` менять не нужно.
- `maxRecursion` (CTE `option (maxrecursion n)`) остаётся SQL Server-специфичным; для PG/MySQL не рендерится.

## Публичный API

- Новых публичных членов нет: `Hint(...)` и `WithTableHint(...)` уже существуют.
- Если понадобится общий helper для комментарных хинтов — оставить `internal` в
  `SqlDialectBase`/утилите, без расширения публичной поверхности.

## План тестов

- Диалектные: `tests/nextorm.postgres.tests/PostgresDialectTests.cs` (сейчас `SupportsQueryHints.Should().BeFalse()`),
  `tests/nextorm.mysql.tests/MySqlDialectTests.cs`, `tests/nextorm.mariadb.tests/MariaDbDialectTests.cs` —
  перевернуть на `true` и добавить ассерты `RenderQueryHints(...)`.
- SQL-генерация: `tests/nextorm.postgres.tests/SqlGenerationTests.cs`
  (`QueryHint_ShouldThrowBecausePostgresHasNoQueryHints` → позитивный тест с `/*+ ... */`),
  `tests/nextorm.mysql.tests/SqlGenerationTests.cs` (аналогично).
- Негативные остаются: `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:400` и ClickHouse-диалект.
- Интеграция: `tests/nextorm.integration.tests/MySqlSpecificTests.cs:85` перевернуть; добавить проверку,
  что PG-запрос с `/*+ ... */` исполняется (комментарий безвреден) и возвращает строки.
- In-memory: хинты не поддерживаются — существующее поведение не трогать.
- Покрытие: замерить до/после; `coverage.settings.xml` включает `nextorm.postgres` (и `core`/`sqlite`/`sqlserver`),
  но не `mysql`/`mariadb`/`clickhouse` — PG-часть сдвинет число, MySQL/MariaDB — нет (зафиксировать явно).

## Файлы к изменению

- Код: `src/nextorm.postgres/PostgresDialect.cs`, `src/nextorm.mysql/MySqlDialect.cs`,
  `src/nextorm.mariadb/MariaDbDialect.cs` (при необходимости),
  `src/nextorm.core/DataContext/Dialect/*` (только если понадобится shared helper).
- Тесты: `tests/nextorm.{postgres,mysql,mariadb}.tests/*`, `tests/nextorm.integration.tests/MySqlSpecificTests.cs`.
- Доки EN+RU: `docs/guide/17-query-hints.md`, `docs/providers/{postgres,mysql,mariadb}.md`,
  `docs/advanced/limitations.md`; матрицы `docs/specs/comparison/{capability-matrix,linq2db-comparison}.md`.
- Спеки: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 (→ shipped), этот файл (удалить при
  шиппинге); `docs/specs/design/API-NAMING-REVIEW.md` — только если появится публичный API.

## Открытые вопросы

1. Табличные хинты: маппить ли `WithTableHint` на MySQL index hints / SQLite `INDEXED BY` (отдельный todo)
   или оставить SQL Server-only?
2. PostgreSQL: рендерить `/*+ */` безусловно (расчёт на `pg_hint_plan`, иначе no-op) — подтвердить.
3. MariaDB: минимальная версия 10.2 для optimizer hints — соответствует ли поддерживаемому диапазону.
4. Нужно ли нормализовать многострочные хинты (каждый хинт — отдельная строка комментария) или рендерить
   одной строкой через запятую.
5. ClickHouse `Settings` — оставить отдельным API (текущее решение) или унифицировать с `Hint`.
