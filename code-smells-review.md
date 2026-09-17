# Аудит запахов кода — nextorm

**Дата:** 16.09.2026 (повторный аудит после коммита `9641660` «mass refactoring»)
**Область анализа:** `src/` (основной), дополнительно `test/` и `benchmarks/`
**Метод:** read-only аудит по каталогу `skill:dotnet-csharp-code-smells`
**Статус:** Находки 1 (подавления), 2 (`IDisposable`), 3 (LINQ) и 5 (хэш-ключи) — **обработаны**. Открыта только Находка 4 (god-классы).

> Номера строк приведены на момент повторной проверки (HEAD `9641660`); дерево на момент аудита чистое.

---

## Сводка

| Категория навыка | Статус |
|---|---|
| 1. Управление ресурсами (`IDisposable`) | ✅ Находка 2 исправлена; 1 минорное наблюдение |
| 2. Подавление предупреждений | ✅ 5 `SuppressMessage` с обоснованием (0 `<Pending>`), все `#pragma` с `restore` |
| 3. Антипаттерны LINQ | ✅ Чисто |
| 4. Работа с событиями | ✅ Чисто |
| 5. Запахи проектирования | ⚠️ Открыто: 4 класса > 500 строк + длинные списки параметров |
| 6. Обработка исключений | ✅ Чисто |
| 7. Хэш-ключи кэша (S2328) | ✅ Находка 5 исправлена |

---

## 🔎 Повторный аудит (HEAD `9641660` «mass refactoring»)

Прогон по всем пунктам заново, на чистом дереве (все прежние правки закоммичены):

| Пункт | Результат |
|---|---|
| 1. `IDisposable` | ✅ Фиксы Находки 2 держатся (`DbContext.DisposeAsync`, `InMemoryDataContext.DisposeAsync` идут через `Dispose()`). Минорное наблюдение — `ResultSetEnumerator.DisposeAsync` (см. ниже). |
| 2. Подавления | 🔴 На момент прогона: 10 `SuppressMessage` с `<Pending>`; **устранено следом** — см. Находку 1. |
| 3. LINQ | ✅ Чисто: остались только законные `Distinct().Count()` (`SqlBuilder.cs:262`, `BaseExpressionVisitor.cs:782`) и необходимый `.ToList()` (`QueryCommand.cs:703`). |
| 4. События | ✅ Чисто: единственное событие — `DbContext.Disposed`; подписки `StateChange`/`Disposed` снимаются симметрично. |
| 5. Проектирование | ⚠️ 4 god-класса + конструкторы с 6+ параметрами: `BaseExpressionVisitor` (11), `QueryCommand` (10), `WhereExpressionVisitor` (10), `SqlBuilder` (8), `DbPreparedQueryCommand` (6). |
| 6. Исключения | ✅ Чисто: в `src/` нет ни одного `catch`; `async void` и `throw ex;` отсутствуют. |
| Хэш-ключи (Находка 5) | ✅ `S2328` в коде — 0, правки держатся; регрессионный тест проходит. |

Сборка: `dotnet build nextorm.sln -c Debug` — **0 warnings, 0 errors**. Тесты ядра — **90/90 passed**.

---

## ✅ Находка 1 — Подавления предупреждений (ОБРАБОТАНО)

В `Directory.Build.props` задано `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, поэтому каждое подавление «несущее». Итог разбора: **`<Pending>` не осталось ни одного** — 6 подавлений снято, 5 оставшихся имеют конкретное обоснование.

### Снято (6)

| Было | Что сделано |
|---|---|
| `Expressions/HashCode.cs:10` — `#pragma warning disable CA1066` без `restore` | Директива удалена целиком: `HashCode` не переопределяет `Equals`, и CA1066 при `AnalysisLevel=latest-all` не срабатывает (проверено сборкой). |
| `DataContext/Cache/QueryPlan.cs:5` — S3897 | Класс реализует `IEquatable<QueryPlan>` (метод `Equals(QueryPlan?)` уже был). |
| `Query/ExpressionPlanEqualityComparer.cs:516` — S3897 | `QueryCommandKey` реализует `IEquatable<QueryCommandKey>`. |
| `Query/QueryCommand.cs:11` — S3897 | Удалено как неприменимое: `QueryCommand` не определяет ни `Equals`, ни `GetHashCode`. |
| `Query/QueryCommand.cs:632` — IDE0028 | Код приведён к `_referencedQueries ??= [];`; подавление не нужно. |
| `Builders/Entity.cs:15` — S2292 | Снято как избыточное: в `.editorconfig` уже стоит `dotnet_diagnostic.S2292.severity = silent`, а `SonarAnalyzer.CSharp` не подключён — правило не может сработать. |

