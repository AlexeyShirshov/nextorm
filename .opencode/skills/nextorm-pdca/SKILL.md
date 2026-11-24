---
name: nextorm-pdca
description: "nextorm overlay for the global `pdca-dotnet` cycle: project implementation constraints, the DO design pass for code changes, independent CHECK assessment, cached-path performance acceptance, and ACT hygiene. Load together with `pdca-dotnet` before PLAN whenever a PDCA cycle runs in this repository."
---

# nextorm: PDCA-оверлей (DO/Check)

Проектные дополнения к глобальному скиллу **`pdca-dotnet`**. Загружай их **вместе** с ним и
**до PLAN**: сначала `pdca-dotnet` (машина состояний, гейты, делегирование), затем этот
оверлей. Здесь — nextorm-ограничения реализации, DO-дизайн-проход, CHECK-оценка его evidence,
перф-приёмка и гигиена ACT. Гейты и машину состояний оверлей **не меняет** — уточняет содержание фаз.

## Политика агентов

- Предметные агенты (`nextorm-design-engineer`, `nextorm-code-auditor`,
  `nextorm-db-perf-analyst`, `nextorm-inmemory-perf-analyst`) существуют для интерактивной
  работы. В цикле их **не диспатчат**. Нужные ограничения и процедура дизайн-прохода встроены
  ниже: standalone `nextorm-design-engineer` не требуется читать или запускать.
- Driver передаёт применимые инструкции этого оверлея **inline** в DO и каждый CHECK. В DO
  `pdca-executor` готовит design/audit evidence; CHECK получает отчёт и независимые наблюдения,
  достаточные для суждения. CHECK не загружает скиллы, не читает product source, не запускает
  сканы и не создаёт субагентов. Недостающее evidence запрашивается driver у уже разрешённых ролей;
  permission и роль CHECK не расширяются.
- Записи в регистры (`docs/specs/design/*`) и правки кода/конфигов делает `pdca-executor`
  в фазе Do, не оркестратор.

## Владение фазами и проектные ограничения

### Граница PLAN / DO / CHECK

Базовый контракт `pdca`/`pdca-dotnet` авторитетен: PLAN определяет только результат, критерии
приёмки, ограничения, границы, риски и условия остановки. Этот оверлей не добавляет критерии и
не поручает PLAN проектировать исполнение или проверку.

- Вариантную матрицу из фактических путей исполнения, test strategy, coverage/mutation strategy,
  конкретные команды, oracle/evidence methods и подготовку артефактов выбирает и выполняет DO
  (`pdca-executor`) в пределах утверждённой задачи. Project policy может войти в PLAN как
  ограничение, но не как замороженная процедура проверки.
- CHECK самостоятельно выбирает метод и достаточность проверки по базовому контракту. PLAN не
  обязан заранее доказать конкретный oracle или закрепить evidence-source/command. Если факт о
  measurability действительно неясен, base допускает один bounded choice probe; unusable method
  при CHECK пере-выбирается, а недостающее evidence дособирается. Только критерий, который нельзя
  измерить доступными средствами, использует существующий PLAN → ACT outcome.
- DO передаёт идентифицированный implemented candidate на CHECK, даже если declared proof ещё не
  получен. В handoff отдельно указывай implementation status и статус каждого declared criterion;
  для `unverified` перечисляй точный недостающий run/result/artifact. Отсутствие proof не превращает
  реализованный candidate в incomplete implementation и не отправляет выбор метода в PLAN.
- Design/audit отчёт — candidate evidence от DO, не самосертификация. Каждый CHECK оценивает его
  применимость, полноту и findings; правила и конкретный формат приведены ниже.

Следующие nextorm-инварианты ограничивают реализацию и fix; они не превращают findings или
технические решения в обязанности PLAN.

1. **Абстракция только на втором потребителе / наблюдаемом шве.** Не предлагать
   `IX`/`X` «потому что чище»: при единственном потребителе сплит запрещён
   (`solid-review.md` F12; F3/F7 — `IConnectionFactory`, `IColumnMapper`).
