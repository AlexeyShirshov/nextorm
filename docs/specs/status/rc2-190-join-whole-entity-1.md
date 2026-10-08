# D190 — Fix whole-entity selection from JOIN projections

- task_id: D190
- github issue: #190 — https://github.com/AlexeyShirshov/nextorm/issues/190
- selected_variant: pdca-dotnet
- cycle_id: rc2-190-join-whole-entity-1
- plan_revision: r1
- iteration: n1/3
- plan_state: ready
- base: 18659e41 (branch 1.0.9-rc2)
- status file: docs/specs/status/rc2-190-join-whole-entity-1.md (UTF-8, CRLF)

## Статус цикла

- **task_id:** D190
- **github issue:** #190 — https://github.com/AlexeyShirshov/nextorm/issues/190
- **selected_variant:** расширение существующего entity-item projection и прямое возвращение entity-item из materializer
- **cycle_id:** rc2-190-join-whole-entity-1; N=1
- **plan_revision:** r1
- **iteration:** n1
- **plan_state:** ready; DO разрешён только после выполнения перечисленных pre-DO prerequisites
- **base:** `18659e41`; перед DO записать фактический HEAD и отличие от этой базы
- **status-file role:** авторитетный PLAN, evidence contract и последующий DO ledger; не заменяет фактические логи
- **status file:** `docs/specs/status/rc2-190-join-whole-entity-1.md`, UTF-8, CRLF
- **footprint (planned, with uncertainty):** projection planning, root materialization, cache identity; возможная локальная коррекция in-memory null handling; тесты core/sqlite/postgres/integration; EN/RU статьи и внутренние регистры. Точные неопределённости перечислены ниже.
- **predecessor-result requirements:** обязательного результата D173 нет; обязательны исключительное владение пересекающимися файлами и проверка актуальной базы. D173 на указанной базе не применён.
- **assumptions/prerequisites:** .NET 10; действующие mappings; отсутствие параллельного редактирования общего footprint; доступность контейнерной инфраструктуры либо выполнение штатного восстановления Podman.
- **pre-DO prerequisite:** записать этот контракт, structured test scope, владение файлами, skill audit и фактическую базу; подтвердить ограничения гейта 1.
- **next step:** `coder` записывает PLAN, выполняет pre-DO prerequisite и начинает D190-01 в режиме подтверждения, установленном драйвером collection. В autonomous нет дополнительного `go` или ожидания; в normal сохраняется пользовательский gate.

---

## PLAN

**Plan revision:** r1, first DO attempt n1  
**Evidence contract:** rv1

### 1. Цель и границы

Поддержать прямую проекцию целой присоединённой сущности:

```text
Select(p => p.ItemN)
```

через существующие entity-item mapping и materialization, включая правильное поведение отсутствующей стороны outer join и повторное использование планов.

**Не расширяем объём:**

- никаких новых публичных API;
- никаких wildcard `SELECT`;
- никаких сравнений целых сущностей в `WHERE`;
- никаких изменений произвольных member projections, относящихся к D173;
- scalar/composite/nested projections и bare-query поведение сохраняются;
- не меняем provider defaults, TVP metadata, временные таблицы;
- не меняем milestone: задача остаётся в `1.0.9-rc2`.

### 2. Критерии приёмки

| ID | Критерий и проверка | Негативный случай |
|---|---|---|
| R190-01 | Поддерживаемый `ItemN` распознаётся как целая сущность; SQL содержит её упорядоченные mapped columns, материализуется заявленный result type. Проверки первых, промежуточных и последних слотов поддерживаемых join arities. | Обычный entity-valued member, не являющийся распознанным `Projection<T…>.ItemN`, не получает случайного разрешения; `SELECT *` не появляется. |
| R190-02 | Отсутствующая reference-сторона outer join возвращает `null`, присутствующая — корректную сущность; in-memory и SQL дают согласованные результаты. | Сущность с допустимыми нулевыми/default scalar values не превращается в отсутствующую; соседняя присутствующая сторона не обнуляется. |
| R190-03 | Сохраняются scalar, composite, nested `new { Entity = p.ItemN }`, bare queries и существующие ограничения mapping/constructor. | Unsupported mapping/constructor по-прежнему выдаёт штатную ошибку, а не частично заполненный объект или обход проверки. |
| R190-04 | Различные слоты, result shapes и реально значимые column/source bindings не переиспользуют несовместимый SQL/mapper; одинаковые формы сохраняют cache reuse. | Чередование двух проекций на одном context не даёт alias/slot leak, неправильного mapper или sticky отключения cache. |
| R190-05 | Работает стандартный SQL/entity materialization путь у SQLite, PostgreSQL, SQL Server, MySQL, MariaDB и ClickHouse; обязательные container-backed прогоны не заменяются skipped. | Provider-only pass или запуск integration без реально выполненных обязательных провайдеров не считается доказательством переносимости. |
| R190-06 | Сборка без ошибок и предупреждений; coverage включённых модулей не ниже 85% line / 75% branch; branch delta и mutation outcome раскрыты. | Зелёные тесты без branch/coverage evidence либо необъяснённые surviving mutants не закрывают критерий. |
| R190-07 | Acceptance benchmark: ровно 7 cases, 0 failures, wall time ≤240 s; зафиксированы Mean, Allocated, cached/prepared ratio и сравнение с baseline. | Пропущенные cases, превышение времени, отсутствие ratio либо необъяснённая сопоставимая регрессия не закрывают строку. |
| R190-08 | EN/RU документация больше не объявляет поддержанный сценарий запрещённым; ограничения и регистры согласованы, без преждевременной сертификации. | Обновление только одного языка, ссылка из публичных docs на specs или запись «закрыто» без CHECK недопустимы. |
| R190-09 | Соблюдены scope, test safety, single boundary sweep и полнота evidence contract. | Missing evidence, broad inner-loop test run, выдуманный selected count или неисполненная команда не допускают PASS. |

### 3. Минимальное решение

**Цель по сути:** добавить отсутствующий root entity-item projection, а не новый механизм projection.

**Непреложные ограничения:** typed explicit columns, прежние mapping/null semantics, совместимый cache identity, неизменность shared `QueryCommand`, сохранение остальных форм.

**Оптимум в этих ограничениях:** распознать direct `ItemN` до scalar member translation, использовать существующее entity-item expansion и возвращать построенную entity expression непосредственно для соответствующего root result type.

| Подход | Плюсы | Минусы, цена и риск |
|---|---|---|
| **Существующее entity expansion + root entity passthrough** | Единые mapping/column/null rules; нет публичного API и дополнительной оболочки | Нужно явно проверить mapper signature и in-memory null semantics; локальная средняя цена |
| Переписывание в синтетическую nested projection с последующим unwrap | Можно переиспользовать nested форму | Дополнительная форма и возможные allocation/cache различия; сложнее обеспечить result type |
| Независимое построение member-init для каждого provider/слота | Локально очевидный SQL | Дублирование mapping/constructor/null правил, высокая цена сопровождения и расхождения |

**Выбор:** первый подход.

Конкретные действия:

