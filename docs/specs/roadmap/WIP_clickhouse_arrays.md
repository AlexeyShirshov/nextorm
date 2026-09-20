# WIP: массивы ClickHouse и `arrayJoin`

> Рабочий план/отчёт по скилу `implementing-todo-features`. Источник: `todo_clickhouse.md` →
> «Уровень 3 → Массивы ClickHouse и `ARRAY JOIN`». **Статус: первый срез + клауза + привязка элемента
> реализованы, проаудированы, интеграция зелёная; follow-up перечислен ниже.**

## Пункт и цель

- Фича: array-поверхность ClickHouse — функции массивов над array-**колонками** и `arrayJoin`
  (расширение строк).
- Провайдер: ClickHouse.
- Критерий приёмки: запросы `has`/`indexOf`/`length`/`arrayStringConcat`/`splitByChar`/`hasAny`/
  `hasAll`/`arraySort`/`arrayReverse`/`arrayDistinct` и `arrayJoin` для расширения строк строятся,
  компилируются и выполняются на реальном ClickHouse; прочие провайдеры отклоняют.
- Срез: функции первого порядка + скалярный `arrayJoin`. Higher-order (`arrayMap`/`arrayFilter`),
  `ARRAY JOIN`-клауза с множественными массивами и row reader `Array(T)`/`Tuple` — отдельные
  follow-up (см. «Осталось»).

## Провайдер × форма (шаг 1)

Источники: PostgreSQL — официальный array reference; ClickHouse — официальный array-functions
reference и `ARRAY JOIN` doc; SQL Server / MySQL / MariaDB / SQLite — соответствующие справочники
(массивов как типа нет).

| Провайдер | array-колонки | функции массивов | `arrayJoin`/`ARRAY JOIN` |
|---|---|---|---|
| PostgreSQL | `T[]` (native), но поверхность — только параметры/`Split` | `cardinality`, `array_position`, `array_to_string`, `@>`/`&&`, … (`PostgresFunctions`, `SupportsArrays`) | `unnest(...)` (TVF, уже есть) |
| SQL Server | — (нет array-типа) | — | — |
| MySQL | — | — | — |
| MariaDB | — | — | — |
| SQLite | — | — | — |
| ClickHouse | `Array(T)` | `length`, `has`, `indexOf`, `hasAny`, `hasAll`, `arrayStringConcat`, `arraySort`, `arrayReverse`, `arrayDistinct`, `splitByChar` | `arrayJoin(arr)` (расширяет строки) |
| InMemory | — | — | — |

Единообразие: массив как тип есть только у PostgreSQL и ClickHouse; на PG поверхность уже закрыта
`PostgresFunctions` + `SupportsArrays` (семантика и имена PG: `array_position` 1-based и т.п.), поэтому
добавляется **нативная CH-поверхность** `ClickHouseFunctions` с CH-именами (tier b) и отдельными
флагами. SQL Server/MySQL/MariaDB/SQLite/InMemory не выражают массивы — гейт `Supports*` (default
`false`).

Имена CH = camelCase от CLR-имени: `length`→`length`, `has`→`has`, `index_of`→`indexOf`,
`has_any`→`hasAny`, `has_all`→`hasAll`, `array_string_concat`→`arrayStringConcat`,
`split_by_char`→`splitByChar`, `array_sort`→`arraySort`, `array_reverse`→`arrayReverse`,
`array_distinct`→`arrayDistinct`, `array_join`→`arrayJoin`.

## Ближайший аналог C# и tier

- `T[]`/`T[]` аргументы → tier (b): новый метод `ClickHouseFunctions` + транслятор + флаг.
- `arrayJoin` — tier (b)/скаляр (возвращает элемент `T`, поэтому проецируется без row reader массива).
- BCL-аналога нет → tier (b).

## Диалектный план

- `ISqlDialect.SupportsArrayFunctions` (default `false`; ClickHouse `true`) — гейт CH-функций.
- `ISqlDialect.SupportsArrayJoin` (default `false`; ClickHouse `true`) — гейт `arrayJoin`.
- `ISqlDialect.MakeArrayFunction(name, call)` (default identity; ClickHouse оборачивает `length`/`indexOf`
  в `toInt64(...)`): имена рендерит транслятор, отдельного `Make*` на каждое имя не нужно.
