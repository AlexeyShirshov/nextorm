# Расширяемость: пользовательские SQL-диалекты и рендереры

> nextorm рендерит SQL через [`ISqlDialect`](xref:NextORM.Core.ISqlDialect); поддерживаемая граница расширяемости позволяет внешней сборке поставлять собственный диалект, для нативной стратегии экстремальной строки — собственный рендерер, либо компонент запроса, добавляющий структурное поведение в построение запроса, — без friend-доступа.

**Предварительные требования:** [Нативные стратегии выбора экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md) · [Обзор провайдеров](../connecting-providers/overview.md) · [Краткий справочник API](api-reference.md)

## Обзор

Нативная стратегия экстремальной строки — это публичная возможность диалекта, а не жёстко зашитая ветка по провайдеру:

- [`ISqlDialect.ExtremeRowRenderer`](xref:NextORM.Core.ISqlDialect.ExtremeRowRenderer) — необязательный рендерер, объявленный default-членом интерфейса, возвращающим `null`; [`SqlDialectBase.ExtremeRowRenderer`](xref:NextORM.Core.SqlDialectBase.ExtremeRowRenderer) переопределяет его виртуально с тем же значением по умолчанию. `null` означает отсутствие нативной стратегии.
- [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer) предоставляет `CanRender(`[`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription)`)` — решение без побочных эффектов, принимаемое до сборки любого SQL, — и `Render(`[`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest)`)`, возвращающий источник строки-победителя. Только положительный `CanRender` ведёт к `Render`, и у `Render` нет позднего отката к переносимому пути.
- [`ExtremeRowRenderColumn`](xref:NextORM.Core.ExtremeRowRenderColumn), [`ExtremeRowDescription`](xref:NextORM.Core.ExtremeRowDescription) и [`ExtremeRowRenderRequest`](xref:NextORM.Core.ExtremeRowRenderRequest) несут только факты формы (CLR-тип, nullable, признаки прямой отображённости и конвертера, а также подготовленный источник и алиасы), но никогда — выражение или контекст построения. См. [Краткий справочник API](api-reference.md).
- Каждый из трёх DTO также предоставляет **публичный eager-конструктор**, поэтому сборка без friend-доступа может их создать и управлять своим [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer). Конструирование немедленно и структурно проверяет ссылки и элементы коллекций: обязательная ссылка `null` даёт `ArgumentNullException`; элемент `null`, пустой строковый элемент алиаса или `SourceSql`, пустой/состоящий только из пробелов, даёт `ArgumentException`; необъявленное значение `KeywordCase` даёт `ArgumentException`. Оно **не** выполняет разбор SQL и не проверяет типы провайдера/безопасность, не добавляет правил о минимальном количестве, согласованности размеров списков или непересечении алиасов (пустые формы и перекрывающиеся алиасы допустимы), никогда не читает `ExtremeRowDescription.Payload` и никогда не вызывает `CanRender`/`Render`. Коллекции **заимствуются**, не копируются и не делаются неизменяемыми: через свойство наблюдается тот же экземпляр, и вызывающий не должен изменять его, пока DTO используется, в том числе из конкурентного вызова. Внутренний ленивый путь подготовки payload/кандидата (и невалидируемое trusted-конструирование запроса на горячем пути) остаётся деталью реализации и **не** является публичным API.

`PostgresDialect` и `ClickHouseDialect` намеренно **не `sealed`**: их наследуемость вместе с публичными конструкторами (`PostgresDialect()`, `PostgresDialect(Version?)`, `ClickHouseDialect()`), общим синглтоном `Instance` и виртуальным хостом `ExtremeRowRenderer` — это и есть поддерживаемая внешняя граница расширяемости, публичное обещание совместимости, а не тестовая деталь. Пользовательский диалект может наследоваться от любого из них, переопределить хост и вернуть собственную публичную реализацию [`IExtremeRowRenderer`](xref:NextORM.Core.IExtremeRowRenderer); встроенные рендереры (`PostgresExtremeRowRenderer`, `ClickHouseExtremeRowRenderer`) `internal` и в этот контракт не входят. Возврат `null` из переопределения — или рендерер, чей `CanRender` отвечает `false`, — сохраняет переносимое понижение до оконной функции; значения по умолчанию провайдера и конструкторы не меняются.

