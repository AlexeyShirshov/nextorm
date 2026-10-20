# Расширяемость: пользовательские SQL-диалекты и рендереры

> nextorm рендерит SQL через [`ISqlDialect`](xref:NextORM.Core.ISqlDialect); поддерживаемая граница расширяемости позволяет внешней сборке поставлять собственный диалект и, для нативной стратегии экстремальной строки, собственный рендерер — без friend-доступа.

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

## См. также

- [Нативные стратегии выбора экстремальной строки (`SelectWhereMax` / `SelectWhereMin`)](select-where-extrema-native.md)
- [Краткий справочник API](api-reference.md)
- [Обзор провайдеров](../connecting-providers/overview.md)

---

Source: `src/nextorm.core/DataContext/Dialect/**`, `src/nextorm.postgres/**`, `src/nextorm.clickhouse/**` (XML doc comments are the authoritative API documentation).
