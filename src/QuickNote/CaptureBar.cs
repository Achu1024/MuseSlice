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
        private const int LogicalSearchHeight = 560;

        /// <summary>搜索模式的兜底高度：屏幕再矮也不让它塌成一条。</summary>
        private const int LogicalSearchMinHeight = 240;

        /// <summary>
        /// 内边距。两件事都靠它：一是卡片四角的圆弧让开，圆角才不会被内容压出直角；
        /// 二是正文与分隔线的左右留白——16 太贴边，正文看着像从线头上长出来的。
        /// </summary>
        private const int LogicalPadX = 22;
        private const int LogicalPadY = 14;

        /// <summary>上下两条辅助信息行的高度。</summary>
        private const int LogicalChromeLine = 24;

        /// <summary>
        /// 正文区自己的上下内边距，两条分隔线各让开这么多。
        /// <para>
        /// 正文区是 Dock=Fill，不加这道留白的话第一行的行盒顶就直接压在上 hairline 上，
        /// 光标顶端看着像从线上长出来的；最后一行同理贴着下 hairline。
        /// </para>
        /// </summary>
        private const int LogicalContentPadY = 8;

        private Palette _palette;      // 非 readonly：改主题时要换掉
        private NoteStore _store;      // 非 readonly：改日记目录时要换掉
        private readonly BarChrome _chrome;

        private readonly Panel _content;          // 两种模式互斥的内容区
        private readonly CaretOverlay _caret;    // 自绘光标，浮在输入框之上
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

        /// <summary>删除确认态。Backspace 进入，Enter 真删，Esc 取消。</summary>
        private bool _confirmDelete;

        /// <summary>进入确认态时选中的那一条。真删之前要比对，见 DoDelete。</summary>
        private SearchHit _confirmHit;

        /// <summary>DIAG（临时）：上一次记下的首字纵坐标与窗体高度。</summary>
        private int _lastTextTop = int.MinValue;
        private int _lastFormHeight = int.MinValue;

        /// <summary>DIAG（临时）：日志行数上限，免得刷屏。</summary>
        private int _diagLines;

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
            BackColor = palette.Paper;
            KeyPreview = true;
            DoubleBuffered = true;

            AutoScaleMode = AutoScaleMode.None;   // 缩放由 _scale 显式负责；内边距在 ApplySize 里设

            _chrome = new BarChrome(palette);
            _input = BuildInput(palette);
            _results = BuildResults(palette);
            _searchRow = BuildSearchRow(palette);

            _content = new Panel { Dock = DockStyle.Fill, BackColor = palette.Paper };
            _content.Controls.Add(_input);     // Fill 先加
            _content.Controls.Add(_results);   // 与 _input 互斥，同一时间只有一个可见

            _caret = new CaretOverlay { Parent = _content };
            _caret.BringToFront();             // Add 会把新控件放到 z 序最后，也就是最底下
            _caret.SetColors(palette);

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
                BackColor = palette.Paper,
                ForeColor = palette.Ink,
                Font = palette.Body
            };
            box.TextChangedByUser += delegate
            {
                // DIAG（临时）：直接量「文字有没有移动」。前面两条假设（高亮器在组合期间
                // 重排格式、组合字体与正文不一致）都已被证否，所以不再猜第三条——
                // GetPositionFromCharIndex(0).Y 就是第一个字符的纵坐标，它变了就是文字动了。
                // 只在数值真的变化时记一行，免得刷屏。
                if (_diagLines++ < 60)
                {
                    QuickNoteApp.Log("DIAG 文本变更: 组合中=" + box.IsComposing
                        + " 长度=" + box.TextLength
                        + (box.IsComposing ? " [跳过，不重排]" : " [重排]"));
                }
                LogLayoutIfMoved(box);

                // 输入法组合期间**什么都不做**：那段时间文本归 IME 管。
                // 高亮器每次都会给全文重设字体、关开重绘、再 ScrollToCaret 把视图
                // 拉回原处——而组合中字符索引和组合串边界都处于中间态，这三下都会
                // 让文字跳。中文输入时看到「文字抖动、往下沉」就是这么来的。
                // 组合结束后补一次即可（见下面的 CompositionEnded）。
                if (box.IsComposing) return;

                ExpandTodoMarker(box);
                MarkdownHighlighter.Apply(box, _palette);
                FitToContent();
                _caret.Follow();
            };

            // 组合结束后补上：组合期间跳过了着色，而那次上屏的文本变更又先于
            // WM_IME_ENDCOMPOSITION 到达，已经被上面挡掉了。
            box.CompositionEnded += delegate
            {
                if (_diagLines++ < 60)
                {
                    QuickNoteApp.Log("DIAG 组合结束: 长度=" + box.TextLength + " [重排]");
                }
                MarkdownHighlighter.Apply(box, _palette);
                FitToContent();
                _caret.Follow();
            };

            // 光标只在有焦点时存在：失焦立刻停表，待机没有任何计时器在跑。
            box.GotFocus += delegate { _caret.Attach(box, _content, _scale); };
            box.LostFocus += delegate { _caret.Stop(); };
            return box;
        }

        /// <summary>DIAG（临时）：首字纵坐标或窗体高度一变就记一行。</summary>
        private void LogLayoutIfMoved(BarTextBox box)
        {
            int top, formHeight, lines;
            try
            {
                top = box.GetPositionFromCharIndex(0).Y;
                formHeight = Height;
                lines = box.GetLineFromCharIndex(box.TextLength) + 1;
            }
            catch { return; }

            if (top == _lastTextTop && formHeight == _lastFormHeight) return;
            _lastTextTop = top;
            _lastFormHeight = formHeight;

            QuickNoteApp.Log("DIAG 布局变化: 首字y=" + top + " 窗体高=" + formHeight
                + " 输入框高=" + box.Height + " 行数=" + lines
                + " 组合中=" + box.IsComposing + " 长度=" + box.TextLength);
        }

        /// <summary>
        /// 打进「- 」就自动补成勾选框并画成脸（定案 §10.4）。屏幕上看到的是脸，
        /// 文件里存的是 "- [ ]"——使用者从未真正手打过 "[ ]"，所以不构成困扰。
        /// <para>
        /// 只在行首正好打出这两个字符时触发。行中间打 "- " 是正常内容，不动它。
        /// </para>
        /// </summary>
        private void ExpandTodoMarker(BarTextBox box)
        {
            if (box.SelectionLength != 0) return;

            var caret = box.SelectionStart;
            if (caret < 2) return;

            var lineStart = box.GetFirstCharIndexOfCurrentLine();
            if (caret - lineStart != 2) return;      // 不是刚在行首打完两个字符

            var text = box.Text;
            if (lineStart + 1 >= text.Length) return;
            if (text[lineStart] != '-' || text[lineStart + 1] != ' ') return;

            // 赋值 SelectedText 会再触发一次 TextChangedByUser。回到这里时行首已经
            // 是脸而不是 "- "，上面那道判断自然拦住，不会递归。
            var face = EntryLine.UndoneMark + " ";
            box.Select(lineStart, 2);
            box.SelectedText = face;
            box.SelectionStart = lineStart + face.Length;
            box.SelectionLength = 0;
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
                Font = palette.Chrome,
                ForeColor = palette.Muted,
                TextAlign = ContentAlignment.MiddleRight,
                Dock = DockStyle.Right,
                Width = 210
            };

            _queryBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = palette.Paper,
                ForeColor = palette.Ink,
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

        /// <summary>保存成功。回执音与右下角的提示由 QuickNoteApp 接。</summary>
        public event Action Saved;

        /// <summary>记录模式的草稿（未保存的内容）。退出前要拿它问一句。</summary>
        public string Draft { get { return _input.Text; } }

        // ---- 尺寸 ----

        /// <summary>
        /// 一行正文的实际高度，物理像素。
        /// <para>
        /// 取 RichEdit 自己的行高（<see cref="Font.Height"/> 就是它排版单倍行距时用的
        /// 行距）再加一点呼吸，而不是写死一个逻辑常量：字体一换、DPI 一变，
        /// 写死的值会让最后一行的下沿被切掉、或者留出一块晾着的空当。
        /// </para>
        /// </summary>
        private int LineHeightPx
        {
            get
            {
                var font = _input.Font;
                if (font == null) return (int)Math.Round(24 * _scale);
                return font.Height + (int)Math.Round(4 * _scale);
            }
        }

        private int ChromeLinePx { get { return (int)Math.Round(LogicalChromeLine * _scale); } }

        private void ApplySize()
        {
            // 内边距与两条辅助行也要跟着 DPI 缩放。只缩窗体宽高而不缩它们的话，
            // 文字按 DPI 放大了、留白却没放大，高 DPI 下整张卡片会显得局促。
            var padX = (int)Math.Round(LogicalPadX * _scale);
            var padY = (int)Math.Round(LogicalPadY * _scale);
            var contentPadY = (int)Math.Round(LogicalContentPadY * _scale);

            Padding = new Padding(padX, padY, padX, padY);
            // 上下各让开 contentPadY：正文与光标都不贴分隔线。
            _content.Padding = new Padding(0, contentPadY, 0, contentPadY);
            _chrome.Top.Height = ChromeLinePx;
            _chrome.Bottom.Height = ChromeLinePx;

            if (_mode == BarMode.Search)
            {
                Width = (int)Math.Round(LogicalSearchWidth * _scale);

                // 结果列表的每一条是三行文字，行高又按字体真实尺寸算（125% 下约 96px），
                // 所以搜索模式比填写页高得多。小条是从上方三分之一处往下长的，
                // 不钳的话会探出屏幕底部。
                var wa = Screen.FromPoint(Cursor.Position).WorkingArea;
                var room = wa.Bottom - (wa.Top + wa.Height / 3) - (int)Math.Round(20 * _scale);
                Height = BarGeometry.FitSearchHeight(
                    (int)Math.Round(LogicalSearchHeight * _scale), room,
                    (int)Math.Round(LogicalSearchMinHeight * _scale));
                return;
            }

            var oldHeight = Height;
            Width = (int)Math.Round(LogicalWidth * _scale);
            // 高度正好等于「上下内边距 + 上下辅助行 + 正文区上下留白 + N 行正文」，一点不多。
            Height = 2 * padY + 2 * ChromeLinePx + 2 * contentPadY + _lineCount * LineHeightPx;

            if (Height != oldHeight && _diagLines++ < 60)
            {
                QuickNoteApp.Log("DIAG 窗口尺寸变化: 高 " + oldHeight + " -> " + Height
                    + " 行数=" + _lineCount + " 倍率=" + _scale);
            }
        }

        /// <summary>按内容行数调整高度。</summary>
        public void SetLineCount(int lineCount)
        {
            _lineCount = BarGeometry.ClampLines(lineCount);
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
            _caret.Attach(_input, _content, _scale);

            try
            {
                // Windows 11 让系统画圆角，比自绘 region 干净得多。
                // 半径是固定的物理像素（8px），560–700px 宽的卡片上用默认那档正合适；
                // ROUNDSMALL 只有 4px，是在更小的表面上用的，用在这里会显得更方。
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
            var key = keyData & Keys.KeyCode;

            // 搜索模式的 Tab / Esc 也要在这里兜，不能只挂在 _queryBox.KeyDown 上。
            // 理由和下面注释里写的是同一个：Tab 与 Esc 都是「对话框键」，在
            // ProcessDialogKey 阶段就被消费掉了，压根到不了控件的 OnKeyDown。
            // 搜索页原先只挂了 _queryBox.KeyDown，所以按 Tab 毫无反应——
            // 表现就是「卡在搜索界面回不去」。
            if (_mode != BarMode.Record)
            {
                if (key == Keys.Tab)
                {
                    QuickNoteApp.Log("DIAG Tab/Esc 落在搜索页的处理函数上 → 切回填写");
                    SwitchTo(BarMode.Record);
                    return true;
                }
                if (key == Keys.Escape)
                {
                    QuickNoteApp.Log("DIAG Tab/Esc 落在搜索页的处理函数上 → 切回填写");
                    if (_confirmDelete) CancelDeleteConfirm();   // 确认态下 Esc 是「取消」
                    else SwitchTo(BarMode.Record);
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }

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
                QuickNoteApp.Log("DIAG Tab 落在填写页的处理函数上 → "
                    + (_input.TextLength == 0 ? "切到搜索" : "缩进"));
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
                RefreshChrome();
                HideBar();

                // 小条一保存就消失了，屏幕上没有任何「存进去了」的痕迹。
                // 回执（一声「叮咚」+ 右下角闪一下）由 QuickNoteApp 负责——它才拿得到
                // 托盘与回执窗口；这里只报「存成了」。
                var saved = Saved;
                if (saved != null) saved();
                return true;
            }
            catch (NoteStoreException ex)
            {
                // 最关键的一条：内容绝不清空。让使用者能把内容复制走。
                QuickNoteApp.Log(ex.Message);
                _chrome.Bottom.SetPlain("保存失败，内容已保留：" + ex.Message, _palette.Error);
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

            return DateFormat.ShortHeading(today) + "　·　今天已记 " + count + " 条";
        }

        private void RefreshChrome()
        {
            if (_mode == BarMode.Search)
            {
                _chrome.Top.Text = "搜索条目";
                if (_confirmDelete) return;      // 确认文案由 EnterDeleteConfirm 负责，别覆盖掉

                _chrome.Bottom.SetHint(
                    KeyHint.Key("↑↓", KeyHint.Gap.None), KeyHint.Word("选择"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("空格"), KeyHint.Word("完成"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("退格"), KeyHint.Word("删除"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("Enter"), KeyHint.Word("打开"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("Esc"), KeyHint.Word("返回"));
            }
            else
            {
                _chrome.Top.Text = TopText();

                _chrome.Bottom.SetHint(
                    KeyHint.Key("Enter", KeyHint.Gap.None), KeyHint.Word("保存"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("Shift+Enter"), KeyHint.Word("换行"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("Tab"), KeyHint.Word("搜索"),
                    KeyHint.Word("·", KeyHint.Gap.Wide),
                    KeyHint.Key("Esc"), KeyHint.Word("收起"));
            }
        }

        // ---- 搜索模式 ----

        private void OnSearchKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Down) { _results.MoveSelection(1); e.SuppressKeyPress = true; return; }
            if (e.KeyCode == Keys.Up) { _results.MoveSelection(-1); e.SuppressKeyPress = true; return; }

            // 先判 action 再判键本身：Esc 在非确认态走「切回填写」，在确认态走「取消」。
            // 反过来写的话 Esc 会直接切模式，确认态永远退不出来。
            var action = InputRules.DecideSearchKey(_queryBox.Text, _confirmDelete, ToSearchKey(e.KeyCode));

            if (action == SearchKeyAction.ToggleDone)
            {
                e.SuppressKeyPress = true;
                ToggleSelected();
                return;
            }
            if (action == SearchKeyAction.AskDelete)
            {
                e.SuppressKeyPress = true;
                EnterDeleteConfirm();
                return;
            }
            if (action == SearchKeyAction.ConfirmDelete)
            {
                e.SuppressKeyPress = true;
                DoDelete();
                return;
            }
            if (action == SearchKeyAction.CancelDelete)
            {
                e.SuppressKeyPress = true;
                CancelDeleteConfirm();
                return;
            }

            if (e.KeyCode == Keys.Enter) { _results.ActivateSelected(); e.SuppressKeyPress = true; return; }

            // Tab / Esc 不在这里处理：它们是对话框键，到不了这个 KeyDown。
            // 统一由窗体上的 ProcessCmdKey 兜（见那里）。
        }

        private void EnterDeleteConfirm()
        {
            var hit = _results.SelectedHit;
            if (hit == null) return;

            _confirmDelete = true;
            _confirmHit = hit;              // 记住确认框里说的是哪一条
            _results.SetDanger(true);

            EntryInfo info;
            EntryLine.TryParse(hit.Line, out info);
            var what = info == null ? hit.Line : info.Content;
            if (what.Length > 20) what = what.Substring(0, 20) + "…";

            _chrome.Bottom.SetPlain("删除「" + what + "」？　Enter 确认　·　Esc 取消", _palette.Error);
        }

        private void CancelDeleteConfirm()
        {
            if (!_confirmDelete) return;
            _confirmDelete = false;
            _confirmHit = null;
            _results.SetDanger(false);
            RefreshChrome();
        }

        /// <summary>
        /// 真删。删除要重写当天文件——spec 第 5 节靠「只追加」换来的「崩溃不丢前面
        /// 内容」在这条路径上没有了，所以 NoteStore 用临时文件 + 原子替换，
        /// 并在当天首次改写前留一份 .bak。
        /// </summary>
        private void DoDelete()
        {
            var hit = _results.SelectedHit;

            // 只有选中的还是当初那一条才真删。中间若改了查询词（结果重排、选中回到
            // 第一条），或用鼠标点了别的行，列表里已经是**另一批**对象了——这时按
            // Enter 会删掉一条确认框里根本没提过的记录。对象身份就足以识别这件事。
            if (hit == null || !ReferenceEquals(hit, _confirmHit))
            {
                CancelDeleteConfirm();
                return;
            }

            try
            {
                if (!_store.DeleteEntry(hit.Date, hit.LineNumber, hit.Line))
                {
                    _searchStatus.Text = "这条记录已经变了，已重新载入";
                }
            }
            catch (NoteStoreException ex)
            {
                QuickNoteApp.Log(ex.Message);
                _searchStatus.Text = "删除失败：" + ex.Message;
            }

            CancelDeleteConfirm();
            _search.Query(_lastQuery);
        }

        private static SearchKey ToSearchKey(Keys key)
        {
            if (key == Keys.Space) return SearchKey.Space;
            if (key == Keys.Back) return SearchKey.Backspace;
            if (key == Keys.Enter) return SearchKey.Enter;
            if (key == Keys.Escape) return SearchKey.Escape;
            return SearchKey.Other;
        }

        /// <summary>
        /// 切换选中条目的完成状态。这一下就是写到盘上的那一下——屏幕上脸变了，
        /// 文件里对应的 "- [ ]" 也跟着变成 "- [x]"。
        /// </summary>
        private void ToggleSelected()
        {
            var hit = _results.SelectedHit;
            if (hit == null) return;

            EntryInfo info;
            if (!EntryLine.TryParse(hit.Line, out info)) return;

            try
            {
                if (!_store.SetDone(hit.Date, hit.LineNumber, hit.Line, !info.IsDone))
                {
                    // 文件在列表生成之后被改过。不写，重新列一遍让使用者看到真实内容。
                    _searchStatus.Text = "这条记录已经变了，已重新载入";
                    _search.Query(_lastQuery);
                    return;
                }
            }
            catch (NoteStoreException ex)
            {
                QuickNoteApp.Log(ex.Message);
                _searchStatus.Text = "改写失败：" + ex.Message;
                return;
            }

            _search.Query(_lastQuery);
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
            var before = Width + "x" + Height;
            CancelDeleteConfirm();   // 切模式的瞬间确认态必须失效，否则 Enter 会删掉看不见的东西
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

            // DIAG（临时）：切完之后尺寸与可见性到底变成什么样、焦点在谁身上。
            // 「按 Tab 回不去」要么是这一步没跑，要么是焦点压根没到处理函数上。
            QuickNoteApp.Log("DIAG switch-to " + mode + ": " + before + " -> " + Width + "x" + Height
                + ", input=" + _input.Visible + ", results=" + _results.Visible
                + ", searchRow=" + _searchRow.Visible + ", 焦点=" + FocusName()
                + ", 调用者=" + CallerName());
        }

        /// <summary>
        /// DIAG（临时）：谁调的 SwitchTo。启动时出现了一行 switch-to，而 Main 里没有
        /// 任何地方会调它，使用者又说小条不会自己弹——所以要直接问调用栈，别再猜。
        /// </summary>
        private static string CallerName()
        {
            var frames = new System.Diagnostics.StackTrace(2, false);
            if (frames.FrameCount == 0) return "(未知)";
            var m = frames.GetFrame(0).GetMethod();
            return (m.DeclaringType == null ? "" : m.DeclaringType.Name + ".") + m.Name;
        }

        /// <summary>DIAG（临时）：焦点在哪个控件上。</summary>
        private string FocusName()
        {
            var c = ActiveControl;
            if (c == null) return "(不在本窗体)";
            if (c == _input) return "input";
            if (c == _queryBox) return "queryBox";
            if (c == _results) return "results";
            return c.GetType().Name;
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
            CancelDeleteConfirm();
            RefreshChrome();
        }

        /// <summary>设置里改了主题后重新着色。</summary>
        public void ApplyTheme(Palette palette)
        {
            _palette = palette;
            BackColor = palette.Paper;
            _content.BackColor = palette.Paper;
            _input.BackColor = palette.Paper;
            _input.ForeColor = palette.Ink;
            _input.Font = palette.Body;
            _queryBox.BackColor = palette.Paper;
            _queryBox.ForeColor = palette.Ink;
            _queryBox.Font = palette.Body;
            _chrome.ApplyTheme(palette);
            _searchStatus.ForeColor = palette.Muted;
            _chrome.ApplyHairline(palette.Hairline);
            _results.ApplyTheme(palette);   // 结果列表自己持有配色，换主题得告诉它
            _caret.SetColors(palette);
            MarkdownHighlighter.Apply(_input, palette);
            ApplySize();          // 行高跟着字体走，换主题后高度要重算
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
            CancelDeleteConfirm();
            _draft = _input.Text;              // 记录模式草稿保留
            _search.Cancel();
            _lastQuery = "";                   // 搜索词与结果在小条隐藏时丢弃
            _lastResult = SearchResult.Empty();
            _queryBox.Text = "";
            _results.SetResult(_lastResult, "");
            _caret.Stop();
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
                _caret.Follow();
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
