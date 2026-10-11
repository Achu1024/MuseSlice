using MuseSlice.Core;

namespace MuseSlice.SelfCheck
{
    internal static class BarGeometryChecks
    {
        public static void Run()
        {
            Check.Section("BarGeometry");

            Check.That(BarGeometry.ShouldHighlight(0), "空文档需要着色");
            Check.That(BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars),
                "正好等于上限时需要着色");
            Check.That(!BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars + 1),
                "超过上限后不再着色——粘贴大段文本后打字才不会被冻住");
        }
    }
}
