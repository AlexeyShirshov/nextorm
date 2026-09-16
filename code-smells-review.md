# Аудит запахов кода — nextorm

**Дата:** 16.09.2026
**Область анализа:** `src/` (основной), дополнительно `test/` и `benchmarks/`
**Метод:** read-only аудит по каталогу `skill:dotnet-csharp-code-smells`
**Статус:** Находки 2 (`IDisposable`), 3 (LINQ на горячем пути) и 5 (хэш-ключи кэша) — **исправлены**. Находки 1 и 4 — открыты.

> Рабочая копия редактируется параллельно, поэтому номера строк приведены на момент проверки и могут сдвигаться.

---

## Сводка

| Категория навыка | Статус |
|---|---|
| 1. Управление ресурсами (`IDisposable`) | ⚠️ Находка 2 исправлена |
| 2. Подавление предупреждений | 🔴 Открыто: 10 `SuppressMessage` с `<Pending>` + 1 `#pragma` без `restore` |
| 3. Антипаттерны LINQ | ✅ Находка 3 исправлена |
| 4. Работа с событиями | ✅ Чисто |
| 5. Запахи проектирования | ⚠️ Открыто: 6 классов > 500 строк |
| 6. Обработка исключений | ✅ Чисто |
| 7. Хэш-ключи кэша (S2328) | ✅ Находка 5 исправлена |

---

## 🔴 Находка 1 — Подавления предупреждений (открыта)

В `Directory.Build.props` задано `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, поэтому каждое подавление «несущее»: оно либо маскирует реальную проблему, либо закрепляет осознанное отклонение — но в обоих случаях должно быть обосновано.

### `[SuppressMessage]` — 10 шт., все с `Justification = "<Pending>"`

| Файл:строка | Правило | Комментарий |
|---|---|---|
| `Builders/Entity.cs:15` | S2292 | Trivial properties should be auto-implemented |
| `DataContext/Cache/DbPreparedQueryCommand.cs:34` | S2583 | Conditionally executed code should be reachable |
| `DataContext/Cache/QueryPlan.cs:5` | S3897 | Equals без `IEquatable<T>` |
| `DataContext/DbContext.cs:226` | S2583 | Conditionally executed code should be reachable |
| `Expressions/SelectExpression.cs:7` | IDE1006 | Naming Styles |
| `Query/ExpressionPlanEqualityComparer.cs:516` | S3897 | Equals без `IEquatable<T>` |
| `Query/QueryCommand.cs:11` | S3897 | Equals без `IEquatable<T>` |
| `Query/QueryCommand.cs:632` | IDE0028 | Simplify collection initialization |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:6` | IDE1006 | Naming Styles |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:7` | S3011 | Reflection increases accessibility |

Три подавления `S2328` (`QueryPlan.cs:13`, `ExpressionCache.cs:16`, `ExpressionPlanEqualityComparer.cs:528`) сняты в рамках Находки 5: хэш теперь вычисляется один раз из `readonly`-состояния.

Правила `S2583` (недостижимый код) и `S3011` (рефлексия) относятся к корректности, а не к стилю, и потому наиболее ценны для переоценки.

### `#pragma warning disable` — 5 шт., 4 с `restore`, 1 без

| Файл:строка | Правило | `restore` |
|---|---|---|
| `Builders/Entity.cs:254` | CS8619 | ✅ `:256` |
| `Builders/Entity.cs:263` | CS8619 | ✅ `:265` |
| `Builders/Entity.cs:293` | CS8619 | ✅ `:295` |
| `Query/ExpressionPlanEqualityComparer.cs:408` | IDE0066 | ✅ `:410` |
| **`Expressions/HashCode.cs:10`** | **CA1066** | ❌ **отсутствует** |

`HashCode.cs:10` — `#pragma warning disable CA1066` без парного `restore`, то есть правило подавлено до конца файла (438 строк). Это ровно тот «blanket suppression», который запрещён разделом 2 навыка.

### Первопричина и влияние

Технический долг по анализаторам, отложенный шаблонной заглушкой `"<Pending>"`. Переоценить такие подавления невозможно: неясно, что именно проверялось и актуален ли отказ. Дополнительно: `SonarAnalyzer.CSharp` **не подключён** ни в одном `.csproj`/`Directory.Packages.props`, поэтому эти `SuppressMessage` сегодня носят чисто документационный характер и сборку не гейтят.

### Рекомендация

1. Для каждого `SuppressMessage` — либо устранить проблему, либо заменить `<Pending>` конкретным обоснованием.
2. `HashCode.cs:10` — либо добавить `#pragma warning restore CA1066`, либо сделать тип `IEquatable<T>` (или точечно подавить только нужную конструкцию).
3. В идеале — подключить анализатор пакетно и включить правила (например, `AnalysisLevel=latest-all`), чтобы подавления стали реально проверяемыми.

---

