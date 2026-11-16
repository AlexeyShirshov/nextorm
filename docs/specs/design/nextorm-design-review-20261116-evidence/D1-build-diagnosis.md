# D1 solution-build diagnosis

## Verdict

The solution-build failure is reproducible from the current, unchanged project files; it is not
explained by a stale generated-source artifact alone. The compile-reference evidence points to an
assembly-reference collision: the benchmark compile resolves `nextorm.core.dll` from the NuGet
`nextorm` 1.0.9 package, while the source generator emits code against APIs available in the
workspace's current `src/nextorm.core` source.

This is outside D1's authorized edit scope. No benchmark, example, package, or source-generator
configuration was changed; no clean or artifact deletion was run.

## Evidence

- Roslyn resolves `NextORM.Core.RowMaterializerColumn` to the workspace struct at
  `src/nextorm.core/DataContext/Mapping/RowMaterializerColumn.cs:13` and
  `NextORM.Core.IRowMaterializerProvider` to the interface at
  `src/nextorm.core/DataContext/Mapping/IRowMaterializerProvider.cs:17`.
- Roslyn references for these symbols include generated materializer source under
  `benchmarks/nextorm.benchmark/obj/linux/Debug/net11.0/.../NextORM.RowMaterializers.g.cs`.
  The failed build reports the first missing-type diagnostics at generated lines 6 and 135.
  The generated file was not present as a physical file when read after the build; the complete
  diagnostics are preserved in the build logs.
- `benchmarks/nextorm.benchmark/nextorm.benchmark.csproj:21-35` directly references both
  `src/nextorm.core` and `src/nextorm.core.sourcegenerator`, as well as the PostgreSQL example.
  Its `DropTransitiveNextormGenerator` target at `:54-64` removes the NuGet analyzer only.
- `examples/nextorm.examples.postgres.aviasales/nextorm.examples.postgres.aviasales.csproj:14-15`
  references NuGet packages `nextorm` and `nextorm.postgres`. Both benchmark and example
  `project.assets.json` files resolve `nextorm/1.0.9`; the benchmark target lists
  `lib/net11.0/nextorm.core.dll` as its package compile asset (`benchmarks/.../project.assets.json:1340-1355`).
- `dotnet msbuild benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -property:Configuration=Debug -target:ResolveReferences -getItem:ReferencePath`
  exited 0. Its resolved `ReferencePath` contains
  `/home/alex/.nuget/packages/nextorm/1.0.9/lib/net11.0/nextorm.core.dll`
  (`D1-build-diagnosis-reference-items.log:2473-2487`); it contains no `src/nextorm.core/bin`
  or `src/nextorm.core/obj` assembly path. The project-reference outputs for other projects are
  present in the same item list.
- Scoped `git status` and `git diff` for the benchmark project, PostgreSQL example project,
  source-generator source, and the two mapping API files returned no changes. The observed
  reference collision is therefore in the current tracked configuration, not an uncommitted edit
  to those files.

## Build reproduction

- `dotnet build nextorm.slnx -c Debug` — exit 1, 0 warnings, 202 errors; all reported compile
  errors are missing `IRowMaterializerProvider` / `RowMaterializerColumn` references in generated
  materializer source for `nextorm.benchmark`. Full log: `D1-solution-build.log`.
- `dotnet build benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Debug --no-restore` —
  exit 1, 0 warnings, 202 errors with the same generated-source diagnostics. The command rebuilt
  the current core, source generator, and provider projects before failing at benchmark compile,
  ruling out a solution-only stale output explanation. Full log:
  `D1-build-diagnosis-benchmark-build.log`.
- The reference-resolution command above exited 0. Full output:
  `D1-build-diagnosis-reference-items.log`.

## Limitation and next verification

The evidence establishes a deterministic dependency/reference-resolution problem outside D1; it
does not establish which authorized project-level correction should be chosen. Do not clean or
delete artifacts as a remedy: the targeted no-restore build reproduced the mismatch from current
references. The benchmark dependency graph must be separately reviewed so the generated source
and compiler use the same workspace `nextorm.core` assembly. After an authorized graph correction,
the minimal checks are:

1. `dotnet build benchmarks/nextorm.benchmark/nextorm.benchmark.csproj -c Debug --no-restore`
2. `dotnet build nextorm.slnx -c Debug`
