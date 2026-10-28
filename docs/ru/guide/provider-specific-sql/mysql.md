# Специфичный для MySQL и MariaDB SQL

> `nextorm.mysql` (и `nextorm.mariadb`, который наследует его) открывает нативные строковые и
> условные идиомы MySQL/MariaDB, преобразование дат по `%`-шаблонам и функции Unix-эпохи,
> шестнадцатеричные хеши, пару преобразования IPv4, семейство мутации JSON и пару бинарного UUID на
> [`SqlFunctions.MySql`](xref:NextORM.Core.SqlFunctions.MySql). Поверхность гейтится по имени через
> [`ISqlDialect.MySqlFunctions`](xref:NextORM.Core.ISqlDialect.MySqlFunctions); прочие провайдеры
> отклоняют вызов с `NotSupportedException`.

**Что нужно знать:** [Скалярные функции](../../scalar-functions/index.md) · [Провайдер MySQL](../../connecting-providers/mysql.md) · [Провайдер MariaDB](../../connecting-providers/mariadb.md)

## Нативные функции

Каждый член [`MySqlFunctions`](xref:NextORM.Core.MySqlFunctions) вызывается через
`SqlFunctions.MySql.*` внутри выражения запроса — так же, как `SqlFunctions.Sql.*`:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Where(x => SqlFunctions.MySql.find_in_set(x.Status, "new,open") > 0)
    .Select(x => new
    {
        Position = SqlFunctions.MySql.field(x.Status, "new", "open", "closed"),
        Part = SqlFunctions.MySql.substring_index(x.Path, "/", 2),
        Price = SqlFunctions.MySql.format(x.Price, 2),
        Hash = SqlFunctions.MySql.md5(x.Name),
        Updated = SqlFunctions.MySql.from_unixtime(x.UpdatedAtUnix)
    })
    .ToList();
```

```sql
select field(status, 'new', 'open', 'closed'), substring_index(path, '/', 2),
       format(price, 2), md5(name), from_unixtime(updated_at_unix)
 from complex_entity
where find_in_set(status, 'new,open') > 0
```

| Член | Рендер | Примечания |
|---|---|---|
| `find_in_set(value, set)` | `find_in_set(value, set)` | позиция с 1 в списке через запятую, `0` если нет |
| `field(value, values...)` | `field(value, v1, ...)` | индекс с 1, `0` если нет |
| `elt(index, values...)` | `elt(index, v1, ...)` | доступ к элементу с 1 |
| `substring_index(value, delimiter, count)` | `substring_index(...)` | при отрицательном `count` счёт справа |
| `format(value, decimals)` | `format(value, decimals)` | группировка разрядов + фиксированные знаки |
| `str_to_date(value, format)` | `str_to_date(...)` | разбор по `%`-шаблону |
| `date_format(value, format)` | `date_format(...)` | формат по `%`-шаблону |
| `from_unixtime(seconds)` | `from_unixtime(...)` | Unix-эпоха в дату/время |
| `unix_timestamp(value)` | `unix_timestamp(...)` | дата/время в секунды Unix-эпохи |
| `md5(value)` / `sha1(value)` / `sha2(value, bits)` | то же | шестнадцатеричные дайджесты |
| `inet_aton(text)` / `inet_ntoa(number)` | то же | IPv4 из/в числовую форму |
| `json_set` / `json_insert` / `json_replace` | то же | мутация JSON по пути |
| `json_remove(json, path)` | `json_remove(...)` | удаляет значение по пути |
| `json_merge_patch` / `json_merge_preserve` | то же | merge-patch по RFC 7396 / слияние с дублями |
| `json_array_append` / `json_array_insert` | то же | добавить в конец / вставить в массив по пути |
| `json_depth` / `json_keys` / `json_length` / `json_type` | то же | интроспекция JSON |
| `json_storage_free(json)` | `json_storage_free(...)` | **только MySQL** — байты, освобождённые в бинарном значении `JSON`-колонки при частичном `JSON_SET`/`JSON_REPLACE`/`JSON_REMOVE`; `0`, если ничего не освободилось, `null` для SQL NULL |
| `uuid_to_bin(uuid)` / `bin_to_uuid(binary)` | то же | **только MySQL** (см. ниже) |

Методы мутации JSON принимают одну пару `path`/`value`; MySQL допускает и несколько пар, но
переносимая поверхность держит одну пару на вызов, а значение — строка (MySQL трактует строковый
аргумент как JSON-строку, пока он не является корректным JSON).

```csharp
var updated = dataContext.From<IComplexEntity>()
    .Select(x => SqlFunctions.MySql.json_set(x.Payload, "$.active", "true"))
    .First();
