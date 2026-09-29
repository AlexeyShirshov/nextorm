# Необработанный SQL (Raw SQL)

> Заменяйте генерируемый SQL запроса вручную написанным текстом, сохраняя сопоставление строк nextorm.

**Предварительные требования:** [Запросы и проекции](../querying/index.md) · [Сущности и метаданные](../getting-started/03-entities-and-metadata.md) · [Переиспользование запросов: кэш против Prepare](../infrastructure/01-query-reuse-and-caching.md)

## Обзор

[`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) и [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) позволяют сохранить обычный типизированный запрос как **форму результата** и
подставить необработанную инструкцию для выполнения. Всё остальное — проекция, сопоставление сущности,
конструирование через инициализацию членов, вложенные DTO — берётся из запроса, построенного до подстановки.

```csharp
// QueryCommand<TResult>
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params);

public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, PrepareFromSqlMode mode, CancellationToken cancellationToken = default);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this QueryCommand<TResult> queryCommand, string sql, object? @params, PrepareFromSqlMode mode, CancellationToken cancellationToken = default);

// EntityBuilder<TResult> convenience overloads
public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql);
public static QueryCommand<TResult> WithSql<TResult>(this EntityBuilder<TResult> entity, string sql, object? @params);
public static IPreparedQueryCommand<TResult> PrepareFromSql<TResult>(this EntityBuilder<TResult> entity, string sql);
```

* [`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) возвращает [`QueryCommand<TResult>`](xref:NextORM.Core.QueryCommand`1), который вы выполняете обычными терминалами
  ([`ToListAsync`](xref:NextORM.Core.EntityBuilderExtensions.ToListAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), [`FirstAsync`](xref:NextORM.Core.EntityBuilderExtensions.FirstAsync``1(NextORM.Core.EntityBuilder{``0},System.Object[])), ...). Он проходит через неявный кэш планов, как и любая другая команда.
* [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) возвращает [`IPreparedQueryCommand<TResult>`](xref:NextORM.Core.IPreparedQueryCommand`1); выполняйте его через перегрузки контекста
  (`dataContext.ToListAsync(prepared, ...)`, `dataContext.FirstAsync(prepared, ...)`, ...).
* `@params` — это обычный объект. Его **открытые свойства экземпляра** становятся именованными параметрами
  в порядке свойств, причём имя свойства используется как имя параметра.
* `mode` — это `[Flags]`-значение: [`None`](xref:NextORM.Core.PrepareFromSqlMode.None) (по умолчанию) — буферизованное/скалярное
  выполнение (как `nonStreamUsing: true` в `Prepare(...)`), [`Streaming`](xref:NextORM.Core.PrepareFromSqlMode.Streaming) требуется для потокового
  (небуферизованного) чтения, а [`StoreInCache`](xref:NextORM.Core.PrepareFromSqlMode.StoreInCache) заполняет кэш планов. Все перегрузки по умолчанию
  используют `None`, поэтому необработанный SQL не заполняет кэш планов без явного запроса.

Необработанная инструкция передаётся дословно, включая комментарии. Заполнители параметров должны
соответствовать тому, что ожидает базовый провайдер ADO.NET (`@name` для SQL Server/PostgreSQL;
Microsoft.Data.Sqlite также принимает `@name`, хотя генерируемый nextorm SQL для SQLite использует
`$name`).

## [`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String))

```csharp
var ids = await dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity --this is custom sql")
    .ToListAsync();
```

```sql
-- executed as written
select id from simple_entity --this is custom sql
```

Таблицы `Вывод:` ниже показывают строки, которые возвращает каждый пример на сид-данных интеграционных тестов (`tests/nextorm.integration.tests/Providers/SqliteTestProvider.cs`).

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Необработанная инструкция с именованными параметрами:

```csharp
var rows = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity where id = @id", new { id = 1 })
    .ToListAsync();
```

```sql
select id from simple_entity where id = @id
-- @id is bound from the property `id` of the params object
```

Вывод:

| Id |
|----|
| 1 |

## [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String))

Подготовьте необработанную инструкцию и выполните её в контексте. Параметры времени выполнения передаются
во время выполнения точно так же, как для `Prepare(...)`:

```csharp
var prepared = dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .PrepareFromSql("select id from simple_entity", cancellationToken);

var ids = await dataContext.ToListAsync(prepared);
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Параметр из объекта params плюс параметр времени выполнения (`@norm_p0`), переданный в терминал:

```csharp
var prepared = dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .PrepareFromSql("select id from simple_entity where id = @id+@norm_p0", new { id = 1 }, cancellationToken);

var entity = await dataContext.FirstAsync(prepared, 1);
// id = 1 + 1 = 2
```

Вывод:

| Id |
|----|
| 2 |

## Сопоставление результата

Тип результата определяется запросом, который вы строите **до** подстановки SQL:

```csharp
// scalar
var ids = await dataContext.From<ISimpleEntity>()
    .Select(it => it.Id)
    .WithSql("select id from simple_entity")
    .ToListAsync();

