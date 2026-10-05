using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 悬浮小条。无边框、置顶、不进任务栏、带投影与圆角。
    /// </summary>
    internal sealed class CaptureBar : Form
    {
        /// <summary>设计基准尺寸（96 DPI 下的逻辑像素）。</summary>
        private const int LogicalWidth = 560;
        private const int LogicalLineHeight = 22;
        private const int LogicalChrome = 90;

        private readonly Palette _palette;
        private readonly BarTextBox _input;

        /// <summary>
        /// 显示器 DPI / 96。尺寸常量都按 96 DPI 写，实际用之前乘上它。
        /// <para>
        /// 不靠 WinForms 的 AutoScaleMode：对纯代码构造的窗体，它会自己把
        /// AutoScaleDimensions 归一到当前系统 DPI，得到恒为 1 的系数，等于什么
        /// 也不做。而 Control.DeviceDpi 在本机谎报 96（显示器实为 125%），
        /// 所以用 GetDpiForWindow。
        /// </para>
        /// </summary>
        private float _scale = 1f;

        private int _lineCount = BarGeometry.MinLines;

        public CaptureBar(Palette palette)
        {
            _palette = palette;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = palette.BarBack;
            KeyPreview = true;
            DoubleBuffered = true;

            AutoScaleMode = AutoScaleMode.None;   // 缩放由 _scale 显式负责
            Padding = new Padding(14, 10, 14, 10);

            _input = new BarTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = palette.BarBack,
                ForeColor = palette.Text,
                Font = palette.Body
            };
            _input.TextChangedByUser += delegate
            {
                MarkdownHighlighter.Apply(_input, _palette);
                FitToContent();
            };
            Controls.Add(_input);

            ApplySize();
        }

        private void ApplySize()
        {
            Width = (int)Math.Round(LogicalWidth * _scale);
            Height = (int)Math.Round(
                BarGeometry.HeightFor(_lineCount, LogicalLineHeight, LogicalChrome) * _scale);
        }

        /// <summary>按内容行数调整高度。</summary>
        public void SetLineCount(int lineCount)
        {
            _lineCount = lineCount;
            ApplySize();
        }

        private void FitToContent()
        {
            // 走 SetLineCount 而不是直接改 Height：尺寸常量是 96 DPI 的逻辑值，
            // 乘缩放系数的事由 ApplySize 统一负责，绕过它就会在高 DPI 下偏小。
            SetLineCount(_input.Lines.Length);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= NativeMethods.CS_DROPSHADOW;   // 系统绘制的投影，免费
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // 句柄到手后显示器 DPI 才可查。尺寸常量乘上它，高 DPI 下才能保持
            // 和设计稿一致的观感（否则框不放大、字却按 DPI 放大了，显得局促）。
            try
            {
                var dpi = NativeMethods.GetDpiForWindow(Handle);
                if (dpi >= 96) _scale = dpi / 96f;
            }
            catch { /* 老系统上没有这个 API，保持 1.0 */ }
            ApplySize();

            try
            {
                // Windows 11 让系统画圆角，比自绘 region 干净得多
                var pref = NativeMethods.DWMWCP_ROUND;
                NativeMethods.DwmSetWindowAttribute(Handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref pref, sizeof(int));
            }
            catch { /* 非 Win11 上忽略 */ }
            Reposition();
        }

        /// <summary>定位到鼠标所在显示器工作区的上方三分之一处居中。</summary>
        public void Reposition()
        {
            var screen = Screen.FromPoint(Cursor.Position);
            var wa = screen.WorkingArea;
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + wa.Height / 3;
        }

        public void ShowBar()
        {
            Reposition();
            Show();
            NativeMethods.SetForegroundWindow(Handle);
        }

        public void HideBar()
        {
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            HideBar();   // 失焦隐藏但不清空内容，防手滑
        }
    }
}
