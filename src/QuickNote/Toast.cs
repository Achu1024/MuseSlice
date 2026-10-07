using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 右下角一闪而过的回执。小条一保存就消失了，屏幕上总得留个「存进去了」的痕迹。
    /// <para>
    /// **不用托盘气泡**（<c>NotifyIcon.ShowBalloonTip</c>）：Win10 起那个超时参数会被
    /// 忽略，固定显示约五秒，而且会留在操作中心里——达不到「快速消失」。
    /// </para>
    /// <para>
    /// **绝不抢焦点**：小条一失焦就自动收起，而使用者按回车保存时多半正在别的编辑器里
    /// 打字。<c>WS_EX_NOACTIVATE</c> + <see cref="ShowWithoutActivation"/> 保证它只是被
    /// 画出来，不改变前台窗口——否则保存一下就把人从正在打字的窗口里踢出去。
    /// </para>
    /// <para>
    /// 整个程序里只有一个实例，反复用：每次保存新建一个窗口会累积句柄。
    /// </para>
    /// </summary>
    internal sealed class Toast : Form
    {
        // 三个阶段合成一次出现：淡入 → 停住 → 淡出。硬出现的提示会「啪」地跳出来，
        // 硬消失的会像被掐断；两头都淡，才像一口气读完然后自然散去。
        private const int StepMs = 20;         // 50fps，够顺了
        private const int FadeInMs = 180;
        private const int HoldMs = 2000;
        private const int FadeOutMs = 350;

        private readonly Timer _life;
        private Palette _p;
        private string _text = "";
        private float _scale = 1f;
        private int _elapsed;

        private Rectangle _markRect;
        private Rectangle _textRect;
        private int _pad, _gap, _lineHeight;

        /// <summary>不让它抢前台。见类注释。</summary>
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
                cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;
                return cp;
            }
        }

        public Toast(Palette palette)
        {
            _p = palette;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = palette.Paper;
            DoubleBuffered = true;

            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            _life = new Timer { Interval = StepMs };
            _life.Tick += delegate { Step(); };

            // 先把控件建出来并留下句柄，之后 Show 只是把它显示出来。
            var unused = Handle;
        }

        /// <summary>显示一条回执。已经在显示时会重新计时，不会叠出第二个窗口。</summary>
        public void Pop(string text, Palette palette)
        {
            _p = palette;
            _text = text ?? "";

            try
            {
                var dpi = NativeMethods.GetDpiForWindow(Handle);
                if (dpi >= 96) _scale = dpi / 96f;
            }
            catch { /* 老系统上没有这个 API，保持 1.0 */ }

            Measure();
            Position();
            Invalidate();

            // 从全透明开始重新走一遍淡入。连着保存两次时，第二次会从头淡入，
            // 而不是接着上一次的淡出往下走——看起来才像「又记了一笔」。
            _elapsed = 0;
            Opacity = 0;
            _life.Stop();
            _life.Start();
            Show();
        }

        /// <summary>一次节拍：按已经过去的时间决定现在的不透明度。</summary>
        private void Step()
        {
            _elapsed += StepMs;

            if (_elapsed < FadeInMs)
            {
                Opacity = (double)_elapsed / FadeInMs;
                return;
            }

            var held = _elapsed - FadeInMs;
            if (held < HoldMs)
            {
                if (Opacity < 1.0) Opacity = 1.0;
                return;
            }

            var fading = held - HoldMs;
            if (fading >= FadeOutMs)
            {
                // 藏起来之后把表停掉：待机时进程里不该有任何计时器在跑。
                _life.Stop();
                Hide();
                Opacity = 0;
                return;
            }

            Opacity = 1.0 - (double)fading / FadeOutMs;
        }

        public void ApplyTheme(Palette palette)
        {
            _p = palette;
            BackColor = palette.Paper;
            Invalidate();
        }

        private int S(int logical) { return (int)Math.Round(logical * _scale); }

        /// <summary>
        /// 尺寸按文字实宽算，不写死——字号一换、DPI 一变，写死的宽会把「已记录」截掉。
        /// </summary>
        private void Measure()
        {
            using (var g = CreateGraphics())
            {
                _pad = S(14);
                _gap = S(8);
                _lineHeight = Math.Max(_p.Icon.Height, _p.Body.Height);

                var markWidth = _p.Icon.Height;
                var textWidth = TextRenderer.MeasureText(g, _text, _p.Body).Width;

                var height = _lineHeight + S(18);
                var width = _pad * 2 + markWidth + _gap + textWidth;

                ClientSize = new Size(width, height);

                _markRect = new Rectangle(_pad, 0, markWidth, height);
                _textRect = new Rectangle(_pad + markWidth + _gap, 0, textWidth, height);
            }
        }

        /// <summary>贴在工作区右下角。跟小条一样，按鼠标所在的那块屏幕算。</summary>
        private void Position()
        {
            var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            Left = wa.Right - Width - S(16);
            Top = wa.Bottom - Height - S(16);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            try
            {
                // 与小条同一套：Win11 让系统画圆角，比自绘 region 干净
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

            using (var pen = new Pen(_p.Hairline))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // 与小条、搜索结果里用的是同一个图标字形（Windows 自带图标字体）
            TextRenderer.DrawText(g, EntryLine.DoneMark, _p.Icon, _markRect, _p.InkSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            TextRenderer.DrawText(g, _text, _p.Body, _textRect, _p.Ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
