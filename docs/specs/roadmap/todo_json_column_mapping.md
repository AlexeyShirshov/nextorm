# TODO: JSON-колонка ↔ CLR-объект (авто-сериализация свойства)
> Tracking issue: [#64](https://github.com/AlexeyShirshov/nextorm/issues/64).

> Рабочий план (design RFC). Gap-анализ: **G2** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md); аналог linq2db
> [#1661](https://github.com/linq2db/linq2db/issues/1661) «JSON Column Types, auto serialization with
> objects». Опирается на общий слой конвертеров (G1) — [`todo_value_converters.md`](todo_value_converters.md);
> здесь `JsonConverter<,>` регистрируется поверх него.

## Пункт и цель

- Фича: свойство сущности произвольного CLR-типа (`record`/`class`/`struct`, вложенные и коллекции)
  хранится в JSON-колонке и прозрачно сериализуется/десериализуется `System.Text.Json`:
  - **чтение** — `From<T>().ToList()` материализует `T.JsonProp` из JSON-колонки;
  - **запись** — `InsertInto(entity)`/`Update(...)`/`MergeInto(...)` пишут JSON-строку/native JSON;
  - **проекции и `Returning`** — то же преобразование.
- Критерий приёмки: `[JsonColumn]` на свойстве достаточно для round-trip на PostgreSQL (`jsonb`) и на
  text-JSON провайдерах (SQL Server/MySQL/MariaDB/ClickHouse/SQLite-`TEXT`); в in-memory конвертация
  работает через тот же materializer; SQL и кэш-ключ стабильны.
- **Не путать** с [`todo_json_streaming.md`](todo_json_streaming.md) (выдача `SELECT` в JSON-поток) и
  с существующим `SqlFunctions.Postgres`/`SqlServer` JSON-поверхностями (это функции, а не маппинг
  колонки).

## Почему это нужно (мотивация)

1. **Нет способа объявить JSON-колонку.** Сегодня JSON — только функции поверх `string`/native-типа:
   `Select(e => SqlFunctions.Postgres.json_get_text(e.String, "name"))`, затем ручной
   `JsonSerializer.Deserialize<T>` (`docs/guide/18-json.md:280-281`). Свойство-DTO, лежащее в `jsonb`,
   объявить нельзя.
2. **Чтение такого свойства сейчас падает.** `SelectExpression.GetDataRecordMethod()` знает только
   примитивы/`Guid`/массивы/`Tuple`/`Dictionary` и бросает `NotSupportedException` на POCO
   (`src/nextorm.core/Expressions/SelectExpression.cs:78-151`).
3. **Запись тоже.** `InsertBuilder.Values(entity)` берёт `property.PropertyInfo.GetValue(...)` «как
   есть» (`Builders/InsertBuilder.Values.cs:124`), а параметр строится из сырого значения
   (`DataContext/SqlMutationBuilder.cs:801` `new Parameter(name, value.Constant)`).
4. **Это частный случай G1.** Общий механизм конвертера типа
   ([`todo_value_converters.md`](todo_value_converters.md)) закрывает enum-as-string, `DateTimeOffset`
   и кастомные типы; JSON-колонка — самый востребованный частный случай.

## Текущее состояние и разрыв

| Слой | Где | Сейчас | Нужно |
|---|---|---|---|
| Метаданные свойства | `DataContext/Meta/IPropertyMetadata.cs:12-57` | `PropertyInfo`/`ColumnName`/`IsKey`/`IsIdentity`/`IsComputed`; конвертера нет | `IValueConverter? Converter` (+ признак JSON-колонки) с default-реализацией `null` |
| Обнаружение | `DataContext/Meta/EntityMetadataBuilder.cs:52-77` | атрибуты `[Column]`/`[Key]`/`[DatabaseGenerated]`, fallback на интерфейс (`:61-64`) | читать `[JsonColumn]` (и `intProp`) и прокидывать конвертер |
| Fluent | `DataContext/Meta/EntityPropertyBuilder.cs:32-83` | `HasColumnName`/`Key`/`Identity`/`Computed` | `.JsonColumn(...)`/`.HasConversion(...)` |
| Read (запрос) | `DataContext/RowMapperFactory.cs:24-57,120-159` | typed getter по `SelectExpression.PropertyType`; конвертера нет | читать провайдерский тип, затем `ConvertFromProvider`, затем биндить к модели |
| Select-колонка | `Expressions/SelectExpression.cs:78-151` | getter выбирается по CLR-типу свойства | для JSON-колонки читать `string` (text) / `string`/`JsonDocument` (PG), хранить provider-тип |
| Материализация | `DataContext/RowMaterializerBuilder.cs` | биндит значение колонки к `PropertyInfo` | применять конвертер до биндинга (и в oneColumn-ветке `RowMapperFactory:130-143`) |
| Write (DML) | `DataContext/SqlMutationBuilder.cs:801`, `Query/Mutations/MutationCommand.cs:81-112` | `InsertValue.FromConstant` сырой; `InsertColumn.Property` несёт метаданные | конвертировать по `Property.Converter` при построении `Parameter` |
| Write (entity) | `Builders/InsertBuilder.Values.cs:124` | `GetValue` напрямую | — (конвертация в `SqlMutationBuilder`, чтобы покрыть все формы) |
| Update/Merge | `Builders/UpdateBuilder.cs`, `MergeBuilder*.cs` | те же `InsertValue` | то же |
| Предикаты/константы | `Visitors/BaseExpressionVisitor.cs`, `SqlOperandTranslator`, `JsonSqlTranslator.cs:11-15` | PG-параметр `JsonDocument`/`JsonNode` биндится как jsonb вручную | конвертер для `Where(e => e.Json == obj)` (фаза 2) |
| Кэш | `DataContext/RowMapperFactory.cs:176-196` (`BuildSignature`) | хэширует тип/имя/`Nullable` | включить идентичность конвертера |
| In-memory | `InMemoryRowMaterializer.cs` | тот же `RowMaterializerBuilder` | наследует конвертацию автоматически |

JSON-поверхности, которые уже есть и с фичей не конфликтуют:
`SupportsJson` (`JsonSqlTranslator`, PostgreSQL native), `SupportsTextJson` (`TextJsonSqlTranslator`,
SQL Server/MySQL/MariaDB), `SupportsJsonExtract` (ClickHouse), `ForJson` (SQL Server), `openjson`.

## Дизайн

### Слой конвертеров (G1 + JSON-надстройка)

```csharp
public abstract class ValueConverter<TModel, TProvider>
{
    public abstract TProvider? ConvertToProvider(TModel? model);
    public abstract TModel? ConvertFromProvider(TProvider? provider);
    public virtual bool ConvertsNulls => false; // true -> null прогоняется через конвертер
}
```

- `IPropertyMetadata` расширяется `IValueConverter? Converter => null` (default-реализация — как
  `IsColumnNameAuto`/`IsKey`, чтобы внешние имплементаторы не ломались);
- `JsonConverter<TModel, TProvider>` — частный случай поверх `System.Text.Json` с
  `JsonSerializerOptions`/`JsonTypeInfo<TProvider>`;
- общий `ValueConverter<,>`/атрибут `[ValueConverter]`/`.HasConversion(...)` — в
  [`todo_value_converters.md`](todo_value_converters.md); этот todo использует seam и добавляет только
  JSON-надстройку.

### Публичный API

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class JsonColumnAttribute : Attribute
{
    public JsonColumnStorage Storage { get; set; } = JsonColumnStorage.Auto; // Auto | Native | Text
    public JsonSerializerOptions? Options { get; set; }
}

public enum JsonColumnStorage { Auto, Native, Text }

// fluent
EntityPropertyBuilder<T>.JsonColumn(Action<JsonColumnOptions>? configure = null);
EntityPropertyBuilder<T>.HasConversion<TProvider>(ValueConverter<TModel, TProvider> converter);
```

`Storage.Auto` резолвится по диалекту: `SupportsJson` → native `jsonb` (PostgreSQL), иначе text
(SQL Server `nvarchar`, MySQL/MariaDB `JSON`/text, ClickHouse `String`, SQLite `TEXT`). Провайдер без
`SupportsJson`/`SupportsTextJson`/`SupportsJsonExtract` всё равно может писать text-JSON как обычную
строку — решить в фазе 1 (см. открытые вопросы).

### Read-путь

1. `SelectExpression` для JSON-свойства получает **провайдерский** тип чтения (`string` для text;
   `string` для PG `jsonb` — Npgsql отдаёт `string`, либо `JsonDocument` при `Storage.Native`);
   `PropertyType` остаётся CLR-моделью (для alias/`PropertyName`).
2. `RowMapperFactory.MapColumn` после get'а вставляет `converter.ConvertFromProvider(value)`
   (через `Expression.Call` по сохранённому `MethodInfo`) и только затем `Convert` к `PropertyType`;
   ветка `Nullable` (`:32-44`) — `FromProvider(null)` при `ConvertsNulls`, иначе `null`.
3. `RowMaterializerBuilder` биндит уже преобразованное значение — менять его не нужно, если шаг (2)
   выполнен в `mapColumn` (единая точка и для entity-, и для `oneColumn`-ветки).
4. `BuildSignature` включает `converter.GetType()`/`ConvertFromProvider.Method`, чтобы разные
   конвертеры не делили кэш-запись.

### Write-путь

1. В `SqlMutationBuilder.cs:801` вместо `new Parameter(name, value.Constant)`:
   `new Parameter(name, column.Property.Converter?.ConvertToProvider(value.Constant) ?? value.Constant)`.
   `InsertColumn.Property` (`MutationCommand.cs:59-75`) уже несёт `IPropertyMetadata` — точка покрывает
   `INSERT VALUES` (все формы), `UPDATE ... SET`, `MERGE`.
2. Форма `Value(expr, entityProperty)` (колонка↔колонка, `InsertBuilder.Values.cs:66-70`) конвертации
   **не требует** — обе стороны читаются в SQL.
3. `INSERT ... SELECT` (`Values(source, mapping)`) пишет значения на сервере из `SELECT` — конвертер к
   исходному `EntityBuilder` не применяется; если источник — тот же `TEntity`, каст в SQL или
   документировать ограничение.

### Предикаты и проекции (фаза 2)

- `Where(e => e.Json == obj)` / `Contains` по JSON-свойству: правую константу нужно конвертировать в
  provider-представление до эмита параметра (`SqlOperandTranslator`/`BaseExpressionVisitor`), иначе
  `Parameter` получит CLR-объект. Опирается на общий конвертер констант (часть G1).
- `Select(e => e.Json)` как скаляр — расширить `SelectExpression`-фабрику, чтобы колонка несла
  конвертер и читалась как provider-тип, а наружу отдавался CLR-тип.

### Провайдерная матрица

| Провайдер | Storage по умолчанию | Колонка | Запись параметра | Чтение |
|---|---|---|---|---|
| PostgreSQL | Native | `jsonb` | Npgsql `JsonDocument`/`string` | `GetString`/`GetFieldValue<JsonDocument>` |
| SQL Server | Text | `nvarchar(max)` | `string` | `GetString` |
| MySQL / MariaDB | Text | `JSON`/`LONGTEXT` | `string` | `GetString` |
| ClickHouse | Text | `String` | `string` | `GetString` |
| SQLite | Text | `TEXT` | `string` | `GetString` |
| In-memory | — | — | CLR-объект | CLR-объект (конвертер всё равно применяется) |

Gating: `Storage.Native` требует `ISqlDialect.SupportsJson` (иначе — понятное исключение или
деградация к Text по решению).

### AOT / trimming

`JsonSerializerOptions`-рефлексия не trim/AOT-safe. Поддержать `[JsonSerializable]`-контекст:
`[JsonColumn(Options = …)]` или `JsonColumnOptions.TypeInfo<TPoco>()` (`JsonTypeInfo<T>`), и в
документации требовать source-gen-контекст для AOT (у nextorm уже есть
`src/nextorm.core.sourcegenerator`-стаб).

### Фазы

1. **MVP**: `[JsonColumn]` + `JsonConverter<,>` поверх STJ; read + write (INSERT/UPDATE/MERGE);
   PostgreSQL `jsonb` + text-провайдеры; интерфейсный fallback атрибута; в in-memory через materializer.
2. **Конвертеры**: предикаты/константы и `Select(e => e.Json)`; `Storage`-тонкая настройка;
   `Returning`/`Output`. Общий слой (`ValueConverter<,>`, `[ValueConverter]`, `.HasConversion`) — в
   [`todo_value_converters.md`](todo_value_converters.md).
3. **AOT/source-gen**: `JsonTypeInfo<T>`; nested/коллекции; bulk-write.

## Ограничения и цена

- Только **свойство-в-колонку**; граф связей и циклы не поддерживаются (у nextorm нет навигации).
- `[JsonColumn]` — nextorm-специфичный атрибут (как `[SqlTable]`); стандартный `[Column]` задаёт имя.
- Семантика `null`: SQL `NULL` vs JSON-литерал `null` — по умолчанию `NULL`↔`null`-модель;
  `ConvertsNulls` переключает на сериализацию литерала. Зафиксировать в доке.
- Стоимость: сериализация на строку при материализации/записи; кэшируется делегат конвертера, не
  значения. Для больших документов — точка роста; профилировать.
- Публичный API (`JsonColumnAttribute`, `ValueConverter<,>`, `IPropertyMetadata.Converter`) → обновить
  `docs/specs/design/API-NAMING-REVIEW.md`, доки EN+RU, `PublicAPI.*`.
- Text-fallback на провайдере без JSON-возможностей — риск, что БД не примет/не распарсит; gate
  `Storage.Auto` → text только если колонка уже `string`/`text`.

## Этапы внедрения

- **Фаза 1**: seam `IValueConverter` + `JsonConverter<,>`; `[JsonColumn]` в `EntityMetadataBuilder`
  (с интерфейсным fallback, как `ColumnAttribute`); `.JsonColumn` в `EntityPropertyBuilder`;
  read-конвертация в `RowMapperFactory.MapColumn`; write-конвертация в `SqlMutationBuilder:801`;
  ключ кэша; тесты (unit + SQL-gen + интеграция + in-memory).
- **Фаза 2**: общий `ValueConverter<,>`/`[ValueConverter]`; конвертация констант в предикатах/проекциях;
  `Returning`.
- **Фаза 3**: `JsonTypeInfo<T>`/source-gen, nested, bulk.
- **Вне области**: `ForJson` (уже есть), `openjson`-маппинг, JSON-документ как *тип параметра*
  (уже частично: PG `JsonDocument`).

## План тестов

- Unit (`tests/nextorm.core.tests`): метаданные читают `[JsonColumn]` (в т.ч. с интерфейса); fluent
  `.JsonColumn()`; ключ кэша различает конвертеры; `ConvertFromProvider` в материализации; null-политика.
- SQL-gen (`tests/nextorm.{postgres,sqlserver,mysql,sqlite,clickhouse}.tests`): JSON-свойство в
  проекции/INSERT/UPDATE/SET даёт корректное имя колонки и provider-тип.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.JsonColumn.cs`): round-trip
  POCO↔`jsonb` (PostgreSQL) и POCO↔text (SQL Server/MySQL/ClickHouse/SQLite); вложенный объект,
  `List<T>`; `null`; `Returning` (фаза 2).