## ⚠️ Находка 2 — Корректность `IDisposable` (ИСПРАВЛЕНО)

### Было

1. `DbContext.DisposeAsync()` вызывал `DisposeStaff()` напрямую, **не выставляя `_disposed`**, и дублировал `GC.SuppressFinalize`. В результате:
   - после `DisposeAsync()` контекст оставался «живым» по флагу `_disposed` (проверка `CheckDisposed()` в DEBUG не срабатывала);
   - повторный `Dispose()`/`DisposeAsync()` повторно выполнял `DisposeStaff()` и повторно поднимал событие `Disposed`.
2. `InMemoryContext.DisposeAsync()` (`DataContext/InMemoryDataContext.cs:461`) страдал тем же дефектом: возвращал `CompletedTask`, не выставляя `_disposedValue`.
3. На `DbPreparedQueryCommand<TResult>` стояло устаревшее подавление `S3881 "IDisposable should be implemented correctly"`, хотя ни сам класс, ни его базовый `PreparedQueryCommand` не реализуют `IDisposable`.

### Стало

- `DataContext/DbContext.cs` — `DisposeAsync()` теперь идёт через `Dispose()`:
  ```csharp
  public ValueTask DisposeAsync()
  {
      // Route through Dispose() so the _disposed guard is honored and cleanup runs
      // exactly once, matching the synchronous disposal path (CA1816).
      Dispose();
      return ValueTask.CompletedTask;
  }
  ```
- `DataContext/InMemoryDataContext.cs` — `DisposeAsync()` аналогично идёт через `Dispose()` и выставляет `_disposedValue`.
- `DataContext/Cache/DbPreparedQueryCommand.cs` — удалено устаревшее подавление `S3881`. Подавление `S2583` на этом же классе оставлено (относится к Находке 1).

### Ожидаемый побочный эффект

- После `DisposeAsync()` `_disposed == true`, поэтому в DEBUG `CheckDisposed()` (`DbContext.cs:927`) теперь корректно бросает `ObjectDisposedException` при обращении к освобождённому контексту. Использований «dispose async → продолжение работы» в `src/` и `test/` не найдено.
- Повторное освобождение — no-op, событие `Disposed` поднимается ровно один раз.

### Проверка

- `dotnet build src/nextorm.core/nextorm.core.csproj` — **0 warnings, 0 errors**.
- Тесты ядра (xUnit v3, запуск сборки напрямую) — **83/83 passed, 0 failed**.

---

## ✅ Находка 3 — LINQ на горячем пути (ИСПРАВЛЕНО)

### Было

1. `ExpressionExtensions.cs:135`:
   ```csharp
   && _outerParams?.Count() > 0 && _outerParams.Contains(param))
   ```
   `_outerParams` — `IEnumerable<ParameterExpression>` (`:85`). Здесь сразу `CA1827` (вместо `Any()`) и `CA1851` (двойной перебор: `Count()`, затем `Contains()`) на пути, проходящем при каждом обращении к члену во время трансляции выражений.

2. `Visitors/BaseExpressionVisitor.cs:782`: `args.Count()`, где `args` — `Type[]` из `GetGenericArguments()` (`CA1829`).

### Стало

- `ExpressionExtensions.cs:135` — один проход, без подсчёта:
  ```csharp
  && _outerParams is not null && _outerParams.Contains(param))
  ```
- `Visitors/BaseExpressionVisitor.cs:782` — `args.Count()` → `args.Length`. Соседний `args.Distinct().Count()` оставлен: он действительно требует LINQ.

### Проверка

- `dotnet build src/nextorm.core/nextorm.core.csproj` — **0 warnings, 0 errors**.
- Тесты ядра (xUnit v3) — **84/84 passed, 0 failed**.

---

## ⚠️ Находка 4 — God-классы (открыта)

Выше порога раздела 5 навыка (>500 строк):

| Файл | Строк |
|---|---|
| `DataContext/DbContext.cs` | 1322 |
| `Query/QueryCommand.cs` | 1043 |
| `Visitors/BaseExpressionVisitor.cs` | 1035 |
| `DataContext/InMemoryDataContext.cs` | 1002 |
| `Query/ExpressionPlanEqualityComparer.cs` | 893 |
| `Builders/Entity.cs` | 596 |

Для движка запросов часть этого ожидаема, но масштаб стоит держать под контролем и извлекать связные классы по SRP.

---

## ✅ Находка 5 — Хэш-ключи кэша (ИСПРАВЛЕНО)

### Проверка инварианта (и найденная ошибка)

Подтверждено: `QueryPlan.GetHashCode()` **действительно вызывается до** `GetCacheVersion()` — через `QueryPlanCache.TryGetValue(new QueryPlanCacheKey(GetType(), queryPlan), ...)` (`DbContext.cs:310`) и `_cmdIdx.TryGetValue(queryPlan, ...)` (`InMemoryDataContext.cs:64`). То есть `_hashPlan` кэшировался по исходной команде, а `GetCacheVersion()` затем подменял `QueryCommand`/`_comparer` клоном.

