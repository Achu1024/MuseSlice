using MuseSlice.Core;

namespace MuseSlice.SelfCheck
{
    /// <summary>
    /// 代码复查揪出的三处缺陷的断言。都是把判定抽成纯函数之后才可测的。
    /// </summary>
    internal static class ReviewFixChecks
    {
        public static void Run()
        {
            Check.Section("ReviewFix");

            // I2：粘贴超长文本后，每敲一个字都重扫全文着色会冻住 UI 线程，
            // 而 UI 线程一冻就卡住全局键盘钩子。超过阈值就不再逐字着色。
            Check.That(BarGeometry.ShouldHighlight(0), "空文档需要着色");
            Check.That(BarGeometry.ShouldHighlight(500), "几百字节（设计预期规模）需要着色");
            Check.That(BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars),
                "正好等于上限时需要着色");
            Check.That(!BarGeometry.ShouldHighlight(BarGeometry.MaxHighlightChars + 1),
                "超过上限后不再着色——粘贴大段文本后打字才不会被冻住");

            // I4：设置里把日记目录清空会让 NoteStore 构造抛异常，
            // 而异常逃到全局处理器只写日志，使用者看到的是「点了确定什么都没发生」。
            Check.Equal(AppConfig.ValidateNotesDir(""), "日记目录不能为空", "空目录被拒绝");
            Check.Equal(AppConfig.ValidateNotesDir("   "), "日记目录不能为空", "纯空白目录被拒绝");
            Check.Equal(AppConfig.ValidateNotesDir(null), "日记目录不能为空", "null 目录被拒绝");
            Check.Equal(AppConfig.ValidateNotesDir(@"D:\Project\daliy_log\notes"), null, "正常目录通过");
            Check.Equal(AppConfig.ValidateNotesDir(@"D:\不存在的盘\nope"), null,
                "不存在的路径不在设置时拦——留给写入时报错，因为网络盘可能稍后才挂上");

            // I5：结果多于可视行数时，↑↓ 会把选中项走到屏幕外，
            // 然后回车打开一条使用者根本看不见的记录。
            Check.Equal(SearchScroll.FirstVisible(0, 5, 100, 0), 0, "选中项在窗口内时不动");
            Check.Equal(SearchScroll.FirstVisible(4, 5, 100, 0), 0, "选中最后一条可视行时仍不动");
            Check.Equal(SearchScroll.FirstVisible(5, 5, 100, 0), 1, "选中项越过下边界时下滚一行");
            Check.Equal(SearchScroll.FirstVisible(6, 5, 100, 0), 2, "继续下滚保持选中项在末行");
            Check.Equal(SearchScroll.FirstVisible(9, 5, 100, 5), 5, "选中项在窗口内时保持当前起点");
            Check.Equal(SearchScroll.FirstVisible(4, 5, 100, 5), 4, "向上越界时上滚一行");
            Check.Equal(SearchScroll.FirstVisible(20, 5, 8, 0), 3,
                "结果不足时起点被总数钳制，不会滚出结果范围");
            Check.Equal(SearchScroll.FirstVisible(0, 5, 0, 0), 0, "没有结果时不越界");
        }
    }
}
