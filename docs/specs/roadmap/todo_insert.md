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

## Провайдерная матрица (шаг 1: провайдер × форма)

Матрица заполнена по документации самих провайдеров, а не по коду nextorm; колонка «Источник» —
что именно проверялось.

| Провайдер | `INSERT ... VALUES` | Возврат сгенерированного ключа (нативная форма) | Транзакции | Источник |
|---|---|---|---|---|
| SQLite | да | `INSERT ... RETURNING col` (3.35.0+); также `last_insert_rowid()` | да | SQLite docs: `RETURNING` (lang_returning), `last_insert_rowid()` (lang_corefunc) |
| PostgreSQL | да | `INSERT ... RETURNING col` | да | PostgreSQL docs: `INSERT` (sql-insert, `RETURNING`), `currval`/`RETURNING` |
| SQL Server | да | `OUTPUT inserted.col`; `SCOPE_IDENTITY()`/`@@IDENTITY` | да | Microsoft Learn: `INSERT (Transact-SQL)` (OUTPUT), `SCOPE_IDENTITY`, `OUTPUT clause` |
| MySQL | да | `LAST_INSERT_ID()` (auto-increment; общий `RETURNING` отсутствует) | да | MySQL docs: `INSERT ... RETURNING` отсутствует; `LAST_INSERT_ID()` |
| MariaDB | да | `INSERT ... RETURNING` (10.5.0+); `LAST_INSERT_ID()` | да | MariaDB docs: `INSERT ... RETURNING`, `LAST_INSERT_ID()` |
| ClickHouse | да (малые батчи) | нет | нет | ClickHouse docs: `INSERT INTO`, отсутствие `RETURNING`; массовая запись — бинарный API драйвера (`docs/providers/clickhouse.md`) |
| In-memory | — (нет SQL) | — (это CLR-контекст без БД) | — | это не `ISqlDialect`-провайдер; отказ через роль `IMutationExecutor` |

**Единообразие провайдеров (решение шага 1).** `INSERT ... VALUES` выразим на всех SQL-провайдерах,
поэтому отдельная поверхность `PostgresFunctions`/… не заводится: одно кросс-провайдерное API
(`InsertInto<T>()`), а нативное написание `VALUES` одинаково у всех. Возврат ключа различается, поэтому
он делегирован трём `Supports*`-флагам с одним `Make*`-хуком на каждый:
`SupportsReturning`/`MakeReturning` (SQLite, PostgreSQL — но MariaDB сознательно оставлена на MySQL-диалекте
и пути `LAST_INSERT_ID`, чтобы не плодить четвёртую ветку), `SupportsOutput`/`MakeOutput` (SQL Server) и
`SupportsLastInsertId`/`MakeLastInsertId` (MySQL/MariaDB). ClickHouse не выражает ни одной из трёх форм →
`InsertWithIdentity` бросает `NotSupportedException`, обычный `Insert` работает. In-memory — не диалект, а
отдельный контекст; роль `IMutationExecutor` им не реализуется, поэтому и `Insert`, и `InsertWithIdentity`
отклоняются с явным сообщением (без per-function флага — обоснование: у in-memory нет серверной БД, которую
можно менять).

| Провайдер | `SupportsReturning` | `SupportsOutput` | `SupportsLastInsertId` | `MakeReturning`/`MakeOutput`/`MakeLastInsertId` |
|---|---|---|---|---|
| SQLite | `true` | `false` | `true` (резерв) | наследует ANSI `returning <col>`; `select last_insert_rowid()` |
| PostgreSQL | `true` | `false` | `false` | ANSI `returning <col>` |
| SQL Server | `false` | `true` | `false` | `output inserted.<col>` |
| MySQL | `false` | `false` | `true` | `select last_insert_id()` |
| MariaDB | `false` | `false` | `true` (наследует MySQL) | `select last_insert_id()` |
| ClickHouse | `false` | `false` | `false` | — (ключ не возвращается) |

Лимиты строк в одном `VALUES` (SQL Server 1000, SQLite ~500 параметров, PostgreSQL 65535) —
в фазе 1 батч **не** разбивается на чанки (документировано как ограничение); массовый
`BulkCopy`/binary insert — отдельный план [todo_bulk_insert.md](todo_bulk_insert.md).

## Статус фаз

- **Фаза 1 (MVP) — реализована в этом изменении.** Флаги метаданных
  `IsKey`/`IsIdentity`/`IsComputed` (атрибуты `[Key]`/`[DatabaseGenerated]` и флюентные
  `.Key()`/`.Identity()`/`.Computed()`), ось `MutationCommand`/`InsertCommand`,
  `SqlMutationBuilder.MakeInsert`, роль `IMutationExecutor` + `QueryExecutor.ExecuteNonQuery`,
  `InsertInto<T>()` (значения/сущность/батч), `InsertWithIdentity`, хуки
  `SupportsReturning`/`SupportsOutput`/`SupportsLastInsertId`. `Prepare()` в фазе 1 **не** реализован
  (открытый вопрос 6); ClickHouse отклоняет только `InsertWithIdentity`, in-memory — и `Insert`, и
  `InsertWithIdentity`.
