using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
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

        public BarChrome(Palette p)
        {
            _hairline = p.Hairline;

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
                using (var pen = new Pen(_hairline))
                    e.Graphics.DrawLine(pen, 0, Top.Height - 1, Top.Width, Top.Height - 1);
            };
            Bottom.Paint += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(_hairline))
                    e.Graphics.DrawLine(pen, 0, 0, Bottom.Width, 0);
            };
        }

        public void ApplyHairline(Color hairline)
        {
            _hairline = hairline;
            Top.Invalidate();
            Bottom.Invalidate();
        }

        public void ApplyTheme(Palette p)
        {
            Top.ForeColor = p.Muted;
            Top.Font = p.Chrome;
            Bottom.ApplyTheme(p);
        }
    }
}
