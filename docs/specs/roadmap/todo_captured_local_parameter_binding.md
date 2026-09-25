# TODO: Биндинг captured local в SQL-параметры (повторные ссылки)

> **Статус: реализовано (SHIPPED) в `1.0-b.1`.** Captured local, достижимый из финального SQL,
> регистрируется ровно один раз — включая повторные вхождения одного local и locals, живущие только
> внутри joined derived/subquery. Доки: `docs/guide/02-filtering-where.md`, `docs/guide/06-subqueries.md`
> (+RU); gap-analysis §4 п.43.

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps — verified on `nextorm 1.0.6-alpha`», пункт 1. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.43.

## Статус верификации (`nextorm 1.0.6-alpha`)

Воспроизводится харнессом `Gaps/` в `~/sources/linq2db-apps-nextorm` (`dotnet run --project Gaps`).

| Провайдер | Пункт 1 (повторная ссылка) | Пункт 2 (только в joined derived) |
|---|---|---|
| SQLite | **падает** (`Must add values for the following parameters: $needle`) | — |
| PostgreSQL | работает | — |
| MySQL | **падает** (`Parameter 'needle' has already been defined`) | — |
| SQL Server | **падает** (`The variable name '@needle' has already been declared`) | — |

- **Пункт 2/проблема B исправлен в 1.0.6-alpha** (captured local внутри join-подзапроса биндится) —
  отдельная работа не нужна. См. также §5 ledger.
- Остаётся **проблема A**, и только на SQLite/MySQL/SQL Server; PostgreSQL принимает повторное имя.

## Пункт и цель

- **Проблема A:** captured local, использованный более одного раза в `Where` (в т.ч. поверх
  join-проекции), попадает в SQL несколько раз, но значение не передаётся; команда падает с
  `Must add values for the following parameters`, либо (MySQL/SQL Server) повторный `@pN` объявляется
  дважды. На PostgreSQL тот же запрос проходит.
- **Цель:** любой captured local, достижимый из финального SQL, регистрируется как параметр ровно один
  раз (при повторе — одна и та же позиция), на всех SQL-провайдерах.
- **Критерий приёмки:** SQL-generation тест: один local, использованный ≥2 раз в `Where` (в т.ч. поверх
  `Join`), даёт корректные `@pN`; параметры зарегистрированы; один и тот же SQL на всех SQL-провайдерах;
  `CommonTestSuite` на контейнерах воспроизводит оригинальные запросы nopCommerce 5/6 и WoW 6.

## Падающие формы

- nopCommerce query 5 (`OrderService.GetOrderItemsAsync`) и query 6
  (`ProductService.GetCategoryFeaturedProductsAsync`) — фильтры по аргументу, использованному более
  одного раза поверх join-проекции.
- jube query 3/5/8 (`ActivationWatcherRepository.GetByDateRangeAscendingAsync`,
  `ApplicationLogEntryRepository.GetLastAsync`, `UserLogoutRepository.GetLastAsync`) — `.ToLower().Contains`
  по одному local в нескольких плечах предиката.
- WoW query 6 (`GetLinesCallingSmartTimedActionList`, `OR` + диапазонные предикаты) — тот же класс.

Обход в примерах: каждому captured local дают ровно одно обращение.

## Гипотеза и область

- Сбор/дедуп параметров (`Visitors/ParameterVisitors.cs`, `Visitors/TypedParamVisitor.cs`,
  `DataContext/ParamNameCache.cs`, `Query/DefaultParameterProvider.cs`) регистрирует параметр для
  первого узла, а повторное вхождение того же `Expression`/имени не доходит до набора `Parameter`
  либо перетирается при подготовке (`Query/QueryCommand.QueryPreparer.cs`).
- Провайдерная разница (только PostgreSQL проходит) указывает на рендер имени параметра: PostgreSQL
  дедуплицирует по имени, SQLite/MySQL/SQL Server — нет.

## Файлы к изменению

- `src/nextorm.core/Visitors/{ParameterVisitors,TypedParamVisitor,BaseExpressionVisitor,PredicateTranslator,CorrelatedQueryExpressionVisitor}.cs`
- `src/nextorm.core/Query/{DefaultParameterProvider,QueryCommand.QueryPreparer}.cs`, `Parameter.cs`,
  `DataContext/ParamNameCache.cs`, `DataContext/{SqlSourceRenderer,QueryPlanner}.cs`
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.*.cs`
- Доки EN+RU, `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.43

## Открытые вопросы

1. Один параметр на все вхождения одного local (reuse позиции) или отдельная позиция на вхождение —
   что предпочтительнее для план-кэша и драйверов?
2. Почему PostgreSQL уже проходит: полагаться на это нельзя (разные провайдеры должны давать один SQL).

## Источник

README портов, пункт 1; после закрытия — убрать/пометить его в README примеров.
