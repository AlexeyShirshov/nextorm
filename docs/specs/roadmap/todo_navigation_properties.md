# Спека: navigation properties / relationships (#105)

> Tracking issue: [#105](https://github.com/AlexeyShirshov/nextorm/issues/105). Заменяет #52
> («Navigation properties», закрыт как superseded). Связано: #40 «Child collections», #95
> (eager loading, закрыт), `sql-capabilities-gap-analysis.md` §4 п.49, §6 workstream 13.

> **Статус (2026-09-28).** Слайсы A и B реализованы на ветке `1.0.9-a`, CHECK пройден. Слайс C
> (неявные соединения) отложен. Спека — единственный источник истины по объёму; при расхождении
> с #105 приоритет у спеки.

## 1. Цель и не-цели

**Цель.** Дать nextorm декларативные метаданные связей и — как первый видимый срез — явную
single-query загрузку дочерней коллекции `JoinInto` (`LEFT JOIN` + дедуп родителя + in-memory
группировка). Полные **неявные** соединения (вывод `JOIN` из `e.Parent.Name`) — следующий этап, их
закладывает фундамент, но в этот цикл не входят.

Сегодня есть только явные `Join`/`LeftJoin`/…/`OuterApply` и явная `Projection<T1..T8>`; метаданных
связей нет, eager loading уровня 1 — split-query `LoadWith` (#95), у которого связь задаётся
селекторами на месте вызова, а не метаданными.

**Не-цели этого цикла.**

- Неявные соединения из навигаций (слайс C) — отдельный цикл.
- Составные ключи и M2M/O2O в `JoinInto` — модель их представляет, реализация отклоняет явно.
- Изменение `LoadWith` (#95, split-query) — сосуществует; связь по-прежнему задаётся селекторами.
- Вывод связей по конвенции (без явного объявления).
- Изменение существующих join-методов и `Projection<T1..T8>`.

## 2. Декомпозиция

| Слайс | Содержание | Гейт |
|---|---|---|
| **A — фундамент** | Модель метаданных связей (все виды), fluent + атрибут, исключение навигаций из маппинга колонок, `IEntityMetadata.Relationships` | Обязателен для B и C |
| **B — `JoinInto`** | Явная single-query загрузка O2M/M2O на всех провайдерах + in-memory | Требует A |
| **C — неявные join'ы** | Вывод соединений для основного источника / join-источников / подзапросов | Отдельный цикл после B |

Слайс B поставляется двумя PR (см. §9).

## 3. Модель метаданных связей (слайс A)

### 3.1. Типы (`src/nextorm.core/DataContext/Meta/`)

```csharp
public enum RelationshipKind
{
    OneToMany,   // навигация-коллекция на principal-стороне
    ManyToOne,   // навигация-ссылка на dependent-стороне
    OneToOne,    // навигация-ссылка, FK уникален (уникальность не валидируется ядром)
    ManyToMany,  // обе навигации — коллекции, через junction
}

public interface IRelationshipMetadata
{
    RelationshipKind Kind { get; }
    Type DeclaringType { get; }
    Type RelatedType { get; }
    PropertyInfo Navigation { get; }                    // свойство на DeclaringType
    bool IsCollection { get; }                          // Navigation — ICollection<T>
    IReadOnlyList<IPropertyMetadata> ForeignKey { get; } // dependent-сторона, FK-колонки
    IReadOnlyList<IPropertyMetadata> PrincipalKey { get; } // principal-сторона, key-колонки
    IRelationshipMetadata? Inverse { get; }             // навигация на RelatedType, если объявлена
}
```

`IEntityMetadata` получает член с default-реализацией (обратная совместимость внешних реализаций):

```csharp
IReadOnlyList<IRelationshipMetadata> Relationships => Array.Empty<IRelationshipMetadata>();
```

M2M представляется расширением модели (junction-дескриптор: тип/таблица junction и два FK). Полная
форма M2M — часть модели, но реализуется в слайсе B как `NotSupportedException` (см. §6). Точная форма
junction-дескриптора фиксируется при реализации M2M и не является публичным API до этого.

### 3.2. Fluent-объявление

Объявление — **явное и симметричное**: каждая сторона объявляет свою навигацию и общий FK.
Principal-ключ берётся из существующих key-метаданных (`IPropertyMetadata.IsKey`):
`[Key]`, `EntityPropertyBuilder<T>.Key()` или конвенция `Id`/`<TypeName>Id`.

```csharp
// Principal-сторона (Parent):
ctx.From<Parent>(b => b
    .HasMany(p => p.Children, c => c.ParentId));

// Dependent-сторона (Child):
ctx.From<Child>(b => b
    .HasOne(c => c.Parent, c => c.ParentId));
```

- `HasMany` — навигация-коллекция (`ICollection<TChild>`), FK на `TChild`.
- `HasOne` — навигация-ссылка (`TParent?`), FK на **объявляющем** типе.
- O2M/M2O — обе стороны обязательны для полной модели; инверсия **не выводится**.
  `Inverse` заполняется, когда обе стороны объявлены и сходятся по FK и типам; иначе `null`.
- Fallback для сущности **без навигационного свойства**: перегрузка принимает FK/principal-ключ
  селекторами (симметрично `LoadWith`), навигация при этом не заводится.

### 3.3. Атрибут

Парный атрибут на навигационном свойстве, обёртка над той же моделью:

```csharp
public sealed class RelationshipAttribute : Attribute
{
    public string ForeignKey { get; set; }   // имя FK-свойства на dependent-стороне
    // отношение HasMany/HasOne выводится из типа навигации (коллекция vs ссылка)
}

public sealed class Parent
{
    public int Id { get; set; }
    [Relationship(ForeignKey = nameof(Child.ParentId))]
    public ICollection<Child> Children { get; } = new List<Child>();
}
```

Атрибут и fluent не комбинируются на одном свойстве: побеждает fluent, атрибут игнорируется
(регистрируется один раз на тип; см. §3.5).

### 3.4. Навигации и маппинг колонок

Навигационное свойство исключается из маппинга колонок **только если участвует в объявленной
связи**. Всё остальное `AutoBuildProperties` мапит как раньше — нулевой риск регрессии для
существующих сущностей. Конвенция «`ICollection<T>`/entity-тип ⇒ навигация» **не применяется**.

Правило: множественное `Properties` не содержит навигаций; при объявлении связи ядро валидирует, что
навигационный `PropertyInfo` не попал в `Properties` (иначе `InvalidOperationException`).

### 3.5. Регистрация и жизненный цикл

- Связи регистрируются там же, где метаданные типа: `From<T>(b => ...)`.
- Первая регистрация на тип побеждает (как `HasQueryFilter`/маппинг): повторная конфигурация того же
  типа игнорируется.
- Для `JoinInto`/неявных join'ов метаданные **обеих** сторон должны быть разрешены; неразрешённая
  связь — `NotSupportedException` (§6), без обращения к рефлексии в момент запроса.
- Кэш метаданных — существующий (`DataContextCache.Metadata`); участия связей в ключе плана — см. §5.

## 4. `JoinInto` (слайс B)

### 4.1. API

```csharp
// LEFT по умолчанию
public EntityBuilder<TEntity> JoinInto<TChild>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection);

// Явный тип соединения
public EntityBuilder<TEntity> JoinInto<TChild>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    JoinType joinType);                      // допустимы Inner и Left; иное — NotSupportedException

// Fallback без метаданных связи — явные ключи (симметрично LoadWith)
public EntityBuilder<TEntity> JoinInto<TChild, TKey>(
    EntityBuilder<TChild> child,
    Expression<Func<TEntity, TChild, bool>> predicate,
    Expression<Func<TEntity, ICollection<TChild>>> collection,
    Expression<Func<TEntity, TKey>> parentKey,
    Expression<Func<TChild, TKey>> childKey);
```

Возвращает **копию** билдера (как `LoadWith`), исходный не мутируется.

### 4.2. SQL-форма и материализация

Один round trip:

```sql
select t1.<parent cols>, t2.<child cols>
from Parent as t1
left join Child as t2 on <predicate>
```

Строки материализуются в in-memory пару `(Parent, Child?)`; затем:

1. Родители дедуплицируются по ключу (значения `IsKey`-колонки; fallback — `parentKey`).
2. Дети группируются по FK-значению (fallback — `childKey`).
3. Дочерняя коллекция присваивается родителю по правилам присваивания из #95
   (`EagerLoadSpec.Assign`): non-null — clear+refill; null + settable — свежий список;
   null + read-only — `NotSupportedException`.
4. **Порядок родителей** — порядок первого появления строки в результате запроса;
   **порядок детей** — порядок строк; добавь `OrderBy` при необходимости.

`JoinInto` реализуется **переиспользованием** in-memory группировки/присваивания из
`EntityBuilderEagerLoading.cs`; батч-путь (#95) не затрагивается.

### 4.3. Семантика

- **`LEFT` по умолчанию**: родитель без детей сохраняется с пустой коллекцией. `Inner` доступен
  явной перегрузкой.
- **`Where` на join-билдере** фильтрует строки. Для `LEFT` не-null-safe предикат может выбросить
  родителя целиком (известная семантика SQL `LEFT JOIN ... WHERE`); для `Inner` — обычный фильтр.
  Задокументировать.
- **Paging** (`Take`/`Page`/`Limit`/`Offset`) ограничивает **родителей**, не денормализованные строки:
  паренты выбираются в подзапросе с лимитом, `LEFT JOIN` оборачивается вокруг. In-memory —
  лимит применяется к списку дедуплицированных родителей.
- **Несколько коллекций**: каждый `JoinInto` добавляет свой `LEFT JOIN`; две коллекции у одного
  родителя дают декартово произведение строк — **ограничение**, документируется; дедуп по ключу
  родителя и независимая группировка каждой коллекции сохраняют корректность.

### 4.4. Идентичность родителя и ключи

- Ключ родителя — из `IPropertyMetadata.IsKey` (скалярный, одна колонка).
- FK — из метаданных связи dependent-стороны.
- Fallback-перегрузка принимает явные `parentKey`/`childKey` (обе части — из #95).
- **Составной ключ** (несколько `IsKey`-колонок или несколько FK-колонок) — `NotSupportedException`.

### 4.5. Ключ плана

Ключ плана команды обязан включать идентичность модификатора `JoinInto`:

- тип связи (principal/dependent) и `RelationshipKind`;
- FK-члены и principal-key-члены;
- навигационный/collection-член;
- тип join (`Inner`/`Left`).

Две связи с одинаковой формой SQL, но разным `collection`/FK, не должны разделять запись кэша.
Проверка — тест на кэш (две разные связи не коллидируют; повтор даёт hit).

### 4.6. Терминалы

- `JoinInto` — **часть запроса**: `JOIN` присутствует в `ToCommand()` и SQL; ключ плана — см. §4.5.
- Стичинг/дедуп — пост-шаг **только list-терминалов** (`ToList`/`ToListAsync`), как у `LoadWith`.
  `ToCommand()` возвращает денормализованные строки `(parent, child)`; прочие терминалы (`First`,
  `Count`, …) коллекции не заполняют — задокументировать.

### 4.7. In-memory паритет

`JoinInto` обязан работать на in-memory контексте: соединение и группировка выполняются делегатами
(`InMemoryJoin` + группировка #95). Поведение (лево-внешняя семантика, дедуп, порядок, пустые
коллекции) совпадает с SQL-провайдерами; проверяется `CommonTestSuite`.

### 4.8. Провайдеры

Рендеринг — нативный на PostgreSQL / SQL Server / MySQL / MariaDB / ClickHouse / SQLite через
существующий механизм `LeftJoin`/`Join`. `RightJoin`/`FullJoin`/`APPLY` для `JoinInto` не
предусмотрены (только `Left`/`Inner`).

## 5. Совместимость и обратная совместимость

- `IEntityMetadata.Relationships` и `IPropertyMetadata`-виды — default-реализации, внешние
  реализации интерфейсов продолжают компилироваться.
- Конвенции навигаций не вводятся; существующие сущности без объявленных связей мапятся как раньше.
- `LoadWith` и явные `Join` не меняются.
- Публичный API расширяется аддитивно; имена фиксируются до реализации, XML-doc обязателен
  (`CS1591` не подавлен).

## 6. `NotSupportedException` (точный список слайса B)

- `JoinInto` по связи `ManyToMany` или `OneToOne`.
- Связь не объявлена в метаданных (навигация не распознана) — без тихой клиентской оценки.
- Составной principal-ключ или составной FK (в модели допустимы).
- Несовпадение типов FK/principal-key и `TKey` fallback-перегрузки.
- Collection-член не присваиваем и в текущем значении `null` (унаследовано из #95).
- `joinType`, отличный от `Inner`/`Left`.
- `joinType`, не поддержанный диалектом (защита на будущее; сегодня оба поддержаны всеми).
- `JoinInto` после `As`/над derived-источником — до отдельного решения.

## 7. Приёмка (матрица «функция × провайдер × форма»)

| Функция | SQL (PG/MSSQL/MySQL/MariaDB/CH/SQLite) | In-memory |
|---|---|---|
| `HasMany`/`HasOne` (метаданные, все виды) | — (метаданные) | — |
| Навигация не мапится в колонку | SQL-generation тест схемы SELECT | unit |
| `JoinInto` LEFT, одна коллекция | SQL-generation + `CommonTestSuite` | `InMemoryTests` |
| `JoinInto` INNER | SQL-generation + `CommonTestSuite` | `InMemoryTests` |
| `Where` на join-билдере | SQL-generation + `CommonTestSuite` | `InMemoryTests` |
| Paging парентов | SQL-generation + `CommonTestSuite` | `InMemoryTests` |
| Несколько коллекций (cartesian) | SQL-generation + `CommonTestSuite` | `InMemoryTests` |
| Fallback с явными ключами | SQL-generation | `InMemoryTests` |
| Ключ плана | план-кэш тест (core) | план-кэш тест |
| Отклонения (`NotSupportedException`) | unit | unit |

Тесты: core `nextorm.core.tests` (метаданные, in-memory, план-кэш); провайдерные
`tests/nextorm.<p>.tests/SqlGenerationTests.cs`; `tests/nextorm.integration.tests/CommonTestSuite.*`
(общий кейс на все провайдеры). Порог покрытия — `MIN_LINE_COVERAGE` (см. `AGENTS.md`).

## 8. Документация

- `docs/advanced/limitations.md` + `docs/ru/advanced/limitations.md` — обновить статус навигаций и
  ограничения `JoinInto` (одна коллекция на запрос без cartesian, только `Left`/`Inner`, составные
  ключи и M2M/O2O отклоняются, `ToCommand` даёт денормализованные строки).
- Новый гид `docs/advanced/relationships.md` (+ RU) — объявление связей и `JoinInto`.
- `docs/advanced/eager-loading.md` (+ RU) — перекрёстная ссылка `LoadWith` ↔ `JoinInto`.
- `docs/guide/02-joins.md` (+ RU) — ссылка на `JoinInto`.
- `docs/specs/roadmap/sql-capabilities-gap-analysis.md` — workstream 13: статус и ссылка на спеку.
- Спеки — внутренние: **не** линковать из публичных доков/readme.

## 9. Разбиение на PR

1. **PR1 (слайс A)** — модель метаданных связей, fluent + атрибут, исключение навигаций из колонок,
   `IEntityMetadata.Relationships`, тесты метаданных. Поведение запросов не меняется.
2. **PR2 (слайс B1)** — `JoinInto` LEFT, дедуп/группировка, in-memory, SQL на всех провайдерах,
   участие в ключе плана, тесты.
3. **PR3 (слайс B2)** — INNER, `Where`, paging парентов, несколько коллекций, docs EN+RU,
   `limitations.md`, обновление gap-анализа.

Слайс C (неявные соединения) — отдельная спека и цикл.

## 10. Открытые пункты (решаются при реализации, модель их допускает)

- Точная форма junction-дескриптора M2M (внутренняя, не публичная до реализации M2M).
- Валидация уникальности FK для `OneToOne` (по умолчанию — доверие объявлению).
- Поведение `JoinInto` над `As`/derived-источником — отклонить сейчас, пересмотреть при спросе.

## Deferred / follow-ups

Отложено после слайсов A+B (CHECK пройден); кандидаты на отдельные циклы/issue.

**Ограничения контракта (слайс B, приняты сознательно).**

- Смешивание обычного `Join`/`LeftJoin` с `JoinInto` в одном запросе отклоняется (`NotSupportedException`).
- `.Select(...)` / `.As(...)` поверх `JoinInto` отклоняются.
- Стичинг/дедуп выполняют только list-терминалы (`ToList`/`ToListAsync`); прочие терминалы коллекции не заполняют.

**Latent P2.**

- `SelectExpressionPlanEqualityComparer.cs` / `RowMapperFactory.cs` сворачивают проекцию только по
  `EntityType`+`Slot`, без `Member`. Сейчас безопасно по инварианту `Member == Item{slot+1}`;
  пересмотреть, если инвариант изменится.

**P2.**

- `IdentitySelectorCache` (`JoinIntoSpec.cs`) не очищается в `DataContextCache.Clear()` — оценить
  время жизни и рост.

**ℹ️ Test-quality debt.**

- Остающиеся проверки через hash-inequality / отсутствие исполнения (слабее plan-equality).
- Расширение видимости `EagerLoadSpec.Assign` может быть неиспользуемым.
