# Collection status: 365-366

- collection-id: 365-366
- form: collection
- plan_cap: 4
- lane_cap: 1
- cap_reason: both items share the nextorm.extensions write surface and each runs heavyweight container-backed integration and performance measurement that must be serialized
- base_ref: 83c545767cdd46cb58d054445c3ce028d4d55b0d
- branch: 1.0.9-rc5
- owner: pdca-orchestrator (autonomous)

## Input

User task: «пдца по задачам 365 366 автономно автокомит»

Constraints: `по задачам 365 366`, `автономно`, `автокомит`.

### T365

Issue #365 — nextorm.extensions: SelectWhereMinEx/SelectWhereMaxEx on public query components (legacy frozen as baseline)
https://github.com/AlexeyShirshov/nextorm/issues/365 · milestone 1.0.9-rc5 · OPEN

```text
## Status and boundaries

Scope agreed in conversation after #362 closed `incomplete` (AC7 infeasible while the component was required to emit the AC4 rank-window shape against an aggregate+join baseline). No written spec and no implementation plan yet; product implementation is **not started**. This issue is the tracking record for the next slice; it stays in milestone `1.0.9-rc5`.

## Intent

Ship a **product** extension package built entirely on the public query-component layer, next to the frozen built-in operator:

- keep the current `SelectWhereMin` / `SelectWhereMax` implementation **untouched** as the compatibility path and as the performance **baseline**;
- add `SelectWhereMinEx` / `SelectWhereMaxEx` that are implemented **only** through public query components (`IQueryComponent` + `EntityBuilder.WithComponent`), with **zero** intersection with the legacy implementation;
- put the new implementation in a separate project/package `nextorm.extensions`.

The component layer was already proven end-to-end in #362 by an external no-IVT library; this issue is the productization of that proof, not a new design.

## Frozen legacy (must not change)

`SelectWhereMin` / `SelectWhereMax`, `SqlBuilder.MakeExtremeRowSelect`, `MakePortableExtremeRowSelect`, `MakePortableExtremeRowInner`, `MakeNativeExtremeRowSelect`, `ExtremeRowLowering`, `ExtremeRowCompatibility`, the `QueryCommand.ExtremeRow*` fields, and the `InMemoryExtremeRow` path. Verified unchanged by diff in the implementing cycle. They serve two purposes only: compatibility and the benchmark baseline.

## Design

- **Project**: `src/nextorm.extensions`, a separate NuGet package that references `nextorm.core` only (no provider references, no `InternalsVisibleTo`, no raw SQL, no global handler registration).
- **API**: `SelectWhereMinEx` / `SelectWhereMaxEx` as ordinary extension methods on `EntityBuilder<TEntity>`, attaching one query component with a stable slot; Min and Max share one slot (assignment replaces in place).
- **No shared infrastructure with legacy**: no `ExtremeRow*` command fields, no new branches in `SqlBuilder.MakeSelect` / `InMemoryQueryBuilder`, no new rows in `QueryPlanEqualityComparer`, no reuse of `ExtremeRowLowering` / `ExtremeRowCompatibility`.
- **SQL shape is free**: the component may emit the aggregate+join shape where it is faster (as legacy does for `All`) and a rank-window shape where that wins. The AC4 rank-window mandate from #362 was a *proof* constraint, not a product requirement, and it is **not** carried here.
- **Seed implementation**: the proven `ExternalSelectWhereMin/Max` library under `tests/nextorm.querycomponents` (52 assertions, semantic parity matrix, six-provider SQL generation and live coverage). The no-friend test assembly keeps proving the packaged surface through an ordinary package reference (no IVT).
- **Caching**: component state and semantics survive clones; structural hash **and** equality participate in the plan key; `BuildQuery` runs on the cache miss only and never on a confirmed warm hit; parameter rebinding (captured and positional) works with a warm cache and does not leak.

## Acceptance criteria

- **AC1 Packaging & surface** — `nextorm.extensions` builds and packs as its own package, references `nextorm.core` only, exposes `SelectWhereMinEx`/`SelectWhereMaxEx` and nothing else public beyond the component wiring; no IVT anywhere; no raw SQL; no global registration.
- **AC2 Semantic parity with the frozen legacy** — `One`/`All`, scalar/composite values, global/grouped, NULL keys and NULL group keys, input `Where`, ties, scalar/DTO/anonymous projections, `.Select(...)` after, outer `OrderBy`/`ThenBy`, paging, and the same rejection boundaries (distinct, set operations, aggregates, unsupported shapes, in-memory policy), verified by paired execution against legacy on the same database.
- **AC3 Component semantics** — multiple independent components, same-slot replacement (Min→Max, Max→Min) without nesting, explicit rejection of conflicting exclusive reshape slots, structural hash-collision discrimination via `Equals`, clone/`CloneForCache`/`ResetPreparation` parity, hidden-alias collision safety for user columns named `rn`/`rank`/`__nextorm_ext_rn`.
- **AC4 Plan cache & rebinding** — `BuildQuery` only on a cache miss, zero calls on a confirmed warm hit (counter-asserted), changing captured/positional values with a warm cache rebind without leakage, no `queryCommand.Cache = false`, no cache disable.
- **AC5 Providers** — SQL-generation tests plus a live integration matrix on PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse and SQLite; no component in the in-memory provider for integration; no-component regression tests for ordinary commands.
- **AC6 Performance vs frozen legacy** — for every mandatory scenario (`Min/Max × One/All × global/grouped × scalar/composite` plus no-component controls): `CI99_upper(T_ex / T_legacy) <= 1.0` (or throughput `CI99_lower >= 1.0`) **and** `Alloc_ex <= Alloc_legacy`. SQL shape is free, the threshold is not; an inconclusive interval counts as unmet.
- **AC7 Docs & hygiene** — EN + RU docs for the new package and API, no links to `docs/specs/**`, build `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors, CRLF, CPM, coverage thresholds 85/75 and 65/74 unchanged.
- **AC8 Legacy untouched** — a diff check proves none of the frozen legacy files changed; only `nextorm.extensions`, its tests, docs and the solution membership are touched.
  - Замороженный список из тела `#365` (Frozen legacy, must not change): `SelectWhereMin` / `SelectWhereMax`, `SqlBuilder.MakeExtremeRowSelect`, `MakePortableExtremeRowSelect`, `MakePortableExtremeRowInner`, `MakeNativeExtremeRowSelect`, `ExtremeRowLowering`, `ExtremeRowCompatibility`, the `QueryCommand.ExtremeRow*` fields, and the `InMemoryExtremeRow` path.

## Out of scope

- Migrating, deprecating or removing the legacy `SelectWhereMin/Max`.
- Changing `#302` lowering.
- A public opaque-SQL-fragment primitive (separate user-level decision; see #362).
- Re-basing `#362`'s AC7 measurement.

## Related

- #362 — component proof; closed `incomplete` because AC4's rank-window mandate could not meet the aggregate+join baseline.
- #302 — frozen internal lowering.
- #153 — external no-IVT precedent.
- #319 — build-once candidate.
```

### T366

Issue #366 — NativeAOT: full nextorm feature parity and executable integration coverage
https://github.com/AlexeyShirshov/nextorm/issues/366 · milestone 1.0.9-rc5 · OPEN

```text
## Goal / agreed requirements
Support nextorm under .NET 10 NativeAOT with functional parity for ALL currently supported nextorm features and provider/feature combinations, not just removal of the first MetadataToken crash. The user explicitly requires all capabilities to be covered; no SQLite-only or reduced-feature AOT support contract.

## Reproduction context
A .NET 10 file-based SQLite reporting application using nextorm/nextorm.sqlite 1.0.9-rc4 publishes as a small NativeAOT executable but fails on its first query with InvalidOperationException: There is no metadata token available for the given member, reported at NextORM.Core.MemberInfoExtensions.SameProperty. Its query shapes include interface entities, captured predicates, anonymous projections, nullable scalar mapping, SQLite json_extract, grouping and aggregates. The external application is ONLY an example; it is not a test fixture and its repository must not be modified.

## Agreed test architecture requirements
- Add a dedicated executable NativeAOT test project to the solution (not merely a normal JIT unit test).
- Reuse existing integration databases, schemas, seed data, connection resolution and container lifecycle. Do not invent a separate test database infrastructure.
- The integration host resolves/starts/seeds the existing provider once and passes the actual resolved connection to the native child process. In particular SQLite must use the parent's existing file rather than the child's process-derived default. The child must not independently reseed or manage containers.
- Publish with PublishAot=true and run the resulting native executable. Compare semantic results against existing expected values/JIT behavior; successful compilation alone is insufficient. Cover runtime execution and failures in automation/CI.
- Coverage must include every existing supported feature/provider combination and all public product surfaces, including core/in-memory, providers, nextorm.extensions and the EF integration surface. Preserve existing dialect capability differences; do not manufacture SQL features unsupported by a provider.
- Missing AOT functionality/driver support is a blocker to full acceptance, not grounds for silently skipping or reducing the requirement.

## Known investigation areas (facts, not a preapproved implementation)
- Token-based property identity appears in SameProperty, insert member comparison, query-filter validation and preparer filter-member keys. Preserve inherited/reflected/hidden/interface-property semantics and plan-cache correctness.
- Runtime Reflection.Emit for composite many-to-many junction carriers must have an AOT-capable implementation.
- Runtime generic construction, metadata preservation/reflection and JSON serialization paths require NativeAOT analysis and runtime tests.
- Expression.Compile is not categorically unavailable: .NET uses interpretation under NativeAOT. Source generation is a possible tool, not yet a universally required redesign.
- There is a real JoinAliasGenerator alongside the empty AnonymousClassGenerator stub; do not assume the generator project is entirely inactive.
- Native compatibility of each dependency/driver must be verified, not inferred from another driver's success.

## Acceptance
1. Repro-class queries and every supported public feature/provider combination execute correctly in published NativeAOT tests using existing integration infrastructure.
2. CRUD, query translation and materialization, joins/eager loading (including composite junctions), filters/cache behavior, converters/JSON/CSV/streaming, advanced query constructs and provider-specific operations retain JIT semantics wherever currently supported.
3. Normal JIT behavior, public API compatibility and existing test suites remain intact. Any required generation/registration contract must be explicitly designed and approved before implementation; no silent API narrowing.
4. AOT/trim diagnostics are accounted for and root causes addressed. No blanket warning suppression, disabling trimming, or rooting the entire dependency graph as a substitute for compatibility.
5. Automated gates run actual native binaries and report feature/provider evidence; unsupported dependencies or unexecuted cases are not reported green.
6. Public documentation and its Russian mirror accurately describe the accepted support contract.

## Open design questions
- How to replace runtime-emitted carriers and guarantee required closed generic instantiations while preserving public behavior.
- Whether source-generated metadata/materializers/registrations are required, and how they are discovered or explicitly declared for dynamically composed queries and external assemblies.
- Exact reusable shared test-support boundary and feature/provider coverage ledger.
- Third-party blockers, especially driver and EF integration behavior; no feature exclusions have been approved.

## Status / gates
Requirements above are agreed in conversation. Architecture/design and written specification are NOT approved. No implementation plan has been prepared; product implementation is NOT authorized. Issue creation is tracking only. Design draft: `docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md` (local/uncommitted; builder check in progress; not approved). Implementation plan: not prepared. Milestone uses the verified current branch's exact matching existing milestone; this is not a promise of release readiness.


## Additional agreed requirement — zero-configuration migration

Added after the requirements above were agreed; it constrains *how* AOT support may be delivered. It does not relax or replace anything above.

- Existing applications must reach full NativeAOT support by enabling `PublishAot=true` **only**. No manual listing or registration of entity, projection or query shapes; no per-type rooting configuration; no separate hand-authored AOT-specific code paths in the consuming application.
- If source generation is needed, it must be delivered automatically through the nextorm package/analyzer (discovered and wired in as part of the normal package reference) and must require no new opt-in switch and no new consumer-written code.
- Existing public API and runtime-composed query behavior must be preserved to the extent already supported; this requirement does not authorize narrowing the supported surface.
- Any feasibility conflict with this zero-configuration constraint is an explicit **blocking finding** for the cycle/design, not permission to shrink the contract. It must be recorded and escalated, not silently resolved by requiring consumer configuration.
- This answers the discovery half of the earlier open question "Whether source-generated metadata/materializers/registrations are required, and how they are discovered or explicitly declared for dynamically composed queries and external assemblies": where required, discovery must be automatic and cover dynamically composed queries and external assemblies without consumer declaration. The architectural mechanism itself remains unapproved.

All previous requirements remain in force. No architecture, specification, plan or implementation is approved by this addition.
```

## Entry PLAN

```text
# PLAN (Entry Classification & Contract)

## 1. Форма, специализированный скилл (overlay) и входной объём (Entry scope)

- **Форма (machine ID)**: `collection`
  - **Внутренняя форма элементов коллекции**:
    - Элемент 1 (`T365`, issue `#365`, цикл `N=2`): `cycle` — реализация пакета `nextorm.extensions` (`SelectWhereMinEx`/`SelectWhereMaxEx`) и тесты AC1–AC5, AC7–AC8 уже доставлены в коммите `9014f688d` (цикл `N=1`, `.pdca/status/T365-rc5-1.md`, закрыт `incomplete` исключительно по AC6 на 6 тёплых `All`-строках с отношением `1.000–1.005`); метод закрытия остатка известен и объём ограничен проверкой эквивалентности порождаемого SQL/плана для `All` и парным измерением с контролем дрейфа (A/A baseline).
    - Элемент 2 (`T366`, issue `#366`, цикл `N=1`): `incremental` — несмотря на зафиксированный в `HEAD` проект архитектуры (`docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md`), поведение .NET 10 `PublishAot=true` без пользовательской конфигурации (zero-configuration migration) в сочетании с source generator, заменой `MetadataToken`/`Reflection.Emit`, EF Core surface и 6 внешними драйверами СУБД в нативном бинарнике содержит зависимые от рантайм-диагностики неизвестные, где результат каждого шага определяет состав следующего.
- **Специализированный скилл / overlay**: `coder+.NET` (`pdca-dotnet`, включает `pdca-coder`) + проектный оверлей `nextorm-pdca`.
- **Обоснование формы (одной строкой)**: задача пользователя задаёт два независимых открытых GitHub issue (`#365` и `#366`) с собственными приёмочными контрактами и жизненным циклом закрытия; внутри коллекции `#365` имеет известный метод и узко ограниченный остаток (`cycle`, `N=2` после `incomplete` в `N=1`), а `#366` требует пошагового подтверждения нативной компиляции и исполнения по слоям и провайдерам (`incremental`, `N=1`).
- **Порядок и зависимость элементов коллекции**:
  1. **`T365` (`#365`, `N=2`)** выполняется первым.
  2. **`T366` (`#366`, `N=1`)** выполняется вторым после завершения `T365`, поскольку контракт `#366` требует покрытия всех публичных поверхностей продукта, явно включая `nextorm.extensions`.
- **Entry scope (входной объём)**:
  - На уровне коллекции: набор из двух элементов — `T365` (issue `#365`) и `T366` (issue `#366`).
  - Для `T365` (`cycle`, `N=2`): весь объём задачи `#365` (AC1–AC8) с опорой на уже доставленное в `N=1` состояние (`HEAD = 83c545767cdd46cb58d054445c3ce028d4d55b0d`) и закрытием единственного открытого критерия AC6 без регрессии остальных критериев.
  - Для `T366` (`incremental`, `N=1`, шаг 1 — measurement-first / baseline): вычисление текущего базового состояния совместимости `PublishAot=true` и воспроизводимого отказа (`MetadataToken` / trim- и AOT-диагностики / текущее состояние нативной сборки и запуска) выбранным методом измерения до внесения продуктовых изменений.
- **Привязка к снимку (Re-anchor)**: репозиторий `/home/alex/sources/nextorm`, ветка `1.0.9-rc5`, `HEAD = 83c545767cdd46cb58d054445c3ce028d4d55b0d` (дерево чистое).

## 2. Цель, запрашиваемый/доставляемый результат и критерии приёмки

### Исходное задание пользователя (verbatim)
«пдца по задачам 365 366 автономно автокомит»

### Пользовательские ограничения (verbatim)
`по задачам 365 366`, `автономно`, `автокомит`.

### Запрашиваемый и доставляемый результат
- **Запрашиваемый результат (requested result)**: выполненные и верифицированные по своим полным приёмочным контрактам задачи GitHub `#365` (`nextorm.extensions`: `SelectWhereMinEx`/`SelectWhereMaxEx` поверх публичных query components при замороженном legacy baseline) и `#366` (полная функциональная совместимость nextorm с .NET 10 NativeAOT при `PublishAot=true` без ручной конфигурации и исполняемое интеграционное покрытие нативных бинарников по всем поддерживаемым фичам и провайдерам), закоммиченные в соответствии с политикой автокоммита и с закрытием соответствующего GitHub issue только при полном принятии всей задачи этого issue.
- **Доставляемый результат (delivered result)**: тот же самый объект — принятые по полным критериям `#365` (AC1–AC8) и `#366` (Acceptance 1–6 + архитектурные и zero-configuration требования из тела `#366`) изменения в репозитории `/home/alex/sources/nextorm` на ветке `1.0.9-rc5`, статусные записи в `.pdca/status/**`, коммиты только затронутых путей цикла/элемента и закрытие `#365` / `#366` исключительно при полном закрытии соответствующего issue.

### Элемент коллекции 1: `T365` — GitHub Issue `#365` (`N=2`)

#### Вход из предыдущего терминального исхода (обязательный вход по инварианту 12)
- Предыдущий цикл: `.pdca/status/T365-rc5-1.md` (`N=1, r=1, n=3/3`), терминальный исход ACT: `incomplete` (исходящая фаза DO, последний DO-коммит `9014f688d`).
- Доставлено в `N=1`: проект `src/nextorm.extensions` (ссылается только на `nextorm.core`), `SelectWhereMinEx`/`SelectWhereMaxEx` (4 перегрузки; `One` = rank-window, `All` = aggregate+join), 62 unit-теста, генерация SQL на 6/6 провайдерах, live-матрица 68/68, документация EN+RU, доказательство план-кэша, чистая сборка и DocFX, 16/16 сценариев по аллокациям (`Alloc_ex <= Alloc_legacy`) и 10/16 сценариев по времени (`CI99_upper <= 1.0`).
- Остаток из `N=1`: только **AC6** по времени на 6 тёплых `All`-строках (`Max-All-global-scalar`, `Max-All-global-composite`, `Max-All-grouped-scalar`, `Min-All-global-scalar`, `Min-All-global-composite`, `Min-All-grouped-scalar`), где `CI99_upper(T_ex / T_legacy)` составил `1.000–1.005` (4 строки статистически выше `1.0` на `0.4–0.5 %`, 2 строки — неразличимый интервал) из-за межпрогонного дрейфа и отсутствия проверки побайтовой идентичности SQL/плана для `All` и A/A-контроля.

#### Приёмочные критерии `T365` (verbatim из тела issue `#365`, без изменений и добавлений)

- **T365-AC1 (`AC1 Packaging & surface`)** — `nextorm.extensions` builds and packs as its own package, references `nextorm.core` only, exposes `SelectWhereMinEx`/`SelectWhereMaxEx` and nothing else public beyond the component wiring; no IVT anywhere; no raw SQL; no global registration.
- **T365-AC2 (`AC2 Semantic parity with the frozen legacy`)** — `One`/`All`, scalar/composite values, global/grouped, NULL keys and NULL group keys, input `Where`, ties, scalar/DTO/anonymous projections, `.Select(...)` after, outer `OrderBy`/`ThenBy`, paging, and the same rejection boundaries (distinct, set operations, aggregates, unsupported shapes, in-memory policy), verified by paired execution against legacy on the same database.
- **T365-AC3 (`AC3 Component semantics`)** — multiple independent components, same-slot replacement (Min→Max, Max→Min) without nesting, explicit rejection of conflicting exclusive reshape slots, structural hash-collision discrimination via `Equals`, clone/`CloneForCache`/`ResetPreparation` parity, hidden-alias collision safety for user columns named `rn`/`rank`/`__nextorm_ext_rn`.
- **T365-AC4 (`AC4 Plan cache & rebinding`)** — `BuildQuery` only on a cache miss, zero calls on a confirmed warm hit (counter-asserted), changing captured/positional values with a warm cache rebind without leakage, no `queryCommand.Cache = false`, no cache disable.
- **T365-AC5 (`AC5 Providers`)** — SQL-generation tests plus a live integration matrix on PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse and SQLite; no component in the in-memory provider for integration; no-component regression tests for ordinary commands.
- **T365-AC6 (`AC6 Performance vs frozen legacy`)** — for every mandatory scenario (`Min/Max × One/All × global/grouped × scalar/composite` plus no-component controls): `CI99_upper(T_ex / T_legacy) <= 1.0` (or throughput `CI99_lower >= 1.0`) **and** `Alloc_ex <= Alloc_legacy`. SQL shape is free, the threshold is not; an inconclusive interval counts as unmet.
- **T365-AC7 (`AC7 Docs & hygiene`)** — EN + RU docs for the new package and API, no links to `docs/specs/**`, build `dotnet build nextorm.slnx -c Debug` = 0 warnings / 0 errors, CRLF, CPM, coverage thresholds 85/75 and 65/74 unchanged.
- **T365-AC8 (`AC8 Legacy untouched`)** — a diff check proves none of the frozen legacy files changed; only `nextorm.extensions`, its tests, docs and the solution membership are touched.
  - Замороженный список из тела `#365` (Frozen legacy, must not change): `SelectWhereMin` / `SelectWhereMax`, `SqlBuilder.MakeExtremeRowSelect`, `MakePortableExtremeRowSelect`, `MakePortableExtremeRowInner`, `MakeNativeExtremeRowSelect`, `ExtremeRowLowering`, `ExtremeRowCompatibility`, the `QueryCommand.ExtremeRow*` fields, and the `InMemoryExtremeRow` path.

### Элемент коллекции 2: `T366` — GitHub Issue `#366` (`N=1`)

#### Приёмочные критерии `T366` (verbatim из тела issue `#366`, без изменений и добавлений)

- **T366-AC1 (`Acceptance 1`)** — Repro-class queries and every supported public feature/provider combination execute correctly in published NativeAOT tests using existing integration infrastructure.
- **T366-AC2 (`Acceptance 2`)** — CRUD, query translation and materialization, joins/eager loading (including composite junctions), filters/cache behavior, converters/JSON/CSV/streaming, advanced query constructs and provider-specific operations retain JIT semantics wherever currently supported.
- **T366-AC3 (`Acceptance 3`)** — Normal JIT behavior, public API compatibility and existing test suites remain intact. Any required generation/registration contract must be explicitly designed and approved before implementation; no silent API narrowing.
- **T366-AC4 (`Acceptance 4`)** — AOT/trim diagnostics are accounted for and root causes addressed. No blanket warning suppression, disabling trimming, or rooting the entire dependency graph as a substitute for compatibility.
- **T366-AC5 (`Acceptance 5`)** — Automated gates run actual native binaries and report feature/provider evidence; unsupported dependencies or unexecuted cases are not reported green.
- **T366-AC6 (`Acceptance 6`)** — Public documentation and its Russian mirror accurately describe the accepted support contract.
- **T366-ARCH (`Agreed test architecture requirements`, verbatim из `#366`)**:
  - Add a dedicated executable NativeAOT test project to the solution (not merely a normal JIT unit test).
  - Reuse existing integration databases, schemas, seed data, connection resolution and container lifecycle. Do not invent a separate test database infrastructure.
  - The integration host resolves/starts/seeds the existing provider once and passes the actual resolved connection to the native child process. In particular SQLite must use the parent's existing file rather than the child's process-derived default. The child must not independently reseed or manage containers.
  - Publish with PublishAot=true and run the resulting native executable. Compare semantic results against existing expected values/JIT behavior; successful compilation alone is insufficient. Cover runtime execution and failures in automation/CI.
  - Coverage must include every existing supported feature/provider combination and all public product surfaces, including core/in-memory, providers, `nextorm.extensions` and the EF integration surface. Preserve existing dialect capability differences; do not manufacture SQL features unsupported by a provider.
  - Missing AOT functionality/driver support is a blocker to full acceptance, not grounds for silently skipping or reducing the requirement.
- **T366-ZEROCONF (`Additional agreed requirement — zero-configuration migration`, verbatim из `#366`)**:
  - Existing applications must reach full NativeAOT support by enabling `PublishAot=true` **only**. No manual listing or registration of entity, projection or query shapes; no per-type rooting configuration; no separate hand-authored AOT-specific code paths in the consuming application.
  - If source generation is needed, it must be delivered automatically through the nextorm package/analyzer (discovered and wired in as part of the normal package reference) and must require no new opt-in switch and no new consumer-written code.
  - Existing public API and runtime-composed query behavior must be preserved to the extent already supported; this requirement does not authorize narrowing the supported surface.
  - Any feasibility conflict with this zero-configuration constraint is an explicit **blocking finding** for the cycle/design, not permission to shrink the contract. It must be recorded and escalated, not silently resolved by requiring consumer configuration.
  - Where required, discovery must be automatic and cover dynamically composed queries and external assemblies without consumer declaration.

## 3. Метод измерения (Measurement method)

### 3.1. Для `T365-AC6` (производительность и аллокации против замороженного legacy baseline)
- **Метрики и единицы**:
  1. Отношение времени выполнения на запрос: безразмерное отношение `R_T = T_ex / T_legacy` (на основе наносекунд/операцию в тёплом состоянии план-кэша и эквивалентно для холодного состояния при его измерении) и верхняя граница 99 % доверительного интервала `CI99_upper(T_ex / T_legacy)` (или эквивалентно нижняя граница пропускной способности `CI99_lower(Throughput_ex / Throughput_legacy)`).
  2. Выделенная управляемая память на операцию в тёплом состоянии: `Alloc_ex` и `Alloc_legacy` (в байтах на операцию, `B/op`).
- **Знаменатель (Denominator)**: полная обязательная матрица из 16 основных сценариев `Min/Max × One/All × global/grouped × scalar/composite` плюс контрольные сценарии без компонентов (`no-component controls`); в каждом сценарии знаменателем отношения `R_T` служит измерение замороженной legacy-реализации (`SelectWhereMin` / `SelectWhereMax`) на тех же данных и том же экземпляре БД.
- **Фильтры включения/исключения**:
  - Включаются все 16 обязательных комбинаций матрицы и контрольные no-component сценарии.
  - Исключаются прогревочные итерации (warmup) из тёплой статистики; тёплые измерения проводятся только после подтверждённого попадания в кэш плана (0 вызовов `BuildQuery`).
- **Условия сопоставимости (Comparability conditions)**:
  - Парное (interleaved / paired) исполнение `ex` и `legacy` в одном процессе, на одной и той же схеме и идентичном наборе строк БД, при одинаковой конфигурации GC/JIT (Release), с нейтрализацией позиционного/межпрогонного дрейфа и проверкой A/A-контроля (`legacy` против `legacy`), чтобы систематический сдвиг таймера не искажал отношение `T_ex / T_legacy`.
- **Порог и его происхождение (Threshold & provenance)**:
  - Задан пользователем в теле `#365` (`AC6`): для каждого обязательного сценария `CI99_upper(T_ex / T_legacy) <= 1.0` (или `CI99_lower(Throughput) >= 1.0`) **и** `Alloc_ex <= Alloc_legacy`.
  - **Правило для неопределённого интервала**: неразрешённый/широкий доверительный интервал (`CI99_lower <= 1.0 < CI99_upper`) считается **невыполненным** (`unmet`), точно как предписано в `AC6` ("an inconclusive interval counts as unmet").

### 3.2. Для покрытия кода и сборки (`T365-AC7`, `T366-AC3`, `T366-AC4`, `T366-AC5`)
- **Сборка и анализаторы (`dotnet build` / `dotnet publish -p:PublishAot=true`)**:
  - Метрика: число ошибок и предупреждений компилятора, nullable- и AOT/trim-анализаторов (`IL2xxx`, `IL3xxx`, `CSxxxx`).
  - Порог: `0 errors`, `0 warnings` (при `TreatWarningsAsErrors=true`, без пакетного подавления `IL2xxx`/`IL3xxx` или отключения тримминга). Происхождение: правила репозитория (`AGENTS.md`) и `#365 AC7` / `#366 Acceptance 4`.
- **Покрытие кода (Line / Branch coverage)**:
  - Метрика: процент покрытия строк (`Line coverage, %`) и ветвей (`Branch coverage, %`) по Cobertura-отчёту.
  - Знаменатель и фильтры:
    1. Общий гейт решения (`coverage.settings.xml`, сборки `nextorm.{core,sqlite,postgres,sqlserver}`): пороги `Line >= 85 %`, `Branch >= 75 %`.
    2. Интеграционный гейт (`coverage.integration.settings.xml`, все ссылаемые провайдерные сборки за вычетом `-NextORM.Core.InMemory*` и членов с `[ExcludeFromCodeCoverage]`): пороги сохраняются без снижения (`Line >= 65 %`, `Branch >= 75 %` в `AGENTS.md` / `65/74` в тексте `#365 AC7`).
- **Матрица NativeAOT (`T366-AC1`, `T366-AC2`, `T366-AC5`)**:
  - Метрика: доля успешно опубликованных (`PublishAot=true`) и прошедших рантайм-исполнение в нативном дочернем процессе поддерживаемых комбинаций `feature × provider` с совпадением семантического результата с ожидаемым JIT-поведением.
  - Знаменатель: все поддерживаемые публичные поверхности (`core`/in-memory, SQLite, PostgreSQL, SQL Server, MySQL, MariaDB, ClickHouse, `nextorm.extensions`, EF Core integration) и все поддерживаемые конкретным диалектом фичи; пропуски контейнеров через `NEXTORM_SKIP_PROVIDERS` не допускаются и не засчитываются как доказательство.
  - Порог: `100 %` поддерживаемых комбинаций исполнены в нативном бинарнике с `0` сбоев и `0` пропусков сверх документированного dialect-capability skip budget.

---

## 4. Ограничения и допущения (Constraints & Assumptions)

### Ограничения
1. **Замороженный legacy в `#365`**: файлы и члены legacy-реализации `SelectWhereMin`/`SelectWhereMax` (`SqlBuilder.MakeExtremeRowSelect`, `MakePortableExtremeRowSelect`, `MakePortableExtremeRowInner`, `MakeNativeExtremeRowSelect`, `ExtremeRowLowering`, `ExtremeRowCompatibility`, поля `QueryCommand.ExtremeRow*`, `InMemoryExtremeRow`) неприкосновенны; отсутствие изменений подтверждается `git diff`.
2. **Изоляция `nextorm.extensions`**: только ссылка на `nextorm.core`, никаких ссылок на провайдеры, никакого `InternalsVisibleTo`, никакого raw SQL, никакой глобальной регистрации обработчиков, никакого мутирования `queryCommand.Cache = false`.
3. **Zero-configuration NativeAOT в `#366`**: потребитель включает только `<PublishAot>true</PublishAot>`; запрещены ручная регистрация сущностей/проекций/запросов, пользовательские `rd.xml`/`TrimmerRootAssembly`, отдельные ручные AOT-ветки кода у потребителя и сужение поддерживаемого публичного API.
4. **Архитектура тестирования `#366`**: отдельный исполняемый NativeAOT тестовый проект в `nextorm.slnx`; переиспользование существующей интеграционной инфраструктуры БД/контейнеров/сидинга; родительский хост поднимает и сидит БД один раз и передаёт разрешённую строку подключения нативному дочернему процессу (включая путь к существующему файлу SQLite). Внешний репро-репозиторий не модифицируется.
5. **Интеграционные контейнеры (fail-closed)**: обязателен `DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`; прогон с `NEXTORM_SKIP_PROVIDERS` не является доказательством.
6. **Гигиена репозитория**: CRLF во всех изменяемых файлах, Central Package Management (`Directory.Packages.props`), `TreatWarningsAsErrors=true`, запрет ссылок на `docs/specs/**` из публичной документации (`docs/**`, `docs/ru/**`, `readme.md`).
7. **Политика автокоммита**:
   - Коммитить **только** пути, принадлежащие текущему элементу/циклу, по одному явному `git add <path>` на путь;
   - Запрещены `git add -A` и `git add .`;
   - Запрещены `git push`, `git merge` / merge-commit в `main` или текущую ветку без отдельной инструкции, создание тегов/релизов;
   - `.pdca/**` кроме `.pdca/status/**` — локальные артефакты доказательств и **не коммитятся**;
   - Закрытие GitHub issue (`gh issue close 365` / `gh issue close 366`) выполняется **только** при полном принятии всех критериев соответствующего issue (`done`); при `incomplete`/остатке issue остаётся `OPEN`.
8. **Обязательный provenance-гейт перед CHECK**: манифест доказательств должен проходить `pdca_evidence.py lint` и `validate_inner_loop.py manifest` с кодом возврата `0` до чтения манифеста верификатором `pdca-check`.

### Допущения
1. Закоммиченный в `HEAD` (`83c545767cdd46cb58d054445c3ce028d4d55b0d`) проектный документ `docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md` вместе с прямым указанием пользователя `«пдца по задачам 365 366 автономно автокомит»` является авторизованной отправной архитектурной спецификацией для `#366`; при обнаружении в ходе инкрементальной реализации неразрешимого конфликта с ограничением zero-configuration или несовместимости стороннего драйвера/EF Core цикл не сужает контракт самовольно, а фиксирует блокирующую находку и маршрутизирует её через `CHECK → PLAN` / `pdca-escalate`.
2. Локальный нативный тулчейн Linux для .NET 10 `PublishAot=true` (компилятор/линковщик `clang`/`zlib` в окружении WSL) доступен или проверяется на первом измерительном шаге `T366`; при отсутствии системного нативного линковщика это фиксируется как ресурсный блокер на шаге 1 `T366`.

---

## 5. Закрытие пробелов (Gaps)

| Пробел в постановке | Способ закрытия (Evidence / Assumption / Blocker) |
| --- | --- |
| В теле `#365` указано «product implementation is not started», тогда как в репозитории уже завершён цикл `N=1` (`9014f688d`, `.pdca/status/T365-rc5-1.md`) | **Evidence**: проверенный факт из окружения — `N=1` завершён со статусом `incomplete`, где доставлены `src/nextorm.extensions`, тесты и доки, а открытым остался только `AC6` на 6 тёплых `All`-строках (`1.000–1.005`). Цикл `T365` запускается как `N=2` поверх `HEAD`. |
| В теле `#366` указано, что дизайн `docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md` не закоммичен и реализация не авторизована | **Evidence**: `HEAD = 83c545767cdd46cb58d054445c3ce028d4d55b0d` — это именно коммит `docs(specs): NativeAOT full-compatibility design (2026-11-04)` (50 479 байт), после которого пользователь дал прямое поручение запустить автономный PDCA по задачам `#365` и `#366`. |
| Зависимость покрытия `#366` от пакета `nextorm.extensions` из `#365` | **Evidence / Порядок коллекции**: `#366` явно включает `nextorm.extensions` в обязательную поверхность NativeAOT-покрытия, поэтому в коллекции зафиксирован строгий порядок `T365 → T366`. |
| Совместимость сторонних ADO.NET-драйверов 6 СУБД и EF Core с `PublishAot=true` в рантайме | **Bounded incremental steps в `T366`**: проверяется инкрементально начиная с шага измерения базового состояния (Step 1); любая неподдерживаемая зависимость согласно `#366` трактуется как блокер полного принятия, а не повод для скрытого пропуска. |

---

## 6. Минимальное решение и альтернативы (Minimal solution & alternatives)

### Для `T365` (`#365`, `N=2`, форма `cycle`)
- **Выбранное минимальное решение**: сохранить уже доказанную в `N=1` реализацию `src/nextorm.extensions` и замороженный legacy-код; устранить причину расхождения 6 тёплых `All`-строк в `AC6` (проверить побайтовую идентичность порождаемого SQL-дерева/команды и накладные расходы в компоненте/оракуле при тёплом кэше, добавить A/A-контроль `legacy-vs-legacy`), добиться выполнения `CI99_upper(T_ex / T_legacy) <= 1.0` и `Alloc_ex <= Alloc_legacy` на всех 16 обязательных сценариях и контролях, и переподтвердить AC1–AC8.
- **Отвергнутая альтернатива**: переписывание `All` обратно на оконную функцию `RANK()` — отвергнуто ещё в `#362`/`#365` (`N=1`), так как оконная форма для `All` объективно проигрывает `aggregate+join` на базовом плане выполнения СУБД.

### Для `T366` (`#366`, `N=1`, форма `incremental`)
- **Выбранное минимальное решение**: инкрементальная кампания, опирающаяся на `docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md`:
  - **Шаг 1 (Measurement-first / Baseline)**: без изменения продуктового кода создать/подключить минимальный измерительный контур `PublishAot=true` (воспроизведение `MetadataToken` crash и сбор полного базового списка AOT/trim-предупреждений и рантайм-сбоев).
  - **Последующие инкременты** (открываются по решению `pdca-planner` на границах шагов после подтверждённого CHECK предыдущего шага):
    1. Устранение `MetadataToken`-зависимости в сравнении свойств/членов (`SameProperty`, insert/query-filter/preparer keys) и runtime `Reflection.Emit` для составных many-to-many junction carriers с сохранением семантики кэша планов и наследования/интерфейсов.
    2. Автоматическая zero-configuration генерация/презервация метаданных, материализаторов и обобщённых инстанциаций через встроенный в пакет source generator (`nextorm.core.sourcegenerator`, подключаемый автоматически без пользовательской конфигурации) + чистка AOT/trim-диагностик в `nextorm.core`, провайдерах, `nextorm.extensions` и `nextorm.entityframeworkcore`.
    3. Выделенный исполняемый NativeAOT интеграционный проект, запускаемый дочерним нативным процессом от родительского интеграционного хоста с передачей готовых подключений по всем 6 провайдерам, in-memory, `nextorm.extensions` и EF Core, плюс обновление EN/RU документации.
- **Отвергнутая альтернатива**: требовать от пользователя атрибуты/реестры типов или `rd.xml` — прямо запрещено разделом `Additional agreed requirement — zero-configuration migration` в `#366`.

---

## 7. Пути статусов и доказательств (Status & evidence paths)

Предложенные драйвером пути **принимаются и фиксируются как обязательные**:
- **Статус коллекции**: `.pdca/status/collection-365-366.md`
- **Корень доказательств уровня коллекции**: `.pdca/365-366/rv1/`
- **Элемент 1 (`T365`, issue `#365`, цикл `N=2`, `r=1`, `n=1/3`)**:
  - Статус элемента: раздел `T365` в `.pdca/status/collection-365-366.md` и файл статуса цикла `.pdca/status/T365-rc5-2.md` (с явной ссылкой на предшественника `.pdca/status/T365-rc5-1.md`).
  - Корень доказательств элемента: `.pdca/365-366/items/T365/rv2/`
- **Элемент 2 (`T366`, issue `#366`, цикл `N=1`, `r=1`, `n=1/3`)**:
  - Статус элемента: раздел `T366` в `.pdca/status/collection-365-366.md` и файл статуса кампании `.pdca/status/T366-rc5-1.md`.
  - Корень доказательств элемента: `.pdca/365-366/items/T366/rv1/`

---

## 8. Plan-to-DO handoff и правило выбора диспетчеризации для драйвера (`pdca-orchestrator`)

### Что каждый DO-диспетч переносит по ссылке (Plan-to-DO handoff)
1. Неизменённое задание пользователя: `«пдца по задачам 365 366 автономно автокомит»` и политика автокоммита из §4.
2. Привязанный снимок `HEAD = 83c545767cdd46cb58d054445c3ce028d4d55b0d` на ветке `1.0.9-rc5`.
3. Для **Элемента 1 (`T365`, `cycle`, `N=2`)**:
   - Полный набор критериев `T365-AC1`..`T365-AC8` и метод измерения из §3.1–§3.2;
   - Обязательный вход из `N=1` (`.pdca/status/T365-rc5-1.md`): фокус на закрытии остатка `T365-AC6` (6 тёплых `All`-строк) и подтверждении отсутствия регрессий по `T365-AC1`..`T365-AC8`;
   - Запрет любых правок замороженных legacy-файлов (`T365-AC8`) и запрет `queryCommand.Cache = false` (`T365-AC4`);
   - Путь доказательств `.pdca/365-366/items/T365/rv2/`.
4. Для **Элемента 2 (`T366`, `incremental`, `N=1`, стартовый Шаг 1 — Baseline / Measurement-first)**:
   - Полный набор критериев `T366-AC1`..`T366-AC6`, `T366-ARCH`, `T366-ZEROCONF` и метод измерения из §3.2;
   - Объём **Шага 1 (Measurement-first)**: вычислить и зафиксировать базовое состояние совместимости `PublishAot=true` (воспроизведение отказа `MetadataToken`, фиксация текущих диагностик `IL2xxx`/`IL3xxx` при публикации `PublishAot=true` и проверка наличия нативного тулчейна публикации) **без продуктовых изменений**;
   - После независимого CHECK Шага 1 драйвер обязан вернуть управление в `pdca-planner` (§Step boundary) для принятия базового уровня (knowledge increment) и выпуска брифа Шага 2;
   - Путь доказательств `.pdca/365-366/items/T366/rv1/`.

### Правило маршрутизации для драйвера (`pdca-orchestrator`)
1. Инициализировать статус коллекции `.pdca/status/collection-365-366.md` и статус `T365` (`.pdca/status/T365-rc5-2.md`) через `pdca-executor` с дословной записью этого плана.
2. Запустить **Элемент 1 (`T365`, форма `cycle`, оверлей `coder+.NET` / `nextorm-pdca`)**: `DO (pdca-executor) → provenance gate (pdca_evidence.py lint + validate_inner_loop.py manifest) → CHECK (pdca-check) → ACT`.
   - Если `T365` получает вердикт `PASS` по всем `T365-AC1`..`T365-AC8`: `pdca-executor` фиксирует `done`, коммитит только пути `T365` и `.pdca/status/**` по одному `git add <path>`, закрывает issue `#365` (`gh issue close 365`), после чего драйвер переходит к `T366`.
   - Если в `T365` возникает возврат `CHECK → DO`, используются попытки в пределах `n <= 3/3`; при контрактном дефекте — `CHECK → PLAN` (`pdca-planner`); на `n = 3/3` без закрытия — эскалация в `pdca-escalate` или фиксация `incomplete` (issue `#365` в этом случае остаётся `OPEN`), и переход к следующему элементу коллекции `T366`.
3. Запустить **Элемент 2 (`T366`, форма `incremental`, оверлей `coder+.NET` / `nextorm-pdca`)** начиная со стартового **Шага 1 (Measurement-first)**:
   - После каждого шага: `DO (pdca-executor) → provenance gate → CHECK (pdca-check) → граница шага в pdca-planner` (который в одном ходе принимает инкремент и выдаёт бриф следующего шага либо финальный CHECK / STOP).
   - Закоммичивать принятые инкременты по политике автокоммита; закрывать `#366` (`gh issue close 366`) только после финального `PASS` по всему набору `T366-AC1`..`T366-AC6`, `T366-ARCH`, `T366-ZEROCONF`.
4. По завершении обоих элементов финализировать `.pdca/status/collection-365-366.md` и закоммитить обновлённый файл статуса.

---

## 9. Риски и условия остановки (Risks & Stop conditions)

### Риски
1. **Тепловой/частотный дрейф CPU при микробенчмарке `T365-AC6`**: поскольку для `All` в `nextorm.extensions` и legacy исполняется одинаковая форма `aggregate+join`, малейший дрейф частоты процессора даёт смещение `0.3–0.5 %` вокруг `1.000`. Купируется проверкой побайтовой идентичности SQL/плана и парным измерением с A/A-контролем.
2. **Ограничения сторонних драйверов СУБД и EF Core под NativeAOT в `T366`**: отдельные провайдеры (`Npgsql`, `Microsoft.Data.SqlClient`, `MySqlConnector`, `ClickHouse.Driver`, `Microsoft.EntityFrameworkCore`) могут иметь собственные `IL2xxx`/`IL3xxx` предупреждения или рантайм-ограничения в `.NET 10 PublishAot=true`.
3. **Динамически конструируемые запросы и внешние сборки при `T366-ZEROCONF`**: автоматическое обнаружение через source generator без ручной регистрации в потребительском коде может столкнуться с границами видимости синтаксических деревьев между сборками.

### Условия остановки (Stop conditions)
1. **Успешное завершение элемента (`done`)**: независимый `pdca-check` подтвердил `met` по всем обязательным критериям элемента (`T365-AC1`..`T365-AC8` для `#365`; `T366-AC1`..`T366-AC6`, `T366-ARCH`, `T366-ZEROCONF` для `#366`) при зелёном provenance-гейте.
2. **Исчерпание бюджета попыток (`n = 3/3`, правило no-4th-pass)**: после третьей неуспешной попытки в рамках одной ревизии плана четвёртый проход запрещён — цикл обращается к `pdca-escalate` либо завершает шаг/элемент со статусом `incomplete`.
3. **Блокирующая невыполнимость ограничения (`T366-ZEROCONF` / сторонний драйвер)**: если на шаге `T366` доказано, что требование zero-configuration (`PublishAot=true` без пользовательского кода/конфигурации) или работа конкретного обязательного драйвера/EF технически невыполнимы без нарушения ограничений задачи, запрещено скрыто сужать контракт или пропускать провайдер; находка передаётся через `CHECK → PLAN` / `pdca-escalate` и при подтверждении невыполнимости переводит элемент в терминальный исход с открытым issue `#366` и полной записью в статусе и финальном отчёте.
4. **Аппаратный/инфраструктурный блокер**: недоступность Podman-сокета после попытки запуска машины или отсутствие системного нативного линковщика для `PublishAot=true` останавливает соответствующую фазу как ресурсный блокер без фальсификации зелёного результата.

---

## 10. Уверенность и проверенные факты (Confidence & verified vs assumed)

- **Проверено (Verified)**:
  - Чистое рабочее дерево на ветке `1.0.9-rc5`, `HEAD = 83c545767cdd46cb58d054445c3ce028d4d55b0d`.
  - Наличие в `HEAD` полной реализации `#365` из `N=1` (`9014f688d`) и закрытого статуса `.pdca/status/T365-rc5-1.md` (`incomplete` только по 6 тёплым `All`-строкам в AC6; аллокации 16/16 и остальные критерии AC1–AC5, AC7–AC8 были выполнены).
  - Наличие в `HEAD` спецификации `docs/superpowers/specs/2026-11-04-nativeaot-full-compatibility-design.md`.
  - Наличие `.NET SDK 10.0.401`, Podman-сокета и скриптов верификации в `.pdca/scripts/`.
- **Остаётся допущением до проверки в DO/Step 1**:
  - Что 6 тёплых `All`-строк в `T365-AC6` укладываются в `CI99_upper(T_ex / T_legacy) <= 1.0` после устранения любых остаточных различий в пути подготовки/кэширования и стабилизации парного протокола с A/A-контролем.
  - Что все 6 ADO.NET-драйверов и пакет `nextorm.entityframeworkcore` проходят нативную публикацию `PublishAot=true` и исполнение в `.NET 10` без нерешаемых сторонних блокеров.

```

## Phase decisions

- T365 item PLAN persisted → .pdca/status/T365-rc5-2.md (plan_state=ready)
- T366 charter persisted → .pdca/365-366/items/T366/preparation.md@r1 (preparation_state=ready)
- ALL-barrier: passed
- Schedule: dependency T365 → T366; group g1; one lane; current worktree/branch /home/alex/sources/nextorm @ 1.0.9-rc5; no worktree, no group/task branch, no merge; plan_cap=4, lane_cap=1; revision vector T365@r1, T366@r1
- Notice:

Pre-DO lane applicability check failed (blocked / STOP before DO): active concurrent third-party writer detected in working tree /home/alex/sources/nextorm on the #365 surface. After the initial 7-path dirty snapshot (21:43–21:55 local: src/nextorm.extensions/ExtremeRowExtensions.cs plus 6 docs/SVG paths), a second snapshot at 22:02 local showed two newly created diagnostic test files (tests/nextorm.clickhouse.tests/QueryComponents/TempExDump.cs at 22:01:59 and tests/nextorm.integration.tests/ClickHouse/TempLiveDump.cs at 22:02:29) exercising SelectWhereMinEx/SelectWhereMin and throwing unconditional exceptions. Dispatched DO work, builds, 0.4–0.5% AC6 timing attribution, and autocommits on branch 1.0.9-rc5 cannot run safely without racing and clobbering the active writer.

- Collection outcome: blocked — STOP before DO (group g1 incomplete, 0 commits, 0 accepted goals)

### Groups

| Group | Items | Worktree | Branch/ref | Status | Stop reason | Tip | Preserved results | Remainder | Patch path |
|---|---|---|---|---|---|---|---|---|---|
| g1 | T365, T366 | /home/alex/sources/nextorm (current) | 1.0.9-rc5 @ 83c54576 | incomplete | the group is stopped | unchanged, no commits | none | both items | none |

### Items

| Item | Group | Branch | Status | plan_state | PLAN/preparation | Form | Contract | Overlay | cycle_id | Status path | Revision | Attempt | Terminal | Reason | Disposition | Evidence root |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| T365 | g1 | 1.0.9-rc5 | incomplete | ready | plan_state=ready | cycle | pdca | coder+.NET + nextorm-pdca | T365-rc5-2 (N=2) | .pdca/status/T365-rc5-2.md | r1 | 0/3 | execution | pre-do blocked: concurrent writer; DO not dispatched | finalized | .pdca/365-366/items/T365/rv2/ |
| T366 | g1 | 1.0.9-rc5 | incomplete | n/a | preparation_state=ready; preparation_ref .pdca/365-366/items/T366/preparation.md@r1 | campaign | — | — | — | .pdca/status/T366-rc5-1.md (not created: never bootstrapped) | r1 | 0 | — | the group is stopped | finalized | .pdca/365-366/items/T366/rv1/ |

### General verification and recovery

- State: blocked
- Last common CHECK: not run — no candidate and no product change; recording inability needs no verification
- Blocking reason: Pre-DO lane applicability check failed (blocked / STOP before DO): active concurrent third-party writer detected in working tree /home/alex/sources/nextorm on the #365 surface. After the initial 7-path dirty snapshot (21:43–21:55 local: src/nextorm.extensions/ExtremeRowExtensions.cs plus 6 docs/SVG paths), a second snapshot at 22:02 local showed two newly created diagnostic test files (tests/nextorm.clickhouse.tests/QueryComponents/TempExDump.cs at 22:01:59 and tests/nextorm.integration.tests/ClickHouse/TempLiveDump.cs at 22:02:29) exercising SelectWhereMinEx/SelectWhereMin and throwing unconditional exceptions. Dispatched DO work, builds, 0.4–0.5% AC6 timing attribution, and autocommits on branch 1.0.9-rc5 cannot run safely without racing and clobbering the active writer.
- Defect history: `WC-1-concurrent-writer` (symptom/timeline: 7 paths at 21:55 → 9 paths at 22:02 with two `Temp*Dump.cs` added → `TempLiveDump.cs` deleted and `tests/nextorm.integration.tests/ClickHouse/ClickHouseQueryComponentsExIntegrationTests.cs` modified; effect: pre-DO STOP; prevention: exclusive workspace ownership before dispatching a measurable cycle) and `AC6-six-warm-All-rows` (carried from `T365` `N=1`, unattempted)
  - third observation (after the records were written): core files src/nextorm.core/Query/QueryCommand.QueryPreparer.cs and src/nextorm.core/Query/QueryComponents.cs modified, new tests/nextorm.sqlite.tests/QueryComponents/RawRecipeComponentIsolationTests.cs untracked, both Temp*Dump.cs gone and the ClickHouse test back to HEAD; HEAD unchanged; the recorded dirty-set snapshots are point-in-time, the fact of concurrent writing is not
- Next allowed step: restore exclusive workspace ownership, then a new authorized PLAN (`N=3` for `T365`, fresh charter check for `T366`).

### Completeness

- 0/2 goals accepted
- Integrated-tree verdict: none (no product change, nothing staged)
- Incomplete items: T365 — pre-do blocked: concurrent writer; DO not dispatched; T366 — the group is stopped
- Campaign: prepared, never bootstrapped
- GitHub issues: #365 and #366 both OPEN
- Commits made by this run: none

## Progress log

2026-11-04T17:17:11Z | PLAN | revision r1 | attempt n0 | collection PLAN persisted | plan entry above
2026-11-04T17:17:11Z | PLAN | revision r1 | attempt n0 | T365 plan ready | .pdca/status/T365-rc5-2.md
2026-11-04T17:17:11Z | PLAN | revision r1 | attempt n0 | T366 charter ready | .pdca/365-366/items/T366/preparation.md@r1
2026-11-04T17:17:11Z | PLAN | revision r1 | attempt n0 | ALL-barrier passed | phase decisions above
2026-11-04T17:17:11Z | DO | revision r1 | attempt n0 | pre-DO applicability check failed (concurrent writer) | phase decisions above
2026-11-04T17:17:11Z | ACT | revision r1 | attempt n0 | collection blocked — STOP before DO | phase decisions above
2026-11-04T17:17:11Z | ACT | revision r1 | attempt n0 | no commits, no push | none
2026-11-04T17:31:17Z | ACT | revision r1 | attempt n1/3 | third tree observation appended (core query files modified) — blocker unchanged | .pdca/status/collection-365-366.md
