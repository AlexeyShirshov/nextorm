# TODO: Массивы ClickHouse — row reader `Array(T)`/`Tuple` и остаток

> Рабочий план. Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.4 (row reader),
> п.5 (higher-order), п.9 (скаляры над массивами).
> **Статус: срезы 1 (row reader), 2 (topK/topKWeighted/quantiles), 3 (higher-order lambda), 4 (предикаты над массивами `startsWith`/`endsWith`/`hasSubstr`) и 5 (`tuple`/`tupleElement`) — готовы.**

## Источник, цель, критерий приёмки

- Проблема (gap §4 п.4): у nextorm нет row reader для `Array(T)`/`Tuple(...)`, поэтому
  array/Tuple-колонку нельзя спроецировать как значение. Это блокирует `groupArray`/`groupUniqArray`,
  `topK`/`topKWeighted`, `quantiles`, возвращающие массивы `JSONExtract*`, `tuple`/`tupleElement`,
  `dictGet*`-функции над массивами и прямую проекцию array-колонок.
- Цель среза 1: материализовать `Array(T)` (включая вложенные `Array(Array(T))`) и `Tuple(...)` как
  значения проекции; включить ClickHouse-агрегаты, возвращающие массивы (`groupArray`,
  `groupUniqArray`), и прямую проекцию уже существующих array-функций.
- Критерий приёмки среза 1: массив/Tuple-колонка и перечисленные агрегаты проецируются и
  выполняются на реальном ClickHouse 25.8; прочие провайдеры — `NotSupportedException`.

## Что уже сделано (не переделывать)

- Функции первого порядка над array-колонками: `length`/`has`/`index_of`/`has_any`/`has_all`/
  `array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`; флаг
  `SupportsArrayFunctions`, хук `MakeArrayFunction`; array-колонка рендерится как SQL, захваченный
  массив — одним параметром.
- Скалярный `array_join` (разворачивает строки) — `SupportsArrayJoin`.
- Клауза `[LEFT] ARRAY JOIN` (`EntityBuilder.ArrayJoin`/`LeftArrayJoin`) и привязка вырожденного
  элемента (`ArrayJoinElement`/`LeftArrayJoinElement`).
- `string.Split` → `splitByChar` (`StringSplit`).
- Гейты `SupportsArrays` (PostgreSQL) и `SupportsArrayFunctions` (ClickHouse) разделены; массив как
  параметр (`= any(@array)`) требует `SupportsArrays`, функции над array-колонками —
  `SupportsArrayFunctions`.

## Матрица «провайдер × форма» — материализация array/Tuple

Форма: есть ли в СУБД типизированный array/Tuple и что возвращает ADO-драйвер в `GetValue`.

| Провайдер | Array-тип | Tuple/record | `GetValue` для array/tuple | Источник |
| --- | --- | --- | --- | --- |
| PostgreSQL | `integer[]`, `text[]`, … | composite/record `(a,b)` | `T[]` (Npgsql); record read-only, nextorm не выставляет | https://www.postgresql.org/docs/current/arrays.html , https://www.postgresql.org/docs/current/rowtypes.html |
| SQL Server | — нет array-типа (S091 not supported; заменяют table-valued параметры) | — | — | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| MySQL | — нет array-типа (`JSON` only) | — | — | https://dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | — нет array-типа (`JSON` only) | — | — | https://mariadb.com/kb/en/json-data-type/ |
| SQLite | — нет array-типа (`json_array` only) | — | — | https://www.sqlite.org/json1.html |
| ClickHouse | `Array(T)` (T — любой, включая вложенный) | `Tuple(T1, …, Tn)` | `T[]`; `System.Tuple<…>` (≤7 элементов) | https://clickhouse.com/docs/en/sql-reference/data-types/array , https://clickhouse.com/docs/en/sql-reference/data-types/tuple ; драйвер ClickHouse.Driver 1.4.0 (`ArrayType.Read` → `T[]`, `TupleType.MakeTuple` → `System.Tuple`) |
| InMemory | CLR `T[]` (нативно) | CLR `Tuple`/`ValueTuple` (нативно) | без SQL — материализуется напрямую | — |

