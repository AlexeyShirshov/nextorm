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

Публичный контракт — пять небольших типов: [`IQueryComponent`](xref:NextORM.Core.IQueryComponent) (слот, вид
вклада, `Prepare`, `BuildQuery`), [`IQueryComponentDescriptor`](xref:NextORM.Core.IQueryComponentDescriptor)
(неизменяемое подготовленное состояние), [`QueryComponentContribution`](xref:NextORM.Core.QueryComponentContribution)
(`AdditivePredicate` или `ExclusiveRowSource`) и контексты
[`IQueryComponentPrepareContext`](xref:NextORM.Core.IQueryComponentPrepareContext) /
[`IQueryComponentBuildContext`](xref:NextORM.Core.IQueryComponentBuildContext), которые ядро создаёт как
[`QueryComponentContext`](xref:NextORM.Core.QueryComponentContext). Команда раскрывает присоединённые компоненты
как [`QueryCommand.Components`](xref:NextORM.Core.QueryCommand.Components), а подготовленные дескрипторы — как
[`QueryCommand.PreparedComponents`](xref:NextORM.Core.QueryCommand.PreparedComponents); обоими управляет ядро, и
автор компонента обычно их не трогает.

- **Подготовка/валидация → неизменяемый подготовленный дескриптор.** `Prepare` выполняется один раз на
  построение плана, против подготавливаемой команды. Он валидирует комбинацию — неподдерживаемая бросает
  исключение, а не составляется произвольно, — и возвращает дескриптор со слотом, конкретным типом компонента,
  видом вклада, смысловым `Discriminator`, `StructuralHash` по всему состоянию и `StructurallyEquals`.
  Дескриптор неизменяем и разделяется кэшированным планом, поэтому не должен ссылаться на изменяемое состояние
  компонента.
- **Структурное построение только на промахе кэша.** `BuildQuery` вызывается при создании плана. Он получает
  команду через `IQueryComponentBuildContext` и перестраивает утверждение из примитивов ядра: производный
  источник (`FromExpression`), исходный список выборки плюс скрытая вспомогательная колонка (`SelectExpression`,
  `SetComponentSelectList`), необязательный переписанный предикат (`SetComponentPreparedCondition`) и
  очищенная сортировка. На подтверждённом **попадании в тёплый кэш** ядро возвращает кэшированный план, поэтому
  `BuildQuery` больше не вызывается и SQL не перестраивается; захваченные значения и позиционные параметры
  перепривязываются обычным путём исполнения к исходной, неизменённой команде.
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

Полный оператор в стиле `SelectWhereMin` — производный источник, окно, внешний `rank = 1`, восстановленная
исходная форма результата:

