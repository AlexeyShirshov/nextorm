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
| Встроенные табличные функции | `NotSupportedException` | `string_split(...)`, `openjson(...)` | `generate_series(...)`, `unnest(...)` |

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

## Функции только для MySQL

Имена из [`SqlFunctions.MySql`](../guide/provider-specific-sql/mysql.md) рендерятся только провайдером
MySQL; PostgreSQL, SQL Server, SQLite, ClickHouse и in-memory-провайдер отклоняют их с
`NotSupportedException`. Эта половина каталога **поставлена в
[#283](https://github.com/AlexeyShirshov/nextorm/issues/283) и проверена только на MySQL 8.4**.
Свидетельства MariaDB отдельны и никогда не выводятся из MySQL:
[#284](https://github.com/AlexeyShirshov/nextorm/issues/284) перепроверил каждое имя на MariaDB 11.4.13 и
записал результат в [каталоге MariaDB](#каталог-mariadb) — поэтому четырнадцать имён, заявленных в этой
половине, там отклонены.

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

<a id="mariadb-only-json-functions"></a>

## Каталог MariaDB

`SqlFunctions.MySql` несёт обе половины: имена, которые рендерит только MySQL, — в разделе выше, а имена,
которые рендерит MariaDB, перечислены здесь. Член только для MariaDB отклоняют MySQL, PostgreSQL,
SQL Server, SQLite, ClickHouse и in-memory-провайдер.
[#283](https://github.com/AlexeyShirshov/nextorm/issues/283) поставил каталог MySQL;
[#284](https://github.com/AlexeyShirshov/nextorm/issues/284) закрывает его оценку для MariaDB и добавляет
поверхность только для MariaDB.

Таблица покрывает все 138 кандидатов инвентаря #284: **109 поставлено / 6 уже покрыто / 18 отклонено с
причиной / 5 ограничено версией (MariaDB 11.7) / 0 в ожидании**. Проверка выполнена на
**MariaDB 11.4.13**; пять функций, ограниченных версией 11.7, не исполнялись, и поведение MariaDB никогда
не выводится из результата MySQL. В третьей колонке `11.4` означает «присутствует на проверенной базовой
версии 11.4.13», меньшее значение — документированный минимум, большее — непроверенное ограничение.
Семнадцать поставленных строк возвращают 32-битное поле провайдера там, где общий член типизирован под
более широкий тип MySQL: утверждаемый контракт — объявленный CLR-тип, а расширение выполняется без потерь.

### Поставлено

| Inventory name | Public API / form | MariaDB minimum or gate | Behaviour / refusal reason / verification qualification |
|---|---|---|---|
| `JSON_ARRAY` | `json_array(values...)` | 11.4 | принято на 11.4.13 (`[1, 2]`); форма безтипового NULL **не сравнима** (бинарные сетевые метаданные дают `byte[]`), форма с не-NULL значением проверена |
| `JSON_ARRAYAGG` | `json_arrayagg(value)` | 11.4 | принято на 11.4.13 (`[1,2]`) |
| `JSON_CONTAINS` | `json_contains(target, candidate[, path])` | 11.4 | принято на 11.4.13 (`1`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `JSON_CONTAINS_PATH` | `json_contains_path(json, oneOrAll, paths...)` | 11.4 | принято на 11.4.13 (`1`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `JSON_MERGE` | `json_merge(first, second[, rest...])` | 11.4 | принято на 11.4.13 (`{"a": 1, "b": 2}`) |
| `JSON_OBJECT` | `json_object(keyValues...)` | 11.4 | принято на 11.4.13 (`{"a": 1}`) |
| `JSON_OBJECTAGG` | `json_objectagg(key, value)` | 11.4 | принято на 11.4.13 (`{"1":2}`) |
| `JSON_OVERLAPS` | `json_overlaps(left, right)` | 11.4 | принято на 11.4.13 (`1`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `JSON_PRETTY` | `json_pretty(json)` | 11.4 | принято на 11.4.13 (документ переформатирован с отступами на несколько строк) |
| `JSON_QUOTE` | `json_quote(value)` | 11.4 | принято на 11.4.13 (`"a"`); форма безтипового NULL **не сравнима** (бинарные сетевые метаданные дают `byte[]`), форма с не-NULL значением проверена |
| `JSON_SCHEMA_VALID` | `json_schema_valid(schema, document)` | 11.4 | принято на 11.4.13 (`1`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `JSON_SEARCH` | `json_search(json, oneOrAll, search[, escape[, paths...]])` | 11.4 | принято на 11.4.13 (`"$.a"`) |
| `BIN` | `bin(value)` | 11.4 | принято на 11.4.13 (`101`) |
| `EXPORT_SET` | `export_set(bits, on, off[, separator[, count]])` | 11.4 | принято на 11.4.13 (битовая строка из 64 символов `Y,N,Y,N,...` для `5`) |
| `HEX` | `hex(value)` | 11.4 | принято на 11.4.13 (`616263`) |
| `MAKE_SET` | `make_set(bits, values...)` | 11.4 | принято на 11.4.13 (`a`) |
| `OCT` | `oct(value)` | 11.4 | принято на 11.4.13 (`10`) |
| `ORD` | `ord(value)` | 11.4 | принято на 11.4.13 (`65`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `QUOTE` | `quote(value)` | 11.4 | принято на 11.4.13 (`'a\'b'`); форма безтипового NULL **не сравнима** (бинарные сетевые метаданные дают `byte[]`), форма с не-NULL значением проверена |
| `SOUNDEX` | `soundex(value)` | 11.4 | принято на 11.4.13 (`H400`) |
| `STRCMP` | `strcmp(left, right)` | 11.4 | принято на 11.4.13 (`-1`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `TO_BASE64` | `to_base64(value)` | 11.4 | принято на 11.4.13 (`YWJj`) |
| `FROM_BASE64` | `from_base64(text)` | 11.4 | принято на 11.4.13 (`byte[]` с байтами `abc`) |
| `UNHEX` | `unhex(text)` | 11.4 | принято на 11.4.13 (`byte[]` с байтами `ab`) |
| `WEIGHT_STRING` | `weight_string(value[, cast, length])` | 11.4 | принято на 11.4.13 (`byte[]`, `2075208F20A9`) |
| `CONV` | `conv(value, fromBase, toBase)` | 11.4 | принято на 11.4.13 (`255`) |
| `CRC32` | `crc32(value)` | 11.4 | принято на 11.4.13 (`891568578`); сетевой тип провайдера `UInt32`, без потерь расширен до объявленного CLR-контракта |
| `LOG2` | `log2(value)` | 11.4 | принято на 11.4.13 (`3`) |
| `RAND` | `rand([seed])` | 11.4 | принято на 11.4.13 (`0.6629110161800972`; значение не детерминировано) |
| `TRUNCATE` | `truncate(value, places)` | 11.4 | принято на 11.4.13 (`1.23`) |
| `BIT_COUNT` | `bit_count(value)` | 11.4 | принято на 11.4.13 (`2`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `BIT_AND` | `bit_and(value)` | 11.4 | принято на 11.4.13 (`1`) |
| `BIT_OR` | `bit_or(value)` | 11.4 | принято на 11.4.13 (`3`) |
| `BIT_XOR` | `bit_xor(value)` | 11.4 | принято на 11.4.13 (`2`) |
| `ADDDATE` | `adddate(date, days)` | 11.4 | принято на 11.4.13 (`2020-01-02`) |
| `ADDTIME` | `addtime(datetime, time)` | 11.4 | принято на 11.4.13 (`2020-01-01 01:00:00`) |
| `CONVERT_TZ` | `convert_tz(datetime, from, to)` | 11.4 | принято на 11.4.13 (`2020-01-01T02:00:00`) |
| `DAYNAME` | `dayname(date)` | 11.4 | принято на 11.4.13 (`Wednesday`) |
| `FROM_DAYS` | `from_days(days)` | 11.4 | принято на 11.4.13 (`2020-07-29T00:00:00`) |
| `GET_FORMAT` | `get_format(kind, format)` | 11.4 | принято на 11.4.13 (`%d.%m.%Y`) |
| `MAKEDATE` | `makedate(year, dayOfYear)` | 11.4 | принято на 11.4.13 (`2020-02-01T00:00:00`) |
| `MAKETIME` | `maketime(hour, minute, second)` | 11.4 | принято на 11.4.13 (`12:34:56`) |
| `MICROSECOND` | `microsecond(datetime)` | 11.4 | принято на 11.4.13 (`123456`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `MONTHNAME` | `monthname(date)` | 11.4 | принято на 11.4.13 (`January`) |
| `PERIOD_ADD` | `period_add(period, months)` | 11.4 | принято на 11.4.13 (`202403`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `PERIOD_DIFF` | `period_diff(period1, period2)` | 11.4 | принято на 11.4.13 (`1`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `SEC_TO_TIME` | `sec_to_time(seconds)` | 11.4 | принято на 11.4.13 (`01:01:01`) |
| `SUBDATE` | `subdate(date, days)` | 11.4 | принято на 11.4.13 (`2019-12-31`) |
| `SUBTIME` | `subtime(datetime, time)` | 11.4 | принято на 11.4.13 (`2019-12-31 23:00:00`) |
| `TIME` | `time(datetime)` | 11.4 | принято на 11.4.13 (`12:34:56`) |
| `TIME_FORMAT` | `time_format(time, format)` | 11.4 | принято на 11.4.13 (`12:34`) |
| `TIME_TO_SEC` | `time_to_sec(time)` | 11.4 | принято на 11.4.13 (`45296`) |
| `TIMEDIFF` | `timediff(datetime1, datetime2)` | 11.4 | принято на 11.4.13 (`1.00:00:00`) |
| `TIMESTAMP` | `timestamp(datetime)` | 11.4 | принято на 11.4.13 (`2020-01-01T00:00:00`) |
| `TO_DAYS` | `to_days(date)` | 11.4 | принято на 11.4.13 (`737790`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `TO_SECONDS` | `to_seconds(date)` | 11.4 | принято на 11.4.13 (`63745056000`) |
| `YEARWEEK` | `yearweek(date[, mode])` | 11.4 | принято на 11.4.13 (`201952`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `AES_ENCRYPT` | `aes_encrypt(data, key[, iv, kdf, salt, iterations])` | 11.4 | принято на 11.4.13 (`byte[]`; при фиксированном ключе AES шифротекст стабилен) |
| `AES_DECRYPT` | `aes_decrypt(data, key[, iv, kdf, salt, iterations])` | 11.4 | принято на 11.4.13 (`byte[]` с открытым текстом `text`) |
| `COMPRESS` | `compress(value)` | 11.4 | принято на 11.4.13 (`byte[]` с zlib-кадром и префиксом длины) |
| `UNCOMPRESS` | `uncompress(value)` | 11.4 | принято на 11.4.13 (`byte[]` с байтами `abc`) |
| `UNCOMPRESSED_LENGTH` | `uncompressed_length(value)` | 11.4 | принято на 11.4.13 (`3`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `RANDOM_BYTES` | `random_bytes(length)` | 11.4 | принято на 11.4.13 (`byte[]`; значение случайно) |
| `FOUND_ROWS` | `found_rows()` | 11.4 | принято на 11.4.13 (`1`) |
| `LAST_INSERT_ID` | `last_insert_id([value])` | 11.4 | принято на 11.4.13 (`0`) |
| `ROW_COUNT` | `row_count()` | 11.4 | принято на 11.4.13 (`-1`) |
| `CURRENT_ROLE` | `current_role()` | 11.4 | принято на 11.4.13 (SQL NULL для анонимного соединения, `string?`) |
| `CHARSET` | `charset(expression)` | 11.4 | принято на 11.4.13 (`utf8mb4`) |
| `COLLATION` | `collation(expression)` | 11.4 | принято на 11.4.13 (`utf8mb4_uca1400_ai_ci`) |
| `COERCIBILITY` | `coercibility(expression)` | 11.4 | принято на 11.4.13 (`4`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `BENCHMARK` | `benchmark(count, expression)` | 11.4 | принято на 11.4.13 (`0`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `UUID_SHORT` | `uuid_short()` | 11.4 | принято на 11.4.13 (`102141557221621760`; уникально для экземпляра сервера) |
| `SLEEP` | `sleep(seconds)` | 11.4 | принято на 11.4.13 (`0`); сетевой тип провайдера `Int32`, без потерь расширен до объявленного CLR-контракта |
| `NAME_CONST` | `name_const(name, value)` | 11.4 | принято на 11.4.13 (`1`) |
| `COLUMN_DEFAULT` | `column_default(column)` | 11.4 | принято на 11.4.13 (`7`) |
| `SFORMAT` | `sformat(format, values...)` | 11.4 | принято на 11.4.13 (`42`) |
| `TRIM_ORACLE` | `trim_oracle(value)` | 11.4 | принято на 11.4.13 (`a`) |
| `NATURAL_SORT_KEY` | `natural_sort_key(value)` | 11.4 | принято на 11.4.13 (`a110`) |
| `LENGTHB` | `lengthb(value)` | 11.4 | принято на 11.4.13 (`3`) |
| `CHR` | `chr(value)` | 11.4 | принято на 11.4.13 (`A`) |
| `FORMAT_PICO_TIME` | `format_pico_time(picoseconds)` | 11.4 | принято на 11.4.13 (`  1 ps`) |
| `DECODE_ORACLE` | `decode_oracle(expression, search, result, ...)` | 11.4 | принято на 11.4.13 (`a`) |
| `ROWNUM` | `rownum()` | 11.4 | принято на 11.4.13 (`1`); **только форма функции без аргументов** — рендерится ровно `ROWNUM()`, а голый псевдостолбец требует `sql_mode=ORACLE` и не поставлен |
| `DECODE_HISTOGRAM` | `decode_histogram(bucket, value)` | 11.4 | принято на 11.4.13 (`0.380,0.004,0.004,0.612`) |
| `BINLOG_GTID_POS` | `binlog_gtid_pos(file, position)` | 11.4 + binary logging | имя разрешается на 11.4.13, но живой случай **пропущен по предусловию на стоковом контейнере** (ошибка 1381, бинарный лог выключен); отдельно проверено на экземпляре 11.4.13 с включённым бинарным логом — `VARCHAR`, допускает NULL, значение `0-1-5` |
| `ENCODE` | `encode(value, password)` | 11.4 | принято на 11.4.13 (`byte[]`; при фиксированном пароле шифротекст стабилен) |
| `DECODE` | `decode(value, password)` | 11.4 | принято на 11.4.13 (`byte[]` с байтами `abc`) |
| `ENCRYPT` | `encrypt(value[, salt])` | 11.4 | принято на 11.4.13 (`abcFZSxKKdq5s6`) |
| `DES_ENCRYPT` | `des_encrypt(value[, key])` | 11.4 (removed in 13.0) | принято на 11.4.13 (`byte[]`); есть в базовой версии 11.4 и удалено в MariaDB 13.0 |
| `DES_DECRYPT` | `des_decrypt(value[, key])` | 11.4 (removed in 13.0) | принято на 11.4.13 (`byte[]` с байтами `abc`); удалено в MariaDB 13.0 |
| `OLD_PASSWORD` | `old_password(value)` | 11.4 | принято на 11.4.13 (`7cd2b5942be28759`) |
| `CRC32C` | `crc32c(value)` | 11.4 | принято на 11.4.13 (`910901175`) |
| `SYS_GUID` | `sys_guid()` | 11.4 | принято на 11.4.13 (шестнадцатеричный идентификатор из 32 символов; значение случайно) |
| `JSON_LOOSE` | `json_loose(json)` | 11.4 | принято на 11.4.13 (`{"a": 1, "b": [2, 3]}`) |
| `JSON_ARRAY_INTERSECT` | `json_array_intersect(first, second[, rest...])` | 11.2 | принято на 11.4.13 (`[1, 2]`) |
| `JSON_KEY_VALUE` | `json_key_value(json, path)` | 11.2 | принято на 11.4.13 (`[{"key": "a", "value": 1}]`) |
| `JSON_OBJECT_FILTER_KEYS` | `json_object_filter_keys(json, keys...)` | 11.2 | принято на 11.4.13 (`byte[]` с JSON-объектом `{"a": 1}`) |
| `JSON_OBJECT_TO_ARRAY` | `json_object_to_array(json)` | 11.2 | принято на 11.4.13 (`byte[]` с `[["a", 1]]`) |
| `PREVIOUS VALUE FOR` | `previous_value_for(sequence)` | 11.4 | принято на 11.4.13 (`1`); грамматическая форма, поэтому имя последовательности — некавыченный идентификатор (`previous value for t350_seq`); возвращает значение, которое произвёл `NEXT VALUE FOR` этого соединения |
| `COLUMN_CREATE` | `column_create(name, value, ...)` | 11.4 | принято на 11.4.13 (`byte[]`, блоб динамических колонок) |
| `COLUMN_ADD` | `column_add(dynamicColumn, name, value, ...)` | 11.4 | принято на 11.4.13 (`byte[]`, расширенный блоб) |
| `COLUMN_DELETE` | `column_delete(dynamicColumn, name, ...)` | 11.4 | принято на 11.4.13 (`byte[]`, уменьшенный блоб) |
| `COLUMN_JSON` | `column_json(dynamicColumn)` | 11.4 | принято на 11.4.13 (`{"a":1}`) |
| `COLUMN_LIST` | `column_list(dynamicColumn)` | 11.4 | принято на 11.4.13 (имя колонки в обратных кавычках: `` `a` ``) |
| `COLUMN_EXISTS` | `column_exists(dynamicColumn, name)` | 11.4 | принято на 11.4.13 (`1`) |
| `COLUMN_CHECK` | `column_check(dynamicColumn)` | 11.4 | принято на 11.4.13 (`1`) |
| `WSREP_LAST_SEEN_GTID` | `wsrep_last_seen_gtid()` | 11.4 | принято на обычном сервере без Galera на 11.4.13 (`byte[]` с текстом `0-0-0`) |
| `WSREP_LAST_WRITTEN_GTID` | `wsrep_last_written_gtid()` | 11.4 | принято на обычном сервере без Galera на 11.4.13 (`byte[]` с текстом `0-0-0`) |
| `WSREP_SYNC_WAIT_UPTO_GTID` | `wsrep_sync_wait_upto_gtid(gtid)` | 11.4 | принято на 11.4.13 (`1`); на сервере без Galera возвращает строку `1` |

### Уже покрыто

| Inventory name | Public API / form | MariaDB minimum or gate | Behaviour / refusal reason / verification qualification |
|---|---|---|---|
| `JSON_NORMALIZE` | `json_normalize(json)` | 11.4 | уже покрыто поверхностью MariaDB из T284 (`json_normalize`, живой тест `T284_JsonNormalize_ShouldEvaluate`): канонический текст с отсортированными ключами, идемпотентно; принято на 11.4.13 (`{"a":1.0E0,"b":2.0E0}`). |
| `JSON_EQUALS` | `json_equals(json1, json2)` | 11.4 | уже покрыто поверхностью из T284 (`json_equals`, живой тест `T284_JsonEquals_ShouldEvaluate`): `1` для равных документов (порядок ключей не важен); принято на 11.4.13 (`1`). |
| `MEDIAN` | `median(value)` | 10.3 | уже покрыто работой по перцентилям: компилируется в `percentile_cont(0.5) within group (order by ...) over (...)`; принято на 11.4.13 (`2`). |
| `PERCENTILE_CONT` | `SqlFunctions.Sql.percentile_cont(fraction, value)` | 10.3 | уже покрыто: нативный оконный перцентиль `percentile_cont(f) within group (order by ...) over (...)`; принято на 11.4.13 (`2`). |
| `PERCENTILE_DISC` | `SqlFunctions.Sql.percentile_disc(fraction, value)` | 10.3 | уже покрыто: нативный оконный перцентиль `percentile_disc(f) within group (order by ...) over (...)`; принято на 11.4.13 (`1`). |
| `NEXT VALUE FOR` | `next_value_for(sequence)` | 11.4 | уже покрыто на шве последовательностей: рендерит `next value for <sequence>` с именем как идентификатором; принято на 11.4.13 (`1`). |

### Отказы

Каждое имя ниже отклоняется до эмиссии любого SQL (`NotSupportedException`); причина цитирует наблюдение
на 11.4.13, на котором основано решение.

| Inventory name | Public API / form | MariaDB minimum or gate | Behaviour / refusal reason / verification qualification |
|---|---|---|---|
| `UUID_TO_BIN` | `uuid_to_bin(value)` | none — absent on 11.4.13 | на сервере нет `UUID_TO_BIN` (ошибка 1305 в форме значения и в форме NULL); MariaDB выполняет это приведение через `CAST`. Наследуется из постоянного набора исключений из трёх имён; член бросает `NotSupportedException` до любого SQL. |
| `BIN_TO_UUID` | `bin_to_uuid(value)` | none — absent on 11.4.13 | нет `BIN_TO_UUID` (ошибка 1305 в обеих формах); MariaDB выполняет это приведение через `CAST`. Наследуется из того же постоянного набора исключений. |
| `JSON_STORAGE_FREE` | `json_storage_free(json)` | none — absent on 11.4.13 | ошибка 1305 в обеих формах: в MariaDB нет учёта обновлений бинарного JSON. Наследуется из постоянного набора исключений, хотя на MySQL имя поставлено. |
| `JSON_ARROW` | `json_arrow(column, path)` | none — 11.4 parser rejects the form | ошибка 1064 на токене `->` и повторно в написании с пробелами `'{"a":1}' -> '$.a'`; исправленная инструкция тоже отклоняется, значит это отсутствие, а не опечатка. Принятый контроль — `JSON_EXTRACT`. |
| `JSON_ARROW_TEXT` | `json_arrow_text(column, path)` | none — 11.4 parser rejects the form | ошибка 1064 на токене `->>` и в написании с пробелами; MariaDB извлекает через `JSON_EXTRACT`/`JSON_VALUE`. |
| `JSON_SCHEMA_VALIDATION_REPORT` | `json_schema_validation_report(schema, document)` | none — absent on 11.4.13 | ошибка 1305 `FUNCTION ... does not exist` в единственной форме. |
| `JSON_STORAGE_SIZE` | `json_storage_size(json)` | none — absent on 11.4.13 | ошибка 1305 в обеих формах: нет учёта хранения бинарного JSON. |
| `MEMBER_OF` | `member_of(value, jsonArray)` | none — 11.4 parser rejects the form | ошибка 1064 на токене `OF` для `1 MEMBER OF ('[1,2]')` и для формы без скобок `1 MEMBER OF JSON_ARRAY(1,2)`; принятый контроль — `JSON_CONTAINS`. |
| `VALIDATE_PASSWORD_STRENGTH` | `validate_password_strength(password)` | none — absent on 11.4.13 | ошибка 1305 в обеих формах; компонент отсутствует в стоковом образе 11.4. |
| `STATEMENT_DIGEST` | `statement_digest(statement)` | none — absent on 11.4.13 | ошибка 1305 в обеих формах; функции дайджеста performance-schema не встроены. |
| `STATEMENT_DIGEST_TEXT` | `statement_digest_text(statement)` | none — absent on 11.4.13 | ошибка 1305 в обеих формах; родственное имя `STATEMENT_DIGEST`. |
| `ICU_VERSION` | `icu_version()` | none — absent on 11.4.13 | ошибка 1305; образ не содержит идентификатора сборки ICU. |
| `IS_UUID` | `is_uuid(value)` | none — absent on 11.4.13 | ошибка 1305 в обеих формах. |
| `GROUPING` | `grouping(expressions...)` | none — absent on 11.4.13 | ошибка 1305 в форме `WITH ROLLUP`; в MariaDB нет агрегата `GROUPING()`. |
| `TRUNC` | `trunc(value)` | none — absent on 11.4.13, ORACLE mode included | ошибка 1305 для строковой формы в обычном режиме и для `TRUNC(SYSDATE)`, `TRUNC(CAST(... AS DATETIME),'DD')` и `TRUNC(123.456)` при `sql_mode=ORACLE`, при этом живой режим ORACLE подтверждён голым `ROWNUM`. Для числового усечения используйте `TRUNCATE`. |
| `FORMAT_BYTES` | — (no member; the dialect renderer refuses) | none — absent on 11.4.13 | ошибка 1305 в обеих формах; члена поверхности у имени нет, поэтому отказ выдаёт рендерер диалекта. |
| `_rowid` | `_rowid()` | n/a — table-dependent precondition | нужна исходная таблица с единственной целочисленной колонкой первичного ключа, а поверхность функций не может проверить это условие. `Int32` на таблице rowid с одним целочисленным первичным ключом; ошибка 1054 `Unknown column` на таблице без PK, с текстовым PK и с составным PK. |
| `COLUMN_GET` | `column_get(value, path)` | none — no compile-time `AS` clause | требуется клауза типа `AS` времени компиляции, которую поддерживаемая поверхность функций не может безопасно представить. Токен обязателен (ошибка 1064 для формы с двумя аргументами) и выбирает CLR-тип: `unsigned`→`UInt64`, `char`→`String`, `int`→`Int64`; на `COLUMN_CREATE`/`COLUMN_ADD` токен отклоняется. |

### Ограничения по версии

| Inventory name | Public API / form | MariaDB minimum or gate | Behaviour / refusal reason / verification qualification |
|---|---|---|---|
| `VEC_FromText` | `vec_from_text(text)` | 11.7 | требует MariaDB 11.7 или новее; ошибка 1305 на базовой версии 11.4.13, не исполнялось — член бросает `NotSupportedException` с текстом ограничения 11.7. |
| `VEC_ToText` | `vec_to_text(vector)` | 11.7 | требует MariaDB 11.7 или новее; ошибка 1305 на базовой версии 11.4.13, не исполнялось — член бросает `NotSupportedException` с текстом ограничения 11.7. |
| `VEC_DISTANCE_EUCLIDEAN` | `vec_distance_euclidean(left, right)` | 11.7 | требует MariaDB 11.7 или новее; ошибка 1305 на базовой версии 11.4.13, не исполнялось — член бросает `NotSupportedException` с текстом ограничения 11.7. |
| `VEC_DISTANCE_COSINE` | `vec_distance_cosine(left, right)` | 11.7 | требует MariaDB 11.7 или новее; ошибка 1305 на базовой версии 11.4.13, не исполнялось — член бросает `NotSupportedException` с текстом ограничения 11.7. |
| `VEC_DISTANCE` | `vec_distance(left, right)` | 11.7 | требует MariaDB 11.7 или новее; ошибка 1305 на базовой версии 11.4.13, не исполнялось — член бросает `NotSupportedException` с текстом ограничения 11.7. |

## Явно не поддерживается

Отказы MariaDB — отдельная категория, и здесь они не дублируются: это имена вендора, которых нет в
MariaDB 11.4.13 (или которые нельзя выразить через эту поверхность), и каждое сохраняет точную причину и
улику в [каталоге MariaDB](#каталог-mariadb). Список ниже — не зависящая от провайдера поверхность API,
которую не рендерит ни один провайдер.

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
