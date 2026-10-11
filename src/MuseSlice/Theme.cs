using System;
using System.Drawing;
using Microsoft.Win32;

namespace MuseSlice
{
    /// <summary>
    /// 小条的配色与字体。
    /// <para>
    /// 立场（定案 §1）：这不是生产力看板，是一块掉落下来的便签。全界面近乎无彩，
    /// 标题用字重区分，列表符是结构不是装饰，行内代码用 ink-soft 加等宽、不加底色
    /// （RichEdit 画不出圆角底，方底更丑）。
    /// </para>
    /// <para>
    /// 定案里原先写着「唯一的彩色是那两个 emoji 脸、没有强调色」。加了 MuseSlice
    /// 标志之后这句话不再成立：品牌靛蓝（<see cref="BrandAccent"/>）是界面里第一个、
    /// 也是唯一一个强调色。它只出现在标志上，不扩散到按钮、选中态和提示文案——
    /// 一旦扩散，这块便签就变成看板了，那正是定案当初要躲开的东西。
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
        public Color Brand;      // 标志的主形。深色主题下必须翻转，否则看不见
        public Color BrandAccent;// 标志的切片。品牌靛蓝，界面里唯一的强调色

        public Font Body;        // 正文
        public Font StampFont;   // 时间戳。界面里唯一使用第二套字体的地方
        public Font Chrome;      // 上下两条辅助信息行
        public Font Heading;     // 标题（加粗，不是换色）
        public Font Mono;        // 行内代码
        public Font Icon;        // 勾选框的图标字形（Windows 自带的图标字体）

        // 搜索结果列表专用：比正文小一档，为的是在小条里多放下几条。
        // 三个必须一起调——SearchResultsView 的行高取「正文高」与「元信息高」的较大者，
        // 只调小 ResultBody 会被 ResultMeta 兜住，行高根本不降。
        public Font ResultBody;
        public Font ResultMeta;
        public Font ResultIcon;
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
                Brand = Rgb(0x17, 0x1B, 0x2A),
                BrandAccent = Rgb(0x5B, 0x43, 0xF5),
                Body = Font(CjkFamilies, 11f, FontStyle.Regular),
                StampFont = Font(NumeralFamilies, 10.5f, FontStyle.Regular),
                Chrome = Font(CjkFamilies, 8.5f, FontStyle.Regular),
                Heading = Font(CjkFamilies, 11f, FontStyle.Bold),
                Mono = Font(MonoFamilies, 10f, FontStyle.Regular),
                Icon = Font(IconFamilies, 11f, FontStyle.Regular),
                ResultBody = Font(CjkFamilies, 10f, FontStyle.Regular),
                ResultMeta = Font(NumeralFamilies, 9f, FontStyle.Regular),
                ResultIcon = Font(IconFamilies, 10f, FontStyle.Regular),

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
                // 深色底上炭黑等于隐形，主形翻成近白；靛蓝提亮一档，否则压在深灰上发闷。
                Brand = Rgb(0xE8, 0xE8, 0xE6),
                BrandAccent = Rgb(0x7C, 0x6B, 0xFF),
                Body = Font(CjkFamilies, 11f, FontStyle.Regular),
                StampFont = Font(NumeralFamilies, 10.5f, FontStyle.Regular),
                Chrome = Font(CjkFamilies, 8.5f, FontStyle.Regular),
                Heading = Font(CjkFamilies, 11f, FontStyle.Bold),
                Mono = Font(MonoFamilies, 10f, FontStyle.Regular),
                Icon = Font(IconFamilies, 11f, FontStyle.Regular),
                ResultBody = Font(CjkFamilies, 10f, FontStyle.Regular),
                ResultMeta = Font(NumeralFamilies, 9f, FontStyle.Regular),
                ResultIcon = Font(IconFamilies, 10f, FontStyle.Regular),

            };
        }

        // Noto Sans SC 比雅黑 UI 开阔、字面更匀，不是系统默认脸。
        private static readonly string[] CjkFamilies =
            { "Noto Sans SC", "思源黑体", "Source Han Sans SC", "DengXian", "Microsoft YaHei UI" };

        // Bahnschrift Light 是 DIN 系，数字有技术感，跟中文的人文气质拉开层次。
        private static readonly string[] NumeralFamilies =
            { "Bahnschrift Light", "Bahnschrift", "Consolas" };

        // Windows 自带的图标字体：Win11 是 Segoe Fluent Icons，Win10 是 Segoe MDL2 Assets。
        // 勾选框的图标字形从这里取——一套专业设计的单色矢量图标，零第三方依赖，
        // 放大不糊，也不像 emoji 那样会退回黑白。
        private static readonly string[] IconFamilies =
            { "Segoe Fluent Icons", "Segoe MDL2 Assets" };
        private static readonly string[] MonoFamilies = { "Consolas", "Courier New" };

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
