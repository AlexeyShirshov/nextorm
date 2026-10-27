# Математические функции

| C# | SQL | Примечания |
|---|---|---|
| `Math.Abs(x)` | `abs(x)` | |
| `Math.Round(x)` | `round(x)` / `round(x, 0)` | SQL Server требует аргумент длины. |
| `Math.Round(x, digits)` | `round(x, digits)` | PostgreSQL приводит первый аргумент `double`/`float` к `numeric` (`round((x)::numeric, digits)`), так как в нём нет `round(double precision, integer)`. |
| `Math.Truncate(x)` | `trunc(x)` / `round(x, 0, 1)` | В SQL Server нет `trunc`. |
| `Math.Log(x)` | натуральный логарифм: `ln(x)` (SQLite, PostgreSQL) / `log(x)` (SQL Server) | Только одноаргументная форма. |
| `Math.Log10(x)` | `log10(x)` | |
| `Math.Acos(x)` / `Math.Asin(x)` / `Math.Atan(x)` | `acos(x)` / `asin(x)` / `atan(x)` | |
| `Math.Atan2(y, x)` | `atan2(y, x)` (SQL Server: `atn2(y, x)`) | |

```csharp
var values = dataContext.From<IComplexEntity>()
    .Select(e => Math.Abs(e.Id - 5))
    .ToList();
// ids 1, 2, 3 -> 4, 3, 2
```

```sql
select abs((id - 5))
 from complex_entity
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Abs |
|-----|
| 4   |
| 3   |
| 2   |

## Переносимые тригонометрические и угловые функции

Помимо трансляции `Math.*` выше, [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) предоставляет
угловые функции, у которых нет BCL-аналога. Они рендерятся нативно там, где провайдер умеет (гейтинг
по имени через [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String))):

| C# | SQL | Примечания |
|---|---|---|
| `SqlFunctions.Sql.cot(x)` | `cot(x)` | PostgreSQL, SQL Server и MySQL/MariaDB; в ClickHouse и SQLite нет `cot`, вызов отклоняется. |
| `SqlFunctions.Sql.degrees(x)` | `degrees(x)` | Все провайдеры (SQL Server приводит аргумент к `float`). |
| `SqlFunctions.Sql.radians(x)` | `radians(x)` | Все провайдеры (SQL Server приводит аргумент к `float`). |
| `SqlFunctions.Sql.pi()` | `pi()` | Все провайдеры. |

`Math.Acos`/`Asin`/`Atan`/`Atan2` отдельной обёртки не требуют — они транслируются напрямую, а SQL Server
для двухаргументной формы использует своё написание `atn2`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Angle = SqlFunctions.Sql.degrees(SqlFunctions.Sql.pi()),
        Cot = SqlFunctions.Sql.cot(1.0)
    })
    .ToList();
```

## Математика только для ClickHouse

Помимо переносимой поверхности [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql),
[`SqlFunctions.ClickHouse`](xref:NextORM.Core.SqlFunctions.ClickHouse) предоставляет четыре
математические функции, существующие только в ClickHouse. Они **не** входят в кросс-провайдерную
трансляцию `Math.*`/BCL и не добавлялись в [`CommonFunctions`](xref:NextORM.Core.CommonFunctions);
остальные провайдеры (PostgreSQL, SQL Server, MySQL, MariaDB, SQLite) и in-memory провайдер отклоняют
вызов `NotSupportedException`, а не эмулируют его.

| C# | SQL | Результат |
|---|---|---|
| `SqlFunctions.ClickHouse.log2(x)` / `log2(x)` | `log2(x)` | Логарифм по основанию 2. |
| `SqlFunctions.ClickHouse.cbrt(x)` / `cbrt(x)` | `cbrt(x)` | Кубический корень. |
| `SqlFunctions.ClickHouse.exp2(x)` / `exp2(x)` | `exp2(x)` | Два в степени `x`. |
| `SqlFunctions.ClickHouse.exp10(x)` / `exp10(x)` | `exp10(x)` | Десять в степени `x`. |

