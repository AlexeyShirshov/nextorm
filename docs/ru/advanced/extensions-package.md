# Пакет расширений (`nextorm.extensions`)

> Отдельный продуктовый пакет, который добавляет два семейства операторов-компонентов запроса — выбор экстремальной строки (`SelectWhereMin` / `SelectWhereMax`) и группировку, выведенную из проекции (`SelectGroup` / `SelectGroupHaving`), — построенных целиком на публичном слое компонентов запроса, рядом с замороженными встроенными `SelectWhereMin` / `SelectWhereMax`.

**Предварительные требования:** [Группировка и агрегаты](../guide/05-grouping-and-aggregates.md) (включая [выбор экстремальной строки](../guide/05-grouping-and-aggregates.md#выбор-экстремальной-строки-selectwheremax--selectwheremin)) · [Расширяемость: пользовательские SQL-диалекты и рендереры](extensibility.md) · [Краткий справочник API](api-reference.md)

## Обзор

`nextorm.extensions` — самостоятельный NuGet-пакет. Он ссылается только на `nextorm.core`: без пакета провайдера, без `InternalsVisibleTo`, без сырого SQL и без глобальной регистрации обработчиков. Всё, что он делает, идёт через публичную поверхность компонентов запроса ([`IQueryComponent`](xref:NextORM.Core.IQueryComponent) и [`EntityBuilder<TEntity>.WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent))).

Пакет предоставляет два семейства обычных методов расширения над построителем сущности и связку компонентов, которую они присоединяют: фильтры экстремальной строки `SelectWhereMin` / `SelectWhereMax` и проекции с выведенной группировкой `SelectGroup` / `SelectGroupHaving`. Первая пара — **фильтр строк, а не агрегат**: выжившая строка сохраняет все колонки, ровно как у встроенного оператора. Вторая пара устроена противоположным образом — это агрегатная проекция, ключи `GROUP BY` которой выводятся из самой проекции, поэтому явный вызов `GroupBy` не нужен.

Встроенные [`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMinOld``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) / [`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMaxOld``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) этот пакет **не меняет, не оборачивает и не переписывает**. Они остаются путём совместимости и базой сравнения производительности; пакет расширений — независимая альтернатива на публичном слое, которая строит собственную форму запроса.

## Установка

```sh
dotnet add package nextorm.extensions
```

Пакет подтягивает `nextorm.core`. Пакет провайдера для вашей базы добавляется как обычно (`nextorm.sqlite`, `nextorm.postgres`, …); методы расширения работают на любом провайдере, который поддерживает ядро.

## Выбор экстремальной строки (`SelectWhereMin` / `SelectWhereMax`)

### API

Все четыре перегрузки живут в `NextORM.Extensions.ExtremeRowExtensions`:

| Перегрузка | Возвращает | Назначение |
|---|---|---|
| `SelectWhereMin(source, valueSelector, ties = One, groupBy = null)` | `EntityBuilder<TEntity>` | Оставить строку(и) с наименьшим значением; продолжить построение запроса. |
| `SelectWhereMax(source, valueSelector, ties = One, groupBy = null)` | `EntityBuilder<TEntity>` | Оставить строку(и) с наибольшим значением; продолжить построение запроса. |
| `SelectWhereMin(source, valueSelector, projection, ties = One, groupBy = null)` | `QueryCommand<TResult>` | Тот же выбор с проекцией, применённой к каждой выжившей строке. |
| `SelectWhereMax(source, valueSelector, projection, ties = One, groupBy = null)` | `QueryCommand<TResult>` | Тот же выбор с проекцией, применённой к каждой выжившей строке. |

```csharp
using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

public static class ExtremeRowExtensions
{
    public static EntityBuilder<TEntity> SelectWhereMin<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);

    public static EntityBuilder<TEntity> SelectWhereMax<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);

    public static QueryCommand<TResult> SelectWhereMin<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);

    public static QueryCommand<TResult> SelectWhereMax<TEntity, TValue, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        Expression<Func<TEntity, TResult>> projection,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null);
}
```

Перегрузки на всю сущность возвращают построитель, поэтому после них можно вызвать `.Select(...)`, `.Distinct()` и внешний `.OrderBy(...)`. Перегрузки с проекцией возвращают типизированную команду, которую выполняют её терминальные методы (`ToList`, `First`, …).

### `One` против `All`

[`ExtremeRowTies`](xref:NextORM.Core.ExtremeRowTies) управляет кратностью выборки и передаётся как есть:

- [`ExtremeRowTies.One`](xref:NextORM.Core.ExtremeRowTies.One) (значение по умолчанию) оставляет ровно **одну** строку на партицию — одну глобально или одну на группу. Какая именно строка выигрывает при равенстве значений — не определено, ровно как у встроенного оператора; API для тай-брейка нет.
- [`ExtremeRowTies.All`](xref:NextORM.Core.ExtremeRowTies.All) оставляет **все** строки, разделяющие экстремальное значение, — все глобально или все в каждой группе.

Равенство определяется только селектором: `All` возвращает полный набор связанных строк, `One` — одного произвольного его представителя.

### Группировка и составные ключи

Без `groupBy` экстремум берётся по всему источнику. С `groupBy` экстремальная строка(и) выбирается внутри каждой отдельной группы; `null`-ключ группировки образует свою группу.

Составной ключ экстремума записывается анонимным типом (или кортежем) и сравнивается **лексикографически**, компонент за компонентом: `(9, 2)` больше `(8, 100)`. Составной ключ группировки партиционирует по кортежу целиком. Одна и та же декомпозиция работает для обеих политик кратности:

```csharp
// По одной строке на (TeamId, Season) с наибольшим (Score, Assists).
var best = dataContext.From<Player>()
    .SelectWhereMax(p => new { p.Score, p.Assists }, ExtremeRowTies.One, p => new { p.TeamId, p.Season })
    .ToList();
