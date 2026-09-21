# TODO: Интерцепторы (hooks) по образцу linq2db

> Рабочий план (design RFC). Источник: `comparison/linq2db-comparison.md:54,81` — разрыв в расширяемости
> («Extensibility (interceptors, custom SQL, query filters): extensive vs minimal»).

## Пункт и цель

- Фича: публичный механизм интерцепторов для перехвата жизненного цикла команды, соединения и
  исключений — без наследования `DataContext` и без форка библиотеки.
- Критерий приёмки: потребитель регистрирует интерцептор на `DataContextBuilder`, получает события
  «команда выполняется/выполнена/упала», «соединение открывается/открыто» и может транслировать
  исключения БД; при отсутствии зарегистрированных интерцепторов путь исполнения не аллоцирует.

## Что дают интерцепторы linq2db

| Интерфейс | События | Назначение |
|---|---|---|
| `ICommandInterceptor` | `CommandInitialized`, `ExecuteScalar/NonQuery/Reader` (+Async), `AfterExecuteReader`, `BeforeReaderDispose` | правка команды, замеры, профилирование, provider-specific опции |
| `IConnectionInterceptor` | `ConnectionOpening/Opened` (+Async) | аудит открытия, трейсинг, прогрев |
| `IDataContextInterceptor` | `OnClosing/OnClosed` | жизненный цикл контекста |
| `IExceptionInterceptor` | `ProcessException` | трансляция ошибок БД в доменные |
| `IEntityServiceInterceptor` | `EntityCreated` | создание сущности |
| `IUnwrapDataObjectInterceptor` | `UnwrapConnection/Transaction/Command/DataReader` | разворачивание обёрток (MiniProfiler) |
| `IQueryExpressionInterceptor` / query filters | построение выражения | глобальные фильтры (soft-delete, multi-tenancy) |

Регистрация: per-instance (`AddInterceptor`), глобально (`DataOptions.UseInterceptor`), разово
(`OnNextCommandInitialized`). Несколько интерцепторов одного типа исполняются в порядке добавления.

## Зачем нужны (сценарии)

1. Observability: замер длительности команд, метрики, structured-логирование SQL.
2. Правка команды: `OracleCommand.BindByName`, timeout, provider-специфичные опции.
3. Трансляция ошибок: `SqlException`/`NpgsqlException` → доменный тип.
4. Аудит открытий соединения и прогрев пула.
5. Глобальные фильтры запросов (soft-delete, multi-tenancy) — отдельный, более сложный слой.

## Точки расширения nextorm сегодня

| Механизм | Тип | Где |
|---|---|---|
| `ConnectionHooks` (`CreateDbConnection`/`OnConnectionCreated`) | internal | `DataContextDependencies.cs:30`, `DataContext.cs:39` |
| `OnConnectionCreated` | `protected virtual` | `DataContext.cs:113` |
| `ProviderHooks` (`MapColumn`/`CreateParam`/`CreateCommand`) | internal | `DataContextDependencies.cs:21` |
| `DataContext.Disposed` | публичное событие | `DataContext.cs:97` |
| `ISqlDialect.Make*` / `Supports*`, `[SqlFunction]`, `[SqlTableFunction]`, `RawSqlOverride`, `WithSql` | публичные | SQL-уровень |

Публичной точки перехвата **исполнения** (команда/соединение/исключение) нет — это и есть gap.

## Применимость по категориям

| Категория linq2db | Применимо в nextorm | Комментарий |
|---|---|---|
| `ICommandInterceptor` | **Да** (MVP) | одна точка создания команды + терминалы |
| `IConnectionInterceptor` | **Да** (MVP) | `DbConnectionManager` |
| `IExceptionInterceptor` | **Да** (MVP) | обернуть терминалы в `QueryExecutor` |
| `IDataContextInterceptor` | Частично | уже есть `Disposed`; `OnClosing` можно добавить даром |
| query filters / `IQueryExpressionInterceptor` | Позже (Фаза 2) | требует ключа план-кэша и per-context scoping |
| `IEntityServiceInterceptor` | Нет | нет сущностей и change tracking |
| `IUnwrapDataObjectInterceptor` | Нет/позже | обёртки конфликтуют со статическим план-кэшем и `ResetConnection` |
| DML-перехват | Нет | библиотека read-only by design |

