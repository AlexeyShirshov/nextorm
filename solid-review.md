# Обзор SOLID / DRY — nextorm

Read-only ревью дизайна core-библиотеки `nextorm` на соответствие принципам SOLID и
DRY. Код не изменялся. Ссылки на находки — в формате `file:line`.

> **Примечание об области.** Это дизайн-ревью, а не анализ производительности.
> Механика DI-контейнера (регистрация/времена жизни) формально относится к
> `dotnet-csharp-dependency-injection`; здесь она приведена только на уровне дизайна.
> Идентификаторы находок (F1..F11) сохранены стабильными для перекрёстных ссылок;
> порядок изложения ниже начинается с **F4** по запросу.
>
> **Примечание о ссылках `file:line`.** Блоки «Доказательство» описывают **состояние до
> рефакторинга**, поэтому их номера строк указывают на прошлые ревизии и в текущем дереве
> сдвинуты. Ссылки, адресующие текущее дерево («Сверка сводной таблицы», «Исправление»,
> «Статус реализации»), повторно сверены с рабочей копией после `9641660` и приведены к
> актуальным номерам строк; переходы размеров вида «X → Y» в «Статусе реализации» —
> исторические снимки выполненных работ, а не текущее состояние.

---

## Сводка

| # | Симптом | Принцип | Критичность | Статус |
|---|---|---|---|---|
| [F4](#f4-дублирование-логики-материализации-строк-dry-средне-высокая) | Логика материализации строк продублирована в обоих контекстах | DRY | Средне-высокая | ✅ Реализовано |
| [F1](#f1-dbcontext--god-class-srp-высокая) | `DbContext` — God class: соединение + диалект + маппинг + кэш | SRP | **Высокая** | Частично |
| [F2](#f2-idatacontext--fat-interface-isp-высокая) | `IDataContext` — ~60 членов на все роли | ISP | **Высокая** | ✅ Реализовано |
| [F3](#f3-протекающий-контракт-и-дублирование-guard-проверок-lspdry-средняя) | 27 guard-проверок `is …` + `NotSupportedException`; контракт протекает | LSP/DRY | Средняя | Частично |
| [F5](#f5-notimplementedexceptionnotsupportedexception-как-базовое-поведение-lsp-средняя) | `NotImplementedException`/`NotSupportedException` как контракт базы | LSP | Средняя | ✅ Реализовано |
| [F6](#f6-зависимость-от-конкретного-dbcontext-dip-средняя) | SQL-билдер и визиторы зависят от конкретного `DbContext` | DIP | Средняя | ✅ Реализовано |
| [F7](#f7-пустой-алиас--перегруженный-iqueryprovider-ispyagni-низкая) | `IQueryContext` — пустой алиас; `IQueryProvider` перегружен хешированием плана | ISP/YAGNI | Низкая | Маркер удалён; ISP отложен |
| [F8](#f8-di-регистрации-дублирование-и-двойной-инстанс-drydip-средняя) | 6 дублирующихся DI-оверлоадов + двойная регистрация | DRY/DIP | Средняя | ✅ Реализовано |
| [F9](#f9-baseexpressionvisitor--god-class--switch-на-именах-srpocp-средняя) | `BaseExpressionVisitor` — 1035 строк, switch на 20 имён | SRP/OCP | Средняя | Открыто |
| [F10](#f10-дублирование-агрегатов-в-entitytentity-dry-низкая) | `Entity<TEntity>`: 40 членов, 8 семейств агрегатов отличаются только `MethodInfo` | DRY | Низкая | ✅ Реализовано |
| [F11](#f11-параллельные-реализации-кэшей-dry-низко-средняя) | Параллельные реализации кэшей (static vs instance) | DRY | Низко-средняя | ✅ Реализовано |

---

## Сверка сводной таблицы (прогон по коду)

Повторный прогон по рабочей копии после `9641660` (все ссылки перепроверены по содержимому
строк). Прежняя сверка была от 13:48:18, после закрытия F6.

| # | Что проверено в коде | Вердикт |
|---|---|---|
| F4 | `RowMaterializerBuilder.Build` вызывается из `InMemoryDataContext` (`:775`) и `RowMapperFactory` (`:88`) | ✅ подтверждено |
| F1 | `DbContext.cs` 1357 → 1104 на момент работы; текущее дерево — **1095** строк; вынесены диалект (`ISqlDialect`), маппинг (`RowMapperFactory`), соединение (база + `IConnectionManager`) | Частично — осталась разгрузка исполнения |
| F2 | `IDataContext.cs` 122 → **25** строк: пустой композит 4 ролей; `EnsureConnectionOpen` остался только у `DbContext` (`:76,88`) и в `IConnectionManager` | ✅ подтверждено |
| F3 | guard'ов `is I…` в `DbContext` — **0**, `NotSupportedException` — **0**; на месте единые `AsDbCommand` (`DbContext.cs:657`) и `AsInMemoryCommand` (`InMemoryDataContext.cs:832`) | Частично — осталась контрактная часть (публичный `ToList(IPreparedQueryCommand<TResult>)`) |
| F5 | `NotImplementedException` в `DbContext` — **1**, и тот внутри закомментированного блока (`:1076`) | ✅ подтверждено |
| F6 | `SqlBuilder`, `WhereExpressionVisitor`, `BaseExpressionVisitor` — **0** обращений к `DbContext`; единый `StringBuilderPool.Shared`; `NormParam` вместо `DbContext.GetParamName`. Остаток (**исполнение**) закрыт: `ResultSetEnumerator` и `DbPreparedQueryCommand` больше не зависят от конкретного `DbContext` (роль `IConnectionManager` + делегат `CreateParam`), `InitEnumerator` стал `internal`, блок открытия соединения (жил 4 раза) сведён к `EnsureConnectionOpen(Async)`; в этих двух файлах `DbContext` — только в комментариях | ✅ подтверждено |
| F7 | `Query/IQueryContext.cs` удалён; упоминаний в `src`/`test` нет | ✅ подтверждено |
| F8 | приватные `RegisterContextFactory` / `RegisterContextType` (`DI/ServiceCollectionExtensions.cs:73,103`) | ✅ подтверждено |
| F9 | `BaseExpressionVisitor` **2128** строк (на предыдущую сверку — 2223); 6 `NotImplementedException` (не изменилось); switch по `nameof(TableAlias.*)` на месте (`:123+`); `TryTranslateFunction` (`:541`) и `ISqlDialect.Make*` на месте | Открыто — размер немного сократился, часть OCP-долга ушла в диалект |
| F10 | `Entity.cs` 674 → 602 на момент работы; текущее дерево — **627** (рабочая копия добавила ~25 строк); 8 приватных `XCore` + 8 async-тел → **2** (`AggregateCore`/`AggregateAsyncCore`), 32 публичных члена не изменены; `var`/`varp` зарегистрированы в SQLite | ✅ подтверждено |
| F11 | `DataContextCache` — единственный источник (`Metadata`/`SelectListCache`/`ExpressionsCache`/`InValuesCache`); дубли-статики `_metadata`/`_selectListCache` у `InMemoryContext` **удалены**, свойства делегируют (`InMemoryDataContext.cs:59,62`); `_expCache` оставлен инстансным **осознанно** (захват `this`); scope всех кэшей задокументирован; дубли `_sbPool` и роста имён **устранены** (F6) | ✅ подтверждено |

**Прогон (повторная сверка).** build 0/0; core **111**, sqlite **144** — Failed 0 (xUnit v3
раннер напрямую; `dotnet test` в среде сверки тесты не обнаруживает). postgres/sqlserver/
integration в этом прогоне не исполнялись. Прежний снимок (после закрытия F6): build 0/0;
core 107, sqlite 136, postgres 90, sqlserver 114 — везде Failed 0; integration 561
(Failed 0, Skipped 384); `f4check` — ALL OK.

---

## F4. Дублирование логики материализации строк (DRY, Средне-высокая)

**Доказательство.** `src/nextorm.core/DataContext/DbContext.cs`, `GetMap<TResult>`
(`:995-1080`), и `src/nextorm.core/DataContext/InMemoryDataContext.cs`,
`GetMap<TResult,TEntity>` (`:613-693`), содержат почти идентичный алгоритм выбора
конструктора:

```
var ctorInfo = resultType.GetConstructors()
    .OrderByDescending(it => it.GetParameters().Length).FirstOrDefault()
    ?? throw new PrepareException($"Cannot get ctor from {resultType}");

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

**Влияние.** Расхождение уже есть: `InMemoryContext` обрабатывает `IgnoreColumns`
отдельной ветвью (`:640`), `DbContext` — нет. Любое изменение правил маппинга (новая
форма конструктора, init-only члены, records) нужно вносить дважды, и они могут
незаметно разойтись.

**Исправление.** Выделить `RowMaterializerBuilder`, принимающий выражение-параметр
записи и поставляемый провайдером маппер колонок
(`Func<SelectExpression, Expression, Expression>`). Композировать его в обоих
контекстах вместо копирования. Композиция вместо наследования (см. guidance скилла о
переиспользовании поведения).

---

## F1. `DbContext` — God class (SRP, Высокая)

**Доказательство.** `src/nextorm.core/DataContext/DbContext.cs` — 1357 строк, 81
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
  выделены из `DbContext`; все `Make*`/`Escape`/`Require*`/`MakePage`/`MakeTop` переехали
  туда, три провайдера получили `SqliteDialect`/`PostgresDialect`/`SqlServerDialect`.
  `DbContext` объявлен `abstract` и владеет только `public abstract ISqlDialect Dialect`.
  `SqlBuilder` и визиторы теперь зависят от `ISqlDialect` и `ILogger?`, а не от контекста
  (закрывает F6).
- **Сделано — ось соединения.** Connection-состояние (`_connectionString`,
  `_providedConnection`, `ConnectionString`, логирование) переехало в `DbContext`; провайдер
  задаёт только `CreateDbConnection`/`OnConnectionCreated`. `IConnectionFactory` как отдельная
  абстракция **сознательно не вводится**: единственный потребитель — сам `DbContext`, а
  реализации трёх провайдеров были на 90% идентичны, т.е. это DRY-дефект, а не отсутствующий
  шов. Роль соединения при этом усилена: `IConnectionManager` получил `GetConnection()`.
- **Сделано — ось маппинга.** Построение и компиляция строкового маппера вынесены в
  `RowMapperFactory`; в `DbContext` остались тонкий хук `MapColumnExpression` (его
  переопределяет SQL Server) и одна строка делегирования. `IColumnMapper` **сознательно не
  вводится**: политика маппинга передаётся делегатом `Func<SelectExpression, Expression,
  Expression>`, а клиент, ради которого именованный интерфейс имел смысл
  (`ReplaceMemberVisitor`), оказался мёртвым кодом и удалён. `ConvertScalar` оставлен — это
  скалярная конверсия для `ExecuteScalar`, а не маппинг строк.
- **Осталось.** Только разгрузка исполнения — роли для неё уже есть из F2
  (`IQueryExecutor`/`IQueryMaterializer`).
- **Риск.** SQL-рендеринг на горячем пути: перед дальнейшим дроблением зафиксировать
  benchmark-базу (`performance-findings.md`).

---

## F2. `IDataContext` — fat interface (ISP, Высокая)

**Доказательство.** `DataContext/IDataContext.cs` — 122 строки, ~60 членов. Смешаны:
управление соединением (`EnsureConnectionOpen`, `:57-58`), исполнение
(`ToList/First/Single` × sync/async, `:92-115`), создание энумераторов (`:82-91`),
фабрика команд (`CreateCommand`, `:25-34`), метаданные (`Create<T>`, `:15-24`), кэш
(`PurgeQueryCache`, `:120`), подготовка плана (`GetPreparedQueryCommand`, `:81`), плюс
~15 default-extension методов (`Any`, `GetAsyncEnumerable`, `From`).

`InMemoryContext` вынужден реализовывать весь контракт, включая no-op
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
`InMemoryContext` нельзя подсунуть команду реляционного провайдера и наоборот. Проверка
`is` здесь — это **guard инварианта**, а не механизм диспетчеризации.

**Доказательство (состояние до рефакторинга).** `DbContext.cs` — 14 вхождений
`if (preparedQueryCommand is DbPreparedQueryCommand<TResult> compiledQuery) ...
else throw new NotSupportedException`, и 13 симметричных в `InMemoryDataContext.cs`
(`is InMemoryPreparedQueryCommand<TResult>`), плюс downcast
`(DbPreparedQueryCommand<TResult>)planCache` (`DbContext.cs:384`). Текущее состояние — A.2.

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
- `SqliteDbContext.MakeCount(distinct, big: true)` бросал `NotSupportedException`,
  тогда как база и PostgreSQL флаг `big` игнорируют, а SQL Server его обрабатывает —
  подстановка подтипа ломала программу; **переопределение удалено**;
- `Projection<T1,T2>.Extend` работал, а `Projection<T1,T2,T3>.Extend` бросал при одном
  контракте `IProjection`; **`Extend` вынесен в `IExtendableProjection`**;
- `EntityP2.Join` бросает при `Condition is not null`
  (`Builders/Joins/JoinCommandBuilder.cs:25-26`), хотя базовый `Entity<TEntity>.Join` —
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
вместо броска. `DbContext` объявлен `abstract`, `CreateConnection`/`CreateParam` и
`Dialect` — `abstract`. **Бросающих дефолтов в базе больше нет** — в `DbContext` не
осталось ни одного `throw new NotImplementedException`.

---

## F6. Зависимость от конкретного `DbContext` (DIP, Средняя)

**Доказательство.** `SqlBuilder` принимает `DbContext dbContext`
(`DataContext/SqlBuilder.cs:13,20`), `BaseExpressionVisitor` — `DbContext dataProvider`
(`Visitors/BaseExpressionVisitor.cs:10,26`), тогда как `QueryCommand` уже работает через
`IDataContext` (`Query/QueryCommand.cs:62`). Инстанцирование — `new SqlBuilder(this, ...)`
(`DbContext.cs:178`).

**Первопричина.** Абстракция есть только на внешней границе; внутренние коллабораторы
привязаны к конкретному провайдеру.

**Влияние.** In-memory провайдер не может переиспользовать SQL/visitor-конвейер;
невозможно подставить тестовый диалект; риск циклической зависимости сборок.

**Исправление (частично).** Введён `ISqlDialect`: `SqlBuilder` принимает его вместо
`DbContext`, `BaseExpressionVisitor`/`WhereExpressionVisitor` — `ISqlDialect` + `ILogger?`,
`DbContext.MakeSelect` создаёт builder из `Dialect`. Диалект тестируется/подставляется в
изоляции (SQL-тесты теперь проверяют `ctx.Dialect`), in-memory не обязан реализовывать
SQL-конвейер. Вторая половина остатка — ось маппинга — закрыта позже: `ReplaceMemberVisitor`
оказался мёртвым кодом и удалён, построение маппера вынесено в `RowMapperFactory`, а политика
колонки передаётся делегатом (см. F1, ось маппинга).

**Остаток (дозакрыт частично).** Ранее `BaseExpressionVisitor` статически тянулся в `DbContext`:
10 обращений к `DbContext._sbPool` и `DbContext.GetParamName`, плюс 2 обращения к тому же пулу из
`ResultSetEnumerator`. Статическая связка снята:

- `StringBuilderPool.Shared` (`DataContext/StringBuilderPool.cs`) — единственный пул на процесс;
  `SqlBuilder`, визиторы и `ResultSetEnumerator` берут буфер из него, а не из `DbContext`.
- `BaseExpressionVisitor` и `ResultSetEnumerator` принимают `ObjectPool<StringBuilder>?` последним
  необязательным параметром и по умолчанию используют `StringBuilderPool.Shared`; вложенные
  визиторы получают пул родителя, поэтому подмена пула (тестовый шов) сквозная.
- `norm_pN` — контракт между SQL-текстом и `DbCommand.Parameters` — вынесен в `NormParam`
  (`DataContext/NormParam.cs`, `GetName`/`IsName`): им пользуются визитор
  (`BaseExpressionVisitor.cs:177`) и `DbPreparedQueryCommand`
  (`DataContext/Cache/DbPreparedQueryCommand.cs:60`). `DbContext.GetParamName` удалён, приватный
  `IsRuntimeParam` делегирует в `NormParam.IsName` (`DbContext.cs:203`).

**Было не закрыто (диагноз до правки).** `ResultSetEnumerator` держал конкретный `DbContext`: поле
`_dbContext` (`ResultSetEnumerator.cs:13`), публичное свойство `DbContext` (`:46`),
`InitEnumerator(DbContext, …)` (`:145`, вызовы — `DbContext.cs:679,867`) и чтение/запись
внутреннего флага `_connOpen` (`:158,165,180,188`). Публичное свойство `DbContext` (`:46`) при
этом читает и сбрасывает `DbPreparedQueryCommand` (`:117-118`), т.е. связка двусторонняя. От
статического пула тип отвязан, от класса — нет. Прежняя формулировка «зависимостей нет, осталось упоминание в комментарии»
была неточной: она описывала только статическую часть.

**Вторая связка, ранее не отмеченная.** `DbPreparedQueryCommand.GetDbCommand(…, DbContext, …)`
(`DataContext/Cache/DbPreparedQueryCommand.cs:35,38`) тоже принимает конкретный `DbContext`, и в
«Не закрыто» он не значился. Существенно, что **вся** эта зависимость — один вызов:
`dataContext.CreateParam(paramName!, @params[i])` (`:99`). `CreateParam` — `public abstract` на
`DbContext` (`:510`), переопределён всеми тремя провайдерами.

**Почему «сузить параметр до роли» сегодня не проходит.** `_connOpen` — `internal`-поле
`DbContext` (`:28`), а в `IConnectionManager` его нет: роль даёт только `GetConnection()` и
`EnsureConnectionOpen(Async)` (`Roles/IConnectionManager.cs:13-15`). Пока энумератор читает и
пишет это поле напрямую, сужение `InitEnumerator` до роли невозможно.

**Блок открытия соединения продублирован 4 раза:** `DbContext.GetDbCommand<TResult>` — span-перегрузка
(`:230`, блок `:238-248`) и async (`:256`, блок `:266-277`), плюс `ResultSetEnumerator.InitReader`
(`:158-166`) и `InitReaderAsync` (`:180-189`); при этом `EnsureConnectionOpen(Async)` (`:76`,
`:88`) уже выражает ту же логику один раз и входит в роль. Различие: в хелперах `DbContext` и в
энумераторе блок стоит под внешним `if (!_connOpen)`, а `EnsureConnectionOpen` проверяет состояние
соединения всегда (и выставляет `_connOpen = true` безусловно).

**✅ Закрыто (реализовано).** Шов оказался ровно тем, что был намечен выше.

- **`CreateParam` — делегатом.** `DbPreparedQueryCommand.GetDbCommand` принимает
  `Func<string, object?, DbParameter>` вместо `DbContext`; вызов по-прежнему один — `:99`.
  `DbContext` связывает абстрактный метод в поле один раз (`_createParam = CreateParam` в
  конструкторе), поэтому провайдерский override остаётся на пути диспетчеризации, а делегат не
  аллоцируется на команду.
- **`ResultSetEnumerator` — на роль.** Поле `_dbContext` заменено на `IConnectionManager` +
  делегат создания параметров; публичное свойство `DbContext` удалено; дублированный блок
  открытия соединения убран в пользу `EnsureConnectionOpen(Async)`; `ResetConnection` дёргает
  `DetachFrom(IDataContext)`. Логирование (`ResultSetEnumeratorLogger`, `LogSensitiveData`)
  передаётся один раз через `InitEnvironment`, а не вытягивается из контекста свойством.
- **`InitEnumerator` стал `internal`** — вызывается только из `DbContext` (`:677,865`), публичной
  поверхностью быть не обязан.
- **Токен отмены сохранён:** `IConnectionManager.EnsureConnectionOpenAsync` получил
  `CancellationToken cancellationToken = default` (роль реализует только `DbContext`), иначе
  отмена при открытии соединения потерялась бы.
- **Дедупликация:** блок открытия соединения жил **4 раза** — `DbContext.GetDbCommand<TResult>`
  (span и async) и `ResultSetEnumerator.InitReader`/`InitReaderAsync`. Осталось одно выражение —
  `EnsureConnectionOpen(Async)`; оба хелпера `DbContext` теперь тоже зовут его.

Проверено по коду: `DbContext` в `ResultSetEnumerator.cs` и `DbPreparedQueryCommand.cs` — **0**
вхождений в коде (остались только упоминания в комментариях). В `src` конкретный `DbContext`
теперь фигурирует лишь в самом типе, DI-слое, провайдерских подклассах и комментариях.

Замер (`ParamsAllocationBenchmark`, fast/ShortRun, до/после): аллокации **идентичны** —
`GetDbCommand_1Arg_ReusedArray` 2.34 KB, `_Params` 5.47 KB, `Nextorm_Any_1Arg_Params` 85.16 KB,
`_2Arg_ReusedArray` 117.2 KB; тайминги в пределах шума. То есть рефакторинг перф-нейтрален.
Интеграционный набор: **561, Failed 0, Skipped 16**.

Цена (осознанная): «Opening connection» теперь логирует `Logger` контекста, а не
`ResultSetEnumeratorLogger`; `EnsureConnectionOpen` проверяет `conn.State` на каждом вызове
(чтение enum).

**Ревалидация после параллельной работы** (CTE, `Distinct`, `JoinType`, IN-транслятор,
string/math-функции, `Not`). SQL-генерация осталась чистой: `DbContext` не упоминается ни в одном
из ключевых файлов — `SqlBuilder`, `BaseExpressionVisitor`, `WhereExpressionVisitor`, `CteQuery`,
`InValuesEvaluator`, `QueryCommand.Prepare`, `SelectExpression`, `Entity`, `JoinCommandBuilder` —
**0** вхождений в каждом. Новый `CteQuery` зависит от `IDataContext` (абстракция), а не от класса,
т.е. регресса F6 параллельная работа не внесла.

Проверено: `DbContext._sbPool` / `DbContext.GetParamName` в `src` — **0** вхождений; сборка 0/0;
контракт `norm_pN` явно утверждается в `SqlGenerationTests.cs` всех трёх провайдеров (7 проверок)
и проходит.

---

## F7. Пустой алиас + перегруженный `IQueryProvider` (ISP/YAGNI, Низкая)

**Доказательство.** `Query/IQueryContext.cs` — `public interface IQueryContext :
IQueryProvider {}` без членов. Во всём решении он встречался ровно 3 раза: объявление,
`QueryCommand : IQueryContext` и параметр `DbContext.MakeSelect(..., IQueryContext, ...)`,
который сразу уходил в `SqlBuilder`, где тип параметра — уже `IQueryProvider`. Ни одной
проверки `is IQueryContext`, constraint, DI-скана или рефлексии нет; тестовый
`QueryProvider : IQueryProvider` (`PreciseExpressionEqualityComparerTests.cs`) работал, не
реализуя `IQueryContext`. Отдельно: `Query/IQueryProvider.cs` обязывает реализации давать
6 фабрик equality-comparer'ов — это хеширование планов, а не «провайдер запроса»; их
единственные потребители — классы-компараторы.

**Первопричина.** Алиас вместо абстракции: имя `IQueryContext` обещало больше, чем базовый
интерфейс, но не добавляло ни контракта, ни наблюдаемости. В базовом `IQueryProvider` слиты
две роли: «граф запроса» и «хеширование плана».

**Влияние.** Алиас — лишнее имя в публичном API, оно вводит в заблуждение (обещает
«контекст») и отсекает валидные реализации: `QueryProvider : IQueryProvider` нельзя передать
туда, где ждут `IQueryContext`. ISP-часть на сегодня **теоретическая**: реализаций
`IQueryProvider` в проде одна (`QueryCommand`) плюс один тестовый дабл.

**Исправление.**
- **Сделано:** `IQueryContext` удалён (файл + 2 ссылки); `QueryCommand : IQueryProvider`,
  `MakeSelect` принимает `IQueryProvider`. Интерфейсов стало меньше, поведение не изменилось.
- **Отложено (YAGNI):** выделение `IPlanComparerFactory` и
  `IQueryContext : IQueryProvider, IPlanComparerFactory` дало бы узкие зависимости, но
  второй реализации/потребителя нет, а тестовый дабл платит лишь 6 однострочных заглушек.
  Вернуться к рефактору при триггере: (1) второй реальный `IQueryProvider`;
  (2) компонент, которому нужен провайдер **без** comparer-машинерии (внешний/сериализуемый
  кэш планов); (3) публичные comparer'ы со своими потребителями.

---

## F8. DI-регистрации: дублирование и двойной инстанс (DRY/DIP, Средняя)

**Доказательство.** `DI/ServiceCollectionExtensions.cs`: 6 почти одинаковых оверлоадов;
тела `Action<DbContextBuilder>`- и `Action<IServiceProvider,DbContextBuilder>`-вариантов
дублируются (`:7-24` против `:43-61`; keyed — `:25-42` против `:62-80`).
`AddNextOrmContext<T>` регистрирует `IDataContext -> T` и `T` **отдельно** (`:84-85`) ->
два разных экземпляра контекста в одном scope; при `optionsBuilder == null` резолвится
незарегистрированный `DbContextBuilder` (`:19-23`, `:56-60`) -> `InvalidOperationException`
в рантайме (в тестах спасает только `AddNextOrmContext<InMemoryContext>`).

> Номера строк — до фикса; файл с тех пор переписан (см. «Исправление»).

**Первопричина.** Копипаста оверлоадов без общей фабрики; нет единой регистрации на роль.

**Влияние.** Тонкие DI-баги; потребитель `T` и потребитель `IDataContext` видят разное
состояние.

**Исправление (реализовано).** Шесть оверлоадов сведены к двум приватным хелперам:
`RegisterContextFactory` (options-driven: scoped `DbContextBuilder` + `IDataContext`-фабрика) и
`RegisterContextType<T>` (type-driven: scoped `T`, затем `IDataContext -> sp.GetRequiredService<T>()`);
в каждом — вариант для keyed-сервисов. Что починено:

- `AddNextOrmContext<T>` / `AddKeyedNextOrmContext<T>` больше не регистрируют `T` и `IDataContext`
  независимо — один инстанс на scope (потребитель `T` и потребитель `IDataContext` видят одно
  состояние);
- options-делегат стал обязательным: `ArgumentNullException` на регистрации вместо
  `InvalidOperationException` на резолве (`DbContextBuilder` без options не может создать контекст);
- 4 фабричных оверлоада делегируют в один хелпер вместо копипасты тел.

Покрыто `test/nextorm.core.tests/DependencyInjectionTests.cs` (5 тестов: identity `T`/`IDataContext`,
keyed-вариант, переиспользование scoped-билдера, fail-fast на `null`).

---

## F9. `BaseExpressionVisitor` — God class + switch на именах (SRP/OCP, Средняя)

**Доказательство (сверено с текущим деревом).** `Visitors/BaseExpressionVisitor.cs` —
**2128 строк** (в `afb0189` было 1048; на предыдущую сверку — 2223, с тех пор класс немного
сократился). `ExpressionVisitor, ICloneable, IDisposable`. Диспетчеризация по именам осталась:
switch по `nameof(TableAlias.Long/Int/Boolean/...)` теперь в pattern-форме
(`or nameof(...)`, `:123+`). 6 вхождений `NotImplementedException` сохранились (количество не
изменилось, номера строк сдвинулись). Добавлены `TryTranslateFunction` (`:541`) и
делегирование в `ISqlDialect.Make*` (`MakeUpper`/`MakeLower`/`MakeTrim`/`MakeSubstring`/…) —
часть OCP-долга из F9 уже ушла в диалект.

**Влияние.** Новый метод `TableAlias` требует правки визитора (OCP); класс совмещает
обход выражений, рендеринг SQL, разрешение алиасов и работу с параметрами.

**Исправление.** Разбить по конструкциям (резолвинг TableAlias, `NORM.Param`,
`NORM_SQL.exists/any/all`, числовые конверсии); switch по именам заменить таблицей
`Dictionary<string, Func<...>>` или полиморфизмом.

---

## F10. Дублирование агрегатов в `Entity<TEntity>` (DRY, Низкая)

**Доказательство.** `Builders/Entity.cs:448-575` — Min/Max/Avg/Sum/Stdev/Stdevp/Var/Varp.
Каждое семейство — 5 членов (`X(exp)`, `X(exp, params ReadOnlySpan<object?>)`, приватный
`XCore`, `XAsync(exp, params object[])`, `XAsync(exp, CancellationToken, params object[])`),
итого **40 членов**. Все 8 sync-тел и 8 async-тел отличаются **только** `MethodInfo`
(`NORM.NORM_SQL.MinMI`/`MaxMI`/`AvgMI`/`SumMI`/`StdevMI`/`StdevpMI`/`VarMI`/`VarpMI`);
остальное — `Expression.Call(NORM.NORM_SQL.SQLExpression, MI.MakeGenericMethod(typeof(TResult)),
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
(`=> AggregateCore(NORM.NORM_SQL.MinMI, exp, @params)`), атрибуты
`[MethodImpl(AggressiveInlining)]` на async-обёртках сохранены как были. **Публичные сигнатуры
не изменены ни в одной из 40 перегрузок** — это внутренняя чистка, а не правка API. Итог:
`Builders/Entity.cs` 674 → **602** строки (−72), приватных ядер 8 → 2, копий формулы —
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
  `Select(e => NORM.SQL.count())`, то есть через LINQ-форму, а не через
  `NORM_SQL.SQLExpression` + `MethodInfo`, поэтому в `AggregateCore` он не укладывается без
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
(`test/nextorm.integration.tests/CommonTestSuite.Aggregates.cs`: 109 → 378 строк, 24 теста) —
`Stdevp` (в т.ч. точная `double`-проекция через `BeApproximately`), `Var`/`Varp`, все 8
`*Async` и 10 тестов на `params`-перегрузку (через `NORM.Param<int>(0)`). Ожидаемые значения
считались по формулам (для 1..10: sample var = 82.5/9, population var = 82.5/10), а не
подгонялись под вывод. Заодно доказано, что int-проекция **округляет** (`Convert.ToInt32`,
midpoint-to-even), а не усекает: `Stdevp` = 2.8722813232690143 → 3 (усечение дало бы 2).

**Остаточный пробел (закрыт).** SQLite регистрировал только `stdev`/`stdevp`, поэтому
`Entity.Var`/`Varp`/`VarAsync`/`VarpAsync` падали с «no such function». В
`src/nextorm.sqlite/SQLiteFunctions.cs` добавлены агрегаты `var`/`varp`: аккумулятор (он уже нёс
`Count`/`Sum`/`SumSq`) переименован в `VarianceAccumulator`, а его `Final` разделён на
`FinalVariance` — она возвращает **дисперсию**, `stdev`/`stdevp` берут от неё `Math.Sqrt`,
`var`/`varp` возвращают напрямую. Одна реализация на четыре функции вместо двух копий; значения
`stdev`/`stdevp` не изменились. `ITestProvider.SupportsVarianceAggregates` для SQLite → `true`.

Маппинг имён не потребовался: `SqliteDialect` не переопределяет `MakeAggregate`, поэтому
`var`/`varp` уходят в SQLite как есть и совпадают с именами зарегистрированных функций.
Postgres для сравнения маппит `stdev`→`stddev`, `stdevp`→`stddev_pop`, `var`→`variance`,
`varp`→`var_pop`.

Отдельно: `docs/sql-capabilities-gap-analysis.md` помечает агрегаты `**yes**` на уровне
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
`InMemoryContext` держал собственные `_metadata` и `_selectListCache` (тоже `static`) и
инстансный `_expCache`. Проверка по коду показала:

- писателей в `InMemoryContext._metadata`/`_selectListCache` — **0** (метаданные наполняются через
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
`Metadata`/`SelectListCache`; `MapperCache` (process-wide) и `DbContext._queryPlanCache`
(per-thread, ключ `QueryPlanCacheKey(ContextType, Plan)`) — отдельные понятия, не дубли.

**Влияние (было).** Два объявления одного понятия; `Metadata`/`SelectListCache` у in-memory
навсегда пустые, что маскировало бы ошибку при чтении их через контекст; scope кэшей нигде не был
зафиксирован и выводился археологией.

**Исправление (сделано).** Единый источник — `DataContextCache`. У `InMemoryContext` удалены
`_metadata`/`_selectListCache` и осиротевший `using System.Collections.Concurrent`; свойства
`Metadata`/`SelectListCache` делегируют в `DataContextCache` (публичная сигнатура сохранена).
`_expCache` оставлен инстансным с документирующим комментарием; `_conditionFactoryCache` /
`_conditionDirectCache` помечены как in-memory-специфичные (SQL-аналога нет — не дубли). Область
шаринга **явно задокументирована** в `<remarks>` у `DataContextCache`: process-wide — он и
`MapperCache`; per-instance — `InMemoryContext.ExpressionsCache`; per-thread —
`DbContext._queryPlanCache`. Регресс закрыт `test/nextorm.core.tests/DataContextCacheScopeTests.cs`
(4 факта: `BeSameAs` для общих кэшей, `NotBeSameAs` — для инстансного и для двух контекстов).

---

## Что НЕ является нарушением (чтобы не переусердствовать с рефакторингом)

- `IEntityMeta`/`IPropertyMeta` (`Meta/*.cs`) — маленькие, связные, оправданные
  абстракции. Оставить как есть.
- `IAliasProvider`, `IParamProvider`, `IColumnsProvider`, `IDbCommandHolder` — хорошо
  сегрегированы. Оставить как есть.
- Провайдерские подклассы `Sqlite/Postgres/SqlServerDbContext` — тонкие и
  целенаправленные; наследование здесь уместно (расширение диалекта).
- `MapperCacheKey` (readonly record struct) — корректный value-key, а не «IFoo/Foo ради
  галочки».
- `DefaultParamProvider`/`GetParamName` — осознанная оптимизация (без `string.Format`),
  не нарушение.
- `First`/`FirstOrDefault`/`Single`/`SingleOrDefault` в `Entity<TEntity>`
  (`Builders/Entity.cs:295-372`) — форвардинг на разные методы `QueryCommand`; общей логики
  нет, схлопывание невозможно без потери типизации. Не путать с F10, где логика
  действительно продублирована.

## Приоритеты исправления

1. **P0** — F2 (ISP): ✅ **реализовано** — ролевые интерфейсы, `IDataContext` как пустой
   композит, вынос provider-независимых оверлоадов в `DataContextExtensions`.
2. **P1** — F1 **частично**: оси диалекта (`ISqlDialect`, + F6), соединения (дедупликация в
   базу + `IConnectionManager.GetConnection`) и маппинга (`RowMapperFactory`) закрыты.
   Осталось только разгрузка исполнения.
3. **P1** — F5: ✅ **реализовано** — бросающих дефолтов в `DbContext` не осталось.
4. **P2** — F9. F11 ✅ **реализовано** (единый `DataContextCache` + задокументированный scope).
   F7: пустой алиас удалён, ISP-часть `IQueryProvider` отложена по YAGNI (см. F7 — триггеры).
5. **Реализовано:** F4, F3, F2, F8, F5, F10, **F11** (+ ось диалекта F1) — см. «Статус
   реализации». **F6 — закрыт:** снята и статическая связка (`_sbPool`, `GetParamName`), и
   зависимость исполнения от конкретного `DbContext` — см. F6, «✅ Закрыто».

## Оговорка для performance-работ

`performance-findings.md` и `performance optimizations.md` описывают оптимизации
горячего пути. Перед рефакторингом F1/F6 зафиксируйте benchmark-базу
(`benchmark-report.md`); иначе есть реальный риск откатить уже сделанную работу.
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
- **F3 — частично реализовано.** 27 инлайн-guard'ов сведены в единый
  `AsDbCommand<TResult>` / `AsInMemoryCommand<TResult>`, вызываемый на входе публичных
  методов; `NotSupportedException` заменён на `ArgumentException` с пояснением. Остаётся
  контракт: default-методы интерфейса по-прежнему позволяют передать чужой контекст.
- **Удалён `IsScalar`.** Член `IPreparedQueryCommand<TResult>.IsScalar` убран целиком
  (интерфейс, `PreparedQueryCommand`, `DbPreparedQueryCommand`, `InMemoryPreparedQueryCommand`);
  `DbContext.First/FirstOrDefault/FirstAsync/FirstOrDefaultAsync` определяют scalar-режим по
  `compiledQuery.MapDelegate is null`.
- **F2 — реализовано (ISP).** Разбито на роли в `DataContext/Roles/`:

  | Роль | Члены | Кто реализует |
  |---|---|---|
  | `IConnectionManager` | `EnsureConnectionOpen(Async)` | только `DbContext` |
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
  - **Удалены заглушки-пустышки**: `InMemoryContext.EnsureConnectionOpen() { }` и
    `EnsureConnectionOpenAsync() => Task.CompletedTask` больше не нужны — провайдер без
    соединения не обязан его реализовывать. `DbContext : IDataContext, IConnectionManager`.
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
  - Потребители, которым реально нужен конвейер (`Entity`, `QueryCommand`, DI,
    `DataContextOptionsBuilder.Factory`), по-прежнему принимают `IDataContext` — это
    осознанно: они используют 4 роли сразу, сужение до одной было бы фиктивным.
  - Бенчмарки и `PlanCacheTests` переведены на явный `((IConnectionManager)ctx).EnsureConnectionOpen()`.
  - Проверка: build 0 warnings/0 errors; тесты core 85, sqlite 22 (1 skipped —
    документированный баг), postgres 16, sqlserver 19; `f4check` — ALL OK.
- **F2: что осталось (осознанно).** F1/F6 (`DbContext` как God class и зависимость
  `SqlBuilder`/визиторов от конкретного `DbContext`) — отдельная работа: роли разделяют
  *контракт*, но не разбивают 1357-строчную реализацию. Пока F1/F6 открыты, `DbContext`
  остаётся и композитом ролей, и диалектом, и конвейером.
- **F8 — реализовано (DI/DRY).** `ServiceCollectionExtensions` сведён к двум приватным хелперам
  (`RegisterContextFactory` / `RegisterContextType<T>`, каждый с keyed-веткой). Двойная регистрация
  `T` + `IDataContext` заменена на forwarding (`IDataContext -> sp.GetRequiredService<T>()`) —
  один инстанс контекста на scope. Options-делегат стал обязательным: fail-fast
  `ArgumentNullException` на регистрации вместо падения на первом резолве. Добавлены
  `DependencyInjectionTests` (5 тестов). Core-тесты: 85 → 90.
- **F7 — частично реализовано.** `IQueryContext` (пустой алиас `: IQueryProvider`, никем не
  наблюдаемый) удалён: файл + 2 ссылки (`QueryCommand : IQueryProvider`,
  `DbContext.MakeSelect(..., IQueryProvider, ...)`). Интерфейсов стало на один меньше.
  Разделение `IQueryProvider` (граф запроса) / `IPlanComparerFactory` (6 comparer-фабрик)
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
    `test/nextorm.sqlite.tests/MetadataRegistrationTests.cs`.
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
  - `DbContext` объявлен `abstract`; владеет `public abstract ISqlDialect Dialect`, а все
    `Make*`/`Escape`/`Require*`/`MakePage`/`MakeTop` из него убраны. `CreateConnection` и
    `CreateParam` стали `abstract` (оба переопределяют все три провайдера).
  - **F5 закрыт полностью:** в `DbContext` не осталось ни одного бросающего дефолта. Пара
    `RequireSorting`+`EmptySorting` сведена в один `ISqlDialect.GetPagingOrderBy(QueryCommand)`,
    возвращающий `null`, когда сортировка не нужна (SQL Server — константный
    `(select null as anyorder)`), — капабилити выражено возвращаемым значением, а не броском.
  - **F6 закрыт:** `SqlBuilder` принимает `ISqlDialect` (было
    `DbContext`), `BaseExpressionVisitor`/`WhereExpressionVisitor` — `ISqlDialect` + `ILogger?`,
    `DbContext.MakeSelect` строит builder из `Dialect`. Побочно убран мёртвый параметр
    `IDataContext` у `MemberInfoExtensions.GetPropertyColumnName` (метод читает статический
    кэш метаданных и параметр игнорировал). **Остаток (исполнение) закрыт позже:**
    `ResultSetEnumerator`/`DbPreparedQueryCommand` переведены на `IConnectionManager` + делегат
    `CreateParam` — см. F6, «✅ Закрыто».
  - Тесты: `ctx.Escape(...)`/`ctx.RequireSubqueryAlias` переведены на `ctx.Dialect.*`
    (5 мест). Ни один SQL-тест не менялся по существу — значит вывод диалектов идентичен.
  - Проверка: build 0/0; core 90, sqlite 24, postgres 16, sqlserver 19; integration 309
    (Failed 0); `f4check` — OK. SQL-тесты диалектов (`SqlGenerationTests`) — зелёные во всех
    трёх провайдерах, что и есть главное доказательство неизменности рендеринга.
  - Осталось по F1: SRP-разбиение самой 1095-строчной реализации (`DbContext` — и композит ролей,
    и диалект, и конвейер); DIP-часть исполнения закрыта вместе с F6.
- **F1 (ось соединения) — реализовано; сознательное отклонение от исходной формулировки F1.**
  В F1 значился `IConnectionFactory` (`CreateConnection`/`CreateParam`). Разбор показал, что
  (1) `CreateParam` — не connection-ось (он в одной компании с `MakeParam`/`GetParamName`) и
  (2) три провайдера содержали почти дословную копию `CreateConnection`/`ConnectionString`, а
  единственный потребитель «фабрики» — сам `DbContext`. То есть это DRY-дефект, а не
  отсутствующий шов, и заведение интерфейса дало бы концепцию без точки подстановки. Поэтому:
  - `DbContext` владеет `_connectionString`/`_providedConnection`/`ConnectionString` и общим
    скелетом `CreateConnection()` (логирование, short-circuit на внешнее соединение, привязка
    событий, учёт владения);
  - провайдер реализует `protected abstract DbConnection CreateDbConnection(string?)` и, при
    необходимости, `protected virtual void OnConnectionCreated(DbConnection)`; у SQLite это
    `SQLiteFunctions.Register`, который, как и раньше, вызывается и для внешнего соединения;
  - `_connWasCreatedByMe` выставляет база — устранена инверсия «база зависит от побочного
    эффекта в коде наследника»; мёртвая развилка `if (!_connWasCreatedByMe)` после
    `= true` исчезла;
  - `CreateConnection` из `abstract` стал `virtual` с рабочим телом (ещё одно обязательство
    контракта снято, в духе F5); `CreateParam` остался `abstract` — это реально варьирующаяся
    фабрика ADO-параметра, реализованная всеми провайдерами;
  - `IConnectionManager` получил `GetConnection()`: роль соединения стала осмысленной
    (`EnsureConnectionOpen` + доступ к соединению), а не только «открой его»;
  - сохранён `protected DbContext(DbContextBuilder)` — наследники, вызывавшие
    `base(builder)`, компилируются без изменений.
  - Эффект: −79 строк дублирования (207 → 128 в трёх провайдерах), `ConnectionString` ×3 → ×1;
    поведение (включая регистрацию функций на внешнем соединении и то, что внешнее соединение
    не диспозится контекстом) сохранено.
  - Покрытие: `test/nextorm.sqlite.tests/ConnectionManagementTests.cs` (10 тестов) — владение
    соединением (внешнее остаётся открытым, своё диспозится), `ConnectionString` в обоих
    режимах, роль `IConnectionManager`, регистрация функций на внешнем соединении, сквозной
    запрос через внешнее соединение.
  - Проверка: build 0/0; core 95, sqlite 67 (все connection-тесты зелёные), postgres 40,
    sqlserver 44; integration 366 (Failed 0); `f4check` — OK.
- **F1 (ось маппинга) — реализовано; `IColumnMapper` сознательно не введён.** Построение и
  компиляция строкового маппера вынесены из `DbContext` в
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
  - В `DbContext` остались только `public virtual Expression MapColumnExpression(...)`
    (делегирует в `RowMapperFactory.MapColumn`; переопределяется `SqlServerDbContext`) и
    `GetMapCached` в одну строку. `MapColumn`, `GetMap`, `BuildMapperKey` и `IsDBNullMI` из
    контекста убраны — заодно из публичной поверхности ушли `public static MapColumn` и
    `public GetMap` (наружу не использовались). `IsDBNull`-`MethodInfo` теперь локальная статика
    в `RowMapperFactory` и `SqlServerDbContext`.
  - **Удалён мёртвый `Visitors/ReplaceMemberVisitor.cs`.** В HEAD он использовался в `GetMap`
    (`DbContext.cs:989`), но рефакторинг F4 (`RowMaterializerBuilder`) его осиротил: ни одного
    `new ReplaceMemberVisitor` ни в `src`, ни в `test`, ни в `benchmarks` (проверено grep'ом по
    всему репозиторию). Та же история, что `GetColumnName`/`GetTableName` в F5.
  - `ConvertScalar` оставлен в `DbContext`: это конверсия скалярного результата для
    `ExecuteScalar`/`First`, а не маппинг строк (другая ось).
  - `DbContext`: 1156 → 1104 строки.
  - Проверка: build 0/0; core 95, postgres 40, sqlserver 44, integration 366 (Failed 0),
    `f4check` — OK. В sqlite 78 тестов, единственное падение —
    `Contains_CapturedArray_ShouldRenderInPredicate` в `BaseExpressionVisitor.GetInValues`:
    это параллельная (незавершённая на момент прогона) правка нового `IN`-транслятора, в HEAD
    этих методов нет, к маппингу отношения не имеет.
- **Нумерация строк** приведена к текущему рабочему дереву (после F4). Правка F4 сдвинула
  номера в `DbContext.cs` после ~1019 и в `InMemoryDataContext.cs` после ~636, поэтому
  ссылки в F1/F5 на диапазоны `DbContext` правее ~1019 могут отличаться на величину
  вырезанного блока.
- **F10 — реализовано (DRY).** Все 16 тел агрегатов `Entity<TEntity>` сведены к двум приватным
  ядрам: `AggregateCore<TResult>` (sync) и `AggregateAsyncCore<TResult>` (async). Оба принимают
  `MethodInfo` (`NORM.NORM_SQL.MinMI`/`MaxMI`/`AvgMI`/`SumMI`/`StdevMI`/`StdevpMI`/`VarMI`/`VarpMI`) —
  это единственное, чем отличались 8 семейств. Публичные сигнатуры не тронуты ни в одной из 40
  перегрузок, `[MethodImpl(AggressiveInlining)]` на async-обёртках сохранены. `Entity.cs`
  674 → 602 строки, приватных ядер 8 → 2.
  - Заодно закрыт остаточный пробел SQLite: в `SQLiteFunctions` зарегистрированы `var`/`varp`,
    аккумулятор переименован в `VarianceAccumulator`, его `Final` разделён на `FinalVariance`
    (дисперсия) — `stdev`/`stdevp` берут от неё `Math.Sqrt`, `var`/`varp` возвращают напрямую.
    `SupportsVarianceAggregates` для SQLite → `true`.
  - Проверка: build 0/0; core 107, sqlite 136, postgres 90, sqlserver 114 (везде Failed 0);
    integration 561 (скипов 16, падений по агрегатам 0), в `SqliteIntegrationTests` скипы
    10 → 2; `f4check` ALL OK.
- **F11 — реализовано (DRY).** Единый источник кэшей — `DataContextCache`; у `InMemoryContext`
  удалены мёртвые дубли-статики `_metadata`/`_selectListCache` (свойства делегируют, публичный
  API сохранён), `_expCache` оставлен инстансным осознанно, scope кэшей задокументирован, добавлен
  `DataContextCacheScopeTests` (4 теста). Доказательства — в самом разделе F11.
  - Проверка: build 0/0; core **111** (+4), sqlite 143, postgres 90, sqlserver 114 — везде
    Failed 0; `f4check` ALL OK.
- **`[MethodImpl(AggressiveInlining)]` — выравнивание конвенции + измерение (перф-работа, не
  SOLID).** Устранено расхождение, помеченное в F10: атрибут стоял только на async-обёртках
  `Entity<TEntity>`, но не на sync. Добавлено 27 атрибутов (99 → 126): 16 sync-обёрток агрегатов и
  8 форвардеров в `Entity.cs`, `MapperCache.TryGet`, `DbContext.IsRuntimeParam`.
  - **Измерено** (`MicroOptimizationsBenchmark.M12`, fast/ShortRun): на чистых форвардерах атрибут
    **не даёт ничего** (678.7 ns против 698.6 ns — внутри шума, StdDev 40–58 ns): JIT инлайнит их и
    так. На leaf-методе с циклом (`for` ×4 + ветвление) — **−20.5%** (2447.0 → 1944.4 ns), т.е.
    атрибут работает именно там, где JIT отказывается инлайнить сам.
  - **Вывод:** достижимого выигрыша от атрибута в этом коде нет — построчная работа сидит внутри
    `Expression.Compile()`-делегата (атрибуты до него не достают), а циклы по колонкам уже инлайн.
    Добавленные 25 атрибутов в `Entity.cs` — **консистентность конвенции, не ускорение**.
  - Побочно: существующий `M10` (sealed vs unsealed) не измеряет девиртуализацию — оба плеча берут
    экземпляр из `static readonly` конкретного поля, и JIT девиртуализирует оба.
- **F6 (остаток — исполнение) — реализовано (DIP/DRY).** `DbPreparedQueryCommand.GetDbCommand`
  принимает `Func<string, object?, DbParameter>` вместо `DbContext` (вся зависимость была в одном
  вызове `CreateParam`); `ResultSetEnumerator` переведён на `IConnectionManager` + делегат,
  `_dbContext`/публичное свойство `DbContext`/reach-in в internal-поле `_connOpen` убраны,
  `InitEnumerator` стал `internal`. Блок открытия соединения жил **4 раза** — осталось одно
  выражение `EnsureConnectionOpen(Async)`, включая оба хелпера `DbContext`. Токен отмены сохранён:
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
(`DbContext.cs`; в `InMemoryDataContext.cs` — симметричный `AsInMemoryCommand`):

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

До рефакторинга этот же guard был скопирован 27 раз (14 в `DbContext`, 13 в
`InMemoryContext`) и бросал `NotSupportedException`; сейчас он один на контекст.

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

Реляционные провайдеры (Sqlite/Postgres/SqlServer) наследуют `DbContext` и переиспользуют
один `DbPreparedQueryCommand<TResult>`; `GetPreparedQueryCommand` не virtual. Значит,
добавление нового реляционного провайдера не требует правок в ядре — «closed for
modification» соблюдён. Цена возникла бы только при желании задать иную модель исполнения
(как in-memory), но это по определению отдельный класс контекста, а не модификация
существующего.

Поэтому `is` — не dispatch-проблема, а guard инварианта. Остаётся вопрос контракта:
абстракция принимает любой импл, а поддерживает один (A.5).

Внутренний downcast в кэше — деталь реализации `DbContext`, DIP не нарушает:

```csharp
// DbContext.cs:17-18,23,414
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
`InMemoryPreparedQueryCommand` в `DbContext` — получите `ArgumentException` в рантайме
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
> абстракции уже есть — остаётся только разгрузить исполнение в `DbContext`.

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

Проверено вручную на связке «in-memory команда → `SqliteDbContext`».
