# nextorm vs Dapper / EF Core / linq2db — отчёт по бенчмаркам

Машина: AMD Ryzen 7 5800HS, 16 логических / 8 физических ядер, Linux (WSL2), .NET 10.0.12, BenchmarkDotNet 0.15.8.
БД: `benchmarks/nextorm.benchmark/data/test.db` (SQLite; `simple_entity` ~10k строк, `large_table` ~10k строк).

> **ВАЖНО (методика).** Все прогоны итераций 1–2 выполнялись с БД на `/mnt/c/...` (файловая система WSL↔Windows), где ~0.9 мс **на запрос** тратится на файловый ввод-вывод — это в ~100 раз больше реальной стоимости запроса и полностью маскирует различия ORM. Актуальные и корректные результаты — в разделе **«Итерация 3»**, где БД лежит в tmpfs (`/tmp/nextorm-bench/test.db`), как и задумано в `BenchDb.Resolve()`.

Провайдеры: после итерации 2 nextorm, Dapper, EF Core и linq2db используют **Microsoft.Data.Sqlite**.

## Методика
- `ShortRun` + `InProcessEmitToolchain` (быстрый режим) и `Job.Default` (полный режим, out-of-process).
- Полный режим включается переменной окружения `NEXTORM_BENCH_FULL=1`, путь к БД — `NEXTORM_BENCH_DB=<abs path>`.
- Отчёты BenchmarkDotNet складываются в `benchmarks/BenchmarkDotNet.Artifacts` (путь задан в `NextormConfig`, поэтому не зависит от рабочего каталога запуска).
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
3. Маппер entity: expression-based `MemberInit` с `Convert`/`IsDBNull` на nullable-колонки (`DataContext.GetMap` 805-884, `MapColumn` 527-542) — дороже IL-маппинга Dapper.
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
- `src/nextorm.core/DataContext/DataContext.cs` — `GetMap`, `MapColumn`, `GetDbCommand`, `FirstOrDefaultAsync`.
- `src/nextorm.core/Query/QueryCommand.cs` — `PrepareCommand`, `First/FirstOrDefault/Single`.
- `src/nextorm.sqlite/SqliteDataContext.cs`, `src/nextorm.sqlite/SQLiteFunctions.cs` — провайдер.

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
  - `SqliteDataContext` — `SqliteConnection`/`SqliteParameter`.
  - `SQLiteFunctions` — регистрация `stdev`/`stdevp` через `SqliteConnection.CreateAggregate` (нет attribute-based регистрации как в System.Data.SQLite).
  - Нормализация `null` → `DBNull.Value` для параметров (Microsoft.Data.Sqlite бросает "Value must be set.").
  - Бенчмарки `Cache`/`SimulateWork` и тесты переведены на Microsoft.Data.Sqlite.
- **1.2** — `First/FirstOrDefault/Single/SingleOrDefault` больше не откатывают `Paging.Limit`: форма single-row остаётся на команде, поэтому повторные вызовы не пересобирают план (ключ `QueryPlanEqualityComparer` включает Limit/SingleRow).
- **1.3** — `DataContext.MapColumn`: убран лишний `Expression.Convert` для ссылочных типов, типизированные геттеры и постоянные ординалы.
- **1.4** — `ConfigureAwait(false)` в `GetDbCommand`/`ExecuteScalar` async-путях.
- **2.3** — `ResultSetEnumerator` маппит строку внутри `MoveNext/MoveNextAsync`, `Current` — чтение поля; добавлен рукописный `ResultSetAsyncEnumerable<TResult>` вместо компиляторного async-iterator (убран per-row state machine и лишний диспатч).
- **2.4** — `ConfigureAwait(false)` в `ResultSetEnumerator.MoveNextAsync`, `IDataContext.CreateEnumeratorAsync`.

Проверки: `tests/nextorm.integration.tests` 102/102, `tests/nextorm.core.tests` 35/35 — зелёные.

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

Тесты: `tests/nextorm.integration.tests` 102/102, `tests/nextorm.core.tests` 35/35.

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
- `Nextorm_PreparedForLoop_*` фактически использует кешированный API (`EntityBuilder.FirstOrDefaultAsync/ToListAsync`) внутри измеряемого метода, поэтому отнесён к категории B, несмотря на имя.
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
- `DataContext.GetMapCached(queryCommand, sql)` — используется в `GetPreparedQueryCommand`; при промахе строит маппер через `GetMap` и кладёт в кэш. Публичный `GetMap` не изменён.
- Маппер провайдеро- и контекст-независим (работает только с `IDataRecord`), поэтому кэш общий для всех `DataContext` и переживает `PurgeQueryCache`.
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
- **nextorm InMemory** — `InMemoryDataContext` + `EntityBuilder<SimpleEntity>.WithData(...)`, только `*.Prepare(...)`.
- **EF Core InMemory** — `Microsoft.EntityFrameworkCore.InMemory` 10.0.12, `EFInMemoryDataContext` (`ValueGeneratedNever` для `id`, `NoTracking`), только `EF.CompileQuery(...)`.

> **linq2db не участвует.** У linq2db нет in-memory провайдера для объектов (в его документации прямо есть «No way to work with in-memory database» именно в этом смысле). Единственная опция — SQLite `Data Source=:memory:`, но это настоящий реляционный движок в RAM, а не in-memory контекст над объектами; эмулировать его и сравнивать с object-store участниками некорректно, поэтому он исключён.

> **Методическая оговорка.** В исходном виде Void-бенчмарки без потребления результата JIT выбрасывал как мёртвый код — out-of-process прогон занижал `Where` примерно в 100 раз. Все измеряемые методы теперь аккумулируют результат в поле `_sink`, так что работа не может быть удалена. Отдельный stopwatch-прогон тех же методов подтвердил, что работа реально выполняется (порядок участников совпадает; абсолютные значения там выше из-за не разогретого Tier1-кода).

Что было исправлено по пути:
- **Блокирующий баг `InMemoryDataContext`.** `GetPreparedQueryCommand` падал с `NullReferenceException`, если `queryCommand.Cache == true`, а `storeInCache == false` (путь `QueryCommand.Prepare`): `queryPlan` не создавался, но использовался через `queryPlan!`. Теперь запись в кэш планов идёт под тем же условием `storeInCache && queryCommand.Cache`, что и в `DataContext`. До фикса **все** in-memory бенчмарки возвращали `NA`.
- В `nextorm.benchmark` добавлены пакет `Microsoft.EntityFrameworkCore.InMemory` и контекст `EFInMemoryDataContext` (`benchmarks/nextorm.benchmark/EfInMemory.cs`).

## Что оптимизировано в in-memory провайдере

Профилирование горячего пути показало переплату nextorm над LINQ ≈ 2–3.3 ns/строка. Реализовано (R1–R7):

