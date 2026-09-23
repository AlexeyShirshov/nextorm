# TODO: Удаление данных (`DELETE`)

> Рабочий план (design RFC). Источник: GitHub issue
> [#5 «TODO: Delete»](https://github.com/AlexeyShirshov/nextorm/issues/5), milestone `1.0-a.4`.
> Общий каркас DML (метаданные, ось `MutationCommand`, `SqlMutationBuilder`, роль
> `IMutationExecutor`, план-кэш, транзакции) описан в [todo_insert.md](todo_insert.md).

## Пункт и цель

- Фича: `DELETE` через билдер — удаление по предикату и удаление сущности по объявленному ключу.
- Критерий приёмки: `ctx.DeleteFrom<T>().Where(...).Delete()` рендерит параметризованный
  `DELETE FROM ... WHERE ...` на SQLite/PostgreSQL/SQL Server/MySQL/MariaDB и возвращает число
  затронутых строк; ClickHouse и in-memory отклоняют то, что не умеют, явным исключением.
- **Change tracking отсутствует**: удаление выполняется по заданному условию/ключу, а не по
  отслеживаемому состоянию.

## Почему это нужно (мотивация)

1. **Замыкает DML.** Вместе с `INSERT` (#3) и `UPDATE` (#4) закрывает issue-трекер
   `comparison/linq2db-comparison.md:50` / `sql-capabilities-gap-analysis.md:132`.
2. **Удаление по ключу.** Типовой сценарий «удалить сущность» должен рендериться в
   `DELETE ... WHERE <pk> = @p`, используя метаданные ключа из #3.
3. **Симметрия фильтра.** Предикат `WHERE` — тот же транслятор и та же параметризация, что в
   SELECT/UPDATE; отдельного движка не требуется.

## Текущее состояние и разрыв

Специфичного для `DELETE` в движке нет; общий разрыв перечислен в
[todo_insert.md](todo_insert.md#текущее-состояние-и-разрыв). Дополнительно:

| Слой | Чего не хватает |
|---|---|
| Команда | `DeleteCommand<TEntity>`: цель + `WhereExpression?` |
| Рендер | `SqlMutationBuilder.MakeDelete`: `DELETE FROM`, `WHERE`, диалектные `RETURNING`/`OUTPUT` |
| Метаданные | ключ для формы по сущности (вводится в #3) |
| Безопасность | политика для `DELETE` без `WHERE` (удаление всех строк) |

## Дизайн: публичный API `DELETE`

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) по предикату
var n = ctx.DeleteFrom<ISimpleEntity>()
    .Where(x => x.Id == 1)
    .Delete();

// (B) по сущности — WHERE из объявленного ключа
ctx.Delete(entity).Delete();

// (C) вся таблица (осознанно)
ctx.DeleteFrom<ISimpleEntity>().Delete();

// async-вариант: DeleteAsync(ct)
```

Черновые сигнатуры:

```csharp
public sealed class DeleteBuilder<TEntity>
{
    public DeleteBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    public DeleteBuilder<TEntity> Returning<TValue>(Expression<Func<TEntity, TValue>> column);

    public int Delete();
    public Task<int> DeleteAsync(CancellationToken cancellationToken = default);
    public DeleteBuilder<TEntity> Prepare();
}

// входные точки (расширения над IDataContext)
public static DeleteBuilder<TEntity> DeleteFrom<TEntity>(this IDataContext ctx, Action<EntityMetadataBuilder<TEntity>>? config = null);
public static int Delete<TEntity>(this IDataContext ctx, TEntity entity);         // по ключу
public static Task<int> DeleteAsync<TEntity>(this IDataContext ctx, TEntity entity, CancellationToken ct = default);
```

`Where` необязателен — тогда удаляются все строки (как в linq2db/ADO.NET). Открытый вопрос №2 —
требовать явный `.All()`/подтверждение, чтобы не потерять данные случайно.

## Провайдерная матрица

| Провайдер | `DELETE ... WHERE` | `RETURNING` удалённых | `DELETE ... USING`/join | Комментарий |
|---|---|---|---|---|
| SQLite | да | `RETURNING` (3.35+) | нет (эмуляция подзапросом) | `MakeReturning` |
| PostgreSQL | да | `RETURNING` | да (`USING`) | `MakeReturning` |
| SQL Server | да | `OUTPUT deleted.<col>` | `DELETE ... FROM ... JOIN` | `MakeOutput` |
| MySQL | да | нет | multi-table `DELETE` | общего `RETURNING` нет |
| MariaDB | да | `RETURNING` (10.5+) | multi-table `DELETE` | наследует MySQL-диалект |
| ClickHouse | `ALTER TABLE ... DELETE` (mutation) | нет | нет | асинхронно, affected rows не возвращает; Фаза 2 |
| In-memory | Фаза 2 | — | — | Фаза 1 — `NotSupportedException` |

`DELETE ... USING`/join и `RETURNING`-материализация — Фаза 2 (issue #15). ClickHouse-мутации
асинхронны (`system.mutations`) и не возвращают синхронный счётчик — `NotSupportedException`
в фазе 1.

## Ограничения и цена

- **Нет soft delete / global query filters** — только физическое удаление.
- **`DELETE` без `WHERE`** удаляет всю таблицу; отдельного `TRUNCATE` нет (вне области).
- **Нет `RETURNING`-материализации в фазе 1** — заготовка `Returning` под issue #15.
- **Публичный API растёт** (билдер + входные точки) — обновить `API-NAMING-REVIEW.md`; при заморозке
  поверхности — `PublicAPI.*`.
- **Аллокации**: при отсутствии `RETURNING`/транзакции путь не должен добавлять боксинга; предикат
  готовится на этапе плана.

## Этапы внедрения

- **Фаза 1 (MVP, 1.0-a.4):** `DeleteCommand`, `MakeDelete` (`WHERE`), `Where`, форма `Delete(entity)`
  по ключу, `Delete()`/`DeleteAsync()`, `NotSupportedException` для ClickHouse/in-memory.
- **Фаза 2:** `RETURNING`/`OUTPUT` (issue #15), `DELETE ... USING`/join, ClickHouse-мутация,
  in-memory, `TRUNCATE` (если потребуется).
- **Вне области:** soft delete, каскады по relationship-метаданным (их в nextorm нет), change tracking.

## План тестов

- Core (`tests/nextorm.core.tests/`): ключ из #3 обязателен для `Delete(entity)`; отсутствие ключа —
  понятное исключение; in-memory — `NotSupportedException`.
- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`):
  `DELETE FROM`, параметризованный `WHERE`, отсутствие `WHERE`, `RETURNING`/`OUTPUT`, экранирование.
- Диалекты: `SupportsReturning`/`SupportsOutput`; ClickHouse-флаг мутаций (`false` в фазе 1).
- Интеграция (`CommonTestSuite.Delete.cs`): удаление по предикату, по сущности, ноль затронутых
  строк, удаление всех строк, повторное исполнение из плана.
- Покрытие: база 84.9% line / 73.2% branch; новые файлы core входят в `coverage.settings.xml`.

## Открытые вопросы

1. `DeleteFrom<T>()` vs `Delete<T>()` (конфликт с формой `Delete(entity)`) — рекомендация:
   `DeleteFrom<T>()` для предиката, `Delete(entity)` для сущности.
2. Разрешать ли `DELETE` без `Where` (как linq2db) или требовать `.All()`; рекомендация — требовать
   явный маркер, чтобы исключить случайную потерю данных.
3. Общая абстракция `Where`-предиката для update/delete (см. [todo_update.md](todo_update.md),
   открытый вопрос №4) — рекомендация: один общий `MutationFilter<TEntity>`.
4. Нужен ли `TRUNCATE` как отдельная эргономика (быстрее, без `WHERE`) или ограничиться `DELETE`.

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/DeleteBuilder.cs`, `src/nextorm.core/Query/Mutations/DeleteCommand.cs`.
- Правки: `src/nextorm.core/DataContext/SqlMutationBuilder.cs`, `DataContext/Roles/IMutationExecutor.cs`,
  `DataContext/QueryExecutor.cs`, `DataContext/DataContextExtensions.cs`,
  `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, провайдерные диалекты,
  `DataContext/InMemoryDataContext.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql}.tests/`,
  `tests/nextorm.integration.tests/CommonTestSuite.Delete.cs`, `*SpecificTests.cs`.
- Документация: `docs/guide/19-insert-statement.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