1. В `QueryCommand.QueryPreparer.cs:458-518,679-689,789,883` распознавать только подтверждённый entity-item текущей joined projection. Не добавлять общий permissive fallback в `MemberTranslator.cs:416-576`.
2. В `RowMaterializerBuilder.cs:143-188,226-253` добавить root passthrough для соответствующего single entity item вместо повторного binding result type constructor/member-init. Переиспользовать существующий entity builder и all-NULL guard.
3. В `InMemoryRowMaterializer.cs:19,35-36,51,73` сохранить identity semantics. Для отсутствующего entity-item использовать фактический `null` исходного item либо эквивалентный корректный null predicate; **не определять отсутствие через `default` всех scalar values**.
4. В comparer/signature включать только семантически необходимые discriminators direct root shape и source/column binding. Если существующий ключ уже различает конкретную размерность, не добавлять дублирующее поле. Проверить это тестом и структурным evidence.
5. Не менять `QueryCommand.Cache`/sticky `_dontCache` ради отдельной проекции.

### 4. Чего не сказала постановка

| Пробел | Закрытие |
|---|---|
| Где проходит граница direct entity projection? | Только распознанный `ItemN` joined `Projection<T1…T8>`; явные casts к unrelated/object result и другие entity-valued expressions deferred до отдельного требования. |
| Как вести себя при all-NULL entity columns? | Сохраняем существующую семантику `BuildEntityItem:239-253`; не вводим новое определение присутствия строки. Возможная неоднозначность полностью nullable mapping документируется как существующая. |
| Поддерживаются ли entity structs? | Не вводим такую поддержку. Для уже поддерживаемого value-type mapping проверяем прежние semantics; для запрещённого — фиксируем существующий guard и его негативный тест. |
| Что делать при отсутствии usable constructor/mapping? | Сохраняем штатные ошибки существующего nested entity-item пути, проверяем regression. |
| D173 должен быть завершён первым? | Нет: он не применён на базе. Нужно сериализовать общий footprint; при изменившейся базе повторить targeted cache tests и сохранить оба контракта. |
| Что означает ссылка на #105 в происхождении? | Неподтверждённая provenance assumption: репозиторный #105 — navigation properties. Не использовать эту ссылку как доказательство реализации. |
| Есть ли validator inner-loop evidence? | Нет; применяется явное отклонение ниже, без ослабления содержательных ограничений. |
| Доступны ли mutation tool и контейнеры? | Проверяются в DO. Контейнеры восстанавливаются штатным способом. Для отсутствующего mutation tooling предусмотрена честная отчётность, не выдуманный результат. |

### 5. Матрица вариантов — до тест-кейсов

`test` означает обязательную проверку; `guard` — доказанный существующий запрет с негативной проверкой. Deferred не относится к исходным критериям приёмки.

| Вариант | Закрытие |
|---|---|
| Direct `Item1`, промежуточный и последний доступный `ItemN`; arities 2–8 | **test:** core/SQL shape; parameterized cases в существующих projection/join tests |
| Выбор разных сторон self join с одинаковым EntityType | **test:** values, slots, aliases, cache alternation |
| Inner join; nullable сторона left join; обе выбранные стороны | **test:** SQLite execution, in-memory и provider integration |
| Отсутствующий reference item / all-NULL DB item | **test:** `null`, без constructor exception |
| Присутствующая сущность с `0`, `false`, default scalar и nullable columns | **test:** не ошибочно `null` |
| Null/default/uninitialized in-memory item | **test:** поведение отсутствующего item; не расширять контракт некорректной projection |
| Reference entities | **test:** основной сценарий |
| Value entities | **test** для существующей поддержки; иначе **guard** с доказательством прежнего запрета |
| Missing usable constructor; missing/invalid mapping | **guard + test:** штатный отказ без обхода |
| Direct root; nested whole object; explicit scalar/composite; bare | **test:** root result и regression остальных форм |
| Materialized entity, не являющаяся joined `ItemN`; entity comparison в WHERE | **guard:** новый resolver их не принимает |
| Explicit cast к object/unrelated type, arbitrary whole-object expressions | **deferred:** триггер — отдельное согласованное требование; прежнее поведение сохраняется |
| Cached / prepared, повторное использование одного context | **test:** cache identity и отсутствие sticky mutations; **perf** |
| Provider SQLite | **test:** SQL shape + исполнение |
| PostgreSQL | **test:** SQL shape, typed CTE и container integration |
| SQL Server / MySQL / ClickHouse | **test:** provider SQL shape + container integration |
| MariaDB | **test:** dialect tests; standalone live MariaDB **deferred**, триггер — provider-specific execution regression/отдельная infra задача |
| Temp-table/TVP-specific branches | **guard / условное N/A:** diff и semantic impact не затрагивают эти ветви; иначе новый обязательный вариант через PLAN revision |

### 6. Конкретные DO-единицы

Пути с `**` — точные ограниченные globs по известным именам файлов, не утверждение о непроверенном полном пути. Новые test sources явно **планируемые**, не evidence существования.

| Единица | Fix now, footprint и результат |
|---|---|
| **D190-01 — preflight и red regressions** | `src/nextorm.core/**/{QueryCommand.QueryPreparer,RowMaterializerBuilder,InMemoryRowMaterializer,SelectExpressionPlanEqualityComparer,RowMapperFactory}.cs`; существующие `tests/nextorm.core.tests/**/{EntityItemProjectionInMemoryTests,RowMaterializerBuilderTests,SelectExpressionPlanEqualityComparerTests,PlanKeyUniquenessTests,JoinReturningIdentityTests}.cs`, `tests/nextorm.sqlite.tests/**/EntityItemProjectionTests.cs`. Закрепить scope/ownership, добавить regression cases и получить содержательный red до production fix. |
| **D190-02 — root projection и materialization** | `QueryCommand.QueryPreparer.cs:458-518,679-689,789,883`; `RowMaterializerBuilder.cs:41,72,143-188,226-253`; `InMemoryRowMaterializer.cs:19,35-36,51,73`. Реализовать выбранное решение и green core/sqlite. |
| **D190-03 — cache correctness** | `SelectExpressionPlanEqualityComparer.cs:41-76,116`; `RowMapperFactory.cs:456-481`; перечисленные comparer/plan-key/join tests. Сначала тесты distinguish/reuse, затем только необходимая коррекция ключа/signature. D173 folding не включать. |
| **D190-04 — providers и интеграции** | Существующие `tests/nextorm.postgres.tests/**/{TypedCteSqlGenerationTests,JoinReturningIdentitySqlGenerationTests}.cs`; планируемый `tests/nextorm.integration.tests/CommonTestSuite.JoinWholeEntity.cs`; существующие `CommonTestSuite.Cte.cs:136,163` и `PostgresJoinAritiesTests.cs:16`. Планируемые provider-local `JoinWholeEntitySqlGenerationTests.cs` в sqlite/postgres/sqlserver/mysql/mariadb/clickhouse tests, если существующие focused classes не покрывают нужный SQL shape. |
| **D190-05 — qualification** | Финальная сборка, единственный boundary sweep с coverage, branch delta, mutation outcome, acceptance benchmark; planned artifacts `artifacts/pdca/rc2-190/`. |
| **D190-06 — docs и reconciliation** | `docs/advanced/limitations.md:20`, `docs/ru/advanced/limitations.md:20`, `docs/guide/02-joins.md:104-106`, `docs/ru/guide/02-joins.md:106`, `docs/specs/design/code-smells-review.md:1770-1773`, `docs/specs/design/API-NAMING-REVIEW.md` §«Закрытие планов», текущий status file. |

