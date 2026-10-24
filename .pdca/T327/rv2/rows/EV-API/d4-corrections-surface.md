# EV-API - surface audit for the two D4 corrections (base aafece5c)

Text recipe (`.pdca/T327/rv2/logs/D4-surface-audit.sh aafece5c`):
- anchored public/protected declaration lines: base 5207, head 5208; the only delta is
  `public override object? Value` **inside the private nested class**
  `ClickHouseValueParameter` (line 220 of `ClickHouseDataContext.cs`). A private nested type is not
  part of the exported surface.
- tokens: base 5413, head 5414 (same line).
- InternalsVisibleTo entries: 14 = 14.
- changed src paths: exactly `SqlBuilder.cs`, `SqlSourceRenderer.cs`, `ClickHouseDataContext.cs`.

Assembly-level exported-surface dump (`MetadataLoadContext`, `IsVisible` types, public/protected/
family members; tool and outputs under `.pdca/T327/rv2/scripts/surface-dump/` and
`.pdca/T327/rv2/logs/nextorm.*.{base,head}.txt`):
- `nextorm.core`: 5156 = 5156 lines, diff empty (exit 0).
- `nextorm.clickhouse`: 284 = 284 lines, diff empty (exit 0).
- VERDICT: PASS - the exported surface is byte-identical to `aafece5c`.

`git diff --check` exit 0; CRLF normalized on every touched `.cs` file.