2. **KISS = число концептов.** Новый тип/интерфейс/индирекция обязан назвать
   конкретного потребителя, которого разблокирует.
3. **Measure, never assert.** «Аллоцирует / медленнее» — только из кода (аллокация
   на per-row пути) или в пользу perf-аналитика. Дельты <~20% — шум на этой машине
   (разброс до 19%; `docs/specs/performance/decisions-and-lessons.md`).
4. **Не переоткрывать settled findings без нового измерения.** Регистры смотреть
   on-demand (только нужную находку): `docs/specs/design/solid-review.md` (F1..F13),
   `docs/specs/design/code-smells-review.md`, `docs/specs/design/API-NAMING-REVIEW.md`
   (P0-n), `docs/specs/performance/*` (M*/I*/R*).
5. **Public API — сквозной.** Rename/remove публичного типа/члена требует и
   `docs/**`, и `docs/ru/**` в том же изменении (AGENTS.md).
6. **Build discipline задаёт fix.** `TreatWarningsAsErrors=true`, nullable, CRLF,
   CPM (`Version` только в `Directory.Packages.props`). Изменение с warning — не fix.
7. **Известные факты архитектуры не репортить заново:** `DataContext` — тонкий фасад
   над `QueryExecutor`/`QueryPlanner`/`DbConnectionManager`/`ContextEnvironment`/
   `QueryCache`; `InMemoryDataContext` делегирует фасад `InMemoryQueryExecutor`;
   `ISqlDialect` — намеренно 97-членный композит (F12). Длины файлов читать из
   текущего дерева.
8. **Ничего не выносится за текущий milestone.** Если unit/фаза не решается целиком в
   этом цикле, её **можно засплитить**, но все новые задачи/срезы/tracking-issue
   остаются в **том же milestone**, что и исходная. Перенос в следующий milestone
   (например `1.1`) запрещён: нереализованный срез — это отдельный unit/issue с
   текущим milestone, а не `deferred` в будущий релиз. Запись ограничения в
   `docs/advanced/limitations.md` фиксирует поведение, но **не заменяет** задачу в
   текущем milestone.
9. **Поиск — ignore-aware; символы — `roslyn`.** Для C#-символов (типы/члены/`refs`/`callers`/overloads/
    implementations/renames) — только инструмент `roslyn`, не текстовый поиск. В V2 `roslyn` не
    top-level: вызов — **внутри `execute`** (`const r = await tools.roslyn({ action: "members",
    target: "Ns.Type" }); return r`), батч — одним `execute` (`Promise.all`); если его нет в каталоге
    Code Mode, сначала `search({ query: "roslyn" })`. Для текста (доки,
    комментарии, строковые литералы, имена SQL) — `rg`/`git grep` с ограничением по исходникам
    (`rg -n --glob '*.cs' "<lit>" src tests`). **Никогда `grep -r`/`grep -rn`/`find`** по дереву: они
    игнорируют `.gitignore` и читают `bin/`/`obj/`/`TestResults/` (≈1.3 GB артефактов под `src`/`tests`),
    т.е. ~1 GB диска на один вызов. См. `AGENTS.md` → «Searching text».
10. **Повторный заход после терминального `incomplete`/STOP** не перезапускает провалившийся критерий
    без изменений: прежняя терминальная причина — обязательный вход PLAN; `planner` либо показывает,
    что новые факты/evidence устранили терминальную причину. Это не разрешает менять supplied
    criteria и не создаёт отдельного правила oracle для PLAN.

### DO/CHECK: affected paths и объём анализа

Для исправления бага сначала запусти минимальный тест, который фиксирует исходное поведение; он
должен завершиться ошибкой до фикса и пройти после него. Для изменения без дефекта начни с
затронутого unit-проекта и его применимых тестов. Если меняются SQL generation, provider или query
engine, добавь ограниченный live regression на затронутых provider paths по workflow ниже. Собери
затронутый проект один раз после изменения компилируемых inputs, затем запускай его тесты; не
запускай всю solution в локальном inner loop.

Матрицу вариантов составляй только когда изменение действительно затрагивает несколько путей,
форм API или провайдеров. Записывай применимые варианты как `тест` / `guard` / `deferred + триггер`;
не добавляй несвязанные ветки в узкий fix.

