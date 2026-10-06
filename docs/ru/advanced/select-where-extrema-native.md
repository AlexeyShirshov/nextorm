# Нативные стратегии выбора экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)

> Для запроса `SelectWhereMax`/`SelectWhereMin` с `ExtremeRowTies.One` PostgreSQL и ClickHouse рендерят нативную SQL-стратегию вместо переносимого понижения до оконной функции — автоматически, по провайдеру и форме запроса, без opt-in и без переключателя на экземпляре.

**Предварительные требования:** [Выбор экстремальной строки](../guide/07-distinct.md) · [Обзор провайдеров](../providers/overview.md) · [Краткий справочник API](api-reference.md)

## Обзор

[`SelectWhereMax`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMax``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) и
[`SelectWhereMin`](xref:NextORM.Core.EntityBuilder`1.SelectWhereMin``1(System.Linq.Expressions.Expression{System.Func{`0,``0}},NextORM.Core.ExtremeRowTies,System.Linq.Expressions.Expression{System.Func{`0,System.Object}})) оставляют строку, значение которой под селектором наибольшее или наименьшее в источнике — глобально или по группам. Это фильтр строк, а не агрегат: выжившая строка сохраняет все колонки. Семантика не изменилась относительно переносимой реализации — полный контракт см. в разделе [Выбор экстремальной строки](../guide/07-distinct.md).

Эта страница описывает *то, как* запрос с `One` доходит до базы. Выбор стратегии автоматический и детерминированный **по провайдеру и форме/типу запроса**:

- на PostgreSQL нативно подходящие ключи и ключи группировки рендерят `DISTINCT ON` (сгруппированная форма) или внутренний `ORDER BY ... LIMIT 1` (глобальная форма);
- на ClickHouse нативно подходящие ключ, группа и payload рендерят один агрегат `argMin`/`argMax`;
- любая другая комбинация сохраняет переносимое понижение до оконной функции.

Opt-in нет, переключателя на экземпляре или на запрос нет, повтор при ошибке не делается: если диалект принял запрос, он его рендерит, а ошибка рендеринга всплывает наружу, а не приводит к молчаливому повтору переносимым путём. Форма `ExtremeRowTies.All` нативно не рендерится никогда.

## Использование

Публичный API не меняется; SQL выбирает провайдер.

```csharp
// Глобально: единственная строка с наибольшим Score.
var top = dataContext.From<Player>()
    .SelectWhereMax(p => p.Score)
    .ToList();

// По группам: единственная строка с наименьшим Score в каждом TeamId.
// Селектор группы композитный; голый p => p.TeamId остался бы на переносимом пути.
var perTeam = dataContext.From<Player>()
    .SelectWhereMin(p => p.Score, ExtremeRowTies.One, p => new { p.TeamId })
    .ToList();
```

## Нативная или переносимая

| Провайдер | Форма запроса | Стратегия |
|---|---|---|
| PostgreSQL | сгруппированная `One` | один `DISTINCT ON (<колонки группы>) ... ORDER BY <колонки группы>, <ключ экстремума> ASC/DESC` |
| PostgreSQL | глобальная `One` | внутренний `ORDER BY <ключ экстремума> ASC/DESC LIMIT 1` |
| ClickHouse | глобальная `One` | один `argMin`/`argMax(tuple(<колонки payload>), <ключ экстремума>)` плюс `HAVING count() > 0`, чтобы подавить строку по умолчанию при пустом вводе |
| ClickHouse | сгруппированная `One` | один `argMin`/`argMax(tuple(<колонки payload>), <ключ экстремума>)` рядом с колонками группы и `GROUP BY` |
| Любой другой провайдер или неподходящий запрос | любая | переносимое понижение до оконной функции |

Сгруппированная форма PostgreSQL оставляет первую строку группы при заданном порядке, поэтому порядок «сначала компоненты группы, затем полный ключ экстремума» выбирает экстремальную строку в каждой группе. Агрегат ClickHouse выбирает целый кортеж payload и проецирует его элементы обратно в канонические алиасы payload. В обоих случаях общий построитель затем применяет внешнюю проекцию, `DISTINCT` и пользовательскую сортировку вывода.

Для примера с `Player` выше (`players(id, score, team_id, name)`) два вызова рендерятся так.

```sql
-- PostgreSQL, глобально (SelectWhereMax): внутренний ORDER BY ... LIMIT 1
select t1.id, t1.score, t1.team_id as "TeamId", t1.name
from (select * from players
 where score is not null order by "score" desc limit 1) as "t1"

-- PostgreSQL, по группам (SelectWhereMin по new { TeamId }): DISTINCT ON по колонкам группы
select t1.id, t1.score, t1.team_id as "TeamId", t1.name
from (select distinct on ("team_id") * from (select * from players
 where score is not null) __nextorm_extreme order by "team_id", "score") as "t1"
```

```sql
-- ClickHouse, глобально (SelectWhereMax): один агрегат argMax над кортежем payload
select t1.id, t1.score, t1.team_id as `TeamId`, t1.name
from (select tupleElement(`__nextorm_extreme_tuple`, 1) as `id`, tupleElement(`__nextorm_extreme_tuple`, 2) as `score`, tupleElement(`__nextorm_extreme_tuple`, 3) as `team_id`, tupleElement(`__nextorm_extreme_tuple`, 4) as `name`
from (
 select argMax(tuple(`id`, `score`, `team_id`, `name`), `score`) as `__nextorm_extreme_tuple`
 from (select * from players
 where score is not null) as `__nextorm_extreme_src`
 having count() > 0)) as `t1`

-- ClickHouse, по группам (SelectWhereMin по new { TeamId }): агрегат рядом с колонками группы и GROUP BY
select t1.id, t1.score, t1.team_id as `TeamId`, t1.name
from (select `team_id`, tupleElement(`__nextorm_extreme_tuple`, 1) as `id`, tupleElement(`__nextorm_extreme_tuple`, 2) as `score`, tupleElement(`__nextorm_extreme_tuple`, 4) as `name`
from (
 select `team_id`, argMin(tuple(`id`, `score`, `team_id`, `name`), `score`) as `__nextorm_extreme_tuple`
 from (select * from players
 where score is not null) as `__nextorm_extreme_src`
 group by `team_id`)) as `t1`
```

## Какие запросы подходят

Условия нарочно узкие, и решение принимается по подготовленному описанию команды до построения любого SQL, алиаса или параметра.

| Часть | Нативная пригодность |
|---|---|
| Ключ экстремума | прямая отображённая колонка, привязанная к `short`/`int`/`long` или `float`/`double` (включая nullable), или композит из таких компонентов; композит с плавающим компонентом ограничен тремя компонентами, а чисто целочисленный композит не имеет ограничения по арности; компонент с плавающей точкой адаптируется NaN-безопасно |
| Ключ группировки | те же целочисленные типы, что и у ключа экстремума (`short`/`int`/`long`, включая nullable), или композит из таких любой арности; ключ группировки с плавающей точкой сохраняет переносимый путь |
| Payload ClickHouse | целочисленные типы (`sbyte`/`byte`/`short`/`ushort`/`int`/`uint`/`long`/`ulong`, включая nullable), типы с плавающей точкой (`float`/`double`, включая nullable) и `string` |
| Всё остальное | переносимое понижение до оконной функции |

Композитный ключ сравнивается лексикографически, покомпонентно. Компонент `Float32`/`Float64` ключа экстремума адаптируется в том же запросе, а не передаётся в `argMin`/`argMax` как есть: ведущий ранг `isNaN` оставляет NaN последним в обоих направлениях, совпадая с переносимым контрактом, а компонент `Float32` для сравнения расширяется до `Float64`. Прямой агрегат намеренно не используется, потому что ClickHouse инициализирует его первой строкой, а `x > NaN`/`x < NaN` оба ложны, поэтому ведущий NaN иначе победил бы. Адаптация покрывает одно-, двух- и трёхкомпонентные ключи для `Min`/`Max`, глобальной и сгруппированной форм и nullable/non-nullable компонентов. Ограничения по арности намеренно асимметричны: чисто целочисленный ключ экстремума или ключ группировки — это обычный лексикографический кортеж без ограничения по арности, а ограничение в три компонента действует только на ключ, содержащий плавающий компонент, потому что только эта адаптация доказана покомпонентно. Селектор группы должен быть композитным (`e => new { e.TeamId }`); голый одноколоночный селектор группы сохраняет переносимый путь. Всё вне таблицы выше сохраняет переносимый путь с тем же контрактом результата: остальные провайдеры, `ExtremeRowTies.All`, ключи группировки с плавающей точкой, ключи экстремума более чем из трёх компонентов **с плавающим компонентом**, ключи `Float16`/`Decimal`, конвертеры значений, вычисляемые выражения и прочие типы ключей или payload.

## Семантика

Нативные стратегии не меняют наблюдаемый результат:

- **Одна настоящая строка на партицию.** `One` возвращает ровно одну победившую *исходную* строку на группу (одну строку для глобальной формы), и все поля берутся из этой же исходной строки, поэтому чтение сущности целиком и проекция согласованы. Какая именно строка выбрана при равенстве — не определено; API для разрешения ничьих нет.
- **Сортировка вывода — отдельно.** Пользовательский `OrderBy` только сортирует результат. Он применяется вне выбора победителя и никогда не вплетается в порядок выбора победителя.
- **Обработка NULL.** Строка с `NULL`-компонентом ключа экстремума исключается до выбора победителя, а партиция, у которой все компоненты ключа `NULL`, не даёт строк. Nullable-ключи групп и nullable-значения payload сохраняются.
- **Пустой ввод.** Результат — ноль строк, включая глобальную форму ClickHouse, где `HAVING count() > 0` подавляет строку по умолчанию агрегата вместо её возврата.
- **Отклоняемые формы.** Комбинации, запрещённые переносимым путём — соединения, постраничный вывод, `DistinctOn`, `GroupBy`, `Having`, именованные окна, CTE, операции над множествами, `LimitBy`, `ArrayJoin`, `PreWhere` и нефизические источники — по-прежнему отклоняются до диспетчеризации на обоих путях.

## Расширяемость диалектов

Нативная стратегия — это публичная возможность диалекта, а не жёстко зашитая ветка по провайдеру:

- [`ISqlDialect.ExtremeRowRenderer`](xref:NextORM.Core.ISqlDialect.ExtremeRowRenderer) — необязательный рендерер, объявленный default-членом интерфейса, возвращающим `null`; [`SqlDialectBase.ExtremeRowRenderer`](xref:NextORM.Core.SqlDialectBase.ExtremeRowRenderer) переопределяет его виртуально с тем же значением по умолчанию. `null` означает отсутствие нативной стратегии.
- [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) предоставляет `CanRender(`[`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription)`)` — решение без побочных эффектов, принимаемое до сборки любого SQL, — и `Render(`[`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest)`)`, возвращающий источник строки-победителя. Только положительный `CanRender` ведёт к `Render`, и у `Render` нет позднего отката к переносимому пути.
- [`ExtremeRowRenderColumn`](xref:NextORM.Core.ExtremeRowRenderColumn), [`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription) и [`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest) несут только факты формы (CLR-тип, nullable, признаки прямой отображённости и конвертера, а также подготовленный источник и алиасы), но никогда — выражение или контекст построения. См. [Краткий справочник API](api-reference.md).

`PostgresDialect` и `ClickHouseDialect` не `sealed`, поэтому пользовательский диалект может наследоваться от них и переопределять рендерер (например, чтобы изменить условия пригодности), не трогая общий построитель.

## См. также

- [Выбор экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)](../guide/07-distinct.md)
- [Краткий справочник API](api-reference.md)
- [Обзор провайдеров](../providers/overview.md)
