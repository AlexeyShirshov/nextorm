# #408 — серверный захват строк DML вне SQL Server

Матрица предназначена для разработчиков NextORM, которым нужно выбрать контракт захвата без передачи строк через клиент. Она сравнивает пять движков и отдельно фиксирует поверхность NextORM на `7309e7e4948c74d8a587ba8f96397536350b7b55`. SQL Server — ориентир, не строка матрицы. Документ не предлагает единого API.

## Как читать матрицу

- **Компонуемый захват:** инструкция предоставляет SQL-отношение с результатом конкретной мутации. Следующая серверная операция обращается к этому отношению, а не перечитывает таблицу.
- **Встроенная маршрутизация:** заранее созданный триггер или материализованное представление передаёт строки события серверной операции. Это закладка в схеме, не результат DML, доступный произвольному `SELECT`.
- **Не захват:** `RETURNING` только клиенту, `SELECT` из изменённой таблицы, повторный вызов с полученными ключами. Один батч или один сетевой запрос сами по себе не доказывают происхождение строк.

«Один запрос» далее означает один запрос выполнения по уже открытой клиентской сессии, без подготовки схемы, отдельного `SELECT` для проверки и очистки. Это не число TCP-пакетов и не обещание о поведении всех драйверов. Сетевой обмен в этом цикле не измерялся. SQLite проверен в процессе, без сетевого протокола.

## Матрица движков и NextORM

