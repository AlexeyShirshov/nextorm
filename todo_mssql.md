# TODO: недостающие функции MS SQL Server

Черновик списка популярных функций/конструкций SQL Server, которых пока нет в nextorm.
Отсортирован **по сложности реализации** (от простого к сложному). В конце — то, что отсутствует
по замыслу (read-only построитель `SELECT`).

Источник — код `1.0.3-alpha` и документы
[`docs/sql-capabilities-gap-analysis.md`](docs/sql-capabilities-gap-analysis.md),
[`docs/advanced/limitations.md`](docs/advanced/limitations.md),
[`docs/providers/sqlserver.md`](docs/providers/sqlserver.md).

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
- `NORM.MS_SQL.string_split` (TVF, 2016+), `date_add` → `dateadd` / `date_diff` → `datediff` /
  `date_from_parts` → `datefromparts` / `end_of_month` → `eomonth`,
  `GROUP BY ROLLUP`/`CUBE`, текстовые JSON-функции `json_value`/`json_query`/`json_modify`/`isjson` (2016+),
  `openjson` (TVF), полнотекстовые предикаты `contains`/`freetext`, табличные хинты `WithTableHint`,
  JSON/XML-вывод `ForJson` (`FOR JSON PATH`/`AUTO`) и `ForXml` (`FOR XML`) — добавлено.

---

## Простое (изолированное, без изменения модели запроса)

Добавление одной ветки в `NORM_SQL` + транслятор, либо флаг/оверрайд в `SqlServerDialect`.

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
      `CONCAT_WS`/`FORMAT`/`REVERSE`/`TRANSLATE`/`OVERLAY` остаются в extended-наборе `NORM_SQL`
      (PostgreSQL).
- [ ] **`IIF` / `CHOOSE`** — не обязательны: `CASE`/ternary уже транслируются; при желании
      добавляются как сахар над `MakeCase`.
- [ ] **Расширенные части `DATEPART`**: `NORM.SQL.extract("quarter"|"week"|...)` уже существует, но
      входит в extended-набор (PostgreSQL). `DateTime.DayOfYear`/`DayOfWeek` напрямую не маппятся:
      `datepart(weekday, x)` зависит от `DATEFIRST`, а `DayOfWeek` в .NET 0-based — семантика не совпадает.

## Среднее (новая площадь API, но модель запроса уже есть) — закрыто

> Все средние пункты выполнены. Оставшиеся «пограничные» задачи (сырой SQL как источник,
> коррелированная скалярная проекция, `CONTAINSTABLE`/`FREETEXTTABLE`) по решению пользователя
> перенесены в раздел «Отложено: сложное».

- [x] **Арифметика дат**: `date_add` → `dateadd(field, amount, value)`, `date_diff` →
      `datediff(field, start, end)` и `end_of_month` → `eomonth` (флаг `SupportsDateArithmetic`;
      PostgreSQL — интервальная арифметика, ClickHouse — `addXxx`/`dateDiff`/`toLastDayOfMonth`;
      `DateTime.AddDays`/`AddMonths`/… — тот же хук). `date_from_parts(year, month, day)` →
      `datefromparts`/`make_date`/`makeDate` — тоже добавлено.
- [x] **`STRING_SPLIT`** — `NORM.MS_SQL.string_split(value, separator)` + `NORM.IStringSplitRow`
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
- [x] **`OPENJSON`** — `NORM.MS_SQL.openjson(json)` + `NORM.IOpenJsonRow` (`key`/`value`/`type`),
      схема по умолчанию (без `WITH`), через `FromTableFunction`. Осталось: типизированная
      схема `WITH (...)` (пользовательский `[SqlTableFunction]`-враппер уже покрывает этот случай).
- [x] **Полнотекстовый поиск**: предикаты `NORM.SQL.contains` / `NORM.SQL.freetext`
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
  - `string_agg`: MySQL/MariaDB `group_concat(x separator d)`, SQLite `group_concat(x, d)`.
