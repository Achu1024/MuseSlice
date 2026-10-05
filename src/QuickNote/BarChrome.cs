using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 小条的上下两条辅助信息行。刻意做成极简：只有日期、条数与键位提示。
    /// <para>
    /// 各带一条 1px 分隔线。原先完全没有结构线，日期、正文、提示挤在一起，
    /// 那是「乱」的来源之一（定案 §4）。线画在 Label 自己的边缘上，
    /// 不动停靠顺序——动停靠会打乱 Fill 的取空间方式。
    /// </para>
    /// </summary>
    internal sealed class BarChrome
    {
        public readonly Label Top;
        public readonly Label Bottom;

        private Color _hairline;

        public BarChrome(Palette p)
        {
            _hairline = p.Hairline;
            Top = NewLabel(p.Chrome, p.Muted);
            Bottom = NewLabel(p.Chrome, p.Muted);
            Top.Dock = DockStyle.Top;
            Bottom.Dock = DockStyle.Bottom;

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

        private static Label NewLabel(Font font, Color color)
        {
            return new Label
            {
                Font = font,
                ForeColor = color,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(2, 0, 2, 0)
            };
        }
    }
}
