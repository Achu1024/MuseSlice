namespace QuickNote.Core
{
    /// <summary>
    /// 小条高度的纯计算。单独抽出来是因为「粘贴一大段文本」这条路径：
    /// 没有上限的话小条会一路长高直到遮住整个屏幕。
    /// <para>
    /// 只钳行数，不再算总高度——一行到底多高由 RichEdit 自己的行距决定，
    /// 写死在 Core 里的话换字体或换 DPI 就会对不上（字体一换，最后一行的
    /// 下沿会被切掉，或者下面留出一块晾着的空当）。
    /// </para>
    /// </summary>
    public static class BarGeometry
    {
        public const int MinLines = 3;
        public const int MaxLines = 8;

        /// <summary>
        /// 超过这个字符数就不再逐字着色。设计预期是「几百字节」，而粘贴一大段文本
        /// 会打破这个前提：每次按键重扫全文着色会冻住 UI 线程，而 UI 线程一冻
        /// 就卡住全局键盘钩子，超过约 300ms 会被 Windows 静默摘掉。
        /// </summary>
        public const int MaxHighlightChars = 20000;

        public static bool ShouldHighlight(int textLength)
        {
            return textLength <= MaxHighlightChars;
        }

        public static int ClampLines(int lineCount)
        {
            if (lineCount < MinLines) return MinLines;
            if (lineCount > MaxLines) return MaxLines;
            return lineCount;
        }

        /// <summary>
        /// 搜索模式的高度：想要的高度与「屏幕下方还剩多少」取小，再兜一个下限。
        /// <para>
        /// 搜索条目比填写页高得多（一条三行、字号又都是按 DPI 放大的），
        /// 470 逻辑像素乘上 1.25 就可能探出屏幕底部——小条是从上方三分之一处往下长的。
        /// </para>
        /// </summary>
        public static int FitSearchHeight(int desired, int roomBelow, int minHeight)
        {
            var height = desired < roomBelow ? desired : roomBelow;
            return height < minHeight ? minHeight : height;
        }
    }
}
