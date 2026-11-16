# TODO: Шардирование (consistent hashing) поверх mapping variant
> Tracking issue: [#69](https://github.com/AlexeyShirshov/nextorm/issues/69).
> Шардирование — частный случай общего слоя роутинга данных, вынесенного в
> [#394](https://github.com/AlexeyShirshov/nextorm/issues/394)
> (`docs/specs/design/issue-394-data-routing.md`); `IShardTopology` станет производным от
> общего `IHostTopology`.

> **МОДЕЛЬ v2.** Прежний текст этого файла трактовал шард как «схема/таблица на одном соединении» и
> вводил виртуальные узлы. Это отменено: шард — физический сервер (`connection-per-shard` в области),
> бакет — фиксированная единица размещения, не зависящая от числа шардов, виртуальных узлов нет.

## 1. Пункт и цель

- **Проблема:** горизонтальное масштабирование — данные раскладываются по нескольким физическим
  серверам по одному или нескольким свойствам сущности, при этом добавление сервера не должно
  менять физические имена данных и не должно требовать перекомпиляции/деплоя приложения.
- **Модель:**
  - **Шард** — физический сервер со своим соединением.
  - **Бакет** — фиксированная единица размещения: задаётся схемой либо именем таблицы (режим
    выбирается один раз, когда создаётся физическая схема данных). Число бакетов фиксировано и от
    числа шардов не зависит.
  - **bucket → shard** — consistent hashing. Виртуальных узлов нет.
  - Изменение числа шардов меняет только маршрут `bucket → shard`: бакет с тем же id уезжает на
    другой сервер, имя его схемы/таблицы остаётся прежним.
- **Критерий приёмки:** вставка N сущностей с разными значениями shard key раскладывает их по
  бакетам и серверам согласно кольцу; point-read/update/delete с зафиксированным shard key попадает
  на тот же сервер; добавление/удаление шарда перемещает минимум бакетов и не меняет
  `bucket → schema/table`; при отсутствии конфигурации шардинга поведение не меняется.

## 2. Почему так

1. Горизонтальное масштабирование — это добавление серверов, а не переименование таблиц.
2. Consistent hashing минимизирует перемещение бакетов при изменении числа шардов, поэтому топология
   меняется без DDL и без деплоя приложения.
3. Бакет как стабильная единица размещения отделяет физическую схему данных от текущего парка
   серверов: парк меняется, схема данных — нет.

## 3. Инварианты

- `(entity, bucket) → schema/table` не зависит от состава шардов и версии топологии.
- При `AddShard`/`RemoveShard` меняется только `bucket → shard`.
- Ключ → бакет детерминирован между процессами и после перезапуска.
- Значение ключа не сравнивается с id шарда; id шарда не попадает в имя схемы/таблицы.
- Планы, метаданные и префикс параметров ключуются по бакету (mapping variant = bucket).

## 4. Публичный API (как реализовано)

```csharp
var topology = new ShardTopology(bucketCount: 4096);              // BucketPlacementMode.SchemaPerBucket
topology.AddShard("server-eu-1", "Host=eu-1;Database=app");
topology.AddShard("server-eu-2", "Host=eu-2;Database=app");

// не адрес маршрутизации: только нешардированные сущности, сырой SQL и транзакции
var builder = new DataContextBuilder()
    .UseSharding(topology)
    .UseDataRouting(routing => routing.UseDefaultTopology("main", "Host=eu-1;Database=app_master"));

using var context = new PostgresDataContext(builder);
```

- `IShardTopology`: `Version`, `Ring`, `ShardIds`, `PlacementMode`, `GetConnectionString(shardId)`;
  `ShardTopology(bucketCount, placementMode = SchemaPerBucket)` + `AddShard(shardId, connectionString)` /
  `RemoveShard(shardId)`. Число бакетов обязательно, значения по умолчанию нет.
- `IConsistentHashRing` (без vnode): `GetBucket(keyBytes)` — стабильный номер бакета;
  `GetShard(bucket)` — владелец по rendezvous/HRW по `(bucket, shardId)`; добавление `(K+1)`-го
  шарда забирает только его бакеты и не перетасовывает остальные. `ConsistentHashRing` принимает
  пользовательские делегаты `BucketHash`/`ShardHash`; `UseSharding` принимает любую реализацию
  `IShardTopology`, поэтому своё кольцо подключается собственной топологией.
- `BucketPlacementMode`: `TableSuffix` (`table = {mappedTable}_{bucket}`) либо `SchemaPerBucket`
  (`schema = {mappedSchema}_{bucket}`); имена зависят только от бакета.
- `ShardRouter : IEntityVariantResolver` → `MappingVariant(bucket.ToString())`.
- Исполнение: соединение на шард (`DataContext.ManagerFor(scope)`); команда выполняется на соединении
  сервера-владельца бакета, DML несёт `MappingVariant` в `MutationCommand`.
- Fan-out: `AcrossBuckets(int? parallelism = null)` → `ShardQuery<TEntity>` / `ShardQuery<TEntity,
  TResult>`. Результат — массив кардинальности «число бакетов», каждый элемент — вектор строк этого
  бакета (`List<T>[]`, `T[][]`); библиотека ничего не сливает. Скаляры — `int[] Count()`,
  `bool[] Any()`, `T[] First()`, `T[] Single()`. `Where`/`OrderBy`/`Distinct`/`Select` применяются на
  бакет. `AsAsyncEnumerable()` отдаёт векторы по одному в порядке бакетов.
- Параллелизм: бакеты группируются по шарду-владельцу; внутри шарда — последовательно, между
  шардами — параллельно (`Parallel.ForEach` / `SemaphoreSlim`), лимит по умолчанию = число шардов,
  значение ограничивается `[1, shardCount]`.

## 5. Критерии приёмки (измеримые)

- K=2, 4096 бакетов: доля бакетов каждого шарда в диапазоне 45–55 %.
- `AddShard`: перемещается не более `1/(K+1)` + 5 % бакетов.
- Снимок `bucket → schema/table` идентичен до и после `AddShard`/`RemoveShard` (тест).
- INSERT/UPDATE/DELETE и point-read выполняются на соединении сервера-владельца (проверка на живых
  БД: два файла SQLite и два database одного контейнера PostgreSQL).
- `AcrossBuckets` возвращает вектор на бакет; скалярные терминалы — значение на бакет; ничего не
  сливается.
- Шарды выполняются параллельно, бакеты одного шарда — последовательно (счётчик одновременных
  исполнений в тесте).
- В публичном API и в гайде нет «виртуальных узлов».

## 6. Что переделано относительно v1

- `ConsistentHashRing` (виртуальные узлы) → кольцо бакетов с rendezvous-хешем и пользовательскими
  `BucketHash`/`ShardHash`.
- `ShardTopology.AddShard(shardId, schema, tableName)` → `AddShard(shardId, connectionString)`.
- `ShardEntityMetadataProvider` — размещение по бакету, а не по id шарда.
- `DataContext`/`DataContextBuilder` — соединение на шард, маршрутизация команды на владельца бакета;
  DML-команда несёт scope (иначе UPDATE попадал на соединение по умолчанию).
- Скалярные терминалы на бакетном builder не используют общие артефакты/команду `Any` без scope.
- `AcrossShards()`/`Concatenate` → `AcrossBuckets()` с векторами по бакетам.
- Тесты, live-сьюты, гайд (`docs/guide/35-*`, `docs/ru/guide/35-*`).

## 7. Ограничения

- Глобальные `ORDER BY`/`LIMIT`/`OFFSET`/`DISTINCT`/агрегаты на fan-out не композируются — выполняются
  на одном сервере либо сливаются на клиенте.
- Распределённые транзакции (2PC) вне области; транзакция живёт в пределах одного шарда.
- Перенос самих данных при изменении состава шардов — ответственность приложения (имена при этом
  стабильны).