Проверка по докам провайдеров: SQL Server (MS Learn: basic array support S091 не поддерживается,
эквивалент — table-valued parameters), ClickHouse (`Array(T)`, `Tuple`, subcolumns). Для PostgreSQL,
MySQL, MariaDB, SQLite использованы официальные страницы типов/JSON.

## Единообразие провайдеров (решение)

- Row reader — это инфраструктура материализации, а не SQL-функция: тип ветки выбирается по
  объявленному CLR-типу колонки (`SelectExpression.PropertyType`), без диалектного флага. Обобщённая
  array-ветка покрывает и PostgreSQL (`text[]` уже работал), и ClickHouse (`Array(T)`), и любые
  `T[]`/вложенные массивы.
- Провайдеры без array/Tuple-типа (SQL Server, MySQL, MariaDB, SQLite) физически не могут вернуть
  такие колонки; менять их диалекты не нужно.
- InMemory не использует row reader (перечисляет CLR-объекты), изменений не требует.
- ClickHouse-агрегаты `groupArray`/`groupUniqArray` — провайдер-специфичная поверхность
  `ClickHouseFunctions`, гейт `SupportsArrayFunctions` (ClickHouse `true`, PostgreSQL `false`);
  это не family-umbrella, а именно массив-возвращающие агрегаты.

## Срез 1 (это изменение): row reader `Array(T)`/`Tuple`

- Closest C# analog: CLR `T[]` (массив) и `System.Tuple<…>` (кортеж); tier (a) — существующий
  CLR-тип, новый public API не нужен для ридера.
- Row reader: в `SelectExpression.GetDataRecordMethod()` заменить частные ветки `byte[]`/`string[]`
  на общую `_realType.IsArray` → `IDataRecord.GetValue` (без типа-геттера) и добавить ветку
  `System.Tuple<>` (арность 1–7) → `GetValue`; `RowMapperFactory.MapColumn` уже приводит `object` к
  `PropertyType` и обрабатывает `IsDBNull`. `QueryCommand.QueryPreparer` классифицирует array и
  (для не-`NewExpression`) Tuple как single-column проекцию (`TypeFacts.IsSingleColumnProjection`/
  `IsTupleType`), поэтому `Select(x => x.Nums)`/`Select(x => x.Pair)` идут через OneColumn-ридер, а
  `new Tuple<...>(a, b)` по-прежнему разворачивается в аргументы конструктора.
- Публичный API (ClickHouse-поверхность, `SqlFunctions.ClickHouse.cs`):
  - `public T[] group_array<T>(T? value)` → `groupArray(value)`;
  - `public T[] group_uniq_array<T>(T? value)` → `groupUniqArray(value)`.
  Плюс маппинг в `ClickHouseDialect.MakeAggregate`; трансляция — `AdvancedAggregateTranslator`
  через `EmitSimple`, гейт `SupportsArrayFunctions`.
- Уже существующие array-возвращающие функции (`array_sort`/`array_reverse`/`array_distinct`/
  `range`/`array_enumerate`/`array_cum_sum`/`array_slice`/`array_push_back`/`split_by_char`/
  `retention`, `json_all_paths`) перестают требовать обёртки: их результат теперь проецируется
  напрямую. XML-доки обновляются.
- Тест-план:
  - SQL-gen: `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs` — рендер `groupArray`/
    `groupUniqArray` и проекции array-колонки;
  - rejection: `Array...ShouldThrow` для провайдера без `SupportsArrayFunctions`;
  - интеграционные (ClickHouse 25.8, таблица `array_entity` и новая `tuple_entity`):
    `Select(x => x.Nums)`, `Select(x => x.Tags)`, вложенная array-функция без обёртки,
    `group_array`/`group_uniq_array` с `array_sort`.
  - Покрытие: `coverage.settings.xml` включает только `nextorm.{core,sqlite,postgres,sqlserver}` —
    ClickHouse-only фича число не двигает; для core-ридера ожидается рост (используется и PG).
- Доки: `docs/providers/clickhouse.md` (+RU), `docs/guide/provider-specific/clickhouse.md` (+RU),
  `docs/guide/18-json.md`/`13-table-valued-functions.md` при необходимости,
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU), gap-analysis §4 п.4.

## Срез 2 (это изменение, gap §4 п.4 остаток): `topK`/`topKWeighted`/`quantiles` — параметризованные array-агрегаты

