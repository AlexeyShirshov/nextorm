# D167 — CSV streaming: chunked read for byte[] fields (task status)

- collection: `1.0.9-rc2` (phase P)
- task_id: D167 (GitHub issue #167, milestone 1.0.9-rc2)
- selected_variant: pdca-dotnet
- cycle: N=1
- plan_revision: r1 (revision r=1)
- attempt: n=2 (CHECK loop-back: REQ-06 surface/params gap)
- evidence_contract: rv=1
- plan_state: ready
- task_status: done
- check: PASS r1/N=1/rv=1
- evidence_root: artifacts/pdca/D167/r1/
- base: branch `1.0.9-rc2` @ `18659e41`
- provenance: issue url https://github.com/AlexeyShirshov/nextorm/issues/167 (state Open, label enhancement)
- note: COMPLETE — CHECK PASS r=1/N=1/rv=1; chunked CSV read for direct stored byte[] fields shipped (bounded GetBytes + Base64 3-byte carry; buffered fallback). Defect history below; plan body retained verbatim.
- contract revision r1/rv1; CHECK re-gather budget and owner as pinned in the evidence-contract section.

## Defect history

| defect key | class | observed | fix applied | evidence |
|---|---|---|---|---|
| parameter-crash | P1 | r1/n1 — chunked admission called `GetBytes` on bound-parameter/expression/function ordinals → SQLite native reader crash (exit 139, no rows written) | direct-stored byte[] eligibility (`IsDirectStoredColumn`/`IsDirectByteArrayRead`); parameter/expression ordinals keep the buffered path | `artifacts/pdca/D167/r1/D3b/int-sqlite-crash-before-fix.log` |
| Excel first-byte latch | Critical | r1/n1 — Excel first-byte guard evaluated before the carry merge, wrong byte at field index 0 (5/6 ExcelMode cases red) | post-carry latch order corrected; 6/6 ExcelMode cases green (sha256-verified restore) | `artifacts/pdca/D167/r1/D3c/fix1-red.log`, `fix1-green.log` |
| eligibility hardening | Critical | r1/n1 — direct-stored check admitted computed/unmapped columns | require `PhysicalColumnName` + mapped non-computed metadata (guard probes updated) | `artifacts/pdca/D167/r1/D3c/build.log` |
| sync cancellation | P1 | r1/n1 — sync path read all chunks without observing `CancellationToken` | CT checked between `GetBytes` reads | `artifacts/pdca/D167/r1/D3c/core-chunk.log` |
| clearArray / A3 perf | P1 | r1/n1 — fixed 12 KiB `ArrayPool` input rent returned with `clearArray:true` dominated small-payload Mean (64 B +134.4%) | `clearArray:false` on input returns (exception-safe rent/FIX4 intact); CSV perf A1/A2/A3 PASS (64 B +17.5% ≤20%) | `artifacts/pdca/D167/r1/D4e/` |
| REQ-06 surface/params | P1 | r1/n2 (CHECK→DO loop-back) — mandatory public-surface/params variants not exercised | new SQLite test over both surfaces × sync/async × default/custom options × positional params (byte-identical, real 1 MiB+1 BLOB) | `artifacts/pdca/D167/r1/D6/` |

---

# D167 — CSV streaming: chunked read for byte[] fields

## Статус и границы

- Задача: **D167**, issue [#167](https://github.com/AlexeyShirshov/nextorm/issues/167), milestone **1.0.9-rc2**.
- База планирования: ветка `1.0.9-rc2`, commit `18659e41`.
- Вариант: `pdca-dotnet` + `nextorm-pdca`; collection phase **P**.
- **Cycle N=1; revision r=1; attempt n=1; evidence contract rv=1; plan_state=ready.**
- Статус-файл: `docs/specs/status/rc2-167-csv-chunked-read-1.md`.
- Это только PLAN. Код, тесты и замеры ещё не выполнены; перечисленные будущие источники evidence являются **планируемыми**.
- План готов для durable checkpoint PLAN→DO. **DO не разрешён самим этим документом:** перед ним обязательны запись плана, проверка применимости collection-плана и закрытие предусловия P0 ниже.

## 1. Цель

Устранить материализацию целого бинарного поля на поддерживаемом CSV chunked-path: читать поле через `IDataRecord.GetBytes` ограниченными порциями, кодировать Base64 с правильной обработкой границ трёхбайтовых групп и постепенно передавать результат в destination.

Сохранить байтовую совместимость CSV, обе публичные поверхности, sync/async, параметры, отмену, владение ресурсами и существующую семантику `ValueTransform`.

Область гарантии: память **CSV-реализации**, а не внутренняя память драйвера или пользовательского destination. `MemoryStream`, сохраняющий весь экспорт, сам по себе не доказывает ограниченную память реализации.

Источник требования: `docs/specs/roadmap/todo_csv_streaming.md:66-74`, §2(b).

## 2. Критерии приёмки

| ID | Наблюдаемый положительный результат | Негативный случай |
|---|---|---|
| REQ-01 | Для chunked-path CSV побайтно совпадает с текущей реализацией: NULL; пустой массив; длины 1, 2, 3, 4; границы буфера и поля существенно больше буфера | Потерянный байт, лишний padding, изменённые кавычки, delimiter, NULL marker или newline — дефект |
| REQ-02 | Chunked-path использует `GetBytes`; размер каждого запроса ограничен логической ёмкостью буфера; нет массива/строки размером с поле | Любой `GetFieldValue<byte[]>`, `GetValue` с получением массива, полный Base64 string либо рост row-buffer пропорционально BLOB — дефект |
| REQ-03 | Произвольные короткие чтения, включая 1–2 байта, корректно собираются в трёхбайтовые группы; padding появляется только в конце поля | Кодирование каждой короткой порции отдельно с промежуточным `=` — дефект |
| REQ-04 | Chunked-path допускается только при известном применённом sequential-режиме и совместимой колонке/политике | Неизвестная provenance reader, неподдерживаемая конфигурация или ошибка `GetBytes` не приводят к попытке «дочитать» поле другим способом |
| REQ-05 | Существующий `ValueTransform` получает целый `byte[]` для непустого поля; NULL и пустое поле сохраняют текущий обход transform; результат transform, включая `null`, обрабатывается как прежде | Transform над отдельными chunks, вызов для пустого массива либо Base64 вместо результата transform — дефект |
| REQ-06 | `QueryCommand<TResult>` и `EntityBuilder<TEntity>` поддерживают sync и async, существующие `params` и значения по умолчанию | Работает только одна поверхность или параметры перестают передаваться — дефект |
| REQ-07 | Async учитывает `CancellationToken` при открытии/чтении строк, между бинарными чтениями и при передаче вывода | Продолжение чтения следующих chunks после наблюдаемой отмены — дефект |
| REQ-08 | Destination не закрывается и не Dispose; прежняя семантика завершающего flush сохраняется; внутренние reader/буферы освобождаются при успехе, отмене и ошибке | Закрытый пользовательский Stream, утечка арендованных буферов или flush на каждый chunk — дефект |
| REQ-09 | CSV sequential-настройка локальна вызову; подготовка остаётся `storeInCache:false`; общий `QueryCommand` не мутируется | Sticky `Cache=false`, изменение последующих запросов или помещение несовместимого плана в cache — дефект |
| REQ-10 | SQLite CSV не получает служебный locator; проекции, DISTINCT, JOIN и агрегатные CSV-запросы сохраняют SQL-семантику | Простое игнорирование лишнего `rowid` в выводе при изменившемся SQL — недостаточно |
| REQ-11 | Provider matrix закрыта реальными результатами; для buffered fallback явно отсутствует гарантия bounded-memory | Пропущенный контейнерный provider либо buffered fallback, выданный за chunked streaming, — незакрытый критерий |
| REQ-12 | Замеры показывают ограниченную память chunked-path и приемлемую скорость; cached-path acceptance выполнен | Отсутствие измерений per-row изменения или превышение согласованных порогов — незакрытый критерий |
| REQ-13 | EN/RU, XML-doc, roadmap и naming register согласованы с фактической гарантией | Документация обещает chunking для transform/unsupported provider либо ссылается из публичных страниц на specs — дефект |
| REQ-14 | Для всех применимых строк контракта есть evidence текущего `rv`, артефакты и успешная валидация report | Missing, skipped, пустой test selection или неработающий validator не считаются PASS |

## 3. Минимальное решение

### Три ответа

1. **Что действительно требуется:** изменить получение и выдачу непосредственно бинарного поля, а не только заменить вызов Base64-кодировщика.
2. **Что нельзя нарушить:** публичный API, CSV-байты, transform, последовательность ordinal-доступа, локальность command options, ownership/flush и SQL-семантику SQLite.
3. **Оптимум в этих ограничениях:** внутренний descriptor chunked-колонки + отдельная sync/async выдача поля; локальная CSV-настройка sequential reader; явное подавление locator только для CSV; существующий buffered путь для заранее определённых несовместимых режимов.

### Альтернативы

| Подход | Плюсы | Минусы / цена / риск | Решение |
|---|---|---|---|
| Заменить byte[] callback на цикл `GetBytes`, оставив opaque `Action` и row accumulation | Наименьший локальный diff | Не обеспечивает async drain; может сохранить целый Base64 в row-buffer; не решает provenance reader | Отвергнут |
| Включить sequential и проигнорировать trailing SQLite `rowid` | Мало изменений подготовки | Locator добавляется независимо от наличия LOB; может изменить DISTINCT/aggregate/JOIN SQL, не только `FieldCount` | Отвергнут |
| Descriptor бинарной колонки, bounded выдача, локальный sequential request и подавление CSV locator | Сохраняет публичный контракт; проверяемые ограничения памяти и порядка | Несколько внутренних файлов подготовки и CSV; нужны provider regression и acceptance benchmarks | **Выбран** |
| Новый публичный opt-in API / binary-mode option | Позволяет явный strict режим | Расширяет API без требования issue и оставляет исходный сценарий без исправления | Не вводим в D167 |

### 3.1. Как колонка достигает chunked writer

Опора: `CsvColumn.Write` — opaque `Action<IDataRecord,CsvRowBuffer>` (`CsvStreamWriter.cs:11,21`), ordinal известен в `BuildColumn` (`:257`), запись идёт по `plan.Columns` (`:378-383`).

- Сохранить существующий callback для обычных колонок и buffered binary-path.
- Дополнить внутренний descriptor колонки признаком chunked binary и reader ordinal. Названия новых внутренних членов — предмет реализации, не существующие symbols.
- Выбирать descriptor после сохранения существующего приоритета provider typed mapping.
- Chunked eligibility требует:
  - фактически применённого CSV sequential-режима;
  - непосредственного бинарного storage mapping;
  - отсутствия converter/typed mapping, меняющего существующий способ получения значения;
  - отсутствия `ValueTransform`;
  - политики поля, позволяющей определить обрамление до чтения всего Base64.
- Для delimiter, пересекающегося с Base64-алфавитом, или иной политики, требующей просмотра всего результата, сохранить buffered путь. Это guard совместимости, не новая «потоковая» гарантия.

Sync/async row-dispatcher обрабатывает такой descriptor отдельно. Не пытаться спрятать async передачу destination внутрь синхронного `Action`.

### 3.2. Чтение и Base64

- Логическая бинарная ёмкость: **12 KiB = 12 288 байт**, кратна трём. Размер фактического массива `ArrayPool` не является размером разрешённого запроса.
- Ограниченный арендованный byte-buffer; ограниченный char-buffer для Base64.
- Не запрашивать длину поля посредством `GetBytes(..., buffer:null, ...)`.
- Поддерживать монотонный byte-offset и остаток 0–2 байта. Короткое чтение не означает EOF; EOF — результат `0`.
- Кодировать только полные трёхбайтовые группы; хвост кодировать один раз после EOF.
- NULL обрабатывать до бинарного чтения; пустое non-null поле сохранить как пустое поле.
- Перед бинарным полем передать уже накопленный текст в sink; выдавать Base64 ограниченными порциями. **Не допускать накопления полного бинарного поля в `CsvRowBuffer`.**
- Drain внутренних буферов не равен `destination.Flush`.
- Возврат арендованных буферов — в `finally`.

На async-пути `GetBytes` остаётся синхронным API `IDataRecord`; отмена проверяется между чтениями. Не обещать прерывание уже выполняющегося синхронного provider-вызова.

### 3.3. Открытие reader и SQLite

Опора: `PrepareResultCommand` уже использует `storeInCache:false`, но `sequentialAccess:false` (`DataContext.cs:481-487`). `SequentialAccess` не входит в `QueryCommand`; prepared options не входят в cache key.

- Передавать **внутренний локальный CSV read request** от обоих CSV-terminal к подготовке.
- Для PostgreSQL, SQL Server и SQLite применять sequential reader.
- Передавать обратно достоверный режим подготовки вместе с reader/его внутренним handle. Не выводить его из типа reader или наличия `GetBytes`.
- Все не-CSV callers сохраняют текущие defaults.
- Сохранить `storeInCache:false`, `createEnumerator:false`, текущую streaming-rows настройку; не менять поля общего `QueryCommand`.

**SQLite: подавить locator на CSV-пути.**

Добавить внутренний локальный признак подавления LOB locator, протянуть его через подготовку/SQL build context и учитывать в условии `SqlBuilder.cs:524-528`. Default сохраняет прежнее поведение `ToStream`, `ToTextReader`, `ToDataReader`.

Это не глобальное отключение SQLite locator и не новый публичный option. CSV не должен менять SELECT добавлением `rowid`.

### 3.4. Provider fallback и fail-closed

Два различимых режима:

1. **Заранее выбранный buffered compatibility-path:** MySQL/MariaDB/ClickHouse с текущим `SupportsSequentialAccess=false`; transform; несовместимая field policy/mapping. Старое поведение сохраняется, bounded-memory не обещается.
2. **Выбранный chunked-path:** требует подтверждённого sequential-режима. Если provenance отсутствует или `GetBytes` не поддерживается/падает, операция завершается ошибкой; **нет** позднего fallback на `GetFieldValue<byte[]>`, повторного чтения или переоткрытия.

Таким образом fail-closed относится к допуску и исполнению chunked-режима, а не к необоснованному запрету ранее работающего buffered CSV у неподдерживаемых providers.

## 4. Чего не сказала постановка

| Пробел | Закрытие |
|---|---|
| Распространяется ли bounded-memory на driver и destination? | Допущение: нет; измеряется CSV implementation. Ограничение фиксируется в EN/RU |
| Как chunking взаимодействует с transform? | Доказательство: transform получает целый массив (`CsvRowBuffer.cs:168-186`); сохраняем buffered compatibility-path |
| Достаточно ли `GetBytes` без sequential? | Нет. Guard по фактически применённому reader mode |
| Что делать с SQLite locator? | Доказательство: условие добавляет locator для любого sequential SELECT (`SqlBuilder.cs:524-528`); подавляем локально для CSV |
| Можно ли накопить весь Base64 в row-buffer? | Нет: это противоречит цели. Bounded drain входит в REQ-02 |
| Какие policy/converter ветви совместимы? | Узкая разведка S0 до реализации; defaults и guards заданы выше. Новый необходимый вариант требует PLAN-revision, а не скрытого ослабления |
| Где evidence validator? | Его нет. **P0 — активное предусловие**, описано ниже; gate нельзя заменить `iteration15_evidence.py` |
| Нужна ли настройка Stryker? | Инструмента и config нет. Не считать mutation выполненным; зафиксировать unavailable и точный список mutation-непроверенных ветвей |
| Есть ли collection predecessor? | Предшествующий результат другой feature-задачи не требуется; tooling P0 — самостоятельная зависимость |
| Каковы baseline cached-path ratios? | Источник — `docs/specs/performance/acceptance-benchmarks.md`; значения извлекает scout перед замером, не выдумываются в PLAN |

## 5. Предусловия и разведка

### P0 — обязательный evidence validator

**Состояние:** active prerequisite; D167 остаётся active, blocked на P0 до его закрытия. Нет supersession исходной задачи.

`scripts/validate_inner_loop.py` отсутствует. До первого DO-edit tooling-owner должен предоставить в workspace реализацию validator, соответствующую глобальному `pdca-dotnet`, и доказать:

- приём валидного `brief`;
- отклонение неполного/некорректного `brief`;
- приём полного report;
- отклонение missing/invalid report;
- совместимость с закреплёнными строками `rv=1`.

Порядок:

1. Read-only scout проверяет источник нормативной реализации/полной схемы у установленного global skill.
2. Provisioning validator выполняется как **отдельное tooling-предусловие до DO**, а не как продуктовая правка без brief-gate.
3. После provisioning выполняются обязательные точные вызовы `scripts/validate_inner_loop.py`.
4. Если нормативный источник/схема не найдены, запросить tooling-owner; не писать предположительный validator, не использовать другой скрипт и не начинать DO.

Отсутствие файла сейчас — установленный факт; невозможность его предоставить ещё не доказана. Это **предусловие + недостаток tooling-evidence**, не объявленный внешний blocker. При стойкой низкой уверенности после точечного scout оркестратор вызывает `escalate` по триггеру 5.

### S0 — узкая разведка нужна

Владелец: `scout`, read-only, C# symbols только Roslyn.

Проверить:

- весь путь `CsvRowBuffer` → sink: накопление, drain, flush, sync/async;
- precedence provider typed mapping и converter для byte[];
- реальные quote/escape/policy варианты и совместимость delimiter с Base64;
- threading reader behavior и локального suppress-locator;
- семь acceptance cases и нормативные cached/prepared ratios;
- доступность integration runtime согласно integration skill.

**Наблюдаемый результат:** отчёт с существующими `file:line`, перечнем eligibility guards и подтверждением возможности bounded sync/async drain без изменения публичного API. Если необходимая несовместимая ветвь не покрыта матрицей, вернуть PLAN с обоснованием; не расширять реализацию молча.

Это reconnaissance реализации, а не разрешение заново выбирать цель.

## 6. Задачи и footprint

Все перечисленные исправления — **fix now**, кроме явно отложенного расширения provider streaming.

| Единица | Файлы / исходные anchors | Работа и критерий завершения |
|---|---|---|
| D1 — reader mode / locator | `DataContext.cs:452-487`; `QueryCommandExtensions.cs:409-466`; `QueryPlanner.cs:539,546-599`; `SqlBuildContext.cs:34`; `SqlBuilder.cs:524-528` | Локальный CSV request и mode provenance; sequential на поддерживаемых providers; локальный suppress-locator; shared command/cache invariants; SQLite и LOB regressions |
| D2 — binary descriptor / bounded output | `Query/Csv/CsvStreamWriter.cs:11,21,68,248-258,376-390`; `CsvRowBuffer.cs:168-186,265-284`; `CsvValueFormatter.cs:52` | Descriptor, eligibility guards, chunking/carry, bounded drain, sync/async dispatch, cancellation/finally; legacy transform и buffered path сохранены |
| D3 — тесты контракта | Новый **планируемый** `tests/nextorm.core.tests/CsvChunkedReadTests.cs`; существующие CSV/SelectExpression tests; SQLite CSV tests; `CommonTestSuite.Csv.cs`; provider-specific CSV tests | Red↔green probe; полная матрица; public surfaces/params/resources; реальные provider runs |
| D4 — измерения и evidence | Новый **планируемый** `benchmarks/nextorm.benchmark/CsvBinaryChunkedBenchmark.cs`; `artifacts/pdca/D167/r1/**` | Paired legacy/chunked benchmark, mandatory acceptance, coverage/branch delta, mutation availability и report validation |
| D5 — документация / register | EN/RU guides, limitations, API reference; `CsvStreamOptions.cs` и public-terminal XML-doc при необходимости; roadmap §2(b); naming register | Точная область streaming/fallback, память, CT/ownership; §2(b) завершён только в поддерживаемой области; register согласован |

**Изменяемый footprint:**

- перечисленные core CSV и preparation/SQL-build файлы;
- CSV-тесты core, SQLite, integration и при необходимости существующие provider mapping tests;
- `docs/guide/28-streaming-data.md` и RU mirror;
- `docs/advanced/limitations.md`, `docs/advanced/api-reference.md` и RU mirrors;
- `docs/specs/roadmap/todo_csv_streaming.md`;
- существующий `API-NAMING-REVIEW.md:59-60`;
- status file.

**Новые планируемые файлы:**

- `tests/nextorm.core.tests/CsvChunkedReadTests.cs`;
- `benchmarks/nextorm.benchmark/CsvBinaryChunkedBenchmark.cs`;
- evidence/brief/log artifacts в `artifacts/pdca/D167/r1/`;
- `scripts/validate_inner_loop.py` — только как результат P0, если tooling provisioning требует repository-local файл.

**Не меняем:** публичные сигнатуры, общую cache identity, глобальную политику SQLite locator, другие binary terminals, source generator, package versions без отдельного обоснования.

**Неопределённости footprint:**

1. Точное число внутренних callsites для нового preparation request.
2. Потребуется ли отдельный bounded-drain helper-файл.
3. Какие существующие provider-specific CSV-файлы потребуют новых tests.
4. Способ поставки нормативного validator.
5. Фактические sink/policy guards после S0.

Это uncertainty, не разрешение бесконтрольно расширять scope.

**Deferred:**

- Chunked guarantees для MySQL/MariaDB/ClickHouse: триггер — подтверждённая sequential/GetBytes capability и отдельные provider tests.
- Streaming transform API: триггер — отдельное согласованное требование; текущий `Func<object?,string?>` требует целого значения.
- Универсальная streaming-экранизация delimiter из Base64-алфавита: триггер — необходимость bounded-memory для такой конфигурации.

## 7. Режим единиц и collection handoff

**Последовательно в одном дереве:** P0/S0 → D1 → D2 → D3 → D4 → D5 → boundary sweep → CHECK.

Причина: D1/D2 пересекаются по reader contract; D2/D3 — по общей eligibility/policy семантике. Параллельная независимая реализация этих единиц создаёт конфликт контракта.

Отдельный worktree этим PLAN не запрашивается. Collection parent должен сериализовать задачи, пересекающиеся с указанным footprint, либо выделить изоляцию по своему разрешённому collection-режиму.

**Predecessor-result requirements:**

- Feature-predecessors: **нет**, задача независима от результатов других collection feature-задач.
- Обязательные входы: P0 validator, S0 report, неизменённые исходные API/CSV semantics.
- Перед DO lane сверяет HEAD, доступность этих входов и отсутствие конфликтующих изменений. Если predecessor или соседняя задача изменила preparation/CSV/locator contract — повторная оценка применимости PLAN обязательна.

## 8. Тест-стратегия

### Уровни

1. **Unit:** fake/probe reader, ограничение чтений и накопления, short reads, Base64 oracle, guards, transform, CT/resources.
2. **In-process SQLite:** реальные бинарные поля, SELECT semantics, обе поверхности и sync/async.
3. **Container-backed:** PostgreSQL, SQL Server, MySQL, ClickHouse и MariaDB CSV suites. SQLite integration также запускается.
4. **Boundary sweep:** весь core, SQLite/provider test projects, CSV+LOB integration.
5. **Coverage/perf:** отдельные обязательные запуски после стабилизации.

Будущая `nextorm.core.tests.CsvChunkedReadTests` — **планируемый**, пока не проверенный test symbol. После создания coder фиксирует реальные symbols через Roslyn и подтверждает непустую выборку.

### Red↔green regression

Добавить probe, который:

- обслуживает `GetBytes` из детерминированного генератора, без собственного целого BLOB;
- бросает при `GetFieldValue<byte[]>` и whole-value getters;
- считает offset, request size, returned size и ordinal accesses;
- может возвращать short reads;
- регистрирует максимальную ёмкость output buffering.

До продуктового исправления probe-test на прежнем byte[] path должен быть **RED именно из-за whole-field getter**. После исправления — GREEN с идентичным CSV и bounded counters.

Отдельный probe подтверждает fail-closed: ошибка после нескольких `GetBytes` не вызывает fallback getter.

### Матрица вариантов

| Варианты | Закрытие |
|---|---|
| NULL; non-null empty | Unit + SQLite/integration tests; нет transform для обоих случаев согласно старой семантике |
| Длины `1,2,3,4`; `B-2,B-1,B,B+1,B+2`; `2B+1`; `1 MiB`; `B=12288` | Unit exact-byte tests; большие поля также SQLite и поддерживаемые контейнеры |
| Short reads `1`, `2`, нерегулярные порции | Unit carry/offset tests |
| Два BLOB, scalar до/между/после BLOB | Unit ordinal probe + SQLite/PG/SQL Server |
| Chunked, buffered, неизвестный/non-sequential mode | Unit: chunked допускается только с provenance; unknown запрещён; явный buffered сохраняет legacy |
| PG / SQL Server / SQLite | Реальные chunked tests |
| MySQL / MariaDB / ClickHouse | Реальные buffered compatibility tests; chunking не заявляется |
| Transform absent / identity / иной string / `null` result | Unit + representative real-provider tests; transform — buffered guard |
| Direct storage byte[] / converter / typed mapping | Unit и существующий SQL Server mapping suite; несовместимое mapping — legacy guard |
| Standard delimiter / quoting / NULL marker / newline / header settings | Exact-byte tests по реальным существующим опциям |
| Delimiter пересекается с Base64-алфавитом | Buffered policy guard + exact-byte test |
| Обе public surfaces × sync/async × default/custom options × params | Tests; signatures/call forwarding дополнительно проверяются Roslyn |
| CT default / pre-cancel / cancel между chunks / provider error / sink error | Unit resource/cancellation tests |
| Destination open; flush success/failure; pooled buffers returned | Instrumented sink/pool tests |
| SQLite DISTINCT / JOIN / aggregate / projection без BLOB | In-process SQLite regression; locator не добавлен |
| `ToStream` / `ToTextReader` / `ToDataReader` | LOB regression: прежний locator и resource contract |
| `byte[]` reference / `null`; nullable/default options | Tests. Value-type binary field не существует в текущем контракте; другие value-type CSV колонки — existing regression tests |
| Расширение streaming на providers с capability=false | Deferred с указанным выше capability-триггером |

### Точные команды и селекторы

В cwd workspace; каждому запуску сохраняются stdout/stderr и отдельный exit-code.

```bash
dotnet test tests/nextorm.core.tests -c Debug \
  --filter "FullyQualifiedName~CsvChunkedReadTests"

dotnet test tests/nextorm.core.tests -c Debug \
  --filter "FullyQualifiedName~CsvStreamWriterTests"

dotnet test tests/nextorm.core.tests -c Debug \
  --filter "FullyQualifiedName~SelectExpressionTests"

dotnet test tests/nextorm.sqlite.tests -c Debug \
  --filter "FullyQualifiedName~CsvStreamTests"

dotnet test tests/nextorm.sqlserver.tests -c Debug \
  --filter "FullyQualifiedName~CsvTypedColumnMappingTests"

dotnet test tests/nextorm.integration.tests -c Debug \
  --filter "FullyQualifiedName~Csv|FullyQualifiedName~LobStreamingTests"
```

Последняя команда выполняется с:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock
```

Перед контейнерами загрузить `.opencode/skills/running-integration-tests/SKILL.md`. Если socket отсутствует, выполнить предусмотренный skill запуск Podman, дождаться готовности и повторить. PostgreSQL/SQL Server/MySQL/ClickHouse, сообщившие skipped, не закрывают provider evidence. MariaDB закрывается фактическим запуском её CSV tests согласно имеющейся fixture.

**Boundary sweep:**

```bash
dotnet build -c Debug
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
dotnet test tests/nextorm.sqlserver.tests -c Debug
dotnet test tests/nextorm.postgres.tests -c Debug
dotnet test tests/nextorm.mysql.tests -c Debug
dotnet test tests/nextorm.clickhouse.tests -c Debug
```

Дополнительно — приведённый контейнерный CSV+LOB selector. Непустая выборка обязательна; exit `0` без выбранных тестов недостаточен.

### Per-unit test scope JSON

Сохранять как `artifacts/pdca/D167/r1/D<номер>.brief.json`. Перед **каждой** соответствующей единицей:

```bash
python3 scripts/validate_inner_loop.py brief artifacts/pdca/D167/r1/D1.brief.json
```

Для остальных единиц — тот же точный вызов с соответствующим `D2`…`D5`. Требуется exit `0` до edits.

**D1**
```json
{"projects":["tests/nextorm.sqlite.tests","tests/nextorm.integration.tests"],"selectors":["FullyQualifiedName~CsvStreamTests","FullyQualifiedName~Csv|FullyQualifiedName~LobStreamingTests"],"files":["src/nextorm.core/DataContext.cs","src/nextorm.core/QueryCommandExtensions.cs","src/nextorm.core/QueryPlanner.cs","src/nextorm.core/SqlBuildContext.cs","src/nextorm.core/SqlBuilder.cs"],"rebuild":"affected","boundary":"After D3: full core/provider tests and container CSV+LOB sweep","rationale":"CSV reader-mode provenance and SQLite locator suppression; preserve sibling LOB terminals"}
```

**D2**
```json
{"projects":["tests/nextorm.core.tests"],"selectors":["FullyQualifiedName~CsvChunkedReadTests","FullyQualifiedName~CsvStreamWriterTests"],"files":["src/nextorm.core/Query/Csv/CsvStreamWriter.cs","src/nextorm.core/Query/Csv/CsvRowBuffer.cs","src/nextorm.core/Query/Csv/CsvValueFormatter.cs"],"rebuild":"affected","boundary":"After D3: full core/provider tests and container CSV+LOB sweep","rationale":"Chunked descriptors, carry, bounded sync/async output and preserved policies"}
```

**D3**
```json
{"projects":["tests/nextorm.core.tests","tests/nextorm.sqlite.tests","tests/nextorm.sqlserver.tests","tests/nextorm.integration.tests"],"selectors":["FullyQualifiedName~CsvChunkedReadTests","FullyQualifiedName~CsvStreamWriterTests","FullyQualifiedName~SelectExpressionTests","FullyQualifiedName~CsvStreamTests","FullyQualifiedName~CsvTypedColumnMappingTests","FullyQualifiedName~Csv|FullyQualifiedName~LobStreamingTests"],"files":["tests/nextorm.core.tests/CsvChunkedReadTests.cs","tests/nextorm.core.tests/CsvStreamWriterTests.cs","tests/nextorm.sqlite.tests/CsvStreamTests.cs","tests/nextorm.sqlserver.tests/CsvTypedColumnMappingTests.cs","tests/nextorm.integration.tests/CommonTestSuite.Csv.cs"],"rebuild":"affected","boundary":"Immediately after D3: full core/provider tests and container CSV+LOB sweep","rationale":"Exact-byte matrix, public terminal parity, provider coverage and red-green regression"}
```

**D4**
```json
{"projects":["benchmarks/nextorm.benchmark","tests/nextorm.core.tests"],"selectors":["FullyQualifiedName~CsvChunkedReadTests"],"files":["benchmarks/nextorm.benchmark/CsvBinaryChunkedBenchmark.cs"],"rebuild":"affected","boundary":"Before CHECK: final sweep, coverage, CSV and seven-case acceptance benchmarks","rationale":"Per-row runtime and allocation acceptance; benchmark harness does not replace correctness tests"}
```

**D5**
```json
{"projects":["tests/nextorm.core.tests"],"selectors":["FullyQualifiedName~CsvChunkedReadTests"],"files":["docs/guide/28-streaming-data.md","docs/ru/guide/28-streaming-data.md","docs/advanced/limitations.md","docs/ru/advanced/limitations.md","docs/advanced/api-reference.md","docs/ru/advanced/api-reference.md","docs/specs/roadmap/todo_csv_streaming.md"],"rebuild":"affected","boundary":"Before CHECK: final sweep and DocFX build","rationale":"Document scope and fallback; rebuild also covers any XML-doc changes"}
```

После S0 coder дополняет `files` реальными разрешёнными путями, включая фактический naming register и новые helpers, **до** validation/edit; не использует это для расширения требований.

### Coverage и mutation

- Нижние границы проекта: **line ≥85%, branch ≥75%**. Ветка не `main`; CI warning не заменяет проверку.
- Зафиксировать baseline и post-change line **и branch** coverage, отдельно changed core types/branches. Никакого необъяснённого branch regression.
- Команды:

```bash
dotnet tool run dotnet-coverage collect -s coverage.settings.xml \
  -f cobertura -o tests/coverage/coverage.cobertura.xml \
  "dotnet test --no-build --verbosity normal"

dotnet tool run reportgenerator \
  -reports:tests/coverage/coverage.cobertura.xml \
  -targetdir:tests/coverage/report \
  -reporttypes:"Html;TextSummary;Cobertura" \
  -riskhotspotassemblyfilters:"+nextorm.*"
```

Для collect применяется тот же `DOCKER_HOST`; skipped обязательных providers не принимаются. Coverage settings не охватывают MySQL/ClickHouse — их tests остаются обязательными отдельно.

**Stryker отсутствует, config отсутствует.** В данной ревизии не планируется неподтверждённая установка tooling. Evidence обязан явно сказать `mutation not run: tooling unavailable`, приложить inventory и перечислить mutation-непроверенные ветви:

- eligibility/mode/provider guard;
- typed mapping/converter/transform fallback;
- NULL/empty;
- short-read/carry/EOF/padding;
- bounded drain/quote policy;
- cancellation/error/finally;
- CSV locator suppression и сохранение LOB default.

Unit/branch tests этих ветвей обязательны, но не выдаются за mutation evidence. Если tooling появляется до CHECK, Stryker запускается scoped на изменённые core types; surviving mutants убиваются или получают индивидуальное обоснование.

## 9. Матрица приоритетов

**P1 по построению, CHECK не понижает:**

- REQ-01…REQ-11: инварианты issue и все обязательства класса «стриминговый терминал».
- REQ-12: per-row perf и обязательная cached-path acceptance.
- REQ-14: evidence completeness и fail-closed validator.
- REQ-13: обязательные EN/RU и register obligations проекта.

Все contract rows ниже относятся к этим требованиям и являются P1. Нельзя заменить provider evidence unit probe либо объявить отсутствующий лог N/A.

## 10. Документация

**Меняем:**

- `docs/guide/28-streaming-data.md:12,117,229,285` + RU mirror;
- `docs/advanced/limitations.md:53` + RU;
- `docs/advanced/api-reference.md:92` + RU;
- XML-doc CSV terminals/options там, где требуется описать memory/fallback/cancellation contract;
- roadmap §2(b): реализован для поддерживаемого direct binary sequential-path; ограничения перечислены явно;
- `API-NAMING-REVIEW.md:59-60`: обе поверхности и sync/async отмечены без public rename.

**Не трогаем:** нумерацию guide pages, generated `docs/api`/`docs/_site`, unrelated docs. Публичные страницы не получают links на `docs/specs/**`.

Проверка:

```bash
dotnet docfx docs/docfx.json
git diff --check
```

Сохранить CRLF во всех редактируемых файлах.

## 11. Перф-замер

**Нужен.** Это per-row CSV serialization (`CsvStreamWriter.cs:376-383`, `CsvRowBuffer.cs:265-284`), плюс изменяется preparation/sequential path. Аргумента «one-time, не измеряем» здесь нет.

### CSV baseline и критерии

Планируемый benchmark `CsvBinaryChunkedBenchmark`:

- paired legacy buffered writer и новый chunked writer в одном harness;
- legacy baseline использует сохранённую buffered ветвь и предварительно проверяется на эквивалентность старому CSV;
- детерминированный probe provider без заранее выделенного большого BLOB;
- destination, не сохраняющий весь результат;
- payloads: **64 B, 1 MiB, 16 MiB**; одна бинарная колонка; одинаковый вывод и encoding;
- MemoryDiagnoser; Mean/Allocated, runtime, machine/SDK, параметры и исходная ревизия в отчёте.

Команда:

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --filter '*CsvBinaryChunkedBenchmark*' --job short
```

Приёмка:

- для 1/16 MiB chunked `Allocated` ≤25% paired legacy;
- прирост chunked allocation между 1 и 16 MiB ≤128 KiB на операцию;
- Mean не хуже legacy более чем на 20% для каждого payload;
- probe дополнительно подтверждает фиксированные read/output bounds.

Если доверительные интервалы не позволяют решить 20% criterion, выполнить один дополнительный более длинный запуск; не объявлять шум PASS.

### Обязательный cached-path acceptance

```bash
dotnet run --project benchmarks/nextorm.benchmark -c Release -- \
  --anyCategories=acceptance
```

Ровно **7 cases**, без failures; сохранить wall/Mean/Allocated и cached/prepared ratios относительно `docs/specs/performance/acceptance-benchmarks.md`. Измерение обязательно из-за затронутого preparation path, даже при отсутствии намеренного cache change.

## 12. Риски, blast radius и rollback

- **Sequential ordinal regression:** provider mapping может читать не по порядку. Защита — S0, descriptor guards и ordinal probe.
- **SQLite SQL regression:** suppress-locator должен иметь CSV-only scope. Защита — DISTINCT/JOIN/aggregate и sibling LOB tests.
- **Псевдостриминг:** whole Base64 остаётся в row-buffer. Защита — high-water counters и allocation criterion.
- **Async blocking:** `GetBytes` синхронен; документировать гранулярность CT, не обещать прерывание provider-вызова.
- **Policy compatibility:** нестандартный delimiter или transform требует buffered guard; область гарантии должна быть видна в docs.
- **Tooling/runtime:** validator отсутствует, Stryker отсутствует, контейнеры требуют runtime. Ни один из этих фактов не маскируется зелёным общим статусом.
- **Collection collision:** соседняя задача может затронуть preparation/CSV. Parent проверяет footprint до DO.

Blast radius: core CSV и локальная подготовка reader/SQL locator; providers затрагиваются поведением вызова, не public API.

Rollback: убрать единым patch descriptor/chunked dispatch и CSV-local preparation/suppression изменения; вернуть прежний buffered CSV. Не оставлять sequential reader с восстановленным старым ordinal-поведением или глобальным locator change. Commits/merge/push этим PLAN не разрешаются.

## 13. Design checklist

| Проверка | Решение |
|---|---|
| Public API / naming | Сигнатуры не меняются; async twins сохраняются |
| Общий command/cache | Только per-call options; `storeInCache:false`; без sticky mutation |
| Reader provenance | Явно передаётся применённый mode |
| SQLite locator | CSV-only suppression; siblings сохраняют default |
| Memory bound | Ограничены input, Base64 output и binary row accumulation |
| Transform/mapping | Существующий приоритет; explicit buffered guards |
| Sync/async/resources | Отдельный dispatch; CT между chunks; finally; destination ownership сохранено |
| Variants/provider coverage | Матрица выше; обязательные контейнерные runs |
| Docs/perf/evidence | Обязательные задачи и contract rows ниже |
| Tooling gate | Fail-closed P0; отсутствующий validator не обходится |

**Design checklist passed как проектное решение.** Реализация, тестовые symbols и фактическое закрытие evidence ещё не подтверждены.

## 14. Версионированный evidence contract — rv=1

### Общие правила и точные invocation labels

Каждая строка ниже имеет стабильные requirement ID и row ID. Все источники в этой таблице пока **планируемые**.

Артефакты строк: `artifacts/pdca/D167/r1/<row-id>/`. Каждый command log содержит argv, cwd, HEAD, UTC timestamps, stdout/stderr, exit-code; test report — selected/passed/failed/skipped counts. Pipeline logging не должен терять исходный exit-code.

Следующие labels означают **ровно указанные вызовы**, а не произвольную замену:

| Label | Точный вызов |
|---|---|
| V-BRIEF | `python3 scripts/validate_inner_loop.py brief artifacts/pdca/D167/r1/D1.brief.json` и аналогично для D2…D5 |
| V-REPORT | `python3 scripts/validate_inner_loop.py report artifacts/pdca/D167/r1/evidence.json` |
| CORE-CHUNK | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~CsvChunkedReadTests"` |
| CORE-CSV | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~CsvStreamWriterTests"` |
| CORE-SELECT | `dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~SelectExpressionTests"` |
| SQLITE-CSV | `dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~CsvStreamTests"` |
| SQLSERVER-MAP | `dotnet test tests/nextorm.sqlserver.tests -c Debug --filter "FullyQualifiedName~CsvTypedColumnMappingTests"` |
| PROVIDER-CSV-LOB | `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock dotnet test tests/nextorm.integration.tests -c Debug --filter "FullyQualifiedName~Csv|FullyQualifiedName~LobStreamingTests"` |
| BUILD | `dotnet build -c Debug` |
| CSV-PERF | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter '*CsvBinaryChunkedBenchmark*' --job short` |
| ACCEPT-PERF | `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance` |
| DOCS | `dotnet docfx docs/docfx.json` |
| DIFF | `git diff --check` |
| MUTATION-INVENTORY | `dotnet tool list --local` |

Coverage commands и boundary-sweep commands — ровно команды раздела 8. Дополнительный повтор CSV-PERF допускается только по описанному noise criterion и фиксируется отдельно.

### Строки контракта

Во всех строках `rv=1`, priority **P1**.

| Row ID / requirement | Требуемая проверка | Evidence kinds и источники | Команда / результат / лог | Ожидаемые артефакты | Владелец | Наблюдаемый predicate |
|---|---|---|---|---|---|---|
| EV-01 / REQ-14 | Validator предоставлен; briefs прошли до edits; report прошёл до CHECK | Tooling provenance; validator validation log; timestamps | V-BRIEF для D1…D5, V-REPORT: exit 0; malformed fixtures — nonzero; отсутствие файла — blocked | `EV-01/validator-provenance.md`, `brief-validation.log`, `report-validation.log`; все briefs, `evidence.json` | tooling-owner / coder, затем check | **Всегда** |
| EV-02 / REQ-01 | Полная size/NULL/empty byte matrix | Test results; verified symbols; byte comparisons | CORE-CHUNK + CORE-CSV: exit 0, непустой selection; все size cases перечислены | `EV-02/tests.log`, `case-map.json` | D2/D3 coder | **Всегда** |
| EV-03 / REQ-02 | Whole materialisation отсутствует на chunked-path; red↔green | RED/GREEN logs; getter/read/high-water counters; source references | CORE-CHUNK: прежний продукт с regression test — expected failing test; исправленный — exit 0; failure cause whole getter | `EV-03/red.log`, `green.log`, `counters.json` | D2/D3 coder | **Всегда** |
| EV-04 / REQ-03 | Short reads, carry, offset, EOF, padding | Tests + byte oracle | CORE-CHUNK: exit 0; short-read cases явно названы | `EV-04/tests.log`, `case-map.json` | D2/D3 coder | **Всегда** |
| EV-05 / REQ-04 | Fail-closed chunked admission; no late fallback; explicit buffered mode | Guard tests; invocation counters; verified source | CORE-CHUNK: exit 0; unknown mode rejected; mid-read error не вызывает getter | `EV-05/tests.log`, `guard-map.md` | D1/D2/D3 coder | **Всегда** |
| EV-06 / REQ-05 | Transform и typed/converter mapping сохранены | Tests, transform invocation data, mapping review | CORE-CHUNK + CORE-CSV + SQLSERVER-MAP + CORE-SELECT: exit 0 | `EV-06/tests.log`, `mapping-review.md` | D2/D3 coder | **Всегда** |
| EV-07 / REQ-06 | Обе публичные поверхности | Tests; Roslyn definitions/call forwarding | CORE-CHUNK + SQLITE-CSV: exit 0; verified Roslyn invocation для `NextORM.Core.QueryCommandExtensions` и `NextORM.Core.EntityBuilderExtensions` | `EV-07/tests.log`, `surface-review.md` | D3 coder / scout | **Всегда** |
| EV-08 / REQ-06 | Sync **и** async | Tests с одинаковым expected CSV | CORE-CHUNK + PROVIDER-CSV-LOB: exit 0; оба пути отражены в case-map | `EV-08/tests.log`, `case-map.json` | D3 coder | **Всегда** |
| EV-09 / REQ-07 | Async CancellationToken | Cancellation tests; read/write counters; cleanup | CORE-CHUNK: exit 0; default/pre-cancel/mid-chunk cases | `EV-09/tests.log`, `cancellation-counters.json` | D2/D3 coder | **Всегда** |
| EV-10 / REQ-06 | `params` parity и defaults с siblings | Forwarding tests + Roslyn member/signature review | CORE-CHUNK + SQLITE-CSV: exit 0; Roslyn `members`/`refs` вызовы сохраняются в ledger | `EV-10/tests.log`, `params-review.md` | D3 coder / scout | **Всегда** |
| EV-11 / REQ-08 | Ownership, flush, finally/pool cleanup | Instrumented sink/pool tests | CORE-CHUNK: exit 0; success/cancel/provider-error/sink-error | `EV-11/tests.log`, `resource-counters.json` | D2/D3 coder | **Всегда** |
| EV-12 / REQ-09 | Call-local options, no cache/shared mutation | Tests; Roslyn refs/source review; preparation trace | CORE-CHUNK + BUILD: exit 0; reviewed preparation invocations показывают `storeInCache:false` | `EV-12/tests.log`, `preparation-review.md` | D1 coder / scout | **Всегда** |
| EV-13 / REQ-10 | SQLite no CSV locator; sibling LOB не сломан | SQL/result tests; real reader evidence | SQLITE-CSV + PROVIDER-CSV-LOB: exit 0; DISTINCT/JOIN/aggregate и LOB cases | `EV-13/tests.log`, `sql-case-map.json` | D1/D3 coder | **Всегда** |
| EV-14 / REQ-11 | Provider coverage и честный fallback | Реальные integration results с разбивкой по provider/mode | PROVIDER-CSV-LOB: exit 0; PG/SQL Server/MySQL/ClickHouse обязательные cases не skipped; MariaDB и SQLite evidence присутствует | `EV-14/tests.log`, `provider-matrix.json` | D3 integration stream | **Всегда** |
| EV-15 / REQ-12 | CSV runtime/allocation acceptance | BenchmarkDotNet raw/report; paired baseline; machine/runtime | CSV-PERF: exit 0, все payloads, пороги раздела 11 выполнены | `EV-15/run.log`, raw BDN exports, `comparison.md` | D4 perf stream | **Всегда** |
| EV-16 / REQ-12 | Cached-path acceptance | BDN results, wall/Mean/Allocated, reference ratios | ACCEPT-PERF: exit 0, **ровно 7 cases**, без failures; нормативные ratios соблюдены | `EV-16/run.log`, raw exports, `acceptance-comparison.md` | D4 perf stream | **Всегда** |
| EV-17 / REQ-14 | Coverage, branch delta и boundary sweep | Baseline/post coverage; changed-type branch map; suite results | Команды coverage и boundary sweep из §8: exit 0; line≥85, branch≥75; branch regression объяснён/исправлен | `EV-17/baseline/`, `post/`, `branch-delta.md`, `sweep.log`; `tests/coverage/report/` | D4 evidence stream | **Всегда** |
| EV-18 / REQ-14 | Mutation strategy честно закрыта | Tool inventory; unavailable reason и untested branch list; либо Stryker results | MUTATION-INVENTORY: exit 0. При отсутствии Stryker — явное `not run`, не PASS mutation; при появлении — scoped run и survivor dispositions | `EV-18/tool-inventory.log`, `mutation-status.md`, при наличии — Stryker report | D4 evidence stream | **Всегда**; только Stryker-run predicate — инструмент реально доступен |
| EV-19 / REQ-13 | EN/RU/XML-doc и roadmap | Diff review; DocFX log; source references | DOCS + DIFF: exit 0; review перечисленных EN/RU страниц и §2(b) | `EV-19/docfx.log`, `diff-check.log`, `docs-review.md` | D5 docs stream | **Всегда** |
| EV-20 / REQ-13 | Naming register и terminal class obligations | Register diff; signature review | DIFF: exit 0; read-only review существующего `API-NAMING-REVIEW.md:59-60` и CSV declarations | `EV-20/register-review.md`, `diff-check.log` | D5 docs stream / scout | **Всегда** |
| EV-21 / REQ-14 | S0 и collection applicability | Roslyn/source report; HEAD/footprint check; prerequisite availability | Read-only Roslyn invocations записаны фактически; `git rev-parse HEAD`, `git status --short`: exit 0; вывод сопоставлен с базой | `EV-21/scout.md`, `applicability.log`, `dependencies.md` | scout / collection lane | **Всегда перед DO** |

Roslyn symbol names, которые не подтверждены supplied pack полностью, scout сначала разрешает через `structure/types/members`; ledger фиксирует **реальные** subsequent invocations. Таблица не утверждает существование будущих test symbols или новых `file:line`.

### Ledger и CHECK re-gather

- Coder записывает actual evidence по каждому `EV-*` и `rv=1`: команды, exit-code, log/artifact paths, verified symbols/references, результат.
- Failed/not-run/missing/blocked фиксируются явно.
- N/A допускается только для условного подпункта с доказанным predicate; unconditional rows не снимаются.
- **CHECK re-gather budget: максимум 2 целевых dispatches на весь CHECK этой ревизии; владелец — `check`.**
  1. Добор уже произведённых логов/артефактов/символов.
  2. Повтор одного bounded evidence-producing запуска без продуктовых edits, если необходим.
- Исчерпание бюджета не превращает missing report в product defect и не разрешает PASS. Статус evidence-incomplete; оркестратор маршрутизирует дальнейшее решение.
- Новая required variant требует обоснованного CHECK→PLAN. При ревизии явно supersede `rv=1`, сохранить `EV-01…EV-21` и обязательства, добавить новые ID.
- Сейчас прежней ревизии контракта нет; supersession не применяется.

## 15. Уверенность и открытые риски

**Высокая уверенность:** нынешний whole-array путь, transform semantics, buffered reader preparation, local non-cached preparation возможность, SQLite locator hazard, отсутствие validator/Stryker и обязательные terminal obligations подтверждены входным evidence.

**Средняя уверенность:** точный sink/drain footprint и mapping/policy eligibility; закрываются S0, а не догадками.

**Открытые условия до DO:** P0 validator, S0 findings, integration runtime, отсутствие footprint-конфликта. Они не ослабляют критерии приёмки.

Для текущего PLAN нет отчёта DO и нет оснований объявлять реализацию завершённой. При возврате DO→PLAN сначала классифицировать новый факт: предусловие / допустимый риск / доказанный внешний blocker / недостаток evidence. Один отсутствующий отчёт не создаёт новую revision и не сбрасывает attempts.

## Итог для collection parent

Цель: убрать whole-field материализацию бинарного CSV на подтверждённом chunked-path, сохранив CSV/API semantics.
Выбранный дизайн: binary descriptor, bounded sync/async drain, локальный sequential reader и CSV-only подавление SQLite locator; explicit buffered guards.
Footprint: core CSV/preparation/SQL context, CSV/LOB tests, один benchmark, EN/RU/XML-doc/roadmap/register и evidence artifacts.
Тесты/перф: red↔green probe, size/policy/provider matrix, реальные контейнеры, branch delta, CSV baseline и обязательные 7 acceptance cases.
Открытые риски: P0 validator отсутствует; S0 уточняет sink/mapping guards; Stryker unavailable; DO остаётся закрыт до prerequisites и lane applicability check.
---

## DO progress log (r1, cycle N=1, attempt n=2)

- 2026-10-09T08:15:00Z | DO | revision r1 | iteration 1/3 | DO started (collection lane G1 resume; HEAD fba54f17; no worktree/branch — single group) | artifacts/pdca/D167/r1/
- 2026-10-09T08:15:16Z | DO | revision r1 | iteration 1/3 | P0 closed: scripts/validate_inner_loop.py present in-repo (504 lines, brief+report subcommands); brief validation exit codes: D1..D5 = 0 0 0 0 2 | artifacts/pdca/D167/r1/D1.brief.json .. D5.brief.json
- BLOCKER (D5): brief gate failed — `FAIL: fictitious rebuild for docs-only scope: no compiled file in scope.files but rebuild == affected`. Plan §8 D5 declares `rebuild:"affected"` with docs-only files; validator rule rejects it. No workaround applied; DO blocked pending plan/brief reconciliation.
- 2026-10-09T03:25:40Z | DO | revision r1 | iteration 1/3 | D1 done (reader-mode provenance + SQLite suppress-locator); D2 done; evidence D1 logs artifacts/pdca/D167/r1/D1/ (build.log, core-csvwriter.log, sqlite-csvstream.log, sqlite-lob.log), D2 logs artifacts/pdca/D167/r1/D2/ (build.log, core-csvwriter.log, core-select.log); D2 introduced helper src/nextorm.core/Query/Csv/CsvBinaryFieldWriter.cs (outside original D2 file list); CsvRowBuffer.cs and CsvValueFormatter.cs left unchanged | artifacts/pdca/D167/r1/D1/ artifacts/pdca/D167/r1/D2/
- 2026-10-09T03:29:52Z | DO | revision r1 | iteration 1/3 | D3 done: NEW tests/nextorm.core.tests/CsvChunkedReadTests.cs (access via InternalsVisibleTo nextorm.core.tests), 47 tests green; red-injection (force buffered path) exit 2, 26 failed on whole-field getter, then restored byte-identical (sha256 + git diff unchanged) and green; dotnet build nextorm.slnx 0 warnings/0 errors; CORE-CHUNK 47, CORE-CSV 116, CORE-SELECT 23 all exit 0; validate_inner_loop.py report exit 0; D2.brief.json amended to add src/nextorm.core/Query/Csv/CsvBinaryFieldWriter.cs; no real product defect found | artifacts/pdca/D167/r1/D3/ (evidence.json, red.log, green.log, red-green.md, core-chunk.log, core-csv.log, core-select.log, build.log)
- 2026-10-09T08:48:30Z | DO | revision r1 | iteration 1/3 | D3/D3b closed: PARAMETER-ELIGIBILITY DEFECT FIX (chunked binary path now admits only direct stored byte[] columns via IsDirectStoredColumn/IsDirectByteArrayRead; bound-parameter/expression/function ordinals keep the buffered path, so GetBytes is never called on a parameter ordinal) — this removed the SQLite native reader crash (exit 139, int-sqlite-crash-before-fix.log); core CsvChunkedReadTests now 48 green; SELECTOR AMENDMENT (validated before run): plan selector `FullyQualifiedName~Csv|FullyQualifiedName~LobStreamingTests` replaced by the semantically-equivalent union of two single-filter invocations `FullyQualifiedName~Csv` + `FullyQualifiedName~Lob` because (a) validator compound-token hygiene rejects the literal `|` and (b) `LobStreamingTests` matches no class; PROVIDER-CSV 69 selected/69 passed/0 failed/0 skipped exit 0; PROVIDER-LOB 311 selected/222 passed/0 failed/89 skipped exit 0 (all 89 skips provider-inapplicable capability/polarity or opt-in probes, none environmental/container-missing); report validator exit 0 | artifacts/pdca/D167/r1/D3b/ (evidence.json, skips.md, int-csv-filter.log, int-lob-filter.log, int-csv-list.log, int-lob-list.log)
2026-10-09T04:00:53Z | DO | revision r1 | iteration 1/3 | D4 done: NEW benchmarks/nextorm.benchmark/CsvBinaryChunkedBenchmark.cs (paired legacy-buffered vs chunked; deterministic GetBytes probe owning no BLOB; counting sink; 64 B / 1 MiB / 16 MiB). CSV-PERF `--filter '*CsvBinaryChunkedBenchmark*' --job short` exit 0, 12 executed: chunked Alloc 74 B vs legacy 1,049,570 B @1 MiB and 220 B vs 16,777,694 B @16 MiB (growth +146 B <= 128 KiB); Mean via mandated longer `--job medium` run: 64 B +8.7%, 1 MiB/16 MiB faster (ShortRun in-process 64 B +26.3% had CI margin 89.9% of Mean, superseded). ACCEPT-PERF exit 0, exactly 7 cases / 0 failures, wall 52 s (<=4 min), cached/prepared time ratio 2.17 vs baseline 1.87 (+16.1%), alloc ratio 7.67 vs 7.42 (+3.4%). validate_inner_loop.py report exit 0; build 0 warnings/0 errors; benchmark artifacts restored. | artifacts/pdca/D167/r1/D4/ (csvbench.log, csvbench-long.log, acceptance.log, evidence.json, bench-build.log, environment.txt)
2026-10-09T04:05:30Z | DO | revision r1 | iteration 1/3 | D4c coverage + mutation-disclosure evidence done: BUILD `dotnet build nextorm.slnx -c Debug` exit 0 (0 warnings/0 errors); COVERAGE-COLLECT exit 0 with DOCKER_HOST (9751 total / 9553 passed / 198 skipped / 0 failed); REPORTGENERATOR exit 0; aggregate line 88.37% (50528/57177), branch 80.34% (27118/33753) — both >=85/>=75; changed-type line/branch %: CsvStreamWriter 98.9/91.4, CsvBinaryFieldWriter 92.4/81.6, DataContext 89.8/84.2, QueryCommandExtensions 88.5/87.5, QueryPlanner 97.1/89.7, SqlBuilder 92.0/83.4, SqlBuildContext 100.0/100.0; no environmental/container provider skips (all 198 are capability/polarity/opt-in); MUTATION NOT RUN: tooling unavailable — `dotnet tool list --local` has no Stryker and `dotnet tool run stryker --version` exit 1; 7 mutation-unchecked branch areas enumerated (M1 eligibility/mode/provider guard, M2 typed mapping/converter/transform fallback, M3 NULL/empty, M4 short-read/carry/EOF/padding, M5 bounded drain/quote policy, M6 cancellation/error/finally, M7 CSV locator suppression vs LOB default); report validator `python3 scripts/validate_inner_loop.py report artifacts/pdca/D167/r1/D4c/evidence.json` exit 0 | artifacts/pdca/D167/r1/D4c/ (evidence.json, build.log, coverage-collect.log, reportgenerator.log, tool-inventory.log, stryker-attempt.log)
2026-10-09T04:12:07Z | DO | revision r1 | iteration 1/3 | D5 docs done: D5.brief.json fixed to rebuild='none' (markdown-only scope, no compiled .cs) and revalidated (brief validator exit 0); EN+RU updates in docs/guide/28-streaming-data.md, docs/advanced/limitations.md, docs/advanced/api-reference.md and RU mirrors state the precise bounded-memory guarantee (direct stored byte[] columns on confirmed-sequential providers PostgreSQL/SQL Server/SQLite; MySQL/MariaDB/ClickHouse and parameter/function/transform/converter/incompatible-delimiter cases stay buffered; async CT checked between GetBytes reads; guarantee covers CSV implementation only, not driver/destination); roadmap §2(b) marked partially addressed with explicit limitations (plan file kept), API-NAMING-REVIEW.md notes both CSV surfaces + sync/async preserved with no public rename; no public page links to docs/specs; guide numbering unchanged; Debug+Release builds 0 warnings/0 errors; docfx exit 0 (2 pre-existing source-generator warnings); git diff --check exit 0 (CRLF preserved); report validator exit 0 | artifacts/pdca/D167/r1/D5/ (build-debug.log, build-release.log, docfx.log, diff-check.log, evidence.json)
2026-10-09T04:26:13Z | DO | revision r1 | iteration 1/3 | D3c loop-back fixes applied+verified: FIX1 Excel first-byte latch (red: buggy post-carry order exit 2, 5/6 ExcelMode cases fail at index 0; exact sha256 restore eceace6a; green 6/6 exit 0), FIX2 IsDirectStoredColumn requires PhysicalColumnName + mapped non-computed metadata (guard probes CsvBinaryGuardTests/CsvTypedColumnMappingTests now carry PhysicalColumnName="data"; CS0136 pattern-var collision fixed by rename memberProperty), FIX3 sync CT checked between GetBytes reads, FIX4 exception-safe rent + clearArray on input; NEW REQ-10 SQLite tests CsvLocatorSuppression_KeepsDistinctJoinAggregateAndPlainSqlUnchanged (DISTINCT/JOIN/aggregate/plain no rowid, byte-equal to sequentialAccess:false render) + LOB sibling locator guard kept; BUILD 0 warnings/0 errors; CORE-CHUNK 56, CORE-CSV 116, SQLITE-CSV 28, SQLSERVER-MAP 11, POSTGRES-GUARD 1 all exit 0; container SQLite Csv_InvariantTypedColumns_ShouldWriteExactBytes 1/1 exit 0; report validator exit 0 | artifacts/pdca/D167/r1/D3c/ (evidence.json, build.log, core-chunk.log, core-csvwriter.log, sqlite-csv.log, sqlserver-map.log, postgres-guard.log, fix1-red.log, fix1-green.log, integration-csv-typed.log)
2026-10-09T09:47:57Z | DO | revision r1 | iteration 1/3 | D4d final-boundary refresh on post-FIX1-FIX4 tree: D4/D4c coverage+perf evidence SUPERSEDED (production CsvBinaryFieldWriter.cs/CsvStreamWriter.cs changed); Debug+Release builds 0W/0E; COVERAGE 9760 total/9562 passed/198 skipped/0 failed, aggregate line 88.37%/branch 80.34%, all changed types above branch min (CsvStreamWriter 98.9/89.9, CsvBinaryFieldWriter 92.7/84.6, DataContext 89.8/84.2, QueryCommandExtensions 88.5/87.5, QueryPlanner 97.1/89.7, SqlBuilder 92.0/83.4, SqlBuildContext 100/100), 0 environmental/container skips; CSV PERF A1 PASS / A2 PASS / A3 FAIL — 64 B chunked mean +134.4% (MediumRun tight CI 417.2+/-10.54 ns vs legacy 178.0+/-5.15 ns, ratio 2.35) and +125.4% InProcess, 1/16 MiB faster; root cause: per-op fixed 12 KiB input ArrayPool rent + clearArray:true in CsvBinaryFieldWriter.cs (lines 178/187/274) after FIX1-FIX4; ACCEPT-PERF 7 cases/0 failures, wall 61.1 s, cached/prepared time ratio 2.51 vs baseline 1.87 (+34.2%, ShortRun Error>Mean — flagged, unresolved); validator report exit 0; BenchmarkDotNet.Artifacts and benchmarks/BenchmarkDotNet.Artifacts restored identical | artifacts/pdca/D167/r1/D4d/
2026-10-09T10:09:52Z | DO | revision r1 | iteration 1/3 | D4e A3 fix + final-tree boundary refresh: causation CONFIRMED — toggling CsvBinaryFieldWriter input-buffer ArrayPool Return clearArray true->false alone moved the 64 B chunked Mean 828.2->420.3 ns Default (ratio 2.24->1.16) and 883.6->392.6 ns InProcess (1.83->1.01), so the fixed 12 KiB clear dominated the small payload; FIX applied at src/nextorm.core/Query/Csv/CsvBinaryFieldWriter.cs (both input returns now clearArray:false, with a trade-off comment; FIX4 exception-safe rent and FIX1/2/3 intact; buffer-reuse step 3 NOT needed); CSV PERF A1 PASS / A2 PASS / A3 PASS — MediumRun tight CI 64 B chunked 388.609 +/- 4.218 ns vs legacy 330.671 +/- 2.887 ns = +17.5% (<=20%), 1 MiB -54.6% and 16 MiB -57.4% faster, chunked alloc 64 B vs 1,048,716 / 16,777,240 B; FIX1 ExcelMode guard latch green (CsvChunkedReadTests 56/56, CsvStreamWriterTests 116/116); ACCEPT-PERF exactly 7 cases / 0 failures wall 59 s, cached/prepared ShortRun 2.40 (+28.1%, unstable Error 3.699 ms) resolved by ONE longer MediumRun pass to 1.98 (+5.8% vs baseline 1.87; alloc +3.4%) = noise not regression; Debug + Release builds 0 warnings / 0 errors; validator `python3 scripts/validate_inner_loop.py report artifacts/pdca/D167/r1/D4e/evidence.json` exit 0; BenchmarkDotNet.Artifacts and benchmarks/BenchmarkDotNet.Artifacts restored identical | artifacts/pdca/D167/r1/D4e/
2026-10-09T10:16:21Z | DO | revision r1 | iteration 1/3 | D5freeze authoritative FINAL FROZEN BOUNDARY (HEAD fba54f17; D4e clearArray:false at CsvBinaryFieldWriter.cs:188/279): Debug+Release builds 0 warnings/0 errors (25 projects each); COVERAGE 9760 total/9562 passed/198 skipped/0 failed, 12/12 assemblies passed; aggregate line 88.37% (50558/57211) / branch 80.34% (27125/33761), both >=85/>=75; changed types CsvStreamWriter 98.93/89.89, CsvBinaryFieldWriter 92.70/84.62 (chunk reader 89.06/83.33), DataContext 89.76/84.21, QueryCommandExtensions 88.52/87.50, QueryPlanner 97.08/89.67, SqlBuilder 91.99/83.39, SqlBuildContext 100/100; per-provider integration CSV 69/69 passed/0 skipped (ClickHouse 2, MariaDB 2, MySQL 16, PostgreSQL 16, SQL Server 16, SQL Server-specific 1, SQLite 16); 0 environmental skips of 198 (all provider capability/polarity or opt-in probes); supersedes D4d coverage; validator `python3 scripts/validate_inner_loop.py report artifacts/pdca/D167/r1/D5freeze/evidence.json` exit 0; NO code changes by this unit | artifacts/pdca/D167/r1/D5freeze/
2026-10-09T10:28:40Z | DO | revision r1 | iteration 2/3 | DO loop-back n=2 (CHECK->DO): closed the mandatory REQ-06 public-surface/params gap with new SQLite test WriteCsv_EntityBuilderAndQueryCommandSurfaces_SyncAsync_DefaultAndCustomOptions_ByteIdentical (tests/nextorm.sqlite.tests/CsvStreamTests.cs:693-750; both surfaces x sync/async x default/custom options x positional params, byte-identical over a real direct-stored 1 MiB+1 BLOB) and re-ran the final NONEMPTY sibling ~Lob suite; DEFECT HISTORY: key REQ-06-surface-params observed r1/n1 (CHECK), 1 fix applied r1/n2, evidence artifacts/pdca/D167/r1/D6/; no production change; CHECK-pack wrote artifacts/pdca/D167/r1/CHECK-ledger.md and artifacts/pdca/D167/r1/audit.md | artifacts/pdca/D167/r1/D6/ (build.log 0W/0E, sqlite-csvstream.log 30/30/0/0, core-chunk.log 56/56/0/0, int-lob-filter.log 311/222/0/89, report-validation.log validator exit 0)
- 2026-10-09T05:36:46Z | ACT | revision r1 | iteration 2/3 | CHECK PASS r1/N=1/rv=1; task status done; evidence root artifacts/pdca/D167/r1/; defect history recorded (parameter-crash P1, Excel-guard Critical, eligibility hardening, sync CT, clearArray/A3 resolution, REQ-06 surface/params); pending commit + #167 close | artifacts/pdca/D167/r1/CHECK-ledger.md
