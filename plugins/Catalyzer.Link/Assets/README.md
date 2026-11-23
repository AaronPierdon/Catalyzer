# Catalyzer link — a hidden Windows terminal for this repo

Catalyzer (Aaron's project dashboard) writes this folder when **LINK** is on for this project's card. While it's on,
anything that can see this repo folder can run commands **on the Windows PC**, hidden (no console window), in the
repo's root folder, with Aaron's own tools and sign-ins (`dotnet`, `git` + GitHub credentials, PowerShell, npm,
Android SDK...). Aaron sees every command and its output in Catalyzer (right-click the LINK chip → Show).

## Use it
```bash
bash .catalyzer/link/run.sh --status                  # ACTIVE?  If OFF: ask Aaron to click LINK on the card
bash .catalyzer/link/run.sh 'dotnet build'            # PowerShell; waits, prints output, exits with its code
bash .catalyzer/link/run.sh -f tools/install.ps1      # a script file from the repo
bash .catalyzer/link/run.sh -t 1800 -w 170 'gradlew assembleDebug'   # long: PC timeout 30 min, wait here 170 s
bash .catalyzer/link/run.sh --wait <id>               # keep following one that's still running
bash .catalyzer/link/run.sh --cancel <id>             # stop it and everything it started
bash .catalyzer/link/run.sh -b 'npm run dev'          # background: runs until stopped or unlinked
bash .catalyzer/link/run.sh -s -f tools/install.ps1   # survive: outlives Catalyzer (only for installing Catalyzer)
```
- Each call is a fresh PowerShell (Windows PowerShell 5.1) started in the repo root. Chain steps in one call:
  `'cd src; dotnet test; git status'`.
- Requests run **one at a time, in order** (background ones excepted). Default PC timeout 15 min.
- Output lines starting with `! ` came from the error stream (git prints progress there: not always a failure —
  trust the exit code).
- No input: anything that waits for a key or a password gets end-of-input. Git never prompts (`GIT_TERMINAL_PROMPT=0`).

## How it works (if run.sh isn't usable)
- Write a PowerShell script to `in/<id>.ps1` (write `<id>.ps1.part`, then rename). `<id>`: letters, digits, `-`, `_`.
  Optional first line: `# catalyzer: timeout=<seconds> [background] [survive]`. `.cmd` files work too.
- Catalyzer picks it up within a second, moves it to `out/<id>.ps1`, streams output to `out/<id>.log`, and writes the
  exit code to `out/<id>.done`. `in/<id>.cancel` stops it.
- `STATUS`: first line `ACTIVE` (rewritten every 15 s) or `OFF`. ACTIVE older than ~2 min means Catalyzer isn't running.
- Only requests written **after** the link was switched on run; anything older is set aside, never run.
- `out/` keeps 3 days. This whole folder is git-ignored (`.catalyzer/.gitignore`).

## Rules that still apply
The repo's CLAUDE.md and the Catalyzer Development Principles (`repos\Catalyzer\principles`): never print secrets, never touch `repos\archives`, warn
Aaron before installs that restart something he's using, destructive git (force-push, branch deletes) only after he
says yes. The link makes commands possible; it doesn't make them approved.
