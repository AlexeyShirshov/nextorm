# D370 S3 — typed reader accessors without runtime generic instantiation

Status: implemented in the S3 increment of the campaign for GitHub issue #370, after expert review
(`pdca-escalate`) ruled that changing the public accessor's return value would be a compatibility
break requiring prior approval. The public contract is therefore preserved; the mapper-side split is
internal. The public-contract variant is recorded as a proposal awaiting user approval, not applied.
Scope: `src/nextorm.core/Expressions/SelectExpression.cs`,
`src/nextorm.core/DataContext/Mapping/RowMapperFactory.cs`.

## Problem

`SelectExpression.GetDataRecordMethod(Type)` selected the typed reader accessor for eight
CLR types by instantiating the generic method `DbDataReader.GetFieldValue<T>` at runtime:

```csharp
GetFieldValueMI.MakeGenericMethod(typeof(TimeSpan));   // and DateTimeOffset, ulong, uint,
                                                       // JsonDocument, JsonElement, JsonObject, byte[]
```

`MethodInfo.MakeGenericMethod` needs JIT-generated code for the closed instantiation, which
NativeAOT does not have, and the trimmer cannot analyse the instantiation statically. The
canonical instrument therefore reported sixteen rows (IL3050 and IL2060 on each of the eight
sites) and a native run that reached one of those branches would fail at runtime.

The repair cannot be a suppression: the rows are not annotation debt, they are an AOT-unsupported
operation in the materializer pipeline.

## Decision

The accessor table is split in two, so the increment is obtained **without changing the public
contract** (issue AC4: "public API compatibility preserved unless a design is explicitly approved
first"):

- `public MethodInfo GetDataRecordMethod()` keeps exactly its pre-S3 result. For the eight types it
  still returns the closed generic `DbDataReader.GetFieldValue<T>` member, built with
  `MethodInfo.MakeGenericMethod` in the private `GetLegacyDataRecordMethod`.
- `internal MethodInfo GetReaderAccessorMethod(Type readType)` is what the shared row mapper consumes.
  For the eight types it returns the method of a non-generic static reader on `SelectExpression`:

```csharp
private static ulong ReadFieldValueUInt64(DbDataReader reader, int ordinal)
    => reader.GetFieldValue<ulong>(ordinal);

private readonly static MethodInfo GetFieldValueUInt64MI =
    ((Func<DbDataReader, int, ulong>)ReadFieldValueUInt64).Method;
```

Both tables share `ResolveSimpleAccessor` for the non-generic getters (`GetInt32`, `GetInt64`, …,
`GetValue` for arrays other than `byte[]`, tuples and `Dictionary<string,string>`), so only the eight
typed-accessor branches differ. `RowMapperFactory.GetReaderAccessor` emits
`Expression.Call(method, reader, index)` for a static accessor and keeps the existing instance-getter
path unchanged.

Result: every `GetFieldValue<T>` instantiation the **mapper** reaches is closed at compile time, so
the trimmer sees it, NativeAOT compiles it ahead of time, and the mapper tree contains a plain static
call. The reader observes exactly the same `GetFieldValue<T>` request as before. The public method is
not called by the product or by the AOT runner, so it is trimmed from the published image; if a
consumer calls it from a native build, the pre-existing runtime instantiation is still there — that is
the unchanged pre-S3 behaviour, not a regression introduced here.

## Alternatives rejected

- **Change the public method's return value to the static accessor.** Rejected: that is a public
  compatibility break (reflection callers get a different member, `IsStatic`/`DeclaringType` change,
  IL emitters break) and AC4 requires an explicitly approved design *before* implementation. It stays
  a recorded proposal for the user, not applied.
- **Hoist `MakeGenericMethod` into a static field.** It removes the analyzer warning but not the
  operation: the field initializer still instantiates a generic method from a runtime `Type`, which
  NativeAOT cannot do for a value-type instantiation. Hiding a diagnostic is the shortcut the issue
  forbids.
- **Read through `IDataRecord.GetValue` plus a cast.** AOT-safe, but it boxes every value-type read
  (`TimeSpan`, `DateTimeOffset`, `ulong`, `uint`, `JsonElement`) and moves the conversion into the
  mapper body; both are regressions against the current typed path.
- **Keep the generic accessor on the mapper path and annotate the caller.** `DbDataReader.GetFieldValue<T>`
  carries no `DynamicallyAccessedMembers` contract to satisfy, and annotating would move the warning to
  every caller instead of removing the operation.

## Observable change

None on the public surface: `GetDataRecordMethod()` returns the same members, with the same
visibility and signature, as before S3. The new `GetReaderAccessorMethod(Type)` and the internal
`ByteArrayFieldValueReader` are internal. Unit tests characterize both tables: the public one keeps the
instance generic `GetFieldValue<T>` shape for the eight types, and the mapper path is proven at runtime
by observing the reader receive `GetFieldValue<T>` for the same `T`.

## Deferred

- **Public-contract proposal (needs user approval, not applied).** Returning the static reader from
  the public accessor would remove the last `MakeGenericMethod` calls from the assembly, at the cost of
  the compatibility break described above. It is out of scope unless the user explicitly approves a
  design that changes that contract.
- The same pattern exists in provider assemblies that the canonical runner does not reference
  (`PostgresDataContext`, `ClickHouseDataContext`). They are out of this step's allowed surface and
  are in scope for Stream B, when a native run per provider makes their accessors reachable.
