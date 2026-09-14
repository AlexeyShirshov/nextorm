# AGENTS

## Git
- **Never run `git push`.** The user pushes manually.
- Do not create commits unless the user explicitly asks.

## Line endings
- The repo uses **CRLF**; `core.autocrlf=true` is set in `.git/config` (shared with Windows).
- Always preserve CRLF when creating or editing files — never leave LF-only or mixed endings.
- To normalize a file after editing: `perl -pi -e 's/\r?\n/\r\n/g' <file>`.
