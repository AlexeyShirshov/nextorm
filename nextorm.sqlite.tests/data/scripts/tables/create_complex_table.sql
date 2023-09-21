-- SQLite
CREATE TABLE complex_entity
(
    id integer PRIMARY KEY AUTOINCREMENT,
    nullableInt INT null,
    someString varchar(100),
    tinyInt tinyint not null,
    small smallint null,
    r real,
    d double,
    m numeric,
    dt datetime,
    onlyDate date not null,
    b boolean
)