У каждой функции две перегрузки: `double f(double x)` и `double? f(double? x)`. ClickHouse возвращает
`Float64` для ненулевой формы и `Nullable(Float64)` для nullable-формы; аргумент `NULL` даёт `NULL`, а
не ноль. На закреплённой версии 25.8 `log2(8) = 3`, `cbrt(8) = 2`, `exp2(3) = 8` и `exp10(2) = 100`.

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        L2 = SqlFunctions.ClickHouse.log2((double)e.Id),
        Cbrt = SqlFunctions.ClickHouse.cbrt((double)e.Id),
        Exp2 = SqlFunctions.ClickHouse.exp2((double)e.Id),
        E10 = SqlFunctions.ClickHouse.exp10((double)e.Id),
        NullableL2 = SqlFunctions.ClickHouse.log2(e.Double)
    })
    .ToList();
```

### Поставленные числовые, округляющие и битовые функции (#286, T286b)

Остальные числовые семейства ClickHouse — тоже типизированные члены. `is_finite`/`is_infinite`/
`is_nan` возвращают `bool`, целочисленные формы сохраняют ширину вендора (`int_div`/`int_exp2`/
`int_exp10` — `UInt64` → `ulong`), семейство округления возвращает целочисленную ширину аргумента, а
битовое семейство объявляет `UInt64`-расширенную арифметику (`bitAnd`/`bitOr`/`bitXor`/`bit_count`/
`bit_hamming_distance` → `ulong`) с сохраняющими ширину сдвигами/ротациями и булевыми `bitTest*`. У
каждой есть перегрузка, пробрасывающая `NULL`, там где вендор принимает `Nullable`.

| Функция | SQL | Результат |
|---|---|---|
| `SqlFunctions.ClickHouse.erf(x)` / `erfc(x)` | `erf(x)` / `erfc(x)` | `double` (`Nullable` для `double?`) |
| `SqlFunctions.ClickHouse.lgamma(x)` / `tgamma(x)` | `lgamma(x)` / `tgamma(x)` | `double` |
| `SqlFunctions.ClickHouse.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` | `ulong` |
| `SqlFunctions.ClickHouse.is_finite(x)` / `is_infinite(x)` / `is_nan(x)` | `isFinite(x)` / `isInfinite(x)` / `isNaN(x)` | `bool` |
| `SqlFunctions.ClickHouse.hypot(a, b)` | `hypot(a, b)` | `double` |
| `SqlFunctions.ClickHouse.int_div(a, b)` / `int_exp2(x)` / `int_exp10(x)` | `intDiv(a, b)` / `intExp2(x)` / `intExp10(x)` | `ulong` |
| `SqlFunctions.ClickHouse.sigmoid(x)` / `sqr(x)` | `sigmoid(x)` / `sqr(x)` | `double` |
| `SqlFunctions.ClickHouse.round_age(x)` / `round_bankers(x)` / `round_down(x)` / `round_duration(x)` / `round_to_exp2(x)` | `roundAge` / `roundBankers` / `roundDown` / `roundDuration` / `roundToExp2` | целочисленная ширина аргумента |
| `SqlFunctions.ClickHouse.bitAnd(a, b)` / `bitOr` / `bitXor` | `bitAnd(a, b)` / `bitOr` / `bitXor` | `ulong` |
| `SqlFunctions.ClickHouse.bitNot(a)` / `bitShiftLeft(a, n)` / `bitShiftRight(a, n)` | `bitNot(a)` / `bitShiftLeft(a, n)` / `bitShiftRight(a, n)` | ширина операнда (`byte`/`int`/`uint`/`ulong`) |
| `SqlFunctions.ClickHouse.bitRotateLeft(a, n)` / `bitRotateRight(a, n)` | `bitRotateLeft(a, n)` / `bitRotateRight(a, n)` | ширина операнда |
| `SqlFunctions.ClickHouse.bitCount(a)` | `bitCount(a)` | `byte` |
| `SqlFunctions.ClickHouse.bitTest(a, i)` / `bitTestAll` / `bitTestAny` | `bitTest(a, i)` / `bitTestAll` / `bitTestAny` | `bool` |
| `SqlFunctions.ClickHouse.bitHammingDistance(a, b)` | `bitHammingDistance(a, b)` | `ulong` (включая строковую перегрузку) |

Переносимые эквиваленты `e`, `plus`, `minus`, `multiply`, `divide` и `negate` — это константа
`Math.E` и арифметические операторы CLR, а не члены только для ClickHouse.

## Расширенная математика PostgreSQL

Остальные математические функции входят в расширенную библиотеку скалярных функций
([`SupportsExtendedScalarFunctions`](xref:NextORM.Core.ISqlDialect.SupportsExtendedScalarFunctions), только PostgreSQL).
Функции `degrees`/`radians`/`pi` больше не специфичны для PostgreSQL — используйте переносимые
написания [`SqlFunctions.Sql`](xref:NextORM.Core.SqlFunctions.Sql) выше:

| C# | SQL |
|---|---|
| `SqlFunctions.Postgres.asin(x)` / `acos(x)` / `atan(x)` | `asin(x)` / `acos(x)` / `atan(x)` |
| `SqlFunctions.Postgres.atan2(y, x)` | `atan2(y, x)` |
| `SqlFunctions.Postgres.cbrt(x)` | `cbrt(x)` |
| `SqlFunctions.Postgres.sinh(x)` / `cosh(x)` / `tanh(x)` | `sinh(x)` / `cosh(x)` / `tanh(x)` |
| `SqlFunctions.Postgres.asinh(x)` / `acosh(x)` / `atanh(x)` | `asinh(x)` / `acosh(x)` / `atanh(x)` |
| `SqlFunctions.Postgres.random()` | `random()` |
| `SqlFunctions.Postgres.log(base, x)` | `log((base)::numeric, (x)::numeric)` — оба операнда приводятся к `numeric`, потому что двухаргументная форма в PostgreSQL есть только у `log(numeric, numeric)`; одноаргументный `log` остаётся `ln` |
| `SqlFunctions.Postgres.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` |
| `SqlFunctions.Postgres.factorial(n)` | `factorial(n)` |
| `SqlFunctions.Postgres.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

