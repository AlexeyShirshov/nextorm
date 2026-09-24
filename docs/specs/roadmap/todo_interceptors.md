# TODO: Интерцепторы (hooks) по образцу linq2db

> Рабочий план (design RFC). Источник: `comparison/linq2db-comparison.md:54,81` — разрыв в расширяемости
> («Extensibility (interceptors, custom SQL, query filters): extensive vs minimal»).

## Статус

- **Фаза 1 — реализована** (публичные `IQueryInterceptor`/`IConnectionInterceptor`, регистрация на
  `DataContextBuilder` и `DataContext`, события на путях запросов, DML, стриминга и открытия соединения,
  zero-cost при пустом наборе). Пользовательская документация: `docs/guide/27-interceptors.md` (+ RU).
- Фаза 2 (query filters) вынесена в `todo_query_filters.md`; `IExceptionInterceptor` —
  вне области (см. ниже).
- Открытые решения фиксации публичного API — в `docs/specs/design/API-NAMING-REVIEW.md`
  (раздел «Interceptors Phase 1»).

## Пункт и цель

- Фича: публичный механизм интерцепторов для перехвата жизненного цикла команды и соединения —
  без наследования `DataContext` и без форка библиотеки.
- Критерий приёмки: потребитель регистрирует интерцептор на `DataContextBuilder`, получает события
  «команда выполняется/выполнена/упала», «соединение открывается/открыто»; при отсутствии
  зарегистрированных интерцепторов путь исполнения не аллоцирует.

## Что дают интерцепторы linq2db

| Интерфейс | События | Назначение |
|---|---|---|
| `ICommandInterceptor` | `CommandInitialized`, `ExecuteScalar/NonQuery/Reader` (+Async), `AfterExecuteReader`, `BeforeReaderDispose` | правка команды, замеры, профилирование, provider-specific опции |
| `IConnectionInterceptor` | `ConnectionOpening/Opened` (+Async) | аудит открытия, трейсинг, прогрев |
| `IDataContextInterceptor` | `OnClosing/OnClosed` | жизненный цикл контекста |
| `IExceptionInterceptor` | `ProcessException` | трансляция ошибок БД в доменные; в nextorm **out of scope** — см. ниже |
| `IEntityServiceInterceptor` | `EntityCreated` | entity lifecycle в linq2db (change tracking / materialization); в nextorm N/A |
| `IUnwrapDataObjectInterceptor` | `UnwrapConnection/Transaction/Command/DataReader` | разворачивание обёрток (MiniProfiler) |
| `IQueryExpressionInterceptor` | построение выражения | query filters — см. `todo_query_filters.md` |

Регистрация: per-instance (`AddInterceptor`), глобально (`DataOptions.UseInterceptor`), разово
(`OnNextCommandInitialized`). Несколько интерцепторов одного типа исполняются в порядке добавления.

## Зачем нужны (сценарии)

1. Observability: замер длительности команд, метрики, structured-логирование SQL.
2. Правка команды: `OracleCommand.BindByName`, timeout, provider-специфичные опции.
3. Аудит открытий соединения и прогрев пула.
4. Глобальные фильтры запросов (soft-delete, multi-tenancy) — вынесены в отдельный RFC `todo_query_filters.md`.

## Точки расширения nextorm сегодня

| Механизм | Тип | Где |
|---|---|---|
| `ConnectionHooks` (`CreateDbConnection`/`OnConnectionCreated`) | internal | `DataContextDependencies.cs:30`, `DataContext.cs:39` |
| `OnConnectionCreated` | `protected virtual` | `DataContext.cs:113` |
| `ProviderHooks` (`MapColumn`/`CreateParam`/`CreateCommand`) | internal | `DataContextDependencies.cs:21` |
| `DataContext.Disposed` | публичное событие | `DataContext.cs:97` |
| `ISqlDialect.Make*` / `Supports*`, `[SqlFunction]`, `[SqlTableFunction]`, `RawSqlOverride`, `WithSql` | публичные | SQL-уровень |

Публичной точки перехвата **исполнения** (команда/соединение) нет — это и есть gap.

## Два уровня перехвата (не смешивать)

| Уровень | Что перехватывает | Механизм | Где |
|---|---|---|---|
| Формирование запроса (expression/SQL) | построение дерева и SQL; «добавить фильтр» (soft-delete, tenant) | query filters / `IQueryExpressionInterceptor` | отдельный RFC `todo_query_filters.md` |
| Исполнение команды | уже собранную `DbCommand`; события до/после `Execute*` | `IQueryInterceptor` | этот RFC, `QueryExecutor` |

- На уровне `IQueryInterceptor` команда **уже сформирована**, поэтому добавить в неё фильтр нельзя —
  фильтр обязан строиться до сборки SQL и участвовать в ключе план-кэша (см. `todo_query_filters.md`).
