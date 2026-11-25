# SUMMARY — Catalyzer

What Catalyzer does for Aaron, in plain words. How it's built is in [ARCHITECTURE.md](ARCHITECTURE.md).

## In one paragraph
Aaron builds several apps at once (Nexus, MAWP and its phone and TV siblings, utilities) across machines and chats.
Catalyzer is the one place they all live: every repo as a glass card with its git state, its notes and a hidden
terminal its Claude chat can use. New projects start here, already wired to GitHub, the principles and Claude. It
also carries the rulebook every app follows (`principles/`), so the hub and its rules ship together.

## Features
### The board
- **A card per repo** under the projects folder (`source\repos` by default; more folders, single projects and
  hidden ones in Settings). New repos appear by themselves.
- Each card shows the branch, CLEAN / n CHANGED / n TO PUSH / TO PULL, the last commit and 21 days of activity, plus
  buttons: FOLDER, GITHUB, CODE, VS, TERMINAL, NEXUS, and the project's own (from its `catalyzer.json`). Extras fold
  into a ⋯ menu.
- Search (Ctrl+F), rescan (F5), hide, move to front, and a right-click menu (copy path, edit catalyzer.json, refresh).

### Starting and managing projects
- **First-run welcome:** pick the projects folder; GitHub works through the PC's existing git sign-in (nothing
  stored).
- **GitHub panel:** every repo on the account against this PC: CLONE what's missing, PUSH what's ahead, PUBLISH a
  local repo. Nothing is ever deleted.
- **NEW PROJECT:** a name and a line about it → the folder from the principles' templates, the first commit, the
  GitHub repo, the push, then the Claude handoff (project instructions and a kickoff prompt, each with COPY).

### Git on the card
- A **GIT chip** on every card says where the repo stands against GitHub and what to do next, and clicking it does
  it: **PULL ↓n**, **PUSH ↑n**, **PUBLISH BRANCH**, **COMMIT n** (opens the git panel), or **✓ IN STEP** (click to
  check GitHub now). Its tip says when GitHub was last checked. Right-click: pull, push, git panel, check now,
  Visual Studio, GitHub.
- **Odd situations block the buttons** and send you to **Visual Studio's Git Changes window** (the solution opens
  on it; if Visual Studio already has it open, that window comes to the front): a conflict or a merge / rebase in
  progress, both sides moved (DIVERGED), a detached HEAD, a branch deleted on GitHub, git busy, or a chat running
  something through the link.
- **Safe by design:** pull only fast-forwards and never with uncommitted changes; push never forces, checks GitHub
  first and refuses if GitHub moved; a new branch is published only if GitHub has none by that name.
- **The git panel** (from the chip): what GitHub has that you don't and the other way round (commit by commit), the
  changed files with checkboxes (new files and flagged ones start unticked: possible secrets like `.env` or keys,
  files over 50 MB, `_devSpace` scratch, line-ending-only changes), a suggested message you can edit, **COMMIT**
  and **COMMIT + PUSH**. It commits exactly the ticked files.
- **FETCH ALL** in the header checks GitHub for every project and says what needs doing ("2 TO PULL · 1 TO
  PUSH"); it also happens quietly every 15 minutes. The header counts TO PULL and TO PUSH.

### Updating Catalyzer
- When the Catalyzer repo holds a newer version than the one running, an **UPDATE** chip lights in the header.
  One click builds it, closes Catalyzer, installs on top and starts the new one, in about a minute. If the build
  fails, the running copy stays and the chip says so (click: the log; right-click: try again). Settings, links and
  notes backups always stay. `tools\install.cmd` does the same by double-click.

### Notes (Aaron's own)
- A **NOTES** chip on every card shows how many notes the repo's `docs/NOTES.md` holds; clicking it slides in the
  notes drawer.
- Type and press Enter: the note lands at the top, stamped with the date and time. RAW edits the whole file; EDITOR
  opens it in the editor; right-click a note to copy or remove it.
- **SYNC** commits NOTES.md alone, brings in notes written elsewhere (phone, another PC) and pushes. It happens by
  itself a minute after the last edit (Settings can turn that off), and a SYNC ↑/↓ chip shows when the card is out
  of step.
- **WIPE + SYNC** clears the notes for a fresh slate once a chat has pulled them in, after a confirm, and syncs.
- **Backups:** every version Catalyzer sees is kept in `%LOCALAPPDATA%\Catalyzer\notes-cache\`, up to a size cap
  per project (Settings, 100 MB by default). A project's backups are deleted when it leaves the board. NOTES
  BACKUPS (in the drawer) opens the folder.

### Working with Claude
- **LINK:** one click gives a repo a hidden Windows terminal its chat can use for builds, git and installs. Every
  command shows in the card's feed. Links come back when Catalyzer starts (a setting).
- **Helpers:** a project's long-running scripts run hidden from its card, and stop when Catalyzer stops. A helper
  only starts by itself after Aaron has started it once.
- **`_devSpace` chip:** the scratch pad's size; a cleanup that lists old files first and sends them to the Recycle
  Bin.

### The window
- **X keeps Catalyzer in the tray** (it keeps checking GitHub); click the tray icon to open it, right-click → Exit
  Catalyzer to quit. A one-time hint says so. Settings → General → *Close to the tray* turns it off (then X quits, or
  asks when work is running: ask / stop all / keep in tray). Run at Windows start goes straight to the tray.
  Windows shutting down always really quits.
- Live themes (4 built in, plus Nexus through its plugin), reduce motion, a single instance.
- **Plugins:** switch on or off, import a .dll or .zip (Settings → Plugins).

## A typical session
1. Catalyzer starts with Windows into the tray; open it from the tray or the Start menu.
2. Glance at the board: what's changed, what's ahead, which notes are new.
3. Jot a note on a card, or start a NEW PROJECT and paste the handoff into Claude.
4. Click LINK on the card a chat is working in; watch its commands in the feed.

## Where its data lives
| What | Where |
|---|---|
| Settings | `%APPDATA%\Catalyzer\settings.json` |
| Plugin data | `%APPDATA%\Catalyzer\plugin-data\<id>\` |
| Log | `%LOCALAPPDATA%\Catalyzer\log.txt` |
| Notes backups | `%LOCALAPPDATA%\Catalyzer\notes-cache\<project>\` |
| Update log | `%LOCALAPPDATA%\Catalyzer\update.log` |
| Install | `%LOCALAPPDATA%\Programs\Catalyzer` (Apps & Features entry; `tools\uninstall.ps1`) |

In a repo it only writes `docs/NOTES.md` (from the notes drawer), `.catalyzer/` (the link mailbox, git-ignored) and,
for NEW PROJECT, the new folder. It never deletes a repo, locally or on GitHub.

## Works with
- **GitHub:** clone, pull, push, publish, notes sync, through the PC's git sign-in.
- **Visual Studio** (Community is fine): everything git that isn't a safe one-click case.
- **Nexus:** the live Nexus look and the NEXUS button, through NexusPlugin. Catalyzer runs fine without it.
- **Claude chats:** through the repo folder: the link mailbox, `docs/NOTES.md` and the principles.
