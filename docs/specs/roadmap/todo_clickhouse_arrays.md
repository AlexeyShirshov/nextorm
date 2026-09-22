# TODO: Массивы ClickHouse — row reader `Array(T)`/`Tuple` и остаток

> Рабочий план. Источник: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` §4 п.4 (row reader),
> п.5 (higher-order), п.9 (скаляры над массивами).
> **Статус: срез 1 (row reader) — готово (влито в дерево); срезы 2–4 — открыты.**

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

## Остаток (срезы 2–4)

### Срез 2 (gap §4 п.6, отдельный todo): `topK`/`topKWeighted`/`quantiles` — параметризованные агрегаты
Требуют renderer по образцу `IQuantileAggregateRenderer`; вынести в
`todo_clickhouse_aggregate_function_state.md`-родственный, но это массивы — оставить в этом файле.

### Срез 3 (gap §4 п.5): higher-order/lambda
`arrayMap`/`arrayFilter`/`arrayExists`/`arrayAll`/`arrayCount`/`arrayFirst*` — трансляция
lambda/higher-order аргумента; не начато.

### Срез 4 (gap §4 п.9): скаляры над массивами
`position` → `indexOf`, `length` → `length`, префикс/суффикс → `hasSubstr`/`arraySlice`. Требует
разделения гейта на `SupportsArrayParameters`/`SupportsArrayFunctions` (для binding `T[]`-параметров
с явным типом) и срезов 1–2.

### Прочее
- `tuple`/`tupleElement`/`untuple` — скалярная поверхность над `Tuple` (после среза 1).
- Возвращающие массивы `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`.
- `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`.
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
