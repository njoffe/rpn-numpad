// RPN Numpad - turns the numeric keypad into an HP-style RPN calculator.
// Toggle with Ctrl+Alt+Numpad Enter. NumLock acts as the gold "f" shift key while active.
// Build: build.cmd (uses the C# compiler that ships with Windows / .NET Framework 4.x).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyTitle("RPN Numpad")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace RpnNumpad
{
    public enum PadKey { None, D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, Dot, Enter, Add, Sub, Mul, Div, Shift }

    // ------------------------------------------------------------------
    // HP-style 4-level stack calculator (X, Y, Z, T + LAST X)
    // ------------------------------------------------------------------
    public class Calculator
    {
        public double X, Y, Z, T, LastX;
        public string Entry;      // number being keyed in ("-12.5e-3"); null when not entering
        public string Error;
        public bool Radians;      // angle mode for trig (false = degrees)
        bool lift = true;         // stack-lift flag, as on HP calculators

        struct Snapshot { public double X, Y, Z, T, LastX; public string Entry; public bool Lift; }
        readonly List<Snapshot> history = new List<Snapshot>();

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public void Save()
        {
            Snapshot s;
            s.X = X; s.Y = Y; s.Z = Z; s.T = T; s.LastX = LastX; s.Entry = Entry; s.Lift = lift;
            history.Add(s);
            if (history.Count > 500) history.RemoveAt(0);
        }

        public bool Undo()
        {
            if (history.Count == 0) return false;
            Snapshot s = history[history.Count - 1];
            history.RemoveAt(history.Count - 1);
            X = s.X; Y = s.Y; Z = s.Z; T = s.T; LastX = s.LastX; Entry = s.Entry; lift = s.Lift;
            return true;
        }

        void Push() { T = Z; Z = Y; Y = X; }

        void BeginEntry()
        {
            if (Entry != null) return;
            if (lift) Push();
            Entry = "";
            X = 0;
        }

        void SyncEntry() { X = ParseEntry(Entry); }

        public void Finish()
        {
            if (Entry == null) return;
            SyncEntry();
            Entry = null;
        }

        static double ParseEntry(string e)
        {
            string s = e;
            while (s.Length > 0 && "e-.".IndexOf(s[s.Length - 1]) >= 0) s = s.Substring(0, s.Length - 1);
            double v;
            if (s.Length == 0 || !double.TryParse(s, NumberStyles.Float, Inv, out v)) return 0;
            return v;
        }

        static int CountDigits(string s)
        {
            int n = 0;
            foreach (char ch in s) if (ch >= '0' && ch <= '9') n++;
            return n;
        }

        // ---- number entry ----
        public void Digit(int d)
        {
            BeginEntry();
            int e = Entry.IndexOf('e');
            if (e >= 0)
            {
                if (Entry.Substring(e + 1).TrimStart('-').Length >= 2) return;   // exponent: 2 digits max
            }
            else
            {
                if (CountDigits(Entry) >= 15) return;                            // mantissa: 15 digits max
                if (Entry == "0") Entry = "";
                else if (Entry == "-0") Entry = "-";
            }
            Entry += (char)('0' + d);
            SyncEntry();
        }

        public void Dot()
        {
            BeginEntry();
            if (Entry.IndexOf('.') >= 0 || Entry.IndexOf('e') >= 0) return;
            if (Entry == "" || Entry == "-") Entry += "0";
            Entry += ".";
            SyncEntry();
        }

        public void Eex()
        {
            BeginEntry();
            if (Entry.IndexOf('e') >= 0) return;
            if (CountDigits(Entry) == 0) Entry = (Entry == "-" ? "-" : "") + "1";
            Entry += "e";
            SyncEntry();
        }

        public void Chs()
        {
            if (Entry != null)
            {
                int e = Entry.IndexOf('e');
                if (e >= 0)
                {
                    if (Entry.Length > e + 1 && Entry[e + 1] == '-') Entry = Entry.Remove(e + 1, 1);
                    else Entry = Entry.Insert(e + 1, "-");
                }
                else Entry = Entry.StartsWith("-") ? Entry.Substring(1) : "-" + Entry;
                SyncEntry();
            }
            else X = -X;
        }

        public void Backspace()
        {
            if (Entry == null) { ClearX(); return; }
            if (Entry.Length > 0) Entry = Entry.Substring(0, Entry.Length - 1);
            if (Entry == "" || Entry == "-") { Entry = null; X = 0; lift = false; }
            else SyncEntry();
        }

        // ---- stack operations ----
        public void Enter() { Finish(); Push(); lift = false; }
        public void ClearX() { Entry = null; X = 0; lift = false; }
        public void ClearAll() { Entry = null; X = Y = Z = T = 0; lift = false; }
        public void Swap() { Finish(); double t = X; X = Y; Y = t; lift = true; }
        public void RollDown() { Finish(); double x = X; X = Y; Y = Z; Z = T; T = x; lift = true; }
        public void RecallLastX() { Finish(); if (lift) Push(); X = LastX; lift = true; }

        public void Recall(double v) { Finish(); if (lift) Push(); X = v; lift = true; }

        // ---- scientific helpers ----
        double ToRad(double x) { return Radians ? x : (x % 360.0) * Math.PI / 180.0; }
        double FromRad(double r) { return Radians ? r : r * 180.0 / Math.PI; }
        static double Clean(double r) { return Math.Abs(r) < 1e-15 ? 0 : r; }   // sin(180°) = 0, not 1.2E-16

        public double Sin(double x) { return Clean(Math.Sin(ToRad(x))); }
        public double Cos(double x) { return Clean(Math.Cos(ToRad(x))); }
        public double Tan(double x)
        {
            if (!Radians && Math.Abs(x % 180.0) == 90.0) return double.NaN;     // tan(90°) undefined
            return Clean(Math.Tan(ToRad(x)));
        }
        public double Asin(double x) { return FromRad(Math.Asin(x)); }
        public double Acos(double x) { return FromRad(Math.Acos(x)); }
        public double Atan(double x) { return FromRad(Math.Atan(x)); }

        public static double Factorial(double x)
        {
            if (x < 0 || x != Math.Floor(x) || x > 170) return double.NaN;
            double r = 1;
            for (int i = 2; i <= (int)x; i++) r *= i;
            return r;
        }

        // x-th root of y; odd roots of negative numbers are allowed (³√-8 = -2)
        public static double Root(double y, double x)
        {
            if (y < 0 && x == Math.Floor(x) && Math.Abs(x % 2) == 1) return -Math.Pow(-y, 1.0 / x);
            return Math.Pow(y, 1.0 / x);
        }

        static bool Bad(double r) { return double.IsNaN(r) || double.IsInfinity(r); }

        public bool Binary(Func<double, double, double> f, string err)
        {
            Finish();
            double r = f(Y, X);
            if (Bad(r)) { Error = err; return false; }
            LastX = X; X = r; Y = Z; Z = T; lift = true;
            return true;
        }

        public bool Unary(Func<double, double> f, string err)
        {
            Finish();
            double r = f(X);
            if (Bad(r)) { Error = err; return false; }
            LastX = X; X = r; lift = true;
            return true;
        }

        // ---- formatting ----
        static double Round12(double v)
        {
            return double.Parse(v.ToString("G12", Inv), Inv);
        }

        public static string Format(double v)
        {
            if (double.IsNaN(v)) return "NaN";
            if (v == 0) return "0";
            double r = Round12(v);
            double a = Math.Abs(r);
            if (a >= 1e12 || a < 1e-6) return r.ToString("0.###########E+0", Inv);
            return r.ToString("#,##0.###########", Inv);
        }

        // Plain number for clipboard / typing, using the user's locale decimal separator.
        public static string Plain(double v)
        {
            return Round12(v).ToString("G12", CultureInfo.CurrentCulture);
        }

        public string EntryDisplay()
        {
            return Entry.Replace("e", " E") + "_";
        }

        // ---- persistence ----
        public string Serialize()
        {
            return string.Join(";", new[] { X, Y, Z, T, LastX }.Select(d => d.ToString("R", Inv)));
        }

        public void Deserialize(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            string[] p = s.Split(';');
            if (p.Length != 5) return;
            double[] v = new double[5];
            for (int i = 0; i < 5; i++)
                if (!double.TryParse(p[i], NumberStyles.Float, Inv, out v[i])) return;
            X = v[0]; Y = v[1]; Z = v[2]; T = v[3]; LastX = v[4];
        }
    }

    static class LinqLite
    {
        public static IEnumerable<string> Select(this double[] a, Func<double, string> f)
        {
            foreach (double d in a) yield return f(d);
        }
    }

    // ------------------------------------------------------------------
    // Win32 interop
    // ------------------------------------------------------------------
    static class Native
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public uint vkCode, scanCode, flags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr extra; }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool ReleaseCapture();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();

        public const uint KEYEVENTF_KEYUP = 2, KEYEVENTF_UNICODE = 4;

        public static bool IsDown(int vk) { return (GetAsyncKeyState(vk) & 0x8000) != 0; }

        static INPUT Key(ushort vk, ushort scan, uint flags)
        {
            INPUT i = new INPUT();
            i.type = 1; // INPUT_KEYBOARD
            i.u.ki.wVk = vk; i.u.ki.wScan = scan; i.u.ki.dwFlags = flags;
            return i;
        }

        public static void TapVk(ushort vk)
        {
            INPUT[] a = { Key(vk, 0, 0), Key(vk, 0, KEYEVENTF_KEYUP) };
            SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void TypeText(string s)
        {
            var list = new List<INPUT>();
            foreach (char c in s)
            {
                list.Add(Key(0, c, KEYEVENTF_UNICODE));
                list.Add(Key(0, c, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP));
            }
            INPUT[] a = list.ToArray();
            SendInput((uint)a.Length, a, Marshal.SizeOf(typeof(INPUT)));
        }
    }

    // ------------------------------------------------------------------
    // Application: tray icon + keyboard hook
    // ------------------------------------------------------------------
    class App : ApplicationContext
    {
        public readonly Calculator Calc = new Calculator();
        public readonly float Scale;
        public bool Enabled { get; private set; }
        public int Level { get; private set; }            // 0 = normal, 1 = f (gold), 2 = g (blue)
        public bool Shift { get { return Level == 1; } }
        public bool GShift { get { return Level == 2; } }
        public string Note;
        public readonly List<string> Tape = new List<string>();
        bool tapeNumOpen;   // last tape entry is a number still being keyed in

        readonly Overlay overlay;
        readonly NotifyIcon tray;
        readonly Native.LowLevelKeyboardProc hookProc;
        readonly IntPtr hook;
        readonly HashSet<int> swallowed = new HashSet<int>();
        readonly Icon iconOn, iconOff;
        readonly ToolStripMenuItem miEnable, miStartup;

        const string RegKey = @"Software\RpnNumpad";
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "RpnNumpad";

        public const string KeyMap =
            "TOGGLE RPN MODE:   Ctrl + Alt + Numpad Enter\r\n\r\n" +
            "While RPN mode is on, the numpad is the calculator:\r\n" +
            "  0-9  .         key in a number\r\n" +
            "  Enter          ENTER (push X up the stack)\r\n" +
            "  +  -  *  /     arithmetic on Y and X\r\n\r\n" +
            "NumLock once = gold f shift; then press:\r\n" +
            "  f 7  √x        f 8  x²        f 9  yˣ        f /  CLEAR stack + tape\r\n" +
            "  f 4  1/x       f 5  %         f 6  LAST X    f *  COPY X\r\n" +
            "  f 1  R↓        f 2  x⇄y       f 3  UNDO      f -  ← (backspace)\r\n" +
            "  f 0  CHS       f .  EEX       f Enter  TYPE X into the active app\r\n" +
            "  f +  CLx\r\n\r\n" +
            "NumLock twice = blue g shift; then press:\r\n" +
            "  g 7  SIN       g 8  COS       g 9  TAN       g /  DEG/RAD\r\n" +
            "  g 4  ASIN      g 5  ACOS      g 6  ATAN      g *  π\r\n" +
            "  g 1  LN        g 2  LOG       g 3  eˣ        g -  10ˣ\r\n" +
            "  g 0  n!        g .  |x|       g Enter  INT   g +  ʸ√x (x-th root of y)\r\n\r\n" +
            "NumLock a third time cancels the shift.\r\n\r\n" +
            "Ctrl/Alt/Win + numpad keys pass through untouched.\r\n" +
            "Drag the display to move it. Right-click it or the tray icon for options.";

        public App()
        {
            Scale = GetScale();
            LoadState();

            iconOn = MakeIcon(Color.FromArgb(242, 179, 61));
            iconOff = MakeIcon(Color.FromArgb(110, 115, 130));

            miEnable = new ToolStripMenuItem("RPN mode on\tCtrl+Alt+Num Enter", null, delegate { Toggle(); });
            miStartup = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            miStartup.Checked = IsStartupEnabled();
            if (string.IsNullOrEmpty(System.Reflection.Assembly.GetExecutingAssembly().Location))
            {
                // Loaded into PowerShell from memory (Smart App Control workaround): there is no exe path
                // to register, so auto-start must be done with a Startup-folder shortcut instead.
                miStartup.Enabled = false;
                miStartup.Text = "Start with Windows (use a Startup shortcut)";
            }
            var menu = new ContextMenuStrip();
            menu.Items.Add(miEnable);
            menu.Items.Add("Key map…", null, delegate { ShowKeyMap(); });
            menu.Items.Add(miStartup);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });

            tray = new NotifyIcon();
            tray.Icon = iconOff;
            tray.Text = "RPN Numpad: off (Ctrl+Alt+Num Enter)";
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Toggle(); };
            tray.Visible = true;

            overlay = new Overlay(this, menu);

            hookProc = OnHook;
            hook = Native.SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, hookProc, Native.GetModuleHandle(null), 0);
            if (hook == IntPtr.Zero)
            {
                MessageBox.Show("Could not install the keyboard hook (error " + Marshal.GetLastWin32Error() + ").",
                                "RPN Numpad", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            tray.ShowBalloonTip(4000, "RPN Numpad is running",
                "Press Ctrl+Alt+Numpad Enter to turn the numpad into an RPN calculator.", ToolTipIcon.Info);
        }

        static float GetScale()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }

        static Icon MakeIcon(Color c)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    using (var b = new SolidBrush(c)) g.FillRectangle(b, 2, 2, 28, 28);
                    using (var f = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString("R", f, Brushes.Black, new RectangleF(0, 0, 32, 32), sf);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        // ---- keyboard hook ----
        IntPtr OnHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                if ((k.flags & 0x10) == 0)  // ignore injected input (including our own typing)
                {
                    bool handled = false;
                    try { handled = HandleKey(k); } catch (Exception) { }
                    if (handled) return (IntPtr)1;
                }
            }
            return Native.CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // Identify numpad keys by scan code + extended flag so it works whether NumLock is on or off.
        static PadKey Map(uint vk, uint sc, bool ext)
        {
            if (vk == 0x90) return PadKey.Shift;  // VK_NUMLOCK
            if (ext)
            {
                if (sc == 0x35) return PadKey.Div;
                if (sc == 0x1C) return PadKey.Enter;
                return PadKey.None;
            }
            switch (sc)
            {
                case 0x52: return PadKey.D0;
                case 0x4F: return PadKey.D1;
                case 0x50: return PadKey.D2;
                case 0x51: return PadKey.D3;
                case 0x4B: return PadKey.D4;
                case 0x4C: return PadKey.D5;
                case 0x4D: return PadKey.D6;
                case 0x47: return PadKey.D7;
                case 0x48: return PadKey.D8;
                case 0x49: return PadKey.D9;
                case 0x53: return PadKey.Dot;
                case 0x4E: return PadKey.Add;
                case 0x4A: return PadKey.Sub;
                case 0x37: return vk == 0x6A ? PadKey.Mul : PadKey.None;
            }
            return PadKey.None;
        }

        bool HandleKey(Native.KBDLLHOOKSTRUCT k)
        {
            bool ext = (k.flags & 0x01) != 0;
            bool up = (k.flags & 0x80) != 0;
            PadKey key = Map(k.vkCode, k.scanCode, ext);
            if (key == PadKey.None) return false;

            int id = key == PadKey.Shift ? 0x10000 : (int)k.scanCode | (ext ? 0x100 : 0);
            if (up) return swallowed.Remove(id);

            bool ctrl = Native.IsDown(0x11), alt = Native.IsDown(0x12);
            bool win = Native.IsDown(0x5B) || Native.IsDown(0x5C);

            if (key == PadKey.Enter && ctrl && alt && !win)
            {
                swallowed.Add(id);
                Native.TapVk(0xE8);   // unassigned key: stops the Alt release from opening the app's menu bar
                overlay.BeginInvoke(new Action(Toggle));
                return true;
            }

            if (!Enabled || ctrl || alt || win) return false;

            swallowed.Add(id);
            Process(key);
            return true;
        }

        // ---- calculator dispatch ----
        void Process(PadKey key)
        {
            Calc.Error = null;
            Note = null;
            overlay.Flash(key);

            // NumLock cycles: normal -> f (gold) -> g (blue) -> normal
            if (key == PadKey.Shift) { Level = (Level + 1) % 3; overlay.UpdateView(); return; }

            int lvl = Level;
            bool f = lvl == 1;
            RecordTape(key, lvl);
            Level = 0;

            bool noStateChange = (f && (key == PadKey.D3 || key == PadKey.Mul || key == PadKey.Enter)) ||
                                 (lvl == 2 && key == PadKey.Div);
            if (!noStateChange) Calc.Save();

            if (lvl == 2) { ProcessBlue(key); overlay.UpdateView(); return; }

            if (!f)
            {
                switch (key)
                {
                    case PadKey.Dot: Calc.Dot(); break;
                    case PadKey.Enter: Calc.Enter(); break;
                    case PadKey.Add: Calc.Binary((y, x) => y + x, "Overflow"); break;
                    case PadKey.Sub: Calc.Binary((y, x) => y - x, "Overflow"); break;
                    case PadKey.Mul: Calc.Binary((y, x) => y * x, "Overflow"); break;
                    case PadKey.Div: Calc.Binary((y, x) => y / x, "Divide by zero"); break;
                    default: Calc.Digit(key - PadKey.D0); break;
                }
            }
            else
            {
                switch (key)
                {
                    case PadKey.D7: Calc.Unary(Math.Sqrt, "√ of negative"); break;
                    case PadKey.D8: Calc.Unary(x => x * x, "Overflow"); break;
                    case PadKey.D9: Calc.Binary(Math.Pow, "Invalid yˣ"); break;
                    case PadKey.Div:
                        Calc.ClearAll();
                        Tape.Clear();
                        tapeNumOpen = false;
                        Note = "Stack and tape cleared";
                        break;
                    case PadKey.D4: Calc.Unary(x => 1 / x, "Divide by zero"); break;
                    case PadKey.D5: { double y = Calc.Y; Calc.Unary(x => y * x / 100, "Overflow"); break; }
                    case PadKey.D6: Calc.RecallLastX(); break;
                    case PadKey.Mul: CopyX(); break;
                    case PadKey.D1: Calc.RollDown(); break;
                    case PadKey.D2: Calc.Swap(); break;
                    case PadKey.D3: Note = Calc.Undo() ? "Undone" : "Nothing to undo"; break;
                    case PadKey.Sub: Calc.Backspace(); break;
                    case PadKey.D0: Calc.Chs(); break;
                    case PadKey.Dot: Calc.Eex(); break;
                    case PadKey.Enter: TypeX(); break;
                    case PadKey.Add: Calc.ClearX(); break;
                }
            }
            overlay.UpdateView();
        }

        // Blue (g) functions: trigonometry, logarithms and friends.
        void ProcessBlue(PadKey key)
        {
            Calculator c = Calc;
            switch (key)
            {
                case PadKey.D7: c.Unary(c.Sin, "Invalid sin"); break;
                case PadKey.D8: c.Unary(c.Cos, "Invalid cos"); break;
                case PadKey.D9: c.Unary(c.Tan, "tan undefined"); break;
                case PadKey.D4: c.Unary(c.Asin, "asin needs −1…1"); break;
                case PadKey.D5: c.Unary(c.Acos, "acos needs −1…1"); break;
                case PadKey.D6: c.Unary(c.Atan, "Invalid atan"); break;
                case PadKey.D1: c.Unary(Math.Log, "ln needs x > 0"); break;
                case PadKey.D2: c.Unary(Math.Log10, "log needs x > 0"); break;
                case PadKey.D3: c.Unary(Math.Exp, "Overflow"); break;
                case PadKey.Sub: c.Unary(x => Math.Pow(10, x), "Overflow"); break;
                case PadKey.D0: c.Unary(Calculator.Factorial, "n! needs integer 0…170"); break;
                case PadKey.Dot: c.Unary(Math.Abs, "Invalid"); break;
                case PadKey.Enter: c.Unary(Math.Truncate, "Invalid"); break;
                case PadKey.Add: c.Binary(Calculator.Root, "Invalid root"); break;
                case PadKey.Mul: c.Recall(Math.PI); break;
                case PadKey.Div:
                    c.Radians = !c.Radians;
                    Note = c.Radians ? "Angles in radians" : "Angles in degrees";
                    break;
            }
        }

        // Keep a readable history: digits of one number are grouped ("12.5"), operations are separate.
        void RecordTape(PadKey key, int lvl)
        {
            bool f = lvl == 1;
            bool numKey = lvl == 0 && (key <= PadKey.D9 && key >= PadKey.D0 || key == PadKey.Dot);
            bool entryEdit = f && (key == PadKey.Dot || key == PadKey.D0 || key == PadKey.Sub);
            bool open = tapeNumOpen && Calc.Entry != null && Tape.Count > 0;

            if (numKey)
            {
                string ch = key == PadKey.Dot ? "." : ((int)(key - PadKey.D0)).ToString();
                if (open) Tape[Tape.Count - 1] += ch; else Tape.Add(ch);
                tapeNumOpen = true;
            }
            else if (entryEdit && open)
            {
                string t = Tape[Tape.Count - 1];
                if (key == PadKey.Dot) t += "E";
                else if (key == PadKey.D0) t = t.StartsWith("−") ? t.Substring(1) : "−" + t;
                else if (t.Length > 0) t = t.Substring(0, t.Length - 1);
                if (t.Length == 0 || t == "−") { Tape.RemoveAt(Tape.Count - 1); tapeNumOpen = false; }
                else Tape[Tape.Count - 1] = t;
            }
            else
            {
                Tape.Add(Overlay.Label(key, lvl));
                tapeNumOpen = false;
            }
            if (Tape.Count > 60) Tape.RemoveAt(0);
        }

        void CopyX()
        {
            Calc.Finish();
            string s = Calculator.Plain(Calc.X);
            overlay.BeginInvoke(new Action(delegate
            {
                try { Clipboard.SetText(s); Note = "Copied " + s; }
                catch (Exception) { Calc.Error = "Clipboard busy"; }
                overlay.UpdateView();
            }));
        }

        void TypeX()
        {
            Calc.Finish();
            string s = Calculator.Plain(Calc.X);
            Note = "Typed " + s;
            overlay.BeginInvoke(new Action(delegate { Native.TypeText(s); }));
        }

        // ---- mode / settings ----
        public void Toggle()
        {
            Enabled = !Enabled;
            Level = 0;
            Note = null;
            Calc.Error = null;
            miEnable.Checked = Enabled;
            tray.Icon = Enabled ? iconOn : iconOff;
            tray.Text = Enabled ? "RPN Numpad: ON (Ctrl+Alt+Num Enter)" : "RPN Numpad: off (Ctrl+Alt+Num Enter)";
            if (Enabled) overlay.ShowOverlay(); else { overlay.Hide(); SaveState(); }
        }

        public void ShowKeyMap()
        {
            MessageBox.Show(KeyMap, "RPN Numpad – key map", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        static bool IsStartupEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        void ToggleStartup()
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (IsStartupEnabled()) k.DeleteValue(RunName, false);
                else k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
            }
            miStartup.Checked = IsStartupEnabled();
        }

        public Point? SavedLocation;

        void LoadState()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    if (k == null) return;
                    Calc.Deserialize(k.GetValue("Stack") as string);
                    Calc.Radians = (k.GetValue("Radians") as int?) == 1;
                    object l = k.GetValue("Left"), b = k.GetValue("Bottom");
                    if (l is int && b is int) SavedLocation = new Point((int)l, (int)b);
                }
            }
            catch (Exception) { }
        }

        // Called on toggle-off, Exit, Windows shutdown/restart/sign-out and sleep.
        // X already holds the value of a number being typed, so no need to end entry here.
        public void SaveState()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegKey))
                {
                    k.SetValue("Stack", Calc.Serialize());
                    k.SetValue("Radians", Calc.Radians ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("Left", overlay.AnchorLeft, RegistryValueKind.DWord);
                    k.SetValue("Bottom", overlay.AnchorBottom, RegistryValueKind.DWord);
                }
            }
            catch (Exception) { }
        }

        protected override void ExitThreadCore()
        {
            SaveState();
            if (hook != IntPtr.Zero) Native.UnhookWindowsHookEx(hook);
            tray.Visible = false;
            tray.Dispose();
            overlay.Close();
            base.ExitThreadCore();
        }
    }

    // ------------------------------------------------------------------
    // Always-on-top display that never steals focus
    // ------------------------------------------------------------------
    class Overlay : Form
    {
        readonly App app;
        readonly ContextMenuStrip menu;
        public int AnchorLeft, AnchorBottom;
        bool positioning;

        static readonly Color Bg = Color.FromArgb(30, 31, 36);
        static readonly Color Edge = Color.FromArgb(60, 63, 72);
        static readonly Color Cell = Color.FromArgb(44, 46, 54);
        static readonly Color Dim = Color.FromArgb(110, 115, 130);
        static readonly Color Mid = Color.FromArgb(200, 204, 212);
        static readonly Color Bright = Color.FromArgb(245, 246, 248);
        static readonly Color Gold = Color.FromArgb(242, 179, 61);
        static readonly Color Red = Color.FromArgb(255, 107, 107);
        static readonly Color Green = Color.FromArgb(123, 216, 143);

        readonly Font fSmall = new Font("Segoe UI", 8.5f);
        readonly Font fSmallBold = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        readonly Font fTiny = new Font("Segoe UI", 7f);
        readonly Font fCell = new Font("Segoe UI", 9f, FontStyle.Bold);
        readonly Font fMono = new Font("Consolas", 11f);

        int S(float v) { return (int)Math.Round(v * app.Scale); }

        const int StackH = 156, KeyH = 44, KeyGap = 4, PadH = 5 * (KeyH + KeyGap) + 8, Width0 = 300;
        static readonly Color Blue = Color.FromArgb(105, 175, 255);

        PadKey flash = PadKey.None;
        readonly System.Windows.Forms.Timer flashTimer = new System.Windows.Forms.Timer();

        // Light up a key on the on-screen keypad briefly.
        public void Flash(PadKey k)
        {
            flash = k;
            flashTimer.Stop();
            flashTimer.Start();
            Invalidate();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 /* NOACTIVATE */ | 0x80 /* TOOLWINDOW */ | 0x8 /* TOPMOST */;
                return cp;
            }
        }

        public Overlay(App app, ContextMenuStrip menu)
        {
            this.app = app;
            this.menu = menu;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Bg;
            Text = "RPN Numpad";
            Size = new Size(S(Width0), S(StackH) + S(PadH));
            flashTimer.Interval = 180;
            flashTimer.Tick += delegate { flashTimer.Stop(); flash = PadKey.None; Invalidate(); };

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            AnchorLeft = wa.Right - Width - S(16);
            AnchorBottom = wa.Bottom - S(16);
            if (app.SavedLocation.HasValue)
            {
                Point p = app.SavedLocation.Value;
                foreach (Screen sc in Screen.AllScreens)
                    if (sc.WorkingArea.Contains(new Point(p.X + 20, p.Y - 20))) { AnchorLeft = p.X; AnchorBottom = p.Y; break; }
            }
            CreateHandle();
        }

        public void ShowOverlay()
        {
            UpdateView();
            Show();
        }

        public void UpdateView()
        {
            if (InvokeRequired) { BeginInvoke(new Action(UpdateView)); return; }
            int h = S(StackH) + S(PadH);
            positioning = true;
            SetBounds(AnchorLeft, AnchorBottom - h, S(Width0), h);
            positioning = false;
            Invalidate();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (!positioning && Visible) { AnchorLeft = Left; AnchorBottom = Bottom; }
        }

        // This window exists (hidden) for the app's whole life, so it receives the shutdown and
        // power broadcasts that Windows sends to top-level windows. Save before they take effect.
        protected override void WndProc(ref Message m)
        {
            const int WM_QUERYENDSESSION = 0x11, WM_ENDSESSION = 0x16, WM_POWERBROADCAST = 0x218, PBT_APMSUSPEND = 4;
            if (m.Msg == WM_QUERYENDSESSION || (m.Msg == WM_ENDSESSION && m.WParam != IntPtr.Zero) ||
                (m.Msg == WM_POWERBROADCAST && m.WParam == (IntPtr)PBT_APMSUSPEND))
                app.SaveState();
            base.WndProc(ref m);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                Native.ReleaseCapture();
                Native.SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
            }
            else if (e.Button == MouseButtons.Right) menu.Show(this, e.Location);
        }

        const TextFormatFlags RightMid = TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                                         TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        const TextFormatFlags LeftMid = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                       TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Calculator c = app.Calc;
            int W = ClientSize.Width, H = ClientSize.Height, pad = S(12);

            using (var p = new Pen(Edge)) g.DrawRectangle(p, 0, 0, W - 1, H - 1);

            // header
            var head = new Rectangle(pad, S(6), W - 2 * pad, S(20));
            TextRenderer.DrawText(g, "RPN", fSmallBold, head, Gold, LeftMid);
            TextRenderer.DrawText(g, c.Radians ? "RAD" : "DEG", fSmall,
                                  new Rectangle(pad + S(32), head.Y, S(30), head.Height), Mid, LeftMid);
            if (app.Shift || app.GShift)
            {
                var badge = new Rectangle(pad + S(64), head.Y + S(2), S(18), S(16));
                using (var b = new SolidBrush(app.Shift ? Gold : Blue)) g.FillRectangle(b, badge);
                TextRenderer.DrawText(g, app.Shift ? "f" : "g", fSmallBold, badge, Bg, Center);
            }
            string msg = c.Error ?? app.Note;
            if (msg != null) TextRenderer.DrawText(g, msg, fSmall, head, c.Error != null ? Red : Green, RightMid);
            else TextRenderer.DrawText(g, app.Shift ? "gold function?" : app.GShift ? "blue function?" : "NumLock = f, twice = g",
                                       fSmall, head, Dim, RightMid);

            // tape: the most recent keys pressed, newest on the right
            var tapeR = new Rectangle(pad, S(27), W - 2 * pad, S(18));
            using (var b = new SolidBrush(Cell)) g.FillRectangle(b, tapeR.X - S(4), tapeR.Y, tapeR.Width + S(8), tapeR.Height);
            string tape = "";
            for (int i = app.Tape.Count - 1; i >= 0; i--)
            {
                string t = app.Tape[i] + (tape.Length > 0 ? "  " + tape : "");
                if (TextRenderer.MeasureText(g, t, fSmall, Size.Empty, TextFormatFlags.NoPadding).Width > tapeR.Width - S(8)) break;
                tape = t;
            }
            TextRenderer.DrawText(g, tape.Length > 0 ? tape : "keys you press appear here", fSmall, tapeR,
                                  tape.Length > 0 ? Mid : Dim, RightMid);

            // T Z Y
            int y = S(50), rowH = S(20);
            string[] names = { "T", "Z", "Y" };
            double[] vals = { c.T, c.Z, c.Y };
            for (int i = 0; i < 3; i++)
            {
                var r = new Rectangle(pad, y, W - 2 * pad, rowH);
                TextRenderer.DrawText(g, names[i], fSmall, r, Dim, LeftMid);
                TextRenderer.DrawText(g, Calculator.Format(vals[i]), fMono, r, Mid, RightMid);
                y += rowH;
            }

            using (var p = new Pen(Edge)) g.DrawLine(p, pad, y + S(3), W - pad, y + S(3));
            y += S(6);

            // X (shrink font until it fits)
            var xr = new Rectangle(pad, y, W - 2 * pad, S(40));
            string xs = c.Entry != null ? c.EntryDisplay() : Calculator.Format(c.X);
            TextRenderer.DrawText(g, "X", fSmallBold, xr, Gold, LeftMid);
            int avail = xr.Width - S(22);
            for (float size = 20f; size >= 8f; size -= 1f)
            {
                using (var f = new Font("Consolas", size, FontStyle.Bold))
                {
                    if (size > 8f && TextRenderer.MeasureText(g, xs, f, Size.Empty, TextFormatFlags.NoPadding).Width > avail) continue;
                    TextRenderer.DrawText(g, xs, f, xr, Bright, RightMid);
                    break;
                }
            }

            DrawPad(g, S(StackH), W, pad);
        }

        // On-screen keypad laid out like the physical numpad:
        // col, row, colspan, rowspan, key, label, gold f-function, blue g-function.
        class PadCell
        {
            public int Col, Row, Cs, Rs; public PadKey Key; public string Label, Gold, Blue;
            public PadCell(int c, int r, int cs, int rs, PadKey k, string l, string f, string g)
            { Col = c; Row = r; Cs = cs; Rs = rs; Key = k; Label = l; Gold = f; Blue = g; }
        }

        static readonly PadCell[] Cells =
        {
            new PadCell(0, 0, 1, 1, PadKey.Shift, "f",     "→ g",   "cancel"),
            new PadCell(1, 0, 1, 1, PadKey.Div,   "÷",     "CLEAR", "DEG/RAD"),
            new PadCell(2, 0, 1, 1, PadKey.Mul,   "×",     "COPY",  "π"),
            new PadCell(3, 0, 1, 1, PadKey.Sub,   "−",     "←",     "10ˣ"),
            new PadCell(0, 1, 1, 1, PadKey.D7,    "7",     "√x",    "SIN"),
            new PadCell(1, 1, 1, 1, PadKey.D8,    "8",     "x²",    "COS"),
            new PadCell(2, 1, 1, 1, PadKey.D9,    "9",     "yˣ",    "TAN"),
            new PadCell(3, 1, 1, 2, PadKey.Add,   "+",     "CLx",   "ʸ√x"),
            new PadCell(0, 2, 1, 1, PadKey.D4,    "4",     "1/x",   "ASIN"),
            new PadCell(1, 2, 1, 1, PadKey.D5,    "5",     "%",     "ACOS"),
            new PadCell(2, 2, 1, 1, PadKey.D6,    "6",     "LASTx", "ATAN"),
            new PadCell(0, 3, 1, 1, PadKey.D1,    "1",     "R↓",    "LN"),
            new PadCell(1, 3, 1, 1, PadKey.D2,    "2",     "x⇄y",   "LOG"),
            new PadCell(2, 3, 1, 1, PadKey.D3,    "3",     "UNDO",  "eˣ"),
            new PadCell(3, 3, 1, 2, PadKey.Enter, "ENTER", "TYPE",  "INT"),
            new PadCell(0, 4, 2, 1, PadKey.D0,    "0",     "CHS",   "n!"),
            new PadCell(2, 4, 1, 1, PadKey.Dot,   ".",     "EEX",   "|x|"),
        };

        // Name of a key for the tape (lvl 0 = plain, 1 = gold, 2 = blue).
        public static string Label(PadKey k, int lvl)
        {
            foreach (PadCell c in Cells) if (c.Key == k) return lvl == 1 ? c.Gold : lvl == 2 ? c.Blue : c.Label;
            return "?";
        }

        static readonly Color KeyFace = Color.FromArgb(52, 55, 64);
        static readonly Color KeyLit = Color.FromArgb(150, 156, 172);
        readonly Font fKey = new Font("Segoe UI", 11f, FontStyle.Bold);

        const TextFormatFlags TopCenter = TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPadding;
        const TextFormatFlags BottomCenter = TextFormatFlags.HorizontalCenter | TextFormatFlags.Bottom | TextFormatFlags.NoPadding;

        void DrawPad(Graphics g, int top, int W, int pad)
        {
            int gap = S(KeyGap), ch = S(KeyH);
            int cw = (W - 2 * pad - 3 * gap) / 4;
            int lvl = app.Level;
            foreach (PadCell c in Cells)
            {
                var r = new Rectangle(pad + c.Col * (cw + gap), top + c.Row * (ch + gap),
                                      c.Cs * cw + (c.Cs - 1) * gap, c.Rs * ch + (c.Rs - 1) * gap);
                bool lit = c.Key == flash;

                if (c.Key == PadKey.Shift)
                {
                    // The NumLock key shows what pressing it next will do.
                    Color face = lit ? KeyLit : lvl == 1 ? Gold : lvl == 2 ? Blue : Color.FromArgb(120, 90, 30);
                    using (var b = new SolidBrush(face)) g.FillRectangle(b, r);
                    string main = lvl == 0 ? "f" : lvl == 1 ? "→ g" : "cancel";
                    TextRenderer.DrawText(g, main, lvl == 0 ? fKey : fCell, new Rectangle(r.X, r.Y, r.Width, r.Height - S(8)),
                                          lvl == 0 ? Gold : Bg, Center);
                    TextRenderer.DrawText(g, "NumLock", fTiny, new Rectangle(r.X, r.Y, r.Width, r.Height - S(3)),
                                          lvl == 0 ? Dim : Bg, BottomCenter);
                    continue;
                }

                using (var b = new SolidBrush(lit ? KeyLit : KeyFace)) g.FillRectangle(b, r);

                if (lvl == 0)
                {
                    // HP style: gold function above, key label in the middle, blue function below
                    TextRenderer.DrawText(g, c.Gold, fTiny, new Rectangle(r.X, r.Y + S(2), r.Width, S(12)), Gold, TopCenter);
                    TextRenderer.DrawText(g, c.Label, c.Label.Length > 2 ? fCell : fKey, r, Bright, Center);
                    TextRenderer.DrawText(g, c.Blue, fTiny, new Rectangle(r.X, r.Y, r.Width, r.Height - S(2)), Blue, BottomCenter);
                }
                else
                {
                    // shifted: show only the function the key will perform
                    TextRenderer.DrawText(g, lvl == 1 ? c.Gold : c.Blue, fCell, r, lit ? Bright : (lvl == 1 ? Gold : Blue), Center);
                }
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "RpnNumpad_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("RPN Numpad is already running (see the system tray).", "RPN Numpad");
                    return;
                }
                try { Native.SetProcessDPIAware(); } catch (Exception) { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new App());
            }
        }
    }
}
