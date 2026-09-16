# nextorm vs Dapper / EF Core / linq2db — отчёт по бенчмаркам

Машина: AMD Ryzen 7 5800HS, 16 логических / 8 физических ядер, Linux (WSL2), .NET 10.0.12, BenchmarkDotNet 0.15.8.
БД: `benchmarks/nextorm.benchmark/data/test.db` (SQLite; `simple_entity` ~10k строк, `large_table` ~10k строк).

> **ВАЖНО (методика).** Все прогоны итераций 1–2 выполнялись с БД на `/mnt/c/...` (файловая система WSL↔Windows), где ~0.9 мс **на запрос** тратится на файловый ввод-вывод — это в ~100 раз больше реальной стоимости запроса и полностью маскирует различия ORM. Актуальные и корректные результаты — в разделе **«Итерация 3»**, где БД лежит в tmpfs (`/tmp/nextorm-bench/test.db`), как и задумано в `BenchDb.Resolve()`.

Провайдеры: после итерации 2 nextorm, Dapper, EF Core и linq2db используют **Microsoft.Data.Sqlite**.

## Методика
- `ShortRun` + `InProcessEmitToolchain` (быстрый режим) и `Job.Default` (полный режим, out-of-process).
- Полный режим включается переменной окружения `NEXTORM_BENCH_FULL=1`, путь к БД — `NEXTORM_BENCH_DB=<abs path>`.
- В полном режиме прогонялись: `Any`, `First`, `LargeIteration`, `Cache`, `Where`, `Join`, `Single`.
- Оговорка: nextorm использует System.Data.SQLite, конкуренты — Microsoft.Data.Sqlite, поэтому часть разницы обусловлена ADO-провайдером, а не ORM.

## Сводка по классам (полный режим)

| Класс | Победитель | Nextorm vs лучший конкурент | Аллокации Nextorm |
|---|---|---|---|
| Any | **Nextorm_Prepared 238.4 ms** | Dapper +6.4%, EF_Compiled +10.5%, linq2db +10.6% | 41 KB против 139 KB–1.2 MB |
| First (scalar) | **Nextorm_Cached_Scalar 9.916 ms** | Dapper +3.4%, EF_Compiled +6.2%, linq2db +10.0% | 40.8 KB |
| First (entity) | Dapper 9.981 ms | Nextorm_Cached_Entity 14.605 ms (**+46%**) | 43 KB (но время хуже) |
| Join | **Nextorm_Prepared 9.904 ms** | Dapper +5.1%, linq2db +7.0%, EF +8.0% | 9.26 KB |
| Single | **Nextorm_Prepared 9.848 ms** | Dapper +4.8%, EF +5.8%, linq2db +11.8% | 4.3 KB |
| Where | **Nextorm_CachedForLoop_ToListAsync 99.81 ms** | Dapper +0.84%, linq2db +3.8%, EF +4.7% (ничья) | 48–59 KB |
| LargeIteration (stream) | linq2db 11.41 ms | Nextorm_Prepared_AsyncStream 12.29 ms (**+7.7%**), Cached 12.99 (**+13.8%**) | 2.14 MB |
| LargeIteration (toList) | **Nextorm_Cached_ToListAsync 12.04 ms** | linq2db +16%, EF +27%, Dapper +29% | 2.22 MB |
| Cache (I=1) | linq2db 1.060 ms | NextormPrepared 1.448 ms (**+36.6%**), Cached 1.762 (**+66%**) | — |
| Cache (I=3) | linq2db 3.343 ms | NextormPrepared 3.623 ms (+8.4%) | — |
| Cache (I=5) | linq2db_Compiled 5.136 ms | NextormPrepared 6.216 ms (+21%) | — |
| Cache (I=10) | linq2db_Compiled 10.317 ms | NextormPrepared 10.628 ms (+3.0%) | — |
| Cache (I>=15) | **Nextorm** | Nextorm быстрее на 0.7–2% | — |

## Перепроверка ShortRun-подозрений в полном режиме
- **Any** — подозрение снято: в ShortRun Dapper «выигрывал» 13%, в full Nextorm_Prepared первый (238.4 против 253.6).
- **First** — проигрыш только по **entity**; скаляр наоборот лучший.
- **LargeIteration** — проигрыш только по **AsyncStream**; ToList лучший.
- **Where / Join / Single** — проигрышей нет, Nextorm первый во всех трёх.

## Реальные проигрыши

### 1. First — entity (`LargeEntity?`)
`Nextorm_Cached_Entity 14.605 ms` против `Dapper_Entity 9.981`, `EFCore_Compiled_Entity 10.475`, `Linq2Db_Entity 10.761`.

