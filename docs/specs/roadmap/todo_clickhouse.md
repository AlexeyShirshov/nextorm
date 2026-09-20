# TODO: недостающие функции ClickHouse

> Незаблокированный остаток Phase 2 сведён в `todo_phase2.md`.

Черновик списка популярных функций/конструкций ClickHouse, которых пока нет в nextorm.
Отсортирован **по сложности реализации** (от простого к сложному). В конце — то, что отсутствует
по замыслу (read-only построитель `SELECT`) и сводная оценка объёма.

Источник — код `1.0.3-alpha`, `src/nextorm.clickhouse/ClickHouseDialect.cs`,
`src/nextorm.core/Visitors/*Translator.cs`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
`docs/advanced/limitations.md`, `docs/providers/clickhouse.md`.

> Обходной путь уже сегодня: любую скалярную/табличную функцию можно объявить самому через
> `[SqlFunction("name")]` / `[SqlTableFunction("name")]`. Это не покрывает операторный синтаксис
> (`->`, `ARRAY JOIN`, `FINAL`, `LIMIT BY`), комбинаторы (`-If`/`-Array`/`-State`/`-Merge`) и
> агрегаты с особым синтаксисом (`quantile(0.5)(x)`).

## Как устроено добавление (шпаргалка)

Одна встроенная функция обычно затрагивает 6–8 файлов:

1. `src/nextorm.core/Query/SqlFunctions.cs` — новый метод `CommonFunctions` (публичный API, extend-only).
2. Транслятор:
   - `Visitors/BuiltinFunctionTranslator.cs` — `nullif`/`greatest`/`date_trunc`/`string_agg`;
   - `Visitors/AdvancedAggregateTranslator.cs` — редкие агрегаты (bool/bit/stat/ordered);
   - `Visitors/ExtendedScalarFunctionTranslator.cs` — расширенные скаляры;
   - `Visitors/ArraySqlTranslator.cs` / `Visitors/JsonSqlTranslator.cs` — массивы/JSON;
   - `Visitors/ScalarFunctionTranslator.cs` — `string`/`Math`/`DateTime`.
3. `DataContext/Dialect/ISqlDialect.cs` + `SqlDialectBase.cs` — флаг `Supports*` (по умолчанию
   `false`) и/или хук `Make*`.
4. `src/nextorm.clickhouse/ClickHouseDialect.cs` — включить флаг, переопределить хук/имя.
5. Тесты: `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs` (SQL без БД),
   `ClickHouseDialectTests.cs` (прямые хуки).
6. Документация **EN + RU**: `docs/guide/11-scalar-functions.md`, `docs/guide/04-grouping-and-aggregates.md`,
   `docs/providers/clickhouse.md`, `docs/providers/overview.md` (матрица),
   `docs/advanced/limitations.md`, `docs/specs/roadmap/sql-capabilities-gap-analysis.md`,
   `docs/advanced/api-reference.md`; XML-doc комментарии.

Ограничение row reader: функции, возвращающие массив/кортеж (`groupArray`, `topK`, `quantiles`,
`JSONExtractArrayRaw`), нельзя проецировать как колонку — только внутри запроса, как сейчас
`array_agg` в PostgreSQL.

## Уже реализовано (для контекста)

- Строки: `upper`, `lower`, `trim`/`trimLeft`/`trimRight`/`trimBoth`, `substring`, `replace`,
  `like`, `contains`/`startswith`/`endswith`, `lengthUTF8`, `string.IsNullOrEmpty`, `concat`.
- Математика: `abs`, `ceiling`, `floor`, `round`, `sqrt`, `pow`, `exp`, `log`, `sin`, `cos`, `tan`,
  `sign`, `trunc`.
- Даты: `now()`/`now('UTC')`, `year`/`month`/`day`/`hour`/`minute`/`second`.
- Агрегаты: `count`, `count_big`, `count_distinct`, `min`, `max`, `avg`, `sum`, `stdev`/`stdevp` →
  `stddevSamp`/`stddevPop`, `var`/`varp` → `varSamp`/`varPop` (+ `distinct`).
- Окна и фреймы, `CASE`/ternary/`switch`, `COALESCE` → `coalesce`, числовой `CAST`,
  `nullif`, `greatest`/`least`.
- `INTERSECT ALL`/`EXCEPT ALL`, `GROUP BY ... WITH ROLLUP`/`WITH CUBE`, CTE (без `recursive`),
  `limit`/`offset`, бэктики, `@name`.

---

## Уровень 1. Малые правки (флаг/хук диалекта + тесты)

Изолированные изменения: один флаг/хук в `ClickHouseDialect` и одна ветка маппинга, модель
запроса не меняется. Оценка: **0.5–1 день каждое**.

- [x] **`date_trunc`** — `SupportsDateTrunc => true` + `MakeDateTrunc` → `dateTrunc('part', value)`
      со сворачиванием `microseconds`/`milliseconds` в единственные и отказом для
      `decade`/`century`/`millennium`. Тесты: `SqlGenerationTests.DateTrunc_*`,
      `ClickHouseDialectTests.DateAndStringHooks_*`, `DateTrunc_WithUnsupportedField_*`.
- [x] **`date_add` / `DateTime.Add*` / `end_of_month`** — `SupportsDateArithmetic => true` +
      `MakeDateAdd` рендерит выделенные `addYears`/`addQuarters`/…/`addSeconds` (три крупные части
      сворачиваются в масштабированный `addYears`), `MakeEndOfMonth` → `toLastDayOfMonth(value)`.
      Тесты: `SqlGenerationTests.DateAdd_*`, `DateTimeAddMethods_*`, `EndOfMonth_*`.
