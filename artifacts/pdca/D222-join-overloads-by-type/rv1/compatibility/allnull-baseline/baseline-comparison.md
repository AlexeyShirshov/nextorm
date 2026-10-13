# E222-COMPAT baseline comparison — V24 all-null untyped pair

- Probe source: `allnull-baseline/BaselineProbe.cs`. The all-null *argument-pair* line is identical to
  `allnull/AllNull.cs`, but the receiver line differs deliberately: `BaselineProbe.cs:13` builds the
  joined receiver with the explicit `Join(ctx.From<A2>(), ...)` (the only form that exists pre-T222),
  while `allnull/AllNull.cs:20` uses the source-less `Join<A2>(...)`. The pair under test (`joined.Join<A1>(null, null)`)
  is the same in both.
- Pre-T222 surface: detached worktree at base commit `4fbb603b`; the probe project referenced that core.
  `E222-COMPAT-BASELINE` exit 0 -> the all-null pair already compiled before the source-less overloads.
  Baseline pin: `4fbb603b` = `4fbb603b27ff811508ec477604efba2c2132066a` (see `baseline-revision.txt`).
- HEAD surface: `E222-COMPAT` case `T222-COMPAT-ALLNULL` exit 0 -> it still compiles.
- Bound overload (unchanged by T222): the pre-existing two-argument
  `Join<TJoinEntity>(Cte<TJoinEntity>, Expression<...>)` overload, present at `4fbb603b` in both
  `EntityBuilder.cs` and `Builders/Joins/JoinedEntityBuilder.cs` (see `baseline-overloads.txt`).
- Runtime binding: `InMemoryJoinTests.TestTypeOnlyJoin_AllNullPair_BindsToPreExistingCteOverload`
  asserts `ArgumentNullException` with `ParamName == "cte"`.

Conclusion: no new ambiguity and no change of the bound overload versus the pre-T222 surface (baseline
parity). The source-less overloads are not candidates for the untyped all-null pair.
