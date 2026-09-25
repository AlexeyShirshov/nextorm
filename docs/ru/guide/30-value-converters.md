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
}

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

Объект настроек хранится по ссылке, и его нельзя менять после построения маппинга. Поддержка
`JsonTypeInfo<T>`/source-gen для AOT пока не подключена; используйте рефлексию.

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

## In-memory контекст

In-memory провайдер хранит CLR-объекты как есть: отдельного представления провайдера у него нет,
поэтому конвертеры там не применяются (чтение — identity). Это задокументированная согласованная
семантика.

## Ограничения

- Константы в предикатах и скалярных проекциях (например, `Where(x => x.Status == Status.Active)`)
  пока не конвертируются; сравнивайте через хранимое значение или проецируйте сущность. Это
  следующий этап [#31](https://github.com/AlexeyShirshov/nextorm/issues/31).
- JSON-колонка хранит один граф объекта; навигационные свойства и циклы не поддерживаются.
- `[JsonColumn]` нельзя сочетать с `[ValueConverter]` на одном свойстве.
