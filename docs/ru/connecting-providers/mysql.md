# Провайдер MySQL

> Используйте `nextorm.mysql` для MySQL 8.x. Провайдер формирует SQL с параметрами `@name`, идентификаторами в обратных кавычках, конкатенацией `concat(...)`, постраничной выборкой `limit`/`offset` и именами агрегатов стандартного отклонения и дисперсии.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [Быстрый старт](../getting-started/02-quickstart.md)

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.MySql;

// Из строки подключения.
var builder = new DataContextBuilder()
    .UseMySql("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

// Из существующего соединения, принадлежащего вызывающему коду.
using var connection = new MySqlConnector.MySqlConnection("Server=localhost;Database=app");
var byConnection = new DataContextBuilder().UseMySql(connection);

using var ctx = builder.CreateDataContext();   // IDataContext
```

Также можно создать контекст напрямую (именно так поступают тесты провайдера):

```csharp
using NextORM.Core;
using NextORM.MySql;

using IDataContext ctx = new MySqlDataContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DataContextBuilder());
```

## Конкатенация строк

Диалект преобразует сложение строк в C# в вызов `concat(...)`:

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

Сгенерированный SQL:

```sql
select concat('id:', id) as `Label`
 from simple_entity
```

## Разбиение на страницы

Используйте `Page`, чтобы задать число строк и смещение, или `Offset`, чтобы задать смещение без
ограничения числа строк:

```csharp
ctx.From<ISimpleEntity>().Page(5, 10).Select(x => x.Id);   // limit 5 offset 10
ctx.From<ISimpleEntity>().Offset(10).Select(x => x.Id);    // limit 18446744073709551615 offset 10
```

## Обзор

[`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext) (`src/nextorm.mysql/MySqlDataContext.cs`) оборачивает `MySqlConnector`. Он создаёт
`MySqlConnection` из строки подключения и возвращает [`Instance`](xref:NextORM.MySql.MySqlDialect.Instance) из свойства `Dialect`.
[`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) (`src/nextorm.mysql/MySqlDialect.cs`) — это диалект:

### Синтаксис SQL и функции

- маркер параметра `@name`;
- идентификаторы и псевдонимы квотируются обратными кавычками;
- конкатенация строк использует функцию `concat(a, b, ...)` — инфиксный `||` в MySQL является
  логическим ИЛИ, если не включён режим `PIPES_AS_CONCAT`, поэтому диалект его не выдаёт;
- [`MakeCoalesce`](xref:NextORM.Core.ISqlDialect.MakeCoalesce(System.String,System.String)) формирует `coalesce(a, b)`;
- [`MakeStringLength`](xref:NextORM.Core.ISqlDialect.MakeStringLength(System.String)) формирует `char_length(x)` (`length()` в MySQL считает байты);
- [`MakeNow`](xref:NextORM.Core.ISqlDialect.MakeNow(System.Boolean)) формирует `now()` для локального времени и `utc_timestamp()` для UTC;
- `stdev`/`stdevp`/`var`/`varp` отображаются в `stddev_samp`/`stddev_pop`/`var_samp`/`var_pop`
  (`stddev` и `variance` в MySQL — синонимы *популяционных* величин);
- преобразование CLR-типа формирует MySQL-цель `CAST` (`signed`/`unsigned` для целых,
  `double`, `decimal`, `char`, `datetime`), а не через ANSI `bigint`/`integer`;
- унарное дополнение формируется как `(-(x) - 1)`, потому что `~` в MySQL даёт беззнаковое
  64-битное значение, которое переполняет знаковый CLR-целый тип;
- символ `ESCAPE` в предикате `LIKE` записывается независимым от режимов токеном `_utf8mb4 X'5c'`,
  поэтому `NO_BACKSLASH_ESCAPES`, `ANSI_QUOTES` и настройка драйвера `NoBackslashEscapes` не могут
  изменить его смысл;
- разбиение на страницы — `limit n` / `limit n offset m`; offset без limit превращается в
  `limit 18446744073709551615 offset m`, потому что MySQL принимает `offset` только вместе с `limit`;
- функции сведений о сеансе ([`SessionInfoFunctions`](xref:NextORM.Core.ISqlDialect.SessionInfoFunctions)) формируют
  `SqlFunctions.Sql.current_user`/`session_user`/`current_database`/`version` как `current_user()`/`session_user()`/
  `database()`/`version()`, а `current_schema` — как `schema()`;
- агрегат произвольного значения [`SqlFunctions.Sql.any_agg`](xref:NextORM.Core.CommonFunctions.any_agg``1(``0)) формирует
  `ANY_VALUE(x)` ([`SupportsAnyValueAggregate`](xref:NextORM.Core.ISqlDialect.SupportsAnyValueAggregate), MySQL 5.7+);
- переносимый условный [`SqlFunctions.Sql.iif`](xref:NextORM.Core.CommonFunctions.iif``1(System.Boolean,``0,``0)) формирует
  `if(condition, a, b)` ([`Iif`](xref:NextORM.Core.ISqlDialect.Iif), [`IIifRenderer.Render`](xref:NextORM.Core.IIifRenderer.Render(System.String,System.String,System.String))).

### Семантика агрегатов

Агрегаты вариативности следуют общему контракту кардинальности: `Stdev`/`Var` дают `null` при нуле или
одном вносящем вклад значении, `Stdevp`/`Varp` — `null` при нуле и `0.0` при одном. Одно ограничение
провайдера не связано с nullable-результатом: MySQL 8.4 отклоняет `DISTINCT` внутри
`STDDEV_SAMP`/`STDDEV_POP`/`VAR_SAMP`/`VAR_POP` (`stddev_samp(distinct x)` завершается синтаксической
ошибкой). Диалект продолжает генерировать это написание для форм `*_distinct`, поэтому гарантирован только
SQL-текст, а живой запрос distinct-дисперсии на MySQL 8.4 не заявлен как поддерживаемый.

## Строковые литералы и параметры

Каждое встроенное текстовое значение записывается в независимой от режимов форме `_utf8mb4 X'…'` —
шестнадцатеричном UTF-8-литерале в верхнем регистре. Это относится и к операндам в позициях, где
грамматика принимает только литерал. Поэтому смысл значения не зависит от `sql_mode`, `ANSI_QUOTES`,
`NO_BACKSLASH_ESCAPES` или настройки драйвера `NoBackslashEscapes`.

**Место текстового параметра**, принадлежащее nextorm в запросе на чтение, сохраняет маркер `@name`
в кэшированной команде. Во время выполнения nextorm заменяет этот маркер тем же литералом
`_utf8mb4 X'…'` и не передаёт параметр драйверу. Перед следующим выполнением команда возвращается к
каноническому тексту и привязкам. Мутации (`INSERT`/`UPDATE`/`DELETE`) не подготавливаются: nextorm
сразу подставляет их текстовые значения при генерации SQL и не сохраняет шаблон.

В `GROUP_CONCAT ... SEPARATOR` грамматика принимает только текстовый литерал. **Константный**
разделитель записывается как литерал с удвоенными апострофами; допустимы символы из диапазона
`U+0020`–`U+007E`, кроме обратной косой черты. Неконстантные, `null`, содержащие обратную косую черту,
NUL или другой непечатаемый ASCII-символ разделители отклоняются с `NotSupportedException`, в котором
указан MySQL/MariaDB. При этом не создаётся маркер параметра или некорректный SQL, а значение не выводится.

```csharp
var query = ctx.From<IComplexEntity>().Where(e => e.String == "O'Brien").Select(e => e.Id);
```

```sql
-- слот текстового параметра понижается до литерала для этого выполнения
select `Id` as `Id`
 from complex_entity
 where `String` = _utf8mb4 X'4F27427269656E'
```

Константный `SEPARATOR` сохраняет литеральную форму:

```csharp
var names = ctx.From<IComplexEntity>().Select(e => SqlFunctions.Sql.string_agg(e.String, ","));
```

```sql
select group_concat(`String` separator ',')
 from complex_entity
```

## Массовая вставка

Встроенный путь использует `MySqlBulkCopy` и команду `LOAD DATA LOCAL INFILE`; для него нужны **оба** условия:
`AllowLoadLocalInfile=true` в строке подключения (клиент) и `local_infile=1` как серверная переменная
`@@GLOBAL.local_infile`. Серверное значение читается предварительным запросом только на чтение
(`select @@global.local_infile`), и nextorm не включает ни одну настройку. Без них `Auto` выбирает
переносимый путь только если он может выполнить весь запрос, а `Native` отклоняет его по имени; ошибка `LOAD` во время
выполнения никогда не переигрывается через SQL-путь.

- Источник читается ограниченными чанками (`MaxBatchSize`, по умолчанию 10 000 строк); каждый чанк — одна
  загрузка.
- `KeepIdentity` записывает заданный identity-столбец при включённом на время операции параметре
  `NO_AUTO_VALUE_ON_ZERO`, поэтому явный `0` не
  заменяется сгенерированным значением.
- `IgnoreDuplicates` отображается в `ConflictOption.Ignore` при встроенной загрузке и в `INSERT IGNORE` в
  переносимой вставке.
- `KeepNulls=false` допускается только для формы, эквивалентной значениям по умолчанию; `CheckConstraints=false`, `TableLock` и `FireTriggers`
  отклоняются (MySQL всегда проверяет ограничения), а `Returning*` отклоняется — ключи читайте через
  `CreateInsertBuilder`.
- `DatabaseName` задаёт квалифицированное имя `db.table`; `ServerName` отклоняется (перенаправления нет). `Timeout` — один бюджет
  на весь вызов; истёкший бюджет даёт `TimeoutException`, а отмена побеждает.

См. [Массовая вставка](../guide/22-bulk-insert.md).

## Различия провайдера

В таблице собраны особенности реализации MySQL:

| Аспект | MySQL |
|---|---|
| Маркер параметра | `@name` |
| Конкатенация | `concat(a, b)` |
| Подстановка при `NULL` | `coalesce` |
| Логический литерал | `1` / `0` (синонимы `true` / `false`) |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| Псевдоним TVF | требуется |
| `FULL JOIN` | не поддерживается (right join поддерживается) |
| `*ALL` | не поддерживается (в MySQL 8.0.31 есть `INTERSECT`/`EXCEPT`, но без вариантов `ALL`) |
| Текстовый JSON | `json_value`/`json_query`/`json_modify`/`isjson` (через `JSON_EXTRACT`/`JSON_UNQUOTE`/`JSON_SET`/`JSON_VALID`) |
| Функции сведений о сеансе | `current_user()`, `session_user()`, `schema()`, `database()`, `version()` |
| Произвольное значение | `ANY_VALUE(x)` |
| Условная функция | `iif(cond, a, b)` → `if(cond, a, b)` |
| Оконные квантили | не поддерживаются: `percentile_cont`/`percentile_disc` в оконной форме бросают `NotSupportedException` (в MySQL нет ни функций, ни `WITHIN GROUP`; `PERCENTILE_CONT` только в MariaDB) |
| Медиана (аналитическая) | не поддерживается: `SqlFunctions.MySql.median(v).Over(...)` бросает `NotSupportedException("The median function is not supported by MySQL.")` |
| Общий квантиль | не поддерживается: `SqlFunctions.MySql.percentile(fraction, v)` бросает `NotSupportedException("The percentile function is not supported by MySQL.")` |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`) | `NotSupportedException` |

MySQL 8.0.31 и новее поддерживает `INTERSECT`/`EXCEPT`; диалект отклоняет варианты `*ALL` с
`NotSupportedException`, соответствуя движку.

## См. также

- [Обзор провайдеров](overview.md)
- [Специфичный для MySQL и MariaDB SQL](../guide/provider-specific-sql/mysql.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.mysql/MySqlDialect.cs`, `src/nextorm.mysql/MySqlDataContext.cs`,
`src/nextorm.mysql/DI/MySqlDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mysql.tests/Dialect/MySqlDialectTests.cs`, `tests/nextorm.mysql.tests/SqlGeneration/SqlGenerationTests.cs`.
