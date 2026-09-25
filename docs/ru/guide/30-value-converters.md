# Конвертеры значений и JSON-колонки

**Конвертер значения** отображает свойство между его CLR-моделью и представлением, которое реально
хранится в колонке. Типичные случаи: `enum`, хранимый как текст, `DateTimeOffset` в строке
заданного формата, строго типизированное значение (`Money`) как `decimal` или произвольный объект,
хранимый как JSON.

## Объявление конвертера

Унаследуйтесь от `ValueConverter<TModel, TProvider>` и переопределите два типизированных
преобразования. Движок применяет их автоматически и при чтении, и при записи, а также кэширует
типизированный инвокер, чтобы value-типы не упаковывались на каждую строку.

```csharp
public enum Status { Unknown, Active, Closed }

public sealed class StatusConverter : ValueConverter<Status, string>
{
    public override string? ConvertToProvider(Status model) => model.ToString();
    public override Status ConvertFromProvider(string? provider) => Enum.Parse<Status>(provider!);
}
```

Подключение атрибутом:

```csharp
[SqlTable("orders")]
public sealed class Order
{
    public long Id { get; set; }

    [ValueConverter(typeof(StatusConverter))]
    public Status Status { get; set; }
}
```

или через fluent-конфигурацию:

```csharp
ctx.From<Order>(b => b
    .Table("orders")
    .Property(x => x.Status).HasConversion(new StatusConverter()));
```

`HasConversion<TModel, TProvider>` принимает либо экземпляр `ValueConverter<TModel, TProvider>`,
либо пару выражений преобразования:

```csharp
b.Property(x => x.Status).HasConversion<Status, string>(
    status => status.ToString(),
    text => Enum.Parse<Status>(text));
```

## Встроенный конвертер enum

Случай enum-as-string достаточно частый, чтобы поставляться готовым конвертером:
`EnumToStringConverter<TEnum>` хранит имя перечисления (`Enum.ToString()`) и разбирает его обратно
через `Enum.Parse<TEnum>`, поэтому заменяет написанный выше `StatusConverter`. Подключается любой из
форм:

```csharp
[ValueConverter(typeof(EnumToStringConverter<Status>))]
public Status Status { get; set; }
```

```csharp
b.Property(x => x.Status).HasConversion(new EnumToStringConverter<Status>());
```

Он работает и для nullable-свойства (`Status?`): SQL `NULL` по-прежнему отображается в `null` без
вызова конвертера. Пишите собственный конвертер, если нужно другое представление провайдера
(например, номер перечисления) или разбор без учёта регистра.

## Прямая реализация интерфейса

`ValueConverter<TModel, TProvider>` реализует `IPropertyValueConverter`, и именно этот интерфейс
использует движок. Наследование от базового класса — обычный путь, но `IPropertyValueConverter`
можно реализовать напрямую, когда тип модели неизвестен на этапе компиляции (например, конвертер
строится из метаданных времени выполнения) или когда один экземпляр конвертера переиспользуется для
нескольких свойств с разными CLR-типами:

```csharp
public sealed class InvariantStringConverter : IPropertyValueConverter
{
    public Type ProviderType => typeof(string);

    public bool ConvertsNulls => false;

    public object? ConvertToProvider(object? model)
        => model is null ? null : Convert.ToString(model, CultureInfo.InvariantCulture);

    public object? ConvertFromProvider(object? provider)
        => provider;
}
```

`ProviderType` — это представление, которое хранит колонка: движок использует его, чтобы выбрать
типизированный аксессор чтения и типизировать привязываемый параметр записи. `ConvertsNulls` имеет
тот же смысл, что и у базового класса (см. «Семантика NULL»).

Прямая реализация подключается так же, как типизированная, любой из форм:

```csharp
ctx.From<Order>(b => b
    .Table("orders")
    .Property(x => x.Payload).HasConversion(new InvariantStringConverter()));
```

```csharp
[ValueConverter(typeof(InvariantStringConverter))]
public object? Payload { get; set; }
```

Относительно базового класса вы теряете две вещи: значения упаковываются на каждой строке (нет
кэшированного типизированного инвокера), и маппинг не может проверить, что модель конвертера
совпадает со свойством, — эта проверка опирается на базовый `ValueConverter<TModel, TProvider>`.
Несовпадение тогда всплывает как ошибка приведения при первом исполнении, а не при построении
маппинга.

## JSON-колонки

