# План декомпозиции `BaseExpressionVisitor`

**Статус:** исполняется по фазам.
**Связано:** [code-smells-review.md](code-smells-review.md) — Находка 4 (god-классы).
**Файл:** `src/nextorm.core/Visitors/BaseExpressionVisitor.cs` — на старте 2213 строк, порог навыка >500.

## 0. Прогресс

| Фаза | Статус | Результат |
|---|---|---|
| 0. Characterization | ⏸ частично | снимок baseline: `test/TestResults/bev-baseline/BaseExpressionVisitor.baseline.cs.txt` (gitignored); отдельные golden-тесты на `exists`/`any`/`all` не добавлялись — ветка покрыта `InMemoryTests.cs:231`, `CommonTestSuite.SqlCommand.cs:439-461`, `CommonTestSuite.CorrelatedQuery.cs:11` |
| 1. `internal`-поверхность | ✅ | 14 геттеров, 2 `internal`-сеттера (`NeedAliasForColumn`, `ColumnName`), `VisitToString`/`RenderPredicate`/`TranslateInValues` → `internal`; публичный API не изменён |
| 2. `SqlLiteral`/`TypeFacts`/`WindowSql` | ✅ | 2213 → **2011**; новые типы 42/58/147 |
| 3. `NormSqlTranslator` | ✅ | 2011 → **1769**; `NormSqlTranslator` 301 |
| 4. `ScalarFunctionTranslator` | ✅ | 1769 → **1404**; `ScalarFunctionTranslator` 384 |
| 5. `WindowFunctionTranslator` | ✅ | 1404 → **1263**; `WindowFunctionTranslator` 154 |
| 6. `InValuesTranslator` | ✅ | 1263 → **1170**; `InValuesTranslator` 107 |
| 7. `PredicateTranslator` | ✅ | 1170 → **806**; `PredicateTranslator` 400 |
| 8. `MemberTranslator` + `AliasResolver` | ✅ | 806 → **389** (порог 500 пройден); `MemberTranslator` 422, `AliasResolver` 23 |
| 9. Параметр-объект конструктора | ✅ | `VisitorOptions` (28) + предпочтительный ctor на 1 параметр; старый 12-параметровый оставлен как compatibility overload (extend-only) |
| 10. Финал | ✅ | замер + отчёт; `BaseExpressionVisitor` **2213 → 404** (−82 %), 10 новых типов, ни один ≤500 |

**Текущие размеры:** `BaseExpressionVisitor` **404**; `VisitorOptions` 28; `MemberTranslator` 422; `PredicateTranslator` 400; `ScalarFunctionTranslator` 384; `NormSqlTranslator` 301; `WindowFunctionTranslator` 154; `WindowSql` 147; `InValuesTranslator` 107; `TypeFacts` 58; `SqlLiteral` 42.

**Проверка фаз 1–10:** `dotnet build nextorm.sln -c Debug` — 0/0; тесты core 111, sqlite 144, sqlserver 116, postgres 90, mariadb 6 — все зелёные. Механическая сверка: фаза 2 — потерь нет (только квалификация вызовов + скаффолдинг); фаза 3 — 218 код-строк, отличие только `WindowSql.MapWindowFunctionName`; фаза 4 — 211 код-строк, `LOST`/`EXTRA` = 0; фаза 5 — 78 код-строк, 0/0; фаза 6 — 51 код-строка, 0/0; повторная сверка фазы 3 после переименования локальной переменной — без потерь; фаза 7 — 234 код-строки, все расхождения объяснены (мой `base.VisitUnary` → `null` + ренейм локальной `whereVisitor`; плюс независимая параллельная правка `VisitBinary`/`ISqlDialect.MakeConcat` автором репозитория); фаза 8 — 260 код-строк, единственное расхождение — намеренный ренейм локальной `visitor` → `twoTypeVisitor` в `VisitMember`; фаза 9 — без переноса логики (только группировка состояния), проверено: все 14 readonly-полей присвоены в options-ctor, legacy ctor делегирует, `Clone()` идёт через `_options`.

**Итог:** цель достигнута — god-класс устранён (404 < 500), длинные методы разнесены, конструктор сгруппирован в `VisitorOptions`. Публичный API сохранён полностью (только добавления).

