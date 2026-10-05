using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace QuickNote
{
    /// <summary>
    /// 整体重扫全文重新着色。内容只有几百字节，增量高亮的复杂度不值得。
    ///
    /// 两条硬性纪律：
    /// 1) 绝不使用 RichTextBox.Text 赋值——那会清空全部格式，等于每敲一个字
    ///    就把高亮结果抹掉一次。
    /// 2) 重着色前后要保存并恢复光标与滚动位置，并用 WM_SETREDRAW 关闭/开启重绘。
    ///    不做的话使用者看到的是「光标乱跳」和「每敲一个字闪一下」。
    /// </summary>
    internal static class MarkdownHighlighter
    {
        private static readonly Regex HeadingLine = new Regex(@"^\s{0,3}#{1,6}\s", RegexOptions.Compiled);
        private static readonly Regex BulletMarker = new Regex(@"^(\s*)([-*+])(\s)", RegexOptions.Compiled);
        private static readonly Regex TodoMarker = new Regex(@"^(\s*)([-*+]\s\[[ xX]\])(\s)", RegexOptions.Compiled);
        private static readonly Regex QuoteLine = new Regex(@"^\s{0,3}>\s?", RegexOptions.Compiled);
        private static readonly Regex Bold = new Regex(@"\*\*[^*\n]+\*\*", RegexOptions.Compiled);
        private static readonly Regex Code = new Regex(@"`[^`\n]+`", RegexOptions.Compiled);

        public static void Apply(RichTextBox box, Palette p)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated) return;
            if (box.TextLength == 0) return;

            var selStart = box.SelectionStart;
            var selLength = box.SelectionLength;
            var firstVisible = box.GetCharIndexFromPosition(new Point(1, 1));

            NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                box.SelectAll();
                box.SelectionColor = p.Text;
                box.SelectionFont = p.Body;

                var text = box.Text;
                var lineStart = 0;
                while (lineStart <= text.Length)
                {
                    var nl = text.IndexOf('\n', lineStart);
                    var lineEnd = nl < 0 ? text.Length : nl;
                    ColorLine(box, text, lineStart, lineEnd, p);

                    if (nl < 0) break;
                    lineStart = nl + 1;
                }
            }
            finally
            {
                box.Select(selStart, selLength);
                NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                box.Invalidate();

                // 把视图滚回原处，否则每次重着色都会把内容拽回顶部
                if (firstVisible >= 0 && firstVisible < box.TextLength)
                {
                    box.SelectionStart = firstVisible;
                    box.SelectionLength = 0;
                    box.ScrollToCaret();
                    box.SelectionStart = selStart;
                    box.SelectionLength = selLength;
                }
            }
        }

        private static void ColorLine(RichTextBox box, string text, int start, int end, Palette p)
        {
            var line = text.Substring(start, end - start);

            if (HeadingLine.IsMatch(line))
            {
                Paint(box, start, line.Length, p.Accent, p.HeadingFont);
                return;
            }

            if (QuoteLine.IsMatch(line))
            {
                Paint(box, start, line.Length, p.Muted, p.Body);
                return;
            }

            var todo = TodoMarker.Match(line);
            if (todo.Success)
            {
                // 待办标记整段（含 "- [ ]"）用橙色，其余用正文色
                var markerStart = start + todo.Groups[1].Length;
                Paint(box, start, line.Length, p.Text, p.Body);
                Paint(box, markerStart, todo.Groups[2].Length, p.Todo, p.Body);
            }
            else
            {
                var bullet = BulletMarker.Match(line);
                if (bullet.Success)
                {
                    var markerStart = start + bullet.Groups[1].Length;
                    Paint(box, start, line.Length, p.Text, p.Body);
                    Paint(box, markerStart, bullet.Groups[2].Length, p.Accent, p.Body);
                }
            }

            foreach (Match m in Bold.Matches(line))
            {
                Paint(box, start + m.Index, m.Length, p.Text, p.HeadingFont);
            }
            foreach (Match m in Code.Matches(line))
            {
                Paint(box, start + m.Index, m.Length, p.Accent, p.Mono);
            }
        }

        private static void Paint(RichTextBox box, int start, int length, Color color, Font font)
        {
            if (length <= 0) return;
            box.Select(start, length);
            box.SelectionColor = color;
            box.SelectionFont = font;
        }
    }
}