### DO: scoped design review

Узкий локальный fix, не меняющий public API или архитектурные границы, требует ревью изменённого кода
и затронутого пути выполнения, но не отдельного `design-review.md`, полного набора SOLID/type/perf
рецептов, подсчёта sealing, smell/API-скана, branch delta или mutation testing.

Для изменений public API, абстракций, ownership-контрактов, слоёв или cross-provider архитектуры
выполни scoped design review по изменённой поверхности и её affected paths. Выбери только
обоснованные линзы; фиксируй candidate, применимые findings с `file:line`, disposition и проверку
фиксов. CHECK независимо оценивает такой отчёт, если он применим. Не создавай обязательные recipe
счётчики или метрики без явного требования контракта. Performance-утверждения требуют применимого
измерения или очевидной аллокации на per-row пути; числа и выводы передавай профильному perf analyst.

## DO: формирование гайдов (документация)

- **Любая новая функциональность требует пользовательской документации.** В том же изменении обнови
  подходящие опубликованные страницы или добавь новые, если подходящих нет. Синхронизируй версии
  EN и RU, исправь устаревшие описания и добавь новые страницы в соответствующие `toc.yml`.
  Объясни, как воспользоваться новой возможностью и какие ограничения действуют. Внутренняя
  спецификация, запись PDCA и XML-комментарии не заменяют пользовательский гайд. В handoff укажи
  пути и разделы обновлённых страниц.
- **Крупный блок новой функциональности выносится в отдельный гайд.** Если большой
  законченный блок новой функциональности не относится ни к одному существующему
  гайду, он оформляется **своим гайдом** (`docs/<тема>/index.md` + нумерованные
  страницы + `toc.yml`), а не дописывается в чужой гайд.
- **Гайд > 15 подразделов — выделять в отдельный блок.** Когда гайд разрастается
  больше чем на 15 подразделов, его выделяют в отдельный гайд-блок — как это сделано
  с гайдом scalar functions (`docs/scalar-functions/`).
- **Перенумерация после выноса.** Когда гайд (или его часть) выносится в отдельный
  раздел/блок либо удаляется, номера оставшихся нумерованных страниц `docs/guide/NN-*`
  пересчитываются подряд, без пропусков. В том же изменении обновляются все ссылки на
  переименованные страницы и записи `toc.yml` с обеих сторон (EN+RU); «дырку» в
  нумерации (например, `30 → 32`) оставлять нельзя.

## DO: применение фиксов (pdca-executor)

Фиксы в пределах утверждённых criteria и scope применяет `pdca-executor` по общему «Coder editing
discipline» (`pdca-dotnet` §Delegation), с nextorm-конкретикой:

- Работай по одной связной оси. Для bug fix сначала зафиксируй красный target test до изменения,
  затем зелёный результат после фикса; сохраняй обе команды, exit codes и логи.
- После изменения компилируемых inputs собери затронутый проект один раз; полный solution build
  запускай только если его требует действующий acceptance scope. Требуется `0 Warning(s) 0 Error(s)`
  (`TreatWarningsAsErrors=true`).
- Запусти затронутый unit project и его относящиеся тесты. При изменениях SQL generation, provider
  или query engine добавь bounded live regressions по affected providers через
  `.opencode/skills/running-integration-tests/SKILL.md`; не заменяй ими unit inner loop.
- **CRLF всегда.** После правки любого файла: `perl -pi -e 's/\r?\n/\r\n/g' <file>`;
  LF-only/смешанные окончания не оставлять.
- **CPM:** версии только в `Directory.Packages.props`.
- Локально серверные тесты используют уже запущенные БД в `wslc` через настройки подключения из workflow; SQLite работает в процессе теста. Пропуск или недоступность требуемого провайдера не подтверждают приёмку.
- **Никаких `git push`/коммитов** без явной просьбы (AGENTS.md).
- **BDN-артефакты трекаются:** после применимого бенчмарка вернуть `benchmarks/BenchmarkDotNet.Artifacts`.
- Performance acceptance остаётся обязательной только для затрагивающих её query/cache/executor/
  materialization/in-memory scope (см. ниже); не добавляй необязательные метрики раньше проверки
  поведения.

