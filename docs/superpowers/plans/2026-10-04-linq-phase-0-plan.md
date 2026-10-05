# Этап 0 — Inventory: детальный план (LINQ-провайдер)

- **Дата:** 2026-10-04
- **Статус:** Утверждённая архитектура/roadmap — за architect (работа завершена). Этот план этапа 0 — **неутверждённый справочный материал**; его review/утверждение и выбор способа исполнения в текущей сессии не требуются. Этап 0 не выполнен; продуктовая реализация не разрешена.
- **Self-review:** 2026-10-04 — narrow execution-readiness corrections (F1–F9): строгий 4-значный scope-partition, строго последовательный порядок, baseline evidence внутри `00-overloads.json`, нормативный data dictionary, нормализованные ref-домены. Одобренный scope/спека не переоткрывались, семантика не менялась.
- **Тип документа:** детальный **execution-ready** план **только этапа 0** (research/inventory), не план реализации продукта и не фаза 1+.
- **Одобренный roadmap:** `docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md` (утверждён пользователем 2026-10-04; ядро семантики здесь не меняется).
- **Трекинг:** GitHub issue [#20](https://github.com/AlexeyShirshov/nextorm/issues/20) — «TODO: LINQ support», OPEN, milestone **1.2-a.1** (milestone/10), label `enhancement`; статус/чекбоксы #20 не закрываются этим планом.
- **Способ исполнения:** **не выбран; текущий документ справочный, пользователь не обязан выбирать режим для него в этой сессии.** Если отдельный скилл/пользователь решит использовать и утвердить этот draft, режим (обычный интерактивный с per-task checkpoints либо делегированные батчи с checkpoints) выбирается тогда. PDCA/autocommit из этого плана не следуют.
- **Доказательная база:** ссылки ниже — навигационные и sourced-факты, а не воспроизведённые прогоны. В docs-only подготовке плана сборка/тесты/БД не запускались, артефакты этапа 0 не создавались.

> Ключевое: этот документ — **план**. Он не создаёт будущие артефакты этапа 0, не выполняет инвентаризацию и не даёт разрешения на продуктовый код. «Го» пользователя авторизовало подготовку плана, а не исполнение без review.

---

## 1. Намерение и границы

**Намерение.** Подготовить actionable план, который на исполнении даёт **версионированный, воспроизводимый и точный** универсум методов/перегрузок и acceptance-матрицу для relational LINQ + EF в .NET 10, сравнимый с **pinned** linq2db baseline.

**Границы (что этап 0 делает).** Sources + method inventory + acceptance cases + provider expectations + проверяемая human-review матрица. Итог — **frozen scoped required set** с явной **четырёхзначной** классификацией scope: `required` | `baseline-unsupported` | `out-of-scope` | `unresolved` (где `unresolved` допустим **только** в draft и обязан быть снят к финальному freeze — см. §4). Термина «excluded» как значения enum **нет**; «исключённые» в прозе означает объединение `baseline-unsupported ∪ out-of-scope`.

**Что этап 0 НЕ делает:**

- Не реализует продуктовый код, не меняет существующие builders, не вводит новых пакетных зависимостей, не ставит зависимости, не клонирует внешние репозитории.
- Не запускает тесты/БД, не заявляет passed parity. Все runtime-статусы — `not-run`; это честно фиксируется, а не выдаётся за результат.
- Не выполняет фазы 1–9 (компилятор, IR, EF import и т. д.).
- Не пересматривает и не сокращает одобренный scope ради упрощения работы.

**Сохранение scope.** `ToNextOrm` остаётся first-class нативным builder-входом. Новый вход `ToNextOrmQueryable<T>` — неограниченный `T`, общий нативный `IQueryable`; native all-6 SQL. EF-адаптер — только реально поддерживаемые комбинации (см. §4.6). Parity — **read-only query**; без InMemory-паритета, tracking, `SaveChanges`, DML, новых LINQ SQL-special CTE/window/hints.

**Settled scope нельзя урезать автоматически.** Если оператор/перегрузка неудобны, они не удаляются из универсума молча — они классифицируются с явной причиной. Если у метода есть **любой** обязательный контекст, его overload `scope = required`, даже если часть контекстов unsupported-by-baseline; контекст-специфичный unsupported-evidence фиксируется отдельно и **не** превращается в global method exclusion.

**Required negative cases разрешены.** Классификация метода как `out-of-scope`/invalid не запрещает acceptance-case с ожидаемым rejection: `AsTracking`, malformed `ThenBy`, cross-context/connection roots — это валидные required cases об отказе. Baseline с меньшей поддержкой **не** является автоматическим оправданием отбросить согласованное более сильное требование; такие требования фиксируются через `requirement_refs` (§4.4).

---

## 2. Известные sourced-факты и ссылки (вход этапа 0)

- **Одобренная спека:** `docs/superpowers/specs/2026-10-04-linq-provider-roadmap-design.md`.
- **Центральные версии уже в репозитории:** `Directory.Packages.props` — `linq2db` и `linq2db.SQLite` **6.5.0** уже referenced центрально; **не добавлять дубликаты** и не менять версии. Bridge `linq2db.EntityFrameworkCore` **10.6.0** — baseline сравнения, но **не текущая зависимость**.
- **EF snapshot:** EF relational **10.0.12**, Npgsql **10.0.3**, MySql EF **10.0.9** — исходный snapshot версий; финальные идентичности фиксируются в Task 1.
- **SDK:** `global.json` пинует только test runner (`Microsoft.Testing.Platform`), **не SDK**. Точный resolved SDK/reference-pack/assembly identities **записываются фактически**, а не выводятся.
- **Baseline — core:** linq2db **6.5.0**, tag `v6.5.0`, commit `47ed37b1db6c58eaf2cfd8a7fbe6aac294aae305` (verified: `GET https://api.github.com/repos/linq2db/linq2db/git/ref/tags/v6.5.0`).
- **Baseline — EF bridge:** `linq2db.EntityFrameworkCore` **10.6.0**, package metadata repository commit `f74d8d345e20f39b71457fbd81d2feb6836abb5b`, ref `refs/heads/release` (`https://api.nuget.org/v3/catalog0/data/2026.09.11.15.02.34/linq2db.entityframeworkcore.10.6.0.json`).
- **Tag ≠ package commit.** У core tag-commit и у NuGet-пакета могут быть разные commits; **разрешать их независимо** в Task 1 и фиксировать `package` vs `tag` раздельно.
- **Pinned docs:** runtime tag `v10.0.0` — supplementary URL `https://github.com/dotnet/runtime/tree/v10.0.0`; официальные monikers `Queryable`/`Enumerable` — supportive only.
- **Broad package ranges не определяют baseline.** Зависимости пакетов с широкими range не задают фактические версии; сравнение идёт по **явно зафиксированным** `6.5.0` + bridge `10.6.0` + EF `10.0.12`.
- **Upstream-референсы** (для навигации по реализации/тестам) — только по version-pinned commits: `Source/LinqToDB/Internal/Linq/Builder`, `Tests/Linq/Linq`, `Source/LinqToDB.EntityFrameworkCore`, `Tests/EntityFrameworkCore` EF10 projects на корректных separate pins. Предпочитать **commit-permalinks**, не `main`/`raw?ref` (на raw такой ref невалиден).
- **Неизвестное evidence — unresolved, не «отсутствует».** Отсутствие доступа/инструмента фиксируется как blocker, а не как окончательный вывод.

### 2.1 Правило поиска символов

Для C#-символов (определения, перегрузки, члены, реализации) **первым шагом всегда Roslyn**; `rg`/`grep` для символов запрещён, включая имена из metadata. Fallback — только документированный **`System.Reflection.Metadata`**/reference-inspection, когда Roslyn не даёт семантического доступа или требуемых полей; это **не** текстовый поиск символов. Нет инструмента/нет доступа — **blocker/report**, не догадка.

### 2.2 Ловушка .NET 11 / merged docs

Официальные **merged** docs-страницы могут включать `.NET 11` (`net11.0`) в «net10» view (например, `FullJoin`). **Никогда** не использовать список имён с merged-страницы как авторитетный. Точный универсум — из **bound net10 reference assembly**. `Roslyn`-выдача вида `members System.Linq.Queryable` может вернуть framework metadata с `<no location>`; ранее наблюдённые **168 members — это discovery snapshot**, а не захардкоженное ожидаемое число. Произвольный `FullJoin` **не** считается ожидаемым для net10, пока фактически resolved reference pack это не подтвердит; при расхождении — investigate target mismatch, а не переписывать ожидание под найденное.

---

## 3. Выходные артефакты этапа 0 (будущие пути, НЕ создавать сейчас)

Все артефакты создаются **на исполнении** Task 1–9, не при подготовке этого плана. Ровно **семь** артефактов; восьмого нет — baseline evidence живёт **внутри** `00-overloads.json` (Task 4), а не отдельным файлом.

| Артефакт | Назначение |
|---|---|
| `docs/specs/linq/issue-20/00-sources.lock.json` | Environment/toolchain + `sources[]` + `provider_profiles[]`; hashes. Root-тип `SourcesLock` (§4.1). |
| `docs/specs/linq/issue-20/00-overloads.json` | Универсум overloads + **top-level `baseline_evidence[]`**; канонические подписи и scope. Root-тип `OverloadsRoot` (§4.2). |
| `docs/specs/linq/issue-20/00-cases.json` | Acceptance cases: overload refs, evidence refs, семантика, oracle. Root-тип `CasesRoot` (§4.5). |
| `docs/specs/linq/issue-20/00-provider-expectations.json` | Provider cells: `entry=native`/`entry=EF`, 6 провайдеров, ожидания/ограничения, not-run. Root-тип `ProvidersRoot` (§4.5). |
| `docs/specs/linq/issue-20/00-parity-matrix.md` | Human-review rendering **из JSON** (JSON — single source of truth). |
| `docs/specs/linq/issue-20/00-decisions.md` | Scope rationale/blocking unknowns/freeze record. |
| `docs/specs/status/linq-phase-0-20.md` | Фактический прогресс, blockers и verification этапа 0 при исполнении (единственное точное имя status-артефакта; сокращения вроде `00-status` не используются). |

**Формат JSON:** UTF-8, CRLF, deterministic ordering, `schema_version: 1`, корневой объект (не line-based JSON). `entry=native` и `entry=EF` записи **различны**. Сгенерированные docs — internal specs; **не** линкуются из публичных docs (`docs/**`, `docs/ru/**`, readme).

**Source lock / Source IDs:** `id` — стабильный `L20-S-0001` (назначается на первом freeze, **не переиспользуется**). Хранит SHA256 загруженного evidence/reference assemblies либо `null` для чисто web-citation; опциональные retrieval-metadata для web-excerpts; для offline — не копировать огромные upstream-репозитории. `tag` vs `package` commits — раздельно. **Плейсхолдеры** patch-версий .NET/БД **не выдумывать** — фиксировать фактическое на этапе 0, при недоступности — blocked. Server candidate profiles — это **не** observed DB runs и **не** minimum guarantees.

**Baseline evidence storage/freeze:** evidence-записи (`L20-E-0001`, стабильные, без переиспользования) складываются в top-level `baseline_evidence[]` внутри `00-overloads.json`; overload-записи ссылаются на них через `baseline_evidence_refs` (Evidence IDs). Отдельного восьмого файла evidence нет. Evidence-заморозка происходит вместе с freeze `00-overloads.json` в Task 9.

**Никаких будущих файлов при подготовке плана:** раздел описывает только будущие пути; сейчас ни один из них не создаётся.

---

## 4. Модель данных и ID (нормативный data dictionary)

Все JSON-структуры ниже — **нормативны**: валидатор Task 7 проверяет ровно этот input-контракт. Без объявленных здесь полей/ссылок validator не вправе требовать ничего. Поля, не перечисленные как required, nullable/optional. Любые примеры ниже — schema-example, не наблюдаемые source-версии.

### 4.0 ID-домены

- `SourceID` = `L20-S-####` — SourcesLock, стабильный, no recycling.
- `EvidenceID` = `L20-E-####` — OverloadsRoot.baseline_evidence, стабильный, no recycling.
- `OverloadID` = `L20-O-####` — OverloadsRoot.overloads, стабильный, no recycling.
- `CaseID` = `L20-C-####` — CasesRoot.cases, стабильный, no recycling.

### 4.1 `SourcesLock` — `00-sources.lock.json`

Root REQUIRED keys: `schema_version`, `environment`, `sources`, `provider_profiles`.

- `schema_version`: integer const `1`.
- `environment`: object `Environment` (required).
- `sources`: `Source[]` (required).
- `provider_profiles`: `Profile[]` (required; может быть пустым только до Task 6).

`Environment` REQUIRED:

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `sdk_version` | string nonempty | yes | фактический resolved SDK (Task 1), не выдуманный |
| `ref_pack_version` | string nonempty | yes | фактический reference pack |
| `tfm` | string const `net10.0` | yes | |
| `resolved_assemblies` | `Assembly[]` nonempty | yes | |
| `captured_utc` | string ISO-8601 | yes | |

`Assembly` REQUIRED: `assembly_identity`:string nonempty; `path`:string nonempty (записывается; отсутствие пути не является причиной хэшировать ref pack); `sha256`:string nonempty lowercase 64-hex.

`Source` REQUIRED keys: `id`, `kind`, `url`, `version`, `commit`, `retrieved_utc`, `proof_origin`, `sha256`.

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `id` | SourceID | yes | уникален |
| `kind` | enum | yes | `official-api` \| `reference-assembly` \| `runtime-source` \| `package-metadata` \| `upstream-source` \| `upstream-test` \| `repository-config` \| `provider-doc` |
| `url` | string nonempty | yes | URL или repo-relative path |
| `version` | string \| null | yes (nullable) | точная версия для packages |
| `commit` | string \| null | yes (nullable) | точный commit обязателен для `upstream-source`/`upstream-test` |
| `retrieved_utc` | string ISO-8601 | yes | |
| `proof_origin` | string nonempty | yes | откуда доказательство |
| `sha256` | string \| null | yes (nullable) | при наличии — lowercase 64-hex |

Правила источника: SHA **опционален** для простой web-citation; точный commit обязателен для upstream-source/test; точная version — для package-metadata; `official-api` moniker **не** является authority над method-universe.

`Profile` REQUIRED keys: `id`, `provider`, `entry`, `description`, `server_version_constraint`, `capability_doc_refs`, `observation`.

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `id` | string unique | yes | |
| `provider` | enum | yes | `sqlite` \| `postgres` \| `sqlserver` \| `mysql` \| `mariadb` \| `clickhouse` |
| `entry` | enum | yes | `native` \| `EF` |
| `description` | string nonempty | yes | |
| `server_version_constraint` | string \| null | yes (nullable) | |
| `capability_doc_refs` | SourceID[] | yes | |
| `observation` | string const `not-run` | yes | actual DB version/digest **не** утверждается |

Профили нужны для native-6 + EF-4 (см. §4.6), плюс отдельный conditional MariaDB profile при его записи. Unsupported EF ClickHouse может быть отражён как не-applicability, но это **не** подразумевает существующий EF-провайдер.

### 4.2 Root: `OverloadsRoot` — `00-overloads.json`

Root REQUIRED keys: `schema_version` (integer const `1`), `overloads` (`Overload[]`), `baseline_evidence` (`Evidence[]`, изначально пустой до Task 4).

### 4.3 `Overload`

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `id` | OverloadID | yes | уникален; сортировка канонических подписей на первом freeze |
| `surface` | enum | yes | `Queryable` \| `Enumerable` \| `EFCore` \| `ProviderEF` |
| `canonical_signature` | string nonempty | yes | полный declaring type + name + generic arity + parameter types/ref/index/default/comparer + return type + constraints; включает все доступные return/constraints; при недоступности — blocker, **не** fake |
| `family` | enum | yes | `projection` \| `filter` \| `order` \| `paging` \| `join` \| `group` \| `set` \| `terminal` \| `loading` \| `rawsql` \| `metadata` \| `other` |
| `contextual_role` | enum | yes | `server-operator` \| `server-expression` \| `terminal-materializer` \| `annotation` \| `client-boundary` \| `out-of-scope` |
| `scope` | enum | yes | `required` \| `baseline-unsupported` \| `out-of-scope` \| `unresolved` |
| `source_refs` | SourceID[] | yes | ≥1 |
| `baseline_evidence_refs` | EvidenceID[] | yes | может быть пустым до Task 4; ссылается только на Evidence ID |
| `exclusions` | string \| null | yes (nullable) | **nonempty обязателен**, когда `scope ∈ {baseline-unsupported, out-of-scope}` |
| `note` | string \| null | no | |

**Четырёхзначный partition + freeze rule.** `scope` — ровно 4 значения. Домен всех обнаруженных overloads разбивается на `required ∪ baseline-unsupported ∪ out-of-scope ∪ unresolved`; множества **попарно непересекающиеся**, объединение равно полному универсуму (валидатор сверяет counts). **На финальном freeze `Overload.scope='unresolved'` запрещён где-либо** (не только «для required»): каждый overload обязан быть `required`/`baseline-unsupported`/`out-of-scope`. Не-required baseline-unknown допустим только при **explicit `out-of-scope` rationale** и **не** считается supported. На draft `unresolved` — нормальное состояние. `scope = required`, если **любой** контекст метода обязателен. `excluded` — не enum-значение.

### 4.4 `Evidence`

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `id` | EvidenceID | yes | уникален, no recycling |
| `entry` | enum | yes | `native` \| `EF` |
| `overload_ids` | OverloadID[] nonempty | yes | |
| `context` | string nonempty | yes | |
| `provider_scope` | provider enum[] nonempty | yes | |
| `result` | enum | yes | `supported` \| `unsupported` \| `conditional` \| `unresolved` |
| `source_refs` | SourceID[] | yes | может быть пустым |
| `test_refs` | SourceID[] | yes | Source IDs version-pinned test-record'ов; может быть пустым |
| `runtime_verification` | string const `not-run` | yes | |
| `note` | string nonempty | yes | |

Правила: `source_refs ∪ test_refs` **непусто** для `supported`/`unsupported`/`conditional`; `unresolved` может иметь оба пустыми, но `note` обязан назвать concrete blocker. Распознавание source само по себе **не** семантическое доказательство; для `supported` `note` описывает semantic path coverage. EF bridge evidence использует **свой** source pin (другой commit, не core tag). Артефакт хранит наблюдения фактического baseline, а не universal support.

### 4.5 `CasesRoot` — `00-cases.json` и `ProvidersRoot` — `00-provider-expectations.json`

**`CasesRoot`** root REQUIRED keys: `schema_version` (integer const `1`), `cases` (`Case[]`).

`Case`:

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `id` | CaseID | yes | уникален |
| `overload_ids` | OverloadID[] nonempty | yes | |
| `description` | string nonempty | yes | |
| `expression_example` | string nonempty | yes | |
| `input_root_provenance` | enum | yes | `native` \| `EF` \| `combined`; контексты не смешивать молча |
| `entries` | enum[] nonempty unique | yes | `native` \| `EF` (одно или оба); applicability из concrete input/query-контракта (Task 5), review Task 8; `input_root_provenance='EF'` **не** требует native entry неявно |
| `context` | string nonempty | yes | filter/projection/group/pagination и т. д. |
| `result_shape` | enum | yes | `scalar` \| `entity` \| `constructed` \| `group` \| `collection` (nullable-детали — в expectation) |
| `semantic_expectation` | string nonempty | yes | null/empty/duplicates/order/cardinality |
| `oracle` | oracle enum[] nonempty | yes | `linq-to-objects` \| `relational-contract` \| `EF-differential` \| `linq2db-differential` |
| `oracle_preconditions` | string[] | yes | nonempty где применимо; relational-contract expectation определён всегда |
| `baseline_mode` | enum | yes | `baseline-comparison` \| `target-only-contract` |
| `baseline_core` | `BaselineResult` \| null | yes | null-правила ниже; для `baseline-comparison` обычно nonnull |
| `baseline_ef` | `BaselineResult` \| null | yes | null-правила ниже; `null` для не-EF case |
| `requirement_refs` | string[] | yes | approved-spec anchors / repo-relative `path#anchor` для stronger-than-baseline требований; допускает required negative cases, не считающиеся baseline-excluded; **обязателен nonempty** для `target-only-contract` |
| `scope` | enum | yes | `required` \| `baseline-unsupported` \| `out-of-scope` \| `unresolved` |
| `related_provider_cells` | string[] | yes | Cell IDs |
| `target_stage` | integer 1–9 | yes | |
| `source_refs` | SourceID[] | yes | может быть пустым |

`BaselineResult`: `result` enum `supported` \| `unsupported` \| `conditional` \| `unresolved` (значения `not-applicable` **нет**); `baseline_evidence_refs` EvidenceID[]; `runtime_verification` const `not-run`.

Правила `baseline_mode`/non-null (validator проверяет точную null-rule):
- `baseline-comparison`: **хотя бы один** из `baseline_core`/`baseline_ef` **NONnull**; `baseline_ef: null` для не-EF case, `baseline_core: null` для EF-only negative bridge case.
- `target-only-contract`: **оба** `baseline_core` и `baseline_ef` могут быть `null` **ТОЛЬКО** при nonempty `Case.requirement_refs` **И** `Case.source_refs`, доказывающих согласованное target-требование (например AsTracking-reject или strategy-honor). «Нет evidence» **не** даёт target-only по умолчанию: escape требует валидных refs из approved-спеки. Required target-only contract **не** блокируется отсутствием baseline (target независим от baseline).
- Поля реального test-execution (`passed` и т. п.) в схеме **нет**.

**`ProvidersRoot`** root REQUIRED keys: `schema_version` (integer const `1`), `cells` (`Cell[]`).

`Cell`:

| Поле | Тип | Required | Правило |
|---|---|---|---|
| `id` | string | yes | детерминированный `<case_id>:<entry>:<provider>:<profile_id>` |
| `case_id` | CaseID | yes | |
| `entry` | enum | yes | `native` \| `EF` |
| `provider` | enum | yes | `sqlite` \| `postgres` \| `sqlserver` \| `mysql` \| `mariadb` \| `clickhouse` |
| `profile_id` | string | yes | ссылается на Profile.id |
| `expectation` | enum | yes | `translate` \| `semantics-preserving-emulation` \| `provider-limited` \| `not-applicable` \| `unresolved` |
| `capability_constraints` | string[] | yes | version/extensions/collation/null behavior |
| `evidence_refs` | EvidenceID[] | yes | только Evidence ID |
| `nextorm_translation` | enum | yes | `not-implemented` \| `existing-native-backend` \| `unknown` |
| `runtime_verification` | string const `not-run` | yes | |
| `reason` | string nonempty | yes | обязателен для `provider-limited`/`not-applicable`/`unresolved`; для `semantics-preserving-emulation` описывает proof |

**Правила Cell (validator):** для каждой cell `entry` обязан равняться `entry` referenced Profile, `provider` — `provider` referenced Profile, `Cell.entry ∈ Case.entries`, а `Cell.id` — точной формуле `<case_id>:<entry>:<provider>:<profile_id>`; связанный case и `Case.related_provider_cells` проверяются **bidirectional**. `existing-native-backend` **никогда** не означает, что новый `IQueryable` уже реализован. Старые `partial`/`by design` из прежних docs **не** могут считаться success. Baseline-supported per provider **отличим** от EF availability; никаких произвольных унаследованных exclusions. Если согласованная explicit split strategy сильнее baseline — фиксировать через `requirement_refs`, **даже если** linq2db метод отбрасывает.

### 4.6 EF coverage: четыре поддерживаемые комбинации + пятая conditional

- **Поддерживаемые EF-комбинации (ровно четыре):** PostgreSQL, SQL Server, SQLite, MySQL.
- **MariaDB EF** — отдельный **пятый conditional** profile; записывается отдельно и **не** включается молча в четвёрку. Нативная MariaDB требует независимой EF-сертификации и **не** заменяется MySQL.
- **EF ClickHouse** — `not-applicable`, если адаптер отсутствует; это **не** native provider fail; опционально и **не** нужно для заполнения native-6.
- **Покрытие по `Case.entries` (qualified coverage):** required case с `native ∈ entries` получает **6 native cells** (исключения — explicit context reason); required case с `EF ∈ entries` получает **4 поддержанные EF-комбинации** + отдельная conditional MariaDB запись, если применимо. Case с `entries=['EF']` **не** требует native-6 filler cells.
- Unrelated native-метод **не** форсируется на EF (иначе — неверное покрытие). Task 8 cross-check: native core LINQ coverage **не** может быть обойдён произвольной установкой `entries=['EF']` — required family/overload обязаны иметь native query suite.

### 4.7 Правила подсчёта и валидации

- Никаких invented record counts; counts отчитываются фактически.
- Checks считают **фактические уникальные canonical_signature** против точного универсума.
- Много cases на overload разрешено.
- Полный Cartesian combinatorics невозможен: risk-based patterns с **явным rationale**.
- **Freeze rule (точно):** на финальном freeze **нигде** нет `Overload.scope='unresolved'` и **нигде** нет `Case.scope='unresolved'`. Для `Case.scope='required'` дополнительно **блокируют** freeze: `baseline_core`/`baseline_ef` с `result='unresolved'` (кроме `baseline_mode='target-only-contract'` по §4.5) и любая applicable provider `expectation='unresolved'`. Не-required baseline-unknown допустим только при explicit `out-of-scope` rationale и **не** считается supported. На draft unknown — нормально.
- Валидатор различает **source-домен** (`source_refs`, `test_refs`, `capability_doc_refs`) и **evidence-домен** (`baseline_evidence_refs`, `evidence_refs`); смешивать нельзя; nested baseline-evidence проверяются точно как `Case.baseline_core.baseline_evidence_refs`/`Case.baseline_ef.baseline_evidence_refs`, а не как несуществующие case-level refs. Термин `refsource` не используется.

---

## 5. Детальные задачи (строго последовательно)

Порядок исполнения — **единственный**: Task 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9. Никакого параллельного claim для Task 4/5 нет; read-only preliminary work не является completion/gate shortcut. Любая задача начинается только после gate предыдущей. После каждой задачи — обновление `docs/specs/status/linq-phase-0-20.md`.

### Task 1 — Зафиксировать источники и toolchain до инвентаризации

**Вход:** одобренная спека, тело #20, текущие configs (`Directory.Packages.props`, `global.json`), workspace snapshot.

**Шаги:**
1. Проверить статус одобренной спеки и metadata issue #20 (OPEN, milestone 1.2-a.1, label); зафиксировать snapshot изменений workspace.
2. Только **чтение** существующих configs — версии не менять.
3. Для framework-символов — сначала Roslyn solution `structure`/`members`.
4. Захватить identity net10 reference assemblies (`environment`); verify upstream pins по catalog (фактическое).
5. Разрешить **независимо** core NuGet 6.5.0 repository commit и tag commit `47ed37b…`; записать package vs tag.
6. Проверить EF bridge 10.6.0 package commit `f74d8d34…` (refs/heads/release) и зафиксировать.
7. Собрать `00-sources.lock.json` (`SourcesLock`) с `sources[]`, `provider_profiles[]`, hashes/retrieval metadata.

**Доступ к чтению (не prerequisite `gh`):** read-only scout может использовать официальный GitHub REST/HTML по verified URL; `gh` **не** требуется для inventory-read. Мутации issue — только через authorized `gh` (coder). Если issue-write недоступен — сохранить pending tracking blocker и **не** заявлять «posted».
**STOP-семантика:** **блокирующий** mismatch source/toolchain/TFM/pin в Task 1 **останавливает Task 2–9**. Blocker записывается в `docs/specs/status/linq-phase-0-20.md`; наблюдения сохраняются как drafts, без ложного gate-completion. Недоступные обязательный официальный доступ/known pins **нельзя** заменить «самым новым» или выводом.

**Команды (планируемые, on execution):** `dotnet --info`; чтение configs; `gh issue view 20` (если доступен; иначе REST/HTML); `roslyn structure`/`members`.

**Выход:** воспроизводимый `SourcesLock` manifest + запись в status (включая blocker, если он есть).

**Exit gate:** воспроизводимый manifest; при mismatch — STOP Task 2–9 и зафиксированный blocker.

### Task 2 — Извлечь точный универсум перегрузок

**Вход:** `SourcesLock` из Task 1, если Task 1 не остановлен.

**Шаги:**
1. Roslyn external `members`: .NET 10 `System.Linq.Queryable`, `System.Linq.Enumerable` — только методы, канонические подписи, constraints, overloads.
2. Релевантные EF Core `IQueryable` extensions/roots/properties/functions, provider family symbols.
3. Задокументировать tool limitations. Roslyn-first; если семантический доступ/поля недоступны — документированный `System.Reflection.Metadata`/reference fallback; **без** symbol-grep.
4. Любой временный metadata formatter — изолированно в `/tmp`, **не** в репозитории; **без** новых deps/config.
5. Verification: каждый public method учтён уникальной канонической подписью, включая **все** overloads типа.
6. Anti-contamination: не тащить .NET 11; сверять assembly identities.

**Выход:** черновой `00-overloads.json` (`overloads[]`, `baseline_evidence: []`) + запись в status.

**Exit gate:** полный универсум по подписям; расхождение с resolved ref pack — investigate, не подгонять.

### Task 3 — Классифицировать контексты/scope

**Вход:** `00-overloads.json` (черновик), одобренные границы.

**Шаги:**
1. Присвоить `family`, `contextual_role`, `scope` (4-значный enum).
2. Различать materializers и настоящую client boundary от server-операторов.
3. Не удалять произвольно hard variants/comparer/indexed; `scope=required`, если любой контекст обязателен.
4. Записать `exclusions` rationale для `baseline-unsupported`/`out-of-scope` (nonempty).

**Выход:** `00-overloads.json` + rationale в `00-decisions.md`.

**Exit gate:** `required ∪ baseline-unsupported ∪ out-of-scope ∪ unresolved` — **disjoint partition**, равный универсуму; отсутствует undeclared `excluded`. `unresolved` допустим на draft; на финальном freeze `Overload.scope='unresolved'` должен быть 0 (см. §4.3/§4.7).

### Task 4 — Закартировать version-pinned baseline evidence (core + EF)

**Вход:** `00-overloads.json` + pinned commits.

**Шаги:**
1. Для каждого оператора/метода — handling/rewrites/test cases/provider restrictions из version-pinned core.
2. Pattern equivalents: **нет имени ≠ unsupported**; запрещено выводить case-unsupported из отсутствия method name.
3. Старые docs — только navigation starting points, **reverify**.
4. `split`/`single` — распознаются и **срезаются**, не honors; tracking baseline — **не** в target.
5. Bridge EF metadata capabilities — **отдельно** по source commit (другой pin), не по core tag assumption.
6. Все `baseline-unsupported` требуют overload+context-specific proof; provider нельзя forced classify global unsupported; контекст-специфичный unsupported **не** переводит overload в global excluded.
7. Наличие tests в source — **не** «test passed».

**Выход:** Task 4 пишет **только** evidence — расширяет top-level `baseline_evidence[]` в `00-overloads.json` и заполняет overload `baseline_evidence_refs`; **cases не создаются**. Evidence IDs `L20-E-####` стабильны.

**Exit gate:** каждое exclusion подкреплено evidence; upstream-unknown — `unresolved` **draft**. Unresolved baseline-result для case с `scope='required'` при `baseline_mode='baseline-comparison'` допустим как draft, но **блокирует** freeze Task 8/9; для `target-only-contract` отсутствие baseline не блокирует (§4.5). Финальный parity claim не делается.

### Task 5 — Построить acceptance cases

**Вход:** `00-overloads.json` (overloads + baseline_evidence).

**Обязательные семейства (входной список):**

- Проекции: scalar / entity / DTO / nullable / nested.
- `Select → Where → Order → paging`; paging → Where new derived; повторный primary `Order` включая boundary; повторные `Skip`/`Take`/`Distinct`; malformed `ThenBy`.
- Multi-root: join выбранных shapes после projection of key, `SelectMany` correlated/uncorrelated, left patterns `DefaultIfEmpty` vs grouped `GroupJoin` + empty.
- Set-операции: compatible shapes, null/duplicates, order.
- `GroupBy`: все overloads key/element/result, aggregate, HAVING vs `IGrouping` return.
- Correlated `Exists`/`In`/`Scalar`/`Aggregate` после group/paging.
- Terminals: empties/default/cardinality/count overflow/cancellation lifecycle sync+async (`not-run`).
- Local `Contains` — collections/parameters.
- Indexed и `*By`/`Range`/`Index` types точно 10.
- EF: filters ignore named current `DbContext` value; shadow/converters/schema/owned/complex/inheritance/composite relation/skip-nav — только если фактический baseline; `Include`/`ThenInclude`, filtered include, loading strategy, raw SQL parameters.
- Strict method expansion, fail-closed/client final opt-in/subsequent server reject.
- Cache: dynamic closure contexts/model/dialect/load-strategy differences.
- Graph: nested results, identity-not-tracking, N+1 counts, streaming/buffering.
- Required negative: `AsTracking`, malformed `ThenBy`, cross-context — могут ссылаться на `out-of-scope`/invalid методы и `requirement_refs`.

**Выход:** Task 5 **единолично** создаёт `00-cases.json` (`CasesRoot`); задаёт для каждого case required `entries` (applicability из concrete input/query-контракта), required `baseline_mode`; `baseline_core`/`baseline_ef` ссылаются на stable Evidence IDs через **вложенный** `baseline_evidence_refs` + запись в status.

**Exit gate:** каждый case ссылается на ≥1 overload; `entries` nonempty/unique; `baseline_mode` задан; для `baseline-comparison` ≥1 baseline-объект nonnull; для `target-only-contract` оба baseline null только при nonempty `requirement_refs`+`source_refs`; ни один basic case не заявляет «весь метод реализован». Overload-specific parity — по классификации, а не auto-all.

### Task 6 — Развернуть provider expectations на all-6 SQL

**Вход:** `00-cases.json`.

**Шаги:**
1. Для каждого cell поле `entry` = `native` или `EF` (никаких schema-имён `nativeEntry`/`EFEntry`).
2. Matrix profiles (docs/tests текущие теги): PG 17-alpine, SQL Server 2025-latest, MySQL 8.4, MariaDB 11.4, CH 25.8-alpine, SQLite (deployed native version — захватить позже). Теги — **candidate references**, не runs и не minimum; `observation: not-run`.
3. Capabilities — версионно-описанные, не угаданный broad minimum.
4. Ровно **4 поддержанные EF-комбинации** (PostgreSQL, SQL Server, SQLite, MySQL); MariaDB EF — **пятый conditional**, отдельно, не в четвёрке; MariaDB **не** заменяет MySQL; EF ClickHouse — `not-applicable` (не native unsupported).
5. Source-level expected emulation — только semantics proof + future verification requirement; **без** invented fallback.
6. Все `runtime_verification` — `not-run`.

**Выход:** `00-provider-expectations.json` (`ProvidersRoot`) + запись в status.

**Exit gate (qualified coverage):** required case с `native ∈ entries` → 6 native cells (`not-applicable` с explicit reason); required case с `EF ∈ entries` → 4 поддержанные EF-комбинации + отдельная conditional MariaDB policy; `entries=['EF']` не требует native-6 filler; unrelated native-метод не форсируется на EF.

### Task 7 — Структурная валидация и review rendering

**Вход:** полный draft всех JSON.

**Шаги:**
1. Планируемые CLI (реально запускаются на исполнении, только **после** записи файлов, не сейчас): `dotnet --info`; `git diff --check`; `python3 -m json.tool <each file>`.
2. Temp Python stdlib validator (pseudocode/executable, **не retained**; proposed future tool, **не** executed сейчас) проверяет ровно input-контракт §4: root-тип dict + `schema_version`; array-типы; allowed enums; ID-домены и уникальность; `canonical_signature` uniqueness; **referential integrity**: `Overload.source_refs`, `Evidence.overload_ids`+`source_refs`/`test_refs`, `Overload.baseline_evidence_refs`, `Case.overload_ids`, `Case.baseline_core.baseline_evidence_refs` и `Case.baseline_ef.baseline_evidence_refs` (**точные вложенные пути, только когда объект nonnull; case-level `baseline_evidence_refs` не существует**), `Cell.evidence_refs`, `Case.related_provider_cells`; **равенства** `Cell.entry == Profile.entry`, `Cell.provider == Profile.provider`, `Cell.entry ∈ Case.entries`, `Cell.id == "<case_id>:<entry>:<provider>:<profile_id>"`; **bidirectional** linkage (overload ↔ evidence ↔ case ↔ cell, включая `related_provider_cells`); 4-значный partition counts; overload → ≥1 case **или** explicit `baseline-unsupported`/`out-of-scope`; **coverage по `entries`** (native∈entries → 6 native cells; EF∈entries → 4 EF + conditional MariaDB policy; `['EF']` без native-6 filler); `baseline_mode` null-rule (`baseline-comparison` ≥1 nonnull; `target-only-contract` оба null только при nonempty `requirement_refs`+`source_refs`); **freeze rule** (нигде нет `Overload.scope='unresolved'` и нигде нет `Case.scope='unresolved'`; для required — baseline `result='unresolved'` кроме target-only и applicable provider `expectation='unresolved'` блокируют); absence of undeclared required fields; источники locked (нет branch-only URL у upstream refs); deterministic render (два прогона без diff, кроме frozen `retrieved` timestamps).
3. Raw loop-программы используют **repository paths**, без filesystem `find`/`grep` scan; CRLF normalize.
4. Использовать установленный tooling, иначе blocker; `pip` deps не ставить.
5. Dotnet build/test для этого docs-only этапа **не требуется**.
6. `00-parity-matrix.md` генерируется из JSON — без hand-vary verdicts.

**Выход:** `00-parity-matrix.md` + validation evidence + запись в status.

**Exit gate:** validator deterministic и соответствует словарю §4; матрица воспроизводима; оба прогона совпадают.

### Task 8 — Независимый review

**Вход:** draft matrix/cases/expectations.

**Шаги:**
1. Scouts собирают факты (low tier); architect/decision принимает решения по exclusions/семантическому oracle/provider capability claims.
2. Mandatory unknown резолвится **до** freeze; факты — low tier, решение — primary.
3. Никакой subagent **не** урезает scope самостоятельно.
4. Если upstream требует динамического теста для решения — **не** делать вид, что он выполнен; оставить specific blocker и запросить separately approved probe/execution.
5. Phase0 **без** real DB runs; если runtime probe позже авторизован — загрузить integration guide и container recovery; никаких «green» без `DOCKER_HOST`.
6. Cross-check `entries`: required family/overload обязаны иметь native query suite; `entries=['EF']` не может произвольно скрывать native core LINQ coverage.

**Выход:** review-запись в `00-decisions.md`.

**Exit gate:** все mandatory unknowns разрешены или явно блокируют freeze. Freeze **невозможен**, пока где-либо есть `Overload.scope='unresolved'` или `Case.scope='unresolved'`, либо у required-`baseline-comparison` case есть baseline `result='unresolved'`/applicable provider `expectation='unresolved'`; матрица не называется complete.

### Task 9 — Freeze snapshot и handoff (только после user review)

**Шаги:**
1. Freeze — **только** после того, как пользователь отревьюил матрицу: concrete required set/exclusions.
2. Source references и validation evidence review записаны; IDs (Source/Evidence/Overload/Case) immutable/stable.
3. Разделять три гейта: **phase0 plan approval** (разрешает execution) ≠ **phase0 matrix approval** (freeze) ≠ **next stage1 spec/plan** (требуется до product code). Freeze — отдельный user gate, не следствие plan approval.
4. Stage0 status `complete` — только при concrete deliverables + checks + matrix review; обновить **только** phase0-чекбокс issue #20, **никогда** не закрывать общий #20.
5. Handoff в phase1: spec/compiler contracts input counts/digest, unimplemented coverage; next-stage approval **не** выдаётся неявно.

**Выход:** frozen artifacts + `00-decisions.md` freeze record + status update.

**Exit gate:** все 7 артефактов фактически существуют; freeze review зафиксирован; нигде нет `Overload.scope='unresolved'`/`Case.scope='unresolved'`; у required `baseline-comparison` нет unresolved baseline/applicable provider; `target-only-contract` подтверждён nonempty `requirement_refs`+`source_refs`.

---

## 6. Gates и checks (сводно)

- Порядок строго последовательный: **Task 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8 → 9**. Параллельного claim для 4/5 нет.
- Task 1 blocking mismatch → STOP Task 2–9; blocker в status; drafts сохраняются; gate не считается пройденным.
- JSON `schema_version: 1`; добавления — только контролируемые в пределах словаря §4.
- Полный parity **required denominator заморожен**; дополнения/baseline upgrades — только reviewed version bump при stable existing IDs. Любой later missing required **не** waive'ится.
- **Не** хардкодить `count ≥ 100` или `168 methods`. Counts отчитываются фактически.
- Partition проверяется как disjoint-4, равный универсуму; на freeze `Overload.scope='unresolved'` и `Case.scope='unresolved'` = 0 **везде**; у required `baseline-comparison` нет unresolved baseline/applicable provider (target-only — по §4.5).
- Referential integrity + bidirectional linkage source/evidence/case/cell; равенства `Cell.entry==Profile.entry`, `Cell.provider==Profile.provider`, `Cell.entry∈Case.entries`; точные вложенные baseline-evidence пути; coverage по `entries` (§4.6).
- Stage0 никогда не запускает real DB/parity; все runtime — `not-run`.

---

## 7. Pitfalls / Stop-условия

- Task 1 blocking source/toolchain/TFM/pin mismatch → **STOP Task 2–9**; blocker в status; draft observations, не false gate.
- Отсутствует инструмент / заблокирован официальный доступ / source-package mismatch → stop/report blocker, не выводить версию/полноту догадкой.
- Недоступные обязательный официальный доступ/known pins → нельзя «самое новое»/infer.
- `gh` read не prerequisite; mutation issue — только coder через authorized `gh`; issue-write unavailable → pending tracking blocker, не «posted».
- Неясная baseline-семантика → **не** инферить; `unresolved` с concrete blocker.
- Unresolved evidence для required case — draft, но блокирует Task 8/9 freeze; без final parity claim.
- Недоступный core provider minimum → не давать false guarantees.
- Caller `DbContext` **не** удерживается как global key value.
- linq2db — без runtime deps в продукт.
- Никакого invasive refactor/renames; Roslyn-first, symbol-grep запрещён.
- Никаких unrelated public-doc rewrites; старое marketing wording флагируется в sources, но **не** правится.
- Никаких product code, tests, fixtures, package refs, installs, inventory JSON, source clones, commits/push/merges, новых тикетов или foreign-repo writes в рамках **подготовки плана**.
- Артефакты этапа 0 (раздел 3) **не создаются** до approved execution.

---

## 8. Критерии завершения этапа 0

1. Все **семь** артефактов фактически созданы.
2. Точный универсум стабилен; Overload/Evidence/Source/Case IDs уникальны и не переиспользованы; sources locked.
3. Concrete required set; partition = disjoint-4 = универсум; **нигде** нет `Overload.scope='unresolved'`/`Case.scope='unresolved'`; у required `baseline-comparison` нет unresolved baseline/applicable provider (target-only — §4.5); coverage соответствует `entries` (§4.6).
4. Каждый required: `entries` + `baseline_mode` + semantic expectation + evidence + valid oracle + provider expectation + future stage owner.
5. Исключённые (baseline-unsupported ∪ out-of-scope) имеют одобренную nonempty причину; undeclared `excluded` отсутствует.
6. Валидации проходят; referential integrity/bidirectional linkage подтверждены; deterministic CRLF.
7. Явно зафиксировано, что **все** runtime — `not-run`.
8. Независимый review + user matrix freeze.
9. Issue phase0-чекбокс обновляется **только тогда**; остальные чекбоксы #20 остаются unchecked.

`00-parity-matrix.md` / baseline evidence — **не** provider certification.

---

## 9. Применимость review/исполнения

- **Варианты ниже применимы только если отдельный скилл/пользователь решит использовать и утвердить этот draft; сейчас это не запрос review/выбора.**
- План подготовлен **вручную** (skill `writing-plans` в этой сессии недоступен и **не** вызывался; это не заявляется как вызов).
- Если draft решено использовать: после **письменного review** пользователь выбирает способ исполнения — обычный интерактивный с per-task checkpoints **или** делегированные агентские батчи с checkpoints.
- **Autocommit/PDCA из этого плана не подразумеваются**; исполнение начинается только после отдельного одобрения.
- «Го» на подготовку плана (2026-10-04) — описательное разрешение preparation, **не** приглашение к исполнению и не одобрение плана.

## 10. Handoff

- Следующий шаг: передать отдельному скиллу утверждённую спецификацию roadmap, issue #20 и этот справочный draft этапа 0; скилл готовит собственный детальный план в рамках утверждённого дизайна и применяет необходимые review/approval gates.
- Этот draft этапа 0 **не утверждён и не исполнялся**; архитектурный этап 0 (матрица паритета) и последующие этапы не отменены и не отмечаются выполненными.
- Продуктовая реализация требует отдельного утверждённого плана и пройденных approval gates; этим handoff **не** разрешена.
- Коммиты/push/merge этими правками **не** выполняются.
