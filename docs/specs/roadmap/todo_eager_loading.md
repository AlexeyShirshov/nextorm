# TODO: Eager loading графа (`LoadWith`/`Include`)

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps — verified on `nextorm 1.0.6-alpha`», пункт 9. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.49, §6 workstream 13
> («Navigation properties — Out of scope»).

## Статус верификации (`nextorm 1.0.6-alpha`)

Харнесс `Gaps/` (`dotnet run --project Gaps`) проверяет отсутствие API рефлексией: у
`EntityBuilder<T>`/`QueryCommand<T>` нет `LoadWith`/`Include`, метаданных навигаций нет ни на одном
провайдере — гэп валиден на всех.

- OdataToEntity query 8 (`Orders?$expand=Customer,Items`) — граф собирается одним запросом на уровень
  и сшивается в памяти.
- RawDataAccessBencher `FetchGraphAsync` — то же: заголовки, детали и customer тремя запросами.

## Пункт и цель

- **Проблема:** linq2db умеет `LoadWith(x => x.Children)` (и EF — `Include`), т.е. жадную загрузку
  связанного графа одним запросом (JOIN/второй запрос). В nextorm навигаций нет вообще, поэтому
  `$expand`/graph-fetch — это N+1 по уровням и ручная сшивка.
- **Цель (если решим поддерживать):** минимальный eager-load уровня один: `LoadWith`/`Include` по
  явно заданной связи, с материализацией дочерней коллекции; без вывода навигаций из соглашений.
- **Критерий приёмки:** `From<Parent>().LoadWith(p => p.Children).ToListAsync()` даёт заполненные
  коллекции на всех SQL-провайдерах (JOIN + дедуп родителя или split-query); SQL-generation тесты;
  `CommonTestSuite` с реальными данными; документированное поведение по дедупликации и порядку.

## Контекст: решение «out of scope»

Workstream 13 (navigation properties) сознательно вне области: nextorm строит запросы явно, без
relationship-метаданных. Eager loading — надстройка над ними, поэтому его нет. Этот todo либо
пересматривает решение (минимальный `LoadWith` без соглашений), либо фиксирует его как «не планируется»
и закрывается в ledger.

## Гипотеза и область

- Нужны: (1) способ сослаться на связь без навигационного свойства (например, явный
  `LoadWith(parent => childQuery, (p, c) => p.Id == c.ParentId)`), (2) материализация родителя с
  дочерней коллекцией в `RowMapperFactory`/`QueryExecutor`, (3) корректный дедуп родителя при JOIN.
- Альтернатива (дешевле): `LoadWith` как синтаксический сахар над двумя запросами с `Contains` по
  ключам — без изменения материализатора; тогда границы (уровень 1, без вложенных) фиксируются явно.

## Файлы к изменению

