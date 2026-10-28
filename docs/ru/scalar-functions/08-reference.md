# Таблица сопоставления провайдеров

| Возможность | SQLite | SQL Server | PostgreSQL |
|---|---|---|---|
| `ToUpper` / `ToLower` | `upper` / `lower` | `upper` / `lower` | `upper` / `lower` |
| `Length` | `length` | `len` | `length` |
| `Substring` | `substring`, 1-based | `substring`, 1-based | `substring`, 1-based |
| `Trim` / `TrimStart` / `TrimEnd` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` | `trim` / `ltrim` / `rtrim` |
| `Replace` | `replace` | `replace` | `replace` |
| `Contains` / `StartsWith` / `EndsWith` / `like` | `like` (escape `\`) | `like` (escape `\`) | `like` (escape `\`) |
| `string.IsNullOrEmpty` | `(x is null or x = '')` | `(x is null or x = '')` | `(x is null or x = '')` |
| `string.Format` / `ToString(format)` | `printf` / `strftime` | `format` | `to_char` |
| Ordinal `Equals` / `CompareOrdinal` | `collate binary` | `collate Latin1_General_100_BIN2` | `collate "C"` |
| `collate(s, name)` | `s collate name` | `s collate name` | `s collate "name"` |
| Столбец с `[Collation]` | `s collate name` | `s collate name` | `s collate "name"` |
| Regex (`IsMatch` / `Replace`) | `s regexp ...` / `regexp_replace(...)` | `regexp_like(...)` / `regexp_replace(...)` (2025+) | `s ~ ...` / `regexp_replace(...)` |
| `Abs` | `abs` | `abs` | `abs` |
| `Round` | `round(x)` | `round(x, 0)` | `round(x)` |
| `Truncate` | `trunc` | `round(x, 0, 1)` | `trunc` |
| `Log` (натуральный) | `ln` | `log` | `ln` |
| `Now` / `UtcNow` | `datetime('now')` / `datetime('now')` | `getdate()` / `getutcdate()` | `now()` / `now() at time zone 'utc'` |
| `Year` / `Month` / `Day` / `Hour` | `cast(strftime('%Y'...`/`'%m'`/`'%d'`/`'%H'` `as integer)` | `datepart(year, ...)` и т. д. | `extract(year from ...)` и т. д. |
| `??` объединение | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` |
| Конкатенация строк (`+`) | `\|\|` | `+` | `\|\|` |
| Логический предикат как значение | без изменений | `cast(case when ... then 1 else 0 end as bit)` | без изменений |
| Массивы (`any`/`all`, функции массивов) | `NotSupportedException` | `NotSupportedException` | `any(@array)`, `cardinality(...)`, ... |
| JSON/JSONB (`json_agg`, `->`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Текстовый JSON (`json_value`, `json_query`, `json_modify`, `isjson`) | `NotSupportedException` | `json_value(...)`, ..., `isjson(...)` | `NotSupportedException` |
| `nullif` | поддерживается | поддерживается | поддерживается |
| `greatest` / `least` | `max(...)` / `min(...)` (один аргумент -> `(...)`) | поддерживается (2022+) | поддерживается |
| `iif` | `iif(cond, a, b)` (3.32+) | `iif(cond, a, b)` | `case when cond then a else b end` |
| `multi_if` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (только ClickHouse; `multiIf`) |
| `date_trunc` | `NotSupportedException` | `datetrunc(...)` (2022+) | поддерживается |
| `date_add` / `end_of_month` / `date_diff` / `date_from_parts` | `datetime(x, n \|\| ' days')` / `date(x, 'start of month', ...)` / разность `strftime` / `date(printf(...))` | `dateadd(...)` / `eomonth(...)` / `datediff(...)` / `datefromparts(...)` | интервальная арифметика / `date_trunc` / разность частей даты / `make_date` |
| `string_agg` / `array_agg` | `group_concat(x, delimiter)` (нет `array_agg`) | `string_agg` (2017+); `array_agg` бросает исключение | поддерживается |
| `filter (where ...)` у агрегатов | `filter (where ...)` | `NotSupportedException` | `filter (where ...)` |
| Расширенная библиотека скалярных функций (`asin`, `split_part`, `regexp_*`, `to_char`, ...) | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Логические/битовые/статистические агрегаты | `NotSupportedException` | `NotSupportedException` | поддерживается |
| Упорядоченные агрегаты (`percentile_cont`, ...) | `NotSupportedException` | оконный `percentile_cont(f) within group (order by x) over (...)` | `within group (order by ...)` |
| Медиана | `NotSupportedException` | `percentile_cont(0.5) within group (order by x) over (...)` (аналитическая: одна строка на входную строку, окно только с `partition by`) | `percentile_cont(0.5) within group (order by x)` (агрегат: одна строка на группу; только сортировка с плавающей точкой) |
| Общий `percentile(fraction, value)` | `NotSupportedException` | `NotSupportedException` | `NotSupportedException` (никогда не псевдоним непрерывной формы) |
| JSONPath (`jsonb_path_*`) | `NotSupportedException` | `NotSupportedException` | `cast(path as jsonpath)` |
| Встроенные табличные функции | `NotSupportedException` | `string_split(...)`, `openjson(...)`, `regexp_split_to_table(...)` (2025+) | `generate_series(...)`, `unnest(...)` |

Провайдер in-memory не рендерит SQL: он компилирует и вычисляет выражение для строк в памяти, поэтому
выполняется сам метод .NET. Приведённая выше матрица SQL относится к провайдерам SQLite, SQL Server и
PostgreSQL.

ClickHouse рендерит `dateTrunc('part', x)`, `addDays`/`addMonths`/.../`addSeconds` (и масштабированный
`addYears` над операндом `DateTime64(6)` для `year`/`decade`/`century`, тип SQL-результата которых —
`DateTime64(6)`, а CLR-API не меняется; `millennium` бросает исключение), `toLastDayOfMonth(x)`,
`arrayStringConcat(groupArray(x), delimiter)`, `groupBitAnd`/`groupBitOr`/`groupBitXor`,
`covarPop`/`covarSamp`, `argMin`/`argMax`, комбинаторы `-If` и `multiIf`. Он отклоняет ANSI-предложение
`filter (where ...)`, агрегаты `regr_*` и логические агрегаты через `NotSupportedException`; см.
[Провайдер ClickHouse](../connecting-providers/clickhouse.md).

## Версионно-зависимые функции SQL Server (2022/2025)

Эти члены живут в [`SqlFunctions.SqlServer`](../guide/provider-specific-sql/sqlserver.md#версионно-зависимые-функции-2022-и-2025)
и закрыты **только объявленной мажорной версией**: объявление edition никогда не подменяет версию, а
неверсионированный контекст отклоняет каждый из них до SQL, указывая требуемую и объявленную мажорную
версию в сообщении.

| Член | SQL | Результат | Требуется |
|---|---|---|---|
| `regexp_count(input, pattern)` | `regexp_count(input, pattern)` | `int?` — `0` при отсутствии совпадений, `NULL` при NULL-входе | мажорная версия 17 |
| `regexp_substr(input, pattern[, start[, occurrence[, match_type]]])` | `regexp_substr(input, pattern[, …])` | `string?` — `NULL` при отсутствии совпадения или NULL-операнде; match type рендерится как `cast(… as varchar(4000))`, потому что нативный аргумент не принимает `nvarchar` (Msg 8116) | мажорная версия 17; уровень совместимости не требуется |
| `regexp_instr(input, pattern[, start[, occurrence[, return_option[, match_type]]]])` | `regexp_instr(input, pattern[, …])` | `int?` — `0` при отсутствии совпадения, `NULL` при NULL-операнде; переданный NULL return option ведёт себя как `0` | мажорная версия 17; уровень совместимости не требуется |
| `regexp_matches(input, pattern[, match_type])` | источник `FROM`, например `from regexp_matches(input, pattern)` | одна строка на неперекрывающееся совпадение: `match_id` (`bigint`), `start_position`/`end_position` (`int`), `match_value`, `substring_matches` (полный JSON-текст захватов); нет совпадений → ноль строк | мажорная версия 17 **и** уровень совместимости БД 170 |
| `edit_distance(input, pattern[, maximum_distance])` | `edit_distance(input, pattern[, maximum_distance])` | `int?`; ограниченная форма возвращает `maximum_distance + 1`, когда истинное расстояние больше предела; отрицательный предел — без ограничения | мажор 17 + совместимость 170 + `PREVIEW_FEATURES = ON` + Windows-коллация |
| `edit_distance_similarity(input, pattern)` | `edit_distance_similarity(input, pattern)` | `int?` в `0..100` | те же предусловия fuzzy |
| `jaro_winkler_distance(input, pattern)` | `jaro_winkler_distance(input, pattern)` | `double?` (`float(53)`) | те же предусловия fuzzy |
| `jaro_winkler_similarity(input, pattern)` | `jaro_winkler_similarity(input, pattern)` | `int?` в `0..100` | те же предусловия fuzzy |
| `left_shift(value, shift)` / `right_shift(value, shift)` | `CASE WHEN value IS NULL OR shift IS NULL THEN CAST(NULL AS <тип операнда>) ELSE left_shift(cast(value as <тип операнда>), cast(shift as bigint)) END` | тип самого операнда (`byte?`/`short?`/`int?`/`long?`); логический zero-fill сдвиг; типизированная NULL-защита («голый» `left_shift(NULL, …)` даёт Msg 8116) | мажорная версия 16 |
| `bit_count(value)` | `CASE WHEN value IS NULL THEN CAST(NULL AS bigint) ELSE bit_count(cast(value as <тип операнда>)) END` | `long?` (`bigint`) | мажорная версия 16 |
| `get_bit(value, offset)` | `CASE WHEN value IS NULL OR offset IS NULL THEN CAST(NULL AS bit) ELSE get_bit(cast(value as <тип операнда>), cast(offset as int)) END` | `bool?` (`bit`) | мажорная версия 16 |
| `set_bit(value, offset[, bitValue])` | `… ELSE set_bit(cast(value as <тип операнда>), cast(offset as int)[, cast(bitValue as bit)]) END` | тип самого операнда; явный NULL в `bitValue` даёт `NULL`, опущенный — нативное значение по умолчанию 1 | мажорная версия 16 |
| `product(value)` | `product(value)` (агрегатный контекст; без `OVER`) | тип входа; NULL-строки игнорируются, группа только из NULL даёт `NULL` | мажорная версия 17 |
| `regexp_split_to_table(input, pattern)` | источник `FROM`, например `from regexp_split_to_table(input, pattern)` | одна строка на фрагмент: `value` и one-based `ordinal` (`bigint`) | мажорная версия 17 **и** уровень совместимости БД 170 |

`regexp_split_to_table` возвращает
[`TvfEntityBuilder<SqlFunctions.ISqlServerRegexpSplitToTableRow>`](xref:NextORM.Core.SqlFunctions.ISqlServerRegexpSplitToTableRow)
и не требует хука `Render`: член, форма строки и регистрация в `SupportsTableFunction` — весь маршрут. На
**уровне совместимости 160** движок мажорной версии 17 сообщает, что функции нет (Msg 208), поэтому
предварительное условие 170 — реальный гейт, который nextorm документирует и проверяет вживую, а не
молча предполагает (диалект не может прочитать уровень совместимости БД).

Regex-скаляры не зависят от уровня совместимости: они работают на 160 и на 170 на движке мажорной
версии 17. `regexp_matches` — исключение среди них: на уровне совместимости 160 движок сообщает, что
функции нет (Msg 208), поэтому его условие 170 — такой же документированный и проверяемый вживую
рантайм-гейт. Четырём fuzzy-именам нужны **все** условия: совместимость 170, `PREVIEW_FEATURES = ON`
и Windows-коллация; на протестированной сборке `17.0.5005.3` коллация `SQL_*` даёт Msg 9847, а
отсутствие preview-флага — Msg 195. Флаг объявленной мажорной версии 17 обещает только версионный
гейт; эти рантайм-предусловия — факты окружения, проверяемые вживую, и обещаний для будущих сборок не
даётся.

## SQL Server TRY_CAST и TRY_CONVERT

`SqlFunctions.SqlServer.try_cast<T>(value)` и `try_convert<T>(value[, style])` рендерят T-SQL
`TRY_CAST`/`TRY_CONVERT` через **закрытое отображение CLR→SQL**; некорректное, переполняющее или
выходящее за диапазон значение даёт SQL NULL вместо ошибки. В отличие от имён выше, они не версионно-
зависимы: любое объявление SQL Server (15, 16, 17 и неверсионированное) их рендерит, а любой другой
провайдер отклоняет член с `The TRY_CAST function is not supported by this provider.`

| Член | SQL | Результат |
|---|---|---|
| `try_cast<T>(value)` | `try_cast(value as <type>)` | `T?` — NULL, когда преобразование недопустимо |
| `try_convert<T>(value)` / `try_convert<T>(value, style)` | `try_convert(<type>, value[, style])` | `T?` — NULL, когда преобразование недопустимо |
| `try_cast(value)` / `try_convert(value[, style])` | `try_cast(value as nvarchar(4000))` / `try_convert(nvarchar(4000), value[, style])` | `string?` — вход длиннее 4000 символов усекается до 4000 |

| CLR-цель `T` | Тип SQL Server |
|---|---|
| `byte` / `short` / `int` / `long` | `tinyint` / `smallint` / `int` / `bigint` |
| `float` / `double` | `real` / `float(53)` |
| `decimal` | `decimal(38,10)` |
| `bool` | `bit` |
| `Guid` | `uniqueidentifier` |
| `DateTime` / `DateTimeOffset` | `datetime2(7)` / `datetimeoffset(7)` |
| `TimeSpan` | `time(7)` |

Цель вне отображения отклоняется **до** подготовки команды (`The TRY_CONVERT function cannot map CLR
target 'MyType' to a supported SQL Server conversion type.`). Style — нативный стиль
`datetime`/`float`/money: его отсутствие и NULL-стиль — разные перегрузки, и NULL-стиль заставляет движок
вернуть NULL даже для конвертируемого значения. Преобразование, которое движок сам запрещает, не
маскируется — `TRY_CAST(1 AS xml)` даёт Msg 529, и `xml` не является публичной целью. См.
[условные функции и преобразования](04-conditionals-and-conversion.md#sql-server-try_cast-и-try_convert).

## Функции только для MySQL

Имена из [`SqlFunctions.MySql`](../guide/provider-specific-sql/mysql.md) рендерятся только провайдером
MySQL; PostgreSQL, SQL Server, SQLite, ClickHouse и in-memory-провайдер отклоняют их с
`NotSupportedException`. Каждое имя ниже **проверено только на MySQL 8.4**: MariaDB отклоняет имена,
добавленные в этом срезе, до собственной проверки ([#284](https://github.com/AlexeyShirshov/nextorm/issues/284)),
а имена только для MariaDB остаются в отдельном разделе ниже.

Каталог содержит 86 новых членов и два имени, которые уже были покрыты (`json_storage_free`, поставлен
в предыдущем срезе, и кросс-провайдерный `connection_id`); три отклонённых имени перечислены в разделе
[Отказы MySQL](#отказы-mysql).

| Семейство | Член | SQL MySQL | Примечания |
|---|---|---|---|
| Строковые | `bin(value)` | `BIN(value)` | двоичная форма беззнакового 64-битного значения, без префикса `0b` |
| Строковые | `export_set(bits, on, off[, separator[, count]])` | `EXPORT_SET(...)` | битовая строка от младшего бита; константный `count` вне `[0, 64]` отклоняется |
| Строковые | `hex(value)` | `HEX(value)` | шестнадцатеричная форма бинарных данных, байтов строки или 64-битного целого |
| Строковые | `make_set(bits, values...)` | `MAKE_SET(...)` | значения через запятую, чья позиция с 1 соответствует установленному биту |
| Строковые | `oct(value)` | `OCT(value)` | восьмеричная форма беззнакового 64-битного значения |
| Строковые | `ord(value)` | `ORD(value)` | код самого левого символа, `0` для пустой строки |
| Строковые | `quote(value)` | `QUOTE(value)` | SQL-литерал в одинарных кавычках с экранированием кавычек и обратных слэшей; слово `NULL` для SQL NULL |
| Строковые | `soundex(value)` | `SOUNDEX(value)` | четырёхсимвольный код Soundex, пустая строка, если в значении нет букв |
| Строковые | `strcmp(left, right)` | `STRCMP(...)` | `-1`/`0`/`1` по порядку сопоставления, `null` при SQL NULL любого аргумента |
| Строковые | `to_base64(value)` | `TO_BASE64(...)` | Base64 бинарных данных с переносом строк каждые 76 символов |
| Строковые | `from_base64(text)` | `FROM_BASE64(...)` | `null`, если текст не является корректным Base64 |
| Строковые | `unhex(text)` | `UNHEX(...)` | `null` при нечётной длине или не шестнадцатеричном символе |
| Строковые | `weight_string(value[, cast, length])` | `WEIGHT_STRING(...)` | только модификаторы `AS CHAR(N)`/`AS BINARY(N)`, оба — константы времени компиляции |
| Числовые | `conv(value, fromBase, toBase)` | `CONV(...)` | целевые основания `2`–`36`, `-1` для знаковой интерпретации |
| Числовые | `crc32(value)` | `CRC32(...)` | беззнаковая 32-битная контрольная сумма CRC-32, расширенная до `long` |
| Числовые | `log2(value)` | `LOG2(...)` | логарифм по основанию 2 |
| Числовые | `rand([seed])` | `RAND([seed])` | `double` в `[0, 1)`; с seed последовательность повторяется |
| Числовые | `truncate(value, places)` | `TRUNCATE(...)` | усечение к нулю; при отрицательном счётчике целая часть обнуляется |
| Бит | `bit_count(value)` | `BIT_COUNT(...)` | число установленных битов в 64-битном представлении значения |
| Дата/время | `adddate(date, days)` | `ADDDATE(...)` | только форма с числом дней; форма с `INTERVAL` не моделируется |
| Дата/время | `addtime(datetime, time)` | `ADDTIME(...)` | прибавляет длительность |
| Дата/время | `convert_tz(datetime, from, to)` | `CONVERT_TZ(...)` | зоны IANA; `null`, если сервер не знает зону |
| Дата/время | `dayname(date)` | `DAYNAME(...)` | название дня недели, например `Wednesday` |
| Дата/время | `from_days(days)` | `FROM_DAYS(...)` | дата по номеру дня, считая от года 0 |
| Дата/время | `get_format(kind, format)` | `GET_FORMAT(...)` | типизированный `MySqlFormatKind` плюс литеральное имя формата (`USA`, `JIS`, `ISO`, `EUR`, `INTERNAL`) |
| Дата/время | `makedate(year, dayOfYear)` | `MAKEDATE(...)` | `null`, если день выходит за пределы года |
| Дата/время | `maketime(hour, minute, second)` | `MAKETIME(...)` | `null`, если какая-либо часть вне диапазона |
| Дата/время | `microsecond(datetime)` | `MICROSECOND(...)` | микросекундная часть, `0`–`999999` |
| Дата/время | `monthname(date)` | `MONTHNAME(...)` | название месяца, например `May` |
| Дата/время | `period_add(period, months)` | `PERIOD_ADD(...)` | период `YYMM`/`YYYYMM`, сдвинутый на число месяцев |
| Дата/время | `period_diff(period1, period2)` | `PERIOD_DIFF(...)` | число месяцев между двумя периодами `YYMM`/`YYYYMM` |
| Дата/время | `sec_to_time(seconds)` | `SEC_TO_TIME(...)` | длительность; дробный вход сохраняет дробную часть |
| Дата/время | `subdate(date, days)` | `SUBDATE(...)` | только форма с числом дней |
| Дата/время | `subtime(datetime, time)` | `SUBTIME(...)` | вычитает длительность |
| Дата/время | `time(datetime)` | `TIME(...)` | часть времени суток |
| Дата/время | `time_format(time, format)` | `TIME_FORMAT(...)` | форматирование длительности по `%`-шаблону |
| Дата/время | `time_to_sec(time)` | `TIME_TO_SEC(...)` | целые секунды: MySQL 8.4 отбрасывает дробную часть аргумента |
| Дата/время | `timediff(datetime1, datetime2)` | `TIMEDIFF(...)` | длительность между двумя значениями |
| Дата/время | `timestamp(datetime)` | `TIMESTAMP(...)` | значение как `DATETIME` |
| Дата/время | `to_days(date)` | `TO_DAYS(...)` | номер дня, считая от года 0 |
| Дата/время | `to_seconds(date)` | `TO_SECONDS(...)` | номер секунды, считая от года 0 |
| Дата/время | `yearweek(date[, mode])` | `YEARWEEK(...)` | год и неделя по режиму по умолчанию или по явно заданному режиму |
| JSON | `json_array(values...)` | `JSON_ARRAY(...)` | каждый аргумент — SQL-значение, поэтому строки становятся JSON-строками |
| JSON | `json_arrayagg(value)` | `JSON_ARRAYAGG(...)` | собирает значения группы в один JSON-массив |
| JSON | `json_arrow(column, path)` | `column -> 'path'` | требует JSON-колонку слева и литерал пути времени компиляции справа |
| JSON | `json_arrow_text(column, path)` | `column ->> 'path'` | некавыченная форма `->`; те же ограничения на колонку и литерал |
| JSON | `json_contains(target, candidate[, path])` | `JSON_CONTAINS(...)` | `1`, если кандидат содержится в цели |
| JSON | `json_contains_path(json, oneOrAll, paths...)` | `JSON_CONTAINS_PATH(...)` | режим должен быть литералом `'one'` или `'all'`, а каждый путь — литералом |
| JSON | `json_merge(first, second[, rest...])` | `JSON_MERGE(...)` | слияние с сохранением дублей; MySQL считает его устаревшим в пользу пары patch/preserve |
| JSON | `json_object(keyValues...)` | `JSON_OBJECT(...)` | чередующиеся ключи и значения; число аргументов должно быть чётным, ключи — строками |
| JSON | `json_objectagg(key, value)` | `JSON_OBJECTAGG(...)` | собирает пары ключ/значение группы в один JSON-объект |
| JSON | `json_overlaps(left, right)` | `JSON_OVERLAPS(...)` | `1`, если у документов есть хотя бы одно общее значение |
| JSON | `json_pretty(json)` | `JSON_PRETTY(...)` | переформатирует документ для читаемости |
| JSON | `json_quote(value)` | `JSON_QUOTE(...)` | значение как JSON-строковый литерал |
| JSON | `json_schema_valid(schema, document)` | `JSON_SCHEMA_VALID(...)` | `1`, если документ проходит проверку по схеме |
| JSON | `json_schema_validation_report(schema, document)` | `JSON_SCHEMA_VALIDATION_REPORT(...)` | JSON-отчёт о проверке |
| JSON | `json_search(json, oneOrAll, search[, escape[, paths...]])` | `JSON_SEARCH(...)` | режим должен быть `'one'` или `'all'`; escape — один символ |
| JSON | `json_storage_size(json)` | `JSON_STORAGE_SIZE(...)` | байты, занятые хранимым представлением |
| JSON | `json_storage_free(json)` | `JSON_STORAGE_FREE(...)` | поставлен в предыдущем срезе: байты, освобождённые в бинарном значении `JSON`-колонки при частичном обновлении на месте, `0`, если ничего не освободилось, `null` для SQL NULL — см. [руководство по MySQL](../guide/provider-specific-sql/mysql.md#нативные-функции) |
| JSON | `member_of(value, jsonArray)` | `value MEMBER OF (jsonArray)` | аргумент-массив должен быть литералом JSON-массива времени компиляции |
| Агрегатные | `bit_and(value)` | `BIT_AND(value)` | побитовое AND всех не-null значений группы, `null` для пустой группы |
| Агрегатные | `bit_or(value)` | `BIT_OR(value)` | побитовое OR всех не-null значений группы |
| Агрегатные | `bit_xor(value)` | `BIT_XOR(value)` | побитовое XOR всех не-null значений группы |
| Шифрование/сжатие/хеш | `aes_encrypt(data, key[, iv, kdf, salt, iterations])` | `AES_ENCRYPT(...)` | MySQL 8.4 по умолчанию использует `aes-128-ecb` |
| Шифрование/сжатие/хеш | `aes_decrypt(data, key[, iv, kdf, salt, iterations])` | `AES_DECRYPT(...)` | `null` при некорректном входе или дополнении |
| Шифрование/сжатие/хеш | `compress(value)` | `COMPRESS(...)` | zlib с префиксом длины несжатых данных |
| Шифрование/сжатие/хеш | `uncompress(value)` | `UNCOMPRESS(...)` | `null`, если вход не является сжатым значением |
| Шифрование/сжатие/хеш | `uncompressed_length(value)` | `UNCOMPRESSED_LENGTH(...)` | длина из заголовка `COMPRESS` |
| Шифрование/сжатие/хеш | `random_bytes(length)` | `RANDOM_BYTES(...)` | криптографически случайные байты |
| Шифрование/сжатие/хеш | `validate_password_strength(password)` | `VALIDATE_PASSWORD_STRENGTH(...)` | оценка зависит от конфигурации проверки паролей на сервере |
| Шифрование/сжатие/хеш | `statement_digest(statement)` | `STATEMENT_DIGEST(...)` | SHA-256-дайджест нормализованного текста инструкции |
| Шифрование/сжатие/хеш | `statement_digest_text(statement)` | `STATEMENT_DIGEST_TEXT(...)` | нормализованная форма текста инструкции |
| Информация | `connection_id()` | — | уже покрыто кросс-провайдерным `CommonFunctions.connection_id()`, рендерится как `CAST(CONNECTION_ID() AS UNSIGNED)`; см. [Информация о сессии и сервере](09-system-functions.md#информация-о-сессии-и-сервере-кросс-провайдерная) |
| Информация | `found_rows()` | `FOUND_ROWS()` | число строк, которое вернул бы последний `SELECT` без своего `LIMIT` |
| Информация | `last_insert_id([value])` | `LAST_INSERT_ID(...)` | первое автоматически сгенерированное значение последней вставки в этом соединении |
| Информация | `row_count()` | `ROW_COUNT()` | число строк, затронутых последней инструкцией в этом соединении |
| Информация | `current_role()` | `CURRENT_ROLE()` | активные роли, `NONE`, если роль не активна |
| Информация | `charset(expression)` | `CHARSET(...)` | набор символов выражения |
| Информация | `collation(expression)` | `COLLATION(...)` | сопоставление выражения |
| Информация | `coercibility(expression)` | `COERCIBILITY(...)` | значение coercibility, используемое для разрешения конфликтов сопоставления |
| Информация | `benchmark(count, expression)` | `BENCHMARK(...)` | вычисляет выражение `count` раз ради побочных эффектов и возвращает `0` |
| Информация | `icu_version()` | `ICU_VERSION()` | версия ICU, с которой собран сервер; пустая строка, если ICU нет |
| Прочее | `uuid_short()` | `UUID_SHORT()` | 64-битный идентификатор, уникальный для экземпляра сервера |
| Прочее | `is_uuid(value)` | `IS_UUID(...)` | `1` для синтаксически корректного строкового UUID, `null` для SQL NULL |
| Прочее | `sleep(seconds)` | `SLEEP(...)` | приостанавливает инструкцию, возвращает `0` или `1`, если сон прерван |
| Прочее | `grouping(expressions...)` | `GROUPING(...)` | битовая карта rollup; каждый аргумент должен совпадать с выражением `GROUP BY` — это обязанность вызывающего кода |
| Прочее | `name_const(name, value)` | `NAME_CONST(...)` | возвращает значение под меткой колонки, заданной литералом времени компиляции |
| Прочее | `column_default(column)` | `DEFAULT(column)` | значение по умолчанию именованной колонки; аргумент обязан быть ссылкой на колонку |

### Ограничения поведения MySQL

Эти формы проверяются до эмиссии любого SQL; несоответствующий вызов отклоняется с
`NotSupportedException`, а не рендерится с другой семантикой:

* `column_default` требует ссылку на отображаемую колонку — константы, параметры, приведения и
  вычисляемые выражения отклоняются.
* `->` и `->>` требуют JSON-колонку слева и литерал пути времени компиляции справа.
* `json_object` требует чётного числа аргументов со строковыми ключами.
* `json_contains_path` и `json_search` требуют литерал `'one'` или `'all'` и литеральные пути.
* `json_search` принимает escape из одного символа.
* `get_format` принимает типизированный вид (`MySqlFormatKind`) и литеральное имя формата.
* `weight_string` поддерживает только модификаторы `AS CHAR(N)`/`AS BINARY(N)`, оба — константы
  времени компиляции.
* `time_to_sec` возвращает целые секунды: MySQL 8.4 отбрасывает дробную часть аргумента.
* `name_const` требует литеральное имя времени компиляции; `member_of` требует литеральный JSON-массив.

### Отказы MySQL

Три имени вендора отклоняются намеренно: член существует только чтобы нести отказ, бросает
`NotSupportedException` до эмиссии любого SQL и покрыт тестом с положительным контролем на том же
провайдере. Точные сообщения перечислены в
[руководстве по MySQL](../guide/provider-specific-sql/mysql.md#ограничения-и-отказы).

| Отклонённое имя | Причина | Куда обращаться |
|---|---|---|
| `uuid` | `UUID()` генерирует значение версии 1; контракт UUID в этом API — v4 (`gen_random_uuid`) или v7 (`uuidv7`), и значение v1 никогда не подставляется вместо него | `uuidv7()` или `UUID()` в сыром SQL, если v1 действительно нужен |
| `values` | у `VALUES(column)` нет контракта скалярного выражения — имя ссылается на новую строку только внутри построителя upsert — и оно устарело в MySQL 8.4 | API upsert построителя мутаций |
| `json_table` | конструирует таблицу: нужна позиция `FROM`/join и список определений `COLUMNS`, поэтому это не скалярная функция | сырой SQL или обёртка `[SqlFunction]` |

## Функции JSON только для MariaDB

`json_equals` и `json_normalize` из
[`SqlFunctions.MySql`](../guide/provider-specific-sql/mysql.md#mariadb) рендерятся только на MariaDB
(проверено на базовой версии 11.4); MySQL, PostgreSQL, SQL Server, SQLite, ClickHouse и
in-memory-провайдер отклоняют оба имени с `NotSupportedException`.

| Член | SQL | Результат | Доступность |
|---|---|---|---|
| `json_equals(json1, json2)` | `json_equals(...)` | `int?` — `1` для равных JSON-документов (порядок ключей объекта не важен), `0`, если различаются, `null` при SQL NULL | [только MariaDB](../guide/provider-specific-sql/mysql.md#mariadb); MySQL и другие провайдеры отклоняют |
| `json_normalize(json)` | `json_normalize(...)` | `string?` — канонический текст JSON с отсортированными ключами объекта и без лишних пробелов (идемпотентно), `null` при SQL NULL | [только MariaDB](../guide/provider-specific-sql/mysql.md#mariadb); MySQL и другие провайдеры отклоняют |

## Явно не поддерживается

Эти случаи бросают `NotSupportedException`, а не генерируют SQL с другой семантикой:

* `string.IsNullOrWhiteSpace(x)` — бросает исключение с сообщением, упоминающим `IsNullOrWhiteSpace`
  (`SqlGenerationTests.IsNullOrWhiteSpace_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:974`).
* `Math.Log(value, base)` — у двухаргументной формы порядок аргументов зависит от провайдера, поэтому она
  оставлена неподдерживаемой (`SqlGenerationTests.MathLogWithBase_ShouldThrowClearException`,
  `tests/nextorm.sqlite.tests/SqlGeneration/SqlGenerationTests.cs:985`). Двухаргументная форма PostgreSQL доступна как
  `SqlFunctions.Postgres.log(base, x)` в расширенной библиотеке скалярных функций. Единственная
  двухаргументная перегрузка PostgreSQL — `log(numeric, numeric)`, поэтому вызов рендерится как
  `log((base)::numeric, (x)::numeric)` с приведением обоих операндов; одноаргументный `Math.Log`
  по-прежнему рендерится как `ln(...)`.
* Перегрузки `Math.Round`, принимающие `MidpointRounding` (больше двух аргументов), — не переносимы.
* `string.Substring(Range)` — нет эквивалента в SQL.
* `string.Trim(c)`/`TrimStart(c)`/`TrimEnd(c)` — SQL обрезает только пробельные символы, не
  произвольный набор символов.
* `string.ToUpper(CultureInfo)`/`ToLower(CultureInfo)` с культурой, отличной от
  `CultureInfo.InvariantCulture` — переносимую форму имеет только инвариантный upper/lower.
* `string.Compare(a, b)` и `string.Compare(a, b, bool)` без `StringComparison` — culture-sensitive;
  используйте `string.Compare(a, b, StringComparison.Ordinal)` или `CompareOrdinal`.
* `StringComparison.InvariantCulture`/`CurrentCulture` (с `IgnoreCase` или без) — нет переносимой формы.
* Спецификаторы `string.Format`/`ToString(format)` вне документированного подмножества в
  [Форматировании дат и чисел в строки](03-date-and-time.md#форматирование-дат-и-чисел-в-строки) — никогда не
  отбрасываются.
* `Regex` в SQL Server 2022 и раньше — функции `REGEXP_*` есть только в SQL Server 2025 (а `REGEXP_LIKE`
  дополнительно требует уровень совместимости БД 170); на 2025+ трансляция работает, а
  `SqlFunctions.Sql.like` остаётся запасным вариантом для простых шаблонов (см.
  [Регулярные выражения](01-string-functions.md#регулярные-выражения)).
* `Regex`-шаблон, замена или `RegexOptions`, не являющиеся константой времени компиляции; опция
  `RegexOptions`, отличная от `IgnoreCase` (при этом `Compiled`/`CultureInvariant` — no-op); прочие
  члены `Regex`, кроме `IsMatch`/`Replace` (например, `Match`, `Split`, группы захвата).
