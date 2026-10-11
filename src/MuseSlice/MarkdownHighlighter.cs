using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using MuseSlice.Core;

namespace MuseSlice
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

            // 关键修复：使用 HideSelection = true 阻止 Select() 触发自动滚动
            var wasHidden = box.HideSelection;
            box.HideSelection = true;

            NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                // 先全选并重置基础格式
                box.SelectAll();
                box.SelectionColor = p.Ink;
                box.SelectionFont = p.Body;

                // 强制重置段落格式，确保顶部对齐
                // 这可以防止输入法改变段落的垂直对齐方式
                box.SelectionAlignment = System.Windows.Forms.HorizontalAlignment.Left;
                box.SelectionIndent = 0;
                box.SelectionHangingIndent = 0;
                box.SelectionRightIndent = 0;

                // 关键修复：强制固定行高为 27 像素
                // bLineSpacingRule = 4 表示使用绝对高度
                var pf = new NativeMethods.PARAFORMAT2();
                pf.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(pf);
                pf.dwMask = NativeMethods.PFM_LINESPACING;
                pf.dyLineSpacing = 270; // 27 像素 * 10 (单位是 1/20 点 = twips/20)
                pf.bLineSpacingRule = 4; // 4 = 使用绝对高度
                NativeMethods.SendMessage(box.Handle, NativeMethods.EM_SETPARAFORMAT, IntPtr.Zero, ref pf);

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
                // 恢复选区，HideSelection = true 会阻止自动滚动
                box.Select(selStart, selLength);

                NativeMethods.SendMessage(box.Handle, NativeMethods.WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);

                // 恢复 HideSelection 状态
                box.HideSelection = wasHidden;

                box.Invalidate();
            }
        }

        private static void ColorLine(RichTextBox box, string text, int start, int end, Palette p)
        {
            var line = text.Substring(start, end - start);

            // 勾选框是结构、不是内容：压成 ink-soft，让正文当主角（与列表符同一套纪律）。
            // 字体必须显式指定成图标字体——这两个码位在 Unicode 私用区，正文的 CJK
            // 字体没有它们，指望字体链接去兜是兜不到的，会画成豆腐块。
            var mark = LeadingMark.Match(line);
            if (mark.Success) Paint(box, start, mark.Length, p.InkSoft, p.Icon);

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
