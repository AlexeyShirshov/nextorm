# Обзор SOLID / DRY — nextorm

Read-only ревью дизайна core-библиотеки `nextorm` на соответствие принципам SOLID и
DRY. Код не изменялся. Ссылки на находки — в формате `file:line`.

> **Примечание об области.** Это дизайн-ревью, а не анализ производительности.
> Механика DI-контейнера (регистрация/времена жизни) формально относится к
> `dotnet-csharp-dependency-injection`; здесь она приведена только на уровне дизайна.
> Идентификаторы находок (F1..F11) сохранены стабильными для перекрёстных ссылок;
> порядок изложения ниже начинается с **F4** по запросу.

---

## Сводка

| # | Симптом | Принцип | Критичность | Статус |
|---|---|---|---|---|
| F4 | Логика материализации строк продублирована в обоих контекстах | DRY | Средне-высокая | ✅ Реализовано |
| F1 | `DbContext` — 1357 строк, 81 объявление, 5+ осей изменений | SRP | **Высокая** | Открыто |
| F2 | `IDataContext` — ~60 членов на все роли | ISP | **Высокая** | ✅ Реализовано |
| F3 | 27 guard-проверок `is …` + `NotSupportedException`; контракт протекает | LSP/DRY | Средняя | Частично |
| F5 | `NotImplementedException`/`NotSupportedException` как контракт базы | LSP | Средняя | Открыто |
| F6 | SQL-билдер и визиторы зависят от конкретного `DbContext` | DIP | Средняя | Открыто |
| F7 | `IQueryContext` — пустой алиас; `IQueryProvider` перегружен хешированием плана | ISP/YAGNI | Низкая | Маркер удалён; ISP отложен |
| F8 | 6 дублирующихся DI-оверлоадов + двойная регистрация | DRY/DIP | Средняя | ✅ Реализовано |
| F9 | `BaseExpressionVisitor` — 1035 строк, switch на 20 имён | SRP/OCP | Средняя | Открыто |
| F10 | 24 почти идентичных aggregate-метода в `Entity<TEntity>` | DRY | Низкая | Открыто |
| F11 | Параллельные реализации кэшей (static vs instance) | DRY | Низко-средняя | Открыто |

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

**Исправление.** Выделять инкрементально, по одной оси: сначала `ISqlDialect` (все
`Make*`/`Escape`/`Require*` плюс `MakeColumnExpression`), затем `IConnectionFactory`
(`CreateConnection`/`CreateParam`), затем вынести исполнение в `IQueryExecutor`.
`DbContext` становится тонким координатором. Пока F1 не закрыт, крупные оптимизации
горячего пути (см. `performance-findings.md`) остаются рискованными.

---

## F2. `IDataContext` — fat interface (ISP, Высокая)

**Доказательство.** `DataContext/IDataContext.cs` — 122 строки, ~60 членов. Смешаны:
управление соединением (`EnsureConnectionOpen`, `:57-58`), исполнение
(`ToList/First/Single` × sync/async, `:92-115`), создание энумераторов (`:82-91`),
фабрика команд (`CreateCommand`, `:25-34`), метаданные (`Create<T>`, `:15-24`), кэш
(`PurgeQueryCache`, `:120`), подготовка плана (`GetPreparedQueryCommand`, `:81`), плюс
~15 default-extension методов (`Any`, `GetAsyncEnumerable`, `From`).

`InMemoryContext` вынужден реализовывать весь контракт, включая no-op
`EnsureConnectionOpen() { }` (`InMemoryDataContext.cs:48`) — классический признак
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
   (`Cache/IPreparedQueryCommand.cs:34`); guard в контексте — лишь последняя линия обороны.

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

- база бросает: `CreateConnection` (`:138`), `GetTableName` (`:161`), `CreateParam`
  (`:488`), `MakePage` (`:501`), `EmptySorting` (`:497`), `MakeParam` (`:582`);
- `internal string GetColumnName(...)` — не virtual и **всегда** бросает (`:526-529`) —
  мёртвый контракт;
- `SqliteDbContext.MakeCount(distinct, big: true)` бросает `NotSupportedException`
  (`:88-93`), тогда как база (`:1315`) и SQL Server (`:141-148`) это поддерживают —
  подстановка подтипа ломает программу;
- `Projection<T1,T2>.Extend` работает, а `Projection<T1,T2,T3>.Extend` бросает
  (`Builders/Projection.cs:11-19` против `:27-30`) при одном контракте `IProjection`;
- `EntityP2.Join` бросает при `Condition is not null`
  (`Builders/Joins/JoinCommandBuilder.cs:15-16`), хотя базовый `Entity<TEntity>.Join` —
  нет.

**Первопричина.** Опциональные хуки с «throwing default»; подтипы усиливают
пред-условия / ослабляют пост-условия.

**Влияние.** Вызывающий не может полагаться на базовый контракт; ошибки всплывают в
рантайме на конкретном диалекте/арности.

