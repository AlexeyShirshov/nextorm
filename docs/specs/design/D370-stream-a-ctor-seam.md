# D370 Stream A constructor-resolution seam

## Decision

Relocate constructor reflection from `RowMaterializerBuilder` to one internal cached resolver in `DataContext/Meta/`, keyed by runtime `Type`. The resolver returns `ConstructorInfo?` and provides three operations: longest public constructor, public parameterless constructor, and the seven-parameter `Range<T>` constructor. Each selection is cached per type and operation.

## Rationale

`EntityMetadata` is not the seam: DTO projection types have no entity metadata, both entity and projection materialization use the shared `BuildCore` path, and `Build<TResult>` has no command from which to obtain metadata. The metadata layer already owns unannotated-`Type` inspection, including `EntityMetadataBuilder`; keeping the cached type inspection there follows that ownership. Each selection is executed once per cached type. `RowMaterializerBuilder` remains pure expression assembly.

## Preserved behavior

The longest-public-constructor rule remains `GetConstructors()`, ordered by descending parameter count, then `FirstOrDefault()`. Parameterless lookup remains `GetConstructor(Type.EmptyTypes)`. The range constructor remains selected with `Single` for seven parameters, preserving the missing/multiple-match failure behavior. Existing preparation exceptions and messages remain unchanged.

## Deferred follow-ups (outside S2)

- Lambda-constructor token via `NewExpression.Constructor`.
- `[DynamicallyAccessedMembers]` propagation.
- Source generator.

Each is a later elimination step, not part of this relocation.
