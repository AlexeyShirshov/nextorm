# Cycle status: issue-26 / cycle 1

## Current state

- task: `issue-26` · form: `не назначена` · specialized skill: `none`
- mode: `autonomous`
- cycle: `N=1` · revision: `r=1` · attempt: `n=0` (`0/3`; DO не начат)
- scope: GitHub nextorm#26, тот же входной PLAN; объём DO не назначен · status path: `/home/alex/sources/nextorm/.pdca/status/issue-26-1.md`
- goal: поддержка двух форм числовых структур в общем SQL-материализаторе без выделения объекта результата на каждую строку, с сохранением API и семантики терминалов
- phase: `ACT` · record: `completed` · outcome: `blocked / STOP` (терминальный PLAN; продукт не принят)
- source: исходный запрос и уточнение пользователя в §Original task · context refs: §PLAN record `r1`, источник issue и `pdca-scout` там же; evidence root: `/home/alex/sources/nextorm/.pdca/issue-26/rv1/`
- criteria: отдельно не утверждены; ориентиры issue и терминальное решение — в §PLAN record `r1`

## Original task

```text
pdca по задаче 26 автономно автокомит
```

Точное уточнение пользователя в продолжение того же входного PLAN: `гитхаб`. Отдельных критериев пользователь не предоставил. Источник: диспетчерский бриф, исходная задача и явное уточнение.

## PLAN record

### r1

#### PLAN-r1-initial

Источник: `pdca-planner`, полный первоначальный отчёт из диспетчерского брифа, до уточнения; не является действующим решением.

**PLAN — терминальный исход: `acceptance criteria cannot be determined`.** Переход напрямую в ACT для фиксации блокера, без DO, CHECK и автокоммита.

**Исходная задача:** `pdca по задаче 26 автономно автокомит`.

**Запрошенный и передаваемый результат** — выполненная именно «задача 26». Её постановка не идентифицирована, поэтому результат не передаётся. Пользовательских критериев нет; вывести критерии из одного номера нельзя. **Форма, специализированный скилл и входной объём не определены:** пока неясно, какую задачу имел в виду пользователь, нельзя установить границы работы и даже то, требуется ли менять код.