**Исправление.** Обязательные хуки — `abstract` (проверка компилятором). Различия
возможностей выражать явно: `bool SupportsCountBig` / `TryMakeCount(...)` вместо
исключения. Для `IProjection` — либо реализовать `Extend` на всех арностях, либо не
выставлять его там, где он не поддержан. Удалить мёртвый `GetColumnName`.

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

**Исправление.** Ввести `ISqlDialect`/`IColumnMapper`, от которого зависят `SqlBuilder`
и визиторы (та же экстракция, что в F1). Передавать абстракцию, не `DbContext`.

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

**Доказательство.** `Visitors/BaseExpressionVisitor.cs` — 1035 строк,
`ExpressionVisitor, ICloneable, IDisposable`; `VisitMethodCall` содержит switch на ~20
имён `TableAlias.Long/Int/Boolean/.../get_Item` (`:106-137`) и 6 вхождений
`throw new NotImplementedException` (`:143,393,429,461,490,925`).

**Влияние.** Новый метод `TableAlias` требует правки визитора (OCP); класс совмещает
обход выражений, рендеринг SQL, разрешение алиасов и работу с параметрами.

**Исправление.** Разбить по конструкциям (резолвинг TableAlias, `NORM.Param`,
`NORM_SQL.exists/any/all`, числовые конверсии); switch по именам заменить таблицей
`Dictionary<string, Func<...>>` или полиморфизмом.

---

## F10. Дублирование агрегатов в `Entity<TEntity>` (DRY, Низкая)

**Доказательство.** `Builders/Entity.cs:396-523` — Min/Max/Avg/Sum/Stdev/Stdevp/Var/Varp,
каждый с sync + async + `*Core`. ~24 метода, отличаются только `MethodInfo`.
`First/FirstOrDefault/Single/SingleOrDefault` продублированы так же.

**Влияние.** Низкое, но «Rule of Three» превышен многократно; добавление перегрузки
`params ReadOnlySpan<object?>` повторяется 8 раз.

**Исправление.** Приватный
`AggregateCore<TResult>(Expression, MethodInfo, ReadOnlySpan<object?>)` плюс тонкие
публичные обёртки (или source generator).

---

## F11. Параллельные реализации кэшей (DRY, Низко-средняя)

**Доказательство.** `DataContextCache` — статические `_metadata/_selectListCache/_expCache`
(`:7-12`), а `InMemoryContext` держит **свои** инстансные
`_metadata/_selectListCache/_expCache` (`InMemoryDataContext.cs:17-22`). Аналогично
`MapperCache` (static, process-wide) и `[ThreadStatic] QueryPlanCache` (per-thread).
`ObjectPool<StringBuilder>` продублирован: `DbContext._sbPool` (`:38`) и
`SqlBuilder._sbPool` (`:12`).

**Влияние.** Два источника истины для одного понятия; семантика шаринга различается
(глобально vs per-instance/per-thread) — источник трудноуловимых багов кэша.

**Исправление.** Единая абстракция кэша, инжектируемая в контекст; явно
задокументировать область шаринга. Оставить один `_sbPool`.

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

## Приоритеты исправления

1. **P0** — F2 (ISP): ✅ **реализовано** — ролевые интерфейсы, `IDataContext` как пустой
   композит, вынос provider-независимых оверлоадов в `DataContextExtensions`.
2. **P1** — F1 (выделение `ISqlDialect`) и F6: снижают концентрацию риска на горячем
   пути; делать инкрементально.
3. **P1** — F5: устраняет уже существующее расхождение поведения (`MakeCount(big)`).
4. **P2** — F9, F11, F10. F7: пустой алиас удалён, ISP-часть `IQueryProvider` отложена по
   YAGNI (см. F7 — триггеры).
5. **Реализовано:** F4, F3, F2, F8 — см. «Статус реализации».

## Оговорка для performance-работ

`performance-findings.md` и `performance optimizations.md` описывают оптимизации
горячего пути. Перед рефакторингом F1/F6 зафиксируйте benchmark-базу
(`benchmark-report.md`); иначе есть реальный риск откатить уже сделанную работу.
Несколько текущих оптимизаций завязаны на конкретные внутренние типы (например, `ParamMap`,
`MapperCacheKey`, `IsRuntimeParam`) и должны быть сохранены при экстракции.

## Статус реализации

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
- **Нумерация строк** приведена к текущему рабочему дереву (после F4). Правка F4 сдвинула
  номера в `DbContext.cs` после ~1019 и в `InMemoryDataContext.cs` после ~636, поэтому
  ссылки в F1/F5 на диапазоны `DbContext` правее ~1019 могут отличаться на величину
  вырезанного блока.

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
    => dataContext.ToList(this, @params);   // Cache/IPreparedQueryCommand.cs:34
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
// DbContext.cs:41,46,384
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

> Обновление: F2 с тех пор реализован (см. «Статус реализации»), поэтому для варианта D
> остаётся только F6 (диалект-абстракция).

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