## Создание пользовательского диалекта

```csharp
// Сборка-потребитель без friend-доступа наследует диалект провайдера и ставит свой рендерер.
public sealed class MyPostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => MyRenderer.Instance;
}

// Нативно обрабатывается только глобальная (несгруппированная) форма с одним ключом; любая
// другая форма уходит на переносимый путь, потому что CanRender отвечает false.
public sealed class MyRenderer : IExtremeRowRenderer
{
    public static readonly MyRenderer Instance = new();

    public bool CanRender(ExtremeRowDescription description)
        => description.Groups.Count == 0 && description.Keys.Count == 1;

    public string Render(ExtremeRowRenderRequest request)
    {
        var direction = request.IsMax ? "desc" : "asc";
        return request.SourceSql
            + " order by \"" + request.KeyAliases[0] + "\" " + direction + " limit 1";
    }
}

// Второй пример: возврат null из переопределения принудительно включает переносимое
// понижение до оконной функции для каждого запроса, без установки рендерера.
public sealed class PortablePostgresDialect : PostgresDialect
{
    public override IExtremeRowRenderer? ExtremeRowRenderer => null;
}
```

## Регистрация пользовательского диалекта

Создание подкласса диалекта — только половина дела: контексту нужно указать, что его использовать. [`DataContext.Dialect`](xref:NextORM.Core.DataContext.Dialect) — **`abstract`**-свойство (`src/nextorm.core/DataContext/DataContext.cs:153`), и каждый провайдерный контекст переопределяет его, возвращая свой встроенный диалект: `PostgresDataContext` и `MariaDbDataContext` возвращают экземпляр, построенный при создании контекста, а `SqliteDataContext`, `MySqlDataContext`, `SqlServerDataContext` и `ClickHouseDataContext` — общий синглтон `Instance`. Переключателя `DataContextBuilder`/`UseDialect` нет; поддерживаемый способ установить пользовательский диалект — унаследовать **провайдерный контекст** и переопределить `Dialect`, вернув свой диалект:

```csharp
public sealed class MyPostgresDataContext : PostgresDataContext
{
    private readonly ISqlDialect _dialect = new MyPostgresDialect();

    public MyPostgresDataContext(string connectionString, DataContextBuilder builder)
        : base(connectionString, builder)
    {
    }

    public override ISqlDialect Dialect => _dialect;
}

using var ctx = new MyPostgresDataContext(connectionString, new DataContextBuilder());

// Идёт в базу через MyPostgresDialect → MyRenderer.
var top = ctx.From<Player>().SelectWhereMax(p => p.Score).ToList();
```

Та же форма работает для `MyClickHouseDataContext : ClickHouseDataContext` (`ClickHouseDialect` тоже не `sealed`). Кэш планов ключуется по **типу контекста**, поэтому заводите отдельный подкласс `DataContext` на каждый диалект — то же правило, что уже сформулировано для серверной версии PostgreSQL. Установка диалекта, чей `ExtremeRowRenderer` возвращает `null` (или рендерера, чей `CanRender` отвечает `false`), сохраняет переносимое понижение до оконной функции.

## Расширение построения запросов компонентами запроса

