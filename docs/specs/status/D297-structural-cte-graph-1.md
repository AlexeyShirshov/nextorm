# D297 — Structural CTE graph: PLAN rv1

| Поле | Значение |
|---|---|
| Issue / milestone / collection | [#297](https://github.com/AlexeyShirshov/nextorm/issues/297), OPEN / `1.0.9-rc3` / `1.0.9-rc3-open-3` |
| Branch / исходный HEAD | `1.0.9-rc3` / `adca5cdd` |
| Фаза | PLAN + measured spike only; **STOP на gate §13-unfreeze** |
| Plan state | `ready` (D297-01 исполним; D297-02+ blocked-on-unfreeze) |
| Счётчики | `N=1`, `r=1`, `n=1` |
| Evidence contract | `rv=1` |
| Status | `docs/specs/status/D297-structural-cte-graph-1.md` |
| Evidence root | `.pdca/T297/rv1/` (spike evidence — `.pdca/T297/rv1/spike/`) |
| Manifest | `.pdca/T297/rv1/manifest.json` |
| Предшественники | #265 CLOSED, #275 CLOSED (не переоткрывать); #244 terminal `incomplete` (`D244-warm-cte-1.md` §Terminal disposition) |

**Основание status-пути:** явно заданный dispatcher путь; репозиторная конвенция для новых циклов — `.pdca/status/`, здесь приоритет у явного указания.

Это PLAN of record цикла D297: ни одна строка `src/**` не изменена, ни одно измерение ещё не выполнено. Все числа предшественников ниже помечены как **входные факты (бюджет)**, а не как результаты этого спайка. Терминальный `incomplete` T244 не переоткрывается и не переквалифицируется: D297 — отдельная задача с отдельным порогом входа и отдельной авторизацией.

## Цель и непреложные ограничения

**Вопрос цикла (дословно):** «Can a compact shareable-query-description + copy-on-write candidate plausibly remove ≥2,640 B/query (RecursiveCte) on the real warm path, or is its ceiling far below?»

Ответ даётся **измеренными per-node и per-query числами**: B на узел на реальных типах построения и B на запрос на реальных графах `Cte`/`RecursiveCte` в warm-пути, сверенными замыканием (сумма узлов против end-to-end). Источник-уровневое умножение, оценка «по аналогии» и перенос бюджетных чисел результатом не являются. До получения измерений ответа нет; «вероятно, хватит» запрещено как формулировка вердикта.

Входные факты (бюджет, не результаты спайка) — owner's measured allocation spike по #297, снимок `c9aa8690`, замороженный Release `nextorm.core.dll` SHA-256 `c47bf7a6…0465`, .NET 10.0.12 linux-x64:

| Величина | B/op | Статус |
|---|---:|---|
| `QueryCommand<int>` из существующего definition | 816 | input fact |
| unprepared `Clone()` без joins | 816 | input fact |
| `QueryDefinition` | 240 | input fact |
| fresh definition + command | 1 056 | input fact |
| `QueryShape` + command | 816 | input fact |
| `EntityBuilder` new/clone/Where (warmed metadata) | 400 | input fact |
| control `new object` | 24 | control |
| control preallocated instance | 0 | control |
| derived: Cte (два definition) | 480 | derived input fact |
| derived: RecursiveCte (три definition) | 720 | derived input fact |

**816/240/400 и производные 480/720 — INPUT FACTS.** Это бюджет, посчитанный как «стоимость сайта × число source-level вызовов», а не измеренная оптимизированная композиция. 720 B оставляет RecursiveCte ≈1 920 B до порога входа 2 640, ≈2 762 B до checkpoint 3 482 и ≈4 479 B до final parity 5 199; никакого вывода о достижимости из этого не следует.

**Не-цели:** product implementation; acceptance; #244 и #245 (не переоткрывать, не переквалифицировать, не включать в объём); изменение oracle/порогов/budgets предшественников; unfreeze; commit/merge/push; публичные docs; изменение `docs/specs/status/collection-1.0.9-rc3-open-3.md` (строку D297 обновляет collection primary).

**Замороженная граница (hard §13):** construction-путь в `src/**` — `EntityBuilder`, `QueryBuilder`, `CteHoister` (построение), `QueryCommand` construction. Unfreeze возможен только repo owner'ом, явной §13-авторизацией с перечнем разрешённых файлов и конструкций; **такая авторизация НЕ дана**, и ни один пункт этого плана её не подразумевает.

**Разрешено:** out-of-repo throwaway prototype (вне git-дерева репозитория), benchmark-only arms, status-файл и evidence. Запрещено: любая правка `src/**`, любая правка frozen construction даже «на время», любые acceptance-заявления из InMemory-пробы.

## Критерии приёмки

Критерий спайка — go/no-go по R01–R05. Acceptance #297 этими строками не объявляется; acceptance остаётся за отдельным циклом после unfreeze.

| ID | Требуемый результат | Негативный случай |
|---|---|---|
| **R01** | Доказано отсутствие изменений `src/**` на всём протяжении спайка: scope diff по production-путям пуст и это зафиксировано до/после каждого прогона. | Любой production diff (включая временный, откаченный) ⇒ **STOP**, а не «спайк прошёл». |
| **R02** | Per-node B измерены **на реальных типах** (`QueryCommand`, `QueryDefinition`, `QueryShape`, `EntityBuilder`); per-query композиция измерена **на реальных Cte и RecursiveCte**; числа согласованы замыканием (сумма узлов ≈ измеренный graph cost в пределах объявленного допуска). | Только source-level умножение, synthetic stand-in типы или несведённое замыкание ⇒ evidence недостаточно. |
| **R03** | Out-of-repo COW-prototype воспроизводит **те же** операции: общий core, overlay-on-write, union clone, materialisation; каждая операция наблюдаемо эквивалентна по семантике. | Free writes (запись в общий core), пропущенный clone, скрытая materialisation ⇒ prototype недействителен. |
| **R04** | Измеренная delta-таблица + консервативная проекция (**baseline-min минус candidate-max**) + явный вердикт: demonstrated ceiling <2 640 ⇒ **no-go**; консервативная проекция ≥2 640 ⇒ **plausible** (не acceptance); порог пересекается диапазоном ⇒ **inconclusive**. | Вердикт отсутствует, проекция построена на средних, «plausible» выдано за acceptance ⇒ R04 не выполнен. |
| **R05** | Условный real-warm arm: если R04 даёт **plausible**, выполняется benchmark-only arm `SqliteBenchmarkFeaturesFairCached` (`B_Nextorm_Cached_Cte` / `B_Nextorm_Cached_RecursiveCte`, категории `B_Cte`/`B_RecursiveCte`) под canonical host lock. Если честный arm невозможен без `src/**` — D297-01 **partial** + STOP на §13, обход запрещён. | Arm подменён InMemory-пробой, запущен без lock или объявлен acceptance ⇒ R05 не выполнен. |

**Checkpoint-таблица (пороги предшественника, не пересчитываются):**

| Сценарий | Порог входа (демонстрация) | До checkpoint ≤1.20× | До final parity ≤1.00× |
|---|---:|---:|---:|
| RecursiveCte | −2 640 B/query | −3 482 B/query | −5 199 B/query |
| Cte | — (порог входа относится к RecursiveCte) | −216 B/query | −2 073 B/query |

**Поздний acceptance oracle в этом спайке НЕ запускается и НЕ заявляется:** same-round `SqliteBenchmarkFeaturesFairCached`, per-row, worst-applicable-job, `Warm_Reused=0 B`, prepared `A_*` ≤1.03×. Он требует unfreeze и отдельного цикла; его отсутствие не является провалом спайка, а его имитация — фальсификацией.

## Минимальное решение

**Что делаем.** Строим out-of-repo throwaway prototype компактного shareable query description с copy-on-write и измеряем ровно то, что спрашивает вопрос: per-node bytes на реальных типах и per-query bytes на реальных графах Cte/RecursiveCte в warm-пути, сверяя замыканием. Затем считаем консервативную проекцию и выдаём вердикт.

**Что сохраняем.** Frozen construction path (`EntityBuilder`, `QueryBuilder`, `CteHoister`, `QueryCommand` construction), публичный API, наблюдаемую семантику, query plan cache и его ключи, все существующие пороги/budgets предшественников, тестовые конвенции (FluentAssertions, CRLF, CPM, warnings-as-errors).

**Оптимум.** Один общий неизменяемый core description + overlay-on-write для отличий + явный clone только в точках, где реальный граф обязан разойтись (union, materialisation). Ничего сверх этого: prototype существует, чтобы ответить на вопрос о ceiling, а не чтобы стать будущей реализацией.

| Вариант | Цена / преимущество | Риск | Решение |
|---|---|---|---|
| Размеры типов × source-level вызовы | Дёшево, не требует кода | Не измеряет реальную warm-композицию; воспроизводит только уже известные 480/720 | **Отклонён** |
| GC-byte probe + per-node isolation + out-of-repo matched prototype | Единственный путь к per-node и per-query числам без `src/**`; замыкание проверяемо | Prototype ≠ product; риск artificial sharing | **Выбран** |
| Правка frozen construction в `src/**` | Прямое измерение product-пути | Нарушает §13; требует unfreeze, которого нет | **Запрещён** |

Если R04 даёт no-go, критерии не снижаются и «частичный» кандидат не выдаётся за достаточный. Если R04 даёт plausible, это остаётся гипотезой до unfreeze и отдельного acceptance-цикла.

## Матрица вариантов

| Область | Что покрывается | Закрытие |
|---|---|---|
| Реальные типы | `QueryCommand<T>`, `QueryCommand`, `QueryDefinition`, `QueryShape`, `EntityBuilder<T>`; B на экземпляр, не на «похожий» тип | **Test:** per-node probe, повторяемые серии, публикуются все точки |
| Where + collections | joins-массив, parameters, `Where`/inline IN-коллекции; кто владеет и кто копирует | **Guard:** ни одного shared mutable collection; overlay-копии измеряются отдельно |
| Графы | Cte (два definition) и RecursiveCte (три definition + UnionAll) | **Test:** per-query composition на реальном warm-пути, сверка замыканием |
| Операции prototype | shared core / overlay-on-write / union clone / materialisation | **Test:** COW-изоляция, clone-семантика, отсутствие free writes |
| Types / null flags | nullability и default-флаги, struct-семантика `QueryShape`, отсутствие скрытых боксингов | **Guard:** побитовая эквивалентность описания и наблюдаемой семантики |
| Providers | InMemory — только construction microprobe (unit-tier, без БД); live SQLite — warm-путь; PostgreSQL/SQL Server/MySQL/ClickHouse | **Deferred + trigger:** остальные providers — после unfreeze в D297-03; до этого их поведение не заявляется |
| Cache + lifecycle | plan cache hit, command cloning, повторное использование, отсутствие мутации общего состояния | **Deferred + trigger:** после unfreeze (D297-03); в спайке только read-only identity-проверки |

InMemory-проба никогда не подаётся как acceptance: это construction microprobe уровня unit, без БД и без warm-пути.

## DO-задачи

| Unit | Работа | Статус решения |
|---|---|---|
| **D297-01** | Measured spike: per-node bytes на реальных типах + per-query composition на реальных Cte/RecursiveCte + out-of-repo COW-prototype + консервативная проекция и вердикт R04; условный warm arm R05 | **active**, исполним без unfreeze |
| **D297-02** | Реализация компактного shareable description в construction-пути (`EntityBuilder`/`QueryBuilder`) | **blocked-on-unfreeze**, gate **G13-OWNER**; acceptance не заявляется |
| **D297-03** | `CteHoister`/`QueryCommand` construction, ownership и lifecycle, кэш-ключи | **blocked-on-unfreeze**, gate **G13-OWNER**; acceptance не заявляется |
| **D297-04** | Provider-boundary, regression-suite, coverage и финальные измерения | **blocked-on-unfreeze**, gate **G13-OWNER**; acceptance не заявляется |

**G13-OWNER** = repo owner (AlexeyShirshov) даёт явную §13-авторизацию с перечнем разрешённых файлов и конструкций. Без этой авторизации D297-02…04 остаются `blocked`, их критерии и остаток не меняются; блокер не делает их `done` и не порождает replacement-единиц.

Подзадачи активного спайка:

| Подзадача | Работа | Evidence |
|---|---|---|
| **D297-01a** | Per-node: `QueryCommand` (from existing definition, unprepared clone), `QueryDefinition`, `QueryShape`+command, `EntityBuilder` new/clone/Where, controls | `.pdca/T297/rv1/spike/per-node.json` |
| **D297-01b** | Графы и builder/Cte/UnionAll: реальные Cte (2 definitions) и RecursiveCte (3 + UnionAll), per-query B, замыкание | `.pdca/T297/rv1/spike/graphs.json` |
| **D297-01c** | Out-of-repo COW-prototype, matched operation matrix, delta-таблица, консервативная проекция, вердикт R04 | `.pdca/T297/rv1/spike/delta.json`, `.pdca/T297/rv1/spike/prototype/` |
| **D297-01d** | Условный real-warm arm R05 (только при plausible) либо partial + STOP на §13 | `.pdca/T297/rv1/spike/warm-arm/` |

**Измеренные факты, а не задачи рефакторинга:** 129 полей `QueryCommand`, 52 поля builder-состояния и неиспользуемый `QueryShape` — это наблюдения о размере и связности текущего construction-состояния. Они не являются backlog-единицами D297, не авторизуют чистку и не меняют frozen boundary; в отчёте они приводятся только как контекст цены кандидата.

## Тест-стратегия

- **Probe self-checks (обязательны до любых чисел):** shared-core identity (один core на N описаний), COW-изоляция (запись в overlay не видна соседям), сохранение source/union семантики (операции prototype дают тот же наблюдаемый результат, что и прямые записи), harness controls (`new object` = 24 B, preallocated = 0 B).
- Пустой/нулевой control, несовпадение контроля с 24/0 B или нестабильные серии ⇒ прогон недействителен, числа не публикуются.
- Prototype собирается в **Release**, с warnings-as-errors; сборка prototype — часть evidence, а не «подготовка».
- Self-check результаты пишутся в evidence до measurement-окна; падение self-check блокирует measurement.
- Product-сьюты (`dotnet test`) в этом цикле **не являются** measurement evidence: они не измеряют allocation warm-пути и не заменяют probe.
- Coverage-гейты проекта не меняются: `MIN_LINE_COVERAGE=85` / `MIN_BRANCH_COVERAGE=75`, integration gate 65/75; D297 их не трогает и не пересчитывает.
- Никаких новых skips/suppressions; CRLF во всех создаваемых файлах; FluentAssertions — если в prototype появляются assert-ы, только через него.

## Измерения и команды

Все aliases ниже — **планируемые**; ни одна команда ещё не выполнена.

```text
P=/tmp/opencode/t297-spike
E=.pdca/T297/rv1

G (scope guard):
git status --porcelain=v1 -- src benchmarks/nextorm.benchmark

X (probe build):
dotnet build "$P/t297-probe.csproj" -c Release -warnaserror

S (per-node):
dotnet run --project "$P/t297-probe.csproj" -c Release -- --mode per-node --out "$E/spike/per-node.json"

C (graphs / composition):
dotnet run --project "$P/t297-probe.csproj" -c Release -- --mode graphs --out "$E/spike/graphs.json"

D (prototype + delta):
dotnet run --project "$P/t297-probe.csproj" -c Release -- --mode delta --out "$E/spike/delta.json"

W (conditional warm arm, только при plausible):
NEXTORM_BENCH_FULL=1 dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter '*SqliteBenchmarkFeaturesFairCached*' --anyCategories B_Cte B_RecursiveCte \
  --exporters json csv --artifacts "$E/spike/warm-arm"
```

**Lock protocol.** Единственный canonical host lock — `/tmp/opencode/nextorm-bdn-host.lock`; он берётся снаружи и ровно один раз. Duplicate guard — отдельный marker-файл `/tmp/opencode/t247-measure.guard`, берётся **non-blocking до** host lock; при занятости — сообщение `T297-DUPLICATE-GUARD` и exit 90. Guard не является вложенным host lock, host lock не вкладывается сам в себя. `S`/`C`/`D` — короткие probe-прогоны без BDN, host lock для них не обязателен, но concurrent build/test/benchmark запрещён; `W` — всегда под host lock.

```text
flock -n /tmp/opencode/t247-measure.guard \
  flock -x /tmp/opencode/nextorm-bdn-host.lock \
  <W-команда выше>
```

**Measurement protocol:**

- Controls в каждом прогоне: `new object` = **24 B**, preallocated instance = **0 B**; иначе прогон недействителен.
- Counting: `GC.GetAllocatedBytesForCurrentThread`; результат каждой операции потребляется **вне** измерительного окна (sink), чтобы не учитывать побочные аллокации внутри окна.
- Warmup: **3 000** операций до первого окна; затем **5 rounds × 2 000 ops**.
- По каждому раунду публикуются min/median/max; вердикт строится на консервативной границе (baseline-min минус candidate-max), а не на средних.
- Quiet window: **≥30 s** с CPU <5% до старта; во время окна запрещены concurrent benchmark/build/test, включая чужие lane'ы.
- БД: pinned DB path `/tmp/nextorm-bench/test.db`, sha256 `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295`; несовпадение — стоп.
- InMemory — только construction microprobe; live SQLite — warm-путь; InMemory никогда не acceptance.
- Probe sources и их sha256 фиксируются в evidence; prototype живёт вне git-дерева (`$P`), в репозиторий не попадает.
- Каждый прогон пишет argv, exit code, discovered/executed counts и путь к полному логу.

## Versioned evidence contract rv=1

`rv=1` — первый контракт D297; supersession нет. Все sources/artifacts ниже — **planned**, не утверждение о выполненных проверках. Manifest пишется на границе DO→CHECK.

| Req / row / rv | Проверка и негативный случай | Evidence (invocation → required result) | Артефакты | Владелец / применимость |
|---|---|---|---|---|
| **E01 / R01 / 1** | Scope diff по production-путям до и после каждого прогона; негативный случай — любой diff в `src/**` ⇒ STOP | `G` → пустой вывод на каждом шаге; `git diff --stat -- src` → пусто | `$E/spike/scope/*.txt` | coder; всегда |
| **E02 / R02 / 1** | Per-node B на реальных типах; негативный — synthetic тип или source-level умножение | `S` → exit 0, все пять типов + два контроля представлены, controls 24/0 B | `$E/spike/per-node.json`, build log | coder; всегда |
| **E03 / R02 / 1** | Per-query composition на реальных Cte/RecursiveCte + замыкание; негативный — несведённая сумма | `C` → exit 0, обе формы, closure residual в объявленном допуске | `$E/spike/graphs.json` | coder; всегда |
| **E04 / R03 / 1** | Prototype операций: shared core, overlay-on-write, union clone, materialisation; негативный — free write/пропущенный clone | `D` → exit 0, все четыре операции, self-checks pass | `$E/spike/delta.json`, `$E/spike/prototype/**` | coder; всегда |
| **E05 / R04 / 1** | Delta-таблица + консервативная проекция (baseline-min − candidate-max) + явный вердикт no-go/plausible/inconclusive; негативный — вердикт отсутствует или из средних | `D` → exit 0, вердикт присутствует и однозначен | `$E/spike/delta.json` | coder; всегда |
| **E06 / R05 / 1** | Условный warm arm; негативный — InMemory вместо warm, отсутствие lock, acceptance-заявление | `W` → exit 0, per-row числа, `Warm_Reused=0 B`; при impossible — partial + STOP с причиной | `$E/spike/warm-arm/**` | coder; только при plausible |
| **E07 / R01–R04 / 1** | Harness controls, probe self-checks и Release-сборка с warnings-as-errors; негативный — controls не совпали | `X` → exit 0, 0 warnings; self-check log pass | `$E/spike/build.log`, `$E/spike/self-check.log` | coder; всегда |
| **E08 / R01–R05 / 1** | Доказательства условий окна: DB sha, quiet window, lock ownership, sha256 probe sources | `sha256sum` → pinned value; lock/CPU logs → соответствие | `$E/spike/env/*.txt` | coder; всегда |
| **E09 / CHECK / 1** | CHECK completeness: manifest покрывает E01–E08, все обязательные слоты заполнены, applicability подтверждена; негативный — missing row или N/A без обоснования | `V1` + `V2` → rc 0, required/actual rows согласованы | `$E/manifest.json`, validator logs | coder produces; check verifies; всегда |

**CHECK re-gather budget: 2 точечных запроса суммарно**, owner — `check`. Каждый запрос указывает missing slot, источник и ожидаемый результат. После исчерпания budget отсутствующее evidence остаётся GAP/inconclusive и маршрутизируется по контракту цикла; фабрикация pass запрещена.

## Документация

- Канонический набор этого цикла — только: этот status-файл, `.pdca/T297/rv1/manifest.json` и evidence под `.pdca/T297/rv1/`.
- Публичные docs (`docs/**`, `docs/ru/**`, root `readme.md`) не меняются; публичных ссылок на internal specs не добавляется.
- Acceptance-заявлений нет: ни «улучшено», ни «достигнуто», ни «готово к релизу». Формулировки отчёта — только «измерено», «спроецировано», «вердикт».
- Бюджетные числа 816/240/400/480/720 всегда помечаются как input facts предшественника, отдельно от измерений спайка.
- Строка D297 в `docs/specs/status/collection-1.0.9-rc3-open-3.md` обновляется collection primary, не этим циклом.

## Что не сказала постановка

| Вопрос | Решение / остаточный риск |
|---|---|
| Transient allocations внутри окна | Sink потребляется вне окна; остаточный риск — JIT/GC-шум на первых итерациях, снимается warmup 3 000 |
| Double counting при композиции | Закрывается замыканием E03; при расхождении residual публикуется, а не распределяется по узлам |
| Perturbation от profiler/диагностики | Профайлеры в измерительных окнах не используются; при необходимости — отдельный прогон, помеченный diagnostic и не admitted |
| Artificial sharing в prototype | Прототип может «подешеветь» сильнее product-пути; консервативная проекция обязана это учитывать, R03 требует наблюдаемой эквивалентности операций |
| Hidden materialisation | Любая материализация внутри prototype измеряется как операция, а не прячется в constructor |
| Noisy InProcess BDN | Warm arm только под canonical host lock и в quiet window; шумный раунд не усредняется в pass, а помечается inconclusive |
| Честный warm arm невозможен без `src/**` | D297-01 partial + STOP на §13 (R05); обход через synthetic substitute запрещён |
| Scope creep в «заодно почистить» 129/52/QueryShape | Не задача: только контекст цены; любая чистка требует unfreeze и отдельного плана |

**Уверенность:** высокая в scope, в counting method и в запретах; средняя в том, что prototype удастся честно свести к product-операциям без `src/**`; ответ на главный вопрос (ceiling ≥2 640 или существенно ниже) до измерений неизвестен и не предполагается.

## Предусловия DO

- **G0 (контракт):** этот PLAN зафиксирован в status-файле до первого DO-шага; counting method (`GC.GetAllocatedBytesForCurrentThread`, 3 000 warmup, 5×2 000, min/median/max, sink вне окна) подтверждён как единственный; DB path и sha256 закреплены.
- **G1 (состояние):** снят snapshot dirty tree/HEAD (`adca5cdd`)/toolchain; проверено, что canonical host lock не удерживается чужой lane'ой; в autonomous mode approval-пауза не запрашивается, но gate не отменяется.
- **Design checklist:** frozen boundary перечислена явно, ни одна разрешённая операция её не пересекает; prototype вне git-дерева; acceptance не заявляется; public docs не затрагиваются.
- **Blocker rule:** отчёт о блокере (включая «arm невозможен без unfreeze») **не** делает D297-01 `done`: единица остаётся `blocked`/`partial`, исходные критерии и остаток сохраняются.

## Журнал прогресса (append-only)

2026-10-18 | PLAN | revision r=1 | iteration n=1/3 | PLAN authored by `planner`; status file written by `coder`; counters `N=1, r=1, n=1`, `rv=1`; no `src/**` change, no measurement executed; next step = DO spike **D297-01** (subunits 01a–01d), STOP at §13-unfreeze gate | `docs/specs/status/D297-structural-cte-graph-1.md`, `.pdca/T297/rv1/`
2026-10-18T12:36:14Z | DO | revision r=1 | iteration n=1/3 | measured spike executed: Cte whole 9744.17 B/op, RecursiveCte whole 15440.25 B/op; corrected model commandCount Cte 3 (R=2) / RecursiveCte 6 (R=5), definitionCount 2/3 via runtime Ctes.Count; delta Cte [904.00;1680.00], RecursiveCte [3208.00;4072.00]; verdicts RecursiveCte entry 2640 plausible / checkpoint 3482 inconclusive / parity 5199 no-go, Cte checkpoint 216 plausible / parity 2073 no-go; R05 warm arm open (entry plausible, parity no-go), not executed in this allocation stream | .pdca/T297/rv1/spike/graphs.json, ablate.json, delta.json, decision.md, check.md; manifest.json 29 receipts, verify-evidence.sh exit 0

2026-10-18T12:49:30Z | DO | revision r=1 | iteration n=1/3 | R05 benchmark-only real-provider construction arm executed (D297-01d) — this validates the BASELINE, not the candidate: new benchmark-only file benchmarks/nextorm.benchmark/Sqlite/SqliteBenchmarkConstructAllocation.cs (category B_Construct, construction-only, no Execute/ToList/prepare), 2/2 cases executed, exit 0, Ctes.Count asserted 2 (Cte) / 3 (RecursiveCte) in [GlobalSetup]; B_Nextorm_Construct_Cte 8,816 B/op (Mean 4.695 us, Error 4.214 us, N=3), B_Nextorm_Construct_RecursiveCte 11,376 B/op (Mean 5.531 us, Error 3.857 us, N=3); vs spike InMemory microprobe 9,744.17 / 15,440.25 B/op => 0.905x / 0.737x (real path cheaper, provider-specific, not a candidate gain); DB sha256 32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295 unchanged on both /tmp/nextorm-bench/test.db and the committed seed before/after; duplicate guard (stale, age 12,628 s, not held) and canonical host lock /tmp/opencode/nextorm-bdn-host.lock both held for the whole invocation; quiet-window CPU<5% predicate NOT met — attempt 1 rejected on a foreign dotnet-coverage integration lane (64.75%), then a 6.17-6.38% floor from persistent VBCSCompiler/roslynq/opencode with no build/test/coverage/benchmark workload; recorded as an explicit deviation (allocation is the metric), not a relabelled pass; R05 = baseline-validated, candidate-open: the candidate needs the §13 src/** change and remains unmeasured on the real path by construction — no acceptance, no threshold claimed; no src/** edits, no docs edits other than this status file, no commits; manifest re-sealed to 39 receipts (R05 row updated) and verify-evidence.sh rc 0 | .pdca/T297/rv1/spike/warm/{warm.md,warm.log,hash.log,quiet.log,build.log,raw/results/}, .pdca/T297/rv1/spike/manifest.json

## Spike result (2026-10-18) — D297-01 complete, STOP at §13

**CHECK verdict:** `PASS-WITH-FINDINGS`; строки контракта R01–R05 все **met** (R05 = baseline-validated, candidate-open); loop-back — none.

**Measured per-node (B/op, median):**

| Операция | B/op |
|---|---:|
| `QueryDefinition` | 240 |
| `ctx.From` | 400 |
| builder `Clone` | 400 |
| `Where` cached-expr | 400 |
| `Where` inline | 984 |
| `CreateCommand(existing definition)` | 816 |
| `ToCommand()` | 1 456 |
| `Clone()` | 816 |
| `CloneForCache()` | 816 |
| `CteDefinition` | 64 |
| `UnionAll` | 816 |
| `With` | 120 / 168 |
| controls (`new object` / preallocated) | 24 / 0 |

**Graphs (real warm-путь, microprobe):** Cte — **9 744.17 B/op** (3 commands = 2 replaceable + 1 final; 2 definitions); RecursiveCte — **15 440.34 B/op** (6 commands = 5 replaceable + 1 final; 3 definitions). Counts pinned by ablations: `select_cached_entity=1,456`, `select_cached_alias=1,632`, `operands−step=816`, `clone−operands=816`, `rec_no_clone=+816.08`.

**Prototype (out-of-repo, Release, COW self-checks pass):** core 88, node 40, derive 40, write_first 88, write_second 176, union_clone 40, materialise 816 — B/op.

**Delta (bounded, baseline-min − candidate-max):** Cte — opt **1 680** / cons **1 504** / pess **904**; RecursiveCte — opt **4 072** / cons **3 808** / pess **3 208**.

**Вердикт по checkpoint'ам:**

| Сценарий | Checkpoint | Вердикт | Остаток |
|---|---|---|---|
| RecursiveCte | entry 2 640 | **plausible** | pess surplus 568; opt 1 432 |
| RecursiveCte | checkpoint 3 482 | **inconclusive** | pess residual 274 / cons surplus 326 |
| RecursiveCte | parity 5 199 | **no-go** | residual 1 991 / 1 127 |
| Cte | checkpoint 216 | **plausible** | surplus 688 / 1 464 |
| Cte | parity 2 073 | **no-go** | residual 1 169 / 393 |

**Caveat (CHECK P2, must be recorded):** реальный SQLite construction baseline — **8 816 / 11 376 B/op** (0.905× / 0.737× от microprobe); если реальный граф RecursiveCte — **R=4 / D=2**, а не R=5 / D=3, потолок падает до opt **3 232** / cons **3 056** / pess **2 456**, то есть 2 640 становится **borderline**. Поэтому entry-plausible означает «не исключено», а не «доказано».

**R05:** добавлен benchmark-only arm `B_Nextorm_Construct_Cte` / `B_Nextorm_Construct_RecursiveCte`, прогон под `/tmp/opencode/nextorm-bdn-host.lock`, DB pin `32bf10a0…d72295` проверен до/после; предикат CPU-quiet **не выполнен** (floor 6.17–6.38 %) — зафиксированное отклонение; сам кандидат на реальном пути без замороженного изменения измерен быть не может.

**Hard stop:** D297-01 — done (spike). D297-02 / D297-03 / D297-04 остаются **blocked-on-unfreeze** под gate **G13-OWNER**. Acceptance не заявляется. Никакого T244 re-acceptance, никакой работы по #244/#245.

**Exact question for the owner (verbatim):** «Разрешаете ли вы разморозить §13 (конструкция `EntityBuilder`/`QueryBuilder`/`CteHoister`/`QueryCommand`) для продуктовой реализации compact shared-description + COW, при доказанном потолке 3 208–4 072 B/query (6-command граф) и 2 456–3 232 B/query (реальная SQLite-форма), то есть entry-plausible, checkpoint-inconclusive, parity-no-go?»

**What it unlocks:** D297-02 (design + implementation of the description/COW layer), D297-03 (integration into frozen CTE construction), D297-04 (T244 re-acceptance с неизменным oracle).

**Evidence note:** evidence лежит в `.pdca/T297/rv1/spike/**` и намеренно оставлен **uncommitted** (local evidence policy); он несёт **39 sha256 receipts** и `verify-evidence.sh` rc **0**; `pdca_evidence.py lint` rc **2** — документированный gap tree-receipt (в этом lane нет non-`.pdca` footprint), не pass.

2026-10-18T12:51:42Z | CHECK | revision r=1 | iteration n=1/3 | D297-01 spike closed: CHECK PASS-WITH-FINDINGS, R01–R05 met (R05 baseline-validated/candidate-open), no loop-back; status finalized, STOP at §13 gate G13-OWNER | `.pdca/T297/rv1/spike/{decision.md,check.md,ablate.json,graphs.json,delta.json,manifest.json}`

## Real graph shape (G8)

**Дата:** 2026-10-18. **Host:** WSL2, AMD Ryzen 7 5800HS (8 logical / 4 physical), .NET SDK 10.0.401, runtime 10.0.12. **HEAD:** `ee9549c5` (benchmark harness) поверх baseline `9ffb8bdc`, branch `1.0.9-rc3`. **DB pin:** `32bf10a08291f25afff934f42c378215b6e8f76b8568d6afd519a53a92d72295` совпал на обоих путях (`/tmp/nextorm-bench/test.db`, `benchmarks/nextorm.benchmark/data/test.db`). **CPU admission** (30×1 s `/proc/stat`, формула `run.sh:45-84`): median **4.99 %**, p95 **7.58 %** — admitted (порог median ≤ 8.4, p95 ≤ 11.4); отклонение от G7-floor 6.2–6.4 %: **−1.31 pp** ниже пола, зафиксировано.

**Источник чисел:** probe-only одиночный процесс (`NEXTORM_G8_PROBE_ONLY=1`, benchmark-project-only entry point) — 32/32 probes × 1 001 samples, `Complete: true`, `samples.csv` = 32 033 строки (header + 32×1001). Медианы — `GC.GetAllocatedBytesForCurrentThread` вокруг только конструкции; 64 B BDN-пол **не** вычитается, builder-control = **400 B** по спецификации (не 0). Evidence: `.pdca/T297/rv1/shape/probe-1/{steps.json,samples.csv,stdout.log}`; BDN round-1 не перезапускался.

### D/R по семействам

| Семейство | commands (всего) | definitions (всего) | R | D | Вывод (construction sites) |
|---|---:|---:|---:|---:|---|
| Cte | 2 | 2 | **1** | **1** | body `Select` = 1 command + 1 `QueryDefinition`; `With` = 120 B декларация, 0 command/definition; `From`/`Join` = builder'ы (400 / 1 640 B), 0/0; финальный `Select` из R/D исключён |
| RecursiveCte | 4 | 3 | **3** | **2** | anchor `Select` + step `Select` = 2 command + 2 definition; `UnionAll` = 1 command (clone), 0 definition; `WithRecursive` = 120 B, 0/0; `From` = builder (400 B); финальный `Select` исключён |

**Измеренная база (медианы probe-1, B):**

| Rung | Медиана | Что доказывает |
|---|---:|---|
| `control.noop` / `control.object` / `control.builder` | **0 / 24 / 400** | контроли; builder = 400 B, command/definition не появляются |
| `node.definition` | **240** | `QueryDefinition` = 240 B |
| `rec.iso.union` | **816** | `UnionAll` = 1 command (clone), definition не платит |
| `rec.union_body` − `rec.anchor` − `rec.iso.step` | **7 728 − 2 880 − 4 032 = 816** | clone ровно 816 B в составе цепочки |
| `node.tocommand_fresh_builder` − `control.builder` | **1 456 − 400 = 1 056** | terminal `Select`/`ToCommand` = 240 (definition) + 816 (command) |
| `node.select_cached_fresh_builder` | **1 456** | definition **не** элидируется (см. расхождение ниже) |
| `rec.iso.with_recursive` / `cte.iso.with` | **120 / 120** | `With`/`WithRecursive` — декларация, 0 command/definition |
| `rec.iso.from` / `cte.iso.from` / `cte.iso.join` | **400 / 400 / 1 640** | `From`/`Join` — builder'ы, не command/definition |
| `rec.iso.final_select` / `cte.iso.final_select` | **1 960 / 3 048** | финальный `Select` (исключён из R/D) |

**Расхождение с ожиданием `verify.py`:** verifier ожидает `node.select_cached_fresh_builder = 1 216`
(«isolated Select элидирует 240 B definition») и `node.select_cached = 816`. Измерено **1 456** и
**1 056** — definition платится в обоих случаях; в этом probe изолированный `Select` definition
**не** элидирует. Измеренное значение принято как авторитетное (EC-G8-05), verifier не правился.

### Closure (whole vs Σ parts)

`tolerance = max(64, ceil(0.05 × whole))`.

| Семейство | whole | Σ parts | diff | tolerance | Pass |
|---|---:|---:|---:|---:|---|
| RecursiveCte | 10 208 | 10 208 (2 880 + 4 032 + 816 + 120 + 400 + 1 960) | 0 | 511 | **да** |
| Cte | 8 824 | 8 824 (3 616 + 120 + 400 + 1 640 + 3 048) | 0 | 442 | **да** |

Нулевой остаток — свойство детерминированного счётчика аллокаций: каждая стадия измерена как
константа, и сумма стадий равна целому байт-в-байт.

### D297-модель на измеренной форме

`δopt = 776·R + 64·D`, `δcons = 776·R − 24·D`, `δpess = δcons − 600`.

| Семейство | R | D | δopt | δcons | δpess |
|---|---:|---:|---:|---:|---:|
| Cte | 1 | 1 | **840** | **752** | **152** |
| RecursiveCte | 3 | 2 | **2 456** | **2 280** | **1 680** |

`eps = max(64, 0.05 × gate)`; строгие ветви verifier'а (`verify.py:223-228`): reachable-with-margin
если `δpess > gate+eps`; not-reachable если `δopt < gate−eps`; иначе borderline.

| Семейство | Gate | eps | δopt / δcons / δpess | margin opt | margin cons | margin pess | Вердикт |
|---|---:|---:|---|---:|---:|---:|---|
| RecursiveCte | entry 2 640 | 132 | 2 456 / 2 280 / 1 680 | **−184** | −360 | −960 | **not-reachable** |
| RecursiveCte | checkpoint 3 482 | 174.1 | 2 456 / 2 280 / 1 680 | **−1 026** | −1 202 | −1 802 | **not-reachable** |
| RecursiveCte | parity 5 199 | 259.95 | 2 456 / 2 280 / 1 680 | **−2 743** | −2 919 | −3 519 | **not-reachable** |
| Cte | checkpoint 216 | 64 | 840 / 752 / 152 | **+624** | +536 | **−64** | **borderline** (δpess = gate−eps = 152 ровно) |
| Cte | parity 2 073 | 103.65 | 840 / 752 / 152 | **−1 233** | −1 321 | −1 921 | **not-reachable** |

**Итог по семействам (verifier `family_verdicts`):** RecursiveCte — **not-reachable**;
Cte — **not-reachable** (checkpoint при этом borderline). Это не acceptance: строка §13 остаётся
замороженной, приёмка T244 не перезапускалась.

### BDN cross-check (существующие round-1 артефакты, без перезапуска)

| Канал | Cte | RecursiveCte |
|---|---:|---:|
| probe-1 median (`cte.whole` / `rec.whole`) | 8 824 | 10 208 |
| BDN round-1 `G8_Cte_Whole` / `G8_RecursiveCte_Whole` (Allocated) | 8 297 | 9 520 |
| относительная разница (BDN − probe) | **−5.97 %** | **−6.74 %** |
| WarmDecompose `Construct/100` (round-1) | 8 200.70 | 9 616.18 |
| относительная разница (Construct/100 − probe) | −7.06 % | −5.80 % |

BDN-канал завышает controls на константу **+64 B** (`G8_Control_Noop` 64 vs probe 0,
`G8_Control_Object` 88 vs 24, `G8_Control_Builder` 464 vs 400), поэтому согласие оценивается в
полосе verifier'а `consistent_within_15pct` — **согласуются** (|Δ| ≤ 7.1 %). В 5 % closure-допуск
разница **не** укладывается; это независимые каналы, а не одна и та же величина.

### Границы

- **`src/**` не изменён:** tracked-fingerprint `b7ccf93b…` совпадает с baseline, `git status --porcelain src` пуст (verifier `src` rc 0). Продуктового изменения нет.
- **§13 заморожен:** D297-02/03/04 не запускались, description/COW не реализовывались, пороги/допуски не менялись, #297/#244/#245 не закрывались.
- **Опубликованная цепочка модели отделена от измеренных arm'ов:** `(R=2,D=2) → 1 680/1 504/904` и `(R=5,D=3) → 4 072/3 808/3 208` воспроизведены verifier'ом как published examples; измеренная форма — `(1,1)` и `(3,2)`; смешивать их нельзя.

**Verifier rc (byte-identical копия, `.pdca/T297/rv1/verify/verify.py`):** `shape` 1, `nodes` 1,
`goal` 1, `model` 0, `verdict` 0, `environment` 1, `boundary` 1, `src` 0. Причины: (1) probe — один
прогон, а `shape/nodes/goal/environment` требуют 3/3 BDN-раунда; (2) `boundary` не содержит в
ALLOWED новый `benchmarks/nextorm.benchmark/Program.cs`, разрешённый D1 (verifier не правился);
(3) `shape` не находит фрагмент `cte_projection` в oracle/WarmDecompose (в benchmark-файле он есть).

