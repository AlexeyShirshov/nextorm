# ClickHouse: паритет native extreme-row для float/double — issue #150

## 1. Статус и цель

**Дата:** 2026-10-02
**Issue:** #150 — https://github.com/AlexeyShirshov/nextorm/issues/150
**Milestone (проверено на GitHub):** 1.0.9-rc2 (номер 20); state = `OPEN`.
**Статус:** письменная спека одобрена пользователем в этой сессии (точная фраза `спеки ок`,
2026-10-02); разговорные решения по scope из более раннего обсуждения остаются историей.
Written spec approved; implementation plan not created/approved, execution not requested/started
in THIS interview; next eligible phase planning but not automatically begun. Заявлений о полном
отсутствии кода где-либо вне этого интервью нет. Реализация #144 (и #155) остаётся нетронутой;
локальная спека не закоммичена. Обновление issue/спеки не закрывает issue и не авторизует
исполнение. Этот файл фиксирует одобренную спеку, а не авторизацию исполнения.
**Цель:** завершить rc2-вопрос — либо безопасно сделать нативными только доказанные формы
float/double-ключей CH, либо отдать **evidence-backed negative result**. Ни нативный
enablement, ни ускорение не гарантируются.

## 2. Ссылки на существующие доказательства
Внутренние specs не публикуются; ниже — локальные пути (никаких выдуманных published URL).

- `docs/specs/design/issue-144-native-extreme-row.md:22-30` — семантика: одна реальная
  winning-строка со всеми полями, ties только по selector, NULL-компоненты исключаются,
  all-null партиции опускаются, nullable-группы/payload сохраняются, пустой вход → 0 строк.
- `docs/specs/design/issue-144-native-extreme-row.md:40-49` — floating-point ключи CH
  (включая составные) откатываются на portable из-за неподтверждённого NaN-паритета;
  дополнительные типы допускаются только с явными parity-тестами.
- `docs/specs/design/issue-144-native-extreme-row.md:51-56` — измерения производительности
  явно исключены; приёмка — корректность и факт нативной SQL-генерации.
- `docs/specs/status/native-extreme-row-144-1.md:24-25` — eligibility проверяется по
  prepared column/type shape без мутаций build-context; отсутствие capability или
  отрицательный eligibility → portable **до** рендера; renderer error не откатывается поздно.
- `docs/specs/status/native-extreme-row-144-1.md:301` — debt 5: CH NaN/extreme parity
  отложен с триггером «если float/double станут нативно-eligible».
- Debt provenance: `docs/specs/status/native-extreme-row-144-1.md:307-313` — строка debt 5
  ведёт на issue **#150**; исходный текст issue сохраняется без изменений.

## 3. Согласованные решения (ровно три)
1. **Исследовать safe native; eligible становятся только доказанные формы.**
   - Для каждой формы допустим **evidence-backed negative outcome**.
   - Portable-путь сохраняется; принудительного native нет.
2. **Проверять direct `argMin`/`argMax` И bounded SQL key adaptation в том же запросе.**
   - Если direct и adaptation дают расхождение — одного direct недостаточно для завершения.
   - Нового пользовательского семантического контракта не вводится.
3. **Исследовать single И composite ключи с float/double**, nullable-компоненты,
   `Min`/`Max`, global/grouped.
   - «Полная матрица» = определённые измерения, а не неограниченный поиск по произвольным
     арностям/адаптациям.
   - Покрыть позиции floating-компонента и смешанные integral/floating representatives.
   - Точные finite-fixtures и arity-representatives фиксируются в PLAN внутри этих измерений.
   - Эти representative-детали — рамка спеки, а не отдельно согласованный выбор арности.

## 4. Унаследованные инварианты (не меняются)
- Все возвращаемые поля берутся из **одной реальной winning-строки**.
- Ties: selector выбирает только **некоторую** tied-строку; пользовательский `OrderBy` —
  только output, tie-break им не является; tied ID native vs portable не сравниваются.
- NULL-компонент экстремального ключа исключает строку; all-null партиция отсутствует;
  nullable grouping/payload сохраняется; пустой global/grouped даёт ровно ноль строк;
  composite сравнивается лексикографически.
- Portable CH-реализация — **семантический референс**, включая NaN/+inf/-inf/+zero/-zero.
  Новый total order не определяется; CLR LINQ не используется как единственный oracle.
