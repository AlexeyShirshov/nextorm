# Обзор SOLID / DRY — nextorm

Read-only ревью дизайна core-библиотеки `nextorm` на соответствие принципам SOLID и
DRY. Код не изменялся. Ссылки на находки — в формате `file:line`.

> **Примечание об области.** Это дизайн-ревью, а не анализ производительности.
> Механика DI-контейнера (регистрация/времена жизни) формально относится к
> `dotnet-csharp-dependency-injection`; здесь она приведена только на уровне дизайна.
> Идентификаторы находок (F1..F13) сохранены стабильными для перекрёстных ссылок;
> порядок изложения ниже начинается с **F4** по запросу. F12/F13 добавлены при актуализации
> после расширения до шести провайдеров (MySQL, MariaDB, ClickHouse); **F1 закрыт** на шаге 4
> (ось окружения/кэша), **F13 закрыт** на шаге 5 (исполнительный фасад in-memory вынесен в
> `InMemoryQueryExecutor`), **F12** перепроверен и отложен (разбиение не окупается, см. F12).
>
> **Примечание о ссылках `file:line`.** Блоки «Доказательство» описывают **состояние до
> рефакторинга**, поэтому их номера строк указывают на прошлые ревизии и в текущем дереве
> сдвинуты. Ссылки, адресующие текущее дерево («Сверка сводной таблицы», «Исправление»,
> «Статус реализации»), повторно сверены с рабочей копией после `d21c473` и приведены к
> актуальным номерам строк; переходы размеров вида «X → Y» в «Статусе реализации» —
> исторические снимки выполненных работ, а не текущее состояние.

---

## Сводка

