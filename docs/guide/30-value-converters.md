# Value converters and JSON columns

A **value converter** maps a property between its CLR model type and the representation actually
stored in the column. Typical uses are an `enum` stored as text, a `DateTimeOffset` stored in a
formatted string, a strong-typed value (`Money`) stored as a `decimal`, or an arbitrary object stored
as JSON.

## Declaring a converter

Derive from `ValueConverter<TModel, TProvider>` and override the two typed conversions. The engine
applies them automatically on both the read and the write path, and caches a typed invoker so value
types are not boxed per row.

```csharp
public enum Status { Unknown, Active, Closed }

public sealed class StatusConverter : ValueConverter<Status, string>
{
    public override string? ConvertToProvider(Status model) => model.ToString();
    public override Status ConvertFromProvider(string? provider) => Enum.Parse<Status>(provider!);
}
```

Attach it with the attribute:

```csharp
[SqlTable("orders")]
public sealed class Order
{
    public long Id { get; set; }

    [ValueConverter(typeof(StatusConverter))]
    public Status Status { get; set; }
}
```

or fluently:

```csharp
ctx.From<Order>(b => b
    .Table("orders")
    .Property(x => x.Status).HasConversion(new StatusConverter()));
```

`HasConversion<TModel, TProvider>` accepts either a `ValueConverter<TModel, TProvider>` instance or a
pair of conversion expressions:

```csharp
b.Property(x => x.Status).HasConversion<Status, string>(
    status => status.ToString(),
    text => Enum.Parse<Status>(text));
```

## JSON columns

`[JsonColumn]` stores any serializable CLR object as JSON. The **storage** is chosen by the dialect:

- `JsonColumnStorage.Auto` (default) uses the provider's native JSON type where it has one
  (PostgreSQL `jsonb`) and a text column otherwise;
- `JsonColumnStorage.Native` forces `jsonb` (PostgreSQL only);
- `JsonColumnStorage.Text` forces a text column.

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

The fluent form adds serializer options:

```csharp
b.Property(x => x.Address).JsonColumn(o =>
{
    o.Storage = JsonColumnStorage.Text;
    o.Options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
});
```

The options object is held by reference and must not be mutated after the mapping is built. AOT/source
generated `JsonTypeInfo<T>` is not wired yet; use reflection-based serialization for now.

## Provider matrix

The converter runs in the application, so it is provider independent; only the provider representation
must be readable and bindable.

| Provider | Native JSON | Text storage | Enum-as-string |
|---|---|---|---|
| PostgreSQL | `jsonb` (read/written as `JsonElement`) | `text` | `text` |
| SQL Server | — (`nvarchar(max)`) | `nvarchar(max)` | `nvarchar` |
| MySQL / MariaDB | — (`longtext`) | `longtext` | `longtext` |
| SQLite | — (`TEXT`) | `TEXT` | `TEXT` |
| ClickHouse | — (`String`) | `String` | `String` |
| In-memory | CLR value | CLR value | CLR value |

## Null handling

By default SQL `NULL` maps to a `null` model value and the converter is not called. Override
`ConvertsNulls` to `true` to pass `NULL` through the converter instead. The policy lives on the
converter instance, so it is the single source of truth.

## In-memory context

The in-memory provider stores CLR objects as-is: it does not have a separate provider representation,
so converters are not applied there (the read is identity). This is the documented, consistent
semantics.

## Limitations

- Constant values inside predicates and scalar projections (for example
  `Where(x => x.Status == Status.Active)`) are not converted yet; compare through the stored value or
  project the entity. This is the next phase of [#31](https://github.com/AlexeyShirshov/nextorm/issues/31).
- A JSON column stores a single object graph; navigation properties and cycles are not supported.
- `[JsonColumn]` cannot be combined with `[ValueConverter]` on the same property.
