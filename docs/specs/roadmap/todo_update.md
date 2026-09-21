# TODO: Обновление данных (`UPDATE`)

> Рабочий план (design RFC). Источник: GitHub issue
> [#4 «TODO: Update»](https://github.com/AlexeyShirshov/nextorm/issues/4), milestone `1.0-a.4`.
> Общий каркас DML (метаданные, ось `MutationCommand`, `SqlMutationBuilder`, роль
> `IMutationExecutor`, план-кэш, транзакции) описан в [todo_insert.md](todo_insert.md).

## Пункт и цель

- Фича: `UPDATE` через билдер — присваивания по колонкам (константа/выражение), фильтр `WHERE` и
  обновление сущности по объявленному ключу.
- Критерий приёмки: `ctx.Update<T>().Set(...).Where(...).Update()` рендерит параметризованный
  `UPDATE ... SET ... WHERE ...` на SQLite/PostgreSQL/SQL Server/MySQL/MariaDB и возвращает число
  затронутых строк; ClickHouse и in-memory отклоняют то, что не умеют, явным исключением.
- **Change tracking отсутствует**: обновляется ровно то, что указано в `Set`/сущности, без
  отслеживания изменённых свойств и без `SaveChanges`.

## Почему это нужно (мотивация)

1. **Симметрия с `INSERT`.** После #3 запись остаётся неполной без update; `comparison/linq2db-comparison.md:50`
   и `sql-capabilities-gap-analysis.md:132` перечисляют DML единым блоком.
2. **Обновление по ключу.** Типовой сценарий «загрузил сущность — изменил — сохранил» должен
   рендериться в `UPDATE ... WHERE <pk> = @p`, используя метаданные ключа из #3.
3. **Выражения справа.** `SET col = col + 1`, `SET updated = created` должны переиспользовать
   существующий транслятор выражений, а не вводить второй.

## Текущее состояние и разрыв

Специфичного для `UPDATE` в движке нет; всё, чего не хватает, перечислено в
[todo_insert.md](todo_insert.md#текущее-состояние-и-разрыв). Дополнительно к общему каркасу
требуется:

| Слой | Чего не хватает |
|---|---|
| Команда | `UpdateCommand<TEntity>` со списком присваиваний `(column, value-expression)` |
| Рендер | `SqlMutationBuilder.MakeUpdate`: `SET`-список, `WHERE`, диалектные `RETURNING`/`OUTPUT` |
| Метаданные | ключ для формы по сущности (вводится в #3); `IsComputed`/`IsIdentity` исключаются из `SET` |
| Безопасность | политика для `UPDATE` без `WHERE` (обновление всех строк) |

## Дизайн: публичный API `UPDATE`

```csharp
using var ctx = new DataContextBuilder().UsePostgres(connectionString).CreateDataContext();

// (A) присваивания по колонкам
var n = ctx.Update<ISimpleEntity>()
    .Set(x => x.Name, "a")
    .Set(x => x.UpdatedAt, x => x.CreatedAt)     // выражение по колонке
    .Where(x => x.Id == 1)
    .Update();

// (B) из сущности — обновляет все не-key/non-identity/non-computed колонки
ctx.Update<ISimpleEntity>().Set(entity).Where(x => x.Id == entity.Id).Update();

// (C) по сущности — WHERE строится из объявленного ключа
ctx.Update(entity).Update();

// async-вариант: UpdateAsync(ct)
```

Черновые сигнатуры:

```csharp
public sealed class UpdateBuilder<TEntity>
{
    public UpdateBuilder<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> column, TValue value);
    public UpdateBuilder<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> column, Expression<Func<TEntity, TValue>> value);
    public UpdateBuilder<TEntity> Set(TEntity entity);
    public UpdateBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    public UpdateBuilder<TEntity> Returning<TValue>(Expression<Func<TEntity, TValue>> column);

    public int Update();
    public Task<int> UpdateAsync(CancellationToken cancellationToken = default);
    public UpdateBuilder<TEntity> Prepare();
}

// входные точки (расширения над IDataContext)
public static UpdateBuilder<TEntity> Update<TEntity>(this IDataContext ctx, Action<EntityMetadataBuilder<TEntity>>? config = null);
public static int Update<TEntity>(this IDataContext ctx, TEntity entity);         // по ключу
public static Task<int> UpdateAsync<TEntity>(this IDataContext ctx, TEntity entity, CancellationToken ct = default);
```

`Where` можно не задавать — тогда обновляются все строки (как в linq2db/ADO.NET). Это осознанное
решение; см. открытый вопрос №2 (опция «требовать предикат»/`.All()`).

## Провайдерная матрица

| Провайдер | `UPDATE ... SET ... WHERE` | `RETURNING` обновлённых | `UPDATE ... FROM` | Комментарий |
|---|---|---|---|---|
| SQLite | да | `RETURNING` (3.35+) | да (3.33+) | `MakeReturning` |
| PostgreSQL | да | `RETURNING` | да (`FROM`) | `MakeReturning` |
| SQL Server | да | `OUTPUT inserted.<col>` | да (`FROM`-join) | `MakeOutput` |
| MySQL | да | нет | multi-table `UPDATE` | общего `RETURNING` нет |
| MariaDB | да | `RETURNING` (10.5+) | multi-table `UPDATE` | наследует MySQL-диалект |
| ClickHouse | `ALTER TABLE ... UPDATE` (mutation) | нет | нет | асинхронно, affected rows не возвращает; Фаза 2 |
| In-memory | Фаза 2 | — | — | Фаза 1 — `NotSupportedException` |

`UPDATE ... FROM`/multi-table (соединение целевой таблицы с источником) — Фаза 2, чтобы не смешивать
с `INSERT`. ClickHouse-мутации асинхронны (`system.mutations`), поэтому в фазе 1 —
`NotSupportedException`; если поддержка нужна, то отдельной фазой с документированной семантикой.

## Ограничения и цена

- **Нет change tracking.** `Set(entity)` пишет все не-key/non-identity/non-computed колонки; это
  полный `UPDATE`, а не «только изменённые поля».
- **Нет `RETURNING`-материализации в фазе 1** — заготовка `Returning` под issue #15.
- **`UPDATE` без `WHERE`** обновляет всю таблицу; политика безопасности — открытый вопрос.
- **Публичный API растёт** (билдер + входные точки) — обновить `API-NAMING-REVIEW.md`; при заморозке
  поверхности — `PublicAPI.*`.
- **Аллокации**: при отсутствии `RETURNING`/транзакции путь не должен добавлять боксинга; `SET`
  рендерится на этапе плана, значения — параметры.

## Этапы внедрения

- **Фаза 1 (MVP, 1.0-a.4):** `UpdateCommand`, `MakeUpdate` (`SET`+`WHERE`), `Set` (значение/
  выражение/сущность), `Where`, `Update()`/`UpdateAsync()`, форма `Update(entity)` по ключу,
  `NotSupportedException` для ClickHouse/in-memory.
- **Фаза 2:** `RETURNING`/`OUTPUT` (issue #15), `UPDATE ... FROM`/multi-table, ClickHouse-мутация,
  in-memory, `Prepare()`-эргономика.
- **Вне области:** change tracking, optimistic concurrency (`rowversion`), global query filters.

## План тестов

- Core (`tests/nextorm.core.tests/`): ключ из #3 обязателен для `Update(entity)`; `Set(entity)`
  исключает identity/computed; отсутствие ключа — понятное исключение; in-memory —
  `NotSupportedException`.
- SQL-gen (`tests/nextorm.{sqlite,postgres,sqlserver,mysql}.tests/SqlGenerationTests.cs`):
  `SET`-список и порядок, параметризация констант, ссылки на колонки в RHS, `WHERE`, отсутствие
  `WHERE`, `RETURNING`/`OUTPUT`.
- Диалекты: `SupportsReturning`/`SupportsOutput`; ClickHouse-флаг мутаций (`false` в фазе 1).
- Интеграция (`CommonTestSuite.Update.cs`): обновление по колонке, выражение `x = x + 1`, по
  сущности, ноль затронутых строк, повторное исполнение из плана.
- Покрытие: база 84.9% line / 73.2% branch; новые файлы core входят в `coverage.settings.xml`.

## Открытые вопросы

1. `ctx.Update<T>()` vs `From<T>().Update()` — рекомендация: SQL-ориентированная входная точка.
2. Разрешать ли `UPDATE` без `Where` (рекомендация: да, как в linq2db) или требовать `.All()`
   для явного «обновить всё».
3. Форма `Update(entity)` возвращает `int` или саму сущность (chaining/возврат ключа)?
4. Общая абстракция `Where`-предиката для update/delete (общий билдер `MutationFilter`) или
   дублирование мелкого `Where` в каждом билдере.
5. Нужен ли `Set` по имени колонки строкой в фазе 1 (сейчас — только выражения).

## Файлы к изменению

- Новое: `src/nextorm.core/Builders/UpdateBuilder.cs`, `src/nextorm.core/Query/Mutations/UpdateCommand.cs`.
- Правки: `src/nextorm.core/DataContext/SqlMutationBuilder.cs`, `DataContext/Roles/IMutationExecutor.cs`,
  `DataContext/QueryExecutor.cs`, `DataContext/DataContextExtensions.cs`,
  `DataContext/Dialect/{ISqlDialect,SqlDialectBase}.cs`, провайдерные диалекты,
  `DataContext/InMemoryDataContext.cs`.
- Тесты: `tests/nextorm.{core,sqlite,postgres,sqlserver,mysql}.tests/`,
  `tests/nextorm.integration.tests/CommonTestSuite.Update.cs`, `*SpecificTests.cs`.
- Документация: `docs/guide/19-data-modification.md` (+RU), `docs/providers/*` (+RU),
  `docs/advanced/limitations.md` (+RU), `docs/advanced/api-reference.md` (+RU),
  `docs/specs/comparison/linq2db-comparison.md` (+RU),
  `docs/specs/roadmap/sql-capabilities-gap-analysis.md`, `docs/specs/design/API-NAMING-REVIEW.md`.
