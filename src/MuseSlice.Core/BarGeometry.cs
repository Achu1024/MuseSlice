namespace MuseSlice.Core
{
    /// <summary>
    /// 小条的一条硬限制：粘贴大段文本时，着色要不要做。
    /// <para>
    /// 这个类原先还管「小条该多高」——钳行数、按屏幕剩余空间钳搜索页高度。两个模式
    /// 改成固定同一尺寸之后，那些计算全部失去了用途，已经删掉。名字没跟着改：
    /// 它被另外两个工程引用，为一个只剩一条常量的类去改四处 <c>using</c> 不划算。
    /// </para>
    /// </summary>
    public static class BarGeometry
    {
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
    }
}