## Дизайн: предлагаемые интерфейсы (Фаза 1)

```csharp
namespace NextORM.Core;

/// <summary>Per-execution metadata passed to command interceptors.</summary>
public readonly record struct CommandEventData(IDataContext DataContext, string? Sql);

/// <summary>Hooks into the command execution lifecycle.</summary>
public interface ICommandInterceptor
{
    void CommandInitialized(CommandEventData eventData, DbCommand command) { }
    void CommandExecuting(CommandEventData eventData, DbCommand command) { }
    void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed) { }
    void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception) { }
}

/// <summary>Metadata passed to connection interceptors.</summary>
public readonly record struct ConnectionEventData(IDataContext DataContext, DbConnection Connection);

/// <summary>Hooks into connection open/close.</summary>
public interface IConnectionInterceptor
{
    void ConnectionOpening(ConnectionEventData eventData) { }
    void ConnectionOpened(ConnectionEventData eventData) { }
}

/// <summary>Metadata passed to exception interceptors.</summary>
public readonly record struct ExceptionEventData(IDataContext DataContext, string? Sql);

/// <summary>Translates provider exceptions into caller-defined types.</summary>
public interface IExceptionInterceptor
{
    Exception ProcessException(ExceptionEventData eventData, Exception exception) => exception;
}
```

- Default interface methods → потребитель переопределяет только нужные события, а новые события
  добавляются без binary-breaking изменений (extend-only, см. `API-NAMING-REVIEW.md`).
- Все публичные типы и члены — с `<summary>` (CS1591 + `TreatWarningsAsErrors=true`).

## Точки встраивания

Параметр-объект (рядом с `ProviderHooks`/`ConnectionHooks` в `DataContextDependencies.cs`):

```csharp
internal readonly record struct InterceptorHooks(
    ICommandInterceptor[] Command,
    IConnectionInterceptor[] Connection,
    IExceptionInterceptor[] Exception)
{
    public static readonly InterceptorHooks Empty =
        new([], [], []);
}
```

`Array.Empty` → проверка `hooks.Command.Length == 0` не аллоцирует и не вызывает ничего.

| Событие | Точка | Файл:строка |
|---|---|---|
| `CommandInitialized` | сразу после `_createCommand(sql)` при построении плана | `QueryPlanner.cs:137` |
| `CommandExecuting` | buffered/scalar/First/… | `QueryExecutor.cs:64`, `:77` |
| `CommandExecuting` | streaming `InitReader` | `ResultSetEnumerator.cs:173`, `:187` |
| `CommandExecuted`/`Failed` | вокруг `ExecuteReader*`/`ExecuteScalar*` | `QueryExecutor.cs:131,151,239,258,285,305,325,345,361,388,415,440`; `ResultSetEnumerator.cs:177,191` |
| `ConnectionOpening/Opened` | `EnsureConnectionOpen`/`Async` | `DbConnectionManager.cs:44`, `:54` |
| `ExceptionInterceptor` | обернуть тело терминала, `ProcessException` → rethrow или подмена | `QueryExecutor.cs`, `ResultSetEnumerator.cs` |

## Регистрация

```csharp
builder
    .UseLoggerFactory(loggerFactory)
    .AddInterceptor(new MetricsCommandInterceptor())
    .AddInterceptor(MyExceptionInterceptor.Instance);

var ctx = builder.CreateDataContext();
```

- `DataContextBuilder.AddInterceptor(...)` накапливает иммутабельный список, `CreateDataContext()`
  строит `InterceptorHooks`.
- `DataContext.AddInterceptor(...)` — per-instance (после создания контекста); хранить в снимке,
  публикуемом через `Interlocked`/volatile, чтобы не ломать потокобезопасность.
- Порядок вызова = порядок регистрации (как в linq2db).

## Ограничения и цена