```

### Поведение с `NULL`

Контракт совпадает со встроенным оператором:

- строка, у которой компонент ключа экстремума равен `null`, никогда не выигрывает экстремум и не попадает в результат;
- группа, все значения ключа которой равны `null`, не даёт строк вообще, а глобальная форма возвращает пусто, когда все значения равны `null`;
- nullable-ключи **группировки** сохраняются; `null`-ключ группы образует отдельную группу. Выборка `All` по nullable-ключу группировки использует секционированное ранжирующее окно вместо агрегатно-соединительной формы, поэтому null-безопасный предикат соединения не нужен и такие строки сохраняются на любом провайдере, включая ClickHouse. Ключ экстремума с плавающей точкой использует то же секционированное ранжирующее окно, потому что на ClickHouse агрегат и сортировка расходятся в трактовке `NaN`;
- nullable-колонки payload сохраняются без изменений.

### Проекции, сортировка и композиция

- **Входной `Where`** до оператора сохраняется и объединяется с выбором (включая захваченные и позиционные параметры).
- **Проекции** — строки сущности, скаляр, DTO или анонимный тип — работают и через перегрузки с проекцией, и через `.Select(...)` после перегрузок на всю сущность; проекция формирует только результат и никогда не участвует в выборе победителя.
- **Внешняя сортировка** — `.OrderBy(...)` / `.ThenBy(...)` сортирует выжившие строки и никогда не участвует в выборе победителя.
- **`Distinct`** — принимается так же, как на встроенном пути; дубликаты спроецированного кортежа удаляет общий построитель после перестройки запроса.
- **Несколько компонентов** — независимый компонент (например, дополнительный фильтр) композируется. Оператор экстремальной строки занимает один стабильный слот: повторный `SelectWhereMin`/`SelectWhereMax` на том же построителе **заменяет** его на месте (`Min → Max`, `Max → Min`, без вложенности), а чужой компонент, претендующий на тот же эксклюзивный слот перестройки, отвергается при подготовке.
- **Имена скрытых колонок** — внутренняя служебная колонка компонента получает приватный алиас (`__nextorm_ext_rn` и суффиксы), выбранный так, чтобы не совпасть с пользовательской колонкой того же имени (`rn`, `rank`, …). Алиас никогда не попадает в результат.

### Границы отказа

Компонент отвергает те же формы, что и встроенный оператор, — при подготовке/построении, а не молча выдаёт другой ответ:

| Отвергается | Примечание |
|---|---|
| постраничный вывод (`Skip` / `Take` / `Page` / `Limit`) | Перестройка оборачивает источник в производную таблицу; пейджинг отвергается. |
| соединения, `GroupBy`, `Having`, именованные окна, оконные функции в списке выборки | Компонент сам ранжирует строки источника. |
| CTE, операции над множествами (`Union` / `Concat` / `Intersect` / `Except`) | Не выражаются поверх перестройки. |
| `DistinctOn`, `LimitBy`, `ArrayJoin`, `PreWhere` | Провайдерные модификаторы формы строк. |
| `TABLESAMPLE`, `FOR SYSTEM_TIME`, блокировки строк, хинты запроса/таблиц/индексов/области таблиц | Модификаторы уровня инструкции. |
| `FINAL`, `SAMPLE`, `SETTINGS`, `FOR JSON` / `FOR XML` | Модификаторы строк/инструкции. |
| агрегаты (`min`, `max`, `sum`, `avg`, `count`, …) в списке выборки | Скрытая колонка ранжирования агрегировалась бы по перестроенным строкам; отвергается явно. |
| провайдер in-memory | `NotSupportedException`; используйте встроенный оператор или реальную базу. |

Перегрузки на всю сущность требуют **смапленного источника сущности** (`From<TEntity>()`); у источника, адресуемого по имени, нет смапленных колонок для материализации, поэтому он отвергается так же, как у встроенного оператора.

### Связь со встроенным оператором

`SelectWhereMin` / `SelectWhereMax` реализуют тот же наблюдаемый контракт, что `SelectWhereMin` / `SelectWhereMax` — `One`/`All`, скалярные/составные ключи, глобально/по группам, `NULL`-ключи, входной `Where`, проекции, внешняя сортировка и перечисленные границы отказа, — и проверяются против них парным исполнением на одной и той же базе. Это **не** внутренний алиас: они присоединяют публичный компонент запроса, порождают собственную форму SQL и не делят код с замороженной реализацией `ExtremeRow*`.

Встроенный оператор остаётся значением по умолчанию и базой сравнения. Компонент расширения строит свой запрос один раз на план (на промахе кэша; подтверждённое попадание в прогретый кэш переиспользует план без перестроения), поэтому оба оператора можно сравнивать и использовать в одном приложении, не меняя поведение ни одного из них.

## Группировка, выведенная из проекции (`SelectGroup` / `SelectGroupHaving`)

`SelectGroup` и `SelectGroupHaving` поставляются тем же пакетом. Они принимают ту же лямбду проекции, что и `Select`, но вместо явного `GroupBy` выводят ключи группировки из самой проекции: каждое не содержащее агрегата выражение, зависящее от текущей строки, становится ключом, а агрегатные вызовы сворачивают каждую группу. Проекция сохраняет ровно свою форму — нет ни клиентской группировки, ни динамического типа, ни reflection-emit слияния плоского типа результата. Полные правила вывода и квартальный пример — в разделе [Группировка и агрегаты](../guide/05-grouping-and-aggregates.md#группировка-выведенная-из-проекции-selectgroup).

### API

Оба метода живут в `NextORM.Extensions.SelectGroupExtensions`:

| Метод | Возвращает | Назначение |
|---|---|---|
| `SelectGroup(source, projection)` | `QueryCommand<TResult>` | Вывести ключи группировки из `projection` и спроецировать каждую группу. |
| `SelectGroupHaving(source, projection, condition)` | `QueryCommand<TResult>` | Тот же вывод ключей, `condition` добавляется в эффективный `HAVING` через `AND`. |

```csharp
using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.Extensions;