| # | Симптом | Принцип | Критичность | Статус |
|---|---|---|---|---|
| [F4](#f4-дублирование-логики-материализации-строк-dry-средне-высокая) | Логика материализации строк продублирована в обоих контекстах | DRY | Средне-высокая | ✅ Реализовано |
| [F1](#f1-dbcontext--god-class-srp-высокая) | `DataContext` — God class: соединение + диалект + маппинг + кэш | SRP | **Высокая** | ✅ Реализовано |
| [F2](#f2-idatacontext--fat-interface-isp-высокая) | `IDataContext` — ~60 членов на все роли | ISP | **Высокая** | ✅ Реализовано |
| [F3](#f3-протекающий-контракт-и-дублирование-guard-проверок-lspdry-средняя) | 27 guard-проверок `is …` + `NotSupportedException`; контракт протекает | LSP/DRY | Средняя | ✅ Реализовано (единый runtime-guard + regress-тесты; compile-time CRTP отклонён — см. F3) |
| [F5](#f5-notimplementedexceptionnotsupportedexception-как-базовое-поведение-lsp-средняя) | `NotImplementedException`/`NotSupportedException` как контракт базы | LSP | Средняя | ✅ Реализовано |
| [F6](#f6-зависимость-от-конкретного-dbcontext-dip-средняя) | SQL-билдер и визиторы зависят от конкретного `DataContext` | DIP | Средняя | ✅ Реализовано |
| [F7](#f7-пустой-алиас--перегруженный-iqueryprovider-ispyagni-низкая) | `IQueryContext` — пустой алиас; `IQueryRegistry` перегружен хешированием плана | ISP/YAGNI | Низкая | Маркер удалён; ISP отложен |
| [F8](#f8-di-регистрации-дублирование-и-двойной-инстанс-drydip-средняя) | 6 дублирующихся DI-оверлоадов + двойная регистрация | DRY/DIP | Средняя | ✅ Реализовано |
| [F9](#f9-baseexpressionvisitor--god-class--switch-на-именах-srpocp-средняя) | `BaseExpressionVisitor` — God class + switch на именах | SRP/OCP | Средняя | ✅ Реализовано (switch по `nameof(TableAlias.*)` заменён таблицей `TableAliasAccessors`) |
| [F10](#f10-дублирование-агрегатов-в-entitybuildertentity-dry-низкая) | `EntityBuilder<TEntity>`: 40 членов, 8 семейств агрегатов отличаются только `MethodInfo` | DRY | Низкая | ✅ Реализовано |
| [F11](#f11-параллельные-реализации-кэшей-dry-низко-средняя) | Параллельные реализации кэшей (static vs instance) | DRY | Низко-средняя | ✅ Реализовано |
| [F12](#f12-isqldialect--фат-интерфейс-на-97-членов-isp-средняя) | `ISqlDialect` — 97 членов (34 флага + 63 метода), 1 реализация по умолчанию; шестеро провайдеров закрывают остальное через `SqlDialectBase` | ISP | Средняя | ⚠️ Отложено осознанно (KISS) |
| [F13](#f13-inmemorycontext--параллельный-фасад-исполнения-srpdry-средняя) | `InMemoryDataContext` дублирует структуру исполнения, вынесенную из `DataContext` | SRP/DRY | Средняя | ✅ Реализовано |

---

## Сверка сводной таблицы (прогон по коду)

Актуализация по рабочей копии после `d21c473` (`refactoring`; все ссылки перепроверены по
содержимому строк, а не по номерам). Кодовая база с предыдущей сверки заметно изменилась:
провайдеров стало **шесть** (добавлены MySQL, MariaDB, ClickHouse), `BaseExpressionVisitor`
декомпозирован, `Entity` переименован в `EntityBuilder`.

| # | Что проверено в коде | Вердикт |
|---|---|---|
| F4 | `RowMaterializerBuilder.Build` вызывается из `InMemoryDataContext` (`:1362`) и `RowMapperFactory` (`:88`) | ✅ подтверждено |
| F1 | `DataContext.cs` 1357 → 1104 → 397 → **235** строк; вынесены диалект (`ISqlDialect`), маппинг (`RowMapperFactory`), **соединение** (`DbConnectionManager`, 146), **исполнение** (`QueryExecutor`, 452), **планирование** (`QueryPlanner`, 251 + `QueryPlanStore`, 32) и **окружение/кэш** (`ContextEnvironment`, 44 — общий с `InMemoryDataContext`; `QueryCache`, 22); удалены мёртвые `_connOpen`, `GetAliasFromProjection`, `CreateConnection` (public, без внешних потребителей) и ~180 строк закомментированного кода; провайдерские `DataContext` — 20–65 строк | ✅ подтверждено |
| F2 | `IDataContext.cs` 122 → **25** строк: пустой композит; ролей — **7** файлов (`IConnectionManager`, `IContextEnvironment`, `IQueryCache`, `IQueryExecutor`, `IQueryMaterializer`, `IQueryPlanner`, `IRowReaderFactory`) | ✅ подтверждено |
| F3 | guard'ов `is I…` в `DataContext` — **0**, `NotSupportedException` в `DataContext` — **0**; единая проверка `AsDbCommand`/`AsInMemoryCommand` + регресс-тесты `BackendMixingTests` (18.09.2026). По всему `src/nextorm.core` `NotSupportedException` — по большей части **capability-сообщения с текстом** («… not supported by this provider»), а не заглушки базового контракта | ✅ закрыто (runtime-guard + regress-тесты; compile-time CRTP отклонён, см. F3) |
| F5 | `NotImplementedException` в `DataContext` — **1**, внутри закомментированного блока (`:378`); в `SqlDialectBase` — 4 `NotSupportedException` с внятными сообщениями (repeat/position/reverse «not supported by this provider») | ✅ подтверждено |
| F6 | `SqlBuilder`, `BaseExpressionVisitor`, `WhereExpressionVisitor` — **0** обращений к `DataContext`; `StringBuilderPool.Shared` — 16 вызовов из одного пула; `ResultSetEnumerator`/`DbPreparedQueryCommand` отвязаны (роль `IConnectionManager` + делегат `CreateParam`), `InitEnumerator` — `internal`, блок открытия соединения сведён к `EnsureConnectionOpen(Async)` | ✅ подтверждено |
| F7 | `Query/IQueryContext.cs` отсутствует; упоминаний в `src`/`test` нет | ✅ подтверждено |
| F8 | приватные `RegisterContextFactory` (`DI/ServiceCollectionExtensions.cs:73`) / `RegisterContextType` (`:103`); 6 публичных оверлоадов делегируют в них | ✅ подтверждено |
| F9 | `BaseExpressionVisitor` **2128 → 366** строк; декомпозирован в ~19 translator-классов (`PredicateTranslator`, `MemberTranslator`, `ScalarFunctionTranslator`, `StringFunctionTranslator`, `MathFunctionTranslator`, `DateTimeFunctionTranslator`, `BuiltinFunctionTranslator`, `NormSqlTranslator`, `SqlOperandTranslator`, `AdvancedAggregateTranslator`, `ArraySqlTranslator`, `JsonSqlTranslator`, `TextJsonSqlTranslator`, `WindowFunctionTranslator`, `InValuesTranslator`, `AggregateFilter`, `TypeFacts`, `SqlLiteral`, `AliasResolver`, `WindowSql`, `TypedParamVisitor`, `TableAliasAccessors`); `NotImplementedException` — **0** в этом файле. `switch` по `nameof(TableAlias.*)` заменён таблицей `TableAliasAccessors` (`Visitors/`) | ✅ Реализовано |
| F10 | `Builders/EntityBuilder.cs` 674 → 602 → **899** (переименован из `Entity.cs`, вырос за счёт новых членов); `XCore`-дублей — **0**; `AggregateCore`/`AggregateAsyncCore` на месте (`:721+`) | ✅ подтверждено |
| F11 | `DataContextCache` (34 строки) — единственный источник (`Metadata`/`SelectListCache`/`ExpressionsCache`/`InValuesCache`); `InMemoryDataContext` делегирует `Metadata` (`:70`) / `SelectListCache` (`:73`), дублей-статиков нет; `_expCache` инстансный **осознанно** (захват `this`, `:30`) | ✅ подтверждено |
| F12 | `ISqlDialect` — **502 строки, 97 членов** (34 свойства + 63 метода), реализаций по умолчанию — **1**; шесть провайдеров + `SqlDialectBase` (325 строк) закрывают остальное; `MariaDbDialect` — 14 строк (наследник `MySqlDialect` + `SupportsIntersectExceptAll => true`) | Отложено — разбиение не окупается (см. F12, «Актуализация») |
| F13 | `InMemoryDataContext` — **351** строк ✅ (было 1711; шаг 5 — 1446, затем фазы 1–8 декомпозиции in-memory: фазы 1–7 вынесли `InMemoryOrdering`/`InMemorySetOperations`/`InMemoryProjectionFactory`/`InMemoryConditionFactory`/`InMemoryGrouping`/`InMemoryRowMaterializer`/`InMemoryLinqSource`, фаза 8 перенесла тела reflection-адресуемых методов в `InMemoryQueryBuilder` (373) и `InMemoryJoin` (157), оставив на контексте тонкие обёртки 1:1 — `MethodInfo`-target и `Expression.Call` не менялись; порог 500 достигнут); исполнительный фасад (13 членов + `GetEnumerable`/`CreateEnumerator(Async)`) вынесен в `InMemoryQueryExecutor` (**336 строк**, `internal sealed`, `IQueryExecutor` + `IRowReaderFactory`); окружение/кэш общие (`ContextEnvironment`, `QueryCache`); инстансные кэши выражений остались на контексте (передаются хелперам параметрами) | ✅ подтверждено |

**Прогон (актуализация).** build Debug **0/0**; core **154**, sqlite **179**, postgres **150**,
sqlserver **166** — везде Failed 0 (xUnit v3 раннер напрямую; `dotnet test` тесты не
обнаруживает); integration **833**, Failed 0, **Skipped 23** (Testcontainers: PostgreSQL +
SQL Server + MySQL через podman). Прежний снимок (после закрытия F6): core 107, sqlite 136,
postgres 90, sqlserver 114, integration 561 (Skipped 384); на момент шага 3 F1 —
111/149/115/124, integration 561 (Skipped 16).

---

## F4. Дублирование логики материализации строк (DRY, Средне-высокая)

**Доказательство.** `src/nextorm.core/DataContext/DataContext.cs`, `GetMap<TResult>`
(`:995-1080`), и `src/nextorm.core/DataContext/InMemoryDataContext.cs`,
`GetMap<TResult,TEntity>` (`:613-693`), содержат почти идентичный алгоритм выбора
конструктора:

```
var ctorInfo = resultType.GetConstructors()
    .OrderByDescending(it => it.GetParameters().Length).FirstOrDefault()
    ?? throw new QueryPreparationException($"Cannot get ctor from {resultType}");

if (ctorInfo.GetParameters().Length == queryCommand.SelectList!.Length)
    // Expression.New(ctorInfo, newParams)      -> путь через конструктор
else
    // Expression.MemberInit(ctor, bindings)    -> путь через присваивание свойств
```

Оба используют одни и те же вызовы `MapColumn*` для построения member-выражений.
Циклы построчного сканирования sync/async для `Single`/`SingleOrDefault` («ровно одна
строка, иначе исключение») также продублированы между двумя классами.

**Первопричина.** Алгоритм «форма результата -> дерево выражений» был скопирован, а не
вынесен в общий строитель.

**Влияние.** Расхождение уже есть: `InMemoryDataContext` обрабатывает `IgnoreColumns`
отдельной ветвью (`:640`), `DataContext` — нет. Любое изменение правил маппинга (новая
форма конструктора, init-only члены, records) нужно вносить дважды, и они могут
незаметно разойтись.

**Исправление.** Выделить `RowMaterializerBuilder`, принимающий выражение-параметр
записи и поставляемый провайдером маппер колонок
(`Func<SelectExpression, Expression, Expression>`). Композировать его в обоих
контекстах вместо копирования. Композиция вместо наследования (см. guidance скилла о
переиспользовании поведения).

---

## F1. `DataContext` — God class (SRP, Высокая)

**Доказательство.** `src/nextorm.core/DataContext/DataContext.cs` — 1357 строк, 81
объявление, 25 `virtual`-членов. Одновременно владеет минимум пятью несвязанными
осями:

- жизненный цикл соединения — `GetConnection`, `EnsureConnectionOpen(Async)`,
  `CreateConnection`, `OnStateChanged`, `ConnDisposed`, `DisposeStaff`
  (`:84-157`, `:669-715`);
- исполнение запросов — `ToList/First/Single/ExecuteScalar` × sync/async (`:739-1313`);
- маппинг строк — `GetMap`, `GetMapCached`, `BuildMapperKey`, `MapColumn`,
  `ConvertScalar` (`:807-1080`);
- рендеринг диалекта SQL — `MakeCoalesce/Escape/MakePage/MakeParam/MakeTypeName/`
  `MakeBool/MakeCount` (`:491-642`, `:1315`);
- кэширование — `[ThreadStatic] QueryPlanCache`, `MapperCache`, `PurgeQueryCache`
  (`:40-46`, `:298-377`, `:1321`);
- создание команд/параметров — `CreateCommand`, `CreateParam`, `GetParamName`,
  `ExtractParams` (`:23-35`, `:152-157`, `:486`).

Тест «описать одним предложением без *и*» не проходит: контекст открывает соединение
**и** генерирует SQL **и** кэширует планы **и** материализует строки.

**Первопричина.** Базовый класс — одновременно runtime-конвейер и контракт диалекта;
провайдеры переопределяют диалект, но наследуют весь конвейер.

**Влияние.** Любое изменение диалекта/маппинга проходит через класс, целиком лежащий
на горячем пути. Диалект невозможно протестировать в изоляции. Это же — корень F6.

**Исправление (в работе).**
- **Сделано — ось диалекта.** `ISqlDialect` + `SqlDialectBase` (`DataContext/Dialect/`)
  выделены из `DataContext`; все `Make*`/`Escape`/`Require*`/`MakePage`/`MakeTop` переехали
  туда, три провайдера получили `SqliteDialect`/`PostgresDialect`/`SqlServerDialect`.
  `DataContext` объявлен `abstract` и владеет только `public abstract ISqlDialect Dialect`.
  `SqlBuilder` и визиторы теперь зависят от `ISqlDialect` и `ILogger?`, а не от контекста
  (закрывает F6).
- **Сделано — ось соединения.** Connection-состояние (`_connectionString`,
  `_providedConnection`, `ConnectionString`, логирование) переехало в `DataContext`; провайдер
  задаёт только `CreateDbConnection`/`OnConnectionCreated`. `IConnectionFactory` как отдельная
  абстракция **сознательно не вводится**: единственный потребитель — сам `DataContext`, а
  реализации трёх провайдеров были на 90% идентичны, т.е. это DRY-дефект, а не отсутствующий
  шов. Роль соединения при этом усилена: `IConnectionManager` получил `GetConnection()`.
- **Сделано — ось маппинга.** Построение и компиляция строкового маппера вынесены в
  `RowMapperFactory`; в `DataContext` остались тонкий хук `MapColumnExpression` (его
  переопределяет SQL Server) и одна строка делегирования. `IColumnMapper` **сознательно не
  вводится**: политика маппинга передаётся делегатом `Func<SelectExpression, Expression,
  Expression>`, а клиент, ради которого именованный интерфейс имел смысл
  (`ReplaceMemberVisitor`), оказался мёртвым кодом и удалён. `ConvertScalar` оставлен — это
  скалярная конверсия для `ExecuteScalar`, а не маппинг строк.
- **Сделано — ось исполнения (шаг 1).** Терминалы (`ToList/First/Single/ExecuteScalar`
  × sync/async), их `GetDbCommand`-обвязка, `ConvertScalar`, `CheckDisposed` и
  `CreateEnumerator(Async)` вынесены в `QueryExecutor` (`DataContext/QueryExecutor.cs`) —
  `internal sealed`, реализует `IQueryExecutor` + `IRowReaderFactory`. Зависимости приходят в
  конструктор (`IConnectionManager`, делегат `CreateParam`, логгер, флаг `logParams`,
  `logSensitiveData`, `Func<bool> isDisposed`), конкретного контекста он не видит.
  `DataContext` оставляет тонкие делегаты — публичная поверхность не изменилась. Попутно:
  `CreateResultSetEnumerator` **оставлен** на `DataContext`, потому что его использует
  **планировщик** (`:376`, `:462`), а не исполнитель (чуть не уехал по ошибке).
- **Сделано — мёртвое состояние.** `internal bool _connOpen` после F6 писался и не читался
  нигде; вместе с ним удалены `OnStateChanged` и подписки на `conn.StateChange` (обработчик
  существовал только ради этого поля).
- **Сделано — ось планировщика (шаг 2).** `QueryPlanner` (`internal sealed`, `IQueryPlanner`,
  `DataContext/QueryPlanner.cs`) забрал `GetPreparedQueryCommand` (183 стр.), `MakeSelect`,
  `ExtractParams`, `IsRuntimeParam`, `GetMapCached`, `ResetPreparation` и оба `GetFrom`
  (+ `_fromCache`). Store планов вынесен в отдельный `QueryPlanStore` (thread-static), который
  теперь делят планировщик, сброс соединения (`ConnDisposed`/`DisposeStaff`) и `PurgeQueryCache`.
  Провайдерские хуки (`Dialect`, `MapColumnExpression`, `CreateParam`, `CreateCommand`) переданы
  делегатами и вызываются **лениво** — вызов виртуального/абстрактного члена из базового
  конструктора выполнил бы код наследника до его конструктора. Удалён мёртвый
  `GetAliasFromProjection` (существовал только как определение); `ResetPreparation` сведён к
  делегату (был пустой no-op). `DataContext` 1036 → **473** строки (**−57%** от исходных 1095).
- **Сделано — ось соединения (шаг 3).** `DbConnectionManager` (`internal sealed`,
  `IConnectionManager`, `DataContext/DbConnectionManager.cs`) забрал состояние и алгоритм соединения:
  ленивое создание, владение (`_conn`/`_connWasCreatedByMe`), открытие, `ConnDisposed`, teardown.
  Провайдерские хуки **остались на контексте** — `CreateDbConnection` (abstract) и
  `OnConnectionCreated` (virtual) передаются менеджеру делегатами, контракт подклассов не изменён.
  Сам контекст уходит в менеджер только как роль `IDataContext` (нужна для
  `IDbCommandHolder.ResetConnection(conn, IDataContext)`). `DisposeStaff` схлопнулся в
  `_connectionManager.DisposeConnection()` + событие `Disposed`. `DataContext` 473 → **397** строк.
  Публичная поверхность сохранена: `GetConnection`, `EnsureConnectionOpen(Async)`, `ConnectionString`
  (используется тестом), `CreateCommand` — тонкие делегаты. **Изменение поверхности:**
  `public virtual DbConnection CreateConnection()` удалён (никем не переопределялся и не вызывался
  извне; точка расширения провайдера — `CreateDbConnection`/`OnConnectionCreated`, как и
  задокументировано).
- **Сделано — ось окружения/кэша (шаг 4).** `ContextEnvironment`
  (`DataContext/ContextEnvironment.cs`, 44 строки, `internal sealed`, реализует
  `IContextEnvironment`) забрал логгеры (`Logger`, `CommandLogger`, `ResultSetEnumeratorLogger`),
  флаг `LogParams` (выводится из уровня лога), `LogSensitiveData`, `NeedMapping` и
  пользовательский `Properties`. `QueryCache` (`DataContext/Cache/QueryCache.cs`, 22 строки,
  `internal sealed`, реализует `IQueryCache`) владеет слотом `AnyCommand` и маршрутизирует
  `PurgeQueryCache` в провайдерский store (`QueryPlanStore.Clear` для SQL-контекстов,
  `_cmdIdx.Clear` для in-memory). Семантика очистки у них **разная**, поэтому она передана
  делегатом `Action`, а не унаследована. Оба коллаборатора **переиспользует `InMemoryDataContext`** —
  это и есть второй потребитель, обосновывающий вынос: до шага 4 он объявлял те же
  `Logger`/`CommandLogger`/`Properties`/`AnyCommand`/`NeedMapping` inline. Попутно из `DataContext`
  удалены ~180 строк закомментированного кода и пять неиспользуемых `using`.
  `DataContext` 397 → **235** строк (**−83%** от исходных 1357), `InMemoryDataContext` 1706 → 1711.
- **Итог.** Все пять осей вынесены. `DataContext` оставляет себе жизненный цикл, точку расширения
  провайдера (abstract/virtual хуки) и тонкие делегаты на коллабораторы; SRP-тест «одно
  предложение без *и*» проходит: контекст управляет жизненным циклом и является composition root
  своих коллабораторов. **Изменение формы:** `LogSensitiveData` из `protected internal readonly`
  поля стал `protected internal` свойством (для наследников читается так же).
- **Замер шага 4.** build 0/0; core **154**, sqlite **179**, postgres **150**, sqlserver **166**
  (Failed 0); integration **833, Failed 0, Skipped 23** (~30 c, контейнеры поднялись, не ловушка
  «Skipped 376»). Бенчмарки `ParamsAllocationBenchmark` (full mode, Release, 3 прогона до / 3
  после, медианы): `EntityAnyCommand_Build` 1.603 → 1.672 µs (**+4.3%**), `BuildAndPrepare`
  5.417 → 5.506 µs (**+1.6%**), `Nextorm_EntityAny_1Arg_Span` 12.674 → 12.993 µs (**+2.5%**) —
  все в пределах разброса между прогонами (до 19%), аллокации не изменились (2.55 / 5.32→5.46 /
  2.94 KB). Регресса не обнаружено: лишняя делегирующая индирекция `AnyCommand` находится на
  холодном пути построения команды и в пределе шума.
- **Риск.** SQL-рендеринг на горячем пути: перед дальнейшим дроблением зафиксировать
  benchmark-базу (`docs/specs/performance/performance-findings.md`). **Отдельно про измерения:** быстрый режим
  (`Job.ShortRun` + InProcess — дефолт) на этом дереве даёт разброс **до ±30% на одном и том
  же коде** (два прогона подряд: `Cached_PlanOnly_Param` 528.9 vs 378.1 µs, `Cached_ToList`
  1577 vs 2074 µs), поэтому выводы о регрессах меньше ~30% по нему делать нельзя. Надёжный
  `NEXTORM_BENCH_FULL=1` (`Job.Default`) сейчас **не собирается**: `ValueList.cs` даёт CS1587
  (XML-комментарий не на том языковом элементе).

---

## F2. `IDataContext` — fat interface (ISP, Высокая)

**Доказательство.** `DataContext/IDataContext.cs` — 122 строки, ~60 членов. Смешаны:
управление соединением (`EnsureConnectionOpen`, `:57-58`), исполнение
(`ToList/First/Single` × sync/async, `:92-115`), создание энумераторов (`:82-91`),
фабрика команд (`CreateCommand`, `:25-34`), метаданные (`Create<T>`, `:15-24`), кэш
(`PurgeQueryCache`, `:120`), подготовка плана (`GetPreparedQueryCommand`, `:81`), плюс
~15 default-extension методов (`Any`, `GetAsyncEnumerable`, `From`).

`InMemoryDataContext` вынужден реализовывать весь контракт, включая no-op
`EnsureConnectionOpen() { }` (`InMemoryDataContext.cs:48` в `afb0189`) — классический признак
нарушения ISP.

**Первопричина.** Один интерфейс «на всё» вместо ролевых.

**Влияние.** Моки/фейки неподъёмные; каждый провайдер реализует поверхность, часть
которой ему не нужна; нет compile-time гарантий роли.

**Исправление (реализовано).** Интерфейс разбит на ролевые, `IDataContext` стал пустым
композитом; provider-независимые оверлоады вынесены в extension-методы. Коллабораторы
получают самый узкий достаточный тип (`IQueryMaterializer`), а не весь контекст. Детали —
в «Статусе реализации».

---

## F3. Протекающий контракт и дублирование guard-проверок (LSP/DRY, Средняя)

> Подробный разбор (механика, почему это не OCP, варианты исправления, риски) — в
> **Приложении A**.

**Замысел (легитимный).** Контекст полностью инкапсулирует работу со стораджем: в
`InMemoryDataContext` нельзя подсунуть команду реляционного провайдера и наоборот. Проверка
`is` здесь — это **guard инварианта**, а не механизм диспетчеризации.

**Доказательство (состояние до рефакторинга).** `DataContext.cs` — 14 вхождений
`if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery) ...
else throw new NotSupportedException`, и 13 симметричных в `InMemoryDataContext.cs`
(`is InMemoryPreparedQueryCommand<TResult>`), плюс downcast
`(DbPreparedQueryCommand<TResult>)planCache` (`DataContext.cs:384`). Текущее состояние — A.2.

**Первопричина.** `IDataContext.ToList<TResult>(IPreparedQueryCommand<TResult> cmd, …)`
публично принимает *любой* импл, а поддерживает только «свой». Ограничение выражается
рантайм-проверкой вместо типа, поэтому неверная пара «команда ↔ контекст» представима в
типах и падает в рантайме.

**Влияние (что осталось).**
1. **Контракт (LSP):** `IDataContext.ToList(IPreparedQueryCommand<TResult>, …)` принимает
   *любой* импл, а поддерживает только «свой» — неверная пара падает в рантайме.
2. **Вход для смешивания** — public default-методы интерфейса
   (`DataContext/Cache/IPreparedQueryCommand.cs:33`); guard в контексте — лишь последняя линия обороны.

**Устранено в ходе рефакторинга.**
- **DRY/SRP:** было 27 копий guard'а — стал единый `AsDbCommand`/`AsInMemoryCommand`,
  вызываемый на входе каждого публичного метода.
- **Тип исключения:** `ArgumentException` с сообщением «Expected …, got …» вместо
  `NotSupportedException`.
- **`IsScalar`:** член контракта, который in-memory реализовывал через
  `throw NotImplementedException`, убран из `IPreparedQueryCommand`; реляционный контекст
  определяет scalar-режим по `MapDelegate is null`.

OCP здесь **не нарушается**: все реляционные провайдеры переиспользуют один
`DbPreparedQueryCommand<TResult>`, поэтому новый реляционный провайдер не требует правок
(подробнее — A.4).

**Исправление (остаток, опционально).** Закрыть контракт: сузить параметры методов до
конкретного типа команды (тогда смешивание — ошибка компиляции) либо сделать интерфейс
generic по контексту (`IPreparedQueryCommand<TContext, TResult>`). Детали и компромиссы — A.7.

---

## F5. `NotImplementedException`/`NotSupportedException` как базовое поведение (LSP, Средняя)

**Доказательство.**

- база бросает: `CreateConnection` (`:138`), `CreateParam` (`:480`), `MakePage` (`:493`),
  `EmptySorting` (`:491`), `MakeParam` (`:574`). Из них **`EmptySorting` — необязательный**
  (охраняется `RequireSorting`, его переопределяет только SQL Server), остальные
  переопределяют все три провайдера;
- `internal string GetColumnName(...)` — не virtual и **всегда** бросает — мёртвый
  контракт; **удалён**;
- `public virtual string GetTableName(Type)` — **не переопределён ни одним провайдером**,
  всегда бросал, но достижим из fallback-ветки `GetFrom` (`:583`, тип без записи в
  `DataContextCache.Metadata`) — мнимый хук; **удалён**;
- `SqliteDataContext.MakeCount(distinct, big: true)` бросал `NotSupportedException`,
  тогда как база и PostgreSQL флаг `big` игнорируют, а SQL Server его обрабатывает —
  подстановка подтипа ломала программу; **переопределение удалено**;
- `Projection<T1,T2>.Extend` работал, а `Projection<T1,T2,T3>.Extend` бросал при одном
  контракте `IProjection`; **`Extend` вынесен в `IExtendableProjection`**;
- `JoinedEntityBuilder.Join` бросает при `Condition is not null`
  (`Builders/Joins/JoinCommandBuilder.cs:25-26`), хотя базовый `EntityBuilder<TEntity>.Join` —
  нет (нюанс: это `new`-hiding, а не override).

**Первопричина.** Опциональные хуки с «throwing default»; подтипы усиливают
пред-условия / ослабляют пост-условия.

**Влияние.** Вызывающий не может полагаться на базовый контракт; ошибки всплывают в
рантайме на конкретном диалекте/арности.

**Исправление (реализовано полностью).** См. «Статус реализации»: `GetColumnName` и
`GetTableName` удалены, `Projection<T1,T2,T3>.Extend` убран, SQLite-бросок в `MakeCount`
снят. Затем вместе с `ISqlDialect`: `MakeParam`/`MakePage` переехали в интерфейс диалекта
(обязательные там), `MakeCount`/`MakeTop` — в `SqlDialectBase`/диалекты, а пара
`RequireSorting`+`EmptySorting` сведена в один `GetPagingOrderBy`, возвращающий `null`
вместо броска. `DataContext` объявлен `abstract`, `CreateConnection`/`CreateParam` и
`Dialect` — `abstract`. **Бросающих дефолтов в базе больше нет** — в `DataContext` не
осталось ни одного `throw new NotImplementedException`.

---

## F6. Зависимость от конкретного `DataContext` (DIP, Средняя)

**Доказательство.** `SqlBuilder` принимает `DataContext dbContext`
(`DataContext/SqlBuilder.cs:13,20`), `BaseExpressionVisitor` — `DataContext dataProvider`
(`Visitors/BaseExpressionVisitor.cs:10,26`), тогда как `QueryCommand` уже работает через
`IDataContext` (`Query/QueryCommand.cs:62`). Инстанцирование — `new SqlBuilder(this, ...)`
(`DataContext.cs:178`).

**Первопричина.** Абстракция есть только на внешней границе; внутренние коллабораторы
привязаны к конкретному провайдеру.

**Влияние.** In-memory провайдер не может переиспользовать SQL/visitor-конвейер;
невозможно подставить тестовый диалект; риск циклической зависимости сборок.

**Исправление (частично).** Введён `ISqlDialect`: `SqlBuilder` принимает его вместо
`DataContext`, `BaseExpressionVisitor`/`WhereExpressionVisitor` — `ISqlDialect` + `ILogger?`,
`DataContext.MakeSelect` создаёт builder из `Dialect`. Диалект тестируется/подставляется в
изоляции (SQL-тесты теперь проверяют `ctx.Dialect`), in-memory не обязан реализовывать
SQL-конвейер. Вторая половина остатка — ось маппинга — закрыта позже: `ReplaceMemberVisitor`
оказался мёртвым кодом и удалён, построение маппера вынесено в `RowMapperFactory`, а политика
колонки передаётся делегатом (см. F1, ось маппинга).

**Остаток (дозакрыт частично).** Ранее `BaseExpressionVisitor` статически тянулся в `DataContext`:
10 обращений к `DataContext._sbPool` и `DataContext.GetParamName`, плюс 2 обращения к тому же пулу из
`ResultSetEnumerator`. Статическая связка снята:

- `StringBuilderPool.Shared` (`DataContext/StringBuilderPool.cs`) — единственный пул на процесс;
  `SqlBuilder`, визиторы и `ResultSetEnumerator` берут буфер из него, а не из `DataContext`.
- `BaseExpressionVisitor` и `ResultSetEnumerator` принимают `ObjectPool<StringBuilder>?` последним
  необязательным параметром и по умолчанию используют `StringBuilderPool.Shared`; вложенные
  визиторы получают пул родителя, поэтому подмена пула (тестовый шов) сквозная.
- `norm_pN` — контракт между SQL-текстом и `DbCommand.Parameters` — вынесен в `NormParam`
  (`DataContext/NormParam.cs`, `GetName`/`IsName`): им пользуются визитор
  (`BaseExpressionVisitor.cs:177`) и `DbPreparedQueryCommand`
  (`DataContext/Cache/DbPreparedQueryCommand.cs:60`). `DataContext.GetParamName` удалён, приватный
  `IsRuntimeParam` делегирует в `NormParam.IsName` (`DataContext.cs:203`).

**Было не закрыто (диагноз до правки).** `ResultSetEnumerator` держал конкретный `DataContext`: поле
`_dbContext` (`ResultSetEnumerator.cs:13`), публичное свойство `DataContext` (`:46`),
`InitEnumerator(DataContext, …)` (`:145`, вызовы — `DataContext.cs:679,867`) и чтение/запись
внутреннего флага `_connOpen` (`:158,165,180,188`). Публичное свойство `DataContext` (`:46`) при
этом читает и сбрасывает `DbPreparedQueryCommand` (`:117-118`), т.е. связка двусторонняя. От
статического пула тип отвязан, от класса — нет. Прежняя формулировка «зависимостей нет, осталось упоминание в комментарии»
была неточной: она описывала только статическую часть.

**Вторая связка, ранее не отмеченная.** `DbPreparedQueryCommand.GetDbCommand(…, DataContext, …)`
(`DataContext/Cache/DbPreparedQueryCommand.cs:35,38`) тоже принимает конкретный `DataContext`, и в
«Не закрыто» он не значился. Существенно, что **вся** эта зависимость — один вызов:
`dataContext.CreateParam(paramName!, @params[i])` (`:99`). `CreateParam` — `public abstract` на
`DataContext` (`:510`), переопределён всеми тремя провайдерами.

**Почему «сузить параметр до роли» сегодня не проходит.** `_connOpen` — `internal`-поле
`DataContext` (`:28`), а в `IConnectionManager` его нет: роль даёт только `GetConnection()` и
`EnsureConnectionOpen(Async)` (`Roles/IConnectionManager.cs:13-15`). Пока энумератор читает и
пишет это поле напрямую, сужение `InitEnumerator` до роли невозможно.

**Блок открытия соединения продублирован 4 раза:** `DataContext.GetDbCommand<TResult>` — span-перегрузка
(`:230`, блок `:238-248`) и async (`:256`, блок `:266-277`), плюс `ResultSetEnumerator.InitReader`
(`:158-166`) и `InitReaderAsync` (`:180-189`); при этом `EnsureConnectionOpen(Async)` (`:76`,
`:88`) уже выражает ту же логику один раз и входит в роль. Различие: в хелперах `DataContext` и в
энумераторе блок стоит под внешним `if (!_connOpen)`, а `EnsureConnectionOpen` проверяет состояние
соединения всегда (и выставляет `_connOpen = true` безусловно).

**✅ Закрыто (реализовано).** Шов оказался ровно тем, что был намечен выше.

- **`CreateParam` — делегатом.** `DbPreparedQueryCommand.GetDbCommand` принимает
  `Func<string, object?, DbParameter>` вместо `DataContext`; вызов по-прежнему один — `:99`.
  `DataContext` связывает абстрактный метод в поле один раз (`_createParam = CreateParam` в
  конструкторе), поэтому провайдерский override остаётся на пути диспетчеризации, а делегат не
  аллоцируется на команду.
- **`ResultSetEnumerator` — на роль.** Поле `_dbContext` заменено на `IConnectionManager` +
  делегат создания параметров; публичное свойство `DataContext` удалено; дублированный блок
  открытия соединения убран в пользу `EnsureConnectionOpen(Async)`; `ResetConnection` дёргает
  `DetachFrom(IDataContext)`. Логирование (`ResultSetEnumeratorLogger`, `LogSensitiveData`)
  передаётся один раз через `InitEnvironment`, а не вытягивается из контекста свойством.
- **`InitEnumerator` стал `internal`** — вызывается только из `DataContext` (`:677,865`), публичной
  поверхностью быть не обязан.
- **Токен отмены сохранён:** `IConnectionManager.EnsureConnectionOpenAsync` получил
  `CancellationToken cancellationToken = default` (роль реализует только `DataContext`), иначе
  отмена при открытии соединения потерялась бы.
- **Дедупликация:** блок открытия соединения жил **4 раза** — `DataContext.GetDbCommand<TResult>`
  (span и async) и `ResultSetEnumerator.InitReader`/`InitReaderAsync`. Осталось одно выражение —
  `EnsureConnectionOpen(Async)`; оба хелпера `DataContext` теперь тоже зовут его.

Проверено по коду: `DataContext` в `ResultSetEnumerator.cs` и `DbPreparedQueryCommand.cs` — **0**
вхождений в коде (остались только упоминания в комментариях). В `src` конкретный `DataContext`
теперь фигурирует лишь в самом типе, DI-слое, провайдерских подклассах и комментариях.

Замер (`ParamsAllocationBenchmark`, fast/ShortRun, до/после): аллокации **идентичны** —
`GetDbCommand_1Arg_ReusedArray` 2.34 KB, `_Params` 5.47 KB, `Nextorm_Any_1Arg_Params` 85.16 KB,
`_2Arg_ReusedArray` 117.2 KB; тайминги в пределах шума. То есть рефакторинг перф-нейтрален.
Интеграционный набор: **561, Failed 0, Skipped 16**.

Цена (осознанная): «Opening connection» теперь логирует `Logger` контекста, а не
`ResultSetEnumeratorLogger`; `EnsureConnectionOpen` проверяет `conn.State` на каждом вызове
(чтение enum).

**Ревалидация после параллельной работы** (CTE, `Distinct`, `JoinType`, IN-транслятор,
string/math-функции, `Not`). SQL-генерация осталась чистой: `DataContext` не упоминается ни в одном
из ключевых файлов — `SqlBuilder`, `BaseExpressionVisitor`, `WhereExpressionVisitor`, `CteQuery`,
`InValuesEvaluator`, `QueryCommand.Prepare`, `SelectExpression`, `EntityBuilder`, `JoinCommandBuilder` —
**0** вхождений в каждом. Новый `CteQuery` зависит от `IDataContext` (абстракция), а не от класса,
т.е. регресса F6 параллельная работа не внесла.

Проверено: `DataContext._sbPool` / `DataContext.GetParamName` в `src` — **0** вхождений; сборка 0/0;
контракт `norm_pN` явно утверждается в `SqlGenerationTests.cs` всех трёх провайдеров (7 проверок)
и проходит.

---

## F7. Пустой алиас + перегруженный `IQueryRegistry` (ISP/YAGNI, Низкая)

**Доказательство.** `Query/IQueryContext.cs` — `public interface IQueryContext :
IQueryRegistry {}` без членов. Во всём решении он встречался ровно 3 раза: объявление,
`QueryCommand : IQueryContext` и параметр `DataContext.MakeSelect(..., IQueryContext, ...)`,
который сразу уходил в `SqlBuilder`, где тип параметра — уже `IQueryRegistry`. Ни одной
проверки `is IQueryContext`, constraint, DI-скана или рефлексии нет; тестовый
`QueryProvider : IQueryRegistry` (`PreciseExpressionEqualityComparerTests.cs`) работал, не
реализуя `IQueryContext`. Отдельно: `Query/IQueryRegistry.cs` обязывает реализации давать
6 фабрик equality-comparer'ов — это хеширование планов, а не «провайдер запроса»; их
единственные потребители — классы-компараторы.

**Первопричина.** Алиас вместо абстракции: имя `IQueryContext` обещало больше, чем базовый
интерфейс, но не добавляло ни контракта, ни наблюдаемости. В базовом `IQueryRegistry` слиты
две роли: «граф запроса» и «хеширование плана».

**Влияние.** Алиас — лишнее имя в публичном API, оно вводит в заблуждение (обещает
«контекст») и отсекает валидные реализации: `QueryProvider : IQueryRegistry` нельзя передать
туда, где ждут `IQueryContext`. ISP-часть на сегодня **теоретическая**: реализаций
`IQueryRegistry` в проде одна (`QueryCommand`) плюс один тестовый дабл.

**Исправление.**
- **Сделано:** `IQueryContext` удалён (файл + 2 ссылки); `QueryCommand : IQueryRegistry`,
  `MakeSelect` принимает `IQueryRegistry`. Интерфейсов стало меньше, поведение не изменилось.
- **Отложено (YAGNI):** выделение `IPlanComparerFactory` и
  `IQueryContext : IQueryRegistry, IPlanComparerFactory` дало бы узкие зависимости, но
  второй реализации/потребителя нет, а тестовый дабл платит лишь 6 однострочных заглушек.
  Вернуться к рефактору при триггере: (1) второй реальный `IQueryRegistry`;
  (2) компонент, которому нужен провайдер **без** comparer-машинерии (внешний/сериализуемый
  кэш планов); (3) публичные comparer'ы со своими потребителями.

---

## F8. DI-регистрации: дублирование и двойной инстанс (DRY/DIP, Средняя)

**Доказательство.** `DI/ServiceCollectionExtensions.cs`: 6 почти одинаковых оверлоадов;
тела `Action<DataContextBuilder>`- и `Action<IServiceProvider,DataContextBuilder>`-вариантов
дублируются (`:7-24` против `:43-61`; keyed — `:25-42` против `:62-80`).
`AddNextOrmContext<T>` регистрирует `IDataContext -> T` и `T` **отдельно** (`:84-85`) ->
два разных экземпляра контекста в одном scope; при `optionsBuilder == null` резолвится
незарегистрированный `DataContextBuilder` (`:19-23`, `:56-60`) -> `InvalidOperationException`
в рантайме (в тестах спасает только `AddNextOrmContext<InMemoryDataContext>`).

> Номера строк — до фикса; файл с тех пор переписан (см. «Исправление»).

**Первопричина.** Копипаста оверлоадов без общей фабрики; нет единой регистрации на роль.

**Влияние.** Тонкие DI-баги; потребитель `T` и потребитель `IDataContext` видят разное
состояние.

**Исправление (реализовано).** Шесть оверлоадов сведены к двум приватным хелперам:
`RegisterContextFactory` (options-driven: scoped `DataContextBuilder` + `IDataContext`-фабрика) и
`RegisterContextType<T>` (type-driven: scoped `T`, затем `IDataContext -> sp.GetRequiredService<T>()`);
в каждом — вариант для keyed-сервисов. Что починено:

- `AddNextOrmContext<T>` / `AddKeyedNextOrmContext<T>` больше не регистрируют `T` и `IDataContext`
  независимо — один инстанс на scope (потребитель `T` и потребитель `IDataContext` видят одно
  состояние);
- options-делегат стал обязательным: `ArgumentNullException` на регистрации вместо
  `InvalidOperationException` на резолве (`DataContextBuilder` без options не может создать контекст);
- 4 фабричных оверлоада делегируют в один хелпер вместо копипасты тел.

Покрыто `tests/nextorm.core.tests/DependencyInjectionTests.cs` (5 тестов: identity `T`/`IDataContext`,
keyed-вариант, переиспользование scoped-билдера, fail-fast на `null`).

---

## F9. `BaseExpressionVisitor` — God class + switch на именах (SRP/OCP, Средняя)

**Доказательство (сверено с текущим деревом).** `Visitors/BaseExpressionVisitor.cs` —
**439 строк** (в `afb0189` было 1048; на предыдущую сверку — 2128, затем декомпозиция).
Класс разобран на ~18 translator-классов: `PredicateTranslator`, `MemberTranslator`,
`ScalarFunctionTranslator`, `BuiltinFunctionTranslator`, `NormSqlTranslator`,
`SqlOperandTranslator`, `AdvancedAggregateTranslator`, `ArraySqlTranslator`, `JsonSqlTranslator`,
`TextJsonSqlTranslator`, `WindowFunctionTranslator`, `InValuesTranslator`, `AggregateFilter`,
`TypeFacts`, `SqlLiteral`, `VisitorOptions`, `AliasResolver`, `WindowSql`, `TypedParamVisitor`.
Базовый класс оставляет себе обход `ExpressionVisitor` и делегирует (`VisitCondition`,
`VisitConditional`, `VisitSwitch`, `VisitBinary` → `PredicateTranslator`); `NotImplementedException`
— **4** (было 6).

**Остаток (OCP) — закрыт 18.09.2026.** `VisitMethodCall` больше не содержит pattern-switch по
`nameof(TableAlias.*)`: добавлен `Visitors/TableAliasAccessors.cs` (таблица `FrozenSet<string>` из
публичных методов `TableAlias` + признак «колоночный аргумент может быть вычисляемым выражением»,
что верно только для `GetColumn`). Добавление нового аксессора `TableAlias` теперь не требует
правки визитора.

**Влияние (историческое).** До декомпозиции новый метод `TableAlias` требовал правки визитора,
а класс совмещал обход выражений, рендеринг SQL, разрешение алиасов и работу с параметрами.

**Исправление — выполнено.** Разбивка по конструкциям сделана (translator-классы выше), switch по
именам заменён таблицей.

---

## F10. Дублирование агрегатов в `EntityBuilder<TEntity>` (DRY, Низкая)

**Доказательство.** `Builders/EntityBuilder.cs:448-575` — Min/Max/Avg/Sum/Stdev/Stdevp/Var/Varp.
Каждое семейство — 5 членов (`X(exp)`, `X(exp, params ReadOnlySpan<object?>)`, приватный
`XCore`, `XAsync(exp, params object[])`, `XAsync(exp, CancellationToken, params object[])`),
итого **40 членов**. Все 8 sync-тел и 8 async-тел отличаются **только** `MethodInfo`
(`CommonFunctions.MinMI`/`MaxMI`/`AvgMI`/`SumMI`/`StdevMI`/`StdevpMI`/`VarMI`/`VarpMI`);
остальное — `Expression.Call(CommonFunctions.SQLExpression, MI.MakeGenericMethod(typeof(TResult)),
exp.Body)` + `cmd.SingleRow = true` + `ExecuteScalar(...)` — побайтово одинаково.

**Влияние.** Низкое (путь не горячий: агрегат выполняется раз на запрос, а не на строку
результата), но «Rule of Three» превышен восьмикратно — 16 копий формулы и 16 копий
`SingleRow`/`ExecuteScalar`. Добавление перегрузки `params ReadOnlySpan<object?>` приходится
повторять 8 раз.

**Исправление (реализовано).** Все 16 тел свёрнуты в два приватных ядра, принимающих
`MethodInfo`:

- `AggregateCore<TResult>(MethodInfo sqlMethod, exp, ReadOnlySpan<object?> @params)` — sync-путь;
- `AggregateAsyncCore<TResult>(MethodInfo sqlMethod, exp, CancellationToken, object[] @params)` —
  async-путь.

Публичные члены стали однострочными форвардерами
(`=> AggregateCore(CommonFunctions.MinMI, exp, @params)`), атрибуты
`[MethodImpl(AggressiveInlining)]` на async-обёртках сохранены как были. **Публичные сигнатуры
не изменены ни в одной из 40 перегрузок** — это внутренняя чистка, а не правка API. Итог:
`Builders/EntityBuilder.cs` 674 → **602** строки (−72), приватных ядер 8 → 2, копий формулы —
16 → 1, копий `SingleRow`/`ExecuteScalar` — 16 → 2. Стоимость на вызов не изменилась:
`MakeGenericMethod` и построение дерева выражений как доминировали, так и доминируют;
передача `MethodInfo` параметром вместо обращения к static-полю — одна загрузка поля.

**Не входит в дефект (проверено).**
- `First/FirstOrDefault/Single/SingleOrDefault` — не дублирование логики, а форвардинг на
  *разные* методы `QueryCommand`; схлопнуть нельзя (разные возвращаемые типы/нуллабельность,
  а `ReadOnlySpan` не может быть generic-аргументом). См. «Что НЕ является нарушением».
- Асимметрия `params ReadOnlySpan<object?>` (sync) / `params object[]` (async) намеренная:
  state machine async-метода не может держать ref struct. Объединять нельзя.
- `Count` сознательно оставлен отдельно: его выражение строится как
  `Select(e => SqlFunctions.Sql.count())`, то есть через LINQ-форму, а не через
  `CommonFunctions.SQLExpression` + `MethodInfo`, поэтому в `AggregateCore` он не укладывается без
  смены транслируемого выражения. `Any` — тоже отдельно (свой `GetAnyCommand` +
  `ReplaceCommand`).

**Наблюдения попутно (не часть исправления).**
- `[MethodImpl(AggressiveInlining)]` стоит только на async `params object[]`-обёртках; на
  sync-обёртках его нет — расхождение без обоснования.
- Безпараметрические перегрузки `X(exp)` избыточны: `params ReadOnlySpan<object?>` допускает
  вызов без аргументов (проверено), т.е. для совместимости они не нужны. Удаление — отдельное
  решение: это breaking change публичного API, а не DRY-чистка.

**Покрытие тестами.** Исходно из 8 семейств были покрыты 5 (`Min`/`Max`/`Avg`/`Sum`/`Stdev`),
и только безпараметрическая sync-перегрузка: `Stdevp`, `Var`, `Varp` и **все** async-варианты не
исполнялись ничем. Дописаны характеризационные тесты
(`tests/nextorm.integration.tests/CommonTestSuite.Aggregates.cs`: 109 → 378 строк, 24 теста) —
`Stdevp` (в т.ч. точная `double`-проекция через `BeApproximately`), `Var`/`Varp`, все 8
`*Async` и 10 тестов на `params`-перегрузку (через `SqlFunctions.Parameter<int>(0)`). Ожидаемые значения
считались по формулам (для 1..10: sample var = 82.5/9, population var = 82.5/10), а не
подгонялись под вывод. Заодно доказано, что int-проекция **округляет** (`Convert.ToInt32`,
midpoint-to-even), а не усекает: `Stdevp` = 2.8722813232690143 → 3 (усечение дало бы 2).

**Остаточный пробел (закрыт).** SQLite регистрировал только `stdev`/`stdevp`, поэтому
`EntityBuilder.Var`/`Varp`/`VarAsync`/`VarpAsync` падали с «no such function». В
`src/nextorm.sqlite/SQLiteFunctions.cs` добавлены агрегаты `var`/`varp`: аккумулятор (он уже нёс
`Count`/`Sum`/`SumSq`) переименован в `VarianceAccumulator`, а его `Final` разделён на
`FinalVariance` — она возвращает **дисперсию**, `stdev`/`stdevp` берут от неё `Math.Sqrt`,
`var`/`varp` возвращают напрямую. Одна реализация на четыре функции вместо двух копий; значения
`stdev`/`stdevp` не изменились. `ITestProvider.SupportsVarianceAggregates` для SQLite → `true`.

Маппинг имён не потребовался: `SqliteDialect` не переопределяет `MakeAggregate`, поэтому
`var`/`varp` уходят в SQLite как есть и совпадают с именами зарегистрированных функций.
Postgres для сравнения маппит `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
`varp`→`var_pop`.

Отдельно: `docs/specs/roadmap/sql-capabilities-gap-analysis.md` помечает агрегаты `**yes**` на уровне
**трансляции выражений**. После этой правки пометка верна и для рантайма SQLite, так что
оговаривать её больше не нужно.

**Проверка (финальная).** Сборка 0/0; unit-проекты core **107**, sqlite **136**, postgres
**90**, sqlserver **114** — везде Failed 0; `f4check` — ALL OK.

Агрегаты проверены **на всех трёх провайдерах**: после подключения Podman
(`DOCKER_HOST=unix:///mnt/wsl/podman-sockets/podman-machine-default/podman-user.sock`) Postgres
и SQL Server перестали скипаться целиком — integration 561, скипов 16, падений по агрегатам 0.
В `SqliteIntegrationTests` скипы **10 → 2**: минус 8 там, где ожили `Var`/`Varp` и их
async-варианты; оставшиеся 2 — `INTERSECT ALL`/`EXCEPT ALL`, отсутствующие в SQLite.

Промежуточный прогон (до регистрации `var`/`varp`) давал integration 453 → 525 и баланс скипов
304 → 360, дельта +56 = SQLite 8 + Postgres 24 + SQL Server 24.

**Исправление.** Приватные `AggregateCommand<TResult>(Expression, MethodInfo)`,
`AggregateCore<TResult>(..., ReadOnlySpan<object?>)` и `AggregateAsyncCore<TResult>(...,
CancellationToken, object[])` (без `params` — чтобы не переаллоцировать массив), плюс тонкие
публичные обёртки. `MI` упоминается по-прежнему дважды на семейство (sync + async), поэтому
риск перепутать `MI` не растёт, а формула остаётся в одном месте: ~128 строк → ~52. Source
generator отклонён по YAGNI (build-time машинерия ради 8 методов). Предусловие —
характеризационные тесты — **выполнено** (см. «Покрытие тестами»), кроме `Var`/`Varp`:
они локально неисполнимы из-за пробела в SQLite.

---

## F11. Параллельные реализации кэшей (DRY, Низко-средняя)

**Доказательство (сверено с текущим деревом).** Исходная формулировка была неточна в главном: это
не «инстансные копии», а **второй набор статических кэшей**, который оказался мёртвым.
`DataContextCache` держит `_metadata`, `_selectListCache`, `_expCache`, `_inValuesCache`;
`InMemoryDataContext` держал собственные `_metadata` и `_selectListCache` (тоже `static`) и
инстансный `_expCache`. Проверка по коду показала:

- писателей в `InMemoryDataContext._metadata`/`_selectListCache` — **0** (метаданные наполняются через
  `DataContextExtensions` только в `DataContextCache`; селект-листы — в `QueryCommand.Prepare`);
- читателей свойств `Metadata`/`SelectListCache`/`ExpressionsCache` — **0** в `src` и **0** в `test`;
- ни один ролевой интерфейс их не объявляет (`IDataContext` — пустой композит 4 ролей;
  `IQueryCache` — только `AnyCommand` + `PurgeQueryCache`).

То есть это были не два источника истины, а всегда-пустая копия, к которой никто не обращался.

**Важное уточнение по `_expCache`.** Его инстансный scope — **не дефект и не расхождение**, а
требование. Выражения, которые в него кладутся, содержат `Expression.Constant(this)`
(`GetPreparedQueryCommand`, `BuildCreateEnumeratorDelegate`), поэтому скомпилированный делегат
вызывает метод **того** контекста, для которого собран. Сделать кэш process-wide — значит отдать
вызывающему чужой инстанс. Глобальными могут быть только провайдер-независимые
`Metadata`/`SelectListCache`; `MapperCache` (process-wide) и `DataContext._queryPlanCache`
(per-thread, ключ `QueryPlanCacheKey(ContextType, Plan)`) — отдельные понятия, не дубли.

**Влияние (было).** Два объявления одного понятия; `Metadata`/`SelectListCache` у in-memory
навсегда пустые, что маскировало бы ошибку при чтении их через контекст; scope кэшей нигде не был
зафиксирован и выводился археологией.

**Исправление (сделано).** Единый источник — `DataContextCache`. У `InMemoryDataContext` удалены
`_metadata`/`_selectListCache` и осиротевший `using System.Collections.Concurrent`; свойства
`Metadata`/`SelectListCache` делегируют в `DataContextCache` (публичная сигнатура сохранена).
`_expCache` оставлен инстансным с документирующим комментарием; `_conditionFactoryCache` /
`_conditionDirectCache` помечены как in-memory-специфичные (SQL-аналога нет — не дубли). Область
шаринга **явно задокументирована** в `<remarks>` у `DataContextCache`: process-wide — он и
`MapperCache`; per-instance — `InMemoryDataContext.ExpressionsCache`; per-thread —
`QueryPlanStore` (вынесен из `DataContext` на шаге 2 F1). Регресс закрыт `tests/nextorm.core.tests/DataContextCacheScopeTests.cs`
(4 факта: `BeSameAs` для общих кэшей, `NotBeSameAs` — для инстансного и для двух контекстов).

---

## F12. `ISqlDialect` — фат-интерфейс на 97 членов (ISP, Средняя)

**Доказательство (сверено с текущим деревом).** `DataContext/Dialect/ISqlDialect.cs` — **502
строки, **97 членов** (34 флага-свойства + 63 метода), и ровно **одна** реализация по умолчанию. Шесть
провайдеров обязаны удовлетворить весь контракт: `SqliteDialect` (140 строк), `PostgresDialect`
(117), `MySqlDialect` (194), `MariaDbDialect` (14, наследник MySQL), `ClickHouseDialect` (213),
`SqlServerDialect` (337); остальное закрывает `SqlDialectBase` (325 строк). Это зеркало F2 на оси
диалекта: там фатированным был `IDataContext` (~60 членов), здесь — `ISqlDialect`.

**Первопричина.** При закрытии F1 (`Make*`/`Escape`/`Require*`/`MakePage`/`MakeTop` уехали из
`DataContext`) вся capability-поверхность собрана в один интерфейс. Потребителей много и они разные
(строки, даты, множественные операции, JSON, оконные функции, пагинация), но каждый транслятор
зависит от **всего** интерфейса.

**Влияние.** Потребитель, которому нужны три метода (`MakeUpper`, `MakeLower`, `MakeTrim`),
получает обязательство на 97. Тест/мок диалекта обязан реализовать всё; новый провайдер видит
500-строчный контракт целиком. `MariaDbDialect` (14 строк) показывает, что наследование
класса спасает, а интерфейс — нет.

**Исправление (предложение).** Разбить по capability-группам, как уже сделано для трансляторов:
`IStringFunctions`, `IDateFunctions`, `ISetOperations` (флаг `SupportsIntersectExceptAll` уже
есть), `IJsonFunctions`, `IWindowFunctions`, `IPagingSql`; сам `ISqlDialect` оставить композитом.
Транслятор объявляет зависимость только от нужной группы.

**Триггер.** Не «стало много методов», а **второй потребитель, которому нужна не вся
поверхность** — он уже есть: `BuiltinFunctionTranslator`/`ScalarFunctionTranslator` против
`WindowFunctionTranslator`.

**Актуализация (проверено на текущем дереве) — разбиение отложено осознанно.** Пересчёт по коду:
было «61 член» (устарело), стало **97** — 34 флага-свойства + 63 метода. Рост дал
JSON/array/advanced-aggregate слой. Однако измерение потребителей не подтвердило триггер в
решающей части:

- **Все потребители внутренние и берут диалект целиком.** `ISqlDialect` объявлен полем ровно в
  трёх местах: `SqlBuilder` (`DataContext/SqlBuilder.cs:18`), `BaseExpressionVisitor`
  (`Visitors/BaseExpressionVisitor.cs:18`, отдаётся наружу как `Dialect`) и `QueryPlanner`
  (`Func<ISqlDialect>`). Трансляторы обращаются к диалекту **только** через единый
  `visitor.Dialect` — то есть зависят от полного интерфейса, а не от «своей» группы.
- **Специфичные по капабилити методы вызываются одним-двумя файлами, и это те же файлы, что
  делают `Make*`-рендеринг.**
  `MakeUpper`/`MakeLower`/`MakeTrim`/`MakeSubstring`/`MakeReplace`/`MakePad`/`MakeStuff`/
  `MakeStringIndexOf`/`MakeStringLastIndexOf` → только `ScalarFunctionTranslator`;
  `MakeDateTrunc`/`MakeDateAdd`/`MakeDateDiff`/`MakeEndOfMonth`/`MakeDateFromParts`/`MakeStringAgg`/
  `MakeArrayAgg`/`MakeGreatest`/`MakeLeast`/`MakeFullText` → только `BuiltinFunctionTranslator`;
  `MakeIsJson`/`MakeTextJsonFunction` → только `TextJsonSqlTranslator`; `MakePage`/`MakeTop`/
  `GetPagingOrderBy`/`MakeWith`/`MakeMaxRecursion`/`MakeApply`/`MakeGrouping`/`MakeGroupingSets`/
  `RenderQueryHints`/`MakeTableHints`/`MakeForJson`/`MakeForXml` → только `SqlBuilder`. Ядро шире
  (`MakeParam` — 5 файлов, `MakeBooleanPredicate` — 4, `MakeAggregate` — 3), но и там все
  потребители — те же трансляторы с общим `visitor.Dialect`. По правилу
  проекта «абстракция только при **втором** потребителе» каждая группа дала бы интерфейс с одним
  потребителем — ровно та церемония, которую ревью отвергало в F3/F7 (`IConnectionFactory`,
  `IColumnMapper`).
- **Боли моков нет.** В `test`/`benchmarks` нет ни одного фейка `ISqlDialect`: тесты используют
  реальные синглтоны (`SqliteDialect.Instance` и т.п.), а `ISqlDialect` встречается в 6 тест-файлах
  только как тип статического поля.
- **Стоимость провайдера уже амортизирована.** Новый провайдер наследует `SqlDialectBase`
  (325 строк дефолтов); `MariaDbDialect` — **14 строк**. Размер интерфейса не барьер для провайдера.
- **Единственный член не про рендеринг** — `SupportsCommandBehaviorSingleRow`: это про поведение
  ADO.NET-драйвера (ClickHouse добавляет `LIMIT 1`), потребляется в одном месте
  (`QueryPlanner.cs:128`). Кандидат на переезд в ось исполнения, но выигрыш — 1 член из 97,
  отдельного шага не заслуживает.

**Вывод.** Формальное ISP-нарушение есть (клиент видит 97 членов), но практического вреда нет:
потребители замкнуты, внутренни и однородны, а разбиение добавило бы типов, не сузив ни одной
реальной зависимости. **Решение: не разбивать** (KISS; ср. F7). **Условия пересмотра:**
(1) трансляторы перестанут ходить через общий `visitor.Dialect` (например, переиспользование в
другом хосте); (2) понадобится фейк/мок диалекта для юнит-теста одной капабилити; (3) появится
внешний реализатор `ISqlDialect` без наследования `SqlDialectBase`; (4) новая капабилити будет
добавляться в интерфейс чаще, чем раз в релиз (ломает внешних реализаторов).

---

## F13. `InMemoryDataContext` — параллельный фасад исполнения (SRP/DRY, Средняя)

**Доказательство (сверено с текущим деревом).** `DataContext/InMemoryDataContext.cs` — **1706
строк**, `partial class InMemoryDataContext : IDataContext`. После выноса исполнения из `DataContext`
(шаг 1 F1: `QueryExecutor`, 452 строки) картина стала асимметричной: DB-путь — тонкий фасад плюс
три коллаборатора (`QueryExecutor`, `QueryPlanner`, `DbConnectionManager`), а in-memory
по-прежнему описывает **тот же 13-членный исполнительный фасад** inline: `CreateEnumerator`
(`:1386`), `CreateAsyncEnumerator` (`:1423`), `ToListAsync` (`:1430`), `ToList` (`:1456`),
`ExecuteScalar` ×2 (`:1483`, `:1496`), `First` (`:1510`), `FirstOrDefault` (`:1528`),
`FirstAsync` (`:1546`), `FirstOrDefaultAsync` (`:1565`), `Single` (`:1584`),
`SingleOrDefault` (`:1611`), `SingleAsync` (`:1635`) и далее.

**Важно:** это **не** дословный дубль — тела отличаются законно (in-memory сам применяет
`Paging.Offset`/`Limit`, у него нет `DbCommand`/`DbDataReader`, вместо них
`IEnumerator<TResult>`). Дублируется **структура и оркестровка**, а не строки.

**Влияние.** Два близнеца: `DataContext` (397 строк) разобран, `InMemoryDataContext` (1706) — нет.
Изменение формы исполнительного контракта (как шаг 1 F1) приходится делать дважды; читая один
контекст, нельзя считать, что понял второй.

**Исправление (реализовано — шаг 5).** Применён шаблон «тонкий фасад + коллаборатор»:
`InMemoryQueryExecutor` (`DataContext/InMemoryQueryExecutor.cs`, **336 строк**, `internal sealed`,
реализует `IQueryExecutor` + `IRowReaderFactory`) забрал исполнительный фасад — `CreateEnumerator`/
`CreateAsyncEnumerator`, `GetEnumerable` (override с прямым возвратом энумератора), `ToList(Async)`,
`ExecuteScalar` ×2, `First`/`FirstOrDefault(Async)`, `Single`/`SingleOrDefault(Async)`, а вместе с
ними приватные `AsInMemoryCommand`/`ToParams` и `EnumeratorEnumerable<TResult>`. `InMemoryDataContext`
делегирует эти члены полю `_executor` (тип — конкретный `sealed`-класс, инлайн без интерфейсной
диспетчеризации). Коллаборатор **самодостаточен**: фабрика энумератора живёт в самом
`InMemoryPreparedQueryCommand<TResult>` (делегат `CreateEnumerator`), а не в контексте, поэтому
контекст ему не нужен. `InMemoryDataContext` 1711 → **1446** строк (−265). Тела перенесены
**дословно**: общая с `QueryExecutor` только форма фасада, не реализация (у in-memory нет
`DbCommand`/`DbDataReader`, пагинация применяется при чтении).

**Что осталось на контексте (осознанно).** `CreateEnumerator<TResult,TEntity>`, `protected CreateAsyncEnumerator<TResult>(QueryCommand,…)` и `CreateEnumeratorAdapter` остаются на `InMemoryDataContext`, но после **фазы 8** — тонкими обёртками 1:1 по сигнатуре и видимости (тела вынесены в `InMemoryQueryBuilder`/`InMemoryJoin`). Это сохраняет адресацию через `MethodInfo` (`miCreateEnumerator`, `miCreateAsyncEnumerator`, `miCreateEnumeratorAdapter`) и встраивание в `Expression.Call(@this, …)` без изменения target. Инстансные кэши выражений (`_expCache`, `_condition…`, `_aggregateSelectorCache`, `_sortingSelectorCache`, `_linqSelectorCache`) по-прежнему на контексте — они захватывают `this` осознанно и передаются хелперам параметрами.

**Актуализация (18.09.2026, фазы 1–8 декомпозиции `InMemoryDataContext`).** Контекст 1446 → **351** строк (порог 500 достигнут). Новые `internal`-типы ≤500: `InMemoryQueryBuilder` 373, `InMemoryJoin` 157, `InMemoryLinqSource` 184, `InMemorySetOperations` 160, `InMemoryGrouping` 154, `InMemoryOrdering` 96, `InMemoryProjectionFactory` 87, `InMemoryConditionFactory` 75, `InMemoryRowMaterializer` 75. Открыта минимальная `internal`-поверхность: `mi*`-поля (`internal static readonly`) и геттеры кэшей (`CommandIndex`, `SortingSelectorCache`, `AggregateSelectorCache`, `ConditionFactoryCache`, `ConditionDirectCache`, `LinqSelectorCache`); публичный/`protected` API не изменён. Проверка: build 0/0; core **151**, sqlite **180**, integration SQLite **196**, Postgres **196** — Failed 0.

**Замер шага 5.** build 0/0; core **154**, sqlite **179**, postgres **150**, sqlserver **166**
(Failed 0); integration **833, Failed 0, Skipped 23** (~24 c). Бенчмарки in-memory (full mode,
Release, 2 прогона на плечо): `NextormPreparedSync` (GetEnumerable, 10k строк) 96.8 → **~98–105 µs**
(один выброс 112), `NextormPreparedSyncToList` 146.9 → **147.5 µs**, `NextormPreparedParam`
2.74 → **2.56 ms**; аллокации идентичны (234.38 KB / 312.55 KB / 22.66 KB). Лишняя делегирующая
индирекция — на вызов терминала, а не на строку, поэтому в шуме не видна.

---

## Что НЕ является нарушением (чтобы не переусердствовать с рефакторингом)

- `IEntityMetadata`/`IPropertyMetadata` (`Meta/*.cs`) — маленькие, связные, оправданные
  абстракции. Оставить как есть.
- `IAliasProvider`, `IParameterProvider`, `IColumnsProvider`, `IDbCommandHolder` — хорошо
  сегрегированы. Оставить как есть.
- Провайдерские подклассы `Sqlite/Postgres/SqlServerDataContext` — тонкие и
  целенаправленные; наследование здесь уместно (расширение диалекта).
- `MapperCacheKey` (readonly record struct) — корректный value-key, а не «IFoo/Foo ради
  галочки».
- `DefaultParameterProvider`/`GetParamName` — осознанная оптимизация (без `string.Format`),
  не нарушение.
- `First`/`FirstOrDefault`/`Single`/`SingleOrDefault` в `EntityBuilder<TEntity>`
  (`Builders/EntityBuilder.cs:295-372`) — форвардинг на разные методы `QueryCommand`; общей логики
  нет, схлопывание невозможно без потери типизации. Не путать с F10, где логика
  действительно продублирована.

## Приоритеты исправления

1. **P0** — F2 (ISP): ✅ **реализовано** — ролевые интерфейсы, `IDataContext` как пустой
   композит, вынос provider-независимых оверлоадов в `DataContextExtensions`.
2. **P1** — F1: ✅ **реализовано**: оси диалекта (`ISqlDialect`, + F6), маппинга
   (`RowMapperFactory`), **соединения** (`DbConnectionManager`), **исполнения** (`QueryExecutor`),
   **планирования** (`QueryPlanner` + `QueryPlanStore`) и **окружения/кэша**
   (`ContextEnvironment` + `QueryCache`, общие с `InMemoryDataContext`) вынесены; `DataContext`
   1357 → **235** строк.
3. **P1** — F5: ✅ **реализовано** — бросающих дефолтов в `DataContext` не осталось.
4. **P2** — F9: ✅ **реализовано** (439 строк + ~18 translator-классов; остаток — switch по
   `nameof(TableAlias.*)` в `VisitMethodCall`). F11 ✅ (единый `DataContextCache` + scope).
   F7: пустой алиас удалён, ISP-часть `IQueryRegistry` отложена по YAGNI (см. F7 — триггеры).
5. **P2 (новое, из актуализации)** — **F12** (`ISqlDialect`, 97 членов — **отложено осознанно**,
   разбиение не окупается) и **F13** (`InMemoryDataContext` — ✅ **реализовано**: исполнительный
   фасад вынесен в `InMemoryQueryExecutor`, контекст 1711 → **1446**).
6. **Реализовано:** F4, F3, F2, F8, F5, F9, F10, **F11**, **F1** (оси: диалект, маппинг,
   соединение, исполнение, планирование, окружение/кэш), **F13** (исполнительный фасад in-memory) —
   см. «Статус реализации». **F6 — закрыт:** снята и статическая связка (`_sbPool`, `GetParamName`),
   и зависимость исполнения от конкретного `DataContext` — см. F6, «✅ Закрыто».

## Оговорка для performance-работ

`docs/specs/performance/performance-findings.md` и `docs/specs/performance/performance optimizations.md` описывают оптимизации
горячего пути. Перед рефакторингом F1/F6 зафиксируйте benchmark-базу
(`docs/specs/performance/benchmark-report.md`); иначе есть реальный риск откатить уже сделанную работу.
Несколько текущих оптимизаций завязаны на конкретные внутренние типы (например, `ParamMap`,
`MapperCacheKey`, `IsRuntimeParam`) и должны быть сохранены при экстракции.

## Статус реализации

> Все числовые счётчики прогонов ниже — **снимки на момент соответствующей работы**, а не
> текущее состояние дерева: работа идёт параллельно, totals быстро растут. Актуальные цифры
> получать повторным прогоном, а не отсюда.

- **F4 — реализовано** (выделен `RowMaterializerBuilder`, оба `GetMap` на него
  переведены). Дедупликация циклов построчного сканирования `Single`/`SingleOrDefault`
  отложена: семантика paging в in-memory (`ToList` применяет offset/limit в коде) и в
  БД (`OFFSET/FETCH` в SQL) расходится, и трогать исполнение рискованно.
- **Рефакторинг делегата — реализовано.** `InMemoryPreparedQueryCommand<TResult>.CreateEnumerator`
  переведён с анонимного `Func<QueryCommand<TResult>, InMemoryPreparedQueryCommand<TResult>, object[]?, CancellationToken, IAsyncEnumerator<TResult>>`
  на именованный делегат `CreateEnumeratorDelegate<TResult>`; метод-построитель переименован в
  `BuildCreateEnumeratorDelegate<TResult>` (тип и метод не могут называться одинаково). На F3
  (27 `is`-проверок) это не влияет — поведение и производительность те же, меняется только форма типа.
- **Переименование.** `InMemoryCacheEntry<TResult>` → `InMemoryPreparedQueryCommand<TResult>`,
  файл `InMemoryCacheEntry.cs` → `InMemoryPreparedQueryCommand.cs`.
- **F3 — реализовано (закрыто 18.09.2026).** 27 инлайн-guard'ов сведены в единый
  `AsDbCommand<TResult>` / `AsInMemoryCommand<TResult>`, вызываемый на входе публичных
  методов; исключение — `ArgumentException` с пояснением. «Протечка» контракта (default-методы
  интерфейса позволяют передать чужой контекст) закрыта иначе, чем предполагал исходный разбор:
  - добавлены регресс-тесты `tests/nextorm.sqlite.tests/BackendMixingTests.cs`, фиксирующие
    быстрое падение с `ArgumentException` (параметр `preparedQueryCommand`) в обе стороны
    (in-memory-команда → SQL-контекст и наоборот) — критерий A.10;
  - compile-time вариант (CRTP, Вариант C) отклонён: он требует `IDataContext<TContext>` по всей
    fluent-цепочке (`QueryCommand`/`EntityBuilder`/расширения) и 229 мест использования
    `IPreparedQueryCommand<…>`; цена публичного контракта несопоставима с выигрышем (единственный
    сценарий отказа — misuse, а не штатный путь; см. A.4/A.6).
- **Удалён `IsScalar`.** Член `IPreparedQueryCommand<TResult>.IsScalar` убран целиком
  (интерфейс, `PreparedQueryCommand`, `DbPreparedQueryCommand`, `InMemoryPreparedQueryCommand`);
  `DataContext.First/FirstOrDefault/FirstAsync/FirstOrDefaultAsync` определяют scalar-режим по
  `compiledQuery.MapDelegate is null`.
- **F2 — реализовано (ISP).** Разбито на роли в `DataContext/Roles/`:

  | Роль | Члены | Кто реализует |
  |---|---|---|
  | `IConnectionManager` | `EnsureConnectionOpen(Async)` | только `DataContext` |
  | `IQueryExecutor` | 12 терминальных операторов | оба |
  | `IQueryPlanner` | `GetPreparedQueryCommand`, `ResetPreparation`, `GetFrom` | оба |
  | `IRowReaderFactory` | `CreateEnumerator`, `CreateAsyncEnumerator` (+4 default) | оба |
  | `IQueryCache` | `AnyCommand`, `PurgeQueryCache` | оба |
  | `IContextEnvironment` | `Logger`, `CommandLogger`, `NeedMapping`, `Properties` | оба |
  | `IQueryMaterializer` | `IQueryPlanner + IRowReaderFactory` | композитный (без своих членов) |

  Что это дало:
  - `IDataContext` стал **пустым композитом**: `IQueryExecutor, IQueryMaterializer,
    IQueryCache, IContextEnvironment, IAsyncDisposable, IDisposable`. `IConnectionManager`
    в композит **не входит**.
  - **Удалены заглушки-пустышки**: `InMemoryDataContext.EnsureConnectionOpen() { }` и
    `EnsureConnectionOpenAsync() => Task.CompletedTask` больше не нужны — провайдер без
    соединения не обязан его реализовывать. `DataContext : IDataContext, IConnectionManager`.
  - ~20 provider-независимых default-методов (`Any`, `AnyAsync`, `ToList`/`ToListAsync` и
    `First*`/`Single*` оверлоады, `From`, `CreateCommand<T>`, `Create<T>`) вынесены в
    `DataContextExtensions`. Обязательная для провайдера поверхность ужалась с ~43 до 25
    членов; при этом **исходный код потребителей не меняется** (`ctx.Any(...)` и т.п.
    резолвятся в extension-метод).
  - Стриминговые `GetEnumerable`/`GetAsyncEnumerable`/`CreateEnumeratorAsync` оставлены
    default-методами **на роли** `IRowReaderFactory` — так in-memory сохранил свой
    оптимизированный `GetEnumerable` (без `yield`-машины).
  - `CorrelatedQueryExpressionVisitor` сужен до `IQueryMaterializer` + отдельный
    `ILogger?` вместо `IDataContext` (единственный коллаборатор, которому хватает роли).
  - Потребители, которым реально нужен конвейер (`EntityBuilder`, `QueryCommand`, DI,
    `DataContextOptionsBuilder.Factory`), по-прежнему принимают `IDataContext` — это
    осознанно: они используют 4 роли сразу, сужение до одной было бы фиктивным.
  - Бенчмарки и `PlanCacheTests` переведены на явный `((IConnectionManager)ctx).EnsureConnectionOpen()`.
  - Проверка: build 0 warnings/0 errors; тесты core 85, sqlite 22 (1 skipped —
    документированный баг), postgres 16, sqlserver 19; `f4check` — ALL OK.
- **F2: что осталось (осознанно).** F1/F6 (`DataContext` как God class и зависимость
  `SqlBuilder`/визиторов от конкретного `DataContext`) — отдельная работа: роли разделяют
  *контракт*, но не разбивают 1357-строчную реализацию. Пока F1/F6 открыты, `DataContext`
  остаётся и композитом ролей, и диалектом, и конвейером.
- **F8 — реализовано (DI/DRY).** `ServiceCollectionExtensions` сведён к двум приватным хелперам
  (`RegisterContextFactory` / `RegisterContextType<T>`, каждый с keyed-веткой). Двойная регистрация
  `T` + `IDataContext` заменена на forwarding (`IDataContext -> sp.GetRequiredService<T>()`) —
  один инстанс контекста на scope. Options-делегат стал обязательным: fail-fast
  `ArgumentNullException` на регистрации вместо падения на первом резолве. Добавлены
  `DependencyInjectionTests` (5 тестов). Core-тесты: 85 → 90.
- **F7 — частично реализовано.** `IQueryContext` (пустой алиас `: IQueryRegistry`, никем не
  наблюдаемый) удалён: файл + 2 ссылки (`QueryCommand : IQueryRegistry`,
  `DataContext.MakeSelect(..., IQueryRegistry, ...)`). Интерфейсов стало на один меньше.
  Разделение `IQueryRegistry` (граф запроса) / `IPlanComparerFactory` (6 comparer-фабрик)
  отложено по YAGNI: реализация в проде одна, потребителя с узкой зависимостью нет;
  условия возврата перечислены в F7.
- **F5 — частично реализовано (LSP).**
  - `GetColumnName(MemberInfo)` (internal, non-virtual, всегда бросал) удалён; fallback-ветка
    `BaseExpressionVisitor` теперь бросает доменное `BuildSqlCommandException` с именем
    члена — как соседняя ветка для inner column. Гарантированный рантайм-отказ заменён
    внятной ошибкой, поведение рабочего пути не изменилось.
  - `GetTableName(Type)` (`public virtual`, всегда бросал, **не переопределён ни одним
    провайдером**, но достижим из fallback `GetFrom` для незарегистрированного типа) —
    мнимый хук, удалён; на его месте `BuildSqlCommandException` с инструкцией
    («materialize the entity first …»). Покрыто
    `tests/nextorm.sqlite.tests/MetadataRegistrationTests.cs`.
  - `Extend` вынесен из `IProjection` в новый `IExtendableProjection`; `Projection<T1,T2>`
    реализует его, `Projection<T1,T2,T3>` — больше нет (бросающий член исчез). `IProjection`
    остаётся **осмысленным маркером** — он наблюдаем (`IsAssignableTo(typeof(IProjection))` в
    SQL-билдере, маппинге и in-memory). Единственный вызов
    (`InMemoryDataContext.CreateProjection`) проверяет `is IExtendableProjection`; для
    арности выше поддержанной — явное `NotSupportedException` с размерностью.
  - SQLite-бросок в `MakeCount(distinct, big)` убран (переопределение удалено целиком):
    `count` в SQLite и так возвращает 64-битное целое, поэтому `big` удовлетворяется без
    спецсинтаксиса — ровно как уже было в PostgreSQL (`count_big` → `count(*)`) и в базовой
    реализации. Это устранило LSP-нарушение (подтип больше не усиливает предусловие) и
    заодно починило выполнимый запрос. У базового `MakeCount` появился XML-doc с контрактом
    флага `big`; SQL Server по-прежнему переопределяет метод на `count_big`.
  - Тесты: SQLite `CountBig_ShouldThrow` → `CountBig_ShouldUseCountStar`; тест `count_big`
    поднят из `SqlServerSpecificTests` в `CommonTestSuite.Aggregates`
    (`CountBig_ShouldReturn10`), т.к. это теперь кросс-провайдерный контракт. Прогон
    интеграционного набора: Total 309, Failed 0 (SQLite исполнился, Docker-провайдеры
    скипнулись).
  - Часть «хуков» закрыта дальше: `CreateConnection` стал `virtual` с рабочим телом (ось
    соединения, F1), `MakePage`/`MakeParam` уехали в обязательный контракт `ISqlDialect`,
    `GetTableName` удалён. `CreateParam` остаётся `abstract` — это реально варьирующаяся
    фабрика ADO-параметра, реализованная всеми провайдерами.
- **F1 (ось диалекта) + F6 — реализовано.** Введён `ISqlDialect` (+ `SqlDialectBase` с рабочими
  дефолтами) в `src/nextorm.core/DataContext/Dialect/`; провайдеры получили
  `SqliteDialect`/`PostgresDialect`/`SqlServerDialect` (`sealed`, статический `Instance`).
  - `DataContext` объявлен `abstract`; владеет `public abstract ISqlDialect Dialect`, а все
    `Make*`/`Escape`/`Require*`/`MakePage`/`MakeTop` из него убраны. `CreateConnection` и
    `CreateParam` стали `abstract` (оба переопределяют все три провайдера).
  - **F5 закрыт полностью:** в `DataContext` не осталось ни одного бросающего дефолта. Пара
    `RequireSorting`+`EmptySorting` сведена в один `ISqlDialect.GetPagingOrderBy(QueryCommand)`,
    возвращающий `null`, когда сортировка не нужна (SQL Server — константный
    `(select null as anyorder)`), — капабилити выражено возвращаемым значением, а не броском.
  - **F6 закрыт:** `SqlBuilder` принимает `ISqlDialect` (было
    `DataContext`), `BaseExpressionVisitor`/`WhereExpressionVisitor` — `ISqlDialect` + `ILogger?`,
    `DataContext.MakeSelect` строит builder из `Dialect`. Побочно убран мёртвый параметр
    `IDataContext` у `MemberInfoExtensions.GetPropertyColumnName` (метод читает статический
    кэш метаданных и параметр игнорировал). **Остаток (исполнение) закрыт позже:**
    `ResultSetEnumerator`/`DbPreparedQueryCommand` переведены на `IConnectionManager` + делегат
    `CreateParam` — см. F6, «✅ Закрыто».
  - Тесты: `ctx.Escape(...)`/`ctx.RequireSubqueryAlias` переведены на `ctx.Dialect.*`
    (5 мест). Ни один SQL-тест не менялся по существу — значит вывод диалектов идентичен.
  - Проверка: build 0/0; core 90, sqlite 24, postgres 16, sqlserver 19; integration 309
    (Failed 0); `f4check` — OK. SQL-тесты диалектов (`SqlGenerationTests`) — зелёные во всех
    трёх провайдерах, что и есть главное доказательство неизменности рендеринга.
  - **F1 — сдвинулся (шаги 2–3: оси планирования и соединения).** Шаг 2: `QueryPlanner`
    (`internal sealed`, `IQueryPlanner`) забрал `GetPreparedQueryCommand` (183 стр.), `MakeSelect`,
    `ExtractParams`, `IsRuntimeParam`, `GetMapCached`, `ResetPreparation` и оба `GetFrom`; store
    планов вынесен в `QueryPlanStore` (thread-static, общий с `ConnDisposed`/`DisposeStaff`/
    `PurgeQueryCache`); удалён мёртвый `GetAliasFromProjection`. Шаг 3: `DbConnectionManager`
    (`internal sealed`, `IConnectionManager`) забрал состояние и алгоритм соединения, провайдерские
    хуки `CreateDbConnection`/`OnConnectionCreated` остались на контексте и переданы делегатами;
    удалён `public virtual CreateConnection()` (никем не переопределялся и не вызывался извне).
    Провайдерские хуки во всех случаях передаются делегатами и вызываются **лениво** — иначе
    базовый конструктор звал бы override наследника. `DataContext` 1036 → **473** → **397** строк.
  - **F1 — закрыт (шаг 4: ось окружения/кэша).** `ContextEnvironment` (44 строки) + `QueryCache`
    (22) вынесены и **переиспользованы `InMemoryDataContext`**; из `DataContext` удалены ~180 строк
    закомментированного кода и пять неиспользуемых `using`. `DataContext` 397 → **235** строк
    (**−83%** от исходных 1357). Замер: build 0/0; core 154, sqlite 179, postgres 150,
    sqlserver 166 (Failed 0); integration **833, Failed 0, Skipped 23**; бенчмарки
    `ParamsAllocationBenchmark` (full mode, 3 прогона на плечо) — дельты **+1.6…+4.3%**, в
    пределах разброса между прогонами, аллокации без изменений. Подробности — в F1, «шаг 4».
  - **F13 — закрыт (шаг 5: исполнительный фасад in-memory).** `InMemoryQueryExecutor` (336 строк,
    `IQueryExecutor` + `IRowReaderFactory`) забрал все терминалы и `GetEnumerable`; `InMemoryDataContext`
    делегирует их полю `_executor`. Коллаборатор самодостаточен: фабрика энумератора живёт в
    `InMemoryPreparedQueryCommand<TResult>`, а не в контексте. `InMemoryDataContext` 1711 → **1446**
    строк. На контексте осознанно остались reflection-адресуемые методы конвейера (`:368`,
    `:1124`, `:1403`) и инстансные кэши выражений. Замер: build 0/0; core 154, sqlite 179,
    postgres 150, sqlserver 166 (Failed 0); integration 833 (Failed 0, Skipped 23); in-memory
    бенчмарки — аллокации идентичны, время в шуме. Подробности — в F13, «шаг 5».
  - Замер шагов 1–3: build 0/0; core 111, sqlite 149, postgres 115, sqlserver 124; integration
    **561, Failed 0, Skipped 16** (зелёные после каждого шага).
  - **Методика замеров (важная поправка).** `Job.Default` (`NEXTORM_BENCH_FULL=1`) даёт узкий CI
    *внутри* прогона (StdErr 0.4–4%), но **между** прогонами на неизменном коде разброс до **19%**:
    три полных прогона `SqliteBenchmarkCachedPlan` подряд дали `Cached_PlanOnly_Param`
    336.5 / 348.4 / 391.6 µs, `RePrepare_PlanOnly_Param` 142.0 / 140.7 / 167.3 µs,
    `Cached_ToList` 1450.6 / 1358.1 / 1506.2 µs. Вывод: CI прогона нельзя использовать как меру
    воспроизводимости; значимыми считать только изменения заметно выше ~20%, либо сравнивать
    медианы нескольких прогонов. Разовый скачок аллокаций в `Cached_PlanOnly_Param`
    (311.73 → 325.79 KB) во втором прогоне **не воспроизвёлся** (снова 311.73 KB) — это была
    аномалия, не следствие правки.
  - **Перф шагов 2–3 — в пределах этого разброса.** Систематического сдвига нет: в одном прогоне
    подряд идущие строки улучшались на 2–13% (от правки невозможно), в другом — ухудшались на
    8–19%; при этом аллокация `Cached_PlanOnly_Param` вернулась к базовым 311.73 KB.
    Structural cost — несколько вызовов делегатов на построение плана / обращение к соединению
    (не на строку).
  - **Условие чистого замера:** заморозить дерево (коммит) и сравнивать медианы нескольких полных
    прогонов. Пока в одном рабочем дереве смешаны ось диалекта, три новых провайдера, правки
    `QueryCommand`/`SqlBuilder` и шаги 1–3, любой A/B не интерпретируем.
- **F1 (ось соединения) — реализовано; сознательное отклонение от исходной формулировки F1.**
  В F1 значился `IConnectionFactory` (`CreateConnection`/`CreateParam`). Разбор показал, что
  (1) `CreateParam` — не connection-ось (он в одной компании с `MakeParam`/`GetParamName`) и
  (2) три провайдера содержали почти дословную копию `CreateConnection`/`ConnectionString`, а
  единственный потребитель «фабрики» — сам `DataContext`. То есть это DRY-дефект, а не
  отсутствующий шов, и заведение интерфейса дало бы концепцию без точки подстановки. Поэтому:
  - тогда `DataContext` владел `_connectionString`/`_providedConnection`/`ConnectionString` и общим
    скелетом `CreateConnection()` (логирование, short-circuit на внешнее соединение, привязка
    событий, учёт владения) — **позже это целиком ушло в `DbConnectionManager` (шаг 3)**;
  - провайдер реализует `protected abstract DbConnection CreateDbConnection(string?)` и, при
    необходимости, `protected virtual void OnConnectionCreated(DbConnection)`; у SQLite это
    `SQLiteFunctions.Register`, который, как и раньше, вызывается и для внешнего соединения;
    **после шага 3 это единственные две точки расширения соединения** — они остались на контексте,
    а менеджер получает их делегатами;
  - `_connWasCreatedByMe` выставляет база — устранена инверсия «база зависит от побочного
    эффекта в коде наследника» (после шага 3 флаг живёт в `DbConnectionManager`);
  - `CreateConnection` из `abstract` стал `virtual` с рабочим телом, а на шаге 3 удалён вовсе
    (никем не переопределялся и не вызывался извне); `CreateParam` остался `abstract` — это
    реально варьирующаяся фабрика ADO-параметра, реализованная всеми провайдерами;
  - `IConnectionManager` получил `GetConnection()`: роль соединения стала осмысленной
    (`EnsureConnectionOpen` + доступ к соединению), а не только «открой его»;
  - сохранён `protected DataContext(DataContextBuilder)` — наследники, вызывавшие
    `base(builder)`, компилируются без изменений.
  - Эффект: −79 строк дублирования (207 → 128 в трёх провайдерах), `ConnectionString` ×3 → ×1;
    поведение (включая регистрацию функций на внешнем соединении и то, что внешнее соединение
    не диспозится контекстом) сохранено.
  - Покрытие: `tests/nextorm.sqlite.tests/ConnectionManagementTests.cs` (10 тестов) — владение
    соединением (внешнее остаётся открытым, своё диспозится), `ConnectionString` в обоих
    режимах, роль `IConnectionManager`, регистрация функций на внешнем соединении, сквозной
    запрос через внешнее соединение.
  - Проверка: build 0/0; core 95, sqlite 67 (все connection-тесты зелёные), postgres 40,
    sqlserver 44; integration 366 (Failed 0); `f4check` — OK.
- **F1 (ось маппинга) — реализовано; `IColumnMapper` сознательно не введён.** Построение и
  компиляция строкового маппера вынесены из `DataContext` в
  `src/nextorm.core/DataContext/RowMapperFactory.cs`:
  - `MapColumn` — дефолтный аксессор колонки (типизированные геттеры, константные ординалы,
    `IsDBNull`-ветка для nullable);
  - `GetOrBuild` — сборка expression-дерева через `RowMaterializerBuilder`, `Expression.Compile()`
    и кэш `MapperCache` по `MapperCacheKey` (provider type + SQL + сигнатура колонок);
  - `BuildKey` — построение дешёвого ключа.
  Политика маппинга передаётся **делегатом** `Func<SelectExpression, Expression, Expression>`, а
  не интерфейсом: единственный клиент, ради которого именованный `IColumnMapper` имел смысл, —
  `ReplaceMemberVisitor` — оказался мёртвым кодом, а клиенту `GetMap` нужна ровно одна операция,
  которую делегат уже выражает (см. YAGNI/ISP в приоритетах).
  - В `DataContext` остались только `public virtual Expression MapColumnExpression(...)`
    (делегирует в `RowMapperFactory.MapColumn`; переопределяется `SqlServerDataContext`) и
    `GetMapCached` в одну строку. `MapColumn`, `GetMap`, `BuildMapperKey` и `IsDBNullMI` из
    контекста убраны — заодно из публичной поверхности ушли `public static MapColumn` и
    `public GetMap` (наружу не использовались). `IsDBNull`-`MethodInfo` теперь локальная статика
    в `RowMapperFactory` и `SqlServerDataContext`.
  - **Удалён мёртвый `Visitors/ReplaceMemberVisitor.cs`.** В HEAD он использовался в `GetMap`
    (`DataContext.cs:989`), но рефакторинг F4 (`RowMaterializerBuilder`) его осиротил: ни одного
    `new ReplaceMemberVisitor` ни в `src`, ни в `test`, ни в `benchmarks` (проверено grep'ом по
    всему репозиторию). Та же история, что `GetColumnName`/`GetTableName` в F5.
  - `ConvertScalar` оставлен в `DataContext`: это конверсия скалярного результата для
    `ExecuteScalar`/`First`, а не маппинг строк (другая ось).
  - `DataContext`: 1156 → 1104 строки.
  - Проверка: build 0/0; core 95, postgres 40, sqlserver 44, integration 366 (Failed 0),
    `f4check` — OK. В sqlite 78 тестов, единственное падение —
    `Contains_CapturedArray_ShouldRenderInPredicate` в `BaseExpressionVisitor.GetInValues`:
    это параллельная (незавершённая на момент прогона) правка нового `IN`-транслятора, в HEAD
    этих методов нет, к маппингу отношения не имеет.
- **Нумерация строк** приведена к текущему рабочему дереву (после F4). Правка F4 сдвинула
  номера в `DataContext.cs` после ~1019 и в `InMemoryDataContext.cs` после ~636, поэтому
  ссылки в F1/F5 на диапазоны `DataContext` правее ~1019 могут отличаться на величину
  вырезанного блока.
- **F10 — реализовано (DRY).** Все 16 тел агрегатов `EntityBuilder<TEntity>` сведены к двум приватным
  ядрам: `AggregateCore<TResult>` (sync) и `AggregateAsyncCore<TResult>` (async). Оба принимают
  `MethodInfo` (`CommonFunctions.MinMI`/`MaxMI`/`AvgMI`/`SumMI`/`StdevMI`/`StdevpMI`/`VarMI`/`VarpMI`) —
  это единственное, чем отличались 8 семейств. Публичные сигнатуры не тронуты ни в одной из 40
  перегрузок, `[MethodImpl(AggressiveInlining)]` на async-обёртках сохранены. `EntityBuilder.cs`
  674 → 602 строки, приватных ядер 8 → 2.
  - Заодно закрыт остаточный пробел SQLite: в `SQLiteFunctions` зарегистрированы `var`/`varp`,
    аккумулятор переименован в `VarianceAccumulator`, его `Final` разделён на `FinalVariance`
    (дисперсия) — `stdev`/`stdevp` берут от неё `Math.Sqrt`, `var`/`varp` возвращают напрямую.
    `SupportsVarianceAggregates` для SQLite → `true`.
  - Проверка: build 0/0; core 107, sqlite 136, postgres 90, sqlserver 114 (везде Failed 0);
    integration 561 (скипов 16, падений по агрегатам 0), в `SqliteIntegrationTests` скипы
    10 → 2; `f4check` ALL OK.
- **F11 — реализовано (DRY).** Единый источник кэшей — `DataContextCache`; у `InMemoryDataContext`
  удалены мёртвые дубли-статики `_metadata`/`_selectListCache` (свойства делегируют, публичный
  API сохранён), `_expCache` оставлен инстансным осознанно, scope кэшей задокументирован, добавлен
  `DataContextCacheScopeTests` (4 теста). Доказательства — в самом разделе F11.
  - Проверка: build 0/0; core **111** (+4), sqlite 143, postgres 90, sqlserver 114 — везде
    Failed 0; `f4check` ALL OK.
- **`[MethodImpl(AggressiveInlining)]` — выравнивание конвенции + измерение (перф-работа, не
  SOLID).** Устранено расхождение, помеченное в F10: атрибут стоял только на async-обёртках
  `EntityBuilder<TEntity>`, но не на sync. Добавлено 27 атрибутов (99 → 126): 16 sync-обёрток агрегатов и
  8 форвардеров в `EntityBuilder.cs`, `MapperCache.TryGet`, `DataContext.IsRuntimeParam`.
  - **Измерено** (`MicroOptimizationsBenchmark.M12`, fast/ShortRun): на чистых форвардерах атрибут
    **не даёт ничего** (678.7 ns против 698.6 ns — внутри шума, StdDev 40–58 ns): JIT инлайнит их и
    так. На leaf-методе с циклом (`for` ×4 + ветвление) — **−20.5%** (2447.0 → 1944.4 ns), т.е.
    атрибут работает именно там, где JIT отказывается инлайнить сам.
  - **Вывод:** достижимого выигрыша от атрибута в этом коде нет — построчная работа сидит внутри
    `Expression.Compile()`-делегата (атрибуты до него не достают), а циклы по колонкам уже инлайн.
    Добавленные 25 атрибутов в `EntityBuilder.cs` — **консистентность конвенции, не ускорение**.
  - Побочно: существующий `M10` (sealed vs unsealed) не измеряет девиртуализацию — оба плеча берут
    экземпляр из `static readonly` конкретного поля, и JIT девиртуализирует оба.
- **F6 (остаток — исполнение) — реализовано (DIP/DRY).** `DbPreparedQueryCommand.GetDbCommand`
  принимает `Func<string, object?, DbParameter>` вместо `DataContext` (вся зависимость была в одном
  вызове `CreateParam`); `ResultSetEnumerator` переведён на `IConnectionManager` + делегат,
  `_dbContext`/публичное свойство `DataContext`/reach-in в internal-поле `_connOpen` убраны,
  `InitEnumerator` стал `internal`. Блок открытия соединения жил **4 раза** — осталось одно
  выражение `EnsureConnectionOpen(Async)`, включая оба хелпера `DataContext`. Токен отмены сохранён:
  `IConnectionManager.EnsureConnectionOpenAsync(CancellationToken = default)`.
  - Проверка: build 0/0 (Debug и Release); core 111, sqlite 144, postgres 90, sqlserver 116;
    integration **561, Failed 0, Skipped 16**; бенчмарк `ParamsAllocationBenchmark` —
    аллокации до/после **идентичны**, тайминги в пределах шума (перф-нейтрально).

---

## Приложение A. Подробный разбор F3

### A.1. Суть

Контекст владеет своим стораджем: пару «команда ↔ контекст» смешивать нельзя. Проверка
`is` — это **guard инварианта**, а не механизм диспетчеризации; замысел легитимен. Дефект
был в форме: guard дублировался 27 раз, бросал `NotSupportedException`, а сигнатура
абстракции обещала принимать любой импл. OCP здесь не нарушается (A.4). DIP требует, чтобы
`IDataContext`/`IPreparedQueryCommand` ссылались только на абстракции, поэтому «зашить»
производные типы в сигнатуры нельзя — это и определяет выбор фикса (A.7).

### A.2. Как это выглядит в коде

Публичная цепочка:

```
QueryCommand<TResult>.ToList(params)
   └─ _dataContext.GetPreparedQueryCommand(this, ...)   → IPreparedQueryCommand<TResult>
   └─ _dataContext.ToList(preparedCommand, params)
```

После рефакторинга вход каждого публичного метода начинается с одного guard'а
(`DataContext.cs`; в `InMemoryDataContext.cs` — симметричный `AsInMemoryCommand`):

```csharp
public List<TResult> ToList<TResult>(
    IPreparedQueryCommand<TResult> preparedQueryCommand, ReadOnlySpan<object?> @params)
{
    var compiledQuery = AsDbCommand(preparedQueryCommand);   // ← один guard на входе
    var sqlCommand = GetDbCommand(compiledQuery, @params);
    ...
}

private static DbPreparedQueryCommand<TResult> AsDbCommand<TResult>(IPreparedQueryCommand<TResult> preparedQueryCommand)
    => preparedQueryCommand as DbPreparedQueryCommand<TResult>
       ?? throw new ArgumentException($"Expected {nameof(DbPreparedQueryCommand<TResult>)}, got {preparedQueryCommand.GetType().Name}", nameof(preparedQueryCommand));
```

До рефакторинга этот же guard был скопирован 27 раз (14 в `DataContext`, 13 в
`InMemoryDataContext`) и бросал `NotSupportedException`; сейчас он один на контекст.

### A.3. Иерархия типов

```
IPreparedQueryCommand<TResult>              // без собственных членов + ~20 default-методов
├─ PreparedQueryCommand<TResult, TRecord>   // хранит MapDelegate
│   └─ DbPreparedQueryCommand<TResult>      // sealed: DbCommand, ParamMap, Enumerator, Behavior...
└─ InMemoryPreparedQueryCommand<TResult>    // sealed, НЕ наследует PreparedQueryCommand
```

Два ключевых факта:

1. `InMemoryPreparedQueryCommand<TResult>` напрямую реализует интерфейс, минуя базовый класс
   (у которого есть `MapDelegate`); собственных членов контракта он не несёт.
2. Сам интерфейс не содержит операций исполнения — его default-методы просто
   переадресуют обратно в контекст:

```csharp
public List<TResult> ToList(IDataContext dataContext, params ReadOnlySpan<object?> @params)
    => dataContext.ToList(this, @params);   // DataContext/Cache/IPreparedQueryCommand.cs:33
```

То есть абстракция не описывает **варьирующееся поведение** — она описывает только
«ручку», за которую контекст дёргает. Это осознанно: интерфейс — opaque handle, а
исполнение остаётся в контексте. Замечание не в том, что так нельзя, а в том, как именно
проверяется инвариант (A.5–A.7).

### A.4. Почему это не OCP

Реляционные провайдеры (Sqlite/Postgres/SqlServer) наследуют `DataContext` и переиспользуют
один `DbPreparedQueryCommand<TResult>`; `GetPreparedQueryCommand` не virtual. Значит,
добавление нового реляционного провайдера не требует правок в ядре — «closed for
modification» соблюдён. Цена возникла бы только при желании задать иную модель исполнения
(как in-memory), но это по определению отдельный класс контекста, а не модификация
существующего.

Поэтому `is` — не dispatch-проблема, а guard инварианта. Остаётся вопрос контракта:
абстракция принимает любой импл, а поддерживает один (A.5).

Внутренний downcast в кэше — деталь реализации `DataContext`, DIP не нарушает:

```csharp
// DataContext.cs:17-18,23,414
[ThreadStatic] private static Dictionary<QueryPlanCacheKey, IDbCommandHolder>? _queryPlanCache;
private readonly record struct QueryPlanCacheKey(Type ContextType, QueryPlan plan);
...
var compiledQuery = (DbPreparedQueryCommand<TResult>)planCache;
```

Конкретный тип встречается только внутри реализации и никогда — в сигнатурах абстракции
(`IDataContext`, `IPreparedQueryCommand`).

### A.5. Это ещё и LSP

Нарушение подстановки: `IDataContext.ToList(IPreparedQueryCommand<TResult>, …)` принимает
любой импл интерфейса, но реально работает только с одним. Передайте
`InMemoryPreparedQueryCommand` в `DataContext` — получите `ArgumentException` в рантайме
(после рефакторинга; раньше — `NotSupportedException`), компилятор промолчит.

Это учебниковый LSP: type-корректная программа падает в рантайме.

Раньше сюда же примыкал `IsScalar` — член контракта, который `InMemoryPreparedQueryCommand`
был вынужден реализовывать через `throw NotImplementedException`. **Устранён**: член убран
из интерфейса, реляционный контекст определяет scalar-режим по `MapDelegate is null`.

### A.6. Почему guard остаётся рантайм-проверкой

Почему не ошибка компиляции? Упирается в DIP: `IDataContext` — абстракция, и её публичные
методы не могут принимать производные `DbPreparedQueryCommand` /
`InMemoryPreparedQueryCommand` — иначе абстракция зависела бы от реализаций. Пока
`IPreparedQueryCommand<TResult>` — единый абстрактный handle, единственная точка отказа —
рантайм.

Рантайм-отказ здесь приемлем: он затрагивает только misuse (смешивание бэкендов), в штатном
пути команда всегда берётся у своего контекста, и guard даже не срабатывает. После
рефакторинга цена — одно понятное `ArgumentException` на входе, а не 27 копий.

### A.7. Варианты и почему выбран текущий

**Вариант A (реализован) — единый guard на входе.** Публичные методы остаются на
абстракции `IPreparedQueryCommand<TResult>`; внутри реализации — один `AsDbCommand` /
`AsInMemoryCommand`, который проверяет тип и бросает `ArgumentException`:

```csharp
private static DbPreparedQueryCommand<TResult> AsDbCommand<TResult>(IPreparedQueryCommand<TResult> cmd)
    => cmd as DbPreparedQueryCommand<TResult>
       ?? throw new ArgumentException($"Expected {nameof(DbPreparedQueryCommand<TResult>)}, got {cmd.GetType().Name}", nameof(cmd));
```

- Соответствует DIP: абстракция ссылается только на абстракции.
- Соответствует исходному замыслу: чужой бэкенд отвергается.
- Минус: отказ в рантайме (но в одном месте и с внятным сообщением).

**Вариант B (отвергнут) — сузить параметры до производных типов.** Даёт ошибку компиляции,
но нарушает DIP: `IDataContext` начинает ссылаться на конкретные реализации. Неприемлемо.

**Вариант C (опция) — generic по контексту.** `IPreparedQueryCommand<TContext, TResult>
where TContext : IDataContext`. Абстракция по-прежнему не ссылается на реализации, но пара
«команда ↔ контекст» становится частью типа → смешивание не компилируется. Цена: усложнение
публичного контракта и всех сигнатур.

**Вариант D — исполнение на команде (Tell, Don't Ask).** `IPreparedQueryCommand<TResult>`
получает методы исполнения и принимает абстрактный контекст; контекст не диспетчеризует по
типу вовсе. Соответствует DIP, но это крупный отдельный рефакторинг (задевает F2/F6, A.8).

### A.8. Связь с F2/F6

Реализованный вариант A намеренно не трогает F2/F6: guard живёт внутри реализации и
опирается только на приватный хелпер. Вариант D (исполнение на команде), наоборот,
потребует ролевых абстракций (F2) и диалект-абстракции (F6), потому что команде
понадобятся `CreateParam`, `GetParamName`, `ExecuteReader`. То есть порядок «F2 → F3» был
нужен только для варианта D; текущий фикс от них не зависит.

> Обновление: F2 и F6 с тех пор реализованы (см. «Статус реализации»), поэтому для варианта D
> абстракции уже есть — остаётся только разгрузить исполнение в `DataContext`.

### A.9. Что нельзя сломать при исправлении

Код на горячем пути и уже оптимизирован:

- `[ThreadStatic] QueryPlanCache` и `MapperCache` шарят один экземпляр команды между
  **инстансами** контекста (ключ включает только `ContextType`). Значит, команда **не
  должна хранить** ссылку на контекст/соединение — передавать их параметром (сейчас так и
  сделано в `GetDbCommand`).
- `DbPreparedQueryCommand.ParamMap`, `NeedsParamRefresh`, `NoParams`, `IsRuntimeParam` —
  точечные оптимизации, завязанные на конкретный тип; сохранить.
- `[MethodImpl(AggressiveInlining)]` на `AnyAsync`/`CreateCommand` — сохранить.
- Не «раздувать» generic-путь до `object`/`dynamic`.

### A.10. Как поймать регресс

Смешивание бэкендов должно падать быстро и внятно:

```csharp
var prepared = inMemoryCtx.GetPreparedQueryCommand(someQuery, false, true, default);
dbCtx.ToList((IPreparedQueryCommand<int>)prepared, ReadOnlySpan<object?>.Empty);
// → ArgumentException: Expected DbPreparedQueryCommand, got InMemoryPreparedQueryCommand`1
```

Проверено вручную на связке «in-memory команда → `SqliteDataContext`».

---

## Приложение B. Точечное ревью 25.09.2026 — трансляция `dict[column]` (design / type / perf; uncommitted working tree)

**Область.** `src/nextorm.core/Query/DictionaryLookup.cs`, `src/nextorm.core/Visitors/DictionaryLookupTranslator.cs`,
`Query/InValues.cs` (`ShapeVisitor`/`CheckLookup`/`ComputeShapeHash`), `Query/QueryCommand.cs`
(`LookupPartitions`/`ShapeScanned`), `Query/QueryCommand.Clone.cs`, `Query/QueryCommand.QueryPreparer.cs`,
`Visitors/BaseExpressionVisitor.cs`. Публичной поверхности фича не добавляет (все новые типы/члены —
`internal`; `EmitFoldedParameter` — `private`→`internal`); запись по API — в `API-NAMING-REVIEW.md`.

**Метод.** Сканы `analyzing-dotnet-performance` по трём фокус-файлам и местам правки: `.Substring` 0,
`.IndexOf("…")` 0, `.StartsWith/.EndsWith("…")` 0, `.Contains("…")` 0, `.ToLower/.ToUpper` 0, `.Replace(` 0,
`params` 0, LINQ (`Select/Where/OrderBy/GroupBy`) 0, `string.Format` 0; `new List<` 2 (`DictionaryLookup.cs:109,117`),
`new Dictionary<` 2 (`InValues.cs:225,261`); `foreach (DictionaryEntry …)` 1 (`DictionaryLookup.cs:110`).
Обратная проверка типов ниже (F-DL2).

### 🟡 F-DL1 (DRY, исправлено) — порядок key/value-параметров дублировался в `BuildCase` и `AddParameters`

**Место (до).** `Visitors/DictionaryLookupTranslator.cs:90-99` и `:113-117` независимо вызывали
`GetParamName()` в одном и том же порядке; рассинхрон этих двух циклов — структурная первопричина
P1-дефекта (Находка 191 в `code-smells-review.md`).
**Стало.** Единый источник порядка — `EmitBranches` (`Visitors/DictionaryLookupTranslator.cs:101-116`);
`BuildCase` (`:82-98`) передаёт построитель, param-проход (`:45`) — `null`. Имена, порядок и текст SQL
не изменились (регресс-тесты sqlite `Dictionary_IndexedByExpressionKey_ShouldKeepParametersAligned`,
`Dictionary_IndexedByColumn_ShouldBindKeyValueParameters`).

### ✅ F-DL2 (TYPE, нарушений нет) — типы спроектированы верно

`LookupEntry` — `internal readonly struct` с readonly-полями (`Query/DictionaryLookup.cs:10-25`);
`DictionaryLookup` / `DictionaryLookupTranslator` — `internal static`; `InValues.ShapeVisitor` — `private sealed`.
Новых интерфейсов и пар `IFoo`/`Foo` нет (invariant 1: второго потребителя нет, seam не наблюдается).
Возврат `List<LookupEntry>` — из `internal`-методов, публичный контракт не затронут. `ValueTask`/`Span`/`Memory`
в фиче не используются (синхронный build-путь). Новых `ValueTask`-злоупотреблений и `Span` в async нет.

### ℹ️ F-DL3 (PERF, отложено) — переоценка коллекции и боксинг на каждом исполнении

`RefreshInValuesShape` на каждое исполнение уже подготовленной кэшируемой команды строит новый
`Dictionary<Expression, List<LookupEntry>>` (`Query/InValues.cs:261`) и `List<LookupEntry>`
(`Query/DictionaryLookup.cs:109,117`); `ToEntries` перечисляет необобщённый `IDictionary`/`IList`
(боксинг `DictionaryEntry` на элемент, `:110`). Осознанно (shape и значения коллекции могут меняться
между исполнениями) и структурно идентично IN-пути (`InValues.Partition`). Двойной переоценки нет:
SQL-проход и param-проход кэш-хита переиспользуют `LookupPartitions`
(`Visitors/DictionaryLookupTranslator.cs:70-71`); `Has<ParameterExpression>` в `TryGetLookup` отрабатывает
только на `get_Item`/`ArrayIndex`-узлах. Замер — `nextorm-db-perf-analyst`/`nextorm-inmemory-perf-analyst`;
без замера micro-оптимизация не вносится (invariant 3).

### ℹ️ F-DL4 (PERF/Design, подтверждено — не чистить) — `LookupPartitions` в cache-клоне load-bearing

`Query/QueryCommand.Clone.cs:32-33` копирует `LookupPartitions`/`ShapeScanned` в clone, тогда как
`InValuesPartitions` не копируется. Это **не** мёртвое состояние кэш-ключа: `CloneForCache()` — ещё и вход
рендера source-мутаций — `QueryPlanner.RenderSource` (`DataContext/QueryPlanner.cs:111`,
`ctx = ctx with { QueryProvider = body }`, затем `MakeSelect(body)`) и `SqlSourceRenderer.RenderMutation`
(`DataContext/SqlSourceRenderer.cs:122-134`). В `DictionaryLookupTranslator.ResolveEntries`
(`Visitors/DictionaryLookupTranslator.cs:68-78`) отсутствие партиции при `Cache && ShapeScanned` бросает
`NotSupportedException`, поэтому снятие копии сломало бы легитимный `dict[column]` в WHERE такого source.
IN-путь деградирует мягко (`Visitors/InValuesTranslator.cs:52-55` отключает кэш и считает на месте),
поэтому асимметрия обоснована. Кандидат на будущее — разделить clone-для-ключа и clone-для-рендера;
триггер: третий потребитель `CloneForCache()`. Снимает ℹ️-наблюдение B из `code-smells-review.md`.

### ℹ️ F-DL5 (PERF, benign) — `_whereBasePlanHash` в ветке `!ShapeScanned` равен 0

`Query/QueryCommand.QueryPreparer.cs:147` делает `WherePlanHash = _whereBasePlanHash * 13 + InValuesShapeHash`,
но при первой подготовке с `dontCalculateHash=true` база не вычисляется (`:653`, под `!noHash`) и остаётся 0 —
множитель вырожден. Наблюдаемого дефекта нет: при no-hash подготовке обнулены и остальные part-хэши
(`:76-81`), а равенство планов обеспечивает `QueryPlanEqualityComparer.Equals` по `PreparedCondition`.
Не чинилось: наблюдаемого эффекта и тест-поверхности нет (`WherePlanHash` — `internal`, `InternalsVisibleTo` нет).

### ✅ F-DL6 — регрессии 191/192 и EOL 194 закрыты

`tests/nextorm.sqlite.tests/DictionaryLookupSqlGenerationTests.cs`:
`Dictionary_IndexedByExpressionKey_ShouldKeepParametersAligned` (191) и
`Dictionary_PreparedWithoutHashThenCached_ShouldTranslate` (192). EOL-находка 194 закрыта: 9/9 новых `.cs`
(2 `src` + 7 tests) переведены в CRLF.

**Проверка (25.09.2026).** `dotnet build nextorm.slnx -c Release` — **0 warnings / 0 errors**.
`dotnet test tests/nextorm.sqlite.tests -c Debug --filter "FullyQualifiedName~DictionaryLookup"` — **9/9**;
полный `nextorm.sqlite.tests` — **463/463**; `nextorm.core.tests` — **327/327**; `~DictionaryLookup` по
postgres/sqlserver/mysql/mariadb/clickhouse — по **2/2**, core — **1/1**. Контейнерная интеграция не перезапускалась.
