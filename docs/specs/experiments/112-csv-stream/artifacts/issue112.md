# Issue #112 — Потоковая выдача csv в Stream

Терминал над QueryCommand<TResult> (и EntityBuilder<TEntity>), который пишет результат Select как csv прямо в выходной Stream, не создавая TResult на строку и не бокся значения.
