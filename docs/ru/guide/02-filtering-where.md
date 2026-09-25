# Фильтрация (WHERE)

> Стройте предикаты SQL `WHERE` из операторов C#, проверок на null, условных выражений и параметризованных списков значений.

**Предварительные требования:** [Запросы и проекции](01-querying-and-projections.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)

## Обзор

[`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) принимает логическое выражение и возвращает новый [`EntityBuilder<TEntity>`](xref:NextORM.Core.EntityBuilder`1); как и любой метод
построителя, он неизменяемый, поэтому исходный объект не меняется. Повторный вызов [`Where`](xref:NextORM.Core.EntityBuilder`1.Where(System.Linq.Expressions.Expression{System.Func{`0,System.Boolean}})) объединяет
предикаты с помощью `and`:

```csharp
public EntityBuilder<TEntity> Where(Expression<Func<TEntity, bool>> condition)
```

Лямбда транслируется в предложение `WHERE`, и ничего не выполняется, пока не вызван терминальный метод,
такой как [`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])). То же дерево выражений также компилирует и выполняет in-memory провайдер,
поэтому запрос, работающий с реальной базой данных, можно прогнать и в памяти.

Два вида значений по-разному попадают в базу данных:

* **Захваченная локальная переменная** (переменная из внешней области видимости) и [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))
  становятся параметрами команды, поэтому план может повторно использоваться при выполнениях с разными
  значениями.
* **Литеральная константа**, записанная непосредственно в лямбде, подставляется в текст SQL как есть.

## Операторы сравнения

| C# | SQL | Примечания |
|---|---|---|
| `x.Id == 1` | `id = 1` | `=`; с учётом null (см. ниже) |
| `x.Id != 1` | `id != 1` | `!=` |
| `x.Id > 1` | `(id > 1)` | `>`; операнды сравнения заключаются в скобки |
| `x.Id >= 1` | `(id >= 1)` | `>=` |
| `x.Id < 1` | `(id < 1)` | `<` |
| `x.Id <= 1` | `(id <= 1)` | `<=` |

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id >= 9)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from simple_entity where (id >= 9)
```

## Обработка null

Сравнение колонки с `null` генерирует `is null` / `is not null` вместо `=` / `!=`:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Where(x => x.String == null)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from complex_entity where somestring is null
```

`!= null` генерирует `is not null`. Эта перезапись управляется тем, что операндом сравнения является
литерал `null`; колонку, допускающую null, следует проверять литералом `null`, а не захваченным
значением, которое лишь предполагается равным `null`.

## Объединение предикатов

`&&` и `||` отображаются на `and` и `or` и заключаются в скобки как группа:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Where(x => x.Boolean == true && x.Id > 1)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from complex_entity where (b = 1 and (id > 1))
```

В PostgreSQL логический литерал - это `true` (`b = true`); в SQLite и SQL Server - `1`
(`b = 1`).

## Отрицание

`!` отображается на `not (...)` и инвертирует весь операнд:

```csharp
var ids = await dataContext.From<ComplexEntity>()
    .Where(x => !x.Boolean!.Value)
    .Select(x => x.Id)
    .ToListAsync();
```

```sql
select id from complex_entity where not (b)
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id |
|----|
| 2 |
| 3 |

```csharp
.Where(x => !(x.Boolean!.Value && x.Id > 1L))
```

```sql
select id from complex_entity where not ((b and (id > 1)))
```

Когда отрицание проецируется, а не используется в фильтре, SQL Server вынужден материализовать его как
значение `bit` (`cast(case when not (b) then 1 else 0 end as bit)`); SQLite и PostgreSQL сохраняют
логический скаляр (`not (b)`).

## Арифметические и побитовые операторы

Арифметические операторы генерируются дословно и заключаются в скобки: `+`, `-`, `*`, `/` и `%`.

```csharp
var rows = await dataContext.From("simple_entity")
    .Where(tbl => tbl.GetInt64("id") + 2 == 1)
    .Select(tbl => new { Id = tbl.GetInt64("id") })
    .ToListAsync();
```

```sql
select id from simple_entity where (id + 2) = 1
```

Унарный минус - это `-(x)`, а дополнение до единицы - `~(x)`. Целочисленные побитовые операторы
отображаются следующим образом:

| C# | SQL | Примечания |
|---|---|---|
| `x.Id & 1` | `(id & 1)` | побитовое И |
| `x.Id \| 1` | `(id \| 1)` | побитовое ИЛИ |
| `x.Id << 1` | `(id << 1)` | сдвиг влево; недопустимо в T-SQL |
| `x.Id >> 1` | `(id >> 1)` | сдвиг вправо; недопустимо в T-SQL |
| `x.Id ^ 1` | - | XOR **не поддерживается** и выбрасывает `NotSupportedException` |

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => (x.Id & 1) == 1)
    .Select(x => new { x.Id })
    .ToListAsync();
```

```sql
select id from simple_entity where (id & 1) = 1
```

> Операторы сдвига генерируются буквально. SQLite и PostgreSQL принимают `<<` / `>>`; T-SQL -
> нет, поэтому запрос, использующий их, завершается ошибкой в SQL Server.

## COALESCE (`??`)

`??` превращается в функцию coalesce провайдера:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(x => new { x.Id, V = x.String ?? "" })
    .ToListAsync();
```

| Провайдер | SQL |
|---|---|
| SQLite | `select id, ifnull(somestring, '') as 'V' from complex_entity` |
| SQL Server | `select id, isnull(somestring,'') as [V] from complex_entity` |
| PostgreSQL | `select id, coalesce(somestring, '') as "V" from complex_entity` |

## Условный оператор (`?:`) и switch

Тернарный оператор превращается в ANSI `CASE WHEN`:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(x => new { x.Id, Size = x.Id > 1 ? "big" : "small" })
    .ToListAsync();
```

```sql
select id, case when (id > 1) then 'big' else 'small' end as 'Size' from complex_entity
```

Условное выражение может появляться внутри предиката:

```csharp
var count = await dataContext.From<ComplexEntity>()
    .Where(x => (x.Int == null ? 0 : x.Int) == 1)
    .CountAsync();
```

```sql
select count(*) from complex_entity where case when nullableint is null then 0 else nullableint end = 1
```

Выражение C# `switch` по константным шаблонам превращается в поисковый `CASE`:

```csharp
var rows = await dataContext.From<ComplexEntity>()
    .Select(e => new { e.Id, Label = e.Id switch { 1 => "one", 2 => "two", _ => "other" } })
    .ToListAsync();
```

```sql
select id, case when id = 1 then 'one' when id = 2 then 'two' else 'other' end as 'Label' from complex_entity
```

switch, в котором сравнение является вызовом метода (например, перегрузка сравнения строк, которую
компилятор C# использует для некоторых строковых шаблонов), не поддерживается и выбрасывает
`NotSupportedException`.

## Захваченные параметры и [`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32))

Захваченная локальная переменная извлекается как именованный параметр команды:

```csharp
var threshold = 5L;
var rows = await dataContext.From<ComplexEntity>()
    .Where(x => x.Id > threshold)
    .Select(x => new { x.Id })
    .ToListAsync();
```

| Провайдер | SQL |
|---|---|
| SQLite | `select id from complex_entity where (id > $threshold)` |
| SQL Server | `select id from complex_entity where (id > @threshold)` |
| PostgreSQL | `select id from complex_entity where (id > @threshold)` |

[`Parameter`](xref:NextORM.Core.SqlFunctions.Parameter``1(System.Int32)) объявляет параметр времени выполнения, значение которого передаётся терминальному
методу; это полезно, когда одна и та же форма запроса подготавливается или кэшируется и выполняется
многократно:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
    .Select(x => new { x.Id })
    .ToListAsync(42);
```

```sql
-- SQLite
select id from simple_entity where id = $norm_p0
```

Параметры времени выполнения именуются `norm_p{index}`; значение `42` привязывается к `norm_p0`
терминальным методом. См. [Повторное использование запросов: cache и Prepare](15-query-reuse.md)
для правил времени жизни.

Захваченная локальная переменная регистрируется **один раз на утверждение**, сколько бы раз она ни
встречалась и в каком бы источнике ни находилась. Переменная, использованная дважды в `WHERE` поверх
join-проекции, получает один placeholder, а переменная, живущая только внутри присоединённого
derived-подзапроса, привязывается на окружающей команде:

```csharp
var v = 5;
var derived = dataContext.From<ComplexEntity>()
    .Where(c => c.Int == v)
    .Select(c => new { c.Id });

var rows = await dataContext.From<SimpleEntity>()
    .Join(derived, (s, d) => s.Id == d.Id)
    .Where(p => p.Item1.Id != v || p.Item2.Id != v)   // один параметр `@v`, переиспользуется
    .Select(p => new { p.Item1.Id })
    .ToListAsync();
```

Не нужно заводить отдельную локальную переменную на каждое обращение, чтобы каждая ссылка получила
собственный параметр.

## `IN` и `Contains`

`SqlFunctions.Sql.@in` принимает колонку плюс `QueryCommand<T>`, `IEnumerable<T>` или `params T[]`:

```csharp
var values = new long[] { 1, 3, 10 };
var ids = await dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.Sql.@in(e.Id, values))
    .Select(e => e.Id)
    .ToListAsync();
```

```sql
-- SQLite
select id from complex_entity where id in ($p0, $p1, $p2)
```

Вывод:

| Id |
|----|
| 1 |
| 3 |

`Contains` по захваченному `List<T>` или `T[]` порождает тот же предикат `IN`:

```csharp
var values = new List<long> { 1, 3 };
var ids = await dataContext.From<ComplexEntity>()
    .Where(e => values.Contains(e.Id))
    .Select(e => e.Id)
    .ToListAsync();
```

```sql
select id from complex_entity where id in ($p0, $p1)
```

Вывод:

| Id |
|----|
| 1 |
| 3 |

Особые случаи:

| Случай | SQL |
|---|---|
| Пустая коллекция | `where 1 = 0` (без параметров) |
| Один элемент | `where id in ($p0)` |
| Коллекция с `null` для колонки, допускающей null | `where (nullableint in ($p0) or nullableint is null)` |
| Коллекция, содержащая только `null` | `where nullableint is null` |

Захваченный список или массив захватывается деревом выражений по ссылке, но его значения встраиваются
в подготовленную команду. Когда коллекция изменяется или переприсваивается между выполнениями, nextorm
обнаруживает изменившуюся форму и перестраивает команду, поэтому второй [`ToList`](xref:NextORM.Core.EntityBuilderExtensions.ToList``1(NextORM.Core.EntityBuilder{``0},System.ReadOnlySpan{System.Object})) видит новые значения,
а не устаревшие результаты.

### `GLOBAL IN` (ClickHouse)

Распределённый предикат ClickHouse `column GLOBAL IN (subquery | values)` доступен через
[`SqlFunctions.ClickHouse.global_in`](xref:NextORM.Core.ClickHouseFunctions.global_in``1(``0,NextORM.Core.QueryCommand{``0})), принимающий ту же
левую колонку и правую часть, что и [`SqlFunctions.Sql.@in`](xref:NextORM.Core.CommonFunctions.in``1(``0,NextORM.Core.QueryCommand{``0})):

```csharp
var values = new int[] { 1, 3 };
var ids = dataContext.From<ComplexEntity>()
    .Where(e => SqlFunctions.ClickHouse.global_in(e.Id, values))
    .Select(e => e.Id)
    .ToList();
```

```sql
select id from complex_entity where global in (@p0, @p1)
```

Отрицание через `!` в C# даёт `GLOBAL NOT IN`. Предикат требует
[`SupportsGlobalPredicates`](xref:NextORM.Core.ISqlDialect.SupportsGlobalPredicates) и доступен только
в ClickHouse; остальные провайдеры и контекст in-memory выбрасывают `NotSupportedException`.
Полный каталог — в разделе [Специфичный для провайдеров SQL](provider-specific/overview.md).

## Предикаты шаблонов и подзапросов

`SqlFunctions.Sql.like`, `SqlFunctions.Sql.exists`, `SqlFunctions.Sql.any` и `SqlFunctions.Sql.all` - оставшиеся вспомогательные
предикаты:

| Выражение | SQL |
|---|---|
| `x.String.Contains("df")` | `somestring like '%df%'` |
| `x.String.StartsWith("xx")` | `somestring like 'xx%'` |
| `x.String.EndsWith("sd")` | `somestring like '%sd'` |
| `x.String.Contains("a%b_c")` | `somestring like '%a\%b\_c%' escape '\'` |
| `SqlFunctions.Sql.like(x.String, "%a%")` | `somestring like '%a%'` |
| `SqlFunctions.Sql.like(x.String, "%a!%", "!")` | `somestring like '%a!%' escape '!'` |
| `SqlFunctions.Sql.exists(query)` | `exists(<query>)` |
| `x.Id == SqlFunctions.Sql.any(query)` | `id = any(<query>)` |
| `x.Id == SqlFunctions.Sql.all(query)` | `id = all(<query>)` |
| `SqlFunctions.Postgres.any(x, array)` | `x = any(@array)` (PostgreSQL) |
| `x == SqlFunctions.Postgres.any(array)` | `x = any(@array)` (PostgreSQL) |
| `SqlFunctions.Sql.contains(x.String, "foo")` | `contains(somestring, 'foo')` (SQL Server) |
| `SqlFunctions.Sql.freetext(x.String, "foo")` | `freetext(somestring, 'foo')` (SQL Server) |

`SqlFunctions.Sql.contains`/`SqlFunctions.Sql.freetext` — предикаты полнотекстового поиска
([`SupportsFullText`](xref:NextORM.Core.ISqlDialect.SupportsFullText), отрисовываются через [`MakeFullText`](xref:NextORM.Core.ISqlDialect.MakeFullText(System.String,System.String,System.String))); колонка должна быть
полнотекстово проиндексирована. SQL Server отрисовывает `contains`/`freetext` (при проецировании
материализуются в `bit`), PostgreSQL — `to_tsvector(col) @@ plainto_tsquery(search)` (или
`websearch_to_tsquery` для `freetext`), MySQL/MariaDB — `match(col) against(search in boolean mode) > 0`
(режим natural language для `freetext`).

Скалярный подзапрос также допустим в `WHERE`: одно-строчный терминал в правой части (`First`,
`Single`, их формы `*OrDefault` или агрегат, например `Count`) отрисовывается как `select` в
скобках и может ссылаться на внешнюю строку (коррелированный). Эти формы, а также все позиции
подзапросов и ограничения in-memory провайдера описаны в [Подзапросы](06-subqueries.md).

Для захваченного шаблона подстановочные знаки конкатенируются вокруг параметра во время построения,
например `somestring like '%' || $needle || '%'` в SQLite. `any` и `all` не поддерживаются движком
SQLite и завершаются ошибкой при выполнении оператора. Для **массива** они требуют PostgreSQL, где
массив целиком привязывается как один параметр; см.
[Массивы](11-scalar-functions.md#массивы-postgresql).

## Отображение функций и операторов

| Конструкция C# | SQL |
|---|---|
| `==` / `!=` | `=` / `!=`; `is` / `is not` для `null` |
| `>`, `>=`, `<`, `<=` | `>`, `>=`, `<`, `<=` (в скобках) |
| `&&` / `\|\|` | `and` / `or` |
| `!` | `not (...)`; проецируемый логический результат приводится к `bit` в SQL Server |
| `+ - * / %` | `+ - * / %`; строковый `+` - это `\|\|` в SQLite/PostgreSQL и `+` в SQL Server |
| `&` / `\|` | `&` / `\|` |
| `<<` / `>>` | `<<` / `>>` |
| `^` | не поддерживается (`NotSupportedException`) |
| `~x` | `~(x)` |
| `-x` | `-(x)` |
| `??` | `ifnull` (SQLite), `isnull` (SQL Server), `coalesce` (PostgreSQL) |
| `?:` | `case when ... then ... else ... end` |
| `switch` | поисковый `case when ... then ... end` |
| `SqlFunctions.Sql.@in` / `Contains` | `in (...)` |
| `SqlFunctions.Sql.like` / `Contains` / `StartsWith` / `EndsWith` | `like` |
| `SqlFunctions.Sql.contains` / `SqlFunctions.Sql.freetext` | `contains` / `freetext` (SQL Server); сопоставление `@@` (PostgreSQL); `match ... against` (MySQL/MariaDB) |

## Различия провайдеров

| Провайдер | Поведение |
|---|---|
| SQLite | Параметры используют `$` (`$norm_p0`); `??` - это `ifnull`; логические литералы - `1` / `0`; `any` / `all` недоступны. |
| SQL Server | Параметры используют `@`; `??` - это `isnull`; логические литералы - `1` / `0`; проецируемый логический предикат оборачивается в `cast(case ... as bit)`; сдвиги недопустимы в T-SQL. |
| PostgreSQL | Параметры используют `@`; `??` - это `coalesce`; логические литералы - `true` / `false`; логические скаляры не требуют приведения. |
| MySQL | Параметры используют `@`; `??` - это `coalesce`; логические литералы - `1` / `0`; `any` / `all` недоступны; побитовое дополнение целого отрисовывается как `(-(x) - 1)`. |
| MariaDB | То же, что MySQL: параметры `@`, `coalesce`, логические `1` / `0`, `any` / `all` недоступны. |
| ClickHouse | Параметры используют `@` (драйвер переписывает их в `{name:Type}`); `??` - это `coalesce`; логические литералы - `true` / `false`; `any` / `all` недоступны; `global_in` добавляет распределённый предикат `GLOBAL IN`. |
| In-memory | Предикаты компилируются как делегаты .NET; префикса параметров и генерации SQL нет. |

## См. также

* [Запросы и проекции](01-querying-and-projections.md)
* [Сортировка и постраничная выборка](05-sorting-and-paging.md)
* [Скалярные функции](11-scalar-functions.md)
* [Подзапросы](06-subqueries.md)
* [Специфичный для провайдеров SQL](provider-specific/overview.md)
* [Ограничения и возможности вне области охвата](../advanced/limitations.md)

---

Source: `tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:187`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:202`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:216`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:230`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:244`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:254`,
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:276`,
`tests/nextorm.integration.tests/CommonTestSuite.Conditional.cs:9`,
`tests/nextorm.integration.tests/CommonTestSuite.Unary.cs:8`,
`tests/nextorm.integration.tests/CommonTestSuite.In.cs:9`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:602`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:664`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:775`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:821`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:997`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1051`,
`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1107`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:422`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:367`,
`src/nextorm.core/Query/SqlFunctions.cs:185`;
`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:2032`.
