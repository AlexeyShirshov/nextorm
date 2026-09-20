# Аудит запахов кода — nextorm

**Дата:** 16.09.2026 (повторный аудит после коммита `9641660` «mass refactoring»); **актуализация 18.09.2026 по HEAD `d21c473`**
**Область анализа:** `src/` (основной), дополнительно `tests/` и `benchmarks/`
**Метод:** read-only аудит по каталогу `skill:dotnet-csharp-code-smells` + `skill:slopwatch` (паттерн-скан выполнен вручную: локальный tool `slopwatch` в `.config/dotnet-tools.json` не установлен)
**Статус:** Находки 1 (подавления), 2 (`IDisposable`), 3 (LINQ), 4 (god-классы), 5 (хэш-ключи), **6 (утечка подписки внешнего соединения)** и **7 (`*DELETE*.cs`)** — **исправлены/закрыты 18.09.2026**. Находка 4: god-классы разобраны — `EntityBuilder<TEntity>` **769→466**, `SqlBuilder` **716→318**, `ScalarFunctionTranslator` **595→131**, `BaseExpressionVisitor` **425→366** (`VisitMethodCall` 183→77); единственное исключение автора — `ExpressionPlanEqualityComparer` (879 формально, 77 собственных строк). Длинные списки параметров ≥6 — все 15 «боевых» разобраны параметр-объектами. Осознанно не закрываются: `CA2213`/`CA1816` (ложные) и `CA1508` (2 вероятно ложных в `NormSqlTranslator.cs:223,250`, сборкой не гейтится). `#pragma disable` в `src/` — 5, все с `restore`.

> Номера строк — по актуальному рабочему дереву HEAD `d21c473` (Build `0 warnings / 0 errors`), если не указано иное. Числа в разделах «Повторный аудит» и «Динамика» — исторические снимки на соответствующий коммит.
>
> **Примечание (20.09.2026):** полностью реализованные отчёты `WIP_*.md`, на которые ссылаются записи ниже, перенесены в документацию и удалены из `docs/specs/roadmap/`; ссылки на них в этом журнале — историческое свидетельство аудита.

---

## Сводка

| Категория навыка | Статус |
|---|---|
| 1. Управление ресурсами (`IDisposable`) | ✅ Находка 2 держится; `CA2213` — 1 (ложное, `_conn` по владению), `CA1816` — 2 (ложные из-за индирекции) |
| 2. Подавление предупреждений | ✅ В `src/`: 5 `SuppressMessage` (0 `<Pending>`) + 5 `#pragma` (все с `restore`). В `tests/`: мёртвые `*DELETE*.cs` с 2 `<Pending>` удалены (Находка 7); `<Pending>` — 0 |
| 3. Антипаттерны LINQ | ✅ Чисто: `.Count()`/`.Distinct().Count()` в `src/` — 0 (единственное вхождение строки — комментарий `InMemoryDataContext.cs:1137`); `CA1827`/`CA1851`/`CA1829` — 0 при `latest-all` |
| 4. Работа с событиями | ✅ `DataContext.Disposed` — ок; подписка `DbConnection.Disposed` для внешнего соединения снимается при dispose контекста (`DbConnectionManager.DisposeConnection`), покрыто `ConnectionManagementTests` (Находка 6) |
| 5. Запахи проектирования | ✅ God-классы разобраны (см. Находку 4): `EntityBuilder<TEntity>` **769→466**, `SqlBuilder` **716→318** (+`SqlSourceRenderer` 359), `ScalarFunctionTranslator` **595→131** (+`StringFunctionTranslator` 363, `MathFunctionTranslator` 57, `DateTimeFunctionTranslator` 50), `BaseExpressionVisitor` **425→366** (`VisitMethodCall` 183→77, локальные `CompileExp`/`EmitValue` вынесены, switch по `nameof(TableAlias.*)` → таблица `TableAliasAccessors`). Длинные списки параметров ≥6 — ✅ все 15 «боевых» разобраны через параметр-объекты (`QueryDefinition` и др.) |
| 6. Обработка исключений | ✅ 1 `catch` (EDF-rethrow, `InMemoryAggregates.cs:74`); пустых/общих нет; `async void`/`throw ex;` нет |
| 7. Хэш-ключи кэша (S2328) | ✅ Находка 5 держится; `S2328` в `src/` — 0 |

---

## 🔎 Повторный аудит (HEAD `d21c473`, 18.09.2026)

Прогон по всем пунктам заново на рабочем дереве HEAD `d21c473`. База: `dotnet build nextorm.sln -c Release` — **0 warnings, 0 errors** (тесты в этом проходе не перезапускались, числа ниже — исторические снимки предыдущих прогонов).

| Пункт | Результат |
|---|---|
| 1. `IDisposable` | ✅ Фиксы Находки 2 держатся: `DataContext.DisposeAsync` (`DataContext.cs:176`) и `InMemoryDataContext.DisposeAsync` (`InMemoryDataContext.cs:1183`) идут через `Dispose()`; `InMemoryEnumerator.Dispose` освобождает `_enumerator` (`InMemoryEnumerator.cs:227`). Минорное наблюдение — `ResultSetEnumerator.DisposeAsync` (`:71`). `CA2213` — 1 (`_conn`, by design), `CA1816` — 2 (ложные). |
| 2. Подавления | ✅ В `src/`: 5 `SuppressMessage` (все с `Justification`, 0 `<Pending>`) + 5 `#pragma` (все с `restore`). В `tests/nextorm.core.tests/` мёртвые `*DELETE*.cs` (Находка 7) удалены 18.09.2026; `<Pending>` — 0. |
| 3. LINQ | ✅ В `src/` нет `.Count()`/`.Distinct().Count()` по коллекциям (единственное совпадение — комментарий `InMemoryDataContext.cs:1137`); `CA1827`/`CA1851`/`CA1829` при `AnalysisLevel=latest-all` — 0. |
| 4. События | ✅ `DataContext.Disposed` (`DataContext.cs:105`) поднимается ровно один раз; подписка `DbConnection.Disposed` для внешнего соединения снимается при dispose контекста (`DbConnectionManager.DisposeConnection`), план-кэш при этом детачится — Находка 6 исправлена. |
| 5. Проектирование | ✅ God-классы разобраны 18.09.2026 (см. Находку 4). Списки параметров ≥6 — ✅ все 15 «боевых» исправлены параметр-объектами; остались только `VisitorOptions` (сам параметр-объект), `make_interval` (сигнатура SQL) и `XxHash32.Combine` (фреймворк-стиль). |
| 6. Исключения | ✅ В `src/` ровно 1 `catch` — `InMemoryAggregates.cs:74` (`TargetInvocationException` с фильтром, переброс через `ExceptionDispatchInfo`); пустых/`catch (Exception)`/log-and-swallow и `throw ex;`/`async void` нет. |
| Хэш-ключи (Находка 5) | ✅ `QueryPlan._hashPlan` — `readonly` (`QueryPlan.cs:18`), `GetHashCode() => _hashPlan` (`:43`), `Debug.Assert` `:61`; `CopyTo` переносит `FromPlanHash`/`UnionPlanHash`/`ReferencedQueriesPlanHash` (`QueryCommand.Clone.cs:25-27`). `S2328` в `src/` — 0. |

### Точечный аудит 19.09.2026 — оконные `percent_rank`/`cume_dist` (чисто)

Изменение новых запахов не вносит: добавленных `SuppressMessage`/`#pragma`/`NoWarn`, пустых
`catch`, `Task.Delay`/`Thread.Sleep`, `Skip=` нет; `IDisposable`, события, LINQ и хэш-ключи не
затронуты. `WindowSql.MapWindowFunctionName` (`WindowSql.cs:23-24`) использует `nameof`.
`percent_rank`/`cume_dist` — ANSI-функции без `Make*`/`Supports*`-хуков: `WindowFunctionTranslator` 
рендерит их как есть (`WindowFunctionTranslator.cs:77-80`), ветка `MakeAggregate` для них не
вызывается. Build Release — **0/0**. Чинить нечего; соотношение подавлений проекта не изменилось
(5 `SuppressMessage` с обоснованием + 5 `#pragma` с `restore`, неоправданных — 0).

### Точечный аудит 19.09.2026 — ClickHouse `any`/`anyLast` (чисто)

Изменение новых запахов не вносит: добавленных `SuppressMessage`/`#pragma`/`NoWarn`, пустых
`catch`, `Task.Delay`/`Thread.Sleep`, `Skip=` нет; `IDisposable`, события, LINQ и хэш-ключи не
затронуты. Рендер переиспользует существующий `EmitSimple`
(`AdvancedAggregateTranslator.cs:123-128`) — как у `arg_min`/`arg_max`; отдельный `Make*`-хук не
нужен, потому что ClickHouse отдаёт тип входа без приведения (в отличие от `uniq*`/`quantile*`).
`ClickHouseDialect` объявляет флаг с XML-`<summary>` (`:51-52`), маппинг имён — `nameof`/строковые
ключи в `MakeAggregate` (`:139-140`).

Build Release — **0/0** (прогнано заново); ClickHouse unit **62/62**, PostgreSQL **155/155**
(прогнано заново). Соотношение подавлений проекта не изменилось (5 `SuppressMessage` с
обоснованием + 5 `#pragma` с `restore`, неоправданных — 0). Чинить нечего.

### Точечный аудит 19.09.2026 — ClickHouse строковый JSON `JSONExtract*` (чисто)

Изменение новых запахов не вносит: добавленных `SuppressMessage`/`#pragma`/`NoWarn`, пустых
`catch`, `Task.Delay`/`Thread.Sleep`, `Skip=` нет; `IDisposable`, события, LINQ и хэш-ключи не
затронуты. Новый `JsonExtractSqlTranslator` (`Visitors/JsonExtractSqlTranslator.cs:18-73`) повторяет
проверенную структуру `TextJsonSqlTranslator` (`TextJsonSqlTranslator.cs:18-84`): switch по `nameof`,
param-mode обход аргументов, предрендер через `VisitToString`, гейт `SupportsJsonExtract` с
`NotSupportedException` (`:53-55`). Тип возврата и касты разобраны в API-реестре (J-раздел); публичных
переименований нет (snake_case — SQL-зеркало, `API-NAMING-REVIEW.md` §3). Аллокация
`new string[args.Count]` + `string.Join` — на холодном пути построения плана, не на исполнении запроса.

Build Release — **0/0** (прогнано заново); ClickHouse unit **64/64**, PostgreSQL **156/156**
(прогнано заново). Соотношение подавлений проекта не изменилось: 5 `SuppressMessage` с обоснованием
+ 5 `#pragma` с `restore`, неоправданных — 0 (актуальные адреса после рефакторинга:
`Builders/EntityBuilderExtensions.cs:61,70,78` CS8619, `DataContext/InMemoryLinqSource.cs:82` CS8714,
`Query/ExpressionPlanEqualityComparer.cs:406` IDE0066). Чинить нечего.

ℹ️ Наблюдение (не находка): в `JsonExtractSqlTranslator.cs:22-60` тринадцать почти одинаковых веток
(8 `json_extract_*`/`json_has`/`json_length`/`json_type` + 5 `visit_param_extract_*`) дублируют
строку-имя (`case nameof(...)` + литерал `"json_extract_*"`/`"visit_param_extract_*"` в `EmitFunction`);
замена литерала на `node.Method.Name` убрала бы рассинхрон при переименовании метода. Та же структура у
`TextJsonSqlTranslator.cs:24-35`, поэтому это не регресс и отдельного фикса не требует.

ℹ️ Наблюдение (не находка): классовая `<summary>` `AdvancedAggregateTranslator`
(`:5-21`) перечисляет не все семейства (нет `uniq*`, `-If`, `any`/`anyLast`) — неполна и до
этого изменения; дополняется при закрытии Шага 5, отдельного фикса не требует.

### Точечный аудит 19.09.2026 — ClickHouse `visitParamExtract*` (чисто)

Изменение новых запахов не вносит: добавленных `SuppressMessage`/`#pragma`/`NoWarn`, пустых
`catch`, `Task.Delay`/`Thread.Sleep`, `Skip=` нет; `IDisposable`, события, LINQ и хэш-ключи не
затронуты. Пять новых методов (`SqlFunctions.ClickHouse.cs:119-131`) — чистые `=> default!` с
XML-`<summary>` у всех; маппинг имён идёт через существующий `ClickHouseDialect.MakeJsonExtract`
(`ClickHouseDialect.cs:73-77`, строковые ключи), транслятор переиспользует
`JsonExtractSqlTranslator.EmitFunction` (`:66-88`) с тем же гейтом `SupportsJsonExtract`; новых
подавлений нет (неоправданных `SuppressMessage` — 0/5, все `#pragma` — с `restore`). Каст не
добавлен и не нужен: `visitParamExtractInt`
возвращает `Int64`, в отличие от `UInt64` у `JSONLength`/`uniq*`. Аллокация `new string[args.Count]` +
`string.Join` — на холодном пути построения плана.

Build Release — **0/0** (прогнано); ClickHouse unit **65/65**, PostgreSQL **157/157** (прогнано
заново); `VisitParamExtract_ShouldReadFlatJson` — зелёный на реальном ClickHouse (по данным
изменения). Соотношение подавлений проекта не изменилось: 5 `SuppressMessage` с обоснованием + 5
`#pragma` с `restore`, неоправданных — 0. Чинить нечего.

---

## ✅ Находка 1 — Подавления предупреждений (ОБРАБОТАНО)

В `Directory.Build.props` задано `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, поэтому каждое подавление «несущее». В `src/` разбор закрыт: **`<Pending>` не осталось ни одного** — 6 подавлений снято, 5 оставшихся имеют конкретное обоснование. В `tests/nextorm.core.tests/` мёртвые файлы `*DELETE*.cs` (в них было 2 `SuppressMessage` с `Justification = "<Pending>"`) удалены 18.09.2026 — см. Находку 7.

### Снято (6)

| Было | Что сделано |
|---|---|
| `Expressions/XxHash32.cs:10` — `#pragma warning disable CA1066` без `restore` | Директива удалена целиком: `XxHash32` не переопределяет `Equals`, и CA1066 при `AnalysisLevel=latest-all` не срабатывает (проверено сборкой). |
| `DataContext/Cache/QueryPlan.cs:5` — S3897 | Класс реализует `IEquatable<QueryPlan>` (метод `Equals(QueryPlan?)` уже был). |
| `Query/ExpressionPlanEqualityComparer.cs:516` — S3897 | `QueryCommandKey` реализует `IEquatable<QueryCommandKey>`. |
| `Query/QueryCommand.cs:11` — S3897 | Удалено как неприменимое: `QueryCommand` не определяет ни `Equals`, ни `GetHashCode`. |
| `Query/QueryCommand.cs:632` — IDE0028 | Код приведён к `_referencedQueries ??= [];`; подавление не нужно. |
| `Builders/EntityBuilder.cs:15` — S2292 | Снято как избыточное: в `.editorconfig` уже стоит `dotnet_diagnostic.S2292.severity = silent`, а `SonarAnalyzer.CSharp` не подключён — правило не может сработать. |

### Оставлено с обоснованием (5)

| Файл:строка | Правило | Обоснование |
|---|---|---|
| `DataContext/Cache/DbPreparedQueryCommand.cs:37` | S2583 | Обе ветки достижимы: `@params` может быть `null`; анализатор не моделирует преобразование в `ReadOnlySpan`. |
| `DataContext/QueryExecutor.cs:73` | S2583 | Состояние пула соединений заранее неизвестно — проверка `ConnectionState.Closed` достижима с обеих сторон; сайт переехал из `DataContext` при выносе исполнения (шаг F1). |
| `Expressions/SelectExpression.cs:7` | IDE1006 | `GetInt32MI` и подобные — намеренный PascalCase для immutable-таблиц рефлексии; правило лишь suggestion. |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:9` | IDE1006 | То же для `AnyMIGeneric`, `ConcatMI` и др. |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:10` | S3011 | Рефлексия нужна, чтобы привязать приватные `Any`/`Concat` в деревья выражений; публичной альтернативы нет. |

### `#pragma warning disable` — 5 шт. в `src/`, все с `restore`

| Файл:строка | Правило | `restore` |
|---|---|---|
| `Builders/EntityBuilder.cs:547` | CS8619 | ✅ `:549` |
| `Builders/EntityBuilder.cs:556` | CS8619 | ✅ `:558` |
| `Builders/EntityBuilder.cs:588` | CS8619 | ✅ `:590` |
| `Query/ExpressionPlanEqualityComparer.cs:406` | IDE0066 | ✅ `:408` |
| `DataContext/InMemoryDataContext.cs:265` | CS8714 | ✅ `:267` |

«Blanket suppression» `XxHash32.cs:10` больше нет: остались только парные `disable`/`restore` на несколько строк.

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
- Итог: в `src/` `SuppressMessage` — 5 (все с обоснованием), `<Pending>` — 0; `#pragma disable` — 5, все с `restore`. В `tests/` — 2 `<Pending>` (см. Находку 7).

---

## ✅ Дополнительно — CA2213 и CA1508 (найдены прогоном анализаторов)

Оба правила не входят в набор по умолчанию (видны только при `AnalysisLevel=latest-all`), но указывали на реальные вещи.

### CA1508 «недостижимое условие»

| Место | Что было | Что сделано |
|---|---|---|
| `Query/ExpressionPlanEqualityComparer.cs:145` | В `Compare2` ветка `_ => left is null ? right is null : ...`, хотя `left` уже гарантированно не `null` вызывающими (`Equals`/`Compare` проверяют раньше) | `Compare2(Expression? left, Expression? right)` → `Compare2(Expression left, Expression right)`; мёртвая проверка удалена. |
| `Visitors/BaseExpressionVisitor.cs:702` | `node.Expression?.Type == typeof(TableColumn)` — после `else if (node.Expression is null)` выражение не может быть `null` | `node.Expression.Type`. |
| `Visitors/BaseExpressionVisitor.cs:749` | `if (key is not null)` — `key` присваивается безусловно | `ExpressionKey? key = null; key = new ...` → `var key = new ...`; проверка и `key!` убраны. |

После правок (на `9641660`): `CA1508` в `src/` — **0**.

**Актуализация 18.09.2026 (HEAD `d21c473`): снова 3 места** — декомпозиция `BaseExpressionVisitor` разнесла условия по translator-классам, и часть проверок стала мёртвой (проверено прогоном `AnalysisLevel=latest-all`):

| Место | Сообщение | Оценка |
|---|---|---|
| `Visitors/MemberTranslator.cs:313` | `'lambdaParameter is not null' is always 'true'` | ✅ **Исправлено (18.09.2026)**: мёртвый третий конъюнкт убран — `if (!visitor.IsParamMode && !visitor.DontNeedAlias)` (внешняя проверка `if (lambdaParameter is not null)` на `:305` сохраняет семантику). |
| `Visitors/NormSqlTranslator.cs:223` | `'countFilter is not null' is always 'false'` | Скорее ложное: анализатор не моделирует присваивание `countFilter` через `node.Arguments is [Expression filterCandidate] && AggregateFilter.IsFilterExpression(...)`. `FILTER (WHERE ...)` покрыт тестами, поведение рабочее. |
| `Visitors/NormSqlTranslator.cs:250` | `'countFilter is not null' is always 'false'` | То же, что `:223` (хвостовой `AggregateFilter.Append`). |

### CA2213 «disposable-поле не освобождается» — 1 реальный, 1 ложный

- **Реальный:** `DataContext/InMemoryEnumerator.cs` — поле `_enumerator` (`IEnumerator<TEntity>`, создаётся для произвольных `IEnumerable`-источников в `Init`) **не освобождалось**: `Dispose()`/`DisposeAsync()` только вызывали `GC.SuppressFinalize`. Исправлено: `Dispose()` вызывает `_enumerator?.Dispose()` и обнуляет поле, `DisposeAsync()` идёт через `Dispose()` (попутно снимает прежнее наблюдение «no-op dispose» из Находки 2).
- **Ложное (по владению):** `DataContext/ResultSetEnumerator.cs` — поле `_conn` (`:30`) берётся из `IConnectionManager.GetConnection()` и освобождается самим контекстом (`DataContext.DisposeStaff` `DataContext.cs:185` → `DbConnectionManager.DisposeConnection` `DbConnectionManager.cs:132`); `Dispose()` в перечислителе закрыл бы соединение, всё ещё используемое контекстом. Поле задокументировано как невладеемое и только обнуляется.

После правок: `CA2213` в `src/` — 1, и это `_conn` с обоснованием по владению.

### CA1816 — оставлено как ложное

`DataContext.DisposeAsync`/`InMemoryDataContext.DisposeAsync` идут через `Dispose()`, который вызывает `GC.SuppressFinalize(this)`; анализатор не видит вызов через индирекцию. Функционально инвариант соблюдён.

### Проверка

- `dotnet build nextorm.sln -c Debug` — **0 warnings, 0 errors**.
- Тесты ядра — **90/90 passed**.
- Актуальный прогон `AnalysisLevel=latest-all` (HEAD `d21c473`, только `nextorm.core`): `CA1508` — **2** (1 реальный исправлен в `MemberTranslator`, остались 2 вероятно ложных в `NormSqlTranslator`; см. выше), `CA2213` — **1** (`_conn`, by design), `CA1816` — **2** (ложные из-за индирекции); `CA1827`/`CA1851`/`CA1829`/`CA2000`/`CA2200`/`CA1031` — **0**.

---

## ⚠️ Находка 2 — Корректность `IDisposable` (ИСПРАВЛЕНО)

### Было

1. `DataContext.DisposeAsync()` вызывал `DisposeStaff()` напрямую, **не выставляя `_disposed`**, и дублировал `GC.SuppressFinalize`. В результате:
   - после `DisposeAsync()` контекст оставался «живым» по флагу `_disposed` (проверка `CheckDisposed()` в DEBUG не срабатывала);
   - повторный `Dispose()`/`DisposeAsync()` повторно выполнял `DisposeStaff()` и повторно поднимал событие `Disposed`.
2. `InMemoryDataContext.DisposeAsync()` (`DataContext/InMemoryDataContext.cs:461`) страдал тем же дефектом: возвращал `CompletedTask`, не выставляя `_disposedValue`.
3. На `DbPreparedQueryCommand<TResult>` стояло устаревшее подавление `S3881 "IDisposable should be implemented correctly"`, хотя ни сам класс, ни его базовый `PreparedQueryCommand` не реализуют `IDisposable`.

### Стало

- `DataContext/DataContext.cs` — `DisposeAsync()` теперь идёт через `Dispose()`:
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

- После `DisposeAsync()` `_disposed == true`, поэтому в DEBUG `CheckDisposed()` (`QueryExecutor.cs:273`) теперь корректно бросает `ObjectDisposedException` при обращении к освобождённому контексту. Использований «dispose async → продолжение работы» в `src/` и `tests/` не найдено.
- Повторное освобождение — no-op, событие `Disposed` поднимается ровно один раз.

### Проверка

- `dotnet build src/nextorm.core/nextorm.core.csproj` — **0 warnings, 0 errors**.
- Тесты ядра (xUnit v3, запуск сборки напрямую) — **83/83 passed, 0 failed** (на момент правки).

### Остаточное наблюдение (повторный аудит)

`DataContext/ResultSetEnumerator.cs:71` — `DisposeAsync()` не идёт через `Dispose(bool)` и не выставляет `_disposed`: он освобождает `_reader` и зануляет `_reader`/`_conn`. Идемпотентность сохраняется (повторный вызов видит `_reader == null`), а `_disposed` читается только внутри `Dispose(bool)` (`:247`), поэтому функционального дефекта нет — это несогласованность стиля, а не баг. Если захочется единообразия: `DisposeAsync()` → `await DisposeAsyncCore()` + общий `_disposed`-guard. Фикс `InMemoryEnumerator` (Находка 2) держится: `DisposeAsync()` → `Dispose()` → `_enumerator?.Dispose()` (`InMemoryEnumerator.cs:91,227`).

---

## ✅ Находка 3 — LINQ на горячем пути (ИСПРАВЛЕНО)

### Было

1. `ExpressionExtensions.cs:135` (в текущем дереве — `:159`):
   ```csharp
   && _outerParams?.Count() > 0 && _outerParams.Contains(param))
   ```
   `_outerParams` — `IEnumerable<ParameterExpression>` (поле `:109`, инициализация `:118`). Здесь сразу `CA1827` (вместо `Any()`) и `CA1851` (двойной перебор: `Count()`, затем `Contains()`) на пути, проходящем при каждом обращении к члену во время трансляции выражений.

2. `Visitors/BaseExpressionVisitor.cs:782`: `args.Count()`, где `args` — `Type[]` из `GetGenericArguments()` (`CA1829`). После декомпозиции этот участок переработан — в текущем `src/` `.Count()` по коллекциям не осталось (см. «Стало»).

### Стало

- `ExpressionExtensions.cs:159` — один проход, без подсчёта:
  ```csharp
  && _outerParams is not null && _outerParams.Contains(param))
  ```
- `Visitors/BaseExpressionVisitor.cs:782` — `args.Count()` → `args.Length`; в текущем дереве и `args.Count()`, и `args.Distinct().Count()` отсутствуют (участок переработан при декомпозиции). Проверка свежим grep: `rg "\.Count\(\)" src` — единственное вхождение — комментарий `InMemoryDataContext.cs:1137`; `.Distinct().Count()` — 0.

### Проверка

- `dotnet build src/nextorm.core/nextorm.core.csproj` — **0 warnings, 0 errors**.
- Тесты ядра (xUnit v3) — **84/84 passed, 0 failed**.

---

## ✅ Находка 4 — God-классы (ЗАКРЫТА 18.09.2026)

Порог раздела 5 навыка — >500 строк на **класс** (не на файл). Замер — длина тела класса от строки объявления до закрывающей скобки.

**Итог (18.09.2026, build 0/0; core 153, sqlite 193, postgres 151, sqlserver 167, mysql 31, mariadb 7, clickhouse 47, integration 853/0/23):**

| Класс | Было | Стало | Как |
|---|---:|---:|---|
| `BaseExpressionVisitor` | 425 | **366** | `VisitMethodCall` 183→77; локальные `CompileExp`/`EmitValue` вынесены в приватные методы; switch по `nameof(TableAlias.*)` (22 упоминания) заменён таблицей `TableAliasAccessors`; 4 `NotImplementedException`/мёртвый код убраны |
| `ScalarFunctionTranslator` | 595 | **131** | Вынесены `StringFunctionTranslator` (363), `MathFunctionTranslator` (57), `DateTimeFunctionTranslator` (50); остался диспетчер + `SqlFunctionAttribute`-трансляция + `string.Concat` |
| `SqlBuilder` (**`struct`**) | 716 | **318** | Вынесены FROM/JOIN/CTE/WHERE/ORDER BY/столбцы в `SqlSourceRenderer` (359); общие коллабораторы — в `readonly struct SqlBuildContext` |
| `EntityBuilder<TEntity>` | 769 | **466** | Терминалы (`Any`/`ToList`/`First`/`Single`/`Last`/`Count`/8 семейств агрегатов/`To*`/`Prepare`/`AnyCommand`/`*Or*Command`) вынесены в extension-методы `EntityBuilderExtensions` (248); на билдере осталась форма запроса |
| `InMemoryDataContext` | 1446 | **351** | Выбыл ранее (фазы 1–8 плана) |
| `ExpressionPlanEqualityComparer` | 879 | 879 | Исключение автора: собственных строк 77, остальное — вложенные `ExpressionComparer` 427 и `Visitor` 335 |

Все прочие — < 500. Публичное API не сужалось: терминалы вызываются как раньше (extension-методы); единственное следствие — потребителю нужен `using NextORM.Core;` (он и так нужен для `From<T>()`/`SqlFunctions`).

**Списки параметров ≥6 — ✅ исправлено 18.09.2026.** Все 15 «боевых» объявлений разобраны параметр-объектами (`QueryDefinition`, `PreparedCommandOptions`, `PrepareFromSqlMode`, `AggregateTypeInfo`, `FromRenderOptions`, `LoggingOptions`, `ProviderHooks`, `ConnectionHooks`, `SqlBuildContext`); длинные перегрузки `BaseExpressionVisitor`/`WhereExpressionVisitor`/`CreateCommand`/`QueryCommand ctors` удалены. В `src` остались только осознанно исключённые: `VisitorOptions` (сам параметр-объект), `make_interval` (сигнатура SQL) и `XxHash32.Combine` (фреймворк-стиль).