- **Фаза 2 (частично, issue #15):** `Returning`-материализация — **Done** (`Returning()`/
  `Returning(projection)` → `InsertReturningBuilder.Single/ToList`, PostgreSQL/SQLite/SQL Server; см.
  [guide 19](../guide/19-insert-statement.md)); `INSERT ... SELECT` — **Done** (см. ниже); key upsert —
  **Done** ([todo_merge.md](todo_merge.md), Фаза 1); остаются полный `MERGE` с ветками, in-memory мутации, чанкинг батча.
- **`INSERT ... SELECT` (реализовано, 23.09.2026).** Второй источник строк — серверный запрос:
  тот же overload `Values<TSource,TResult>(EntityBuilder<TSource> source, Expression<Func<TSource,TResult>> mapping)`
  (источник `IEnumerable` пишет на клиенте, `EntityBuilder` — рендерит `INSERT ... SELECT`). Целевые
  колонки — по именам членов проекции (как у клиентского батча); `computed`/`SqlDefault` запрещены;
  форма взаимоисключающая с прочими. Рендер: колонки → `OUTPUT` (SQL Server, перед `SELECT`) →
  текст `SELECT` → `RETURNING` (PostgreSQL/SQLite); параметры источника вливаются в команду. Источник
  с CTE (`With`) **поддерживается** — его `WITH` поднимается перед `INSERT`
  (`with c as (...) insert into ... select ... from c`), а рантайм-`SqlFunctions.Parameter` отклоняется
  (`NotSupportedException`). Реализация:
  `InsertBuilder` (`_source`/`_selectColumns`), `InsertCommand.Source`/`SourceColumns`,
  `SqlMutationBuilder` (`RenderSourceInsert`), `QueryPlanner.RenderSource`, приватный
  `DataContext.BuildInsertSql`. Детали и провайдерная матрица были в рабочем плане
  `todo_insert_select.md` (удалён после переноса выводов сюда в docs).
- **Модифицирующие CTE (реализовано, 23.09.2026).** Postgres-only (`ISqlDialect.SupportsDataModifyingCtes`,
  остальные бросают `NotSupportedException`): `ctx.With(name, insert.Returning(...))` и
  `CteQuery.With<TEntity,TResult>(name, insert)` → `MutationCteQuery<TResult>`; `From(name)` читает
  `RETURNING`-проекцию типизированно (полный набор операторов), `FromTable(name)` — соседний read-CTE,
  `InsertInto<T>().Values(mutationCte.From(name), mapping)` — главный `INSERT ... SELECT` (его `WITH`
  поднят перед `INSERT`), а тело CTE принимает и `VALUES`, и `INSERT ... SELECT` (в т.ч. читающий более
  ранний read-CTE). План не кэшируется (`Cache=false`). Детали были в `todo_insert_cte.md` (удалён после
  переноса выводов сюда и в guide 09).
- **API генерации ключа (переименовано, 22.09.2026).** Плоский `InsertWithIdentity<TKey>(selector)` →
  `TKey` заменён на builder-формы: `ReturningIdentity<TKey>(selector)` (колонка через `RETURNING`/`OUTPUT`,
  фолбэк `LAST_INSERT_ID`), `ReturningIdentity<TKey>()` (identity-функция провайдера: `last_insert_rowid()`,
  `lastval()`, `SCOPE_IDENTITY()`, `LAST_INSERT_ID()`; новый хук `ISqlDialect.SupportsIdentityFunction`/
  `MakeIdentityFunction`) и `ReturningKey<TKey>()` (ключ из метаданных). Все читаются через
  `InsertReturningBuilder.Single()/ToList()/SingleAsync()/ToListAsync()/ToSql()`. Обновления ниже в этом
  файле (RFC-скетч) сохранены как исторические.
- **Батч значений из источника (реализовано, 23.09.2026).** Кроме entity-батча добавлены формы ввода в
  `InsertBuilder<TEntity>` (пишут в те же `Columns[c].Values[r]` + `RowCount`, рендер не менялся):
  - `Values<TSource,TResult>(IEnumerable<TSource> source, Expression<Func<TSource,TResult>> mapping)` —
    источник + проекция: concrete → member-init `new Order { Id = d.Id, Name = d.Name }`, interface →
    анонимка с маппингом `new { Id = d.Id, Name = d.Name }`. Имена членов матчатся по **имени свойства**
    `TEntity`; `computed` запрещён на всех путях записи (mapping/`Value`/`Values(column, …)`), `identity`
    можно указать явно; значения биндятся параметрами.
  - `Value<TScalar>(TScalar value)` и `Values<TScalar>(IEnumerable<TScalar> values)` — скалярный инсерт
    (одно/несколько значений) без лямбды; колонка выводится как **единственная записываемая**
    (`!IsIdentity && !IsComputed`); иначе `InvalidOperationException`.
  - `Values<TValue>(Expression<Func<TEntity,TValue>> column, IEnumerable<TValue> values)` — колоночный
    escape hatch для сущностей с несколькими колонками.
  Формы взаимоисключающие (явный `ValueMode`), длины колонок валидируются в `BuildCommand`; `Single()`
  на батче кидает по `RowCount > 1`. Источник — только `IEnumerable` (клиентский `VALUES`);
  `IQueryable` = серверный `INSERT … SELECT` остаётся в фазе 2. Провайдеры: `INSERT … VALUES (...),(...)`
  поддержан всеми SQL-диалектами; лимиты параметров/чанкинг не решаются здесь.
