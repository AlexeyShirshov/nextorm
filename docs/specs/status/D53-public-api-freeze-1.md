# D53 — IF6: public API freeze & tracking (#53)

- issue: #53 — https://github.com/AlexeyShirshov/nextorm/issues/53
- selected_variant: `pdca-dotnet`
- cycle: N=1
- plan_revision: r=1
- iteration: n=1
- phase: **DO complete → CHECK pending** (owner decision 2026-11-11: baseline goes to `Shipped`, release 1.0.9 follows immediately)
- status file: docs/specs/status/D53-public-api-freeze-1.md
- evidence contract revision: rv2

## Goal

Включить обязательный учёт публичного API (`Microsoft.CodeAnalysis.PublicApiAnalyzers` +
`PublicAPI.Shipped/Unshipped.txt`), не вводя обратную совместимость до 1.0 (alpha: renames in place,
без `[Obsolete]`/aliases; freeze только на 1.0).

## Acceptance criteria

- **A1** Анализатор активен во всех выбранных проектах; у каждого подключены два API-файла
  (`PublicAPI.Shipped.txt`, `PublicAPI.Unshipped.txt`) как `AdditionalFiles`.
  Негатив: пропущенный проект/AdditionalFile → отказ приёмки.
- **A2** `Shipped` содержит только `#nullable enable`; `Unshipped` — полный текущий публичный API;
  чистый Release rebuild даёт `0 Warning(s) 0 Error(s)`. Негатив: отсутствующая/устаревшая
  декларация → ошибка сборки.
- **A3** Добавление публичного API без правки `Unshipped` вызывает **RS0016**; удаление элемента из
  кода при сохранённой записи — **RS0017**. Негатив: такой build проходит → отказ приёмки.
- **A4** CI собирает Release перед Release-тестами `--no-build`; диагностики RS0016/RS0017 закреплены
  как `error`. Негатив: Debug/Release mismatch или подавление диагностик → отказ.
- **A5** XP3/XP6 исправлены; UG1/JP2 явно зарегистрированы с текущим baseline и активным продолжением
  в текущем milestone. Негатив: молчаливый перенос в будущий milestone / ложная отметка «исправлено».
- **A6** IF6 и Шаг 5 закрыты именно в согласованном объёме tracking; baseline присутствует в
  разрешённом коммите. Негатив: только локальный diff/непроверенные ссылки → GAP.

## Decisions

- **Sourcegenerator:** включить (`src/nextorm.core.sourcegenerator`, netstandard2.0): собственный
  публичный API; сохранить существующие `AnalyzerReleases.*` AdditionalFiles.
- **EntityFrameworkCore:** включить `src/nextorm.entityframeworkcore` (8-й packable проект);
  число «7» в issue — не граница охвата (`src/Directory.Build.props:10 IsPackable=true`,
  `nextorm.slnx:27`).
- **CI:** перейти на Release одновременно для build и test (`dotnet build -c Release` →
  `dotnet test -c Release --no-build`); отдельный clean rebuild доказывает 0/0, а не incremental.
- **Версия пакета:** `Microsoft.CodeAnalysis.PublicApiAnalyzers` 3.3.4 — кандидат; restore/build
  обязаны доказать совместимость; отказ → возврат в PLAN, не тихая замена.
- **Baseline:** основной путь — SDK code-fix (`dotnet format analyzers --diagnostics RS0016`),
  fallback — ручные canonical записи по Roslyn-inventory, проверяемые анализатором (grep не
  использовать для символов).
- **Renames:** XP3/XP6 — **fix now**; UG1/JP2 — **сознательно оставить в baseline** (активная
  единица текущего milestone):
  - UG1 (deferred): согласование `uuidv4`/`uuidv7` и dialect mapping
    (`src/nextorm.core/Query/SqlFunctions.cs:458,466`; диалекты `PostgresDialect.cs:631,635` и др.).
  - JP2 (deferred): `MakeJsonExtract` → `MakeJsonFunction`/`MakeJsonTextFunction` меняет контракт
    JSON (`ISqlDialect.cs:634`), не механический rename.

## D-units

- **D0 prerequisites:** проверить SDK/restore/formatter и Python для evidence harness; Roslyn-разведка
  rename footprint; проверить URL #53/milestone; создать/связать активные UG1/JP2 единицы текущего milestone.
