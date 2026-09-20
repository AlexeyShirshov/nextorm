# WIP: общий коррелированный скалярный подзапрос в проекции

> Статус: **реализовано** (core + провайдеры); остаток — ссылка на член join-проекции
> (`p.Item1.Id`), закрыт в этом изменении. Ветка `todo-mssql2`.

> Рабочий план по скиллу `implementing-todo-features`. Источник: `todo_mssql.md`
> («Отложено: сложное» → «Общий коррелированный скалярный подзапрос в проекции»),
> `todo_phase2.md` → SQL Server — не заблокировано; `plan-correlated-subqueries.md`.

## Пункт и цель

- Пункт: **общий коррелированный скалярный подзапрос в проекции** (`SELECT`/`WHERE`/`ORDER BY`),
  включая ссылку на внешний столбец, приходящий из **join-проекции** (`p.Item1.Id`).
- Провайдеры: все SQL-провайдеры; in-memory — явный `NotSupportedException`.
- Критерий приёмки: скалярный подзапрос, ссылающийся на столбец внешней строки (в том числе через
  элемент join-проекции), рендерится как `(select ... where inner.col = <outer-alias>.col ...)` и
  исполняется per-row; глубина > 1 и in-memory отклоняются явно.

## Статус до этого изменения

Механизм корреляции (`OuterRefMarker<T>`, `CorrelatedQueryExpressionVisitor`,
`QueryPlanEqualityComparer` с `OuterReferences`) и публичная поверхность (терминалы
`QueryCommand<T>.First/FirstOrDefault/Single/SingleOrDefault`,
`SqlFunctions.Sql.exists/@in/any/all`) уже существовали и были покрыты тестами
(`plan-correlated-subqueries.md`, `CommonTestSuite.CorrelatedQuery.cs`,
`tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1814`). Публичный API добавлять не требуется —
синтаксис выражается существующими терминалами (см. `plan-correlated-subqueries.md` §2 «Не-цели»);
дублировать поверхность запрещено границами скилла.

Оставался один незакрытый кейс из `plan-correlated-subqueries.md` §5 (фаза 2, п.4, «открытый риск»):
внешняя ссылка на **член join-проекции**. `ReplaceConstantsExpressionVisitor.VisitMember` заменяет
только звено от параметра (`p.Item1`), поэтому в дереве появляется `OuterRefMarker<T>(idx).Ref.Id`.
`MemberTranslator` не разворачивал цепочку `marker.Ref.<member>`: узел попадал в ветку
«closure-константа», где вычислялся `marker.Ref` (значение которого всегда `null` при сборке SQL) —
`NullReferenceException` (`MemberTranslator.cs:312`).

## Матрица «провайдер × форма» (шаг 1)

Заполнено по официальной документации СУБД; для SQL Server — T-SQL reference (Microsoft Learn).

| Провайдер | Коррелированный скалярный подзапрос | Ограничение строки | Источник |
| --- | --- | --- | --- |
| SQL Server | да, `(select ...)` со ссылкой на внешний алиас | `TOP(1)` в скалярной ветке | https://learn.microsoft.com/sql/t-sql/queries/select-clause-transact-sql |
| PostgreSQL | да | `LIMIT 1` | https://www.postgresql.org/docs/current/functions-subquery.html |
| MySQL | да | `LIMIT 1` | https://dev.mysql.com/doc/refman/8.4/en/scalar-subqueries.html |
| MariaDB | да (как MySQL) | `LIMIT 1` | https://mariadb.com/kb/en/subqueries/ |
| SQLite | да | `LIMIT 1` | https://www.sqlite.org/lang_expr.html#scalar_subquery |
| ClickHouse | да, скалярный подзапрос во внешнем контексте | без `LIMIT` (движок отдаёт одно значение) | https://clickhouse.com/docs/en/sql-reference/operators/in |
| InMemory | — нет per-row привязки внешней строки | — | `InMemoryQueryBuilder` → `NotSupportedException` при `OuterReferences.Count > 0` |

