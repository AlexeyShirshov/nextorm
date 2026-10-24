# EV05 - SQL Server Unicode/long/NUL and SQLite NUL native limits

- SQL Server (10/10): Cyrillic, CJK, emoji, NUL-bearing and the 4096-character value are exact through
  inline and captured projection and through INSERT/UPDATE into the task-owned `nvarchar(max)` probe
  table; NUL storage/readback exact.
- SQLite (10/10): NUL storage/readback is exact (`CAST(X'00' AS TEXT)`); native LIKE truncates at the
  first NUL - measured (pattern `"a"` matches a NUL-bearing `"a..b"` value) and asserted as the native
  limitation, not emulated. Unicode/emoji and the 4096-character value exact.
