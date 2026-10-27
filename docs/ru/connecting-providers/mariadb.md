# Провайдер MariaDB

> Используйте `nextorm.mariadb` для MariaDB 10.4+; он переиспользует отрисовку MySQL и добавляет варианты операций над множествами `INTERSECT ALL`/`EXCEPT ALL`.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [MySQL](mysql.md)

## Обзор

[`MariaDbDataContext`](xref:NextORM.MariaDb.MariaDbDataContext) (`src/nextorm.mariadb/MariaDbDataContext.cs`) наследуется от [`MySqlDataContext`](xref:NextORM.MySql.MySqlDataContext), поэтому
использует тот же драйвер `MySqlConnector` и то же управление соединением и параметрами. Он
возвращает [`Instance`](xref:NextORM.MariaDb.MariaDbDialect.Instance) из свойства `Dialect`.

[`MariaDbDialect`](xref:NextORM.MariaDb.MariaDbDialect) (`src/nextorm.mariadb/MariaDbDialect.cs`) наследуется от [`MySqlDialect`](xref:NextORM.MySql.MySqlDialect) и меняет
набор возможностей: `INTERSECT ALL` и `EXCEPT ALL` поддерживаются в MariaDB 10.4 и новее, поэтому
[`SupportsIntersectExceptAll`](xref:NextORM.Core.ISqlDialect.SupportsIntersectExceptAll) равно `true`, а MariaDB 10.3+ рендерит оконные
квантили `percentile_cont`/`percentile_disc` (`... within group (order by ...) over (...)`), поэтому
[`SupportsPercentileWindow`](xref:NextORM.Core.ISqlDialect.SupportsPercentileWindow) тоже равно `true`; та же аналитическая форма лежит в
основе `SqlFunctions.MySql.median(value).Over(...)` (`percentile_cont(0.5) within group (order by value) over (...)`,
одна строка на входную строку, окно только с `partition by`), а у общего `SqlFunctions.MySql.percentile`
нет написания в MariaDB — вызов бросает `NotSupportedException`; `UPDATE ... RETURNING` включается только при явно
настроенной версии сервера 13.0+ ([`SupportsUpdateReturning`](xref:NextORM.Core.ISqlDialect.SupportsUpdateReturning)). Агрегат произвольного значения
`any_agg` при этом **отключён**, потому что в MariaDB нет `ANY_VALUE` в 10.4–12.x (возможность SQL-2023
`T626` всё ещё ожидается, ориентир — 13.2). Всё остальное (параметры, квотирование, `concat`, coalesce,
разбиение на страницы, имена агрегатов, написание `if(...)` для переносимого `iif`) наследуется без
изменений.

## Регистрация провайдера

На [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) доступны две перегрузки
(`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`):

```csharp
using NextORM.Core;
using NextORM.MariaDb;

var builder = new DataContextBuilder()
    .UseMariaDb("Server=localhost;Port=3306;Database=app;User ID=app;Password=secret");

using var ctx = builder.CreateDataContext();   // IDataContext
```

Также можно создать контекст напрямую:

```csharp
using NextORM.Core;
using NextORM.MariaDb;

using IDataContext ctx = new MariaDbDataContext(
    "Server=localhost;Database=app;User ID=app;Password=secret", new DataContextBuilder());
```

## Версия сервера

Диалект можно привязать к версии сервера, которая гейтит `UPDATE ... RETURNING` и терминалы bulk
`Returning*`. Настройка **явная** — nextorm никогда не опрашивает живой сервер. Передайте версию через
параметр `Version` конструктора контекста. В отличие от PostgreSQL, **незаданная** версия **не** включает
возможность: без явной 13.0+ терминал `Returning()` у `UPDATE` и вызов bulk `Returning*` бросают
`NotSupportedException`. `RETURNING` у одно-row insert и delete не меняется (MariaDB отклоняет их как и
раньше), как и гейт `ANY_VALUE` (`any_agg`, ориентир — 13.2). Bulk `Returning*` поддерживается начиная с
13.0+, а ниже отклоняется; живая сертификация идёт на `mariadb:11.4`, то есть покрывает ветку отказа.