**Компонент запроса** позволяет стороннему пакету добавить структурное поведение в построение запроса без
изменения ядра под каждый оператор, без friend-доступа, без глобального реестра и без сырого SQL. Компонент
несёт собственную идентичность слота, свою логику подготовки/валидации и обработчик структурного построения, и
присоединяется к неизменяемому построителю через
[`WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent)):

```csharp
var top = ctx.From<Player>()
    .ExternalSelectWhereMin(p => p.Score)   // обычный метод-расширение из пакета
    .ToList();
```

### Зачем нужен этот контракт

Ядро рендерит SQL из собственного дерева выражений, поэтому новую форму запроса обычно можно добавить только
тремя способами: изменить ядро, получить friend-доступ или собрать текст SQL. Компонентный контракт — четвёртый
способ: вы описываете *структуру*, которая нужна оператору, в терминах примитивов самого ядра, а ядро решает,
**когда** вас вызвать. Поэтому ваш оператор наследует кэш планов, привязку параметров и провайдеронезависимость
обычного запроса, вместо того чтобы дублировать их.

Здесь нет ни реестра, ни глобального состояния. Компонент — это объект, прикреплённый к одному неизменяемому
построителю: его время жизни равно времени жизни запроса, и два компонента одного слота никогда не вкладываются.

### Кто что реализует и кто это вызывает

| Тип | Вы | Ядро |
|---|---|---|
| [`IQueryComponent`](xref:NextORM.Core.IQueryComponent) | Реализуете: один объект на экземпляр оператора. | Читает `Slot` при присоединении и при сравнении ключа плана; читает `Contribution` и вызывает `Prepare` на подготовке; вызывает `BuildQuery` на рендеринге. |
| [`IQueryComponentDescriptor`](xref:NextORM.Core.IQueryComponentDescriptor) | Возвращаете из `Prepare`; он неизменяем и не должен ссылаться на изменяемое состояние компонента. | Хранит его в [`QueryCommand.PreparedComponents`](xref:NextORM.Core.QueryCommand.PreparedComponents), хэширует (`StructuralHash`) и сравнивает (`StructurallyEquals`) как часть ключа плана, сбрасывает при сбросе подготовки. |
| [`QueryComponentContribution`](xref:NextORM.Core.QueryComponentContribution) | Объявляете один раз на оператор: `AdditivePredicate` или `ExclusiveRowSource`. | Отвергает второй слот `ExclusiveRowSource` на подготовке и объединяет `AdditivePredicate` через `AND`. |
| [`IQueryComponentPrepareContext`](xref:NextORM.Core.IQueryComponentPrepareContext) | Используете в `Prepare`. | Создаёт его над подготавливаемой командой. |
| [`IQueryComponentBuildContext`](xref:NextORM.Core.IQueryComponentBuildContext) | Используете в `BuildQuery`. | Создаёт его над перестроенной командой (шаг 4 ниже). |
| [`QueryComponentContext`](xref:NextORM.Core.QueryComponentContext) | Обычно не трогаете; он публичный, чтобы тесты могли прогнать компонент напрямую. | Реализует оба контекста и получает `ExpressionComparer` из команды — сравнивайте селекторы им, а не `Expression.Equals`. |
| [`IQueryComponentShape`](xref:NextORM.Core.IQueryComponentShape) | Необязательно: реализуйте, чтобы ваш компонент обслуживался «сырым» (pre-preparation) кэшем рецептов — сообщите его скалярное состояние и селекторы. | Сравнивает `ShapeState` через `Equals`, а селекторы — компаратором выражений, когда сопоставляет свежую команду с сохранённой сырой формой; без этого интерфейса компонент идёт обычным путём подготовки. |
| [`WithComponent`](xref:NextORM.Core.EntityBuilder`1.WithComponent(NextORM.Core.IQueryComponent)) | Вызываете из своего метода-расширения. | Копирует построитель и заменяет компонент того же слота на месте. |
| [`QueryCommand.Components`](xref:NextORM.Core.QueryCommand.Components) / [`PreparedComponents`](xref:NextORM.Core.QueryCommand.PreparedComponents) | Только чтение, для диагностики. | Владеет, публикует и очищает ядро. |

### Когда происходит каждый вызов

1. **Присоединение** — ваш метод-расширение вызывает `WithComponent`. Copy-on-write: построитель клонируется,
   компонент того же слота заменяется, другой слот добавляется. Здесь ничего не валидируется.