**Deferred findings:**

- #105 provenance correction за пределами проверяемого факта — триггер: отдельная подтверждённая история изменений.
- Общее projection folding D173 — триггер: его собственный цикл.
- Новые casts, wildcard и entity comparison — триггер: отдельное согласованное требование.
- Широкие unrelated suppressions/slop — не исправлять попутно; регистрировать `file:line` и отдельный trigger. В затронутом execution path новые дефекты исправляются сейчас.

### 7. Режим единиц и пересечения

**Последовательно в одном дереве.** D190-01 → D190-02 → D190-03 → D190-04 → D190-05/D190-06 → CHECK.

Worktree не нужен: задача связана общими planning/materialization/cache contracts, а независимого implementation footprint нет.

До редактирования драйвер резервирует общие файлы против D173, D176, D160, D159/184, D178/185. Они не становятся предшественниками автоматически.

Если overlap уже применён:

- записать фактический diff относительно `18659e41`;
- сохранить требования обоих циклов;
- выполнить relevant focused regressions;
- при несовместимом изменении объёма вернуть DO → PLAN с конкретным дефектом/предусловием, а не молча заменить D190.

### 8. Тест-стратегия и безопасность

#### Уровни

- **Unit:** shape recognition, materializer result/null semantics, mapping guards, equality/signature, cache reuse.
- **SQL shape:** explicit columns, aliases/slots, отсутствие wildcard, provider rendering.
- **Execution:** SQLite и in-memory.
- **Integration:** direct entity-item cases в общей suite для реально выполняемых провайдеров; существующие typed CTE/self-join regressions.
- **Red → green:** новые direct projection/null/cache cases сначала должны падать из-за проверяемого поведения, не из-за build/infra/пустого фильтра.

Планируемые новые integration methods используют focused selector token `JoinWholeEntity_`. Это **планируемый selector**, а не заявление о существующем symbol. DO ledger заменяет планируемый источник actual Roslyn/test discovery references и `file:line`.

#### Обозначения точных вызовов

Все команды исполняются из корня репозитория. `$E`:

```bash
E=artifacts/pdca/rc2-190
```

DO использует следующие shell functions; их текст и actual argument arrays записываются в evidence artifacts:

```bash
run_logged() {
  local id="$1"
  shift
  mkdir -p "$E/$id"
  printf '%s\0' "$@" > "$E/$id/argv.nul"
  set -o pipefail
  "$@" 2>&1 | tee "$E/$id/run.log"
  local rc=${PIPESTATUS[0]}
  printf '%s\n' "$rc" > "$E/$id/exit-code.txt"
  return "$rc"
}

T() {
  local id="$1" project="$2" filter="$3"
  run_logged "$id" env \
    DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 \
    DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
    timeout 600 dotnet test "$project" -c Debug --no-build \
    --filter "$filter" --verbosity normal \
    --results-directory "$E/$id/results"
}
```

`T` запускается **строго последовательно**. Для каждого запуска сохраняется `selection.json`: actual arguments, фильтр, observed `selected_count`, passed/failed/skipped, источник этих чисел. `selected_count ≥ 1`; count нельзя получать из предположения по имени файла.

Отсутствие файлов runner results допускается только с явным observed runner behavior: тогда лог и структурная запись результатов обязательны. Логи/argv/exit code никогда не N/A.

#### Per-unit test scope JSON

Этот JSON — обязательный DO brief. `planned` selectors не считаются доказательством до test discovery.

```json
{
  "schema": "nextorm-pdca-test-scope/1",
  "task": "D190",
  "plan_revision": "r1",
  "evidence_revision": "rv1",
  "parallel_test_projects": false,
  "safety": {
    "DOTNET_GCHeapHardLimit": "0x80000000",
    "DOTNET_gcServer": "0",
    "timeout_seconds": 600
  },
  "units": [
    {
      "id": "D190-01",
      "selectors": [
        {
          "project": "tests/nextorm.core.tests",
          "filter": "FullyQualifiedName~EntityItemProjectionInMemoryTests|FullyQualifiedName~RowMaterializerBuilderTests"
        },
        {
          "project": "tests/nextorm.sqlite.tests",
          "filter": "FullyQualifiedName~EntityItemProjectionTests"
        }
      ],
      "phase": "red",
      "selected_count_min": 1
    },
    {
      "id": "D190-02",
      "selectors": [
        {
          "project": "tests/nextorm.core.tests",
          "filter": "FullyQualifiedName~EntityItemProjectionInMemoryTests|FullyQualifiedName~RowMaterializerBuilderTests|FullyQualifiedName~JoinReturningIdentityTests"
        },
        {
          "project": "tests/nextorm.sqlite.tests",
          "filter": "FullyQualifiedName~EntityItemProjectionTests"
        }
      ],
      "phase": "green",
      "selected_count_min": 1
    },
    {
      "id": "D190-03",
      "selectors": [
        {
          "project": "tests/nextorm.core.tests",
          "filter": "FullyQualifiedName~SelectExpressionPlanEqualityComparerTests|FullyQualifiedName~PlanKeyUniquenessTests|FullyQualifiedName~JoinReturningIdentityTests"
        }
      ],
      "phase": "green",
      "selected_count_min": 1
    },
    {
      "id": "D190-04",
      "selectors": [
        {
          "project": "tests/nextorm.postgres.tests",
          "filter": "FullyQualifiedName~TypedCteSqlGenerationTests|FullyQualifiedName~JoinReturningIdentitySqlGenerationTests|FullyQualifiedName~JoinWholeEntitySqlGenerationTests",
          "new_selector_component": "planned"
        },
        {
          "project": "tests/nextorm.sqlite.tests",
          "filter": "FullyQualifiedName~EntityItemProjectionTests|FullyQualifiedName~JoinWholeEntitySqlGenerationTests",
          "new_selector_component": "planned"
        },
        {
          "project": "tests/nextorm.sqlserver.tests",
          "filter": "FullyQualifiedName~JoinWholeEntitySqlGenerationTests",
          "source": "planned"
        },
        {
          "project": "tests/nextorm.mysql.tests",
          "filter": "FullyQualifiedName~JoinWholeEntitySqlGenerationTests",
          "source": "planned"
        },
        {
          "project": "tests/nextorm.mariadb.tests",
          "filter": "FullyQualifiedName~JoinWholeEntitySqlGenerationTests",
          "source": "planned"
        },
        {
          "project": "tests/nextorm.clickhouse.tests",
          "filter": "FullyQualifiedName~JoinWholeEntitySqlGenerationTests",
          "source": "planned"
        },
        {
          "project": "tests/nextorm.integration.tests",
          "filter": "FullyQualifiedName~JoinWholeEntity_",
          "source": "planned"
        },
        {
          "project": "tests/nextorm.integration.tests",
          "filter": "FullyQualifiedName~Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData|FullyQualifiedName~Cte_Typed_SelfJoin_ShouldReturnBothSides|FullyQualifiedName~JoinArities_ToSqlAndUpdateJoin_ShouldRender"
        }
      ],
      "phase": "green",
      "selected_count_min": 1
    },
    {
      "id": "D190-05",
      "selectors": [],
      "phase": "qualification",
      "boundary_sweep": "BOUNDARY-01"
    },
    {
      "id": "D190-06",
      "selectors": [],
      "phase": "documentation",
      "verification": "DocFX plus EN/RU and register audit"
    }
  ],
  "single_boundary_sweep": {
    "id": "BOUNDARY-01",
    "after": [
      "D190-02",
      "D190-03",
      "D190-04",
      "all implementation and test edits"
    ],
    "mode": "one sequential full-solution coverage run"
  }
}
```

