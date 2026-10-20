# Составные ключи связей (#251): проект спецификации

> **Статус: APPROVED — письменная спецификация одобрена пользователем в чате 2026-10-17.**
>
> Документ фиксирует концептуальный объём, согласованный с пользователем в обсуждении, и отдельно
> помеченные рекомендации архитектора. Одобрение 2026-10-17 относится к written shared scope:
> **#251 + только новый metadata-driven `LoadWith` subset #249**; оно **не** одобряет остальной
> standalone collection projection/`AsEntityBuilder` DRAFT #249 и **не** является планом реализации.
> **Builder check завершён.** Следующий артефакт — только общий план реализации; в этой сессии
> планирование не начато, потому что skill `writing-plans` недоступен (экспонирован только
> `brainstorming`), и оно не подменяется реализацией. Плана реализации нет; разрешения на
> реализацию нет.
>
> **Три разных состояния, которые нельзя смешивать:** одобренная письменная спецификация ≠ план
> реализации ≠ разрешение на реализацию. Одобрением 2026-10-17 выдано только первое. Исторические
> формулировки ниже («ожидает review», «reviewer не одобрял») сохранены как история до этой даты.

| Поле | Значение |
|---|---|
| Задача | [#251](https://github.com/AlexeyShirshov/nextorm/issues/251) — составные ключи связей |
| Связанная задача | [#249](https://github.com/AlexeyShirshov/nextorm/issues/249) — только необходимый новый metadata-driven `LoadWith` subset в общем плане; остальной #249 — отдельный DRAFT |
| Milestone | `1.0.9-rc3`, обе задачи **OPEN** |
| Стадия | APPROVED written spec (пользователь, в чате 2026-10-17); builder check completed; ожидает только общий план |
| Группировка плана | user-approved (в чате 2026-10-17): общий будущий план #251 + new `LoadWith` subset #249, не весь #249 и не разрешение на реализацию |
| Одобрение | 2026-10-17, в чате (ответ «да» на вопрос об одобрении письменной спеки); отдельного GitHub-approval/comment/URL нет |
| План реализации | нет; не создан — планирование не начато, `writing-plans` недоступен в этой сессии |
| Приёмка новых возможностей | только SQL-провайдеры; new `LoadWith` subset; in-memory не расширяется |
| Разрешение на реализацию | отсутствует |

## 0. Как читать документ

- **«Согласовано пользователем»** — концептуальное решение, зафиксированное в обсуждении. С
  2026-10-17 полная формулировка этого документа одобрена пользователем в чате; пометка описывает
  происхождение решения, а не отсутствие одобрения.
- **«User-approved grouping»** — пользователь согласился сгруппировать будущую работу в один план
  #251 + необходимый новый `LoadWith` subset #249; 2026-10-17 он одобрил и письменную спеку этого
  shared scope. Это не одобрение остального #249 и не разрешение на реализацию.
- **«ARCHITECT CALL»** — рекомендация архитектора (primary), а не отдельно выбранная пользователем
  деталь. В составе одобренной 2026-10-17 письменной спеки она входит в утверждённый written target;
  API stop & focused spec amendment при противоречии real-assembly evidence продолжает применяться.
- **«Builder freedom»** — внутренняя свобода реализации, не меняющая наблюдаемый контракт.

## 1. Контекст и цель

### 1.1 Что уже есть

- `IRelationshipMetadata.ForeignKey`/`PrincipalKey` и `RelationshipJunctionMetadata` уже предоставляют
  **упорядоченные списки** компонентов, а `JoinInto` умеет соединять и группировать по каждому
  объявленному компоненту, если такие метаданные предоставлены вручную (#213/#223).
- Обычные fluent-объявления и `[Relationship]` при этом принимают **один** ключевой столбец:
  `ResolveSelectedProperty` возвращает один `PropertyInfo`, а `RelationshipDeclaration` хранит
  одиночные `ForeignKey`/`PrincipalKey`.
- `RelationshipResolver.ResolveKey` для default principal возвращает **первый** `IsKey`, а не весь
  составной PK.
- `NavigationPathResolver.RequireSingleKey` явно отклоняет составной ключ в неявной навигации.
- В `EntityBuilder<TEntity>` на строке 642 находится **ровно один legacy explicit-key
  `LoadWith` overload**; новых metadata-driven entrypoint-ов (short form, child-factory form) ещё
  нет. Поэтому общий план должен включать их реализацию, а не только расширение уже отгруженного
  API.

Иными словами, «составные ключи уже поддержаны» верно только для низкоуровневого пути с
инъекцией `IRelationshipMetadata`. Естественно объявить и использовать составную связь обычным
пользовательским способом сегодня нельзя.

### 1.2 Цель #251

Сделать составные связи естественно объявляемыми и пригодными к использованию без инъекции
`IRelationshipMetadata`:

- снять ограничение «один ключевой столбец» в объявлениях и в потреблении SQL;
- не вводить произвольный предел арности 2 или 3 — поддерживать 1, 2, 3, 4+ компонента;
- провести один и тот же упорядоченный контракт через `JoinInto`, **необходимый новый
  metadata-driven `LoadWith` subset #249** (short/factory, split/single, SQL) и неявную навигацию
  (reference access + существующие collection predicates);
- новые возможности принимаются только на SQL-провайдерах. Объявления и метаданные при этом
  provider-neutral: in-memory не получает новых возможностей, но и не получает artificial rejection
  на этапе metadata resolution.

## 2. Согласованные пользователем решения

Хронологическая сводка; поздние уточнения имеют приоритет над ранними.

1. **Все четыре кардинальности во fluent:** 1:N, N:1, 1:1 и M:N через явный junction.
2. **Два носителя объявления:** fluent и атрибуты. Атрибуты остаются только для существующих
   кардинальностей 1:N и N:1; 1:1 и M:N объявляются только fluent.
3. **Внешний ключ задаётся явно.** Default principal — **весь** PK, упорядоченный как метаданные.
   Дополнительно доступен явный selector principal-ключа (EF-style override). Сопоставление
   **позиционное**; имена компонентов с обеих сторон не обязаны совпадать. Сравниваются
   нижележащие mapped-типы после снятия nullable-обёрток, а не типы анонимных wrapper-классов.
4. **User-approved grouping будущего плана:** один общий будущий план реализации для #251 плюс
   **только необходимый новый metadata-driven `LoadWith` subset #249**:
   - short form и child-factory form;
   - все 4 кардинальности, split и single;
   - простые child queries и projection child queries там, где новый `LoadWith` их поддерживает;
   - filters/order/paging/hidden correlation; SQL only;
   - composite implicit-navigation expansion для существующих reference/collection predicates,
     SQL pipeline only.

   Будущий план включает реализацию **новых** `LoadWith` entrypoint-ов, а не только расширение якобы
   уже отгруженного API: scout evidence — `EntityBuilder.cs:642` содержит ровно один legacy
   explicit-key overload, новых metadata-driven overload-ов нет. Legacy explicit-key `LoadWith`
   остаётся без изменений.

   - **#249 rest** — новые implicit collection-valued `Select` slots, `AsEntityBuilder` adapter и
     их независимые terminals/matrix — остаётся отдельно scoped DRAFT и **не** добавляется как
     implementation work или AC blocker #251.
   - Общий composite contract применяется к этим будущим metadata consumers, когда их реализуют в
     #249; но их реализация **не** требуется для accept/close #251.
   - Projection существующего child **query** внутри нового `LoadWith` входит в scope; это не
     standalone collection projection syntax.
   - #249 в целом этим планом не auto-approved и не закрывается.
   - Ответ «да» — согласие на эту группировку; одобрение пользователем 2026-10-17 в чате относится
     к письменной спеке ровно этого shared scope (#251 + new `LoadWith` subset), не к остальному
     #249 и не к реализации.
5. **Семантика null:** любой null-компонент FK означает **отсутствие ассоциации**. Для eager loading
   это null-reference/пустая коллекция; «висячий» non-null FK также не находит related row.
   Null-safe соединения `null == null` нет.
6. **Последнее ограничение по in-memory (имеет приоритет; architect clarification):**
   пользователь явно сказал «inmemory не поддерживаем». Это отменяет более раннее обсуждение
   all-provider parity/null-поведения.
   - Объявления и метаданные **provider-neutral**: нет in-memory-specific rejection на этапе
     metadata resolution и нет provenance flag «new declaration vs old injection».
   - Существующие in-memory consumers не модифицируются. Если они уже обрабатывают list metadata
     из старой ручной регистрации, они могут естественно потребить те же wellformed metadata из
     новых объявлений. Это **не** support guarantee, **не** новые parity-тесты и **не** deliberate
     regression по признаку происхождения metadata.
   - Старые preexisting memory `null == null` tests/behavior и существующие рабочие memory paths
     сохраняются.
   - Новые metadata-driven `LoadWith` entrypoints — SQL-only: при вызове с in-memory context они
     бросают `NotSupportedException` на query construction/provider capability validation **до
     исполнения**; новая memory implementation не вводится.
   - Это не вводит blanket fail-closed для старых memory routes.
   - Новая implicit-navigation expansion — SQL pipeline only; memory features ради упражнения новых
     объявлений не добавляются.
7. **Совместимость escape hatch:** публичный `DataContextCache.Metadata` и возможность вручную
   предоставить `IRelationshipMetadata` остаются совместимыми, включая valid manual mappings;
   malformed custom metadata валидируется по §9.4. Обычным объявлениям этот путь больше не нужен.

## 3. Границы #251 и #249

| Область | #251 | #249 |
|---|---|---|
| Объявление связи | составные fluent-формы, атрибутные массивы имён, общий component parser | не владеет |
| Метаданные | упорядоченные списки FK/PK, junction-списки, inverse, структурная валидация | потребляет общий контракт |
| SQL-семантика ключа | упорядоченное равенство по каждой паре, null-компонент → нет ассоциации | потребляет, не переопределяет |
| Неявная навигация | снятие composite-блокера (`RequireSingleKey`) + existing reference/collection predicates | не владеет |
| Новый metadata-driven `LoadWith` subset | общий composite contract; ownership публичных форм — #249 | short form, child-factory form, split/single, child query/projection внутри них |
| #249 rest: implicit collection-valued `Select`, `AsEntityBuilder` adapter, независимые terminals/matrix | не входит в implementation work и AC blockers #251 | отдельный DRAFT; не auto-approved общим планом |
| In-memory | новых возможностей не добавляет; existing consumers не трогает | новых возможностей не добавляет; metadata не получает provenance rejection |

**User-approved grouping будущего плана.** Один общий будущий план реализации покрывает #251 плюс
**только необходимый новый metadata-driven `LoadWith` subset #249**. Это не одобрение всего #249,
не auto-approval и не закрытие #249.

**Граница приёмки.** Финальная функциональная приёмка #251 требует, чтобы **новый `LoadWith`
subset #249** принимал составные ключи, а не чтобы все metadata-потребители #249 это делали.
Существующая неявная навигация (reference access и существующие collection predicates) входит в
scope; новые projection/Select-формы #249 — нет. Composite contract применяется к будущим
metadata consumers, когда их реализуют в #249, но их реализация не требуется для accept/close #251.

**Что не делает #251.** Не дублирует и не реализует не относящиеся к составным ключам части #249 и
не реализует их in-memory режим. Существующие правила #249 (кардинальности, отсутствие N+1,
фильтры/пагинация/схема/кэш/терминалы) сохраняются; расширение идёт только single → composite.
Legacy explicit-key `LoadWith` не меняется, и новые composite-возможности для legacy selector route
не обещаются. Новые `LoadWith` entrypoint-ы входят в общий будущий план, но их legacy-форма остаётся
ровно такой, как сейчас.

**Координация.** Финальная работа требует согласованного плана #251 + new `LoadWith` subset. Одобрение
письменной спеки (2026-10-17, в чате) разрешает следующий артефакт — **только общий план**
реализации; порядок исполнения и реализация этим не авторизуются.

## 4. Не-цели

- Генерация схемы/DDL/миграций, создание FK-constraints, discovery уникальности.
- Угадывание FK по конвенции.
- Новый change tracking/fixup.
- Импорт составных связей из EF bridge.
- Составной `ReturningKey`.
- Новый публичный API порядка PK (`IsKey(...).Order(...)` и т.п.).
- Новая атрибутная синтаксическая форма для many-to-many или one-to-one.
- Произвольный язык выражений/кортежей для ключей.
- Новая поддержка in-memory провайдера.
- In-memory-specific rejection на этапе metadata resolution и provenance flag «new declaration vs old
  injection».
- Реализация #249 rest: новых implicit collection-valued `Select` slots, `AsEntityBuilder` adapter и
  их независимых terminals/matrix.
- Новая standalone collection projection syntax (projection child **query** внутри нового
  `LoadWith` — это другой случай и входит в scope).

Ограничения провайдерных возможностей, не относящиеся к составным ключам, сохраняются. Но **сам
факт составного ключа не может быть общим основанием «unsupported»** ни на одном поддерживаемом
SQL-маршруте нового metadata-driven `LoadWith` subset.

## 5. Principal-ключ и порядок компонентов (ARCHITECT CALL)

Это рекомендация архитектора, а не выбранная пользователем деталь. Пользователь согласовал саму
концепцию default full PK; источник порядка и отсутствие нового API — architect call.

- Default principal = `metadata.Properties`, отфильтрованные по `IsKey`, **с сохранением порядка
  списка**. Это тот же порядок, которым уже пользуются identity/DML (`JoinIntoSpec`).
  Алфавитная сортировка и сопоставление по имени члена не применяются.
- `IEntityMetadata.Properties` документирует порядок declared/discovered. Для fluent порядок следует
  порядку вызовов `Property`; для auto — порядок reflection-метаданных. Порядок иерархии типов не
  становится новым универсальным обещанием «порядка объявления в исходнике».
- Новый `IsKey` Order API в #251 не добавляется. Документация обязана показывать порядок явно;
  если нужен другой порядок — используется явный selector.
- Один FK при default multi-column PK — это **arity error**, а не молчаливое взятие первого ключа.
- Явный principal override может указывать на mapped non-PK свойства/альтернативный logical key
  (сохраняем текущую возможность), не обязан состоять только из `IsKey` или покрывать весь PK.
  Проверки уникальности в БД нет.
- Составной явный principal может переупорядочивать выбранные компоненты PK. Частичного
  «случайного default» нет: если selector задан, он задаёт весь principal-ключ.
- Равенство арности строгое для всех ног связи: число FK-компонентов = числу principal-компонентов;
  для M:N отдельно согласуются обе пары (parent ↔ junction parent, child ↔ junction child).

## 6. Язык selector-выражений

Selector — это либо:

- прямое скалярное mapped-свойство: `p => p.ParentId`;
- плоский анонимный объект: `p => new { p.ParentTenantId, p.ParentId }`.

Правила:

- Порядок компонентов — **ровно порядок аргументов** `new { ... }`. Алиасы компонентов игнорируются.
- Разрешено только прямое свойство от корневого параметра; унаследованные rooted-свойства
  разрешены.
- Отклоняются: поля, вложенные пути членов (`p => p.Nav.Id`), вычисления, вызовы методов,
  `new[] { ... }`, tuple-литералы, tuple-конструкторы, DTO-конструкторы, вложенные анонимные
  объекты, пустой список, дубликаты разрешённых свойств.
- Boxing и nullable-обёртки при извлечении разрешены. Совместимость сравнивается по нижележащим
  mapped-типам; **не** используются equality анонимного агрегата, имена/типы C#-wrapper-классов или
  SQL row-value tuples.
- **ARCHITECT CALL:** числовые/вычисляемые cast-селекторы не поддерживаются. Это осознанное
  ужесточение валидации, а не binary change; деталь не выбиралась пользователем.
- Искусственного предела арности нет: тестируются 1, 2, 3, 4+ компонента.

Атрибутные массивы (`string[]`) — отдельный синтаксис имён и под правила селекторов не попадают.

## 7. Публичный fluent API

### 7.1 Существующие шесть сигнатур

Все шесть существующих generic-сигнатур сохраняются **без изменения** (возвращают
`EntityMetadataBuilder<T>`); меняется только внутренний разбор на общий component parser. Одиночные
скалярные вызовы продолжают работать.

```csharp
public EntityMetadataBuilder<T> HasMany<TChild, TKey>(
    Expression<Func<T, ICollection<TChild>>> navigation,
    Expression<Func<TChild, TKey>> foreignKey) where TChild : class;

public EntityMetadataBuilder<T> HasMany<TChild, TKey>(
    Expression<Func<TChild, TKey>> foreignKey,
    Expression<Func<T, TKey>> principalKey) where TChild : class;

public EntityMetadataBuilder<T> HasOne<TChild, TKey>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, TKey>> foreignKey) where TChild : class;

public EntityMetadataBuilder<T> HasOne<TChild, TKey>(
    Expression<Func<T, TKey>> foreignKey,
    Expression<Func<TChild, TKey>> principalKey) where TChild : class;

public EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childForeignKey) where TChild : class;

public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction, TParentKey, TChildKey>(
    Expression<Func<T, IEnumerable<TChild>>> navigation,
    Expression<Func<T, TParentKey>> parentKey,
    Expression<Func<TJunction, TParentKey>> junctionParentForeignKey,
    Expression<Func<TChild, TChildKey>> childKey,
    Expression<Func<TJunction, TChildKey>> junctionChildForeignKey)
    where TChild : class
    where TJunction : class;
```

### 7.2 Новые десять overload-ов

Добавляются ровно десять compile-tested overload-ов в `public class EntityMetadataBuilder<T>`. Все
возвращают `EntityMetadataBuilder<T>`; `TChild : class`, `TJunction : class` — где применимо.
`IsKey`-overload-ов не добавляется.

```csharp
// A. 1:N, navigation + explicit FK + explicit principal.
public EntityMetadataBuilder<T> HasMany<TChild>(
    Expression<Func<T, ICollection<TChild>>> navigation,
    Expression<Func<TChild, object?>> foreignKey,
    Expression<Func<T, object?>> principalKey) where TChild : class;

// B. 1:N, no navigation, explicit FK + explicit principal.
public EntityMetadataBuilder<T> HasMany<TChild>(
    Expression<Func<TChild, object?>> foreignKey,
    Expression<Func<T, object?>> principalKey) where TChild : class;

// C. N:1, navigation + explicit FK + explicit principal.
public EntityMetadataBuilder<T> HasOne<TChild>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, object?>> foreignKey,
    Expression<Func<TChild, object?>> principalKey) where TChild : class;

// D. N:1, no navigation, explicit FK + explicit principal.
public EntityMetadataBuilder<T> HasOne<TChild>(
    Expression<Func<T, object?>> foreignKey,
    Expression<Func<TChild, object?>> principalKey) where TChild : class;

// E. 1:1, navigation + explicit parent key + explicit child FK.
public EntityMetadataBuilder<T> HasOneToOne<TChild>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<T, object?>> parentKey,
    Expression<Func<TChild, object?>> childForeignKey) where TChild : class;

// F. M:N, navigation + explicit parent key, junction parent FK, child key, junction child FK.
public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(
    Expression<Func<T, IEnumerable<TChild>>> navigation,
    Expression<Func<T, object?>> parentKey,
    Expression<Func<TJunction, object?>> junctionParentForeignKey,
    Expression<Func<TChild, object?>> childKey,
    Expression<Func<TJunction, object?>> junctionChildForeignKey)
    where TChild : class
    where TJunction : class;

// G. 1:N, no navigation, FK only; principal = full PK of T.
public EntityMetadataBuilder<T> HasMany<TChild>(
    Expression<Func<TChild, object?>> foreignKey) where TChild : class;

// H. N:1, no navigation, FK only; principal = full PK of TChild.
public EntityMetadataBuilder<T> HasOne<TChild>(
    Expression<Func<T, object?>> foreignKey) where TChild : class;

// I. 1:1, navigation + child FK only; parent key = full PK of T.
public EntityMetadataBuilder<T> HasOneToOne<TChild, TKey>(
    Expression<Func<T, TChild?>> navigation,
    Expression<Func<TChild, TKey>> childForeignKey) where TChild : class;

// J. M:N, navigation + junction FKs only; both entity keys = full PKs.
public EntityMetadataBuilder<T> HasManyThrough<TChild, TJunction>(
    Expression<Func<T, IEnumerable<TChild>>> navigation,
    Expression<Func<TJunction, object?>> junctionParentForeignKey,
    Expression<Func<TJunction, object?>> junctionChildForeignKey)
    where TChild : class
    where TJunction : class;
```

A–F — формы с явным principal; G–J — короткие формы с default full PK. Они дополняют старые
`HasMany(nav, fk)`/`HasOne(nav, fk)`, а не заменяют их. В J выводятся оба PK; если нужно
кастомизировать хотя бы один, используется F с явными всеми четырьмя ключами — отдельный
переупорядоченный overload не добавляется.

### 7.3 Вывод generic-параметров

- A, C, E, I выводят `TChild` из navigation. I выводит `TKey` из child FK, поэтому составной
  анонимный ключ работает без явного типа.
- B, D, G, H — key-only формы: из нетипизированной лямбды `TChild` не вывести, поэтому обычный
  вызов требует явного `<TChild>`; типизированная переменная selector-а может дать вывод, но
  публичный пример и тесты используют явный аргумент.
- F и J требуют явных `<TChild, TJunction>`: `TJunction` встречается только в нетипизированных
  лямбдах, а C# не позволяет задать часть generic-аргументов. Типизированная переменная
  `Expression<Func<TJunction, object?>>` может дать вывод, но обычный вызов пишется с двумя
  аргументами типа.
- Явные старые generic-вызовы и их binary-сигнатуры сохраняются.

### 7.4 Эскизы вызовов

```csharp
var blog = new EntityMetadataBuilder<Blog>();   // Blog: TenantId, Id
var post = new EntityMetadataBuilder<Post>();   // Post: ParentTenantId, ParentId

// 1:N — default full PK (старый 2-param overload).
blog.HasMany(b => b.Posts, p => new { p.ParentTenantId, p.ParentId });

// 1:N — явный principal.
blog.HasMany(
    b => b.Posts,
    p => new { p.ParentTenantId, p.ParentId },
    b => new { b.TenantId, b.Id });

// 1:N без navigation — явный principal.
blog.HasMany<Post>(
    p => new { p.ParentTenantId, p.ParentId },
    b => new { b.TenantId, b.Id });

// 1:N без navigation — default full PK.
blog.HasMany<Post>(p => new { p.NullableTenantId, p.NullableParentId });

// N:1 — default full PK (старый 2-param overload).
post.HasOne(p => p.Parent, p => new { p.NullableTenantId, p.NullableParentId });

// N:1 — явный principal.
post.HasOne(
    p => p.Parent,
    p => new { p.NullableTenantId, p.NullableParentId },
    b => new { b.TenantId, b.Id });

// N:1 без navigation.
post.HasOne<Blog>(
    p => new { p.NullableTenantId, p.NullableParentId },
    b => new { b.TenantId, b.Id });

// 1:1 — default full PK.
blog.HasOneToOne(b => b.FeaturedPost, p => new { p.ParentTenantId, p.ParentId });

// 1:1 — явные обе стороны.
blog.HasOneToOne(
    b => b.FeaturedPost,
    b => new { b.TenantId, b.Id },
    p => new { p.ParentTenantId, p.ParentId });

// M:N — оба PK по умолчанию.
blog.HasManyThrough<Tag, BlogTagLink>(
    b => b.Tags,
    l => new { l.BlogTenantId, l.BlogId },
    l => new { l.TagTenantId, l.TagId });

// M:N — явные все четыре списка.
blog.HasManyThrough<Tag, BlogTagLink>(
    b => b.Tags,
    b => new { b.TenantId, b.Id },
    l => new { l.BlogTenantId, l.BlogId },
    t => new { t.TenantId, t.Id },
    l => new { l.TagTenantId, l.TagId });
```

### 7.5 Нормализация и binding-контракт

- Десять A–J — **утверждённый written-design target одобренной спеки** (2026-10-17); это не
  разрешение молча перепроектировать API на этапе планирования. Планировщик может выбирать
  verification steps, но не менять утверждённый public API самостоятельно.
- Существующие шесть сигнатур сохраняются binary и source. Compiler-evidence спайка — синтетическое,
  а не тест реальной library assembly.
- Будущий план **обязан** запланировать early real-assembly binding/expression smoke checks для
  scalar, anonymous, object-typed, explicit-generic, default и explicit-override вызовов. Если
  real-assembly evidence противоречит target — затронутое изменение останавливается и возвращается
  на focused spec amendment/user approval; нельзя продолжать с самовольно изменённым API. Сейчас
  ничего из этого не реализуется.
- Каждый старый и новый overload строит **один и тот же** внутренний relation descriptor. Эквивалентные
  селекторы дают идентичные parser/metadata/result/error semantics, precedence fluent-vs-attribute и
  поведение кэша.
- Binding **может** измениться для object-typed вызовов: они перепривязываются к новому overload.
  Нельзя утверждать «binding не изменился»; тесты должны доказать, что перепривязанный вызов
  сохраняет семантику.
- Старые scalar/int/explicit-generic/same-anonymous-shape вызовы, проверенные спайком, остаются на
  старых overload-ах.
- Один и тот же результат обязателен для default- и explicit-форм при эквивалентных селекторах.

### 7.6 Отвергнутые альтернативы и доказательная база

- **Отдельное семейство generic FK/PK** (независимые `TFk`,`TPk`) отклонено: старые scalar,
  inferred key-only и typed-variable M:N вызовы становятся неоднозначными (`CS0121`).
- **Tuple-литералы** отклонены: в expression tree это `CS8143`; nullable-компоненты дают `CS8144`.
- **RelationshipKey.Create(params object?[])** — только резервный эксперимент, не выбранная политика.
- Предел арности не вводится.

Спайк: 47 синтетических проектов, .NET SDK `10.0.401`; 31 сборка прошла, 16 упали — 11 ожидаемых
негативов и 5 намеренно несовместимых сценариев семейства B. Это только compiler/marker evidence,
**не** runtime- и не продуктовые тесты; отдельный отчёт содержит все кандидаты, но в этот документ
их код не переносится.

## 8. Атрибуты

`RelationshipAttribute` сохраняет `string? ForeignKey` и получает два массива:

```csharp
public string? ForeignKey { get; set; }
public string[]? ForeignKeys { get; set; }
public string[]? PrincipalKeys { get; set; }
```

Правила:

- Ровно одна из форм FK: либо `ForeignKey`, либо `ForeignKeys`. Обе заданы — ошибка; ни одной —
  ошибка.
- `PrincipalKeys` опционален; отсутствие означает full PK. `PrincipalKeys` без FK-формы — ошибка.
- Имена проверяются как точные имена членов (`nameof` рекомендуется). Comma-delimited parsing нет;
  скалярный `PrincipalKey` не добавляется.
- Ошибки: пустая/whitespace строка, пустой массив, дубликаты, отсутствующий/unmapped член,
  несовпадение арности, несовпадение нижележащих типов.
- Массивы в атрибуте легальны: `new[] { nameof(...) }`.
- Collection navigation → `OneToMany`: FK на related child, principal на declaring parent.
  Reference navigation → `ManyToOne`: FK на declaring, principal на related.
- Существующий fluent выигрывает у атрибута для той же navigation. Если fluent выиграл, теневой
  атрибут **не** запускает новую валидацию.
- Кардинальность атрибутом не расширяется: 1:1 и M:N — только fluent.

Пример с типами `Parent`/`Child`: navigation названа `ParentNav`, чтобы внутри `Child`
`nameof(Parent.TenantId)` однозначно указывал на тип, а не на navigation-свойство с именем `Parent`.

```csharp
public sealed class Parent
{
    public int TenantId { get; set; }
    public int Id { get; set; }

    [Relationship(ForeignKeys = new[] { nameof(Child.TenantId), nameof(Child.ParentId) })]
    public ICollection<Child> Children { get; set; } = new List<Child>();
}

public sealed class Child
{
    public int TenantId { get; set; }
    public int ParentId { get; set; }

    [Relationship(
        ForeignKeys = new[] { nameof(Child.TenantId), nameof(Child.ParentId) },
        PrincipalKeys = new[] { nameof(Parent.TenantId), nameof(Parent.Id) })]
    public Parent ParentNav { get; set; } = null!;
}
```

Для `Children` `PrincipalKeys` не задан — используется полный PK `Parent` в порядке метаданных.
Для `ParentNav` principal задан явно; имена `TenantId`/`ParentId` на child и `TenantId`/`Id` на
parent различаются, что допустимо: сопоставление позиционное.

## 9. Нормализованные метаданные, inverse и валидация

### 9.1 Хранение

- Публичные list-контракты сохраняются: `IRelationshipMetadata.ForeignKey`/`PrincipalKey`,
  `RelationshipJunctionMetadata.JunctionParentForeignKey`/`JunctionChildForeignKey` остаются
  `IReadOnlyList<IPropertyMetadata>`.
- Внутреннее одиночное хранение заменяется списками. Junction хранит все четыре списка
  упорядоченными: parent key, junction parent FK, child key, junction child FK.
- Публичные интерфейсы не теряют членов; новые обязательные члены не добавляются.

### 9.2 Inverse (ARCHITECT CALL)

Inverse сопоставляется по **semantic mapped CLR member identity**, а не по object-reference equality
`IPropertyMetadata` и не по arity/type shape.

Канонические роли владения связью:

- principal entity type + **упорядоченная** последовательность identity principal-свойств;
- dependent entity type + **упорядоченная** последовательность identity FK-свойств.

Кандидат — navigation на противоположных declaring/related сторонах с совместимой кардинальностью:

- `OneToMany` ↔ `ManyToOne`;
- `OneToOne` (principal → dependent) может инвертировать dependent `HasOne` (metadata `ManyToOne`),
  указывающий на **ту же** каноническую пару; равенство `Kind` не требуется;
- M:N — тот же junction type и точный полный leg mapping с перестановкой parent/child ролей;
- self-reference — совместимость сторонних ролей обязательна.

Identity включает declaring property member/type/name, привязанные через mapped property; одних имён
на разных entity недостаточно. Последовательности сравниваются **упорядоченно**, не как set.

Неоднозначность конструктивна, когда **две разные navigation-свойства** expose ровно один и тот же
канонический mapping (например, два `Child -> Parent` reference), а не просто несвязанные CLR-типы
одинаковой формы. В таком случае запрошенный `Inverse` бросает `NotSupportedException` с именами
конфликтующих navigation-кандидатов; при отсутствии inverse возвращается `null`. Прямо выбранная
navigation **не** требует inverse.

Операция, которой inverse действительно нужен, при отсутствии/неоднозначности бросает
`NotSupportedException` при demand-resolution на query preparation — **до** SQL и до успешной
вставки в cache. Глобального падения прямых mappings из-за отсутствующего/неоднозначного
**несвязанного** inverse нет; исключений из unrelated registration до момента, когда inverse реально
понадобился, тоже нет. Новый публичный inverse declaration API не добавляется.

### 9.3 Фаза валидации (ARCHITECT CALL)

- Синтаксис selector-а и null-аргументы проверяются **при объявлении**.
- Проверки mapped-свойств, default full PK, арности и типов выполняются тогда, когда полностью
  разрешаются configured relationship metadata. Не требуется преждевременно auto-register-ить
  связанные типы до пользовательской конфигурации.
- Валидация **обязательна до исполнения SQL** и **до успешной вставки в plan cache**; невалидное или
  частично разрешённое состояние не кэшируется как валидное.
- `From<T>()` не обещает eager-валидацию ещё не зарегистрированных cross-reference.
- Null expression-аргумент → `ArgumentNullException`. Невалидное объявление → `NotSupportedException`
  с именем declaring entity/navigation и указанием компонента(ов)/количества. Существующая
  `InvalidOperationException` материализации кардинальности остаётся за #249.
- Для вручную предоставленных metadata действуют те же обязательные проверки до SQL/cache — см.
  §9.4.

### 9.4 Manual metadata compatibility (ARCHITECT CALL)

До SQL нормализованная структурная валидация применяется **и** к built-in declarations, **и** к
вручную предоставленным `IRelationshipMetadata`/junction-спискам:

- непустота;
- равная арность на каждой ноге;
- отсутствие дубликатов компонентов внутри списка;
- каждый компонент разрешается в корректное configured mapped-свойство нужной entity;
- pairwise nullable-unwrapped mapped CLR-типы.

Custom `IPropertyMetadata` **не обязан** быть тем же object reference, что configured metadata:
разрешение идёт по semantic member identity к configured mapping.

Existing valid escape-hatch mappings остаются поддержанными; явные non-PK principal-колонки
допустимы. Новый automatic principal inference или более строгая `IsKey`/unique-проверка для
injected explicit-списков не вводятся.

Malformed custom metadata не является валидным bypass: он бросает `NotSupportedException` до SQL и
до успешной вставки в cache. Это намеренное диагностическое ужесточение, а не обещание сохранить
malformed/loosely typed metadata. Публичные сигнатуры интерфейсов не меняются; fixtures старых
valid metadata обязаны проходить regression-проверку. Все relation consumers, формирующие SQL,
должны нормализовать/валидировать; свобода data-layer реализации сохраняется.

## 10. SQL-потребление

### 10.1 Общее правило ключа

Для каждой ноги связи SQL использует **упорядоченную конъюнкцию равенств по каждой паре**
компонентов. Не используются row-value tuples, не вводятся новые провайдерные лимиты на число
ключевых колонок.

Null-семантика общая: любой null-компонент FK не даёт ассоциации. Для скалярного NULL обычная SQL
семантика `NULL = ...` уже не даёт совпадения; никакого null-safe `null == null` не добавляется.

### 10.2 1:N, N:1, 1:1 в `JoinInto`

- Соединение/корреляция по metadata строятся по всем компонентам.
- Существующий non-M:N `JoinInto` с **пользовательским** `ON`-предикатом сохраняет этот предикат как
  условие. Нельзя подменять его «metadata-only» соединением, подставлять несвязанные join-ы или
  отфильтровывать родителей через `WHERE`. Builder выбирает подходящий ON/stitch-guard так, чтобы
  observable cardinality родителей сохранялась.
- Для metadata-built ON/correlation используются все компоненты.

### 10.3 M:N

- Если **любой** FK-компонент parent-link или child-link равен null, occurrence не возникает.
- «Висячие» junction-ссылки опускаются.
- Валидные повторные occurrence сохраняются как различные элементы.
- Идентичность parent/child entity по-прежнему определяется полным entity PK отдельно.
  `JoinIntoKey` и его структурное equality не меняются.

### 10.4 Неявная навигация

- Reference access, chained references, self/same-type paths; `nav == null`/`nav != null` проверяют
  существование related **ROW**, а не произвольную одну nullable-колонку; неправильная presence-check
  запрещена.
- Коллекции: существующие `Any`/`All`/`Count` и т.п., M:N-корреляция — по всем компонентам.
- Cache/join reuse identity включают полный упорядоченный mapping/junction и occurrence scope.
- Нельзя «заодно» снимать несвязанные ограничения форм запросов.
- Расширение неявной навигации — SQL pipeline only; memory features ради новых объявлений не
  добавляются.

## 11. Контракт нового metadata-driven `LoadWith` subset #249

#251 фиксирует только составную часть общего контракта. **В общий план входит только необходимый
новый metadata-driven `LoadWith` subset #249**: short form, child-factory form, все 4 кардинальности,
split/single, простые child queries и projection child queries там, где новый `LoadWith` их
поддерживает, filters/order/paging/hidden correlation, SQL only. Остальной #249 (новые implicit
collection-valued `Select` slots, `AsEntityBuilder` adapter и их независимые terminals/matrix)
остаётся отдельным DRAFT, не входит в implementation work/AC blockers #251 и не одобряется этим
документом. Для new `LoadWith` subset старые single-only формулировки #249 заменяются этим
контрактом; к #249 rest это не относится.

- `LoadWith` short/factory работает по всем компонентам ключа.
- Child predicates, global filters, ordering и per-parent paging применяются с учётом всех
  компонентов ключа.
- Projected parent без публичного ключа получает hidden correlation по всем компонентам; ключи не
  протекают в DTO-схему.
- Root cardinality/order/limit сохраняются; N+1 не появляется.
- Reference без related row → `null`; collection → пустая; более одной **различной полной entity
  identity** для одного parent → `InvalidOperationException`; дубликаты физических строк с той же
  identity допустимы. Для M:N сохраняется семантика валидных повторных junction occurrence.
- `nav == null` включает «висячий» FK.
- Общий command cache не мутируется: sticky `dontCache` не выставляется. Валидная plan identity
  включает порядок компонентов и scope; другой порядок/scope — другая identity.
- Невалидная форма не порождает SQL и не вставляет запись в кэш.
- Новые `LoadWith` entrypoint-ы **SQL-only**: вызов с in-memory context бросает
  `NotSupportedException` на query construction/provider capability validation до исполнения. Новая
  memory implementation не вводится; старые memory routes это не ломает.
- Legacy explicit-key `LoadWith` не меняется и не получает новых composite-обещаний.
- Проекция дочернего **query** внутри нового `LoadWith` входит в scope; standalone collection
  projection syntax из #249 rest — нет.
- Это контракт **new `LoadWith` subset**, а не всех metadata-потребителей #249.

## 12. Матрица приёмки (SQL only)

Все строки — acceptance criteria для SQL. AC-18 — ограничение на in-memory: новых feature
conformance/parity-тестов нет; допустимы только regression-only проверки отсутствия provenance
rejection и неизменности старого поведения.

| ID | Область | Проверяемое утверждение |
|---|---|---|
| AC-01 | Кардинальности | Fluent 1:N, N:1, 1:1, M:N с явным junction; атрибуты только 1:N и N:1 |
| AC-02 | Арность | 1, 2, 3 и 4+ компонента на каждой поддерживаемой ноге; нет искусственного cap |
| AC-03 | Имена/типы | Одинаковые и разные имена компонентов; смешанные типы; nullable и non-nullable в паре сравниваются после unwrap |
| AC-04 | Principal | Default = полный PK в порядке метаданных; явный override на non-PK/альтернативный logical key; явный порядок |
| AC-05 | Inverse | Semantic mapped member identity + canonical roles; OneToMany↔ManyToOne; OneToOne↔dependent HasOne на той же паре; M:N same junction/full swapped legs; ordered sequences; ambiguity только две разные navigation с одним mapping; direct nav без inverse; ambiguous `Inverse` → `NotSupportedException` с кандидатами; demand-resolution до SQL/cache; unrelated direct mappings не падают |
| AC-06 | Невалидные объявления | single-vs-composite/arity/type/unmapped/duplicate/nested/computed/empty/field/tuple/array отклоняются **до** SQL и до вставки в plan cache |
| AC-07 | Старый API | Scalar compile+runtime regressions; object-rebind пути сохраняют семантику; source и binary совместимость старых сигнатур; early real-assembly smoke checks для A–J |
| AC-08 | `JoinInto` | 1:N/N:1/1:1/M:N, пользовательский ON сохранён, metadata-корреляция по всем компонентам, cardinality родителей не меняется |
| AC-09 | Новый `LoadWith` subset #249 | Short/factory, 1:N/N:1/1:1/M:N, split/single, простые и projection child queries, где поддержаны; фильтры/order/per-parent paging; projected parent hidden key; DTO не получает ключи; root limit/order сохранены; N+1 нет; SQL only |
| AC-10 | Неявная навигация | Reference/scalar chain, self/same type, `nav == null`/`!= null` по row existence, collection `Any`/`All`/`Count`, M:N |
| AC-11 | Null FK | Каждый компонент null, все null, «висячий» non-null FK: нет ассоциации; null-reference/пустая коллекция при eager |
| AC-12 | M:N links | Null в любой стороне link → нет occurrence; dangling links опущены; валидные duplicate occurrences сохранены |
| AC-13 | Изоляция | Родители с одинаковым локальным `Id`, но разными `Tenant`; несвязанные relations с тем же первым ключом не делят cache |
| AC-14 | Кэш | Cold/warm/prepared планы; разный порядок/scope компонентов — разная identity; ошибка не кэшируется |
| AC-15 | SQL-генерация | Все шесть: sqlite, postgres, sqlserver, mysql, mariadb, clickhouse — в соответствующих unit-проектах |
| AC-16 | Live DB | SQLite in-process + контейнерные PostgreSQL, SQL Server, MySQL, ClickHouse через integration harness |
| AC-17 | MariaDB | Live-исполнение должно быть явно «доступно/не запускалось»; нельзя выдавать MySQL-прогон за MariaDB |
| AC-18 | In-memory | Новых feature conformance/parity-тестов нет. Regression может утверждать отсутствие provenance rejection на metadata resolution и неизменность старого поведения; новый `LoadWith` на in-memory бросает `NotSupportedException` до исполнения |
| AC-19 | Assertions | Каждая проверка значения/исключения — FluentAssertions; `Xunit.Assert` только для skip-примитивов |
| AC-20 | Integration | Реальная БД; перед запуском прочитать локальные инструкции `running-integration-tests`; при отсутствии socket поднять Podman и задать `DOCKER_HOST`; skip не выдавать за green |
| AC-21 | Coverage | Общие 85 line / 75 branch и integration 65 line / 75 branch сохраняются; известный baseline 61.9 branch (красный) не понижается и не объявляется зелёным из-за фичи |
| AC-22 | Coverage honesty | Если baseline остаётся красным — сообщить blocker; не «подшивать» несвязанный repo-wide uplift в scope фичи |
| AC-23 | Manual metadata | Valid old escape-hatch fixtures проходят регрессию; malformed/loosely typed списки (empty, arity, duplicates, wrong mapped property, type mismatch) → `NotSupportedException` до SQL/cache; semantic member identity, не object reference; explicit non-PK principal разрешён |
| AC-24 | Scope #249 | AC блокируется только new metadata-driven `LoadWith` subset; implicit collection-valued `Select`, `AsEntityBuilder` adapter и их terminals/matrix не входят в implementation work/AC #251 |
| AC-25 | API target validation | Ранняя real-assembly binding/expression smoke-проверка scalar/anon/object-typed/explicit-generic/default и explicit override; при противоречии — stop затронутого изменения и возврат на focused spec amendment, а не unilateral redesign |

## 13. Совместимость и намеренные изменения

- **Default composite first-key bug fixed → arity error.** Ранее single FK при default multi-PK
  молча брал первый ключ; теперь это ошибка арности. Это намеренное изменение поведения.
- **Ранее бросавший anon теперь поддержан.** Формы, которые раньше не компилировались/бросали
  `NotSupportedException`, становятся валидными.
- **ARCHITECT CALL — плохие nested/numeric/computed формы теперь отклоняются** явно: это
  ужесточение валидации, а не user-picked detail.
- **Object-overload rebind:** старые вызовы со статическим `object`/`object?` selector-ом могут
  перепривязаться к новому overload; они обязаны сохранять семантику.
- **Null SQL semantics:** null-компонент никогда не даёт ассоциацию; старое in-memory `null == null`
  не переносится в SQL и не нормализуется.
- **Metadata provider-neutral:** declarations и metadata не получают in-memory-specific rejection на
  metadata resolution или provenance flag. Existing in-memory consumers не модифицируются; если они
  уже понимают list metadata из ручной регистрации, они могут естественно потребить wellformed
  metadata из новых объявлений. Это не support guarantee, не новые parity-тесты и не deliberate
  regression по происхождению metadata. Старые memory `null == null` tests/behavior и рабочие paths
  сохраняются; `JoinIntoKey.Equals` не меняется.
- **New `LoadWith` SQL-only:** вызов нового metadata-driven `LoadWith` с in-memory context бросает
  `NotSupportedException` на query construction/provider capability validation до исполнения. Это не
  blanket fail-closed для старых memory routes и не новая memory implementation.
- **Manual metadata:** structural validation применяется и к built-in, и к manual lists; valid old
  escape-hatch mappings сохраняются, explicit non-PK principal разрешён. Malformed/loosely typed
  custom metadata больше не bypass: `NotSupportedException` до SQL/cache — намеренное ужесточение.
- **#249 boundary:** общий план включает только new metadata-driven `LoadWith` subset (short/factory,
  split/single, child query/projection внутри него); implicit collection-valued `Select`,
  `AsEntityBuilder` adapter и их terminals/matrix остаются отдельным DRAFT и не являются AC blocker
  #251. #249 в целом не auto-approved и не закрывается.
- **API target:** A–J — written-design target. Compiler evidence synthetic, не real-assembly тест;
  early real-assembly binding/expression smoke checks обязательны. При противоречии — stop и focused
  spec amendment/user approval, без unilateral redesign.
- **Публичные metadata-интерфейсы:** ни один член не удаляется; новые обязательные члены не
  добавляются, чтобы не ломать внешние реализации.
- **Документация EN/RU:** актуальные пути нужно перепроверить перед docs-работой из-за текущей
  несвязанной реорганизации. На момент brainstorm наблюдались `docs/relationships/*.md` и
  `docs/ru/relationships/*.md`; в них придётся обновить утверждения «fluent helpers take a single
  key column» и доступность composite-форм. Публичные docs не должны ссылаться на внутренние
  `docs/specs/**`.
- **Никаких implementation-изменений на этой стадии:** без коммитов, push, merge и правок
  product/test/config; этот артефакт — только документ.

## 14. Ledger решений

| Решение | Статус | Комментарий |
|---|---|---|
| Письменная спека shared scope: #251 + new `LoadWith` subset #249 | **APPROVED пользователем в чате 2026-10-17** | Одобрен только written shared scope; остальной #249 — DRAFT; реализация не разрешена; плана нет |
| Четыре fluent-кардинальности; атрибуты только 1:N/N:1 | Согласовано пользователем | Концепция |
| Явный FK; default full PK; позиционное сопоставление; override | Согласовано пользователем | Детали порядка — architect call ниже |
| Общий будущий план #251 + только new metadata-driven `LoadWith` subset #249 | **User-approved grouping; вошло в одобренную спеку 2026-10-17** | Не весь #249, не auto-approval, не разрешение на реализацию |
| Маршруты `JoinInto`, new `LoadWith` subset, существующая неявная навигация | **User-approved grouping** для new `LoadWith`; одобренная спека покрывает shared scope | New `LoadWith` entrypoints входят в будущий план; legacy не меняется; #249 rest — отдельный DRAFT |
| Null FK → нет ассоциации; dangling → нет row | Согласовано пользователем | SQL-only |
| In-memory не поддерживаем для нового; старое не трогаем | Согласовано пользователем (последнее уточнение) | Приоритет над ранним all-provider parity |
| `DataContextCache.Metadata`/`IRelationshipMetadata` escape | Согласовано пользователем | Valid manual mappings совместимы; malformed валидируется |
| Источник порядка = `metadata.Properties` по `IsKey`; без нового PK Order API | **ARCHITECT CALL** | Не user-picked detail |
| Ровно 10 overload-ов A–J как written-design target | **ARCHITECT CALL** | Не planner freedom; real-assembly smoke + stop rule |
| In-memory metadata provider-neutral, без provenance rejection | **ARCHITECT CALL** | Existing consumers не модифицируются; new `LoadWith` SQL-only |
| Manual metadata structural validation + semantic member identity | **ARCHITECT CALL** | Valid old fixtures regress; malformed → `NotSupportedException` до SQL/cache |
| Inverse canonical ownership roles + member identity | **ARCHITECT CALL** | OneToMany↔ManyToOne, OneToOne↔dependent HasOne, M:N swapped legs; ambiguity only same canonical mapping |
| Фаза валидации и demand-resolution inverse | **ARCHITECT CALL** | До SQL/cache; unrelated mappings не падают |
| Запрет tuple/array/computed selector-языка | **ARCHITECT CALL** | Ужесточение |
| Детализация матрицы тестов | **ARCHITECT CALL** | Уточняется на этапе планирования |

Builder freedoms: внутренняя факторизация representation helper-ов, имена nonpublic helper-ов,
организация тестовых fixture-ов и порядок шагов плана. Это **не** свобода молча выбросить new
`LoadWith` subset, расширить in-memory или изменить утверждённый A–J target.

## 15. Риски и открытые вопросы

- **Кросс-issue координация #251/#249.** Финальная приёмка #251 невозможна без composite-потребителя
  в **new metadata-driven `LoadWith` subset**; остальной #249 не является AC blocker. Следующий
  артефакт — только общий план; реализация документом не авторизована.
- **API target vs real assembly.** Спайк даёт только synthetic compiler evidence. Ранняя
  real-assembly smoke-проверка может выявить противоречие A–J; в этом случае затронутое изменение
  останавливается и возвращается на focused spec amendment, а не перепроектируется планировщиком.
- **Manual metadata tightening.** Structural validation может отклонить ранее «работавшие»
  malformed/loosely typed custom mappings. Это намеренно; valid old fixtures обязаны проходить
  regression-проверку.
- **In-memory boundary.** Metadata provider-neutral, но new `LoadWith` entrypoints SQL-only. Нельзя
  добавлять provenance rejection на metadata resolution или молча реализовывать memory support;
  старые memory paths и `null == null` tests сохраняются.
- **Dirty tree.** В ходе brainstorm рабочее дерево могло содержать посторонние изменения; этот
  артефакт добавляет один файл и не должен захватывать чужие правки.
- **Spike ephemeral.** Отчёт лежит в `/tmp/opencode/...` и не является постоянным evidence; его
  выводы и сигнатуры перенесены в этот документ. Спайк не даёт разрешения на реализацию.
- **Overload rebind.** `object`-типизированные старые вызовы — главный источник скрытой
  семантической регрессии.
- **Порядок компонентов.** Его нужно явно документировать; иначе пользователи будут ожидать
  алфавитный или «как в исходнике» порядок.
- **Coverage baseline.** Известный integration branch baseline 61.9 % уже красный; фича не обязана и
  не должна молча его «исправлять» ценой порога.
- **#249 rest.** Новые implicit collection-valued `Select`, `AsEntityBuilder` adapter и их
  terminals/matrix остаются отдельным DRAFT; их реализация не обещана и не блокирует #251.

## 16. Следующая стадия

1. **Builder check — выполнен** (исторически, до одобрения). Его результат зафиксирован в §18.
2. **Письменный review — завершён 2026-10-17.** Пользователь в чате ответил «да» на вопрос об
   одобрении этой письменной спеки. Одобрен shared scope: #251 + только новый metadata-driven
   `LoadWith` subset #249; остальной #249 остаётся DRAFT.
3. **Tracking — согласован:** #251 и #249 обновлены в статусной части (без новых issue/комментариев,
   без закрытия); см. §17.1.