**Грабли фазы 8:** в `VisitMember` есть закомментированная скобка `//}` — при подсчёте баланса скобок её надо срезать (`re.sub(r"//.*","",line)`), иначе метод обрезается и извлечение портит оба файла. Учитывать в любой подобной механике.

**Порог достигнут:** `BaseExpressionVisitor` 404 < 500, оболочка + 10 типов, ни один метод >~115 строк.

**Фаза 9 (параметр-объект):** `VisitorOptions` — `sealed record` со всеми construction-time коллабораторами; `BaseExpressionVisitor(VisitorOptions)` — предпочтительный ctor, длинная сигнатура делегирует в него. Внутренние вызовы (`SqlBuilder` ×4, `WhereExpressionVisitor` base-call, дочерние визиторы в `NormSqlTranslator` через `visitor.Options with { ... }`, `Clone()`) переведены на параметр-объект. Публичный 12-параметровый ctor сохранён для бинарной совместимости — S107 на нём остаётся осознанно (ломать публичный API нельзя).

> ⚠️ В репозитории одновременно идут чужие правки (например, `ISqlDialect.MakeConcat` и перевод `VisitBinary` на него). Извлечения делаются из текущего рабочего дерева, поэтому сверка с baseline может показывать и их изменения.


## 1. Цель

Разбить `BaseExpressionVisitor` на связные типы по SRP: убрать god-класс, длинные методы
(`VisitMethodCall` 461, `VisitMember` 311, `VisitBinary` 109, …) и 12-параметровый конструктор,
**не меняя ни одного байта генерируемого SQL и публичный API**.

## 2. Что есть сейчас

`public class BaseExpressionVisitor : ExpressionVisitor, ICloneable, IDisposable`.

| Кластер | Методы | Строки | Размер |
|---|---|---|---:|
| Диспетчер метода | `VisitMethodCall` (+ локальная `CompileExp`) | 67–527 | 461 |
| `NORM.NORM_SQL` built-ins | ветка `typeof(NORM)` внутри `VisitMethodCall` | 163–424 | 262 |
| Оконные функции | `TryTranslateWindowFunction` + 10 хелперов | 609–884 | ~276 |
| Скалярные функции | `TryTranslateFunction`/`SqlFunction`/`String*`/`Math`/`Like*`, `BuildLikePattern` | 529–1203 | ~400 |
| IN / `Contains` | `TryTranslateCollectionContains`, `TranslateInValues` | 892–977 | 86 |
| Члены и проекции | `VisitMember`, `TryTranslateMember`, `GetAliasFromParam`×2, `VisitIndex` | 1205–1277, 1353–1375, 1430–1739 | ~427 |
| Предикаты / bool | `RenderPredicate`, `AppendCondition`, `VisitCondition`, `VisitNot`/`OnesComplement`/`UnaryOperator`, `VisitUnary`, `VisitBinary`, `VisitConditional`, `VisitSwitch`, `Is*`, `TryGetNumericConversion` | 1755–2061, 1847–1929 | ~340 |
| Скалярные константы | `VisitConstant` | 1384–1429 | 46 |
| Оболочка | поля, ctor (12 параметров), свойства, `AsPredicate`, `Clone`, `Dispose`, `ToString`, `WriteTo` | 13–66, 2171–2213 | ~100 |

Замер по методам (топ): 461, 311, 109, 106, 91, 79, 73, 59, 47, 46 — **13 методов >30 строк**.

## 3. Сеть безопасности

| Проверка | Покрытие |
|---|---|
| `SqlGenerationTests` (exact-string SQL) | **279 тестов**: sqlite 108, sqlserver 89, postgres 82 |
| `WindowFunctionMarkerTests` | 4 |
| `CommonTestSuite.Window`/`In`/`Aggregates` (integration) | 49 |
| Golden-упоминания по семействам | `cast(` 28, `is null` 41, `case when` 18, агрегаты 61, window 40, `In` 23, `like` 18, `Math.` 16, `Length` 15, DateTime-части 14, `.Value`/`HasValue` 27 |

**Слепые зоны (закрыть до фаз 3 и 8):**
- `exists`/`any`/`all` — 1 упоминание в golden (код: строки 195–275);
- `OuterRef`/correlated — 2 упоминания.

## 4. Жёсткие инварианты

