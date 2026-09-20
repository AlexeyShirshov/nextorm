# WIP: `setseed(float)` (PostgreSQL)

> Статус: **реализовано** (`todo_postgres.md:65-70`, ветка `todo-pg`).

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_postgres.md:65-70`,
> `todo_phase2.md` → PostgreSQL — не заблокировано.

## Пункт и цель

- Пункт: **`setseed(float)`** — скалярный PostgreSQL-метод рядом с `random()`.
- Провайдер-источник: PostgreSQL; конструкция не имеет аналога с той же семантикой у других
  провайдеров (см. матрицу).
- Критерий приёмки: `SqlFunctions.Postgres.setseed(seed)` рендерится как `setseed(seed)`; вызов
  разрешён только на PostgreSQL (гейт), остальные провайдеры бросают `NotSupportedException`.

## Матрица «провайдер × форма» (шаг 1)

Заполнено по официальной документации СУБД.

| Провайдер | Форма | Поддержка | Источник |
| --- | --- | --- | --- |
| PostgreSQL | `setseed(float)` — задаёт seed для последующих `random()` в сессии, возвращает `void` | **да** | https://www.postgresql.org/docs/current/functions-math.html |
| SQL Server | `RAND(seed)` — возвращает `float` и задаёт seed; отдельного `setseed` нет, семантика пары «set + random» не выражается | — | https://learn.microsoft.com/sql/t-sql/functions/rand-transact-sql |
| MySQL | `RAND(N)` — задаёт seed и возвращает значение; `RAND()` продолжает последовательность, но отдельного `void`-метода нет | — | https://dev.mysql.com/doc/refman/8.4/en/mathematical-functions.html |
| MariaDB | `RAND(N)` — как в MySQL | — | https://mariadb.com/kb/en/rand/ |
| ClickHouse | `rand(seed)` — seed на уровне выражения, сессионного `setseed` нет | — | https://clickhouse.com/docs/en/sql-reference/functions/random-functions |
| SQLite | `random()`/`randomblob()` — seed не задаётся | — | https://www.sqlite.org/lang_corefunc.html#random |
| InMemory | скалярные `SqlFunctions` в in-memory не оцениваются | — | — |

**Единообразие провайдеров.** Только PostgreSQL имеет отдельный `void`-функционал установки seed,
после которого `random()` сессии воспроизводим. MySQL/MariaDB `RAND(N)` совмещает seed и возврат
значения, а SQL Server `RAND(seed)` возвращает значение при каждом вызове; это не та же абстракция
(нельзя отделить установку seed от получения значения). Конструкция остаётся на
`PostgresFunctions` под отдельным флагом `SupportsRandomSeed` (base `false`, PostgreSQL `true`) —
не под семейным `SupportsExtendedScalarFunctions`.

## Ближайший C#-аналог и уровень

- CLR-аналога нет (`Random(seed)` создаёт объект, а не настраивает глобальный генератор).
- Уровень **(b)**: метод на `PostgresFunctions` + ветка транслятора + флаг/хук.

## Диалектный план

- `ISqlDialect.SupportsRandomSeed` (base `false`), `PostgresDialect` → `true`.
- `ExtendedScalarFunctionTranslator`: ветка `PostgresFunctions.setseed` → `setseed(arg)` под гейтом.
- `MakeMathFunction` не используется: PostgreSQL рендерит `setseed(...)` базовым дефолтом, но гейт
  отдельный; ветка вызывает `SqlOperandTranslator.EmitFunction`.

## Публичный API

```csharp
public double? PostgresFunctions.setseed(double? seed);
```

Возвращает `double?`, потому что PostgreSQL-функция возвращает `void`; колонка всегда `NULL`, вызов
делается ради побочного эффекта. XML-doc это фиксирует.

## План тестов

- SQL-gen (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): `setseed(0.5)` в проекции.
- Rejection (`tests/nextorm.sqlite.tests`, `sqlserver`, `mysql`, `clickhouse`): `setseed` бросает.
- Прямой хук (`PostgresDialectTests`): `SupportsRandomSeed` = `true` у PostgreSQL.
- Интеграция (`PostgresSpecificTests`): реальный вызов `setseed` даёт `NULL` (void) и не падает.
- Базовая линия покрытия: снимается после сборки.

## Файлы доков/специй

- `docs/specs/roadmap/todo_postgres.md` (чекбокс), `docs/guide/11-scalar-functions.md` + RU,
  `docs/providers/postgres.md` + RU.
