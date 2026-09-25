# TODO: PostgreSQL range-типы и `Overlaps` (`&&`)
> Tracking issue: [#66](https://github.com/AlexeyShirshov/nextorm/issues/66).

> **Статус: реализовано (1.0-b.1, 25.09.2026).** Дизайн-ревью ниже разобран полностью:
> `Range<T>` выражает unbounded/empty через `LowerInfinite`/`UpperInfinite`/`IsEmpty` (+ `default` ==
> empty); PG-only рендер в `PostgresRangeSqlTranslator` без новых `MakeRange*` в `ISqlDialect`,
> `SupportsRanges` — DIM `=> false`; read-хук — `PostgresDataContext.MapColumnExpression` с
> `GetFieldValue<NpgsqlRange<T>>` и явной конвертацией; имена — SQL-токены (`overlaps`,
> `range_contains`, `range_contained_by`), поверхность static; `PostgresFunctions` запечатан; in-memory
> поддерживает `overlaps`/`range_contains`/`range_contained_by` + `lower`/`upper`/`isempty`/
> `lower_inc`/`upper_inc`/`lower_inf`/`upper_inf` (остальные операторы и конструкторы — только SQL);
> round-trip `Range<T>` ↔ `NpgsqlRange<T>` для int4/int8/num/ts/tstz/date.
> **Отложено (фаза 2):** in-memory вычисление `range_union`/`range_intersection`/`range_difference`/
> `range_adjacent`/позиционных операторов и конструкторов; multirange; эмуляция парой колонок на
> не-PG провайдерах. Документация: `docs/guide/provider-specific/postgresql.md` (+RU),
> `docs/providers/postgres.md` (+RU), `docs/advanced/api-reference.md` (+RU). Реализация:
> `src/nextorm.core/Query/Range.cs`, `src/nextorm.core/Query/SqlFunctions.Postgres.cs`,
> `src/nextorm.core/Visitors/PostgresRangeSqlTranslator.cs`,
> `src/nextorm.postgres/{PostgresDialect,PostgresDataContext,PostgresRange}.cs`.

> Рабочий план (design RFC). Источник — **G11** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md):
> `linq2db#4562` (PG `Overlaps`, range `&&`) и shipped `Sql.Row.Overlaps` (6.5.0).
> PG-only поверхность. Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** range-типы PostgreSQL (`int4range`/`int8range`/`numrange`/`tsrange`/`tstzrange`/`daterange`)
  как значения колонок/параметров и их операторы: прежде всего `&&` (overlap), а также
  containment `@>`/`<@`, позиционные `<<`/`>>`/`&<`/`&>`, смежность `-|-`, union `+`,
  intersection `*`, difference `-`, плюс функции (`lower`/`upper`/`isempty`/`lower_inc`/`upper_inc`/
  `lower_inf`/`upper_inf`) и конструкторы.
- **Критерий приёмки:** `From<T>()` читает range-колонку, `InsertInto`/`Update` пишут range-параметр;
  `Where(x => SqlFunctions.Postgres.overlaps(x.During, range))` и предикаты containment транслируются
  в корректный SQL; unbounded/empty и inclusive/exclusive границы сохраняются; in-memory ведёт себя как
  PG-семантика (для `overlaps`/`range_contains`/`range_contained_by` и функций проверки).

## 2. Почему это нужно

1. **Range — идиоматичный способ моделировать интервалы** (бронирования, периоды действия, тарифные
   диапазоны) и главный кейс — «пересекается ли бронь с интервалом» (`&&`), иначе выражаемый
   громоздким `lower < upper AND upper > lower`.
2. linq2db закрыл это в 6.5.0 (`Sql.Row.Overlaps`), у nextorm нет ни range-типов, ни операторов.
3. Смежно с [Duration-колонками](../../guide/26-duration-columns.md) (типы длительностей) и
   точностью длительностей, но это **другой** тип — диапазон значений, а не длительность.

## 3. Текущее состояние (проверено по коду)

