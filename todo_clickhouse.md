# TODO: недостающие функции ClickHouse

Черновик списка популярных функций/конструкций ClickHouse, которых пока нет в nextorm.
Отсортирован **по сложности реализации** (от простого к сложному). В конце — то, что отсутствует
по замыслу (read-only построитель `SELECT`) и сводная оценка объёма.

Источник — код `1.0.3-alpha`, `src/nextorm.clickhouse/ClickHouseDialect.cs`,
`src/nextorm.core/Visitors/*Translator.cs`, `docs/sql-capabilities-gap-analysis.md`,
`docs/advanced/limitations.md`, `docs/providers/clickhouse.md`.

> Обходной путь уже сегодня: любую скалярную/табличную функцию можно объявить самому через
> `[SqlFunction("name")]` / `[SqlTableFunction("name")]`. Это не покрывает операторный синтаксис
> (`->`, `ARRAY JOIN`, `FINAL`, `LIMIT BY`), комбинаторы (`-If`/`-Array`/`-State`/`-Merge`) и
> агрегаты с особым синтаксисом (`quantile(0.5)(x)`).

## Как устроено добавление (шпаргалка)

Одна встроенная функция обычно затрагивает 6–8 файлов:

1. `src/nextorm.core/Query/NORM.cs` — новый метод `NORM_SQL` (публичный API, extend-only).
2. Транслятор:
   - `Visitors/BuiltinFunctionTranslator.cs` — `nullif`/`greatest`/`date_trunc`/`string_agg`;
   - `Visitors/AdvancedAggregateTranslator.cs` — редкие агрегаты (bool/bit/stat/ordered);
   - `Visitors/ExtendedScalarFunctionTranslator.cs` — расширенные скаляры;
   - `Visitors/ArraySqlTranslator.cs` / `Visitors/JsonSqlTranslator.cs` — массивы/JSON;
   - `Visitors/ScalarFunctionTranslator.cs` — `string`/`Math`/`DateTime`.
3. `DataContext/Dialect/ISqlDialect.cs` + `SqlDialectBase.cs` — флаг `Supports*` (по умолчанию
   `false`) и/или хук `Make*`.
4. `src/nextorm.clickhouse/ClickHouseDialect.cs` — включить флаг, переопределить хук/имя.
5. Тесты: `test/nextorm.clickhouse.tests/SqlGenerationTests.cs` (SQL без БД),
   `ClickHouseDialectTests.cs` (прямые хуки).
6. Документация **EN + RU**: `docs/guide/11-scalar-functions.md`, `docs/guide/04-grouping-and-aggregates.md`,
   `docs/providers/clickhouse.md`, `docs/providers/overview.md` (матрица),
   `docs/advanced/limitations.md`, `docs/sql-capabilities-gap-analysis.md`,
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
- [x] **`string_agg`** — `SupportsStringAgg => true` + `MakeStringAgg` →
      `arrayStringConcat(groupArray(x), delim)`. Отличие семантики (порядок/`NULL`) задокументировано.
- [x] **`bit_and` / `bit_or` / `bit_xor`** → `groupBitAnd`/`groupBitOr`/`groupBitXor` через
      `MakeAggregate`; `SupportsBitAggregates => true`.
- [x] **`corr` / `covar_pop` / `covar_samp`** → `corr`/`covarPop`/`covarSamp`;
      `SupportsStatisticalAggregates => true`. Добавлен отдельный флаг
      `SupportsRegressionAggregates` (PostgreSQL), чтобы `regr_*` продолжали бросать
      `NotSupportedException` в ClickHouse.
- [x] **`argMin` / `argMax`** — методы `NORM_SQL.arg_min`/`arg_max`, ветка в
      `AdvancedAggregateTranslator`, флаг `SupportsArgMinMax`; `MakeAggregate` → `argMin`/`argMax`.
- [x] **`countIf` / `sumIf` / `avgIf` / `minIf` / `maxIf`** — реализован **вариант A**: методы
      `NORM_SQL.count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` с `Expression<Func<bool>>`, флаг
      `SupportsIfAggregates`, `MakeAggregate` → `countIf`/`sumIf`/…. Предикат рендерит
      `AggregateFilter.AppendPredicate` (без обёртки `filter (where ...)`). Вариант B (обобщённый
      хук для ANSI `FILTER` → `-If`) оставлен на будущее.
- [ ] **Скалярные `startsWith` / `endsWith` / `position` / `length` над массивами** — часть уже
      эмулируется через `LIKE`/`lengthUTF8`; можно добавить явные методы-обёртки. Мелочь.

## Уровень 2. Среднее (новый API `NORM.SQL` + транслятор)

Новая площадь API/family, но модель запроса уже существует. Оценка: **1–3 дня каждое**.

- [ ] **`uniq` / `uniqExact` / `uniqCombined` / `uniqHLL12`** — самые популярные агрегаты
      ClickHouse. Флаг `SupportsUniqAggregates`, методы `NORM_SQL.uniq`/`uniq_exact`/…, рендер
      `uniq(x)`. В PostgreSQL аналог — `count(distinct)`, поэтому флаг только для CH.
