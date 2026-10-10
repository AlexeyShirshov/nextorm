# D175 — усиление проверок равенства планов JoinInto

- Issue: **#175**
- Milestone / branch: **1.0.9-rc2**
- Base: **18659e41**
- Selected variant: **pdca-dotnet**
- План: **r=1**, попытка **N=1**, evidence contract **rv=1**
- `plan_state: ready`
- Основание: предоставленный evidence pack; самостоятельного чтения кода и запуска команд не было.
- `P:D175-r1`: закрепить план, классифицировать перечисленные проверки по фактически проверяемому контракту и усилить доказательства неравенства планов без изменения runtime-поведения.

## Цель и Scope

### Цель

Сделать проверки различия планов независимыми от случайного различия hash-кодов. При этом сохранить самостоятельные проверки кеширования, количества исполнений и hash-coherence. Подтвердить минимально необходимую видимость `EagerLoadSpec.Assign`.

### В scope

1. Семантическая классификация восьми перечисленных hash-inequality проверок.
2. Замена проверок, действительно доказывающих **неравенство планов**, на `Equals(left, right) == false` соответствующего comparer.
3. Проверка назначения перечисленных assertion об исполнении и identity/reuse; добавление plan-equality только там, где сценарий действительно требует сравнения планов.
4. Подтверждение решения **оставить `Assign` internal**.
5. Прогоны затронутых тестов, полные core/sqlite suites, build и контроль footprint.
6. Условно — container-backed integration suite, если изменяется общий интеграционный тест.

### Вне scope

- Изменения реализации comparer, генерации SQL, исполнения запросов, кеширования и eager loading.
- Новые публичные API, расширение видимости `Assign`, переименование типов/методов.
- Массовая переработка всех JoinInto/EagerLoading тестов.
- Изменения пакетов, CPM, SDK, coverage settings.
- Коммиты, push, merge.
- Перенос обнаруженного остатка D175 за пределы milestone 1.0.9-rc2.

Если усиленный тест обнаруживает продуктовый дефект, исправление runtime-кода **не добавляется молча**: исходный D175 остаётся активным, возвращается в PLAN для согласования конкретного предусловия/расширения.

## Критерии приёмки

| ID | Наблюдаемый критерий | Явный негативный случай |
|---|---|---|
| R175-HASH | Все восемь sites классифицированы. Каждый site, проверяющий неравенство планов, содержит assertion `Equals(...) == false` comparer нужного уровня. Hash `NotBe` удалён либо имеет конкретное документированное самостоятельное назначение. | Разные hash-коды остаются единственным доказательством различия планов; выбран comparer другого уровня; один из sites не разобран. |
| R175-SEM | Проверки количества исполнений и identity/reuse сохранены, если это самостоятельный контракт. При наличии отдельного требования о различии/равенстве планов добавлена соответствующая проверка comparer. | `Executions == 1` или `ReferenceEquals` механически заменены на Equals и больше не проверяют прежнее поведение; либо объявлены доказательством plan inequality без основания. |
| R175-COH | Существующие hash-equality проверки вместе с Equals сохранены как проверка coherence: равные планы имеют равные hash-коды. | Удалена полезная hash-equality проверка или введено общее требование «неравные планы обязаны иметь разные hash-коды». |
| R175-ASSIGN | `Assign` остаётся `internal`; семантический отчёт подтверждает известных потребителей внутри `nextorm.core` и отсутствие внешнего потребителя, требующего widening. | Видимость расширена без названного потребителя; изменена на `private` с поломкой межтиповых вызовов; вывод основан только на текстовом поиске. |
| R175-TEST | Все обязательные тестовые прогоны завершаются exit 0; целевые selectors реально исполняют тесты. Условный интеграционный прогон, если применим, исполняет требуемых провайдеров. | Exit 0 при нулевом числе целевых тестов; новые/целевые тесты skipped; контейнерные провайдеры пропущены, но прогон назван успешным. |
| R175-BUILD | `dotnet build nextorm.slnx -c Debug`: exit 0, **0 warnings / 0 errors**. | Suppression или изменение build settings введены ради обхода gate; присутствуют warnings/errors. |
| R175-SCOPE | D175 меняет только разрешённые тестовые файлы и статус цикла; runtime/API/configuration не меняются. CRLF сохранён. | Появился D175-specific diff в `src/**`, новая dependency, потеря CRLF или повреждение исходных пользовательских изменений. |
| R175-EVIDENCE | Каждая применимая строка контракта rv1 имеет требуемые отчёты/логи/результаты; каждая неприменимая условная ветка закрыта доказательством её предиката. | Отсутствующий отчёт трактуется как неприменимость либо как основание ослабить контракт. |

