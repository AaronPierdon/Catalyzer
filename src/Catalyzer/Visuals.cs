using System;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace Catalyzer
{
    /// <summary>How hard the look works: FULL (everything) → BALANCED → ECO (nothing moves on its own, no glow) → STATIC.</summary>
    public enum VisualLevel { Full, Balanced, Eco, Static }

    /// <summary>How a frame clock is paced: ambience (backdrop, reactor), status pulses (LEDs) or feedback (hover, grow-in).</summary>
    public enum Pace { Ambient, Pulse, Feedback }

    /// <summary>
    /// The visuals level in force: one for this PC, one for Remote Desktop (RDP renders WPF in software, so the
    /// backdrop and glow land on the CPU). Ambient motion also stops whenever nobody can see it: minimized, in the
    /// tray, locked, or the remote session disconnected.
    /// </summary>
    public static class Visuals
    {
        public static event Action Changed;

        [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
        private const int SM_REMOTESESSION = 0x1000;
        /// <summary>This copy is running inside a Remote Desktop session.</summary>
        public static bool Remote { get { try { return GetSystemMetrics(SM_REMOTESESSION) != 0; } catch { return false; } } }

        private static bool _minimized, _away;
        /// <summary>Nobody can see the window: ambience and pulses stop.</summary>
        public static bool Paused => _minimized || _away;

        public static VisualLevel Level => Parse(Remote ? Prefs.Value.VisualsRemote : Prefs.Value.VisualsLocal, Remote ? VisualLevel.Eco : VisualLevel.Full);
        public static VisualLevel Parse(string s, VisualLevel fallback) => Enum.TryParse<VisualLevel>(s, true, out var v) ? v : fallback;
        public static string Id(VisualLevel v) => v.ToString().ToLowerInvariant();

        /// <summary>Glow effects (DropShadow) are on.</summary>
        public static bool Glow => Level <= VisualLevel.Balanced;
        /// <summary>A theme's own backdrop (a plugin's, unthrottled) is used only at FULL; below that, Catalyzer's.</summary>
        public static bool ThemeBackdrop => Level == VisualLevel.Full;
        /// <summary>The hex lattice on the window floor.</summary>
        public static bool HexFloor => Prefs.Value.HexFloor;
        /// <summary>Sparks rising through the backdrop.</summary>
        public static int Sparks => Level switch { VisualLevel.Full => 46, VisualLevel.Balanced => 16, _ => 0 };
        /// <summary>Nothing moves (also: reduce motion, or Windows' animations off).</summary>
        public static bool Still => Level == VisualLevel.Static;

        /// <summary>Frames per second for a clock that asks for <paramref name="want"/> (0 = stand still).</summary>
        public static int Fps(Pace pace, int want)
        {
            if (pace != Pace.Feedback && (Paused || Cz.Still)) return 0;
            var l = Level;
            return pace switch
            {
                Pace.Ambient => l == VisualLevel.Full ? want : l == VisualLevel.Balanced ? Math.Max(1, want * 2 / 5) : 0,
                Pace.Pulse => l == VisualLevel.Full ? want : l == VisualLevel.Balanced ? Math.Max(1, want * 3 / 5) : Math.Min(want, 8),
                _ => l == VisualLevel.Full ? want : Math.Min(want, 30),
            };
        }

        public static void Set(bool remote, VisualLevel v)
        {
            Prefs.Save(p => { if (remote) p.VisualsRemote = Id(v); else p.VisualsLocal = Id(v); });
            Raise();
        }
        /// <summary>Something the level reads changed (reduce motion, hex floor): re-pace and redraw.</summary>
        public static void Poke() => Raise();
        public static void SetMinimized(bool on) { if (_minimized == on) return; _minimized = on; Raise(); }

        private static bool _hooked;
        /// <summary>Follow Remote Desktop connecting / disconnecting and the session locking (call once, on the UI thread).</summary>
        public static void Hook()
        {
            if (_hooked) return; _hooked = true;
            var ui = Application.Current?.Dispatcher;
            try
            {
                SystemEvents.SessionSwitch += (s, e) =>
                {
                    bool away = e.Reason == SessionSwitchReason.SessionLock || e.Reason == SessionSwitchReason.RemoteDisconnect || e.Reason == SessionSwitchReason.ConsoleDisconnect;
                    bool back = e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.RemoteConnect || e.Reason == SessionSwitchReason.ConsoleConnect || e.Reason == SessionSwitchReason.SessionLogon;
                    if (!away && !back) return;
                    ui?.BeginInvoke(new Action(() => { _away = away; Log.Write("visuals: session " + e.Reason + " (" + Id(Level) + (Remote ? ", remote" : "") + ")"); Raise(); }));
                };
            }
            catch (Exception ex) { Log.Write("visuals hook: " + ex.Message); }
        }

        private static VisualLevel _last = (VisualLevel)(-1);
        private static void Raise()
        {
            var l = Level;
            bool lookChanged = l != _last; _last = l;
            Changed?.Invoke();
            // glow and the backdrop choice live in the theme pass: re-run it when the level really moved
            if (lookChanged) ThemeService.I.Refresh();
        }
    }
}
