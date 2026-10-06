using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using QuickNote.Core;

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

        /// <summary>填写页那一行开头的勾选框（☐ / ☑）。</summary>
        private static readonly Regex LeadingMark = new Regex(
            "^(" + EntryLine.UndoneMark + "|" + EntryLine.DoneMark + ")", RegexOptions.Compiled);

        public static void Apply(RichTextBox box, Palette p)
        {
            if (box == null || box.IsDisposed || !box.IsHandleCreated) return;
            if (box.TextLength == 0) return;

            // 粘贴一大段文本会打破「内容只有几百字节」这个设计前提。
            // 那时每次按键重扫全文着色会冻住 UI 线程，而 UI 线程一冻就卡住
            // 全局键盘钩子。超过阈值就干脆不着色——纯文本仍然可读，
            // 而打字不会被冻住。
            if (!BarGeometry.ShouldHighlight(box.TextLength)) return;

            var selStart = box.SelectionStart;
            var selLength = box.SelectionLength;
            var firstVisible = box.GetCharIndexFromPosition(new Point(1, 1));

            NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                box.SelectAll();
                box.SelectionColor = p.Ink;
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

            // 勾选框是结构、不是内容：压成 ink-soft，让正文当主角（与列表符同一套纪律）。
            // 不用再指定字体——☐/☑ 是普通 BMP 字形，正文 CJK 字体自带，不走字体回退。
            var mark = LeadingMark.Match(line);
            if (mark.Success) Paint(box, start, mark.Length, p.InkSoft, p.Body);

            // 标题不用颜色，用字重（定案 §2 的「颜色的使用纪律」）。
            if (HeadingLine.IsMatch(line))
            {
                Paint(box, start, line.Length, p.Ink, p.Heading);
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
                // 手打的 "- [ ]"：不换色，只把它压成 ink-soft，让正文仍然是主角
                var markerStart = start + todo.Groups[1].Length;
                Paint(box, start, line.Length, p.Ink, p.Body);
                Paint(box, markerStart, todo.Groups[2].Length, p.InkSoft, p.Body);
            }
            else
            {
                var bullet = BulletMarker.Match(line);
                if (bullet.Success)
                {
                    var markerStart = start + bullet.Groups[1].Length;
                    Paint(box, start, line.Length, p.Ink, p.Body);
                    Paint(box, markerStart, bullet.Groups[2].Length, p.InkSoft, p.Body);
                }
            }

            foreach (Match m in Bold.Matches(line))
            {
                Paint(box, start + m.Index, m.Length, p.Ink, p.Heading);
            }
            foreach (Match m in Code.Matches(line))
            {
                // 行内代码用 ink-soft + 等宽，不加底色——RichEdit 画不出圆角底，方底更丑
                Paint(box, start + m.Index, m.Length, p.InkSoft, p.Mono);
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