Исторический снимок (до закрытия, HEAD `d21c473`):

| Класс | Файл:строка объявления | Строк класса |
|---|---|---:|
| `InMemoryDataContext` (`partial`) | `DataContext/InMemoryDataContext.cs:9` | **351** — ✅ выбыл (было 1446; фазы 1–8 плана: вынесены `InMemoryQueryBuilder` 373, `InMemoryJoin` 157, `InMemoryLinqSource` 184, `InMemorySetOperations` 160, `InMemoryGrouping` 154, `InMemoryOrdering` 96, `InMemoryProjectionFactory` 87, `InMemoryConditionFactory` 75, `InMemoryRowMaterializer` 75; reflection-методы — тонкие обёртки) |
| `ExpressionPlanEqualityComparer` | `Query/ExpressionPlanEqualityComparer.cs:11` | **879** (собственных строк — 77; остальное — вложенные `ExpressionComparer` 427 и `Visitor` 335, каждый < 500; исключение по решению автора — см. ниже) |
| `EntityBuilder<TEntity>` | `Builders/EntityBuilder.cs:21` | **769** |
| `SqlBuilder` (**`struct`**) | `DataContext/SqlBuilder.cs:16` | **716** |
| `ScalarFunctionTranslator` | `Visitors/ScalarFunctionTranslator.cs:12` | **595** |

Выбыли из списка: `InMemoryDataContext` — **351** строк (фазы 1–8 декомпозиции in-memory: тело reflection-ядра перенесено в `InMemoryQueryBuilder`/`InMemoryJoin`, на контексте — тонкие обёртки), `DataContext` — **229** строк (вынесены исполнение, планирование, соединение и окружение, см. [solid-review.md](solid-review.md)), `BaseExpressionVisitor` — **425** (< 500, см. «Сделано»), `QueryCommand` — ни один partial-файл не превышает 500 (`QueryCommand.cs` 264, `QueryCommand.QueryPreparer.cs` 500 со вложенным типом).

**Прогресс `InMemoryDataContext` (18.09.2026, фазы 1–8 декомпозиции).** Фазы 1–2: ось упорядочивания (`ApplyOrdering`/`GetSortingSelector`/`OrderAsyncEnumerable`/`ApplyProjectedOrdering`) вынесена в `InMemoryOrdering` (**96**), set-операции (`CreateSetOperationEnumerator`/`MaterializeBuffered`/`CombineSet`/`CountValues`/`FindValue`) — в `InMemorySetOperations` (**160**). Фазы 3–4: join-проекции (`CreateProjectionType`/`CreateProjection`/`CompileJoinCondition`) — в `InMemoryProjectionFactory` (**87**), условия (`GetConditionPredicates`/`BuildConditionFactory`) — в `InMemoryConditionFactory` (**75**). Фазы 5–7: группировка/агрегаты (`CreateGroupedEnumerator`/`GetAggregateSelector`/`TryGetAggregate`) — в `InMemoryGrouping` (**154**), тело `GetMap` — в `InMemoryRowMaterializer` (**75**), LINQ-источник (`BuildLinqSourceDelegate`/`GetCompiledLinqSelector`/`Enumerate`/`Materialize`) — в `InMemoryLinqSource` (**184**, включая тела `ApplySelectMany`/`ApplyGroupJoin`). Фаза 8: тела reflection-адресуемых (`CreateEnumerator`, `ApplySelectMany`/`ApplyGroupJoin`, `LoopJoin`, `CreateEnumeratorAdapter`, `CreateCompiledQuery`, `CreateAsyncEnumerator(QueryCommand,…)`) и `public`/`protected` (`GetPreparedQueryCommand`, `BuildCreateEnumeratorDelegate`) методов перенесены в `InMemoryQueryBuilder` (**373**) и `InMemoryJoin` (**157**); на контексте остались тонкие обёртки 1:1 по сигнатуре и видимости, поэтому target у `MethodInfo` и встроенные `Expression.Call(@this, mi…)` **не менялись**. Добавлена минимальная `internal`-поверхность: `mi*`-поля (`internal static readonly`) и геттеры кэшей (`CommandIndex`, `SortingSelectorCache`, `AggregateSelectorCache`, `ConditionFactoryCache`, `ConditionDirectCache`, `LinqSelectorCache`); публичный/`protected` API не тронут. Все вынесенные типы — `internal top-level`, все ≤500 (макс. — `InMemoryQueryBuilder` 373). Контекст 1446 → **351** (−1095), **порог 500 достигнут**. Финальная проверка: build **0/0**; core **151**, sqlite **180**, integration SQLite **196** (2 capability-skip), Postgres **196** (6 capability-skip) — Failed 0.

**`ScalarFunctionTranslator` (был 595).** ✅ Разобран 18.09.2026: string-группа вынесена в `StringFunctionTranslator` (363), Math — в `MathFunctionTranslator` (57), DateTime — в `DateTimeFunctionTranslator` (50); остался диспетчер `TryTranslateBuiltIn`, `TryTranslateSqlFunction`, `TryTranslateLikeFunction`, `EmitStringConcat` — **131**.

**`EntityBuilder<TEntity>` (был 769).** ✅ Разобран 18.09.2026: терминальные операторы вынесены в extension-методы `EntityBuilderExtensions` (248); на билдере остался набор формы запроса — **466**.

**Методика замера (18.09.2026).** Размер — число строк тела типа от строки объявления до закрывающей скобки (комментарии и строковые литералы при подсчёте скобок игнорируются). Вложенные типы считаются отдельными классами (их строки вычтены из «собственных» строк внешнего типа), partial-типы суммируются по имени. Поэтому `ExpressionPlanEqualityComparer` формально 879, но собственных строк у него 77.

**Длинные списки параметров (≥6) — исправлено 18.09.2026.** Было 20 объявлений (15 «боевых»); все 15 разобраны через параметр-объекты, длинные перегрузки удалены. Соответствие «было → стало»:

| Было (место) | Парам. | Стало |
|---|---:|---|
| `Visitors/BaseExpressionVisitor.cs` длинная перегрузка ctor | 12 | удалена (мёртвая; остался `BaseExpressionVisitor(VisitorOptions)`) |
| `Visitors/WhereExpressionVisitor.cs` primary ctor | 10 | `WhereExpressionVisitor(VisitorOptions)` |
| `Query/QueryCommand.cs` protected ctor | 10 | `QueryCommand(IDataContext?, QueryDefinition)` |
| `Query/QueryCommand.cs` публичные ctors | 8 | удалены (единый ctor с `QueryDefinition`) |
| `Query/QueryCommand.TResult.cs` ctors | 9 | `QueryCommand<TResult>(IDataContext?, QueryDefinition)` |
| `DataContext/DataContextExtensions.cs` `CreateCommand` ×4 | 9–10 | один `CreateCommand<T>(IDataContext, QueryDefinition)` |
| `DataContext/SqlBuilder.cs` ctor | 8 | `SqlBuilder(VisitorOptions)` / `SqlBuilder(in SqlBuildContext)`; `SqlBuildContext` строится из `VisitorOptions` или object-initializer |
| `DataContext/Cache/DbPreparedQueryCommand.cs` ctor | 6 | `DbPreparedQueryCommand(DbCommand, Func<…>, PreparedCommandOptions)` |
| `DataContext/InMemoryAggregates.cs` `Compute` | 6 | `Compute(object, string, Delegate?, AggregateTypeInfo)` |
| `DataContext/InMemoryDataContext.cs` `PrepareFromSql` | 6 | удалён (мёртвый дубликат расширения; in-memory raw SQL теперь бросает `NotSupportedException` в `InMemoryQueryBuilder`) |
| `Query/QueryCommandExtensions.cs` `PrepareFromSql` | 6 | `…, object? @params, PrepareFromSqlMode mode, CancellationToken` (≤5) |
| `DataContext/QueryPlanner.cs` ctor | 8 | `QueryPlanner(Func<ISqlDialect>, Type, ProviderHooks, LoggingOptions)` |
| `DataContext/DbConnectionManager.cs` ctor | 7 | `DbConnectionManager(IDataContext, ConnectionHooks, string?, DbConnection?, LoggingOptions)` |
| `DataContext/QueryExecutor.cs` ctor | 6 | `QueryExecutor(IConnectionManager, Func<…>, LoggingOptions, Func<bool>)` |
| `DataContext/SqlSourceRenderer.cs` `MakeFrom` | 6 | `MakeFrom(in SqlBuildContext, FromExpression, FromRenderOptions)` |

Новые параметр-объекты: `QueryDefinition`, `PreparedCommandOptions`, `PrepareFromSqlMode`, `AggregateTypeInfo`, `SqlBuildContext` (reuse), `FromRenderOptions`, `LoggingOptions`, `ProviderHooks`, `ConnectionHooks`.

**Остаток в `src` (осознанно исключён):** `VisitorOptions` — сам параметр-объект визиторов (12 позиционных членов by design; `with`-варианты для дочерних визиторов); `PostgresFunctions.make_interval` — сигнатура SQL-функции, зеркалится из PostgreSQL; `XxHash32.Combine` (`Expressions/XxHash32.cs`) — 4 generic-перегрузки в стиле `System.HashCode.Combine`.

Историческая динамика (снимок на `9641660`, к текущему дереву не применять): `BaseExpressionVisitor` 1026 → 2221; `InMemoryDataContext` 983 → 1097; `DataContext` 1247 → 1082 — все эти числа устарели после декомпозиции осей `DataContext` (шаги F1) и `BaseExpressionVisitor` (фазы 1–10).

### Сделано: `QueryCommand` разбит на partial + вынесен `QueryPreparer`

**Вариант A (организационный, поведение не менялось).** Было — один файл 1192 строки; стало 6 файлов (один содержит вложенный тип):

| Файл | Строк | Ответственность |
|---|---:|---|
| `Query/QueryCommand.cs` | 270 | модель: состояние, свойства, ctor, `ResetPreparation`, tree-ops (`ReplaceCommand`/`AddCommand`/`SetOperation`/`AddOuterReference`) |
| `Query/QueryCommand.Prepare.cs` | 13 | точки входа `PrepareCommand` (тонкая делегация в `QueryPreparer`) |
| `Query/QueryCommand.Plan.cs` | 19 | forwarder `RefreshInValuesShape` + 6 фабрик plan-компараторов |
| `Query/QueryCommand.Clone.cs` | 119 | `CopyTo`/`CreateSelf`/`CreateSelfForClone`/`CloneForCache`/`Clone` |
| `Query/QueryCommand.QueryPreparer.cs` | 506 | `QueryPreparer` (вложенный, ~484) — конвейер подготовки (Вариант B) |
| `Query/QueryCommand.TResult.cs` | 438 | generic-фасад исполнения (`QueryCommand<TResult>`) |

**Вариант B (SRP).** Конвейер подготовки вынесен из `QueryCommand` в отдельный вложенный тип `QueryCommand.QueryPreparer` (`internal static`): `Prepare`, `RefreshInValuesShape`, `PrepareFrom`/`Ctes`/`Columns`/`Join`/`Sorting`/`Where`/`Grouping`. `PrepareCommand` теперь только делегирует:

```csharp
public virtual void PrepareCommand(bool dontCalculateHash, CancellationToken cancellationToken)
    => QueryPreparer.Prepare(this, dontCalculateHash, cancellationToken);
```

Размеры типов (HEAD `d21c473`): `QueryCommand` объявлен `partial` в 5 файлах (суммарно 927 строк); сам `QueryCommand` без вложенного `QueryPreparer` — **~425**, вложенный `QueryPreparer` — **~484**. Оба ниже порога 500, поэтому `QueryCommand` больше не god-класс. `QueryCommand<TResult>` — 432 строки класса (438 файла).

Почему nested, а не top-level: конвейер читает и пишет `private`/`protected` состояние команды (`_exp`, `_condition`, `PreparedCondition`, `_whereBasePlanHash`, per-part хэши, `InValues*`, ...). Top-level helper заставил бы `protected`-поля (`_exp`, `_condition`, `_groupExp`, `_having`, `_sorting`, `_isPrepared`, `_srcType`, `_selectList`, `_from`, `_dataContext`) стать `internal`, что ломает внешние наследники `QueryCommand`. Вложенный тип сохраняет доступ, не расширяя API. Оговорка: при подсчёте «строк класса» вложенный тип могут включить в `QueryCommand` (тогда ~910) — если это неприемлемо, следующий шаг — промоушен `QueryPreparer` в top-level с осознанным расширением доступа.

Поведение не менялось: `PrepareCommand` остаётся `virtual` и единственной публичной точкой входа, рекурсивные подготовки (`_union`, `from.SubQuery`, CTE-запросы) по-прежнему идут через виртуальный `PrepareCommand`, поэтому override в `QueryCommand<TResult>` (`ResultPlanHash`/`ResultType`) работает как раньше. Хэш-арифметика, порядок вызовов, sentinel `7`, `unchecked`-блоки и проверки отмены перенесены дословно.

Одновременно удалён мёртвый закомментированный код: **147 → 14** строк построчных `//` (минус 133 строки; ещё +4 строки обоснования добавил параллельный фикс union-хэша, см. Находку 5). Остались только пояснительные комментарии-обоснования (инвариант хэшей клона, владение CTE-клонами в кэше, `IgnoreColumns`/`Paging.Limit` и plan key, форма in-values) и XML-доки. Удалены, в частности, `_columnsHash`/`_joinHash`/`_sortingHash`/`_whereHash`, блок `PLAN_CACHE`, закомментированный `DataProvider`, `_preciseExpressionComparer`, `//public bool CacheList`, мёртвый `FindSourceFromAlias` и десятки `// selList.Add(...)`/`columnsHash = ...`. Заодно устранён «повисший» XML-doc `RefreshInValuesShape`, который при первом сплите оказался приклеен к `PrepareGrouping`.

Границы partial-класса: `QueryCommand` объявлен `partial` в 5 файлах (`QueryCommand.cs`, `Clone`, `Plan`, `Prepare`, `QueryPreparer`), `QueryCommand<TResult>` — `sealed partial` в шестом (`TResult.cs`). Публичный API не менялся; внешних наследников `QueryCommand` в репозитории нет.

### Сделано: `BaseExpressionVisitor` декомпозирован (фазы 1–10)

Было — один класс **2213** строк, 13 методов >30 строк (крупнейший `VisitMethodCall` — 461), конструктор на 12 параметров. Стало — оболочка **439** строк файла (**425** строк класса) + translator-типы. Заявленные в плане «404» (и «389» в предыдущей редакции этого отчёта) устарели: после декомпозиции в файлы добавились параллельные фичи (`BuiltinFunctionTranslator`, `ExtendedScalarFunctionTranslator`, `ArraySqlTranslator`, `JsonSqlTranslator`, `TextJsonSqlTranslator`, `AdvancedAggregateTranslator`, `SqlOperandTranslator`), из-за чего `ScalarFunctionTranslator` дорос с 384 до **595** (> 500 — новый god-класс, см. таблицу Находки 4).

| Файл | Строк файла | Строк класса | Ответственность |
|---|---:|---:|---|
| `Visitors/BaseExpressionVisitor.cs` | 439 | 425 | поля/ctor/свойства, `AsPredicate`, тонкие override-делегаторы, `Clone`/`Dispose`/`ToString`/`WriteTo` |
| `Visitors/MemberTranslator.cs` | 422 | 411 | `VisitMember`/`VisitIndex`: колонки, проекции `tN`, `Value`, `Length`, части `DateTime`, замыкания, ссылки на подзапросы |
| `Visitors/PredicateTranslator.cs` | 412 | 402 | логика/сравнения/CASE/switch + value→predicate; `VisitBinary` остаётся override-делегатором (важно для `WhereExpressionVisitor`) |
| `Visitors/ScalarFunctionTranslator.cs` | 606 | **595** | string/`Math`/`DateTime`/SQL-функции/`LIKE`, `string.Concat`, `SqlFunctionAttribute` |
| `Visitors/NormSqlTranslator.cs` | 362 | 351 | `SqlFunctions.Parameter` и `CommonFunctions`: агрегаты, `EXISTS`/`ANY`/`ALL`, `IN`-подзапросы |
| `Visitors/BuiltinFunctionTranslator.cs` | 305 | 288 | `nullif`/`greatest`/`date_trunc`/`string_agg`/полнотекст |
| `Visitors/AdvancedAggregateTranslator.cs` | 237 | 217 | bool/bit/stat/ordered/`argMin`-`argMax`/`-If` агрегаты |
| `Visitors/JsonSqlTranslator.cs` | 229 | 208 | нативный JSON (PostgreSQL) |
| `Visitors/SqlOperandTranslator.cs` | 188 | 174 | операнды/арифметика |
| `Visitors/ArraySqlTranslator.cs` | 184 | 166 | массивы (PostgreSQL) |
| `Visitors/WindowFunctionTranslator.cs` | 154 | 143 | `Over(...)` и кадр окна |
| `Visitors/WindowSql.cs` | 147 | 138 | чистые хелперы оконных функций |
| `Visitors/ExtendedScalarFunctionTranslator.cs` | 144 | 128 | расширенные скаляры PostgreSQL |
| `Visitors/InValuesTranslator.cs` | 107 | 96 | `in (v1, ...)` / `Contains` |
| `Visitors/ParameterVisitors.cs` | 72 | 25 / 37 | `TestSpecialMethodCallVisitor`, `ParameterBinderVisitor` |
| `Visitors/TextJsonSqlTranslator.cs` | 71 | 61 | текстовые JSON-функции (SQL Server) |
| `Visitors/AggregateFilter.cs` | 62 | 48 | `FILTER (WHERE ...)` |
| `Visitors/TypeFacts.cs` | 60 | 51 | `IsBoolean`/`IsPredicate`/`TryGetNumericConversion` |
| `Visitors/TypedParamVisitor.cs` | 45 | 23 / 13 | `ParamCollectorVisitor`, `ParamLocalSubstitutionVisitor` |
| `Visitors/SqlLiteral.cs` | 42 | 34 | кавычки и экранирование LIKE |
| `Visitors/VisitorOptions.cs` | 28 | 13 | параметр-объект: все construction-time коллабораторы визитора; `with`-варианты для дочерних визиторов |
| `Visitors/AliasResolver.cs` | 23 | 18 | резолв алиаса таблицы |

Взаимодействие — через `internal`-поверхность визитора (15 геттеров включая `Options`, 2 внутренних сеттера, 3 метода); публичный и `protected` API не менялись, `WhereExpressionVisitor` и точки вызова `VisitCondition`/`WriteTo`/`NeedAliasForColumn`/`ColumnName` не затронуты. Конструктор сгруппирован в `VisitorOptions` (фаза 9); длинная перегрузка оставлена только ради совместимости.

Ограничение проверяемости: статические трансляторы не могут вызвать `base.Visit*`, поэтому `VisitUnary`/`VisitMember`/`VisitIndex` возвращают `Expression?` (`null` = «не обработано»), а делегатор делает `?? base.VisitXxx(node)`. Поведение идентично прежнему.

Перенос пофазно верифицирован нормализованным diff'ом против снимка baseline: **ни одной потерянной строки логики** (фазы 2–8: 0 расхождений, кроме явно перечисленных переименований локальных и чужой параллельной правки `MakeConcat`); сборка 0/0; тесты core 111, sqlite 144, sqlserver 116, postgres 90, mariadb 6 — зелёные.

**Публичное API не сломано.** `src/nextorm.core/Visitors/BaseExpressionVisitor.cs` побайтово (без учёта CRLF) совпадает с `git show d21c473:src/nextorm.core/Visitors/BaseExpressionVisitor.cs`; `WhereExpressionVisitor` (единственный наследник) — тоже. Все публичные/`protected` члены сохранены, `internal`-поверхность добавлена, но `InternalsVisibleTo` в сборке нет.

### ✅ Остаток `BaseExpressionVisitor` закрыт 18.09.2026

| Было | Стало |
|---|---|
| `VisitMethodCall` **183** строки | **77** — линейная цепочка вызовов translator-классов + обработка `Convert`/`TableAlias` |
| Локальная `CompileExp` (25 строк, мёртвый `throw NotImplementedException`) внутри `VisitMethodCall` | приватный метод `CompileExpression` (мёртвый код удалён) |
| Локальная `EmitValue` (22 строки) внутри `VisitConstant` | приватный метод `TryEmitValue` |
| `node switch` по `nameof(TableAlias.*)` (22 упоминания) | таблица `TableAliasAccessors` (`FrozenSet<string>` + признак «допускает выражение») |
| 4 `NotImplementedException` | переведены в `NotSupportedException` с сообщением (доменный контракт), недостижимый — удалён |
| мёртвый закомментированный код (81 строка `//` в файле) | удалён |

### Исключения (по решению автора)

| Класс | Строк класса | Почему исключён |
|---|---|---|
| `ExpressionPlanEqualityComparer` | 879 | Размер — следствие полноты дерева `Expression`, а не смешения ответственностей: один `Compare*`-метод на тип узла. Декомпозиция выигрыша не даёт. |

Для движка запросов часть размера ожидаема, но god-классов больше нет: `EntityBuilder<TEntity>` **466**, `SqlBuilder` **318** (+`SqlSourceRenderer` 359), `ScalarFunctionTranslator` **131** (+`StringFunctionTranslator` 363), `BaseExpressionVisitor` **366**. `DataContext` (**229**) и `InMemoryDataContext` (**351**, фазы 1–8 декомпозиции in-memory) выбыли ранее.

---

## ✅ Находка 5 — Хэш-ключи кэша (ИСПРАВЛЕНО)

### Проверка инварианта (и найденная ошибка)

Подтверждено: `QueryPlan.GetHashCode()` **действительно вызывается до** `GetCacheVersion()` — через `QueryPlanCache.TryGetValue(new QueryPlanCacheKey(GetType(), queryPlan), ...)` (`DataContext.cs:310`) и `_cmdIdx.TryGetValue(queryPlan, ...)` (`InMemoryDataContext.cs:64`). То есть `_hashPlan` кэшировался по исходной команде, а `GetCacheVersion()` затем подменял `QueryCommand`/`_comparer` клоном.

`Debug.Assert` в `QueryPlan.cs` должен был подтверждать хэш-эквивалентность клона, **но в ветке `_sql == null` (in-memory и обычный SQL-путь) assert был вакуумным**: сравнивал `GetHashCode()` со значением, посчитанным по ещё не подменённому `QueryCommand`, т. е. сам с собой. Как только assert сделали настоящим, упали три теста (`TestWhere_Subquery`, `SelectAny_ShouldReturnData`, `SelectAny2_ShouldReturnData`): **`Equals(clone, original) == true`, но хэши разные** — нарушение контракта `Equals ⇒ одинаковый хэш`.

**Корневая причина:** `QueryCommand.CopyTo(dst, copyAll: false)` (`QueryCommand.cs`) не копировал hash-значимые поля `FromPlanHash`, `UnionPlanHash`, `ReferencedQueriesPlanHash`, которые читает `QueryPlanEqualityComparer.GetHashCode()`. У клона они обнулялись. Раньше это маскировалось тем, что `_hashPlan` «замерзал» на исходном значении, поэтому промахов кэша не было заметно, хотя ключ в словаре переставал соответствовать собственному `Equals`.

### Стало

- `Query/QueryCommand.cs` — `CopyTo` переносит `FromPlanHash`, `UnionPlanHash`, `ReferencedQueriesPlanHash` (восстановлен контракт `Equals ⇒ одинаковый хэш`).
- `DataContext/Cache/QueryPlan.cs` — хэш вычисляется один раз из состояния на момент конструирования и хранится в `readonly int _hashPlan`: `GetCacheVersion()` больше не может оставить устаревший хэш, `GetHashCode` стабилен на всю жизнь ключа.
- `DataContext/Cache/QueryPlan.cs` — вакуумный assert заменён настоящим (`_hashPlan == ComputeHash(newCmd, ...)`), инвариант теперь реально проверяется в DEBUG.
- `ExpressionCache.cs` (`ExpressionKey`) и `Query/ExpressionPlanEqualityComparer.cs` (`QueryCommandKey`) — входные поля там и так `readonly`; хэш тоже считается один раз в `readonly`-поле, ложные подавления `S2328` сняты.
- Регрессионный тест `InMemoryTests.QueryPlan_HashMustStayStableAcrossGetCacheVersion` фиксирует: хэш не меняется после `GetCacheVersion()`, а логически равный свежий план находит кэш в `Dictionary<QueryPlan, object>`.

### Про глобальный кэш — исходная тревога не подтвердилась

`DataContextCache` (`DataContextCache.cs`) ключуется по `Type` (`_metadata`, `_selectListCache`) — размер ограничен числом типов модели, утечки нет. Поле `_expCache` там объявлено, но внутри библиотеки не используется: `InMemoryDataContext` держит **свои экземплярные** кэши (`InMemoryDataContext.cs:29` и далее), живущие вместе с контекстом. Политика вытеснения не требуется. Наблюдение на будущее: `InMemoryDataContext.PurgeQueryCache()` (`:1445`) очищает только `_cmdIdx`, но не `_expCache`/`_conditionFactoryCache`/`_conditionDirectCache`.

### Проверка

- `dotnet build nextorm.sln -c Debug` — **0 warnings, 0 errors**.
- Тесты ядра (xUnit v3) — **85/85 passed** на момент правки; повторный аудит (90 тестов) — **90/90 passed** (три падавших тогда — подзапросные формы).

---

## ✅ Находка 6 — Утечка подписки на событие внешнего соединения (ИСПРАВЛЕНО 18.09.2026)

`DbConnectionManager.GetConnection` подписывал менеджер на `DbConnection.Disposed` (`_conn.Disposed += ConnDisposed`) только для **внешнего** (caller-supplied) соединения. Собственное соединение (`_connWasCreatedByMe == true`) `DisposeConnection` освобождал сам, а в невладеемой ветке он не делал ничего: подписка жила до фактического освобождения соединения вызывающим и держала `DbConnectionManager` (а через `_owner` — и контекст с коллабораторами) достижимыми. Классическая утечка через событие (раздел 4 навыка).

**Исправление.** `DisposeConnection` переписан: ранний выход при `_conn is null`, затем всегда детач план-кэша (`cached.ResetConnection(_conn, _owner)`), затем для собственного соединения — лог и `Dispose()`, а для внешнего — снятие подписки `_conn.Disposed -= ConnDisposed` без диспоза (владелец — вызывающий). `_conn` обнуляется в обоих случаях, поэтому живое соединение больше не удерживает менеджер и контекст.

**Покрытие.** `tests/nextorm.sqlite.tests/ConnectionManagementTests.Dispose_WithSuppliedConnection_UnsubscribesDisposedHandler`: через `WeakReference` + `GC.Collect` проверяет, что живое внешнее соединение не удерживает освобождённый контекст. Тест чувствителен — без снятия подписки падает (проверено временным откатом фикса).

---

## ✅ Находка 7 — `*DELETE*.cs` тест-файлы (ИСПРАВЛЕНО 18.09.2026)

Исходная формулировка «3 мёртвых файла» неточна: проверка по коду показала, что два из трёх использовались тестами.

| Файл | Строк | Реальность |
|---|---:|---|
| `ExpressionEqualityComparerDELETE.cs` | 723 | действительно мёртв — ни одной ссылки |
| `ExpressionPlanEqualityComparerDELETE.cs` | 828 | использовался как `_sut` в `ExpressionPlanEqualityComparerTests`, но дублировал реальный `ExpressionPlanEqualityComparer` (он же `_sut2` в том же тесте) |
| `PreciseExpressionEqualityComparerDELETE.cs` | 799 | был субъектом `PreciseExpressionEqualityComparerTests`; типа `PreciseExpressionEqualityComparer` в `src/` нет (только закомментированные упоминания в `IQueryRegistry`/`*PlanEqualityComparer`), т.е. тест проверял непоставляемый класс |

