using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 悬浮小条。无边框、置顶、不进任务栏、带投影与圆角。
    /// 本任务只做外壳与定位；语法高亮与业务在后续任务接入。
    /// </summary>
    internal sealed class CaptureBar : Form
    {
        /// <summary>设计基准宽度（96 DPI 下的逻辑像素）。</summary>
        private const int LogicalWidth = 560;
        private const int LogicalLineHeight = 22;
        private const int LogicalChrome = 90;

        private readonly Palette _palette;

        /// <summary>
        /// 显示器 DPI / 96。尺寸常量都按 96 DPI 写，实际用之前乘上它。
        /// <para>
        /// 不靠 WinForms 的 AutoScaleMode 做这件事：对纯代码构造的窗体，
        /// 它会自己把 AutoScaleDimensions 归一到当前系统 DPI，得到一个恒为 1
        /// 的缩放系数，等于什么也不做，还平添一层难以推理的行为。
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

            ApplySize();
        }

        private void ApplySize()
        {
            Width = (int)Math.Round(LogicalWidth * _scale);
            Height = (int)Math.Round(
                BarGeometry.HeightFor(_lineCount, LogicalLineHeight, LogicalChrome) * _scale);
        }

        /// <summary>按内容行数调整高度（Task 9 起由输入框内容驱动）。</summary>
        public void SetLineCount(int lineCount)
        {
            _lineCount = lineCount;
            ApplySize();
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
            //
            // 用 GetDpiForWindow 而不是 Control.DeviceDpi：本机实测后者恒返回 96，
            // 而同一个进程看到的 workArea 是 1920×1080（DPI 不感知的进程只能看到
            // 虚拟化的 1536×864），两者不自洽，说明 DeviceDpi 在这个
            // .NET Framework + PerMonitorV2 组合下不可信。
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