- [x] **Функции приведения и частей даты** — `toDate`/`toDateTime`/`toDate32`, `toYear`/`toQuarter`/
      `toMonth`/`toDayOfMonth`/`toDayOfWeek`/`toHour`/…, `toStartOf{Year,Quarter,Month,Week,Day,Hour,…}`,
      `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp`. Методы `ClickHouseFunctions.to_date`/
      `to_date_time`/`to_date32`, `to_year`/`to_quarter`/`to_month`/`to_day_of_month`/`to_day_of_week`/
      `to_day_of_year`/`to_hour`/`to_minute`/`to_second`, `to_start_of_*`, `to_monday`, `to_yyyymm`/
      `to_yyyymmdd`, `to_unix_timestamp`; флаг `SupportsDateConversionFunctions`; хук
      `ISqlDialect.MakeDateConversion` для конверсий/начал периодов, а части переиспользуют
      существующий `MakeDatePart` (расширен до `to*`-аксессоров). Целочисленные результаты
      оборачиваются в `toInt32`/`toInt64` (нативные `UInt8`/`UInt16`/`UInt32` не читаются построителем
      строк). `toLastDayOfMonth`/`dateTrunc` остаются на `end_of_month`/`date_trunc`.
      Тесты: `SqlGenerationTests.DateConversionFunctions_ShouldUseClickHouseNames`/`DateTimeParts_ShouldUseToAccessors`,
      `ClickHouseDialectTests.MakeDateConversion_ShouldMapToClickHouseNames`/`DateAndStringHooks_*`,
      `Postgres…DateConversionFunctions_ShouldThrowBecausePostgresHasNoClickHouseDateSurface`,
      `ClickHouseIntegrationTests.DateConversionFunctions_ShouldReturnDateParts` (реальный ClickHouse);
      `WIP_clickhouse_date_functions.md`.
- [x] **`string_agg`** — `SupportsStringAgg => true` + `MakeStringAgg` →
      `arrayStringConcat(groupArray(x), delim)`. Отличие семантики (порядок/`NULL`) задокументировано.
- [x] **`bit_and` / `bit_or` / `bit_xor`** → `groupBitAnd`/`groupBitOr`/`groupBitXor` через
      `MakeAggregate`; `SupportsBitAggregates => true`.
- [x] **`corr` / `covar_pop` / `covar_samp`** → `corr`/`covarPop`/`covarSamp`;
      `SupportsStatisticalAggregates => true`. Добавлен отдельный флаг
      `SupportsRegressionAggregates` (PostgreSQL), чтобы `regr_*` продолжали бросать
      `NotSupportedException` в ClickHouse.
- [x] **`argMin` / `argMax`** — методы `CommonFunctions.arg_min`/`arg_max`, ветка в
      `AdvancedAggregateTranslator`, флаг `SupportsArgMinMax`; `MakeAggregate` → `argMin`/`argMax`.
- [x] **`countIf` / `sumIf` / `avgIf` / `minIf` / `maxIf`** — реализован **вариант A**: методы
      `CommonFunctions.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` с `Expression<Func<bool>>`, флаг
      `SupportsIfAggregates`, `MakeAggregate` → `countIf`/`sumIf`/…. Предикат рендерит
      `AggregateFilter.AppendPredicate` (без обёртки `filter (where ...)`). Вариант B (обобщённый
      хук для ANSI `FILTER` → `-If`) оставлен на будущее.
- [ ] **Скалярные `startsWith` / `endsWith` / `position` / `length` над массивами** — по
      `WIP_clickhouse_array_scalars.md` это не «мелочь», а часть массивы-воркстрима Уровня 3:
      блокирует отсутствие row reader для массивов и binding `T[]`-параметров, а не движок ClickHouse.
      Корректные соответствия — `length` → `length` (алиас `CARDINALITY`), `position` → `indexOf`
      (1-based), префикс/суффикс → `hasSubstr`/`arraySlice`; `startsWith`/`endsWith` к массивам
      **не** применимы (только `String`/`FixedString`).
- [x] **`count` / `count_big` / `count_if` в проекции** — флаг `WrapsCountResult` + хук
      `WrapCount(expression, big)` (base — тождество); ClickHouse оборачивает `count`/`count_distinct`/
      `count_if` в `toInt32(...)` (CLR `int`), а `count_big`/`count_big_distinct` — в `toInt64(...)`
      (CLR `long`). Точки применения: `NormSqlTranslator` (после `FILTER`, если он есть),
      `AdvancedAggregateTranslator.EmitIfAggregate` (`count_if`), `WindowFunctionTranslator`
      (`count(*) over (...)`).
      Тесты: `SqlGenerationTests.CountAggregates_ShouldCastToClrInteger`,
      `SqlGenerationTests.WindowCount_ShouldCastWholeWindowExpression`,
      `ClickHouseDialectTests.WrapCount_ShouldCastToClrInteger`,
      `ClickHouseIntegrationTests.CountAggregates_ShouldCastToInt64InProjection` (реальный ClickHouse);
      `docs/providers/clickhouse.md`.
- [ ] **Row reader `UInt64`** — `SelectExpression.GetDataRecordMethod` не знает `ulong`, а
      типизированные геттеры (`GetInt32`/`GetInt64`) не читают нативный `UInt64`. Поэтому колонка
      `hits_v1.UserID` (`UInt64`) и нативные результаты `count()`/`uniq()`/`uniqMerge` не
      материализуются без SQL-приведения: диалект оборачивает только LINQ-агрегаты
      (`WrapCount`/`MakeUniqAggregate`), а в `WithSql` приведение приходилось дописывать вручную.
      Нужны ветка `ulong` и чтение `UInt64` в row reader; до этого demo-запросы с нативным `UInt64`
      падают в рантайме.