`[JsonColumn]` хранит любой сериализуемый CLR-объект как JSON. **Форма хранения** выбирается
диалектом:

- `JsonColumnStorage.Auto` (по умолчанию) — нативный JSON-тип, где он есть (PostgreSQL `jsonb`),
  иначе текстовая колонка;
- `JsonColumnStorage.Native` — принудительно `jsonb` (только PostgreSQL);
- `JsonColumnStorage.Text` — принудительно текстовая колонка.

```csharp
public sealed class Address
{
    public string? City { get; set; }
    public List<string> Lines { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}

[SqlTable("customers")]
public sealed class Customer
{
    public long Id { get; set; }

    [JsonColumn]
    public Address Address { get; set; } = new();
}
```

Fluent-форма добавляет настройки сериализатора:

```csharp
b.Property(x => x.Address).JsonColumn(o =>
{
    o.Storage = JsonColumnStorage.Text;
    o.Options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
});
```

Объект настроек хранится по ссылке, и его нельзя менять после построения маппинга. `JsonTypeInfo<T>`/source-gen
для Native AOT/trimming — вне области; используйте сериализацию через рефлексию.

Вставка сериализует отображаемый объект в документ, который хранит колонка. Члены вроде
`DateTimeOffset` конвертера не требуют — `System.Text.Json` пишет их как ISO 8601:

```csharp
ctx.InsertInto<Customer>()
    .Values(new Customer
    {
        Id = 1,
        Address = new Address
        {
            City = "Berlin",
            Lines = ["a", "b"],
            UpdatedAt = new DateTimeOffset(2026, 9, 25, 12, 34, 56, TimeSpan.Zero),
        },
    })
    .Insert();
```

```sql
insert into customers (id, address) values (@p0, @p1)
```

`@p1` несёт сериализованный документ (`JsonElement` в нативной `jsonb`-колонке, `string` в текстовой):

```json
{"City":"Berlin","Lines":["a","b"],"UpdatedAt":"2026-09-25T12:34:56+00:00"}
```

Имена свойств определяются настройками сериализатора; по умолчанию — CLR-имя (`City`), поэтому для
camelCase задайте `JsonNamingPolicy.CamelCase`. Значение JSON в списке `SET` конвертируется так же.

Чтение — обратный процесс: колонка планируется как любая другая, а документ разворачивается обратно
на клиенте.

```csharp
var customer = ctx.From<Customer>().Where(x => x.Id == 1).ToList().Single();
// customer.Address.City      == "Berlin"
// customer.Address.Lines     == ["a", "b"]
// customer.Address.UpdatedAt == 2026-09-25T12:34:56+00:00
```

```sql
select id, address from customers
 where id = 1
```

На клиенте движок читает колонку как представление провайдера (`JsonElement` в нативной `jsonb`-колонке,
`string` в текстовой) и выполняет `ConvertFromProvider` конвертера, то есть
`JsonElement.Deserialize<Address>()` или `JsonSerializer.Deserialize<Address>(text)`. `System.Text.Json`
восстанавливает граф и превращает `"UpdatedAt"` из ISO 8601 обратно в `DateTimeOffset` — снова без
конвертера и ручного разбора.

### JSON в запросах

Свойство с JSON-маппингом планируется как любая конвертируемая колонка, поэтому работает и в
проекциях, и в сравнениях. Скалярная проекция материализует модель, а не представление провайдера:

```csharp
var addresses = ctx.From<Customer>().Select(x => x.Address).ToList();
```

Константа в сравнении с JSON-колонкой сериализуется в представление провайдера до биндинга параметра,
поэтому предикат сравнивается с хранимым документом (равенство документов в нативной `jsonb`-колонке,
равенство строк в текстовой):

```csharp
var berlin = new Address { City = "Berlin", Lines = [], UpdatedAt = DateTimeOffset.UnixEpoch };
ctx.From<Customer>().Where(x => x.Address == berlin).ToList();
```

```sql
select id, address from customers
 where address = @p0
```

`@p0` несёт сериализованный `Address` (`JsonElement` в PostgreSQL по умолчанию, JSON-строка в
текстовой колонке). Та же конвертация применяется внутри анонимной проекции
(`Select(x => new { x.Address })`). Сравнение идёт по сериализованному представлению, поэтому совпадает,
когда хранимый документ равен сериализованной константе — то есть настройки сериализатора должны быть
стабильными.

## Матрица провайдеров

Конвертер выполняется в приложении, поэтому не зависит от провайдера; различается лишь
представление, которое должно читаться и биндироваться.

