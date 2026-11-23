# NOTES — Catalyzer

The shared notepad: Aaron, Claude and Catalyzer's notes panel all write here. New things go in *Inbox*; chats sort
them into the other sections. Never delete Aaron's words. Done items move to *Done* with a date.

## Inbox

- [ ] Tst 3 (Aaron, 2026-11-23)
- [ ] This isa test note for Claude to get. My favorite number is 21. (Aaron, 2026-11-23)
- [ ] This is a second test. (NO TIME STAMP MUAHAHHA)

## Next up
- [ ] **NEW PROJECT: every project starts here** (Aaron, 2026-11-23). He never makes a repo or folder by hand again.
      One dialog: name, purpose, public / private. Then Catalyzer, automatically:
      1. creates `AaronPierdon/<Name>` through the GitHub REST API (`POST /user/repos`);
      2. creates `%USERPROFILE%\source\repos\<Name>`, copies in the principles' `templates/`, `catalyzer.json` and
         the principles marker (current VERSION), makes the first commit and pushes (git hidden, as always);
      3. adds the card, then shows the Claude handoff with copy buttons: project name and instructions, the
         filled-in PROMPTS → *Kickoff*, and the steps (new project in Claude from the folder → paste Kickoff → send →
         that chat sets up the *Morning brief* scheduled task).

      Claude Desktop has no documented way to create projects or scheduled tasks from outside, `claude://` links only
      prefill, and folder links always ask. So the Claude part stays about a minute of clicks; don't try to automate
      the desktop app.
- [ ] **GitHub dashboard** (Aaron, 2026-11-23): list every repo on the account, public and private, with name,
      visibility, last push and URL (open / copy). Show local status where a card exists; offer CLONE where it
      doesn't. Nice to have: RENAME (`PATCH /repos/{owner}/{repo}`) and DELETE (`DELETE /repos/...`, needs the token's
      delete permission; type the repo name to confirm, per principles 05 rule 8).
      - **Token:** a fine-grained personal access token for all repos (Administration read / write to create and
        rename; Metadata and Contents read). Store it encrypted with DPAPI in settings. Never log or show it.
      - Use **Octokit** (GitHub's official .NET client) or plain `HttpClient`. `GET /user/repos?affiliation=owner`,
        paged. Cache the list and refresh on F5.
      - Probably a plugin (`Catalyzer.GitHub`), so it fits the SDK and the board.
- [ ] Live check of the `pc/2026-11-22-helpers` branch, then merge and install through the link. Covers: Aaron
      clicks LINK, then git, snap and cancel go through the link; the notes panel; helpers; tray. See HANDOFF.
- [ ] Aaron links the phone app and the two desktop apps. Each one's chat runs "update to the latest principles"
      (2.0.0: NOTES, VISION, ARCHITECTURE, GUIDE).
- [ ] Helpers: add real `helpers` to the projects that have them (ask Aaron which).
- [ ] Principles badge: show each repo's `.catalyzer-principles` (or old `.nexus-principles`) version against the current principles VERSION
      (behind = warn).

- [ ] **First-run setup** (Aaron, 2026-11-23): on a new install, a glass welcome asks for the projects folder
      (default `source\repos`), then GitHub: reuse the PC's git sign-in (Git Credential Manager, after asking) or
      open GitHub's own sign-in. Stored with DPAPI, never shown or logged. No Claude API key (scrapped: API use is
      billed apart from Pro, and Aaron works in the desktop app).
- [ ] **GitHub panel** (Aaron, 2026-11-23): every repo on the account, public and private, each marked:
      on this PC (it has a card); not here (CLONE into the projects folder); local only (PUBLISH = create + push);
      ahead (PUSH). No cleanup. Merges with the GitHub dashboard item above.
- [ ] **`_devSpace` on cards** (Aaron, 2026-11-23): show its size; suggest cleaning files untouched since well before
      the last commit, always listing them first, never deleting silently.

## Ideas
- [ ] Cross-device Catalyzer (Aaron, 2026-11-22): the same dashboard on the laptop, with GitHub as the backend
      (every machine pulls); maybe the phone later. Each PC runs its own link.
- [ ] Notes from the phone (Aaron, 2026-11-22): say "note this for <App>" in any chat, and it lands in that repo's
      NOTES Inbox; Catalyzer shows it after SYNC. An "ideas" repo for notes with no app yet.
- [ ] Project groups (Aaron, 2026-11-22): work on several repos as one, e.g. the music player's PC and Android
      repos that hand playback to each other. A group card, linked together.
- [ ] A CLAUDE button per card that opens Claude Code in that repo (and recent sessions), as an alternative to the
      chat + link (Aaron, 2026-11-22).
- [x] The principles moved into this repo as Catalyzer Development Principles 3.0.0 (Aaron, 2026-11-23) — done 2026-11-23
- [ ] Rename to "Nexus Catalyzer", and maybe the principles to a brand name. Parked: naming later is cheap, and
      GitHub redirects old links (Aaron, 2026-11-22).
- [ ] Repo plugin: pull / push / fetch-all buttons.
- [ ] Drag to reorder cards.
- [ ] A New app button: creates the repo from the principles' `templates/` and registers it on GitHub.

## Bugs

## Done
- [x] Notes auto-sync: NOTES.md only, a minute after the last edit, generated commit messages; SYNC ↑/↓ chip on the card; pull on open when safe; Settings toggle — done 2026-11-23
- [x] Notes panel: the NOTES chip on every card plus the notes drawer, with add / tick / move / RAW / SYNC — done 2026-11-22
- [x] Link: a hidden Windows terminal per repo for chats — done 2026-11-22
- [x] Helpers: hidden long-running scripts per card, close prompt, tray — done 2026-11-22
- [x] GitHub repos for Catalyzer and the principles — done 2026-11-22