## Уровень 2. Среднее (новый API `SqlFunctions.Sql` + транслятор)

Новая площадь API/family, но модель запроса уже существует. Оценка: **1–3 дня каждое**.

- [x] **`uniq` / `uniqExact` / `uniqCombined` / `uniqHLL12`** — методы `ClickHouseFunctions.uniq`/
      `uniq_exact`/`uniq_combined`/`uniq_hll12` (`long`), флаг `SupportsUniqAggregates`, хук
      `ISqlDialect.MakeUniqAggregate` (ClickHouse оборачивает в `toInt64(...)`, т.к. нативный `UInt64`
      не материализуется). Точный `uniqExact` перекрывается переносимым `count_distinct`/
      `count_big_distinct` (без побитовой эквивалентности), поэтому алгоритм-специфичное семейство
      остаётся ClickHouse-only; единственный кросс-провайдерный аналог приближённого distinct —
      SQL Server 2019+ `APPROX_COUNT_DISTINCT` (см. `docs/providers/clickhouse.md`).
      Тесты: `SqlGenerationTests.UniqAggregates_ShouldUseClickHouseNames`,
      `ClickHouseDialectTests.MakeAggregate_ShouldMapProviderNames`,
      `ClickHouseIntegrationTests.UniqAggregates_ShouldCountDistinctValues` (реальный ClickHouse).
- [ ] **Комбинаторы `-State` / `-Merge` над `AggregateFunction(...)`** — `uniqState`/`sumState`/… и
      `uniqMerge`/`sumMerge`/`avgMerge`/… работают не над обычной колонкой, а над колонкой-состоянием
      `AggregateFunction(uniq, UInt64)` (`AggregatingMergeTree`/материализованные представления).
      Нужны: методы `ClickHouseFunctions.uniq_merge`/`…` (или суффиксный хук `MakeAggregateCombinator`),
      флаг и поддержка типа `AggregateFunction` в метаданных/row reader (значение-состояние нельзя
      проецировать как скаляр). Блокирует demo-запрос `uniqMerge(users_state)`
      (`clickhouse_incremental.sql`), зависим от row-reader-воркстрима; см. `-If` (закрыт,
      `SupportsIfAggregates`) — это другой комбинатор.
- [ ] **`groupArray` / `groupUniqArray`** — возвращают массив; до использования нужен row reader
      для массивов (или ограничиться вложенными выражениями, как `array_agg`). Флаг
      `SupportsArrayAggregateFunctions`, поэтому зависит от работ по массивам (уровень 3).
- [ ] **`topK` / `topKWeighted`** — возвращают массив; та же зависимость от row reader.
- [x] **`quantile` / `median` (`quantileExact`, `quantileTiming`, …)** — параметрические агрегаты с
      двойными скобками: `quantile(0.5)(x)`. Рендер — `ISqlDialect.MakeQuantile`/`MakeMedian`
      (ClickHouse оборачивает в `toFloat64(...)`, т.к. `quantileTiming` возвращает `Float32`, а
      `quantileExact` сохраняет тип входа), методы `ClickHouseFunctions.quantile`/`quantile_exact`/
      `quantile_timing`/`median`. `quantiles` (массив) остаётся заблокированным row reader'ом
      массивов (см. `groupArray`/`topK`). Кросс-провайдерный `percentile_cont`/`percentile_disc`
      (SQL Server/MariaDB — оконная форма) вынесен в `todo_mssql.md` → «Осталось»; см.
      `docs/providers/clickhouse.md`.
      Тесты: `SqlGenerationTests.QuantileAggregates_ShouldUseDoubleParentheses`,
      `ClickHouseDialectTests.MakeQuantile_ShouldUseDoubleParenthesesAndCastToFloat64`,
      `ClickHouseIntegrationTests.QuantileAggregates_ShouldReturnQuantile` (реальный ClickHouse).
- [x] **`any` / `anyLast` (агрегаты)** — из-за конфликта с `CommonFunctions.any` (квантор подзапроса)
      методы названы `any_agg`/`any_last` (`T?`), SQL — `any`/`anyLast`. После промоушена `any_agg`
      живёт на кросс-провайдерном `CommonFunctions` под флагом `SupportsAnyValueAggregate` (MySQL
      `ANY_VALUE`, ClickHouse `any`; MariaDB выключен — нет `ANY_VALUE` до 13.2, SQL Server выключен —
      только 2025/Fabric); на `ClickHouseFunctions` остался только `any_last` под
      `SupportsAnyAggregates`. См. `docs/providers/clickhouse.md`; кросс-провайдерная часть зафиксирована в
      `todo_mssql.md`.
      Тесты: `SqlGenerationTests.AnyAggregates_ShouldUseClickHouseNames` (`any_last`),
      `AnyValueAggregate_ShouldUseClickHouseAny`,
      `ClickHouseDialectTests.CapabilityFlags_ShouldMatchClickHouse`,
      `ClickHouseIntegrationTests.AnyAggregates_ShouldReturnRowValue` (реальный ClickHouse).
- [x] **`groupBitAnd` / `groupBitOr` / `groupBitXor`** — реализовано маппингом `bit_and`/`bit_or`/
      `bit_xor` → `groupBitAnd`/`groupBitOr`/`groupBitXor` в `ClickHouseDialect.MakeAggregate` под
      флагом `SupportsBitAggregates`; отдельные методы не нужны.