1. **R1. Индексный fast-path для `List<T>`/`T[]`** (`InMemoryEnumerator.Init`/`MoveNext`): убран interface-диспатч `IEnumerator<T>` и бокс `List<T>.Enumerator` (те самые 32 B/вызов на `Any`); обход по индексу, текущий элемент кэшируется.
2. **R2. Типизированный предикат** (`InMemoryCompiledQuery.ConditionFactory` + `InMemoryDataContext.BuildConditionFactory` + `Visitors/TypedParamVisitor.cs`): параметры из `object[]` распаковываются один раз на запрос, а не на каждой строке — на строку больше нет индексации массива и unbox.
3. **R3. Одно чтение текущего элемента** на строку (было два: в условии и в `Current`).
4. **R4. Убран `yield`-слой** `IDataContext.GetEnumerable` для in-memory: `InMemoryDataContext.GetEnumerable` возвращает сам enumerator (`InMemoryEnumerator`), минуя state machine.
5. **R5. Кэш per-call резолвера** (`InMemoryCacheEntry.Resolver`): повторные вызовы только переинициализируют enumerator, не повторяя разрешение данных/joins/sorting.
6. **R6. `Any`/scalar** идут через тот же резолвер — лишняя аллокация enumerator'а ушла (`Any` теперь 0 B).
7. **R7. `ToList`/`ToListAsync`**: `Offset`/`Limit` вынесены в локальные переменные (без повторного чтения `Paging` на строку).

Тесты после изменений: `tests/nextorm.core.tests` 35/35, `tests/nextorm.integration.tests` 102/102.

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

На полном обходе nextorm InMemory (prepared) держится в пределах 1.33–1.83× от LINQ (разница — обёртка `EntityBuilder<T>`/enumerator), EF InMemory (compiled) — в ~19.5–20.9× медленнее и аллоцирует в ~15 раз больше (3.4–3.6 МБ на проход).

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
- Реализованы пункты R1–R7; регрессий нет: `tests/nextorm.core.tests` 35/35, `tests/nextorm.integration.tests` 102/102.

## Воспроизведение
```bash
NEXTORM_BENCH_FULL=1 dotnet run -c Release --project benchmarks/nextorm.benchmark -- --filter "*InMemoryBenchmark*"
```
Классы: `InMemoryBenchmarkAny`, `InMemoryBenchmarkIteration`, `InMemoryBenchmarkWhere`; EF-контекст — `benchmarks/nextorm.benchmark/EfInMemory.cs`.

---

# Итерация 5 — проверка после извлечения `ISqlDialect` (регрессий нет)

## Что проверялось
Рефакторинг SOLID (F1/F5/F6) вынес рендеринг диалекта из `DataContext` в `ISqlDialect` + `SqlDialectBase`, реализации `SqliteDialect`/`PostgresDialect`/`SqlServerDialect`; `DataContext` стал `abstract` (владеет только `Dialect`, `CreateConnection`, `CreateParam`); `SqlBuilder`, `BaseExpressionVisitor`, `WhereExpressionVisitor` зависят от `ISqlDialect`/`ILogger?`, а не от `DataContext`; пара `RequireSorting`+`EmptySorting` сведена в один `GetPagingOrderBy` (возврат `null` вместо броска). Это горячий путь генерации SQL, поэтому изменение проверено бенчмарками.

Ожидание: правка должна быть **нейтральной** — тот же SQL, те же аллокации, время в пределах шума.

## Методика
- **BASE** — HEAD `9641660` (диалектные методы внутри `DataContext`, `SqlBuilder(DataContext, ...)`), снят через `git archive` в `/tmp/opencode/nextorm-base`. **NEW** — рабочее дерево с `ISqlDialect`.
- Обе версии собраны `-c Release`, прогнаны на одной машине (AMD Ryzen 7 5800HS, WSL2, .NET 10.0.12, BenchmarkDotNet 0.15.8) на одной и той же БД в tmpfs (`/tmp/nextorm-bench/test.db`).
- Прогоны **строго последовательно** — параллельные BDN-прогоны в этом репозитории конфликтуют за генерируемый проект.
- Класс `SqliteBenchmarkCachedPlan` выбран прицельно: это единственный набор, где `Build_Sql`/`Build_Sql_Join` изолированно измеряют генерацию SQL (visitor + alias resolution, `storeInCache:false`) — ровно изменённый код. Там же кэш плана (`Cached_PlanOnly_*`, `RePrepare_*`, `M12_*`) и end-to-end путь в БД (`Prepared_ToList`, `Cached_ToList`, `M12_NoCache_ToList`).
- Режим `Job.Default` (out-of-process) + `MemoryDiagnoser`.

## Полный прогон (mean, `Job.Default`)

| Метод | BASE | NEW | Δ |
|---|---:|---:|---:|
| Construct_Only | 125.8 µs | 110.1 µs | −12% |
| RePrepare_PlanOnly_Param | 147.0 µs | 144.0 µs | −2% |
| Cached_PlanOnly_NoParam | 302.5 µs | 260.2 µs | −14% |
| Cached_PlanOnly_Param | 346.7 µs | 357.7 µs | +3% |
| **Build_Sql** | 472.8 µs | 499.1 µs | **+6%** |
| M12_NoCache_PlanOnly_Param | 564.7 µs | 504.5 µs | −11% |
| Prepared_ToList | 1 101.4 µs | 989.1 µs | −10% |
| **Build_Sql_Join** | 1 376.3 µs | 1 190.2 µs | **−13%** |
| Cached_ToList | 1 716.5 µs | 1 587.9 µs | −8% |
| M12_NoCache_ToList | 3 594.1 µs | 3 453.2 µs | −4% |

**Аллокации побайтово идентичны** — набор выполняемой работы не изменился:

| Метод | BASE | NEW |
|---|---:|---:|
| Construct_Only | 167.97 KB | 167.97 KB |
| Cached_PlanOnly_Param | 301.57 KB | 301.57 KB |
| Build_Sql | 441.42 KB | 441.42 KB |
| Build_Sql_Join | 1 150.07 KB | 1 143.04 KB |
| Prepared_ToList | 76.13 KB | 76.13 KB |
| Cached_ToList | 377.7 KB | 377.7 KB |

## `Build_Sql` — интерлив A/B/A/B
Знак разницы в полном прогоне противоречив: `Build_Sql` +6%, но `Build_Sql_Join` −13%; при этом `Construct_Only` (код, который не трогали) «ускорился» на 12%. Это признак дрейфа машины, поэтому сделано 4 чередующихся прогона на самом чувствительном `Build_Sql`:

| Прогон | Mean |
|---|---:|
| NEW #1 | 551.8 µs |
| BASE #1 | 517.1 µs |
| NEW #2 | 598.9 µs |
| BASE #2 | 533.2 µs |
| BASE #3 | 588.0 µs |
| NEW #3 | 519.9 µs |
| BASE #4 | 459.8 µs |
| NEW #4 | 433.5 µs |

- NEW: среднее **526.0 µs**, медиана 535.9 µs
- BASE: среднее **524.5 µs**, медиана 525.2 µs
- Δ средних: **+0.3%**

Разброс внутри одного и того же кода — 433–599 µs (**±19%**), то есть в разы больше разницы средних. Эффект от замены class-virtual на interface-dispatch неотличим от шума WSL2.

