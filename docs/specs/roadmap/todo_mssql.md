# TODO: недостающие функции MS SQL Server

> Незаблокированный остаток Phase 2 сведён в `todo_phase2.md`.

Черновик списка популярных функций/конструкций SQL Server, которых пока нет в nextorm.
Отсортирован **по сложности реализации** (от простого к сложному). В конце — то, что отсутствует
по замыслу (read-only построитель `SELECT`).

Источник — код `1.0.3-alpha` и документы
[`docs/specs/roadmap/sql-capabilities-gap-analysis.md`](sql-capabilities-gap-analysis.md),
[`docs/advanced/limitations.md`](../../advanced/limitations.md),
[`docs/providers/sqlserver.md`](../../providers/sqlserver.md).

## Уже реализовано (для контекста)

- Paging: `TOP(n)` и `OFFSET … FETCH` (+ инъекция `ORDER BY`), `count_big`.
- `CROSS APPLY` / `OUTER APPLY` (без корреляции с внешней строкой).
- Query hints: `Hint(...)` → `OPTION (...)`, `MAXRECURSION` для рекурсивных CTE.
- Join всех типов, arity 2–8; `CASE`/ternary/`switch`; `COALESCE`; числовой `CAST`.
- Строки: `LIKE`, `Contains`/`StartsWith`/`EndsWith`, `ToUpper`/`ToLower`,
  `Trim`/`TrimStart`/`TrimEnd`, `Substring`, `Replace`, `Length`; конкатенация `+`.
- Математика: `abs`, `ceiling`, `floor`, `round`, `sqrt`, `pow`, `exp`, `log` (1 арг.),
  `sin`, `cos`, `tan`, `sign`, `trunc` (`round(x, 0, 1)`).
- Даты: `datepart` для `year`/`month`/`day`/`hour`/`minute`/`second`, `getdate`/`getutcdate`.
- Агрегаты: `count`, `count_big`, `count_distinct`, `min`, `max`, `avg`, `sum`,
  `stdev`, `stdevp`, `var`, `varp` (+ `distinct`).
- Окна: `row_number`, `rank`, `dense_rank`, `ntile`, `lag`, `lead`, `first_value`,
  `last_value`, `sum_over`/`avg_over`/`min_over`/`max_over`/`count_over`, фреймы `ROWS`/`RANGE`.
- CTE (в т.ч. рекурсивные), `UNION`/`INTERSECT`/`EXCEPT`, `DISTINCT`.
- UDF (`[SqlFunction]`) и TVF (`[SqlTableFunction]`), сырой SQL на весь запрос (`WithSql`).
- `greatest`/`least` (2022+), `date_trunc` → `datetrunc` (2022+), `string_agg` (2017+) — добавлено.
- `SqlFunctions.SqlServer.string_split` (TVF, 2016+), `date_add` → `dateadd` / `date_diff` → `datediff` /
  `date_from_parts` → `datefromparts` / `end_of_month` → `eomonth`,
  `GROUP BY ROLLUP`/`CUBE`, текстовые JSON-функции `json_value`/`json_query`/`json_modify`/`isjson` (2016+),
  `openjson` (TVF), полнотекстовые предикаты `contains`/`freetext`, табличные хинты `WithTableHint`,
  JSON/XML-вывод `ForJson` (`FOR JSON PATH`/`AUTO`) и `ForXml` (`FOR XML`) — добавлено.

---

## Простое (изолированное, без изменения модели запроса)

Добавление одной ветки в `CommonFunctions` + транслятор, либо флаг/оверрайд в `SqlServerDialect`.

- [x] **`GREATEST` / `LEAST`** — `SqlServerDialect.SupportsGreatestLeast => true` (стандартный
      синтаксис). Требует SQL Server 2022+. Тесты: `SqlGenerationTests.GreatestLeast_ShouldEmitFunctions`,
      `SqlServerDialectTests.GreatestLeast_ShouldUseStandardSyntax`.
- [x] **`DATETRUNC`** — `SupportsDateTrunc` + `MakeDateTrunc` (`datetrunc(part, value)`); множественные
      ANSI-части сворачиваются в единственные T-SQL (`milliseconds` → `millisecond`),
      `decade`/`century`/`millennium` бросают. Требует SQL Server 2022+.