- In-memory: `From<T>()` с JSON-свойством.
- Память: снапшот аллокаций round-trip; большие документы.

## Открытые вопросы

1. Вводить сразу общий `ValueConverter<,>` (часть G1) или только `[JsonColumn]` в фазе 1?
2. `Storage.Auto` на провайдере без JSON-флага: писать text как обычную строку или бросать?
3. Хранить в метаданных `JsonSerializerOptions` (мутабельный, не потокобезопасный кэш-ключ) или
   `JsonTypeInfo<T>`/профиль?
4. Имя: `[JsonColumn]` vs `[Json]`/`[JsonValue]`; `JsonColumnStorage` vs `JsonStorage`.
5. PG: `json` vs `jsonb` по умолчанию; читать `GetString` или `GetFieldValue<JsonDocument>`.
6. Нужны ли коллекции/вложенность в фазе 1 или отложить до фазы 3.
7. Как сочетать с существующим `SqlFunctions.Postgres.json_get*` (ручной путь не должен конфликтовать).

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Meta/JsonColumnAttribute.cs`,
  `src/nextorm.core/DataContext/Meta/JsonConverter.cs` (JSON-надстройка; базовый `ValueConverter<,>`/`IValueConverter`
  — в [`todo_value_converters.md`](todo_value_converters.md)), тесты
  `tests/nextorm.core.tests/JsonColumnTests.cs`,
  `tests/nextorm.integration.tests/CommonTestSuite.JsonColumn.cs`.
- Правки: `DataContext/Meta/IPropertyMetadata.cs`, `Meta/Implementation/PropertyMetadata.cs`,
  `Meta/EntityMetadataBuilder.cs` (чтение атрибута + интерфейсный fallback),
  `Meta/EntityPropertyBuilder.cs`, `DataContext/RowMapperFactory.cs` (`MapColumn`, `BuildSignature`),
  `Expressions/SelectExpression.cs` (provider-тип + конвертер),
  `DataContext/SqlMutationBuilder.cs` (конвертация параметра),
  опционально `Visitors/SqlOperandTranslator.cs`/`BaseExpressionVisitor.cs` (фаза 2).
- Документация: `docs/guide/18-json.md` (раздел «mapping a JSON column to a CLR property», +RU),
  `docs/advanced/api-reference.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.