Новый selector component в объединённом фильтре проверяется отдельно discovery: успешный старый class не может скрыть нулевой selection нового class.

#### Boundary, coverage и branches

После всех implementation/test edits выполнить:

```bash
run_logged EV190-BUILD dotnet build nextorm.slnx -c Debug
```

Один boundary sweep одновременно даёт полное regression и coverage evidence:

```bash
run_logged EV190-COVERAGE env \
  DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 \
  DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
  timeout 600 dotnet tool run dotnet-coverage collect \
  -s coverage.settings.xml -f cobertura \
  -o tests/coverage/coverage.cobertura.xml \
  "dotnet test --no-build --verbosity normal -c Debug --max-parallel-test-modules 1"
```

```bash
run_logged EV190-COVERAGE-REPORT dotnet tool run reportgenerator \
  -reports:tests/coverage/coverage.cobertura.xml \
  -targetdir:tests/coverage/report \
  '-reporttypes:Html;TextSummary;Cobertura' \
  '-riskhotspotassemblyfilters:+nextorm.*'
```

- `--max-parallel-test-modules 1` запрещает одновременный запуск test projects.
- Не заменять этот boundary sweep несколькими broad inner-loop прогонами.
- Существующие CI thresholds 85/75 — нижняя граница приёмки D190, независимо от warning-only поведения feature branch.
- Отчёт относится к configured modules core/sqlite/postgres/sqlserver; excluded modules не объявлять измеренными.
- До исправления снять baseline branch totals через coverage того же **focused red scope**, затем после исправления — focused green scope. Это не второй broad sweep.
- Зафиксировать changed-type/changed-branch delta: total, covered, новые/uncovered ветви и связь с matrix rows.

#### Mutation

Scoped mutation — только изменённые production types core, с core/sqlite focused test scope. Планируемый config:

`artifacts/pdca/rc2-190/mutation/stryker-config.json`

Он должен перечислять actual touched files, test projects и фильтры, исключать unrelated assembly-wide mutations и сохранять JSON/HTML reports.

Проверка tooling:

```bash
run_logged EV190-MUTATION-TOOLS dotnet tool list --local
run_logged EV190-MUTATION-TOOLS-GLOBAL dotnet tool list --global
```

Если доступен local `dotnet-stryker`:

```bash
run_logged EV190-MUTATION env \
  DOTNET_GCHeapHardLimit=0x80000000 DOTNET_gcServer=0 \
  timeout 600 dotnet tool run dotnet-stryker \
  --config-file artifacts/pdca/rc2-190/mutation/stryker-config.json
```

Для global tool — такой же вызов `dotnet-stryker --config-file …`, с actual argv в ledger.

Survivors: убить дополнительным focused test либо обосновать каждый actual mutant и соответствующую ветвь. Отсутствующее tooling — не «mutation passed»: записать установленный факт отсутствия/неработоспособности, испробованные разрешённые варианты и перечень непроверенных mutation branches. Это разрешённое контрактом disclosure, а не замена coverage/tests.

#### Notice: отклонение inner-loop validator

`scripts/validate_inner_loop.py` отсутствует. `iteration15_evidence.py` и perf gates не являются заменой.

В rv1 применяется **manual evidence gate**:

- DO brief содержит приведённый structured JSON;
- ledger содержит exact argv/filter, actual selected counts, exit codes и logs;
- CHECK независимо проверяет raw facts;
- никаких whole-project inner-loop runs;
- никаких broad filters вместо перечисленного scope;
- `selected_count ≥ 1`;
- каждый `--no-build` имеет предшествующий успешный build после relevant edits;
- один boundary sweep.

Missing reporting собирается внутри CHECK; не выдаётся за product defect.

### 9. Приоритеты и design checklist

#### P1 по построению

| P1 область | Обязательные строки |
|---|---|
| Инварианты задания: direct root, null/default, сохранение форм | EV190-RED, EV190-ROOT, EV190-NULL, EV190-REGRESSION |
| Query-path / plan-cache priority table | EV190-CACHE, EV190-PERF, EV190-SHARED, EV190-TEMP |
| Cross-provider execution | EV190-SQL, EV190-INTEGRATION-DIRECT, EV190-INTEGRATION-REGRESSION |
| Build/coverage/branch correctness | EV190-BUILD, EV190-COVERAGE, EV190-BRANCH |
| Evidence completeness/test safety | EV190-SCOPE |

CHECK не понижает эти строки из-за малого diff или отсутствующего отчёта.

#### Design checklist

- **SOLID/DRY:** один entity expansion и один entity construction/null path; provider-specific дублирование запрещено.
- **Type design:** typed result, никаких permissive `object` fallbacks, новых публичных типов, свойств или flags.
- **Cache correctness:** shape/source/slot identity проверяется отдельно от alias naming; ключ не использует случайные per-call объекты.
- **Perf anti-patterns:** нет дополнительных per-row wrappers, reflection, LINQ allocation, boxing либо нового поиска metadata при materialization.
- **Structural sealedness:** посчитать затронутые class declarations и sealed count/ratio; новые internal leaf classes, если действительно нужны, sealed. Не sealing существующие публичные extensibility points ради ratio.
- **Suppressions/slop:** actual scan с counts/denominator/ratio, а не «просмотрено»; findings имеют `file:line`. Новые suppressions требуют конкретного обоснования.

### 10. Документация и регистры

**Меняем EN+RU:**

- limitations: убрать запрет ровно для поддержанного direct `ItemN`;
- joins guide: пример direct whole entity, обе стороны join, nullable outer side;
- сохранить ограничения wildcard/entity comparison/casts;
- не ссылаться из публичных страниц на `docs/specs/**`.

**XML-doc:** новый публичный API отсутствует, обязательного нового XML-doc нет. Если существующий XML-comment `Select` противоречит новой поддержке, исправить его локально в `src/nextorm.core/**/EntityBuilder.cs`; не выполнять unrelated doc cleanup.

**Регистры:**

