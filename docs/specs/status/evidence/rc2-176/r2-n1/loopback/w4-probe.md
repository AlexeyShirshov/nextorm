# D176 loop-back — W4 probe (interface-typed / upcast member)

Finding **W4** (`QueryPreparer.cs` capture classified via `TypeFacts.UnwrapConvert(...).Type`, while
the shape build classified from the declared member type) was probed with a projection whose member is
an upcast/interface-typed value:

```csharp
var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
    .Select(x => new { x.Id, Upcast = (IJsonEntity)x });
```

## Probe outcome

`w4-probe.log` (the probe run before the guard assertion was completed) shows the ordinary
materialization path (`command.ToList()`) rejecting the projection first:

```
System.NotSupportedException : Property 'Upcast' with index (1) has type
NextORM.Sqlite.Tests.JsonStreamingTests+IJsonEntity which is not supported
    at NextORM.Core.SelectExpression.GetDataRecordMethod(Type readType)
    at NextORM.Core.RowMapperFactory.GetReaderAccessor(...)
    ...
    at NextORM.Sqlite.Tests.JsonStreamingTests.InterfaceTypedUpcastMember_ShouldFailClosedBeforeOutput()
```

## Disposition: **guarded — no defect**

- The ordinary materializer already rejects an interface-typed member (`NotSupportedException` from
  `SelectExpression.GetDataRecordMethod`), so there is no JSON-only divergence: the JSON path cannot
  succeed where ordinary materialization fails, nor produce a wrong shape.
- The guard test `JsonStreamingTests.InterfaceTypedUpcastMember_ShouldFailClosedBeforeOutput` now
  asserts **both** outcomes uniformly: `command.ToList()` throws, and `WriteJson` throws
  `NotSupportedException` with the destination untouched (`Position`/`Length` unchanged). It passes.
- No product-source change was made for W4; the divergence is closed by the existing uniform
  fail-closed behaviour plus the guard test. `probe-w4-t1-t2.log` also captures the W4/T1/T2 probe
  set (`OpaqueFactoryProducedObject`, `DbSideJsonRoute`).
