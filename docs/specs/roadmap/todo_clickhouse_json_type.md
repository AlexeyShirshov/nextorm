# TODO: ClickHouse нативный тип `JSON` (`JSON_VALUE`/`JSON_QUERY`/`JSONAllPaths*`/`toJSONString`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.7.

## Пункт и цель

- Проблема: новый тип колонки ClickHouse `JSON` не поддержан: `JSON_VALUE`/`JSON_QUERY` над ним,
  `JSONAllPaths`/`JSONAllPathsWithTypes`, `toJSONString` отсутствуют. Замаплено только строковое
  JSON-семейство `JSONExtract*`/`visitParamExtract*`.
- Цель: API типа колонки для нативного `JSON` + перечисленные функции.
- Критерий приёмки: SQL-gen тесты на `JSON_VALUE`/`JSON_QUERY`/`JSONAllPaths*`/`toJSONString`;
  остальные провайдеры — `NotSupportedException`.

## Текущее состояние

- `SupportsTextJson` покрывает `JSONExtract*` над `String`; нативный `JSON`-тип не моделируется.
- Отдельного `TypeMapping`/ридера для `JSON`-колонки нет.

## Дизайн (черновик)

- CLR-представление: `string` (сырой JSON) либо `JsonDocument`/`JsonElement`-обёртка.
- Новый дизайн-объект `IJsonTypeFunctions` (ClickHouse-only) с `MakeJsonValue`/`MakeJsonQuery`/
  `MakeJsonAllPaths`/`MakeToJsonString` или `[SqlFunction]`-surface `ClickHouseFunctions.*`.

## Открытые вопросы

1. CLR-тип проекции для `JSON` (`string` vs `JsonDocument`).
2. Достаточно ли `[SqlFunction]`-UDF против полноценного `TypeMapping`.
3. Единый объект для `JSONExtract*` и нативного `JSON` или раздельно.

## Файлы к изменению

- `src/nextorm.clickhouse/*` (TypeMapping/ридер), `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
  `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs`.
- Тесты: `tests/nextorm.clickhouse.tests`, `tests/nextorm.integration.tests`.
- Доки EN+RU, gap-analysis.