**Статус: готово.** Реализовано: `ITopKAggregateRenderer` (+`ISqlDialect.TopKAggregates`, CH-override);
`IQuantileAggregateRenderer.RenderLevels`; 3 метода `ClickHouseFunctions`; `EmitTopK`/`EmitQuantiles`;
`MakeAggregate` `top_k`→`topK`, `top_k_weighted`→`topKWeighted`. Аудит: Находка 64 (param-проход `k`)
исправлена; CHQA2 (`RenderArray`→`RenderLevels`) применён; CHQA1/CHQA4 — приняты (трекинг); CHQA3 (доки) закрыт.

- Closest C# analog: параметризованный агрегат с двумя скобками моделируется как existing
  `quantile(level, value)` (tier b): новые public-методы + renderer, гейт по объекту-способности.
- Публичный API (`SqlFunctions.ClickHouse.cs`):
  - `public T[] top_k<T>(long k, T? value)` → `topK(k)(value)` (`Array(T)`);
  - `public T[] top_k_weighted<T, TWeight>(long k, T? value, TWeight? weight)` → `topKWeighted(k)(value, weight)`;
  - `public double[] quantiles<T>(double[] levels, T? value)` → `quantiles(level1, ...)(value)` (`Array(Float64)`).
- Диалектный план:
  - `IQuantileAggregateRenderer` расширяется `RenderArray(name, levels, value)`; ClickHouse реализует
    `{MakeAggregate(name)}({levels})({value})` (для `quantiles` нативный тип уже `Array(Float64)`).
  - Новый `ITopKAggregateRenderer { Render(name, k, value); RenderWeighted(name, k, value, weight) }`;
    `ISqlDialect.TopKAggregates` default `null` (объект = способность), `SqlDialectBase` virtual `null`,
    ClickHouse override. Отдельный объект, а не `SupportsArrayFunctions`: провайдер с массивами может
    не иметь topK (per-function, не family-umbrella).
  - `MakeAggregate`: `top_k` → `topK`, `top_k_weighted` → `topKWeighted`; `quantiles` — identity.
- Трансляция: `AdvancedAggregateTranslator.EmitTopK` (2/3 аргумента; `k` — константа-литерал) и
  `EmitQuantiles` (levels — только inline `new[]`; захваченный массив → `NotSupportedException`,
  как у `EmitSequenceAggregate`). Param-mode обходит уровни и значение.
- Ограничение среза: только `topK(N)`/`topKWeighted(N)`, без `load_factor`/`'counts'`; только
  `quantiles` без `quantilesExact`/`quantilesTiming`/`quantilesGK`.
- Тест-план:
  - SQL-gen (clickhouse): `TopK_ShouldRenderTopK` (`topK(3)(id)`), `TopKWeighted_ShouldRenderTopKWeighted`,
    `Quantiles_ShouldRenderQuantiles` (`quantiles(0.25, 0.5, 0.75)(id)`),
    `Quantiles_WithCapturedLevels_ShouldThrow`;
  - dialect: `MakeAggregate` для `top_k`/`top_k_weighted`; renderer-юниты `ITopKAggregateRenderer` и
    `RenderArray`; `DialectCapabilityContractTests` — новый объект-способность;
  - rejection (postgres): `TopK_UnsupportedByProvider_ShouldThrow`,
    `Quantiles_UnsupportedByProvider_ShouldThrow`;
  - интеграционные (ClickHouse 25.8, `complex_entity` id 1..3): `topK(2)(id)` → 2 элемента из {1,2,3};
    `topKWeighted(2)(id, id)` → 2 элемента; `quantiles(0.25, 0.5, 0.75)(id)` → 3 элемента, середина ≈ 2;
  - покрытие: core-транслятор покрывается clickhouse-тестами; базис line 85.4% / branch 74.4%.
- Доки EN+RU: `docs/guide/04-grouping-and-aggregates.md` (+RU) — таблица ClickHouse,
  `docs/guide/provider-specific/clickhouse.md` (+RU) агрегаты, `docs/advanced/api-reference.md` (+RU),
  gap-analysis §4 п.4 (снять `topK`/`quantiles` из «Still open»).

## Остаток (срез 4 и прочее)

