# EV02 - ClickHouse captured `char` cell (defect 2 resolved)

**Original failure:** `System.ArgumentOutOfRangeException: Unknown type: System.Char` from
`ClickHouse.Driver.Types.TypeConverter.ToClickHouseType` whenever a captured `char` was refreshed on a
cached command.

**Fix (`#327 D4: normalize ClickHouse char parameters`):**
`src/nextorm.clickhouse/ClickHouseDataContext.cs` creates its parameters as the private
`ClickHouseValueParameter : ClickHouseDbParameter`, whose `Value` setter maps a CLR `char` to the
equivalent one-character `string` at **every** driver assignment. That covers both the cold bind
(`CreateParam`) and the cached-command refresh, which writes `Value` on the existing driver parameter
without calling `CreateParam` again. Every other value (including `null`, numbers and the JSON
branches) passes through unchanged.

**Live proof:** `ClickHouseStringLiteralSafetyExecutionTests` 3/3 passed, 0 failed, 0 skipped; the
inline and captured char sets are exact (`'a'`, `'\''`, `'\\'`, `default(char)` -> `'\x00'`,
`'\u0001'` -> `'\x01'`). Shape level:
`CapturedCharParameter_ShouldBindAsAOneCharacterString` pins `'a'`/`'П'`/`default(char)`/`null`/`42`.
Log: `.pdca/T327/rv2/logs/D8a-clickhouse.log`, `.pdca/T327/rv2/logs/D4-shape-clickhouse.log`.
