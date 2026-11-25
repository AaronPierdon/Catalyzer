# catalyzer.json — a project describes itself

Put a `catalyzer.json` in a project's root folder. Every key is optional, and comments and trailing commas are allowed. To start one for you, right-click a card and choose **Create catalyzer.json**.

```jsonc
{
  "name": "Nexus",                          // card title (default: the folder name)
  "description": "Holographic Windows shell suite",
  "github": "https://github.com/AaronPierdon/Nexus",   // default: read from the git remote
  "accent": "#1BF28C",                      // card colour (default: the theme accent)
  "glyph": "",                        // Segoe Fluent Icons character for the badge (default: initials)
  "solution": "Nexus.sln",                  // what the VS button opens (default: the first .sln / .slnx)
  "helpers": [                              // scripts Catalyzer keeps running for you, hidden (Helpers plugin)
    { "name": "Build agent", "run": "_tools\\agent.ps1", "auto": true, "restart": true }
  ],
  "actions": [                              // the project's own buttons
    { "label": "Build",   "glyph": "", "run": "build.cmd" },
    { "label": "Install", "run": "powershell.exe", "args": "-File _tools\\scripts\\install.ps1", "tip": "Install to this PC" }
  ]
}
```

## `app` — where an installable app lives (the UPDATE chip)
```jsonc
"app": {
  "exe": "%LOCALAPPDATA%\\Programs\\Nexus\\Nexus.exe",   // the installed exe (environment variables expand)
  "version": "src\\Nexus.App\\Nexus.App.csproj",           // the file whose <Version> is the repo's version
  "install": "tools\\install.ps1"                           // the script UPDATE runs hidden with -Log
}
```
Every key is optional; without them Catalyzer assumes the template layout: `%LOCALAPPDATA%\Programs\<folder>\<folder>.exe`,
`src\<folder>\<folder>.csproj` and `tools\install.ps1`. The card shows **UPDATE a → b** when the repo is newer than the
exe, **INSTALL** when the exe isn't there, and nothing when they match. The script should follow the install contract
(principles 07); one without `-Log` still runs, with its output captured to `%LOCALAPPDATA%\Catalyzer\installs\`.

How `actions` run:
- `run` is relative to the project folder when that file exists. Otherwise Catalyzer starts it as a command: an exe on PATH, a URL, or a document.
- The project folder is always the working directory.

Plugins can read any other key you add: `Project.Extra["yourKey"]` holds its raw text.

## `helpers` — scripts that run hidden (Helpers plugin)
Each helper is a long-running script the project wants alive while you work: a build agent, a watcher, a local
server. Its card shows a chip (**HELPERS 1/2**). Click the chip to start or stop each helper, or to open its log.

| key | | |
|---|---|---|
| `name` | required in practice | what the card and menus call it; unique within the project |
| `run` | required | a file in the project (`.ps1`, `.cmd` / `.bat`, `.py`, `.js`, `.exe`) or a command on PATH |
| `args` | optional | added after the script |
| `auto` | default `false` | start it when Catalyzer starts (once a session: if you stop it, it stays stopped) |
| `restart` | default `false` | if it fails (non-zero exit), start it again after 2 s; it gives up after 3 failures in a minute |

How they run:
- **Never a console window.** `.ps1` runs through `powershell -NoProfile -NonInteractive -ExecutionPolicy Bypass -File`,
  `.cmd` / `.bat` through `cmd /d /c`, `.py` through `python -u`, and `.js` through `node`. Everything starts hidden,
  with the project folder as the working directory. Input is closed, so a script that asks a question gets
  end-of-input instead of hanging.
- **Contained.** Every helper is in a Windows job object that kills its members when Catalyzer goes, whether it
  closes, crashes or is killed. Stopping a helper kills its whole process tree.
- **Logged.** Output goes to `%APPDATA%\Catalyzer\plugin-data\helpers\logs\<project>--<name>.log`, which rolls over at
  1 MB. Errors are marked `!`.
- The environment has `CATALYZER_HELPER=<name>` set, so a script can tell it's running under Catalyzer.
- **Closing Catalyzer while helpers run** asks: *Stop all & exit* or *Keep in tray*, with an option to remember the
  answer. You can change it later in Settings → General. From the tray you can open Catalyzer, stop one helper, stop
  them all, or exit, each in one click.
- **Not for the Nexus helper** (`Start Nexus helper.cmd`). It installs Catalyzer, and an install closes Catalyzer,
  which would take the helper down mid-install. Keep starting it from Windows Explorer.
