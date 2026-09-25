# TODO: Интерфейсные сущности без классов реализации (runtime proxy, POCO)

> Рабочий план (design RFC). Источник: GitHub issue
> [#34 «POCO»](https://github.com/AlexeyShirshov/nextorm/issues/34), milestone `1.4-a.1`.
> «Emit class with interface implementation, create and return instance of it». Цель — позволить
> описывать сущность **только** интерфейсом и оперировать им: `ctx.From<IEntity>().ToList()` должен
> вернуть `IEntity[]`, где реализация сгенерирована на лету и закэширована в метаданных.

## Пункт и цель

- Фича: для сущности-интерфейса (и интерфейсного `TResult`) генерировать в рантайме класс-реализацию
  и материализовать его экземпляры; прокси-тип кэшируется process-wide рядом с метаданными.
- Критерий приёмки: `From<IEntity>()` без реализации и без явного `Select` возвращает `IEntity[]`
  через `ToList`/`First*`/`ToAsyncEnumerable`; атрибуты на интерфейсе (`[SqlTable]`/`[Key]`/
  `[Column]`/`[DatabaseGenerated]`), включая **унаследованные** интерфейсы, учитываются; прокси-тип
  создаётся один раз на интерфейс; in-memory и DB-провайдеры дают одинаковый результат.
- **Не путать** с потоковой выдачей JSON (`todo_json_streaming.md`): там прокси не нужен, JSON
  пишется прямо из ридера.

## Почему это нужно (мотивация)

1. **Меньше boilerplate.** Сейчас на каждую сущность нужен класс-реализация (`SimpleEntity :
   ISimpleEntity`, `Entities.cs:15`); интерфейс уже несёт разметку, класс — чистый дубль ради ctor.
2. **Интерфейс как контракт.** DDD-подход: наружу отдаётся `IOrder`, реализация — деталь хранения.
3. **Уже почти готово.** Атрибуты с интерфейса читаются (`EntityMetadataBuilder.FindInterfaceProperty:84`),
   `IsInterface` учитывается в SQL-планировании (`QueryPlanner.cs:432`, `SqlMutationBuilder.cs:775`).
   Не хватает ровно одного шага — материализации.

## Текущее состояние и разрыв

| Слой | Где | Чего не хватает |
|---|---|---|
| Метаданные | `Meta/EntityMetadataBuilder.cs:46-82` | свойства собираются, но `FlattenHierarchy` не разворачивает интерфейсную иерархию; фильтр `CanWrite` отсекает get-only |
| Атрибуты | `EntityMetadataBuilder.cs:61-64,84,155-174` | `[Column]`/`[Key]`/`[DatabaseGenerated]`/`[SqlTable]`/`[Table]` с интерфейса уже читаются |
| Источник | `QueryPlanner.cs:432` | `From<T>()` уже помечает `FromExpression.SourceIsInterface` (`FromExpression.cs:74`) для naming convention |
| Материализация | `RowMaterializerBuilder.cs:21-54` | `resultType.GetConstructors()` (`:28`) у интерфейса пуст → `QueryPreparationException`; `Expression.New(interface)` невозможен |
| Returning | `DataContext.cs:463-470` | `EnsureReturningMaterializable` явно запрещает interface/abstract |
| Кэш | `DataContextCache.cs:22`, `MapperCache.cs:26` | кэша прокси-типов нет |
| Публичный вход | `DataContextExtensions.cs:665`, `:114` | `From<T>`/`ResolveMetadata` кладут только `IEntityMetadata` |
| Прочие пути | `InMemoryRowMaterializer.cs:51,73`, `RowMapperFactory.cs:120-159` | оба идут через `RowMaterializerBuilder` — одна точка правки |
| Source-gen | `src/nextorm.core.sourcegenerator` | пустой `IIncrementalGenerator`, не подключён ни к одному проекту |

## Проверенные факты (прототип Reflection.Emit + Expression)

1. **`Type.GetProperties(FlattenHierarchy)` НЕ разворачивает интерфейсы.** Для `IEntity : IBase`
   вернул только собственные свойства `IEntity`; `IBase.Id` доступен лишь через `GetInterfaces()`.
   Значит `AutoBuildProperties` сегодня **потерял бы** унаследованные свойства интерфейса.
2. **`Expression.Bind(интерфейсное_свойство, value)` поверх `Expression.New(proxy)` работает**, если
   у свойства есть setter (включая унаследованное). Для **get-only** свойства —
   `ArgumentException: The property ... has no 'set' accessor` → надо биндить `PropertyInfo` прокси.
3. Прокси эмитится как `public sealed`, реализует всё замыкание интерфейсов; **get-only свойство
   реализуется свойством с добавленным setter** — тогда его можно заполнить. Дубли имён
   дедуплицируются.
4. `Expression.MemberInit(Expression.New(proxy), …)` компилируется в `Func<…, IEntity>` без проблем
   (proxy assignable к интерфейсу).
5. Ограничения, подтверждённые ошибками:
   - **непубличный интерфейс в чужой сборке** → `TypeLoadException: attempting to implement an
     inaccessible interface` (для internal нужен `IgnoresAccessChecksTo` или source-gen);
   - **NativeAOT**: `Reflection.Emit`/`Expression.Compile` недоступны — регрессии нет, библиотека и
     так на `Expression.Compile()`, но под AOT остаётся только source-gen;
   - два интерфейса с одноимённым свойством **разных типов** — implicit implementation конфликтует.

## Дизайн

### 1. Обнаружение свойств — `EntityMetadataBuilder.AutoBuildProperties`

- для `typeof(T).IsInterface` обходить **всё замыкание** `new[] { T }.Concat(T.GetInterfaces())`,
  дедуп по имени (первое объявление выигрывает);
- для интерфейсных свойств отбирать по `CanRead`, а не `CanWrite` (get-only заполнит прокси);
  исключать индексаторы (`GetIndexParameters().Length > 0`) и события;
- унаследованные `[Column]`/`[Key]`/`[DatabaseGenerated]` уже подтягиваются `FindInterfaceProperty`.

### 2. `InterfaceProxyFactory` (internal)

- один динамический assembly `NextORM.DynamicEntities` (`AssemblyBuilder.DefineDynamicAssembly`,
  `AssemblyBuilderAccess.Run`), один `TypeBuilder` на интерфейс;
- backing field + свойство (getter/setter всегда) для каждого свойства замыкания; публичный
  `sealed` класс, дефолтный ctor;
- кэш `ConcurrentDictionary<Type, Type>` со значениями `Lazy<Type>` (эмит ровно один раз,
  process-wide); кладётся рядом с `DataContextCache.Metadata` (`DataContextCache.cs:22`);
- защита: если интерфейс не public — понятное исключение с указанием на ограничение (фаза 3 —
  `IgnoresAccessChecksTo`/source-gen).

### 3. Подключение к материализации

- `RowMaterializerBuilder.Build` (`:21`) получает **`materializationType`** отдельно от `resultType`
  (тип лямбды остаётся интерфейсом):
  - `GetConstructors()`/`Expression.New` — по прокси (parameterless → member-init путь, `:45-53`);
  - bind-члены резолвить **на прокси** (`proxyType.GetProperty(name)`), а не на интерфейсе — снимает
    разом get-only и унаследованные свойства;
- `RowMapperFactory.Build:146` и `InMemoryRowMaterializer.cs:51,73` — прокинуть прокси (в in-memory
  тот же builder, правка одна);
- `DataContext.EnsureReturningMaterializable:463` — разрешить интерфейсы, оставить запрет только для
  **abstract-классов** (их проксировать нельзя).

### 4. Метаданные и API

- `IEntityMetadata` — `Type? MaterializationType { get; }` с default-реализацией (как
  `IsTableNameAuto:27`), чтобы не ломать внешних имплементаторов; заполняется для интерфейсов
  прокси-типом (issue просит «закешировать в метаданные»);
- `IPropertyMetadata.PropertyInfo` остаётся интерфейсным — чтение/запись значений у прокси идут через
  интерфейсную диспетчеризацию;
- опционально `ctx.New<IEntity>()`/`ctx.Create<IEntity>()` — пустой экземпляр прокси.

### 5. Фазы

- **Фаза 1:** `From<IEntity>()` (без `Select`) и `Select(x => x)` → `ToList`/`First*`/
  `ToAsyncEnumerable`; плоские свойства и наследование интерфейсов; публичные интерфейсы.
- **Фаза 2:** `Returning<IEntity>()`, интерфейс как элемент `Projection<T1,T2>`, вложенные
  интерфейсы.
- **Фаза 3:** source generator (`src/nextorm.core.sourcegenerator`) для AOT и internal-интерфейсов;
  runtime-emit остаётся общим путём; `IgnoresAccessChecksTo`.

## Матрица сценариев

| Сценарий | Сейчас | После фазы 1 |
|---|---|---|
| `From<IEntity>().Select(x => new { … })` | работает (anon) | без изменений |
| `From<IEntity>().ToList()` | `QueryPreparationException` | `IEntity[]` через прокси |
| `From<IEntity>().Where(…).First()` | ошибка | прокси |
| `From<IEntity>()` (in-memory) | ошибка | прокси (тот же `RowMaterializerBuilder`) |
| `Returning<IEntity>()` | `NotSupportedException` (`DataContext.cs:469`) | фаза 2 |
| `Projection<T1,T2>` с интерфейсом | ошибка | фаза 2 |
| abstract-класс как `TResult` | ошибка | остаётся ошибкой |

## Ограничения и цена

- **Только интерфейсы.** Abstract-классы проксировать нельзя (неизвестны абстрактные члены) — остаётся
  `NotSupportedException`.
- **Доступность интерфейса.** `public` — ок; `internal` в чужой сборке — фаза 3 (source-gen/
  `IgnoresAccessChecksTo`).
- **Конфликты членов.** Одноимённые свойства с разным типом у нескольких базовых интерфейсов — явное
  исключение с понятным сообщением (implicit implementation невозможен).
- **NativeAOT/trimming.** `Reflection.Emit` недоступен; под AOT — только source-gen (фаза 3).
- **Прокси-тип виден в рантайме** (dynamic assembly) — не сериализуется иначе, чем через интерфейс;
  зафиксировать в доке.
- **Публичный API** (`IEntityMetadata.MaterializationType`, `ctx.New<T>`) — обновить
  `docs/specs/design/API-NAMING-REVIEW.md`; при заморозке — `PublicAPI.*`.
- **Производительность:** прокси компилируется один раз; на строку — тот же `Expression.Compile`-маппер,
  что и для класса (регрессии нет).

## Этапы внедрения

- **Фаза 1 (MVP):** обход замыкания интерфейсов в `AutoBuildProperties`; `InterfaceProxyFactory`;
  `materializationType` в `RowMaterializerBuilder` + прокидка в `RowMapperFactory` и
  `InMemoryRowMaterializer`; снятие запрета в `EnsureReturningMaterializable` для интерфейсов;
  `IEntityMetadata.MaterializationType`; тесты; доки EN+RU.
- **Фаза 2:** `Returning`, `Projection<T1,T2>`, вложенные интерфейсы.
- **Фаза 3:** source generator + `IgnoresAccessChecksTo` (AOT, internal).
- **Вне области:** проксирование abstract-классов, генерация реализации для `InsertInto`-инициализаторов.

## План тестов

- Unit (`tests/nextorm.core.tests`): метаданные унаследованного интерфейса; get-only свойство;
  прокси-тип один на интерфейс (кэш); конфликт типов бросает; `Expression.Bind` через прокси;
  `AutoBuildProperties` не теряет `IBase`-свойства.
- SQL-gen/провайдерные: `From<IInterface>()` генерирует тот же SQL, что и класс-реализация.
- Интеграция (`tests/nextorm.integration.tests/CommonTestSuite`): round-trip
  `From<IComplexEntity>().ToList()` на SQLite/PG/SQLServer/MySQL/ClickHouse; равенство значений
  прокси и POCO; in-memory.
- Публичный API: `IEntityMetadata.MaterializationType` присутствует; покрытие не ниже базы.

## Открытые вопросы

1. Кэш прокси — свойство `IEntityMetadata.MaterializationType` (публичный API) vs скрытый
   `DataContextCache`-словарь. Issue просит «в метаданные» — склоняемся к свойству.
2. Фаза 1 только `public` интерфейсы или сразу `IgnoresAccessChecksTo` для `internal`.
3. Форма прокси: backing fields + свойства vs `Dictionary`-хранение (стек-путь выбран ради
   производительности и сериализации).
4. Нужен ли `ctx.New<IEntity>()`/`ctx.Create<IEntity>()` и раскрывать ли `MaterializationType`
   наружу.
5. Имена: `InterfaceProxyFactory`/`MaterializationType` vs `ProxyType`/`DynamicEntityFactory`.
6. Как сочетать с `INamingConvention` для имени прокси-типа (влияет ли вообще).

## Файлы к изменению

- Новое: `src/nextorm.core/DataContext/Meta/InterfaceProxyFactory.cs`,
  тесты `tests/nextorm.core.tests/InterfaceMaterializationTests.cs`,
  `tests/nextorm.integration.tests/CommonTestSuite.InterfaceEntities.cs`.
- Правки: `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs` (обход интерфейсов),
  `DataContext/Meta/IEntityMetadata.cs` + `Meta/Implementation/EntityMetadata.cs`
  (`MaterializationType`), `DataContext/RowMaterializerBuilder.cs`,
  `DataContext/RowMapperFactory.cs`, `DataContext/InMemoryRowMaterializer.cs`,
  `DataContext/DataContext.cs` (`EnsureReturningMaterializable`), `DataContext/DataContextCache.cs`,
  опционально `DataContext/DataContextExtensions.cs` (`New<T>`).
- Документация: `docs/guide/` (раздел об интерфейсных сущностях, +RU),
  `docs/advanced/api-reference.md` (+RU), `docs/advanced/limitations.md` (снять «project the
  columns», +RU), `docs/specs/design/API-NAMING-REVIEW.md`.

## Дизайн-ревью (nextorm-design-engineer, 2026-09-24)

> Прогон сабагента `nextorm-design-engineer` по плану (read-only). `file:line` — по дереву на момент ревью.
> Вердикт: **дизайн-здоров, блокеров нет**; 3 🟡 закрыть в тексте плана до реализации.

- **[TYPE]/[LSP] 🟡** Публичный `IEntityMetadata.MaterializationType` (`:96-103,165-166`) протекает динамическим прокси-типом: в фазе 3 (source-gen) он сменится, а пользователь может начать на него опираться — leaky contract и помеха заморозке (`todo_public_api_freeze.md`). Fix: держать прокси в `DataContextCache`, публично не раскрывать тип (или `internal` + документированная нестабильность).
- **[DRY] 🟡** Объявление кэша самопротиворечиво (`:78-80`: `ConcurrentDictionary<Type, Type>` со `Lazy<Type>`, «рядом с `DataContextCache.Metadata`» без нового поля — `DataContextCache.cs:22,30` это `IDictionary<Type, IEntityMetadata>`). Fix: отдельный `static ConcurrentDictionary<Type, Lazy<Type>>` с явным именем владельца.
- **[OCP]/[TYPE] 🟡** Bind-члены «по имени на прокси» (`:89` `proxyType.GetProperty(name)`) повторяет reflection-by-name (`RowMaterializerBuilder.cs:49`); при замыкании интерфейсов/explicit-implementation возможен неверный член. Fix: один раз построить `name → PropertyInfo` карту по прокси-типу (или interface map), не звать `GetProperty` в цикле материализации.
- **[OCP] ℹ️** Две стратегии сбора свойств в одном методе (`:65-71` добавляет обход замыкания интерфейсов рядом с классовым `Where(CanWrite)` `EntityMetadataBuilder.cs:52`). Deferred с триггером (третье правило формы); до этого вынести интерфейсный обход в приватный хелпер.
- **ℹ️** Позитив: один `Expression.Compile` на тип, per-row регресса нет (`:139-140`); `AutoBuildProperties` исключает индексаторы/события (`:69-70`) — верно.
