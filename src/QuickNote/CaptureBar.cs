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
        private readonly NoteStore _store;
        private readonly BarChrome _chrome;

        /// <summary>记录模式的草稿。Esc 与失焦都不清空它，只有保存成功或使用者手动删除才清。</summary>
        private string _draft = "";

        /// <summary>仅在保存出错后短暂启用，用来把红字提示恢复成常规提示。平时不跑。</summary>
        private Timer _errorReset;

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

        public CaptureBar(Palette palette, NoteStore store)
        {
            _palette = palette;
            _store = store;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = palette.BarBack;
            KeyPreview = true;
            DoubleBuffered = true;

            AutoScaleMode = AutoScaleMode.None;   // 缩放由 _scale 显式负责
            Padding = new Padding(14, 10, 14, 10);

            _chrome = new BarChrome(palette);

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

            // Fill 必须先加：WinForms 从 z-order 末尾往前停靠，
            // 先加的 Fill 最后停靠，上下两条才能在它之外占据各自的空间。
            Controls.Add(_input);
            Controls.Add(_chrome.Top);
            Controls.Add(_chrome.Bottom);

            ApplySize();
        }

        /// <summary>记录模式的草稿（未保存的内容）。退出前要拿它问一句。</summary>
        public string Draft { get { return _input.Text; } }

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

        // ---- 键位 ----

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Enter)
            {
                var action = InputRules.DecideEnter(_input.IsComposing, e.Shift);
                if (action == EnterAction.Ignore) { e.SuppressKeyPress = true; return; }
                if (action == EnterAction.Newline) return;      // 交给控件插入换行
                e.SuppressKeyPress = true;
                Save();
                return;
            }

            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                CloseBar();     // 隐藏但不清空：写过三行东西误按一下全没了最劝退
                return;
            }

            if (e.KeyCode == Keys.Tab)
            {
                var tab = InputRules.DecideTab(BarMode.Record, _input.TextLength == 0);
                e.SuppressKeyPress = true;
                if (tab == TabAction.Indent) InsertIndent();
                // SwitchToSearch 由 Task 12 接管
                return;
            }
        }

        private void InsertIndent()
        {
            _input.SelectedText = new string(' ', InputRules.IndentWidth);
        }

        // ---- 保存 ----

        /// <summary>返回 true 表示已保存并清空。</summary>
        public bool Save()
        {
            var text = _input.Text;
            if (text.Trim().Length == 0)
            {
                HideBar();    // 空输入直接关掉，不创建文件、不留痕迹
                return false;
            }

            try
            {
                _store.Append(text, DateTime.Now);
                _input.Clear();
                _draft = "";
                _chrome.Bottom.ForeColor = _palette.Muted;
                RefreshChrome();
                HideBar();
                return true;
            }
            catch (NoteStoreException ex)
            {
                // 最关键的一条：内容绝不清空。让使用者能把内容复制走。
                QuickNoteApp.Log(ex.Message);
                _chrome.Bottom.Text = "保存失败，内容已保留：" + ex.Message;
                _chrome.Bottom.ForeColor = _palette.Error;
                StartErrorReset();
                return false;
            }
        }

        private void StartErrorReset()
        {
            if (_errorReset == null)
            {
                _errorReset = new Timer { Interval = 6000 };
                _errorReset.Tick += delegate
                {
                    _errorReset.Stop();
                    _chrome.Bottom.ForeColor = _palette.Muted;
                    RefreshChrome();
                };
            }
            _errorReset.Stop();
            _errorReset.Start();
        }

        public string TopText()
        {
            var today = DateTime.Now;
            var count = 0;
            try { count = _store.CountEntries(today); }
            catch { /* 读不到就显示 0 条，不打扰使用者 */ }

            return DateFormat.Heading(today).Substring(2)
                + "　·　今天已记 " + count + " 条";
        }

        private void RefreshChrome()
        {
            _chrome.Top.Text = TopText();
            _chrome.Bottom.Text = "Enter 保存　·　Shift+Enter 换行　·　Tab 搜索";
        }

        // ---- 进出 ----

        public void OpenBar()
        {
            RefreshChrome();
            _input.Text = _draft;
            _input.SelectionStart = _input.TextLength;
            MarkdownHighlighter.Apply(_input, _palette);
            FitToContent();
            ShowBar();
            _input.Focus();
        }

        public void CloseBar()
        {
            _draft = _input.Text;      // Esc 与失焦都不清空草稿
            HideBar();
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
            CloseBar();   // 失焦隐藏但不清空内容，防手滑
        }
    }
}
