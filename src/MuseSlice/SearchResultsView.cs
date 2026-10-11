using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
{
    /// <summary>
    /// 自绘结果列表。
    /// <para>
    /// 结构是**一条导轨 + 一个文字栏**：勾选框独占左侧导轨，其余文字全部落在同一个
    /// 左边缘上。早先的结构有三个左边缘（勾选框、时间戳、正文各一个），眼睛要在三个
    /// 起点之间来回跳，看着就是「没对齐」。
    /// </para>
    /// <para>
    /// 每行两行高：第一行是正文（主角）加右对齐的元信息，第二行是上下文（旧格式的
    /// 多行记录才有，没有就留白）。元信息右对齐是用横向空间换掉一整行——搜索列表本来就
    /// 又宽又矮，纵向才是稀缺的。
    /// </para>
    /// <para>
    /// 行高与各段位置**全部由字体度量与 DPI 推出来**，不写死像素。写死的数字在 125% 下
    /// 对不上放大后的字体，正文底部会被裁掉。
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
        private int _pad, _markSize, _rail;
        private int _line1Height, _topPad, _rowHeight;

        public event Action<SearchHit> Activated;

        public SearchResultsView(Palette p)
        {
            _p = p;
            DoubleBuffered = true;

            // 纯展示的列表，绝不拿焦点。Control 默认是可获得焦点的，一旦它拿到了，
            // 搜索页所有按键就都不再送到查询框的处理函数。
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

        /// <summary>
        /// 更新结果但保持当前选中位置。勾选后刷新列表时用，避免选中跳回第一项。
        /// </summary>
        public void SetResultKeepPosition(SearchResult result, string query)
        {
            var oldSelected = _selected;
            _result = result ?? SearchResult.Empty();
            _query = query ?? "";

            // 保持在原位置，但钳制到新结果范围内
            _selected = oldSelected;
            if (_selected >= _result.Hits.Count) _selected = _result.Hits.Count - 1;
            if (_selected < 0) _selected = 0;

            ScrollToSelection();
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
        /// 从字体度量与 DPI 推出整行的几何。字号一换、DPI 一变，行高自动跟上。
        /// </summary>
        private void Measure()
        {
            _pad = S(12);
            // 查询行为了不贴着分隔线，往上让开了一道（见 CaptureBar.LogicalSearchRowPadTop），
            // 这里相应收紧一点，固定高度下仍然放得下同样多的条数。
            _topPad = S(6);
            _markSize = Math.Max(S(11), _p.ResultIcon.Height);

            // 导轨宽度 = 勾选框 + 它与文字之间的留白。文字栏的左边缘就是 _rail，
            // 全行所有文字都从这里起，这是「对齐」这件事的全部。
            _rail = _pad + _markSize + S(10);

            // 给一点余量：TextRenderer 传矩形画时是**裁剪**到矩形的，
            // 矩形正好等于字高时，一个取整误差就会切掉降部。
            //
            // 取的是两者**较大**的那个，所以两套字号必须一起调小——只降 ResultBody
            // 会被 ResultMeta 兜住，行高一点不降，小字号就白调了。
            _line1Height = Math.Max(_p.ResultBody.Height + S(2), _p.ResultMeta.Height);

            // 一行就是一行。早先每行还预留一条上下文行，但记录改成「每行各成一条待办」
            // 之后上下文几乎永远是空的——那 36% 的行高就白晾着，看着每行都往下坠。
            _rowHeight = _topPad + _line1Height + _topPad;
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
                DrawEmptyState(g);
                return;
            }

            var lastVisible = Math.Min(_result.Hits.Count, _firstVisible + VisibleRows);
            for (var i = _firstVisible; i < lastVisible; i++)
            {
                var y = (i - _firstVisible) * _rowHeight;
                DrawSelection(g, y, i == _selected);
                DrawRow(g, _result.Hits[i], y);
            }
        }

        /// <summary>
        /// 空列表居中一行字。贴着左上角写一行「没有找到匹配」会像一句没人管的旁注；
        /// 居中才读得出「这里就是空的」。
        /// </summary>
        private void DrawEmptyState(Graphics g)
        {
            var box = new Rectangle(_pad, 0, Width - 2 * _pad, Height);
            TextRenderer.DrawText(g, "没有找到匹配", _p.Chrome, box, _p.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawSelection(Graphics g, int y, bool selected)
        {
            if (!selected) return;

            // 圆角，四周留缝：整张卡片本身是圆的，一条顶到边的直角底纹在它里面显得生硬，
            // 像是贴上去的而不是选中的一行。
            var band = new Rectangle(S(3), y + S(2), Width - S(6), _rowHeight - S(5));
            using (var path = RoundedRect(band, S(8)))
            using (var b = new SolidBrush(_danger ? Color.FromArgb(46, _p.Error) : _p.Selection))
                g.FillPath(b, path);
        }

        private void DrawRow(Graphics g, SearchHit hit, int y)
        {
            EntryInfo info;
            var isEntry = EntryLine.TryParse(hit.Line, out info);

            // 导轨：只有真正的记录行才有勾选框。匹配落在旧格式的续行上时导轨留空——
            // 画一个空框等于宣称「这条没完成」，而那一行根本不是一条待办。
            if (isEntry)
            {
                DrawMark(g, info.IsDone, new Rectangle(_pad, y + _topPad, _markSize, _line1Height));
            }

            var content = isEntry ? info.Content : hit.Line;

            // 元信息右对齐：先量出它占多宽，正文才知道自己在哪儿收住。
            var meta = isEntry ? Meta(hit, info) : "";
            var metaWidth = meta.Length == 0
                ? 0
                : TextRenderer.MeasureText(g, meta, _p.ResultMeta).Width;

            var metaLeft = Width - _pad - metaWidth;
            var right = meta.Length == 0 ? Width - _pad : metaLeft - S(16);
            var room = right - _rail;

            if (room > 0)
            {
                // 正文按自己的实际宽度占位，不留满——上下文要接在它后面。
                var contentWidth = Math.Min(TextRenderer.MeasureText(g, content, _p.ResultBody).Width, room);
                DrawMatchLine(g, content, new Rectangle(_rail, y + _topPad, contentWidth, _line1Height));

                // 上下文当正文的尾巴，用更浅的一档。旧格式的多行记录才有续行，
                // 新格式每条自成一行，所以它通常根本不出现——出现了就该看得见，
                // 但不该为此占一整行。
                if (!string.IsNullOrEmpty(hit.Context))
                {
                    var tailX = _rail + contentWidth + S(14);
                    if (tailX < right)
                    {
                        TextRenderer.DrawText(g, hit.Context, _p.ResultBody,
                            new Rectangle(tailX, y + _topPad, right - tailX, _line1Height),
                            _p.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    }
                }
            }

            if (metaWidth > 0)
            {
                TextRenderer.DrawText(g, meta, _p.ResultMeta,
                    new Rectangle(metaLeft, y + _topPad, metaWidth, _line1Height),
                    _p.Stamp, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        }

        /// <summary>
        /// 右对齐那一段：日期与时间。整段用 Bahnschrift——第二套字体本来就是为了数字
        /// 存在的，这里全是数字，正好。不带星期：几条结果常常同一天，它是重复的噪声，
        /// 而且混进来会逼这一段用两种字体。
        /// </summary>
        private static string Meta(SearchHit hit, EntryInfo info)
        {
            return hit.Date.ToString("MM-dd") + " " + info.Stamp;
        }

        /// <summary>
        /// 画勾选框。取 Windows 自带图标字体（Segoe Fluent Icons / Segoe MDL2 Assets）
        /// 里的两个「Checkbox」字形——一套专业设计的单色矢量图标，零第三方依赖，
        /// 放大不糊，也不像 emoji 那样会退回黑白。填写页与回执用的是同一对字形。
        /// </summary>
        private void DrawMark(Graphics g, bool done, Rectangle box)
        {
            var mark = done ? EntryLine.DoneMark : EntryLine.UndoneMark;
            TextRenderer.DrawText(g, mark, _p.ResultIcon, box, _p.InkSoft,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        /// <summary>
        /// 圆角矩形路径。GDI+ 没有现成的，用四段圆弧拼。半径大到放不下时退回直角，
        /// 免得 AddArc 抛出或者画出翻卷的图形。
        /// </summary>
        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d = radius * 2;

            if (d <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
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
                TextRenderer.DrawText(g, line, _p.ResultBody, box, _p.Ink,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                return;
            }

            var before = line.Substring(0, index);
            var matched = line.Substring(index, _query.Length);
            var after = line.Substring(index + _query.Length);

            var at = box.X;
            var remaining = box.Width;

            var beforeWidth = TextRenderer.MeasureText(g, before, _p.ResultBody).Width;
            if (beforeWidth > remaining) beforeWidth = remaining;
            if (beforeWidth > 0)
            {
                TextRenderer.DrawText(g, before, _p.ResultBody,
                    new Rectangle(at, box.Y, beforeWidth, box.Height), _p.Ink,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            at += beforeWidth;
            remaining -= beforeWidth;
            if (remaining <= 0) return;

            var matchedWidth = TextRenderer.MeasureText(g, matched, _p.ResultBody).Width;
            var drawMatched = Math.Min(matchedWidth, remaining);
            using (var b = new SolidBrush(_p.Highlight))
                g.FillRectangle(b, at - 1, box.Y, drawMatched + 2, box.Height);
            TextRenderer.DrawText(g, matched, _p.ResultBody,
                new Rectangle(at, box.Y, drawMatched, box.Height), _p.Ink,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            at += drawMatched;
            remaining -= drawMatched;
            if (remaining <= 0 || after.Length == 0) return;

            TextRenderer.DrawText(g, after, _p.ResultBody,
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