```csharp
using System.Linq.Expressions;
using System.Reflection;
using NextORM.Core;

namespace Acme.NextOrm;

// 1. Публичная поверхность: обычные методы-расширения. Без регистрации, без изменений ядра, без сырого SQL.
public enum ExternalExtremeKind { Min, Max }

public static class ExternalExtremeExtensions
{
    public static EntityBuilder<TEntity> ExternalSelectWhereMin<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(
            new ExternalExtremeComponent<TEntity>(ExternalExtremeKind.Min, valueSelector, ties, groupBy));

    public static EntityBuilder<TEntity> ExternalSelectWhereMax<TEntity, TValue>(
        this EntityBuilder<TEntity> source,
        Expression<Func<TEntity, TValue>> valueSelector,
        ExtremeRowTies ties = ExtremeRowTies.One,
        Expression<Func<TEntity, object?>>? groupBy = null)
        => source.WithComponent(
            new ExternalExtremeComponent<TEntity>(ExternalExtremeKind.Max, valueSelector, ties, groupBy));
}

// 2. Примитивы ядра, которые компонент вставляет в своё дерево выражений; разрешаются один раз на процесс.
internal static class EngineHandles
{
    public static readonly MethodInfo Asc =
        typeof(CommonFunctions).GetMethod("asc", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo Desc =
        typeof(CommonFunctions).GetMethod("desc", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo RowNumber =
        typeof(CommonFunctions).GetMethod("row_number", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo Rank =
        typeof(CommonFunctions).GetMethod("rank", BindingFlags.Public | BindingFlags.Instance)!;
    public static readonly MethodInfo ColumnOfInt =
        typeof(SqlFunctions).GetMethod("Column", BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(typeof(int));
    public static readonly MethodInfo Over = typeof(WindowFunction<int>).GetMethod("Over",
        new[] { typeof(Expression<Func<object?>>[]), typeof(WindowOrder[]), typeof(WindowFrame) })!;
}

// 3. Компонент: идентичность слота, валидация, подготовленный дескриптор, структурное построение.
public sealed class ExternalExtremeComponent<TEntity> : IQueryComponent
{
    private readonly ExternalExtremeKind _kind;
    private readonly LambdaExpression _valueSelector;
    private readonly ExtremeRowTies _ties;
    private readonly Expression<Func<TEntity, object?>>? _groupBy;

    public ExternalExtremeComponent(ExternalExtremeKind kind, LambdaExpression valueSelector,
        ExtremeRowTies ties, Expression<Func<TEntity, object?>>? groupBy)
    {
        ArgumentNullException.ThrowIfNull(valueSelector);
        _kind = kind;
        _valueSelector = valueSelector;
        _ties = ties;
        _groupBy = groupBy;
    }

    // Min и Max намеренно объявляют один слот: присоединение одного после другого заменяет его
    // на месте, а не вкладывает. Второй, другой эксклюзивный слот отвергается ядром.
    public Type Slot => typeof(ExternalExtremeComponent<TEntity>);
    public QueryComponentContribution Contribution => QueryComponentContribution.ExclusiveRowSource;

    public IQueryComponentDescriptor Prepare(IQueryComponentPrepareContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Command.DataContext is InMemoryDataContext)
            throw new NotSupportedException(
                "ExternalSelectWhereMin/Max is not supported by the in-memory provider.");
        if (_ties is not (ExtremeRowTies.One or ExtremeRowTies.All))
            throw new ArgumentOutOfRangeException(nameof(_ties), _ties, "Unknown ExtremeRowTies value.");

        // Проверки формы конкретного оператора — здесь: отвергайте то, что оператор не умеет
        // выразить (например, пагинацию или HAVING), вместо произвольной композиции.

        var hash = new HashCode();
        hash.Add(Slot);
        hash.Add(GetType());
        hash.Add(_kind);
        hash.Add(_ties);
        hash.Add(_valueSelector, context.ExpressionComparer);
        if (_groupBy is not null)
            hash.Add(_groupBy, context.ExpressionComparer);

        return new Descriptor(this, hash.ToHashCode(), context.ExpressionComparer);
    }

    public void BuildQuery(IQueryComponentBuildContext context)
    {
        var cmd = context.Command;
        var entityType = typeof(TEntity);
        var rankAlias = PickFreeAlias(entityType);
        var entity = Expression.Parameter(entityType, "x");
        var sql = Expression.Property(null, typeof(SqlFunctions), "Sql");

        // 4. Производный источник: исходная команда, её список выборки плюс одна скрытая колонка ранга.
        var inner = cmd.Clone();
        inner.Components = null;

        var direction = _kind == ExternalExtremeKind.Max ? EngineHandles.Desc : EngineHandles.Asc;
        var orderKeys = Expression.NewArrayInit(typeof(WindowOrder),
            Decompose(_valueSelector, entity).Select(v => (Expression)Expression.Call(sql, direction,
                Expression.Quote(Expression.Lambda<Func<object?>>(
                    Expression.Convert(v, typeof(object)))))).ToArray());

        Expression partition = _groupBy is null
            ? Expression.Constant(null, typeof(Expression<Func<object?>>[]))
            : Expression.NewArrayInit(typeof(Expression<Func<object?>>),
                Decompose(_groupBy, entity).Select(k => (Expression)Expression.Quote(
                    Expression.Lambda<Func<object?>>(Expression.Convert(k, typeof(object))))).ToArray());

        var rankMethod = _ties == ExtremeRowTies.All ? EngineHandles.Rank : EngineHandles.RowNumber;
        var rank = Expression.Call(Expression.Call(sql, rankMethod), EngineHandles.Over,
            partition, orderKeys, Expression.Constant(null, typeof(WindowFrame)));

        var hidden = new SelectExpression(typeof(int))
        {
            Index = inner.SelectList?.Length ?? 0,
            PropertyName = rankAlias,
            Expression = rank,
        };
        inner.SetComponentSelectList(inner.SelectList is null
            ? new[] { hidden }
            : inner.SelectList.Append(hidden).ToArray());
        inner.SetComponentSorting(null);

        // 5. Внешнее утверждение: читаем производный источник, оставляем rank = 1, восстанавливаем форму.
        cmd.From = new FromExpression(inner);
        var outer = Expression.Parameter(entityType, "o");
        var rankColumn = Expression.Call(null, EngineHandles.ColumnOfInt, outer,
            Expression.Constant(rankAlias));
        cmd.SetComponentPreparedCondition(Expression.Lambda(
            Expression.Equal(rankColumn, Expression.Constant(1)), outer));
    }

    private static Expression[] Decompose(LambdaExpression selector, ParameterExpression entity)
    {
        var body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } boxed && boxed.Type == typeof(object))
            body = boxed.Operand;
        var rewritten = new ParameterReplacer(selector.Parameters[0], entity).Visit(body)!;
        // Анонимные типы и ValueTuple дают по ключу на аргумент, а не один непрозрачный ключ.
        return rewritten is NewExpression { Arguments.Count: > 0 } composite
            ? composite.Arguments.ToArray()
            : new[] { rewritten };
    }

    private static string PickFreeAlias(Type entityType)
    {
        const string seed = "__nextorm_ext_rn";
        if (!DataContextCache.Metadata.TryGetValue(entityType, out var metadata))
            return seed;
        var name = seed;
        while (metadata.Properties.Any(p =>
                   string.Equals(p.ColumnName, name, StringComparison.OrdinalIgnoreCase)))
            name += "_";
        return name;
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;

        public ParameterReplacer(ParameterExpression from, ParameterExpression to)
        {
            _from = from;
            _to = to;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _from ? _to : base.VisitParameter(node);
    }

    private sealed class Descriptor : IQueryComponentDescriptor
    {
        private readonly LambdaExpression _valueSelector;
        private readonly Expression? _groupBy;
        private readonly ExtremeRowTies _ties;
        private readonly IEqualityComparer<Expression> _comparer;

        public Descriptor(ExternalExtremeComponent<TEntity> component, int hash,
            IEqualityComparer<Expression> comparer)
        {
            Slot = component.Slot;
            ComponentType = component.GetType();
            Contribution = component.Contribution;
            Discriminator = component._kind.ToString();
            _valueSelector = component._valueSelector;
            _groupBy = component._groupBy;
            _ties = component._ties;
            _comparer = comparer;
            StructuralHash = hash;
        }

        public Type Slot { get; }
        public Type ComponentType { get; }
        public QueryComponentContribution Contribution { get; }
        public string Discriminator { get; }
        public int StructuralHash { get; }

        public bool StructurallyEquals(IQueryComponentDescriptor? other)
            => other is Descriptor rhs
               && Slot == rhs.Slot && ComponentType == rhs.ComponentType
               && Contribution == rhs.Contribution
               && Discriminator == rhs.Discriminator && _ties == rhs._ties
               && _comparer.Equals(_valueSelector, rhs._valueSelector)
               && (_groupBy is null
                   ? rhs._groupBy is null
                   : rhs._groupBy is not null && _comparer.Equals(_groupBy, rhs._groupBy));
    }
}
```

Квотирование, псевдонимы и привязку параметров держит ядро; компонент только собирает логическое дерево,
поэтому один и тот же оператор рендерится на каждом SQL-провайдере без диалект-специфичного кода. Тот же
оператор — оба направления, `One`/`All`, композитные ключи, исключение NULL-значений и проверки
совместимости — сквозным образом прогоняется набором доказательств компонентов запроса в репозитории.

## См. также

- [Нативные стратегии выбора экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md)
- [Краткий справочник API](api-reference.md)
- [Обзор провайдеров](../connecting-providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/**`, `src/nextorm.core/Query/QueryComponents.cs`, `src/nextorm.postgres/**`, `src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
