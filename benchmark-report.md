# nextorm vs Dapper / EF Core / linq2db — отчёт по бенчмаркам

Машина: AMD Ryzen 7 5800HS, 16 логических / 8 физических ядер, Linux (WSL2), .NET 10.0.12, BenchmarkDotNet 0.15.8.
БД: `nextorm.benchmark/data/test.db` (SQLite; `simple_entity` ~10k строк, `large_table` ~10k строк).

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
- `nextorm.core/DataContext/ResultSetEnumerator.cs` — потоковое чтение/маппинг.
- `nextorm.core/DataContext/IDataContext.cs` — `GetAsyncEnumerable`.
- `nextorm.core/DataContext/DbContext.cs` — `GetMap`, `MapColumn`, `GetDbCommand`, `FirstOrDefaultAsync`.
- `nextorm.core/Query/QueryCommand.cs` — `PrepareCommand`, `First/FirstOrDefault/Single`.
- `nextorm.sqlite/SqliteDbContext.cs`, `nextorm.sqlite/SQLiteFunctions.cs` — провайдер.

## Воспроизведение
```bash
# полный прогон одного класса
NEXTORM_BENCH_FULL=1 \
NEXTORM_BENCH_DB=$PWD/nextorm.benchmark/data/test.db \
dotnet run -c Release --project nextorm.benchmark -- --filter "*SqliteBenchmarkFirst.*"
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

Проверки: `nextorm.sqlite.tests` 102/102, `nextorm.core.tests` 35/35 — зелёные.

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

Тесты: `nextorm.sqlite.tests` 102/102, `nextorm.core.tests` 35/35.

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
