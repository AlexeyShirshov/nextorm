# Тёплые агрегаты `Count`/`Sum`: от 3 272/3 672 B к 456/1 016 B на вызов

Задача `T243`, issue [#243](https://github.com/AlexeyShirshov/nextorm/issues/243), milestone `1.0.9-rc3`.
Документ собирает измерения цикла 2026-10-17: аллокации до и после исправления, время, prepared-контроль,
acceptance-набор и постадийную атрибуцию байтов.

## Происхождение чисел и даты

- **2026-10-10** — исторический прогон, откуда взялась постановка #243: `Count` 3 824 B против 2 864 B у
  linq2db (1.34×), `Sum` 4 226 B против 3 490 B (1.21×). Числа взяты из committed-отчёта
  `docs/specs/performance/feature-benchmark-followups-2026-10-17.md` и **не воспроизводились** в этом
  цикле; это другая машина и другое дерево.
- **2026-10-17** — все измерения этого документа. `before`: 12:24:30–12:28:05Z (BDN) и 12:36–12:44Z
  (байтовый оракул, стадии). `after`: 13:07:24–13:11:01Z (BDN), 13:11Z (gate), 13:20:32Z (integration
  boundary), 13:21:23Z и 13:21:30Z (allocation/attribution probes). Хост: WSL2, .NET 10.0.12,
  `InProcessEmitToolchain`, `Job.ShortRun` (warmup 3 / iterations 3).
- Дерево: ветка `1.0.9-rc3`, HEAD `3b0cd73748bc0e7bb6b4b45a208d5aa3eb2aa08f`; `after` снят с рабочего
  дерева, несущего незакоммиченный diff D243 (`after/tree-binding.txt`).

Единицы: `B/op` — байты на одну операцию, µs — среднее BDN. `Count` и `Sum` нигде не усредняются между
собой; каждая строка — отдельный терминал в отдельном раунде.

## Что изменено

Диагностика показала, что тёплый (кэшированный) путь `Count()`/`Sum(x => x.Id)` на каждом вызове заново
строил внутренние scalar-артефакты: определение запроса, список выборки, нормализованный ключ плана и
команду, — хотя кэш планов уже содержал готовый план. Исправление вводит **принадлежащий конкретному
`DataContext`** кэш неизменяемых артефактов скалярной формы:

- **Белый список.** Только `Count()` без аргументов, `LongCount()` без аргументов и `Sum(x => x.Member)`,
  где тело селектора — единственное `MemberExpression`, чей экземпляр **является** параметром лямбды
  (одна отображённая скалярная колонка, nullable или нет). `Avg`/`Min`/`Max`/`Stdev*`/`Var*`/`*_distinct`
  и любые другие тела селектора остаются на обычном пути.
- **Guard-ветки, все fail-closed** (возврат на обычный путь, а не «попытка угадать»): непустые
  аргументы вызова; провайдер не является SQL `DataContext` (in-memory контекст сохраняет свой
  исполнитель); отключён кэш планов контекста; билдер несёт любое SQL-влияющее состояние
  (`IsPlainScalarScope` проверяет поля `_query`, `_condition`, `_filterScope`, `_group`, `_limitBy`,
  `_distinctOn`, `_extremeRow`, `_tablesample`, `_temporal`, `_rowLock`, `_sourceLock`, `_preWhere`,
  `_arrayJoins`, `_windows`, `_having`, `_sorting`, `_joinIntos`, `_joins`, `_table`, `_from`, `Ctes`,
  `Paging`, `Grouping*`, хинты, `Tag`, `CommandTimeout` и т.д.); применим любой фильтр
  (`QueryFilterResolver.GetFilters(...).Count != 0`, включая `IgnoreFilters`); член `Sum` не отображён
  ровно на одну скалярную колонку.
- **Владение и вытеснение.** Кэш живёт в `QueryCache` контекста, ёмкость **16** записей, FIFO-вытеснение,
  линейный поиск. Запись хранит только структурный ключ (типы, терминал, `MemberInfo`) и ключ плана;
  билдер, мутабельная команда, замыкание, текущие аргументы и результат **не удерживаются**. Очистка —
  при `PurgeQueryCache()` (до очистки плана), `DataContextCache.Clear()` и `DisposeStaff()`; статических
  или глобальных словарей нет, два контекста не делят ни одной записи.
- **Публикация и исполнение.** Артефакт публикуется только если план действительно принадлежит
  `QueryPlanStore` для этого типа контекста; иначе он инертен и путь остаётся обычным. Исполнение
  привязывает аргументы текущего вызова к сохранённой prepared-команде, результат пересчитывается
  каждый раз (никакой мемоизации значения), `throwIfNull: false` сохраняет семантику `Sum` по пустому
  набору.
- **Публичный API не менялся**; `Prepare()`, сценарий prepared-команд и shared `QueryCommand` не
  затрагивались (никакого `Cache = false` ради одного вызова).

## Как измерялось

### Кэшированный путь (B4) и prepared-контроль (BP)

Один захват блокировки хоста `flock -x -w 3600 /tmp/opencode/nextorm-bdn-host.lock` на весь блок прогонов,
переменные `NEXTORM_BENCH_DB=<repo>/benchmarks/nextorm.benchmark/data/test.db`, `NEXTORM_BENCH_FULL` не
установлена (отсюда `Job.ShortRun`). Команды (по одному запуску на раунд, `four-1..3`, `prepared-1..3`):

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter "*SqliteBenchmarkAggregates.B_Nextorm_Cached_Count" \
           "*SqliteBenchmarkAggregates.B_Linq2Db_Count" \
           "*SqliteBenchmarkAggregates.B_Nextorm_Cached_Sum" \
           "*SqliteBenchmarkAggregates.B_Linq2Db_Sum" \
  --artifacts artifacts/pdca/D243/rv1/{before|after}/four-N

dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter "*SqliteBenchmarkAggregates.A_Nextorm_Prepared_*" \
  --artifacts artifacts/pdca/D243/rv1/{before|after}/prepared-N
```

Все запуски завершились с кодом 0; каждый раунд дал `*-report.csv`. Скрипты прогонов:
`/tmp/opencode/d243_before.sh`, `/tmp/opencode/d243_after.sh` (идентичны, кроме каталога результатов).

### Acceptance-набор (BA)

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --anyCategories=acceptance --artifacts artifacts/pdca/D243/rv1/{before|after}/acceptance
```

### Байтовый оракул и стадии

SQLite-фикстура `SqliteWarmAggregateAllocationTests` (10 000 строк, те же формы `Count()` и
`Sum(x => x.Id)`, что и в BDN-армах; свежий fluent-запрос и свежий inline-селектор на каждый вызов),
`GC.GetAllocatedBytesForCurrentThread()`, 64 прогрева + 3 серии по 256 вызовов, накладные расходы
пустого цикла 0 B и не вычитаются:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
D243_ALLOCATION_REPORT=<abs>/allocation-probe.json \
  dotnet test tests/nextorm.integration.tests -c Debug --no-build \
  --filter "FullyQualifiedName~SqliteWarmAggregateAllocationTests"

DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
D243_ATTRIBUTION_REPORT=<abs>/after-attribution.json \
  dotnet test tests/nextorm.integration.tests -c Debug --no-build \
  --filter "FullyQualifiedName~SqliteWarmAggregateAllocationTests"
```

### Оговорка о квантовании

`before`-CSV печатал `Allocated` в `KB` (`2.91 KB`, `2.8 KB`, `3.31 KB`, `3.41 KB`), поэтому каждое
`before`-значение — это интервал шириной **±5.12 B** (шаг отображения 0.01 KB = 5.12 B), а
`KB × 1024` даёт 2979.84 / 2867.20 / 3389.44 / 3491.84 B — не целое измерение. `after`-CSV печатал
`Allocated` уже в сырых `B` (`408`, `2864`, `970`, `3490`), и там строка BDN и байты совпадают. Поэтому
сравнение «до/после» по аллокациям пересекает единицы отображения, и выводы строятся на порядке величины
и на порогах (2 864 / 3 490 B), а не на десятых долях байта.

## Кэшированный путь: три раунда, четыре арма

`q` — отношение средних в том же раунде, `Mean(nextorm)/Mean(linq2db)`; это контроль скорости хоста, а не
разрешение ухудшить собственный результат.

### four-1

| Арм | Mean before, µs | B/op before | Mean after, µs | B/op after |
|---|---:|---:|---:|---:|
| `B_Nextorm_Cached_Count` | 18.480 | 2 979.84 | 14.510 | 408 |
| `B_Linq2Db_Count` | 24.810 | 2 867.20 | 24.180 | 2 864 |
| `B_Nextorm_Cached_Sum` | 280.980 | 3 389.44 | 269.590 | 970 |
| `B_Linq2Db_Sum` | 296.170 | 3 491.84 | 302.820 | 3 490 |

`q(Count)`: 0.7449 → 0.6001; `q(Sum)`: 0.9487 → 0.8903.

### four-2

| Арм | Mean before, µs | B/op before | Mean after, µs | B/op after |
|---|---:|---:|---:|---:|
| `B_Nextorm_Cached_Count` | 18.280 | 2 979.84 | 14.770 | 408 |
| `B_Linq2Db_Count` | 23.770 | 2 867.20 | 24.490 | 2 864 |
| `B_Nextorm_Cached_Sum` | 279.870 | 3 389.44 | 274.310 | 970 |
| `B_Linq2Db_Sum` | 289.220 | 3 491.84 | 292.230 | 3 490 |

`q(Count)`: 0.7690 → 0.6031; `q(Sum)`: 0.9677 → 0.9387.

### four-3

| Арм | Mean before, µs | B/op before | Mean after, µs | B/op after |
|---|---:|---:|---:|---:|
| `B_Nextorm_Cached_Count` | 18.410 | 2 979.84 | 14.540 | 408 |
| `B_Linq2Db_Count` | 24.330 | 2 867.20 | 24.700 | 2 864 |
| `B_Nextorm_Cached_Sum` | 278.830 | 3 389.44 | 271.250 | 970 |
| `B_Linq2Db_Sum` | 292.230 | 3 491.84 | 293.050 | 3 490 |

`q(Count)`: 0.7567 → 0.5887; `q(Sum)`: 0.9541 → 0.9256.

Медианы по трём раундам: `q(Count)` 0.7567 → **0.6001**, `q(Sum)` 0.9541 → **0.9256**. Аллокации внутри
каждого раунда идентичны во всех трёх повторах: `Count` 408 B против 2 864 B, `Sum` 970 B против 3 490 B.

Gate `eng/perf/d243_aggregate_gate.py` проверяет каждую строку каждого раунда отдельно (6 строк
аллокаций и 2 строки времени) и завершился с кодом 0; нарушения: нет.

## Prepared-контроль (BP): без изменений

| Арм | B/op (before, 3 раунда) | B/op (after, 3 раунда) | Mean before, µs | Mean after, µs |
|---|---:|---:|---|---|
| `A_Nextorm_Prepared_Count` | 408 / 408 / 408 | 408 / 408 / 408 | 14.230 / 15.050 / 13.980 | 14.610 / 14.320 / 14.310 |
| `A_Nextorm_Prepared_Sum` | 410 / 410 / 410 | 410 / 410 / 410 | 268.450 / 268.770 / 267.010 | 267.280 / 270.590 / 269.390 |
| `A_Nextorm_Prepared_GroupByCount` | 4 736 × 3 | 4 736 × 3 | 1 235.950 / 1 244.790 / 1 230.980 | 1 245.190 / 1 251.210 / 1 239.440 |

Ни одна аллокация prepared-пути не выросла (числа совпадают байт в байт), время осталось в полосе шума
ShortRun: отношения к соответствующим раундам `before` не превышают 1.20 ни для одного арма.

## Acceptance-набор: 7 кейсов, 48 s → 44 s

| Класс | Кейс | Mean before | Mean after | B/op before | B/op after |
|---|---|---|---:|---:|---:|---:|
| `InMemoryBenchmarkAggregates` | `Nextorm_Count` | 2.148 ms | 2.125 ms | 327 997.44 | 327 997.44 |
| `InMemoryBenchmarkGroupBy` | `Nextorm_GroupByCount` | 53.85 ms | 55.89 ms | 52 470 743.04 | 52 470 743.04 |
| `SqliteBenchmarkAny` | `Nextorm_Cached` | 1.931 ms | 2.050 ms | 546 478.08 | 546 478.08 |
| `SqliteBenchmarkCachedPlan` | `Cached_PlanOnly_Param` | 390.3 µs | 392.8 µs | 366 407.68 | 366 407.68 |
| `SqliteBenchmarkCachedPlan` | `Prepared_ToList` | 966.8 µs | 975.7 µs | 77 967.36 | 77 967.36 |
| `SqliteBenchmarkCachedPlan` | `Cached_ToList` | 1 565.4 µs | 1 590.1 µs | 444 375.04 | 444 375.04 |
| `SqliteBenchmarkWhere` | `Nextorm_Cached_ToListAsync` | 1.962 ms | 2.120 ms | 567 070.72 | 567 070.72 |

- Кейсов выполнено ровно **7**, пропусков нет; wall **48 s → 44 s** (отношение 0.917), BDN
  `Global total time` 45.86 s → 41.49 s; код выхода 0 в обоих прогонах.
- Ключевое отношение `Cached_ToList / Prepared_ToList` по времени: **1.619 → 1.630** (+0.7 %), при
  задокументированной базовой линии 1.87 и пороге 2.244; по аллокациям отношение **5.700 → 5.700**
  (базовая линия 7.42, порог 8.904). То есть быстрый путь не «украл» время у prepared-пути.
- Пофакторные отношения после/до по времени: 0.989, 1.038, 1.062, 1.006, 1.009, 1.016, 1.081 — все ниже
  порога 1.20; аллокации всех семи кейсов не изменились.

## Атрибуция по стадиям

Стадии измеряются тремя отдельными невложенными циклами (каждый со своим прогревом 64 вызова):
**A** — построение fluent-запроса и команды без терминала и без БД, **B** — полный терминал, **C** —
prepared-контроль через публичный `ExecuteScalar(IPreparedQueryCommand<T>)`. `stage_overlap = false`.

| Стадия, B/вызов | Count before | Count after | Sum before | Sum after |
|---|---:|---:|---:|---:|
| A — построение | 1 784 | 1 784 | 1 624 | 1 624 |
| B — полный терминал | 3 272 | **456** | 3 672 | **1 016** |
| C — prepared-контроль | 456 | 456 | 456 | 455.06 |
| B − A | 1 488 | −1 328 | 2 048 | −608 |
| неатрибутированный остаток `(B−A) − C` | 1 032 | −1 784 | 1 592 | −1 063.06 |

`before`-числа взяты из `artifacts/pdca/D243/rv1/red/attribution.json` (pre-fix дерево, тот же фикстур и
та же техника); `after` — из `artifacts/pdca/D243/rv1/attribution/after-attribution.json`. До исправления
остаток 1 032/1 592 B на вызов приходился на работу терминала, не объяснённую ни построением, ни
исполнением prepared-команды; после исправления `B` сравнялся с `C` (Count) и с точностью до байта
приблизился к `C` (Sum).

Отрицательные `B − A` и остаток на исправленном дереве — не утечка и не «отрицательные аллокации»: стадии
определены для pre-fix пути, где `B ≥ A`, а теперь тёплый терминал вообще не выполняет построение, которое
измеряет A. Сопоставим только сам `B`; остаток публикуется численно, как требуется контрактом, но
интерпретировать его как «необъяснённые байты» на новом дереве нельзя.

## Что подтверждено тестами и прогонами

- **Integration boundary** (контейнерные провайдеры действительно работали): `Total: 3991, Errors: 0,
  Failed: 0, Skipped: 199, Not Run: 0, Time: 43.962s`, код выхода 0. PostgreSQL, SQL Server, MySQL,
  MariaDB и ClickHouse поднимались и доходили до `ready`; пропуски — только по возможностям провайдеров
  (`This provider ...`), ни одного пропуска из-за недоступности провайдера. SQLite выполняется в процессе.
- **Байтовый оракул после исправления**: `Count` 456 B/запрос (все 3 серии по 116 736 B), `Sum` 1 016 B
  (260 096 B × 3), пороги 2 864 / 3 490 B — оба проходят; на pre-fix дереве тот же фикстур давал
  3 272 / 3 672 B и падал по порогу (red → green).
- **Gate**: `gate.json` — 6 строк аллокаций и 2 строки времени, `status: pass`, нарушений нет;
  red-режим на pre-fix данных падал по аллокациям, как и должен.
- **Safeguards**: 24 новых теста (15 SQLite, 4 core in-memory, 5 core artifact-store) на closure/parameter
  freshness, фильтры, повторные вызовы, очистку кэша, thread-local recipe и отсутствие sticky
  `_dontCache`; core 2 617/0/0 и SQLite 1 782 passed / 1 pre-existing skip.

## Deferred register (всё остаётся в milestone `1.0.9-rc3`)

| Что | Почему не сейчас | Триггер возврата |
|---|---|---|
| `Avg`/`Min`/`Max`, статистические агрегаты и async fast path | не требуются для двух измеренных целей | новый пофакторный allocation regression либо доказанно бесплатное расширение |
| Captured/composite селекторы | нужны дополнительные гарантии привязки | измеренный allocation hotspot в таком селекторе |
| JoinInto/filter/temp/nested источники | высокая стоимость ключа и инвалидации; корректный fallback достаточен | новый измеренный провал соответствующего сценария |
| Сценарии Category-B из #240 | явный non-goal #243 | отдельное измерение покажет регрессию от этого diff |
| Интеграция gate с общим `iteration14_gate.py` | избегаем коллизии с соседними задачами | владелец коллекции интегрирует после барьера |
| Обновление общих comparison/followups-таблиц | shared docs владелец — родительская задача | передача результатов задачи |

## Ограничения и чего отчёт не утверждает

- **ShortRun, а не долгий job.** 3 итерации на кейс: `Error` в таблицах — не пригодный acceptance-интервал,
  выводы о времени строятся на отношениях `q` и их медианах, а не на `Mean ± Error`.
- **Один хост, один рантайм.** Все числа получены на одной машине (WSL2, .NET 10.0.12,
  `InProcessEmitToolchain`); переносить абсолютные значения на другой хост нельзя.
- **Квантование `before`.** Значения `before` по аллокациям получены из отображения в KB и имеют окно
  ±5.12 B; целых «измеренных» байтов в архиве `before` нет. `after` печатался в `B`.
- **Исторические числа — не воспроизведение.** 3 824 / 4 226 B от 2026-10-10 относятся к другому дереву и
  хосту; в этом цикле pre-fix дерево дало 2 979.84 / 3 389.44 B/op. Отчёт не выдаёт исторические значения
  за новые измерения.
- **Стадийная модель после фикса.** `B − A` и остаток отрицательны, потому что терминал больше не
  выполняет построение; это объяснено выше и не означает утечку или «экономию байтов» сверх измеренного `B`.
- **Байтовые пробы — только SQLite.** Для PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse аллокации в этом
  цикле не измерялись; integration-прогон подтверждает корректность поведения, а не байтовые числа.
- **Публичный API и prepared-путь не менялись**, ускорение prepared-пути не заявляется (его аллокации
  остались 408 / 410 / 4 736 B).
- 199 пропущенных integration-тестов — это отказы по возможностям провайдеров, а не недоступные
  провайдеры; ни один контейнерный провайдер не был пропущен целиком.
