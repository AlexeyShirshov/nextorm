# Специфичный для MySQL и MariaDB SQL

> `nextorm.mysql` (и `nextorm.mariadb`, который наследует его) открывает нативные строковые и
> условные идиомы MySQL/MariaDB, преобразование дат по `%`-шаблонам и функции Unix-эпохи,
> шестнадцатеричные хеши, пару преобразования IPv4, семейство мутации JSON и пару бинарного UUID на
> [`SqlFunctions.MySql`](xref:NextORM.Core.SqlFunctions.MySql). Поверхность гейтится по имени через
> [`ISqlDialect.MySqlFunctions`](xref:NextORM.Core.ISqlDialect.MySqlFunctions); прочие провайдеры
> отклоняют вызов с `NotSupportedException`.

**Что нужно знать:** [Скалярные функции](../11-scalar-functions.md) · [Провайдер MySQL](../../providers/mysql.md) · [Провайдер MariaDB](../../providers/mariadb.md)

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
| `uuid_to_bin(uuid)` / `bin_to_uuid(binary)` | то же | **только MySQL** (см. ниже) |

Методы мутации JSON принимают одну пару `path`/`value`; MySQL допускает и несколько пар, но
переносимая поверхность держит одну пару на вызов, а значение — строка (MySQL трактует строковый
аргумент как JSON-строку, пока он не является корректным JSON).

```csharp
var updated = dataContext.From<IComplexEntity>()
    .Select(x => SqlFunctions.MySql.json_set(x.Payload, "$.active", "true"))
    .First();
```

## MariaDB

`nextorm.mariadb` наследует `MariaDbDialect` от `MySqlDialect`, поэтому получает всю поверхность выше.
Исключения — `uuid_to_bin`/`bin_to_uuid`: в MariaDB таких функций нет (преобразование идёт через
`CAST(... AS BINARY(16))`/`CAST(... AS UUID)`), поэтому `MariaDbDialect.MySqlFunctions` сообщает их как
неподдерживаемые, и запрос отклоняется с `NotSupportedException`.

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
| `months_between(a, b)` | `months_between(...)` | MariaDB 12.2+; дробные месяцы |
| `to_char(value, format)` | `to_char(...)` | MariaDB 10.6+; маска формата Oracle |
| `to_date(value, format)` | `to_date(...)` | MariaDB 12.3+; разбор в стиле Oracle |
| `to_number(value, format)` | `to_number(...)` | MariaDB 12.2+; возвращает `DOUBLE` |
| `kdf(password, salt, info, kdfName)` | `kdf(...)` | MariaDB 11.3+; вывод ключа, возвращает binary |
| `xxh3(value)` / `xxh32(value)` | то же | MariaDB 13.1+; быстрый 64/32-битный xxHash |
| `json_detailed(json)` / `json_compact(json)` | то же | переформатирование / удаление пробелов |
| `next_value_for(sequence)` | `next value for sequence` | синтаксис ANSI для последовательностей |
| `nextval(sequence)` | `nextval(sequence)` | доступ к последовательности в стиле PostgreSQL |
| `setval(sequence, value)` | `setval(sequence, value)` | задаёт следующее значение последовательности |
| `lastval(sequence)` | `lastval(sequence)` | последнее значение последовательности в соединении |

Члены последовательностей принимают имя как строку и рендерят его идентификатором (не строковым
литералом), поэтому `SqlFunctions.MySql.next_value_for("order_seq")` даёт `next value for order_seq`.

```csharp
var rows = dataContext.From<IOrderEntity>()
    .Select(x => new
    {
        Id = SqlFunctions.MySql.nextval("order_seq"),
        Label = SqlFunctions.MySql.nvl(x.Label, "unnamed"),
        Month = SqlFunctions.MySql.add_months(x.CreatedAt, 1),
        Pretty = SqlFunctions.MySql.json_detailed(x.Payload)
    })
    .ToList();
```

## In-memory и прочие провайдеры

In-memory-провайдер не умеет вычислять эти нативные функции и отклоняет их с
`NotSupportedException`; PostgreSQL, SQL Server, SQLite и ClickHouse отклоняют всю поверхность
`SqlFunctions.MySql` так же. Кросс-провайдерные функции с совпадающей семантикой живут на
[`SqlFunctions.Sql`](../11-scalar-functions.md); эксклюзивные имена остаются здесь.
