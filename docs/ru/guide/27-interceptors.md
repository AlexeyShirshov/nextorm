# Интерцепторы

> Наблюдайте за жизненным циклом выполнения команд и соединения контекста без наследования от `DataContext` и без логгера.

**Предварительные требования:** [Соединения и логирование](16-connections-and-logging.md) · [Переиспользование запросов](15-query-reuse.md) · [Dependency injection](../getting-started/04-dependency-injection.md).

## Обзор

Интерцептор — это небольшой объект, зарегистрированный на контексте и получающий обратные вызовы вокруг работы, которую выполняет контекст. nextorm поставляет два интерфейса:

| Интерфейс | Что наблюдает | Обратные вызовы |
|---|---|---|
| [`IQueryInterceptor`](xref:NextORM.Core.IQueryInterceptor) | выполнение команд | `CommandInitialized`, `CommandExecuting`, `CommandExecuted`, `CommandFailed` |
| [`IConnectionInterceptor`](xref:NextORM.Core.IConnectionInterceptor) | открытие соединения | `ConnectionOpening`, `ConnectionOpened` |

Обратный вызов получает данные события ([`CommandEventData`](xref:NextORM.Core.CommandEventData) / [`ConnectionEventData`](xref:NextORM.Core.ConnectionEventData)) и задействованный объект ADO.NET (`DbCommand` / `DbConnection`). События поднимает **execution/connection axis контекста**, а не команда: `DbCommand` — это структура данных, которая не может логировать или профилировать сама себя.

Типичные применения — замер времени и метрики, structured-логирование выполняемого SQL и провайдер-специфичные опции команды (например, таймаут).

## Регистрация интерцептора

Зарегистрируйте интерцептор на builder, чтобы он применялся ко всем контекстам, которые создаёт этот builder:

```csharp
using NextORM.Core;

var builder = new DataContextBuilder()
    .UseSqlite("app.db")
    .AddInterceptor(new TimingInterceptor());

using var ctx = builder.CreateDataContext();
```

Интерцепторы вызываются в порядке регистрации. Поскольку интерфейсы используют **default interface methods**, реализация переопределяет только нужные обратные вызовы — остальные остаются no-op. Те же регистрации работают через dependency injection:

```csharp
services.AddNextOrmContext(o => o
    .UseSqlite(connectionString)
    .AddInterceptor(new TimingInterceptor()));
```

Контекст, созданный из builder, может добавить интерцепторы на время своей жизни:

```csharp
using var ctx = builder.CreateDataContext();
ctx.AddInterceptor(new AuditInterceptor());
```

`AddInterceptor` доступен на [`DataContext`](xref:NextORM.Core.DataContext), а не на фасаде [`IDataContext`](xref:NextORM.Core.IDataContext), поэтому при работе через интерфейс нужно привести контекст к типу.

## Интерцептор запросов

```csharp
using System.Data.Common;
using NextORM.Core;

public sealed class TimingInterceptor : IQueryInterceptor
{
    public void CommandExecuting(CommandEventData eventData, DbCommand command)
        => Console.WriteLine($"executing: {eventData.Sql}");

    public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        => Console.WriteLine($"executed in {elapsed.TotalMilliseconds:F1} ms");

    public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        => Console.WriteLine($"failed: {exception.GetType().Name}");
}
```

Жизненный цикл буферизующего терминала (`ToList`, `First`, `ExecuteScalar`, ...), стримингового терминала (`ToAsyncEnumerable`) и DML-оператора (`Insert`, `Update`, `Delete`, `Merge`, `Truncate`) выглядит так:

```text
CommandInitialized -> CommandExecuting -> CommandExecuted
                                      \-> CommandFailed   (при ошибке провайдера)
```

* `CommandInitialized` срабатывает, когда команда создана и её параметры привязаны. Для запроса это происходит **один раз, при построении плана** (попадание в план-кэш повторно его не поднимает); для DML-оператора — **на каждом выполнении**, потому что команды мутаций не кэшируются.
* `CommandExecuting` срабатывает непосредственно перед выполнением, с полностью привязанной командой — здесь интерцептор может прочитать `command.Parameters`.
* `CommandExecuted` несёт затраченный `TimeSpan`; `CommandFailed` несёт исключение провайдера, которое затем пробрасывается без изменений.

## Интерцептор соединения

```csharp
public sealed class AuditConnectionInterceptor : IConnectionInterceptor
{
    public void ConnectionOpening(ConnectionEventData eventData)
        => Console.WriteLine($"opening {eventData.Connection.GetType().Name}");

    public void ConnectionOpened(ConnectionEventData eventData)
        => Console.WriteLine($"opened {eventData.Connection.DataSource}");
}
```

Соединение открывается лениво и переиспользуется, поэтому `ConnectionOpening`/`ConnectionOpened` срабатывают только на переходе из закрытого состояния в открытое — не на каждом запросе. Их поднимают и синхронный [`EnsureConnectionOpen`](xref:NextORM.Core.DataContext.EnsureConnectionOpen), и асинхронный [`EnsureConnectionOpenAsync`](xref:NextORM.Core.DataContext.EnsureConnectionOpenAsync(System.Threading.CancellationToken)).

## Гарантии

* **Порядок регистрации** сохраняется; каждый интерцептор типа получает каждое событие в порядке добавления, после настроенных на builder.
* **Потокобезопасность**: интерцепторы обязаны быть потокобезопасными, так как контекст может выполняться параллельно. Сам список интерцепторов — copy-on-write снимок, поэтому добавление интерцептора во время выполнения запросов безопасно.
* **Нулевая цена без интерцепторов**: если интерцепторы не зарегистрированы, путь исполнения делает ветвление и вызывает провайдер напрямую — никакие объекты событий, отметки времени, делегаты и async-машины состояний не аллоцируются.

## Ограничения

* **Не меняйте `CommandText`.** Команда запроса принадлежит кэшированному плану и переиспользуется между выполнениями; изменение текста молча рассинхронизирует кэш. `CommandInitialized` предназначен для опций, не влияющих на SQL (таймаут, провайдер-специфичные флаги).
* **Трансляция исключений вне области.** Exception-интерцептора нет: чтобы отобразить исключение провайдера на доменный тип, оберните явный вызов терминала (или метод репозитория) в `try`/`catch`. Единственный пробел — отложенное/стриминговое выполнение, где ошибка всплывает во время перечисления `IAsyncEnumerable`, а не в точке вызова.
* **Нет интерцепторов для in-memory провайдера.** У [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) нет команды и соединения, поэтому `AddInterceptor` влияет только на контексты с базой данных.
* **`CommandInitialized` и общий план-кэш.** Кэш планов общий на тип контекста и поток, поэтому для запроса, чей план уже построен другим контекстом, `CommandInitialized` поднимется только на том первом построении.

## См. также

* [Соединения и логирование](16-connections-and-logging.md)
* [Переиспользование запросов: кэш и `Prepare`](15-query-reuse.md)
* [Dependency injection](../getting-started/04-dependency-injection.md)
* [API reference](../advanced/api-reference.md)

---

Источник: `src/nextorm.core/Interceptors/IQueryInterceptor.cs`,
`src/nextorm.core/Interceptors/IConnectionInterceptor.cs`,
`src/nextorm.core/DataContext/QueryExecutor.cs`,
`src/nextorm.core/DataContext/DbConnectionManager.cs`,
`tests/nextorm.sqlite.tests/InterceptorTests.cs`.
