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
  "actions": [                              // the project's own buttons
    { "label": "Build",   "glyph": "", "run": "build.cmd" },
    { "label": "Install", "run": "powershell.exe", "args": "-File _tools\\scripts\\install.ps1", "tip": "Install to this PC" }
  ]
}
```

How `actions` run:
- `run` is relative to the project folder when that file exists. Otherwise Catalyzer starts it as a command: an exe on PATH, a URL, or a document.
- The project folder is always the working directory.

Plugins can read any other key you add: `Project.Extra["yourKey"]` holds its raw text.
