# nextorm.examples.oc-report — the NativeAOT reference app

A real reporting utility over an OpenCode database (`opencode.db`), used as the **reference case for
NativeAOT**: it reads a 13 GB SQLite file read-only and prints a session/model/error report. There are
no secrets in the app or the schema: it opens the database with `Mode=ReadOnly`, takes the path from
`--db` / `$OPENCODE_DB` / `~/.local/share/opencode/opencode.db`, and writes nothing.

The app is intentionally ordinary:

- typed nextorm queries over interfaces (`ISessionRow`, `IMessageRow`), no raw SQL;
- SQL-side `GROUP BY` with aggregates, and `json_extract` for the `model` JSON column;
- a conditional filter (`--agent` / `--all`), i.e. the query is composed at runtime.

## Running

```bash
# real database (read-only)
dotnet run --project examples/nextorm.examples.oc-report -c Release -- 3
dotnet run --project examples/nextorm.examples.oc-report -c Release -- 7 --all
dotnet run --project examples/nextorm.examples.oc-report -c Release -- 3 --agent coder --db /path/opencode.db

# or a synthetic database with the same two tables
sqlite3 /tmp/oc-report.db < examples/nextorm.examples.oc-report/seed.sql
dotnet run --project examples/nextorm.examples.oc-report -c Release -- 3 --db /tmp/oc-report.db
```

| argument | meaning |
| --- | --- |
| `<days>` | window in days (default 3) |
| `--agent <name>` | agent filter (default `pdca-executor`) |
| `--all` | no agent filter |
| `--db <path>` | database path; otherwise `$OPENCODE_DB` or `~/.local/share/opencode/opencode.db` |

`seed.sql` generates a deterministic 20 000-session / 200 000-message database. Its size is parameterised,
so a larger file can be generated for timing:

```bash
sqlite3 /tmp/oc-report.db \
  ".parameter set @sessions 200000" ".parameter set @messages_per_session 20" \
  < examples/nextorm.examples.oc-report/seed.sql
```

## NativeAOT

The only AOT-specific input is the switch itself:

```bash
dotnet publish examples/nextorm.examples.oc-report -c Release -r linux-x64
./examples/nextorm.examples.oc-report/bin/linux/Release/net11.0/linux-x64/publish/nextorm.examples.oc-report 3
```

`PublishAot=true` is the only AOT-specific input. It lives in the csproj so a plain `dotnet publish`
of the project is the whole command; a consumer app sets the same property in its own csproj or on the
command line. The csproj also carries `IlcTreatWarningsAsErrors=false` and
`WarningsNotAsErrors=IL2*;IL3*` for **repository policy only** (`TreatWarningsAsErrors=true` is global
here) and pins `Microsoft.DotNet.ILCompiler` 11.0.0, matching the repository's NativeAOT runner — the
pin exists because a historical SDK default carried a NuGet audit advisory (NU1903) that the
warnings-as-errors gate turned into an error; nextorm itself needs none of that.

The project consumes the published `nextorm` / `nextorm.sqlite` `1.0.9` packages (version pinned by
`NextOrmVersion` in [`Directory.Packages.props`](../Directory.Packages.props)), like the postgres example.

## Reference numbers

Measured 2026-11-08 on WSL2 (Ryzen 7 5800HS), .NET SDK 10.0.401, `linux-x64`, warm page cache, median
of three runs, 3-day window over the 13 GB `opencode.db`. The same report was also implemented with
EF Core 10.0.14 and Dapper 2.1.89 + Dapper.AOT 1.1.0; all three produced byte-identical output.

| Implementation | JIT | AOT | Native binary | Peak RSS | AOT changes in the consumer |
| --- | --- | --- | --- | --- | --- |
| nextorm (this project) | 0.42 s | **0.12 s** | 8.2 MB | 30 MB | `PublishAot=true` only |
| EF Core 10.0.14 | 1.03 s | 0.23 s | 17.3 MB | 42 MB | compiled model + `Microsoft.EntityFrameworkCore.Tasks` (query precompilation) + `InterceptorsNamespaces`; non-sealed entities outside a CLR namespace to avoid two EF 10.0.14 codegen bugs |
| Dapper 2.1.89 + Dapper.AOT 1.1.0 | 0.26 s | **0.12 s** | 3.5 MB | 19 MB | `Dapper.AOT` + `[module: DapperAot]` + `InterceptorsNamespaces`; statically known SQL only |

The full contract, the residual diagnostics (230 unique / 236 raw for the AOT runner) and the provider
coverage are on the [NativeAOT guide](../../docs/infrastructure/05-nativeaot.md). The comparison lives
under [Comparisons → Benchmarks](../../docs/comparisons/benchmarks.md).
