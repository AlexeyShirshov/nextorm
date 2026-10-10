# AGENTS

## Build & toolchain
- .NET 10 SDK; production and test projects target `net10.0` (the source generator targets `netstandard2.0`). `global.json` pins only the test runner (`Microsoft.Testing.Platform`), not an SDK version.
- Central Package Management: reference packages as `<PackageReference Include="X" />` (no `Version`); add/update the version in `Directory.Packages.props`.
- `TreatWarningsAsErrors=true` for every project and configuration — nullable and analyzer warnings fail the build. Fix them or suppress explicitly via `NoWarn`.
- `Directory.Build.props` redirects build output into per-host-OS dirs: `bin/<linux|windows>/`, `obj/<linux|windows>/`, so WSL and Windows builds coexist. Look there for artifacts; there is no shared `obj/`.

## C# semantic analysis (Roslyn)
- Symbols are Roslyn-only: for type/member definitions, references, call sites, implementations, overloads, renaming and impact analysis use the `roslyn` tool — never `rg`/`grep`/`sed`, not even as a first pass. Text search is fine only for docs, comments and string literals; it cannot resolve overloads, partial types or usings.
- The global rule in `~/.config/opencode/AGENTS.md` ("C# symbols: Roslyn only") applies here too and takes precedence over any reflexive grep.
- Custom tool `roslyn` is provided globally by opencode (`~/.config/opencode/tools/roslyn.ts`) and wraps the global `roslynq` dotnet tool, which loads this solution through `MSBuildWorkspace`. Pass `solution` only if the auto-detected `.sln` is wrong.
- Actions: `structure`, `types`, `symbols`, `members`, `refs`, `callers`, `implementations`, `rename`; pass symbols by full name (e.g. `NextORM.Core.SqlBuilder` or `NextORM.Core.SqlBuilder.MakeSelect`).
- Bulk renames: use `rename <symbol> <newName>` (dry-run) and add `apply=true` once the diff looks right — do not edit files one by one.
- Diagnostics after edits: `dotnet build` (see Build & toolchain). `MSBuildWorkspace` cold start is slow on this solution, so prefer one invocation per analysis over per-file calls.

### Searching text (ignore-aware only)
- Plain text (docs, comments, string literals, SQL/CLR names in prose) is searched with ignore-aware tools only: the built-in `grep` tool (ripgrep) or `rg`, or — cheapest — `git grep` (tracked files only). Scope to sources: `rg -n --glob '*.cs' '<literal>' src tests` or `git grep -n '<literal>' -- 'src/**' 'tests/**'`.
- **Never run shell `grep -r`, `grep -rn`, `grep -R` or `find` over the tree.** They ignore `.gitignore` and walk `bin/`, `obj/` and `TestResults/` — on a built worktree ~1.3 GB of artifacts under `src`/`tests` — so one call reads ~1 GB from disk instead of a few MB. Search hygiene, not style.
- This does not replace `roslyn`: any C# symbol/overload/partial/using question goes to the `roslyn` tool (above), never to text search.

## Todos
- Keep the todo list current: call `todowrite` after **each** completed step (and again when starting the next one), not just at the beginning and the end.
- The right-hand sidebar refreshes only when `todowrite` runs, so batching updates leaves it stale for many turns and the session loses its progress indicator.

## Layout
- `src/nextorm.core` is the engine: query builder/plan cache, expression visitors (`Visitors/`), dialects, in-memory context. Providers reference it: `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`, `nextorm.clickhouse`; `nextorm.mariadb` builds on `nextorm.mysql`.
- Core file map (common mis-guesses, `file:line` for a start): SQL/AST expressions are in `Expressions/` (`Expressions/SelectExpression.cs`), not `Query/`; the SQL builder, planner and row mapping live under `DataContext/` (`DataContext/SqlBuilder.cs`, `DataContext/QueryPlanner.cs`, `DataContext/RowMapperFactory.cs`, `DataContext/RowMaterializerBuilder.cs`; note `DataContext/DataContext.cs` is a folder member, not a root `DataContext.cs`); CTE hoisting is `Builders/CteHoister.cs`; JSON shape/streaming is `Query/Json/` (`Query/Json/JsonShapePlan.cs`); CSV options are `Query/CsvStreamOptions.cs`; fluent builders are `Builders/`.
- `src/nextorm.core.sourcegenerator` is an empty `IIncrementalGenerator` stub — in the solution but referenced by no project, so it generates nothing today.
- `tests/nextorm.<provider>.tests` are dialect/SQL-generation tests using placeholder connection strings; they need no database. Only `tests/nextorm.integration.tests` talks to real databases, through `ProviderTestSuite` + `CommonTestSuite.*.cs` (a test added there runs against every provider; provider-only behavior belongs in `*SpecificTests.cs`).