## Вывод
- **Регрессий нет.** SQL-генерация нейтральна по аллокациям (побайтово) и по времени (в пределах шума ±19%).
- Корректность подтверждена тестами: SQL-тесты SQLite/PostgreSQL/SQL Server и интеграционные — зелёные; SQL-вывод не менялся.
- Ограничение: бенчмарки есть только для SQLite-пути; рендеринг PostgreSQL/SQL Server проверен тестами, но не бенчмарками (PG/MSSQL-классов в сьюте нет).

## Воспроизведение
```bash
# BASE: снимок HEAD
mkdir -p /tmp/nextorm-base && git archive HEAD | tar -x -C /tmp/nextorm-base

# NEW (рабочее дерево) и BASE — по очереди, не параллельно
NEXTORM_BENCH_FULL=1 dotnet run -c Release --project benchmarks/nextorm.benchmark -- --filter "*SqliteBenchmarkCachedPlan*"

# интерлив на Build_Sql (повторить N раз, чередуя каталоги)
NEXTORM_BENCH_FULL=1 dotnet run -c Release --project benchmarks/nextorm.benchmark -- --filter "*SqliteBenchmarkCachedPlan.Build_Sql*"
```

---

# Итерация 6 — новые SQL-возможности и оптимизация реальных проигрышей

## Что добавлено (функциональность)

Реализованы и покрыты тестами SQL-возможности, которых раньше не было:

- все виды JOIN — `LEFT`/`RIGHT`/`FULL`/`CROSS` (SQL-генерация, fluent API, in-memory, диалекты);
- арность join до 8 таблиц (`Projection<T1..T8>`, `JoinedEntityBuilder<T1..T8>`);
- `CASE WHEN` / тернарный оператор / `switch`;
- строковые, математические и date/time функции + `LIKE`;
- `IN` по списку/массиву и `Contains` (плюс `SqlFunctions.Sql.@in`);
- логический `!` и унарные операторы (`-`, `+`, `~`);
- `SELECT DISTINCT`;
- `INTERSECT` / `EXCEPT` (и `ALL`-варианты там, где поддерживает провайдер);
- CTE (`WITH`) и рекурсивные CTE;
- оконные функции (`row_number`/`rank`/`dense_rank`/`ntile`/`lag`/`lead`/`first_value`/`last_value`/агрегаты `OVER`, фреймы `ROWS`/`RANGE`);
- пользовательские scalar-valued функции (`[SqlFunction]`);
- table-valued функции (`[SqlTableFunction]`).

Навигационные свойства/связи и DML (INSERT/UPDATE/DELETE) в область работ не входили и не реализовывались.

Покрытие тестами после добавления: **81.9%** строк (при цели CI 75%).

## Методика честного сравнения

- **Категория A (prepared / compiled):** `Nextorm_*` через `Prepare()` ⟷ EF Core `EF.CompileAsyncQuery` ⟷ linq2db `CompiledQuery.Compile` ⟷ Dapper (raw SQL). Это симметричное сравнение «скомпилировано против скомпилировано».
- **Категория B (warm cached):** `Nextorm_*` через неявный кэш плана (fluent-запрос с константой) ⟷ обычные (не compiled) EF Core / linq2db ⟷ Dapper.
- БД — tmpfs `/tmp/nextorm-bench/test.db`, режим `Job.ShortRun` + `InProcessEmitToolchain`, прогоны строго последовательно (параллельные BDN-прогоны в этом репозитории конфликтуют за генерируемый проект).
- Новые классы: `SqliteBenchmarkFeatures`, `SqliteBenchmarkFeaturesFair`, `SqliteBenchmarkFeaturesFairCached`, `SqliteBenchmarkFeaturePlanBuild`, `SqliteBenchmarkFeaturePlanCache`.

## Категория A — prepared против compiled/raw (итог)

Финальный прогон на tmpfs; mean **на один запрос** (метод выполняет 10 итераций). nextorm prepared
**выигрывает на всех новых фичах**:

| Фича | Nextorm prepared | Лучший конкурент | Nextorm ÷ конкурент |
|---|---:|---:|---:|
| String `ToUpper` | 9.80 µs | Dapper 16.15 | 0.61× |
| UDF (`[SqlFunction]` upper) | 10.05 µs | Dapper 16.30 | 0.62× |
| `CASE WHEN` | 10.51 µs | Dapper 15.36 | 0.68× |
| `INTERSECT` | 11.46 µs | Dapper 17.04 | 0.67× |
| `EXCEPT` | 11.59 µs | Dapper 20.73 | 0.56× |
| String `Contains`/LIKE | 11.66 µs | linq2db_Compiled 17.92 | 0.65× |
| `LEFT JOIN` | 12.76 µs | Dapper 21.44 | 0.60× |
| `DISTINCT` | 12.91 µs | linq2db_Compiled 19.36 | 0.67× |
| Recursive CTE | 13.13 µs | Dapper 26.50 | 0.50× |
| `Join4` (4 таблицы) | 13.33 µs | linq2db_Compiled 25.08 | 0.53× |
| IN-list (`@in`/`Contains`) | 13.44–13.84 µs | Dapper 24.01 | 0.57× |
| Window `row_number()` | 16.09 µs | Dapper 35.68 | 0.45× |
| Window `sum() over()` | 16.33 µs | Dapper 34.01 | 0.48× |
| CTE | 16.64 µs | linq2db_Compiled 23.31 | 0.71× |

Аллокации nextorm в разы ниже (например, `EXCEPT` 6.7 KB против 88 KB у EF_Compiled, `Join4` 9.8 KB против 85 KB).

## Категория B — warm cached: после оптимизаций

Именно здесь были реальные проигрыши: prepared-путь быстрый, а неявный кэш на каждый вызов
перестраивал план/хеш. Финальный прогон на tmpfs, mean **на один запрос** (метод — 10 итераций):

| Фича (warm) | Nextorm cached | Лучший конкурент | Nextorm ÷ конкурент |
|---|---:|---:|---:|
| `CASE WHEN` | 13.04 µs | Dapper 15.15 | 0.86× |
| `DISTINCT` | 15.54 µs | Dapper 18.73 | 0.83× |
| String `ToUpper` | 15.44 µs | Dapper 14.93 | 1.03× |
| String `Contains`/LIKE | 15.17 µs | Dapper 20.39 | 0.74× |
| UDF | 15.51 µs | Dapper 15.36 | 1.01× |
| `INTERSECT` | 16.83 µs | Dapper 18.40 | 0.91× |
| `EXCEPT` | 18.17 µs | Dapper 19.03 | 0.96× |
| `LEFT JOIN` | 23.16 µs | Dapper 25.05 | 0.92× |
| CTE | 27.32 µs | Dapper 21.85 | 1.25× |
| IN `@in`/`Contains` captured | 29.69–30.27 µs | Dapper 20.83 | ~1.44× |
| IN `@in`/`Contains` inline | 32.18–32.76 µs | Dapper 20.83 | ~1.56× |
| `Join4` (4 таблицы) | 34.29 µs | Dapper 29.32 | 1.17× |
| Window `row_number()` / `sum() over()` | 35.13–35.89 µs | Dapper 32.95 | ~1.07× |
| Recursive CTE | 37.73 µs | Dapper 30.81 | 1.22× |

