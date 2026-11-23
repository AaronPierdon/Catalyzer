using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Catalyzer
{
    /// <summary>
    /// GitHub, the way git on this PC already talks to it. The sign-in is the one Git Credential Manager keeps for git
    /// (asked for with `git credential fill`, so if it's missing, GitHub's own sign-in window opens). Catalyzer never
    /// stores the token, never logs it and never shows it: it borrows it for one request and forgets it.
    /// </summary>
    public static class GitHub
    {
        public sealed class Repo
        {
            public string Name, FullName, Description, HtmlUrl, CloneUrl, Owner;
            public bool Private;
            public DateTime? Pushed;
        }

        private static readonly HttpClient Http = new HttpClient { BaseAddress = new Uri("https://api.github.com/"), Timeout = TimeSpan.FromSeconds(20) };

        static GitHub()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("Catalyzer/1.0");
            Http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            Http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        }

        /// <summary>The token git uses for github.com (Git Credential Manager). May open GitHub's sign-in window.</summary>
        private static string Token()
        {
            var psi = new ProcessStartInfo("git", "credential fill")
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            };
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            using var p = Process.Start(psi);
            p.StandardInput.Write("protocol=https\nhost=github.com\n\n");
            p.StandardInput.Close();
            var o = p.StandardOutput.ReadToEndAsync();
            if (!p.WaitForExit(120000)) { try { p.Kill(true); } catch { } throw new Exception("GitHub sign-in timed out"); }
            var pass = o.Result.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("password="));
            if (pass == null) throw new Exception("no GitHub sign-in on this PC (sign in, then try again)");
            return pass.Substring(9);
        }

        private static async Task<JsonDocument> Call(HttpMethod method, string path, object body = null)
        {
            var token = Token();
            using var req = new HttpRequestMessage(method, path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var res = await Http.SendAsync(req);
            var text = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
            {
                string msg = null;
                try { using var d = JsonDocument.Parse(text); if (d.RootElement.TryGetProperty("message", out var m)) msg = m.GetString(); } catch { }
                throw new Exception("GitHub " + (int)res.StatusCode + ": " + (msg ?? res.ReasonPhrase));
            }
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
        }

        /// <summary>Who's signed in (login), or an exception saying why not.</summary>
        public static async Task<string> Login()
        {
            using var d = await Call(HttpMethod.Get, "user");
            return d.RootElement.GetProperty("login").GetString();
        }

        /// <summary>Every repo the account owns or works on, newest push first.</summary>
        public static async Task<List<Repo>> Repos()
        {
            var list = new List<Repo>();
            for (int page = 1; page <= 10; page++)
            {
                using var d = await Call(HttpMethod.Get, "user/repos?per_page=100&sort=pushed&affiliation=owner,collaborator,organization_member&page=" + page);
                int n = 0;
                foreach (var r in d.RootElement.EnumerateArray())
                {
                    n++;
                    string S(string k) => r.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    list.Add(new Repo
                    {
                        Name = S("name"), FullName = S("full_name"), Description = S("description"), HtmlUrl = S("html_url"), CloneUrl = S("clone_url"),
                        Owner = r.TryGetProperty("owner", out var o) && o.TryGetProperty("login", out var l) ? l.GetString() : "",
                        Private = r.TryGetProperty("private", out var pv) && pv.GetBoolean(),
                        Pushed = DateTime.TryParse(S("pushed_at"), out var t) ? t.ToLocalTime() : (DateTime?)null,
                    });
                }
                if (n < 100) break;
            }
            return list;
        }

        /// <summary>A new, empty repo on the signed-in account. Returns its clone URL.</summary>
        public static async Task<Repo> Create(string name, string description, bool isPrivate)
        {
            using var d = await Call(HttpMethod.Post, "user/repos", new { name, description = description ?? "", @private = isPrivate, auto_init = false });
            var r = d.RootElement;
            return new Repo { Name = r.GetProperty("name").GetString(), FullName = r.GetProperty("full_name").GetString(), HtmlUrl = r.GetProperty("html_url").GetString(), CloneUrl = r.GetProperty("clone_url").GetString(), Private = isPrivate };
        }

        /// <summary>"https://github.com/Me/X.git" and "git@github.com:me/x" → "me/x" (for matching cards to repos).</summary>
        public static string Key(string url)
        {
            var w = Projects.WebUrl(url ?? "");
            if (w == null || !w.Contains("github.com/", StringComparison.OrdinalIgnoreCase)) return null;
            return w.Substring(w.IndexOf("github.com/", StringComparison.OrdinalIgnoreCase) + 11).Trim('/').ToLowerInvariant();
        }

        // ---------------------------------------------------------------- git (hidden), for clone / publish / push

        public static Task<(int code, string output)> Git(string dir, string args, int ms = 300000) => Task.Run(() =>
        {
            try
            {
                var psi = new ProcessStartInfo("git", (dir != null ? "-C \"" + dir + "\" " : "") + args)
                {
                    RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                };
                psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
                using var p = Process.Start(psi);
                var o = p.StandardOutput.ReadToEndAsync(); var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(ms)) { try { p.Kill(true); } catch { } return (124, "timed out"); }
                return (p.ExitCode, (o.Result + e.Result).Trim());
            }
            catch (Exception ex) { return (127, ex.Message); }
        });

        public static string First(string s) => (s ?? "").Split('\n').Select(x => x.Trim()).FirstOrDefault(x => x.Length > 0 && !x.StartsWith("hint:")) ?? "";
    }
}