**Основание.** По отчёту scout, рабочее дерево `/home/alex/sources/nextorm` на ветке `1.0.11` чистое. Локальный статус `T026` или вход коллекции для задачи 26 не найден. [GitHub #26](https://github.com/AlexeyShirshov/nextorm/issues/26) совпадает по номеру, но локальные ссылки не связывают его с запросом: `.pdca/status/issues-65-69.md:75,705` содержит исторические упоминания, а `docs/specs/design/API-NAMING-REVIEW.md:3638` — другой нумерованный пункт. У самого issue не удалось сверить статус спецификации: формулировки о её согласовании противоречат друг другу, а указанный файл `docs/specs/design/issue-26-struct-streaming-results.md` отсутствует в текущем checkout. В описании issue отдельно сказано, что реализация не авторизована.

**Ограничения:** автономный режим; автокоммит разрешён только для принятой задачи в указанном репозитории; без push и без затрагивания чужих изменений. Приравнять «задачу 26» к GitHub #26 означало бы риск выполнить и закоммитить не ту работу.

**Вопрос пользователю через драйвер:** под «задачей 26» имеется в виду GitHub #26 или другая запись? Если GitHub #26, уточните, какая постановка актуальна и разрешён ли переход к реализации. Если другая запись — укажите точную ссылку или путь к постановке.

#### PLAN-r1-controlling

Источник: `pdca-planner`, полный действующий отчёт из диспетчерского брифа, после уточнения; тот же входной PLAN и та же ревизия.

**Продолжение того же входного PLAN — `original task is infeasible under the stated constraints`.** Переход `PLAN → ACT`: зафиксировать блокер. Без DO, CHECK и автокоммита.

**Исходная задача:** `pdca по задаче 26 автономно автокомит`. Уточнение `гитхаб` однозначно указывает на [nextorm#26](https://github.com/AlexeyShirshov/nextorm/issues/26).

**Запрошенный и передаваемый результат** — реализованная поддержка двух указанных в #26 форм числовых структур в общем SQL-материализаторе, без выделения объекта результата в куче на каждую строку, с сохранением публичного API и семантики терминальных операций. Результат определён и поддаётся оценке: issue задаёт, в частности, 100 из 100 случаев SQLite без пропусков и не обещает нулевых суммарных аллокаций или ускорения. В текущем запуске результат не передаётся.

**Блокер — отдельное одобрение плана реализации.** В `Design draft` и в заключительном абзаце issue сказано, что одобрение письменной спецификации не заменяет одобрения плана реализации: план не подготовлен и не одобрен, а реализация и тесты до этого не разрешены. Уточнение `гитхаб` снимает неоднозначность ссылки, но не даёт этого одобрения. Автономный режим и разрешение на автокоммит его также не заменяют.

**Привязка и ограничения:** по отчёту scout, `/home/alex/sources/nextorm`, ветка `1.0.11`, рабочее дерево чистое. Указанный файл дизайна отсутствует в checkout; расхождение между `local draft, pending review` и `written spec approved by user` не меняет требования об отдельном одобрении плана. Автокоммит разрешён только для принятого результата в этом репозитории, без push и без затрагивания посторонних изменений.

Форма, специализированный скилл и входной объём DO не назначаются. Для снятия блокера нужен отдельно подготовленный и одобренный план реализации #26; его подготовка не считается выполнением самой задачи.

#### Issue evidence

Источник: `pdca-scout`, отчёт об issue из диспетчерского брифа; исходная страница по каноническому URL ниже. В ACT страница повторно не запрашивалась.

Canonical URL: https://github.com/AlexeyShirshov/nextorm/issues/26
Title: “TODO: struct support (zero allocations)”

```text
## Design draft

**Design record:** `docs/specs/design/issue-26-struct-streaming-results.md` (local draft, pending review; not published).

The written spec is approved by the user. The next stage is a separate implementation plan; that plan is not prepared or approved. Implementation is not authorized.

## Intent and scope

Support numeric readonly structs/record structs through public constructors and mutable structs with an implicit parameterless constructor plus public settable properties through member-init. Remove the per-row heap result-object allocation while preserving existing public APIs and terminal semantics. This is not a zero-total-allocation or speedup promise. The shared SQL materializer path should support existing `ToList`, `ToArray`, `ToHashSet`, `ToDictionary`, enumerable/async enumerable, direct enumerator, and First/Last/Single family terminals (sync and async). Aggregates are unchanged. In-memory support is not claimed or verified.

Explicitly out of scope: new `ToAsyncEnumerableStruct`/ref APIs or custom enumerators, ref-mapper redesign, arbitrary fields/private constructors/unsupported immutable shapes, general boxing/converter redesign, and other-provider feature work.

## Diagnosis and design direction

The independent SQLite matrix currently has 100 cases: 50 constructor cases pass and 50 mutable member-init cases fail with `QueryPreparationException: Cannot get ctor` at `RowMaterializerBuilder.BuildCore` (line 312). The constructor resolver returns null for implicit-default structs. Correct shared expression construction for member-init, using type-based construction where appropriate (e.g. `Expression.New(resultType)`), while preserving constructor semantics and class behavior; exact method decomposition belongs to the plan. Do not blanket-default explicit projections or alter unsupported class behavior.

## Evidence and acceptance direction

The controlled benchmark-local class/value/ref-readonly experiment used 32/128/256 B payloads, 1000 rows, warmed cache, .NET 11.0.0, BDN 0.15.8/InProcessEmit, 3 warmups/5 iterations, Windows 11 / Ryzen 7 9800X3D. Value-vs-ref mean differences were under 1%; this short in-process syntax/allocation proof demonstrates no material case for a special API, not that copies are universally free. Observed class-vs-struct `ToList` delta was 28 B/row for a one-int projection (24 B object + 4 B storage); streams were 24 B/row, while struct allocation stayed constant from 1,000 to 10,000 rows. These are observations, not exact-byte guarantees. Collection value storage/backing arrays and driver/query setup allocations remain possible.

Acceptance for the subsequent plan: all 100 independent SQLite cases pass without skips; populated cases assert numeric values for both shapes; retain scalar/class regressions and test implicit-default mutable member-init plus explicit readonly constructor projection. Preserve empty/default, multiple-result, dictionary-key, cancellation, and disposal semantics. Demonstrate no per-row result-object allocation by allocation path/scaling trend, without brittle exact-byte CI thresholds or timing SLA. Run affected unit and targeted SQLite tests, then the canonical capped integration suite at the DO→CHECK boundary; skipped provider runs are not green evidence. The benchmark is diagnostic, not a hard CI threshold.

Report: `BenchmarkDotNet.Artifacts/results/NextORM.Benchmark.Issue26StreamingMaterializerBenchmark-report-github.md`; diagnostic commit `7716ff962`, source `benchmarks/nextorm.benchmark/Focused/Issue26StreamingMaterializerBenchmark.cs`. No full integration suite has been run for this draft. No open product questions. Written-spec approval is complete; the next gate is preparation and separate approval of an implementation plan. Implementation and tests are not authorized until that gate passes.
```

Page facts: Open; label `enhancement`; milestone `1.0.11`; no assignee, branches or pull requests. The source page contains both “local draft, pending review” and “written spec approved by user”; the scout could not reconcile these, and the referenced design file is absent from the current checkout. Earlier scout facts: branch `1.0.11`, clean working tree; no local current T026/collection input.

## Approval record

`not applicable: autonomous mode` — слотов нормального режима нет. Отдельное одобрение плана реализации из issue не получено (§PLAN record).

## DO result

`not reached: PLAN → ACT` — реализации, испытаний и кандидата нет.

## CHECK record

`not run: PLAN → ACT` — кандидата нет, независимой проверки не было.

## ACT record

- Исход `PLAN → ACT`: `blocked / STOP` — `original task is infeasible under the stated constraints`; источник: §PLAN record, `PLAN-r1-controlling` и `Issue evidence`.
- Уточнение определило GitHub #26, но не разрешило реализацию. Согласно PLAN и issue, отдельный план реализации не подготовлен и не одобрен.
- Результат не передан и не принят: реализация остаётся `unmet`, соответствие ориентирам issue — `unverified`. Проверка, тесты и подготовка плана реализации не начаты.
- Для снятия блокера требуется отдельная подготовка и одобрение плана реализации #26; это не результат исходной задачи и не действие текущего ACT.
- ACT завершён после сохранения полного отчёта `pdca-check` в режиме `retrospective` в §Lessons learned. Исход STOP не изменился; дальнейших действий в этом цикле нет.
- Источники доступны в статусе; в evidence root нет артефактов исполнения или проверки.

## Lessons learned

### Ретроспектива цикла (`mode: retrospective`, `PLAN → ACT`)

- **Задача и объём цикла:** исходный запрос `pdca по задаче 26 автономно автокомит`, референт уточнён пользователем как GitHub issue [#26](https://github.com/AlexeyShirshov/nextorm/issues/26) в текущем репозитории `nextorm` (поддержка двух форм числовых структур в общем SQL-материализаторе без выделения объекта результата в куче на каждую строку, с сохранением публичного API и семантики терминальных операций).
- **Фактически выполненные фазы и счётчики:** `N=1`, `r=1`, `n=0/3`; `Defect history: none`. Выполнен только входной `PLAN` (`PLAN-r1-initial` и после уточнения `PLAN-r1-controlling`) с терминальным переходом `PLAN → ACT` (`blocked / STOP`: `original task is infeasible under the stated constraints`). `DO: not run`, `CHECK: not run`. Артефакты в `/home/alex/sources/nextorm/.pdca/issue-26/rv1/` отсутствуют (не создавались).

#### Что сработало и что не сработало (наблюдаемые факты)
- **Сработало:**
  1. Первичный `PLAN-r1-initial` не стал угадывать источник «задачи 26» при наличии риска выполнить и автоматически закоммитить не ту работу (`.pdca/status/issue-26-1.md:30-40`).
  2. После уточнения (`гитхаб`) `PLAN-r1-controlling` корректно распознал внешний гейт в тексте самого issue #26 (`The next stage is a separate implementation plan; that plan is not prepared or approved. Implementation is not authorized`, `.pdca/status/issue-26-1.md:70,88`) и не подменил отдельное одобрение плана реализации автономным режимом запуска или разрешением на автокоммит (`.pdca/status/issue-26-1.md:52-55`).
  3. Оба сырых отчёта `PLAN` сохранены без искажений в одной ревизии `r1` без ложного инкремента счётчиков (`.pdca/status/issue-26-1.md:26-56,118-124`).
- **Не сработало / остановило выполнение:**
  1. В короткой формулировке запроса (`задача 26`) не был указан источник (`GitHub #26`), а запуск `pdca` с флагами `автономно автокомит` был подан для тикета, в котором явно запрещены реализация и тесты до отдельной подготовки и одобрения плана реализации.
  2. Первая отправка `pdca-check` в режиме `retrospective` была прервана блокером из-за обрезки inline-контракта `references/check.md` и отсутствия inline-контракта `references/act.md`; повторная отправка доставила оба контракта полностью.

#### Уроки (Lessons learned)

1. **[Категория: `task-framing / scope-identification`]** Короткая ссылка вида «по задаче N» без префикса источника (`GitHub #N`, путь к спеке или локальному статусу) в репозитории с историческими упоминаниями одноимённых номеров не позволяет однозначно идентифицировать предмет работы на входе `PLAN` и требует уточнения до назначения объёма `DO`.
   - *Указатели на свидетельства:* `.pdca/status/issue-26-1.md:16-20,34-40` (`PLAN-r1-initial`).
   - *Что делать иначе:* при запуске цикла по тикету явно указывать источник (`GitHub #26` / URL).

2. **[Категория: `governance / pre-implementation-gate`]** Наличие в постановке issue явного запрета на реализацию до отдельной подготовки и одобрения плана реализации (`Implementation and tests are not authorized until that gate passes`) блокирует автономный цикл реализации (`original task is infeasible under the stated constraints`): флаги `автономно` и `автокомит` определяют режим работы драйвера, но не отменяют зафиксированных в источнике требований об отдельном гейте одобрения плана.
   - *Указатели на свидетельства:* `.pdca/status/issue-26-1.md:46-56` (`PLAN-r1-controlling`), `.pdca/status/issue-26-1.md:68-70,88` (`Issue evidence`).
   - *Что делать иначе:* до запуска автономного цикла реализации с автокоммитом либо пройти гейт подготовки и явного одобрения плана реализации, либо явно поставить задачу на подготовку самого плана / снять ограничение в инструкции пользователя.

#### Ограничения и неопределённость (Limits and uncertainty)
- **Непроверенные и отсутствующие данные (`absent` / `unverified`):** фазы `DO` и кандидатный `CHECK` не запускались (`DO: not run`, `CHECK: not run`); продуктовый код и тесты не изменялись и не запускались; каталог `/home/alex/sources/nextorm/.pdca/issue-26/rv1/` пуст. Соответствие кодовой базы ориентирам issue #26 (матрица из 100 случаев SQLite, отсутствие построчных аллокаций объекта результата в куче) остаётся `unverified` (`.pdca/status/issue-26-1.md:98-103,109,112`).
- **Неопределённость в источнике issue (наблюдение vs вывод):** в тексте GitHub #26 одновременно присутствуют формулировки `local draft, pending review` и `The written spec is approved by the user`, а файл `docs/specs/design/issue-26-struct-streaming-results.md` отсутствует в текущем checkout на ветке `1.0.11` (`HEAD` `fd68cf3c17229eac574b74c4f69de5eb552b3619`, `.pdca/status/issue-26-1.md:36,54,68-70,91,133`). Точное местонахождение локального черновика спеки из имеющихся свидетельств неизвестно (не устанавливалось в рамках read-only ретроспективы), однако это не влияет на зафиксированный терминальный блокер по отсутствию одобренного плана реализации.

#### Сигнал завершения ретроспективы
- `retrospective_status: complete` (отчёт сформирован для записи исполнителем `pdca-executor` в `## Lessons learned`; исход `blocked / STOP` не изменяется).

## Revision history

`none` — оба отчёта сохранены в порядке поступления в `r1`; уточнение ссылки не создало новой ревизии или цикла.

## Defect history

`none` — попыток исполнения и исправлений не было.

## Changed files

- `.pdca/status/issue-26-1.md` → запись PLAN, источника issue и закрытия ACT с ретроспективой. Продуктовых изменений нет.

## Git state

- status path: `/home/alex/sources/nextorm/.pdca/status/issue-26-1.md` — создан в этом цикле · `untracked` (сохранён на диске, не закоммичен).
- База: по отчёту scout ветка `1.0.11`, рабочее дерево чистое на момент разведки; при создании `HEAD` — `fd68cf3c17229eac574b74c4f69de5eb552b3619`. Целевой путь до записи отсутствовал в рабочем дереве и индексе; нового вывода обо всём дереве не делалось.
- Нет принятого результата или изменений к коммиту. Staging, commit и push не выполнялись; посторонние изменения не затронуты.

## Progress log

2026-11-19T06:43:40Z | PLAN | revision 1 | attempt 0 | Зафиксирован первоначальный терминальный отчёт до уточнения | §PLAN record / PLAN-r1-initial
2026-11-19T06:43:40Z | PLAN | revision 1 | attempt 0 | Зафиксировано уточнение «гитхаб» и действующее решение того же входного PLAN | §Original task; §PLAN record / PLAN-r1-controlling
2026-11-19T06:43:40Z | ACT | revision 1 | attempt 0 | PLAN → ACT: blocked / STOP — нет отдельно подготовленного и одобренного плана реализации; результат не передан и не проверен; ретроспектива ожидается | §ACT record; §Lessons learned
2026-11-19T06:53:20Z | ACT | revision 1 | attempt 0 | ACT закрыт: отчёт ретроспективы сохранён; blocked / STOP — нет отдельно подготовленного и одобренного плана реализации; реализация не выполнена, соответствие ориентирам не проверено | §ACT record; §Lessons learned
2026-11-19T06:54:19Z | ACT | revision 1 | attempt 0 | Notice: историческое наблюдение scout — `docs/specs/design/issue-26-struct-streaming-results.md` отсутствовал в checkout ветки `1.0.11`. Позднее пользователь сообщил, что отдельный поиск по другим рабочим деревьям нашёл этот файл в локальном коммите `c50ec3a3844f3bc5ff44fda4f46763da13759d6f` на ветке `feat/issue-26-struct-projection`, не выданной ни в одном зарегистрированном рабочем дереве; в этом цикле сообщение независимо не проверялось. Это только контекст, не одобрение плана реализации и не разрешение переключать ветки или менять продукт. Исход `blocked / STOP` не изменился. | §PLAN record / Issue evidence (историческое наблюдение); сообщение пользователя после ретроспективы (новый контекст)