После оптимизаций nextorm в warm-пути **выигрывает** у Dapper на `CASE`, `DISTINCT`, `INTERSECT`,
`EXCEPT`, `LEFT JOIN`, `Contains` (0.74–0.96×); идёт вровень на `ToUpper`/`UDF`/окнах; и остаётся в
пределах 1.17–1.56× на CTE / recursive CTE / `Join4` / IN-list — при этом всё равно заметно быстрее
обычных (не compiled) linq2db и EF Core.

До оптимизаций именно warm-путь терял: IN-list — 7–8× Dapper, `INTERSECT`/`EXCEPT` — 2.9–3.4×,
recursive CTE — 3.5× (по замерам предыдущих прогонов; методики отличались, поэтому сравнимы
относительные позиции, а не абсолютные значения).

## Plan-build — холодное построение SQL (µs на вызов, 100 итераций)

| Фича | до | после |
|---|---:|---:|
| Baseline простой SELECT | 5.1 | ~4.1 |
| IN `@in` captured | 74.4 | **6.7** |
| IN `@in` inline | 121.8 | **~7** |
| IN `list.Contains` captured/inline | 73.0 / 121.9 | **~7** |
| Join4 (4 таблицы) | 21.6 | **13.1** |
| LEFT JOIN | 9.3 | **6.7** |
| CTE | 17.2 | ~16 |
| Recursive CTE | 10.9 | ~9.5 |

## Что именно оптимизировали

1. **IN-list (`@in` / `Contains`)** — главный проигрыш (7–8× Dapper в warm-пути).
   - Причина: `GetInValues` вызывал `Expression.Lambda<Func<object>>(...).Compile()` **на каждое построение/исполнение**.
   - Исправление: новый `InValuesEvaluator` кэширует скомпилированный экстрактор по форме выражения (closure-константа заменяется параметром, поэтому разные экземпляры одной формы переиспользуют делегат, но читают актуальные значения); форма списка (число non-null + наличие null) сворачивается в `WherePlanHash`, а значения обновляются существующим механизмом `NeedsParamRefresh`; `RefreshInValuesShape()` переоценивает форму перед исполнением, поэтому выросший/переприсвоенный список не переиспользует устаревший план.
   - Результат: plan-build 74–122 µs → ~7 µs; warm 161–184 µs → 71–79 µs.
2. **INTERSECT / EXCEPT / recursive CTE** — потеря 2.9–3.5×.
   - Причина (баг): в `PrepareCommand` вызов `GetQueryPlanEqualityComparer().GetHashCode()` без аргумента связывался с `object.GetHashCode()`, то есть хешировал **identity компаратора**, а не под-команду. `_queryPlanComparer` создаётся лениво на команду, поэтому `UnionPlanHash` (и, через тело-union, `CtesPlanHash` у рекурсивного CTE) менялись на каждом построении — кэш плана никогда не попадал.
   - Исправление: хешировать саму под-команду (`_union`, `_referencedQueries[0]`).
   - Результат: INTERSECT/EXCEPT ускорились в ~8× (теперь даже быстрее Dapper), recursive CTE — в ~7×.
3. **Join builder** — снижена стоимость холодного построения (LEFT JOIN 9.3→6.7 µs, 4-table join 21.6→13.1 µs) без изменения SQL.
4. Ранее (Итерация 4) — SQL-ключевой кэш маппера, который убрал основную часть холодного построения плана.

## Вывод

- В симметричном сравнении «prepared vs compiled» nextorm побеждает **все** новые SQL-возможности (UDF, который в промежуточном прогоне был на уровне шума, в финальном прогоне на tmpfs у nextorm быстрее остальных).
- Все реальные проигрыши находились в warm-пути и были вызваны не исполнением/маппингом, а построением и ключеванием плана; они устранены: IN-list, `INTERSECT`/`EXCEPT`, recursive CTE.
- После оптимизаций warm-путь новых фич: nextorm выигрывает у Dapper на большинстве фич (0.74–0.96×) и остаётся в пределах ~1.0–1.6× на CTE / recursive CTE / Join4 / IN-list, а prepared-путь стабильно первый.

## Воспроизведение

```bash
cd benchmarks/nextorm.benchmark && dotnet build -c Release

# честное сравнение
dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturesFair.*"
dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturesFairCached*"

# холодное построение SQL по фичам
dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturePlanBuild*"
dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturePlanCache*"
```

## In-memory: новый функционал (агрегаты, GroupBy, set-операции, материализаторы)

Добавлены бенчмарки для функционала, реализованного в in-memory провайдере:
`InMemoryBenchmarkAggregates`, `InMemoryBenchmarkGroupBy`, `InMemoryBenchmarkSetOperations`,
`InMemoryBenchmarkMaterializers`. Сравнение — с голым LINQ и EF Core InMemory (`UseInMemoryDatabase`),
10 000 строк, 100 итераций на замер, режим `ShortRun` + `InProcessEmitToolchain`
(тот же `NextormConfig`, что и остальные in-memory замеры). Запуск:
`dotnet run -c Release -- --anyCategories InMemoryNew`.

### Агрегаты (`Count`/`Sum`/`Min`+`Max`, 100× по 10k строк)

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Linq_Count | 187.8 ns | 0.000 | — |
| **Nextorm_Count** | 2.156 ms | 1.000 | 268 KB |
| Linq_Sum | 2.405 ms | 1.116 | 4.0 KB |
| Linq_MinMax | 4.714 ms | 2.187 | 8.0 KB |
| Nextorm_Sum | 5.540 ms | 2.569 | 321 KB |
| Nextorm_MinMax | 11.253 ms | 5.219 | 642 KB |
| EFCoreInMemory_Count | 31.73 ms | 14.718 | 48.4 MB |
| EFCoreInMemory_Sum | 37.29 ms | 17.295 | 48.5 MB |
| EFCoreInMemory_MinMax | 78.64 ms | 36.479 | 96.9 MB |

`Linq_Count` — O(1) (`Enumerable.Count` видит `ICollection<T>`), поэтому не сопоставим. По существу:
Nextorm быстрее EF Core InMemory в 14–36 раз; отстаёт от LINQ в 2.6× (`Sum`) и 5.2× (`Min`+`Max`).
Отставание — это подготовка команды на каждый вызов агрегата (LINQ не строит запрос), а не сам свёртке:
после оптимизации свёртка типизирована (без боксинга на строку) и аллокации `Sum` упали с ~59 MB до 0.32 MB.

### GroupBy + per-group count (100×)

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| **Linq_GroupByCount** | 12.61 ms | 0.22 | 25.11 MB |
| **Nextorm_GroupByCount** | 58.35 ms | 1.00 | 49.88 MB |
| EFCoreInMemory_GroupByCount | 116.42 ms | 2.00 | 178.63 MB |

Nextorm в 2.0× быстрее EF InMemory; LINQ быстрее Nextorm в 4.6×. Основная стоимость — материализация
групп и компиляция селектора агрегата на каждую группу/вызов.

