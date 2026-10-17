// ProClicker 2 - advanced auto clicker for Windows 11
// Build: run build.bat (uses the C# compiler built into Windows, nothing to install).
// Keep this file ASCII-only: write symbols as \uXXXX escapes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("ProClicker")]
[assembly: AssemblyProduct("ProClicker")]
[assembly: AssemblyDescription("Advanced auto clicker")]
[assembly: AssemblyVersion(ProClicker.AppInfo.Version)]
[assembly: AssemblyFileVersion(ProClicker.AppInfo.Version)]

namespace ProClicker
{
    static class AppInfo
    {
        // Bump this before every release. The GitHub release tag must match it (e.g. v1.0.1).
        public const string Version = "1.0.0";
    }
    // ================================================================== Win32
    static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT { public int dx, dy, mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT { public ushort wVk, wScan; public int dwFlags, time; public IntPtr dwExtraInfo; }
        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT { public int type; public InputUnion u; }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT { public int x, y, mouseData, flags, time; public IntPtr extra; }
        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT { public int vkCode, scanCode, flags, time; public IntPtr extra; }

        public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int idHook, HookProc fn, IntPtr hMod, uint threadId);
        [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint mapType);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] public static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);

        static void Send(INPUT i) { SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT))); }

        static void MouseFlag(int flags, int data)
        {
            INPUT i = new INPUT(); i.type = 0; i.u.mi.dwFlags = flags; i.u.mi.mouseData = data; Send(i);
        }

        // button: 0 left, 1 right, 2 middle
        public static void MouseButton(int button, bool down)
        {
            int f = button == 0 ? (down ? 0x02 : 0x04) : button == 1 ? (down ? 0x08 : 0x10) : (down ? 0x20 : 0x40);
            MouseFlag(f, 0);
        }

        public static void Wheel(int delta) { MouseFlag(0x800, delta); }

        static bool IsExtended(int vk)
        {
            return (vk >= 0x21 && vk <= 0x2E) || vk == 0xA3 || vk == 0xA5 || vk == 0x90 || vk == 0x6F || vk == 0x5B || vk == 0x5C;
        }

        public static void Key(int vk, bool down)
        {
            INPUT i = new INPUT(); i.type = 1;
            i.u.ki.wVk = (ushort)vk;
            i.u.ki.wScan = (ushort)MapVirtualKey((uint)vk, 0);
            i.u.ki.dwFlags = (down ? 0 : 2) | (IsExtended(vk) ? 1 : 0);
            Send(i);
        }
    }

    // ================================================================== Themes
    class Theme
    {
        public string Name; public bool Dark, AllowSystemAccent;
        public Color Back, Layer, Card, CardStroke, Ctl, Stroke, Input, Text, Sub, Accent, AccentText, Danger;

        public Theme(string name, bool dark, bool sys, int back, int layer, int card, int cardStroke, int ctl, int stroke,
                     int input, int text, int sub, int accent, int accentText, int danger)
        {
            Name = name; Dark = dark; AllowSystemAccent = sys;
            Back = C(back); Layer = C(layer); Card = C(card); CardStroke = C(cardStroke); Ctl = C(ctl); Stroke = C(stroke);
            Input = C(input); Text = C(text); Sub = C(sub); Accent = C(accent); AccentText = C(accentText); Danger = C(danger);
        }
        static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

        public Theme WithAccent(Color accent)
        {
            Theme t = (Theme)MemberwiseClone(); t.Accent = accent; t.AccentText = Dark ? Color.Black : Color.White; return t;
        }

        public static readonly Theme[] All = {
            //          name          dark   sysAcc  back      layer     card      cardStr   ctl       stroke    input     text      sub       accent    accTxt    danger
            new Theme("Light",      false, true,  0xF3F3F3, 0xF9F9F9, 0xFFFFFF, 0xE5E5E5, 0xFBFBFB, 0xD9D9D9, 0xFFFFFF, 0x1B1B1B, 0x616161, 0x005FB8, 0xFFFFFF, 0xC42B1C),
            new Theme("Dark",       true,  true,  0x202020, 0x282828, 0x2F2F2F, 0x1C1C1C, 0x383838, 0x444444, 0x232323, 0xFFFFFF, 0xABABAB, 0x4CC2FF, 0x000000, 0xFF6B5E),
            new Theme("Extra Dark", true,  true,  0x000000, 0x0A0A0A, 0x131313, 0x1F1F1F, 0x1C1C1C, 0x2E2E2E, 0x080808, 0xF5F5F5, 0x9A9A9A, 0xA493FF, 0x000000, 0xFF5C6C),
            new Theme("Ocean",      true,  false, 0x0E1A26, 0x13212F, 0x192B3C, 0x0B1520, 0x213649, 0x2C4660, 0x0F1D2A, 0xE6F1FB, 0x93A9BF, 0x3DD6E8, 0x03151C, 0xFF6B6B),
            new Theme("Neon",       true,  false, 0x0A0A12, 0x0F0F1A, 0x161626, 0x08080F, 0x1E1E33, 0x2E2C4C, 0x0B0B14, 0xF0EEFF, 0x9C99C2, 0x00F5A0, 0x00140C, 0xFF2E88),
            new Theme("Sakura",     false, false, 0xFBEFF3, 0xFFF7FA, 0xFFFFFF, 0xF3DDE5, 0xFFF9FB, 0xEBCBD7, 0xFFFFFF, 0x34202B, 0x87677A, 0xD63F78, 0xFFFFFF, 0xC62828),
        };
    }

    // ================================================================== Drawing helpers
    static class Ui
    {
        public static Theme T = Theme.All[1];
        public static float Scale = 1f;
        public static string TextFont = "Segoe UI", SemiFont = "Segoe UI Semibold", DisplayFont = "Segoe UI Semibold", IconFont = "Segoe MDL2 Assets";
        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();

        public static void Init()
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) Scale = g.DpiX / 96f;
            List<string> names = new List<string>();
            foreach (FontFamily f in new InstalledFontCollection().Families) names.Add(f.Name);
            Func<string, string> find = delegate (string prefix) { foreach (string n in names) if (n.StartsWith(prefix)) return n; return null; };
            IconFont = find("Segoe Fluent Icons") ?? "Segoe MDL2 Assets";
            TextFont = find("Segoe UI Variable Text") ?? "Segoe UI";
            SemiFont = find("Segoe UI Variable Text Semib") ?? "Segoe UI Semibold";
            DisplayFont = find("Segoe UI Variable Display Semib") ?? SemiFont;
        }

        public static int S(float v) { return (int)Math.Round(v * Scale); }

        // weight: 0 regular, 1 semibold, 2 display semibold, 3 icon
        public static Font F(float size, int weight)
        {
            string key = size + "|" + weight; Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            string fam = weight == 0 ? TextFont : weight == 1 ? SemiFont : weight == 2 ? DisplayFont : IconFont;
            f = new Font(fam, size, GraphicsUnit.Point); fonts[key] = f; return f;
        }
        public static Font Icon(float size) { return F(size, 3); }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t <= 0) return a; if (t >= 1) return b;
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static void Prep(Graphics g) { g.SmoothingMode = SmoothingMode.AntiAlias; }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 1) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure(); return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float rad, Color fill, Color border)
        {
            using (GraphicsPath p = Round(r, rad))
            {
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                if (border != Color.Empty) using (Pen pen = new Pen(border)) g.DrawPath(pen, p);
            }
        }

        public static RectangleF Box(Control c) { return new RectangleF(0.5f, 0.5f, c.Width - 1.5f, c.Height - 1.5f); }

        const TextFormatFlags Base = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        public static void Text(Graphics g, string s, Font f, Rectangle r, Color c, TextFormatFlags extra)
        {
            TextRenderer.DrawText(g, s, f, r, c, Base | extra);
        }
        public static void TextLeft(Graphics g, string s, Font f, Rectangle r, Color c) { Text(g, s, f, r, c, TextFormatFlags.VerticalCenter | TextFormatFlags.Left); }
        public static void TextCenter(Graphics g, string s, Font f, Rectangle r, Color c) { Text(g, s, f, r, c, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter); }
        // measured without EndEllipsis: with an empty proposed size it under-reports width and breaks centering
        public static Size Measure(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        // Windows 11 title bar: dark mode, rounded corners, caption matching the window background
        public static void StyleTitleBar(Form f)
        {
            int v;
            v = T.Dark ? 1 : 0; Native.DwmSetWindowAttribute(f.Handle, 20, ref v, 4);
            v = 2; Native.DwmSetWindowAttribute(f.Handle, 33, ref v, 4);
            v = ColorRef(f.BackColor); Native.DwmSetWindowAttribute(f.Handle, 35, ref v, 4);
            v = ColorRef(T.Text); Native.DwmSetWindowAttribute(f.Handle, 36, ref v, 4);
        }
        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }

        public static Color? SystemAccent(bool dark)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent"))
                {
                    byte[] b = k == null ? null : k.GetValue("AccentPalette") as byte[];
                    if (b != null && b.Length >= 32) { int i = (dark ? 1 : 4) * 4; return Color.FromArgb(b[i], b[i + 1], b[i + 2]); }
                }
            }
            catch { }
            return null;
        }

        // logo.png is embedded into the exe by build.bat
        static Bitmap logo; static readonly Dictionary<int, Bitmap> logoCache = new Dictionary<int, Bitmap>();
        public static Bitmap Logo
        {
            get
            {
                if (logo == null)
                {
                    Stream s = typeof(Ui).Assembly.GetManifestResourceStream("logo.png");
                    logo = s != null ? new Bitmap(s) : new Bitmap(1, 1);
                }
                return logo;
            }
        }
        public static Bitmap LogoAt(int size)
        {
            Bitmap b;
            if (logoCache.TryGetValue(size, out b)) return b;
            b = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(Logo, 0, 0, size, size);
            }
            logoCache[size] = b; return b;
        }

        // Writes a multi-size PNG .ico from logo.png (used by build.bat via --make-icon)
        public static void WriteIcon(string pngPath, string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 256 };
            List<byte[]> images = new List<byte[]>();
            using (Bitmap src = new Bitmap(pngPath))
            foreach (int n in sizes)
                using (Bitmap bmp = new Bitmap(n, n, PixelFormat.Format32bppArgb))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(src, 0, 0, n, n);
                    }
                    using (MemoryStream ms = new MemoryStream()) { bmp.Save(ms, ImageFormat.Png); images.Add(ms.ToArray()); }
                }
            using (BinaryWriter w = new BinaryWriter(File.Create(path)))
            {
                w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); w.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    w.Write((byte)0); w.Write((byte)0); w.Write((short)1); w.Write((short)32);
                    w.Write(images[i].Length); w.Write(offset); offset += images[i].Length;
                }
                foreach (byte[] img in images) w.Write(img);
            }
        }
    }

    // ================================================================== Animation
    class AnimValue
    {
        public float Value, Target, Speed, Epsilon;
        public Control Owner; public Action Changed;
        public AnimValue(Control owner, float v, float speed = 0.22f, float eps = 0.004f)
        { Owner = owner; Value = Target = v; Speed = speed; Epsilon = eps; }
        public void To(float t) { Target = t; if (Value != t) Anim.Add(this); }
        public void Snap(float v) { Value = Target = v; Fire(); }
        public bool Step()
        {
            float d = Target - Value;
            if (Math.Abs(d) <= Epsilon) { Value = Target; Fire(); return true; }
            Value += d * Speed; Fire(); return false;
        }
        void Fire()
        {
            if (Changed != null) Changed();
            else if (Owner != null && !Owner.IsDisposed) Owner.Invalidate();
        }
    }

    static class Anim
    {
        static System.Windows.Forms.Timer timer;
        static readonly List<AnimValue> list = new List<AnimValue>();
        public static void Add(AnimValue a)
        {
            if (!list.Contains(a)) list.Add(a);
            if (timer == null) { timer = new System.Windows.Forms.Timer(); timer.Interval = 10; timer.Tick += Tick; }
            timer.Enabled = true;
        }
        static void Tick(object s, EventArgs e)
        {
            for (int i = list.Count - 1; i >= 0; i--) if (list[i].Step()) list.RemoveAt(i);
            if (list.Count == 0) timer.Enabled = false;
        }
    }

    interface IValue { string Save(); void Load(string v); }
    interface IThemed { void Retheme(); }

    // ================================================================== Controls
    class FControl : Control
    {
        protected AnimValue hov;
        public FControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.Selectable, false);
            hov = new AnimValue(this, 0, 0.25f);
        }
        protected static int S(float v) { return Ui.S(v); }
        protected Color Bg { get { return Parent != null ? Parent.BackColor : Ui.T.Layer; } }
        protected Graphics Begin(PaintEventArgs e) { Graphics g = e.Graphics; g.Clear(Bg); Ui.Prep(g); return g; }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hov.To(1); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hov.To(0); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }

    enum Kind { Standard, Accent, Subtle }

    class FButton : FControl, IValue
    {
        public Kind Kind; public string Glyph, SubText; public bool Toggled, Toggleable, Bold;
        public float Radius = 4, TextSize = 10, GlyphSize = 11;
        readonly AnimValue press, danger;
        bool isDanger;

        public FButton(string text) { Text = text; Cursor = Cursors.Hand; press = new AnimValue(this, 0, 0.35f); danger = new AnimValue(this, 0, 0.16f); }

        public bool IsDanger { get { return isDanger; } set { if (isDanger == value) return; isDanger = value; danger.To(value ? 1 : 0); } }

        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) press.To(1); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); press.To(0); }
        protected override void OnClick(EventArgs e) { if (Toggleable) { Toggled = !Toggled; Invalidate(); } base.OnClick(e); }
        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        public string Save() { return Toggled.ToString(); }
        public void Load(string v) { Toggled = bool.Parse(v); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T; Color bg = Bg;
            float h = hov.Value, p = press.Value;
            Color fill, border, fore;
            if (Kind == Kind.Accent)
            {
                Color b = Ui.Mix(t.Accent, t.Danger, danger.Value);
                fill = Ui.Mix(b, bg, 0.1f * h + 0.18f * p); border = Color.Empty;
                fore = Ui.Mix(t.AccentText, Color.White, danger.Value);
            }
            else if (Kind == Kind.Subtle)
            {
                fill = Ui.Mix(bg, t.Text, 0.06f * h + 0.03f * p); border = Color.Empty; fore = t.Text;
            }
            else
            {
                fill = Ui.Mix(Ui.Mix(t.Ctl, t.Text, 0.045f * h), bg, 0.35f * p); border = t.Stroke;
                fore = Ui.Mix(t.Text, t.Sub, p);
            }
            if (Toggled) { fill = Ui.Mix(fill, t.Accent, t.Dark ? 0.22f : 0.12f); border = t.Accent; }
            if (!Enabled) { fill = Ui.Mix(fill, bg, 0.55f); if (border != Color.Empty) border = Ui.Mix(border, bg, 0.55f); fore = Ui.Mix(fore, fill, 0.6f); }
            Ui.FillRound(g, Ui.Box(this), S(Radius), fill, border);

            Font tf = Ui.F(TextSize, Bold ? 1 : 0), sf = Ui.F(8.5f, 0), gf = Ui.Icon(GlyphSize);
            Size ts = Ui.Measure(Text, tf), ss = SubText == null ? Size.Empty : Ui.Measure(SubText, sf);
            Size gs = Glyph == null ? Size.Empty : new Size(S(GlyphSize * 1.5f), S(GlyphSize * 1.5f));
            int gap = Glyph != null && Text.Length > 0 ? S(SubText != null ? 14 : 6) : 0;
            int blockW = Math.Max(ts.Width, ss.Width), total = gs.Width + gap + blockW;
            int x = (Width - total) / 2;
            if (Glyph != null) Ui.TextCenter(g, Glyph, gf, new Rectangle(x, 0, gs.Width, Height), fore);
            x += gs.Width + gap;
            if (SubText == null) Ui.TextLeft(g, Text, tf, new Rectangle(x, 0, Width - x, Height), fore);
            else
            {
                int top = (Height - ts.Height - ss.Height - S(2)) / 2;
                Ui.TextLeft(g, Text, tf, new Rectangle(x, top, Width - x, ts.Height), fore);
                Ui.TextLeft(g, SubText, sf, new Rectangle(x, top + ts.Height + S(2), Width - x, ss.Height), Ui.Mix(fore, fill, 0.28f));
            }
        }
    }

    class Toggle : FControl, IValue
    {
        bool on; readonly AnimValue k; public bool ShowState = true;
        public event EventHandler CheckedChanged;
        public Toggle() { k = new AnimValue(this, 0, 0.28f); Cursor = Cursors.Hand; }
        public bool Checked
        {
            get { return on; }
            set { if (on == value) return; on = value; k.To(on ? 1 : 0); Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); }
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Checked = !Checked; }
        public string Save() { return on.ToString(); }
        public void Load(string v) { Checked = bool.Parse(v); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T; Color bg = Bg; float kv = k.Value, h = hov.Value;
            int w = S(40), ht = S(20);
            RectangleF r = new RectangleF(1, (Height - ht) / 2f, w, ht);
            Color offFill = Ui.Mix(t.Input, t.Text, 0.05f * h);
            Color onFill = Ui.Mix(t.Accent, bg, 0.1f * h);
            Color fill = Ui.Mix(offFill, onFill, kv), border = Ui.Mix(t.Sub, onFill, kv);
            if (!Enabled) { fill = Ui.Mix(fill, bg, 0.5f); border = Ui.Mix(border, bg, 0.5f); }
            Ui.FillRound(g, r, ht / 2f, fill, border);
            float kd = S(12) + S(2) * h, cx = r.X + S(10) + kv * S(20), cy = r.Y + ht / 2f;
            using (SolidBrush b = new SolidBrush(Ui.Mix(t.Sub, t.AccentText, kv))) g.FillEllipse(b, cx - kd / 2, cy - kd / 2, kd, kd);
            if (ShowState) Ui.TextLeft(g, on ? "On" : "Off", Ui.F(10, 0), new Rectangle(w + S(12), 0, Width, Height), Enabled ? t.Text : t.Sub);
        }
    }

    class Segmented : FControl, IValue
    {
        readonly string[] items; int sel, hi = -1; readonly AnimValue pos;
        public event EventHandler SelectedIndexChanged;
        public Segmented(string[] items) { this.items = items; pos = new AnimValue(this, 0, 0.24f, 0.002f); Cursor = Cursors.Hand; }
        public int SelectedIndex
        {
            get { return sel; }
            set { if (value == sel || value < 0 || value >= items.Length) return; sel = value; pos.To(value); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }
        float SegW { get { return (Width - S(6)) / (float)items.Length; } }
        int IndexAt(int x) { return Math.Max(0, Math.Min(items.Length - 1, (int)((x - S(3)) / SegW))); }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = IndexAt(e.X); if (i != hi) { hi = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hi = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) SelectedIndex = IndexAt(e.X); }
        public string Save() { return sel.ToString(); }
        public void Load(string v) { SelectedIndex = int.Parse(v); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T; Color bg = Bg; float sw = SegW;
            Ui.FillRound(g, Ui.Box(this), S(5), t.Input, t.Stroke);
            if (hi >= 0 && hi != sel && Enabled)
                Ui.FillRound(g, new RectangleF(S(3) + hi * sw, S(3), sw, Height - S(6)), S(4), Ui.Mix(t.Input, t.Text, 0.06f), Color.Empty);
            Color pill = Enabled ? t.Accent : Ui.Mix(t.Accent, bg, 0.6f);
            Ui.FillRound(g, new RectangleF(S(3) + pos.Value * sw, S(3), sw, Height - S(6)), S(4), pill, Color.Empty);
            for (int i = 0; i < items.Length; i++)
            {
                float a = Math.Max(0, 1 - Math.Abs(pos.Value - i));
                Color c = Ui.Mix(i == hi ? t.Text : t.Sub, t.AccentText, a);
                Ui.TextCenter(g, items[i], Ui.F(9.5f, i == sel ? 1 : 0), new Rectangle((int)(S(3) + i * sw), 0, (int)sw, Height), c);
            }
        }
    }

    class InputBox : FControl, IThemed
    {
        protected readonly TextBox tb = new TextBox(); public string Suffix;
        protected readonly AnimValue foc;
        public InputBox()
        {
            foc = new AnimValue(this, 0, 0.3f); Cursor = Cursors.IBeam;
            tb.BorderStyle = BorderStyle.None; tb.Font = Ui.F(10, 0); Controls.Add(tb);
            tb.GotFocus += delegate { foc.To(1); };
            tb.LostFocus += delegate { foc.To(0); };
            tb.MouseEnter += delegate { hov.To(1); };
            tb.MouseLeave += delegate { hov.To(0); };
            Retheme();
        }
        public override string Text { get { return tb.Text; } set { tb.Text = value; } }
        protected override void OnClick(EventArgs e) { base.OnClick(e); tb.Focus(); }
        public void Retheme() { tb.BackColor = Ui.T.Input; tb.ForeColor = Ui.T.Text; Invalidate(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutBox(); }
        protected void LayoutBox()
        {
            int sw = Suffix == null ? 0 : Ui.Measure(Suffix, Ui.F(9, 0)).Width + S(8);
            tb.SetBounds(S(10), (Height - tb.Height) / 2 + 1, Math.Max(10, Width - S(20) - sw), tb.Height);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T; float f = foc.Value;
            RectangleF r = Ui.Box(this);
            Color fill = Ui.Mix(t.Input, t.Text, 0.03f * hov.Value * (1 - f));
            if (!Enabled) fill = Ui.Mix(fill, Bg, 0.5f);
            if (tb.BackColor != fill) tb.BackColor = fill;
            Ui.FillRound(g, r, S(4), fill, t.Stroke);
            using (GraphicsPath p = Ui.Round(r, S(4)))
            {
                g.SetClip(p);
                Color line = Ui.Mix(Ui.Mix(t.Stroke, t.Text, 0.25f), t.Accent, f);
                int lh = f > 0.5f ? S(2) : 1;
                using (SolidBrush b = new SolidBrush(line)) g.FillRectangle(b, 0, Height - 1 - lh, Width, lh + 1);
                g.ResetClip();
            }
            if (Suffix != null)
                Ui.Text(g, Suffix, Ui.F(9, 0), new Rectangle(0, 0, Width - S(10), Height), t.Sub, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }
    }

    class NumberBox : InputBox, IValue
    {
        long val; public long Min, Max, Step = 1; bool updating;
        public event EventHandler ValueChanged;
        public NumberBox(long min, long max, long v, string suffix)
        {
            Min = min; Max = max; Suffix = suffix; val = v; tb.Text = v.ToString();
            tb.TextChanged += delegate
            {
                if (updating) return; long n;
                if (long.TryParse(tb.Text, out n) && n >= Min && n <= Max && n != val) { val = n; Raise(); }
            };
            tb.Leave += delegate { Value = val; tb.Text = val.ToString(); };
            tb.KeyPress += delegate (object s, KeyPressEventArgs e)
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !(e.KeyChar == '-' && Min < 0)) e.Handled = true;
            };
            tb.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Up) { Value += Step; e.Handled = true; }
                else if (e.KeyCode == Keys.Down) { Value -= Step; e.Handled = true; }
                else if (e.KeyCode == Keys.Enter) { Value = val; e.SuppressKeyPress = true; }
            };
            tb.MouseWheel += delegate (object s, MouseEventArgs e) { Value += Math.Sign(e.Delta) * Step; };
        }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Value += Math.Sign(e.Delta) * Step; }
        void Raise() { if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
        public long Value
        {
            get { return val; }
            set
            {
                long v = Math.Max(Min, Math.Min(Max, value)); bool changed = v != val; val = v;
                updating = true; if (tb.Text != v.ToString()) { tb.Text = v.ToString(); tb.SelectionStart = tb.Text.Length; } updating = false;
                if (changed) Raise();
            }
        }
        public string Save() { return val.ToString(); }
        public void Load(string v) { Value = long.Parse(v); }
    }

    class Card : Panel
    {
        public string Title;
        public Card() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : Ui.T.Layer); Ui.Prep(g);
            Ui.FillRound(g, Ui.Box(this), Ui.S(8), Ui.T.Card, Ui.T.CardStroke);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Title != null) Ui.TextLeft(e.Graphics, Title, Ui.F(10.5f, 1), new Rectangle(Ui.S(16), Ui.S(10), Width - Ui.S(32), Ui.S(26)), Ui.T.Text);
        }
    }

    class Page : Panel { public Page() { DoubleBuffered = true; } }

    class ContentHost : Panel
    {
        public ContentHost() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Theme t = Ui.T; g.Clear(t.Back); Ui.Prep(g);
            float r = Ui.S(16);
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddArc(0.5f, 0.5f, r, r, 180, 90);
                p.AddLine(r / 2 + 0.5f, 0.5f, Width + 2, 0.5f);
                p.AddLine(Width + 2, Height + 2, 0.5f, Height + 2);
                p.CloseFigure();
                using (SolidBrush b = new SolidBrush(t.Layer)) g.FillPath(b, p);
                using (Pen pen = new Pen(t.Dark ? Ui.Mix(t.Layer, t.Back, 2f) : t.CardStroke)) g.DrawPath(pen, p);
            }
        }
    }

    class StatTile : FControl
    {
        public string Caption, Glyph, Value = "0";
        public StatTile(string glyph, string caption) { Glyph = glyph; Caption = caption; }
        public void Set(string v) { if (v != Value) { Value = v; Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T;
            Ui.FillRound(g, Ui.Box(this), S(8), t.Card, t.CardStroke);
            Ui.TextLeft(g, Glyph, Ui.Icon(11), new Rectangle(S(16), S(12), S(20), S(20)), t.Accent);
            Ui.TextLeft(g, Caption, Ui.F(9, 0), new Rectangle(S(40), S(12), Width - S(48), S(20)), t.Sub);
            Ui.TextLeft(g, Value, Ui.F(17, 2), new Rectangle(S(14), S(32), Width - S(24), S(32)), t.Text);
        }
    }

    class NavBar : FControl
    {
        public class Item { public string Glyph, Text; public bool Busy; public AnimValue Hov; }
        public readonly List<Item> Items = new List<Item>();
        public int BottomCount = 1;
        public string StatusTitle = "Idle", StatusDetail = "Ready"; public bool StatusActive;
        public event EventHandler SelectedIndexChanged, ToggleRequested;
        int sel, hi = -2; readonly AnimValue ind, menuHov;
        readonly ToolTip tip = new ToolTip();
        const int Menu = -1;

        public NavBar() { ind = new AnimValue(this, 0, 0.2f, 0.5f); menuHov = new AnimValue(this, 0, 0.25f); }
        public void Add(string glyph, string text) { Item it = new Item(); it.Glyph = glyph; it.Text = text; it.Hov = new AnimValue(this, 0, 0.25f); Items.Add(it); }

        bool Wide { get { return Width >= S(150); } }
        Rectangle MenuRect { get { return new Rectangle(S(8), S(8), S(40), S(36)); } }
        Rectangle ItemRect(int i)
        {
            int h = S(38), step = S(42), top;
            if (i < Items.Count - BottomCount) top = S(60) + i * step;
            else top = Height - S(8) - (Items.Count - i) * step + (step - h);
            return new Rectangle(S(8), top, Width - S(16), h);
        }

        public int SelectedIndex
        {
            get { return sel; }
            set { if (value == sel) return; sel = value; ind.To(ItemRect(value).Y); Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty); }
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); SnapIndicator(); }
        public void SnapIndicator() { if (Items.Count > 0) ind.Snap(ItemRect(sel).Y); }

        int IndexAt(Point p)
        {
            if (MenuRect.Contains(p)) return Menu;
            for (int i = 0; i < Items.Count; i++) if (ItemRect(i).Contains(p)) return i;
            return -2;
        }
        void SetHover(int i)
        {
            if (i == hi) return;
            if (hi == Menu) menuHov.To(0); else if (hi >= 0) Items[hi].Hov.To(0);
            hi = i;
            if (hi == Menu) menuHov.To(1); else if (hi >= 0) Items[hi].Hov.To(1);
            Cursor = hi >= Menu ? Cursors.Hand : Cursors.Default;
            string text = hi == Menu ? (Wide ? "Collapse menu" : "Expand menu") : hi >= 0 && !Wide ? Items[hi].Text : null;
            tip.SetToolTip(this, text);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); SetHover(IndexAt(e.Location)); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); SetHover(-2); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); int i = IndexAt(e.Location);
            if (i == Menu) { if (ToggleRequested != null) ToggleRequested(this, EventArgs.Empty); hi = -3; SetHover(-2); }
            else if (i >= 0) SelectedIndex = i;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Theme t = Ui.T; g.Clear(t.Back); Ui.Prep(g);
            bool wide = Wide;

            Rectangle mr = MenuRect;
            if (menuHov.Value > 0.01f) Ui.FillRound(g, mr, S(5), Ui.Mix(t.Back, t.Text, 0.06f * menuHov.Value), Color.Empty);
            Ui.TextCenter(g, "\uE700", Ui.Icon(12), mr, t.Text);
            if (wide)
            {
                int ls = S(26);
                g.DrawImage(Ui.LogoAt(ls), S(56), mr.Y + (mr.Height - ls) / 2, ls, ls);
                Ui.TextLeft(g, "ProClicker", Ui.F(12, 2), new Rectangle(S(88), mr.Y, Width - S(92), mr.Height), t.Text);
            }

            for (int i = 0; i < Items.Count; i++)
            {
                Item it = Items[i]; Rectangle r = ItemRect(i); float hv = it.Hov.Value;
                if (i == sel || hv > 0.01f)
                {
                    float a = i == sel ? 0.075f + 0.02f * hv : 0.05f * hv;
                    Ui.FillRound(g, r, S(5), Ui.Mix(t.Back, t.Text, a), Color.Empty);
                }
                Ui.TextCenter(g, it.Glyph, Ui.Icon(12), new Rectangle(r.X + S(6), r.Y, S(28), r.Height), t.Text);
                if (wide) Ui.TextLeft(g, it.Text, Ui.F(10, i == sel ? 1 : 0), new Rectangle(r.X + S(44), r.Y, r.Width - S(66), r.Height), t.Text);
                if (it.Busy)
                {
                    int d = S(7), bx = wide ? r.Right - S(18) : r.Right - S(9), by = wide ? r.Y + (r.Height - d) / 2 : r.Y + S(6);
                    using (SolidBrush b = new SolidBrush(t.Accent)) g.FillEllipse(b, bx, by, d, d);
                }
            }
            Ui.FillRound(g, new RectangleF(S(8), ind.Value + (S(38) - S(16)) / 2f, S(3), S(16)), S(1.5f), t.Accent, Color.Empty);

            // status card above the bottom items (a dot only when collapsed)
            int sh = wide ? S(58) : S(40);
            Rectangle sr = new Rectangle(S(8), ItemRect(Items.Count - BottomCount).Y - sh - S(10), Width - S(16), sh);
            Ui.FillRound(g, sr, S(7), Ui.Mix(t.Back, t.Text, t.Dark ? 0.04f : 0.025f), Ui.Mix(t.Back, t.Text, 0.08f));
            int dd = S(8);
            using (SolidBrush b = new SolidBrush(StatusActive ? t.Accent : t.Sub))
                g.FillEllipse(b, wide ? sr.X + S(14) : sr.X + (sr.Width - dd) / 2, wide ? sr.Y + S(15) : sr.Y + (sr.Height - dd) / 2, dd, dd);
            if (wide)
            {
                Ui.TextLeft(g, StatusTitle, Ui.F(10, 1), new Rectangle(sr.X + S(30), sr.Y + S(8), sr.Width - S(36), S(22)), t.Text);
                Ui.TextLeft(g, StatusDetail, Ui.F(8.5f, 0), new Rectangle(sr.X + S(30), sr.Y + S(30), sr.Width - S(36), S(20)), t.Sub);
            }
        }
    }

    class ItemList : FControl
    {
        public readonly List<string> Items = new List<string>();
        public string EmptyTitle = "", EmptyText = "", EmptyGlyph = "\uE946";
        public int[] Columns = new int[0];
        int sel = -1, hi = -1; readonly AnimValue scroll;
        public event EventHandler SelectedIndexChanged, ItemActivated;
        public ItemList() { scroll = new AnimValue(this, 0, 0.3f, 0.5f); }
        int RowH { get { return S(36); } }
        int MaxScroll { get { return Math.Max(0, Items.Count * RowH + S(8) - Height); } }
        public int SelectedIndex
        {
            get { return sel; }
            set
            {
                sel = value < Items.Count ? value : -1;
                if (sel >= 0)
                {
                    int top = S(4) + sel * RowH;
                    if (top < scroll.Target) scroll.To(top - S(4));
                    else if (top + RowH > scroll.Target + Height) scroll.To(Math.Min(MaxScroll, top + RowH - Height + S(4)));
                }
                Invalidate(); if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
        public void Changed() { if (sel >= Items.Count) sel = Items.Count - 1; if (scroll.Target > MaxScroll) scroll.To(MaxScroll); Invalidate(); }
        int IndexAt(Point p)
        {
            if (p.Y < S(2) || p.Y > Height - S(2)) return -1;
            int i = (int)Math.Floor((p.Y - S(4) + scroll.Value) / RowH);
            return i >= 0 && i < Items.Count ? i : -1;
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); int i = IndexAt(e.Location); if (i != hi) { hi = i; Invalidate(); } }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hi = -1; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); int i = IndexAt(e.Location); if (i >= 0) SelectedIndex = i; }
        protected override void OnDoubleClick(EventArgs e) { base.OnDoubleClick(e); if (sel >= 0 && ItemActivated != null) ItemActivated(this, EventArgs.Empty); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll.To(Math.Max(0, Math.Min(MaxScroll, scroll.Target - e.Delta / 120f * RowH * 2)));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T;
            RectangleF box = Ui.Box(this);
            Ui.FillRound(g, box, S(6), t.Input, t.Stroke);
            if (Items.Count == 0)
            {
                int cy = Height / 2;
                Ui.TextCenter(g, EmptyGlyph, Ui.Icon(22), new Rectangle(0, cy - S(56), Width, S(36)), t.Sub);
                Ui.TextCenter(g, EmptyTitle, Ui.F(10.5f, 1), new Rectangle(0, cy - S(14), Width, S(22)), t.Text);
                Ui.TextCenter(g, EmptyText, Ui.F(9, 0), new Rectangle(0, cy + S(10), Width, S(20)), t.Sub);
                return;
            }
            using (GraphicsPath clip = Ui.Round(box, S(6))) g.SetClip(clip);
            for (int i = 0; i < Items.Count; i++)
            {
                int y = S(4) + i * RowH - (int)scroll.Value;
                if (y + RowH < 0 || y > Height) continue;
                Rectangle r = new Rectangle(S(4), y, Width - S(8), RowH - S(2));
                if (i == sel) Ui.FillRound(g, r, S(4), Ui.Mix(t.Input, t.Accent, t.Dark ? 0.16f : 0.1f), Color.Empty);
                else if (i == hi) Ui.FillRound(g, r, S(4), Ui.Mix(t.Input, t.Text, 0.05f), Color.Empty);
                if (i == sel) Ui.FillRound(g, new RectangleF(r.X, r.Y + (r.Height - S(16)) / 2f, S(3), S(16)), S(1.5f), t.Accent, Color.Empty);
                string[] cols = Items[i].Split('\t');
                int x = r.X + S(14);
                for (int c = 0; c < cols.Length; c++)
                {
                    int w = c < Columns.Length ? S(Columns[c]) : r.Right - x;
                    Ui.TextLeft(g, cols[c], Ui.F(10, c == 0 && cols.Length > 1 ? 1 : 0), new Rectangle(x, r.Y, w, r.Height), c == 0 && cols.Length > 1 ? t.Sub : t.Text);
                    x += w;
                }
            }
            if (MaxScroll > 0)
            {
                float vh = Height - S(8), th = Math.Max(S(24), vh * Height / (Items.Count * RowH + S(8)));
                float ty = S(4) + (vh - th) * (scroll.Value / MaxScroll);
                Ui.FillRound(g, new RectangleF(Width - S(7), ty, S(3), th), S(1.5f), Ui.Mix(t.Input, t.Sub, 0.7f), Color.Empty);
            }
            g.ResetClip();
        }
    }

    class Toast : FControl
    {
        string msg = "", glyph = "\uE946"; bool showing;
        readonly AnimValue y; readonly System.Windows.Forms.Timer hide = new System.Windows.Forms.Timer();
        public Toast()
        {
            Visible = false;
            y = new AnimValue(null, 0, 0.22f, 0.5f);
            y.Changed = delegate { Top = (int)y.Value; if (!showing && y.Value == y.Target) Visible = false; };
            hide.Interval = 2600; hide.Tick += delegate { Dismiss(); };
        }
        public void Show(string g, string text)
        {
            if (Parent == null) return;
            glyph = g; msg = text;
            Width = Ui.Measure(text, Ui.F(10, 0)).Width + S(60); Height = S(44);
            Left = (Parent.Width - Width) / 2;
            using (GraphicsPath p = Ui.Round(new RectangleF(0, 0, Width, Height), S(8))) Region = new Region(p);
            float shown = Parent.Height - Height - S(24), hidden = Parent.Height + S(4);
            if (!Visible) { y.Snap(hidden); Visible = true; }
            BringToFront(); showing = true; y.To(shown); hide.Stop(); hide.Start(); Invalidate();
        }
        public void Dismiss() { hide.Stop(); showing = false; if (Parent != null) y.To(Parent.Height + S(4)); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Dismiss(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Theme t = Ui.T; Ui.Prep(g);
            Color fill = t.Dark ? Ui.Mix(t.Card, t.Text, 0.08f) : Ui.Mix(t.Text, t.Back, 0.1f);
            Color fore = t.Dark ? t.Text : t.Back;
            g.Clear(fill);
            using (Pen p = new Pen(Ui.Mix(fill, t.Text, 0.12f))) using (GraphicsPath path = Ui.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), S(8))) g.DrawPath(p, path);
            Ui.TextCenter(g, glyph, Ui.Icon(12), new Rectangle(S(12), 0, S(24), Height), t.Accent);
            Ui.TextLeft(g, msg, Ui.F(10, 0), new Rectangle(S(42), 0, Width - S(50), Height), fore);
        }
    }

    class ThemeTile : FControl
    {
        public readonly Theme Th; public bool Selected;
        public ThemeTile(Theme th) { Th = th; Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T; float h = hov.Value;
            RectangleF outer = new RectangleF(1, 1, Width - 3, Height - 3);
            Ui.FillRound(g, outer, S(6), Th.Back, Color.Empty);
            // mini window preview
            RectangleF layer = new RectangleF(outer.X + S(22), outer.Y + S(8), outer.Width - S(22), outer.Height - S(30));
            float lh = layer.Height;
            Ui.FillRound(g, layer, S(4), Th.Layer, Color.Empty);
            Ui.FillRound(g, new RectangleF(layer.X + S(6), layer.Y + lh * 0.18f, layer.Width - S(12), lh * 0.32f), S(2), Th.Card, Color.Empty);
            Ui.FillRound(g, new RectangleF(layer.X + S(6), layer.Y + lh * 0.6f, S(24), lh * 0.24f), S(2), Th.Accent, Color.Empty);
            Ui.FillRound(g, new RectangleF(outer.X + S(8), outer.Y + S(12), S(8), S(3)), S(1), Th.Accent, Color.Empty);
            Ui.FillRound(g, new RectangleF(outer.X + S(8), outer.Y + S(20), S(8), S(3)), S(1), Th.Sub, Color.Empty);
            Ui.TextLeft(g, Th.Name, Ui.F(8.5f, Selected ? 1 : 0), new Rectangle((int)outer.X + S(8), (int)outer.Bottom - S(22), (int)outer.Width, S(20)), Th.Text);
            Color border = Selected ? t.Accent : Ui.Mix(t.CardStroke, t.Text, 0.12f + 0.25f * h);
            using (Pen p = new Pen(border, Selected ? S(2) : 1)) using (GraphicsPath path = Ui.Round(outer, S(6))) g.DrawPath(p, path);
        }
    }

    // ================================================================== Pick overlay
    class PickOverlay : Form
    {
        public Point Picked; public bool Ok;
        public PickOverlay()
        {
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen; TopMost = true; ShowInTaskbar = false;
            BackColor = Color.Black; Opacity = 0.4; Cursor = Cursors.Cross; KeyPreview = true; DoubleBuffered = true;
            MouseDown += delegate { Picked = Cursor.Position; Ok = true; Close(); };
            KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            MouseMove += delegate { Invalidate(); };
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; Ui.Prep(g);
            Rectangle wa = Screen.PrimaryScreen.Bounds;
            Rectangle r = new Rectangle(wa.Left - Bounds.Left + (wa.Width - Ui.S(460)) / 2, wa.Top - Bounds.Top + Ui.S(40), Ui.S(460), Ui.S(56));
            Ui.FillRound(g, r, Ui.S(10), Color.FromArgb(32, 32, 32), Color.FromArgb(70, 70, 70));
            Ui.TextCenter(g, "Click anywhere to pick a spot   \u2022   Esc to cancel", Ui.F(11, 1), r, Color.White);
            Point c = PointToClient(Cursor.Position);
            Ui.TextLeft(g, c.X + Bounds.Left + ", " + (c.Y + Bounds.Top), Ui.F(10, 1), new Rectangle(c.X + Ui.S(16), c.Y + Ui.S(12), Ui.S(140), Ui.S(22)), Color.White);
        }
        public static bool Pick(out Point p)
        {
            using (PickOverlay o = new PickOverlay()) { o.ShowDialog(); p = o.Picked; return o.Ok; }
        }
    }

    // ================================================================== Sounds
    static class Sfx
    {
        public static bool On;
        static SoundPlayer up, down;
        static byte[] Tone(double f1, double f2, int ms)
        {
            int rate = 44100, n = rate * ms / 1000;
            MemoryStream m = new MemoryStream(); BinaryWriter w = new BinaryWriter(m);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / n; phase += 2 * Math.PI * (f1 + (f2 - f1) * t) / rate;
                double env = Math.Min(1.0, i / (rate * 0.004)) * (1 - t) * (1 - t);
                w.Write((short)(Math.Sin(phase) * env * 0.18 * 32767));
            }
            return m.ToArray();
        }
        public static void Play(bool start)
        {
            if (!On) return;
            try
            {
                if (up == null) { up = new SoundPlayer(new MemoryStream(Tone(660, 990, 110))); up.Load(); down = new SoundPlayer(new MemoryStream(Tone(780, 440, 120))); down.Load(); }
                (start ? up : down).Play();
            }
            catch { }
        }
    }

    // ================================================================== Updates (GitHub Releases)
    static class Updater
    {
        // Where releases are published: https://github.com/Purpletxt/ProClicker/releases
        public const string Repo = "Purpletxt/ProClicker";
        public const string AssetName = "ProClicker.exe";
        public static readonly Version Current = Parse(AppInfo.Version);

        public class Release { public Version Version; public string Tag, Notes, Url, Sha256; public long Size; }

        public static Version Parse(string tag)
        {
            string s = tag.Trim().TrimStart('v', 'V');
            int dash = s.IndexOfAny(new[] { '-', '+', ' ' }); if (dash > 0) s = s.Substring(0, dash);
            if (s.IndexOf('.') < 0) s += ".0";
            return new Version(s);
        }

        static WebClient Client()
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2, required by GitHub
            WebClient wc = new WebClient();
            wc.Headers[HttpRequestHeader.UserAgent] = "ProClicker/" + AppInfo.Version;
            return wc;
        }
        public static WebClient DownloadClient() { return Client(); }

        // Returns the latest published release, or null if there are none yet. Throws on network errors.
        public static Release GetLatest()
        {
            string json;
            using (WebClient wc = Client())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                try { json = wc.DownloadString("https://api.github.com/repos/" + Repo + "/releases/latest"); }
                catch (WebException ex)
                {
                    HttpWebResponse resp = ex.Response as HttpWebResponse;
                    if (resp != null && resp.StatusCode == HttpStatusCode.NotFound) return null; // nothing published yet
                    if (resp != null && ((int)resp.StatusCode == 403 || (int)resp.StatusCode == 429))
                        throw new Exception("GitHub is limiting requests right now, try again in a while");
                    if (resp == null) throw new Exception("no internet connection");
                    throw;
                }
            }
            IDictionary<string, object> d = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
            Release r = new Release();
            r.Tag = (string)d["tag_name"]; r.Version = Parse(r.Tag);
            r.Notes = d.ContainsKey("body") && d["body"] != null ? (string)d["body"] : "";
            string shaUrl = null;
            foreach (object o in (IEnumerable)d["assets"])
            {
                IDictionary<string, object> a = (IDictionary<string, object>)o;
                string name = (string)a["name"];
                if (name.Equals(AssetName, StringComparison.OrdinalIgnoreCase))
                {
                    r.Url = (string)a["browser_download_url"]; r.Size = Convert.ToInt64(a["size"]);
                    object digest; // GitHub publishes "sha256:<hash>" for every uploaded file
                    if (a.TryGetValue("digest", out digest) && digest is string && ((string)digest).StartsWith("sha256:")) r.Sha256 = ((string)digest).Substring(7);
                }
                else if (name.Equals(AssetName + ".sha256", StringComparison.OrdinalIgnoreCase)) shaUrl = (string)a["browser_download_url"];
            }
            if (r.Url == null) throw new Exception("the latest release has no " + AssetName + " attached");
            if (shaUrl != null)
                using (WebClient wc = Client())
                {
                    string expected = wc.DownloadString(shaUrl).Trim().Split(' ', '\t', '\r', '\n')[0];
                    if (r.Sha256 != null && !r.Sha256.Equals(expected, StringComparison.OrdinalIgnoreCase))
                        throw new Exception("the release checksums don't match each other");
                    r.Sha256 = expected;
                }
            return r;
        }

        public static string FileSha256(string path)
        {
            using (SHA256 sha = SHA256.Create()) using (FileStream fs = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
        }

        static string ExePath { get { return Application.ExecutablePath; } }
        public static string NewPath { get { return Path.Combine(Path.GetDirectoryName(ExePath), Path.GetFileNameWithoutExtension(ExePath) + ".new.exe"); } }
        static string OldPath { get { return Path.Combine(Path.GetDirectoryName(ExePath), Path.GetFileNameWithoutExtension(ExePath) + ".old.exe"); } }

        // A running exe can't be overwritten but can be renamed: move ourselves aside, put the new exe in place, start it.
        public static void InstallAndRestart(string newFile)
        {
            if (File.Exists(OldPath)) File.Delete(OldPath);
            File.Move(ExePath, OldPath);
            try { File.Move(newFile, ExePath); }
            catch { File.Move(OldPath, ExePath); throw; }
            Process.Start(ExePath, "--updated");
        }

        // Called on startup: removes the previous version left behind by an update.
        public static void CleanupOld()
        {
            if (!File.Exists(OldPath) && !File.Exists(NewPath)) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                for (int i = 0; i < 20; i++)
                {
                    try { if (File.Exists(OldPath)) File.Delete(OldPath); if (File.Exists(NewPath)) File.Delete(NewPath); return; }
                    catch { Thread.Sleep(500); }
                }
            });
        }

        // GitHub release notes are Markdown; show them as plain text
        public static string PlainNotes(string md)
        {
            if (string.IsNullOrEmpty(md.Trim())) return "No release notes for this version.";
            StringBuilder sb = new StringBuilder();
            foreach (string raw in md.Replace("\r", "").Split('\n'))
            {
                string line = raw.TrimEnd();
                string t = line.TrimStart();
                if (t.StartsWith("#")) line = t.TrimStart('#').Trim();
                else if (t.StartsWith("- ") || t.StartsWith("* ")) line = "\u2022 " + t.Substring(2);
                sb.AppendLine(line.Replace("**", "").Replace("`", ""));
            }
            return sb.ToString().Trim();
        }
    }

    class ProgressLine : FControl
    {
        readonly AnimValue v;
        public ProgressLine() { v = new AnimValue(this, 0, 0.25f, 0.001f); }
        public float Value { set { v.To(Math.Max(0, Math.Min(1, value))); } }
        public void Reset() { v.Snap(0); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = Begin(e); Theme t = Ui.T;
            float h = Height;
            Ui.FillRound(g, new RectangleF(0, 0, Width - 1, h - 1), h / 2, Ui.Mix(t.Input, t.Text, 0.12f), Color.Empty);
            if (v.Value > 0.002f) Ui.FillRound(g, new RectangleF(0, 0, Math.Max(h, (Width - 1) * v.Value), h - 1), h / 2, t.Accent, Color.Empty);
        }
    }

    class NotesBox : Panel, IThemed
    {
        readonly TextBox tb = new TextBox();
        public NotesBox(string text)
        {
            DoubleBuffered = true; Padding = new Padding(Ui.S(12), Ui.S(10), Ui.S(6), Ui.S(10));
            tb.Multiline = true; tb.ReadOnly = true; tb.BorderStyle = BorderStyle.None; tb.ScrollBars = ScrollBars.Vertical;
            tb.Dock = DockStyle.Fill; tb.Font = Ui.F(9.5f, 0); tb.Text = text.Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"); tb.TabStop = false;
            Controls.Add(tb); Retheme();
        }
        public void Retheme() { BackColor = Ui.T.Input; tb.BackColor = Ui.T.Input; tb.ForeColor = Ui.T.Text; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); // dark scrollbar to match dark themes
            if (Ui.T.Dark) tb.HandleCreated += delegate { Native.SetWindowTheme(tb.Handle, "DarkMode_Explorer", null); };
            if (Ui.T.Dark && tb.IsHandleCreated) Native.SetWindowTheme(tb.Handle, "DarkMode_Explorer", null);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Graphics g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : Ui.T.Layer); Ui.Prep(g);
            Ui.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), Ui.S(6), Ui.T.Input, Ui.T.Stroke);
        }
    }

    // The "Update available" screen: release notes, then download progress, verify, install and restart.
    class UpdateDialog : Form
    {
        readonly Updater.Release rel; readonly Action<string> onSkip;
        readonly Label title, sub, status; readonly NotesBox notes; readonly ProgressLine bar;
        readonly FButton btnUpdate, btnLater, btnSkip;
        WebClient wc; bool busy;

        static int S(float v) { return Ui.S(v); }

        public UpdateDialog(Updater.Release r, Action<string> skip)
        {
            rel = r; onSkip = skip;
            AutoScaleMode = AutoScaleMode.None; Font = Ui.F(10, 0); Text = "ProClicker update";
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(S(460), S(330)); BackColor = Ui.T.Layer; DoubleBuffered = true;

            title = AddLabel("Update available", 92, 22, 15, 2, false);
            sub = AddLabel("Version " + r.Version + " is ready. You have " + AppInfo.Version + ".", 92, 54, 9.5f, 0, true);
            AddLabel("What's new", 24, 96, 10, 1, false);
            notes = new NotesBox(Updater.PlainNotes(r.Notes)); notes.SetBounds(S(24), S(122), S(412), S(128)); Controls.Add(notes);

            status = AddLabel("", 24, 262, 9.5f, 0, true); status.Visible = false;
            bar = new ProgressLine(); bar.SetBounds(S(24), S(292), S(300), S(6)); bar.Visible = false; Controls.Add(bar);

            btnSkip = Btn("Skip this version", Kind.Subtle, 24, 140);
            btnLater = Btn("Later", Kind.Standard, 220, 92);
            btnUpdate = Btn("Update now", Kind.Accent, 320, 116); btnUpdate.Glyph = "\uE896"; btnUpdate.Bold = true;
            btnSkip.Click += delegate { onSkip(rel.Tag); Close(); };
            btnLater.Click += delegate { if (busy) Cancel(); else Close(); };
            btnUpdate.Click += delegate { StartDownload(); };

            if (string.IsNullOrEmpty(rel.Sha256))
            {
                btnUpdate.Enabled = false;
                ShowStatus("This release has no checksum, so it can't be installed safely.", true);
            }
        }

        Label AddLabel(string text, int x, int y, float size, int weight, bool subtle)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(S(x), S(y)); l.Font = Ui.F(size, weight);
            l.ForeColor = subtle ? Ui.T.Sub : Ui.T.Text; l.BackColor = Color.Transparent; l.UseMnemonic = false;
            l.MaximumSize = new Size(S(412), 0); Controls.Add(l); return l;
        }
        FButton Btn(string text, Kind kind, int x, int w)
        {
            FButton b = new FButton(text); b.Kind = kind; b.SetBounds(S(x), S(278), S(w), S(34)); Controls.Add(b); return b;
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Ui.StyleTitleBar(this); }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics; int ls = S(52);
            g.DrawImage(Ui.LogoAt(ls), S(24), S(20), ls, ls);
        }

        void ShowStatus(string text, bool error)
        {
            status.Text = text; status.ForeColor = error ? Ui.T.Danger : Ui.T.Sub; status.Visible = true;
            status.Top = S(250);
        }

        void SetBusy(bool on)
        {
            busy = on;
            btnSkip.Visible = !on; btnUpdate.Visible = !on; bar.Visible = on;
            btnLater.Text = on ? "Cancel" : "Later"; btnLater.Left = S(on ? 344 : 220);
            if (on) bar.Reset();
        }
        void StartDownload()
        {
            string tmp = Updater.NewPath;
            try { if (File.Exists(tmp)) File.Delete(tmp); File.WriteAllBytes(tmp, new byte[0]); File.Delete(tmp); }
            catch
            {
                ShowStatus("ProClicker can't update itself in this folder. Move it to Desktop or Documents and try again.", true);
                return;
            }
            SetBusy(true); title.Text = "Downloading update";
            ShowStatus("Starting download...", false);
            wc = Updater.DownloadClient();
            wc.DownloadProgressChanged += delegate (object s, DownloadProgressChangedEventArgs e)
            {
                long total = e.TotalBytesToReceive > 0 ? e.TotalBytesToReceive : rel.Size;
                if (total > 0) bar.Value = (float)e.BytesReceived / total;
                ShowStatus("Downloading... " + Kb(e.BytesReceived) + (total > 0 ? " of " + Kb(total) : ""), false);
            };
            wc.DownloadFileCompleted += delegate (object s, System.ComponentModel.AsyncCompletedEventArgs e)
            {
                wc.Dispose(); wc = null;
                if (e.Cancelled) { TryDelete(tmp); return; }
                if (e.Error != null) { TryDelete(tmp); Fail("Download failed: " + e.Error.Message); return; }
                Finish(tmp);
            };
            wc.DownloadFileAsync(new Uri(rel.Url), tmp);
        }

        void Finish(string tmp)
        {
            bar.Value = 1; title.Text = "Installing update"; ShowStatus("Checking the download is genuine...", false); Refresh();
            string actual = Updater.FileSha256(tmp);
            if (!actual.Equals(rel.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                TryDelete(tmp); Fail("The download didn't match its checksum, so it wasn't installed. Please try again later."); return;
            }
            ShowStatus("Installing and restarting...", false); Refresh();
            try { Updater.InstallAndRestart(tmp); }
            catch (Exception ex) { TryDelete(tmp); Fail("Couldn't install the update: " + ex.Message); return; }
            busy = false; DialogResult = DialogResult.OK; Close();
        }

        void Fail(string msg) { SetBusy(false); title.Text = "Update available"; btnUpdate.Text = "Try again"; ShowStatus(msg, true); }

        void Cancel()
        {
            if (wc != null) wc.CancelAsync();
            SetBusy(false); title.Text = "Update available"; ShowStatus("Download cancelled.", false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy && wc != null) wc.CancelAsync();
            base.OnFormClosing(e);
        }

        static string Kb(long b) { return b >= 1048576 ? (b / 1048576.0).ToString("0.0") + " MB" : (b / 1024) + " KB"; }
        static void TryDelete(string f) { try { if (File.Exists(f)) File.Delete(f); } catch { } }
    }

    // ================================================================== Main window
    struct RecEvent { public long T; public int Type, X, Y, Data; } // Type: 0 move, 1 down, 2 up, 3 wheel, 4 keydown, 5 keyup

    class MainForm : Form
    {
        static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ProClicker");
        static readonly string ProfileDir = Path.Combine(DataDir, "profiles");
        static readonly string SettingsFile = Path.Combine(DataDir, "settings.ini");
        static readonly string LastSessionFile = Path.Combine(DataDir, "last-session.txt");
        // layout (in 96-dpi units): content column is CW wide with PX padding each side
        const int NAV_WIDE = 196, NAV_SLIM = 56, PX = 22, CW = 560, PAGE_W = CW + PX * 2, HEIGHT = 604;

        NavBar nav; ContentHost host; Toast toast;
        readonly List<Page> pages = new List<Page>(); int curPage = -1; AnimValue pageAnim, navAnim;
        bool navCollapsed;
        readonly Dictionary<string, IValue> saved = new Dictionary<string, IValue>();

        // hotkeys: 0 clicker, 1 key presser, 2 record, 3 playback
        readonly string[] hotkeyNames = { "Clicker", "Key presser", "Record", "Playback" };
        readonly Keys[] hotkeys = { Keys.F6, Keys.F7, Keys.F8, Keys.F9 };
        readonly FButton[] hotkeyButtons = new FButton[4];
        int rebindingIndex = -1;

        // clicker
        NumberBox nbH, nbM, nbS, nbMs, nbOffset, nbHold, nbRepeat, nbX, nbY;
        Toggle tgOffset; Segmented segBtn, segType, segRepeat, segPos; FButton heroClick, btnPickFixed;
        StatTile tileCps, tileRun; Label lblEstimate, lblRepeatHint;
        volatile bool clicking; long totalClicks, sessionClicks;
        readonly ManualResetEvent wakeEvent = new ManualResetEvent(false);

        // points
        ItemList listPoints; readonly List<Point> points = new List<Point>();

        // key presser
        int pressKey = (int)Keys.Space; bool capturingPressKey;
        FButton keycap, chipCtrl, chipShift, chipAlt, heroKey;
        NumberBox nbKInt, nbKHold, nbKRep; Segmented segKRep;
        volatile bool keyPressing; long totalKeys;

        // recorder
        readonly List<RecEvent> recording = new List<RecEvent>();
        volatile bool isRecording, isPlaying; Stopwatch recWatch; long lastMoveT = -100;
        IntPtr mouseHook, keyHook; Native.HookProc mouseProc, keyProc;
        Label lblRecInfo; FButton heroRec, heroPlay; NumberBox nbSpeed, nbLoops; Toggle tgMoves, tgLoopForever;

        // settings
        int themeIndex = 1; readonly List<ThemeTile> themeTiles = new List<ThemeTile>();
        Toggle tgSysAccent, tgTop, tgTray, tgSound, tgAutoUpdate; NumberBox nbDelay; FButton btnCheckUpdates; string skippedVersion = "";
        ItemList listProfiles; InputBox tbProfile;
        NotifyIcon tray; ToolStripMenuItem trayToggle;
        readonly System.Windows.Forms.Timer countdown = new System.Windows.Forms.Timer(); int countLeft; Action countAction;

        readonly System.Windows.Forms.Timer uiTimer = new System.Windows.Forms.Timer();
        long lastCount; readonly Stopwatch cpsWatch = Stopwatch.StartNew(); double cps;

        public int StartPage { set { ShowPage(value); } }
        public bool Slim { set { SetNavCollapsed(value, false); } }

        public MainForm()
        {
            AutoScaleMode = AutoScaleMode.None; Font = Ui.F(10, 0);
            Text = "ProClicker"; StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            nav = new NavBar();
            nav.Add("\uE962", "Clicker"); nav.Add("\uE707", "Click points"); nav.Add("\uE765", "Key presser");
            nav.Add("\uE7C8", "Record & play"); nav.Add("\uE713", "Settings");
            nav.SelectedIndexChanged += delegate { ShowPage(nav.SelectedIndex); };
            nav.ToggleRequested += delegate { SetNavCollapsed(!navCollapsed, true); SaveSettings(); };
            Controls.Add(nav);

            host = new ContentHost();
            Controls.Add(host);
            toast = new Toast(); host.Controls.Add(toast);
            LayoutShell(S(NAV_WIDE));

            pageAnim = new AnimValue(null, 0, 0.2f, 0.5f);
            pageAnim.Changed = delegate { if (curPage >= 0) pages[curPage].Top = S(8) + (int)pageAnim.Value; };
            navAnim = new AnimValue(null, S(NAV_WIDE), 0.24f, 0.5f);
            navAnim.Changed = delegate { LayoutShell((int)Math.Round(navAnim.Value)); };

            BuildClicker(); BuildPoints(); BuildKeys(); BuildRecorder(); BuildSettings();

            LoadSettings();
            LoadProfileFile(LastSessionFile, true);
            ApplyTheme();
            RefreshProfiles();
            ShowPage(0);

            countdown.Interval = 1000; countdown.Tick += CountdownTick;
            uiTimer.Interval = 250; uiTimer.Tick += delegate { UpdateStatus(); }; uiTimer.Start();
            BuildTray();
            Native.timeBeginPeriod(1);
            UpdateUi();
        }

        // nav on the left, fixed-width content on the right; the window grows/shrinks with the nav
        void LayoutShell(int navW)
        {
            SuspendLayout();
            ClientSize = new Size(navW + S(PAGE_W) + 1, S(HEIGHT));
            nav.SetBounds(0, 0, navW, ClientSize.Height);
            host.SetBounds(navW, 0, S(PAGE_W) + 1, ClientSize.Height);
            ResumeLayout(false);
            nav.Update(); host.Update();
        }

        void SetNavCollapsed(bool collapsed, bool animate)
        {
            navCollapsed = collapsed;
            int target = S(collapsed ? NAV_SLIM : NAV_WIDE);
            if (animate) navAnim.To(target); else navAnim.Snap(target);
        }

        // ============================================================ layout helpers
        static int S(float v) { return Ui.S(v); }

        T Put<T>(Control parent, T c, int x, int y, int w, int h) where T : Control
        {
            c.SetBounds(S(x), S(y), S(w), S(h)); parent.Controls.Add(c); return c;
        }
        Label Lbl(Control parent, string text, int x, int y, bool sub = false, float size = 10, int weight = 0)
        {
            Label l = new Label(); l.Text = text; l.AutoSize = true; l.Location = new Point(S(x), S(y));
            l.Font = Ui.F(size, weight); l.BackColor = Color.Transparent; l.UseMnemonic = false;
            if (sub) l.Tag = "sub"; parent.Controls.Add(l); return l;
        }
        // label vertically centred on a 32px control row starting at rowY
        Label RowLbl(Control parent, string text, int x, int rowY, bool sub = false, float size = 10)
        {
            Label l = Lbl(parent, text, x, rowY, sub, size);
            l.Top = S(rowY) + (S(32) - l.PreferredHeight) / 2; return l;
        }
        NumberBox NB(Control p, string key, int x, int y, int w, long min, long max, long v, string suffix)
        {
            NumberBox n = Put(p, new NumberBox(min, max, v, suffix), x, y, w, 32); if (key != null) saved[key] = n; return n;
        }
        Segmented SEG(Control p, string key, int x, int y, int w, params string[] items)
        {
            Segmented s = Put(p, new Segmented(items), x, y, w, 32); if (key != null) saved[key] = s; return s;
        }
        Toggle TG(Control p, string key, int x, int y, bool showState = true)
        {
            Toggle t = Put(p, new Toggle(), x, y + 2, showState ? 80 : 44, 28); t.ShowState = showState; if (key != null) saved[key] = t; return t;
        }
        FButton BTN(Control p, string text, string glyph, int x, int y, int w, int h, Kind kind = Kind.Standard)
        {
            FButton b = Put(p, new FButton(text), x, y, w, h); b.Glyph = glyph; b.Kind = kind; return b;
        }
        FButton Hero(Control p, string text, string glyph, int x, int y, int w, int h)
        {
            FButton b = BTN(p, text, glyph, x, y, w, h, Kind.Accent);
            b.Bold = true; b.Radius = 8; b.TextSize = 11.5f; b.GlyphSize = 15; return b;
        }
        Card NewCard(Control p, string title, int x, int y, int w, int h)
        {
            Card c = Put(p, new Card(), PX + x, y, w, h); c.Title = title; return c;
        }
        Page NewPage(string title, string subtitle)
        {
            Page p = new Page(); p.SetBounds(1, S(8), S(PAGE_W), S(HEIGHT) - S(8)); p.Visible = false;
            Lbl(p, title, PX - 2, 6, false, 17, 2);
            Lbl(p, subtitle, PX, 40, true, 9.5f);
            host.Controls.Add(p); pages.Add(p); return p;
        }

        void ShowPage(int idx)
        {
            if (idx == curPage) return;
            if (curPage >= 0) pages[curPage].Visible = false;
            curPage = idx;
            if (nav.SelectedIndex != idx) nav.SelectedIndex = idx;
            pageAnim.Snap(S(20)); pages[idx].Visible = true; pageAnim.To(0);
            toast.BringToFront();
        }

        // ============================================================ pages
        // Every page: title at the top, content from y=72 to y<=584, CW (560) wide.
        void BuildClicker()
        {
            Page pg = NewPage("Clicker", "Automatic mouse clicking with precise timing.");
            tileCps = Put(pg, new StatTile("\uE945", "Clicks per second"), PX, 72, 150, 68);
            tileRun = Put(pg, new StatTile("\uE8EE", "Clicks this run"), PX + 162, 72, 150, 68);
            heroClick = Hero(pg, "Start clicking", "\uE768", PX + 324, 72, 236, 68);
            heroClick.Click += delegate { ToggleClicker(); };

            Card sp = NewCard(pg, "Speed", 0, 152, CW, 160);
            RowLbl(sp, "Interval", 14, 42);
            nbH = NB(sp, "hours", 108, 42, 64, 0, 999, 0, "h");
            nbM = NB(sp, "mins", 178, 42, 64, 0, 59, 0, "m");
            nbS = NB(sp, "secs", 248, 42, 64, 0, 59, 0, "s");
            nbMs = NB(sp, "ms", 318, 42, 82, 0, 999, 100, "ms");
            lblEstimate = RowLbl(sp, "", 412, 42, true);
            RowLbl(sp, "Presets", 14, 81);
            string[] pn = { "1/s", "5/s", "10/s", "20/s", "50/s", "100/s" }; int[] pv = { 1000, 200, 100, 50, 20, 10 };
            for (int i = 0; i < pn.Length; i++)
            {
                int ms = pv[i];
                FButton chip = BTN(sp, pn[i], null, 108 + i * 72, 82, 66, 30);
                chip.Click += delegate { nbH.Value = 0; nbM.Value = 0; nbS.Value = ms / 1000; nbMs.Value = ms % 1000; };
            }
            RowLbl(sp, "Random offset", 14, 120);
            tgOffset = TG(sp, "offsetOn", 120, 120);
            nbOffset = NB(sp, "offset", 204, 120, 86, 0, 100000, 40, "ms");
            RowLbl(sp, "\u00B1 random, so timing looks human", 300, 120, true, 9);

            Card mo = NewCard(pg, "Mouse", 0, 324, 274, 160);
            RowLbl(mo, "Button", 14, 42); segBtn = SEG(mo, "button", 92, 42, 168, "Left", "Right", "Middle");
            RowLbl(mo, "Click type", 14, 81); segType = SEG(mo, "type", 92, 81, 168, "Single", "Double", "Triple");
            RowLbl(mo, "Hold", 14, 120); nbHold = NB(mo, "hold", 92, 120, 96, 0, 10000, 0, "ms");
            RowLbl(mo, "per click", 196, 120, true, 9);

            Card rp = NewCard(pg, "Repeat", 286, 324, 274, 160);
            segRepeat = SEG(rp, "repeatMode", 14, 42, 246, "Until stopped", "Fixed count");
            RowLbl(rp, "Count", 14, 81); nbRepeat = NB(rp, "repeat", 92, 81, 130, 1, 100000000, 100, "times");
            lblRepeatHint = RowLbl(rp, "", 14, 120, true, 9);

            Card po = NewCard(pg, "Cursor position", 0, 496, CW, 84);
            segPos = SEG(po, "posMode", 14, 40, 266, "Cursor", "Fixed spot", "Points");
            RowLbl(po, "X", 292, 40, true); nbX = NB(po, "fixX", 306, 40, 74, -50000, 50000, 0, null);
            RowLbl(po, "Y", 388, 40, true); nbY = NB(po, "fixY", 402, 40, 74, -50000, 50000, 0, null);
            btnPickFixed = BTN(po, "Pick", "\uE81D", 484, 40, 62, 32);
            btnPickFixed.Click += delegate { Point p; if (DoPick(out p)) { nbX.Value = p.X; nbY.Value = p.Y; segPos.SelectedIndex = 1; } };

            EventHandler refresh = delegate { UpdateUi(); };
            foreach (NumberBox n in new[] { nbH, nbM, nbS, nbMs, nbRepeat }) n.ValueChanged += refresh;
            foreach (Segmented s in new[] { segType, segRepeat, segPos }) s.SelectedIndexChanged += refresh;
            tgOffset.CheckedChanged += refresh;
        }

        void BuildPoints()
        {
            Page pg = NewPage("Click points", "Spots the clicker visits in order when \"Points\" is selected.");
            listPoints = Put(pg, new ItemList(), PX, 72, 360, 508);
            listPoints.Columns = new[] { 48, 120 };
            listPoints.EmptyGlyph = "\uE707"; listPoints.EmptyTitle = "No click points yet"; listPoints.EmptyText = "Use \"Pick on screen\" to add one.";
            int bx = PX + 372, bw = 188;
            FButton add = BTN(pg, "Pick on screen", "\uE81D", bx, 72, bw, 36, Kind.Accent);
            FButton later = BTN(pg, "Add in 3 seconds", "\uE916", bx, 114, bw, 34);
            FButton up = BTN(pg, "Move up", "\uE74A", bx, 166, bw, 34);
            FButton down = BTN(pg, "Move down", "\uE74B", bx, 206, bw, 34);
            FButton rem = BTN(pg, "Remove", "\uE74D", bx, 246, bw, 34);
            FButton clr = BTN(pg, "Clear all", "\uE894", bx, 286, bw, 34);
            FButton use = BTN(pg, "Use these points", "\uE73E", bx, 338, bw, 36);
            Label tip = Lbl(pg, "Tip: \"Add in 3 seconds\" grabs wherever your mouse is, so you can place points inside games or menus.", bx, 388, true, 9);
            tip.MaximumSize = new Size(S(bw), 0);

            add.Click += delegate { Point p; if (DoPick(out p)) { points.Add(p); RefreshPoints(points.Count - 1); toast.Show("\uE707", "Added point " + points.Count + " at " + p.X + ", " + p.Y); } };
            later.Click += delegate
            {
                later.Enabled = false; int n = 3; later.Text = "Adding in 3...";
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 1000;
                t.Tick += delegate
                {
                    n--; if (n > 0) { later.Text = "Adding in " + n + "..."; return; }
                    t.Stop(); t.Dispose(); Point p = Cursor.Position; points.Add(p); RefreshPoints(points.Count - 1);
                    later.Text = "Add in 3 seconds"; later.Enabled = true; Sfx.Play(true);
                    toast.Show("\uE707", "Added point " + points.Count + " at " + p.X + ", " + p.Y);
                };
                t.Start();
            };
            up.Click += delegate { MovePoint(-1); };
            down.Click += delegate { MovePoint(1); };
            rem.Click += delegate { int i = listPoints.SelectedIndex; if (i < 0) return; points.RemoveAt(i); RefreshPoints(Math.Min(i, points.Count - 1)); };
            clr.Click += delegate
            {
                if (points.Count == 0) return;
                if (MessageBox.Show(this, "Remove all " + points.Count + " click points?", "ProClicker", MessageBoxButtons.YesNo) == DialogResult.Yes) { points.Clear(); RefreshPoints(-1); }
            };
            use.Click += delegate { segPos.SelectedIndex = 2; ShowPage(0); toast.Show("\uE73E", "Clicker will cycle through your " + points.Count + " points"); };
        }

        void MovePoint(int dir)
        {
            int i = listPoints.SelectedIndex, j = i + dir; if (i < 0 || j < 0 || j >= points.Count) return;
            Point t = points[i]; points[i] = points[j]; points[j] = t; RefreshPoints(j);
        }

        void RefreshPoints(int select)
        {
            listPoints.Items.Clear();
            for (int i = 0; i < points.Count; i++) listPoints.Items.Add((i + 1) + "\tX  " + points[i].X + "\tY  " + points[i].Y);
            listPoints.Changed(); listPoints.SelectedIndex = select;
        }

        void BuildKeys()
        {
            Page pg = NewPage("Key presser", "Press a keyboard key over and over, on its own timer.");
            Card k = NewCard(pg, "Key", 0, 72, CW, 136);
            keycap = BTN(k, "Space", "\uE765", 14, 42, 190, 56); keycap.TextSize = 14; keycap.Bold = true; keycap.GlyphSize = 14; keycap.Radius = 8;
            keycap.Click += delegate { CancelCaptures(); capturingPressKey = true; keycap.Toggled = true; keycap.Text = "Press a key..."; };
            Lbl(k, "Hold with it", 224, 40, true, 9);
            chipCtrl = BTN(k, "Ctrl", null, 224, 64, 64, 32); chipShift = BTN(k, "Shift", null, 294, 64, 64, 32); chipAlt = BTN(k, "Alt", null, 364, 64, 64, 32);
            foreach (FButton c in new[] { chipCtrl, chipShift, chipAlt }) c.Toggleable = true;
            saved["kCtrl"] = chipCtrl; saved["kShift"] = chipShift; saved["kAlt"] = chipAlt;
            Lbl(k, "Click the key box, then press the key you want (Esc cancels).", 14, 108, true, 9);

            Card tm = NewCard(pg, "Timing", 0, 220, CW, 160);
            RowLbl(tm, "Interval", 14, 42); nbKInt = NB(tm, "kInterval", 100, 42, 130, 1, 86400000, 500, "ms"); RowLbl(tm, "time between presses", 242, 42, true, 9);
            RowLbl(tm, "Hold", 14, 81); nbKHold = NB(tm, "kHold", 100, 81, 130, 0, 600000, 30, "ms"); RowLbl(tm, "how long the key stays down", 242, 81, true, 9);
            RowLbl(tm, "Repeat", 14, 120); segKRep = SEG(tm, "kRepeatMode", 100, 120, 220, "Until stopped", "Fixed count");
            nbKRep = NB(tm, "kRepeat", 328, 120, 130, 1, 100000000, 10, "times");
            segKRep.SelectedIndexChanged += delegate { UpdateUi(); };

            heroKey = Hero(pg, "Start key presser", "\uE768", PX, 392, CW, 64);
            heroKey.Click += delegate { ToggleKeyPresser(); };
        }

        void BuildRecorder()
        {
            Page pg = NewPage("Record & play", "Record your mouse and keyboard, then play it back.");
            Card rc = NewCard(pg, "Record", 0, 72, CW, 136);
            heroRec = Hero(rc, "Start recording", "\uE7C8", 14, 42, 240, 56);
            heroRec.Click += delegate { ToggleRecording(); };
            tgMoves = TG(rc, "recMoves", 272, 42, false); tgMoves.Checked = true;
            Lbl(rc, "Record mouse movement", 324, 44);
            Lbl(rc, "Off = record only clicks and keys", 324, 68, true, 9);
            lblRecInfo = Lbl(rc, "Nothing recorded yet.", 14, 108, true, 9);

            Card pb = NewCard(pg, "Playback", 0, 220, CW, 160);
            RowLbl(pb, "Speed", 14, 42); nbSpeed = NB(pb, "speed", 80, 42, 120, 10, 1000, 100, "%"); nbSpeed.Step = 10;
            RowLbl(pb, "Loops", 14, 81); nbLoops = NB(pb, "loops", 80, 81, 120, 1, 1000000, 1, "times");
            tgLoopForever = TG(pb, "loopForever", 80, 120, false); RowLbl(pb, "Loop forever", 130, 120);
            tgLoopForever.CheckedChanged += delegate { UpdateUi(); };
            heroPlay = Hero(pb, "Play recording", "\uE768", 300, 42, 246, 56);
            heroPlay.Click += delegate { TogglePlayback(); };

            FButton save = BTN(pg, "Save recording", "\uE74E", PX, 392, 170, 34);
            FButton open = BTN(pg, "Open recording", "\uE8E5", PX + 180, 392, 170, 34);
            save.Click += delegate { SaveRecording(); };
            open.Click += delegate { LoadRecording(); };
            Label hint = Lbl(pg, "Your hotkeys are never recorded, so stopping with the hotkey leaves a clean recording.", PX, 438, true, 9);
            hint.MaximumSize = new Size(S(CW), 0);
        }

        void BuildSettings()
        {
            Page pg = NewPage("Settings", "Appearance, behavior, hotkeys and saved profiles.");
            Card ap = NewCard(pg, "Appearance", 0, 72, 274, 216);
            for (int i = 0; i < Theme.All.Length; i++)
            {
                ThemeTile tile = Put(ap, new ThemeTile(Theme.All[i]), 14 + (i % 3) * 84, 42 + (i / 3) * 66, 78, 60);
                int idx = i; tile.Click += delegate { themeIndex = idx; ApplyTheme(); SaveSettings(); };
                themeTiles.Add(tile);
            }
            tgSysAccent = TG(ap, null, 14, 174, false); tgSysAccent.Checked = true;
            RowLbl(ap, "Use Windows accent color", 64, 174);
            tgSysAccent.CheckedChanged += delegate { ApplyTheme(); SaveSettings(); };

            Card bh = NewCard(pg, "Behavior", 0, 300, 274, 186);
            RowLbl(bh, "Always on top", 14, 40); tgTop = TG(bh, null, 180, 40);
            RowLbl(bh, "Minimize to tray", 14, 74); tgTray = TG(bh, null, 180, 74);
            RowLbl(bh, "Sounds", 14, 108); tgSound = TG(bh, null, 180, 108);
            RowLbl(bh, "Start delay", 14, 144); nbDelay = NB(bh, null, 160, 144, 100, 0, 60, 0, "sec");

            Card up = NewCard(pg, "Updates", 0, 498, 274, 82);
            Label ver = Lbl(up, "Version " + AppInfo.Version, 0, 14, true, 9); ver.Left = S(260) - ver.PreferredWidth;
            tgAutoUpdate = TG(up, null, 14, 40, false); tgAutoUpdate.Checked = true;
            RowLbl(up, "Auto-check", 64, 40);
            btnCheckUpdates = BTN(up, "Check now", "\uE895", 156, 40, 104, 32);
            btnCheckUpdates.Click += delegate { CheckForUpdates(true); };
            tgAutoUpdate.CheckedChanged += delegate { SaveSettings(); };
            tgTop.CheckedChanged += delegate { TopMost = tgTop.Checked; SaveSettings(); };
            tgTray.CheckedChanged += delegate { if (tray != null) tray.Visible = tgTray.Checked; SaveSettings(); };
            tgSound.CheckedChanged += delegate { Sfx.On = tgSound.Checked; SaveSettings(); };
            nbDelay.ValueChanged += delegate { SaveSettings(); };

            Card hk = NewCard(pg, "Hotkeys", 286, 72, 274, 212);
            for (int i = 0; i < 4; i++)
            {
                RowLbl(hk, hotkeyNames[i], 14, 40 + i * 36);
                FButton b = BTN(hk, hotkeys[i].ToString(), null, 160, 40 + i * 36, 100, 32); b.Bold = true; int idx = i;
                b.Click += delegate { CancelCaptures(); rebindingIndex = idx; b.Toggled = true; b.Text = "Press a key..."; };
                hotkeyButtons[i] = b;
            }
            Lbl(hk, "Hotkeys work while you are in other apps.", 14, 186, true, 8.5f);

            Card pf = NewCard(pg, "Profiles", 286, 296, 274, 284);
            listProfiles = Put(pf, new ItemList(), 14, 42, 246, 150);
            listProfiles.EmptyGlyph = "\uE8F1"; listProfiles.EmptyTitle = "No profiles yet"; listProfiles.EmptyText = "Save your current setup below.";
            listProfiles.SelectedIndexChanged += delegate { if (listProfiles.SelectedIndex >= 0) tbProfile.Text = listProfiles.Items[listProfiles.SelectedIndex]; };
            listProfiles.ItemActivated += delegate { LoadSelectedProfile(); };
            tbProfile = Put(pf, new InputBox(), 14, 202, 164, 32); tbProfile.Text = "My profile";
            FButton ps = BTN(pf, "Save", "\uE74E", 184, 202, 76, 32, Kind.Accent);
            FButton pl = BTN(pf, "Load", "\uE8E5", 14, 242, 120, 32);
            FButton pd = BTN(pf, "Delete", "\uE74D", 140, 242, 120, 32);
            ps.Click += delegate { SaveNamedProfile(tbProfile.Text); };
            pl.Click += delegate { LoadSelectedProfile(); };
            pd.Click += delegate { DeleteSelectedProfile(); };
        }

        // ============================================================ theme
        void ApplyTheme()
        {
            Theme baseT = Theme.All[themeIndex];
            Color? sys = tgSysAccent.Checked && baseT.AllowSystemAccent ? Ui.SystemAccent(baseT.Dark) : null;
            Ui.T = sys.HasValue ? baseT.WithAccent(sys.Value) : baseT;
            BackColor = Ui.T.Back; host.BackColor = Ui.T.Layer;
            for (int i = 0; i < themeTiles.Count; i++) themeTiles[i].Selected = i == themeIndex;
            ThemeTree(this);
            ApplyTitleBar();
            Invalidate(true);
        }

        void ThemeTree(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Card) c.BackColor = Ui.T.Card;
                else if (c is Page) c.BackColor = Ui.T.Layer;
                else if (c is Label) c.ForeColor = (c.Tag as string) == "sub" ? Ui.T.Sub : Ui.T.Text;
                IThemed th = c as IThemed; if (th != null) th.Retheme();
                if (!(c is InputBox)) ThemeTree(c);
            }
        }

        void ApplyTitleBar() { if (IsHandleCreated) Ui.StyleTitleBar(this); }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e); ActiveControl = null;
            if (JustUpdated) toast.Show("\uE73E", "Updated to version " + AppInfo.Version);
            if (tgAutoUpdate.Checked)
            {
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer(); t.Interval = 3000;
                t.Tick += delegate { t.Stop(); t.Dispose(); CheckForUpdates(false); };
                t.Start();
            }
        }

        // ============================================================ updates
        public void PreviewUpdate() // dev aid: shows the update screen with sample data
        {
            Updater.Release r = new Updater.Release(); r.Tag = "v9.9.9"; r.Version = new Version(9, 9, 9); r.Sha256 = "preview";
            r.Url = "https://example.invalid/ProClicker.exe";
            r.Notes = "## What's new\n- Faster clicking at high speeds\n- New **Midnight** theme\n- Fixed hotkeys not saving\n\n## Fixes\n- Window remembers its size";
            new UpdateDialog(r, delegate { }).ShowDialog(this);
        }
        public bool JustUpdated;
        bool checkingUpdates;

        void CheckForUpdates(bool manual)
        {
            if (checkingUpdates) return;
            checkingUpdates = true; btnCheckUpdates.Text = "Checking..."; btnCheckUpdates.Enabled = false;
            ThreadPool.QueueUserWorkItem(delegate
            {
                Updater.Release r = null; string err = null;
                try { r = Updater.GetLatest(); } catch (Exception ex) { err = ex.Message; }
                try { BeginInvoke((MethodInvoker)delegate { UpdateCheckDone(r, err, manual); }); } catch { }
            });
        }

        void UpdateCheckDone(Updater.Release r, string err, bool manual)
        {
            checkingUpdates = false; btnCheckUpdates.Text = "Check now"; btnCheckUpdates.Enabled = true;
            if (err != null) { if (manual) toast.Show("\uE7BA", "Couldn't check for updates: " + err); return; }
            if (r == null || r.Version <= Updater.Current)
            {
                if (manual) toast.Show("\uE73E", "You have the latest version (" + AppInfo.Version + ")");
                return;
            }
            if (!manual && r.Tag == skippedVersion) return;
            if (!manual && (clicking || keyPressing || isPlaying || isRecording))
            {
                toast.Show("\uE896", "Version " + r.Version + " is available. Install it from Settings.");
                return;
            }
            using (UpdateDialog d = new UpdateDialog(r, delegate (string tag) { skippedVersion = tag; SaveSettings(); }))
            {
                d.Icon = Icon;
                if (d.ShowDialog(this) == DialogResult.OK) Close(); // the new version is already starting
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBar(); RegisterHotkeys();
        }

        // ============================================================ hotkeys
        void RegisterHotkeys()
        {
            for (int i = 0; i < 4; i++)
            {
                Native.UnregisterHotKey(Handle, 100 + i);
                if (!Native.RegisterHotKey(Handle, 100 + i, 0x4000, (uint)hotkeys[i]))
                    toast.Show("\uE7BA", hotkeys[i] + " is already used by another app. Pick a different hotkey in Settings.");
                if (hotkeyButtons[i] != null) { hotkeyButtons[i].Text = hotkeys[i].ToString(); hotkeyButtons[i].Toggled = false; hotkeyButtons[i].Invalidate(); }
            }
            UpdateUi();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                int id = m.WParam.ToInt32() - 100;
                if (id == 0) ToggleClicker();
                else if (id == 1) ToggleKeyPresser();
                else if (id == 2) ToggleRecording();
                else if (id == 3) TogglePlayback();
            }
            base.WndProc(ref m);
        }

        void CancelCaptures()
        {
            if (rebindingIndex >= 0) { rebindingIndex = -1; RegisterHotkeys(); }
            if (capturingPressKey) { capturingPressKey = false; keycap.Toggled = false; keycap.Text = KeyName(pressKey); }
        }

        static string KeyName(int vk)
        {
            Keys k = (Keys)vk;
            switch (k)
            {
                case Keys.Return: return "Enter";
                case Keys.Back: return "Backspace";
                case Keys.Next: return "Page Down";
                case Keys.Prior: return "Page Up";
                case Keys.Capital: return "Caps Lock";
                case Keys.LButton: return "Left mouse";
            }
            string s = k.ToString();
            if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s.Substring(1);
            if (s.StartsWith("Oem")) return s.Substring(3);
            return s;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool modifier = key == Keys.ControlKey || key == Keys.ShiftKey || key == Keys.Menu;
            if (rebindingIndex >= 0)
            {
                if (modifier) return true;
                if (key != Keys.Escape)
                {
                    for (int i = 0; i < 4; i++) if (i != rebindingIndex && hotkeys[i] == key) hotkeys[i] = hotkeys[rebindingIndex];
                    hotkeys[rebindingIndex] = key;
                    toast.Show("\uE765", hotkeyNames[rebindingIndex] + " hotkey set to " + KeyName((int)key));
                }
                rebindingIndex = -1; RegisterHotkeys(); SaveSettings(); return true;
            }
            if (capturingPressKey)
            {
                if (modifier) return true;
                if (key != Keys.Escape) pressKey = (int)key;
                capturingPressKey = false; keycap.Toggled = false; keycap.Text = KeyName(pressKey); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        bool DoPick(out Point p)
        {
            Hide();
            bool ok = PickOverlay.Pick(out p);
            Show(); Activate(); return ok;
        }

        // ============================================================ countdown
        void WithDelay(Action a)
        {
            if (countdown.Enabled) { countdown.Stop(); toast.Show("\uE711", "Start cancelled"); return; }
            int d = (int)nbDelay.Value;
            if (d <= 0) { a(); return; }
            countLeft = d; countAction = a; countdown.Start();
            toast.Show("\uE916", "Starting in " + d + "...");
        }
        void CountdownTick(object s, EventArgs e)
        {
            countLeft--;
            if (countLeft > 0) { toast.Show("\uE916", "Starting in " + countLeft + "..."); return; }
            countdown.Stop(); toast.Dismiss(); countAction();
        }

        // ============================================================ clicker engine
        int IntervalMs()
        {
            return (int)Math.Min(int.MaxValue, nbH.Value * 3600000 + nbM.Value * 60000 + nbS.Value * 1000 + nbMs.Value);
        }

        void ToggleClicker() { if (clicking) StopClicking(); else WithDelay(StartClicking); }

        void StartClicking()
        {
            if (clicking || isPlaying) return;
            if (segPos.SelectedIndex == 2 && points.Count == 0)
            {
                ShowPage(1); toast.Show("\uE7BA", "Add at least one click point first"); return;
            }
            int interval = IntervalMs(), offset = tgOffset.Checked ? (int)nbOffset.Value : 0;
            int button = segBtn.SelectedIndex, perTick = segType.SelectedIndex + 1, hold = (int)nbHold.Value;
            long repeat = segRepeat.SelectedIndex == 0 ? long.MaxValue : nbRepeat.Value;
            int mode = segPos.SelectedIndex;
            Point fix = new Point((int)nbX.Value, (int)nbY.Value);
            Point[] pts = points.ToArray();
            clicking = true; sessionClicks = 0; wakeEvent.Reset(); Sfx.Play(true); UpdateUi();

            Thread th = new Thread(delegate ()
            {
                Random rnd = new Random(); long done = 0; int pi = 0;
                while (clicking)
                {
                    if (mode == 1) Native.SetCursorPos(fix.X, fix.Y);
                    else if (mode == 2) { Native.SetCursorPos(pts[pi].X, pts[pi].Y); pi = (pi + 1) % pts.Length; }
                    for (int k = 0; k < perTick && clicking; k++)
                    {
                        Native.MouseButton(button, true);
                        if (hold > 0) wakeEvent.WaitOne(hold);
                        Native.MouseButton(button, false);
                        Interlocked.Increment(ref totalClicks); Interlocked.Increment(ref sessionClicks);
                        if (k < perTick - 1) Thread.Sleep(15);
                    }
                    done++;
                    if (done >= repeat) break;
                    int wait = interval + (offset > 0 ? rnd.Next(-offset, offset + 1) : 0);
                    PreciseWait(Math.Max(1, wait));
                }
                bool wasRunning = clicking; clicking = false;
                try { BeginInvoke((MethodInvoker)delegate { if (wasRunning) Sfx.Play(false); UpdateUi(); }); } catch { }
            });
            th.IsBackground = true; th.Start();
        }

        void PreciseWait(int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            if (ms > 3 && wakeEvent.WaitOne(ms - 2)) return; // sleep most of it, spin only the last ~2ms
            while (sw.ElapsedMilliseconds < ms && clicking) Thread.Sleep(0);
        }

        void StopClicking() { if (!clicking) return; clicking = false; wakeEvent.Set(); Sfx.Play(false); UpdateUi(); }

        // ============================================================ key presser
        void ToggleKeyPresser()
        {
            if (keyPressing) { keyPressing = false; Sfx.Play(false); UpdateUi(); return; }
            WithDelay(StartKeyPresser);
        }

        void StartKeyPresser()
        {
            if (keyPressing) return;
            int vk = pressKey, interval = (int)nbKInt.Value, hold = (int)nbKHold.Value;
            long repeat = segKRep.SelectedIndex == 0 ? long.MaxValue : nbKRep.Value;
            bool ctrl = chipCtrl.Toggled, shift = chipShift.Toggled, alt = chipAlt.Toggled;
            keyPressing = true; Sfx.Play(true); UpdateUi();
            Thread th = new Thread(delegate ()
            {
                long done = 0;
                while (keyPressing && done < repeat)
                {
                    if (ctrl) Native.Key(0x11, true);
                    if (shift) Native.Key(0x10, true);
                    if (alt) Native.Key(0x12, true);
                    Native.Key(vk, true); if (hold > 0) Thread.Sleep(hold); Native.Key(vk, false);
                    if (alt) Native.Key(0x12, false);
                    if (shift) Native.Key(0x10, false);
                    if (ctrl) Native.Key(0x11, false);
                    Interlocked.Increment(ref totalKeys); done++;
                    Stopwatch sw = Stopwatch.StartNew();
                    while (keyPressing && sw.ElapsedMilliseconds < interval) Thread.Sleep(Math.Min(10, interval));
                }
                keyPressing = false;
                try { BeginInvoke((MethodInvoker)UpdateUi); } catch { }
            });
            th.IsBackground = true; th.Start();
        }

        // ============================================================ recorder
        // hooks are only installed while recording, so normal input is never slowed down by the app
        void RemoveHooks()
        {
            if (mouseHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
            if (keyHook != IntPtr.Zero) { Native.UnhookWindowsHookEx(keyHook); keyHook = IntPtr.Zero; }
        }

        void InstallHooks()
        {
            RemoveHooks();
            mouseProc = MouseHook; keyProc = KeyHook;
            IntPtr mod = Native.GetModuleHandle(null);
            mouseHook = Native.SetWindowsHookEx(14, mouseProc, mod, 0);
            keyHook = Native.SetWindowsHookEx(13, keyProc, mod, 0);
        }

        void AddEvent(int type, int x, int y, int data)
        {
            RecEvent e = new RecEvent(); e.T = recWatch.ElapsedMilliseconds; e.Type = type; e.X = x; e.Y = y; e.Data = data;
            recording.Add(e);
        }

        IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && isRecording)
            {
                Native.MSLLHOOKSTRUCT m = (Native.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.MSLLHOOKSTRUCT));
                if ((m.flags & 1) == 0)
                {
                    switch (wParam.ToInt32())
                    {
                        case 0x200:
                            if (tgMoves.Checked && recWatch.ElapsedMilliseconds - lastMoveT >= 8) { lastMoveT = recWatch.ElapsedMilliseconds; AddEvent(0, m.x, m.y, 0); }
                            break;
                        case 0x201: AddEvent(1, m.x, m.y, 0); break;
                        case 0x202: AddEvent(2, m.x, m.y, 0); break;
                        case 0x204: AddEvent(1, m.x, m.y, 1); break;
                        case 0x205: AddEvent(2, m.x, m.y, 1); break;
                        case 0x207: AddEvent(1, m.x, m.y, 2); break;
                        case 0x208: AddEvent(2, m.x, m.y, 2); break;
                        case 0x20A: AddEvent(3, m.x, m.y, (short)((m.mouseData >> 16) & 0xFFFF)); break;
                    }
                }
            }
            return Native.CallNextHookEx(mouseHook, nCode, wParam, lParam);
        }

        IntPtr KeyHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && isRecording)
            {
                Native.KBDLLHOOKSTRUCT k = (Native.KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(Native.KBDLLHOOKSTRUCT));
                bool isHotkey = false; foreach (Keys h in hotkeys) if ((int)h == k.vkCode) isHotkey = true;
                if ((k.flags & 0x10) == 0 && !isHotkey)
                {
                    int msg = wParam.ToInt32();
                    if (msg == 0x100 || msg == 0x104) AddEvent(4, 0, 0, k.vkCode);
                    else if (msg == 0x101 || msg == 0x105) AddEvent(5, 0, 0, k.vkCode);
                }
            }
            return Native.CallNextHookEx(keyHook, nCode, wParam, lParam);
        }

        void ToggleRecording()
        {
            if (isPlaying) return;
            if (isRecording)
            {
                isRecording = false; RemoveHooks();
                // drop the click on our own Stop button if that was used to stop
                int n = recording.Count;
                if (n >= 2 && recording[n - 1].Type == 2 && Bounds.Contains(recording[n - 1].X, recording[n - 1].Y))
                {
                    recording.RemoveAt(n - 1);
                    if (recording.Count > 0 && recording[recording.Count - 1].Type == 1) recording.RemoveAt(recording.Count - 1);
                }
                Sfx.Play(false);
                toast.Show("\uE73E", "Recorded " + recording.Count + " events");
            }
            else { recording.Clear(); lastMoveT = -100; recWatch = Stopwatch.StartNew(); isRecording = true; InstallHooks(); Sfx.Play(true); }
            UpdateUi();
        }

        void TogglePlayback()
        {
            if (isPlaying) { isPlaying = false; Sfx.Play(false); UpdateUi(); return; }
            if (isRecording || clicking) return;
            if (recording.Count == 0) { ShowPage(3); toast.Show("\uE7BA", "Record something first"); return; }
            RecEvent[] evs = recording.ToArray();
            double speed = nbSpeed.Value / 100.0; long loops = tgLoopForever.Checked ? long.MaxValue : nbLoops.Value;
            isPlaying = true; Sfx.Play(true); UpdateUi();
            Thread th = new Thread(delegate ()
            {
                for (long loop = 0; loop < loops && isPlaying; loop++)
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    foreach (RecEvent e in evs)
                    {
                        long due = (long)(e.T / speed);
                        while (isPlaying && sw.ElapsedMilliseconds < due) Thread.Sleep((int)Math.Max(0, Math.Min(5, due - sw.ElapsedMilliseconds - 1)));
                        if (!isPlaying) break;
                        switch (e.Type)
                        {
                            case 0: Native.SetCursorPos(e.X, e.Y); break;
                            case 1: Native.SetCursorPos(e.X, e.Y); Native.MouseButton(e.Data, true); Interlocked.Increment(ref totalClicks); break;
                            case 2: Native.SetCursorPos(e.X, e.Y); Native.MouseButton(e.Data, false); break;
                            case 3: Native.Wheel(e.Data); break;
                            case 4: Native.Key(e.Data, true); break;
                            case 5: Native.Key(e.Data, false); break;
                        }
                    }
                    Thread.Sleep(30);
                }
                isPlaying = false;
                try { BeginInvoke((MethodInvoker)UpdateUi); } catch { }
            });
            th.IsBackground = true; th.Start();
        }

        void SaveRecording()
        {
            if (recording.Count == 0) { toast.Show("\uE7BA", "Nothing to save yet"); return; }
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Filter = "ProClicker recording (*.pcr)|*.pcr"; Directory.CreateDirectory(DataDir); d.InitialDirectory = DataDir;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                StringBuilder sb = new StringBuilder();
                foreach (RecEvent e in recording) sb.AppendLine(e.T + "," + e.Type + "," + e.X + "," + e.Y + "," + e.Data);
                File.WriteAllText(d.FileName, sb.ToString());
                toast.Show("\uE73E", "Saved " + Path.GetFileName(d.FileName));
            }
        }

        void LoadRecording()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Filter = "ProClicker recording (*.pcr)|*.pcr"; d.InitialDirectory = DataDir;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    List<RecEvent> list = new List<RecEvent>();
                    foreach (string line in File.ReadAllLines(d.FileName))
                    {
                        string[] p = line.Split(','); if (p.Length < 5) continue;
                        RecEvent e = new RecEvent(); e.T = long.Parse(p[0]); e.Type = int.Parse(p[1]); e.X = int.Parse(p[2]); e.Y = int.Parse(p[3]); e.Data = int.Parse(p[4]);
                        list.Add(e);
                    }
                    recording.Clear(); recording.AddRange(list); UpdateUi();
                    toast.Show("\uE73E", "Opened " + Path.GetFileName(d.FileName));
                }
                catch (Exception ex) { toast.Show("\uE7BA", "Could not open: " + ex.Message); }
            }
        }

        // ============================================================ UI state
        void UpdateUi()
        {
            if (heroClick == null || heroRec == null || heroKey == null) return;
            string k0 = KeyName((int)hotkeys[0]), k1 = KeyName((int)hotkeys[1]), k2 = KeyName((int)hotkeys[2]), k3 = KeyName((int)hotkeys[3]);

            heroClick.IsDanger = clicking; heroClick.Text = clicking ? "Stop clicking" : "Start clicking";
            heroClick.Glyph = clicking ? "\uE71A" : "\uE768"; heroClick.SubText = "or press " + k0 + " anywhere"; heroClick.Invalidate();
            heroKey.IsDanger = keyPressing; heroKey.Text = keyPressing ? "Stop key presser" : "Start key presser";
            heroKey.Glyph = keyPressing ? "\uE71A" : "\uE768"; heroKey.SubText = "or press " + k1 + " anywhere"; heroKey.Invalidate();
            heroRec.IsDanger = isRecording; heroRec.Text = isRecording ? "Stop recording" : "Start recording";
            heroRec.Glyph = isRecording ? "\uE71A" : "\uE7C8"; heroRec.SubText = "or press " + k2; heroRec.Enabled = !isPlaying; heroRec.Invalidate();
            heroPlay.IsDanger = isPlaying; heroPlay.Text = isPlaying ? "Stop playback" : "Play recording";
            heroPlay.Glyph = isPlaying ? "\uE71A" : "\uE768"; heroPlay.SubText = "or press " + k3; heroPlay.Enabled = !isRecording; heroPlay.Invalidate();

            // clicker options
            bool idle = !clicking;
            nbRepeat.Enabled = segRepeat.SelectedIndex == 1;
            nbOffset.Enabled = tgOffset.Checked;
            nbX.Enabled = nbY.Enabled = segPos.SelectedIndex == 1;
            nbKRep.Enabled = segKRep.SelectedIndex == 1;
            nbLoops.Enabled = !tgLoopForever.Checked;
            lblRepeatHint.Text = segRepeat.SelectedIndex == 0 ? "Keeps clicking until you press stop." : "Stops by itself after " + nbRepeat.Value.ToString("N0") + " clicks.";
            int interval = Math.Max(1, IntervalMs());
            double est = 1000.0 / interval * (segType.SelectedIndex + 1);
            lblEstimate.Text = "\u2248 " + (est >= 10 ? est.ToString("0") : est.ToString("0.##")) + " clicks/sec";

            nav.Items[0].Busy = clicking; nav.Items[2].Busy = keyPressing; nav.Items[3].Busy = isRecording || isPlaying;
            if (!isRecording)
                lblRecInfo.Text = recording.Count == 0 ? "Nothing recorded yet." :
                    "Ready to play: " + recording.Count + " events, " + (recording[recording.Count - 1].T / 1000.0).ToString("0.0") + " seconds long.";
            if (trayToggle != null) trayToggle.Text = clicking ? "Stop clicking" : "Start clicking";
            UpdateStatus();
        }

        void UpdateStatus()
        {
            double secs = cpsWatch.Elapsed.TotalSeconds;
            if (secs >= 0.4)
            {
                long now = Interlocked.Read(ref totalClicks);
                double inst = (now - lastCount) / secs; cps = inst == 0 ? 0 : cps * 0.4 + inst * 0.6;
                lastCount = now; cpsWatch.Restart();
            }
            tileCps.Set(cps.ToString(cps >= 100 ? "0" : "0.0"));
            tileRun.Set(Interlocked.Read(ref sessionClicks).ToString("N0"));

            string title, detail; bool active = true;
            if (clicking) { title = "Clicking"; detail = cps.ToString("0.0") + " CPS \u2022 " + Interlocked.Read(ref sessionClicks).ToString("N0") + " clicks"; }
            else if (isRecording) { title = "Recording"; detail = recording.Count + " events"; }
            else if (isPlaying) { title = "Playing"; detail = "Press " + KeyName((int)hotkeys[3]) + " to stop"; }
            else if (keyPressing) { title = "Pressing keys"; detail = Interlocked.Read(ref totalKeys).ToString("N0") + " presses"; }
            else if (countdown.Enabled) { title = "Starting..."; detail = "in " + countLeft + " sec"; }
            else { title = "Idle"; detail = "Press " + KeyName((int)hotkeys[0]) + " to start clicking"; active = false; }
            if (nav.StatusTitle != title || nav.StatusDetail != detail || nav.StatusActive != active)
            { nav.StatusTitle = title; nav.StatusDetail = detail; nav.StatusActive = active; nav.Invalidate(); }
            string caption = (active ? title + " \u2022 " : "") + "ProClicker";
            if (Text != caption) { Text = caption; if (tray != null) tray.Text = caption.Length > 60 ? "ProClicker" : caption; }
            if (isRecording) lblRecInfo.Text = "Recording... " + recording.Count + " events so far. Press " + KeyName((int)hotkeys[2]) + " to stop.";
            
        }

        // ============================================================ tray
        void BuildTray()
        {
            tray = new NotifyIcon(); tray.Icon = Icon ?? SystemIcons.Application; tray.Text = "ProClicker";
            ContextMenuStrip m = new ContextMenuStrip();
            m.Items.Add("Open ProClicker", null, delegate { RestoreFromTray(); });
            trayToggle = new ToolStripMenuItem("Start clicking", null, delegate { ToggleClicker(); });
            m.Items.Add(trayToggle);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Exit", null, delegate { Close(); });
            tray.ContextMenuStrip = m;
            tray.DoubleClick += delegate { RestoreFromTray(); };
            tray.Visible = tgTray.Checked;
        }
        void RestoreFromTray() { Show(); WindowState = FormWindowState.Normal; Activate(); }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && tgTray != null && tgTray.Checked) Hide();
        }

        // ============================================================ persistence
        void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(DataDir);
                File.WriteAllLines(SettingsFile, new[] {
                    "theme=" + themeIndex, "systemAccent=" + tgSysAccent.Checked, "topmost=" + tgTop.Checked,
                    "tray=" + tgTray.Checked, "autoUpdate=" + tgAutoUpdate.Checked, "skipVersion=" + skippedVersion, "navCollapsed=" + navCollapsed, "sound=" + tgSound.Checked, "delay=" + nbDelay.Value,
                    "hotkeys=" + (int)hotkeys[0] + "," + (int)hotkeys[1] + "," + (int)hotkeys[2] + "," + (int)hotkeys[3] });
            }
            catch { }
        }

        void LoadSettings()
        {
            try
            {
                if (!File.Exists(SettingsFile)) return;
                foreach (string line in File.ReadAllLines(SettingsFile))
                {
                    int eq = line.IndexOf('='); if (eq < 0) continue;
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                    if (k == "theme") { int t = int.Parse(v); if (t >= 0 && t < Theme.All.Length) themeIndex = t; }
                    else if (k == "systemAccent") tgSysAccent.Checked = bool.Parse(v);
                    else if (k == "topmost") tgTop.Checked = bool.Parse(v);
                    else if (k == "tray") tgTray.Checked = bool.Parse(v);
                    else if (k == "navCollapsed") SetNavCollapsed(bool.Parse(v), false);
                    else if (k == "sound") tgSound.Checked = bool.Parse(v);
                    else if (k == "autoUpdate") tgAutoUpdate.Checked = bool.Parse(v);
                    else if (k == "skipVersion") skippedVersion = v;
                    else if (k == "delay") nbDelay.Value = long.Parse(v);
                    else if (k == "hotkeys") { string[] p = v.Split(','); for (int i = 0; i < 4 && i < p.Length; i++) hotkeys[i] = (Keys)int.Parse(p[i]); }
                }
            }
            catch { }
        }

        void RefreshProfiles()
        {
            listProfiles.Items.Clear();
            try
            {
                Directory.CreateDirectory(ProfileDir);
                foreach (string f in Directory.GetFiles(ProfileDir, "*.txt")) listProfiles.Items.Add(Path.GetFileNameWithoutExtension(f));
            }
            catch { }
            listProfiles.Changed();
        }

        void SaveNamedProfile(string name)
        {
            name = name.Trim(); foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch.ToString(), "");
            if (name.Length == 0) { toast.Show("\uE7BA", "Type a name for the profile first"); return; }
            Directory.CreateDirectory(ProfileDir);
            if (WriteProfile(Path.Combine(ProfileDir, name + ".txt")))
            {
                RefreshProfiles(); listProfiles.SelectedIndex = listProfiles.Items.IndexOf(name);
                toast.Show("\uE74E", "Saved profile \"" + name + "\"");
            }
        }

        void LoadSelectedProfile()
        {
            int i = listProfiles.SelectedIndex; if (i < 0) { toast.Show("\uE7BA", "Select a profile to load"); return; }
            if (clicking || keyPressing || isPlaying) { toast.Show("\uE7BA", "Stop everything before loading a profile"); return; }
            if (LoadProfileFile(Path.Combine(ProfileDir, listProfiles.Items[i] + ".txt"), false))
                toast.Show("\uE8E5", "Loaded profile \"" + listProfiles.Items[i] + "\"");
        }

        void DeleteSelectedProfile()
        {
            int i = listProfiles.SelectedIndex; if (i < 0) { toast.Show("\uE7BA", "Select a profile to delete"); return; }
            string name = listProfiles.Items[i];
            if (MessageBox.Show(this, "Delete profile \"" + name + "\"?", "ProClicker", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            try { File.Delete(Path.Combine(ProfileDir, name + ".txt")); } catch { }
            RefreshProfiles(); toast.Show("\uE74D", "Deleted \"" + name + "\"");
        }

        bool WriteProfile(string file)
        {
            List<string> lines = new List<string>();
            foreach (KeyValuePair<string, IValue> kv in saved) lines.Add(kv.Key + "=" + kv.Value.Save());
            lines.Add("pressKey=" + pressKey);
            List<string> pts = new List<string>(); foreach (Point p in points) pts.Add(p.X + ":" + p.Y);
            lines.Add("points=" + string.Join(";", pts.ToArray()));
            try { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllLines(file, lines.ToArray()); return true; }
            catch (Exception ex) { toast.Show("\uE7BA", "Could not save: " + ex.Message); return false; }
        }

        bool LoadProfileFile(string file, bool quiet)
        {
            if (!File.Exists(file)) return false;
            try
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    int eq = line.IndexOf('='); if (eq < 0) continue;
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1); IValue c;
                    if (k == "pressKey") { pressKey = int.Parse(v); keycap.Text = KeyName(pressKey); }
                    else if (k == "points")
                    {
                        points.Clear();
                        foreach (string p in v.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                        { string[] xy = p.Split(':'); points.Add(new Point(int.Parse(xy[0]), int.Parse(xy[1]))); }
                        RefreshPoints(-1);
                    }
                    else if (saved.TryGetValue(k, out c)) { try { c.Load(v); } catch { } }
                }
                UpdateUi(); return true;
            }
            catch (Exception ex) { if (!quiet) toast.Show("\uE7BA", "Could not load profile: " + ex.Message); return false; }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            clicking = false; keyPressing = false; isPlaying = false; isRecording = false; wakeEvent.Set(); RemoveHooks();
            for (int i = 0; i < 4; i++) Native.UnregisterHotKey(Handle, 100 + i);
            if (mouseHook != IntPtr.Zero) Native.UnhookWindowsHookEx(mouseHook);
            if (keyHook != IntPtr.Zero) Native.UnhookWindowsHookEx(keyHook);
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            SaveSettings(); WriteProfile(LastSessionFile);
            base.OnFormClosing(e);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            if (args.Length == 3 && args[0] == "--make-icon") { Ui.WriteIcon(args[1], args[2]); return; }
            if (args.Length >= 1 && args[0] == "--updated") Thread.Sleep(800); // let the old version exit and free the hotkeys
            Updater.CleanupOld();
            Ui.Init();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            MainForm form = new MainForm();
            if (args.Length >= 2 && args[0] == "--page") form.StartPage = int.Parse(args[1]); // open on a specific page
            if (args.Length >= 3 && args[2] == "--slim") form.Slim = true;                   // start with the sidebar collapsed
            if (args.Length >= 1 && args[0] == "--updated") form.JustUpdated = true;
            if (args.Length >= 1 && args[0] == "--preview-update") form.Shown += delegate { form.PreviewUpdate(); };
            Application.Run(form);
        }
    }
}