// entity member-init
var entities = await dataContext.From<SimpleEntity>()
    .Select(it => new SimpleEntity { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

// DTO
var dtos = await dataContext.From<SimpleEntity>()
    .Select(it => new IdDto { Id = it.Id })
    .WithSql("select id from simple_entity")
    .ToListAsync();

public sealed class IdDto
{
    public int Id { get; set; }
}
```

Вывод:

| Id |
|----|
| 1 |
| 2 |
| 3 |
| 4 |
| 5 |
| 6 |
| 7 |
| 8 |
| 9 |
| 10 |

Имена столбцов в необработанном списке `select` сопоставляются с этой проекцией, поэтому они должны точно
совпадать с сопоставленными именами столбцов (или именами `[Column]`).

## Композиция сырого SQL как источника `FROM`

[`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) использует сырой фрагмент как **источник**
запроса вместо сопоставленной таблицы, поэтому его можно фильтровать, присоединять, группировать,
проецировать и постранично листать как любой другой источник. Столбцы читаются через аксессоры
[`TableAlias`](xref:NextORM.Core.TableAlias) (`t["id"].AsInt`); именованные параметры привязываются по той
же конвенции объекта `params`.

```csharp
var rows = dataContext
    .FromSql("select id, somestring from complex_entity where id > @min", new { min = 5 })
    .Select(t => new { Id = t["id"].AsInt })
    .ToList();
```

```sql
select t1.id from (select id, somestring from complex_entity where id > @min) as "t1"
```

Фрагмент может быть и **присоединяемой** стороной (рендерится как производная таблица с псевдонимом):

```csharp
var rows = dataContext
    .From<ISimpleEntity>()
    .Join(dataContext.FromSql("select id from complex_entity"), (s, r) => s.Id == r["id"].AsInt)
    .Select(p => new { p.Item1.Id, R = p.Item2["id"].AsInt })
    .ToList();
```

```sql
select t1.id, t2.id from simple_entity as "t1" join (select id from complex_entity) as "t2" on t1.id = t2.id
```

Фрагмент эмитится дословно (передавайте только доверенный SQL). Провайдер включается через
[`SupportsRawSqlSource`](xref:NextORM.Core.ISqlDialect.SupportsRawSqlSource); его включают все SQL-провайдеры,
а SQLite опускает псевдоним производной таблицы, когда источник не присоединяется.

## Выполнение сырых команд (`ExecuteRaw`)

[`WithSql`](xref:NextORM.Core.EntityExtensions.WithSql``1(NextORM.Core.EntityBuilder{``0},System.String)) и [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) сохраняют типизированный запрос и подменяют его часть. Когда инструкция вообще не является отображаемым запросом — DDL/DML-команда, хранимая процедура или команда, возвращающая несколько наборов результатов, — используйте `ExecuteRaw` (произвольный текст команды) или `ExecuteProcedure` (процедура по имени), которые выполняют команду и возвращают [`ProcedureResult`](xref:NextORM.Core.ProcedureResult):

```csharp
// DataContext и роль IRawCommandExecutor на IDataContext
public ProcedureResult ExecuteRaw(string sql, params IReadOnlyList<ProcedureParameter> parameters);

// async: у развёрнутой формы нет токена; CancellationToken передаётся коллекционной формой
public Task<ProcedureResult> ExecuteRawAsync(string sql, params IReadOnlyList<ProcedureParameter> parameters);
public Task<ProcedureResult> ExecuteRawAsync(string sql, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default);

// перегрузка без параметров для асинхронного IDataContext
public static Task<ProcedureResult> ExecuteRawAsync(this IDataContext dataContext, string sql, CancellationToken cancellationToken = default);
```

Поскольку `parameters` — это `params`-коллекция, встроенный аргумент принимается в двух эквивалентных формах: развёрнутой `ExecuteRaw(sql, new ProcedureParameter("min", 0))` и коллекционной `ExecuteRaw(sql, [new ProcedureParameter("min", 0)])`. `ExecuteProcedure(name, new ProcedureParameter("a", 1))` работает так же. Параметр `params` обязан быть последним (CS0231), поэтому у асинхронных двойников без токена работает развёрнутый вызов `ExecuteRawAsync(sql, new ProcedureParameter(...))`, а для передачи `CancellationToken` нужна коллекционная форма `ExecuteRawAsync(sql, [p1, p2], cancellationToken)` — совмещать их нельзя. `ExecuteProcedureAsync` ведёт себя так же.

Текст инструкции передаётся дословно и **не** проходит через планировщик запросов, поэтому он никогда не переиспользует кэш планов; кэш мапперов результатов ведётся по **форме результата** (упорядоченные имена столбцов читателя и тип результата), а не по тексту SQL, поэтому произвольные инструкции не разрастаются в кэше.

> **SQL-инъекции.** `sql` выполняется дословно; планировщик не параметризует его. Никогда не конкатенируйте недоверенный ввод в текст — передавайте значения через `ProcedureParameter` и ссылайтесь на них плейсхолдерами.

> **Почему `ProcedureParameter`, а не params-объект?** `WithSql`, `PrepareFromSql` и `FromSql` проходят через планировщик, поэтому принимают и его конвенцию params-объекта: обычный объект, публичные свойства которого становятся именованными параметрами, в порядке свойств (`new { id = 1 }`). `ExecuteRaw` намеренно обходит планировщик — текст отправляется дословно, и свойства объекта никто не перебирает, — поэтому параметр задаётся явно, по имени. `ProcedureParameter` — этот явный дескриптор, и он является надмножеством простого входного значения: помимо `Name` и `Value` он несёт ADO.NET `Direction`, `DbType`, `Size` и `TypeName`, а также табличные строки `Table<T>`, поэтому та же форма обслуживает выходные и возвращаемые параметры и `ExecuteProcedure`. Анонимный объект мог бы выражать только входные значения.

`ProcedureResult` удерживает команду и её читатель открытыми до освобождения. В SQL Server **без MARS** открытый читатель блокирует любые другие команды на том же соединении, поэтому освободите результат перед следующей командой в контексте.

### Освобождение ресурсов

`ProcedureResult` владеет командой ADO.NET и читателем до момента освобождения; соединение по-прежнему принадлежит контексту. Всегда освобождайте результат (`using`/`await using`), чтобы команда и читатель были освобождены, а соединение снова стало свободным. `Dispose`/`DisposeAsync` идемпотентны.

```csharp
using var result = dataContext.ExecuteRaw("delete from simple_entity where id = @id", [new ProcedureParameter("id", 7)]);
// Read не вызывается: инструкция не возвращает набор результатов
```

### DML и DDL

Команда без набора результатов не требует чтения: выполните её и освободите результат. `Read<T>()` на таком результате бросает `InvalidOperationException` (наборов результатов нет), поэтому `ExecuteRaw` используется исключительно ради побочного эффекта.

```csharp
using (dataContext.ExecuteRaw("create table raw_log (id integer, message text)"))
{
}

using (dataContext.ExecuteRaw(
    "insert into raw_log (id, message) values (@id, @message)",
    [
        new ProcedureParameter("id", 1),
        new ProcedureParameter("message", "hello"),
    ]))
{
}
```

### Скалярные результаты

`Read<T>()` переходит к следующему набору результатов и материализует его. Если `T` — скаляр, читается столбец 0 каждой строки. Первый `Read` пропускает ведущие наборы без столбцов; когда наборов больше нет, `Read<T>()` бросает `InvalidOperationException`. SQL `NULL`, прочитанный как non-nullable скаляр, возвращает `default` (например, `0` для `int`); читайте через nullable `T` (`int?`), чтобы увидеть `NULL`.

```csharp
using var result = dataContext.ExecuteRaw("select count(*) as total from simple_entity");

IReadOnlyList<int> totals = result.Read<int>();
var total = totals[0];
```

### Сущности в результате

Если `T` — отображаемая сущность, каждый столбец текущего набора сопоставляется со свойством по **имени столбца читателя**, без учёта регистра, в два прохода: сначала с отображённым именем столбца, затем с именем CLR-свойства для ещё несопоставленных свойств. Каждый столбец читателя связывается не более чем с одним свойством, а каждое свойство — не более одного раза; дублированное имя столбца читателя связывается с первым вхождением, последующие игнорируются. Столбцы без подходящего свойства игнорируются, а свойства без столбца остаются со значением по умолчанию (сопоставление не обязано покрывать всю сущность). Свойство `Range<T>`, хранимое двумя столбцами, переупорядочивается так, чтобы нижняя граница шла непосредственно перед верхней; неполная пара в читателе отбрасывается. У `T` должен быть конструктор без параметров. Тип, который никогда не запрашивался через LINQ, отображается **на лету** своим отображением по умолчанию (атрибуты и авто-имена, с применением конвенции именования контекста к авто-именам).

```csharp
[SqlTable("raw_orders")]
public sealed class RawOrder
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

// развёрнутая params-форма; коллекционная [new ProcedureParameter("min", 0)] эквивалентна
using var result = dataContext.ExecuteRaw(
    "select name, id from raw_orders where id > @min order by id",
    new ProcedureParameter("min", 0));

IReadOnlyList<RawOrder> orders = result.Read<RawOrder>();
```

Всё, что не является ни скаляром, ни отображаемой сущностью с конструктором без параметров, бросает `NotSupportedException` (см. [Сопоставление, параметры и кэширование](#сопоставление-параметры-и-кэширование)).

### Несколько наборов результатов

Каждый вызов `Read<T>()` переходит к следующему набору результатов и материализует все его строки. Уже прочитанные наборы больше не возвращаются. Когда следующего набора нет, `Read<T>()` бросает `InvalidOperationException`.

```csharp
using var result = dataContext.ExecuteRaw("select 1 as a; select 2 as b");

IReadOnlyList<int> first = result.Read<int>();   // [1]
IReadOnlyList<int> second = result.Read<int>();  // [2]
// result.Read<int>(); теперь бросает InvalidOperationException
```

### Асинхронное выполнение

`ExecuteRawAsync` открывает читатель асинхронно; `ReadAsync<T>()` возвращает `IAsyncEnumerable<T>` по строкам текущего набора. `await using` асинхронно освобождает результат. Параметр `params` обязан быть последним, поэтому `CancellationToken` нельзя совместить с развёрнутой формой: передавайте токен коллекционной формой `ExecuteRawAsync(sql, [p1, p2], cancellationToken)` либо используйте развёрнутую форму без токена `ExecuteRawAsync(sql, new ProcedureParameter(...))`.

```csharp
await using var result = await dataContext.ExecuteRawAsync(
    "select id from simple_entity order by id",
    Array.Empty<ProcedureParameter>(),
    cancellationToken);

var ids = new List<int>();
await foreach (var id in result.ReadAsync<int>(cancellationToken))
    ids.Add(id);
```

### Выходные параметры и возвращаемое значение

[`ProcedureParameter`](xref:NextORM.Core.ProcedureParameter) описывает имя, значение и параметры ADO.NET `Direction`, `DbType`, `Size` и `TypeName`:

```csharp
public readonly record struct ProcedureParameter(
    string Name,
    object? Value,
    ParameterDirection Direction = ParameterDirection.Input,
    DbType? DbType = null,
    int? Size = null,
    string? TypeName = null);
```

Выходные параметры и возвращаемое значение объявляются через направление. ADO.NET заполняет их только после закрытия читателя, поэтому `OutputParameters` и `ReturnValue` закрывают читатель **при первом обращении** и отбрасывают ещё не прочитанный набор результатов. Пример ниже рассчитан на SQL Server (`exec` с выходными параметрами); поддержку обеспечивает базовый провайдер ADO.NET:

```csharp
using var result = dataContext.ExecuteRaw(
    "exec @result = dbo.usp_Add @a = @a, @b = @b, @sum = @sum output",
    [
        new ProcedureParameter("result", null, ParameterDirection.ReturnValue),
        new ProcedureParameter("a", 2),
        new ProcedureParameter("b", 3),
        new ProcedureParameter("sum", null, ParameterDirection.Output, DbType.Int32),
    ]);

var sum = result.OutputParameters[0].Value;   // closes the reader and returns 5
var returnValue = result.ReturnValue;         // same snapshot
```

`OutputParameters` — это `IReadOnlyList<ProcedureOutputParameter>` (`Name`, `Value`, `Direction`); `DBNull` нормализуется в `null`. `ReturnValue` — значение параметра `ParameterDirection.ReturnValue`, или `null`, если он не объявлен. Оба свойства возвращают снимок, поэтому живой `DbParameter` наружу не выдаётся.

`ReturnValue` заполняется только для **типа команды хранимой процедуры** (см. [`ExecuteProcedure`](#хранимые-процедуры-executeprocedure)) и только там, где у провайдера есть статус возврата (SQL Server). Текстовая команда, например `EXEC`, зависит от провайдера и обычно оставляет его незаданным — захватывайте значение через параметр `ParameterDirection.Output`.

`TypeName` (имя типа провайдера для структурированного / табличного параметра) поддерживается **только в SQL Server**, где он также помечает параметр `SqlDbType.Structured`. Он по-прежнему работает с устаревшим значением `DataTable`/`IEnumerable<SqlDataRecord>`, но рекомендуемая точка входа — [`ProcedureParameter.Table<T>`](#табличные-параметры). PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse отклоняют `TypeName` через `ArgumentException` (они эмулируют табличный параметр связанным массивом или JSON-документом, и именованного типа у них нет); только в in-memory табличных параметров нет вовсе.

### Хранимые процедуры (`ExecuteProcedure`)

Хранимая процедура вызывается по имени через выделенный API, который отправляет команду с `CommandType.StoredProcedure`:

```csharp
// DataContext и роль IRawCommandExecutor на IDataContext
public ProcedureResult ExecuteProcedure(string name, params IReadOnlyList<ProcedureParameter> parameters);

// async: у развёрнутой формы нет токена; CancellationToken передаётся коллекционной формой
public Task<ProcedureResult> ExecuteProcedureAsync(string name, params IReadOnlyList<ProcedureParameter> parameters);
public Task<ProcedureResult> ExecuteProcedureAsync(string name, IReadOnlyList<ProcedureParameter> parameters, CancellationToken cancellationToken = default);

// перегрузка без параметров для асинхронного IDataContext
public static Task<ProcedureResult> ExecuteProcedureAsync(this IDataContext dataContext, string name, CancellationToken cancellationToken = default);
```

> **SQL-инъекция.** `name` **не** заключается в кавычки, не экранируется и не параметризуется — он подставляется как текст команды. Никогда не передавайте недоверенный ввод как имя процедуры. Имена параметров передаются без префикса провайдера (например `@` в SQL Server).

Результат — тот же [`ProcedureResult`](xref:NextORM.Core.ProcedureResult), что и у `ExecuteRaw`: `Read<T>()`/`ReadAsync<T>()` читают наборы результатов процедуры по порядку, а `OutputParameters`/`ReturnValue` закрывают читатель при первом обращении. Поддержка объявляется через [`ISqlDialect.SupportsStoredProcedures`](xref:NextORM.Core.ISqlDialect.SupportsStoredProcedures): **SQL Server, PostgreSQL и MySQL/MariaDB** выполняют процедуры; **SQLite, ClickHouse и in-memory** бросают `NotSupportedException` до открытия соединения.

```csharp
// SQL Server: собственный статус возврата процедуры приходит через параметр ReturnValue.
using var result = dataContext.ExecuteProcedure(
    "dbo.usp_Add",
    [
        new ProcedureParameter("result", null, ParameterDirection.ReturnValue, DbType.Int32),
        new ProcedureParameter("a", 2, DbType.Int32),
        new ProcedureParameter("b", 3, DbType.Int32),
        new ProcedureParameter("sum", null, ParameterDirection.Output, DbType.Int32),
    ]);

var outputs = result.OutputParameters;   // "sum" -> 5
var returnValue = result.ReturnValue;    // статус возврата процедуры
```

| Провайдер | Генерируемый вызов | Примечания |
|---|---|---|
| SQL Server | `exec name ...` | Параметры `Output`/`InputOutput` и параметр `ReturnValue` для собственного статуса возврата процедуры. |
| PostgreSQL | `CALL name(...)` | Вызывает **процедуры** (PostgreSQL 11+) и только их. Значение параметра `INOUT`/`OUT` возвращается столбцом строки результата, и Npgsql копирует его в `OutputParameters` при закрытии читателя — поэтому для процедуры с `INOUT` `OutputParameters` заполнен. Для **функции** используйте `ExecuteRaw`. |
| MySQL / MariaDB | `CALL name(...)` | Параметры `IN`, `OUT` и `INOUT` (`OutputParameters`), а также наборы результатов через `Read<T>()`. Возвращаемого значения нет. |
| SQLite / ClickHouse / in-memory | — | `NotSupportedException` (capability равна `false`). |

Функция PostgreSQL вызывается через `ExecuteRaw`:

```csharp
using var result = dataContext.ExecuteRaw("select f(@a) as value", [new ProcedureParameter("a", 21)]);
var value = result.Read<int>()[0];
```

### Табличные параметры

Таблицу можно передать **параметром** (а не источником `FROM`) через фабрики `ProcedureParameter.Table<T>`, которые создают входной параметр с набором строк `IEnumerable<T>`:

```csharp
public static ProcedureParameter Table<T>(string name, IEnumerable<T> rows);
public static ProcedureParameter Table<T>(string name, string typeName, IEnumerable<T> rows);
```

Например, скалярный набор связывается с одним столбцом, а набор сущностей — по одному столбцу на отображаемое свойство:

```csharp
var ids = ProcedureParameter.Table("ids", new[] { 1, 2, 3 });   // один столбец int

var employees = ProcedureParameter.Table("rows", new[]          // по столбцу на отображаемое свойство
{
    new TvpRow { Id = 1, Name = "alpha" },
    new TvpRow { Id = 2, Name = "beta" },
});
```

Далее параметр передаётся в `ExecuteRaw`/`ExecuteProcedure` как любой другой входной параметр; SQL, потребляющий его, зависит от провайдера (см. таблицу и примеры по провайдерам ниже).

* **Тип строки.** **Скалярный** тип строки (примитив, `string`, `decimal`, `Guid`, `DateTime`/`DateTimeOffset`/`DateOnly`/`TimeOnly`, `TimeSpan`, `byte[]`, перечисление или nullable от них) связывается с одним столбцом. Любой другой тип считается **отображаемой сущностью**: столбцы — это её невычисляемые отображаемые свойства в порядке метаданных, **включая identity-столбцы** (то же отображение, что и у bulk insert). Свойство `Range<T>` отображается на два столбца и не поддерживается.
* **Capability.** Признак [`ISqlDialect.SupportsTableValuedParameters`](xref:NextORM.Core.ISqlDialect.SupportsTableValuedParameters) включает возможность: **SQL Server** связывает нативно, **PostgreSQL, MySQL/MariaDB и SQLite** эмулируют типизированным массивом или JSON-документом, **ClickHouse** эмулирует связанным `Array(T)`/`Array(Tuple(...))`, разворачиваемым на сервере через `arrayJoin(@p)`, а `NotSupportedException` бросает только **in-memory**.
* **`TypeName`** — **только SQL Server** (пользовательский табличный тип). PostgreSQL, MySQL/MariaDB, SQLite и ClickHouse отклоняют его через `ArgumentException`; там вызывайте `Table(name, rows)`.
* **Точность/масштаб decimal.** Столбец `decimal` отображаемой сущности может объявить точность/масштаб атрибутом [`DecimalPrecision`](xref:NextORM.Core.DecimalPrecisionAttribute) (`[DecimalPrecision(12, 4)]`) или fluent-методом `Property(x => x.Amount).DecimalPrecision(12, 4)`. Пара провайдер-нейтральна и валидируется относительно типа, фактически связываемого для столбца, как precision `1..38` и `0 <= scale <= precision` (максимум SQL Server). Требуется связанный провайдерный тип `decimal`: не-`decimal` модель, отображённая через конвертер значений в `decimal`, принимается, а `decimal`-модель, отображённая в не-`decimal` представление (конвертер в `string` или отображение `JsonColumn`), отклоняется. Пара вне диапазона бросает `ArgumentOutOfRangeException` (имя параметра — `precision` или `scale`); не-`decimal` связанный тип или пара с объявлением только одного из precision/scale бросает `InvalidOperationException`. Тогда SQL Server связывает столбец как `decimal(precision, scale)`, а ClickHouse — как `Decimal(precision, scale)`; без объявления действует дефолт провайдера (SQL Server `decimal(38,18)`, ClickHouse `Decimal(38, 10)`). У скалярного типа строки `decimal` нет свойства для аннотации, поэтому он сохраняет дефолт провайдера.
* **Fluent-отображение и атрибуты.** Объявление хотя бы одного свойства через fluent (`Property(x => ...)`) отображает **только** свойства, объявленные в билдере для этой сущности; атрибуты ([`Column`](xref:System.ComponentModel.DataAnnotations.Schema.ColumnAttribute), [`DecimalPrecision`](xref:NextORM.Core.DecimalPrecisionAttribute) и остальные) на свойствах, которые не были объявлены, не строятся автоматически и игнорируются. Объявите в билдере каждое нужное свойство либо полностью полагайтесь на атрибуты и не объявляйте свойства через fluent.
* **Конвертеры значений в `TimeSpan`.** Общий шов записи позволяет конвертеру владеть провайдерным представлением — сконвертированный `TimeSpan` связывается как есть, без приведения к целочисленной единице длительности. У табличного параметра нет нативного связывания длительности на провайдерах без типа длительности (SQL Server, SQLite, ClickHouse), поэтому столбец отображаемой сущности, конвертер которого нацелен на `TimeSpan`, отклоняется через `NotSupportedException` при построении столбцов: связывайте сконвертированное значение как его целочисленное хранение. В PostgreSQL `interval` и MySQL/MariaDB `TIME` нативны и сохраняют сконвертированное значение.
* **Пустой набор.** Пустая последовательность допустима и связывается с пустой таблицей.
* `name`/`rows` валидируются (`ArgumentException` для пустого имени, `ArgumentNullException` для null-последовательности), а табличный параметр доступен только на вход.

SQL, потребляющий параметр, зависит от провайдера:

| Провайдер | Как связывается `T` | Пример SQL с `@p` |
|---|---|---|
| SQL Server | `SqlDbType.Structured` + `TypeName` (пользовательский табличный тип), поток `SqlDataRecord` | `select sum(value) as total from @p` |
| PostgreSQL | скаляр → типизированный массив; сущность → документ `jsonb` | `select sum(x) as total from unnest(@p) as x` / `select x."Id", x."Name" from jsonb_to_recordset(@p) as x("Id" int, "Name" text)` |
| MySQL / MariaDB | JSON-документ | `select t.Id, t.Name from JSON_TABLE(@p, '$[*]' COLUMNS(Id INT PATH '$.Id', Name varchar(100) PATH '$.Name')) as t` |
| SQLite | JSON-документ | `select value from json_each(@p)` / `select json_extract(value, '$.Id') as Id from json_each(@p)` |
| ClickHouse | связанный `Array(T)` (скаляр) / `Array(Tuple(...))` (сущность), разворачиваемый через `arrayJoin` | `select arrayJoin(@p) as value` / `select t.1, t.2 from (select arrayJoin(@p) as t)` |
| In-memory | — | `NotSupportedException` |

SQL Server требует пользовательский табличный тип:

```sql
create type dbo.IdList as table (value int not null);
```

```csharp
using var result = dataContext.ExecuteRaw(
    "select sum(value) as total from @p",
    [ProcedureParameter.Table("p", "dbo.IdList", new[] { 1, 2, 3 })]);

var total = result.Read<int>()[0];   // 6
```

В SQL Server тип CLR каждого столбца выбирает фиксированный тип T-SQL (пользовательский табличный тип должен совпасть по порядку столбцов):

| CLR | T-SQL |
|---|---|
| `bool` | `bit` |
| `char` | `nchar(1)` |
| `sbyte`, `short` | `smallint` |
| `byte` | `tinyint` |
| `ushort`, `int` | `int` |
| `uint`, `long` | `bigint` |
| `ulong` | `decimal(20,0)` |
| `float` | `real` |
| `double` | `float` |
| `decimal` | `decimal(38,18)` (или объявленный `decimal(p,s)`) |
| `string` | `nvarchar(max)` |
| `Guid` | `uniqueidentifier` |
| `DateTime` | `datetime2` |
| `DateTimeOffset` | `datetimeoffset` |
| `DateOnly` | `date` |
| `TimeOnly` | `time` |
| `TimeSpan` | `bigint` (объявленная единица длительности или тики) |
| `byte[]` | `varbinary(max)` |

Столбец `decimal` использует точность/масштаб, объявленные через `[DecimalPrecision]` или fluent-отображение, по умолчанию — `decimal(38,18)`. Для другой точности/масштаба у любого другого типа или длины строки, отличной от `max`, объявите пользовательский табличный тип явно и передайте устаревший `DataTable` с `ProcedureParameter.TypeName` вместо фабрики `Table<T>`.

Эмулирующие провайдеры (PostgreSQL, MySQL/MariaDB, SQLite) сериализуют строки в JSON по таким правилам: `null` → JSON `null`, `DateTime`/`DateTimeOffset` → строки ISO-8601, `DateOnly` → `yyyy-MM-dd`, `TimeOnly` → `HH:mm:ss.fffffff`, `TimeSpan` → инвариантная форма `c`, если у провайдера есть нативный тип длительности (PostgreSQL `interval`, MySQL/MariaDB `TIME`), иначе — его хранимое целое (объявленная единица длительности или тики, например в SQLite), `Guid` → форма `D`, `byte[]` → base64, перечисления → число базового типа, а `NaN`/`±∞` → `NotSupportedException` (нет JSON-представления).

В PostgreSQL набор сущностей — это документ `jsonb`, ключи объектов которого равны отображаемым именам столбцов:

```csharp
using var result = dataContext.ExecuteRaw(
    "select x.\"Id\", x.\"Name\" from jsonb_to_recordset(@rows) as x(\"Id\" int, \"Name\" text)",
    [ProcedureParameter.Table("rows", employees)]);
```

В ClickHouse скалярный набор — нативный `Array(T)`, а набор сущностей — `Array(Tuple(...))`; сервер разворачивает и то, и другое через `arrayJoin`:

```csharp
public sealed class TvpRow
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

// скалярный набор -> select arrayJoin(@ids)
using (var result = dataContext.ExecuteRaw(
    "select arrayJoin(@ids) as value order by value",
    [ProcedureParameter.Table("ids", new[] { 3, 1, 2 })]))
{
    var values = result.Read<int>();   // [1, 2, 3]
}

// набор сущностей -> select t.1, t.2 from (select arrayJoin(@rows) as t)
using (var result = dataContext.ExecuteRaw(
    "select t.1 as Id, t.2 as Name from (select arrayJoin(@rows) as t) order by Id",
    [ProcedureParameter.Table("rows", new[]
    {
        new TvpRow { Id = 2, Name = "beta" },
        new TvpRow { Id = 1, Name = null },
    })]))
{
    var rows = result.Read<TvpRow>();
}
```

В ClickHouse нет хранимых процедур, поэтому табличный параметр потребляется SQL из `ExecuteRaw`/`ExecuteRawAsync`, вызывающим `arrayJoin` — `ExecuteProcedure` там бросает `NotSupportedException` ([`SupportsStoredProcedures`](xref:NextORM.Core.ISqlDialect.SupportsStoredProcedures)). Провайдер задаёт драйверу явный `ClickHouseType`, оборачивая nullable-колонку в `Nullable(...)`, поэтому null-элемент и пустой массив переживают круговой путь; пустой набор связывается пустым массивом, а null-элементы, nullable-колонки и пустые наборы покрыты тестами провайдера. Поддерживаются только CLR-типы с маппингом на ClickHouse (набор скалярных колонок: целочисленные типы, `float`/`double`, `decimal` как `Decimal(38, 10)`, `bool`, `string`/`char`, `Guid`, `DateTime`/`DateTimeOffset`/`DateOnly`, перечисление как его базовое число; `TimeOnly` связывается как `String` в инвариантном формате `HH:mm:ss.fffffff`, `TimeSpan` — как `Int64` в объявленной единице длительности (по умолчанию тики), `byte[]` — как `String`); любой другой тип колонки бросает `NotSupportedException`. Колонка `decimal` связывается как `Decimal(38, 10)` по умолчанию либо как объявленный `Decimal(precision, scale)`. Скалярный `sbyte`/`ushort`/`uint`/`ulong` связывается как нативный `Int8`/`UInt16`/`UInt32`/`UInt64`, а `char` — как `String` — тот же тип ClickHouse, что и у соответствующей колонки сущности. Колонка `byte[]` связывается как `String`, поэтому читатель строк возвращает её как `string`, а не `byte[]` — не полагайтесь на круговой путь `byte[]`. Весь набор связывается одним параметром-массивом — это не потоковый и не бинарный bulk-путь, поэтому очень большие наборы оставляйте API массовой вставки провайдера.

### Сопоставление, параметры и кэширование

* **Кэш мапперов — по форме, не по SQL.** Маппер результатов кэшируется по упорядоченным именам столбцов читателя и типу результата, поэтому две инструкции с одинаковой формой делят один маппер, а произвольный текст SQL не разрастает кэш. Значения параметров по-прежнему лучше передавать как [`ProcedureParameter`](xref:NextORM.Core.ProcedureParameter), а не встраивать — и ради защиты от инъекций, и чтобы текст SQL оставался стабильным.
* **Сопоставление.** `T` — либо скаляр (столбец 0), либо отображаемая сущность с конструктором без параметров. Любой другой тип бросает `NotSupportedException`.
* **Службы контекста.** Сырые команды идут через тот же исполнитель, что и остальной контекст, поэтому они учитывают текущую транзакцию, таймаут команды, интерцепторы и логирование.
* **Поддержка провайдеров.** Контекст in-memory не поддерживает сырые команды и бросает `NotSupportedException`. Хранимые процедуры требуют `ISqlDialect.SupportsStoredProcedures` (SQL Server, PostgreSQL, MySQL/MariaDB); SQLite и ClickHouse бросают `NotSupportedException` до открытия соединения. Табличные параметры требуют `ISqlDialect.SupportsTableValuedParameters` (SQL Server, PostgreSQL, MySQL/MariaDB, SQLite, ClickHouse); in-memory бросает `NotSupportedException`.

## Различия между провайдерами

| Провайдер | Поведение |
|---|---|
| SQLite | Инструкция передаётся дословно; параметры связываются по имени (`@name` работает с `Microsoft.Data.Sqlite`; генерируемый SQL обычно использует `$name`). |
| SQL Server | Инструкция передаётся дословно; параметры `@name`. |
| PostgreSQL | Инструкция передаётся дословно; параметры `@name`. |
| MySQL | Инструкция передаётся дословно; параметры `@name`. |
| MariaDB | Инструкция передаётся дословно; параметры `@name`. |
| ClickHouse | Инструкция передаётся дословно; параметры `@name` (драйвер переписывает их в `{name:Type}`). |
| In-memory | [`PrepareFromSql`](xref:NextORM.Core.EntityExtensions.PrepareFromSql``1(NextORM.Core.EntityBuilder{``0},System.String)) и [`FromSql`](xref:NextORM.Core.DataContextExtensions.FromSql(NextORM.Core.IDataContext,System.String,System.Object)) не поддерживаются ([`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) бросает `NotSupportedException`); для необработанных инструкций используйте SQL-провайдер. |

## См. также

* [Переиспользование запросов: кэш против Prepare](../infrastructure/01-query-reuse-and-caching.md) — компромиссы `nonStreamUsing` / `storeInCache`.
* [Скалярные функции](../scalar-functions/index.md) — оставайтесь в LINQ вместо перехода к необработанному SQL.
* [Обзор провайдеров](../providers/overview.md) — заполнитель параметра для каждого провайдера.

---

Source: `src/nextorm.core/Query/QueryCommandExtensions.cs:7`, `src/nextorm.core/Builders/EntityExtensions.cs:5`, `src/nextorm.core/Query/RawSqlOverride.cs:3`, `src/nextorm.core/DataContext/InMemoryDataContext.cs:634`, `src/nextorm.core/DataContext/DataContextExtensions.cs` (`FromSql`), `src/nextorm.core/DataContext/SqlSourceRenderer.cs` (`MakeRawSqlSource`);
`tests/nextorm.integration.tests/CommonTestSuite.SqlCommand.cs:671`, `:698`, `:713`;
`src/nextorm.core/DataContext/ProcedureParameter.cs`, `src/nextorm.core/DataContext/ProcedureResult.cs`, `src/nextorm.core/DataContext/Roles/IRawCommandExecutor.cs` (`ExecuteRaw`/`ExecuteRawAsync`/`ExecuteProcedure`/`ExecuteProcedureAsync`, включая expanded-перегрузки с `params`), `src/nextorm.core/DataContext/DataContext.cs` (конкретные реализации тех же перегрузок сырых команд), `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs` (`SupportsStoredProcedures`, `SupportsTableValuedParameters`), `tests/nextorm.sqlite.tests/RawCommandTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite.Raw.cs`, `tests/nextorm.integration.tests/CommonTestSuite.StoredProcedures.cs`, `src/nextorm.core/DataContext/TableParameterValue.cs`, `src/nextorm.core/DataContext/TableParameterBinder.cs`, `src/nextorm.core/DataContext/ProcedureParameter.cs` (`Table<T>`), `src/nextorm.sqlserver/SqlServerDataContext.cs`, `src/nextorm.postgres/PostgresDataContext.cs`, `src/nextorm.mysql/MySqlDataContext.cs`, `src/nextorm.sqlite/SqliteDataContext.cs`, `src/nextorm.clickhouse/ClickHouseDataContext.cs`, `tests/nextorm.clickhouse.tests/TableValuedParameterTests.cs`, `tests/nextorm.integration.tests/ClickHouseTableValuedParameterTests.cs`.
