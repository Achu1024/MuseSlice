namespace QuickNote.Core
{
    /// <summary>
    /// 小条高度的纯计算。单独抽出来是因为「粘贴一大段文本」这条路径：
    /// 没有上限的话小条会一路长高直到遮住整个屏幕。
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

        public static int HeightFor(int lineCount, int lineHeight, int chromeHeight)
        {
            var lines = lineCount;
            if (lines < MinLines) lines = MinLines;
            if (lines > MaxLines) lines = MaxLines;
            return chromeHeight + lines * lineHeight;
        }
    }
}
