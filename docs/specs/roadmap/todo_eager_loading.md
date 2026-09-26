# TODO: Eager loading графа (`LoadWith`/`Include`)

> Tracking issue: [#95](https://github.com/AlexeyShirshov/nextorm/issues/95).

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