| Провайдер | Нативный JSON | Текст | enum-as-string |
|---|---|---|---|
| PostgreSQL | `jsonb` (чтение/запись через `JsonElement`) | `text` | `text` |
| SQL Server | — (`nvarchar(max)`) | `nvarchar(max)` | `nvarchar` |
| MySQL / MariaDB | — (`longtext`) | `longtext` | `longtext` |
| SQLite | — (`TEXT`) | `TEXT` | `TEXT` |
| ClickHouse | — (`String`) | `String` | `String` |
| In-memory | CLR-значение | CLR-значение | CLR-значение |

## Семантика NULL

По умолчанию SQL `NULL` отображается в `null`-значение модели, и конвертер не вызывается.
Переопределите `ConvertsNulls` в `true`, чтобы прогонять `NULL` через конвертер. Политика хранится
в экземпляре конвертера — единственный источник истины.

## Запросы и генерируемый SQL

Конвертер не меняет форму запроса: он только преобразует значения на входе и выходе, поэтому колонка
планируется как любая другая. Примеры ниже используют сущность `Order` из начала гайда и показывают
вывод PostgreSQL; другие диалекты отличаются только регистром ключевых слов и квотированием
идентификаторов (см. [матрицу провайдеров](#матрица-провайдеров)).

**Запись.** INSERT передаёт представление провайдера связанным параметром:

```csharp
ctx.InsertInto<Order>()
    .Values(new Order { Id = 1, Status = Status.Active })
    .Insert();
```

```sql
insert into orders (id, status) values (@p0, @p1)
```

`@p1` несёт сконвертированное значение `'Active'` (строковая форма перечисления), а не номер
перечисления. Константа в списке `SET` конвертируется так же:

```csharp
ctx.Update<Order>()
    .Set(x => x.Status, Status.Closed)
    .Where(x => x.Id == 1)
    .Update();
```

```sql
update orders set status = @p0 where id = 1
```

**Чтение.** Движок читает колонку как представление провайдера и выполняет `ConvertFromProvider`
в приложении:

```csharp
var orders = ctx.From<Order>()
    .Where(x => x.Id == 1)
    .ToList();
```

```sql
select id, status from orders
 where id = 1
```

`status` читается как `text` и построчно конвертируется в `Status`.

**Фильтрация.** Константа в сравнении с колонкой-конвертером тоже конвертируется, поэтому предикат
связывает хранимое представление:

```csharp
ctx.From<Order>().Where(x => x.Status == Status.Active).ToList();
```

```sql
select id, status from orders
 where status = @p0
```

`@p0` несёт `'Active'`. Захваченная переменная ведёт себя так же, а список значений конвертируется
поэлементно в `in`-список:

```csharp
var state = Status.Active;
ctx.From<Order>().Where(x => new[] { state, Status.Closed }.Contains(x.Status)).ToList();
```

```sql
select id, status from orders
 where status in (@p0, @p1)
```

Конвертер применяется только к стороне-значению сравнения; операнд, ссылающийся на другую колонку,
сохраняет обычный SQL, поэтому `(int)x.Status == x.Id + 1` не конвертируется. Составное значение
конвертируется целиком, а не по слагаемым: `Where(x => x.Code == a + b)` привязывает один параметр
со значением `ConvertToProvider(a + b)` (то же для захваченного `a ?? b` или тернарника и для
конкатенации строк). В `case`/`switch` на стороне-значении конвертируются только ветви, а тест
сохраняет обычный SQL.

**Проекция.** Скалярная проекция конвертируемого свойства материализует модель, а не
provider-представление:

```csharp
ctx.From<Order>().Where(x => x.Id == 1).Select(x => x.Status).ToList();
```

```sql
select status from orders
 where id = 1
```

Результат — `[Status.Active]`: колонка читается как `text` и построчно конвертируется обратно, как и
при чтении сущности. Анонимная проекция или member-init (`Select(x => new { x.Status })`) несёт ту же
конверсию.

## In-memory контекст

In-memory провайдер хранит CLR-объекты как есть: отдельного представления провайдера у него нет,
поэтому конвертеры там не применяются (чтение — identity). Это задокументированная согласованная
семантика.

## Ограничения

- JSON-колонка хранит один граф объекта; навигационные свойства и циклы не поддерживаются.
- `[JsonColumn]` нельзя сочетать с `[ValueConverter]` на одном свойстве.