4. **Следующий артефакт — только единый план реализации** для #251 + new `LoadWith` subset #249.
   В этой сессии планирование не начато: skill `writing-plans` недоступен (экспонирован только
   `brainstorming`). Это фактический blocker текущей сессии, а не утверждение, что план невозможен;
   планирование не подменяется реализацией.

Письменная спецификация одобрена (2026-10-17). Плана реализации не существует; реализация и
автоматический запуск PDCA не разрешены.

## 17. Evidence и ссылки

### 17.1 Tracking

- #251: https://github.com/AlexeyShirshov/nextorm/issues/251 — OPEN, milestone `1.0.9-rc3`.
- #249: https://github.com/AlexeyShirshov/nextorm/issues/249 — OPEN, milestone `1.0.9-rc3`.
- Одобрение: 2026-10-17, в чате (ответ «да»); GitHub review/comment/approval-URL не создавался.
- Статусные поля обеих задач обновлены под это одобрение; titles/milestones/state/labels не менялись,
  scope/приёмка/закрытие не менялись.

### 17.2 Локальный спайк

- `/tmp/opencode/nextorm-251-api-spike-20261017-211946-374009/RESULTS.md`
- 47 сценариев, SDK `10.0.401`: 31 успешная сборка, 16 ошибок (11 expected negatives + 5
  intentionally incompatible family B).
