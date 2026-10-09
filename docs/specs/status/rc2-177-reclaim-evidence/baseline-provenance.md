# R177 baseline discovery provenance (rcv=2 prerequisite)

- unit: rc2-177-native-json-reclaim-1 (cycle N=1, plan_revision r=2, evidence contract rv=2)
- baseline B = 6263 total; succeeded 6262, skipped 1, failed 0.
- pre-R177 collection revision: cf34f910 (rc2-reclaim baseline).
- source artifact: docs/specs/status/rc2-175-joininto-quality-reclaim-1-evidence/collection/unit.log
  - line 103: `AGGREGATE total=6263 succeeded=6262 skipped=1 failed=0 rc_all=0`
  - lines 208-219: per-project rc=0 for all 11 projects.
  - line 220: repeated `AGGREGATE total=6263 succeeded=6262 skipped=1 failed=0 rc_all=0`; line 221: `exit_code=0`.
- invocation: `source: unit.log aggregate; invocation not captured`.
  - The log header (line 105) records only the shape `# ===== CHECK r2 aggregate re-run 2026-10-09T13:30Z : per-project dotnet test <proj> -c Debug --no-build x11 =====`, i.e. 11 per-project `dotnet test` runs with `-c Debug --no-build`. No single literal runnable command line is present in the log, so no literal command is asserted here.
- pre-change discovery counts for the changed test classes (pre-R177 baseline):
  - NextORM.Core.Tests.JsonNativeStreamTests: 51
  - NextORM.SqlServer.Tests.SqlServerNativeJsonSqlTests: 5
  - NextORM.Integration.Tests.SqlServerNativeJsonStreamTests: not separately evidenced in the cited baseline artifact (integration class; no per-class pre-change count captured).
- B=6263 is the acceptance baseline; the historical collection total 6247 is not the acceptance target.
