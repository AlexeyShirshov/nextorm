# TODO: Унификация провайдерных различий (поля дат, `CUBE`/`FULL JOIN`, NULL-семантика, `FOR JSON`/`FOR XML`)

> Рабочий план (design RFC). Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.15.

## Пункт и цель

- Проблема: принятые поля `date_add`/`date_trunc`/`date_diff` различаются по провайдерам
  (SQLite сворачивает `millisecond`/`quarter`, SQL Server отвергает `decade`/`century`/`millennium`
  для `date_trunc`), `CUBE`/`GROUPING SETS` и `FULL JOIN` отсутствуют на MySQL/MariaDB,
  NULL-семантика `GREATEST`/`LEAST` различается, часть фич (`FOR JSON`/`FOR XML`, hints) — на
  подмножестве провайдеров. Сейчас это документируется в `docs/providers/*.md`, а не унифицируется.
- Цель: решить, что можно свести к единому поведению (полифиллы/эмуляция), а что сознательно
  оставить провайдерным.
- Критерий приёмки: явная матрица «фича × провайдер» с решением (унифицировать/полифилл/оставить),
  реализованные полифиллы + тесты.

## Текущее состояние

- Расхождения зафиксированы в `docs/providers/*.md` и `docs/advanced/limitations.md`.
- Форматирование дат/чисел сознательно оставлено провайдерным `[SqlFunction]`-UDF: языки шаблонов
  (`.NET FORMAT`, PG `to_char`, `%`-шаблоны) несовместимы — единого `template` быть не может.

## Дизайн (черновик)

- Полифилл `FULL JOIN` на MySQL/MariaDB через `LEFT JOIN ... UNION ... RIGHT JOIN` (проверить план).
- `CUBE`/`GROUPING SETS` — эмуляция через `UNION ALL` (дорого; оценить).
- Спецификация NULL-семантики `GREATEST`/`LEAST` (док + опциональный полифилл).

## Открытые вопросы

1. Что унифицировать в коде, а что только документировать.
2. Цена полифиллов (план запроса, дублирование строк).
3. Единый `template`-аргумент для форматирования — окончательно закрыт как невозможный?

## Файлы к изменению

- `src/nextorm.core/DataContext/SqlBuilder.cs`, `Builders/BuiltinFunctionTranslator.cs`,
  `Dialect/*`, провайдерные `*Dialect.cs`.
- Тесты: SQL-gen + integration.
- Доки EN+RU (`docs/providers/*`, `limitations.md`), gap-analysis.