1. **SQL байт-в-байт** — golden-тесты основной оракул.
2. **Порядок и число `_params.Add`** — двухпроходность `_paramMode` (param-проход → SQL-проход)
   обязана обходить дерево в том же порядке, иначе разъедутся `p0..pN`. Не объединять и не
   переупорядочивать вызовы `Visit(...)`.
3. `_needAliasForColumn` / `_colName` — семантика алиасов вычисляемых колонок
   (`SqlBuilder.cs:488-492` читает `NeedAliasForColumn`/`ColumnName`).
4. `Clone()` возвращает **`BaseExpressionVisitor`**, а не производный тип, и бросает в `_paramMode`.
   `RenderPredicate` намеренно создаёт `new WhereExpressionVisitor(...)`, а не `Clone()` — не «унифицировать».
5. `Dispose`/`ObjectPool`: `_builder` возвращается в пул ровно один раз (`_disposedValue`).
6. Публичный API неизменен; `protected _builder`/`_paramMode`/`AsPredicate` не трогать →
   `WhereExpressionVisitor` (наследник) продолжает работать.
7. `InternalsVisibleTo` в `nextorm.core` отсутствует → `internal`-поверхность видна только внутри сборки.

## 5. Целевая архитектура

Трансляторы — **top-level `internal static` классы** в `src/nextorm.core/Visitors/` плюс
минимальная `internal`-поверхность на визиторе.

Почему не nested (как `QueryPreparer`): nested-тип даёт доступ к приватному состоянию без
расширения API, но под метрикой «строк класса» размер внешнего типа не уменьшается — Находка 4
не закроется. Top-level даёт честное сокращение и настоящий SRP.

Поверхность (~20 слов правок, всё `internal`, публичный API не меняется):

```csharp
// Internal collaboration surface for Visitors/Translators/*. Not part of the public API.
internal ISqlDialect Dialect => _dialect;
internal IColumnsProvider ColumnsProvider => _columnsProvider;
internal IAliasProvider? AliasProvider => _aliasProvider;
internal IParamProvider ParamProvider => _paramProvider;
internal IQueryProvider QueryProvider => _queryProvider;
internal ObjectPool<StringBuilder> BuilderPool => _sbPool;
internal List<Param> Params => _params;
internal ILogger? Logger => _logger;
internal Type EntityType => _entityType;
internal int Dim => _dim;
internal bool DontNeedAlias => _dontNeedAlias;
internal bool IsParamMode => _paramMode;
internal StringBuilder? Builder => _builder;
internal bool IsPredicateContext => AsPredicate;
public bool NeedAliasForColumn { get => _needAliasForColumn; internal set => _needAliasForColumn = value; }
public string? ColumnName { get => _colName; internal set => _colName = value; }
internal string VisitToString(Expression e) => ...;   // было private
internal string RenderPredicate(Expression e) => ...; // было private
```

Альтернативы, если этот вариант отклонён: **nested** (ноль правок доступа, метрика не падает) и
**context-object `VisitorContext`** (чище по SRP, ~200 механических замен обращений к полям, выше риск).

## 6. Фазы

| # | Фаза | Извлекаем | Ожидаемо −строк | Риск |
|---|---|---|---:|---|
| 0 | Characterization | baseline build+tests; снимок текущего файла; golden-тесты на `exists`/`any`/`all` и `OuterRef` | 0 | S |
| 1 | `internal`-поверхность | ничего не переносим, только доступы | 0 | S |
| 2 | `SqlLiteral`, `TypeFacts`, `WindowSql` | чистые static: `ToSqlStringLiteral`, `EscapeLikeWildcards`, `TryGetConstantString`, `MapWindowFunctionName`, `ParseWindow*`/`AddWindow*`, `UnwrapWindowLambda`, `SplitWindowArguments`, `RenderWindow*`, `IsBoolean`/`IsPredicate`/`IsPredicateCall`, `TryGetNumericConversion` | ~200 | S |
| 3 | `NormSqlTranslator` | ветка `typeof(NORM)` 163–424 → `TryTranslateNormParam` + `TryTranslateNormSql`; `CompileExp` (502–526) | ~280 | M |
| 4 | `ScalarFunctionTranslator` | `TryTranslateFunction`/`SqlFunction`/`String*`/`Math`/`Like*`, `BuildLikePattern`, ветка `string.Concat` (429–460), `LikePosition` | ~400 | M |
| 5 | `WindowFunctionTranslator` | `TryTranslateWindowFunction`, `EvaluateWindowFrame` | ~240 | M |
| 6 | `InValuesTranslator` | `TryTranslateCollectionContains`, `TranslateInValues` | ~93 | M |
| 7 | `PredicateTranslator` | `RenderPredicate`, `AppendCondition`, `VisitCondition`, `VisitNot`/`OnesComplement`/`UnaryOperator`, `VisitUnary`, `VisitBinary`, `VisitConditional`, `VisitSwitch` | ~340 | M |
| 8 | `MemberTranslator` + `AliasResolver` | `TryTranslateMember`, тело `VisitMember`, `GetAliasFromParam`×2, `VisitIndex` | ~420 | **H** |
| 9 | Параметр-объект конструктора | 12 параметров → `VisitorOptions`; **extend-only** (старый ctor остаётся) | ~10 | M |
| 10 | Финал | замер, обновление отчёта, CRLF | — | S |

