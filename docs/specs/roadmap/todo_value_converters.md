# TODO: Value converters (кастомный маппинг типов свойств)
> Tracking issue: [#31](https://github.com/AlexeyShirshov/nextorm/issues/31).

> Рабочий план (design RFC). Gap-анализ: **G1** из
> [`linq2db-backlog-gap-analysis.md`](../comparison/linq2db-backlog-gap-analysis.md); аналог linq2db
> [#1994](https://github.com/linq2db/linq2db/issues/1994) (open-generic `TypeConverter`) и темы
> `area: mapping`/`area: types`. Это **общий слой**; частный и самый востребованный случай —
> JSON-колонка ↔ CLR-объект (`G2`, [`todo_json_column_mapping.md`](todo_json_column_mapping.md)).

## Пункт и цель

- **Фича:** объявить для свойства пару конвертеров `CLR ↔ provider-representation`, чтобы хранить
  значение в колонке в другом виде: enum-as-string, `DateTimeOffset`-в-строке/`timestamptz`, кастомные
  strong-typed значения (например `Money`↔`decimal`), JSON-объект (через G2).
- **Критерий приёмки:** `[ValueConverter(typeof(...))]` или fluent `.HasConversion(v => …, v => …)`
  достаточно, чтобы read/write/`Returning`/предикаты-с-константой работали одинаково на всех
  провайдерах; SQL и кэш-ключ стабильны; `IPropertyMetadata.Converter` — default-член (внешние
  реализации `IPropertyMetadata` продолжают компилироваться).
- **Не вводит** публичный `IValueConverter` только ради JSON: seam нужен всем конвертерам; G2
  регистрирует `JsonConverter<,>` поверх него.

## Почему это нужно (мотивация)

1. **Сейчас конвертеров нет вообще.** `rg` по `ValueConverter|IValueConverter|HasConversion|TypeConverter`
   в `src/nextorm.core` — ноль. `IPropertyMetadata` (`DataContext/Meta/IPropertyMetadata.cs:12-58`)
   знает только `PropertyInfo`/`ColumnName`/`IsColumnNameAuto`/`IsKey`/`IsIdentity`/`IsComputed`.
2. **Часть типов нечитаема/непишема.** `SelectExpression.GetDataRecordMethod` (`Expressions/SelectExpression.cs:78-152`)
   — жёсткий switch по `int/long/DateTime/string/bool/double/decimal/float/short/byte/Guid/ulong/array/tuple/Dictionary`;
   enum и `DateTimeOffset` там отсутствуют (и `DateTimeOffset` в `src/` нет вовсе). Без конвертера такие
   свойства требуют ручных проекций и ручного `JsonSerializer`/`Parse` в коде (`docs/guide/18-json.md:280`).
3. **База для G2 и соседей.** enum-as-string, `DateTimeOffset`, JSON-колонка, `TimeSpan`-юниты
   ([`todo_timespan_columns.md`](todo_timespan_columns.md)) — всё это частные случаи одного механизма.

## Текущее состояние и разрыв

| Слой | Где | Сейчас | Нужно |
|---|---|---|---|
| Метаданные | `DataContext/Meta/IPropertyMetadata.cs:12-58` | нет конвертера | `IValueConverter? Converter => null` (default-член) |
| Обнаружение | `DataContext/Meta/EntityMetadataBuilder.cs:52-77` (fallback на интерфейс `:61-64`) | `[Column]`/`[Key]`/`[DatabaseGenerated]` | `[ValueConverter(typeof(…))]` (+ интерфейсный fallback) |
| Fluent | `DataContext/Meta/EntityPropertyBuilder.cs:32-83` | `HasColumnName`/`Key`/`Identity`/`Computed` | `.HasConversion(converter)` / `.HasConversion(to, from)` |
| Read (запрос) | `Expressions/SelectExpression.cs:78-152`, `DataContext/RowMapperFactory.cs:24-159` | getter выбирается по CLR-типу; switch бросает на неизвестном | для конвертируемого свойства читать **provider-тип**, затем `ConvertFromProvider` |
| Материализация | `DataContext/RowMaterializerBuilder.cs`, oneColumn-ветка `RowMapperFactory.cs:130-143` | биндит значение колонки «как есть» | применять конвертер до биндинга (единая точка в `MapColumn`) |
| Write (DML) | `DataContext/SqlMutationBuilder.cs:801` (`new Parameter(name, value.Constant)`), `Query/Mutations/MutationCommand.cs:59-75` (`InsertColumn.Property`) | сырое значение | `Converter?.ConvertToProvider(value.Constant)` |
| Write (entity) | `Builders/InsertBuilder.Values.cs:124` | `GetValue` напрямую | — (конвертация в `SqlMutationBuilder`, чтобы покрыть все формы) |
| Update/Merge | `Builders/UpdateBuilder.cs`, `MergeBuilder*.cs` | те же `InsertValue` | то же |
| Предикаты/константы | `Visitors/BaseExpressionVisitor.cs`, `SqlOperandTranslator` | константа биндится как CLR-объект | конвертировать константу в provider-представление до эмита параметра |
| Кэш | `DataContext/RowMapperFactory.cs:176-196` (`BuildSignature`), `DataContextCache.Metadata` | хэширует тип/имя/`Nullable` | включить идентичность конвертера |
| In-memory | `InMemoryRowMaterializer.cs` | тот же materializer | наследует конвертацию |

## Дизайн и публичный API

```csharp
public interface IValueConverter
{
    object? ConvertToProvider(object? model);
    object? ConvertFromProvider(object? provider);
    bool ConvertsNulls { get; }
}

public abstract class ValueConverter<TModel, TProvider> : IValueConverter
{
    public abstract TProvider? ConvertToProvider(TModel? model);
    public abstract TModel? ConvertFromProvider(TProvider? provider);
    public virtual bool ConvertsNulls => false; // true -> null прогоняется через конвертер
}

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class ValueConverterAttribute : Attribute
{
    public ValueConverterAttribute(Type converterType); // должен быть закрытым IValueConverter
    public bool ConvertsNulls { get; set; }
}

// fluent
EntityPropertyBuilder<T>.HasConversion<TProvider>(ValueConverter<T, TProvider> converter);
EntityPropertyBuilder<T>.HasConversion<TProvider>(Expression<Func<T, TProvider>> to, Expression<Func<TProvider, T>> from);
```

- **Provider-тип** (TProvider) определяет и getter чтения, и тип параметра записи. Метаданные
  (`IPropertyMetadata`) несут `Converter`; `SelectExpression` для конвертируемого свойства должен
  использовать `TProvider` в `GetDataRecordMethod` (`_realType` ≠ `PropertyType`), а `PropertyType`
  остаётся CLR-моделью для alias/`PropertyName`.
- **null-политика:** по умолчанию SQL `NULL` ↔ `null` модели (конвертер не вызывается); `ConvertsNulls`
  включает прогон литерала `null` через конвертер.
- **Ошибки:** несовпадение типов в `[ValueConverter]`/`HasConversion` — на этапе построения
  метаданных (`BuildSqlCommandException`/`InvalidOperationException`), а не на исполнении.
- **Кэш:** `BuildSignature` и plan-hash включают тип конвертера, иначе разные конвертеры разделят запись.
- **AOT:** `ValueConverter<,>` — код, не рефлексия; рефлексия нужна только для атрибутной формы, это
  ок вне AOT; JSON-путь (G2) отдельно требует `JsonTypeInfo<T>`.

## Провайдерная матрица

Сам конвертер работает **в приложении**, поэтому фича провайдеро-независима; различается лишь
**provider-представление**, которое должно быть биндируемым/читаемым. Источники: Npgsql type mapping
(`timestamptz`↔`DateTimeOffset`, `jsonb`), MS Learn типы SQL Server (`datetimeoffset`, `nvarchar`),
MySQL/MariaDB (`JSON`, `varchar`), ClickHouse (`String`, `DateTime64`), SQLite (динамическая типизация).

| Провайдер | enum-as-string | `DateTimeOffset` | native JSON | Прочее |
|---|---|---|---|---|
| PostgreSQL | `varchar`/`text` | `timestamptz` (Npgsql нативно) | `jsonb` | `text[]` |
| SQL Server | `nvarchar` | `datetimeoffset` | `nvarchar(max)` | — |
| MySQL / MariaDB | `varchar` | `varchar`/`timestamp` | `JSON`/`LONGTEXT` | — |
| SQLite | `TEXT` | `TEXT` (ISO) | `TEXT` | — |
| ClickHouse | `String`/`Enum8` | `String`/`DateTime64` | `String` | — |
| InMemory | CLR | CLR | CLR | конвертер всё равно применяется |

## Этапы внедрения

1. **Seam**: `IValueConverter`/`ValueConverter<,>`, `IPropertyMetadata.Converter` (default `null`),
   `[ValueConverter]` + интерфейсный fallback, fluent `.HasConversion`; read через provider-тип
   (`SelectExpression`/`RowMapperFactory`), write через `SqlMutationBuilder:801`; ключ кэша.
   Тесты: enum-as-string, `DateTimeOffset`, `Guid`↔`string`.
2. **Константы в предикатах/проекциях**: конвертация правой константы (`Where(e => e.Status == Status.Active)`)
   и скалярный `Select(e => e.Status)`.
3. **G2-интеграция**: `JsonConverter<,>` регистрируется как частный `ValueConverter<,>`.
4. **Возможное расширение**: `[Column(TypeName=…)]`/явный `DbType` (отдельно, если понадобится).

## План тестов

- Unit (`tests/nextorm.core.tests`): метаданные читают `[ValueConverter]` (в т.ч. с интерфейса); fluent
  `.HasConversion`; ключ кэша различает конвертеры; null-политика; ошибки на несовпадении типов.
- SQL-gen (`tests/nextorm.{postgres,sqlserver,mysql,sqlite,clickhouse}.tests`): конвертируемое свойство
  в проекции/`WHERE`/`INSERT`/`UPDATE SET` даёт корректный provider-тип и параметр.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.ValueConverters.cs`): round-trip
  enum-as-string и `DateTimeOffset` на PostgreSQL/SQL Server/MySQL/SQLite; `Returning` с конвертером.
- In-memory: `From<T>()` с конвертируемым свойством.
- Память: делегат конвертера кэшируется, значение конвертируется на строку (снапшот аллокаций).

## Открытые вопросы

1. `TProvider` — выводить из типа конвертера/`IValueConverter` (non-generic) или требовать явный
   параметр?
2. Базовая null-политика: всегда `NULL`↔`null` или конвертер вызывается всегда (как в EF `ConvertsNulls`)?
3. Атрибут: `[ValueConverter(typeof(...))]` vs `[Column(Converter = ...)]`; имя `HasConversion` vs `Convert`.
4. Конвертировать ли значения и в **предикатах** на первом шаге или отложить (риск неверного SQL).
5. Где хранить `TProvider` в кэше плана — тип конвертера или `MethodInfo` (стабильность хэша).
6. Совместимость с source-gen-стабом (`src/nextorm.core.sourcegenerator`) для AOT-регистрации.

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Meta/ValueConverter.cs` (`IValueConverter`, `ValueConverter<,>`,
  `ValueConverterAttribute`), `tests/nextorm.core.tests/ValueConverterTests.cs`,
  `tests/nextorm.integration.tests/CommonTestSuite.ValueConverters.cs`.
- Правки: `DataContext/Meta/IPropertyMetadata.cs`, `Meta/Implementation/PropertyMetadata.cs`,
  `Meta/EntityMetadataBuilder.cs` (атрибут + интерфейсный fallback), `Meta/EntityPropertyBuilder.cs`,
  `Expressions/SelectExpression.cs` (provider-тип чтения), `DataContext/RowMapperFactory.cs`
  (`MapColumn`, `BuildSignature`), `DataContext/SqlMutationBuilder.cs` (конвертация параметра),
  фаза 2 — `Visitors/BaseExpressionVisitor.cs`/`SqlOperandTranslator.cs`.
- Документация: `docs/guide/` (новый раздел «Value converters», +RU + `toc.yml`),
  `docs/advanced/api-reference.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **пересмотр до реализации — 4 🟡**; API закрыт корректно, но нужна «единая точка» конвертации и решение о границе с duration.

- **[DRY]/[OCP] 🟡** Два параллельных слоя конвертации CLR↔provider: `:12-20,31-32` + `todo_timespan_columns.md:164-167`. Duration уже реализован частной конвертацией: read `SelectExpression.cs:107-114`, материализация `RowMapperFactory.cs:39-46`, write `DurationStorage.ToParameterValue`, поправки констант `BaseExpressionVisitor.cs:90,484-489`. Fix: зафиксировать границу — либо (a) `[Duration]` как встроенный `ValueConverter<TimeSpan,TProvider>` и свернуть спец-ветки, либо (b) обосновать, что native-типизация duration не пересекается с G1.
- **[DRY] 🟡** Write-шов недописан: план называет одну точку (`:43,111`, `SqlMutationBuilder.cs:801` — сейчас это `AppendMergeAssignments`), а в дереве 6+ сайтов `new Parameter` + `DurationStorage.ToParameterValue`: `SqlMutationBuilder.cs:285,369,880`, `SqlSourceRenderer.cs:795`, `QueryPlanner.cs:261`, `BulkInsertBuilder.cs:206`; плюс нормализация констант `MemberTranslator.cs:263,371`, `SqlOperandTranslator.cs:72`, `BaseExpressionVisitor.cs:354,489`, `InValuesTranslator.cs:72,96`. Fix: единый хелпер формы `ToProviderValue(value, property, dialect)` и перечислить все сайты.
- **[PERF]/[TYPE] 🟡** `:53-65,127` `IValueConverter.ConvertToProvider/ConvertFromProvider(object?)` боксят value-типы на каждой строке/константе, а `ValueConverter<TModel,TProvider>` объявляет `abstract`-методы, но не реализует `object?`-члены интерфейса (сниппет не удовлетворяет `IValueConverter`). Fix: определить мост и кэшировать типизированный инвокер (`Func<TModel,TProvider>`), вызывать делегат, а не `object?`-метод; бюджет упаковки — в тест «Память» (`:127`).
- **[DRY] 🟡** `ConvertsNulls` — три источника истины (`:57` интерфейс, `:64` virtual в базе, `:71` set-свойство атрибута). Fix: политика только на экземпляре конвертера.
- **[DIP] ℹ️** Назвать 2-го потребителя публичного шва (инвариант 1, прецедент F3/F7): `IPropertyMetadata.Converter` (type-erased хранение) + `[ValueConverter(typeof(...))]` (рефлексия) + G2 (`JsonConverter<,>`). Fix: добавить абзац до реализации.
- **[TYPE]/[DRY] ℹ️** Двойственность типа чтения в `SelectExpression`: getter читается из `_realType` (`SelectExpression.cs:15,55-68,91`), маппер выводит из `PropertyType` (`RowMapperFactory.cs:36`), для конвертера нужен третий — `TProvider`. Fix: ввести `SelectExpression.ProviderType` и включить в `BuildSignature` (`RowMapperFactory.cs:200-221`), как требует `:87`.