## Минимальное решение

### Три ответа

1. **Что исправляем:** способ доказательства различия планов, а не алгоритм построения или сравнения планов.
2. **Непреложные ограничения:** подходящий comparer, сохранение execution/cache контрактов, `Assign internal`, отсутствие runtime/API изменений, полный учёт вариантов и evidence.
3. **Оптимум:** локальные assertion в существующих fixtures, без нового общего test helper и без изменений production-кода.

### Альтернативы

| Подход | Плюсы | Минусы / цена / риск |
|---|---|---|
| Оставить hash-inequality, добавить объяснения | Минимальный diff | Не устраняет слабое доказательство; не принимается для plan inequality |
| **Заменить/дополнить assertion существующим comparer** | Проверяет нужную семантику; небольшой локальный diff | Нужна точная классификация каждого site и уровня comparer |
| Новый общий helper / переработка comparer | Унификация | Избыточный footprint, риск скрыть смысл теста или изменить runtime |

**Выбор:** второй вариант. Helper не нужен для ограниченного числа sites. Hash `NotBe` сохраняется только при доказанном самостоятельном hash-specific контракте, а не для гарантии отсутствия коллизий.

## Чего не сказала постановка: допущения и предусловия

| Пробел | Закрытие |
|---|---|
| Что именно проверяет каждый `NotBe` | **Предусловие DO:** адресный scout-классификатор до правок. Для каждого site фиксируются intent, operands, comparer и самостоятельный смысл hash assertion. |
| Назначение execution/identity assertion | Та же разведка устанавливает контракт. До доказательства сохранять исходный assertion; не считать его автоматически плановым. |
| Точный comparer для каждого site | Разрешается через Roslyn по receiver/operands и существующим вызовам. Использовать текущий comparer нужного уровня, не угадывать по имени файла. |
| Null/default, типы значений, flags/providers в исходных сценариях | Scout составляет перечень **фактически присутствующих** вариантов. Все сохраняются; дополнительные ветки неизменённого продукта не становятся автоматически частью D175. |
| Точное число references `Assign` | В pack заявлено «6», но перечисление содержит definition и шесть use sites. Не использовать число как доказательство; получить повторный Roslyn-отчёт с разделением definition/usages и assembly. |
| Branch/HEAD и исходные незакоммиченные изменения | До правок проверить branch `1.0.9-rc2`, существование base и ancestry; сохранить начальный status/diff. Не переключать branch и не уничтожать чужие изменения автоматически. |
| Наличие реальных filterable test symbols | Имена ниже — точные планируемые selectors по предоставленным fixtures. Roslyn подтверждает их до запуска; exit 0 с нулём тестов неприемлем. |
| Возможность container-backed integration | Не требуется для неизменённого интеграционного файла. При применимости обязательна загрузка integration skill и запуск всех контейнерных провайдеров. |
| Режим normal/autonomous | По умолчанию **normal**: coder записывает план до `go`, DO ждёт `go`. В явно объявленном autonomous — запись и gate 1 без ожидания пользователя. |
| Verified URL issue | В pack дан #175, но не проверенный URL. URL не выдумывать; выполнение не зависит от изменения GitHub tracking. |

Низкая уверенность касается классификации sites, а не выбора архитектуры. Это ограниченная разведка внутри DO, не основание откладывать весь D175.

## Матрица вариантов по пути исполнения

`test` и `guard` ниже — обязательные способы закрытия ветки. Условие выбора наблюдаемо в существующем тесте и фиксируется в audit; это не незакрытый TBD.