### Оставлено с обоснованием (5)

| Файл:строка | Правило | Обоснование |
|---|---|---|
| `DataContext/Cache/DbPreparedQueryCommand.cs:34` | S2583 | Обе ветки достижимы: `@params` может быть `null`; анализатор не моделирует преобразование в `ReadOnlySpan`. |
| `DataContext/DbContext.cs:226` | S2583 | Состояние пула соединений заранее неизвестно — проверка `ConnectionState.Closed` достижима с обеих сторон. |
| `Expressions/SelectExpression.cs:7` | IDE1006 | `GetInt32MI` и подобные — намеренный PascalCase для immutable-таблиц рефлексии; правило лишь suggestion. |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:6` | IDE1006 | То же для `AnyMIGeneric`, `ConcatMI` и др. |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:7` | S3011 | Рефлексия нужна, чтобы привязать приватные `Any`/`Concat` в деревья выражений; публичной альтернативы нет. |

### `#pragma warning disable` — 4 шт., все с `restore`

| Файл:строка | Правило | `restore` |
|---|---|---|
| `Builders/Entity.cs:254` | CS8619 | ✅ `:256` |
| `Builders/Entity.cs:263` | CS8619 | ✅ `:265` |
| `Builders/Entity.cs:293` | CS8619 | ✅ `:295` |
| `Query/ExpressionPlanEqualityComparer.cs:408` | IDE0066 | ✅ `:410` |

«Blanket suppression» `HashCode.cs:10` больше нет: остались только парные `disable`/`restore` на несколько строк.

### Как проверялось

`SonarAnalyzer.CSharp` не подключён, поэтому S-правила недоступны; IDE/CA-правила проверены прогоном сборки с повышенным уровнем (без правки файлов — только свойства MSBuild):

```
dotnet build src/nextorm.core/nextorm.core.csproj -t:Rebuild \
  -p:TreatWarningsAsErrors=false -p:EnforceCodeStyleInBuild=true -p:AnalysisLevel=latest-all
```

Результат: `CA1066`, `IDE1006`, `IDE0028` не срабатывают вовсе; `S2292` в `.editorconfig` уже помечен `silent`, а `S2292`/`S2583`/`S3011`/`S3897` — правила Sonar. Прогон также выявил `CA2213`/`CA1508`/`CA1816` — они разобраны отдельным разделом ниже.

### Проверка

- `dotnet build nextorm.sln -c Debug` — **0 warnings, 0 errors**.
- Тесты ядра — **90/90 passed**.
- Итог: `SuppressMessage` — 5 (все с обоснованием), `<Pending>` — 0; `#pragma disable` — 4, все с `restore`.

---

## ✅ Дополнительно — CA2213 и CA1508 (найдены прогоном анализаторов)

Оба правила не входят в набор по умолчанию (видны только при `AnalysisLevel=latest-all`), но указывали на реальные вещи.

### CA1508 «недостижимое условие» — исправлено (3/3)