2. **`Prepare`** — планировщик вызывает его один раз для каждого присоединённого компонента при подготовке
   команды (`src/nextorm.core/Query/QueryCommand.QueryPreparer.cs`), то есть один раз на *подготовку* = один раз
   на построение плана. Повторно он выполняется, только если та же команда подготавливается снова: другой
   `DataContext`, шаблон, повторно подготовленный из кэша, сброс подготовки. Здесь вы валидируете и отвергаете.
3. **Дескриптор → ключ плана** — возвращённый дескриптор попадает в `QueryCommand.PreparedComponents`; ключ
   плана хэширует `StructuralHash` и сравнивает дескрипторы через `StructurallyEquals` на тёплом поиске
   (`src/nextorm.core/Query/QueryPlanEqualityComparer.cs`, `src/nextorm.core/DataContext/Cache/QueryPlan.cs`).
   Поэтому два структурно равных компонента делят один кэшированный план.
4. **`BuildQuery`** — только на построении плана: `SqlBuilder.MakeSelect`
   (`src/nextorm.core/DataContext/SqlBuilder.cs`) клонирует подготовленную команду, вызывает `BuildQuery` на
   клоне для каждого компонента и рендерит клон. Ваш обработчик меняет одноразовую копию, а не команду, которую
   держит вызывающий.
5. **Исполнение** — SQL сохранён в плане. Тёплое попадание переиспользует его: `BuildQuery` не вызывается,
   дескриптор не перестраивается, а захваченные значения и позиционные параметры перепривязываются к исходной
   команде. Повторный рендеринг для обновления параметров переиспользует форму, построенную на промахе, поэтому
   `BuildQuery` там тоже не вызывается.

Практически: **вы никогда не вызываете `Prepare` и `BuildQuery` сами**, и текст SQL никогда не попадает в ваш код.

### Правила, которые контракт накладывает на реализацию

- **Подготовка/валидация → неизменяемый подготовленный дескриптор.** Он валидирует комбинацию —
  неподдерживаемая бросает исключение, а не составляется произвольно, — и возвращает дескриптор со слотом,
  конкретным типом компонента, видом вклада, смысловым `Discriminator`, `StructuralHash` по всему состоянию и
  `StructurallyEquals`. Дескриптор неизменяем и разделяется кэшированным планом, поэтому не должен ссылаться на
  изменяемое состояние компонента.
- **Структурное построение на промахе кэша.** `BuildQuery` получает команду через
  `IQueryComponentBuildContext` и перестраивает утверждение из примитивов ядра: производный источник
  (`FromExpression`), исходный список выборки плюс скрытая вспомогательная колонка (`SelectExpression`,
  `SetComponentSelectList`), необязательный переписанный предикат (`SetComponentPreparedCondition`) и
  очищенная сортировка. Текст SQL он не рендерит — это делает ядро.
- **Идентичность слота.** `Slot` — стабильная идентичность логического оператора. Присоединение компонента,
  чей слот уже присутствует, **заменяет его на месте**; построитель и команда никогда не вкладывают два
  компонента одного слота. Поэтому `Min` и `Max` одного семейства делят слот и заменяют друг друга. Компоненты
  `AdditivePredicate` компонуются — каждый даёт предикат, объединяемый через `AND`, — а слотов
  `ExclusiveRowSource` может быть не больше **одного**: два разных эксклюзивных слота отвергаются на
  подготовке с [`BuildSqlCommandException`](xref:NextORM.Core.BuildSqlCommandException).
- **Безопасность коллизий скрытой колонки.** Вспомогательная колонка не должна ни протекать в результат, ни
  сталкиваться с mapped-колонкой. Выводите её псевдоним из метаданных цели
  ([`DataContextCache.Metadata`](xref:NextORM.Core.DataContextCache.Metadata)), держите её вне внешнего списка
  выборки и читайте обратно типизированным примитивом
  [`SqlFunctions.Column<T>`](xref:NextORM.Core.SqlFunctions.Column``1(System.Object,System.String)) — тогда
  пользовательская колонка с именем `rn`, `rank` или `__nextorm_ext_rn` не столкнётся и не проявится.
