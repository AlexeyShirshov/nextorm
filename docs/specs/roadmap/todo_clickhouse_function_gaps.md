# TODO: Пробелы функций ClickHouse (`lowerUTF8`/`upperUTF8`, `trim*`, `replaceRegexp*`, `match`/`extract`, `map*`, `arrayConcat`/`arrayFlatten`/`arrayUniq`/`arrayIntersect`, агрегаты `groupBitmap`/`sumMap`, хэши, `generateULID`)
> Tracking issue: [#78](https://github.com/AlexeyShirshov/nextorm/issues/78).

> Рабочий план (design RFC). Источник: таблица «Summary: highest-value gaps»,
> строка **ClickHouse**, в [`sql-function-coverage-gap.md`](sql-function-coverage-gap.md)
> (§«ClickHouse»); новый пункт §4 в
> [`sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md).
> Публичный API → `docs/specs/design/API-NAMING-REVIEW.md`.

## 1. Пункт и цель

- **Фича:** дополнить `ClickHouseFunctions` недостающими нативными функциями ClickHouse:
  - строки: `lowerUTF8`/`upperUTF8`, `trimLeft`/`trimRight`/`trimBoth`, `replaceRegexpOne`/
    `replaceRegexpAll`, `match`/`extract`/`extractAll`, `splitByString`/`splitByRegexp`/
    `splitByWhitespace`;
  - дата/время: `formatDateTime`/`parseDateTime`(+`BestEffort`), `now`/`today`/`yesterday`;
  - массивы: `arrayConcat`, `arrayFlatten`, `arrayUniq`, `arrayIntersect`/`arrayUnion`/`arrayExcept`/
    `arraySymmetricDifference`;
  - Map-семейство: `map`, `mapKeys`/`mapValues`, `mapContainsKey`/`mapContainsValue`, `mapAdd`/
    `mapConcat`, `mapFilter`/`mapApply`/`mapAll`/`mapExists`, `mapSort`;
  - агрегаты: `groupBitmap`(+`And`/`Or`/`Xor`), `sumMap`/`sumMapFiltered` (проверить статус
    `groupBitAnd`/`groupBitOr`/`groupBitXor` — см. §2);
  - хэши: `MD5`/`SHA1`/`SHA256`/`SHA512`, `xxHash32`/`xxHash64`/`xxh3`, `cityHash64`/`sipHash*`/
    `murmurHash*` (подмножество);
  - `generateULID`.
- **Критерий приёмки:** каждое имя рендерится нативной формой ClickHouse; прочие провайдеры
  отклоняют; SQL-gen + ClickHouse-интеграция.
- **Не входит:** `-State`/`-Merge` и `AggregateFunction`-состояния (заблокировано драйвером,
  [`todo_clickhouse_aggregate_function_state.md`](todo_clickhouse_aggregate_function_state.md));
  table functions внешних движков (`mysql`/`postgresql`/`s3Cluster`/…) — out of scope
  (`sql-function-coverage-gap.md` §ClickHouse).

## 2. Текущее состояние (проверено по коду)

| Слой | Где | Сейчас |
|---|---|---|
| Строки | `ClickHouseFunctions` | только `split_by_char`, `array_string_concat`; `lowerUTF8`/`trim*`/`replaceRegexp*`/`match`/`extract`/`splitByString` **нет** |
| Дата | `ClickHouseFunctions` | конверсии/парты (`toDate*`/`toYear`/`toStartOf*`/`toUnixTimestamp`) есть; `formatDateTime`/`parseDateTime`/`now`/`today`/`yesterday` **нет** |
| Массивы | `ClickHouseFunctions` | есть `array_reverse`/`array_sort`/`array_distinct`/`array_slice`/`array_enumerate`/`array_cum_sum`/`array_push_back`/`array_join`/`range`/higher-order; `arrayConcat`/`arrayFlatten`/`arrayUniq`/`arrayIntersect` **нет** |
| Map | `ClickHouseFunctions` | **всего семейства нет** |
| Агрегаты | `ClickHouseFunctions`/CommonFunctions | `uniq*`/`quantile*`/`median`/`topK`/`argMin`/`argMax`/`anyLast`/`retention`/`windowFunnel`/`sequenceMatch` есть; `groupBitmap`/`sumMap` **нет** |
| Хэши | `ClickHouseFunctions` | **нет** |
| ULID | `ClickHouseFunctions` | `uuidv7` через `CommonFunctions`; `generateULID` **нет** |

⚠ **Расхождение:** gap-analysis §3 относит `groupBitAnd`/`groupBitOr`/`groupBitXor` к реализованным,
но в `ClickHouseFunctions`/`CommonFunctions` их не видно. На старте шага 0 — проверить roslyn-ом,
где они (возможно, общий `bit_and`/`bit_or`/`bit_xor` рендерится в ClickHouse-форму), и не заводить
дубль.

## 3. Матрица провайдеров (провайдер × форма)

Источники: ClickHouse function reference (string, string-replace, string-search, splitting-merging,
array, tuple-map, date-time, encoding, hash, uuid/ulid, aggregate); PostgreSQL 18; Microsoft Learn
T-SQL; MySQL 8.4 / MariaDB; SQLite `lang_corefunc`/`json1`/`lang_mathfunc`.

| Функция | ClickHouse | PostgreSQL | SQL Server | MySQL/MariaDB | SQLite |
|---|---|---|---|---|---|
| `lowerUTF8`/`upperUTF8` | есть (`lower`/`upper` — ASCII) | `lower`/`upper` (UTF-8 по локали) | `LOWER`/`UPPER` | `LOWER`/`UPPER` | `lower`/`upper` |
| `trimLeft`/`trimRight`/`trimBoth` | есть | `ltrim`/`rtrim`/`btrim` | `LTRIM`/`RTRIM`/`TRIM` | `LTRIM`/`RTRIM`/`TRIM` | `ltrim`/`rtrim`/`trim` |
| `replaceRegexpOne`/`replaceRegexpAll` | есть | `regexp_replace`(`g`) | `REGEXP_REPLACE` (2025) | `REGEXP_REPLACE` | — |
| `match`/`extract`/`extractAll` | есть | `regexp_like`/`regexp_substr`/`regexp_matches` | `REGEXP_LIKE`/`REGEXP_SUBSTR` (2025) | `REGEXP_LIKE`/`REGEXP_SUBSTR` | — |
| `splitByString`/`splitByRegexp`/`splitByWhitespace` | есть | `regexp_split_to_array`/`string_to_array` | `STRING_SPLIT` | — | — |
| `formatDateTime`/`parseDateTime` | есть | `to_char`/`to_date` | `FORMAT`/`PARSE` | `DATE_FORMAT`/`STR_TO_DATE` | `strftime` |
| `now`/`today`/`yesterday` | есть | `now()`/`current_date` | `GETDATE()`/`CAST` | `NOW()`/`CURDATE()` | `datetime('now')` |
| `arrayConcat`/`arrayFlatten`/`arrayUniq`/`arrayIntersect` | есть | `array_cat`/`array_ndims`… /—/`&&` | — | — | — |
| Map (`map`, `mapKeys`, …) | есть | — (hstore/JSONB-иное) | — | — | — |
| `groupBitmap`/`sumMap` | есть | — | — | — | — |
| Hash (`MD5`/`SHA*`/`xxHash*`/`cityHash64`/`sipHash*`) | есть | `md5`/`sha256`/`digest` | `HASHBYTES` | `MD5`/`SHA2` | — |
| `generateULID` | есть | — (`uuidv7`) | — | — | — |

**Единообразие провайдеров:** строковые/датовые/regexp-функции семантически близки, но форма и
синтаксис отличаются (`trimBoth`, `replaceRegexp*`, `formatDateTime`) — держим на
`ClickHouseFunctions` (провайдерная поверхность), не промотируем. **Map-семейство и
`groupBitmap`/`sumMap` — ClickHouse-only** (у других нет Map-типа/bitmap-агрегатов). Хэши — кандидат
на общую hash-поверхность (follow-up). `generateULID` — ClickHouse-only.

## 4. Ближайший CLR-аналог и тир

- **tier (b)** для всего: точного BCL-аналога нет (Map/lambda/bitmap — серверные понятия;
  `formatDateTime` — ClickHouse-шаблон).
- `arrayConcat` ∈ array family → на `ClickHouseFunctions`; `map*` — новая под-поверхность
  (возможно, `SqlFunctions.ClickHouse.map_*`).
- `groupBitmap`/`sumMap` — агрегаты, рендерятся как `ClickHouseFunctions.group_bitmap`/`sum_map`
  (сигнатуры `groupBitmap(bitmap)`/`sumMap(key, value)`), гейт `IClickHouseAggregateRenderer`-стиль.
- `generateULID` — скаляр без аргументов (или seed-параметры) → `ClickHouseFunctions.generate_ulid`.

## 5. Дизайн и публичный API

```csharp
// строки
public static string? lower_utf8(string? value);
public static string? upper_utf8(string? value);
public static string? trim_left(string? value, string? chars = null);
public static string? trim_right(string? value, string? chars = null);
public static string? trim_both(string? value, string? chars = null);
public static string? replace_regexp_one(string? value, string? pattern, string? replacement);
public static string? replace_regexp_all(string? value, string? pattern, string? replacement);
public static bool? match(string? value, string? pattern);
public static string? extract(string? value, string? pattern);
public static string[] extract_all(string? value, string? pattern);
public static string[] split_by_string(string? value, string? separator);
// дата
public static string? format_date_time(DateTime? value, string? format, string? timezone = null);
public static DateTime? parse_date_time(string? value, string? format);
public static DateTime? now();
public static DateOnly? today();
public static DateOnly? yesterday();
// массивы
public static T[] array_concat<T>(params T[][] arrays);
public static T[] array_flatten<T>(T[][] arrays);
public static T[] array_uniq<T>(T[] array);
public static T[] array_intersect<T>(params T[][] arrays);
// map
public static TValue? map_get<TKey, TValue>(IDictionary<TKey, TValue>? map, TKey? key);   // map[key]
public static TKey[] map_keys<TKey, TValue>(IDictionary<TKey, TValue>? map);
public static TValue[] map_values<TKey, TValue>(IDictionary<TKey, TValue>? map);
public static bool? map_contains_key<TKey, TValue>(IDictionary<TKey, TValue>? map, TKey? key);
public static IDictionary<TKey, TValue>? map_add<TKey, TValue>(IDictionary<TKey, TValue>? a, IDictionary<TKey, TValue>? b);
// агрегаты
public static ulong? group_bitmap<T>(T? value);
public static IDictionary<TKey, TValue[]>? sum_map<TKey, TValue>(TKey? key, TValue? value);
// hash
public static string? md5(string? value);
public static string? sha1(string? value);
public static string? sha256(string? value);
public static ulong? xxhash64(string? value);
// ulid
public static string? generate_ulid();
```

- Map-типы: ClickHouse `Map` читается драйвером как `IDictionary<K,V>`? Уточнить тип-маппинг
  (открытый вопрос); при отсутствии — начать с `map_keys`/`map_values` и функций над `Tuple[]`.
- Хэши в ClickHouse возвращают `FixedString`; CLR-типизация — `string`/`byte[]` (согласовать с
  кросс-провайдерным hash-follow-up).

## 6. Диалект-план

- Расширить существующие capability-объекты ClickHouse (`ISequenceAggregateRenderer`-стиль) или
  добавить `IClickHouseMapFunctions`/`IClickHouseHashFunctions`; строки/даты — на `ClickHouseFunctions`
  с per-name `Supports`, по образцу `IDateConversionRenderer`.
- Транслятор: `src/nextorm.core/Visitors/ArraySqlTranslator.cs` (массивы),
  `src/nextorm.core/Visitors/JsonSqlTranslator.cs` (при необходимости), новый
  `ClickHouseScalarFunctionTranslator.cs` (строки/даты/hash).
- Агрегаты `group_bitmap`/`sum_map` — через capability-объект агрегатов (не family-флаг).

## 7. Этапы внедрения

1. Строки (`lower_utf8`/`upper_utf8`/`trim_*`/`replace_regexp_*`/`match`/`extract`/`split_by_string`); SQL-gen.
2. Дата/время (`format_date_time`/`parse_date_time`/`now`/`today`/`yesterday`); SQL-gen.
3. Массивы (`array_concat`/`array_flatten`/`array_uniq`/`array_intersect`); SQL-gen.
4. Агрегаты (`group_bitmap`/`sum_map`) + проверить `groupBit*`; SQL-gen + интеграция.
5. Map-семейство; hash-подмножество; `generate_ulid`; интеграция.
6. Документация EN+RU; обновить `sql-function-coverage-gap.md`.

## 8. План тестов

- SQL-gen: `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs` + `ClickHouseDialectTests.cs`
  (per-name `Supports`/`Render`).
- Гейт: `tests/nextorm.<other>.tests` — `ClickHouseFunctions.lower_utf8`/`group_bitmap` →
  `NotSupportedException`.
- Интеграция: `tests/nextorm.integration.tests/ClickHouseSpecificTests.cs` — `lowerUTF8`, `match`/
  `extract`, `arrayConcat`/`arrayIntersect`, `groupBitmap`/`sumMap`, hash, `generateULID` на реальной БД.
- Coverage: `nextorm.clickhouse` **не входит** в `coverage.settings.xml` — числа не сдвинут;
  указать явно и всё равно добавить SQL-gen.

## 9. Открытые вопросы

1. Статус `groupBitAnd`/`groupBitOr`/`groupBitXor` — где реализованы (расхождение §2)? Не дублировать.
2. Map-тип: как драйвер `ClickHouse.Driver` отдаёт `Map(K,V)` в CLR (`IDictionary`/`Dictionary`)?
3. Hash-поверхность: промотировать в `CommonFunctions` (`md5`/`sha*`/`xxhash`) или держать
   ClickHouse-only до отдельного cross-provider todo?
4. `extract_all`/`split_by_*` возвращают `Array(String)` → `string[]`: согласуется с `ArrayJoin`?
5. `generateULID` — параметры (seed/дата) поддержать?
6. `now`/`today`/`yesterday` — добавить и в кросс-провайдерную вторую волну (`DateTime.Now` покрывает
   `now` лишь частично)?

## 10. Файлы к изменению

- Правки: `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
  `src/nextorm.core/Visitors/ArraySqlTranslator.cs`, новый
  `src/nextorm.core/Visitors/ClickHouseScalarFunctionTranslator.cs`,
  `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`,
  `DialectCapabilities.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`.
- Тесты: `tests/nextorm.clickhouse.tests/*`, `tests/nextorm.integration.tests/ClickHouseSpecificTests.cs`,
  негативные — `tests/nextorm.<other>.tests`.
- Доки: `docs/guide/provider-specific/clickhouse.md` (+RU), `docs/guide/11-scalar-functions.md` (+RU),
  `docs/providers/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`, `docs/specs/roadmap/sql-function-coverage-gap.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **нужен пересмотр §5/§6 — 2 блокера** (перехват `extract` и `md5`/`sha256`) **+ блокер типа** (`DateOnly`).

- **[OCP] 🔴** `extract` перехватывается builtin-транслятором (молча неверный SQL): план вводит `ClickHouseFunctions.extract(string? value, string? pattern)` (`:99`), а `BuiltinFunctionTranslator` матчит имя+арность `nameof(CommonFunctions.extract)` при `Arguments.Count == 2` (`BuiltinFunctionTranslator.cs:72`) и вызывается раньше всех провайдерных (`NormSqlTranslator.cs:437`); `EmitDatePart` возьмёт `args[0]` как constant string-часть (`:389`) и упадёт на паттерн-колонке. Fix: `DeclaringType != typeof(CommonFunctions)` в ветках `extract`/`date_part` (`BuiltinFunctionTranslator.cs:72-77`) либо переименовать CH-член (`extract_regexp`).
- **[OCP] 🔴** `md5`/`sha256` перехватываются `ExtendedScalarFunctionTranslator`: `:123,125` — `md5` в `DirectFunctions` (`ExtendedScalarFunctionTranslator.cs:34`), `sha256` — отдельная name-проверка (`:138`); матч по имени раньше провайдерного (`NormSqlTranslator.cs:452`); на ClickHouse `SupportsExtendedScalarFunctions == false` → `NotSupportedException`. Fix: DeclaringType-гейт/порядок (как выше).
- **[TYPE] 🟡** `:106-107` `today()`/`yesterday()` → `DateOnly?`: `DateOnly` в `src`/`tests` 0 раз, пути материализации нет; существующий `to_date`/`to_date32` возвращает `DateTime?` (`SqlFunctions.ClickHouse.cs:634,640`). Fix: `DateTime?` (консистентно с `to_date`) либо отдельно обосновать `DateOnly`.
- **[TYPE] 🟡** `:123-126` типы возврата hash (`FixedString`→`string`, `UInt64`→`ulong`) не подтверждены: нет маппинга `FixedString`, `UInt64` существующие функции кастят (`ClickHouseDialect.cs:142-143,305`); план сам помечает открытым (`:133-134`). Fix: зафиксировать hex/byte[]-представление и проверить материализацию до объявления типов.
- **[TYPE] 🟡** `:121` `sum_map → IDictionary<TKey,TValue[]>` расходится с существующим Map-возвратом `Dictionary<string,string>` (`SqlFunctions.ClickHouse.cs:250`); driver-mapping `Map(K,V)` не проверен (§9.2 `:169`). Fix: выровнять форму и подтвердить до типизации.
- **[OCP]/[DRY] 🟡** `:139-140` три механизма гейтинга в одном плане (строки/даты — per-name `Supports`; Map/Hash — новые `IClickHouseMapFunctions`/`IClickHouseHashFunctions`; агрегаты — `IClickHouseAggregateRenderer`-стиль); новые интерфейсы дублируют форму `IScalarFunctions` (`DialectCapabilities.cs:340-351`). Fix: выбрать один механизм (инварианты 1–2).
- **[DRY] ℹ️** `:45-48` §2-«расхождение» по `groupBit*` уже закрыто: `bit_and/bit_or/bit_xor` мапятся в `groupBitAnd/Or/Xor` (`AdvancedAggregateTranslator.cs:41-48`, `ClickHouseDialect.cs:549-551`) под `SupportsBitAggregates`. Fix: заменить «проверить на старте» на факт.
- **[SRP]/[KISS] ℹ️** `ClickHouseFunctions` вырастет за ~1100 строк (`SqlFunctions.ClickHouse.cs` — 772 + ~40 членов). Deferred: `partial class` по семействам при превышении порога.
## 11. Статус реализации (issue #78)

**Реализовано (native surface + SQL-gen + негативные тесты):**

- строки: `lower_utf8`/`upper_utf8`, `trim_left`/`trim_right`/`trim_both`, `replace_regexp_one`/
  `replace_regexp_all`, `match`/`extract`/`extract_all`, `split_by_string`/`split_by_regexp`/
  `split_by_whitespace`;
- дата/время: `format_date_time`, `parse_date_time`, `parse_date_time_best_effort`, `now`/`today`/
  `yesterday`;
- массивы: `array_concat`/`array_flatten`/`array_uniq`/`array_intersect`/`array_union`/`array_except`/
  `array_symmetric_difference`;
- Map: `map` (inline-пары), `map_keys`/`map_values`, `map_contains_key`/`map_contains_value`,
  `map_add`/`map_concat`, `map_filter`/`map_apply`/`map_all`/`map_exists`, `map_sort`;
- агрегаты: `group_bitmap`/`group_bitmap_and`/`group_bitmap_or`/`group_bitmap_xor`,
  `sum_map`/`sum_map_filtered`;
- хэши: `md5`/`sha1`/`sha256`/`sha512`, `xx_hash32`/`xx_hash64`/`xxh3`, `city_hash64`,
  `sip_hash64`/`sip_hash128`, `murmur_hash2_32`/`murmur_hash2_64`/`murmur_hash3_32`/
  `murmur_hash3_64`/`murmur_hash3_128`;
- `generate_ulid`.

**Решения, отклонившиеся от RFC:**

1. Тиры диалекта упрощены: новый `ClickHouseNativeFunctionTranslator` (`Visitors/`) опирается на уже
   существующий `ISqlDialect.ScalarFunctions` (`ClickHouseScalarFunctions.Supports`/`Render`); новый
   интерфейс и правки `ISqlDialect`/`SqlDialectBase`/`DialectCapabilities.cs` не потребовались. Гейт
   прочих провайдеров — их `ScalarFunctions.Supports(name) == false` → `NotSupportedException`.
2. Транслятор вызван **в начале** `TranslateNormSql`, до `BuiltinFunctionTranslator`/
   `ExtendedScalarFunctionTranslator`: имена `extract` (совпадает с `CommonFunctions.extract`) и
   `md5`/`sha256` (PostgreSQL) матчатся там только по имени и без учёта declaring type.
3. `$groupBitAnd`/`$groupBitOr`/`$groupBitXor` уже реализованы как `MakeAggregate("bit_*")`
   (`ClickHouseDialect.cs`), дубль не заводился; `groupBitmap*` — другой bitmap-агрегат.
4. `map` принимает `params Tuple<TKey,TValue>[]`, а не value-tuple/`IDictionary`: C# запрещает
   tuple-литералы и `KeyValuePair`-литералы в expression tree (CS8143), а `IDictionary`-результат
   построитель строк не материализует. Проекция `Map` требует `map_keys`/`map_values`/
   `map_contains_*`.
5. `array_uniq` возвращает `long` (`arrayUniq` — число уникальных, `toInt64`), а не `T[]` (это
   `arrayDistinct`, уже есть).
6. Хэши `FixedString`/`UInt128` (`sip_hash128`/`murmur_hash3_128`) типизированы как `string?`;
   32/64-битные — `int`/`long` через `toInt32`/`toInt64` по конвенции диалекта.

**Осталось (вне этого PR):**

- Интеграция на реальном ClickHouse (`ClickHouseSpecificTests`) — SQL-gen и негативные тесты зелёные,
  контейнерная проверка не запускалась. Перечисленные выше «нативные» результаты (hash FixedString,
  `Map`, bitmap-состояние) требуют проверки материалзации драйвером.
- In-memory не применим: все функции — ClickHouse-only, in-memory их не переписывает (как и прочие
  `ClickHouseFunctions`).
- `sql-function-coverage-gap.md` §ClickHouse / gap-analysis §4 не правились в этом PR сознательно —
  файлы параллельно правят другие агенты; строку-`Todo` после интеграции убрать.
- Coverage: `nextorm.clickhouse` не входит в `coverage.settings.xml`, число не сдвигается.