- Если референс неоднозначен или genuine winner отсутствует — задокументировать
  ограничение, не маскировать под parity и не переопределять семантику; portable сохраняется
  до разрешения.
- Row payload identity проверяется независимо; NaN-ассерты явно обрабатывают NaN.
- Signed-zero: требуется совпадение **множеств победителей/семантики selector**, а не
  идентичность случайной tied-строки; побайтовый bit order заново не обещается.

## 5. Подход к дизайну
- Никаких точных SQL-обещаний без проверки на реальном сервере.
- Eligibility — по форме на этапе prep/type shape, **не по значениям данных**; нет value
  pre-scan и session-global/mutable opt-in/public switch.
- Adaptation внутри того же SQL-запроса может нормализовать comparison key, но обязана
  сохранять эквивалентный winner set для обоих направлений и для composite.
- Payload выбирается **атомарно**, не как независимые экстремумы отдельных полей.
- Неподдержанные формы → portable **до** мутаций SQL/алиасов/параметров; renderer failure —
  это ошибка, а не late fallback.
- Никаких новых runtime-probe и лишнего query roundtrip: реализация — в том же запросе,
  как согласовано по scope. Успех любого кандидата не обещается.

## 6. Матрица приёмки (измерения)
| Измерение | Значения |
| --- | --- |
| Тип | `float`, `double` |
| Ключ | single, composite |
| Nullability | nullable / non-nullable компоненты |
| Область | global, grouped |
| Операция | `Min`, `Max` |
| Состав | позиции floating-компонента, mixed integral/floating representatives |

Datasets: finite positive/negative/extrema/ties, NaN (mixed и all-NaN), infinities, signed
zero, NULL (mixed/all-NULL), empty, heterogeneous nullable payload/grouping. По каждой
declared-форме фиксируется: native eligibility yes/no, direct parity evidence, adaptation
evidence (если direct падает), reason fallback. Для ties — membership в winner set и
payload-row identity **точно**, а не равенство произвольной tied-строки.

Валидация — на реальном ClickHouse; baseline в исходном дизайне — 25.8; в PLAN записать
фактическую версию и не заявлять более широкую сертификацию (baseline — предложение этой
спеки, не прежнее обещание пользователя по версии).
В `native-extreme-row-144-1.md` формулировки о NaN/infinity различаются по факту:
`:42`/`:148` (variant matrix) заявляют «NaN/infinity integration», тогда как `:103`/`:170`
(acceptance row → test) перечисляют только SQL-anchor'ы (`CH-N`), без integration-ссылок;
фактическое integration-evidence подтверждать в PLAN, а не объявлять заранее доказанным.

Нативные enabled-формы: SQL-ассерты на **нативную ветку** (не green fallback-only тест);
неподдержанные guard'ы держат portable; релевантные core/CH тесты и build — 0 warnings /
0 errors; real ClickHouse integration не должен быть skipped; при будущем запуске — скилл
`running-integration-tests`. EN+RU public docs/XML меняются только при смене
behavior/eligibility statement; ссылок на `docs/specs` в публичных доках нет.
Perf/аллокации/тайминги не обещаются.

## 7. Исключения (non-goals)
- All-ties `All` (остаётся portable).
- Native-widening других провайдеров.
- Новый public API / session-options / public switches.
- Матрица произвольных provider-version.
- Performance benchmark, аллокации, тайминги.
- Неограниченный поиск по арностям/адаптациям.
- Выполнение работ во время brainstorming.
- Регрессии shared-кода проверяются по будущему execution-плану.

## 8. Терминальное состояние
Либо enabled доказанные формы (возможно, подмножество), либо evidence-backed «всё portable»;
ни то, ни другое не гарантирует perf. Negative result обязан перечислить attempted
direct/adaptation кандидатов и конкретные наблюдаемые failing cases; «не исследовано» —
это не negative result. Обновление issue не закрывает её и не авторизует исполнение.

## 9. Handoff
Письменная спека одобрена. Следующая допустимая фаза — planning (`writing-plans`), но она
не начата автоматически. Реализация в этом интервью не запрошена и не начата; способ
исполнения пользователь выбирает отдельно. План сейчас не генерируется, коммиты/push/merge
не выполняются.
