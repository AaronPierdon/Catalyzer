using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Catalyzer
{
    /// <summary>
    /// Catalyzer.exe [--autostart] [--restart] [--snapshot dir]
    /// One copy runs; starting it again brings that copy forward. --autostart (from the Run key) starts minimized.
    /// --snapshot renders the board in every theme to PNGs and exits (never writes settings).
    /// </summary>
    public sealed class App : Application
    {
        private static Mutex _one;
        /// <summary>A second start sets this; the running copy comes forward (from the tray too).</summary>
        private const string ShowSignal = @"Local\Catalyzer.Show";
        private static EventWaitHandle _show;

        [STAThread]
        public static int Main(string[] args)
        {
            bool restart = args.Contains("--restart");
            int si = Array.IndexOf(args, "--snapshot");
            string snap = si >= 0 && si + 1 < args.Length ? args[si + 1] : null;
            if (snap != null) Prefs.Offline = true;
            else
            {
                // one copy: a second start just brings the first forward (a restart waits for the old one to leave)
                bool mine = false;
                for (int i = 0; i < (restart ? 40 : 1) && !mine; i++)
                {
                    _one = new Mutex(true, @"Local\Catalyzer.One", out mine);
                    if (!mine) { _one.Dispose(); if (restart) Thread.Sleep(150); }
                }
                if (!mine)
                {
                    try { using var ev = EventWaitHandle.OpenExisting(ShowSignal); ev.Set(); } catch { Forward(); }
                    return 0;
                }
                _show = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSignal);
            }
            var app = new App();
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.DispatcherUnhandledException += (s, e) => { Log.Write(e.Exception); e.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write(e.ExceptionObject as Exception ?? new Exception("unknown"));
            app.Startup += (s, e) => app.Boot(args.Contains("--autostart"), snap);
            app.Exit += (s, e) => { PluginHost.I?.StopAll(); };
            return app.Run();
        }

        private void Boot(bool autostart, string snap)
        {
            Log.Write("start " + string.Join(" ", Environment.GetCommandLineArgs().Skip(1)));
            ThemeService.I.Init();
            var board = new Board();
            var host = new PluginHost(board);
            host.LoadAll();
            var w = new HubWindow(board);
            MainWindow = w;
            if (snap != null) { Snapshot(w, board, snap); return; }
            if (autostart) { w.WindowState = WindowState.Minimized; w.ShowActivated = false; }
            w.Show();
            if (_show != null)
                new Thread(() => { try { while (_show.WaitOne()) Dispatcher.BeginInvoke(new Action(w.FromTray)); } catch { } }) { IsBackground = true, Name = "show-signal" }.Start();
            board.Rescan();
        }

        /// <summary>Render the window in each theme once the cards have settled.</summary>
        private async void Snapshot(HubWindow w, Board board, string dir)
        {
            Directory.CreateDirectory(dir);
            w.Width = 1600; w.Height = 940; w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = -4000; w.Top = 0; w.ShowActivated = false;
            w.Show();
            board.Rescan();
            while (board.Scanning) await System.Threading.Tasks.Task.Delay(200);
            foreach (var t in ThemeService.I.All.ToList())
            {
                ThemeService.I.Apply(t.Id);
                await System.Threading.Tasks.Task.Delay(1600);   // decode + entrance + git settle
                Save(w, Path.Combine(dir, "catalyzer-" + t.Id + ".png"));
            }
            w.OpenSettingsForSnapshot();
            await System.Threading.Tasks.Task.Delay(800);
            Save(w, Path.Combine(dir, "catalyzer-settings.png"));
            w.PromptForSnapshot();
            await System.Threading.Tasks.Task.Delay(900);
            Save(w, Path.Combine(dir, "catalyzer-close.png"));
            w.FeedForSnapshot();
            await System.Threading.Tasks.Task.Delay(900);
            Save(w, Path.Combine(dir, "catalyzer-feed.png"));
            Shutdown();
        }

        private static void Save(FrameworkElement e, string file)
        {
            e.UpdateLayout();
            var bmp = new RenderTargetBitmap((int)e.ActualWidth, (int)e.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(e);
            var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(bmp));
            using var f = File.Create(file); enc.Save(f);
            Log.Write("snapshot " + file);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr h);

        private static void Forward()
        {
            var h = FindWindow(null, "Catalyzer");
            if (h == IntPtr.Zero) return;
            ShowWindow(h, IsIconic(h) ? 9 : 5);   // restore : show
            SetForegroundWindow(h);
        }
    }
}
