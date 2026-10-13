# D221 (#221) docs / API review — raw material (E221-08), rv=1

Raw review material only; the verdict belongs to the `check` role.

## DocFX (F)

- `dotnet docfx docs/docfx.json` → **exit 0** (`artifacts/pdca/D221/rv1/docfx.log`).
- **2 warnings**, both pre-existing and not attributable to T221:
  `Duplicate source file 'src/nextorm.core.sourcegenerator/AnalyzerReleases.Shipped.md'` and
  `'.../AnalyzerReleases.Unshipped.md'` (source-generator project, untouched).
- 0 errors; the conceptual build includes the edited EN/RU pages and xrefs resolve.

## Public API delta

- New public members (two overloads in `src/nextorm.core/DI/DataContextBuilder.cs`):
  - `public DataContextBuilder AddInterceptor(Type interceptorType)`
  - `public DataContextBuilder AddInterceptor<T>() where T : class`
- The new `internal DataContextBuilder(IServiceProvider)` is **not** public surface.
- The public parameterless `DataContextBuilder()` is preserved (explicitly declared so adding the internal
  ctor does not remove it) — not a new member.
- No other production public/signature change; `build` is warning-free (0/0) with
  `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`, so the new public members are
  XML-documented (CS1591 gated).

## Docs EN/RU

- `docs/infrastructure/02-interceptors.md` (+ `docs/ru/infrastructure/02-interceptors.md`): new
  "Registering an interceptor from dependency injection" / "Регистрация интерцептора через dependency
  injection" section — resolution from the builder's scope once per creation, lifetimes, dual-interface
  dispatch, exact-key deduplication, and the standalone/missing-registration error boundary.
- `docs/getting-started/04-dependency-injection.md` (+ RU): new "DI-registered interceptors" /
  "Интерцепторы через DI" section.
- No new pages were added, so no `toc.yml`/index change was needed.
- **No public doc links to `docs/specs/**`** (specs are internal); the new prose is self-contained.

## T53 freeze candidates (freeze HELD — not activated)

- `docs/specs/design/API-NAMING-REVIEW.md` (interceptor Phase-1 audit section): new entry recording both
  overloads as T53 candidates + issue #221 (verified URL).
- `docs/specs/design/code-smells-review.md` (interceptor audit section): addendum for the D221 unified-list
  resolution; no new code smell.
- `docs/specs/status/D53-public-api-freeze-1.md`: new "Candidate records (freeze HELD)" section with the two
  exact signatures; internal ctor explicitly excluded.
- `docs/specs/status/collection-1.0.9-todo.md`: T221 added to the held-freeze candidate list.
- Freeze is **HELD**: no `PublicAPI.*.txt`, no analyzer activation, no API-approval test.

## Issue

- #221 → `https://github.com/AlexeyShirshov/nextorm/issues/221`, verified via `gh issue view 221`
  (`{"number":221,"title":"Интерцепторы: регистрация через DI (generic/factory-оверлоады)","url":"https://github.com/AlexeyShirshov/nextorm/issues/221"}`).

## Evidence

- `artifacts/pdca/D221/rv1/docfx.log`
- `artifacts/pdca/D221/rv1/core-u.log`, `core-un.log`, `sqlite-q.log`, `sqlite-qn.log`, `integration.log`

## Corrective n=2/3 addendum

- B2: the `AddInterceptor(Type)` XML remark now states the exact-key dedup semantics ("entries share one
  key; first registration wins"), matching `AddEntry`/`MixedDuplicate…IsIgnored`.
- B7: the `AddInterceptor<T>()` remark notes the runtime check beyond the compile-time `class` constraint.
- B11: EN/RU `02-interceptors.md` now state that the EF Core bridge builder is standalone, so type-key
  registration there is rejected at creation (instance registration required). No public API change.
- DocFX unchanged (exit 0; 2 pre-existing sourcegenerator warnings).