public static class SelectGroupExtensions
{
    public static QueryCommand<TResult> SelectGroup<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection);

    public static QueryCommand<TResult> SelectGroupHaving<TEntity, TResult>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TResult>> projection,
        Expression<Func<TResult, bool>> condition);
}
```

Оба метода возвращают типизированную команду, которую выполняют её терминальные методы (`ToList`, `First`, …), поэтому `Where`, `Having` и внешний `OrderBy`, скомпонованные до терминального вызова, продолжают действовать. Проекция типизирована по исходной сущности: указывайте оба generic-аргумента (`.SelectGroup<TEntity, TResult>(...)`) или опускайте их.

### Три исхода

| Проекция | SQL | Результат |
|---|---|---|
| ключи и агрегаты | `GROUP BY` по выведенным ключам | по одной строке на каждую различную комбинацию ключей |
| только агрегаты, без ключей | глобальная агрегация, без `GROUP BY` | ровно одна строка с существующим поведением провайдера на пустом входе для `count`, `sum` и `NULL` |
| ни ключей, ни агрегатов | поведение как у `DISTINCT` | каждое различное значение один раз; пустой источник даёт ноль строк |

`SelectGroupHaving(projection, condition)` использует тот же вывод ключей и добавляет `condition` в эффективный `HAVING` через `AND`. `condition` типизирован по результату проекции (`TResult`), а не по исходной сущности: каждый член результата перед валидацией заменяется выражением проекции, которое его производит, поэтому `r => r.Rows > 1` — это тот же `HAVING`, что и нижележащее `count(...) > 1`, а при скалярной проекции параметр условия — само спроецированное значение (`f => f > 5`). Ключи выводятся **только** из `projection`, а существующий `Having` источника объединяется с `condition` через `AND` до валидации. Член, который проекция не связывает, или результат целиком бросает `QueryPreparationException` до генерации любого SQL.

### Валидация и границы отказа

`Having` и expression-`OrderBy` валидируются против выведенных ключей: каждое упомянутое в них выражение строки должно быть покрыто выведенным ключом, быть агрегатом либо захваченным/константным значением. Непокрытый член строки бросает `QueryPreparationException` до того, как команда дойдёт до базы данных, — группировка никогда не расширяется молча, а функциональные зависимости не выводятся. `Where` остаётся pre-group-фильтром и не участвует в выводе.

Следующие формы отклоняются с `QueryPreparationException`:

| Что отклоняется | Примечание |
|---|---|
| уже существующий явный `GroupBy`, `GroupByRollup`, `GroupByCube`, `GroupByGroupingSets`, `WithTotals` | Два механизма группировки никогда не сливаются и не переопределяют друг друга. |
| window-функции в проекции, `Having` или expression-`OrderBy` | Не выразимы внутри выведенной группировки. |
| скалярные или коррелированные подзапросы в этих выражениях | Включая аргументы агрегатов и `FILTER`-предикаты; подзапрос в `Where` по-прежнему поддерживается. |
| провайдер in-memory | `NotSupportedException`; выведенной группировке нужна реальная база данных. |

### Связь с явным `GroupBy`

`GroupBy(...).Select(...)` с теми же ключами и агрегатами даёт тот же SQL и те же строки; разница только в том, кто пишет список ключей. Оператор — обычный компонент запроса на публичном контракте [`IQueryComponent`](xref:NextORM.Core.IQueryComponent), и именно это позволяет стороннему пакету добавить собственный структурный оператор без изменений в ядре — см. [Расширяемость](extensibility.md#расширение-построения-запросов-компонентами-запроса).

## Полный пример

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

[SqlTable("players")]
public sealed class Player
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("team_id")]
    public int TeamId { get; set; }

    [Column("season")]
    public int Season { get; set; }

    [Column("score")]
    public int Score { get; set; }

    [Column("assists")]
    public int? Assists { get; set; }
}

using var db = new SqliteDataContext("Data Source=app.db", new DataContextBuilder());

// Строки сущности: глобальный лучший по очкам (одна строка; при равенстве выбирает провайдер,
// как и у встроенного оператора).
var top = db.From<Player>()
    .Where(p => p.Season == 2026)
    .SelectWhereMax(p => p.Score)
    .OrderByDescending(p => p.Score)
    .ToList();

// Перегрузка с проекцией: все строки, разделившие лучший (Score, Assists) в каждом
// (TeamId, Season), спроецированные на нужные колонки.
var captains = db.From<Player>()
    .SelectWhereMax(
        p => new { p.Score, p.Assists },
        p => new { p.Id, p.TeamId, p.Season, p.Score },
        ExtremeRowTies.All,
        p => new { p.TeamId, p.Season })
    .OrderBy(p => p.TeamId)
    .ThenBy(p => p.Season)
    .ToList();

// Форма с построителем на всю сущность композируется с остальным fluent API.
var ids = db.From<Player>()
    .SelectWhereMin(p => p.Score)
    .Select(p => p.Id)
    .Distinct()
    .ToList();

// Выведенная группировка: без явного GroupBy — зависящие от строки части проекции становятся
// ключами, а агрегатные вызовы сворачивают каждую группу.
var byTeam = db.From<Player>()
    .Where(p => p.Season == 2026)
    .SelectGroup(p => new { p.TeamId, TotalScore = SqlFunctions.Sql.sum(p.Score) })
    .ToList();
```

## См. также

- [Выбор экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)](../guide/05-grouping-and-aggregates.md#выбор-экстремальной-строки-selectwheremax--selectwheremin) — общий контракт результата
- [Группировка и агрегаты](../guide/05-grouping-and-aggregates.md) — правила вывода `SelectGroup` / `SelectGroupHaving`
- [Нативные стратегии выбора экстремальной строки](select-where-extrema-native.md) — как встроенный оператор рендерит `One` нативно у некоторых провайдеров
- [Расширяемость: пользовательские SQL-диалекты и рендереры](extensibility.md)
- [Краткий справочник API](api-reference.md)
