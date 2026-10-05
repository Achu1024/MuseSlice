using QuickNote.Core;

namespace QuickNote.SelfCheck
{
    internal static class BarGeometryChecks
    {
        public static void Run()
        {
            Check.Section("BarGeometry");

            const int lineHeight = 22;
            const int chrome = 90;   // 上下状态行 + 内边距

            Check.Equal(BarGeometry.HeightFor(1, lineHeight, chrome), chrome + 3 * lineHeight,
                "1 行内容时高度取下限 3 行");
            Check.Equal(BarGeometry.HeightFor(3, lineHeight, chrome), chrome + 3 * lineHeight,
                "3 行时正好等于下限");
            Check.Equal(BarGeometry.HeightFor(5, lineHeight, chrome), chrome + 5 * lineHeight,
                "5 行时按实际行数");
            Check.Equal(BarGeometry.HeightFor(8, lineHeight, chrome), chrome + 8 * lineHeight,
                "8 行时正好等于上限");
            Check.Equal(BarGeometry.HeightFor(9, lineHeight, chrome), chrome + 8 * lineHeight,
                "9 行时被上限钳制");
            Check.Equal(BarGeometry.HeightFor(500, lineHeight, chrome), chrome + 8 * lineHeight,
                "粘贴 500 行时仍被钳制在上限，不会撑满屏幕");
            Check.Equal(BarGeometry.HeightFor(0, lineHeight, chrome), chrome + 3 * lineHeight,
                "0 行时取下限");
        }
    }
}