- [x] **`STRING_AGG`** — добавлен отдельный флаг `SupportsStringAgg` (по умолчанию берёт значение
      зонтичного `SupportsStringArrayAggregates`); SQL Server 2017+ включает его, `array_agg` остаётся
      недоступным (`SupportsArrayAgg`). `BuiltinFunctionTranslator` теперь проверяет флаги раздельно.
- [x] **Строковые функции через встроенный транслятор** — `CHARINDEX`/`STUFF` закрыты переносимыми
      built-in конструкциями: `s.IndexOf(x[, start])` → `charindex`, `s.Remove(...)`/`s.Insert(...)` →
      `stuff`, а `LEFT`/`RIGHT`/`REPLICATE` — через `s.Substring(...)`/`new string(c, n)` (`replicate`).
      Добавлены также `s.LastIndexOf(x)` (для SQL Server — `charindex(reverse(...))`), `s.PadLeft`/`PadRight`
      (`replicate`) и `s.PadLeft(n, c)`; хуки: `MakeStringIndexOf`/`MakeStringLastIndexOf`/`MakePad`/
      `MakeStuff`/`MakeRepeat` + `MakeStringPosition`/`MakeStringReverse` (SQLite не умеет `LastIndexOf` —
      нет `reverse`). Осталось только **`PATINDEX`** (аналога в C# нет) — это корректный кейс для
      `[SqlFunction("patindex")]`; `TRY_CAST`/`TRY_CONVERT`/`CONVERT` со style по-прежнему через
      `[SqlFunction]`.
      `CONCAT_WS`/`FORMAT`/`REVERSE`/`TRANSLATE`/`OVERLAY` остаются в extended-наборе `CommonFunctions`
      (PostgreSQL).
- [ ] **`FORMAT(value, formatString)` (дата/число)** — встроенного маппинга нет: у PostgreSQL
      `SqlFunctions.Postgres.format(fmt, args)` — это `format()` с printf-плейсхолдерами, у SQL Server
      `FORMAT` принимает значение и .NET-шаблон (`'yyyy-MM-dd'`). В demo-запросах (`mssql_rolling_kpi.sql`,
      `mssql_vip_churn.sql`) пользовательский UDF `[SqlFunction("format")]` убран: LINQ-пример теперь
      проецирует исходный `DateTime` без форматирования, а не маскирует пробел стабом. Нужен
      кросс-провайдерный метод (например, `CommonFunctions.format_date(value, template)` с флагом и
      хуком) либо явная фиксация: «форматирование дат — только через `[SqlFunction]`». Уровень: простое.
- [x] **`IIF` / `CHOOSE`** — `iif` перенесён на кросс-провайдерный `CommonFunctions.iif` (generic
      `TResult?`) с флагом `SupportsIif` и хуком `MakeIif` (нативный `iif` на SQL Server/SQLite, `if` на
      MySQL/MariaDB/ClickHouse, `case when` на PostgreSQL); `choose` остаётся SQL Server-only на
      `SqlServerFunctions.choose` (variadic) под флагом `SupportsChoose`. Прежний объединённый флаг
      `SupportsIifChoose` разделён на `SupportsIif`/`SupportsChoose`. C#-тернарник по-прежнему
      транслируется в переносимый `case when ... end` (`MakeCase`) и не подменяется на `IIF`.
      Тесты: `SqlGenerationTests.IifChoose_ShouldUseSqlServerFunctions` (SQL Server),
      `Iif_ShouldEmitCaseExpression` (PostgreSQL), `Iif_ShouldUseIfFunction` (MySQL/ClickHouse/MariaDB),
      `Iif_ShouldUseIifFunction` (SQLite), `SqlServerSpecificTests.IifChoose_ShouldEvaluateCondition`
      (реальный SQL Server).
- [x] **Расширенные части `DATEPART`**: `DateTime.DayOfYear` добавлен (внутренняя часть `doy`): PostgreSQL
      `extract(doy ...)`, SQL Server `datepart(dayofyear, ...)`, MySQL `dayofyear(...)`, SQLite
      `cast(strftime('%j', ...) as integer)`, ClickHouse `toDayOfYear(...)`. Публичный
      `SqlFunctions.Sql.extract(part, value)` добавлен повторно (см. `todo_postgres.md:47`): `quarter`,
      `week` (ISO), `dow`/`isodow` нормализованы в том числе для SQL Server через `datepart(weekday)` +
      `@@datefirst`/`datepart(isowk)`; `DateTime.DayOfWeek` по-прежнему не маппится, вместо него —
      `extract("dow", value)`.

## Среднее (новая площадь API, но модель запроса уже есть) — закрыто

> Все средние пункты выполнены. Оставшиеся «пограничные» задачи (сырой SQL как источник,
> коррелированная скалярная проекция, `CONTAINSTABLE`/`FREETEXTTABLE`) по решению пользователя
> перенесены в раздел «Отложено: сложное».

- [x] **Арифметика дат**: `date_add` → `dateadd(field, amount, value)`, `date_diff` →
      `datediff(field, start, end)` и `end_of_month` → `eomonth` (флаг `SupportsDateArithmetic`;
      PostgreSQL — интервальная арифметика, ClickHouse — `addXxx`/`dateDiff`/`toLastDayOfMonth`;
      `DateTime.AddDays`/`AddMonths`/… — тот же хук). `date_from_parts(year, month, day)` →
      `datefromparts`/`make_date`/`makeDate` — тоже добавлено.
- [x] **`STRING_SPLIT`** — `SqlFunctions.SqlServer.string_split(value, separator)` + `SqlFunctions.IStringSplitRow`
      (колонка `value`), через `FromTableFunction`.
- [x] **Table hints (`WITH (NOLOCK)`)** — `EntityBuilder.WithTableHint(...)` + `SupportsTableHints`/
      `MakeTableHints`; SQL Server рендерит ` from table with (hint, ...)` перед псевдонимом. Покрыта
      только основная (первая) таблица; хинты на присоединённых таблицах пока не в API.
- [x] **`ROLLUP` / `CUBE` / `GROUPING SETS`** — `EntityBuilder.GroupByRollup`/`GroupByCube`/
      `GroupByGroupingSets` + `GroupingType`/`GroupingSets`; ANSI-форма для SQL Server/PostgreSQL/SQLite,
      `... with rollup/cube` для MySQL/MariaDB/ClickHouse (`CUBE`/`GROUPING SETS` в MySQL/MariaDB нет;
      in-memory отклоняет всё).
- [x] **JSON-функции на `nvarchar`**: `json_value`, `json_query`, `json_modify` и `isjson` через
      `TextJsonSqlTranslator` (`SupportsTextJson`). `isjson` рендерит диалект — `ISqlDialect.MakeIsJson`
      (в предикате `(isjson(x)) = 1`, в проекции `cast(... as bit)` для SQL Server).
- [x] **`OPENJSON`** — `SqlFunctions.SqlServer.openjson(json)` + `SqlFunctions.IOpenJsonRow` (`key`/`value`/`type`),
      схема по умолчанию (без `WITH`), через `FromTableFunction`. Осталось: типизированная
      схема `WITH (...)` (пользовательский `[SqlTableFunction]`-враппер уже покрывает этот случай).
- [x] **Полнотекстовый поиск**: предикаты `SqlFunctions.Sql.contains` / `SqlFunctions.Sql.freetext`
      (`SupportsFullText`, SQL Server) через `BuiltinFunctionTranslator` (`MakeBooleanPredicate`
      материализует проекцию в `bit`). SQL рендерит диалект — `ISqlDialect.MakeFullText` (ядро больше
      не содержит T-SQL). `CONTAINSTABLE`/`FREETEXTTABLE` перенесены в отложенное.
- [x] **`FOR JSON` / `FOR XML`** — `QueryCommand.ForJson(...)` и `QueryCommand.ForXml(...)` +
      `SupportsForJson`/`SupportsForXml` и `MakeForJson`/`MakeForXml`; SQL Server рендерит хвостовые
      `for json path/auto` (`root`, `include_null_values`) и `for xml raw/auto/explicit/path`
      (имя элемента, `root`, `elements`) перед `OPTION`. Сочетать `FOR JSON` и `FOR XML` нельзя.

## Единообразие провайдеров (аудит)

Проверка, что схожий функционал во всех провайдерах выражен одинаково в библиотеке и различается
только реализацией `ISqlDialect` (правки вносились параллельно/независимо).

### Исправлено

- [x] **Полнотекст**: `BuiltinFunctionTranslator.EmitFullText` жёстко рендерил `contains(...)`/
      `freetext(...)` в ядре. Вынесено в `ISqlDialect.MakeFullText(functionName, column, search)`
      (`SqlDialectBase` → пусто, `SqlServerDialect` — реализация). Добавить полнотекст новому
      провайдеру теперь = включить `SupportsFullText` и переопределить `MakeFullText`, без правок ядра.
- [x] **`isjson`**: `TextJsonSqlTranslator` жёстко рендерил `(isjson(x)) = 1`/`cast(... as bit)`.
      Вынесено в `ISqlDialect.MakeIsJson(value, asPredicate)`.
- [x] **Единый паттерн**: capability-флаги `SupportsFullText`/`SupportsTextJson` + `Make*` в диалекте,
      аналогично `SupportsForJson`/`MakeForJson`, `SupportsForXml`/`MakeForXml`,
      `SupportsTableHints`/`MakeTableHints`, `SupportsDateArithmetic`/`MakeDateAdd|MakeDateDiff|...`.
- [x] Тесты: `SqlServerDialectTests.FullTextHooks_ShouldRenderPredicates`,
      `IsJsonHooks_ShouldRenderPredicateAndValue`.

### Сделано по решению пользователя

- [x] **Кросс-провайдерные пробелы**:
  - полнотекст: `PostgresDialect` (`to_tsvector(col) @@ plainto_tsquery|websearch_to_tsquery`) и
    `MySqlDialect` (`match(col) against(... [in boolean mode])`, наследуется MariaDB);
  - арифметика дат: `MySqlDialect` (`date_add(value, interval n unit)`, `timestampdiff`, `last_day`,
    `str_to_date`) и `SqliteDialect` (модификаторы `datetime`/`strftime`, разность секунд/месяцев,
    `date(..., 'start of month', ...)`, `date(printf(...))`);
  - `string_agg`: MySQL/MariaDB `group_concat(x separator d)`, SQLite `group_concat(x, d)`;
  - session/info-функции: `current_user`/`session_user`/`current_schema`/`current_database`/`version`
    промоутнуты из PostgreSQL-only `PostgresFunctions` в кросс-провайдерный `CommonFunctions`
    (`SupportsSessionInfoFunctions` + `SupportsSessionInfoFunction(name)` + `MakeSessionInfoFunction`).
    SQL Server рендерит ключевые слова `current_user`/`session_user` и `schema_name()`/`db_name()`/`@@version`,
    MySQL/MariaDB — `current_user()`/`session_user()`/`schema()`/`database()`/`version()`, ClickHouse —
    `currentUser()`/`currentDatabase()`/`version()`, SQLite — `sqlite_version()`.
- [x] **TVF capability-гейт**: `ISqlDialect.SupportsTableFunction(name)` (база — `false`) +
    проверка в `SqlBuilder.MakeTableFunction` только для встроенных TVF из `CommonFunctions`.
    PostgreSQL разрешает `generate_series`/`unnest`, SQL Server — `string_split`/`openjson`; на
    «чужом» провайдере бросается `NotSupportedException`. Пользовательские `[SqlTableFunction]` не
    гейтятся.
- [x] **Дубли API удалены из extended**: `SqlFunctions.Sql.make_date`, `age`, `date_bin`, `extract` (и
    `ExtractFields`/`EmitExtract`) убраны; используются `date_from_parts`, `date_diff`, `date_trunc`
    и члены `DateTime`. Тесты и документация EN/RU обновлены.
- [x] **`MakeCoalesce`**: база теперь ANSI `coalesce(a, b)`, T-SQL `isnull` переехал в
    `SqlServerDialect`.
- [x] **Валидация дат-частей**: списки перенесены в `SqlDialectBase`, добавлены
    `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField`; транслятор спрашивает
    диалект. SQL Server/ClickHouse запрещают `decade`/`century`/`millennium` для `date_trunc`.
- [x] **Текстовые JSON-имена**: добавлен `ISqlDialect.MakeTextJsonFunction(name)` (база — тождество),
    транслятор больше не фиксирует имена в ядре.
- [x] **`WITH TIES` / `TABLESAMPLE`**: кросс-провайдерные модификаторы (`EntityBuilder.WithTies`/
    `TableSample`) — SQL Server рендерит `TOP(n) WITH TIES`/`FETCH NEXT n ROWS WITH TIES` и
    `TABLESAMPLE (n PERCENT) [REPEATABLE (seed)]` (только `SYSTEM`; `BERNOULLI` гейтится
    `SupportsTableSampleMethod`). PostgreSQL-половина — `docs/guide/provider-specific/postgresql.md`.

### Осталось

- [x] **`greatest`/`least` в SQLite** — `SqliteDialect.SupportsGreatestLeast => true` +
      `MakeGreatest`/`MakeLeast` рендерят скалярные `max(a, b, ...)`/`min(...)` (одноаргументная форма =>
      сам аргумент, чтобы не попасть в агрегат `max`/`min`). Тесты:
      `SqlGenerationTests.GreatestLeast_ShouldEmitMaxMin`, `SqliteDialectTests.GreatestLeast_ShouldUseScalarMaxMin`.
- [x] **`json_value`/`json_query`/`json_modify`/`isjson` для MySQL/MariaDB** — `SupportsTextJson => true`,
      новый хук `ISqlDialect.MakeTextJsonFunction(name, args)` (по умолчанию оборачивает старый
      одноимённый) и `MySqlDialect` рендерит `json_unquote(json_extract(...))`/`json_extract(...)`/
      `json_set(...)`/`json_valid(...)`. Тесты: `MySqlDialectTests.TextJsonHooks_ShouldUseJsonExtractFamily`,
      `SqlGenerationTests.TextJsonFunctions_ShouldUseJsonExtractFamily`,
      `MySqlSpecificTests.Json*`/`IsJson_ShouldDetectValidJson` (реальный MySQL).
- [x] **`nth_value` (оконная)** — `CommonFunctions.nth_value(property, n)` + флаг `SupportsNthValue`
      (base `false`; PostgreSQL/MySQL/MariaDB/SQLite/ClickHouse `true`). SQL Server этой функции не
      имеет (в T-SQL `NTH_VALUE` отсутствует), поэтому там вызов гейтится `NotSupportedException`. На
      поддерживающих провайдерах результат frame-зависим — полную рамку задаёт вызывающий
      (см. `todo_postgres.md` раздел «Оконные функции и фреймы»).
- [x] **`ANY_VALUE` (кросс-провайдерный `any_agg`)** — `any_agg` перенесён в `CommonFunctions`, флаг
      `SupportsAnyValueAggregate` (base `false`; MySQL и ClickHouse `true`), маппинг
      `any_agg` → `ANY_VALUE` (MySQL) / `any` (ClickHouse). MariaDB намеренно выключен (нет
      `ANY_VALUE` до 13.2, MDEV-10426), SQL Server оставлен выключенным: `ANY_VALUE` есть только в
      SQL Server 2025 / Fabric. `any_last` остаётся ClickHouse-only (`SupportsAnyAggregates`).
      Тесты: `SqlGenerationTests.AnyValueAggregate_*` (MySQL/ClickHouse),
      `AnyValueAggregate_ShouldThrowBecause*` (PG/SQLite/SQL Server/MariaDB),
      `MySqlSpecificTests.AnyValueAggregate_ShouldReturnGroupValue`.
- [x] **`percentile_cont`/`percentile_disc` на SQL Server/MariaDB** — добавлен оконный API
      `CommonFunctions.percentile_cont`/`percentile_disc` (`WindowFunction<T?>` + `.Over(...)`), флаг
      `SupportsPercentileWindow` (SQL Server, MariaDB), рендер
      `percentile_cont(f) within group (order by x) over (...)`. PostgreSQL по-прежнему использует
      упорядоченный агрегат (`SupportsOrderedAggregates`), MySQL/SQLite/ClickHouse отклоняют.
      Тесты: `SqlGenerationTests.PercentileWindow_ShouldEmitWithinGroupOver` (SQL Server/MariaDB),
      `PercentileWindow_ShouldThrowBecauseMySqlHasNoPercentile`,
      `SqlServerSpecificTests.PercentileWindow_ShouldReturnValue`.

### Проверено как единообразное

- Дата: `date_add`/`date_diff`/`date_trunc`/`end_of_month`/`date_from_parts` и `DateTime.Add*` —
  один хук `MakeDateAdd`/`MakeDateDiff`/`MakeDateTrunc`/..., ядро без SQL.
- `greatest`/`least`, `string_agg`/`array_agg` (`MakeStringAgg`/`MakeArrayAgg`/`MakeWithinGroup`),
  `ROLLUP`/`CUBE`/`GROUPING SETS` (`MakeGrouping`/`MakeGroupingSets`), агрегаты (`MakeAggregate`),
  `FOR JSON`/`FOR XML`, table hints — все через `Make*` в диалекте.

## Отложено: сложное (новые конструкции уровня запроса/источника)

> По решению пользователя сложные пункты отложены.

- [ ] **Сырой SQL как композируемый источник/subquery** — сейчас `WithSql` заменяет запрос
      целиком; нужен `FromRawSql` как элемент `FROM` (`limitations.md:25`); трогает
      `QueryPlanner`/`QueryPreparer`.
- [ ] **Производный запрос как первичный `FROM`-источник с `Join`** — `ctx.From(derivedQuery).Join(...)`
      падает на подготовке (`QueryPreparationException: Select must return new anonymous type`): у
      обёрнутого источника нет проекции/метаданных сущности, поэтому колонки join не разрешаются
      (`sql-capabilities-gap-analysis.md`, known gap 10; `QueryCommand.QueryPreparer.cs:373`). В
      demo-запросах SQL Server (`mssql_vip_churn.sql`, `mssql_supply_chain.sql`) из-за этого join
      менеджера и join supply/production либо складываются в порождающий запрос, либо пишутся через
      CTE-API `With(name, query).From(name)`. При этом производный запрос-источник с
      `Where`/`OrderBy`/`GroupBy`/`Select` и join, где производный запрос — присоединяемая сторона,
      работают; нужен только первичный `FROM`.
- [ ] **Общий коррелированный скалярный подзапрос в проекции** — механизм `OuterRefMarker` уже
      существует, нужно довести до публичного API (`limitations.md:22`).
- [ ] **`CONTAINSTABLE` / `FREETEXTTABLE`** — TVF с колонкой `RANK`; первый аргумент — имя
      таблицы/колонки, а не выражение, поэтому плохо ложится на TVF-модель `[SqlTableFunction]`.
      **Блокер:** ссылка на базовую таблицу по имени внутри `FROM`-источника требует механизма
      внешней ссылки в `FROM`-подзапросе (тот же пробел, что «сырой SQL как композируемый источник» и
      «коррелированный APPLY»), поэтому оставлено отложенным.
- [ ] **`PIVOT` / `UNPIVOT`** — новая конструкция модели запроса: агрегатная спецификация,
      список значений, переименование колонок; затрагивает `SqlBuilder`, `EntityBuilder`
      и диалекты. Пока demo-запрос `mssql_quarterly_pivot.sql` выражается эквивалентной условной
      агрегацией `SUM(CASE WHEN quarter = n THEN margin END)` (`GROUP BY CategoryName`), а не
      нативным `PIVOT`; при появлении API переписать без замены.
- [x] **Temporal tables (`FOR SYSTEM_TIME`)** — `EntityBuilder.ForSystemTime(TemporalClause)` +
      `TemporalKind`/`TemporalClause`; диалектные хуки `SupportsTemporalTable`/`SupportsTemporalKind`/
      `MakeTemporalTable` (SQL:2011-синтаксис в `SqlDialectBase`). SQL Server поддерживает все виды
      (`AS OF`/`BETWEEN`/`FROM..TO`/`CONTAINED IN`/`ALL`); MariaDB — все, кроме `CONTAINED IN`;
      остальные провайдеры и in-memory отклоняют. Тесты: `SqlGenerationTests.ForSystemTime_*`
      (SQL Server, MariaDB), `Sqlite/ClickHouse …ForSystemTime_ShouldThrow…`; `docs/guide/provider-specific/sqlserver.md`.
- [ ] **XML-тип и его методы (`.value`, `.query`, `.nodes`, `.exist`)** — постфиксный вызов
      метода на колонке, отдельный синтаксис, плохо ложится на текущий транслятор.
- [ ] **Коррелированный `APPLY`** — applied-source должен ссылаться на внешнюю строку;
      нужен публичный API внешней ссылки внутри `FROM`-подзапроса (`limitations.md:21`).
- [ ] **`INTERSECT ALL` / `EXCEPT ALL`** — сервер их не умеет; диалект корректно бросает
      `NotSupportedException`, реализация не требуется (`sqlserver.md:116`).

## Вне области по дизайну (read-only, без change tracking)

Не реализуется намеренно; запись и инфраструктура остаются за вызывающим кодом
(`docs/advanced/limitations.md:19`).

**DML** (`INSERT` / `UPDATE` / `DELETE` / `MERGE`, в т.ч. `OUTPUT`), `RETURNING`, хранимые процедуры и
динамический SQL, транзакции / `SaveChanges` / change tracking / CDC — за рамками проекта на текущий
момент (nextorm — read-only).

- [ ] **Навигационные свойства и связи** — только явные join.
- [ ] **`OPENROWSET` / `OPENQUERY` / linked servers** — внешние источники данных.