- обновить finding `code-smells-review.md:1770-1773` ссылкой на #190 и статусом, соответствующим фактической фазе;
- подготовить запись закрытия в `API-NAMING-REVIEW.md` §«Закрытие планов»;
- автор изменения — `coder`, certifier — отдельный `check`; идентичности/роли фиксируются;
- финальное закрытие — только после PASS, не при заявлении DO «готово»;
- найти относящийся к D190 TODO/plan document. Полностью реализованный TODO удалить и делинковать references; текущий status/evidence не удалять. Если отдельного TODO нет, сохранить наблюдаемый результат поиска.
- milestone не переносить.

### 11. Perf и разведка

**Perf обязателен.** Изменяется projection planning, а materialization выполняется per row (`RowMaterializerBuilder.cs:41,72,226`; `InMemoryRowMaterializer.cs:51`). One-time аргумент здесь неприменим.

Baseline: `docs/specs/performance/acceptance-benchmarks.md`.

```bash
run_logged EV190-PERF timeout 240 dotnet run \
  --project benchmarks/nextorm.benchmark -c Release -- \
  --anyCategories=acceptance
```

Требования: ровно 7 cases, 0 failures, elapsed ≤240 s, Mean/Allocated каждого case, cached/prepared ratios и сопоставимость baseline. Рост сопоставимого ratio >20% требует расследования; это не автоматический hard fail. Порог времени и полнота cases — обязательны.

**Отдельный предварительный spike не нужен:** существующие `TryExpandEntityItem:883` и `BuildEntityItem:226-253` дают достаточный путь решения. Неизвестные закрываются regression experiments в D190-01:

- direct entity scenario должен стать содержательным red;
- default-valued present entity отличается от absent item;
- alternating cache shapes различаются там, где различаются SQL/mapper.

Если эти наблюдения опровергнут выбранный механизм, DO сообщает конкретный результат в PLAN; не начинает другой архитектурный подход самостоятельно.

### 12. Риски и mitigations

| Риск | Mitigation |
|---|---|
| Plan-cache alias/slot leak | Same-type self join, alternating direct/nested/scalar shapes, prepared/cached comparison; minimal semantic key/signature correction |
| D173 overlap | Исключительное ownership; фактический base/diff; sequential execution; не импортировать projection folding попутно |
| In-memory `Build` без `isNull` | Absent/present/default regression до фикса; null исходного item, не scalar-default heuristic |
| Слишком широкий entity resolver | Guard на projection origin/slot/type; negative unrelated member/WHERE cases |
| Double construction root entity | Direct typed passthrough; constructor/mapping regressions и allocation inspection |
| Provider tests skipped | Явный DOCKER_HOST, infra recovery, provider/result counts; skipped не PASS |
| Новый selector ничего не выбирает | Discovery и отдельный selected count каждого planned component |
| Coverage/mutation формально «зелёные» вне scope | Actual changed-type branch mapping; scoped config; survivors/no-tool disclosure |
| Docs/register преждевременно закрыты | Author≠certifier и closure только после CHECK |
| Timeout/infra/report loss | Сохранить raw exit/log; различить product defect, infrastructure blocker и missing evidence |

### 13. Версионированный evidence contract — rv1

Все источники и artifact paths ниже **планируемые**, пока DO ledger не предъявит actual evidence. `$E` и `T/run_logged` определены выше; ссылка на вызов `T` является точным invocation с приведённым expansion.

#### EV190-PREFLIGHT

- **Requirement / row:** R190-09 / EV190-PREFLIGHT; **rv:** rv1.
- **Проверка:** база, skill audit, footprint ownership, отсутствие неконтролируемого overlap.
- **Виды/источники:** git output; actual tool invocation results; записи драйвера о reservation; известные status-файлы соседних циклов.
- **Вызовы:** `git rev-parse HEAD`; `git status --short`; `git diff 18659e41 -- src tests`; `skill("pdca-dotnet")`; `skill("nextorm-pdca")`; перед integration `Read(".opencode/skills/running-integration-tests/SKILL.md")`.
- **Результат/логи:** git exit 0; successful skill/read results; reservation подтверждена до правок. Записать actual loaded skill names и relevant constraints.
- **Артефакты:** `$E/preflight/{base.txt,status.txt,base-diff.patch,skills.md,ownership.md}`.
- **Владелец:** orchestrator + coder.
- **Применимость:** всегда.

#### EV190-SCOPE

- **Requirement / row:** R190-09 / EV190-SCOPE; **rv:** rv1.
- **Проверка:** manual inner-loop gate, safety, narrow selections, rebuild provenance, single boundary.
- **Виды/источники:** structured scope JSON; actual argv/log/exit/selection files всех test invocations; actual build ledger.
- **Вызов:** CHECK `ReconcileTestScope(scope="$E/test-scope.json", ledger="status-file DO ledger", raw="$E")`; независимое чтение каждого actual argv/filter/count и boundary entry.
- **Результат/логи:** invocation result `satisfied`; каждый selection ≥1, никаких параллельных projects/broad inner loops/фиктивного rebuild.
- **Артефакты:** `$E/test-scope.json`, `$E/check/test-scope-audit.md`, per-run raw artifacts.
- **Владелец:** coder предоставляет; check certifies.
- **Применимость:** всегда; отсутствие validator не делает строку N/A.

#### EV190-RED

- **Requirement / row:** R190-01, R190-02, R190-04 / EV190-RED; **rv:** rv1.
- **Проверка:** содержательный red новых direct/null/cache regressions до production fix.
- **Виды/источники:** actual test symbols/lines, build output, failing assertions.
- **Вызовы:** успешный `dotnet build nextorm.slnx -c Debug` после test additions; `T EV190-RED-CORE tests/nextorm.core.tests 'FullyQualifiedName~EntityItemProjectionInMemoryTests|FullyQualifiedName~RowMaterializerBuilderTests'`; `T EV190-RED-SQLITE tests/nextorm.sqlite.tests 'FullyQualifiedName~EntityItemProjectionTests'`; cache red — focused selector D190-03.
- **Результат/логи:** build exit 0; test nonzero именно из-за ожидаемого product assertion, не infra/selection/build.
- **Артефакты:** `$E/EV190-RED-*/*`, `$E/red-regressions.md`.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-ROOT

- **Requirement / row:** R190-01 / EV190-ROOT; **rv:** rv1.
- **Проверка:** direct slots/arities, typed root, explicit columns.
- **Виды/источники:** core/sqlite tests, SQL assertions; production diff.
- **Вызовы:** `T EV190-ROOT-CORE tests/nextorm.core.tests 'FullyQualifiedName~EntityItemProjectionInMemoryTests|FullyQualifiedName~RowMaterializerBuilderTests|FullyQualifiedName~JoinReturningIdentityTests'`; `T EV190-ROOT-SQLITE tests/nextorm.sqlite.tests 'FullyQualifiedName~EntityItemProjectionTests'`.
- **Результат/логи:** exit 0; selected counts ≥1; перечисленные variant cases фактически присутствуют и pass.
- **Артефакты:** соответствующие run directories; `$E/variant-ledger.md` с actual symbols/lines.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-NULL