| Слой | Где | Сейчас |
|---|---|---|
| Публичная поверхность | `src/nextorm.core/Query/SqlFunctions.Postgres.cs` | range-функций/типов нет |
| Диалект | `src/nextorm.postgres/PostgresDialect.cs` | range-хуков нет |
| Тип-маппинг | `PostgresDataContext.MapColumnExpression` (PG-провайдерский шов) | нет ветки для range-типа; core `SelectExpression.GetDataRecordMethod` не меняется |
| Зависимости | `nextorm.core.csproj` | Npgsql **не** подключён (только в `nextorm.postgres`) |
| Моделирование | — | ближайшее — `TemporalKind`/`TemporalClause` (system-time, SQL Server/PG), но это про `FOR SYSTEM_TIME`, не про range-типы |

Ключевое ограничение: `NpgsqlRange<T>` живёт в Npgsql, а `SqlFunctions.Postgres` — в `nextorm.core`,
куда Npgsql тянуть нельзя. Значит нужен provider-agnostic CLR-представитель range.

## 4. Матрица провайдеров

Источник: PostgreSQL 18 §8.17 (rangetypes) и §9.19 (functions-range); остальные — их собственная
документация типов.

| Провайдер | native range | Форма | Источник |
|---|---|---|---|
| PostgreSQL | `int4range`, `int8range`, `numrange`, `tsrange`, `tstzrange`, `daterange` + multirange | `lower/upper` bounds, `[)`/`()`/`[]`/`(]`, `empty`, unbounded; операторы `&&`/`@>`/`<@`/`<<`/`>>`/`&<`/`&>`/`-|-`/`+`/`*`/`-`; конструкторы `int4range(a,b[,bounds])` и т.д. | PG 18 §8.17, §9.19 |
| SQL Server | — | нет range-типа; выражается парой колонок (`from`/`to`) | MS Learn date/time types |
| MySQL | — | нет range-типа | MySQL type ref |
| MariaDB | — | нет range-типа | MariaDB type ref |
| ClickHouse | — | нет range-типа (есть `Tuple`/массивы, но без range-семантики) | ClickHouse types |
| SQLite | — | нет range-типа | sqlite.org |
| InMemory | — | нет нативного типа; можно моделировать `NextORM.Core.Range<T>` в памяти | — |

**Единообразие:** поверхность **PG-only**; SQL-провайдеры кроме PostgreSQL гейтятся
`SupportsRanges => false` с понятным `NotSupportedException`, а **InMemory поддерживает подмножество**
(`overlaps`/`range_contains`/`range_contained_by` и `lower`/`upper`/`isempty`/`lower_inc`/`upper_inc`/
`lower_inf`/`upper_inf`) в CLR — поэтому гейт `SupportsRanges` его не отключает. Эмуляция парой колонок
на прочих провайдерах — отдельный, более крупный workstream, вне этого todo; multirange — фаза 2.

## 5. Ближайший CLR-аналог и тир

- Ближайший аналог — `NpgsqlRange<T>` (Npgsql), но он недоступен в core.
- Тир **(b)**: новый provider-agnostic value-type `NextORM.Core.Range<T>` (только generic — не-generic
  `Range` дал бы `CS0104` с `System.Range` и боксинг) + `PostgresFunctions` методы + PG-only рендер в
  `PostgresRangeSqlTranslator`; `[SqlFunction]` не подходит (нужны операторы/границы).

## 6. Дизайн и публичный API

```csharp
public readonly struct Range<T> where T : struct, IComparable<T>
{
    public Range(T lower, T upper);                                                        // [lower, upper)
    public Range(T lower, T upper, bool lowerInclusive, bool upperInclusive);
    public Range(T lower, T upper, bool lowerInclusive, bool upperInclusive, bool lowerInfinite, bool upperInfinite);
    public T? Lower { get; }              // null при unbounded/empty
    public T? Upper { get; }
    public bool LowerInclusive { get; }   // false при unbounded/empty
    public bool UpperInclusive { get; }
    public bool LowerInfinite { get; }    // default(Range<T>) == Empty
    public bool UpperInfinite { get; }
    public bool IsEmpty { get; }
    public static Range<T> Empty { get; }
}
```