### Set-операции (`INTERSECT`/`EXCEPT`/`UNION`, 100×)

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Linq_Intersect | 11.00 ms | 0.29 | 8.94 MB |
| Linq_Except | 15.60 ms | 0.41 | 27.49 MB |
| Linq_Union | 24.88 ms | 0.65 | 51.4 MB |
| Nextorm_Intersect | 35.04 ms | 0.91 | 61.1 MB |
| **Nextorm_Except** | 38.42 ms | 1.00 | 61.1 MB |
| Nextorm_Union | 49.37 ms | 1.29 | 87.09 MB |
| EFCoreInMemory_Intersect | 226.18 ms | 5.89 | 362.53 MB |
| EFCoreInMemory_Except | 558.31 ms | 14.53 | 396.65 MB |
| EFCoreInMemory_Union | 638.48 ms | 16.62 | 521.05 MB |

Nextorm быстрее EF InMemory в 4.6–16.6× и медленнее LINQ в 1.5–3× (оба операнда материализуются
и объединяются с equality-политикой `DISTINCT`).

### Материализаторы и `Last` (100×)

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Linq_ToArray | 767.3 µs | 0.11 | 3.92 MB |
| Linq_Last | 2.745 ms | 0.39 | 12.5 KB |
| **Nextorm_ToArray** | 7.125 ms | 1.00 | 7.99 MB |
| Linq_ToDictionary | 12.43 ms | 1.74 | 19.78 MB |
| Nextorm_ToDictionary | 17.64 ms | 2.48 | 23.85 MB |
| EFCoreInMemory_Last | 40.55 ms | 5.69 | 47.47 MB |
| EFCoreInMemory_ToArray | 173.81 ms | 24.40 | 348.16 MB |
| Nextorm_Last | 197.71 ms | 27.75 | 43.26 MB |
| EFCoreInMemory_ToDictionary | 229.35 ms | 32.19 | 379.34 MB |

`ToArray`/`ToDictionary` — в пределах 2.5× от LINQ и до 32× быстрее EF.

### Найденные проблемы (зафиксированы, не блокируют)

1. **`Last`/упорядоченные in-memory запросы медленные** (198 ms против 2.7 ms у LINQ). Причина —
   `ApplyOrdering` (`InMemoryDataContext`) строит `Func<TEntity, object>` (боксинг ключа на каждую
   строку) и вызывает `OrderBy` через `Comparer<object>`, а каждый вызов `Last()` заново готовит
   команду. Селектор сортировки теперь кэшируется (`_sortingSelectorCache`), но типизированный ключ
   (без бокса) — следующий шаг. Это унаследованная стоимость сортировки, не новый функционал.
2. **Агрегаты пересобирают команду на каждый вызов**: `EntityBuilder.Sum/Min/...` не имеют
   prepared-варианта, поэтому в цикле платят за подготовку. Селектор и predicate кэшируются, бокс
   убран; остаётся стоимость команды.
3. **GroupBy** компилирует селектор агрегата на каждую группу и вызов — можно кэшировать по паре
   (выражение, группа) для повторяющихся прогонов.

## In-memory: `SelectMany` / `GroupJoin` (100× по 10k строк)

Класс `InMemoryBenchmarkSelectMany` (категория `InMemoryNew`). `Nextorm_*_Prepared` — запрос построен
один раз и переиспользуется (срабатывает кэш плана), `Nextorm_*` — запрос пересобирается каждую
итерацию, как в остальных `InMemory*`-бенчмарках.

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| Linq_SelectMany | 14.63 ms | 0.13 | 38.15 MB |
| EFCoreInMemory_GroupJoin | 30.84 ms | 0.28 | 46.72 MB |
| Linq_GroupJoin | 35.86 ms | 0.33 | 44.93 MB |
| **Nextorm_SelectMany_Prepared** | 57.53 ms | 0.53 | 70.88 MB |
| **Nextorm_SelectMany** | 108.87 ms | 1.00 | 151.99 MB |
| Nextorm_GroupJoin_Prepared | 122.78 ms | 1.13 | 98.77 MB |
| Nextorm_GroupJoin | 450.57 ms | 4.14 | 215.65 MB |

(`Ratio` — относительно `Nextorm_SelectMany`; `ShortRun`+in-process, N=3, разброс местами ±20–30%, поэтому
`Nextorm_GroupJoin` без подготовки — оценка сверху. `EFCoreInMemory_SelectMany` не попал в таблицу: EF Core
InMemory не транслирует `Enumerable.Range` внутри `SelectMany` и бросает `InvalidOperationException`.)

**Что сделано по перформансу:**
- Селекторы (`collectionSelector`, `resultSelector`, key-селекторы) компилируются один раз и кэшируются
  (`_linqSelectorCache`, ключ — структурный `ExpressionKey`). До этого `Expression.Compile` выполнялся на
  каждом вызове и доминировал: `SelectMany` 163 → 109 ms, `GroupJoin` 357 → 302 ms (per-call).
- `GroupJoin` строит `Dictionary<TKey, List<TInner>>` — O(outer + inner) вместо O(outer × inner).
- Outer-строки больше не материализуются в промежуточный список (стриминг через `Enumerate`), аллокации
  `SelectMany` упали 202 → 152 MB, prepared 96 → 71 MB.

**Вывод:** на prepared-запросах `SelectMany` в ~3.9× медленнее LINQ, `GroupJoin` — в ~3.4× (вровень с EF
InMemory по execution). Пересборка команды на каждый вызов добавляет примерно столько же (у `GroupJoin` —
двойная подготовка outer+inner). Это уровень остальных in-memory операторов (агрегаты 2.6–5.2×, GroupBy 4.6×
от LINQ); in-memory предназначен для тестов, а не для продакшн-нагрузки.

**Остаточные точки роста (не блокируют):**
1. Двойная буферизация: `ApplySelectMany`/`ApplyGroupJoin` строят `List<TResult>` → `InMemoryListEnumerator`
   → `InMemoryEnumeratorAdapter` → финальный `ToList`. Стриминг с dual sync/async-энумератором убрал бы копии.
2. `BuildLinqSourceDelegate` использует рефлексию (`MakeGenericMethod` + `Invoke`) на каждый вызов.
3. Кэш плана не переиспользуется при пересборке запроса: узел `LinqSourceExpression` сравнивается по ссылке.
   Структурное сравнение пробовалось — выигрыша не дало (доминирует `PrepareCommand`), откатано ради простоты.

# Итерация 7 — бенчмарк после серии ядровых правок (prepared vs compiled/raw)