- **Requirement / row:** R190-02 / EV190-NULL; **rv:** rv1.
- **Проверка:** absent reference, all-NULL, present default values, in-memory parity.
- **Виды/источники:** actual cases в тех же focused classes; SQL/in-memory result assertions.
- **Вызов:** оба вызова EV190-ROOT, с cross-reference на actual null/default cases.
- **Результат/логи:** exit 0; каждый null/default variant закрыт actual test либо доказанным baseline guard.
- **Артефакты:** shared run artifacts EV190-ROOT; `$E/null-semantics.md`.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-REGRESSION

- **Requirement / row:** R190-03 / EV190-REGRESSION; **rv:** rv1.
- **Проверка:** nested/scalar/composite/bare, mapping/ctor/value-type guards, запрет scope broadening.
- **Виды/источники:** actual tests, Roslyn references к действующим guards, diff.
- **Вызовы:** focused EV190-ROOT calls; `T EV190-REGRESSION-PG tests/nextorm.postgres.tests 'FullyQualifiedName~TypedCteSqlGenerationTests|FullyQualifiedName~JoinReturningIdentitySqlGenerationTests'`; `roslyn(action="structure", solution="nextorm.slnx")` для actual guard references.
- **Результат/логи:** test exit 0; guard assertions и references подтверждены; каждый matrix variant закрыт.
- **Артефакты:** run directories; `$E/guards.md`; `$E/variant-ledger.md`.
- **Владелец:** coder; scout только для semantic evidence при необходимости.
- **Применимость:** всегда.

#### EV190-CACHE

- **Requirement / row:** R190-04 / EV190-CACHE; **rv:** rv1.
- **Проверка:** shape/source/slot distinctions и equivalent-plan reuse.
- **Виды/источники:** comparer/plan-key/join tests; actual comparer/signature diff.
- **Вызов:** `T EV190-CACHE tests/nextorm.core.tests 'FullyQualifiedName~SelectExpressionPlanEqualityComparerTests|FullyQualifiedName~PlanKeyUniquenessTests|FullyQualifiedName~JoinReturningIdentityTests'`.
- **Результат/логи:** exit 0; actual alternating-context и same-type-slot assertions; отсутствие mapper/alias leak.
- **Артефакты:** `$E/EV190-CACHE/*`, `$E/cache-identity.md`.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-SHARED

- **Requirement / row:** R190-04 / EV190-SHARED; **rv:** rv1.
- **Проверка:** shared `QueryCommand` не мутируется ради отдельного projection call.
- **Виды/источники:** actual diff, Roslyn definitions/references и context-sequence regression.
- **Вызовы:** `git diff -- src/nextorm.core`; Roslyn `structure` → `refs` для actual resolved `QueryCommand.Cache`/связанных symbols; EV190-CACHE tests.
- **Результат/логи:** git exit 0; semantic invocation success; нет новой sticky cache mutation; sequence test pass.
- **Артефакты:** `$E/shared-command-audit.md`, references и shared cache test logs.
- **Владелец:** coder предоставляет, check проверяет.
- **Применимость:** всегда.

#### EV190-TEMP

- **Requirement / row:** R190-04 / EV190-TEMP; **rv:** rv1.
- **Проверка:** применимость temp-table/TVP обязательств overlay.
- **Виды/источники:** diff и Roslyn impact вокруг changed planning branches.
- **Вызовы:** `git diff -- src/nextorm.core`; Roslyn `refs`/`callers` для actual changed symbols, полученных через `structure`.
- **Результат/логи:** successful invocations; N/A только при доказанном отсутствии изменений/семантического влияния на temp/TVP-specific branches.
- **Артефакты:** `$E/temp-tvp-applicability.md`; сами branch tests N/A с указанным доказательством.
- **Владелец:** coder + check.
- **Применимость:** audit всегда; branch tests — если наблюдён temp/TVP impact. Такой новый вариант требует PLAN revision до PASS.

#### EV190-SQL

- **Requirement / row:** R190-01, R190-05 / EV190-SQL; **rv:** rv1.
- **Проверка:** provider SQL shape для шести dialect projects.
- **Виды/источники:** actual SQL assertions в existing/planned focused classes.
- **Вызовы:** последовательно `T EV190-SQL-<provider> tests/nextorm.<provider>.tests 'FullyQualifiedName~JoinWholeEntitySqlGenerationTests'` для `sqlite,postgres,sqlserver,mysql,mariadb,clickhouse`; дополнительные existing selectors — согласно D190-04 JSON.
- **Результат/логи:** каждый вызов exit 0, каждый planned class selected_count ≥1; columns/aliases/slots проверены.
- **Артефакты:** шесть run directories и `$E/provider-sql-matrix.md`.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-INTEGRATION-DIRECT

- **Requirement / row:** R190-02, R190-05 / EV190-INTEGRATION-DIRECT; **rv:** rv1.
- **Проверка:** actual direct projection/outer-null/cache scenarios общей integration suite.
- **Виды/источники:** планируемый `CommonTestSuite.JoinWholeEntity.cs`; actual discovered symbols, provider results.
- **Вызов:** `T EV190-INTEGRATION-DIRECT tests/nextorm.integration.tests 'FullyQualifiedName~JoinWholeEntity_'`.
- **Результат/логи:** exit 0; SQLite/PostgreSQL/SQL Server/MySQL/ClickHouse фактически выполнены, relevant cases не skipped; per-provider counts записаны.
- **Артефакты:** `$E/EV190-INTEGRATION-DIRECT/*`, `$E/integration-providers.md`.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-INTEGRATION-REGRESSION

- **Requirement / row:** R190-03, R190-05 / EV190-INTEGRATION-REGRESSION; **rv:** rv1.
- **Проверка:** существующие typed CTE/self-join/join-arity execution regressions.
- **Виды/источники:** `CommonTestSuite.Cte.cs:136,163`, `PostgresJoinAritiesTests.cs:16`, actual runner discovery.
- **Вызов:** `T EV190-INTEGRATION-REGRESSION tests/nextorm.integration.tests 'FullyQualifiedName~Cte_Typed_HeterogeneousWithDependency_ShouldHoistInOrderAndReturnData|FullyQualifiedName~Cte_Typed_SelfJoin_ShouldReturnBothSides|FullyQualifiedName~JoinArities_ToSqlAndUpdateJoin_ShouldRender'`.
- **Результат/логи:** exit 0, selected_count ≥1; обязательные provider cases выполнены, не skipped.
- **Артефакты:** corresponding run directory; provider ledger.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-INFRA

- **Requirement / row:** R190-05 / EV190-INFRA; **rv:** rv1.
- **Проверка:** integration infrastructure, recovery при отсутствующем socket.
- **Виды/источники:** socket/ping result и actual recovery logs.
- **Вызов:** `curl -s --unix-socket /mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock http://d/v1.40/_ping`; при missing/unreachable socket — `"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`, ожидание и повторный ping перед integration rerun.
- **Результат/логи:** reachable endpoint, successful ping; failed recovery записывается как infrastructure blocker, не green/skipped.
- **Артефакты:** `$E/infra/{ping.log,recovery.log,result.md}`; recovery.log N/A только при успешном первичном ping.
- **Владелец:** coder.
- **Применимость:** initial check всегда; recovery — наблюдённая недоступность socket.

