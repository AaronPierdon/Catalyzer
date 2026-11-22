# The link — a hidden Windows terminal per project

**For Aaron:** click **LINK** on a project's card. That project now has a terminal on this PC that a chat (Claude)
can use: builds, git, installs, anything. There's no window: the terminal is hidden inside Catalyzer.
- **It stays linked** across restarts until you click again to unlink. Catalyzer starts with Windows, and linked
  cards relink by themselves.
- **Right-click the chip → Show the feed** to see every command and its output, live, with Cancel and Unlink.
- **Unlink, close Catalyzer, or a crash** stops everything the link started. The one exception is a *survive*
  request, used only to install Catalyzer itself.
- The chip reads **LINK** (off), **LINKED** (on) or **LINKED · RUNNING** (a command is running).

**For a chat:** read `.catalyzer/link/README.md` in the repo. Catalyzer writes it there. In short:
```bash
bash .catalyzer/link/run.sh --status
bash .catalyzer/link/run.sh 'dotnet build'
```

## How it works
- There's no network and no port. The channel is a mailbox folder inside the repo, `.catalyzer\link\`. Catalyzer makes
  it git-ignored by writing `.catalyzer\.gitignore` (`*`).
- A chat only reaches repos linked to it, so each chat reaches only its own project's terminal.
- A request is a PowerShell script dropped into `in\`. Catalyzer then:
  - moves it to `out\` and runs it hidden (`CreateNoWindow`) in the repo root, inside a kill-on-close job;
  - streams the output to `out\<id>.log` and writes the exit code to `out\<id>.done`;
  - runs requests one at a time. *Background* ones (`-b`) run beside the queue.
- **Guards:**
  - the default timeout is 15 min;
  - requests that were already waiting when the link came on are set aside, never run (so a repo can't arrive
    with a command in it);
  - `out\` keeps 3 days.
- `STATUS` says `ACTIVE`, refreshed every 15 s. When the link is off, it says `OFF`. A stale `ACTIVE` means
  Catalyzer isn't running.
- **Installing Catalyzer through its own link:** use `run.sh -s`. The request survives Catalyzer closing and keeps
  logging. The new Catalyzer relinks and `run.sh` sees the exit code.

## What it is not
- **Not a permission system.** The link runs what it's sent, as Aaron. The chat's own rules still decide what's
  allowed: the CLAUDE.md, the Nexus Development Principles, and asking before installs, deletes and force-pushes.
  The feed is how Aaron sees what happened.
- **Not for the Nexus helper.** Nexus keeps its own `_tools` helper until it adopts the link.
