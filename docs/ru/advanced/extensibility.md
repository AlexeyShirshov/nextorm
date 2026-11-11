# Расширяемость

> Точки расширения позволяют вынести общие правила приложения в отдельный код: объявляйте правила данных на уровне приложения, отображайте пользовательские CLR-типы, создавайте повторно используемые операторы запросов и наблюдайте за выполнением. Эта страница объясняет, какую точку выбрать, кто и когда её вызывает, и приводит короткий рабочий пример для каждой. Внешняя сборка использует их без friend-доступа.

**Предварительные требования:** [Обзор провайдеров](../connecting-providers/overview.md) · [Краткий справочник API](api-reference.md)

## Зачем расширять nextorm

Обратитесь к точке расширения, когда вам нужно одно из следующего:

- **Правила данных на уровне приложения** — предикат, применяемый к каждому запросу по сущности (изоляция арендаторов, мягкое удаление, ограничение строк). Используйте глобальные фильтры запросов.
- **Хранение пользовательских CLR-типов и отображение** — `enum`, хранящийся как текст, строго типизированное значение, хранящееся как `decimal`, перевод идентификаторов в написание БД, CLR-метод, вызывающий функцию БД. Используйте преобразователи значений, соглашения об именовании и SQL-функции.
- **Повторно используемые операторы запросов без форка ядра** — новый оператор, меняющий генерируемый SQL (добавленный предикат, перестроенный источник строк), сохраняя при этом кэш планов, привязку параметров и провайдеронезависимость обычного запроса. Используйте компоненты запроса.
- **Инструментирование и провайдер-специфичный SQL** — наблюдение за командами и соединениями, либо переопределение диалекта провайдера и его нативного рендерера экстремальной строки. Используйте перехватчики и пользовательские диалекты.

Точки расширения делятся на три семейства:

- **Конфигурационные хуки** — декларативные, на контекст, запрос или свойство. Они меняют то, как обрабатываются имена, значения, фильтры и команды; некоторые добавляют предикаты в генерируемый SQL, но структуру SQL-запроса формирует ядро.
- **Структурные плагины запросов** — компоненты запроса. Они меняют структуру запроса; ни реестра, ни глобального состояния нет: компонент — это объект, прикреплённый к одному неизменяемому построителю.
- **Глубокая работа с диалектом** — переопределение диалекта провайдера (и, для PostgreSQL и ClickHouse, его нативного рендерера экстремальной строки) для формирования провайдер-специфичного SQL. Это самая глубокая точка и единственная, где пишется текст SQL.

`BaseExpressionVisitor` публичен, но это не зарегистрированный хук: реестра трансляторов и регистрации плагинов материализатора нет. Некоторые диалекты `sealed` (`SqliteDialect`, `SqlServerDialect`, `MariaDbDialect`); `MySqlDialect` намеренно не sealed, чтобы `MariaDbDialect` мог его переиспользовать. Публичные члены `SqlDialectBase` существуют для реализаций провайдеров; поддерживаемый внешний контракт покрывает рендерер экстремальной строки на PostgreSQL и ClickHouse.

## Карта публичных хуков