- `src/nextorm.core/Builders/EntityBuilder.cs` (`LoadWith`/`Include`), `Query/QueryCommand*.cs`
- `src/nextorm.core/DataContext/{RowMapperFactory,QueryExecutor}.cs` (материализация коллекций)
- `src/nextorm.core/DataContext/Meta/*` (минимальные метаданные связи, если не через лямбду)
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs`, `CommonTestSuite.*.cs`, core in-memory
- Доки EN+RU, `sql-capabilities-gap-analysis.md` §4 п.49

## Открытые вопросы

1. Делаем ли вообще (пересмотр out-of-scope) или закрываем решением? От этого зависит объём.
2. Если делаем — одна ступень (parent→children) или произвольная вложенность?
3. JOIN+дедуп или split-query (два запроса)? Влияет на семантику `Page`/`Limit`.

## Источник

README портов, пункт 9; после закрытия/решения — убрать/пометить в README примеров.

## Провайдерная матрица (шаг 1 скилла)

Eager loading уровня один **не является SQL-конструкцией провайдера**: он раскладывается на
примитивы, которые уже есть у каждого диалекта, и именно поэтому провайдерной работы здесь нет.
Матрица фиксирует, каким нативным обликом каждый провайдер выражает каждую из трёх возможных
стратегий. Проверялось по документации самих провайдеров (не по коду nextorm).

| Провайдер | (a) JOIN + дедуп/группировка родителя | (b) Split-query: `WHERE fk IN (@keys)` | (c) Correlated apply/LATERAL (one-query-per-parent) | Источник (нативный облик) |
|---|---|---|---|---|
| PostgreSQL | `JOIN` + клиентская дедупликация либо `LEFT JOIN LATERAL`; `GROUP BY`/`array_agg` тоже выразимо | `IN (...)` / `= ANY(@array)` | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` | PostgreSQL 17: [Table Expressions / LATERAL](https://www.postgresql.org/docs/17/queries-table-expressions.html), [Subquery Expressions (`IN`/`ANY`)](https://www.postgresql.org/docs/17/functions-subquery.html) |
| SQL Server | `JOIN` + клиентская дедупликация; `OUTER APPLY` как агрегирующий источник | `IN (...)` | `CROSS APPLY` / `OUTER APPLY` | Microsoft Learn: [FROM (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/queries/from-transact-sql), [IN (Transact-SQL)](https://learn.microsoft.com/sql/t-sql/language-elements/in-transact-sql) |
| MySQL | `JOIN` + клиентская дедупликация либо `LEFT JOIN LATERAL` (8.0.14+) | `IN (...)` | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` (8.0.14+) | MySQL 8.4: [JOIN](https://dev.mysql.com/doc/refman/8.4/en/join.html), [Lateral Derived Tables](https://dev.mysql.com/doc/refman/8.4/en/lateral-derived-tables.html) |
| MariaDB | `JOIN` + клиентская дедупликация либо `LEFT JOIN LATERAL` | `IN (...)` | `CROSS JOIN LATERAL` / `LEFT JOIN LATERAL ... ON true` | MariaDB: [JOIN Syntax](https://mariadb.com/kb/en/join-syntax/), [Lateral Derived Tables](https://mariadb.com/kb/en/lateral-derived-tables/) |
| SQLite | `JOIN` + клиентская дедупликация; `LEFT JOIN LATERAL` отсутствует | `IN (...)` | — (нет `LATERAL`/`APPLY`; nextorm гейтит `SupportsApply`) | SQLite: [SELECT](https://sqlite.org/lang_select.html) (only comma/`JOIN`, no `LATERAL`) |
| ClickHouse | `JOIN` + клиентская дедупликация; `ARRAY JOIN`/`LIMIT BY` для частичных форм | `IN (...)` (в т.ч. по подзапросу) | — (nextorm гейтит `SupportsApply`; коррелированный `APPLY` недоступен) | ClickHouse: [JOIN](https://clickhouse.com/docs/en/sql-reference/statements/select/join), [IN operators](https://clickhouse.com/docs/en/sql-reference/operators/in), [ARRAY JOIN](https://clickhouse.com/docs/en/sql-reference/statements/select/array-join) |
| InMemory | LINQ-to-objects `Join` (делегат) + дедупликация | LINQ-to-objects `Contains` по ключам | неприменимо (нет SQL; коррелированные формы in-memory ограничены) | Реализация `InMemoryDataContext`/`InMemoryJoin` (делегатное исполнение) |

**Единообразие провайдеров.** Ни одна ячейка не требует нового `Supports*`/`Make*`: join, `IN` и
(где есть) lateral уже полностью обеспечены диалектами. Единственная разница — стратегия (c) не
доступна на SQLite/ClickHouse, но её можно заменить стратегией (b), так что и она не создаёт
провайдерного пробела. Следовательно, отсутствие eager loading — **не provider-parity gap**, а
следствие отсутствия связи как объекта модели: нечего передать в `LoadWith`/`Include`, пока нет
навигационных свойств (workstream 13, §4 п.49). Матрица выше поэтому пуста от «провайдерных
гейтов» и заполняется только нативными формами.

## Решение (Вариант B — не планируется до навигаций)

Принято решение **не реализовывать** `LoadWith`/`Include` (ни level-1, ни graph) до появления
навигационных свойств. Обоснование:

1. **Нет связи как объекта модели.** Критерий приёмки todo — `From<Parent>().LoadWith(p => p.Children)` —
   требует `p.Children`. Навигационных свойств и метаданных связей в nextorm нет (workstream 13
   сознательно out of scope), поэтому такую подпись выразить нечем. Селекторная вариация
   (`LoadWith(parent => childQuery, (p, c) => p.Id == c.ParentId)`) — это уже другой API и другая
   семантика, не совпадающая с EF `Include`/linq2db `LoadWith`; пользователь, пришедший из них,
   всё равно ожидал бы `p.Children`.
2. **Селекторный level-1 — это новая подсистема, а не аддитивный срез.** Он требует: нового
   builder/result-типа с вложенной коллекцией (проекции nextorm — плоские `QueryCommand<TResult>`,
   `Select` отвергает сущность целиком), нового двухзапросного протокола (keyset родителя + дочерний
   `IN`), ститчера и материализатора коллекций, in-memory-эквивалента, учёта `Page`/`Limit` в
   семантике, а также нового ключа плана. Это затрагивает `QueryExecutor`, `ResultsEnumerator`,
   `RowMapperFactory`/`RowMaterializerBuilder`, `QueryPlanner` и кэш планов — то есть ядро, а не
   одну ветку. В одну сессию это не влезает без риска для существующей модели.
3. **Семантика дедупликации/порядка не определена и не нужна, пока нет связи.** JOIN-стратегия
   дублирует родителя (нужен дедуп), split-query меняет смысл `Page`/`Limit` (пагинация родителя
   против дочерней выборки). Обе требуют явных контрактов, которые в модели без связей лишены
   смысла.
4. **Рабочие паттерны уже есть и документированы.** Связанная загрузка выражается явно:
   `From<Parent>().Join(...).Select(...)` в именованный тип ([Joins](../../guide/03-joins.md),
   [Subqueries](../../guide/06-subqueries.md)) либо два отдельных запроса + `Dictionary`-сшивка;
   коррелированные подзапросы/`EXISTS` покрывают точечные случаи. Регресса нет.
5. **Стоимость API выше пользы.** Полу-`Include` без навигаций хуже, чем явные join'ы: он выглядит
   как стандартный eager loading, но таковым не является. Оставляем «не поддерживается» и
   фиксируем осознанное решение.

## Статус реализации

- **Статус:** PARTIAL — Вариант B (зафиксировано «не планируется до появления навигаций»).
- **Что сделано:** провайдерная матрица (выше), фиксация решения, перенос «вне области» в
  `docs/advanced/limitations.md` (+ RU), обновление
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.49 → §5 (ledger, closed-by-decision),
  workstream 13 в §6 и `docs/specs/design/API-NAMING-REVIEW.md`.
- **Что отложено:** сама реализация `LoadWith`/`Include`, метаданные связей и двухзапросный
  материализатор — до пересмотра workstream 13 (навигационные свойства).
- **Публичный API:** не менялся.
- **Build:** `dotnet build nextorm.slnx -c Release -m:2` — 0/0 (изменений кода нет).