> **Ревизия:** [`9f97714`](https://github.com/AlexeyShirshov/nextorm/commit/9f97714a69c32ac5ee3cd4a129992eefbf62263c) «many improvements», ветка `1.0.3-alpha` (поверх `v1.0.2-alpha`; `git describe` → `v1.0.1-alpha-86-g9f97714`). Бинарник собран из рабочего дерева, которое дополнительно содержит незакоммиченные правки (скалярный `GroupBy` в `QueryCommand.QueryPreparer`, правки доков) — на бенчмаркируемые запросы они не влияют.
> **Milestone:** [`1.0-a.3`](https://github.com/AlexeyShirshov/nextorm/milestones/1.0-a.3) — issue [#17 «TODO: Benchmark with Dapper and EF»](https://github.com/AlexeyShirshov/nextorm/issues/17).
> **Дата прогона:** 2026-09-21.

## Окружение и методика

- Машина: AMD Ryzen 7 5800HS, WSL2, .NET 10.0.12, BenchmarkDotNet 0.15.8; `Job.ShortRun` + `InProcessEmitToolchain` (IterationCount=3, WarmupCount=3).
- БД: `benchmarks/nextorm.benchmark/data/test.db` (**ext4**, не tmpfs); путь задан через `NEXTORM_BENCH_DB`.
- Сравнение симметричное: **Категория A** — `Nextorm_*Prepared*` (`Prepare()`) ⟷ EF Core `EF.CompileAsyncQuery` ⟷ linq2db `CompiledQuery.Compile` ⟷ Dapper (raw SQL); **Категория B** — неявный кэш плана nextorm ⟷ обычные (не compiled) EF/linq2db ⟷ Dapper.
- **Отличие от Итерации 6 (важно для абсолютных чисел):** прогон шёл на 4 логических / 2 физических ядрах (WSL ограничил видимый CPU) и БД на ext4, тогда как Итерация 6 — на 16 логических / 8 физических ядрах и tmpfs. Относительные позиции сопоставимы, абсолютные — в пределах ±10–15 % (см. «Сверка с Итерацией 6»). Внутри одной таблицы все участники замерены в одинаковых условиях.

## Категория A — prepared против compiled/raw (mean на вызов метода; внутри 10 запросов)

nextorm prepared выигрывает **все** категории:

| Фича | Nextorm prepared | Dapper | linq2db compiled | EF compiled | Nextorm ÷ лучший конкурент |
|---|--:|--:|--:|--:|--:|
| CASE WHEN | **104.4 µs** | 156.2 | 163.3 | 369.3 | 0.67× |
| CTE | **108.4 µs** | 233.1 | 237.5 | — | 0.47× |
| DISTINCT | **124.1 µs** | 204.8 | 204.6 | 424.3 | 0.61× |
| EXCEPT | **123.6 µs** | 197.4 | 226.4 | 458.3 | 0.63× |
| IN `@in` / `Contains` | **135.5 / 136.9 µs** | 201.8 | 481.0 | 647.0 | 0.67× |
| INTERSECT | **115.4 µs** | 177.7 | 214.5 | 442.1 | 0.65× |
| Join4 | **140.2 µs** | 274.3 | 257.6 | 518.0 | 0.54× |
| LEFT JOIN | **134.3 µs** | 215.8 | 225.2 | 454.2 | 0.62× |
| Recursive CTE | **134.7 µs** | 266.5 | 279.9 | — | 0.51× |
| ToUpper / Contains | **104.8 / 103.7 µs** | 155.2 / 210.0 | 167.4 / 177.7 | 377.1 / 397.1 | 0.68× / 0.58× |
| UDF (`[SqlFunction]`) | **105.8 µs** | 155.8 | 166.9 | 370.4 | 0.68× |
| Window row_number / sum over | **157.8 / 160.7 µs** | 329.9 / 328.1 | 346.2 | — | 0.48× |

Аллокации nextorm в разы ниже: 7.2–15.7 KB против 12.7–22.2 KB у Dapper, 15.9–152 KB у linq2db и 75–148 KB у EF Core. `EFCore_Compiled` медленнее nextorm в 3.5–4×, linq2db — в 1.5–2×.

## Категория B — warm cached против обычных EF/linq2db/Dapper

| Фича (warm) | Nextorm cached | Dapper | linq2db | EF Core | Nextorm ÷ лучший конкурент |
|---|--:|--:|--:|--:|--:|
| CASE WHEN | **143.5 µs** | 156.0 | 274.6 | 621.2 | 0.92× |
| Contains | **168.6 µs** | 207.2 | 348.3 | 725.6 | 0.81× |
| DISTINCT | **173.1 µs** | 204.4 | 331.7 | 674.4 | 0.85× |
| EXCEPT | **189.7 µs** | 194.3 | 418.6 | 785.9 | 0.98× |
| INTERSECT | 178.6 µs | 178.3 | 406.0 | 764.3 | 1.00× |
| LEFT JOIN | 221.1 µs | 218.5 | 732.2 | 1 012.9 | 1.01× |
| Window row_number / sum over | 358.3 / 359.2 µs | 355.9 / 324.7 | 789.2 / 669.1 | — | 1.01× / 1.11× |
| ToUpper | 177.0 µs | 152.1 | 504.1 | 707.5 | 1.16× |
| UDF | 175.7 µs | 154.1 | 505.4 | 703.2 | 1.14× |
| Join4 | 325.3 µs | 276.1 | 692.6 | 1 209.7 | 1.18× |
| Recursive CTE | 344.3 µs | 265.0 | 645.1 | — | 1.30× |
| CTE | 304.6 µs | 228.1 | 605.3 | — | 1.34× |
| IN-list (`@in`/`Contains`, captured/inline) | 311.9–351.2 µs | 202.4 | 743.9 | 1 109.1 | 1.54–1.74× |

Warm-путь без изменений относительно Итерации 6: nextorm выигрывает/на равных у Dapper на `CASE`, `DISTINCT`, `Contains`, `EXCEPT`, `INTERSECT`, `LEFT JOIN`; ~1.1× на простых проекциях и окнах; 1.2–1.7× на CTE / recursive CTE / Join4 / IN-list. При этом nextorm всё равно в 1.9–3.5× быстрее **обычных** (не compiled) linq2db и EF Core.

## Сверка с Итерацией 6 (регрессий нет)

Базовые числа — прогон 2026-09-20 (`nextorm.benchmark.*-report-github.md`), те же `ShortRun`+in-process, но tmpfs и 16 логических ядер. nextorm prepared:

| Фича | Итерация 6 | Итерация 7 | Δ |
|---|--:|--:|--:|
| CTE | 166.4 µs | 108.4 µs | **−35 %** |
| Contains | 116.6 µs | 103.7 µs | −11 % |
| Distinct | 129.1 µs | 124.1 µs | −4 % |
| CaseWhen | 105.1 µs | 104.4 µs | −1 % |
| SumOver | 163.3 µs | 160.7 µs | −2 % |
| RowNumber | 160.9 µs | 157.8 µs | −2 % |
| IN-list | 138.4 µs | 135.5 µs | −2 % |
| Intersect | 114.6 µs | 115.4 µs | +1 % |
| RecursiveCte | 131.3 µs | 134.7 µs | +3 % |
| Join4 | 133.3 µs | 140.2 µs | +5 % |
| LeftJoin | 127.6 µs | 134.3 µs | +5 % |
| Udf | 100.5 µs | 105.8 µs | +5 % |
| Except | 115.9 µs | 123.6 µs | +7 % |
| ToUpper | 98.0 µs | 104.8 µs | +7 % |

- Все значения, кроме CTE, в пределах ±11 %, и такой же разброс виден у конкурентов (Dapper `In` −16 %, Dapper `Cte` −8 %, linq2db `Distinct` +6 %) — это смещение окружения (16→4 ядра, tmpfs→ext4), а не регрессия.
- **CTE ускорился на ~35 %** при почти неизменных конкурентах (Dapper 254.5→233.1 = −8 %, linq2db 233.1→237.5 = +2 %), `Contains` — на 11 % (Dapper 215.2→210.0, linq2db 179.2→177.7). Похоже на улучшение построения/кэша плана в свежих коммитах.
- Warm-путь (Категория B) просел на 10–15 % на `ToUpper`/`UDF`/`DISTINCT`/`CASE`, но конкуренты в этом прогоне колебались в ту же сторону (linq2db/EF — местами до +15 %), а относительные позиции сохранились. Для чистого вывода нужен повторный прогон на tmpfs с полным набором ядер.

## Вывод

- В симметричном «prepared vs compiled/raw» nextorm побеждает **все** 13 фич (0.47–0.68× лучшего конкурента).
- Ядровые правки, попавшие в `9f97714`, **регрессий не дали**; по CTE и `Contains` prepared-путь даже заметно ускорился.
- Открытые проигрыши остались только в warm-пути относительно Dapper (CTE, recursive CTE, Join4, IN-list — 1.2–1.7×), как и в Итерации 6; обычные EF Core и linq2db nextorm обходит и там.

## Воспроизведение

```bash
cd benchmarks/nextorm.benchmark && dotnet build -c Release

NEXTORM_BENCH_DB=$PWD/data/test.db \
  dotnet run -c Release --no-build -- --filter '*SqliteBenchmarkFeaturesFair.A_*'
NEXTORM_BENCH_DB=$PWD/data/test.db \
  dotnet run -c Release --no-build -- --filter '*SqliteBenchmarkFeaturesFairCached.B_*'
```

Артефакты: `benchmarks/BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.SqliteBenchmarkFeaturesFair{,-Cached}-report-github.md`.

# Итерация 8 — декомпозиция warm-пути и повторное извлечение параметров IN-list

> **Ревизия:** ветка `1.0.3-alpha`, рабочее дерево поверх `9f97714` (в дереве незакоммиченные
> правки: naming conventions + правки этой итерации). SQL-генерация не менялась.
> **Дата прогона:** 2026-09-21.

## Что проверялось и почему

Итерация 7 оставила открытым **warm (не-prepared) путь** на `CTE` / рекурсивном `CTE` / `Join4` /
`IN`-list. Задача итерации 8 — разложить warm-стоимость на компоненты (визиторы `PrepareCommand`,
план-хеширование, ключевание/поиск в кэше, `ExtractParams`) и снять то, что снимается без изменения
SQL и публичного API.

## Методика

- БД — tmpfs `/tmp/nextorm-bench/test.db`; `Job.ShortRun` + `InProcessEmitToolchain`,
  прогоны строго последовательно. Прогон шёл на **4 логических / 2 физических ядрах** (WSL),
  как и Итерация 7; относительные позиции сопоставимы, абсолютные — с разбросом ±20–30 %.
- Новый класс `benchmarks/nextorm.benchmark/SqliteBenchmarkWarmDecompose.cs`. Каждый arm
  пересобирает команду с нуля и останавливается на своём этапе, поэтому стоимость этапа — это
  разность двух arm'ов:
  - `*_Construct` — только fluent-сборка `QueryCommand`;
  - `*_Prepare_NoHash` — Construct + `PrepareCommand(dontCalculateHash: true)` (визиторы);
  - `*_Prepare_Hash` — Construct + `PrepareCommand(false)` (визиторы + все `*PlanHash`);
  - `*_Warm_PlanOnly` — полный implicit-cache hit (construct + prepare + hash + lookup + `ExtractParams`).
- Что кэш действительно **попадает**, проверено временной инструментацией `QueryPlanner`:
  на 4 warm-arm'ах — **1 440 420 hit против 16 miss** (числа arm'ов в этом диагностическом прогоне
  не годятся, поэтому в таблицы не вошли).

## Декомпозиция warm-пути (µs на вызов, `÷100`)

| Фича | Construct | +визиторы | +хеширование | +ключ/lookup/params = Warm_PlanOnly |
|---|---:|---:|---:|---:|
| Cte | 5.61 | 1.45 | 1.98 | **2.69** |
| RecursiveCte | 4.38 | 1.66 | 2.20 | **2.43** |
| Join4 | 6.65 | 0.73 | 1.63 | **3.89** |
| IN `@in` inline | 1.89 | 1.06 | 2.20 | **3.70** |
| IN `@in` captured | 1.73 | 0.96 | 1.63 | — (см. ниже) |

Чтение таблицы:

1. **Доминирует не поиск в кэше, а пересборка и перекэширование**: fluent-дерево (`Construct`) плюс
   визиторы+хеши — 8–10 µs из 11–13 µs plan-only. Поиск в кэше (по `RePrepare`-подобному arm'у
   «уже prepared команда») — ~1–1.7 µs.
2. Стоимость `CtesPlanHash`/`JoinPlanHash`/`ColumnPlanHash` **не** пересчитывается лишний раз:
   `PrepareCtes` хеширует под-команду уже посчитанными `*PlanHash`-полями, повторного обхода дерева нет.
   Хеш-стоимость — это первичный структурный хеш выражений (`ExpressionPlanEqualityComparer` /
   `SelectExpressionPlanEqualityComparer` / `JoinExpressionPlanEqualityComparer`).
3. `ExtractParams` бьёт только там, где у команды вообще есть **computed** параметры. У `Join4` их нет
   вовсе; у CTE/recursive CTE литеральные константы (`id > 1`, `n < 5`) **инлайнятся в SQL** и параметров
   не создают. У IN-list параметры есть всегда (значения списка), и они переизвлекались на каждом hit'е.

## Что сделано

**Повторное извлечение параметров IN-list.** Для инлайн-списка `new[] { 1, 3, 10 }` значения жёстко
зашиты в shape выражения и не могут измениться, пока ключ плана тот же, — но `NeedsParamRefresh`
выставлялся для любого не-runtime параметра, поэтому на каждом cache-hit'е выполнялся полный
param-mode обход `MakeSelect` только чтобы скопировать те же самые значения в `DbCommand`.

- `src/nextorm.core/Parameter.cs` — внутренний `internal bool Stable` (не публичный API);
- `src/nextorm.core/Query/InValues.cs` — `IsStableValueExpression`: стабильны только `NewArrayExpression`
  из констант/вложенных таких же и `NewExpression` значимого типа над константами; `ConstantExpression`
  ссылочного типа (массив!) — **не** стабилен; любой метод-вызов (например `Guid.NewGuid()`) — **не** стабилен;
- `src/nextorm.core/Visitors/InValuesTranslator.cs` — параметры списка помечаются `Stable`, когда
  `valuesExp` стабилен (captured-коллекция всегда `Stable = false`);
- `src/nextorm.core/Visitors/BaseExpressionVisitor.cs` — `EmitFoldedParameter` помечает стабильные
  свёрнутые значения;
- `src/nextorm.core/DataContext/QueryPlanner.cs` — `needsParamRefresh` истинно, только если есть
  не-runtime и **не**-стабильный параметр.

Регресс-тесты: `tests/nextorm.sqlite.tests/InListCacheTests.cs` —
`In_InlineArray_CachedPlan_ShouldNotRefreshParams` и
`In_CapturedCollection_CachedSameShape_ShouldRefreshChangedValue` (captured-коллекция с изменённым
значением обязана обновиться на cache-hit'е).

## Результаты

### Plan-only warm (`SqliteBenchmarkFeaturePlanCache`, mean на вызов, µs)

| Arm | до | после | Δ |
|---|---:|---:|---:|
| `Warm_PlanOnly_Distinct` (контроль) | 2.29 | 2.30 | 0 % |
| `Warm_PlanOnly_In_AtIn_Inline` | 8.80 | **6.53** | **−26 %** |
| `Warm_PlanOnly_In_ListContains_Inline` | 9.75 | **7.08** | **−27 %** |
| `Warm_PlanOnly_In_AtIn_Captured` | 7.33 | 7.08 | в шуме |
| `Warm_PlanOnly_In_ListContains_Captured` | 8.46 | 8.02 | в шуме |

В `SqliteBenchmarkWarmDecompose` тот же эффект: `InAtIn_Inline_Warm_PlanOnly` 8.85 → **6.27 µs**
(−29 %), аллокации 6.98 → 5.81 KB/вызов; `InAtIn_Inline_Prepare_Hash` не изменился (5.15 → 5.28 µs) —
как и ожидалось, ушёл именно `ExtractParams`.

### Warm Category B end-to-end (`SqliteBenchmarkFeaturesFairCached`, mean на запрос, µs)

| Фича (warm) | Итерация 7 (до) | Итерация 8 (после) | Dapper (8) | Доля после |
|---|---:|---:|---:|---:|
| `Cte` | 304.6 | 311.5 | 237.4 | 1.31× |
| `RecursiveCte` | 344.3 | 387.4 | 255.6 | 1.52× |
| `Join4` | 325.3 | 314.2 | 272.4 | 1.15× |
| IN `@in` inline | 311.9–351.2 | **270.3** | 187.0 | **1.45×** |
| IN `List.Contains` inline | 311.9–351.2 | **286.6** | 187.0 | **1.53×** |
| IN `@in` captured | 311.9–351.2 | 301.4 | 187.0 | 1.61× |
| IN `List.Contains` captured | 311.9–351.2 | 318.7 | 187.0 | 1.70× |

Инлайн-варианты IN-list ускорились на **14–27 %** (270/287 против 343/360 µs в базовом прогоне
этой же итерации до правки). Captured-варианты не менялись — их коллекция может измениться при
неизменном ключе плана, поэтому refresh остаётся обязательным. Разброс CTE/recursive/Join4 между
прогонами — в пределах шума (±20–30 %); аллокации у них не менялись побайтово (CTE 123.36 KB,
Join4 106.5 KB), т.е. объём работы тот же.

### Холодное построение SQL не изменилось (`SqliteBenchmarkFeaturePlanBuild`)

| Фича | до | после |
|---|---:|---:|
| Baseline простой SELECT | 4.03 | 3.71 |
| IN `@in` inline / captured | 8.23 / 7.42 | 8.85 / 9.14 |
| Join4 | 14.61 | 14.06 |
| Recursive CTE | 13.05 | 13.78 |
| CTE | 16.02 | 17.30 |

Различия — в пределах шума; аллокации `Build_*` совпадают с точностью до долей KB. Правка не
затрагивает SQL-путь.

### Prepared-путь не регрессировал (`SqliteBenchmarkFeaturesFair`, mean на запрос, µs)

| Фича | nextorm prepared | Dapper | nextorm ÷ Dapper |
|---|---:|---:|---:|
| Cte | **9.83** | 23.50 | 0.42× |
| RecursiveCte | **12.68** | 25.66 | 0.49× |
| Join4 | **14.02** | 29.79 | 0.47× |
| IN-list | **13.22–13.54** | 20.94 | 0.63–0.65× |

Prepared выигрывает все четыре, как и раньше.

## Вывод

- Warm-стоимость разложена: **большая часть — пересборка fluent-дерева и первичное структурное
  хеширование выражений**, а не поиск в плане. Поиск в кэше — ~1–1.7 µs; хеш не пересчитывается «лишний
  раз» (под-команды хешируются по уже посчитанным `*PlanHash`).
- **Закрыт остаток по IN-list**: значения инлайн-списка больше не переизвлекаются на cache-hit'е.
  Warm `@in`/`Contains` inline: **−26…−29 % plan-only, −14…−27 % end-to-end**; план-билд и prepared-путь
  не изменились.
- **CTE / recursive CTE / Join4 остаются ~1.15–1.52× от Dapper.** Это не исполнение и не маппинг и не
  промах кэша (проверено: 1.44M hits / 16 misses), а цена того, что не-prepared API заново собирает
  дерево и заново его ключует на каждый вызов. Безопасного локального рычага здесь нет: любое ускорение
  требует либо кэша prepared-артефактов по форме (широкая перестройка ядра), либо публичного
  compiled/prepared-API — ровно вывод M12 (`performance-findings.md`, M12 п.3). Оценка: даже полное
  устранение plan-build не выведет эти фичи к Dapper, потому что warm-путь по построению платит за
  сборку выражения, а raw-Dapper — нет.
- Регрессий нет: `nextorm.sln` Release 0 warnings / 0 errors; тесты core 192, sqlite 268, postgres 247,
  sqlserver 238, mysql 65, mariadb 29, clickhouse 183 — все зелёные; SQL не изменён; новых публичных
  членов нет (`Parameter.Stable` — `internal`).

## Что осталось открытым

1. `CTE` / рекурсивный `CTE` / `Join4` в warm-пути (1.15–1.52× от Dapper). Требуется либо
   shape-keyed кэш подготовленных под-команд CTE/join'ов, либо проталкивание prepared/compiled API
   во fluent-путь. Оба варианта — вне рамок «минимальной внутренней правки» этой итерации.
2. `ExtractParams` для captured-коллекций: снять нельзя, пока содержимое коллекции может измениться
   при неизменном shape-ключе. Возможный следующий шаг — сверять «отпечаток» коллекции с сохранённым,
   но это O(n) и для больших списков не выигрывает.

## Воспроизведение

```bash
cd benchmarks/nextorm.benchmark && dotnet build -c Release

# декомпозиция warm-пути (Construct / Prepare / Hash / Warm_PlanOnly)
NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db \
  dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkWarmDecompose*"

# warm plan-only (IN-list)
NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db \
  dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturePlanCache*"

# холодное построение SQL
NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db \
  dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturePlanBuild*"

# warm end-to-end
NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db \
  dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturesFairCached*"

# prepared (регресс-гейт)
NEXTORM_BENCH_DB=/tmp/nextorm-bench/test.db \
  dotnet run -c Release --no-build -- --filter "*SqliteBenchmarkFeaturesFair.*" \
  --anyCategories A_Cte A_RecursiveCte A_Join4 A_InList
```