- [x] **TVF capability-гейт**: `ISqlDialect.SupportsTableFunction(name)` (база — `false`) +
    проверка в `SqlBuilder.MakeTableFunction` только для встроенных TVF из `NORM.NORM_SQL`.
    PostgreSQL разрешает `generate_series`/`unnest`, SQL Server — `string_split`/`openjson`; на
    «чужом» провайдере бросается `NotSupportedException`. Пользовательские `[SqlTableFunction]` не
    гейтятся.
- [x] **Дубли API удалены из extended**: `NORM.SQL.make_date`, `age`, `date_bin`, `extract` (и
    `ExtractFields`/`EmitExtract`) убраны; используются `date_from_parts`, `date_diff`, `date_trunc`
    и члены `DateTime`. Тесты и документация EN/RU обновлены.
- [x] **`MakeCoalesce`**: база теперь ANSI `coalesce(a, b)`, T-SQL `isnull` переехал в
    `SqlServerDialect`.
- [x] **Валидация дат-частей**: списки перенесены в `SqlDialectBase`, добавлены
    `SupportsDateTruncField`/`SupportsDateAddField`/`SupportsDateDiffField`; транслятор спрашивает
    диалект. SQL Server/ClickHouse запрещают `decade`/`century`/`millennium` для `date_trunc`.
- [x] **Текстовые JSON-имена**: добавлен `ISqlDialect.MakeTextJsonFunction(name)` (база — тождество),
    транслятор больше не фиксирует имена в ядре.

### Осталось

- [ ] **`greatest`/`least` в SQLite** — у SQLite есть скалярные `max(a, b, ...)`/`min(...)`, но
      `SupportsGreatestLeast` не включён (в выбранный набор правок не входил).
- [ ] **`json_value`/`json_query`/`json_modify` для MySQL** — имена теперь делегируются
      (`MakeTextJsonFunction`), но `SupportsTextJson` в MySQL пока не включён (синтаксис
      `JSON_EXTRACT`/`JSON_UNQUOTE` отличается от SQL/JSON).

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
- [ ] **Общий коррелированный скалярный подзапрос в проекции** — механизм `OuterRefMarker` уже
      существует, нужно довести до публичного API (`limitations.md:22`).
- [ ] **`CONTAINSTABLE` / `FREETEXTTABLE`** — TVF с колонкой `RANK`; первый аргумент — имя
      таблицы/колонки, а не выражение, поэтому плохо ложится на TVF-модель `[SqlTableFunction]`.
- [ ] **`PIVOT` / `UNPIVOT`** — новая конструкция модели запроса: агрегатная спецификация,
      список значений, переименование колонок; затрагивает `SqlBuilder`, `EntityBuilder`
      и диалекты.
- [ ] **Temporal tables (`FOR SYSTEM_TIME`)** — новый синтаксис источника в `FROM`
      (`AS OF`, `BETWEEN`, `CONTAINED IN`), требует изменения рендера `FROM`.
- [ ] **XML-тип и его методы (`.value`, `.query`, `.nodes`, `.exist`)** — постфиксный вызов
      метода на колонке, отдельный синтаксис, плохо ложится на текущий транслятор.
- [ ] **Коррелированный `APPLY`** — applied-source должен ссылаться на внешнюю строку;
      нужен публичный API внешней ссылки внутри `FROM`-подзапроса (`limitations.md:21`).
- [ ] **`INTERSECT ALL` / `EXCEPT ALL`** — сервер их не умеет; диалект корректно бросает
      `NotSupportedException`, реализация не требуется (`sqlserver.md:116`).

## Вне области по дизайну (read-only, без change tracking)

Не реализуется намеренно; запись и инфраструктура остаются за вызывающим кодом
(`docs/advanced/limitations.md:19`).

- [ ] **DML**: `INSERT` / `UPDATE` / `DELETE` / `MERGE`, в т.ч. `OUTPUT`.
- [ ] **Хранимые процедуры** и динамический SQL.
- [ ] **Транзакции, `SaveChanges`, change tracking / CDC**.
- [ ] **Навигационные свойства и связи** — только явные join.
- [ ] **`OPENROWSET` / `OPENQUERY` / linked servers** — внешние источники данных.
