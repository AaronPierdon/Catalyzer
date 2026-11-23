using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Catalyzer.Sdk;

namespace Catalyzer
{
    /// <summary>
    /// First start: where your projects live, and GitHub (the PC's own git sign-in, nothing stored by Catalyzer).
    /// Shown once (Prefs.FirstRunDone); Settings → General can show it again.
    /// </summary>
    public sealed partial class HubWindow
    {
        private void Welcome()
        {
            var d = Dialog("Welcome to Catalyzer", 640);
            d.Body.Children.Add(Para("Every project you build, on one board: its git state, its notes, and a hidden terminal Claude can use. Two quick things first.", 14, "CzTextBrush"));

            // 1. projects folder
            d.Body.Children.Add(Caption("1  //  where your projects live"));
            var root = Prefs.Value.Roots.FirstOrDefault() ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "source", "repos");
            var folderRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            folderRow.ColumnDefinitions.Add(new ColumnDefinition()); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var path = Cz.Mono(root.ToUpperInvariant(), 11.5, "CzTextBrush"); path.VerticalAlignment = VerticalAlignment.Center; path.TextTrimming = TextTrimming.CharacterEllipsis;
            folderRow.Children.Add(path);
            var change = new CzButton("CHANGE", () =>
            {
                var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "The folder that holds your project folders", InitialDirectory = Directory.Exists(root) ? root : null };
                if (dlg.ShowDialog(this) != true) return;
                root = dlg.FolderName; path.Text = root.ToUpperInvariant();
            }, "", height: 30);
            Grid.SetColumn(change, 1); folderRow.Children.Add(change);
            d.Body.Children.Add(folderRow);
            d.Body.Children.Add(Para("Each folder in it with git (or a catalyzer.json) becomes a card. New projects are created here too.", 12.5, "CzTextFaintBrush"));

            // 2. GitHub
            d.Body.Children.Add(Caption("2  //  GitHub"));
            var (ghRow, ghSet) = Step(Prefs.Value.GitHubLogin != null ? "SIGNED IN AS " + Prefs.Value.GitHubLogin.ToUpperInvariant() : "NOT SIGNED IN YET");
            if (Prefs.Value.GitHubLogin != null) ghSet("SIGNED IN AS " + Prefs.Value.GitHubLogin.ToUpperInvariant(), Tone.Ok, false);
            d.Body.Children.Add(ghRow);
            var ghBtn = new CzButton("SIGN IN WITH GITHUB", null, "", height: 32) { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 6) };
            ghBtn.Click = async () =>
            {
                ghSet("ASKING GITHUB...", Tone.Select, true);
                try { var login = await GitHub.Login(); Prefs.Save(p => p.GitHubLogin = login); ghSet("SIGNED IN AS " + login.ToUpperInvariant(), Tone.Ok, false); }
                catch (Exception ex) { ghSet(ex.Message.ToUpperInvariant(), Tone.Warn, false); }
            };
            d.Body.Children.Add(ghBtn);
            d.Body.Children.Add(Para("Uses the sign-in git already has on this PC (Git Credential Manager). If there isn't one, GitHub's own sign-in window opens. Catalyzer never stores or shows the key. It's for: listing your repos, cloning, publishing, NEW PROJECT.", 12.5, "CzTextFaintBrush"));

            d.Buttons.Children.Add(Btn("LATER", () => { Prefs.Save(p => p.FirstRunDone = true); d.Close(); }, tip: "SETTINGS  //  GENERAL HAS THIS AGAIN"));
            d.Buttons.Children.Add(Btn("START", () =>
            {
                Prefs.Save(p =>
                {
                    p.FirstRunDone = true;
                    if (!p.Roots.Any(r => string.Equals(r, root, StringComparison.OrdinalIgnoreCase))) { p.Roots.Clear(); p.Roots.Add(root); }
                });
                d.Close();
                _board.Rescan();
                Say("READY  //  + MAKES A NEW PROJECT  ·  THE GITHUB BUTTON LISTS YOUR REPOS");
            }, "", primary: true));
        }
    }
}