| Место | Что было | Что сделано |
|---|---|---|
| `Query/ExpressionPlanEqualityComparer.cs:145` | В `Compare2` ветка `_ => left is null ? right is null : ...`, хотя `left` уже гарантированно не `null` вызывающими (`Equals`/`Compare` проверяют раньше) | `Compare2(Expression? left, Expression? right)` → `Compare2(Expression left, Expression right)`; мёртвая проверка удалена. |
| `Visitors/BaseExpressionVisitor.cs:702` | `node.Expression?.Type == typeof(TableColumn)` — после `else if (node.Expression is null)` выражение не может быть `null` | `node.Expression.Type`. |
| `Visitors/BaseExpressionVisitor.cs:749` | `if (key is not null)` — `key` присваивается безусловно | `ExpressionKey? key = null; key = new ...` → `var key = new ...`; проверка и `key!` убраны. |

После правок: `CA1508` в `src/` — **0**.

### CA2213 «disposable-поле не освобождается» — 1 реальный, 1 ложный

- **Реальный:** `DataContext/InMemoryEnumerator.cs` — поле `_enumerator` (`IEnumerator<TEntity>`, создаётся для произвольных `IEnumerable`-источников в `Init`) **не освобождалось**: `Dispose()`/`DisposeAsync()` только вызывали `GC.SuppressFinalize`. Исправлено: `Dispose()` вызывает `_enumerator?.Dispose()` и обнуляет поле, `DisposeAsync()` идёт через `Dispose()` (попутно снимает прежнее наблюдение «no-op dispose» из Находки 2).
- **Ложное (по владению):** `DataContext/ResultSetEnumerator.cs` — поле `_conn` берётся из `DbContext.GetConnection()` (`:144`) и освобождается самим контекстом (`DbContext.DisposeStaff`); `Dispose()` в перечислителе закрыл бы соединение, всё ещё используемое контекстом. Поле задокументировано как невладеемое и только обнуляется.

После правок: `CA2213` в `src/` — 1, и это `_conn` с обоснованием по владению.

### CA1816 — оставлено как ложное

`DbContext.DisposeAsync`/`InMemoryDataContext.DisposeAsync` идут через `Dispose()`, который вызывает `GC.SuppressFinalize(this)`; анализатор не видит вызов через индирекцию. Функционально инвариант соблюдён.

### Проверка

- `dotnet build nextorm.sln -c Debug` — **0 warnings, 0 errors**.
- Тесты ядра — **90/90 passed**.
- Повторный прогон `AnalysisLevel=latest-all`: `CA1508` — 0, `CA2213` — 1 (`_conn`, by design).

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
- Тесты ядра (xUnit v3, запуск сборки напрямую) — **83/83 passed, 0 failed** (на момент правки).

### Остаточное наблюдение (повторный аудит)

`DataContext/ResultSetEnumerator.cs:55` — `DisposeAsync()` не идёт через `Dispose(bool)` и не выставляет `_disposed`: он асинхронно освобождает `_reader` и зануляет `_reader`/`_conn`. Идемпотентность сохраняется (повторный вызов видит `_reader == null`), а `_disposed` читается только внутри `Dispose(bool)` (`:245`), поэтому функционального дефекта нет — это несогласованность стиля, а не баг. Если захочется единообразия: `DisposeAsync()` → `await DisposeAsyncCore()` + общий `_disposed`-guard.

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

## ⚠️ Находка 4 — God-классы (открыта, 4 шт.)

Порог раздела 5 навыка — >500 строк на **класс** (не на файл). Замер на текущем рабочем дереве (после серии правок):

| Класс | Файл:строка объявления | Строк класса |
|---|---|---:|
| `BaseExpressionVisitor` | `Visitors/BaseExpressionVisitor.cs:10` | ~2212 |
| `InMemoryContext` (`partial`) | `DataContext/InMemoryDataContext.cs:10` | ~1088 |
| `DbContext` | `DataContext/DbContext.cs:13` | ~1070 |
| `SqlBuilder` (**`struct`**) | `DataContext/SqlBuilder.cs:8` | ~601 |

`QueryCommand` из списка выбыл — см. «Сделано» ниже.

