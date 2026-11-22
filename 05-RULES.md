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
7. **Nothing removed comes back** unless Aaron asks. Check *Removed / decided against* in the app's `STATE.md`
   before restoring anything found in history or old docs.
8. **Never leave Aaron without his tools.** Shell replacements keep a way back (watchdog, panic key, restore
   command). Destructive actions (delete, force-push, branch deletes) are confirmed first.
9. **Edits happen in the working copy that gets synced.** In the cloud setup, edit the container copy and sync. Edits
   made only on the PC get overwritten.
10. **The helper runs outside our apps.** Start it from Windows' own Explorer or Run, never inside a Nexus Explorer
    terminal: an install closes Explorer and kills the helper mid-install.