- Ограничение: compile-time/marker evidence; runtime, product tests, null-семантика, разбор
  анонимных выражений и new `LoadWith` subset #249 в спайке не исполнялись.

### 17.3 Код и тесты (строки зафиксированы во время brainstorm; могут смещаться)

| Файл:строка | Что подтверждает |
|---|---|
| `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:521,551,578,608,642,682` | шесть существующих fluent-сигнатур |
| `src/nextorm.core/DataContext/Meta/EntityMetadataBuilder.cs:937` | `ResolveSelectedProperty` разбирает один `PropertyInfo` |
| `src/nextorm.core/Builders/EntityBuilder.cs:642` | ровно один legacy explicit-key `LoadWith` overload; новых metadata-driven entrypoint-ов нет |
| `src/nextorm.core/DataContext/Meta/Implementation/RelationshipResolver.cs:46` | default principal = первый `IsKey` |
| `src/nextorm.core/DataContext/Meta/Implementation/RelationshipResolver.cs:66` | inverse по одиночному `ForeignKeyProperty` |
| `src/nextorm.core/Visitors/NavigationPathResolver.cs:302` | `RequireSingleKey` отклоняет composite в неявной навигации |
| `src/nextorm.core/DataContext/Meta/IEntityMetadata.cs:12-15` | `Properties` документирует declared/discovered порядок |
| `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:745-779` | identity/DML уже собирают полный PK в порядке метаданных |
| `src/nextorm.core/Builders/Joins/JoinIntoSpec.cs:816` | `JoinIntoKey` — структурная identity, которую нельзя менять |
| `tests/nextorm.core.tests/JoinInto/JoinIntoCompositeNullSemanticsTests.cs:10-15,109,123` | известное расхождение in-memory `null == null` и SQL; остаётся вне scope |
| `docs/relationships/relationships.md:194,292` | публичная формулировка про single-column fluent и composite из метаданных |
| `docs/relationships/implicit-navigation.md:257-258` | публичное ограничение неявной навигации по composite |

