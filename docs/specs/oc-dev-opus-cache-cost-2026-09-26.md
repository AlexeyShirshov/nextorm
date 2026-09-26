# Профиль `oc-dev`: полный отчёт по сессии 2026-09-26

От диагностики запуска профиля до подтверждённой экономии на prompt-cache Opus.

- Дата: 2026-09-26 (время локальное, UTC+5; в логах opencode — UTC).
- Окружение: WSL Ubuntu-22.04, opencode 1.18.32, репозиторий `~/sources/opencode-config` (снимок `~/.config/opencode`).
- Рабочий проект: `~/sources/nextorm` (C# ORM).
- Модели: «мозг» `opencode/claude-opus-5-5` (через OpenCode Zen), «руки» `deepseek/deepseek-flash`.

## 0. Резюме

1. **`oc-dev` стартовал не тем агентом/моделью** — потому что запущенный инстанс был не `oc-dev`, а `oc-ds`, а флаг `-c` продолжает старую сессию, сохраняя её агент/модель. Конфиг профиля был корректен.
2. **«Unexpected server error» при запуске** — баг `opencode -c`, когда в проекте нет сессии для продолжения: TUI ставит placeholder `sessionID:"dummy"` → сервер отдаёт 500 → тост. Частный триггер — `/clear`, который удаляет сессию. Фикс: убрать `-c` из алиаса `oc-dev` (заодно это правильно для PDCA — старт всегда в `plan`/Opus).
3. **Главная находка по стоимости**: Opus-сессия дорога не из-за ставки модели, а из-за **холодных перезаписей prompt-cache** — 5-минутный TTL истекает, пока оркестратор ждёт субагентов 5–10 мин.
4. **Решение**: локальный `zen-cache-proxy`, который добавляет `cache_control.ttl="1h"`. A/B-замер: «холодный» ход дешевле **~23×**; в реальной сессии ход с паузой 7.3 мин показал `cache_write=0`, `cache_read=89287` (−$0.43 на одном ходу).
5. Попутно: методика статистики через `opencode db` (встроенный sqlite), грабли с alias→function в bash и `/dev/tcp` в WSL.

## 1. Исходная точка

- Репозиторий `opencode-config` хранит снимок `~/.config/opencode`; профили открываются алиасами: `oc-ds`, `oc-gp`, `oc-dev` (`OPENCODE_CONFIG=.../profiles/<name>.jsonc opencode ...`), `oc-sandbox` — отдельный.
- Профиль `oc-dev.jsonc`: `default_agent: "plan"`, `model: opencode/claude-opus-5-5`, субагенты (`coder`/`explore`/`general`/`dotnet-*`) на `deepseek/deepseek-flash`, `small_model` — deepseek; `dotnet-security-reviewer` — на Opus.
- Две жалобы на старте: (A) запуск `oc-dev` даёт агента `build` и модель «ds 4.1»; (B) затем — «Unexpected server error».

## 2. Проблема A — `oc-dev` стартует не тем агентом/моделью

**Диагностика.** Проверка процесса, из которого шло сообщение: pid был запущен с `OPENCODE_CONFIG=.../deepseek.jsonc` и cwd `~/sources/opencode-config` — т.е. это был **`oc-ds`**, а не `oc-dev`. Плюс все алиасы тогда использовали `opencode -c`.

**Причина.** `-c` (continue) **продолжает последнюю сессию** и сохраняет её агент/модель. `default_agent`/`model` применяются только к **новой** сессии, поэтому `oc-dev -c` не давал `plan`/Opus.

**Проверка конфига.** `OPENCODE_CONFIG=.../oc-dev.jsonc opencode debug config` подтвердил: `default_agent: "plan"`, `model: "opencode/claude-opus-5-5"`, `build`/`plan` = Opus, субагенты = DeepSeek. Конфиг корректен.

## 3. Проблема B — «Unexpected server error» при запуске `oc-dev`

**Симптом.** `oc-dev` (любой профиль с `-c`) в проекте, где нет сессии для продолжения, показывает тост `Unexpected server error. Check server logs for details.`

**Лог.** Две ошибки в одну миллисекунду:
```
level=ERROR message=failed
error="Error: Expected a string starting with \"ses\", got \"dummy\""
```
Причина: TUI при `-c` задаёт начальный роут `{ type:"session", sessionID:"dummy" }` и до резолва реальной сессии шлёт запросы по этому id → сервер 500 → тост.

**Воспроизведение** (чистая папка):
| запуск | результат |
|---|---|
| `opencode -c` (нет сессий) | «Unexpected server error» |
| `opencode` (без `-c`) | чисто |
| `opencode -c` (сессия есть) | чисто |

**Триггер в бою.** Пользователь перед этим сделал `/clear` (плагин `clear-session.js` удаляет сессию) → у проекта не осталось сессий → `oc-dev -c` упал.

**Фикс.** Убрать `-c` из алиаса `oc-dev` (каждый запуск — новая сессия, старт в `plan`/Opus). Это ещё и методологически верно: `-c` сохраняет старый агент/модель и ломает PDCA-старт.

## 4. Методика мониторинга и статистики

`sqlite3` в системе нет, но у opencode есть **встроенный доступ к БД**: `opencode db "<SQL>"` (`--format json|tsv`), `opencode db path`. БД: `~/.local/share/opencode/opencode.db`.

Ключевые таблицы:
- `session` — `agent`, `model` (JSON), `cost`, `tokens_input/output/reasoning/cache_read/cache_write`, `directory`, `parent_id`, `time_created/updated`, `title`;
- `message` — `session_id`, `data` (JSON: `role`, `agent`, `modelID`, `tokens.*`, `cost`);
- `part` — `data` (JSON: `type`, `tool`, `state.status`, `text`).

Рабочий запрос «за сегодня» (локально):
```bash
opencode db --format json "select model, agent, count(*) sessions, round(sum(cost),4) cost, \
sum(tokens_input) tin, sum(tokens_output) tout, sum(tokens_reasoning) treason, \
sum(tokens_cache_read) tcr, sum(tokens_cache_write) tcw from session \
where time_updated >= $(date -d 'today 00:00' +%s)000 group by model, agent order by cost desc"
```
Важно: `opencode stats --days 1` считает **окно 24 часа**, а не «сегодня» — числа расходятся.

Активные инстансы и их профили: `ps` + `/proc/<pid>/environ` (`OPENCODE_CONFIG`), `/proc/<pid>/cwd`.

## 5. Анатомия стоимости Opus-сессии

Сессия `ses_f238b219fffeIZRKzEyrQPqIlj` («Группировка 4 задач для версии 1.0.8-b»), cwd `~/sources/nextorm`. Ставки Opus 5.5 ($/M), выведены из данных сессии (сходятся точно): **input 4, output 20, reasoning 20, cache write 5, cache read 0.20**.

Снимок дерева на 12:58 — 10 сессий, `$8.5254`:

| сессия | agent · model | $ |
|---|---|---|
| `ses_f238b219…` root | plan→build · opus | 5.8115 |
| `ses_f236122b…` | dotnet-code-review-agent · **opus** | 2.1452 |
| 7 субагентов | coder/explore · deepseek | 0.5687 |

Opus = **93%**. Сплит стоимости root (24 хода):

| компонент | $ | доля |
|---|---|---|
| cache **write** (927 505 ток.) | **4.6375** | **80%** |
| output + reasoning (43 755 ток.) | 0.8751 | 15% |
| cache read (1 493 049 ток.) | 0.2986 | 5% |

Гипотеза «если бы субагенты были на Opus» (применены ставки Opus к их токенам): `coder+explore` $0.0919 → **$3.4804 (×38)**, дерево $1.85 → $5.24. Т.е. дешёвые исполнители — принципиальная часть экономики профиля.

## 6. Диагноз: 5-минутный TTL prompt-cache

Все дорогие ходы имеют `cache_read=0` (промах). Их 8 из 24, суммарно **859 380 ток. = $4.30 (74% стоимости root)** — и каждый следует после паузы **>5 минут** (ожидание субагентов):

| время | пауза | cacheW | $ |
|---|---|---|---|
| 11:44:01 | 0 (первый ход) | 55 389 | 0.2797 |
| 11:56:29 | 8.5 мин | 72 395 | 0.3713 |
| 12:03:53 | 5.3 мин | 79 121 | 0.4049 |
| 12:16:24 | 8.5 мин | 96 429 | 0.5077 |
| 12:27:12 | 7.7 мин | 123 058 | 0.6344 |
| 12:38:01 | 8.8 мин | 139 221 | 0.7861 |
| 12:45:48 | 7.8 мин | 143 460 | 0.7586 |
| 12:55:35 | 9.8 мин | 150 307 | 0.7849 |

Контекст растёт 55K→150K, поэтому поздние перезаписи дороже. У `dotnet-code-review-agent` та же картина (61% стоимости — cache write, включая паузу 14 мин → перезапись 117 347 ток. за $0.5889).

Вывод: дорогой не Opus, а **повторная запись всего контекста после протухания кэша**.

## 7. Почему TTL нельзя было просто поднять в конфиге

- В бинарнике opencode Anthropic-интеграция умеет `cache_control: {type:"ephemeral", ttl:"1h"}` (есть `ephemeral`/`ttl`, zod `ttl: "5m"|"1h"`), но применяется это только к route `anthropic-messages`/`bedrock-converse`, и **пользовательского ключа** `cache`/`ttl`/`ttlSeconds` в `opencode.json` нет; env `OPENCODE_*CACHE*` тоже нет. Без `ttlSeconds` код ставит `{type:"ephemeral"}` → 5 мин.
- Выяснилось (захватом трафика), что `opencode/opus` — это **OpenCode Zen** (`https://opencode.ai/zen/v1`), и рабочий протокол там — **Anthropic Messages**: `POST /v1/messages` с `x-api-key`/`anthropic-version`, а в теле — `cache_control:{type:"ephemeral"}` без `ttl`. (Запись провайдера в `models.json` помечена как `@ai-sdk/openai-compatible`, но фактически используются `/messages` и `/responses`.)

Значит, TTL можно удлинить только «на проводе» — прокси.

## 8. Решение: `zen-cache-proxy`

Локальный реверс-прокси перед Zen, добавляет `ttl:"1h"` каждому ephemeral-блоку `cache_control`.

- Код: `~/.config/opencode/tools/zen-cache-proxy.mjs` (источник: `opencode-config/config/tools/zen-cache-proxy.mjs`).
- Подключение: в `profiles/oc-dev.jsonc` → `provider.opencode.options.baseURL = "http://127.0.0.1:8787/v1"`.
- Алиас: `oc-dev` стал функцией — `_oc_zen_proxy_ensure` проверяет `GET /__health` и поднимает прокси, если он не запущен.
- ENV: `UPSTREAM`, `PORT` (8787), `REWRITE` (`ttl`|`add`|`none`), `LOG=1` + `CAPTURE_DIR` (дамп тел; ключи редактируются).
- Безопасность: секретов не хранит (проксирует заголовки клиента как есть), слушает только `127.0.0.1`.

Проверка: тело `/messages` до и после — блоки `system[0]` и `messages[...]` становятся `{'type':'ephemeral','ttl':'1h'}`; апстрим отвечает 200.

## 9. A/B-замер

Одна модель, две изолированные сессии (разные system-маркеры, чтобы не делили общий cache-префикс), по два хода с паузой **6m53s** (>5m, <1h). Отличие — только `baseURL` (через прокси / напрямую).

| arm | ход | cacheW | cacheR | повтор | $ |
|---|---|---|---|---|---|
| PROXY (ttl=1h) | 1 | 15 841 | 0 | — | 0.0793 |
| | 2 | **19** | **15 841** | **100%** | **0.0034** |
| DIRECT (ttl=5m) | 1 | 15 840 | 0 | — | 0.0793 |
| | 2 | **15 859** | **0** | **0%** | **0.0794** |

**Второй ход: $0.0034 против $0.0794 — дешевле ~23×.**

## 10. Живое подтверждение в реальной сессии

После перезапуска `oc-dev` (процесс, стартовавший после правки конфига) в той же сессии `ses_f238b219`:

| ход | пауза | cacheW | cacheR | out+reas | факт $ | было бы при 5m |
|---|---|---|---|---|---|---|
| 13:44:21 | 38 мин | **0** | 81 987 | 1 337 | 0.0432 | 0.4367 |
| **13:54:46** | **7.3 мин** | **0** | **89 287** | 4 417 | **0.1062** | **0.5348** |

Ход с паузой 7.3 мин (раньше — гарантированная холодная перезапись) теперь читает кэш: `cache_write=0`, `cache_read=89287`, **−$0.43 на одном ходу**. Прокси: все `/messages` → `changed=3`, 200.

## 11. Инциденты и грабли

- **bash: alias vs function.** Замена `alias oc-dev=...` на функцию `oc-dev() {` дала `syntax error near unexpected token '('` в **уже открытом** шелле: алиас живёт в шелле, а интерактивный bash разворачивает его в строке определения функции. Фикс: перед определением добавлен `unalias oc-dev 2>/dev/null || true`.
- **`/dev/tcp` в WSL виснет** на connect — health-check в алиасе сделан через `curl --max-time`.
- **connection reset.** `13:44:24` — мой рестарт прокси оборвал активный стрим (`AI_APICallError: ... socket connection was closed unexpectedly`); opencode пометил как retryable и **переотправил**, ход прошёл. `13:47` — обрыв стрима со стороны апстрима: opencode сделал 6 повторов с backoff, ход закрылся `err=True` **без списания** (0 токенов). Прокси при этом отвечал `200` на все запросы.
- **Учёт стоимости.** 1h-запись у Anthropic = 2× input, а модель стоимости opencode (`cache_write: 5`) считает 1.25× — отображаемая цена может занижать реальную. Экономия всё равно кратная.
- **Порог 200K.** У ряда Opus-провайдеров в `models.json` есть `context_over_200k`/`tiers` (удвоение ставок). В этой сессии контекст дошёл до ~152K.

## 12. Выводы и рекомендации

1. **Дорогой — не модель, а холодные перезаписи.** Основной рычаг сделан: `zen-cache-proxy` (ttl 1h) — работает.
2. **Батчить субагентов параллельно** и не давать Opus-ходу простаивать >5 мин — первопричина промахов.
3. **Ревью — на DeepSeek + короткий Triage на Opus** (сейчас `dotnet-code-review-agent` в реальном запуске оказался на Opus и стоил $2.15). Проверить фактическую модель субагента (в конфиге он deepseek, в сессии — Opus).
4. **`oc-dev` запускать через алиас** (он поднимает прокси); `-c` для него не использовать.
5. Следить за размером контекста (порог 200K) и шагом оркестратора.

## 13. Изменённые файлы

Репозиторий `~/sources/opencode-config`:
- `config/tools/zen-cache-proxy.mjs` — прокси (ttl 1h, health, опциональный дамп).
- `config/profiles/oc-dev.jsonc` — `provider.opencode.options.baseURL` на прокси.
- `home/.bash_aliases` — `oc-dev` как функция с автоподъёмом прокси (и `unalias` старого алиаса).
- `README.md` — описание прокси + результат замера.

Репозиторий `~/sources/nextorm`:
- `docs/specs/oc-dev-opus-cache-cost-2026-09-26.md` — этот отчёт.

## 14. Приложение: полезные команды

```bash
# статистика за сегодня (локально) — см. §4
# запуск прокси вручную
PORT=8787 REWRITE=ttl bun ~/.config/opencode/tools/zen-cache-proxy.mjs
# прокси с дампом тел запросов
LOG=1 CAPTURE_DIR=/tmp/opencode/zen-watch PORT=8787 REWRITE=ttl \
  bun ~/.config/opencode/tools/zen-cache-proxy.mjs

# продолжить ту же сессию (с новым конфигом/прокси)
OPENCODE_CONFIG=$HOME/.config/opencode/profiles/oc-dev.jsonc opencode -c
OPENCODE_CONFIG=$HOME/.config/opencode/profiles/oc-dev.jsonc opencode --session ses_f238b219fffeIZRKzEyrQPqIlj
```