## CHECK: независимая приёмка по scope

CHECK независимо сверяет каждое утверждённое acceptance criterion с тем же candidate и первичным
evidence: target regression test, затронутыми unit tests, bounded provider regressions и применимыми
документацией/дизайн/performance результатами. Для каждого критерия верни `met`, `unmet` или
`unverified`; провал поведения или неверный результат требует CHECK → DO. Отсутствие evidence сначала
считай `unverified`, затем запрашивай bounded re-gather по базовым правилам, без автоматического
расширения критериев.

- Получив implemented candidate, CHECK сверяет proof отдельно от implementation status. Для каждого
  declared criterion укажи требуемый/полученный evidence и точный gap; не трактуй недостающий proof
  как доказательство незавершённой реализации.
- Mandatory criterion со статусом `unverified` или `unmet` запрещает PASS. Если proof отсутствует,
  сохрани candidate для CHECK и маршрутизируй bounded re-gather по существующему контракту; не
  проси PLAN выбрать method/oracle.
- Для bug fix проверь red до фикса и green после него. Проверяй именно logs и exit codes, а не только
  summary автора.
- Live integration evidence остаётся обязательным для SQL generation, provider и query-engine
  изменений; локальный CHECK охватывает только затронутые provider paths. Полная integration/release
  gate, coverage thresholds и skip-budget становятся критериями только когда их требует уже
  существующий declared scope. Выполни такой полный gate целиком на его границе; local workflow ниже
  не разрешает `--full`. CI и опубликованные thresholds не меняются.
- Оценивай design report только для public API, архитектурных или иных изменений, для которых выше
  задан scoped design review. Не требуй sealing/recipe/suppression/coverage/mutation counters для
  узкого fix.
- CHECK принимает/отклоняет findings по независимому evidence и candidate identity; report-only
  улучшения вне scope остаются `deferred + trigger`. CHECK не правит код или регистры и не расширяет
  собственные права.

#### Coverage evidence lifecycle (nextorm)

Этот lifecycle действует только если coverage criterion задан в утверждённом PLAN либо применимом
предшествующем CI/release contract. Для узкого fix coverage и mutation testing не являются
дополнительными gates.

- **Пороги и scope неизменны.** Whole-solution gate `coverage.settings.xml` охватывает
  `nextorm.{core,sqlite,postgres,sqlserver}` с `MIN_LINE_COVERAGE=85` /
  `MIN_BRANCH_COVERAGE=75`. Integration gate `coverage.integration.settings.xml` требует
  `MIN_INTEGRATION_LINE_COVERAGE=65` / `MIN_INTEGRATION_BRANCH_COVERAGE=75` на полном union
  `tests/nextorm.integration.tests` + `tests/nextorm.extensions.integration.tests` и assemblies
  `nextorm.{core,sqlite,postgres,sqlserver,mysql,mariadb,clickhouse,entityframeworkcore,extensions}`;
  исключение `NextORM.Core.InMemory*` сохраняется. CI workflow, команды, thresholds и main-only
  hard-fail не меняются.
- **Требуемое измерение.** Сначала зафиксируй критерий, точный scope/denominator, baseline и candidate,
  конфигурацию, selection и логи. Для дельты или mutation report проверь comparability и фактический
  selection; не запускай повторы без диагностики причины и подтверждённого исправления. Полную
  coverage scope собирай только когда действующий criterion требует её и подходящего evidence нет
  либо его валидность потеряна. Обычный inner loop остаётся затронутыми тестами без instrumentation.
- **Полный integration gate.** Если declared scope требует integration coverage/skip-budget acceptance,
  сохрани оба raw Cobertura отчёта по объявленной конфигурации и полные логи; один suite gate не
  закрывает. Переиспользуй эквивалентное evidence и пересобирай summary из неизменённых raw reports,
  не запуская лишнюю collection.