- `CommandInitialized` — это перехват **сформированной** команды в момент построения плана (timeout,
  provider-специфичные опции). Менять `CommandText` на этом событии запрещено из-за кэша плана
  (см. «Ограничения и цена»).

## Применимость по категориям

| Категория linq2db | Применимо в nextorm | Комментарий |
|---|---|---|
| `ICommandInterceptor` | **Да** (MVP) | в nextorm — `IQueryInterceptor`; события вокруг терминалов |
| `IConnectionInterceptor` | **Да** (MVP) | `DbConnectionManager` |
| `IExceptionInterceptor` | Out of scope | явные вызовы терминалов покрываются штатным `try/catch` / слоем репозитория; см. раздел «Вне области» |
| `IDataContextInterceptor` | Частично | уже есть `Disposed`; `OnClosing` можно добавить даром |
| query filters / `IQueryExpressionInterceptor` | Отдельный RFC | см. `todo_query_filters.md` |
| `IEntityServiceInterceptor` | Out of scope | нет change tracking и события «сущность создана»: nextorm умеет только INSERT, вставка не проходит через сущностный lifecycle |
| `IUnwrapDataObjectInterceptor` | Нет/позже | обёртки конфликтуют со статическим план-кэшем и `ResetConnection` |
| DML-перехват | Да (события) | те же события исполнения, что и для запросов; переписать SQL мутации нельзя |

## Дизайн: предлагаемые интерфейсы (Фаза 1)

```csharp
namespace NextORM.Core;

/// <summary>Per-execution metadata passed to query interceptors.</summary>
public readonly record struct CommandEventData(IDataContext DataContext, string? Sql);

/// <summary>
/// Observes the query execution lifecycle. Raised by the context's query executor, not by the
/// command: <see cref="DbCommand"/> is only the event payload (it is a data structure and cannot
/// log or profile itself).
/// </summary>
public interface IQueryInterceptor
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
```

- События поднимает `QueryExecutor` (execution axis контекста, `QueryExecutor.cs:16`) — единственное
  место, которое реально выполняет запрос; `DbCommand` передаётся как payload, потому что команда —
  структура данных и сама логировать/профилировать не может. Отсюда имя `IQueryInterceptor`, а не
  `ICommandInterceptor`.
- Default interface methods → потребитель переопределяет только нужные события, а новые события
  добавляются без binary-breaking изменений (extend-only, см. `API-NAMING-REVIEW.md`).
- Все публичные типы и члены — с `<summary>` (CS1591 + `TreatWarningsAsErrors=true`).

## Точки встраивания

Параметр-объект (рядом с `ProviderHooks`/`ConnectionHooks` в `DataContextDependencies.cs`):

```csharp
internal sealed class InterceptorHooks
{
    internal IQueryInterceptor[] QueryInterceptors { get; }
    internal IConnectionInterceptor[] ConnectionInterceptors { get; }

    internal void Add(IQueryInterceptor interceptor);
    internal void Add(IConnectionInterceptor interceptor);
}
```

Реализовано как **класс**, а не `record struct`: список должен переживать per-instance
`DataContext.AddInterceptor`. Массивы copy-on-write, чтение/запись через `Volatile` под `lock`, поэтому
добавление интерцептора во время выполнения не рвёт читателя. Событийные циклы (`RaiseCommand*`,
`RaiseConnection*`) вынесены сюда статическими helper'ами, чтобы `QueryExecutor`, `QueryPlanner` и
`ResultSetEnumerator` не дублировали их.

Каждый wrapper сначала проверяет `interceptors.Length == 0` и в этом случае вызывает провайдер
напрямую: без интерцепторов не создаются ни данные события, ни отметки времени, ни делегаты, ни
async-машины состояний.

| Событие | Точка | Файл |
|---|---|---|
| `CommandInitialized` | после `_createCommand(sql)` и привязки параметров при построении плана | `QueryPlanner.cs` |
| `CommandInitialized` | после сборки команды мутации (per-execution — DML не кэшируется) | `QueryExecutor.cs` (`CreateMutationCommand`) |
| `CommandExecuting` | непосредственно перед `ExecuteReader`/`ExecuteScalar`/`ExecuteNonQuery` (sync и async) | `QueryExecutor.cs` (`Run*`) |
| `CommandExecuting` | стриминг, вокруг создания reader'а | `ResultSetEnumerator.cs` (`ExecuteReader`/`Async`) |
| `CommandExecuted`/`Failed` | там же, по завершении/исключению | `QueryExecutor.cs`, `ResultSetEnumerator.cs` |
| `ConnectionOpening/Opened` | `EnsureConnectionOpen`/`EnsureConnectionOpenAsync` | `DbConnectionManager.cs` |

## Регистрация