| Вариант | Site / путь | Закрытие |
|---|---|---|
| V175-H01 | `tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs:34` | **test**, если operands — планы/select expressions и intent — inequality: Equals false на фактически используемом comparer. Иначе **guard**: доказанный hash-specific intent и сохранённый assertion с причиной. |
| V175-H02 | тот же файл `:60` | Та же развилка, самостоятельная строка audit и evidence. |
| V175-H03 | тот же файл `:72` | Та же развилка, самостоятельная строка audit и evidence. |
| V175-H04 | `ImplicitNavigationR3CountBoundaryTests.cs:102` | **test** для plan inequality; иначе **guard** с доказанным иным назначением. Сохранить исходный boundary-сценарий. |
| V175-H05 | `RawSourceBindingFilterTests.cs:927` | **test / guard** по intent; сохранить исходный binding/filter вариант. |
| V175-H06 | `RawSourceBindingFilterTests.cs:948` | Отдельный **test / guard**; не считать покрытым H05 без проверки сценария. |
| V175-H07 | `PlanKeyStructureTests.cs:238` | **test / guard**; comparer должен соответствовать сравниваемому plan-key уровню. |
| V175-H08 | `InMemoryTests.cs:331` | **test / guard**; сначала установить, сравниваются ли планы, а не сущности/значения. |
| V175-C01 | `JoinIntoPlanKeyTests.cs:63` | **test**: сохранить Equals + hash-equality coherence. |
| V175-C02 | тот же файл `:91` | **test**, самостоятельный существующий сценарий. |
| V175-C03 | тот же файл `:106` | **test**, самостоятельный существующий сценарий. |
| V175-C04 | тот же файл `:135` | **test**, самостоятельный существующий сценарий. |
| V175-C05 | `tests/nextorm.sqlite.tests/JoinIntoSqlGenerationTests.cs:136` | **test**: сохранить coherence; использовать существующий query comparer там, где соответствует operands. |
| V175-X01 | `JoinIntoExecutionTests.cs:55` | **test**: сохранить execution count. Plan assertion добавляется только при доказанном отдельном плановом intent. |
| V175-X02 | тот же файл `:76` | **test**, отдельный execution-сценарий и та же развилка. |
| V175-X03 | `tests/nextorm.integration.tests/CommonTestSuite.JoinInto.cs:221-222` | **guard** intent при неизменённом тесте; **test** всех общих provider-вариантов, если добавляется/меняется assertion. |
| V175-X04 | `JoinIntoSqlGenerationTests.cs:159-162`, reuse | **test**: сохранить identity/reuse. При самостоятельном plan-equality контракте добавить Equals true. |
| V175-X05 | тот же участок, miss | **test**: сохранить identity/miss. Equals false добавлять только если причина miss — действительно различие сравниваемых планов. |
| V175-A01 | `Assign`, core consumers | **guard**: Roslyn refs + build; visibility остаётся internal. |
| V175-A02 | Внешний/test consumer | **guard**: подтверждённое отсутствие. Обнаружение именованного потребителя → возврат в PLAN, не автоматический widening. |
| V175-T01 | Фактические null/default варианты затронутых тестов | **test**, если присутствуют в сценариях; иначе **guard**: новые null/default runtime-ветки не добавлены и не изменены. |
| V175-T02 | Фактические value/reference operands | **test** существующих вариантов; **guard** сохранения типов и operands при отсутствии второго варианта. |
| V175-T03 | Фактические flags/provider варианты | **test** всех вариантов затронутого fixture. Core + SQLite обязательны; общий integration fixture при изменении исполняется для всех его провайдеров. |

Новых deferral по D175 нет. Для каждого фактически обнаруженного дополнительного сценария audit получает стабильный под-ID, например `V175-H05.1`; требуемое покрытие добавляется, не заменяет исходное.

## Декомпозиция

### D:175.1 — исходное состояние и адресная классификация, fix now

**Читает:** все sites матрицы; comparer infrastructure:

- `src/nextorm.core/Query/QueryPlanEqualityComparer.cs:11,43,354`;
- `ExpressionPlanEqualityComparer.cs:16`;
- `SelectExpressionPlanEqualityComparer.cs:8`;
- `JoinExpressionPlanEqualityComparer.cs:9`;
- `EntityBuilderEagerLoading.cs:164,201,209,301-307`;
- `JoinIntoSpec.cs:308,424`;
- `JoinIntoManyToManySpec.cs:331`.

**Пишет:** только evidence/status, не production-код.

Действия:

1. Проверить branch/base, сохранить начальный status/diff.
2. Через scout/Roslyn классифицировать H01–H08, X01–X05, подтвердить comparer, operands и фактические варианты.
3. Повторить семантические refs `Assign`; разрешить расхождение счётчика.
4. Подтвердить selectors и выполнить focused baseline.

**Выход:** audit без неклассифицированных sites. Baseline failure фиксируется; не исправляется посторонний дефект без PLAN.

### D:175.2 — локальное усиление assertion, fix now

**Основной разрешённый footprint:**

- `tests/nextorm.core.tests/SelectExpressionPlanEqualityComparerTests.cs`
- `tests/nextorm.core.tests/ImplicitNavigationR3CountBoundaryTests.cs`
- `tests/nextorm.core.tests/RawSourceBindingFilterTests.cs`
- `tests/nextorm.core.tests/PlanKeyStructureTests.cs`
- `tests/nextorm.core.tests/InMemoryTests.cs`

