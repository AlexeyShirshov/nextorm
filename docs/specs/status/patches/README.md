# Preserved B1/B2 patches — #183 iteration-15 cached-path

Durable locations for the reverted/incomplete #183 B1 and B2 patches, copied out of the
group-1 worktree **before any future worktree/branch cleanup**. Both patches are
`unified diff` text and are preserved uncommitted-applied (never merged, never counted as
delivered value).

| file | sha256 | source worktree path |
|---|---|---|
| `b1-incomplete.patch` | `0be74f92aa4250c78a566849dc1574d97c1c74aa154e18f56650501e34a7dc50` | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1/benchmarks/BenchmarkDotNet.Artifacts/iteration15-b1/b1-incomplete.patch` |
| `u2-incomplete.patch` | `dd876ae9ed5b68a4fe3804478194db64741fb39bde3bebae24edb21899d18b7f` | `/home/alex/sources/nextorm-worktrees/1.0.9-b-5/group-1/benchmarks/BenchmarkDotNet.Artifacts/iteration15-remaining/U2/u2-incomplete.patch` |

Both were rejected/reverted because an attributable target-stage speedup was not provable
on the shared host; no performance improvement is claimed. See
`docs/specs/status/iteration-15-cached-path-183-3.md` and
`docs/specs/performance/iteration-15-cached-path-results.md`.