`SqlFunctions.Postgres.setseed(seed)` рендерит `setseed(seed)` и гейтится отдельно
[`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed) (только PostgreSQL). Функция
PostgreSQL возвращает `void`, поэтому проецируемое значение всегда `null`, а вызов делается ради
побочного эффекта (последующие `random()` в сессии становятся воспроизводимыми).

### Поставленные математические и градусные тригонометрические помощники (#281)

Оставшаяся часть математического каталога PostgreSQL доступна на той же поверхности, один член на
серверную перегрузку. Каждое имя гейтится **отдельно** через
[`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)), поэтому
провайдер, не реализующий интерфейс, отклоняет вызов `NotSupportedException`:

| C# | SQL | Результат |
|---|---|---|
| `SqlFunctions.Postgres.div(y, x)` | `div(y, x)` | `decimal?` — деление `numeric` с усечением к нулю |
| `SqlFunctions.Postgres.erf(x)` | `erf(x)` | `double?` — функция ошибок, в `-1..1` |
| `SqlFunctions.Postgres.erfc(x)` | `erfc(x)` | `double?` — `1 - erf(x)`, в `0..2` |
| `SqlFunctions.Postgres.random_normal(mean, stddev)` | `random_normal(mean, stddev)` | `double?` — volatile, новое значение на каждый запуск |
| `SqlFunctions.Postgres.acosd(x)` | `acosd(x)` | `double?` — арккосинус в градусах |
| `SqlFunctions.Postgres.asind(x)` | `asind(x)` | `double?` — арксинус в градусах |
| `SqlFunctions.Postgres.atand(x)` | `atand(x)` | `double?` — арктангенс в градусах |
| `SqlFunctions.Postgres.atan2d(y, x)` | `atan2d(y, x)` | `double?` — арктангенс `y`/`x` в градусах с учётом знаков обоих |
| `SqlFunctions.Postgres.cosd(x)` | `cosd(x)` | `double?` — косинус угла в градусах |
| `SqlFunctions.Postgres.cotd(x)` | `cotd(x)` | `double?` — котангенс угла в градусах |
| `SqlFunctions.Postgres.sind(x)` | `sind(x)` | `double?` — синус угла в градусах |
| `SqlFunctions.Postgres.tand(x)` | `tand(x)` | `double?` — тангенс угла в градусах |