- **Инвалидация и сбои.** Изменения product/tests/build/provider/runtime/tool/config инвалидируют
  только зависимое evidence. Сохраняй каждый failed run. Retry допустим только после подтверждённой
  infrastructure диагностики и её исправления; assertion failure — результат теста, не infrastructure
  (`pdca-coder` §Test run safety).

### Доказательства для CHECK

Executor передаёт независимому CHECK утверждённый PLAN по точной ссылке, идентификатор проверенного
кандидата и ссылки на первичные результаты по критериям PLAN. Например, для критерия о покрытии
укажи конфигурацию, исходный отчёт покрытия и команду, которая его получила. CHECK оценивает
каждый критерий: `met`, `unmet` или `unverified`.

`rv` — ревизия контракта, а не номер запуска. Используй прежний результат, если его существенные
входы и условия соответствуют кандидату. Для сравнения с baseline укажи его собственную версию
и условия измерения отдельно.

**Проектные evidence-корни (nextorm).** Портативный агент `check` даёт scoped `read` на `.pdca/**`,
`TestResults/**` и на исторические `docs/specs/status/**`, `artifacts/**`; nextorm-специфику
объявляют **на стороне проекта**, а не в общем агенте/асете:
- статус/контракт — `.pdca/status/**` (новые циклы); `docs/specs/status/**` — историческое, read-only;
- evidence-артефакты — `.pdca/<task>/rv<k>/**` (новые циклы); `artifacts/**` — историческое evidence, read-only;
- тест-артефакты — `TestResults/**`;
- scratch вне worktree — `/tmp/nextorm*/**` (через `external_directory`, см. проектный `opencode.json`).


Проверяй обязательные criteria из PLAN и применимого существующего CI/release contract. Необязательные
метрики и дополнительные сканы не создают gates.

**Недостающее evidence — не продуктовый дефект:** driver организует re-gather через уже разрешённые
роли в пределах закреплённого бюджета, без product FAIL только из-за отсутствия отчёта. CHECK
оценивает пополненное evidence; сама роль CHECK не запускает gather и не расширяет permissions.
Доказанный дефект требует FAIL; новый обязательный вариант — обоснованного CHECK → PLAN
с явным supersession ревизии контракта и сохранением обязательств.

Пока применимая обязательная строка открыта, PASS запрещён. При исчерпании бюджета CHECK
остаётся неразрешённым; применяются существующие правила блокеров/эскалации, без новой фазы.

### Перф-приёмка cached path

Обязательна для циклов, затрагивающих планирование запросов / plan cache,
executor / материализацию и выполнение запросов in-memory. Одна команда, без
контейнеров:

```
dotnet run --project benchmarks/nextorm.benchmark -c Release -- --anyCategories=acceptance
```

- **Чья это предметная область.** In-memory (`InMemoryDataContext`, LINQ по спискам,
  `InMemoryBenchmark*`) — `nextorm-inmemory-perf-analyst`; ADO (`DataContext`, plan
  cache/`Prepare()`, `QueryExecutor`, SQL builders, `SqliteBenchmark*`) —
  `nextorm-db-perf-analyst`. Симптом на обоих контекстах — начинать с того, что
  первым на горячем пути, и назвать допущение.
- Приёмка обязательна для query-path изменений (план/cache/executor/материализация).
  Для in-memory execution набор напрямую покрывает только агрегаты `Count`/`GroupBy`;
  прочие in-memory изменения требуют дополнительных релевантных бенчмарков помимо набора.
- Убедиться, что команда выбрала хотя бы один кейс и выполнила все выбранные
  кейсы без пропусков и падений. Рядом с baseline
  (`docs/specs/performance/acceptance-benchmarks.md`) записать wall time, Mean и
  Allocated по каждому кейсу, а также cached/prepared ratio.
- Рост **comparable** cached-vs-prepared ratio больше **20 %** относительно baseline —
  повод расследовать и записать исход; это **не** автоматический hard fail. Baseline
  содержит высокую погрешность измерения (у `Nextorm_Count` `Error` > `Mean`), поэтому
  расследование обязано учитывать разброс прогон-к-прогону: повторить на той же машине,
  при необходимости — реплицировать, и зафиксировать исход «шум / воспроизвелось».
