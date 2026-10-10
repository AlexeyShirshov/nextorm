# D53 — IF6: public API freeze & tracking (#53)

- issue: #53 — https://github.com/AlexeyShirshov/nextorm/issues/53
- selected_variant: `pdca-dotnet`
- cycle: N=1
- plan_revision: r=1
- iteration: n=1
- phase: **PLAN complete → stopped at PLAN→DO boundary** (collection P-phase handoff)
- status file: docs/specs/status/D53-public-api-freeze-1.md
- evidence contract revision: rv1

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

## Done / Verified / Incomplete

- Done: own PLAN r1 persisted (plan_state=ready).
- Verified: —
- Incomplete: —
