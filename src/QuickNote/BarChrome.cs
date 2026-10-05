using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>小条的上下两条辅助信息行。刻意做成极简：只有日期、条数与键位提示。</summary>
    internal sealed class BarChrome
    {
        public readonly Label Top;
        public readonly Label Bottom;

        public BarChrome(Palette p)
        {
            Top = NewLabel(p.Small, p.Muted);
            Bottom = NewLabel(p.Small, p.Muted);
            Top.Dock = DockStyle.Top;
            Top.Height = 20;
            Bottom.Dock = DockStyle.Bottom;
            Bottom.Height = 20;
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
