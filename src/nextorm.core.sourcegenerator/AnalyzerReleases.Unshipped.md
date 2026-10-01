; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NORMGEN001 | NextORM.JoinAlias | Error | Duplicate join alias
NORMGEN002 | NextORM.JoinAlias | Error | Join alias collides with a generated member
NORMGEN003 | NextORM.JoinAlias | Error | Join alias is not a valid identifier
NORMGEN004 | NextORM.JoinAlias | Error | Alias projection exceeds the maximum arity
NORMGEN005 | NextORM.JoinAlias | Error | Alias argument is not in the approved form
NORMGEN006 | NextORM.JoinAlias | Error | Assembly name cannot be normalized to a namespace