Причины:
1. Разные ADO-провайдеры (System.Data.SQLite против Microsoft.Data.Sqlite) — главный конфаундер.
2. Non-prepared («Cached») API пересоздаёт план на каждый вызов: `QueryCommand.FirstOrDefaultAsync` (`QueryCommand.cs:996-1014`) в `finally` откатывает `Paging.Limit`, из-за чего условие `Paging.Limit != 1` истинно и `PrepareCommand` выполняется каждый раз. Для entity-проекции (все колонки + MemberInit) это дороже, чем для скаляра.
3. Маппер entity: expression-based `MemberInit` с `Convert`/`IsDBNull` на nullable-колонки (`DbContext.GetMap` 805-884, `MapColumn` 527-542) — дороже IL-маппинга Dapper.
4. `FirstOrDefaultAsync`/`GetDbCommand` без `ConfigureAwait(false)` на горячем пути.

Как исправлять:
1. Уравнять провайдера (сделано: nextorm переведён на Microsoft.Data.Sqlite).
2. Не пере-подготавливать команду для `First/FirstOrDefault/Single/Any` (не откатывать `Paging.Limit`, кэшировать признак подготовленности).
3. Типизированный маппер без `Convert`/бокса, прямые геттеры, кэш ординалов.
4. `ConfigureAwait(false)` в async-путях.

### 2. LargeIteration — AsyncStream
`Nextorm_Prepared_AsyncStream 12.29 ms` / `Cached 12.99` против `Linq2Db_AsyncStream 11.41`, `EFCore_AsyncStream 11.84`. При этом ToList у Nextorm быстрее — значит проблема в async-обвязке.

Причины:
1. Разные ADO-провайдеры (сильнее всего влияет на потоковое чтение).
2. Per-row overhead: `ResultSetEnumerator.MoveNextAsync` (`ResultSetEnumerator.cs:80-87`) делает `await _reader.ReadAsync()` (`Task<bool>`) на каждую строку; `Current` (29-36) маппит через interface-свойство; `IDataContext.GetAsyncEnumerable` (`IDataContext.cs:45-59`) добавляет компиляторный async-iterator и `yield return Current`, без `ConfigureAwait(false)`. Разница ToList vs AsyncStream у Nextorm ≈ 0.95 мс на 10k строк ≈ 95 нс/строку.
3. `ConfigureAwait(false)` пропущен в `IDataContext.GetAsyncEnumerable` (в `IPreparedQueryCommand.ToAsyncEnumerable` он есть).

Как исправлять:
1. Уравнять провайдера.
2. Синхронный fast-path чтения для SQLite (`_reader.Read()` + `ValueTask.FromResult`) — SQLite in-process.
3. Рукописный async-enumerable: объединить чтение и маппинг в `ValueTask<bool> TryReadAsync(out TResult)`.
4. Добавить `ConfigureAwait(false)` в async-путь.

## Ссылки на код
- `src/nextorm.core/DataContext/ResultSetEnumerator.cs` — потоковое чтение/маппинг.
- `src/nextorm.core/DataContext/IDataContext.cs` — `GetAsyncEnumerable`.
- `src/nextorm.core/DataContext/DbContext.cs` — `GetMap`, `MapColumn`, `GetDbCommand`, `FirstOrDefaultAsync`.
- `src/nextorm.core/Query/QueryCommand.cs` — `PrepareCommand`, `First/FirstOrDefault/Single`.
- `src/nextorm.sqlite/SqliteDbContext.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs` — провайдер.

## Воспроизведение
```bash
# полный прогон одного класса
NEXTORM_BENCH_FULL=1 \
NEXTORM_BENCH_DB=$PWD/benchmarks/nextorm.benchmark/data/test.db \
dotnet run -c Release --project benchmarks/nextorm.benchmark -- --filter "*SqliteBenchmarkFirst.*"
```

---

# Итерация 2 — переход на Microsoft.Data.Sqlite + исправления (1.2, 1.3, 1.4, 2.3, 2.4)

## Что сделано
- **Провайдер**: `nextorm.sqlite` переведён с `System.Data.SQLite` на `Microsoft.Data.Sqlite` 10.0.12 (как у Dapper/EF/linq2db). Теперь сравнение apples-to-apples.
  - `SqliteDbContext` — `SqliteConnection`/`SqliteParameter`.
  - `SQLiteFunctions` — регистрация `stdev`/`stdevp` через `SqliteConnection.CreateAggregate` (нет attribute-based регистрации как в System.Data.SQLite).
  - Нормализация `null` → `DBNull.Value` для параметров (Microsoft.Data.Sqlite бросает "Value must be set.").
  - Бенчмарки `Cache`/`SimulateWork` и тесты переведены на Microsoft.Data.Sqlite.
