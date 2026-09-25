# TODO: динамические колонки (dynamic columns store)

> Tracking issue: —

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
- **Возможный статус:** если признаётся вне scope (nextorm не `ITable<T>`-ORM), зафиксировать это в
  `limitations.md` и закрыть пункт решением.

## 2. Провайдерная матрица

Поведение не зависит от SQL-функций: колонки читаются из `IDataRecord` по имени и рендерятся
идентификаторами.

| Провайдер | Чтение | Запись | Примечание |
|---|---|---|---|
| SQL Server | да | да | идентификаторы квотировать `[...]` |
| PostgreSQL | да | да | `"..."`; тип значения — из `DbDataReader` |
| MySQL / MariaDB | да | да | `` `...` `` |
| SQLite | да | да | `"..."`, dynamic typing |
| ClickHouse | да | да | `` `...` ``; таблица фиксирована, колонки должны существовать |
| InMemory | да | да | словарь строки |

## 3. C#-аналог и tier

Аналог linq2db: `DynamicColumnsStoreAttribute` + `DynamicColumnAccessorAttribute`, fluent
`EntityMappingBuilder<T>.DynamicColumnsStore(...)` / `DynamicPropertyAccessors(...)`. Tier **b**:
новый атрибут + слот в метаданных + поддержка в row reader / mutation builder. Имена согласовать с
`docs/specs/design/API-NAMING-REVIEW.md`.

## 4. Дизайн и публичный API (предложение)

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class DynamicColumnsAttribute : Attribute;

// метаданные: IPropertyMetadata.IsDynamicColumnsStore / IEntityMetadata.DynamicColumnsStore
```

- Чтение: `RowMaterializerBuilder` после материализации объявленных свойств кладёт в словарь колонки,
  которых нет в mapped set (ключ — имя колонки без квалификатора).
- Запись: `*Builder` добавляет столбцы из словаря (тип значения — из CLR-типа или `DBNull`).
- План-ключ: набор ключей словаря (порядок нормализовать) → разные планы.
- Безопасность: имена квотируются диалектом; словарь — доверенный источник (документировать).

## 5. План тестов

- Core/in-memory: чтение лишних колонок в словарь; запись словаря.
- SQL-gen: `SELECT`/`INSERT`/`UPDATE` включают динамические колонки с квотированием; разные наборы →
  разные планы.
- Интеграция (опционально): PostgreSQL/SQLite round-trip.

## 6. Файлы к изменению

- Новое: `src/nextorm.core/DynamicColumnsAttribute.cs` (или рядом с `ColumnAttribute`),
  `Meta/` слоты.
- Правки: `DataContext/Meta/*`, `DataContext/RowMaterializerBuilder.cs`,
  `DataContext/SqlMutationBuilder.cs`, `Builders/*Builder.cs`, `Query/QueryCommand.cs`,
  `Query/QueryPlanEqualityComparer.cs`.
- Доки: `docs/guide/30-value-converters.md` (+RU) или новый раздел, `docs/advanced/api-reference.md`
  (+RU), `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## 7. Открытые вопросы

1. В scope ли вообще (нет `ITable<T>`, нет change tracking)? Решение зафиксировать.
2. Тип словаря: `IDictionary<string, object?>` vs типизированный `Dictionary<string, T>`.
3. Запись: какие ключи писать по умолчанию (все) и как отличать null от отсутствия.
4. Взаимодействие с value converters и JSON-колонками.