### Срез 3 (это изменение, gap §4 п.5): higher-order/lambda
`arrayMap`/`arrayFilter`/`arrayExists`/`arrayAll`/`arrayCount`/`arrayFirst*`/`arrayLast*` —
трансляция lambda/higher-order аргумента. **Статус: готово.**
Гейт `SupportsHigherOrderArrayFunctions`; рендер через `HigherOrderLambdaVisitor` (параметр лямбды —
голый идентификатор; вложенные лямбды видят параметры внешней через `BaseExpressionVisitor.LambdaParameters`);
`MakeArrayFunction` оборачивает `arrayCount`/`arrayFirstIndex`/`arrayLastIndex` в `toInt64(...)`.
Аудит: находки 62 (вложенная лямбда) и 63 (param-guard `Clone`) исправлены; HOAF2/HOAF3 (доки) закрыты.

#### Матрица «провайдер × форма» — higher-order (lambda) array-функции

Форма: наличие array-типа и lambda/higher-order функций над массивами.

| Провайдер | Array-тип | Higher-order/lambda над массивами | Источник |
| --- | --- | --- | --- |
| PostgreSQL | `integer[]`, … | — (нет lambda-синтаксиса; только эмуляция через `unnest`+агрегат в подзапросе, не скалярная функция) | https://www.postgresql.org/docs/current/functions-array.html , https://www.postgresql.org/docs/current/functions.html |
| SQL Server | — | — | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| MySQL | — (JSON only) | — | https://dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | — (JSON only) | — | https://mariadb.com/kb/en/json-data-type/ |
| SQLite | — (json only) | — | https://www.sqlite.org/json1.html |
| ClickHouse | `Array(T)` | `arrayMap`/`arrayFilter`/`arrayExists`/`arrayAll`/`arrayCount`/`arrayFirst`/`arrayFirstIndex`/`arrayLast`/`arrayLastIndex` | https://clickhouse.com/docs/en/sql-reference/functions/array-functions (проверено: сигнатуры `func(x[, y…]), arr[, cond…]`; arrayMap/Filter → `Array(T)`, arrayExists/All → `UInt8`, arrayCount/FirstIndex/LastIndex → `UInt32`, First/Last → элемент `T`) |
| InMemory | CLR `T[]` | — (поверхность ClickHouse-only; in-memory её не транслирует и отклоняет) | — |

#### Единообразие провайдеров (решение)

- Фича ClickHouse-only: только ClickHouse умеет lambda/higher-order над массивами; PostgreSQL не
  может выразить их скалярно (только `unnest`+агрегат в отдельном подзапросе, что не тот контракт),
  остальные не имеют array-типа. Поэтому поверхность — `ClickHouseFunctions`, а не
  `CommonFunctions`.
- Гейт — новый `ISqlDialect.SupportsHigherOrderArrayFunctions` (ClickHouse `true`, база `false`).
  Отдельный флаг, а не `SupportsArrayFunctions`: провайдер с array-функциями может не иметь
  lambda; не family-umbrella. Не поддерживающий провайдер → `NotSupportedException`.
- `Make*`-хук не нужен: имена (`arrayMap`/…/`arrayLastIndex`) совпадают с ClickHouse-токенами и
  заданы только этим диалектом (как у существующих `arraySort`/`arrayReverse`); хардкод в
  трансляторе, проверка пост-чек `rg "arrayMap" src/nextorm.*/*Dialect.cs` → совпадений нет, что
  объяснено матрицей.
- Post-check по skill: `rg "<name>" src/nextorm.*/*Dialect.cs` совпадений не даёт, т.к. фича
  заведомо одного провайдера (обосновано выше).

#### Tier и реализация

- Closest C# analog: lambda-аргумент уже моделируется как `Expression<Func<…>>` (прецедент —
  `ClickHouseFunctions.count_if`/`sum_if` через `AggregateFilter`). Tier (b): новые public-методы
  + трансляция + диалектный флаг. Новый API:
  - `public TOut[] array_map<TIn, TOut>(Expression<Func<TIn, TOut>> function, TIn[] array)` → `arrayMap`;
  - `public T[] array_filter<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayFilter`;
  - `public bool array_exists<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayExists`;
  - `public bool array_all<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayAll`;
  - `public long array_count<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayCount`;
  - `public T? array_first<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayFirst`;
  - `public long array_first_index<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayFirstIndex`;
  - `public T? array_last<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayLast`;
  - `public long array_last_index<T>(Expression<Func<T, bool>> predicate, T[] array)` → `arrayLastIndex`.