- **1.2** — `First/FirstOrDefault/Single/SingleOrDefault` больше не откатывают `Paging.Limit`: форма single-row остаётся на команде, поэтому повторные вызовы не пересобирают план (ключ `QueryPlanEqualityComparer` включает Limit/SingleRow).
- **1.3** — `DbContext.MapColumn`: убран лишний `Expression.Convert` для ссылочных типов, типизированные геттеры и постоянные ординалы.
- **1.4** — `ConfigureAwait(false)` в `GetDbCommand`/`ExecuteScalar` async-путях.
- **2.3** — `ResultSetEnumerator` маппит строку внутри `MoveNext/MoveNextAsync`, `Current` — чтение поля; добавлен рукописный `ResultSetAsyncEnumerable<TResult>` вместо компиляторного async-iterator (убран per-row state machine и лишний диспатч).
- **2.4** — `ConfigureAwait(false)` в `ResultSetEnumerator.MoveNextAsync`, `IDataContext.CreateEnumeratorAsync`.

Проверки: `test/nextorm.integration.tests` 102/102, `test/nextorm.core.tests` 35/35 — зелёные.

## Результаты (полный режим, after)

### First — entity
| Метод | до | после |
|---|---|---|
| Nextorm_Prepared_Entity | 24.635 ms | **9.166 ms (−63%)** |
| Nextorm_Cached_Entity | 14.605 ms | **9.578 ms (−34%)** |
| Nextorm_PreparedForLoop_Entity | 19.905 ms | 9.440 ms (−53%) |
| Dapper_Entity | 9.981 ms | 9.474 ms |
| EFCore_Compiled_Entity | 10.475 ms | 9.976 ms |
| Linq2Db_Entity | 10.761 ms | 10.226 ms |

Итог: Nextorm_Prepared_Entity — лучший среди ORM, Nextorm_Cached_Entity в пределах ~1% от Dapper. Проигрыш устранён.

### LargeIteration — AsyncStream
| Метод | до | после |
|---|---|---|
| Nextorm_Prepared_AsyncStream | 12.29 ms | **10.069 ms (−18%)** |
| Nextorm_Cached_AsyncStream | 12.99 ms | **9.928 ms (−24%)** |
| Dapper_AsyncStream | 12.88 ms | 10.527 ms |
| Linq2Db_AsyncStream | 11.41 ms | 10.969 ms |
| EFCore_AsyncStream | 11.84 ms | 11.462 ms |

Итог: Nextorm — 1-е и 2-е места в стриминге; ToList 10.18/10.24 против 13.04 у linq2db. Проигрыш устранён.

### Cache
| Iterations | до | после | linq2db |
|---|---|---|---|
| 1 | 1.448 ms | 1.267 ms | 0.978 ms (+29%) |
| 3 | 3.623 ms | 3.199 ms | 2.923 ms (+9%) |
| 5 | 6.216 ms | 5.039 ms | 4.974 ms (+1.3%) |
| 10 | 10.628 ms | 9.709 ms | 9.597 ms (+1.2%) |
| 15 | 15.733 ms | **14.278 ms** | 14.373 ms (Nextorm) |
| 20 | 20.981 ms | **19.048 ms** | 19.177 ms (Nextorm) |
| 30 | 30.297 ms | **28.140 ms** | 28.826 ms (Nextorm) |

Разрыв сократился; на I≥15 Nextorm первый. На I=1–3 linq2db ещё впереди (его `CompiledQuery`).

### Остальные классы (after, Nextorm — место)
| Класс | Победитель | Nextorm |
|---|---|---|
| Single | Nextorm_Prepared 9.194 ms | 1-е |
| Join | Nextorm_Prepared 9.074 ms | 1-е |
| Where | Nextorm_CachedForLoop 86.89 ms | 1–3 места |
| Iteration | Nextorm_Prepared_ToList 858 us | 1-е |
| SimulateWork | Nextorm_Prepared_AsyncStream 8.0 ms | 1-е |
| Any | Linq2Db 88.16 ms | Nextorm_Prepared 91.33 ms (2-е, +3.5%) |

## Вывод
Пункты 1.2/1.3/1.4/2.3/2.4 + паритет провайдера убрали оба реальных проигрыша: **First (entity)** и **LargeIteration (AsyncStream)**.

---

# Итерация 3 — быстрая БД (tmpfs) + доработки `ExecuteScalar` и `Any`

## Что сделано
- **БД перенесена в tmpfs** (`/tmp/nextorm-bench/test.db`, путь по умолчанию в `BenchDb.Resolve()`). Это устранило ~0.9 мс/запрос накладных расходов файловой системы WSL↔Windows.
- **`ExecuteScalar<TResult>`**: добавлен быстрый типизированный путь `ConvertScalar<TResult>` для `bool/int/long/double` (через `Unsafe.As`) — SQLite отдаёт `EXISTS`/сравнения как `long`, раньше шёл `Convert.ChangeType` (боксинг + `IConvertible`).
- **`Any`/`AnyAsync`**: больше не откатывают `IgnoreColumns` и не пере-подготавливаются при повторном вызове; `GetAnyCommand` не готовит `cmd` повторно, если он уже подготовлен.

