# #143 — Identity Returning для join/multi-table мутаций

**Дата:** 2026-10-01
**Статус:** Дизайн согласован; реализация поставляется в рамках коллекции `1.0.9-b-2` (PDCA задача 1).
**Область утверждения:** согласование дизайна. Реализация ведётся в рамках коллекции `1.0.9-b-2` (PDCA задача 1).

**Ссылки:**
- Issue: https://github.com/AlexeyShirshov/nextorm/issues/143
- Комментарий с требованиями: https://github.com/AlexeyShirshov/nextorm/issues/143#issuecomment-5926558930
- Родительский issue: #136 (explicit projected join UPDATE/DELETE Returning)

---

## 1. Цель и контекст

В #136 отгружен **explicit projected** join UPDATE/DELETE Returning. Форма без проекции
(whole `Projection`) была отложена **структурно**, а не только из-за трудоёмкости: текущий парсер
явно отвергает identity-форму, а поиск имени при чтении CTE не может различить item-слоты
(один и тот же тип/имя свойства в нескольких слотах).

Цель: вызов `Returning()` без явной проекции возвращает **все returnable mapped-свойства каждого
item** встроенного `Projection<T1..Tn>`. Отсутствие вызова `Returning()` **не должно** автоматически
возвращать объекты. Обязательны оба маршрута: прямая материализация и потребление mutation CTE.
Критерий успеха: однозначное разрешение членов self-join CTE и корректные значения объектов для
полной поддержки арностей 2..8 при сохранении explicit-формы.

## 2. Принятая область и существующие ограничения

- Сохраняются текущие provider × operation гейты. Сегодня только PostgreSQL поддерживает
  `SupportsUpdateJoinReturning`, `SupportsDeleteJoinReturning` и `SupportsDataModifyingCtes`.
- Identity имеет ровно ту же поддержку provider/операций, что и explicit-форма; это **не новая
  диалектная фича**. Для не-PostgreSQL поведение reject не меняется.
- Только `INNER JOIN`, физическая таблица как цель `Item1`; уже допустимые derived/read-CTE
  joined-side источники остаются допустимыми. Разрешённый вид источника не означает наличия всех
  колонок исходной сущности — identity возвращает полный замапленный shape, который предоставляет
  существующая модель запроса (см. §4).
