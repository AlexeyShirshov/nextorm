# Провайдер ClickHouse

> Используйте `nextorm.clickhouse` для ClickHouse; он отрисовывает параметры `@name` (драйвер переписывает их в `{name:Type}`), идентификаторы в обратных кавычках, конкатенацию `concat(...)`, разбиение на страницы `limit`/`offset` и имена типов ClickHouse.

**Предварительные требования:** [Обзор провайдеров](overview.md) · [Quickstart](../getting-started/02-quickstart.md)

## Обзор

`ClickHouseDbContext` (`src/nextorm.clickhouse/ClickHouseDbContext.cs`) оборачивает официальный
ADO.NET-провайдер `ClickHouse.Driver`. Он создаёт `ClickHouseConnection` из строки подключения и
возвращает `ClickHouseDialect.Instance` из свойства `Dialect`.

`ClickHouseDialect` (`src/nextorm.clickhouse/ClickHouseDialect.cs`) отрисовывает:

- плейсхолдер параметра `@name`; драйвер переписывает их в нативный для ClickHouse вид
  `{name:Type}` и выводит тип из значения .NET;
- идентификаторы и псевдонимы в обратных кавычках;
- конкатенацию строк функцией `concat(a, b, ...)`;
- `coalesce(a, b)`, логические литералы `true`/`false` и `lengthUTF8(x)` для длины строки;
- `trimBoth`/`trimLeft`/`trimRight` для трёх видов trim;
- `now()` для локального времени и `now('UTC')` для UTC;
- `stdev`/`stdevp`/`var`/`varp` как `stddevSamp`/`stddevPop`/`varSamp`/`varPop`;
- `date_trunc(field, x)` как `dateTrunc('field', x)` (множественные ANSI-части субсекунд сворачиваются
  в единственные; `decade`/`century`/`millennium` бросают исключение), а `date_add`/`DateTime.Add*` —
  как выделенные функции `addYears`/`addQuarters`/…/`addSeconds` (`decade`/`century`/`millennium`
  сворачиваются в масштабированный `addYears`), `end_of_month` — как `toLastDayOfMonth(x)`;
- `string_agg(x, delimiter)` как `arrayStringConcat(groupArray(x), delimiter)`;
- `bit_and`/`bit_or`/`bit_xor` как `groupBitAnd`/`groupBitOr`/`groupBitXor`, `covar_pop`/`covar_samp`
  как `covarPop`/`covarSamp`, `corr` как `corr`, `arg_min`/`arg_max` как `argMin`/`argMax`, а
  фильтрованные агрегаты `count_if`/`sum_if`/`avg_if`/`min_if`/`max_if` — как комбинаторы `-If`
  `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf`;
- имена типов ClickHouse в приведениях (`Int32`, `Int64`, `Float64`, `Decimal(38, 10)`, …);
- разбиение на страницы `limit n` / `limit n offset m`; offset без limit превращается в
  `limit 18446744073709551615 offset m`, потому что ClickHouse принимает `offset` только вместе с `limit`.

ClickHouse не поддерживает рекурсивные CTE, поэтому диалект объявляет каждый CTE просто через `with`.

## Регистрация провайдера

На `DbContextBuilder` доступны две перегрузки
(`src/nextorm.clickhouse/DI/DataContextOptionsBuilderExtensions.cs`):

```csharp
using nextorm.core;
using nextorm.clickhouse;

var builder = new DbContextBuilder()
    .UseClickHouse("Host=localhost;Port=8123;Username=default;Password=secret;Database=app");

using var ctx = builder.CreateDbContext();   // IDataContext
```

Также можно создать контекст напрямую:

```csharp
using nextorm.core;
using nextorm.clickhouse;

using IDataContext ctx = new ClickHouseDbContext(
    "Host=localhost;Username=default;Database=app", new DbContextBuilder());
```

## Конкатенация строк

```csharp
var query = ctx.From<ISimpleEntity>().Select(x => new { Label = "id:" + x.Id });
```

```sql
select concat('id:', id) as `Label` from simple_entity
```

## Различия провайдера

| Аспект | ClickHouse |
|---|---|
| Плейсхолдер параметра | `@name` (драйвер переписывает в `{name:Type}`) |
| Конкатенация | `concat(a, b)` |
| Coalesce | `coalesce` |
| Логический литерал | `true` / `false` |
| Квотирование идентификаторов | обратные кавычки (`` as `t1` ``) |
| Псевдоним производной таблицы | требуется |
| Псевдоним TVF | требуется |
| `*ALL` | поддерживается |
| Рекурсивный CTE | не поддерживается (модификатор `recursive` опускается) |
| `date_trunc` | `dateTrunc('field', x)` |
| Арифметика дат | `addDays(x, n)` … `addYears(x, (n) * 10)`; `toLastDayOfMonth(x)` |
| `string_agg` | `arrayStringConcat(groupArray(x), delimiter)` (без `array_agg`) |
| Битовые / статистические агрегаты | `groupBitAnd`/`groupBitOr`/`groupBitXor`; `corr`/`covarPop`/`covarSamp` |
| Агрегаты регрессии | не поддерживаются (`regr_*` — только PostgreSQL) |
| Логические агрегаты | не поддерживаются (`bool_and`/`bool_or`/`every` — только PostgreSQL) |
| Фильтрованный агрегат | `countIf`/`sumIf`/`avgIf`/`minIf`/`maxIf` (без ANSI `filter (where ...)`) |
| ArgMin / ArgMax | `argMin`/`argMax` |
| Массивы / JSON / расширенные скаляры | не поддерживаются (только PostgreSQL) |

## Замечания и ограничения

- Параметры ClickHouse передаются как параметры HTTP-запроса. Для массовой вставки следует
  использовать API бинарной вставки драйвера, а не параметризованные `INSERT`, что выходит за
  пределы построителя запросов.
- Для `null`-значений параметров тип ClickHouse невозможно вывести только из значения CLR. Когда
  запрос связывает параметр `null`, задайте явный тип параметра на уровне драйвера (например,
  пользовательским резолвером) или приведите плейсхолдер в SQL.

## См. также

- [Обзор провайдеров](overview.md)
- [MySQL](mysql.md)
- [MariaDB](mariadb.md)
- [In-memory](in-memory.md)
- [Ограничения и что вне области](../advanced/limitations.md)

---

Source: `src/nextorm.clickhouse/ClickHouseDialect.cs`, `src/nextorm.clickhouse/ClickHouseDbContext.cs`,
`src/nextorm.clickhouse/DI/DataContextOptionsBuilderExtensions.cs`,
`test/nextorm.clickhouse.tests/ClickHouseDialectTests.cs`, `test/nextorm.clickhouse.tests/SqlGenerationTests.cs`.