- [x] **`windowFunnel` / `retention` / `sequenceMatch`** — реализованы методы
      `ClickHouseFunctions.window_funnel`/`sequence_match`/`retention` (условия — встроенные `bool`-
      выражения), флаг `SupportsSequenceAggregates`, хук `ISqlDialect.MakeSequenceAggregate`;
      `windowFunnel`/`sequenceMatch` рендерятся с двойными скобками и приводятся через `toInt32(...)`
      (нативные `Integer`/`UInt8` не материализуются), `retention` возвращает `Array(UInt8)` и
      применим только вложенно. Тесты: `SqlGenerationTests.WindowFunnel_*`/`SequenceMatch_*`/
      `Retention_*`, `ClickHouseDialectTests.MakeSequenceAggregate_ShouldMapProviderNames`,
      `Postgres…ClickHouseSequenceAggregates_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.WindowFunnel_ShouldCountConsecutiveConditions`/
      `SequenceMatch_ShouldMatchPattern`/`Retention_ShouldReturnConditionMask` (реальный ClickHouse);
      см. `WIP_clickhouse_sequence_aggregates.md`.
- [ ] **`runningAccumulate`** — higher-order агрегат; сложнее (принимает состояние агрегата).
- [ ] **`multiIf` (многоветвевный `if`)** — нет LINQ-поверхности; нужен метод (напр.
      `ClickHouseFunctions.multi_if`) и трансляция ветвления. Иначе demo-`session_depth` (бакеты
      гистограммы) выражается только через `WithSql`.
- [ ] **`lagInFrame` / `leadInFrame`** — оконные функции; в demo-запросах заменяются на `lag`/`lead`,
      что совпадает лишь при полной сортировке окна. Нужны методы + ветка `WindowFunctionTranslator`.
- [ ] **Доступ к колонкам без свойства сущности** — `IHit` описывает только 6 колонок `hits_v1`;
      `RefererDomain`, `IsMobile`, `IsNotBounce` и др. доступны только через `WithSql`. Нужен способ
      ссылаться на типизированную колонку по имени (или расширить `IHit`).
- [~] **JSON-функции на `String`** — реализованы скалярные `ClickHouseFunctions.json_extract_string`/
      `json_extract_int`/`json_extract_float`/`json_extract_bool`/`json_extract_raw`/`json_has`/
      `json_length`/`json_type` (2 арг: `json`, `path`), флаг `SupportsJsonExtract`, хук
      `MakeJsonExtract` (`json_length` → `toInt64(JSONLength(...))`). По `docs/providers/clickhouse.md`
      `json_extract_string` — тот же скаляр-строка-извлекатель, что и переносимый `json_value`
      (отдельная поверхность не нужна), а ClickHouse-only остаются типизированные
      `JSONExtractInt`/`Float`/`Bool`/`Raw`. Осталось: `JSONExtractKeys`/
      `JSONExtractKeysAndValues`/`JSONExtractArrayRaw` (массивы — нет row reader), `JSON_VALUE`/`JSON_QUERY`
      нового типа `JSON` (subcolumns, уровень 3).
      Тесты: `SqlGenerationTests.JsonExtract_ShouldUseClickHouseNames`,
      `ClickHouseDialectTests.MakeJsonExtract_ShouldMapNamesAndCastLength`,
      `ClickHouseIntegrationTests.JsonExtract_ShouldReadStringJson` (реальный ClickHouse).
- [x] **`visitParamExtract*`** — методы `ClickHouseFunctions.visit_param_extract_string`/`_int`/`_float`/
      `_bool`/`_raw` (`string?`/`long`/`double`/`bool`/`string?`), тот же флаг `SupportsJsonExtract` и хук
      `MakeJsonExtract`, что и `JSONExtract*` (новых хуков нет).
      Тесты: `SqlGenerationTests.VisitParamExtract_ShouldUseClickHouseNames`,
      `ClickHouseIntegrationTests.VisitParamExtract_ShouldReadFlatJson` (реальный ClickHouse).
- [x] **Функции словарей `dictGet` / `dictGetOrDefault` / `dictHas`** — методы `ClickHouseFunctions.
      dict_get`/`dict_get_or_default`/`dict_has` (`TValue?`/`bool`, имена словаря/атрибута — строки),
      флаг `SupportsDictionaries`, хук `MakeDictionaryFunction`, транслятор `DictionarySqlTranslator`.
      `dictGetHierarchy`/`dictGetChildren`/`dictIsIn` (массивы) — вне объёма.
      Тесты: `SqlGenerationTests.DictFunctions_ShouldUseClickHouseNames`,
      `ClickHouseDialectTests.MakeDictionaryFunction_ShouldMapNames` (интеграции нет: нужен
      сконфигурированный словарь).
- [ ] **Кортежи: `tuple`, `tupleElement`, `untuple`** — `tuple(...)` как скаляр и `tupleElement`
      как доступ; требует решения по материализации кортежей.

## Уровень 3. Большое (новая площадь: массивы/JSON/конструкции запроса)