Тесты: `test/nextorm.integration.tests` 102/102, `test/nextorm.core.tests` 35/35.

## Результаты (полный режим, tmpfs)

### Any (100 запросов)
| Метод | Mean | Allocated |
|---|---|---|
| **Nextorm_Prepared** | **917.6 µs** | 85.16 KB |
| Nextorm_Cached | 1 310.7 µs | 349.25 KB |
| Dapper | 1 362.9 µs | 139.06 KB |
| Linq2Db | 2 498.4 µs | 401.56 KB |
| EFCore_Compiled | 3 553.3 µs | 807.03 KB |
| EFCore | 5 969.7 µs | 1211.76 KB |

Nextorm_Prepared в 1.5× быстрее Dapper и в 2.7× linq2db. На `/mnt/c` этот же класс показывал «проигрыш» linq2db на 3.5% — это был артефакт FS. Две правки кода дали эффект в пределах шума (главной была локация БД).

### First (10 запросов)
| Метод | Mean |
|---|---|
| Nextorm_Prepared_Scalar | **92.74 µs** |
| Nextorm_Prepared_Entity | 103.15 µs |
| Nextorm_PreparedForLoop_Entity | 132.05 µs |
| Nextorm_Cached_Scalar | 138.15 µs |
| Dapper_Scalar | 145.83 µs |
| Nextorm_Cached_Entity | 155.81 µs |
| Dapper_Entity | 161.98 µs |
| EFCore_Compiled_Scalar | 348.44 µs |
| EFCore_Compiled_Entity | 388.22 µs |
| Linq2Db | ~607 µs |

Nextorm — первые 4 места; entity теперь быстрее Dapper.

### LargeIteration
| Метод | Mean |
|---|---|
| **Nextorm_Cached_ToListAsync** | **8.410 ms** |
| Nextorm_Prepared_ToListAsync | 8.425 ms |
| Nextorm_Cached_AsyncStream | 8.612 ms |
| Nextorm_Prepared_AsyncStream | 8.675 ms |
| Dapper_AsyncStream | 8.986 ms |
| Linq2Db_AsyncStream | 9.305 ms |
| EFCore_AsyncStream | 10.037 ms |
| Linq2Db_ToListAsync | 11.670 ms |
| EFCore_ToListAsync | 13.588 ms |

### Остальные (Nextorm — 1-е место во всех)
| Класс | Победитель | Ближайший конкурент |
|---|---|---|
| Single | Nextorm_Prepared 92.16 µs | Dapper 144.80 µs |
| Join | Nextorm_Prepared 104.6 µs | Dapper 199.0 µs |
| Where | Nextorm_Prepared_AsyncStream 899 µs | Dapper_Async 1334 µs |
| Iteration | Nextorm_PreparedManualSql 10.31 µs | Dapper 15.01 µs |
| SimulateWork | Nextorm_Prepared_AsyncStream 6.72 ms | Dapper_AsyncStream 24.3 ms |

### Cache — «отставание» оказалось артефактом теста
`SqliteBenchmarkCache.NextormPrepared` вызывал `Prepare()` **внутри** измеряемого метода и создавал новую команду на каждый invocation BenchmarkDotNet. `QueryCommand.Prepare()` идёт по ветке `GetPreparedQueryCommand(this, createEnumerator:false, storeInCache:false)` — кэш плана не используется, поэтому план строился заново на каждый invocation. Сам prepared-механизм ничего не пересоздаёт: в цикле `cmd.ToList(...)` выполняется уже скомпилированная команда.

После выноса `Prepare()` в конструктор (и передачи параметра `i` — раньше prepared-запрос выполнялся с NULL-параметром и не возвращал строк):

| Iterations | NextormPrepared | Linq2Db_Compiled | Dapper | NextormCached (purge) |
|---|---|---|---|---|
| 1 | **8.95 µs** | 14.77 µs | 236.5 µs | 264.9 µs |
| 5 | **50.4 µs** | 83.9 µs | 744.2 µs | 364.0 µs |
| 10 | **100.4 µs** | 171.1 µs | 814.8 µs | 471.2 µs |
| 20 | **204.3 µs** | 346.0 µs | 995.2 µs | 700.8 µs |
| 30 | **305.4 µs** | 514.8 µs | 1 186.1 µs | 906.1 µs |

`NextormPrepared` — первый на всех значениях; маржинальная стоимость запроса ~10 µs против ~17 µs у `linq2db_compiled`.

Реальная «холодная» стоимость построения плана ~250 µs видна в `NextormCached` (он намеренно очищает кэш плана каждый invocation) — это цена одиночного/холодного запроса, но не prepared-механизма.

