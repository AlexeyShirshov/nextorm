-- Synthetic opencode.db for examples/nextorm.examples.oc-report.
--
-- Usage:
--   sqlite3 /tmp/oc-report.db < seed.sql
--   dotnet run --project examples/nextorm.examples.oc-report -c Release -- 3 --db /tmp/oc-report.db
--
-- The schema mirrors the two tables oc-report reads (`session_v2`, `session_message`). Sizes are
-- parameters so a run can be small (smoke) or large (timing):
--   sqlite3 /tmp/oc-report.db ".parameter set @sessions 200000" ".parameter set @messages_per_session 20" < seed.sql
-- Counts are deliberately deterministic; there are no credentials or personal data here.

.parameter init
.parameter set @sessions 20000
.parameter set @messages_per_session 10

PRAGMA journal_mode = OFF;
PRAGMA synchronous = OFF;

BEGIN;

DROP TABLE IF EXISTS session_v2;
DROP TABLE IF EXISTS session_message;

CREATE TABLE session_v2
(
    id                TEXT PRIMARY KEY,
    agent             TEXT,
    model             TEXT,
    title             TEXT,
    time_created      INTEGER,
    time_updated      INTEGER,
    tokens_input      INTEGER,
    tokens_output     INTEGER,
    tokens_cache_read INTEGER,
    cost              REAL,
    idle_outcome      TEXT
);

CREATE TABLE session_message
(
    id           TEXT PRIMARY KEY,
    session_id   TEXT,
    type         TEXT,
    time_created INTEGER,
    data         TEXT
);

WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < @sessions)
INSERT INTO session_v2
SELECT printf('ses_%08x', i),
       CASE i % 5
           WHEN 0 THEN 'pdca-executor'
           WHEN 1 THEN 'coder'
           WHEN 2 THEN 'scout'
           WHEN 3 THEN 'architect'
           ELSE 'build'
           END,
       printf('{"id":"model-%d","providerID":"provider-%d"}', i % 7, i % 3),
       printf('session %d: a title long enough to exercise truncation', i),
       (CAST(strftime('%s', 'now') AS INTEGER) - (i % 10) * 86400) * 1000,
       (CAST(strftime('%s', 'now') AS INTEGER) - (i % 10) * 86400 + 600) * 1000,
       (i * 131) % 200000,
       (i * 17) % 50000,
       (i * 7) % 100000,
       ((i * 13) % 1000) / 1000.0,
       CASE i % 4
           WHEN 0 THEN 'succeeded'
           WHEN 1 THEN 'succeeded'
           WHEN 2 THEN 'failed'
           ELSE NULL
           END
FROM n;

WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n WHERE i < @sessions * @messages_per_session)
INSERT INTO session_message
SELECT printf('msg_%08x', i),
       printf('ses_%08x', ((i - 1) / @messages_per_session) + 1),
       CASE WHEN i % 3 = 0 THEN 'error' ELSE 'assistant' END,
       (CAST(strftime('%s', 'now') AS INTEGER) - (i % 10) * 86400) * 1000,
       CASE
           WHEN i % 3 = 0 THEN printf(
                   '{"error":{"type":"provider_%d","message":"synthetic failure %d"},"model":{"id":"model-%d","providerID":"provider-%d"}}',
                   i % 5, i, i % 7, i % 3)
           ELSE '{"role":"assistant","text":"ok"}'
           END
FROM n;

COMMIT;

CREATE INDEX IF NOT EXISTS idx_session_message_time_created ON session_message (time_created);
