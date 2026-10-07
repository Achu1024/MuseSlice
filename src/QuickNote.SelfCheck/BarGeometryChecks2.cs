using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    /// <summary>搜索模式高度的钳制。单开一个文件是因为 BarGeometryChecks 已经满了。</summary>
    internal static class SearchHeightChecks
    {
        public static void Run()
        {
            Check.Section("BarGeometry/搜索高度");

            Check.Equal(BarGeometry.FitSearchHeight(600, 900, 300), 600, "屏幕放得下就用想要的高度");
            Check.Equal(BarGeometry.FitSearchHeight(600, 900, 600), 600, "正好等于剩余空间时也用想要的高度");

            Check.Equal(BarGeometry.FitSearchHeight(900, 600, 300), 600, "放不下就钳到剩余空间");
            Check.Equal(BarGeometry.FitSearchHeight(1000, 600, 300), 600, "远超剩余空间时钳到剩余空间");

            Check.Equal(BarGeometry.FitSearchHeight(600, 100, 300), 300, "剩余空间比下限还小时保留下限");
            Check.Equal(BarGeometry.FitSearchHeight(600, -50, 300), 300, "剩余空间为负（窗口比屏幕还靠下）时保留下限");

            Check.Equal(BarGeometry.FitSearchHeight(200, 900, 300), 300, "想要的高度低于下限时抬到下限");
        }
    }
}