- Рендер lambda: `ArraySqlTranslator.TryTranslateHigherOrderArray` распознаёт методы
  `ClickHouseFunctions` до обычного switch, проверяет гейт, разворачивает `Quote(LambdaExpression)`,
  привязывает параметр к имени через новый `HigherOrderLambdaVisitor : BaseExpressionVisitor`
  (переопределяет `VisitParameter` → голый идентификатор; `VisitMember` на корне-параметре →
  понятный `NotSupportedException`, чтобы не утекло в резолвер колонок) и печатает
  `name(param -> body, array)`. Порядок обхода (тело, затем массивы) един для SQL- и
  param-проходов; массив рендерится существующим `SqlOperandTranslator.AppendArrayOrColumn`.
  Ограничение среза: одна лямбда с одним параметром; member-access на параметре не поддержан.
- Тест-план:
  - SQL-gen (clickhouse): `ArrayMap_ShouldRenderArrayMap` (`arrayMap(v -> -(v), nums)`),
    `ArrayFilter_ShouldRenderArrayFilter`, `ArrayExists_ShouldRenderArrayExists`,
    `ArrayAll_ShouldRenderArrayAll`, `ArrayCount_ShouldRenderArrayCount`,
    `ArrayFirstAndLast_ShouldRenderArrayFirstAndLast`,
    `ArrayFilter_ShouldNestInsideAnotherArrayFunction`,
    `NestedHigherOrderLambda_ShouldReferenceOuterParameter`;
  - rejection (postgres): `HigherOrderArrayFunction_UnsupportedByProvider_ShouldThrow`;
  - интеграционные (ClickHouse 25.8, `array_entity` id=1 nums=[3,1,2], tags=[a,b,c]):
    `array_map` → [6,2,4], `array_sort(array_filter(...))`, `array_exists`/`array_all`,
    `array_count`, `array_first`/`array_first_index`/`array_last`/`array_last_index` с предикатом;
  - покрытие: `coverage.settings.xml` включает core — новые строки транслятора/визитора
    покрываются clickhouse-тестами; ожидается рост/не снижение (базис: line 85.4%, branch 74.4%).
- Доки: `docs/guide/11-scalar-functions.md` (+RU) — секция ClickHouse arrays; `docs/providers/clickhouse.md`
  (+RU), `docs/advanced/api-reference.md` (+RU), `docs/specs/roadmap/sql-capabilities-gap-analysis.md`
  §4 п.5, `docs/specs/design/API-NAMING-REVIEW.md` (новые public-методы).

### Срез 4 (gap §4 п.9): скаляры над массивами — `startsWith`/`endsWith`/`hasSubstr`

**Статус: готово (см. ниже) / план.** Из §4 п.9 закрыты ранее `length`→`length` (срез first-order) и
`position`→`indexOf` (`index_of`); остаются предикаты отношения массивов.

#### Матрица «провайдер × форма» — предикаты над массивами

Форма: есть ли нативный скаляр, проверяющий префикс/суффикс/вхождение подмассива (contiguous
subsequence). Проверено по документации провайдеров и на реальном ClickHouse 25.8.

| Провайдер | Array-тип | `startsWith`/`endsWith`/`hasSubstr` над Array | Источник |
| --- | --- | --- | --- |
| PostgreSQL | `integer[]`, … | — (нет скаляров; префикс выразим только срезом `a[1:array_length(p,1)] = p`, общего `hasSubstr` нет) | https://www.postgresql.org/docs/current/functions-array.html |
| SQL Server | — (нет array-типа) | — | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| MySQL | — (JSON only) | — | https://dev.mysql.com/doc/refman/8.4/en/json.html |
| MariaDB | — (JSON only) | — | https://mariadb.com/kb/en/json-data-type/ |
| SQLite | — (JSON only) | — | https://www.sqlite.org/json1.html |
| ClickHouse | `Array(T)` | `startsWith(arr, prefix)`, `endsWith(arr, suffix)`, `hasSubstr(arr, sub)` (contiguous ordered subsequence) — проверено `clickhouse-local` на 25.8 | https://clickhouse.com/docs/en/sql-reference/functions/array-functions |
| InMemory | CLR `T[]` | — (поверхность ClickHouse-only, не транслируется) | — |