**Условный footprint по результату audit:**

- `tests/nextorm.sqlite.tests/JoinIntoSqlGenerationTests.cs`
- `tests/nextorm.sqlite.tests/JoinIntoExecutionTests.cs`
- `tests/nextorm.integration.tests/CommonTestSuite.JoinInto.cs`

Действия:

- Для plan inequality заменить слабое доказательство на comparer Equals false.
- Удалить лишний hash `NotBe`; сохранить только обоснованный hash-specific assertion.
- Сохранить coherence и execution/identity assertions.
- Не изменять operands или сценарий ради получения зелёного результата.
- Не менять `Assign`; зафиксировать решение и references в evidence.
- Сохранить CRLF; выполнить `git diff --check`.

`JoinIntoPlanKeyTests.cs` — read/test-only по этому плану; изменение потребует отдельного доказанного основания.

### D:175.3 — подтверждение результата, fix now

**Пишет:** логи/evidence и статус.

1. Выполнить focused post-change, полные core/sqlite suites и build.
2. При изменении общего integration fixture выполнить container-backed integration suite.
3. Подготовить D175-specific diff, mapping variant → evidence и отчёт scope/coverage/mutation guards.
4. Передать CHECK. DO не завершает D175 собственным вердиктом.

Зависимости: `D:175.1 → D:175.2 → D:175.3`. Все три единицы активны; предусловие не supersedes исходный D175.

## Footprint и режим единиц

- **Один рабочий tree**, branch `1.0.9-rc2`, без worktree.
- **Последовательное исполнение:** audit, правки, валидация используют одни test contracts и пересекающиеся файлы.
- Scout read-only может собирать сведения параллельно по непересекающимся sites, но результат объединяется до правок.
- Статус: `docs/specs/status/rc2-175-joininto-test-quality-1.md`.
- Временные evidence: `/tmp/nextorm-D175/r1/`.
- `src/**`, configs и public docs — **read-only / без D175-specific изменений**.

Неопределённость footprint ограничена тремя условными тестовыми файлами. Её разрешает D:175.1 по intent, а не предположение «absence-of-execution всегда слабый тест плана».

## Требования к результатам предшественников

**Зависимостей от результатов других задач нет.**

Нужны только уже существующие comparer и fixtures, подтверждённые evidence pack. Предшествующий task/commit не объявляется обязательным без новых доказательств.

## Тест-стратегия

### Unit и SQLite

**Focused baseline и post-change:**

```bash
dotnet test tests/nextorm.core.tests -c Debug --filter "FullyQualifiedName~SelectExpressionPlanEqualityComparerTests|FullyQualifiedName~ImplicitNavigationR3CountBoundaryTests|FullyQualifiedName~RawSourceBindingFilterTests|FullyQualifiedName~PlanKeyStructureTests|FullyQualifiedName~InMemoryTests|FullyQualifiedName~JoinIntoPlanKeyTests"
```

```bash
dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~JoinIntoSqlGenerationTests|FullyQualifiedName~JoinIntoExecutionTests"
```

**Полные suites после правок:**

```bash
dotnet test tests/nextorm.core.tests -c Debug
dotnet test tests/nextorm.sqlite.tests -c Debug
```

Core — unit, без БД. SQLite suite — локальные SQL/execution проверки, без контейнеров.

Selectors подтверждаются Roslyn; отчёт должен доказывать исполнение каждого требуемого fixture, а не только ненулевой общий count.

### Integration — условная обязательная ветка

Предикат: D175 меняет `CommonTestSuite.JoinInto.cs` или другой файл integration suite, необходимый для выбранного assertion.

До запуска загрузить `.opencode/skills/running-integration-tests/SKILL.md`. Точный вызов:

```bash
DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
```

Требуются свидетельства исполнения PostgreSQL, SQL Server, MySQL, ClickHouse и SQLite. Отсутствующий socket сначала восстанавливается по troubleshooting skill. Skipped контейнерные провайдеры не дают passing evidence.

При отсутствии D175-specific integration diff предикат закрывается статическим отчётом; прогон не выдаётся за выполненный.

### Build

```bash
dotnet build nextorm.slnx -c Debug
```

Exit 0, 0 warnings, 0 errors.

### Coverage и red→green