Все три файла удалены вместе с `PreciseExpressionEqualityComparerTests.cs`; в `ExpressionPlanEqualityComparerTests.cs` убран дублирующий `_sut` (оставлено покрытие реального `ExpressionPlanEqualityComparer`); тестовый дабл `QueryProvider : IQueryRegistry`, который жил в удалённом файле, вынесен в `tests/nextorm.core.tests/QueryProvider.cs`. `<Pending>`-подавления (SW002) исчезли вместе с файлами. Проверка: build 0/0; core **151/151**; unit-наборы провайдеров sqlite 180, sqlserver 166, postgres 150, mysql 31, mariadb 7, clickhouse 47 — Failed 0.

---

## Чистые категории

- **4. Работа с событиями** — `DataContext.Disposed` (`DataContext.cs:105`) поднимается ровно один раз в `DisposeStaff` (`:185`) под guard `_disposed`; подписок на `StateChange` больше нет. Внешнее соединение — см. Находку 6 (исправлено 18.09.2026).
- **6. Обработка исключений** — в `src/` ровно **1** `catch` (`InMemoryAggregates.cs:74`): `catch (TargetInvocationException ex) when (ex.InnerException is not null)` с перебросом через `ExceptionDispatchInfo.Capture(ex.InnerException).Throw()` — законный паттерн сохранения стектрейса, не запах. Пустых/`catch (Exception)`/log-and-swallow, `throw ex;` (CA2200) и `async void` нет. Два `catch (Exception)` в тестовых контейнерах (`tests/nextorm.integration.tests/Providers/SqlServerContainer.cs:89`, `PostgresContainer.cs:89`) — законный перехват с сохранением сообщения.

---

## 🔎 Точечный аудит — текст-JSON MySQL/MariaDB (19.09.2026)

Область: `ISqlDialect`/`SqlDialectBase` (новый хук `MakeTextJsonFunction(name, args)`),
`Visitors/TextJsonSqlTranslator.EmitFunction`, `MySqlDialect`, тесты MySQL. Build Release — **0/0**
(сборка прогнана заново, не исторический снимок).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База не изменилась: в `src/` **5** `SuppressMessage` (все с `Justification`) + **5** `#pragma disable`, все с парным `restore`. |
| Слоп-паттерны (slopwatch вручную) | ✅ В изменении `Skip=`, `Task.Delay`, `Thread.Sleep`, пустых `catch` — **0**. По `tests/` целиком: `Skip=` — 0; `Task.Delay` — 2 (обе — `Task.Delay(0)` как yield в `InMemoryTests.cs:80,369`, не задержка). |
| Обработка array-операндов в `EmitFunction` | ✅ Регрессии нет (см. ниже). |
| 1/3–7. `IDisposable`, LINQ, события, исключения, хэш-ключи | ✅ Изменением не затронуты. |

**Массивы в текст-JSON.** Было: `TextJsonSqlTranslator.EmitFunction` звал
`SqlOperandTranslator.EmitFunction`, а тот через `AppendArgument` (`SqlOperandTranslator.cs:24-39`)
биндил array-операнд одним параметром (`AppendArrayOperand`). Стало: аргументы рендерятся напрямую
(`VisitToString`, `TextJsonSqlTranslator.cs:78-82`). Array-путь для этой поверхности **недостижим**:
все четыре сигнатуры принимают только `string?` (`Query/SqlFunctions.SqlServer.cs:19,25,32,38`),
поэтому `Type.IsArray` для аргумента всегда `false`. Порядок параметров сохранён: param-режим обходит
`args` по порядку (`TextJsonSqlTranslator.cs:71-72`), SQL-проход — тоже (`:79-80`).

ℹ️ **Наблюдение (фикс не требуется):** `MySqlDialect.MakeTextJsonFunction` (`:45-51`) индексирует
`args[0..2]` без проверки арности; транслятор уже гарантирует `Count == 2/2/3`
(`TextJsonSqlTranslator.cs:23-30`). Если хук будут вызывать напрямую, стоит добавить guard с внятным
сообщением.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`tests/nextorm.mysql.tests` (`MySqlDialectTests.TextJsonHooks_ShouldUseJsonExtractFamily`,
`SqlGenerationTests.TextJsonFunctions_ShouldUseJsonExtractFamily`, `IsJson_ShouldUseJsonValidPredicate`)
и `MySqlSpecificTests.Json*`/`IsJson_ShouldDetectValidJson`.

---

## 🔎 Точечный аудит — `DateTime.DayOfYear` (19.09.2026)

Область: `MemberTranslator` (новый ключ `"doy"`), `MakeDatePart` в SQLite/MySQL/ClickHouse, SQL-gen- и
диалект-тесты. Build Release — **0/0** (прогон заново). Новые тесты прогнаны точечно: SQL-gen
sqlite/postgres/sqlserver/mysql/clickhouse и `MakeDatePart("doy", …)` sqlite/mysql/clickhouse — зелёные.
Версия тестов по задаче: core 153, sqlite 198, postgres 151, sqlserver 167, mysql 35, mariadb 7, clickhouse 48.

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную) | ✅ `Skip=` — 0 (единственное совпадение — имя теста `…ShouldIgnoreValue`); `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0. |
| 1/3–7. `IDisposable`, LINQ, события, исключения, хэш-ключи | ✅ Изменением не затронуты. |

### 🔴 Находка 8 — SQL Server получает недопустимый `datepart(doy, …)` (ОТКРЫТА)

`MemberTranslator.cs:68` канонизирует `DateTime.DayOfYear` в часть `"doy"`. База
(`SqlDialectBase.cs:207`) рендерит `extract(doy from …)` — это валидный PostgreSQL; MySQL
(`MySqlDialect.cs:159`), ClickHouse (`ClickHouseDialect.cs:98`) и SQLite (`SqliteDialect.cs:134`)
переопределяют корректно. Но SQL Server передаёт ключ в `datepart(part, value)` **как есть**
(`SqlServerDialect.cs:152`), а `doy` не входит в допустимые T-SQL-токены: Microsoft Learn для
`dayofyear` даёт только `dy`/`y`, а EF Core, querydsl и FreeSql эмитят `datepart(dayofyear, …)`.
Запрос с `DateTime.DayOfYear` для SQL Server упадёт при исполнении.

Не гейтится, потому что SQL-gen-тест (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:838`)
проверяет только текст, а интеграционного запроса с `DayOfYear` нет (`rg DayOfYear tests` — только
unit-тесты).

- **Было:** `public override string MakeDatePart(string part, string value) => $"datepart({part}, {value})";` (`SqlServerDialect.cs:152`)
- **Стало:** `... => part == "doy" ? $"datepart(dayofyear, {value})" : $"datepart({part}, {value})";`
- **Проверка:** ожидание теста → `datepart(dayofyear, dt)`; добавить `DayOfYear` в `CommonTestSuite` (реальное исполнение на SQL Server).

### ℹ️ Наблюдения (фикс не требуется)

- Ключ `"doy"` — единственное сокращение среди полнословных `year`/`month`/`day`/`hour`/`minute`/`second`; выбран под `extract(doy …)`. После фикса Находки 8 он остаётся чисто внутренним контрактом визитора и диалектов.
- Новые `public override MakeDatePart` (`MySqlDialect.cs:159`, `ClickHouseDialect.cs:98`) не имеют XML-`<summary>` — ровно как существующий SQL Server-override (`SqlServerDialect.cs:152`); `CS1591` скрыт в 7 `.csproj` до Шага 5 (см. `API-NAMING-REVIEW.md`).
- `DateTime.DayOfWeek` намеренно не маппится. Обоснование в WIP слишком широкое: `datepart(weekday)`/`DATEFIRST` — это только SQL Server; PostgreSQL `extract(dow …)` и SQLite `strftime('%w')` уже 0-based как .NET, MySQL `dayofweek()-1` и ClickHouse `toDayOfWeek()%7` дают тот же результат. Т.е. безопасный пер-диалектный маппинг существует, но это отдельная задача.
- `tests/nextorm.sqlite.tests/SqlGenerationTests.cs:1063` — метод по-прежнему `DateTimeYearMonthDay_ShouldUseStrftime`, хотя теперь покрывает и `DayOfYear`; имя теста сузилось.

---

## 🔎 Точечный аудит — ClickHouse `uniq`-агрегаты (19.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (`uniq`/`uniq_exact`/`uniq_combined`/`uniq_hll12`),
`ISqlDialect`/`SqlDialectBase` (флаг `SupportsUniqAggregates` + хук `MakeUniqAggregate`),
`ClickHouseDialect`, `Visitors/AdvancedAggregateTranslator.EmitUniq`, тесты ClickHouse/PostgreSQL/
интеграционные. Build Release — **0/0** (сборка прогнана заново, не исторический снимок).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0. |
| 1/3–7. `IDisposable`, LINQ, события, исключения, хэш-ключи | ✅ Изменением не затронуты: новых disposable-полей, `.Count()`/`.Any()`, подписок, `catch` и `GetHashCode` нет. |

ℹ️ **Наблюдение (фикс не требуется):** `EmitUniq` (`AdvancedAggregateTranslator.cs:153-166`) структурно
повторяет `EmitSimple` (`:128-146`) — тот же param-режим и `NeedAliasForColumn`; отличие только в вызове
`MakeUniqAggregate` вместо `MakeAggregate` и в тексте исключения. При появлении третьего варианта стоит
свести их к одному методу с делегатом-рендерером; сейчас 12 строк дублирования осознанны.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; в изменённых файлах
подавлений нет, соотношение подавлений проекта не изменилось (5 оправданных + 5 парных `#pragma`,
неоправданных — 0).

---

## 🔎 Точечный аудит — ClickHouse `quantile`/`median` (19.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (`quantile`/`quantile_exact`/`quantile_timing`/`median`),
`ISqlDialect`/`SqlDialectBase` (флаг `SupportsQuantileAggregates` + хук `MakeQuantile`),
`ClickHouseDialect`, `Visitors/AdvancedAggregateTranslator.EmitQuantile`, тесты ClickHouse/PostgreSQL/
интеграционные. Build Release — **0/0** (прогнано заново, не исторический снимок).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0. |
| 3. LINQ | ✅ `EmitQuantile` не содержит LINQ; новых `.Count()`/`.ToList()` на горячем пути нет. |
| 4. События | ✅ Изменением не затронуты. |
| 5. Проектирование | ✅ `EmitQuantile` (`AdvancedAggregateTranslator.cs:186-203`) — 18 строк, param-режим и `NeedAliasForColumn` как у соседей; новых god-классов/длинных параметров нет. |
| 1/6/7. `IDisposable`, исключения, хэш-ключи | ✅ Не затронуты; новый `throw` — `NotSupportedException` по образцу `EmitUniq`, без `catch`. |
| ℹ️ Наблюдения (фикс не требуется) | `EmitQuantile` структурно повторяет `EmitUniq`/`EmitSimple` (тот же param-режим + `NeedAliasForColumn`): при появлении четвёртого варианта свести к одному методу с делегатом-рендерером. Контрактный дефект `median` без `toFloat64` — не запах из каталога навыка, зарегистрирован в `API-NAMING-REVIEW.md` (Q2). |

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; ClickHouse unit **59/59**,
PostgreSQL **154/154** (прогнано заново); в изменённых файлах подавлений/слопа нет, соотношение подавлений
проекта не изменилось (5 оправданных + 5 парных `#pragma`, неоправданных — 0).

---

## 🔎 Точечный аудит — ClickHouse функции словарей `dictGet*` (19.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (`dict_get`/`dict_get_or_default`/`dict_has`),
`ISqlDialect`/`SqlDialectBase` (флаг `SupportsDictionaries` + хук `MakeDictionaryFunction`),
`ClickHouseDialect`, `Visitors/DictionarySqlTranslator` (подключён в `NormSqlTranslator`),
тесты ClickHouse/PostgreSQL. Build Release — **0/0** (прогнано заново, не исторический снимок).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1/3–7. `IDisposable`, LINQ, события, исключения, хэш-ключи | ✅ Изменением не затронуты: новых disposable-полей, `.Count()`/`.Any()`, подписок, `catch` и `GetHashCode` нет. |
| 5. Проектирование | ✅ `DictionarySqlTranslator` — 57 строк, `EmitFunction` — 22 строки; новых god-классов/длинных списков параметров нет. |

ℹ️ **Наблюдение (фикс не требуется):** `DictionarySqlTranslator.EmitFunction` (`:35-56`) — четвёртая
почти дословная копия тела «param-режим → `NeedAliasForColumn` → `VisitToString` → `Make*`» вместе с
`TextJsonSqlTranslator.EmitFunction`, `JsonExtractSqlTranslator.EmitFunction` и
`EmitUniq`/`EmitSimple`. Дублирование ~20 строк на холодном пути построения плана; при появлении пятого
семейства стоит свести к одному методу с делегатом-рендерером (ср. наблюдения в аудитах `JSONExtract*`
и `quantile`). Второе наблюдение: классовая `<summary>` `ClickHouseFunctions` теперь упоминает словари,
но свойство `SqlFunctions.ClickHouse` (`Query/SqlFunctions.cs:40-45`) по-прежнему перечисляет только
`argMin`/`argMax` и `-If` — зарегистрировано в `API-NAMING-REVIEW.md` (D3).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **67/67**,
`tests/nextorm.postgres.tests` — **158/158** (прогнано заново); в изменённых файлах подавлений/слопа нет,
соотношение подавлений проекта не изменилось (5 оправданных + 5 парных `#pragma`, неоправданных — 0).

---

## 🔎 Точечный аудит — PostgreSQL uuid-генераторы и session/info-функции (19.09.2026)

> **Актуализация 19.09.2026 — промоушен session/info-функций.** Пять методов `current_user`/
> `session_user`/`current_schema`/`current_database`/`version` перенесены на `CommonFunctions`
> (кросс-провайдерно) и больше не входят в этот PG-only срез; на `PostgresFunctions` остались
> `gen_random_uuid`/`uuidv7`/`pg_typeof`. Актуальный разбор — в разделе
> «cross-provider session/info-функции» ниже.

> **Актуализация 19.09.2026 — UUID-генераторы промоутнуты.** `gen_random_uuid`/`uuidv7` тоже перенесены
> на `CommonFunctions` (кросс-провайдерно); на `PostgresFunctions` остался только PG-only `pg_typeof`.
> Актуальный разбор — в разделе «cross-provider UUID-генераторы» ниже.

Область: `Query/SqlFunctions.Postgres.cs` (`gen_random_uuid`/`uuidv7`/`pg_typeof`/`current_user`/
`session_user`/`current_schema`/`current_database`/`version`), `Visitors/ExtendedScalarFunctionTranslator`
(`KeywordFunctions`/`DirectFunctions` + `EmitPgTypeOf`), `PostgresDialect.MakeTypeName`, SQL-gen- и
интеграционные тесты. Build Release — **0/0** (прогнано заново, не исторический снимок).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `EmitPgTypeOf` ресурсов не удерживает. |
| 3. LINQ | ✅ Новый код — `HashSet.Contains` + линейные обходы аргументов; `.Count()`/`.ToList()` на горячем пути нет. |
| 4. События / 6. Исключения | ✅ Новый `throw` — `NotSupportedException` в существующем общем `RequireExtended`; `catch` не добавлено. |
| 5. Проектирование | ✅ `EmitPgTypeOf` (`ExtendedScalarFunctionTranslator.cs:112-125`) — 14 строк; новых god-классов/длинных списков параметров нет. |
| 7. Хэш-ключи | ✅ Не затронуты. |

ℹ️ **Наблюдения (фикс не требуется):**
- `SqlFunctions.Postgres.cs:6-12` — два `<summary>` подряд на публичном `PostgresFunctions` (аналог Q1
  в `API-NAMING-REVIEW.md` для `ClickHouseFunctions`) → дубль в `nextorm.core.xml`/DocFX. Пре-существующее,
  не введено этой правкой; зарегистрировано как PG5.
- `ExtendedScalarFunctionTranslator.cs:5-16` — классовая `<summary>` перечисляет math/string/regexp/
  date-time/`num_nulls`, но не новое семейство uuid/session-info (тип `internal`, на CS1591 не влияет).
- `EmitPgTypeOf` (`:112-125`) повторяет param-режим `EmitMath` (`:127-143`); дублирование ~6 строк на
  холодном пути построения плана — при появлении третьего варианта свести к одному методу.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
PostgreSQL **159/159**, SQL Server **168/168**, SQLite **200/200** (по данным изменения); интеграционный
`PostgresSpecificTests.InfoFunctions_ShouldReturnServerValues` — зелёный на реальном PostgreSQL;
в изменённых файлах подавлений/слопа нет, соотношение подавлений проекта не изменилось
(5 оправданных + 5 парных `#pragma`, неоправданных — 0).

---

## 🔎 Точечный аудит — SQL Server `IIF`/`CHOOSE` (19.09.2026)

Область: `Query/SqlFunctions.SqlServer.cs` (`iif`/`choose`), `ISqlDialect`/`SqlDialectBase`
(флаг `SupportsIifChoose`), `SqlServerDialect`, `Visitors/BuiltinFunctionTranslator`
(`EmitIifChoose`/`FlattenChoose`), SQL-gen- и интеграционные тесты. Build Release — **0/0**
(прогнано заново); SQL Server unit **169/169**, PostgreSQL **161/161** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay`/`Thread.Sleep` — 0 в затронутых тестах; пустых `catch` — 0. |
| 1/3/4/6/7. `IDisposable`, LINQ, события, исключения, хэш-ключи | ✅ Изменением не затронуты: новых disposable-полей, `.Count()`/`.Any()`, подписок, `catch` и `GetHashCode` нет. |
| 5. Проектирование | ✅ `EmitIifChoose` (`:115-121`) — 7 строк, `FlattenChoose` (`:124-134`) — 11; новых god-классов/длинных списков параметров нет. Рендер переиспользует `SqlOperandTranslator.EmitFunction`, а не копирует param-режим (в отличие от наблюдений в аудитах `DictionarySqlTranslator`/`JSONExtract*`). |

ℹ️ **Наблюдения (фикс не требуется в рамках оценки запахов):**
- `BuiltinFunctionTranslator.cs:113` — осиротевший `<summary>` про `date_trunc` над `EmitIifChoose`
  (дубль `<summary>`; `EmitDateTrunc` осталась без доки). Это дефект документации, а не запах из
  каталога; зарегистрирован как **I2** в `API-NAMING-REVIEW.md`; фикс — удалить строку `:113`.
- Guard `Arguments.Count == 2` у `choose` (`:37`) избыточен (`params` всегда даёт 2 аргумента) — безвреден.
- Захваченный массив вместо inline-`params` молча биндится одним array-параметром (`FlattenChoose`
  не разворачивает не-`NewArrayExpression`) → `choose(@i, @p)` невалиден для SQL Server; та же
  пре-существующая дыра, что у `greatest`/`least` (`FlattenParams`). Отдельного фикса в реестре
  запахов не требует; рекомендация — в API-реестре (I6).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`dotnet test tests/nextorm.sqlserver.tests -c Release --no-build` — **169/169**,
`tests/nextorm.postgres.tests` — **161/161** (прогнано заново); `rg "SuppressMessage|#pragma warning disable" src`
— 5/5 без изменений; в изменённых файлах подавлений/слопа нет, соотношение подавлений проекта не
изменилось (5 оправданных + 5 парных `#pragma`, неоправданных — 0).

> **Актуализация 19.09.2026 (верификация).** Наблюдения этого раздела закрыты текущим кодом:
> осиротевший `<summary>` про `date_trunc` убран, у `EmitIif`/`EmitChoose`/`EmitDateTrunc` — отдельные
> доки (`BuiltinFunctionTranslator.cs:113,134,156`). Guard `Arguments.Count == 2` у `choose` остался
> (безвреден), дыра с захваченным массивом не закрыта (см. ниже). Подробнее — в разделе «Повторная
> верификация cross-provider `iif`/`choose`/`nth_value`».

---

## 🔎 Точечный аудит — ClickHouse `GROUP BY ... WITH TOTALS` (19.09.2026)

Область: `EntityBuilder.WithTotals()` + флаг `GroupByWithTotals` (`EntityBuilder`/`JoinedEntityBuilder`/
`QueryDefinition`/`QueryCommand`), ключ плана (`QueryPlanEqualityComparer` + `PrepareGrouping`),
`ISqlDialect.SupportsGroupByWithTotals`/`MakeGroupByTotals` + `SqlDialectBase`/`ClickHouseDialect`,
рендер в `SqlBuilder`, unit-/интеграционные тесты. Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1/3/4/6/7. `IDisposable`, LINQ, события, исключения | ✅ Изменением не затронуты: новых disposable-полей, `.Count()`/`.Any()`, подписок, `catch` нет; новый `throw` — `NotSupportedException` без `catch` (`SqlBuilder.cs:127-131`). |
| 5. Проектирование | ✅ Новый публичный `WithTotals()` (`EntityBuilder.cs:444-451`) — 8 строк; новых god-классов/длинных списков параметров нет. Плюмбинг флага однотипен `GroupingType`/`GroupingSets` (join-инициализаторы, `CopyTo`). |

### 🟡 Находка 9 — in-memory молча игнорирует `WITH TOTALS` (ОТКРЫТА)

`InMemoryGrouping.CreateGroupedEnumerator` отклоняет любой модификатор группировки, кроме `None`
(`InMemoryGrouping.cs:89-90`), но `QueryCommand.GroupByWithTotals` не проверяется нигде в `InMemory*`
(`rg GroupByWithTotals src/nextorm.core/DataContext/InMemory*` — пусто). Поэтому
`InMemoryDataContext.From<T>().GroupBy(...).WithTotals().Select(...)` молча возвращает обычные строки
групп вместо `NotSupportedException` — в отличие от `ROLLUP`/`CUBE`/`GROUPING SETS`. SQL-провайдеры
защищены проверкой `SupportsGroupByWithTotals` в `SqlBuilder.cs:125-128`, но in-memory до `SqlBuilder`
не доходит. В прозе задокументировано, что in-memory отклоняет `ROLLUP`/`CUBE`, про `WITH TOTALS` —
ни слова, т.е. у вызывающего нет сигнала.

- **Было:** `if (queryCommand.GroupingType != GroupingType.None) throw new NotSupportedException("The ROLLUP/CUBE grouping modifiers are not supported by the in-memory provider.");` (`InMemoryGrouping.cs:89-90`)
- **Стало:** добавить рядом `if (queryCommand.GroupByWithTotals) throw new NotSupportedException("The WITH TOTALS modifier is not supported by the in-memory provider.");`
- **Проверка:** unit-тест `InMemoryTests`/провайдерный на `NotSupportedException`; синхронизировать `docs/guide/04-grouping-and-aggregates.md` (EN+RU).

### ℹ️ Наблюдения (фикс не требуется)

- **Ключ плана продублирован.** Флаг уже участвует в `QueryPlanEqualityComparer.Equals`
  (`QueryPlanEqualityComparer.cs:45`) и `GetHashCode` (`:185`) — этого достаточно. Дополнительный `+1`
  в `GroupingPlanHash` (`QueryCommand.QueryPreparer.cs:500-505`) избыточен: `GroupingPlanHash` читается
  только внутри `GetHashCode` (`:230-231`), отдельного пути «только по хэшу» нет, а комментарий
  (`:500-501`) о переиспользовании plain-плана неверен (Equals сравнивает флаг). Дублирование безвредно;
  можно убрать фолд либо скорректировать комментарий.
- **`CopyTo` не копирует флаг явно.** `QueryCommand.Clone.cs:45-46` переносит `GroupingType`/`GroupingSets`,
  но не `GroupByWithTotals`. Сейчас корректно держится на том, что `CreateSelf`/`CreateSelfForClone`
  строятся через `Definition`, а `Definition` флаг несёт (`QueryCommand.cs:120`). Но `QueryPlan.cs:60-62`
  прямо отсылает к `CopyTo` как к месту инварианта хэша/`Equals` — для устойчивости стоит добавить
  `dst.GroupByWithTotals = GroupByWithTotals;`.
- **`.WithTotals()` без `.GroupBy(...)` — тихий no-op** на всех провайдерах: `SqlBuilder` смотрит флаг
  только внутри `if (grouping?.Length > 0)` (`SqlBuilder.cs:96,125`). Можно бросать/оговорить; низкий
  приоритет.
- **`WITH TOTALS` + `ROLLUP`/`CUBE` + `HAVING`.** ClickHouse отклоняет такую комбинацию
  (`InterpreterSelectQuery.cpp:1855`, «not supported together in presence of HAVING»), nextorm её
  эмитит. Ответ сервера — ошибка, тихо неверных строк нет; возможна строчка в доках.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **73/73**,
`tests/nextorm.postgres.tests --filter FullyQualifiedName~GroupByWithTotals` — **1/1** (прогнано
заново); `AnalysisLevel=latest-all` (rebuild) — **0** предупреждений на новых строках; в изменённых
файлах подавлений/слопа нет, соотношение подавлений проекта не изменилось (5 оправданных + 5 парных
`#pragma`, неоправданных — 0).

---

## 🔎 Точечный аудит — cross-provider session/info-функции (19.09.2026)

Область: `Query/SqlFunctions.cs` (пять методов `current_user`/`session_user`/`current_schema`/
`current_database`/`version`), `Query/SqlFunctions.Postgres.cs` (остались PG-only `gen_random_uuid`/
`uuidv7`/`pg_typeof`), `ISqlDialect`/`SqlDialectBase` (3 члена `SupportsSessionInfoFunctions`/
`SupportsSessionInfoFunction`/`MakeSessionInfoFunction`), новый `Visitors/SessionInfoFunctionTranslator`,
`NormSqlTranslator` (подключение, `:330`), `ExtendedScalarFunctionTranslator` (удалены пять имён),
`PostgresDialect`/`SqlServerDialect`/`MySqlDialect`/`ClickHouseDialect`/`SqliteDialect`, тесты шести
провайдеров + интеграционные. Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; `VersionOverride` — 0. |
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; транслятор — `static class`, ресурсов не удерживает. |
| 3. LINQ | ✅ `SessionInfoFunctionTranslator.Names` — `static readonly HashSet<string>` (`:16-21`), `Contains` — O(1); ни `.Count()`/`.ToList()`, ни LINQ на горячем пути. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw` — `NotSupportedException` (`:34,37`), `catch` не добавлено, `throw ex;`/`async void` нет. |
| 5. Проектирование | ✅ `SessionInfoFunctionTranslator` — 47 строк, `TryTranslate` — 23 строки; новых god-классов/длинных списков параметров нет. `MakeSessionInfoFunction` в диалектах — плоские `switch`/тернарник, без дублирующих `Make*`-членов. |
| 7. Хэш-ключи | ✅ Не затронуты. |

ℹ️ **Наблюдения (фикс не требуется):**
- Аллокаций на горячем пути нет: `Names` — статический `HashSet` (создаётся один раз), рендеры диалектов
  возвращают строковые литералы (`PostgresDialect.cs:86-88`, `MySqlDialect.cs:57-61`,
  `ClickHouseDialect.cs:96-98`, `SqliteDialect.cs:37`), без `string.Join`/`Format`.
- `SqlDialectBase` классовая `<summary>` (`:9`) утверждает, что «a dialect can never inherit a placeholder
  that throws at runtime»; новый `MakeSessionInfoFunction` (`:47-48`) — ещё один контрпример к
  `MakeRepeat` (`:172`), `MakeStringPosition` (`:220`), `MakeStringReverse` (`:237`). Утверждение было
  неверным до этого изменения; при желании переформулировать, отдельного фикса не требует.
- `SessionInfoFunctionTranslator` повторяет структуру `TextJsonSqlTranslator`/`JsonExtractSqlTranslator`/
  `DictionarySqlTranslator` (param-режим → `NeedAliasForColumn` → рендер), но отдельным транслятором:
  шестое почти-дублирование после `UuidFunctionTranslator` (см. раздел «cross-provider UUID-генераторы»),
  порог «свести к общему `Emit`-хелперу» достигнут (ср. наблюдения в аудитах
  `JSONExtract*`/`quantile`/`dictGet*`). ~20 строк на холодном пути построения плана, не регресс.
- XML-доки пяти методов ссылаются только на умбреллу-гейт — зарегистрировано как S4 в
  `API-NAMING-REVIEW.md`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.core.tests -c Release --no-build` — **153/153**,
`tests/nextorm.postgres.tests` — **163/163**, `…sqlserver` — **171/171**, `…mysql` — **38/38**,
`…mariadb` — **8/8**, `…clickhouse` — **73/73**, `…sqlite` — **202/202** (failed — 0); соотношение
подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных —
0). Чинить нечего.