#### EV190-BUILD

- **Requirement / row:** R190-06 / EV190-BUILD; **rv:** rv1.
- **Проверка:** финальная Debug сборка actual edited tree.
- **Виды/источники:** compiler output и actual HEAD/diff fingerprint.
- **Вызов:** `run_logged EV190-BUILD dotnet build nextorm.slnx -c Debug`.
- **Результат/логи:** exit 0, errors 0, warnings 0; после этой сборки relevant sources не менялись перед `--no-build`.
- **Артефакты:** `$E/EV190-BUILD/*`, build provenance в ledger.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-COVERAGE

- **Requirement / row:** R190-06, R190-09 / EV190-COVERAGE; **rv:** rv1.
- **Проверка:** single boundary sweep и coverage lower bounds.
- **Виды/источники:** coverage tool output, Cobertura, Summary, boundary/provider results.
- **Вызовы:** точные `EV190-COVERAGE` и `EV190-COVERAGE-REPORT` команды раздела 8.
- **Результат/логи:** оба exit 0; boundary tests pass; line ≥85%, branch ≥75% по configured coverage scope; no provider-skipped masquerading.
- **Артефакты:** `tests/coverage/coverage.cobertura.xml`, `tests/coverage/report/Summary.txt`, `tests/coverage/report/`, `$E/EV190-COVERAGE*/*`, `$E/coverage-verdict.md`.
- **Владелец:** coder.
- **Применимость:** всегда.

#### EV190-BRANCH

- **Requirement / row:** R190-06 / EV190-BRANCH; **rv:** rv1.
- **Проверка:** baseline→final branch delta, changed-type branch coverage, каждый variant закрыт.
- **Виды/источники:** focused baseline/final coverage, финальный Cobertura, actual changed lines и tests.
- **Вызов:** `dotnet-coverage collect` с теми же settings и safety, но command-under-collection — точные focused red/green selectors из JSON; CHECK `ReconcileBranches(baseline="$E/branches/baseline", final="tests/coverage/coverage.cobertura.xml", variants="$E/variant-ledger.md")`.
- **Результат/логи:** baseline product-red не объявлять tool failure/pass; final exit 0; численные branch totals/delta и uncovered branches перечислены.
- **Артефакты:** `$E/branches/`, `$E/branch-delta.md`, `$E/variant-ledger.md`.
- **Владелец:** coder; check independently reconciles.
- **Применимость:** всегда.

#### EV190-MUTATION

- **Requirement / row:** R190-06 / EV190-MUTATION; **rv:** rv1.
- **Проверка:** scoped mutation outcome либо доказанное отсутствие tooling с branch disclosure.
- **Виды/источники:** actual tool listings, scoped config, Stryker results и mutant analysis.
- **Вызовы:** exact tooling и scoped Stryker invocations раздела 8.
- **Результат/логи:** actual exit codes; survivors убиты/индивидуально обоснованы. При no-tool явно `not-run`, tooling evidence и список mutation-непроверенных branches; не писать «passed».
- **Артефакты:** `$E/mutation/stryker-config.json`, reports, `$E/mutation/outcome.md`; reports N/A только при observed no-tool.
- **Владелец:** coder.
- **Применимость:** disclosure всегда; Stryker run — работоспособное доступное tooling.

#### EV190-PERF

- **Requirement / row:** R190-07 / EV190-PERF; **rv:** rv1.
- **Проверка:** обязательный acceptance и cached/prepared ratio.
- **Виды/источники:** benchmark raw output/artifacts; baseline document; measured wall time.
- **Вызов:** exact `EV190-PERF` command раздела 11.
- **Результат/логи:** exit 0, exactly 7 cases, 0 failures, elapsed ≤240 s; Mean/Allocated/ratios; comparable growth >20% расследован.
- **Артефакты:** `$E/EV190-PERF/*`, actual BenchmarkDotNet artifact paths, `$E/perf-comparison.md`.
- **Владелец:** coder.
- **Применимость:** всегда; query-path priority table запрещает N/A.

#### EV190-AUDIT

- **Requirement / row:** R190-04, R190-09 / EV190-AUDIT; **rv:** rv1.
- **Проверка:** suppression/slop/perf anti-patterns и structural sealedness.
- **Виды/источники:** actual diff; Roslyn structure/types/members; literal/comment/config scan.
- **Вызовы:** `git diff --unified=0 -- src tests Directory.Build.props Directory.Packages.props`; Roslyn `structure` и `types` для actual touched files/types; независимый CHECK review changed execution path.
- **Результат/логи:** successful invocations; counts, denominators, ratios; каждое finding `file:line` и disposition. Нельзя заменять semantic analysis текстовым symbol search.
- **Артефакты:** `$E/audit/{suppressions-slop.md,sealedness.md,design-checklist.md}`.
- **Владелец:** check certifies; coder supplies facts.
- **Применимость:** всегда.

#### EV190-DOCS

- **Requirement / row:** R190-08 / EV190-DOCS; **rv:** rv1.
- **Проверка:** EN/RU, XML-doc decision, публичные ссылки и DocFX.
- **Виды/источники:** actual article diffs и DocFX output.
- **Вызовы:** `git diff -- docs/advanced/limitations.md docs/ru/advanced/limitations.md docs/guide/02-joins.md docs/ru/guide/02-joins.md`; `run_logged EV190-DOCFX dotnet docfx docs/docfx.json`; targeted text review новых links и snippets.
- **Результат/логи:** DocFX exit 0; оба языка согласованы; нет новых публичных links на specs; XML-doc N/A обоснован отсутствием нового API/противоречащих comments.
- **Артефакты:** `$E/EV190-DOCFX/*`, `$E/docs-audit.md`; generated `docs/_site/` не коммитить.
- **Владелец:** coder; check validates.
- **Применимость:** всегда.

#### EV190-REGISTERS

- **Requirement / row:** R190-08 / EV190-REGISTERS; **rv:** rv1.
- **Проверка:** register reconciliation, TODO hygiene, author≠certifier, milestone.
- **Виды/источники:** register diffs, issue metadata, actual reference search.
- **Вызовы:** `git grep -n -E '#190|issues/190|D190' -- docs/specs`; `gh issue view 190 --repo AlexeyShirshov/nextorm --json number,url,milestone,state,title`; independent CHECK чтение closure records.
- **Результат/логи:** actual exit codes; canonical URL совпадает; milestone `1.0.9-rc2`; closure не раньше PASS; полностью выполненный TODO удалён/делинкован либо его отсутствие доказано. Если `gh` недоступен — предоставить actual GitHub invocation-result evidence, не выдумывать metadata.
- **Артефакты:** `$E/registers/{references.log,issue.json,reconciliation.md}`, register diff; author/certifier identities.
- **Владелец:** coder author, check certifier; ACT driver выполняет окончательное закрытие.
- **Применимость:** всегда; TODO deletion только при обнаруженном отдельном полностью реализованном TODO.