- **Без поддержки in-memory.** Компонент, чья семантика существует только в SQL, отвергает `InMemoryDataContext`
  в `Prepare` через `NotSupportedException`; он не должен молча деградировать или возвращать другие строки.

### Как компонент проходит через ядро

![Жизненный цикл компонента запроса: метод-расширение присоединяет компонент к неизменяемому построителю; ядро готовит его один раз на план, строит утверждение на промахе кэша и переиспользует кэшированный план на попадании, после чего исполнитель идёт обычным путём.](../assets/diagrams/query-component-lifecycle.svg)

Картинка — это весь контракт: ваш пакет даёт компонент, ядро один раз на план готовит его и строит по нему
SQL, а каждый следующий вызов переиспользует кэшированный план, и `BuildQuery` больше не выполняется.

**Синтетический** компонент — минимальное, что показывает форму. Это намеренно не рабочий оператор:
утверждение, вспомогательную колонку и подготовленное состояние проектируете вы, а
[пакет расширений](extensions-package.md) — поставляемая эталонная реализация полного оператора.

```csharp
public static class MyOperatorExtensions
{
    // Обычный метод-расширение: без регистрации, без изменений ядра, без сырого SQL.
    public static EntityBuilder<TEntity> SelectWhereBestEx<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector)
        => source.WithComponent(new BestRowComponent<TEntity>(valueSelector));
}

internal sealed class BestRowComponent<TEntity> : IQueryComponent
{
    private readonly LambdaExpression _valueSelector;

    public BestRowComponent(LambdaExpression valueSelector) => _valueSelector = valueSelector;

    // Один слот на логический оператор: присоединение другого компонента семейства заменяет его на месте.
    public Type Slot => typeof(BestRowComponent<TEntity>);

    // Что оператор привносит: аддитивный предикат либо единственный эксклюзивный источник строк.
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;

    // Выполняется один раз на построение плана, против подготавливаемой команды. Отвергайте то, что форма
    // выразить не может (исключение, а не произвольная композиция), и верните неизменяемое состояние для ключа кэша.
    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        RejectUnsupportedModifiers(context.Command);
        return new Descriptor(Slot, _valueSelector, context.ExpressionComparer);
        //      ^ слот, вид вклада, дискриминатор, StructuralHash, StructurallyEquals
    }

    // Выполняется только на промахе кэша. Перестраивает запрос из примитивов ядра — не из текста SQL.
    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var inner = context.Command.Clone();            // производный источник, payload сохранён
        // inner.SetComponentSelectList(<payload>, <вспомогательная колонка>);
        // inner.SetComponentPreparedCondition(<вспомогательная колонка> == <выжившие строки>);
        inner.SetComponentSorting(null);                // порядок задаёт сам оператор
        context.Command.From = new FromExpression(inner);
    }
}
```

Всё конкретное остаётся вашим: какие строки выживают, какую вспомогательную колонку вы добавляете и какой
псевдоним ей даёте, какие комбинации отвергаете. Квотирование, псевдонимы и привязку параметров держит ядро,
поэтому один и тот же компонент рендерится на каждом SQL-провайдере без диалект-специфичного кода — а на
попадании в тёплый кэш он вообще не участвует: кэшированный план возвращается, а захваченные и позиционные
параметры перепривязываются к исходной, неизменённой команде.

## См. также

- [Пакет расширений (`nextorm.extensions`)](extensions-package.md) — полный оператор на компонентах
- [Нативные стратегии выбора экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md)
- [Краткий справочник API](api-reference.md)
- [Обзор провайдеров](../connecting-providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/**`, `src/nextorm.core/Query/QueryComponents.cs`, `src/nextorm.postgres/**`, `src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
