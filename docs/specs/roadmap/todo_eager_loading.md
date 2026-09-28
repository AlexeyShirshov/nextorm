# TODO: Eager loading графа (`LoadWith`/`Include`)

> Tracking issue: [#95](https://github.com/AlexeyShirshov/nextorm/issues/95).

> **Статус (2026-09-26).** Необходимый eager loading механизм **multi-result-set уже поставлен** (фаза 4
> [#70](https://github.com/AlexeyShirshov/nextorm/issues/70), issue #25 закрыт): батч проносит несколько
> наборов за один round trip — `BatchBuilder.AddQuery<TResult>` + `Execute`/`ExecuteAsync` →
> `BatchResult.Read<TResult>()`, гейт `ISqlDialect.SupportsBatch` (PostgreSQL, SQL Server, MySQL, MariaDB,
> SQLite; ClickHouse и in-memory — `NotSupportedException`). Это устраняет только инфраструктурный блокер.
> Сам eager loading (`LoadWith`/`Include`) на дату этого статуса ещё не был реализован; реализован позже —
> см. блок «Статус реализации» ниже (issue #107).

> **Статус реализации (28.09.2026, ветка `1.0.9-a`, issue #107).** Уровень-1 eager loading **поставлен**
> в двух режимах. По умолчанию — split-query: `EntityBuilder<TEntity>.LoadWith<TChild,TKey>(collection,
> childQueryFactory, parentKey, childKey)` даёт 2 round trip (родительский statement + дочерний
> `WHERE childKey IN (...)`), без N+1; родительские ключи чанкуются по 1000; дочерние коллекции
> сшиваются с родителями в памяти. Опционально `AsSingleQuery()` сводит всё к одной денормализованной
> команде `LEFT JOIN`: >1000 ключей без чанкового `IN`-списка, без молчаливого отката к split; собственный
> `Where` дочернего запроса встраивается в предикат соединения, глобальные фильтры применяются.
> Сшивают четыре терминала: `ToList`/`ToListAsync` и `ToArray`/`ToArrayAsync`; `ToHashSet`,
> `ToDictionary`, `First`/`FirstOrDefault`, `Single*`, `ToEnumerable`, `ToAsyncEnumerable` и `ToCommand`
> eager loading не выполняют, а `Any`/`Count` скалярны. Отмена проверяется между чанками и между
> спецификациями; предварительно отменённый single-query не выполняется.
> **Границы контракта (приняты, `1.0.9-a`).** Split — режим по умолчанию; opt-in `AsSingleQuery()`
> требует, чтобы родительский ключ был частью маппинга, а дочерний запрос сводился к `.Where(...)`
> (прочие формы дочернего запроса отклоняются). `LoadWith`/`AsSingleQuery` не компонуются с
> `Join`/`As`/`Select`/`ArrayJoin`/`Pivot`/`SelectMany`/`GroupJoin`; N-level eager loading не
> поддерживается.
> **Отложено + триггеры:** поддержка ранее отклонённых форм дочернего запроса и дочернего
> `.IgnoreFilters` в single-query; распространение общего eager-интеграционного набора на ClickHouse;
> follow-up по памяти/времени (single-query аллоцирует ~1.68x от split) — триггер: конкретный
> потребитель/регрессия. Вложенность/N-level — отдельное требование; сшивание за пределами `ToArray` —
> отдельный контракт семантики терминалов.
> **Долг по тестам:** один white-box тест сохранения режима дублирует поведенческий; в sqlite-тестах
> предполагается префикс параметра `$p`.
> Реализация — `src/nextorm.core/Builders/EntityBuilderEagerLoading.cs`; документация —
> [Eager loading](../../advanced/eager-loading.md). Тесты: core `EagerLoadingTests` +
> `EagerLoadingSingleQueryTests`, SQLite `EagerLoadingSqlGenerationTests` (split 2 round trip + `IN`,
> single-query ровно 1 команда), integration `CommonTestSuite.EagerLoading`.

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