| Движок и граница проверки | Компонуемое отношение DML | Ограниченная серверная маршрутизация | Поверхность NextORM на указанном HEAD |
|---|---|---|---|
| **PostgreSQL** — `INSERT/UPDATE/DELETE` в `WITH` с 9.1; проверен контракт документации 17, включая `MERGE`; переходные отношения триггеров с 10 | **Да.** `WITH changed AS (DELETE … RETURNING …) INSERT … SELECT … FROM changed`. `RETURNING` внутри CTE образует отношение. Самостоятельный `RETURNING` этого не гарантирует. | `AFTER` с `REFERENCING NEW TABLE` или `OLD TABLE` даёт набор строк операции; действуют ограничения события, вида таблицы и триггера. | Уже есть изменяющие данные CTE: `MutationCteQuery<TResult>.From`/`FromTable`, формы `INSERT/UPDATE/DELETE` и joined `UPDATE/DELETE` (`src/nextorm.core/Builders/Cte/MutationCteQuery.cs:53–133,151–155`, `src/nextorm.postgres/PostgresDialect.cs:103`). Поддержка `MERGE` сервером 17 не означает наличие соответствующего CTE-терминала NextORM. Не дублировать существующий API. |
| **SQLite** — `RETURNING` с 3.35.0; локально 3.37.2 | **Нет для документированного `RETURNING`:** только верхнеуровневый DML; нельзя использовать как подзапрос, тело CTE или источник другой записи. | Строчный триггер с `NEW`/`OLD` может писать в другую таблицу в том же локальном выполнении. Это не перенаправление `RETURNING`; последний не отражает последующие изменения `AFTER`-триггеров. | Диалект включает клиентский `RETURNING` (`src/nextorm.sqlite/SqliteDialect.cs:47`). Изменяющий данные CTE не заявлен; существующий отказ: `tests/nextorm.sqlite.tests/Cte/DataModifyingCteRejectionTests.cs:11`. |
| **MariaDB** — `INSERT … RETURNING` с 10.5; отрицательная граница тела CTE — тег 11.4.2; более новые версии см. ниже | **Нет в грамматике 11.4.2:** CTE принимает `query_expression`, не DML с `RETURNING`. `INSERT … RETURNING` и однотабличный `DELETE … RETURNING` возвращают набор клиенту, не дают SQL-отношение для следующей инструкции. | `AFTER … FOR EACH ROW` с `NEW`/`OLD` фиксирует строки события во второй таблице; режима `FOR EACH STATEMENT` нет. | Версионные флаги `SupportsUpdateReturning` и bulk insert returning начиная с 13.0 в коде; общий флаг `SupportsReturning` не включает одиночные insert/delete (`src/nextorm.mariadb/MariaDbDialect.cs:13–15,51–63`). Это ограничения NextORM, не даты появления серверного синтаксиса. Изменяющий данные CTE отклоняется. |
| **MySQL 8.4 LTS** — [версионный справочник](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/with.html) | **Нет в проверенных формах 8.4:** грамматика `INSERT/UPDATE/DELETE` не даёт `RETURNING`; `WITH` перед `UPDATE/DELETE` означает входной CTE с подзапросом, не возвращаемый результат DML. | `AFTER … FOR EACH ROW` с `NEW`/`OLD` может писать в другую таблицу. Хранимая процедура может координировать инструкции, но один `CALL` не превращает последующий запрос к таблице в захват. | Изменяющий данные CTE отклоняется (`tests/nextorm.mysql.tests/Cte/DataModifyingCteRejectionTests.cs:11`); общий `SupportsReturning` по умолчанию выключен (`src/nextorm.core/DataContext/Dialect/SqlDialectBase.cs:1016`). |
| **ClickHouse 25.8.1.5101 LTS** — проверена документация [точного тега](https://raw.githubusercontent.com/ClickHouse/ClickHouse/v25.8.1.5101-lts/docs/en/sql-reference/statements/create/view.md) | **Не найдено** для документированных в этом теге `INSERT`, `ALTER TABLE … UPDATE` и `DELETE`: эти формы не предоставляют отношения изменённых строк. Это не вывод обо всех версиях и экспериментальных формах. | **Да, только INSERT:** incremental materialized view `TO` преобразует свежий вставленный блок и записывает результат во вторую таблицу; `UPDATE`, `DELETE` и старые строки источника эту ветку не запускают. Это не общий `OUTPUT INTO`; возможна частичная запись при ошибке. | Изменяющий данные CTE отклоняется (`tests/nextorm.clickhouse.tests/Cte/DataModifyingCteRejectionTests.cs:11`). Специальный типизированный контракт маршрутизации INSERT через materialized view в просмотренной поверхности не найден; схему и сам `INSERT` можно рассматривать отдельно, не как `Returning()` NextORM. |

Дополнение по MariaDB: справочники [`UPDATE`](https://mariadb.com/docs/server/reference/sql-statements/data-manipulation/changing-deleting-data/update) и [`DELETE`](https://mariadb.com/docs/server/reference/sql-statements/data-manipulation/changing-deleting-data/delete) отмечают `WITH` перед DML начиная с 12.3, а `UPDATE … RETURNING` — с 13.0, только для одной таблицы. Обе записи описывают синтаксис верхнеуровневой инструкции, не DML внутри тела CTE. Для новых версий не заявляется экспериментально проверенное отсутствие иных возможностей. Для 11.4.2 используйте [грамматику тега](https://raw.githubusercontent.com/MariaDB/server/mariadb-11.4.2/sql/sql_yacc.yy), правило `with_list_element`.

## Воспроизведение и ожидаемые наблюдения

Используйте только свою одноразовую БД, без параллельных записей, и зафиксируйте версию сервера. Выполните подготовку отдельно, затем отправьте **только** указанную измеряемую инструкцию, дождитесь ответа и отдельно проверьте итоговые таблицы. Проверочный `SELECT` подтверждает состояние после выполнения; он не участвует в передаче строк. При проверке сетевой границы регистрируйте запрос клиента на открытом соединении, а не число команд в файле или число возвращённых строк.

### PostgreSQL 17: отношение из `DELETE … RETURNING`

[Документация 17](https://www.postgresql.org/docs/17/queries-with.html#QUERIES-WITH-MODIFYING) показывает перенос строк из `products` в `products_log`; [примечания 9.1](https://www.postgresql.org/docs/9.1/release-9-1.html) фиксируют появление DML в `WITH`. В 17 подзапрос DML исполняется до конца с общим снимком, а подоперации передают значения через `RETURNING`, не через повторное чтение базы. Не модифицируйте одну строку дважды в одной инструкции и не полагайтесь на порядок независимых подопераций.

Подготовка:

```sql
CREATE TEMP TABLE src (id integer PRIMARY KEY, v text);
CREATE TEMP TABLE audit (id integer PRIMARY KEY, v text);
INSERT INTO src VALUES (1, 'moved'), (2, 'untouched');
INSERT INTO audit VALUES (0, 'preexisting');
```

Измеряемая инструкция для одного вызова [`PQexec`](https://www.postgresql.org/docs/17/libpq-exec.html#LIBPQ-PQEXEC) на той же открытой сессии (один Simple Query по [протоколу 17](https://www.postgresql.org/docs/17/protocol-flow.html#PROTOCOL-FLOW-SIMPLE-QUERY)):

```sql
WITH changed AS (
  DELETE FROM src WHERE id = 1 RETURNING id, v
)
INSERT INTO audit SELECT id, v FROM changed RETURNING id, v;
```

Ожидается: результат `(1, 'moved')`; в `src` остаётся `(2, 'untouched')`, в `audit` — старая строка `0` и запись мутации `1`. Публичный upstream [SQL регрессионного набора 17.0](https://github.com/postgres/postgres/blob/REL_17_0/src/test/regress/sql/with.sql) и [его эталонный вывод](https://github.com/postgres/postgres/blob/REL_17_0/src/test/regress/expected/with.out) дают дополнительное воспроизводимое подтверждение DML CTE. Свой серверный прогон в этом цикле не выполнялся.

### PostgreSQL 17: переходное отношение триггера

Это отдельный вариант, когда схема должна автоматически направлять вставленные строки в другую таблицу. [Возможность добавлена в 10](https://www.postgresql.org/docs/10/release-10.html); границы `AFTER` и `REFERENCING` описаны в [`CREATE TRIGGER` 17](https://www.postgresql.org/docs/17/sql-createtrigger.html).

Подготовка на собственной БД с PL/pgSQL:

```sql
CREATE TABLE trigger_src (id integer PRIMARY KEY, v text);
CREATE TABLE trigger_audit (id integer, v text);
INSERT INTO trigger_src VALUES (1, 'old');
CREATE FUNCTION route_insert() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO trigger_audit SELECT id, v FROM new_rows;
  RETURN NULL;
END;
$$;
CREATE TRIGGER route_insert AFTER INSERT ON trigger_src
  REFERENCING NEW TABLE AS new_rows
  FOR EACH STATEMENT EXECUTE FUNCTION route_insert();
```

Отправьте одним `PQexec` только `INSERT INTO trigger_src VALUES (2, 'new'), (3, 'newer');`. Отдельное чтение `trigger_audit ORDER BY id` должно показать только `2` и `3`, не старую `1`. Не выполнялось в этом цикле.

### SQLite 3.37.2: результат клиенту и независимая запись триггера

[SQLite 3.35.0](https://sqlite.org/releaselog/3_35_0.html) вводит `RETURNING`; [ограничения `RETURNING`](https://sqlite.org/lang_returning.html#limitations_and_caveats) запрещают вложение и перенаправление. [Синтаксис триггеров](https://sqlite.org/lang_createtrigger.html) разрешает `NEW`/`OLD` для соответствующих событий.

В локальной `:memory:` базе создайте `src(id INTEGER PRIMARY KEY, v TEXT)` и `audit(id, v)`, добавьте `(1,'old')` и `(2,'unaffected')`; затем создайте триггер:

```sql
CREATE TRIGGER cap AFTER UPDATE ON src BEGIN
  INSERT INTO audit VALUES (NEW.id, NEW.v);
END;
```

Один `sqlite3.Connection.execute` для `UPDATE src SET v='new' WHERE id=1 RETURNING id,v` возвращает приложению `[(1,'new')]`; отдельная проверка `audit` находит ту же пару, записанную **триггером**, и не находит `2`. Отдельные отрицательные пробы отклоняют `WITH d AS (UPDATE … RETURNING …)`, `SELECT` из `DELETE … RETURNING` и `RETURNING` внутри триггера. Получено в SQLite 3.37.2: 3 ожидаемых отказа, 1 положительная проба. Команда и полный результат: `.pdca/collection-405-407/items/408/rv1/sqlite-probe-01.log`.

### MariaDB 11.4.2 и MySQL 8.4: маршрутизация строк события

В MariaDB [10.5](https://mariadb.com/docs/release-notes/community-server/old-releases/10.5/what-is-mariadb-105) `INSERT … RETURNING` возвращает набор вставленных строк, аналогично [однотабличному `DELETE … RETURNING`](https://mariadb.com/docs/server/reference/sql-statements/data-manipulation/changing-deleting-data/delete). Это не источник для триггера. Оба движка поддерживают строчный сценарий: [MariaDB — ограничения триггеров](https://mariadb.com/docs/server/server-usage/triggers-events/triggers/trigger-limitations), [MySQL 8.4 — `NEW`/`OLD`](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/trigger-syntax.html). Отдельно см. грамматику MySQL 8.4: [`INSERT`](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/insert.html), [`UPDATE`](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/update.html), [`DELETE`](https://docs.oracle.com/cd/E17952_01/mysql-8.4-en/delete.html).

Подготовка на обоих движках:

```sql
CREATE TABLE src (id int PRIMARY KEY, v int NOT NULL) ENGINE=InnoDB;
CREATE TABLE audit (id int, old_v int, new_v int) ENGINE=InnoDB;
INSERT INTO src VALUES (1, 10), (2, 20);
CREATE TRIGGER cap AFTER UPDATE ON src FOR EACH ROW
  INSERT INTO audit VALUES (NEW.id, OLD.v, NEW.v);
```

Исполните только `UPDATE src SET v = v + 1 WHERE id = 1` через один запрос текстового протокола (MariaDB [`COM_QUERY`](https://mariadb.com/docs/server/reference/clientserver-protocol/2-text-protocol/com_query), MySQL 8.4 [`mysql_real_query`](https://docs.oracle.com/cd/E17952_01/c-api-8.4-en/mysql-real-query.html)). Отдельное чтение `audit` должно увидеть ровно `(1, 10, 11)`, без незатронутой строки `2`. Здесь происхождение доказывают `OLD`/`NEW`, а не запрос `SELECT` из `src`. Схема заранее фиксирует маршрут; отдельного компонуемого результата для вызывающего нет. Проба не запускалась ни на MariaDB, ни на MySQL.

### ClickHouse 25.8.1.5101: `INSERT` и incremental materialized view

Тег [25.8.1.5101](https://raw.githubusercontent.com/ClickHouse/ClickHouse/v25.8.1.5101-lts/docs/en/sql-reference/statements/create/view.md) описывает прямую обработку свежего блока вставки, сопоставление назначения по именам, отсутствие реакции на изменение старых данных и частичную запись при ошибке. Сопоставьте с [грамматикой `INSERT` того же тега](https://raw.githubusercontent.com/ClickHouse/ClickHouse/v25.8.1.5101-lts/docs/en/sql-reference/statements/insert-into.md).

Подготовка без `POPULATE`:

```sql
CREATE TABLE src (id UInt32, v UInt32) ENGINE = MergeTree ORDER BY id;
CREATE TABLE audit (id UInt32, ten_v UInt32) ENGINE = MergeTree ORDER BY id;
INSERT INTO src VALUES (1, 3);
CREATE MATERIALIZED VIEW route_insert TO audit AS
  SELECT id AS id, v * 10 AS ten_v FROM src;
```

Отправьте только `INSERT INTO src SETTINGS async_insert=0 VALUES (2, 7)` одним HTTP POST с SQL в теле по [интерфейсу тега 25.8](https://raw.githubusercontent.com/ClickHouse/ClickHouse/v25.8.1.5101-lts/docs/en/interfaces/http.md). Без автоматических повторов и при успехе ответа отдельный `SELECT id, ten_v FROM audit ORDER BY id` должен показать только `(2,70)`. Старая строка `1` не должна попадать в `audit`; операция не перечитывает весь `src`. Не обещайте атомарность fan-out, захват `UPDATE/DELETE` или возврат набора клиенту. Проба не запускалась в этом цикле.

## Следующие шаги и границы решения

- **PostgreSQL:** используйте существующий DML CTE. Если понадобится `MERGE` как CTE, сначала подтвердите конкретный сценарий и версионный порог сервера; расширяйте существующий контракт, а не создавайте его дубликат.
- **SQLite, MariaDB, MySQL:** не предлагайте компонуемый захват через клиентский результат, перечитывание или несколько сетевых запросов. Для отдельного запроса на аудит возможен контракт schema-level триггера: тип события, образы строк, таблица назначения, транзакционность и разрешения. Следующий шаг для такого запроса — согласовать владение DDL и проверить rollback на конкретной версии, не добавляя универсальный fluent `OutputInto`. Для SQLite при этом нет сетевой метрики.
- **ClickHouse:** потенциальный отдельный контракт — заранее настроенный `INSERT` fan-out во вторую таблицу с явными именами, согласованным режимом отправки и описанными сбоями. Следующий шаг — подтвердить ограниченный контракт на собственной версии сервера, в том числе поведение при отказе назначения, до решения об API. Не переносить на него гарантии `UPDATE`, `DELETE` или атомарности SQL Server.

Снимки первичных источников и их хеши — `.pdca/collection-405-407/items/408/rv1/primary-fetch.log`, `version-supplement-fetch.log` и `reproducer-primary-fetch.log` в том же каталоге. В этом цикле выполнены только локальные SQLite-пробы, просмотр первичных документов и семантическая сверка NextORM. Для PostgreSQL, MariaDB, MySQL и ClickHouse приведены проверяемые источники, SQL, ожидаемые наблюдения и границы запроса; независимых локальных серверных прогонов и сетевого трассирования нет.
