# nextorm — аудит производительности (`analyzing-dotnet-performance`)

Скиллы: `analyzing-dotnet-performance` (critical / structural / async / memory-and-strings /
collections-and-linq / io-and-serialization).
Область скана: `src/` (nextorm.core + провайдеры), .NET 10 (`LangVersion` 12.0).
Горячий путь: выполнение/стриминг запроса, построение плана.
Свежие бенчмарки: tmpfs `/tmp/nextorm-bench/test.db`, BenchmarkDotNet ShortRun/InProcess.

## Scan execution checklist

| Рецепт | Хитов |
|---|---:|
| `.IndexOf("…")` без StringComparison | 0 |
| `.Substring(` | 0 |
| `.StartsWith/.EndsWith("…")` без StringComparison | 9 |
| `.Contains("…")` строковый без StringComparison | 1 |
| `.ToLower()/.ToUpper()` | 0 |
| Chained `.Replace().Replace()` | 0 |
| `params ` (из них `params object[]` ≈ 104) | 135 |
| `.All/.Any(char…)` | 0 |
| `static readonly Dictionary<` / `FrozenDictionary<` | 0 / 0 |
| `new List<` / `new Dictionary<` | 22 / 3 |
| `StringComparer.CurrentCulture` | 0 |
| LINQ `.Select/.Where/.Cast/.Take/.Aggregate` | 19 |
| `.ToList()/.ToArray()` | 27 |
| `async void` | 0 |
| `sealed class` / unsealed non-abstract class | 17 / 55 |
| `struct` (вкл. inline-массивы) | 13 |
| `new HttpClient(` / uncached `new JsonSerializerOptions` | 0 / 0 |
| `new byte[` / `ArrayPool` / `stackalloc` | 0 / 0 / 0 |
| `Expression.Compile()` | 31 |
| Reflection `GetProperties/GetMethod/GetConstructors` | 49 |
| `Convert.ChangeType` | 2 |
| `Enum.HasFlag` | 3 |
| `.Result` (реальный `Task.Result`) | 10 (0) |
| `Task.Run(` | 1 |
| `ConfigureAwait(false)` / `(true)` | 29 / 0 |
| `yield return` / `ValueTask` | 4 / 19 |
| `ConcurrentDictionary` / `lock` | 10 / 0 |
| `StringBuilder` / `string.Format` | 9 / 6 |

## Сводка находок

