# TODO: коррелированные подзапросы в in-memory

> Вынесено из [`plan-correlated-subqueries.md`](plan-correlated-subqueries.md) §5 (фаза 6). SQL-провайдеры
> поддержаны (см. `plan-correlated-subqueries.md`); в in-memory корреляция закрыта **явной
> границей** — `NotSupportedException`, а не поддержкой. Источник в gap-анализе:
> `roadmap/sql-capabilities-gap-analysis.md` §4.1 («any correlated subquery on the in-memory provider
> throws `NotSupportedException`»).

## Пункт и цель

- **Фича:** исполнять коррелированный подзапрос (`EXISTS`/`IN`/`ANY`/`ALL` и скалярные терминалы
  `First`/`Single`/`*OrDefault`) в `InMemoryDataContext` **per-row**: для каждой внешней строки
  внутренний запрос выполняется с подставленными значениями внешних ссылок.
- **Критерий приёмки:** на in-memory коррелированный скаляр и коррелированный `exists` дают на
  каждую внешнюю строку тот же результат, что SQL-провайдер на тех же данных (depth 1).

## Текущее состояние (граница)

`IDataContext`/`InMemoryDataContext` корреляцию не поддерживают и корректно это заявляют:

- `DataContext/InMemoryQueryBuilder.cs:40-42` — при `queryCommand.OuterReferences is { Count: > 0 }`
  бросается `NotSupportedException("Correlated subqueries are not supported by the in-memory
  provider; run the query against a SQL provider.")`. Guard стоит **до** компиляции материализатора.
- `Visitors/CorrelatedQueryExpressionVisitor.cs` (`_forPrepare == false`, in-memory):
  - `exists`/`any`/`all` (`:119-183`) один раз выполняют `Any<TResult>(cmd)` при сборке
    материализатора — корреляция per-row невозможна;
  - `@in` (`:190-193`) — `throw new NotImplementedException()`;
  - `ReplaceQueryCommand` (`:449-452`) — `throw new NotImplementedException()` (скалярные терминалы).
- `OuterRefMarker` обрабатывается **только** в `Visitors/MemberTranslator.cs` (SQL-рендер); в
  in-memory-пути совпадений нет.
- `DataContext/InMemoryRowMaterializer.cs` (`RowMaterializerBuilder.Build`, многоколоночные проекции)
  и `DataContext/InMemoryConditionFactory.cs` (`ParamCollectorVisitor` + `ParamLocalSubstitutionVisitor`,
  `:34,54-74`) про маркеры не знают.

Итог: сейчас падает внятным `NotSupportedException` (не тихо неверные данные).

## Что нужно

1. Ввести обработку `OuterRefMarker<T>(idx).Ref` в in-memory-конвейере: при исполнении **внутреннего**
   запроса на каждую внешнюю строку подставлять значение внешнего выражения.
   - Механизм: при построении делегата row-материализатора внешней команды (для `exists`/`Any`/
     `AnyAsync` и скалярных терминалов) маркер заменяется на доступ к текущей внешней строке
     (`Expression` над `TEntity`), а не на константу.
2. Переработать ветки `CQEV` для `!_forPrepare`: `exists`/`any`/`all` (`:119-183`) перевести с разового
   `Any<TResult>(cmd)` на замыкание `(TEntity row) => inner(row).Any()`.
3. Скалярные терминалы: `ReplaceQueryCommand` при `!_forPrepare` (`:449-452`) — реализовать возврат
   скаляра через `GetPreparedQueryCommand`/`CreateEnumerator` с подстановкой внешней строки.
4. Многоколоночные проекции — через `RowMaterializerBuilder.Build`
   (`DataContext/InMemoryRowMaterializer.cs`), а не только `OneColumn`.
5. Условия внутренней команды — `InMemoryConditionFactory` должна уметь подставлять внешние маркеры
   (расширить `ParamLocalSubstitutionVisitor` или добавить `OuterRefSubstitutionVisitor`).
6. Снять guard `InMemoryQueryBuilder.cs:40-42` (или сузить его до реально неподдержанных случаев).

## Точки встраивания

| Файл | Роль |
|---|---|
| `DataContext/InMemoryQueryBuilder.cs:40-42` | снять/сузить guard `OuterReferences` |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:119-193,449-452` | ветки `!_forPrepare`: `exists`/`any`/`all`/`@in`/скаляр |
| `DataContext/InMemoryRowMaterializer.cs` | построение делегата с привязкой внешней строки |
| `DataContext/InMemoryConditionFactory.cs` | подстановка маркеров в предикат внутренней команды |
| `DataContext/InMemoryDataContext.cs` | прокидка внешней строки в исполнение внутреннего запроса |

## Тесты

- `tests/nextorm.core.tests/CorrelatedQueryInMemoryTests.cs` — сейчас фиксирует границу
  (`NotSupportedException`); заменить на позитивные сценарии.
- `tests/nextorm.core.tests/InMemoryTests.cs:231` — существующий некоррелированный `exists`; добавить
  коррелированные скаляр/`exists`/`in` depth 1 в `SELECT`/`WHERE`/`ORDER BY`.
- Паритет с SQL: те же данные прогнать через SQLite (`tests/nextorm.sqlite.tests`) и сверить результат.

## Fallback / осознанная граница

Если объём окажется чрезмерным — допустимо оставить in-memory как «SQL MVP + follow-up», но тогда
`InMemoryDataContext` **обязан** бросать понятный `NotSupportedException` на любой коррелированный
подзапрос (текущее поведение) и это должно быть задокументировано в
`docs/advanced/limitations.md` (+ `docs/ru/...`) и `docs/guide/06-subqueries.md`. Решение зафиксировать
до начала реализации.

## Критерии приёмки

- Коррелированный скаляр и `exists`/`in` depth 1 в in-memory исполняются per-row и совпадают с SQL.
- Некоррелированные запросы не деградируют (без лишних боксов/замыканий на горячем пути; сверить с
  `docs/specs/performance/benchmark-report.md`).
- Публичный API — только аддитивно (extend-only); корреляция выражается существующими терминалами
  `QueryCommand<T>`.
- `dotnet build nextorm.sln -c Debug` → 0 warnings / 0 errors; `tests/nextorm.core.tests` → Failed 0.

## Ссылки

- `plan-correlated-subqueries.md` §1.4, §5 (фаза 6), §9.
- [`guide/03-joins.md`](../guide/03-joins.md) § «Correlated APPLY / LATERAL» — коррелированный APPLY на SQL-провайдерах (реализован).
- `roadmap/sql-capabilities-gap-analysis.md` §4.1.
- `.opencode/skills/running-integration-tests/SKILL.md` — прогон интеграционных тестов.
