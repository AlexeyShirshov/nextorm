# TODO: динамические колонки (dynamic columns store)

> Tracking issue: [#94](https://github.com/AlexeyShirshov/nextorm/issues/94).

> Рабочий план (design RFC). Источник: сравнение инфраструктуры маппинга linq2db — атрибуты
> `DynamicColumnsStoreAttribute` / `DynamicColumnAccessorAttribute` и fluent
> `DynamicColumnsStore()` / `DynamicPropertyAccessors()`. В nextorm маппинг фиксирован набором
> объявленных свойств; неизвестные колонки не читаются и не пишутся.

## 1. Пункт и цель

- **Фича:** сущность может отдать «лишние» колонки строки в словарь (`IDictionary<string, object?>`), ключи
  которого определяются схемой во время выполнения, а не CLR-свойствами; на запись ключи словаря
  рендерятся колонками.
- **Критерий приёмки:**
  1. свойство-хранилище помечается атрибутом/fluent и наполняется при чтении всеми колонками, не
     сопоставленными объявленным членам;
  2. `INSERT`/`UPDATE`/`MERGE` записывают ключи словаря как колонки;
  3. имена колонок квотируются (инъекция исключена, только доверенный источник/allow-list);
  4. набор колонок входит в ключ плана (разные наборы → разные планы);
  5. in-memory — тот же словарь при чтении.

**Реализованный срез:** критерии 1, 3 (для чтения), 5 — сторона **чтения**. Критерии 2 и 4 (write-side
и план по набору ключей) — отложены, см. §7 и `docs/advanced/limitations.md`.

## 2. Провайдерная матрица (чтение)

Поведение не зависит от SQL-функций: колонки читаются из `IDataRecord` по имени (`GetName`/`GetValue`),
сопоставленные колонки читаются по порядковому номеру, `*` добавляется после них. Диалектного хука не
нужно. Источники: официальные справочники `SELECT`/`*` каждого движка (PostgreSQL, Microsoft Learn
T-SQL, MySQL/MariaDB, SQLite, ClickHouse) — все поддерживают список колонок вместе с `*`.

| Провайдер | Чтение | Запись | Примечание |
|---|---|---|---|
| SQL Server | **да** | отложено | `select <mapped>, *`; идентификаторы квотируются диалектом при включённом quoting |
| PostgreSQL | **да** | отложено | `select <mapped>, *`; тип значения — из `DbDataReader` |
| MySQL | **да** | отложено | `select <mapped>, *` |
| MariaDB | **да** | отложено | наследует MySQL |
| SQLite | **да** | отложено | `select <mapped>, *`, dynamic typing |
| ClickHouse | **да** | отложено | колонки таблицы должны существовать |
| InMemory | **да** | отложено | провайдер возвращает зарегистрированную строку как есть, поэтому возвращается её собственный словарь |

**Единообразие провайдеров:** чтение реализовано через общий путь
(`QueryCommand.QueryPreparer.PrepareColumns` → `SqlBuilder.MakeSelect` → `RowMapperFactory` →
`RowMaterializerBuilder`/`DynamicColumns`), поэтому все SQL-провайдеры получают его одновременно;
in-memory — через путь identity-материализации. Запись отложена целиком.

## 3. C#-аналог и tier

Аналог linq2db: `DynamicColumnsStoreAttribute` + `DynamicColumnAccessorAttribute`, fluent
`EntityMappingBuilder<T>.DynamicColumnsStore(...)`. Tier **b**: новый атрибут + слот в метаданных +
поддержка в row reader. Имя `DynamicColumnsAttribute` согласовано с `ColumnAttribute` и реестром
`docs/specs/design/API-NAMING-REVIEW.md`.

## 4. Публичный API (реализовано)

```csharp
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class DynamicColumnsAttribute : Attribute;

bool IPropertyMetadata.IsDynamicColumnsStore => false;          // default interface member
IPropertyMetadata? IEntityMetadata.DynamicColumnsStore => null; // default interface member

EntityPropertyBuilder<T> EntityPropertyBuilder<T>.DynamicColumnsStore();
```

- Хранилище исключено из `IEntityMetadata.Properties` (чтобы обычные мапперы/DML его не трогали) и
  доступно через `IEntityMetadata.DynamicColumnsStore`.
- Тип проверяется: `property.PropertyType.IsAssignableFrom(typeof(Dictionary<string, object?>))`
  (`DynamicColumnsTypeFacts.IsStoreType`); обязателен сеттер; нельзя совмещать с `HasColumnName`,
  конвертером, JSON-колонкой или `RangeColumns`.
- Чтение: `RowMaterializerBuilder` при наличии хранилища переходит на member-init с параметрless ctor
  и биндит хранилище к `DynamicColumns.Read(IDataRecord, startIndex, mappedNames)`.
- SQL: `SqlBuilder.MakeSelect` рендерит `*` для маркера `SelectExpression.IsDynamicColumnsStore`.
- План-ключ: маркер участвует в `SelectExpressionPlanEqualityComparer` (Equals/GetHashCode) и,
  значит, в `ColumnsPlanHash`; запрос с хранилищем не делит план с запросом без него.

## 5. План тестов

- Core/in-memory: `tests/nextorm.core.tests/DynamicColumnsInMemoryTests.cs` — возврат словаря
  зарегистрированной строки.
- SQLite (реальная temp-file БД, без контейнеров):
  `tests/nextorm.sqlite.tests/DynamicColumnsTests.cs` — материализация «лишних» колонок (`age`/`city`),
  `NULL`, отсутствие `id`/`name` в словаре + SQL `select id, name, * from dynamic_entity`.
- SQL-gen по провайдерам: `DynamicColumnsStore_ShouldAppendStar` в
  `tests/nextorm.{sqlite,postgres,sqlserver,mysql,mariadb,clickhouse}.tests/SqlGenerationTests.cs` —
  `select id, * from dynamic_entity`.
- Отложено (не реализовано): join → `NotSupportedException`; запись словаря; план по набору ключей.

## 6. Baseline покрытия

Тесты (полный прогон `dotnet run --project tests/nextorm.<p>.tests -c Debug`, без контейнеров): до —
core 400, sqlite 514, postgres 521, sqlserver 388, mysql 168, mariadb 93, clickhouse 338; после —
core **402**, sqlite **519**, postgres **522**, sqlserver **389**, mysql **169**, mariadb **94**,
clickhouse **339** (все зелёные). Покрытие линии после изменения (dotnet-coverage по core/sqlite/
postgres/sqlserver, `-s coverage.settings.xml`): **line 75.8 %**, branch 69.8 % — выше порога
`MIN_LINE_COVERAGE=75`; baseline «до» в этой сессии не снимался (изменение аддитивное + новые тесты в
`nextorm.core`/`nextorm.sqlite`, покрытие не должно упасть). Публичная поверхность: +1 тип
(`DynamicColumnsAttribute`), +1 метод (`EntityPropertyBuilder<T>.DynamicColumnsStore`), +2
default-члена интерфейсов (`IPropertyMetadata.IsDynamicColumnsStore`,
`IEntityMetadata.DynamicColumnsStore`), +1 init-свойство `PropertyMetadata`, +1 свойство
`EntityMetadata` и 2 internal-свойства `SelectExpression` (в публичную поверхность не входят).

## 7. Статус реализации

**DONE (read-side) / write-side отложен.**

- **Реализовано:** атрибут + fluent + слоты метаданных; исключение хранилища из `Properties`; маркер в
  select-list; `select <mapped>, *`; материализация не сопоставленных колонок в словарь; in-memory
  pass-through; план-ключ; XML-доки; build Release 0/0; тесты core/sqlite/5 провайдеров зелёные.
- **Отложено:** `INSERT`/`UPDATE`/`MERGE` из ключей словаря (нет типа провайдера на ключ и нет
  change tracking); набор ключей как план-ключ (для чтения набор определяет схема, а не запрос);
  хранилище при join/проекции/коррелированном подзапросе (сознательно `NotSupportedException`/не
  собирается); `DynamicColumnAccessor`-доступ по имени.
- **Решение:** пишем read-side end-to-end, write-side документируем как вне области в
  `docs/advanced/limitations.md` (+RU); трекинг — issue #94.

## 8. Открытые вопросы (write-side)

1. Тип словаря на запись: явный `IPropertyValueConverter` на ключ или вывод из CLR-значения.
2. Как отличать `null` от отсутствия ключа.
3. Взаимодействие с value converters и JSON-колонками.
4. Квотирование имён-ключей на запись (allow-list/доверенный источник).