---

## 🔎 Точечный аудит — ClickHouse `LIMIT n BY expr` (19.09.2026)

Область: `Query/LimitByClause.cs` (новый `internal sealed record`), `EntityBuilder<TEntity>.LimitBy` ×2 +
плюмбинг (`CopyTo`/`Select`/`ToCommand` + все 7 join-инициализаторов `JoinedEntityBuilder`),
`QueryDefinition.LimitBy`, `QueryCommand.LimitBy`/`LimitByColumns`/`LimitByPlanHash` +
`Definition`/`ResetPreparation`/`Clone.CopyTo`, `QueryCommand.QueryPreparer.PrepareLimitBy`,
`QueryPlanEqualityComparer`, `ISqlDialect.SupportsLimitBy`/`MakeLimitBy` + `SqlDialectBase`/
`ClickHouseDialect`, `SqlBuilder.MakeSelect`, `InMemoryQueryBuilder`, unit-/интеграционные тесты.
Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: в `src/` **5** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma disable` (все с парным `restore`). |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Изменением не затронуты: новых disposable-полей, `.Count()`/`.Any()`, подписок, `catch` нет; новые `throw` — `NotSupportedException` (`SqlBuilder.cs:242`, `InMemoryQueryBuilder.cs:90`) и `QueryPreparationException` (`QueryCommand.QueryPreparer.cs:470,473`) без `catch`. |
| 5. Проектирование | ✅ `LimitByClause` — 11 строк; `EntityBuilder.LimitBy` — 9/17 строк; плюмбинг однотипен `GroupBy`/`GroupByWithTotals`. ℹ️ `PrepareLimitBy` (`QueryCommand.QueryPreparer.cs:462-529`, ~60 строк) структурно повторяет `PrepareGrouping` (`:531-587`): при появлении третьего composite-ключа стоит вынести общий «expand `NewExpression` → `SelectExpression[]`» хелпер. |
| 7. Хэш-ключи | ✅ Находка 10 (исправлена 20.09.2026: поле `LimitByPlanHash` удалено рефакторингом `BuildKeyColumns`; см. раздел ниже). |

### ✅ Находка 10 — мёртвый `QueryCommand.LimitByPlanHash` (ИСПРАВЛЕНА 20.09.2026, см. точечный аудит ниже)

`PrepareLimitBy` считает `planHash` по `SelectExpression.PlanHashCode` (как `PrepareGrouping`), но
результат кладётся в `cmd.LimitByPlanHash` (`QueryCommand.QueryPreparer.cs:68`), который не читается
**нигде**: `rg LimitByPlanHash` даёт ровно 3 вхождения — поле (`QueryCommand.cs:33`), запись в
`Clone.CopyTo` (`QueryCommand.Clone.cs:21`) и сама запись в `Prepare`; в
`QueryPlanEqualityComparer.GetHashCode` клауза хэшируется напрямую (`:244-249`,
`Limit`/`Offset`/`Expression`), в отличие от `GroupingPlanHash` (`:241-242`). Т.е. вычисление
`selExp.PlanHashCode`/`planHash` в `PrepareLimitBy` — работа на построении плана впустую, а поле —
write-only состояние.

- **Было:** `cmd.LimitByPlanHash = limitByPlanHash == 11 ? 0 : limitByPlanHash;` + накопление `planHash`
  в `PrepareLimitBy`; поле копируется, но не читается.
- **Стало:** удалить поле, `dst.LimitByPlanHash = ...` и накопление `planHash` (оставив заполнение
  `LimitByColumns`), либо — если решено кэшировать хэш — добавить
  `if (obj.LimitByPlanHash != 0) hash.Add(obj.LimitByPlanHash);` в `GetHashCode` и убрать прямое
  хэширование клаузы, сохранив консистентность `Equals`↔`GetHashCode` (сейчас `Equals` сравнивает
  `LimitBy.Expression` через `_expComparer`).
- **Проверка:** ключ плана не меняется (`Equals` уже сравнивает клаузу напрямую); после фикса —
  `dotnet build nextorm.sln -c Release` 0/0 + core/clickhouse/postgres unit зелёные; желателен тест
  «два одинаковых `LimitBy`-запроса делят один план» (`PlanCacheTests`).

### ℹ️ Наблюдения (фикс не требуется)

- **Ключ плана полон.** `QueryPlanEqualityComparer.Equals` сравнивает `Limit`/`Offset`/`Expression`
  (`:47-56`), `GetHashCode` фолдит те же три значения (`:244-249`); `_limitByColumns` — derive от
  выражения, в `Equals` не нужен. `CloneForCache` переносит и клаузу, и колонки
  (`QueryCommand.Clone.cs:49-50`), `ResetPreparation` их сбрасывает (`QueryCommand.cs:245`),
  `Definition` несёт клаузу (`:124`), поэтому `Debug.Assert` в `QueryPlan.GetCacheVersion` не срабатывает.
  Порядок рендера (`SqlBuilder.cs:239-264`) — после `order by` (`:205-237`) и до `MakePage`
  (`:266-270`) — соответствует ClickHouse; в `ParamMode` `MakeColumn` вызывается столько же раз, что и
  в SQL-проходе, и в том же порядке (select-list обходится в конце метода в обоих проходах), поэтому
  параметры не дублируются и не смещаются.
- **SQL Server TOP не конфликтует:** `pageApplied` (`SqlBuilder.cs:66`) включает TOP только при
  `Dialect.MakeTop`, а `SupportsLimitBy=false` у SQL Server бросает раньше (`:241-242`) — тихого
  `limit by` + `top` не возникает.
- **`LimitByClause` как `record`.** Генерируемые `Equals`/`GetHashCode` сравнивают `LambdaExpression`
  по ссылке (у `Expression` нет структурного `Equals`), т.е. value-семантика record на деле не
  выполняется; сейчас это безвредно — тип нигде не используется как ключ и не сравнивается, план-сравнение
  делает `QueryPlanEqualityComparer`. Если тип остаётся `record`, стоит отметить это в `<remarks>`;
  иначе — `internal sealed class`.
- **Путь вычисляемого ключа не покрыт.** Для одиночного выражения-не-члена (`x => x.CreatedAt.Year`,
  любая функция) `MemberTranslator`/трансляторы ставят `NeedAliasForColumn=true`, и `SqlBuilder` допишет
  алиас (`SqlBuilder.cs:253-255`, `MakeColumnAlias` → ``as `key` ``) — ровно как `GROUP BY`.
  Документация ClickHouse для `LIMIT BY` определяет ключ как «any number of expressions» и `AS` в этой
  позиции не показывает; нужен SQL-gen-тест с вычисляемым ключом, чтобы подтвердить или снять алиас.
- **Ограничение строже диалекта.** `PrepareLimitBy` отвергает `limit <= 0` и `offset < 0`
  (`QueryCommand.QueryPreparer.cs:469-473`), тогда как ClickHouse поддерживает отрицательные `LIMIT BY`
  (выборка с конца группы) и смешанные знаки. Осознанное ограничение — стоит зафиксировать в XML-доке
  `EntityBuilder.LimitBy`/`ISqlDialect.MakeLimitBy`.
- **WIP-расхождения.** `WIP_limit_by.md:58` заявляет `LimitBy_WithOffsetAndOrderBy_ShouldOrderAndLimit`,
  фактический тест — `...ShouldOrderThenLimitBy` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:448`);
  раздел «Результат» (`:70-72`) не заполнен.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **76/76**,
`tests/nextorm.postgres.tests` — **164/164**, `tests/nextorm.core.tests` — **155/155** (прогнано
заново); реальный ClickHouse (Testcontainers, `ClickHouseIntegrationTests.LimitBy_ShouldTakeTopNPerKey`)
в этом проходе **не перезапускался** — по отчёту изменения зелёный; соотношение подавлений проекта не
изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — 0).

## 🔎 Точечный аудит — ClickHouse-модификаторы `FINAL`/`SAMPLE`/`PREWHERE`/`SETTINGS` (19.09.2026)

Область: `EntityBuilder<TEntity>` (5 fluent-методов + поля/свойства + `CopyTo`/`Select`/`ToCommand` + 8 объектных инициализаторов джойнов), `QueryDefinition`, `QueryCommand` (+ `Definition`/`ResetPreparation`/`Clone.CopyTo`/`QueryPreparer.PreparePreWhere`), `QueryPlanEqualityComparer`, `ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect`, `SqlBuilder.MakeSelect`, `InMemoryQueryBuilder`, unit-/интеграционные тесты. Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: **5** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma disable` (все с парным `restore`); новых подавлений нет. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей, `.Count()`/`.Any()`, подписок и `catch` нет; новые `throw` — `ArgumentOutOfRangeException`/`ArgumentException`/`ArgumentNullException` (`EntityBuilder.cs:220,223,245,259`) и `NotSupportedException` (`SqlBuilder.cs:82,90,108,293`; `InMemoryQueryBuilder.cs:93,96`) без `catch`. |
| 5. Проектирование | 🟡 Находка 12 (`PreWhere` не объединяет условия); ℹ️ ниже — ValueTuple/тип-разнобой, `IsFinal` vs `Final`, плюмбинг ×8. |
| 7. Хэш-ключи | ✅ Находка 11 (исправлена 20.09.2026: `RefreshInValuesShape` пересчитывает `PreWhereShapeHash` и сливает партиции; см. раздел ниже). |

### ✅ Находка 11 — `PREWHERE`-список не освежается в план-ключе (ИСПРАВЛЕНА 20.09.2026, см. точечный аудит ниже)

`PreparePreWhere` (`QueryCommand.QueryPreparer.cs:461-481`) повторяет первую половину `PrepareWhere`: хэширует форму in-списка в `PreWhereShapeHash` и складывает оценённые партиции в `cmd.InValuesPartitions`. Вторая половина — механизм повторного расчёта — не подключена:

- флага-аналога `HasTopLevelInValues` для `PreWhere` нет, поэтому `RefreshInValuesShape` (`:111-121`) на повторном выполнении уже подготовленной команды выходит по `if (!cmd.HasTopLevelInValues …) return;` и `PreWhereShapeHash` **не пересчитывается**;
- `RefreshInValuesShape` делает `cmd.InValuesPartitions = partitions` (`:117`), т.е. **затирает** PREWHERE-партиции, слитые `PreparePreWhere` (`:475-478`).

`QueryPlanEqualityComparer` сравнивает/хэширует устаревший `PreWhereShapeHash` (`:66,290`), ключ плана не меняется при изменении захваченной коллекции, а рендер `InValuesTranslator` (`Visitors/InValuesTranslator.cs:43`) находит в `InValuesPartitions` **старый** партицион. Для `PreWhere(x => ids.Contains(x.Id))`, выполненного дважды на одной команде при `ids`, изменённом между запусками, второй запуск отфильтрует по старым значениям/длине (тихий неверный результат либо рассинхрон числа параметров с кэшированным SQL). Это ровно сценарий, ради которого заведён `RefreshInValuesShape`; `WHERE` защищён, `PREWHERE` — нет.

- **Было:** `PreparePreWhere` считает `PreWhereShapeHash`, но `HasTopLevelInValues` отражает только `WHERE`; `RefreshInValuesShape` пересчитывает только `PreparedCondition` и **перезаписывает** общий словарь партиций.
- **Стало:** (1) поднять общий флаг «есть in-список в `WHERE` **или** `PreWhere`», (2) в `RefreshInValuesShape` пересчитывать и `PreWhereShapeHash` (из `PreparedPreWhere`), и **сливать** партиции вместо перезаписи словаря. `QueryPlanEqualityComparer` менять не нужно — он уже читает оба хэша.
- **Проверка:** `dotnet build nextorm.sln -c Release` 0/0; unit-тест «команда с `PreWhere(ids.Contains)` выполнена дважды после роста `ids` даёт план под новую длину» (по образцу in-values cache-тестов). Живой ClickHouse не требуется — ключ плана provider-agnostic.

### 🟡 Находка 12 — `PreWhere` не объединяет условия, в отличие от `Where` (ОТКРЫТА)

`EntityBuilder.PreWhere` (`EntityBuilder.cs:257-264`) перезаписывает `_preWhere`, тогда как `Where` (`:181-194`) соединяет предикаты через `AndAlso`. `.PreWhere(a).PreWhere(b)` молча теряет `a`, хотя цепочка выглядит накопительной. WIP (`WIP_query_modifiers.md:30-31`) заявляет образец `Where` — по подготовке выражения образец соблюдён, по семантике — нет.

- **Было:** `b._preWhere = condition;`
- **Стало:** повтор объединения `Where` — `ReplaceParameterExpressionVisitor` + `AndAlso` (ClickHouse допускает одну `PREWHERE`, конъюнкция даёт предсказуемую семантику), либо явная XML-оговорка «последний вызов побеждает», если перезапись осознанна.
- **Проверка:** build 0/0; unit-тест `.PreWhere(a).PreWhere(b)` → SQL содержит `prewhere (a) and (b)` (или закреплённая перезапись).

### ℹ️ Наблюдения (фикс не требуется)

- **FINAL/SAMPLE после алиаса — верно, в т.ч. с join'ами.** ClickHouse (`ParserTableExpression`/`ASTTableExpression`) требует `FROM table [AS alias] FINAL [SAMPLE …]`; `MakeFrom` возвращает `table as t1` при `needAlias` (`SqlSourceRenderer.cs:212-223`), а `SqlBuilder` дописывает модификаторы сразу за `fromStr` (`:77-93`) до `JOIN` (`:96-103`). Порядок совпадает с грамматикой. Unit-тест покрывает только не-join-случай — join-вариант с `.Final()` можно добавить для регресса.
- **`PREWHERE` после `JOIN`, перед `WHERE` — верно** (`SqlBuilder.cs:96-118`), совпадает с порядком ClickHouse.
- **SETTINGS в конце — верно, но значения сырые.** `SqlDialectBase.MakeSettings` (`:130-131`) и `Settings(params …)` (валидируется только ключ, `EntityBuilder.cs:244-245`) вставляют `key = value` без кавычек; строковое значение ClickHouse требует `'…'`, поэтому корректный вызов возможен только вручную. XML-док предупреждает «only pass trusted literals» (`:230-234`) — осознанно, но это P2-риск поверхности (FM2 в `API-NAMING-REVIEW.md`).
- **`params`-ValueTuple vs `KeyValuePair`.** Публичная `Settings(params (string Key, string Value)[])` не совпадает по типу с `IReadOnlyList<KeyValuePair<string,string>>` на команде/`QueryDefinition`; имена tuple-элементов не часть CLR-контракта (FM2).
- **Пустой `Settings()`.** `Settings()` без аргументов создаёт пустой список (`EntityBuilder.cs:240`), а `KeyValueListsEqual` (`QueryPlanEqualityComparer.cs:139-152`) считает `null` и `[]` **разными** → лишний промах кэша для запроса, эквивалентного «без settings»; при желании нормализовать `[]`→`null`.
- **Плюмбинг ×8.** Модификаторы пробрасываются через `Select`/`ToCommand`/`CopyTo` и 8 объектных инициализаторов (`EntityBuilder.cs:496,527` + `JoinedEntityBuilder.cs:40,92,132,172,212,252`) очень длинной строкой; при следующем ClickHouse-модификаторе вынести состояние билдера в параметр-объект (ср. `QueryDefinition`).
- **FINAL/SAMPLE на производной таблице/TVF не guard'ятся.** `SqlBuilder.cs:79-93` дописывает их к любому `from`, включая подзапрос (`DataContextExtensions.cs:87-88`) и table function (`:104`), где ClickHouse их не принимает (FM4).
- **in-memory теперь бросает (в отличие от Находки 9).** `InMemoryQueryBuilder.cs:92-96` бросает `NotSupportedException` на FINAL/SAMPLE/SETTINGS/PREWHERE; `WITH TOTALS` по-прежнему игнорируется молча (Находка 9 — отдельная).
- **XML-доки — все новые публичные члены задокументированы**; новых публичных типов нет → Приложение A (45) не меняется. `CS1591` остаётся в `<NoWarn>` 7 `.csproj` (Шаг 5).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **83/83**, `tests/nextorm.core.tests` — **156/156**; `tests/nextorm.postgres.tests` — **164/165, 1 failed** (`OrderedSetAggregates_ShouldEmitWithinGroup`: `NotSupportedException: percentile_cont must be completed with Over(...)`, `NormSqlTranslator.cs:70`) — детерминированный и **не связан** с модификаторами (стек не проходит через FINAL/SAMPLE/PREWHERE/SETTINGS; заявленные в задаче 165/165 в текущем дереве не воспроизводятся). Реальный ClickHouse в этом проходе не перезапускался (по отчёту SETTINGS зелёный). Соотношение подавлений не изменилось (5 + 5, неоправданных — 0).

### Повторный проход 19.09.2026 (после закрытия док-разрыва)

Build Release — **0/0** (заново). Тесты перезапущены на текущем дереве: ClickHouse **83/83**,
PostgreSQL **165/165**, core **156/156**. Предыдущая проверка выше фиксировала PostgreSQL **164/165**
(1 unrelated-провал `OrderedSetAggregates_ShouldEmitWithinGroup`); в текущем дереве тест зелёный, т.е.
165/165 подтверждается. Подавления не менялись: 5 `SuppressMessage` (обоснованные, `<Pending>` — 0)
+ 5 парных `#pragma`, в изменении — 0.

Статус открытых находок (код модификаторов не менялся): **Находка 11 держится** — `RefreshInValuesShape`
(`QueryCommand.QueryPreparer.cs:113,117`) по-прежнему гейтится `HasTopLevelInValues` (только `WHERE`) и
перезаписывает `InValuesPartitions`; **Находка 12 держится** — `EntityBuilder.cs:262` всё ещё
`b._preWhere = condition;`. Док-разрыв `FM1` (API-реестр) закрыт. **Актуализация 20.09.2026:** Находка 11 больше не держится — `PreparePreWhere` заводит `HasPreWhereInValues`, а `RefreshInValuesShape` пересчитывает `PreWhereShapeHash` и сливает партиции (см. точечный аудит модификаторов ниже); Находка 12 всё ещё открыта.

ℹ️ Новое наблюдение — `Sample` пропускает `NaN`. `ratio is < 0 or > 1` (`EntityBuilder.cs:219`) и
`offset is < 0 or > 1` (`:222`) для `double.NaN` ложны, поэтому `Sample(double.NaN)` принимается и
`MakeSample` рендерит `sample NaN` (`SqlDialectBase.cs:117`) — ошибка ClickHouse в рантайме вместо
`ArgumentOutOfRangeException`; `Infinity` ловится (`> 1`). Фикс — `double.IsFinite` в проверке (FM6).

ℹ️ Новое наблюдение — порядок `Settings` участвует в план-ключе. `KeyValueListsEqual`
(`QueryPlanEqualityComparer.cs:139-152`) сравнивает по индексу, `GetHashCode` добавляет пары
последовательно (`:280-285`), поэтому `Settings(a, b)` и `Settings(b, a)` — разные ключи (лишний
промах кэша; SETTINGS неупорядочен семантически, неверный план невозможен). Нормализация — по желанию.

ℹ️ Дополнение к in-memory наблюдению. `InMemoryQueryBuilder.cs:92-93` одним условием покрывает
`Final`/`SampleRatio`/`Settings`, но `QueryModifiers_ShouldThrow` (`InMemoryTests.cs:547-551`) прямо
утверждает только `Final` и `PreWhere`; `Sample`/`Settings` — под тем же guard'ом, но без отдельной
проверки.

## 🔎 Точечный аудит — cross-provider `any_agg` + оконные `percentile_cont`/`percentile_disc` (19.09.2026)

Область: перенос `any_agg` `ClickHouseFunctions` → `CommonFunctions` (`Query/SqlFunctions.cs:318-324`), новый флаг `SupportsAnyValueAggregate` (`ISqlDialect.cs:313`, база `SqlDialectBase.cs:79`, override MySQL `MySqlDialect.cs:49` / ClickHouse `ClickHouseDialect.cs:55`), маппинг `MakeAggregate` (`MySqlDialect.cs:215`, `ClickHouseDialect.cs:239`), транслятор `AdvancedAggregateTranslator.cs:123-128`; оконные `CommonFunctions.percentile_cont`/`percentile_disc` (`SqlFunctions.cs:413,416`), `SupportsPercentileWindow` (`ISqlDialect.cs:163`, база `:37`, SQL Server `:116`, MariaDB `:17`), `WindowSql.cs:25-26`, `WindowFunctionTranslator.cs:50-52,78-85`, guard `NormSqlTranslator.cs:67-73`. Build Release — **0/0**; unit: core **156**, mysql **40**, mariadb **9**, clickhouse **83**, sqlserver **173**, postgres **165**, sqlite **203** (0 failed). Контейнерные интеграционные тесты не запускались (Podman-сокет отсутствует) — SQL-gen + все unit зелёные.

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: **5** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma disable` (все с парным `restore`); новых подавлений нет. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей, `.Count()`/`.Any()`, подписок и `catch` нет; новые `throw` — `NotSupportedException` через `EmitSimple`/`EmitOrdered` без `catch`. |
| 4. Проектирование | ✅ Находка 13 (`AdvancedAggregateTranslator` диспетчеризация по имени) исправлена 19.09.2026 — добавлен `DeclaringType == typeof(PostgresFunctions)`. ℹ️ ниже — доки `SQL Server`/`any` расходятся с кодом (XP1/XP5), см. `API-NAMING-REVIEW.md`. |
| 7. Хэш-ключи | ✅ План-ключ не менялся: `any_agg`/`percentile_cont`/`percentile_disc` — новые методы DSL, в `QueryPlanEqualityComparer` не входят, `_hashPlan`/cache-ключи не затронуты. |

### ✅ Находка 13 — `AdvancedAggregateTranslator` диспетчеризует `percentile_cont`/`percentile_disc` по имени (ИСПРАВЛЕНА 19.09.2026)

`CommonFunctions.percentile_cont<T>(double, T?)` (`Query/SqlFunctions.cs:413`) и `PostgresFunctions.percentile_cont<T>(double, Expression<Func<T>>)` (`Query/SqlFunctions.Postgres.cs:456`) носят одно имя (`percentile_disc` — аналогично, `:416`/`:459`). Сейчас верный маршрут обеспечивает только guard в `NormSqlTranslator.cs:71-73` (`DeclaringType == typeof(CommonFunctions)` → «must be completed with Over»), который выполняется **раньше** `AdvancedAggregateTranslator.TryTranslate`; сам `AdvancedAggregateTranslator.cs:84,87` матчит по `node.Method.Name` и `Arguments.Count == 2`, без проверки объявляющего типа (в отличие от `ArraySqlTranslator`/`WindowFunctionTranslator`, где `DeclaringType` проверяется явно). Любой новый вход или перестановка веток отправит оконный вызов в `EmitOrdered` и отрендерит `percentile_cont(f) within group (order by value)` **без `over (...)`** — невалидный SQL на SQL Server/MariaDB вместо внятной ошибки.

- **Было:** `case nameof(PostgresFunctions.percentile_cont) when node.Arguments.Count == 2:` (только имя + арность; то же для `percentile_disc`).
- **Стало:** добавить `&& node.Method.DeclaringType == typeof(PostgresFunctions)` к обоим case'ам (`:84,87`) — зеркально guard'у `NormSqlTranslator`.
- **Проверка:** build 0/0; тест, что `SqlFunctions.Sql.percentile_cont(0.5, x.Id)` без `.Over()` даёт `NotSupportedException` с «Over» (косвенно уже покрыто), и что PostgreSQL ordered-set по-прежнему рендерится (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1750`).
- **Исправлено 19.09.2026:** к обоим case'ам добавлено `&& node.Method.DeclaringType == typeof(PostgresFunctions)` (`AdvancedAggregateTranslator.cs:84-89`); build 0/0, PostgreSQL `OrderedSetAggregates_ShouldEmitWithinGroup` зелёный, SQL Server/MariaDB `PercentileWindow_ShouldEmitWithinGroupOver` зелёные.

### ℹ️ Наблюдения (фикс не требуется)

- **`EmitSimple` для `any_agg` — верно.** Маппинг имени вынесен в `MakeAggregate` (`MySqlDialect.cs:215` → `ANY_VALUE`, `ClickHouseDialect.cs:239` → `any`); SQLite/PostgreSQL/SQL Server не рендерят, а бросают `NotSupportedException("*ANY_VALUE*")` до вывода SQL (`AdvancedAggregateTranslator.cs:123-125`).
- **Дублирующий тест `any_agg`.** `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:299` всё ещё зовёт `SqlFunctions.ClickHouse.any_agg` (резолвится через наследование от `CommonFunctions`) — не дефект, но дублирует `AnyValueAggregate_ShouldUseClickHouseAny` (`:308`). См. `API-NAMING-REVIEW.md` XP6.
- **Сводка `AdvancedAggregateTranslator` неполна.** XML-`<summary>` (`:5-21`) перечисляет семейства, но не упоминает `any_agg`/`any_last`, которые теперь обрабатывает `switch`; косметика.
- **SQL Server `ANY_VALUE` не включён намеренно.** Тест `AnyValueAggregate_ShouldThrowBecauseSqlServerVersionLacksIt` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1477`) фиксирует поведение, но XML-доки обещают SQL Server — расхождение заведено в `API-NAMING-REVIEW.md` XP1.

## 🔎 Точечный аудит — ClickHouse табличные функции `numbers`/`numbers_mt` (19.09.2026)

Область: `SqlFunctions.INumbersRow` (`Query/SqlFunctions.cs:99-108`), `ClickHouseFunctions.numbers` ×3 + `numbers_mt` (`Query/SqlFunctions.ClickHouse.cs:145-167`), `ISqlDialect.WrapTableFunction` (`DataContext/Dialect/ISqlDialect.cs:668-671`), `SqlDialectBase.WrapTableFunction` (`:118-122`), `ClickHouseDialect.SupportsTableFunction`/`WrapTableFunction` (`:165-176`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:274-338`, обёртка `:312-317`). Build Release — **0/0**; unit: clickhouse **89**, core **157**, postgres **168** (0 failed); интеграция с реальным ClickHouse зелёная.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; хук — чистая строковая функция. |
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: **5** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma disable` (все с парным `restore`). |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей, LINQ-цепочек, подписок и `catch` нет; новые `throw` — `NotSupportedException()` в телах DSL-методов, как у соседних `generate_series`/`string_split`. |
| 4. Проектирование | ✅ `WrapTableFunction`-оверрайды — 4 строки; `MakeTableFunction` — 65 строк; god-классов не создано. ℹ️ ниже — двойной `ToString()`, контракт алиаса. |
| 7. Хэш-ключи | ✅ План-ключ не менялся: `[SqlTableFunction]` уже входит в `FromExpression`, а новый хук — производная рендера и в `QueryPlanEqualityComparer` не входит. |

### ℹ️ Наблюдения (фикс не требуется)

- **Двойной `ToString()` на пути сборки SQL.** `SqlSourceRenderer.cs:312-313`: `WrapTableFunction(function.Name, sqlBuilder.ToString())`, затем повторный `sqlBuilder.ToString()` в `string.Equals(...)`. База `WrapTableFunction` — no-op (`SqlDialectBase.cs:122`), поэтому на всех диалектах, кроме ClickHouse-`numbers`, это **две** лишние строковые аллокации (плюс третья при обёртке). Правка в одну строку: сохранить `var call = sqlBuilder.ToString();` и сравнивать с `call`. Путь сборки плана (не материализации), поэтому ℹ️.
- **Контракт `WrapTableFunction` неявно требует `RequireSubqueryAlias`.** Обёртка — производная таблица; алиас добавляется при `needAlias || RequireSubqueryAlias` (`:319`). ClickHouse выставляет `RequireSubqueryAlias => true` (`ClickHouseDialect.cs:17`), поэтому сейчас корректно (док-пример: `… )) as t1`), но диалект-обёртчик без этого флага отрендерит безалиасный подзапрос. Стоит закрепить инвариант в XML-доке хука либо выставлять `needAlias` при непустой обёртке.
- **Обёртка применяется ко всем TVF по имени.** `:312` не различает встроенную и пользовательскую `[SqlTableFunction]`; пользовательская TVF с именем `numbers` на ClickHouse будет обёрнута ошибочно. Край, не дефект.
- **Параметр-пасс не затронут (подтверждение).** В `ParamMode` (`:284-293`) аргументы обходятся в том же порядке, что и в SQL-проходе, а `WrapTableFunction` не вызывается — обёртка новых параметров не вводит, позиции не смещаются. Join/apply передают `needAlias=true` (`MakeJoin` `:114,148`, `MakeApplyJoin` `:183`), поэтому алиас есть и там.
- **Юнит-тест не закрепляет обёртку.** `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:472,485` проверяют только `Contain("from numbers(@count)")`; слой `toInt64` держит интеграционный тест — см. `API-NAMING-REVIEW.md` TF5 (аналог U6).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/<p> -c Release --no-build` — clickhouse **89/89**, core **157/157**, postgres **168/168**; в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — 0; `.editorconfig` глушит 7 правил (S125/S108/CA2254/S3060/S1104/S3604/S2292 — `silent`), 6 из них инертны без `SonarAnalyzer` (пре-существующее, см. «Примечания»).