`div` — это деление `numeric` с усечением, а не с округлением: `div(7, 2)` равно `3`, а
`div(-7, 2)` равно `-3`; деление на ноль вызывает ошибку на сервере. Семейство `*d` принимает
аргумент **в градусах** и возвращает градусы, поэтому `cosd(60)` равно `0.5`; его радианные
аналоги — строки `asin`/`acos`/`atan`/`atan2` выше. Выставлена только двухаргументная
`random_normal(mean, stddev)`; нуль- и одноаргументная формы сервера — нет. NULL-аргумент даёт NULL.

Имя, которое закреплённый сервер не может исполнить, отклоняется во время подготовки запроса:

| Семейство | Отклонённый член | Проверенная причина |
|---|---|---|
| numeric | `gamma(x)` | отсутствует в закреплённом PostgreSQL 17.11 — нет ни в `pg_proc`, ни в таблице математических функций PG17, а живой вызов падает с `42883`; имени нет ни в одном релизе, поэтому истории переименования тоже нет. |
| numeric | `tgamma(x)` | то же отсутствие в 17.11 (`42883`). |
| numeric | `lgamma(x)` | то же отсутствие в 17.11 (`42883`). |

## Числовой масштаб PostgreSQL

Три функции только для PostgreSQL описывают масштаб значения `numeric`. Они гейтятся **по имени**
через [`IScalarFunctions.Supports`](xref:NextORM.Core.IScalarFunctions.Supports(System.String)),
поэтому все остальные провайдеры отклоняют их `NotSupportedException`, не рендеря SQL PostgreSQL:

| C# | SQL | Результат |
|---|---|---|
| `SqlFunctions.Postgres.scale(decimal? value)` | `scale(value)` | `int?`: число цифр в дробной части, включая хвостовые нули |
| `SqlFunctions.Postgres.min_scale(decimal? value)` | `min_scale(value)` | `int?`: число цифр, остающихся после удаления хвостовых нулей |
| `SqlFunctions.Postgres.trim_scale(decimal? value)` | `trim_scale(value)` | `decimal?`: значение без хвостовых нулей в дробной части |

`scale` доступна во всех поддерживаемых версиях PostgreSQL; `min_scale` и `trim_scale` появились в
PostgreSQL 13. Все три возвращают NULL для NULL-аргумента. `trim_scale` только удаляет хвостовые
нули — это не `round`/`trunc`, значимые цифры не меняются:

```csharp
var rows = dataContext.From<IComplexEntity>()
    .Select(e => new
    {
        Scale = SqlFunctions.Postgres.scale(12.3400m),
        MinScale = SqlFunctions.Postgres.min_scale(12.3400m),
        Trimmed = SqlFunctions.Postgres.trim_scale(12.3400m)
    })
    .ToList();
// Scale = 4, MinScale = 2, Trimmed = 12.34 (масштаб decimal 2)
```

`12.3400` сообщает масштаб 4, потому что в дробной части четыре цифры, а `min_scale` игнорирует
хвостовые нули и сообщает 2. `trim_scale(12.3456m)` возвращает `12.3456` без изменений с масштабом 4,
а `trim_scale(0.0000m)` возвращает `0` с масштабом 0.

## Случайные числа SQL Server

`SqlFunctions.SqlServer.rand()` / `rand(seed)` рендерятся как `rand()` / `rand(seed)` и гейтятся по
имени через [`ISqlServerFunctions`](xref:NextORM.Core.ISqlServerFunctions) (только SQL Server; все
остальные бросают `NotSupportedException`):

| C# | SQL |
|---|---|
| `rand()` / `rand(seed)` | `rand()` / `rand(seed)` |

`rand` без seed **недетерминирован** и вычисляется сервером; фиксированный целый seed делает
последовательность воспроизводимой. Аналог `random()`/`setseed()` в PostgreSQL — в разделе про
расширенную математику выше.
