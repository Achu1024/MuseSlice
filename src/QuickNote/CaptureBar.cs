using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 悬浮小条。无边框、置顶、不进任务栏、带投影与圆角。同一窗口两种模式。
    /// </summary>
    internal sealed class CaptureBar : Form
    {
        /// <summary>设计基准尺寸（96 DPI 下的逻辑像素）。</summary>
        private const int LogicalWidth = 560;
        private const int LogicalSearchWidth = 600;
        private const int LogicalSearchHeight = 470;
        private const int LogicalLineHeight = 22;
        private const int LogicalChrome = 90;

        private Palette _palette;      // 非 readonly：改主题时要换掉
        private NoteStore _store;      // 非 readonly：改日记目录时要换掉
        private readonly BarChrome _chrome;

        private readonly Panel _content;          // 两种模式互斥的内容区
        private readonly BarTextBox _input;
        private readonly SearchResultsView _results;
        private readonly Panel _searchRow;
        private SearchController _search;   // 非 readonly：改目录时要重建
        private TextBox _queryBox;      // 由 BuildSearchRow 赋值
        private Label _searchStatus;    // 由 BuildSearchRow 赋值

        private BarMode _mode = BarMode.Record;

        /// <summary>记录模式的草稿。Esc 与失焦都不清空它，只有保存成功或使用者手动删除才清。</summary>
        private string _draft = "";

        private string _lastQuery = "";
        private SearchResult _lastResult = SearchResult.Empty();

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
            _input = BuildInput(palette);
            _results = BuildResults(palette);
            _searchRow = BuildSearchRow(palette);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = palette.BarBack };
            _content.Controls.Add(_input);     // Fill 先加
            _content.Controls.Add(_results);   // 与 _input 互斥，同一时间只有一个可见

            // 停靠顺序由 z-order 决定：WinForms 从末尾往前停靠，先加的 Fill 最后停靠。
            // 所以 Fill 必须最先加，上下各条才能在它之外拿到自己的空间。
            Controls.Add(_content);
            Controls.Add(_searchRow);
            Controls.Add(_chrome.Bottom);
            Controls.Add(_chrome.Top);

            _search = new SearchController(this, _store.NotesDir, OnSearchResult, OnSearchStarted);

            ApplySize();
        }

        private BarTextBox BuildInput(Palette palette)
        {
            var box = new BarTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = palette.BarBack,
                ForeColor = palette.Text,
                Font = palette.Body
            };
            box.TextChangedByUser += delegate
            {
                MarkdownHighlighter.Apply(box, _palette);
                FitToContent();
            };
            return box;
        }

        private SearchResultsView BuildResults(Palette palette)
        {
            var view = new SearchResultsView(palette) { Dock = DockStyle.Fill, Visible = false };
            view.Activated += OpenHit;
            return view;
        }

        private Panel BuildSearchRow(Palette palette)
        {
            _searchStatus = new Label
            {
                Font = palette.Small,
                ForeColor = palette.Muted,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = 210
            };

            _queryBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = palette.BarBack,
                ForeColor = palette.Text,
                Font = palette.Body,
                Dock = DockStyle.Fill
            };
            _queryBox.TextChanged += delegate
            {
                _lastQuery = _queryBox.Text;
                _search.Query(_lastQuery);
            };
            _queryBox.KeyDown += OnSearchKeyDown;

            // 查询框与右侧状态同一行：先加 Fill 再加 Right，Right 才会贴右边。
            var row = new Panel { Dock = DockStyle.Top, Height = 26, Visible = false };
            row.Controls.Add(_queryBox);
            row.Controls.Add(_searchStatus);
            return row;
        }

        /// <summary>记录模式的草稿（未保存的内容）。退出前要拿它问一句。</summary>
        public string Draft { get { return _input.Text; } }

        // ---- 尺寸 ----

        private void ApplySize()
        {
            if (_mode == BarMode.Search)
            {
                Width = (int)Math.Round(LogicalSearchWidth * _scale);
                Height = (int)Math.Round(LogicalSearchHeight * _scale);
                return;
            }
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
            if (_mode != BarMode.Record) return;
            // 走 SetLineCount 而不是直接改 Height：尺寸常量是 96 DPI 的逻辑值，
            // 乘缩放系数的事由 ApplySize 统一负责，绕过它就会在高 DPI 下偏小。
            //
            // 行数用 GetLineFromCharIndex 算，不用 .Lines——后者在粘贴大段文本时
            // 会为每一行分配一个字符串，正是最不该在此时刻分配。
            var lineCount = _input.TextLength == 0
                ? 1
                : _input.GetLineFromCharIndex(_input.TextLength) + 1;
            SetLineCount(lineCount);
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

        // ---- 记录模式的键位 ----

        /// <summary>
        /// 记录模式的按键处理。
        /// <para>
        /// 必须挂在 <see cref="ProcessCmdKey"/> 而不是 OnKeyDown：Tab 在 WinForms 里
        /// 是「对话框键」，会在 ProcessDialogKey 阶段被当作「移到下一个控件」消费掉，
        /// 靠 KeyPreview + OnKeyDown 拦不住——实测按 Tab 毫无反应。
        /// ProcessCmdKey 是 WinForms 专门留给 Tab / 方向键这类键的拦截点，
        /// 在对话框键处理之前到达。
        /// </para>
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (_mode != BarMode.Record) return base.ProcessCmdKey(ref msg, keyData);

            var key = keyData & Keys.KeyCode;
            var shift = (keyData & Keys.Shift) == Keys.Shift;

            if (key == Keys.Enter)
            {
                var action = InputRules.DecideEnter(_input.IsComposing, shift);
                if (action == EnterAction.Ignore) return true;   // 组合态：交给输入法
                if (action == EnterAction.Newline) return false; // 交给控件插入换行
                Save();
                return true;
            }

            if (key == Keys.Escape)
            {
                CloseBar();     // 隐藏但不清空：写过三行东西误按一下全没了最劝退
                return true;
            }

            if (key == Keys.Tab)
            {
                var tab = InputRules.DecideTab(BarMode.Record, _input.TextLength == 0);
                if (tab == TabAction.Indent) InsertIndent();
                else if (tab == TabAction.SwitchToSearch) SwitchTo(BarMode.Search);
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
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
            if (_mode == BarMode.Search)
            {
                _chrome.Top.Text = "搜索日记";
                _chrome.Bottom.Text = "↑↓ 选择　·　Enter 打开　·　Tab 返回";
            }
            else
            {
                _chrome.Top.Text = TopText();
                _chrome.Bottom.Text = "Enter 保存　·　Shift+Enter 换行　·　Tab 搜索";
            }
        }

        // ---- 搜索模式 ----

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { _results.MoveSelection(1); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Up) { _results.MoveSelection(-1); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Enter) { _results.ActivateSelected(); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Tab || e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                SwitchTo(BarMode.Record);
            }
        }

        private void OnSearchStarted()
        {
            _searchStatus.Text = "搜索中…";
        }

        private void OnSearchResult(SearchResult result)
        {
            _lastResult = result;
            _results.SetResult(result, _lastQuery);
            _searchStatus.Text = result.Hits.Count + " 条"
                + (result.Truncated ? "（已截断）" : "")
                + "　·　扫描 " + result.ScannedFiles + " 个文件"
                + (result.UnreadableFiles > 0 ? "，跳过 " + result.UnreadableFiles + " 个无法读取" : "");
        }

        private void SwitchTo(BarMode mode)
        {
            _mode = mode;
            var searching = mode == BarMode.Search;

            _input.Visible = !searching;
            _results.Visible = searching;
            _searchRow.Visible = searching;

            if (searching)
            {
                ApplySize();
                Reposition();
                _queryBox.Text = _lastQuery;          // 沿用上次查询词，避免反复输入
                _queryBox.SelectionStart = _queryBox.TextLength;
                RefreshChrome();
                _queryBox.Focus();

                // 显式发起一次扫描。不能只靠 _queryBox.Text 的 TextChanged：
                // 首次进入时文本本来就从 "" 到 ""，不产生变更事件，结果是一片空白。
                // 空查询会走 ListRecent，把最近的记录直接列出来。
                _search.Query(_queryBox.Text);
            }
            else
            {
                ApplySize();
                Reposition();
                RefreshChrome();
                _input.Focus();
            }
        }

        /// <summary>设置里改了日记目录后，把存储与搜索器一起换掉。</summary>
        public void SetStore(NoteStore store)
        {
            _store = store;
            _search.Dispose();
            _search = new SearchController(this, store.NotesDir, OnSearchResult, OnSearchStarted);
            _lastQuery = "";
            _lastResult = SearchResult.Empty();
            _queryBox.Text = "";
            _results.SetResult(_lastResult, "");
            RefreshChrome();
        }

        /// <summary>设置里改了主题后重新着色。</summary>
        public void ApplyTheme(Palette palette)
        {
            _palette = palette;
            BackColor = palette.BarBack;
            _content.BackColor = palette.BarBack;
            _input.BackColor = palette.BarBack;
            _input.ForeColor = palette.Text;
            _input.Font = palette.Body;
            _queryBox.BackColor = palette.BarBack;
            _queryBox.ForeColor = palette.Text;
            _queryBox.Font = palette.Body;
            _chrome.Top.ForeColor = palette.Muted;
            _chrome.Bottom.ForeColor = palette.Muted;
            _searchStatus.ForeColor = palette.Muted;
            MarkdownHighlighter.Apply(_input, palette);
            Invalidate(true);
        }

        private static void OpenHit(SearchHit hit)
        {
            try
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(hit.FilePath) { UseShellExecute = true });
            }
            catch (Exception ex) { QuickNoteApp.Log("打开 " + hit.FilePath + " 失败：" + ex.Message); }
        }

        // ---- 进出 ----

        public void OpenBar()
        {
            SwitchTo(BarMode.Record);          // 默认进记录模式，不记忆上次
            _input.Text = _draft;
            _input.SelectionStart = _input.TextLength;
            MarkdownHighlighter.Apply(_input, _palette);
            FitToContent();
            ShowBar();
            _input.Focus();
        }

        public void CloseBar()
        {
            _draft = _input.Text;              // 记录模式草稿保留
            _search.Cancel();
            _lastQuery = "";                   // 搜索词与结果在小条隐藏时丢弃
            _lastResult = SearchResult.Empty();
            _queryBox.Text = "";
            _results.SetResult(_lastResult, "");
            if (_mode == BarMode.Search) SwitchTo(BarMode.Record);
            HideBar();
        }

        public void ShowBar()
        {
            Reposition();
            Show();

            // 必须真正抢到前台，否则小条虽然画出来了，键盘焦点还在原来那个窗口上：
            // Tab、Enter、Esc、打字全部会打到别处，使用者得先用鼠标点一下才正常。
            NativeMethods.ForceForeground(Handle);

            // 激活是异步的，Focus() 紧跟其后可能落在激活完成之前。
            // 用 BeginInvoke 推到消息队列尾部，等激活落地后再设焦点。
            BeginInvoke((Action)FocusInput);
        }

        /// <summary>把键盘焦点放到当前模式该拿焦点的控件上。</summary>
        private void FocusInput()
        {
            Activate();
            if (_mode == BarMode.Search)
            {
                _queryBox.Focus();
                _queryBox.SelectionStart = _queryBox.TextLength;
            }
            else
            {
                _input.Focus();
                _input.SelectionStart = _input.TextLength;
            }
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
