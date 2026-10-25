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
