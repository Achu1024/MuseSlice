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

        public static int HeightFor(int lineCount, int lineHeight, int chromeHeight)
        {
            var lines = lineCount;
            if (lines < MinLines) lines = MinLines;
            if (lines > MaxLines) lines = MaxLines;
            return chromeHeight + lines * lineHeight;
        }
    }
}
