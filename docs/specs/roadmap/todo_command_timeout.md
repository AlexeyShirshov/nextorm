# TODO: command timeout (per-context / per-query)

> Tracking issue: [#93](https://github.com/AlexeyShirshov/nextorm/issues/93).

> Рабочий план (design RFC). Источник: сравнение инфраструктуры linq2db (Data Connection System):
> `DataOptions.UseCommandTimeout()` / `DataContextOptions.WithCommandTimeout()`. В nextorm собственного
> таймаута для обычных запросов нет — только `BulkInsertOptions.TimeoutSeconds` (SQL Server native).

## 1. Пункт и цель

- **Фича:** задать `DbCommand.CommandTimeout` для обычных команд (`SELECT`/DML/`ExecuteScalar`) на уровне
  контекста и/или отдельного запроса.
- **Критерий приёмки:**
  1. `UseCommandTimeout(seconds)` на `DataContextBuilder` влияет на все команды контекста;
  2. per-query override (`WithCommandTimeout`) сильнее контекстного;
  3. значение не «протекает» через план-кэш на другие вызовы;
  4. `null`/0 — провайдерный дефолт (текущее поведение, zero-cost);
  5. in-memory — no-op; отсутствие поддержки на провайдере — понятное поведение.
- **Обходной путь сейчас:** `IQueryInterceptor.CommandInitialized` может выставить `command.CommandTimeout`
  ([гайд 27](../../guide/27-interceptors.md:18)), но это не first-class API.

## 2. Провайдерная матрица

`DbCommand.CommandTimeout` — общий ADO.NET-контракт; конкретный эффект — на драйвере.

| Провайдер | CommandTimeout | Примечание |
|---|---|---|
| SQL Server | да | секунды; `0` = бесконечно |
| PostgreSQL | да | Npgsql `CommandTimeout` |
| MySQL / MariaDB | да | `MySqlCommand.CommandTimeout` |
| SQLite | да | фактически прерывание по таймеру |
| ClickHouse | **проверить** | HTTP-драйвер: маппится ли на HTTP timeout |
| InMemory | — | SQL нет |

## 3. C#-аналог и tier

Аналог — `System.Data.Common.DbCommand.CommandTimeout` (tier b: новое свойство конфигурации).
linq2db-имена: `UseCommandTimeout` / `WithCommandTimeout`.

## 4. Дизайн и публичный API (предложение)

```csharp
public DataContextBuilder UseCommandTimeout(int seconds);
// per-query
public EntityBuilder<TEntity> WithCommandTimeout(int seconds);
public QueryCommand<TResult> WithCommandTimeout(int seconds);
```

- Хранение: `int?` в конфигурации контекста + `int?` на `QueryCommand`.
- Применение: `DataContext/QueryExecutor` при создании команды; **не** на кэшируемой
  `DbPreparedQueryCommand` (иначе таймаут залипнет между вызовами) — выставлять перед исполнением, как
  делают интерсепторы.
- План-ключ: таймаут **не** входит (не меняет SQL), но подготовленная команда не должна «запоминать»
  чужой таймаут.
- Zero-cost: `null` → путь не меняется.

## 5. План тестов

- Core/SQLite: интерсептор/`ExecuteScalar` наблюдает `command.CommandTimeout` (контекстный и per-query).
- «Не протекает»: два запроса с разными таймаутами на одном контексте не портят друг другу команду.
- SQL-gen не нужен (SQL не меняется).
- Интеграция (опционально): реальный таймаут на PostgreSQL/SQL Server.

## 6. Файлы к изменению

- `src/nextorm.core/DI/DataContextBuilder.cs`, `DataContext/DataContext.cs`,
  `DataContext/QueryExecutor.cs`, `Builders/EntityBuilder.cs`, `Query/QueryCommand.cs`,
  `DataContext/Cache/DbPreparedQueryCommand.cs` (не запоминать таймаут).
- Доки: `docs/guide/16-connections-and-logging.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/design/API-NAMING-REVIEW.md`.

## 7. Открытые вопросы

1. Название: `UseCommandTimeout` vs `UseCommandTimeoutSeconds`.
2. Нужен ли per-query override в MVP, или достаточно контекстного.
3. ClickHouse: маппинг таймаута на HTTP (проверить драйвер).