- Несопоставимые прогоны (другая ОС/CPU/config/набор кейсов, `NEXTORM_BENCH_FULL=1`,
  out-of-process vs in-process toolchain) и шумовые различия регрессией **не называть**.
- Цель стоимости: **≤4 мин** wall clock на текущем хосте для `ShortRun`. Превышение —
  провал gate: зафиксировать измеренное время; критерий молча не менять.
- Для несвязанных documentation-only циклов приёмка не требуется.
- **Параметро-биндинг / TVP (per-row путь).** Если цикл трогает биндинг параметров
  или TVP, DO явно решает вопрос замера: либо прогон/бенчмарк, либо аргумент с `file:line`,
  что изменение one-time/per-column, а не в цикле по строкам (per-row циклы:
  `Project`/`BuildRows`/`WriteJson`/`ToTypedArray`). CHECK оценивает это evidence; «не нужен» без
  аргумента не принимается, а per-row работу без замера не закрывает.

Если scoped design/performance review применим, отчёт на русском кратко фиксирует scope/candidate,
наблюдения, применимые findings и disposition, проверку фиксов и ссылки на полные логи. Не добавляй
suppression ratio, sealing census или другие счётчики, если их явно не требует применимый контракт.

## ACT: nextorm-гигиена планов

- **Полностью реализованный TODO удаляется.** Как только все фазы/пункты плана
  `docs/specs/roadmap/todo_<feature>.md` реализованы и его tracking issue закрыт,
  файл плана **удаляется** (`rm`) в том же цикле ACT, а не оставляется «на память».
  Частично реализованный план (остались отложенные фазы/провайдеры) сохраняется.
- **Связанный issue закрывается в автономном режиме с автокомитом.** Если пользователь явно
  запросил автономную работу с автокоммитом (`pdca-dotnet` §Autonomous mode → «Auto-commit»),
  то, когда вся задача завершена (все её циклы/срезы), `pdca-executor` сразу после ACT-коммита закрывает
  её связанный GitHub-issue: `gh issue close <N> --repo <owner/repo> --comment "<итог + ссылка на
  проверку>"`, где `<N>` — из статус-файла/префикса коммита (`#N`). При сплите или оставшихся
  срезах issue остаётся открытым (инвариант 8), остаток идёт в статус-файл и финальный отчёт.
  В ручном режиме (без явного автокомита) и при блокере/STOP issue **не** закрывается. Сбой
  закрытия (сеть/доступ) — `Notice:` в логе и в финальном отчёте, без ложного «закрыто»; push
  не выполняется.
- Закрытие фиксируется в `docs/specs/design/API-NAMING-REVIEW.md` §«Закрытие планов»:
  путь удалённого файла + закрытый tracking issue; живой трекинг ведут этот реестр и
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`.
- Ссылки на удалённый план (`[label](todo_<feature>.md)`) **де-линкуются** по всему
  `docs/**` — остаётся только текст-label, чтобы не оставлять битых относительных
  ссылок. Инлайновые упоминания в исторических записях (audit/status/release) — это
  исторические артефакты, переписывать прозу не нужно.
- `rm` и правки регистров делает `pdca-executor` в фазе Do/ACT; CRLF сохраняется.
- **Milestone при сплите не меняется.** Частично реализованный план сохраняется (см. выше),
  а его оставшиеся фазы/провайдеры остаются в **текущем** milestone: новые issue/срезы
  наследуют milestone исходной задачи. Создавать issue с будущим milestone или переносить туда
  остаток — запрещено (инвариант 8). Заблокированный срез остаётся открытым пунктом плана в
  текущем milestone с триггером пересмотра, а не выносится за его пределы.
- **Перед закрытием потока.** Сам статус-файл удаляется по глобальному контракту (§ACT →
  «Status-file lifetime»); nextorm-специфика — оставшиеся открытыми пункты `Deferred + триггер`
  предварительно переносятся в живой трекинг: регистры `docs/specs/design/*`,
  `docs/specs/roadmap/todo_*.md`, issue.