- Проектные пороги неизменны: **MIN_LINE=85%, MIN_BRANCH=75%**.
- Для этого **test-only** плана отдельный coverage collection не требуется: production execution path не меняется. Пороги не отменяются и не объявляются измеренными.
- Применимость решения подтверждается отсутствием D175-specific `src/**` diff. Продуктовое изменение требует replan с coverage evidence по workflow.
- **Red→green не обязателен:** это устранение слабого оракула, а не исправление известного runtime-дефекта. Ожидаемо baseline и усиленные тесты оба green.
- Если новый Equals assertion red, нельзя ослабить его или менять сценарий: требуется диагностика причины и PLAN.

### Общий контракт

Общий контракт тестов: различие планов доказывает comparer соответствующего уровня; hash equality проверяет coherence; execution count и reference identity проверяют свои отдельные свойства.

## Матрица приоритетов CHECK

| Строки / ветви | Приоритет | Основание |
|---|---|---|
| H01–H08, их фактические варианты | **P1** | Главный инвариант D175: отсутствие hash-only доказательства plan inequality |
| A01–A02 | **P1** | Обязательное решение о visibility |
| C01–C05, X01–X05 | **P1** | Сохранение контрактов на затронутом пути исполнения |
| T01–T03, если вариант присутствует в затронутом fixture | **P1** | Фактический путь исполнения, не только формулировка issue |
| Build, test execution, footprint и applicable integration evidence | **P1** | Непреложные гейты плана |
| Неизменённые остальные JoinInto/EagerLoading tests | Регрессионный suite | Не становятся новыми локальными изменениями D175 |

Проектная таблица приоритетов классов в pack не передана. CHECK применяет указанную матрицу и не понижает P1.

## План документации

- Public `docs/**` и `docs/ru/**`: **не трогаем**; API и runtime-поведение не меняются.
- `docs/specs/roadmap/todo_navigation_properties.md:357-358`: **оставляем без изменений** в этом цикле; это provenance исходного долга, не acceptance evidence.
- Решение, audit и результат записываются в статус D175.
- Новых ссылок из public docs на internal specs не добавлять.

## Перф-замер

**Не нужен.**

План меняет тестовые assertion, например `SelectExpressionPlanEqualityComparerTests.cs:34,60,72`, а не per-row runtime path. `QueryPlanEqualityComparer.cs:43,354` и `EntityBuilderEagerLoading.cs:307` остаются неизменёнными. Benchmark baseline не собирается; отсутствие runtime diff проверяется CHECK.

## Разведка

**Нужна адресная разведка, не нужен архитектурный spike.**

Она доказывает:

1. intent и подходящий comparer каждого H/X site;
2. существующие input/null/default/type/flag/provider варианты;
3. consumers и visibility `Assign`;
4. реальные selectors.

**Наблюдаемый критерий завершения:** audit содержит запись для каждого ID матрицы, проверенный comparer для каждого планового site и объяснение каждого сохранённого `NotBe`/execution/identity assertion. Неклассифицированных sites нет.

Выбор решения эксперимента не требует: существующая comparer infrastructure подтверждена `QueryPlanEqualityComparer.cs:11,43` и её использованием в `JoinIntoSqlGenerationTests.cs:130`, `JoinIntoPlanKeyTests.cs:47`.

## Plan branch-delta и mutation-testing

- Ожидаемый product branch-delta: **0 строк и 0 ветвей**.
- Допустимый test delta: локальные assertion/пояснения, при необходимости дополнительный assertion в существующем сценарии.
- Изменение test assertion не трактуется как изменение runtime execution path.
- **Stryker не применяется:** продуктовый код не меняется; массовое mutation testing расширило бы задачу без необходимого доказательства.
- Если обнаружен продуктовый дефект или нужен product diff, это новая PLAN-ветка. Нельзя продолжить по test-only контракту и затем объявить mutation/coverage неприменимыми.

## Версионированный evidence contract

### Правила rv1

- Все источники ниже **планируемые**, кроме исходного evidence pack.
- Stable requirement IDs и row IDs сохраняются при ревизии.
- Каждая строка сохраняет проверку, evidence kinds/sources, точный вызов, требования к результату, artifact, owner и наблюдаемый applicability predicate.
- Неприменимость условного запуска подтверждается guard evidence; это не удаление строки.
- Новые test symbols и новые `file:line` заранее не выдумываются. Фактические символы/строки фиксирует post-change audit.
- CHECK re-gather: **не более двух адресных пакетов на CHECK**, владелец бюджета — **`check`**, исполнители — scout/coder. Пакет 1 закрывает перечисленные недостающие данные; пакет 2 проверяет результат или стойкую неопределённость. Бюджет не возобновляется переименованием запроса.
- Отсутствие отчёта сначала расходует re-gather, а не инициирует DO или ослабление контракта.

