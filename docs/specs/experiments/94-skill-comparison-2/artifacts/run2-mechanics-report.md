# Эксперимент #94: сравнение сред (механика)

- база: `8366dd6ad0399f675ae44b5285857c13cb33a995`
- эталон upstream (1.0.9-a): `37d3092` (+`b67bcea`)

## Сводка

| рука | среда | rc | wall, s | cost | files | tests | overlap c upstream | skills |
|---|---|---|---|---|---|---|---|---|
| a1 | bare: без скиллов | 0 | 1254 | 0.214725 | 37 | 3 | 19/37 | нет |
| a2 | implementing-todo-features | 0 | 2514 | 0.268319 | 43 | 8 | 17/43 | implementing-todo-features |
| a3 | pdca-dotnet (все агенты flash) | 0 | 38223 | 0.095524 | 37 | 8 | 15/37 | pdca-dotnet, nextorm-pdca, running-integration-tests |

## a1 — bare: без скиллов
- профиль: `deepseek.jsonc (oc-ds)`; shortstat: `37 files changed, 1302 insertions(+), 25 deletions(-)`
- недостаёт против upstream (24): `docs/guide/32-dynamic-columns.md`, `docs/ru/guide/32-dynamic-columns.md`, `docs/specs/design/API-NAMING-REVIEW.md`, `src/nextorm.core/DataContext/DynamicColumns.cs`, `src/nextorm.core/DataContext/InMemoryRowMaterializer.cs`, `src/nextorm.core/DataContext/Meta/DynamicColumnsTypeFacts.cs`, `src/nextorm.core/DataContext/Meta/IEntityMetadata.cs`, `src/nextorm.core/DataContext/Meta/Implementation/EntityMetadata.cs`, `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`, `tests/nextorm.clickhouse.tests/Entities.cs`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`, `tests/nextorm.core.tests/DynamicColumnsInMemoryTests.cs`
- лишнее против upstream (18): `docs/guide/31-dynamic-columns.md`, `docs/ru/guide/31-dynamic-columns.md`, `docs/specs/comparison/linq2db-backlog-gap-analysis.md`, `src/nextorm.core/Builders/BulkInsertBuilder.cs`, `src/nextorm.core/Builders/InsertBuilder.Values.cs`, `src/nextorm.core/Builders/InsertBuilder.cs`, `src/nextorm.core/Builders/MergeBuilder.Columns.cs`, `src/nextorm.core/Builders/MergeBuilder.InMemory.cs`, `src/nextorm.core/Builders/MergeBuilder.cs`, `src/nextorm.core/Builders/ReturningProjection.cs`, `src/nextorm.core/Builders/UpdateBuilder.cs`, `src/nextorm.core/DataContext/DynamicColumnsReader.cs`

## a2 — implementing-todo-features
- профиль: `deepseek.jsonc (oc-ds)`; shortstat: `43 files changed, 1346 insertions(+), 107 deletions(-)`
- недостаёт против upstream (26): `docs/advanced/limitations.md`, `docs/guide/32-dynamic-columns.md`, `docs/guide/toc.yml`, `docs/ru/advanced/limitations.md`, `docs/ru/guide/32-dynamic-columns.md`, `docs/ru/toc.yml`, `src/nextorm.core/DataContext/DynamicColumns.cs`, `src/nextorm.core/DataContext/InMemoryRowMaterializer.cs`, `src/nextorm.core/DataContext/Meta/DynamicColumnsTypeFacts.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`, `tests/nextorm.clickhouse.tests/Entities.cs`, `tests/nextorm.clickhouse.tests/SqlGenerationTests.cs`
- лишнее против upstream (26): `docs/getting-started/03-entities-and-metadata.md`, `docs/ru/getting-started/03-entities-and-metadata.md`, `docs/specs/comparison/linq2db-backlog-gap-analysis.md`, `docs/specs/design/code-smells-review.md`, `docs/specs/design/solid-review.md`, `src/nextorm.core/Builders/EntityBuilder.cs`, `src/nextorm.core/Builders/InsertBuilder.Values.cs`, `src/nextorm.core/Builders/InsertBuilder.cs`, `src/nextorm.core/Builders/MergeBuilder.cs`, `src/nextorm.core/Builders/UpdateBuilder.cs`, `src/nextorm.core/DataContext/InMemoryDataContext.cs`, `src/nextorm.core/DataContext/InMemoryQueryBuilder.cs`

## a3 — pdca-dotnet (все агенты flash)
- профиль: `test-pdca.jsonc`; shortstat: `44 files changed, 3488 insertions(+), 35 deletions(-)`
- коммиты: `c0d0086 #94 dynamic columns store: declared extra columns read into/written from IDictionary<string,object?>`
- недостаёт против upstream (28): `docs/advanced/limitations.md`, `docs/guide/32-dynamic-columns.md`, `docs/ru/advanced/limitations.md`, `docs/ru/guide/32-dynamic-columns.md`, `src/nextorm.core/DataContext/DynamicColumns.cs`, `src/nextorm.core/DataContext/InMemoryRowMaterializer.cs`, `src/nextorm.core/DataContext/Meta/DynamicColumnsTypeFacts.cs`, `src/nextorm.core/DataContext/Meta/IPropertyMetadata.cs`, `src/nextorm.core/DataContext/Meta/Implementation/PropertyMetadata.cs`, `src/nextorm.core/DataContext/RowMapperFactory.cs`, `src/nextorm.core/DataContext/SqlBuilder.cs`, `src/nextorm.core/Expressions/SelectExpressionPlanEqualityComparer.cs`
- лишнее против upstream (22): `docs/getting-started/03-entities-and-metadata.md`, `docs/guide/31-dynamic-columns.md`, `docs/ru/getting-started/03-entities-and-metadata.md`, `docs/ru/guide/31-dynamic-columns.md`, `docs/specs/design/code-smells-review.md`, `docs/specs/status/dynamic_columns-94.md`, `src/nextorm.core/Builders/InsertBuilder.Values.cs`, `src/nextorm.core/Builders/MergeBuilder.cs`, `src/nextorm.core/Builders/UpdateBuilder.cs`, `src/nextorm.core/DataContext/Meta/DynamicColumnName.cs`, `src/nextorm.core/DataContext/Meta/Implementation/DynamicColumnMetadata.cs`, `src/nextorm.core/DataContext/RawMapperFactory.cs`

