using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 自绘结果列表。每条三行：日期与星期、匹配行（前带勾选框，命中处高亮）、上下文行。
    /// 用自绘而不是 ListBox，是为了在一条里画出三种字号、两种底纹和一个矢量勾选框。
    /// <para>
    /// 行高与各段的上下位置**全部由字体度量与 DPI 推出来**，不写死像素。原先那些写死的
    /// 数字在 125% 下对不上放大后的字体：文字按 DPI 放大了，包着它的矩形却没有，
    /// 正文底部约四分之一被切掉。
    /// </para>
    /// </summary>
    internal sealed class SearchResultsView : Control
    {
        private Palette _p;      // 非 readonly：换主题时要换掉
        private SearchResult _result = SearchResult.Empty();
        private int _selected;
        private int _firstVisible;
        private string _query = "";
        private bool _danger;

        private float _scale = 1f;

        // 由字体度量推出来的行几何，见 Measure()。
        private int _pad, _gap;
        private int _markSize, _dateHeight, _bodyHeight, _contextHeight;
        private int _topPad, _bodyTop, _contextTop, _rowHeight;

        public event Action<SearchHit> Activated;

        public SearchResultsView(Palette p)
        {
            _p = p;
            DoubleBuffered = true;

            // 纯展示的列表，绝不拿焦点。Control 默认是可获得焦点的，一旦它拿到了，
            // 搜索页所有按键（↑↓、空格、Backspace、Tab）就都不再送到查询框的处理函数，
            // 表现是「按 Tab 回不去、卡在搜索界面」。
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = p.Paper;
            Measure();
        }

        /// <summary>换主题后重新着色并重算行几何（字号可能变）。</summary>
        public void ApplyTheme(Palette p)
        {
            _p = p;
            BackColor = p.Paper;
            Measure();
            Invalidate();
        }

        public SearchHit SelectedHit
        {
            get
            {
                if (_result.Hits.Count == 0) return null;
                return _result.Hits[_selected];
            }
        }

        public void SetResult(SearchResult result, string query)
        {
            _result = result ?? SearchResult.Empty();
            _query = query ?? "";
            _selected = 0;
            _firstVisible = 0;
            Invalidate();
        }

        /// <summary>删除确认态：选中行改用错误色底纹，让「这一下会删掉」看得见。</summary>
        public void SetDanger(bool danger)
        {
            if (_danger == danger) return;
            _danger = danger;
            Invalidate();
        }

        private int S(int logical) { return (int)Math.Round(logical * _scale); }

        /// <summary>
        /// 从字体度量与 DPI 推出整行的几何。字号一换、DPI 一变，行高自动跟上——
        /// 写死像素就会切字。
        /// </summary>
        private void Measure()
        {
            _pad = S(10);
            _gap = S(2);
            _topPad = S(4);
            _markSize = S(13);

            // 各段都留出余量：TextRenderer 传矩形画时是**裁剪**到矩形的，
            // 矩形正好等于字高时，一个取整误差就会切掉降部。
            _dateHeight = Math.Max(_p.StampFont.Height, _p.Chrome.Height) + S(2);
            _bodyHeight = _p.Body.Height + S(4);
            _contextHeight = _p.Chrome.Height + S(2);

            _bodyTop = _topPad + _dateHeight + _gap;
            _contextTop = _bodyTop + _bodyHeight + _gap;
            _rowHeight = _contextTop + _contextHeight + _topPad;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // 句柄到手后显示器 DPI 才可查。Control.DeviceDpi 在本机谎报 96，
            // 所以用 GetDpiForWindow（接手须知里记着这一条）。
            try
            {
                var dpi = NativeMethods.GetDpiForWindow(Handle);
                if (dpi >= 96) _scale = dpi / 96f;
            }
            catch { /* 老系统上没有这个 API，保持 1.0 */ }

            Measure();
        }

        private int VisibleRows
        {
            get
            {
                var rows = Height / _rowHeight;
                return rows < 1 ? 1 : rows;
            }
        }

        public void MoveSelection(int delta)
        {
            if (_result.Hits.Count == 0) return;
            _selected += delta;
            if (_selected < 0) _selected = 0;
            if (_selected >= _result.Hits.Count) _selected = _result.Hits.Count - 1;
            ScrollToSelection();
            Invalidate();
        }

        /// <summary>
        /// 让选中项始终留在可视范围内。不做这件事的话，结果多于可视行数时
        /// ↑↓ 会把高亮走到屏幕外，然后回车打开一条使用者根本看不见的记录。
        /// </summary>
        private void ScrollToSelection()
        {
            _firstVisible = SearchScroll.FirstVisible(
                _selected, VisibleRows, _result.Hits.Count, _firstVisible);
        }

        public void ActivateSelected()
        {
            var hit = SelectedHit;
            if (hit != null && Activated != null) Activated(hit);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_p.Paper);

            if (_result.Hits.Count == 0)
            {
                TextRenderer.DrawText(g, "没有找到匹配", _p.Chrome,
                    new Point(_pad, _pad), _p.Muted);
                return;
            }

            var lastVisible = Math.Min(_result.Hits.Count, _firstVisible + VisibleRows);
            for (var i = _firstVisible; i < lastVisible; i++)
            {
                var y = (i - _firstVisible) * _rowHeight;
                if (i == _selected)
                {
                    using (var b = new SolidBrush(_danger ? Color.FromArgb(46, _p.Error) : _p.Selection))
                        g.FillRectangle(b, 0, y, Width, _rowHeight - 1);
                }

                var hit = _result.Hits[i];
                DrawStamp(g, hit, y);
                DrawHitLine(g, hit, y);

                if (!string.IsNullOrEmpty(hit.Context))
                {
                    TextRenderer.DrawText(g, hit.Context, _p.Chrome,
                        new Rectangle(_pad, y + _contextTop, Width - 2 * _pad, _contextHeight),
                        _p.Muted, TextFormatFlags.EndEllipsis);
                }
            }
        }

        /// <summary>日期与星期：日期用第二套字体（Bahnschrift，DIN 系），星期用 chrome 字号。</summary>
        private void DrawStamp(Graphics g, SearchHit hit, int y)
        {
            var day = hit.Date.ToString("MM-dd");
            TextRenderer.DrawText(g, day, _p.StampFont, new Point(_pad, y + _topPad), _p.Stamp);

            // 星期起点跟着日期的实际宽度走，不写死 x——字体的数字宽度不是常数。
            var dayWidth = TextRenderer.MeasureText(g, day, _p.StampFont).Width;
            TextRenderer.DrawText(g, hit.Date.ToString("dddd"), _p.Chrome,
                new Point(_pad + dayWidth + _gap * 2, y + _topPad + S(2)), _p.Muted);
        }

        /// <summary>
        /// 匹配行：勾选框 + 时间戳 + 正文。文件里的 "- [ ]" 不再显示成字面量——
        /// 屏幕上看到的就是那条待办的状态。
        /// <para>
        /// 匹配到的行不一定是记录行（使用者写的续行也可能命中），那种行没有勾选框与
        /// 时间戳可拆，画一个未勾选的框加原样正文。
        /// </para>
        /// </summary>
        private void DrawHitLine(Graphics g, SearchHit hit, int y)
        {
            EntryInfo info;
            var isEntry = EntryLine.TryParse(hit.Line, out info);

            DrawMark(g, isEntry && info.IsDone,
                new Rectangle(_pad, y + _bodyTop, _markSize, _bodyHeight));

            var at = _pad + _markSize + _gap * 2;
            var right = Width - _pad;
            if (right - at <= 0) return;

            if (!isEntry)
            {
                DrawMatchLine(g, hit.Line, new Rectangle(at, y + _bodyTop, right - at, _bodyHeight));
                return;
            }

            var stampWidth = TextRenderer.MeasureText(g, info.Stamp, _p.StampFont).Width;
            TextRenderer.DrawText(g, info.Stamp, _p.StampFont,
                new Rectangle(at, y + _bodyTop, stampWidth + _gap * 2, _bodyHeight),
                _p.Stamp, TextFormatFlags.NoPadding);

            var textAt = at + stampWidth + _gap * 4;
            if (right - textAt <= 0) return;
            DrawMatchLine(g, info.Content, new Rectangle(textAt, y + _bodyTop, right - textAt, _bodyHeight));
        }

        /// <summary>
        /// 画勾选框。矢量画的，不依赖任何字体的字形——所以放大不会糊，也不会像 emoji
        /// 那样在本机退回黑白。不画方框画圆：整块界面近乎无彩，圆比方框软，也更像
        /// 「一个状态」而不是「一个表单控件」。
        /// </summary>
        private void DrawMark(Graphics g, bool done, Rectangle box)
        {
            var d = Math.Min(box.Width, box.Height);
            if (d < 4) return;

            var r = new Rectangle(box.X, box.Y + (box.Height - d) / 2, d, d);
            r.Inflate(-1, -1);
            if (r.Width < 3 || r.Height < 3) return;

            if (!done)
            {
                using (var pen = new Pen(_p.Muted, Math.Max(1f, d * 0.11f)))
                    g.DrawEllipse(pen, r);
                return;
            }

            using (var b = new SolidBrush(_p.InkSoft))
                g.FillEllipse(b, r);

            // 勾用纸色描，等于在实心圆上挖出来——比再画一个深色的勾干净
            using (var pen = new Pen(_p.Paper, Math.Max(1f, d * 0.15f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                var x = (float)r.X; var ty = (float)r.Y;
                var w = (float)r.Width; var h = (float)r.Height;
                g.DrawLines(pen, new[]
                {
                    new PointF(x + w * 0.27f, ty + h * 0.52f),
                    new PointF(x + w * 0.44f, ty + h * 0.70f),
                    new PointF(x + w * 0.75f, ty + h * 0.31f)
                });
            }
        }

        /// <summary>
        /// 逐段画出匹配行，命中处加底纹。按「命中前 → 命中 → 命中后」三段依次量宽绘制，
        /// 每段都做边界钳制，保证超长行不越界、也不把后面的字吃掉。
        /// </summary>
        private void DrawMatchLine(Graphics g, string line, Rectangle box)
        {
            var index = string.IsNullOrEmpty(_query)
                ? -1
                : line.IndexOf(_query, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                TextRenderer.DrawText(g, line, _p.Body, box, _p.Ink, TextFormatFlags.EndEllipsis);
                return;
            }

            var before = line.Substring(0, index);
            var matched = line.Substring(index, _query.Length);
            var after = line.Substring(index + _query.Length);

            var at = box.X;
            var remaining = box.Width;

            var beforeWidth = TextRenderer.MeasureText(g, before, _p.Body).Width;
            if (beforeWidth > remaining) beforeWidth = remaining;
            if (beforeWidth > 0)
            {
                TextRenderer.DrawText(g, before, _p.Body,
                    new Rectangle(at, box.Y, beforeWidth, box.Height), _p.Ink,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            at += beforeWidth;
            remaining -= beforeWidth;
            if (remaining <= 0) return;

            var matchedWidth = TextRenderer.MeasureText(g, matched, _p.Body).Width;
            var drawMatched = Math.Min(matchedWidth, remaining);
            using (var b = new SolidBrush(_p.Highlight))
                g.FillRectangle(b, at - 1, box.Y, drawMatched + 2, box.Height);
            TextRenderer.DrawText(g, matched, _p.Body,
                new Rectangle(at, box.Y, drawMatched, box.Height), _p.Ink,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            at += drawMatched;
            remaining -= drawMatched;
            if (remaining <= 0 || after.Length == 0) return;

            TextRenderer.DrawText(g, after, _p.Body,
                new Rectangle(at, box.Y, remaining, box.Height), _p.Ink,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var index = _firstVisible + e.Y / _rowHeight;
            if (index >= 0 && index < _result.Hits.Count)
            {
                _selected = index;
                ScrollToSelection();
                Invalidate();
                ActivateSelected();
            }
            base.OnMouseDown(e);
        }
    }
}
