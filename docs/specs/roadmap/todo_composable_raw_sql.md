# TODO: Raw SQL как композируемый источник (`FROM`/join/subquery)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.2.

## Пункт и цель

- Проблема: `WithSql`/`PrepareFromSql` заменяет запрос целиком; сырой SQL нельзя использовать как
  `FROM`-источник, присоединять (`Join`) или фильтровать/проецировать дальше. EF Core (`FromSql`) и
  linq2db это умеют.
- Цель: сделать сырой SQL композируемым источником: `ctx.FromSql(...).Where(...).Join(...)`.
- Критерий приёмки: SQL-gen тест — сырой SQL как primary `FROM` и как присоединённый источник;
  параметры именованные; провайдеры без поддержки бросают `NotSupportedException`.

## Текущее состояние

- `WithSql` отдаёт готовую команду, дальнейшая композиция отсутствует.
- Механизм внешней ссылки внутри `FROM` уже частично появился (коррелированный `APPLY`, `.nodes`), но
  для литерального SQL-источника API не заведено.

## Дизайн (черновик)

- Новый builder-метод `FromSql(string sql, object? parameters)` → `FromExpression` с `RawSqlSource`.
- SQL-рендер: подзапрос `(<sql>) AS alias`; алиасы колонок — из явного описания/`[Column]`-маппинга.
- Диалектный `SupportsRawSqlSource` (все провайдеры, кроме, возможно, ClickHouse —
  проверить `FROM (subquery)`).

## Открытые вопросы

1. Как объявлять схему результата (entity vs явная проекция vs `TableAlias`)?
2. Параметры: `params object[]` (как `WithSql`) или типизированный билдер?
3. Разрешать ли сырой SQL на both sides join и в CTE.

## Файлы к изменению

- `src/nextorm.core/Builders/EntityBuilderExtensions.cs`, `Expressions/FromExpression.cs`,
  `DataContext/SqlBuilder.cs`, `Dialect/ISqlDialect.cs`/`SqlDialectBase.cs`, провайдерные `*Dialect.cs`.
- Тесты: `tests/nextorm.*.tests/SqlGenerationTests.cs`, `tests/nextorm.integration.tests`.
- Доки EN+RU, `docs/advanced/limitations.md` (убрать из «Out of scope»), gap-analysis.