- `PostgresFunctions` (static-поверхность; имена — SQL-токены в lower_snake, чтобы не конфликтовать с
  полнотекстовым `contains`): `overlaps`, `range_contains(range, value)` / `range_contains(outer, inner)`,
  `range_contained_by`, `range_union`/`range_intersection`/`range_difference`, `range_adjacent`,
  `range_strictly_left_of`/`range_strictly_right_of`, `range_not_extend_right_of`/`range_not_extend_left_of`,
  `lower`/`upper`/`isempty`/`lower_inc`/`upper_inc`/`lower_inf`/`upper_inf`, конструкторы
  `int4range`/`int8range`/`numrange`/`tsrange`/`tstzrange`/`daterange` (с перегрузкой `string bounds`),
  `empty_range<T>()`. Класс `sealed`.
- Диалект: DIM `ISqlDialect.SupportsRanges => false` (+ `SqlDialectBase` virtual); **никаких**
  `MakeRangeConstructor`/`MakeRangeOperator` — весь PG-рендер в `PostgresRangeSqlTranslator`.
- Тип-маппинг: `nextorm.postgres` конвертирует `Range<T>` ↔ `NpgsqlRange<T>` на параметре
  (`CreateParam`, явный `NpgsqlDbType`) и на чтении (`PostgresDataContext.MapColumnExpression`,
  `GetFieldValue<NpgsqlRange<T>>` + явная конвертация, без `GetValue`/`Convert`).
- In-memory: CLR-семантика пересечения/включения/проверок границ (union/intersection/difference,
  позиционные и смежные операторы, конструкторы — только SQL; `NotSupportedException`).

## 7. Этапы внедрения

1. ✅ `Range<T>` в ядре + `PostgresFunctions.overlaps`/`range_contains`/`range_contained_by` + SQL-gen тесты.
2. ✅ Read/write range-колонки в `nextorm.postgres` (`NpgsqlRange<T>` ↔ `Range<T>`), конструкторы, функции
   и полный набор операторов.
3. ⏳ Фаза 2: in-memory для union/intersection/difference, позиционных и смежных операторов и
   конструкторов; multirange; `min`/`max`; эмуляция парой колонок на не-PG провайдерах.

## 8. План тестов

- SQL-gen (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): `overlaps` → `&&`, `@>`/`<@`,
  `insert` с range-параметром, конструкторы с `[)`/`[]`/unbounded/empty.
- Диалект: `PostgresDialectTests` — `SupportsRanges`, `MakeTypeName` для шести типов, `CreateParam`.
- Gate: `tests/nextorm.sqlite.tests/PostgresRangeRejectionTests.cs` — не-PG SQL-провайдер бросает
  `NotSupportedException`.
- Интеграция `PostgresSpecificTests.cs`: round-trip `Range<T>`↔range-колонка; `overlaps`-фильтр на таблице
  резервирований; empty/unbounded/inclusive-границы.
- Core/in-memory: `tests/nextorm.core.tests/RangeTests.cs` (value-семантика) и
  `InMemoryScalarFunctionsTests` (`overlaps`/`range_contains`/проверки границ в CLR).

## 9. Открытые вопросы (решены)

1. `Range<T>` — **generic в ядре** (`NextORM.Core`); Npgsql в core не тянется (конвертация — в
   `nextorm.postgres`).
2. Multirange — **фаза 2** (в этот инкремент не входит).
3. Имя — `overlaps` (SQL-токен `&&`); `Overlapping`/`Intersects` отклонены, `Contains` отклонён из-за
   коллизии с полнотекстовым `contains`. Форма API — **static**.
4. `range_contains(value)` и `range_contains(Range)` — **перегрузки одного имени**; containment наружу —
   `range_contained_by`.
5. Эмуляция «пара from/to» — **по-прежнему нет**; только PG + in-memory-подмножество.

## 10. Файлы к изменению

- Новое: `src/nextorm.core/Query/Range.cs`, `src/nextorm.core/Visitors/PostgresRangeSqlTranslator.cs`
  (или ветка в `AdvancedScalarFunctionTranslator`), PG-специфичный read-хук в `nextorm.postgres`.
- Правки: `src/nextorm.core/Query/SqlFunctions.Postgres.cs` (или новый partial),
  `src/nextorm.postgres/PostgresDialect.cs`, `PostgresDataContext.cs`,
  `SelectExpression`/`RowMapperFactory` (provider-хук).
