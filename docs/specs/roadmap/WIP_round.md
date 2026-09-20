# WIP: `round(double precision, int)` (PostgreSQL)

> Статус: **реализовано** (`todo_postgres.md`, ветка `todo-pg`). Хук и тесты добавлены, WIP закрыт.

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_postgres.md:71-76`,
> `todo_phase2.md` → PostgreSQL — не заблокировано.

## Пункт и цель

- Пункт: **`round(double precision, int)`**.
- Провайдер-источник: PostgreSQL.
- Проблема: `Math.Round(x, n)` транслируется в `round(x, n)` для всех диалектов
  (`MathFunctionTranslator`), но PostgreSQL определяет только `round(numeric, int)` и
  `round(double precision)` (1 аргумент). Над `double precision` сервер бросает
  `function round(double precision, integer) does not exist` — ошибка всплывает только на исполнении.
- Критерий приёмки: `Math.Round(doubleExpr, n)` на PostgreSQL даёт
  `round((doubleExpr)::numeric, n)`; `decimal`/целочисленный первый аргумент рендерится как раньше
  (`round(x, n)`); все остальные провайдеры сохраняют текущий вывод без изменений.

## Матрица «провайдер × форма» (шаг 1)

Заполнено по официальной документации СУБД (не по коду nextorm).

| Провайдер | Форма `round(x, n)` | Источник |
| --- | --- | --- |
| PostgreSQL | `round(numeric, integer)` — есть; `round(double precision, integer)` — **нет** (только 1-аргументный `round(double precision)`); `integer` неявно приводится к `numeric`, `double precision` — нет | https://www.postgresql.org/docs/current/functions-math.html |
| SQL Server | `ROUND(numeric_expression, length [, function])` — принимает любой числовой тип (`float`/`real`/`decimal`/`int`) | https://learn.microsoft.com/sql/t-sql/functions/round-transact-sql |
| MySQL | `ROUND(X, D)` — принимает любой числовой тип | https://dev.mysql.com/doc/refman/8.4/en/mathematical-functions.html |
| MariaDB | `ROUND(X, D)` — принимает любой числовой тип | https://mariadb.com/kb/en/round/ |
| ClickHouse | `round(x, N)` — для `Float*`/`Decimal*`/целых | https://clickhouse.com/docs/en/sql-reference/functions/rounding-functions |
| SQLite | `round(X, Y)` — динамическая типизация, любой числовой операнд | https://www.sqlite.org/lang_corefunc.html#round |
| InMemory | трансляции в SQL нет; `Math.Round` вычисляется CLR-движком | — (in-memory оценивает выражение напрямую) |

**Единообразие провайдеров.** Конструкция выразима на всех SQL-провайдерах (2-аргументный `round`
есть везде), поэтому `Supports*`-гейт не нужен. Различие только в PostgreSQL: перед вызовом
`round(numeric, int)` первый аргумент над плавающим типом оборачивается в `(...)::numeric`.
`decimal` (CLR) отображается в `numeric` и уже совместим; `int` неявно повышается до `numeric`.
SQL Server/MySQL/MariaDB/ClickHouse/SQLite рендерят `round(x, n)` как раньше (базовый дефолт).

## Ближайший C#-аналог и уровень

- C#-аналог: `Math.Round(double, int)` / `Math.Round(decimal, int)` — уже транслируется через
  `MathFunctionTranslator`.
- Уровень **(a)**: нативный CLR-конструкт + диалектный хук, без нового публичного API.

## Диалектный план

- `ISqlDialect.MakeMathFunction(string name, IReadOnlyList<string> args)` — существующий 2-арг.
  хук сохраняется (директивные вызовы из тестов).
- Новый аддитивный 3-арг. хук
  `MakeMathFunction(string name, IReadOnlyList<string> args, IReadOnlyList<Type> argTypes)`
  (те же аргументы + статические CLR-типы), базовый дефолт делегирует в 2-арг. версию.
- `SqlDialectBase`: `public virtual string MakeMathFunction(name, args, argTypes) => MakeMathFunction(name, args);`
- `PostgresDialect`: override — если `name == "round" && args.Count == 2` и `argTypes[0]` —
  `double`/`float` (с учётом `Nullable<>`), вернуть `round(({args[0]})::numeric, {args[1]})`; иначе
  `base.MakeMathFunction(name, args, argTypes)` (что сохраняет `ln` для `log`).
- `MathFunctionTranslator`: собирает `IReadOnlyList<Type>` из `node.Arguments[i].Type` и вызывает
  3-арг. версию.
- SQL Server/SQLite продолжают переопределять 2-арг. версию; база маршрутизирует в неё.

## Публичный API

- Новых публичных методов на `SqlFunctions.Sql` нет. Публичный добавляемый член интерфейса —
  перегрузка `ISqlDialect.MakeMathFunction` с `IReadOnlyList<Type>`.

## План тестов

- SQL-gen (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): `Math.Round(doubleExpr, 2)` →
  `round(((...)::numeric, 2)`; `Math.Round(decimal, 2)` → без приведения.
- Прямые хуки (`tests/nextorm.postgres.tests/PostgresDialectTests.cs`): 3-арг. форма с `typeof(double)`
  даёт приведение, с `typeof(decimal)` — нет; 2-арг. форма неизменна.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs`): `Math.Round(doubleExpr, 2)`
  на реальном PostgreSQL (и остальных провайдерах) возвращает ожидаемое значение.
- Другие провайдеры: существующие SQL-gen тесты `MathRound_ShouldUseRoundFunction` (SQL Server/SQLite)
  не меняются — проверка отсутствия регрессии.
- Базовая линия покрытия: снять после сборки.

## Файлы доков/специй

- `docs/specs/roadmap/todo_postgres.md` (чекбокс), `docs/specs/roadmap/sql-capabilities-gap-analysis.md`
  (если применимо), `docs/guide/11-scalar-functions.md` + RU, `docs/providers/postgresql.md` + RU.