- [ ] **`groupArray` / `groupUniqArray`** — возвращают массив; до использования нужен row reader
      для массивов (или ограничиться вложенными выражениями, как `array_agg`). Флаг
      `SupportsArrayAggregateFunctions`, поэтому зависит от работ по массивам (уровень 3).
- [ ] **`topK` / `topKWeighted`** — возвращают массив; та же зависимость от row reader.
- [ ] **`quantile` / `quantiles` / `median` (`quantileExact`, `quantileTiming`, …)** —
      параметрический агрегат с двойными скобками: `quantile(0.5)(x)`. Текущий
      `MakeWithinGroup` (PostgreSQL `WITHIN GROUP`) не подходит — нужен отдельный рендер
      `MakeQuantile(level, value)`. Методы `NORM_SQL.quantile`/`median`/`quantiles`.
- [ ] **`any` / `anyLast` (агрегаты)** — конфликт имени с `NORM_SQL.any` (квантор массива/подзапроса).
      Предложение: `any_agg`/`anyLast` (или `NORM.SQL.ch_any`), чтобы не ломать существующий API.
- [ ] **`groupBitAnd` / `groupBitOr` / `groupBitXor`** — см. уровень 1 (если не маппить на
      `bit_and`/…, а завести отдельные методы).
- [ ] **`windowFunnel` / `retention` / `sequenceMatch`** — «продвинутые» агрегаты; рендерятся как
      обычные функции с несколькими аргументами, но требуют аккуратной типизации (DateTime/условия).
- [ ] **`runningAccumulate`** — higher-order агрегат; сложнее (принимает состояние агрегата).
- [ ] **JSON-функции на `String` (или новом типе `JSON`)** — `JSONExtractString`, `JSONExtractInt`,
      `JSONExtractFloat`, `JSONExtractBool`, `JSONExtractRaw`, `JSONExtractArrayRaw`, `JSONHas`,
      `JSONLength`, `JSONType`, `JSONExtractKeys`, `JSONExtractKeysAndValues`, `JSON_VALUE`/`JSON_QUERY`.
      Не ложатся на PostgreSQL-поверхность `json`/`->`/`@>`: нужен отдельный флаг
      `SupportsJsonExtract` (строковый JSON) и собственный family методов `NORM_SQL.json_extract_*`.
- [ ] **`visitParamExtract*`** — быстрый разбор валидного JSON из строки; тот же family, что и выше.
- [ ] **Функции словарей `dictGet` / `dictGetOrDefault` / `dictHas` / `dictGetHierarchy`** — важная
      фича ClickHouse. Скалярные, можно завести методы `NORM_SQL.dict_get` + флаг
      `SupportsDictionaries`; сложность — в типизации и в именах атрибутов/словарей.
- [ ] **Кортежи: `tuple`, `tupleElement`, `untuple`** — `tuple(...)` как скаляр и `tupleElement`
      как доступ; требует решения по материализации кортежей.

## Уровень 3. Большое (новая площадь: массивы/JSON/конструкции запроса)

