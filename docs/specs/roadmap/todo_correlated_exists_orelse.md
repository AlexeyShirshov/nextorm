# TODO: Коррелированный EXISTS справа от логических операторов (`||`, `&&`)

> Рабочий план (design RFC). Источник: README репозитория примеров `~/sources/linq2db-apps-nextorm`,
> раздел «Engine gaps surfaced in `nextorm 1.0.5-alpha`», пункт 4. Связано:
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md) §4 п.45.

## Пункт и цель

- **Проблема:** коррелированный `EXISTS` на правой стороне `||` (и аналогично `&&`) не даёт собрать
  предикат; подготовка бросает
  `The binary operator OrElse is not defined for the types 'System.Boolean' and 'System.Func<…>'`.
- **Цель:** коррелированные `EXISTS`/`IN`/`ANY`/`ALL` можно свободно комбинировать логическими
  операторами в `WHERE` (как уже заявлено для пункта 26 gap-analysis «Correlated scalar subqueries» —
  **Done**, но эта форма не покрыта).
- **Критерий приёмки:** SQL-generation тест: `x.Any(...) || y.Any(...)`, `A && B.Any(...)`,
  `!A.Any(...)` дают `EXISTS ... OR EXISTS ...` / `AND` / `NOT EXISTS`; `CommonTestSuite` на
  контейнерах; in-memory — либо поддержка глубины 1, либо явный `NotSupportedException` с понятным
  сообщением; регресс по существующим коррелированным подзапросам отсутствует.

## Падающая форма

- nopCommerce query 7 (`AclService.ApplyAcl`, `Any`/`EXISTS` + `Contains`).
- Обход в примере: подзапрос вычисляется заранее, результат применяется через `Contains`.

## Гипотеза и область

- `Visitors/PredicateTranslator.cs:325,404` собирает `AndAlso`/`OrElse` через `System.Linq.Expressions`
  до того, как коррелированное подзапрос-выражение сведено к скалярному `bool`
  (`Visitors/{CorrelatedQueryExpressionVisitor,WhereExpressionVisitor}.cs`, `OuterRefMarker.cs`).
  Нужно либо нормализовать плечо до `bool` заранее, либо собирать `EXISTS`-SQL напрямую, минуя
  `Expression.OrElse` с несовпадающими типами.

## Файлы к изменению

- `src/nextorm.core/Visitors/{PredicateTranslator,CorrelatedQueryExpressionVisitor,WhereExpressionVisitor}.cs`,
  `Visitors/TypeFacts.cs`, `OuterRefMarker.cs`
- `src/nextorm.core/DataContext/InMemoryCorrelatedSubqueryRewriter.cs` (если in-memory пойдёт по тому же
  пути)
- Тесты: `tests/nextorm.<p>.tests/SqlGenerationTests.cs` (6 провайдеров),
  `tests/nextorm.integration.tests/CommonTestSuite.CorrelatedSubqueries.cs` (+ core in-memory)
- Доки EN+RU, `sql-capabilities-gap-analysis.md` §4 п.45

## Открытые вопросы

1. Поддерживать ли in-memory эту форму (глубина 1) или ограничиться явным отказом?
2. Только `EXISTS`/`Any` или распространить на скалярные коррелированные подзапросы в булевых ветках?

## Источник

README портов, пункт 4; после закрытия — убрать/пометить его в README примеров.
