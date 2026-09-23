# Аудит запахов кода — nextorm

**Дата:** 16.09.2026 (повторный аудит после коммита `9641660` «mass refactoring»); **актуализация 18.09.2026 по HEAD `d21c473`**

**Предрелизный аудит v1.0.3-alpha (21.09.2026, HEAD `2a2dfa6`, рабочее дерево чистое = `origin/1.0.3-alpha`): открытых P0/P1 нет.** Находки 8, 19, 28, 52 и 53, помеченные ниже как «ОТКРЫТА» (в т.ч. с 🔴 и как P1-кандидаты), фактически исправлены в коде/тестах — статусы закрыты в этом проходе. Подавления: `src/` — **6** `SuppressMessage` (все с `Justification`), **5** `#pragma` (все с парным `restore`) = **11/11 оправданных, 0 неоправданных**; `Skip=` — 0, пустых `catch` — 0, `Task.Delay` — 2 (обе `Task.Delay(0)`-yield), `NoWarn` — только `CS1591` в 7 библиотечных `.csproj` (на 22.09.2026 `CS1591` снят во всех 7 — см. предрелизный аудит v1.0.4-alpha ниже).

**Предрелизный аудит v1.0.4-alpha (22.09.2026, HEAD `91379b1` + uncommitted working tree, ~193 файла).** **Открытых P0/P1 нет.** Релизный дифф — XML-документация публичного API (`CS1591` убран из `<NoWarn>` всех 7 библиотечных `.csproj`), правки доков EN+RU (xref, снятие публичных ссылок на `docs/specs/**`) и фичи #55–#58; в `src/` — только `///`-комментарии и 7 строк `NoWarn` (поведение не менялось). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`: `DbPreparedQueryCommand.cs:84`, `QueryExecutor.cs:71` — S2583; `SelectExpression.cs:12`, `AggregateTerminalRewriter.cs:12`, `CorrelatedQueryExpressionVisitor.cs:9` — IDE1006; `CorrelatedQueryExpressionVisitor.cs:10` — S3011) + **5** `#pragma warning disable` (все с парным `restore`: `EntityBuilderExtensions.cs:123,140,156` CS8619; `InMemoryLinqSource.cs:82` CS8714; `ExpressionPlanEqualityComparer.cs:421` IDE0066) = **11/11** оправданных, **0** неоправданных; новых в диффе — **0**. `Skip=` — **0**; пустых `catch` — **0** (единственный `catch` `src/` — `InMemoryAggregates.cs:84`, фильтр `TargetInvocationException` + переброс через EDF); `Task.Delay` — **2** (обе baseline `Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`); `Thread.Sleep` — 0. **`NoWarn` больше ничего не глушит:** во всех 7 `.csproj` теперь `<NoWarn>$(NoWarn)</NoWarn>` (no-op) — `CS1591` снят, и под `TreatWarningsAsErrors=true` пропуск XML-дока публичного члена роняет сборку. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан выполнен вручную. XML-док-работа закрыла внутренние находки **61** и **72** (устаревшие XML-summary `TypeFacts`/`AdvancedAggregateTranslator`, forwarder `IsSingleColumnType`; summary `NormSqlTranslator`/`TranslateNormParam` теперь упоминают `SqlFunctions.Column<T>`). Незакрытые находки имеют только 🟡 P2/ℹ️-приоритет; заморозка `PublicAPI.Shipped/Unshipped.txt` (issue #53, Шаг 5) остаётся **P2** и релиз не блокирует. API-сторона — `API-NAMING-REVIEW.md` (предрелизный аудит v1.0.4-alpha).

**Обновление 22.09.2026 (uncommitted worktree `clickhouse-json-type`).** **Находка 60** (🔴 P1-кандидат: `ClickHouseFunctions.json_all_paths_with_types` был объявлен `string[]` при нативном `Map(String, String)`) **закрыта 22.09.2026** вариантом B: контракт изменён на `Dictionary<string,string>` + `GetValue`-ветка в `SelectExpression.GetDataRecordMethod`, материализация подтверждена контейнерным интеграционным тестом на реальном ClickHouse. `json_all_paths` (`Array(String)`) и `to_json_string` — вне дефекта. Разбор и рекомендации — в конце журнала; API-сторона — `API-NAMING-REVIEW.md`, J8–J10.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse row reader `Array(T)`/`Tuple`).** Добавлены `GetValue`-ветки для `T[]`/`System.Tuple` (`SelectExpression.cs:117-130`), классификация формы проекции (`TypeFacts.cs:45-74`) и ClickHouse-агрегаты `group_array`/`group_uniq_array` (`SqlFunctions.ClickHouse.cs:127-143`, гейт `SupportsArrayFunctions`). Build `0/0`, clickhouse **201/201**, postgres **265/265**; новых подавлений/слопа нет (11/11). Открыта **Находка 61** (устаревшие XML-summary классификации); доки-противоречия — `API-NAMING-REVIEW.md`, CHARR2/CHARR3.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse higher-order/lambda array-функции).** Добавлены `array_map`/`array_filter`/`array_exists`/`array_all`/`array_count`/`array_first`/`array_first_index`/`array_last`/`array_last_index` + флаг `SupportsHigherOrderArrayFunctions`. Build `0/0`; clickhouse **208/208**, postgres **266/266**, контейнерная интеграция ClickHouse **15/15** (0 skipped; включая 5 новых). Новых подавлений/слопа — **0** (соотношение **11/11**). **Находки 62** (вложенная лямбда теряла внешний параметр) и **63** (`Clone()` без param-режимного guard) исправлены, подтверждены тестом (`NestedHigherOrderLambda_ShouldReferenceOuterParameter`) и build `0/0`; дублирование emit-цикла — продолжение Находок 15/30. Док-противоречие закрыто — `API-NAMING-REVIEW.md`, HOAF2/HOAF3.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse параметризованные array-агрегаты `topK`/`topKWeighted`/`quantiles`).** Добавлены `ITopKAggregateRenderer` (+ DIM `ISqlDialect.TopKAggregates`, `SqlDialectBase`/`ClickHouseDialect` override), абстрактный `IQuantileAggregateRenderer.RenderArray`, 3 публичных метода `ClickHouseFunctions` (`quantiles`/`top_k`/`top_k_weighted`) и трансляция `EmitQuantiles`/`EmitTopK` (`AdvancedAggregateTranslator.cs`). Build `0/0`; ClickHouse **220/220**, postgres **268/268**, контейнерная интеграция ClickHouse+capability-contract **73/73**; полный прогон **2315 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.4%** (базис HEAD: 85.4%/74.4%). Новых подавлений/слопа — **0** (соотношение **11/11**). **Находка 64** (🟡 P2: `EmitTopK` в param-режиме не обходил `k` → расхождение списков параметров извлечения/рендера при захваченном `k`) **исправлена** (вариант A: `Visit(Arguments[0])` в param-ветке; тесты `TopK_WithCapturedK_ShouldParameteriseK` и `TopK_WithCapturedK_ShouldRefreshParamsOnCachedPlan`). API-сторона — `API-NAMING-REVIEW.md`, CHQA1–CHQA4.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse предикаты над массивами `startsWith`/`endsWith`/`hasSubstr`, срез 4 `todo_clickhouse_arrays.md`).** Добавлены `ClickHouseFunctions.starts_with`/`ends_with`/`has_substr` (`Query/SqlFunctions.ClickHouse.cs:451,454,462`, гейт `SupportsArrayFunctions`) и три `case` в `ArraySqlTranslator` (`Visitors/ArraySqlTranslator.cs:212-220`). Build Release `0/0`; новых подавлений/слопа — **0** (соотношение **11/11**). Открытых запахов изменение не вносит; API-сторона — `API-NAMING-REVIEW.md`, CHARP1–CHARP3.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse скалярная поверхность над `Tuple` `tuple`/`tupleElement`, срез 5 `todo_clickhouse_arrays.md`).** Добавлен `internal static TupleSqlTranslator` (`Tuple.Create`→`tuple(...)`, `Tuple<>.ItemN`→`tupleElement(t,n)`, гейт `SupportsTupleFunctions`) и два вызова (`BaseExpressionVisitor.VisitMethodCall`, `MemberTranslator.TryTranslate`). Build Release `0/0`; clickhouse **226/226**, postgres **271/271** (прогнано). Новых подавлений/слопа — **0** (соотношение **11/11**). Открыта **Находка 65** (🟡 P2: `.ItemN`-гард допускает `new Tuple<...>` без tuple-рендера → битый `tupleElement(...)`); остальное — ℹ️-наблюдения. API-сторона — `API-NAMING-REVIEW.md`, CHTUP1–CHTUP2.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse array-возвращающие JSON-функции `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`, срез 6 `todo_clickhouse_arrays.md`).** Добавлены 6 публичных методов `ClickHouseFunctions.json_extract_keys`/`json_extract_array_raw`/`json_extract_keys_and_values<T>` (`Query/SqlFunctions.ClickHouse.cs:194-224`), 3 ветки `JsonExtractSqlTranslator` (`Visitors/JsonExtractSqlTranslator.cs:44-52`) и 3 маппинга `ClickHouseDialect.MakeJsonExtract` (`:198-200`); `EmitFunction` получил необязательный `Type? valueType`, дописывающий `'{MakeTypeName(...)}'` после аргументов (`:105,125-130`). Build Release `0/0` (этот проход); clickhouse **228/228**, postgres **271/271** (по отчёту автора: интеграция `ClickHouseIntegrationTests` **75/75**, полный прогон **2330 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%**). Новых подавлений/слопа — **0** (соотношение **11/11**). Открыта **Находка 66** (🟡 P2: nullable `T` в `json_extract_keys_and_values<T>` расходится с `value_type`-литералом → потенциальный `InvalidCastException`); остальное — ℹ️-наблюдения. API-сторона — `API-NAMING-REVIEW.md`, CHJS1–CHJS3.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse иерархические dictionary-функции `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`, срез 7 `todo_clickhouse_arrays.md`).** Добавлены `ClickHouseFunctions.dict_get_hierarchy<TKey>`/`dict_get_children<TKey>` (→ `ulong[]`) и `dict_is_in<TKey>` (→ `bool`) (`Query/SqlFunctions.ClickHouse.cs:316,323,331`), 3 ветки `DictionarySqlTranslator.TryTranslate` (`Visitors/DictionarySqlTranslator.cs:31-39`) и 3 маппинга `ClickHouseDialect.MakeDictionaryFunction` (`src/nextorm.clickhouse/ClickHouseDialect.cs:242-244`); текст `NotSupportedException` и классовая `<summary>` транслятора дополнены (`:48`, `:6-8`). Build Release `0/0`; полный прогон **2333 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%**. Новых подавлений/слопа — **0** (соотношение **11/11**). **Находка 67** (🟡 P2: тест `…_WithCapturedDict_ShouldRefreshParamsOnCachedPlan` не проверял refresh/значение параметра) **исправлена** вариантом A (тест меняет `dict` на `"dict2"` и проверяет `Parameters["dict"].Value`); остальное — ℹ️-наблюдения. API-сторона — `API-NAMING-REVIEW.md`, CHDH1–CHDH3.

**Обновление 22.09.2026 (uncommitted worktree — ClickHouse `SEMI`/`ANTI`/`PASTE` joins).** Добавлены `JoinType.Semi`/`Anti`/`Paste` (`Expressions/JoinExpression.cs:28,33,39`), DIM-гейты `ISqlDialect.SupportsSemiAntiJoin`/`SupportsPasteJoin` (+`SqlDialectBase`/`ClickHouseDialect`), рендер `left semi`/`left anti`/`paste join` (`ClickHouseDialect.MakeJoinKeyword:92-105`), 30 публичных DSL-перегрузок `SemiJoin`/`AntiJoin`/`PasteJoin` (`EntityBuilder`/`JoinedEntityBuilder<T1..T7>`) и обобщение `MakeFrom` (`SqlSourceRenderer.cs:251-254,416-419`). Build Release `0/0`; новых подавлений/слопа — **0** (соотношение **11/11**). Открыты 🟡 **Находки 68–71** (дублирование `PasteJoin`/`AddSemiAntiJoin` с `JoinCore`/`ReplaceLastJoin`; мёртвый параметр `rightEntityType`; невыводимый/несвязанный `PasteJoinCore<TJoinEntity>`; снятый `Debug.Assert` в `MakeFrom`); API-сторона — `API-NAMING-REVIEW.md`, CHJ1–CHJ4 (CHJ1 — **P1 док**: проза «`SEMI`/`ANTI`/`PASTE` not supported» устарела).

**Обновление 22.09.2026 (uncommitted worktree — доступ к колонкам mapped-сущностей по имени `SqlFunctions.Column<T>`).** Добавлен публичный маркер `SqlFunctions.Column<T>(object, string)` (`Query/SqlFunctions.cs:75`), трансляция `TranslateNormParam`→`TranslateColumn` (`Visitors/NormSqlTranslator.cs:40-44,75-85`) и `AppendColumnReference`/`AppendColumnAlias`/`UnwrapConvert` (`Visitors/BaseExpressionVisitor.cs:206-251`). Build Release `0/0`; in-memory `NotSupportedException` подтверждён (`InMemoryTests` 81/81). Новых подавлений/слопа — **0** (соотношение **11/11**). Открыты 🟡 **Находка 72** (устаревшие XML-summary/имя `TranslateNormParam`) и 🟡 **Находка 73** (`object entity` без проверки источника → молча голый/чужой идентификатор); остальное — ℹ️-наблюдения. API-сторона — `API-NAMING-REVIEW.md`, CHCB1–CHCB3.

**Область анализа:** `src/` (основной), дополнительно `tests/` и `benchmarks/`
**Метод:** read-only аудит по каталогу `skill:dotnet-csharp-code-smells` + `skill:slopwatch` (паттерн-скан выполнен вручную: локальный tool `slopwatch` в `.config/dotnet-tools.json` не установлен)
**Статус:** Находки 1 (подавления), 2 (`IDisposable`), 3 (LINQ), 4 (god-классы), 5 (хэш-ключи), **6 (утечка подписки внешнего соединения)** и **7 (`*DELETE*.cs`)** — **исправлены/закрыты 18.09.2026**. Находка 4: god-классы разобраны — `EntityBuilder<TEntity>` **769→466**, `SqlBuilder` **716→318**, `ScalarFunctionTranslator` **595→131**, `BaseExpressionVisitor` **425→366** (`VisitMethodCall` 183→77); единственное исключение автора — `ExpressionPlanEqualityComparer` (879 формально, 77 собственных строк). Длинные списки параметров ≥6 — все 15 «боевых» разобраны параметр-объектами. Осознанно не закрываются: `CA2213`/`CA1816` (ложные) и `CA1508` (2 вероятно ложных в `NormSqlTranslator.cs:223,250`, сборкой не гейтится). `#pragma disable` в `src/` — 5, все с `restore`.

> Номера строк — по актуальному рабочему дереву HEAD `d21c473` (Build `0 warnings / 0 errors`), если не указано иное. Числа в разделах «Повторный аудит» и «Динамика» — исторические снимки на соответствующий коммит.
>
> **Примечание (20.09.2026):** ссылки на удалённые рабочие отчёты в этом журнале — историческое свидетельство аудита; их выводы перенесены в документацию.

---

## Сводка

| Категория навыка | Статус |
|---|---|
| 1. Управление ресурсами (`IDisposable`) | ✅ Находка 2 держится; `CA2213` — 1 (ложное, `_conn` по владению), `CA1816` — 2 (ложные из-за индирекции) |
| 2. Подавление предупреждений | ✅ В `src/`: 6 `SuppressMessage` (все с `Justification`, 0 `<Pending>`) + 5 `#pragma` (все с `restore`). В `tests/`: мёртвые `*DELETE*.cs` с 2 `<Pending>` удалены (Находка 7); `<Pending>` — 0 |
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
| 2. Подавления | ✅ В `src/`: 6 `SuppressMessage` (все с `Justification`, 0 `<Pending>`) + 5 `#pragma` (все с `restore`). В `tests/nextorm.core.tests/` мёртвые `*DELETE*.cs` (Находка 7) удалены 18.09.2026; `<Pending>` — 0. |
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

### Оставлено с обоснованием (6)

| Файл:строка | Правило | Обоснование |
|---|---|---|
| `DataContext/Cache/DbPreparedQueryCommand.cs:84` | S2583 | Обе ветки достижимы: `@params` может быть `null`; анализатор не моделирует преобразование в `ReadOnlySpan`. |
| `DataContext/QueryExecutor.cs:71` | S2583 | Состояние пула соединений заранее неизвестно — проверка `ConnectionState.Closed` достижима с обеих сторон; сайт переехал из `DataContext` при выносе исполнения (шаг F1). |
| `Expressions/SelectExpression.cs:12` | IDE1006 | `GetInt32MI` и подобные — намеренный PascalCase для immutable-таблиц рефлексии; правило лишь suggestion. |
| `Visitors/AggregateTerminalRewriter.cs:12` | IDE1006 | То же для `CountMI` и подобных immutable-таблиц рефлексии. |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:9` | IDE1006 | То же для `AnyMIGeneric`, `ConcatMI` и др. |
| `Visitors/CorrelatedQueryExpressionVisitor.cs:10` | S3011 | Рефлексия нужна, чтобы привязать приватные `Any`/`Concat` в деревья выражений; публичной альтернативы нет. |

### `#pragma warning disable` — 5 шт. в `src/`, все с `restore`

| Файл:строка | Правило | `restore` |
|---|---|---|
| `Builders/EntityBuilderExtensions.cs:123` | CS8619 | ✅ `:125` |
| `Builders/EntityBuilderExtensions.cs:140` | CS8619 | ✅ `:142` |
| `Builders/EntityBuilderExtensions.cs:156` | CS8619 | ✅ `:158` |
| `Query/ExpressionPlanEqualityComparer.cs:421` | IDE0066 | ✅ `:423` |
| `DataContext/InMemoryLinqSource.cs:82` | CS8714 | ✅ `:84` |

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

### ✅ Находка 8 — SQL Server получает недопустимый `datepart(doy, …)` (ИСПРАВЛЕНА 21.09.2026; была 🔴)

> **Закрыто 21.09.2026 (HEAD `2a2dfa6`).** `SqlServerDialect.MakeDatePart` (`src/nextorm.sqlserver/SqlServerDialect.cs:188-196`) теперь маппит `"doy" => datepart(dayofyear, …)`. Покрытие: SQL-gen `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs` и интеграционный `tests/nextorm.integration.tests/CommonTestSuite.Functions.cs` (runs every provider). Неактуален был реестр, а не код.

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
- `DateTime.DayOfWeek` намеренно не маппится. Обоснование в плане слишком широкое: `datepart(weekday)`/`DATEFIRST` — это только SQL Server; PostgreSQL `extract(dow …)` и SQLite `strftime('%w')` уже 0-based как .NET, MySQL `dayofweek()-1` и ClickHouse `toDayOfWeek()%7` дают тот же результат. Т.е. безопасный пер-диалектный маппинг существует, но это отдельная задача.
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
- **Расхождения с планом.** План заявлял `LimitBy_WithOffsetAndOrderBy_ShouldOrderAndLimit`,
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

`EntityBuilder.PreWhere` (`EntityBuilder.cs:257-264`) перезаписывает `_preWhere`, тогда как `Where` (`:181-194`) соединяет предикаты через `AndAlso`. `.PreWhere(a).PreWhere(b)` молча теряет `a`, хотя цепочка выглядит накопительной. план заявлял образец `Where` — по подготовке выражения образец соблюдён, по семантике — нет.

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

Область: `SqlFunctions.IZerosRow` (`Query/SqlFunctions.cs:111-120`), `ClickHouseFunctions.zeros`/`zeros_mt` (`Query/SqlFunctions.ClickHouse.cs:170-185`), `ClickHouseDialect.SupportsTableFunction` (`src/nextorm.clickhouse/ClickHouseDialect.cs:183-185`), `SqlSourceRenderer.MakeTableFunction` (`DataContext/SqlSourceRenderer.cs:311-317`), тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:555-579`, `ClickHouseDialectTests.cs:98-105`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1458-1468`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:177-187`. Build Release — **0/0**; unit: clickhouse **96/96**, core **157/157**, postgres **171/171** (0 failed); контейнерная интеграция `ZerosTableFunction_ShouldReturnThreeRows` на реальном ClickHouse зелёная (ранее; в этом аудите не перезапускалась).

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
`GlobalIn_Subquery_/Values_ShouldFilter` на реальном ClickHouse — зелёные ранее
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

**Ответ на вопрос 1: да, это реальный aliasing-баг, а не только «пометка на месте».** Заявленное ранее обоснование (замена объекта рассинхронизировала бы `JoinCondition` и `Joins[^1]` в arity 2) верно как ограничение реализации, но закрыто мутацией разделяемого состояния. `EntityBuilder.cs:17-20` объявляет билдер «Fluent, immutable query builder», а все прочие модификаторы (`Final`/`Sample`/`Settings`/`PreWhere`/`WithTotals` — `EntityBuilder.cs:200-273,595-599`) идут через `Clone()`-then-set. `WithStrictness` же делает `_joins[^1].Strictness = strictness` и `return this` (`EntityBuilder.cs:285,287`).

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

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе); в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `find -name 'PublicAPI*.txt'` — пусто. Тесты в этом проходе не перезапускались (только build); результаты относятся к прогону автора фичи.

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

План и XML-док `Global()` заявляли комбинируемость в любом порядке, но покрыт только один порядок: `GlobalJoin_ShouldRenderGlobalModifier` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:859-875`) проверяет `.Global().WithStrictness(Any)` → `global any join`; обратный порядок `.WithStrictness(Any).Global()` и «`WithStrictness` после `Global` не сбрасывает global» отдельным тестом не зафиксированы (сам код корректен — см. вопрос 1). Также нет `Global_WithoutJoin_ShouldThrow` (аналога `WithStrictness_WithoutJoin_ShouldThrow` — `:817-825`) и нет плоской цепочки, упражняющей ковариантный `new Global()` (аналога `WithStrictness_ThenJoin_ShouldKeepStrictnessOnFirstJoin` — `:844-856`).

**Было/Стало:** код менять не нужно; **Стало** — добавить 2-3 регресс-теста (обратный порядок, `Global()` без join, flat-chain arity). Рекомендация для `nextorm-design-engineer`; не блокирует, поэтому находка помечена ℹ️.

### ℹ️ Наблюдения (фикс не требуется)

- **`Strictness`/`IsGlobal` можно сузить до `internal init`.** Оба свойства выставляются только в объектном инициализаторе `new JoinExpression { ... }` (`JoinExpression.cs:72`, `EntityBuilder.cs:315-321`), поэтому `init` вместо `internal set` дополнительно запретил бы любую будущую in-place-мутацию и закрепил инвариант Находки 16. Косметическое упрочнение, не дефект.
- **`Global()`/`IsGlobal`/`SupportsGlobalJoin` — нейминг конвенциям соответствует.** `Is*` для bool, `Supports*` — как `SupportsGlobalPredicates` (GLOBAL IN) и `SupportsJoinStrictness`; `Global()` — терсный модификатор в одном ряду с `Distinct()`/`Final()`/`Sample()`. P0/P1 нет (см. `API-NAMING-REVIEW.md`, раздел «ClickHouse GLOBAL JOIN (точечный аудит 20.09.2026)»).

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (прогнано в этом проходе); в изменённых файлах `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение подавлений проекта не изменилось (5 оправданных `SuppressMessage` + 5 парных `#pragma`, неоправданных — **0/10**); `find -name 'PublicAPI*.txt'` — пусто (Шаг 5 открыт); XML-`<summary>` есть у `IsGlobal`, `SupportsGlobalJoin`, `Global()` (8 перегрузок: базовая + 7 `<inheritdoc/>`) и у обеих реализаций `MakeJoinKeyword`. Тесты в этом проходе не перезапускались (только build); ClickHouse unit/integration — по прогону автора фичи.

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
| 5. Проектирование | ✅ **Находка 19** — default-разделитель `array_string_concat` закрыт (ниже); ℹ️ док-дрейф классовых `<summary>` (см. `API-NAMING-REVIEW.md`, раздел «ClickHouse массивы и `arrayJoin`», AR3). |
| 7. Хэш-ключи | ✅ Новых членов план-ключа нет: методы DSL различаются `MethodInfo` в дереве выражения; захваченный массив биндится параметром, а `QueryPlanner.ExtractParams` освежает его значение на кэш-хите (`QueryPlanner.cs:170-180`) — имя не `norm_pN`, поэтому `NeedsParamRefresh` истинно. |

### ✅ Находка 19 — `array_string_concat` при default-разделителе больше не рендерит SQL `null` (ИСПРАВЛЕНА 21.09.2026; была P1-кандидат)

> **Закрыто 21.09.2026 (HEAD `2a2dfa6`).** `ArraySqlTranslator` (`src/nextorm.core/Visitors/ArraySqlTranslator.cs:209-217`) при `args[1]` = `ConstantExpression { Value: null }` опускает второй аргумент (`arrayStringConcat(col)`), а не рендерит `null`. Покрытие — `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs`.

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
| 5. Проектирование | 🟡 Находки 27/29; ✅ Находка 28 закрыта 21.09.2026 (temporal до алиаса). |
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

### ✅ Находка 28 — `FOR SYSTEM_TIME` рендерится до алиаса (ИСПРАВЛЕНА 21.09.2026; была P1-кандидат)

> **Закрыто 21.09.2026 (HEAD `2a2dfa6`).** `SqlSourceRenderer` (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:223-225`) вставляет temporal-клаузу между именем таблицы и алиасом (SQL Server/MariaDB-грамматика). Покрытие — `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs`, `tests/nextorm.mariadb.tests/SqlGenerationTests.cs`.

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

## 🔎 Точечный аудит — PostgreSQL `round(double,n)`, `extract`/`date_part`, `setseed`, `digest`/`sha256`, `array_shuffle`/`array_sample` (20.09.2026)

Область: 5 коммитов ветки `todo-pg` (`5367081`…`496ded7`) — `Query/SqlFunctions{,.Postgres}.cs`,
`DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`,
`Visitors/{MathFunctionTranslator,BuiltinFunctionTranslator,ExtendedScalarFunctionTranslator,ArraySqlTranslator}.cs`,
диалекты Postgres/MySql/Sqlite/SqlServer/ClickHouse, unit- и интеграционные тесты. Build Release — **0/0**.

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В диффе `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`), `#pragma disable` — **5** (все с парным `restore`); неоправданных — **0/10**. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs:115,404`); `Thread.Sleep`/пустых `catch` — 0; инлайновых `Version`/`VersionOverride` — 0. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей/`using`, `.Count()`/`.Any()`/LINQ, подписок и `catch` нет; новые `throw` — `NotSupportedException` (`BuiltinFunctionTranslator.cs:288,293,295,298`; `ExtendedScalarFunctionTranslator.cs:126,135`), без `catch`. |
| 5. Проектирование | 🟡 **Находка 30** — `EmitCrypto` дублирует emit-цикл (ниже); ℹ️ ниже — дубль `MakeMathFunction` (DIM+база) и лишняя аллокация `Type[]`. |
| 7. Хэш-ключи | ✅ Новый код не трогает `GetHashCode`/план-ключ; `DatePartFields` — `static readonly HashSet` с `StringComparer.Ordinal` (`SqlDialectBase.cs:419-423`), на горячем пути не вызывается. |

### 🟡 Находка 30 — `EmitCrypto` — очередная копия семейства emit-циклов (ОТКРЫТА, P2)

`ExtendedScalarFunctionTranslator.EmitCrypto` (`:168-189`) построчно повторяет структуру
`SqlOperandTranslator.EmitFunction` (`:193-211`): param-режим → `NeedAliasForColumn` → `Append(name)`
+ цикл `VisitToString`. Отличие намеренное и обосновано (`:163-166`): `byte[]`-аргумент через
`AppendArgument` попал бы в `AppendArrayOperand`, и колонка-`bytea` была бы связана как параметр
вместо ссылки на колонку. Но это уже **18** циклов `for (var (i, cnt) = (0, args.Count))` в **8**
файлах `Visitors/` (было 16/7 в аудите `todo-pg`; +2 — `DateConversionSqlTranslator.cs:86,94`,
слияние `todo-ch`, см. сводный аудит ниже); порог «пятого семейства» из наблюдения
`DictionarySqlTranslator` перейдён, а `EmitJsonPathFunction` (Находка 15) — тот же класс дублирования.

- **Стало:** добавить в `SqlOperandTranslator.EmitFunction` режим «аргумент-массив рендерить как
  значение» (флаг/делегат рендера), `EmitCrypto` свести к проверке `SupportsCryptoFunctions` + вызову;
  SQL и текст исключений не меняются.
- **Проверка:** build 0/0; `CryptoHash_ShouldEmit` (postgres), `CryptoHash_ShouldThrowBecauseOnlyPostgresHasIt`
  (mysql/mariadb/sqlite/sqlserver/clickhouse), интеграционный `CryptoHash_ShouldReturnSha256`.
- **Статус (todo-pg):** оставлена открытой (P2). Исправление требует режима «аргумент-массив рендерить
  как значение» в общем `SqlOperandTranslator.EmitFunction`, что выходит за рамки пяти пунктов;
  различие в `EmitCrypto` намеренное (`byte[]`-аргумент) и потому не удаляется без этого режима.

### ℹ️ Наблюдения (фикс не требуется)

- **`MakeMathFunction` 3-арг. задвоен осознанно.** DIM `ISqlDialect.cs:678-679` + `SqlDialectBase.cs:433-434`
  (тело-делегат к 2-арг.). Это **не мёртвый** член (в отличие от P2-кандидата `MakeTextJsonFunction(string)`):
  без `virtual` в базе `PostgresDialect.cs:180-183` не смог бы переопределить DIM, а DIM защищает
  внешних реализаторов `ISqlDialect`. Обе записи войдут в заморозку (`API-NAMING-REVIEW.md`, RD2).
- **Лишняя аллокация `Type[]`.** `MathFunctionTranslator.TryTranslate:50-56` теперь всегда строит
  `new Type[args.Count]`, хотя типы читает только PG-`round`; аллокация на холодном пути построения
  плана (не на выполнении) — импакт мал.
- **SQL Server `epoch` теряет доли миллисекунды.** `datediff_big(millisecond, '19700101', value)`
  считает границы в мс — для `datetime2` дробная часть усекается (`SqlServerDialect.cs:187`).
- **MySQL/MariaDB `epoch` зависит от сессии.** `unix_timestamp(value)` трактует `datetime` в
  сессионной временной зоне (`MySqlDialect.cs:201`).
- **ClickHouse `epoch` — целые секунды.** `toUnixTimestamp` возвращает секунды, `toFloat64` дробь не
  восстанавливает (`ClickHouseDialect.cs:387` — ссылка актуализирована 20.09.2026, было `:343`),
  тогда как PG/SQLite/MySQL/SQL Server её сохраняют. На реальном ClickHouse в сводном аудите
  20.09.2026 не перепроверялось (контейнер не поднимался).
- **Слишком мягкий допуск интеграционного теста.** `Extract_ShouldReturnNormalisedDateParts`
  сравнивает `Epoch` через `BeApproximately(..., 86400.0)` (сутки) — сдвиг на часы тест не поймает
  (`tests/nextorm.integration.tests/CommonTestSuite.Functions.cs`).
- **LF-концевиков нет.** Дифф не добавляет ни одного `.cs`-файла (все `M`), Находки 20/22/29 не
  расширяются.
- **`DatePartFields` — ANSI-подмножество.** Базовый `HashSet` (`SqlDialectBase.cs:419-423`) содержит
  `year`/`quarter`/`month`/`week`/`day`/`doy`/`hour`/`minute`/`second`, но не `dow`/`isodow`/`epoch`
  (их добавляют все 5 диалектов). Внешний реализатор `ISqlDialect`, переопределивший `SupportsDatePart`
  без `MakeDatePart`, получит `extract(dow from …)` — риск того же класса, что закрывал IF5, вне
  репозитория.
- **`DialectCapabilityContractTests.ThrowingRenderers` не расширен — корректно:** новые флаги гейтят
  вызовы в трансляторах, а `MakeDatePart`/`MakeMathFunction` имеют не-падающие дефолты; паритет
  `SupportsDatePart` ⇒ ветка `MakeDatePart` покрыт SQL-тестами диалектов.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Release,
`--no-build`, этот проход): core **166/166**, postgres **206/206**, sqlserver **195/195**, mysql
**52/52**, mariadb **19/19**, sqlite **220/220**, clickhouse **143/143** (0 failed / 0 skipped).
Интеграционные на Podman-сокете (5 новых тестов × провайдеры) — **11/11 passed, 0 skipped**.
Соотношение подавлений не изменилось: 5 оправданных `SuppressMessage` + 5 парных `#pragma`,
неоправданных — **0/10**.

## 🔎 Сводный аудит — слияние `todo-pg`/`todo-mssql`/`todo-ch` в `1.0.3-alpha` (20.09.2026)

Область (только дельта трёх merge'ей; база `d4ede0e`, HEAD `c8e8686`): `ClickHouseDialect.MakeDatePart`/
`MakeDateConversion`/`SupportsDatePart`/`WrapTableFunction`, `ClickHouseFunctions` (24 `to_*`-метода +
`generate_random()`/`generate_random(long)`) и `SqlFunctions.IGenerateRandomRow`,
`Visitors/DateConversionSqlTranslator.cs` (новый), `StringFunctionTranslator.TryTranslateSplit`,
`SqlSourceRenderer.MakeTableFunction` (`WithClause`), `SqlDialectBase`/`ISqlDialect` (новые члены).
Build Release — **0 warnings / 0 errors** (прогнано в этом проходе).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В диффе `d4ede0e..HEAD` (`src`+`tests`) новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`), `#pragma disable` — **5** (все с парным `restore`); неоправданных — **0/10**. `CS1591` остаётся в `<NoWarn>` 7 библиотечных `.csproj` (принято, Шаг 5 открыт). |
| Слоп-паттерны (slopwatch локально не установлен; скан вручную) | ✅ `Skip=`/`Thread.Sleep`/пустых `catch`/инлайновых `Version`/`VersionOverride` — **0**; `Task.Delay` — 2 прежние `Task.Delay(0)` (`tests/nextorm.core.tests/InMemoryTests.cs`), не задержки. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей/`using`, LINQ, подписок и `catch` нет; новые `throw` — `NotSupportedException` без `catch` (`StringFunctionTranslator.cs:330-349`, `DateConversionSqlTranslator.cs:103`). |
| 5. Проектирование | ℹ️ **Находка 30 актуализирована** (+2 цикла `DateConversionSqlTranslator`); `WithClause`-ветка — 3 строки без дубля; `MakeMathFunction` 3-арг. — прежнее обоснованное ℹ️; `DateConversionSqlTranslator` — седьмой семейный диспетчер (см. ниже, новой находки не заводится). |
| 7. Хэш-ключи | ✅ `MakeDateConversion`/`DateConversionSqlTranslator` — чистый рендер, план-ключ не трогают; `ClickHouseDialect.MakeDatePart` — детерминированный `switch` без mutable state. |

### ℹ️ Наблюдения (фикс не требуется)

- **Слияние `MakeDatePart` (PG+CH) когерентно — проверено вручную.** `dow` = `toInt32(toDayOfWeek(x) % 7)`:
  `toDayOfWeek` — ISO 1..7 (Mon..Sun), `% 7` даёт `0`=Sunday..`6`=Saturday, что совпадает с
  `extract(dow)` у PG/SQLite/MySQL/SQL Server; `isodow` = `toInt32(toDayOfWeek(x))` = 1..7 (Mon..Sun)
  совпадает с `extract(isodow)`; `week` = `toInt32(toISOWeek(x))` = ISO 8601; `year/quarter/month/day/
  doy/hour/minute/second` — `toXxx` с `toInt32` (нативные UInt8/UInt16); `epoch` — `toFloat64`. Кросс-провайдерные
  наборы `SupportsDatePart` сходятся: PG/CH добавляют `dow`/`isodow`/`epoch`, `week` уже в базовом
  `DatePartFields` (`SqlDialectBase.cs:421-425`). Дублей веток от merge нет.
- **`to_day_of_week` через `MakeDateConversion` — корректен и намеренно не равен `extract('dow')`.**
  `ClickHouseDialect.cs:497,517`: `toInt32(toDayOfWeek(x))` = нативные ISO 1..7; XML-док метода
  «Monday is 1, Sunday is 7» (`SqlFunctions.ClickHouse.cs:306`) совпадает. Нормализованный `0..6` — только
  у `extract`/`date_part` (`MakeDatePart`). Асимметрия документирована в обоих местах, поэтому не находка.
- **`DateConversionSqlTranslator` — седьмой почти-дубликат семейного диспетчера** (после
  `TextJson`/`JsonExtract`/`Uuid`/`SessionInfo`/...; ср. наблюдение в аудите UUID). Структура `TryTranslate`
  → `RequireSupport` → param-mode guard → `Builder.Append` повторяет уже принятый паттерн; это **тот же
  класс**, что Находки 15/30, и отдельная находка не заводится. `EmitPart`/`EmitConversion`
  (`:64-98`) добавляют 2 цикла `(0, args.Count)` (см. Находку 30).
- **`WrapTableFunction`/`generateRandom` режет `call` по строке.** `ClickHouseDialect.cs:278-284`:
  `call["generateRandom(".Length..^1]` завязан на verbatim-рендер `MakeFunction` (базовый, имя не квотируется).
  Сегодня корректно и даёт валидный `generateRandom('id UInt64, value Float64, name String'[, seed])`; хрупко
  только при будущем переопределении `MakeFunction` в ClickHouse. ℹ️, фикс не требуется.
- **`WithClause` + `WrapTableFunction` — порядок безопасен сегодня.** `SqlSourceRenderer.cs:328-329`
  дописывает ` with (...)` уже поверх wrapped-call; для встроенных CH TVF `WithClause` не задан, а SQL Server
  `openjson` обёртки не имеет (`WrapTableFunction` — no-op), поэтому комбинация «обёртка + WITH» недостижима.
  Край: пользовательская `[SqlTableFunction]` с именем `generateRandom` и `WithClause` дала бы
  `(select ...) with (...)` — невалидно; ср. ранее отмеченный край «обёртка по имени» в аудите `numbers`.
- **EOL нового файла.** Индекс нормализован в LF коммитом `746836a`; `DateConversionSqlTranslator.cs` —
  `i/lf`/`w/crlf` (autocrlf), новой EOL-находки нет. Находки 20/22/29 относятся к состоянию до `746836a`.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; в диффе `src`+`tests`
`#pragma warning disable`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустые `catch` — **0**;
`rg -c "for \(var \(i, cnt\) = \(0, args\.Count\)"` — **18** в **8** файлах; `git ls-files --eol` новых файлов —
`i/lf`. Тесты в этом проходе не перезапускались (только build).

## 🔎 Сводный аудит — слияние `todo-pg2`/`todo-mssql2`/`todo-ch2` в `1.0.3-alpha` (20.09.2026)

Область (дельта трёх merge'ей, база `970769a`, HEAD `8670bba`): PostgreSQL наборные TVF
(`Query/SqlFunctions{,.Postgres}.cs`, `PostgresDialect` — `SupportsTableFunction`/`WrapTableFunction`,
`Expressions/SelectExpression.cs` ветка `string[]`, `Visitors/JsonSqlTranslator.cs` ветка `jsonpath`),
MSSQL `Visitors/MemberTranslator.cs` (внутренний `TryTranslateProjectionOuterReference`),
ClickHouse `Query/SqlFunctions.ClickHouse.cs` (8 array-методов + `window_funnel`/`sequence_match`/`retention`),
`ISqlDialect`/`SqlDialectBase`/`ClickHouseDialect` (`SupportsSequenceAggregates`/`MakeSequenceAggregate`),
`Visitors/{ArraySqlTranslator,AdvancedAggregateTranslator}.cs`. Build Release — **0 warnings / 0 errors** (этот проход).
Юнит-наборы (Debug, 0 failed): core **166**, postgres **220**, sqlserver **199**, sqlite **223**, mysql **53**,
mariadb **20**, clickhouse **164**. Контейнерная интеграция на Podman-сокете (0 failed / 0 skipped):
общий PG-набор **213**, PG-specific **18**, ClickHouse **48**, общий SQL Server **213**, SQL-specific **15**,
MySQL **213**.

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В диффе `970769a..HEAD` (`src`+`tests`) новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`), `#pragma disable` — **5** (все с парным `restore`); неоправданных — **0/10**. `CS1591` остаётся в `<NoWarn>` 7 библиотечных `.csproj` (принято, Шаг 5 открыт). |
| Слоп-паттерны (slopwatch локально не установлен; скан вручную) | ✅ `Skip=` — **0**; `Task.Delay` — **2** (обе `Task.Delay(0)` как yield в `tests/nextorm.core.tests/InMemoryTests.cs:115,404`); `Thread.Sleep`/пустых `catch`/инлайновых `Version`/`VersionOverride` — **0**. |
| 1/3/4/6. `IDisposable`, LINQ, события, исключения | ✅ Новых disposable-полей/`using`, LINQ, подписок и `catch` нет; новые `throw` — `NotSupportedException` без `catch` (`AdvancedAggregateTranslator.cs:326,329`, `JsonSqlTranslator`-путь `jsonpath`). |
| 5. Проектирование | ℹ️ **Находка 30 актуализирована** (+4 emit-цикла `EmitSequenceAggregate`, ниже); `MemberTranslator.TryTranslateProjectionOuterReference` (`:433`) — новый `private static` (~45 строк), god-класс не расширяет; `PostgresDialect` — два строковых списка имён (ниже). |
| 7. Хэш-ключи | ✅ Новый код — чистый рендер (`MakeSequenceAggregate`, `WrapTableFunction`, `TryTranslateProjectionOuterReference`); `GetHashCode`/план-ключ не трогаются; `retention` возвращает `int[]` и материализуется только вложенно. |

### ℹ️ Находка 30 (актуализация 20.09.2026) — `EmitSequenceAggregate` добавляет 4 emit-цикла

`AdvancedAggregateTranslator.EmitSequenceAggregate` (`:318-365`) построчно повторяет форму
`SqlOperandTranslator.EmitFunction`: param-режим → `NeedAliasForColumn` → `Append` + циклы
`VisitToString`. Узкий счётчик `for (var (i, cnt) = (0, args.Count))` — по-прежнему **18** в **8** файлах
(не изменился), но новый метод добавляет **4** цикла того же семейства:
`(0, parameters.Count)` — `:333,351`, `(0, conditionArray.Expressions.Count)` — `:339,361`.
Отдельной находки не заводится — это тот же класс, что Находки 15/30.

### ℹ️ Наблюдения (фикс не требуется)

- **`SupportsSequenceAggregates` абстрактный при DIM-рендере.** `ISqlDialect.cs:416` не имеет дефолта,
  `MakeSequenceAggregate` (`:425`) — DIM. Асимметрия source-совместимости вынесена в
  `API-NAMING-REVIEW.md` как **SQ-DIM** (P2); здесь не дублируется.
- **Два строковых списка имён в `PostgresDialect`.** `SupportsTableFunction` (`:47`, 11 имён) и
  `WrapTableFunction` (`:63`, 6 имён) должны поддерживаться в синхроне. Расхождение сейчас осознанное
  (обёртка только у одно-колоночных функций, где колонка названа по функции: `generate_series`, `unnest`,
  `regexp_matches`, `regexp_split_to_table`, `jsonb_object_keys`, `jsonb_path_query`), но при добавлении
  имени только в один список появится либо необёрнутый single-column TVF, либо лишняя обёртка. ℹ️.
- **`string[]`-ветка row reader — точечная, не общая материализация массивов.** `SelectExpression.cs:117`
  повторяет образец `byte[]` и нужна только для PG `regexp_matches` (`text[]`); заблокированный пункт
  «row reader `Array(T)`/`Tuple`» не открывается (см. точечный аудит PG-setof выше).
- **MSSQL `TryTranslateProjectionOuterReference` — только `internal`/`private`.** `MemberTranslator.cs:433`;
  новых подавлений, LINQ, событий и disposable-полей нет; рефлексия не добавлялась, ветка `VisitMember`
  (`:243`) вставлена после существующих.
- **Новых LF-only `.cs`-файлов нет** (дифф добавляет только `M`-файлы); Находки 20/22/29 не расширяются.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (этот проход); в диффе
`src`+`tests` новых `#pragma warning disable`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/
пустых `catch` — **0**; узкий счётчик `rg -c "for \(var \(i, cnt\) = \(0, args\.Count\)"` — **18** в
**8** файлах (без изменений, +4 цикла `parameters.Count`/`conditionArray.Expressions.Count` в
`EmitSequenceAggregate`); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт, см. `API-NAMING-REVIEW.md`).

## 🔎 Сводный аудит Batch 3 — именованные окна / XML-методы / ClickHouse `multi_if` (20.09.2026)

Область (три патча Batch 3 поверх `8670bba`, наложены без коммита): PostgreSQL/ANSI именованные окна и
`GROUPS`/`EXCLUDE` (`Query/WindowDefinition.cs`, `Query/WindowFunctions.cs`, `Builders/EntityBuilder.cs`,
`Query/QueryCommand*`, `DataContext/SqlBuilder.cs`, `Visitors/WindowSql.cs`), SQL Server XML
`value`/`query`/`exist` (`Visitors/XmlSqlTranslator.cs`), ClickHouse `multiIf` + `lagInFrame`/`leadInFrame`
(`Visitors/BuiltinFunctionTranslator.cs`, `Visitors/{WindowSql,WindowFunctionTranslator,NormSqlTranslator}.cs`).
Build Release — **0 warnings / 0 errors** (этот проход). Публичная поверхность и находки — в
`API-NAMING-REVIEW.md` («Сводный аудит Batch 3…»; `NW1`/`X1`/`MF1` свёрнуты в RD2 (batch 3), `NW2` — naming).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/`using`/`IAsyncDisposable` нет; окна/XML/`multiIf` — чистый рендер в `StringBuilder`/`LambdaExpression`. |
| 2. Подавления | ✅ В диффе `8670bba..рабочее дерево` (`src`+`tests`) новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: `SuppressMessage` — **5** (все с `Justification`), `#pragma disable` — **5** (все с парным `restore`); неоправданных — **0/10**. `CS1591` остаётся в `<NoWarn>` 7 библиотечных `.csproj` (принято, Шаг 5). |
| 3. LINQ на горячем пути | ✅ Новый код — индексированные `for`-циклы (`MakeNamedWindow` `SqlBuilder.cs:529-607`, `WindowsEqual` `QueryPlanEqualityComparer.cs:163-199`, `PrepareWindows` `QueryCommand.QueryPreparer.cs:555-596`, `FlattenMultiIfBranches`); новых `.Select/.Where/.ToList` в рантайме нет. |
| 4/6. События, исключения | ✅ Новых подписок/`catch` нет; новые `throw` — `NotSupportedException`/`ArgumentException` без перехвата (`WindowFunctionTranslator.cs:87-101`, `XmlSqlTranslator.cs:41-68`, `BuiltinFunctionTranslator.cs:50,181,222-256`). |
| 5. Проектирование | ℹ️ Ниже — stringly-typed диспетчеризация `Over`-перегрузок и два метода близко к порогу «длинный»; god-классы не расширены (`BuiltinFunctionTranslator` 489 строк, `WindowFunctionTranslator` 217). |
| 7. Хэш-ключи | ✅ `WindowsPlanHash` (`QueryCommand.cs:51`) считается по имени/партиции/порядку/рамке+`Exclusion` (`QueryCommand.QueryPreparer.cs:555-596`), `WindowsEqual` — зеркально; `CopyTo` переносит `_windows`/`WindowsPlanHash` (`QueryCommand.Clone.cs:62,30`). Расхождения ключа и рендера нет. |
| Слоп-паттерны (slopwatch локально не установлен; скан вручную) | ✅ `Skip=`/`Ignore` — **0**; `Task.Delay` — **2** (обе `Task.Delay(0)` как yield в `tests/nextorm.core.tests/InMemoryTests.cs:115,404`, не задержки); `Thread.Sleep` — **0**; пустых `catch` — **0**; инлайновых `Version`/`VersionOverride` — **0**. |

### ℹ️ Наблюдения (фикс не требуется)

- **`WindowSql.SplitWindowArguments`/`AddWindowOrder` диспетчеризуют по имени параметра/метода.**
  `Over` выбирает слоты по `parameters[i].Name` (`"windowName"`/`"partitionBy"`/`"orderBy"`) и типу
  (`WindowFrame`), а `AddWindowOrder` распознаёт `CommonFunctions.asc`/`desc` по
  `DeclaringType`+`Method.Name` (`Visitors/WindowSql.cs:102-171`). Это осознанно (перегрузки отличаются
  составом слотов) и покрыто SQL-gen тестами; остаточная хрупкость — при переименовании параметра маппинг
  сломается молча. Отдельной находки не заводится (ср. уже принятый name-based диспетчинг DSL).
- **Два метода Batch 3 близко к порогу «длинный метод» (>30 строк).**
  `BuiltinFunctionTranslator.FlattenMultiIfBranches` (`:219-259`, ~41 строка) и `EmitMultiIf` (`:178-211`,
  ~34 строки); `WindowFunctionTranslator` вырос, но остаётся < 500 строк. Уже разобранные god-классы не
  расширяются; отдельной находки нет (порог эвристический, методы линейны).
- **`MultiIfBranch<T>` — пустой публичный маркер с приватным ctor.** Осознанный непрозрачный носитель
  для `params`-дерева выражений (ср. `WindowFunction<T>`, `WindowOrder`); XML-doc есть, самостоятельного
  смысла не имеет, голый `when`/`otherwise` отвергается транслятором с понятным сообщением.
- **`xml_*` в param-режиме обходит только операнд.** `XmlSqlTranslator.TryTranslate` при
  `visitor.IsParamMode` посещает `args[0]` и не читает литеральные xpath/тип
  (`Visitors/XmlSqlTranslator.cs:52-56`) — корректно, т.к. остальные аргументы по контракту константы
  (проверяются в SQL-проходе, `:64`), а сервер их не параметризует.
- **Новых LF-only `.cs`-файлов нет**; Находки 20/22/29 не расширяются. (В начале аудита в рабочем
  дереве присутствовал untracked `tests/nextorm.postgres.tests/TempCteProbe.cs`; к концу прохода файл
  уже отсутствовал — в находки не заводится.)

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (Debug, 0 failed):
core **174**, postgres **232**, sqlserver **207**, sqlite **226**, mysql **57**, mariadb **24**,
clickhouse **175**; контейнерная интеграция — PG-specific **21/0**, общий PG **213/0** (6 skip),
ClickHouse **50/0**, SQL-specific **16/0**, `DialectCapabilityContractTests` **8/0**, MySQL **213/0**
(7 skip); в диффе `src`+`tests` новых `#pragma warning disable`/`SuppressMessage`/`NoWarn`/`Skip=`/
`Task.Delay`/`Thread.Sleep`/пустых `catch` — **0**; `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5
открыт, см. `API-NAMING-REVIEW.md`).

## 🔎 Точечный аудит — SQL Server `PIVOT`/`UNPIVOT` (20.09.2026)

Область (uncommitted поверх `8670bba`; план фичи, пункты выполненного бэклога Phase 2):
новый источник `PivotExpression` (`Query/PivotExpression.cs` — новый файл),
поле/ctor `Expressions/FromExpression.cs`, `Builders/EntityBuilder.cs` (`Pivot`/`Unpivot`/`ResolvePivotInner`),
`DataContext/SqlSourceRenderer.cs` (`MakePivot`/`RenderPivotColumn`), `Query/QueryCommand.QueryPreparer.cs`
(`PrepareFrom`), `Expressions/FromExpressionPlanEqualityComparer.cs` (`PivotEquals`/`PivotHash`/`ColumnsEqual`/
`ValuesEqual`), `DataContext/InMemoryQueryBuilder.cs`/`InMemoryJoin.cs` (отказ) и диалектные хуки
(`DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, `src/nextorm.sqlserver/SqlServerDialect.cs`).
Build Release — **0 warnings / 0 errors** (этот проход). Публичная поверхность и нейминг — в
`API-NAMING-REVIEW.md` («SQL Server нативный `PIVOT`/`UNPIVOT`…»).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/`using`/`IAsyncDisposable` нет; `MakePivot`/`RenderPivotColumn` работают с `BaseExpressionVisitor` в `using` и `StringBuilder` диалекта. |
| 2. Подавления | ✅ В диффе (`src`+`tests`, включая новый `PivotExpression.cs`) новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**. База не менялась: `SuppressMessage` — **5** (все с `Justification`), `#pragma warning disable` — **5** (все с парным `restore`); неоправданных — **0/10**. Отношение «подавлено/обосновано» — 10/10 (подавления только легитимные). |
| 3. LINQ на горячем пути | ✅ `PivotEquals`/`PivotHash` — индексированные `for`, без LINQ; `MakePivot`/`RenderPivotColumn` — рендер. Новых `.Select/.Where/.ToList` в рантайме нет. |
| 4/6. События, исключения | ✅ Новых подписок/`catch` нет; новые `throw` — `NotSupportedException`/`ArgumentException` без перехвата (`EntityBuilder.cs:627,652,666`, `SqlSourceRenderer.cs:367`). |
| 5. Проектирование | ℹ️ Новых god-классов нет (`MakePivot` — 28 строк). Найдена одна связная логическая ошибка (Находка 31) и наблюдение по валидации. |
| 7. Хэш-ключи | ✅ `PivotHash` (`FromExpressionPlanEqualityComparer.cs:95`) сворачивает `IsUnpivot`+`Inner`+агрегат/лямбды/значения (или value/name/колонки), `PivotEquals` — зеркально (`:61`); `Values`/`Columns` сравниваются поэлементно (`:79,87`). План-ключ и рендер согласованы, дублирования нет. |
| Слоп-паттерны (slopwatch локально не установлен; скан вручную) | ✅ `Skip=`/`Ignore` — **0**; `Thread.Sleep` — **0**; пустых `catch` — **0**; инлайновых `Version`/`VersionOverride` — **0**; новые `Task.Delay` — **0** (базовые 2 — `Task.Delay(0)` yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`). |

### ✅ Находка 31 — `Pivot`/`Unpivot` молча теряли уже выставленные модификаторы запроса (ИСПРАВЛЕНА 21.09.2026, была 🔴)

**Место:** `src/nextorm.core/Builders/EntityBuilder.cs:630` и `:655` (создание нового `EntityBuilder<TableAlias>`), `:665` (`ResolvePivotInner`).

**Было (что делает код):** `Pivot`/`Unpivot` возвращают **новый** builder
`new EntityBuilder<TableAlias>(_dataProvider) { Logger = Logger, SourceFrom = … }` и переносят только
`Logger` + `FromExpression`. `ResolvePivotInner` отклоняет лишь `_query`/`_joins`/`_condition`/`_having`/`_group`,
а поля `_tablesample` (set `:584`), `_temporal` (`:599`), `_rowLock` (`:696`), `Paging` (`:703`), `_sorting`
(`:1104`), `_preWhere` (`:319`), `_arrayJoins` (`:355`), `_settings`, `_limitBy` (`:756`), `_distinctOn` (`:515`),
`_windows` (`:552`) **не проверяются и не переносятся**, хотя `CopyProjectionIndependentStateTo`
(`:785-810`) умеет их копировать (как это делает `Clone`/`Join`).

**Стало (последствие):** `ctx.From<Sales>().TableSample(10).Pivot(...)`, `.ForSystemTime(...).Pivot(...)`,
`.ForUpdate().Pivot(...)`, `.Limit(10).Pivot(...)` возвращают валидный SQL **без** запрошенного модификатора —
молчаливая потеря данных/выборки/блокировки без ошибки. Это расходится с явным отказом для
`Where`/`Join`/`Group` (тот же `ResolvePivotInner`, сообщение «apply filters, joins and grouping to the
reshaped result»): часть неподдерживаемых модификаторов отвергается, часть — молча теряется.

**Рекомендация (fix now):** либо расширить guard в `ResolvePivotInner` на **все** непустые поля (по образцу
`_condition`), возвращая `NotSupportedException` с понятным текстом, либо переносить projection-независимое
состояние через `CopyProjectionIndependentStateTo`. Семантически `TableSample`/`Temporal` применяются к
внутреннему источнику, поэтому для них перенос на результат неверен — им нужен явный отказ; `Limit`/`OrderBy`/
`RowLock`/`Settings` можно переносить на результат. Минимум — не терять молча. Добавить тест на отказ
(напр. `.TableSample(10).Pivot(...)` / `.Limit(1).Unpivot(...)`).

**Стало (21.09.2026).** Guard в `ResolvePivotInner` (`EntityBuilder.cs:670-677`) расширен на **все** переносимые
модификаторы: `_joins`/`_condition`/`_having`/`_group`/`_sorting`/`Paging`/`IsDistinct`/`_tablesample`/`_temporal`/
`_rowLock`/`_preWhere`/`_arrayJoins`/`_settings`/`_limitBy`/`_distinctOn`/`_windows`/`IsFinal`/`SampleRatio`/
`TableHints`/`Ctes` — каждый бросает `NotSupportedException`, ни один не теряется молча. Покрытие:
`Pivot_ShouldThrowWhenSourceHasModifiers` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2398` — `Limit`,
`TableSample`, `OrderBy`, `WithTableHint`) и `Pivot_ShouldThrowWhenDerivedSourceHasModifiers` (`:2383` —
`OrderBy`/`Where` поверх производного источника). Косметика на будущее: guard проверяет `SampleRatio`, но не
`SampleOffset` (join-guard `HasNoNonWhereModifiers` проверяет `SampleOffset == 0`); недостижимо, т.к.
`Sample(ratio, offset)` всегда выставляет `SampleRatio` (`EntityBuilder.cs:260-276`).

### ℹ️ Наблюдения (фикс не требуется)

- **PIVOT-слоты агрегата/`FOR` не валидируются как «простая колонка».** Лямбды типизированы как
  `Expression<Func<TEntity, object?>>`, а `RenderPivotColumn` (`SqlSourceRenderer.cs:392-397`) пропускает их
  через обычный column-visitor, т.е. `s => s.Margin * 2` отрендерится как `sum(margin * 2)`. Документация
  T-SQL («FROM clause plus JOIN, APPLY, PIVOT») определяет слоты как *column* (`<aggregate_function>(<value_column>)`,
  `FOR <pivot_column>`), поэтому вычисляемое выражение сервер может отвергнуть. Валидации «member-access»
  и теста на составное выражение нет. Отдельной находки не заводится (граница «caller responsibility», как
  у аргументов `[SqlTableFunction]`), но дешёвая проверка в `Pivot`/`Unpivot` была бы полезна.
- **`PivotValue`/`UnpivotColumn` — классы без value-equality.** `PivotValue.Create("1") != PivotValue.Create("1")`
  по ссылке, но план-ключ сравнивает по полю (`ValuesEqual`/`ColumnsEqual`), поэтому расхождения кэша нет.
- **Новых LF-only `.cs` нет**; Находки 20/22/29 не расширяются. `PivotExpression.cs` — новый untracked файл.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; unit (по данным задачи,
0 failed): core **175**, postgres **235**, sqlserver **214**, sqlite **230**, mysql **58**, mariadb **25**,
clickhouse **176**; интеграция SQL Server — `SqlServerSpecificTests` **18/0** (в т.ч. `Pivot_ShouldReshapeRows`,
`Unpivot_ShouldStackColumns`), `DialectCapabilityContractTests` **10/0**; покрытие line **84.8%** / branch
**73.8%** (база 84.7% / 73.6%). `rg` по диффу: новых `#pragma warning disable`/`SuppressMessage`/`NoWarn`/
`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` — **0**; `rg --files -g 'PublicAPI*.txt'` — пусто
(Шаг 5 открыт, см. `API-NAMING-REVIEW.md`).

## 🔎 Точечный аудит — capability-объекты диалекта (Фаза 1) и `PreparedHaving` (20.09.2026)

**Область:** `DataContext/Dialect/DialectCapabilities.cs` (новый), `DataContext/Dialect/ISqlDialect.cs`,
`SqlDialectBase.cs`, шесть
`*Dialect.cs`; `Query/QueryCommand.cs`, `QueryCommand.Clone.cs`, `QueryCommand.QueryPreparer.cs`,
`DataContext/SqlBuilder.cs`; тесты `DialectCapabilityContractTests.cs`, `CorrelatedQueryTests.cs`,
`CommonTestSuite.CorrelatedQuery.cs`. Build Release **0/0**. `slopwatch` локальным tool'ом не
установлен — паттерн-скан вручную.

| Категория навыка | Статус |
|---|---|
| 1. `IDisposable` | ✅ Рендереры — `internal sealed` синглтоны (`static readonly Instance`), полей/ресурсов нет, `IDisposable` не нужен; `CorrelatedQueryExpressionVisitor.OuterScope` — пре-существующий `IDisposable` в `using`-паттерне. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` в диффе — **0**. База: `SuppressMessage` — **5** (все с `Justification`), `#pragma warning disable` — **5** (все с парным `restore`), соотношение не менялось. |
| 3. LINQ | ✅ Не затронуто (LINQ-цепочек в диффе нет). |
| 4. События | ✅ Не затронуто (подписок нет). |
| 5. Проектирование | ✅ God-классы не расширены (`PrepareGrouping` +~10 строк; `DialectCapabilities.cs` — 63 строки на 4 интерфейса); длинных списков параметров ≥6 нет. ℹ️ ниже — `SubqueryDetector` удалён, визит HAVING стал безусловным. |
| 6. Исключения | ✅ Новые `throw new NotSupportedException(...)` — гейты capability с именем функции/провайдера; не пустой `catch`, не `catch (Exception)`. |
| 7. Хэш-ключи | 🟡 **Находка 32** — хэш сворачивает `PreparedHaving`, а `Equals` сравнивает исходный `Having` (ниже). |

### ✅ Находка 32 — `Equals` и `GetHashCode` план-ключа используют разные представления `HAVING` (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место:** `Query/QueryCommand.QueryPreparer.cs:694,698`, `Query/QueryPlanEqualityComparer.cs:130,343-344`,
`DataContext/SqlBuilder.cs:247`.

**Было:** `groupingPlanHash` сворачивал хэш исходного `cmd._having`, а `Equals` сравнивал
`x.Having`/`y.Having` — обе стороны план-ключа использовали одну и ту же лямбду.

**Стало:** `PrepareGrouping` прогоняет `CorrelatedQueryExpressionVisitor` над `_having` и сворачивает
в `groupingPlanHash` хэш `cmd.PreparedHaving`; `QueryPlanEqualityComparer.Equals` по-прежнему сравнивает
исходный `x.Having` (`:130`), а `GetHashCode` — через `GroupingPlanHash` (`:343-344`). `SqlBuilder`
рендерит `PreparedHaving ?? Having` (`:247`). Итог: **равенство ключа строится по одному представлению,
хэш — по другому**.

**Оценка контракта `Equals ⇒ same hash`:** сейчас он **держится**, потому что `PreparedHaving`
детерминированно выводится из `Having` и уже зарегистрированных внешних ссылок команды, а `Equals`
дополнительно сравнивает `PreparedCondition` (`:117`) и `OuterReferences` (`:144`) — у равных команд
совпадает и результат визитора. Но инвариант не зафиксирован конструкцией: любая недетерминированность
или иная нормализация в `CorrelatedQueryExpressionVisitor` даст Equal-команды с разными хэшами. Для
`QueryPlan` (ключ словаря, `Cache/QueryPlan.cs:43,54`) это промах/дубль плана, а не неверный результат:
при совпавшем хэше `Equals` разводит ключи.

**Фикс:** обе стороны — по одному представлению: в `Equals` сравнивать `x.PreparedHaving ?? x.Having`
с `y.PreparedHaving ?? y.Having` (хэш уже по `PreparedHaving`), либо дополнительно сворачивать в хэш и
исходный `Having`. Регресс-тест: две структурно одинаковые grouped+HAVING команды дают одну запись
план-кэша / равные `QueryPlan.GetHashCode()`.

**Стало (21.09.2026):** обе стороны — по исходной лямбде. `PrepareGrouping`
(`QueryCommand.QueryPreparer.cs:729`) сворачивает в `groupingPlanHash` хэш `cmd._having` (сырой
`HAVING`), а `QueryPlanEqualityComparer.Equals` сравнивает `x.Having`/`y.Having`
(`QueryPlanEqualityComparer.cs:130`). `PreparedHaving` остаётся только представлением для рендера
(`SqlBuilder` → `PreparedHaving ?? Having`) и в план-ключ не входит; рассинхрон снят. Проверка:
grouped+HAVING SQL-gen/интеграционные тесты (`CorrelatedQueryTests.CorrelatedSubqueryInHaving_ShouldReferenceOuterAlias`,
`SubqueryInHaving_ShouldRenderScalarSubquery`) зелёные.

### ℹ️ Наблюдения (фикс не требуется)

- **Аллокации / hot-path.** Синглтоны рендереров статичны, без полей и без `IDisposable`; на вызов —
  только интерполяция/`string.Join(", ", columns)` (как в прежних `Make*`). `CorrelatedQueryExpressionVisitor`
  над `_having` создаётся один раз на `PrepareCommand` (результат кэшируется), не на исполнение; раньше
  визит оплачивался лишь при подзапросе в HAVING (`SubqueryDetector`), теперь — для любого HAVING.
  Не горячий путь.
- **Граница in-memory для HAVING.** `InMemoryGrouping.cs:139` читает исходный `Having`, не
  `PreparedHaving`; коррелированный HAVING по-прежнему отсекается guard'ом `OuterReferences`
  (`InMemoryQueryBuilder.cs:40-42`, внятный `NotSupportedException`), поэтому тихо неверных данных нет.
  Но не-коррелированный подзапрос в HAVING теперь минует прежний общий `ContainsSubquery`-guard и
  доходит до in-memory без старого сообщения — качество диагностики может просесть. Сверить с
  `docs/guide/06-subqueries.md` (+RU, in-memory поддерживает коррелированный scalar/`EXISTS`/`IN`
  глубины один с 22.09.2026, см. Находки 78–81); полезен тест на сообщение in-memory.
- **EOL.** Новый `DialectCapabilities.cs` — CRLF (63/63), новые/изменённые тесты — CRLF; Находки 20/22/29
  не расширяются.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `rg` по диффу — 0 новых
подавлений/`Skip=`/задержек/пустых `catch` (`Task.Delay` — 2, обе прежние `Task.Delay(0)` в
`tests/nextorm.core.tests/InMemoryTests.cs:125,414`); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5
открыт, см. `API-NAMING-REVIEW.md`, DC3); Приложение A (45) без изменений.

## 🔎 Точечный аудит — агрегатные терминалы внутри коррелированного подзапроса (21.09.2026)

**Область:** `Visitors/CorrelatedQueryExpressionVisitor.cs` — ветка агрегатных терминалов
(`Count`/`Min`/`Max`/`Avg`/`Sum`/`Stdev`/`Stdevp`/`Var`/`Varp`) больше не бросает
`NotSupportedException`, а переписывается в `builderReceiver.Select(<агрегат>).First()`; после выноса
21.09.2026: `ReplaceAggregateTerminal` (`:357-363`) + новый `internal`
`Visitors/AggregateTerminalRewriter.cs` (`CountMI` `:14`, `Rewrite` `:21-45`, `UnwrapLambda` `:48-53`,
`AggregateMethodFor` `:55-66`, `SelectMethodFor` `:68-83`);
тесты `tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs:224,242,269`,
`tests/nextorm.postgres.tests/SqlGenerationTests.cs:2752`,
`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:1930`,
`tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs:280`. Build Release **0/0**;
`slopwatch` локальным tool'ом не установлен — паттерн-скан вручную.

| Категория навыка | Статус |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых ресурсов/полей-`IDisposable` нет; `OuterScope` — пре-существующий. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` в диффе — **0**. База не менялась: `SuppressMessage` — **5** (все с `Justification`), `#pragma warning disable` — **5** (все с парным `restore`). |
| 3. LINQ | ℹ️ На пути **подготовки** появляется `GetMethods(...).SingleOrDefault(...)` (`SelectMethodFor`), не на горячем per-row пути. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | 🟡 **Находка 33** (порог god-class; вынос применён, класс всё ещё > 500 — открыта), 🟡 **Находка 34** (DRY — открыта), ✅ **Находка 35** (закрыта 21.09.2026: Stdev/Var покрыты; остаток Stdevp/Varp принят). Длинных списков параметров нет. |
| 6. Исключения | ✅ Новые `throw new NotSupportedException(...)` — доменные гейты с текстом; пустых/`catch (Exception)` нет. |
| 7. Хэш-ключи | ✅ `ExpressionsCache` для rewrite безопасен: тела с `OuterRefMarker` не кэшируются (`GetQueryCommand:482-486`). |

### 🟡 Находка 33 — `CorrelatedQueryExpressionVisitor` превышает порог god-class (ОТКРЫТА, P2)

**Место:** `Visitors/CorrelatedQueryExpressionVisitor.cs:11` (объявление), `:357-363` (glue
`ReplaceAggregateTerminal`); вынесенная ось — `Visitors/AggregateTerminalRewriter.cs`.

**Было (HEAD):** 596 строк файла; тело класса (строки 11–596) = 586, из них вложенные типы
`OuterScope` (11) + `OuterReferenceDetector` (13) + `OuterRefMarkerDetector` (12) = 36, т.е. **550** собственных строк — уже > 500, хотя в таблице Находки 4 отмечено
«Все прочие — < 500» (неточность реестра).

**Стало (после выноса 21.09.2026):** 608 строк файла; тело класса = 598, собственных строк **562**.
Rewrite вынесен в `Visitors/AggregateTerminalRewriter.cs` (`internal static`, 83 строки); до выноса было
669 строк / ≈635 собственных (вынос снял ≈73, но под порог 500 класс не вывел — +12 к HEAD, glue
`ReplaceAggregateTerminal`).

**Оценка:** порог раздела 5 навыка — > 500 строк на класс (Находка 4), и он превышен (550 уже на HEAD,
562 после выноса). Вынос rewrite снял вклад изменения, но класс остаётся god-class — переполнение
пре-существующее. Также требует правки утверждение «Все прочие — < 500».

**Фикс (выполнено 21.09.2026: `AggregateTerminalRewriter`).** Остаток — вынести следующую ось
(`GetQueryCommand`/`ReplaceQueryCommand`, `IsOrDefaultScalar`, `ContainsOuterRefMarker` +
`OuterRefMarkerDetector`, `IsAggregateTerminal`) в `internal`-коллаборатор; либо зафиксировать класс
осознанным исключением рядом с `ExpressionPlanEqualityComparer`.

### 🟡 Находка 34 — дублирование построения агрегатного `Select` (ОТКРЫТА, P2)

**Место:** `Visitors/AggregateTerminalRewriter.cs:21-45` (`Rewrite`) против
`Builders/EntityBuilderExtensions.cs:277-282` (`CountCore`) и `:284-289` (`AggregateCore`).

**Проблема:** не-Count ветка повторяет тело `AggregateCore` строка-в-строку
(`Expression.Call(CommonFunctions.SQLExpression, sqlMethod.MakeGenericMethod(typeof(TResult)), exp.Body)`),
Count-ветка — тело `CountCore`; маппинг «имя семейства → `*MI`» (`AggregateMethodFor`,
`AggregateTerminalRewriter.cs:55-66`) дублирует восемь публичных forwarder'ов `Min`/`Max`/…
(`EntityBuilderExtensions.cs:191-245`). Новое семейство агрегата придётся заводить в 2–3 местах.

**Фикс:** единый `internal static` хелпер в `EntityBuilderExtensions` (например,
`BuildAggregateSelector<TEntity,TResult>(MethodInfo, Expression<Func<TEntity,TResult>>)` и
`BuildCountSelector<TEntity>()`), вызываемый и из `AggregateCore`/`CountCore`, и из rewriter'а.

### ✅ Находка 35 — rewrite: статистические агрегаты покрыты (ЗАКРЫТА частично, P2)

**Место:** `Visitors/CorrelatedQueryExpressionVisitor.cs:338-350` (`IsAggregateTerminal`),
`Visitors/AggregateTerminalRewriter.cs:55-66` (`AggregateMethodFor`); тесты
`tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs:224,242,269`.

**Проблема:** заявлено 9 семейств, а коррелированный rewrite покрыт только `Count`/`Sum`/`Min`/`Max`/`Avg`
(sqlite) и `Count` (Postgres/SQL Server). Ветки `Stdev`/`Stdevp`/`Var`/`Varp` в rewrite не исполняются
ни одним тестом (standalone-терминалы покрыты в `CommonTestSuite.Aggregates.cs`, но это другой путь).
Ошибочные пути `SelectMethodFor` (`.Single`) и `UnwrapLambda` (аргумент не-lambda) тоже не покрыты.

**Фикс:** добавить SQL-gen тесты на `Stdev`/`Var` в коррелированном подзапросе для провайдера со
статистическими агрегатами (PostgreSQL/SQL Server; SQLite гейтится по capability).

**Стало (21.09.2026):** добавлен `tests/nextorm.postgres.tests/SqlGenerationTests.cs:2769`
`CorrelatedStatisticalAggregateTerminalInSelect_ShouldRenderAggregateSubquery` — `Stdev`→`stddev(`,
`Var`→`variance(`; postgres **237/0**. Остаток (`Stdevp`/`Varp` и ошибочные пути
`SelectMethodFor`/`UnwrapLambda`) принят как механический вариант тех же веток — отдельный тест не
требуется; при желании `Stdevp`/`Varp` добавляются в тот же тест.

### ♻️ Обновление 21.09.2026 — применённые фиксы

- **Находка 33 — не закрыта.** Rewrite вынесен в `Visitors/AggregateTerminalRewriter.cs` (`internal static`,
  83 строки: `CountMI`, `Rewrite`, `UnwrapLambda`, `AggregateMethodFor`, `SelectMethodFor`). Визитор
  669→**608** строк (собственных ≈635→**562**); класс по-прежнему > 500, поэтому Находка остаётся
  открытой — пре-существующее переполнение (550 уже на HEAD) закрывается только выносом следующей оси.
- **Находка 35 — закрыта частично.** Postgres-тест `:2769` покрывает `Stdev`/`Var`; `Stdevp`/`Varp` и
  ошибочные пути — принятый остаток. postgres **237/0**.
- **Находка 34 — открыта.** Логика переехала в `AggregateTerminalRewriter`, но дублирование с
  `EntityBuilderExtensions.CountCore`/`AggregateCore` и дублирование «имя → `*MI`» сохранились.
- ℹ️ **Новая косметическая деталь.** `AggregateTerminalRewriter.CountMI` (`AggregateTerminalRewriter.cs:14`) —
  PascalCase-поле, но новый класс **не несёт** `[SuppressMessage("Style","IDE1006")]`, который был у
  `CorrelatedQueryExpressionVisitor` (`:9`) и `SelectExpression` (`:7`). Сборка 0/0 (IDE1006 — suggestion,
  не гейтится); при включении `EnforceCodeStyleInBuild`/`AnalysisLevel=latest-all` (Шаг 5) поле может
  пометиться. Фикс не обязателен; при переносе подавления обновить `Justification` (список примеров).

### ℹ️ Наблюдения (фикс не требуется)

- **Рефлексия безопасна; рекомендация выполнена 21.09.2026.** `AggregateTerminalRewriter.SelectMethodFor`
  (`AggregateTerminalRewriter.cs:68-83`) не может не найти метод для достижимого получателя:
  `IsEntityBuilder` (`CorrelatedQueryExpressionVisitor.cs:317`) сравнивает `GetGenericTypeDefinition()`
  с `EntityBuilder<>`, а определения наследников (`JoinedEntityBuilder<,>`) в `EntityBuilder<>` **не**
  конвертируются (проверено: `typeof(JoinedEntityBuilder<int,string>)` → `IsEntityBuilder == false`).
  Значит `builderReceiver.Type` — всегда ровно `EntityBuilder<TEntity>`, у которого `Select<TResult>`
  один; на случай будущего второго `Select` теперь `SingleOrDefault` + осмысленный
  `NotSupportedException` с именем типа (было бы `InvalidOperationException` без контекста).
- **`Expression.Quote`/`MakeGenericMethod`/`NewArrayInit` безопасны (проверено экспериментом на .NET 10).**
  `T?` у unconstrained generic-параметра стирается в `T`: `sum<T>(T?)` при `T=decimal` имеет параметр
  `decimal`, `T=decimal?` — `Nullable<decimal>`, `T=string` — `string`; `selector.ReturnType` совпадает
  с типом `selector.Body` без nullable-lift. `Expression.Quote(Lambda)` даёт
  `Expression<Func<TEntity,resultType>>` — ровно тип параметра `Select`. `NewArrayInit(typeof(object))`
  (пустой массив) совпадает с `count(params object?[])`. `InvalidOperationException`/`ArgumentException`
  не ожидаются.
- **Перегрузки с `params ReadOnlySpan<object?>` недостижимы внутри дерева выражений.** Компилятор
  запрещает (`CS8640`, `CS9226`), поэтому `node.Arguments` агрегатного терминала в дереве — это
  `[builder, lambda]` (или `[builder]` для `Count()`); «молчаливое отбрасывание `@params`» в
  `Rewrite` — невозможный сценарий, не находка.
- **`IsAggregateTerminal` не мёртв** — используется как диспетчер в `:228`; все новые хелперы и `CountMI`
  имеют вызовы; неиспользуемого кода нет.
- **XML-документация.** `ReplaceAggregateTerminal`, `IsAggregateTerminal`, класс `AggregateTerminalRewriter`
  и `Rewrite` документированы; `UnwrapLambda`/`AggregateMethodFor`/`SelectMethodFor` — без `<summary>`;
  члены `private` (или `internal`-тип), `CS1591` не задействован (и вообще в `<NoWarn>` всех 7 библиотек),
  на Приложение A (`API-NAMING-REVIEW.md`) не влияет. Косметика.
- **Аллокации на пути подготовки.** `GetMethods(...)` (массив) + LINQ `.SingleOrDefault` + сборка `Expression`
  выполняются при `PrepareCommand` (промах план-кэша), не на строку результата; при желании кэшируются
  `static readonly` по закрытому типу builder'а. Не горячий путь.
- **`ExpressionsCache`.** `GetQueryCommand` не кэширует тела с `OuterRefMarker` (`:482-486`) и компилирует
  их на каждый внешний command; не-коррелированное тело без параметров компилируется напрямую. Тот же
  контракт, что у существующего скалярного пути; новый ключ/гонки не появляются.
- **Регресс golden SQL.** Новая ветка срабатывает только для имён, которые раньше падали
  `NotSupportedException`; неагрегатные терминалы идут прежним путём. Существующие golden-тесты не
  меняются.

**Проверка (обновлено 21.09.2026):** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
фулл-наборы: postgres **237/0**, sqlite **235/0**, sqlserver **216/0**, core **175/0** (в т.ч. новый
`CorrelatedStatisticalAggregateTerminalInSelect` — 1/1);
`rg` по диффу — 0 новых подавлений/`Skip=`/задержек/пустых `catch`; `rg --files -g 'PublicAPI*.txt'` —
пусто (Шаг 5 открыт); публичная поверхность не менялась (новые члены — `private`/на `internal`-типе).

## 🔎 Точечный аудит — глубина корреляции ≥ 2 и SQLite-гард `Single` (21.09.2026)

**Область:** `Query/QueryCommand.cs` (новые `OuterRegistry`/`RootRegistry`/`IsCardinalityGuardable`,
форвард `AddCommand`/`AddOuterReference`), `Query/QueryCommand.Clone.cs`, `Query/QueryCommand.QueryPreparer.cs`
(`WrapSingleScalarCardinalityGuard`, сайт гарда), `Visitors/CorrelatedQueryExpressionVisitor.cs` (снятие
`NotSupportedException` для глубины > 1, `GetQueryCommand` через `RootRegistry`, новый
`ReplaceParametersByValueVisitor`), `Query/ExpressionPlanEqualityComparer.cs` (`VisitIndex`),
`Visitors/MemberTranslator.cs` (гейт `Single`); тесты `tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs`,
`tests/nextorm.{postgres,sqlserver,clickhouse,mysql,mariadb,sqlite}.tests/SqlGenerationTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs`, `SqliteSpecificTests.cs`.
Build Release **0/0**; после фикса Находки 36 — sqlite **237/0** (см. «Проверка»);
`slopwatch` локальным tool'ом не установлен — паттерн-скан вручную.

| Категория навыка | Статус |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: `ReplaceParametersByValueVisitor` — stateless `ExpressionVisitor`, ресурсов/полей-`IDisposable` нет; `OuterScope` — пре-существующий `using`-паттерн. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` в диффе — **0**. База не менялась: `SuppressMessage` — **5** (все с `Justification`), `#pragma warning disable` — **5** (все с парным `restore`), неоправданных нет. |
| 3. LINQ | ✅ Не горячий путь: гард и инлайн выполняются при `PrepareCommand` (промах план-кэша); LINQ-цепочек по коллекциям в новых путях нет. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ **Находка 36** (вложенный некоррелированный подзапрос → StackOverflow; ИСПРАВЛЕНА 21.09.2026), 🟡 **Находка 37** (ложное срабатывание `Single`-гарда при `DISTINCT`/`GROUP BY`/`UNION`, P2, открыта), ℹ️ **Находка 38** (робастность `OuterRegistry`, P2, открыта). |
| 6. Исключения | ✅ Новых пустых/`catch (Exception)` нет; снятые `NotSupportedException` (глубина > 1, агрегатные терминалы) расширяют поддержку, SQLite-гард бросает `SqliteException` на уровне БД осознанно. |
| 7. Хэш-ключи | ✅ `ExpressionPlanEqualityComparer.VisitIndex` (`:620-628`) резолвит ссылки через `RootRegistry`, согласованно с рендером корреляционной цепочки; **Находка 32 закрыта** (см. выше). `ReferencedQueriesPlanHash` вложенных команд = 0, но ссылки входят в ключ инлайново (см. Наблюдения). |

### ✅ Находка 36 — вложенный некоррелированный подзапрос: локальная регистрация против рендера от корня → StackOverflow (ИСПРАВЛЕНА 21.09.2026, была P1-кандидат)

**Место:** `Query/QueryCommand.cs:410` (`AddCommand` форвардит только при `OuterRegistry != null`),
`Visitors/CorrelatedQueryExpressionVisitor.cs:453,483` (`OuterRegistry` ставится только на тело с
`OuterRefMarker`), `Query/QueryCommand.cs:448` (`AddOuterReference`, там же), рендер —
`Visitors/MemberTranslator.cs:479-494` + `DataContext/SqlBuilder.cs` (всегда резолвит `ReferencedQueries`
от корневой команды, `QueryProvider` не меняется при `MakeSelect(innerQuery)`).

**Было/воспроизведено (HEAD `8670bba`, worktree):**

```csharp
outer.Select(it => new {
    it.Id,
    x = mid.Where(s => s.Id > 0)
           .Select(s => inner.Where(c => c.Id == s.Id).Select(c => c.Id).First())
           .First()
})
```

Промежуточная команда `M` не несёт маркера (её ссылка `s` — собственный параметр), поэтому
`M.OuterRegistry == null` и `M.AddCommand(inner)` пишет в `M._referencedQueries`. Рендер при
`MakeSelect(M)` использует `QueryProvider` = корень `R`, читает `R.ReferencedQueries[0]` = `M` и снова
рендерит `M` → неограниченная рекурсия, **StackOverflow** (процесс, exit 134). Control depth-2
correlated (`mid.Where(s => s.Id == it.Id)...`) при этом рендерится корректно.

**Это не регресс фичи A.** На HEAD `8670bba` тот же сценарий падает так же (проверено worktree); фича A
снимает `NotSupportedException` для глубины > 1, но не покрывает некоррелированное вложение. Новый
`OuterRegistry`/`RootRegistry` — естественное место для фикса.

**Фикс (выполнено 21.09.2026).** `CorrelatedQueryExpressionVisitor.ReplaceQueryCommand` теперь ставит
`cmd.OuterRegistry ??= _queryProvider` для **каждой** команды-подзапроса
(`CorrelatedQueryExpressionVisitor.cs:529`), включая closure-путь `tv.Has2`/entity-builder, который
строит команду вне `GetQueryCommand`. В `GetQueryCommand` добавлен детектор
`ContainsSubqueryExpression`/`NestedSubqueryDetector` (`:394-415`): тело с вложенным подзапросом
компилируется инлайном захваченных значений (как коррелированный случай) и получает `OuterRegistry`
(`:476,510`) — иначе оно кэшировалось бы в `ExpressionsCache` с индексами, относительными чужого
реестра. `AddCommand`/`AddOuterReference` теперь форвардят в корень так же, как рендер.

**Проверка фикса (21.09.2026).** Прежний падающий сценарий
(`mid.Where(s => s.Id > 0).Select(s => inner.Where(c => c.Id == s.Id).Select(c => c.Id).First()).First()`)
рендерится без рекурсии (ранее StackOverflow, exit 134):
`(select (select t3.id from complex_entity as 't3' where t3.id = cast(t2.id as bigint) limit 1) from simple_entity as 't2' where (t2.id > 0) limit 1)`.
Регресс-тест `tests/nextorm.sqlite.tests/CorrelatedQueryTests.cs:310`
`NestedNonCorrelatedSubquery_ShouldResolveItsOwnInnerCommand` (точный SQL) — SQLite **1/1**; sqlite-набор
**237/0** (было 236). Control depth-2 correlated по-прежнему корректен.

### 🟡 Находка 37 — SQLite-гард `Single` считает `count(*)`, а не строки результата: ложное срабатывание при `DISTINCT`/`GROUP BY`/`UNION` (P2)

**Место:** `Query/QueryCommand.QueryPreparer.cs:231-241` (`WrapSingleScalarCardinalityGuard`), сайт
`:304-306`.

**Воспроизведено рендером (SQLite):** `inner.Select(s => s.Id).Distinct().Single()` →
`select distinct case when (count(*) > 1) then cast(abs(-9223372036854775808) as integer) else id end from simple_entity limit 2`;
`inner.GroupBy(s => new { s.Id }).Select(g => g.Id).Single()` → то же с `group by id`. `count(*)` считает
входные строки, а не строки результата: две строки с одним `id` дают `count(*)=2` и `integer overflow`,
хотя `Distinct`/`GROUP BY` дают одну строку и `Single` должен вернуть значение. `UNION` — тот же класс
(гард применяется к первому операнду и не видит объединение). Это не ложное срабатывание «в пользу
ошибки», а ложный отказ: корректный запрос падает.

**Фикс:** оборачивать только «простую» команду (`!IsDistinct`, `DistinctOn is null`, `GroupingList` пуст,
`Union is null`); иначе сохранять прежний `NotSupportedException` (или считать строки обёрткой
`select count(*) from (подзапрос)`). Тест: `Distinct`/`GroupBy` + `Single` на SQLite.

### ℹ️ Находка 38 — робастность `OuterRegistry`: нет защиты от цикла, форвард обходит `_isPrepared`, клон копирует ссылку (P2, фикс не обязателен)

- **Цикл.** `RootRegistry` (`QueryCommand.cs:362-371`) и `AddCommand` (`:410`) идут по цепочке
  `OuterRegistry` без множества посещённых. Текущими внутренними путями цикл не создаётся
  (`OuterRegistry` всегда указывает на уже построенного предка), поэтому бесконечного прохода нет; но
  инвариант «цепочка конечна и упирается в корень» ничем не зафиксирован. Фикс: документировать
  инвариант или visited-set.
- **DEBUG-гард.** `AddCommand` форвардит `owner.AddCommand(cmd)` **до** `#if DEBUG if (_isPrepared) throw`
  (`:410` против `:412-416`), поэтому защита от мутации подготовленной вложенной команды не срабатывает
  (остаётся защита корня). `AddOuterReference` (`:444-457`) гарда не имеет вовсе — как и раньше.
  DEBUG-only.
- **Клон.** `QueryCommand.Clone.cs:37` переносит `OuterRegistry` **ссылкой** на оригинального предка;
  `_referencedQueries` при этом клонируются (`:105-108`). Сейчас безопасно (клоны план-кэша не
  переподготавливаются и не вызывают `AddCommand`), но корень вложенной команды-клона указывает на живой
  оригинал — при любой будущей мутации клона это тихо запишет в чужой реестр. Фикс: ремапить
  `OuterRegistry` на клон предка либо зафиксировать «клоны не мутируются».

### ℹ️ Наблюдения (фикс не требуется)

- **`ReplaceParametersByValueVisitor` (`CorrelatedQueryExpressionVisitor.cs:394-407`) безопасен
  (проверено экспериментом на .NET 10).** Значения приходят из
  `ReplaceConstantsExpressionVisitor.GetConstantParameter` (`ExpressionExtensions.cs:129-133`), где тип
  `ParameterExpression` всегда равен статическому типу `ConstantExpression`, поэтому
  `Expression.Constant(value, node.Type)` — точный round-trip. `null` возможен только для ссылочных и
  `Nullable<>` типов; боксованный underlying в `Nullable<T>` принимается
  (`typeof(int?).IsAssignableFrom(typeof(int)) == true`, проверено). Пропуск `ExpressionsCache` обоснован:
  индексы маркеров указывают в корневой реестр, а инлайн восстанавливает константы, которые
  `ReplaceConstantsExpressionVisitor` параметризовал (`Expression.Constant(idx, ...)`), т.е. маркер не
  портится; тела с `OuterRefMarker` по-прежнему не кэшируются. Для глубины 1 поведение меняется только
  тем, что вложенные ссылки форвардятся в корень (это чинит вложенную регистрацию).
- **SQLite-гард корректен по механизму (проверено SQLite 3.37.2).** `abs(-9223372036854775808)` бросает
  `integer overflow`: SQLite разбирает отрицательный литерал как int64 min и документирует это поведение;
  `cast(... as <тип>)` не успевает сработать. 0/1/>1 строк и `SingleOrDefault` дают ожидаемые значения
  (`DefaultOnNull` при 0 строк → default, `Single` без строк → null → throw); покрыто
  `SqliteSpecificTests`. `Math.Abs(long.MinValue)` как C#-выражение не исполняется клиентом: контексты без
  `DataContext` гард не получают.
- **`(cmd._dataContext as DataContext)` (`QueryCommand.QueryPreparer.cs:305`).** Гард молча не применяется,
  если SQL-контекст не наследует абстрактный `DataContext`; в `IDataContext` нет `Dialect`. Сейчас
  не-`DataContext` только `InMemoryDataContext` (SQL не рендерит), поэтому живого дефекта нет; при
  появлении SQL-контекста без `DataContext` гейт `MemberTranslator.cs:486-491` пропустит guardable-проекцию
  без гарда. Надёжнее вынести `Dialect`/capability в `IDataContext`.
- **`IsCardinalityGuardable` (`QueryCommand.cs:350-356`).** Как `internal static` — рабочий общий дом для
  `QueryPreparer` и `MemberTranslator`, хотя по смыслу это capability диалекта (рядом с
  `ISqlDialect.EnforcesScalarSubqueryCardinality`). XML-`<summary>` есть у всех новых internal-членов
  (`OuterRegistry`, `RootRegistry`, `IsCardinalityGuardable`, `PreparedHaving`,
  `WrapSingleScalarCardinalityGuard`); `CountAllMI`/`AbsMI` — private-поля, CS1591 не гейтится.
- **`ReferencedQueriesPlanHash` вложенных команд = 0.** `ReferencedQueries` вложенной команды пуст/`null`
  (геттер `QueryCommand.cs:269` `=> _referencedQueries!`), но ссылки вложенной команды входят в план-ключ
  инлайново через `VisitIndex` в `SelectList`/`PreparedCondition`, и `Equals` резолвит их тем же
  `RootRegistry`; контракт `Equals ⇒ same hash` держится. Геттер отдаёт `null` — helper
  `IEqualityComparerExtensions.Equals` null-безопасен.
- **Двойной источник истины типа проекции.** Гард проверяет `lambda.Body.Type`, а `MemberTranslator`
  (`:487`) — `innerQuery.ResultType`. Для `Select<TResult>` они совпадают, но инвариант не зафиксирован;
  расхождение даст либо ложный throw, либо незагарженный проход. Косметика/на будущее.
- **Покрытие.** Путь отказа нечисловой проекции (`string`/`DateTime`/`Guid` `Single` на SQLite →
  `NotSupportedException` из `MemberTranslator.cs:486-491`) тестами не покрыт: прежний
  `CorrelatedScalarSingle_ShouldThrowNotSupported` (int) переделан в value-тест, замены для нечисловой
  проекции нет. Полезен SQL-gen тест.
- **`ReplaceCommand`/`AnyCommand`.** `GetAnyCommand` (`Builders/EntityBuilderExtensions.cs:267-272`) меняет
  `_referencedQueries[0]` через `ReplaceCommand` (`QueryCommand.cs:395-405`) без пересчёта
  `ReferencedQueriesPlanHash`: ключ план-кэша any-команды остаётся от первой подставленной команды.
  Пре-существующее; с фичей A сюда может попасть вложенная команда с корнево-относительными индексами, а
  рендер any-команды использует её саму как `QueryProvider`. Рекомендация: освежать хэш/переподготавливать;
  тест на две разные any-команды.
- **EOL.** Все правленые файлы в области — CRLF (QueryCommand.cs 459/459, QueryPreparer 737/737,
  CorrelatedQueryExpressionVisitor 639/639, ExpressionPlanEqualityComparer 893/893, MemberTranslator
  506/506, AggregateTerminalRewriter 84/84, CorrelatedQueryTests.cs 495/495). Находки 20/22/29 не
  расширяются.

**Проверка (актуализация 21.09.2026, после фикса Находки 36):** `dotnet build nextorm.sln -c Release` —
**0 warnings / 0 errors**. Юнит-наборы Release: core **175/0**, sqlite **237/0** (было 236 — добавлен
тест Находки 36), postgres **237/0**, sqlserver **216/0**, mysql **58/0**, mariadb **25/0**,
clickhouse **178/0**. Debug-прогон (до фикса): sqlite **236/0**, postgres **237/0**, sqlserver
**216/0** (DEBUG-ассерты `QueryPlan` и гарды `_isPrepared` прошли). Полный интеграционный прогон
Testcontainers (SQLite+PostgreSQL+SQL Server+MySQL+ClickHouse) по отчёту исполнителя: **997 total,
0 failed, 28 skipped** (capability-скипы); аудитом в этом проходе не перезапускался. `rg` по диффу — 0 новых подавлений/`Skip=`/`Task.Delay`/
`Thread.Sleep`/пустых `catch`; `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт); конфигурация
`.editorconfig` — 7 `dotnet_diagnostic.*.severity` (S125/S108/S3060/S1104/S3604/S2292 + CA2254), все
`silent`; `SonarAnalyzer` не подключён (записи `S*` инертны, уже отмечено в «Примечаниях»).

**Регресс golden SQL по 6 диалектам:** единственное изменение отрендеренного SQL — SQLite `Single`
scalar-subquery (был `NotSupportedException`, стал гард; тесты обновлены). Для остальных диалектов
`EnforcesScalarSubqueryCardinality == true` и гард не добавляется; изменение реестра затрагивает только
коррелированные подзапросы и подтверждено зелёными golden-наборами (`postgres`/`sqlserver`/`sqlite`/
`mysql`/`mariadb`/`clickhouse`). Новых падений существующих golden-тестов нет.

## 🔎 Точечный аудит — коррелированный CROSS/OUTER APPLY (21.09.2026)

**Область:** `Builders/EntityBuilder.cs` (четыре новые перегрузки `CrossApply`/`OuterApply`, `JoinApply`,
`CreateJoined`, `GetJoinSource`), `Expressions/JoinExpression.cs` (`From`/`SetFrom`/`ApplySource`),
`Visitors/CorrelatedQueryExpressionVisitor.cs` (`BuildQueryCommand`),
`Query/QueryCommand.QueryPreparer.cs` (`PrepareJoin`), `DataContext/SqlSourceRenderer.cs`
(`MakeApplyJoin`), `DataContext/Dialect/ISqlDialect.cs` + `SqlDialectBase.cs` + диалекты
(`SupportsApply`/`MakeApply`); тесты `tests/nextorm.{sqlserver,postgres,mysql,sqlite,clickhouse}.tests/
SqlGenerationTests.cs`, `tests/nextorm.core.tests/InMemoryTests.cs`,
`tests/nextorm.integration.tests/CommonTestSuite.CorrelatedQuery.cs` + `ITestProvider.SupportsApply`.
Build Release **0/0**; `slopwatch` локальным tool'ом не установлен — паттерн-скан вручную.

| Категория навыка | Статус |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых `IDisposable`/полей-ресурсов нет; `PushOuter` возвращает пре-существующий `OuterScope` (`CorrelatedQueryExpressionVisitor.cs:38-54`). |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` в диффе — **0**; база не менялась: `SuppressMessage` — **5** (все с `Justification`), `#pragma warning disable` — **5** (все с парным `restore`). |
| 3. LINQ | ✅ `JoinApply`/`PrepareJoin` — путь подготовки (промах план-кэша), не per-row; LINQ-цепочек по коллекциям в новых путях нет. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ **Находка 39** (наследуемая correlated-перегрузка на `JoinedEntityBuilder`; ИСПРАВЛЕНА 21.09.2026, была P1), 🟡 **Находка 40** (мутация `JoinExpression.SetFrom` + field-backed `From`; открыта, P2), 🟡 **Находка 41** (гейт по конкретному `InMemoryDataContext` — повтор Находки 21; открыта, P2), ✅ **Находка 42** (`BuildQueryCommand`/`== typeof`/DRY; ЗАКРЫТА частично 21.09.2026 — `OuterRegistry` исправлен, косметический остаток принят). |
| 6. Исключения | ✅ Новые `NotSupportedException`/`InvalidOperationException` — доменные гейты; пустых/`catch (Exception)`/`throw ex;` нет. |
| 7. Хэш-ключи | ✅ Новый источник входит в план-ключ: `PrepareJoin` ставит построенный `From` **до** хеширования (`QueryCommand.QueryPreparer.cs:446,453-456`), `JoinExpressionPlanEqualityComparer` сравнивает/хеширует `From` (`:36,47`) → `FromExpressionPlanEqualityComparer` → `QueryPlanEqualityComparer` для `SubQuery`. `ApplySource` в ключе нет, но `From` из него детерминированно выводится (инсталляция всегда предшествует хешу). |

### ✅ Находка 39 — correlated `CrossApply`/`OuterApply` на `JoinedEntityBuilder` компилируется, но ломается (ИСПРАВЛЕНА 21.09.2026, была P1)

**Место:** `Builders/EntityBuilder.cs:999,1006,1013,1020` (четыре перегрузки объявлены только на
`EntityBuilder<TEntity>`), `:1022-1050` (`JoinApply`), `:1052-1056` (`CreateJoined`),
`Builders/Joins/JoinedEntityBuilder.cs:13,20-22` (конструктор 2-арности ставит `_joins = [join]`),
`:31-34` (value-перегрузки `new`, идут через собственный `JoinCore` `:36-43`).

**Проблема:** `JoinedEntityBuilder<T1,T2> : EntityBuilder<Projection<T1,T2>>`
(`JoinedEntityBuilder.cs:13`), поэтому новые перегрузки **наследуются** с `TEntity = Projection<T1,T2>`.
Вызов `joined.CrossApply(p => ...)` компилируется (probe: `p.Item1` доступен, результат —
`JoinedEntityBuilder<Projection<T1,T2>, T>` с вложенной арностью, `q.Item1.Item1`). Но `CreateJoined`
строит `new JoinedEntityBuilder<Projection<T1,T2>, T>(_dataProvider, applyJoin)`, чей конструктор ставит
`_joins = [applyJoin]` и **теряет исходный join**; левый тип `Projection<T1,T2>` не зарегистрирован как
таблица → `BuildSqlCommandException: Table name is not registered for type
NextORM.Core.Projection`2[...]. Materialize the entity first`. Воспроизведено:
`ctx.From<IProbeSimple>().Join(ctx.From<IProbeComplex>(), (a,b) => a.Id == b.Id).CrossApply(p => ...)`.
Значение-перегрузки (`new CrossApply<T3>(EntityBuilder<T3>)`) дефекта не имеют: их `JoinCore`
(`JoinedEntityBuilder.cs:36-43`) создаёт плоскую арность и добавляет оба join'а.

**Фикс:** объявить `new`-перегрузки correlated-формы на каждом `JoinedEntityBuilder<T1..T8>` (плоская
арность, сохранение `_joins` через собственный `JoinCore`) **либо** научить `CreateJoined` копировать
`_joins` и резолвить источник левого `Projection<>`; **либо** явно скрыть наследуемые перегрузки
(`[EditorBrowsable(Never)]`/`new`-заглушка с внятным `NotSupportedException`). Регресс-тест:
`joined.CrossApply(p => ...)` → точный SQL (в наборе сейчас нет).

**Стало (21.09.2026).** `EntityBuilder.JoinApply` теперь отклоняет correlated-форму над join-проекцией:
`if (typeof(TEntity).TryGetProjectionDimension(out _)) throw new NotSupportedException("A correlated
CROSS/OUTER APPLY source cannot reference a join projection; apply it to a single-entity source
instead.")` (`Builders/EntityBuilder.cs:1028-1030`). Поскольку `JoinedEntityBuilder<T1,T2> :
EntityBuilder<Projection<T1,T2>>`, вызов `joined.CrossApply(p => ...)` теперь падает с внятным
сообщением сразу на построении (проверено probe'ом: `NotSupportedException: A correlated CROSS/OUTER
APPLY source cannot reference a join projection...`), а не `BuildSqlCommandException` из рендера.
Регресс-тест `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:610`
`CrossApply_OnJoinedProjection_ShouldThrowClearNotSupported` — **1/1**; SQL Server-набор **221/0**.
Перегрузки для value-формы (`new CrossApply<T3>(EntityBuilder<T3>)`) этой проблемы не имели и не
затронуты — они сохраняют плоскую арность.

### 🟡 Находка 40 — `JoinExpression` снова мутируется: `SetFrom` + field-backed `From` (P2)

**Место:** `Expressions/JoinExpression.cs:65-74` (`private FromExpression _from = null!;`
`public required FromExpression From { get => _from; init => _from = value; }`, `internal SetFrom`),
`:77-83` (`CloneForCache` возвращает `this`, когда `From.CloneForCache() == From` — в т.ч. placeholder
`new FromExpression(typeof(TJoinEntity))` c `SourceType != null`, `FromExpression.cs:75`),
`Query/QueryCommand.QueryPreparer.cs:435-446` (rebuild + `join.SetFrom(new FromExpression(applyCommand))`).

**Проблема:** `JoinExpression` был неизменяемым (`From` — auto-property `init`; `Strictness`/`IsGlobal` —
`init`; модификаторы копируют, Находки 16/17). Теперь `PrepareJoin` мутирует элемент `cmd._joins` на
месте, а `CloneForCache` при `newFrom == From` возвращает **тот же** экземпляр — значит `SetFrom` может
переписать `From` join'а, разделяемого с кэш-клоном. `ApplySource` после подготовки сохраняется
(избыточное состояние) и копируется в кэш-клон. Живого падения не наблюдалось (кэш-клоны делаются после
подготовки, `From` = `SubQuery` → `CloneForCache` возвращает новый `FromExpression`), но инвариант
«join неизменяем после конструирования» снят.

**Фикс:** строить apply-команду до создания `JoinExpression` (init-значение `From`) либо возвращать новый
`JoinExpression` (как fluent-модификаторы); как минимум — обнулять `ApplySource` после `SetFrom` и
гарантировать, что подготовка не мутирует разделяемый join. Тест: повторная подготовка/кэш-клон с
apply-источником не меняет SQL.

### 🟡 Находка 41 — in-memory APPLY гейтится по конкретному `InMemoryDataContext` (P2, повтор Находки 21)

**Место:** `Builders/EntityBuilder.cs:1024` (`if (_dataProvider is InMemoryDataContext) throw ...`).

Несогласованно с остальными провайдерами без lateral-источника: SQLite/ClickHouse отклоняются
**декларативно** через `ISqlDialect.SupportsApply == false` на рендере (`SqlSourceRenderer.cs:181-182`),
а in-memory — императивно в билдере, уже на этапе построения. Это тот же паттерн, что Находка 21
(ветвление по конкретному `InMemoryDataContext`). Фикс: capability на `IDataContext`/провайдере (или
бросать в рендере/диалекте), чтобы гейт не зависел от конкретного типа контекста.

### ✅ Находка 42 — `BuildQueryCommand`: тонкий алиас, `== typeof`, дублирование (ЗАКРЫТА частично 21.09.2026, была P2)

**Место:** `Visitors/CorrelatedQueryExpressionVisitor.cs:525-530`
(`internal QueryCommand BuildQueryCommand(Expression body) { var cmd = GetQueryCommand(body); cmd.OuterRegistry ??= _queryProvider; return cmd; }`),
`Builders/EntityBuilder.cs:1045` (`source.Body.Type == typeof(QueryCommand)`),
`EntityBuilder.cs:1026-1050` vs `:949-958` (дублирование `_windows`-гарда и `queryBase`).

- `BuildQueryCommand` обходит `ReplaceQueryCommand`, который безусловно ставит
  `cmd.OuterRegistry ??= _queryProvider` (Находка 36); `GetQueryCommand` ставит его только в ветках
  маркера/вложенного подзапроса. APPLY-источник без маркера и вложений оставит `OuterRegistry == null`;
  сегодня такой источник ничего не регистрирует (живого дефекта нет), но для единообразия стоит
  выставлять реестр и здесь (или назвать метод `BuildApplySource`).
- `source.Body.Type == typeof(QueryCommand)` почти всегда `false` (для `QueryCommand<T>`/`EntityBuilder<T>`
  точного совпадения нет), поэтому `Expression.Convert` навешивается всегда; безвредно (implicit-операторы
  `EntityBuilder.cs:824-825`), но условие вводит в заблуждение — `IsAssignableTo(typeof(QueryCommand))`.
- `JoinApply` (`:1022-1050`) и `JoinCore` (`:949-963`) дублируют `_windows`-гард и `queryBase`;
  `CreateJoined` устранил лишь тройное построение `JoinedEntityBuilder`. Вынести общий
  `PrepareJoinedBase`.

**Стало (21.09.2026).** `BuildQueryCommand` теперь выставляет реестр:
`internal QueryCommand BuildQueryCommand(Expression body) { var cmd = GetQueryCommand(body);
cmd.OuterRegistry ??= _queryProvider; return cmd; }`
(`CorrelatedQueryExpressionVisitor.cs:525-530`) — поведение совпадает с `ReplaceQueryCommand`;
под-пункт закрыт. Остаток: `source.Body.Type == typeof(QueryCommand)` (`EntityBuilder.cs:1045`)
по-прежнему `==` (безвредно — есть implicit-операторы `EntityBuilder.cs:824-825` — но вводит в
заблуждение), и `_windows`-гард + `queryBase` всё ещё продублированы в трёх путях
(`JoinApply:1036-1043`, `JoinCore(EntityBuilder):951-958`, `JoinCore(QueryCommand):1068-1075`);
`CreateJoined` объединяет только построение `JoinedEntityBuilder` (как и до фикса). Остаток —
косметика ℹ️, принят; при желании выносится в общий `PrepareJoinedBase`.

### ℹ️ Наблюдения (фикс не требуется)

- **План-ключ нового источника покрыт (проверено по коду).** `PrepareJoin` устанавливает `From` до
  `GetJoinExpressionPlanEqualityComparer().GetHashCode(join)` (`QueryPreparer.cs:446,453-456`);
  `JoinExpressionPlanEqualityComparer.Equals/GetHashCode` сравнивают `From` (`:36,47`), а
  `FromExpressionPlanEqualityComparer` для `SubQuery` идёт в `QueryPlanEqualityComparer` (то есть в SQL,
  проекцию и `OuterReferences` построенной apply-команды). `ApplySource` в ключе отсутствует, но `From`
  из него выводится детерминированно — коллизии планов нет, пока инсталляция предшествует хешу (она
  предшествует). Наблюдение: `ApplySource` остаётся в кэш-клоне и в ключе не участвует.
- **`ITestProvider.SupportsApply`** (`tests/nextorm.integration.tests/Providers/ITestProvider.cs:59-64`) —
  тест-интерфейс, XML-док есть, реализован всеми провайдерами; `DialectCapabilityContractTests` его с
  `ISqlDialect.SupportsApply` не сверяет. Дрейф тест-флага и диалект-флага был бы виден падением
  интеграционного теста (при `true` без диалекта) или лишним skip'ом (при `false`), поэтому риск
  минорный.
- **Документация — исправлена 21.09.2026.** Врезка «Correlation is not expressible yet» /
  «Корреляция пока не выражается» удалена; в `docs/guide/03-joins.md:181` и
  `docs/ru/guide/03-joins.md:183` добавлен раздел «Correlated APPLY / LATERAL»; синхронно обновлены
  `docs/advanced/limitations.md` (+RU), `sql-capabilities-gap-analysis.md`,
  `todo_correlated_apply_lateral.md`, `plan-correlated-subqueries.md`. См. `API-NAMING-REVIEW.md`,
  AP5 (закрыт).
- **EOL.** Все правленые файлы в области — CRLF (EntityBuilder.cs 1312/1312, JoinExpression.cs 114/114,
  CorrelatedQueryExpressionVisitor.cs 679/679, QueryPreparer.cs 753/753, ISqlDialect.cs 1098/1098,
  SqlDialectBase.cs 656/656, ITestProvider.cs 69/69, SqlGenerationTests.cs 2342/2342). Находки 20/22/29
  не расширяются.

**Проверка (актуализация 21.09.2026, после фиксов Находок 39/42):** `dotnet build nextorm.sln -c Release`
— **0 warnings / 0 errors**. Release-наборы: core **176/0**, sqlite **238/0**, sqlserver **221/0**
(было 220 — добавлен тест Находки 39), postgres **239/0**, mysql **60/0**, mariadb **25/0**,
clickhouse **179/0**; SQL Server-фильтр `~Apply` — **9/9** (включая
`CrossApply_OnJoinedProjection_ShouldThrowClearNotSupported`). Находка 39 подтверждена probe'ом:
`joined.CrossApply(...)` теперь даёт `NotSupportedException` с текстом «join projection» вместо
`BuildSqlCommandException`. Полный интеграционный прогон Testcontainers — по отчёту исполнителя
**1005 total / 0 failed / 30 skipped**; аудитом не перезапускался. `rg` по диффу — 0 новых подавлений/
`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`; `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5
открыт).

## 🔎 Точечный аудит — производный запрос как первичный `FROM` с `Join` (21.09.2026)

**Область:** `Builders/EntityBuilder.cs` — новые private `ResolveJoinBase` (`:1079-1093`),
`HasNoNonWhereModifiers` (`:1099-1104`), `ApplyWhereToJoined<TJoinEntity>` (`:1112-1121`) и
`ReplaceTargetParameterVisitor` (`:1126-1130`); вызовы в `JoinCore` (`:949-959`, `:1060-1070`) и
`JoinApply` (`:1022-1048`); общий `CreateJoined` (`:1054-1059`). Рендер не менялся
(`SqlSourceRenderer.MakeFrom` для `FromExpression.SubQuery`). Публичный API не добавлен. Тесты:
`DerivedSourceThenJoin_*`/`DerivedSourceWhereThenJoin_*` + `DerivedSourceDistinctThenJoin_ShouldThrow`
(`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2400`)/`DerivedSourceOrderByThenJoin_ShouldThrow`
(`:2414`) в `tests/nextorm.{sqlserver,postgres,mysql,mariadb,sqlite,clickhouse}.tests/SqlGenerationTests.cs`;
`tests/nextorm.core.tests/InMemoryJoinTests.cs:229`; `tests/nextorm.integration.tests/CommonTestSuite.Join.cs:209`.
Build Release — **0/0**; `slopwatch` локальным tool'ом не установлен — паттерн-скан вручную.

| Категория навыка | Статус |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных нет, новых `IDisposable`-типов нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/пустых `catch` — **0**; `src/` — **6** `SuppressMessage` (все с `Justification`; +1 из untracked `AggregateTerminalRewriter.cs` относительно прежних 5) + **5** `#pragma disable` (все с парным `restore`), неоправданных **0/11**. |
| 3. LINQ | ✅ Новые пути — построение выражения на подготовке (`ReplaceTargetParameterVisitor.Visit`), не per-row; LINQ по коллекциям нет. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ Находки 43/44 исправлены 21.09.2026; ℹ️ — кумулятивный регресс размера класса, дубль in-memory-гейта (Находка 41). |
| 6. Исключения | ✅ Новые `NotSupportedException` — доменные гейты (`:1084-1092`, тесты `TestDerivedSourceThenJoin_ShouldThrow`, `DerivedSourceDistinctThenJoin_ShouldThrow`, `DerivedSourceOrderByThenJoin_ShouldThrow`); пустых/`catch (Exception)`/`throw ex;` нет. |
| 7. Хэш-ключи | ✅ План-ключ покрывает перенесённый `Where`: `QueryCommand.cs:111` (`_condition = definition.Condition`) → `QueryCommand.QueryPreparer.cs:510` (`PreparedCondition`) → `QueryPlanEqualityComparer.cs:117`; производный `From` — `:113`. `CreateJoined` не копирует `_condition` → двойного применения нет. |

### ✅ Находка 43 — `HasOnlyWhereModifier()` пропускала `IsDistinct`: `DISTINCT` молча переезжал на join-результат (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место (было):** `Builders/EntityBuilder.cs:1097-1101` (`HasOnlyWhereModifier`), `:490-497` (`Distinct()` →
`IsDistinct = true`), `:1078-1091` (`BuildJoinBase`), `:1056` (`CreateJoined` копирует `IsDistinct`).

**Проблема:** guard проверял `_group`/`_having`/`_sorting`/`_preWhere`/`_arrayJoins`/`_windows`/
`_limitBy`/`_distinctOn`/`_tablesample`/`_temporal`/`_rowLock`/`_settings`/`Paging`, но **не** `IsDistinct`.
`ctx.From(derived).Distinct().Join(...)` проходил как «только `Where`»: `BuildJoinBase` возвращал `_query`
(без `DISTINCT`), а `CreateJoined` переносил `IsDistinct = true` на join-билдер ⇒ `SELECT DISTINCT … FROM
(derived) JOIN …` вместо `DISTINCT` по производной проекции **до** join.

**Стало (21.09.2026).** `HasNoNonWhereModifiers()` (`EntityBuilder.cs:1099-1104`) теперь отвергает
**каждый** переносимый модификатор: `!IsDistinct && !IsFinal && SampleRatio is null && SampleOffset == 0`
+ прежний список + `Paging.IsEmpty`. `ResolveJoinBase` (`:1079-1093`) для производного источника
(`_query != null`) больше **не** откатывается молча в `ToCommand()`: при `HasNoNonWhereModifiers()` —
возвращает `_query`, иначе бросает `NotSupportedException` «…supports only a Where clause before Join;
put OrderBy/GroupBy/Distinct and other modifiers into the derived query.». Регресс-тесты
`DerivedSourceDistinctThenJoin_ShouldThrow`/`DerivedSourceOrderByThenJoin_ShouldThrow`
(`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2400,2414`) — по 1/1, сообщение `*only a Where clause*`.

**Проверка:** build Release **0/0**; SQL Server-набор **226/0**; golden `DerivedSourceThenJoin_*`/
`DerivedSourceWhereThenJoin_*` по 6 диалектам зелёные.

### ✅ Находка 44 — `CarryWhereCondition` использовала type-based rebind: вложенные лямбды того же типа переписывались на `p.Item1` (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место (было):** `Builders/EntityBuilder.cs:1107-1115` (`new ReplaceParameterExpressionVisitor(item1).Visit(...)`),
`Visitors/ReplaceExpressionVisitor.cs:16-21` (сопоставление `if (_parameter.Type == node.Type)`).

**Проблема:** visitor сопоставлял параметры **по типу**, а не по ссылке; цель замены — `p.Item1` типа
`TEntity`, поэтому любой `ParameterExpression` того же типа внутри `_condition.Body` заменялся. Для
самоссылающейся сущности (`Node { List<Node> Children }`, `Where(d => d.Children.Any(c => c.Id == d.Id))`)
параметр `c` тоже типа `Node` и тело становилось `p.Item1.Children.Any(p.Item1.Id == p.Item1.Id)`.

**Стало (21.09.2026).** `ApplyWhereToJoined<TJoinEntity>` (бывш. `CarryWhereCondition`, `:1112-1121`)
использует private nested `ReplaceTargetParameterVisitor` (`:1126-1130`), который заменяет **точный**
узел `_condition.Parameters[0]` по `ReferenceEquals(node, target)`, не трогая параметры того же типа во
вложенных лямбдах. Публичный `ReplaceParameterExpressionVisitor` не менялся (его type-контракт нужен для
склейки `Where`×2 на `:235/:314/:1204/:1311`). Отдельный регресс-тест на самоссылающуюся сущность не
заводился (трансляция `Any` по навигации вне области); корректность гарантирована reference-сопоставлением.

**Проверка:** build Release **0/0**; существующие `DerivedSource*`/in-memory/integration тесты зелёные.

### ✅ Находка 41 — дополнение (актуализация 21.09.2026)

`ResolveJoinBase` по-прежнему ветвится по конкретному типу
`if (_dataProvider is InMemoryDataContext || typeof(TEntity).TryGetProjectionDimension(out _)) throw …`
(`EntityBuilder.cs:1084`), но теперь одним гейтом закрывает **и** InMemory, **и** цепочку join поверх
производного первичного источника (последнее — по существу расширение Находки 39/AP1 про плоскую
арность). Отдельный номер не завожу: паттерн `is InMemoryDataContext` остаётся дополнением к Находке 41
(`JoinApply:1028`); живой дефект — внятный `NotSupportedException`, покрыт `InMemoryJoinTests.cs:229`.

### ℹ️ Наблюдения (фикс не требуется)

- **God-class регресс (кумулятивный).** `EntityBuilder<TEntity>` физически занимает строки 21–1255
  (~1235) — далеко за порогом >500 и выше зафиксированных в Находке 4 «466». Рост даёт не только этот
  фикс (windows/pivot/apply/derived — несколько незакоммиченных фич), но порог превышен снова. Кандидат
  на вынос блока clause-модификаторов в partial/параметр-объект (ср. `EntityBuilderExtensions`).
- **`ApplyWhereToJoined` после `CreateJoined` при `_query is null`.** Две дешёвые проверки
  (`_query`/`_condition`), на подготовке, не per-row — принято; `HasNoNonWhereModifiers()` больше не
  дублируется (гейт остался в `ResolveJoinBase`), и это безопасно: метод вызывается сразу после
  `ResolveJoinBase` без мутации билдера.
- **Наследуемая `Join(QueryCommand<…>)` на `JoinedEntityBuilder<T1..T8>` не перекрыта.** `CreateJoined`
  не переносит `_joins`, поэтому цепочка `joined.Join(queryCommand, …)` теряет предыдущие join'ы — тот
  же класс, что Находка 39/AP1 (там закрыт только correlated-`Apply` guard'ом). Для **производного**
  первичного источника цепочка теперь гейтится `TryGetProjectionDimension` (`:1084`), но для
  **непроизводного** (`_query is null`) наследуемый путь по-прежнему возможен; дефект пре-существующий,
  отдельного номера не завожу.
- **План-ключ и EOL.** `_fromComparer`/`PreparedCondition` покрывают и источник, и перенесённый `Where`
  (`QueryPlanEqualityComparer.cs:113,117`); `EntityBuilder.cs` — 1372/1372 CRLF, LF-only строк — 0;
  тестовые файлы фичи — CRLF. Находки 20/22/29 не расширяются.
- **XML-доки новых private-членов.** `<summary>` есть у `ResolveJoinBase`, `HasNoNonWhereModifiers`,
  `ApplyWhereToJoined`, `ReplaceTargetParameterVisitor`, `CreateJoined`, `GetJoinSource`; у private
  `JoinCore`/`JoinApply` доков нет (пре-существующее; `CS1591` в `<NoWarn>` 7 библиотек — не гейтится).
- **Док-разрыв — закрыт 21.09.2026.** `todo_mssql_derived_from_join.md` удалён; gap 3 удалён из
  `sql-capabilities-gap-analysis.md` и пункты перенумерованы; «joins over a derived query» убрано из
  Future workstreams; обновлены guide EN+RU, limitations EN+RU, capability-matrix.
  **Дополнение 21.09.2026:** остаток `docs/specs/roadmap/todo_mssql_pivot_derived.md` тоже удалён (выводы свёрнуты в
  доки), ограничение снято — `PIVOT`/`UNPIVOT` теперь принимают производный источник (`From(query)`); см.
  «Точечный аудит — PIVOT/UNPIVOT по производному источнику» выше. Ссылок на удалённый файл в реестре больше нет.

**Проверка (актуализация 21.09.2026, после фиксов 43/44).** `dotnet build nextorm.sln -c Release` —
**0 warnings / 0 errors** (прогнано в этом проходе). Подавления: `src/` — **6** `SuppressMessage` (все с
`Justification`) + **5** `#pragma disable` (все с `restore`), неоправданных **0/11**; `tests/` — 0.
`.editorconfig` — 7 `dotnet_diagnostic.*.severity`, все `silent` (S125/S108/S3060/S1104/S3604/S2292 +
CA2254), `SonarAnalyzer` не подключён (записи `S*` инертны). `rg --files -g 'PublicAPI*.txt'` — пусто
(Шаг 5 открыт). Release-наборы и покрытие — по отчёту исполнителя (core 177, sqlite 240, sqlserver 226,
postgres 241, mysql 62, mariadb 27, clickhouse 181; integration 1009 total, реальных падений 0 —
окруженческие флейки ClickHouse/SqlServerSpecificTests прошли на повторе; coverage 85.1 % line /
73.8 % branch); аудитом тесты/покрытие не перезапускались. `rg` по диффу — 0 новых `Skip=`/`Task.Delay`/
`Thread.Sleep`/пустых `catch`.

## 🔎 Точечный аудит — SQL Server `PIVOT`/`UNPIVOT` по производному источнику (21.09.2026)

**Область:** изменение `Builders/EntityBuilder.cs` — `ResolvePivotInner` (`:668-690`) больше не отвергает
`_query`, а возвращает `new FromExpression(_query)`; XML-`<summary>` `Pivot`/`Unpivot`/`ResolvePivotInner`;
тесты `Pivot_ShouldAcceptDerivedSource`/`Pivot_ShouldAcceptDerivedComputedSource`/`Unpivot_ShouldAcceptDerivedSource`/
`Pivot_ShouldThrowWhenDerivedSourceHasModifiers` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2325-2395`)
и `Pivot_ShouldReshapeDerivedSource`/`Unpivot_ShouldStackDerivedColumns`
(`tests/nextorm.integration.tests/SqlServerSpecificTests.cs:288,304`). Публичной поверхности не добавлено;
диалектные хуки и рендер (`SqlSourceRenderer.MakePivot`, `QueryPreparer.PrepareFrom`) не менялись — ветка
`FromExpression.SubQuery` уже существовала. Build Release — **0 warnings / 0 errors** (этот проход).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=` — **0**; база не менялась (`src/` — 6 `SuppressMessage` все с `Justification` + 5 `#pragma disable` все с парным `restore`, неоправданных **0/11**). |
| 3. LINQ на горячем пути | ✅ `ResolvePivotInner` — построение на подготовке, не per-row; LINQ по коллекциям нет. Одна аллокация `new FromExpression(_query)` на билд запроса — принято (ср. ветку `_table`/`_from`). |
| 4/6. События, исключения | ✅ Новых подписок/`catch` нет; `NotSupportedException` — доменный гейт. |
| 5. Проектирование | ✅ Находки 45/46 исправлены 21.09.2026 (ниже). Заодно **закрыта Находка 31** (guard расширен на все модификаторы). |
| 7. Хэш-ключи | ✅ `PivotEquals`/`PivotHash` рекурсивно сворачивают `Inner`, в т.ч. новый `SubQuery` (`FromExpressionPlanEqualityComparer.cs:61-76,95-118`) — сравнение и хэш согласованы; деталь по `CloneForCache` — Находка 45. |
| Слоп-паттерны (slopwatch локально не установлен; скан вручную) | ✅ `Skip=`/`Ignore`/`Thread.Sleep`/пустых `catch`/инлайновых `Version` — **0** в изменённых файлах фичи. |

### ✅ Находка 45 — `FromExpression.CloneForCache()` делил `Pivot.Inner.SubQuery` между планом-кэшем и живым запросом (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место:** `src/nextorm.core/Expressions/FromExpression.cs:75` (условие `|| Pivot is not null` → `return this`).

**Было (что делает код):** `CloneForCache` считает любой `Pivot` иммутабельным и возвращает `this`; до этой
фичи `Pivot.Inner` мог быть только таблицей/TVF/сущностью (они тоже шарятся — `:75`). Теперь `ResolvePivotInner`
(`EntityBuilder.cs:679-680`) кладёт внутрь `new FromExpression(_query)`, поэтому план-кэш
(`QueryCommand.CloneForCache` → `QueryCommand.Clone.cs:94`) получает `_from.Pivot.Inner.SubQuery`, ссылающийся
на **живой** производный `QueryCommand`, тогда как «голый» производный `FROM` тот же метод глубоко клонирует
(`:77`). Это расходится с заявленным инвариантом «кэш владеет клонами, чтобы поздняя ре-подготовка живого
запроса не меняла то, с чем сравнивается кэш» (`QueryCommand.Clone.cs:100-101`).

**Последствие (до фикса):** план-кэш и живой запрос делят внутренний подзапрос. Живого падения не
воспроизведено (`_isPrepared`-гейт `PrepareFrom`, `QueryCommand.QueryPreparer.cs:150-151`, не даёт
ре-подготовить inner), поэтому это P2-hardening, а не подтверждённый дефект. Переиспользование плана запинено
только для табличного источника (`Pivot_ShouldReuseCachedPlan`, `SqlGenerationTests.cs:2289`); производный
случай не покрыт.

**Стало (21.09.2026).** `FromExpression.CloneForCache` (`Expressions/FromExpression.cs:75-79`) больше не считает
любой `Pivot` шарируемым: он вызывает `Pivot.CloneForCache()` и возвращает `this` только при
`ReferenceEquals(pivot, Pivot)`, иначе — свежий `FromExpression(pivot)`. Новый `internal
PivotExpression.CloneForCache` (`Query/PivotExpression.cs:154-163`) шарит таблицу/TVF/сущность
(`Inner.CloneForCache()` вернул `this`), а производный `Inner` глубоко клонирует. Инвариант
`QueryCommand.Clone.cs:100-101` восстановлен для pivot-источника. Тест:
`Pivot_ShouldReuseCachedPlan_DerivedSource` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:2308`) — два
структурно идентичных производных pivot'а через кэш-путь дают одинаковый SQL с подзапросом и `pivot`-клаузой.
Публичной поверхности не добавлено (`CloneForCache` — `internal`).

### ✅ Находка 46 — сообщение guard'а `ResolvePivotInner` не упоминало TVF-источник и безусловно давало производный совет (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место:** `src/nextorm.core/Builders/EntityBuilder.cs:676-677`.

**Было (что делает код):** guard допускает таблицу/сущность (`_table`/метаданные), TVF (`_from.TableFunction`),
производный (`_query`) и даже вложенный pivot (`_from.Pivot`), но сообщение перечисляет только «a plain
table/entity source or a derived query» и всегда добавляет «or (for a derived source) inside the derived
query». TVF, который XML-`<summary>` (`:612`) честно называет, в тексте отсутствует; производный совет
выводится и при `_query is null` (обычная таблица).

**Последствие (до фикса):** пользователь TVF-источника получает текст, из которого не следует, что его форма
вообще поддерживается; производный совет нерелевантен для табличного случая. На корректность не влияет, но
это единственная диагностика ошибки.

**Стало (21.09.2026).** `ResolvePivotInner` ветвит сообщение по `_query` (`EntityBuilder.cs:676-678`):
производный источник → «PIVOT/UNPIVOT over a derived query accepts no modifiers on the pivot builder; apply …
inside the derived query or to the reshaped result.»; иначе → «… a plain table/entity source, a table-valued
function or a derived query; …» (TVF теперь назван). Существующие ассерты `*plain table*`/`*derived query*`
(`SqlGenerationTests.cs:2403,2414,2418,2428-2437`) проходят.

**Проверка (после фиксов 45/46, 21.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.sqlserver.tests -c Release --filter "FullyQualifiedName~Pivot|FullyQualifiedName~Unpivot"`
— **13/13 passed** (в т.ч. `Pivot_ShouldReuseCachedPlan_DerivedSource`). По отчёту исполнителя: unit core 177/
sqlite 241/sqlserver 234, integration 1011 total / 0 failed / 30 skipped, coverage 85.1 % line / 73.8 % branch,
пример MSSQL 11/11. `.editorconfig` — 7 `dotnet_diagnostic.*.severity`, все `silent` (S125/S108/S3060/S1104/S3604/
S2292 + CA2254), `SonarAnalyzer` не подключён (записи `S*` инертны); `rg --files -g 'PublicAPI*.txt'` — пусто
(Шаг 5 открыт).

## 🔎 Точечный аудит — capability-объекты диалекта (Фаза 2 + 2a) (21.09.2026)

**Область:** `src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs` (+12 публичных интерфейсов),
`ISqlDialect.cs` (12 nullable DIM-свойств, `Supports*`/`Make*` → вычисляемые делегаты),
`SqlDialectBase.cs` (12 `virtual` object-свойств, удаление throwing-заглушек), шесть `*Dialect.cs`
(override объекта вместо `Make*`), `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs`.
Build Release — **0 warnings / 0 errors** (этот проход). `slopwatch` локальным tool'ом не установлен —
паттерн-скан выполнен вручную. Продолжение аудита Фазы 1 (Находка 32 и раздел 20.09.2026).

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Рендереры — `internal sealed` синглтоны/поля-ссылки без ресурсов; `IDisposable`/`CA2213`/`CA1816` неприменимы. Захватывающие рендереры держат только ссылку на диалект-синглтон. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` в диффе — **0**. База `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`), неоправданных **0/11**. В `tests/` `Skip=`/`Ignore` — 0. |
| 3. LINQ на горячем пути | ✅ Дифф LINQ-цепочек не добавляет; рендеры — прямая интерполяция/`string.Join`, как прежние `Make*`. `AllDialects()` в тесте — reflection, не горячий путь. |
| 4/6. События, исключения | ✅ Новых подписок/`catch` нет; `NotSupportedException` — доменные гейты capability, не пустой `catch`/`catch (Exception)`. |
| 5. Проектирование | ✅ Находки 47–49 закрыты 21.09.2026 (ниже); 16 интерфейсов, god-классы не расширены. |
| 7. Хэш-ключи | ✅ Не затронуто: capability-объекты в план-ключ не входят (singleton на диалект, ключи не менялись). |
| EOL | ✅ Все 10 затронутых файлов — CRLF (`DialectCapabilities.cs` 224/224); Находки 20/22/29 не расширяются. |

### ✅ Находка 47 — `ILockRenderer` несёт бросающий метод: capability-объект с «половинчатой» поддержкой (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место:** `DialectCapabilities.cs:214-224` (`UsesTableHints:217`, `Render:220`, `RenderHint:223`);
`SqlServerDialect.cs:503-514` (`Render` бросает, `:509-510`); `PostgresDialect.cs:282-293`
(`RenderHint` бросает, `:291-292`); `MySqlDialect.cs:281-292` (`:290-291`);
`tests/nextorm.integration.tests/DialectCapabilityContractTests.cs:190-201`.

**Суть:** присутствие объекта означает «row locking поддержан», но внутри него один из двух рендеров
всегда бросает: SQL Server бросает `Render`, PostgreSQL/MySQL (и наследующий MariaDB) бросают
`RenderHint`. Это возвращает внутрь capability-объекта ровно ту проблему, которую закрывает RFC
(«объект есть ⇒ рендер работает»): потребитель обязан знать `UsesTableHints`, иначе получает
`NotSupportedException` из якобы «поддерживающего» объекта. Нарушен ISP: 3 из 4 реализаций содержат
мёртвый бросающий член. Контрактный тест это маскирует — вызывает только метод, выбранный по
`UsesTableHints` (`:192-200`), поэтому непокрытая ветка не падает; его же XML-док обещает «non-null object
implies its renderer does not throw».

**Фикс:** один `Render(LockMode mode)`, возвращающий только токен (`updlock`/`holdlock`/` for update`/
` lock in share mode`), а форму вынести в отдельный член диалекта (`bool LockingUsesTableHints` уже есть,
`ISqlDialect.cs:1164`) или enum `LockPlacement`. Тогда ни один метод объекта не бросает. Альтернатива —
разнести `ILockClauseRenderer`/`ILockHintRenderer` (два объекта, `Lock` = один из них).

### ✅ Находка 48 — контрактный тест: вакуумная проверка `Pivot` и пропущенный `MakeTableSample` (ИСПРАВЛЕНА 21.09.2026, была P2)

**Место:** `tests/nextorm.integration.tests/DialectCapabilityContractTests.cs:203-204`; `:172-182`.

**Суть:** `if (dialect.Pivot is { } pivot) pivot.Should().NotBeNull();` — при паттерне `is { } pivot`
проверка `NotBeNull()` всегда истинна; ни `RenderPivot`, ни `RenderUnpivot` не вызываются, хотя заявленный
инвариант (см. `API-NAMING-REVIEW.md`, «Фаза 2 + 2a») — «объект не `null` ⇒ `Render` не бросает для всех
новых объектов». `IPivotRenderer` — единственный из 16 объектов, чей рендер тестом вообще не исполняется
(и единственный с `PivotExpression`-зависимым телом, где ошибка рендера наиболее вероятна). Рядом
`MakeTableSample` (`:172-182` проверяет только `tableSample.Render`) не вызывается.

**Фикс:** построить минимальный `PivotExpression` (pivot + unpivot) и вызвать оба метода на каждом
диалекте с непустым `Pivot`; добавить `dialect.MakeTableSample(...)` для поддерживаемого метода.

### ✅ Находка 49 — вычисляемые `Supports*` в `SqlDialectBase` остаются `virtual`: инвариант обходится override'ом (ИСПРАВЛЕНА 21.09.2026, была P2, hardening)

**Место:** `SqlDialectBase.cs:39,45,71,88,98,113,120,137,157,163,181,186,220,262,270,283,662` — 17
вычисляемых флагов на 16 объектов.

**Суть:** флаг вычисляется из объекта и в in-repo диалектах **не** переопределяется (проверено:
`override bool Supports*` для 16 object-backed возможностей — **0**). Но `public virtual` позволяет
наследнику `SqlDialectBase` переопределить `SupportsX => true` без объекта; тогда `MakeX` бросит — исходная
класс-A-щель сохраняется для внешних диалектов (продолжение ℹ️-заметки Фазы 1; `ISqlDialect`-DIM флаги
можно переопределить так же). Инвариант держится на in-repo диалектах, а не на конструкции.

**Фикс:** снять `virtual` с вычисляемых `Supports*` в `SqlDialectBase` (`public bool SupportsX => X is not null;`),
оставив `virtual` только объектное свойство; тогда через базу «солгать» нельзя — диалект обязан дать
объект. Для прямых реализаций `ISqlDialect` (мимо базы) щель остаётся и закрывается в Фазе 3 удалением пар.

### ℹ️ Наблюдения (фикс не требуется)

- **Thread-safety захватывающих рендереров.** `SqlServerPivotRenderer` (`SqlServerDialect.cs:468-501`) и
  `ClickHouseMultiIfRenderer`/`ClickHouseUniqAggregates`/`ClickHouseQuantileAggregates`
  (`ClickHouseDialect.cs:581-611`) захватывают диалект; полей, кроме `readonly`-ссылки, нет; вызываемые
  `Escape`/`MakeTableAlias`/`MakeTypeName`/`MakeAggregate` — чистые и состояние не мутируют; диалекты
  `sealed` (`SqlServerDialect.cs:10`, `ClickHouseDialect.cs:12`), вызовы ленивые (из `Render`, не из ctor),
  поэтому `CA2214` не возникает. Синглтоны, доступные конкурентно, безопасны. `Escape`/`MakeTableAlias`
  применены корректно: идентификаторы PIVOT/UNPIVOT и алиас результата брекет-квотируются через диалект.
- **Disposal/аллокации.** `IDisposable` не нужен. Конструктор диалекта создаёт рендереры один раз
  (`SqlServerDialect.cs:17`, `ClickHouseDialect.cs:21-26`) — +1..3 объекта на диалект, не на запрос.
  `Render` аллоцирует локальный `StringBuilder`/`string.Join` как прежние `Make*` — не регрессия.
- **Класс B `MakeUniqAggregate`/`MakeQuantile`/`MakeMedian`.** До изменения база давала не-бросающий
  ANSI-дефолт (`$"{MakeAggregate(name)}({argument})"` и т.п., `SqlDialectBase.cs:115-127`); теперь без
  объекта — `NotSupportedException`. Формально это выход за класс A и поведенческое изменение для внешнего
  диалекта, который выставлял `SupportsUniqAggregates`/`SupportsQuantileAggregates` и полагался на
  `base.Make*`. Практический риск низкий: дефолт был валиден только для ClickHouse (иной провайдер получил
  бы семантически невалидный SQL), alpha допускает слом, Фаза 3 удаляет пары. Зафиксировано, не чинится.
- **XML-docs.** Все 16 публичных интерфейсов (12 новых) и их члены имеют `<summary>`; у
  `IPivotRenderer.RenderPivot`/`RenderUnpivot` нет `<param>` (CS1591 в `<NoWarn>` всех 7 `.csproj`, не
  гейтится). Приложение A (45) не меняется.
- **Находка 24** (`SupportsTableSampleMethod` бросал) закрыта этим изменением: базовый
  `SupportsTableSampleMethod` теперь `=> TableSample?.Supports(method) ?? false` (`SqlDialectBase.cs:223`).
- **Находка 34/33** (дублирование/размер `CorrelatedQueryExpressionVisitor`) не затрагиваются.

**Проверка.** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `rg` по диффу — 0
добавленных подавлений/`Skip=`/задержек/пустых `catch`; база подавлений `src/`: 6 `SuppressMessage` (все с
`Justification`) + 5 `#pragma disable` (все с парным `restore`), неоправданных **0/11**; `Task.Delay` — 2
(прежние `Task.Delay(0)`); `.editorconfig` — 7 `dotnet_diagnostic.*.severity`, все `silent`, `SonarAnalyzer`
не подключён (записи `S*` инертны); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт, DC3/DC8);
docker-интеграция не перезапускалась (по отчёту исполнителя: 1005 total / 0 failed / 30 skipped,
`DialectCapabilityContractTests` 3/3). Публичная сторона — `API-NAMING-REVIEW.md`, DC5–DC8.

**Стало (21.09.2026, фиксы применены).**
- **Находка 47 — закрыта.** `ILockRenderer` сведён к `{ bool UsesTableHints; string Render(LockMode); }`
  (`DialectCapabilities.cs:222-231`), `RenderHint` удалён (`rg RenderHint src tests` — пусто).
  `SqlServerLockRenderer.Render` возвращает голый токен (`updlock`/`holdlock`, `SqlServerDialect.cs:503-513`),
  `PostgresLockRenderer`/`MySqlLockRenderer` — трейлинг-клаузу (`PostgresDialect.cs:282-291`,
  `MySqlDialect.cs:281-290`). `SqlDialectBase.MakeLock`/`MakeLockHint` оба делегируют в `ILockRenderer.Render`
  (`SqlDialectBase.cs:668,673`), достижим только выбранный по форме метод. Ни один capability-объект больше не
  несёт бросающего метода. Контрактный тест вызывает `Render(Share)` без ветвления (`:207-211`) и сверяет
  `LockingUsesTableHints == lockRenderer.UsesTableHints` (`:210`).
- **Находка 48 — закрыта.** Тест собирает минимальный `PivotExpression` через `internal static
  PivotExpression.ForPivot`/`ForUnpivot` рефлексией (`DialectCapabilityContractTests.cs:22-36`; у
  integration-проекта нет `InternalsVisibleTo`), затем вызывает `RenderPivot`+`MakePivot` и
  `RenderUnpivot`+`MakeUnpivot` (`:213-222`); `TableSample`-блок дополнительно ассертит
  `dialect.MakeTableSample(...)` (`:197`). Вакуумный `NotBeNull()` убран, `IPivotRenderer` теперь
  действительно исполняется.
- **Находка 49 — закрыта.** С `virtual` снято у 17 вычисляемых `Supports*`-свойств + 4 поимённых/помесячных
  предикатов + `LockingUsesTableHints` (22 члена) в `SqlDialectBase`; object-свойства остались `virtual`.
  Проверка: `grep "public virtual bool Supports.*is not null" SqlDialectBase.cs` — пусто; in-repo диалектов,
  переопределяющих вычисляемый флаг, — 0, поэтому поведение не изменилось, а база больше не позволяет
  «солгать» флагом без объекта. Остаётся только путь прямой реализации `ISqlDialect` мимо базы (закрывается
  в Фазе 3 удалением пар).

**Обновление 21.09.2026 (Фаза 3):** `MakeLock`/`MakeLockHint`/`LockingUsesTableHints`,
`MakePivot`/`MakeUnpivot`/`MakeTableSample` и 22 вычисляемых `Supports*` **удалены**; см. раздел
«Фаза 3: удаление дублирующих `Supports*`/`Make*`» ниже.

**Остаточные наблюдения (ℹ️, не P2).**
- `PivotExpression.ForPivot`/`ForUnpivot` вызываются из теста рефлексией — приемлемый обход отсутствия
  `InternalsVisibleTo`; при желании можно добавить `[assembly: InternalsVisibleTo("nextorm.integration.tests")]`.
- Дип покрытия 85.1→85.0 % line / 73.8→73.2 % branch — это DIM-фолбэки `ISqlDialect`/`SqlDialectBase`,
  структурно недостижимые через базу (компилируются, но не исполняются); носители полностью покрыты.

**Проверка (после фиксов 47–49, 21.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings /
0 errors** (перепроверено); `DialectCapabilityContractTests` — **3/3 passed** (перепроверено, 710 ms).
По отчёту исполнителя: unit core 177 / sqlite 241 / sqlserver 234 / postgres 241 / mysql 62 / mariadb 27 /
clickhouse 181, 0 failed; full integration 1005 / 0 failed / 30 skipped; coverage 85.0/73.2;
`rg '\bRenderHint\b|\bITableSampleRenderer\b|\bISequenceAggregates\b' src tests` — пусто;
`rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 / DC3 / DC8 — по решению автора). Публичная сторона —
`API-NAMING-REVIEW.md`: DC5/DC6 ✅, DC7 ℹ️.

## 🔎 Точечный аудит — Фаза 3: удаление дублирующих `Supports*`/`Make*` (21.09.2026)

**Область:** `DataContext/Dialect/{ISqlDialect,SqlDialectBase,DialectCapabilities}.cs`, шесть `*Dialect.cs`,
трансляторы (`StringFunctionTranslator`, `XmlSqlTranslator`, `DateConversionSqlTranslator`,
`SessionInfoFunctionTranslator`, `UuidFunctionTranslator`, `AdvancedAggregateTranslator`,
`BuiltinFunctionTranslator`), `DataContext/{SqlBuilder,SqlSourceRenderer}.cs`, 6 dialect-тестов,
`tests/nextorm.integration.tests/DialectCapabilityContractTests.cs`. Build Release — **0 warnings /
0 errors**; контрактный тест — **2/2** (перепроверено этим аудитом). `slopwatch` локальным tool'ом не
установлен — паттерн-скан вручную.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: удаление членов/миграция вызовов, новых disposable-полей нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` — **0**; база `src/`: 6 `SuppressMessage` (все с `Justification`) + 5 `#pragma disable` (все с `restore`), неоправданных **0/11**. |
| 3. LINQ на горячем пути | ✅ Не затронуто; guard'ы — `is not { } local` без LINQ-цепочек. |
| 4/6. События, исключения | ✅ Новых подписок/`catch` нет; `NotSupportedException` — доменные гейты capability. |
| 5. Проектирование | ✅ Дублирование `Supports*`/`Make*` устранено; единственный источник — объект; **Находка 49 снята удалением** (вычисляемых object-backed флагов в базе больше нет). Новых P0–P2 нет. |
| 7. Хэш-ключи | ✅ Не затронуто. |
| EOL | ✅ Затронутые `.cs` — CRLF; новых LF-файлов нет. |

### ✅ Находка 50 — удаление 41 дублирующего члена: проверка мигрированных call-site (ЗАКРЫТА 21.09.2026, наблюдение)

Удалена вся class-A пара `Supports*`/`Make*`, ставшая вычисляемым делегатом к 16 capability-объектам
(41 уникальный член; в `ISqlDialect` + `SqlDialectBase` — 60 деклараций), плюс избыточный
`ClickHouseDialect.MakeDateConversion`. Проверка мигрированных мест (сверено с исходным поведением):

- **Lock hint vs clause** (`SqlBuilder.cs:93-94`, `:388-396`): hint-форма выбирается через
  `_ctx.Dialect.Lock is { UsesTableHints: true } lockHint` и `lockHint.Render(mode)`; трейлинг-клауза —
  `if (!lockRenderer.UsesTableHints) ... lockRenderer.Render(rowLock.Mode)`. Удалённый
  `LockingUsesTableHints` нигде не остался; выбор эквивалентен прежнему.
- **`MakeLimitBy` → `limitByRenderer.Render(...)`** (`SqlBuilder.cs:341-364`): null-guard
  `_ctx.Dialect.LimitBy is not { } limitByRenderer` бросает то же сообщение, `AppendLine()` + `Append(render)`
  сохранены; порядок «guard → append» не изменился.
- **Null-guard паттерн** во всех трансляторах (`StringFunctionTranslator.cs:342`, `XmlSqlTranslator.cs:40`,
  `DateConversionSqlTranslator.RequireSupport`, `SessionInfoFunctionTranslator.cs:33`,
  `UuidFunctionTranslator.cs:31`, `AdvancedAggregateTranslator.cs:188,207,230,325`,
  `BuiltinFunctionTranslator.cs:133,180`, `SqlSourceRenderer.cs:366`): `is not { } local` с прежними
  сообщениями; поимённые `object.Supports(name)` + `object.Render(name)` эквивалентны удалённым
  `ISqlDialect.SupportsX(name)`/`MakeX(name)`.
- **`SqlSourceRenderer.MakePivot`** (`:362-389`) — guard `ctx.Dialect.Pivot` + `RenderPivot`/`RenderUnpivot`.
- **Тесты:** 6 dialect-тестов и контрактный тест переведены; `DialectCapabilityContractTests` сведён к
  2 фактам — удалён тавтологичный факт «флаг ↔ объект», render-факт проверяет только объекты
  (`Iif`…`Lock`, pivot через `RenderPivot`/`RenderUnpivot`, `:149-156`). Удалять факт было корректно:
  флагов-делегатов больше нет.
- **Новых P0–P2 не введено.** ℹ️ остаётся прежнее: `PivotExpression.ForPivot`/`ForUnpivot` вызываются
  из теста рефлексией (нет `InternalsVisibleTo`).

### ✅ Находка 49 (актуализация Фазы 3, 21.09.2026) — снята удалением

Находка 49 (вычисляемые `Supports*` остаются `virtual` в `SqlDialectBase`) **снята**: Фаза 3 удалила
17 вычисляемых `Supports*`-свойств + 4 поимённых/помесячных предиката + `LockingUsesTableHints` вместе с
`Make*`-делегатами. `SqlDialectBase` больше не содержит object-backed вычисляемых флагов — обходить
override'ом нечего (остались только class-B/C `Supports*`, не привязанные к объекту).

### ✅ Находки 47/48 (актуализация Фазы 3, 21.09.2026) — текст фикса устарел, находки закрыты

- **Находка 47:** рекомендация «`bool LockingUsesTableHints` уже есть» устарела — `LockingUsesTableHints`
  удалён в Фазе 3, как и `SqlDialectBase.MakeLock`/`MakeLockHint`. Фиксация прежняя: `ILockRenderer` =
  `{ bool UsesTableHints; string Render }`; форму даёт `Lock.UsesTableHints` (без dialect-обёрток).
- **Находка 48:** `MakePivot`/`MakeUnpivot`/`MakeTableSample` удалены в Фазе 3; контрактный тест теперь
  вызывает только `RenderPivot`/`RenderUnpivot` и `tableSample.Render`, т.е. render-without-throwing
  сохранён без dialect-обёрток.

**Проверка (Фаза 3, 21.09.2026).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet run --project tests/nextorm.integration.tests -c Release --no-build -- -class
nextorm.integration.tests.DialectCapabilityContractTests` — **2/2 passed**. `rg` по `src`/`tests` для 41
удалённого члена — пусто (единственные совпадения — `SqlSourceRenderer.MakePivot`/`MakeArrayJoin`,
статические хелперы, не члены интерфейса); `docs` — старых имён нет (рабочий RFC удалён, его закрытые выводы перенесены в этот реестр и `API-NAMING-REVIEW.md`).
По отчёту
исполнителя: unit core/sqlite/sqlserver/postgres/mysql/mariadb/clickhouse — 0 failed; integration 1005 /
0 failed / 30 skipped; coverage 85.2 % line / 74 % branch. `rg --files -g 'PublicAPI*.txt'` — пусто;
трекинг/заморозка — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md), issue [#53](https://github.com/AlexeyShirshov/nextorm/issues/53) (см.
`API-NAMING-REVIEW.md`, «Фаза 3»). Публичная сторона: удаление 41 пары — source+binary-breaking для
внешних реализаторов `ISqlDialect`/`SqlDialectBase` (alpha допускает; фиксация — [`todo_public_api_freeze.md`](../roadmap/todo_public_api_freeze.md)).

## 🔎 Точечный аудит — закрытие source-скоупа в `DefaultColumnsProvider` (21.09.2026)

**Область (uncommitted):** `Query/DefaultColumnsProvider.cs` — 4-й флаг `closed` в записи `_list` (`:10` ветка NET8/ValueList, `:15` ветка `#else`/List), `PopSourceScope` (`:24-39`), пропуск закрытых записей в `FindAlias(ParameterExpression,…)` (`:65`), `FindQueryCommand` (`:92`) и `FindAlias(Type,…)` (`:116`); XML-`<summary>` `IColumnsProvider.PopSourceScope` (`Query/IColumnsProvider.cs:40-44`). Индекс записи остаётся равным номеру алиаса (`t{idx+1}`, `DefaultAliasProvider.FindAlias:26`), поэтому записи **не удаляются**, а помечаются `closed`. Исправляет утечку: записи вложенной команды оставались видимыми для объемлющей, и внешний `Join` по тому же типу сущности резолвился в чужой alias (`t2` из внутреннего join → SQLite `no such column: t2.requiredstring`).

**Тесты (uncommitted):** `DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias` в шести `tests/nextorm.{sqlite,sqlserver,postgres,mysql,mariadb,clickhouse}.tests/SqlGenerationTests.cs` (`:2282`, `:2489`, `:3158`, `:589`, `:323`, `:1789`); интеграционный `DerivedSourceWithJoinThenJoin_ShouldReturnData` (`tests/nextorm.integration.tests/CommonTestSuite.Join.cs:226`). Публичная поверхность не менялась.

| Категория навыка | Статус |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных и `IDisposable`-типов нет; `DefaultColumnsProvider` владеет только `ValueList`/`List`, unmanaged-ресурсов нет, финализаторов/`Dispose` не добавлено. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/пустых `catch` — **0**. В `src/`: **5** `SuppressMessage` (все с `Justification`) + **5** `#pragma` (все с парным `restore`) → неоправданных **0/10**; в `tests/`: 0. Соотношение не изменилось. |
| 3. LINQ / горячий путь | ✅ `PopSourceScope` — арифметический цикл, LINQ и аллокаций нет; `FindAlias` добавляет по одной проверке `item.Item4` на запись-кандидат (перебор записей команды, не per-row). 4-й `bool` **не увеличил** запись: `(object,object,bool)` и `(object,object,bool,bool)` оба **24 байта** (padding; замер `Unsafe.SizeOf`, см. «Проверка»). `PopSourceScope` вызывается раз на команду (`SqlBuilder.cs:57/518`), не на колонку/строку. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ Правка минимальна и локализована (флаг в той же записи, без второго параллельного состояния); ⚠️ `#else`-ветка не компилируется (наблюдение A), `FindQueryCommand` остаётся type-indexed (наблюдение C). |
| 6. Исключения | ✅ `PopSourceScope` — в `finally` (`SqlBuilder.cs:516-518`), парность push/pop гарантирована; новых `catch`/`throw` нет. На NET8+ `Peek()` на пустом стеке бросает (раньше `ValueList.Pop()` молча уводил `_curIdx` в минус) — fail-fast корректен, путь недостижим. |
| 7. Хэш-ключи кэша | ✅ Рендер-онли: меняется только видимость записей при построении SQL; нумерация алиасов детерминирована и не менялась, состав `QueryPlan`/`QueryPlanEqualityComparer` не затронут, кэш-ключ не инвалидируется. |

### ✅ Вердикт по граничным случаям (закрытый скоуп не ломает существующие пути)

| Путь | Почему безопасно | Покрытие |
|---|---|---|
| Коррелированные подзапросы (`includeOuterScopes:true`) | Внешняя запись добавляется объемлющей командой до `PushSourceScope` вложенной и остаётся **open** до её собственного `PopSourceScope`; внешний lookup (`AliasResolver.GetOuterAliasFromParam`) идёт при ещё не завершённом рендере внешней команды. Скип закрытых дополнительно убирает ложное попадание в запись соседнего/уже закрытого подзапроса. | `CorrelatedQueryTests` + `CommonTestSuite.CorrelatedQuery` (все зелёные) |
| Глубина корреляции ≥ 2 | Средняя запись **open**, пока рендерится самый внутренний подзапрос (средняя ещё не делала pop); внутренний видит и `t2`, и `t1`. | `NestedCorrelationDepth2_ShouldReferenceBothOuterLevels`, `NestedCorrelationDepth2_ShouldEvaluatePerRow` |
| Соседние вложенные подзапросы одного типа | Первый сосед закрывает свои записи в своём `PopSourceScope`; второй при поиске своей записи идёт от своего `SourceScopeStart` (не видит чужие), а объемлющая команда при поиске внешнего alias теперь пропускает закрытые. | `CorrelatedSiblingSubqueriesOfSameType_ShouldUseTheirOwnAlias`, `CorrelatedSiblingExistsOfSameType_ShouldUseTheirOwnAlias` |
| Несколько join'ов с источниками-подзапросами | `Add(cmd)` для производного источника делает **объемлющая** команда после возврата вложенного `MakeSelect` (`SqlSourceRenderer.cs:252→256`), т.е. запись open; внутренние записи закрыты. Именно этот путь — баг-репро. | `DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias` (6 диалектов) + интеграционный |
| `ARRAY JOIN` | Привязанный элемент адресуется фиксированным алиасом `ArrayJoinNames.ElementAlias` (`MemberTranslator.cs:103`), а не индексом `ColumnsProvider`; пересечения нет. | `SqlGenerationTests` ClickHouse + интеграционные `ARRAY JOIN` |
| CTE | Каждый CTE рендерится со **свежим** `DefaultColumnsProvider` (`SqlSourceRenderer.cs:44,68`), состояние `closed` за границу CTE не переходит. | `Cte_*`, `Cte_JoinedToAnotherCte_ShouldQualifyAliasColumns` |
| `PIVOT`/`UNPIVOT` и TVF | Внутренний `MakeFrom` добавляет запись и возвращается до `Add(TableAlias)`/`GetNextAlias`; `RenderPivotColumn` выполняется уже при open-записи. `#else`-ветка TVF не задействована. | SQL Server `Pivot_*`, интеграционные `Pivot_ShouldReshapeDerivedSource` |
| `FindAlias`/`FindQueryCommand` после pop вложенной команды | Закрытая запись больше не удовлетворяет поиск; запись, добавленная текущей командой **после** возврата вложенной, остаётся open. `foundIdx`/`paramIdx` не сдвигаются закрытыми записями (проверка `closed` стоит до инкремента). | `NestedNonCorrelatedSubquery_ShouldResolveItsOwnInnerCommand`, `CorrelatedScalarOnJoinProjection_*` |
| `FindQueryCommand` в коррелированном скаляре проекции | Внутренний подзапрос ищет внешнюю запись при открытой объемлющей команде; закрытые кандидаты-сиблинги пропускаются. Регрессии нет. | `CorrelatedScalarInSelect_*`, `CorrelatedScalarOnJoinProjection_*`, `CorrelatedAggregateTerminalInSelect` |

### ℹ️ Наблюдения (фикс не требуется)

- **A. `#else`-ветка `DefaultColumnsProvider.cs:14-18` не компилируется ни одним TFM.** `nextorm.core.csproj:4` — `<TargetFramework>net10.0</TargetFramework>`, поэтому `NET8_0_OR_GREATER` всегда истинно и ветка `List<(Type, QueryCommand?, bool, bool)>` — мёртвый (непроверяемый сборкой) код; синхронность арности здесь держится только глазами. Фикс **обновил обе ветки корректно**, но будущая правка в `#else` не будет поймана `dotnet build`. Тот же паттерн — `DefaultAliasProvider.cs:16-20`, `ValueList.cs:5/162`. Пре-существующее; рекомендация (опционально): убрать `#if`/`#else` (репо одно-TFM) либо добавить TFM/compile-check, реально компилирующий `#else`.
- **B. `PopSourceScope` повторно сканирует уже закрытый суффикс.** Каждый pop идёт `[scopeStart, _list.Count)`; внешние pop'ы заново перебирают диапазоны, закрытые вложенными (запись не переписывается, но чтение есть). Суммарная работа на построение — сумма длин суффиксов (глубина вложенности × записи), без аллокаций; на практике глубина мала. Принято; при росте вложенности заменить на high-water mark на скоуп.
- **C. `FindQueryCommand` остаётся type-indexed.** Два источника с одинаковым CLR-типом результата (например, два производных запроса с идентичной формой анонимной проекции) по-прежнему дадут первый **open**-матч; флаг `closed` лишь убирает чужие закрытые кандидаты и не добавляет различения по позиции. Пре-существующее, не регрессия; отдельного номера не завожу.
- **D. Два «скоупа» в одном публичном интерфейсе.** `PushScope`/`PopScope` (вхождения lambda-параметров, `_scope`) и `PushSourceScope`/`PopSourceScope` (записи команд, `_sourceScopes`) — разные сущности; у пары source-скоупа доки есть (в этом фиксе `PopSourceScope` доуточнён), у `PushScope`/`PopScope`/`FindAlias`/`FindQueryCommand`/`Add`/`HasAliases` — нет. Пре-существующий пробел; кандидат в `API-NAMING-REVIEW.md` к Шагу 5, не к этому фиксу.

**Проверка (21.09.2026, выполнено в этом проходе).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. `dotnet run --project tests/<p> -c Release --no-build`: core **177/177**, sqlite **242/242**, sqlserver **235/235**, postgres **242/242**, mysql **63/63**, mariadb **28/28**, clickhouse **182/182** (0 failed везде). Integration без `DOCKER_HOST` (реально выполняется SQLite, остальные провайдеры skip): **Total 1008 / Failed 0 / Skipped 780** (выполнено 228); точечно `DerivedSourceWithJoinThenJoin_ShouldReturnData` — **1/1**, `SqlGenerationTests.DerivedSourceWithJoinThenJoin_ShouldResolveTheOuterAlias` (SQLite) — **1/1**. Замер размера записи: `Unsafe.SizeOf<(object,object,bool)>() == 24`, `Unsafe.SizeOf<(object,object,bool,bool)>() == 24` (фикс не растит inline-буфер `ValueList`). `rg` по изменённым файлам: подавлений/`Skip=`/`Task.Delay`/пустых `catch` — 0; EOL: `DefaultColumnsProvider.cs`/`IColumnsProvider.cs` — CRLF. Diff фикса локален двум файлам; тесты и остальная рабочая правка — в общем uncommitted-дереве (много несвязанных изменений, см. предыдущие разделы).

## 🔎 Точечный аудит — скалярный ключ GroupBy (21.09.2026)

**Область (uncommitted):** `Query/QueryCommand.QueryPreparer.cs` — `PrepareGrouping` (`:690-734`):
ветка `if (Body is NewExpression ctor && !IsSingleColumnType(ctor.Type)) {…} else throw new
InvalidOperationException("Only new expression is supported")` заменена на вызов общего
`BuildKeyColumns(cmd._groupExp, "GROUP BY", cancellationToken)` (`:646-688`; уже используется для
`DISTINCT ON`/`LIMIT BY`) и тот же цикл `PlanHashCode`/`GroupingPlanHash` (`:699-712`). Публичного
API нет (`PrepareGrouping`/`BuildKeyColumns` — `private static`; `GroupBy<TResult>` уже generic).
Тесты: `InMemoryTests.GroupBy_ScalarKey_ShouldAggregatePerGroup`,
`SqlGenerationTests.GroupBy_ScalarKey_ShouldMatchAnonymousKey` (sqlite+postgres),
`CommonTestSuite.GroupBy.TestGroup_ScalarKey`. Build Release **0/0**.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных и `IDisposable`-типов нет. |
| 2. Подавления | ✅ Фикс не добавил ни одного `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/пустого `catch`. Срез рабочего дерева: `src/` — **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma` (все с парным `restore`) → неоправданных **0/11**; `tests/` — 0 (шестой `SuppressMessage` — из несвязанных uncommitted-правок, не из этого фикса; база реестра была 5). |
| 3. LINQ / горячий путь | ✅ Правка — построение плана (раз на команду, не per-row); LINQ/аллокаций в новом коде нет, `SelectExpression[]` аллоцируется один раз, как и прежней веткой. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ Дублирование устранено: `GROUP BY` использует тот же expand-хелпер, что `DISTINCT ON`/`LIMIT BY`; ℹ️ ниже — `BuildKeyColumns` стал 3-м потребителем. |
| 6. Исключения | ✅ Новых `catch`/`throw`; удалён `throw new InvalidOperationException("Only new expression is supported")`. Для `GroupBy(e => new { })` `BuildKeyColumns` бросает `QueryPreparationException("GROUP BY requires at least one key column.")` вместо прежнего тихого пустого списка — fail-fast, не регресс. |
| 7. Хэш-ключи кэша | ✅ Паритет сохранён: анонимная ветка `BuildKeyColumns` (`:648-673`) — дословная копия удалённого блока (`Index=idx`, `PropertyName=ctorParam.Name!`, `Expression=args[idx]`), `PlanHashCode` считается тем же `SelectExpressionPlanEqualityComparer` в том же порядке → `GroupingPlanHash` для `new { x.Int }` не меняется. |

### ℹ️ Наблюдения (фикс не требуется)

- **`BuildKeyColumns` — теперь 3-й потребитель (`DISTINCT ON`/`LIMIT BY`/`GROUP BY`).** Прежнее
  наблюдение прямо предупреждало держать в виду расхождение: `BuildKeyColumns` не проставляет
  `SelectExpression.PlanHashCode`, а `PrepareGrouping` — проставляет после вызова (`:705-706`). Третий
  потребитель добавлен корректно: LIMIT BY/DISTINCT ON в `GroupingPlanHash` не входят (хэшируются
  напрямую в `QueryPlanEqualityComparer`), GROUP BY — входит и хэшируется тем же циклом, что и прежде.
- **Отмена безопасна.** Анонимная ветка `BuildKeyColumns` при отмене посреди цикла возвращает
  частично заполненный массив (`:660-661`), но `PrepareGrouping` перепроверяет тот же токен в начале
  каждой итерации до разыменования `columns[idx]` (`:701-702`) → `NullReferenceException` невозможен;
  скалярная ветка (`:675-687`) отмену не наблюдает, но так же ведёт себя и для `DISTINCT ON`/`LIMIT BY`.
- **`PropertyName`/`Index` для GROUP BY безопасны.** Единственный рендер-потребитель `_groupingList`
  — `SqlBuilder.cs:176-201`: `MakeColumn(..., alias:false)` использует только `Expression`/
  `PropertyType`; `PropertyName`/`Index` нужны лишь план-компаратору и детерминированы.
- **Скалярная и анонимная формы законно делят план.** `GroupBy(x => x.Int)` и
  `GroupBy(x => new { x.Int })` дают value-equal `SelectExpression` (Type/Index/PropertyName/
  Expression) → один план-ключ; SQL идентичен (закреплено тестом). Переименованный член
  (`new { Value = x.Int }`) даёт отдельный ключ при том же SQL — только промах кэша, не дефект.

### 🟡 Находка 51 — `tests/nextorm.integration.tests/CommonTestSuite.GroupBy.cs` целиком LF-only (пре-существует; файл затронут фичей)

Файл — **93/93 строк LF-only** (`file` → «ASCII text»; проверка LF-only счётчиком → 93/93),
`git diff` предупреждает «LF will be replaced by CRLF». Тот же класс, что Находки 20/22/29, но LF были
у файла **уже на HEAD `d21c473`** (`git show HEAD:…` → 80/80) — это **не регресс** скалярного GroupBy;
13 новых строк `TestGroup_ScalarKey` лишь унаследовали LF. Номер заведён, потому что файл изменён и
ранее в реестре не числился.

- **Стало:** нормализовать переводы строк файла в CRLF (`perl -pi`, замена одиночного LF на CRLF, как в Находках 20/22/29).
- **Проверка:** `file` → CRLF; счётчик LF-only → **0/93**.

**Проверка (21.09.2026, выполнено в этом проходе).** `dotnet build nextorm.sln -c Release` —
**0 warnings / 0 errors**. `dotnet run --project tests/<p> -c Release --no-build -noColor`: core
**178/178**, sqlite **243/243**, postgres **243/243** (0 failed). Integration без `DOCKER_HOST`
(выполняется только SQLite): `-method "*TestGroup_ScalarKey*"` — **Total 4 / Failed 0 / Skipped 3**.
Паттерн-скан: `#pragma warning disable` — 5 (все с `restore`), `SuppressMessage` — 6 (все с
`Justification`), `Skip=` — 0, пустых `catch` — 0, `Task.Delay` — 2 (обе `Task.Delay(0)` в
`InMemoryTests.cs:125,414`), `Thread.Sleep` — 0, `NoWarn` — только `CS1591` в 7 библиотечных `.csproj`,
`VersionOverride`/inline `Version` — 0. `slopwatch` локальным tool'ом не установлен
(`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен вручную.

## 🔎 Точечный аудит — квотирование идентификаторов (21.09.2026)

**Область (uncommitted):** `ISqlDialect.QuoteIdentifier` (DIM) + `SqlDialectBase`/`SqliteDialect`
override; флаг `bool QuoteIdentifiers` в `VisitorOptions`/`SqlBuildContext`; точки вывода
`BaseExpressionVisitor.AppendIdentifier`, `MemberTranslator`, `SqlSourceRenderer`; `DataContextBuilder`/
`IContextEnvironment`/`DataContext`; `EntityBuilder<TEntity>`/`EntityBuilder`/`QueryCommand`/
`QueryCommand<TResult>`; план-ключ `QueryPlanEqualityComparer` (+`ResolvedQuoteIdentifiers`, `CopyTo`).
Build Release **0/0**; новые SQL-gen тесты — sqlite **9/9**, sqlserver **2/2**.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных и `IDisposable`-типов нет. |
| 2. Подавления | ✅ Фикс не добавил ни одного `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/пустого `catch`. Срез: `src/` — **5** `#pragma` (все с парным `restore`, 5/5) + **6** `SuppressMessage` (все с `Justification`) = **11/11 оправданных, 0 неоправданных**; новых — **0**. |
| 3. LINQ / горячий путь | ✅ `AppendIdentifier` — один `if` по record-свойству + (при включённом флаге) конкатенация строки; LINQ/лямбд/аллокаций в выключенном (default) режиме нет. `bool` в `SqlBuildContext` (readonly struct) и `VisitorOptions` — план строится раз на команду, не per-row. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ Квотирование централизовано в `AppendIdentifier`/`QuoteIdentifier`; точки вывода не дублируют логику. ℹ️ `VisitorOptions` — 13 позиционных параметров (см. наблюдение F). |
| 6. Исключения | ✅ Новых `catch`/`throw` нет. |
| 7. Хэш-ключи кэша | ⚠️ `ResolvedQuoteIdentifiers` входит в `Equals` (`:138`) и hash (`:423`) согласованно; `CopyTo` копирует и `QuoteIdentifiers`, и `ResolvedQuoteIdentifiers` (`Clone.cs:33-34`) → DEBUG-assert `QueryPlan.GetCacheVersion` держится. Render вложенных команд берёт флаг **корня**, а hash — **свой** (наблюдение B); неверного разделения планов нет. |

### ✅ Находка 52 — schema-qualified имена квотируются по сегментам (ИСПРАВЛЕНА 21.09.2026; была P1-кандидат)

> **Закрыто 21.09.2026 (HEAD `2a2dfa6`).** Введён `SqlSourceRenderer.QuoteQualifiedIdentifier` (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:463`), применяемый к `from.Table` (`:217`) и CTE (`:63`); `Sales.SalesOrderHeader` → `[Sales].[SalesOrderHeader]`. Покрытие — тесты `QuoteIdentifiers`/`QuotedIdentifiers` в `tests/nextorm.*.tests/SqlGenerationTests.cs`.

`SqlSourceRenderer.cs:213` (`MakeFrom`) и объявление CTE (`:63`) оборачивают `from.Table`/`cte.Name`
**целиком**. Для `[SqlTable("Sales.SalesOrderHeader")]` при включённом флаге получается
`[Sales.SalesOrderHeader]` — один идентификатор вместо `[Sales].[SalesOrderHeader]`; то же для
PostgreSQL `"bookings.airports_data"` и ClickHouse `` `datasets.hits_v1` ``. Схемно-квалифицированные
имена используются в `examples/**` (MSSQL adventureworks, PostgreSQL aviasales, ClickHouse analytics) и
обычны в реальных БД — критерий приёмки RFC («флаг включает квотирование и не ломает запросы») для них
не выполняется. RFC сам помечает это открытым вопросом №3 (`docs/specs/roadmap/todo_identifier_quoting.md:66`),
тестами не покрыто.

- **Было:** `sqlBuilder.Append(ctx.QuoteIdentifiers ? ctx.Dialect.QuoteIdentifier(from.Table) : from.Table);`
- **Стало (предложение):** разбирать имя по `.` и квотировать каждый сегмент отдельно (с
  провайдерным разделителем схемы), либо явно отклонять (`NotSupportedException`/guard) при включённом
  флаге и наличии `.`, задокументировав ограничение.
- **Проверка:** SQL-gen тест `UseQuotedIdentifiers` на сущности с `[SqlTable("s.t")]` → `[s].[t]`
  (и `"s"."t"` для PG/SQLite).

### ✅ Находка 53 — внутренний разделитель идентификатора удваивается (ИСПРАВЛЕНА 21.09.2026; была P1-кандидат)

> **Закрыто 21.09.2026 (HEAD `2a2dfa6`).** `QuoteIdentifier` удваивает разделитель: default `ISqlDialect.cs:581`/`SqlDialectBase.cs:271` (`"` → `""`), SQL Server `SqlServerDialect.cs:28` (`]` → `]]`), MySQL `MySqlDialect.cs:156` и ClickHouse `ClickHouseDialect.cs:303` (`` ` `` → ` `` `).

Матрица RFC (`todo_identifier_quoting.md:25-30`) требует удвоения внутреннего разделителя
(PostgreSQL `"`, SQL Server `]`, MySQL/MariaDB/ClickHouse `` ` ``). Реализация — простая конкатенация:
default `ISqlDialect.cs:576`, `SqlDialectBase.cs:269` (=> `Escape`), `PostgresDialect.cs:26`,
`SqlServerDialect.cs:25`, `MySqlDialect.cs:153`, `ClickHouseDialect.cs:300`. Имя `we"ird` на PG даёт
`"we"ird"` (невалидно), `a]b` на MSSQL — `[a]b]`.

- **Было:** `"\"" + keyword + "\""` (и аналоги в `Escape`).
- **Стало:** удваивать вхождение разделителя (например `keyword.Replace("\"", "\"\"")`) в
  `QuoteIdentifier` (и отдельно решить судьбу `Escape` — см. `API-NAMING-REVIEW.md`, P2-1).
- **Проверка:** dialect-тест `QuoteIdentifier("a\"b")` → `"a""b"`; `QuoteIdentifier("a]b")` → `[a]]b]`.
- **Примечание:** реальный риск ниже, чем у Находки 52: имена приходят из `[SqlTable]`/`[Column]`
  (доверенный код), а не из пользовательского ввода; поэтому P1-кандидат, не P0. На SQLite внутренние
  `"` в физическом имени встречаются редко, но контракт матрицы не выполнен.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `AppendIdentifier` не сторожит `null`/пустую строку.** `BaseExpressionVisitor.cs:87-93`: при
  включённом флаге `QuoteIdentifier(null/empty)` → `""` (пустой квотированный идентификатор), тогда как
  выключенный режим печатает пусто. Call-site `:218` сам сворачивает `null` в `string.Empty`
  (`constExp.Value?.ToString() ?? string.Empty`). Null-аргументы `TableAlias`-аксессоров маловероятны;
  для робастности — guard `if (string.IsNullOrEmpty(name)) return;`.
- **B. План-ключ вложенных команд расходится с рендером.** CTE/UNION/subquery рендерятся с флагом
  **корня** (`SqlSourceRenderer.cs:44,68`, `SqlBuilder.cs:299` — `ctx with {…}` сохраняет
  `QuoteIdentifiers`), а `UnionPlanHash`/`ReferencedQueriesPlanHash`/`CtesPlanHash` хэшируют
  **собственный** `ResolvedQuoteIdentifiers` вложенной команды (`QueryCommand.QueryPreparer.cs:84,93,100,179`).
  Следствие: per-command `WithQuotedIdentifiers` на вложенной команде молча игнорируется рендером, но
  меняет ключ → лишние промахи кэша. Неверного разделения планов нет (корневой флаг в ключе тоже есть).
  Кандидат: либо резолвить флаг вложенной команды при рендере, либо не включать его в хэш вложенных.
- **C. `ResetPreparation` не сбрасывает `ResolvedQuoteIdentifiers`.** `QueryCommand.cs:386-406`: после
  `WithQuotedIdentifiers` (`QueryCommand.TResult.cs:360`) клон хранит устаревшее resolved-значение до
  следующего `PrepareCommand`. Ошибочного пути не найдено: `QueryPlanner.GetPreparedQueryCommand`
  вызывает `PrepareCommand` **до** построения `QueryPlan` (`QueryPlanner.cs:83,90`), а `CopyTo` копирует
  resolved (`Clone.cs:34`), поэтому DEBUG-assert `QueryPlan.GetCacheVersion` держится. Гигиена:
  сбрасывать в `ResetPreparation` либо заменить поле вычисляемым свойством.
- **D. Имена окон не квотируются.** `SqlBuilder.cs:273` печатает `window.Name` без кавычек.
  `WindowSql.IsValidWindowName` (`Visitors/WindowSql.cs:43-63`) допускает зарезервированные слова
  (`window select as (…)` → невалидный SQL) независимо от флага — **пре-существующее**, вне объёма v1
  (RFC перечисляет только имена функций, `todo_identifier_quoting.md:65`). CTE-колоночных алиасов в
  модели нет (`CteDefinition` — только `Name`, `Column`-списка нет), имена функций намеренно вне объёма.
- **E. `QuoteIdentifier` на включённом флаге аллоцирует строку на идентификатор** (конкатенация);
  режим opt-in, план строится раз на команду/кэш — не per-row. Не запах.
- **F. `VisitorOptions` — 13 позиционных параметров** (`VisitorOptions.cs:16-29`), добавлен 13-й
  (`QuoteIdentifiers`). Пре-существующий длинный список (record публичный); в репо 4 конструирования,
  все `internal`. Альтернатива — свойство в теле record — разобрана в `API-NAMING-REVIEW.md` P2-2.

**Проверка (21.09.2026, выполнено в этом проходе).** `dotnet build nextorm.sln -c Release` —
**0 warnings / 0 errors**. `dotnet run --project tests/nextorm.sqlite.tests -c Release --no-build --
-noColor -method "*QuotedIdentifiers*"` — **9/9**; то же для `nextorm.sqlserver.tests` — **2/2**
(0 failed). Паттерн-скан: `#pragma warning disable` — 5 (все с парным `restore`), `SuppressMessage` —
6 (все с `Justification`), `Skip=` — 0, пустых `catch` — 0, `Thread.Sleep` — 0, `Task.Delay` — 2
(обе `Task.Delay(0)` в `tests/nextorm.core.tests/InMemoryTests.cs:125,414`), `NoWarn` — только `CS1591`
в 7 библиотечных `.csproj`, `VersionOverride`/inline `Version` — 0. `.editorconfig` — 7
`dotnet_diagnostic.*.severity`, все `silent` (S125/S108/S3060/S1104/S3604/S2292 + CA2254),
`SonarAnalyzer` не подключён (записи `S*` инертны). `slopwatch` локальным tool'ом не установлен
(`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен вручную.

## 🔎 Точечный аудит — соглашения об именовании (snake_case) (21.09.2026)

**Область (uncommitted):** новые публичные типы `INamingConvention` (`DataContext/Meta/INamingConvention.cs`) и
`SnakeCaseNamingConvention` (`DataContext/Meta/SnakeCaseNamingConvention.cs`); `UseNamingConvention` на
`DataContextBuilder`, `NamingConvention` на `IContextEnvironment` (DIM `=> null`)/`ContextEnvironment`/
`DataContext`; `IsTableNameAuto`/`IsColumnNameAuto` в `IEntityMetadata`/`IPropertyMetadata`;
`WithNamingConvention` на `EntityBuilder<TEntity>`/`EntityBuilder`/`QueryCommand<TResult>`;
`ResolvedNamingConvention` в `QueryCommand.QueryPreparer.Prepare` (`:28`) и план-ключе
`QueryPlanEqualityComparer` (`:140,427`); `MemberInfoExtensions.GetPropertyColumnName` (ключ кэша
`(PropertyInfo, INamingConvention?)`, `:11,22`); флаги `FromExpression.IsAutoMapped`/`SourceIsInterface`
(`:34,36`); render-time трансляция в `SqlSourceRenderer.MakeFrom` (`:214`) и `MemberTranslator` (4 call-site).
Build Release **0/0**.

| Категория навыка | Результат |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных и `IDisposable`-типов нет; `using var visitor` не менялся. |
| 2. Подавления | ✅ Фикс не добавил ни одного `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/пустого `catch`. Срез: `src/` — **5** `#pragma` (все с парным `restore`, 5/5) + **6** `SuppressMessage` (все с `Justification`) = **11/11 оправданных, 0 неоправданных**; новых — **0**. |
| 3. LINQ / горячий путь | ✅ `ToSnakeCase` — цикл + `StringBuilder`, без LINQ/лямбд; трансляция выполняется на cache-miss `_columnNames`, не per-row; имя таблицы транслируется при построении плана (кэшируется). Аллокация `StringBuilder` — только на промахе/плане, режим opt-in. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ Логика централизована (`SnakeCaseNamingConvention` + `INamingConvention`, render-time в `MakeFrom`/`AppendIdentifier`); `EntityBuilder`/`JoinedEntityBuilder` лишь пробрасывают флаг. ℹ️ `EntityBuilder.cs` 1470 строк — pre-existing god-class (не усугублён: +~30 строк). |
| 6. Исключения | ✅ Новых `catch`/`throw` нет. |
| 7. Хэш-ключи кэша | ⚠️ `_columnNames` — статический словарь, ключ включает произвольный экземпляр `INamingConvention` (Находка 54); план-ключ `ReferenceEquals`+`hash.Add` корректен (корневой `ResolvedNamingConvention` резолвится до `QueryPlan`); `_fromCache` (Type→`FromExpression`) корректен — конвенция НЕ «запечена», применяется на рендере (Находка 55 — вложенные команды). |

### 🟡 Находка 54 — статический `_columnNames` растёт по экземплярам конвенции и залипает на изменяемой конвенции (ОТКРЫТА; P2)

`MemberInfoExtensions.cs:11` — `static readonly ConcurrentDictionary<(PropertyInfo, INamingConvention?), string>`,
никогда не очищается. Ключ включает **произвольный экземпляр** `INamingConvention`; `UseNamingConvention`/
`WithNamingConvention` принимают любой объект. Если потребитель создаёт конвенцию на контекст/запрос
(`new SnakeCaseNamingConvention()` вместо `.Instance`), словарь бесконечно растёт
(`кол-во PropertyInfo × кол-во экземпляров`, процесс-wide; `DataContextCache.Metadata` не очищает —
`DataContextCache.cs:22-26`), а `QueryPlanEqualityComparer` (`ReferenceEquals`, `:140`) заодно фрагментирует
план-кэш. Кроме того конвенция с `Equals`, отличным от ссылочного (record/`IEquatable`), даёт **разные**
план-ключи, но **общий** ключ `_columnNames` — рассогласование семантики равенства; изменяемая конвенция
отдаёт залипшее имя.

- **Было:** `ConcurrentDictionary<PropertyInfo, string>` — один вход на `PropertyInfo` (процесс-wide).
- **Стало (предложение):** ограничить кэш стабильными конвенциями: либо ключевать по
  `(PropertyInfo, convention?.GetType())` для встроенных/безсостояниевых, либо вынести резолв в
  метаданные/`FromExpression`, либо явно задокументировать «`INamingConvention` должен быть
  stateless и стабильный (singleton)», плюс согласовать `Equals`/`ReferenceEquals` с план-ключом.
- **Проверка:** тест/бенч: 1000 разных экземпляров `new SnakeCaseNamingConvention()` → `_columnNames`
  растёт линейно (фиксирует утечку) / не растёт при ключе-по-типу; контракт задокументирован.

### 🟡 Находка 55 — `WithNamingConvention` вложенной команды игнорируется рендером, но входит в хэш (ОТКРЫТА; P2)

Вложенные команды (CTE/subquery/UNION) рендерятся с конвенцией **корня**: `SqlSourceRenderer.cs:44` и
`:68` (`new SqlBuilder(ctx with {…})` сохраняет `NamingConvention`), `:256` (`new SqlBuilder(in ctx)` —
`ctx` корня, не `cmd.ResolvedNamingConvention`); см. также `QueryPlanner.MakeSelect` (`:73`). При этом
`UnionPlanHash`/`ReferencedQueriesPlanHash`/`CtesPlanHash` хэшируют **собственный**
`ResolvedNamingConvention` вложенной команды (`QueryCommand.QueryPreparer.cs` — рекурсивный
`GetQueryPlanEqualityComparer().GetHashCode(...)`). Следствие: `WithNamingConvention(...)` на вложенной
команде молча не применяется (в отличие от корневой), но меняет ключ → лишние промахи кэша. Неверного
разделения планов нет (корневой `ResolvedNamingConvention` в ключе тоже есть). Тот же паттерн, что и
наблюдение B аудита квотирования.

- **Было (эскиз):** `var sql = new SqlBuilder(in ctx).MakeSelect(cmd);` для `from.SubQuery`.
- **Стало (предложение):** либо резолвить конвенцию вложенной команды при рендере
  (`ctx with { NamingConvention = cmd.ResolvedNamingConvention }`), либо не включать resolved-значение
  вложенной команды в `QueryPlanEqualityComparer` (использовать корневой).
- **Проверка:** SQL-gen тест: вложенный подзапрос с `WithNamingConvention(X)` под корнем без конвенции
  → имя таблицы/колонки транслируется (или документировать, что override только корневой).

### ℹ️ Наблюдения (фикс не требуется)

- **A. `ResetPreparation` не сбрасывает `ResolvedNamingConvention`.** `QueryCommand<TResult>.WithNamingConvention`
  (`QueryCommand.TResult.cs:374`) — `Clone()` → `ResetPreparation()` → set `NamingConvention`;
  `ResolvedNamingConvention` (`QueryCommand.cs:332`) остаётся от прежнего состояния до следующего
  `PrepareCommand`. Ошибочного пути не найдено: `Prepare` перезаписывает resolved (`QueryPreparer.cs:28`) до
  `new QueryPlan` (`QueryPlanner.cs:84,91`), `Clone` копирует оба (`QueryCommand.Clone.cs:33-34`). Тот же
  паттерн, что и наблюдение C квотирования; гигиена — сбрасывать либо вычислять.
- **B. `Equals` vs `ReferenceEquals` для конвенции.** План-ключ — `ReferenceEquals`
  (`QueryPlanEqualityComparer.cs:140`), кэш имён — `ValueTuple` → `Equals` (`MemberInfoExtensions.cs:22`).
  Для встроенного `SnakeCaseNamingConvention` (без override) это одно и то же; для value-equal
  пользовательской конвенции — рассогласование (учтено в Находке 54).
- **C. `_fromCache` (`QueryPlanner.cs:209`, Type→`FromExpression`) корректен при смене конвенции.**
  `FromExpression` хранит только `IsAutoMapped`/`SourceIsInterface` (факты метаданных), а `TableName`
  трансформируется на рендере (`SqlSourceRenderer.cs:214`) — один `FromExpression` на тип
  переиспользуется всеми конвенциями, «запекания» нет.
- **D. `IsColumnNameAuto`/`IsTableNameAuto` проставляются во всех ветках.** `AutoBuildProperties` —
  `false` при `[Column]` (класс и интерфейс), `true` иначе; `AutoBuildTableName` — `false` при
  `[SqlTable]`/`[Table]` (класс и интерфейс), `true` иначе; fluent `Table(string)`
  (`EntityMetadataBuilder.cs:136`) и `HasColumnName` → `false`. Явные имена конвенцией не трогаются.
- **E. `SnakeCaseNamingConvention` — без BCL-конфликта, namespace `NextORM.Core`**; `Instance` — singleton,
  `ToSnakeCase` — `internal static`, LINQ-free. Тесты `SnakeCaseNamingConventionTests` (14 кейсов) покрывают
  акронимы (`OrderID`→`order_id`, `HTTPServer`→`http_server`), интерфейсный `I`-дроп и граничный `IO`→`o`.

**Проверка (21.09.2026, выполнено в этом проходе).** `dotnet build nextorm.sln -c Release` — **0 warnings /
0 errors**. Паттерн-скан: `#pragma warning disable` — 5 (все с парным `restore`), `SuppressMessage` — 6
(все с `Justification`), `Skip=` — 0, пустых `catch` — 0, `Thread.Sleep` — 0, `Task.Delay` — 2 (обе
`Task.Delay(0)` в `tests/nextorm.core.tests/InMemoryTests.cs:125,414`), `NoWarn` — только `CS1591` в 7
библиотечных `.csproj`, `VersionOverride`/inline `Version` — 0, `PublicAPI.*.txt` — 0. `.editorconfig` — 7
`dotnet_diagnostic.*.severity`, все `silent` (S125/S108/S3060/S1104/S3604/S2292 + CA2254), `SonarAnalyzer`
не подключён (записи `S*` инертны). `slopwatch` локальным tool'ом не установлен
(`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен вручную.

## 🔎 Точечный аудит — query hints PostgreSQL/MySQL (`/*+ ... */`) (22.09.2026)

**Область (uncommitted):** `src/nextorm.postgres/PostgresDialect.cs` — `SupportsQueryHints` (`:41`),
`RenderQueryHints` (`:48-53`); `src/nextorm.mysql/MySqlDialect.cs` — `:26`, `:33-38` (MariaDB наследует);
тесты `tests/nextorm.{postgres,mysql,mariadb}.tests/*` и `tests/nextorm.integration.tests/{Postgres,MySql}SpecificTests.cs`;
доки EN+RU (`docs/guide/17-query-hints.md`, `docs/advanced/limitations.md`) и матрицы
`docs/specs/comparison/*`; RFC `docs/specs/roadmap/todo_query_hints_providers.md` удалён, §4 п.16
`sql-capabilities-gap-analysis.md` переведён в «Shipped». Новых публичных имён нет — переопределены уже
существующие члены `ISqlDialect`/`SqlDialectBase`; XML-`<summary>` у обоих override есть.

| Категория навыка | Вердикт |
|---|---|
| 1. `IDisposable` | ✅ Не затронуто: новых disposable-полей/локальных и `IDisposable`-типов нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/пустых `catch` — **0**. В `src/`: **5** `#pragma` (все с парным `restore`) + **6** `SuppressMessage` (все с `Justification`) → неоправданных **0/11**; `Skip=` — 0, `Task.Delay` — 2 (обе `Task.Delay(0)`-yield в `tests/nextorm.core.tests/InMemoryTests.cs`), `Thread.Sleep` — 0, пустых `catch` — 0. Соотношение не изменилось. |
| 3. LINQ / горячий путь | ✅ LINQ нет; `IndexOf`/`Insert`/`string.Join` — на построении SQL (план-кэшируется, не per-row); аллокации того же порядка, что интерполяция SQL Server-хука. См. ℹ️ B. |
| 4. События | ✅ Не затронуто. |
| 5. Проектирование | ✅ **Находка 56 исправлена 22.09.2026** (позиция комментария теперь ищется по верхнеуровневому `select` с учётом скобок/кавычек; во время фикса найден и закрыт смежный баг ядра `Hint`). ℹ️ A — тело метода по-прежнему продублировано в двух диалектах (принято: план запрещает расширять публичную поверхность `protected`-хелпером). |
| 6. Исключения | ✅ Новых `catch`/`throw` нет; `Hint` на SQLite/ClickHouse по-прежнему бросает `NotSupportedException` (`SqlBuilder.cs:492-493`), негативные тесты сохранены. |
| 7. Хэш-ключи кэша | ✅ Хинты уже участвуют в план-ключе (регресс-тест `QueryHint_ShouldNotReuseThePlanOfAnUnhintedCommand`, `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:730-741`); фича — рендер-онли, состав `QueryPlanEqualityComparer` не менялся. |

### ✅ Находка 56 — позиция inline-комментария берётся `IndexOf("select")`, при `WITH`-запросе хинт попадает не в тот query block (ИСПРАВЛЕНА 22.09.2026; P1)

Оба диалекта (`PostgresDialect.cs:50-52`, `MySqlDialect.cs:35-37`) ищут точку вставки как
`sql.IndexOf("select", StringComparison.OrdinalIgnoreCase)` — первое **литеральное** вхождение подстроки.
Для CTE-запроса `SqlBuilder` приставляет `withClause` перед готовым `select …` (`SqlBuilder.cs:466-467`,
`SqlSourceRenderer.MakeWithClause:22-80`), поэтому первой находится `select` **внутри тела CTE**, а не
верхнеуровневый. Комментарий уезжает внутрь CTE: MySQL применит optimizer hint к этому query block, а
`pg_hint_plan` читает комментарий сразу после первого ключевого слова инструкции и такой хинт не увидит —
т.е. для CTE-запросов хинт молча не срабатывает. Опаснее второй случай: если первый идентификатор до
`select` содержит подстроку `select` (CTE с именем `selected`, `select_ids`, …), `start + 6` разрывает
идентификатор и рендерится синтаксически невалидный SQL (`with select /*+ … */ed as (…)`).

CTE + `Hint` — поддерживаемая комбинация: SQL Server покрывает её тестом
`QueryHint_WithRecursiveCte_ShouldMergeIntoOneOptionClause` (`tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:714-727`);
у PG/MySQL аналогичного теста нет (все новые тесты — на одиночный `select`), т.е. путь не покрыт.

- **Было:** `const string keyword = "select"; var start = sql.IndexOf(keyword, OrdinalIgnoreCase); return start < 0 ? sql : sql.Insert(start + keyword.Length, $" /*+ {string.Join(" ", hints)} */");`
- **Стало (предложение):** вычислять позицию по ведущему ключевому слову инструкции (пропустив префикс `with …`), а не по первому вхождению подстроки, и вынести общий helper (ℹ️ A); добавить SQL-gen тесты PG/MySQL `WithRecursiveCte … Hint` (хинт сразу после верхнеуровневого `select`) и негатив на CTE-имя, содержащее `select`.
- **Проверка:** SQL-gen тест с CTE + `Hint` ассертит позицию комментария и целостность идентификатора; тест «CTE-имя `selected`» не порождает невалидный SQL.

- **Исправление (22.09.2026):** `RenderQueryHints` обоих диалектов заменён на сканер: он идёт по строке, отслеживает глубину `(...)` и пропускает кавычки (`'`, `"`, `` ` ``), вставляя `/*+ … */` после первого `select` на нулевой глубине с проверкой границ идентификатора. Добавлены ассерты диалекта на CTE и на CTE-имя `selected`, а также SQL-gen тесты `QueryHint_WithCte_ShouldPlaceHintAfterTheTopLevelSelect` (PG/MySQL). Во время проверки CTE-кейса найден и закрыт **смежный баг ядра:** `QueryCommand<TResult>.Hint` клонирует команду и вызывает `ResetPreparation()`, который обнуляет `_from`; для источника-`TableAlias` (CTE/производный запрос) `GetFrom` вернуть его не может, поэтому хинт **терял `FROM`** (`with recent as (...) select /*+ … */ id` без `from recent`). `Hint` теперь сохраняет `_from` клона до `ResetPreparation` и восстанавливает его после. Регресс-гейт: core 192/192, sqlite 268/268, postgres 249/249, sqlserver 238/238, mysql 67/67, mariadb 30/30, clickhouse 183/183; `dotnet build -c Release` 0/0. Прочие clone+reset методы (`WithQuotedIdentifiers`/`WithNamingConvention`) сохраняют тот же баг источника — вынесены на следующий аудит.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Тело `RenderQueryHints` продублировано между диалектами.** `PostgresDialect.cs:48-53` и
  `MySqlDialect.cs:33-38` совпадают построчно (различаются только XML-доки). Семейство «комментарных»
  хинтов лучше держать в одном `protected`/`internal` helper'е `SqlDialectBase` рядом с базовым
  `RenderQueryHints` (`SqlDialectBase.cs:515-516`) — убирает копию и позволяет починить Находку 56 в
  одном месте. Не блокер.
- **B. Аллокации на построении SQL.** `string.Join(" ", hints)` + `sql.Insert(...)` дают две новые строки
  на команду с хинтами; вызывается однократно на форму запроса (план-кэш, `SqlBuilder.cs:489-499`), не на
  строку/колонку. На фоне `StringBuilder`-сборки всего SQL и интерполяции SQL Server-хука — не горячий
  путь. Принято.
- **C. `maxRecursionOption` фактически всегда `null`.** `MakeMaxRecursion` переопределён только в SQL Server
  (`SqlDialectBase.cs:258` → `null`), поэтому игнорирование параметра в PG/MySQL ничего не теряет. Если
  появится диалект с recursion-опцией, контракт `RenderQueryHints` обяжет его учесть.
- **D. `SupportsQueryHints => true` для PostgreSQL при отсутствии расширения.** `pg_hint_plan` — не ядро;
  без расширения `/*+ … */` — обычный комментарий, поэтому флаг не вводит в заблуждение и оговорён в
  XML-доке и `docs/guide/17-query-hints.md`. Принято.

**Проверка (22.09.2026, выполнено в этом проходе).** `dotnet build nextorm.sln -c Release` — **0 warnings /
0 errors**. `dotnet run --project tests/<p> -c Release --no-build`: postgres **248/248**, mysql **66/66**,
mariadb **30/30** (0 failed, 0 skipped). Паттерн-скан: `#pragma`/`SuppressMessage`/`Skip=`/`Task.Delay`/
пустых `catch` новых — **0**; EOL: оба диалекта и все правленые тесты — CRLF (согласовано с `AGENTS.md`).
`PublicAPI.*.txt` — по-прежнему 0 (Шаг 5 открыт); новых имён нет, добавлены **4** `override` уже
существующих членов (по паре `SupportsQueryHints`+`RenderQueryHints` в PostgreSQL и MySQL; MariaDB
наследует). `slopwatch` локальным tool'ом не установлен — скан выполнен вручную.

## 🔎 Точечный аудит — PostgreSQL `ts_rank_cd` + `[SqlTableFunction]` `CallClause`/`VerbatimArguments` (22.09.2026)

Область (uncommitted, worktree `tvf-expansion`): `Query/SqlFunctions.cs` (`IKeyRankRow<TKey>`),
`Query/SqlFunctions.SqlServer.cs` (`containstable<TKey>`/`freetexttable<TKey>`),
`Query/SqlFunctions.Postgres.cs` (`ts_rank_cd`), `Visitors/ExtendedScalarFunctionTranslator.cs`,
`SqlTableFunctionAttribute.cs` (`CallClause`/`VerbatimArguments`), `Expressions/TableFunctionExpression.cs`,
`DataContext/SqlSourceRenderer.cs` (`MakeTableFunction`/`IsVerbatimArgument`/`GetVerbatimArgument`),
`src/nextorm.sqlserver/SqlServerDialect.cs`; тесты sqlserver/postgres/mysql. Build Release — **0/0**
(прогнано в этом проходе); тесты Release `--no-build` — core **192/192**, sqlserver **240/240**,
postgres **249/249**, mysql **66/66** (0 failed, 0 skipped).

| Категория навыка | Результат |
|---|---|
| 2. Подавления | ✅ В диффе `src`+`tests` новых `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**. База прежняя: 6 `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + 5 `#pragma disable` (все с парным `restore`) = **11/11 оправданных, 0 неоправданных**. |
| Слоп-паттерны (slopwatch локальным tool'ом не установлен; скан вручную) | ✅ `Skip=`/`Ignore` — 0; пустых `catch` — 0; `Thread.Sleep` — 0; `Task.Delay` — 2, обе прежние `Task.Delay(0)`-yield (`tests/nextorm.core.tests/InMemoryTests.cs`); инлайновых `Version`/`VersionOverride`/CPM-bypass — 0; `*.dump`/мусорных артефактов нет. |
| 1/4/6. `IDisposable`, события, исключения | ✅ Новых disposable-полей/подписок нет; новые `throw` — `NotSupportedException` (`SqlSourceRenderer.cs:389`, `SqlFunctions.SqlServer.cs:100,107`) без `catch`. |
| 3. LINQ на горячем пути | ✅ `IsVerbatimArgument` (`SqlSourceRenderer.cs:371-384`) — явный цикл по `int[]`, без LINQ и без аллокаций; вызывается 2× на аргумент. Находка 3 держится. |
| 5. Проектирование | 🟡 Находка 56 (новый публичный 6-параметрический ctor). |
| 7. Хэш-ключи кэша | ✅ Нового per-instance состояния нет: `CallClause`/`VerbatimArguments` — константы атрибута метода, resolved `FROM` остаётся чистой функцией вызова; план-ключ не затронут. |
| EOL (AGENTS.md: CRLF) | ✅ Все изменённые `.cs` (`src`+`tests`) — CRLF-only, LF-строк **0** (проверено `perl -ne 'print if /(?<!\r)\n$/'`); Находки 20/22/29 не повторяются. |

### 🟡 Находка 56 — новый публичный 6-параметрический ctor `TableFunctionExpression` (ОТКРЫТА, P2)

- **Было:** `new TableFunctionExpression(name, schema, withClause, callClause, verbatimArguments, call)`
  (`Expressions/TableFunctionExpression.cs:26`) — 6 позиционных параметров, публичный; порог реестра «≥6»
  превышен (ср. `VisitorOptions` P2-2 и `FromExpression` в API-реестре).
- **Стало:** сузить широчайший ctor до `internal` — единственный потребитель в решении это фабрика
  `TableFunctionExpression.Create` (`:82`), внешних call-site нет (`roslyn refs` — 3 ссылки, все
  внутри `TableFunctionExpression.cs`); либо ввести параметр-объект. Публичные 3-/4-арг. ctor'ы не трогать.
- **Импакт:** косметика публичной поверхности (alpha; обратная совместимость не гарантируется), сборкой не
  гейтится.
- **Проверка:** build 0/0; публичная поверхность без 6-арг. ctor (или с параметром-объектом).

### ℹ️ Наблюдения (фикс не требуется)

- **A. Param-mode и порядок аргументов согласованы.** Первый проход `MakeTableFunction`
  (`SqlSourceRenderer.cs:301-308`) пропускает verbatim-аргументы, второй (`:318-332`) рендерит их сырым
  текстом; порядок параметров совпадает. Тесты фиксируют params `["search"]` (`containstable`/
  `freetexttable`) и `["doc"]` (`JSON_TABLE`).
- **B. `GetVerbatimArgument` — явный fail-fast.** Не-константный verbatim-аргумент (например, захваченная
  локальная переменная с именем таблицы) бросает `NotSupportedException` с понятным текстом
  (`SqlSourceRenderer.cs:386-389`), а не выдаёт небезопасный SQL. Ограничение задокументировано на
  `SqlTableFunctionAttribute.VerbatimArguments`; док-зазор у встроенных методов — в API-реестре (TFV1).
- **C. Свойство-массив `SqlTableFunctionAttribute.VerbatimArguments` (`int[]?`) — изменяемая ссылка.**
  Для атрибутов риск низкий (значение задаётся в метаданных), но при желании — `IReadOnlyList<int>?`
  или защитная копия в `Create`. Косметика.
- **D. `SupportsTableFunction` — строковый гейт по имени.** SQL Server добавил `"containstable"`/
  `"freetexttable"` (`SqlServerDialect.cs:76-77`); контрактный тест `SupportsTableFunction_…` расширен, а
  `BuiltInTableFunction_Containstable_ShouldThrowOnPostgres` подтверждает отказ на чужом провайдере.

**Проверка:** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**;
`dotnet test tests/nextorm.{core,sqlserver,postgres,mysql}.tests -c Release --no-build` — core **192/192**,
sqlserver **240/240**, postgres **249/249**, mysql **66/66** (0 failed, 0 skipped); в диффе новых
`#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; `find -name 'PublicAPI*.txt'`
— пусто; LF-only строк в изменённых `.cs` — **0**. Публичный API-разбор — в `API-NAMING-REVIEW.md`
(раздел 22.09.2026, TFV1/TFV2). **Актуализация 22.09.2026:** оба P2 закрыты (TFV1 — док-предупреждение у
`CallClause`; TFV2 — ctor понижен до `internal`); повторная сборка/тесты — 0/0 и 747/747.

## 🔎 Точечный аудит — композируемый сырой SQL (`FromSql`) (22.09.2026, uncommitted worktree `composable-raw-sql`)

**Область:** `src/nextorm.core/Expressions/RawSqlSourceExpression.cs` (новый, `internal`),
`Expressions/FromExpression.cs` (поле + ctor + `CloneForCache`), `DataContext/SqlSourceRenderer.cs`
(`MakeRawSqlSource`), `DataContext/DataContextExtensions.cs` (`FromSql`),
`DataContext/Dialect/ISqlDialect.cs` + `SqlDialectBase.cs` + 6 диалектов (`SupportsRawSqlSource`).
Build Release — **0 warnings / 0 errors**; тесты — core 192, postgres 249, sqlserver 240, mysql 67,
mariadb 29, sqlite 270, clickhouse 185 = **1232/1232, 0 failed, 0 skipped**.

| Категория навыка | Результат |
|---|---|
| 1. Управление ресурсами (`IDisposable`) | ✅ Не затронуто; новых disposable-полей/локальных нет. |
| 2. Подавления предупреждений | ✅ Новых нет; соотношение проекта не изменилось — **11/11 оправданных** (6 `SuppressMessage` + 5 `#pragma`), неоправданных — 0. |
| 3. Антипаттерны LINQ | 🟡 **Находка 58** — рефлексия по объекту-параметру на каждом рендере/исполнении кэш-плана (LINQ-free, но аллокации). |
| 4. Работа с событиями | ✅ Не затронуто. |
| 5. Запахи проектирования | ✅ `MakeRawSqlSource` — 31 строка, один приватный метод; god-классов/длинных списков параметров нет. |
| 6. Обработка исключений | 🟡 **Находка 56** — in-memory путь `FromSql` не гейтится → NRE/фантомная строка вместо `NotSupportedException`; 🟡 **Находка 57** — коллизия имён именованных параметров. |
| 7. Хэш-ключи кэша (S2328) | ✅ `FromExpression.CloneForCache` (`:100`) включает `RawSqlSource` в early-return; `RawSqlSourceExpression` иммутабелен (`readonly`-свойства), поэтому `this` для кэш-плана безопасен, а значения параметров перечитываются на исполнении (см. Находку 58). |

### 🟡 Находка 56 — `FromSql` на in-memory провайдере не отклоняется: NRE или фантомная строка

`InMemoryQueryBuilder.GetPreparedQueryCommand` явно отклоняет `WithSql`/`PrepareFromSql` (`:32-34`) и
TVF/PIVOT-источники (`:121-125`), но `FromSql` кладёт фрагмент в `FromExpression.RawSqlSource`, а не в
`CustomData`, поэтому ни один guard не срабатывает. Ветка `else` (`:165-190`) исполняет запрос как по
обычной таблице: `CreateEnumerator<TResult, TEntity>` (`:199-217`) не находит данных `TableAlias` и
подставляет фантомную строку. Проверено точечным репро вне репозитория (`InMemoryDataContext`):
`FromSql("select 1 as id", null).Select(t => new { A = 1 }).ToList()` → **1 фантомная строка** (данных
нет, результат непустой); `…Select(t => new { Id = t["id"].AsInt }).ToList()` →
`System.NullReferenceException` в скомпилированной лямбде проекции
(`lambda_method…(Closure, TableAlias)`), а не понятный отказ. То есть одновременно «тихо неверный
результат» (как в Находке 9) и необработанный NRE.

- **Было:** guard смотрит только `queryCommand.CustomData is RawSqlOverride` (`InMemoryQueryBuilder.cs:32`).
- **Стало (предложение):** рядом добавить
  `if (queryCommand.From?.RawSqlSource is not null) throw new NotSupportedException("Raw SQL as a FROM source is not supported by the in-memory provider; run the query against a SQL provider.");`
  (в духе `:121-125`).
- **Проверка:** unit-тест `InMemoryTests` на `NotSupportedException`; оговорка в
  `docs/advanced/limitations.md` (+RU).

### 🟡 Находка 57 — именованные параметры фрагмента не уникализируются: коллизия/дубликаты имён

`MakeRawSqlSource` (`SqlSourceRenderer.cs:299-308`) добавляет в `ctx.Params` по одному
`Parameter(prop.Name, value)` на каждое публичное свойство объекта-параметра, **не проверяя
уникальность** имён. Имена движковых параметров тоже берутся из имён членов (`MemberTranslator` —
`node.Member.Name`, `SqlOperandTranslator`/`InValuesTranslator` — `GetParamName`), поэтому совпадение с
именем во фрагменте даёт два `Parameter` с одним именем, а `QueryPlanner` (`:128-132`) без дедупликации
зовёт `_createParam(p.Name, …)` на каждый → два `DbParameter` с одним именем. Проверено точечным репро
(SQLite, сборка команды):
- фрагмент `…where id > $min` (`new { min = 1 }`) + `.Where(t => t["v"].AsInt > min)` (захват `min = 99`)
  → SQL `… where id > $min) where (v > $min)`, `PARAMS: min=1, min=99`, **2 параметра с одним именем**;
- два вхождения одного фрагмента (join raw-источника на raw-источник) → `PARAMS: min=1, min=2`, `COUNT: 2`.
- Исполнение на SQLite: `InvalidOperationException: Must add values for the following parameters: $min`.
  SQL Server `SqlParameterCollection` отклоняет дубликат имени; итог зависит от драйвера, но «валидная
  с виду» композиция падает либо (на терпимом драйвере) связывает одно значение с обоими
  плейсхолдерами — тихо неверно.

- **Было:** `ctx.Params.Add(new Parameter(prop.Name, prop.GetValue(raw.Parameters)));` без проверки.
- **Стало (предложение):** при добавлении raw-параметров либо уникализировать имя (префикс вида
  `__raw_<n>_<name>` + переписать вхождения в фрагменте), либо детектировать совпадение по имени и
  бросать понятное исключение; задокументировать зарезервированное пространство имён.
- **Проверка:** SQL-gen тест «фрагмент + захваченная переменная с тем же именем» и тест на два вхождения
  источника; при уникализации — исполнение на SQLite + интеграционный тест.

### 🟡 Находка 58 — рефлексия по объекту-параметру на каждом рендере/исполнении кэш-плана

`MakeRawSqlSource` (`:301-305`) на каждом вызове делает `raw.Parameters.GetType().GetProperties(...)`
(массив) и `prop.GetValue(...)` (бокс). LINQ нет, но аллокации/рефлексия есть, и для кэш-плана это не
разовая работа: `Parameter.Stable` по умолчанию `false` (`Parameter.cs:21`), поэтому `QueryPlanner`
выставляет `needsParamRefresh = true` (`:109-122`) и на **каждом** исполнении кэш-плана зовёт
`ExtractParams` (`:47-52`) → param-mode проход → снова `MakeRawSqlSource` → снова рефлексия. Тот же путь
уже есть у `WithSql` (`QueryCommandExtensions.cs:27-31`), т.е. долг теперь на двух call-site'ах; для
читающего API это лишние аллокации на запрос.

- **Было:** `GetProperties`/`GetValue` без кэша.
- **Стало (предложение):** кэшировать аксессоры по типу (как `ParamNameCache`/`MemberTranslator`:
  `PropertyInfo[]` или скомпилированные геттеры в `ConditionalWeakTable<Type, …>`/
  `ConcurrentDictionary<Type, …>`).
- **Проверка:** аллокационный/бенч-тест кэш-плана `FromSql` до/после; тест, что значения перечитываются
  при повторном исполнении (флаг `Stable` не выставляется — см. `needsParamRefresh`).

### ℹ️ Наблюдения (фикс не требуется)

- **SQL-инъекция — by design.** Фрагмент эмитится дословно (как `WithSql` и EF Core `FromSql`);
  значения биндятся параметрами, а не интерполируются, новых путей для пользовательского ввода нет.
  Предупреждение есть в `docs/guide/14-raw-sql.md` (+RU): «The fragment is emitted verbatim (only pass
  trusted SQL)» / «передавайте только доверенный SQL».
- **`GetProperties(Instance | Public)` включает индексаторы.** Объект-параметр с публичным
  индексатором (словарь/обёртка) уронит `prop.GetValue` (`TargetParameterCountException`).
  Пре-существующее у `WithSql` (`QueryCommandExtensions.cs:27`); `FromSql` наследует ту же конвенцию —
  кандидат в доку (какие объекты допустимы), не блокер.
- **ClickHouse-тест использует `@min`,** тогда как параметры ClickHouse — `{name:Type}`; рендер
  проверяется как текст, фрагмент не исполним. Интеграционного теста на новую фичу нет ни у одного
  провайдера (в TODO-приёмке он значился) — стоит добавить хотя бы SQLite/PostgreSQL, иначе связывание
  параметров и исполнение не покрыты.
- **Гейт диалекта согласован.** `MakeRawSqlSource` бросает `NotSupportedException`
  (`SqlSourceRenderer.cs:294`) до `ParamMode`-ветки, поэтому param- и SQL-проходы видят один и тот же
  отказ; `SqlDialectBase` default `false` (`:500`), все 6 SQL-диалектов — `true`. In-memory гейт не
  использует `ISqlDialect` — см. Находку 56.
- **CRLF.** Все изменённые/новые `.cs` и `.md` — CRLF (кроме удалённого файла, которого нет в дереве).

**Актуализация 22.09.2026 (фиксы применены).** Находки 56 и 57 закрыты: `InMemoryQueryBuilder` теперь гейтит `From?.RawSqlSource`/`HasRawSqlJoin` (`NotSupportedException`, тест `InMemoryTests.FromSql_ShouldThrowClearNotSupported`); для команды с raw-источником `SqlBuilder.MakeSelect` валидирует уникальность имён параметров и бросает `BuildSqlCommandException` с подсказкой переименовать (`EnsureUniqueParameterNames`, тест `FromSql_CollidingParameterName_ShouldThrow`). Оговорка про in-memory (`P2-2`) и `<exception>` у `FromSql` (`P2-3`) добавлены. Находка 58 (рефлексия без кэша) остаётся открытой — тот же долг у `WithSql`, вынесена отдельно. Регресс-гейт: build 0/0; core 193/193, postgres 250/250, sqlserver 240/240, mysql 67/67, mariadb 29/29, sqlite 270/270, clickhouse 185/185.

**Проверка (22.09.2026).** Build Release — **0/0**; тесты **1232/1232** (см. выше). Скан слопа: новых
`Skip=`/`#pragma`/`SuppressMessage`/`NoWarn`/`Task.Delay`/пустых `catch` нет; `slopwatch` локальным
tool'ом не установлен (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен
вручную. Воспроизведения Находок 56–57 — точечные репро вне репозитория (код в рамках аудита не
правился).

---

## Аудит 22.09.2026 — SQL Server `xml.nodes()` rowset как `CROSS/OUTER APPLY` источник

**Область (uncommitted worktree).** Новый публичный `SqlFunctions.IXmlNodesRow` (`Query/SqlFunctions.cs:175-179`);
новый публичный `SqlServerFunctions.xml_nodes(string?, string?) -> QueryCommand<IXmlNodesRow>`
(`Query/SqlFunctions.SqlServer.cs:67-79`); новый `internal sealed XmlNodesExpression` (`Expressions/XmlNodesExpression.cs`);
`FromExpression.XmlNodes` + ctor + `CloneForCache` (`Expressions/FromExpression.cs:32-35,70-76,111`);
`FromExpressionPlanEqualityComparer` ветки Equals/GetHashCode (`:58-63,140-146`); `PrepareJoin`-интерцепт
(`Query/QueryCommand.QueryPreparer.cs:446-454`); `CorrelatedQueryExpressionVisitor.RewriteOuterReference`
(`Visitors/CorrelatedQueryExpressionVisitor.cs:56-64`); `SqlSourceRenderer.MakeXmlNodes`/`GetSingleColumnName`
(`DataContext/SqlSourceRenderer.cs:425-481`); `SqlServerXmlFunctions.Supports("nodes") == true`
(`src/nextorm.sqlserver/SqlServerDialect.cs:453`); доки `DialectCapabilities`/`XmlSqlTranslator`; тесты во всех
провайдерах + core + интеграционный.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**.
`tests/nextorm.sqlserver.tests` — **245/245**, `tests/nextorm.core.tests` — **194/194** (0 failed, 0 skipped).
Подавления: `src/` — **6** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma warning
disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в изменённых файлах новых —
**0** (2 `SuppressMessage` в `CorrelatedQueryExpressionVisitor.cs:9-10` — пре-существующие). Слоп: новых
`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn` в изменённых файлах — **0** (`slopwatch` локально
не установлен, скан вручную).

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `MakeXmlNodes` создаёт визитор через `using var visitor = ctx.CreateColumnVisitor(...)` (`SqlSourceRenderer.cs:442`), как `MakePivot`/`MakeArrayJoin`; в `ParamMode` возвращается до создания визитора — утечки нет. `PrepareJoin` сохраняет `using var outerScope` (`:444`). Новых disposable-**полей** нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта 11/11, неоправданных 0. |
| 3. LINQ на горячем пути | ✅ `MakeXmlNodes`/`GetSingleColumnName`/`TryCreate` — индексированные циклы и `is`-паттерны; `.Count()`/`.ToList()`/LINQ нет. |
| 4. God-классы | ℹ️ `SqlSourceRenderer` физически **603** строки тела (было 542 на HEAD), собственного кода (без комментариев/пустых) — **429**; `MakeXmlNodes`+`GetSingleColumnName` — +48 из +61 строк этого изменения. Формального нарушения порога по «собственным» строкам нет, но заявление закрытия Находки 4 «все прочие < 500» по физическому замеру больше не держится — см. ниже. |
| 5. Hash-ключи / план-кэш | ✅ `XmlNodesExpression` иммутабелен (`readonly` свойства, expression-tree), `CloneForCache` возвращает `this` — шаринг безопасен. `FromExpressionPlanEqualityComparer` для `XmlNodes` сравнивает `XPath` + `Operand`, а сами внешние ссылки (реальная колонка `t1.somestring`) входят в план-ключ команды через `QueryPlanEqualityComparer.Equals` (`Query/QueryPlanEqualityComparer.cs:148`) и `GetHashCode` (`:413-417`) — **разные XML-колонки не делят план**. |
| 6. События / исключения | ✅ Подписок нет; новые `throw` — `NotSupportedException` (`SqlSourceRenderer.cs:437`, `XmlNodesExpression.cs:42`, `SqlFunctions.SqlServer.cs:79`) и `BuildSqlCommandException` (`SqlSourceRenderer.cs:480`) без `catch`. |
| 7. Публичная поверхность | ✅ Новых недокументированных типов нет; Приложение A (45) не меняется — см. `API-NAMING-REVIEW.md`, раздел «SQL Server `xml.nodes()` rowset». |

### ⚠️ Находка 59 — `RewriteOuterReference` теряет `ReplaceConstantsExpressionVisitor.Params` (P2, открыта)

**Место:** `src/nextorm.core/Visitors/CorrelatedQueryExpressionVisitor.cs:63-64` →
`Expressions/XmlNodesExpression.cs:44`.

**Что не так.** `RewriteOuterReference` возвращает только `.Visit(...)`, отбрасывая визитор вместе с его
`Params` — а `ReplaceConstantsExpressionVisitor.VisitConstant` (`ExpressionExtensions.cs:125-132,150-155`)
превращает каждую константу/захваченное значение в `ParameterExpression` и складывает её в `_params`.
Для документированного сценария (`x => xml_nodes(x.XmlCol, "…")`) операнд — внешняя ссылка, и ветка
`VisitMember` (`:156-166`) заменяет её на `OuterRefMarker<T>(idx).Ref`, параметры не возникают. Но литерал или
захваченная переменная (`var xml = "…"; … xml_nodes(xml, "/x")`) даёт в операнде **свободный**
`ParameterExpression`, который ничем не биндится: `MakeXmlNodes` рендерит его через column-визитор и он не
попадает в `ctx.Params`. Это асимметрия с `GetQueryCommand` (`:468-522`), где `constRepl.Params` **читаются** и
компилируются в тело запроса. Тест `XmlNodes_ShouldThrowWhenXPathIsNotConstant` покрывает только непостоянный
**xpath**; для **xml**-операнда аналогичного гейта нет.

**Было:** `xml_nodes(capturedOrLiteralXml, "/x")` → операнд с неотбинденным параметром (рендер/исполнение
неверны либо падают позже без внятной диагностики).

**Стало (рекомендация).** Либо потребовать «операнд — внешняя ссылка» и бросить
`NotSupportedException` по образцу xpath-гейта (`XmlNodesExpression.cs:41-42`), либо вернуть
`constRepl.Params` наружу и пробросить их в `ctx.Params`/подготовку команды, как в `GetQueryCommand`.
Код не правился — маршрутизировано в `nextorm-design-engineer`.

**Проверка:** механизм подтверждён чтением `ReplaceConstantsExpressionVisitor`/`RewriteOuterReference`;
репро-запрос не исполнялся (в этом проходе код и тесты не менялись).

### ℹ️ Наблюдения (фикс не требуется)

- **`GetSingleColumnName` — рефлексия без кэша и «первая попавшаяся» колонка.** `SqlSourceRenderer.cs:470-481`
  делает `rowType.GetProperties(...)` на каждом рендере и возвращает первую колонку с непустым именем,
  игнорируя остальные. Сегодня `entityType` всегда `IXmlNodesRow` (ровно один `[Column("value")]`), поэтому
  поведение корректно и тест фиксирует `as [t2](value)`, но хелпер переусложнён и не кэширован. Тот же класс
  долга, что Находка 58 (рефлексия без кэша), и `FromSql`-`GetProperties` (`:304`). Путь — сборка плана, не
  построчная материализация, поэтому ℹ️.
- **`TryCreate` — NRT и слоение.** `out XmlNodesExpression? nodes` без `[NotNullWhen(true)]` —
  вызывающий (`QueryCommand.QueryPreparer.cs:453`) вынужден писать `xmlNodes!`. Атрибут убрал бы
  null-forgiving и документировал контракт. Плюс `XmlNodesExpression.TryCreate` принимает
  `CorrelatedQueryExpressionVisitor` только чтобы дёрнуть `RewriteOuterReference` — лёгкое протекание
  абстракции «выражение → визитор»; терпимо, т.к. тип `internal`.
- **`MakeXmlNodes` жёстко требует `entityType`.** `ctx.ColumnsProvider.Add(entityType!, false)` и
  `GetSingleColumnName(entityType!, …)` (`:448,450`) упадут с `NullReferenceException` при `entityType == null`.
  Сегодня безопасно: `JoinApply` ставит `EntityType = typeof(TJoinEntity)` (`EntityBuilder.cs:1060-1065`), а
  `FromExpression(XmlNodes)` ставится только из apply-ветки `PrepareJoin` (`:446-454`). Оговорка для будущих
  путей (например, обычный `JOIN` с `XmlNodes`-источником) — стоит защитить guard'ом.
- **Доки-поверхность.** Классовый `<summary>` `SqlServerFunctions` (`SqlFunctions.SqlServer.cs:6-12`)
  перечисляет только `xml_value`/`xml_query`/`xml_exist` — без `xml_nodes`; `docs/**` EN+RU, описывающие
  XML-скаляры, метода не содержат. Обе — `P2` в `API-NAMING-REVIEW.md`.
- **CRLF.** Изменённые/новые `.cs` и `.md` — CRLF.

**Проверка (22.09.2026).** Build Release — **0/0**; `nextorm.sqlserver.tests` — **245/245**,
`nextorm.core.tests` — **194/194** (прогнано в этом проходе); в изменённых файлах `#pragma warning
disable`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay` — **0**; соотношение подавлений проекта **11/11**
(0 неоправданных); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт). План-ключ проверен чтением
`FromExpressionPlanEqualityComparer`/`QueryPlanEqualityComparer`; `SqlSourceRenderer` измерен по методике
Находки 4.

---

## 🔎 Точечный аудит 22.09.2026 — ClickHouse `UInt64` row reader (`DbDataReader.GetFieldValue<ulong>`) (чисто; 3 наблюдения)

**Область (uncommitted worktree `clickhouse-uint64-row-reader`).** Ветка `ulong` в публичном
`SelectExpression.GetDataRecordMethod()` (`src/nextorm.core/Expressions/SelectExpression.cs:60,113-116`; новое
`private readonly static MethodInfo GetFieldValueMI = typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!`);
выбор аксессора `IDataRecord` → `DbDataReader` в `internal RowMapperFactory.MapColumn`
(`src/nextorm.core/DataContext/RowMapperFactory.cs:26-30`); правки только комментариев в
`ClickHouseDialect.cs:57-59,163-166,251-256,562-566`; тесты `SelectExpressionTests.cs:18-34`,
`ClickHouseDialectTests.cs:143-148`, `SqlGenerationTests.cs:1793-1801`,
`ClickHouseIntegrationTests.cs:789-836` + фикстура `ClickHouseTestProvider.cs:137-152`; доки EN+RU.

**База (этот проход).** `dotnet build nextorm.sln -c Release --no-incremental` — **0 warnings / 0 errors**;
`nextorm.core.tests` **196/196**, `nextorm.clickhouse.tests` **188/188** (0 failed/0 skipped). Подавления `src/`:
**6** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma warning disable` (все с парным
`restore`) = **11/11 оправданных, 0 неоправданных**; в изменённых файлах новых — **0**. Слоп: новых
`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn` в изменённых файлах — **0** (`Task.Delay` по
репозиторию — 2, обе `Task.Delay(0)`-yield в `InMemoryTests.cs:125,414`, пре-существующие; `slopwatch` локально
не установлен, скан вручную). EOL: все изменённые файлы — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локалов нет; владение `ResultSetEnumerator._reader` (`DbDataReader?`) не менялось (`Reset`/`DisposeAsync` — `ResultSetEnumerator.cs:71-90,231-241`). |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11**, неоправданных `0`. |
| 3. LINQ на горячем пути | ✅ `MapColumn`/`GetDataRecordMethod` — цепочка `==` и `Expression.Call`; LINQ/`.Count()`/`.ToList()` нет (пункт 2 вопроса из брифинга). |
| 4. God-классы | ✅ `RowMapperFactory` **144** строки, `SelectExpression` **187**; изменение +4/+5 строк — порог не затронут. |
| 5. Хэш-ключи / план-кэш | ✅ Ключ маппера (`RowMapperFactory.cs:121-143`) хэширует `column.PropertyType` + `Nullable`/`DefaultOnNull`/`Index`/`PropertyName`, поэтому `ulong` и `long` в одном SQL не делят запись кэша; `MapperCacheKey`/`MapperCache` не менялись, пере-хэш не нужен. Новое `GetFieldValueMI` — `static readonly`, в ключ не входит. |
| 6. События / исключения | ✅ Подписок нет; новых `catch`/`throw` нет. Наоборот, `ulong` перестал бросать `NotSupportedException` из `GetDataRecordMethod` (`SelectExpression.cs:130`). |
| 7. Аллокации/бокс на материализации (пункт брифинга) | ✅ `MapColumn` исполняется при сборке маппера (один раз на SQL/план), не построчно. Для всех не-`ulong` типов `accessor == param`, т.е. скомпилированное дерево **идентично прежнему** — регресса нет; для `ulong` добавляется один ссылочный `castclass`, бокса нет. Построчный бокс внутри `GetFieldValue<ulong>` — см. Наблюдение 2. |

### ℹ️ Наблюдение 1 — контракт `Func<IDataRecord, TResult>` сужен до `DbDataReader` только для `ulong`

**Место:** `RowMapperFactory.cs:26-30`; тип делегата — `ResultSetEnumerator.cs:21` (`Func<IDataRecord, TResult>`),
вызовы `:125,149`; точка входа — `DataContext.cs:154`.

Для `ulong` `MapColumn` добавляет `Expression.Convert(param, method.DeclaringType!)` — даункаст `IDataRecord` →
`DbDataReader`. Фактический аргумент — `ResultSetEnumerator._reader` (`DbDataReader?`, `:26`), и все 13 реализаций
`IDataRecord` в решении наследуют `DbDataReader` (кроме абстрактного `System.Data.Common.DbDataRecord`), поэтому
сегодня путь **недостижим** и багом не является. Но подпись делегата/`MapColumnExpression` рекламирует
`IDataRecord`: будущий `IDataRecord`-only ридер или тест-двойник упадёт с `InvalidCastException` **только** на
`ulong`-колонках (латентное сужение контракта, LSP-характер).

**Рекомендация (маршрут — `nextorm-design-engineer`, код не правился):** либо сузить делегат до
`Func<DbDataReader, TResult>` (все существующие вызовы и так передают `DbDataReader`), либо оставить `IDataRecord`
и дать `ulong`-ветке guard/fallback без даункаста. Пока — ℹ️, фикс не обязателен.

### ℹ️ Наблюдение 2 — бокс в `GetFieldValue<ulong>` и охват SQL Server

У `IDataRecord` нет `GetUInt64`, поэтому `DbDataReader.GetFieldValue<ulong>` — единственный портируемый аксессор;
его реализация у провайдеров обычно идёт через `GetValue` и боксит `ulong`. Альтернатива
`Convert.ToUInt64(record.GetValue(i))` боксит так же и медленнее, поэтому выбор верен. `Expression.Convert` в
`MapColumn` — ссылочный `castclass`, не бокс; `ulong?`-ветка остаётся без бокса (`GetFieldValue<ulong>` →
`Expression.Convert` в `ulong?`), а `IsDBNull` по-прежнему вызывается на `param` (`IDataRecord`), без лишнего
приведения на строку. Отдельно: `SqlServerDataContext.IsNumeric` (`:72-74`) не включает `ulong`, поэтому SQL Server
теперь тоже идёт в базовый `MapColumn` (раньше `ulong` там падал `NotSupportedException`) — ожидаемо для новой
поддержки; нативного `UInt64` у SQL Server нет, так что практический смысл имеют ClickHouse/MySQL. Фикс не требуется.

### ℹ️ Наблюдение 3 — доки заявляют MySQL/MariaDB `BIGINT UNSIGNED`, интеграционного теста на него нет

`docs/advanced/limitations.md:56`, `docs/ru/advanced/limitations.md:56` и провайдерные страницы утверждают, что тем
же аксессором пользуется MySQL/MariaDB `BIGINT UNSIGNED`, но в это изменение добавлен только ClickHouse-интеграционный
тест (`IUInt64Entity`/`uint64_entity`); MySQL/MariaDB покрыты лишь общим core-тестом
`SelectExpression.GetDataRecordMethod`. Код-путь общий (MySQL не переопределяет `MapColumnExpression`), но заявление
о драйвере не подтверждено прогоном. Рекомендация: добавить `uint64`-кейс в MySQL-интеграцию (общий
`CommonTestSuite` или MySQL-specific) либо смягчить формулировку. Это тест/док-точность, не запах кода.

**Проверка (22.09.2026).** Build Release `--no-incremental` — **0/0**; core **196/196**, ClickHouse unit
**188/188**; в изменённых файлах `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay` — **0**; соотношение
подавлений проекта **11/11** (0 неоправданных); `PublicAPI*.txt` — пусто (Шаг 5 открыт); все изменённые файлы CRLF.
План-ключ проверен чтением `RowMapperFactory.BuildKey`; контракт делегата — чтением `ResultSetEnumerator`/
`MapColumnExpression`. ClickHouse-интеграционные тесты в этом проходе **не запускались** (нужен Testcontainers/Podman).
## Аудит 22.09.2026 — серверные/кластерные табличные функции ClickHouse (generic `TRow`)

**Область (uncommitted worktree).** Новые публичные `ClickHouseFunctions.url<TRow>`/`s3<TRow>`/`file<TRow>`/`remote<TRow>`/`remote_secure<TRow>`/`cluster<TRow>`/`cluster_all_replicas<TRow>` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:286,296,306,317,324,334,342`); расширенный гейт `ClickHouseDialect.SupportsTableFunction` (`src/nextorm.clickhouse/ClickHouseDialect.cs:251-253`); общий отказ `SqlSourceRenderer.MakeTableFunction` (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:350-351`); доки класса/свойства; тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1015-1125` (+`IServerTableRow:1992`), `ClickHouseDialectTests.cs:168-185` (`SupportsTableFunction`, 16 утверждений), `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1477-1507`; доки EN+RU.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.clickhouse.tests -c Debug` — **193/193**; `dotnet test tests/nextorm.postgres.tests -c Debug --filter FullyQualifiedName~BuiltInTableFunction` — **6/6** (0 failed, 0 skipped). Подавления: `src/` — **6** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в diff — **0** новых `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` (скан вручную: `slopwatch` локально не установлен — `.config/dotnet-tools.json` содержит только coverage/reportgenerator/docfx).

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: 7 методов — `=> throw new NotSupportedException()`, `SupportsTableFunction` — предикат, `MakeTableFunction` ресурсов не удерживает; тесты — `using var ctx`. |
| 2. Подавления | ✅ Новых — **0**; соотношение проекта 11/11 (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ В продовом коде LINQ нет; `SupportsTableFunction` — `is … or …`. В тестах `.Select(...)` — путь сборки SQL (не исполняется). |
| 4. God-классы | ✅ `SqlFunctions.ClickHouse.cs` — 570 строк файла (комментарии/XML ~326, пустые ~110) → **~134 собственных** строк `ClickHouseFunctions` (< 500); новых классов/частичных файлов нет. |
| 5. Хэш-ключи / план-кэш | ✅ **Ключевой вопрос проверен по существу — дефекта нет.** `TRow` входит в `QueryPlanEqualityComparer.Equals` (`:38`) и `GetHashCode` (`:291-292`): разные `TRow` = разные планы (ограничено числом закрытых generic-инстанциаций), одинаковые — делят план; сама TVF сравнивается структурно (`FromExpressionPlanEqualityComparer.Equals:52-53`, `GetHashCode:134-135`). Значения аргументов (URL/S3/адреса) в план-ключ **не** входят: для closure `ExpressionPlanEqualityComparer.CompareMember:255-258`/`VisitMember:675-712` хэшируют (тип замыкания, имя и тип члена), не значение — высококардинальные URL не размножают кэш. На cache-hit значения обновляются: `Parameter.Stable=false` (`BaseExpressionVisitor.cs:253` → `InValues.IsStableValueExpression(MemberExpression)` → `default:false`), поэтому `QueryPlanner` выставляет `needsParamRefresh` и зовёт `ExtractParams` (`QueryPlanner.cs:109-122,173-184`), а `MakeTableFunction` в param-режиме (`SqlSourceRenderer.cs:353-365`) перечитывает захваченные значения. Неограниченного/промахивающегося ключа нет. |
| 6. События / исключения | ✅ Подписок нет; новые `throw` — только `NotSupportedException` общего гейта (`SqlSourceRenderer.cs:351`); `catch` не добавлено. |
| 7. Публичная поверхность | ✅ 7 новых публичных методов, **новых типов нет**, `<summary>` есть у всех 7; Приложение A (45) без изменений — см. `API-NAMING-REVIEW.md` (SCTF1/SCTF2). |

### ℹ️ Наблюдения (фикс не требуется)

- **Security — значения параметризованы; лог значений только при opt-in.** Аргументы биндятся параметрами (`from url(@url, @format, @structure)`), в SQL-текст не инлайнятся и в план-ключ не входят. Значения параметров пишутся в лог только при явном `LoggingOptions.LogSensitiveData` (`QueryExecutor.cs:43-49`, `ResultSetEnumerator.cs:193-225`); по умолчанию логируется SQL с плейсхолдерами. Тесты используют инертные `http://127.0.0.1/...`/`remote.example.com` без креденшелов. Caller-declared `url` может содержать `user:pass@`/presigned-параметры — доки/guide рекомендуют named collections (формулировка уточнена в `API-NAMING-REVIEW.md`).
- **Wrong-provider тест покрывает 3 из 7 имён.** `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1477-1507` проверяет `url`, `remote`, `cluster_all_replicas`; `s3`/`file`/`remote_secure`/`cluster` на чужом провайдере не проверены. Все идут через один гейт, но регресс по конкретному имени пройдёт незамеченным. Рекомендация: `Theory` по 7 SQL-именам.
- **Нет регресс-теста на план-кэш/refresh generic-TVF.** Корректность (разные `TRow` → разные планы; один `TRow`, разные URL → общий план + обновлённый параметр) подтверждена чтением компараторов и `QueryPlanner.ExtractParams`, но тестом не закреплена: существующие проверки смотрят SQL один раз на контекст. Кандидат — core-тест.
- **Copy/paste в postgres-тесте.** `SqlGenerationTests.cs:1500-1502`: `cluster_all_replicas<ISimpleEntity>(database, database, table)` — первый аргумент (cluster) получает `database`; функционально безвредно (вызов бросает до рендера) + лишняя пустая строка (`:1509-1510`). Косметика.
- **`<typeparam name="TRow">` отсутствует** у 7 методов при наличии `<typeparamref name="TRow"/>` — как у `IUnnestRow<T>`; на `CS1591` (в `<NoWarn>` 7 `.csproj`) не влияет, для DocFX можно добавить.
- **Интеграционный тест не предложен осознанно.** Серверные TVF требуют внешних ресурсов (HTTP/S3/кластер) либо серверного `user_files_path`; SQL-gen — разумный максимум в отличие от `numbers`/`zeros`.
- **CRLF.** Все добавленные `.cs`/`.md` — CRLF (кроме удалённого `todo_clickhouse_server_table_functions.md`).

**Проверка (22.09.2026).** Build Release — **0/0**; `nextorm.clickhouse.tests` — **193/193**, `nextorm.postgres.tests --filter ~BuiltInTableFunction` — **6/6** (прогнано в этом проходе); новых подавлений/слопа в diff — **0**; `PublicAPI*.txt` — **0** файлов (Шаг 5 открыт); план-ключ/refresh проверены чтением `QueryPlanEqualityComparer`, `ExpressionPlanEqualityComparer`, `FromExpressionPlanEqualityComparer`, `QueryPlanner`.
## 🔎 Аудит 22.09.2026 — ClickHouse нативные JSON-функции `json_all_paths`/`json_all_paths_with_types`/`to_json_string` (uncommitted worktree `clickhouse-json-type`)

**Область (uncommitted worktree).** `ClickHouseFunctions.json_all_paths`/`json_all_paths_with_types` → `string[]`,
`to_json_string<T>` → `string?` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:167-190`); ветки
`JsonExtractSqlTranslator` (`src/nextorm.core/Visitors/JsonExtractSqlTranslator.cs:59-67`, `EmitFunction` `:93-118`);
маппинг имён `ClickHouseDialect.MakeJsonExtract` (`src/nextorm.clickhouse/ClickHouseDialect.cs:169-197`, новые пары
`:189-191`); расширенные XML-доки `SupportsJsonExtract`/`MakeJsonExtract` (`ISqlDialect.cs:399-421`,
`SqlDialectBase.cs:101`); тесты `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:832-848`,
`ClickHouseDialectTests.cs:160-166`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs:1735-1750`; доки EN+RU,
gap-analysis §4 п.7, удалён `todo_clickhouse_json_type.md`.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**.
`tests/nextorm.clickhouse.tests` — **188/188**, `tests/nextorm.postgres.tests` — **257/257** (0 failed, 0 skipped).
Подавления: `src/` — **6** `SuppressMessage` (все с `Justification`, `<Pending>` — 0) + **5** `#pragma warning disable`
(все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: новых
`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** (`slopwatch` локальным tool'ом
не установлен, скан вручную; `Task.Delay` проекта — 2, обе прежние `Task.Delay(0)`-yield в
`tests/nextorm.core.tests/InMemoryTests.cs:80,369`). Контейнерные интеграционные тесты в этом проходе не
запускались (CLI podman/docker в среде аудита нет; у новых функций интеграционного теста нет) — см. Находку 60.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет: три DSL-заглушки `=> default!`, `EmitFunction` — строковый рендер без ресурсов; тесты оборачивают контекст в `using var`. |
| 2. Подавления | ✅ В диффе `#pragma warning disable`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных) не изменилось; `CS1591` — по-прежнему `NoWarn` в 7 библиотечных `.csproj` (Шаг 5). |
| 3. LINQ на горячем пути | ✅ `EmitFunction` — `for` + `new string[args.Count]` на холодном пути построения плана; новых LINQ-цепочек нет. |
| 4. God-классы | ✅ Новых god-классов нет: +3 ветки `switch`, `EmitFunction` остаётся **одним** методом (не копия, в отличие от прежней Находки 15). |
| 5. Hash-ключи / план-кэш | ✅ Новые методы DSL различаются `MethodInfo` в дереве выражения; новых членов `QueryPlanEqualityComparer`/`_hashPlan` нет. |
| 6. События / исключения | ✅ Подписок нет; новый `throw new NotSupportedException(nativeJson ? … : jsonPath ? … : …)` — гейт `SupportsJsonExtract`, не пустой `catch`. |
| 7. Публичная поверхность | ✅ **Находка 60 закрыта 22.09.2026** — `json_all_paths_with_types` теперь `Dictionary<string,string>` (ветка `GetValue` в `SelectExpression.GetDataRecordMethod`), прямая проекция материализуется; остаётся J9 (трекинг `PublicAPI`, Шаг 5). |

### 🔴 Находка 60 — `json_all_paths_with_types` объявлен `string[]`, а нативный результат — `Map(String, String)` (ЗАКРЫТА 22.09.2026 — вариант B: `Dictionary<string,string>` + `GetValue`-ветка, подтверждено интеграционным тестом)

**Место:** `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:176-183` (объявление + XML-`<summary>`), транслятор
`src/nextorm.core/Visitors/JsonExtractSqlTranslator.cs:62-64`, рендер `src/nextorm.clickhouse/ClickHouseDialect.cs:190`;
путь материализации — `src/nextorm.core/Expressions/SelectExpression.cs:117-122` и
`src/nextorm.core/DataContext/RowMapperFactory.cs:24-53`.

**Что не так.** Официальная документация ClickHouse (JSON functions): `JSONAllPaths(json)` → `Array(String)`,
`JSONAllPathsWithTypes(json)` → **`Map(String, String)`**. `ClickHouse.Driver` 1.4.0 маппит эти типы на CLR `string[]`
и `Dictionary<string, string>` соответственно (`ArrayType.FrameworkType = T[]`, `MapType.FrameworkType =
Dictionary<K,V>`; `DbDataReader.GetValue` возвращает ровно эти объекты). NextORM для CLR-типа `string[]` выбирает
`IDataRecord.GetValue` + приведение (`SelectExpression.cs:117-122`), а `RowMapperFactory.MapColumn` заворачивает
геттер в `(string[])((IDataRecord)record).GetValue(i)`. Следствия:

1. **Прямая проекция `json_all_paths_with_types(...)`** даёт `(string[])(Dictionary<string,string>)` →
   `InvalidCastException` при материализации строки. SQL-gen тест этого не ловит — он проверяет только текст SQL
   (`SqlGenerationTests.cs:841,846`) и оборачивает вызов в `length`, т.е. прямой проекции в тестах нет.
2. **Единственная задокументированная и протестированная вложенная форма** — `length<T>(T[])`
   (`SqlFunctions.ClickHouse.cs:180`; `SqlGenerationTests.cs:841,846`) — для `Map` невалидна: документация ClickHouse
   `length` перечисляет `String`/`FixedString`/`Array`/`QBit`, но не `Map`; мост `Map → Array` — это
   `mapKeys`/`mapValues`. То есть у функции сегодня **нет ни одной корректной формы вызова**.

**Было:** публичный `string[] json_all_paths_with_types(string?)`; рендер `JSONAllPathsWithTypes(col)`; тест ассертит
`toInt64(length(JSONAllPathsWithTypes(somestring)))`.

**Стало (рекомендация, одна из):**
- **(A, минимальный корректный гейт)** запретить прямую проекцию/вложенность до появления map-ридера: не давать
  `Map`-типу попасть в `SelectExpression.GetDataRecordMethod` (fail-fast `NotSupportedException` при построении плана)
  либо явный guard в трансляторе/на подготовке; не документировать `length` как пример. `json_all_paths`
  (`Array(String)`: `length` корректна) и `to_json_string` оставить.
- **(B, полноценно)** ввести корректный CLR-контракт `IReadOnlyDictionary<string,string>`/`Dictionary<string,string>`
  + кейс в `SelectExpression.GetDataRecordMethod()` (`GetValue` + cast) и мост `mapKeys`/`mapValues`
  в array-поверхность; тогда прямая проекция материализуется, а `length` заменяется на `length(mapKeys(...))`.
- **(C)** если нужны только пути — транслировать `json_all_paths_with_types` в `mapKeys(JSONAllPathsWithTypes(...))`
  (теряя типы), либо не выставлять функцию до маппинга нативного `JSON`.

**Проверка:** build 0/0; clickhouse 188/188, postgres 257/257; тип возврата и путь материализации подтверждены
чтением `SelectExpression.GetDataRecordMethod`/`RowMapperFactory.MapColumn`; семантика ClickHouse — по официальной
документации (JSON functions: `Map(String, String)`; `length` — String/Array/QBit); маппинг драйвера — по исходникам
`ClickHouse/clickhouse-cs` (`ClickHouse.Driver` 1.4.0). Реальный ClickHouse в этом проходе не поднимался. Код не
правился — маршрутизировано в `nextorm-design-engineer`.

### ℹ️ Наблюдения (фикс не требуется / требует проверки)

- **Аргумент `JSONAllPaths*` — нативный `JSON`, а unit-тест передаёт `String`-колонку.** По документации ClickHouse
  аргумент `JSONAllPaths`/`JSONAllPathsWithTypes` — колонка типа `JSON`; тест рендерит `JSONAllPaths(somestring)`
  (колонка `String`). Неявного `String → JSON` документация не описывает (нужен `CAST(col AS JSON)`), поэтому зелёный
  SQL-gen не доказывает исполнимость. Вместе с отложенным маппингом нативного `JSON` это значит, что типизированного
  пути вызвать эти две функции сегодня нет. Рекомендуется добавить интеграционный тест на
  `JSONAllPaths(CAST(col AS JSON))` либо явно задокументировать требование нативного JSON-выражения.
- **`json_all_paths` (`Array(String)`) — «usable only nested» избыточно.** Драйвер отдаёт `Array(String)` как
  `string[]`, а `SelectExpression.GetDataRecordMethod()` уже поддерживает `string[]` (`GetValue` + cast), поэтому
  прямая проекция `json_all_paths` должна материализоваться. XML-док (`:167-173`) и доки называют её «usable only
  nested» — безопасное, но неточное сужение; проверить интеграционным тестом и либо разрешить прямую проекцию,
  либо оставить ограничение осознанно.
- **`EmitFunction` с двумя булевыми флагами** (`jsonPath`/`nativeJson`, `JsonExtractSqlTranslator.cs:93-100`) — вложенный
  тернарник читается хуже плоских веток, но дублирования тела, как в Находке 15 (`EmitJsonPathFunction`), нет. Не находка.
- **Новые ветки `switch`** (`:59-67`) — та же почти-дублирующая структура `case nameof(...)` + строковый литерал, что
  и в ℹ️-наблюдении `JSONExtract*`/`visitParamExtract*`; не регресс.
- **CRLF.** Изменённые/новые `.cs` и `.md` — CRLF.

**Проверка (22.09.2026).** Build Release — **0/0**; `nextorm.clickhouse.tests` — **188/188**,
`nextorm.postgres.tests` — **257/257** (прогнано в этом проходе); в диффе `#pragma warning
disable`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch` — **0**; соотношение подавлений
проекта **11/11** (0 неоправданных); `rg --files -g 'PublicAPI*.txt'` — пусто (Шаг 5 открыт). Контейнерная интеграция
ClickHouse в этом проходе не запускалась (нет CLI podman/docker; у этих функций интеграционного теста нет) — прямой
проекции/`Map`-материализации на реальном сервере нет; вывод построен по коду nextorm + официальной документации
ClickHouse + маппингу `ClickHouse.Driver` 1.4.0.

---

## 🔎 Точечный аудит 22.09.2026 — ClickHouse row reader `Array(T)`/`Tuple` и агрегаты `group_array`/`group_uniq_array` (uncommitted worktree)

**Область.** Row reader: array-ветка `_realType.IsArray` (`src/nextorm.core/Expressions/SelectExpression.cs:117-124`, заменила частные `byte[]`/`string[]`) и tuple-ветка `TypeFacts.IsTupleType(_realType)` (`:125-130`); классификация проекции `TypeFacts.IsSingleColumnProjection`/`IsTupleType` (`src/nextorm.core/Visitors/TypeFacts.cs:45-74`); `QueryCommand.QueryPreparer.IsSingleColumnType` → делегирование (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:209-215`) и non-`NewExpression`-ветка Tuple (`:286-287`); новые публичные `ClickHouseFunctions.group_array<T>`/`group_uniq_array<T>` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:127-143`), диспетчеризация `AdvancedAggregateTranslator.cs:142-147`, маппинг `ClickHouseDialect.cs:398-399`. Тесты — `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`, `tests/nextorm.postgres.tests/SqlGenerationTests.cs`, `tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs` (+`ITupleEntity`), `Providers/ClickHouseTestProvider.cs`.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `nextorm.clickhouse.tests` — **201/201**, `nextorm.postgres.tests` — **265/265** (0 failed, 0 skipped; прогнано заново). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** (`slopwatch` локально не установлен — `.config/dotnet-tools.json` только coverage/reportgenerator/docfx; скан вручную). EOL: все изменённые файлы — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локалов нет: `GetDataRecordMethod` — выбор `MethodInfo`, `EmitSimple` — строковый рендер; тесты оборачивают контекст в `using var`. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет: `IsSingleColumnProjection`/`IsTupleType` — предикаты `is`/`==`; `GetDataRecordMethod` — цепочка `==`/`IsArray`/`IsGenericType`. |
| 4. God-классы | ✅ `TypeFacts` 96, `SelectExpression` 196, `AdvancedAggregateTranslator` 418 строк — порог 500 не затронут; новых типов нет. |
| 5. Хэш-ключи / план-кэш | ✅ Ключ маппера (`RowMapperFactory.cs:121-143`) хэширует `column.PropertyType` + `Nullable`/`DefaultOnNull`/`Index`/`PropertyName` + флаг `OneColumn`: `int[]`/`string[]`/`Tuple<…>` не делят запись кэша между собой и со скалярами; новых членов план-ключа нет. Перевод array/Tuple-проекций в `OneColumn` меняет ключ существующих array-проекций — корректно (тот же SQL + другой `PropertyType`/флаг). |
| 6. События / исключения | ✅ Подписок нет; новых `catch`/`throw` нет; `GetDataRecordMethod` наоборот перестал бросать `NotSupportedException` для `T[]`/`Tuple`. |
| 7. Аллокации на материализации (пункт брифинга) | ✅ `GetValue` возвращает уже материализованный массив/`System.Tuple` (ссылочные типы) — бокса нет, `MapColumn` добавляет только ссылочный `castclass` (`RowMapperFactory.cs:27-43`); построчного LINQ/аллокаций не появляется, маппер компилируется один раз на SQL/план. |

### ✅ Находка 61 — новые helper'ы классификации проекции оставили устаревшие XML-summary (ЗАКРЫТА 22.09.2026, была P2)

**Место:** `src/nextorm.core/Visitors/TypeFacts.cs:5-9` (классовый `<summary>`), `:45-74`; `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:209-215`; `src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs:5-21`.

**Что не так.** `TypeFacts` описан как «Static classification helpers … **used by the visitors**: whether an expression already renders as a predicate and whether a numeric conversion needs an explicit `cast(...)`». Новые `IsSingleColumnProjection`/`IsTupleType` (a) не классифицируют предикат/каст, (b) используются не visitor'ами, а `QueryCommand.QueryPreparer` (подготовка проекции) и `SelectExpression.GetDataRecordMethod` (выбор аксессора чтения) — summary класса уже не описывает заметную часть его поверхности. `AdvancedAggregateTranslator` в summary перечисляет семейства (boolean/bitwise/statistical/ordered-set/quantile), но не пополнено `groupArray`/`groupUniqArray`. Плюс приватный `IsSingleColumnType` (`QueryPreparer.cs:215`) — теперь однострочный forwarder и второе имя одного концепта (`IsSingleColumnProjection`), а его doc дублирует doc `TypeFacts`; читателю не ясно, какое имя каноническое.

**Было:** summary `TypeFacts` — только про предикаты/касты; `IsSingleColumnType` — собственная реализация и doc.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** обновить классовый `<summary>` `TypeFacts` (добавить классификацию формы проекции; либо убрать «used by the visitors» и перечислить потребителей) и summary `AdvancedAggregateTranslator`; в `QueryPreparer` оставить один канонический вызов `TypeFacts.IsSingleColumnProjection` (убрать forwarder/дублирующий doc) — поведение не меняется.

**Проверка:** build 0/0; `grep -n "IsSingleColumnType"` — 3 call-site (`:247,286,658`) + определение; XML `cref`‐ссылка на `IsTupleType` (`TypeFacts.cs:50`) разрешается. **Закрыто 22.09.2026:** summary `TypeFacts` дополнен формой проекции/Tuple и её потребителями (`TypeFacts.cs:5-10`), summary `AdvancedAggregateTranslator` — `groupArray`/`groupUniqArray` (`:5-16`), forwarder `IsSingleColumnType` удалён (все 3 call-site переведены на `TypeFacts.IsSingleColumnProjection`; `QueryPreparer.cs:239,278,650`).

### ℹ️ Наблюдения (фикс не требуется)

- **(a) Мисмаршрутизация проекций не найдена.** `IsSingleColumnProjection` расширяет прежний `IsSingleColumnType` только на `type.IsArray` (tuple намеренно исключён). На двух других call-site (`QueryPreparer.cs:247,658`) условие — `Body is NewExpression ctor && !IsSingleColumnType(ctor.Type)`, а `NewExpression.Type` не бывает типом-массивом (массивы порождает `NewArrayExpression`), поэтому array-ветка там — no-op. Реальная array/Tuple-проекция (`x.Tags`, `x.Pair`, `group_array(...)`) идёт в scalar/OneColumn-ветку `:286-287` — это и есть цель. Единственная новая форма — array-выражение с составным элементом/литерал (`new[] { new { … } }`): оно теперь тоже `OneColumn`, но SQL-рендер такой проекции и раньше не существовал (веток `VisitNewArray` в visitor'ах нет), т.е. отказ остаётся отказом — молчаливой подмены данных нет.
- **(b) Имена и размещение.** `IsSingleColumnProjection`/`IsTupleType` следуют конвенции `TypeFacts` (`Is*`/`TryGet*`, PascalCase), помечены `internal`; вопрос — только к классу-«дому» (Находка 61). `IsTupleType` покрывает `System.Tuple` арности 1..7; `ValueTuple<…>` и арность 8+ — вне (см. наблюдение ниже).
- **(c) Гейт `SupportsArrayFunctions` корректен.** Флаг `true` только у `ClickHouseDialect` (`:50`), у `SqlDialectBase`/прочих — `false` (`SqlDialectBase.cs:35`), поэтому PostgreSQL-тест `GroupArray_UnsupportedByProvider_ShouldThrow` ждёт `NotSupportedException` с сообщением `*groupArray/groupUniqArray*` — совпадает с `EmitSimple(..., "groupArray/groupUniqArray")`. Маппинг имён есть (`ClickHouseDialect.MakeAggregate` `:398-399`); у неподдерживающего диалекта до `MakeAggregate` дело не доходит.
- **(d) `SelectExpression` array/Tuple-ветки.** Обе возвращают `GetValueMI` (`IDataRecord.GetValue`), `RowMapperFactory.MapColumn` приводит `object` к `PropertyType` (`Expression.Convert` → `castclass`), `Nullable` для массивов/Tuple истинно (`IsClass`), поэтому NULL-путь `IsDBNull` работает. Типизированный `GetFieldValue<T[]>` не нужен: массивы/`Tuple` — ссылочные типы, бокса нет, семантика идентична прежним `byte[]`/`string[]`-ветвям.
- **`ValueTuple` и арность.** TODO-план (`docs/specs/roadmap/todo_clickhouse_arrays.md:70`) обещает ветку `System.Tuple<>`/`ValueTuple<>`, а реализована только `System.Tuple<>` (`ValueTuple` драйвер ClickHouse не возвращает; интеграционный тест — `System.Tuple<int,string>`); XML-док `IsTupleType` (`TypeFacts.cs:61-64`) утверждает, что драйверный тип >7 «internal», — в этом проходе не подтверждено. Рекомендация: выровнять формулировку TODO с реализацией; при необходимости `ValueTuple` — отдельный кейс.
- **Доки.** `docs/advanced/limitations.md:36` (+RU), `docs/providers/clickhouse.md:56` (+RU), `docs/guide/provider-specific/clickhouse.md:152` (+RU) прямо противоречат новому поведению; `group_array`/`group_uniq_array` не добавлены в списки. Разбор и приоритеты — в `API-NAMING-REVIEW.md`, CHARR2/CHARR3. `docs/specs/roadmap/sql-capabilities-gap-analysis.md:162-172` (§4 п.4) всё ещё называет row reader отсутствующим — пункт TODO-плана не закрыт (не реестр; маршрут — `nextorm-design-engineer`).
- **`T? value` vs `T element`.** У `group_array`/`group_uniq_array` элемент объявлен `T?`, у соседних `array_push_back<T>(T[], T)` — `T`; для unconstrained `T` это не `Nullable<T>`, семантику SQL не меняет. Косметика.
- **`byte[]`/`string[]` не регрессируют.** Прежние частные ветки полностью покрыты `IsArray`; `Nullable`-путь для них тот же.

**Проверка (22.09.2026).** Build Release — **0/0**; `nextorm.clickhouse.tests` **201/201**, `nextorm.postgres.tests` **265/265** (прогнано в этом проходе); в диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных); `find -name 'PublicAPI*.txt'` — пусто (Шаг 5 открыт); все изменённые файлы CRLF. Контейнерная интеграция ClickHouse/PostgreSQL в этом проходе не перезапускалась; вывод по row reader построен чтением `SelectExpression`/`RowMapperFactory`/`SqlOperandTranslator` и приложенного набора тестов.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse higher-order (lambda) array-функции (uncommitted worktree)

**Область.** `ClickHouseFunctions.array_map`/`array_filter`/`array_exists`/`array_all`/`array_count`/`array_first`/`array_first_index`/`array_last`/`array_last_index` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:485-545`); `ArraySqlTranslator.TryTranslateHigherOrderArray`/`EmitHigherOrderArray`/`ExtractLambda` (`src/nextorm.core/Visitors/ArraySqlTranslator.cs:261-341`); новый `internal sealed HigherOrderLambdaVisitor` (`:408-455`); флаг `ISqlDialect.SupportsHigherOrderArrayFunctions` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:145-152`; база `SqlDialectBase.cs:36`; override `src/nextorm.clickhouse/ClickHouseDialect.cs:57`); `MakeArrayFunction` +3 имени (`ClickHouseDialect.cs:70`). Тесты — clickhouse SQL-gen (7), postgres rejection (1), контейнерная интеграция ClickHouse (5).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.clickhouse.tests -c Debug` — **208/208**; `dotnet test tests/nextorm.postgres.tests -c Debug` — **266/266**; `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~ClickHouseIntegrationTests.Array"` — **15/15** (0 failed / 0 skipped; включая 5 новых). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** (новых нет). `.editorconfig` — 6 инертных `S*`-silent без `SonarAnalyzer` (пре-существующее, см. «Примечания»).

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `using var bodyVisitor` корректен: `HigherOrderLambdaVisitor` наследует `BaseExpressionVisitor : IDisposable`; `Dispose(bool)` (`BaseExpressionVisitor.cs:403-414`) возвращает pooled `StringBuilder`, `using` гарантирует возврат и при `NotSupportedException` из `VisitMember`. Новых disposable-полей/локалов нет; `CA2000`/`CA2213` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет: `TryTranslateHigherOrderArray` — `switch` по `nameof` + `args.Count`; `EmitHigherOrderArray` — циклы `for (var (i, cnt) = …)`. Аллокации (`Dictionary<ParameterExpression,string>` + визитор) — на холодном пути построения плана, не на выполнении. |
| 4. God-классы | ✅ `ArraySqlTranslator` ~455 строк (было ~407, +48) — порог 500 не перейдён; `EmitHigherOrderArray` ~40 строк, `HigherOrderLambdaVisitor` ~48 (новый `internal`-тип). |
| 5. Хэш-ключи / план-кэш | ✅ Лямбды уже поддержаны: `ExpressionPlanEqualityComparer.CompareLambda` (`:194-244`), `CompareParameter` (`:277-281`), хэшер `VisitLambda`/`VisitParameter` (`:652-658,760-765`). Две структурно одинаковые `v => v > 2` хэшируются и сравниваются одинаково → план-запись делится. Новых членов план-ключа нет. |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет. Ошибочные формы бросают `NotSupportedException` (не-`ClickHouse`-провайдер `:285-287`; не-inline lambda `:335-341`; member-access на параметре `:439-446`; число параметров ≠1 `:297-298`). |
| 7. Дублирование emit-циклов | ⚠️ `EmitHigherOrderArray` (`:293-333`) — очередная копия хвоста `EmitArrayFunction` (`:343-372`); продолжение Находок 15/30, см. ℹ️. |

### 🟡 Находка 62 — вложенная higher-order лямбда молча теряет внешний параметр (ИСПРАВЛЕНА 22.09.2026, P2)

**Место:** `src/nextorm.core/Visitors/ArraySqlTranslator.cs:300-305` (`EmitHigherOrderArray` строит `HigherOrderLambdaVisitor` с картой только текущей лямбды) и `:423-437` (`VisitParameter` эмитит лишь параметры своей карты; неизвестный `ParameterExpression` уходит в `base.VisitParameter`, который ничего не печатает).

**Что не так.** При вложенном higher-order вызове внутри тела лямбды (например `array_map(a => array_exists(b => a == b, e.Nums), e.Nums)`, где `e.Nums` — int[]-колонка) внутренний `EmitHigherOrderArray` получает `visitor = bodyVisitor` и создаёт **новый** визитор с картой `{ b }`; внешний параметр `a` в карту не попадает. `VisitParameter(a)` возвращает узел без эмиссии, и `PredicateTranslator.VisitBinary` рендерит `( = b)` — синтаксически битый SQL вместо понятного `NotSupportedException`; ошибка всплывает только на сервере ClickHouse. Ограничение среза «одна лямбда с одним параметром» в коде не enforced для вложенности (внешний параметр не отслеживается).

**Было:** карта параметров не пробрасывается во вложенный визитор; неизвестный параметр молча ничего не рендерит.

**Исправлено:** добавлено `internal virtual IReadOnlyDictionary<ParameterExpression, string>? LambdaParameters` на `BaseExpressionVisitor`; `HigherOrderLambdaVisitor` его переопределяет, а `EmitHigherOrderArray` объединяет внешнюю карту с картой новой лямбды. Вложенный вызов теперь видит параметр внешней лямбды (`array_map(a => array_exists(b => a == b, nums), nums)` рендерит `arrayMap(a -> arrayExists(b -> (a = b), nums), nums)`). Тест: `NestedHigherOrderLambda_ShouldReferenceOuterParameter` (CH SQL-gen).

**Проверка:** `dotnet test tests/nextorm.clickhouse.tests` — 209/209 (включая новый вложенный тест).

### 🟡 Находка 63 — `HigherOrderLambdaVisitor.Clone()` ослабляет param-режимный инвариант базы (ИСПРАВЛЕНА 22.09.2026, P2, hardening)

**Место:** `src/nextorm.core/Visitors/ArraySqlTranslator.cs:421` (`public override BaseExpressionVisitor Clone() => new HigherOrderLambdaVisitor(Options, _parameters);`) против `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:396-401` (`if (_paramMode) throw new NotSupportedException("Cannot clone in param mode")`).

**Что не так.** Override необходим и корректен по существу: `PredicateTranslator.VisitBinary` (`:305,308`) клонирует визитор при null-aware переписывании, и без переноса карты `_parameters` лямбда-параметр потерялся бы (как в примечании к `CompareLambda`). Но override **не повторяет** param-режимный guard базы. Сегодня все вызовы `Clone()` идут под `!IsParamMode` (`PredicateTranslator.cs:115,137,167,213,303,325`; `WhereExpressionVisitor.cs:19`), поэтому дефект недостижим; при будущем вызове клон в param-режиме получит `_builder == null`, и `ToString()` (`BaseExpressionVisitor.cs:381`) даст `NullReferenceException` вместо понятного исключения.

**Было:** `Clone()` без проверки режима.

**Исправлено:** `Clone()` теперь повторяет guard базы:
`Clone() => IsParamMode ? throw new NotSupportedException("Cannot clone in param mode") : new HigherOrderLambdaVisitor(Options, _parameters);`.

**Проверка:** `roslyn callers NextORM.Core.BaseExpressionVisitor.Clone` — 14 call-site, все под `!IsParamMode`; достижимые пути не изменились, build 0/0.

### ℹ️ Наблюдения (фикс не требуется)

- **Дублирование emit-цикла — продолжение Находок 15/30.** `EmitHigherOrderArray` (`ArraySqlTranslator.cs:293-333`) повторяет хвост `EmitArrayFunction` (`:343-372`: захват `builder.Length` → `ToString(start, len)` → откат → `MakeArrayFunction`) и второй `switch` по `nameof(ClickHouseFunctions.*)` (`:268-280` у `TryTranslateHigherOrderArray`). Отличие (префикс `name(param -> …`) намеренное; общий `EmitCore` не выносился. Не новый номер — тот же класс, что Находки 15/30.
- **`VisitMember`-перехват уже́, чем «любая форма с корнем-параметром».** `IsLambdaParameter` (`:448-454`) разворачивает только цепочку `MemberExpression`; `Convert(v).Member`, `v[i].Member`, `v?.Member` (Conditional) не распознаются и уходят в `base.VisitMember` → `MemberTranslator`. Для элементных `T` слайса 3 (примитивы/строки) невоспроизводимо; при составном `T` — потенциальный «сырой» рендер. Кандидат в guard при расширении слайса.
- **`ExtractLambda` принимает только `Quote(LambdaExpression)`** (`:335-341`): lambda, сохранённая в `Expression<Func<…>>`-переменную, отвергается MemberExpression-ветвью с понятным сообщением; XML-док методов не оговаривает «строго inline» (кандидат в формулировку).
- **param-mode порядок согласован.** Тело обходится до аргументов-массивов (`:304-310`), ровно как печатается SQL (`:319-326`); тот же приём и `DontNeedAlias = false`, что у `AggregateFilter.AppendPredicate` (`AggregateFilter.cs:45-50`). `DontNeedAlias = false` осознан (иначе алиас колонки в теле потерялся бы).
- **`MakeArrayFunction` +3 корректно.** `arrayCount`/`arrayFirstIndex`/`arrayLastIndex` нативно `UInt32` → `toInt64` (`ClickHouseDialect.cs:70`) согласовано с объявленными `long`; `arrayMap`/`arrayFilter`/`arrayFirst`/`arrayLast` не оборачиваются (возвращают `T[]`/`T`). Подтверждено интеграционно (`array_count` = 2, `array_first_index` = 1, `array_last_index` = 3).
- **`HigherOrderLambdaVisitor` — второй тип в `ArraySqlTranslator.cs`** (`:408`), тогда как соседи-хелперы (`SqlOperandTranslator`, `AggregateFilter`) вынесены в одноимённые файлы. Для `internal`-типа — косметика (ср. P2-20 «файл ↔ тип» в `API-NAMING-REVIEW.md`).

**Проверка (22.09.2026).** Build Release — **0/0**; clickhouse **208/208**, postgres **266/266**, интеграция ClickHouse **15/15** (0 skipped; включая 5 новых `ArrayMap`/`ArrayFilter`/`ArrayExistsAndAll`/`ArrayCount`/`ArrayFirstAndLast`). В диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных). `find -name 'PublicAPI*.txt'` — пусто (Шаг 5 открыт). Док-противоречие (provider-specific ClickHouse EN/RU относят higher-order к «out of scope») — `API-NAMING-REVIEW.md`, HOAF2/HOAF3; фикс кода не применялся.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse параметризованные array-агрегаты `topK`/`topKWeighted`/`quantiles` (uncommitted worktree)

**Область.** Capability-слой: новый `ITopKAggregateRenderer` (`src/nextorm.core/DataContext/Dialect/DialectCapabilities.cs:132-143`), новый **абстрактный** член `IQuantileAggregateRenderer.RenderArray` (`:125-126`), DIM `ISqlDialect.TopKAggregates` (`src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:386-392`), `SqlDialectBase.TopKAggregates` (`:93-94`); реализация — `ClickHouseDialect._topKAggregates`/`TopKAggregates`/`MakeAggregate`/`RenderArray`/`ClickHouseTopKAggregateRenderer` (`src/nextorm.clickhouse/ClickHouseDialect.cs:19,27,145,409-410,605,612-619`). Публичный DSL — `ClickHouseFunctions.quantiles`/`top_k`/`top_k_weighted` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:95-118`). Трансляция — `AdvancedAggregateTranslator.TryTranslate` + `EmitQuantiles`/`EmitTopK` (`src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs:160-168,261-322`). Тесты — clickhouse SQL-gen (3) + dialect (mapping, renderer), postgres rejection (2), интеграция ClickHouse (3) + capability-contract.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** (новых нет; `slopwatch` локально не установлен — `.config/dotnet-tools.json` только coverage/reportgenerator/docfx; скан вручную). EOL: все 11 изменённых `.cs` — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `ClickHouseTopKAggregateRenderer` — `internal sealed`, держит только `readonly`-ссылку на `sealed`-диалект, ресурсов нет; `CA2000`/`CA2213`/`CA1816` неприменимы. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11**. |
| 3. LINQ на горячем пути | ✅ Новые циклы — `for (var (i, cnt) = …)` без LINQ (как `EmitSequenceAggregate`); `List<string>` в `EmitQuantiles` строится на построении плана, не на строке. |
| 4. God-классы | ⚠️ `AdvancedAggregateTranslator.cs` — **492** строки (порог 500; ранее 418, +74 этим и соседними срезами). Новых типов нет; `ClickHouseDialect.cs` — 691 (+21). |
| 5. Хэш-ключи / план-кэш | ✅ **Находка 64** исправлена: param-режим `EmitTopK` теперь обходит `Arguments[0]` (`k`), как обычный рендер, поэтому списки `ExtractParams` и `MakeSelectInternal` совпадают (тест `TopK_WithCapturedK_ShouldParameteriseK`). |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет; guard'ы бросают `NotSupportedException` (нет topK у провайдера; не-inline `levels`). |
| 7. NRT / тип возврата | ✅ `T[]`/`double[]` согласованы с нативными `Array(T)`/`Array(Float64)` и существующим `group_array<T> -> T[]`; `T? value` — как у соседей. |

### 🟡 Находка 64 — `EmitTopK`: param-режим пропускал `k`, из-за чего извлечение и рендер параметров расходятся (ИСПРАВЛЕНА 22.09.2026, P2)

**Место.** `src/nextorm.core/Visitors/AdvancedAggregateTranslator.cs:305-313` (`EmitTopK`, ветка `if (visitor.IsParamMode)` обходит только `Arguments[1]`/`Arguments[2]`) против `:316` (`var k = visitor.VisitToString(node.Arguments[0]);` в обычном режиме) и против sibling'а `EmitQuantile` (`:228-233`, обходит **оба** аргумента).

**Что не так.** `QueryPlanner.ExtractParams` (`src/nextorm.core/DataContext/QueryPlanner.cs:47-52`) прогоняет запрос вторым проходом (`paramMode: true`) и на cache-hit при `NeedsParamRefresh` сопоставляет полученный список с параметрами подготовленной команды **по индексу** (`:177-183`). Обычный рендер (`MakeSelectInternal`, `:195-200`) собирает свой список из фактического обхода. Если `k` — захваченная переменная (`long k = 3; … top_k(k, x.Id)`; API это допускает — сигнатура `long k`, а документация лишь оговаривает «k — константа-литерал», guard'а нет), то:
- рендер посещает `Arguments[0]` → добавляет параметр `k` в список (`MemberTranslator.cs:209`, `Stable == false`);
- param-проход `EmitTopK` `k` не посещает → в списке извлечения его нет.

Итог: `pp.Count` меньше `dbCommandParams.Count`. Если `k` — единственный параметр, refresh-цикл не выполняется вовсе и `k` **остаётся от первого выполнения** (на cache-hit эмитится `topK(<старое N>)(id)`); если параметров несколько — сдвиг индексов (обновляются не те значения) и/или срабатывание `Debug.Assert` (`QueryPlanner.cs:182`). Для литерала (`top_k(3, …)`) дефекта нет: `ConstantExpression` в param-режиме ничего не добавляет. Тот же класс, что Находки 35/43 (расхождение двух проходов), но новый триггер.

**Исправлено (вариант A).** В param-ветке `EmitTopK` первым делом добавлен `visitor.Visit(node.Arguments[0]);` — оба прохода теперь посещают `k`, как у sibling'а `EmitQuantile`. Захваченный `k` рендерится параметром `@k` и извлекается тем же проходом (списки совпадают), литерал по-прежнему эмитится константой. Тест `TopK_WithCapturedK_ShouldParameteriseK` (CH SQL-gen) фиксирует `topK(@k)(id)`.

**Проверка (после фикса).** build Release — **0/0**; `dotnet test tests/nextorm.clickhouse.tests` — **220/220** (включая тесты на захваченный `k`, в т.ч. refresh кэш-плана); `postgres` — **268/268**; контейнерная интеграция ClickHouse+capability-contract — **73/73** (0 skipped). Полный прогон с покрытием — **2315 passed / 30 skipped / 0 failed**, **line 85.4% / branch 74.4%** (базис HEAD 85.4%/74.4%).

### ℹ️ Наблюдения (фикс не требуется)

- **Summary `IQuantileAggregateRenderer` не пополнён multi-level формой.** `DialectCapabilities.cs:116-119` перечисляет `quantile(level)(value)`, `median(value)`, но не новый `quantiles(level...)(value)`; член добавлен, summary — прежний (ср. Находка 61). Косметика.
- **`AdvancedAggregateTranslator.cs` 492 строки** — до порога god-class 500 остаётся 8 строк; следующее семейство переведёт файл за порог. Кандидат в декомпозицию (продолжение Находок 33/34).
- **`EmitTopK`/`EmitQuantiles` дублируют каркас `EmitSimple`/`EmitUniq`/`EmitQuantile`** (guard → param-ветка → `NeedAliasForColumn` → рендер) — продолжение Наблюдения к `EmitQuantile` (аудит 19.09.2026); при следующем варианте свести к общему `EmitWithRenderer`.
- **Ограничения среза документированы** (`topK(N)`/`topKWeighted(N)` без `load_factor`/`'counts'`; `quantiles` без `Exact`/`Timing`/`GK`) — соответствует `todo_clickhouse_arrays.md`, не запах.
- **Положительное:** `ClickHouseTopKAggregateRenderer` — `internal sealed` без состояния; контрактный тест (`DialectCapabilityContractTests.cs:123,182`) вызывает `Render`/`RenderWeighted`/`RenderArray` не вакуумно (Находка 48 не повторяется).

**Проверка (22.09.2026).** Build Release — **0/0**; в диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11**; `find -name 'PublicAPI*.txt'` — пусто (Шаг 5 открыт); все изменённые файлы CRLF. Полный прогон с покрытием — **2315 passed / 30 skipped / 0 failed**, **line 85.4% / branch 74.4%** (ClickHouse **220/220**, postgres **268/268**, ClickHouse+capability-contract **73/73**). Публичная сторона — `API-NAMING-REVIEW.md`, CHQA1–CHQA4.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse предикаты над массивами `startsWith`/`endsWith`/`hasSubstr` (uncommitted worktree; чисто)

Изменение (срез 4 `todo_clickhouse_arrays.md`, gap §4 п.9) новых запахов не вносит: три метода `ClickHouseFunctions` (`Query/SqlFunctions.ClickHouse.cs:451,454,462`) — чистые `=> default!` с XML-`<summary>`; три `case` в `Visitors/ArraySqlTranslator.cs:212-220` переиспользуют существующий `EmitArrayFunction`/`RequireArrayFunctions`, гейт `SupportsArrayFunctions`. Добавленных `SuppressMessage`/`#pragma`/`NoWarn`, пустых `catch`, `Task.Delay`/`Thread.Sleep`, `Skip=` в диффе нет; `IDisposable`, события, LINQ и хэш-ключи не затронуты. Ветка транслятора — холодный путь построения плана, новых аллокаций/`.ToList()` не появляется; повтор трёх 3-строчных `case` — принятый в этом `switch` паттерн (как `hasAny`/`hasAll`/`arraySort`), отдельного фикса не требует (ср. наблюдение к `JsonExtractSqlTranslator`).

**Post-check.** `rg "startsWith|endsWith|hasSubstr" src/nextorm.*/*Dialect.cs` — **0 совпадений**; CH-only маппинг гейтится `SupportsArrayFunctions` (`ClickHouseDialect.cs:52`) — как у соседних array-функций; объяснение — `API-NAMING-REVIEW.md`, наблюдение CHARP.

**Проверка (22.09.2026).** Build Release — **0/0** (этот проход); в диффе подавлений/слопа — **0**, соотношение подавлений проекта **11/11** (6 `SuppressMessage` с `Justification` + 5 `#pragma` с `restore`, неоправданных — 0; `Skip=` — 0, пустых `catch` — 0, `Task.Delay` — 2, обе `Task.Delay(0)`-yield, `NoWarn` — только `CS1591` ×7, Шаг 5). Тесты (по отчёту автора изменения): clickhouse **221/221**, postgres **269/269**, контейнерная интеграция ClickHouse **72/72**, полный прогон **2318 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.4%**. Публичная сторона — `API-NAMING-REVIEW.md`, CHARP1–CHARP3.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse скалярная поверхность над `Tuple` (`tuple`/`tupleElement`), срез 5 `todo_clickhouse_arrays.md` (uncommitted worktree; 🟡 — 1, ℹ️ — 5)

**Область.** Новый `internal static TupleSqlTranslator` (`src/nextorm.core/Visitors/TupleSqlTranslator.cs:12-85`): `TryTranslateCreate` (`Tuple.Create(a, b, …)` → `tuple(a, b, …)`), `TryTranslateElement` (`System.Tuple<…>.ItemN` → `tupleElement(t, N)`, только при `node.Expression.Has<ParameterExpression>()`); точки вызова — `BaseExpressionVisitor.VisitMethodCall` (`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:118`) и `MemberTranslator.TryTranslate` (`src/nextorm.core/Visitors/MemberTranslator.cs:86`). Гейт — новый `ISqlDialect.SupportsTupleFunctions` (DIM `=> false`, `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:155-162`; `SqlDialectBase.cs:36`; override `src/nextorm.clickhouse/ClickHouseDialect.cs:66`). Тесты — clickhouse SQL-gen (5), postgres rejection (2), контейнерная интеграция ClickHouse `ITupleEntity` (2, таблица/seed `tuple_entity` уже были с среза 1 — `Providers/ClickHouseTestProvider.cs:167-179`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**; `dotnet test tests/nextorm.clickhouse.tests -c Release --no-build` — **226/226**; `tests/nextorm.postgres.tests` — **271/271** (0 failed / 0 skipped; прогнано заново). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** новых (`Task.Delay(0)` ×2 — baseline в `InMemoryTests.cs`; `NoWarn` — только `CS1591` ×7, Шаг 5; `slopwatch` локально не установлен — `.config/dotnet-tools.json` только coverage/reportgenerator/docfx, скан вручную). `find -name 'PublicAPI*.txt'` — **0**; все изменённые `.cs` — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `TupleSqlTranslator` — `internal static`, без состояния/полей/локалов; классов с ресурсами не добавлено. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет: `TryTranslateCreate`/`TryTranslateElement` — `for (var (i, cnt) = …)` и `if`; `Has<ParameterExpression>()` — обход дерева на холодном пути построения плана. |
| 4. God-классы | ⚠️ `MemberTranslator` — тело класса **499** строк (было **496** на HEAD; порог 500): +3 строки хука оставили класс в одной строке от порога. `TupleSqlTranslator` — 86; `BaseExpressionVisitor` — 430 файла. |
| 5. Хэш-ключи / план-кэш | ✅ Новых членов план-ключа нет. param-режим `TryTranslateCreate` (все аргументы, `:26-32`) и `TryTranslateElement` (выражение-кортеж, `:74-78`) обходят ровно то, что печатает обычный рендер → списки извлечения/рендера совпадают (ср. Находку 64). Тесты `…_WithCapturedValue_/…_WithCapturedFilter_ShouldRefreshParamsOnCachedPlan` покрывают refresh. |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет; ошибочные формы бросают `NotSupportedException` с сообщением `*native tuple type*` (интерфейс+CH — как у соседних array-гейтов). |
| 7. Аллокации на материализации | ✅ Новых аллокаций на выполнение нет: трансляция — только построение SQL на холодном пути; материализация `System.Tuple` — существующая ветка среза 1. |

### 🟡 Находка 65 — `.ItemN`-гард допускает `new Tuple<...>` без tuple-рендера (ОТКРЫТА, P2)

**Место:** `src/nextorm.core/Visitors/TupleSqlTranslator.cs:55-85` (`TryTranslateElement`, проверка `node.Expression.Has<ParameterExpression>()` на `:67`) при том, что `tuple(...)` порождает только `TryTranslateCreate` (`:15-48`), совпадающий с `Tuple.Create`.

**Что не так.** Гард «кортеж ссылается на запрос» пропускает дальше **любую** форму с `ParameterExpression`, а скалярный tuple-рендер умеет порождать только `Tuple.Create`. Для `new Tuple<int, string>(x.Id, x.String!).Item1` (обычный конструктор, который проектное решение намеренно оставляет `NewExpression`-проекцией) `TryTranslateElement` проходит гард, затем `visitor.Visit(node.Expression)` уходит в `VisitNew` → `base.VisitNew` (`BaseExpressionVisitor.cs:295-334`), который печатает аргументы конструктора подряд **без разделителя** и без обёртки `tuple(...)`. На ClickHouse выходит `tupleElement(idint, 1)` (при алиасах — `tupleElement(t.idt.int, 1)`): битый SQL, отклоняемый сервером как неясная ошибка вместо понятного сообщения. На провайдере без tuple-функций путь падает раньше с `NotSupportedException` — дефект ClickHouse-only. До среза 5 эта же форма рендерилась не лучше (`idint` без обёртки, `.ItemN` вообще не эмитился), т.е. это расширение существующей дыры, а не новый регресс, — но теперь вывод маскируется под валидный `tupleElement(...)`.

**Было:** гард `IsTupleType(declaringType) && node.Expression.Has<ParameterExpression>()` пропускает `new Tuple<...>` к рендеру, который его не поддерживает.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** либо **(A)** сузить гард `TryTranslateElement` до выражений, которые точно рендерятся как кортеж — `Tuple.Create`-вызов или mapped tuple-колонка (например, требовать `node.Expression is MethodCallExpression { Method.DeclaringType: typeof(Tuple) }` **или** кортеж-типизированный `MemberExpression`, у которого `MemberTranslator` умеет отдать колонку), иначе оставлять constant folding; либо **(B)** научить `TryTranslateCreate`/`VisitNew` оборачивать `new Tuple<...>(a, b)` в `tuple(a, b)` — тогда обе формы кортежа рендерятся одинаково. Вариант A не трогает задокументированное «`new Tuple<...>` — многоколоночная проекция»; вариант B расширяет поверхность. Тест-кандидат (CH SQL-gen): `e.Select(x => new { V = new Tuple<int, int>(x.Id, x.Int.Value).Item1 })` должен либо рендерить валидный `tupleElement(tuple(id, int), 1)`, либо бросать понятное исключение.

**Проверка:** маршрут прочитан по коду (`TryTranslateElement:67` → `VisitNew:295-334` → `base.VisitNew`); в этом проходе не исполнялся (правило аудита: код не правится). Build 0/0.

### ℹ️ Наблюдения (фикс не требуется)

- **Гард `Has<ParameterExpression>()` для `.ItemN` в остальном верен.** `TryTranslateElement` сначала требует `TypeFacts.IsTupleType(declaringType)`, поэтому массивы перехватить нельзя (`x.Nums[0]` — `IndexExpression`; у массивов нет члена `ItemN`), а член `x.Item1` у сущности с `declaringType` = интерфейс сущности отсекается тем же `IsTupleType`; захваченный/локальный `Tuple` не содержит `ParameterExpression` и сворачивается в константу — подтверждено тестом `TupleElementAccess_OnCapturedTuple_ShouldNotTranslateToSql`. Дефект — только `new Tuple<...>` (Находка 65).
- **Константный `Tuple.Create(1, 'a')` на неподдерживающем провайдере теперь бросает, а `.ItemN` на нём же сворачивается.** `TryTranslateCreate` не имеет `Has<ParameterExpression>()`-гарда: как только встречен `Tuple.Create`, диалект без флага бросает `NotSupportedException`, хотя полностью константный кортеж раньше уходил в `EmitFoldedParameter` (`BaseExpressionVisitor.cs:160-164`). Асимметрия выглядит осознанной (CLR `System.Tuple`-параметр неподдерживающие провайдеры всё равно не забиндили бы, а ранний отказ понятнее серверной ошибки); теста на этот случай нет.
- **Диспетчеризация согласована.** `TupleSqlTranslator` владеет обоими методами; вызовы стоят на канонических хабах — `VisitMethodCall` для method-call и `MemberTranslator.TryTranslate` для member-access (последний — тонкое делегирование `TupleSqlTranslator`). Это повторяет раскладку `ArraySqlTranslator` (вызовы из `NormSqlTranslator`/`SqlOperandTranslator`), без дублирования логики.
- **`Tuple.Create` ничего не затеняет.** Матч узкий: `node.Object is null && node.Method.DeclaringType == typeof(Tuple) && Method.Name == "Create"`; `ValueTuple.Create` (другой `DeclaringType`) и `new Tuple<...>` (`NewExpression`) не задеваются, ветка `Convert.ToXxx` выше (`:102-116`) не пересекается, `ScalarFunctionTranslator` не затрагивается.
- **Кортежи через outer reference join-проекции `.ItemN` не переводятся.** `OuterRefMarker<T>.Ref` — `NewExpression`, `ParameterExpression` в дереве нет → гард false; тот же класс «уже, чем любая parameter-rooted форма», что наблюдение к HOAF. Вне объёма среза.

**Post-check.** `rg "tupleElement|SupportsTupleFunctions" src/nextorm.*/*Dialect.cs` — единственное совпадение `src/nextorm.clickhouse/ClickHouseDialect.cs:64,66` (XML-док + `=> true`); PostgreSQL/SQL Server/MySQL/MariaDB/SQLite — **0**. Соответствует матрице `todo_clickhouse_arrays.md`: first-class `Tuple(T...)`/`tupleElement` есть только у ClickHouse (PostgreSQL composite-тип nextorm не моделирует; остальные tuple-типа не имеют). Рендер `tuple(...)`/`tupleElement(...)` живёт в общем `TupleSqlTranslator`, а не в per-dialect `Make*`-хуке, поэтому «одиночность» диалекта выражена флагом — как у array-семейства.

**Проверка (22.09.2026).** Build Release — **0/0**; `nextorm.clickhouse.tests` **226/226**, `nextorm.postgres.tests` **271/271** (прогнано в этом проходе). В диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных); `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт); изменённые `.cs` — CRLF. Контейнерная интеграция ClickHouse/PostgreSQL в этом проходе не перезапускалась (по отчёту автора: `ClickHouseIntegrationTests` **74/74**, полный прогон **2327 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%**, baseline 85.4%/74.4%). Публичная сторона — `API-NAMING-REVIEW.md`, CHTUP1–CHTUP2.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse array-возвращающие JSON-функции `JSONExtractKeys`/`JSONExtractKeysAndValues`/`JSONExtractArrayRaw`, срез 6 `todo_clickhouse_arrays.md` (uncommitted worktree; 🟡 — 1, ℹ️ — 7)

**Область.** 6 новых публичных методов `ClickHouseFunctions` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:194-224`): `string[] json_extract_keys(string?)`/`(string?, string?)`, `string[] json_extract_array_raw(string?)`/`(string?, string?)`, `Tuple<string, T>[] json_extract_keys_and_values<T>(string?)`/`(string?, string?)`. Трансляция — 3 ветки `JsonExtractSqlTranslator.TryTranslate` (`src/nextorm.core/Visitors/JsonExtractSqlTranslator.cs:44-52`) через существующий `EmitFunction` (`:105`, добавлен необязательный `Type? valueType`); маппинг имён — `ClickHouseDialect.MakeJsonExtract` +3 (`src/nextorm.clickhouse/ClickHouseDialect.cs:198-200`). Гейт — существующий `SupportsJsonExtract` (нового флага нет). Тесты — CH SQL-gen `JsonArrayExtractFunctions_ShouldUseClickHouseNames` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1060`), 3 rejection-записи в `JsonExtract_ShouldThrowBecausePostgresHasNoJsonExtract` (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1838-1840`), контейнерный `JsonArrayExtract_ShouldProjectArrays` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:113-137`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** новых (`Task.Delay(0)` ×2 — baseline `tests/nextorm.core.tests/InMemoryTests.cs:125,414`; `NoWarn` — только `CS1591` ×7, Шаг 5; `slopwatch` локально не установлен — `.config/dotnet-tools.json` только coverage/reportgenerator/docfx, скан вручную). `.editorconfig` — 7 `dotnet_diagnostic.*.severity`, из них 6 инертных `S*`-`silent` без `SonarAnalyzer` (пре-существующее, «Примечания»). Все 7 изменённых файлов (6 `.cs` + todo) — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локалов нет: 6 DSL-заглушек `=> default!`, `EmitFunction` — строковый рендер без ресурсов. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет: `new string[args.Count + …]` + `for`-цикл `VisitToString` — как у соседнего `EmitFunction`, на холодном пути построения плана. |
| 4. God-классы | ✅ `JsonExtractSqlTranslator.cs` — 134 строки (+3 `case`/`valueType`); `SqlFunctions.ClickHouse.cs` — 767 физических, но ~134 собственных (XML-доки/пустые), god-классом не является (см. аудит row reader 22.09.2026). `ClickHouseDialect.cs` — 701 (+10). |
| 5. Хэш-ключи / план-кэш | ✅ `valueType` — типобуква compile-time, не параметр: обычный рендер дописывает литерал, param-режим возвращается до него; списки извлечения и рендера параметров совпадают (класс Находки 64 не воспроизводится). Новых членов план-ключа нет. |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет. Не-`ClickHouse`-провайдер по-прежнему получает `NotSupportedException("*JSONExtract*")` до вывода SQL (`:107-112`), подтверждено postgres rejection-тестом. |
| 7. NRT / возвратный тип | ✅ `string[]` (не `string?[]`) и `Tuple<string,T>[]` соответствуют нативным `Array(String)`/`Array(Tuple(String, value_type))`; форма `System.Tuple<,>` — ровно то, что отдаёт драйвер и распознаёт `TypeFacts.IsTupleType` (`Visitors/TypeFacts.cs:62-75`), материализация — `GetValue`-ветка массива `SelectExpression.GetDataRecordMethod` (`:117-123`). Для nullable `T` — см. Находку 66. |

### 🟡 Находка 66 — nullable `T` в `json_extract_keys_and_values<T>` расходится с `value_type`-литералом (ОТКРЫТА, P2)

**Место:** `src/nextorm.core/Visitors/JsonExtractSqlTranslator.cs:130` (`'...MakeTypeName(Nullable.GetUnderlyingType(valueType) ?? valueType)...'`) + `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs:221,224` (объявлен `Tuple<string, T>[]`); материализация — `src/nextorm.core/Expressions/SelectExpression.cs:117-123` (`GetValue`) и `src/nextorm.core/DataContext/RowMapperFactory.cs:32-44` (`Expression.Convert(object, column.PropertyType)`).

**Что не так.** `T` не ограничен, поэтому вызов `json_extract_keys_and_values<int?>(json)` допустим. Транслятор **разворачивает** `Nullable<int>` до `Int32` и печатает `'Int32'`, т.е. ClickHouse возвращает `Array(Tuple(String, Int32))`, а драйвер — `System.Tuple<string,int>[]` (подтверждено интеграционным тестом для `T=int`). Объявленный же CLR-элемент остаётся `Tuple<string,int?>` (массив — ссылочный тип → `SelectExpression.Nullable = true`), и `RowMapperFactory` вставляет `Convert(object → Tuple<string,int?>[])` = `castclass`. Приведение `Tuple<string,int>[]` к `Tuple<string,int?>[]` невозможно (инвариантность `Tuple<,>`; `int`→`int?` не reference-conversion) → `InvalidCastException` на материализации. Для reference-`T` и non-nullable value-`T` дефект не воспроизводится; тест на nullable `T` отсутствует. Разворот `Nullable` необходим (без него `MakeTypeName` ушёл бы в `base` → `type.Name` = `` Nullable`1 ``, невалидный `value_type`), но не согласован с объявленным типом — тот же класс, что Находка 60 (объявленный тип ≠ тип драйвера).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** либо **(A)** отклонять nullable `T` понятным исключением в `EmitFunction`/на входе (`T` должен быть non-nullable ClickHouse-типом — как и написано в XML: «names the ClickHouse value type»), либо **(B)** рендерить nullable ClickHouse-тип (`Nullable(Int32)`) без разворота, чтобы тип драйвера совпал с `T`; вариант A дешевле и не расширяет тест-матрицу. В любом случае добавить CH SQL-gen/интеграционный тест на `int?`/`string?`.

**Проверка:** маршрут прочитан по коду (`JsonExtractSqlTranslator:130` → `SqlFunctions.ClickHouse.cs:221` → `SelectExpression:117-123` → `RowMapperFactory:32-44`); в этом проходе не исполнялся (правило аудита: код не правится). Build 0/0.

### ℹ️ Наблюдения (фикс не требуется)

- **`Nullable.GetUnderlyingType(...) ?? valueType` — верно для SQL-стороны.** `MakeTypeName` не имеет ветки `Nullable<>` (база уходит в `type.Name`), поэтому разворот обязателен; для `T=int?`/`long?`/`decimal?` литерал получается `Int32`/`Int64`/`Decimal(38, 10)`. Несогласован с объявленным CLR-типом только при nullable `T` — Находка 66.
- **Литерал `'{MakeTypeName(...)}'` — без риска экранирования.** Тип-литерал строится из `System.Type`, а не из ввода пользователя; имя CLR-типа не может содержать `'`, поэтому инъекция/ошибка квотирования невозможны. Жёсткий `'` — общая SQL-конвенция строкового литерала (все диалекты проекта используют `'`), не диалектное знание.
- **Типобуква в core, а не в `Make*`-хуке — приемлемо при одном диалекте.** `visitor.Dialect.MakeTypeName` уже выносит знание о типе в диалект, а сам литерал дописывается в `EmitFunction`; `MakeJsonExtract(name, args)` остаётся N-арным и не меняет сигнатуру. Если `SupportsJsonExtract` когда-либо включат у второго диалекта с иной формой `value_type`, литерал придётся переносить в хук — сейчас это преждевременно (ср. ℹ️ к `visitParamExtract*`).
- **Переиспользование `SupportsJsonExtract` — обосновано (DC-критерий).** Это то же семейство «JSON-as-text из текстовой колонки» (`JSONExtractKeys`/`JSONExtractArrayRaw`/`JSONExtractKeysAndValues` делят `MakeJsonExtract`/транслятор с `JSONExtract*`), `SupportsJsonExtract => true` только у ClickHouse; отдельный флаг был бы истинен ровно там же и добавил бы лишний публичный контракт к Шагу 5. Тот же вывод сделан для `visitParamExtract*` и `startsWith`/`hasSubstr`.
- **`MakeTypeName`-фолбэк даёт не-ClickHouse имена для неотображённых типов.** `Guid`→base `"Guid"` (у ClickHouse тип `UUID`), `sbyte`→`"SByte"`, `char`→`"Char"`; пре-существующее ограничение `MakeTypeName` (используется и в кастах, `ClickHouseDialect.cs:595`), достижимое теперь как `value_type`. `bool`→`"Boolean"` — у ClickHouse `Bool` с алиасом `BOOLEAN` (нужна проверка); `string`/`DateTime`/`int`/`long`/`double`/`decimal` совпадают. Кандидат в тест при расширении набора `T`, не регресс среза.
- **Три новых `case` — принятый паттерн транслятора.** `case nameof(...) when node.Arguments.Count is 1 or 2` + литерал имени — как 13 соседних веток (`JsonExtractSqlTranslator.cs:29-94`); ℹ️-наблюдение о замене литерала на `node.Method.Name` и Находка 15 (дублирование `EmitFunction`/`EmitJsonPathFunction`) — пре-существующие, срез их не расширяет (ветки не копируют тела).
- **Post-check.** `rg "JSONExtractKeys|JSONExtractArrayRaw|json_extract_keys" src/nextorm.*/*Dialect.cs` — **3 совпадения, все в `src/nextorm.clickhouse/ClickHouseDialect.cs:198-200`**; PostgreSQL/SQL Server/MySQL/MariaDB/SQLite — **0**. Соответствует матрице `todo_clickhouse_arrays.md` (срез 6): `Array(Tuple(String, value_type))`/`Array(String)` есть только у ClickHouse; у PostgreSQL/MySQL/MariaDB/SQLite эквиваленты set-returning/JSON-текстовые, у SQL Server — table-valued `OPENJSON`. «Одиночность» диалекта выражена существующим `SupportsJsonExtract`, а не отдельным токеном.

**Проверка (22.09.2026).** Build Release — **0/0**; в диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных); `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт); изменённые файлы — CRLF. Тесты (по отчёту автора изменения): clickhouse unit **228/228**, postgres **271/271**, контейнерная интеграция `ClickHouseIntegrationTests` **75/75**, полный прогон **2330 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%** (baseline 85.4%/74.5%). Публичная сторона — `API-NAMING-REVIEW.md`, CHJS1–CHJS3.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse иерархические dictionary-функции `dictGetHierarchy`/`dictGetChildren`/`dictIsIn`, срез 7 `todo_clickhouse_arrays.md` (uncommitted worktree; 🟡 — 1, ℹ️ — 5)

**Область.** 3 новых публичных метода `ClickHouseFunctions` (`src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`): `ulong[] dict_get_hierarchy<TKey>(string?, TKey?)` (`:316`), `ulong[] dict_get_children<TKey>(string?, TKey?)` (`:323`), `bool dict_is_in<TKey>(string?, TKey?, TKey?)` (`:331`). Трансляция — 3 ветки `DictionarySqlTranslator.TryTranslate` (`src/nextorm.core/Visitors/DictionarySqlTranslator.cs:31-39`) через существующий `EmitFunction` (`:45-66`; текст исключения и классовая `<summary>` дополнены, `:48`,`:6-8`); маппинг имён — `ClickHouseDialect.MakeDictionaryFunction` +3 (`src/nextorm.clickhouse/ClickHouseDialect.cs:242-244`). Гейт — существующий `SupportsDictionaries` (нового флага нет). Тесты — CH SQL-gen `HierarchicalDictFunctions_ShouldUseClickHouseNames` (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1173`), `HierarchicalDictFunctions_WithCapturedDict_ShouldRefreshParamsOnCachedPlan` (`:1191`), `ClickHouseDialectTests.MakeDictionaryFunction_ShouldMapNames` (+3, `tests/nextorm.clickhouse.tests/ClickHouseDialectTests.cs:254-256`), postgres rejection +3 (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:1875-1877`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=`/`Task.Delay`/`Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** новых (`Task.Delay(0)` ×2 — baseline `tests/nextorm.core.tests/InMemoryTests.cs:125,414`; `NoWarn` — только `CS1591` ×7, Шаг 5; `slopwatch` локально не установлен — `.config/dotnet-tools.json` только coverage/reportgenerator/docfx, скан вручную). `.editorconfig` — 7 `dotnet_diagnostic.*.severity`, из них 6 инертных `S*`-`silent` без `SonarAnalyzer` (пре-существующее). Все 6 изменённых `.cs` — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локалов нет: 3 DSL-заглушки `=> default!`, `EmitFunction` — строковый рендер без ресурсов. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет; изменённый код — `switch`/`for` на холодном пути построения плана. |
| 4. God-классы | ✅ `DictionarySqlTranslator.cs` — 67 строк (+10); `SqlFunctions.ClickHouse.cs` — +41 физических строк XML/заглушек; новых god-классов/длинных списков параметров нет. |
| 5. Хэш-ключи / план-кэш | ✅ Новых членов план-ключа нет. param-режим `DictionarySqlTranslator.EmitFunction` (`:52-58`) обходит те же аргументы, что печатает обычный рендер → списки извлечения/рендера совпадают (класс Находки 64 не воспроизводится). |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет. Не-`ClickHouse`-провайдер получает `NotSupportedException("*dictionary*")` до вывода SQL (`:47-48`), подтверждено postgres rejection-тестом. |
| 7. NRT / возвратный тип | ✅ `ulong[]` (не `ulong?[]`) и non-nullable `bool` соответствуют нативным `Array(UInt64)`/`UInt8`; array-ветка row reader — `GetValue`+cast (`SelectExpression.cs:117-123`), `UInt8`→`bool` — как `dict_has`. |

### 🟡 Находка 67 — тест `HierarchicalDictFunctions_WithCapturedDict_ShouldRefreshParamsOnCachedPlan` не проверяет то, что заявлено в имени (ИСПРАВЛЕНА 22.09.2026, P2)

**Место:** `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1190-1205`.

**Что не так.** Тест называет себя проверкой refresh параметров на закэшированном плане, но (а) `dict` не меняется между двумя вызовами `Build()` (оба раза `"dict"`), (б) ассертится только `CommandText.Contains("dictGetHierarchy(@dict, id)")` — ни `DbCommand.Parameters["@dict"].Value`, ни факт повторного использования плана не проверяются. Фактически тест покрывает лишь param-режимную ветку `EmitFunction` (захваченный `dict` превращается в `@dict`), т.е. утверждение имени («refresh params on cached plan») не выполняется. Паттерн скопирован с `TopK_WithCapturedK_ShouldRefreshParamsOnCachedPlan` (там он обоснован Находкой 64 — расхождением списков `ExtractParams`/рендера при захваченном `k`); у dictionary-функций такого расхождения нет, param-ветка не менялась. Дефекта не создаёт (текст `@dict` действительно регрессировал бы при потере параметризации), но имя вводит в заблуждение и создаёт ложное ощущение покрытия refresh.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** либо **(A)** усилить: между вызовами изменить захваченное значение (`dict = "d2"`) и дополнительно проверять `DbCommand.Parameters["@dict"].Value.Should().Be("d2")` (как refresh-семантика у соседних семейств); либо **(B)** переименовать в `HierarchicalDictFunctions_WithCapturedDict_ShouldParameteriseTheDict` и ограничиться проверкой текста — тогда имя соответствует проверяемому. Вариант B отражает реальный объём покрытия, вариант A добавляет то, что заявлено.

**Проверка:** код теста прочитан (`SqlGenerationTests.cs:1190-1203`); в этом проходе не исполнялся (правило аудита: код не правится). Build 0/0.

**Стало (исправлено автором изменения 22.09.2026, вариант A).** Тест теперь меняет захваченное значение между вызовами (`dict = "dict2"`) и проверяет refresh: `second.DbCommand.Parameters["dict"].Value.Should().Be("dict2")` (`SqlGenerationTests.cs:1200-1204`). Выяснено при правке: ClickHouse-коллекция хранит имя без префикса (`dict`, не `@dict`), в SQL-литерале — `@dict`. Усиленный тест зелёный (clickhouse `HierarchicalDict*` 2/2). Находка закрыта.

### ℹ️ Наблюдения (фикс не требуется)

- **`ulong[]` для `Array(UInt64)` — верная форма, интеграционного теста нет намеренно.** `dictGetHierarchy`/`dictGetChildren` всегда возвращают `Array(UInt64)`; `ClickHouse.Driver` 1.4.0 отдаёт `ulong[]` (элемент — framework-тип скалярного `UInt64`, уже первоклассный: `SelectExpression.cs:113-115`, интеграционные `UInt64Columns_ShouldMaterializeAsUlong`/`UInt64Projection_ShouldMaterializeValueAboveInt64Max`). Материализация идёт общей array-веткой `GetValue`+cast (`:117-123`). Интеграционный тест недоступен: нужен `CREATE DICTIONARY ... HIERARCHICAL` (у существующих `dictGet*` интеграционных тестов тоже нет) — пре-существующее ограничение D-раздела, зафиксировано в `todo_clickhouse_arrays.md` §«Срез 7».
- **`bool` для `dictIsIn` — тот же путь, что `dictHas`.** Нативный `UInt8` → `bool`, подтверждено существующими `dict_has`/`JSONHas`; отдельного риска нет.
- **`ulong` — не CLS-проблема.** `[assembly: CLSCompliant(true)]` в репозитории нет (`rg` — 0), поэтому публичный `ulong[]` не даёт `CS3003`/`CS3001`; сборка 0/0.
- **Дублирование `EmitFunction` не расширено.** Три новых `case` не копируют тело (одна строка `EmitFunction(...)` + `return true`), как 3 существующих dictionary-ветки; пре-существующее ℹ️ о сведении `EmitFunction`-копий (аудит `dictGet*` 19.09.2026, Находка 15) остаётся в силе, но срез его не ухудшает.
- **Post-check.** `rg "dictGetHierarchy|dictGetChildren|dictIsIn" src/nextorm.*/*Dialect.cs` — только `src/nextorm.clickhouse/ClickHouseDialect.cs:242-244` (+ XML-док в `SqlFunctions.ClickHouse.cs`); override `MakeDictionaryFunction` — единственный, в `ClickHouseDialect.cs`. PostgreSQL/SQL Server/MySQL/MariaDB/SQLite — **0**. Соответствует матрице `todo_clickhouse_arrays.md`: hierarchical dictionaries есть только у ClickHouse (эквивалент — рекурсивный `WITH RECURSIVE`, другой контракт); «одиночность» выражена существующим `SupportsDictionaries`, а не новым токеном.

**Проверка (22.09.2026).** Build Release — **0/0**; в диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных); `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт); изменённые `.cs` — CRLF. Тесты (по отчёту автора изменения): полный прогон **2333 passed / 30 skipped / 0 failed**, покрытие **line 85.4% / branch 74.5%**. Публичная сторона — `API-NAMING-REVIEW.md`, CHDH1–CHDH3.

## 🔎 Точечный аудит 22.09.2026 — ClickHouse `SEMI`/`ANTI`/`PASTE` joins (uncommitted worktree; 🟡 — 4, ℹ️ — 6)

**Область.** Новые виды join'а в ClickHouse. Поверхность: `JoinType.Semi=8`/`Anti=9`/`Paste=10` (`src/nextorm.core/Expressions/JoinExpression.cs:28,33,39`), гейты `ISqlDialect.SupportsSemiAntiJoin`/`SupportsPasteJoin` (DIM `=> false`, `ISqlDialect.cs:71,78`) + `SqlDialectBase.cs:30-31` + `src/nextorm.clickhouse/ClickHouseDialect.cs:48,51`, валидация/рендер (`DataContext/SqlSourceRenderer.cs:89-103`, keyword — `ClickHouseDialect.cs:92-105`), DSL: `EntityBuilder<TEntity>.SemiJoin`/`AntiJoin`/`PasteJoin` + 3 `QueryCommand`-перегрузки (`Builders/EntityBuilder.cs:981-1006`), non-generic `EntityBuilder` (6 методов, `:1506-1554`), 6 классов `JoinedEntityBuilder<T1..T7>` × 3 `new`-метода (18, `Builders/Joins/JoinedEntityBuilder.cs:36-51,122-133,188-199,254-265,320-331,386-397`; терминал `T8` не расширяется), in-memory (`DataContext/InMemoryJoin.cs:26-27`, `InMemoryQueryBuilder.cs:246,366`), обобщение `MakeFrom` (`SqlSourceRenderer.cs:251-254,416-419`) и удалённый `using System.Diagnostics` (`:1`). Тесты — CH SQL-gen 8 (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:1782-1924`), dialect keyword (`ClickHouseDialectTests.cs:77-85`), postgres rejection +1 (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:402-417`), in-memory +1 (`tests/nextorm.core.tests/InMemoryJoinTests.cs:95-114`), интеграционные +4 (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:707-756`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=` — 0; `Task.Delay` — **2** (обе baseline `Task.Delay(0)` в `tests/nextorm.core.tests/InMemoryTests.cs:125,414`); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** новых (`NoWarn` — только `CS1591` ×7, Шаг 5). `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан выполнен вручную. `.editorconfig` — 7 `dotnet_diagnostic.*.severity`, из них 6 инертных `S*`-`silent` без `SonarAnalyzer` (пре-существующее). Все 9 изменённых `.cs` — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локалов нет: enum-члены, `bool`-флаги, существующие `StringBuilder`/пулы. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` в диффе — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет; `AddSemiAntiJoin`/`PasteJoin` — `List.Add`/`AddRange` на холодном пути построения плана. |
| 4. God-классы | 🟡 **Находка 68**: `EntityBuilder.cs` 1470→1561 (+91), `JoinedEntityBuilder.cs` +89 — новых god-классов нет, но дублирование по арностям усугубилось (ниже). |
| 5. Хэш-ключи / план-кэш | ✅ `JoinType` входит в `JoinExpressionPlanEqualityComparer` (`Equals:34`, `GetHashCode:54`) и в `JoinExpression`-ключ; `CloneForCache` переносит join целиком. Новых членов ключа нет. |
| 6. События / исключения | ✅ Подписок нет; новые `NotSupportedException` — гейты (`SqlSourceRenderer.cs:92,97,101`, `InMemoryJoin.cs:27`, `InMemoryQueryBuilder.cs:246,366`), не пустые `catch`. Обобщение `MakeFrom` сняло один `Debug.Assert` (🟡 **Находка 71**). |
| 7. NRT / возвратный тип | ✅ `SemiJoin`/`AntiJoin` возвращают `EntityBuilder<TEntity>` (только левые колонки), `PasteJoin` — `JoinedEntityBuilder<TEntity,TJoinEntity>` (обе стороны); nullable-аннотации `Expression<Func<...>>` не менялись. |

### 🟡 Находка 68 — `PasteJoin` и `AddSemiAntiJoin` дублируют `JoinCore`/`ReplaceLastJoin` вместо делегирования (ОТКРЫТА, P2)

**Место:** `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs:42-51,128-133,194-199,260-265,326-331,392-397`; `src/nextorm.core/Builders/EntityBuilder.cs:997,1006,1021-1029,1515-1516,1554-1555`; `AddSemiAntiJoin` — `EntityBuilder.cs:1007-1020,1517-1527`; пре-существующий дубль-образец — `ReplaceLastJoin` `EntityBuilder.cs:468-494`.

**Что не так.** `PasteJoin` нигде не добавляет ничего нового по сравнению с уже существующим `JoinCore(_, JoinType.Paste, null)`:

- `PasteJoinCore<TJoinEntity>(JoinExpression)` (`:1021-1029`) делает ровно `CreateJoined<TJoinEntity>(join, ResolveJoinBase())` + `ApplyWhereToJoined(joined)`, т.е. тело `JoinCore<TJoinEntity>` (`:1030-1040`). Отличие только в том, что `JoinExpression` собирает вызывающий. Все 3 entry point'а (`:996`,`:1005` и generic-`JoinCore`-путь) могли бы звать `JoinCore` напрямую (`PasteJoin(EntityBuilder<TJoinEntity> _) => JoinCore(_, JoinType.Paste, null)`; `PasteJoin(QueryCommand<TJoinEntity> q) => JoinCore(q, JoinType.Paste, null)`).
- Каждый из 6 per-arity `PasteJoin` (тела `:43-51,129-133,195-199,261-265,327-331,393-397`) — побайтово тот же state-copy + `AddRange(Joins)` + `Add(new JoinExpression(null, JoinType.Paste){From=GetFrom(Tn),EntityType=typeof(Tn)})`, что и `JoinCore<Tn>` при `joinCondition == null` (`JoinCore` вычисляет `EntityType = joinCondition is null ? typeof(Tn) : null` — здесь тоже `typeof(Tn)`). Достаточно `=> JoinCore(_, JoinType.Paste, null)`; это убирает ~75 строк и единственный способ, которым новая ветка может разойтись с обычными join'ами.
- Non-generic `EntityBuilder.PasteJoin(EntityBuilder)` (`:1515-1516`) и `PasteJoin<TJoinEntity>` (`:1554-1555`) равны `JoinCore(from, JoinType.Paste, null)` (`:1528-1532`) и `JoinCore<TJoinEntity>(_, JoinType.Paste, null)` (`:1556-1560`).
- Оба `AddSemiAntiJoin` (`:1007-1020`, `:1517-1527`) повторяют clone/ensure-list-паттерн, который уже есть в `ReplaceLastJoin` (`:473-480`). Отдельный общий helper (напр. `private List<JoinExpression> EnsureJoinListCopy()`) убрал бы третью копию.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** удалить `PasteJoinCore` и тела per-arity `PasteJoin`, свести их к `JoinCore(_, JoinType.Paste, null)` (и `JoinCore(query, JoinType.Paste, null)` для `QueryCommand`-форм); вынести клонирование+инициализацию списка join'ов в один helper, переиспользуемый `AddSemiAntiJoin` и `ReplaceLastJoin`. Поведение не меняется — тесты `PasteJoin_*`/`SemiAntiPaste_OnJoinedLhs_ShouldRenderAtEveryArity` должны остаться зелёными.

**Проверка:** build Release **0/0**; тесты в этом проходе не перезапускались (правило аудита: код не правится).

### 🟡 Находка 69 — `EntityBuilder<TEntity>.AddSemiAntiJoin` принимает `rightEntityType`, но не использует его (ОТКРЫТА, P2)

**Место:** `src/nextorm.core/Builders/EntityBuilder.cs:1007,1017`; call-sites `:982,990,1000,1003`.

**Что не так.** Сигнатура `private EntityBuilder<TEntity> AddSemiAntiJoin(FromExpression rightSource, Type rightEntityType, LambdaExpression joinCondition, JoinType joinType)` принимает `Type rightEntityType`, но тело его не читает: `joins.Add(new JoinExpression(joinCondition, joinType) { From = rightSource, EntityType = null });`. Четыре вызова всё равно передают `typeof(TJoinEntity)`. Компилятор неиспользуемый параметр не диагностирует (IDE0060 не включён), поэтому build 0/0 дефект не ловит. `EntityType = null` корректен: для SEMI/ANTI `joinCondition` всегда непустой, и `JoinCore` при непустом условии тоже ставит `EntityType = null` (правый тип берётся из `joinCondition.Parameters[1].Type` в `SqlSourceRenderer.MakeJoin:165`), так что параметр — просто мёртвый код, а не забытая запись.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** удалить параметр `Type rightEntityType` из `AddSemiAntiJoin` и из 4 call-sites либо (если он задуман как страховка) записать `EntityType = rightEntityType` и убрать `null` — но тогда это должно быть осознанным решением, а не расхождением с `JoinCore`. Предпочтительно первое.

**Проверка:** build Release **0/0**; тело метода прочитано (`:1007-1020`).

### 🟡 Находка 70 — `PasteJoinCore<TJoinEntity>(JoinExpression)`: тип-параметр не выводится и не связан с `join.EntityType` (ОТКРЫТА, P2; следствие/усиление Находки 68)

**Место:** `src/nextorm.core/Builders/EntityBuilder.cs:1021-1029`; вызовы `:997,1006`; источник типа — `new JoinExpression(...) { EntityType = typeof(TJoinEntity) }`.

**Что не так.** `PasteJoinCore<TJoinEntity>(JoinExpression join)` — параметр всего один и он непараметрический `JoinExpression`, поэтому `TJoinEntity` **не может быть выведен** ни из одного аргумента: все вызовы обязаны писать `<TJoinEntity>` явно. При этом `TJoinEntity` (тип возврата `JoinedEntityBuilder<TEntity, TJoinEntity>`) и `join.EntityType` (правая сторона для рендера) задаются независимо на call-site, и ничто не проверяет их совпадение: `PasteJoinCore<A>(new JoinExpression(null, Paste) { From = ..., EntityType = typeof(B) })` компилируется и даёт `JoinedEntityBuilder<TEntity, A>` для источника `B` — расхождение проявится только в рантайме/на неверном SQL. `JoinCore<TJoinEntity>(EntityBuilder<TJoinEntity> _, ...)`, наоборот, выводит тип из аргумента и сам ставит `EntityType`, поэтому эта ветка и защищена.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** устранить helper вместе с Находкой 68 — `PasteJoin` идёт через `JoinCore(_, JoinType.Paste, null)`, где `TJoinEntity` и `EntityType` берутся из одного источника. Если helper сохранять, принимать `Type joinEntityType` и строить `JoinExpression` внутри, чтобы тип возврата и `EntityType` собирались в одном месте.

**Проверка:** build Release **0/0**; сигнатура и оба call-site прочитаны (`:997,1006,1021`).

### 🟡 Находка 71 — обобщение `MakeFrom` сняло `Debug.Assert(joins ⇒ IProjection)` без замены: инвариант ослаблен (ОТКРЫТА, P2/hardening)

**Место:** `src/nextorm.core/DataContext/SqlSourceRenderer.cs:249-257` (table-ветка), `:416-419` (table-function-ветка); `FromRenderOptions`/`hasJoins` — `SqlBuilder.cs:78-96`; `MakeFrom`-сигнатура — `:205`.

**Что не так.** До изменения обе ветки при `hasJoins` содержали `Debug.Assert(typeof(IProjection).IsAssignableFrom(entityType))` и брали `entityType.GetGenericArguments()[0]`; иначе добавляли `entityType`. Теперь ветка выбирается по `hasJoins && typeof(IProjection).IsAssignableFrom(entityType)`, а `Debug.Assert` удалён. Для новой фичи это **необходимо и корректно** (SEMI/ANTI на плоской сущности дают `TEntity` без проекции — покрыто `SemiAntiPaste_OnJoinedLhs_ShouldRenderAtEveryArity`), но `MakeFrom` получает только `bool hasJoins` и потому не может отличить «join, который оставил левую сторону плоской» (SEMI/ANTI) от обычного join'а, для которого левая сторона обязана быть `IProjection`. Инвариант «join ⇒ проекция» больше нигде не проверяется: если в будущем обычный join (или PASTE/новый вид) попадёт с непроекционным LHS, `Add(entityType)` молча отрендерит не тот набор колонок вместо assert — ровно та ошибка, которую ловил удалённый assert.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** сохранить проверку хотя бы в DEBUG — либо прокинуть в `MakeFrom`/`FromRenderOptions` признак, различающий виды join'а (напр. `bool leftMayBeFlatEntity` или сам `JoinType`), и оставить `Debug.Assert(hasJoins is false || IProjection || leftMayBeFlatEntity)`; либо на call-site (SqlBuilder) ассертить, что непроекционный LHS с join'ами допускается только для `JoinType.Semi/Anti`. Как минимум — комментарий, фиксирующий новое условие вместо удалённого assert.

**Проверка:** до/после сравнено по `git diff` (`:249-257,416-419`); build Release **0/0**; новая ветка покрыта CH SQL-gen тестами (SEMI/ANTI плоской сущности и на проекции).

### ℹ️ Наблюдения (фикс не требуется)

- **Ветка `_ => throw` в `ClickHouseDialect.MakeJoinKeyword` (`:101`) недостижима** — внешний `if` ограничивает `joinType` тремя значениями; безвредна, но при желании заменяется на `SwitchExpressionException`/`unreachable`.
- **`(isGlobal ? " global" : "")` для SEMI/ANTI/PASTE (`:104`) недостижима из рендера** — `SqlSourceRenderer.MakeJoin:91-92` бросает на `join.IsGlobal` для этих видов. Публичный метод допускает `isGlobal:true` и вернул бы невалидный ` global paste join `, но единственный in-repo вызов защищён; ℹ️.
- **`throw new NotImplementedException();` без сообщения скопирован** в `JoinedEntityBuilder<T2>.PasteJoin` (`:44-45`) — ровно как в существующем `JoinCore<T3>` (`:54-55`); консистентно, но `NotSupportedException`+сообщение читались бы лучше (код-сторона, не блокер).
- **`EntityType = null` для SEMI/ANTI согласован с `JoinCore`** — правый тип для `ON` берётся из `joinCondition.Parameters[1].Type` (`SqlSourceRenderer.cs:165`); ℹ️.
- **Обобщение `MakeFrom` — необходимо для фичи, но меняет debug-контракт** — см. Находку 71; само условие `hasJoins && IProjection` симметрично в table- и TVF-ветках (`:251`, `:419`), регресса для существующих форм нет.
- **Отсутствие интеграционного теста на `Select` правых колонок при SEMI/ANTI — намеренно** — API-форма не даёт к ним доступ (возврат `EntityBuilder<TEntity>`), так что «нельзя спроецировать правые» проверено типом, а не тестом; ℹ️.

**Проверка (22.09.2026).** Build Release — **0/0**; в диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных); `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт); 9 изменённых `.cs` — CRLF. Тесты в этом проходе не перезапускались; публичная сторона — `API-NAMING-REVIEW.md`, CHJ1–CHJ4.

## 🔎 Точечный аудит 22.09.2026 — доступ к колонкам mapped-сущностей по имени (`SqlFunctions.Column<T>`), uncommitted worktree; 🟡 — 2, ℹ️ — 8

**Область.** Новый публичный маркер `SqlFunctions.Column<T>(object entity, string columnName)` (`src/nextorm.core/Query/SqlFunctions.cs:75`, тело — `throw new NotSupportedException`); трансляция `TranslateNormParam`→`TranslateColumn` (`src/nextorm.core/Visitors/NormSqlTranslator.cs:40-44,75-85`); рефакторинг `EmitTableAliasColumn` → `AppendColumnReference`/`AppendColumnAlias`/`UnwrapConvert` (`src/nextorm.core/Visitors/BaseExpressionVisitor.cs:185-199,206-212,219-246,248-251`). Тесты — CH SQL-gen 3 (`tests/nextorm.clickhouse.tests/SqlGenerationTests.cs:2595-2628`), postgres +1 (`tests/nextorm.postgres.tests/SqlGenerationTests.cs:160-170`), sqlite +1 (`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:367-377`), core in-memory +1 (`tests/nextorm.core.tests/InMemoryTests.cs:852-861`), интеграция ClickHouse +2 и seed `wide_entity` (`tests/nextorm.integration.tests/ClickHouseIntegrationTests.cs:25-44,1240-1246`, `Providers/ClickHouseTestProvider.cs:74-86`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=` — 0; `Task.Delay` — **2** (обе baseline `Task.Delay(0)` в `tests/nextorm.core.tests/InMemoryTests.cs:125,414`); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0** новых (`NoWarn` — только `CS1591` ×7, Шаг 5). `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан выполнен вручную. `.editorconfig` — 7 `dotnet_diagnostic.*.severity` (S125/S108/S3060/S1104/S3604/S2292 — `silent` + `CA2254`), 6 инертных `S*` без `SonarAnalyzer` (пре-существующее). 9 изменённых `.cs` — CRLF; `docs/specs/roadmap/todo_clickhouse_columns_by_name.md` — **LF-only** (ℹ️ ниже).

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локалов нет: `Column<T>` — `static`-маркер без ресурсов, `TranslateColumn`/`AppendColumnAlias` работают с существующим `StringBuilder`. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Новых LINQ-цепочек нет; изменённый код — один `if`-диспетчер и обход выражения на холодном пути построения плана. |
| 4. God-классы | ✅ `EmitTableAliasColumn` сокращён (~50→15 строк), alias-логика вынесена в `AppendColumnAlias` (~28); `BaseExpressionVisitor` — 449 строк (<500, god-класс не появился); `NormSqlTranslator` — 435. |
| 5. Хэш-ключи / план-кэш | ✅ Участие в ключе подтверждено: `SelectExpressionPlanEqualityComparer` хэширует `SelectExpression.Expression` через `ExpressionPlanEqualityComparer`, который на `MethodCallExpression` добавляет `node.Method` (конструированный generic-метод `Column<ulong>` ≠ `Column<string>`) и на `ConstantExpression` — строку имени (`ExpressionPlanEqualityComparer.cs:590-620,728-734`); `QueryPlanEqualityComparer.Equals` сравнивает `SelectList`. Новых членов ключа нет. |
| 6. События / исключения | ✅ Подписок нет; новые `throw` — `NotSupportedException` в теле маркера (in-memory) и на неверной форме аргументов (`NormSqlTranslator.cs:78`); пустых/общих `catch` нет. |
| 7. NRT / возвратный тип | ✅ `T Column<T>` — осознанно non-nullable (nullable-вариант достижим как `Column<string?>`); `object entity` намеренно (ср. `EF.Property<T>`); аннотации остальных членов не менялись. |

### ✅ Находка 72 — после добавления `Column` XML-summary и имя `TranslateNormParam` в `NormSqlTranslator` устарели (ЗАКРЫТА 22.09.2026, была P2)

**Место:** `src/nextorm.core/Visitors/NormSqlTranslator.cs:6-11` (классовый `<summary>`), `:37` (summary `TranslateNormParam`), `:38-44` (диспетчер).

**Что не так.** Классовый `<summary>` по-прежнему описывает транслятор как перевод «`SqlFunctions.Parameter` и built-ins `CommonFunctions`», а summary `TranslateNormParam` — «Emits `SqlFunctions.Parameter` as a named parameter; any other `SqlFunctions` method throws». После этого изменения `TranslateNormParam` первым делом диспетчеризует `SqlFunctions.Column` в `TranslateColumn`, т.е. «any other method throws» больше неверно, а `Column` в summary не упомянут; имя метода («param») тоже сузилось до одного из двух маркеров. Тот же класс, что Находка 61 (устаревшие XML-summary после добавления helper'ов); на сборку не влияет (`internal static class`).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** дополнить классовый `<summary>` и summary `TranslateNormParam` упоминанием `SqlFunctions.Column<T>` (или переименовать в `TranslateSqlFunctionsMarker`), сохранив остальной текст. Поведение не меняется.

**Проверка:** summary и тела прочитаны (`:6-11,37-44,75-85`); build Release `0/0`. **Закрыто 22.09.2026:** классовый summary упоминает `SqlFunctions.Parameter` и `SqlFunctions.Column<T>` (`NormSqlTranslator.cs:6-11`), summary `TranslateNormParam` — «Emits `SqlFunctions.Parameter`/`SqlFunctions.Column<T>`; any other `SqlFunctions` method throws» (`:36-37`); имя оставлено (косметика отдельного фикса не требует).

### 🟡 Находка 73 — `Column<T>(object entity, …)` не проверяет, что `entity` — параметр источника: опечатка молча даёт голый/чужой идентификатор (ОТКРЫТА, P2)

**Место:** `src/nextorm.core/Query/SqlFunctions.cs:75` (`object entity`); `src/nextorm.core/Visitors/NormSqlTranslator.cs:75-85`; `src/nextorm.core/Visitors/BaseExpressionVisitor.cs:206-246` (ранний `return` при `!v.Has` — `:223-224`).

**Что не так.** Первый параметр объявлен `object`, поэтому компилятор принимает **любое** выражение: `Column<int>(new object(), "x")`, `Column<int>(x.Id, "x")`, `Column<int>(x.A + x.B, "c")`. `AppendColumnAlias` ищет в аргументе `ParameterExpression` (`v.Visit`), и если не находит — молча `return` (`BaseExpressionVisitor.cs:223-224`), после чего `AppendColumnReference` печатает голое имя колонки (`:211`). Итог: вызов с сущностью не из запроса/с производного выражения не падает, а рендерит `select x` (без таблицы) или `t1.x`, где `t1` — первый найденный параметр (`v.Target`), т.е. ассоциация «сущность → колонка» теряется. Это расходится с mapped-путём `MemberTranslator`, который в такой ситуации бросает `BuildSqlCommandException("Cannot resolve column for member …")` (`MemberTranslator.cs:417`), и позволяет молча получить синтаксически валидный, но неверный SQL. Публичная форма (`object`) — сознательный выбор под эргономику (ср. `EF.Property<T>(object, string)`), поэтому дефект — в отсутствии проверки, а не в сигнатуре как таковой.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** в `TranslateColumn`/`AppendColumnReference`, когда разрешение алиаса включено (`_columnsProvider.HasAliases && !_dontNeedAlias`), требовать, чтобы аргумент разрешался в `ParameterExpression` источника (`v.Has`), иначе бросать `BuildSqlCommandException`/`NotSupportedException` с понятным сообщением (по образцу `MemberTranslator.cs:417`). Добавить негативный тест (`Column<int>(new object(), "id")` → исключение). Поведение корректных вызовов не меняется.

**Проверка:** сигнатура, `TranslateColumn` и `AppendColumnReference`/`AppendColumnAlias` прочитаны; в этом проходе не исполнялось (правило аудита: код не правится). Build `0/0`.

### ℹ️ Наблюдения (фикс не требуется)

- **Рефакторинг `EmitTableAliasColumn` — поведение сохранено.** Alias-блок перенесён в `AppendColumnAlias` дословно (та же `TypeExpressionVisitor<ParameterExpression>`, ветка `IProjection`+`p.ItemN`, `ProjectionAliasCache.GetOccurrence`, `AliasResolver.GetAliasFromParam`); для `TableAlias`-аксессоров путь идентичен. Единственное отличие — проверка `TableAliasAccessors.IsAccessor` перенесена перед эмиссией алиаса (`:187-188` против старого порядка). Наблюдаемого эффекта нет: при `false` метод бросает в обоих вариантах, а новый порядок оставляет меньше мусора в пуле `StringBuilder`; `TableColumn`-ветка (`VisitMethodCall:140-143`) не затронута.
- **`UnwrapConvert` корректен и нужен.** `Column<T>` принимает `object`, поэтому value-type сущность/`p.Item2`-значимый тип оборачивается `Convert(…, object)`; без снятия обёртки ветка `source is MemberExpression` не сработала бы и `p.ItemN`-проекция потеряла бы нужный alias. Хелпер рекурсивно снимает только `Convert`/`ConvertChecked`, приватный static, без аллокаций; на `TableAlias`-путь не влияет (там ресивер не конвертируется).
- **План-ключ — участие подтверждено.** `SelectExpressionPlanEqualityComparer` включает `SelectExpression.Expression` в ключ; `ExpressionPlanEqualityComparer.VisitMethodCall` добавляет `node.Method` (generic-аргумент различает `T`), `VisitConstant` — строку имени; `CompareMethodCall`/`CompareConstant` дают точное равенство. Две разные колонки/типа дают разные планы, одинаковые — переиспользуют. **ℹ️** явного теста «разные имена → разные планы / одинаковые → кэш» нет (все новые тесты строят запрос один раз); структурно участие гарантировано, но регресс-тест на кэш приветствуется (ср. Находки 64/67).
- **In-memory — не молчит `default`.** `Column<T>` бросает `NotSupportedException` в теле (`SqlFunctions.cs:75-76`); тест `ColumnByName_ShouldThrowClearNotSupported` (`InMemoryTests.cs:852`) подтверждает на `Select`. Соответствует заявленному в `<remarks>`.
- **`ColumnName`/rename-aware — согласовано с mapped-путём.** `TranslateColumn` ставит `visitor.ColumnName = columnName` (`NormSqlTranslator.cs:83`), `MakeColumn` использует его в `renameAware`-сравнении (`SqlSourceRenderer.cs:600-604`), поэтому `Region = Column(…, "RegionID")` рендерит `RegionID as Region`; в param-режиме установка пропущена, но `MakeColumn` там возвращается раньше (`:593`) — списки параметров не рассинхронизируются (класс Находки 64 не воспроизводится).
- **Дублирование с `MemberTranslator` — пре-существующее, не усилено.** `MemberTranslator.cs:130-147` содержит ту же пару «`TypeExpressionVisitor` + `AliasResolver.GetAliasFromParam`»; срез вынес одну копию в `AppendColumnAlias` и переиспользовал её, новых копий не добавил.
- **Матрица тестов диалект-агностичного кода неполна.** SQL-gen покрыт CH/PG/SQLite; SQL Server (`[x]`) и MySQL/MariaDB (`` `x` ``) не покрыты, хотя todo-матрица заявляет их поддержку. Оба идут через общий `AppendIdentifier`/`QuoteIdentifier`, риск низкий (ℹ️, не блокер).
- **`docs/specs/roadmap/todo_clickhouse_columns_by_name.md` — LF-only.** Единственный изменённый файл с LF (остальные 9 `.cs` — CRLF); AGENTS.md требует CRLF. Нормализовать при коммите.

**Проверка (22.09.2026).** Build Release — **0/0**; в диффе `#pragma`/`SuppressMessage`/`NoWarn`/`Skip=`/`Task.Delay`/пустых `catch` — **0**; соотношение подавлений **11/11** (0 неоправданных); `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). Сфокусированные тесты этого прохода: `InMemoryTests` **81/81**, CH `ColumnByName` **3/3**, postgres `ColumnByName` **1/1**, sqlite `ColumnByName` **1/1**. Публичная сторона — `API-NAMING-REVIEW.md`, CHCB1–CHCB3.

## 🔎 Точечный аудит 22.09.2026 — DML `INSERT` (фаза 1), uncommitted working tree; 🟡 — 2, ℹ️ — 7

**Область.** Новая подсистема DML: `Query/Mutations/MutationCommand.cs` (`SqlStatementType`, `MutationCommand`, `InsertColumn`, `InsertValue`, `InsertCommand`; все `internal`), `DataContext/SqlMutationBuilder.cs` (`internal static`, `MakeInsert`), `DataContext/Roles/IMutationExecutor.cs` (новая `internal`-роль, явно реализована `DataContext`), `Builders/InsertBuilder.cs` (единственный новый **публичный** тип), `DataContextExtensions.InsertInto<TEntity>`, новые `QueryExecutor.ExecuteNonQuery`/`ExecuteNonQueryAsync`/`ExecuteScalar`/`ExecuteScalarAsync(string, IReadOnlyList<Parameter>)` + `CreateMutationCommand`, явные `IMutationExecutor.*` в `DataContext`, флаги/хуки в `ISqlDialect`/`SqlDialectBase` и `Postgres`/`Sqlite`/`SqlServer`/`MySql`-диалектах, `IPropertyMetadata.IsKey`/`IsIdentity`/`IsComputed` (DIM) + `PropertyMetadata`, `EntityMetadataBuilder` (атрибуты `[Key]`/`[DatabaseGenerated]` + конвенция `Id`/`<TypeName>Id`), `EntityPropertyBuilder<T>.Key`/`.Identity`/`.Computed`. Тесты: core +6 (`tests/nextorm.core.tests/InsertMetadataTests.cs`), SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql,clickhouse}.tests/InsertSqlGenerationTests.cs`), интеграция `tests/nextorm.integration.tests/CommonTestSuite.Insert.cs` (+4) и `ClickHouseIntegrationTests.cs` (+2).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в диффе новых — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** (обе baseline `Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан выполнен вручную. `.editorconfig` — 7 `dotnet_diagnostic.*.severity`, все `silent` (S125/S108/S3060/S1104/S3604/S2292 + CA2254), 6 `S*` инертны без `SonarAnalyzer` (пре-существующее, «Примечания»). **Уточнение:** в текущем дереве `NoWarn` отсутствует во всех 7 библиотечных `.csproj` (см. `API-NAMING-REVIEW.md`, IDML-раздел), т.е. `CS1591` не подавлен. Все новые `.cs` (4 production + `CommonTestSuite.Insert.cs` и SQL-gen/core-тесты) — CRLF; `docs/specs/roadmap/todo_insert.md` — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `QueryExecutor.ExecuteNonQuery*`/`ExecuteScalar*` оборачивают `DbCommand` в `using var` (`:112,124,135,148`); новых disposable-полей нет (`InsertBuilder`/`SqlMutationBuilder`/`MutationCommand` ресурсов не держат); `StringBuilderPool` возвращается в `finally` (`SqlMutationBuilder.cs:87-90`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. `Values(IEnumerable)` один раз материализует (`as IReadOnlyList ?? ToList()`, `InsertBuilder.cs:115`) и читает ячейки рефлексией (ℹ️ B). `ISqlDialect.MakeOutput`/`SqlDialectBase.MakeOutput` — единственная LINQ-цепочка (`Select`+`Join`), один раз на identity-insert (холодный путь). |
| 4. God-классы | ✅ `InsertBuilder<TEntity>` **279**, `MutationCommand.cs` **128**, `SqlMutationBuilder` **116**, `IMutationExecutor` **39** — все <500; декомпозиция «команда / рендер / исполнение» соблюдена. |
| 5. Хэш-ключи / план-кэш | ✅ Мутационный путь собственного план-кэша/хэша не имеет (`Prepare()` в фазе 1 не реализован — отклонение ниже), поэтому категория «хэш-ключи кэша» не затронута; общий `DataContextCache.Metadata` (`ConcurrentDictionary<Type,…>`) используется как в `From<T>`, ключ не менялся. |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет; новые `throw` — `InvalidOperationException`/`NotSupportedException`/`ArgumentException`/`BuildSqlCommandException` с понятными сообщениями (`null`/`DBNull`-кейс — ℹ️ F). |
| 7. NRT / возвратный тип | ✅ `TKey InsertWithIdentity<TKey>` — non-nullable generic с `default!` для `null`/`DBNull`; `string?`/`object?`-аннотации корректны, сборка `0/0`. |

### ✅ Находка 74 — `InsertWithIdentity<TKey>` не проверяет, что селектор — identity-колонка; на MySQL/MariaDB селектор молча игнорируется (ЗАКРЫТА 22.09.2026 в фазе 2, была P2)

**Закрытие (22.09.2026).** В текущем дереве `InsertWithIdentity`/`InsertWithIdentityAsync` идут через `ResolveIdentityProperty` (`InsertBuilder.cs:258-268`), который требует `property.IsIdentity` и бросает `InvalidOperationException` с подсказкой про `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]`/`.Identity()`. Добавлен негативный тест `InsertMetadataTests.InsertWithIdentity_OnNonIdentityColumn_ShouldThrow` (`tests/nextorm.core.tests/InsertMetadataTests.cs:140-149`). На MySQL/MariaDB неверный селектор больше не игнорируется.

**Место:** `src/nextorm.core/Builders/InsertBuilder.cs:164-169,180-186` (`ResolveProperty` без проверки `IsIdentity`); `src/nextorm.core/DataContext/DataContext.cs:240-274` (ветка `SupportsLastInsertId`); `src/nextorm.mysql/MySqlDialect.cs` (нет `SupportsReturning`/`SupportsOutput`).

**Что не так.** `InsertWithIdentity(keySelector)` принимает **любое** mapped-свойство: вызывается только `ResolveProperty`, флаг `IPropertyMetadata.IsIdentity` не читается. На PostgreSQL/SQLite/SQL Server селектор хотя бы попадает в `RETURNING`/`OUTPUT`, поэтому возвращается значение выбранной колонки. На MySQL/MariaDB `SupportsReturning`/`SupportsOutput` — `false`, `MakeInsert` identity-ветку не рендерит, а `DataContext.ExecuteIdentity` возвращает `LAST_INSERT_ID()` **независимо от селектора** (`DataContext.cs:251-252`). Итог: `ctx.InsertInto<T>().Value(x => x.Name, "a").InsertWithIdentity(x => x.Name)` на MySQL/MariaDB отдаёт не `"a"`, а auto-increment id (через `ConvertIdentity<string>` — его строковое представление), т.е. синтаксически валидный, но неверный результат без ошибки. Это расходится и с критерием приёмки RFC (возврат **сгенерированного** ключа), и с именем метода.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** в `InsertWithIdentity`/`InsertWithIdentityAsync` требовать `property.IsIdentity` и бросать `ArgumentException`/`BuildSqlCommandException` с подсказкой «mark the column with [DatabaseGenerated(DatabaseGeneratedOption.Identity)] or `.Identity()`»; при желании — отклонять вызов на диалекте без RETURNING/OUTPUT/LAST_INSERT_ID до исполнения. Добавить негативный тест (`InsertWithIdentity(x => x.Name)` → исключение) в core/SQL-gen.

**Проверка:** `InsertBuilder` и `DataContext` прочитаны; `MySqlDialect` (нет RETURNING/OUTPUT, `SupportsLastInsertId=true`) и SQL-gen тесты (identity-ветка негативным кейсом не покрыта) подтверждают. Не исполнялось (правило аудита: код не правится). Build `0/0`.

### ✅ Находка 75 — мёртвое свойство `InsertCommand.Metadata` (write-only) (ЗАКРЫТА 22.09.2026 в фазе 2, была P2)

**Закрытие (22.09.2026).** `InsertCommand` больше не несёт `Metadata`: ctor принимает `entityType`/`tableName`/`isTableNameAuto`/`columns`/`rowCount`/`identityColumn`/`returningColumns` (`MutationCommand.cs:95-133`), `TableName`/`IsTableNameAuto`/`Columns`/`RowCount`/`IdentityColumn`/`ReturningColumns` — единственные члены. `BuildCommand` (`InsertBuilder.cs:300-310`) собирает команду без метаданных.

**Место:** `src/nextorm.core/Query/Mutations/MutationCommand.cs:105-117` (`InsertCommand.Metadata`), `:108` — единственная запись.

**Что не так.** `Metadata` присваивается в конструкторе и больше нигде не читается. `roslyn refs NextORM.Core.InsertCommand.Metadata` → **1** ссылка (запись в ctor `:108`); для сравнения `TableName` — **4**, `RowCount` — **2**. Для рендера/исполнения нужны только `TableName`/`IsTableNameAuto`/`Columns`/`RowCount`/`IdentityColumn` (+`EntityType`), а маппинг колонок уже несёт `InsertColumn.Property`; `IEntityMetadata` в команде избыточен. Класс Находки 10 (мёртвый `LimitByPlanHash`) — «мёртвый член, попавший в новую ось».

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** удалить `InsertCommand.Metadata` и соответствующий параметр ctor, либо начать использовать его (например, для валидации identity из Находки 74). `InsertBuilder.BuildCommand` (`:245`) — единственный call-site, правка тривиальна; поведение не меняется.

**Проверка:** roslyn `refs` (`Metadata` = 1), чтение `SqlMutationBuilder` (Metadata не используется), build `0/0`.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `StatementType`/`SqlStatementType` — тоже write-only, но это каркас RFC.** `MutationCommand.StatementType` — 1 ссылка (запись в ctor), члены `Select`/`Update`/`Delete`/`Merge` не используются; в отличие от `Metadata` это сознательный задел под update/delete/merge фазы 2 (`todo_insert.md:85-97`). Оставить до фазы 2.
- **B. `Values(IEnumerable<TEntity>)` читает ячейки рефлексией.** `property.PropertyInfo.GetValue(list[i])` в двойном цикле строк×колонок (`InsertBuilder.cs:119-129`) даёт бокс value-type и reflection-диспетчер на каждую ячейку. Для I/O-bound вставки приемлемо; если батч-путь станет горячим — кандидат для `nextorm-db-perf-analyst` (скомпилированный аксессор).
- **C. `GetOrAddColumn`/`FindProperty` — линейный поиск по колонкам** (O(n²) на N `Value`-вызовов, `InsertBuilder.cs:212-234`), холодный путь построения команды; не hot path.
- **D. Новые `Supports*`/`Make*` добавлены прямо в `ISqlDialect`/`SqlDialectBase`** — растёт уже отмеченный фат-интерфейс (F12 в `solid-review.md`); «Фаза 3» уводила диалектные возможности в capability-объекты (`ILockRenderer` и др.). RFC предписывал именно такой вид (`todo_insert.md:117-129`), поэтому это консистентность/долг, а не дефект.
- **E. `DataContext.ExecuteIdentity` (sync/async) дублирует рендер+исполнение** для RETURNING/OUTPUT и LAST_INSERT_ID (4 почти одинаковых блока, `DataContext.cs:240-274`); общий хелпер убрал бы дублирование, объём мал, риск низкий.
- **F. `ConvertIdentity<TKey>` отображает `null`/`DBNull` в `default!`** (`InsertBuilder.cs:262-272`) — молча, без ошибки; для identity-колонки NULL — аномалия, но не блокер.
- **G. EOL чист:** новых LF-only файлов нет (все новые `.cs` — CRLF), в отличие от недавних Находок 20/22/29/51.

**Проверка (22.09.2026).** Build Release — **0/0**; `rg` подавлений `src/` — **6+5=11** (0 неоправданных), новых в диффе **0**; `rg 'Skip='` — 0, `Task.Delay` — 2 baseline `(0)`; `rg -c NoWarn` по 7 библиотечным `.csproj` — **0**; `roslyn refs` — `InsertCommand.Metadata` **1** (dead), `MutationCommand.StatementType` **1** (scaffolding), control `InsertCommand.TableName` **4** / `RowCount` **2**; `file` новых файлов — CRLF. Публичная сторона — `API-NAMING-REVIEW.md`, IDML1–IDML5.

## 🔎 Точечный аудит 22.09.2026 — DML `INSERT` (фаза 2): `RETURNING`/`OUTPUT`-материализация, uncommitted working tree; 🟡 — 2, ℹ️ — 8

**Область.** Фаза 2 поверх некоммитнутой фазы 1: новый `Builders/InsertReturningBuilder.cs` (`public sealed InsertReturningBuilder<TEntity,TResult>`, internal `ParseProjection`), `InsertBuilder.Returning()`/`Returning<TResult>(projection)` + internal `DataContext`/`RowCount`/`BuildReturningCommand`, `InsertCommand.ReturningColumns`, `SqlMutationBuilder.RenderReturningColumns` (OUTPUT — между списком колонок и `VALUES`, RETURNING — после), `IMutationExecutor.ExecuteReturning<TResult>` (+async, internal), явные реализации + `EnsureReturningSupported`/`EnsureReturningSupportedIfNeeded` в `DataContext`, `RowMapperFactory.GetOrBuild<TResult>(sql, providerType, selectList, oneColumn, mapColumn)` (+рефактор `Build`/`BuildKey`/`BuildSignature`), `QueryExecutor.ExecuteReader<TResult>`/`ExecuteReaderAsync<TResult>`. Тесты: `InsertMetadataTests` (+3), 5× `InsertSqlGenerationTests` (returning-кейсы), integration `CommonTestSuite.Insert.cs` (+5, из них `Assert.SkipUnless`) + `ITestProvider.SupportsInsertReturning`.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, новых в диффе — **0** (единственный `SuppressMessage` в `QueryExecutor.cs:71` — контекстная строка, не добавлена). Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`tests/nextorm.core.tests/InMemoryTests.cs`, `0`-yield, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. 4 новых `Assert.SkipUnless` (`CommonTestSuite.Insert.cs:97,116,134,155`) — рантайм-гейт по `Provider.SupportsInsertReturning`, **не** отключённый тест (SW001 не применим). `slopwatch` локально не установлен; скан выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent`, не менялось. Сфокусированные тесты: core `InsertMetadataTests` **9/9**; SQL-gen `InsertSqlGenerationTests` — sqlite **11/11**, postgres **9/9**, sqlserver **7/7**, mysql **5/5**, clickhouse **5/5**. Публичная сторона — `API-NAMING-REVIEW.md`, IDML6–IDML8.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `QueryExecutor.ExecuteReader` (`:162-173`) и `ExecuteReaderAsync` (`:182-193`) оборачивают и `DbCommand`, и `DbDataReader` в `using var`; порядок dispose корректный (reader → cmd). Новых disposable-полей нет. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11**. |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `RowMapperFactory.BuildSignature` — числовой цикл; `RowMaterializerBuilder` строит expression-tree один раз и кэширует. |
| 4. God-классы/методы | ✅ `InsertReturningBuilder` **188** строк (<500); самый длинный метод `ParseProjection` **44** строки (ℹ️ H). `RowMapperFactory` **191**, `SqlMutationBuilder` **136**. |
| 5. Хэш-ключи / план-кэш | ✅ Новый ключ `MapperCacheKey(providerType, typeof(TResult), sql, BuildSignature(selectList), oneColumn)` — коллизии с query-overload'ом нет (ℹ️ A/B); общий `MapperCache` теперь наполняется и мутациями (ℹ️ C). |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет; новые `throw` — `InvalidOperationException`/`NotSupportedException` с понятными сообщениями (кроме Находок 76/77). |
| 7. NRT / возвратный тип | ✅ `IReadOnlyList<TResult>`/`Task<...>`-аннотации корректны, сборка `0/0`; `Nullable` выводится из `SelectExpression(Type)` (`SelectExpression.cs:42-58`), `string?`/`int?`-колонки читаются через `IsDBNull`-гард. |

> **Примечание (22.09.2026).** Терминалы `InsertReturningBuilder` переименованы по ревью владельца: `InsertReturning`/`InsertReturningAsync` → `Single`/`SingleAsync`, `InsertReturningMany`/`InsertReturningManyAsync` → `ToList`/`ToListAsync` (убрано «масло масляное» `Returning().InsertReturning()`, `To*` — по конвенции `EntityBuilderExtensions`). В тексте находок ниже сохранены исходные имена на момент аудита. См. `API-NAMING-REVIEW.md`.

### 🟡 Находка 76 — `Returning()` (entity-форма) на interface-сущности падает `QueryPreparationException` из `RowMaterializerBuilder` (ЗАКРЫТА, P2)

**Место:** `src/nextorm.core/Builders/InsertBuilder.cs:200-206` (`Returning()` → `TResult = TEntity`); `src/nextorm.core/Builders/InsertReturningBuilder.cs:39-44`; `src/nextorm.core/DataContext/RowMapperFactory.cs:120-148`; `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:28-31`.

**Что не так.** `ctx.InsertInto<IInsertEntity>().Value(...).Returning().InsertReturning()` создаёт `InsertReturningBuilder<IInsertEntity, IInsertEntity>`, и на исполнении `RowMapperFactory.Build<IInsertEntity>` уходит в `RowMaterializerBuilder.Build`, где `resultType.GetConstructors()` для интерфейса пуст → `FirstOrDefault()` = `null` → `QueryPreparationException: Cannot get ctor from IInsertEntity`. Интеграционные тесты используют `InsertInto<IInsertEntity>()` для `Value`/`InsertWithIdentity` (`CommonTestSuite.Insert.cs:16,78`), т.е. сценарий естественный; при этом entity-форма возвращает вводящее в заблуждение исключение (запись не выполняется — исключение бросается до `ExecuteReader`). Проекции (`x => new { ... }`, `x => x.Id`) работают, т.к. `TResult` — не интерфейс.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** отклонять entity-форму для interface `TEntity` уже в `Returning()`/`ParseProjection` (`NotSupportedException` с подсказкой «project to a concrete type or an anonymous type»), либо материализовать в конкретную реализацию; покрыть негативным тестом и задокументировать (IDML7).

**Исправлено (22.09.2026):** отклонение перенесено в исполнение, а не в `Returning()`/`ParseProjection` (иначе ломается SQL-генерация `ToSql()` на интерфейсной сущности, которую используют SQL-gen тесты). `DataContext.EnsureReturningMaterializable<TResult>(oneColumn)` бросает понятный `NotSupportedException` до рендера/исполнения; покрыто `Insert_ReturningWholeEntity_OnInterface_ShouldThrow` (интеграция, PG/SQLite/SQL Server) и `Returning_WholeEntity_OnInterface_ShouldParseForSqlGeneration` (core). Проверено: SQL-gen и интеграционные `*Returning*` — 0 failed.

**Проверка:** подтверждено reflection-пробой вне репозитория: `RowMaterializerBuilder.Build(typeof(IFace), 2 columns, oneColumn: false)` → `QueryPreparationException: Cannot get ctor from IProbeEntity`; `GetConstructors()` на интерфейсе — 0. Cross-check: тесты `Returning()`/`InsertReturningMany` (`CommonTestSuite.Insert.cs:102,139`) используют concrete `InsertEntity`.

### 🟡 Находка 77 — member-init в `Returning(projection)` игнорирует левую часть binding: `new Dto { Name = x.Id }` → `ArgumentNullException` (ЗАКРЫТА, P2)

**Место:** `src/nextorm.core/Builders/InsertReturningBuilder.cs:145-154` (ветка `MemberInitExpression`), `:163-178` (`BuildSelectList` кладёт `PropertyName` **источника**), `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:49-50` (`resultType.GetProperty(column.PropertyName!)`).

**Что не так.** Для `x => new Dto { Target = x.Source }` парсер берёт только `binding.Expression` (`x.Source`) и теряет `binding.Member` (`Target`); `PropertyName` становится именем **источника** (`Source`). Далее `RowMaterializerBuilder` ищет свойство `Dto.Source`; если его нет — `GetProperty("Source")` = `null` → `Expression.Bind(null, ...)` → `ArgumentNullException: Value cannot be null. (Parameter 'member')`. Работает только при совпадении имён цели и источника. XML-summary `Returning<TResult>` (`InsertBuilder.cs:208-217`) обещает «member-init» без оговорки — over-claim.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** нести цель binding'а в selectList (например, `PropertyName = binding.Member.Name` или отдельное поле целевого имени) и учитывать её в materializer'е, либо явно отклонять переименованные binding'и `NotSupportedException`; поправить `<summary>` (IDML8).

**Проверка:** reflection-пробой подтверждён `ArgumentNullException` для `Dto` без свойства-источника; при совпадающих именах (`new Dto { Name = x.Name }`) путь работает (свойство находится по имени). `MemberAssignment.Member` в коде не читается (`rg` по `binding.Member` в `InsertReturningBuilder.cs` — 0).

**Исправлено (22.09.2026):** `ParseProjection` теперь несёт цель binding'а в `BuildSelectList` (`PropertyName = target.Name`, `PropertyInfo = target`), поэтому `new Dto { Renamed = x.Id }` материализуется в `Dto.Renamed` из колонки `id`; покрыто SQL-gen `ReturningMemberInit_ShouldReturnSourceColumn` (postgres).

### ℹ️ Наблюдения (фикс не требуется)

- **A. Коллизии кэш-ключа между overload'ами `GetOrBuild` нет.** Query-ключ и mutation-ключ совпали бы лишь при одинаковом `Sql`; query-текст всегда начинается с `select`/`with`, mutation-текст — с `insert into` (`SqlMutationBuilder.cs:36`), поэтому пространства текстов не пересекаются. Внутри mutation-ключа совпадение требует одинаковых `ProviderType`+`TResult`+`Sql`+`BuildSignature`+`OneColumn`, а `Sql` уже содержит таблицу и список возвращаемых колонок — в этом случае мапперы эквивалентны. Коллизии нет.
- **B. Mutation-ключ не включает `EntityType`** (query-`BuildKey` добавляет `queryCommand.EntityType?.GetHashCode()`, `RowMapperFactory.cs:159-168`). Безвредно по причине A; асимметрия отмечена на будущее (UPDATE/DELETE `... RETURNING`).
- **C. Общий `MapperCache` наполняется мутациями.** Каждый размер батча/набор возвращаемых колонок даёт уникальный `Sql` и свою compiled-запись; `MapperCache.MaxEntries = 4096`, при заполнении `Add` молча не кэширует (`MapperCache.cs:36-40`) — корректность не страдает, но новые SELECT-шейпы могут перестать кэшироваться. Кандидат для `nextorm-db-perf-analyst`.
- **D. `SupportsReturning && SupportsOutput` одновременно** → `MakeInsert` вставит и `OUTPUT` (до `VALUES`), и `RETURNING` (после) — невалидный SQL. Сейчас ни один диалект так не делает; добавить `Debug.Assert`/гард.
- **E. `InsertReturningAsync` при `RowCount > 1`** бросает `EnsureSingleRow` внутри `async`-метода (`InsertReturningBuilder.cs:53-58`) → faulted `Task` (sync-версия бросает синхронно); сообщение советует только `InsertReturningMany`, не `...ManyAsync`.
- **F. Порядок батча.** SQLite `RETURNING` не гарантирует порядок вставки; XML-доки аккуратно пишут «in result-set order», но пользовательской оговорки нет — док-находка IDML6; интеграционный тест `Insert_ReturningBatch_ShouldReturnEveryRow` использует `BeEquivalentTo` (порядко-независимо, `CommonTestSuite.Insert.cs:148`).
- **G. SQL Server `OUTPUT` без `INTO`** запрещён, если на целевой таблице есть enabled-триггеры (error 334) — ограничение не задокументировано, кандидат в `limitations.md`.
- **H. `ParseProjection` — 44 строки**, чуть выше 30-строчной эвристики, но это связный парсер из 4 веток; god-классов нет.
- **I. Мёртвого кода нет:** `InsertCommand.Metadata` (Находка 75) удалён; `EnsureReturningSupportedIfNeeded` читается в `Render` (`DataContext.cs:227`), `EnsureReturningSupported` — в `ExecuteReturning` (`:283,291`), `ParseProjection` — в `Returning` (`InsertBuilder.cs:204,221`).

**Закрытие ранее открытых.** Находка 74 — ✅ закрыта (`ResolveIdentityProperty` + негативный тест); Находка 75 — ✅ закрыта (`InsertCommand.Metadata` удалён).

**Проверка (22.09.2026).** Build Release — **0/0**; подавления `src/` **6+5=11** (0 неоправданных), новых **0**; `rg 'Skip='` — 0, `Task.Delay` — 2 baseline `(0)`; `Assert.SkipUnless` — 4 (легитимный capability-гейт); `rg -c NoWarn` — 0; `find PublicAPI*.txt` — **0** (Шаг 5 открыт); reflection-пробы: interface-entity → `QueryPreparationException`, member-init renamed → `ArgumentNullException`. Тесты (Release, `--no-build`): core `InsertMetadataTests` **9/9**, SQL-gen sqlite **11/11**, postgres **9/9**, sqlserver **7/7**, mysql **5/5**, clickhouse **5/5**. `file` новых `.cs` — CRLF. Интеграционные прогоны контейнеров (PG/SQL Server/SQLite `RETURNING`) в этом проходе **не выполнялись** — требуют `DOCKER_HOST` (см. скилл `running-integration-tests`).

## 🔎 Точечный аудит 22.09.2026 — коррелированные подзапросы в in-memory провайдере, uncommitted working tree; 🟡 — 4, ℹ️ — 5

**Область.** Новые `DataContext/InMemoryCorrelatedPlan.cs` (`InMemoryCorrelatedPlan`, `InMemoryCorrelatedEvaluator`, `OuterReferenceToParameterVisitor`), `DataContext/InMemoryCorrelatedSubqueryRewriter.cs`; правки `InMemoryDataContext` (`_correlatedPlans` + `GetCorrelatedPlan`), `InMemoryRowMaterializer` (rewriter для one-/multi-column + `EnsureType`), `InMemoryConditionFactory` (context-параметр + rewrite `PreparedCondition`), `InMemoryOrdering` (context-параметр + rewrite сортировки; снят прямой каст `Expression<Func<TEntity,object>>`), `InMemoryQueryBuilder` (снят blanket `OuterReferences`-guard, добавлен grouped-guard), `Query/QueryCommand.Clone.cs` (`CloneForCorrelatedEvaluation`), `tests/nextorm.core.tests/CorrelatedQueryInMemoryTests.cs` (9 позитивных тестов вместо `NotSupportedException`). Публичной поверхности не добавляет (все типы/члены `internal`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, новых в диффе — **0**; в новых файлах `Skip=`/`Task.Delay`/`Thread.Sleep`/`NoWarn`/пустых `catch` — **0**. `slopwatch` локально не установлен; скан выполнен вручную. Сфокусированные тесты: `CorrelatedQueryInMemoryTests` **9/9** (Release). Публичная/доковая сторона — `API-NAMING-REVIEW.md`, IMC1–IMC2.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `InMemoryCorrelatedPlan` не держит disposable-полей; перечислители создаются/освобождаются в `try/finally` через `(enumerator as IDisposable)?.Dispose()` (`:34-42,48-68,74-88`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `Any`/`Scalar`/`Contains` — явные циклы. Но план исполняется заново на каждую строку (по построению) — аллокации, ℹ️ A. |
| 4. God-классы | ✅ `InMemoryCorrelatedPlan` **306**, `InMemoryCorrelatedSubqueryRewriter` **314** — <500; rewriter совмещает разбор терминала, унификацию типов и связывание внешних ссылок (ℹ️ C). |
| 5. Хэш-ключи / план-кэш | 🟡 `_correlatedPlans` не чистится `PurgeQueryCache` (Находка 78); `CloneForCorrelatedEvaluation` оставляет устаревший `ReferencedQueriesPlanHash` (Находка 81). Сам `_correlatedPlans` использует identity-`QueryCommand` (`Equals`/`GetHashCode` не переопределены) — корректно. |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет. Новые `NotSupportedException` (grouped/HAVING/projection-outer-ref/ORDER-BY-outer-ref/depth>1/async/mixed-placeholder) и `InvalidOperationException` скалярных терминалов — осознанные гейты; `Invoke`-хелпер разворачивает `TargetInvocationException` через `ExceptionDispatchInfo` (`:201-205`). |
| 7. NRT / возвратный тип | ✅ Сборка `0/0`; есть `!`-подавления в visitor'ах (`Visit(expression)!`, `base.Visit(node)!`, `member.Expression!`) — ℹ️ D. |

### 🟡 Находка 78 — `_correlatedPlans` не очищается `PurgeQueryCache()`, растёт без границы

**Место:** `src/nextorm.core/DataContext/InMemoryDataContext.cs:48` (поле), `:122-131` (`GetCorrelatedPlan`), `:511` (`PurgeQueryCache`).

**Что не так.** `PurgeQueryCache()` маршрутизируется в `QueryCache(_cmdIdx.Clear)` (`:59`), т.е. чистит `_cmdIdx`, но `_correlatedPlans` (`Dictionary<QueryCommand, InMemoryCorrelatedPlan>`) не очищается никогда. Ключ — конкретный экземпляр inner-`QueryCommand` (identity: `Equals`/`GetHashCode` не переопределены), а команды пересоздаются при каждом построении запроса; план держит скомпилированные делегаты (`Enumerate`, `Func<object?[],IEnumerable>`). Долгоживущий `InMemoryDataContext`, исполняющий много разных коррелированных запросов, накапливает планы без эвикции — тот же класс ресурсов, что и план-кэш, который `PurgeQueryCache` обязан очищать.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** добавить `_correlatedPlans.Clear()` в purge-путь (например `new QueryCache(() => { _cmdIdx.Clear(); _correlatedPlans.Clear(); })` или отдельный callback) и/или ограничить размер. Тест: запрос → `PurgeQueryCache` → повторное исполнение не падает и план пересобирается.

**Проверка:** `PurgeQueryCache`/`QueryCache` прочитаны; `_correlatedPlans` в purge-пути отсутствует.

**Пост-фикс (22.09.2026): ✅ устранено.** `PurgeQueryCache` вызывает `_correlatedPlans.Clear()` вместе с `_queryCache.PurgeQueryCache()` (`InMemoryDataContext.cs:511-516`). Регрессия — общий прогон core-тестов 218/218.

### 🟡 Находка 79 — rewriter выполняется до обращения к кэшу на каждом исполнении ORDER BY (лишний обход дерева вместо кэш-хита)

**Место:** `src/nextorm.core/DataContext/InMemoryOrdering.cs:37-47`; аналогично `InMemoryConditionFactory.cs:29-31`.

**Что не так.** `GetSortingSelector` сначала строит `InMemoryCorrelatedSubqueryRewriter` и проигрывает всё дерево `sorting.PreparedExpression`, и только потом вычисляет `ExpressionKey` и проверяет `sortingSelectorCache`. `ApplyOrdering` вызывается при каждом создании энумератора/`OrderAsyncEnumerable` (в `InMemoryQueryBuilder.cs:306-309` — на каждое перечисление, включая повторное по уже закэшированному `data`). Для не-коррелированных упорядоченных запросов (подавляющее большинство) это лишний полный обход дерева + аллокация ключа на каждое исполнение, которого до фичи не было (раньше был прямой каст `Expression<Func<TEntity,object>>`). `InMemoryConditionFactory` переписывает `condition` до `TryGetValue` аналогично, но вызывается из `CreateCompiledQuery` (кэш уровня команды), поэтому эффект меньше.

**Стало (рекомендация):** early-return при `queryCommand.OuterReferences is null or { Count: 0 }` (`return expression;`) — переписывать нужно только коррелированный запрос; и/или кэшировать результат rewrite в `sortingSelectorCache` под ключом исходного выражения. Маршрут — `nextorm-design-engineer`; замер — `nextorm-inmemory-perf-analyst`.

**Проверка:** `ApplyOrdering` вызывается из `CreateEnumerator` (`InMemoryQueryBuilder.cs:308`) на каждое исполнение; rewriter стоит до `sortingSelectorCache.TryGetValue` (`InMemoryOrdering.cs:39,45`).

**Пост-фикс (22.09.2026): ✅ устранено.** Добавлен ранний выход `InMemoryCorrelatedSubqueryRewriter.IsNeeded` (`OuterReferences is null or {Count:0}` **и** `ReferencedQueries is null or {Count:0}` → без обхода дерева), применённый в `InMemoryOrdering.GetSortingSelector`, `InMemoryConditionFactory.GetConditionPredicates` и `InMemoryRowMaterializer.GetMap`; не-коррелированный путь вернулся к прямому касту/`MapColumn`.

### 🟡 Находка 80 — `ValueEquals` сравнивает числа через `Convert.ToDouble`, теряя точность `long`/`ulong`/`decimal`

**Место:** `src/nextorm.core/DataContext/InMemoryCorrelatedPlan.cs:94-114` (`ValueEquals`/`IsNumeric`), используется в `Contains` (`:72-89`).

**Что не так.** Разнотипные числа приводятся к `double` (`:102-103`). `double` точно представляет целые лишь до 2^53, а `decimal` теряет масштаб, поэтому коррелированный `IN`/`Contains` может дать ложное совпадение: например, для `long` id `9007199254740993` и `9007199254740992` сравниваются как равные. SQL-провайдеры сравнивают в типе колонки и такой ошибки не делают — расхождение in-memory с SQL.

**Стало (рекомендация):** для целочисленных типов приводить к `long`/`ulong`, `decimal` не конвертировать в `double`; в общем случае — `Convert.ChangeType` к типу-победителю из `NumericRank`/`CommonType`. Тест: `IN`-подзапрос по `long` со значениями > 2^53 не даёт ложных совпадений.

**Проверка:** `ValueEquals` прочитан, путь `Contains` = `Convert.ToDouble`.

**Пост-фикс (22.09.2026): ✅ устранено.** `ValueEquals` при отсутствии float/double-операндов сравнивает через `Convert.ToDecimal` (целые и `decimal` — точно), к `Convert.ToDouble` падает только если хотя бы один операнд `Single`/`Double` (`InMemoryCorrelatedPlan.cs`, `NumericEquals`/`IsFloating`).

### 🟡 Находка 81 — `CloneForCorrelatedEvaluation` чистит `_referencedQueries`/`_outerRefs`, но оставляет скопированный `ReferencedQueriesPlanHash`

**Место:** `src/nextorm.core/Query/QueryCommand.Clone.cs:163-171`; инвариант — `:35-40` (комментарий `CopyTo`) и `src/nextorm.core/Query/QueryPlanEqualityComparer.cs:426-433`.

**Что не так.** `Clone()` копирует `ReferencedQueriesPlanHash` (и остальные hash-поля) из оригинальной команды, после чего `CloneForCorrelatedEvaluation` обнуляет `_referencedQueries` и `_outerRefs`. Инвариант, явно зафиксированный в `CopyTo` («hash-поля должны соответствовать живому состоянию, иначе `QueryPlan.GetCacheVersion` Debug.Assert упадёт»), нарушается: у клона ненулевой `ReferencedQueriesPlanHash` при `ReferencedQueries == null`. Сейчас не проявляется только потому, что коррелированный путь идёт с `storeInCache:false` (`InMemoryCorrelatedPlan.cs:181`) и `GetCacheVersion()` для клона не вызывается, — но это скрытая мина: любой будущий кэш/`ExpressionKey`-хэш по клону получит несогласованный хэш.

**Стало (рекомендация):** вместе с обнулением ссылочных коллекций обнулять и производные хэши (`clone.ReferencedQueriesPlanHash = 0;`), либо не копировать их в этом сценарии, либо пересчитывать; проверка — Debug-сборка с `GetCacheVersion()` на клоне не падает по `Debug.Assert`.

**Проверка:** `CloneForCorrelatedEvaluation`/`CopyTo`/`QueryPlanEqualityComparer` прочитаны; `GetCacheVersion` для коррелированного клона не вызывается (`storeInCache:false`).

**Пост-фикс (22.09.2026): ✅ устранено.** `CloneForCorrelatedEvaluation` обнуляет `ReferencedQueriesPlanHash` вместе с `_referencedQueries`/`_outerRefs` (`QueryCommand.Clone.cs`).

### ℹ️ Наблюдения (фикс не требуется)

- **A. Per-row boxing внешних значений (кандидат для `nextorm-inmemory-perf-analyst`).** `BuildOuterValuesExpression` компилирует `Func<row, object[]>` и на каждую внешнюю строку пакует каждую внешнюю ссылку в `object[]` (`InMemoryCorrelatedSubqueryRewriter.cs:205-222`); `InMemoryCorrelatedPlan.Any`/`Scalar`/`Contains` затем пересоздают inner-энумератор. Для in-memory это ожидаемая цена корреляции; при горячем пути — цель для замеров/оптимизации (пул массивов, типизированный биндер).
- **B. Reflection-шов в evaluator'е.** `InMemoryCorrelatedEvaluator` резолвит `GetPreparedQueryCommand`/`GetEnumerable`/`BuildTyped` через `MethodInfo`/`MakeGenericMethod`/`Invoke` (`InMemoryCorrelatedPlan.cs:124-136,181-184`), т.к. работа идёт над не-generic `QueryCommand`. Срабатывает один раз на план (кэшируется `_correlatedPlans`), не на строку; `QueryCommand<TResult>.CreateSelf` переопределён (`QueryCommand.TResult.cs:458`), поэтому клон сохраняет корректный generic-тип. Хрупко к переименованию методов — закрепить комментарием-якорем.
- **C. `InMemoryCorrelatedSubqueryRewriter` совмещает разбор терминала, унификацию типов (`CommonType`/`NumericRank`), связывание внешних значений и константную свёртку** — 314 строк, под порогом god-класса; при росте (ANY/ALL, `HAVING`) стоит разнести terminal-разбор и type-unification.
- **D. `!`-подавления NRT.** `Rewrite(...) => Visit(expression)!`, `base.Visit(node)!`, `single.Body`, `_registry.OuterReferences!`, `member.Expression!` (`InMemoryCorrelatedSubqueryRewriter.cs:26,100,139,208,213,227`). Сборка `0/0`; это внутренние visitor'ы, но `member.Expression!` в `FindParameter` для статического члена вернул бы `null` и ушёл в `_ => null` (безопасно, но `!` вводит в заблуждение).
- **E. Константный фолдинг/дефолты через reflection.** `CompileValue` делает `Expression.Lambda(...).Compile().DynamicInvoke()` (`:169-170`), `DefaultOf` — `Activator.CreateInstance` (`InMemoryCorrelatedPlan.cs:91-92`), `GetReferencedQueryIndex` ищет член по имени `ReferencedQueries` без проверки объявляющего типа (`:268-306`). Всё на холодном пути (один раз на план / non-correlated `IN`); риски низкие.

**Проверка (22.09.2026).** Build Release — **0/0**; `CorrelatedQueryInMemoryTests` **9/9**; подавления `src/` **6+5=11** (0 неоправданных), новых **0**; в новых `.cs` `Skip=`/`Task.Delay`/`Thread.Sleep`/`NoWarn`/пустых `catch` — **0**; `file` новых/изменённых файлов — CRLF; `_correlatedPlans` в purge-пути отсутствует (Находка 78); `GetCacheVersion` на коррелированном клоне не вызывается (`storeInCache:false`). Публичная сторона — `API-NAMING-REVIEW.md`, IMC1–IMC2.

## 🔎 Точечный аудит 22.09.2026 — DML `INSERT` (фаза 3): перенос `InsertWithIdentity` → `ReturningIdentity`/`ReturningKey`, uncommitted working tree; 🔴 — 1 (P1-кандидат), 🟡 — 2, ℹ️ — 9

**Область.** Перекроенная за этот ход «returning»-поверхность: из `Builders/InsertBuilder.cs` **удалены** `InsertWithIdentity<TKey>(selector)`/`InsertWithIdentityAsync<TKey>(selector, ct)`; добавлены терминалы `Returning()`, `Returning<TResult>(projection)`, `ReturningIdentity<TKey>(selector)`, `ReturningIdentity<TKey>()`, `ReturningKey<TKey>()` — все возвращают `InsertReturningBuilder<TEntity,TResult>` и читаются через `Single`/`SingleAsync`/`ToList`/`ToListAsync`/`ToSql`. Новые DIM `ISqlDialect.SupportsIdentityFunction => SupportsLastInsertId` и `MakeIdentityFunction() => MakeLastInsertId()` (`ISqlDialect.cs:1016,1038`); `SqlDialectBase` — те же два как `virtual` (`:694-696`); переопределения: `SqlServerDialect` (`select scope_identity()`), `PostgresDialect` (`select lastval()`); MySQL/SQLite/MariaDB наследуют `last_insert_id()`/`last_insert_rowid()`. Новые члены `internal`-роли `IMutationExecutor`: `SupportsGeneratedColumns`, `RenderIdentityFunction`, `ExecuteIdentityFunction` (+async). Идентичность-функция дописывается в **тот же батч**, что и insert (`sql + "; " + MakeIdentityFunction()`, `DataContext.cs:240-256`), а `EnsureIdentityColumn` (`DataContext.cs:360-365`) отклоняет `LAST_INSERT_ID`-фолбэк для не-identity колонки. Тесты: `InsertMetadataTests` (+негативы), 5× `InsertSqlGenerationTests`, интеграционные `ReturningIdentity_*`/`ReturningKey_*`/`Insert_Returning*`.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных; новых в диффе — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`tests/nextorm.core.tests/InMemoryTests.cs:125,414`, `0`-yield); `Assert.SkipUnless` — **7** (`CommonTestSuite.Insert.cs:121,138,158,177,195,216,231`, рантайм-гейт `SupportsInsertReturning`, не SW001); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer`, пре-существующее). Сфокусированные тесты (Release, `--no-build`): core `InsertMetadataTests` **13/13**; SQL-gen `InsertSqlGenerationTests` — sqlite **14/14**, postgres **13/13**, sqlserver **10/10**, mysql **7/7**, clickhouse **7/7**.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ `QueryExecutor.ExecuteNonQuery*`/`ExecuteScalar*`/`ExecuteReader*` оборачивают `DbCommand` (и `DbDataReader`) в `using var` (`QueryExecutor.cs:102,113,124,137,153,172`); новых disposable-полей нет (`InsertBuilder`/`InsertReturningBuilder`/`SqlMutationBuilder` ресурсов не держат). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. `MakeOutput`/`MakeReturning`/`MakeIdentityFunction` — `Select`/`string.Join` один раз на команду (холодный путь); `RowMapperFactory` компилирует expression-tree один раз и кэширует; скалярный (`oneColumn`) путь читает одну колонку без LINQ. |
| 4. God-классы | ⚠️ `InsertBuilder` **400**, `InsertReturningBuilder` **267** (<500), `SqlMutationBuilder` **136**, `IMutationExecutor` **96**; но `DataContext` вырос **397 → 541** и перешёл порог 500 (ℹ️ F). Внутри `InsertReturningBuilder` — два режима (Находка 83). |
| 5. Хэш-ключи / план-кэш | ✅ Мутационный путь по-прежнему без собственного план-кэша. Ключ `MapperCacheKey(providerType, typeof(TResult), sql, BuildSignature(selectList), oneColumn)` (`RowMapperFactory.cs:99`) — `sql` мутации начинается с `insert into`, query-тексты — с `select`/`with`; коллизий нет (продолжение наблюдения A фазы 2, асимметрия `EntityType` — наблюдение B). |
| 6. События / исключения | ✅ Подписок нет, новых `catch` нет; новые `throw` — `InvalidOperationException`/`NotSupportedException` с понятными сообщениями. |
| 7. NRT / возвратный тип | ✅ Аннотации `TKey`/`TResult`/`IReadOnlyList<>`/`Task<>` корректны, сборка `0/0`; `ConvertIdentity<TResult>` → `default!` для `null`/`DBNull` (ℹ️ C). |

### 🔴 Находка 82 (P1-кандидат, корректность) — скалярная проекция/ключ с приведением типа падает `ArgumentException` в materializer'е

**Место:** `src/nextorm.core/Builders/InsertReturningBuilder.cs:241-257` (`BuildSelectList`: `new SelectExpression(column.PropertyInfo.PropertyType)` — CLR-тип **источника**); `src/nextorm.core/DataContext/RowMapperFactory.cs:130-137` (`oneColumn`-ветка: `Expression.Lambda<Func<IDataRecord,TResult>>(body, param)` без приведения `body` к `TResult`).

**Что не так.** Для одноколоночной проекции `BuildSelectList` кладёт в `SelectExpression` CLR-тип **mapped-свойства**, `RowMapperFactory.MapColumn` строит выражение этого же типа, а делегат имеет тип `TResult` — тип **проекции**. Когда они различаются приведением (nullable-lifting / widening / boxing), `mapColumn` возвращает, например, `int`, а `Expression.Lambda<Func<IDataRecord,int?>>` требует ровно `int?` и падает:
- подтверждено пробой вне репозитория: `Expression.Lambda<Func<int?>>(Expression.Constant(1))` → `ArgumentException: Expression of type 'System.Int32' cannot be used for return type 'System.Nullable'1[System.Int32]'`.
- задевает: `Returning(x => (long)x.IntCol)` / `Returning(x => (int?)x.IntCol)` (`.Single()`/`.ToList()` на PostgreSQL/SQLite/SQL Server), `ReturningIdentity<int?>(x => x.Id)`, `ReturningKey<long?>()` (`.ToList()`; `EnsureKeyType` (`InsertBuilder.cs:307-315`) сам нормализует nullable и **разрешает** такой вызов).
- `ReturningKey<long>().Single()` не задет — идёт `ExecuteIdentity`/`ConvertIdentity`, минуя row-mapper; query-путь не задет — там `SelectExpression` создаётся по типу цели (`QueryCommand.QueryPreparer.cs:257,286,323,374`). Тесты зелёные именно потому, что ни один кейс не проецирует скаляр с приведением (все SQL-gen — `x => x.Id`/`new { … }`).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** в `RowMapperFactory.Build` (ветка `oneColumn`) выровнять тело под тип делегата:
```csharp
var body = mapColumn(selectList[0], param);
if (body.Type != resultType)
    body = Expression.Convert(body, resultType);
lambda = Expression.Lambda<Func<IDataRecord, TResult>>(body, param);
```
`Expression.Convert` покрывает nullable-lifting, widening и boxing (работоспособность проверена пробой). Альтернатива — нести целевой тип проекции в `BuildSelectList`. Добавить позитивные тесты `Returning(x => (long)x.IntCol).Single()` и `ReturningKey<int?>().ToList()` на PostgreSQL/SQLite/SQL Server.

**Проверка:** пробы/чтение (`RowMapperFactory.cs:135-136`, `InsertReturningBuilder.cs:248`, `SelectExpression.cs:42-46`); query-путь — `QueryCommand.QueryPreparer.cs:286`. Build `0/0`.

### 🟡 Находка 83 (P2, дизайн/DRY) — `InsertReturningBuilder<TEntity,TResult>`: один тип, две работы; диспетчеризация режима продублирована

**Место:** `InsertReturningBuilder.cs:24-29` (поля обоих режимов), `:66-79` (`SingleAsync`), `:88-99` (`ToList`), `:109-120` (`ToListAsync`), `:127-141` (`ToSql`), `:143-154` (`SingleCore`).

**Что не так.** Тип держит два режима: (1) row-возврат — `_returningColumns`/`_selectList`/`_oneColumn`; (2) скалярный ключ — `_identityColumn`/`_identityFunction`. Один и тот же 2–3-веточный выбор повторён в `SingleCore`, `ToList`, `ToListAsync`, `ToSql`, а `Single`/`SingleAsync` почти дублируют друг друга (sync/async). **God-object по порогу >500 это не делает** (267 строк, 5 публичных членов, единая ответственность «терминал результата INSERT»; публичную форму менять не нужно — отдельный тип под ключи удвоил бы терминалы). Но это «один тип — две работы» плюс копипаста диспетчеризации; при `_identityFunction` поля row-режима пусты (`[]`/`false`, `InsertBuilder.cs:188-189`) и не должны читаться — хрупкий инвариант.

**Стало (рекомендация):** не разделять тип, а вынести выбор режима в один private-хелпер (например, `ExecuteCore`/`ResultKind`), которому делегируют `Single`/`SingleAsync`/`ToList`/`ToListAsync`/`ToSql`; sync и async — одна реализация. Риск низкий, поведение не меняется.

### 🟡 Находка 84 (P2, несоответствие) — `ToSql()` пропускает `EnsureIdentityColumn`, поэтому рендерит SQL, который исполнение затем отвергает

**Место:** `InsertReturningBuilder.cs:134-135`; `DataContext.cs:233-238` (`RenderIdentityFunction` вызывает только `EnsureIdentityFunctionSupported`), `DataContext.cs:278,298` (`ExecuteIdentity` вызывает `EnsureIdentityColumn`).

**Что не так.** На MySQL/MariaDB `ReturningKey<int>()` по **не-identity** ключу (например `ConventionalEntity.Id` — ключ по конвенции, без `[DatabaseGenerated(Identity)]`) в `.ToSql()` уходит в `RenderIdentityFunction(BuildIdentityCommand())` и **успешно** возвращает `insert …; select last_insert_id()`, тогда как `.Single()`/`.ToList()` бросают `NotSupportedException` из `EnsureIdentityColumn`. Диагностика показывает выполнимый SQL, который на деле будет отвергнут (guide 19 уже пишет «a non-identity key is rejected»). Исполнение при этом корректно — guard есть.

**Стало (рекомендация):** в `DataContext.RenderIdentityFunction` для команды с `IdentityColumn is not null` вызвать `EnsureIdentityColumn(command)` (для `_identityFunction`-команд `IdentityColumn` — `null`, проверку туда не ставить). Покрыть `ToSql()`-негативом на MySQL. Влияние — только на диагностический рендер.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `InsertReturningBuilder` — два режима, но публичная форма верная.** `ReturningIdentity<TKey>()` создаёт билдер с `_returningColumns=[]`/`_selectList=[]`/`_oneColumn=false` (`InsertBuilder.cs:188-189`); все 5 терминалов сначала проверяют `_identityFunction`, поэтому «пустой row-режим» не читается. Инвариант хрупкий, но корректен (см. Находку 83).
- **B. `SupportsGeneratedColumns` назван вводяще** (`IMutationExecutor.cs:20`, `DataContext.cs:231`): это «провайдер умеет возвращать сгенерированные колонки **в самом INSERT**» (`SupportsReturning || SupportsOutput`), а не «поддерживает generated-колонки» (их умеют все). Роль `internal` — на публичную поверхность не влияет; кандидат на `SupportsInlineReturning`.
- **C. `ReturningIdentity<TKey>()` без якоря типа.** `ReturningKey<TKey>()` валидирует `TKey` через `EnsureKeyType` (`InsertBuilder.cs:307-315`), а no-selector identity-форма отдаёт `TKey` прямо в `ConvertIdentity`/`Convert.ChangeType` (`InsertBuilder.cs:383-393`): неверный `TKey` (например `int` при `bigint`-identity) компилируется и падает `OverflowException`/`InvalidCastException` в рантайме. Осознанно (identity может быть не замаплена), но заслуживает строки в `<remarks>`.
- **D. `lastval()`/`LAST_INSERT_ID()` дают «тихое» неверное значение вне identity-сценария.** PostgreSQL `lastval()` ошибётся, если в сессии не вызывалась последовательность, а MySQL `LAST_INSERT_ID()` вернёт `0` для таблицы без auto-increment; `ReturningIdentity<TKey>()` не может это проверить (не называет колонку) — документационная оговорка.
- **E. Мёртвого кода нет.** `RenderIdentityFunction` читается в `ToSql` (`InsertReturningBuilder.cs:132,135`), `SupportsGeneratedColumns` — в `ToList`/`ToListAsync`/`ToSql` (`:95,116,134`), `ExecuteIdentityFunction` — в `Single*`/`ToList*`/`ToSql`, `EnsureIdentityColumn` — в `ExecuteIdentity` (`DataContext.cs:278,298`).
- **F. `DataContext.cs` 397 → 541 строк** — перешёл порог «god-класса» 500 из-за ~140 строк явных реализаций `IMutationExecutor` (`DataContext.cs:223-362`). Регион связный; при желании вынести в partial/роль-хелпер. Не дефект.
- **G. `ParseProjection` — ~53 строки** (`InsertReturningBuilder.cs:187-239`), выше эвристики 30, но это связный парсер 4 веток; god-классов нет.
- **H. `IPropertyMetadata.cs` без завершающего перевода строки** (`\ No newline at end of file` в диффе); CRLF в остальном соблюдён — EOL-гигиена.
- **I. Устаревший комментарий ссылается на удалённый API:** `tests/nextorm.clickhouse.tests/InsertSqlGenerationTests.cs:30` — `<c>InsertWithIdentity</c> is rejected`; публичные доки (`docs/guide/19-insert-statement.md`, `api-reference`, `providers/overview`, `linq2db-comparison`, EN+RU) уже переведены на `ReturningIdentity`/`ReturningKey`.

**Закрытие ранее открытых.** Находка 74 — **снята удалением API**: `InsertWithIdentity`/`InsertWithIdentityAsync` из текущего дерева удалены вместе с методом, проверка `IsIdentity` переехала в `ResolveIdentityProperty` (`InsertBuilder.cs:272-282`) и вызывается из `ReturningIdentity(selector)`; негативный тест переименован в `ReturningIdentity_OnNonIdentityColumn_ShouldThrow` (`InsertMetadataTests.cs:144-154`). Находки 75–77 остаются закрытыми. **К исправлению (маршрут — `nextorm-design-engineer`):** Находка 82 (P1-кандидат, обязательна), Находки 83–84 (P2, по желанию).

**Проверка (22.09.2026).** Build Release — **0/0**; `rg` подавлений `src/` — **6+5=11** (0 неоправданных), новых в диффе **0**; `rg 'Skip='` — 0, `Task.Delay` — 2 baseline `(0)`, `Assert.SkipUnless` — 7 (capability-гейт); `rg -c NoWarn` — 0; `find PublicAPI*.txt` — **0** (Шаг 5 открыт); пробами подтверждены `ArgumentException` (без фикса) и работоспособность `Expression.Convert` (фикс). Тесты (Release, `--no-build`): core `InsertMetadataTests` **13/13**, SQL-gen sqlite **14/14**, postgres **13/13**, sqlserver **10/10**, mysql **7/7**, clickhouse **7/7**. Публичная сторона — `API-NAMING-REVIEW.md`, IDML9–IDML10.

## 🔎 Точечный аудит 22.09.2026 — `KeywordCase`, index-хинты и row-значения (`ITupleRenderer`), uncommitted working tree; 🔴 — 1 (P1-кандидат), 🟡 — 4, ℹ️ — 10

**Область (только три фичи; DML `INSERT` в этом дереве — вне прохода).** (1) Регистр SQL-ключевых слов `KeywordCase`: новые `KeywordCase.cs`, `SqlKeywords.cs`, гейт `ResolvedKeywordCase`/ключ плана, `VisitorOptions`/`SqlBuildContext`/`QueryPlanner`, `DataContextBuilder.UseKeywordCase`/`UseUppercaseKeywords`, `EntityBuilder<TEntity>.WithKeywordCase`/`WithUppercaseKeywords`, `QueryCommand<TResult>.WithKeywordCase`, `ISqlDialect.Make{Page,Top,With,JoinKeyword,TableAlias,ColumnAlias}(... KeywordCase)` + `protected static SqlDialectBase.Kw`, миграция `SqlBuilder`/`SqlSourceRenderer`/трансляторов. (2) Index-хинты: `IndexHintKind`, `IIndexHintRenderer`, `ISqlDialect.IndexHints`, `EntityBuilder<TEntity>.WithIndex`/`WithoutIndex`, `MySql/Sqlite/SqlServer*IndexHintRenderer`, `SqlSourceRenderer.TableHintsWithIndex`. (3) Row-значения PostgreSQL: `ITupleRenderer`, `ISqlDialect.Tuple`, `SqlDialectBase.Tuple`, `PostgresTupleRenderer`/`ClickHouseTupleRenderer`, `TupleSqlTranslator`, `TypeFacts.IsValueTupleType`/`IsTupleLike`, `VisitNew`-ветка.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных; новых в трёх фичах — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`tests/nextorm.core.tests/InMemoryTests.cs`, `0`-yield, вне диффа фич); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer`; пре-существующее; не трогалось). CS1591 не подавлен (`rg -c NoWarn` по 7 `.csproj` — 0), `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true` → build `0/0` доказывает 100 % XML-doc нового публичного API. Сфокусированные SQL-gen тесты (Release, `--no-build`, `--filter SqlGenerationTests`): sqlite **222/222**, postgres **269/269**, sqlserver **214/214**, mysql **59/59**, clickhouse **195/195**.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новые `*TupleRenderer`/`*IndexHintRenderer` — `sealed`-синглтоны без полей/ресурсов; `SqlKeywords.UpperCache` — статический `ConcurrentDictionary` (ключи — константы-литералы, неограниченного роста нет; unmanaged-ресурсов/финализаторов нет, `Dispose` не нужен); `TableHintsWithIndex` — чистая аллокация массива. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. `EntityBuilder.WithIndex` — `.Where(...).ToArray()` на этапе построения (холодно); `TableHintsWithIndex` — ручной цикл вместо LINQ; маппинг/материализация не затронуты. |
| 4. God-классы | ⚠️ Пре-существующие крупные файлы: `ISqlDialect` **1066**, `EntityBuilder` **1965** (фича +~120), `SqlDialectBase` **708**, `SqlSourceRenderer` **659**, `SqlBuilder` **653**, `QueryCommand.TResult` **727**. Новые файлы малы (`TupleSqlTranslator` 134, `TypeFacts` 115, `SqlKeywords` 19). God-классов-новоделов нет; `EntityBuilder` < 500 не был и до фичи (ℹ️ F из Находки 82). |
| 5. Хэш-ключи / план-кэш | ✅ `QueryPlanEqualityComparer` включает `IndexHints` (`StringListsEqual`), `IndexHintKind` и `ResolvedKeywordCase` в `Equals`+`GetHashCode` — два контекста/команды с разным регистром не делят план. Побочное: `IndexHintKind` хэшируется всегда (ℹ️ H). |
| 6. События / исключения | ✅ Подписок нет, новых `catch` нет; новые `throw` — `NotSupportedException`/`ArgumentException`-класс с понятными сообщениями; единственная ловушка — nullable-возврат `IIndexHintRenderer.RenderIndexHint` + молчаливый skip (ℹ️ F). |
| 7. NRT / возвратный тип | ✅ `string?`-аннотации корректны (`RenderIndexHint`, `RenderElement`), `indexRenderer!` стоит после null-проверки; `Tuple`/`IndexHints` — nullable DIM; сборка `0/0`. |

### 🟡 Находка 85 (P2, охват/консистентность) — `KeywordCase.Upper` не поднимает два ключевых слова внутри заявленного охвата

**Место:** `src/nextorm.core/DataContext/SqlBuilder.cs:153`; `src/nextorm.sqlserver/SqlServerDialect.cs:108`.

**Что не так.** (1) `SqlBuilder.cs:153` для привязанного элемента ARRAY JOIN эмитит голый `" as "`: `expressionSql + " as " + ArrayJoinNames.ElementAlias`. `array join` — отложенная фаза 3, но сам `as` — **core-алиас**, и `docs/advanced/limitations.md:24` (EN+RU) прямо заявляет, что алиасы `as` поднимаются. В `Upper` выходит `... ARRAY JOIN ... as 'e'` (смешанный регистр). (2) `MakeTableHints` (`SqlServerDialect.cs:108`) → `$" with ({...})"` не проходит через `Kw`; при этом index-хинт-feature уже поднимает `index(` (`SqlServerIndexHintRenderer.RenderIndexHint`, `:582-585`) → в `Upper` SQL Server даёт `with (INDEX(idx_id))` — смешанный регистр внутри одного хинта. Прочие capability-клозы (`pivot`, `tablesample`, `limit by`, `for update/share`, `distinct on`, `for json/xml`, `array join`) — **задокументированное состояние фазы 3** (`docs/advanced/limitations.md:24`, `docs/advanced/limitations.md:177-179`), повторно не репортуются.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** `SqlBuilder.cs:153` → `expressionSql + Kw(" as ") + …`; протянуть `KeywordCase` в `ISqlDialect.MakeTableHints`/`SqlServerDialect.MakeTableHints` (source-breaking — уже в трекинге API-реестра, KC4). Тесты: `Upper` + ARRAY JOIN с element binding (ClickHouse), `Upper` + `WithTableHint("nolock")`/`WithIndex` (SQL Server) — assert отсутствия нижнерегистровых ` as `/` with `.

**Проверка:** чтение кода + существующие `Upper`-тесты покрывают только skeleton/алиасы/пагинацию (`tests/nextorm.sqlite.tests/SqlGenerationTests.cs:124-217`, `tests/nextorm.postgres.tests/...:37`, `tests/nextorm.sqlserver.tests/...:46`); ни один не проходит через ARRAY JOIN/table hints.

### 🟡 Находка 86 (P2, корректность) — `WithIndex`/`WithoutIndex` могут дать пустой список индексов → невалидный SQL на MySQL/SQL Server

**Место:** `src/nextorm.core/Builders/EntityBuilder.cs:1474-1481`; `src/nextorm.core/DataContext/SqlSourceRenderer.cs:232-238`; `src/nextorm.mysql/MySqlDialect.cs:399-404`; `src/nextorm.sqlserver/SqlServerDialect.cs:582-585`; `src/nextorm.sqlite/SqliteDialect.cs:252-261`.

**Что не так.** XML-док `WithIndex(params string[])` (`:1462`) обещает «never empty», но overload с `IndexHintKind` фильтрует whitespace-имена (`:1478-1479`) и не проверяет результат: `WithIndex("  ")` даёт `IndexHints = []` (не `null`) → `SqlSourceRenderer.cs:236-238` всё равно зовёт `RenderIndexHint([], Use, …)` → MySQL `" use index ()"`, SQL Server `index()` внутри `with (...)` — **синтаксически невалидный SQL, эмитится молча**. `WithoutIndex()` (`:1491-1492`) намеренно ставит `[]` (для SQLite `NOT INDEXED` — валидно), но на MySQL та же `[]` с `Ignore` рендерит `" ignore index ()"` (MySQL `IGNORE INDEX` требует ≥1 имя) — тоже невалидно. PostgreSQL/ClickHouse корректно бросают «Index hints are not supported».

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** после фильтрации в `WithIndex` отклонять пустой результат (`ArgumentException`), кроме `Ignore`, выразимого без имён; и/или в `SqlSourceRenderer` трактовать `IndexHints is { Count: 0 }` как «не выразимо» и бросать `NotSupportedException`; MySQL-рендерер должен явно отвергать пустой список. Негативные тесты: `WithIndex("  ")`, `WithoutIndex()` на MySQL/SQL Server.

**Проверка:** чтение; существующие тесты (`tests/nextorm.mysql.tests/SqlGenerationTests.cs:24-37`, `tests/nextorm.sqlite.tests/...:94-119`, `tests/nextorm.sqlserver.tests/...:26-45`) всегда передают имена и не покрывают пустой/whitespace-кейс.

### 🔴 Находка 87 (P1-кандидат, корректность) — новый `QueryCommand<TResult>.WithKeywordCase` теряет `_from` для CTE/derived источника

**Место:** `src/nextorm.core/Query/QueryCommand.TResult.cs:616-622`; `src/nextorm.core/Query/QueryCommand.cs:472` (`ResetPreparation` → `_from = null`); `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:44` (`cmd._from ?? cmd._dataContext.GetFrom(srcType, cmd)`); контраст — `Hint` (`QueryCommand.TResult.cs:573-580`).

**Что не так.** `WithKeywordCase` делает `Clone() → ResetPreparation() → KeywordCase = …` и **не сохраняет `_from`**. Для источника-`TableAlias` (CTE/производный запрос: `ctx.WithRecursive("nums", …).From("nums")…`, ср. `tests/nextorm.sqlserver.tests/SqlGenerationTests.cs:752`) `GetFrom(srcType)` восстановить его не может, поэтому клон теряет `FROM`. Это в точности баг, закрытый для `Hint` при Находке 56 (`:3589`: «Прочие clone+reset методы … вынесены на следующий аудит»); `WithQuotedIdentifiers` (`:589-595`) и `WithNamingConvention` (`:603-609`) несут тот же латентный дефект, а `WithKeywordCase` добавляет в семейство новый публичный член.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** в `WithKeywordCase` повторить приём `Hint` (`var source = cmd._from; cmd.ResetPreparation(); cmd._from = source;`) либо (предпочтительно) ввести единый `CloneAndReset()`/`ResetPreparationKeepingFrom()` и перевести на него все clone+reset методы. Тест: CTE/derived source + `.WithKeywordCase()` содержит `from <cte>`; то же для `WithQuotedIdentifiers`/`WithNamingConvention`.

**Проверка:** чтение пути `_from` (clone → reset → `QueryPreparer:44`); фикс `Hint` и его регресс-гейт подтверждают механизм; теста на CTE + `WithKeywordCase` нет.

### 🟡 Находка 88 (P2, мёртвый публичный член) — `ISqlDialect.SupportsTupleFunctions` больше никем не читается

**Место:** `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs:179`; `src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:60`; потребители — `src/nextorm.core/Visitors/TupleSqlTranslator.cs:75,105`.

**Что не так.** Флаг переведён на `Tuple is not null`, но `rg "SupportsTupleFunctions"` по `src/`/`tests/` находит **только определения** (и доки): `TupleSqlTranslator` гейтит по `visitor.Dialect.Tuple` напрямую (`:75,:105`, а inline-ветка вообще не проверяет диалект). Публичный DIM/virtual без потребителей — мёртвая поверхность; ранее (CHTUP1) член был осмысленным гейтом. Ссылки в доках (`docs/providers/clickhouse.md` +RU, `capability-matrix.md`, `advanced/api-reference.md` +RU) держат xref, поэтому удаление требует их правки. Кодовая сторона мёртвого члена; API-сторона — `API-NAMING-REVIEW.md`, TUP1.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** либо удалить `ISqlDialect.SupportsTupleFunctions`/`SqlDialectBase.SupportsTupleFunctions` до заморозки (alpha; обновить xref в EN+RU), либо использовать флаг как единственный гейт в `TupleSqlTranslator` и убрать прямые `Dialect.Tuple`-проверки. Если оставить — тест, читающий флаг, иначе он снова останется декоративным.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Сцепленные присвоения.** `Builders/EntityBuilder.cs:206,262,909`: три оператора в одну строку — `cmd.TableHints = TableHints;        cmd.IndexHints = IndexHints;        cmd.IndexHintKind = IndexHintKind;` (аналогично `dst` в `CopyState`) — форматный дефект от патча, косметика.
- **B. Двойная пустая строка.** `Visitors/TypeFacts.cs:92-94`: между `IsTupleLike` и следующим `<summary>` — два пустых перевода строки.
- **C. `Kw(...)`-фрагменты смешивают keywords с именами функций/типов.** `ExtendedScalarFunctionTranslator.cs:159` (`"cast(pg_typeof("`), `JsonSqlTranslator.cs:210,222` (`" as jsonpath))"`, `" as jsonb)"`), `InValuesTranslator.cs:81,105`. Контракт `KeywordCase`/`SqlKeywords` («меняются только ключевые слова») при этом неточен; вреда нет (идентификаторы/типы SQL регистронезависимы), но формулировку стоит либо сузить до фрагментов, либо разбить вызов (`Kw("cast(") + "pg_typeof("`).
- **D. `PostgresTupleRenderer` игнорирует `KeywordCase`.** `PostgresDialect.cs:397-401` эмитит `ROW(...)` в верхнем регистре даже в `Lower`, а `ClickHouseTupleRenderer` (`:763-767`) — `tuple(...)` в нижнем даже в `Upper`; у `ITupleRenderer` нет `KeywordCase`-параметра. Тесты фиксируют `ROW(id, somestring)`, т.е. выглядит намеренно, но новая поверхность сразу даёт расхождение (API-сторона — TUP2).
- **E. `SqlKeywords` public + неограниченный кэш.** `SqlKeywords.cs:14,17-18`: `ConcurrentDictionary` растёт на каждый уникальный `text`, а публичный API позволяет передать произвольную строку (неограниченный кэш, неверный регистр идентификатора). `IDisposable`-дефекта нет; решение по видимости — `API-NAMING-REVIEW.md`, KC2.
- **F. Nullable-возврат `IIndexHintRenderer.RenderIndexHint` + молчаливый skip.** `DialectCapabilities.cs:272`, `SqlSourceRenderer.cs:236-238,258-259`: `null`-результат молча отбрасывает хинт. Сейчас ни одна реализация не возвращает `null` (все бросают), поэтому это латентная ловушка: либо сделать член non-nullable с исключением, либо закрепить `Debug.Assert(result is not null)`.
- **G. Имена индексов эмитятся verbatim.** Задокументировано в XML-доке `EntityBuilder.WithIndex` (`:1459`); имена с reserved-словом/спецсимволами — ответственность вызывающего. Приемлемо; при желании — через `Escape`.
- **H. Асимметрия план-ключа.** `QueryPlanEqualityComparer.cs:342-346` хэширует `IndexHintKind` и сравнивает его безусловно, поэтому `WithIndex(IndexHintKind.Force)` без имён даёт отдельный план, хотя SQL равен обычному запросу, — безвредное дублирование планов; при желании канонизировать (хэшировать `IndexHintKind` только при непустых `IndexHints`).
- **I. Задокументированная фаза 3 — честная и полная.** `docs/advanced/limitations.md:24` (EN+RU) и `docs/advanced/limitations.md:177-179` перечисляют непокрытые clause-эмиттеры; аудит их повторно не открывает. Вне перечня — только два места Находки 85.
- **J. Тонкое покрытие failure-путей трёх фич.** `KeywordCase`: только skeleton/пагинация/алиасы (sqlite) + по одному тесту PG/SQL Server; нет `Upper`-тестов на table hints/pivot/array join. Index-хинты: нет негативов на пустой/whitespace-список и `WithoutIndex()` на MySQL. Row-значения: `PostgresTupleRenderer` не проверяется на `Lower`/`Upper`. Тесты-кандидаты перечислены в Находках 85–88.

**Проверка (22.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых **0**; `rg 'Skip='` — 0, `Task.Delay` — 2 baseline `(0)`, `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — 0; `rg -c NoWarn` по 7 `.csproj` — 0; `find PublicAPI*.txt` — **0** (Шаг 5 открыт); `roslyn members` по `SqlKeywords`/`IIndexHintRenderer`/`ITupleRenderer`/`KeywordCase`/`IndexHintKind` — XML-doc есть у всех; `rg "SupportsTupleFunctions" src/ tests/` — только определения (Находка 88); `git diff`/чтение — сцепленные строки (ℹ️ A), двойная пустая (ℹ️ B). Сфокусированные SQL-gen тесты: sqlite **222/222**, postgres **269/269**, sqlserver **214/214**, mysql **59/59**, clickhouse **195/195**. Публичная сторона — `API-NAMING-REVIEW.md`, KC1–KC5 / TUP1–TUP2.

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

**Пост-фикс 23.09.2026 (по находкам 85–88).** Все четыре применены в рабочем дереве:
- **Находка 85** — `SqlBuilder.cs` ARRAY JOIN `" as "` → `Kw(" as ")`; `MakeTableHints` получил параметр `KeywordCase` (`ISqlDialect`/`SqlDialectBase`/`SqlServerDialect`, вызовы в `SqlSourceRenderer`), поэтому `Upper` + `WithTableHint`/`WithIndex` даёт `WITH (nolock, INDEX(...))`. Остальные clause-эмиттеры остаются задокументированной фазой 3 (KC4).
- **Находка 86** — `EntityBuilder.WithIndex` для `Use`/`Force` с пустым (после whitespace-фильтра) списком теперь no-op (`IndexHints = null`); рендереры MySQL и SQL Server бросают `NotSupportedException` на пустом списке (`WithoutIndex()` на MySQL отклоняется, SQLite `NOT INDEXED` сохранён).
- **Находка 87** — `WithKeywordCase` (и заодно `WithQuotedIdentifiers`/`WithNamingConvention`, тот же паттерн) сохраняют `_from` до `ResetPreparation`, как `Hint` (Находка 56).
- **Находка 88 / TUP1** — `TupleSqlTranslator` теперь гейтит по `SupportsTupleFunctions` (в дополнение к `Dialect.Tuple`), поэтому флаг имеет потребителя.

**Фаза 3 `KeywordCase` (23.09.2026).** Все ранее отложенные диалектные клозы подняты в `Upper` (см. перечень в `docs/advanced/limitations.md`, EN+RU): добавлен `KeywordCase`-параметр в `IArrayJoinRenderer`/`ILimitByRenderer`/`ILockRenderer`/`ITableSampleMethods`/`IPivotRenderer` и в `ISqlDialect.{MakeForJson,MakeForXml,MakeTemporalTable,MakeFinal,MakeSample,MakeSettings,MakeGrouping,MakeGroupingSets,MakeGroupByTotals,MakeWithinGroup,MakeLikeEscape,MakeCase,MakeApply,MakeReturning,MakeOutput,MakeLastInsertId,MakeIdentityFunction,MakeMaxRecursion,RenderQueryHints}`; ARRAY JOIN `as` (`SqlBuilder`) переведён на `Kw`. `KeywordCase.Lower` не изменился. Конструкторы `ROW`/`tuple` намеренно вне политики (функциональные конструкторы, TUP2).

Тесты добавлены: SQLite CTE + `WithKeywordCase` (FROM сохраняется), MySQL whitespace-index + `WithoutIndex` throw, SQL Server Upper table/index hints. Полный прогон: core 220, sqlite 297, postgres 290, sqlserver 260, mysql 81, mariadb 31, clickhouse 255 — 0 failed; build Release 0/0.

## 🔎 Верификация 23.09.2026 — фаза 3 `KeywordCase`, uncommitted working tree; новых 🔴/🟡 нет, ℹ️ — 4

**Область (только фаза 3 `KeywordCase`; DML `INSERT`, index-хинты и row-значения в дереве — вне прохода, см. 22.09).** `KeywordCase`-параметр добавлен к 19 `ISqlDialect`-методам (`MakeApply`, `MakeGrouping`, `MakeGroupingSets`, `MakeGroupByTotals`, `MakeForJson`, `MakeForXml`, `MakeTemporalTable`, `MakeFinal`, `MakeSample`, `MakeSettings`, `MakeWithinGroup`, `MakeLikeEscape`, `MakeCase`, `MakeReturning`, `MakeOutput`, `MakeLastInsertId`, `MakeIdentityFunction`, `MakeMaxRecursion`, `RenderQueryHints`) и к 7 методам capability-рендереров (`IArrayJoinRenderer.Render`, `ILimitByRenderer.Render`, `ILockRenderer.Render`, `ITableSampleMethods.Render`, `IPivotRenderer.RenderPivot`/`RenderUnpivot`, `IIndexHintRenderer.RenderIndexHint`). `ITupleRenderer` **намеренно** без параметра (`ROW`/`tuple` — функциональные конструкторы, TUP2). ARRAY JOIN element alias переведён на `Kw` (`SqlBuilder.cs:153`). Эмиттеры: `SqlDialectBase.cs:168,204,210,234,263,277,286,293,329,378,481,604,642,648,655,659,699,701,703,707`; `MySqlDialect.cs:47,245,328,337,389,399`; `PostgresDialect.cs:23,73,293,367,376,393`; `SqliteDialect.cs:15,19,212,222,254`; `SqlServerDialect.cs:105,109,121,141,180,373,398,416,431,452,525,543,575,579`; `ClickHouseDialect.cs:74,95,107,130,310,369,573,576,627,714`. Вызовы: `SqlBuilder.cs:75,94,99,109,117,125,153,167,170,214,241,243,274,289,307,319,328,338,363,375,380,395,402,421,451,478,486,498`; `SqlSourceRenderer.cs:37,54,63,124,171,202,252,262,275,304,318,371,406,429,448,497,576,578`; `BaseExpressionVisitor.cs:127,129,392,428`; `PredicateTranslator.cs:23,25,66,189-194,236,250,266,268,271,274,283,285,328,360,391`; `ScalarFunctionTranslator.cs:113,116`; `StringFunctionTranslator.cs:381,382,401`; `AdvancedAggregateTranslator.cs:477`; `WindowSql.cs:173,183,186,192-198`.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`: `EntityBuilderExtensions.cs:123/125,140/142,156/158`, `ExpressionPlanEqualityComparer.cs:421/423`, `InMemoryLinqSource.cs:82/84`) = **11/11** оправданных; в файлах фазы 3 (`ISqlDialect`/`SqlDialectBase`/провайдеры/`DialectCapabilities`/`SqlBuilder`/`SqlSourceRenderer`/визитёры/`QueryCommand`) — **0** новых. `NoWarn` в 7 библиотечных `.csproj` — **0** (единственное вхождение — комментарий в `Directory.Build.props:37`). Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне фазы 3); `Thread.Sleep`/пустых `catch`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer` + `CA2254`; пре-существующее, не трогалось). `find PublicAPI*.txt` — **0** (Шаг 5 открыт). CS1591 не подавлен, `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true` → build `0/0` доказывает member-уровневое покрытие. Сфокусированные SQL-gen-тесты (Release, `--no-build`, `FullyQualifiedName~SqlGenerationTests`, включает `InsertSqlGenerationTests`): sqlite **224/224**, postgres **270/270**, sqlserver **217/217**, mysql **62/62**, mariadb **27/27**, clickhouse **196/196** — 0 failed.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новые `*TupleRenderer`/`*IndexHintRenderer` — `sealed`-синглтоны без полей/ресурсов; `SqlKeywords.UpperCache` (`SqlKeywords.cs:14`) — статический `ConcurrentDictionary`, unmanaged-ресурсов/финализаторов нет, `Dispose` не нужен. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` в фазе 3 — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. Все изменения — `Kw(kind)`/`SqlKeywords.Of(case, literal)` на этапе построения SQL (холодно) и `string.Join` по уже отрендеренным фрагментам. |
| 4. God-классы | ⚠️ Пре-существующие крупные файлы (`ISqlDialect` **1080**, `SqlDialectBase` **708**, `SqlSourceRenderer` **659**, `SqlBuilder` **653**); новых файлов фаза 3 не добавила — god-классов-новоделов нет. |
| 5. Хэш-ключи / план-кэш | ✅ `QueryPlanEqualityComparer` включает `ResolvedKeywordCase` в `Equals`+`GetHashCode` (`:156,457`); `QueryCommand.Clone.cs:49-50` копирует `KeywordCase`/`ResolvedKeywordCase`; `EntityBuilder`/`JoinedEntityBuilder` пробрасывают `KeywordCase` во всех copy-путях. Побочно — см. ℹ️ B (ключи `SqlKeywords` не все константны). |
| 6. События / исключения | ✅ Подписок нет, новых `catch` нет; новые `throw` — `NotSupportedException`/`ArgumentOutOfRangeException` с понятными сообщениями. |
| 7. NRT / возвратный тип | ✅ `string? MakeMaxRecursion` сохранён (`SqlDialectBase.cs:329`), nullable-аннотации корректны, сборка `0/0`. |

### ℹ️ Наблюдения (фикс не требуется)

- **A. Мёртвых `keywordCase`-параметров нет.** Из 26 `ISqlDialect`-методов и 7 renderer-методов с `KeywordCase` параметр не читается только там, где тело **не эмитит ни одного ключевого слова**: базовые no-op/сентинелы `SqlDialectBase.MakeGroupByTotals` (`:168`, возвращает `grouping`), `MakeCase` (`:378`), `MakeMaxRecursion` (`:329`), `RenderQueryHints` (`:642`), `MakeTableHints` (`:648`), `MakeForJson`/`MakeForXml` (`:655,659`, `string.Empty`), `MakeTop` (`:677`, `false`) и ветка `_ => columns` в `MakeGrouping` (`:277`); плюс `MySqlDialect.RenderQueryHints` (`:47`) и `PostgresDialect.RenderQueryHints` (`:73`) — оба эмитят только комментарий `/*+ ... */` (поиск `select` через `MatchesSelect` — `OrdinalIgnoreCase`, поэтому `Upper`-вывод хинты по-прежнему получает). Все они — interface-contract параметры, обязательные для единой сигнатуры override'ов; **0 genuine dead-param** (ответ на вопрос 1). SQL Server `MakeApply` fallthrough (`SqlServerDialect.cs:184`) пробрасывает `keywordCase` в `base.MakeApply(...)` — не мёртв.
- **B. `SqlKeywords`-кэш: ключи не только константы (коррекция 22.09).** `SqlServerDialect.MakeTop` (`:418`) передаёт интерполированное `top({limit}) with ties`, а `ClickHouseDialect.MakeJoinKeyword` (`:107,130`) — конкатенацию `(isGlobal?…)+kind/modifier`. Ключи `MakeJoinKeyword` ограничены (enum-комбинации), но ключ `MakeTop` зависит от пользовательского `limit` → рост без верхней границы. Утверждение таблицы 22.09 («ключи — константы-литералы, неограниченного роста нет») неточно. Это **не новая находка** (уже трекается KC2/наблюдением E) и **не регресс фазы 3** (`MakeTop`-тело с `Kw` — фаза 1/2); при заморозке предпочтительна сборка фрагмента из `Kw("top(") + limit + Kw(withTies ? ") with ties" : ")")`, чтобы ключ оставался константным.
- **C. Контракт «keywords and separators only» остаётся неточным (расширение наблюдения 22.09 C).** Фаза 3 добавила `Kw`-фрагменты, смешивающие ключевые слова с пунктуацией/типами/функциоподобными токенами: `SqlServerDialect.MakeForXml` (`:125` `", root('"`, `:137` `", elements"`), `MakeForJson` (`:131` `", include_null_values"`), `MakeCase` (`:380` `"cast("`, `" as bit)"`), `MakeTableHints` (`:109` `" with ("`); `SqlDialectBase.MakeSample` (`:212` `" sample "`), `MakeGrouping`/`MakeGroupingSets` (`"rollup ("`/`"cube ("`/`"grouping sets ("`), `MakeWithinGroup` (`:604`), `ClickHouseLimitByRenderer` (`:631` `"limit "`/`" by "`), `SqlServerPivotRenderer` (`" pivot ("`/") for "/" in ("`). Вреда нет (регистр SQL нечувствителен, **идентификаторы/значения через `Kw` не проходят** — проверено для `columns`/`hints`/`settings`/`indexes`/`limit`), но формулировку `SqlKeywords`-контракта стоит либо сузить, либо заменить на «фрагмент без пользовательских идентификаторов».
- **D. XML-doc новых параметров — member-уровень полон, param-уровень неполон.** CS1591 не подавлён, build `0/0`, DocFX `0/0`; однако из 26 изменённых `ISqlDialect`-методов `<param name="keywordCase">` есть только у `MakeTop` (`ISqlDialect.cs:980`), а 7 renderer-методов дают лишь фразу `<paramref name="keywordCase"/>…` в `<summary>` (`DialectCapabilities.cs:70,167,181,196,203,214,254`; у `IIndexHintRenderer.RenderIndexHint` `:279` нет и этого). Build-предупреждений нет (у этих членов и раньше не было `<param>`-тегов, поэтому CS1573 не срабатывает). Оформлено как **KC6** в `API-NAMING-REVIEW.md`.
- **E. `KeywordCase.Lower` побайтово сохранён — подтверждено.** Каждая мигрированная точка в `Lower` возвращает ровно прежний литерал: `SqlKeywords.Of` (`SqlKeywords.cs:17-18`) при `Lower` возвращает `text` без изменений; перепроверены все фаза-3-эмиттеры и вызывающие сайты (в т.ч. порядок `limit`/`offset`, `fetch first … rows with ties`, `distinct on (`, `array join`/`left array join`, `for system_time …`, `settings`, `returning`/`output`, `option (maxrecursion n)`, `pivot`/`unpivot`). Отдельно: `SqlServerDialect.RenderQueryHints` (`:452-464`) извлекает тело из `maxRecursionOption` по `'('…[..^1]`, поэтому уже-верхнерегистровая опция `OPTION (MAXRECURSION 100)` корректно сворачивается в `OPTION (MAXRECURSION 100, …)` (тест `KeywordCase_Upper_ShouldUppercaseDialectClauses`, `SqlServerDialect`-тесты). 6 SQL-gen-наборов с точными `Be(...)`-ассертами — 0 failed (см. «База»). Единственное намеренное исключение — `ROW`/`tuple` (TUP2), оно задокументировано, а не является изменением `Lower`.
- **F. `ROW`/`tuple` вне политики — подтверждено кодом и докой.** `PostgresTupleRenderer` (`PostgresDialect.cs:397-401`) всегда эмитит `ROW(`, `ClickHouseTupleRenderer` (`ClickHouseDialect.cs:763-767`) — `tuple(`; `ITupleRenderer` без `KeywordCase`; `docs/advanced/limitations.md` (EN+RU) фиксирует исключение. Тесты `TupleCreate_ShouldRenderRowConstructor`/`TupleElementAccess_OnInlineConstructor_ShouldFoldToArgument` проходят.
- **G. Пост-фиксы 85–88 подтверждены в коде.** Находка 85 — `SqlBuilder.cs:153` → `Kw(" as ")`, `MakeTableHints` с `KeywordCase` (`ISqlDialect.cs:554`, `SqlServerDialect.cs:108`); Находка 86 — `EntityBuilder.WithIndex(kind, …)` (`:1483-1488`) фильтрует whitespace и для `Use`/`Force` ставит `null`, MySQL/SQL Server бросают на пустом списке (`MySqlDialect.cs:402`, `SqlServerDialect.cs:591`); Находка 87 — `WithKeywordCase`/`WithQuotedIdentifiers`/`WithNamingConvention` сохраняют `_from` (`QueryCommand.TResult.cs:592-594,608-610,622-625`); Находка 88/TUP1 — `TupleSqlTranslator.cs:75,105` гейтит по `SupportsTupleFunctions`. Все четыре — ✅ закрыты.

**Итог фазы 3.** Новых 🔴/🟡 нет; 🔴 Находка 87 и 🟡 Находки 85/86/88 остаются закрытыми (подтверждены в коде). ℹ️ — 4 (A–D; E–G — подтверждающие). Публичная сторона — `API-NAMING-REVIEW.md`, KC1 (расширен) / KC6 (новый P2).

**Проверка (23.09.2026).** Build Release — **0/0**; подавления — **11/11** (0 новых); слоп-скан чист; `find PublicAPI*.txt` — **0**; 6 SQL-gen-наборов — **0 failed** (sqlite 224 / postgres 270 / sqlserver 217 / mysql 62 / mariadb 27 / clickhouse 196). Чтение: `KeywordCase`-использование по всем 7 эмиттерам и всем вызывающим сайтам; `SqlKeywords.Of`-инвариант `Lower`; проброс `KeywordCase` через `Clone`/`CopyState`/`CreateJoined`/`CloneForCache`. Докфайл `docs/advanced/limitations.md` (EN+RU) — покрытие заявлено полно, исключение `ROW`/`tuple` оговорено; `docs/specs/roadmap/todo_sql_keyword_case.md` удалён.

## 🔎 Точечный аудит 23.09.2026 — DML `INSERT` (фаза 4): batch values surface, uncommitted working tree; 🔴 — 1 (P1), 🟡 — 1, ℹ️ — 6

**Область.** Только новая batch-поверхность значений `InsertBuilder<TEntity>` (`src/nextorm.core/Builders/InsertBuilder.cs`, 644 строки, untracked): `Values<TSource,TResult>(IEnumerable<TSource>, Expression<Func<TSource,TResult>>)` (`:169-191`, `ParseMapping` `:410-460`), `Value<TScalar>(TScalar)` (`:201-210`) / `Values<TScalar>(IEnumerable<TScalar>)` (`:220-236`) через `ResolveSingleWritableProperty` (`:462-483`), `Values<TValue>(Expression<Func<TEntity,TValue>>, IEnumerable<TValue>)` (`:249-277`), приватный `ValueMode` (`:27-35`) вместо `_entityRows`, `[OverloadResolutionPriority(1)]` (`:112,126`), `BuildCommand`-валидация «колонка == `_rowCount`» (`:596-600`). Рендер (`SqlMutationBuilder.cs:62-86`) и `InsertCommand` не менялись. `tests/nextorm.core.tests/_DumpTests.cs` — untracked scratch, игнорируется.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фазы 4 — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан выполнен вручную. `.editorconfig` — 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer` + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в 7 `.csproj` — 0), build `0/0` ⇒ member-уровневое покрытие XML-doc; `docfx` `0/0`.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет (`InsertBuilder` ресурсов не держит); `SqlMutationBuilder`/`QueryExecutor` не менялись. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. `source as IReadOnlyList ?? ToList()` — одна материализация (`:132,176,225,256`). `ParseMapping` компилирует делегат (`Func<TSource,object?>`) один раз на колонку на вызове, а не на строку — ℹ️ A (бокс value-type per row). |
| 4. God-классы/методы | ⚠️ `InsertBuilder` **400 → 644** (>500, перешёл порог); `ParseMapping` **51** строка (>30). `MutationCommand` 133, `SqlMutationBuilder` 138, `InsertReturningBuilder` 267. → 🟡 Находка 90. |
| 5. Хэш-ключи / план-кэш | ✅ Мутационный путь без план-кэша; `SqlMutationBuilder` индексирует `Values[r]` при `r < RowCount`, а `BuildCommand` гарантирует `Values.Count == _rowCount` для каждой колонки (`:596-600`) — выхода за границы нет. |
| 6. События / исключения | ✅ Подписок/новых `catch` нет; исключения — `ArgumentException`/`InvalidOperationException`/`NotSupportedException`/`BuildSqlCommandException` (public) с понятными сообщениями. ℹ️ D (текст `EnterExclusiveMode`), ℹ️ B (half-entered mode при throw). |
| 7. NRT / возвратный тип | ✅ `object?`-бокс nullable-безопасен (`Convert(..., object)`), `FromConstant(null)` → SQL NULL; сборка `0/0`. ℹ️ C (null-элемент `IEnumerable<TEntity>`). |

### 🔴 Находка 89 (P1, корректность/консистентность) — column-oriented и single-`Value` пути не отклоняют computed-колонку-цель

**Место:** `src/nextorm.core/Builders/InsertBuilder.cs:58-66` (`Value(column, TValue)`), `:77-103` (`Value(column, Expression)`), `:249-277` (`Values(column, values)`); корень — `ResolveProperty` (`:496-505`) без проверки `IsComputed`.

**Что не так.** `ResolveProperty` делает только `FindProperty` и возвращает любой mapped-член. Проверку `IsComputed` имеют ровно три пути: entity (`:138`), mapping (`:451`, покрыт `ValuesMapping_ComputedMember_ShouldThrow`) и `ResolveSingleWritableProperty` (`:468`). Поэтому `ctx.InsertInto<IInsertEntity>().Values(x => x.Total, new[] { 1, 2 })` и `...Value(x => x.Total, 1)` **молча рендерят** `insert into insert_entity (total) values ($p0), ($p1)` (`Total` — `[DatabaseGenerated(Computed)]`, `tests/nextorm.sqlite.tests/InsertSqlGenerationTests.cs:21-23`), т.е. INSERT в generated-колонку — гарантированный отказ провайдера (SQL Server/PostgreSQL/SQLite) либо расхождение с маппингом. Публичный контракт одного и того же builder'а противоречив: projection-форма computed отклоняет, column-форма — нет. (Identity в column-форме допустим — это осознанный explicit-identity insert; computed — никогда не записываем.)

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** ввести `ResolveWritableColumn(LambdaExpression, string)`, который после `ResolveProperty` отклоняет `IsComputed` (`NotSupportedException` с текстом как в `:452`) и **не трогать** `ResolveProperty` (он же обслуживает read-селекторы `ReturningIdentity`/`Returning`). Перевести на него три write-сайта (`:63`, `:89/94`, `:255`). Негативные тесты: `Values(x => x.Total, new[]{1})`, `Value(x => x.Total, 1)` на SQL-gen наборе каждого провайдера. Публичная сторона — `API-NAMING-REVIEW.md`, IDML13.

**Проверка:** чтение; `grep -n IsComputed InsertBuilder.cs` → только `:138,451,468`; `ResolveProperty` callers — `:63,89,94,255,512`; тест `ValuesMapping_ComputedMember_ShouldThrow` (`InsertSqlGenerationTests.cs:338-346`) покрывает только mapping-форму; column-форма негативом не покрыта. Build `0/0`.

### 🟡 Находка 90 (P2, god class / длинный метод) — `InsertBuilder<TEntity>` 644 строки и `ParseMapping` 51 строка

**Место:** `src/nextorm.core/Builders/InsertBuilder.cs` (644 строки); `ParseMapping` `:410-460`.

**Что не так.** За три фазы файл вырос 279 → 400 → **644** и перешёл порог god-класса (>500 из `dotnet-csharp-code-smells` §5); `ParseMapping` — >30 строк. Он совмещает три ответственности: публичные fluent-терминалы, разрешение метаданных/выражений (`ParseMapping`/`FindProperty*`/`Resolve*`) и gate-режимы (`ValueMode`). Публичную форму менять не нужно — декомпозиция внутренняя.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести binding-логику в `internal` helper (`InsertValueBinder<TEntity>`: `ParseMapping`/`FindPropertyByName`/`FindProperty`/`ResolveProperty`/`ResolveSingleWritableProperty`/`GetOrAddColumn` + `ValueMode`-гейт), оставив `InsertBuilder` тонким fluent-фасадом; либо проверенный в репозитории приём — `partial`-разбивка (`InsertBuilder.Values.cs`, как `QueryCommand.*.cs`). Тесты поведения не меняются.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Бокс value-type в mapping-пути.** `Values<TSource,TResult>` компилирует `Func<TSource,object?>` через `Expression.Convert(valueExpression, typeof(object))` (`:454-455`) — на каждую ячейку value-type даёт бокс. Это **лучше** entity-пути (`PropertyInfo.GetValue` + reflection, уже наблюдение B фазы 1), но при «горячем» батче — кандидат для `nextorm-db-perf-analyst` (компилированный типизированный аксессор/`ArrayPool` без object-бокса).
- **B. Half-entered mode при throw.** `EnterSingleValueMode`/`EnterColumnSequenceMode` выставляют `_mode` до валидации выражения/списка (`:61,253`); исключение оставляет builder в `Single`/`ColumnSequence` без колонок. Builder документирован как single-use, поэтому безвредно; при желании — перенести `_mode`-присвоение после валидации.
- **C. `Values(IEnumerable<TEntity>)` без null-элементов.** `property.PropertyInfo.GetValue(list[i])` (`:143`) даёт `NullReferenceException` на `null`-элементе вместо `ArgumentException`. Низкий приоритет.
- **D. Текст `EnterExclusiveMode` неточен для повторного вызова той же формы.** `:394-400` («...cannot be combined with Value or another values form...») используется и когда повторно вызван ровно тот же `Values(...)`.
- **E. Однопараметрический `Value(x => x.Col)` компилируется и биндится к `Value<TScalar>`.** `:201` (TScalar = `Expression<Func<...>>`) → `FromConstant(delegate)` → provider-ошибка, либо `ResolveSingleWritableProperty`-исключение на многоколоночной сущности. Публичная сторона — IDML12.
- **F. Асимметрия identity.** Mapping- и column-формы допускают identity-колонку-цель (explicit identity insert), entity-форма исключает. Осознанно, но контракт нигде не задокументирован (IDML13/IDML14).

**Проверка (23.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых в фазе 4 — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`, `Thread.Sleep`/пустых `catch`/`NoWarn` — 0; `find PublicAPI*.txt` — **0**; `roslyn members NextORM.Core.InsertBuilder'1` — **16** публичных методов (фаза 3 — 12; +4: `Value<TScalar>`, `Values<TScalar>`, `Values<TSource,TResult>`, `Values<TValue>(col, values)`); `grep -n IsComputed InsertBuilder.cs` — 3 (только `:138,451,468`); `wc -l` — **644**. Сфокусированные тесты batch-форм: SQLite `InsertSqlGenerationTests` (mapping/scalar/column-sequence/дубликаты/пустой источник), интеграционные `CommonTestSuite.Insert.cs:284,304,325` (пользовательский прогон: SQLite/PG/MySQL/SQL Server — green). Публичная сторона — `API-NAMING-REVIEW.md`, IDML11–IDML14.

## 🔎 Точечный аудит 23.09.2026 — DML `INSERT` (фаза 5): строка «только дефолты» и per-column `DEFAULT`, uncommitted working tree; 🔴 — 0, 🟡 — 2, ℹ️ — 6

**Область.** `InsertBuilder.BuildCommand` (`src/nextorm.core/Builders/InsertBuilder.cs:377-400`) отдаёт команду с пустым списком колонок при `!HasWritableColumns()` (`:402-411`), иначе — прежний `InvalidOperationException`. Новый публичный маркер `SqlDefault` (`src/nextorm.core/SqlDefault.cs:13-16`), overload `Value<TValue>(column, SqlDefault)` (`Builders/InsertBuilder.Values.cs:39-47`), `InsertValue.IsDefault`/`FromDefault()` (`Query/Mutations/MutationCommand.cs:65-96`). Три DIM `ISqlDialect.{SupportsDefaultValues,UsesEmptyColumnListForDefaults,SupportsColumnDefault}` (`DataContext/Dialect/ISqlDialect.cs:1041,1048,1056`) + `SqlDialectBase` (`:710,712,714`) + overrides (PG/SQL Server/SQLite — `SupportsDefaultValues`; MySQL/MariaDB — `+UsesEmptyColumnListForDefaults`,`SupportsColumnDefault`; PG/SQL Server — `SupportsColumnDefault`). Рендер — `SqlMutationBuilder.MakeInsert` (`DataContext/SqlMutationBuilder.cs:41-62,94-101`), детект маркера — `ParseMapping` (`InsertBuilder.cs:224`) и binding-loop (`InsertBuilder.Values.cs:161-168`), `ToInsertValue` (`:260-261`).

**Ноль-колоночный путь и порядок OUTPUT/RETURNING — корректен.** Для `Columns.Count==0` (`SqlMutationBuilder.cs:45-62`): гейт `SupportsDefaultValues` (ClickHouse → `NotSupportedException`), `OUTPUT` — сразу после имени таблицы **до** `default values` (`:50-51`, T-SQL), `RETURNING` — **после** (`:58-59`, ANSI), `() values ()` — только MySQL/MariaDB (`:53-54`). Per-cell `DEFAULT` гейтится `SupportsColumnDefault` (`:95-100`, SQLite/ClickHouse → `NotSupportedException`). Тесты `Insert_NoValues_*` (PG/SQLite/MySQL/SQL Server), `Insert_NoValues_WithOutput_ShouldPlaceOutputBeforeDefaultValues`, `Value_ColumnDefault_ShouldThrow` (SQLite/ClickHouse) подтверждают.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фазы 5 — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан выполнен вручную. `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен, build `0/0` ⇒ member-уровневое покрытие XML-doc; `docfx` 0/0.

| Категория | Статус фазы 5 |
|---|---|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `SqlMutationBuilder` держит `StringBuilderPool.Shared` в `try/finally` (`:34,121-124`); `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ 0 новых; `SqlDefault`/`InsertBuilder.*` — без `#pragma`/`SuppressMessage`. |
| 3. LINQ на hot path | ✅ `MakeInsert` — ручные циклы; LINQ (`.Select`) только в `SqlDialectBase.MakeOutput` на холодной сборке SQL. |
| 4. God-классы/методы | ✅ `InsertBuilder<TEntity>` разбит на `InsertBuilder.cs` **448** + `InsertBuilder.Values.cs` **263** (было 644) — Находка 90 закрыта рекомендованным `partial`-приёмом; `ParseMapping` **57** строк (>30, ℹ️ F). `SqlMutationBuilder` 166, `MutationCommand` 140. |
| 5. Хэш-ключи / план-кэш | ➖ Фича ключей кэша не трогает. |

### 🟡 Находка 91 (P2, корректность/тихий биндинг) — `ParseMapping` детектит `SqlDefault` по точному типу без `UnwrapConvert`, пропуская converted/boxed маркер

**Место:** `src/nextorm.core/Builders/InsertBuilder.cs:224` (`valueExpression.Type == typeof(SqlDefault)`); binding-loop `InsertBuilder.Values.cs:165`; `ToInsertValue` `:260-261`.

**Что не так.** Проверка сравнивает **объявленный тип** правого выражения с `typeof(SqlDefault)`, без `UnwrapConvert`, хотя остальной `ParseMapping` `Convert` разворачивает (`:182`). Если mapped-член имеет тип `object`/base/interface, компилятор вставляет узкое `Convert`: probe (`dotnet run`, expression-tree dump) дал `argType=Object, unwrapped=SqlDefault, node=Convert` для `new Entity { Data = SqlDefault.Value }` с `object Data` и для явного `(object)SqlDefault.Value`. Тогда ветка `:224` не срабатывает, `getValue` компилируется, `SqlDefault` боксится и уходит в `FromConstant` → параметр со значением-маркером вместо SQL `DEFAULT` (ошибка провайдера либо тихая неверная запись). Anonymous-проекция (`new { Name = SqlDefault.Value }`) безопасна (тип члена выводится как `SqlDefault`) и покрыта `ValuesMapping_ColumnDefault_ShouldRenderDefaultKeyword` (PG).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** `UnwrapConvert(valueExpression).Type == typeof(SqlDefault)` и/или рантайм-гард в binding-loop (`var v = getValue(list[i]); accumulator.Values.Add(v is SqlDefault ? InsertValue.FromDefault() : InsertValue.FromConstant(v))`) — зеркально `ToInsertValue`; тест на `object`-типизированную колонку.

**Проверка:** expression-tree probe подтвердил `Convert`; чтение `ParseMapping`; PG-тест покрывает только anonymous-форму. Build `0/0`.

### 🟡 Находка 92 (P2, расширяемость/консистентность) — форма all-defaults/`DEFAULT` захардкожена в `SqlMutationBuilder`, а не на `ISqlDialect`

**Место:** `src/nextorm.core/DataContext/SqlMutationBuilder.cs:53-56` (`" () "`/`"values"`/`"default values"`), `:100` (`"default"`); паттерн-эталон — `ISqlDialect.MakeReturning`/`MakeOutput`/`MakeLastInsertId`/`MakeIdentityFunction` (`ISqlDialect.cs:1062-1069+`).

**Что не так.** Флаги живут на диалекте, но **SQL-текст** обеих форм эмитит ядро: третий диалект с иной формой «строки из дефолтов» не переопределит рендер без правки `nextorm.core`, тогда как `RETURNING`/`OUTPUT`/identity-функция вынесены в `Make*`. Флаги без рендерера — «полурасширяемая» поверхность.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести `MakeDefaultValues(KeywordCase)`/`MakeColumnDefault(KeywordCase)` на `ISqlDialect`/`SqlDialectBase` (дефолты = текущие строки) и вызывать из `MakeInsert`; либо зафиксировать встроенность обеих форм в `<remarks>` флагов. Публичная сторона — `API-NAMING-REVIEW.md`, **IDML15**.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `Value(x => x.Col, default)` потенциально неоднозначен.** `default`-литерал применим и к `TValue`, и к `SqlDefault`-overload'у (`InsertBuilder.Values.cs:18,39`) — сродни IDML5; требует каста. Компиляцией не воспроизводил.
- **B. Column-sequence `Values(column, values)` не проходит `ToInsertValue`** (`:254`) — недостижимо: `TValue` выводится и из селектора (`string`), и из `IEnumerable<TValue>` (`SqlDefault`) → CS0411. Расхождение со scalar-формой (`:261`) формальное.
- **C. `value`-параметр dedicated-перегрузки не читается** (`:39-46`) — намеренно (маркер), любой экземпляр `SqlDefault` эквивалентен; отмечено в `<param>`.
- **D. Сообщение `ResolveSingleWritableProperty`** (`InsertBuilder.cs:251-258`) при `Value(SqlDefault.Value)` на zero-writable сущности советует «project the columns explicitly», хотя корректен `Insert()` — текст устарел.
- **E. Покрытие.** Нет теста zero-column + `RETURNING` (PG/SQLite) и `Returning()` на zero-writable сущности (упражняет `ReturningColumns` + RETURNING-после-`default values`); есть только SQL Server OUTPUT-кейс.
- **F. `ParseMapping` — 57 строк (>30).** Остаток Находки 90; уедет при выносе binder'а.

**Закрытие ранее открытых.** Находка 89 (computed-цель в column-формах) — ✅ закрыта: `ResolveWritableColumn` (`InsertBuilder.cs:283-291`) гейтит `IsComputed` и читается из `Value(column, TValue)`/`Value(column, Expression)`/`Values(column, values)`. Находка 90 (god class) — ✅ закрыта `partial`-разбивкой (`InsertBuilder.cs` 448 + `InsertBuilder.Values.cs` 263; совокупно 711, но тип — тонкий fluent-фасад + values-часть). Находки 82–84 остаются в силе (вне прохода).

**Проверка (23.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых в фазе 5 — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`, `Thread.Sleep`/пустых `catch`/`NoWarn` — 0; `find PublicAPI*.txt` — **0**; `roslyn members NextORM.Core.InsertBuilder` — **17** публичных методов (фаза 4 — 16; +`Value<TValue>(col, SqlDefault)`); `roslyn members NextORM.Core.SqlDefault` — `Value` (static getter); `wc -l` — `InsertBuilder.cs` 448, `InsertBuilder.Values.cs` 263, `SqlMutationBuilder.cs` 166, `MutationCommand.cs` 140; expression-tree probe подтвердил `Convert`-узел (Находка 91). Сфокусированные SQL-gen тесты (заявлены пользователем, Release): sqlite 32 / postgres 22 / sqlserver 20 / mysql 16 / clickhouse 14 — green, включая `Insert_NoValues_*`, `Value_ColumnDefault_ShouldThrow`; интеграционные контейнеры в этом проходе **не выполнялись**. Публичная сторона — `API-NAMING-REVIEW.md`, IDML15–IDML17.

**Закрытие по итогам правок (23.09.2026).** Находка 91 — ✅ закрыта: `ParseMapping` теперь сверяет `UnwrapConvert(valueExpression).Type == typeof(SqlDefault)` (`InsertBuilder.cs:224`), regression-тест `ValuesMapping_BoxedColumnDefault_ShouldRenderDefaultKeyword` (postgres). Наблюдение E — частично закрыто: добавлен `Insert_NoValues_WithReturningIdentity_ShouldAppendReturning` (postgres, `... default values returning id`). Находка 92 — ✅ закрыта: введены `ISqlDialect.MakeDefaultValues(KeywordCase)`/`MakeColumnDefault(KeywordCase)` (база — текущие строки); `SqlMutationBuilder` вызывает их, MySQL переопределяет `MakeDefaultValues` на `() values ()`; флаг `UsesEmptyColumnListForDefaults` удалён. Наблюдение D (текст `ResolveSingleWritableProperty`) остаётся открытым.

## 🔎 Точечный аудит 23.09.2026 — DML `INSERT` (фаза 6): `INSERT ... SELECT`, uncommitted working tree; 🔴 — 0, 🟡 — 3, ℹ️ — 6

**Область.** Новый публичный overload `InsertBuilder<TEntity>.Values<TSource,TResult>(EntityBuilder<TSource>, Expression<Func<TSource,TResult>>)` (`src/nextorm.core/Builders/InsertBuilder.Values.cs:189-205`) и внутренний путь серверной вставки: поля `_source`/`_selectColumns` + `ValueMode.Select` + `EnterSelectMode` (`Builders/InsertBuilder.cs:26-27,36,183-189`), `ParseSelectMapping`/`ExtractMappingMembers` (`:224-281`), ветка `_source` в `BuildCommand` (`:422-425`); `InsertCommand.Source`/`SourceColumns` (`Query/Mutations/MutationCommand.cs:114,150,153`); ветка `Source` в `SqlMutationBuilder.MakeInsert` (`DataContext/SqlMutationBuilder.cs:50-69`, `AppendColumnList` `:187-203`); `QueryPlanner.RenderSource` (`DataContext/QueryPlanner.cs:63-79`); `DataContext.BuildInsertSql` (`DataContext/DataContext.cs:337-344`). Тесты — `ValuesQuery_*` в 5 провайдерных наборах, `Insert_FromQuery_ShouldPersistSelectedRows` (`tests/nextorm.integration.tests/CommonTestSuite.Insert.cs:73`), `InMemoryContext_ShouldRejectInsertFromQuery` (`tests/nextorm.core.tests/InsertMetadataTests.cs:145`).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фазы 6 — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer` + `CA2254`; пре-существующее, не трогалось). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в 7 `.csproj` — 0; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ member-уровневое покрытие XML-doc.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет. `RenderSource` аллоцирует только managed (`List<Parameter>`, `SqlBuildContext`, `SqlBuilder`, `DefaultParameterProvider`, `DefaultAliasProvider`) на холодном пути мутации; `SqlMutationBuilder` держит `StringBuilderPool.Shared` в `try/finally` (`:38,141-144`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11**. |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `MakeSelect` источника — этап сборки SQL (холодно). `source.Select(mapping)` — не материализация, а построение `QueryCommand`. |
| 4. God-классы/методы | ⚠️ `SqlMutationBuilder.MakeInsert` **119** строк / 3 рендер-ветки; `InsertBuilder.cs` 496 + `InsertBuilder.Values.cs` 297. → 🟡 Находка 95 (метод), Находка 94 (дублирование). |
| 5. Хэш-ключи / план-кэш | ✅ Мутация без план-кэша; источник рендерится в `RenderSource` через `MakeSelect` без записи в `QueryPlanStore`; `PrepareCommand(false, …)` — обычный путь подготовки. |
| 6. События / исключения | ✅ Новых подписок/`catch` нет; `NotSupportedException`/`InvalidOperationException` с понятными сообщениями (CTE, runtime-параметр, computed, mixing). ℹ️ A (недостижимый defensive-throw). |
| 7. NRT / возвратный тип | ✅ `QueryCommand? Source`/`IReadOnlyList<IPropertyMetadata>? SourceColumns` корректно аннотированы; `command.SourceColumns!` — null-forgiving в гарантированно согласованной паре (ℹ️ A). Сборка `0/0`. |

### 🟡 Находка 93 (P2, корректность/контракт) — `_rowCount = 1` делает `Returning(...).Single()` «псевдо-одиночным» для `INSERT ... SELECT`

**Место:** `src/nextorm.core/Builders/InsertBuilder.Values.cs:203` (`_rowCount = 1`); потребитель — `src/nextorm.core/Builders/InsertReturningBuilder.cs:162-166` (`EnsureSingleRow`), `:168-171` (`FirstOrThrow`).

**Что не так.** `EnsureSingleRow` гейтит `_insert.RowCount > 1`, но source-форма всегда выставляет `_rowCount = 1`, хотя `SELECT` может вернуть N строк. Поэтому `ctx.InsertInto<T>().Values(query, m).Returning(x => new { x.Id }).Single()` вставляет N строк и **молча возвращает первую** (`FirstOrThrow`), не бросая `InvalidOperationException`, который обещан в `<exception>` `Single()`/`SingleAsync()` (`InsertReturningBuilder.cs:51,64`). Та же тишина — у `ReturningIdentity(selector).Single()`/`ReturningKey().Single()` (идут через `ExecuteIdentity`/`ExecuteScalar`). Это расходится с `Values(IEnumerable<TEntity>)`, где при N>1 `EnsureSingleRow` корректно бросает. `ToList()` не затронут (возвращает все строки).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** отделить «число строк известно» от `RowCount`: завести `internal bool IsMultiRowSource => _source is not null` (или `_rowCount = int.MaxValue` в source-ветке) и в `EnsureSingleRow` отклонять его с текстом «an INSERT ... SELECT source can return more than one row; use ToList». Тест: источник с 2+ строками + `.Returning(...).Single()` → `InvalidOperationException`. Публичная сторона — `API-NAMING-REVIEW.md`, **IDML20**.

**Проверка:** чтение; `rg "RowCount" src/nextorm.core` → единственный потребитель `InsertReturningBuilder.cs:164`; негативного теста на `INSERT ... SELECT` + `.Single()` нет (в `ValuesQuery_*` — только `ToSql()`/`Insert()`).

### 🟡 Находка 94 (P2, DRY/дублирование) — `EnterSelectMode` дублирует `EnterExclusiveMode`, а `ParseMapping`/`ParseSelectMapping` — общий скелет

**Место:** `src/nextorm.core/Builders/InsertBuilder.cs:167-173` (`EnterExclusiveMode(ValueMode)`) и `:183-189` (`EnterSelectMode()`); `:191-222` (`ParseMapping`) и `:224-249` (`ParseSelectMapping`).

**Что не так.** `EnterSelectMode()` — побайтовый дубль `EnterExclusiveMode(ValueMode.Select)`: та же проверка `_mode != ValueMode.None || _columns.Count > 0`, тот же текст исключения, та же установка `_mode`. Второй случай: `ParseMapping` и `ParseSelectMapping` делят всю «склейку» — `ExtractMappingMembers`, `HashSet<string>` для дублей, `FindPropertyByName`, проверку `IsComputed`, разбор `SqlDefault` — и расходятся лишь финалом (компиляция `getValue` vs сбор списка `IPropertyMetadata`). Это ~30 строк продублированной логики, которую придётся править дважды (например, при добавлении нового гейта).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** `EnterSelectMode()` удалить, вызвать `EnterExclusiveMode(ValueMode.Select)`; вынести приватный `ResolveMappingBindings(Expression)` → `(IPropertyMetadata Property, Expression Value)[]` со всей валидацией, а `ParseMapping`/`ParseSelectMapping` оставить тонкими адаптерами. Публичную форму не менять. Продолжение Находки 90 (god-class `InsertBuilder`).

**Проверка:** чтение — тела методов идентичны; callers `EnterExclusiveMode` — `InsertBuilder.Values.cs:111,154,218,238`; `EnterSelectMode` — `:193`.

### 🟡 Находка 95 (P2, длинный метод) — `SqlMutationBuilder.MakeInsert` вырос до 119 строк с тремя рендер-ветками

**Место:** `src/nextorm.core/DataContext/SqlMutationBuilder.cs:27-145` (source-ветка `:50-69`, all-defaults `:73-87`, values `:89-139`).

**Что не так.** После добавления `INSERT ... SELECT` метод >30 строк (навык §5) и держит три независимых формата на одном уровне, каждый со своим размещением `OUTPUT`/`RETURNING`. `AppendColumnList` (`:187-203`) уже выделен — это правильный вектор; ветки же остаются вложенными.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** выделить `RenderSourceInsert`/`RenderDefaultValuesInsert`/`RenderValuesInsert` (или хотя бы source-ветку `:50-69`), вызываемые из `MakeInsert`. Тесты SQL-gen не меняются.

**Проверка:** `wc -l` — `SqlMutationBuilder.cs` **204** (было 166), `MakeInsert` 119 строк; чтение.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Недостижимый defensive-throw и null-forgiving `SourceColumns!`.** `SqlMutationBuilder.cs:52-53` (`sourceSql is null || sourceParameters is null` → `BuildSqlCommandException`) недостижим: `DataContext.BuildInsertSql` (`:342-343`) всегда передаёт оба непустыми при `Source != null`. `:55` использует `command.SourceColumns!`. Пара `Source`/`SourceColumns` — два независимых nullable-поля, согласованность не выражена типом; при желании — единый `SourceInsert?`-record. Покрытия нет.
- **B. Комментарий `MutationCommand` устарел.** `Query/Mutations/MutationCommand.cs:20-25` утверждает «Mutations deliberately do not reuse `QueryCommand` … keeping the axes separate», но `InsertCommand.Source` (`:150`) — именно `QueryCommand?`, и источник рендерится общим `SqlBuilder`/`QueryPlanner`. Комментарий стоит переформулировать (мутация *ссылается* на query для `INSERT ... SELECT`, не переиспользует его материализацию).
- **C. Слияние параметров — корректно и подтверждено.** В source-ветке `parameters` вставки пуст (нет `VALUES`), поэтому `parameters.AddRange(sourceParameters)` (`:65-66`) вливает параметры источника без переименования; провайдерный `DefaultParameterProvider` ядра не используется. Тесты `ValuesQuery_ShouldRenderInsertSelect` (5 диалектов) и интеграционный `Insert_FromQuery_ShouldPersistSelectedRows` подтверждают. Ветка `sourceParameters.Count == 0` покрыта `ValuesQuery_WithReturning_*` (источник без `WHERE`).
- **D. Пробелы покрытия (негативы/формы).** Нет тестов: member-init проекция (`new TEntity { … }`) и concrete-entity `TResult` в source-форме (есть только anonymous); `ReturningIdentity<TKey>()` (identity-функция) и `ReturningIdentity(selector)`/`ReturningKey()` в сочетании с `INSERT ... SELECT`; дубль члена в проекции; CTE-негатив только на PostgreSQL; `.Single()` на многорядном источнике (Находка 93). `InMemoryContext_ShouldRejectInsertFromQuery` покрывает только терминал `Insert()`, не `ToSql()`.
- **E. `RenderSource` аллокации холодные.** `SqlBuildContext` + `SqlBuilder` + `DefaultParameterProvider` + `DefaultAliasProvider` + `List<Parameter>` на вызов; путь мутации (не горячий SELECT), оптимизация не требуется.
- **F. `_source`/`_selectColumns` — параллельные nullable-поля.** `BuildCommand` (`:424-425`) ветвится по `_source`, но `_selectColumns` может быть `null` при непустом `_source`; тогда `MakeInsert` упадёт на `command.SourceColumns!`. Сейчас недостижимо (обе выставляются в `Values(...)` вместе), но инвариант не выражен — ср. ℹ️ A.

**Публичная сторона — `API-NAMING-REVIEW.md`, IDML18–IDML20.**

**Проверка (23.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых в фазе 6 — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`, `Thread.Sleep`/пустых `catch`/`NoWarn` — 0; `find PublicAPI*.txt` — **0** (Шаг 5); `roslyn members NextORM.Core.InsertBuilder'1` — **18** публичных методов (фаза 5 — 17; +`Values<TSource,TResult>(EntityBuilder<TSource>, Expression<…>)`), новых публичных типов — **0**; `rg "RowCount" src/nextorm.core` — потребитель только `InsertReturningBuilder.cs:164`; `wc -l` — `InsertBuilder.cs` 496, `InsertBuilder.Values.cs` 297, `SqlMutationBuilder.cs` 204, `MutationCommand.cs` 154. Сфокусированные SQL-gen тесты (Release, `--no-build`): sqlite **36**, postgres **30**, sqlserver **24**, mysql **20**, clickhouse **17**, core `InsertMetadataTests` **14** — 0 failed. Интеграция (`DOCKER_HOST` на podman-сокет): `Insert_FromQuery_ShouldPersistSelectedRows` — **4/4**, 0 skipped. Кодовая сторона — здесь; публичная — `API-NAMING-REVIEW.md`.

**Закрытие по итогам правок (23.09.2026).** Находка 93 — ✅ закрыта: `FirstOrThrow` (`InsertReturningBuilder.cs`) теперь бросает `InvalidOperationException` при `rows.Count > 1` (рантайм-проверка вместо недостижимого `RowCount > 1`), так что `.Returning(...).Single()` на многорядном `INSERT ... SELECT` падает; интеграционный тест `Insert_FromQuery_ReturningSingle_OnMultipleRows_ShouldThrow` (PostgreSQL/SQLite/SQL Server; MySQL — skip по возврату). Находка 94 — ✅ закрыта: `EnterSelectMode` удалён (вызов `EnterExclusiveMode(ValueMode.Select)`), из `ParseMapping`/`ParseSelectMapping` вынесены `EnsureUniqueColumn`/`ResolveWritableTarget`. Находка 95 — ✅ закрыта: `MakeInsert` разбит на `RenderSourceInsert`/`RenderDefaultValuesInsert`/`RenderValuesInsert` (сам `MakeInsert` — ~25 строк). Наблюдение B — ✅ исправлено (комментарий `MutationCommand` уточнён: «embed a QueryCommand as source»). Наблюдения A/C/E/F остаются. Наблюдение D — частично: `.Single()` на многорядном источнике покрыт; member-init/`ReturningIdentity`-с-источником по-прежнему без тестов.

## 🔎 Точечный аудит 23.09.2026 — data-modifying CTE (PostgreSQL): `WITH ins AS (INSERT ... RETURNING ...)`, uncommitted working tree; 🔴 — 0, 🟡 — 4, ℹ️ — 8

**Область.** Новый публичный тип `Builders/MutationCteQuery.cs` (`MutationCteQuery<TResult>`, sealed, internal ctor: `Ctes`, `With`, `WithRecursive`, `From`, `FromTable`; internal `BuildShape`, private `BuildKeyProjection`), новый extension `DataContextExtensions.With<TEntity,TResult>(this IDataContext, string, InsertReturningBuilder<TEntity,TResult>)` (`DataContextExtensions.cs:270-307`), публичный `CteDefinition.IsDataModifying` + internal ctor `(string, QueryCommand, InsertCommand)` (`Builders/CteQuery.cs:33-62`), DIM `ISqlDialect.SupportsDataModifyingCtes => false` (`DataContext/Dialect/ISqlDialect.cs:1034-1040`) + `SqlDialectBase` virtual (`:709-710`) + PG override `true` (`src/nextorm.postgres/PostgresDialect.cs:24-25`), internal `FromExpression.ColumnShape` + ctor (`Expressions/FromExpression.cs:28-40,79-85`), `SqlSourceRenderer.MakeWithClause` mutation-ветка + `RenderMutation`/`CollectMutationParams` (`:44-48,71-79,101-128`), `MakeFrom` ColumnShape-алиас/регистрация (`:313-322`), `SqlMutationBuilder.MakeInsert(..., IParameterProvider?)` (`:26-36`), `InsertReturningBuilder` internal `Projection`/`EntityType`/`ResultType`/`DataContext`/`ReturningColumns`/`BuildMutationCommand()` (`:49-65`), `QueryCommand.QueryPreparer.PrepareCtes` `Cache=false` (`:160-174`), clone/equality (`QueryCommand.Clone.cs:118-125`, `QueryPlanEqualityComparer.cs:271-297`). Тесты: PG SQL-gen ×4 (`DataModifyingCte_*`), гейты SQLite/SQL Server/MySQL/ClickHouse (`DataModifyingCte_ShouldThrowBecauseNotSupported`), core `InMemoryContext_ShouldRejectDataModifyingCte`, интеграция PG `DataModifyingCte_ShouldInsertAndReturnRows`.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фичи — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**. `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer` + `CA2254`). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в 7 `.csproj` — 0), build `0/0` ⇒ XML-doc-покрытие полное. EOL новых/изменённых `.cs` — CRLF.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `SqlSourceRenderer.MakeWithClause` держит `StringBuilderPool.Shared` в `try/finally` (`:57-98`), `SqlMutationBuilder.MakeInsert` — тоже (`:39-59`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. `QueryCommand.Clone.cs:122-124` использует `Select(...).ToList()` один раз на клонирование кэш-плана, `FromExpressionPlanEqualityComparer` — сравнение/хэш, а не обход строк. |
| 4. God-классы/методы | ✅ `MutationCteQuery` **126**, `InsertReturningBuilder` **290**, `SqlSourceRenderer` **708**, `SqlMutationBuilder` **236**; новых методов >30 строк нет (крупнейшие новые — `MakeWithClause`-ветка и `RenderMutation` ~17 строк). |
| 5. Хэш-ключи / план-кэш | 🟡 `FromExpression.ColumnShape` не входит в план-кэш equality/hash, mutation-CTE — по ссылке (Находка 96); `Cache=false` спасает и делает параметр-проход мёртвым (Находка 97). |
| 6. События / исключения | ✅ Подписок нет; новых `catch` нет; новые `throw` — `ArgumentException`/`NotSupportedException` с понятными сообщениями. |
| 7. NRT / возвратный тип | ✅ `IReadOnlyList<CteDefinition>`/`EntityBuilder<TResult>`/`EntityBuilder<TableAlias>`-аннотации корректны; `cte.Mutation!` (`SqlSourceRenderer.cs:110`) гарантирован ветвью `cte.Mutation is not null`; сборка `0/0`. Три мёртвых internal-свойства — Находка 98. |

### 🟡 Находка 96 (P2, план-кэш/корректность) — `FromExpression.ColumnShape` не участвует в план-кэш равенстве/хэше; mutation-CTE сравнивается по ссылке

**Место:** `src/nextorm.core/Expressions/FromExpressionPlanEqualityComparer.cs:54` (источник-таблица сравнивается только по имени), `:177-178` (хэш — только `obj.Table.GetHashCode()`); `src/nextorm.core/Query/QueryPlanEqualityComparer.cs:285-291` (`ReferenceEquals(a.Mutation, b.Mutation)`); `src/nextorm.core/Query/QueryCommand.Clone.cs:122-124` (клон делит живой `Mutation` по ссылке).

**Что не так.** Источник `from ins as "t1"` отличается от обычной таблицы `ins` только `ColumnShape` и принудительным алиасом (`SqlSourceRenderer.cs:313-322`), но план-кэш сравнивает два `FromExpression` равными при совпадении `Table` (`:54`), а хэш равен `Table.GetHashCode()` (`:178`) — `ColumnShape` не входит ни туда, ни туда. Тело mutation-CTE сравнивается по ссылке (`QueryPlanEqualityComparer.cs:289`), тогда как RFC-план (закрыт, удалён) требует **структурного** равенства `InsertCommand`; `CloneForCache` при этом делит живой `Mutation` с кэшируемым определением. Сегодня это не проявляется только потому, что `PrepareCtes` жёстко ставит `Cache=false` для любой команды с data-modifying CTE (`QueryCommand.QueryPreparer.cs:170-173`), поэтому план-компаратор до таких команд не доходит. Как только `Cache` окажется `true` (публичный сеттер `QueryCommand.Cache`, `QueryCommand.cs:212-217`; будущий кэш/вызов), план-идентичность источника перестанет отражать его колонки.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** учесть `ColumnShape` в `Equals`/`GetHashCode` `FromExpressionPlanEqualityComparer` (например, через `ResultType`+`SelectList`), либо явно закрепить инвариант «data-modifying CTE ⇒ `Cache=false`» (assert/`Debug.Assert`) и привести `CteDefinitionsEqual` к структурному сравнению `InsertCommand` (или задокументировать сознательный reference-вариант с обоснованием). Тест: два `With("ins", …)` с разными `Returning`-проекциями → разные планы при включённом кэше.

**Проверка:** чтение `FromExpressionPlanEqualityComparer`/`QueryPlanEqualityComparer`/`CloneForCache`; `QueryPlanner.cs:111,170` гейтят хранение/поиск плана по `queryCommand.Cache`; RFC-план закрыт (удалён).

### 🟡 Находка 97 (P2, мёртвый код) — параметр-проход mutation-CTE (`CollectMutationParams`) недостижим

**Место:** `src/nextorm.core/DataContext/SqlSourceRenderer.cs:44-48` (ветка `cte.Mutation is not null` в `ParamMode`), `:123-128` (`CollectMutationParams`); `src/nextorm.core/DataContext/QueryPlanner.cs:47-52` (`ExtractParams` — единственный вызов `MakeSelect(..., paramMode: true)`), `:199` (этот вызов только на кэш-хите).

**Что не так.** `ctx.ParamMode == true` бывает только в `QueryPlanner.ExtractParams`, а тот вызывается лишь при попадании в план-кэш (`:199`, ветка `planCache is not null`). `PrepareCtes` ставит `cmd.Cache = false` для любой команды с data-modifying CTE (`QueryCommand.QueryPreparer.cs:170-173`), поэтому такие команды никогда не сохраняются и не находятся в кэше → параметр-проход не исполняется, и `CollectMutationParams` (вместе с веткой `:44-48`) недостижим. Параметры тела на самом деле собираются в единственном SQL-проходе (`SqlSourceRenderer.cs:76-78`, `ctx.Params.AddRange(insertParams)`), так что код ещё и избыточен; комментарий «both the SQL and parameter passes number them identically» (`:73-75`) вводит в заблуждение — второго прохода для `Cache=false`-команды нет.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** удалить `CollectMutationParams` и mutation-ветку в `ParamMode`, либо — если параметр-проход для мутаций нужен по замыслу — вызывать его явно и покрыть тестом (иначе он не проверяется); поправить комментарий. Удаление безопасно: единственный путь (`Cache=false`) параметры не теряет.

**Проверка:** `rg 'ParamMode'` — `MakeSelect(..., true, ...)` только `QueryPlanner.cs:50`; `ExtractParams` — вызов только `:199` (внутри `else` кэш-хита); `QueryPlanner.cs:111,170` гейтят хранение/поиск по `queryCommand.Cache`.

### 🟡 Находка 98 (P2, мёртвые члены) — три новых internal-свойства `InsertReturningBuilder` не читаются (0 ссылок)

**Место:** `src/nextorm.core/Builders/InsertReturningBuilder.cs:56-61` (`EntityType`, `ResultType`, `DataContext` — каждое с XML-`<summary>`).

**Что не так.** `BuildShape` (`MutationCteQuery.cs:81-92`) использует только `insert.Projection` (`:54`) и `insert.ReturningColumns` (`:63`) плюс собственные generic-параметры и переданный `dataContext`; `EntityType`/`ResultType`/`DataContext` не читаются нигде: `roslyn refs` → **0** у каждого. `DataContextExtensions.With` (`:304`) тоже получает контекст отдельным параметром. Класс Находки 75 (write-only `InsertCommand.Metadata`): «мёртвый член, попавший в новую ось».

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** удалить три свойства; либо использовать их в `BuildShape` (`CreateCommand<TReturn>` → `insert.ResultType`, `SrcType` → `insert.EntityType`, убрать отдельный `IDataContext`-параметр). Поведение не меняется.

**Проверка:** roslyn `refs` — `InsertReturningBuilder.EntityType`=0, `.ResultType`=0, `.DataContext`=0; контроль — `BuildMutationCommand`=**1** (`DataContextExtensions.cs:305`), `Projection`/`ReturningColumns` читаются в `BuildShape`.

### 🟡 Находка 99 (P2, лишняя работа/асимметрия) — `With(insert)` строит и подготавливает shape-команду немедленно

**Место:** `src/nextorm.core/DataContext/DataContextExtensions.cs:294-306`; `src/nextorm.core/Builders/MutationCteQuery.cs:81-92` (`BuildShape`), `:96-106` (`BuildKeyProjection`).

**Что не так.** Публичный `With` сразу вызывает `BuildShape`, который создаёт `QueryCommand` (`dataContext.CreateCommand<TReturn>`) и **подготавливает** его (`shape.PrepareCommand(false, …)`, `:90`) — т.е. на этапе построения fluent-запроса, до исполнения и даже для `ToSql()`/если scope не используют. Соседний `With(name, query)` (read-CTE, `:270-272`) подготовку откладывает до `PrepareCtes`. `dontCalculateHash: false` заставляет посчитать хэши команды-дескриптора, которая никогда не станет планом (обёртка `Cache=false`). `BuildKeyProjection` (путь `ReturningKey<TKey>()`, когда `Projection is null`) читает `columns[0].PropertyInfo` и строит `Expression.MakeMemberAccess` — разовая рефлексия, не на строку. Путь холодный (построение запроса, не per-row), поэтому P2, а не hot-path дефект.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** отложить создание shape до `PrepareCtes` (как у read-CTE) либо передать `dontCalculateHash: true`; рефлексию оставить (один раз на scope). Замер при необходимости — `nextorm-inmemory-perf-analyst`/`nextorm-db-perf-analyst`.

**Проверка:** чтение `DataContextExtensions.cs:294-307`, `MutationCteQuery.cs:81-92,96-106`; `shape.PrepareCommand(false, …)` — единственный вызов подготовки в `BuildShape`.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `MutationCteQuery.From` — двойной поиск и недостижимый throw.** `From` сравнивает имя с `_mutationName` (`:58`), затем вызывает `FindCte(cteName)` (`:61`), чей `throw` (`:116`) из `From` недостижим (имя уже равно mutation-имени, которое всегда в `_ctes`); `FromTable` (`:72`) вызывает `FindCte` и игнорирует результат. Мелочь; можно искать индекс напрямую.
- **B. `FromTable(_mutationName)` не отклоняется.** `FromTable` проверяет только наличие имени, поэтому read-путь можно открыть на самом data-modifying CTE (`from ins` без shape). RFC закреплял `FromTable` за read-CTE (план закрыт и удалён). При желании — отклонять `_mutationName` в `FromTable`.
- **C. `MutationCteQuery.Ctes` — публичное свойство с 0 потребителями** в репозитории (зеркало `CteQuery.Ctes`, у которого 7). Для инспекции осмысленно; удалять не обязательно.
- **D. Пробелы покрытия.** Нет негативов на `From` по имени read-CTE (`ArgumentException`), на `From("missing")`, на `FromTable` по mutation-имени, на `ReturningIdentity<TKey>()` как тело (throw из `BuildKeyProjection`) и на **MariaDB** (гейт есть у SQLite/SQL Server/MySQL/ClickHouse/in-memory, у `nextorm.mariadb.tests` теста нет). Ещё один тест на `INSERT ... SELECT` как тело CTE (`RenderMutation.cs:111-112`) закрыл бы ветку.
- **E. `FromExpression.cs:85` — XML-`<summary>` приклеен к строке поля.** `internal readonly QueryCommand? ColumnShape;     /// <summary>` (нет перевода строки/пустой строки перед doc-блоком `TableFunction`). Сгенерированный `nextorm.core.xml` корректен (Roslyn трактует `///` как leading trivia), поэтому это чисто формат исходника.
- **F. `CtesPlanHash` считается и при `Cache=false`** (`QueryCommand.QueryPreparer.cs:176-188`) — хэш не используется, работа холодная; безвредно.
- **G. Перегрузка `With`.** Extension overload'ит существующий `With(IDataContext, string, QueryCommand)` (`DataContextExtensions.cs:270-272`); неоднозначности нет (`InsertReturningBuilder<TEntity,TResult>` не `QueryCommand`), но две перегрузки возвращают разные типы (`CteQuery` vs `MutationCteQuery<TResult>`) — к сведению при заморозке (публичная сторона — `API-NAMING-REVIEW.md`, наблюдение G).
- **H. EOL чист:** все новые/изменённые `.cs` — CRLF (в отличие от недавних Находок 20/22/29/51).

**Проверка (23.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`, `Thread.Sleep`/пустых `catch`/`NoWarn` — 0; `rg 'ParamMode'` — `MakeSelect(..., true, ...)` только `QueryPlanner.cs:50`, `ExtractParams` только `:199`; `roslyn refs` — `InsertReturningBuilder.EntityType/ResultType/DataContext` = **0/0/0**, `BuildMutationCommand` = 1, `CteDefinition.IsDataModifying` = 3 (не dead); `find -name 'PublicAPI*.txt'` — **0** (Шаг 5). Сфокусированные тесты (Release, `--no-build`): postgres `DataModifyingCte_*` **4/4**, SQLite/SQL Server/MySQL/ClickHouse гейты **1/1** каждый, core `InsertMetadataTests` **15/15**. `file` новых `.cs` — CRLF. Публичная сторона — `API-NAMING-REVIEW.md`, IDML21–IDML24.

## 🔎 Точечный аудит 23.09.2026 — DML `MERGE` (key upsert), uncommitted working tree; 🔴 — 0, 🟡 — 3, ℹ️ — 7

**Область.** Новый публичный билдер `src/nextorm.core/Builders/MergeBuilder.cs` (`MergeBuilder<TEntity>`, sealed, internal ctor: `Using(TEntity)`/`Using(IEnumerable<TEntity>)`, `OnKeys`, `WhenMatchedUpdate`, `WhenNotMatchedInsert`, `ToSql`, `Merge`, `MergeAsync`), новый extension `DataContextExtensions.MergeInto<TEntity>(this IDataContext, Action<EntityMetadataBuilder<TEntity>>?)` (`DataContextExtensions.cs:51`) + общий private `ResolveMetadata<TEntity>` (`:58`), internal `Query/Mutations/MergeCommand.cs` (`MergeCommand` + `SqlStatementType.Merge`), новый рендер `SqlMutationBuilder.MakeMerge`/`RenderOnConflictUpsert`/`RenderMerge`/`ColumnProperties`/`AppendValuesRows` (`SqlMutationBuilder.cs:162-329`), диспетчер `DataContext.BuildMutationSql` (`DataContext.cs`), 7 новых членов `ISqlDialect` (DIM) + `SqlDialectBase`-двойники + override'ы PG/SQLite/MySQL/SQL Server (MariaDB наследует MySQL). Тесты: `MergeBuilderTests` (3), `MergeSqlGenerationTests` ×6 провайдеров (sqlite/postgres/sqlserver/mysql/mariadb/clickhouse).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фичи — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer` + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в 7 `.csproj` — 0; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ XML-doc-покрытие полное.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `MergeBuilder` держит `IDataContext` без владения (как `InsertBuilder`) — `IDisposable` не требуется; `MakeMerge`/`RenderMerge` держат `StringBuilderPool.Shared` в `try/finally` (`:172,186-189,264-273`). `CA2000`/`CA2213`/`CA1816` не затронуты. `ImplicitUsings=enable` (`Directory.Build.props:30`) покрывает `List<>`/`Task`/`CancellationToken`; `MergeBuilder.cs` импортирует только `System.Runtime.CompilerServices` (нужен для `[OverloadResolutionPriority]`), лишних using нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет. `Using` — `entities as IReadOnlyList<TEntity> ?? entities.ToList()` один раз; значения боксятся в `object` при построении (холодно, как `InsertBuilder.Values`). |
| 4. God-классы/методы | ⚠️ `MergeBuilder.cs` 196 строк (норма); `SqlMutationBuilder.cs` 388; новые рендер-методы >30 строк (`RenderOnConflictUpsert` 41, `RenderMerge` 39, `AppendValuesRows` 45) → 🟡 Находка 101. Дублирование `MergeBuilder`↔`InsertBuilder` → 🟡 Находка 100. |
| 5. Хэш-ключи / план-кэш | ✅ `MergeBuilder`/`MergeCommand` в план-кэш не пишут; `MakeMerge` — чистый рендер. |
| 6. События / исключения | ✅ Новых подписок/`catch` нет; `InvalidOperationException`/`ArgumentException`/`NotSupportedException`/`BuildSqlCommandException` с понятными сообщениями. ℹ️ C (`SqlStatementType.Merge` пишется, не читается). |
| 7. NRT / возвратный тип | ✅ `IReadOnlyList<IPropertyMetadata>? _keys`, `IReadOnlyList<InsertColumn>` корректно аннотированы; `_metadata.TableName!` гарантирован `ResolveMetadata`; сборка `0/0`. |

### 🟡 Находка 100 (P2, DRY/дублирование) — `MergeBuilder<TEntity>` дублирует `InsertBuilder<TEntity>` почти дословно

**Место:** `src/nextorm.core/Builders/MergeBuilder.cs:51-78` (`Using(IEnumerable<TEntity>)`) ↔ `src/nextorm.core/Builders/InsertBuilder.Values.cs:108-134` (`Values(IEnumerable<TEntity>)`); `MergeBuilder.cs:131-137` (`ToSql`) ↔ `InsertBuilder.cs:151-157`; `MergeBuilder.cs:182-189` (`RequireExecutor`) ↔ `InsertBuilder.cs:457-464`; `MergeBuilder.cs:191-195` (`ColumnAccumulator`) ↔ `InsertBuilder.cs:487-491`; `MergeBuilder.cs:159-167` (сборка `InsertColumn[]` + проверка `Values.Count != _rowCount`) ↔ `InsertBuilder.cs:433-443`.

**Что не так.** Тело `Using(IEnumerable<TEntity>)` совпадает с `Values(IEnumerable<TEntity>)` построчно, кроме гейта «уже задано» (`_columns.Count > 0` vs `EnterExclusiveMode(ValueMode.Entity)`) и текста `BuildSqlCommandException` («to merge» vs «to insert»): тот же пропуск identity/computed, тот же `ColumnAccumulator`, тот же цикл `GetValue`, тот же `_rowCount`. `ToSql()` идентичен 7 строкам. `RequireExecutor()` отличается только словом «ClickHouse» в сообщении. `ColumnAccumulator` — байт-в-байт. Это ~60 строк, которые придётся править дважды (и уже пришлось: `MergeBuilder` скопирован **после** правок фаз 4-6 `InsertBuilder`). Дополнительная асимметрия: `[OverloadResolutionPriority(1)]` стоит только на `Using(TEntity)` (`MergeBuilder.cs:37`), тогда как `InsertBuilder` ставит атрибут на **обе** entity-перегрузки (`InsertBuilder.Values.cs:93,107`).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести общий каркас builder'а строк из сущностей в internal-базу/хелпер (напр. `EntityRowBuilderBase<TEntity>` с `_columns`/`_rowCount`/`BuildInsertColumns()`/`RequireExecutor()`/`ToSql()`), либо хотя бы общий `EntityRowAccumulator<TEntity>.FromEntities(...)`; `[OverloadResolutionPriority(1)]` — на обе `Using`-перегрузки (или убрать как ненужный при отсутствии конкурирующей generic-перегрузки). Публичную форму не менять. Продолжение Находок 90/94 (god-class/DRY `InsertBuilder`).

**Проверка:** построчное сравнение; `ColumnAccumulator` присутствует в обоих файлах; `ToSql`/`RequireExecutor` — в обоих; атрибут — `MergeBuilder.cs:37` (1) vs `InsertBuilder.Values.cs:93,107` (2).

### 🟡 Находка 101 (P2, длинные методы/DRY) — merge-рендеры `SqlMutationBuilder` >30 строк и повторяют insert-head

**Место:** `src/nextorm.core/DataContext/SqlMutationBuilder.cs:194-234` (`RenderOnConflictUpsert`, 41 строка), `:238-274` (`RenderMerge`, 39), `:286-329` (`AppendValuesRows`, 45); `:276-283` (`ColumnProperties`) ↔ `:128-131` (извлечение `IPropertyMetadata[]` в `RenderValuesInsert`); `:204-208` ↔ `:131-142` (голова `insert into <table> (<cols>) values`).

**Что не так.** `RenderOnConflictUpsert` заново строит голову `insert into … (…) values`, которую уже рендерит `RenderValuesInsert` (`:131-142`), плюс отдельный `ColumnProperties` дублирует `IPropertyMetadata[]`-проекцию `command.Columns` из `RenderValuesInsert` (`:128-131`). Три метода выходят за порог 30 строк. Пара `RenderMerge`/`RenderOnConflictUpsert` делит подготовку `columns`/`keys`/`updates` (`:210-225`/`:252-262`) и вызов `AppendValuesRows`.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** извлечь `RenderInsertHead(writer, dialect, quoteIdentifiers, namingConvention, table, columns, keywordCase)` (общий и для upsert-головы, и для `RenderValuesInsert`) и `AppendColumnProperties`; в `RenderMerge`/`RenderOnConflictUpsert` — общий `ResolveMergeParts` (`columns`/`keys`/`updates`). Тесты SQL-gen не меняются. Продолжение Находки 95 (длинный `MakeInsert`).

**Проверка:** границы методов по `grep -n`; чтение — дублирование головы `insert into`; `wc -l` `SqlMutationBuilder.cs` = **388**.

### 🟡 Находка 102 (P2, контракт/тихий no-op) — `OnKeys()` обязателен, но на MySQL/MariaDB ключи игнорируются; ветки — no-arg гейты

**Место:** `src/nextorm.core/Builders/MergeBuilder.cs:87-108` (`OnKeys`), `:110-124` (`WhenMatchedUpdate`/`WhenNotMatchedInsert`), `:154-157` (гейт «обе ветки вызваны»), `:169-177` (вычисление `updateColumns`); `src/nextorm.core/DataContext/SqlMutationBuilder.cs:214-225` (ветка `SupportsOnDuplicateKey` не передаёт `command.Keys` в `MakeOnDuplicateKey`).

**Что не так.** `BuildCommand` требует и `OnKeys()`, и **обе** ветки (`:154-157`), но:
- на MySQL/MariaDB `ON DUPLICATE KEY UPDATE` не имеет conflict-target, поэтому `command.Keys` в этой ветке не читается вовсе (`MakeOnDuplicateKey(KeywordCase)` ключей не принимает; тесты `Merge_Entity_ShouldRenderOnDuplicateKeyUpdate` в SQL ключа `(id)` не содержат) — обязательный вызов `OnKeys()` там молча ничего не делает;
- `WhenMatchedUpdate()`/`WhenNotMatchedInsert()` не принимают ни условия, ни списка колонок, а флаги `_whenMatchedUpdate`/`_whenNotMatchedInsert` используются **только** как гейт «обе вызваны» (`:156`) — то есть два обязательных no-arg вызова не влияют на рендер (в linq2db аналогичные точки принимают конфигурацию/условие).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** зафиксировать контракт в XML-`<remarks>`/guide: `OnKeys()` обязателен для единообразия DSL, но на MySQL/MariaDB используется unique-индекс, а не переданные ключи; ветки пока не параметризуются. Поведение не менять без решения владельца; публичная сторона — `API-NAMING-REVIEW.md`, MRG3.

**Проверка:** `MakeOnDuplicateKey` принимает только `KeywordCase`; `command.Keys` читается только в `SupportsOnConflict`/`RenderMerge`-ветках; `_whenMatched*` — запись в двух методах + чтение только `MergeBuilder.cs:156`; SQL mysql/mariadb — без `(id)`.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Per-row рефлексия/боксинг в `Using`.** `property.PropertyInfo.GetValue(list[i])` + `InsertValue.FromConstant(object)` боксят каждое значение на строку×колонку (`MergeBuilder.cs:61-68`). Путь построения команды (не per-row исполнения), но масштабируется на больших батчах; измерение — `nextorm-db-perf-analyst`/`nextorm-inmemory-perf-analyst`, оптимизация по умолчанию не требуется. `null`-элемент батча даст `TargetException` от `GetValue` без внятного сообщения (как в `InsertBuilder`).
- **B. Нет `Prepare()` из черновика RFC.** `todo_merge.md` (черновые сигнатуры) обещал `MergeBuilder.Prepare()`; в фазе 1 его нет — ок для key upsert, но при фазе 2 (источник-запрос, повторное использование) может понадобиться.
- **C. `SqlStatementType.Merge` пишется, но не читается.** `MutationCommand.StatementType` — по-прежнему write-only (`SqlStatementType.Merge` — 1 ссылка, запись в ctor `MergeCommand.cs:27`). Пре-существующий каркас, продолжение ℹ️ A фазы 7 (строка 4493 этого файла): не новая проблема.
- **D. Пробелы покрытия (важно).** По плану RFC нужен **интеграционный** upsert на каждом провайдере (`*SpecificTests.cs`), но `rg -i 'merge|upsert' tests/nextorm.integration.tests` — **0**: реальное исполнение `Merge()`/`MergeAsync()` нигде не проверяется (только `ToSql()` и отказ in-memory/ClickHouse). Нет негативов: identity/computed-ключ → `NotSupportedException` (`OnKeys`), entity только с key-колонками → `NotSupportedException` (`BuildCommand:176-177`), `BuildSqlCommandException` при рассинхроне `Values.Count`, `.MergeAsync()` (0 тестов); Uppercase покрыт только MySQL, у PG/SQL Server/SQLite — нет.
- **E. `MakeUpsertValueReference` игнорирует `keywordCase` в дефолте.** `ISqlDialect.cs:1129` и `SqlDialectBase.cs:718`: `=> "excluded." + column;` — в `Upper`-режиме `ON CONFLICT … DO UPDATE SET name = excluded.name` остаётся с нижним `excluded`, тогда как остальные клаузы казуются через `Kw`. `excluded` — имя псевдо-таблицы (идентификатор), поэтому решение TUP2 (конструкторы/имена вне политики) применимо; стоит явно задокументировать (публичная сторона — API-регистр, MRG4).
- **F. `MakeMerge`-дефолт бросает в двух местах.** DIM `ISqlDialect.MakeMerge` (`:1150`) и `SqlDialectBase.MakeMerge` (`:722`) оба `throw NotSupportedException`; диспетчер `MakeMerge` сначала проверяет `SupportsMerge`, поэтому недостижимо — контракт честный (как у `MakeForJson`).
- **G. EOL чист:** `MergeBuilder.cs`, `MergeCommand.cs`, `SqlMutationBuilder.cs` — CRLF.

**Публичная сторона — `API-NAMING-REVIEW.md`, MRG1–MRG5.**

**Проверка (23.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фичи — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `roslyn members NextORM.Core.MergeBuilder<TEntity>` — **8** публичных методов, ctor `internal`; `roslyn refs SqlStatementType.Merge` — **1** (запись); `wc -l` — `MergeBuilder.cs` 196, `SqlMutationBuilder.cs` 388, `MergeCommand.cs` 54; EOL — CRLF. Сфокусированные SQL-gen тесты: `MergeSqlGenerationTests` sqlite/postgres/sqlserver/mysql/mariadb/clickhouse + core `MergeBuilderTests`. Публичная сторона — `API-NAMING-REVIEW.md`, MRG1–MRG5.

**Закрытие по итогам правок (23.09.2026).** Находка 101 — ✅ закрыта: в `SqlMutationBuilder` вынесены общий `RenderColumns` и `ColumnProperties(IReadOnlyList<InsertColumn>)`; `RenderOnConflictUpsert`/`RenderMerge` переиспользуют их (методы сокращены, insert-head/проекция колонок больше не дублируются). Находка 102 — ✅ задокументирована: `<remarks>` `MergeBuilder<TEntity>` описывает обязательность `OnKeys()`/обеих веток и no-op переданных ключей на MySQL/MariaDB (в SQL конфликт-таргета нет). Наблюдение E — ✅ задокументировано в XML `MakeUpsertValueReference`/`SqlDialectBase`. Находка 100 — ⚠️ **принята (P2, отложена):** полноценный shared-base `MergeBuilder`↔`InsertBuilder` не выносился (риск для горячего пути `InsertBuilder`); выровнен `[OverloadResolutionPriority(1)]` на **обеих** `Using`-перегрузках. Наблюдение D — ✅ закрыто: добавлены интеграционные `CommonTestSuite.Merge.cs` (upsert новой строки, существующей строки и частичного батча) на SQLite/PostgreSQL/SQL Server/MySQL с отдельной таблицей `merge_entity` (4 провайдерные схемы), плюс юнит-негативы: identity-ключ → `NotSupportedException`, сущность только из key-колонок → `NotSupportedException`, `Using` дважды/пустой батч, `OnKeys()` без ключа и `.MergeAsync()` на in-memory. Покрытие после правок: line **85.1%** / branch **74%** (было 84.9/73.2; `MergeBuilder` 89.2%, `MergeCommand` 100%, `SqlMutationBuilder` 93.8%).

## 🔎 Точечный аудит 23.09.2026 — DML `DELETE` (фаза 1): `DeleteFrom`/`All`/`Delete`/`DeleteAsync`, uncommitted working tree; 🔴 — 0, 🟡 — 2, ℹ️ — 8

**Область.** Новый публичный билдер `src/nextorm.core/Builders/DeleteBuilder.cs` (`DeleteBuilder<TEntity>`, sealed, internal ctor `:20`: `Where(Expression<Func<TEntity,bool>>)` `:34`, `All()` `:47`, `ToSql()` `:58`, `Delete()` `:68`, `DeleteAsync(CancellationToken = default)` `:77`; internal `DeleteEntity`/`DeleteEntityAsync` `:83,89`), новый internal `src/nextorm.core/Query/Mutations/DeleteCommand.cs` (`DeleteCommand` `:31` + `DeleteKey` `:7`), extension'ы `DataContextExtensions.DeleteFrom<TEntity>` (`:81`), `Delete<TEntity>(entity)` (`:97`), `DeleteAsync<TEntity>(entity, ct)` (`:115`), новый рендер `SqlMutationBuilder.MakeDelete` (`SqlMutationBuilder.cs:202-245`), диспетчер `DataContext.BuildDeleteSql` (`DataContext.cs:358-369`), `QueryPlanner.RenderPredicate` (`QueryPlanner.cs:115-143`), новый overload `SqlSourceRenderer.MakeWhere(..., bool dontNeedAlias)` (`SqlSourceRenderer.cs:661-679`) + `SqlBuildContext.CreateWhereVisitor(..., bool dontNeedAlias = false)` (`:44`), DIM `ISqlDialect.SupportsDelete` (`:1166`) + `SqlDialectBase` (`:735`) / `ClickHouseDialect` (`:38`, `false`) override'ы. Тесты: `DeleteBuilderTests` (4), `DeleteSqlGenerationTests` ×6 провайдеров (18), `CommonTestSuite.Delete` (5 × SQLite/PostgreSQL/SQL Server/MySQL), таблица `delete_entity` в 4 провайдерных сидах.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors**. Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фичи — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же 7 `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer`-пакета + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в `.csproj` — 0; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ XML-doc-покрытие новых публичных членов полное.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `DeleteBuilder` держит `IDataContext` без владения (как `InsertBuilder`/`MergeBuilder`) — `IDisposable` не требуется; `MakeDelete` держит `StringBuilderPool.Shared` в `try/finally` (`:211,241-244`). `CA2000`/`CA2213`/`CA1816` не затронуты. `ImplicitUsings=enable` покрывает `List<>`/`Task`/`CancellationToken`; лишних using нет. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `BuildKeyCommand` обходит `_metadata.Properties` циклом (не LINQ). Значения ключей боксятся в `object` при построении команды (холодно, как `InsertBuilder.Values`/`MergeBuilder.Using`). |
| 4. God-классы/методы | ✅ `DeleteBuilder.cs` 133 строки (норма); `MakeDelete` 44 строки — формально >30, но одна линейная рендер-ветка (условный порог, ср. `RenderValuesInsert`); `BuildDeleteSql` 12. Дублирование терминалов `DeleteBuilder`↔`InsertBuilder`/`MergeBuilder` → 🟡 Находка 104. |
| 5. Хэш-ключи / план-кэш | ✅ `DeleteBuilder`/`DeleteCommand` в план-кэш не пишут; мутации минуют `QueryPlanStore`. ℹ️ B (`RenderPredicate` считает план-хэш без нужды). |
| 6. События / исключения | ✅ Новых подписок/`catch` нет; `InvalidOperationException`/`NotSupportedException` с понятными сообщениями. ℹ️ C/ℹ️ D/ℹ️ E. |
| 7. NRT / возвратный тип | ✅ `QueryCommand?`/`IReadOnlyList<DeleteKey>?`/`object?` аннотированы; `_metadata.TableName!` гарантирован `ResolveMetadata`; сборка `0/0`. |

### 🟡 Находка 103 (P2, контракт/тихий no-op) — `All()` молча игнорируется, если уже вызван `Where(...)`

**Место:** `src/nextorm.core/Builders/DeleteBuilder.cs:47-51` (`All`), `:95-105` (`BuildCommand`).

**Что не так.** `All()` выставляет `_all = true`, но `BuildCommand` строит условие как `_filter?.ToCommand()` и при непустом фильтре рендерит `WHERE`, полностью игнорируя `_all`:
- `DeleteFrom<T>().Where(x => x.Id == 1).All().Delete()` удаляет **только** строку с `Id == 1`, а не всю таблицу;
- обратный порядок `All().Where(...)` даёт тот же результат (фильтр всегда побеждает).

Один из двух взаимоисключающих маркеров молча не имеет эффекта, а порядок вызовов не проверяется. XML `<summary>` `All()` лишь замечает «(и только) значим, когда `Where` не задан», но код этого не сигнализирует во время выполнения. Тот же класс, что **Находка 102** (обязательный no-op-вызов); правила «последний вызов побеждает» (linq2db) здесь нет.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** либо бросать `InvalidOperationException` при комбинации `Where`+`All` (безопаснее для данных), либо зафиксировать «WHERE побеждает / last-wins» в `<remarks>` `All()`/`Where` и в публичном guide (EN+RU). Поведение не менять без решения владельца; публичная сторона — `API-NAMING-REVIEW.md`, DEL2.

**Проверка:** чтение `BuildCommand` — `command.Condition` непуст при любом `_filter`, `_all` в рендер не попадает; гарда нет.

### 🟡 Находка 104 (P2, DRY/дублирование) — `DeleteBuilder.ToSql()`/`RequireExecutor()` — третья дословная копия `InsertBuilder`/`MergeBuilder`

**Место:** `src/nextorm.core/Builders/DeleteBuilder.cs:58-64` (`ToSql`) ↔ `MergeBuilder.cs:139-145` ↔ `InsertBuilder.cs:151-157`; `DeleteBuilder.cs:125-132` (`RequireExecutor`) ↔ `MergeBuilder.cs:190-197` ↔ `InsertBuilder.cs:457-464`.

**Что не так.** `ToSql()` совпадает дословно (различается только аргумент `BuildCommand`: у `Insert` — `BuildCommand(null)`), а `RequireExecutor()` отличается лишь хвостом сообщения («no synchronous DELETE» vs «no DML upsert» vs insert-текст). После Находки 100 (принятой как отложенная для `MergeBuilder`↔`InsertBuilder`) появилась **третья** копия: сообщение об in-memory/ClickHouse-отказе поддерживается в трёх местах, `ToSql`-шаблон — в трёх. ~14 строк × 3.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** продолжить направление Находки 100 — общий internal-хелпер (напр. `RequireMutationExecutor(this IDataContext, string feature)` + общий `RenderOrThrow(Func<MutationCommand>)` для `ToSql`); текст отличия — параметром. Публичную форму не менять.

**Проверка:** построчное сравнение трёх файлов; `ToSql` — 7 строк идентичны; `RequireExecutor` — 8 строк с тем же `is IMutationExecutor`-паттерном.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Пробелы покрытия (важно).** Key-форма `Delete(entity)` (равенства по ключам + параметры, `SqlMutationBuilder.cs:218-233`) не имеет ни одного unit-SQL-gen-теста: `Delete<TEntity>(entity)` — терминал без `ToSql`, поэтому ветка `command.Keys` проверяется только интеграционными тестами. В SQL-gen тестах нет захваченного параметра, кроме PostgreSQL (`Delete_WhereCapturedValue_ShouldParameterise`); `Upper`-режим — только SQL Server; повторный `Where` (AND) — нигде. Все SQL-gen-тесты переиспользуют `IMergeEntity`/`merge_entity` как цель удаления (семантически чужой тип). Поведение «`Where().All()` игнорирует `All`» (Находка 103) также не покрыто.
- **B. `RenderPredicate` считает план-хэш без нужды.** `QueryPlanner.cs:117-118` вызывает `command.PrepareCommand(false, CancellationToken.None)` (`dontCalculateHash = false`, см. `QueryCommand.Prepare.cs:17-18`), тогда как для SELECT это `!storeInCache` (`:170`); мутации в `QueryPlanStore` не пишутся, поэтому `QueryPlanHash`/`CtesPlanHash` фильтра считаются на каждый delete впустую. Холодный путь; при оптимизации — `PrepareCommand(true, ...)`.
- **C. `SqlStatementType.Delete` — write-only.** `DeleteCommand.cs:40` пишет `SqlStatementType.Delete`, поле `MutationCommand.StatementType` больше нигде не читается (0 потребителей, как `Merge`/`Insert`). Пре-существующий каркас, продолжение ℹ️ C фазы MERGE.
- **D. XML-doc терминалов не объявляет `NotSupportedException`.** `Delete()`/`DeleteAsync()`/`ToSql()` (`DeleteBuilder.cs:58-81`) не имеют `<exception cref="NotSupportedException">`, хотя in-memory (и ClickHouse через `BuildDeleteSql`) его бросают; сообщения `ToSql` (`:63`) и `RequireExecutor` (`:131`) различаются. Согласовано с `InsertBuilder`/`MergeBuilder` (та же неполнота), CS1591 не нарушен — только doc-полнота.
- **E. ClickHouse-ветка в `RequireExecutor` недостижима.** `ClickHouseDataContext : DataContext` (`ClickHouseDataContext.cs:12`), а `DataContext` реализует `IMutationExecutor` (`DataContext.cs:13`), поэтому для ClickHouse `RequireExecutor()` возвращает executor, а `NotSupportedException` приходит из `BuildDeleteSql` по `SupportsDelete == false` (`DataContext.cs:360-363`). Упоминание ClickHouse в тексте `RequireExecutor` (`DeleteBuilder.cs:131`) вводит в заблуждение; фактический гейт — диалектный флаг.
- **F. `dontNeedAlias`-расширение узкое и безопасное.** `CreateWhereVisitor(..., bool dontNeedAlias = false)` сохраняет прежний дефолт; `true` передаёт только `RenderPredicate`; `PredicateTranslator.cs:123`/`AggregateFilter.cs:45`/`ArraySqlTranslator.cs:320` явно сбрасывают `DontNeedAlias = false` для вложенных визиторов, поэтому обычные SELECT/подзапросы не затронуты.
- **G. `BuildDeleteSql` — раздельные ветки condition/keys/all.** `DataContext.cs:358-369` вызывает `RenderPredicate` только при непустом `Condition`; key/all-формы идут одним вызовом `MakeDelete` без `WHERE`/с ключами. `MakeDelete` принимает и мутирует `List<Parameter>` — вызывающий владеет списком (соблюдено; две ветки взаимоисключающие, коллизий имён параметров нет).
- **H. EOL чист:** `DeleteBuilder.cs`, `DeleteCommand.cs`, `DataContextExtensions.cs`, `SqlMutationBuilder.cs`, `QueryPlanner.cs`, `SqlSourceRenderer.cs` — CRLF.

**Публичная сторона — `API-NAMING-REVIEW.md`, DEL1–DEL4.**

**Проверка (23.09.2026).** Build Release — **0/0**; подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фичи — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `roslyn members NextORM.Core.DeleteBuilder` — **5** публичных методов, ctor `internal`; `roslyn types DeleteBuilder/DeleteCommand` — 1 публичный тип + 1 internal; `wc -l` — `DeleteBuilder.cs` 133, `DeleteCommand.cs` 59, `SqlMutationBuilder.cs` 433; EOL — CRLF. Сфокусированные тесты (Release, `--no-build`): core `DeleteBuilderTests` 4/4, `DeleteSqlGenerationTests` sqlite/postgres/sqlserver/mysql/mariadb/clickhouse 18/18. Интеграция (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, Debug, filter `FullyQualifiedName~Delete_`) — **20/20, 0 skipped** (SQLite/PostgreSQL/SQL Server/MySQL × 5). Публичная сторона — `API-NAMING-REVIEW.md`, DEL1–DEL4.

**Закрытие по итогам правок (23.09.2026).** Находка 103 — ✅ закрыта: `Where`/`All` взаимно исключающие (бросают `InvalidOperationException`), покрыто `All_AfterWhere_ShouldThrow`/`Where_AfterAll_ShouldThrow`. Находка 104 — ⚠️ **принята (P2, отложена)** как и Находка 100: общий `RequireMutationExecutor`/`RenderOrThrow` не выносился (риск для горячего пути `InsertBuilder`). ℹ️ D — ✅ закрыто: `<exception cref="NotSupportedException">` добавлен на `Delete()`/`DeleteAsync()`/`ToSql()`, сообщение `RequireExecutor` больше не упоминает ClickHouse. ℹ️ A (key-форма без unit-SQL-gen) и B (`PrepareCommand(false)`) — приняты: key-форма покрыта интеграцией (`Delete_ByEntity_ShouldRemoveByKey`), `PrepareCommand(false)` не менялся. Тесты после правок: core `DeleteBuilderTests` 6/6; полный non-integration Release — 1582/0; интеграция DELETE — 20/20. Покрытие: line **85.2%** / branch **74.2%** (база 85.1/74.0, без регрессии).

## 🔎 Точечный аудит 23.09.2026 — DML `DELETE` (фаза 2): `RETURNING`/`OUTPUT`, `TRUNCATE`, in-memory-мутации, uncommitted working tree; 🔴 — 0, 🟡 — 2, ℹ️ — 10

**⛔ Отозвано/надстроено (23.09.2026, uncommitted working tree) — in-memory-мутации сняты.** Реализация in-memory `DELETE`/`TRUNCATE` **удалена**: `DataContext/InMemoryDataContext.Mutations.cs` и internal-роль `DataContext/Roles/IInMemoryMutationExecutor.cs` больше не существуют; ветки `_dataContext is IInMemoryMutationExecutor` из `DeleteBuilder`/`TruncateBuilder` сняты, исполнение снова падает в `Unsupported()` → `NotSupportedException` с текстом `"<ctx> does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB)."` (`DeleteBuilder.cs:200,208,211-213`; `TruncateBuilder.cs:44,58,64-66`). In-memory снова **query-only**: `INSERT`/`DELETE`/`TRUNCATE` вне области. Тесты `DeleteBuilderTests` — **8/8**: 7 прежних execution-тестов заменены негативами `InMemory_Delete_ShouldThrow`/`InMemory_DeleteAsync_ShouldThrow`/`InMemory_Truncate_ShouldThrow`, ожидающими `NotSupportedException`; builder-validation-тесты сохранены. `dotnet build nextorm.sln -c Debug` — **0/0**; `tests/nextorm.core.tests` — **242/242**. **Находка 105, Находка 106 и наблюдение I ниже — отозваны (withdrawn)**: их предмет (reflection-диспетчер и семантика in-memory-источника) удалён вместе с ролью; фактически открытых 🟡 в секции — **0** (счёт в заголовке — исторический). `RETURNING`/`OUTPUT`, `TRUNCATE`, `MakeDelete` и диалектные DIM'ы (наблюдения J–R) остаются в силе.

**Область.** Фаза 2 поверх фазы 1 (Находки 103–104), переименований нет. Публичный `NextORM.Core.DeleteReturningBuilder<TEntity,TResult>` (`Builders/DeleteReturningBuilder.cs`, sealed, internal ctor `:21`; терминалы `Single` `:37`, `SingleAsync` `:45`, `ToList` `:51`, `ToListAsync` `:58`, `ToSql` `:66`) и публичный `NextORM.Core.TruncateBuilder<TEntity>` (`Builders/TruncateBuilder.cs`, sealed, internal ctor `:17`; `ToSql` `:26`, `Execute` `:37`, `ExecuteAsync` `:54`). `DeleteBuilder<TEntity>` +2 публичных члена (`Returning()` `:84`, `Returning<TResult>(projection)` `:102`; всего 7) и internal `BuildReturningCommand` `:175`. `DataContextExtensions.Truncate<TEntity>` `:132`. Извлечён internal `ReturningProjection` (`Builders/ReturningProjection.cs`; `InsertReturningBuilder.ParseProjection`/`BuildSelectList` теперь делегируют, `InsertReturningBuilder.cs:199-206`). Новый internal `IInMemoryMutationExecutor` (`DataContext/Roles/IInMemoryMutationExecutor.cs`) + `InMemoryDataContext.Mutations.cs` (reflection-диспетчер + `DeleteInMemory`/`TruncateInMemory`). `TruncateCommand` (`Query/Mutations/TruncateCommand.cs`) + `SqlStatementType.Truncate` (`Query/Mutations/MutationCommand.cs:18`); `DeleteCommand.ReturningColumns` (`Query/Mutations/DeleteCommand.cs:66`). Рендер `SqlMutationBuilder.MakeDelete` (`SqlMutationBuilder.cs:202-265`)/`MakeTruncate` (`:276`)/`RenderColumnsOrNull` (`:373`). Хуки `ISqlDialect.MakeDeletedOutput` `:1085` / `SupportsTruncate` `:1177` / `MakeTruncate` `:1186` / `MakeDeleteHead` `:1196` / `DeleteRequiresWhere` `:1203` / `MakeDeleteSuffix` `:1212`, двойники `SqlDialectBase` `:703,740,743,746,749,752`; провайдеры: `SupportsTruncate` (Postgres `:40`, SqlServer `:43`, MySql `:40`, ClickHouse `:47`), ClickHouse `MakeDeleteHead` `:38`/`DeleteRequiresWhere` `:41`/`MakeDeleteSuffix` `:44` (override `SupportsDelete => false` снят). Диспетчер `DataContext.BuildReturningSql`/`BuildTruncateSql`/`BuildDeleteSql` (`DataContext.cs:337-387`). Тесты: core `DeleteBuilderTests` (10), `DeleteSqlGenerationTests` ×6 (34: sqlite 6 / postgres 10 / sqlserver 6 / mysql 5 / mariadb 4 / clickhouse 3), `CommonTestSuite.Delete` (7 тестов; integration DELETE/TRUNCATE — 29 passed / 3 skipped по прогону автора).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (проверено). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фазы 2 — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же **7** `dotnet_diagnostic.*.severity = silent` (6 `S*` инертны без `SonarAnalyzer`-пакета + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ XML-doc новых публичных членов полон (полнота `<exception>` — Находка 108).

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `DeleteReturningBuilder`/`TruncateBuilder` держат `IDataContext`/метаданные без владения (как `Insert`/`Merge`); `MakeDelete` берёт `StringBuilderPool.Shared` в `try/finally` (`SqlMutationBuilder.cs:206,261-264`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `RenderColumnsOrNull` — null-проверка, `MakeDelete` обходит ключи циклом. `InMemoryDataContext.Mutations.cs` — `source.Count()` + циклы (не LINQ); двойное перечисление источника — Находка 106. |
| 4. God-классы/методы | ⚠️ `SqlMutationBuilder.cs` 479 строк (<500 god-class-порога); `MakeDelete` (`:202-265`) **64** строки (>30) — линейная рендер-ветка (ср. `MakeInsert`); `DeleteBuilder.cs` 220, `DeleteReturningBuilder.cs` 92, `TruncateBuilder.cs` 73, `ReturningProjection.cs` 105, `InMemoryDataContext.Mutations.cs` 110. |
| 5. Хэш-ключи / план-кэш | ✅ Мутации по-прежнему минуют `QueryPlanStore`; in-memory-предикат вызывается как `GetPreparedQueryCommand(..., storeInCache: false, ...)` (`:46`) — план-кэш не засоряется. |
| 6. События / исключения | ⚠️ Новых подписок/`catch` нет; in-memory reflection-диспетчер искажает тип исключения — **Находка 105**; XML-полнота — Находка 108. |
| 7. NRT / возвратный тип | ✅ `IReadOnlyList<...>?`/`object?`/`Task<int>` аннотированы; материализуемость `TResult` проверяется `EnsureReturningMaterializable` до чтения (`DataContext.cs:321,330`); сборка `0/0`. |

### 🟡 Находка 105 (P2, контракт исключения + reflection) — reflection-диспетчер in-memory оборачивает `NotSupportedException` в `TargetInvocationException`

**⛔ Отозвана (23.09.2026).** Предмет удалён: internal-роль `IInMemoryMutationExecutor`, `InMemoryDataContext.Mutations.cs` и reflection-мост (`MakeGenericMethod`/`MethodInfo.Invoke`) больше не существуют — in-memory снова query-only, поэтому обёртка в `TargetInvocationException` недостижима. Запись оставлена как историческая.

**Место:** `src/nextorm.core/DataContext/InMemoryDataContext.Mutations.cs:14-18` (кэш `MethodInfo`), `:17-18` (`ExecuteDelete`), `:26-27` (`ExecuteTruncate`) ↔ throw-сайты `:40-42`, `:102-104`.

**Что не так.** Обе explicit-реализации `IInMemoryMutationExecutor` вызывают generic-метод через `MakeGenericMethod(command.EntityType).Invoke(this, [command])`. `MethodBase.Invoke` оборачивает **любое** исключение целевого метода в `TargetInvocationException`; поэтому при источнике, зарегистрированном в `Data`/`WithData` как не являющийся синхронным `IEnumerable<TEntity>` (async-only, iterator, произвольный объект), `DeleteBuilder.Delete()`/`DeleteAsync()` и `TruncateBuilder.Execute()`/`ExecuteAsync()` отдают `TargetInvocationException` вместо задокументированного `NotSupportedException` (`DeleteBuilder.cs:112`, `TruncateBuilder.cs:36,53`), а `InnerException` не разворачивается. Дополнительно каждый вызов аллоцирует `MakeGenericMethod` + `object[]` + бокс.

**Проверка (эмпирически).** Скретч-проба (вне репозитория): `ctx.Data[typeof(Foo)] = AsyncOnly<Foo>` → `DELETE threw: System.Reflection.TargetInvocationException; inner=System.NotSupportedException`; то же для `TRUNCATE`. Ни один тест не покрывает не-`IEnumerable<T>`-источник.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** сделать `IInMemoryMutationExecutor` generic — `int ExecuteDelete<TEntity>(DeleteCommand command)`, `Task<int> ExecuteDeleteAsync<TEntity>(DeleteCommand, CancellationToken)`, `int ExecuteTruncate<TEntity>(TruncateCommand)`, `Task<int> ExecuteTruncateAsync<TEntity>(TruncateCommand, CancellationToken)`: интерфейс **internal**, поэтому изменение безопасно; `DeleteBuilder<TEntity>`/`TruncateBuilder<TEntity>` вызывают метод со статически известным `TEntity`, а reflection-мост и `MethodInfo`-поля удаляются. Альтернатива — разворачивать: `ExceptionDispatchInfo.Capture(ex.InnerException!).Throw()`. Публичную форму не менять.

### 🟡 Находка 106 (P2, тест/док) — in-memory-семантика источника не покрыта и не задокументирована (двойное перечисление, reference-equality, подмена коллекции)

**⛔ Отозвана (23.09.2026).** In-memory-мутации удалены (роль `IInMemoryMutationExecutor` + `InMemoryDataContext.Mutations.cs`), поэтому `DeleteInMemory`/`TruncateInMemory`, двойное перечисление источника, `ReferenceEqualityComparer` и подмена `_data[typeof(TEntity)]` более не применимы; in-memory снова query-only. Запись оставлена как историческая.

**Место:** `InMemoryDataContext.Mutations.cs:35-84` (`DeleteInMemory`): запрос-материализация `:46`, повторное перечисление источника `:55-59`, `HashSet<object?>`+`ReferenceEqualityComparer.Instance` `:50`, замена `_data[typeof(TEntity)] = remaining` `:61,77,82`; `TruncateInMemory` `:107`.

**Что не так.** (1) Предикатная форма сначала материализует совпадения через полный in-memory-пайплайн (`:46`), затем **повторно** перечисляет тот же зарегистрированный `IEnumerable<TEntity>`, чтобы собрать выживших (`:55-59`); для forward-only/single-use источника (iterator, генератор) или источника, отдающего новые экземпляры на каждом перечислении, выжившие теряются/пусты, а возвращается счётчик первого прохода. (2) Удаление — по ссылке (`ReferenceEqualityComparer`), поэтому источник, чей запрос отдаёт копии (проекция), не удаляет ничего. (3) `_data[typeof(TEntity)]` заменяется на `List<TEntity>`, из-за чего коллекция, переданная вызывающим в `WithData(...)`/`Data[...]`, «осиротевает» и продолжает содержать удалённые строки. Это **принятое** проектное решение (RFC), но ни один из 10 тестов `DeleteBuilderTests` не использует источник, отличный от `List<TEntity>`, и семантика не описана в публичном guide. Проверено: `Where(x => false)` → `removed=0`, строки остаются (безопасно).

**Стало (рекомендация):** код не менять (поведение принято); добавить unit-тесты на (a) массив-источник, (b) не-`IEnumerable<T>`-источник (после фикса 105), (c) `Where(false)`/no-match, и документировать reference-equality/подмену коллекции в guide (EN+RU). Публичная сторона — `API-NAMING-REVIEW.md`, DEL5.

ℹ️ **Наблюдения (фикс не требуется).**

- **I. Роль `IInMemoryMutationExecutor`.** **⛔ Отозвано (23.09.2026):** роль и `InMemoryDataContext.Mutations.cs` удалены, in-memory снова query-only (`INSERT`/`DELETE`/`TRUNCATE` вне области); отделять её от `IMutationExecutor` больше нечего, рефлексия-моста (Находка 105) нет. Запись оставлена как историческая.
- **J. `MakeDelete` — длинный метод.** `SqlMutationBuilder.cs:202-265` (64 строки, >30) — линейный рендер (голова → OUTPUT → WHERE/ключи → RETURNING → суффикс), ср. `MakeInsert`; `SqlMutationBuilder.cs` 479 строк (<500).
- **K. DRY: `MakeDeletedOutput`.** `ISqlDialect.MakeDeletedOutput` (`:1085`) и `SqlDialectBase.MakeDeletedOutput` (`:703`) — копия `MakeOutput` (`:1080`/`:701`) с заменой литерала `inserted.`→`deleted.`; семейство Находки 104.
- **L. DRY: guard «нет Where/All».** `DeleteBuilder.BuildCommand` (`:142-152`) и `BuildReturningCommand` (`:175-182`) дублируют один и тот же guard; семейство Находки 104.
- **M. `Returning()` строит identity-лямбду на каждый вызов.** `DeleteBuilder.cs:86-88` (`Expression.Parameter`+`Expression.Lambda`) — холодный путь создания билдера; можно вынести в `static readonly` Expression.
- **N. `dontNeedAlias`-расширение узкое.** `SqlSourceRenderer.MakeWhere(..., bool dontNeedAlias)` (`:661-679`) и `SqlBuildContext.CreateWhereVisitor(..., bool dontNeedAlias = false)` (`:44`) сохраняют прежний дефолт; `true` передаёт только `QueryPlanner.RenderPredicate` (`:139`); продолжение ℹ️ F фазы 1.
- **O. `SqlStatementType.Truncate` — write-only.** `MutationCommand.cs:18` пишет значение, `MutationCommand.StatementType` не читается (как `Delete`/`Merge`); продолжение ℹ️ C.
- **P. `SupportsDelete` больше не override'ится ClickHouse.** Override `=> false` снят (`grep` — 0); ClickHouse наследует `true` и выражает delete через `MakeDeleteHead`/`DeleteRequiresWhere`/`MakeDeleteSuffix`; фактический гейт — диалектные хуки, а не флаг.
- **Q. Публичная поверхность расширена 6 DIM'ами + 2 типами + extension'ом.** Публичная сторона — DEL6/DEL7 в `API-NAMING-REVIEW.md`.
- **R. EOL чист.** Все файлы фазы 2 — CRLF (`grep -c $'\r'` = число строк).

**Публичная сторона — `API-NAMING-REVIEW.md`, DEL5–DEL8.**

**Проверка (23.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0** (проверено); подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фазы 2 — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `roslyn members` — `DeleteReturningBuilder<TEntity,TResult>` **5** публичных методов + internal ctor, `TruncateBuilder<TEntity>` **3** + internal ctor, `DeleteBuilder<TEntity>` **7**, `ISqlDialect` содержит 7 новых членов (`SupportsDelete` `:1170`, `SupportsTruncate` `:1177`, `MakeTruncate` `:1186`, `MakeDeleteHead` `:1196`, `DeleteRequiresWhere` `:1203`, `MakeDeleteSuffix` `:1212`, `MakeDeletedOutput` `:1085`); `wc -l` — `SqlMutationBuilder.cs` 479, `DeleteBuilder.cs` 220, `InMemoryDataContext.Mutations.cs` 110; EOL — CRLF. Скретч-пробы: reflection-обёртка (105) и `Where(false)`→`where 0`/`removed=0`, `Where(true)`→`where 1`, ClickHouse-`Returning`→`NotSupportedException`, SQLite-`Truncate`→`NotSupportedException` (106). Сфокусированные тесты (Release, `--no-build`): core `DeleteBuilderTests` **10/10**; `DeleteSqlGenerationTests` **34/34** (sqlite 6 / postgres 10 / sqlserver 6 / mysql 5 / mariadb 4 / clickhouse 3). Интеграция (`DOCKER_HOST`, Debug) — **29 passed / 3 skipped** по прогону автора (MySQL returning ×2, SQLite truncate ×1); аудитом не перезапускалась. Публичная сторона — `API-NAMING-REVIEW.md`, DEL5–DEL8.



## 🔎 Точечный аудит 23.09.2026 — multi-table DELETE (join, фаза 3): `DELETE ... USING` / `DELETE <alias> FROM ... JOIN`, uncommitted working tree; 🔴 — 0, 🟡 — 2, ℹ️ — 7

**Область.** Фаза 3 поверх фаз 1–2 (Находки 103–106). Новый internal `NextORM.Core.DeleteJoinCommand` (`Query/Mutations/DeleteJoinCommand.cs:9`, `sealed`, ctor `:14`) + `SqlStatementType.DeleteJoin` (`MutationCommand.cs:17`). Рендер: `SqlBuilder.MakeDeleteJoin` (`SqlBuilder.cs:540-625`), `QueryPlanner.RenderDeleteJoin` (`QueryPlanner.cs:148-171`), `DataContext.BuildDeleteJoinSql` (`DataContext.cs:390-397`), `SqlSourceRenderer.MakeJoinParts` (`SqlSourceRenderer.cs:251-282`) + новый out-перегруз `MakeFrom(..., out string? alias)` (`:330`) и хелперы `JoinNeedsScope` `:286`/`JoinDimension` `:303`. Диалекты: `ISqlDialect.SupportsDeleteJoin` `:1220` / `MakeDeleteJoin` `:1238` / `DeleteJoinUsesUsing` `:1252`, `SqlDialectBase` `:755,761,775`, `PostgresDialect` `:43,46,53`, `SqlServerDialect:46`, `MySqlDialect:43` (MariaDB наследует). Публичная сторона (18 extension'ов + 6 `Where`) — `DataContextExtensions.cs:135-267`, `JoinedEntityBuilder.cs:120,226,326,426,526,626`; гейт не-INNER — `DataContextExtensions.RequireInnerJoinsForDelete` `:280-291`. Тесты: `DeleteJoinBuilderTests` (7), `DeleteSqlGenerationTests` ×6 (10), интеграция `CommonTestSuite.Delete` (1).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (проверено). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фазы 3 — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же 7 inert `dotnet_diagnostic.*.severity = silent` (6 `S*` без `SonarAnalyzer`-пакета + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0`.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `DeleteJoinCommand` держит `QueryCommand Source` без владения (как `MutationCommand`-соседи); `MakeDeleteJoin` берёт 4 `StringBuilderPool.Shared` и возвращает их в `finally` (`SqlBuilder.cs:559-619`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `RequireInnerJoinsForDelete` и сборка `using`/`joinConditions` — циклы; `PrepareDeleteJoinSource` аллоцирует `Select`/`QueryDefinition` один раз на терминал (холодно). |
| 4. God-классы/методы | ⚠️ `SqlBuilder.MakeDeleteJoin` — **86** строк (`:540-625`, >30) — линейный рендер (4 буфера + ветвление USING/alias); `MakeJoinParts` 32; `SqlSourceRenderer.cs` 796, `JoinedEntityBuilder.cs` 677 (<500). Дублирование `MakeJoinParts`↔`MakeJoin` — Находка 107. |
| 5. Хэш-ключи / план-кэш | ✅ `DeleteJoinCommand` минует `QueryPlanStore`; `RenderDeleteJoin` (`QueryPlanner.cs:152`) вызывает `source.PrepareCommand(false, …)` — план-хэш считается, но мутации в сторе не пишутся; продолжение ℹ️ B фазы 1 (холодный путь). |
| 6. События / исключения | ⚠️ Новых подписок/`catch` нет; сообщения понятные, но USING-ветка теряет часть валидации — Находка 108; несогласованный тип исключения — ℹ️ A. |
| 7. NRT / возвратный тип | ✅ `out string? alias` (`SqlSourceRenderer.cs:330`), `QueryCommand?`, `List<Parameter>` аннотированы; `(string Sql, List<Parameter> Parameters)` возвращается без null; сборка `0/0`. |

### 🟡 Находка 107 (P2, DRY/дублирование) — `MakeJoinParts` — копия сборочного ядра `MakeJoin`

**Место:** `src/nextorm.core/DataContext/SqlSourceRenderer.cs:251-282` (`MakeJoinParts`) ↔ `:154-243` (`MakeJoin`); общие хелперы `JoinNeedsScope` `:286`, `JoinDimension` `:303`.

**Что не так.** `MakeJoinParts` повторяет сборочное ядро `MakeJoin` один-в-один: (1) `var scopedAdded = JoinNeedsScope(condition.Parameters); if (scopedAdded) ctx.ColumnsProvider.PushScope(...)` + парный `PopScope` в `finally` (`:256-281` ↔ `:213-234`); (2) `MakeFrom(in ctx, join.From, new FromRenderOptions(true, condition.Parameters[1].Type, false))` (`:262` ↔ `:221`); (3) `MakeWhere(in ctx, target, entityType, condition.Body, JoinDimension(condition))` (`:268` ↔ `:228`). Отличие — только `MakeJoin` дополнительно печатает join-keyword/`" on "` и **валидирует** join (`:156-187`), а `MakeJoinParts` возвращает `from`/`condition` раздельно (USING-форма). Копии уже разошлись: проверки `MakeJoin` в `MakeJoinParts` отсутствуют (Находка 108) — классический риск рассинхронизации. Семейство Находок 100/104 (принятые отложенные).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести общее ядро, напр. `private static (string From, string Condition) RenderJoinCore(in SqlBuildContext ctx, JoinExpression join, Type entityType, StringBuilder? conditionTarget)`; `MakeJoin` после валидации собирает `keyword + from + " on " + Condition`, `MakeJoinParts` возвращает `(from, Condition)`. Один сборочный путь, одна валидация. Публичную форму не менять.

**Проверка:** построчное сравнение `:251-282` и `:154-243` — блоки scoping/MakeFrom/MakeWhere совпадают; `MakeJoinParts` имеет 1 потребителя (`SqlBuilder.cs:580`); `MakeJoin` — `SqlBuilder.cs:596`.

### 🟡 Находка 108 (P2, консистентность — тихая потеря join-модификатора) — USING-ветка не валидирует `Strictness`/`IsGlobal`

**Место:** `src/nextorm.core/DataContext/SqlSourceRenderer.cs:251-282` (`MakeJoinParts`, проверок нет) → `SqlBuilder.cs:580` (USING-ветка); сравнить с `SqlSourceRenderer.cs:176-184` (`MakeJoin`, проверки есть) → `SqlBuilder.cs:596` (alias-ветка); гейт `DataContextExtensions.cs:280-291` (`RequireInnerJoinsForDelete`).

**Что не так.** `RequireInnerJoinsForDelete` проверяет только `JoinType`, но не `Strictness`/`IsGlobal`. `MakeJoin` затем отклоняет `join.Strictness != Default` при `!SupportsJoinStrictness` и `join.IsGlobal` при `!SupportsGlobalJoin` (оба дефолта `false` у всех диалектов). `MakeJoinParts` этих проверок не выполняет и просто рендерит `MakeFrom`/`MakeWhere`, игнорируя модификатор. Поэтому на PostgreSQL (USING-форма) `ctx.From<A>().Join(b, …).WithStrictness(JoinStrictness.Any).Delete()` молча рендерит обычный `INNER JOIN`, тогда как на SQL Server/MySQL/MariaDB (alias-форма) тот же запрос бросает `NotSupportedException` из `MakeJoin`. Один и тот же API даёт «ошибку» на одних провайдерах и «тихо другое удаление» на других. Модификаторы достижимы: `JoinedEntityBuilder<T1..Tn>` даёт `new Global`/`WithStrictness`, возвращающие тот же joined-тип (`JoinedEntityBuilder.cs:106-110,214-218,314-318,414-418,514-518,614-618`).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** переиспользовать валидацию `MakeJoin` через общее ядро Находки 107, либо продублировать 3 проверки в `MakeJoinParts`, либо расширить `RequireInnerJoinsForDelete` до `Strictness != Default`/`IsGlobal`. Публичную форму не менять.

**Проверка:** кодовое чтение — `MakeJoinParts:251-282` не содержит `SupportsJoinStrictness`/`SupportsGlobalJoin`; `MakeJoin:176-184` содержит; `RequireInnerJoinsForDelete:285-290` читает только `JoinType`. Эмпирическая скретч-проба не выполнялась: правила доступа запрещают создавать файлы вне двух реестров — сравнение alias/USING сделано по коду; тест на PG-USING + strictness отсутствует.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Несогласованный тип исключения.** `MakeJoinParts` при `JoinCondition is null` бросает `BuildSqlCommandException` с текстом «only supports INNER joins…» (`SqlSourceRenderer.cs:253-254`), тогда как все внешние гейты бросают `NotSupportedException` (`DataContextExtensions.cs:288`, `SqlBuilder.cs:543`, `DataContext.cs:393`). Ветка после гейта недостижима, но тип/сообщение рассогласованы.
- **B. Мёртвая работа в alias-ветке.** `SqlBuilder.cs:574` безусловно рендерит неалиасированный `target`, но его использует только USING-ветка (Postgres `MakeDeleteJoin`); alias-ветка (`:594`) берёт `targetSql`. Проверено: неалиасированный физический `MakeFrom` не вызывает `ColumnsProvider.Add` и не расходует alias (`SqlSourceRenderer.cs:392-403`, `needAlias=false`/`ColumnShape=null`), т.е. это лишь лишняя работа — перенести внутрь `if (DeleteJoinUsesUsing)`.
- **C. `SqlStatementType.DeleteJoin` — write-only.** `MutationCommand.cs:17` пишет значение, `MutationCommand.StatementType` нигде не читается (0 потребителей, как `Delete`/`Merge`/`Truncate`); продолжение ℹ️ C.
- **D. Четыре буфера на один рендер.** `MakeDeleteJoin` арендует `fromAndJoins`/`usingSources`/`joinConditions`/`whereSql` всегда, хотя alias-ветка не использует `usingSources`/`joinConditions`, а USING — `fromAndJoins`. Холодный путь; при желании — аренда по ветке.
- **E. `PrepareDeleteJoinSource` строит фиктивную проекцию.** `query.Select(p => new { Unit = 1 })` + `IgnoreColumns = true` (`DataContextExtensions.cs:269-278`) нужны лишь чтобы получить подготовленный `QueryCommand`; можно было бы `ToCommand()` без dummy-проекции. Холодный путь.
- **F. Пробелы покрытия.** Нет SQL-gen-теста `.Where` на арности ≥3 (PG 3-table тест — без user-Where), нет теста `WithStrictness`/`Global` при delete (Находка 108), нет PG-USING с `CreateQuoted()`, нет негативов `CrossJoin`/`SemiJoin`/`CrossApply`/`OuterApply` (только `LeftJoin`, `DeleteJoinBuilderTests.cs:37`). Позитив: `DeleteJoin_WhereCapturedValue_ShouldParameterise` (PG), `DeleteJoin_ShouldRemoveRowsMatchingAnotherTable` (интеграция).
- **G. EOL/подавления чисты.** Все файлы фазы 3 — CRLF; новых подавлений/слопа нет.

**Публичная сторона — `API-NAMING-REVIEW.md`, DEL9–DEL14 (P1 — DEL9; P2 — DEL10–DEL14).**

**Проверка (23.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0** (проверено); подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фазы 3 — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `grep` `new JoinedEntityBuilder<...> Where` — 6 (`JoinedEntityBuilder.cs:120,226,326,426,526,626`); `wc -l` — `SqlBuilder.cs` 747, `SqlSourceRenderer.cs` 796, `JoinedEntityBuilder.cs` 677, `DataContextExtensions.cs` 599, `DeleteJoinCommand.cs` 22; EOL — CRLF. Сфокусированные тесты (Release, `--no-build`, `FullyQualifiedName~DeleteJoin`) — **17/17, 0 skipped** (core 7 / postgres 4 / sqlserver 2 / mysql 1 / mariadb 1 / sqlite 1 / clickhouse 1). Интеграция (`DOCKER_HOST`) не перезапускалась аудитом. Публичная сторона — `API-NAMING-REVIEW.md`, DEL9–DEL14.

## 🔎 Точечный аудит 23.09.2026 — DML `UPDATE`: `Update<T>()`/`Update(entity)`/`UpdateAsync(entity)` + `UpdateBuilder<TEntity>`, uncommitted working tree; 🔴 — 0, 🟡 — 4, ℹ️ — 8

**Область.** Новый публичный `NextORM.Core.UpdateBuilder<TEntity>` (`src/nextorm.core/Builders/UpdateBuilder.cs`, `sealed`, internal ctor `:26`; 7 публичных членов: `Set<TValue>(Expression<Func<TEntity,TValue>>, TValue)` `:44`, `Set<TValue>(Expression<Func<TEntity,TValue>>, Expression<Func<TEntity,TValue>>)` `:64`, `Set(TEntity)` `:81`, `Where(Expression<Func<TEntity,bool>>)` `:104`, `ToSql()` `:119`, `Update()` `:131`, `UpdateAsync(CancellationToken = default)` `:139`; internal `UpdateEntity`/`UpdateEntityAsync` `:142,148`). Новые internal `UpdateCommand`/`UpdateAssignment`/`UpdateValueKind` (`Query/Mutations/UpdateCommand.cs:69,21,6`). Извлечён internal `KeyValue` (`Query/Mutations/KeyValue.cs:7`), теперь общий для DELETE и UPDATE (`DeleteCommand.cs:18,38`). Extension'ы `DataContextExtensions.Update<TEntity>` `:69`, `Update<TEntity>(entity)` `:86`, `UpdateAsync<TEntity>(entity, ct)` `:104`. Рендер `SqlMutationBuilder.MakeUpdate` (`:284-332`, 49 строк), `QueryPlanner.RenderAssignments` (`:155-214`, 60 строк) + перегруз `RenderPredicate(QueryCommand, IParameterProvider, List<Parameter>)` (`:120-148`), диспетчер `DataContext.BuildUpdateSql` (`:357-378`) + ветка `BuildMutationSql` `:384`. Диалект: DIM `ISqlDialect.SupportsUpdate` `:1260` + `MakeUpdateHead` `:1270`, двойники `SqlDialectBase` `:778,781`, `ClickHouseDialect.SupportsUpdate => false` `:47`. Тесты: core `UpdateBuilderTests` (4), `UpdateSqlGenerationTests` ×6 (37), интеграция `CommonTestSuite.Update` (5).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (проверено). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах UPDATE — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же **7** inert `dotnet_diagnostic.*.severity = silent` (6 `S*` без `SonarAnalyzer`-пакета + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ XML-doc новых публичных членов полон.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Корректно. Колоночный визитор в `RenderAssignments` (`QueryPlanner.cs:204-208`) взят в `using`; `BaseExpressionVisitor` (`Visitors/BaseExpressionVisitor.cs:15`) арендует pooled `StringBuilder` в ctor (`:55`) и возвращает его в `Dispose` (`:509-510`) — `using` обязателен и совпадает с паттерном `SqlSourceRenderer.cs:621,721,750,760,770`. `MakeUpdate` берёт `StringBuilderPool.Shared` в `try/finally` (`:295,328-331`). Новых disposable-полей у билдеров нет (`IDataContext` без владения). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет; `Set(entity)`/`BuildEntityCommand` обходят `_metadata.Properties` циклами; `RenderAssignments` обходит `Assignments`. Значения боксятся в `object` при построении команды (холодно, как `InsertBuilder`/`DeleteBuilder`). |
| 4. God-классы/методы | ⚠️ `SqlMutationBuilder.cs` **546** строк — перешёл порог >500 (**Находка 110**); `RenderAssignments` **60**, `MakeUpdate` **49** — линейные рендеры (>30, ср. `MakeDelete` 64 / `MakeInsert` 119); `UpdateBuilder.cs` 275, `UpdateCommand.cs` 102. Дублирование терминалов — ℹ️ E (Находка 104). |
| 5. Хэш-ключи / план-кэш | ✅ `UpdateBuilder`/`UpdateCommand` в план-кэш не пишут; мутации минуют `QueryPlanStore`. ℹ️ B (`PrepareCommand(false)` считает план-хэш зря, продолжение ℹ️ B DELETE-фазы). |
| 6. События / исключения | ✅ Новых подписок/`catch` нет; `InvalidOperationException`/`NotSupportedException` с понятными сообщениями; контрактный дефект `Set` — **Находка 109**; неполный `<exception>` — ℹ️ H. |
| 7. NRT / возвратный тип | ✅ `QueryCommand`/`IReadOnlyList<KeyValue>?`/`object?`/`Expression?` аннотированы; `_metadata.TableName!` гарантирован `ResolveMetadata`; сборка `0/0`. |

### 🟡 Находка 109 (P2, корректность/контракт) — `Set(column, expr)` принимает захваченное/статическое **свойство** за ссылку на колонку и бросает ложное «not mapped»

**Место:** `src/nextorm.core/Builders/UpdateBuilder.cs:196-214` (`BuildAssignment`; классификация — `:200-205`), `:232-241` (`FindProperty`); XML-обещание — `:52-58` (комментарий `<summary>` expression-перегруза).

**Что не так.** `BuildAssignment` классифицирует RHS как ссылку на колонку по факту «тело — `MemberExpression` с `PropertyInfo`», **не проверяя**, чей это член:
```
if (body is MemberExpression { Member: PropertyInfo valueProperty })   // :200
{
    var source = FindProperty(valueProperty) ?? throw BuildSqlCommandException(...); // :202-203
    return UpdateAssignment.FromColumn(property, source);
}
```
`FindProperty` ищет совпадение только среди `_metadata.Properties` сущности. Поэтому любой доступ к **свойству захваченного объекта** (`x => config.Value`) или к **статическому свойству** (`x => Config.Default`) попадает в эту ветку, не находится и падает `BuildSqlCommandException("Property {Name} of {TEntity} is not mapped.")`. Это прямо противоречит XML-doc (`:56-57`): «captured variables (which become parameters)», и расходится с SELECT-пайплайном, который такой узел (цель — не параметр лямбды) трактует как захваченный константный параметр. Обходной путь есть — constant-перегруз `Set(x => x.Name, config.Value)` (значение вычисляется до вызова), но expression-перегруз молча отвергает валидное выражение с вводящим в заблуждение сообщением.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** классифицировать как `FromColumn` только когда цепочка члена **укоренена в параметре лямбды** и `valueProperty.DeclaringType` — целевая сущность, напр.
```
if (body is MemberExpression { Expression: ParameterExpression, Member: PropertyInfo p } && p.DeclaringType == typeof(TEntity))
    return UpdateAssignment.FromColumn(...);
```
иначе проваливаться в существующий constant-fold-путь (`!body.Has<ParameterExpression>()` → `Expression.Lambda<Func<object?>>(...).Compile()()`), как это делает SELECT. Публичную форму не менять.

**Проверка (эмпирически, file-based probe вне репозитория, `InMemoryDataContext`).** `Set(x => x.Name, x => holder.Name)` → `BuildSqlCommandException: Property Name of Foo is not mapped.`; `Set(x => x.Name, x => Config.Default)` → `BuildSqlCommandException: Property Default of Foo is not mapped.`; при этом `Set(x => x.Name, x => x.Name)` (`FromColumn`) и constant-перегруз `Set(x => x.Name, holder.Name)` — без исключения. Существующие тесты (`Update_SetColumn_ShouldRenderColumnReference`, `Update_SetExpressionCapturedValue_ShouldParameterise`) этот путь не покрывают — захвачен **локальный**, а не член.

### 🟡 Находка 110 (P2, god class) — `SqlMutationBuilder.cs` перешёл порог 500 строк

**Место:** `src/nextorm.core/DataContext/SqlMutationBuilder.cs` — **546** строк (было 479 в фазе DELETE 2); добавление `MakeUpdate` (`:284-332`) вывело файл за порог «god class >500» из `dotnet-csharp-code-smells` §5.

**Что не так.** Класс накопил 5 публичных точки входа (`MakeInsert` `:28`, `MakeMerge` `:159`, `MakeDelete` `:202`, `MakeUpdate` `:284`, `MakeTruncate` `:343`) и ~12 приватных хелперов (`RenderSourceInsert`, `RenderDefaultValuesInsert`, `RenderValuesInsert`, `RenderOnConflictUpsert`, `RenderMerge`, `AppendValuesRows`, `AppendColumnList`, …) для четырёх разных инструкций. Ответственность всё ещё «рендер DML», но одна точка изменения (напр. квотирование, `MakeParam`) затрагивает 500+ строк.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** разделить по инструкциям (напр. `InsertRenderer`/`MergeRenderer`/`DeleteRenderer`/`UpdateRenderer`), общие `ResolveTableName`/`RenderColumnReference`/`AppendValuesRows` — в общий helper/base. Публичную форму не менять. Дефекта нет — вопрос размера/сопровождаемости.

### 🟡 Находка 111 (P2, DRY/дублирование) — `MakeUpdate` дублирует key-equality `WHERE`-цикл `MakeDelete`

**Место:** `SqlMutationBuilder.cs:306-320` (`MakeUpdate`, ключи) ↔ `:228-243` (`MakeDelete`, ключи).

**Что не так.** Обе ветки дословно повторяют один рендер: `where ` → цикл по ключам с разделителем `and` `:235-241`/`:313-319` → `RenderColumnReference` → `provider.GetParamName()` → `new Parameter(name, keys[i].Value)` → `column = MakeParam(name)`. Отличие только в источнике провайдера (`MakeDelete` создаёт свой `DefaultParameterProvider` `:230`, `MakeUpdate` получает общий `:316`). `KeyValue` был извлечён именно чтобы **поделить** DELETE и UPDATE — но поделён только тип модели, а не рендер-цикл; при правке формата равенств (напр. NULL-safe) нужно менять два места. Семейство Находок 104/107 (принятые/открытые дефолты).

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести `AppendKeyEqualities(StringBuilder, ISqlDialect, bool quoteIdentifiers, INamingConvention?, IReadOnlyList<KeyValue>, IParameterProvider, List<Parameter>, KeywordCase)`; `MakeDelete` передаёт свежий провайдер, `MakeUpdate` — общий. Публичную форму не менять.

**Проверка:** построчное сравнение `:228-243` и `:306-320` — совпадают все 13 строк тела цикла, кроме объявления провайдера.

### 🟡 Находка 112 (P2, EOL) — все 11 новых файлов UPDATE записаны с LF-концевиками

**Место:** новые (untracked) файлы: `src/nextorm.core/Builders/UpdateBuilder.cs` (275 строк, `crlf=0`), `src/nextorm.core/Query/Mutations/KeyValue.cs` (23, 0), `src/nextorm.core/Query/Mutations/UpdateCommand.cs` (102, 0), `tests/nextorm.core.tests/UpdateBuilderTests.cs` (51, 0), `tests/nextorm.integration.tests/CommonTestSuite.Update.cs` (97, 0), `tests/nextorm.clickhouse.tests/UpdateSqlGenerationTests.cs` (31, 0), `tests/nextorm.mariadb.tests/UpdateSqlGenerationTests.cs` (79, 0), `tests/nextorm.mysql.tests/UpdateSqlGenerationTests.cs` (79, 0), `tests/nextorm.postgres.tests/UpdateSqlGenerationTests.cs` (140, 0), `tests/nextorm.sqlite.tests/UpdateSqlGenerationTests.cs` (79, 0), `tests/nextorm.sqlserver.tests/UpdateSqlGenerationTests.cs` (79, 0) — **все LF-only**.

**Что не так.** AGENTS.md («Line endings») требует CRLF (`core.autocrlf=true`). Все изменённые (отслеживаемые) файлы фазы — CRLF (`UpdateBuilder`-диффы, `DataContext.cs`, `QueryPlanner.cs`, `SqlMutationBuilder.cs`, `DataContextExtensions.cs`, `ISqlDialect.cs`, `SqlDialectBase.cs`, `ClickHouseDialect.cs`, `DeleteCommand.cs`, `DeleteBuilder.cs` — у каждого `crlf=total`). LF-only только у новых. Тот же класс, что **Находки 20/22/29/51** (повторяющийся дефект генерации новых файлов); смешанных EOL нет.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** нормализовать 11 новых файлов в CRLF (`perl -pi -e 's/\r?\n/\r\n/g' <файл>`), как предписано AGENTS.md, и добавить `.gitattributes`-гард `* text=auto eol=crlf` — дефект рекуррентный.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `UpdateCommand.Source` — двойная роль, функционально корректна.** `Source` (`UpdateCommand.cs:98`, non-nullable) — это тот же `QueryCommand`, построенный как `(_filter ?? From<TEntity>()).ToCommand()` (`UpdateBuilder.cs:178`); он служит (1) носителем `PreparedCondition` для `WHERE` (`QueryPlanner.cs:126`) и (2) `QueryProvider`/источником `ResolvedQuoteIdentifiers`/`ResolvedNamingConvention`/`ResolvedKeywordCase` для `SET`-выражений (`QueryPlanner.cs:160-180`). Роли совместимы: `RenderAssignments` заводит `ColumnsProvider` и явно регистрирует `command.EntityType` (`:181`), поэтому колонки сущности резолвятся независимо; `QueryProvider` нужен только для RHS-подзапросов. Конфликта нет. Два замечания на будущее: имя `Source` расходится с `DeleteCommand.Condition` (тот же смысл, но nullable для форм `All()`/keys) — при желании переименовать в `Condition`/`Filter`; и non-nullable оправдан, т.к. UPDATE всегда имеет команду-носитель. Баг-репорта нет.
- **B. Общий `IParameterProvider` между SET/WHERE/keys — корректен.** В `BuildUpdateSql` (`DataContext.cs:365-377`) один `DefaultParameterProvider` и один `List<Parameter>` идут в `RenderAssignments` и `RenderPredicate`; `MakeUpdate` продолжает ту же нумерацию для key-равенств (`:316`). SET-константы получают `@p0..`, predicate-захваченные значения сохраняют member-based имена SELECT-пайплайна (`@Id`, `@increment`), key-равенства продолжают `@pN` без рестарта. Формы keys/where **взаимоисключающие** (`Update(entity)` → keys, `Set(...).Where(...)` → predicate), поэтому коллизий нет. Подтверждено тестами PG (`... set name = @p0, age = @p1 where id = @Id` и `... where id = 1`) и интеграцией (20/20). ✅
- **C. `IDisposable` колоночного визитора — корректно.** См. таблицу, строка 1: pooled `StringBuilder` возвращается в `Dispose`, `using` обязателен и совпадает с `SqlSourceRenderer`. Не smell.
- **D. Длинные методы без нового класса.** `QueryPlanner.RenderAssignments` 60 строк (цикл + 3-веточный `switch` + `using`-визитор) и `SqlMutationBuilder.MakeUpdate` 49 — линейные рендеры (>30), ср. `MakeDelete` 64 / `MakeInsert` 119; отдельного фикса не требуют (в отличие от пересечения порога файлом — Находка 110).
- **E. Дублирование терминалов — 4-я копия.** `UpdateBuilder.ToSql`/`RequireExecutor`/`Unsupported`/`Execute`/`ExecuteAsync` (`:119-270`) дословно повторяют семейство `InsertBuilder`/`MergeBuilder`/`DeleteBuilder`; это **актуализация Находки 104** (была «третья копия»), а не новая находка. Направление фикса — общий `RequireMutationExecutor`/`RenderOrThrow`.
- **F. Пробелы покрытия.** Key-форма `Update(entity)` не имеет `ToSql()` (SQL-gen-ветка `command.Keys` проверяется только интеграцией — как ℹ️ A DELETE-фазы); нет unit-теста на expression-RHS **без** `Where`; нет теста на RHS-подзапрос (путь `QueryProvider = Source`, ℹ️ A); нет теста на захваченное свойство (Находка 109); `Set(entity)` покрыт по ключу/компьютед, но не по исключению identity/computed.
- **G. `SqlStatementType.Update` — write-only.** `UpdateCommand.cs:79` пишет значение, `MutationCommand.StatementType` не имеет читателей (0 потребителей, как `Delete`/`Merge`/`Truncate`/`DeleteJoin`); продолжение ℹ️ C.
- **H. `<exception>`-полнота расширений.** `Update<TEntity>(entity)`/`UpdateAsync<TEntity>(entity)` (`DataContextExtensions.cs:86-92,104-110`) объявляют только `InvalidOperationException` («нет ключа»), но не `NotSupportedException` для in-memory/ClickHouse (`BuildUpdateSql`), тогда как терминалы билдера его объявляют. CS1591 не нарушен — только полнота; согласовано с `Delete(entity)` (DEL8/ℹ️ D).

**Применено (23.09.2026, после аудита).** Находка 109 исправлена: `UpdateBuilder.BuildAssignment` признаёт ссылкой на колонку только member, прочитанный с параметра лямбды (`body.Expression is ParameterExpression && == value.Parameters[0]`); захваченное свойство (`x => holder.Name`) и статическое (`x => Config.Default`) постоян folds-ятся в параметр, добавлен SQL-gen-тест `Update_SetCapturedProperty_ShouldParameterise`. Находка 112 исправлена: все новые файлы нормализованы в CRLF. Находки 110 (`SqlMutationBuilder` > 500 строк) и 111 (DRY key-`WHERE` в `MakeDelete`/`MakeUpdate`) приняты как defer.

**Публичная сторона — `API-NAMING-REVIEW.md`, UPD1–UPD4.**

**Проверка (23.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0** (проверено); подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах UPDATE — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `roslyn members NextORM.Core.UpdateBuilder` — **7** публичных методов + internal ctor; `wc -l` — `SqlMutationBuilder.cs` 546, `UpdateBuilder.cs` 275, `UpdateCommand.cs` 102, `KeyValue.cs` 23, `QueryPlanner.cs` 451, `DataContext.cs` 638; EOL — 11 новых файлов LF-only (`crlf=0`), все изменённые — CRLF. Скретч-пробы (file-based app вне репозитория): `Set(x => x.Name, x => holder.Name)`/`x => Config.Default` → `BuildSqlCommandException`, `x => x.Name`/constant-перегруз — OK. Сфокусированные тесты (Release, `--no-build`): core `UpdateBuilderTests` **4/4**; `UpdateSqlGenerationTests` **37/37** (sqlite 6 / postgres 11 / sqlserver 6 / mysql 6 / mariadb 6 / clickhouse 2). Интеграция (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`, Debug) — **20/20, 0 skipped** (`~Update_` 16 + `~UpdateAsync` 4; SQLite/PostgreSQL/SQL Server/MySQL). Публичная сторона — `API-NAMING-REVIEW.md`, UPD1–UPD4.

## 🔎 Точечный аудит 23.09.2026 — DML `UPDATE`, фаза 2: `RETURNING`/`OUTPUT` (`UpdateReturningBuilder<TEntity,TResult>` + `Returning()`), uncommitted working tree; 🔴 — 0, 🟡 — 2, ℹ️ — 6

**Область.** Продолжение аудита UPDATE-фазы 1 (Находки 109–112, публичная сторона UPD1–UPD4). Новый публичный `NextORM.Core.UpdateReturningBuilder<TEntity,TResult>` (`src/nextorm.core/Builders/UpdateReturningBuilder.cs:13`, `sealed`, internal ctor `:20`; 5 терминалов: `Single` `:36`, `SingleAsync` `:44`, `ToList` `:50`, `ToListAsync` `:57`, `ToSql` `:66`). `UpdateBuilder<TEntity>` **+2** публичных члена: `Returning() -> UpdateReturningBuilder<TEntity,TEntity>` (`:119`) и `Returning<TResult>(Expression<Func<TEntity,TResult>>)` (`:137`) — всего **9**; internal `BuildReturningCommand(IReadOnlyList<IPropertyMetadata>)` (`:217`, всегда `keys: null`). `UpdateCommand.ReturningColumns` (`src/nextorm.core/Query/Mutations/UpdateCommand.cs:109`, ctor-параметр `:79`). Рендер `SqlMutationBuilder.MakeUpdate` (`src/nextorm.core/DataContext/SqlMutationBuilder.cs:284-342`): `OUTPUT` — между `SET` и `WHERE` (`:310-311`), `RETURNING` — в самом конце (`:333-334`), колонки — через общий `RenderColumnsOrNull` (`:306`). Диспетчер `DataContext.BuildReturningSql` получил ветку `UpdateCommand` (`src/nextorm.core/DataContext/DataContext.cs:341`), `EnsureReturningSupportedIfNeeded` расширен на `UpdateCommand` (`:443-447`). Тесты: `UpdateSqlGenerationTests` ×6 (+`returning`/`output`-кейсы), `UpdateBuilderTests` (+`InMemory_Returning_ShouldThrow`), интеграция `CommonTestSuite.Update.cs` (+2). Публичная сторона — `API-NAMING-REVIEW.md`, UPD5–UPD6.

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (проверено). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах фазы 2 — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же **7** inert `dotnet_diagnostic.*.severity = silent` (6 `S*` без `SonarAnalyzer`-пакета + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` в `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ XML-doc новых публичных членов полон (включая `<exception>` на всех 5 терминалах `UpdateReturningBuilder` — в отличие от DEL8).

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Новых disposable-полей/локальных нет; `MakeUpdate` берёт `StringBuilderPool.Shared` в `try/finally` (`SqlMutationBuilder.cs:295,338-341`); `UpdateReturningBuilder`/`UpdateCommand` ресурсов не держат; `ExecuteReturning` идёт через `_executor.ExecuteReader*` (внутри `using`). `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет: `FirstOrThrow` — `Count switch` (`UpdateReturningBuilder.cs:76-82`), `ReturningProjection.Parse` — `is`-паттерны, рендер ключей `MakeUpdate` — цикл. `MakeOutput`/`MakeReturning` — `Select`/`Join` по колонкам **на инструкцию** (не на строку), cold. |
| 4. God-классы/методы | ⚠️ `SqlMutationBuilder.cs` **546 → 556** — порог >500 держится (**Находка 110**, не новая); `UpdateBuilder.cs` 275 → **323**, `UpdateCommand.cs` 102 → **110**, `UpdateReturningBuilder.cs` **92** (<500); `DataContext.cs` 638 → **639** (>500, см. ℹ️ D). |
| 5. Хэш-ключи / план-кэш | ✅ `UpdateBuilder`/`UpdateReturningBuilder`/`UpdateCommand` в план-кэш не пишут; мутации минуют `QueryPlanStore`; возврат строк использует `RowMapperFactory.GetOrBuild` (ключ — sql+providerType+selectList, кэшируется как у query-проекций). |
| 6. События / исключения | ✅ Новых подписок/`catch` нет; `InvalidOperationException`/`NotSupportedException` с понятными сообщениями; `<exception>`-полнота — ✅ (все 5 терминалов). |
| 7. NRT / возвратный тип | ✅ `IReadOnlyList<IPropertyMetadata>? ReturningColumns` аннотирован; `TResult`/`IReadOnlyList<TResult>`/`Task<...>` корректны; `DataContext` — internal-геттер (не расширяет публичную поверхность); сборка `0/0`. |

### 🟡 Находка 113 (P2, DRY/дублирование) — `UpdateReturningBuilder<TEntity,TResult>` — вторая дословная копия в семействе `*ReturningBuilder`

**Место:** `src/nextorm.core/Builders/UpdateReturningBuilder.cs` (**92** строки) ↔ `src/nextorm.core/Builders/DeleteReturningBuilder.cs` (**93** строки).

**Что не так.** После нормализации имён (`delete`↔`update`, `removed`↔`updated`) построчный `diff` даёт только: (1) `using NextORM.Core;` (`DeleteReturningBuilder.cs:1` — избыточный, файл и так в этом namespace), (2) слова в XML-`<summary>`/`<param>`/`<exception>` и в двух сообщениях (`"removed"/"updated"`, `"deleted"/"updated"`). Идентичны: 4 поля (`_delete`/`_update`, `_returningColumns`, `_selectList`, `_oneColumn`), internal ctor, тела всех 5 терминалов, `BuildCommand`, `FirstOrThrow`, `RequireExecutor`. Это уже **третья** реализация одного контракта (`InsertReturningBuilder` — 207 строк из-за dual identity-режима; `Delete`/`Update` — 93/92, различаются лишь именем родителя). `ReturningProjection` поделил только парсер проекции, а не сами терминалы. Семейство Находок 100/104/107/111.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`).** Либо (a) извлечь shared-ядро `Delete`/`Update`: внутренний generic-тип/база, параметризованные родителем (контракт — `IDataContext DataContext` + `MutationCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata>)`), оставив публичные `DeleteReturningBuilder<,>`/`UpdateReturningBuilder<,>` тонкими обёртками (имена/члены не меняются); `InsertReturningBuilder` в это ядро полностью не ложится (identity-режим) — ему композиция, не наследование. Либо (b) **принять как defer** (как Находки 100/104): дублирование терминалов уже принято владельцем, риск здесь минимален (никакого горячего пути — только `IDataContext`+`UpdateCommand`), а правка может быть отложена. Публичную форму не менять.

**Проверка:** нормализованный `diff` (sed `delete/update/removed/updated`→`X`) — совпадают все строки тела, кроме `using` и прозы; `wc -l` — 92 vs 93.

### 🟡 Находка 114 (P2, диспетчеризация/DRY) — `EnsureReturningSupportedIfNeeded` — растущая 3-арм `or`-цепочка; `ReturningColumns` не поднят в `MutationCommand`

**Место:** `src/nextorm.core/DataContext/DataContext.cs:443-447` (3-арм паттерн), `:337-344` (`BuildReturningSql`), `:381-391` (`BuildMutationSql`); `ReturningColumns` объявлен независимо в `InsertCommand` (`MutationCommand.cs:148`), `DeleteCommand` (`DeleteCommand.cs:44`), `UpdateCommand` (`UpdateCommand.cs:109`), но **не** в базовом `MutationCommand` (`MutationCommand.cs:31-45`).

**Что не так.** Функционально корректно (проверено в фазах 1–2), но каждая новая returning-мутация требует правки `or`-цепочки, а ту же ось типов уже свитчат `BuildReturningSql` (3 ветки) и `BuildMutationSql` (6 веток) — три параллельные диспетчеризации по `command is <Type>`. Инвариант «нужен ли `RETURNING`/`OUTPUT`» хранится не в модели команды, а в двух местах рендера.

**Стало (рекомендация).** Поднять в `MutationCommand` `public virtual IReadOnlyList<IPropertyMetadata>? ReturningColumns => null;` (переопределяют только `Insert`/`Delete`/`Update`; `Merge`/`DeleteJoin`/`Truncate` наследуют `null`) — тогда `EnsureReturningSupportedIfNeeded` сводится к `if (command.ReturningColumns is { Count: > 0 }) EnsureReturningSupported();`. Опционально, дефекта нет.

### ℹ️ Наблюдения (фикс не требуется)

- **A. Расположение `OUTPUT`/`RETURNING` по провайдерам — корректно (проверено).** SQL Server (`SupportsOutput => true`, `SupportsReturning` наследует `false`): `update <t> set ... output inserted.<cols> where ...` — `MakeOutput` даёт `inserted.<col>` (`SqlDialectBase.cs:701`; для `UPDATE` это правильная pseudo-таблица), `OUTPUT` строго между `SET`-списком и `WHERE` (`SqlMutationBuilder.cs:310-311`). PostgreSQL/SQLite (`SupportsReturning => true`): `update <t> set ... where ... returning <cols>` — в самом конце (`:333-334`). MySQL/MariaDB (ни одного): `ToSql()` → `EnsureReturningSupportedIfNeeded` → `NotSupportedException`, исполнение → `EnsureReturningSupported` (`DataContext.cs:424-429`). Совпадает с ANSI/T-SQL; тесты фиксируют строки (sqlserver `:90,103`, postgres `:144,157`, sqlite `:80`, mysql/mariadb/clickhouse `Update_Returning_ShouldThrow`).
- **B. `Returning()` на key-форме невозможен по построению (подтверждено).** Key-форма — расширения `Update<TEntity>(entity)`/`UpdateAsync<TEntity>(entity)` (`DataContextExtensions.cs:87,106`) возвращают `int`/`Task<int>` (через `UpdateEntity`/`UpdateEntityAsync`) и билдер наружу не отдают; `Returning()` живёт только на `UpdateBuilder<TEntity>`, чей `BuildReturningCommand` (`:217`) всегда передаёт `keys: null`. «key-форма + `Returning`» невыразима на компиляции. XML-оговорка `Returning()` (`UpdateBuilder.cs:112-118`) верна; к формулировке — ℹ️ (1) публичного регистра.
- **C. `IDisposable`/подавления/LINQ/NRT — чисто.** См. таблицу, строки 1–3, 7. `CA2213`/`CA1816`/`CA1508` не затронуты.
- **D. Обновление размеров (Находки 110/111 держатся).** `SqlMutationBuilder.cs` **546 → 556** (Находка 110), `DataContext.cs` **638 → 639** (перешёл порог >500 ещё в INSERT-фазе, ℹ️ F того раздела). Keys-цикл `MakeUpdate`↔`MakeDelete` (Находка 111) не изменился.
- **E. EOL — Находка 112 закрыта.** Все 12 новых/изменённых файлов UPDATE-поверхности — CRLF (`file`: «with CRLF line terminators»; напр. `UpdateReturningBuilder.cs` CR=92/LF=92, `DeleteReturningBuilder.cs` CR=93/LF=93). Рекуррентный дефект генерации новых файлов в этом инкременте не воспроизвёлся.
- **F. Пробелы покрытия / сопровождение.** (1) Терминалы `Single`/`SingleAsync` `UpdateReturningBuilder` не покрыты ни unit-, ни интеграционным тестом (интеграция использует только `ToList`/`ToListAsync`) — ветки 0/1 `FirstOrThrow` не исполняются; симметрично `DeleteReturningBuilder` (`Single` не покрыт и там) — не регрессия, но стоит закрыть. (2) `UpdateReturningBuilder.ToSql()` in-memory-ветка (`:71`) не покрыта (core-тест бьёт `.ToList()`). (3) `ITestProvider.SupportsInsertReturning` (`tests/nextorm.integration.tests/Providers/ITestProvider.cs:71`) теперь гейтит и UPDATE-, и DELETE-возврат (`CommonTestSuite.Update.cs:101,123`, `Delete.cs:85,106`) — имя уже не отражает назначение (кандидат на `SupportsReturning`; только тесты). (4) `ReturningProjection` XML-summary (`ReturningProjection.cs:8-9`) всё ещё «Shared by the insert and delete returning builders» — теперь и update; `DeleteReturningBuilder.cs:1` несёт избыточный `using NextORM.Core;` — косметика.

**Применено (23.09.2026, после аудита).** Находка 114 исправлена: `ReturningColumns` поднят в `MutationCommand` как `public virtual … => null` и переопределён в `InsertCommand`/`DeleteCommand`/`UpdateCommand`, поэтому `EnsureReturningSupportedIfNeeded` сведён к `command.ReturningColumns is { Count: > 0 }`. Находка 113 (общее ядро `Delete/UpdateReturningBuilder`) — принята как defer по решению владельца (как 110/111; `InsertReturningBuilder` отличается identity-режимом).

**Публичная сторона — `API-NAMING-REVIEW.md`, UPD5–UPD6.**

**Проверка (23.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0** (проверено); подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах фазы 2 — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `roslyn members NextORM.Core.UpdateReturningBuilder` — **5** публичных методов + internal ctor; `roslyn members NextORM.Core.UpdateBuilder` — **9** публичных методов + internal ctor; `wc -l` — `SqlMutationBuilder.cs` 556, `DataContext.cs` 639, `UpdateBuilder.cs` 323, `UpdateReturningBuilder.cs` 92, `UpdateCommand.cs` 110, `DeleteReturningBuilder.cs` 93; EOL — CRLF (12/12). Сфокусированные тесты (Release, `--no-build`): core `UpdateBuilderTests` **5/5**; `UpdateSqlGenerationTests` **46/46** (sqlite 7 / postgres 14 / sqlserver 8 / mysql 7 / mariadb 7 / clickhouse 3), 0 skipped. Контейнерная интеграция аудитом не перезапускалась (по отчёту автора — 1210 total, 0 failed, 48 skipped; интеграционные `Update_Returning*` — 2). Публичная сторона — `API-NAMING-REVIEW.md`, UPD5–UPD6.

## 🔎 Точечный аудит 23.09.2026 — DML `UPDATE`, финальный инкремент: multi-table `UPDATE ... FROM`/join (`UpdateJoin`) + ClickHouse `ALTER TABLE ... UPDATE` mutation, uncommitted working tree; 🔴 — 0, 🟡 — 5, ℹ️ — 7

**Область.** Продолжение аудитов UPDATE-фазы 1 (Находки 109–112, UPD1–UPD4) и фазы 2 `RETURNING`/`OUTPUT` (Находки 113–114, UPD5–UPD6). Новый публичный `NextORM.Core.UpdateJoinBuilder<TProjection>` (`src/nextorm.core/Builders/UpdateJoinBuilder.cs:19`, `sealed`, internal ctor `:25`; **6** публичных членов: `Set<TValue>(Expression<Func<TProjection,TValue>>, TValue)` `:40`, `Set<TValue>(..., Expression<Func<TProjection,TValue>>)` `:58`, `Where(Expression<Func<TProjection,bool>>)` `:73`, `ToSql()` `:87`, `Update()` `:99`, `UpdateAsync(CancellationToken = default)` `:107`). **7** новых extension'ов `DataContextExtensions.UpdateJoin<T1..T8>` (`DataContextExtensions.cs:520,532,545,559,574,590,607`). Новый internal `UpdateJoinCommand`/`UpdateJoinAssignment` (`Query/Mutations/UpdateJoinCommand.cs:51,11`) + `SqlStatementType.UpdateJoin` (`MutationCommand.cs:15`). Рендер: `SqlBuilder.MakeUpdateJoin` (`SqlBuilder.cs:634-725`), `SqlSourceRenderer.MakeUpdateAssignments` (`SqlSourceRenderer.cs:755-795`), `QueryPlanner.RenderUpdateJoin` (`QueryPlanner.cs:247-270`), `DataContext.BuildUpdateJoinSql` (`DataContext.cs:425-432`) + ветка `BuildMutationSql` `:386`. Диалект: DIM `SupportsUpdateJoin` (`ISqlDialect.cs:1296`), `UpdateJoinRequiresFrom` (`:1305`), `MakeUpdateJoin` (`:1324`), ClickHouse-хуки `UpdateRequiresWhere` (`:1279`)/`MakeUpdateSuffix` (`:1288`)/`MakeUpdateHead` (`:1272`) + двойники `SqlDialectBase` (`:778-812`); override'ы `PostgresDialect:75,78,85`, `SqliteDialect:25,27,34`, `SqlServerDialect:49,55`, `MySqlDialect:46,53`, `ClickHouseDialect:47,50,53,56`. `SqlMutationBuilder.MakeUpdate` (`:284-345`) получил ветку `where 1` (`UpdateRequiresWhere`) и суффикс (`MakeUpdateSuffix`) — пара к `DeleteRequiresWhere`/`MakeDeleteSuffix`. `JoinedEntityBuilder<T1..T8>` объявлены `partial` (`:13,131,239,341,443,545,656`). Тесты: `UpdateJoinSqlGenerationTests` ×5 провайдеров (PG 5, SQLite 2, SQL Server 2, MySQL 2, MariaDB 2), ClickHouse `UpdateSqlGenerationTests` (5, включая `UpdateJoin_ShouldThrowBecauseMutationCannotJoin`), core `InMemory_UpdateJoin_ShouldThrow`, интеграция `CommonTestSuite.Update` (+2).

**База (этот проход).** `dotnet build nextorm.sln -c Release` — **0 warnings / 0 errors** (проверено). Подавления `src/`: **6** `SuppressMessage` (все с `Justification`) + **5** `#pragma warning disable` (все с парным `restore`) = **11/11** оправданных, **0** неоправданных; в файлах инкремента — **0**. Слоп: `Skip=` — **0**; `Task.Delay` — **2** baseline (`Task.Delay(0)`-yield, `tests/nextorm.core.tests/InMemoryTests.cs:125,414`, вне диффа); `Thread.Sleep`/пустых `catch`/`NoWarn`/inline `Version` — **0**; `slopwatch` локально не установлен (`.config/dotnet-tools.json` — только coverage/reportgenerator/docfx), скан паттернов выполнен вручную. `.editorconfig` — те же **7** inert `dotnet_diagnostic.*.severity = silent` (6 `S*` без `SonarAnalyzer`-пакета + `CA2254`; пре-существующее). `find -name 'PublicAPI*.txt'` — **0** (Шаг 5 открыт). CS1591 не подавлен (`NoWarn` по `.csproj` — **0**; `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`), build `0/0` ⇒ XML-doc новых публичных членов полон.

| # | Проверка | Итог |
|---|----------|------|
| 1. `IDisposable` | ✅ Корректно. `SqlBuilder.MakeUpdateJoin` берёт четыре pooled-`StringBuilder` (`fromAndJoins`/`usingSources`/`joinConditions`/`whereSql`) и возвращает их в `finally` (`:659-724`); `SqlSourceRenderer.MakeUpdateAssignments` берёт один pooled `StringBuilder` в `try/finally` (`:757,792-794`) и оборачивает `CreateColumnVisitor` в `using` (`:767,779`). `UpdateJoinBuilder` владеет только `EntityBuilder`/`List<>`, disposable-полей нет; `QueryPlanner.RenderUpdateJoin` ресурсов не держит. `CA2000`/`CA2213`/`CA1816` не затронуты. |
| 2. Подавления | ✅ Новых `#pragma`/`SuppressMessage`/`NoWarn` — **0**; соотношение проекта **11/11** (0 неоправданных). |
| 3. LINQ на горячем пути | ✅ Per-row LINQ нет: `SetAssignment` (`:129-141`), `RequireInnerJoins` (`:145-153`), `ValidateTargets`/`FindProperty` (`:157-187`) обходят `List` циклами; `MakeUpdateAssignments` — цикл по `assignments`. |
| 4. God-классы/методы | ⚠️ `DataContextExtensions.cs` **942** — перешёл порог >500 и продолжает расти (**Находка 119**); `SqlMutationBuilder.cs` **563** (>500, **Находка 110**, не новая); `SqlBuilder.cs` 847 / `SqlSourceRenderer.cs` 856 — пре-существующие крупные. `MakeUpdateJoin` ~92, `MakeUpdateAssignments` ~41, `RenderUpdateJoin` 24 — линейные рендеры (>30); `UpdateJoinBuilder.cs` 238, `UpdateJoinCommand.cs` 69 — норма. |
| 5. Хэш-ключи / план-кэш | ✅ `UpdateJoinBuilder`/`UpdateJoinCommand` в план-кэш не пишут; мутации минуют `QueryPlanStore`. |
| 6. События / исключения | ✅ Новых подписок/`catch` нет; `ArgumentException` (`ResolveTarget`), `InvalidOperationException` (нет assignment/join), `NotSupportedException` (in-memory, не-INNER, provider без join-формы) с понятными сообщениями; гейт `SupportsUpdateJoin` — в `DataContext.BuildUpdateJoinSql` и защитно в `SqlBuilder.MakeUpdateJoin`. |
| 7. NRT / возвратный тип | ✅ `UpdateJoinCommand.Source` non-null, `Assignments` — `IReadOnlyList<UpdateJoinAssignment>`; `_query`/`_targetType` non-null; `TProjection`-селекторы аннотированы; сборка `0/0`. |

### 🟡 Находка 115 (P2, DRY/дублирование) — `SqlBuilder.MakeUpdateJoin` — почти дословная копия `MakeDeleteJoin`

**Место:** `src/nextorm.core/DataContext/SqlBuilder.cs:634-725` (`MakeUpdateJoin`) ↔ `:540-625` (`MakeDeleteJoin`).

**Что не так.** Совпадают: `_ctx.ColumnsProvider.PushSourceScope()` + `try`/`finally` с `PopSourceScope`; четыре pooled-`StringBuilder` (`fromAndJoins`/`usingSources`/`joinConditions`/`whereSql`) с идентичным `finally`-возвратом; `targetSql = SqlSourceRenderer.MakeFrom(..., out targetAlias)` + проверка `IsNullOrEmpty(targetAlias)`; `targetAliasToken = _ctx.Dialect.Escape(targetAlias)`; `target = MakeFrom(FromRenderOptions(false, null, false))`; ветка `if (_ctx.Dialect.<…RequiresFrom|…RequiresUsing>)` с одинаковым циклом `MakeJoinParts` (разделители `", "`/`" and "`); `MakeWhere`; вызов диалекта; `return (sql, _ctx.Params)`. Отличия: имя флага (`UpdateJoinRequiresFrom` vs `DeleteJoinRequiresUsing`), строка сообщения (`"multi-table UPDATE"` vs `"DELETE"`), защитный гейт `SupportsUpdateJoin` и вставка `MakeUpdateAssignments` перед `MakeWhere`. Это 6-й случай семейства 104/111/113 (дублирование рендер-каркаса мутаций); ~58 строк совпадают.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести общий каркас в private `RenderJoinMutationSources(...)` (возврат `(target, targetAliasToken, fromAndJoins, usingSources, joinConditions, whereSql)` + колбэк/делегат на assignments), а `MakeDeleteJoin`/`MakeUpdateJoin` оставить тонкими адаптерами. Публичную форму не менять.

**Проверка:** построчное сравнение `:540-625` и `:634-725` — совпадают все строки каркаса, кроме перечисленных.

### 🟡 Находка 116 (P2, DRY/дублирование) — `PostgresDialect.MakeUpdateJoin` и `SqliteDialect.MakeUpdateJoin` идентичны

**Место:** `src/nextorm.postgres/PostgresDialect.cs:85-108` ↔ `src/nextorm.sqlite/SqliteDialect.cs:34-56`.

**Что не так.** Обе реализации `FROM`-формы совпадают дословно (свёртка `joinConditions`+`whereSql` через `" and "`, затем `update <target> as <alias> set <assignments> from <usingSources> [where …]`), различаются только XML-summary. Флаг `UpdateJoinRequiresFrom` уже сообщает эту форму, т.е. реализация — кандидат в базу. Продолжение Находок 104/113/115.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** поднять FROM-форму в `SqlDialectBase` (напр. базовая `MakeUpdateJoin`, реагирующая на `UpdateJoinRequiresFrom`), тогда PG/SQLite выставляют только `SupportsUpdateJoin`/`UpdateJoinRequiresFrom`. Публичную форму не менять.

**Проверка:** нормализованный `diff` тел `PostgresDialect.cs:85-108` и `SqliteDialect.cs:34-56` — совпадают; `rg MakeUpdateJoin` — 5 override'ов.

### 🟡 Находка 117 (P2, DRY/дублирование) — INNER-гейт и placeholder-источник UPDATE дублируют DELETE

**Место:** `src/nextorm.core/Builders/UpdateJoinBuilder.cs:145-153` (`RequireInnerJoins`) ↔ `src/nextorm.core/DataContext/DataContextExtensions.cs:623-634` (`RequireInnerJoinsForDelete`); `UpdateJoinBuilder.cs:124-125` ↔ `DataContextExtensions.cs:613-621` (`PrepareDeleteJoinSource`).

**Что не так.** (1) Оба валидатора перебирают `joins` и бросают `NotSupportedException` с одинаковой формулировкой «only supports INNER joins…», отличаясь только словом `UPDATE`/`DELETE`. (2) Обе «пустышки» строят `query.Select(p => new { Unit = 1 })` + `IgnoreColumns = true`, чтобы получить подготовленный `QueryCommand` (ℹ️ E фазы DELETE 3). Логика одна, живёт в двух местах, при этом `PrepareDeleteJoinSource` — `private` в `DataContextExtensions` и недоступен билдеру.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** вынести общий internal-хелпер (напр. `MutationJoinGuards.RequireInnerJoins(IReadOnlyList<JoinExpression>?, string statement)` и `PrepareJoinMutationSource<TProjection>(EntityBuilder<TProjection>)`), которым пользуются и `UpdateJoinBuilder`, и DELETE-join extension'ы. Публичную форму не менять.

**Проверка:** сравнение `:145-153`↔`:623-634` и `:124-125`↔`:613-621` — совпадают, кроме ключевого слова.

### 🟡 Находка 118 (P2, форматирование) — склеенные `{` и statement в `PrepareDeleteJoinSource`

**Место:** `src/nextorm.core/DataContext/DataContextExtensions.cs:614` — `    {        RequireInnerJoinsForDelete(query.Joins);`.

**Что не так.** В этом инкременте блок `PrepareDeleteJoinSource` сдвинулся вниз (после 7 extension'ов `UpdateJoin`), и открывающая скобка оказалась на одной строке с первым оператором. Компилируется, build `0/0`, EOL CRLF — но это артефакт редактирования, нарушающий форматирование. Дефекта поведения нет.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** разнести на две строки: `{` и `RequireInnerJoinsForDelete(query.Joins);`. Файл остаётся CRLF.

**Проверка:** `sed -n '613,615p' DataContextExtensions.cs` показывает склейку; `rg PrepareDeleteJoinSource` — `:613`.

### 🟡 Находка 119 (P2, god class) — `DataContextExtensions.cs` 599 → 942 строк

**Место:** `src/nextorm.core/DataContext/DataContextExtensions.cs` — **942** строки (было **599** в фазе DELETE-3, **613** в UPDATE-фазе 1). Инкремент добавил 7 extension'ов `UpdateJoin` (`:505-611`, ~107 строк с XML-doc).

**Что не так.** Файл давно перешёл порог «god class >500» из `dotnet-csharp-code-smells` §5 и продолжает расти: ~24 публичных DELETE-extension'а (`:137-503`) + 3 single-table UPDATE (`:57-112`) + 7 `UpdateJoin` (`:505-611`) — это ~34 generic-перегрузки с XML-doc, значительная часть — почти одинаковый boilerplate. Класс объявлен `public static class` (не `partial`), поэтому инкрементальная изоляция перегрузок невозможна.

**Стало (рекомендация, маршрут — `nextorm-design-engineer`):** объявить `public static partial class DataContextExtensions` и вынести `UpdateJoin*` в `DataContextExtensions.UpdateJoin.cs`, DELETE-join — в `DataContextExtensions.DeleteJoin.cs` (ср. Находку 90: `InsertBuilder` разбит `partial`). Публичную форму не менять.

**Проверка:** `wc -l` — 942; `rg "public static class DataContextExtensions"` — `:11` (без `partial`); `roslyn members NextORM.Core.DataContextExtensions` — 34 публичных extension'а, из них ~31 — DML-перегрузки.

### ℹ️ Наблюдения (фикс не требуется)

- **A. `ClickHouseDialect.SupportsUpdate => true` (`:47`) — избыточный override.** Базовый `SqlDialectBase.SupportsUpdate => true` (`:778`), DIM `ISqlDialect.SupportsUpdate => true` (`:1261`); override появился в фазе 1 со значением `false` и после разворота на `true` стал no-op. Семантика «ClickHouse обновляет через мутацию» уже выражена `MakeUpdateHead`/`UpdateRequiresWhere`/`MakeUpdateSuffix`; override можно удалить. Поведенческое следствие (ClickHouse `UPDATE` теперь поддержан) — публичная сторона (`API-NAMING-REVIEW.md`, UPD-наблюдение (1)).
- **B. `partial` на 7 `JoinedEntityBuilder<…>` (`:13,131,239,341,443,545,656`) — мёртвый модификатор.** `rg "partial class JoinedEntityBuilder"` даёт только эти 7 объявлений; второй части нет, `Set`-методы не добавлялись (вход — extension `UpdateJoin`). Метаданные/бинарь не меняются, но диффа прибавляет. Вернуть `class` либо оставить осознанно под будущую часть.
- **C. `UpdateJoinBuilder.ResolveTarget` не проверяет корень селектора (`:189-199`).** Условие — лишь `MemberExpression { Member: PropertyInfo, Expression: MemberExpression { Member: PropertyInfo item } } && item.Name == "Item1"`; не проверяется, что вложенный `Item1` прочитан **с параметра лямбды** (`body.Expression is MemberExpression { Expression: ParameterExpression p } && p == column.Parameters[0]`). `p => holder.Item1.Name` (захваченный `Projection<T1,..>`) пройдёт проверку, если `Name` — mapped-колонка T1; это LHS-двойник закрытой Находки 109 (там был RHS). Вероятность мала (нужен захваченный projection), но harden-фикс однострочный. Дефекта поведения не наблюдалось.
- **D. `SqlBuilder.MakeUpdateJoin` считает `targetSql` и наполняет `fromAndJoins` для FROM-диалектов впустую.** `targetSql` (`:659`) нужен только за `out targetAlias`; для PG/SQLite `fromAndJoins` не заполняется, а `target` (`:668`) и `usingSources` используются. Холодный путь, дефекта нет; при рефакторинге Находки 115 можно считать `targetSql` только в alias-ветке.
- **E. `Item1` — строковый литерал (`UpdateJoinBuilder.cs:194`).** Согласовано с `QueryPlanner.cs:450` (`GetProperty("Item1")`), `AliasFromProjectionVisitor.cs:33`, `MemberTranslator.cs:343`; при желании `nameof(Projection<,>.Item1)`.
- **F. Пробелы покрытия.** Нет SQL-gen-теста на: (1) arity ≥3 (`UpdateJoin<T1,T2,T3>`…`<T1..T8>` — 6 из 7 перегрузок не исполняются); (2) повторный `Set` по той же колонке (замена в `SetAssignment` `:129-141`); (3) отказ по computed-цели (`ValidateTargets` `:170-171`); (4) `ArgumentException` на селектор не из первой таблицы (`p => p.Item2.Name`, `ResolveTarget` `:197`); (5) `UpdateJoin()` без join'а (`:116-117`); (6) ClickHouse `UpdateJoin().Update()`/`UpdateAsync()` (тестируется только `ToSql`). Интеграция покрывает одну join-форму на 4 контейнерных провайдера.
- **G. Нет `RETURNING` у multi-table UPDATE.** PostgreSQL/SQLite нативно поддерживают `UPDATE ... FROM ... RETURNING`, но `UpdateJoinBuilder` не имеет `Returning()`, а `UpdateJoinCommand` наследует `ReturningColumns => null` (`MutationCommand.cs:52`). Если пробел намеренный — ок; иначе кандидат на будущую фазу. Не дефект текущей поверхности.

**Итог.** 🔴 — нет; 5 находок P2 (115–119) и наблюдения — маршрут `nextorm-design-engineer` (публичную форму не менять; 116–119 — внутренние/форматные; 118 — простой фикс форматирования).

**Применено (23.09.2026, после аудита).** Находка 116 исправлена: `FROM`-форма поднята в `SqlDialectBase.MakeUpdateJoin` (рендерит при `UpdateJoinRequiresFrom`), а `MakeUpdateJoin`-override'ы в `PostgresDialect`/`SqliteDialect` удалены — остались только флаги `SupportsUpdateJoin`/`UpdateJoinRequiresFrom`. Находка 117 исправлена: общий internal-хелпер `JoinedMutationSource.Prepare`/`RequireInnerJoins` теперь используется и `PrepareDeleteJoinSource`, и `UpdateJoinBuilder.BuildCommand` (INNER-гейт и placeholder-источник больше не дублируются). Находка 118 исправлена (склейка `{`/statement в `PrepareDeleteJoinSource` устранена — метод свёрнут в expression-bodied). ℹ️ A закрыто: избыточный `ClickHouseDialect.SupportsUpdate => true` удалён (база уже `true`). ℹ️ B закрыто: мёртвый `partial` с 7 классов `JoinedEntityBuilder` снят. Находки 115 (`MakeUpdateJoin` ↔ `MakeDeleteJoin`) и 119 (`DataContextExtensions.cs` > 500 строк) приняты как defer по решению владельца (как 110/111/113).

**Публичная сторона — `API-NAMING-REVIEW.md`, UPD7–UPD8.**

**Проверка (23.09.2026).** `dotnet build nextorm.sln -c Release` — **0/0** (проверено); подавления `src/` — **6+5=11** (0 неоправданных), новых в файлах инкремента — **0**; `Skip=` — 0, `Task.Delay` — 2 baseline `(0)`; `find PublicAPI*.txt` — **0** (Шаг 5); `slopwatch` не установлен (`.config/dotnet-tools.json` — coverage/reportgenerator/docfx), скан паттернов вручную; `roslyn members NextORM.Core.UpdateJoinBuilder` — **6** публичных методов + internal ctor; `roslyn members NextORM.Core.DataContextExtensions` — `UpdateJoin` ×7 (`:520-607`); `rg 'SupportsUpdateJoin|UpdateJoinRequiresFrom|MakeUpdateJoin|UpdateRequiresWhere|MakeUpdateSuffix'` — ISqlDialect `:1279,1288,1296,1305,1324`, SqlDialectBase `:784-812`, PG `:75,78,85`, SQLite `:25,27,34`, SqlServer `:49,55`, MySQL `:46,53`, ClickHouse `:47,50,53,56`; `wc -l` — `UpdateJoinBuilder.cs` 238, `UpdateJoinCommand.cs` 69, `SqlBuilder.cs` 847, `SqlSourceRenderer.cs` 856, `DataContextExtensions.cs` 942, `DataContext.cs` 649, `QueryPlanner.cs` 479, `SqlMutationBuilder.cs` 563; EOL — 10/10 новых файлов CRLF, все изменённые — CRLF (Находка 112 держится закрытой); `rg PrepareDeleteJoinSource` — `:613`, склейка `{` на `:614`. Сфокусированные тесты (Release, `--no-build`): `~UpdateJoin` — **14/14, 0 skipped** (postgres 5 / sqlite 2 / sqlserver 2 / mysql 2 / mariadb 2 / clickhouse 1); core `~UpdateBuilder` — **6/6**; ClickHouse `~UpdateSqlGeneration` — **5/5**. Контейнерная интеграция аудитом не перезапускалась (по отчёту автора — 2879 total, 0 failed, 48 skipped; интеграционные join-тесты `UpdateJoin_*`/`UpdateJoinAsync_*` — 2). Публичная сторона — `API-NAMING-REVIEW.md`, UPD7–UPD8.
