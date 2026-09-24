# TODO: Шардирование (consistent hashing) поверх mapping scope
> Tracking issue: [#69](https://github.com/AlexeyShirshov/nextorm/issues/69).

> Рабочий план (design RFC). Продвинутый слой: опирается на базовый
> [`todo_mapping_scope.md`](todo_mapping_scope.md) и не дублирует его механику кэшей/подмены имён.
> Источник: обсуждение шардирования по значениям свойств сущности.

## 1. Пункт и цель

- **Проблема:** нужно раскладывать данные по шардам на основе **одного или нескольких свойств
  сущности**: вычислить бакет (consistent hashing), сопоставить бакет шарду, шард — схеме/имени
  таблицы. Один коннект, разные schema/table (connection-per-shard — вне области).
- **Цель:** декларация shard key, кольцо consistent hashing, маршрутизация точки записи/чтения на шард,
  и явный сценарий сбора с нескольких шардов. Базовая механика (scope, подмена названий, префикс
  параметров) берётся из [`todo_mapping_scope.md`](todo_mapping_scope.md).
- **Критерий приёмки:** вставка N сущностей с разными значениями shard key раскладывает их по M
  таблицам/схемам согласно кольцу; point-read/update/delete с зафиксированным shard key попадает в тот
  же шард; добавление/удаление шарда перераспределяет минимум ключей (проверка на кольце); при
  отсутствии конфигурации шардинга поведение не меняется.

## 2. Почему это нужно

1. Горизонтальное масштабирование: шард на схему/таблицу, единый коннект.
2. Consistent hashing минимизирует перераспределение при изменении числа шардов.
3. Опирается на базовый слой; не форкает ядро и не дублирует кэши.

## 3. Зависимость и что берём из базового слоя

Базовый слой даёт (см. `todo_mapping_scope.md` §4):

- `MappingScope` — ключ метаданных; `IEntityScopeResolver` — переход «сущность + значения → scope»;
- `IEntityMetadataProvider` — подмена table/schema/column per scope, кэш `(Type, scope)`;
- `IEntityMetadata.Schema`, `FromExpression` со схемой;
- `PrefixedParameterProvider` + scope-aware `NormParam` — изоляция имён параметров.

Шардинг добавляет только: shard key, кольцо, топологию, маршрутизацию и сбор.

## 4. Дизайн и публичный API

### 4.1. Декларация shard key

```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = true)]
public sealed class ShardKeyAttribute(params string[] propertyNames) : Attribute;
```

- Fluent на метаданных: `EntityMetadataBuilder<T>.ShardKey(params Expression<Func<T, object>>[])`.
- В `IEntityMetadata` (через базовый provider): `IReadOnlyList<IPropertyMetadata> ShardKey => []`
  (дефолт пустой — шардинг выключен).
- Компилируемый экстрактор `Func<T, object?[]>` строится один раз на `(Type, scope)`.

### 4.2. Consistent hash ring

```csharp
/// <summary>Consistent hash ring mapping a key to a bucket and a bucket to a shard.</summary>
public interface IConsistentHashRing
{
    /// <summary>Number of buckets on the ring.</summary>
    int BucketCount { get; }

    /// <summary>Stable ring version; changes on add/remove of a shard.</summary>
    int Version { get; }

    /// <summary>Bucket for a hash of the shard-key values.</summary>
    int GetBucket(ReadOnlySpan<byte> key);

    /// <summary>Shard owning <paramref name="bucket"/>.</summary>
    string GetShard(int bucket);
}
```

- Реализация: виртуальные узлы (например 256 vnode/shard), хэш `XxHash64`, бинарный поиск по
  отсортированному массиву vnode → shard. Детерминированная между процессами (общий DB).
- `Add`/`Remove` шарда → новый `Version`, минимум перемещённых бакетов (свойство consistent hashing).
- Кэш `ShardAddress`, вычисленного для набора значений, — опционально; инвалидируется по `Version`.

### 4.3. Топология (шард → schema/table)

```csharp
public readonly record struct ShardAddress(string ShardId, int Version);

public readonly record struct ShardPlacement(string? Schema, string? TableName);

public interface IShardTopology
{
    /// <summary>Current topology version (guards placement changes).</summary>
    int Version { get; }

    /// <summary>Shard owning a bucket for the entity.</summary>
    ShardAddress GetShard(Type entityType, int bucket);

    /// <summary>Physical schema/table for a shard.</summary>
    ShardPlacement GetPlacement(Type entityType, ShardAddress shard);
}
```

- По умолчанию `TableName` — исходное имя из базовых метаданных (если `null`) или
  `{base}_{shardId}`; `Schema` — per-shard схема. Конфигурируется пользователем.

### 4.4. Роутер — реализует базовый `IEntityScopeResolver`

```csharp
public interface IShardRouter : IEntityScopeResolver
{
    /// <summary>Scope for explicit shard-key values (bypasses predicate extraction).</summary>
    MappingScope ResolveShardScope(Type entityType, IReadOnlyList<object?> shardKeyValues);

    /// <summary>Whether <paramref name="entityType"/> is sharded.</summary>
    bool IsSharded(Type entityType);
}
```

- `Resolve(type, values)` = хэш значений → бакет → `ShardAddress` → `MappingScope(Key: shard.Id)`.
- Регистрируется как `UseScopeResolver(shardRouter)`; базовая `IEntityMetadataProvider` получает
  `ShardPlacement` через `IShardTopology` (шардинг подставляет свой провайдер/декоратор имён).
- **Ключ scope = id шарда** (стабилен), а `Version` топологии участвует только в вычислении placement
  и инвалидации при перекладке схем.

### 4.5. Источники значений shard key

| Операция | Источник значений | Когда известно |
|---|---|---|
| INSERT | инстанс сущности (`InsertBuilder.Values(entity)`) | build-time |
| UPDATE/DELETE по сущности | инстанс (`Update(entity)`/`Delete(entity)`) | build-time |
| SELECT/UPDATE/DELETE по предикату | равенство по shard-key свойству в `Where` | build-time для констант и замыканий; execution-time для `params` |
| Явно | `.Shard(values)` / `.ShardKey(...)` fluent | build-time |

- **Extraction из предиката:** `ShardKeyPredicateExtractor.TryGetValues(LambdaExpression predicate,
  IReadOnlyList<IPropertyMetadata> shardKey, out object?[] values)` — ищет `prop == value` /
  `value == prop` для всех shard-key свойств, значения берёт через вычисление дерева (замыкания и
  константы доступны на build-time — query-лямбды nextorm это `Expression<Func<...>>`).
- **`params object[]` (runtime-параметры, `NormParam`)**: значения появляются только при исполнении.
  Варианты: (a) v1 — требовать явный `.Shard(...)` или shard key в замыкании; (b) фаза 2 — двухфазная
  подготовка per-shard команд (см. §6).
- Если shard key не пинован и сбор не запрошен явно — **fail-fast** (`InvalidOperationException`), а не
  молчаливый полносканирующий запрос.

### 4.6. Сбор с нескольких шардов

```csharp
public EntityBuilder<T> AcrossShards();                 // UNION ALL по затронутым шардам
public EntityBuilder<T> AcrossShards(ShardSelection sel);
```

- v1: только шарды, попавшие из предиката (`shardKey IN (...)` или диапазон) — `UNION ALL`
  per-shard-запросов; базовый `PrefixedParameterProvider` изолирует коллизии имён параметров.
- Ограничения (документировать): глобальные `ORDER BY`/`LIMIT`/`OFFSET`/`DISTINCT` и агрегаты требуют
  слияния на клиенте — либо ограниченная поддержка, либо `ShardFanOut` (N команд + merge в исполнителе).
- Полный scatter-gather по всем шардам — фаза 3 и только по явному запросу.

## 5. Точки встраивания

| Что | Точка | Файл:строка |
|---|---|---|
| Резолв scope для чтения | `From<T>`/`ResolveMetadata` через базовый `IEntityScopeResolver` | `DataContextExtensions.cs:114`, `:665` |
| Вставка | значения инстанса → scope | `Builders/InsertBuilder.Values.cs:94` |
| Update/Delete по сущности | инстанс → scope | `DataContextExtensions.cs:87` (`Update`), `:153` (`Delete`) |
| Предикат | extractor обходит `Where`-лямбду | `Builders/EntityBuilder.cs:295` (`Where`), предикат в `Query/QueryCommand.QueryPreparer.cs` |
| FROM | scope → `ShardPlacement` → table/schema | `DataContext/QueryPlanner.cs:479-481` |
| DML | scope → table/schema | `InsertBuilder.cs:421`, `UpdateBuilder.cs:211`, `DeleteBuilder.cs:151`, `MergeBuilder.cs:375`, `TruncateBuilder.cs:62` |
| Имена параметров при сборе | базовый префикс | `Query/PrefixedParameterProvider.cs`, `DataContext/NormParam.cs:12` |
| Регистрация | `UseScopeResolver`/`UseEntityMetadataProvider`/`UseShardTopology` | `DI/DataContextBuilder.cs:146` |

## 6. Этапы внедрения

- **Фаза 0:** базовый [`todo_mapping_scope.md`](todo_mapping_scope.md) Фазы 0–2 (schema + scope +
  префикс параметров).
- **Фаза 1 (MVP, build-time routing):** `ShardKey` в метаданных, `ConsistentHashRing`, `IShardTopology`,
  `IShardRouter`; маршрутизация INSERT/UPDATE/DELETE по инстансу и point-read по константе/замыканию;
  fail-fast при непокрытом shard key.
- **Фаза 2 (execution-time):** двухфазная подготовка per-shard команд / parameterized shard key из
  `params`; scoping prepared-команд (`Cache/DbPreparedQueryCommand.cs`).
- **Фаза 3 (сбор):** `AcrossShards()` (`UNION ALL`), `ShardFanOut` (N команд + merge), глобальные
  `OrderBy`/`Limit`/агрегаты на клиенте.
- **Вне области:** connection-per-shard, распределённые транзакции (2PC), rebalancing с переносом
  данных (только вычисление кольца), online-смена числа шардов без version-bump.

## 7. Ограничения и цена

- **Кэши:** ключ — id шарда (стабилен при ребалансе), `Version` топологии — только для placement.
  Prepared-команды per-shard. План-кэш `[ThreadStatic]` (`Cache/QueryPlanStore.cs:18`) — scope
  захватывается на build-time.
- **Инъекция:** имена шардовых схем/таблиц — из `IShardTopology` (доверенный), не из пользовательских
  значений shard key. Значения shard key идут только в параметры предиката, не в идентификаторы.
- **Хэш значений:** стабильная сериализация ключа (тип + разделитель) обязана быть детерминированной
  между процессами; null, строки, `Guid`, числа — каноническое представление.
- **Ребаланс:** consistent hashing перемещает ~K/N; но сменивший шард ключ окажется в «старой» таблице,
  пока данные не перенесены — это ответственность потребителя (документировать).
- **Агрегаты/пагинация при сборе:** не композируются глобально — задокументировать или отклонить.
- **Производительность:** fast-path при `!IsSharded` — тот же план, что до фичи.

## 8. План тестов

- **Core (`tests/nextorm.core.tests`):**
  - `ConsistentHashRing`: детерминизм, распределение, минимум перемещений при add/remove, version;
  - `ShardKeyPredicateExtractor`: константа, замыкание, `AND` нескольких свойств, отсутствие ключа →
    fail;
  - маршрутизация INSERT/UPDATE/DELETE по инстансу; SELECT по предикату;
  - разные shard key → разные SQL (table/schema).
- **SQL-gen (`tests/nextorm.<provider>.tests`):** `FROM "shard_2"."orders"`; `UNION ALL`-сбор.
- **Интеграция (`tests/nextorm.integration.tests/CommonTestSuite.*.cs`):** PostgreSQL + SQL Server —
  создать 3 схемы/таблицы, вставить пачку, проверить раскладку кольца, point-read/update/delete,
  сбор `AcrossShards`. Нужен `DOCKER_HOST` (skill `running-integration-tests`).
- **Покрытие:** не ниже базового; заявить before/after.

## 9. Открытые вопросы

1. **Тип кольца:** бакеты `[0, N)` vs позиции на 64-битном кольце; число vnode/shard.
2. **Автоматический extraction предиката** vs только явный `.Shard(...)` в v1 (сложность/риск).
3. **Execution-time shard key из `params`:** двухфазная подготовка vs отказ в v1.
4. **Форма `UNION ALL`-сбора:** server-side (`UNION ALL`) vs client-side `ShardFanOut` (merge в
   исполнителе) — что поддержать первым.
5. **Где хранить топологию** (in-memory конфиг vs провайдер с runtime-refresh) и как триггерить
   version-bump.
6. **Совместимость с `todo_dynamic_result_schema.md`** — не конфликтуют ли понятия «динамическая схема».
7. **Канонизация ключа хэша** для смешанных типов (десятичные `decimal`, `DateTimeOffset`, enum).

## 10. Файлы к изменению

- Новое: `src/nextorm.core/Query/Sharding/ShardKeyAttribute.cs`, `Sharding/ShardKey.cs`,
  `Sharding/ConsistentHashRing.cs`, `Sharding/ShardTopology.cs`, `Sharding/ShardRouter.cs`,
  `Sharding/ShardKeyPredicateExtractor.cs`, `Sharding/ShardPlacement.cs`.
- Правки: базовые `DataContext/Meta/*` (ShardKey в метаданных), `DataContext/QueryPlanner.cs`,
  `Builders/EntityBuilder.cs` (+`AcrossShards`), `Builders/InsertBuilder*.cs`,
  `Builders/UpdateBuilder.cs`, `Builders/DeleteBuilder.cs`, `DataContextExtensions.cs`,
  `DI/DataContextBuilder.cs`, `Query/QueryCommand.QueryPreparer.cs`, `Cache/DbPreparedQueryCommand.cs`.
- Тесты: `tests/nextorm.core.tests/ShardingTests.cs` (+ ring/extractor), SQL-gen провайдеров,
  `tests/nextorm.integration.tests/CommonTestSuite.Sharding.cs`.
- Документация: новый гайд по шардированию (+RU), `docs/providers/overview.md` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/specs/design/API-NAMING-REVIEW.md`,
  `docs/specs/design/code-smells-review.md`.
