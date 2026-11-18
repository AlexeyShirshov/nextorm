# Public documentation readability — initial 12-page batch

## Scope

This report covers the initial 12-page learning-path batch only:

* English and Russian landing pages: `docs/index.md` and `docs/ru/index.md`.
* English and Russian installation pages.
* English and Russian quickstart pages.
* English and Russian projection, filtering, and join guides.

The batch helps .NET developers move from package installation to a first query,
then learn to filter rows, select fields, and join tables. It is not a review of
all public documentation and does not mark the wider website readability task
complete. Other public pages are handled in separate batches.

## Changes

The landing pages now introduce the intended reading path and link to each
step. Their full catalogs put the Guide section before Engine and Relationships
without changing existing heading text. The installation pages identify
SQLite for the quickstart, explain that applications can install the provider
packages they need, and move optional packages out of the primary procedure.
They also show the exact `ImplicitUsings` and `Nullable` values assumed by the
examples.

The English and Russian quickstarts now use the same in-memory SQLite sample:
`orders` and `customers` tables, their entity interfaces, ordered sample output,
and a `dotnet run` instruction. The pages tell readers to replace a console
project's `Program.cs` and explain why the entity declarations follow its
top-level statements. The projection and filtering guides begin with the same
order data; the joins guide uses those tables to connect orders with customer
names. Before the older examples, all three guides identify their separate
test models and link to entity-mapping guidance. The join introduction labels
matching customer IDs as one example, not the definition of every join.
Russian projection prose and join wording were clarified.

## Verification and limits

The C# Quickstart blocks in English and Russian are identical after line-ending
normalization: 61 lines, SHA-256
`07f053997920023cf5262b4782754f7029350054ece0698edcf9b590a2a8448b`.

### Verification correction

The package-based sample runs below used published `nextorm` 1.0.9 and
`nextorm.sqlite` 1.0.9 packages from the NuGet cache, not assemblies built from
the current working tree. That was the wrong verification target. Those runs
do not verify the current working tree and are not valid acceptance evidence
for this task; retain them only as historical package-run records. No
current-tree sample execution has been performed, and no rerun was made after
this correction.

### Current-working-tree static verification

An independent static review compared the examples with the current working-tree
API declarations. It found:

* `UseSqlite(DbConnection)` at
  `src/nextorm.sqlite/DI/SqliteDataContextOptionsBuilderExtensions.cs:45-57`;
  `CreateDataContext` at `src/nextorm.core/DI/DataContextBuilder.cs:579`;
  and `From` at `DataContextExtensions.cs:1105`.
* `Where` at `EntityBuilder.cs:960`, `Select` at `EntityBuilder.cs:719`,
  `OrderBy` at `EntityBuilder.cs:5746`, and `Join` at `EntityBuilder.cs:3963`;
  `ToListAsync` at `EntityBuilderExtensions.cs:505`.
* The `SqlTableAttribute` declaration is at line 7 (the source path was not
  included in the supplied verification details).
* By inspection, the Quickstart setup, order rows, and displayed output agree
  with the first projection, filtering, and join examples. The English and
  Russian Quickstart C# blocks match. The historical fixture output is
  consistent with `tests/nextorm.integration.tests/Providers/TestProviders/SqliteTestProvider.cs:131-174`.
  No concrete incompatibility was found.

This is static review only. It did not execute against the current working
tree, validate SQL rendering or materialization, or review the full advanced
join surface. It does not change the invalid status of the published-package
runs above.

### Historical package-run record (not current-tree validation)

The samples ran in a temporary `net11.0` console project under
`/tmp/opencode/nextorm-readability-sample`, using the locally cached published
stable NuGet packages `nextorm` 1.0.9 and `nextorm.sqlite` 1.0.9. The project
restored only from the existing local NuGet package cache; no repository
restore or build was run. Its `ImplicitUsings` and `Nullable` settings match
the values in the repository's `Directory.Build.props`.
The three guide queries were extracted from their Markdown samples and inserted
before the exact `IOrder` and `ICustomer` declarations from the Quickstart.

Commands and results:

* Restore: `["dotnet", "restore", "NextormReadabilitySample.csproj", "--source", "/home/alex/.nuget/packages", "--packages", "/tmp/opencode/nextorm-readability-sample/packages", "--ignore-failed-sources"]`, working directory `/tmp/opencode/nextorm-readability-sample`, exit code 0.
* Each sample: `["dotnet", "run", "--no-restore"]`, working directory `/tmp/opencode/nextorm-readability-sample`.
  The final Quickstart, filtering, projection, and join runs each exited 0,
  emitted no stderr, and matched their documented output (4 of 4). Quickstart
  printed three orders; filtering returned two rows; projection returned three
  rows; join returned three customer/order pairs.
* The capture harness was `["python3", "run_samples.py"]` in the same
  directory. Its first invocation exited 1 because the join-output comparison
  found the formatting mismatch below; after the docs output was aligned, the
  second invocation exited 0 with all four comparisons passing.
* The first join comparison found a formatting-only mismatch: the documentation
  padded the order IDs, while the program printed compact rows. The displayed
  output was changed to match the program, then all four samples were rerun and
  matched. The first comparison is retained in
  `/tmp/opencode/nextorm-readability-sample/first-run-mismatch.json`.

Full command, exit-code, output, and stderr records:
`/tmp/opencode/nextorm-readability-sample/execution-results.json` and
`/tmp/opencode/nextorm-readability-sample/logs/`.

The following static structural checks also passed:

* All headings from the saved baseline remain present on the 12 pages, keeping
  existing heading anchors intact.
* All 12 pages use CRLF line endings.
* The five English/Russian content-page pairs have matching heading-level
  outlines. The landing-page catalogs have different existing structures.
* Relative Markdown links on the batch pages resolve to existing target files.
  This checks file existence only, not fragment anchors or DocFX resolution.
* SHA-256 hashes for the seven unrelated provider README files match the saved
  user-work baseline.

Full structural-check output: `/tmp/opencode/readability-structure-check.txt`.
No test suite or DocFX generation was run, and publication was not checked.