## Итог
На корректной быстрой БД Nextorm выигрывает все классы, включая оба прежних проигрыша (First entity, LargeIteration AsyncStream) и Cache. Prepared-механизм работает как задумано — компилирует команду один раз, дальше только выполняет. Единственная реальная слабость — **стоимость холодного построения плана (~250 µs)** для не-prepared запросов (`NextormCached`), а не prepared-механизма.

---

# Группировка бенчмарков по категориям

Правило сопоставления:
- **Категория A (подготовленные/компилированные):** `Nextorm_*Prepared*` ⟷ `EFCore_*Compiled*` ⟷ `Linq2Db_Compiled` ⟷ `Dapper` (raw SQL, Dapper кэширует IL по тексту запроса).
- **Категория B (кешированные/обычные):** `Nextorm_*Cached*` ⟷ `EFCore_*` (без `_Compiled`) ⟷ `Linq2Db` (без `_Compiled`).

Оговорки по классификации:
- `Nextorm_PreparedForLoop_*` фактически использует кешированный API (`Entity.FirstOrDefaultAsync/ToListAsync`) внутри измеряемого метода, поэтому отнесён к категории B, несмотря на имя.
- В `Cache` `NextormCached`/`Dapper` намеренно очищают кэш плана каждый invocation, а `Linq2Db` — нет; это делает сравнение B в этом классе не симметричным (холодный vs тёплый кэш).
- `AdoTupleToList`/`AdoWithDelegate` (LargeIteration) — сырой ADO.NET, вне категорий (базовая линия).

## Категория A — Prepared / Compiled / Dapper (mean)
| Бенчмарк | Nextorm_Prepared | EF_Compiled | linq2db_Compiled | Dapper |
|---|---|---|---|---|
| Any (100×) | **917.6 µs** | 3 553.3 µs | — | 1 362.9 µs |
| Single (10×) | **92.16 µs** | 362.22 µs | — | 144.80 µs |
| First scalar (10×) | **92.74 µs** | 348.44 µs | — | 145.83 µs |
| First entity (10×) | **103.15 µs** | 388.22 µs | — | 161.98 µs |
| Join (10×) | **104.6 µs** | 510.3 µs | — | 199.0 µs |
| Where (stream / list) | **899.3 / 903.8 µs** | 3 131.4 µs | — | 1 334.3 / 1 348.2 µs |
| Iteration (manual / stream / list) | **10.31 / 10.51 / 10.55 µs** | 35.77 µs | — | 15.01 / 15.35 µs |
| LargeIteration (stream / list) | **8.675 / 8.425 ms** | 10.021 ms | — | 8.986 / 11.651 ms |
| SimulateWork (stream / list) | **6.724 / 47.217 ms** | 106.143 ms | — | 24.314 / 67.660 ms |
| Cache I=1 | **8.95 µs** | — | 14.77 µs | 236.5 µs |
| Cache I=30 | **305.4 µs** | — | 514.8 µs | 1 186.1 µs |

Итог A: Nextorm_Prepared первый во всех классах, включая против `linq2db.CompiledQuery` и `EF.CompileAsyncQuery`.

## Категория B — Cached / Regular (mean)
| Бенчмарк | Nextorm_Cached | EF (regular) | linq2db (regular) |
|---|---|---|---|
| Any (100×) | **1 310.7 µs** | 5 969.7 µs | 2 498.4 µs |
| Single (10×) | **143.59 µs** | 697.72 µs | 624.16 µs |
| First scalar (10×) | **138.15 µs** | — | 607.75 µs |
| First entity (10×) | **155.81 µs** | — | 606.78 µs |
| Join (10×) | **260.3 µs** | 814.8 µs | 534.1 µs |
| Where (cached-for / list / stream) | **1 107.3 / 1 410.5 / 1 424.3 µs** | 6 573.6 / 6 527.7 µs | 2 503.1 / 2 549.3 µs |
| Iteration (list / manual) | **10.74 / 10.81 µs** | — | 15.85 / 15.95 µs |
| LargeIteration (list / stream) | **8.410 / 8.612 ms** | 13.588 / 10.037 ms | 11.670 / 9.305 ms |
| SimulateWork (stream / list) | **41.347 / 90.800 ms** | — | 364.654 ms |
| Cache I=1 (cold vs warm) | 264.9 µs | — | 28.0 µs* |
| Cache I=30 (cold vs warm) | 906.1 µs | — | 967.9 µs* |

\* `Cache` B-сравнение не симметрично: `NextormCached` очищает кэш плана на каждый invocation, `Linq2Db` — нет.

Итог B: Nextorm_Cached первый во всех классах, кроме `Cache` при малых I (холодный кэш против тёплого у linq2db).

---

# Итерация 4 — SQL-ключевой кэш маппера (устранение холодного плана)

