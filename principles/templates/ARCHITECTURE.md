# ARCHITECTURE — <App>

The inventory and the reasons: what the app is built from, and why each piece was chosen. Readable in ten minutes
(before an interview, or before a chat touches the code), concrete enough to find your way. Not a
compare-and-contrast: each choice gets the reason it won, in a line or two.

## Stack at a glance
| Layer | Technology | Why this |
|---|---|---|
| UI | <e.g. WPF on .NET 10> | <the reason> |
| Data | <e.g. SQL Server through Dapper> | <the reason> |

## Parts
| Part | Folder / project | Owns |
|---|---|---|
| <name> | `src/<...>` | <one line> |

## Libraries
| Library (version) | Used for | Why this one |
|---|---|---|
| <name> | <what> | <the reason> |

## Main classes
| Class | File | What it's for |
|---|---|---|
| `<Name>` | `src/<file>` | <one line> |

## How the main flows work
### <Flow>
<3–6 steps through the code, with the key class and method at each step>

## Data
<the stores, the shape of the data, who writes what, how it changes (migrations)>

## Decisions and patterns
- **<decision>**: <what we do and why>. (Example: *raw SQL through Dapper, not an ORM: the queries are hand-tuned
  and the schema is ours.*)

## Outside connections
<other apps, APIs, devices, the file server, GitHub>