| Хук | Настройка | Кто вызывает | Когда | Раздел |
|---|---|---|---|---|
| `[SqlFunction]` | атрибут на методе/типе | транслятор выражений | при трансляции LINQ-выражения | [SQL-функции](#sql-функции-sqlfunction) |
| `INamingConvention` | `DataContextBuilder.UseNamingConvention` / `EntityBuilder.WithNamingConvention` | метаданные + SQL-строитель | разрешение имён таблиц/колонок | [Соглашения об именовании](#соглашения-об-именовании-inamingconvention) |
| `IPropertyValueConverter` / `ValueConverter<TModel,TProvider>` | атрибут или fluent `HasConversion` | путь чтения и путь записи/параметров | чтение и запись свойства | [Преобразователи значений](#преобразователи-значений-ipropertyvalueconverter--valueconvertertmodeltprovider) |
| `HasQueryFilter` | `EntityMetadataBuilder<T>` | планировщик запросов | каждый запрос по сущности (с учётом флагов обхода) | [Глобальные фильтры запросов](#глобальные-фильтры-запросов-hasqueryfilter) |
| `IQueryInterceptor` / `IConnectionInterceptor` | `DataContextBuilder.AddInterceptor` / `DataContext.AddInterceptor` | исполнитель команд и управление соединением | жизненный цикл команды и соединения | [Перехватчики](#перехватчики-iqueryinterceptor--iconnectioninterceptor) |
| `IQueryComponent` (+ дескриптор, необязательная форма) | `EntityBuilder.WithComponent` | планировщик + `SqlBuilder` | подготовка (возможно, несколько раз) и рендеринг (только промах) | [Расширение построения запросов компонентами запроса](#расширение-построения-запросов-компонентами-запроса) |
| `ISqlDialect` / `SqlDialectBase` (+ `IExtremeRowRenderer`) | подкласс контекста провайдера | SQL-строитель повсюду | каждый фрагмент SQL | [Продвинутое: пользовательские SQL-диалекты и рендереры](#продвинутое-пользовательские-sql-диалекты-и-рендереры) |

## Типы, используемые в примерах

В сниппетах ниже `ctx` — уже созданный SQL-контекст данных, а `connectionString` — его строка соединения; объявления и фрагменты использования показаны отдельно.

```csharp declarations
using NextORM.Core;
using NextORM.Sqlite;

public sealed class Player
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public bool IsActive { get; set; }
    public int Score { get; set; }
}

public enum Status { Unknown, Active, Closed }

public sealed class Order
{
    public long Id { get; set; }
    public Status Status { get; set; }
}
```

## Жизненный цикл SQL: подготовка, рендеринг и кэш

Структурные хуки (компоненты) участвуют в том же жизненном цикле, что и любой другой запрос. Его понимание предотвращает две частые ошибки: предположение, что `Prepare` выполняется раз на исполнение, и предположение, что `BuildQuery` выполняется при попадании в кэш.

1. **Присоединение** — `WithComponent` копирует построитель; компонент того же слота заменяется на месте.
2. **Prepare** — `QueryCommand.QueryPreparer` готовит команду. Для компонента `Prepare` может выполняться больше одного раза для одного и того же кэшированного плана: новая команда, подготовленная против прогретого кэша планов, повторно выполняет подготовку и может перестроить дескриптор, хотя сам план переиспользуется. Попадание в сырой кэш рецептов для компонента, реализующего `IQueryComponentShape`, может полностью пропустить `Prepare`.
3. **Дескриптор → ключ плана** — возвращённый дескриптор попадает в `QueryCommand.PreparedComponents`; ключ плана хэширует `StructuralHash` и сравнивает через `StructurallyEquals`. Два структурно равных дескриптора делят план только тогда, когда совпадает и остальная часть запроса; дескриптор — лишь часть ключа, а не весь ключ.
4. **BuildQuery** — только при построении плана (промах кэша): `SqlBuilder.MakeSelect` клонирует подготовленную команду, вызывает `BuildQuery` на клоне для каждого компонента и рендерит клон. Обработчик меняет одноразовую копию.
5. **Исполнение / тёплое попадание** — SQL сохранён в плане. Тёплое попадание переиспользует его: `BuildQuery` не вызывается. Захваченные значения и позиционные параметры перепривязываются к исходной команде. Повторный рендеринг для обновления параметров переиспользует форму, построенную на промахе, поэтому `BuildQuery` там тоже не выполняется.

Практически: вы никогда не вызываете `Prepare` и `BuildQuery` сами. `BuildQuery` компонента никогда не видит текст SQL; рендерингом владеет ядро.

## Конфигурационные хуки

### SQL-функции (`[SqlFunction]`)

Отображает CLR-метод на скалярную функцию БД. Тело-заглушка никогда не выполняется, поэтому `=> throw new NotSupportedException()` или `=> default!` достаточно. Атрибут применяется к методу или к объявляющему типу.

```csharp declarations
using NextORM.Core;

public static class Udf
{
    [SqlFunction("upper")]
    public static string ToUpper(string value) => throw new NotSupportedException();
}

```

```csharp example
var value = ctx.From<Player>()
    .Where(p => p.Id == 2)
    .Select(p => Udf.ToUpper(p.Name!))
    .First();
```

`[SqlFunction]` работает только внутри выражения запроса: вызов транслируется, а не выполняется, поэтому прямой вызов `Udf.ToUpper(...)` в обычном C# бросает исключение. Встроенные трансляции `string`/`Math`/`DateTime` выполняются первыми и не могут быть переопределены. nextorm только формирует вызов; функция должна уже существовать в БД. Полный контракт и SQL-вывод: [Пользовательские функции](../guide/12-user-defined-functions.md).

### Соглашения об именовании (`INamingConvention`)

`ColumnName(string)` и `TableName(string, bool)` (логическое значение говорит, пришло ли CLR-имя из интерфейса). Настраиваются глобально или на запрос; явные имена `[SqlTable]`/`[Column]`/fluent берутся дословно.

```csharp declarations
using NextORM.Core;

public sealed class PrefixedNamingConvention : INamingConvention
{
    public string TableName(string clrName, bool isInterface)
        => "tbl_" + (isInterface && clrName.StartsWith('I') ? clrName[1..] : clrName);

    public string ColumnName(string propertyName) => "col_" + propertyName;
}

```

```csharp example
var builder = new DataContextBuilder()
    .UseNamingConvention(new PrefixedNamingConvention())
    .UseSqlite(connectionString);
```

См. [Сущности и метаданные](../getting-started/03-entities-and-metadata.md#соглашения-об-именовании).

### Преобразователи значений (`IPropertyValueConverter` / `ValueConverter<TModel,TProvider>`)

Реализуют `ProviderType`, `ConvertToProvider`, `ConvertFromProvider` и `ConvertsNulls` (или наследуются от `ValueConverter<TModel,TProvider>`). Применяются через атрибут или fluent `HasConversion`.

```csharp declarations
using NextORM.Core;

public sealed class StatusConverter : ValueConverter<Status, string>
{
    public override string? ConvertToProvider(Status model) => model.ToString();
    public override Status ConvertFromProvider(string? provider) =>
        Enum.Parse<Status>(provider!);
}

```

```csharp example
// fluent:
ctx.From<Order>(m => m.Property(x => x.Status).HasConversion(new StatusConverter()));
```

`ConvertToProvider` выполняется на путях записи и параметров (модель → провайдер, включая параметры запроса); `ConvertFromProvider` — при чтении строк (провайдер → модель). Если `ConvertsNulls` равно false, базовый `NULL` отображается в `null` без вызова преобразователя, поэтому nullable-свойство остаётся null. Подробности: [Преобразователи значений и JSON-колонки](../infrastructure/03-value-converters.md).

### Глобальные фильтры запросов (`HasQueryFilter`)

Присоединяет предикат к типу сущности; он применяется к каждому запросу по этой сущности. Перегрузки принимают `Expression<Func<T,bool>>`, `Expression<Func<T,IDataContext,bool>>`, именованные варианты и делегаты-трансформеры построителя.

```csharp example
ctx.From<Player>(m => m.HasQueryFilter(p => p.IsActive));
```

Фильтры применяются при обычной подготовке запроса. Флаги обхода и полный список перегрузок описаны в [Глобальные фильтры запросов](query-filters.md).

### Перехватчики (`IQueryInterceptor` / `IConnectionInterceptor`)

Наблюдают за выполнением команд и открытием соединений без наследования от `DataContext`. Регистрируются на построителе или на экземпляре контекста.

```csharp declarations
using System.Data.Common;
using NextORM.Core;

public sealed class TimingInterceptor : IQueryInterceptor
{
    public void CommandExecuted(CommandEventData e, DbCommand cmd, TimeSpan elapsed)
        => Console.WriteLine($"{cmd.CommandText} took {elapsed}");
}

public sealed class AuditConnectionInterceptor : IConnectionInterceptor
{
    public void ConnectionOpened(ConnectionEventData e)
        => Console.WriteLine("Database connection opened.");
}

```

```csharp example
var builder = new DataContextBuilder()
    .UseSqlite(connectionString)
    .AddInterceptor(new TimingInterceptor())
    .AddInterceptor(new AuditConnectionInterceptor());
```

Колбэки вызываются в порядке регистрации: `CommandInitialized` → `CommandExecuting` → `CommandExecuted` или `CommandFailed`; `ConnectionOpening` → `ConnectionOpened`. Оба интерфейса используют методы интерфейса по умолчанию, поэтому реализуйте только нужные колбэки. Провайдер in-memory их не вызывает. Регистрация по типу (`AddInterceptor<T>()`, `AddInterceptor(Type)`) разрешает перехватчик из DI-области, когда построитель поддержан DI; на автономном построителе она падает при создании контекста. Подробности: [Перехватчики](../infrastructure/02-interceptors.md).

## Расширение построения запросов компонентами запроса

Компонент запроса позволяет стороннему пакету добавить структурное поведение в построение запроса без изменения ядра под каждый оператор, без friend-доступа, без глобального реестра и без сырого SQL. Он несёт собственную идентичность слота, логику подготовки/валидации и обработчик структурного построения, и присоединяется через [`WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent)).

```csharp example
var rows = ctx.From<Player>()
    .ActiveOnly()          // обычный метод-расширение из пакета
    .ToList();
```

### Кто что реализует и кто это вызывает

| Тип | Вы | Ядро |
|---|---|---|
| `IQueryComponent` | Один объект на экземпляр оператора. | Читает `Slot` при присоединении и сравнении ключа плана; читает `Contribution`; вызывает `Prepare` на подготовке; вызывает `BuildQuery` на рендеринге. |
| `IQueryComponentDescriptor` | Возвращаете из `Prepare`; неизменяем, без ссылок на изменяемое состояние компонента. | Хранит в `QueryCommand.PreparedComponents`; хэширует `StructuralHash`; сравнивает через `StructurallyEquals`; сбрасывает при сбросе подготовки. |
| `QueryComponentContribution` | Объявляете один раз: `AdditivePredicate`, `ExclusiveRowSource` или `ExclusiveGrouping`. | Отвергает второй слот `ExclusiveRowSource` и второго различного владельца `ExclusiveGrouping` на подготовке, а два эксклюзивных режима считает взаимоисключающими. Аддитивные предикаты **не** объединяются автоматически: компонент должен сделать AND сам (см. правила). |
| `IQueryComponentPrepareContext` | Используете в `Prepare`. | Создаёт над подготавливаемой командой. |
| `IQueryComponentBuildContext` | Используете в `BuildQuery`. | Создаёт над перестроенной командой. |
| `QueryComponentContext` | Обычно не трогаете; публичен для прямых тестов. | Реализует оба контекста; получает `ExpressionComparer` из команды. |
| `IQueryComponentShape` | Необязательно: сообщает скалярное состояние и селекторы для сырого кэша рецептов. | Сравнивает `ShapeState` через `Equals`, селекторы — компаратором выражений. Без интерфейса — обычный путь подготовки. |
| `WithComponent` | Вызываете из своего метода-расширения. | Копирует построитель; заменяет компонент того же слота на месте. |
| `QueryCommand.Components` / `PreparedComponents` | Только чтение, для диагностики. | Владеет, публикует и очищает ядро. |

### Когда происходит каждый вызов

См. [Жизненный цикл SQL](#жизненный-цикл-sql-подготовка-рендеринг-и-кэш). Вкратце: присоединение (без валидации), `Prepare` (возможно, несколько раз для одного кэшированного плана), дескриптор → ключ плана, `BuildQuery` (только промах), исполнение/тёплое попадание (без `BuildQuery`).

### Правила реализации

- **Подготовка/валидация → неизменяемый дескриптор.** Отвергайте неподдерживаемые комбинации исключением; возвращайте дескриптор со слотом, конкретным типом компонента, вкладом, строковым `Discriminator`, `StructuralHash` по всему состоянию и `StructurallyEquals`. Дескриптор разделяется кэшированным планом, поэтому не должен ссылаться на изменяемое состояние компонента. Захваченные значения должны привязываться как параметры; любое состояние, влияющее на SQL, должно входить в `StructuralHash`, `StructurallyEquals` и (где полезно) `Discriminator` — ядро не сравнивает захваченные значения и состояние времени выполнения «магически».
- **Эксклюзивное перестроение на промахе.** Относится к компоненту `ExclusiveRowSource`: `BuildQuery` получает команду через `IQueryComponentBuildContext` и перестраивает из примитивов ядра: производный источник (`FromExpression`), исходный список выборки плюс скрытая вспомогательная колонка (`SelectExpression`, `SetComponentSelectList`), необязательный переписанный предикат (`SetComponentPreparedCondition`), очищенная сортировка. Текст SQL он не рендерит. Аддитивный компонент вызывает только `SetComponentPreparedCondition`.
- **Группировка объявляется, а не переписывается.** Компонент группировки возвращает `IQueryPreparationDescriptor` с `QueryPreparationContribution`: упорядоченные лямбды ключей группировки, `QueryPreparationHavingMode` (`Unchanged`, `Clear` или `Replace`) с необязательной лямбдой HAVING, необязательный предикат источника и `RequireDistinct`. Ядро применяет этот вклад к отдельному эффективному состоянию подготовки до подготовки колонок и группировки и никогда не мутирует сырые поля [`QueryCommand`](xref:NextORM.Core.QueryCommand); активный вклад с пустым вектором ключей — это явная глобальная агрегация. Вклад `Replace` несёт только *добавляемое* условие — ядро ровно один раз объединяет с ним сырой `Having` источника через AND.
- **Аддитивные предикаты объединяете вы.** Ядро **не** делает AND автоматически. Возьмите существующий `LambdaExpression`, переиспользуйте его параметр (или создайте `Expression.Parameter(typeof(Player), "p")`), объедините тела через AND и оберните результат в новое лямбда-выражение: `SetComponentPreparedCondition(Expression.Lambda(Expression.AndAlso(existingBody, predicateBody), param))`. Никогда не пишите `Expression.AndAlso(existingLambda, predicateLambda)`.
- **Идентичность слота.** `Slot` — стабильная идентичность логического оператора. Присоединение компонента, чей слот уже присутствует, заменяет его на месте; построитель/команда никогда не вкладывают два компонента одного слота. `Min` и `Max` одного семейства делят слот. Слотов `ExclusiveRowSource` и владельцев `ExclusiveGrouping` может быть не больше одного; второй различный эксклюзивный слот или владелец группировки отвергается на подготовке с `BuildSqlCommandException`, а владелец группировки несовместим с перестроением источника строк.
- **Безопасность псевдонима скрытой колонки.** Вспомогательная колонка не должна ни протекать, ни сталкиваться. Автор компонента обязан выбрать уникальный псевдоним (например, вывести его из метаданных цели через `DataContextCache.Metadata`), держать его вне внешнего списка выборки и читать обратно через `SqlFunctions.Column<T>`. Ядро не гарантирует автоматически, что произвольная пользовательская колонка не столкнётся; исключайте псевдоним из внешней проекции и проверяйте метаданные.
- **Без поддержки in-memory, если не добавите сами.** SQL-only компонент отвергает `InMemoryDataContext` в `Prepare` через `NotSupportedException`; он не должен молча деградировать.
- **Форма (необязательно).** Реализуйте `IQueryComponentShape`, чтобы сырой кэш рецептов обслуживал ваш компонент: сообщите неизменяемое скалярное состояние в `ShapeState` (со стабильными `Equals`/`GetHashCode`) и селекторы через `ShapeSelectorCount`/`GetShapeSelector`. Два компонента с равным состоянием формы и структурно равными селекторами должны рендерить одинаковое утверждение. Без интерфейса — обычный путь подготовки.

### Пример аддитивного компонента

Компонент без состояния, добавляющий постоянное бизнес-правило (`IsActive == true`), объединяя его с любым уже имеющимся предикатом через AND. Дескриптор — запись без изменяемого состояния. Компонент также реализует `IQueryComponentShape` для сырого кэша рецептов. Пример намеренно ограничен обычным одиночным `From<Player>().Where(...).ActiveOnly()` до соединений и проекций; компонент, перестраивающий более сложные запросы, требует собственной проверки и не должен предполагать универсальную совместимость формы.

```csharp declarations
using System.Linq.Expressions;
using NextORM.Core;

internal sealed class ActiveOnlyComponent : IQueryComponent, IQueryComponentShape
{
    public Type Slot => typeof(ActiveOnlyComponent);
    public QueryComponentContribution Contribution =>
        QueryComponentContribution.AdditivePredicate;

    public object? ShapeState => typeof(ActiveOnlyComponent);
    public int ShapeSelectorCount => 0;
    public LambdaExpression GetShapeSelector(int index) =>
        throw new ArgumentOutOfRangeException(nameof(index));

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException("ActiveOnly работает только в SQL.");
        return new ActiveOnlyDescriptor();
    }

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var existing = cmd.GetComponentPreparedCondition();
        ParameterExpression param;
        Expression? existingBody;
        if (existing is null)
        {
            param = Expression.Parameter(typeof(Player), "p");
            existingBody = null;
        }
        else if (existing is LambdaExpression lambda && lambda.Parameters.Count > 0)
        {
            param = lambda.Parameters[0];
            existingBody = lambda.Body;
        }
        else
        {
            throw new InvalidOperationException("Existing prepared condition must be a lambda with a parameter.");
        }

        var activeBody = Expression.Equal(
            Expression.Property(param, nameof(Player.IsActive)),
            Expression.Constant(true));
        var combined = existingBody is null ? activeBody : Expression.AndAlso(existingBody, activeBody);
        cmd.SetComponentPreparedCondition(Expression.Lambda(combined, param));
    }
}
```

```csharp declarations
internal sealed record ActiveOnlyDescriptor : IQueryComponentDescriptor
{
    public Type Slot => typeof(ActiveOnlyComponent);
    public Type ComponentType => typeof(ActiveOnlyComponent);
    public QueryComponentContribution Contribution =>
        QueryComponentContribution.AdditivePredicate;
    public string Discriminator => "active-only";
    public int StructuralHash => HashCode.Combine(Slot, ComponentType, Contribution, Discriminator);
    public bool StructurallyEquals(IQueryComponentDescriptor? other) =>
        other is ActiveOnlyDescriptor;
}

public static class ActiveOnlyExtensions
{
    public static EntityBuilder<Player> ActiveOnly(this EntityBuilder<Player> source)
        => source.WithComponent(new ActiveOnlyComponent());
}
```

При попадании в прогретый кэш планов `BuildQuery` не вызывается; `Prepare` может потребоваться новой команде. При промахе `IsActive = 1` объединяется через AND с любым предикатом, который уже несёт запрос.

### Эксклюзивные компоненты

`ExclusiveRowSource` — это режим вклада, а не отдельная точка расширения: он объявляет, что компонент перестраивает источник строк, а не добавляет предикат. Такой слот может быть не больше одного. Готовый пример поставляется в пакете расширений — `SelectWhereMax`/`SelectWhereMin`:

```csharp example
using NextORM.Extensions;

var rows = ctx.From<Player>()
    .SelectWhereMax(p => p.Score)
    .ToList();
```

Пакет реализует полное перестроение (производный источник, вспомогательная колонка, подготовленный предикат, очищенная сортировка) на публичном контракте компонентов. См. [Пакет расширений](extensions-package.md) и [Нативные стратегии выбора экстремальной строки](select-where-extrema-native.md).

`ExclusiveGrouping` — соседний эксклюзивный режим: компонент владеет группировкой команды и вносит ключи и `HAVING` декларативно, а не перестраивает строки. Готовый пример из того же пакета — `SelectGroup`/`SelectGroupHaving`:

```csharp example
using NextORM.Core;
using NextORM.Extensions;

var rows = ctx.From<Order>()
    .SelectGroup(o => new { o.CustomerId, Total = SqlFunctions.Sql.sum(o.Amount) })
    .ToList();
```

Правила вывода и границы отказа — в разделе [«Группировка, выведенная из проекции»](extensions-package.md) пакета расширений.

## Продвинутое: пользовательские SQL-диалекты и рендереры

`ISqlDialect` предоставляет возможности; `SqlDialectBase` даёт виртуальные значения по умолчанию. Диалекты PostgreSQL и ClickHouse намеренно не `sealed` и предоставляют публичные конструкторы, общий синглтон `Instance` и виртуальный `ExtremeRowRenderer`. Диалекты SQLite, SQL Server и MariaDB — `sealed`.

### Установка пользовательского диалекта

Переключателя `DataContextBuilder.UseDialect` нет. `DataContext.Dialect` абстрактен; каждый провайдерный контекст переопределяет его. Унаследуйте провайдерный контекст и переопределите `Dialect`:

```csharp declarations
using NextORM.Core;
using NextORM.Postgres;

public sealed class MyPostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => MyRenderer.Instance;
}

public sealed class MyPostgresDataContext : PostgresDataContext
{
    private readonly ISqlDialect _dialect = new MyPostgresDialect();

    public MyPostgresDataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder) { }

    public override ISqlDialect Dialect => _dialect;
}

```

```csharp example
using var ctx = new MyPostgresDataContext(connectionString, new DataContextBuilder());
```

Кэш планов ключуется по типу контекста, поэтому заводите отдельный подкласс `DataContext` на каждый диалект. Та же форма работает для `MyClickHouseDataContext : ClickHouseDataContext`.

### Нативный рендерер экстремальной строки (специализированно, устаревшие операторы)

`IExtremeRowRenderer` — хук нативной стратегии для **устаревших** операторов `SelectWhereMaxOld`/`SelectWhereMinOld`. Рекомендуемые `SelectWhereMax`/`SelectWhereMin` из `nextorm.extensions` построены на компонентах запроса и **не** вызывают этот рендерер. Хук остаётся поддерживаемым для legacy-пути.

`CanRender(ExtremeRowDescription)` — решение без побочных эффектов, принимаемое до сборки любого SQL; только положительный ответ ведёт к `Render(ExtremeRowRenderRequest)`, возвращающему источник строки-победителя. Рендерер `null` (или `CanRender == false`) сохраняет переносимый SQL с оконной функцией. У `Render` нет позднего отката: ошибка рендеринга распространяется. Колбэки происходят во время генерации SQL, а не при каждом переиспользовании кэшированного SQL; повторный рендеринг для обновления параметров может вызвать их снова. Встроенный нативный путь доступен только для формы `ExtremeRowTies.One` по отображённой форме сущности; остальные режимы обработки равных значений остаются на переносимом SQL с оконной функцией.

Декоратор, добавляющий инструментирование без написания SQL:

```csharp declarations
using System;
using NextORM.Core;
using NextORM.Postgres;

public sealed class MyRenderer : IExtremeRowRenderer
{
    public static readonly MyRenderer Instance = new();

    private readonly IExtremeRowRenderer? _inner =
        PostgresDialect.Instance.ExtremeRowRenderer;

    public bool CanRender(ExtremeRowDescription description)
        => _inner?.CanRender(description) ?? false;

    public string Render(ExtremeRowRenderRequest request)
    {
        if (_inner is null)
            throw new NotSupportedException("Внутренний рендерер отсутствует.");
        Console.WriteLine("Нативный рендеринг экстремальной строки.");
        return _inner.Render(request);
    }
}
```

Три DTO (`ExtremeRowRenderColumn`, `ExtremeRowDescription`, `ExtremeRowRenderRequest`) несут только факты формы (CLR-тип, nullable, признаки прямой отображённости и конвертера, подготовленный источник и алиасы), но никогда — выражение или контекст построения. Они предоставляют публичные конструкторы, принимающие готовые коллекции для сборок без friend-доступа; коллекции заимствуются, не копируются. Встроенные рендереры (`PostgresExtremeRowRenderer`, `ClickHouseExtremeRowRenderer`) `internal` и в контракт не входят.

### Прочие возможности диалекта

`SqlDialectBase` предоставляет множество виртуальных членов (`Iif`, `MultiIf`, `Tuple`, `Lock`, `DistinctOn`, `LimitBy`, `IndexHints`, `StringSplit`, `ArrayJoin`, `DateConversion`, `Pivot`, рендереры агрегатов, ...). Они публичны для реализаций провайдеров; поддерживаемый внешний контракт покрывает рендерер экстремальной строки на PostgreSQL и ClickHouse. Переопределяйте их только при создании диалекта для нового провайдера.

## Ограничения

- Нет публичного реестра трансляторов и нет регистрации плагинов материализатора.
- Запечатанные диалекты (`SqliteDialect`, `SqlServerDialect`, `MariaDbDialect`) нельзя унаследовать; `MySqlDialect` — точка расширения для MariaDB.
- Компоненты не имеют поддержки in-memory, если не реализовать её явно; эксклюзивные компоненты-источники строк наследуют проверку совместимости модификаторов.
- У `Render` нет позднего отката к переносимому пути.
- Перехватчики не вызываются провайдером in-memory.
- Кэш планов ключуется по типу контекста; один диалект на подкласс контекста.

## См. также

- [Нативные стратегии выбора экстремальной строки](select-where-extrema-native.md)
- [Пакет расширений](extensions-package.md)
- [Пользовательские функции](../guide/12-user-defined-functions.md)
- [Сущности и метаданные](../getting-started/03-entities-and-metadata.md)
- [Преобразователи значений и JSON-колонки](../infrastructure/03-value-converters.md)
- [Перехватчики](../infrastructure/02-interceptors.md)
- [Глобальные фильтры запросов](query-filters.md)
- [Краткий справочник API](api-reference.md)
- [Обзор провайдеров](../connecting-providers/overview.md)
