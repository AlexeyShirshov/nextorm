# TODO: PostgreSQL range-типы и `Overlaps` (`&&`)
> Tracking issue: [#66](https://github.com/AlexeyShirshov/nextorm/issues/66).

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
  `Where(x => x.During.Overlaps(range))` и предикаты containment транслируются в корректный SQL;
  unbounded/empty и inclusive/exclusive границы сохраняются; in-memory ведёт себя как PG-семантика.

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
| Тип-маппинг | `Expressions/SelectExpression.GetDataRecordMethod` | нет ветки для range-типа |
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

**Единообразие:** фича **PG-only**, остальные гейтятся `SupportsRanges => false` с понятным
`NotSupportedException`. Эмуляция парой колонок на прочих провайдерах — отдельный, более крупный
workstream, вне этого todo.

## 5. Ближайший CLR-аналог и тир

- Ближайший аналог — `NpgsqlRange<T>` (Npgsql), но он недоступен в core.
- Тир **(b)**: новый provider-agnostic value-type `NextORM.Core.Range<T>` (или не-generic `Range`
  с `object`-границами) + `PostgresFunctions` методы + PG-диалект-хуки; `[SqlFunction]` не подходит
  (нужны операторы/границы).

## 6. Дизайн и публичный API

```csharp
public readonly struct Range<T>
{
    public Range(T? lower, T? upper, bool lowerInclusive = true, bool upperInclusive = false);
    public T? Lower { get; }
    public T? Upper { get; }
    public bool LowerInclusive { get; }
    public bool UpperInclusive { get; }
    public bool IsEmpty { get; }
    public static Range<T> Empty { get; }
}
```

- `PostgresFunctions`:
  - `bool Overlaps<T>(Range<T> a, Range<T> b)` → `a && b`;
  - `bool Contains<T>(Range<T> range, T value)` → `range @> value`;
  - `bool Contains<T>(Range<T> outer, Range<T> inner)`, `bool ContainedBy<T>(...)` → `@>`/`<@`;
  - конструкторы `int4range`/`int8range`/`numrange`/`tsrange`/`tstzrange`/`daterange`;
  - `T? lower<T>(Range<T>)`, `T? upper<T>(Range<T>)`, `bool isempty(Span)` и т.д.
- Диалект: `SupportsRanges` (default `false`), `MakeRangeConstructor`/`MakeRangeOperator` либо рендер
  прямо в PG-визиторе (PG-only можно держать в `PostgresFunctions` без общего `Make*`).
- Тип-маппинг: `nextorm.postgres` конвертирует `Range<T>` ↔ `NpgsqlRange<T>` на параметре и на чтении
  (новый PG-специфичный read-хук), `int4range`/`tsrange` — по типу `T`.
- In-memory: сравнение пересечения границ в CLR.

## 7. Этапы внедрения

1. `Range<T>` в ядре + `PostgresFunctions.Overlaps`/`Contains` + SQL-gen тесты.
2. Read/write range-колонки в `nextorm.postgres` (`NpgsqlRange<T>` ↔ `Range<T>`), конструкторы и функции.
3. Остальные операторы/функции (`<<`/`>>`/`min`/`max`/union/intersection/difference), multirange — при спросе.

## 8. План тестов

- SQL-gen (`tests/nextorm.postgres.tests/SqlGenerationTests.cs`): `Overlaps` → `&&`, `@>`/`<@`,
  конструкторы с `[)`/`[]`/unbounded/empty.
- Диалект: `PostgresDialectTests` — gate и рендер операторов.
- Интеграция `PostgresSpecificTests.cs`: round-trip `Range<T>`↔range-колонка; `&&`-фильтр на таблице
  резервирований; empty/unbounded/inclusive-границы.
- In-memory: `Overlaps`/`Contains` в CLR.

## 9. Открытые вопросы

1. `Range<T>` generic в ядре vs PG-only тип в `nextorm.postgres` (и не тянем ли Npgsql в core).
2. Multirange включать сразу или фаза 2.
3. Как называть: `Overlaps` (linq2db/`Sql.Row`) vs `Overlapping`/`Intersects`.
4. Поддержать ли `Contains(value)` и `Contains(range)` перегрузками или разными именами.
5. Нужна ли эмуляция «пара from/to» на других провайдерах (сейчас — нет).

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