### Вызовы и артефакты

Команды из тест-стратегии обозначаются `CORE-FOCUSED`, `SQLITE-FOCUSED`, `CORE-FULL`, `SQLITE-FULL`, `INTEGRATION`, `BUILD`.

Для каждого запуска runner сохраняет stdout/stderr и exit code отдельно в `/tmp/nextorm-D175/r1/`. Baseline и post-change логи не перезаписываются.

**GATHER-INTENT — точный запрос scout:**

> По evidence pack D175 семантически разберите V175-H01–H08, C01–C05 и X01–X05. Используйте Roslyn первым для symbols/references/comparer resolution. Для каждого site сообщите intent, operands, comparer и его разрешённый символ, существующие null/default/value/reference/flag/provider варианты, test symbol/selector и доказательства file:line. Отделите plan inequality от hash-specific, execution-count и identity/reuse контрактов. Дайте факты, без рекомендаций.

**GATHER-ASSIGN — точный запрос scout:**

> Через Roslyn подтвердите declared visibility EagerLoadSpec<TEntity,TChild,TKey>.Assign, definitions и все usages с file:line и assembly. Отдельно перечислите потребителей вне nextorm.core, включая tests, либо явно подтвердите отсутствие. Разрешите расхождение счётчика evidence pack; не используйте текстовый поиск для symbols.

**GATHER-POST — точный запрос scout:**

> Проверьте итоговый D175-specific diff и соответствие audit: для каждого планового H/X site разрешите фактический comparer и assertion; для сохранённого hash NotBe укажите документированный самостоятельный intent; подтвердите сохранение C/X контрактов и исходных вариантов. Дайте итоговое mapping стабильных variant IDs на фактические symbols/file:line.

### Строки контракта

| Row / requirement IDs | rv | Applicability predicate | Owner | Проверка / сценарий | Planned evidence kinds и sources | Точный вызов | Требуемый результат / лог | Артефакт |
|---|---:|---|---|---|---|---|---|---|
| EC175-01 / R175-HASH, SEM, COH | 1 | Всегда: sites перечислены в pack | `G:scout` | Полная начальная классификация H/C/X и T-вариантов | Семантический audit; Roslyn по существующим tests/comparers | `Task → scout: GATHER-INTENT` | Все IDs закрыты; comparer и selectors разрешены; нет догадок о назначении | `intent-audit.md` |
| EC175-02 / R175-HASH, SEM, COH | 1 | Всегда после DO | `G:scout` | Проверка итоговых assertion и сохранения сценариев | Семантический post-change audit + D175 diff | `Task → scout: GATHER-POST` | Каждый plan-inequality site имеет Equals false; retained NotBe объяснён; execution/coherence сохранены | `assertion-audit.md`, `variant-evidence.md` |
| EC175-03 / R175-ASSIGN | 1 | Всегда | `G:scout` | Visibility и consumers Assign | Roslyn declaration/refs, assembly classification | `Task → scout: GATHER-ASSIGN` | Internal; известные core usages; отсутствие внешнего named consumer; расхождение count разрешено | `assign-refs.md` |
| EC175-04 / R175-TEST | 1 | Всегда | `T:coder` | Core focused baseline + post-change | Runtime test logs, подтверждённые selectors из EC175-01 | `CORE-FOCUSED` до/после правок | Exit 0; каждый требуемый fixture исполняется; целевые тесты не skipped. Baseline failures явно зарегистрированы | `core-focused-baseline.log/.exit`, `core-focused-post.log/.exit` |
| EC175-05 / R175-TEST | 1 | Всегда | `T:coder` | SQLite focused baseline + post-change | Runtime test logs, selectors из EC175-01 | `SQLITE-FOCUSED` до/после правок | Exit 0; обе целевые fixture группы исполняются; нет целевых skips | `sqlite-focused-baseline.log/.exit`, `sqlite-focused-post.log/.exit` |
| EC175-06 / R175-TEST | 1 | Всегда после правок | `T:coder` | Полная core regression suite | Runtime suite log | `CORE-FULL` | Exit 0, ненулевое исполнение; failures отсутствуют; skips раскрыты | `core-full.log/.exit` |
| EC175-07 / R175-TEST | 1 | Всегда после правок | `T:coder` | Полная SQLite regression suite | Runtime suite log | `SQLITE-FULL` | Exit 0, ненулевое исполнение; failures отсутствуют; skips раскрыты | `sqlite-full.log/.exit` |
| EC175-08 / R175-TEST, EVIDENCE | 1 | Запуск: D175-specific integration diff есть. Guard: его нет | `T:coder` | Общий JoinInto сценарий у всех провайдеров либо доказательство отсутствия изменения | При true: integration logs + provider execution report. При false: initial/final diff comparison | True: `INTEGRATION`; false: `git diff -- tests/nextorm.integration.tests` и `git diff --cached -- tests/nextorm.integration.tests`, сравнить с начальным snapshot | True: exit 0, контейнерные providers не skipped. False: подтверждено отсутствие D175 delta, не заявлен выполненный suite | `integration.log/.exit`, `providers.md` **или** `integration-guard.md` |
| EC175-09 / R175-BUILD | 1 | Всегда | `T:coder` | Solution build | Build stdout/stderr и exit code | `BUILD` | Exit 0; 0 warnings / 0 errors | `build.log/.exit` |
| EC175-10 / R175-SCOPE, EVIDENCE | 1 | Всегда | `C:check` | Branch/base, scope, CRLF, docs, coverage и mutation guards | Начальный/final git evidence, D175-specific diff, line-ending report | `git branch --show-current`; `git rev-parse HEAD`; `git merge-base --is-ancestor 18659e41 HEAD`; `git status --porcelain=v1`; `git diff --check`; `git diff --binary`; `git diff --cached --binary`; line-ending report coder | Branch 1.0.9-rc2; ancestry exit 0; diff-check exit 0; CRLF; product delta 0; public docs/config неизменны; coverage/Stryker решения применимы | `initial-state.md`, `final-state.md`, `d175.patch`, `scope-guards.md` |

