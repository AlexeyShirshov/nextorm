# TODO: Вставка данных (`INSERT`)

> Рабочий план (design RFC). Источники: GitHub issues
> [#3 «TODO: Insert»](https://github.com/AlexeyShirshov/nextorm/issues/3),
> [#15 «TODO: Insert, update, delete with output table»](https://github.com/AlexeyShirshov/nextorm/issues/15);
> milestone `1.0-a.4`. Этот документ вводит **общий каркас DML**, на который ссылаются планы
> [todo_update.md](todo_update.md), [todo_delete.md](todo_delete.md), [todo_merge.md](todo_merge.md).

## Пункт и цель

- Фича: `INSERT` через билдер — одиночная и многострочная вставка значений и сущностей, возврат
  сгенерированного ключа.
- Критерий приёмки: `ctx.InsertInto<T>().Value(...).Insert()` рендерит параметризованный
  `INSERT ... VALUES (...)` на SQLite/PostgreSQL/SQL Server/MySQL/MariaDB, возвращает число
  вставленных строк, а `InsertWithIdentity` отдаёт сгенерированное значение; ClickHouse и
  in-memory отклоняют то, что не умеют, через `NotSupportedException` с явным сообщением.
- Позиционирование: библиотека перестаёт быть строго read-only — появляется подсистема DML.
  **Change tracking и `SaveChanges` не добавляются**: каждая запись — явная команда, как и в
  linq2db/`ExecuteNonQuery`, а не как в EF Core.

## Почему это нужно (мотивация)

1. **Пробел в матрице.** `comparison/linq2db-comparison.md:50-51` и `sql-capabilities-gap-analysis.md:132`
   фиксируют DML (`INSERT`/`UPDATE`/`DELETE`/`MERGE`) как отсутствующий; issue #3/#4/#5/#6 — его
   трекеры, issue #15 — отдельно про `OUTPUT`/`RETURNING`.
2. **Реальные сценарии.** Без записи nextorm нельзя использовать как единственный слой доступа к
   данным; приходится смешивать его с сырым ADO.NET или другим ORM на том же соединении —
   именно тот сценарий, который закрывает [todo_transactions.md](todo_transactions.md).
3. **Единая параметризация и план-кэш.** Записи должны переиспользовать существующий механизм
   параметров (`norm_p`), `Prepare()` и кэш планов, чтобы не терять заявленную производительность.

## Текущее состояние и разрыв

Токена `INSERT`/`Update`/`Delete` в движке нет. `QueryCommand` описывает только `SELECT`
(поля `SelectList`/`From`/`Joins`/`Where`/`Grouping`/`Sorting`/`Paging`), а `SqlBuilder.MakeSelect`
(`DataContext/SqlBuilder.cs:33`) — единственный сборщик SQL. Исполнение — `QueryExecutor`
(`DataContext/QueryExecutor.cs:16`), только чтение (`ExecuteReader`/`ExecuteScalar`), метода
`ExecuteNonQuery` нет.

| Слой | Где | Чего не хватает |
|---|---|---|
| Роли контекста | `DataContext/IDataContext.cs:17`, `DataContext/Roles/` | нет роли исполнителя мутаций |
| Команда | `Query/QueryCommand.cs:10` | нет типа утверждения и `INSERT`-полей |
| SQL | `DataContext/SqlBuilder.cs:33` | нет `MakeInsert`/`MakeUpdate`/`MakeDelete`/`MakeMerge` |
| Диалект | `DataContext/Dialect/ISqlDialect.cs:15`, `SqlDialectBase.cs:11` | нет `SupportsReturning`/`SupportsOutput`/identity |
| Метаданные | `DataContext/Meta/IPropertyMetadata.cs:12` | нет `IsKey`/`IsIdentity`/`IsComputed` |
| Исполнение | `DataContext/QueryExecutor.cs` | нет `ExecuteNonQuery` (affected rows) |
| In-memory | `DataContext/InMemoryDataContext.cs` | мутаций нет |

**Ключевой разрыв — метаданные.** Auto-маппинг (`Meta/EntityMetadataBuilder.cs:33-78`) знает только
`PropertyInfo` + `ColumnName`; ни ключа, ни identity, ни computed-колонок нет. Без этого невозможно
ни обновление/удаление «по сущности», ни возврат сгенерированного ключа.

## Общий каркас DML (вводится здесь)

### 1. Метаданные: ключ / identity / computed

Расширяется `IPropertyMetadata` (`Meta/IPropertyMetadata.cs`) и `PropertyMetadata`
(`Meta/Implementation/PropertyMetadata.cs`):

```csharp
public interface IPropertyMetadata
{
    PropertyInfo PropertyInfo { get; }
    string ColumnName { get; }
    bool IsKey { get; }        // участвует в WHERE для update/delete по сущности
    bool IsIdentity { get; }   // значение генерирует БД; не входит в INSERT-значения
    bool IsComputed { get; }   // генерируется БД; исключается из INSERT/UPDATE
}
```

Декларация — атрибутами (для POCO) и флюентно (для интерфейсов):

- `[Key]` (`System.ComponentModel.DataAnnotations`) и
  `[DatabaseGenerated(DatabaseGeneratedOption.Identity | Computed)]`;
- `EntityPropertyBuilder<T>.Key()`, `.Identity()`, `.Computed()` (`Meta/EntityPropertyBuilder.cs`).

`EntityMetadataBuilder<T>.Build/AutoBuildProperties` (`Meta/EntityMetadataBuilder.cs:16,33`)
заполняют флаги; `EntityMetadata` получает удобные `Keys`/`Identity`.
**Соглашение при отсутствии ключа:** свойство `Id` или `<TypeName>Id`; иначе update/delete
по сущности бросают `InvalidOperationException` с подсказкой объявить `.Key()`.
Флаги — prerequisite для всего DML; при первом изменении `IPropertyMetadata` — это breaking
изменение публичного интерфейса (см. `docs/specs/design/API-NAMING-REVIEW.md`).

### 2. Ось команд

Вводится `enum SqlStatementType { Select, Insert, Update, Delete, Merge }` и параллельная
`QueryCommand` иерархия `MutationCommand` (namespace `NextORM.Core`, папка
`Query/Mutations/`):

```
MutationCommand (abstract)
├── InsertCommand<TEntity>
├── UpdateCommand<TEntity>
├── DeleteCommand<TEntity>
└── MergeCommand<TEntity>
```

`MutationCommand` хранит: `StatementType`, `IDataContext`, целевой `FromExpression`/`IEntityMetadata`,
список присваиваний/значений ( параметры типа `Expression`), `WhereExpression?`, набор
`SqlFunctions.Parameter`-плейсхолдеров и хэш плана. Условия/выражения рендерятся существующими
визиторами через `SqlBuildContext.CreateWhereVisitor/CreateColumnVisitor`
(`DataContext/SqlBuildContext.cs:36,39`) — новый транслятор выражений не требуется.

**Почему отдельная ось, а не поле в `QueryCommand`.** `QueryCommand<TResult>` завязан на
материализацию результата (`ResultPlanHash`, компилируемый mapper, `ResultSetEnumerator`).
DML нужен `ExecuteNonQuery` и опциональный `RETURNING`/`OUTPUT`. Расширение `QueryCommand`
протащило бы тип утверждения через горячий SELECT-путь и `SqlBuilder.MakeSelect`, что рискует
бенчмарками. Отдельная ось изолирует риск (тот же приём, что и `ITransactionManager` вне
`IDataContext`). Альтернатива (дискриминатор в `QueryCommand`) — открытый вопрос №1.

### 3. Рендер SQL

Новый внутренний сборщик `SqlMutationBuilder` (рядом с `SqlBuilder`) с точками входа
`MakeInsert`/`MakeUpdate`/`MakeDelete`/`MakeMerge`, использующий `SqlBuildContext` и
`SqlSourceRenderer` для колонок и условий. Провайдерно-специфичные хвосты — через хуки диалекта
(`ISqlDialect` + `SqlDialectBase`, base возвращает `false`/бросает):

```
bool SupportsReturning { get; }        // PostgreSQL, SQLite (3.35+)
bool SupportsOutput { get; }           // SQL Server
bool SupportsLastInsertId { get; }     // MySQL/MariaDB
string MakeReturning(IReadOnlyList<string> columns);
string MakeOutput(IReadOnlyList<string> columns);
string MakeLastInsertId();
```

`Supports*` по умолчанию `false` в `SqlDialectBase` (`DataContext/Dialect/SqlDialectBase.cs:20-41`)
— тот же паттерн, что `SupportsRollup`/`SupportsApply`.

### 4. Исполнение: роль `IMutationExecutor`

Новая роль `DataContext/Roles/IMutationExecutor.cs` по образцу `IQueryExecutor`, реализуется
`DataContext` (делегирует в `QueryExecutor`), но **не** входит в `IDataContext` — как
`ITransactionManager`, чтобы in-memory не обязывали к no-op:

```csharp
public interface IMutationExecutor
{
    int ExecuteNonQuery(IMutationCommand command, ReadOnlySpan<object?> parameters);
    Task<int> ExecuteNonQueryAsync(IMutationCommand command, object[]? parameters, CancellationToken cancellationToken);
    TResult? ExecuteScalar<TResult>(IMutationCommand command, ReadOnlySpan<object?> parameters);
}
```

`QueryExecutor` получает `ExecuteNonQuery` (через `DbCommand.ExecuteNonQuery`) и общий
`DbPreparedMutationCommand` (параллель `DbPreparedQueryCommand`,
`DataContext/Cache/DbPreparedQueryCommand.cs:41`) с тем же биндингом параметров.
Входные точки-расширения (`DataContextExtensions` или `MutationExtensions`) делают приведение к
роли и бросают понятную ошибку, если контекст роль не реализует.

### 5. План-кэш, параметризация, транзакции

- `Prepare()` и кэш планов распространяются на мутации: SQL параметризуется через существующий
  `IParameterProvider`/`NormParam` (`Query/DefaultParameterProvider.cs:8`,
  `DataContext/NormParam.cs:12`); значения-константы становятся параметрами, `x => x.Col` —
  ссылкой на колонку.
- `MutationCommand` привязывает `DbCommand.Transaction` так же, как SELECT после
  [todo_transactions.md](todo_transactions.md) (`Func<DbTransaction?>` в исполнитель).
  #3/#4/#5 и #32 идут в одном milestone `1.0-a.4`; при отсутствии транзакции путь не должен
  добавлять боксинга/замыканий.
- Логирование — существующий `CommandLogger`; в debug-логе дополнительно число затронутых строк.

## Дизайн: публичный API `INSERT`

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) значения по колонкам
var n = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Id, 1)
    .Value(x => x.Name, "a")
    .Insert();

