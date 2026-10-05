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
        private string _query = "";

        public event Action<SearchHit> Activated;

        public SearchResultsView(Palette p)
        {
            _p = p;
            DoubleBuffered = true;
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            BackColor = p.BarBack;
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
            Invalidate();
        }

        public void MoveSelection(int delta)
        {
            if (_result.Hits.Count == 0) return;
            _selected += delta;
            if (_selected < 0) _selected = 0;
            if (_selected >= _result.Hits.Count) _selected = _result.Hits.Count - 1;
            Invalidate();
        }

        public void ActivateSelected()
        {
            var hit = SelectedHit;
            if (hit != null && Activated != null) Activated(hit);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(_p.BarBack);

            if (_result.Hits.Count == 0)
            {
                TextRenderer.DrawText(g, "没有找到匹配", _p.Small,
                    new Point(12, 12), _p.Muted);
                return;
            }

            var lastVisible = Math.Min(_result.Hits.Count, Height / RowHeight);
            for (var i = 0; i < lastVisible; i++)
            {
                var y = i * RowHeight;
                if (i == _selected)
                {
                    using (var b = new SolidBrush(_p.Selection))
                        g.FillRectangle(b, 0, y, Width, RowHeight - 2);
                }

                var hit = _result.Hits[i];
                TextRenderer.DrawText(g, hit.Date.ToString("MM-dd dddd"), _p.Small,
                    new Point(10, y + 4), _p.Muted);

                DrawMatchLine(g, hit.Line, new Rectangle(10, y + 21, Width - 20, 20));

                if (!string.IsNullOrEmpty(hit.Context))
                {
                    TextRenderer.DrawText(g, hit.Context, _p.Small,
                        new Rectangle(10, y + 40, Width - 20, 18), _p.Muted,
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
                TextRenderer.DrawText(g, line, _p.Body, box, _p.Text, TextFormatFlags.EndEllipsis);
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
                    new Rectangle(at, box.Y, beforeWidth, box.Height), _p.Text,
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            at += beforeWidth;
            remaining -= beforeWidth;
            if (remaining <= 0) return;

            var matchedWidth = TextRenderer.MeasureText(g, matched, _p.Body).Width;
            var drawMatched = Math.Min(matchedWidth, remaining);
            using (var b = new SolidBrush(_p.Selection))
                g.FillRectangle(b, at - 1, box.Y, drawMatched + 2, box.Height);
            TextRenderer.DrawText(g, matched, _p.Body,
                new Rectangle(at, box.Y, drawMatched, box.Height), _p.Todo,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            at += drawMatched;
            remaining -= drawMatched;
            if (remaining <= 0 || after.Length == 0) return;

            TextRenderer.DrawText(g, after, _p.Body,
                new Rectangle(at, box.Y, remaining, box.Height), _p.Text,
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            var index = e.Y / RowHeight;
            if (index >= 0 && index < _result.Hits.Count)
            {
                _selected = index;
                Invalidate();
                ActivateSelected();
            }
            base.OnMouseDown(e);
        }
    }
}
