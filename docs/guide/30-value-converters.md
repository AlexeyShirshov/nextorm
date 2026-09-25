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

## Built-in enum converter

The enum-as-string case is common enough to ship as a converter: `EnumToStringConverter<TEnum>` stores
the enum name (`Enum.ToString()`) and parses it back with `Enum.Parse<TEnum>`, so it replaces the
hand-written `StatusConverter` above. Attach it with either form:

```csharp
[ValueConverter(typeof(EnumToStringConverter<Status>))]
public Status Status { get; set; }
```

```csharp
b.Property(x => x.Status).HasConversion(new EnumToStringConverter<Status>());
```

It also works for a nullable property (`Status?`): SQL `NULL` still maps to `null` without calling the
converter. Write a custom converter when you need a different provider representation (for example the
enum's number) or a case-insensitive parse.

## Implementing the interface directly

`ValueConverter<TModel, TProvider>` implements `IPropertyValueConverter`, and that interface is what
the engine actually consumes. Deriving from the base class is the normal path, but you can implement
`IPropertyValueConverter` directly when the model type is not known at compile time — for example a
converter built from runtime metadata — or when one converter instance is shared across several
properties of different CLR types:

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

`ProviderType` is the representation the column stores: the engine uses it to select the typed reader
accessor and to type the bound write parameter. `ConvertsNulls` keeps the same meaning as on the base
class (see [Null handling](#null-handling)).

A direct implementation is attached exactly like a typed one, through either form:

```csharp
ctx.From<Order>(b => b
    .Table("orders")
    .Property(x => x.Payload).HasConversion(new InvariantStringConverter()));
```

```csharp
[ValueConverter(typeof(InvariantStringConverter))]
public object? Payload { get; set; }
```

Two things you give up relative to the base class: values are boxed on every row (there is no cached
typed invoker), and the mapping cannot verify that the converter's model type matches the property,
because that check reads the `ValueConverter<TModel, TProvider>` base. A mismatch then surfaces as a
cast error on first execution instead of at mapping-build time.

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

The fluent form adds serializer options:

```csharp
b.Property(x => x.Address).JsonColumn(o =>
{
    o.Storage = JsonColumnStorage.Text;
    o.Options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
});
```

The options object is held by reference and must not be mutated after the mapping is built. Source
generated `JsonTypeInfo<T>` (Native AOT/trimming) is out of scope; use reflection-based serialization.

Inserting a row serializes the mapped object into the document stored in the column. Members such as
`DateTimeOffset` need no converter — `System.Text.Json` writes them as ISO 8601:

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

`@p1` carries the serialized document (a `JsonElement` on a native `jsonb` column, a `string` on a text
column):

```json
{"City":"Berlin","Lines":["a","b"],"UpdatedAt":"2026-09-25T12:34:56+00:00"}
```

Property names follow the serializer options; the default is the CLR name (`City`), so set
`JsonNamingPolicy.CamelCase` if you need camelCase. A JSON value in a `SET` list is converted the same
way.

Reading is the reverse: the column is planned like any other and the document is deserialized back on
the client.

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

On the client the engine reads the column as the provider value (a `JsonElement` on a native `jsonb`
column, a `string` on a text column) and runs the converter's `ConvertFromProvider`, i.e.
`JsonElement.Deserialize<Address>()` or `JsonSerializer.Deserialize<Address>(text)`. `System.Text.Json`
reconstructs the graph and turns `"UpdatedAt"` from ISO 8601 back into a `DateTimeOffset` — again with no
converter or manual parsing.

### JSON in queries

A JSON-mapped property is planned like any other converted column, so it also works in projections and
comparisons. A scalar projection materializes the model, not the provider value:

```csharp
var addresses = ctx.From<Customer>().Select(x => x.Address).ToList();
```

A constant compared against a JSON column is serialized to its provider representation before it is
bound, so the predicate runs against the stored document (document equality on a native `jsonb` column,
text equality on a text column):

```csharp
var berlin = new Address { City = "Berlin", Lines = [], UpdatedAt = DateTimeOffset.UnixEpoch };
ctx.From<Customer>().Where(x => x.Address == berlin).ToList();
```

```sql
select id, address from customers
 where address = @p0
```

`@p0` carries the serialized `Address` (a `JsonElement` on PostgreSQL by default, the JSON string on a
text column). The same conversion applies inside an anonymous projection (`Select(x => new { x.Address })`).
Comparison is by the serialized representation, so it matches when the stored document equals the
serialized constant — the serializer options must therefore be stable.

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

## Queries and generated SQL

The converter never changes the shape of the statement: it only converts values on the way in and out,
so the column is planned like any other. The examples below use the `Order` entity from the top of the
guide and show PostgreSQL output; other dialects differ only in keyword casing and identifier quoting
(see the [provider matrix](#provider-matrix)).

**Writing.** An insert sends the provider representation as a bound parameter:

```csharp
ctx.InsertInto<Order>()
    .Values(new Order { Id = 1, Status = Status.Active })
    .Insert();
```

```sql
insert into orders (id, status) values (@p0, @p1)
```

`@p1` carries the converted value `'Active'` (the enum's string form), not the enum number. A constant
in a `SET` list is converted the same way:

```csharp
ctx.Update<Order>()
    .Set(x => x.Status, Status.Closed)
    .Where(x => x.Id == 1)
    .Update();
```

```sql
update orders set status = @p0 where id = 1
```

**Reading.** The engine reads the column as the provider representation and runs `ConvertFromProvider`
in the application:

```csharp
var orders = ctx.From<Order>()
    .Where(x => x.Id == 1)
    .ToList();
```

```sql
select id, status from orders
 where id = 1
```

`status` is read as `text` and converted to `Status` per row.

**Filtering.** A constant compared against a converted column is converted as well, so the predicate
binds the stored representation:

```csharp
ctx.From<Order>().Where(x => x.Status == Status.Active).ToList();
```

```sql
select id, status from orders
 where status = @p0
```

`@p0` carries `'Active'`. A captured variable behaves the same, and a value list is converted element
by element into an `in` list:

```csharp
var state = Status.Active;
ctx.From<Order>().Where(x => new[] { state, Status.Closed }.Contains(x.Status)).ToList();
```

```sql
select id, status from orders
 where status in (@p0, @p1)
```

The converter is applied only to the value side of the comparison; an operand that references another
column keeps its normal SQL, so `(int)x.Status == x.Id + 1` is not converted. A compound value is
converted as a whole rather than leaf by leaf, so `Where(x => x.Code == a + b)` binds a single parameter
holding `ConvertToProvider(a + b)` (the same for a captured `a ?? b` or conditional, and for string
concatenation). In a `case`/`switch` used as the value side, only the branch values are converted while
the test keeps its normal SQL.

**Projection.** A scalar projection of a converted property materializes the model value, not the
provider value:

```csharp
ctx.From<Order>().Where(x => x.Id == 1).Select(x => x.Status).ToList();
```

```sql
select status from orders
 where id = 1
```

The result is `[Status.Active]` — the column is read as `text` and converted back per row, exactly like
an entity read. An anonymous or member-init projection (`Select(x => new { x.Status })`) carries the
same conversion.

## In-memory context

The in-memory provider stores CLR objects as-is: it does not have a separate provider representation,
so converters are not applied there (the read is identity). This is the documented, consistent
semantics.

## Limitations

- A JSON column stores a single object graph; navigation properties and cycles are not supported.
- `[JsonColumn]` cannot be combined with `[ValueConverter]` on the same property.