// (B) сущность (identity/computed колонки исключаются автоматически)
var n2 = ctx.InsertInto<ISimpleEntity>().Values(entity).Insert();

// (C) многострочно
ctx.InsertInto<ISimpleEntity>().Values(new[] { e1, e2, e3 }).Insert();

// (D) сгенерированный ключ
long id = ctx.InsertInto<ISimpleEntity>()
    .Value(x => x.Name, "a")
    .InsertWithIdentity(x => x.Id);

// async-варианты — по общему соглашению: InsertAsync/InsertWithIdentityAsync(ct)
```

Черновые сигнатуры билдера:

```csharp
public sealed class InsertBuilder<TEntity>
{
    public InsertBuilder<TEntity> Value<TValue>(Expression<Func<TEntity, TValue>> column, TValue value);
    public InsertBuilder<TEntity> Value<TValue>(Expression<Func<TEntity, TValue>> column, Expression<Func<TEntity, TValue>> value);
    public InsertBuilder<TEntity> Values(TEntity entity);
    public InsertBuilder<TEntity> Values(IEnumerable<TEntity> entities);
    public InsertBuilder<TEntity> Returning<TValue>(Expression<Func<TEntity, TValue>> column);

    public int Insert();
    public Task<int> InsertAsync(CancellationToken cancellationToken = default);
    public TKey InsertWithIdentity<TKey>(Expression<Func<TEntity, TKey>> keySelector);
    public Task<TKey> InsertWithIdentityAsync<TKey>(Expression<Func<TEntity, TKey>> keySelector, CancellationToken cancellationToken = default);
    public InsertBuilder<TEntity> Prepare();
}
```

`Returning` — заготовка под issue #15 (материализация строк), в фазе 1 достаточно
`InsertWithIdentity`. Имена SQL-ориентированы (`Into`/`Value`/`Returning`), совместимы с linq2db
(`InsertWithIdentity`) и ADO.NET.

## Провайдерная матрица

| Провайдер | `INSERT ... VALUES` | Сгенерированный ключ | Комментарий |
|---|---|---|---|
| SQLite | да | `RETURNING` (SQLite 3.35+) и `last_insert_rowid()` | `MakeReturning` |
| PostgreSQL | да | `RETURNING` | `MakeReturning` |
| SQL Server | да | `OUTPUT inserted.<col>` / `SCOPE_IDENTITY()` | `MakeOutput` |
| MySQL | да | `LAST_INSERT_ID()` (только auto-inc) | общего `RETURNING` нет |
| MariaDB | да | `RETURNING` (10.5+) / `LAST_INSERT_ID()` | наследует MySQL-диалект |
| ClickHouse | да, малые батчи | нет | массовая запись — бинарный API драйвера (`docs/providers/clickhouse.md:121`), вне билдера |
| In-memory | Фаза 2 | — | Фаза 1 — `NotSupportedException` (роль не реализована) |

Лимиты строк в одном `VALUES` (SQL Server 1000, SQLite ~500 параметров, PostgreSQL 65535) —
билдер разбивает батч на чанки; массовый `BulkCopy`/binary insert — вне области.

## Ограничения и цена

- **Нет change tracking / `SaveChanges`** — только явные команды; подключение к контексту/транзакции
  через роли (#32).
- **Нет `INSERT ... SELECT`, upsert и default-values** в фазе 1; `ON CONFLICT`/`ON DUPLICATE KEY` —
  предмет [todo_merge.md](todo_merge.md).
- **ClickHouse**: транзакций нет, `RETURNING` нет; вставка строк через `VALUES` — только малые
  батчи, крупные — бинарным API.
- **Публичный API расширяется**: новый интерфейс роли, билдер и флаги метаданных; обновление
  `API-NAMING-REVIEW.md` и `PublicAPI.*` (когда заморозят поверхность).
- **Метаданные — breaking change** для `IPropertyMetadata` (новые члены). На время
  `dotnet build` 0/0 и покрытие не ниже базового (84.9% line / 73.2% branch).

## Этапы внедрения

- **Фаза 1 (MVP, 1.0-a.4):** метаданные ключ/identity/computed, ось `MutationCommand`,
  `SqlMutationBuilder.MakeInsert`, роль `IMutationExecutor` + `ExecuteNonQuery`, `InsertInto`
  (значения/сущность/батч), `InsertWithIdentity`, ClickHouse/in-memory — `NotSupportedException`.
- **Фаза 2:** `Returning`-материализация (issue #15), `INSERT ... SELECT`, `ON CONFLICT`/upsert
  (совместно с merge), in-memory мутации.
- **Вне области:** change tracking, `SaveChanges`, bulk copy, временные таблицы.

## План тестов

- Core (`tests/nextorm.core.tests/`): определение ключа (`Id`, `<Type>Id`, `.Key()`), флаги
  identity/computed в `IPropertyMetadata`; повторный `Prepare` не пересобирает SQL; in-memory
  бросает `NotSupportedException`.
- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`): колонки,
  параметры, экранирование имён, многострочный `VALUES`, `RETURNING`/`OUTPUT`/`LAST_INSERT_ID`.
