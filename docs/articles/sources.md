# Источники — навигация

## Исторические версии

| Дата (коммит) | Документ | Для каких тезисов |
|---|---|---|
| [20.09.2023](https://github.com/AlexeyShirshov/nextorm/blob/7ee4651280ed2f902297e14bf562d89cea281a30/readme.md) | README | Отказ от ручного SQL; C# type checking / IntelliSense; запросы без сущностей; начальная формулировка цели |
| [09.12.2023](https://github.com/AlexeyShirshov/nextorm/blob/40c792cb09ffb6713da60d3c19f01384d76b2de2/readme.md) | README | Цель «as fast as Dapper»; отсутствие change tracking; компиляция и параметризация |
| [05.01.2024](https://github.com/AlexeyShirshov/nextorm/blob/5e6e6e8b06bbebc274d7da4970cbe249dfe9e7fc/nextorm.core/docs/ru/overview.md) | «Введение», первая содержательная версия | Генерация SQL; маппинг; слой доступа к данным |
| [05.01.2024](https://github.com/AlexeyShirshov/nextorm/blob/5e6e6e8b06bbebc274d7da4970cbe249dfe9e7fc/nextorm.core/docs/ru/motivation.md) | «Постановка задачи», первая содержательная версия | Три подхода к доступу к данным; плюсы/минусы; формулировка компромисса Nextorm |
| [09.01.2024](https://github.com/AlexeyShirshov/nextorm/blob/e3186601983a7b6180ebbeda3fb42a12c39c0869/nextorm.core/docs/ru/overview.md) | «Введение», оформленная версия | Редакция с заголовком |
| [09.01.2024](https://github.com/AlexeyShirshov/nextorm/blob/e3186601983a7b6180ebbeda3fb42a12c39c0869/nextorm.core/docs/ru/motivation.md) | «Постановка задачи», оформленная версия | Таблица сравнения подходов |

## Текущие материалы

- [README](https://github.com/AlexeyShirshov/nextorm/blob/main/readme.md) — актуальное позиционирование и примеры.
- [Русское «Введение»](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/overview.md) — текущая версия.
- [Русская «Постановка задачи»](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/motivation.md) — текущая версия.
- [Сравнения](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/comparisons/index.md) — сопоставление с другими библиотеками.
- [Бенчмарки](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/comparisons/benchmarks.md) — проверка утверждений о производительности.
- [Ограничения](https://github.com/AlexeyShirshov/nextorm/blob/main/docs/ru/advanced/limitations.md) — границы подхода.

## История расположения документов

- 2023–2024: `nextorm.core/docs/ru/`.
- Сентябрь 2026, промежуточный путь: `src/nextorm.core/docs/ru/` — [коммит переноса](https://github.com/AlexeyShirshov/nextorm/commit/afb01897ff226019415130162f5c86c3504bcd17).
- Сентябрь 2026 — настоящее время: `docs/ru/` — [коммит переноса](https://github.com/AlexeyShirshov/nextorm/commit/16ded18352ec476163f28b0a0f4f931ea635b3e5).

## Замечания о датах

- Русские файлы присутствовали в дереве 09.12.2023, но без содержательного текста; текст добавлен 05.01.2024.
- В истории ветки `main` за 2025 год коммитов не обнаружено.
- Ссылки с SHA фиксируют редакцию; ссылки `main` могут измениться.
