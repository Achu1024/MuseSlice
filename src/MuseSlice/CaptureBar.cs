using System;
using System.Drawing;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 悬浮小条。无边框、置顶、不进任务栏、带投影与圆角。同一窗口两种模式。
    /// </summary>
    internal sealed class CaptureBar : Form
    {
        /// <summary>
        /// 设计基准尺寸（96 DPI 下的逻辑像素）。**两个模式共用同一组。**
        /// <para>
        /// 原先填写页宽 560、搜索页宽 600 高 560，切换时窗口既缩放又位移，还伴随两次
        /// 布局——那就是「闪」。定死尺寸之后，切换只剩「谁可见」和「两条辅助行写什么」，
        /// 窗口纹丝不动。代价是写短句时下方留一片空白，这是换来的确定性。
        /// </para>
        /// </summary>
        private const int LogicalWidth = 560;
        private const int LogicalHeight = 340;

        /// <summary>
        /// 搜索页多出来的那一行查询框。
        /// <para>
        /// 给得比查询框本身宽裕，上面再留一道和正文区一样的内边距——不然查询框会紧贴
        /// 顶上的分隔线，连带着里面那根光标也像从线上长出来的。
        /// </para>
        /// </summary>
        private const int LogicalSearchRow = 34;

        /// <summary>查询行上方的留白。与正文区那道对齐，两个页面的首行才在同一高度上。</summary>
        private const int LogicalSearchRowPadTop = 8;

        /// <summary>搜索行右侧状态文字的宽度。</summary>
        private const int LogicalStatusWidth = 210;

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
        private readonly CaretOverlay _queryCaret;   // 同一个类，给搜索页的查询框用
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
        private int _lastBoxTop = int.MinValue;
        private int _lastBoxHeight = int.MinValue;

        /// <summary>DIAG（临时）：日志行数上限，免得刷屏。</summary>
        private int _diagLines;

        /// <summary>上一次重排时的文本，见 Highlight。null 表示还没排过。</summary>
        private string _highlighted;

        private SearchResult _lastResult = SearchResult.Empty();

        /// <summary>标记搜索结果是否来自勾选刷新，用于保持选中位置。</summary>
        private bool _keepSearchPosition;

        /// <summary>仅在保存出错后短暂启用，用来把红字提示恢复成常规提示。平时不跑。</summary>
        private Timer _errorReset;

        /// <summary>失焦收起的兜底，见 <see cref="StartForegroundWatch"/>。只在可见时跑。</summary>
        private Timer _foregroundWatch;

        /// <summary>这一次显示期间有没有真的抢到过前台。没抢到过就不许兜底收窗口。</summary>
        private bool _wasForeground;

        /// <summary>还没抢到前台时补了几次。</summary>
        private int _foregroundRetries;

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

            // 查询框用同一根自绘光标。系统那根既与字身同高、又跟填写页这根不同位，
            // 一个界面里两根竖线长得不一样，来回切的时候看得出来。
            // 它挂在查询行上而不是 _content 上：子控件会被父控件裁掉，而查询行不在
            // _content 里面。它是非停靠控件，提前放到 z 序最前不影响那两个的停靠。
            _queryCaret = new CaretOverlay { Parent = _searchRow };
            _queryCaret.BringToFront();
            _queryCaret.SetColors(palette);
            _queryBox.GotFocus += delegate { _queryCaret.Attach(_queryBox, _searchRow, _scale); };
            _queryBox.LostFocus += delegate { _queryCaret.Stop(); };

            // 停靠顺序由 z-order 决定：WinForms 从末尾往前停靠，先加的 Fill 最后停靠。
            // 所以 Fill 必须最先加，上下各条才能在它之外拿到自己的空间。
            Controls.Add(_content);
            Controls.Add(_searchRow);
            Controls.Add(_chrome.Bottom);
            Controls.Add(_chrome.Top);

            _search = new SearchController(this, _store.NotesDir, OnSearchResult, OnSearchStarted);

            ApplySize();

            // 建完就把句柄定下来。不这么做的话，句柄要等第一次 Show 才创建，而
            // OnHandleCreated 要到那时才查得到真实 DPI：于是首次弹出走的还是
            // 「96 DPI 的尺寸」，在 125% 的机器上就是先按 560×340 摆好、再改成
            // 700×425。日志里看得很清楚——启动后第一次弹出记的是 560×340，
            // 之后每一次都是 700×425。那次改动本身就发生在窗口正要显示的时候，
            // 就是「闪一下再打开」。
            //
            // 还有一层更花钱的：RichEdit 第一次拿到句柄要加载并初始化，那一帧的绘制
            // 明显比之后每一帧慢。放到启动时做，托盘程序本来就闲着，这点开销白送。
            var unused = Handle;
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
                LogLayoutIfMoved(box);

                // 文本没变就**整段早退**。重排本身会引发 EN_CHANGE（RichEdit 为格式变化
                // 送出的），所以一次上屏会走进来三次；只挡住重排不够——ExpandTodoMarker
                // 与 caret.Follow 也照样跑一遍。三次叠在一起就是「抖」。
                // 文本没变，这一整段本来就没有任何事可做。
                if (!box.IsComposing && box.Text == _highlighted)
                {
                    if (_diagLines++ < 60)
                        MuseSliceApp.Log("DIAG 文本变更: 长度=" + box.TextLength + " [文本未变，整段跳过]");
                    return;
                }

                if (_diagLines++ < 60)
                {
                    MuseSliceApp.Log("DIAG 文本变更: 组合中=" + box.IsComposing
                        + " 长度=" + box.TextLength
                        + (box.IsComposing ? " [组合中，跳过]" : " [重排]"));
                }

                // 输入法组合期间**什么都不做**：那段时间文本归 IME 管。
                // 高亮器每次都会给全文重设字体、关开重绘、再 ScrollToCaret 把视图
                // 拉回原处——而组合中字符索引和组合串边界都处于中间态，这三下都会
                // 让文字跳。中文输入时看到「文字抖动、往下沉」就是这么来的。
                // 组合结束后补一次即可（见下面的 CompositionEnded）。
                if (box.IsComposing) return;

                ExpandTodoMarker(box);
                Highlight(box);

                // 文本变更后延迟更新光标，确保布局完全稳定
                BeginInvoke((Action)delegate { _caret.Follow(); });
            };

            // 组合结束后补上：组合期间跳过了着色，而那次上屏的文本变更又先于
            // WM_IME_ENDCOMPOSITION 到达，已经被上面挡掉了。
            box.CompositionEnded += delegate
            {
                if (_diagLines++ < 60)
                {
                    MuseSliceApp.Log("DIAG 组合结束: 长度=" + box.TextLength + " [重排]");
                }
                Highlight(box);

                // 使用双重 BeginInvoke 确保光标更新在布局完全稳定后进行
                // 第一层：等待高亮完成
                // 第二层：等待 RichTextBox 内部布局更新
                BeginInvoke((Action)delegate
                {
                    BeginInvoke((Action)delegate
                    {
                        _caret.Follow();
                    });
                });
            };

            // 光标只在有焦点时存在：失焦立刻停表，待机没有任何计时器在跑。
            box.GotFocus += delegate { _caret.Attach(box, _content, _scale); };
            box.LostFocus += delegate { _caret.Stop(); };
            return box;
        }

        /// <summary>
        /// 重排的入口。**文本没变就不重排。**
        /// <para>
        /// 重排只改格式，但 RichEdit 会为此送出 EN_CHANGE——于是「组合结束」里那次
        /// 重排又触发一次 TextChangedByUser，同一个字符连着重排三遍。而每次重排都要
        /// 关开重绘 + 全量重画，几次挨在一起看着就是「抖」。这在中文上必然发生
        /// （组合串的格式在上屏时变过），拉丁字母则不会——这就是为什么只有中文抖。
        /// </para>
        /// <para>
        /// 文本没变就没有重排的理由：重排是幂等的，同样的文本重排出来是同样的结果。
        /// </para>
        /// </summary>
        private void Highlight(BarTextBox box)
        {
            var text = box.Text;
            if (_highlighted != null && text == _highlighted) return;
            _highlighted = text;
            // 关掉「文本变更」通知再重排：RichEdit 会为格式变化送出 EN_CHANGE，
            // 不关的话这次重排会把自己再拽回来一趟（日志里那两次「文本未变」）。
            box.WithoutTextChanged(delegate { MarkdownHighlighter.Apply(box, _palette); });
        }

        /// <summary>DIAG（临时）：首字纵坐标或窗体高度一变就记一行。</summary>
        private void LogLayoutIfMoved(BarTextBox box)
        {
            int top, formHeight, lines;
            Point charPos;
            try
            {
                charPos = box.GetPositionFromCharIndex(0);
                top = charPos.Y;
                formHeight = Height;
                lines = box.GetLineFromCharIndex(box.TextLength) + 1;
            }
            catch { return; }

            // 始终记录，不管有没有变化，这样能看到控件位置的变化
            var delta = top - _lastTextTop;
            var boxTopChanged = box.Top != _lastBoxTop;
            var boxHeightChanged = box.Height != _lastBoxHeight;

            if (top != _lastTextTop || formHeight != _lastFormHeight || boxTopChanged || boxHeightChanged)
            {
                _lastTextTop = top;
                _lastFormHeight = formHeight;
                _lastBoxTop = box.Top;
                _lastBoxHeight = box.Height;

                // 获取控件在窗口中的绝对位置
                var boxScreenPos = box.PointToScreen(new Point(0, 0));

                MuseSliceApp.Log("DIAG 布局变化: 首字y=" + top + " (Δ=" + delta + ") 窗体高=" + formHeight
                    + " 输入框高=" + box.Height + " 输入框Top=" + box.Top + " 输入框屏幕Y=" + boxScreenPos.Y
                    + " 行数=" + lines
                    + " 组合中=" + box.IsComposing + " 长度=" + box.TextLength
                    + " Font高=" + box.Font.Height + " 字号=" + box.Font.SizeInPoints);
            }
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
            // 高度与上边距由 ApplySize 按 DPI 设，这里只给个起点。
            var row = new Panel { Dock = DockStyle.Top, Height = LogicalSearchRow, Visible = false };
            row.Controls.Add(_queryBox);
            row.Controls.Add(_searchStatus);
            return row;
        }

        /// <summary>保存成功。回执音与右下角的提示由 MuseSliceApp 接。</summary>
        public event Action Saved;

        /// <summary>记录模式的草稿（未保存的内容）。退出前要拿它问一句。</summary>
        public string Draft { get { return _input.Text; } }

        // ---- 尺寸 ----

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

            // 这两个原先写的是物理像素（26 / 210），高 DPI 下不跟着长——一并纳入缩放。
            _searchRow.Height = (int)Math.Round(LogicalSearchRow * _scale);
            _searchRow.Padding = new Padding(0, (int)Math.Round(LogicalSearchRowPadTop * _scale), 0, 0);
            _searchStatus.Width = (int)Math.Round(LogicalStatusWidth * _scale);

            // 顶部信息行左侧的品牌标志：占辅助行高的六成，其余留给日期与条数。
            _chrome.SetLogoInset((int)Math.Round(ChromeLinePx * 0.62));

            // 两个模式同一个尺寸，一次到位。不再随内容长高——输入框是 Dock=Fill，
            // 写长了由 RichTextBox 自己滚动；窗体跟着长高只会让每次切模式都缩放一遍。
            Width = (int)Math.Round(LogicalWidth * _scale);
            Height = (int)Math.Round(LogicalHeight * _scale);
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
                    MuseSliceApp.Log("DIAG Tab/Esc 落在搜索页的处理函数上 → 切回填写");
                    SwitchTo(BarMode.Record);
                    return true;
                }
                if (key == Keys.Escape)
                {
                    MuseSliceApp.Log("DIAG Tab/Esc 落在搜索页的处理函数上 → 切回填写");
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
                MuseSliceApp.Log("DIAG Tab 落在填写页的处理函数上 → "
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
                // 回执（一声「叮咚」+ 右下角闪一下）由 MuseSliceApp 负责——它才拿得到
                // 托盘与回执窗口；这里只报「存成了」。
                var saved = Saved;
                if (saved != null) saved();
                return true;
            }
            catch (NoteStoreException ex)
            {
                // 最关键的一条：内容绝不清空。让使用者能把内容复制走。
                MuseSliceApp.Log(ex.Message);
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
                MuseSliceApp.Log(ex.Message);
                _searchStatus.Text = "删除失败：" + ex.Message;
            }

            CancelDeleteConfirm();
            _keepSearchPosition = true;
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
                    _keepSearchPosition = true;
                    _search.Query(_lastQuery);
                    return;
                }
            }
            catch (NoteStoreException ex)
            {
                MuseSliceApp.Log(ex.Message);
                _searchStatus.Text = "改写失败：" + ex.Message;
                return;
            }

            _keepSearchPosition = true;
            _search.Query(_lastQuery);
        }

        private void OnSearchStarted()
        {
            _searchStatus.Text = "搜索中…";
        }

        private void OnSearchResult(SearchResult result)
        {
            _lastResult = result;

            if (_keepSearchPosition)
            {
                _results.SetResultKeepPosition(result, _lastQuery);
                _keepSearchPosition = false;
            }
            else
            {
                _results.SetResult(result, _lastQuery);
            }

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

            // 尺寸两个模式完全一样，所以这里不再有 ApplySize/Reposition——那两次都是
            // 多余的布局，而布局正是切换时那一下闪的来源。
            //
            // 可见性切换用 Suspend/ResumeLayout 包住：三个控件的 Visible 各自都会触发
            // 一次重排，不包的话中间那一两个状态会被真的画出来。
            SuspendLayout();
            _input.Visible = !searching;
            _results.Visible = searching;
            _searchRow.Visible = searching;
            ResumeLayout(true);

            // 光标归谁由模式定，另一根显式停掉。
            //
            // 不能指望「把输入框藏起来会顺带触发 LostFocus，于是那根自己停掉」——
            // 本窗体当时没拿到焦点的话，那个事件压根不发生。结果就是搜索页里同时
            // 竖着两根：查询框那根会跟着打字走，正文框那根停在左边一动不动。
            if (searching) _caret.Stop();
            else _queryCaret.Stop();

            if (searching)
            {
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
                RefreshChrome();
                _input.Focus();
            }

            // DIAG（临时）：切完之后尺寸与可见性到底变成什么样、焦点在谁身上。
            // 「按 Tab 回不去」要么是这一步没跑，要么是焦点压根没到处理函数上。
            MuseSliceApp.Log("DIAG switch-to " + mode + ": " + before + " -> " + Width + "x" + Height
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
            _queryCaret.SetColors(palette);
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
            catch (Exception ex) { MuseSliceApp.Log("打开 " + hit.FilePath + " 失败：" + ex.Message); }
        }

        // ---- 进出 ----

        public void OpenBar()
        {
            SwitchTo(BarMode.Record);          // 默认进记录模式，不记忆上次
            _input.Text = _draft;
            _input.SelectionStart = _input.TextLength;
            Highlight(_input);
            ShowBar();
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
            _queryCaret.Stop();                // 收起时两根都停掉：待机不该有计时器在跑
            if (_mode == BarMode.Search) SwitchTo(BarMode.Record);
            HideBar();
        }

        public void ShowBar()
        {
            // 句柄在构造函数末尾就建好了，这里只是兜一道：万一哪条路径先把它销毁了，
            // 也不能让 OnHandleCreated 里的尺寸修正拖到窗口已经可见之后才发生。
            if (!IsHandleCreated) { var unused = Handle; }

            Reposition();
            Show();

            // 必须真正抢到前台，否则小条虽然画出来了，键盘焦点还在原来那个窗口上：
            // Tab、Enter、Esc、打字全部会打到别处，使用者得先用鼠标点一下才正常。
            NativeMethods.ForceForeground(Handle);

            // 抢前台这一步有可能没成，成了也可能中途被摘出去。兜底放在它后面启动，
            // 免得第一拍就撞上「还没抢到」那一瞬，把小条误收掉。
            StartForegroundWatch();

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
            StopForegroundWatch();   // 收起了就没有「还在不在前台」这回事了
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            CloseBar();   // 失焦隐藏但不清空内容，防手滑
        }

        /// <summary>
        /// 失焦收起的兜底。
        /// <para>
        /// 正常路径是 <see cref="OnDeactivate"/>：Windows 发 WM_ACTIVATE(WA_INACTIVE)，
        /// WinForms 把它变成事件，我们收起。但那条路有个前提——**窗口真的成为过前台**。
        /// 实测遇到「小条可见、已经不是前台，而 WA_INACTIVE 始终不来」：这时它就永远
        /// 停在屏幕上，点哪儿都不收。
        /// </para>
        /// <para>
        /// 原因在 <see cref="NativeMethods.ForceForeground"/> 走的 AttachThreadInput：
        /// 摘除失败、或者前台在查询与附加之间换了人，我们的线程就会被挂进别的输入
        /// 队列。那种状态下打字照常能打（输入是共享的），但失活通知不再单独送到我们
        /// 这里——于是 OnDeactivate 这条路永远不会响。
        /// </para>
        /// <para>
        /// 所以再加一道不看通知的：小条可见期间隔一会儿问一次「现在前台还是不是我」。
        /// 计时器只在可见时跑，一收起就停，待机时进程里依然没有任何计时器在跑。
        /// </para>
        /// </summary>
        private void StartForegroundWatch()
        {
            if (_foregroundWatch == null)
            {
                _foregroundWatch = new Timer { Interval = 250 };
                _foregroundWatch.Tick += delegate { WatchForeground(); };
            }

            _wasForeground = false;
            _foregroundRetries = 0;
            _foregroundWatch.Stop();
            _foregroundWatch.Start();
        }

        private void WatchForeground()
        {
            if (!Visible || IsDisposed) { _foregroundWatch.Stop(); return; }

            if (NativeMethods.GetForegroundWindow() == Handle)
            {
                _wasForeground = true;      // 抢到了。往后一旦不是它，就是真失焦
                _foregroundRetries = 0;
                return;
            }

            // 还没抢到过前台：ForceForeground 偶尔会被系统拒（比如从第二个实例、
            // 或者不是由使用者输入拉起来的时候）。补几次，然后就不再纠缠——
            // 但**绝不因此收起**：刚弹出来就收掉，使用者双击完屏幕上什么都没有，
            // 比留着更糟。留着至少能用鼠标点一下，而一点它就成了前台，
            // _wasForeground 转真，兜底从此生效。
            if (!_wasForeground)
            {
                if (_foregroundRetries++ < 6) NativeMethods.ForceForeground(Handle);
                return;
            }

            CloseBar();     // 与 OnDeactivate 同一个出口，草稿照样保留
        }

        private void StopForegroundWatch()
        {
            if (_foregroundWatch != null) _foregroundWatch.Stop();
        }
    }
}