Новая конструкция в модели запроса или снятие «зонтичного» гейта. Оценка: **3+ дней каждое**
(лучше отдельными workstream'ами, как в `docs/specs/roadmap/sql-capabilities-gap-analysis.md`).

- [~] **Массивы ClickHouse** — **первый срез реализован** (`WIP_clickhouse_arrays.md`):
      функции над array-колонками `ClickHouseFunctions.length`/`has`/`index_of`/`has_any`/`has_all`/
      `array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct` и скалярный
      `array_join` (разворачивает строки). Флаги `SupportsArrayFunctions`/`SupportsArrayJoin` (default
      `false`; ClickHouse `true`), хук `MakeArrayFunction` (`length`/`indexOf` → `toInt64(...)`, т.к.
      нативно `UInt64`); array-колонки рендерятся как SQL, захваченный массив — одним параметром
      (`SqlOperandTranslator.AppendArrayOrColumn`/`IsCapturedArray`). Тесты: CH SQL-gen
      (`ArrayLength_…`, `ArrayHas…`, `ArrayJoin_…`, `ArrayStringConcat_WithDefaultDelimiter_…`,
      `ArrayFunction_WithCapturedArray_ShouldBindSingleParameter`),
      `ClickHouseDialectTests.ArrayCapabilities_ShouldBeEnabled`,
      `Postgres…ClickHouseArrayFunctions_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.ArrayFunctions_ShouldReturnValues`/`ArrayStringConcat_WithDefaultDelimiter_…`/
      `ArrayHasAny_WithCapturedArray_…`/`ArrayJoin_ShouldExpandRows` (реальный ClickHouse).
      **Клауза `ARRAY JOIN`/`LEFT ARRAY JOIN`:** `EntityBuilder.ArrayJoin`/`LeftArrayJoin` (+ ковариантные
      `new` на `JoinedEntityBuilder<T1..T8>`), enum `ArrayJoinKind { Inner, Left }`; флаг
      `SupportsArrayJoinClause` + `MakeArrayJoin`; рендер после JOIN'ов до `WHERE`; план-ключ учитывает;
      in-memory бросает. Тесты: `SqlGenerationTests.ArrayJoinClause_…` (+`Left…`, `…MultipleExpressions…`,
      `…OnJoinedQuery…`, `…MixingKinds…`, `…NonSequence…`),
      `Postgres…ArrayJoinClause_UnsupportedByProvider_ShouldThrow`,
      `InMemoryJoinTests.TestArrayJoinClause_ShouldThrow`,
      `ClickHouseIntegrationTests.ArrayJoinClause_ShouldExpandRowsAndDropEmptyArrays`/
      `LeftArrayJoinClause_ShouldKeepEmptyArrays` (реальный ClickHouse).
      **Привязка вырожденного элемента (A1):** `EntityBuilder.ArrayJoinElement`/`LeftArrayJoinElement`
      возвращают `EntityBuilder<ArrayJoinProjection<TEntity, TElement>>` (public marker
      `IArrayJoinProjection`, `Item1` — исходная сущность, `Element` — вырожденный элемент); выражение
      клаузы алиасится (`as __nextorm_aj_element`), `MemberTranslator` транслирует `p.Element` в этот
      алиас; `SourceEntityType`/`BindArrayJoinElement` протянуты через `QueryDefinition`/`QueryCommand`/
      clone/план-ключ. Первый срез: один источник без join'ов, `Where`/`Having` — после вызова, in-memory
      бросает (проверка до компиляции материализатора). Тесты: CH SQL-gen `ArrayJoinElement_…` (+`Left…`,
      `…WhereOnElement…`, `…OrderByElement…`, `…AfterCondition…ShouldThrow`, `…SecondCall…ShouldThrow`),
      `Postgres…ArrayJoinElement_UnsupportedByProvider_ShouldThrow`,
      `InMemoryJoinTests.TestArrayJoinElement_ShouldThrow`,
      `ClickHouseIntegrationTests.ArrayJoinElement_ShouldBindExpandedElement`/`…ShouldFilterOnElement`/
      `LeftArrayJoinElement_ShouldKeepEmptyArrayWithDefaultElement`.
      **Осталось:** higher-order (`arrayMap`/`arrayFilter`/`arrayExists`/`arrayAll`/`arrayCount`,
      `arrayFirst*`);       привязка элемента для нескольких массивов/join'ов (сейчас один источник без
     `join`); row reader `Array(T)`/`Tuple` (`groupArray`/`topK`/`quantiles`/`JSONExtractArrayRaw`);
     `arraysZip`. Маппинг `string.Split` на `splitByChar` реализован
     (`SupportsStringSplit` + `MakeStringSplit`; тесты `SqlGenerationTests.Split_*`,
     `ClickHouseIntegrationTests.Split_ShouldCountParts`; см. `WIP_clickhouse_string_split.md`).
     **Скалярные array-функции `range`/`arrayEnumerate`/`arrayCumSum`/`arraySlice`/`arrayPushBack`
     реализованы** (`ClickHouseFunctions` + `ArraySqlTranslator`, тот же гейт
     `SupportsArrayFunctions`; проекция массива по-прежнему упирается в row reader, поэтому функции
     применимы вложенно — `length(range(...))`, `arrayStringConcat(arrayCumSum(...), ',')`).
     Тесты: `SqlGenerationTests.ArrayRange_*`/`ArrayEnumerate_*`/`ArrayCumSum_*`/`ArraySlice_*`/
     `ArrayPushBack_*`, `Postgres…ClickHouseArrayScalarFunctions_UnsupportedByProvider_ShouldThrow`,
     `ClickHouseIntegrationTests.ArrayScalarFunctions_ShouldReturnValues`; см.
     `WIP_clickhouse_array_scalar_functions.md`.