| ID | Статус | Severity | Категория | Суть | Файлы |
|---|---|---|---|---|---|
| M1 | ♻️ переоткрыто → **M12** | 🟡 Moderate | allocations, GC | уточнено: SQL на cache-hit **не** пересобирается; стоимость builder-API до lookup. Вывод верен **механически**, но экономика кэша не проверялась — см. M12 | `EntityBuilder.cs`, `DataContext.cs:280-340`, `QueryPlan.cs` |
| **M2** | ✅ сделано (sync) | 🟡 Moderate | allocations | `params object[]` → `M()` + `M(params ReadOnlySpan<object?>)`; async/streaming — массив by design | `EntityBuilder.cs`, `IDataContext.cs`, `IPreparedQueryCommand.cs`, `QueryCommand.cs` |
| M3 | ✅ сделано | 🟡 Moderate | async, CPU | `MoveNextAsync` без async state machine: sync fast-path при уже завершённом `ReadAsync`; **690 → 108 ns / 100 строк** | `ResultSetEnumerator.cs:81-94` |
| M4 | ✅ сделано | 🟡 Moderate | async, CPU, memory | `Pipeline` без `Task.Run` + `Channel`: прямое `IAsyncEnumerable` + `[EnumeratorCancellation]`; нет unbounded-буфера и занятого pool-потока | `QueryCommand.cs:823-857` |
| M5 | ✅ сделано | 🟡 Moderate | CPU | fast-path `ConvertScalar` вместо `IConvertible`-диспетчеризации (не боксинг — эффект в шуме) | `DataContext.cs` |
| M6 | ✅ сделано | 🟡 Moderate | CPU, strings | `Enum.HasFlag` → битовая маска; `StartsWith` → Ordinal (`HasFlag`-эффект ~0) | `TypeExtensions.cs:10-19` |
| M7 | ✅ сделано | 🟡 Moderate | strings, CPU | `Method.Name.EndsWith/StartsWith/Contains` без Ordinal (реальный выигрыш ~10-13 ns) | `BaseExpressionVisitor.cs:302,369`, `CorrelatedQueryExpressionVisitor.cs:294-319` |
| M8 | ✅ сделано | 🟡 Moderate | CPU, strings | `string.Format` бокс убран (кэш имён); `IndexOf` амортизирован `ParamMap` (первый вызов) | `DbPreparedQueryCommand.cs:51-99`, `DataContext.cs` |
| M9 | ✅ сделано | 🟡 Moderate | allocations, concurrency | общий мутируемый `Visitor` → per-(thread, comparer) TLS (регресс-тест: 49 971/50 000 неверных хэшей до фикса); `CompareLambda` больше не бросает | `ExpressionPlanEqualityComparer.cs` |
| M10 | ✅ сделано (выигрыш не измерен) | 🟡 Moderate | CPU, structural | `sealed`: `ResultSetEnumerator`, `InMemoryEnumerator`, `InMemoryCompiledQuery`, `QueryCommand<TResult>`; JIT-эффект в микро-бенчмарке = 0 | `ResultSetEnumerator.cs:9`, `InMemoryEnumerator.cs:6`, `InMemoryCompiledQuery.cs:4`, `QueryCommand.cs:773` |
| M11 | ✅ сделано | 🟡 Moderate | allocations | LINQ → циклы в plan/map-build (4 места); `Select().ToArray()` −72 B/вызов (путь холодный) | `DataContext.cs` |
| **M12** | 🟢 закрыто (решение) | 🟠 High | allocations, CPU | **implicit-кэш проигрывает явному `Prepare()`**: 17.56 vs 11.94 µs/вызов (1.47×) и 3.76 vs 0.76 KB (4.94×); но кэш **в 2.4× лучше полного отказа от кэша** (42.06 µs, +Gen1) — рекомендация «убрать кэш» снята | `DataContext.cs` (`GetPreparedQueryCommand`), `QueryCommand.PrepareCommand`, `QueryPlanCacheKey` |
| I1 | ✅ закрыто | ℹ️ Info | strings | `string.Format` убран из всех 3 «живых» сайтов (последний — `MakeTop`) | `BaseExpressionVisitor.cs`, `IParameterProvider.cs`, `SqlServerDataContext.cs` |
| I2 | ✅ сделано | ℹ️ Info | allocations | LINQ в map-build → циклы (2 сайта в in-memory + 2 в `DataContext` из M11) | `InMemoryDataContext.cs:650,658` |
| I3 | 🔵 закрыто (холодные) | ℹ️ Info | allocations | остаток 19 (было 27); реальные — `_joins?.ToArray()` (снапшот мутабельного списка, только при join'ах), `CloneForCache`, reflection | `src/` |
| I4 | 🔵 закрыто (подтверждено) | ℹ️ Info | CPU | проверено по коду: все `Expression.Compile()` под `DataContextCache.ExpressionsCache` / `MapperCache` — one-time на выражение | `src/` |
| I5 | ✅ сделано | ℹ️ Info | allocations | `LogCommand`: пул `StringBuilder` + `object[]` вместо `StringBuilder`+`ArrayList`+интерполяции | `ResultSetEnumerator.cs:161-199` |
| I6 | 🔵 закрыто (холодные) | ℹ️ Info | strings | `.ToString()` 32; в per-row материализаторе — 0, единственный в enumerator'е — debug-лог | `src/` |

Positive: `ConfigureAwait(false)` 29 / true 0; нет `async void`, `Task.Result/.Wait()`; нет
`Substring/ToLower/ToUpper/chained Replace/CurrentCulture`; нет `HttpClient/JsonSerializerOptions`;
нет `new byte[]/ArrayPool/stackalloc` в ядре (пулированный `ObjectPool<StringBuilder>` с `try/finally`,
inline `ValueList<T>`); кэши ограничены (`MapperCache` cap 4096, `lock` 0); `List<TResult>(LastRowCount)`.

---

# Remediation

Порядок работ: **M2 → M1 → M3 → M4 → M5–M8 → M9 → M10 → M11 → I1–I6** (закрыто) → **M12 (закрыто решением)**.
M12 — переоткрытие M1: механизм кэша корректен, но его **экономика** не подтвердилась (см. M12).
Закрыт 2026-09-22 решением «`Prepare()` — санкционированный быстрый путь, fresh-fluent warm-паритет с Dapper снят как цель».

---

## M2. Устранить аллокацию `object[]` из `params` на горячем API

### Проблема

Публичный API принимает `params object[]` (≈104 объявления). При вызове с 1+ аргументом
компилятор создаёт `object[]` на каждый вызов: 24 (заголовок) + 8·n байт. Нулевая арность
использует кэшированный `Array.Empty<object>()` — там аллокации нет.

Замеренный масштаб (tmpfs, ShortRun):

| Бенчмарк | Allocated / 10 вызовов | На вызов | `object[1]` доля |
|---|---:|---:|---:|
| `Nextorm_Prepared_SingleOrDefault` | 8.36 KB | ~856 B | ~32 B (~3.7%) |
| `Nextorm_Cached_SingleOrDefault` | 45.57 KB | ~4.56 KB | ~32 B (~0.7%) |
| `Nextorm_Prepared` (Any, 100×) | 85.16 KB | ~852 B | ~32 B (~3.8%) |

Вывод: фикс даёт фиксированные ~32 B/вызов (0 args — уже 0). На высоком QPS (10k rps) это
≈ 0.32 MB/s мусора Gen0 — значимо, но это **не** главный рычаг (M1 — ~3 KB/вызов).

### Почему здесь нельзя просто заменить на `params ReadOnlySpan<object?>`

1. **Массив — транспорт для async/streaming.** Терминал сохраняет массив в
   `ResultSetEnumerator._params` и читает его после `await`; `Span<T>` этого не переживает.
   Async/streaming остаются на `params object[]` — там оптимизировать нечего.
2. **Expression trees — жёсткое ограничение компилятора.** Любой вызов с `params`-коллекцией
   внутри `Expression<>`-ламбды отвергается: `CS8640` (ref struct в дереве) + `CS9226` (expanded
   форма не-массива) — **даже с нулём аргументов** (проверено на `net10.0`). Поэтому `EntityBuilder.Any()/
   First()/Single()`, которые тесты используют как подзапросы в лямбдах, не могут быть
   `params ReadOnlySpan<object?>`.
3. **Обходной путь.** Рядом с `params ReadOnlySpan<object?>` добавляется **точный overload без
   параметров**: 0-арг вызов выбирает его (normal form приоритетнее expanded), он expression-safe
   и не создаёт span на call-site. `stackalloc object?[n]` не нужен и вообще не компилируется для
   managed-типа.

### Рекомендуемый подход (исторический план; заменён финальным дизайном ниже)

**Шаг 0. Сначала измерение (обязательно).**
Добавить в `SqliteBenchmarkSingle` (или отдельный класс) arm'ы, изолирующие именно params-массив:
`Prepared_0args`, `Prepared_1arg`, `Prepared_2args` — вызов `IPreparedQueryCommand.SingleOrDefault(ctx, …)`.
Ожидаемо: +32 B/arg. Если дельта < шума `MemoryDiagnoser` — M2 закрыть как незначимый.

**Фаза 1 — sync scalar/aggregate, без ломки бинаря (рекомендуемый старт).**
Оставить `params object?[]` как есть (binary-compat), добавить явные overload'ы на 0/1/2 аргумента,
которые кладут значения в `stackalloc object?[n]` и зовут новый span-core:

```csharp
// EntityBuilder<TEntity>.Any — было: public bool Any(params object[] @params) { ... }
public bool Any(params object[] @params) => AnyCore(@params);
public bool Any() => AnyCore(ReadOnlySpan<object?>.Empty);
public bool Any(object? p0)
{
    Span<object?> p = stackalloc object?[1];
    p[0] = p0;
    return AnyCore(p);
}
public bool Any(object? p0, object? p1)
{
    Span<object?> p = stackalloc object?[2];
    p[0] = p0;
    p[1] = p1;
    return AnyCore(p);
}
[MethodImpl(MethodImplOptions.AggressiveInlining)]
private bool AnyCore(ReadOnlySpan<object?> @params) { /* тело как раньше */ }
```

Терминал: продублировать `DbPreparedQueryCommand.GetDbCommand` span-версией
(`ReadOnlySpan<object?>` вместо `object[]?`), а в `DataContext` — приватный span-`GetDbCommand`,
который зовёт её. `IDataContext.ExecuteScalar(…, ReadOnlySpan<object?>, …)` добавляется как
**default interface method** с fallback на array-версию, чтобы не ломать сторонние реализации:

```csharp
// IDataContext.cs
public TResult? ExecuteScalar<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand,
    ReadOnlySpan<object?> @params, bool throwIfNull)
    => ExecuteScalar(preparedQueryCommand, @params.ToArray(), throwIfNull); // fallback: аллокация
```

`DataContext` переопределяет это без аллокации; `InMemoryDataContext` может оставить fallback
(у in-memory `Any` уже 0 B).

Применить к синхронным: `Any`, `Count`, `Min/Max/Avg/Sum/Stdev/Stdevp/Var/Varp`, `ExecuteScalar`,
`First/FirstOrDefault/Single/SingleOrDefault` (sync). Это ~1/3 поверхности, даёт эффект на
самом частом паттерне `Any`/агрегаты.

**Фаза 2 — C# 13, `params ReadOnlySpan<object?>` (апгрейд языка).**
`<LangVersion>13.0</LangVersion>` (или `latest`) и замена `params object?[]` → `params ReadOnlySpan<object?>`
на sync-методах. Компилятор сам делает stack-alloc для малых арностей. **Binary-breaking** для
публичного API → нужен bump версии пакета (сейчас `1.0.3-alpha` — допустимо) и запись в release notes.

**Фаза 3 — async/streaming.**
Span неприменим. Массив там неизбежен (сохраняется в enumerator). Ограничиться:
- убрать двойное копирование: проверить, что `EntityBuilder`/`QueryCommand`/`IPreparedQueryCommand`
  не создают второй массив при пробросе одного и того же `@params`;
- `[MethodImpl(AggressiveInlining)]` на проброс уже стоит.

### Точки изменения (фаза 1)

| Слой | Файл | Что |
|---|---|---|
| Builder | `src/nextorm.core/Builders/EntityBuilder.cs` | overload'ы 0/1/2 + `*Core(ReadOnlySpan<object?>)` для sync-методов |
| Query | `src/nextorm.core/Query/QueryCommand.cs:864-1010` | то же для sync-терминалов |
| Prepared | `src/nextorm.core/DataContext/Cache/IPreparedQueryCommand.cs` | overload'ы `(dataContext, p0)` / `(dataContext, p0, p1)` |
| Context iface | `src/nextorm.core/DataContext/IDataContext.cs` | default `ExecuteScalar(…, ReadOnlySpan<object?>, …)` |
| Context impl | `src/nextorm.core/DataContext/DataContext.cs` | span-`GetDbCommand`; переопределение `ExecuteScalar` |
| Leaf | `src/nextorm.core/DataContext/Cache/DbPreparedQueryCommand.cs:36-110` | `GetDbCommand(ReadOnlySpan<object?>, …)` |

### Риски / совместимость

- Явные overload'ы **не ломают** исходный код и бинарь (старый `params` остаётся).
- Нельзя держать одновременно `M(params object?[])` и `M(params ReadOnlySpan<object?>)` —
  ambiguous. Поэтому фаза 2 заменяет, а не добавляет.
- `stackalloc object?[n]` в safe-коде допустим (managed-ссылки на стеке), но **не** используйте
  его для больших/неограниченных `n` — только фиксированные 1–4.
- `ReadOnlySpan<object?>` в параметрах интерфейса/default-метода допустим (ref struct как параметр).

### Step 0 — дельта подтверждена замером (сделано)

`LangVersion` поднят до `latest` в `NextORM.Core`, `nextorm.postgres`, `nextorm.sqlserver`,
`nextorm.benchmark`. Добавлен бенчмарк `benchmarks/nextorm.benchmark/ParamsAllocationBenchmark.cs`
(каждая пара arm'ов — один и тот же путь, различие только в аллокации params-массива).
Прогон: `--filter "*ParamsAllocationBenchmark*"`, ShortRun/InProcess, tmpfs.

| Arm | Allocated/op (100 iter) | На вызов | Δ |
|---|---:|---:|---:|
| `GetDbCommand_1Arg_ReusedArray` | 2 400 B | 24 B (бокс `int`) | — |
| `GetDbCommand_1Arg_Params` | 5 600 B | 56 B | **+32 B** (`object[1]`) |
| `Nextorm_Any_1Arg_ReusedArray` | 84 008 B | 840.08 B | — |
| `Nextorm_Any_1Arg_Params` | 87 200 B | 872 B | **+32 B** |
| `Nextorm_Any_2Arg_ReusedArray` | 120 008 B | 1200.08 B | — |
| `Nextorm_Any_2Arg_Params` | 124 008 B | 1240.08 B | **+40 B** (`object[2]`) |

Вывод: params-массив стоит ровно 24 + 8·n байт (32 B при n=1, 40 B при n=2); время во всех парах
в пределах шума, т.е. стоимость чисто аллокационная. `params ReadOnlySpan<int>` компилируется под
`latest` (arm'ы `Generic_*`), но в микродемо .NET 10 stack-alloc'ит неэскейпящий `int[]`, поэтому
доказательство дельты опирается на `GetDbCommand_*` / `Nextorm_Any_*`, где массив уходит в heap.

### Реализация M2 для sync (сделано)

Дизайн (после экспериментов):
- Публичный API остаётся `params object[]` — совместимость и **expression trees**: `params ReadOnlySpan`
  внутри лямбд даёт CS8640/CS9226, а тесты используют `_sut.SimpleEntity.Any()` в подзапросах.
- Для горячих sync-методов добавлены явные перегрузки `M(object? p0)` и `M(object? p0, object? p1)`
  плюс приватный core `MCore(params ReadOnlySpan<object?>)`. Вызов `M(i)` биндится к `M(object?)`
  (normal form предпочтительнее params-формы), `M()` — к `params object[]` с `Array.Empty` (0 аллокаций).
- Внутренний span-путь: `DbPreparedQueryCommand.GetDbCommand(ReadOnlySpan<object?>, …)`; span-версии
  `DataContext.GetDbCommand/ExecuteScalar/ToList/First/FirstOrDefault/Single/SingleOrDefault` и
  соответствующие методы `IDataContext`; `InMemoryDataContext` конвертирует span→array (`ToParams`) —
  поведение и аллокации in-memory не меняются.

Важно: `stackalloc object?[n]` **не компилируется** (managed-тип), но `params ReadOnlySpan<object?>`
массив не аллоцирует (компилятор использует inline-массив): замерено 48 B/вызов против 88 B у
`params object?[]` — разница только боксы.

Покрытие: `EntityBuilder.Any`, `EntityBuilder.Count`, `EntityBuilder.Min/Max/Avg/Sum/Stdev/Stdevp/Var/Varp`
(+ `QueryCommand.ExecuteScalar` span). Стриминговые/row sync (`ToList`, `First`, `Single`,
`ToEnumerable`) пока остаются на `params object[]` — массив/энumerator там неизбежен; отдельный шаг.

Изменённые файлы: `Builders/EntityBuilder.cs`, `Query/QueryCommand.cs`, `DataContext/IDataContext.cs`,
`DataContext/DataContext.cs`, `DataContext/InMemoryDataContext.cs`,
`DataContext/Cache/DbPreparedQueryCommand.cs`.

Результат (sync, тот же прогон):

| Arm | Allocated/op | На вызов | Δ |
|---|---:|---:|---:|
| `Nextorm_EntityAny_1Arg_Array` (старое `params`) | 2 856 B | 28.56 B | — |
| `Nextorm_EntityAny_1Arg_Overload` (`Any(object?)`) | 2 824 B | 28.24 B | **−32 B** |

Проверки: сборка Release — 0 ошибок (только 5 пре-существующих CS8602); `nextorm.core.tests` 82/82;
`nextorm.integration.tests` 102 ran / 0 failed (205 skip — нет Docker).

### Верификация

```bash
# 0) сборка
dotnet build nextorm.sln -c Release

# 1) изолированный замер аллокаций params (M2)
./benchmarks/nextorm.benchmark/bin/linux/Release/net10.0/nextorm.benchmark \
  --filter "*ParamsAllocationBenchmark*"
# после фикса ожидание: Nextorm_Any_*Arg_Params Allocated == ..._ReusedArray (Δ → 0)

# 2) целевые бенчмарки
... --filter "*SqliteBenchmarkAny*"
... --filter "*SqliteBenchmarkFirst*"
... --filter "*SqliteBenchmarkSingle*"
... --filter "*InMemoryBenchmarkWhere*"

# 3) тесты (tests/nextorm.integration.tests, tests/nextorm.core.tests)
```

Критерий приёмки: `MemoryDiagnoser.Allocated` для prepared-арм'ов с 1–2 параметрами падает
на ожидаемую величину; время не регрессирует (> шума); тесты зелёные.

### Ожидаемый эффект

- ≤2 параметров: **0 heap-аллокаций** на params (против 32 B при n=1 и 40 B при n=2 —
  подтверждено замером выше).
- Оценка по текущим цифрам: `Nextorm_Any_1Arg` 872 B/вызов → 840 B (−3.7%);
  на 10k rps — ~0.3 MB/s меньше Gen0.
- Async/streaming не ускоряется (span невозможен) — там правки фазы 3 дают только
  устранение двойного копирования.

---

## M1 — стоимость builder-API до plan-cache lookup (уточнено)

**Что НЕ происходит (важно): на cache-hit SQL не пересобирается.** `GetPreparedQueryCommand`
(line 280+) делает структурный lookup: `new QueryPlan(cmd, sql)` → `new QueryPlanCacheKey(GetType(), plan)`
→ `QueryPlanCache.TryGetValue` (per-thread `Dictionary`, хэш по `*PlanHash`-полям команды).
При попадании возвращается уже скомпилированный `DbPreparedQueryCommand` (готовый `DbCommand` + SQL):
`MakeSelectInternal()`, `GetMapCached`, `CreateCommand`, `CreateParam` выполняются **только на miss**.
`QueryPlanEqualityComparer.GetHashCode` использует уже посчитанные `From/Where/Columns/Join/Sorting/
GroupingPlanHash`, а не повторный обход дерева.

**Что происходит на каждом вызове `EntityBuilder.Any(...)` (даже при cache-hit):**
1. `Where(lambda)` — комбинирует предикат в новый expression tree.
2. `ToCommand()` — новый `QueryCommand` + `_joins?.ToArray()` / `_sorting?.ToArray()`.
3. `GetAnyCommand` → `cmd.PrepareCommand(...)`: свежая команда не prepared, поэтому идёт полный
   план-билд (`PrepareFrom/Join/Columns/Where/Grouping/Sorting` + расчёт `*PlanHash`). Обёртка
   `exists(...)` переиспользуется из кэшированного `Lazy<QueryCommand<bool>>`.
4. `GetPreparedQueryCommand` — `new QueryPlan` + `new QueryPlanCacheKey` + lookup.

**Замеры (ShortRun, sqlite, sync):**

| Arm | Mean | Allocated | Что меряет |
|---|---:|---:|---|
| `EntityAnyCommand_Build` | 1.65 µs | 2.25 KB | только builder: свежий `QueryCommand` + `exists` |
| `EntityAnyCommand_BuildAndPrepare` | 5.55 µs | 4.99 KB | builder + полный `PrepareCommand` |
| `Nextorm_EntityAny_1Arg_Span` | 13.0 µs | 2.76 KB | весь публичный путь (cache-hit) |
| `Nextorm_Any_1Arg_Params` (prepared, async) | 12.2 µs | 0.87 KB | `IPreparedQueryCommand` reuse |

**Вывод:** это **не баг кэша**. Чтобы найти план в словаре по значению, всё равно нужно построить
команду и посчитать её хэш — ровно поэтому и существует `Prepare()`/`IPreparedQueryCommand`
(0.87 KB вместо 2.76 KB). Цена builder-API ≈ 1.9 KB/вызов — по замыслу. Реальные (небольшие) рычаги:
- документация/API: `Prepare()` как рекомендованный путь повторного выполнения;
- мемоизация prepared-команды на экземпляре `EntityBuilder` (если инстанс не мутируют после сборки) —
  повторные вызовы на том же `q` → ~0.87 KB; требует проверки мутабельности `Paging`;
- убрать per-call аллокации `QueryPlan`/`QueryPlanCacheKey` (хэшировать `*PlanHash`-поля напрямую) —
  сотни байт, но трогает дизайн ключа кэша.

Отдельно (не perf, а корректность/параллелизм): `GetAnyCommand` через `ReplaceCommand` мутирует
общий кэшированный `AnyCommand`, а `PrepareCommand` использует общий mutable-визитор — см. M9.

### Addendum (после полного прогона бенчмарков — см. M12)

**История решения.** Кэш планов был реализован → **убран владельцем как слишком дорогой**
(«иногда без кэша работает быстрее») → возвращён обратно после вывода M1 «это не баг кэша».

**Что подтвердилось и что нет.** Прямой замер с **выключенным** кэшем
(`QueryCommand.Cache = false`, полный `Job.Default`, out-of-process) показал: «без кэша» —
это **не** более быстрый вариант, а самый медленный из трёх.

| Вариант | на вызов | Alloc/вызов | Ratio |
|---|---:|---:|---:|
| явный `Prepare()` → `IPreparedQueryCommand` | **11.94 µs** | **0.76 KB** | 1.00 |
| implicit plan-cache (как сейчас по умолчанию) | 17.56 µs | 3.76 KB | 1.47 |
| кэш выключен (`Cache = false`) | 42.06 µs | 5.49 KB | **3.52** |

То есть **кэш лучше, чем его отсутствие** (2.4×), и я был неправ, когда в первой редакции M12
заключил обратное. Но **верным оказался другой вывод M1** — про `Prepare()`: он быстрее implicit-кэша
на 5.62 µs/вызов и в 4.94× экономнее по аллокациям. Наблюдение владельца «иногда без кэша быстрее»
на этих данных соответствует не «отключённому кэшу», а **явно подготовленной команде**.

**В чём была ошибка формулировки M1.** Я проверил ровно один вопрос — «пересобирается ли SQL на
cache-hit» — и получил «нет». Это верно. Но вывод я сформулировал как «это **не баг** кэша», что
читается как «кэш полезен», хотя из него следует только «кэш работает». Экономику (с чем сравнивать
и что дешевле) я не проверял вообще; снятие этого вопроса — в M12.

## M3 (сделано)

`ResultSetEnumerator.MoveNextAsync` больше не `async` на горячем пути. Вместо
`await _reader.ReadAsync(ct)` на каждую строку:

- `reader.ReadAsync(ct)` вызывается напрямую, и если задача уже завершилась успешно
  (`IsCompletedSuccessfully`), строка читается и мапится **синхронно** — без async state machine
  и без await-машинерии;
- `async`-продолжение (`AwaitAndReadAsync`) остаётся только для реально асинхронных чтений;
- холодный путь (первый вызов, когда нужно инициализировать ридер) вынесен в
  `InitReaderAndMoveNextAsync`, который просто доинициализирует ридер и переиспользует тот же путь.

Микро-бенчмарк (`MicroOptimizationsBenchmark`, 100 строк, завершённый `Task<bool>` на месте `ReadAsync`):

| Arm | Mean / 100 строк | На строку | Allocated |
|---|---:|---:|---:|
| `M3_Old_AwaitPerRow` | 690.0 ns | 6.90 ns | 0 B |
| `M3_New_FastPathPerRow` | 108.4 ns | 1.08 ns | 0 B |

**Эффект:** −5.8 ns/строка (**6.4×** по времени). Аллокаций не было и нет: `async ValueTask<T>` не боксит
state machine, если await завершается синхронно, поэтому выигрыш чисто CPU-шный (на 1M строк ≈ 5.8 ms).
Маппинг по-прежнему внутри `MoveNextAsync`, `Current` — поле (см. `ResultSetAsyncEnumerable`).

## M4 (сделано)

`QueryCommand.Pipeline` переписан: синхронный `ToEnumerable` в `Task.Run` и unbounded-`Channel` убраны.

| Было | Стало |
|---|---|
| `Task.Run` занимал pool-поток на весь результат set | один async-конвейер без выделенного потока |
| `Channel.CreateUnbounded` буферизовал **весь** результат, если потребитель медленнее | построчная передача без буфера |
| `cancellationToken` проверялся только между строками внутри фонового цикла | `[EnumeratorCancellation]` + токен из `WithCancellation` |
| `Task.Run` + `Channel` + writer-таск на каждый вызов | `await using` enumerator + `while (await MoveNextAsync())` |

Семантика сохранена, pragma `CS8425` удалена, `using System.Threading.Channels` убран.

Проверка (`nextorm.core.tests`):
- `TestFetch` теперь утверждает содержимое: 100 строк, `HaveCount(100)` + `BeEquivalentTo(Range(0, 100))`;
- новый `TestFetch_PipelineStopsOnCancellation`: после `cts.Cancel()` на 10-й строке итерация
  останавливается ровно на 10 (`seen.Should().Be(10)`), а не вычитывает все 100.

## M9 (сделано)

1. **Общий мутируемый `Visitor`.** `ExpressionPlanEqualityComparer.GetHashCode` накапливал `XxHash32`
   в общем поле `_visitor._hash`. Компаратор кэшируется на `QueryCommand` (включая общий
   `AnyCommand`), поэтому два потока, хэширующие разные выражения, интерливят записи в один
   `XxHash32` и получают значение, не согласованное с `Equals`: ключ попадает в `Dictionary`/
   `ConcurrentDictionary` в один bucket, а ищется в другом → недостижимая запись и повторная
   компиляция плана.
   Теперь посетитель — per-(thread, comparer): `[ThreadStatic]` поля + проверка владельца
   (`_tlsVisitorOwner`). Аллокаций не прибавилось: visitor, как и раньше, создаётся лениво и один
   раз на (поток, компаратор).

   **Evidence:** добавлен регресс-тест `GetHashCode_ShouldBeThreadSafe`
   (`tests/nextorm.core.tests/ExpressionPlanEqualityComparerTests.cs`) — 8 выражений × 50 000 вызовов
   в `Parallel.For` со сверкой с однопоточным эталоном. С временно снятым `[ThreadStatic]`
   тест падает с **49 971 / 50 000** неверных хэшей; с фиксом — 0.

2. **`CompareLambda` бросал `InvalidOperationException`** при `TryAdd`-коллизии (один и тот же
   `ParameterExpression` встречается во вложенной лямбде). Теперь возвращает `false`: цена — лишний
   cache miss, а не падение запроса.

> ⚠️ Побочно замечено, **не чинилось** (не perf, а корректность, требует отдельного дизайна):
> `GetAnyCommand` через `ReplaceCommand` мутирует общий кэшированный `AnyCommand`, поэтому при
> конкурентном использовании одного `DataContext` разные предикаты могут увидеть чужой план.

## I1–I6 (ревизия)

| # | Итог |
|---|---|
| I1 | `string.Format` убран из всех «живых» сайтов: `BaseExpressionVisitor.cs:158` → `DataContext.GetParamName` (кэш имён из M8), `DefaultParameterProvider.GetParamName` → растущий кэш `pN`, `SqlServerDataContext.MakeTop` → `$"top({limit})"` (интерполяция `int` не боксит) |
| I2 | LINQ в map-build заменён циклами: 2 сайта в `InMemoryDataContext` + 2 в `DataContext` (закрыты в M11) |
| I3 | остаток 19 `ToList/ToArray` (было 27) — холодные: `_joins?.ToArray()` (снапшот мутабельного списка, только когда join'ы есть), `CloneForCache`, `MakeGenericType` |
| I4 | проверено по коду: все `Expression.Compile()` под `DataContextCache.ExpressionsCache` (`ConcurrentDictionary<ExpressionKey, Delegate>`) или `MapperCache` — one-time на выражение, не на вызов |
| I5 | `LogCommand` (debug-only): `StringBuilder` из `DataContext._sbPool`, `object[]` вместо `ArrayList`, `Append` литералов вместо интерполяции; текст лога не изменился |
| I6 | `.ToString()` 32, в per-row материализаторе 0; единственный в `ResultSetEnumerator` — `sb.ToString()` в debug-логе |

## M5–M8 (сделано)

| # | Что изменено | Файл |
|---|---|---|
| M5 | `ConvertScalar<TResult>`: fast-path `value is TResult`, `long→bool`, `int→long`, `float→double` вместо `IConvertible`-диспетчеризации | `DataContext/DataContext.cs` |
| M6 | `TypeAttributes.HasFlag(...)` → битовая маска; `StartsWith("...")` → `StringComparison.Ordinal` | `TypeExtensions.cs` |
| M7 | `EndsWith/Contains/StartsWith` → `StringComparison.Ordinal` (7 мест) | `Visitors/BaseExpressionVisitor.cs`, `Visitors/CorrelatedQueryExpressionVisitor.cs` |
| M8 | `string.Format("norm_p{0}", i)` → растущий кэш `DataContext.GetParamName(i)` | `DataContext/DataContext.cs`, `DataContext/Cache/DbPreparedQueryCommand.cs` |

Микро-бенчмарк `MicroOptimizationsBenchmark` (ShortRun; эффект на end-to-end ниже шума, поэтому
измеряется сам примитив, старый vs новый):

| Операция | Старое | Новое | Δ |
|---|---:|---:|---|
| M5 `Convert.ToBoolean(long)` | 0.182 ns | 0.150 ns | −18% (шум) |
| M6 `HasFlag(NotPublic)` | 1.204 ns | 1.176 ns | ~0 (шум) |
| M7 `StartsWith("count")` | 10.41 ns | ~0 ns | **−10.4 ns** |
| M7 `EndsWith("distinct")` | 13.55 ns | ~0 ns | **−13.5 ns** |
| M8 `string.Format("norm_p{0}", 7)` | 35.21 ns + 64 B | 0.21 ns + 0 B | **−35 ns, −64 B** |

**Приоритеты по факту:** реальный выигрыш дают **M7 и M8**. M5/M6 улучшены формально, но в пределах
шума: `HasFlag` на `TypeAttributes` в .NET 10 не боксет, а `Convert.ToBoolean(object)` не аллоцирует
(там только interface dispatch). M7 ценен потому, что plan-build (`PrepareCommand`) выполняется на
**каждом** вызове builder-API (см. M1), т.е. проверки имён — фактически на горячем пути.
M8 убирает 64 B на имя параметра, но `ParamMap` кэширует индекс, поэтому это стоимость первого
вызова на скомпилированную команду, а не каждого.

Проверки: Release+Debug — 0 warnings / 0 errors (с `TreatWarningsAsErrors`); `nextorm.core.tests`
82/82; `nextorm.integration.tests` 307 total / 0 failed / 1 skip (sqlite + Postgres + SQL Server via Podman).

## M10–M11 (сделано)

**M10 — sealing.** `sealed` добавлен к `ResultSetEnumerator<TResult>`, `InMemoryEnumerator<TResult,TEntity>`,
`InMemoryCompiledQuery<TResult,TEntity>`, `QueryCommand<TResult>` (наследников нет). Нельзя:
`EntityBuilder<TEntity>` (наследуют `JoinedEntityBuilder<T1,T2>`, `JoinedEntityBuilder<T1,T2,T3>`, …) и `PreparedQueryCommand` (наследуют `DbPreparedQueryCommand`/
`InMemoryCompiledQuery`). Попутно понадобилось: `ResultSetEnumerator.Dispose(bool)` `protected virtual` →
`private`, protected-ctor `QueryCommand<TResult>` → `private` (иначе CS0628 под warnings-as-errors).

**M11 — LINQ в plan/map-build** заменён циклами в `DataContext`:
1. `@params.Any(it => !IsRuntimeParam(it.Name))` → цикл с early-exit;
2. `Parameters.AddRange(@params.Select(it => CreateParam(...)).ToArray())` → цикл с `Parameters.Add`;
3. `SelectList.Select(column => MapColumnExpression(...)).ToArray()` → `new Expression[n]` + цикл;
4. `SelectList.Select(column => Expression.Bind(...)).ToArray()` → `new MemberBinding[n]` + цикл.

Микро-бенчмарк (`MicroOptimizationsBenchmark`, добавлены arm'ы M10/M11):

| Arm | Старое | Новое | Δ |
|---|---:|---:|---|
| M10 интерфейсный вызов (unsealed → sealed) | 30.75 ns | 30.82 ns | **0 (в шуме)** |
| M11 `Any(pred)` | 2.67 ns | 2.06 ns | −0.6 ns |
| M11 `Select().ToArray()` | 24.71 ns + 120 B | 9.87 ns + 48 B | **−15 ns, −72 B** |

**Честно про эффект:** M10 в микро-бенчмарке **выигрыша не дал** — с tiered PGO JIT и так
девиртуализирует мономорфные интерфейсные вызовы. Это структурная правка (сужение API + страховка
на случай отсутствия PGO), а не измеренное ускорение; если важна минимальность публичной поверхности,
можно откатить. M11 — реальные −72 B на каждом из 4 мест, но путь **холодный** (plan-cache miss /
map-cache miss, по разу на форму запроса на поток), поэтому на steady-state не влияет.

Проверки: Release+Debug — 0 warnings / 0 errors; `nextorm.core.tests` 90/90;
`nextorm.sqlite.tests` 22 total / 0 failed / 1 skip (skip = подтверждённый баг из M12);
`nextorm.integration.tests` 307 total / 0 failed / 1 skip (sqlite + Postgres + SQL Server via Podman).

---

## M12 — cached-путь дороже `Prepare()`, но дешевле отказа от кэша (закрыто решением)

**Статус:** 🟢 закрыто решением (2026-09-22). Уточняет M1 (см. addendum там). Это **не** про корректность кэша —
механизм работает (M9 починил единственную гонку). Вопрос — **с чем сравнивать**.

> ⚠️ **Коррекция первой версии.** Первая редакция этого пункта утверждала «кэш не окупается»,
> опираясь на сравнение plan-only арм'а (`Build_Sql` 4.54 µs) с end-to-end накладными (5.32 µs).
> Это было **неверно**: я не померил главную альтернативу — работу **без кэша**
> (`QueryCommand.Cache = false`), а сравнил несравнимые арм'ы. Прямой замер ниже показывает,
> что без кэша **в 2.4× хуже**, а не лучше. Доминирующий факт другой: **и кэш, и его отсутствие
> проигрывают явному `Prepare()`**.

### Декомпозиция (`SqliteBenchmarkCachedPlan`, 100 итераций в цикле → на вызов)

Полный прогон (`NEXTORM_BENCH_FULL=1`, `Job.Default`, out-of-process, ошибки 2–4%):

| Arm | на вызов | Alloc/вызов | Gen1/вызов | Что изолирует |
|---|---:|---:|---:|---|
| `Construct_Only` | 1.12 µs | 1.68 KB | — | только builder: `EntityBuilder.Clone` + `Where` + `Select` |
| `RePrepare_PlanOnly_Param` | **1.63 µs** | 0.78 KB | — | prepare + hash + lookup без построения команды |
| `Cached_PlanOnly_NoParam` | 2.77 µs | 2.66 KB | — | cache-hit без параметров |
| `Cached_PlanOnly_Param` | 3.56 µs | 3.00 KB | — | cache-hit + `ExtractParams` (Δ = 0.79 µs) |
| `Build_Sql` (`storeInCache:false`) | 5.09 µs | 4.54 KB | **0.51** | мимо кэша: SQL строится заново |
| `M12_NoCache_PlanOnly_Param` (`Cache=false`) | 5.36 µs | 4.55 KB | **0.53** | то же, что `Build_Sql` (сходится ⇒ замер корректен) |
| `Build_Sql_Join` | **14.25 µs** | 11.52 KB | **0.86** | SQL с join'ом (alias resolution) |
| `Prepared_ToList` (baseline, DB-bound) | **11.94 µs** | **0.76 KB** | — | `IPreparedQueryCommand` reuse |
| `Cached_ToList` | **17.56 µs** | 3.76 KB | — | полный implicit cached-путь |
| `M12_NoCache_ToList` (`Cache=false`) | **42.06 µs** | 5.49 KB | **0.47** | кэш выключен полностью |

### Выводы

1. **Ранжирование однозначно: `Prepare()` (1.00×) → implicit cache (1.47×) → без кэша (3.52×).**
   Явный `Prepare()` быстрее implicit-кэша на 5.62 µs/вызов и аллоцирует в **4.94× меньше**
   (0.76 против 3.76 KB). Кэш в свою очередь в **2.4×** быстрее полного отказа от кэша.
2. **Кэш всё-таки окупается** относительно пересборки SQL: `Cached_PlanOnly_Param` (3.56 µs) <
   `Build_Sql`/`M12_NoCache_PlanOnly_Param` (5.09/5.36 µs). Отказ от кэша к тому же рождает
   **Gen1 на каждый вызов** (0.47–0.53) из-за пересоздания `DbCommand`+`CreateParameter`,
   тогда как cached-путь даёт **0 Gen1**.
3. **Декомпозиция cache-hit-пути (3.56 µs):** построение команды 1.12 µs (31%) + prepare с
   вычислением `*PlanHash` и lookup 1.63 µs (46%) + `ExtractParams` 0.79 µs (22%). Проверка
   сходимости: 1.12 + 1.63 = 2.75 ≈ 2.77 (`Cached_PlanOnly_NoParam`) ✓ — модель непротиворечива.
4. **Дорого именно вычисление `*PlanHash` на каждом вызове.** `PrepareCommand(bool dontCalculateHash)`
   считает хэши полным обходом дерева (`QueryCommand.cs:541` и аналоги в `PrepareColumns/Join/
   Sorting/Grouping`), а флаг `_dontCache` гейтит их вместе с lookup'ом. На implicit-пути публичный
   API всегда передаёт `dontCalculateHash: false`, поэтому свежая команда **всегда** платит обход.
   Отсюда же — измеренный «потолок»: `RePrepare` (prepare+hash+lookup, без построения) = 1.63 µs.
5. **Роль join'ов подтверждена:** `Build_Sql_Join` 14.25 µs / 11.52 KB / 0.86 Gen1 на вызов —
   вдвое дороже и вдвое «мусорнее» одиночного запроса; это главный источник Gen1 в прогоне.

### Кросс-подтверждение: cached vs `Prepared` (не артефакт одного класса)

| Класс | `Prepared` | `Cached` | Время | Аллокации |
|---|---:|---:|---:|---:|
| `SqliteBenchmarkAny` | 943.2 µs / 85.2 KB | 1438.4 / 352.4 KB | 1.53× | 4.14× |
| `SqliteBenchmarkJoin` | 109.5 / 12.3 KB | 348.0 / 92.2 KB | 3.18× | 7.49× |
| `SqliteBenchmarkWhere` | 950.1 / 100.6 KB | 1699.7 / 485.8 KB | 1.79× | 4.83× |
| `SqliteBenchmarkFirst` | 94.8 / 8.6 KB | 159.1 / 44.1 KB | 1.68× | 5.15× |
| `SqliteBenchmarkSimulateWork` (AsyncStream) | 7.00 ms / 6.24 MB | 57.07 / 30.55 MB | **8.15×** | 4.90× |

Отрывы 1.5–8× при одном и том же SQL и плане — это чистые накладные расходы implicit-пути,
а не разница в выполнении. Направление одно и то же во всех классах.

### Что делать (в порядке отношения эффекта к риску)

1. **Не убирать кэш** — это 2.4× регрессия. Первая редакция M12 это рекомендовала, рекомендация
   снята.
2. **Продвигать `Prepare()`** как штатный способ повторного выполнения: 1.47× по времени и
   ~5× по аллокациям. M1 предлагал ровно это — и это оказался единственный верный рычаг из
   всего M1/M12. Практически: примеры в README/доках + `Prepare()` в `TestDataRepository`-стиле
   репозиториев.
3. **Дать fluent-API паритет с `Prepare()`** — единственная структурная возможность. Цель: снять
   `Construct_Only` (1.12 µs) и/или обход для хэша (1.63 µs). Реалистичный минимум — API вида
   «скомпилировать один раз, выполнять много» поверх уже существующего `IPreparedQueryCommand`,
   чтобы пользователь не собирал дерево на каждый вызов. Мемоизация хэша по *экземпляру*
   выражения не поможет: `EntityBuilder.Where()` каждый раз строит новое дерево.
4. **Убрать per-call `QueryPlan`/`QueryPlanCacheKey`** — по декомпозиции это ~2% (сам объект
   ~50 B из 3.00 KB/call); **не приоритет**. Оставляю как отмеченную мелочь, а не как задачу.

**Что НЕ делать:** не менять вслепую `QueryPlanCacheKey`/`QueryPlanEqualityComparer` — M9 показал,
насколько там тонкая семантика (регресс-тест на потокобезопасность уже добавлен).

### Найдено попутно: баг в cache-hit-ветке — **исправлено**

При написании `docs/specs/performance/prepared-vs-cached.md` дефект был **проверен и подтверждён тестом** (не только
чтением кода): cache-hit-ветка `GetPreparedQueryCommand` игнорировала `createEnumerator`
(`DataContext.cs:449-461` были закомментированы), поэтому форма, впервые выполненная буферизованным
терминалом (`ToList`/`First`/`ExecuteScalar` → `createEnumerator: false`), кэшировалась **без**
`ResultSetEnumerator`, и последующий **потоковый** вызов той же формы падал с `NullReferenceException`
(`compiledQuery.Enumerator!`). Тот же дефект был и в sync-стриминге (`DataContext.CreateEnumerator`).

**Исправление** (в hit-ветке, вместо закомментированного блока):

```csharp
if (createEnumerator && compiledQuery.Enumerator is null)
    compiledQuery.Enumerator = new ResultSetEnumerator<TResult>(compiledQuery);
```

Кэш `[ThreadStatic]`, entry доступна только своему потоку — гонки нет. Для буферизованных вызовов
`createEnumerator == false`, условие замыкается на первом операнде, так что на горячем пути изменений нет.

**Регресс-тесты** (`tests/nextorm.sqlite.tests/PlanCacheTests.cs`): `BufferedThenAsyncStreaming_OnTheSameShape_ShouldNotThrow`,
`BufferedThenSyncStreaming_OnTheSameShape_ShouldNotThrow` (оба падали до фикса с NRE), плюс контракты
`Prepare()`: `Prepared_Default_ShouldNotBeStreamable`, `Prepared_NonStreaming_ShouldSupportStreaming`,
`Prepared_Default_ShouldBeBufferedAndReusable`, `Prepared_ShouldNotPopulateThePlanCache`.

Проверено: Release+Debug 0/0; `nextorm.sqlite.tests` 23/23 (0 skip); `nextorm.core.tests` 90/90;
`nextorm.integration.tests` 307 / 0 failed / 1 skip; стриминговые арм'ы `SqliteBenchmarkIteration`
(`*_AsyncStream`) отрабатывают без исключений.

**Диагностика поверх фикса.** Остался случай «`Prepare()` по умолчанию + стриминг»: команда с
`nonStreamUsing: true` (дефолт) не имеет enumerator'а по замыслу, но падала тем же голым
`NullReferenceException`. Добавлен `DataContext.RequireEnumerator` — вместо разыменования `null` бросается
`InvalidOperationException` с текстом «…created with nonStreamUsing: true … Use Prepare(nonStreamUsing: false) …».
Оба стриминговых входа (`CreateAsyncEnumerator`, `CreateEnumerator`) переведены на него.
Тонкость: `ToEnumerable` — ленивый итератор, поэтому на sync-пути исключение всплывает при старте
перечисления, а не при вызове; тест это фиксирует (`prepared.ToEnumerable(ctx).ToList()`).

### Документация

Написана `docs/specs/performance/prepared-vs-cached.md` (EN, как readme/docs): таблица сравнения, что именно кэшируется и
как считается hit, пошаговая декомпозиция 3.56 µs, семантика `nonStreamUsing`, правила безопасности
(shared mutable `DbCommand`/enumerator), lifetime/инвалидация, гайд по выбору, known problems, команды
воспроизведения. Ссылки добавлены в `readme.md` (Features) и `docs/index.md` (раздел Query reuse).

### Решение (2026-09-22)

Пункт закрыт **решением**, а не правкой кода. Свежий baseline (итерация 9,
`SqliteBenchmarkFeaturesFairCached`, изолированный worktree на `1.0.4-alpha`) подтверждает разрыв
1.11–1.60× (CTE 1.22×, recursive CTE 1.29×, Join4 1.11×, IN 1.40–1.60×) и 7–8× аллокаций; разложение
показывает, что стоимость — это построение и хеширование свежего дерева на каждый вызов
(≈3–9 µs/запрос), причём `QueryPlanEqualityComparer` уже считает по под-хешам (`*PlanHash`,
`CtesPlanHash` через `GetHashCode(subQuery)`), т.е. скрытых повторных обходов под-команд нет.

Вывод: критерий «≤ ±3 % от Dapper» для не-prepared fluent-арм'ов **недостижим безопасной локальной
правкой** — arm Dapper сравнивается с константным SQL, а fluent-путь обязан построить и захешировать
дерево. Закрытие требовало бы структурного паритета fluent с `Prepare()` (shape-keyed кэш подготовленных
команд), что является отдельным крупным рефактором ядра с риском для семантики cache-ключей (см. M9).

Принятое решение:

- **`Prepare()` — санкционированный быстрый путь** повторного выполнения: 1.47× по времени и ~5× по
  аллокациям быстрее implicit-кэша и быстрее Dapper на всех классах. Пользовательские доки и примеры
  уже продвигают его (`docs/guide/15-query-reuse.md`, `prepared-vs-cached.md`).
- **Implicit plan cache сохраняется** (он в 2.4× быстрее полного отказа от кэша) как безопасный
  per-thread дефолт.
- **Fresh-fluent warm-паритет с Dapper снят как цель**; пункт бэклога
  (`todo_warm_path_plan_build.md`) удалён, §4 п.19 помечен closed-by-decision. При необходимости паритет
  оформляется заново как структурная задача M12 #3 с отдельным бенчмарк-гейтом.

---

## Приложение: полный прогон всех бенчмарков (ShortRun, 2026-09-16)

**Команда:** `./nextorm.benchmark --filter "*"` — дефолт `NextormConfig` (`Job.ShortRun`, warmup 3 /
iterations 3, `InProcessEmitToolchain`). **161 кейс**, 22 мин 21 с. Артефакты —
`benchmarks/BenchmarkDotNet.Artifacts/results/*-report-github.md`.

### Достоверность чисел (читать до таблиц)

ShortRun + in-process **не разрешает эффекты в 1–2 нс**: у 16 из 129 методов `Error > Mean`, у ~50 —
больше 20%. BDN не смог поднять приоритет процесса (`Failed to set up priority High ... Permission
denied` — ожидаемо в WSL), GC=Concurrent Workstation.

| Что | Доверие |
|---|---|
| Ранжирование продуктовых бенчмарков (`Sqlite*`, отрывы ≥1.4×) | ✅ стабильно |
| `M3`, `M7` (ранжирование), `M8` (ранжирование), `M11` | ✅ |
| `M6` (−24%) | ⚠️ на грани (ошибка 13–21%) |
| `M5` (−2%), `M10` (−1%) | ❌ шум |
| `BenchmarkQueryCommand.ExpressionPlanEqualityComparer` (190.3 ns ± **597.4 ns**) | ❌ непригодно, нужен полный прогон |
| Абсолюты между сессиями | ❌ плавают ~30% (M3: 690→480 ns old, 108→83 ns new; **отношение стабильно ~6×**) |

### Микро-арм'ы (M2–M11)

| Арм (было → стало) | Δ | Alloc | Ошибка |
|---|---:|---:|---|
| `M3_Old_AwaitPerRow` → `M3_New_FastPathPerRow` | 479.9 → **82.6 ns** | 0 → 0 B | 3.2% / 1.1% ✅ |
| `M7_StartsWith_Culture` → `_Ordinal` | 14.57 → **0.005 ns** | 0 → 0 B | ранжирование ✅ |
| `M7_EndsWith_Culture` → `_Ordinal` | 19.31 → **0.008 ns** | 0 → 0 B | ранжирование ✅ |
| `M8_StringFormat` → `M8_CachedLookup` | 47.48 → **0.257 ns** (185×) | 64 → **0 B** | ✅ |
| `M11_Linq_Any` → `M11_Loop_Any` | 2.914 → **2.011 ns** (−31%) | 0 → 0 B | 9.4% ✅ |
| `M11_Linq_SelectToArray` → `_Loop` | 19.89 → **9.16 ns** (−54%) | 120 → **48 B** | 84% ⚠️ |
| `M6_HasFlag` → `M6_Bitwise` | 2.233 → **1.699 ns** (−24%) | 0 → 0 B | 13–21% ⚠️ |
| `M5_ConvertChangeType` → `M5_FastPath` | 0.2075 → 0.2032 ns (−2%) | 0 → 0 B | ❌ шум |
| `M10_Unsealed` → `M10_Sealed_InterfaceCall` | 42.40 → 41.92 ns (−1%) | 0 → 0 B | ❌ шум |
| `Generic_ParamsArray` → `_ReadOnlySpan` | 0.969 → 0.777 ns (−20%) | 0 → 0 B | ⚠️ не про аллокации (0 args = `Array.Empty`) |
| `GetDbCommand_1Arg_Params` → `_ReusedArray` | 1486.5 → 1406.9 ns | 5600 → **2400 B** | ✅ −3200 B |
| `Nextorm_EntityAny_1Arg_Array` → `_Span` | 13114 → 13013 ns (−0.8%) | 2832 → **2800 B** | ✅ ровно −32 B |
| `Nextorm_Any_1Arg_Params` → `_ReusedArray` | 946687 → 942202 ns (−0.5%) | 87208 → **84008 B** | ✅ −3200 B, время — шум |

**По M-пунктам:** M3 подтверждён и воспроизводим (6.4× в прошлый прогон, 5.8× сейчас — отношение
стабильно, абсолюты нет). M7/M8 — устранение работы, а не ускорение (Ordinal сворачивается JIT в
0.005 ns = floor измерения). M11 реален, но это наносекунды и 72 байта на **холодном** пути.
M2 даёт ровно **−32 B** на вызов публичного API и −3200 B на уровне `GetDbCommand`; время — в шуме.
M5 и M10 — не подтверждены (M10 второй раз подряд; рекомендация откатить остаётся).

### Продуктовые (`Nextorm_Prepared` vs лучший конкурент)

| Класс | Nextorm_Prepared | Лучший конкурент | Отрыв | Аллокации |
|---|---:|---|---:|---:|
| `Any` | **943.2 µs** / 85.2 KB | Dapper 1401.8 / 139.1 KB | 1.49× | 1.63× меньше |
| `Iteration` | **10.78 µs** / 976 B | Dapper 15.83 / 1904 B | 1.47× | 1.95× |
| `First` (scalar) | **94.8 µs** / 8.6 KB | Dapper 146.9 / 16.5 KB | 1.55× | 1.93× |
| `Single` | **95.1 µs** / 8.4 KB | Dapper 148.5 / 16.4 KB | 1.56× | 1.96× |
| `Join` | **109.5 µs** / 12.3 KB | Dapper 201.7 / 21.5 KB | 1.84× | 1.74× |
| `Where` | **942.3 µs** / 92.4 KB | Dapper 1385.0 / 180.7 KB | 1.47× | 1.96× |
| `SimulateWork` | **7.003 ms** / 6.24 MB | Dapper 25.836 / 8.9 MB | **3.69×** | 1.43× |
| `LargeIteration` | 9.10 ms / 2.21 MB | Ado 9.76 / 2.68 MB | 1.07× | 1.21× |

EF Core стабильно в 3.5–8.5× позади, Linq2Db — в 2.9–7.2×, причём в `SimulateWork` Linq2Db
аллоцирует **122.18 MB против 6.24 MB (19.6×)**.

### Прочие наблюдения

- `InMemoryBenchmarkAny`: Nextorm 30.5 ns / **0 B** vs EFCore InMemory 384.6 µs / 481.8 KB, но у
  Nextorm `Error` 168% — **мерить заново**. Сырой LINQ там 2.9 ns (10.6× быстрее) — единственный
  заметный hot-path-разрыв в in-memory.
- `InMemoryBenchmarkIteration`: `NextormPreparedSync` аллоцирует **ровно столько же, сколько сырой
  LINQ** (234.38 vs 234.45 KB), но **0 Gen1 против 12.70 Gen1** у `LinqToList`; цена — 1.34× по
  времени. EF Core InMemory: 2062.7 µs / 3439 KB (14.9×).
- `InMemoryBenchmarkWhere`: Nextorm на 13% быстрее LINQ, но на 31% больше аллокаций (22.68 vs
  17.33 KB); EF Core InMemory — 47 122 KB (2077×).
- `SqliteBenchmarkSimulateWork`: `AsyncStream` в **7× быстрее** `ToListAsync` (7.00 vs 49.04 ms) при
  равных аллокациях — прямое подтверждение M3+M4.
- `SqliteBenchmarkLargeIteration`: все реализации сходятся в 8.6–14.7 ms (I/O-bound), отрыв от
  Dapper 7% — микро-оптимизации на больших выборках бессмысленны.
- `SqliteBenchmarkCache`: кривая амортизации по `Iterations` 1→30; `Dapper` даёт 254→42 µs/итер
  (6× разброс) — артефакт tiered-PGO внутри цикла, колонку Dapper в этом арме использовать нельзя.

### Гигиена бенчмарков

- `SqliteBenchmarkMakeSelect` и `ExpressionsExperiments` — **все `[Benchmark]` закомментированы**;
  классы молча не запускаются (`--filter "*"` → 161 кейс, а не 227 по grep). Восстановить или удалить.
- `SqliteBenchmarkCachedPlan` скрывает `Column.Error`/`Column.StdDev` (`[HideColumns]`) — вернуть,
  иначе не видно, какие из отрывов значимы.
- `M5/M6/M7/M8/M10` и `BenchmarkQueryCommand` нужен `NEXTORM_BENCH_FULL=1` (или `IterationCount`
  10–15), иначе эффекты в 1–2 нс неразрешимы.

---

> ⚠️ **Disclaimer:** Результаты сгенерированы AI-ассистентом и недетерминированы. Возможны
> ложные срабатывания, пропуски и неверные для вашего контекста рекомендации. Проверяйте
> изменения бенчмарками и ревью перед применением в проде.