- `SqlOperandTranslator.AppendArrayOrColumn`/`IsCapturedArray`: array-колонка рендерится как SQL,
  захваченный/константный массив — одним параметром (поведение как раньше).

## Публичный API (точные сигнатуры)

`ClickHouseFunctions`: `array_join<T>(T[] array)`, `length<T>(T[] array)`, `has<T>(T[] array, T element)`,
`index_of<T>(T[] array, T element)`, `has_any<T>(T[] array, T[] other)`, `has_all<T>(T[] array, T[] other)`,
`array_string_concat<T>(T[] array, string? delimiter = null)`,
`split_by_char(string? separator, string? value)`, `array_sort<T>(T[] array)`, `array_reverse<T>(T[] array)`,
`array_distinct<T>(T[] array)`.
Каждый — `<summary>`; регистр `API-NAMING-REVIEW.md`. `length`/`indexOf` диалект оборачивает в
`toInt64(...)` (`MakeArrayFunction`); `array_string_concat` с дефолтным `delimiter` (null) опускает
второй аргумент; array-возвращающие функции применимы только вложенно.

## Тесты (итог)

- CH SQL-gen (13 тестов): рендер имён, array-колонка, `arrayJoin` в SELECT, захваченный массив одним
  параметром, дефолтный разделитель.
- `ClickHouseDialectTests.ArrayCapabilities_ShouldBeEnabled`.
- PostgreSQL: `ClickHouseArrayFunctions_UnsupportedByProvider_ShouldThrow`.
- CH integration (4 теста, реальный ClickHouse): `ArrayFunctions_ShouldReturnValues`,
  `ArrayStringConcat_WithDefaultDelimiter_…`, `ArrayHasAny_WithCapturedArray_…`, `ArrayJoin_ShouldExpandRows`.
- Release 0/0; CH unit **123**, все наборы зелёные; покрытие 84.7% line / 73.1% branch.
- Аудит `nextorm-code-auditor`: P0 нет; **P1 AR2** (доки EN+RU противоречили) — исправлено;
  **Находка 19** (`arrayStringConcat(col, null)` при дефолтном разделителе) — исправлено;
  P2 AR1/AR3/AR4 занесены в реестры; AR3 (устаревшие `<summary>`) исправлены.

## Клауза `[LEFT] ARRAY JOIN` (реализовано)

FROM-модификатор: `EntityBuilder.ArrayJoin`/`LeftArrayJoin` (+ ковариантные `new` на
`JoinedEntityBuilder<T1..T8>`), enum `ArrayJoinKind { Inner, Left }`. Модель: `_arrayJoins`/`_arrayJoinKind`
на `EntityBuilder` → `QueryDefinition` → `QueryCommand` (`_arrayJoins`, `_preparedArrayJoin`,
`ArrayJoinExpressions`); подготовка `PrepareArrayJoin`; план-ключ (`ArrayJoinKind` + выражения);
рендер `SqlBuilder.MakeSelect` после JOIN'ов и до `PREWHERE`/`WHERE` через
`SqlSourceRenderer.MakeArrayJoin`; флаг `SupportsArrayJoinClause` + `MakeArrayJoin` (default
throw; ClickHouse → ` array join `/` left array join `); in-memory бросает. Валидация: выражение —
массив/`IEnumerable` (кроме `string`), нельзя смешивать `ArrayJoin` и `LeftArrayJoin` в одной клаузе.
Вырожденный элемент можно привязать к CLR-члену через `ArrayJoinElement`/`LeftArrayJoinElement`
(см. ниже) либо спроецировать скалярным `array_join`. Тесты: CH SQL-gen (рендер/список/join-запрос/
mixing throw/non-sequence throw), `Postgres…ArrayJoinClause_UnsupportedByProvider_ShouldThrow`,
`InMemoryJoinTests.TestArrayJoinClause_ShouldThrow`,
CH integration `ArrayJoinClause_ShouldExpandRowsAndDropEmptyArrays`/`LeftArrayJoinClause_ShouldKeepEmptyArrays`.
Аудит: P0/P1 нет; AJ2 (сделать `ArrayJoinExpressions` internal) — исправлено; AJ3 (`<inheritdoc/>`) —
исправлено; AJ4 (нет `None` у `ArrayJoinKind`) — принято; AJ5 (доки EN+RU) — исправлено.

## Привязка вырожденного элемента (A1, реализовано)

