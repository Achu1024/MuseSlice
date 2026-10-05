using System.Drawing;
using Microsoft.Win32;

namespace QuickNote
{
    internal sealed class Palette
    {
        public Color BarBack;
        public Color Border;
        public Color Text;
        public Color Muted;
        public Color Accent;      // 标题、列表符
        public Color Todo;        // - [ ] 待办
        public Color Selection;
        public Color Error;

        public Font Body;
        public Font HeadingFont;
        public Font Mono;
        public Font Small;
    }

    internal static class Theme
    {
        public static bool IsDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key == null) return true;
                    var v = key.GetValue("AppsUseLightTheme");
                    if (v is int) return (int)v == 0;
                }
            }
            catch { /* 读不到就按深色处理 */ }
            return true;
        }

        public static bool ShouldUseDark(string themeSetting)
        {
            if (themeSetting == "Dark") return true;
            if (themeSetting == "Light") return false;
            return IsDarkMode();
        }

        public static Palette Current(string themeSetting)
        {
            var dark = ShouldUseDark(themeSetting);
            return dark ? Dark() : Light();
        }

        private static Palette Dark()
        {
            return new Palette
            {
                BarBack = Color.FromArgb(242, 28, 28, 32),
                Border = Color.FromArgb(38, 255, 255, 255),
                Text = Color.FromArgb(232, 232, 234),
                Muted = Color.FromArgb(120, 255, 255, 255),
                Accent = Color.FromArgb(124, 176, 255),
                Todo = Color.FromArgb(240, 180, 94),
                Selection = Color.FromArgb(60, 110, 168, 254),
                Error = Color.FromArgb(255, 120, 120),
                Body = new Font("Microsoft YaHei UI", 10.5f),
                HeadingFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Mono = new Font("Consolas", 10f),
                Small = new Font("Microsoft YaHei UI", 8.5f)
            };
        }

        private static Palette Light()
        {
            return new Palette
            {
                BarBack = Color.FromArgb(246, 250, 250, 252),
                Border = Color.FromArgb(30, 0, 0, 0),
                Text = Color.FromArgb(28, 28, 32),
                Muted = Color.FromArgb(130, 0, 0, 0),
                Accent = Color.FromArgb(28, 96, 200),
                Todo = Color.FromArgb(176, 106, 16),
                Selection = Color.FromArgb(50, 60, 130, 230),
                Error = Color.FromArgb(190, 40, 40),
                Body = new Font("Microsoft YaHei UI", 10.5f),
                HeadingFont = new Font("Microsoft YaHei UI", 10.5f, FontStyle.Bold),
                Mono = new Font("Consolas", 10f),
                Small = new Font("Microsoft YaHei UI", 8.5f)
            };
        }
    }
}