```

`json_storage_free` — диагностика бинарного JSON-хранилища MySQL: после частичного обновления она
показывает, сколько байт освободило изменение на месте, поэтому число описывает хранимое
представление, а не длину текста JSON. Для литералов, captured-значений и колонок, где обновление
ничего не освободило, результат `0`; при SQL NULL — `null` (никогда не `0`). Реализует её только MySQL.

### Строковые

Тринадцать строковых и бинарных членов: `bin`, `oct`, `hex`, `make_set`, `export_set`, `ord`, `quote`,
`soundex`, `strcmp`, пара Base64 `to_base64`/`from_base64`, `unhex` и `weight_string`. `hex` принимает
бинарные данные, текст и 64-битное целое; `weight_string` поддерживает только модификаторы
`AS CHAR(N)` / `AS BINARY(N)`, и оба должны быть константами времени компиляции; `export_set`
отклоняет константный `count` вне `[0, 64]`.

### Числовые

Пять числовых членов: `conv` (основания 2–36, `-1` для знаковой интерпретации), `crc32` (расширяется
до `long`), `log2`, `rand` (`double` в `[0, 1)`, seed повторяет последовательность) и `truncate`
(усечение к нулю, при отрицательном счётчике целая часть обнуляется).

### Бит

Один битовый член: `bit_count(value)` считает установленные биты в 64-битном представлении бинарного
значения. Три битовых агрегата находятся в разделе [Агрегатные](#агрегатные).

### Дата и время

Двадцать три временных члена: `adddate`/`subdate` (только форма с числом дней), `addtime`/`subtime`,
`convert_tz` (зоны IANA), `dayname`/`monthname`, `from_days`/`to_days`/`to_seconds`, `get_format`,
`makedate`/`maketime`, `microsecond`, `period_add`/`period_diff`, `sec_to_time`, `time`, `time_format`,
`time_to_sec`, `timediff`, `timestamp` и `yearweek`. `get_format` принимает типизированный
`MySqlFormatKind` и литеральное имя формата (`USA`, `JIS`, `ISO`, `EUR`, `INTERNAL`); `time_to_sec`
возвращает целые секунды, потому что MySQL 8.4 отбрасывает дробную часть аргумента.

### JSON

Семнадцать JSON-членов и два оператора пути: `json_array`, `json_arrayagg`, `json_contains`,
`json_contains_path`, `json_merge`, `json_object`, `json_objectagg`, `json_overlaps`, `json_pretty`,
`json_quote`, `json_schema_valid`, `json_schema_validation_report`, `json_search`, `json_storage_size`,
`member_of`, `json_arrow` (`column -> 'path'`) и `json_arrow_text` (`column ->> 'path'`). Операторам
нужна отображаемая JSON-колонка слева и литерал пути времени компиляции справа; `json_object` требует
чётного числа аргументов со строковыми ключами; `json_contains_path` и `json_search` требуют литерал
`'one'` или `'all'` и литеральные пути, а escape в `json_search` — один символ; `member_of` требует
литеральный JSON-массив. `json_storage_free` описан выше, а [`JSON_TABLE`](#ограничения-и-отказы)
отклоняется.

### Агрегатные

Три агрегата по группе: `bit_and`, `bit_or` и `bit_xor`; каждый возвращает `null` для пустой группы и
игнорирует входные SQL NULL.

### Шифрование, сжатие и хеширование

Девять членов: `aes_encrypt`/`aes_decrypt` (в MySQL 8.4 по умолчанию `aes-128-ecb`), пара `compress`
`compress`/`uncompress` плюс `uncompressed_length`, `random_bytes`, `validate_password_strength`,
`statement_digest` и `statement_digest_text`. `validate_password_strength` даёт осмысленную оценку
только при настроенном на сервере компоненте `validate_password`.

### Информация

Девять членов информации о сессии: `found_rows`, `last_insert_id`, `row_count`, `current_role`,
`charset`, `collation`, `coercibility`, `benchmark` и `icu_version`. Идентификатор соединения здесь
**не** дублируется: это по-прежнему кросс-провайдерный `CommonFunctions.connection_id()`, рендерящийся
как `CAST(CONNECTION_ID() AS UNSIGNED)` — см.
[Информация о сессии и сервере](../../scalar-functions/09-system-functions.md#информация-о-сессии-и-сервере-кросс-провайдерная).

### Прочее

Шесть членов: `uuid_short`, `is_uuid`, `sleep`, `grouping`, `name_const` и `column_default`.
`grouping` возвращает битовую карту rollup, и каждый аргумент должен совпадать с выражением
`GROUP BY` — это обязанность вызывающего кода, которую транслятор проверить не может; `name_const`
требует литеральную метку времени компиляции; `column_default` требует ссылку на колонку, поэтому
константы, параметры, приведения и вычисляемые выражения отклоняются.

Каждый член, его SQL-написание и форма результата перечислены в
[таблице соответствия провайдеров](../../scalar-functions/08-reference.md#функции-только-для-mysql).

## Ограничения и отказы

Каждое ограничение и каждый отказ возникают при подготовке, **до эмиссии любого SQL**, и каждый
покрыт тестом с положительным контролем на том же провайдере (после отказа соседние имена
продолжают рендериться). Ограничения:

* `column_default` требует ссылку на колонку; константы, параметры, приведения и вычисляемые
  выражения отклоняются.
* `->` и `->>` требуют JSON-колонку слева и литерал пути времени компиляции справа.
* `json_object` требует чётного числа аргументов со строковыми ключами.
* `json_contains_path` и `json_search` требуют литерал `'one'` или `'all'` и литеральные пути.
* `json_search` принимает escape из одного символа.
* `get_format` принимает типизированный вид и литеральное имя формата; `weight_string` — только
  модификаторы `AS CHAR(N)`/`AS BINARY(N)`; `name_const` — литеральное имя; `member_of` — литеральный
  JSON-массив.
* Константный `count` у `export_set` вне `[0, 64]` отклоняется.
* `time_to_sec` возвращает целые секунды — MySQL 8.4 отбрасывает дробную часть.

Три имени вендора отклоняются полностью; каждый член существует только чтобы нести отказ, и бросает
`NotSupportedException` ровно с таким текстом:

* `json_table()` — `MySQL JSON_TABLE is a table-producing construct that requires a FROM/join position and a COLUMNS definition list; it is not a scalar function and cannot be expressed on SqlFunctions.MySql.`
* `uuid()` — `uuid is refused: MySQL UUID() generates UUID version 1; this API's UUID contract is v4 (gen_random_uuid) or v7 (uuidv7), and a v1 value is never substituted for it. Use uuidv7() to generate an identifier, or UUID() through raw SQL when v1 is genuinely intended.`
* `values(column)` — `MySQL VALUES(column) cannot be translated as a scalar query function; use the mutation builder's upsert API.`

