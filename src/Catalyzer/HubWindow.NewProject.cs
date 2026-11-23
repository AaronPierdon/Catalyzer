using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// NEW PROJECT: every project starts here. One dialog (name, what it's for, private), then Catalyzer:
    ///   1. makes the folder in the projects folder from the principles' templates (docs, CLAUDE.md, catalyzer.json,
    ///      tools\, .gitignore, the principles version), filled in;
    ///   2. git init + the first commit;
    ///   3. creates the GitHub repo and pushes;
    ///   4. the card appears; then the Claude handoff: project instructions and the kickoff prompt, with COPY.
    /// </summary>
    public sealed partial class HubWindow
    {
        /// <summary>The principles folder: bundled with the install, or the Catalyzer repo's own (dev).</summary>
        private string PrinciplesDir()
        {
            var here = Path.Combine(AppContext.BaseDirectory, "principles");
            if (File.Exists(Path.Combine(here, "VERSION"))) return here;
            foreach (var p in _board.Items)
            {
                var d = Path.Combine(p.Folder, "principles");
                if (File.Exists(Path.Combine(d, "VERSION")) && Directory.Exists(Path.Combine(d, "templates"))) return d;
            }
            return null;
        }

        private void NewProject()
        {
            var d = Dialog("New project", 620, onEsc: null);
            d.Body.Children.Add(Para("Catalyzer makes the folder, fills in the repo from the principles' templates, creates it on GitHub, pushes the first commit, and hands you what to paste into Claude.", 13.5));
            d.Body.Children.Add(Caption("name (folder and GitHub repo)"));
            var (nameRow, name) = Field("e.g. GlassAlarm");
            d.Body.Children.Add(nameRow);
            d.Body.Children.Add(Caption("what it's for (one line)"));
            var (descRow, desc) = Field("e.g. A sci-fi alarm clock for the desk");
            d.Body.Children.Add(descRow);
            bool priv = true, gh = true;
            var toggles = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
            toggles.Children.Add(new CzToggle(true, v => priv = v, "Private on GitHub") { Margin = new Thickness(0, 0, 24, 0) });
            toggles.Children.Add(new CzToggle(true, v => gh = v, "Create it on GitHub"));
            d.Body.Children.Add(toggles);
            var root = ProjectsRoot;
            d.Body.Children.Add(Cz.Mono("IN  " + root.ToUpperInvariant(), 10, "CzTextFaintBrush").Also(t => t.Margin = new Thickness(0, 0, 0, 8)));
            var steps = new StackPanel(); d.Body.Children.Add(steps);
            d.Buttons.Children.Add(Btn("CANCEL", d.Close));
            var go = Btn("CREATE", null, "", primary: true);
            d.Buttons.Children.Add(go);
            Dispatcher.BeginInvoke(new Action(() => name.Focus()), System.Windows.Threading.DispatcherPriority.Input);

            go.Click = async () =>
            {
                steps.Children.Clear();
                var n = name.Text.Trim(); var what = desc.Text.Trim();
                var (r0, s0) = Step(""); steps.Children.Add(r0);
                if (!Regex.IsMatch(n, @"^[A-Za-z0-9._-]{1,80}$")) { s0("NAME: LETTERS, DIGITS, . _ - ONLY (NO SPACES)", Tone.Warn, false); return; }
                var dir = Path.Combine(root, n);
                if (Directory.Exists(dir)) { s0("A FOLDER CALLED " + n.ToUpperInvariant() + " IS ALREADY THERE", Tone.Warn, false); return; }
                var pr = PrinciplesDir();
                if (pr == null) { s0("CAN'T FIND THE PRINCIPLES' TEMPLATES (REINSTALL CATALYZER)", Tone.Danger, false); return; }
                if (gh && Prefs.Value.GitHubLogin == null)
                {
                    s0("CHECKING GITHUB...", Tone.Select, true);
                    try { var login = await GitHub.Login(); Prefs.Save(p => p.GitHubLogin = login); }
                    catch (Exception ex) { s0("GITHUB: " + ex.Message.ToUpperInvariant() + "  //  OR SWITCH 'CREATE IT ON GITHUB' OFF", Tone.Warn, false); return; }
                }
                go.IsEnabled = false;
                string url = null;
                try
                {
                    s0("MAKING THE FOLDER FROM THE TEMPLATES...", Tone.Select, true);
                    var version = File.ReadAllText(Path.Combine(pr, "VERSION")).Trim();
                    Scaffold(dir, n, what, version, pr);
                    s0("FOLDER READY  //  " + dir.ToUpperInvariant(), Tone.Ok, false);

                    var (r1, s1) = Step("FIRST COMMIT..."); steps.Children.Add(r1); s1("FIRST COMMIT...", Tone.Select, true);
                    foreach (var a in new[] { "init -b main", "add -A", "commit -q -m \"" + n + ": a new project, started in Catalyzer from the principles' templates (" + version + ")\"" })
                    {
                        var (c, o) = await GitHub.Git(dir, a);
                        if (c != 0) throw new Exception("git " + a.Split(' ')[0] + ": " + GitHub.First(o));
                    }
                    s1("FIRST COMMIT DONE", Tone.Ok, false);

                    if (gh)
                    {
                        var (r2, s2) = Step(""); steps.Children.Add(r2); s2("CREATING " + n.ToUpperInvariant() + " ON GITHUB...", Tone.Select, true);
                        var repo = await GitHub.Create(n, what, priv);
                        url = repo.HtmlUrl;
                        var add = await GitHub.Git(dir, "remote add origin \"" + repo.CloneUrl + "\"");
                        if (add.code != 0) throw new Exception(GitHub.First(add.output));
                        var push = await GitHub.Git(dir, "push -u origin main", 300000);
                        if (push.code != 0) throw new Exception("push: " + GitHub.First(push.output));
                        s2("ON GITHUB  //  " + url.ToUpperInvariant(), Tone.Ok, false);
                    }
                    Log.Write("new project " + n + " at " + dir + (url != null ? " / " + url : ""), show: true);
                    _board.Rescan();
                    d.Close();
                    Handoff(n, what, dir, url, version);
                }
                catch (Exception ex)
                {
                    var (re, se) = Step(""); steps.Children.Add(re); se(ex.Message.ToUpperInvariant(), Tone.Danger, false);
                    steps.Children.Add(Para("What's done stays done (nothing is undone or deleted). Fix the cause, then finish by hand or ask a chat; the folder is " + dir + ".", 12, "CzTextFaintBrush"));
                    go.IsEnabled = true; go.Click = d.Close; go.SetLabel("CLOSE");
                }
            };
        }

        /// <summary>The new repo from the templates, with the name, purpose and principles version filled in.</summary>
        private static void Scaffold(string dir, string name, string what, string version, string principles)
        {
            var t = Path.Combine(principles, "templates");
            Directory.CreateDirectory(Path.Combine(dir, "docs"));
            Directory.CreateDirectory(Path.Combine(dir, "tools"));
            Directory.CreateDirectory(Path.Combine(dir, "src"));
            Directory.CreateDirectory(Path.Combine(dir, "_devSpace"));
            string Fill(string s) => s.Replace("<App>", name).Replace("<one line: what it is>", what == "" ? name : what)
                                     .Replace("<one paragraph. The why and the where-next are in docs/VISION.md>", (what == "" ? name : what) + " The why and the where-next are in docs/VISION.md.");
            void Copy(string from, string to) { var src = Path.Combine(t, from); if (File.Exists(src)) File.WriteAllText(Path.Combine(dir, to), Fill(File.ReadAllText(src)), new UTF8Encoding(false)); }
            Copy("CLAUDE.md", "CLAUDE.md");
            Copy(".gitignore", ".gitignore");
            Copy("catalyzer.json", "catalyzer.json");
            foreach (var doc in new[] { "NOTES", "VISION", "ARCHITECTURE", "GUIDE", "HANDOFF", "STATE" }) Copy(doc + ".md", Path.Combine("docs", doc + ".md"));
            foreach (var f in Directory.Exists(Path.Combine(t, "tools")) ? Directory.GetFiles(Path.Combine(t, "tools")) : Array.Empty<string>())
                File.WriteAllText(Path.Combine(dir, "tools", Path.GetFileName(f)), Fill(File.ReadAllText(f)), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dir, ".catalyzer-principles"), version + "\n");
            File.WriteAllText(Path.Combine(dir, "README.md"), "# " + name + "\n\n" + (what == "" ? "" : what + "\n\n") +
                "Started in Catalyzer on " + DateTime.Now.ToString("yyyy-MM-dd") + ". Follows the Catalyzer Development Principles " + version + ".\n" +
                "How to build, install and run: see CLAUDE.md (Build / install / run).\n", new UTF8Encoding(false));
            var vision = Path.Combine(dir, "docs", "VISION.md");
            if (File.Exists(vision) && what != "") File.WriteAllText(vision, File.ReadAllText(vision).Replace("<the problem, in Aaron's words. One paragraph>", what), new UTF8Encoding(false));
        }

        /// <summary>After NEW PROJECT: what to paste into Claude, with COPY buttons, and the few clicks that remain.</summary>
        private void Handoff(string name, string what, string dir, string url, string version)
        {
            var gh = url != null ? url.Replace("https://", "") : "(not on GitHub yet)";
            var instructions =
                "Project repo: " + gh + " (PC: " + dir + ")\n" +
                "Rules: Catalyzer Development Principles, github.com/AaronPierdon/Catalyzer/tree/main/principles\n" +
                "(PC: " + Path.Combine(ProjectsRoot, "Catalyzer", "principles") + ")\n\n" +
                "Every chat:\n" +
                "1. Read CLAUDE.md and follow its new-chat steps (handover, notes, state, git log).\n" +
                "2. Check the Catalyzer link: bash .catalyzer/link/run.sh --status. If it's OFF, ask me to click LINK on the card. Builds, git and installs go through the link.\n" +
                "3. Follow the principles for the look, the workflow and the repo docs. Never bring back anything in STATE → Removed / decided against.\n" +
                "4. Tell me in a few lines what you understood and what you'll do, then start.\n" +
                "5. End every piece of work by updating HANDOFF (and NOTES / STATE if they changed), committing and pushing.";
            var kickoff =
                "This is the first chat for " + name + ": " + (what == "" ? "(tell Claude what it's for)" : what) + "\n" +
                "Catalyzer already made the repo from the principles' templates (" + version + "), pushed it" + (url != null ? " to " + gh : "") + ", and gave it a card.\n" +
                "- Read the principles: README, 04-WORKFLOW, 05-RULES, 07-APP-REPO (\"Wiring an app to Catalyzer\").\n" +
                "- Fill in VISION from what I said (ask me what you need), then CLAUDE.md, ARCHITECTURE and the tools\\ scripts for the stack we pick.\n" +
                "- Ask me to click LINK on the " + name + " card, check it with --status, and run the first build through it.\n" +
                "- Update HANDOFF / NOTES / STATE, commit and push.\n" +
                "- Then give me the Morning brief to schedule from inside this project (principles PROMPTS → Morning brief).";

            var d = Dialog(name + " is ready", 720, onEsc: null);
            d.Body.Children.Add(Para("The folder, the docs and the first commit are done" + (url != null ? ", and it's on GitHub" : "") + ". Now Claude, in about a minute:", 13.5, "CzTextBrush"));
            var how = new[]
            {
                "In the Claude desktop app on this PC: Projects → New project → name it " + name + " → add the folder " + dir + ".",
                "Paste the PROJECT INSTRUCTIONS below into the project's instructions.",
                "Start a chat in the project, paste the KICKOFF, send.",
                "Click LINK on the " + name + " card when the chat asks.",
            };
            for (int i = 0; i < how.Length; i++) d.Body.Children.Add(Para((i + 1) + ".  " + how[i], 13));
            void Block(string label, string text)
            {
                var head = new Grid { Margin = new Thickness(0, 10, 0, 6) };
                head.Children.Add(Caption(label));
                var copy = new CzButton("COPY", null, "", height: 26) { HorizontalAlignment = HorizontalAlignment.Right };
                copy.Click = () => { try { Clipboard.SetText(text); copy.SetLabel("COPIED"); Say(label.ToUpperInvariant() + " COPIED"); } catch (Exception ex) { Log.Write("copy: " + ex.Message, show: true); } };
                head.Children.Add(copy);
                d.Body.Children.Add(head);
                var box = Cz.Mono(text, 10.5, "CzTextBrush"); box.TextWrapping = TextWrapping.Wrap; box.Margin = new Thickness(10, 0, 0, 6);
                d.Body.Children.Add(box);
            }
            Block("project instructions", instructions);
            Block("kickoff", kickoff);
            d.Buttons.Children.Add(Btn("OPEN FOLDER", () => Launch.Shell("explorer.exe", "\"" + dir + "\""), ""));
            if (url != null) d.Buttons.Children.Add(Btn("GITHUB", () => Launch.Url(url), ""));
            d.Buttons.Children.Add(Btn("DONE", d.Close, primary: true));
        }
    }
}
