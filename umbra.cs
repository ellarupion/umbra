// umbra — tray toggle for Windows light/dark theme with a daily schedule.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

static class Umbra
{
    const string ThemeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static readonly string Ini = Path.ChangeExtension(Application.ExecutablePath, ".ini");

    static TimeSpan lightAt = new TimeSpan(7, 0, 0), darkAt = new TimeSpan(19, 0, 0);
    static bool auto = true;
    static bool? lastScheduled, shown;

    static NotifyIcon tray;
    static ToolStripMenuItem autoItem, startupItem;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    [STAThread]
    static void Main()
    {
        bool first;
        using (new Mutex(true, "umbra-theme-toggle", out first))
        {
            if (!first) return;

            var menu = new ContextMenuStrip();
            var toggle = menu.Items.Add("Toggle theme", null, delegate { SetLight(!IsLight()); Refresh(); });
            toggle.Font = new Font(toggle.Font, FontStyle.Bold);
            autoItem = new ToolStripMenuItem("", null, delegate { auto = !auto; Save(); Tick(); });
            startupItem = new ToolStripMenuItem("Start with Windows", null, delegate { SetStartup(!InStartup()); Refresh(); });
            menu.Items.Add(autoItem);
            menu.Items.Add("Edit schedule…", null, delegate { System.Diagnostics.Process.Start("notepad.exe", "\"" + Ini + "\""); });
            menu.Items.Add(startupItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { tray.Visible = false; Application.Exit(); });

            tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) { SetLight(!IsLight()); Refresh(); } };

            var timer = new System.Windows.Forms.Timer { Interval = 30000 };
            timer.Tick += delegate { Tick(); };
            timer.Start();

            Tick();
            Application.Run();
        }
    }

    // Applies the schedule only when crossing a boundary, so manual toggles stick until the next one.
    static void Tick()
    {
        Load();
        if (auto)
        {
            bool want = LightNow();
            if (lastScheduled != want)
            {
                lastScheduled = want;
                if (IsLight() != want) SetLight(want);
            }
        }
        else lastScheduled = null;
        Refresh();
    }

    static bool LightNow()
    {
        var now = DateTime.Now.TimeOfDay;
        return lightAt < darkAt ? now >= lightAt && now < darkAt : now >= lightAt || now < darkAt;
    }

    static bool IsLight()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(ThemeKey))
            return k == null || Convert.ToInt32(k.GetValue("AppsUseLightTheme", 1)) != 0;
    }

    static void SetLight(bool light)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(ThemeKey))
        {
            k.SetValue("AppsUseLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
            k.SetValue("SystemUsesLightTheme", light ? 1 : 0, RegistryValueKind.DWord);
        }
        IntPtr r;
        SendMessageTimeout((IntPtr)0xFFFF, 0x1A, IntPtr.Zero, "ImmersiveColorSet", 2, 100, out r);
    }

    static void Refresh()
    {
        bool light = IsLight();
        if (shown != light)
        {
            shown = light;
            var old = tray.Icon;
            tray.Icon = MakeIcon(light);
            if (old != null) old.Dispose();
        }
        tray.Text = "umbra — " + (light ? "light" : "dark");
        autoItem.Text = string.Format("Auto ({0:hh\\:mm} light, {1:hh\\:mm} dark)", lightAt, darkAt);
        autoItem.Checked = auto;
        startupItem.Checked = InStartup();
    }

    // ◐ drawn in the taskbar's contrast color.
    static Icon MakeIcon(bool light)
    {
        int s = SystemInformation.SmallIconSize.Width;
        using (var bmp = new Bitmap(s, s))
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var c = light ? Color.Black : Color.White;
            float pad = s / 8f, d = s - 2 * pad - 1;
            using (var pen = new Pen(c, Math.Max(1f, s / 12f))) g.DrawEllipse(pen, pad, pad, d, d);
            using (var brush = new SolidBrush(c)) g.FillPie(brush, pad, pad, d, d, 90, 180);
            IntPtr h = bmp.GetHicon();
            var icon = (Icon)Icon.FromHandle(h).Clone();
            DestroyIcon(h);
            return icon;
        }
    }

    static void Load()
    {
        if (!File.Exists(Ini)) { Save(); return; }
        foreach (var line in File.ReadAllLines(Ini))
        {
            var kv = line.Split(new[] { '=' }, 2);
            if (kv.Length != 2) continue;
            string key = kv[0].Trim().ToLowerInvariant(), val = kv[1].Trim();
            TimeSpan t;
            if (key == "light" && TimeSpan.TryParse(val, out t)) lightAt = t;
            else if (key == "dark" && TimeSpan.TryParse(val, out t)) darkAt = t;
            else if (key == "auto") auto = val == "1" || val.ToLowerInvariant() == "true";
        }
    }

    static void Save()
    {
        File.WriteAllText(Ini, string.Format("light={0:hh\\:mm}\r\ndark={1:hh\\:mm}\r\nauto={2}\r\n", lightAt, darkAt, auto ? 1 : 0));
    }

    static bool InStartup()
    {
        using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
            return k != null && k.GetValue("umbra") != null;
    }

    static void SetStartup(bool on)
    {
        using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            if (on) k.SetValue("umbra", "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue("umbra", false);
    }
}
