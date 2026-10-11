using System;
using System.Drawing;
using System.Windows.Forms;

namespace MuseSlice
{
    /// <summary>
    /// 小条的上下两条辅助信息行。刻意做成极简：上面只有日期与条数，下面只有键位提示。
    /// <para>
    /// 各带一条 1px 分隔线。原先完全没有结构线，日期、正文、提示挤在一起，
    /// 那是「乱」的来源之一（定案 §4）。线画在控件自己的边缘上，
    /// 不动停靠顺序——动停靠会打乱 Fill 的取空间方式。
    /// </para>
    /// <para>
    /// 下面那条是自绘的 <see cref="KeyHintBar"/>：键名要画成键帽、说明要浅一档，
    /// 一个 Label 做不到两种样式，所以整条提示以前是一团同色的浅灰、看不出哪个是键。
    /// </para>
    /// </summary>
    internal sealed class BarChrome
    {
        public readonly Label Top;
        public readonly KeyHintBar Bottom;

        private Color _hairline;
        private Color _brand;
        private Color _accent;

        /// <summary>顶部信息行左侧标志的高度（物理像素）。0 表示不画。</summary>
        private int _markHeight;

        public BarChrome(Palette p)
        {
            _hairline = p.Hairline;
            _brand = p.Brand;
            _accent = p.BrandAccent;

            Top = new Label
            {
                Font = p.Chrome,
                ForeColor = p.Muted,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 2, 0),
                Dock = DockStyle.Top
            };

            Bottom = new KeyHintBar(p) { Dock = DockStyle.Bottom };

            Top.Paint += delegate(object s, PaintEventArgs e)
            {
                if (_markHeight > 0)
                {
                    // 行高不够时按行高收，绝不探出去压到分隔线。
                    var h = Math.Min(_markHeight, Top.Height - 2);
                    var w = (int)Math.Round(h * LogoMark.Aspect);
                    LogoMark.Draw(e.Graphics,
                        new Rectangle(2, (Top.Height - h) / 2, w, h), _brand, _accent);
                }

                using (var pen = new Pen(_hairline))
                    e.Graphics.DrawLine(pen, 0, Top.Height - 1, Top.Width, Top.Height - 1);
            };
            Bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(_hairline))
                    e.Graphics.DrawLine(pen, 0, 0, Bottom.Width, 0);
            };
        }

        /// <summary>
        /// 顶部信息行左侧要给标志让出多高（宽度按标志的宽高比推）。由
        /// <see cref="CaptureBar.ApplySize"/> 在算出缩放之后调它——这里不自己推 DPI，
        /// 否则两个地方各算一遍缩放，迟早对不上。
        /// <para>
        /// 必须在布局阶段设 <c>Padding</c>，不能等 Paint 里再设：改 Padding 会触发重排，
        /// 在绘制途中改就是「画一半重来」，正好是这次要消掉的那种闪。
        /// </para>
        /// </summary>
        public void SetLogoInset(int markHeight)
        {
            _markHeight = markHeight < 0 ? 0 : markHeight;
            var width = _markHeight > 0 ? (int)Math.Round(_markHeight * LogoMark.Aspect) : 0;

            var want = new Padding(width > 0 ? width + 8 : 2, 0, 2, 0);
            if (!Top.Padding.Equals(want)) Top.Padding = want;

            Top.Invalidate();
        }

        public void ApplyHairline(Color hairline)
        {
            _hairline = hairline;
            Top.Invalidate();
            Bottom.Invalidate();
        }

        public void ApplyTheme(Palette p)
        {
            _brand = p.Brand;
            _accent = p.BrandAccent;
            Top.ForeColor = p.Muted;
            Top.Font = p.Chrome;
            Top.Invalidate();
            Bottom.ApplyTheme(p);
        }
    }
}
