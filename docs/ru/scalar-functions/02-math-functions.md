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
| `SqlFunctions.Postgres.log(base, x)` | `log(base, x)` |
| `SqlFunctions.Postgres.gcd(a, b)` / `lcm(a, b)` | `gcd(a, b)` / `lcm(a, b)` |
| `SqlFunctions.Postgres.factorial(n)` | `factorial(n)` |
| `SqlFunctions.Postgres.width_bucket(x, low, high, count)` | `width_bucket(x, low, high, count)` |

`SqlFunctions.Postgres.setseed(seed)` рендерит `setseed(seed)` и гейтится отдельно
[`SupportsRandomSeed`](xref:NextORM.Core.ISqlDialect.SupportsRandomSeed) (только PostgreSQL). Функция
PostgreSQL возвращает `void`, поэтому проецируемое значение всегда `null`, а вызов делается ради
побочного эффекта (последующие `random()` в сессии становятся воспроизводимыми).

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