## 🔎 Точечный аудит — cross-provider UUID-генераторы (19.09.2026)

Область: `Query/SqlFunctions.cs` (`gen_random_uuid`/`uuidv7` переехали с `PostgresFunctions` на
`CommonFunctions`), `Query/SqlFunctions.Postgres.cs` (остался PG-only `pg_typeof`), `ISqlDialect`/
`SqlDialectBase` (3 члена `SupportsUuidGenerators`/`SupportsUuidGenerator`/`MakeUuidGenerator`), новый
`Visitors/UuidFunctionTranslator`, `NormSqlTranslator` (подключение, `:336`),
`ExtendedScalarFunctionTranslator` (два имени удалены из `DirectFunctions`),
`PostgresDialect`/`SqlServerDialect`/`ClickHouseDialect`/`MariaDbDialect`, тесты шести провайдеров +
интеграционные. Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`); соотношение не изменилось. |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `UuidFunctionTranslator` — `static class`, ресурсов не удерживает. |
| 3. LINQ | ✅ `Names` — `static readonly HashSet<string>` (`UuidFunctionTranslator.cs:16-19`), `Contains` — O(1); ни `.Count()`/`.ToList()`, ни LINQ на горячем пути. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw` — `NotSupportedException` (`UuidFunctionTranslator.cs:32,35`), `catch` не добавлено, `throw ex;`/`async void` нет. |
| 5. Проектирование | ✅ `UuidFunctionTranslator` — 45 строк, `TryTranslate` — 23; новых god-классов/длинных списков параметров нет. ℹ️ ниже — это шестой почти-дубликат семейного транслятора. |
| 7. Хэш-ключи | ✅ Не затронуты: методы DSL в `QueryPlanEqualityComparer`/кэш-ключи не входят. |

### ℹ️ Наблюдения (фикс не требуется)

- **`UuidFunctionTranslator` структурно повторяет `SessionInfoFunctionTranslator`.** Оба класса —
  `static readonly HashSet<string> Names` + `TryTranslate(visitor, node)` из четырёх шагов:
  `Names.Contains`, умбрелла-гейт, предикат-по-имени, param-режим → `NeedAliasForColumn` →
  `dialect.Make*(name)` (`UuidFunctionTranslator.cs:16-44` против `SessionInfoFunctionTranslator.cs:16-46`).
  Различаются только именами, флагом и текстом исключений. Это **не проблемное дублирование**: структура —
  сознательная архитектура «одно семейство → один транслятор», код на холодном пути построения плана,
  аллокаций на горячем пути нет. Но это уже шестой почти-дубликат (после `TextJson`/`JsonExtract`/
  `Dictionary`/`SessionInfo`) — порог, заявленный в наблюдении session-info, достигнут; при следующем
  семействе стоит вынести общий `Emit`-хелпер (с делегатом-рендерером).
- **In-memory молча отдаёт `null` вместо исключения.** `CommonFunctions.gen_random_uuid()` объявлен как
  `=> default!;` (`SqlFunctions.cs:225`), а in-memory провайдер компилирует дерево выражения
  (`InMemoryQueryBuilder`/`InMemoryConditionFactory`) и вызывает реальную заглушку; отдельного guard'а на
  скалярные `CommonFunctions`-вызовы нет (в отличие от `LIMIT BY`/`PREWHERE`/`FINAL`
  `InMemoryQueryBuilder.cs:89-96`). Поэтому `InMemoryDataContext…Select(x => SqlFunctions.Sql.gen_random_uuid())`
  вернёт `null` (`Guid?`), а не бросит. Это **общее пре-существующее поведение всего скалярного DSL**
  (`like`, `version`, `current_user` и т.п. ведут себя так же), не регресс от UUID-промоушена; тестов
  in-memory на новое семейство нет. Если in-memory должен явно отклонять серверные функции, нужен
  отдельный guard — вне рамок этого изменения.
- **Флаг возможностей не зависит от версии сервера.** `PostgresDialect.cs:97` разрешает `uuidv7` на любой
  PostgreSQL (функция с PG18), `MariaDbDialect.cs:23` — на любой MariaDB (11.7+), `gen_random_uuid` — на
  PG13+. На старом сервере генерируется SQL, падающий на сервере («function uuidv7() does not exist»), а
  не `NotSupportedException` библиотеки. Диалект статичен и версии не знает; ограничение оговорено в
  XML-доках (`SqlFunctions.cs:222-223,230-231`) и `docs/guide/11` — принято (см. `API-NAMING-REVIEW.md`, UG3).
- **Путь рантайма покрыт неравномерно.** Unit (SQL-gen + dialect-hooks) — все шесть провайдеров;
  интеграционно: PostgreSQL `PostgresSpecificTests.cs:20` — только v4, ClickHouse
  `ClickHouseIntegrationTests.cs:32-33` — v4+v7, SQL Server `SqlServerSpecificTests.cs:50` — v4 (v7
  корректно reject'ится), MariaDB — 0. `uuidv7()` реально исполняется только на ClickHouse; образ
  `postgres:17-alpine` старше PG18. Добавить PG18-образ — по желанию (ср. UG3).
- **Классовая `<summary>` `ExtendedScalarFunctionTranslator.cs:5-16`** по-прежнему перечисляет
  math/string/regexp/date-time/`num_nulls`, но не uuid/session-info (они вынесены в отдельные трансляторы);
  тип `internal`, на CS1591 не влияет — тот же класс косметики, что в session-info-аудите.
- **`SqlDialectBase` классовая `<summary>` (`:9`)** утверждает, что «a dialect can never inherit a
  placeholder that throws at runtime»; `MakeUuidGenerator` (`:70-71`) — ещё один контрпример (к
  `MakeRepeat`/`MakeStringPosition`/`MakeSessionInfoFunction`). Утверждение было неверным до этого
  изменения; отдельного фикса не требует.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`dotnet test` (Release, `--no-build`) — core **157/157**, postgres **168/168**, sqlserver **176/176**,
mysql **42/42**, mariadb **11/11**, clickhouse **89/89**, sqlite **205/205** (failed — 0); integration
**909 / 0 failed / 696 skipped** (без `DOCKER_HOST`: реальные PostgreSQL/SQL Server/MySQL/ClickHouse не
запускались, поэтому `uuidv7()` и `newid()`/`UUID_v4()` интеграционно в этом проходе не проверялись);
в изменённых файлах подавлений/слопа нет, соотношение подавлений проекта не изменилось
(5 оправданных + 5 парных `#pragma`, неоправданных — 0).

## 🔎 Точечный аудит — ClickHouse счётчики `count`/`count_big`/`count_distinct`/`count_big_distinct`/`count_if`/`count_over` (19.09.2026)

Область: `DataContext/Dialect/ISqlDialect.cs` (`MakeCount` `:662-666`, `WrapsCountResult` `:667-672`, `WrapCount` `:673-680`), `SqlDialectBase.cs` (`:407,411,413`), `ClickHouseDialect.cs` (`:150-158`), `Visitors/NormSqlTranslator.cs` (`:224-268`), `Visitors/AdvancedAggregateTranslator.cs` (`:282-298`), `Visitors/WindowFunctionTranslator.cs` (`:76,92-95,150-157`), тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:315-347`, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs:74-80`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:18-42,338-344`. Build Release — **0/0**; unit: clickhouse **94/94**, core **157/157** (0 failed); контейнерная интеграция с реальным ClickHouse — **3/3** (`Count_ShouldReturn10`, `CountAggregates_ShouldCastToInt64InProjection`, `CountIf_ShouldReturnCount`).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: `WrapCount` — чистая строковая функция, трансляторы ресурсов не удерживают; тесты по-прежнему оборачивают контекст в `using var`. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`); соотношение не изменилось. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек нет; `WrapCount` — конкатенация строк; `node.Method.Name.EndsWith`/`Contains` — пре-существующие скалярные проверки на пути сборки плана. |
| 5. Проектирование | ✅ Оверрайды — 2 строки (`ClickHouseDialect.cs:155-158`); длинных списков параметров и god-классов не создано. `ISqlDialect` вырос на 2 члена; рост однотипен ранее принятым `Supports*`/`Make*`-хукам (см. TF-аудит). |
| 4. События / 6. Исключения | ✅ Подписок нет; новых `throw`/`catch` нет. |
| 7. Хэш-ключи | ✅ План-ключ не менялся: `WrapCount` — производная рендера, SQL-строка и так входит в `QueryPlan` (`QueryPlan.cs:24-31`), а `QueryPlanStore` ключуется парой `(ContextType, QueryPlan)` (`QueryPlanStore.cs:22-25`), поэтому ClickHouse-обёртка не может «протечь» в план другого провайдера. |

### ℹ️ Наблюдения (фикс не требуется)

- **Гейт аллокаций подтверждён (запрошено в задаче).** Во всех трёх точках `StringBuilder.ToString(start, len)` + `Length = start` вызываются **только** внутри `if (... WrapsCountResult)`: `NormSqlTranslator.cs:259-265`, `AdvancedAggregateTranslator.cs:292-298`, `WindowFunctionTranslator.cs:152-157`. `countStart`/`aggregateStart`/`windowStart` — обычные `int`, вычисляются всегда, аллокаций не дают. На не-ClickHouse диалектах (`WrapsCountResult => false`, `SqlDialectBase.cs:411`) суб-строка не создаётся и `WrapCount` не вызывается. ✔
- **Обёртка применяется до `filter (where ...)`.** `NormSqlTranslator.cs:257-268`: сначала закрывается `count(...)` и применяется `WrapCount`, и только потом `AggregateFilter.Append` дописывает ` filter (where ...)`. Для диалекта, который одновременно `WrapsCountResult => true` и `SupportsFilter => true`, вышло бы `toInt32(count(*)) filter (where ...)` — невалидно (FILTER обязан следовать за агрегатом, а не за скалярной `toInt32`). Сегодня **недостижимо**: `WrapsCountResult` выставляет только ClickHouse, а `SupportsFilter` у него `false` (`SqlDialectBase.cs:33`), поэтому `RequireFilter` (`NormSqlTranslator.cs:387-390`) бросает раньше. Латентная связка двух флагов; при появлении третьего диалекта-обёртчика wrap-блок надо перенести после `AggregateFilter.Append`. ℹ️
- **`count_big_distinct` не покрыт.** Из шести вариантов только `count_big_distinct` не имеет ни unit-, ни integration-ассерта (`rg count_big_distinct tests/` — 0). Гейт `toInt64` для него выводится из общего кода, но не зафиксирован — см. `API-NAMING-REVIEW.md` CNT4. ℹ️
- **Комментарий integration-теста неточен.** `ClickHouseIntegrationTests.cs:26-27` пишет «the dialect casts them to Int64», хотя `count()`/`count_distinct()`/`count_if()` кастятся в `Int32` (`toInt64` — только `*_big`). Косметика комментария — см. `API-NAMING-REVIEW.md` CNT5. ℹ️
- **Параметр-пасс не затронут (подтверждение).** Wrap-блоки стоят под `if (!visitor.IsParamMode)` (`NormSqlTranslator.cs:255`, `AdvancedAggregateTranslator.cs:272`) либо после `ParamMode`-ветки (`WindowFunctionTranslator.cs:57-71`), поэтому `WrapCount` на параметрическом проходе не вызывается, номера `@pN` не сдвигаются; обёртка вводит только `toInt32`/`toInt64`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **94/94**, `tests/nextorm.core.tests` — **157/157**; `DOCKER_HOST=… dotnet run --project tests/nextorm.integration.tests -c Release --no-build -- -method …ClickHouseIntegrationTests.Count*` — **3/3** (реальный ClickHouse); `rg "WrapsCountResult|WrapCount" src` — 6 объявлений/оверрайдов в 3 диалект-файлах + 3 использования в visitors, мёртвых вхождений нет; в изменённых файлах подавлений/слопа нет, соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — 0).

### Поправка по итогам точечного аудита 2 (19.09.2026)

Точечный аудит 2 (cross-provider conditional/`any_agg`/`percent_rank`) закрыл ложные утверждения
«не поддерживается» — правки документационные, кода (кроме новых гейт-флагов) не добавлялось:

- **ClickHouse `percent_rank`/`cume_dist`** — движок их поддерживает; прежняя ошибка возникала при вызове
  без `OVER (...)` (`ClickHouseDialect.SupportsPercentRankCumeDist => true`).
- **MariaDB `ANY_VALUE`** — `any_agg` гейтится `MariaDbDialect.SupportsAnyValueAggregate => false`
  (нет `ANY_VALUE` до 13.2, MDEV-10426), а не наследует MySQL.
- **`iif`** — перенесён на кросс-провайдерный `CommonFunctions` (`SupportsIif`/`MakeIif`); `choose`
  остаётся SQL Server-only (`SupportsChoose`), объединённый `SupportsIifChoose` разделён.
- **`nth_value`** — добавлен `CommonFunctions.nth_value` + `SupportsNthValue` (SQL Server гейтит).

Изменение новых запахов не вносит: добавленных `SuppressMessage`/`#pragma`/`NoWarn`, пустых `catch`,
`Task.Delay`/`Thread.Sleep`, `Skip=` нет; `IDisposable`, события, LINQ и хэш-ключи не затронуты —
план-ключ не менялся (новые члены DSL в `QueryPlanEqualityComparer` не входят, `_hashPlan`/cache-ключи
не затронуты). Новые флаги и `Make*`-хуки однотипны принятым `Supports*`/`Make*`-парам. Build Release —
**0/0**; соотношение подавлений проекта не изменилось (5 `SuppressMessage` с обоснованием + 5 `#pragma`
с `restore`, неоправданных — 0).

### Повторная верификация cross-provider `iif`/`choose`/`nth_value` (19.09.2026)

Независимый повторный проход по тому же изменению (см. «Поправка по итогам точечного аудита 2»
выше): подтверждает вывод «чисто» и добавляет метрики/наблюдения. Build Release — **0/0**
(прогнано заново). Новых запахов каталога нет.

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` в изменённых файлах — **0**. База: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma warning disable` — **5** (все с парным `restore`; адреса `EntityBuilderExtensions.cs:61,70,78`, `InMemoryLinqSource.cs:82`, `ExpressionPlanEqualityComparer.cs:406`). Неоправданных — 0. |
| Слоп-паттерны (slopwatch вручную; локальный tool не установлен) | ✅ `Skip=` — 0; пустых `catch` — 0; `Thread.Sleep` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); инлайновых `Version`/`VersionOverride` — 0. Ложных/пустых ассертов в новых тестах нет. |
| 1/3/4/6/7. `IDisposable`, LINQ, события, исключения, хэш-ключи | ✅ Не затронуты: новых disposable-полей/локальных, `.Count()`/`.Any()`/`CA1827`/`CA1851`, подписок, `catch`, `GetHashCode` нет. План-ключ не менялся: члены DSL и гейт-флаги в `QueryPlanEqualityComparer`/`DataContext/Cache` отсутствуют (`rg "MakeIif\|SupportsIif\|SupportsChoose\|nth_value\|SupportsNthValue\|SupportsPercentRankCumeDist" src/nextorm.core/Expressions src/nextorm.core/Query/QueryPlanEqualityComparer.cs src/nextorm.core/DataContext/Cache` — пусто). `WindowSql.MapWindowFunctionName` (`:23`) — через `nameof`; `BuiltinFunctionTranslator` — по `node.Method.Name`. |
| 5. Проектирование | ✅ Новых god-классов нет: `BuiltinFunctionTranslator` — 354 строки, `WindowFunctionTranslator` — 184, `WindowSql` — 152; `EmitIif` — 20 строк, `EmitChoose` — 8. ℹ️ `ISqlDialect.cs` — **757** строк (нетто +3 члена за изменение: −`SupportsIifChoose`, +`SupportsIif`/`SupportsChoose`/`MakeIif`/`SupportsNthValue`/`SupportsPercentRankCumeDist`); это осознанная схема capability-флагов, декомпозиция — предмет `solid-review.md`, не запах-регресс. |

ℹ️ **Наблюдения (фикс не требуется):**
- **Throwing-заглушка `MakeIif`** (`SqlDialectBase.cs:331-332`) повторяет уже принятый паттерн
  `MakeSessionInfoFunction` (`:58`) и `MakeUuidGenerator` (`:71`), гейтится `SupportsIif` (дефолт
  `false`); все 5 SQL-диалектов переопределяют оба члена, рантайм-падения нет. Противоречие с
  классовым доком (`:6-11`) зарегистрировано как **IF3** в `API-NAMING-REVIEW.md`.
- **XML-доки новых override отсутствуют** (`MakeIif` ×4, `Supports*`-флаги ×6; `NoWarn=CS1591`) —
  зарегистрировано как **IF1/IF2** в API-реестре.
- **Остаточный тест-пробел I6:** guard на захваченный массив для `choose`
  (`FlattenChoose` не разворачивает не-`NewArrayExpression`) не добавлен; ассерт `iif` разбит на два
  `Contain` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:46-47`).
- **Косметика** `SqlFunctions.cs:126-136`: строки XML-`<summary>` `CommonFunctions` смещены
  (`:128-129`), внутри тела — строчный `// Common (cross-provider) surface.` (`:136`); на DocFX не
  влияет, привести при закрытии Шага 5.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано заново);
`rg "SuppressMessage|#pragma warning disable" src` — 5/5 без изменений; `rg "SupportsIifChoose|EmitIifChoose" src`
— 0; соотношение подавлений проекта не изменилось (5 оправданных + 5 парных `#pragma`, неоправданных — 0).

## 🔎 Точечный аудит — ClickHouse табличные функции `zeros`/`zeros_mt` (20.09.2026)

