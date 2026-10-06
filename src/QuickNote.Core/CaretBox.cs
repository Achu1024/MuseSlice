namespace QuickNote.Core
{
    /// <summary>
    /// 自绘光标在行盒里占的上下范围。
    /// <para>
    /// **不能拿行距当光标高度**：CJK 字体的行距里含着大量行间空白（Noto Sans SC
    /// 约 1.45em，而字身只有 1.16em 上下）。用行距当高度，那根线会比文字高出一大截，
    /// 上头顶到分隔线——看起来像光标贴着横线，很不舒服。
    /// </para>
    /// <para>
    /// 用字身（升 + 降）当高度，再在行盒里居中，光标才与文字齐平。
    /// </para>
    /// <para>
    /// 只做整数运算，度量值由界面层从 <c>FontFamily</c> 取（Core 不引用 System.Drawing）。
    /// </para>
    /// </summary>
    public static class CaretBox
    {
        /// <param name="lineSpacing">行距，物理像素。</param>
        /// <param name="ascent">字身升部。</param>
        /// <param name="descent">字身降部。</param>
        /// <param name="top">算出的上边距（相对行盒顶）。</param>
        /// <param name="height">算出的光标高度。</param>
        public static void Range(int lineSpacing, int ascent, int descent,
                                 out int top, out int height)
        {
            height = ascent + descent;

            if (lineSpacing <= 0)
            {
                // 行距拿不到：至少给一个不越界的高度，且绝不除零
                if (height < 1) height = 1;
                top = 0;
                return;
            }

            if (height < 1) height = lineSpacing;        // 字身也拿不到，退回行距
            if (height > lineSpacing) height = lineSpacing;   // 字身再大也不越出自己那一行

            // 余量上下均分，奇数时上面少 1px——光标比文字略低一点比略高好看
            top = (lineSpacing - height) / 2;
        }
    }
}