В документе нет вымышленных GitHub `blob` URL: используются только проверенные issue-ссылки и
локальные пути/строки, зафиксированные в brainstorm.

## 18. Review record

- **Исторически:** fresh builder check выполнен; его замечания учтены в draft-е.
- **Шесть вопросов (исторически).** Единственный user-owned вопрос — deliverable grouping —
  пользователь ответил **YES**: один общий будущий план для #251 + **только необходимый новый
  metadata-driven `LoadWith` subset #249**, а не все projected collection/`AsEntityBuilder`
  возможности #249. Тогда это было согласие на группировку, не разрешение на реализацию.
- **Остальные пять** урегулированы уточнёнными существующими фактами/architect calls в
  соответствующих разделах: API target — §7.5/§14; in-memory boundary — §2.6/§11/§13; manual
  metadata — §9.4; inverse — §9.2; validation/scope details — §3/§9.3/§11/§14.
- **APPROVED: 2026-10-17, в чате.** Пользователь ответил «да» на вопрос об одобрении этой письменной
  спеки. Одобрен **written shared scope #251 + только новый metadata-driven `LoadWith` subset #249**;
  остальной standalone collection projection/`AsEntityBuilder` DRAFT #249 не одобрен.
- **Что входит в одобрение (сводка):** все четыре fluent-кардинальности; атрибуты только 1:N/N:1;
  default principal — полный PK в порядке метаданных; composite-контракт для `JoinInto`, SQL implicit
  navigation и нового `LoadWith` (split/single); любой null-компонент FK → нет SQL-связи; in-memory
  исключён, старое memory-поведение (`null == null`) сохраняется; escape hatch
  `DataContextCache.Metadata` сохранён. Полные детали и architect calls — §2–§14.
- **Доказательная база:** локальный compiler-only спайк (§17.2,
  `/tmp/opencode/nextorm-251-api-spike-20261017-211946-374009/RESULTS.md`); runtime/продуктовых
  тестов нет.
- **Неразрешённых USER-вопросов нет.**
- **APPROVED ≠ implementation.** Одобрение спеки не одобряет реализацию: следующий артефакт — только
  общий план. Плана нет; планирование в этой сессии не начато, потому что skill `writing-plans`
  недоступен (экспонирован только `brainstorming`), и оно не подменяется реализацией. Нельзя
  утверждать «reviewer approved implementation».
- **Новые architect decisions** перечислены в ledger §14.