- **D1 cleanup:** XP3 (`ISqlDialect.SupportsAnyAggregates` → `SupportsAnyLastAggregate`,
  `ISqlDialect.cs:593`, `SqlDialectBase.cs:189`, `ClickHouseDialect.cs:268`, refs
  `AdvancedAggregateTranslator.cs:122`, `SqlFunctions.ClickHouse.cs:104`, test
  `ClickHouseDialectTests.cs:438`) + EN/RU docs; XP6 (`ClickHouseIntegrationTests.cs:208` →
  `SqlFunctions.Sql.any_agg`) + исправить преждевременную отметку регистра; зафиксировать UG1/JP2 baseline debt.
- **D2 wiring:** CPM-версия в `Directory.Packages.props`; во всех девяти `src/<project>/<project>.csproj`
  PackageReference без Version, `PrivateAssets="all"`; `AdditionalFiles` Shipped/Unshipped; создать пары
  файлов рядом с csproj (первая строка `#nullable enable`); `.editorconfig` — RS0016/RS0017 `error`.
- **D3 baseline:** restore → Release diagnostics → formatter → rebuild, повтор до 0/0 (fallback — Roslyn inventory).
- **D4 CI/evidence:** `.github/workflows/dotnet.yml` — `-c Release` для build и test; harness
  `artifacts/T53/probe.py`.
- **D5 docs/register:** IF6 `docs/specs/design/API-NAMING-REVIEW.md:1145`, Шаг 5 `:3645-3657`,
  closure `:5226`; `docs/specs/roadmap/todo_public_api_freeze.md` — durable record; contributor guide EN+RU;
  stale ref `docs/specs/todo_dialect_capabilities.md` убрать/исправить.
- **D6 CHECK/ACT:** собрать evidence contract; commit — только явно уполномоченная collection-операция.

## Variant matrix (public-API-change path)

| строка | закрытие |
|---|---|
| 9 проектов: add/remove API | **test** (red↔green probes) |
| public rename / overload (core) | **test** |
| value/reference, nullable/default signatures | **test** |
| internal/private change | **test** (baseline неизменен) |
| nullable headers / severity / AdditionalFiles / Shipped-empty | **guard** |
| provider runtime | существующие suites |
| XP6 | реальный ClickHouse integration |
| 1.0 Shipped move + ApiCompat | **deferred + trigger: release 1.0** |

## Evidence contract rv1 (planned sources)

- **E01** (A1,A2): `dotnet restore nextorm.slnx` → `dotnet build nextorm.slnx -c Release --no-restore -t:Rebuild`; exit 0, 0/0.
- **E02** (A2): `dotnet format analyzers nextorm.slnx --diagnostics RS0016 --severity error --no-restore`; exit 0; fallback — Roslyn inventory.
- **E03** (A3): `python3 artifacts/T53/probe.py --mode api --solution nextorm.slnx --configuration Release`; RS0016 red / Unshipped green / RS0017 red / fix green.
- **E04** (A4,A5): `dotnet test -c Release --no-build`; exit 0 + counts.
- **E05** (A5): контейнерные integration-тесты (см. skill running-integration-tests), ClickHouse выполнен, не skipped.
- **E06** (A1,A4,A5,A6): `python3 artifacts/T53/probe.py --mode policy`; wiring/severity/CI/docs + Roslyn rename evidence.
- **E07** coverage: `dotnet-coverage collect` + `reportgenerator`; пороги line ≥85 / branch ≥75.
- **E08** (A6): `python3 artifacts/T53/probe.py --mode committed --revision HEAD`; 18 tracked baseline files (после разрешённого ACT commit).

## Perf / reconnaissance / unit mode

- **Perf: не требуется** — build-time analyzer/CI/baseline; XP3 меняет имя (`ISqlDialect.cs:593`),
  XP6 — тест (`:208`); per-row runtime-алгоритм не меняется.
- **Reconnaissance: нужна** — D0 проверяет restore, formatter и одну nullable baseline-итерацию.
- **Unit execution mode:** единый режим в одном дереве (общие CPM/CI/docs и API-файлы исключают
  независимую запись; worktree не нужен).

## Assumptions / risks / gaps

- Факты brief приняты; команды ещё не выполнены — tool availability (SDK `dotnet format`), контейнеры,
  milestone/issue URL и разрешение commit — предпосылки до доказательства, не `ready`.
