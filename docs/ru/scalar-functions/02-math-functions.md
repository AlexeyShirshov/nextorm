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
select abs((id - 5)) from complex_entity
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