## Замеры: из чего состоял холодный build
| Компонент | µs/op |
|---|---|
| `PrepareCommand` (визиторы) | 19.4 |
| `Expression plan hash` (where) | 13.1 |
| `QueryPlan hash` (полный ключ плана) | 14.9 |
| **SQL-string dict lookup** | **0.10** |
| **`Expression.Compile()` в `GetMap`** | **~285** |
| Полный `Prepare()` | 292.6 |

Вывод: почти всё — `Expression.Compile()`. Ключ не обязан быть выражением: SQL уже сгенерирован к моменту `GetMap`, поэтому ключ по SQL стоит ~0.1 µs. Так же делает linq2db: материализаторы кэшируются в статическом `MemoryCache<QueryKey, Delegate>`, где `QueryKey` включает **SQL text + TargetType + DbReaderType + ConfigId** (а дерево выражения используется только для кэша самих запросов).

## Реализация
- Новый `src/nextorm.core/DataContext/MapperCache.cs`: статический `ConcurrentDictionary<MapperCacheKey, Delegate>`, ключ `(ResultType, Sql, ColumnsSignature, OneColumn)`, лимит 4096 записей (при переполнении новые формы не кэшируются — без утечки).
- `DbContext.GetMapCached(queryCommand, sql)` — используется в `GetPreparedQueryCommand`; при промахе строит маппер через `GetMap` и кладёт в кэш. Публичный `GetMap` не изменён.
- Маппер провайдеро- и контекст-независим (работает только с `IDataRecord`), поэтому кэш общий для всех `DbContext` и переживает `PurgeQueryCache`.
- Тесты: 102/102 и 35/35 зелёные.

## Результат (`Cache`, полный режим, tmpfs)
| Iterations | NextormPrepared | NextormCached до | NextormCached после | Linq2Db | Linq2Db_Compiled | Dapper |
|---|---|---|---|---|---|---|
| 1 | **9.2 µs** | 264.9 µs | **37.9 µs** | 33.4 µs | 16.2 µs | 252.9 µs |
| 3 | **31.1 µs** | 318.0 µs | **92.0 µs** | 97.3 µs | 53.9 µs | 762.7 µs |
| 5 | **52.6 µs** | 364.0 µs | **138.7 µs** | 164.6 µs | 93.0 µs | 755.0 µs |
| 10 | **106.6 µs** | 471.2 µs | **255.6 µs** | 328.9 µs | 161.7 µs | 890.5 µs |
| 15 | **147.5 µs** | 593.1 µs | **339.1 µs** | 477.4 µs | 244.2 µs | 889.6 µs |
| 20 | **200.6 µs** | 700.8 µs | **439.7 µs** | 642.7 µs | 338.1 µs | 977.6 µs |
| 30 | **299.9 µs** | 906.1 µs | **638.5 µs** | 942.9 µs | 499.4 µs | 1135.1 µs |

Холодный build упал с ~293 µs до ~30 µs: `NextormCached` ускорился на I=1 в 7 раз (265 → 38 µs), на I=30 — на 30% (906 → 639 µs). Теперь `NextormCached` обгоняет обычный `Linq2Db` начиная с I=3, а `NextormPrepared` стабильно быстрее `Linq2Db_Compiled` на всех значениях.

Примечание по методике: параллельный запуск второго BDN-прогона конфликтует за сгенерированный проект `nextorm.benchmark-DefaultJob-1` и даёт `NullReferenceException`/`DirectoryNotFoundException` и мусорные времена. Все числа выше получены в изолированном прогоне (копия репозитория на `/tmp`).

---

# Проверка остальных классов в коротком режиме (sanity)

ShortRun / InProcess, tmpfs, изолированная копия. Разброс большой, важен порядок, не абсолют.

| Класс | Победитель (Nextorm) | Ближайший конкурент |
|---|---|---|
| Single | Nextorm_Prepared 93.4 µs | Dapper 157.2 µs |
| Any | Nextorm_Prepared 942 µs | Dapper 1 571 µs, linq2db 2 725 µs |
| Join | Nextorm_Prepared 114.8 µs | Dapper 203.6 µs |
| Where | Nextorm_CachedForLoop 963 µs | Dapper 1 462 µs |
| First scalar | Nextorm_Prepared_Scalar 96.5 µs | Dapper_Scalar 149.5 µs |
| First entity | Nextorm_Prepared_Entity 109.8 µs | Dapper_Entity 163.7 µs |
| LargeIteration | Nextorm_Cached_AsyncStream 9.16 ms | linq2db 9.52 ms, Dapper 10.24 ms |
| Iteration | Nextorm_Prepared_ToList 10.45 µs | Dapper 15.49 µs |
| SimulateWork | Nextorm_Prepared_AsyncStream 7.04 ms | Dapper_AsyncStream 26.3 ms |