- Не добавляются: outer/cross/apply, alias-only именованные проекции (#113 API), мутации других
  target-слотов, дополнительные Range/dynamic Returning-возможности.
- Только встроенные арности `Projection` 2..8.
- Кардинальность/порядок и обработка multi-match строк источника следуют существующей семантике
  мутаций PostgreSQL; identity не вводит новых гарантий упорядочивания/кардинальности.

## 3. Публичный API и совместимость

- `UpdateJoinBuilder<TProjection>.Returning()` возвращает
  `UpdateJoinReturningBuilder<TProjection,TProjection>`; используется та же конвенция построения
  identity-лямбды, что и для single-table `Returning`.
- Delete-расширения на `JoinedEntityBuilder<T1..Tn>` добавляют parameterless `Returning()` формы для
  всех арностей 2..8, возвращающие `DeleteJoinReturningBuilder<Projection<...>,Projection<...>>`.
- `Returning(p => p)` эквивалентен parameterless-форме для этих встроенных типов.
- Существующие `Returning<TResult>(projection)`, типы builder/result и поведение сохраняются; нет
  удалённых/переименованных сигнатур.
- Существующие explicit проекции (scalar/anonymous/ctor/member-init) не меняются.
- Именованные alias'ы недостижимы через существующие mutation-приёмники и вне области; generic-
  обещаний для произвольной `IProjection` реализации нет.

**Иллюстративный PROPOSED-пример API (НЕ существующий сейчас):**

```csharp
var update = ctx.From<Employee>()
    .Join(ctx.From<Employee>(), (e, m) => e.ManagerId == m.Id)
    .UpdateJoin()
    .Set(p => p.Item1.Name, "Alice!")
    .Returning();
```

Контраст с explicit-формой: `.Returning(p => new { p.Item1.Id, p.Item2.Name })`. Отличие от
отсутствия `Returning()`: без вызова ничего не возвращается автоматически.

## 4. Семантика результата

- Исходный тип/порядок остаётся `Projection<T1..Tn>`; `Item1` — первый/базовый источник и цель
  мутации, `Item2..ItemN` — joined-источники в исходном порядке; **никогда** не переупорядочивать по
  CLR-типу или имени.
- `UPDATE`: item-цель отражает возвращённые post-update значения БД. `DELETE`: item-цель отражает
  значения удалённой строки; joined-items — значения, возвращённые базой, без искусственного refresh.
- Слоты self-join с одним CLR-типом обязаны оставаться различимыми.
- Возвращаются все mapped entity-свойства по текущим правилам Returning-метаданных, включая
  identity/computed и mapped physical names; конвертеры/provider types/duration-метаданные
  переносятся в материализацию.
- `NotMapped`/dynamic store исключаются по текущему Returning (dynamic data не возвращается «магически»).
- Существующая неподдерживаемая multi-column Range-пара в любом месте полного результата даёт
  явную ошибку в существующем стиле, а не отбрасывает колонки; обычное расширение Range в SELECT не
  должно случайно расширять Returning за счёт переиспользования whole-entity select-expansion «слепо».
- Разрешённый вид источника не означает наличие всех колонок исходной сущности. Identity возвращает
  полный замапленный shape каждого item, предоставленный существующей моделью запроса; разрешённая
  derived/read-CTE сторона с полным shape должна работать. Если metadata требует возвращаемое
  свойство, которого нет в доступном shape источника, запрос явно отклоняется с указанием
  slot/member; нельзя молча уменьшать возвращаемый результат или обращаться к несуществующей колонке.
  `NotMapped`-свойства не являются требуемыми возвращаемыми колонками. PoC проверяет одну разрешённую
  representative full-item derived-shape; по-настоящему недоступный returnable-член — explicit error,
  без изобретения поддержки.

## 5. Выбранный дизайн идентичности колонок и альтернативы

**Принятый логический адрес:** item slot + mapped property identity. Нормализация слотов
согласована с `ProjectionAliasCache`/`ItemN` из #113 (внутренний `ProjectionEntityItem.Slot`
zero-based, публичный `ItemN` one-based). Это **не** CLR-тип сам по себе, **не** `PropertyName` сам по
себе и **не** result ordinal сам по себе.

- Ассоциация явно сохраняется в возвращаемом `SelectExpression`/shape-метаданных с использованием
  существующей slot-grouping `ProjectionEntityItem` и property-метаданных, где это возможно.
- Дизайн **не требует** конкретного нового публичного типа или публичных полей; точное приватное
  представление принадлежит плану реализации и обязано сохранять семантический ключ.
- Ordinal остаётся для материализации, отдельно от source table alias; `ProjectionEntityItem.Slot` —
  идентичность projection/result item, а не SQL table ID.
- Разрешение source-column идёт через правильный source query/slot, включая повторяющиеся типы и
  derived joins; разрешение output CTE-column — через логический mapping returned slot+property.
- Генерируются детерминированные уникальные SQL alias'ы для identity flattened outputs; сохранённый
  alias в метаданных используется напрямую, без обратного парсинга строк.
- Алиасы вида `__s1_id` / `__s2_id` — иллюстративны, не обязательный финальный нейминг.
- Аллокация ведётся по всему flattened output, чтобы избежать коллизий внутри/между слотами и
  коллизий mapped names; стабильна для одной prepared shape; нельзя полагаться на
  case-folding/уникальность CLR-имён свойств.
- Существующие SQL-имена/shape explicit-проекции остаются неизменными.
- CTE-read и последующие поддерживаемые derived-обёртки должны сохранять slot/property/alias
  метаданные и разрешать `ItemN.Property` в его returned column, а не через name-only lookup или
  underlying table alias.
- Обычное поведение joins/explicit flat projection сохраняется.
- Неизвестный/неоднозначный логический ключ — explicit diagnostic, а не выбор первого совпадения.

**Отклонённые альтернативы:**
- Только SQL alias — обратный парсинг строк хрупок.
- Только ordinal — CTE SQL всё равно нуждается в именах.

Текущие #113 slot-хелперы переиспользуются там, где применимо, но **не** предполагается, что они уже
чинят CTE.

**Концептуальный SQL из обсуждения (концептуально, не финальный рендер):**

