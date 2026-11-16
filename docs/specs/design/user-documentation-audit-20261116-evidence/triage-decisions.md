# Primary triage decisions

These decisions distinguish findings from search candidates.

## Confirmed corrections

1. Contributor SDK: `CONTRIBUTING.md:51` contradicts the repository SDK pin in
   `global.json:3-4`. This is not evidence that consumer `net10.0` support is
   wrong. Published 1.0.9 catalog metadata confirms both `net10.0` and `net11.0`
   dependency groups for the three packages checked.
2. Package release status: seven provider/EF package READMEs, each at line 12,
   claim all releases are prereleases and `--prerelease` is required. Official
   NuGet indexes contain stable 1.0.9. Do not claim that the existing installation
   command necessarily fails: it is the stated requirement/status that is wrong.
3. DI prose: EN line18/RU line17 contain a bare double-backtick span after the
   description of the factory call. The missing API name must be restored after
   semantic confirmation; nearby examples show `CreateDataContext()`.
4 eux. Aggregate prose: EN lines3/42 and RU lines3/44 similarly end with a bare
   double-backtick span. This is real file content, not a read truncation.
5. API reference formatting: EN line240/RU line238 concatenate the
   `CommandTimeout` and `UseQueryCache` links and contain stray punctuation.

## Candidates, not confirmed runtime failures

- CSV example: EN streaming guide lines361-365/RU363-367 combines a captured
  `from` with an explicit positional `from` argument. This appears inconsistent
  with the page's own parameter guidance; whether the extra argument is ignored,
  rejected, or otherwise meaningful requires implementation evidence.
- Installation implicit-usings/nullable wording is ambiguous. Repository build
  properties are established, consumer propagation is not. No package archive
  has been inspected.
- Wrong-overload links in the DI type-path explanation require semantic
  confirmation of the generic overload UID.
- Missing MySQL/MariaDB/ClickHouse links in the RU landing provider list are
  a discoverability improvement, not evidence that the pages are unpublished or
  unreachable through the overview or TOC.

## Rejected false positives

- Dated benchmark environments and historical verification reports are
  legitimate snapshots, not inherently stale documentation.
- Explicit MariaDB/ClickHouse/SQL Server version and test-coverage disclaimers
  are useful qualifications, not unsupported-feature defects.
- Unspecified `MyTopology`, `AmbientTenant`, and hash implementations in
  illustrative recipes do not automatically make a tutorial non-executable.
- Reference pages are not required to adopt a full tutorial skeleton or numbered
  steps for every API example.
- The duration guide explicitly says placeholders differ and names SQLite
  `$p0`; a grouped illustrative `@p0` SQL block is not proof of a broken query.
- Truncated display of very long Markdown table rows does not prove source
  corruption.
- Existing consistent project heading conventions and project async-naming
  rules outrank generic style defaults.
- Accidental inclusion of internal `docs/superpowers/**` in DocFX input is an
  input-configuration exposure, not proof that an actual deployed site contains
  those pages. DocFX was not run.