Порядок совпадает с полными прогонами: Nextorm первый во всех классах.

---

# In-memory: EF Core InMemory (Compiled) vs чистый LINQ vs nextorm InMemory (Prepared)

Машина: AMD Ryzen 7 5800HS, 16 логических / 8 физических ядер, Linux (WSL2), .NET 10.0.12, BenchmarkDotNet 0.15.8.
Режим: `Job.Default` (out-of-process, полный); `MemoryDiagnoser`.

Данные: 10 000 сущностей `SimpleEntity` (одна INT-колонка `id`), у каждого участника — своя копия в памяти.
Сравниваются только «быстрые» пути: **nextorm — только Prepared**, **EF Core — только Compiled**.

Участники:
- **Чистый LINQ** — `List<SimpleEntity>`.
- **nextorm InMemory** — `InMemoryContext` + `Entity<SimpleEntity>.WithData(...)`, только `*.Prepare(...)`.
- **EF Core InMemory** — `Microsoft.EntityFrameworkCore.InMemory` 10.0.12, `EFInMemoryDataContext` (`ValueGeneratedNever` для `id`, `NoTracking`), только `EF.CompileQuery(...)`.

> **linq2db не участвует.** У linq2db нет in-memory провайдера для объектов (в его документации прямо есть «No way to work with in-memory database» именно в этом смысле). Единственная опция — SQLite `Data Source=:memory:`, но это настоящий реляционный движок в RAM, а не in-memory контекст над объектами; эмулировать его и сравнивать с object-store участниками некорректно, поэтому он исключён.

> **Методическая оговорка.** В исходном виде Void-бенчмарки без потребления результата JIT выбрасывал как мёртвый код — out-of-process прогон занижал `Where` примерно в 100 раз. Все измеряемые методы теперь аккумулируют результат в поле `_sink`, так что работа не может быть удалена. Отдельный stopwatch-прогон тех же методов подтвердил, что работа реально выполняется (порядок участников совпадает; абсолютные значения там выше из-за не разогретого Tier1-кода).

Что было исправлено по пути:
- **Блокирующий баг `InMemoryContext`.** `GetPreparedQueryCommand` падал с `NullReferenceException`, если `queryCommand.Cache == true`, а `storeInCache == false` (путь `QueryCommand.Prepare`): `queryPlan` не создавался, но использовался через `queryPlan!`. Теперь запись в кэш планов идёт под тем же условием `storeInCache && queryCommand.Cache`, что и в `DbContext`. До фикса **все** in-memory бенчмарки возвращали `NA`.
- В `nextorm.benchmark` добавлены пакет `Microsoft.EntityFrameworkCore.InMemory` и контекст `EFInMemoryDataContext` (`benchmarks/nextorm.benchmark/EfInMemory.cs`).

## Что оптимизировано в in-memory провайдере

Профилирование горячего пути показало переплату nextorm над LINQ ≈ 2–3.3 ns/строка. Реализовано (R1–R7):

1. **R1. Индексный fast-path для `List<T>`/`T[]`** (`InMemoryEnumerator.Init`/`MoveNext`): убран interface-диспатч `IEnumerator<T>` и бокс `List<T>.Enumerator` (те самые 32 B/вызов на `Any`); обход по индексу, текущий элемент кэшируется.
2. **R2. Типизированный предикат** (`InMemoryCompiledQuery.ConditionFactory` + `InMemoryDataContext.BuildConditionFactory` + `Visitors/TypedParamVisitor.cs`): параметры из `object[]` распаковываются один раз на запрос, а не на каждой строке — на строку больше нет индексации массива и unbox.
3. **R3. Одно чтение текущего элемента** на строку (было два: в условии и в `Current`).
4. **R4. Убран `yield`-слой** `IDataContext.GetEnumerable` для in-memory: `InMemoryContext.GetEnumerable` возвращает сам enumerator (`InMemoryEnumerator`), минуя state machine.
5. **R5. Кэш per-call резолвера** (`InMemoryCacheEntry.Resolver`): повторные вызовы только переинициализируют enumerator, не повторяя разрешение данных/joins/sorting.
6. **R6. `Any`/scalar** идут через тот же резолвер — лишняя аллокация enumerator'а ушла (`Any` теперь 0 B).
7. **R7. `ToList`/`ToListAsync`**: `Offset`/`Limit` вынесены в локальные переменные (без повторного чтения `Paging` на строку).

Тесты после изменений: `test/nextorm.core.tests` 35/35, `test/nextorm.integration.tests` 102/102.

## До/после (полный режим, изолированный прогон)

