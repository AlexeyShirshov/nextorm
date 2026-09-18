# AGENTS

## Git
- **Never run `git push`.** The user pushes manually.
- Do not create commits unless the user explicitly asks.

## Line endings
- The repo uses **CRLF**; `core.autocrlf=true` is set in `.git/config` (shared with Windows).
- Always preserve CRLF when creating or editing files — never leave LF-only or mixed endings.
- To normalize a file after editing: `perl -pi -e 's/\r?\n/\r\n/g' <file>`.

## Documentation
- **Renaming a public type or method requires updating the docs in the same change** — both the
  English pages and the Russian mirror (`docs/**` and `docs/ru/**`), including code samples, prose,
  source paths and the curated API reference. Grep both trees for the old name; an un-updated rename
  is an incomplete change.

## Tests
- Test projects use `xunit.v3` + Microsoft.Testing.Platform. `dotnet test` discovers 0 tests here;
  run a project with `dotnet run --project test/<project> -c Debug -- [filters]`, e.g.
  `... -- -class nextorm.core.tests.InMemoryTests` or `... -- -noColor`.
- **Integration tests** (`test/nextorm.integration.tests`) start PostgreSQL, SQL Server and MySQL with
  Testcontainers. A Podman machine is already running; point Testcontainers at its socket:
  ```bash
  DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock \
    dotnet run --project test/nextorm.integration.tests -c Debug -- -noColor
  ```
  Images `postgres:17-alpine`, `mcr.microsoft.com/mssql/server:2022-latest`, `mysql:8.4` and
  `testcontainers/ryuk` are pre-pulled. Without `DOCKER_HOST` only the SQLite suite can run.
- Postgres/SqlServer/MySQL can also be pointed at an external server via
  `NEXTORM_POSTGRES_CONNECTION` / `NEXTORM_SQLSERVER_CONNECTION` / `NEXTORM_MYSQL_CONNECTION` (see
  `Providers/*Container.cs`).

## Coverage
- `coverage.settings.xml` limits the report to `nextorm.{core,sqlite,postgres,sqlserver}`.
- CI enforces `MIN_LINE_COVERAGE=75` (`.github/workflows/dotnet.yml`). To reproduce locally, collect
  each test project with `dotnet tool run dotnet-coverage collect` (with the `DOCKER_HOST` above),
  merge the cobertura files, then run `reportgenerator` (see the workflow).