#### Единообразие провайдеров (решение)

- Фича ClickHouse-only: только ClickHouse выражает предикаты нативно; PostgreSQL — лишь частную
  эмуляцию префикса, остальные не имеют array-типа. Поверхность — `ClickHouseFunctions`, гейт
  `SupportsArrayFunctions` (как у `has`/`hasAny`/`hasAll`; отдельный флаг не нужен — одно семейство).
- Ранее в плане предполагалась эмуляция префикс/суффикс через `hasSubstr`/`arraySlice`; проверка на
  ClickHouse 25.8 показала, что `startsWith`/`endsWith` работают над `Array(T)` напрямую, поэтому
  маппинг — нативный. `hasSubstr` добавляется как самостоятельный предикат вхождения подмассива.
- `Make*`-хук не нужен: имена (`startsWith`/`endsWith`/`hasSubstr`) совпадают с ClickHouse-токенами и
  заданы только этим диалектом (как `hasAny`/`hasAll`/`arraySort`); хардкод в трансляторе.

#### Tier и публичный API

- Closest C# analog: нет (у `T[]`/BCL нет предиката префикса/суффикса/подмассива); tier (b) — новые
  методы `ClickHouseFunctions` + ветка транслятора.
- `public bool starts_with<T>(T[] array, T[] prefix)` → `startsWith(array, prefix)`;
- `public bool ends_with<T>(T[] array, T[] suffix)` → `endsWith(array, suffix)`;
- `public bool has_substr<T>(T[] array, T[] other)` → `hasSubstr(array, other)`.
- Трансляция: ветки в `ArraySqlTranslator.TryTranslateClickHouseArray` → `EmitArrayFunction`
  (аргументы-массивы биндятся одним параметром, `MakeArrayFunction` не кастит — результат `UInt8`→`bool`).

#### Тест-план

- SQL-gen (clickhouse): `StartsWith_ShouldRenderStartsWith` (`startsWith(nums, @p0)`),
  `EndsWith_ShouldRenderEndsWith`, `HasSubstr_ShouldRenderHasSubstr`;
- rejection (postgres): `ArrayRelationPredicates_UnsupportedByProvider_ShouldThrow`;
- интеграционные (ClickHouse 25.8, `array_entity` id=1 nums=[3,1,2]): `starts_with` → true на `[3,1]`,
  `ends_with` → true на `[1,2]`, `has_substr([1,2])` → true, `has_substr([3,2])` → false;
