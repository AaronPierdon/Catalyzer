# 05 — Hard rules

These are never broken. An app's CLAUDE.md can add rules but never loosen these.

1. **Touch only the repo you were asked to work in.** Other repos under `C:\Users\Aaron\source\repos` may be *read*
   for reference (MAWP is the look reference), never written, unless Aaron explicitly says otherwise for that
   task.
2. **Never look in `repos\archives`.** It's old material and deliberately off limits.
3. **Never print secrets.** This includes the SQL password (MAWP's, DPAPI-protected) and any key or token. Never
   in chat, logs, docs or commits. Never commit `.env`, credentials or `settings.local` files.
4. **Don't run MAWP agents.**
5. **Warn Aaron before every install** that restarts something he's using. Say what will blink or close (Nexus
   install: the bar blinks, Nexus Explorer windows and their terminals close).
6. **Computer control only with permission.**
   - Read the app's computer-use notes first (Nexus: `docs/COMPUTER-USE.md`).
   - Work in short bursts and release control when done.
   - Never inject fake mouse or keyboard input with scripts to get around the permission system.
   - Screenshot and crop scripts are fine, and so is closing our own windows.
7. **Nothing removed comes back** unless Aaron asks. Check *Decided against* in the app's `ROADMAP.md`
   before restoring anything found in history or old docs.
8. **Never leave Aaron without his tools.** Shell replacements keep a way back (watchdog, panic key, restore
   command). Destructive actions (delete, force-push, branch deletes) are confirmed first.
9. **Edits happen in the working copy that gets synced.** In the cloud setup, edit the container copy and sync. Edits
   made only on the PC get overwritten.
10. **Old `_tools` queue helpers run outside our apps** (the Catalyzer link replaces them; see 04). Start it from Windows' own Explorer or Run, never inside a Nexus Explorer
    terminal: an install closes Explorer and kills the helper mid-install. Through the link, install Catalyzer
    itself with `run.sh -s` (survive).
11. **Commit and push every piece of work, every time, in every project.** When a change works (it builds, the docs
    are updated), the chat commits it and pushes it to GitHub straight away. Don't ask first, and don't save it up
    for the end of the chat or the next chat. One piece of work is one commit (or a few small ones); a chat that
    does three things pushes three times.
    - Git runs on Windows through the link (Aaron's credentials, his lock files), so the local repo and GitHub
      both get it. Never run git from the Linux VM.
    - Link OFF: ask Aaron to click LINK and wait for it (or use the old helper's `git` action). Then commit and push.
    - Still can't: the reply's **first line** says `NOT COMMITTED / NOT PUSHED: <why>`, and HANDOFF → *Next* starts
      with it, so the next chat does it first.
    - Rules 3 and 8 still hold: no secrets, no force-push. NOTES.md is committed only on Aaron's word.
12. **Nothing personal or machine-specific in the repo, so every repo can go public.** Never hardcode IPs, host or
    machine names, phone numbers, email addresses, real names, account ids, local user paths (`C:\Users\Aaron\…`)
    or keys in any code or config file in the repo.
    - Real values live outside it: `%APPDATA%\<App>\settings.json`, environment variables, or the user's input.
    - The repo ships only placeholders: `example.com`, `192.0.2.1`, `<your-email>`, in a `*.example` file if needed.
    - Temp data, dumps and test values go in `_devSpace/`. It's never committed: every repo's `.gitignore` lists
      `_devSpace/`, Catalyzer adds it to each repo's `.git/info/exclude`, and its git drawer never lists or commits
      anything under it.
    - Before a repo goes public, scan its whole history for these (not just the current files).
