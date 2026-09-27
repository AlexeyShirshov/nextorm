# guide-24-sql — цикл 1

**Задача:** добавить достоверные SQL-примеры в главу 24 гайда (EN+RU).
**Цель цикла:** в docs/guide/24-duration-columns.md и docs/ru/guide/24-duration-columns.md добавлены sql-блоки — DDL колонок по провайдерам и SQL фильтрации duration-колонки; EN/RU синхронны; DocFX 0/0.

**Критерии приёмки:**
- sql-блок DDL в §«Where the value is stored» / «Где хранится значение» (PG interval/interval(3); MySQL/MariaDB time/time(3); SQL Server/SQLite bigint; ClickHouse Int64/Nullable(Int64)).
- sql-блок фильтра в §«Queries and comparisons» / «Запросы и сравнения» (select id, estimate from tasks where (estimate > @p0)), с пометкой про значение параметра (нативный TimeSpan vs целое 300 сек) и $p0 для SQLite.
- EN и RU структурно идентичны; DocFX 0 warnings / 0 errors.

**Тест-стратегия:** docs-only — unit/integration не пишем; верификация `dotnet docfx docs/docfx.json` (0/0), ссылки не меняются.
**План доков:** только эти два файла.
**Перф-замер:** не нужен — только markdown-текст, рантайм-путь не затронут.
**DO-задачи:** статус-файл; EN; RU; CRLF+docfx.
**Риски:** некорректный DDL — типы строго диалектные (свидетельство /tmp/nextorm-duration-research-output.txt).

**Done / Verified:**
- EN docs/guide/24-duration-columns.md и RU docs/ru/guide/24-duration-columns.md: по 2 sql-блока (DDL + фильтр) и по 5 уточнений после CHECK.
- SQL сверен с реальной генерацией движка (лог /tmp/nextorm-duration-research-output.txt): PG interval/interval(3), MySQL/MariaDB time/time(3), SQL Server/SQLite bigint, ClickHouse Int64/Nullable(Int64), SQLite `$p0`, параметр 300 сек.
- EN/RU паритет; fences sql=2 / csharp=3 в каждом файле; CRLF корректен, LF-only строк нет.
- `dotnet docfx docs/docfx.json` — 0 warnings / 0 errors (логи /tmp/opencode/guide24-docfx.log, /tmp/opencode/guide24-docfx2.log).
- CHECK: аудит кода + тест/док/перф-линзы зелёные, вердикт PASS (loop-back не требовался); 2 Warning + 3 Suggestion закрыты.
- Изменения не закоммичены.

**Deferred + триггер:**
- Диапазон MySQL/MariaDB `TIME` (−838:59:59…838:59:59) не добавлен в §Details/Ограничения — вернуться при следующем редактировании главы 24.
- Внешние закладки/redirects на старые URL — вне цикла.

**Next plan:** новый цикл не требуется; отдельной задачей — пункт про диапазон MySQL `TIME`.
**Указатели:** docs/guide/24-duration-columns.md; docs/ru/guide/24-duration-columns.md; /tmp/nextorm-duration-research-output.txt; /tmp/opencode/guide24-docfx2.log.