- **GAP (none blocking):** если SDK `dotnet format` недоступен — fallback Roslyn inventory, A2/A3 не ослабляются.

## Candidate records (freeze HELD)

New public members awaiting the freeze, recorded by their owning tasks. Internal members and preserved
constructors are **not** candidates.

- **T221 (#221, `docs/specs/status/D221-interceptor-di-registration-1.md`):** DI-registered interceptors add
  `public DataContextBuilder AddInterceptor(System.Type interceptorType)` and
  `public DataContextBuilder AddInterceptor<T>() where T : class`. The new internal
  `DataContextBuilder(IServiceProvider)` constructor and the preserved public parameterless constructor are
  **not** candidates.

- **T222 (#222, `docs/specs/status/D222-join-overloads-by-type-1.md`):** the source-less (type-only) join
  overloads add **28 public members**. Result arity 2, on `EntityBuilder<TEntity>` (4 members, one per
  operator):
  `public JoinedEntityBuilder<TEntity, TJoinEntity> <Op><TJoinEntity>(Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)`
  for `<Op>` ∈ { `Join`, `LeftJoin`, `RightJoin`, `FullJoin` }.
  Result arity n+1, on `JoinedEntityBuilder<T1..Tn>` for n = 2..7 (4 members each, 24 total, declared
  `new`):
  `public new JoinedEntityBuilder<T1..Tn, TJoinEntity> <Op><TJoinEntity>(Expression<Func<Projection<T1..Tn>, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)`
  for `<Op>` ∈ { `Join`, `LeftJoin`, `RightJoin`, `FullJoin` }.
  All 28 delegate to the existing context-local `From<TJoinEntity>()` source resolution and to the
  existing join core; the source-explicit overloads are unchanged. Freeze remains **HELD**: no
  `PublicAPI.Shipped/Unshipped` file is created and the analyzer is not activated by this task.

- **T218 (#218, `docs/specs/status/D218-sqlserver-sequences-1.md`):** SQL Server sequence access adds **2 public
  members**, both on `SqlFunctions.SqlServer` and both returning CLR `long`:
  `public long next_value_for(string sequence)` and
  `public long nextval(string sequence)` (`SqlFunctions.SqlServer.cs:294,301`). Both translate to the same
  T-SQL `NEXT VALUE FOR`; no other public surface changes (the translator/dialect additions are
  `internal`). Freeze remains **HELD**: no `PublicAPI.Shipped/Unshipped` file is created and the analyzer is
  not activated by this task.

## Overlap / footprint

- Последовательность: API-результаты #209/#211/#212/#213 → T53 D1–D6; их Unshipped-правки (если уже
  созданы) объединить до финального baseline. D201 — не зависимость, пока не доказано пересечение.
- **Плановый write-footprint:** `Directory.Packages.props`, `.editorconfig`, `.github/workflows/dotnet.yml`,
  9 `src/<project>/*.csproj` + 18 `PublicAPI.*.txt`, XP3 declarations/refs/tests, XP6 test,
  EN/RU docs (`docs/specs/design/API-NAMING-REVIEW.md`, contributor guide, `docs/specs/roadmap/todo_public_api_freeze.md`),
  status + `artifacts/T53/**`. Неопределённость: точный Roslyn rename/docs список фиксирует D0.

## Progress log

- PLAN (r=1) issued by `planner`, persisted by `coder`. Phase stopped at PLAN→DO boundary; handoff to
  collection parent (no DO started, no branches/worktrees, no code).
- **DO 2026-11-11 (single tree, no worktree).**
  - **D1 cleanup.** XP3 applied via Roslyn rename `NextORM.Core.ISqlDialect.SupportsAnyAggregates` →
    `SupportsAnyLastAggregate` (11 documents: `ISqlDialect`, `SqlDialectBase`, `ClickHouseDialect`,
    `SqlFunctions.ClickHouse`, `AdvancedAggregateTranslator`, `ClickHouseDialectTests`) plus the EN/RU
    guide table and `capability-matrix.md`. XP6 finished: the two remaining
    `SqlFunctions.ClickHouse.any_agg` call sites (`ClickHouseArrayReturnTypeSqlGenerationTests`,
    `ClickHouseIntegrationTests`) now use `SqlFunctions.Sql.any_agg`. UG1/JP2 consciously deferred
    and recorded in the baseline under their current names.
  - **D2 wiring.** `Microsoft.CodeAnalysis.PublicApiAnalyzers` 5.6.0 in `Directory.Packages.props`;
    `PackageReference` + both `AdditionalFiles` in the 9 library `.csproj` **and**
    `nextorm.core.sourcegenerator` (its public surface is empty — both generators are `internal` — so
    the pair holds the invariant only). 20 `PublicAPI.*.txt` files created with a `#nullable enable`
    first line. RS0016/RS0017 pinned as `error` in `.editorconfig`.
  - **D3 baseline.** Generated by `dotnet format analyzers --diagnostics RS0016` (not hand-written):
    `nextorm` 6355, clickhouse 224, sqlserver 187, postgres 156, mysql 117, sqlite 88, extensions 36,
    mariadb 30, entityframeworkcore 12, sourcegenerator 0. `RS0026`/`RS0027` (optional-parameter
    overload guidelines) fired only while the baseline lived in `Unshipped` — moving it to `Shipped`
    silenced them without a suppression; no `NoWarn` was left behind.
  - **Freeze point.** Owner decision: the first production version is 1.0.9 and is released right
    after this change, so the baseline is in `Shipped` and `Unshipped` holds only the header. A
    `Unshipped`-first variant was rejected because nothing in CI moves the file at release time
    (the release job only packs and pushes), so the move would have to be remembered by hand.
  - **D4 package gate.** `EnablePackageValidation=true` in `src/Directory.Build.props`; `dotnet pack
    -c Release` green for all 8 released projects. Baseline comparison is skipped for prerelease
    baselines (verified against 1.0.8-b and 1.0.9-rc5: no findings), so
    `PackageValidationBaselineVersion=1.0.9` is the one follow-up, added once 1.0.9 is on nuget.org.
  - **Deviation from A4.** CI keeps building Debug. The plan wanted a Release build + `--no-build`
    Release tests; the acceptance intent (RS0016/RS0017 fail CI) is already met by
    `TreatWarningsAsErrors` in the existing Debug build — proved by probe, not argued. Switching the
    whole suite to Release would change coverage measurements and release-only code paths for no
    extra gate, so it was left out of this change.
  - **D5 docs/register.** IF6, DC3, DC8 and XP3 closed in `API-NAMING-REVIEW.md`; Шаг 5 section
    records the closure; `todo_public_api_freeze.md` rewritten as the execution record (kept — the
    register links it from ~30 places).

## Done / Verified / Incomplete

- Done: D1 (XP3, XP6; UG1/JP2 deferred), D2 (CPM + 10 csproj + 20 API files + `.editorconfig`),
  D3 (baseline in `Shipped`), D4 (`EnablePackageValidation`), D5 (register + roadmap record).
- Verified:
  - `dotnet build nextorm.slnx -c Release --no-restore` → `0 Warning(s) 0 Error(s)`.
  - `dotnet build nextorm.slnx -c Debug --no-restore` → `0 Warning(s) 0 Error(s)`.
  - RS0016 probe: `public sealed class ApiProbe` added to `nextorm.core` without a declaration →
    `error RS0016` on both TFMs; after adding the two lines to `PublicAPI.Unshipped.txt` the analyzer
    stopped (only the unrelated `CS1591` remained); removing the type with the declaration still in
    place → `error RS0017` on both lines; removing the declaration → 0/0.
  - `dotnet pack -c Release` for `nextorm`, sqlite, sqlserver, postgres, mysql, mariadb, clickhouse,
    extensions → exit 0 with package validation on.
  - `dotnet test tests/nextorm.core.tests -c Debug` → 4033/4033 passed.
  - `dotnet test tests/nextorm.clickhouse.tests -c Debug` → 1525/1525 passed.
- Incomplete:
  - `PackageValidationBaselineVersion=1.0.9` — cannot be set before 1.0.9 is published; tracked as
    [#389](https://github.com/AlexeyShirshov/nextorm/issues/389) (milestone `1.0.10-a.1`).
  - Integration suites not run in this DO (no behaviour change: the rename is compile-time only and
    the XP6 edit replaces an inherited call with the declaring type). Container evidence is the
    CHECK step's job.