## Shared query commands & plan cache
- `.Any()`/`.Count()`/`.All()` and similar reuse **one** `QueryCommand` per `DataContext` (`EntityBuilderExtensions.GetAnyCommand` → `QueryCache.AnyCommand`; `ReplaceCommand` only swaps the referenced subquery, it does not reset command state).
- Never mutate that command on behalf of a single call — in particular `queryCommand.Cache = false`, which is backed by the sticky `_dontCache` field. The flag persists across calls, so it leaks to every later query on the context and silently disables the plan cache for the whole context (green tests, production regression).
- To prepare without caching, pass `storeInCache: false` to `_planner.GetPreparedQueryCommand(...)` — it is local to the call and does not touch the command. The lazy temp-table path (`DataContext.GetPreparedTemporaryTableCommand`, `DataContext.cs`) is the reference example.

## TVP metadata & decimal precision
- The TVP auto-resolution path keeps **its own** metadata cache inside `DataContextCache` (`TvpMetadataCacheTests`): it must never seed the process-wide configured-metadata cache, always defers to a configured `From<T>(cfg)` mapping, and is cleared by `DataContextCache.Clear()`. Seeding the configured cache silently ignores a later fluent registration.
- `[DecimalPrecision(p, s)]` / fluent `DecimalPrecision(p, s)` are validated against the **bound provider type** (`converter?.ProviderType ?? propertyType`) in `DecimalPrecisionRules.Validate`; a non-decimal bound type or a pair declaring only one of precision/scale throws. Provider defaults stay (SQL Server `decimal(38,18)`, ClickHouse `Decimal(38,10)`).

## Git
- Never run `git push`; the user pushes manually.
- Do not create commits unless explicitly asked — **this includes work in separate git worktrees**.
- Never merge branches and never create merge commits unless explicitly asked. `git merge` inherently requires commits; do not use it as the default integration mechanism.
- **Исключение `pdca-collection`.** Для циклов, запущенных скиллом `pdca-collection` в режиме явно разрешённого автокоммита, разрешаются: (a) коммиты в ветках `collection/**` внутри worktree цикла, (b) `git merge --no-ff` ветки группы в текущий бранч. Вне такого цикла правила «never merge» (выше) и «no commits unless explicitly asked» (выше) сохраняются.
- Integrate work done in isolated worktrees with **patches, not commits/merges**: in each worktree produce a diff (`git diff <base> > /tmp/<name>.patch`, or `git diff` for uncommitted changes), then apply it into the target tree (`git apply` / `patch`), leaving the result uncommitted for the user to review.

## Async naming
- Use the `Async` suffix only when a method has a synchronous twin. If an async method is the only one (no sync counterpart exists), do not add the `Async` suffix.

## Line endings
- CRLF throughout (`core.autocrlf=true`). Preserve CRLF when writing/editing; never leave LF-only or mixed.
- Normalize after edits: `perl -pi -e 's/\r?\n/\r\n/g' <file>`.

