# Почему не продолжили WORM: тезисы автора

> Источник мотивации: устное объяснение автора от 27.10.2026. Это авторские воспоминания, а не выводы из Git-истории. Только тезисы; прозу для публикации пишет автор.

## Причины начать новую библиотеку

- **Накопленный legacy-код в WORM / OML:**
  - Большой объём кода, написанного за годы разработки.
  - Сложность сопровождения и дальнейшего развития.
  - Переработка прежней архитектуры выглядела менее привлекательной, чем новый старт.
- **Выбор языка:**
  - WORM реализован главным образом на **Visual Basic .NET**.
  - К моменту работы над Nextorm VB.NET стал, по оценке автора, нишевым по сравнению с **C#**.
  - C# был выбран для новой реализации как более активно развивавшийся язык с более широким применением в .NET.
  - Не превращать авторскую оценку положения языков в неподтверждённое официальное заявление Microsoft.
- **Пересмотр идеи «кэшировать всё»:**
  - Изначальная установка WORM: максимально широкое кэширование, в том числе параметров/подготовки запросов и **результатов выполнения запросов**.
  - Накопленный опыт: подробное кэширование породило множество проблем.
  - Решение при создании Nextorm: отказаться от кэширования результатов; сохранить **кэширование планов запросов**.
  - Ключевое различие: **повторное использование плана ≠ повторное использование полученных строк данных**.

## Проверенные технические опорные точки

- **WORM — изменения сущностей:** [`Entity.vb`, состояния объектов](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Entity/Entity.vb#L365-L402), [`CachedEntity.vb`, `SaveChanges`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Entity/CachedEntity.vb#L1240-L1253).
- **WORM — кэш результатов:** [`OrmCache.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Cache/OrmCache.vb), [`CacheItem.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/obm/Cache/CacheItem.vb).
- **WORM — тест инвалидации:** [`TestTableFilterUpdate`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/Cache.vb#L168-L197) — первый запрос `CacheHit=False`, повторный `True`, изменение сущности, новый запрос `False`.
- **WORM — широкое тестирование кэша:** [`Cache.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/Cache.vb), [`ImmCache.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/ImmCache.vb), [`SmartCache.vb`](https://github.com/AlexeyShirshov/oml/blob/Worm2010/TestProject1/Query/SmartCache.vb).
- **Nextorm — явно задокументированный механизм:** [описание конвейера и кэша планов](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/engine/index.md), [`Prepare()` и повторное использование](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/engine/03-plan-cache-and-reuse.md).

## Что ещё нужно уточнить у автора

- **Какие именно проблемы** вызывало кэширование результатов WORM?
  - Инвалидация при изменениях?
  - Согласованность между транзакциями/процессами?
  - Управление жизненным циклом данных и расходом памяти?
  - Сложность реализации и поддержки?
  - Другие конкретные случаи?
- Были ли отдельные проблемы от `change tracking`, помимо сложности legacy-кода?
- Какие элементы архитектуры WORM всё-таки стали основой Nextorm?
- Какой конкретный практический случай лучше всего иллюстрирует переход от кэша результатов к кэшу планов?

## Связанные заметки

- [Исследовательский бриф OML → Nextorm](oml-history-brief.md).
- [Тезисы первой статьи](01-problem-statement.md).
- [Карта источников](sources.md).