- Доки: `docs/guide/provider-specific/postgresql.md` (+RU), `docs/providers/postgres.md` (+RU),
  `docs/advanced/api-reference.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **дизайн-нездоров — 2 блокера** (невыразимость unbounded; «PG-only гейт vs InMemory»).

- **[TYPE] 🔴** `Range<T>` не выражает unbounded: `:73` `Range(T? lower, T? upper, …)`, `:76-77` `T? Lower/Upper`. Для `int` `T?`==`int`, `default(T)`=0 неотличим от реальной границы → критерий `:18` («unbounded/empty … сохраняются») недостижим. Fix: `T Lower/Upper` + explicit `bool LowerInfinite`/`UpperInfinite` + `IsEmpty` (как `NpgsqlRange<T>`), инвариант в конструкторе.
- **[LSP] 🔴** Гейт PG-only противоречит InMemory: `:57` `SupportsRanges => false` + `NotSupportedException`, но `:93,108` и критерий `:18` требуют in-memory `Overlaps`/`Contains`. Fix: либо явно объявить поддержку `Range<T>` в InMemory (тогда он не гейтится), либо убрать in-memory из критерия.
- **[DIP]/[ISP] 🟡** Развилка «`MakeRange*` в диалекте vs PG-визитор» (`:89-90`): единственный потребитель — PostgreSQL, значит инвариант 1 и долг F12 запрещают новые `MakeRangeConstructor`/`MakeRangeOperator` в `ISqlDialect`. Fix: PG-only рендер; `SupportsRanges` — DIM `=> false` (образец `ISqlDialect.cs:314`), не abstract.
- **[LSP]/[TYPE] 🟡** Неверный шов для read-хука: `:35,124` называют core `SelectExpression.GetDataRecordMethod` (`:91-176`), но core не знает `NpgsqlRange<T>`; провайдерский шов — `DataContext.MapColumnExpression` (`DataContext.cs:269`, override `SqlServerDataContext.cs:72`). Fix: переопределить `PostgresDataContext.MapColumnExpression`.
- **[PERF] 🟡** Если PG-хук пойдёт через `IDataRecord.GetValue` + `Convert` (как `SqlServerDataContext.cs:83-85`) — боксинг `NpgsqlRange<T>` на каждую строку. Fix: `GetFieldValue<NpgsqlRange<T>>` + явная конвертация.
- **[OCP]/[DRY] 🟡** Имена расходятся с конвенцией файла: `:84-86` `Overlaps`/`Contains`/`ContainedBy` (PascalCase) vs SQL-токены (`array_overlaps` `SqlFunctions.Postgres.cs:55`, `json_contains` `:162`); голый `Contains` сталкивается с full-text (`SqlFunctions.cs:400`). Fix: `overlaps`, `range_contains`, `range_contained_by`.
- **[SRP] 🟡** Static vs instance не сведены: критерий `:17` требует `x.During.Overlaps(range)` (instance), §6 `:84` — static `Overlaps<T>(Range<T>, Range<T>)`. Fix: зафиксировать один путь (рекомендуется static).
- **[TYPE] 🟡** Вариант не-generic `Range` (`:64`) даст `CS0104` с `System.Range` + boxing. Fix: только generic `Range<T>`.
- **[DRY] 🟡** Сигнатуры §6 некомпилируемы: `:88` `bool isempty(Span)` (нет типа-аргумента), `T? lower<T>(Range<T>)` повторяет unbounded-проблему. Fix.
- **[TYPE] ℹ️** Value-семантика `Range<T>`: нужен `IEquatable<Range<T>>` + `==`/`GetHashCode` (или `readonly record struct`) и `in`-параметры. Deferred.
- **[SRP] ℹ️** `:121` ссылается на несуществующий `AdvancedScalarFunctionTranslator`; есть `ExtendedScalarFunctionTranslator`/`ScalarFunctionTranslator`/`CrossProviderScalarTranslator`. Fix.
- **[TYPE] ℹ️** `PostgresFunctions` — лист, но `public class` (`SqlFunctions.Postgres.cs:12`). Fix: `sealed` при добавлении членов. Ratio по `src`: 164 sealed / 68 unsealed (~71 %).
- **[BUILD] ℹ️** XML-doc обязателен для новых публичных членов: `CS1591` теперь ошибка (`src/Directory.Build.props:11`, `Directory.Build.props:40`). Fix: внести в критерий приёмки.
