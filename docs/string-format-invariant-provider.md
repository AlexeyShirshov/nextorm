# Culture-invariant string formatting

> `string.Format` and `x.ToString(format)` are translated to SQL for the culture-invariant subset only. The invariant culture is accepted both as a constant and as the framework's static property expression — matched **structurally**, never evaluated.

**Prerequisites:** [String functions](scalar-functions/01-string-functions.md) · [Provider overview](connecting-providers/overview.md)

## Overview

The `string.Format` translator emits native SQL instead of evaluating the call in memory, so a culture that has no portable SQL form cannot be honoured: a database locale would silently change the result. `CultureInfo.InvariantCulture` is therefore the only accepted provider, and every other culture is refused.

The provider argument may be written in the natural C# way, `CultureInfo.InvariantCulture`, or as an explicit constant. Both are recognised without executing anything.

## What is accepted

```csharp
var rows = ctx.From<Order>()
    .Select(o => new
    {
        // No provider: unchanged, always accepted.
        Plain = string.Format("{0}-{1}", o.Code, o.Region),
        // The static property expression: accepted structurally.
        Formatted = string.Format(CultureInfo.InvariantCulture, "{0:D4}", o.Id),
        // The same provider behind the conversion the translator already unwraps.
        Wrapped = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "{0:D4}", o.Id),
        // A null provider keeps the provider-less behaviour.
        NullProvider = string.Format((IFormatProvider?)null, "{0}", o.Id)
    })
    .ToList();
```

| Provider expression | Outcome |
|---|---|
| `CultureInfo.InvariantCulture` (static property access) | Accepted — matched by declaring type `CultureInfo` and member name `InvariantCulture` on a static (receiver-less) member access |
| `(IFormatProvider)CultureInfo.InvariantCulture` | Accepted — the conversion wrapper is unwrapped first, then matched as above |
| `null`, `default(IFormatProvider)` | Accepted — behaves exactly like the provider-less overload |
| an explicit `Expression.Constant(CultureInfo.InvariantCulture)` built by hand | Accepted — reference-identity constant arm, unchanged |
| any other culture: `CultureInfo.CurrentCulture`, `CultureInfo.CurrentUICulture`, `CultureInfo.GetCultureInfo("en-US")`, `new CultureInfo("")` | Refused with the message below |
| a captured or instance provider (`var c = CultureInfo.InvariantCulture; ... string.Format(c, ...)`) | Refused — only a static member access qualifies |
| a member named `InvariantCulture` on another type, or a property/method returning the invariant singleton | Refused — the declaring type and the member identity must be `System.Globalization.CultureInfo.InvariantCulture` |

Recognition is purely structural: the expression tree node is inspected for its declaring type and member identity. The getter is never invoked, no expression is compiled and no value is evaluated, so a lookalike property whose getter throws is refused without the getter running.

## Supported formats

Accepting the provider does not widen the format surface. Only the formats the translator already supports are emitted:

| Value | Supported specifiers |
|---|---|
| numeric (`byte` … `decimal`) | `N`, `F`, `D`, `X`, each with an optional precision; provider-dependent (see below) |
| date/time (`DateTime`, `DateTimeOffset`) | the tokens `yyyy`, `yy`, `MM`, `dd`, `HH`, `mm`, `ss` with the separators `-`, `/`, `.`, `:`, space |
| any value without a specifier | rendered like the provider-less overload |
| alignment (`{0,8}`) | padding with the provider's native function |

A format specifier that the provider cannot render exactly, a non-constant format string, and overloads that take a `CompositeFormat` are still refused — with their own messages, unchanged by the provider acceptance.

## Provider support

| Provider | `N` | `F` | `D` | `X` | date tokens |
|---|---|---|---|---|---|
| SQLite | — | `printf` | `printf` | `printf` | `strftime` |
| PostgreSQL | — | `to_char` | `to_char` | `to_hex` | `to_char` |
| SQL Server | `format` | `format` | `format` | `format` | `format` |
| MySQL | `format` | — | `lpad` | `hex` | `date_format` |
| MariaDB | `format` | — | `lpad` | `hex` | `date_format` |
| ClickHouse | — | `format` | `leftPad` | `hex` | `formatDateTime` |

A dash means the provider has no exact rendering for that specifier and the query is refused rather than approximated.

> **Numbers in concatenations.** A numeric part without a specifier is emitted as the bare column, exactly as for the provider-less overload. Some servers do not implicitly convert it inside a concatenation — SQL Server rejects `varchar + bigint` — so give numeric parts an explicit specifier (`{0:D4}`) when they are concatenated with text.

## Refusal message

Every non-canonical provider throws `NotSupportedException` with this exact message:

```text
string.Format is only supported with CultureInfo.InvariantCulture; the current/other cultures have no portable SQL form.
```

The message is unchanged from earlier releases; the accepted surface gained the canonical static property expression and nothing else.
