# План выпуска Nextorm 1.0.10

**Статус: подготовка, без публикации.** Ветка `1.0.10`, база `v1.0.9`, планируемый
стабильный тег `v1.0.10`.

- Скоуп: [майлстон 1.0.10](https://github.com/AlexeyShirshov/nextorm/milestone/5) и
  [изменения с 1.0.9](https://github.com/AlexeyShirshov/nextorm/compare/v1.0.9...1.0.10).
- Публичные заметки: [EN](../index.md#1010) и [RU](../ru/index.md#1010).
- Пакеты: `nextorm`, `nextorm.sqlite`, `nextorm.sqlserver`, `nextorm.postgres`, `nextorm.mysql`,
  `nextorm.mariadb`, `nextorm.clickhouse`, `nextorm.extensions`, `nextorm.entityframeworkcore`.
  Каждый пакет содержит ассеты `net10.0` и `net11.0`.

## Проверки до публикации

- [x] На исходном коммите подготовки `003b30740`
  [веточный CI](https://github.com/AlexeyShirshov/nextorm/actions/runs/44233982776) прошёл jobs
  `build` и `aot`. `publish` пропущен. Этот прогон не заменяет coverage-гейты на `main`.
- [x] [#392](https://github.com/AlexeyShirshov/nextorm/issues/392) закрыта после проверки:
  8 unit-тестов `Vector` и 7 интеграционных тестов PostgreSQL/SQL Server прошли без пропусков.
- [x] [#397](https://github.com/AlexeyShirshov/nextorm/issues/397) закрыта после проверки навигации
  и обратных ссылок, 20 локальных ссылок и якорей, 9 одинаковых блоков примеров и вывода, CRLF.
- [x] В майлстоне 13 закрытых задач, открытых нет. Сам майлстон остаётся открытым до публикации.
- [x] Согласована замена низкоуровневого публичного API для коррелированных подзапросов.
  Коммит `a3272350` заменил публичный `OuterRefMarker<T>` внутренним необобщённым маркером для
  NativeAOT. В `1.0.10` расширения используют `OuterReference.Create(IQueryRegistry, Expression)`:
  метод регистрирует внешнее выражение и возвращает узел с тем же CLR-типом.
  `CompatibilitySuppressions.xml` в ядре допускает только два удаления на обоих TFM —
  `OuterRefMarker<T>` (`CP0001`) и
  `ReplaceConstantsExpressionVisitor.GetOuterRefMarkerCI(Type)` (`CP0002`).
  ApiCompat по-прежнему сравнивает пакеты с опубликованной версией `1.0.9`.
- [x] Внешний потребитель без `InternalsVisibleTo` проверяет регистрацию выражений и сохранение
  их CLR-типов (32 теста в `nextorm.publicextensibility.tests`). Свежий NativeAOT-раннер
  `.NET 11 / linux-x64` прошёл `AotNativeRunTests` с `NEXTORM_AOT_EVIDENCE=1`, включая сверку
  коррелированных запросов с JIT.
- [x] Все девять проектов локально упакованы с `-p:Version=1.0.10` и активным ApiCompat,
  как в [job `publish`](../../.github/workflows/dotnet.yml). Для ядра:

  ```bash
  dotnet pack src/nextorm.core/nextorm.core.csproj -c Release -p:Version=1.0.10
  ```

  Файл `.nupkg` может появиться до ошибки ApiCompat: проверяйте код возврата команды.

## Публикация владельцем

1. После успешной упаковки отправьте подготовленные изменения в ветку `1.0.10` и влейте её в
   `main` через PR.
2. На итоговом коммите `main` дождитесь успешных `build` и `aot`, включая оба гейта покрытия,
   и деплоя документации EN/RU. Проверьте раздел `1.0.10` в обеих версиях сайта.
3. Создайте и отправьте тег `v1.0.10` на проверенном коммите `main`. Тег запускает `publish`:
   CI упаковывает девять проектов с версией из тега и отправляет их на NuGet через OIDC.
4. Проверьте успешный `publish` и наличие версии `1.0.10` на nuget.org у всех девяти пакетов.
5. Опубликуйте стабильный GitHub Release для `v1.0.10`: используйте публичные заметки и
   ссылку на сравнение `v1.0.9...v1.0.10`. Закройте майлстон.
6. После публикации обновите `Version` в `Directory.Build.props`, `NextOrmVersion` в
   `examples/Directory.Packages.props` и проверьте примеры на опубликованных пакетах.
   До публикации эти значения остаются прежними: CI переопределяет версию при упаковке,
   а примеры потребляют доступные пакеты NuGet.
