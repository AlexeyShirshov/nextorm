# E222-MUTATION — T222 rv=1 mutation disposition

- Tool: `dotnet-stryker` 5.0.0 (installed via `.config/dotnet-tools.json`; installed = yes).
- Scope: `src/nextorm.core/Builders/Joins/JoinedEntityBuilder.cs` (holds 24 of the 28 new members),
  project `nextorm.core`, test project `nextorm.core.tests`, runner `mtp`, config
  `E222-MUTATION/stryker-config.json`.
- Command: `bash artifacts/pdca/D222-join-overloads-by-type/rv1/capture.sh E222-MUTATION -- dotnet stryker -f artifacts/pdca/D222-join-overloads-by-type/rv1/E222-MUTATION/stryker-config.json -O artifacts/pdca/D222-join-overloads-by-type/rv1/E222-MUTATION`
- Result: exit 0; 34931 mutants created, 107 tested (8900 compile-error/ignored/no-coverage removed), 65 killed / 42 survived / 0 timeout, score 37.36%; 18m05s.
- JSON report: `E222-MUTATION/reports/mutation-report.json` (21 MB; gitignored by `E222-MUTATION/.gitignore`).

## Attribution (important)

Of the 107 tested mutants, all are in `JoinedEntityBuilder.cs`:

- **New T222 source-less members: 24 mutants killed, 0 survived** (24/28 line-targets overlap the new method bodies).
- Pre-existing methods in the same file: 41 killed, **42 survived** — pre-existing mutation debt, not T222 code.

So the new code has no surviving mutants; the 42 survivors are unrelated to this change.