Динамика с прошлого замера: `BaseExpressionVisitor` 1026 → **2221** строк (+1264 к коммиту `9641660`, +34 метода — `VisitConditional`/`VisitSwitch`, `VisitNot`/`VisitOnesComplement`/`VisitUnaryOperator`, `TranslateInValues`, `BuildLikePattern`, `VisitToString` и др.); `InMemoryDataContext` 983 → 1097; `DbContext` 1247 → **1082** (уменьшился). **`SqlBuilder` стал новым кандидатом** (был < 500, теперь 608) — и это `struct`, что усугубляет: копирование по значению большого изменяемого типа.

### Сделано: `QueryCommand` разбит на partial + вынесен `QueryPreparer`

**Вариант A (организационный, поведение не менялось).** Было — один файл 1192 строки; стало 6 файлов (один содержит вложенный тип):

| Файл | Строк | Ответственность |
|---|---:|---|
| `Query/QueryCommand.cs` | 201 | модель: состояние, свойства, ctor, `ResetPreparation`, tree-ops (`ReplaceCommand`/`AddCommand`/`SetOperation`/`AddOuterReference`) |
| `Query/QueryCommand.Prepare.cs` | 13 | точки входа `PrepareCommand` (тонкая делегация в `QueryPreparer`) |
| `Query/QueryCommand.Plan.cs` | 19 | forwarder `RefreshInValuesShape` + 6 фабрик plan-компараторов |
| `Query/QueryCommand.Clone.cs` | 112 | `CopyTo`/`CreateSelf`/`CreateSelfForClone`/`CloneForCache`/`Clone` |
| `Query/QueryCommand.QueryPreparer.cs` | 471 | `QueryPreparer` — конвейер подготовки (Вариант B) |
| `Query/QueryCommand.TResult.cs` | 314 | generic-фасад исполнения (`QueryCommand<TResult>`) |

**Вариант B (SRP).** Конвейер подготовки вынесен из `QueryCommand` в отдельный вложенный тип `QueryCommand.QueryPreparer` (`internal static`): `Prepare`, `RefreshInValuesShape`, `PrepareFrom`/`Ctes`/`Columns`/`Join`/`Sorting`/`Where`/`Grouping`. `PrepareCommand` теперь только делегирует:

```csharp
public virtual void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
    => QueryPreparer.Prepare(this, dontCalculateHash, cancellationToken);
```

Размеры типов: `QueryCommand` **~775 → 345** строк (4 partial-файла, без вложенного типа), `QueryPreparer` — **~450** строк. Оба ниже порога 500, поэтому `QueryCommand` больше не god-класс. `QueryCommand<TResult>` — 314 строк отдельным файлом.

Почему nested, а не top-level: конвейер читает и пишет `private`/`protected` состояние команды (`_exp`, `_condition`, `PreparedCondition`, `_whereBasePlanHash`, per-part хэши, `InValues*`, ...). Top-level helper заставил бы `protected`-поля (`_exp`, `_condition`, `_groupExp`, `_having`, `_sorting`, `_isPrepared`, `_srcType`, `_selectList`, `_from`, `_dataContext`) стать `internal`, что ломает внешние наследники `QueryCommand`. Вложенный тип сохраняет доступ, не расширяя API. Оговорка: при подсчёте «строк класса» вложенный тип могут включить в `QueryCommand` (тогда ~795) — если это неприемлемо, следующий шаг — промоушен `QueryPreparer` в top-level с осознанным расширением доступа.

Поведение не менялось: `PrepareCommand` остаётся `virtual` и единственной публичной точкой входа, рекурсивные подготовки (`_union`, `from.SubQuery`, CTE-запросы) по-прежнему идут через виртуальный `PrepareCommand`, поэтому override в `QueryCommand<TResult>` (`ResultPlanHash`/`ResultType`) работает как раньше. Хэш-арифметика, порядок вызовов, sentinel `7`, `unchecked`-блоки и проверки отмены перенесены дословно.

