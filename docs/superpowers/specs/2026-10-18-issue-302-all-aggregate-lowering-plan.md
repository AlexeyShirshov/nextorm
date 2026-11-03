# #302 — план реализации: агрегатный `All` и общий query-tree lowering

- **Дата:** 2026-11-02
- **Статус:** **план утверждён USER 2026-11-02** (явное «апрув»). Approval плана ≠ разрешение на
  исполнение: продуктовая реализация не авторизована до отдельного явного разрешения (см. §14).
- **Спека (утверждена USER 2026-10-18):**
  `docs/superpowers/specs/2026-10-18-issue-302-all-aggregate-lowering-design.md`. Этот документ
  реализует её и **не переоткрывает** согласованный scope: SQL-only, in-memory вне области,
  native-пути PostgreSQL/ClickHouse сохраняются, `One` без изменения наблюдаемого SQL.
- **Tracking:** GitHub [#302](https://github.com/AlexeyShirshov/nextorm/issues/302), OPEN,
  milestone `1.0.9-rc5`.
- **Тип документа:** execution-ready план реализации; не спека, не разрешение на код.
- **Git:** commit/push/merge этот документ не авторизует.

> Этот план — ответ на вопрос §5.2 спеки («план обязан выбрать и документировать точную фазу и
> владельца lowering»). Он также фиксирует, **что именно** считается дублирующим конвейером в
> текущем коде и как он устраняется, чтобы требование §2 не превратилось в переименование.

---

## 1. Что уже решено и что решает этот план

Из спеки (не переоткрывается):

1. `All` на всех SQL-провайдерах понижается **агрегатно**, оконное понижение для `All` не
   используется и не является fallback (§5.5.5).
2. `SelectWhereMax/Min` — SQL-side синтаксический сахар; результат собирается **общими**
   примитивами движка (source/projection/filter/alias/`DISTINCT`/sorting/parameters), отдельного
   outer-SELECT конвейера быть не должно (§2, §11.1).
3. `One` сохраняет SQL byte-identical; native `One` PG/CH сохраняется; публичный
   `IExtremeRowRenderer` не меняется (§5.3, §5.6, §11.3).
4. In-memory не трогаем; его suite — только regression check (§7).
5. Публичная документация EN+RU — часть объёма (§12).

Этот план решает: фазу lowering, внутреннее представление lowered-дерева, порядок работ по
провайдерам, способ доказательства (гейты), и что делать, если какая-то часть окажется
невыполнимой без публичного изменения контракта.

---

## 2. Границы плана

**В плане:** конструкция lowered-дерева, устранение дублирующего outer-конвейера, поэтапные
гейты, тест-план, документация, приёмка, риски.

**Не в плане:** изменения in-memory; новые native `All` renderer'ы; новый публичный API;
переписывание общего optimizer'а; `PerParentPaging`; schema/index-стратегии; доработка
raw-shape recipe (#363 закрыт как направление).

---

## 3. Текущее состояние кода (проверено)

Номера строк — на HEAD `89823528`; спека ссылается на более старые ревизии, ориентир — методы.

| Точка | Факт |
|---|---|
| `SqlBuilder.MakeSelect` (`src/nextorm.core/DataContext/SqlBuilder.cs:35`) | Ранний диспатч `if (cmd.ExtremeRow is not null) return MakeExtremeRowSelect(cmd, selectInto);` (`:54-55`) — команда уходит из общего пути до рендеринга клауз. |
| `MakeExtremeRowSelect` (`:1131`) | Сначала `EnsureExtremeRowCompatible`, затем native-ветка **только для `Ties == One`**, иначе `MakePortableExtremeRowSelect`. |
| `MakePortableExtremeRowSelect` (`:1171`) → `MakePortableExtremeRowInner` (`:1185`) | Вручную собирает `select *, row_number()|rank() over (...)` + фильтр (`AppendExtremeRowSourceFilter`, `:1225`). |
| `MakeExtremeRowOuter` (`:1457`) | **Дублирующий outer-SELECT конвейер:** сам пишет `select`/`distinct`, projection (`MakeColumn`), `from (inner) alias`, `where alias.rn = 1`, `order by`, и отдельно `WalkOrderByParameters` для param-mode. |
| `MakeNativeExtremeRowSelect` (`:1264`) | Строит filtered source (`:1300`), вызывает `renderer.Render`, и передаёт строку в тот же `MakeExtremeRowOuter`. |
| `EnsureExtremeRowCompatible` (`:1801`) | Guards: `select into`, `ExtremeRowCompatibility.EnsureModifiersCompatible(cmd)`, и требование «single physical-table source» (`cmd.From.SubQuery is not null` → отказ). |
| `ExtremeRowCompatibility.EnsureModifiersCompatible` (`src/nextorm.core/Query/ExtremeRowCompatibility.cs:32`) | Отклоняет CTE, HAVING, windows, `OVER`, GROUP BY, joins, LIMIT BY, ARRAY JOIN, PREWHERE, paging, DISTINCT ON, set-op, TABLESAMPLE, temporal, row locks, hints, FINAL, SAMPLE, SETTINGS, FOR JSON/XML. **Следствие: пользовательский запрос — одна физическая таблица + условие + проекция + `DISTINCT` + `ORDER BY`.** |
| `QueryPreparer.Prepare` (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs:29`) | Готовит `_extremeRowColumns`/`_extremeRowGroupByColumns` (`:129`, `:158-159`, `PrepareExtremeRow` `:3712`), `PreparedCondition`, `SelectList`, `From`. **`EnsureModifiersCompatible` в подготовке не вызывается** — только в `SqlBuilder` и in-memory. |
| `QueryPlanner` (`src/nextorm.core/DataContext/QueryPlanner.cs:865-867`, `MakeSelectInternal` `:994-1001`) | SQL рендерится **один раз** на plan-miss (`MakeSelect(queryCommand, paramMode:false, …)`); cache-hit SQL не пересобирает. |
| `FromExpression(QueryCommand)` (`src/nextorm.core/Expressions/FromExpression.cs:48`) | Derived-table source. Рендер: `(inner select) alias`, затем `ColumnsProvider.Add(cmd, false)` (`src/nextorm.core/DataContext/SqlSourceRenderer.cs:875-910`). |
| `DefaultColumnsProvider.Add(QueryCommand)` (`src/nextorm.core/Query/DefaultColumnsProvider.cs:89-92`) | Регистрирует `cmd.ResultType`; разрешение параметра при двух источниках одного типа — по индексу параметра в scope (`FindAliasInScope`, `:115-161`). |
| `JoinExpression` (`src/nextorm.core/Expressions/JoinExpression.cs:78`, `:140`) | Публичный конструктор принимает произвольный `LambdaExpression`; `From` — `required FromExpression`. |
| `SqlFunctions.Column<T>(object, string)` (`src/nextorm.core/Query/SqlFunctions.cs:101`) | Рендерится как ссылка на колонку по имени (`NormSqlTranslator.TranslateColumn`, `~:192`). |
| Тесты `All` | `tests/nextorm.<provider>.tests/SqlGeneration/SqlGenerationTests.cs`, `tests/nextorm.integration.tests/CommonTestSuite/Queries/CommonTestSuite.SelectWhereExtrema.cs`, native-тесты PG/CH, in-memory suite. |

**Вывод.** Сегодня `All` исполняется через `rank()`-окно, а outer-часть собирается вторым,
независимым конвейером. Именно это спека требует устранить.

---

## 4. Выбранная архитектура

### 4.1 Фаза и владелец lowering

**Решение плана (в рамках §5.2 спеки).**

- Lowering выполняется **на стадии рендера SQL**, в `SqlBuilder.MakeSelect`, в существующей точке
  диспатча (`:54-55`), **после** `EnsureExtremeRowCompatible` и **после** формирования логического
  ключа команды.
- Владелец: новый внутренний тип `ExtremeRowLowering` (`src/nextorm.core/DataContext/`), вызываемый
  только из `SqlBuilder`. Он не является clause-состоянием `QueryCommand`.
- Исходная keyed-команда **не мутируется**: lowering работает на детачнутых клонах/вариантах,
  построенных из уже подготовленного состояния.
- Логический ключ, equality, clone-путь и raw-shape recipe не меняются: ключ считается по исходной
  команде до рендера, lowered-дерево в ключ не попадает.
- Детерминизм: одно и то же подготовленное состояние даёт структурно идентичное lowered-дерево при
  каждом рендере. Рендер происходит один раз на plan-miss (§3), cache-hit его не повторяет.
- Before-key normalizer не вводится.

### 4.2 Внутреннее представление: detached lowered command tree

Lowering возвращает **`QueryCommand`** (далее `O` — outer lowered command) и рендерит его обычным
`new SqlBuilder(ctx).MakeSelect(O)`. Никакой новой строковой сборки outer-части.

`O` строится из подготовленной пользовательской команды:

- `O = cmd.Clone()` (живой клон prepared-состояния) → `ExtremeRow = null`, замена внутренних
  prepared-полей (`_selectList`, `_groupingList`, `PreparedCondition`, `_joins`, `From`) там, где это
  требует стратегия;
- `O.From` — исходный источник (или derived source, см. 4.3/4.4);
- `O.SelectList`, `O.IsDistinct`, `O.Sorting`, `O.Tag`, `O.Paging` (пусто по guard'у) — как у
  пользовательской команды, без изменений;
- `O.Joins` — только **внутренние** joins, добавленные lowering'ом;
- `O.PreparedCondition` — исходное пользовательское условие (и/или предикат на synthetic column).

Внутренние узлы **не проходят** `ExtremeRowCompatibility` (она вызывается только для
пользовательской команды и только в `MakeExtremeRowSelect`), поэтому внутренние join/group-by
легальны. Набор отказов для пользовательских запросов не меняется (§5.4 спеки).

Проверка инварианта «общий render»: `O` рендерится `MakeSelect`, значит projection, `DISTINCT`,
sorting, alias'ы и parameter traversal — общие. `MakeExtremeRowOuter` удаляется (§4.4, §4.5).

### 4.3 `All`: топология lowering

Обозначения: `T` — физическая таблица; `C` — пользовательское условие (`PreparedCondition`);
`v_1..v_n` — компоненты value key; `g_1..g_m` — компоненты group key (m может быть 0).
`N` — `v_i IS NOT NULL` по каждому компоненту.

**Ключевой приём типизации.** Каждый stage-command — это **тот же `TEntity`** в качестве
`ResultType`, поэтому оба параметра join-условия имеют один CLR-тип и разрешаются существующим
механизмом «параметр → индекс в scope» (`DefaultColumnsProvider`, `:115-161`) — новые CLR-типы
проекций не нужны.

**Ключевой приём именования.** Опираться на совпадение property name и physical column name нельзя
(конвенции именования, case-sensitive PostgreSQL, mapped columns). Поэтому:

- **каждая** выходная колонка stage'а получает **явный alias, выбранный lowering'ом**
  (`__nextorm_ext_<k>_<i>`), — и group/prefix pass-through, и агрегат;
- **каждая** ссылка на выход stage'а с другого уровня идёт через
  `SqlFunctions.Column<T>(param, "<alias>")` (`SqlFunctions.cs:101` → `NormSqlTranslator`), т.е.
  рендерится ровно тем идентификатором, который lowering и назначил;
- alias и ссылка порождаются из одного и того же источника в lowering'е, поэтому property/physical
  name и case не могут их рассинхронизировать. Точные идентификаторы пинуются SQL-golden-тестом.

**Global scalar (`m = 0`, `n = 1`)**

```
E = select max|min(v1) as __nextorm_ext_1_0 from T where N and C
O = <projection> from T join E on T.v1 = Column(E,'__nextorm_ext_1_0')
    where C   [distinct] [order by …]
```
Пустой `S`: агрегат даёт `NULL`, равенство не срабатывает → ноль строк (инвариант §4 спеки).

**Grouped scalar (`m > 0`, `n = 1`)**

```
E = select g_1..g_m as __nextorm_ext_1_1.., max|min(v1) as __nextorm_ext_1_0
    from T where N and C group by g_1..g_m
O = <projection> from T join E
      on nullSafeEq(T.g_1, Column(E,'__nextorm_ext_1_1')) and …
     and nullSafeEq(T.g_m, Column(E,'__nextorm_ext_1_m'))
     and T.v1 = Column(E,'__nextorm_ext_1_0')
    where C   [distinct] [order by …]
```

`nullSafeEq(a,b)` = `a = b or (a is null and b is null)` — обычная OR-форма (§4 спеки), собирается
в lowered-выражении явно.

**Composite (`n > 1`) — поэтапные агрегаты (§5.5.4)**

```
E_1 = select g_* as __nextorm_ext_1_g*, max|min(v1) as __nextorm_ext_1_v1
      from T where N and C group by g_*
E_k = select g_*, v_1..v_{k-1}, max|min(v_k) as __nextorm_ext_k_vk
      from T join E_{k-1}
        on nullSafeEq(g_*) and T.v1 = Column(E_{k-1},'__nextorm_ext_{k-1}_v1') and …
       and T.v_{k-1} = Column(E_{k-1},'__nextorm_ext_{k-1}_v{k-1}')
      where N and C group by g_*, v_1..v_{k-1}
O   = <projection> from T join E_n
        on nullSafeEq(g_*) and T.v1 = Column(E_n,'__nextorm_ext_n_v1') and …
       and T.vn = Column(E_n,'__nextorm_ext_n_vn')
      where C   [distinct] [order by …]
```

Лексикографический контракт: `(9,2)` побеждает `(8,100)`; комбинация `(9,100)` не возникает,
потому что `E_k` агрегирует `v_k` только внутри префикса, выжившего на шаге `k-1`.

`E_k` — не новый «движок»: это detached-вариант того же подготовленного `TEntity`-command с
заменёнными `SelectList`, `GroupingList`, `PreparedCondition` и `Joins`, отрендеренный тем же
`MakeSelect`.

**Что именно дублируется осознанно.** Пользовательское условие `C` рендерится `n+1` раз (в каждом
stage и в `O`). Это следствие переносимой конструкции: без общего CTE нельзя прочитать одну
отфильтрованную популяцию дважды. Следствия, которые обязаны быть покрыты тестами:

- число и порядок параметров меняются относительно текущего `rank()`-понижения (детерминированно);
- captured value в `C` попадает в SQL несколько раз с одинаковым значением;
- внутренний CTE/`MATERIALIZED` **не вводится** (спека: поддержка CTE не должна становиться
  требованием к публичной форме).

### 4.4 `One` (portable): устранение дублирующего outer

`One` обязан остаться byte-identical. Внутренний `select *, row_number() over (…) as rn` обычный
projection-конвейер не выражает (он не умеет `*` + synthetic column). Поэтому:

- inner остаётся специализированным фрагментом (`MakePortableExtremeRowInner` переезжает в lowering
  без изменения текста);
- outer становится обычной lowered-командой: `O.From` = **opaque fragment source** (новый
  внутренний случай `FromExpression`, см. 4.6), `O.PreparedCondition` = предикат
  `rank = 1`, `O.SelectList`/`DISTINCT`/`ORDER BY` — пользовательские.

Таким образом исчезает именно **outer**-дубль (`MakeExtremeRowOuter`); специализированным остаётся
только само оконное понижение — это и есть «semantic-to-query lowering», разрешённый §5.1.

Предикат `rank = 1` выражается `SqlFunctions.Column<int>(o, "<rank>") == 1` и рендерится как
`o.<rank> = 1`; byte-identical проверяется golden-тестом (§8, S5).

### 4.5 Native `One`: opaque source → общий outer

Native-ветка уже возвращает строку (`IExtremeRowRenderer.Render`). Она подаётся в `O.From` как
opaque fragment source; outer projection/`DISTINCT`/ordering/parameters — общие. Это ровно адаптер
из §5.3 спеки. Публичный контракт renderer'а не меняется, `CanRender` остаётся side-effect-free до
любых мутаций, исключение пробрасывается без retry.

### 4.6 Opaque fragment source

Новый **внутренний** вид source внутри `FromExpression` (имя рабочее: `DerivedFragmentExpression`):

- несёт делегат/структуру, которая в `SqlBuildContext` (a) отдаёт SQL-фрагмент, (b) в param-mode
  проходит те же выражения в том же порядке;
- рендерится как `(<fragment>) <alias>` и регистрирует `TEntity` как physical-shaped source
  (как сегодня делает `MakeExtremeRowOuter` через `ColumnsProvider.Add(entityType,false)`);
- живёт **только в detached lowered-дереве**: в keyed-команду не попадает, в plan-key, equality,
  clone и raw-shape capture не участвует (это фиксируется тестом, S0-4).

Альтернатива (добавить `select *` + synthetic column в общий projection-конвейер) отклонена
планом: она меняет общий рендер ради одной фичи и создаёт риск для всех запросов.

---

## 5. Инварианты семантики → как обеспечиваются

| Инвариант спеки | Механизм |
|---|---|
| Все tied winners, полная исходная строка | Join-back возвращает строки `T`, projection пользовательский; payload не собирается из агрегатов. |
| Ties только по полному селектору | Равенство по каждому компоненту value key; `ORDER BY` пользователя на выбор не влияет. |
| Дубликаты сохраняются | `JOIN` не дедуплицирует; `DISTINCT` — только внешний, пользовательский. |
| `Max` и `Min` | `max|min` в агрегате; направление не влияет на join. |
| `NULL` в любом компоненте value key исключается до агрегата и до join-back | `N` в каждом stage и равенство `T.v = E.v` (NULL не равен) в `O`. |
| Nullable group keys, null-safe равенство по каждому компоненту | Явная OR-форма в join-условии. |
| Группа без допустимых value-строк опускается | В `S` для неё нет строк → нет группы в `E` → нет join-совпадений. |
| `NULL` в group-selector группу не исключает | `nullSafeEq` даёт `true` для `(NULL, NULL)`. |
| Пустой вход: grouped → пусто; global → 0 строк | Агрегат `NULL` / отсутствие групп. |
| Фильтр на обеих сторонах одинаков | `C` в каждом stage и в `O`. |
| Составной key лексикографически | Поэтапные `E_k` (§4.3). |
| Projection/mapping/`DISTINCT`/ordering | Общий конвейер `O`. |
| Guards до lowering, внутренние узлы их не вызывают | `EnsureExtremeRowCompatible` на входе; внутренние узлы не проходят `ExtremeRowCompatibility`. |
| Нет window-fallback для `All` | `All` не имеет ветки на `MakePortableExtremeRowInner`; тест assert'ит отсутствие `rank()`/`row_number()`. |
| Один statement, без client-side materialization | Одна команда, один SQL. |

---

## 6. Кэш, identity, параметры

- Логический ключ и `QueryPlan` считаются по исходной команде — без изменений.
- Lowered-дерево строится заново на каждый рендер, но рендер один на plan-miss (§3). Cache-hit
  использует сохранённый SQL и param-recipe; структура не пересобирается.
- Sticky `QueryCommand.Cache` не мутируется.
- Параметры: SQL-pass и param-pass обходят **одно и то же** lowered-дерево в одном порядке;
  `param-mode` рендер строит идентичную структуру из того же подготовленного состояния.
- Renderer на cache path: поведение пинуется тестом как существующее (callbacks могут повторяться
  при extraction — тест фиксирует факт, а не изобретает контракт).
- Raw-shape: fragment source не участвует в capture; repeated fresh/prepared запросы сохраняют
  поведение.

---

## 7. Провайдерные адаптации

Один внутренний seam в lowering — **на уровне выражений**, не SQL-строк:

- `BuildExtremeAggregate(Expression value, ExtremeKind kind)` → `Expression`
  (`SqlFunctions.Sql.max/min<T>(…)` сегодня, `SqlFunctions.cs:1565-1570`);
- `BuildValueMatch(Expression left, Expression right)` — по умолчанию `==`;
- `BuildNullSafeEqual(Expression left, Expression right)` — по умолчанию OR-форма.

Провайдерная адаптация реализуется либо в самом выражении агрегата (например, `Convert` к
order-preserving типу), либо в существующем dialect/translator-слое; конкретное место выбирается на
S0/S4 и не размазывается по core-семантике по имени провайдера.

Переопределения:

| Провайдер | Что нужно | Решение |
|---|---|---|
| SQL Server | `MIN/MAX` над `bit` невалиден | агрегировать order-preserving представление (`CAST(v AS int)`) и сравнивать обратно; seam выше. |
| ClickHouse | `Nullable(T)` в агрегате, `FINAL/SAMPLE` уже отсечены guard'ом | проверить `max/min` над `Nullable`; при необходимости `maxOrNull`/`minOrNull` в seam'е. |
| MySQL/MariaDB | collation равенства | сравнение по колонке, как `rank()` сегодня; parity-тест. |
| PostgreSQL | native `One` не затрагивается | — |
| SQLite | `max/min` над text/blob | parity-тест на существующих допустимых типах. |
| Все | floating-point `NaN`/non-finite | parity там, где поддержано сейчас; не расширяем и не сужаем. |

Любая невозможность паритета — **planning blocker** (§15 спеки), а не повод менять контракт.

---

## 8. Работы по этапам

Каждый этап заканчивается гейтом; следующий не начинается без зелёного предыдущего.

### S0 — Feasibility probe (обязателен до продуктового кода)

Цель: доказать пять механизмов в scratch-харнессе, без правок продукта, на SQLite + PostgreSQL.

1. **Detached prepared variant.** Клон подготовленной `All`-команды с заменёнными
   `SelectList`/`GroupingList`/`PreparedCondition`/`Joins` рендерится `MakeSelect` без падений
   (DEBUG-инварианты, alias'ы, param-mode).
2. **Same-type join + lowering-owned aliases.** `T JOIN (derived T)` разрешает оба параметра по
   индексу scope; ссылки на выходы stage'а через `SqlFunctions.Column` рендерятся назначенными
   alias'ами; SQL корректен; параметры в правильном порядке.
3. **Composite chain.** `E_1 → E_2 → O` рендерится, семантика `(9,2)` vs `(8,100)` верна на живой БД.
4. **Fragment source.** `One`-inner и native-строка рендерятся как `(<fragment>) alias`, outer
   получает проекцию/`DISTINCT`/ordering/params, и fragment **не попадает** в plan-key/equality.
5. **Guard-изоляция.** Внутренние join/group-by не вызывают `ExtremeRowCompatibility`.

Выход: `RESULTS.md` с SQL-дампом каждого случая, значениями и param-порядком.
**Стоп-условие:** если (1) или (4) невыполнимы без публичного изменения контракта — STOP, отчёт
пользователю, план не исполняется.

### S1 — `All`, global scalar

Lowering `E` + `O`; guards и рендер общие; SQL-generation по 6 диалектам; отсутствие
`rank()`/`row_number()`; live parity на SQLite + PG + SQL Server.

### S2 — `All`, grouped scalar

`GROUP BY` + null-safe join; матрица nullable-групп, полностью `NULL` составного ключа, групп без
допустимых value-строк, пустого входа.

### S3 — `All`, composite

Поэтапные `E_k`; counterexample `(9,2)` vs `(8,100)`; arity 2 и 3; converted keys.

### S4 — Провайдерная матрица `All`

SQL-generation и live по всем шести семействам (MariaDB — SQL-generation обязательно, live — в
пределах harness); SQL Server `bit`; ClickHouse nullable; collation/`NaN` parity.

### S5 — `One` portable через общий outer (**семантическая эквивалентность**)

> **Поправка гейта, решение USER 2026-11-03:** требование byte-identity для `One` заменено
> эквивалентностью по смыслу. Причина — измеренная: 74/84 арма совпадают байт-в-байт, а остаток
> сводится к кавычкам ссылки на rank-колонку и одному пробелу перед `ORDER BY`, которые задаются
> глобальными конвенциями рендера; подгонка под них означала бы либо менять рендер для всех
> запросов, либо возвращать спец-случаи, то есть дублирование под другим именем.

Baseline полного SQL, параметров и строк снимается **до** изменения (все диалекты × формы).
Затем `MakeExtremeRowOuter` удаляется, `One` идёт через fragment source + общий `MakeSelect`.
Гейт:
- **семантическая эквивалентность SQL**: после нормализации пробелов и кавычек идентификаторов
  statement совпадает с baseline;
- **тот же набор строк** на живой БД для матрицы форм (порядок — согласно `ORDER BY`);
- **тот же состав и порядок параметров**;
- поведение, владение reader'ом и публичный контракт не меняются.

Любое расхождение сверх нормализованных пробелов/кавычек — блокер, а не повод расширять
нормализацию.

### S6 — Native `One` через общий outer

`MakeNativeExtremeRowSelect` перестаёт звать `MakeExtremeRowOuter`; native-строка → fragment source.
Accept/decline/throw, ленивость DTO, cache-stability — существующие тесты + новые на общий outer.

### S7 — Документация EN+RU

`docs/guide/09-distinct.md`, `docs/advanced/select-where-extrema-native.md`,
`docs/advanced/api-reference.md` и RU-зеркала — по §12 спеки. Без ссылок на `docs/specs/**` и
`docs/superpowers/**`.

### S8 — Полные гейты и приёмка

Матрица §11 спеки целиком, полный benchmark matrix, все suite'ы, E19, coverage.

---

## 9. Тест-план

**SQL-generation (`tests/nextorm.<provider>.tests`)** для каждого из 6 семейств:

- global/grouped, `Max`/`Min`, scalar/composite (arity 2, 3);
- assert: присутствует агрегат + join-back; **отсутствуют** `rank(`/`row_number(` для `All`;
- nullable group keys → OR-форма по каждому компоненту;
- hidden alias collision: имя физической колонки не конфликтует с внутренними алиасами;
- mapped physical columns, DTO/anonymous/record projection, nullable payload, `DISTINCT`,
  output ordering;
- guards: исходные отказы срабатывают до lowering; внутренние узлы не вызывают чужие guards.

**Live integration (`tests/nextorm.integration.tests`)** — correctness matrix §11.6 спеки:
несколько ties, целостность payload, дубликаты, `NULL` в каждом компоненте, nullable-группы,
полностью `NULL` составной ключ, группы без допустимых value-строк, пустой вход, фильтры,
удаляющие winners, `(9,2)` vs `(8,100)`, converted keys (SQL Server `bool`), floating-point/collation,
ordering/projection/distinct. Window-lowering — только как test-only baseline.

**Кэш/параметры:** одинаковая форма с разными bound values → переиспользование плана с текущими
значениями; другая форма `Where` → нет ошибочного sharing; raw-shape cold/hit; repeated
prepared/fresh; после decline renderer'а cache включён; sticky `Cache` не мутируется; параметры
детерминированы в SQL и extraction.

**`One`:** golden SQL до/после (S5), native accept/decline/throw, forced-portable parity.

**In-memory:** существующий suite — regression only, без новых тестов.

---

## 10. Наблюдаемые изменения контракта

Явно фиксируется, чтобы не всплыло как «регрессия»:

1. **Текст SQL для `All` меняется** — это цель задачи.
2. **Число/порядок параметров для `All` меняется** (условие рендерится `n+1` раз). Тесты,
   пинующие параметры `All`, обновляются осознанно; `One` не затрагивается.
3. Число DB statements не меняется.

Всё остальное — наблюдаемо неизменно.

---

## 11. Приёмка (mapping §11 спеки)

| Пункт спеки | Чем закрывается |
|---|---|
| 11.1 Архитектура | `MakeExtremeRowOuter` удалён (S5/S6); `All`/`One`/native идут через `MakeSelect`; architecture review диффа. |
| 11.2 SQL-generation и normalized tree | §9, S1–S5. |
| 11.3 `One`, native, renderer | baseline SQL/строк/параметров + семантическая эквивалентность (S5, поправка 2026-11-03), native-тесты (S6). |
| 11.4 Кэш и параметры | §9. |
| 11.5 Live integration | §9, S4/S8. |
| 11.6 Correctness matrix | §9, S2–S4. |
| 11.7 In-memory | regression suite. |
| 11.8 Performance | supporting-отчёт: без индекса / с индексом / только по value; улучшения и регрессии показаны честно. |
| 11.9–11.10 Документация | S7. |

---

## 12. Риски и планировочные блокеры

| Риск | Реакция |
|---|---|
| Detached prepared variant ломает DEBUG-инварианты/alias'ы | S0-1; при провале — STOP и отчёт. |
| Авторазрешение двух источников одного типа не работает вне builder'а | S0-2; fallback — явная привязка `SourceParameter` (уже существует для navigation joins). |
| `SqlFunctions.Column` не резолвит выход derived source (или ломает case/quoting) | S0-2; fallback — эталонный alias через `SelectExpression.OutputName` + `IsProjectionOutputReference`, иначе STOP. |
| `select *` inner нельзя выразить общим конвейером | принято в дизайне: fragment source (4.6); требует S0-4. |
| Рост числа параметров ломает существующие тесты/лимиты провайдера | §10; лимит параметров SQL Server (2100) — отдельная проверка при большом `C`. |
| SQL Server `bit`/`uniqueidentifier` | seam §7; невозможность паритета — blocker. |
| Collation/`NaN` дают другой winner, чем `rank()` | parity-тест; расхождение — blocker, не «поправим требование». |
| Composite chain даёт неверный план или неверную семантику | S3 counterexample + live matrix. |
| Публичное изменение `IExtremeRowRenderer` понадобится | STOP, отдельный вопрос на approval (спека §5.3/§15). |
| Fragment source протекает в plan-key | S0-4 тест на equality/hash/clone/raw-shape. |

---

## 13. Порядок исполнения и режим

Предлагаемый режим после approval плана: **один PDCA-цикл на этап** (S0, S1, …, S8), с
обязательным независимым CHECK и полными гейтами на S4/S8. S0 — отдельный цикл, потому что его
отрицательный результат останавливает всё.

Если пользователь предпочитает другой режим (например, один цикл на весь `All`), это меняет только
группировку шагов, не сами гейты.

---

## 14. Следующий гейт

1. ~~USER review и approval этого плана~~ — **выполнено 2026-11-02** (явное «апрув»).
2. **Текущий гейт:** отдельное явное разрешение на исполнение (код/тесты/доки продукта).
3. Commit/push/merge этим планом не авторизованы.

Approval плана не означает approval реализации; issue #302 остаётся OPEN до завершения работ.
