using System;
using System.Drawing;
using Microsoft.Win32;

namespace QuickNote
{
    /// <summary>
    /// 小条的配色与字体。
    /// <para>
    /// 立场（定案 §1）：这不是生产力看板，是一块掉落下来的便签。全界面近乎无彩，
    /// **唯一的颜色来自那两个 emoji 脸**——所以没有强调色、没有待办橙。
    /// 标题用字重区分，列表符是结构不是装饰，行内代码用 ink-soft 加等宽、不加底色
    /// （RichEdit 画不出圆角底，方底更丑）。
    /// </para>
    /// </summary>
    internal sealed class Palette
    {
        public Color Paper;      // 小条底色
        public Color Ink;        // 正文
        public Color InkSoft;    // 次要正文、续行、行内代码
        public Color Muted;      // 键位提示、状态文字、引用行
        public Color Stamp;      // 时间戳——比 muted 还轻，退到背景里
        public Color Hairline;   // 上下两条 chrome 的分隔线
        public Color Selection;  // 选中行底纹（仅用于绘制，可带 alpha）
        public Color Highlight;  // 搜索命中处底纹（仅用于绘制，可带 alpha）
        public Color Error;      // 保存失败与删除确认。唯一被允许的彩色，是特例

        public Font Body;        // 正文
        public Font StampFont;   // 时间戳。界面里唯一使用第二套字体的地方
        public Font Chrome;      // 上下两条辅助信息行
        public Font Heading;     // 标题（加粗，不是换色）
        public Font Mono;        // 行内代码
        public Font Emoji;       // 两个脸。不指定它的话会被正文 CJK 字体带去黑白字形
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
            return ShouldUseDark(themeSetting) ? Dark() : Light();
        }

        // 交给控件属性的颜色必须完全不透明（见接手须知）：WinForms 的 Control.BackColor
        // 收到 alpha < 255 会直接抛 ArgumentException，在构造函数里就把进程打死。
        // 只有纯 GDI+ 绘制的 Selection / Highlight 可以带 alpha。
        private static Palette Light()
        {
            return new Palette
            {
                Paper = Rgb(0xFA, 0xFA, 0xF8),      // 近白微暖，不是奶油色
                Ink = Rgb(0x23, 0x23, 0x26),
                InkSoft = Rgb(0x55, 0x55, 0x5C),
                Muted = Rgb(0x8E, 0x8E, 0x93),
                Stamp = Rgb(0xA9, 0xA9, 0xAE),
                Hairline = Rgb(0xE8, 0xE8, 0xE4),
                Selection = Color.FromArgb(38, 0x23, 0x23, 0x26),
                Highlight = Color.FromArgb(52, 0x23, 0x23, 0x26),
                Error = Rgb(0x9C, 0x3A, 0x34),
                Body = Font(CjkFamilies, 11f, FontStyle.Regular),
                StampFont = Font(NumeralFamilies, 10.5f, FontStyle.Regular),
                Chrome = Font(CjkFamilies, 8.5f, FontStyle.Regular),
                Heading = Font(CjkFamilies, 11f, FontStyle.Bold),
                Mono = Font(MonoFamilies, 10f, FontStyle.Regular),
                Emoji = Font(EmojiFamilies, 11f, FontStyle.Regular)
            };
        }

        private static Palette Dark()
        {
            return new Palette
            {
                Paper = Rgb(0x1A, 0x1A, 0x1C),
                Ink = Rgb(0xE8, 0xE8, 0xE6),
                InkSoft = Rgb(0xB4, 0xB4, 0xB8),
                Muted = Rgb(0x7E, 0x7E, 0x84),
                Stamp = Rgb(0x6E, 0x6E, 0x74),
                Hairline = Rgb(0x2C, 0x2C, 0x30),
                Selection = Color.FromArgb(46, 0xE8, 0xE8, 0xE6),
                Highlight = Color.FromArgb(64, 0xE8, 0xE8, 0xE6),
                Error = Rgb(0xE0, 0x73, 0x6C),
                Body = Font(CjkFamilies, 11f, FontStyle.Regular),
                StampFont = Font(NumeralFamilies, 10.5f, FontStyle.Regular),
                Chrome = Font(CjkFamilies, 8.5f, FontStyle.Regular),
                Heading = Font(CjkFamilies, 11f, FontStyle.Bold),
                Mono = Font(MonoFamilies, 10f, FontStyle.Regular),
                Emoji = Font(EmojiFamilies, 11f, FontStyle.Regular)
            };
        }

        // Noto Sans SC 比雅黑 UI 开阔、字面更匀，不是系统默认脸。
        private static readonly string[] CjkFamilies =
            { "Noto Sans SC", "思源黑体", "Source Han Sans SC", "DengXian", "Microsoft YaHei UI" };

        // Bahnschrift Light 是 DIN 系，数字有技术感，跟中文的人文气质拉开层次。
        private static readonly string[] NumeralFamilies =
            { "Bahnschrift Light", "Bahnschrift", "Consolas" };

        private static readonly string[] MonoFamilies = { "Consolas", "Courier New" };
        private static readonly string[] EmojiFamilies = { "Segoe UI Emoji" };

        private static Color Rgb(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        /// <summary>
        /// 按顺序取第一个装得上的字体。装不上时直接 new Font("不存在", ...) 会静默退回
        /// Microsoft Sans Serif——中文用那个脸很难看，而且没有任何提示。
        /// </summary>
        private static Font Font(string[] families, float size, FontStyle style)
        {
            foreach (var name in families)
            {
                try { return new Font(name, size, style); }
                catch (ArgumentException) { /* 这个家族不存在，试下一个 */ }
            }
            return new Font(FontFamily.GenericSansSerif, size, style);
        }
    }
}
