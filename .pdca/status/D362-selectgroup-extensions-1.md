# Cycle status: D362-selectgroup-extensions / cycle 1

## Current state

- task: `D362-selectgroup-extensions` · form: `cycle` · specialized skill: `pdca-coder` + `pdca-dotnet`
- mode: `normal`
- cycle: `N=1` · revision: `r=1` · attempt: `n=3/3` (CHECK #1/#2/#3 not accepted; no fourth attempt and no second evidence round authorized)
- scope: полный объём задачи #362 за один цикл — публичный декларативный контракт компонентов в Core, полное удаление политики SelectGroup из Core, реализация SelectGroup/SelectGroupHaving в nextorm.extensions, доказательство внешней расширяемости без IVT, миграция тестов/примеров, отсутствие регрессии производительности vs baseline f991d5ea, документация EN/RU · status path: `.pdca/status/D362-selectgroup-extensions-1.md`
- goal: рабочая реализация переноса SelectGroup/SelectGroupHaving в nextorm.extensions поверх публичного декларативного контракта Core с сохранением SQL-семантики на всех СУБД и без регрессии производительности/покрытия — §PLAN record и §Original task авторитетны
- phase: `ACT` · record: `completed` · outcome: `incomplete / STOP` (CHECK final) — user explicitly **accepted the measured component-operator overhead** on 2026-11-06; the delivered behaviour stands; U6/AC9 for follow-up are governed by that acceptance; optimization tracked in **#368**; cycle closed, no further DO/CHECK; status retained
- source: пользовательская инструкция «pdca приступай к реализации»; agreed scope https://github.com/AlexeyShirshov/nextorm/issues/362#issuecomment-6654133696 · context refs: branch `1.0.9-rc5`, HEAD `f991d5ea`, milestone `1.0.9-rc5`, tracking #362 (canonical), #367 (closed as transferred, not completed)
- criteria: `U1–U9`, `AC1–AC11` — точный текст в §PLAN record r1 (источник: эта запись), не перепечатывать
- evidence root: `.pdca/D362-selectgroup-extensions/rv1/`

## Original task

**Пользовательская инструкция (дословно):** «pdca приступай к реализации»

**Согласованный объём (точная ссылка, не переформулировать):** https://github.com/AlexeyShirshov/nextorm/issues/362#issuecomment-6654133696 (GitHub #362, milestone 1.0.9-rc5)

**Явная инструкция пользователя сегодня (дословно, ответ на вопрос о шуме производительности):** «Если новая реализация и baseline статистически неразличимы в пределах измеренного шума (A/A-контроль показывает тот же разброс) — считать отсутствием регрессии; протокол измерения и границы шума фиксируются.»

**Отслеживание:** #362 — канонический; #367 — закрыт как перенесённый, не выполненный.

**Ограничения (Constraints):**
- Никаких git commit/push/merge агентом; пользователь пушит.
- Не изменять чужие незакоммиченные файлы документации: `docs/advanced/extensibility.md`, `docs/advanced/toc.yml`, `docs/ru/advanced/extensibility.md`, `docs/ru/toc.yml`; никогда не трогать `docs/specs/status/**`, `artifacts/**` и чужие `.pdca/**`.
- `nextorm.core` не получает `InternalsVisibleTo` для `nextorm.extensions`/тестов компонентов; запрещены reflection emit, сырой SQL, глобальный реестр обработчиков; без новых `[ExcludeFromCodeCoverage]`/фильтров для сокрытия кода операторов/механизма; без ослабления порогов.
- Конвенции: CRLF, CPM (версии только в `Directory.Packages.props`), `TreatWarningsAsErrors=true`, FluentAssertions-only (`Xunit.Assert` только для `Assert.Skip*`).
- Интеграционный набор fail-closed при `DOCKER_HOST`; прогон всего решения — `--max-parallel-test-modules 1`.
- Документация EN + RU; примеры/тесты алиасов/CTE переводятся на `NextORM.Extensions`.

## PLAN record

### r1

Source: `pdca-planner` (medium tier). The plan below is recorded verbatim; its first line (`---`) is the beginning marker for read-back/hash-match.

````markdown
---
# План цикла PDCA (N=1, r=1, n=1)

- **Привязка к снимку (Anchor):** репозиторий `/home/alex/sources/nextorm`, ветка `1.0.9-rc5`, коммит `HEAD = f991d5ea`. Рабочее пространство PDCA и артефакты доказательств: `.pdca/` внутри основного чекаута `/home/alex/sources/nextorm`.
- **Отслеживание:** GitHub Issue `#362` (milestone `1.0.9-rc5`, канонический объём `https://github.com/AlexeyShirshov/nextorm/issues/362#issuecomment-6654133696`).

## 1. Форма, специализированный скилл и входной объём (Form, specialized skill, entry scope)

- **Форма (Form):** `cycle` (обычный цикл).
- **Специализированный скилл (Overlay):** `coder+.NET` (`pdca-coder` + `pdca-dotnet`).
- **Обоснование формы:** архитектурный метод (декларативные неизменяемые вклады подготовки `QueryPreparationContribution`, вынос `SelectGroup` / `SelectGroupHaving` в `nextorm.extensions` через публичные контракты `nextorm.core`, общее классифицирование агрегатов и семантический компаратор выражений) полностью спроектирован и зафиксирован в `docs/superpowers/specs/2026-11-05-extensible-selectgroup-design.md`, а объём работ жёстко ограничен известным набором файлов ядра, пакета расширений, тестовых сборок и документации EN/RU без открытых исследовательских развилок.
- **Входной объём (Entry scope):** полный объём задачи за один цикл — публичный декларативный механизм группирующих компонентов в `nextorm.core`, полное удаление политики `SelectGroup` из `nextorm.core` (и `nextorm.core.sourcegenerator`), реализация `SelectGroup` и `SelectGroupHaving` в `nextorm.extensions`, доказательство расширяемости в независимой сборке без `InternalsVisibleTo`, миграция и пополнение юнит-/провайдерных/интеграционных тестов и примеров, подтверждение отсутствия деградации производительности относительно зафиксированного baseline `f991d5ea`, обновление документации EN и RU.

## 2. Цель, запрошенный/поставляемый результат и критерии приёмки (Goal and acceptance criteria)

### Дословная задача пользователя (Authoritative task)
Указание пользователя: «pdca приступай к реализации» для согласованной задачи:
1. Completely move the `SelectGroup` operator policy out of `nextorm.core` into `nextorm.extensions` (inference visitor, coverage validation, mode policy, flags, special branches all leave Core).
2. Add `SelectGroupHaving(projection, condition)` in `nextorm.extensions`: same key inference as `SelectGroup`, condition AND-ed into `HAVING`.
3. Give third-party packages a public Core-only declarative mechanism to build analogous operators in their own package (no IVT, no reflection emit, no raw SQL, no global handler registry, no edits to our `nextorm.extensions` for each new operator).

- **Запрошенный результат:** рабочая реализация в кодовой базе `nextorm`, в которой `nextorm.core` предоставляет только общий публичный декларативный контракт компонентов подготовки запросов и универсальные сервисы анализа выражений (без знания об операторах `SelectGroup*`), пакет `nextorm.extensions` реализует `SelectGroup` и `SelectGroupHaving` поверх только публичного API `nextorm.core` с сохранением SQL-семантики на всех поддерживаемых СУБД и отсутствием регрессии производительности и покрытия, а документация EN/RU и тесты приведены в полное соответствие.
- **Поставляемый результат:** тот же самый объект — изменения исходного кода `src/**`, тестов `tests/**`, примеров/бенчмарков и документации `docs/**` (EN и RU) в рабочем дереве, удовлетворяющие всем критериям `U1`–`U9` и `AC1`–`AC11`.

### Пользовательские критерии и ограничения (дословно)
- **U1:** `SelectGroup` fully in `nextorm.extensions`; Core keeps no analyzer, no `IsSelectGroup` flag, no mode enum, no operator-specific branch.
- **U2:** Public mechanism usable by an external package referencing `nextorm.core` only; no IVT, no raw SQL, no global registration.
- **U3:** `SelectGroupHaving` = same inference as `SelectGroup` + condition in HAVING.
- **U4:** Existing `SelectGroup` SQL semantics preserved on all providers.
- **U5:** In-memory support for both operators explicitly dropped (`NotSupportedException`), not a regression to fix.
- **U6:** No performance regression vs current `SelectGroup`, including warm/raw plan-cache hits; no overhead on queries without components.
- **U7:** Tests assert expected values directly (no legacy/parity comparisons as the semantic oracle); live tests live in `tests/nextorm.extensions.integration.tests`.
- **U8:** Docs updated in EN and RU.
- **U9:** if the new implementation and the baseline are statistically indistinguishable within measured noise confirmed by an A/A control, that counts as absence of regression; the measurement protocol and noise bounds must be recorded.

### Матрица критериев приёмки (`AC1`–`AC11`)
- **AC1 (Пакетные API и полное удаление политики оператора из Core — `U1`, `U2`, `U3`):**
  - В `NextORM.Extensions` доступны публичные методы расширения `SelectGroup<TEntity, TResult>(this EntityBuilder<TEntity>, Expression<Func<TEntity, TResult>>)` и `SelectGroupHaving<TEntity, TResult>(this EntityBuilder<TEntity>, Expression<Func<TEntity, TResult>> projection, Expression<Func<TEntity, bool>> condition)`, оба возвращают `QueryCommand<TResult>`, реализованы через неизменяемый компонент и `source.WithComponent(...).Select(projection)` без копирования `SelectCore`; `null`-аргументы выбрасывают `ArgumentNullException`.
  - В `nextorm.core` (и `nextorm.core.sourcegenerator`) отсутствуют публичные или внутренние методы `SelectGroup*`, тип/визитор `SelectGroupExpressionAnalyzer`, флаг `IsSelectGroup`, enum режимов `SelectGroup`, специальная ветка в `InMemoryGrouping` и строковые проверки имени метода `SelectGroup` в генераторе и конвейере подготовки/рендеринга SQL (проверяется семантически по символам и дереву компиляции, а не только текстовым поиском).
  - Негативное условие: ни один файл в `src/nextorm.core/**` и `src/nextorm.core.sourcegenerator/**` не содержит операторно-специфичной логики `SelectGroup` или `SelectGroupHaving`, а `nextorm.core.csproj` не добавляет `InternalsVisibleTo` для `nextorm.extensions` или тестовых библиотек компонентов.
- **AC2 (Доказательство внешней расширяемости без привилегий — `U2`):**
  - В отдельной сборке, ссылающейся исключительно на публичный API `nextorm.core` (без `InternalsVisibleTo`, без приватной рефлексии/emit, без сырого SQL, без глобального реестра обработчиков и без правок `nextorm.extensions`), реализованы: (а) сторонний группирующий компонент с собственным ключом владения слотом группировки и (б) вариант с добавлением/переопределением `HAVING`.
  - Существующие реализации `IQueryComponent` / `Prepare` / `BuildQuery` продолжают работать без ломающих изменений обязательных членов интерфейса; дескриптор подготовки передаёт неизменяемый `QueryPreparationContribution` (упорядоченный вектор скалярных лямбда-ключей группировки, где пустой список при активном вкладе означает явную глобальную агрегацию, три-состояние `HAVING` unchanged/clear/replace, аддитивный предикат источника, флаг `RequireDistinct`) и не мутирует сырые поля `QueryCommand`.
  - Публичные сервисы `nextorm.core` предоставляют неизменяемый снимок неподготовленного запроса (проекция, тип источника, `WHERE`, `HAVING`, сортировки, модификаторы группировки), общую классификацию агрегатных выражений (единую с SQL-транслятором, где аргументы `FILTER`/`DISTINCT` агрегатов непрозрачны) и семантический компаратор выражений (отличный от value-free планового компаратора: литералы равны по значению, параметры источника альфа-эквивалентны, захваты замыканий сравниваются по ссылочной идентичности таргета и пути членов, неизвестные узлы консервативно неравны).
- **AC3 (Три семантических исхода и инференс ключей/HAVING в пакете — `U3`, `U4`, `U7`):**
  - Политика трёх исходов целиком находится в `nextorm.extensions`:
    1. *Keyed grouping*: выведенные неагрегатные скалярные подвыражения проекции становятся ключами `GROUP BY`, эффективный `HAVING` = `existingHaving AND condition` (если применимо).
    2. *Global aggregation*: 0 выведенных ключей при наличии хотя бы одного агрегата в проекции или эффективно заданного `HAVING` → глобальный агрегатный запрос с реальным `HAVING` (без искусственного `GROUP BY`).
    3. *Constant projection*: 0 ключей и 0 агрегатов → отсутствие `GROUP BY`, очистка эффективного `HAVING`, провалидированный константный предикат один раз опускается в эффективный `WHERE`, включается `DISTINCT`, что сохраняет 0 строк на пустом входе и не более 1 строки на непустом.
  - Существующий `Having` на источнике и `condition` из `SelectGroupHaving` объединяются по `AND` до валидации покрытия; ключи группировки выводятся только из `projection`; `condition` никогда не добавляет ключей группировки; непокрытое выражение строки в `projection` или эффективном `HAVING` вне агрегата детерминированно завершается `QueryPreparationException` до обращения к БД (без неявного расширения ключей).
  - Все три исхода для `SelectGroup` и `SelectGroupHaving` подтверждены прямыми проверками ожидаемых значений на живых СУБД в `tests/nextorm.extensions.integration.tests` для всех 6 провайдеров: PostgreSQL, SQL Server, MySQL, MariaDB (отдельно от MySQL), ClickHouse и SQLite.
- **AC4 (Точность генерируемого SQL на всех диалектах — `U4`, `U7`):**
  - Провайдерные тесты генерации SQL для всех диалектов (SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse) напрямую проверяют точный список и порядок ключей `GROUP BY`, размещение и объединение `HAVING`, опускание константного предиката в `WHERE` с `DISTINCT`, связывание параметров и конвертеров типов.
  - Негативное условие: в тестах не используются сравнения со старой реализацией в качестве оракула правильности.
- **AC5 (Явный отказ от поддержки In-Memory — `U5`, `U7`):**
  - Вызов `SelectGroup` и `SelectGroupHaving` на in-memory провайдере детерминированно выбрасывает `NotSupportedException`.
  - Старые позитивные in-memory тесты для `SelectGroup` удалены; в модульном тестовом проекте добавлены негативные тесты для обоих операторов. В интеграционных проектах in-memory провайдер не используется.
- **AC6 (Иммутабельность источника, жизненный цикл подготовки и кэш планов — `U6`):**
  - Оба оператора реализуют `IQueryComponentShape` (селектор проекции, сырое условие, стабильные настройки).
  - Порядок подготовки: lookup сырого рецепта по исходной форме (включая `IQueryComponentShape`) → раскрытие навигаций + `Prepare`/валидация/дескриптор → применение декларативных эффектов в отдельное эффективное состояние ДО `PrepareColumns`/`PrepareGrouping` → обычная подготовка клауз → lookup подготовленного ключа плана → рендеринг через `BuildQuery` при промахе. В `BuildQuery` SQL-рендеринга не выполняется нормализация/инференс.
  - `ResetPreparation` полностью очищает/перевычисляет эффективное состояние; клоны и кэш-клоны переносят неизменяемое состояние компонента; повторные подготовки/выполнения исходного `QueryCommand` не накапливают дублирующиеся `AND` в `WHERE`/`HAVING` и не мутируют флаг `DISTINCT` сырой команды; захваченные и позиционные значения перепривязываются при warm-hit; на подтверждённом raw-cache hit визитор инференса вызывается 0 раз.
  - Различия в форме компонента (наличие/отсутствие `condition`, структурно разные выражения, разные константные литералы, разные структуры замыканий) дают разные ключи raw-shape и не дают ложных попаданий в кэш.
- **AC7 (Композиция компонентов и детерминированное разрешение конфликтов — `U2`, `U3`):**
  - Вводится общий вид вклада группировки (слот единственного владельца): повторное применение компонента с тем же ключом владельца заменяет предыдущий; комбинация двух разных владельцев группировки или владельца группировки с `ExclusiveRowSource` детерминированно отвергается независимо от порядка добавления.
  - Наличие на сыром запросе явных `GroupBy`, `Rollup`, `Cube`, `GroupingSets` или `Totals` детерминированно отвергается пакетом по неизменяемому снимку до выполнения в БД.
  - Аддитивные предикаты применяются ровно один раз; `DISTINCT` — монотонный `OR` в эффективном состоянии.
- **AC8 (Отсутствие регрессий смежной функциональности):**
  - Историческое поведение обычного негруппированного `.Having(...)` без `GroupBy` и без группирующих компонентов (тихий пропуск рендеринга `HAVING`) сохранено и покрыто явными регрессионными тестами.
  - Обычный `GroupBy`/`Having`/`Rollup`/`Cube`/`GroupingSets`/`Totals` и существующие операторы пакета `nextorm.extensions` (`SelectWhereMin`/`Max` и др.) проходят все юнит- и интеграционные тесты без регрессий.
- **AC9 (Производительность и аллокации — `U6`, `U9`):**
  - Новая реализация `SelectGroup` и `SelectGroupHaving` не имеет статистически значимой деградации по времени и управляемым аллокациям относительно baseline `HEAD = f991d5ea` на сценариях с компонентами (создание, cold prepare, warm raw-recipe hit, prepared fallback hit, перепривязка параметров, полное выполнение), а запросы без компонентов не несут накладных расходов, согласно §3 и правилу `U9`.
- **AC10 (Документация, примеры и гигиена сборки — `U8`):**
  - Документация обновлена синхронно EN (`docs/**`) и RU (`docs/ru/**`): переезд в `using NextORM.Extensions`, новый `SelectGroupHaving`, три исхода, отказ от in-memory, осознанное ломающее изменение `.SelectGroup<TResult>(...)` (оба generic-аргумента или вывод типов) без `*Old`/`Ex` обёрток.
  - Публичная документация не ссылается на `docs/specs/**` или `docs/superpowers/**`; не затронуты 4 чужих незакоммиченных файла документации.
  - Все вызовы `SelectGroup` в примерах, тестах алиасов/CTE и тестовых проектах переведены на `NextORM.Extensions`.
  - Сборка всего решения `Debug` (и `Release` для бенчмарков) — `0 warnings` / `0 errors`, CRLF, CPM, FluentAssertions-only (проходят `AssertionStyleTests`, `IntegrationSuiteScopingTests`).
- **AC11 (Полный прогон тестов и пороги покрытия):**
  - Полный прогон тестов решения (с `--max-parallel-test-modules 1`) — `0 failed`, без пропусков контейнерных провайдеров (`DOCKER_HOST` активен).
  - Whole-solution gate: `Line >= 85.0%`, `Branch >= 75.0%`; integration gate: `Line >= 65.0%`, `Branch >= 75.0%`; пороги без изменений.
  - Интеграционное покрытие собирает оба интеграционных проекта последовательно и отдельно отчитывает покрытие сборки `nextorm.extensions`.
  - Негативное условие: не добавляются новые `[ExcludeFromCodeCoverage]` или фильтры для сокрытия кода операторов/механизма компонентов.

## 3. Метод измерения (Measurement method)

### 3.1. Производительность и аллокации (`AC9`, `U6`, `U9`)
- **Метрики:** `Mean`/`Median` времени на операцию; управляемые аллокации (`Allocated`, `B/op`); счётчик вызовов анализатора/нормализации на подтверждённом warm raw-recipe hit (порог = `0`).
- **База сравнения:** изолированный слепок текущего кода до изменений на `HEAD = f991d5ea` (отдельный git worktree / изолированная сборка `Release`), без копирования старого кода `SelectGroup` в продуктовые сборки кандидата. Для `SelectGroup` — кандидат против baseline `NextORM.Core.SelectGroup`; для `SelectGroupHaving` — против `.Having(cond).SelectGroup(proj)` как без предсуществующего `.Having(...)`, так и с ним, при обязательном совпадении SQL и параметров; для запросов без компонентов — 7 базовых приёмочных сценариев + обычный `GroupBy` + контроли экстремумов.
- **Матрица сценариев:** keyed (скалярный/составной ключ), global (простые/смешанные агрегаты), constant, filtered grouped/`HAVING`, перепривязка captured/positional. Фазы: construct, cold prepare, warm raw recipe hit, prepared fallback hit, execution (in-process SQLite, фиксированная схема/кардинальность).
- **Условия сопоставимости:** идентичная машина, ОС, .NET 10 SDK, настройки BenchmarkDotNet (`Release`, одинаковый Job/итерации), идентичные фикстуры/кардинальность/исходные выражения; SQL и параметры кандидата и baseline идентичны. Парные сбалансированные прогоны (например `A-B-B-A`) + парный `A/A`-контроль для прямого измерения шума.
- **Порог и правило решения (`U9`):** аллокации на warm raw hit и на контролах без компонентов не растут; вызовов инференса на warm raw hit строго `0`. По времени положительный допуск отсутствует (`0%`). Если относительная разница `Δ = (Candidate - Baseline) / Baseline` меньше или равна верхней границе шума `NoiseBound` из парного `A/A`-контроля при том же протоколе (или доверительный интервал разности парных замеров покрывает `0` внутри подтверждённого `A/A`-коридора), результат классифицируется как статистически неразличимый = отсутствие регрессии (PASS). Протокол, сырые таблицы и вычисленные границы шума фиксируются в артефактах цикла.

### 3.2. Покрытие кода (`AC11`)
- **Метрики:** Line/Branch coverage (`dotnet-coverage` Cobertura + `reportgenerator` TextSummary).
- **Знаменатели:** whole-solution gate (`coverage.settings.xml`, сборки core/sqlite/postgres/sqlserver, все модульные и провайдерные тесты); integration gate (`coverage.integration.settings.xml`, оба интеграционных проекта последовательно, `DOCKER_HOST` активен, стандартный фильтр `-classfilters:"-NextORM.Core.InMemory*"`); отдельная метрика `nextorm.extensions` отдельно от агрегатов ядра/провайдеров.
- **Пороги:** whole `Line >= 85.0%` / `Branch >= 75.0%`; integration `Line >= 65.0%` / `Branch >= 75.0%`; без новых исключений.

## 4. Ограничения и допущения (Constraints and assumptions)

### Ограничения
1. **Git:** никаких `git commit`, `git push`, `git merge` (включая отдельные worktree). Временный worktree для baseline допустим, основное дерево не получает merge-коммитов, после снятия эталона worktree очищается.
2. **Неприкосновенность чужих файлов:** запрещено изменять/откатывать `docs/advanced/extensibility.md`, `docs/advanced/toc.yml`, `docs/ru/advanced/extensibility.md`, `docs/ru/toc.yml`, исторические `docs/specs/status/**`, `artifacts/pdca/**`, чужие `.pdca/**`.
3. **Инструментарий C#:** семантический анализ символов — только `roslyn` внутри `execute`.
4. **Границы пакетов:** `nextorm.core` не имеет `InternalsVisibleTo` для `nextorm.extensions` и `tests/nextorm.querycomponents`; приватная рефлексия/`Reflection.Emit` для обхода видимости запрещены.
5. **Вне задачи:** удаление `SelectWhereMinOld`/`SelectWhereMaxOld` (отложено в `#365` AC6); новые SQL-диалекты/кастомные рендереры; сохранение in-memory для `SelectGroup*`.
6. **Конвенции:** `TreatWarningsAsErrors=true`, CRLF, версии только в `Directory.Packages.props`, FluentAssertions-only (`Xunit.Assert` только для `Assert.Skip*`).

### Ресурсные ограничения
- Доступны .NET 10 SDK, демон Roslyn, `dotnet-coverage`/`reportgenerator`/`docfx`, Podman (`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) для 6 СУБД.

### Допущения
- **A1:** `src/nextorm.core.sourcegenerator` — пустая заглушка, не подключённая ни к одному проекту; удаление проверки имени `"SelectGroup"` из `JoinAliasGenerator.cs` не нарушает сборку.
- **A2:** 4 незакоммиченных файла документации относятся к параллельной работе; документация `SelectGroup`/`SelectGroupHaving` обновляется в профильных страницах `docs/guide/**`, `docs/ru/guide/**` и связанных обзорных страницах.

## 5. Закрытие пробелов (Gaps)

| Пробел | Способ закрытия | Статус |
| --- | --- | --- |
| Формальный статус файла дизайна «не утверждён» | Закрыто прямым указанием пользователя «pdca приступай к реализации» + согласованными требованиями 2026-11-05 | Закрыто (Evidence) |
| Открытый вопрос дизайна §11 (неразличимость измерений) | Закрыто дословным правилом `U9` | Закрыто (User instruction `U9`) |
| Отсутствие готового бенчмарка `SelectGroup` | Baseline снимается в DO на `HEAD = f991d5ea` по методу §3.1 | Закрыто (Метод §3.1) |

**Вопросы к пользователю перед DO:** отсутствуют.

## 6. Минимальное решение и альтернативы
- **Выбранное решение:** (1) аддитивно расширить публичный контракт компонентов в Core: неизменяемое представление сырого запроса, дескриптор с `QueryPreparationContribution` (ключи, три-состояние `HAVING`, аддитивный предикат, `RequireDistinct`), слот эксклюзивного владельца группировки, публичная классификация агрегатов и семантический компаратор; (2) хранить эффективное состояние подготовки отдельно от сырого, применять вклад до `PrepareColumns`/`PrepareGrouping`, полностью очищать в `ResetPreparation`; (3) перенести анализ/покрытие/три исхода в `nextorm.extensions` с обязательным `IQueryComponentShape`.
- **Отвергнуто:** публичный `QueryPlanner`/мутация полей `QueryCommand` (ломает кэш/`ResetPreparation`); `InternalsVisibleTo` для extensions (нарушает `U2`).

## 7. Оценка трудоёмкости
- Крупный, но детерминированный цикл: ~15–25 затрагиваемых продуктовых/тестовых файлов + миграция вызовов + парный бенчмарк + полный прогон с покрытием. Bounded probe не требуется: механизм `IQueryComponent`/`IQueryComponentShape` и конвейер `QueryPreparer` уже верифицированы на пакете экстремумов.

## 8. Передача в DO
Исполнитель получает настоящий план по ссылке и обязан выполнить задачу в рамках `U1`–`U9`, `AC1`–`AC11`, §3.1/§3.2, §4. Декомпозицию, порядок правок и команды проверки определяет DO; независимо проверяет CHECK.

## 9. Риски и условия остановки
### Риски
1. Загрязнение сырого состояния `QueryCommand` при повторных вызовах/клонировании — сломает идемпотентность (`AC6`).
2. Расхождение семантического компаратора с правилами SQL-трансляции — ошибка СУБД или ложный `QueryPreparationException` (`AC2`, `AC3`).
3. Коллизия контейнеров при параллельном запуске двух интеграционных сборок без `--max-parallel-test-modules 1` (`AC11`).
4. Шум среды при микро-бенчмарках без парного чередования и `A/A`-контроля (`AC9`).
### Условия остановки
- **PASS → ACT:** все `U1`–`U9` и `AC1`–`AC11` независимо подтверждены в CHECK.
- **CHECK → PLAN / эскалация:** (1) фундаментальное противоречие между публичным Core без IVT и нулевыми вызовами инференса на warm raw hit; (2) значимая регрессия за пределами подтверждённого `A/A`-коридора; (3) выполнение критерия требует нарушения ограничений (чужие файлы, снижение порогов); (4) исчерпан лимит `n = 3` по одному ключу дефекта.

## 10. Уверенность и пробелы
- **Подтверждено фактами:** точки входа текущего `SelectGroup` в Core и генераторе, структура `QueryComponents.cs`, отсутствие IVT в `nextorm.core.csproj`, инфраструктура `nextorm.extensions*` и `tests/nextorm.querycomponents`, зелёный `f991d5ea`, все архитектурные решения спецификации и правило `U9`.
- **Остаётся допущением:** A1, A2. Открытых блокеров нет.
---
````

## Approval record

approved: user explicitly approved the persisted plan (cycle N=1, r=1) via the driver's approval gate on 2026-11-05T09:29:58Z. Approved scope = recorded PLAN r1 unchanged (criteria U1–U9 / AC1–AC11). Effort/time policy = no cap ("Без лимита", user's explicit choice; persists across resume/replan until changed or revoked). Approval is not a change to the task or criteria.

## DO result

**DO complete — candidate ready for CHECK.**

Final numbers: whole-solution build **0 warnings / 0 errors**; whole test suite (D10c coverage run, `--filter-not-trait Category=Diagnostic --max-parallel-test-modules 1`) **RC=0** (all projects passed; 3 earlier failures fixed in D10b); integration pre-warm **8658 total / 0 failed / 197 capability skips**; clean integration coverage runs **8461 + 118 passed / 0 failed**; whole coverage **line 89.3% / branch 83.7%** (85/75 PASS); integration coverage **line 81% / branch 75.3%** (65/75 PASS); manifest `validate_inner_loop.py` **rc=0**, `pdca_evidence.py lint` **rc=0**.

Delivered per AC:
- AC1: Core operator policy fully removed (analyzer, IsSelectGroup, SelectGroupMode/Keys/Outputs, all branches, source-generator check deleted); `SelectGroupComponent`/`SelectGroupExtensions` in nextorm.extensions.
- AC2: public declarative mechanism (`IQueryPreparationDescriptor`, `QueryPreparationContribution`, `IQueryPreparationContext`, `AggregateClassification`, `SemanticExpressionEqualityComparer`, `ExclusiveGrouping`); independent no-IVT consumer `ExternalGroupingComponent` + tests.
- AC3: inference matrix (12 tests) + three outcomes + live oracles (6 providers).
- AC4: six-dialect SQL generation (8 scenarios × 6 providers) + live row oracles.
- AC5: in-memory rejection tests for both operators.
- AC6: cache/lifecycle (10 tests) + effective-state Clone/CloneForCache/ResetPreparation.
- AC7: composition/conflict (5 tests: two owners, ExclusiveRowSource conflict, same-slot replacement, additive predicate once, DISTINCT monotonic).
- AC8: adjacent functionality preserved (whole suite green; ordinary ungrouped HAVING silent drop retained).
- AC9: **open finding** — measured residual: `Warm_RawHit` +23.5% time / +788 B, all scenarios +543…+2693 B allocations; cause = raw-shape component identity capture (`RawShape.ComponentShape[]` per capture) plus the approved `WithComponent(...).Select(...)` composition (component instance + builder clone + command). Not waived; not reclassified as noise; for CHECK.
- AC10: docs EN+RU updated (guide/05, guide/12, relationships/collection-projection); examples migrated; no specs/superpowers links; DocFX build 0 errors / 4 pre-existing warnings.
- AC11: met — whole gate 89.3/83.7 PASS; integration gate 81/75.3 PASS; both rc=0.

Evidence pointers: `rv1/unit-evidence.md`, `rv1/manifest.json`, `rv1/logs/`, `rv1/perf-baseline/`, `rv1/perf-candidate/`, `rv1/coverage/`.

Unit table:

| Unit | State | Criteria covered | Evidence |
|---|---|---|---|
| D1 | done | AC2 (partial — public types), AC10 | build 0/0; 2 tests pass |
| D2 | done | AC2 (effective state, aggregate classifier, semantic comparer), AC6 (partial), AC7 (partial), AC10 | whole-solution build 0/0; 5 tests pass |
| D3 | done | AC2, AC6 (partial), AC7 (partial), AC8, AC10 | whole-solution build 0/0; core tests 3956 pass |
| D4 | done | AC2 (independent consumer proof), AC3, AC5, AC7, AC10 | whole-solution build 0/0; qc tests 63 pass |
| D5b | done | AC1, AC2, AC4, AC5, AC6, AC8, AC10 | whole-solution build 0/0; ext tests 66 pass; core tests 3903 pass |
| D5c | done | AC2, AC3, AC4, AC5, AC6, AC7, AC8, AC10 | whole-solution build 0/0; ext tests 76 pass |
| D5c-1 | done | AC3, AC4, AC5, AC10 | whole-solution build 0/0; ext tests 88 pass (12 inference tests) |
| D5c-2 | done | AC3, AC4, AC5, AC7, AC10 | whole-solution build 0/0; ext tests 99 pass (11 validation tests) |
| D5c-2b | done | AC4, AC5, AC7, AC10 | whole-solution build 0/0; ext tests 107 pass (8 added validation tests) |
| D5c-2c | done | AC4, AC5, AC10 | whole-solution build 0/0; ext tests 112 pass (5 subquery rejection tests) |
| D5c-3 | done | AC7, AC8, AC10 | whole-solution build 0/0; ext tests 117 pass (5 composition tests) |
| D5c-4 | done | AC6, AC9, AC10 | whole-solution build 0/0; ext tests 123 pass (6 cache/lifecycle tests) |
| D5c-4b | done | AC6, AC9, AC10 | whole-solution build 0/0; ext tests 127 pass (4 cache/lifecycle tests) |
| D5d | done | AC4, AC5, AC10 | whole-solution build 0/0; 6 providers x 8 tests pass |
| D5e-1 | done | AC3, AC4, AC5, AC10 | whole-solution build 0/0; 4 providers x 7 tests pass (28 total) |
| D5e-2 | done | AC3, AC4, AC5, AC10 | whole-solution build 0/0; 6 providers x 7 tests pass (42 total); ClickHouse int-constant capability limitation noted |
| D6 | superseded → D6a/D6b/D6c | AC9 (partial) | single arm/phase was insufficient; superseded by D6a–D6c |
| D6a | done | AC9 (partial) | full benchmark harness authored in both trees (7 phases); both build 0/0; BDN not yet run |
| D6b | done | AC9 (partial) | 3 benchmark methods added to both twins (10 total); both build 0/0; BDN not yet run |
| D6c | done | AC9 (partial) | A/A baseline: 4 runs (2 short + 2 full); noise-bounds.json + README.md; 3 methods inconclusive |
| D7b | done | AC9 (partial) | Matched comparison (in-process, both trees): Warm_RawHit regression (+24.6% time, +19.5% alloc); all alloc increased; comparison-matched.json + README.md |
| D8 | done | AC9 (partial) | Hot-path cleanup: ShapeState cached; all methods within noise for time; alloc increases persist (9-23.5%); comparison-matched2.json |
| D8b | done | AC9 (partial) | Allocation accounting: A/A delta 0 bytes; all methods real (+543 to +2693 bytes); attribution documented; allocation-accounting.json |
| D8c | done | AC9 (partial) | Warm-hit parity confirmed (bodies identical); phase attribution: raw-shape ComponentShape[] allocation on warm hit (~0.9us); no product edits |
| D8d | done | AC9 (partial) | ShapeState cached (boxed); raw-shape ComponentShape[] still allocates per capture; Warm_RawHit +23.5% persists; irreducible cost ~0.9us + ~788 bytes |
| D8e | done | AC9 (partial) | CaptureRawShape runs exactly once per warm hit; ConditionalWeakTable cache added (per-instance); irreducible cost remains ~0.9us + ~788 bytes — contract conflict for CHECK/PLAN |
| D8f | done | AC9 (partial) | Shape selectors deduplicated (projection removed from ShapeSelectors); Warm_RawHit still +23.5% time +19.5% alloc; irreducible cost is ComponentShape[] array per capture |
| D8g | done | AC9 (partial) | CaptureRawShape runs exactly once per warm hit; ConditionalWeakTable cache removed (no benefit for fresh instances); irreducible cost ~0.9us + ~788 bytes — contract conflict for CHECK/PLAN |
| D9 | done | AC8, AC10 | Docs EN+RU updated (guide/05, guide/12, relationships/collection-projection); no specs/superpowers links; DocFX not installed (blocker) |
| D10a | done | AC11 | Full suite RC=2: 3 failures (2 constant-projection parameter rebind + 1 warm-set-op allocation); container providers executed | 
| D10b | done | AC3, AC4, AC8, AC11 | Fixed constant-projection parameter rebinding; both residual tests pass; warm-set-op allocation ceiling passes; build 0/0 |
| D10c | done | AC11 | Whole-solution coverage gate: line 89.3% (>=85), branch 83.7% (>=75); both rc=0; ~10m 25s |
| D10d | invalid | AC11 | integration coverage run had 4015 container connection failures; artifacts isolated in invalid-broken-env/; superseded by D10g |
| D10e | done | AC11 | D10d integration1 RC=2 classified: 4015 container connection failures (all providers) — environmental (coverage profiler run); no code regression; sample passes on re-run |
| D10f | done | AC11 | Coverage gap analysis: nextorm.extensions line 65.1%/branch 41.9%; even at 100% ext branch aggregate = 73.1% (<75%); systemic finding; no code changes |
| D10g | done | AC11 | Clean integration coverage: pre-warm 0 failed; integration1 8461/0/197, integration2 118/0/0; merged line 81% (>=65), branch 75.3% (>=75) PASS; invalid run moved to invalid-broken-env/ |
| P1 | done | AC9 (attempt 2) | per-site allocation attribution (probe, main+baseline); clone count = 1; shape state now static references; floor = component instance + clone + Components[] + ComponentShape[]; per-site-allocation.md |
| P2a | done | AC9 (attempt 2) | four raw runs (B1/B2/C1/C2) 10/10, 0 fail; SQL parity confirmed; per-method tables in perf-final/TABLES.md; no verdicts |
| P2b | done | AC9 (attempt 2) | verdict table (4 time regressions, 10 alloc regressions); no-component controls GroupBy+extrema identical alloc; VERDICT.md |
| P3 | done | AC1, AC2 | semantic audit: Core clean, IVT clean, public mechanism documented, external consumer Core-only; generator still has "SelectGroup"/"SelectGroupHaving" string literals (F2 contradiction) |
| P3b | done | AC1, AC2 | generator operator-name special-case removed: return-type rule (concrete unwrapped type) + projection-lambda fallback; no "SelectGroup"/"SelectGroupHaving"/"Select" literal left; alias tests 176/0/176/0; build 0/0; audit updated |
| P4 | done | AC3–AC8 | zero-inference proof: internal thread-static counter + IVT to tests only; 3 new tests — warm raw hit = 1 inference total/0 on hit for both operators, captured value = 2 (not a raw hit by design, finding); oracle map: AC5/AC8 complete, AC3/AC4/AC6/AC7 gaps; ext tests 130 total/0 failed/130 passed; build 0/0 |
| P5a | done | AC6 | captured closures are raw-cache hits: RawShape carries closure identities (reference-compared), captured QueryCommand still ineligible; publish gate `CanPublishCapturedRecipe` (every refresh parameter has CapturedKey); staged apply now carries EffectiveHaving/Grouping state (was lost); 3 ext tests: same closure 1 inference total/0 on hit + fresh rows, different member = 2 (no false hit); ext 132/0; core 3903/1 (old policy expectation) |
| P5b | done | AC6 | core test rewritten to the new policy (publish 1, reuse 1, parameter 99 fresh, same prepared instance) + guard test: folded parameter without CapturedKey is not published (publishes 0, reuse 0, value 51); core 3904/0/3904/0; sqlite 2307 (1 skip) rc=0; postgres 1946 rc=0; build 0/0; footprint extended (QueryPlanner.cs, CachedPreparationRecipeTests.cs); manifest gates rc=0 |
| T1 | done | AC3 | live oracles replaced by exact values on 6 providers via shared `SelectGroupLiveOracles` (keyed max, aggregate HAVING, source-HAVING AND, global pass/fail, constant non-empty + empty, NULL key, ORDER BY+LIMIT); dead `SelectGroupLiveTests` deleted; vacuous global-aggregate unit test fixed + real empty-input constant test; 6 × 7 live tests pass, 0 skipped; ext unit 133/0; build 0/0 |
| T2 | done | AC4 | exact per-dialect SQL oracles: shared base 8 facts × 6 dialects (full string, three-key order, HAVING, constant WHERE+DISTINCT, parameter names/values, converter `ACTIVE`, quoted `order`, nullable key, ORDER BY); parity removed: 4-5 facts in each `T323SelectGroupSqlTests` + `CompareCase` baseline in all 6 `T323LegacySqlBaselineTests`; provider projects 2307/1946/2084/1215/1058/1505 all 0 failed; core 3904/0; build 0/0; CRLF clean |
| T3 | done | AC6, AC7 | AC6: raw-shape matrix (same shape+closure = one command/one inference; different literal/condition/operator/closure member = distinct keys), reset/re-prepare predicate+distinct once; AC7: behavioural slot replacement (second keys, one GROUP BY), concrete `BuildSqlCommandException` + slot-naming messages both orders for both conflict families, exact additive WHERE with entity filter + user Where + component predicate counted once (placeholder `$min` once), DISTINCT monotonic 3 ways, five explicit-modifier facts; ext unit 145/0; build 0/0 |
| R1 | done | AC10, AC11 | original agreement archived verbatim (`rv1/original-task/issue-362.md`: issue body + #issuecomment-6654133696, README); EN↔RU content audit: package/namespace, both generic args + breaking change, SelectGroupHaving semantics, three outcomes, in-memory NotSupportedException, guide/12 + collection-projection all match — one symmetric gap found and fixed (third-party `IQueryComponent` extensibility pointer); published docs link to specs/superpowers = 0; foreign four files untouched (pre-existing diff); Release build 0/0; CRLF 0 offenders (6 pre-existing LF files fixed); CPM 0 inline versions; no add/commit (HEAD f991d5ea); `rv1/audit/docs-hygiene.md` |
| R3 | done | process | defect history rebuilt with five stable keys (`perf-residual-warm-hit`, `captured-not-rawhit`, `effective-state-lost-in-staged-apply`, `coverage-invalid-env`, `generator-name-specialcase`), each with symptom/units/fixes/outcome/evidence; pre-clean coverage claim marked superseded in `unit-evidence.md` and `gap-analysis.md` (clean D10g: line 81 %, branch 75.3 %); process-defect spec `docs/specs/design/D362-pdca-process-defects.md` (PD1–PD5); no product/test/threshold change; footprint + gates refreshed |
| R2a | blocked | AC11 | final suite on the current tree: 23385 total / 23176 passed / **3 failed** / 206 skipped, rc=2 — the 3 failures are stale pre-P5a raw-shape expectations (defect `stale-rawshape-oracles`, not fixed in this unit); container providers all executed (skips are the pinned capability set); whole coverage 89.3 % line / 83.7 % branch PASS (42264/50437); exclusions audit: 0 new `[ExcludeFromCodeCoverage]`, whole-gate settings unchanged |
| R2b | done | AC11 | three stale raw-shape oracles rewritten to the P5a policy without weakening: closure identity keys the plan and rebinds live rows (same instance one plan; fresh instance distinct key), literal vs closure distinct keys, folded parameter without `CapturedKey` not published, cross-join graph capturable and rebinding 8→12 rows; two classes 27/0; whole integration project 8658/0/8461/197 rc=0; Debug build 0/0 |
| R2c | done | AC11 | clean whole-solution evidence on the fixed tree: suite 23385 total / **0 failed** / 23179 passed / 206 skipped, rc=0 (7 m 34 s); coverage run same counts, rc=0 (6 m 59 s); line **89.3 %** (73222/81977) / branch **83.8 %** (42280/50437) PASS at 85/75; per module core 89.0/84.3, postgres 95.6/78.4, sqlite 94.3/80.9, sqlserver 96.6/81.9; previous `final-whole` marked superseded; no file change, tree unchanged |
| R2d | done | AC11 | integration coverage re-measured on the current tree: pre-warm 8658/0/8461/197 rc=0; integration1 8658/0/8461/197 rc=0 (267.9 s), integration2 118/0/118/0 rc=0 (19.1 s); merged report 9 modules / 790 classes; **line 81 %** (67080/82751) / **branch 75.3 %** (40150/53276) PASS at 65/75; extensions reported separately 61.7/38.5; merge audit: 66 `NextORM.Core.InMemory*` classes filtered by the pre-existing classfilter, 0 remain, no other class dropped, no new exclusion; previous `coverage/integration/` superseded |
| R2e | done | AC11 | whole-solution gates re-established on the post-PF1b/PF3 tree: build 0 warnings/0 errors rc=0; suite **23404 total / 0 failed / 23198 passed / 206 skipped**, rc=0 (7 m 15 s), container providers executed; coverage run same counts rc=0 (7 m 33 s); reportgenerator rc=0; **line 89.3 %** (73257/82008) / **branch 83.8 %** (42291/50445) PASS at 85/75; per module core 89.0/84.3, postgres 95.6/78.4, sqlite 94.3/80.9, sqlserver 96.6/81.9; `final-whole-clean` (R2c) marked superseded; no product/test/threshold change, tree unchanged |
| R2f | done | AC11 | integration coverage gate re-measured on the post-PF1b/PF3 tree: pre-warm 8658/0/8461/197 rc=0 (6 m 19.5 s); integration1 coverage 8658/0/8461/197 rc=0 (227.5 s); integration2 (extensions) 130/0/130/0 rc=0 (18.6 s); merged **line 81 %** (67110/82785) / **branch 75.3 %** (40151/53284) PASS at 65/75; per module core 80.8/76.6, extensions 61.8/38.6, clickhouse 87.5/74.1, efcore 72.6/67.1, mariadb 84.1/74.3, mysql 80.0/72.7, postgres 92.4/73.6, sqlite 92.4/79.9, sqlserver 88.1/76.3; audit: 9 modules / 790 entries, 66 InMemory classes filtered (0 remain), 598/598 user classes present, 0 new exclusions; earlier attempts blocked by a foreign cycle on the same Podman machine (logged); R2d `final-integration` superseded; no product/test/threshold change |
| C1 | done | AC3 | live exact outcomes for **both** operators: `SelectGroupHaving` keyed (existing, HAVING count>1 → (null,2),(a,4),(b,3)), global (one row count=11, HAVING >10/>11 pass/fail) and constant (one row "constant", empty → 0) added to the shared oracle base + MariaDB; `SelectGroup` keyed/global/constant already exact; 6 providers × 9/9, project 130/0/130/0, build 0/0; AC3 matrix in oracle-map |
| C2 | done | AC6 | positional warm-hit freshness for both operators (`SqlFunctions.Parameter<int>(0)`): one inference total, 0 on the raw hit, rows rebound (stale set not observed); `CloneForCache_PreservesShape` replaced by behavioural `..._PreservesShapeAndRebinds` (identical SQL, one GROUP BY/HAVING, no DISTINCT, clone rebinds the changed capture, one inference, constant clone keeps one DISTINCT); `CapturedValue_Rebinds` kept; ext unit 147/0; sqlite raw 195/0; core raw 47/0; build 0/0; oracle-map AC6 complete |
| PF1 | done (author only) | AC4, AC9 | protocol v2: counterbalanced A-B-B-A (operator + control sets, 8 runs), SDK `10.0.401` recorded verbatim, ShortRun (launch 1/warmup 3/iterations 3), per-metric paired formula with A/A bands incl. allocations and control-specific bands; 9 new control arms (GroupBy + extrema, own fixtures/benchmarks) added to candidate and baseline twin, 19 arms discoverable in both; `run.sh` produces 8 runs + full-matrix parity (`verify.sql`, 9 scenarios); pre-check parity found `having-combination-duplicated` (open, not fixed); builds: candidate/baseline solution 0/0, both benchmark projects Release 0/0; no measurement run |
| PF1b | done | AC4, AC7, AC9 | HAVING duplication fixed in one place: Replace contributions carry only the added condition, engine ANDs raw Having once (null-aware); Clear keeps the full replacement (constant WHERE); 5 new composition facts (SelectGroup/Having, SelectGroupHaving two predicates/params, global, constant WHERE+DISTINCT, plain silent drop); ext unit 152/0, live suite 130/0, sqlite 24/0, core 3904/0, build 0/0; parity dump identical |
| PF2 | done (measurement) | AC9 | final counterbalanced A-B-B-A (8 runs, 19 arms each, 0 failures, SDK 10.0.401, parity diff empty); operator: 6 time regressions (Construct +9.0/Warm_RawHit +15.0/Prepared_FallbackHit +14.4/Prepare_Cold +6.5/Execute_ConstantProjection +5.4/Execute_PositionalRebind +2.1) and 10/10 allocation regressions (+9.5…+22.9); controls: time within noise, **all 9 allocation arms +31…+41 B, band 0 → regression** (U6 finding); AC9 not met; verdict in `perf-final2/VERDICT.md` + `verdict.json` |
| PF3 | done | AC9 | control allocation overhead removed: closure scan is thread-static/reusable, lazy list, null closures (exact probe: capture 4848→4752 B, exact arm 5264→5232 B candidate vs baseline); control re-run 6 runs × 9 arms, 0 failures; 3+3-run A/A band: all control allocation deltas within band (GroupBy +0.78…+1.09 %, extrema negative) and time within noise; ext unit 152/0, core 3904/0, raw-shape classes 27/0, build 0/0 |

D1 changes: ExclusiveGrouping contribution kind; QueryPreparationHavingMode; QueryPreparationContribution; IQueryPreparationDescriptor; IQueryPreparationContext + QueryPreparationContext.
D2 changes: EffectiveGroupingKeys/HavingMode/Having/SourcePredicate/RequireDistinct fields on QueryCommand with Clone/CloneForCache/ResetPreparation handling; population from IQueryPreparationDescriptor contributions during Prepare (ExclusiveGrouping slot ownership + conflict with ExclusiveRowSource); CombinePredicates helper; public AggregateClassification service (wraps SelectGroupExpressionAnalyzer.IsAggregateCall); public SemanticExpressionEqualityComparer. Tests: D362PreparationContributionTests (2), D362Unit2Tests (5).

## CHECK record

### r1, n=1 — not accepted (CHECK → DO)

Verdict: **not accepted**. Findings (stable keys): **F1** (allocation delta is real against a measured 0 B/op A/A band — AC9 allocation claim fails), **F2** (perf artifacts use inconsistent noise datasets: three different numbers for the same method — evidence inconsistency), **F3–F5** (recorded as returned; the full CHECK report text was not included in the dispatch, only the verdict and finding keys). Infeasibility claim marked unverified (not accepted as a waiver). Routing: implementation defects → DO (attempt n=2).

### r1, n=2 — not accepted (CHECK #2 → DO), recorded as returned in the dispatch

Verdict: **not accepted**. The dispatch named findings **F1/F2/F3** plus **AC3** and **AC6** findings; the
full CHECK report text was not included (only the keys and the AC-level summary), so it is recorded as
returned and not reconstructed:

- **F1/F2/F3** — returned as keys only (detail not included in the dispatch).
- **AC3** — the live suites do not demonstrate all three outcomes for **each** operator with exact values:
  `SelectGroupHaving` lacked live keyed/global/constant oracles (the existing keyed case was aggregate-HAVING
  only, and global/constant existed only for `SelectGroup`). Routed to DO unit **C1**.
- **AC6** — the AC6 oracle set still contains weak/vacuous entries that T3 kept weak on purpose:
  `CloneForCache_PreservesShape` asserts only `NotBeNull`, `CapturedValue_Rebinds` only
  `NotBeNullOrEmpty`. Recorded as a finding; not relaxed or deleted.

Routing: implementation/evidence → DO, attempt **n=3** (last attempt before escalation).

### r1, n=3/3 — not accepted (CHECK → ACT: incomplete / STOP)

Verdict: **not accepted**; the originating CHECK ends the cycle as **ACT: incomplete / STOP**. The complete
CHECK report text was not included in the dispatch; the outcome is recorded as returned.

Matrix: **AC1–AC8, AC10, AC11 — met**; **AC9 — unmet**; **U6 — unmet**. No other U status was returned.

Findings and grounds:
- **AC9 / U6 unmet.** PF2 measured **6 time regressions** (`Warm_RawHit` **+15.04 %** against its
  **1.99 %** A/A band; `Prepared_FallbackHit` +14.4 %, `Prepare_Cold` +6.5 %, `Construct` +9.0 %,
  `Execute_ConstantProjection` +5.4 %, `Execute_PositionalRebind` +2.1 %) and **10/10 allocation
  regressions** (+9.5 %…+22.9 %), all outside their own A/A bands. The post-PF3 **operator matrix was not
  re-measured**, so final operator parity after the control fix is **unverified**.
- CHECK **did not confirm a criteria defect**: the ≥472 B floor is not established as a universal lower
  bound, so there is **no PLAN transition** and **no `original task is infeasible`** record.
- **No fourth DO attempt and no second evidence round are authorized** (no-4th rule; attempts n=3/3).
- The mandatory escalation took place; its conclusion **classification (ii)** is recorded but **not adopted
  by CHECK**.

Evidence pointers: `rv1/perf-final2/VERDICT.md`, `rv1/perf-final2/verdict.json`,
`rv1/perf-final2/controls-after-fix/README.md`, `rv1/audit/oracle-map.md`,
`rv1/coverage/final-whole-pf/report/Summary.txt`,
`rv1/coverage/final-integration-pf/report/Summary.txt`, `.pdca/status/D362-selectgroup-extensions-1.md`.

## ACT record

**Outcome: `incomplete / STOP`** — not `PASS`, not `done`. Finalized 2026-11-06T00:03:53Z.

Reason: the originating CHECK (r=1, n=3/3) returned **not accepted** and ended the cycle in ACT:
**AC9/U6 unmet** — the PF2 counterbalanced operator matrix measured 6 time regressions (`Warm_RawHit`
+15.04 % vs a 1.99 % A/A band; five further methods +2.1…+14.4 %) and 10/10 allocation regressions
(+9.5…+22.9 %), all outside their own A/A bands; the post-PF3 operator matrix was not re-measured, so
final operator parity after the control-allocation fix is **unverified**. CHECK did not confirm a
criteria defect (the ≥472 B floor is not established as a universal lower bound), so there was no
PLAN transition and no `original task is infeasible` record. No fourth DO attempt and no second
evidence round are authorized.

Delivered and assessed `met`: **AC1–AC8, AC10, AC11** (AC11: whole suite **23 404 total / 0 failed /
23 198 passed / 206 skipped**, rc=0; whole coverage **line 89.3 % (73 257/82 008) / branch 83.8 %
(42 291/50 445)** PASS at 85/75; integration **8 658 / 0 / 8 461 / 197** rc=0, merged **line 81 %
(67 110/82 785) / branch 75.3 % (40 151/53 284)** PASS at 65/75, extensions 61.8/38.6 reported
separately).

Residual:
- **`unmet` — AC9** (time and allocation parity vs baseline `f991d5ea` on operator scenarios) and **U6** —
  **accepted by the user on 2026-11-06 for the delivered behaviour**; the optimization/measurement
  requirement is now tracked in **#368**.
- **`unverified` — post-PF3 operator parity**: the final operator matrix was not re-measured; only the
  control arms were re-run after PF3 (all within their representative A/A bands). This remains unmeasured
  in this cycle; #368 requires the same counterbalanced protocol.

Evidence root: `.pdca/D362-selectgroup-extensions/rv1/` (`perf-final2/`, `coverage/final-whole-pf/`,
`coverage/final-integration-pf/`, `audit/oracle-map.md`, `pf3/`).

No further DO or CHECK runs in this cycle. The counters remain `N=1, r=1, n=3/3`; the status file and all
evidence are retained. A further cycle is a **separate authorized PLAN**, not an automatic reopening.

**User decision (explicit instruction, 2026-11-06T04:31:16Z):** the user accepted the measured component-operator
overhead — the "accept the quantified overhead as a new instruction" option — so the delivered behaviour
stands. U6/AC9 for follow-up work are now governed by that acceptance, not by the CHECK finding; the
optimization is tracked in **#368** (https://github.com/AlexeyShirshov/nextorm/issues/368), opened in milestone `1.0.9-rc5` after the
mandatory duplicate search. No product/test/doc edit was made; no new DO/CHECK; #362 was not closed.

## Revision history

none

## Defect history

_Historical note:_ this section previously read `none`; that value is **superseded** by the entries below
(it was inaccurate — repeated fixes had been applied without being recorded).

- **`perf-residual-warm-hit`**
  Symptom: the raw-recipe path costs ~0.9 µs and ~788 B more than the baseline per warm hit, so AC9's
  component scenarios regress in both time and allocations. Final PF2 counterbalanced numbers:
  `Warm_RawHit` +15.0 %, `Prepared_FallbackHit` +14.4 %, `Prepare_Cold` +6.5 %, `Construct` +9.0 %,
  `Execute_ConstantProjection` +5.4 %, `Execute_PositionalRebind` +2.1 %; allocations 10/10
  (+9.5 %…+22.9 %). First seen: D8. Units/attempts: **7 DO units D8–D8g** (`D8`, `D8b`–`D8g`). Applied
  fixes: 4 (cache `ShapeState`, deduplicate shape selectors, per-instance `ConditionalWeakTable` cache,
  remove the same cache as ineffective) — none removed the floor. Last outcome: unresolved at the time of
  CHECK #1 (findings F1/F2); now measured and explained as the component-mechanism floor (component
  instance + builder clone + `Components[]`/`ComponentShape[]`), AC9 remains **open**. Evidence: status
  rows D8–D8g (lines 239–245), `rv1/perf-final/VERDICT.md`, `rv1/perf-candidate/per-site-allocation.md`.
  Final CHECK r1 n=3/3: **not accepted** — AC9/U6 unmet; the mandatory escalation's classification
  (ii) was recorded and **not adopted** by CHECK; no fourth attempt authorized. Evidence:
  `rv1/perf-final2/VERDICT.md`, this status file §CHECK record.
  User decision 2026-11-06: the measured overhead is **accepted** for the delivered behaviour; the
  optimization follow-up is **#368**.
- **`captured-not-rawhit`** (alias: `P4-CAPTURED-NOT-RAWHIT`, driver label `plateau`)
  Symptom: a query whose condition captures a local produced **2** inference runs — no raw-cache hit —
  because `CaptureRawShape` rejected every closure-bearing graph. First seen: P4. Units/attempts:
  P4 (found), P5a/P5b (fixed). Applied fixes: `RawShape.Closures` (reference identity, compared in
  `Matches`, hashed in `Fingerprint`); closure scan over Exp/Condition/Group/Having/PreWhere/
  PerParentPartition/ArrayJoins/Sorting/JoinCondition/Windows/component selectors with captured
  `QueryCommand` still ineligible; publication gated by `QueryPlanner.CanPublishCapturedRecipe`
  (every refresh parameter must carry `Parameter.CapturedKey`). Last outcome: **resolved** — changed
  captured value in the same closure = 1 inference total / 0 on the hit with rebound rows; a different
  captured member = 2 (no false hit). Evidence: `tests/nextorm.extensions.tests/D362CacheLifecycleBTests.cs:169,204,236`,
  `tests/nextorm.core.tests/Caching/CachedPreparationRecipeTests.cs:632,665`, `rv1/p5/*.log`,
  `rv1/audit/oracle-map.md` (AC6).
- **`effective-state-lost-in-staged-apply`**
  Symptom: on a captured-value raw hit the HAVING/grouping effective state was missing from the staged
  command, so parameter extraction produced 0 parameters against 1 cached parameter and the stale captured
  value was served. First seen: P5a (while fixing `captured-not-rawhit`). Units/attempts: P5a. Applied
  fix: `QueryCommand.Clone.cs` now carries `EffectiveHavingMode`/`EffectiveHaving`/`EffectiveGroupingKeys`/
  `EffectiveSourcePredicate`/`EffectiveRequireDistinct` through `CopyTo` (lines 108–112). Last outcome:
  **resolved** — `SelectGroupHaving` captured key `"a"` → `"b"` rebinds on the raw hit. Evidence:
  `src/nextorm.core/Query/QueryCommand.Clone.cs:108`, `tests/nextorm.extensions.tests/D362CacheLifecycleBTests.cs:204`,
  `rv1/p5/extensions-tests.log`.
- **`coverage-invalid-env`**
  Symptom: the D10d integration coverage run recorded 4015 container connection failures across every
  provider and produced a bogus aggregate **branch 71.5 %** figure, from which a "75 % gate is systemic"
  conclusion (D10f) was drawn. First seen: D10d. Units/attempts: D10d (run), D10e (classification),
  D10f (gap analysis), D10g (clean re-measure). Applied fixes: none to code; the invalid artifacts were
  isolated in `invalid-broken-env/` and the clean D10g run replaced the numbers. Last outcome:
  **superseded** — clean merged result line **81 %**, branch **75.3 % (40041/53166)**, both PASS. Evidence:
  status rows D10d/D10e/D10f/D10g (lines 250–253), `rv1/coverage/integration/invalid-broken-env/`,
  `rv1/coverage/integration/report/Summary.txt`, supersession banners in `rv1/unit-evidence.md` and
  `rv1/coverage/integration/gap-analysis.md`.
- **`generator-name-specialcase`**
  Symptom: the join-alias source generator matched the operator names `"SelectGroup"`/`"SelectGroupHaving"`
  (and `"Select"`) as string literals; the check was removed in D5b and then re-added to make
  `AliasedChainCteGeneratorTests` pass. First seen: D5b (reintroduced). Units/attempts: P3/P3b. Applied
  fix: name-free rule in `ResolveQueryElementType` — concrete unwrapped return type first, then the first
  single-parameter non-boolean projection lambda; join operators excluded; no operator-name literal
  remains. Last outcome: **resolved** — `grep SelectGroup src/nextorm.core.sourcegenerator` empty; alias
  tests 176/0/176/0. Evidence: `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs:450-521`,
  `rv1/audit/semantic-audit.md`, `rv1/p3b/generator-grep.txt`, status row P3b.

- **`stale-rawshape-oracles`** (reported R2a; **resolved R2b**)
  Symptom: after the approved P5a policy made closure-bearing raw shapes eligible (identity-keyed),
  three integration tests still asserted the old "closure/cross-join ⇒ `CaptureRawShape` is `null`"
  premise, so the full suite was red. First seen: R2a. Units/attempts: R2a (reported), R2b (fixed).
  Applied fixes (3):
  `RawShape_ClosureValue_ShouldBeIneligibleAndRebindLiveRows` → `RawShape_ClosureIdentity_ShouldKeyThePlanAndRebindLiveRows`
  (same closure instance = one key and one prepared plan, rows rebind 6..10 → 9..10; a fresh instance is a
  distinct key with correct rows); `RawShape_LiteralVersusClosure_ShouldDifferInEligibilityOnly` →
  `RawShape_LiteralVersusClosure_ShouldBeDistinctKeys` (both capturable; literal vs closure are different
  keys; both read the same rows; a folded parameter without `CapturedKey` is not published — checked with
  `QueryPlanStore.TryGetRaw`); `CrossJoinBattery_LeftConditionArm_ShouldCompareAndReadLiveRows` now proves
  the cross-join graph itself is capturable (the old null came from the captured `maxId`, not the join
  kind), the same closure rebinds 8 → 12 rows into one prepared plan, and a fresh instance is a distinct
  key with correct rows. Every test carries a comment naming the P5a policy.
  Last outcome: **resolved** — the two classes are 27/0/27/0, the whole integration project is
  **8658 total / 0 failed / 8461 passed / 197 skipped**, rc=0; Debug build 0/0.
  Evidence: `rv1/r2b/rawshape-classes.log`, `rv1/r2b/integration.log`, `rv1/r2b/build.log`,
  `tests/nextorm.integration.tests/Sqlite/RawShapeBranchCoverage2Tests.cs`,
  `tests/nextorm.integration.tests/Sqlite/PreparerBatteryBranchCoverage4Tests.cs`.

- **`check2-f1` / `check2-f2` / `check2-f3`** (CHECK #2, r1 n=2, recorded as returned)
  Symptom/root cause/effect: not included in the dispatch beyond the stable keys; recorded as returned,
  not reconstructed. Last outcome: **as returned** (no DO action beyond the AC3/AC6 items below were named).
  Evidence: `## CHECK record` §r1 n=2.
- **`check2-ac3-live-outcomes`** (CHECK #2; **resolved C1**)
  Symptom: AC3 requires the three outcomes on live DBs for both operators, but the live suites demonstrated
  them mostly through `SelectGroup`; `SelectGroupHaving` had only the keyed aggregate-HAVING case. Applied
  fix: `SelectGroupLiveOracles.SelectGroupHavingGlobalAggregation` (one row, `count(Id)=11`, HAVING `>10`
  keeps it / `>11` returns zero rows) and `SelectGroupHavingConstantProjection` (one row `"constant"`,
  `Where(Id < 0)` → zero rows, condition lowered into the pre-group WHERE with DISTINCT), wired into the
  shared base and MariaDB; the existing keyed helper is the **keyed** cell; `SelectGroup`'s three cells were
  already exact. Last outcome: **resolved** — 6 providers × 9/9, 130/0/130/0 rc=0.
  Evidence: `rv1/audit/oracle-map.md` C1 matrix, `rv1/c1/<Provider>.log`, `rv1/c1/integration.log`.
- **`check2-ac6-weak-oracles`** (CHECK #2; **resolved C2**)
  Symptom: two AC6 oracles were intentionally weak (`CloneForCache_PreservesShape` only `NotBeNull`,
  `CapturedValue_Rebinds` only `NotBeNullOrEmpty`), and no direct positional warm-hit freshness proof existed.
  Applied fixes: `CloneForCache_PreservesShape` → `CloneForCache_PreservesShapeAndRebinds` (behavioural:
  identical SQL, one GROUP BY, one HAVING, no DISTINCT; clone execution rebinds the changed captured value;
  inference stays 1; a cloned constant projection keeps one DISTINCT and zero rows); two positional tests
  (`SelectGroup_PositionalWarmHit_RebindsRows_OneInference`,
  `SelectGroupHaving_PositionalWarmHit_RebindsRows_OneInference`) prove a warm raw hit with
  `SqlFunctions.Parameter<int>(0)`, one inference total, zero on the second, and the new rows observed (the
  stale value cannot be). `CapturedValue_Rebinds` is kept unchanged. Last outcome: **resolved** — extensions
  unit 147/0/147/0; sqlite PlanCache/Raw/SelectGroup 195/0; core RawShape/CachedPreparationRecipe/ParamRefresh
  47/0; Debug build 0/0. Evidence: `rv1/audit/oracle-map.md` AC6, `rv1/c2/*.log`.

- `src/nextorm.core.sourcegenerator/JoinAliasGenerator.cs`
- `src/nextorm.core/Builders/EntityBuilder.cs`
- `src/nextorm.core/DataContext/InMemoryGrouping.cs`, `InMemoryQueryBuilder.cs`, `SqlBuilder.cs`, `QueryPlanner.cs`
- `src/nextorm.core/Query/QueryCommand.Clone.cs`, `QueryCommand.QueryPreparer.cs`, `QueryCommand.cs`, `QueryComponents.cs`, `QueryDefinition.cs`, `QueryPlanEqualityComparer.cs`
- `src/nextorm.core/Visitors/ScalarFunctionTranslator.cs`
- `src/nextorm.extensions/ExtremeRowExtensions.cs`

**src/** (new): `src/nextorm.core/Query/QueryPreparationContext.cs`, `src/nextorm.core/Visitors/AggregateClassification.cs`, `src/nextorm.core/Visitors/SemanticExpressionEqualityComparer.cs`, `src/nextorm.extensions/SelectGroupComponent.cs`, `src/nextorm.extensions/SelectGroupExpressionAnalyzer.cs`

**src/** (deleted): `src/nextorm.core/Visitors/SelectGroupExpressionAnalyzer.cs`

**tests/** (modified): alias, clickhouse, core.tests csproj, provider exact-SQL suites (shared base + 6 `QueryComponents/SelectGroupSqlGenerationTests.cs`), 6 `SqlGeneration/T323LegacySqlBaselineTests.cs`, 6 `SqlGeneration/T323SelectGroupSqlTests.cs`, core.tests caching (`Caching/CachedPreparationRecipeTests.cs` — P5b policy rewrite + guard), integration (SqliteStep3SqlBuilderResidualTests, SqliteStep5ProbeTests, csproj), mariadb, mysql, postgres, sqlite (RawRecipeComponentIsolationTests, T323SelectGroupSqlTests, csproj), sqlserver (T323SelectGroupSqlTests, csproj).

**tests/** (new): D362 core unit tests (3), D362 extensions tests (7 files incl. inference/validation/composition/cache/lifecycle), `nextorm.querycomponents/ExternalGrouping.cs` + tests, `nextorm.extensions.provider.shared/SelectGroupSqlGenerationTestsBase.cs`, 6 provider SelectGroupSqlGenerationTests, `nextorm.extensions.integration.tests/SelectGroupLiveTestsBase.cs` + `SelectGroupLiveOracles.cs` (shared exact-value oracles) + 5 provider suites + `MariaDbSelectGroupLiveTests.cs` (the duplicate abstract `SelectGroupLiveTests.cs` was deleted).

**tests/** (deleted): core T323SelectGroup{Cache,Classification,InMemory,Inference,Validation}Tests.cs; integration CommonTestSuite.SelectGroup.cs, C13, C14, T323SelectGroupPostgresSpecificTests.cs; postgres PlanCache/T323SelectGroupPlanCacheTests.cs.

**docs/specs/** (new): `docs/specs/design/D362-pdca-process-defects.md` (internal process-defect spec, not public).

**docs/** (modified): `docs/guide/05-grouping-and-aggregates.md`, `docs/guide/12-user-defined-functions.md`, `docs/relationships/collection-projection.md`, `docs/ru/guide/05-grouping-and-aggregates.md`, `docs/ru/guide/12-user-defined-functions.md`, `docs/ru/relationships/collection-projection.md`.

**examples/** (modified): clickhouse analytics, mssql adventureworks, postgres aviasales (queries + csproj).

**benchmarks/** (new): `benchmarks/nextorm.benchmark/SelectGroup/D362SelectGroupBenchmarks.cs`.

**config/** (modified): `coverage.integration.settings.xml` (added `extensions` module).

**.pdca/** (new): `.pdca/status/D362-selectgroup-extensions-1.md`, `.pdca/D362-selectgroup-extensions/rv1/**`, `.pdca/scripts/` (staged helper copies).

**Excluded / not claimed (foreign or other cycles):** `docs/advanced/extensibility.md`, `docs/advanced/toc.yml`, `docs/ru/advanced/extensibility.md`, `docs/ru/toc.yml`; `.pdca/status/T365-rc5-2.md`, `.pdca/status/collection-365-366.md`; `docs/superpowers/specs/2026-11-05-extensible-selectgroup-design.md` (design spec, not product change); `benchmarks/BenchmarkDotNet.Artifacts/**` (restored).

- **`having-combination-duplicated`** (PF1 found; **resolved PF1b**)
  Symptom: for `.Having(cond).SelectGroup(proj)` the candidate rendered the condition twice in HAVING and
  bound its parameter twice, while the baseline rendered it once (same for the positional placeholder).
  Two sites: (1) `SelectGroupComponent.Prepare` put `cmd.Having AND condition` into the contribution
  (`Replace`), and (2) `QueryCommand.QueryPreparer` combined `cmd.Having` with that contribution again
  (`EffectiveHaving = CombinePredicates(cmd.Having, prep.Having)`). Ownership rule chosen: a **Replace**
  contribution carries only the ADDED condition and the engine ANDs the raw source Having exactly once
  (either side may be absent); a **Clear** contribution carries the full replacement, used as-is (the
  constant projection lowers it into the pre-group WHERE). Applied fix: `SelectGroupComponent` passes
  `_condition` for Keyed/Global (the full `combinedHaving` stays for coverage validation and the mode
  decision; Clear still passes it), and `QueryPreparer` replaces the Replace branch with a null-aware
  single combination. Plain queries are untouched (no grouping owner). Tests: 5 new facts in
  `D362CompositionTests` (SelectGroup + raw Having once; SelectGroupHaving + raw Having two predicates/
  two parameters once each; global aggregate one HAVING; constant + raw Having lowered into WHERE once
  with one DISTINCT; plain query keeps the silent drop). Last outcome: **resolved** — extensions unit
  152/0/152/0; extensions live suite 130/0/130/0 (six providers); sqlite SelectGroup/T323 24/0;
  core 3904/0; build 0/0; parity dump now **identical** (empty `parity.diff`, candidate
  `having (Score > $captured)`/`$norm_p0` once). Evidence: `rv1/pf1b/*.log`,
  `rv1/perf-final2/parity.diff` (empty), `rv1/perf-final2/parity-candidate.txt`.

- **`control-allocation-overhead`** (PF2 found; **resolved PF3**)
  Symptom: no-component control arms allocated +31…+41 B/op in the candidate against a 0 B two-sample band.
  Site: `QueryCommand.QueryPreparer.TryCollectClosureKey` allocated `new ClosureKeyScan()` + its
  `List<object>` on every eligible raw shape; exact probe: `CaptureRawShape` 4848 B baseline vs 4752 B
  candidate after the fix, the per-call List measured at 32 B. Fix: thread-static reusable scanner (reset
  per capture, lazy list, `Snapshot()` returns null without closures), so the no-closure path allocates
  nothing; closure keying semantics unchanged. Re-run: 6 control runs (A-B-B-A + A3/B3), 9 arms each,
  0 failures. With the two-run A/B/B/A band the GroupBy arms still read +1.4…+1.9 % because the candidate
  GroupBy allocation varies 4.88/4.99/5.02 KB between identical runs (KB-rounded report, 0.01 KB ≈ 10 B);
  with the representative 3+3-run band **all control deltas are within their own band**: GroupBy
  +0.78…+1.09 % (band 2.09–2.87 %), extrema negative (band 1.85–2.66 %), time within noise. The exact
  in-process arm (5232 B candidate vs 5264 B baseline, 3 stable rounds) confirms no remaining allocated
  site; the residual is report precision/run-to-run variance, not code. Last outcome: **resolved** — no
  remaining site reproducible. Evidence: `rv1/perf-final2/controls-after-fix/README.md`,
  `controls-after-fix-3x3.json`, `controls-after-fix/*/results/*.csv`, `rv1/pf3/*.log`.

## Changed files

Task-owned artefacts only (full footprint: `rv1/manifest.json`). ACT made no product/test/doc change; ACT
wrote only this status file.

- Core mechanism: `src/nextorm.core/Query/QueryComponents.cs`,
  `src/nextorm.core/Query/QueryPreparationContext.cs`, `src/nextorm.core/Query/QueryCommand.cs`,
  `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `src/nextorm.core/Query/QueryCommand.Clone.cs`,
  `src/nextorm.core/DataContext/SqlBuilder.cs`, `src/nextorm.core/DataContext/QueryPlanner.cs`,
  `src/nextorm.core/Visitors/AggregateClassification.cs`,
  `src/nextorm.core/Visitors/SemanticExpressionEqualityComparer.cs`.
- Operator package: `src/nextorm.extensions/SelectGroupComponent.cs`,
  `src/nextorm.extensions/SelectGroupExpressionAnalyzer.cs`; the Core operator policy was removed.
- Tests: `tests/nextorm.extensions.tests/D362*.cs`,
  `tests/nextorm.extensions.integration.tests/SelectGroupLiveOracles.cs`,
  `tests/nextorm.core.tests/Query/D362*.cs`,
  `tests/nextorm.core.tests/Caching/CachedPreparationRecipeTests.cs`,
  `tests/nextorm.<provider>.tests/QueryComponents/SelectGroupSqlGenerationTests.cs` and `T323*`,
  `tests/nextorm.integration.tests/Sqlite/{RawShapeBranchCoverage2Tests,PreparerBatteryBranchCoverage4Tests}.cs`.
- Docs (EN/RU): `docs/guide/05-grouping-and-aggregates.md`, `docs/guide/12-user-defined-functions.md`,
  `docs/relationships/collection-projection.md` and their `docs/ru/**` mirrors.
- Benchmarks: `benchmarks/nextorm.benchmark/SelectGroup/`.
- Gate/evidence: `coverage.integration.settings.xml` (adds `extensions` to the include list);
  `.pdca/D362-selectgroup-extensions/rv1/**`; `.pdca/status/D362-selectgroup-extensions-1.md`.
- GitHub tracking: **#368** (optimization follow-up, milestone `1.0.9-rc5`).

## Git state

- branch: `1.0.9-rc5`; HEAD: `f991d5ea` (no commits made this cycle).
- status path: `.pdca/status/D362-selectgroup-extensions-1.md` — created this cycle · untracked. Evidence root `.pdca/D362-selectgroup-extensions/rv1/` — untracked. `.pdca/scripts/` — untracked.
- all product/test/doc/example/benchmark output is uncommitted (modified/deleted/untracked per `git status`); nothing staged; no `git add`/`commit`/`push`/`merge`.
- ACT (2026-11-06T00:03:53Z): only `.pdca/status/D362-selectgroup-extensions-1.md` was modified;
  no `git add`/stage/commit/push/merge.
- GitHub (explicit user instruction, 2026-11-06T04:31:16Z): optimization issue **#368** created in milestone
  `1.0.9-rc5`; no other GitHub mutation; #362 not closed.
- foreign uncommitted docs (`docs/advanced/extensibility.md`, `docs/advanced/toc.yml`, `docs/ru/advanced/extensibility.md`, `docs/ru/toc.yml`) and other cycles' `.pdca/status/{T365-rc5-2,collection-365-366}.md` remain untouched and unclaimed.

## Progress log

2026-11-05T09:20:24Z | PLAN | r=1 | n=1 | cycle created; status file initialized from act.md skeleton | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:20:24Z | PLAN | r=1 | n=1 | plan recorded verbatim (pdca-planner) | `.pdca/status/D362-selectgroup-extensions-1.md` §PLAN record r1
2026-11-05T09:20:24Z | PLAN | r=1 | n=1 | awaiting user approval (normal mode); DO not started | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:29:58Z | PLAN | r=1 | n=1 | PLAN approved by user (N=1, r=1); no effort cap | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:29:58Z | PLAN | r=1 | n=1 | DO authorized | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:31:36Z | DO | r=1 | n=1 | DO started | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:34:37Z | DO | r=1 | n=1 | DO could not finish — scope exceeds single-session capacity; no product edit; CHECK not run | `.pdca/status/D362-selectgroup-extensions-1.md` §ACT record
2026-11-05T09:34:37Z | ACT | r=1 | n=1 | DO→ACT incomplete recorded; residual U1–U9/AC1–AC11 unmet | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:35:45Z | DO | r=1 | n=1 | DO continues after driver rejection of an effort-based stop | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T09:50:51Z | DO | r=1 | n=1 | whole-solution build nextorm.slnx Debug = 0 warnings / 0 errors (post-unit-1) | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T10:02:09Z | DO | r=1 | n=1 | unit D2 complete: effective preparation state + aggregate classifier + semantic comparer; whole-solution build 0/0; 7 new tests green | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T10:12:00Z | DO | r=1 | n=1 | unit D3 complete: effective state authoritative (PrepareGrouping/PrepareWhere/SqlBuilder HAVING+DISTINCT+constant); AggregateClassification no longer façade; build 0/0; core tests 3956 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T10:17:56Z | DO | r=1 | n=1 | unit D4 complete: independent-consumer grouping proof (own slot, HAVING AND-ed, zero-key global, no-component control); build 0/0; qc tests 63 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T10:52:11Z | DO | r=1 | n=1 | unit D5b complete: SelectGroup policy removed from Core; call sites migrated; in-memory rejection tests added; build 0/0; ext tests 66 pass; core tests 3903 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T10:58:34Z | DO | r=1 | n=1 | unit D5c complete: test matrix restored (10 tests); build 0/0; ext tests 76 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:05:54Z | DO | r=1 | n=1 | unit D5c-1 complete: inference matrix (12 tests); build 0/0; ext tests 88 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:14:48Z | DO | r=1 | n=1 | unit D5c-2 complete: validation matrix (11 tests); build 0/0; ext tests 99 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:17:41Z | DO | r=1 | n=1 | unit D5c-2b complete: validation completion (8 tests); build 0/0; ext tests 107 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:19:17Z | DO | r=1 | n=1 | unit D5c-2c complete: subquery rejection (5 tests); build 0/0; ext tests 112 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:24:09Z | DO | r=1 | n=1 | unit D5c-3 complete: composition/conflict (5 tests); build 0/0; ext tests 117 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:26:10Z | DO | r=1 | n=1 | unit D5c-4 complete: cache/lifecycle (6 tests); build 0/0; ext tests 123 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:29:18Z | DO | r=1 | n=1 | unit D5c-4b complete: cache/lifecycle (4 tests); build 0/0; ext tests 127 pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:43:21Z | DO | r=1 | n=1 | unit D5d complete: six-dialect SQL generation (6x8 tests); build 0/0; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T11:52:26Z | DO | r=1 | n=1 | unit D5e-1 complete: live row-level oracles (4 providers x 7 tests = 28); build 0/0; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T12:00:32Z | DO | r=1 | n=1 | unit D5e-2 complete: MariaDB + ClickHouse live (14 tests); build 0/0; 42 total pass; logs rv1/logs/ | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T12:05:39Z | DO | r=1 | n=1 | unit D6: baseline harness built+run (~22s); A/A noise bounds deferred to D7; artifacts rv1/perf-baseline/ | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T12:08:28Z | DO | r=1 | n=1 | unit D6a complete: full benchmark harness authored (7 phases) in both trees; both build 0/0; BDN not run; diff note rv1/perf-harness-diff.md | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T12:10:40Z | DO | r=1 | n=1 | unit D6b complete: 3 benchmark methods added to both twins (10 total); both build 0/0; BDN not run | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T12:14:46Z | DO | r=1 | n=1 | unit D6b confirmed: 10 methods/file; both trees build 0/0; BDN not run; limitation: WithData seeding, needs physical table (T296b style) | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T12:22:15Z | DO | r=1 | n=1 | unit D6c complete: A/A baseline 2 runs x 10 methods (~58.5s + ~55.9s); noise-bounds.json + README.md; no flagged methods | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T12:34:19Z | DO | r=1 | n=1 | unit D6c tightened: 2 full runs (~103s + ~107s); 3 methods inconclusive (Prepared_FallbackHit, Execute_ConstantProjection, Execute_Keyed); noise-bounds.json updated | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:03:47Z | DO | r=1 | n=1 | unit D7b complete: matched comparison (in-process, both trees); Warm_RawHit regression (+24.6%); all alloc increased; comparison-matched.json + README.md | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:16:06Z | DO | r=1 | n=1 | unit D8 complete: hot-path cleanup (ShapeState cached); all methods within noise for time; alloc increases persist; comparison-matched2.json | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:17:56Z | DO | r=1 | n=1 | unit D8b complete: allocation accounting (A/A delta 0 bytes; all real +543 to +2693 bytes); allocation-accounting.json + .md | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:19:35Z | DO | r=1 | n=1 | unit D8c complete: warm-hit parity confirmed (bodies identical); raw-shape ComponentShape[] allocation attributed (~0.9us); no product edits | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:31:28Z | DO | r=1 | n=1 | unit D8d complete: ShapeState cached (boxed); raw-shape ComponentShape[] still allocates per capture; Warm_RawHit +23.5% persists; irreducible cost ~0.9us + ~788 bytes | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:36:02Z | DO | r=1 | n=1 | unit D8e complete: CaptureRawShape runs exactly once per warm hit; ConditionalWeakTable cache added; irreducible cost ~0.9us + ~788 bytes — contract conflict for CHECK/PLAN | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:45:52Z | DO | r=1 | n=1 | unit D8f complete: shape selectors deduplicated (projection removed); Warm_RawHit +23.5% persists; irreducible cost ComponentShape[] array per capture | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T13:50:12Z | DO | r=1 | n=1 | unit D8g complete: capture runs exactly once per warm hit; ConditionalWeakTable removed; irreducible cost ~0.9us + ~788 bytes — contract conflict for CHECK/PLAN | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T14:10:50Z | DO | r=1 | n=1 | DO complete: all units done; manifest + both gates passed (RC=0); candidate ready for CHECK; AC9/AC11 open (perf contract conflict + coverage not collected) | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T15:24:59Z | DO | r=1 | n=1 | unit D10a: full suite RC=2; 14 passed / 3 failed / 32 skipped (opt-in probes, not provider skips); 2 constant-projection HAVING parameter-rebind failures + 1 warm-set-op allocation ceiling failure | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T14:03:56Z | DO | r=1 | n=1 | unit D9 complete: docs EN+RU updated; no specs/superpowers links; DocFX not installed (blocker) | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T15:38:38Z | DO | r=1 | n=1 | unit D10b complete: fixed constant-projection parameter rebinding (EffectiveHaving + preGroupHaving path); both residual tests pass; warm-set-op allocation passes; build 0/0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T15:59:35Z | DO | r=1 | n=1 | unit D10c complete: whole-solution coverage gate passed (line 89.3% >= 85, branch 83.7% >= 75); rc=0; ~10m 25s | `.pdca/status/D362-selectgroup-extensions-1.md` ## DO result
2026-11-05T16:27:01Z | DO | r=1 | n=1 | unit D10e complete: D10d integration1 RC=2 classified as environmental (4015 container connection failures across all providers during coverage profiler run); no code regression; sample passes on re-run | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T16:39:57Z | DO | r=1 | n=1 | unit D10f complete: coverage gap analysis (nextorm.extensions line 65.1% branch 41.9%; 75% gate unreachable even at 100% ext branch = 73.1%); gap-analysis.md written | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T16:56:12Z | DO | r=1 | n=1 | unit D10g complete: clean integration coverage (prewarm 8658/0/197; int1 8461/0/197; int2 118/0/0); merged line 81% (>=65), branch 75.3% (>=75) PASS; invalid run isolated | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T16:58:26Z | DO | r=1 | n=1 | DO complete — candidate ready for CHECK: build 0/0; whole suite RC=0; integration 8461+118 pass/0 fail; whole coverage 89.3/83.7; integration coverage 81/75.3; both script gates rc=0; AC9 open, AC11 met | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:03:26Z | CHECK | r=1 | n=1 | CHECK #1 not accepted → DO; findings F1–F5 (F1 real allocation delta vs 0 B/op A/A; F2 inconsistent noise datasets; F3–F5 as returned); infeasibility claim unverified; attempt n→2 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:06:30Z | DO | r=1 | n=2 | unit P1 complete: per-site allocation probe (main 576/424/1928/1288 B; baseline 1832/1112 B; delta +1272 B; clone=1); shape state static refs; floor documented; build 0/0; ext tests 127 pass | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:13:25Z | DO | r=1 | n=2 | unit P2a complete: 4 raw runs (B1 59.7s, B2 55.0s, C1 62.5s, C2 58.4s; 10/10, 0 fail each); SQL parity identical; tables perf-final/TABLES.md; candidate identity recorded | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:21:04Z | DO | r=1 | n=2 | unit P2b complete: verdict from 4 runs (4 time regressions: Warm_RawHit/Prepare_Cold/Prepared_FallbackHit/Execute_CapturedCondition; all 10 alloc regressions); no-component controls GroupBy/extrema alloc-identical; VERDICT.md | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:26:06Z | DO | r=1 | n=2 | unit P3 complete: semantic audit (roslyn); Core/IVT/mechanism/consumer/operator all pass; generator string literal "SelectGroup" still present at JoinAliasGenerator.cs:452 (contradiction) | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:32:52Z | DO | r=1 | n=2 | unit P3b complete: name-free generator rule (return type first, projection lambda second); grep SelectGroup in generator empty; alias tests 176 total/0 failed/176 succeeded/0 skipped exit 0; build 0 warnings/0 errors; semantic-audit updated | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:41:52Z | DO | r=1 | n=2 | unit P4 complete: SelectGroupDiagnostics (internal, thread-static, IVT to nextorm.extensions.tests only) proves 1 inference total / 0 on a confirmed warm raw hit for SelectGroup and SelectGroupHaving with explicit rows; changed captured value = 2 inferences (raw shape ineligible by Core design, finding); oracle map AC3–AC8: AC5/AC8 complete, AC3/AC4/AC6/AC7 gaps, parity assertions flagged; ext tests 130/0, build 0/0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:56:29Z | DO | r=1 | n=2 | unit P5a complete: closure-keyed raw recipe (RawShape.Closures reference identity, closure scan over joins/sorting/windows/selectors, captured QueryCommand still ineligible); publication gated on every refresh parameter having CapturedKey; QueryCommand.Clone.cs now copies EffectiveHaving/EffectiveGroupingKeys/SourcePredicate/RequireDistinct (staged apply lost them -> stale HAVING params); SelectGroup/SelectGroupHaving changed-captured-value = 1 inference total/0 on hit with fresh rows; different member = 2 (no false hit); ext 132/0; core 3903/1 (policy expectation); P4-CAPTURED-NOT-RAWHIT resolved | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T17:56:29Z | DO | r=1 | n=2 | unit P5b complete: core policy test rewritten (publish 1, reuse 1, same instance, parameter 99 fresh) without weakening freshness; new guard Recipe_RefreshParameterWithoutCapturedKey_IsNotPublished (publish 0, reuse 0, value 51); core 3904/0/3904/0 rc=0; sqlite 2307/0/2306/1 rc=0; postgres 1946/0/1946/0 rc=0; build 0 warnings/0 errors rc=0; footprint += src/nextorm.core/DataContext/QueryPlanner.cs, tests/nextorm.core.tests/Caching/CachedPreparationRecipeTests.cs; manifest tree 7544240f...; gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T18:05:39Z | DO | r=1 | n=2 | unit T1 complete: exact live oracles on PostgreSQL/SQL Server/MySQL/MariaDB/ClickHouse/SQLite (7/7 each, 0 skipped; project 118/0/118/0); keyed=max per group, HAVING count>1 filters c/d by name, source-HAVING AND keeps only a/b (NULL drops by three-valued logic), global count 11 with pass/fail HAVING, constant "constant" one row + empty input 0 rows, NULL group once, ORDER BY+LIMIT page [a=4,b=3]; dead SelectGroupLiveTests deleted; unit global test fixed + constant empty test; ext unit 133/0; build 0/0; footprint += SelectGroupLiveOracles.cs, MariaDbSelectGroupLiveTests.cs + 5 provider suites; tree ac85783e...; gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T19:06:23Z | DO | r=1 | n=2 | unit T2 complete: exact SQL per dialect on 6 provider projects (8/8 each) with parameter collections; replaced all `SelectGroup` vs explicit-`GroupBy/Select` parity facts (sqlite 8, sqlserver 9, mysql 8, mariadb 8, clickhouse 9, postgres 25 T323 facts) and the `T323LegacySqlBaselineTests.CompareCase` recorded-baseline oracle (8 exact facts × 6 projects); full provider runs 2307/1946/2084/1215/1058/1505 with 0 failed (pre-existing skips only: sqlite 1, sqlserver 8); core 3904/0; build 0 warnings/0 errors --disable-build-servers; CRLF clean; ClickHouse `sum(Int32)`/int-literal type limitation documented, not dropped; oracle-map AC4 complete; footprint + tree/gates refreshed | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T19:12:37Z | DO | r=1 | n=2 | unit T3 complete: AC6/AC7 oracles strengthened (raw-shape no-false-hit matrix + reset/re-prepare non-accumulation; behavioural same-slot replacement; concrete conflict exceptions/messages both orders; exact additive WHERE counted once with `$min` and `NeedsParamRefresh`; DISTINCT monotonic; five explicit-grouping facts). ext unit 145 total/0 failed/145 passed rc=0; build nextorm.slnx --disable-build-servers 0 warnings/0 errors rc=0; oracle-map AC6/AC7 complete; footprint += nextorm.extensions.tests.csproj (ClickHouse reference for `WithTotals`); tree/gates refreshed | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T19:20:30Z | DO | r=1 | n=2 | unit R1 complete: original agreement archived (issue #362 body + canonical comment #issuecomment-6654133696); EN↔RU audit found one symmetric divergence — missing third-party `IQueryComponent` extensibility pointer — fixed in both guide/05 pages and re-checked against all other SelectGroup mentions; public-doc links to `docs/specs/**`/`docs/superpowers/**` = 0 (published pages); foreign four files untouched (pre-existing 515/2/518/2 diff, no SelectGroup/D362 markers); Release build 0 warnings/0 errors rc=0; CRLF 0 offenders after fixing 6 pre-existing LF-only footprint files; CPM 0 inline versions; no `git add`/commit (staged 0, HEAD f991d5ea); evidence `rv1/audit/docs-hygiene.md`; tree 3583e494...; gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T19:24:14Z | DO | r=1 | n=2 | unit R3 complete: `## Defect history` now carries five stable keys with outcomes (`perf-residual-warm-hit` open/mechanism floor; `captured-not-rawhit` resolved P5a/P5b; `effective-state-lost-in-staged-apply` resolved; `coverage-invalid-env` superseded by D10g 81%/75.3%; `generator-name-specialcase` resolved P3b), original `none` marked superseded; pre-clean 71.5% claim marked `SUPERSEDED — invalid environment` in `rv1/unit-evidence.md` and `rv1/coverage/integration/gap-analysis.md` (history retained); process-defect spec `docs/specs/design/D362-pdca-process-defects.md` PD1–PD5; no product/test/threshold edits; footprint += spec path, tree e10a23c2...; gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T19:42:33Z | DO | r=1 | n=2 | unit R2a complete: full suite 23385/23176/3/206 rc=2 — 3 failures are stale pre-P5a raw-shape expectations (`RawShape_ClosureValue_ShouldBeIneligibleAndRebindLiveRows`, `RawShape_LiteralVersusClosure_ShouldDifferInEligibilityOnly`, `CrossJoinBattery_LeftConditionArm_ShouldCompareAndReadLiveRows`), recorded as defect `stale-rawshape-oracles`, not fixed; container providers ran (206 skips = pinned capability set: 197 integration + 8 SQLServer repro + 1 SQLite LOB); coverage 89.3 % line / 83.7 % branch (42264/50437) PASS at 85/75, per module core 89.0/84.2, postgres 95.6/78.4, sqlite 94.3/80.9, sqlserver 96.6/81.9; exclusions audit 0 new attributes, whole settings unchanged; evidence `rv1/coverage/final-whole/` | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T19:54:39Z | DO | r=1 | n=2 | unit R2b complete: stale raw-shape oracles updated without weakening — `RawShape_ClosureIdentity_ShouldKeyThePlanAndRebindLiveRows` (same closure = one raw key/one plan, rows 6..10→9..10; fresh instance distinct key), `RawShape_LiteralVersusClosure_ShouldBeDistinctKeys` (literal vs closure distinct keys, same rows, folded `ThresholdPlus(offset)` parameter without CapturedKey not published via `QueryPlanStore.TryGetRaw`), `CrossJoinBattery_LeftConditionArm_...` (cross-join capturable; same closure rebinds 8→12 rows, one plan; fresh instance distinct key); each test documents the P5a policy; two classes 27/0/27/0 rc=0; whole integration 8658/0/8461/197 rc=0; Debug build 0 warnings/0 errors rc=0; defect `stale-rawshape-oracles` resolved | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T20:10:56Z | DO | r=1 | n=2 | unit R2c complete: clean whole-suite on the fixed tree 23385/0/23179/206 rc=0 (7 m 34 s); coverage collect same counts rc=0 (6 m 59 s); reportgenerator rc=0; line 89.3 % / branch 83.8 % (42280/50437) PASS at 85/75, per module core 89.0/84.3, postgres 95.6/78.4, sqlite 94.3/80.9, sqlserver 96.6/81.9; container providers executed (206 pinned capability skips); previous `final-whole` marked superseded by `final-whole-clean`; no product/test/threshold change, manifest tree unchanged and gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T20:29:33Z | DO | r=1 | n=2 | unit R2d complete: pre-warm 8658/0/8461/197 rc=0 (4 m 31 s); integration1 coverage 8658/0/8461/197 rc=0 (267.9 s), integration2 118/0/118/0 rc=0 (19.1 s); merged line 81 % / branch 75.3 % (40150/53276) PASS at 65/75 (core 80.8/76.6, extensions 61.7/38.5, clickhouse 87.5/74.1, efcore 72.6/67.1, mariadb 84.1/74.3, mysql 80.0/72.7, postgres 92.4/73.6, sqlite 92.4/79.9, sqlserver 88.1/76.3); merge audit: 9 modules in report, 66 InMemory* classes filtered (0 remain), no other class dropped, no new exclusion/threshold change; first attempt failed 10 SQL Server tests on a stale reused container, purged and rerun clean (recorded in README); `coverage/integration/` D10g marked superseded; AC11 evidence updated; tree unchanged, gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T20:40:15Z | DO | r=1 | n=3 | unit C1 complete: `SelectGroupHaving` global (count=11, HAVING >10 pass / >11 zero rows) and constant ("constant" one row, empty filter zero rows) live oracles added to the shared base + MariaDB, keyed existing; `SelectGroup` three outcomes already exact; 6 providers × 9/9 (Postgres, SqlServer, MySql, MariaDb, ClickHouse, Sqlite), project 130/0/130/0 rc=0; Debug build 0 warnings/0 errors rc=0; oracle-map AC3 operator×outcome×provider matrix added; CHECK #2 F1/F2/F3 + AC3/AC6 recorded; AC3 finding resolved, AC6 weak-oracle finding open; tree/gates refreshed | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T20:44:52Z | DO | r=1 | n=3 | unit C2 complete: positional warm-hit freshness proven for SelectGroup (WHERE `Parameter(0)`: 6 → a=2/null=1, 0 → a=2/b=1/null=1, one inference, same prepared command) and SelectGroupHaving (HAVING `Parameter(0)`: 2 → empty, 0 → a/b/null, one inference); `CloneForCache_PreservesShape` replaced by behavioural `CloneForCache_PreservesShapeAndRebinds` (identical SQL, one GROUP BY/HAVING, no DISTINCT, clone rebinds changed capture, inference 1, constant clone one DISTINCT and zero rows); `CapturedValue_Rebinds` kept; ext unit 147/0/147/0 rc=0, sqlite raw subset 195/0 rc=0, core raw subset 47/0 rc=0; Debug build 0 warnings/0 errors rc=0; AC6 finding resolved, oracle-map complete | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T20:52:31Z | DO | r=1 | n=3 | unit PF1 complete: protocol v2 (A-B-B-A, SDK 10.0.401, per-metric + control-specific A/A bands, allocations not assumed zero), 9 control arms added to candidate + baseline twin (19 arms listed in both), run.sh with 8 runs + full-matrix SQL parity (9 scenarios, verify.sql), pre-check parity found the HAVING duplication (`having-combination-duplicated`, open, not fixed); candidate/baseline solution builds 0/0 and both benchmark projects Release 0/0; no measurement run; footprint + gates refreshed | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T21:00:08Z | DO | r=1 | n=3 | unit PF1b complete: HAVING duplication fixed — component passes only the added condition for Replace (full combined predicate kept for validation and Clear/constant WHERE), engine ANDs the raw Having once (null-aware); 5 new composition facts pass (one HAVING/predicate/parameter for SelectGroup+Having; two each for SelectGroupHaving; global one HAVING; constant lowered to WHERE once + one DISTINCT; plain query unchanged); ext unit 152/0/152/0 rc=0, live suite 130/0/130/0 rc=0, sqlite SelectGroup/T323 24/0, core 3904/0, build 0 warnings/0 errors; parity dump now identical (parity.diff empty); defect resolved; tree/gates refreshed | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T21:10:21Z | DO | r=1 | n=3 | unit PF2 complete: candidate identity sha256 bd8c7725...; 8 counterbalanced A-B-B-A runs (10 operator + 9 control arms each, 0 failures, SDK 10.0.401, parity diff 0 lines); operator 6 time regressions + 10/10 allocation regressions; controls time within noise but all 9 allocation arms +31..+41 B outside the 0 B band → `control-allocation-overhead` open; AC9 not met; verdict.json + VERDICT.md written; no product edits; tree unchanged, gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T21:25:02Z | DO | r=1 | n=3 | unit PF3 complete: closure key scan made thread-static reusable with lazy list/null snapshot (site: TryCollectClosureKey; per-call List 32 B); exact probe candidate capture 4752 B vs baseline 4848 B, exact arm 5232 vs 5264 B; control re-run 6×9 arms 0 failures — 3+3-run band puts every control allocation delta within band (GroupBy +0.78..+1.09 %, extrema negative) and time within noise; closure tests (ext 152/0, core 3904/0, raw-shape 27/0) green; build 0/0; defect resolved with the report-precision caveat | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T21:41:31Z | DO | r=1 | n=3 | unit R2e complete: post-PF1b/PF3 whole-solution gates re-established — build 0/0 rc=0; suite 23404/0/23198/206 rc=0 (7 m 15 s); coverage run 23404/0/23198/206 rc=0 (7 m 33 s); reportgenerator rc=0; line 89.3 % (73257/82008) / branch 83.8 % (42291/50445) PASS at 85/75 (core 89.0/84.3, postgres 95.6/78.4, sqlite 94.3/80.9, sqlserver 96.6/81.9); `final-whole-clean` superseded by `final-whole-pf`; no product/test/threshold change, manifest tree unchanged and gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-05T23:53:14Z | DO | r=1 | n=3 | unit R2f complete: clean-environment integration gate on the post-PF1b/PF3 tree — pre-warm 8658/0/8461/197 rc=0 (6 m 19.5 s, normal Testcontainers mode, no overrides); integration1 coverage 8658/0/8461/197 rc=0 (227.5 s); integration2 extensions 130/0/130/0 rc=0 (18.6 s); merged line 81 % (67110/82785) / branch 75.3 % (40151/53284) PASS at 65/75 (extensions 61.8/38.6 separately); merge audit 9 modules / 790 entries, 66 InMemory filtered 0 remain, 598/598 user classes, 0 new exclusions; R2d superseded by `final-integration-pf`; no product/test/threshold change, manifest tree unchanged and gates rc=0 | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-06T00:03:53Z | CHECK | r=1 | n=3 | CHECK #3 final: **not accepted** → ACT incomplete/STOP; AC1–AC8/AC10/AC11 met, AC9/U6 unmet; PF2 6 time + 10/10 allocation regressions outside A/A bands (Warm_RawHit +15.04 % vs 1.99 %); post-PF3 operator matrix unverified; criteria defect not confirmed; no fourth attempt | `.pdca/status/D362-selectgroup-extensions-1.md` §CHECK record
2026-11-06T00:03:53Z | CHECK | r=1 | n=3 | mandatory escalation concluded classification (ii); recorded but not adopted by CHECK | `.pdca/status/D362-selectgroup-extensions-1.md` §CHECK record
2026-11-06T00:03:53Z | ACT | r=1 | n=3 | cycle closed **incomplete/STOP** — AC9/U6 unmet, post-PF3 operator parity unverified; delivered AC1–AC8/AC10/AC11; no further DO/CHECK; user decision recorded (accept overhead / new optimization cycle / stop) | `.pdca/status/D362-selectgroup-extensions-1.md` §ACT record
2026-11-06T00:03:53Z | ACT | r=1 | n=3 | status file finalized and retained; all evidence kept; no git operations | `.pdca/status/D362-selectgroup-extensions-1.md`
2026-11-06T04:31:16Z | ACT | r=1 | n=3 | user explicit instruction: measured component-operator overhead **accepted** for the delivered behaviour; U6/AC9 for follow-up governed by the acceptance; no product edit | `.pdca/status/D362-selectgroup-extensions-1.md` §ACT record
2026-11-06T04:31:16Z | ACT | r=1 | n=3 | new optimization issue **#368** opened in milestone `1.0.9-rc5` after mandatory duplicate search (no existing coverage): https://github.com/AlexeyShirshov/nextorm/issues/368; #362 not closed | `.pdca/status/D362-selectgroup-extensions-1.md` §ACT record
