using System;
using System.Drawing;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 首次运行那一张卡片：告诉使用者怎么把它叫出来。
    /// <para>
    /// 这是整个程序里唯一一次「教怎么用」的机会。它要说清的只有一件事——
    /// **双击左 Ctrl**。所以那句话用最大的字号、最重的字重、并且画成键帽，
    /// 其余全是陪衬。
    /// </para>
    /// <para>
    /// 无边框圆角带投影，居中。用的键帽画法与底下那条提示行是同一套
    /// （<see cref="KeyHint"/>），只是字号大得多——这样「那个能按的东西」在两张
    /// 地方的样貌是一致的，使用者认得出。
    /// </para>
    /// </summary>
    internal sealed class WelcomeCard : Form
    {
        private const int LogicalWidth = 430;

        private readonly Palette _p;
        private readonly float _scale;
        private readonly Font _big;

        private KeyHint.Part[] _headline;
        private Rectangle _headlineRect;
        private Rectangle _bodyRect;
        private Button _ok;

        public WelcomeCard(Palette palette)
        {
            _p = palette;
            _scale = DetectScale();

            // 整张卡片里最大的一处字。用正文的家族、放大加粗——不引第三套字体。
            _big = new Font(_p.Body.FontFamily, 15f, FontStyle.Bold);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = _p.Paper;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            Build();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;
                return cp;
            }
        }

        private int S(int logical) { return (int)Math.Round(logical * _scale); }

        private float DetectScale()
        {
            try
            {
                using (var g = CreateGraphics()) return g.DpiX / 96f;
            }
            catch { return 1f; }
        }

        private void Build()
        {
            var pad = S(30);
            var innerWidth = S(LogicalWidth) - pad * 2;
            var y = pad;

            _headline = new[]
            {
                KeyHint.Key("双击左 Ctrl", KeyHint.Gap.None)
            };

            // 键帽比字高一点，这里给足
            var headlineHeight = _big.Height + S(14);
            _headlineRect = new Rectangle(pad, y, innerWidth, headlineHeight);
            y += headlineHeight + S(14);

            var body = "随时唤出小条，写下就存进当天的日记。\n之后也可以在托盘图标上点右键找到它。";
            _bodyRect = new Rectangle(pad, y, innerWidth, 0);

            using (var g = CreateGraphics())
            {
                var size = TextRenderer.MeasureText(g, body, _p.Body,
                    new Size(innerWidth, 0), TextFormatFlags.WordBreak);
                _bodyRect.Height = size.Height + S(4);
            }
            y += _bodyRect.Height + S(26);

            _ok = new Button
            {
                Text = "知道了",
                Font = _p.Body,
                Size = new Size(innerWidth, S(34)),
                Location = new Point(pad, y)
            };
            _ok.Click += delegate { Close(); };
            Controls.Add(_ok);
            AcceptButton = _ok;
            y += _ok.Height + pad;

            ClientSize = new Size(S(LogicalWidth), y);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                var pref = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref pref, sizeof(int));
            }
            catch { /* 非 Win11 上忽略 */ }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_p.Paper);

            // 键帽：与底下那条提示行同一套画法，只是字号大得多
            KeyHint.Draw(g, _p, _headline, _big,
                _headlineRect.X, _headlineRect.Y, _headlineRect.Height);

            TextRenderer.DrawText(g, "随时唤出小条，写下就存进当天的日记。\n之后也可以在托盘图标上点右键找到它。",
                _p.Body, _bodyRect, _p.Muted,
                TextFormatFlags.Left | TextFormatFlags.WordBreak);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _big != null) _big.Dispose();
            base.Dispose(disposing);
        }
    }
}