```sql
WITH changed AS (
  UPDATE employees AS e SET name = e.name || '!'
  FROM employees AS m
  WHERE e.manager_id = m.id
  RETURNING e.id          AS "__s1_id",
            e.name        AS "__s1_name",
            e.manager_id  AS "__s1_manager_id",
            m.id          AS "__s2_id",
            m.name        AS "__s2_name",
            m.manager_id  AS "__s2_manager_id"
)
SELECT changed."__s1_id" AS "EmployeeId",
       changed."__s2_id" AS "ManagerId"
FROM changed;
```

**Метаданные (таблица):**

| Логический член | Stored alias |
|---|---|
| `Item1.Employee.Id` | `__s1_id` |
| `Item2.Employee.Id` | `__s2_id` |

Translator извлекает **сохранённый** alias. Прямая материализация переиспользует grouped entity item
`RowMaterializerBuilder`, а не новый projection-тип. Существующий интерфейс/публичная поверхность не
должны расширяться только ради хранения внутренних slot-ключей.

## 6. Компонентный поток данных

1. Join builder создаёт identity-expression.
2. Prepare готовит returnable mapped scalar-колонки для каждого item с source binding,
   slot/property/alias и materializer-метаданными.
3. Join mutation SQL рендерит эти самые alias'ы.
4. Standalone исполняет с grouped materializer **или** `MutationCteQuery` строит согласованный
   `ColumnShape`; `MemberTranslator` разрешает returned item member из этого shape; outer SQL
   выбирает записанный alias; существующий materializer конструирует Projection items.

Точная интеграция/приватная декомпозиция откладывается в план реализации — это **не** только снятие
identity-guard. Сохраняются инварианты `QueryCommand`/plan cache: никакого sticky per-call
`Cache = false` на shared-командах, никакого per-row string alias parsing или повторной рефлексии на
hot-path; prepared-метаданные переиспользуются. Это наследуемая repo-гигиена, а не заявление об
измеренной производительности.

## 7. Ошибки

- Сохраняются dialect- и join-form-отказы и explicit Range-ошибка.
- Недоступный/неоднозначный returned-column адрес падает с полезной информацией о slot/member.
- Нет silent partial identity result, нет произвольной первой колонки.

## 8. Тестовая матрица

- Обязательно UPDATE и DELETE × арности 2..8 × standalone и mutation CTE для базовой корректности
  all-item результата (core/SQL-генерация **плюс** реальная материализация на PG).
- Эквивалентность `Returning()` и `p => p` для обеих операций.
- Коллизии: разные CLR-типы с одинаковыми именами свойств; один и тот же CLR-тип повторно; mixed
  repeated types, nonadjacent repetitions.
- Special collisions/mapping/converters на representative арностях 2,3,8 вместо полного
  декартова взрыва.
- Проверка `Item1`-цели и исходного порядка слотов на различающихся значениях (не полагаться на SQL
  ordering), значения после update / удалённая строка, no rows по существующей семантике.
- Physical mapped names, отличные от CLR-имён; converter, читающий метаданные; исключение
  `NotMapped`/dynamic; explicit провал Range.
- CTE-read полного `Projection` и выбор/фильтрация `Item1.Id`/`Item2.Id`; поддерживаемая последующая
  derived read-обёртка сохраняет адресацию.
- Существующий допустимый derived/read-CTE joined side с корректными binding'ами.
- Compatibility-тесты существующих explicit-форм (scalar/anonymous/ctor/member-init), snapshot'ы
  source/SQL naming неизменны где релевантно, поведение без `Returning()`, single-table `Returning`,
  обычные repeated-type joins/#113 alias'ы, отказы unsupported providers и outer/cross join неизменны.
- Unit/SQL тестов недостаточно, чтобы доказать реальные возвращённые объекты; требуется PG
  real-infra integration. Container-провайдерные наборы нельзя подавать как green, если они skipped.
- На этапе исполнения следовать guidance `running-integration-tests`. Для написания этого документа
  тесты не запускаются.

## 9. PoC-гейт (не полная реализация)