`Debug.Assert` в `QueryPlan.cs` должен был подтверждать хэш-эквивалентность клона, **но в ветке `_sql == null` (in-memory и обычный SQL-путь) assert был вакуумным**: сравнивал `GetHashCode()` со значением, посчитанным по ещё не подменённому `QueryCommand`, т. е. сам с собой. Как только assert сделали настоящим, упали три теста (`TestWhere_Subquery`, `SelectAny_ShouldReturnData`, `SelectAny2_ShouldReturnData`): **`Equals(clone, original) == true`, но хэши разные** — нарушение контракта `Equals ⇒ одинаковый хэш`.

**Корневая причина:** `QueryCommand.CopyTo(dst, copyAll: false)` (`QueryCommand.cs`) не копировал hash-значимые поля `FromPlanHash`, `UnionPlanHash`, `ReferencedQueriesPlanHash`, которые читает `QueryPlanEqualityComparer.GetHashCode()`. У клона они обнулялись. Раньше это маскировалось тем, что `_hashPlan` «замерзал» на исходном значении, поэтому промахов кэша не было заметно, хотя ключ в словаре переставал соответствовать собственному `Equals`.

### Стало

- `Query/QueryCommand.cs` — `CopyTo` переносит `FromPlanHash`, `UnionPlanHash`, `ReferencedQueriesPlanHash` (восстановлен контракт `Equals ⇒ одинаковый хэш`).
- `DataContext/Cache/QueryPlan.cs` — хэш вычисляется один раз из состояния на момент конструирования и хранится в `readonly int _hashPlan`: `GetCacheVersion()` больше не может оставить устаревший хэш, `GetHashCode` стабилен на всю жизнь ключа.
- `DataContext/Cache/QueryPlan.cs` — вакуумный assert заменён настоящим (`_hashPlan == ComputeHash(newCmd, ...)`), инвариант теперь реально проверяется в DEBUG.
- `ExpressionCache.cs` (`ExpressionKey`) и `Query/ExpressionPlanEqualityComparer.cs` (`QueryCommandKey`) — входные поля там и так `readonly`; хэш тоже считается один раз в `readonly`-поле, ложные подавления `S2328` сняты.
- Регрессионный тест `InMemoryTests.QueryPlan_HashMustStayStableAcrossGetCacheVersion` фиксирует: хэш не меняется после `GetCacheVersion()`, а логически равный свежий план находит кэш в `Dictionary<QueryPlan, object>`.

### Про глобальный кэш — исходная тревога не подтвердилась

`DataContextCache` (`DataContextCache.cs`) ключуется по `Type` (`_metadata`, `_selectListCache`) — размер ограничен числом типов модели, утечки нет. Поле `_expCache` там объявлено, но внутри библиотеки не используется: `InMemoryDataContext` держит **свои экземплярные** кэши (`InMemoryDataContext.cs:20-22`), живущие вместе с контекстом. Политика вытеснения не требуется. Наблюдение на будущее: `InMemoryDataContext.PurgeQueryCache()` (`:984`) очищает только `_cmdIdx`, но не `_expCache`/`_conditionFactoryCache`/`_conditionDirectCache`.

### Проверка

- `dotnet build nextorm.sln -c Debug` — **0 warnings, 0 errors**.
- Тесты ядра (xUnit v3) — **85/85 passed, 0 failed** (три падавших — подзапросные формы).

---

## Чистые категории

- **4. Работа с событиями** — событий в `src/` не найдено (кроме корректно реализованного `Disposed` в `DbContext`).
- **6. Обработка исключений** — нет `throw ex;` (CA2200), нет пустых и общих `catch` в `src/`, нет `async void`. Два `catch (Exception)` в тестовых контейнерах (`test/nextorm.integration.tests/Providers/SqlServerContainer.cs:89`, `PostgresContainer.cs:89`) — законный перехват с сохранением сообщения, не запах.

---

## Примечания

- `SonarAnalyzer.CSharp` в репозитории не подключён, поэтому идентификаторы правил `S####` в `SuppressMessage` не проверяются сборкой.
- Рабочая копия редактировалась параллельно (незакоммиченные изменения и посторонние файлы вроде `solid-review.md`); номера строк в отчёте актуальны на 16.09.2026.
- Правки по Находкам 2, 3 и 5 не закоммичены — по `AGENTS.md` коммит только по явному запросу.

## Следующие шаги (предложение)

1. Находка 1: заменить `<Pending>` на обоснования или снять подавления (осталось 10); закрыть `CA1066` в `HashCode.cs`.
2. Находка 4: при случае декомпозировать god-классы по SRP.

## Ссылки

- [skill:dotnet-csharp-code-smells]
- [Microsoft Code Quality Rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
- [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