Область: `SqlFunctions.IZerosRow` (`Query/SqlFunctions.cs:111-120`), `ClickHouseFunctions.zeros`/`zeros_mt` (`Query/SqlFunctions.ClickHouse.cs:170-185`), `ClickHouseDialect.SupportsTableFunction` (`src/nextorm.clickhouse/ClickHouseDialect.cs:183-185`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:311-317`), тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:555-579`, `ClickHouseDialectTests.cs:98-105`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1458-1468`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:177-187`. Build Release — **0/0**; unit: clickhouse **96/96**, core **157/157**, postgres **171/171** (0 failed); контейнерная интеграция `ZerosTableFunction_ShouldReturnThreeRows` на реальном ClickHouse зелёная (по `WIP_clickhouse_table_functions_remaining.md`; в этом аудите не перезапускалась).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `SupportsTableFunction` — чистая предикатная функция, `SqlSourceRenderer` ресурсов не удерживает; тесты по-прежнему оборачивают контекст в `using var`. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`); соотношение не изменилось. `CS1591` остаётся в `<NoWarn>` 7 библиотечных `.csproj` (Шаг 5) — `<summary>` типа `IZerosRow` и обоих методов добавлены вручную, сборкой не гейтятся (свойство `Value` — без отдельного `<summary>`, как у соседних row-интерфейсов). |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек в проде нет; `SupportsTableFunction` — сопоставление строки шаблоном `is ... or ...`. В тестах `Select(...).ToList()` — пре-существующий стиль. |
| 5. Проектирование | ✅ Оверрайд вырос на два литерала (`ClickHouseDialect.cs:185`); god-классов и длинных списков параметров не создано. `IZerosRow` — 1 свойство. |
| 4. События / 6. Исключения | ✅ Подписок нет; новых `throw`/`catch` нет (тела DSL-методов бросают `NotSupportedException`, как соседние `numbers`/`generate_series`). |
| 7. Хэш-ключи | ✅ План-ключ не менялся: `[SqlTableFunction]` уже входит в `FromExpression`, а имя функции/диалектный гейт — производные рендера и в `QueryPlanEqualityComparer` не входят. |

### ℹ️ Наблюдения (фикс не требуется)

- **Кумулятивный док-пробел (не запах кода).** `ClickHouseFunctions`, свойство `SqlFunctions.ClickHouse` и `<summary>` `SupportsTableFunction` перечисляют `numbers`/`numbers_mt`, но не `zeros`/`zeros_mt` — см. `API-NAMING-REVIEW.md` Z2. Фикс документационный, на запахи не влияет.
- **Юнит-тест не закрепляет «нет обёртки».** `SqlGenerationTests.cs:565,578` проверяют только `Contain("from zeros(@count)")`; отсутствие `WrapTableFunction` для `zeros` (суть решения) держит лишь интеграционный тест — см. `API-NAMING-REVIEW.md` Z3 (аналог TF5).
- **Материализация без обёртки подтверждена.** `WrapTableFunction` оборачивает только `numbers`/`numbers_mt` (`ClickHouseDialect.cs:191-194`), `MakeTypeName` — `typeof(byte) → "UInt8"` (`:364`), чтение через `GetByte`; `zeros` рендерится как `from zeros(@count) as t1` без каста.
- **Двойной `ToString()` из TF-аудита более не воспроизводится.** `SqlSourceRenderer.cs:312-314` теперь сохраняет `var callSql = sqlBuilder.ToString()` один раз и сравнивает `wrappedCall` с `callSql` — рекомендация прежнего ℹ️-наблюдения реализована.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project tests/<p> -c Release --no-build` — clickhouse **96/96**, core **157/157**, postgres **171/171**; в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — 0; `grep -rn "WrapTableFunction" tests/` — пусто (подтверждает наблюдение о тесте); `.editorconfig` глушит 7 правил (S125/S108/CA2254/S3060/S1104/S3604/S2292 — `silent`), 6 из них инертны без `SonarAnalyzer` (пре-существующее, см. «Примечания»).

## 🔎 Точечный аудит — ClickHouse distributed `GLOBAL IN` (20.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (`global_in` ×3, `:146-158`),
`DataContext/Dialect/ISqlDialect.cs:687-691`, `SqlDialectBase.cs:417-418`,
`src/nextorm.clickhouse/ClickHouseDialect.cs:196-197`, `Visitors/NormSqlTranslator.cs:160-222`,
`Visitors/InValuesTranslator.cs:36,86`, `Visitors/CorrelatedQueryExpressionVisitor.cs:116-117,184-186`;
тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:581-609`, `ClickHouseDialectTests.cs:239`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1470-1483`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:189-211`. Build Release — **0/0**;
unit: clickhouse **99/99**, postgres **172/172** (0 failed); контейнерная интеграция
`GlobalIn_Subquery_/Values_ShouldFilter` на реальном ClickHouse — зелёные по `WIP_global_in.md`
(в этом аудите не перезапускалась — `docker`/`podman` CLI недоступны).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: `global_in` — DSL-заглушки `=> default!`, `SupportsGlobalPredicates` — предикат, `TranslateInValues` ресурсов не удерживает (`inBuilder` возвращается в `BuilderPool` в `finally`, `InValuesTranslator.cs:103-106`). |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). Неоправданных — **0/10**; соотношение не изменилось. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек в проде нет; `global ? " global in (" : " in ("` — тернарник, `InValues.TryGetArguments`/`EvaluatePartition` — пре-существующие скаляры на холодном пути сборки плана. |
| 4. События / 6. Исключения | ✅ Подписок нет; новый `throw new NotSupportedException` — гейт capability с сообщением (`NormSqlTranslator.cs:165-166,217-218`), не пустой `catch`; база `SqlDialectBase` не бросает (возвращает `false`). |
| 5. Проектирование | ✅ God-классов и длинных списков параметров не создано; `TranslateInValues` — 5 параметров (порог 6 не превышен). `ISqlDialect` вырос на 1 член (см. ℹ️-наблюдение). |
| 7. Хэш-ключи | 🟡 **Находка 14** — план-ключ не сворачивает value-list `global_in` (ниже). |

### 🟡 Находка 14 — value-list `global_in` не сворачивается в план-ключ (ОТКРЫТА)

**Где:** `src/nextorm.core/Query/InValues.cs:47-48` (`TryGetArguments` матчит только
`DeclaringType == typeof(CommonFunctions)` и имя `@in`),
`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:470-474`
(`InValuesShapeHash`/`HasTopLevelInValues`), `src/nextorm.core/Visitors/InValuesTranslator.cs:45-55`.

**Что происходит.** `InValues.ComputeShapeHash` — общий детектор формы value-list для `@in` и
`Enumerable.Contains` — не распознаёт `ClickHouseFunctions.global_in`. Поэтому:
1. `cmd.InValuesShapeHash` не получает вклад от `global_in`, `HasTopLevelInValues` остаётся `false`;
2. при рендере `TranslateInValues` не находит `InValuesPartitions` и для захваченной коллекции
   (не `NewArrayExpression`) выставляет `command.Cache = false` (`InValuesTranslator.cs:51-52`) —
   план-кэш молча отключается.

Для инлайновой формы (`params T[]`, `new[] { … }`) проблемы нет: `NewArrayExpression` входит в
план-ключ (`ExpressionPlanEqualityComparer.cs:269-270,734-738`). Воспроизведение по аналогии с
`@in`: `tests/nextorm.sqlite.tests/InListCacheTests.cs` фиксирует, что захваченная коллекция
**должна** переиспользовать план; для `global_in` тот же сценарий даст разные экземпляры (кэш
выключен).

**Почему это не (пока) ошибка SQL.** `Cache = false` не даёт положить план в `QueryPlanStore`
(`QueryPlanner.cs:145`) — стухший план с прежним числом `@pN` не будет переиспользован. Но это
тот же класс дефекта, что был у `@in` до сворачивания формы; защита держится только на побочном
эффекте `Cache=false` в рендере. `RefreshInValuesShape` (`QueryPreparer.cs:111-114`) для `global_in`
выходит раньше по `HasTopLevelInValues == false`, поэтому при малейшем изменении порядка/снятии
`Cache=false` вернётся stale-plan с неверным числом параметров.

**Опасный соблазн фикса.** Просто расширить `InValues.TryGetArguments` на
`ClickHouseFunctions.global_in` **нельзя**: `InValuesTranslator.TryTranslateCollectionContains`
(`InValuesTranslator.cs:20-27`) вызывается из `BaseExpressionVisitor.cs:109` **до**
`NormSqlTranslator.TryTranslate` (`:123`) и позовёт `TranslateInValues(..., global = false)` —
получится ` in (` вместо ` global in (` плюс пропуск гейта `SupportsGlobalPredicates`. Проверено
по порядку диспетчеризации.

**Было (текущее):** `global_in(col, captured)` → `Cache=false`, форма коллекции в план-ключ не входит.
**Стало (рекомендация):** научить детектор формы различать `@in` и `global_in`
(например, `TryGetArguments(…, out bool isGlobal)`); `ComputeShapeHash` сворачивает форму для обоих,
а `TryTranslateCollectionContains` при `isGlobal` возвращает `false`, чтобы `global_in` обрабатывался
только `NormSqlTranslator` (с гейтом). Тогда захваченная коллекция вновь кэшируется, как `@in`, а
stale-plan сценарий закрывается `RefreshInValuesShape`.
**Проверка:** после фикса — ClickHouse-аналог `InListCacheTests`: два билда `global_in(col, captured)`
одной формы дают `ReferenceEquals` планов; `dotnet build nextorm.sln -c Release` — 0/0; clickhouse
unit — без регрессий.

### ℹ️ Наблюдения (фикс не требуется)

- **Расширение guard'а `CorrelatedQueryExpressionVisitor` безопасно (ответ на вопрос 1).**
  `PostgresFunctions`/`SqlServerFunctions` **наследуют** `CommonFunctions`
  (`SqlFunctions.Postgres.cs:12`, `SqlFunctions.SqlServer.cs:12`), поэтому унаследованные
  `exists`/`any`/`all`/`@in` уже имели `DeclaringType == typeof(CommonFunctions)` и попадали в ветку
  до изменения; собственные методы провайдеров (`PostgresFunctions.any`/`all` на массивах, `:19-31`)
  объявлены на `PostgresFunctions` и в guard не входят ни до, ни после. `global_in` и остальные члены
  `ClickHouseFunctions` объявлены на самом `ClickHouseFunctions`, поэтому `ClickHouseFunctions` и есть
  единственное практическое добавление. Изменения поведения для `exists`/`any`/`all`/`@in` ни на
  одном провайдере нет.
- **Побочный эффект расширения (низкий риск).** Любой `ClickHouseFunctions`-метод, не совпавший с
  `exists/any/all`/`@in`/`global_in`, теперь возвращается как `node`
  (`CorrelatedQueryExpressionVisitor.cs:224`) вместо `base.VisitMethodCall(node)`, т.е. аргументы
  такого вызова больше не обходятся рекурсивно. Практически не наблюдается: подзапросных аргументов у
  `arg_min`/`json_extract_*`/`dict_get`/TVF нет, а `global_in` покрыт явной веткой. Консистентно с
  обработкой «прочих» `CommonFunctions`-методов (тот же `return node`).
- **Гейт `SupportsGlobalPredicates` есть только в `NormSqlTranslator`.** В
  `CorrelatedQueryExpressionVisitor.cs:184-186` ветка `global_in` его не проверяет, но нештатный
  провайдер всё равно упадёт на этапе перевода (`NormSqlTranslator.cs:165-166,217-218`) — что и
  подтверждает `Postgres…GlobalIn_UnsupportedByProvider_ShouldThrow`. Наблюдение, не дефект:
  валидация сосредоточена в одном слое, но выполняется уже после побочных `PrepareCommand`/
  `AddCommand` внутреннего запроса.
- **`bool global = false` в `TranslateInValues` (ответ на вопрос 2).** Внутренний helper, 5 параметров;
  единственные вызовы — `TryTranslateCollectionContains` (по умолчанию `false`) и `NormSqlTranslator`
  (явно `globalIn`). Дефолт скрывает намерение в первом вызове и создаёт «тихую» развилку (см.
  Находку 14); безопаснее сделать параметр обязательным/`enum InModifier`, но это не дефект. Флаг не
  влияет на param-mode (ранний `return` до рендера текста, `:61-68`) — номера `@pN` и гейт не
  затронуты.
- **Три перегрузки `global_in` согласованы с `CommonFunctions.@in` (ответ на вопрос 3).** Порядок и
  формы идентичны: `(T, QueryCommand<T>)`, `(T, IEnumerable<T>)`, `(T, params T[])`; тот же тип
  возврата `bool`; имена параметров `column`/`cmd`/`values` совпадают; XML-`<summary>` есть у всех трёх
  (у `@in` доков нет вовсе). Snake_case `global_in` — принятое SQL-зеркало (`@in`, `count_big`), не
  новое нарушение.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; clickhouse unit
**99/99**, postgres **172/172**; в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` —
0, соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных
`#pragma`, неоправданных — 0); `grep -n "CommonFunctions.@in" src/nextorm.core/Query/InValues.cs` —
`:48` (единственная точка детекции, `global_in` не входит); `find -name 'PublicAPI*.txt'` — пусто.

## 🔎 Точечный аудит — ClickHouse JSONPath-скаляры `JSON_VALUE`/`JSON_QUERY`/`JSON_EXISTS` (20.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (`json_value`/`json_query`/`json_exists`, `:113-124`),
`DataContext/Dialect/ISqlDialect.cs` (`SupportsJsonPath` `:354-360`), `SqlDialectBase.cs:100-101`,
`src/nextorm.clickhouse/ClickHouseDialect.cs:72-73` + маппинг имён `MakeJsonExtract` `:96-98`,
`Visitors/JsonExtractSqlTranslator.cs` (ветки `:48-56`, `EmitJsonPathFunction` `:101-128`),
`Visitors/TextJsonSqlTranslator.cs:20-21`, `Visitors/JsonSqlTranslator.cs:30-33`; тесты
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:428-444`, `ClickHouseDialectTests.cs:90-97`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1499-1509`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:112-127`. Build Release — **0/0**;
unit: clickhouse **101/101**, postgres **174/174**, sqlserver **177/177**, mysql **44/44**,
mariadb **14/14**, sqlite **207/207**, core **157/157** (0 failed). Контейнерный
`JsonPath_ShouldReadStringJson` в этом проходе не перезапускался (Podman-сокет недоступен); по отчёту
изменения — зелёный.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: три DSL-заглушки `=> default!`, `EmitJsonPathFunction` — строковый рендер без ресурсов; тесты по-прежнему оборачивают контекст в `using var`. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). Неоправданных — **0/10**; соотношение не изменилось. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch` — 0; inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек нет; `EmitJsonPathFunction` — `for`-цикл по `node.Arguments` + `new string[]`, как у соседнего `EmitFunction`. |
| 4. События / 6. Исключения | ✅ Подписок нет; новый `throw new NotSupportedException` — гейт `SupportsJsonPath` (`JsonExtractSqlTranslator.cs:108-110`), не пустой `catch`. |
| 5. Проектирование | 🟡 **Находка 15** — `EmitJsonPathFunction` дублирует `EmitFunction` (ниже). |
| 7. Хэш-ключи | ✅ План-ключ не менялся: новые методы DSL и диалектный гейт — производные рендера, в `QueryPlanEqualityComparer`/`_hashPlan` не входят. |

### 🟡 Находка 15 — `EmitJsonPathFunction` дублирует `EmitFunction` (ОТКРЫТА)

`JsonExtractSqlTranslator.EmitFunction` (`:77-99`) и `EmitJsonPathFunction` (`:106-128`) — почти
построчные копии: различаются только проверяемым флагом (`SupportsJsonExtract` против
`SupportsJsonPath`) и текстом исключения. Повторяются param-mode обход аргументов, `NeedAliasForColumn`,
`new string[args.Count]` + `VisitToString` в цикле и `Dialect.MakeJsonExtract(name, rendered)`
(~20 строк). Это тот же класс дублирования, что зафиксирован в ℹ️-наблюдениях аудитов
`JSONExtract*`/`visitParamExtract*`/UUID (13 почти одинаковых веток и «шестой почти-дубликат
транслятора»), но здесь копия — целое тело метода в одном и том же классе, и её устранение локально.

- **Было:** две независимые реализации; гейт и текст исключения продублированы внутри каждой.
- **Стало:** вынести общее ядро `EmitCore(visitor, node, name)` (param-mode + рендер), а в
  `EmitFunction`/`EmitJsonPathFunction` оставить только проверку флага с профильным
  `NotSupportedException` и вызов ядра. Точечная правка, поведение и сообщения не меняются.
- **Проверка:** build 0/0; `JsonPathFunctions_ShouldUseClickHouseNames`,
  `JsonExtract_ShouldUseClickHouseNames`, `VisitParamExtract_ShouldUseClickHouseNames` зелёные.

### ℹ️ Наблюдения (фикс не требуется)

- **Declaring-type guard'ы не ломают SQL Server/PostgreSQL (ответ на вопрос 1).** `TextJsonSqlTranslator`
  (`:20-21`) и `JsonSqlTranslator` (`:30-33`) теперь матчат по
  `node.Method.DeclaringType == typeof(SqlServerFunctions)` / `typeof(PostgresFunctions)`. Все
  обрабатываемые методы объявлены непосредственно на этих типах (`Query/SqlFunctions.SqlServer.cs:19,25,32,38`,
  `Query/SqlFunctions.Postgres.cs:100-222`); ни один не наследуется от `CommonFunctions` (JSON-методов
  там нет), а унаследованный вызов сохраняет `DeclaringType` базового типа. Провайдерные пути
  (`SqlFunctions.SqlServer.json_value` на SQL Server/MySQL, `SqlFunctions.Postgres.json_exists` на
  PostgreSQL) не изменились — подтверждено зелёными `TextJsonFunctions_ShouldEmitFunctions`
  (`sqlserver:1482-1497`), `TextJsonFunctions_ShouldUseJsonExtractFamily` (`mysql:65-81`),
  `TextJsonFunctions_ShouldThrowBecauseSqliteLacksThem` (`sqlite:1800-1809`),
  `json_exists`/`json_exists_any`/`json_exists_all` (`postgres:939-941`). Guard'ы зеркальны уже
  исправленной Находке 13 (`AdvancedAggregateTranslator`, `DeclaringType == typeof(PostgresFunctions)`).
  Единственное поведенческое следствие: пользовательский `new`-метод с тем же именем на своём
  наследнике больше не перехватывается by-name; это желаемое сужение, а не регресс.
- **Текст исключения точен именно из-за флага.** `EmitJsonPathFunction` сообщает про JSONPath-семейство,
  а `EmitFunction` — про `JSONExtract*`; при отказе от отдельного флага (см. `API-NAMING-REVIEW.md`,
  JP1) профильное сообщение нужно сохранить в ветке JSONPath. Не дефект.
- **Аллокации — на холодном пути.** `new string[args.Count]` + `VisitToString` выполняются при
  построении плана, не при материализации (как у соседнего `EmitFunction`).
- **Кумулятивный док-пробел (не запах кода).** Классовые `<summary>` `JsonExtractSqlTranslator`
  (`:5-13`), `ClickHouseFunctions` (`:5-14`) и свойства `SqlFunctions.ClickHouse`
  (`SqlFunctions.cs:41-51`) JSONPath не упоминают — см. `API-NAMING-REVIEW.md`, JP2/JP3.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet run --project
tests/<p> -c Release --no-build` — clickhouse **101/101**, postgres **174/174**, sqlserver **177/177**,
mysql **44/44**, mariadb **14/14**, sqlite **207/207**, core **157/157**; в изменённых файлах
`#pragma warning disable`/`SuppressMessage`/`NoWarn` — 0, соотношение подавлений проекта не изменилось
(5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — 0); `find -name 'PublicAPI*.txt'`
— пусто.

### 🔎 Точечный аудит — ClickHouse join strictness `ANY`/`ALL`/`ASOF` (20.09.2026)

Область: `JoinStrictness` (`Expressions/JoinExpression.cs:30-40`), `JoinExpression.Strictness`/`CloneForCache`
(`:51,59-66`), `JoinExpressionPlanEqualityComparer` (`:35,55`), `ISqlDialect.SupportsJoinStrictness`/
`MakeJoinKeyword` (`DataContext/Dialect/ISqlDialect.cs:59,65`; база — `SqlDialectBase.cs:26,191-204`),
рендер (`DataContext/SqlSourceRenderer.cs:89-95,106`; вызов — `SqlBuilder.cs:96-103`),
in-memory (`DataContext/InMemoryJoin.cs:20-21`), ClickHouse (`src/nextorm.clickhouse/ClickHouseDialect.cs:25,31-54`),
билдеры (`Builders/EntityBuilder.cs:280-288`; `Builders/Joins/JoinedEntityBuilder.cs:60-64,111-115,157-161,203-207,249-253,295-299,329-333`).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; enum, switch и рендер — чистые значения. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). Неоправданных — **0/10**; соотношение не изменилось. |
| Слоп-паттерны (`slopwatch` локально не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch`/inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек нет; `MakeJoinKeyword` — `switch` по enum. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw new NotSupportedException` — гейты (`SqlSourceRenderer.cs:87,92,94`, `InMemoryJoin.cs:21`, `SqlDialectBase.cs:193`), не пустые `catch`/не `catch (Exception)`. |
| 5. Проектирование | 🔴 **Находка 16** — мутируемый общий `JoinExpression` и 🟡 **Находка 17** — пре-существующий `CloneForCache` (ниже). ✅ **Обе закрыты 20.09.2026** фичей `GLOBAL JOIN` (см. точечный аудит ниже). |
| 7. Хэш-ключи | 🔴 **Находка 16** — `Strictness` участвует в `Equals`/`GetHashCode` план-ключа (`JoinExpressionPlanEqualityComparer.cs:35,55`), но выставляется через публичный мутабельный сеттер. ✅ **Закрыто 20.09.2026**: сеттеры `Strictness`/`IsGlobal` — `internal set` (актуальные строки: `:36,58`). |

### ✅ Находка 16 — `WithStrictness` мутирует общий `JoinExpression` и возвращает `this`, ломая иммутабельность билдера и план-ключ (ИСПРАВЛЕНА 20.09.2026, была P1)

> **Статус 20.09.2026 (фича ClickHouse `GLOBAL JOIN`): исправлена.** Реализована ровно рекомендованная
> copy-on-write-схема: `EntityBuilder<TEntity>.ReplaceLastJoin(JoinStrictness?, bool isGlobal)`
> (`Builders/EntityBuilder.cs:300-326`) делает `Clone()`, копирует список (`[.. _joins]` при `null`,
> иначе свежий список из `CloneImp`) и заменяет **последний** элемент новым `JoinExpression`
> (`:315-321`); `WithStrictness`/`Global` возвращают клон, а не `this` (`:282-283,293`). Сеттеры
> `JoinExpression.Strictness`/`IsGlobal` сужены до `internal set` (`Expressions/JoinExpression.cs:51,58`),
> so in-place-мутация из публичной поверхности невозможна. Синхронизация arity-2 сделана через
> protected-хук `OnLastJoinReplaced` (`EntityBuilder.cs:333-335`; override —
> `JoinedEntityBuilder.cs:66`), как и предлагалось. Историческое описание ниже сохранено.

**Ответ на вопрос 1: да, это реальный aliasing-баг, а не только «пометка на месте».** Заявленное в `WIP_clickhouse_join_strictness.md:47-48` обоснование (замена объекта рассинхронизировала бы `JoinCondition` и `Joins[^1]` в arity 2) верно как ограничение реализации, но закрыто мутацией разделяемого состояния. `EntityBuilder.cs:17-20` объявляет билдер «Fluent, immutable query builder», а все прочие модификаторы (`Final`/`Sample`/`Settings`/`PreWhere`/`WithTotals` — `EntityBuilder.cs:200-273,595-599`) идут через `Clone()`-then-set. `WithStrictness` же делает `_joins[^1].Strictness = strictness` и `return this` (`EntityBuilder.cs:285,287`).

**Почему объект разделяемый.** `JoinExpression`-объекты общие для всех клонов билдера:
- arity 2: `CloneImp` передаёт тот же `JoinCondition` в конструктор (`JoinedEntityBuilder.cs:51-58`), а `CopyTo` `_joins` не копирует вовсе (`EntityBuilder.cs:352-376`) → `r._joins[0]` — тот же объект;
- arity ≥3: `CloneImp` делает поверхностную копию списка `r.Joins = [.. Joins]` (`JoinedEntityBuilder.cs:103-109,149-155,195-201,241-247,287-293`) — ссылки те же.

Следствие: `var q2 = q1.Where(...); q2.WithStrictness(Any);` меняет `q1.Joins[0].Strictness`; и даже без клона `q1.WithStrictness(Any)` возвращает `q1`, поэтому «старый» билдер становится `Any`. Это тихая подмена семантики SQL у запроса, который пользователь уже построил.

**План-ключ.** `Strictness` — публичное свойство с сеттером (`JoinExpression.cs:51`) и участвует в `Equals`/`GetHashCode` (`JoinExpressionPlanEqualityComparer.cs:35,55`). `JoinExpression.CloneForCache` для табличных источников возвращает `this` (`JoinExpression.cs:61-65`; `FromExpression.CloneForCache` тоже `this` — `FromExpression.cs:61-68`), поэтому кэш-план и билдер делят `JoinExpression`; мутация меняет вход уже посчитанного `QueryPlan._hashPlan` (readonly с момента создания) → рассинхрон `GetHashCode`/`Equals`, кэш-миссы и дубли планов. Неверного результата нет (`Equals` перечитывает актуальное значение), но мутабельный ключ кэша — тот же класс дефекта, что Находка 5.

- **Было:** `_joins[^1].Strictness = strictness; return this;` (`EntityBuilder.cs:285-287`) + `public JoinStrictness Strictness { get; set; }` (`JoinExpression.cs:51`).
- **Стало (рекомендация):** добавить `internal JoinExpression WithStrictness(JoinStrictness)`, возвращающий новый `JoinExpression` с теми же `JoinCondition`/`JoinType`/`From`/`EntityType`; в `EntityBuilder.WithStrictness` — `var b = Clone(); b._joins![^1] = b._joins![^1].WithStrictness(strictness);`; в `JoinedEntityBuilder<T1,T2>` синхронизировать `JoinCondition` с новым `_joins[^1]` (protected-хук/override), чтобы не вернуть рассинхрон arity 2. Сеттер `Strictness` сузить до `internal set` (или `init` через фабрику). Тогда модификатор следует паттерну `Final`/`Sample`/`WithTotals`, а план-ключ становится неизменяемым.
- **Проверка:** `dotnet build nextorm.sln -c Release` — **0/0**; существующие тесты `JoinWithStrictness_ShouldRenderClickHouseModifier` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:777`), `JoinStrictness_OnCrossJoin_ShouldThrow` (`:803`), `WithStrictness_WithoutJoin_ShouldThrow` (`:817`), `JoinStrictness_UnsupportedByProvider_ShouldThrow` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:313`), `TestJoinStrictness_ShouldThrow` (`tests/nextorm.core.tests/InMemoryJoinTests.cs:65`) и интеграционные Any/All/Asof (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:433,444,458`) должны остаться зелёными; добавить регресс-тест «соседний/клонированный билдер не меняется».

**Явно не предлагается** откатывать in-place без синхронизации `JoinCondition`: это вернуло бы рассинхрон arity 2.

### ✅ Находка 17 — `JoinExpression.CloneForCache` присваивает исходный `From`, а не клон (ИСПРАВЛЕНА 20.09.2026, была P2, пре-существующая)

> **Статус 20.09.2026: исправлена.** `Expressions/JoinExpression.cs:72` теперь присваивает
> `From = newFrom!` (ветка `if (newFrom == From) return this;` сохранена), поэтому кэш-план больше не
> делит `From.SubQuery` с вызывающим для подзапросного join'а. В ту же инициализацию добавлен
> `IsGlobal = IsGlobal` (перенос нового флага). Историческое описание ниже сохранено.

`JoinExpression.cs:61-65`: `var newFrom = From.CloneForCache(); if (newFrom == From) return this; return new JoinExpression(...) { From = From, ... }`. `newFrom` вычисляется, но в инициализатор попадает исходный `From`. Для подзапросного join это значит, что кэш-план делит `From.SubQuery` с билдером и `CloneForCache` не отвязывает план от вызывающего (для табличных источников обе ветки — `this`, поэтому дефект там невидим). Фича лишь добавила `Strictness = Strictness` в тот же инициализатор (`:65`) и дефект **не внесла**; метод ею тронут, поэтому фиксируется отдельно. **Стало:** `From = newFrom!`. **Проверка:** build 0/0; тесты подзапросных join без изменений.

### ℹ️ Наблюдения (фикс не требуется)

- **7 ковариантных `new WithStrictness` — допустимо (вопрос 2).** Файл `JoinedEntityBuilder.cs` уже дублирован по арности целиком (`new Join`/`LeftJoin`/… и `new Clone`); `new`-перегрузка нужна, чтобы плоская цепочка `.Join().WithStrictness().Join()` сохраняла конкретный arity (C# не даёт ковариантного возврата иначе). Общий базовый метод вернул бы `EntityBuilder<Projection<…>>` и сломал бы chaining. Не находка.
- **Двойная валидация — допустимо (вопрос 3).** Гейт рендера (`SqlSourceRenderer.cs:89-95`) и `throw` базового `MakeJoinKeyword` (`SqlDialectBase.cs:191-194`) — defense-in-depth в том же стиле, что `SupportsLimitBy`/`MakeLimitBy`. Гейт рендера — авторитетный (выполняется и в param-mode); `throw` в базе на практике недостижим. Ветки ClickHouse `Cross`/`FullCross` со strictness (`ClickHouseDialect.cs:39-40`) недостижимы из-за гейта `:93` — безвредны.
- **Param-mode и план-кэш — согласованы (вопрос 5).** `MakeJoin` проверяет strictness **до** разветвления по `ParamMode` (`SqlSourceRenderer.cs:89-95`), а сам keyword (не несущий параметров) пропускается только в SQL-режиме (`:104-107`); вызывается из обоих проходов (`SqlBuilder.cs:100`). `Strictness` входит в `Equals`/`GetHashCode` плана и переносится `CloneForCache` (`JoinExpression.cs:65`). Пропущенных путей нет. Единственная асимметрия: in-memory бросает при перечислении (`InMemoryJoin.cs:20-21`), а не при построении плана — консистентно с тем, что in-memory вообще не строит SQL-план.
- **Публичный API — аддитивен, переименований нет** (см. `API-NAMING-REVIEW.md`, раздел JS).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе); в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `find -name 'PublicAPI*.txt'` — пусто. Тесты в этом проходе не перезапускались (только build); результаты из `WIP_clickhouse_join_strictness.md:77-79` относятся к прогону автора фичи.

### 🔎 Точечный аудит — ClickHouse `GLOBAL JOIN` (20.09.2026)

Область: `JoinExpression.IsGlobal` (`Expressions/JoinExpression.cs:52-58,72`),
`JoinExpressionPlanEqualityComparer` (`:36,58`), `ISqlDialect.SupportsGlobalJoin`/`MakeJoinKeyword`
(`DataContext/Dialect/ISqlDialect.cs:60-71`; база — `SqlDialectBase.cs:27,192-210`), рендер
(`DataContext/SqlSourceRenderer.cs:89-97,108`), in-memory (`DataContext/InMemoryJoin.cs:23-24`),
ClickHouse (`src/nextorm.clickhouse/ClickHouseDialect.cs:27-28,30-57`), билдеры
(`Builders/EntityBuilder.cs:282-326`; `Builders/Joins/JoinedEntityBuilder.cs:60-66,113-117,159-163,205-209,251-255,297-301,331-335`).
Build Release — **0 warnings / 0 errors** (прогнано в этом проходе).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; флаг, switch и рендер — чистые значения. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`). Неоправданных — **0/10**; соотношение не изменилось. |
| Слоп-паттерны (`slopwatch` локально не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch`/inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек нет; `MakeJoinKeyword`/`ReplaceLastJoin` — `switch`/массивы. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw` — гейты (`SqlSourceRenderer.cs:93-96`, `InMemoryJoin.cs:24`, `SqlDialectBase.cs:194-198`, `EntityBuilder.cs:303`), не пустые `catch`/не `catch (Exception)`. |
| 5. Проектирование | ✅ **Находка 16 закрыта** (copy-on-write `ReplaceLastJoin`); `new JoinExpression { ... }` читаема, `ReplaceLastJoin` — 27 строк. |
| 7. Хэш-ключи | ✅ `IsGlobal` участвует в `Equals`/`GetHashCode` план-ключа (`JoinExpressionPlanEqualityComparer.cs:36,58`) и переносится `CloneForCache` (`JoinExpression.cs:72`); сеттеры `Strictness`/`IsGlobal` — `internal set`. |

**Вопрос 1 — композиция `Global()`/`WithStrictness(...)` подтверждена корректной.** `ReplaceLastJoin` переносит оба флага независимо от порядка: `Strictness = strictness ?? last.Strictness` (`EntityBuilder.cs:319`) и `IsGlobal = isGlobal || last.IsGlobal` (`:320`). `WithStrictness(s)` вызывает `ReplaceLastJoin(s)` с `isGlobal` по умолчанию `false`, но выражение `false || last.IsGlobal` не может сбросить уже установленный global; `Global()` вызывает `ReplaceLastJoin(null, isGlobal: true)`, и `null ?? last.Strictness` сохраняет strictness. Оба порядка дают `global <type> <strictness> join`.

**Вопрос 2 — расширение `MakeJoinKeyword` допустимо.** Метод введён непосредственно предыдущей (ещё не выпущенной) фичей join-strictness; внешних реализаторов `ISqlDialect` в репозитории нет (только `SqlDialectBase`). Оба модификатора рендерятся в перемежающемся порядке (`global left any join`), поэтому единый хук связнее отдельного `MakeGlobalJoinKeyword`; отдельная функция потребовала бы дублировать разбор `JoinType`/`Strictness`. Детали — в `API-NAMING-REVIEW.md`, раздел «ClickHouse GLOBAL JOIN (точечный аудит 20.09.2026)», GG2.

**Вопрос 4 — пропущенных plan-cache/param-mode путей нет.** `IsGlobal` входит в `Equals`/`GetHashCode` (`JoinExpressionPlanEqualityComparer.cs:36,58`) и копируется `CloneForCache` (`JoinExpression.cs:72`, обе ветки); `QueryCommand.CloneForCache` (`Query/QueryCommand.Clone.cs:105-119`) и `QueryCommand<TResult>` (`:275`) идут через общий helper. Гейт в `MakeJoin` стоит **до** разветвления по `ParamMode` (`SqlSourceRenderer.cs:89-97`), а keyword (без параметров) эмитится только в SQL-режиме (`:106-108`). `CloneForCache` для табличных источников по-прежнему возвращает `this` (`JoinExpression.cs:70`) — это безопасно ровно потому, что in-place-мутации больше нет: модификатор заменяет объект в списке билдера, не трогая объект, который мог попасть в кэш-план.

**Вопрос 5 (Находка 16) — in-place-мутация не вернулась.** `Clone()` для `EntityBuilder<TEntity>` не копирует `_joins` (`CopyTo` — `EntityBuilder.cs:399-423`), поэтому `ReplaceLastJoin` берёт ветку `joins = [.. _joins]` (`:307-312`); для `JoinedEntityBuilder<T1,T2>` конструктор клона создаёт свежий список `[JoinCondition]` (`JoinedEntityBuilder.cs:16-20,51-58`); для arity ≥3 `CloneImp` всегда делает `r.Joins = [.. Joins]` (`:109,155,201,247,293,327`). Ни в одной ветке `b._joins` не разделяется с источником, поэтому `joins[^1] = new JoinExpression(...)` (`EntityBuilder.cs:315`) не может изменить исходный билдер (подтверждено `WithStrictness_ShouldNotMutateSourceBuilder` — `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:828-842` и `GlobalJoin_ShouldNotMutateSourceBuilder` — `:878-891`).

### ℹ️ Находка 18 — неполное покрытие композиции `GLOBAL` (ℹ️, тесты; исправить не обязательно)

`WIP_global_join.md:43-44` и XML-док `Global()` заявляют комбинируемость в любом порядке, но покрыт только один порядок: `GlobalJoin_ShouldRenderGlobalModifier` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:859-875`) проверяет `.Global().WithStrictness(Any)` → `global any join`; обратный порядок `.WithStrictness(Any).Global()` и «`WithStrictness` после `Global` не сбрасывает global» отдельным тестом не зафиксированы (сам код корректен — см. вопрос 1). Также нет `Global_WithoutJoin_ShouldThrow` (аналога `WithStrictness_WithoutJoin_ShouldThrow` — `:817-825`) и нет плоской цепочки, упражняющей ковариантный `new Global()` (аналога `WithStrictness_ThenJoin_ShouldKeepStrictnessOnFirstJoin` — `:844-856`).

**Было/Стало:** код менять не нужно; **Стало** — добавить 2-3 регресс-теста (обратный порядок, `Global()` без join, flat-chain arity). Рекомендация для `nextorm-design-engineer`; не блокирует, поэтому находка помечена ℹ️.

### ℹ️ Наблюдения (фикс не требуется)

- **`Strictness`/`IsGlobal` можно сузить до `internal init`.** Оба свойства выставляются только в объектном инициализаторе `new JoinExpression { ... }` (`JoinExpression.cs:72`, `EntityBuilder.cs:315-321`), поэтому `init` вместо `internal set` дополнительно запретил бы любую будущую in-place-мутацию и закрепил инвариант Находки 16. Косметическое упрочнение, не дефект.
- **`Global()`/`IsGlobal`/`SupportsGlobalJoin` — нейминг конвенциям соответствует.** `Is*` для bool, `Supports*` — как `SupportsGlobalPredicates` (GLOBAL IN) и `SupportsJoinStrictness`; `Global()` — терсный модификатор в одном ряду с `Distinct()`/`Final()`/`Sample()`. P0/P1 нет (см. `API-NAMING-REVIEW.md`, раздел «ClickHouse GLOBAL JOIN (точечный аудит 20.09.2026)»).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе); в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `find -name 'PublicAPI*.txt'` — пусто (Шаг 5 открыт); XML-`<summary>` есть у `IsGlobal`, `SupportsGlobalJoin`, `Global()` (8 перегрузок: базовая + 7 `<inheritdoc/>`) и у обеих реализаций `MakeJoinKeyword`. Тесты в этом проходе не перезапускались (только build); ClickHouse unit/integration — по `WIP_global_join.md:53-60`.

## 🔎 Точечный аудит — ClickHouse массивы и `arrayJoin` (20.09.2026)

Область: `Query/SqlFunctions.ClickHouse.cs` (`array_join`/`length`/`has`/`index_of`/`has_any`/`has_all`/
`array_string_concat`/`split_by_char`/`array_sort`/`array_reverse`/`array_distinct`, `:215-253`),
`DataContext/Dialect/ISqlDialect.cs` (`SupportsArrayFunctions` `:136-143`, `SupportsArrayJoin` `:144-149`,
`MakeArrayFunction` `:682-689`), `DataContext/Dialect/SqlDialectBase.cs:32-34,421-423`,
`src/nextorm.clickhouse/ClickHouseDialect.cs:30-45`, `Visitors/ArraySqlTranslator.cs`
(`TryTranslateClickHouseArray` `:174-226`, `EmitArrayFunction` `:228-257`, гейты `:259-271`),
`Visitors/SqlOperandTranslator.cs` (`AppendArrayOrColumn`/`IsCapturedArray` `:143-188`). Тесты:
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:933-1038`, `ClickHouseDialectTests.cs:42-43,261`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:2251-2262`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:487,504,516`. Build Release — **0 warnings / 0 errors**
(прогнано в этом проходе); unit: clickhouse **122/122**, postgres **177/177** (0 failed). Контейнерные
интеграционные тесты в этом проходе не перезапускались (Podman-сокет недоступен) — по отчёту автора зелёные.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: `ArraySqlTranslator`/`SqlOperandTranslator` — статические хелперы, `EmitArrayFunction` пишет в builder визитора (возвращается в пул через `BaseExpressionVisitor.Dispose`, `:355-366`). |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`), `<NoWarn>CS1591` — 7 `.csproj` (принято, Шаг 5 открыт). Неоправданных — **0/10**; соотношение не изменилось. |
| Слоп-паттерны (`slopwatch` локально не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch`/inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек нет; `EmitArrayFunction` — `for`-цикл по `IReadOnlyList<Expression>`. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw new NotSupportedException` — гейты `RequireArrayFunctions`/`RequireArrayJoin` (`ArraySqlTranslator.cs:259-271`), не пустые `catch`/не `catch (Exception)`. |
| 5. Проектирование | 🟡 **Находка 19** — default-разделитель `array_string_concat` (ниже); ℹ️ док-дрейф классовых `<summary>` (см. `API-NAMING-REVIEW.md`, раздел «ClickHouse массивы и `arrayJoin`», AR3). |
| 7. Хэш-ключи | ✅ Новых членов план-ключа нет: методы DSL различаются `MethodInfo` в дереве выражения; захваченный массив биндится параметром, а `QueryPlanner.ExtractParams` освежает его значение на кэш-хите (`QueryPlanner.cs:170-180`) — имя не `norm_pN`, поэтому `NeedsParamRefresh` истинно. |

### 🟡 Находка 19 — `array_string_concat` при default-разделителе рендерит SQL `null` вместо пустого (ОТКРЫТА; P1-кандидат, рантайм-эффект требует контейнера)

`ClickHouseFunctions.array_string_concat<T>(T[] array, string? delimiter = null)`
(`Query/SqlFunctions.ClickHouse.cs:240-241`) объявлен с XML-доком «default separator is the empty string».
Дерево выражения всегда содержит второй аргумент: компилятор подставляет default как
`ConstantExpression(null)` (C# не опускает optional-аргументы в expression tree). `TryTranslateClickHouseArray`
требует `args.Count == 2` и передаёт **оба** аргумента в `EmitArrayFunction`
(`Visitors/ArraySqlTranslator.cs:200-201,228-257`). Не-массивный `null` идёт по
`SqlOperandTranslator.AppendArgument` → `VisitToString` → `BaseExpressionVisitor.VisitConstant`/`TryEmitValue`
(`BaseExpressionVisitor.cs:283-316`; ветка `if (v is null) _builder!.Append("null")`). Итог: одноаргументный
вызов `SqlFunctions.ClickHouse.array_string_concat(x.Tags)` рендерит `arrayStringConcat(tags, null)`, а не
`arrayStringConcat(tags)`/`arrayStringConcat(tags, '')`. Явный разделитель покрыт
(`SqlGenerationTests.cs:979-987`), default-путь — нет (ни unit, ни integration).

- **Было:** `arrayStringConcat(col, null)`.
- **Стало (рекомендация):** в `EmitArrayFunction`/`TryTranslateClickHouseArray` при `args[1]` =
  `ConstantExpression { Value: null }` для `array_string_concat` опускать второй аргумент (рендерить
  `arrayStringConcat(col)`) либо рендерить `''`; добавить регресс-тест на одноаргументный вызов.
- **Проверка:** build 0/0; существующий `ArrayStringConcat_ShouldRenderArrayStringConcat` зелёный. Точное
  рантайм-поведение ClickHouse при `null`-разделителе в этом проходе не подтверждено (контейнера нет):
  по общему правилу ClickHouse функции с NULL-аргументом возвращают NULL, что и мотивирует правку; перед
  фиксом сверить в контейнере.

### ℹ️ Наблюдения (фикс не требуется)

- **Вопрос 1 — `IsCapturedArray` корректен для поддержанных форм.** `ConstantExpression`/`NewArrayExpression`
  → захвачено; `SqlFunctions.Parameter<T>` → нет; член-цепочка с корнем `ConstantExpression` (поле/local
  замыкания, property замыкания замыкания) или `null` (статическое поле) → захвачено; корень
  `ParameterExpression` (колонка сущности) → нет (`SqlOperandTranslator.cs:167-188`). `Convert` снимается
  `UnwrapConvert` (`:231-234`), поэтому бокс колонки не превращает её в параметр. Колонку как параметр не
  биндит, захваченный local как SQL не рендерит. Захваченный массив, полученный **вызовом метода**
  (`list.ToArray()`), классифицируется как «не захваченный», но попадает в общий fold-путь
  `EmitFoldedParameter` (`BaseExpressionVisitor.cs:196-221`) и всё равно биндится одним параметром; в
  param-режиме — тем же путём. Остаточный зазор — формы с корнем `IndexExpression`/`BinaryExpression`
  (например `matrix[0]`) или вызов с `ParameterExpression`; там возможна деградация до `base.VisitMethodCall`,
  но это не типовой сценарий. Фикс не обязателен. Проверено: `toInt64(length(tags))` (`:939`),
  `@p0`/`@p1` для inline `new[]` (`:969-977`), `@p0` для захваченного local (`:1028-1038`).
- **Вопрос 2 — SQL- и param-проходы согласованы.** Оба режима обходят `args` в одном порядке через
  `AppendArrayOrColumn` (`ArraySqlTranslator.cs:228-257`); ветка «не захвачено» в SQL-режиме вызывает
  `VisitToString` на **клоне**, но клон делит `_params` с родителем (`BaseExpressionVisitor.cs:73,242-247`),
  поэтому вложенные параметры попадают в тот же список в том же порядке. Захваченный массив добавляется в
  `Params` в обоих проходах через `AppendArrayOperand` (`SqlOperandTranslator.cs:46-74`). Кэш-хит обновляет
  значения через `ExtractParams` (`QueryPlanner.cs:170-180`, `Debug.Assert` на совпадение имён). Покрыто
  `ArrayFunction_WithCapturedArray_ShouldBindSingleParameter` (`SqlGenerationTests.cs:1027-1038`).
- **Вопрос 3 — перезапись builder'а безопасна.** `EmitArrayFunction` фиксирует `start = builder.Length` до
  `sqlName(`, копирует подстроку `builder.ToString(start, …)`, затем `builder.Length = start` и добавляет
  `MakeArrayFunction(...)` (`ArraySqlTranslator.cs:240-256`). Вложенные аргументы рендерятся в **отдельном**
  builder'е (`VisitToString` → `Clone()` → новый `_sbPool.Get()`, `:348-353`), а пул
  (`StringBuilderPool.Shared`; `StringBuilderPooledObjectPolicy`) очищает буфер при возврате. Потери
  вложенного append'а и порчи пула нет.
- **Вопрос 4 — нейминг и default interface method.** `MakeArrayFunction(string name, string call)` согласован
  с `MakeJsonExtract`/`MakeDictionaryFunction`; identity-DIM безопасен для внешних реализаторов и повторяет
  `MakeFunction`/`MakeTextJsonFunction`, но отличается от пары `abstract`+`SqlDialectBase.virtual`, которой
  следуют два новых флага и соседний `MakeJsonExtract` — принято (alpha; см. `API-NAMING-REVIEW.md`, AR4).
  XML-`<summary>` есть у обоих флагов и у `MakeArrayFunction` (интерфейс/база/ClickHouse).
- **Вопрос 5 — дублирование PG-поверхности оправдано.** `ClickHouseFunctions` не наследует
  `PostgresFunctions`; нативные имена/семантика различаются (`length` vs `cardinality`, `has` vs `@>`,
  `indexOf` vs `array_position`, `arrayStringConcat` vs `array_to_string`, `arrayJoin` без PG-аналога), а
  CH-поверхность работает по array-колонкам, PG — по параметрам. Правило «≥2 провайдера выражают одно и то
  же → `CommonFunctions`» применимо только к идентичной C#-поверхности; промоушен не требуется (ср.
  `any_agg`/session-info/uuid/iif, где поверхности совпадали). Разбор — `API-NAMING-REVIEW.md`, AR5.
- **Вопрос 6 — пропущенных путей нет.** Вложенная array-функция как операнд работает
  (`length(arraySort(nums))` — `SqlGenerationTests.cs:999-1015`); в param-режиме она обходится тем же путём,
  параметры совпадают. Гейт `arrayJoin` отделён от `SupportsArrayFunctions` и наследуется верно. Ночных
  plan-cache/param-путей не найдено.
- **Кумулятивный док-дрейф (не запах кода).** Классовые `<summary>` `ArraySqlTranslator`
  (`Visitors/ArraySqlTranslator.cs:5-18`, всё ещё «PostgreSQL/`SupportsArrays`/`CommonFunctions`») и
  `SqlOperandTranslator` (`:5-14`) новую CH-ветку не упоминают; публичные `ClickHouseFunctions`
  (`Query/SqlFunctions.ClickHouse.cs:5-14`) и `SqlFunctions.ClickHouse` (`SqlFunctions.cs:41-51`) не
  перечисляют array-семейство. См. `API-NAMING-REVIEW.md`, AR2/AR3.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **122/122**;
`tests/nextorm.postgres.tests` — **177/177** (0 failed); в изменённых файлах
`#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение подавлений проекта не изменилось
(5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `find -name 'PublicAPI*.txt'`
— пусто (Шаг 5 открыт); XML-`<summary>` есть у 11 новых методов `ClickHouseFunctions`, обоих флагов и
`MakeArrayFunction`.

## 🔎 Точечный аудит — ClickHouse `ARRAY JOIN` (клауза уровня `FROM`) (20.09.2026)

Область: `Expressions/ArrayJoinKind.cs:8-13`; `Builders/EntityBuilder.cs` (`_arrayJoins` `:31`,
`_arrayJoinKind` `:32`, `ArrayJoins` `:82`, `ArrayJoinKind` `:84`, `ArrayJoin` `:291-292`,
`LeftArrayJoin` `:299-300`, `AddArrayJoin` `:302-320`, `CopyTo` `:468-469`, проброс `:118-119,160-161,616,647`);
`Builders/Joins/JoinedEntityBuilder.cs` (14 `new`-перегрузок `:65-68,122-125,172-175,222-225,272-275,322-325,360-363`;
проброс `:40,104,154,204,254,304`); `Query/QueryDefinition.cs:58-60`; `Query/QueryCommand.cs`
(`:26-27,113-114,139-140,201-203,285`); `Query/QueryCommand.QueryPreparer.cs:47,505-517`;
`Query/QueryCommand.Clone.cs:55-57`; `Query/QueryPlanEqualityComparer.cs:69-71,159-171,311-317`;
`DataContext/Dialect/ISqlDialect.cs:777-787`; `DataContext/Dialect/SqlDialectBase.cs:153-161`;
`src/nextorm.clickhouse/ClickHouseDialect.cs:270-278`; `DataContext/SqlBuilder.cs:105-120`;
`DataContext/SqlSourceRenderer.cs:364-372`; `DataContext/InMemoryQueryBuilder.cs:98-99`. Тесты:
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1050-1113`, `ClickHouseDialectTests.cs:43`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:2265-2270`, `tests/nextorm.core.tests/InMemoryJoinTests.cs:209-213`,
`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:534-556`. Build Release — **0 warnings / 0 errors**
(прогнано в этом проходе); unit: clickhouse **129/129**, core **160/160** (0 failed). Контейнерные
интеграционные тесты в этом проходе не перезапускались (реальный ClickHouse не поднимался) — по отчёту автора зелёные.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: `ArrayJoinKind` — enum, `_preparedArrayJoin` — `Expression[]`, визитор в `MakeArrayJoin` освобождается через `using var`. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`, `<Pending>` — 0), `#pragma disable` — **5** (все с парным `restore`), `<NoWarn>CS1591` — 7 `.csproj` (принято, Шаг 5 открыт). Неоправданных — **0/10**; соотношение не изменилось. |
| Слоп-паттерны (`slopwatch` локально не установлен, скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — **2**, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:80,369`); `Thread.Sleep`/пустых `catch`/inline `Version`/`VersionOverride` — 0. |
| 3. LINQ | ✅ Новых LINQ-цепочек на горячем пути нет: `AddArrayJoin` копирует `List<LambdaExpression>` конструктором, рендер — `for` по `IReadOnlyList<Expression>`. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw` — валидация (`ArgumentException`/`InvalidOperationException` в `AddArrayJoin` `:307,310`), гейты (`SqlBuilder.cs:108`, `InMemoryQueryBuilder.cs:99`, `SqlDialectBase.cs:161`), не пустые `catch`/не `catch (Exception)`. |
| 5. Проектирование | ℹ️ `QueryCommand.ArrayJoinExpressions` публично раскрывает внутренний `Expression[]` (см. `API-NAMING-REVIEW.md`, AJ2); ℹ️ ниже — `CopyTo` делит список, `ArrayJoinKind` без `None`, слабая проверка `IEnumerable`. Функциональных дефектов не найдено. |
| 7. Хэш-ключи | ✅ План-ключ корректен (разбор ниже): `ArrayJoinKind` — в `Equals` (`:69`) и `GetHashCode` (`:311`); подготовленные выражения — `:71`/`:313-317`; `CopyTo` переносит оба (`Clone.cs:55-57`); `Definition` — оба (`QueryCommand.cs:139-140`). Мёртвого `ArrayJoinPlanHash` нет — хэш считается в компараторе. |

### ℹ️ Вопрос 1 — `PrepareArrayJoin` игнорирует `dontCalculateHash`: не баг

`QueryPreparer.PrepareArrayJoin` (`QueryCommand.QueryPreparer.cs:505-517`) принимает только `(cmd, cancellationToken)` и не гейтит подготовку по `noHash`, в отличие от `PreparePreWhere` (`:482-503`). Это не дефект: метод **не считает никакого хэша** — в `QueryCommand` нет `ArrayJoinPlanHash`, а план-ключ читает `_preparedArrayJoin` напрямую в `QueryPlanEqualityComparer` (`:71,311-317`). `PreparePreWhere` же складывает `PreWhereShapeHash` и потому обязан пропускать эту часть при `noHash`. Подготовка обязана выполняться всегда (нужна рендеру) и выполняется даже при `dontCalculateHash=true` (команда без кэша). Асимметрия — стилевая, не дефект.

### ℹ️ Вопрос 1/4 — план-ключ: захваченные значения освежаются, потери нет

`ArrayJoinKind` сравнивается (`:69`) и хэшируется (`:311`) безусловно (у команды без клаузы — `Inner`/0 с обеих сторон, ложных промахов нет). Список подготовленных выражений сравнивается `ExpressionListsEqual` (`:159-171`, `_expComparer`, по индексу) и хэшируется по каждому элементу (`:313-317`) — корректно. `CopyTo` (`Clone.cs:55-57`) и `QueryCommand.Definition` (`:139-140`) переносят `_arrayJoins`/`ArrayJoinKind`/`_preparedArrayJoin`, поэтому `QueryPlan.GetCacheVersion` (`Debug.Assert`) сходится. Захваченный массив в выражении клаузы биндится **одним** параметром (как `has_any(tags, @p0)`), а на кэш-хите его значение освежается `QueryPlanner.ExtractParams` (`QueryPlanner.cs:170-180`); формы, зависящей от длины коллекции (как `InValues` у `WHERE`), здесь нет, поэтому `RefreshInValuesShape` не нужен. `ResultPlanHash` (`QueryCommand.TResult.cs:20`) — только тип результата, к клаузе отношения не имеет.

### ℹ️ Вопрос 3 — param-mode согласован

`SqlSourceRenderer.MakeArrayJoin` (`:364-372`) создаёт визитор и обходит выражение **до** раннего выхода `if (ctx.ParamMode) return string.Empty;`, а `SqlBuilder.MakeSelect` вызывает его в обоих проходах в одном порядке (`:112-116`); в SQL-режиме результат только дописывается в `renderedArrayJoins`. Число/порядок параметров совпадают; отдельных тестов на параметр внутри `ARRAY JOIN` нет — зазор тестов, не дефект.

### ℹ️ Вопрос 2 — утечки мутации между клонами нет

`EntityBuilder.CopyTo` (`Builders/EntityBuilder.cs:468`) присваивает `dst._arrayJoins = _arrayJoins` (делит тот же `List`), в отличие от `SettingsList` (`:466`), который копирует. Это **безопасно**: единственный мутатор, `AddArrayJoin` (`:314-316`), всегда создаёт новый `List`, а внутренний сеттер `ArrayJoins` (`:82`) копирует `[.. value]` — согласованный copy-on-write, как у `Settings`/`PreWhere`. Латентный риск при будущей in-place мутации списка — стоит копировать и здесь.

### ℹ️ Вопрос 6 — порядок клаузы после `JOIN` допустим ClickHouse

Клауза рендерится после `JOIN` и перед `PREWHERE`/`WHERE` (`SqlBuilder.cs:96-128`), `FINAL`/`SAMPLE` — сразу за `FROM` (`:79-93`). Грамматика `SELECT` перечисляет `ARRAY JOIN` до `JOIN`, но парсер ClickHouse принимает оба порядка: в `ParserTablesInSelectQueryElement::parseImpl` (`src/Parsers/ParserTablesInSelectQuery.cpp`) для каждого не-первого элемента сначала пробуется `ParserArrayJoin`, и только затем `JOIN`; тест `tests/queries/0_stateless/00855_join_with_array_join.sql` содержит обе формы (`... ARRAY JOIN ax JOIN ...` и `... JOIN ... ARRAY JOIN ax`). Порядок `FINAL`/`SAMPLE` (сразу после `table [AS alias]`, до `JOIN`) совпадает с `ParserTableExpression`. **Семантическое следствие:** при `ARRAY JOIN` после `JOIN` размножение массива идёт поверх результата join'а, поэтому клауза может ссылаться на обе стороны (`p.Item1`/`p.Item2`) и результат зависит от кратности join'а; идиоматическая форма `ARRAY JOIN … JOIN … ON <элемент>` невыразима — что согласуется с осознанным решением «элемент не привязан к CLR-члену».

### ℹ️ Вопрос 7 — пропущенных путей нет

`Select`/`ToCommand` (`EntityBuilder.cs:118-119,160-161`), `Definition`-снимок (`QueryCommand.cs:139-140`, через него `CreateSelf`/`CreateSelfForClone`/`OrderBy`/`ForLast`/`CloneForCache`) и `CopyTo` (`Clone.cs:55-57`) переносят клаузу; подзапрос (`From.SubQuery`), CTE (`PrepareCtes`/`MakeWithClause`) и `UNION` рендерятся своими командами через `MakeSelect`; `Any` оборачивает array-join-команду как referenced query (`EntityBuilderExtensions.GetAnyCommand`, `:247-275`), не теряя её. `EntityBuilder`/`TableAlias`-ветка (`:868-889`) клаузу не пробрасывает, но публичного `ArrayJoin` в ней нет.

### ℹ️ Наблюдения (фикс не требуется)

- **`ArrayJoinKind` без `None`.** Enum `{ Inner = 0, Left = 1 }` повторяет ClickHouse (`ASTArrayJoin::Kind::Inner/Left`), но у команды без клаузы `ArrayJoinKind` всё равно `Inner`; отличать отсутствие клаузы приходится по `ArrayJoinExpressions is null`. Добавление `None` возможно (alpha, поверхность не заморожена), но сменит default-значение — решение автора.
- **Слабая проверка формы.** `AddArrayJoin` (`:306`) отбрасывает только `string` и требует `IEnumerable`; `Dictionary<,>`/`byte[]` пройдут валидацию и упадут на стороне ClickHouse. Проверка best-effort — приемлемо.
- **`dontNeedAlias: false` в `MakeArrayJoin`** (`SqlSourceRenderer.cs:366`) не покрыт тестом на переименованное свойство (`MyTags` → `tags`); возможен рендер `array join tags as MyTags` — валидный ClickHouse (`ARRAY JOIN arr AS a`), но регресс-тест отсутствует.
- **`ArrayJoinExpressions` раскрывает подготовленный массив** — публичная поверхность, см. `API-NAMING-REVIEW.md`, AJ2; 14 `new`-перегрузок `ArrayJoin`/`LeftArrayJoin` без `/// <inheritdoc/>` — AJ3; публичные доки EN/RU новую клаузу не описывают — AJ5.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
`dotnet run --project tests/nextorm.clickhouse.tests -c Release --no-build` — **129/129**; `tests/nextorm.core.tests` — **160/160** (0 failed); в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `find -name 'PublicAPI*.txt'` — пусто (Шаг 5 открыт). План-ключ проверен чтением `QueryPlanEqualityComparer`/`Clone`/`Definition`; порядок клаузы — по исходнику парсера ClickHouse и тесту `00855_join_with_array_join.sql`. Реальный ClickHouse в этом проходе не перезапускался.

## 🔎 Точечный аудит — ClickHouse `ARRAY JOIN`: привязка элемента (`ArrayJoinElement`, вариант A1) (20.09.2026)

Область: `Expressions/ArrayJoinProjection.cs:9-42`; `Builders/EntityBuilder.cs` (`_sourceEntityType` `:33`,
`_bindArrayJoinElement` `:34`, `SourceEntityType` `:96-102`, `BindArrayJoinElement` `:103-104`, `ArrayJoin` `:291`,
`LeftArrayJoin` `:299`, `AddArrayJoin` `:302-334`, `ArrayJoinElement` `:336-344`, `LeftArrayJoinElement` `:346-355`,
`ToArrayJoinElement` `:357-393`, `CopyTo` `:520-529`, `CopyProjectionIndependentStateTo` `:530-556`, `Select` `:116`,
`ToCommand` `:159`, `EnsureInMemory` `:653-659`); `Query/QueryDefinition.cs:13-16,63,65`; `Query/QueryCommand.cs:115,142,209-211`;
`Query/QueryCommand.Clone.cs:57`; `Query/QueryPlanEqualityComparer.cs:71,315`; `Visitors/MemberTranslator.cs:94-109,410`;
`DataContext/SqlBuilder.cs:105-127`; `DataContext/InMemoryQueryBuilder.cs:98-99`. Тесты:
`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1115-1182`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:2276-2281`,
`tests/nextorm.core.tests/InMemoryJoinTests.cs:219-223`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:558-595`.
Build Release — **0 warnings / 0 errors**; unit: core **161/161**, clickhouse **135/135**, postgres **179/179** (0 failed).
Контейнерные интеграционные тесты в этом проходе не перезапускались (по отчёту автора — 3/3 зелёные).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: `ArrayJoinProjection<,>` — POCO, `ElementAlias` — `const string`, визитор `MemberTranslator` создаётся вызывающим кодом. |
| 2. Подавления | ✅ В изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`), `#pragma disable` — **5** (все с парным `restore`), `<NoWarn>CS1591` — **7** `.csproj` (принято, Шаг 5 открыт). Неоправданных — **0/10**; соотношение не изменилось. |
| 3. LINQ | ✅ Новых LINQ-цепочек на горячем пути нет: `ToArrayJoinElement`/`CopyProjectionIndependentStateTo` копируют списки через `[.. value]`/`new List`, рендер — `for` по `IReadOnlyList<Expression>`. |
| 4. События / 6. Исключения | ✅ Подписок нет; новые `throw` — валидация билдера (`ArgumentNullException.ThrowIfNull` `:359`, `NotSupportedException` `:362`, четыре `InvalidOperationException` `:365,368,371,374`), не пустые `catch`/не `catch (Exception)`. |
| 5. Проектирование / DRY | ✅ Извлечение `CopyProjectionIndependentStateTo<TOther>` (`:536`) корректно устраняет дублирование: `CopyTo` (`:520`) вызывает его и доводит projection-зависимые `_condition`/`_having`/`_arrayJoins`/`_sourceEntityType`/`_bindArrayJoinElement`; `ToArrayJoinElement` (`:357`) переиспользует. ℹ️ жёсткая проверка `_dataProvider is InMemoryDataContext` (`:361`) — Находка 21; ℹ️ `SourceEntityType` — ниже. |
| 7. Хэш-ключи | ✅ `BindArrayJoinElement` в `Equals` (`:71`) и `GetHashCode` (`:315`), переносится `CopyTo` (`Clone.cs:57`) и `Definition` (`QueryCommand.cs:142`) — `Debug.Assert` `GetCacheVersion` сходится. `SourceEntityType` в ключ не входит, но выводим из `Exp` (тип проекции), поэтому расхождения ключа не создаёт. |

### 🟡 Находка 20 — новый файл `ArrayJoinProjection.cs` записан с LF-концевиками (ОТКРЫТА)

`src/nextorm.core/Expressions/ArrayJoinProjection.cs` — **единственный новый файл** слайса — имеет LF-концевики:
`file` → `ASCII text` (без «with CRLF»), `grep -c $'\r'` → **0**. Репозиторий объявлен CRLF (`AGENTS.md`:
«CRLF throughout… never leave LF-only or mixed»); соседние (`ArrayJoinKind.cs`, `EntityBuilder.cs`,
`MemberTranslator.cs`) — CRLF. Функционального дефекта нет, но нарушен явный инвариант репозитория, а смешение
концевиков в одном файле ломает диффы/`.gitattributes`-нормализацию.

**Рекомендация:** нормализовать `perl -pi -e 's/\r?\n/\r\n/g' src/nextorm.core/Expressions/ArrayJoinProjection.cs`
(и проверить новые тестовые вставки на смешение). Код-фикс применяет `nextorm-design-engineer`.

### 🟡 Находка 21 — привязка элемента гейтится по конкретному `InMemoryDataContext` в билдере (ОТКРЫТА)

`EntityBuilder.ToArrayJoinElement` (`Builders/EntityBuilder.cs:361`) жёстко проверяет
`if (_dataProvider is InMemoryDataContext) throw new NotSupportedException(...)`, тогда как:
- соседний `ArrayJoin`/`AddArrayJoin` (`:302-334`) так не гейтится — отказ для in-memory приходит позже, из
  `InMemoryQueryBuilder.cs:98-99`;
- для обратного случая в билдере уже есть абстракция `EnsureInMemory` (`:653-659`), и она проверяет
  `is not InMemoryDataContext`.

Итог: одно и то же семейство ведёт себя по-разному (throw на вызове билдера vs. на исполнении), а `nextorm.core`
получает прямую зависимость от конкретного провайдера. Тест `InMemoryJoinTests.cs:219-223` разницы не ловит
(лямбда включает вызов `ArrayJoinElement`).

**Рекомендация:** гейтить по возможности («диалект/контекст поддерживает `ARRAY JOIN`»), как у `ArrayJoin`,
либо делегировать отказ провайдеру; сообщение выровнять по стилю `EnsureInMemory`.

### ℹ️ Вопрос — `SourceEntityType` это осознанный обходной путь, а не smell

`internal Type? SourceEntityType` (`:96-102`) существует, чтобы у команды остался `EntityType = typeof(TEntity)`
(рендер `FROM`/алиасов не меняется), пока параметр `Select`/`Where` — `ArrayJoinProjection<TEntity,TElement>`.
Альтернативы (два generic-параметра билдера, раскрытие `Item1` в трансляторе, перенос в `QueryDefinition`) дороже
и затрагивают больше кода; для внутреннего однопроходного состояния выбранный вариант приемлем. Два замечания:
1. `QueryDefinition` (`Query/QueryDefinition.cs:13-16`) декларирует «exactly one of `Exp` or `SrcType` identifies
   the source», но ветка `Select` (`EntityBuilder.cs:116`) кладёт **оба** (`Exp` — проекция, `SrcType` — физический
   источник). Док-инвариант нужно уточнить (или переименовать `SrcType` в `SourceType`, чтобы он не читался как
   альтернатива `Exp`).
2. `EntityBuilder<ArrayJoinProjection<,>>.ToCommand()` (`:159`) строит команду **без `Exp`** с `SrcType = TEntity`:
   терминалы-расширения (`EntityBuilderExtensions.ToList`/`First`/… зовут `ToCommand()`) на таком билдере
   скомпилируются и попытаются материализовать `ArrayJoinProjection` из колонок сущности. Поддерживаемый путь
   всегда идёт через `Select`; стоит либо бросить понятное исключение, либо закрыть тестом.

### ℹ️ Наблюдения (фикс не требуется)

- **`p.Item1` без колонки.** `Select(p => p.Item1)`/`Where(p => p.Item1 == …)` не поддержаны: член `Item1` не
  проходит спец-обработку (`MemberTranslator.cs:94-109` ловит только `Element`) и упирается в терминальный
  `throw BuildSqlCommandException("Cannot resolve column for member Item1")` (`:410`). Документированный пример
  (`p.Item1.Id`) и тесты работают. Зазор UX/тестов, не дефект.
- **`ArrayJoinNames.ElementMember` — `const string "Element"`,** а не `nameof(ArrayJoinProjection<object, object>.Element)`
  (`ArrayJoinProjection.cs:41`): переименование свойства не обновит константу автоматически. Риск низкий.
- **SQL-алиас `__nextorm_aj_element`** рендерится без кавычек (`SqlBuilder.cs:119-121`); идентификатор безопасен для
  ClickHouse (двойное подчёркивание), коллизия с пользовательской колонкой крайне маловероятна. В тестах литерал
  `__nextorm_aj_element` дублируется строками (`SqlGenerationTests.cs:1123,1136,1147,1159`) — приемлемо.
- **`IArrayJoinProjection` вне `IProjection`** — см. `API-NAMING-REVIEW.md`, AJ7; сигнатура только под массив — AJ6;
  `<exception>`-доки — AJ9; публичные доки EN/RU — расширение AJ5.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе);
unit — core **161/161**, clickhouse **135/135**, postgres **179/179** (0 failed, Release, `--no-build`);
в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0** (соотношение проекта не изменилось:
5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `Skip=` — 0, пустых `catch` — 0,
`Task.Delay` — 2 (обе прежние `Task.Delay(0)` в `InMemoryTests.cs:80,369`); план-ключ проверен чтением
`QueryPlanEqualityComparer`/`Clone`/`Definition`; `file`/`grep -c $'\r'` подтверждают LF-only у
`ArrayJoinProjection.cs` (Находка 20). Реальный ClickHouse в этом проходе не перезапускался.

## 🔎 Точечный аудит — PostgreSQL/ANSI модификаторы запроса: `DISTINCT ON` / `TABLESAMPLE` / `WITH TIES` / row locking (20.09.2026)

Область: `Builders/EntityBuilder.cs` (+`Joins/JoinedEntityBuilder.cs`), `Builders/Paging.cs`,
`Query/{DistinctOnClause,TablesampleClause,TablesampleMethod,LockClause,LockMode}.cs`,
`Query/{QueryDefinition,QueryCommand,QueryCommand.Clone,QueryPlanEqualityComparer}.cs`,
`QueryCommand.QueryPreparer.cs` (`PrepareLimitBy` → общий `BuildKeyColumns`), `DataContext/SqlBuilder.cs`,
`DataContext/InMemoryQueryBuilder.cs`, `Dialect/{ISqlDialect,SqlDialectBase}.cs`,
`PostgresDialect`/`SqlServerDialect`/`MySqlDialect`, unit-тесты. Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: в `src/` **5** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma disable` (все с парным `restore`); неоправданных — 0/10. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:108,397`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. 🟡 Находка 26: мусорный `*.dump`. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей, `.Count()`/`.Any()`, подписок и `catch` нет; новые `throw` — `InvalidOperationException` (`EntityBuilder.cs:480,498`), `ArgumentOutOfRangeException` (`:513`), `NotSupportedException` (`SqlBuilder.cs:67,88,91,307,333,354,367`; `InMemoryQueryBuilder.cs:95-107`), `BuildSqlCommandException` (`SqlBuilder.cs:70`) без `catch`. |
| 5. Проектирование | 🟡 Находки 22/24/25; ℹ️ — `MakeTop` bool+out, плюмбинг ×8/×2, `BuildKeyColumns` вынесен из `PrepareLimitBy`. |
| 7. Хэш-ключи | ✅ План-ключ покрывает все 4 клаузы (см. наблюдения); 🟡 Находка 23 — `_distinctOnColumns` не сбрасывается в `ResetPreparation`. |

### 🟡 Находка 22 — LF-переводы строк в новых файлах и смешанные EOL в `EntityBuilder.cs` (ОТКРЫТА)

Пять новых файлов записаны целиком с LF: `Query/DistinctOnClause.cs`, `TablesampleClause.cs`,
`TablesampleMethod.cs`, `LockClause.cs`, `LockMode.cs` (у каждого — 100 % LF-only строк). В
`Builders/EntityBuilder.cs` три новых строки плюмбинга — LF внутри CRLF-файла (`:160`, `:206`,
`:639`), т.е. EOL смешаны. `AGENTS.md` требует CRLF по всему репозиторию (аналог Находки 20).

- **Было:** `file` показывает «LF line terminators» у пяти новых `.cs`; `perl -ne 'print if /(?<!\r)\n$/'` даёт 10/10, 7/7, 10/10, 7/7, 10/10 и 3/1068 для `EntityBuilder.cs`.
- **Стало:** нормализовать `perl -pi -e 's/\r?\n/\r\n/g' <files>`.
- **Проверка:** `file` → CRLF; `perl -ne 'print if /(?<!\r)\n$/'` → 0/0.

### 🟡 Находка 23 — `_distinctOnColumns` не сбрасывается в `ResetPreparation` (ОТКРЫТА)

`QueryCommand.ResetPreparation` (`Query/QueryCommand.cs:310-328`) обнуляет `_selectList`, `_groupingList`,
`_limitByColumns` (`:315`), но **не** `_distinctOnColumns` (`:91`). `PrepareDistinctOn`
(`QueryCommand.QueryPreparer.cs:539-547`) читает `cmd._distinctOnColumns` и пропускает
`BuildKeyColumns`, если массив не `null` (`:543-544`). После `ResetPreparation` (её вызывают
`QueryCommand<TResult>.ForLast`/`OrderBy`/`Distinct`/`Hint`/`ForJson`/`ForXml`/`Union`/`Intersect`/
`Except`, `:301,326,336,349,362,374-417`) производное состояние DISTINCT ON не инвалидируется.

- **Было:** `_limitByColumns = null;` — симметричной строки для `_distinctOnColumns` нет.
- **Стало:** добавить `_distinctOnColumns = null;` рядом с `:315`.
- **Импакт:** латентный — `DistinctOn` выставляется только на свежей команде (`EntityBuilder.Select`/
  `ToCommand`/`Clone`), пост-подготовительной мутации клаузы сейчас нет, поэтому активного неверного SQL
  не воспроизведено; но контракт `ResetPreparation` (сбросить все derive-поля) нарушен, а соседние
  `_selectList`/`_groupingList`/`_limitByColumns` его соблюдают. Исправление однострочное.
- **Проверка:** build 0/0; тест «`DistinctOn`-команда после `ResetPreparation` пересобирает
  `DistinctOnColumns`» (или сравнение двух прогонов на равенство плана).

### 🟡 Находка 24 — базовый `SupportsTablesampleMethod` бросает вместо `false` (ОТКРЫТА)

`SqlDialectBase.SupportsTablesampleMethod` (`DataContext/Dialect/SqlDialectBase.cs:166-167`) — единственный
`Supports*`-предикат с параметром, который **бросает** `NotSupportedException` по умолчанию. Все соседи
возвращают `false`: `SupportsSessionInfoFunction` (`:58`), `SupportsUuidGenerator` (`:71`),
`SupportsTableFunction` (`:469`), `SupportsDateTruncField` (`:453`). Гейт `SupportsTablesample` (`:160`)
уже отсекает неподдерживающие диалекты (`SqlBuilder.cs:87`), поэтому бросок из предиката полезной
нагрузки не несёт, зато диалект, включивший `SupportsTablesample` и забывший метод, упадёт из
capability-проверки, а не вернёт «нет».

- **Стало:** `public virtual bool SupportsTablesampleMethod(TablesampleMethod method) => false;`
  (PostgreSQL/SQL Server переопределяют, как сейчас).
- **Проверка:** build 0/0; SQL-gen-тесты SQL Server `Tablesample_Bernoulli_ShouldThrow…` не меняются
  (`SupportsTablesample`-ветка бросает раньше method-ветки).

### 🟡 Находка 25 — `WITH TIES` не гейтится с `DISTINCT`/`DISTINCT ON` (ОТКРЫТА)

`EntityBuilder` гейтит пару `DISTINCT`↔`DISTINCT ON` (`:477-504`), но `WithTies()` (`:570-576`) не
проверяет ни `IsDistinct`, ни `_distinctOn`; `SqlBuilder` для `WITH TIES` проверяет только диалект и
положительный лимит (`SqlBuilder.cs:66-70`). PostgreSQL и SQL Server не сочетают `WITH TIES` с
`DISTINCT` (PostgreSQL — и с `DISTINCT ON`), поэтому `.Distinct().Limit(n).WithTies()` доходит до
сервера как недопустимый SQL. Рантайм-эффект на живом сервере в этом проходе не перепроверялся
(аналог Находки 19).

- **Стало:** зеркальный guard в `WithTies()` (как `Distinct()`/`DistinctOn()`) либо явная оговорка в
  XML-доке, что `WITH TIES` несовместим с `DISTINCT`/`DISTINCT ON`.
- **Проверка:** unit-тест `.Distinct().Limit(2).WithTies()` → `InvalidOperationException` (или
  закреплённая документированная комбинация); живой сервер — по возможности.

### 🟡 Находка 26 — мусорный артефакт `SqlGenerationTests.cs.dump` (ОТКРЫТА)

`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs.dump` — 0 байт, untracked, не часть решения.
Похоже на отладочный дамп, оставленный прогоном тестов.

- **Стало:** удалить файл (код/тесты не затрагиваются).
- **Проверка:** `git status` — файл отсутствует.

### ✅ Находка 10 (актуализация 20.09.2026) — мёртвый `LimitByPlanHash` устранён

Рефакторинг `PrepareLimitBy` → общий `BuildKeyColumns` (`QueryCommand.QueryPreparer.cs:549-591`) убрал
вычисление `planHash`/поле `LimitByPlanHash`. `rg LimitByPlanHash src/` — **0**; `BuildKeyColumns` не
проставляет `selExp.PlanHashCode`, клауза по-прежнему хэшируется напрямую в `QueryPlanEqualityComparer`
(`:304-309`). Находка закрыта рекомендованным «Стало». Сообщения `PrepareLimitBy` сохранены
(`:527-531`), добавлен `"{clauseName} requires at least one key column."` (`:557`) и поддержка
одиночного выражения (`:578-590`) — новые SQL-gen-тесты DISTINCT ON это закрепляют.

### ✅ Находка 11 (актуализация 20.09.2026) — `PREWHERE`-список освежается

В текущем дереве `PreparePreWhere` заводит `HasPreWhereInValues` (`QueryCommand.QueryPreparer.cs:495`),
а `RefreshInValuesShape` (`:114-145`) пересчитывает `PreWhereShapeHash` (`:131-133`) и **сливает**
партиции (`:135-141`) вместо перезаписи. Рекомендованное «Стало» выполнено; статус переведён в закрытый.

### ℹ️ Наблюдения (фикс не требуется)

- **План-ключ полон по всем четырём клаузам.** `DistinctOn` — `_expComparer` + прямой хэш выражения
  (`QueryPlanEqualityComparer.cs:59-66,311-314`); `Tablesample` — record-равенство + `Method`/`Percent`/`Seed`
  (`:68,316-321`); `RowLock` — record-равенство + `Mode` (`:70,323-326`); `Paging.WithTies` — `Equals`/хэш
  (`:98,296`). `Clone.CopyTo` переносит клаузы и prepared-колонки (`QueryCommand.Clone.cs:48-53`),
  `Definition` их несёт (`QueryCommand.cs:108-111,138-141`).
- **`DISTINCT`↔`DISTINCT ON` гейт корректно двусторонний.** `Distinct()` проверяет `_distinctOn`
  (`EntityBuilder.cs:479-480`), `DistinctOn()` — `IsDistinct` (`:497-498`), оба до `Clone()`.
- **Табличные модификаторы после алиаса — верно.** `SqlBuilder` дописывает TABLESAMPLE сразу за
  `fromStr` (`:85-94`), FINAL/SAMPLE — там же (`:96-110`); для PostgreSQL и SQL Server грамматика
  допускает `table [AS alias] TABLESAMPLE …`. `TABLESAMPLE` на производной таблице/TVF не гейтится —
  тот же класс, что FM4 (FINAL/SAMPLE), новый номер не заводим.
- **`MakeTop(int,bool,out string?)` — bool+out.** Дизайн-вопрос к `ISqlDialect` (ср. `GetPagingOrderBy`,
  возвращающий `string?`); именование/форма — QM3 в `API-NAMING-REVIEW.md`.
- **`BuildKeyColumns` не хэширует `SelectExpression`.** `PrepareGrouping` проставляет `PlanHashCode`
  (`:620-628`), а `BuildKeyColumns` — нет; для LIMIT BY/DISTINCT ON колонки в план-ключ не входят
  (клауза хэшируется напрямую), поэтому это корректно, но расхождение стоит держать в виду при
  добавлении третьего потребителя.
- **Плюмбинг состояния разросся.** Три новых клаузы пробрасываются через `Select`/`ToCommand`/`CopyTo` +
  2 инициализатора `EntityBuilder` (`:792,823`) + 6 `JoinedEntityBuilder` (`:40,106,158,210,262,314`)
  одной очень длинной строкой; при следующем модификаторе — параметр-объект (ср. наблюдение в
  аудите `FINAL`/`SAMPLE`). Не-генериковый `EntityBuilder` alias-join (`:1044-1066`) не пробрасывает
  **ни одного** модификатора — пре-существующее поведение, не регресс.
- **`QueryCommand<TResult>.Distinct()` не гейтит `DistinctOn`.** В отличие от `EntityBuilder.Distinct()`,
  командный метод (`QueryCommand.TResult.cs:333-339`) молча ставит `IsDistinct` поверх `DistinctOn`;
  рендер выбирает `distinct on` (if-ветка `SqlBuilder.cs:364-388`), `IsDistinct` игнорируется. Низкий
  риск, отдельного фикса не требует.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Release,
`--no-build`, этот проход): core **165/165**, postgres **194/194**, sqlserver **185/185**, mysql **49/49**,
mariadb **14/14**, sqlite **209/209**, clickhouse **137/137** (0 failed). Соотношение подавлений проекта
не изменилось: 5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**.
Контейнерные интеграционные тесты не перезапускались.

## 🔎 Точечный аудит — PostgreSQL text-search и temporal tables (20.09.2026)

Область: `Query/SqlFunctions.Postgres.cs`, `Query/{TemporalClause,TemporalKind}.cs`,
`Visitors/ExtendedScalarFunctionTranslator.cs`, `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`,
`DataContext/SqlBuilder.cs`, `DataContext/SqlSourceRenderer.cs`, `DataContext/InMemoryQueryBuilder.cs`,
`Builders/EntityBuilder.cs`, `Query/{QueryDefinition,QueryCommand,QueryCommand.Clone,QueryPlanEqualityComparer}.cs`,
`PostgresDialect`/`SqlServerDialect`/`MariaDbDialect`, unit-тесты. Build Release — **0/0** (прогнано заново).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` в изменённых файлах — **0**. База прежняя: в `src/` **5** `SuppressMessage` (все с `Justification`) + **5** `#pragma disable` (все с парным `restore`: 3+1+1) — неоправданных **0/10**. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:115,404`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей, `.Count()`/`.Any()`, подписок и `catch` нет; новые `throw` — `NotSupportedException` (`SqlBuilder.cs:102,105`; `InMemoryQueryBuilder.cs:104`; `ExtendedScalarFunctionTranslator.cs:189,196`), `ArgumentNullException`/`ArgumentOutOfRangeException` (`EntityBuilder.cs:537`; `TemporalClause.cs:56`) без `catch`. |
| 5. Проектирование | 🟡 Находки 27/28/29. |
| 7. Хэш-ключи | 🟡 Находка 27 — `Temporal` в компараторе сравнивается по ссылке, хотя хэш считается по `Kind`/`From`/`To`. |

### 🟡 Находка 27 — `TemporalClause` без value-equality: план-ключ сравнивает клаузу по ссылке (ОТКРЫТА)

`QueryPlanEqualityComparer.Equals` сравнивает temporal-клаузу оператором `!=`
(`Query/QueryPlanEqualityComparer.cs:70`), а хэш считает по значениям `Kind`/`From`/`To` (`:325-330`).
`TemporalClause` — `public sealed class` без `Equals`/`GetHashCode`/`==` (`Query/TemporalClause.cs:8`),
поэтому `!=` — ссылочное сравнение. Соседние носители клауз — `internal sealed record`
(`Query/TableSampleClause.cs:7`, `Query/LockClause.cs:7`) — дают value-equality, поэтому их ветки
(`:68,72`) работают по значению.

- **Было:** `if (x.Temporal != y.Temporal) return false;` — каждая новая фабрика (`TemporalClause.AsOf(t)`)
  даёт новый instance, равенство не срабатывает, план-ключ не совпадает → промах план-кэша; при создании
  клаузы на каждый вызов план-кэш растёт.
- **Стало:** реализовать value-equality (`IEquatable<TemporalClause>` + `Equals`/`GetHashCode`, либо
  сделать тип `sealed record`, как соседние клаузы); хэш уже покрыт.
- **Импакт:** неверного SQL нет (разные значения → разные ссылки → not equal), страдает эффективность
  план-кэша и память. P1-кандидат.
- **Проверка:** тест «две одинаковые команды с `TemporalClause.AsOf(t)` дают равные план-ключи» (либо
  `Equals(a, b)` истинно при равных `Kind`/`From`/`To`).

### 🟡 Находка 28 — `FOR SYSTEM_TIME` рендерится после алиаса → невалидный SQL при JOIN (ОТКРЫТА, P1-кандидат)

`SqlBuilder` дописывает temporal-клаузу после `fromStr` (`DataContext/SqlBuilder.cs:99-108`), а `fromStr`
уже содержит алиас, когда `needAlias` (`:79,83`): `SqlSourceRenderer.MakeFrom` при `needAlias` добавляет
` as <alias>` (`DataContext/SqlSourceRenderer.cs:213-224`; `SqlDialectBase.MakeTableAlias` `:292`).
Итог для join'а: `from t as t0 for system_time as of '…'`.

- **Грамматика:** SQL Server — «include the `FOR SYSTEM_TIME` clause between the temporal table name and
  the alias» (MS Learn, querying temporal data); MariaDB — `table_factor: tbl_name …
  [query_system_time_period_specification] [[AS] alias]` (join syntax). Оба требуют клаузу **до** алиаса.
- **Было:** тесты покрывают только однотабличный запрос без алиаса
  (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1836-1883`,
  `tests/nextorm.mariadb.tests/SqlGenerationTests.cs:151-158`) → баг не виден.
- **Стало:** вставлять temporal-клаузу между именем таблицы (с её table hints) и алиасом — например,
  прокинуть её в `FromRenderOptions`/`MakeFrom` и рендерить рядом с alias, сохранив текущий порядок
  `TABLESAMPLE` после `fromStr`.
- **Проверка:** unit-тест «запрос с join'ом и `ForSystemTime`» содержит `… for system_time … as <alias>`,
  а не `… as <alias> for system_time …`; живой SQL Server/MariaDB — по возможности.

### 🟡 Находка 29 — LF-концевики в новых `TemporalClause.cs` и `TemporalKind.cs` (ОТКРЫТА)

Оба новых файла записаны целиком с LF: `perl -ne 'print if /(?<!\r)\n$/'` → 58/58 и 16/16. `AGENTS.md`
требует CRLF. Тот же класс, что Находки 20/22; в `EntityBuilder.cs` смешанные EOL прежние — 3 строки
`TableSample` (`:163,210,663`), не новые.

- **Стало:** `perl -pi -e 's/\r?\n/\r\n/g' src/nextorm.core/Query/TemporalClause.cs src/nextorm.core/Query/TemporalKind.cs`.
- **Проверка:** `file` → CRLF; счётчик LF-only → 0/0.

### ℹ️ Наблюдения (фикс не требуется)

- **`MakeTemporalTable` не дублируется.** Ни один провайдер его не переопределяет: SQL Server/MariaDB
  только гейтят (`SqlServerDialect.cs:69,72`; `MariaDbDialect.cs:42,45`), рендер единый в
  `SqlDialectBase.cs:191-205`. Дублирования базового рендера и провайдерных гейтов нет.
- **`SupportsTemporalKind` — `false` по умолчанию, не бросок.** `SqlDialectBase.cs:185` возвращает
  `false` (в отличие от закрытой Находки 24); `kind` не используется — согласовано с
  `SupportsTableSampleMethod` после фикса.
- **Валидация фабрик достаточна для диапазона.** `Between`/`FromTo`/`ContainedIn` проверяют `to > from`
  (`TemporalClause.cs:30-48,53-57`); `AsOf`/`All` инварианта диапазона не имеют. Замечания уровня P2:
  (а) `AsOf(default)` и даты вне диапазона `datetime` SQL Server (до 1753) не отвергаются заранее;
  (б) `MakeTemporalTable.Literal` (`SqlDialectBase.cs:193-194`) печатает `yyyy-MM-dd HH:mm:ss` — доли
  секунды и `DateTimeKind` теряются, а SQL Server трактует `AS OF` как UTC; в XML-доке не отражено.
- **`ts_*`-имена — SQL-зеркало.** Соответствуют уже зафиксированному DSL-исключению
  (`API-NAMING-REVIEW.md`, «Отмечено, но менять не рекомендуется»), отдельной находки не требуют.
- **Гейт text-search не смешан с `SupportsFullText`.** `SupportsTextSearchFunctions` — отдельный флаг
  (`ISqlDialect.cs:251`), `RequireTextSearch` (`ExtendedScalarFunctionTranslator.cs:193-198`) не
  пересекается с `contains`/`freetext`.
- **`System.Globalization.CultureInfo` в `SqlDialectBase.MakeTemporalTable`** записан полным именем при
  наличии `using System.Globalization;` (`SqlDialectBase.cs:1`) — косметика.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Release,
`--no-build`, этот проход, через `dotnet run` — `dotnet test --no-build` даёт «Zero tests ran»):
core **166/166**, postgres **198/198**, sqlserver **191/191**, mariadb **16/16**, sqlite **211/211**,
clickhouse **139/139** (0 failed / 0 skipped). Соотношение подавлений не изменилось: 5 оправданных
`SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**. Контейнерные интеграционные тесты
не перезапускались.

## Примечания

- `SonarAnalyzer.CSharp` не подключён, поэтому правила `S####` (в т.ч. в 5 `SuppressMessage` из `src/`) сборкой не проверяются. При этом `.editorconfig` глушит 7 правил (`S125`, `S108`, `S3060`, `S1104`, `S3604`, `S2292` — `silent`; плюс `CA2254`), т.е. часть записей **инертна**: без пакета Sonar они не могут сработать (мёртвые настройки). `Directory.Build.props` задаёт только `TreatWarningsAsErrors=true` (без `AnalysisLevel=latest-all`), так что CA-правила навыка (включая `CA1508`/`CA2213`/`CA1816`) сборкой не гейтятся.
- `slopwatch` локальным tool'ом не установлен (`.config/dotnet-tools.json` содержит только coverage/reportgenerator/docfx); паттерн-скан выполнен вручную (`Skip=`, `Task.Delay`, `Thread.Sleep`, `<NoWarn>`, `#pragma`, `SuppressMessage`, пустые `catch`). В `src`/`tests`/`benchmarks`: `Skip=` — 0; `Task.Delay` — 2 (обе — `Task.Delay(0)` как yield в `tests/nextorm.core.tests/InMemoryTests.cs:80,369`, не задержки); `Thread.Sleep` — 0; пустых `catch` — 0.
- На момент актуализации рабочее дерево **не чистое**: правки реестров (`docs/specs/design/*`) не закоммичены; код — HEAD `d21c473`.
- Повторно в рамках этого отчёта коммитов не делалось — по `AGENTS.md` коммит только по явному запросу.

## Следующие шаги (предложение)

1. ✅ Выполнено 18.09.2026 (Находка 4): `BaseExpressionVisitor` — `VisitMethodCall` 183→77, локальные `CompileExp`/`EmitValue` вынесены, `node switch` по `nameof(TableAlias.*)` заменён таблицей `TableAliasAccessors`, 4 `NotImplementedException` и мёртвый код убраны.
2. Находка 4, `QueryCommand` закрыт по размеру — partial-разбивка (Вариант A) + вынос конвейера в `QueryPreparer` (Вариант B): `QueryCommand` ~425 без вложенного типа, `QueryPreparer` ~484. Осталось решить, промоутить ли `QueryPreparer` в top-level тип (потребует расширения `protected`-доступа), и по желанию — `PlanComparerSet` (D) и хэши в `PlanHashes` (C, рискованно — ключ кэша).
3. ✅ Выполнено 18.09.2026 (Находка 4): остальные god-классы разобраны — `ScalarFunctionTranslator` 595→131 (`StringFunctionTranslator`/`MathFunctionTranslator`/`DateTimeFunctionTranslator`), `SqlBuilder` 716→318 (`SqlSourceRenderer` + `SqlBuildContext`), `EntityBuilder<TEntity>` 769→466 (`EntityBuilderExtensions`). `InMemoryDataContext` 351 — ✅ выполнен ранее (фазы 1–8).
4. ✅ Выполнено 18.09.2026: длинные списки параметров ≥6 — все 15 «боевых» разобраны параметр-объектами (`QueryDefinition` и др.), длинные перегрузки удалены. Осталось (осознанно): `CA2213` (`_conn` — ложное по владению) и `CA1816` (ложное из-за индирекции) закрывать не требуется; `CA1508` — осталось **2** вероятно ложных в `NormSqlTranslator.cs:223,250` (реальное место в `MemberTranslator.cs:313` исправлено 18.09.2026), сборкой не гейтится. См. раздел «Дополнительно».
5. ✅ Выполнено 18.09.2026: снята подписка `DbConnection.Disposed` при dispose контекста для внешнего соединения (Находка 6); удалены `*DELETE*.cs` и зависимый тест, мёртвый `<Pending>` (Находка 7).

## Ссылки

- [skill:dotnet-csharp-code-smells]
- [Microsoft Code Quality Rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/)
- [Framework Design Guidelines](https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/)