- **`DEFAULT VALUES` и `DEFAULT`-значение (реализовано, 23.09.2026).** Строка целиком из дефолтов (все
  колонки generated) вставляется без значений: `InsertInto<T>().Insert()` рендерит `DEFAULT VALUES`
  (PostgreSQL/SQL Server/SQLite) или `() VALUES ()` (MySQL/MariaDB); ClickHouse отклоняет. Отдельную
  колонку можно записать как дефолт через `SqlDefault.Value` (`Value(x => x.Col, SqlDefault.Value)` или
  член проекции `Values(source, mapping)`); SQLite/ClickHouse не выражают `DEFAULT` в `VALUES` →
  `NotSupportedException` (в SQLite вместо этого колонку опускают). Хуки
  `ISqlDialect.SupportsDefaultValues`/`UsesEmptyColumnListForDefaults`/`SupportsColumnDefault`; новый
  публичный тип `SqlDefault`.
- **Вне области:** change tracking, `SaveChanges`, bulk copy (см. [todo_bulk_insert.md](todo_bulk_insert.md)); временные таблицы вынесены отдельно в [todo_create_table_as_select.md](todo_create_table_as_select.md).

## Ограничения и цена

- **Нет change tracking / `SaveChanges`** — только явные команды; подключение к контексту/транзакции
  через роли (#32).
- **Key upsert реализован** — `ON CONFLICT`/`ON DUPLICATE KEY`/`MERGE`, см. [todo_merge.md](todo_merge.md)
  (Фаза 1) и [Upsert (key merge)](../guide/19-insert-statement.md#upsert-key-merge). Полный `MERGE` с
  ветками остаётся предметом [todo_merge.md](todo_merge.md). `INSERT ... SELECT` и per-column
  `DEFAULT`/`DEFAULT VALUES` уже реализованы (см. «Статус фаз»).
- **ClickHouse**: транзакций нет, `RETURNING` нет; вставка строк через `VALUES` — только малые
  батчи, крупные — бинарным API.
- **Публичный API расширяется**: новый публичный тип `InsertBuilder<TEntity>`, метод
  `DataContextExtensions.InsertInto<T>`, DIM-члены `ISqlDialect.SupportsReturning`/`SupportsOutput`/
  `SupportsLastInsertId` (+ `Make*`) и `IPropertyMetadata.IsKey`/`IsIdentity`/`IsComputed`,
  флюентные `.Key()/.Identity()/.Computed()`; обновление `API-NAMING-REVIEW.md` и `PublicAPI.*`
  (когда заморозят поверхность).
- **Метаданные — НЕ breaking change.** `IsKey`/`IsIdentity`/`IsComputed` добавлены как default
  interface members (`=> false`), поэтому внешние реализации `IPropertyMetadata` продолжают
  компилироваться.
- **Проверка фазы 1:** `dotnet build nextorm.sln -c Release` — 0/0; `dotnet docfx docs/docfx.json` —
  0/0; полный прогон 2441 тестов (0 failed / 30 skipped) на SQLite/PostgreSQL/SQL Server/MySQL/
  ClickHouse; покрытие **85.4% line / 74.6% branch** (порог 75%), новые файлы
  `SqlMutationBuilder`/`MutationCommand`/`InsertCommand`/`InsertColumn`/`InsertValue` — 100%,
  `InsertBuilder<TEntity>` — 87.1%.

## Этапы внедрения

- **Фаза 1 (MVP, 1.0-a.4):** метаданные ключ/identity/computed, ось `MutationCommand`,
  `SqlMutationBuilder.MakeInsert`, роль `IMutationExecutor` + `ExecuteNonQuery`, `InsertInto`
  (значения/сущность/батч), `InsertWithIdentity`, ClickHouse/in-memory — `NotSupportedException`.
- **Фаза 2 (частично):** `Returning`-материализация (issue #15) — **Done**; `INSERT ... SELECT` — **Done**;
  модифицирующие CTE PostgreSQL — **Done**; key upsert — **Done** ([todo_merge.md](todo_merge.md), Фаза 1);
  остаются полный `MERGE` с ветками, in-memory мутации.
- **Вне области:** change tracking, `SaveChanges`, bulk copy (см. [todo_bulk_insert.md](todo_bulk_insert.md)); временные таблицы вынесены отдельно в [todo_create_table_as_select.md](todo_create_table_as_select.md).

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
- Документация: `docs/guide/19-insert-statement.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