- **План-кэш статический** (`QueryPlanStore`) и команда переиспользуется (`DbPreparedQueryCommand`
  держит один `DbCommand`). Значит: `CommandInitialized` в nextorm = момент построения плана, а
  per-execution событие — `CommandExecuting`. Интерцептор **не должен менять `CommandText`** — иначе
  кэшированный SQL разойдётся между вызовами; для этого нужен отдельный механизм ключа плана (Фаза 2).
- **Аллокации:** zero-cost при пустых списках обязателен, иначе просядут бенчмарки
  (`docs/specs/performance/benchmark-report.md`). Никаких замыканий/`foreach` при `Length == 0`.
- **Потокобезопасность:** список интерцепторов — неизменяемый снимок; сами интерцепторы обязаны быть
  thread-safe (контекст может использоваться параллельно).
- **Публичный API:** сейчас «запирается» (`API-NAMING-REVIEW.md`). Поэтому только 3 интерфейса + 3
  `readonly record struct` в Фазе 1; не выкатывать весь набор linq2db сразу.
- **`QueryExecutor`/`DbConnectionManager` — `internal sealed`:** пробрасывать `InterceptorHooks`
  конструктором, а не через зависимость на конкретный `DataContext`.

## Этапы внедрения

- **Фаза 1 (MVP):** `ICommandInterceptor` + `IConnectionInterceptor` + `IExceptionInterceptor`,
  регистрация на `DataContextBuilder`/`DataContext`, zero-cost при пустом наборе. Закрывает
  observability, provider-опции, трансляцию ошибок.
- **Фаза 2:** `IDataContextInterceptor.OnClosing/OnClosed` (дёшево) и query filters /
  `IQueryExpressionInterceptor` — с идентичностью фильтра в ключе плана и per-context scoping.
- **Вне области:** entity lifecycle (нет сущностей), unwrap-обёртки (конфликт с план-кэшем),
  DML-интерцепторы (read-only).

## План тестов

- Core (`tests/nextorm.core.tests/InterceptorTests.cs`): регистрация, порядок вызова, no-op при пустом
  наборе, `ProcessException` (подмена и проброс).
- SQLite (`tests/nextorm.sqlite.tests/`): `CommandExecuting`/`Executed` на реальном `:memory:`
  соединении; проверка, что интерцептор видит заполненные `DbCommand.Parameters`.
- Интеграция (`CommonTestSuite`): события соединения на Postgres/SQL Server, `CommandFailed` при
  синтаксической ошибке, трансляция `SqlException`.
- Ассерты порядка: `Executing` → `Executed`; при исключении `Executing` → `Failed`.
- Покрытие: не ниже базового (84.9% line / 73.2% branch); `coverage.settings.xml` включает
  `nextorm.{core,sqlite,postgres,sqlserver}` — новые файлы в core учитываются.

## Открытые вопросы

1. Нужен ли уже в Фазе 1 механизм `OnNextCommandInitialized` (разовый интерцептор)?
2. Как именно фильтры запросов участвуют в ключе плана (Фаза 2) — отдельный RFC.
3. Глобальная регистрация (аналог `DataOptions.UseInterceptor`) — нужна ли, или достаточно builder.
4. Именование: `ICommandInterceptor` vs `IQueryInterceptor`; согласовать с `API-NAMING-REVIEW.md`.

## Файлы к изменению

- Новое: `src/nextorm.core/Interceptors/ICommandInterceptor.cs`, `IConnectionInterceptor.cs`,
  `IExceptionInterceptor.cs`, `EventData.cs`.
- Правки: `DataContextDependencies.cs` (`InterceptorHooks`), `DI/DataContextBuilder.cs`,
  `DataContext/DataContext.cs`, `DataContext/QueryExecutor.cs`, `DataContext/DbConnectionManager.cs`,
  `DataContext/QueryPlanner.cs`, `DataContext/ResultSetEnumerator.cs`.
- Документация: новый гайд `docs/guide/19-interceptors.md` (+ `docs/ru/guide/`), `docs/advanced/
  api-reference.md` (+RU), `docs/advanced/limitations.md` (+RU), `comparison/linq2db-comparison.md`,
  `docs/specs/design/API-NAMING-REVIEW.md`.
