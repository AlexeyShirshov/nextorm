# План релиза 1.0.3.1-alpha (hotfix поверх 1.0.3-alpha)

> Патч-релиз поверх `1.0.3-alpha`. Причина — регрессия SQL-генерации: у производного запроса, в
> проекции которого используется источник из вложенного (nested) запроса, пропадали столбцы, из-за
> чего запрос падал (`syntax error at or near ","`) или бросал
> `InvalidOperationException: Operation is not valid due to the current state of the object`.
> Затронуты PostgreSQL, SQL Server, MySQL/MariaDB и ClickHouse.
>
> **Статус: патч подготовлен**, интеграция в `main` и публикация — по шагам §4.

- **Тег:** `v1.0.3.1-alpha`
- **Базовая версия:** `1.0.3-alpha` (помечается deprecated на nuget.org)
- **Версии пакетов:** `1.0.3.1-alpha` во всех 7 `src/*.csproj` (CI дополнительно переопределяет версию
  тегом через `-p:Version` — источник истины тег; бамп нужен локальному `dotnet pack` и зависимостям nuspec)
- **Публикация:** CI (`dotnet.yml`, job `publish`), **tag-triggered** (`refs/tags/v*`) через NuGet
  trusted publishing (OIDC, `NuGet/login@v1`)

## 1. Причина и скоуп

Регрессия внесена коммитом `9f97714`. `DefaultColumnsProvider.PopSourceScope` помечает источники
вложенного запроса как вышедшие из области видимости (`Item4 = true`); при вычислении выходных колонок
производного запроса повторно рендерится проекция, ссылающаяся на источники этого вложенного запроса, и
разрешение колонок их не находит.

Исправление: пометку сохраняем (она нужна для различения однотипных внешних/вложенных алиасов), но для
рекурсивного рендера колонок производного запроса добавляем флаг `IncludeNestedSources`, при котором
поиск сначала идёт по вложенным источникам.

## 2. Изменения

| Файл | Что |
| --- | --- |
| `src/nextorm.core/Visitors/VisitorOptions.cs` | флаг `IncludeNestedSources` |
| `src/nextorm.core/Visitors/BaseExpressionVisitor.cs` | проксирование флага |
| `src/nextorm.core/DataContext/SqlBuildContext.cs` | перенос флага в построители колонок/WHERE |
| `src/nextorm.core/Query/IColumnsProvider.cs` | перегрузки `FindAlias`/`FindQueryCommand` с `includeNestedSources` |
| `src/nextorm.core/Query/DefaultColumnsProvider.cs` | nested-first поиск (`FindAliasInScope`, `FindQueryCommandInScope`) |
| `src/nextorm.core/Visitors/MemberTranslator.cs` | рекурсивный `MakeColumn` с `IncludeNestedSources = true` |
| `src/nextorm.core/Visitors/AliasResolver.cs` | проброс флага в разрешение алиасов |
| `tests/nextorm.postgres.tests/SqlGenerationTests.cs` | регрессионный тест `DerivedSourceOverDerivedWithWindow_ShouldResolvePassThroughColumns` |

## 3. Предрелизные проверки

- [x] Unit / SQL-gen тесты (`dotnet test -c Debug` по всем `tests/nextorm.<provider>.tests`) —
      **1223 passed, 0 failed** (core 192, sqlite 268, sqlserver 238, postgres 248, mysql 65, mariadb 29,
      clickhouse 183); новых — 1 регрессионный.
- [x] Примеры (Podman): postgres `11/11`, mssql `11/11`, clickhouse `8/11` (не покрыто осознанно:
      `ArrayAnalytics`, `Incremental`, `Retention`). До фикса на `1.0.3-alpha`: `7/11`, `6/11`, `7/11`.
- [ ] Бамп версий — сделать (§4, шаг 1).
- [ ] `docs/index.md` (Status + Releases) — сделать (§4, шаг 1).

## 4. Публикация (шаги)

1. Применить патч `nextorm-derived-columns-fix.patch` на `main` (`git apply`), собрать и прогнать тесты;
   проверить бамп `<Version>` = `1.0.3.1-alpha` в 7 `src/*.csproj` и правки `docs/index.md`.
2. **Прогнать примеры** из `examples/` против Podman и сверить с `examples/VERIFICATION.md`: postgres `11/11`,
   mssql `11/11`, clickhouse `8/11` (допустимые `FAIL` — только `ArrayAnalytics`, `Incremental`,
   `Retention`). Любой другой `[FAIL]` или отсутствие `n/m succeeded` — реальная регрессия, релиз не
   выпускать. Механика (DOCKER_HOST, detached-запуск, кэш датасетов, OOM) —
   `.opencode/skills/running-examples/SKILL.md`. Именно прогон примеров выявил эту регрессию, поэтому шаг
   обязателен и для будущих релизов.
3. Закоммитить, запушить `main`.
4. Создать и запушить тег `v1.0.3.1-alpha` на коммите с фиксом.
5. CI `publish` соберёт и отправит 7 пакетов на nuget.org (проверить «Your package was pushed»).
6. Пометить `1.0.3-alpha` как deprecated на nuget.org.
