# EV-R6 - D8a evidence (final)

ClickHouse escaping/LIKE/grammar: default LIKE backslash escaping exact; a prepared default-like
statement contains no `escape` token; the custom escape `"!"` throws the product's
`NotSupportedException` (`*ClickHouse*ESCAPE*`). The ClickHouse class is 3/3 green (0 failed,
0 skipped). Log: `D8a-clickhouse.log`.