```csharp
// Явная 13.0+: UPDATE ... RETURNING включён.
using var ctx = new MariaDbDataContext(
    "Server=localhost;Database=app;...", new DataContextBuilder(), new Version(13, 0));
```

Та же явная версия гейтит и нативные функции, появившиеся в MariaDB после 11.4. Каждая рендерится только при
версии не ниже своего минимума; **контекст без версии** (конструктор с двумя аргументами) отклоняет её с
`NotSupportedException` при трансляции/подготовке, а не эмитит SQL, который сервер отвергнет:

| Член | Нативная форма | Минимум |
|---|---|---|
| `SqlFunctions.MySql.months_between(a, b)` | `months_between(...)` | 12.2.1 |
| `SqlFunctions.MySql.to_date(value, format)` | `to_date(...)` | 12.3 |
| `SqlFunctions.MySql.to_number(value, format)` | `to_number(...)` | семейство 12.2, принудительно 12.2.1 |
| `SqlFunctions.Sql.gen_random_uuid()` | `uuid_v4()` | 11.7 |
| `SqlFunctions.Sql.uuidv7()` | `uuid_v7()` | 11.7 |

Это минимумы **функций**, а не поднятая нижняя граница провайдера: пакет по-прежнему поддерживает MariaDB
10.4+, а `add_months`/`to_char`/остальной каталог сохраняют собственную доступность. Имена базовой версии
MariaDB 11.4 (`add_months`, `to_char`, `nvl`, семейство JSON, последовательности) от версии не зависят.

В отличие от PostgreSQL, MariaDB **не** накладывает защиту «одна версия на конкретный тип контекста»:
каждый контекст несёт собственный экземпляр диалекта и предикат возможностей, поэтому один и тот же
конкретный тип контекста можно создавать с разными версиями. Строго типизированный подкласс всё ещё удобен,
чтобы зафиксировать одну версию:

```csharp
public sealed class MariaDb130DataContext : MariaDbDataContext
{
    public MariaDb130DataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder, new Version(13, 0)) { }
}
```

## Операции над множествами

```csharp
ctx.From<ISimpleEntity>().Select(x => x.Id).IntersectAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
ctx.From<ISimpleEntity>().Select(x => x.Id).ExceptAll(ctx.From<ISimpleEntity>().Select(x => x.Id));
```

```sql
select id
 from simple_entity
 intersect all
select id
 from simple_entity
```

## Массовая вставка

MariaDB использует общую реализацию MySQL, поэтому нативный путь — `MySqlBulkCopy` (загрузка через
`LOAD DATA LOCAL INFILE`), и ему нужны `AllowLoadLocalInfile=true` в строке подключения и серверная
переменная `@@GLOBAL.local_infile=1`, читаемая read-only preflight-запросом. nextorm не включает ни одно из
них; без них `Auto` откатывается, только если портируемый путь выполняет весь запрос, а `Native` отклоняет по
имени.

- Identity и дубликаты — как в MySQL: `KeepIdentity` пишет явную identity-колонку под scoped-guard
  `NO_AUTO_VALUE_ON_ZERO`, а `IgnoreDuplicates` — это `ConflictOption.Ignore` на нативной загрузке или
  `INSERT IGNORE` в портируемом утверждении.
- `CheckConstraints=false` выполняется через scoped-контроль сессии, который снимается и восстанавливается на
  той же сессии даже при ошибке загрузки; `TableLock` и `FireTriggers` остаются отклонёнными.
