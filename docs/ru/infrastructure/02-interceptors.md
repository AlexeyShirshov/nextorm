# Интерцепторы

> Наблюдайте за жизненным циклом выполнения команд и соединения контекста без наследования от `DataContext` и без логгера.

**Предварительные требования:** [Соединения](01-connections.md) · [Переиспользование запросов](../engine/03-plan-cache-and-reuse.md) · [Внедрение зависимостей](../getting-started/04-dependency-injection.md).

## Обзор

Интерцептор — это небольшой объект, зарегистрированный на контексте. Он получает обратные вызовы до и после работы контекста. nextorm поставляет два интерфейса интерцепторов.

В таблице указано, за чем наблюдает каждый интерфейс и какие обратные вызовы он получает:

| Интерфейс | Что наблюдает | Обратные вызовы |
|---|---|---|
| [`IQueryInterceptor`](xref:NextORM.Core.IQueryInterceptor) | выполнение команд | `CommandInitialized`, `CommandExecuting`, `CommandExecuted`, `CommandFailed` |
| [`IConnectionInterceptor`](xref:NextORM.Core.IConnectionInterceptor) | открытие соединения | `ConnectionOpening`, `ConnectionOpened` |

Каждый обратный вызов получает данные события ([`CommandEventData`](xref:NextORM.Core.CommandEventData)
или [`ConnectionEventData`](xref:NextORM.Core.ConnectionEventData)) и задействованный объект ADO.NET
(`DbCommand` или `DbConnection`). Контекст вызывает события при выполнении команды или открытии
соединения. Сама команда события не вызывает: `DbCommand` — структура данных, которая не может
логировать или профилировать себя.

Интерцепторы используют для замера времени и сбора метрик, структурированного журналирования SQL и
настройки параметров команды, специфичных для провайдера, например таймаута.

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

### Регистрация интерцептора через dependency injection

Когда контейнер создаёт [`DataContextBuilder`](xref:NextORM.Core.DataContextBuilder) (options-путь),
регистрация **по типу** или **обобщённая регистрация** заставляет builder разрешать интерцептор из
своей области видимости:

```csharp
services.AddScoped<TimingInterceptor>();

services.AddNextOrmContext(builder => builder
    .UseSqlite(connectionString)
    .AddInterceptor<TimingInterceptor>()); // или .AddInterceptor(typeof(TimingInterceptor))
```

`AddInterceptor<T>()` и `AddInterceptor(Type)` разрешают сервис через `GetRequiredService` один раз
при создании контекста, из области, которая создала builder. Времена жизни контейнера сохраняются:
scoped-интерцептор переиспользуется внутри области, но отличается между областями; singleton общий;
transient разрешается для каждого контекста. Контейнер владеет интерцептором и освобождает его.
Builder никогда не освобождает экземпляр, разрешённый из контейнера.

Разрешённый объект подключается к каждому интерфейсу интерцептора, который он реализует. Поэтому
класс, реализующий и [`IQueryInterceptor`](xref:NextORM.Core.IQueryInterceptor), и
[`IConnectionInterceptor`](xref:NextORM.Core.IConnectionInterceptor), наблюдается обоими конвейерами.
Тип должен быть закрытым ссылочным типом, реализующим хотя бы один из этих интерфейсов. Открытый
generic-тип или тип-значение отклоняется сразу с `ArgumentException`, а `null` — с
`ArgumentNullException`.

Регистрации дедуплицируются по точному ключу: `instance.GetType()` для экземпляра и переданный
`Type` для регистрации типа. Первая регистрация побеждает, в том числе при смешении этих вариантов.
Поэтому повторная регистрация того же типа разрешается и вызывается только один раз.

Регистрация типа требует builder, созданный через dependency injection. Для отдельного
`new DataContextBuilder()` вызов `CreateDataContext()` бросает `InvalidOperationException`.
Если сервис не зарегистрирован, контейнер бросает собственную ошибку `GetRequiredService`. Оба сбоя
происходят **до** вызова фабрики контекста, поэтому частично созданный контекст не появляется.

Мост EF Core (`CreateNextOrmContext`) создаёт контекст из отдельного builder без доступа к контейнеру.
Поэтому регистрация интерцептора по типу при создании контекста приводит к тому же
`InvalidOperationException`. Вместо неё зарегистрируйте экземпляр:
`configure: builder => builder.AddInterceptor(new TimingInterceptor())`.

Контекст, созданный из builder, может добавить интерцепторы на время своей жизни:

```csharp
using var ctx = builder.CreateDataContext();
ctx.AddInterceptor(new AuditInterceptor());
```

`AddInterceptor` доступен на [`DataContext`](xref:NextORM.Core.DataContext), а не на фасаде [`IDataContext`](xref:NextORM.Core.IDataContext), поэтому при работе через интерфейс нужно привести контекст к типу.

## Интерцептор запросов

Пример записывает SQL перед выполнением, затраченное время после успеха и тип исключения после ошибки:

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

Пример записывает тип соединения и источник данных при открытии соединения:

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
* **Потокобезопасность**: если один экземпляр интерцептора зарегистрирован в независимых контекстах, его обработчики могут вызываться параллельно. На каждом контексте и соединении допускается только одна активная операция. Регистрация использует снимок списка интерцепторов и не меняет обработчики, уже выбранные для события.
* **Нулевая цена без интерцепторов**: если интерцепторы не зарегистрированы, путь исполнения делает ветвление и вызывает провайдер напрямую — никакие объекты событий, отметки времени, делегаты и async-машины состояний не аллоцируются.

## Ограничения

* **Не меняйте `CommandText`.** NextORM может повторно использовать `DbCommand` на том же соединении. Изменение текста может нарушить последующие выполнения. Используйте `CommandInitialized` для настроек, которые не меняют SQL, например таймаута команды.
* **Трансляция исключений вне области.** Exception-интерцептора нет: чтобы отобразить исключение провайдера на доменный тип, оберните явный вызов терминала (или метод репозитория) в `try`/`catch`. Единственный пробел — отложенное/стриминговое выполнение, где ошибка всплывает во время перечисления `IAsyncEnumerable`, а не в точке вызова.
* **Нет интерцепторов для in-memory провайдера.** У [`InMemoryDataContext`](xref:NextORM.Core.InMemoryDataContext) нет команды и соединения, поэтому `AddInterceptor` влияет только на контексты с базой данных.
* **`CommandInitialized` и общий кэш планов.** Контекст выполнения вызывает событие при создании `DbCommand`, в том числе при первом использовании на соединении плана, уже построенного на другом соединении. При повторном использовании команды на том же соединении событие не вызывается. `CommandExecuting` срабатывает при каждом выполнении и получает команду со значениями параметров этого вызова.

## См. также

* [Соединения](01-connections.md)
* [Переиспользование запросов: кэш и `PrepareBuffered`](../engine/03-plan-cache-and-reuse.md)
* [Внедрение зависимостей](../getting-started/04-dependency-injection.md)
* [Справочник API](../advanced/api-reference.md)

---

Источник: `src/nextorm.core/Interceptors/IQueryInterceptor.cs`,
`src/nextorm.core/Interceptors/IConnectionInterceptor.cs`,
`src/nextorm.core/DataContext/QueryExecutor.cs`,
`src/nextorm.core/DataContext/Infrastructure/DbConnectionManager.cs`,
`tests/nextorm.sqlite.tests/Interceptors/InterceptorTests.cs`.