`EntityBuilder.ArrayJoinElement<TElement>(Expression<Func<TEntity, IEnumerable<TElement>>>)` и
`LeftArrayJoinElement<TElement>(...)` возвращают
`EntityBuilder<ArrayJoinProjection<TEntity, TElement>>`; тип реализует маркер `IArrayJoinProjection`
(намеренно **не** `IProjection`) и даёт `Item1` (исходная сущность) и `Element` (вырожденный элемент).
Рендер: последнее выражение клаузы алиасится (`as __nextorm_aj_element`), а `MemberTranslator`
транслирует `ArrayJoinProjection<,>.Element` в этот алиас (константы в `ArrayJoinNames`, `internal`).
Ключевой приём: команда сохраняет `EntityType` = исходная сущность (`SourceEntityType`), а проекцией
меняется лишь параметр лямбды — поэтому FROM/алиасы/рендер клаузы не трогаются. `BindArrayJoinElement`
(флаг) протянут через `QueryDefinition`/`QueryCommand`/clone/план-ключ (`Equals`+`GetHashCode`).
Первый срез: один источник без join'ов; `Where`/`Having` — после вызова (их параметр — проекция);
in-memory бросает `NotSupportedException` в `InMemoryQueryBuilder.GetPreparedQueryCommand` (до
компиляции материализатора, т.к. проекция ссылается на параметр `ArrayJoinProjection`).
Тесты: CH SQL-gen `ArrayJoinElement_ShouldRenderElementAlias`/`LeftArrayJoinElement_ShouldRenderLeftElementAlias`/
`ArrayJoinElement_WhereOnElement_ShouldReferenceAlias`/`ArrayJoinElement_OrderByElement_ShouldReferenceAlias`/
`ArrayJoinElement_AfterCondition_ShouldThrow`/`ArrayJoinElement_SecondCall_ShouldThrow`;
`Postgres…ArrayJoinElement_UnsupportedByProvider_ShouldThrow`;
`InMemoryJoinTests.TestArrayJoinElement_ShouldThrow`;
CH integration `ArrayJoinElement_ShouldBindExpandedElement`/`ArrayJoinElement_ShouldFilterOnElement`/
`LeftArrayJoinElement_ShouldKeepEmptyArrayWithDefaultElement` (реальный ClickHouse).
Аудит: P0/P1 нет; AJ6 (`IEnumerable<TElement>` вместо `TElement[]`) — исправлено;
AJ9 (`<exception>`-доки) — исправлено; Находка 20 (CRLF нового файла) — исправлено;
Находка 21 (гейт in-memory в core) — исправлено (перенесено в провайдер); AJ7/AJ8 — принято/отложено.

## Осталось (follow-up)

- Higher-order `arrayMap`/`arrayFilter`/`arrayExists` (лямбда-аргумент).
- Привязка элемента для нескольких массивов/join'ов (сейчас один источник без `join`).
- Row reader `Array(T)`/`Tuple` для `groupArray`/`topK`/`quantiles`/`JSONExtractArrayRaw`.
- Маппинг `string.Split` на `splitByChar` в CH (сейчас `string_to_array` — PG-only).

## Файлы

1. `src/nextorm.core/DataContext/Dialect/ISqlDialect.cs`, `SqlDialectBase.cs`
2. `src/nextorm.clickhouse/ClickHouseDialect.cs`
3. `src/nextorm.core/Visitors/ArraySqlTranslator.cs`, `SqlOperandTranslator.cs`, `MemberTranslator.cs`
4. `src/nextorm.core/Query/SqlFunctions.ClickHouse.cs`
5. `src/nextorm.core/Expressions/ArrayJoinKind.cs`, `ArrayJoinProjection.cs`
6. `src/nextorm.core/Builders/EntityBuilder.cs`, `Builders/Joins/JoinedEntityBuilder.cs`
7. `src/nextorm.core/Query/QueryDefinition.cs`, `QueryCommand.cs`, `QueryCommand.Clone.cs`, `QueryPlanEqualityComparer.cs`, `QueryCommand.QueryPreparer.cs`
8. `src/nextorm.core/DataContext/SqlBuilder.cs`, `SqlSourceRenderer.cs`, `InMemoryQueryBuilder.cs`
9. Тесты (`nextorm.clickhouse.tests`, `nextorm.postgres.tests`, `nextorm.core.tests`, `nextorm.integration.tests`) + docs EN+RU