`C:check` проверяет и фиксирует результат строки EC175-10; техническое получение командных evidence делегируется coder.

### Ревизии и возврат в PLAN

Текущая версия — **rv1**, supersession отсутствует.

Обоснованная смена действий/зависимостей увеличивает `r` и, при изменении evidence contract, `rv`; новая попытка начинается с `N=1`. Запись должна явно указывать `rv1 → rv2`, сохранять IDs и прежние обязательства, добавлять IDs новых вариантов.

Уточнение audit или получение отсутствующего лога само по себе не меняет ревизию и не сбрасывает попытки.

## Классификация DO → PLAN

Для текущего initial PLAN возврата ещё нет. При появлении отчёта:

- Неясный intent/comparer/consumer → **недостаток доказательств**, сначала адресный scout.
- Требуемый дополнительный тестовый шаг в текущих полномочиях → **предусловие**, добавить активную единицу; D175 остаётся active/blocked, критерии и остаток сохраняются.
- Новый Equals assertion выявил продуктовый дефект → отдельная новая `P:`-задача для решения о runtime-предусловии; не объявлять D175 done.
- Истинный блокер вне ресурсов/полномочий → рекомендовать оркестратору вызвать `escalate`.
- Стойкая низкая уверенность после точечного scout → `escalate`, **триггер 5**, даже без доказанной внешности блокера.

## Уверенность и риски

**Уверенность:** высокая в минимальном подходе и сохранении `Assign internal`; средняя в точном составе тестовых правок до адресной классификации.

Основные риски:

1. Ошибочно принять hash-specific или entity test за plan inequality.
2. Сравнить другой уровень плана и получить бессодержательный зелёный assertion.
3. Удалить execution/cache oracle вместо усиления планового oracle.
4. Скрыть обнаруженный comparer-дефект изменением operands.
5. Получить ложный green из пустого selector или skipped integration providers.
6. Затронуть существующие пользовательские изменения или нарушить CRLF.

Все риски закрываются audit, сохранением сценариев, позитивным evidence исполнения и scope guards. Неограниченного discovery и скрытого расширения scope нет.

## Progress log