Одновременно удалён мёртвый закомментированный код: **147 → 14** строк построчных `//` (минус 133 строки; ещё +4 строки обоснования добавил параллельный фикс union-хэша, см. Находку 5). Остались только пояснительные комментарии-обоснования (инвариант хэшей клона, владение CTE-клонами в кэше, `IgnoreColumns`/`Paging.Limit` и plan key, форма in-values) и XML-доки. Удалены, в частности, `_columnsHash`/`_joinHash`/`_sortingHash`/`_whereHash`, блок `PLAN_CACHE`, закомментированный `DataProvider`, `_preciseExpressionComparer`, `//public bool CacheList`, мёртвый `FindSourceFromAlias` и десятки `// selList.Add(...)`/`columnsHash = ...`. Заодно устранён «повисший» XML-doc `RefreshInValuesShape`, который при первом сплите оказался приклеен к `PrepareGrouping`.

Границы partial-класса: `QueryCommand` объявлен `partial` в 4 файлах, `QueryCommand<TResult>` — `sealed partial` в пятом. Публичный API не менялся; внешних наследников `QueryCommand` в репозитории нет.

### Исключения (по решению автора)

| Класс | Строк класса | Почему исключён |
|---|---|---|
| `ExpressionPlanEqualityComparer` | ~880 | Размер — следствие полноты дерева `Expression`, а не смешения ответственностей: один `Compare*`-метод на тип узла. Декомпозиция выигрыша не даёт. |
| `Entity<TEntity>` | ~490 | По пересчёту на класс — **ниже порога**: >500 давал файл из-за второго типа `Entity` (~97 строк). Формально исключать больше нечего. |

Для движка запросов часть размера ожидаема, но оставшиеся 4 класса стоит держать под контролем и извлекать связные классы по SRP. Наибольший риск — `BaseExpressionVisitor`: он растёт быстрее всех (каждая новая SQL-возможность идёт в один класс) и уже в ~4.4× выше порога.

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
- Тесты ядра (xUnit v3) — **85/85 passed** на момент правки; повторный аудит (90 тестов) — **90/90 passed** (три падавших тогда — подзапросные формы).

---

## Чистые категории

- **4. Работа с событиями** — единственное событие `DbContext.Disposed` (`:83`); подписки `_conn.StateChange`/`_conn.Disposed` снимаются при dispose. Утечек нет.
- **6. Обработка исключений** — в `src/` нет ни одного `catch` (проверено повторно), значит нет пустых/общих обработчиков, log-and-swallow и `throw ex;` (CA2200); `async void` отсутствует. Два `catch (Exception)` в тестовых контейнерах (`test/nextorm.integration.tests/Providers/SqlServerContainer.cs:89`, `PostgresContainer.cs:89`) — законный перехват с сохранением сообщения, не запах.

---

## Примечания

- `SonarAnalyzer.CSharp` в репозитории не подключён, поэтому идентификаторы правил `S####` в `SuppressMessage` не проверяются сборкой. `Directory.Build.props` задаёт только `TreatWarningsAsErrors=true` (без `AnalysisLevel=latest-all`), так что CA-правила навыка сборкой не гейтятся.
- На момент повторного аудита дерево чистое: правки по Находкам 2, 3 и 5 вошли в коммит `9641660`.
- Повторно в рамках этого отчёта коммитов не делалось — по `AGENTS.md` коммит только по явному запросу.

## Следующие шаги (предложение)

1. Находка 4: `QueryCommand` закрыт по размеру — partial-разбивка (Вариант A) + вынос конвейера в `QueryPreparer` (Вариант B): 775 → 345 строк, `QueryPreparer` ~450. Осталось решить, промоутить ли `QueryPreparer` в top-level тип (потребует расширения `protected`-доступа), и по желанию — `PlanComparerSet` (D) и хэши в `PlanHashes` (C, рискованно — ключ кэша).
2. Декомпозировать по SRP остальные god-классы (в первую очередь `BaseExpressionVisitor` — ~2212 строк).
3. `CA2213` (`_conn` — ложное по владению) и `CA1816` (ложное из-за индирекции) закрывать не требуется; см. раздел «Дополнительно».

## Ссылки

- [skill:dotnet-csharp-code-smells]
- [Microsoft Code Quality Rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
- [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)
