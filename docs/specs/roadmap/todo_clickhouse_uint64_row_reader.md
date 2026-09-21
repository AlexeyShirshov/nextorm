# TODO: ClickHouse — row reader `UInt64`

> Остаток ClickHouse. Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.4.
> **Статус: заблокировано** отсутствием row reader для `UInt64`.

## Проблема

`SelectExpression.GetDataRecordMethod` не знает `ulong`, а типизированные геттеры (`GetInt32`/
`GetInt64`) не читают нативный `UInt64`. Поэтому колонка `hits_v1.UserID` (`UInt64`) и нативные
результаты `count()`/`uniq()`/`uniqMerge` не материализуются без SQL-приведения: диалект оборачивает
только LINQ-агрегаты (`WrapCount`/`IUniqAggregateRenderer.Render`), а в `WithSql` приведение приходилось
дописывать вручную. Demo-запросы с нативным `UInt64` падают в рантайме.

## Что нужно

- Ветка `ulong` в `SelectExpression.GetDataRecordMethod`.
- Чтение `UInt64` в row reader (`GetFieldValue<ulong>`).
- SQL-gen + интеграционные тесты на реальном ClickHouse (колонка `UInt64`, нативные `count()`/`uniq()`).
- Docs EN+RU, `sql-capabilities-gap-analysis.md`.

## Источник

- `SelectExpression.GetDataRecordMethod`; колонка `hits_v1.UserID`.
