# BACKLOG — Aaron's list + bugs

## Next up
- [ ] Update to Nexus Development Principles 2.1.0 (on 1.0.0; 2.0.0 moves this list into NOTES.md).
- [ ] **NEW PROJECT: every project starts here** (Aaron, 2026-11-23). He never makes a repo or folder by hand again.
      One dialog: name, purpose, public / private. Then Catalyzer, automatically:
      1. creates `AaronPierdon/<Name>` through the GitHub REST API (`POST /user/repos`);
      2. creates `%USERPROFILE%\source\repos\<Name>`, copies in the principles' `templates/`, `catalyzer.json` and
         `.nexus-principles` (current VERSION), makes the first commit and pushes (git hidden, as always);
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
- [ ] Helpers: launch each project's helper scripts hidden, status per card (plugin).
- [ ] Principles badge: show each repo's `.nexus-principles` version vs the current principles.

## Ideas
- [ ] Repo plugin: pull / push / fetch-all buttons.
- [ ] Tray icon + flyout.
- [ ] Drag to reorder cards.

## Bugs
