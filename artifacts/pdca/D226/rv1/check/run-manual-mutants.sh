#!/usr/bin/env bash
# Manual targeted mutants for T226 (corrected: one pristine snapshot, restored around every mutant).
set -u
cd /home/alex/sources/nextorm
OUT=artifacts/pdca/D226/rv1/check
BK=/tmp/opencode/t226-mutants
rm -rf "$BK"; mkdir -p "$BK"

CLI=src/nextorm.core/DataContext/CommandReaderOwner.cs
BR=src/nextorm.core/DataContext/BatchRunner.cs
PR=src/nextorm.core/DataContext/ProcedureResult.cs
cp "$CLI" "$BK/CommandReaderOwner.cs"
cp "$BR"  "$BK/BatchRunner.cs"
cp "$PR"  "$BK/ProcedureResult.cs"

# apply_mutation <file> <python-expr-file>
restore() { cp "$BK/$(basename "$1")" "$1"; }

mutate_and_test() { # $1 name ; $2 file ; $3 buildproj ; rest: test args
  local name="$1" file="$2" buildproj="$3"; shift 3
  local log="$OUT/mutant-$name.log"
  echo "### MUTANT: $name" > "$log"
  echo "### mutator: $MUTATOR_DESC" >> "$log"
  echo "### BUILD: dotnet build $buildproj -c Debug" >> "$log"
  dotnet build "$buildproj" -c Debug >> "$log" 2>&1
  echo "BUILD_EXIT=$?" >> "$log"
  echo "### TEST ARGS: $*" >> "$log"
  dotnet test "$@" >> "$log" 2>&1
  echo "TEST_EXIT=$?" >> "$log"
  restore "$file"
}

# M1 - sentinel initial value -1 -> 0
restore "$CLI"
MUTATOR_DESC="private int _recordsAffected = -1;  ->  = 0;"
python3 - "$CLI" <<'PY'
import sys
p=sys.argv[1]; s=open(p,'r',encoding='utf-8',newline='').read()
assert "private int _recordsAffected = -1;" in s
open(p,'w',encoding='utf-8',newline='').write(s.replace("private int _recordsAffected = -1;","private int _recordsAffected = 0;",1))
PY
mutate_and_test "M1-sentinel-init" "$CLI" tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~RecordsAffected"
restore "$CLI"

# M2 - capture timing: drop the post-release upgrade in Dispose
restore "$CLI"
MUTATOR_DESC="Dispose(): remove PublishAfterRelease(reader);"
python3 - "$CLI" <<'PY'
import sys
p=sys.argv[1]; s=open(p,'r',encoding='utf-8',newline='').read()
t=("            PublishBeforeRelease(reader);\r\n"
   "            reader.Dispose();\r\n"
   "            PublishAfterRelease(reader);")
r=("            PublishBeforeRelease(reader);\r\n"
   "            reader.Dispose();")
assert t in s
open(p,'w',encoding='utf-8',newline='').write(s.replace(t,r,1))
PY
mutate_and_test "M2-drop-postrelease" "$CLI" tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~RecordsAffected"
restore "$CLI"

# M3 - batch runner affected-row pass-through removed (sentinel passed instead)
restore "$BR"
MUTATOR_DESC="BatchRunner: columnNames, CommandReaderOwner.ReadRecordsAffected(reader) -> columnNames, -1"
python3 - "$BR" <<'PY'
import sys
p=sys.argv[1]; s=open(p,'r',encoding='utf-8',newline='').read()
assert "columnNames, CommandReaderOwner.ReadRecordsAffected(reader));" in s
open(p,'w',encoding='utf-8',newline='').write(s.replace("columnNames, CommandReaderOwner.ReadRecordsAffected(reader));","columnNames, -1);"))
PY
mutate_and_test "M3-batchrunner-passthrough" "$BR" tests/nextorm.sqlite.tests -c Debug --no-build --filter "FullyQualifiedName~BatchMultipleResultTests"
restore "$BR"

# M4 - ProcedureResult getter returns constant -1 instead of the owner cache
restore "$PR"
MUTATOR_DESC="ProcedureResult: => _owner.RecordsAffected  ->  => -1"
python3 - "$PR" <<'PY'
import sys
p=sys.argv[1]; s=open(p,'r',encoding='utf-8',newline='').read()
assert "public int RecordsAffected => _owner.RecordsAffected;" in s
open(p,'w',encoding='utf-8',newline='').write(s.replace("public int RecordsAffected => _owner.RecordsAffected;","public int RecordsAffected => -1;",1))
PY
mutate_and_test "M4-procedureresult-getter" "$PR" tests/nextorm.core.tests -c Debug --no-build --filter "FullyQualifiedName~RecordsAffected"
restore "$PR"

touch "$OUT/MUTANTS_DONE"