- покрытие: core-транслятор покрывается clickhouse-тестами; базис line 85.4% / branch 74.4%.
- Доки EN+RU: `docs/guide/11-scalar-functions.md` (+RU) таблица ClickHouse arrays,
  `docs/guide/provider-specific/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  gap-analysis §4 п.9; `API-NAMING-REVIEW.md` (новые публичные методы).

### Срез 5 (gap §4 п.4 остаток): скалярная поверхность над `Tuple` — `tuple`/`tupleElement`

**Статус: готово.** `Tuple.Create` → `tuple(...)`, `Tuple<>.ItemN` → `tupleElement(t, n)`; гейт
`SupportsTupleFunctions` (DIM default `false`, CH override). `untuple` — вне объёма (меняет набор
колонок, не скаляр).

#### Матрица «провайдер × форма» — конструктор и доступ к элементу кортежа

| Провайдер | Tuple/record-тип | `tuple(...)` / `tupleElement` / `untuple` | Источник |
| --- | --- | --- | --- |
| PostgreSQL | composite/record `(a,b)` | `ROW(a,b)` и доступ к полю `(t).f1`; `untuple` нет; nextorm не моделирует composite-тип → `—` | https://www.postgresql.org/docs/current/rowtypes.html |
| SQL Server | — (нет tuple-типа) | — | https://learn.microsoft.com/sql/t-sql/data-types/data-types-transact-sql |
| MySQL | — (row constructor `(a,b)` не first-class) | — | https://dev.mysql.com/doc/refman/8.4/en/row-constructor.html |
| MariaDB | — | — | https://mariadb.com/kb/en/row-constructors/ |
| SQLite | — | — | https://www.sqlite.org/lang_expr.html |
| ClickHouse | `Tuple(T1, …, Tn)` | `tuple(x1, …)`, `tupleElement(t, n)` (и `t.n`), `untuple(t)` — проверено `clickhouse-local` на 25.8 | https://clickhouse.com/docs/en/sql-reference/data-types/tuple , https://clickhouse.com/docs/en/sql-reference/functions/tuple-functions |
| InMemory | CLR `Tuple`/`ValueTuple` | — (поверхность ClickHouse-only, переводчиком не обрабатывается) | — |

#### Единообразие провайдеров (решение)

- Фича ClickHouse-only: только ClickHouse имеет first-class `Tuple(T...)` и `tupleElement`; PostgreSQL
  composite-тип nextorm не моделирует, остальные tuple-типа не имеют. Новый флаг
  `ISqlDialect.SupportsTupleFunctions` (DIM default `false`, `SqlDialectBase` virtual `false`, CH
  override) — отдельный от `SupportsArrayFunctions` (array ≠ tuple).
- `untuple(tuple)`: возвращает несколько колонок, а не скаляр, поэтому в nextorm не выразим; остаётся
  ограничением (см. `docs/advanced/limitations.md`).

#### Tier и реализация

- Closest C# analog (tier a, без нового публичного API): `System.Tuple.Create(a, b, …)` конструирует
  кортеж, `System.Tuple<…>.ItemN` читает элемент. Новый internal `TupleSqlTranslator`:
  `TryTranslateCreate` → `tuple(args…)`, `TryTranslateElement` → `tupleElement(t, N)`.
  `TryTranslateCreate` вызывается из `BaseExpressionVisitor.VisitMethodCall`, `TryTranslateElement` —
  из `MemberTranslator` (`.ItemN`). `.ItemN` переводится только когда выражение-кортеж ссылается на
  запрос (`Has<ParameterExpression>()`); захваченный/локальный `Tuple` сворачивается в константу, как
  раньше. `new Tuple<...>(a, b)` не затронут — это `NewExpression` (многоколоночная проекция).
- Тест-план: SQL-gen clickhouse `TupleElementAccess_ShouldRenderTupleElement`,
  `TupleCreate_ShouldRenderTuple`, refresh кэш-плана (`…_WithCapturedValue_…`,
  `…_WithCapturedFilter_…`), локальный кортеж `…_OnCapturedTuple_ShouldNotTranslateToSql`; rejection
  postgres `TupleFunctions_…`/`TupleElementAccess_…`; интеграционные `tuple_entity` id=1
  (`TupleElementAccess_ShouldReturnValues`, `TupleCreate_ShouldMaterialiseTuple`).
- Доки EN+RU: `docs/guide/11-scalar-functions.md` (+RU), `docs/guide/provider-specific/clickhouse.md`
  (+RU), `docs/providers/clickhouse.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/advanced/limitations.md` (+RU, `untuple`); gap-analysis §4 п.4; `API-NAMING-REVIEW.md`.

### Прочее
- Возвращающие массивы `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`.
- `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`.
- `untuple` (меняет набор колонок; не скаляр).
- Привязка элемента для нескольких массивов/join’ов.

## Критерий приёмки (полного item)

- Array/Tuple-колонка и агрегаты из п.1 материализуются и проецируются; запрос строится и
  выполняется на реальном ClickHouse 25.8.
- Higher-order функции из п.2 рендерятся и выполняются.
- Скаляры из п.3 корректно маппятся; прочие провайдеры отклоняют (`NotSupportedException`).
- SQL-gen + rejection + интеграционные тесты зелёные; покрытие не ниже прежнего; аудит
  `nextorm-code-auditor`; docs EN+RU.

## Источники и файлы

- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.4/5/9.
- Код: `src/nextorm.core/Expressions/SelectExpression.cs`,
  `src/nextorm.core/DataContext/RowMapperFactory.cs`, `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`,
  `src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs`, `src/nextorm.clickhouse/ClickHouseDialect.cs`.
- Тесты: `tests/nextorm.clickhouse.tests`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`,
  `tests/nextorm.integration.tests/Providers/ClickHouseTestProvider.cs`.