- `KeepNulls=false` требует default-equivalent формы.
- **`Returning*` на bulk-пути гейтится версией.** MariaDB выполняет его при явно настроенной 13.0+, а ниже
  (незаданная или более старая версия) отклоняет именованным `NotSupportedException`; `RETURNING` у
  одно-row insert и delete отклоняется на любой версии, поэтому ключи там читаются через
  `CreateInsertBuilder`. Живая сертификация идёт на `mariadb:11.4`, то есть покрывает ветку отказа; ветка
  13.0+ сертифицирована офлайн только по рендерингу SQL `INSERT ... RETURNING`.
- `DatabaseName` рендерит `db.table`; `ServerName` отклоняется (перенаправления нет). `Timeout` — один бюджет
  на весь вызов; истёкший бюджет даёт `TimeoutException`, а отмена побеждает.

См. [Массовая вставка](../guide/22-bulk-insert.md).

## Различия провайдера

MariaDB отличается от [MySQL](mysql.md) возможностью операций над множествами, оконными квантилями, версионным гейтом `UPDATE ... RETURNING` и агрегатом произвольного значения:

| Аспект | MariaDB |
|---|---|
| Плейсхолдер параметра | `@name` |
| Конкатенация | `concat(a, b)` |
| Coalesce | `coalesce` |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| `*ALL` | поддерживается (MariaDB 10.4+) |
| `UPDATE ... RETURNING` | включается только при явной версии сервера 13.0+ (`SupportsUpdateReturning`); иначе `NotSupportedException` |
| Версия сервера | настраивается явно через `Version` у контекста |
| Текстовый JSON | наследуется от MySQL (`JSON_EXTRACT`/`JSON_SET`) |
| Session/info-функции | наследуются от MySQL (`current_user()`, `session_user()`, `schema()`, `database()`, `version()`) |
| Произвольное значение | не поддерживается (в 10.4–12.x нет `ANY_VALUE`; ожидается MDEV-10426, ориентир — 13.2) |
| Условная функция | наследуется от MySQL (`iif(cond, a, b)` → `if(cond, a, b)`) |
| Оконные квантили | `percentile_cont`/`percentile_disc` как `... within group (order by x) over (...)` (MariaDB 10.3+); аналитическая: одна строка на входную строку |
| Медиана (аналитическая) | `SqlFunctions.MySql.median(v).Over(...)` → `percentile_cont(0.5) within group (order by v) over (...)`: одна строка на входную строку; окно только с `partition by` (оконный `ORDER BY`/рамка отклоняются) |
| Общий квантиль | не поддерживается: `SqlFunctions.MySql.percentile(fraction, v)` бросает `NotSupportedException("The percentile function is not supported by MariaDB.")` |
| Дробь квантиля | `double` в `[0, 1]` (не проценты), проверяется до рендеринга; значение вне диапазона или не конечное число бросает `ArgumentOutOfRangeException` |
| Нативные функции | поверхность `SqlFunctions.MySql`, унаследованная от MySQL, **минус** `uuid_to_bin`/`bin_to_uuid`, **плюс** имена MariaDB (расширенный regexp, `nvl`/`nvl2`, `add_months`/`months_between`, `to_char`/`to_date`/`to_number`, `kdf`, `xxh3`/`xxh32`, `json_detailed`/`json_compact`, доступ к последовательностям) |
| Потоковое чтение LOB (`ToStream`/`ToTextReader`) | `NotSupportedException` |

## См. также

- [Обзор провайдеров](overview.md)
- [Специфичный для MySQL и MariaDB SQL](../guide/provider-specific-sql/mysql.md)
- [MySQL](mysql.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.mariadb/MariaDbDialect.cs`, `src/nextorm.mariadb/MariaDbDataContext.cs`,
`src/nextorm.mariadb/DI/MariaDbDataContextOptionsBuilderExtensions.cs`,
`tests/nextorm.mariadb.tests/Dialect/MariaDbDialectTests.cs`, `tests/nextorm.mariadb.tests/SqlGeneration/SqlGenerationTests.cs`.