- Диалекты (`*DialectTests.cs`): значения `SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`.
- Интеграция (`tests/nextorm.integration.tests/`, `CommonTestSuite.Insert.cs`): вставка значений,
  сущности, батча; возврат ключа; текст/число/null; повторный запуск из плана.
- ClickHouse: `INSERT ... VALUES` проходит, `InsertWithIdentity` — `NotSupportedException`.
- Покрытие: база 84.9% line / 73.2% branch; `coverage.settings.xml` включает
  `nextorm.{core,sqlite,postgres,sqlserver}` — новые файлы core учитываются.

## Открытые вопросы

1. Отдельная ось `MutationCommand` vs дискриминатор `SqlStatementType` в `QueryCommand`
   (рекомендация — отдельная ось, чтобы не трогать SELECT-путь).
2. `IMutationExecutor` вне `IDataContext` (рекомендация) vs в составе фасада с реализацией
   in-memory в фазе 1.
3. Один билдер `InsertBuilder<T>` vs перегрузка `From<T>().Insert(...)`; SQL-ориентированные
   `InsertInto`/`Update`/`DeleteFrom`/`MergeInto` выглядят последовательнее.
4. `InsertWithIdentity<TKey>(selector)` vs linq2db-стиль `InsertWithIdentity()` (возврат `object`)
   и `InsertWithInt32Identity`.
