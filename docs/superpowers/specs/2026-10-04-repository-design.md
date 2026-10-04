# Repository API и ограниченное удаление в nextorm

- **Дата:** 2026-10-04
- **Статус:** все in-chat архитектурные секции дизайна одобрены пользователем; **письменная спецификация утверждена пользователем 2026-10-04 (ответ: «принимаем»)**; следующий гейт — ревью пользователем **детального письменного implementation plan** и выбор способа исполнения; план реализации не создан и не одобрен, способ исполнения не выбран; product-реализация, коммиты, push и merge **не авторизованы**. Acceptance-тесты — будущее evidence, ещё не выполнялись.
- **Тип документа:** дизайн-спека (записанные решения), не детальный план реализации. Описывается **один дизайн с двумя будущими work packages**, а не план работ.
- **Трекинг:** GitHub issue [#187](https://github.com/AlexeyShirshov/nextorm/issues/187) — «Repository API: provider-neutral DbContext and bounded query DELETE», статус OPEN; milestone **1.1-a.1** ([milestone/5](https://github.com/AlexeyShirshov/nextorm/milestone/5)). Issue проверен (VERIFIED) через scout.
- **Локальный путь этой спеки:** `docs/superpowers/specs/2026-10-04-repository-design.md` (файл в этом репозитории, не опубликованный blob; в рамках этой задачи не коммитится).
- **Доказательная база:** ссылки на код в разделе 2 — навигационные ориентиры (line pointers), а не воспроизведённые тесты. Новые тесты не запускались; в этой задаче нет сборки/тестов/установки.

## 1. Намерение и критерии успеха

Пользователь хочет производный application `DbContext` с типизированными источниками `IRepository<Order> Orders`, которые поддерживают:

- `ctx.From(ctx.Orders)`;
- joins против repos;
- прямые query starters и `ToList`/`Count`;
- свежие (fresh) `MakeInsert`/`MakeUpdate`/`MakeDelete`.

Назначение — **эргономичный API nextorm**, а **не** ORM-независимый бизнес-репозиторий. Существующие builders остаются движком. Не вводится tracking/SaveChanges/материализация перед мутацией. Тот же application context выбирает провайдера через нижележащий `IDataContext`; принимается вариант с явным getter-свойством.

Предлагаемый будущий пример (proposed API, **не** существующая compile-ready реализация):

```csharp
public class AppDbContext(IDataContext context) : DbContext(context)
{
    public IRepository<Order> Orders => Repository<Order>();
    public IRepository<Customer> Customers => Repository<Customer>();
}

ctx.Orders.Where(o => o.IsActive).ToList();
ctx.Orders.Count();
ctx.From(ctx.Orders);
ctx.From<Customer>().Join(ctx.Orders, (c, o) => c.Id == o.CustomerId); // типизированный Join, см. §4
ctx.Orders.MakeInsert().Values(order).Insert();
ctx.Orders.MakeUpdate().Where(o => o.Id == id).Set(o => o.IsActive, false).Update();
ctx.Orders.MakeDelete().Where(o => o.Id == id).Delete();
ctx.Orders.Where(o => o.IsActive).Limit(10).Delete();
```

Примечание к `Join`: типизированная форма подтверждена (VERIFIED через Roslyn). Существующий generic-метод: `public JoinedEntityBuilder<TEntity,TJoinEntity> Join<TJoinEntity>(EntityBuilder<TJoinEntity> _, Expression<Func<TEntity,TJoinEntity,bool>> joinCondition, Action<JoinOptions>? options = null)` — `src/nextorm.core/Builders/EntityBuilder.cs:2496`; фактическое использование — `tests/nextorm.integration.tests/CommonTestSuite.Join.cs:27`. Non-generic overload с `TableAlias` к этому не относится. Новый repository adapter форвардит в этот существующий типизированный метод через `repo.Query()` после валидации exact-context/root-invariant. Неразрешённого вопроса про typed-left/placeholder больше нет.

Новые starters `ctx.Delete<T>()`/`Insert<T>()` **не вводятся**.

## 2. Существующие факты и ограничения (с указателями на доказательства)

- `DataContext` — абстрактный класс, есть провайдерные наследники; `IDataContext` — композитный интерфейс без внутренностей мутаций. `src/nextorm.core/DataContext/DataContext.cs:15`; конструкторы и три абстрактных провайдерных хука.
- Fluent query-методы `EntityBuilder<T>` используют copy-on-write; сам `EntityBuilder` имеет публично изменяемые коллекции/поля (`EntityBuilder.cs:146,148,150`). DML-builders — мутабельные, свежие/одноразовые. Repository `Query()` обязан всегда быть свежим и не разделять мутабельный корневой список/состояние `Paging`. Новых гарантий потокобезопасности это не вводит.
- Существующие тестовые репозитории: `tests/.../TestDataRepository.cs:5-23`.
- `DataContextExtensions.From<T>`: строка 996; `From(EntityBuilder)`: строка 1352 → производный (derived) subquery.
- Существующие starters: `CreateInsertBuilder:74`, `CreateUpdateBuilder:175`, `CreateDeleteBuilder:362`.
- Процессно-глобальный `DataContextCache.Metadata` по CLR-типу и поведение «первой регистрации» (`DataContextExtensions:220-290` и `DataContextCache:23,45`).
- Опасность общей `AnyCommand` (AGENTS): никогда не выставлять `Cache=false` на общей команде; локальный planner `storeInCache:false` — только если это реально необходимо.
- Конкретные касты `DataContext`/`InMemory`, привязка EF через `ConditionalWeakTable` по идентичности (`QueryFilterContext.cs:296`, `EfCoreFilterBinding.cs:25`) означают: привязывать всё к **точному исходному** `IDataContext`, а не к фасаду.
- Существующий `Paging`: ноль = unlimited и нет маркера «явно задано» (`Paging.cs:11-49`; `EntityBuilder.cs:2130-2161`); для валидации delete это нужно расширить provenance, не меняя существующее поведение SELECT.
- Повторные `OrderBy` добавляются (append) (`EntityBuilder:3605-3654`).
- `InMemory` не поддерживает мутации (`InMemoryDataContext.cs:13`).
- Существующий ClickHouse `ALTER DELETE` + `mutations_sync=1` (`ClickHouseDialect.cs:67-74`); count не гарантирован.
- Новые тесты в этой задаче не запускались, и это не заявляется.

## 3. Альтернативы и выбранная архитектура

Выбран **тонкий source adapter** с существующими builder-типами.

Отклонено:

- свойство `EntityBuilder` само по себе — из-за различия «источник vs сконструированный запрос» и из-за `From(rootbuilder)` как subquery;
- отдельный repository DSL — из-за дублирования.

Новые типы:

- `NextORM.Core.DbContext` — абстрактный фасад (**не** `IDataContext` и **не** executor);
- `IRepository<TEntity>` — публичный;
- конкретная реализация репозитория — internal/sealed.

Предлагаемый публичный минимальный интерфейс — ровно такой:

```csharp
public interface IRepository<TEntity>
{
    IDataContext DataContext { get; }
    EntityBuilder<TEntity> Query();
}
```

`Query()` — фабрика корневого источника: свежий, неотфильтрованный, непагинированный builder, привязанный к тому же `DataContext`; это **не** `IQueryable`/`IEnumerable`. Extensions предоставляют `Where`, `OrderBy`, `OrderByDescending`, `Limit`, `IgnoreFilters`, `ToList`, `Count` и async-двойники для терминалов, а также `MakeInsert`/`MakeUpdate`/`MakeDelete`. Остальная функциональность запросов доступна через `Query()`, а не дублированием всей существующей поверхности. Sync/async-имена следуют существующим правилам именования, cancellation распространяется, блокирующей обёртки над async-исполнением нет.

`DbContext`:

- protected-конструктор принимает `IDataContext`;
- публично экспонирует `DataContext`;
- `protected Repository<TEntity>(Action<EntityMetadataBuilder<TEntity>>? configEntity = null)`.

Per-facade кэш по CLR-типу хранит только источник-обёртку репозитория и иммутабельное config-намерение, **не** `EntityBuilder`/`QueryCommand`/состояние мутаций. Никакой рефлексии, source generator или автоматических свойств. Запросы и DML репозитория всегда свежие и используют точный внутренний контекст.

Фасад **никогда не маскируется** под `IDataContext`; фабричные форвардинги/`From`-оверлоады вызывают внутренний контекст; низкоуровневые/редкие провайдерные API доступны через `DataContext`/`Query()`. Новый фасад должен быть аддитивным и не менять поведение существующих конкретных провайдеров/`IDataContext`. Используются тот же внутренний кэш/логи/привязки фильтров/транзакции/соединение. Потокобезопасность одновременных операций на одном `IDataContext` **не обещается**.

Коллизия имени с EF `DbContext` осознана; в документации использовать fully-qualified имя/alias, когда импортированы оба namespace.

## 4. Source adapters и идентичность

Добавляются:

- `DbContext.From<TEntity>(существующие параметры маппинга)`;
- `From(IRepository<T>)`;
- форвардинг существующих query-builder/query-command источников в объёме, нужном для согласованного fluent API.

`From(repository)` даёт прямой mapped entity root (**не** оверлоад `From(repo.Query())`, который дал бы subquery); `ctx.From(repo.Where(...))` сохраняет существующее поведение derived-запроса. Repository `Join`-оверлоады (существующие поддерживаемые варианты join, без нового вида join) делегируют с корневым источником и обычными предикатами/опциями.

Предлагаемая **новая** extension-сигнатура типизированного Join (spec pseudo-signature с `;`, **не** скомпилированный код; это не заявление, что новый метод уже существует):

```csharp
public static JoinedEntityBuilder<TLeft,TRight> Join<TLeft,TRight>(
    this EntityBuilder<TLeft> left,
    IRepository<TRight> right,
    Expression<Func<TLeft,TRight,bool>> joinCondition,
    Action<JoinOptions>? options = null);
```

Она форвардит в существующий overload `EntityBuilder.cs:2496` через `repo.Query()` после валидации exact-context/root-invariant. Варианты `Left`/`Right`/`Full`/`Cross`/`Apply` (и прочие существующие) переиспользуют только уже поддерживаемую семантику; новый вид join не вводится.

Валидация:

- `repository.DataContext` — тот же **точный** экземпляр, что и нижележащий левый контекст, и root builder из `Query()` привязан к тому же экземпляру;
- разные экземпляры провайдерных контекстов отвергаются `InvalidOperationException` **до выполнения SQL/до обращения к БД**, даже при одинаковом типе/connection string.

Два фасада над одним и тем же точным внутренним контекстом могут разделять источники репозиториев. Существующее поведение `From(EntityBuilder)` между разными контекстами этой спекой **не переопределяется**.

Нельзя встраивать WHERE-отфильтрованный/упорядоченный repo-объект: `IRepository` — всегда корневой источник; глобальные фильтры по-прежнему применяются обычным образом на этапе существующей подготовки.

## 5. Маппинг и время жизни

Переиспользуются атрибуты/конвенции/fluent `EntityMetadataBuilder`. `Repository<T>(cfg)` сохраняет существующую семантику **первой глобальной регистрации**; per-facade создание репозитория кэширует первое config-намерение; разрешение маппинга — ленивое, на первом `Query`/`MakeDML`/использовании источника, а не просто при конструировании обёртки. И query-, и mutation-создание резолвят одни и те же сконфигурированные метаданные (MakeDML не должен резолвить auto-метаданные раньше переданного repo `cfg`). Новый per-context model/mapping scope не вводится. Раздельные конфиги для одного CLR-типа по разным контекстам **не обещаются**. Повторный getter-cfg delegate не перезапускается для мутации уже существующих метаданных.

Соответствие фабрик репозитория существующим фабрикам контекста:

| Repository API (новое) | Существующая фабрика |
| --- | --- |
| `repo.MakeInsert()` | `IDataContext.CreateInsertBuilder<T>()` |
| `repo.MakeUpdate()` | `IDataContext.CreateUpdateBuilder<T>()` |
| `repo.MakeDelete()` | `IDataContext.CreateDeleteBuilder<T>()` |

Каждая фабрика вызывается на точном внутреннем контексте после того, как переданный repository `cfg` разрешил метаданные, и каждый вызов даёт свежий builder. Repository `MakeX` — это **новое** слоевое API над существующими фабриками; внутренние имена `SqlMutationBuilder` не являются repository API, и нельзя утверждать, что публичный `MakeX`-API уже существует.

Фасад заимствует внутренний `IDataContext` и не диспозит его; независимого connection/transaction lifecycle нет. DI использует существующую провайдерную регистрацию `AddNextOrmContext` плюс scoped-регистрацию `AppDbContext`; **не** форвардить регистрацию `IDataContext` обратно в `AppDbContext` (это дало бы цикл и неверную идентичность). Репозиторий не может жить дольше внутреннего контекста; после диспоза провайдера — существующие ошибки. Ownership-конструкторы/фабрики и новые generic DI registration helpers — вне объёма.

## 6. Контракт DELETE

Новые терминалы `Delete`/`DeleteAsync` на `EntityBuilder<T>` — для single mapped entity root; `int` affected count синхронно и соответствующий существующий async-паттерн; провайдерная семантика сохраняется там, где count неизвестен. Обычный unbounded predicate delete делегирует существующее поведение DML.

- Возвращается фактический driver count, **без** предварительного `COUNT`-запроса; точный count для ClickHouse не обещается.
- Короткая форма требует **явного** `Where`-условия **до** инъекции глобальных фильтров; фильтров/Limit самих по себе недостаточно; `repo.Query().Delete()` и `repo.Limit(N).Delete()` отвергаются `InvalidOperationException`.
- Намерение «вся таблица» выражается явно: `MakeDelete().All().Delete()`.
- `All` плюс положительный `Limit` на явном `DeleteBuilder` разрешены, если выполнены предусловия bounded-delete.
- Повторные `Where` соединяются конъюнкцией; scope фильтров, включая `IgnoreFilters`, сохраняется.

Расширение `DeleteBuilder<T>`: `OrderBy`/`OrderByDescending`/`Limit` при неизменном мутабельном одноразовом поведении; положительные bounded-формы используют ту же внутреннюю семантическую модель/рендеринг, что и query-терминал. Кэширование/переиспользование mutation builders в репозитории запрещено.

- Ordering поддерживается только с положительным limit: ordering без limit отвергается (а не молча отбрасывается и не делает бессмысленный key-selection).
- Positional/nonnamed ordering, которое нельзя сопоставить свойствам цели, отвергается; `OrderBy`-выражение валидируется по существующим правилам поддерживаемых column expressions.
- `Returning` остаётся доступным на явном builder только там, где это позволяют существующие провайдерные возможности; bounded returning обязан отвергнуть неподдерживаемую провайдерную комбинацию **до выполнения SQL/до обращения к БД**; новых обещаний по returning не вводится.

Bounded deletion:

- ровно одна SQL-команда, без клиентской материализации/списка ID, без N отдельных `DELETE` и без дополнительного `COUNT`;
- серверный selected-key запрос применяет полные предикаты/scope глобальных фильтров и order/положительный limit;
- target deletion сопоставляется по полному уникальному non-null объявленному ключу; составные ключи поддерживаются;
- метаданные обязаны объявлять уникальную non-null identity, согласованную с DB-ограничениями/данными; **нельзя** выводить уникальность БД из произвольных свойств или использовать non-key `OrderBy`-значения как identity; никакой schema introspection и никакой fallback на скрытые row identifiers провайдера;
- без ключа positive-limit delete падает `InvalidOperationException` до выполнения SQL/до обращения к БД; unbounded predicate delete ключа не требует;
- при корректном key mapping/схеме удаляется не более N target-строк в пределах обычной DB-изоляции; может удалиться меньше; стабильности набора/batch между вызовами и при конкурентных изменениях нет; triggers/cascades — вне предела target rows;
- сортировка не обязательна; какие именно ключи попадут — произвольно;
- при sorted selection ничьи (ties) произвольны, если пользователь не добавил уникальный tiebreaker; неявная сортировка/tiebreaker **не** добавляется.

Limit для мутаций только положительный; явный ноль обязан бросить `ArgumentOutOfRangeException` **до выполнения SQL/до обращения к БД**, а не означать unlimited. Существующая семантика SELECT с нулём не меняется. Нужно сохранять provenance «явно заданного limit» через копии `Paging`, клоны `EntityBuilder` и конвертацию `ToCommand`/DML (включая прямые пути установки field-property `Limit(0)`), чтобы «default unspecified limit» отличался от «explicit 0» для деструктивной валидации; при этом нельзя мутировать общие cached-команды и ломать существующее равенство/поведение SELECT-кэша. Отрицательные значения сохраняют существующие argument errors.

Поддерживаемая форма: mapped single entity table, явный `Where`, scope фильтров, опциональный представимый ordering плюс положительный `Limit`. `offset = 0` допускается как no-op; отвергаются: положительный `Offset`, `WithTies` (может превысить N), `Distinct`/`DistinctOn`, grouping/Having, joins на этом новом пути, set operators/unions, смена projection/result shape, CTE/derived/raw SQL/table-function/temp-table/pivot/unbound named table sources, eager-loading/includes и провайдерные опции, семантика которых не может быть достоверно представлена. Падение — `NotSupportedException` **до выполнения SQL/до обращения к БД**, с диагностикой operation/source/modifier/provider; неизвестное состояние **никогда не отбрасывается молча**. Существующий joined-delete API остаётся отдельным и неизменным. Часть core-валидации может выполняться внутри SQL-рендеринга; важно только, что до обращения к БД не происходит исполнения и побочных эффектов; заявлять, что весь query builder валидируется немедленно при создании (до рендеринга), **нельзя**.

## 7. Провайдерный дизайн и официальные доказательства

Общая семантическая selected-key модель с dialect-specific SQL; используются существующие expression renderer/quoting/parameters/converters/schema names (без ad-hoc текста выражений).

- **SQL Server:** `TOP` в selected-key запросе и full-key `JOIN`/`EXISTS`; никакого допущения о tuple `IN`.
- **PostgreSQL:** key selection `LIMIT` и `DELETE USING`/`IN` с полным ключом.
- **MySQL/MariaDB:** derived/materialized selected-key relation с внутренним `LIMIT` и `JOIN`/equality; избегать запрещённого directly limited `IN` subquery/self-table merge.
- **SQLite:** key-subquery `IN` (single/composite row value) без требования `SQLITE_ENABLE_UPDATE_DELETE_LIMIT`.

Существующая SQL-генерация и фактически поддерживаемые версии провайдеров должны быть верифицированы интеграционно, прежде чем заявлять успех реализации. Native top/limit optimization delete для начального дизайна **не требуется**; приоритет — семантика.

Матрица:

| Провайдер | Проектируемая поддержка ограниченного DELETE (ещё не реализовано/не проверено) |
| --- | --- |
| PostgreSQL | планируется: да, при наличии ключа и полной семантике |
| SQL Server | планируется: да, при наличии ключа и полной семантике |
| MySQL | планируется: да, при наличии ключа и полной семантике |
| MariaDB | планируется: да, при наличии ключа и полной семантике (интеграционное evidence — обязательное предусловие приёмки) |
| SQLite | планируется: да, при наличии ключа и полной семантике |
| ClickHouse | `NotSupportedException` в начальной версии (нет гарантированного уникального ключа, mutation subquery/count не верифицированы); обычный существующий `ALTER DELETE` не меняется, driver count для него не гарантирован |
| InMemory | мутации не поддерживаются, без изменений (не проектируется) |

MariaDB: интеграционное evidence — обязательное предусловие приёмки (acceptance prerequisite), а не неразрешённое поведение дизайна. Политика ClickHouse по heavy/lightweight мутациям **не меняется**; disclaimer про driver count для обычного ClickHouse `ALTER DELETE` остаётся в силе. Returning limited подчиняется существующим флагам и тестам, без blanket-гарантии.

Официальные ссылки (обоснование дизайна, **не** доказательство существующей реализации nextorm):

- SQL Server `DELETE`: <https://learn.microsoft.com/en-us/sql/t-sql/statements/delete-transact-sql?view=sql-server-ver17> — `TOP` без упорядочивания; `ORDER` через subquery; дубликаты в nonkey subquery могут превысить N.
- PostgreSQL `DELETE`: <https://www.postgresql.org/docs/current/sql-delete.html> — limited batch по selected relation.
- MySQL `DELETE`: <https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/delete.html>.
- MySQL subquery restrictions: <https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/subquery-restrictions.html>.
- MySQL derived table optimization: <https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/derived-table-optimization.html> — `LIMIT` препятствует merge.
- MariaDB `DELETE`: <https://mariadb.com/kb/en/delete/>.
- SQLite `DELETE`: <https://www.sqlite.org/lang_delete.html>.
- SQLite row values: <https://www.sqlite.org/rowvalue.html>.
- SQLite expressions: <https://www.sqlite.org/lang_expr.html>.
- ClickHouse `ALTER DELETE`: <https://clickhouse.com/docs/en/sql-reference/statements/alter/delete>.

## 8. Таблица ошибок

| Ситуация | Исключение |
| --- | --- |
| Cross-context / нарушение root invariant / отсутствие явного `Where` / отсутствие ключа | `InvalidOperationException` |
| Неподдерживаемый source/modifier/provider/ordering-without-limit | `NotSupportedException` |
| Явный ноль / отрицательный mutation cap | `ArgumentOutOfRangeException` |

Валидация — до выполнения SQL/до обращения к БД и без записей; **нельзя** ловить провайдерные SQL-ошибки и превращать их в «unsupported» успешный fallback. Неподдерживаемые limited ClickHouse/InMemory отвергаются детерминированно. Конфликты маппинга подчиняются существующему поведению регистрации, а не новому кастомному исключению. Async cancellation форвардится в исполнение; отменённый вызов никогда не откатывается на синхронный путь.

## 9. Матрица верификации/приёмки (будущая; тесты сейчас НЕ запускаются)

Core unit:

- per-facade кэш, отдельные facade repo wrappers, shared inner identity;
- `Query()` свежий; независимые цепочки `Where`/`OrderBy`/`Limit`; свежие `MakeX`; отсутствие накопленного состояния репозитория;
- ленивый сконфигурированный маппинг раньше auto-разрешения; source root vs `From(filtered builder)` subquery;
- cross-inner rejection.

Query:

- фильтры tenant/softdelete/closure + `IgnoreFilters` по scope на query и на delete, включая selected-key запрос;
- фасад сохраняет точные EF owner/filter ожидания;
- изоляция cache/Any Count test, sticky `Cache` флаг не меняется, нет регрессии `SELECT Limit 0`, provenance limit переживает clone и конвертацию, а также явный `Paging.Limit` setter.

SQLgen:

- все bounded-провайдеры: quoting/schema, составные ключи, order/tie/filter/params, key columns с конвертерами;
- semantic parity direct и explicit-builder; нет потери фильтров дублированием.

Runtime (provider-backed):

- PostgreSQL/SQL Server/MySQL + SQLite (и evidence для MariaDB для фактической целевой версии, а не вывод из MySQL);
- >N совпадений → affected ≤ N и корректные оставшиеся строки;
- sorted unique tie-break selection ожидаем; unsorted тест проверяет только count/membership, не конкретные id;
- составные ключи, где первая колонка повторяется, — проверять полное tuple matching;
- nullable/no key rejection; глобально отфильтрованный subset не тронут;
- transactional rollback; sync/async/cancellation; returning, где поддерживается;
- zero cap, unsupported options, missing key, positive offset, ties, different inner repo и CH limited — все без DB-команд/побочных эффектов;
- существующие plain/key/join Delete регрессии прогоняются.

Пропущенные container-тесты **нельзя** подавать как green. Будущий исполнитель обязан прочитать `.opencode/skills/running-integration-tests/SKILL.md` до запуска реальных наборов и запустить Podman, как требуется. Существующие контейнеры не покрывают MariaDB: нужно зафиксировать фактически добавленное/провизионное test evidence или явный gap, а не выводить runtime success из MySQL. В этой design-only задаче нет build/test.

## 10. Документация и границы результата

Публичные EN/RU guides и examples для facade/`IRepository` и escape hatch `Query()`, `MakeX`, direct `Delete`, unordered caveat, schema key precondition, provider matrix, lifetime, mapping safety. Публичные docs/readme **не** должны ссылаться на внутреннюю спеку. Квалификация/alias для namespace-коллизии. Реализация сейчас не делается.

Два будущих work packages:

- **A** — facade/repo/source adapters + docs/tests;
- **B** — query DELETE bridge/mutation snapshot/provenance/key selection dialects + docs/tests.

Оба покрываются #187; порядок реализации планируется позже.

Вне объёма: tracking, `SaveChanges`, ORM-независимая domain abstraction, автоматические свойства, source generation, `IQueryable`, отдельный DSL, `Update from query`, limited joins, context-local mapping, ownership/factories, limited `DML` для ClickHouse, in-memory мутации.

## 11. Handoff и approve-гейты

- In-chat секции одобрены; **письменная спецификация утверждена пользователем 2026-10-04 (ответ: «принимаем»)**; #187 OPEN в milestone 1.1-a.1 (VERIFIED). Следующий гейт — ревью пользователем **детального письменного implementation plan** и выбор способа исполнения; план pending и способ исполнения pending; product code не писан, авторизации на commit нет.
- Следующий шаг — ревью пользователем **детального письменного implementation plan** и выбор способа исполнения (это уже не ревью письменной спеки: она утверждена). Планирование выполняется через planning handoff: в текущей architect-сессии доступен только `brainstorming`, а skill `writing-plans` недоступен, поэтому вызывать его не следует и нельзя делать вид, что он вызван.
- Утверждение письменной спеки **не** является утверждением реализации.
- В рамках этой задачи не вызываются другие skills и не создаётся план.
- Письменная спецификация утверждена 2026-10-04; будущие acceptance/tests — это pending implementation evidence, а не design TBD. Утверждение письменной спеки не авторизует план или исполнение.