`JSON_TABLE` — таблице-образующая конструкция, а не скаляр: ей нужна позиция `FROM`/join и список
определений `COLUMNS`, поэтому она достижима только через сырой SQL или обёртку `[SqlFunction]`. То же
верно для всей нативной поверхности MySQL: отказы — это детерминированные исходы транслятора, а не
пробелы, оставленные на будущее.

## MariaDB

`nextorm.mariadb` наследует `MariaDbDialect` от `MySqlDialect`, поэтому получает всю поверхность выше.
Три имени, которые он никогда не поддерживал, остаются исключёнными: `uuid_to_bin`/`bin_to_uuid` и
`json_storage_free`. В MariaDB таких функций нет (`uuid_to_bin`/`bin_to_uuid` преобразуют через
`CAST(... AS BINARY(16))`/`CAST(... AS UUID)`, а учёта освобождённого места в бинарном JSON MariaDB не
реализует), поэтому `MariaDbDialect.MySqlFunctions` сообщает их как неподдерживаемые, и запрос
отклоняется с `NotSupportedException`.

Каталог MySQL, реализованный в #283, проверен на MySQL 8.4. Оценка его поддержки в MariaDB завершена
в #284: перечень из 138 позиций содержит 109 реализованных имён, 6 ранее поддержанных, 18 явных отказов
и 5 функций, доступных начиная с MariaDB 11.7; ожидающих решения позиций нет. Проверка MariaDB выполнена
на версии 11.4.13; функции с ограничением 11.7 не выполнялись. Постоянные исключения `UUID_TO_BIN`,
`BIN_TO_UUID` и `JSON_STORAGE_FREE` сохраняются. См.
[каталог MariaDB](../../scalar-functions/08-reference.md#mariadb-catalogue): в нём указаны статус каждого
имени, ограничения версий, причины отказов и оговорки к результатам проверки.

Поверх наследуемого набора MariaDB добавляет собственные имена через тот же вход `SqlFunctions.MySql`.
Диалект MySQL их не сообщает, поэтому запрос MySQL с таким именем отклоняется с `NotSupportedException`:

| Член | Рендер | Примечания |
|---|---|---|
| `regexp_instr(value, pattern)` | `regexp_instr(...)` | позиция первого совпадения с 1, `0` если нет |
| `regexp_substr(value, pattern)` | `regexp_substr(...)` | совпавшая подстрока, пусто если нет |
| `regexp_replace(value, pattern, replacement)` | `regexp_replace(...)` | заменяет все совпадения |
| `nvl(value, fallback)` | `nvl(...)` | синоним `IFNULL` |
| `nvl2(value, whenNotNull, whenNull)` | `nvl2(...)` | выбор по NULL-ности `value` |
| `add_months(date, months)` | `add_months(...)` | MariaDB 10.6.1+; при переполнении — последний день месяца |
| `months_between(a, b)` | `months_between(...)` | MariaDB 12.2.1+ (принудительный минимум; 12.2 и 12.2.0 отклоняются); дробные месяцы |
| `to_char(value, format)` | `to_char(...)` | MariaDB 10.6+; маска формата Oracle |
| `to_date(value, format)` | `to_date(...)` | MariaDB 12.3+; разбор в стиле Oracle |
| `to_number(value, format)` | `to_number(...)` | семейство MariaDB 12.2, принудительный минимум 12.2.1; возвращает `DOUBLE` |
| `kdf(password, salt, info, kdfName)` | `kdf(...)` | MariaDB 11.3+; вывод ключа, возвращает binary |
| `xxh3(value)` / `xxh32(value)` | то же | MariaDB 13.1+; быстрый 64/32-битный xxHash |
| `json_detailed(json)` / `json_compact(json)` | то же | переформатирование / удаление пробелов |
| `json_equals(json1, json2)` | `json_equals(...)` | `1`, если JSON-документы равны (порядок ключей объекта не важен), `0`, если различаются, `null` при SQL NULL; базовая версия MariaDB 11.4, MySQL отклоняет |
| `json_normalize(json)` | `json_normalize(...)` | канонический текст JSON (ключи объекта отсортированы, лишние пробелы удалены, идемпотентно), `null` при SQL NULL; базовая версия MariaDB 11.4, MySQL отклоняет |

Эти три Oracle-совместимые конверсии (`months_between`, `to_date`, `to_number`) **зависят от версии**:
в MariaDB 11.4 их нет, поэтому `MariaDbDialect` рендерит каждую только тогда, когда контекст создан с явно
заданной версией сервера не ниже её минимума. **Без версии** (конструктор по умолчанию с двумя аргументами)
MariaDB-контекст отклоняет вызов с `NotSupportedException` при подготовке, вместо того чтобы эмитить SQL,
который сервер отвергнет; платформенные эквиваленты (`TIMESTAMPDIFF`, `STR_TO_DATE`, `CAST(... AS DECIMAL)`)
не подставляются. Генераторы UUID подчиняются тому же правилу с минимумом MariaDB 11.7 (см.
[Последовательности и генераторы UUID](../../scalar-functions/10-sequences.md)).

Доступ к последовательностям провайдерный; см. общий раздел
[Последовательности и генераторы UUID](../../scalar-functions/10-sequences.md).

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new
    {
        Label = SqlFunctions.MySql.nvl(x.Label, "unnamed"),
        Month = SqlFunctions.MySql.add_months(x.CreatedAt, 1),
        Pretty = SqlFunctions.MySql.json_detailed(x.Payload)
    })
    .ToList();
```

## Null-safe-сравнение (MySQL и MariaDB)

У обоих провайдеров есть оператор null-safe-равенства `<=>`:
`SqlFunctions.Sql.is_not_distinct_from(a, b)` рендерит `(a <=> b)`, а `is_distinct_from(a, b)` — его
дополнение `(not (a <=> b))`. MariaDB наследует тот же рендеринг из `MySqlDialect`; обе формы двузначны
(1/0). Обычные `==`/`!=` сохраняют семантику SQL NULL. См.
[Null-safe-сравнение](../../scalar-functions/04-conditionals-and-conversion.md#null-safe-сравнение-is-not-distinct-from).

## In-memory и прочие провайдеры

In-memory-провайдер не умеет вычислять эти нативные функции и отклоняет их с
`NotSupportedException`; PostgreSQL, SQL Server, SQLite и ClickHouse отклоняют всю поверхность
`SqlFunctions.MySql` так же. Кросс-провайдерные функции с совпадающей семантикой живут на
[`SqlFunctions.Sql`](../../scalar-functions/index.md); эксклюзивные имена остаются здесь.
