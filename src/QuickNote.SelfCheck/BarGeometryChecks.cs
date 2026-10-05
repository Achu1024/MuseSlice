using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class BarGeometryChecks
    {
        public static void Run()
        {
            Check.Section("BarGeometry");

            Check.Equal(BarGeometry.ClampLines(1), 3, "1 行内容时高度取下限 3 行");
            Check.Equal(BarGeometry.ClampLines(3), 3, "3 行时正好等于下限");
            Check.Equal(BarGeometry.ClampLines(5), 5, "5 行时按实际行数");
            Check.Equal(BarGeometry.ClampLines(8), 8, "8 行时正好等于上限");
            Check.Equal(BarGeometry.ClampLines(9), 8, "9 行时被上限钳制");
            Check.Equal(BarGeometry.ClampLines(500), 8, "粘贴 500 行时仍被钳制在上限，不会撑满屏幕");
            Check.Equal(BarGeometry.ClampLines(0), 3, "0 行时取下限");
            Check.Equal(BarGeometry.ClampLines(-7), 3, "负数行数取下限");

            Check.That(BarGeometry.ShouldHighlight(0), "空文档需要着色");
            Check.That(BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars),
                "正好等于上限时需要着色");
            Check.That(!BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars + 1),
                "超过上限后不再着色——粘贴大段文本后打字才不会被冻住");
        }
    }
}
