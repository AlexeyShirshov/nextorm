# Обзор провайдеров

> Провайдер предоставляет диалект (правила SQL-текста) и подкласс [`DataContext`](xref:NextORM.Core.DataContext) (создание соединения + параметров); выберите тот, который соответствует базе данных, которую вы уже используете.

**Предварительные требования:** [Quickstart](../getting-started/02-quickstart.md) · [Dependency injection](../getting-started/04-dependency-injection.md)

## Обзор

nextorm состоит из нейтрального к провайдеру ядра (`nextorm`, пространство имён `nextorm.core`) и одного пакета на
базу данных: `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.mariadb`
и `nextorm.clickhouse`. Провайдер in-memory встроен
в пакет ядра. Провайдер вносит две вещи:

1. **диалект** — объект без состояния, который отрисовывает всё, что различается между базами данных (плейсхолдеры
   параметров, разбиение на страницы, квотирование, имена функций, флаги возможностей); и
2. **контекст** — подкласс [`DataContext`](xref:NextORM.Core.DataContext), который знает, как создавать соединение и параметры, и
   предоставляет диалект через своё свойство `Dialect`.

Генерация SQL полностью управляется [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`),
поэтому построитель SQL и посетители выражений никогда не содержат имён провайдеров. Семантика запросов (проекция,
фильтрация, соединения, группировка, операции над множествами, CTE, оконные функции, поддержка scalar/UDF/TVF) является общей;
различается только отрисовка.

## Выбор провайдера

| Ситуация | Провайдер |
|---|---|
| Локальная разработка, тесты, встраиваемая база данных, небольшие приложения | `nextorm.sqlite` |
| Вы уже используете Microsoft SQL Server / Azure SQL | `nextorm.sqlserver` |
| Вы уже используете PostgreSQL | `nextorm.postgres` |
| Вы уже используете MySQL | `nextorm.mysql` |
| Вы уже используете MariaDB | `nextorm.mariadb` |
| Вы уже используете ClickHouse | `nextorm.clickhouse` |
| Модульные тесты, которые не должны обращаться к базе данных, тесты кэша планов, тесты формы запросов | [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) (ядро) |

Используйте один и тот же код запросов со всеми провайдерами; перечисленные ниже различия провайдеров — единственное, что
меняется.

## Матрица поддержки

| Возможность | SQLite | SQL Server | PostgreSQL | MySQL | MariaDB | ClickHouse | In-memory |
|---|---|---|---|---|---|---|---|
| Пакет | `nextorm.sqlite` | `nextorm.sqlserver` | `nextorm.postgres` | `nextorm.mysql` | `nextorm.mariadb` | `nextorm.clickhouse` | встроен в `nextorm` |
| Плейсхолдер параметра | `$name` | `@name` | `@name` | `@name` | `@name` | `@name` | не применимо |
| `INSERT ... VALUES` | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается (малые батчи) | бросает `NotSupportedException` |
| Key upsert (`MergeInto`) | `ON CONFLICT ... DO UPDATE` | `MERGE ... USING (VALUES ...)` | `ON CONFLICT ... DO UPDATE` | `ON DUPLICATE KEY UPDATE` | `ON DUPLICATE KEY UPDATE` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `DELETE` (`DeleteFrom`/`Delete`) | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | `ALTER TABLE ... DELETE ... SETTINGS mutations_sync = 1` | бросает `NotSupportedException` |
| `UPDATE` (`Update`/`Update(entity)`) | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | `ALTER TABLE ... UPDATE ... SETTINGS mutations_sync = 1` | бросает `NotSupportedException` |
| `UPDATE ... RETURNING` (`Returning`) | `RETURNING` | `OUTPUT inserted.<col>` | `RETURNING` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `UPDATE ... FROM` (`UpdateJoin`) | `UPDATE ... FROM` | `UPDATE <alias> ... FROM ... JOIN` | `UPDATE ... FROM` | `UPDATE ... JOIN ... SET` | `UPDATE ... JOIN ... SET` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `DELETE ... RETURNING` (`Returning`) | `RETURNING` | `OUTPUT deleted.<col>` | `RETURNING` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| `TRUNCATE` (`Truncate`) | бросает `NotSupportedException` | поддерживается | поддерживается | поддерживается | поддерживается | поддерживается | бросает `NotSupportedException` |
| `DELETE ... USING`/join (`From<T>().Join(...).Delete()`, INNER) | бросает `NotSupportedException` | `DELETE <a> FROM ... JOIN ...` | `DELETE FROM ... USING ...` | `DELETE <a> FROM ... JOIN ...` | `DELETE <a> FROM ... JOIN ...` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Сгенерированный ключ (`ReturningIdentity`/`ReturningKey`) | `RETURNING` | `OUTPUT inserted.<col>` | `RETURNING` | фолбэк `LAST_INSERT_ID()` | фолбэк `LAST_INSERT_ID()` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Identity-функция (`ReturningIdentity<TKey>()`) | `last_insert_rowid()` | `SCOPE_IDENTITY()` | `lastval()` | `LAST_INSERT_ID()` | `LAST_INSERT_ID()` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Возврат вставленных строк (`Returning`) | `RETURNING` | `OUTPUT inserted.<cols>` | `RETURNING` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` | бросает `NotSupportedException` |
| Только limit | `limit n` | `top(n)` | `limit n` | `limit n` | `limit n` | `limit n` | take в процессе |
| Limit + offset | `limit n offset m` | `offset m rows fetch next n rows only` | `limit n offset m` | `limit n offset m` | `limit n offset m` | `limit n offset m` | skip/take в процессе |
| Только offset | `limit -1 offset m` | `offset m rows` | `offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | `limit 18446744073709551615 offset m` | skip в процессе |
| Разбиение на страницы без `ORDER BY` | допускается | внедряет `order by (select null as anyorder)` | допускается | допускается | допускается | допускается | допускается |
| `INTERSECT ALL` / `EXCEPT ALL` (`*ALL`) | бросает `NotSupportedException` | бросает `NotSupportedException` | поддерживается | бросает `NotSupportedException` | поддерживается | поддерживается | не применимо |
| Ключевое слово рекурсивного CTE | `with recursive` | `with` (плюс `option (maxrecursion n)`) | `with recursive` | `with recursive` | `with recursive` | `with` (рекурсивные CTE не поддерживаются) | не применимо |
| Именованные окна / `GROUPS` / `EXCLUDE` | именованное окно + `GROUPS` + `EXCLUDE` | бросает (нет `WINDOW`-клаузы) | именованное окно + `GROUPS` + `EXCLUDE` | только именованное окно | только именованное окно | именованное окно + `GROUPS` | не применимо |
| Конкатенация строк | `\|\|` | `+` | `\|\|` | `concat(a, b)` | `concat(a, b)` | `concat(a, b)` | не применимо |
| Логический литерал | `1` / `0` | `1` / `0` (через `bit`) | `true` / `false` | `1` / `0` (`true` / `false`) | `1` / `0` (`true` / `false`) | `true` / `false` | не применимо |
| `??` (coalesce) | `ifnull(a, b)` | `isnull(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | `coalesce(a, b)` | не применимо |
| `stdev` / `stdevp` | `stdev` / `stdevp` (пользовательские) | `stdev` / `stdevp` (нативные) | `stddev` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddev_samp` / `stddev_pop` | `stddevSamp` / `stddevPop` | не применимо |
| `var` / `varp` | `var` / `varp` (пользовательские) | `var` / `varp` (нативные) | `variance` / `var_pop` | `var_samp` / `var_pop` | `var_samp` / `var_pop` | `varSamp` / `varPop` | не применимо |
| `date_trunc` | бросает | `datetrunc(...)` (2022+) | поддерживается | бросает | бросает | `dateTrunc(...)` | бросает |
| Арифметика дат (`date_add`, `date_diff`, `end_of_month`, `date_from_parts`, `DateTime.Add*`) | `datetime(x, n \|\| ' days')` / `date(...)` / разность `strftime` | поддерживается | поддерживается | поддерживается | поддерживается | `addDays(...)` … / `toLastDayOfMonth(...)` | бросает |
| Приведение / части даты (`SqlFunctions.ClickHouse.to_*`) | бросает | бросает | бросает | бросает | бросает | `toDate`/`toDateTime`/`toDate32`, `toYear`/…, `toStartOf*`, `toMonday`, `toYYYYMM`/`toYYYYMMDD`, `toUnixTimestamp` | бросает |
| `string_agg` | `group_concat(x, delimiter)` | поддерживается (2017+) | поддерживается | `group_concat(x separator delimiter)` | `group_concat(x separator delimiter)` | `arrayStringConcat(groupArray(...), ...)` | бросает |
| Полнотекст `contains` / `freetext` | бросает | `contains` / `freetext` | `to_tsvector(...) @@ ...tsquery(...)` | `match(...) against(...)` | `match(...) against(...)` | бросает | бросает |
| Полнотекстовое ранжирование / score | бросает | `containstable` / `freetexttable` (`RANK`, табличная функция) | `ts_rank` / `ts_rank_cd` | бросает | бросает | бросает | бросает |
| Битовые / статистические / `-If` агрегаты | бросает | бросает | поддерживается | бросает | бросает | `groupBit*`, `corr`/`covarPop`, `countIf`/… | бросает |
| `multi_if` (многоветвевный) | бросает | бросает | бросает | бросает | бросает | `multiIf(c1, v1, …, else)` | не применимо |
| `lag_in_frame` / `lead_in_frame` | бросает | бросает | бросает | бросает | бросает | `lagInFrame` / `leadInFrame` | не применимо |
| Квотирование идентификаторов / псевдонимов | одинарные кавычки: `as 't1'` | квадратные скобки: `as [t1]` | двойные кавычки: `as "t1"` | обратные кавычки: `` as `t1` `` | обратные кавычки: `` as `t1` `` | обратные кавычки: `` as `t1` `` | не применимо |
| Псевдоним производной таблицы (подзапрос в `FROM`) | не требуется | требуется | требуется | требуется | требуется | требуется | не применимо |
| Псевдоним табличной функции | не требуется | требуется | требуется | требуется | требуется | требуется | источник TVF не поддерживается |
| Соединение `LEFT` / `RIGHT` / `FULL` / `CROSS` | да | да | да | без `FULL` | без `FULL` | да | да |
| Возможность соединения `RIGHT` / `FULL` | поддерживается | поддерживается | поддерживается | только `RIGHT` | только `RIGHT` | поддерживается | поддерживается |

## Различия провайдеров: решения по унификации

Там, где провайдеры различаются, nextorm либо **унифицирует** поверхность в коде, либо **гейтит**
возможность (неподдерживающий провайдер бросает `NotSupportedException`), либо **документирует**
различие и оставляет его провайдерным. Решение по каждому известному расхождению:

| Различие | Решение | Обоснование / хук |
|---|---|---|
| Принимаемые поля `date_add`/`date_diff`/`date_trunc` | **Оставить провайдерным, гейт по полю** | `SupportsDateAddField`/`SupportsDateDiffField`/`SupportsDateTruncField` отклоняют неподдерживаемое поле; единый нормализованный набор молча менял бы результат (SQLite сворачивает `millisecond`/`quarter`, в SQL Server нет `decade`/`century`/`millennium`). |
| `FULL JOIN` в MySQL/MariaDB | **Гейт без полифилла** | `SupportsFullJoin => false`; переписывание в `LEFT JOIN … UNION … RIGHT JOIN` меняет форму строк/дедупликацию и может испортить план, поэтому неявно не эмитится. |
| `CUBE`/`GROUPING SETS` в MySQL/MariaDB (и весь `ROLLUP`/`CUBE`/`GROUPING SETS` в in-memory) | **Гейт без полифилла** | `SupportsCube`/`SupportsGroupingSets`; эмуляция через `UNION ALL` множит сканы и меняет семантику (`GROUPING()`), поэтому оставлена сырому SQL. |
| NULL-семантика `GREATEST`/`LEAST` | **Документированное различие** | PostgreSQL/SQL Server 2022+/ClickHouse игнорируют NULL-аргументы; MySQL/MariaDB и SQLite возвращают NULL, если любой аргумент NULL. Доступность гейтится `SupportsGreatestLeast`; поведение с NULL не переписывается. |
| Шаблоны форматирования дат/чисел (`to_char`, `FORMAT`, `strftime`, `formatDateTime`) | **Закрыто — не унифицируемо** | Языки шаблонов несовместимы, поэтому форматирование остаётся провайдерными UDF `[SqlFunction]`; единого портируемого аргумента `template` нет. |
| `FOR JSON` / `FOR XML` | **Гейт (SQL Server)** | `SupportsForJson`/`SupportsForXml`. |
| Хинты уровня инструкции | **Унифицировано** | SQL Server `OPTION (...)`, PostgreSQL/MySQL/MariaDB встроенный `/*+ ... */`; SQLite/ClickHouse без синтаксиса и остаются под гейтом (см. [Хинты запросов](../guide/17-query-hints.md)). |
| Блокирующие табличные хинты vs index hints | **Оставить провайдерным** | У `WITH (NOLOCK)` нет аналога среди index hints MySQL/MariaDB/SQLite (`USE INDEX`/`INDEXED BY` меняют план, а не блокировки), поэтому подключён только SQL Server (`SupportsTableHints`). |
| Сырой SQL как композируемый источник `FROM` | **Унифицировано** | `FromSql` + `SupportsRawSqlSource` у всех SQL-провайдеров (см. [Сырой SQL](../guide/14-raw-sql.md)). |
| `INTERSECT ALL`/`EXCEPT ALL` | **Гейт** | PostgreSQL и MariaDB поддерживают; SQL Server/SQLite/MySQL отклоняют через `SupportsIntersectExceptAll`. |

Строки таблицы [ограничений](../advanced/limitations.md) описывают итоговое поведение в рантайме.

## Как подключается диалект

Диалект реализует [`ISqlDialect`](xref:NextORM.Core.ISqlDialect) или наследуется от [`SqlDialectBase`](xref:NextORM.Core.SqlDialectBase). В [`SqlDialectBase`](xref:NextORM.Core.SqlDialectBase) абстрактными являются только
[`MakeParam`](xref:NextORM.Core.ISqlDialect.MakeParam(System.String)) и [`MakePage`](xref:NextORM.Core.ISqlDialect.MakePage(NextORM.Core.Paging,System.Text.StringBuilder,NextORM.Core.KeywordCase)); у всех остальных членов есть рабочее значение по умолчанию ANSI, поэтому диалект
переопределяет только то, что отличается. Различия возможностей (разбиение на страницы, требующее `ORDER BY`, обязательные
псевдонимы подзапросов, `INTERSECT ALL`/`EXCEPT ALL`) выражаются свойствами, а не особыми случаями в
построителе SQL.

```csharp
// The built-in dialects are singletons exposed as a static Instance.
ISqlDialect sqlite = SqliteDialect.Instance;
ISqlDialect sqlServer = SqlServerDialect.Instance;
ISqlDialect postgres = PostgresDialect.Instance;
ISqlDialect mysql = MySqlDialect.Instance;
ISqlDialect mariaDb = MariaDbDialect.Instance;
ISqlDialect clickHouse = ClickHouseDialect.Instance;
```

Контекст провайдера возвращает свой диалект из переопределённого свойства:

```csharp
public class SqliteDataContext : DataContext
{
    public override ISqlDialect Dialect => SqliteDialect.Instance;
    // CreateDbConnection / CreateParam are provider specific.
}
```

Контракт диалекта намеренно исключает создание соединения/параметров (`CreateConnection`/
`CreateParam`) и отображение столбцов (`MapColumnExpression`): они находятся на контексте, потому что это
отдельная ось от SQL-текста.

## Регистрация провайдера

Каждый пакет провайдера добавляет методы расширения `UseXxx` на [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder), и у каждого контекста также есть
публичный конструктор, принимающий строку подключения или существующий `DbConnection`:

```csharp
using NextORM.Core;
using NextORM.Sqlite;      // or nextorm.sqlserver / nextorm.postgres

var builder = new DataContextBuilder().UseSqlite("app.db");   // provider-specific overload
using var ctx = builder.CreateDataContext();                   // returns IDataContext
```

С внедрением зависимостей:

```csharp
services.AddNextOrmContext(builder => builder.UseSqlite("app.db"));
```

См. [Dependency injection](../getting-started/04-dependency-injection.md) для регистраций с ключом и универсальных
регистраций.

## См. также

- [SQLite](sqlite.md)
- [SQL Server](sqlserver.md)
- [PostgreSQL](postgres.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [ClickHouse](clickhouse.md)
- [In-memory](in-memory.md)
- [Limitations and out-of-scope features](../advanced/limitations.md)

---

Source: `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs`,
`tests/nextorm.sqlite.tests/SqliteDialectTests.cs`, `tests/nextorm.sqlserver.tests/SqlServerDialectTests.cs`,
`tests/nextorm.postgres.tests/PostgresDialectTests.cs`, `tests/nextorm.mysql.tests/MySqlDialectTests.cs`,
`tests/nextorm.mariadb.tests/MariaDbDialectTests.cs`, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`.