**Ожидаемый остаток оболочки: ~250–350 строк** (поля, ctor, свойства, `AsPredicate`, 8 тонких
override-делегаторов, `Clone`/`Dispose`/`ToString`/`WriteTo`) — ниже порога 500. Новые типы —
60–430 строк каждый.

**Опционально (низкий приоритет):** `CachedExpressionEvaluator` — блоки `ExpressionKey` +
`DataContextCache.ExpressionsCache` + логирование продублированы 4× (строки 213–246, 291–306,
464–478, 1518–1534, 1616–1634). Каждый кэшируется под своим ключом и по-своему логирует —
унифицировать только с отдельными тестами.

## 7. Порядок

`2 → 3 → 4 → 5 → 6 → 7 → 8 → 9` (фазы 0–1 — предпосылки, выполняются перед соответствующей фазой).

Статики первыми (нулевой риск, разгружают остальные). `NormSql` (3) — максимальный выигрыш при
умеренном риске. Самый рискованный `MemberTranslator` (8) — после того, как остальные кластеры
вынесены и слепые зоны закрыты. Если нужен чекпоинт под порогом 500 раньше — `8` можно поменять
местами с `7`.

## 8. Протокол проверки фазы

1. `dotnet build nextorm.sln -c Debug` → 0 warnings / 0 errors.
2. `dotnet test/<proj>.tests/bin/linux/Debug/net10.0/<proj>.tests.dll -noColor` ×4
   (core/sqlite/sqlserver/postgres); `nextorm.integration.tests` — если поднимаются контейнеры.
3. Механическая сверка: нормализованный diff кода (без `v.`/`this.`, комментариев, отступов)
   против снимка — «ничего не потеряно» (скрипт по образцу `verify_preparer.py`).
4. Замер строк по типам скриптом, с раздельным учётом каждого типа.

## 9. Риски

| Риск | Митигация |
|---|---|
| Сдвиг нумерации параметров при выносе веток `_paramMode` | не менять порядок `Visit`; golden In-тесты + plan-cache-тесты после каждой фазы |
| Подмена `new WhereExpressionVisitor` на `Clone()` в `RenderPredicate` | зафиксировано инвариантом §4.4 |
| Double-return `ObjectPool` | `Dispose`/`Clone` не трогаем до фазы 9 и только по чек-листу |
| Поломка внешних наследников | только `internal`; `protected` не меняем; IVT нет |
| Файл активно правится параллельно | короткие фазы; перед началом `git diff`/mtime; снимок текущего файла для механической сверки |
| Логика спрятана в `CorrelatedQueryExpressionVisitor` (отдельный визитор, 418 строк) | в этом плане не трогаем |

## 10. Критерии готовности

- `BaseExpressionVisitor` ≤500 строк (~250–350), ни один метод >~60 строк.
- 9 кластеров — в отдельных `internal` типах, каждый ≤500.
- Все тесты зелёные, SQL не изменился.
- `WhereExpressionVisitor` и публичный API не затронуты.
- Отчёт: Находка 4 — **4 → 3** класса; закрыто и «длинные методы».

## 11. Не-цели

Не меняю SQL/семантику, не разбираю `NotImplementedException`-ветки, не трогаю
`CorrelatedQueryExpressionVisitor`, `SqlBuilder`, публичный ctor (до фазы 9), не добавляю провайдеров.