5. Возврат числа строк vs `void` для провайдеров без affected-rows (ClickHouse) — рекомендация:
   `int`, а неподдерживаемое бросает.
6. Нужен ли `.Prepare()` на мутациях в фазе 1 или достаточно неявного план-кэша.

## Файлы к изменению

- Новое: `src/nextorm.core/Query/Mutations/{MutationCommand,InsertCommand,UpdateCommand,DeleteCommand,MergeCommand}.cs`,
  `src/nextorm.core/Query/SqlStatementType.cs`, `src/nextorm.core/DataContext/SqlMutationBuilder.cs`,
  `src/nextorm.core/DataContext/Roles/IMutationExecutor.cs`,
  `src/nextorm.core/DataContext/Cache/DbPreparedMutationCommand.cs`,
  `src/nextorm.core/Builders/InsertBuilder.cs`.
- Правки: `DataContext/Meta/{IPropertyMetadata,EntityMetadataBuilder,EntityPropertyBuilder}.cs`,
  `DataContext/Meta/Implementation/PropertyMetadata.cs`, `DataContext/Dialect/ISqlDialect.cs`,
  `DataContext/Dialect/SqlDialectBase.cs`, провайдерные диалекты, `DataContext/QueryExecutor.cs`,
  `DataContext/DataContext.cs`, `DataContextExtensions.cs`, `DataContext/InMemoryDataContext.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql}.tests/`, `CommonTestSuite.Insert.cs`,
  `*SpecificTests.cs`.
- Документация: `docs/guide/19-data-modification.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