- [~] **JSON-тип ClickHouse** — JSONPath-скаляры по строковому JSON `JSON_VALUE`/`JSON_QUERY`/
      `JSON_EXISTS` реализованы: `ClickHouseFunctions.json_value`/`json_query`/`json_exists` (тот же гейт
      `SupportsJsonExtract`, имена маппит `MakeJsonExtract`); `TextJsonSqlTranslator` и
      `JsonSqlTranslator` получили guard по `DeclaringType`, иначе одноимённые методы SQL Server /
      PostgreSQL перехватывали вызов.
      Тесты: `SqlGenerationTests.JsonPathFunctions_ShouldUseClickHouseNames`,
      `ClickHouseDialectTests.MakeJsonExtract_ShouldMapJsonPathNames`,
      `Postgres…JsonPath_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.JsonPath_ShouldReadStringJson` (реальный ClickHouse);
      `docs/providers/clickhouse.md`.
      Осталось: нативный тип `JSON` (колонка/подколонки), `JSONAllPaths`/`JSONAllPathsWithTypes`,
      `JSONExtractKeys`, `JSONExtractKeysAndValuesRaw`, `JSONExtractArrayRaw`, `toJSONString` — требуют
      описания типа колонки или row reader массивов/кортежей (пересекается с workstream'ом массивов).
- [x] **`LIMIT n BY expr`** — `EntityBuilder.LimitBy(limit, exp)` (+ перегрузка с `offset`, ключ —
      колонка или анонимный тип) + внутренний `LimitByClause`; флаг `SupportsLimitBy` и хук
      `MakeLimitBy` (ClickHouse → `limit [offset, ]n by cols`, эмитится после `ORDER BY` перед
      финальным `LIMIT`); план-ключ учитывает клаузу; in-memory и прочие провайдеры бросают
      `NotSupportedException`.
      Тесты: `SqlGenerationTests.LimitBy_ShouldAppendClause` (+`…WithOffsetAndOrderBy…`,
      `…WithComputedKey_ShouldNotAlias`), `ClickHouseDialectTests.MakeLimitBy_ShouldRenderClause`,
      `Postgres…LimitBy_ShouldThrowBecausePostgresHasNoLimitBy`,
      `InMemoryTests.LimitBy_ShouldThrow`,
      `ClickHouseIntegrationTests.LimitBy_ShouldTakeTopNPerKey` (реальный ClickHouse).
- [x] **`GROUP BY ... WITH TOTALS`** — `EntityBuilder.WithTotals()` + флаг `SupportsGroupByWithTotals` +
      хук `MakeGroupByTotals` (ClickHouse → `"… with totals"`); `QueryDefinition`/`QueryCommand`/
      `EntityBuilder`/ключ плана/`SqlBuilder` проброшены. С `GROUPING SETS` не сочетается.
      Тесты: `SqlGenerationTests.GroupByWithTotals_ShouldAppendModifier` (+`…RollupWithTotals…`),
      `ClickHouseDialectTests.MakeGroupByTotals_ShouldAppendTotals`,
      `ClickHouseIntegrationTests.GroupByWithTotals_ShouldExecute` (ограничение драйвера — в
      `docs/providers/clickhouse.md`).
- [x] **`FINAL`, `SAMPLE k`, `PREWHERE`, `SETTINGS`** — методы `EntityBuilder.Final()`,
      `Sample(ratio[, offset])`, `PreWhere(predicate)`, `Settings(("key","value"), …)`; флаги
      `SupportsFinal`/`SupportsSample`/`SupportsPreWhere`/`SupportsSettings` и хуки
      `MakeFinal`/`MakeSample`/`MakeSettings`; порядок `FINAL`/`SAMPLE` (после таблицы) → `PREWHERE` →
      `WHERE` → `…` → `SETTINGS` (в конце); план-ключ и `PreWhere`-подготовка (по образцу `WHERE`).
      `FINAL`/`PREWHERE` требуют поддерживающего движка таблицы (Memory не поддерживает).
      Тесты: `SqlGenerationTests.Final_/Sample_/PreWhere_/Settings_*`,
      `ClickHouseDialectTests.MakeQueryModifiers_ShouldRender`,
      `Postgres…QueryModifiers_ShouldThrowBecausePostgresHasNone`,
      `InMemoryTests.QueryModifiers_ShouldThrow`,
      `ClickHouseIntegrationTests.QueryModifiers_ShouldExecute` (в интеграции — только `SETTINGS`).
- [x] **`GLOBAL IN`** — методы `ClickHouseFunctions.global_in` (подзапрос и список значений), флаг
      `SupportsGlobalPredicates`; рендер ` global in (` в `NormSqlTranslator`/`InValuesTranslator`
      (отрицание — C# `!` → `GLOBAL NOT IN`). `CorrelatedQueryExpressionVisitor` расширен на
      `ClickHouseFunctions`, иначе коррелированный подзапрос ломал вызов.
      Тесты: `SqlGenerationTests.GlobalIn_Subquery_/Values_ShouldRenderGlobalIn`,
      `ClickHouseDialectTests.CapabilityFlags_ShouldMatchClickHouse`,
      `Postgres…GlobalIn_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.GlobalIn_Subquery_/Values_ShouldFilter` (реальный ClickHouse);
      `docs/providers/clickhouse.md`.
- [x] **`GLOBAL JOIN`** — `JoinExpression.IsGlobal`, флаг `SupportsGlobalJoin` (default `false`;
      ClickHouse `true`), `MakeJoinKeyword(JoinType, JoinStrictness, bool isGlobal)` рендерит
      `global [type] [strictness] join`; модификатор `EntityBuilder.Global()` (copy-on-write, общий
      `ReplaceLastJoin` с `WithStrictness`) + ковариантные `new Global()` на `JoinedEntityBuilder<T1..T8>`.
      Комбинируется со strictness (`global left any join`); прочие провайдеры и in-memory бросают.
      Тесты: `SqlGenerationTests.GlobalJoin_ShouldRenderGlobalModifier`/`GlobalJoin_ShouldNotMutateSourceBuilder`,
      `ClickHouseDialectTests.JoinStrictness_ShouldRenderClickHouseModifiers`,
      `Postgres…GlobalJoin_UnsupportedByProvider_ShouldThrow`,
      `InMemoryJoinTests.TestGlobalJoin_ShouldThrow`,
      `ClickHouseIntegrationTests.GlobalJoin_ShouldExecute` (реальный ClickHouse);
      `docs/providers/clickhouse.md`.
- [~] **Join'ы ClickHouse: `ANY`/`ALL`/`ASOF` (готово), `SEMI`/`ANTI`, `PASTE JOIN` (осталось)** —
      реализованы `ANY`/`ALL`/`ASOF`: enum `JoinStrictness`, мутирующий модификатор
      `EntityBuilder<TEntity>.WithStrictness(strictness)` (на `JoinedEntityBuilder<T1..T8>` —
      ковариантные `new`-перегрузки, чтобы сохранить плоскую цепочку join'ов), флаг
      `SupportsJoinStrictness` + хук `ISqlDialect.MakeJoinKeyword(JoinType, JoinStrictness)`;
      `SqlSourceRenderer.MakeJoin` отклоняет модификатор у провайдера без флага и на
      `CROSS`/`APPLY`-join'ах; in-memory бросает `NotSupportedException`.
      Тесты: `ClickHouseDialectTests.JoinStrictness_ShouldRenderClickHouseModifiers`,
      `SqlGenerationTests.JoinWithStrictness_/JoinStrictness_OnCrossJoin_/WithStrictness_WithoutJoin_*`,
      `Postgres…JoinStrictness_UnsupportedByProvider_ShouldThrow`,
      `InMemoryJoinTests.TestJoinStrictness_ShouldThrow`,
      `ClickHouseIntegrationTests.AnyStrictness_/AllStrictness_/AsofJoin_*` (реальный ClickHouse);
      `WIP_clickhouse_join_strictness.md`.
      Осталось: `SEMI`/`ANTI` (меняют набор колонок — несовместимо с `Projection<T1,T2>`, нужен
      результат только с левыми колонками) и `PASTE JOIN` (нет `ON`, cross-подобный).
      ClickHouse-синтаксис: `[INNER|LEFT|RIGHT|FULL] [ANY|ALL|ASOF|SEMI|ANTI] JOIN`.
- [x] **`numbers` / `numbers_mt`** — методы `ClickHouseFunctions.numbers(count)`/`numbers(start,stop[,
      step])`/`numbers_mt(count)`, row-тип `SqlFunctions.INumbersRow`; `SupportsTableFunction("numbers"/
      "numbers_mt")`; хук `ISqlDialect.WrapTableFunction` (ClickHouse оборачивает вызов в
      `(select toInt64(number) as number from numbers(...))`, т.к. нативный `UInt64` не материализуется).
      Тесты: `SqlGenerationTests.TableFunction_Numbers_/NumbersMt_ShouldEmitCall`,
      `ClickHouseDialectTests.SupportsTableFunction_ShouldMatchClickHouse`,
      `Postgres…BuiltInTableFunction_Numbers_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.NumbersTableFunction_ShouldReturnThreeRows` (реальный ClickHouse).
- [x] **`zeros` / `zeros_mt`** — row-тип `SqlFunctions.IZerosRow` (колонка `zero UInt8` → CLR `byte`),
      методы `ClickHouseFunctions.zeros`/`zeros_mt` + `SupportsTableFunction("zeros"/"zeros_mt")`;
      `WrapTableFunction` не нужен (`UInt8` материализуется напрямую).
      Тесты: `SqlGenerationTests.TableFunction_Zeros_/ZerosMt_ShouldEmitCall`,
      `ClickHouseDialectTests.SupportsTableFunction_ShouldMatchClickHouse`,
      `Postgres…BuiltInTableFunction_Zeros_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.ZerosTableFunction_ShouldReturnThreeRows` (реальный ClickHouse).
      См. `WIP_clickhouse_table_functions_remaining.md`.
- [x] **`generateRandom`** — row-тип `SqlFunctions.IGenerateRandomRow` (`id UInt64`, `value Float64`,
      `name String`), методы `ClickHouseFunctions.generate_random()`/`generate_random(long seed)` +
      `SupportsTableFunction("generateRandom")`; `WrapTableFunction` подставляет фиксированную структуру
      (нативная схема ClickHouse — динамическая строка) и оборачивает вывод в
      `(select toInt64(id) as id, value, name from generateRandom('id UInt64, value Float64, name String'[, seed]))`,
      т.к. нативный `UInt64` не материализуется. Поток бесконечен — нужен `Page`/`First`.
      Тесты: `SqlGenerationTests.TableFunction_GenerateRandom_/…WithSeed_Should…`,
      `ClickHouseDialectTests.SupportsTableFunction_ShouldMatchClickHouse`/`WrapTableFunction_…`,
      `Postgres…BuiltInTableFunction_GenerateRandom_UnsupportedByProvider_ShouldThrow`,
      `ClickHouseIntegrationTests.GenerateRandomTableFunction_ShouldReturnRequestedRows` (реальный ClickHouse).
      См. `WIP_clickhouse_generate_random.md`.
- [ ] **Прочие табличные функции ClickHouse** — `values` (динамическая схема из
      строки — не выражается статическим `IQueryable<T>`), `url`/`s3`/`remote`/`file`/`format`/`merge`/
      `input` (нужна конфигурация сервера), `cluster`/`clusterAllReplicas` (нужен кластер),
      `system.numbers` (покрыт табличной функцией `numbers`), `system.one` (валиден только
      `FROM system.one` как системная таблица; `system.one()` даёт `Code: 46 Unknown table function
      system.one`). Для любой из них пользователь может объявить свою `[SqlTableFunction]`-обёртку.

## Инфраструктура тестов

- [x] **ClickHouse-контейнер в integration-тестах** — добавлены `ClickHouseContainer` (Testcontainers,
      образ `clickhouse/clickhouse-server:25.8-alpine`, переменная `NEXTORM_CLICKHOUSE_CONNECTION`),
      `ClickHouseTestProvider` и `ClickHouseIntegrationTests` (13 тестов: `dateTrunc`, `addDays`,
      `DateTime.Add*`, `toLastDayOfMonth`, `arrayStringConcat(groupArray(...))`, `groupBit*`,
      `corr`/`covarPop`/`covarSamp`, `argMin`/`argMax`, `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`).
      Провайдер намеренно не подключён к `CommonTestSuite` (у ClickHouse нет многих переносимых
      возможностей); вместо этого — отдельный класс по образцу `PostgresSpecificTests`.
      Тесты гоняются с `DOCKER_HOST`, иначе скипаются. Затронуто: `Directory.Packages.props`,
      csproj интеграционных тестов, `DatabaseContainers.cs`.
- [x] **Попутно найден и исправлен баг пейджинга ClickHouse**: драйвер превращает
      `CommandBehavior.SingleRow` в дополнительный `LIMIT 1`, а диалект уже рисует `limit 1` для
      `First()` → двойной `LIMIT`. Добавлен флаг `ISqlDialect.SupportsCommandBehaviorSingleRow`
      (по умолчанию `true`, ClickHouse — `false`), который проверяется в `QueryPlanner`.

## Вне области по дизайну (read-only)

**DML** (`INSERT` / `ALTER TABLE ... UPDATE` / `DELETE` / `MERGE`), `SaveChanges` / change tracking /
транзакции уровня ORM — за рамками проекта на текущий момент: nextorm — read-only. Для вставок
использовать binary insert API драйвера (`docs/providers/clickhouse.md:80`).

Не реализуется намеренно (`docs/advanced/limitations.md:19`):

- [ ] **Навигационные свойства и связи** — только явные join.
- [ ] **`OPTIMIZE`, `ALTER`, `RENAME`, DDL** и прочая административная поверхность.

## Приоритетный шортлист

Уровни 1–2 закрыты в сессии 19.09.2026 (осталось только то, что зависит от row reader для массивов —
`groupArray`/`topK`/`quantiles`). В работе — уровень 3, по одному workstream'у за изменение.

1. [x] `date_trunc` + `date_add`/`Add*`/`end_of_month` — снимает самые частые «даты» (уровень 1).
2. [x] `uniq`/`uniqExact`/`uniqCombined`/`uniqHLL12` (уровень 2) + `countIf`/`sumIf` — ключевые агрегаты
   ClickHouse (уровень 1, `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf` готовы).
3. [x] `corr`/`covar*`, `bit_and`→`groupBit*`, `argMin`/`argMax` (уровень 1).
4. [x] `quantile`/`median` + `JSONExtract*`/`visitParamExtract*` + `dictGet*` + `any_agg`/`any_last`
   (уровень 2). `any_agg` промоутнут в кросс-провайдерный `CommonFunctions` (`SupportsAnyValueAggregate`),
   на ClickHouse остался `any_last`. Остаются `groupArray`/`topK`/`quantiles` — зависят от row reader
   для массивов.
5. [~] Level 3 закрыто: `GROUP BY ... WITH TOTALS`, `LIMIT n BY`, `FINAL`/`PREWHERE`/`SAMPLE`/`SETTINGS`,
   `numbers`/`zeros`, `GLOBAL IN`, `GLOBAL JOIN`, JSONPath-скаляры, join-strictness `ANY`/`ALL`/`ASOF`,
   массивы (функции над array-колонками + скалярный `arrayJoin` + клауза `[LEFT] ARRAY JOIN`).
   Осталось: higher-order array-функции, row reader массивов,
   нативный JSON-тип, `SEMI`/`ANTI`/`PASTE`, `groupArray`/`topK`/`quantiles`.

## Оценка объёма (сводка)

| Уровень | Что | Файлов на функцию | Срок |
|---|---|---|---|
| 1 | Флаг/хук диалекта (`date_trunc`, `date_add`, `string_agg`, `groupBit*`, `corr`, `argMin/Max`) | 6–8 | 0.5–1 д. |
| 2 | Новый API `SqlFunctions.Sql` + транслятор (`uniq`, `topK`, `quantile`, `JSONExtract*`, `dictGet`) | 8–12 | 1–3 д. |
| 2 | Комбинаторы `-If` через хук `MakeAggregateFilter` | 5–7 | 1–2 д. |
| 3 | Массивы (разделение флагов + имена + `arrayJoin`) | 15+ | 4–6 д. |
| 3 | `ARRAY JOIN` / `LIMIT BY` / `FINAL` / `PREWHERE` / `SAMPLE` / `SETTINGS` | 10+ | 3–6 д. |
| 3 | `ASOF`/`ANY`/`SEMI`/`ANTI` join | 10+ | 3–5 д. |
| — | ClickHouse в integration-тестах (Testcontainers) | 4–5 | 1–2 д. |

Кросс-факторы (из `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, «Cross-cutting requirements»): extend-only API,
тесты до «done», пулинг `StringBuilder` на горячем пути, CRLF, и обязательное обновление **EN+RU**
документации в том же изменении.
