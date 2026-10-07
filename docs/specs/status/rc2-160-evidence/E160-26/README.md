# E160-26 — R160-03: `From(QueryCommand<T>)` root `.WithAlias(Alias.Root)` preserves the derived query

- task: D160, cycle 1, plan revision r=2, attempt n=1/3, **contract rv=3**
- branch: 1.0.9-rc2
- decision: P:160-ROOT-CONTRACT (iii) bounded in-scope fix (`EntityBuilder.AliasRoot` normalizes a derived
  root `_query` into an explicit derived-table `FromExpression`; the `ResolveJoinBase` projection guard is not
  removed)
- tree identity: `d5c6b76c` + this working-tree change (uncommitted at capture)

## Exact invocation (argument array)

```
dotnet test tests/nextorm.alias.tests -c Debug --filter "FullyQualifiedName~RootAliasTests"
```

- phase: DO (post-fix)
- exit_code: `0`
- selected_count: `13`, passed `13`, failed `0`, skipped `0`
- log: `root-alias-filtered-green.log` (green run after the expectation correction); the SQL below is
  captured from `query-command-derived-root-redrun.log`, produced by the same fixed code while the
  QueryCommand test still held the stale `orders as 't1'` expectation.

## Captured SQL/plan (positive scenario)

Scenario: `source = ctx.From<Order>(b => b.Table("orders")).Where(o => o.Id == 1).ToCommand()`, then
`ctx.From(source).WithAlias(Alias.Root).Join<Person>(people, (o,p) => o.Root.BuyerId == p.Id, Alias.Buyer)`.

```
select t2.Id from (select Id, BuyerId, ApproverId from orders
 where Id = 1) as 't1' join person as 't2' on t1.BuyerId = t2.Id
```

- derived query preserved (including its `where Id = 1`) as `(select … ) as 't1'`, not flattened
- unaliased baseline regression: `From(source).Join<Person>(...)` returns `ids=[10]`
  (`../E160-ROOT-CONTRACT/unaliased-baseline.txt`)
- negative: `last.Should().NotContain("orders as 't1'")` (physical unwrap must not happen)

## Files

- `query-command-derived-root-redrun.log` — captured SQL + stale expectation failure (fixed code)
- `query-command-source.sql` — the captured SQL one-liner
- `root-alias-filtered-green.log` — filtered run, exit 0, 13/13
- `alias-boundary.log` — full alias suite, exit 0, 71/71
- `build.log` — `dotnet build nextorm.slnx -c Debug`, 0 warnings / 0 errors
