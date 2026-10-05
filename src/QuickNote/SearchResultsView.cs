using System;
using System.Drawing;
using System.Windows.Forms;
using QuickNote.Core;

namespace QuickNote
{
    /// <summary>
    /// 自绘结果列表。每条三行：日期、匹配行（命中处高亮）、上下文行。
    /// 用自绘而不是 ListBox，是为了在一条里画出三种字号与一种高亮。
    /// </summary>
    internal sealed class SearchResultsView : Control
    {
        private const int RowHeight = 62;

        private readonly Palette _p;
        private SearchResult _result = SearchResult.Empty();
        private int _selected;
        private int _firstVisible;
        private string _query = "";
        private bool _danger;

        public event Action<SearchHit> Activated;

        public SearchResultsView(Palette p)
        {
            _p = p;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = p.Paper;
        }

        /// <summary>删除确认态：选中行改用错误色底纹，让「这一下会删掉」看得见。</summary>
        public void SetDanger(bool danger)
        {
            if (_danger == danger) return;
            _danger = danger;
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

        private int VisibleRows
        {
            get
            {
                var rows = Height / RowHeight;
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
                    new Point(12, 12), _p.Muted);
                return;
            }

            var lastVisible = Math.Min(_result.Hits.Count, _firstVisible + VisibleRows);
            for (var i = _firstVisible; i < lastVisible; i++)
            {
                var y = (i - _firstVisible) * RowHeight;
                if (i == _selected)
                {
                    using (var b = new SolidBrush(_danger ? Color.FromArgb(46, _p.Error) : _p.Selection))
                        g.FillRectangle(b, 0, y, Width, RowHeight - 2);
                }

                var hit = _result.Hits[i];

                // 日期用第二套字体（Bahnschrift，DIN 系，数字有技术感），星期用 chrome 字号。
                // 字号要比 CJK 大一档才压得住——Bahnschrift 的 x-height 偏小。
                TextRenderer.DrawText(g, hit.Date.ToString("MM-dd"), _p.StampFont,
                    new Point(10, y + 4), _p.Stamp);
                TextRenderer.DrawText(g, hit.Date.ToString("dddd"), _p.Chrome,
                    new Point(64, y + 6), _p.Muted);

                DrawMatchLine(g, hit.Line, new Rectangle(10, y + 22, Width - 20, 20));

                if (!string.IsNullOrEmpty(hit.Context))
                {
                    TextRenderer.DrawText(g, hit.Context, _p.Chrome,
                        new Rectangle(10, y + 45, Width - 20, 18), _p.Muted,
                        TextFormatFlags.EndEllipsis);
                }
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
            var index = _firstVisible + e.Y / RowHeight;
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
