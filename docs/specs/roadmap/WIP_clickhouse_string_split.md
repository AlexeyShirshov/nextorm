# WIP: ClickHouse — `string.Split` → `splitByChar`

- Пункт бэклога: `docs/specs/roadmap/todo_clickhouse.md:230` (остаток массивы-воркстрима),
  `docs/specs/roadmap/todo_phase2.md:37`.
- Целевой провайдер: ClickHouse. Модель запроса не меняется.
- Критерий приёмки: `x.String.Split(',')` (и однобайтовый строковый разделитель) внутри
  array-функции рендерится как `splitByChar(',', x)`; многобайтовый разделитель,
  `StringSplitOptions` != `None`, overload с `count` и несколько разделителей отклоняются
  `NotSupportedException`; прочие провайдеры отклоняют поверхность.

## Матрица «провайдер × форма»

Источники: PostgreSQL official function reference (`string_to_array`), Microsoft Learn
(`STRING_SPLIT`), MySQL 8.4 / MariaDB string functions, ClickHouse string/array functions
(`splitByChar`, `splitByString`), SQLite core functions.

| Провайдер | Форма | Примечание |
|---|---|---|
| PostgreSQL | `string_to_array(value, separator)` | уже подключено как array-операнд (`SqlOperandTranslator.AppendSplitOperand`) и на `PostgresFunctions.string_to_array`; новый хук не нужен |
| SQL Server | `STRING_SPLIT(value, separator)` | табличная функция (TVF), а не скалярный массив; как скалярный array-операнд `string.Split` не выражается — `—` |
| MySQL | `SUBSTRING_INDEX` | разбивает по одному вхождению, не возвращает массив — `—` |
| MariaDB | — | нет скалярной split-функции, возвращающей массив — `—` |
| ClickHouse | `splitByChar(separator, value)` (однобайтовый), `splitByString` (многосимвольный) | целевая форма; берём `splitByChar` |
| SQLite | — | нет split; эмулируется рекурсивным CTE — `—` |
| InMemory | — | in-memory не поддерживает array-операнды — `—` |

Вывод по единообразию: безусловно переносимой скалярной формы нет (SQL Server — TVF, MySQL/MariaDB
и SQLite без массива, у PostgreSQL свой хук). ClickHouse опт-ин через `SupportsStringSplit`;
многосимвольный разделитель не поддержан (ClickHouse-форма `splitByString` вне объёма пункта) и
документирован как ограничение.

## C#-аналог и уровень

`string.Split` (tier b): ветка в `StringFunctionTranslator` + диалектный хук. Возвращает `string[]`,
поэтому проецировать результат нельзя (нет row reader массивов) — только как операнд array-функции
(`length`, `arrayJoin`, …).

## Диалектный план

- `ISqlDialect.SupportsStringSplit` (default `false`), `MakeStringSplit(separator, value)` (base throw).
- `ClickHouseDialect` — флаг `true`, `MakeStringSplit` → `splitByChar(separator, value)`.

## Публичный API

Публичных типов/методов `SqlFunctions` не добавляется: используется существующий CLR `string.Split`.

## План тестов

- CH SQL-gen: `Split_ShouldUseSplitByChar`, `Split_StringSeparator_ShouldUseSplitByChar`,
  `Split_MultiCharSeparator_ShouldThrow`, `Split_RemoveEmptyEntries_ShouldThrow`.
- Хуки: `ClickHouseDialectTests.MakeStringSplit_ShouldRenderSplitByChar`, флаг в
  `CapabilityFlags_ShouldMatchClickHouse`, контракт `SupportsStringSplit`→`MakeStringSplit`.
- Rejection PG: `Split_UnsupportedByProvider_ShouldThrow`.
- Интеграция CH: `Split_ShouldCountParts` на реальном ClickHouse.

## Документация

`docs/providers/clickhouse.md` (+RU), `docs/guide/11-scalar-functions.md` (+RU),
`docs/providers/overview.md` (+RU), `docs/advanced/limitations.md` (+RU),
`docs/specs/roadmap/todo_clickhouse.md`, `docs/specs/roadmap/todo_phase2.md`,
`docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
