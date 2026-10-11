using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MuseSlice
{
    /// <summary>
    /// 键位提示的绘制。键名画成小键帽、操作说明留浅灰——区分靠的是「一块底色 + 深色字」
    /// 而不是颜色，跟整块界面「近乎无彩、结构靠字重」的纪律一致。
    /// <para>
    /// 键帽的所有尺寸都由字号推出来（内缩、圆角、间距都是字高的几分之一），
    /// 所以同一套代码在底下那条 8.5pt 的提示行和首次运行那张大卡片上都能用。
    /// </para>
    /// </summary>
    internal static class KeyHint
    {
        /// <summary>两段之间的间隔。显式声明，不靠「前一段是什么」去猜。</summary>
        internal enum Gap
        {
            None,    // 行首
            Tight,   // 键与它自己的说明之间
            Wide     // 不同键位之间
        }

        internal struct Part
        {
            public string Text;
            public bool IsKey;
            public Gap Before;
        }

        internal static Part Key(string text, Gap before = Gap.Wide)
        {
            return new Part { Text = text, IsKey = true, Before = before };
        }

        internal static Part Word(string text, Gap before = Gap.Tight)
        {
            return new Part { Text = text, IsKey = false, Before = before };
        }

        private static int PadX(Font font) { return (int)Math.Round(font.Height * 0.34); }
        private static int PadY(Font font) { return (int)Math.Round(font.Height * 0.14); }
        private static int Radius(Font font) { return Math.Max(2, (int)Math.Round(font.Height * 0.22)); }

        private static int GapWidth(Gap gap, Font font, bool compact)
        {
            if (gap == Gap.None) return 0;
            if (gap == Gap.Tight) return (int)Math.Round(font.Height * 0.26);
            // 紧凑模式把所有宽间隔收到紧间隔。放不下时用，见 Draw 的说明。
            return (int)Math.Round(font.Height * (compact ? 0.26 : 0.44));
        }

        /// <summary>某个零件之前要留多少空。行首不留。</summary>
        private static int GapBefore(Part[] parts, int index, Font font, bool compact)
        {
            if (index == 0) return 0;
            return GapWidth(parts[index].Before, font, compact);
        }

        private static int WidthOf(Graphics g, Part part, Font font)
        {
            var textWidth = TextRenderer.MeasureText(g, part.Text, font).Width;
            return part.IsKey ? textWidth + PadX(font) * 2 : textWidth;
        }

        internal static int Measure(Graphics g, Part[] parts, Font font, bool compact = false)
        {
            var width = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                width += GapBefore(parts, i, font, compact) + WidthOf(g, parts[i], font);
            }
            return width;
        }

        internal static void Draw(Graphics g, Palette p, Part[] parts, Font font,
                                  int left, int top, int height, bool compact = false)
        {
            var at = left;

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                at += GapBefore(parts, i, font, compact);

                var textWidth = TextRenderer.MeasureText(g, part.Text, font).Width;

                if (!part.IsKey)
                {
                    TextRenderer.DrawText(g, part.Text, font,
                        new Rectangle(at, top, textWidth + 2, height), p.Muted,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                    at += textWidth;
                    continue;
                }

                // 键帽：墨色的一层极淡着色。深浅由 ink 决定，所以两套主题都成立。
                var capWidth = textWidth + PadX(font) * 2;
                var capHeight = Math.Min(height, font.Height + PadY(font) * 2);
                var cap = new Rectangle(at, top + (height - capHeight) / 2, capWidth, capHeight);

                using (var path = Rounded(cap, Radius(font)))
                using (var b = new SolidBrush(Color.FromArgb(26, p.Ink)))
                    g.FillPath(b, path);

                TextRenderer.DrawText(g, part.Text, font, cap, p.Ink,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                at += capWidth;
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>
    /// 小条底下那一条键位提示。自绘而不是用 Label：需要两种深浅与一块键帽底色，
    /// 一个 Label 做不到。
    /// </summary>
    internal sealed class KeyHintBar : Control
    {
        private Palette _p;
        private KeyHint.Part[] _parts = new KeyHint.Part[0];

        /// <summary>非空时按纯文本画（保存失败的红字提示走这条）。</summary>
        private string _plain;
        private Color _plainColor;

        public KeyHintBar(Palette p)
        {
            _p = p;
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = p.Paper;
        }

        public void SetHint(params KeyHint.Part[] parts)
        {
            _parts = parts ?? new KeyHint.Part[0];
            _plain = null;
            Invalidate();
        }

        public void SetPlain(string text, Color color)
        {
            _plain = text;
            _plainColor = color;
            Invalidate();
        }

        public void ApplyTheme(Palette p)
        {
            _p = p;
            BackColor = p.Paper;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_p.Paper);

            if (_plain != null)
            {
                TextRenderer.DrawText(g, _plain, _p.Chrome,
                    new Rectangle(0, 0, Width, Height), _plainColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis);
                return;
            }

            // 竖着居中：这个控件比字体高，直接按字高画会贴顶
            var lineHeight = _p.Chrome.Height + 4;

            // 先量放不放得下。放不下就把间隔收到最紧——**绝不裁掉最后一个键**：
            // 提示行被裁掉一半比没有还糟，使用者会以为那就是全部。
            var compact = KeyHint.Measure(g, _parts, _p.Chrome) > Width;

            KeyHint.Draw(g, _p, _parts, _p.Chrome, 0, (Height - lineHeight) / 2, lineHeight, compact);
        }
    }
}
