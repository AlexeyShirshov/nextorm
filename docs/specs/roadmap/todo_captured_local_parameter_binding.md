# TODO: Биндинг captured local в SQL-параметры (повторные ссылки и вложенные подзапросы)

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps surfaced in `nextorm 1.0.5-alpha`», пункты 1–2. Каждый пункт воспроизводится в
> портированном запросе. Связано: [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md)
> §4 п.43.

## Пункт и цель

- **Проблема A (пункт 1 README):** captured local, использованный более одного раза в `Where` поверх
  join-проекции, попадает в SQL несколько раз, но значение не передаётся; команда падает с
  `Must add values for the following parameters`.
- **Проблема B (пункт 2 README):** captured local, живущий **только** внутри join-подзапроса
  (grouped derived subquery), не биндится вообще.
- **Цель:** любой captured local, достижимый из финального SQL, регистрируется как параметр ровно один
  раз (при повторе — одна и та же позиция), независимо от того, в каком источнике/подзапросе он
  встречается.
- **Критерий приёмки:** SQL-generation тест: один local, использованный ≥2 раз в `Where` поверх
  `Join`, и local, живущий только в joined derived subquery, дают корректные `@pN`; параметры
  зарегистрированы; один и тот же SQL на всех SQL-провайдерах; `CommonTestSuite` на контейнерах
  воспроизводит оригинальные запросы nopCommerce 3/5/6 и WoW 6.

## Падающие формы

- nopCommerce query 5 (`OrderService.GetOrderItemsAsync`) и query 6
  (`ProductService.GetCategoryFeaturedProductsAsync`) — фильтры по аргументу, использованному более
  одного раза поверх join-проекции (проблема A).
- WoW query 6 (`GetLinesCallingSmartTimedActionList`, `OR` + диапазонные предикаты) — тот же класс.
- nopCommerce query 3 (`SearchTermService.GetSearchTermsAsync`, grouped derived subquery joined back,
  order, paging) — проблема B.

Обход в примерах: каждому captured local дают ровно одно обращение; во втором случае опциональный
фильтр переносят на внешний запрос.

## Гипотеза и область

- Сбор/дедуп параметров (`Visitors/ParameterVisitors.cs`, `Visitors/TypedParamVisitor.cs`,
  `DataContext/ParamNameCache.cs`, `Query/DefaultParameterProvider.cs`) регистрирует параметр для
  первого узла, а повторное вхождение того же `Expression`/имени не доходит до набора `Parameter`
  либо перетирается при подготовке (`Query/QueryCommand.QueryPreparer.cs`).
- Для проблемы B visitor, вероятно, не обходит замыкания вложенного `QueryCommand`/derived-источника
  при join-проекции (`Visitors/CorrelatedQueryExpressionVisitor.cs`, `DataContext/SqlSourceRenderer.cs`).
- Два случая чинить одним заходом: общий конвейер «выражение → параметр → SQL».

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
2. Обход `Where` внутри derived-источников — общий механизм для скалярных/`EXISTS`-подзапросов или
   отдельный путь для grouped derived source?

## Источник

README портов, пункты 1–2; после закрытия — убрать/пометить их в README примеров.