```csharp
builder
    .UseLoggerFactory(loggerFactory)
    .AddInterceptor(new MetricsCommandInterceptor());

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
- **Публичный API:** сейчас «запирается» (`API-NAMING-REVIEW.md`). Поэтому только 2 интерфейса + 2
  `readonly record struct` в Фазе 1; не выкатывать весь набор linq2db сразу.
- **`QueryExecutor`/`DbConnectionManager` — `internal sealed`:** пробрасывать `InterceptorHooks`
  конструктором, а не через зависимость на конкретный `DataContext`.

## Этапы внедрения

- **Фаза 1 (MVP) — реализовано:** `IQueryInterceptor` + `IConnectionInterceptor`, регистрация на
  `DataContextBuilder`/`DataContext`, события на путях buffered/scalar/First/…, streaming, DML и
  открытия соединения (sync+async), zero-cost при пустом наборе. Закрывает observability и
  provider-опции.
- **Фаза 2:** `IDataContextInterceptor.OnClosing/OnClosed` (дёшево). Query filters —
  отдельный RFC: [`todo_query_filters.md`](todo_query_filters.md).
- **Вне области:** `IExceptionInterceptor` — явные вызовы терминалов пользователь покрывает
  штатным `try/catch` (или слоем репозитория), регистрация маппинга на уровне конфигурации не
  окупает роста публичного API. Единственное ограничение — ленивое/потоковое исполнение
  (`IAsyncEnumerable`) всплывает вне `try/catch` вокруг вызова LINQ; это принимается осознанно.
  Также вне области: entity lifecycle (нет change tracking и notion «создание сущности» — только
  INSERT) и unwrap-обёртки (конфликт с план-кэшем). DML-команды **наблюдаемы** теми же событиями
  `IQueryInterceptor` (`CommandInitialized` per-execution, `CommandExecuting/Executed/Failed`), но
  переписать SQL мутации интерцептор не может.

## План тестов (реализовано)

- Core (`tests/nextorm.core.tests/InterceptorBuilderTests.cs`): null-аргументы и fluent-чейнинг
  `DataContextBuilder.AddInterceptor`.
- SQLite (`tests/nextorm.sqlite.tests/InterceptorTests.cs`): порядок вызова и lifecycle
  (`Inited` → `Executing` → `Executed`), заполненные `DbCommand.Parameters`, per-instance
  `AddInterceptor`, `CommandFailed` при ошибке провайдера, streaming и async-терминал, события
  соединения (sync и async), no-op без интерцепторов.
- Интеграция (`CommonTestSuite`) для событий соединения на Postgres/SQL Server **не добавлялась**:
  фича провайдер-независима и покрыта SQLite end-to-end; общий suite создаёт контекст без builder'а.
- Покрытие (прогон `dotnet-coverage` по unit-проектам core+sqlite+postgres+sqlserver, без контейнеров):
  **76.9% line / 68.3% branch** — выше порога `MIN_LINE_COVERAGE=75`. Новые файлы: `InterceptorHooks`
  83.3%/100%, `QueryPlanner` 97.3%/83.1%, async-ветки `RunReaderCoreAsync` 63.6% и
  `ResultSetEnumerator.ExecuteReaderCoreAsync` 66.7%. Цифра не сравнима напрямую с полным CI-прогоном
  (там ещё интеграция и остальные провайдеры), но порог не нарушен.

## Открытые вопросы

1. Нужен ли уже в Фазе 1 механизм `OnNextCommandInitialized` (разовый интерцептор)?
2. Глобальная регистрация (аналог `DataOptions.UseInterceptor`) — нужна ли, или достаточно builder.
3. Перенести итоговое имя `IQueryInterceptor` (вызывается `QueryExecutor`, `DbCommand` — payload) в
   `API-NAMING-REVIEW.md`.

## Файлы

- Новое (реализовано): `src/nextorm.core/Interceptors/IQueryInterceptor.cs` (с `CommandEventData`),
  `src/nextorm.core/Interceptors/IConnectionInterceptor.cs` (с `ConnectionEventData`).
- Правки (реализовано): `DataContext/DataContextDependencies.cs` (`InterceptorHooks`),
  `DI/DataContextBuilder.cs`, `DataContext/DataContext.cs`, `DataContext/QueryExecutor.cs`,
  `DataContext/DbConnectionManager.cs`, `DataContext/QueryPlanner.cs`,
  `DataContext/ResultSetEnumerator.cs`.
- Документация (реализовано): гайд `docs/guide/27-interceptors.md` (+ `docs/ru/guide/27-interceptors.md`),
  `docs/guide/toc.yml` (+ `docs/ru/toc.yml`), `docs/advanced/api-reference.md` (+RU),
  `docs/advanced/limitations.md` (+RU); регистр `docs/specs/design/API-NAMING-REVIEW.md` (раздел
  «Interceptors Phase 1»).