## Tests
- `dotnet test tests/<project> -c Debug` works (via the Microsoft.Testing.Platform runner in `global.json`). Focus one test with `--filter "FullyQualifiedName~InMemoryTests"`.
- xunit v3 native filters also work: `dotnet run --project tests/<project> -c Debug -- -class nextorm.core.tests.InMemoryTests` (`-method`, `-namespace`, `-trait`).
- `nextorm.integration.tests` starts PostgreSQL, SQL Server, MySQL and ClickHouse with Testcontainers. Point them at the running Podman machine:
  ```bash
  DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
    dotnet run --project tests/nextorm.integration.tests -c Debug -- -noColor
  ```
  Pre-pulled images: `postgres:17-alpine`, `mcr.microsoft.com/mssql/server:2025-latest`, `mysql:8.4`, `testcontainers/ryuk`; `clickhouse/clickhouse-server:25.8-alpine` is pulled on the first ClickHouse run (`TESTCONTAINERS_RYUK_DISABLED` is not needed). Without `DOCKER_HOST` only the SQLite integration tests run. See `.opencode/skills/running-integration-tests/SKILL.md`.
- External databases: `NEXTORM_POSTGRES_CONNECTION`, `NEXTORM_SQLSERVER_CONNECTION`, `NEXTORM_MYSQL_CONNECTION`, `NEXTORM_CLICKHOUSE_CONNECTION` (see `tests/nextorm.integration.tests/Providers/*Container.cs`).
- **Container-backed integration tests are not optional evidence.** Load `.opencode/skills/running-integration-tests/SKILL.md` before running them. If the Podman socket is missing, do **not** stop at "cannot run": start the machine (`"/mnt/c/Program Files/RedHat/Podman/podman.exe" machine start`), wait, re-check the socket and rerun with `DOCKER_HOST` (see the skill's Troubleshooting). A run where PostgreSQL/SQL Server/MySQL/ClickHouse report `skipped` (i.e. no `DOCKER_HOST`) is **not** a passing run: either set `DOCKER_HOST` and execute them, or state explicitly which providers were not run. Never present a suite that skips provider tests as green.

## Coverage
- `coverage.settings.xml` includes only `nextorm.{core,sqlite,postgres,sqlserver}`.
- CI thresholds `MIN_LINE_COVERAGE=85` and `MIN_BRANCH_COVERAGE=75` (both, `dotnet-coverage`/`reportgenerator`) hard-fail only on `main`; other branches warn. Reproduce with the `dotnet-coverage collect` → `reportgenerator` steps in `.github/workflows/dotnet.yml`.

## Docs
- DocFX is a local tool: `dotnet docfx docs/docfx.json`. `docs/api/` and `docs/_site/` are generated and gitignored; article pages are hand-written.
- Renaming a public type or method requires updating both `docs/**` and `docs/ru/**` (prose, samples, source paths, curated API reference) in the same change. Search both trees for the old name first (`rg -n "<old-name>" docs docs/ru`, or `git grep` — never `grep -r`).
- Moving a guide page into a separate section (or deleting one) leaves a gap in the numbered `docs/guide/NN-*.md` series: renumber the remaining pages consecutively, and update every link and `toc.yml` entry in both `docs/**` and `docs/ru/**` in the same change — never leave a hole (e.g. `30 → 32`).
- Public docs (`docs/**`, `docs/ru/**`, root `readme.md`) must **not link to `docs/specs/**`**: specs are internal, excluded from the DocFX build and never published. No hyperlinks, no relative `.md` links, no GitHub `blob` links to specs from article pages or the readme — keep the reasoning inline or point to a public page.

## Benchmarks
- `benchmarks/nextorm.benchmark` is a BenchmarkDotNet console app using `BenchmarkSwitcher`: `dotnet run --project benchmarks/nextorm.benchmark -c Release -- --filter *SqliteBenchmarkWhere*`.

## Brainstorming & GitHub tracking

- In this repo, load `.opencode/skills/nextorm-brainstorming/SKILL.md` together with the global `brainstorming` skill before starting the brainstorm.
- For any agreed feature/refactor/fix, create or update its GitHub issue and milestone, and record `#N` + verified URL in the spec/handoff, before asking for written-spec review or handing off to planning/implementation.
- The overlay only adds tracking; brainstorming's approval gates stay unchanged, and no git commit/push/merge is granted.
- PDCA work keeps its existing spine — use `nextorm-pdca`; the overlay does not turn PDCA into brainstorming.
