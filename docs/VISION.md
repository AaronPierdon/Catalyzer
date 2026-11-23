# VISION — Catalyzer

## Why it exists
Aaron builds several apps at once (Nexus, MAWP, phone apps, utilities) across several machines and chats. Without a
hub, the work scatters:
- which repo is where, and what state it's in;
- notes in text files on the file server;
- an ugly command prompt left open just so Claude can build and install.

Catalyzer is the **developer hub**: every project in one beautiful place, with its notes, its state and a hidden
terminal, so building feels like the rest of the Nexus world, not like Windows plumbing.

## What it is now (0.2)
- **The board:** every repo under `source\repos` as a glass card, showing git state, activity, and buttons (folder,
  GitHub, VS Code, Visual Studio, terminal, plus the project's own).
- **Notes:** each repo's `docs/NOTES.md` from its card: add, tick, move, sync to GitHub.
- **Link:** one click gives a repo a hidden Windows terminal its chat can use (builds, git, installs).
- **Helpers:** a project's long-running scripts, hidden, with a light per card.
- Live themes, Nexus included through a plugin; a plugin SDK; starts with Windows; a tray.

## Where it could go
1. **Every app adopts the principles** (2.0.0), so every card has notes, docs and a link.
2. **Principles badge:** each card shows whether its repo is behind the principles.
3. **Cross-device:** Catalyzer on the laptop too, with GitHub as the backend; each PC runs its own link.
4. **Project groups:** several repos worked as one (the music player's PC + Android pair).
5. **Notes from anywhere:** an ideas repo, and notes from the phone through chats.
6. **A New app button:** create a repo from the templates, register it on GitHub, and it's on the board.
7. **Claude Code launcher:** a card button that opens a Claude Code session in the repo, as an alternative to the
   chat + link.
8. **Nexus integration** as a developer utility (maybe "Nexus Catalyzer"), always standalone so Nexus reinstalls
   never touch it.

## Not doing
- Not a code editor or a git client. It launches the real ones and shows state.
- No custom backend database or file sync. GitHub and the file server are the backends.
- Never a hard dependency on Nexus. Nexus only arrives through a plugin.

## Fits with
- **Nexus:** NexusPlugin follows the live Nexus look; the NEXUS button opens Nexus Explorer.
- **Nexus Development Principles:** Catalyzer is the hub the principles assume (07): `catalyzer.json`, NOTES, the link.
- **Every app:** reads its `catalyzer.json`, `docs/NOTES.md` and `.catalyzer/link`.