Форма внешней ссылки одинакова: ссылка на столбец внешней строки, в том числе через элемент
join-проекции (`p.Item1.Id` / `p.Item2.String`).

**Единообразие провайдеров.** Конструкция ANSI и выражается всеми SQL-провайдерами; различается
только хвост ограничения одной строки (`TOP`/`LIMIT`) — он уже инкапсулирован в диалектном
`MakePage`/`MakeTop`. Отдельный `Supports*`-флаг не нужен: ограничение относится к
**in-memory**, который гейтится существующим `NotSupportedException` в `InMemoryQueryBuilder`.
Новых `Make*`-хуков в этом изменении не требуется: цепочка `marker.Ref.<member>` разрешается
общим кодом `MemberTranslator` (алиас — по позиции элемента join-проекции, столбец — по
`[Column]`-разметке члена).

## Ближайший C#-аналог и уровень

- Аналог — обычная ссылка на член внешней лямбды (`it.Id`), уже поддержанная
  (уровень **(a)** — без нового публичного API). Добавленный кейс — ссылка на член элемента
  `Projection<T1,T2>` (`p.Item1.Id`); это тот же уровень (a): перевод в существующем
  `MemberTranslator`, новый публичный член не вводится.
- Публичная поверхность не меняется.

## Диалектный план

- `ISqlDialect`/`SqlDialectBase`/провайдерные диалекты — **без изменений**.
- Ядро: `Visitors/MemberTranslator.cs` — новый приватный метод
  `TryTranslateProjectionOuterReference`, вызываемый из `VisitMember` перед веткой
  `TableColumn`. Он распознаёт `OuterRefMarker<T>(idx).Ref.Member`, берёт из
  `IQueryRegistry.OuterReferences[idx]` сохранённое выражение (`p.Item1`), определяет алиас через
  `AliasFromProjectionVisitor` (позиция `ItemN` → `tN`) и печатает `<alias>.<column>`, где
  `<column>` — `[Column]`-имя члена. В `IsParamMode` текст не печатается (как и у остальных веток).

## Публичный API

- Новых публичных типов/методов **нет** (extend-only соблюдён). Изменение внутреннее
  (`MemberTranslator` — `internal`).

## План тестов

- SQL-gen:
  - `tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs` —
    `CorrelatedScalarOnJoinProjection_ShouldReferenceOuterAlias` (exact SQL),
    `CorrelatedExistsOnJoinProjection_ShouldReferenceTheSecondItemAlias` (алиас второго элемента).
  - `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs` —
    `CorrelatedScalarOnJoinProjection_ShouldReferenceOuterAlias`.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs`, все
  SQL-провайдеры с контейнерами: SQLite/SQL Server/PostgreSQL/MySQL):
  `CorrelatedScalarOnJoinProjection_ShouldEvaluatePerRow`,
  `CorrelatedExistsOnJoinProjection_ShouldEvaluatePerRow`.
- Отклонение: глубина > 1 и in-memory — уже покрыты
  (`NestedCorrelationDepth2_ShouldThrowNotSupported`, `CorrelatedQueryInMemoryTests`).
- Базовая линия покрытия: снимается после сборки (ядро входит в `coverage.settings.xml`).

## Файлы доков/специй

- `docs/guide/06-subqueries.md` + `docs/ru/guide/06-subqueries.md` — пример со ссылкой на
  join-проекцию.
- `docs/advanced/limitations.md` + RU — уточнение, что join-проекция поддержана (если строка
  упоминает обратное).
- `docs/specs/roadmap/todo_mssql.md`, `docs/specs/roadmap/todo_phase2.md` — чекбокс `[x]`.
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` — отметить закрытие остаточного кейса.
- `docs/specs/design/API-NAMING-REVIEW.md` — запись регистра (публичных изменений нет).
