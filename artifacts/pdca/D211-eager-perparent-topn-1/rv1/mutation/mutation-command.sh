#!/usr/bin/env bash
# T211 D:06 targeted Stryker.NET mutation run.
# Scope: only the line spans introduced/changed by T211 in nextorm.core (uncommitted working tree
# vs HEAD), passed via the documented CLI span syntax (config-file `mutate` does not honour spans,
# only plain file globs). Command mirrors D151's working `--config-file ... -O ...` recipe.
set -u
cd /home/alex/sources/nextorm
ART=artifacts/pdca/D211-eager-perparent-topn-1/rv1/mutation
exec dotnet tool run dotnet-stryker -- \
  --config-file "$ART/stryker-config.json" \
  --output "$ART" \
  --concurrency 4 \
  --skip-version-check \
  --verbosity info \
  -m "Builders/EntityBuilder.cs{1190..1209}{1212..1212}{1215..1215}{1316..1328}{1331..1331}{1407..1407}{1417..1422}{1509..1509}{1559..1561}{1627..1660}" \
  -m "Builders/EntityBuilderEagerLoading.cs{63..63}{66..66}{239..239}{245..252}{267..304}{329..331}{333..337}{339..339}{341..342}{345..354}{356..356}{367..367}{379..379}{555..559}{562..563}{565..565}{576..578}" \
  -m "Builders/EntityBuilderExtensions.cs{538..538}{564..564}" \
  -m "Builders/Joins/JoinIntoStitcher.cs{112..114}" \
  -m "DataContext/SqlBuilder.cs{51..55}{628..891}" \
  -m "Expressions/JoinExpression.cs{179..187}{212..212}{233..233}" \
  -m "Query/QueryCommand.cs{167..167}{453..476}{882..882}" \
  -m "Query/QueryCommand.Clone.cs{92..95}" \
  -m "Query/QueryCommand.QueryPreparer.cs{43..48}{102..102}{131..131}{3220..3234}" \
  -m "Query/QueryPlanEqualityComparer.cs{163..171}{468..473}"