| Сценарий | nextorm до | nextorm после | Δ | LINQ | до vs LINQ | после vs LINQ |
|---|---:|---:|---:|---:|---:|---:|
| Any | 31.4 ns / 32 B | **22.0 ns / 0 B** | −30%, alloc −32 B | 2.52 ns | 12.7× | 8.7× |
| Iteration stream | 116.3 µs | **99.9 µs** | −14% | 75.3 µs | 1.40× | 1.33× |
| Iteration ToList | 150.3 µs | **137.5 µs** | −9% | 95.4 µs | 1.48× | 1.44× |
| Where (100×) | 5.046 ms | **2.514 ms** | **−50%** | 2.835 ms | 1.69× | **0.89×** |

> Число `Iteration 155.8 µs`, полученное в одном из прогонов, было испорчено параллельным BDN-прогоном (конфликт за `nextorm.benchmark-DefaultJob-*`). Все числа «после» выше — из изолированного прогона.

## Any — один `Any()` по 10 000 строк
| Метод | Mean | Allocated | vs LINQ |
|---|---:|---:|---:|
| **Linq** | **2.518 ns** | — | 1× |
| NextormPrepared | 22.046 ns | 0 B | 8.7× |
| EFCoreInMemory_Compiled | 283.56 µs | 481.8 KB | ~112 600× |

`Any()` без предиката: LINQ и nextorm проверяют первый элемент (nextorm — через свой enumerator), а EF InMemory прогоняет полный query pipeline и на этом классе стоит ~0.28 мс независимо от данных. У nextorm аллокаций больше нет.

## Iteration — полный обход 10 000 строк с проекцией `{ Id }`
| Метод | Mean | Allocated | vs LINQ |
|---|---:|---:|---:|
| **Linq** | **75.33 µs** | 234.45 KB | 1× |
| LinqToList | 95.39 µs | 312.59 KB | 1.27× |
| NextormPreparedSync (baseline) | 99.91 µs | 234.38 KB | 1.33× |
| NextormPreparedSyncToList | 137.49 µs | 312.55 KB | 1.83× |
| EFCoreInMemory_Compiled | 1 467.34 µs | 3.44 MB | 19.5× |
| EFCoreInMemory_Compiled_ToList | 1 575.88 µs | 3.57 MB | 20.9× |

На полном обходе nextorm InMemory (prepared) держится в пределах 1.33–1.83× от LINQ (разница — обёртка `Entity<T>`/enumerator), EF InMemory (compiled) — в ~19.5–20.9× медленнее и аллоцирует в ~15 раз больше (3.4–3.6 МБ на проход).

## Where — 100 запросов `Id == i`, `i = 0..99`
| Метод | Mean | на запрос | Allocated | vs LINQ |
|---|---:|---:|---:|---:|
| **NextormPreparedParam (baseline)** | **2.514 ms** | **25.1 µs** | 22.66 KB | **0.89×** |
| Linq | 2.835 ms | 28.4 µs | 17.28 KB | 1× |
| EFCoreInMemory_Compiled | 48.938 ms | 489.4 µs | 47.12 MB | 17.3× |

После оптимизаций nextorm InMemory (prepared по параметру) **быстрее чистого LINQ на ~11%** — сказывается фузия filter+map в одном enumerator'е и типизированный предикат вместо цепочки `Where`+`Select`. Цена — per-query closure типизированного предиката: аллокации 22.66 KB против 19.53 KB до оптимизации (и 17.28 KB у LINQ). EF InMemory (compiled) — в 17.3× медленнее и суммарно аллоцирует ~47 МБ на 100 запросов.

## Вывод
- **Чистый LINQ** — базовая линия без ORM-обвязки; после оптимизаций на точечных выборках nextorm его обгоняет.
- **nextorm InMemory (Prepared)** после R1–R7: 1.33–1.83× на обходе, **0.89× на выборках** (быстрее LINQ), ~8.7× на одиночном `Any()` при **нулевых аллокациях**. Основной выигрыш дали индексный fast-path (R1) и типизированный предикат (R2); `Where` ускорился вдвое.
- **EF Core InMemory (Compiled)** — самый медленный и самый аллокационно тяжёлый во всех трёх классах: ~19.5–20.9× на обходе, ~17.3× на выборках и ~112 600× на `Any()`. Это ожидаемо: InMemory-провайдер EF — полноценный ORM-конвейер (модель, кэш запросов, материализация, change tracker), а не словарь «ключ → значение», и предназначен для тестов, а не для производительности.
- Реализованы пункты R1–R7; регрессий нет: `test/nextorm.core.tests` 35/35, `test/nextorm.integration.tests` 102/102.

## Воспроизведение
```bash
NEXTORM_BENCH_FULL=1 dotnet run -c Release --project benchmarks/nextorm.benchmark -- --filter "*InMemoryBenchmark*"
```
Классы: `InMemoryBenchmarkAny`, `InMemoryBenchmarkIteration`, `InMemoryBenchmarkWhere`; EF-контекст — `benchmarks/nextorm.benchmark/EfInMemory.cs`.