### 14. DO ledger и CHECK completeness

DO ledger ведётся в этом status file **по row ID + rv1** и содержит:

- actual test symbols и verified `file:line`;
- executed command/invocation и exact argv;
- exit code/invocation result;
- selected/passed/failed/skipped counts;
- actual logs/artifact paths;
- `satisfied / failed / not-run / missing / blocked / N/A`;
- для N/A — наблюдение pinned applicability predicate.

Shared test run может удовлетворять несколько строк, но каждая строка имеет явную ссылку на нужные assertions/artifacts. Planned paths сами по себе не evidence.

**CHECK re-gather budget:** максимум **2 раунда**, каждый не более **3 адресных запросов** владельцам, всего ≤6 запросов. Владелец бюджета — `check`; orchestrator только маршрутизирует. Запрос перечисляет открытые row IDs и отсутствующие факты.

- Missing log/report сначала собирается внутри CHECK.
- Это не product FAIL, не новая DO iteration и не основание менять rv.
- Verified product defect → FAIL и loop-back.
- Новый обязательный вариант → CHECK → PLAN с конкретным обоснованием; новый контракт явно supersedes rv1, сохраняет старые row IDs/обязательства и добавляет новые IDs.
- Бюджет исчерпан с открытыми applicable rows → CHECK unresolved, список row IDs и blocker/escalation route; PASS запрещён.

### 15. Возврат DO → PLAN и уверенность

На старте **истинный внешний блокер не установлен**.

- File ownership, актуализация базы, восстановление Podman — **предусловия**, выполняемые внутри цикла.
- Provenance #105 и существующая all-NULL неоднозначность — **явные assumptions/risks**, не ослабляющие критерии.
- Неудавшееся восстановление внешней infra/отсутствие полномочий — кандидат внешнего blocker; классификация остаётся за PLAN.
- Недостаточное доказательство механизма → точечный scout; стойкая низкая уверенность → рекомендация orchestrator вызвать `escalate`, trigger 5.

Аддитивное предусловие оставляет исходный D190 активным, blocked на новой зависимости; его критерии и остаток не меняются. Отчёт «implementation complete» сам по себе не завершает единицу.

**Уверенность:** высокая в выбранной минимальной архитектуре и границах; средняя в необходимой cache-signature поправке и конкретном in-memory null predicate до red regressions. Эти неопределённости имеют наблюдаемые проверки, не требуют предварительного внешнего spike.

### 16. Завершение

После satisfied evidence и независимого CHECK PASS:

1. выполнить ACT register closure и TODO hygiene;
2. зафиксировать итог, remaining deferred items и фактические ограничения tooling;
3. не переносить milestone;
4. не commit/push/merge без отдельного разрешения действующего collection режима или пользователя;
5. оставить исходные критерии D190 и полный ledger доступными в этом status file.

## Progress log

2026-10-07T13:44:37Z | PLAN | r1 | n1/3 | PLAN ready — awaiting confirmation | collection-task-plan handoff; no DO started
2026-10-08T13:52:00Z | DO | r1 | n1/3 | D190-01 red regressions added; meaningful red (product, not infra) | core 5 selected/3 failed exit 2 ($E/EV190-RED-CORE); sqlite 7 selected/3 failed exit 2 ($E/EV190-RED-SQLITE)
2026-10-08T13:55:00Z | DO | r1 | n1/3 | D190-02 root projection/materialization implemented; green | QueryCommand.QueryPreparer.cs:484,934; RowMaterializerBuilder.cs:59,77; core EntityItemProjectionInMemoryTests 5/0 exit 0, sqlite EntityItemProjectionTests 7/0 exit 0
2026-10-08T14:00:00Z | DO | r1 | n1/3 | D190-03 cache identity covered; green | SelectExpressionPlanEqualityComparerTests 12/0, JoinReturningIdentityTests 24/0, sqlite PlanKeyUniquenessTests 12/0 (PlanCache_ShouldSeparateDirectEntityItemSlots) all exit 0
2026-10-08T14:01:00Z | DO | r1 | n1/3 | D190-04 provider SQL-shape tests added; green | 6x JoinWholeEntitySqlGenerationTests exit 0 (2 each); postgres TypedCte 7, JoinReturningIdentitySql 29 exit 0
2026-10-08T14:05:00Z | DO | r1 | n1/3 | D190-04 integration direct+regression green, 0 skipped | DOCKER_HOST podman-user.sock; JoinWholeEntity_ 16/0/0 exit 0; CTE/join-arity 9/0/0 exit 0 ($E/EV190-INTEGRATION-*)
2026-10-08T14:10:00Z | DO | r1 | n1/3 | builds Debug+Release 0/0; boundary BOUNDARY-01 coverage | slnx Debug/Release 0W/0E; coverage line 88.1% branch 80.2%; total 9184/0 failed/8986 passed/198 capability skips ($E/EV190-BUILD*, EV190-COVERAGE*)
2026-10-08T14:14:00Z | DO | r1 | n1/3 | perf acceptance 7 cases 0 failures 43.92s; cached/prepared 2.10 vs baseline 1.87 | $E/EV190-PERF (wall 44s)
2026-10-08T14:45:00Z | DO | r1 | n1/3 | D190-06 docs EN+RU + registers; DocFX exit 0 | limitations EN+RU, guide/02-joins EN+RU, code-smells-review.md, API-NAMING-REVIEW.md (#190 audit); DocFX 0 errors, 2 pre-existing warnings ($E/EV190-DOCFX)
2026-10-08T14:45:00Z | DO | r1 | n1/3 | mutation attempted, aborted at timeout (tooling present); disclosed | $E/mutation (1167 combined / 182 RowMaterializerBuilder mutants pending, exit 124); see r1/evidence-brief.md R190-06
2026-10-08T14:50:00Z | DO | r1 | n1/3 | inner-loop validator brief+report rc 0; r1 evidence brief written | $E/test-scope.json, $E/evidence.json, $E/r1/evidence-brief.md, $E/r1/red-regressions.md
2026-10-08T14:55:00Z | DO | r1 | n1/3 | DO-complete-awaiting-CHECK; task files committed | HEAD b447b7be (message starts #190)

## DO state

- **phase:** DO-complete-awaiting-CHECK
- **plan_revision:** r1; **iteration:** n1/3
- **unit states:** D190-01 done; D190-02 done; D190-03 done; D190-04 done; D190-05 done (mutation aborted/disclosed, not "passed"); D190-06 done (docs EN+RU, registers; API closure pending CHECK)
- **base:** plan base `18659e41`; task-start tip `0817ffef`; D173 `29e794fd` already touched the comparer/mapper — built upon, not reverted.
- **evidence:** `artifacts/pdca/rc2-190/r1/evidence-brief.md` (criteria↔evidence + provenance); `artifacts/pdca/rc2-190/test-scope.json` + `evidence.json` (validator rc 0).
- **open at CHECK:** branch-delta baseline not separately captured; direct-projection arities >2 covered indirectly; mutation not completed.