- 2026-10-08T12:28:46Z | D:175.1 | r1 | n=1/3 | branch `1.0.9-rc2` @ `04836505`; base `18659e41` ancestor exit 0; tracked tree clean (только untracked status-файлы). Roslyn-классификация H01–H08: все 8 сайтов имеют paired `Equals == false` на верном comparer; hash `NotBe` — лишний/коллизионный оракул (нет hash-inequality инварианта; `EqualHashes_ShouldNotImplyEquality`, `QueryPlanStore_ForcedHashCollision`). `Assign` `internal` @ `EntityBuilderEagerLoading.cs:307`; def + 6 refs, все внутри `nextorm.core` (pack «6» = refs); внешних потребителей нет, widening не нужен. Baseline: CORE-FOCUSED exit 0 (252/0/0); SQLITE-FOCUSED exit 0 (16/0/0). | `/tmp/nextorm-D175/r1/{initial-state.txt,intent-audit.md,core-focused-baseline.log,sqlite-focused-baseline.log}`
- 2026-10-08T12:28:46Z | D:175.2 | r1 | n=1/3 | правки test-only, 5 файлов, +4/−16, CRLF, `git diff --check` exit 0: удалены 8 hash-`NotBe` (H01–H08); переименованы `SameSqlAndColumns_DifferentEntityType_NotEqual`, `SameTypeSameColumnCount_DifferentColumnNames_NotEqual`; обновлён summary `SelectExpressionPlanEqualityComparerTests`. Coherence (hash `Be`) и execution/identity (`Executions`, `ReferenceEquals`) сохранены; `JoinIntoPlanKeyTests.cs` не менялся; operands/сценарии не тронуты. | `/tmp/nextorm-D175/r1/d175.patch`
- 2026-10-08T12:28:46Z | D:175.3 | r1 | n=1/3 | post focused: CORE-FOCUSED exit 0 (252/0/0), SQLITE-FOCUSED exit 0 (16/0/0); individual selectors 8/8 exit 0 (4+7+60+19+154+8=252 core, 9+7=16 sqlite), 0 skipped. CORE-FULL `dotnet test tests/nextorm.core.tests -c Debug` exit 0 (1767/0/0); SQLITE-FULL `dotnet test tests/nextorm.sqlite.tests -c Debug` exit 0 (1161/0/1) — 1 env-gated skip `SqliteRowIdLobProbeTests.Sqlite_RowId_Projection_Streams_Lobs_MemoryBounded` (`NEXTORM_LOB_SQLITE_PROBE=1`); BUILD `dotnet build nextorm.slnx -c Debug` exit 0, 0 warnings / 0 errors. Integration predicate false: `git diff -- tests/nextorm.integration.tests` пуст, suite не запускался. Validator: brief exit 0; report exit 2, единственный FAIL `more than one comprehensive boundary test sweep: 2` — helper cap (1 comprehensive sweep) vs план, требующий CORE-FULL + SQLITE-FULL; записанное принятое отклонение (ср. rc1-126, rc1-141). | `/tmp/nextorm-D175/r1/{evidence.json,validator-brief.txt,validator-report.txt,core-full.log,sqlite-full.log,build.log,integration-guard.txt}`

- 2026-10-08T17:41:00Z | STOP | r1 | n=4/3 | terminal STOP bookkeeping — escalation decision **(c) STOP**; defect family «rv1 evidence-completeness / row-bound ledger provenance», 4× CHECK fail; no product defect demonstrated (H01–H08 clean, COH/SEM supported, retained hash `NotBe` accepted); scope = 5 core test files; patch `docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch`; product/test paths restored to green tip `04836505`; #175 stays OPEN; no DO re-attempt, no 5th CHECK | `docs/specs/status/rc2-175-evidence/`

## STOP — terminal

- **status: INCOMPLETE (terminal STOP).** Эскалация вынесла решение **(c) STOP**; DO re-attempt не выполняется, 5-й CHECK не проводится.
- **escalation decision: (c) STOP.**
- **Defect family:** «rv1 evidence-completeness / row-bound ledger provenance» — 4× CHECK fail по одному семейству.
- **Продуктовый дефект НЕ продемонстрирован:** H01–H08 проверены чисто; COH/SEM подтверждены; сохранённый hash `NotBe` принят; runtime/API diff отсутствует.
- **Зелёные гейты:** build exit 0 (0 warnings / 0 errors); CORE-FULL 1767; SQLITE-FULL 1161 с 1 раскрытым env-gated skip; focused 252 + 16.
- **Scope:** ровно 5 файлов `tests/nextorm.core.tests/*` (см. patch).
- **Validator:** `report` exit 2 — единственный FAIL «more than one comprehensive boundary test sweep: 2» (single-boundary cap vs требуемые CORE-FULL + SQLITE-FULL); записанное принятое отклонение.
- **Patch:** `docs/specs/status/rc2-175-evidence/D175-STOP-incomplete.patch`; ключевые артефакты — `docs/specs/status/rc2-175-evidence/`.
- **Issue #175 остаётся OPEN.**
- **Next allowed step:** none for D175.