Новая конструкция в модели запроса или снятие «зонтичного» гейта. Оценка: **3+ дней каждое**
(лучше отдельными workstream'ами, как в `sql-capabilities-gap-analysis.md`).

- [ ] **Массивы ClickHouse** — сейчас всё array-поверхность привязана к единому
      `SupportsArrays => false` (только PostgreSQL). Для ClickHouse нужно:
      1. **Разделить флаг**: `SupportsArrayParameters` (привязка `T[]` к `{p:Array(...)}`) и
         `SupportsArrayFunctions` (`arrayMap`, `has`, `indexOf`, …). Привязка массивов-параметров в
         ClickHouse.Driver требует явного типа (`Array(Int64)`), а `null`/`Nullable` типы не
         выводятся из CLR-значения (см. `docs/providers/clickhouse.md:82`) — рискованный пункт.
      2. **Маппинг имён**: `cardinality`→`length`, `array_position`→`indexOf`, `array_to_string`→
         `arrayStringConcat`, `array_contains`→`has`, `array_overlaps`/`@>`/`&&`→`hasAny`/`hasAll`,
         `array_sort`→`arraySort`, `array_reverse`→`arrayReverse`, `array_distinct`→`arrayDistinct`.
      3. **Новые семейства**: `arrayMap`, `arrayFilter`, `arrayExists`/`arrayAll`/`arrayCount`,
         `arrayFirst`/`arrayFirstIndex`, `arrayJoin`, `has`/`hasAny`/`hasAll`, `indexOf`, `range`,
         `splitByChar`/`splitByString`/`splitByRegexp`, `arrayStringConcat`, `arrayEnumerate`,
         `arrayCumSum`, `arraySlice`, `arrayPushBack`/`PopFront`, `arraysZip`.
      4. **`arrayJoin` / `ARRAY JOIN` / `LEFT ARRAY JOIN`** — это **не скаляр**, а расширение строк
         (lateral): нужен новый элемент `FROM`/join в `SqlBuilder`/`EntityBuilder`/`JoinType`.
      Самый крупный workstream; пересекается с `row reader` для массивов.
- [ ] **JSON-тип ClickHouse** (`JSON`, subcolumns, `JSON_VALUE`/`JSON_QUERY`, `JSONAllPaths`,
      `toJSONString`, `JSONExtractKeysAndValuesRaw`) — отдельный флаг `SupportsJsonType`, не путать
      со строковым `JSONExtract` из уровня 2 (у PostgreSQL `SupportsJson` уже занят под `jsonb`).
- [ ] **`LIMIT n BY expr`** — новый paging/limit API + хук диалекта (сейчас `MakePage` знает только
      `limit`/`offset`).
- [ ] **`GROUP BY ... WITH TOTALS`** — расширение `MakeGrouping`/`GroupingType` + API `GroupBy`.
- [ ] **`FINAL`, `SAMPLE k`, `PREWHERE`, `SETTINGS`, `GLOBAL IN`, `GLOBAL JOIN`** — модификаторы
      уровня запроса; логичнее всего через точку расширения, аналогичную `Hint`/`RenderQueryHints`
      (`QueryCommand` + `SqlBuilder` + `ISqlDialect`).
- [ ] **Join'ы ClickHouse: `ASOF JOIN`, `ANY`/`ALL` strictness, `SEMI`/`ANTI`, `PASTE JOIN`** —
      расширение `JoinType` + `SqlBuilder.MakeJoin` + in-memory/диалекты.
- [ ] **Встроенные табличные функции** — `numbers`/`numbers_mt`, `zeros`, `generateRandom`,
      `url`, `s3`, `remote`/`remoteSecure`, `cluster`/`clusterAllReplicas`, `file`, `values`, `merge`,
      `format`, `system.numbers`, `system.one`. Механизм `[SqlTableFunction]` + `FromTableFunction`
      уже есть — это в основном row-типы + объявления (дешевле, чем массивный workstream).
- [ ] **`INSERT`** — для ClickHouse особенно значим (вставки — хлеб ClickHouse). Вне области:
      nextorm — read-only; для вставок использовать binary insert API драйвера
      (`docs/providers/clickhouse.md:80`).

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

## Вне области по дизайну (read-only, без change tracking)

Не реализуется намеренно (`docs/advanced/limitations.md:19`):

- [ ] **DML**: `INSERT` / `ALTER TABLE ... UPDATE` / `DELETE` / `MERGE`.
- [ ] **Навигационные свойства и связи** — только явные join.
- [ ] **Change tracking / транзакции уровня ORM**.
- [ ] **`OPTIMIZE`, `ALTER`, `RENAME`, DDL** и прочая административная поверхность.

## Приоритетный шортлист

1. [x] `date_trunc` + `date_add`/`Add*`/`end_of_month` — снимает самые частые «даты» (уровень 1).
2. [~] `uniq`/`uniqExact` (уровень 2) + `countIf`/`sumIf` — ключевые агрегаты ClickHouse (уровень 1,
   `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf` готовы).
3. [x] `corr`/`covar*`, `bit_and`→`groupBit*`, `argMin`/`argMax` (уровень 1).
4. [ ] `quantile`/`median`, `groupArray`, `topK` (уровень 2; зависят от row reader для массивов).
5. [ ] `JSONExtract*` и `dictGet*` (уровень 2; отдельные family/флаги).
6. [ ] Массивы + `ARRAY JOIN` (уровень 3, отдельный workstream).

## Оценка объёма (сводка)

| Уровень | Что | Файлов на функцию | Срок |
|---|---|---|---|
| 1 | Флаг/хук диалекта (`date_trunc`, `date_add`, `string_agg`, `groupBit*`, `corr`, `argMin/Max`) | 6–8 | 0.5–1 д. |
| 2 | Новый API `NORM.SQL` + транслятор (`uniq`, `topK`, `quantile`, `JSONExtract*`, `dictGet`) | 8–12 | 1–3 д. |
| 2 | Комбинаторы `-If` через хук `MakeAggregateFilter` | 5–7 | 1–2 д. |
| 3 | Массивы (разделение флагов + имена + `arrayJoin`) | 15+ | 4–6 д. |
| 3 | `ARRAY JOIN` / `LIMIT BY` / `FINAL` / `PREWHERE` / `SAMPLE` / `SETTINGS` | 10+ | 3–6 д. |
| 3 | `ASOF`/`ANY`/`SEMI`/`ANTI` join | 10+ | 3–5 д. |
| — | ClickHouse в integration-тестах (Testcontainers) | 4–5 | 1–2 д. |

Кросс-факторы (из `sql-capabilities-gap-analysis.md`, «Cross-cutting requirements»): extend-only API,
тесты до «done», пулинг `StringBuilder` на горячем пути, CRLF, и обязательное обновление **EN+RU**
документации в том же изменении.