Перед полной реализацией — изолированный throwaway probe/patch по утверждённому плану. Минимум:
two-item self-join, один тип, идентичные имена свойств, различающиеся данные; обе identity-мутации
UPDATE/DELETE; standalone full materialization и mutation CTE full/materialized + отдельное чтение
`Item1.Id`/`Item2.Id`; один представительный разрешённый derived/read-CTE joined-source случай с
доступной полной shape. PoC обязан продемонстрировать уникальные SQL alias'ы, персистентную
slot-aware shape и фактические PG-значения; простой генерации SQL или снятия guard недостаточно.
Отчёт с evidence, с чёткой меткой throwaway; PoC не закрывает issue и не является автоматически
production-реализацией. Если блок падает — остановиться и пересмотреть архитектуру/требования с
владельцем, никогда не отбрасывать молча CTE/derived/arity/duplicate scope. Полная матрица 2..8
всё равно обязательна после успеха PoC.

**Владелец утвердил дизайн PoC, но НЕ его исполнение** (сначала ревью письменной спеки и
планирование реализации).

## 10. Документация, завершение и передача

- Публичные EN/RU документация: обновить guides `08-cte`, `17-update-statement`,
  `16-delete-statement` и релевантные API XML в **той же** будущей реализации; убрать identity-
  запрет и описать покрытие/лимиты; без публичных ссылок на `docs/specs`.
- Issue можно закрыть только когда завершены: согласованный API, все арности, оба маршрута,
  collision/mapping/error/compatibility тесты и evidence реальной PG-интеграции, документация.
- Следующий шаг: владелец ревьюит эту письменную спеку; после явного письменного одобрения спеки —
  написать implementation plan, затем отдельно выбрать способ исполнения/PDCA.
- Комментарий рекомендует PDCA, но пользователь не запрашивал цикл в этой беседе; **не запускать
  PDCA** на основании предположения. Без явного запроса — никаких коммитов/push.

## 11. Опорные точки (evidence anchors)

### 11.1 Подтверждённые существующие факты

- Issue/комментарий: ссылки в шапке.
- Identity-guard: `src/nextorm.core/Builders/JoinedReturningProjection.cs:30-32`.
- Name-based derived read: `src/nextorm.core/Visitors/MemberTranslator.cs:531-551`, `573-615`.
- Existing grouped item expansion: `src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:599-681`.
- Slot-модель: `src/nextorm.core/Expressions/SelectExpression.cs:319-340` (`ProjectionEntityItem`),
  `SelectExpression.ProjectionItem:105`.
- Materializer: `src/nextorm.core/DataContext/RowMaterializerBuilder.cs:53-60`, `110-187`, `226-254`.
- #113 slot helpers: `src/nextorm.core/DataContext/ProjectionAliasCache.cs:35-53`, `85-130`.
- Встроенные типы: `src/nextorm.core/Builders/Projection.cs` (типы `Projection<...>` 2..8).
- INNER rejection: `src/nextorm.core/Builders/Joins/JoinedMutationSource.cs:36-47`.
- Update entry / delete returning: `src/nextorm.core/DataContext/DataContextExtensions.cs:745-836`,
  `845-961`.
- Existing Returning rules: `src/nextorm.core/Builders/ReturningProjection.cs:26-35`, `84-98`.
- Mutation CTE: `src/nextorm.core/Builders/MutationCteQuery.cs:56-65`, `112-158`, `184-196`.
- Accepted provider flags: `src/nextorm.postgres/PostgresDialect.cs:25`, `28`, `34`.

Ссылки на строки взяты из scout; при необходимости сверяются, точные refs не выдуманы.

### 11.2 Новые согласованные решения

- Identity `Returning()` для встроенных `Projection` 2..8, только по существующим provider/operation
  гейтам (PostgreSQL), INNER JOIN.
- Логический адрес колонки: item slot + mapped property identity; детерминированные уникальные SQL
  alias'ы, сохранённые в slot-aware shape-метаданных; alias обязательно стабильны для prepared shape.
- `Returning()` ⇔ `Returning(p => p)`; отсутствие вызова ничего не возвращает автоматически.
- Порядок/тип результата `Projection<T1..Tn>` не переупорядочивается; слоты одного CLR-типа
  остаются различимыми.
- Отклонены alias-only и ordinal-only подходы; #113-хелперы переиспользуются, но CTE не считаются
  уже починенными.
- Обязательны PoC-гейт и последующая полная матрица 2..8; текущий документ — только утверждение
  дизайна, не разрешение на реализацию или исполнение PoC.
