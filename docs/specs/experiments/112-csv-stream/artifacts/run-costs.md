# Стоимость сторон (parent + все потомки), из opencode.db

## a1 — 'exp112/a1'
- 11:03-11:23: parent=$0.1821 + kids(0)=$0.0000 = $0.1821
- **ВСЕГО (все попытки) ≈ $0.1821**

## a2 — 'exp112/a2'
- 11:23-12:58: parent=$0.2554 + kids(2)=$0.1345 = $0.3898
- 12:59-14:11: parent=$0.1784 + kids(2)=$0.0940 = $0.2723
- **ВСЕГО (все попытки) ≈ $0.6621**

## a3 — 'exp112/a3'
- 14:11-15:26: parent=$0.0722 + kids(32)=$0.8628 = $0.9350
- 15:46-16:54: parent=$0.0762 + kids(31)=$0.4374 = $0.5137
- **ВСЕГО (все попытки) ≈ $1.4487**

## upstream — 'Потоковая выдача CSV в Stream (#112)'
- 11:07-15:54: parent=$0.1160 + kids(48)=$1.8733 = $1.9893
- **ВСЕГО (все попытки) ≈ $1.9893**

## Судьи (canonical, 2026-09-29 17:11+ и 18:36+)

- 17:11 eval-l2/a1           judge         $0.0357
- 17:14 eval-l3/a1           auditor       $0.0425
- 17:18 eval-l3s/a1          code-auditor  $0.0366
- 17:22 eval-l2/a2           judge         $0.0291
- 17:26 eval-l3/a2           auditor       $0.0383
- 17:29 eval-l3s/a2          code-auditor  $0.0381
- 17:33 eval-l2/a3           judge         $0.0302
- 17:38 eval-l3/a3           auditor       $0.0434
- 17:42 eval-l3s/a3          code-auditor  $0.0263
- 17:45 eval-l2/upstream     judge         $0.0563
- 17:51 eval-l3/upstream     auditor       $0.0596
- 18:30 eval-l3s/upstream    code-auditor  $0.0442
- 18:36 eval-l2/a1           judge         $0.0300
- 18:39 eval-l2/a2           judge         $0.0248
- 18:42 eval-l2/a3           judge         $0.0296
- 18:55 eval-l2/upstream     judge         $0.0369
